// ============================================================================
// Customer assessment persistence integration tests.
//
// These run against a real PostgreSQL database that has been built through the
// canonical path including 833. They are the only place where idempotence,
// immutability, concurrency and the authority barrier can honestly be proven:
// an in-memory fake cannot fail a unique constraint and cannot take a row lock.
//
// CONNECTION
//   The connection string is read from PPIQ_TEST_ASSESSMENT_DB, falling back to
//   ConnectionStrings__PlantProcessDb. There is no hard-coded database name:
//   running these against the wrong database would invalidate every result
//   silently, which has already cost this programme once.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using PlantProcess.Application.CustomerAssessment;
using PlantProcess.Application.Tests.CustomerAssessment;
using PlantProcess.Infrastructure.CustomerAssessment;
using Xunit;

namespace PlantProcess.Infrastructure.Tests.CustomerAssessment
{
    public sealed class RuleVersionOverride : ICustomerAssessmentSemanticVersionProvider
    {
        public RuleVersionOverride(string contractVersion, string ruleVersion)
        {
            ContractVersion = contractVersion;
            RuleVersion = ruleVersion;
        }

        public string ContractVersion { get; }

        public string RuleVersion { get; }
    }

    public sealed class CustomerAssessmentPersistenceTests : IAsyncLifetime
    {
        private readonly List<Guid> _createdAssessments = new List<Guid>();
        private string _connectionString = string.Empty;

        public Task InitializeAsync()
        {
            _connectionString =
                Environment.GetEnvironmentVariable("PPIQ_TEST_ASSESSMENT_DB")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
                ?? throw new InvalidOperationException(
                    "No assessment test database is configured. Refusing to run against an unnamed database.");

            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            // Children before parents. A teardown that violates a foreign key
            // fails a test whose body already passed.
            await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            foreach (Guid assessmentId in _createdAssessments)
            {
                await ExecuteAsync(
                    connection,
                    "DELETE FROM ppiq_meta.customer_assessment_versions WHERE assessment_id = @id;",
                    assessmentId).ConfigureAwait(false);

                await ExecuteAsync(
                    connection,
                    "DELETE FROM ppiq_meta.customer_assessments WHERE assessment_id = @id;",
                    assessmentId).ConfigureAwait(false);
            }
        }

        private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, Guid id)
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private CustomerAssessmentService NewService(string ruleVersion = "1.0.0", string contractVersion = "1.0.0")
        {
            return new CustomerAssessmentService(
                new CustomerAssessmentConnectionFactory(_connectionString),
                new CustomerAssessmentEngine(),
                new RuleVersionOverride(contractVersion, ruleVersion));
        }

        private static CustomerIntake WithLineage(CustomerIntake intake, string lineageCode)
        {
            return new CustomerIntake
            {
                LineageCode = lineageCode,
                DisplayName = intake.DisplayName,
                Sources = intake.Sources,
                Entities = intake.Entities,
                Declarations = intake.Declarations
            };
        }

        private static string NewLineage()
        {
            return "T213-" + Guid.NewGuid().ToString("N");
        }

        private void Track(CustomerAssessmentVersionResult result)
        {
            if (!_createdAssessments.Contains(result.AssessmentId))
            {
                _createdAssessments.Add(result.AssessmentId);
            }
        }

        // ------------------------------------------------------------------
        // Idempotence
        // ------------------------------------------------------------------

        [Fact]
        public async Task Assessing_the_same_intake_twice_reuses_the_existing_version()
        {
            CustomerAssessmentService service = NewService();
            Guid tenant = Guid.NewGuid();
            CustomerIntake intake = WithLineage(ForeignIntakeFixture.V1(), NewLineage());

            AssessmentOutcome<CustomerAssessmentVersionResult> first =
                await service.AssessAsync(tenant, intake, CancellationToken.None);
            Assert.True(first.Succeeded);
            Track(first.Value!);

            AssessmentOutcome<CustomerAssessmentVersionResult> second =
                await service.AssessAsync(tenant, intake, CancellationToken.None);
            Assert.True(second.Succeeded);

            Assert.Equal(1, first.Value!.VersionNumber);
            Assert.Equal(1, second.Value!.VersionNumber);
            Assert.Equal(first.Value!.AssessmentVersionId, second.Value!.AssessmentVersionId);
            Assert.False(first.Value!.Reused);
            Assert.True(second.Value!.Reused);
        }

