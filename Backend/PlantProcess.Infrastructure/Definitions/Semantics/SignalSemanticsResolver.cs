using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions.Semantics;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions.Semantics;

/// <summary>
/// PPIQ T-210. The resolver over the parameter and KPI-binding authorities.
///
/// THIN ON PURPOSE. Resolution order and compatibility live in one SQL
/// function, ppiq_meta.resolve_aggregation_semantics, so that every consumer -
/// C#, a report, an operator at psql - gets the same answer from the same
/// place. This class maps that answer onto the public contract and refuses in
/// the contract's terms; it does not re-decide anything.
/// </summary>
public sealed class SignalSemanticsResolver : ISignalSemanticsResolver
{
    private readonly PlantProcessDbContext _dbContext;

    public SignalSemanticsResolver(PlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<ResolvedAggregation>> ResolveAsync(
        Guid tenantId,
        Guid parameterId,
        Guid? kpiBindingId,
        AggregationKind? requested,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        await using var command = Command(
            "SELECT resolved_kind, resolution_source, refusal_code, refusal_message, signal_kind, sampling_basis, " +
            "weight_basis, semantics_version FROM ppiq_meta.resolve_aggregation_semantics(@tenant, @parameter, @binding, @requested);",
            connection);

        command.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter("parameter", NpgsqlDbType.Uuid) { Value = parameterId });
        command.Parameters.Add(new NpgsqlParameter("binding", NpgsqlDbType.Uuid)
        {
            Value = kpiBindingId.HasValue ? kpiBindingId.Value : (object)DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("requested", NpgsqlDbType.Varchar)
        {
            Value = requested.HasValue ? requested.Value.ToString() : (object)DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ApplicationResult<ResolvedAggregation>.Failure(ApplicationError.Validation(
                AggregationRefusal.SemanticsUndeclared + ": the resolver returned no row."));
        }

        var refusal = reader.IsDBNull(2) ? null : reader.GetString(2);
        if (refusal is not null)
        {
            // The message carries everything ruling 17 asks a failure to name:
            // parameter, signal kind, sampling basis, requested method, source.
            var detail = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var message = refusal + ": " + detail +
                " [parameter=" + parameterId + " signal=" + (reader.IsDBNull(4) ? "none" : reader.GetString(4)) +
                " sampling=" + (reader.IsDBNull(5) ? "none" : reader.GetString(5)) +
                " requested=" + (requested.HasValue ? requested.Value.ToString() : "none") +
                " source=" + reader.GetString(1) + "]";

            return ApplicationResult<ResolvedAggregation>.Failure(ApplicationError.Validation(message));
        }

        return ApplicationResult<ResolvedAggregation>.Success(new ResolvedAggregation(
            Kind: Enum.Parse<AggregationKind>(reader.GetString(0)),
            Source: reader.GetString(1) switch
            {
                "kpi_binding" => AggregationResolutionSource.KpiBinding,
                "requested" => AggregationResolutionSource.Requested,
                _ => AggregationResolutionSource.Parameter,
            },
            SignalKind: Enum.Parse<SignalKind>(reader.GetString(4)),
            SamplingBasis: reader.IsDBNull(5) ? null : Enum.Parse<SamplingBasis>(reader.GetString(5)),
            WeightBasis: reader.IsDBNull(6) ? null : Enum.Parse<WeightBasis>(reader.GetString(6)),
            SemanticsVersion: reader.GetInt32(7)));
    }

    public async Task<ApplicationResult<SignalSemantics>> DeclareAsync(
        Guid tenantId,
        Guid parameterId,
        SignalSemanticsDeclaration declaration,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        // CLAIM-ON-DECLARATION (CENTRAL ruling 5). An unowned legacy row is
        // claimed atomically by the declaring tenant - the UPDATE succeeds only
        // where tenant_id IS NULL - and a row owned by anyone else is left
        // exactly as it is. Ownership never transfers.
        await using (var claim = Command(
            "UPDATE ppiq_meta.parameter_definitions SET tenant_id = @tenant WHERE id = @parameter AND tenant_id IS NULL;",
            connection))
        {
            claim.Parameters.Add(new NpgsqlParameter("parameter", NpgsqlDbType.Uuid) { Value = parameterId });
            claim.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId });
            await claim.ExecuteNonQueryAsync(cancellationToken);
        }

        // The validation trigger refuses an indefensible default with AG02 and
        // the versioning trigger keeps identical redeclaration idempotent. The
        // UPDATE is the whole write path; there is no second place semantics
        // can be set. Strictly the caller's own row: another tenant's row
        // matches nothing and reports not-found, exposing nothing.
        await using var command = Command(
            """
            UPDATE ppiq_meta.parameter_definitions SET
                signal_kind = @signal_kind,
                sampling_basis = @sampling_basis,
                aggregation_kind = @aggregation_kind,
                interpolation_kind = @interpolation_kind,
                weight_basis = @weight_basis,
                maximum_gap_seconds = @maximum_gap_seconds,
                counter_reset_policy = @counter_reset_policy,
                quality_policy = @quality_policy,
                time_basis = @time_basis
             WHERE id = @parameter AND tenant_id = @tenant;
            """, connection);

        command.Parameters.Add(new NpgsqlParameter("parameter", NpgsqlDbType.Uuid) { Value = parameterId });
        command.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId });
        Text(command, "signal_kind", declaration.SignalKind.ToString());
        Text(command, "sampling_basis", declaration.SamplingBasis?.ToString());
        Text(command, "aggregation_kind", declaration.DefaultAggregation?.ToString());
        Text(command, "interpolation_kind", declaration.Interpolation?.ToString());
        Text(command, "weight_basis", declaration.WeightBasis?.ToString());
        command.Parameters.Add(new NpgsqlParameter("maximum_gap_seconds", NpgsqlDbType.Integer)
        {
            Value = declaration.MaximumGapSeconds.HasValue ? declaration.MaximumGapSeconds.Value : (object)DBNull.Value
        });
        Text(command, "counter_reset_policy", declaration.CounterResetPolicy?.ToString());
        Text(command, "quality_policy", declaration.QualityPolicy?.ToString());
        Text(command, "time_basis", declaration.TimeBasis?.ToString());

        try
        {
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                return ApplicationResult<SignalSemantics>.Failure(ApplicationError.NotFound(
                    "No parameter with that identity exists in this tenant."));
            }
        }
        catch (PostgresException exception) when (exception.MessageText.StartsWith("AG02", StringComparison.Ordinal))
        {
            return ApplicationResult<SignalSemantics>.Failure(ApplicationError.Validation(
                exception.MessageText + " [parameter=" + parameterId + " signal=" + declaration.SignalKind +
                " sampling=" + (declaration.SamplingBasis?.ToString() ?? "none") +
                " requested=" + (declaration.DefaultAggregation?.ToString() ?? "none") + " source=declaration]"));
        }

        return await GetAsync(tenantId, parameterId, cancellationToken);
    }

    public async Task<ApplicationResult<SignalSemantics>> GetAsync(
        Guid tenantId,
        Guid parameterId,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        await using var command = Command(
            """
            SELECT signal_kind, sampling_basis, aggregation_kind, interpolation_kind, weight_basis,
                   maximum_gap_seconds, counter_reset_policy, quality_policy, time_basis,
                   semantics_version, semantics_declared_at_utc
              FROM ppiq_meta.parameter_definitions
             WHERE id = @parameter AND tenant_id = @tenant;
            """, connection);

        command.Parameters.Add(new NpgsqlParameter("parameter", NpgsqlDbType.Uuid) { Value = parameterId });
        command.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ApplicationResult<SignalSemantics>.Failure(ApplicationError.NotFound(
                "No parameter with that identity exists in this tenant."));
        }

        if (reader.IsDBNull(0))
        {
            return ApplicationResult<SignalSemantics>.Failure(ApplicationError.Validation(
                AggregationRefusal.SemanticsUndeclared + ": parameter " + parameterId + " has no signal semantics declared."));
        }

        return ApplicationResult<SignalSemantics>.Success(new SignalSemantics(
            parameterId, tenantId,
            Enum.Parse<SignalKind>(reader.GetString(0)),
            reader.IsDBNull(1) ? null : Enum.Parse<SamplingBasis>(reader.GetString(1)),
            reader.IsDBNull(2) ? null : Enum.Parse<AggregationKind>(reader.GetString(2)),
            reader.IsDBNull(3) ? null : Enum.Parse<InterpolationKind>(reader.GetString(3)),
            reader.IsDBNull(4) ? null : Enum.Parse<WeightBasis>(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : Enum.Parse<CounterResetPolicy>(reader.GetString(6)),
            reader.IsDBNull(7) ? null : Enum.Parse<QualityPolicy>(reader.GetString(7)),
            reader.IsDBNull(8) ? null : Enum.Parse<TimeBasis>(reader.GetString(8)),
            reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10)));
    }

    private static void Text(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Varchar)
        {
            Value = value is null ? DBNull.Value : value
        });

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private NpgsqlCommand Command(string sql, NpgsqlConnection connection)
    {
        var command = new NpgsqlCommand(sql, connection);
        var transaction = _dbContext.Database.CurrentTransaction;
        if (transaction is not null)
        {
            command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        }

        return command;
    }
}
