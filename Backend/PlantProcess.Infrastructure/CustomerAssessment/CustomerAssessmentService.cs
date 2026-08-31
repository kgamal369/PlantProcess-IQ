// ============================================================================
// Customer assessment persistence service.
//
// CONCURRENCY AUTHORITY
//   Database uniqueness and row locking are the authority. There is no static
//   or process-wide lock anywhere in this file: two application instances must
//   behave the same way as two threads, and a process lock would be a lie the
//   moment the product is run twice.
//
//   Version allocation is serialised by taking a row lock on the assessment
//   lineage row (SELECT ... FOR UPDATE). The lock is scoped to one lineage, so
//   unrelated lineages never serialise against each other.
//
// IDEMPOTENCE
//   The semantic fingerprint decides. Same normalised intake plus same
//   contract and rule versions returns the existing version. A new request id,
//   a new timestamp, a regenerated identifier or a different collection order
//   cannot create a version, because none of them reaches the fingerprint.
//
// AUTHORITY BARRIER
//   Every statement in this file touches ppiq_meta.customer_assessments or
//   ppiq_meta.customer_assessment_versions. Nothing here writes a definition,
//   a definition version, a dimension, a measure, a schema mapping or a
//   published relationship, and no such service is injected.
// ============================================================================

using System;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.CustomerAssessment;

namespace PlantProcess.Infrastructure.CustomerAssessment
{
    public interface ICustomerAssessmentConnectionFactory
    {
        NpgsqlConnection Create();
    }

    public sealed class CustomerAssessmentConnectionFactory : ICustomerAssessmentConnectionFactory
    {
        private readonly string _connectionString;

        public CustomerAssessmentConnectionFactory(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "A connection string is required. There is no fallback connection.",
                    nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        public NpgsqlConnection Create()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }

    public sealed class CustomerAssessmentService : ICustomerAssessmentService
    {
        private const string PostgresUniqueViolation = "23505";
        private const int MaxAllocationAttempts = 3;

        private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

        private readonly ICustomerAssessmentConnectionFactory _connections;
        private readonly ICustomerAssessmentEngine _engine;
        private readonly ICustomerAssessmentSemanticVersionProvider _versions;

        public CustomerAssessmentService(
            ICustomerAssessmentConnectionFactory connections,
            ICustomerAssessmentEngine engine,
            ICustomerAssessmentSemanticVersionProvider versions)
        {
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _versions = versions ?? throw new ArgumentNullException(nameof(versions));
        }

        private static JsonSerializerOptions BuildJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null
            };

            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        // ------------------------------------------------------------------
        // Assess
        // ------------------------------------------------------------------

        public async Task<AssessmentOutcome<CustomerAssessmentVersionResult>> AssessAsync(
            Guid tenantId,
            CustomerIntake intake,
            CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
            {
                return AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(
                    AssessmentRefusalReason.TenantNotResolved);
            }

            if (intake == null)
            {
                return AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(
                    AssessmentRefusalReason.IntakeInvalid);
            }

            string lineageCode = CustomerAssessmentNormalization.TrimOnly(intake.LineageCode);
            if (lineageCode.Length == 0 || lineageCode.Length > 128)
            {
                return AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(
                    AssessmentRefusalReason.IntakeInvalid);
            }

            string contractVersion = _versions.ContractVersion;
            string ruleVersion = _versions.RuleVersion;

            string fingerprint = CustomerAssessmentNormalization.ComputeFingerprint(
                intake, contractVersion, ruleVersion);

            CustomerAssessmentReport report = _engine.Evaluate(intake);

            string intakeJson = JsonSerializer.Serialize(intake, JsonOptions);
            string reportJson = JsonSerializer.Serialize(report, JsonOptions);

            for (int attempt = 1; attempt <= MaxAllocationAttempts; attempt++)
            {
                try
                {
                    CustomerAssessmentVersionResult result = await AssessOnceAsync(
                        tenantId,
                        lineageCode,
                        intake.DisplayName,
                        contractVersion,
                        ruleVersion,
                        fingerprint,
                        intakeJson,
                        reportJson,
                        report,
                        cancellationToken).ConfigureAwait(false);

                    return AssessmentOutcome<CustomerAssessmentVersionResult>.Success(result);
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresUniqueViolation
                                                   && attempt < MaxAllocationAttempts)
                {
                    // A concurrent caller won the same allocation. Re-read and
                    // converge rather than inventing a second version.
                }
            }

            CustomerAssessmentVersionResult converged = await AssessOnceAsync(
                tenantId,
                lineageCode,
                intake.DisplayName,
                contractVersion,
                ruleVersion,
                fingerprint,
                intakeJson,
                reportJson,
                report,
                cancellationToken).ConfigureAwait(false);

            return AssessmentOutcome<CustomerAssessmentVersionResult>.Success(converged);
        }

