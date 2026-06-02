namespace PlantProcess.Application.Analytics.Calibration;

public interface IRiskCalibrationProvider
{
    Task<CalibrationResult> ComputeForRiskTypeAsync(string riskType, int buckets, CancellationToken cancellationToken);
}