        [Fact]
        public async Task A_reordered_intake_with_a_new_display_name_creates_no_version()
        {
            CustomerAssessmentService service = NewService();
            Guid tenant = Guid.NewGuid();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> first = await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Track(first.Value!);

            AssessmentOutcome<CustomerAssessmentVersionResult> second = await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V1Shuffled(), lineage), CancellationToken.None);

            Assert.True(second.Value!.Reused);
            Assert.Equal(first.Value!.AssessmentVersionId, second.Value!.AssessmentVersionId);
        }

        [Fact]
        public async Task A_changed_rule_version_creates_a_new_immutable_version()
        {
            Guid tenant = Guid.NewGuid();
            string lineage = NewLineage();
            CustomerIntake intake = WithLineage(ForeignIntakeFixture.V1(), lineage);

            AssessmentOutcome<CustomerAssessmentVersionResult> atRuleOne =
                await NewService("1.0.0").AssessAsync(tenant, intake, CancellationToken.None);
            Track(atRuleOne.Value!);

            AssessmentOutcome<CustomerAssessmentVersionResult> atRuleTwo =
                await NewService("1.0.1").AssessAsync(tenant, intake, CancellationToken.None);

            Assert.Equal(1, atRuleOne.Value!.VersionNumber);
            Assert.Equal(2, atRuleTwo.Value!.VersionNumber);
            Assert.False(atRuleTwo.Value!.Reused);
            Assert.NotEqual(atRuleOne.Value!.SemanticFingerprint, atRuleTwo.Value!.SemanticFingerprint);
        }

        // ------------------------------------------------------------------
        // Immutability
        // ------------------------------------------------------------------

        [Fact]
        public async Task Version_one_survives_unchanged_after_version_two_is_created()
        {
            CustomerAssessmentService service = NewService();
            Guid tenant = Guid.NewGuid();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> v1 = await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Track(v1.Value!);

            string v1Statement = v1.Value!.Report.Sections
                .First(s => s.SectionCode == AssessmentSectionCodes.TransitionDefinition).Statement;

            AssessmentOutcome<CustomerAssessmentVersionResult> v2 = await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V2(), lineage), CancellationToken.None);

            Assert.Equal(2, v2.Value!.VersionNumber);

            AssessmentOutcome<CustomerAssessmentVersionResult> reread =
                await service.GetVersionAsync(tenant, lineage, 1, CancellationToken.None);

            Assert.True(reread.Succeeded);
            Assert.Equal(1, reread.Value!.VersionNumber);
            Assert.Equal(
                v1Statement,
                reread.Value!.Report.Sections
                    .First(s => s.SectionCode == AssessmentSectionCodes.TransitionDefinition).Statement);

            Assert.Equal(AssessmentStatus.Unknown, reread.Value!.Report.Sections
                .First(s => s.SectionCode == AssessmentSectionCodes.TransitionDefinition).Status);

            Assert.Equal(AssessmentStatus.Known, v2.Value!.Report.Sections
                .First(s => s.SectionCode == AssessmentSectionCodes.TransitionDefinition).Status);
        }

        [Fact]
        public async Task An_in_place_rewrite_of_a_persisted_version_is_refused_by_the_database()
        {
            CustomerAssessmentService service = NewService();
            Guid tenant = Guid.NewGuid();

            AssessmentOutcome<CustomerAssessmentVersionResult> created = await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V1(), NewLineage()), CancellationToken.None);
            Track(created.Value!);

            await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand update = connection.CreateCommand();
            update.CommandText =
                "UPDATE ppiq_meta.customer_assessment_versions SET report_json = '{}'::jsonb " +
                "WHERE assessment_version_id = @id;";
            update.Parameters.AddWithValue("id", created.Value!.AssessmentVersionId);

            PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(
                () => update.ExecuteNonQueryAsync());

            Assert.Contains("PPIQ_833_ASSESSMENT_VERSION_IMMUTABLE", refusal.MessageText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_diff_between_two_persisted_versions_is_computed_from_the_stored_reports()
        {
            CustomerAssessmentService service = NewService();
            Guid tenant = Guid.NewGuid();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> v1 = await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Track(v1.Value!);

            await service.AssessAsync(
                tenant, WithLineage(ForeignIntakeFixture.V2(), lineage), CancellationToken.None);

            AssessmentOutcome<CustomerAssessmentDiff> diff =
                await service.GetDiffAsync(tenant, lineage, 1, 2, CancellationToken.None);

            Assert.True(diff.Succeeded);
            Assert.Equal(4, diff.Value!.Entries.Count(e => e.ChangeKind == AssessmentChangeKinds.SectionStatusChanged));
            Assert.True(diff.Value!.ReadinessChanged);
        }

        // ------------------------------------------------------------------
        // Concurrency. Independent connections, no shared context.
        // ------------------------------------------------------------------

        [Fact]
        public async Task Identical_concurrent_first_assessments_converge_on_one_version()
        {
            Guid tenant = Guid.NewGuid();
            string lineage = NewLineage();
            CustomerIntake intake = WithLineage(ForeignIntakeFixture.V1(), lineage);

            Task<AssessmentOutcome<CustomerAssessmentVersionResult>> left =
                NewService().AssessAsync(tenant, intake, CancellationToken.None);
            Task<AssessmentOutcome<CustomerAssessmentVersionResult>> right =
                NewService().AssessAsync(tenant, intake, CancellationToken.None);

            AssessmentOutcome<CustomerAssessmentVersionResult>[] results =
                await Task.WhenAll(left, right);

            Assert.All(results, r => Assert.True(r.Succeeded));
            Track(results[0].Value!);

            Assert.Equal(results[0].Value!.AssessmentId, results[1].Value!.AssessmentId);
            Assert.Equal(results[0].Value!.AssessmentVersionId, results[1].Value!.AssessmentVersionId);
            Assert.Equal(1, await CountVersionsAsync(results[0].Value!.AssessmentId));
        }

        [Fact]
        public async Task Different_concurrent_first_assessments_produce_one_lineage_and_two_monotonic_versions()
        {
            Guid tenant = Guid.NewGuid();
            string lineage = NewLineage();

            Task<AssessmentOutcome<CustomerAssessmentVersionResult>> left =
                NewService().AssessAsync(tenant, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Task<AssessmentOutcome<CustomerAssessmentVersionResult>> right =
                NewService().AssessAsync(tenant, WithLineage(ForeignIntakeFixture.V2(), lineage), CancellationToken.None);

            AssessmentOutcome<CustomerAssessmentVersionResult>[] results = await Task.WhenAll(left, right);

            Assert.All(results, r => Assert.True(r.Succeeded));
            Track(results[0].Value!);

            Assert.Equal(results[0].Value!.AssessmentId, results[1].Value!.AssessmentId);

            List<int> versions = results.Select(r => r.Value!.VersionNumber).OrderBy(v => v).ToList();
            Assert.Equal(new List<int> { 1, 2 }, versions);
            Assert.Equal(2, await CountVersionsAsync(results[0].Value!.AssessmentId));
        }

        [Fact]
        public async Task Unrelated_lineages_do_not_serialise_against_each_other()
        {
            Guid tenant = Guid.NewGuid();

            Task<AssessmentOutcome<CustomerAssessmentVersionResult>> left =
                NewService().AssessAsync(tenant, WithLineage(ForeignIntakeFixture.V1(), NewLineage()), CancellationToken.None);
            Task<AssessmentOutcome<CustomerAssessmentVersionResult>> right =
                NewService().AssessAsync(tenant, WithLineage(ForeignIntakeFixture.V1(), NewLineage()), CancellationToken.None);

            AssessmentOutcome<CustomerAssessmentVersionResult>[] results = await Task.WhenAll(left, right);

            Assert.All(results, r => Assert.True(r.Succeeded));
            foreach (AssessmentOutcome<CustomerAssessmentVersionResult> result in results)
            {
                Track(result.Value!);
                Assert.Equal(1, result.Value!.VersionNumber);
            }

            Assert.NotEqual(results[0].Value!.AssessmentId, results[1].Value!.AssessmentId);
        }

        private async Task<int> CountVersionsAsync(Guid assessmentId)
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM ppiq_meta.customer_assessment_versions WHERE assessment_id = @id;";
            command.Parameters.AddWithValue("id", assessmentId);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        // ------------------------------------------------------------------
        // Tenant isolation
        // ------------------------------------------------------------------

        [Fact]
        public async Task An_unresolved_tenant_refuses_and_mutates_nothing()
        {
            CustomerAssessmentService service = NewService();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> outcome = await service.AssessAsync(
                Guid.Empty, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);

            Assert.False(outcome.Succeeded);
            Assert.Equal(AssessmentRefusalReason.TenantNotResolved, outcome.Reason);
            Assert.Equal(0, await CountLineagesAsync(lineage));
        }

        [Fact]
        public async Task Tenant_A_cannot_read_the_latest_version_belonging_to_tenant_B()
        {
            CustomerAssessmentService service = NewService();
            Guid tenantB = Guid.NewGuid();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> owned = await service.AssessAsync(
                tenantB, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Track(owned.Value!);

            AssessmentOutcome<CustomerAssessmentVersionResult> intruder =
                await service.GetLatestAsync(Guid.NewGuid(), lineage, CancellationToken.None);

            Assert.False(intruder.Succeeded);
            Assert.Equal(AssessmentRefusalReason.AssessmentNotFound, intruder.Reason);
        }

        [Fact]
        public async Task Tenant_A_cannot_read_an_exact_version_belonging_to_tenant_B()
        {
            CustomerAssessmentService service = NewService();
            Guid tenantB = Guid.NewGuid();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> owned = await service.AssessAsync(
                tenantB, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Track(owned.Value!);

            AssessmentOutcome<CustomerAssessmentVersionResult> intruder =
                await service.GetVersionAsync(Guid.NewGuid(), lineage, 1, CancellationToken.None);

            Assert.False(intruder.Succeeded);
            Assert.Equal(AssessmentRefusalReason.VersionNotFound, intruder.Reason);
        }

        [Fact]
        public async Task Tenant_A_cannot_diff_versions_belonging_to_tenant_B()
        {
            CustomerAssessmentService service = NewService();
            Guid tenantB = Guid.NewGuid();
            string lineage = NewLineage();

            AssessmentOutcome<CustomerAssessmentVersionResult> v1 = await service.AssessAsync(
                tenantB, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            Track(v1.Value!);

            await service.AssessAsync(
                tenantB, WithLineage(ForeignIntakeFixture.V2(), lineage), CancellationToken.None);

            AssessmentOutcome<CustomerAssessmentDiff> intruder =
                await service.GetDiffAsync(Guid.NewGuid(), lineage, 1, 2, CancellationToken.None);

            Assert.False(intruder.Succeeded);
            Assert.Equal(AssessmentRefusalReason.VersionNotFound, intruder.Reason);
        }

        [Fact]
        public async Task The_same_lineage_code_under_two_tenants_is_two_separate_histories()
        {
            CustomerAssessmentService service = NewService();
            string lineage = NewLineage();
            Guid tenantA = Guid.NewGuid();
            Guid tenantB = Guid.NewGuid();

            AssessmentOutcome<CustomerAssessmentVersionResult> a = await service.AssessAsync(
                tenantA, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);
            AssessmentOutcome<CustomerAssessmentVersionResult> b = await service.AssessAsync(
                tenantB, WithLineage(ForeignIntakeFixture.V1(), lineage), CancellationToken.None);

            Track(a.Value!);
            Track(b.Value!);

            Assert.NotEqual(a.Value!.AssessmentId, b.Value!.AssessmentId);
            Assert.Equal(1, a.Value!.VersionNumber);
            Assert.Equal(1, b.Value!.VersionNumber);

            // Identical fingerprints in two different lineages are legitimate.
            Assert.Equal(a.Value!.SemanticFingerprint, b.Value!.SemanticFingerprint);
        }

        private async Task<int> CountLineagesAsync(string lineageCode)
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM ppiq_meta.customer_assessments WHERE lineage_code = @lineage;";
            command.Parameters.AddWithValue("lineage", lineageCode);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        // ------------------------------------------------------------------
        // Behavioural authority barrier
        // ------------------------------------------------------------------

        [Fact]
        public async Task Running_an_assessment_changes_assessment_rows_and_nothing_else()
        {
            Dictionary<string, long> before = await SnapshotAsync();

            CustomerAssessmentService service = NewService();
            AssessmentOutcome<CustomerAssessmentVersionResult> outcome = await service.AssessAsync(
                Guid.NewGuid(), WithLineage(ForeignIntakeFixture.V1(), NewLineage()), CancellationToken.None);

            Assert.True(outcome.Succeeded);
            Track(outcome.Value!);

            Dictionary<string, long> after = await SnapshotAsync();

            Assert.Equal(before.Count, after.Count);

            var drifted = new List<string>();
            foreach (KeyValuePair<string, long> entry in before)
            {
                if (after[entry.Key] != entry.Value)
                {
                    drifted.Add(entry.Key + ": " + entry.Value + " -> " + after[entry.Key]);
                }
            }

            Assert.True(
                drifted.Count == 0,
                "An assessment mutated tables it does not own: " + string.Join("; ", drifted));
        }

        /// <summary>
        /// Row counts for every table in the governed schemas except the two
        /// this task owns. Naming the forbidden tables individually would only
        /// prove the ones remembered; this proves all of them.
        /// </summary>
        private async Task<Dictionary<string, long>> SnapshotAsync()
        {
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);

            await using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var qualified = new List<string>();

            await using (NpgsqlCommand list = connection.CreateCommand())
            {
                list.CommandText =
                    "SELECT table_schema, table_name FROM information_schema.tables " +
                    "WHERE table_type = 'BASE TABLE' " +
                    "  AND table_schema IN ('ppiq_meta', 'ppiq_plant', 'ppiq_staging', 'public') " +
                    "  AND table_name NOT IN ('customer_assessments', 'customer_assessment_versions') " +
                    "ORDER BY table_schema, table_name;";

                await using NpgsqlDataReader reader = await list.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    qualified.Add("\"" + reader.GetString(0) + "\".\"" + reader.GetString(1) + "\"");
                }
            }

            foreach (string table in qualified)
            {
                await using NpgsqlCommand count = connection.CreateCommand();
                count.CommandText = "SELECT count(*) FROM " + table + ";";
                counts[table] = Convert.ToInt64(await count.ExecuteScalarAsync());
            }

            return counts;
        }
    }
}
