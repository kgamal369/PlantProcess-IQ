using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Analytics;

/// <summary>
/// Stores reusable analytics/correlation outputs.
/// This is not root-cause proof; it is an auditable suspected-contributor result.
/// </summary>
public class CorrelationResult : BaseEntity
{
    public string CorrelationType { get; private set; } = null!;
    public string SubjectCode { get; private set; } = null!;
    public string OutcomeCode { get; private set; } = null!;
    public decimal? Score { get; private set; }
    public string ResultJson { get; private set; } = null!;
    public DateTime CalculatedAtUtc { get; private set; }

    private CorrelationResult()
    {
    }

    public CorrelationResult(
        string correlationType,
        string subjectCode,
        string outcomeCode,
        decimal? score,
        string resultJson,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(correlationType))
            throw new ArgumentException("Correlation type is required.", nameof(correlationType));

        if (string.IsNullOrWhiteSpace(subjectCode))
            throw new ArgumentException("Subject code is required.", nameof(subjectCode));

        if (string.IsNullOrWhiteSpace(outcomeCode))
            throw new ArgumentException("Outcome code is required.", nameof(outcomeCode));

        if (string.IsNullOrWhiteSpace(resultJson))
            throw new ArgumentException("Result JSON is required.", nameof(resultJson));

        CorrelationType = correlationType.Trim();
        SubjectCode = subjectCode.Trim();
        OutcomeCode = outcomeCode.Trim();
        Score = score;
        ResultJson = resultJson.Trim();
        CalculatedAtUtc = DateTime.UtcNow;
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}
