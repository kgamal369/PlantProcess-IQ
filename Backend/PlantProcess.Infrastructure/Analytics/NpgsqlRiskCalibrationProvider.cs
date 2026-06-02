using Npgsql;
using PlantProcess.Application.Analytics.Calibration;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// Pulls (predicted, outcome) pairs for a risk type from live (non-synthetic) data and runs them
/// through <see cref="RiskCalibrationService"/>. If the outcome source is unavailable it ABSTAINS
/// rather than emitting a confident number.
///
/// NOTE: 'outcome' is derived here as "a quality event occurred for the scored material at/after the
/// score time". If your truth source differs, adjust the SQL below; the abstain-on-error path keeps
/// the build safe in the meantime. 'predicted' assumes Score is on a 0..100 scale.
/// </summary>
public sealed class NpgsqlRiskCalibrationProvider : IRiskCalibrationProvider
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly RiskCalibrationService _calibration;

    public NpgsqlRiskCalibrationProvider(NpgsqlDataSource dataSource, RiskCalibrationService calibration)
    {
        _dataSource = dataSource;
        _calibration = calibration;
    }

    public async Task<CalibrationResult> ComputeForRiskTypeAsync(string riskType, int buckets, CancellationToken cancellationToken)
    {
        var samples = new List<CalibrationSample>();

        try
        {
            await using var cmd = _dataSource.CreateCommand(
                "SELECT (rs.score / 100.0)::float8 AS predicted, " +
                "       EXISTS (SELECT 1 FROM public.quality_events qe " +
                "               WHERE qe.material_unit_id = rs.material_unit_id " +
                "                 AND qe.event_time_utc >= rs.scored_at_utc) AS outcome " +
                "FROM public.risk_scores rs " +
                "JOIN public.material_units mu ON mu.id = rs.material_unit_id " +
                "WHERE rs.risk_type = @rt AND COALESCE(mu.is_synthetic, false) = false");
            cmd.Parameters.AddWithValue("rt", riskType);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var predicted = await reader.IsDBNullAsync(0, cancellationToken) ? 0.0 : reader.GetDouble(0);
                var outcome   = !await reader.IsDBNullAsync(1, cancellationToken) && reader.GetBoolean(1);
                samples.Add(new CalibrationSample(predicted, outcome));
            }
        }
        catch
        {
            return new CalibrationResult(0, 0, 0, Array.Empty<ReliabilityBucket>(), 0, false, true,
                "Insufficient calibration: outcome history is not available for this model.");
        }

        return _calibration.Compute(samples, buckets, CalibrationThresholds.Default);
    }
}