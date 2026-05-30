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

    public string EngineKey => "postgres-default-v1";

    public async Task<CorrelationComputeResult> ComputeAsync(
        CorrelationComputeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OutcomeKey))
            throw new ArgumentException("Outcome key is required.", nameof(request));

        var grain = string.IsNullOrWhiteSpace(request.Grain) ? "coil" : request.Grain.Trim();
        var windowDays = Math.Clamp(request.WindowDays <= 0 ? 90 : request.WindowDays, 1, 3650);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        await using (var refresh = connection.CreateCommand())
        {
            refresh.CommandText = "SELECT * FROM public.ppiq_ml_refresh_feature_store(@window_days);";
            refresh.Parameters.AddWithValue("window_days", windowDays);
            await refresh.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT compute_run_id, result_count FROM public.ppiq_ml_compute_basic_correlations(@outcome_key, @grain, @window_days);";
        command.Parameters.AddWithValue("outcome_key", request.OutcomeKey.Trim());
        command.Parameters.AddWithValue("grain", grain);
        command.Parameters.AddWithValue("window_days", windowDays);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new CorrelationComputeResult(
                Guid.Empty,
                0,
                EngineKey,
                "NoResult",
                "The compute function returned no rows.");
        }

        return new CorrelationComputeResult(
            reader.GetGuid(0),
            reader.GetInt32(1),
            EngineKey,
            "Success",
            "Correlation compute completed against the ML feature store.");
    }
}