        private async Task<CustomerAssessmentVersionResult> AssessOnceAsync(
            Guid tenantId,
            string lineageCode,
            string? displayName,
            string contractVersion,
            string ruleVersion,
            string fingerprint,
            string intakeJson,
            string reportJson,
            CustomerAssessmentReport report,
            CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = _connections.Create();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using NpgsqlTransaction transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            Guid assessmentId = await EnsureLineageAsync(
                connection, transaction, tenantId, lineageCode, displayName, cancellationToken)
                .ConfigureAwait(false);

            // Row-scoped serialisation of version allocation for this lineage
            // only. Unrelated lineages are not touched by this lock.
            await using (NpgsqlCommand lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                lockCommand.CommandText =
                    "SELECT assessment_id FROM ppiq_meta.customer_assessments " +
                    "WHERE assessment_id = @assessment_id FOR UPDATE;";
                lockCommand.Parameters.Add(new NpgsqlParameter("assessment_id", NpgsqlDbType.Uuid)
                {
                    Value = assessmentId
                });

                await lockCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            CustomerAssessmentVersionResult? existing = await ReadByFingerprintAsync(
                connection, transaction, assessmentId, lineageCode, fingerprint, cancellationToken)
                .ConfigureAwait(false);

            if (existing != null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                existing.Reused = true;
                return existing;
            }

            int nextVersion;
            await using (NpgsqlCommand nextCommand = connection.CreateCommand())
            {
                nextCommand.Transaction = transaction;
                nextCommand.CommandText =
                    "SELECT COALESCE(MAX(version_number), 0) + 1 " +
                    "FROM ppiq_meta.customer_assessment_versions WHERE assessment_id = @assessment_id;";
                nextCommand.Parameters.Add(new NpgsqlParameter("assessment_id", NpgsqlDbType.Uuid)
                {
                    Value = assessmentId
                });

                object? scalar = await nextCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                nextVersion = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }

            Guid versionId = Guid.NewGuid();
            DateTimeOffset createdAtUtc;

            await using (NpgsqlCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText =
                    "INSERT INTO ppiq_meta.customer_assessment_versions " +
                    "(assessment_version_id, assessment_id, version_number, contract_version, rule_version, " +
                    " semantic_fingerprint, intake_json, report_json) " +
                    "VALUES (@id, @assessment_id, @version_number, @contract_version, @rule_version, " +
                    " @fingerprint, @intake_json, @report_json) " +
                    "RETURNING created_at_utc;";

                insert.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = versionId });
                insert.Parameters.Add(new NpgsqlParameter("assessment_id", NpgsqlDbType.Uuid) { Value = assessmentId });
                insert.Parameters.Add(new NpgsqlParameter("version_number", NpgsqlDbType.Integer) { Value = nextVersion });
                insert.Parameters.Add(new NpgsqlParameter("contract_version", NpgsqlDbType.Varchar) { Value = contractVersion });
                insert.Parameters.Add(new NpgsqlParameter("rule_version", NpgsqlDbType.Varchar) { Value = ruleVersion });
                insert.Parameters.Add(new NpgsqlParameter("fingerprint", NpgsqlDbType.Char) { Value = fingerprint });
                insert.Parameters.Add(new NpgsqlParameter("intake_json", NpgsqlDbType.Jsonb) { Value = intakeJson });
                insert.Parameters.Add(new NpgsqlParameter("report_json", NpgsqlDbType.Jsonb) { Value = reportJson });

                object? scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                createdAtUtc = ToUtcOffset(scalar);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new CustomerAssessmentVersionResult
            {
                AssessmentId = assessmentId,
                AssessmentVersionId = versionId,
                LineageCode = lineageCode,
                VersionNumber = nextVersion,
                ContractVersion = contractVersion,
                RuleVersion = ruleVersion,
                SemanticFingerprint = fingerprint,
                CreatedAtUtc = createdAtUtc,
                Reused = false,
                Report = report
            };
        }

