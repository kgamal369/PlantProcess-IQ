namespace PlantProcess.Application.Analytics.Calibration;

public sealed record CalibrationSample(double Predicted, bool Outcome);

public sealed record ReliabilityBucket(
    int Index,
    double LowerBound,
    double UpperBound,
    int Count,
    double MeanPredicted,
    double ObservedFrequency);

public sealed record CalibrationResult(
    double BrierScore,
    int SampleSize,
    int OutcomeCount,
    IReadOnlyList<ReliabilityBucket> ReliabilityCurve,
    double MaxCalibrationGap,
    bool IsReliable,
    bool Abstain,
    string? AbstainReason);

/// <summary>Thresholds beyond which the model is treated as too poorly calibrated to surface a number.</summary>
public sealed record CalibrationThresholds(int MinOutcomes = 50, double MaxBrier = 0.25, double MaxGap = 0.20)
{
    public static CalibrationThresholds Default { get; } = new();
}