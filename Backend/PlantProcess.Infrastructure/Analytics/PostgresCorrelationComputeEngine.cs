using Npgsql;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Infrastructure.Analytics;

public sealed class PostgresCorrelationComputeEngine : ICorrelationComputeEngine
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCorrelationComputeEngine(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public string EngineKey => "postgres-v6-type-aware";

    public async Task<CorrelationComputeResult> ComputeAsync(
        CorrelationComputeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OutcomeKey))
            throw new ArgumentException("Outcome key is required.", nameof(request));

        var grain = string.IsNullOrWhiteSpace(request.Grain) ? "coil" : request.Grain.Trim();
        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 3650 : request.WindowDays, 1, 3650);
        var functionName = await FunctionExistsAsync("ppiq_ml_compute_correlations_v6", cancellationToken)
            ? "ppiq_ml_compute_correlations_v6"
            : "ppiq_ml_compute_basic_correlations";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM public.{functionName}(@outcomeKey, @grain, @windowDays);";
        command.Parameters.AddWithValue("outcomeKey", request.OutcomeKey.Trim());
        command.Parameters.AddWithValue("grain", grain);
        command.Parameters.AddWithValue("windowDays", windowDays);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new CorrelationComputeResult(Guid.Empty, 0, EngineKey, "Empty", "Correlation compute returned no rows.");

        var runId = GetGuid(reader, "compute_run_id");
        var resultCount = GetInt(reader, "result_count");
        var methodCount = HasColumn(reader, "method_count") ? GetInt(reader, "method_count") : 1;
        var status = resultCount > 0 ? "Ok" : "NoData";
        var message = functionName == "ppiq_ml_compute_correlations_v6"
            ? $"Type-aware PostgreSQL compute finished. ResultCount={resultCount}, MethodCount={methodCount}."
            : $"Basic PostgreSQL compute finished. ResultCount={resultCount}. Apply 201_phase02_ml_feature_store_v6_completion.sql for type-aware methods.";

        return new CorrelationComputeResult(runId, resultCount, EngineKey, status, message);
    }

    private async Task<bool> FunctionExistsAsync(string functionName, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = 'public'
                  AND p.proname = @functionName
            );
            """;
        command.Parameters.AddWithValue("functionName", functionName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool exists && exists;
    }

    private static bool HasColumn(NpgsqlDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    private static Guid GetGuid(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);
    }

    private static int GetInt(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }
}
