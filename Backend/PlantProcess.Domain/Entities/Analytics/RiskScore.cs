using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Analytics;

public class RiskScore : BaseEntity
{
    public Guid MaterialUnitId { get; private set; }

    public string RiskType { get; private set; } = null!;

    public decimal Score { get; private set; }

    public string? RiskClass { get; private set; }

    public string? MainContributorsJson { get; private set; }

    /// <summary>
    /// Full persisted explanation snapshot.
    /// This preserves why the score was calculated at that time,
    /// even if rules/model versions change later.
    /// </summary>
    public string? ExplanationJson { get; private set; }

    public DateTime ScoredAtUtc { get; private set; }

    public DateTime ScoredAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    public string? ModelVersion { get; private set; }

    private RiskScore()
    {
    }

    public RiskScore(
        Guid materialUnitId,
        string riskType,
        decimal score,
        bool isSynthetic,
        string? riskClass = null,
        string? mainContributorsJson = null,
        string? explanationJson = null,
        string? modelVersion = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "Europe/Berlin",
        int plantUtcOffsetMinutes = 60)
    {
        if (materialUnitId == Guid.Empty)
            throw new ArgumentException("Material unit ID is required.", nameof(materialUnitId));

        if (string.IsNullOrWhiteSpace(riskType))
            throw new ArgumentException("Risk type is required.", nameof(riskType));

        if (score < 0 || score > 1)
            throw new ArgumentOutOfRangeException(nameof(score), "Risk score must be between 0 and 1.");

        MaterialUnitId = materialUnitId;
        RiskType = riskType.Trim();
        Score = score;
        RiskClass = Clean(riskClass);
        MainContributorsJson = NormalizeJson(mainContributorsJson);
        ExplanationJson = NormalizeJson(explanationJson);
        ModelVersion = Clean(modelVersion);

        ScoredAtUtc = DateTime.UtcNow;
        ScoredAtLocal = DateTime.SpecifyKind(
            ScoredAtUtc.AddMinutes(plantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        PlantTimeZoneId = string.IsNullOrWhiteSpace(plantTimeZoneId)
            ? "Europe/Berlin"
            : plantTimeZoneId.Trim();

        PlantUtcOffsetMinutes = plantUtcOffsetMinutes;

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void UpdateExplanation(
        string? mainContributorsJson,
        string? explanationJson)
    {
        MainContributorsJson = NormalizeJson(mainContributorsJson);
        ExplanationJson = NormalizeJson(explanationJson);
        MarkAsUpdated();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}