        private static async Task<Guid> EnsureLineageAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid tenantId,
            string lineageCode,
            string? displayName,
            CancellationToken cancellationToken)
        {
            await using (NpgsqlCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText =
                    "INSERT INTO ppiq_meta.customer_assessments (assessment_id, tenant_id, lineage_code, display_name) " +
                    "VALUES (@id, @tenant_id, @lineage_code, @display_name) " +
                    "ON CONFLICT (tenant_id, lineage_code) DO NOTHING;";

                insert.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = Guid.NewGuid() });
                insert.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
                insert.Parameters.Add(new NpgsqlParameter("lineage_code", NpgsqlDbType.Varchar) { Value = lineageCode });
                insert.Parameters.Add(new NpgsqlParameter("display_name", NpgsqlDbType.Varchar)
                {
                    Value = string.IsNullOrWhiteSpace(displayName) ? (object)DBNull.Value : displayName!.Trim()
                });

                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using NpgsqlCommand select = connection.CreateCommand();
            select.Transaction = transaction;
            select.CommandText =
                "SELECT assessment_id FROM ppiq_meta.customer_assessments " +
                "WHERE tenant_id = @tenant_id AND lineage_code = @lineage_code;";
            select.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
            select.Parameters.Add(new NpgsqlParameter("lineage_code", NpgsqlDbType.Varchar) { Value = lineageCode });

