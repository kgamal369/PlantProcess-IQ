namespace PlantProcess.Application.Analytics.Calibration;

/// <summary>
/// T-037: computes the Brier score and an equal-width reliability curve for a risk model against
/// historical outcomes, and decides whether to ABSTAIN ("insufficient calibration") rather than
/// surface a confident-but-untrustworthy number. Pure and deterministic.
/// </summary>
public sealed class RiskCalibrationService
{
    public CalibrationResult Compute(
        IReadOnlyList<CalibrationSample> samples,
        int buckets = 10,
        CalibrationThresholds? thresholds = null)
    {
        thresholds ??= CalibrationThresholds.Default;
        if (buckets < 1) buckets = 10;

        var n = samples.Count;
        if (n == 0)
            return new CalibrationResult(0, 0, 0, Array.Empty<ReliabilityBucket>(), 0, false, true,
                "Insufficient calibration: no outcome samples are available.");

        double sumSquaredError = 0;
        var outcomeCount = 0;
        var width = 1.0 / buckets;
        var counts  = new int[buckets];
        var sumPred = new double[buckets];
        var sumObs  = new int[buckets];

        foreach (var sample in samples)
        {
            var p = Clamp01(sample.Predicted);
            var o = sample.Outcome ? 1.0 : 0.0;
            sumSquaredError += (p - o) * (p - o);
            if (sample.Outcome) outcomeCount++;

            var index = Math.Min(buckets - 1, (int)(p / width));
            counts[index]++;
            sumPred[index] += p;
            sumObs[index]  += sample.Outcome ? 1 : 0;
        }

        var brier = sumSquaredError / n;

        var curve = new List<ReliabilityBucket>(buckets);
        double maxGap = 0;
        for (var i = 0; i < buckets; i++)
        {
            var lower = i * width;
            var upper = i == buckets - 1 ? 1.0 : (i + 1) * width;

            if (counts[i] == 0)
            {
                curve.Add(new ReliabilityBucket(i, lower, upper, 0, double.NaN, double.NaN));
                continue;
            }

            var meanPredicted = sumPred[i] / counts[i];
            var observed = (double)sumObs[i] / counts[i];
            curve.Add(new ReliabilityBucket(i, lower, upper, counts[i], meanPredicted, observed));
            maxGap = Math.Max(maxGap, Math.Abs(meanPredicted - observed));
        }

        var tooFewOutcomes = outcomeCount < thresholds.MinOutcomes;
        var poorlyCalibrated = brier > thresholds.MaxBrier || maxGap > thresholds.MaxGap;
        var abstain = tooFewOutcomes || poorlyCalibrated;

        string? reason =
            tooFewOutcomes  ? $"Insufficient calibration: only {outcomeCount} positive outcomes (need >= {thresholds.MinOutcomes})." :
            poorlyCalibrated ? $"Insufficient calibration: Brier {brier:0.###} / max reliability gap {maxGap:0.###} exceeds thresholds." :
            null;

        return new CalibrationResult(brier, n, outcomeCount, curve, maxGap, IsReliable: !abstain, Abstain: abstain, AbstainReason: reason);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}