using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// T-045 Pack B. Reads the governed outcome definition so a readiness widget
/// can be evaluated at the grain the outcome is actually registered at.
///
/// Matching is case-insensitive and highest-version-wins, which is exactly what
/// NpgsqlFeatureVectorLoader already does when it reads outcome_type for the
/// same key. Two readers of one definition must not disagree about which
/// version is current.
/// </summary>
public sealed class NpgsqlAnalysisOutcomeTargetResolver : IAnalysisOutcomeTargetResolver
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlAnalysisOutcomeTargetResolver(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<AnalysisOutcomeTarget?> ResolveAsync(string outcomeKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(outcomeKey))
            return null;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        await using var cmd = new NpgsqlCommand(
            @"SELECT outcome_key, grain, display_name, outcome_type
              FROM public.ml_outcome_definitions
              WHERE lower(outcome_key) = lower(@k) AND is_deleted = false
              ORDER BY version DESC
              LIMIT 1", conn);

        cmd.Parameters.AddWithValue("k", outcomeKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new AnalysisOutcomeTarget(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? reader.GetString(0) : reader.GetString(2),
            reader.IsDBNull(3) ? string.Empty : reader.GetString(3));
    }
}