            object? found = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (found == null || found == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "The assessment lineage could not be resolved after insertion. This is a database contract failure, not a caller error.");
            }

            return (Guid)found;
        }

        // ------------------------------------------------------------------
        // Reads. Every read is tenant-scoped in SQL, never in memory.
        // ------------------------------------------------------------------

        public async Task<AssessmentOutcome<CustomerAssessmentVersionResult>> GetLatestAsync(
            Guid tenantId,
            string lineageCode,
            CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
            {
                return AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(
                    AssessmentRefusalReason.TenantNotResolved);
            }

            CustomerAssessmentVersionResult? found = await ReadOneAsync(
                tenantId,
                CustomerAssessmentNormalization.TrimOnly(lineageCode),
                null,
                cancellationToken).ConfigureAwait(false);

            return found == null
                ? AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(AssessmentRefusalReason.AssessmentNotFound)
                : AssessmentOutcome<CustomerAssessmentVersionResult>.Success(found);
        }

        public async Task<AssessmentOutcome<CustomerAssessmentVersionResult>> GetVersionAsync(
            Guid tenantId,
            string lineageCode,
            int versionNumber,
            CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
            {
                return AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(
                    AssessmentRefusalReason.TenantNotResolved);
            }

            if (versionNumber <= 0)
            {
                return AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(
                    AssessmentRefusalReason.VersionNotFound);
            }

            CustomerAssessmentVersionResult? found = await ReadOneAsync(
                tenantId,
                CustomerAssessmentNormalization.TrimOnly(lineageCode),
                versionNumber,
                cancellationToken).ConfigureAwait(false);

            return found == null
                ? AssessmentOutcome<CustomerAssessmentVersionResult>.Refused(AssessmentRefusalReason.VersionNotFound)
                : AssessmentOutcome<CustomerAssessmentVersionResult>.Success(found);
        }

        public async Task<AssessmentOutcome<CustomerAssessmentDiff>> GetDiffAsync(
            Guid tenantId,
            string lineageCode,
            int fromVersionNumber,
            int toVersionNumber,
            CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
            {
                return AssessmentOutcome<CustomerAssessmentDiff>.Refused(
                    AssessmentRefusalReason.TenantNotResolved);
            }

            AssessmentOutcome<CustomerAssessmentVersionResult> from = await GetVersionAsync(
                tenantId, lineageCode, fromVersionNumber, cancellationToken).ConfigureAwait(false);

            if (!from.Succeeded)
            {
                return AssessmentOutcome<CustomerAssessmentDiff>.Refused(from.Reason);
            }

            AssessmentOutcome<CustomerAssessmentVersionResult> to = await GetVersionAsync(
                tenantId, lineageCode, toVersionNumber, cancellationToken).ConfigureAwait(false);

            if (!to.Succeeded)
            {
                return AssessmentOutcome<CustomerAssessmentDiff>.Refused(to.Reason);
            }

            CustomerAssessmentDiff diff = CustomerAssessmentDiffCalculator.Compute(
                from.Value!.Report,
                from.Value!.VersionNumber,
                to.Value!.Report,
                to.Value!.VersionNumber);

            return AssessmentOutcome<CustomerAssessmentDiff>.Success(diff);
        }

        private async Task<CustomerAssessmentVersionResult?> ReadOneAsync(
            Guid tenantId,
            string lineageCode,
            int? versionNumber,
            CancellationToken cancellationToken)
        {
            if (lineageCode.Length == 0)
            {
                return null;
            }

            await using NpgsqlConnection connection = _connections.Create();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT v.assessment_version_id, v.assessment_id, v.version_number, v.contract_version, " +
                "       v.rule_version, v.semantic_fingerprint, v.report_json, v.created_at_utc " +
                "FROM ppiq_meta.customer_assessment_versions v " +
                "JOIN ppiq_meta.customer_assessments a ON a.assessment_id = v.assessment_id " +
                "WHERE a.tenant_id = @tenant_id AND a.lineage_code = @lineage_code " +
                (versionNumber.HasValue ? "AND v.version_number = @version_number " : string.Empty) +
                "ORDER BY v.version_number DESC LIMIT 1;";

            command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
            command.Parameters.Add(new NpgsqlParameter("lineage_code", NpgsqlDbType.Varchar) { Value = lineageCode });

            if (versionNumber.HasValue)
            {
                command.Parameters.Add(new NpgsqlParameter("version_number", NpgsqlDbType.Integer)
                {
                    Value = versionNumber.Value
                });
            }

            await using NpgsqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return Materialise(reader, lineageCode);
        }

        private static async Task<CustomerAssessmentVersionResult?> ReadByFingerprintAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid assessmentId,
            string lineageCode,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "SELECT v.assessment_version_id, v.assessment_id, v.version_number, v.contract_version, " +
                "       v.rule_version, v.semantic_fingerprint, v.report_json, v.created_at_utc " +
                "FROM ppiq_meta.customer_assessment_versions v " +
                "WHERE v.assessment_id = @assessment_id AND v.semantic_fingerprint = @fingerprint;";

            command.Parameters.Add(new NpgsqlParameter("assessment_id", NpgsqlDbType.Uuid) { Value = assessmentId });
            command.Parameters.Add(new NpgsqlParameter("fingerprint", NpgsqlDbType.Char) { Value = fingerprint });

            await using NpgsqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return Materialise(reader, lineageCode);
        }


        /// <summary>
        /// Npgsql materialises a timestamptz as a boxed DateTime with
        /// Kind=Utc, not as a DateTimeOffset, and unboxing across those types
        /// throws. Every timestamp read goes through this one conversion so
        /// the provider's representation is handled in exactly one place.
        /// </summary>
        private static DateTimeOffset ToUtcOffset(object? value)
        {
            if (value is DateTimeOffset offset)
            {
                return offset.ToUniversalTime();
            }

            if (value is DateTime dateTime)
            {
                if (dateTime.Kind == DateTimeKind.Unspecified)
                {
                    dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                }

                return new DateTimeOffset(dateTime.ToUniversalTime());
            }

            throw new InvalidOperationException(
                "A timestamp column materialised as "
                + (value == null ? "null" : value.GetType().FullName)
                + ", which this service does not accept.");
        }

        private static CustomerAssessmentVersionResult Materialise(NpgsqlDataReader reader, string lineageCode)
        {
            string reportJson = reader.GetString(6);

            CustomerAssessmentReport report =
                JsonSerializer.Deserialize<CustomerAssessmentReport>(reportJson, JsonOptions)
                ?? throw new InvalidOperationException(
                    "A persisted assessment report could not be read back. The stored report is the immutable record and is never silently replaced.");

            return new CustomerAssessmentVersionResult
            {
                AssessmentVersionId = reader.GetGuid(0),
                AssessmentId = reader.GetGuid(1),
                VersionNumber = reader.GetInt32(2),
                ContractVersion = reader.GetString(3),
                RuleVersion = reader.GetString(4),
                SemanticFingerprint = reader.GetString(5).Trim(),
                CreatedAtUtc = ToUtcOffset(reader.GetValue(7)),
                LineageCode = lineageCode,
                Reused = false,
                Report = report
            };
        }
    }
}
