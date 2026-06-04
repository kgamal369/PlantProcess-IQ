namespace PlantProcess.Application.Acquisition;

/// <summary>
/// PPIQ-T029:
/// Maps historian-style tag history into canonical parameter observation drafts.
/// Read-only by doctrine: mapping only, no write/control path to source.
/// </summary>
public sealed class HistorianTagHistoryMapper
{
    public IReadOnlyList<CanonicalParameterObservationDraft> Map(
        IReadOnlyList<HistorianTagObservation> observations,
        IReadOnlyList<HistorianTagMapping> mappings)
    {
        var byTag = mappings
            .Where(x => x.IsActive)
            .ToDictionary(x => x.TagName, StringComparer.OrdinalIgnoreCase);

        var drafts = new List<CanonicalParameterObservationDraft>();

        foreach (var observation in observations)
        {
            if (!byTag.TryGetValue(observation.TagName, out var mapping))
                continue;

            drafts.Add(new CanonicalParameterObservationDraft(
                ParameterCode: mapping.ParameterCode,
                ObservedAtUtc: observation.TimestampUtc,
                NumericValue: observation.NumericValue,
                TextValue: observation.TextValue,
                Unit: mapping.UnitOverride ?? observation.Unit,
                QualityFlag: NormalizeQuality(observation.QualityFlag),
                SourceRecordId: observation.SourceRecordId));
        }

        return drafts
            .OrderBy(x => x.ObservedAtUtc)
            .ThenBy(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeQuality(string? qualityFlag)
    {
        if (string.IsNullOrWhiteSpace(qualityFlag))
            return "Unknown";

        return qualityFlag.Trim().Equals("good", StringComparison.OrdinalIgnoreCase)
            ? "Good"
            : qualityFlag.Trim();
    }
}