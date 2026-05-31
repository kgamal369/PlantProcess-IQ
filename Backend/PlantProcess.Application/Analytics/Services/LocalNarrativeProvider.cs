
using System.Globalization;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Application.Analytics.Services;

/// <summary>
/// Fully local/offline narrative provider. It never sends data outside the process.
/// It uses only abstracted finding fields and is safe for air-gapped plants.
/// </summary>
public sealed class LocalNarrativeProvider : INarrativeProvider
{
    public string ProviderKey => "local-null-narrative-v1";

    public Task<NarrativeResult> GenerateAsync(
        NarrativeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var findings = request.Findings ?? Array.Empty<NarrativeFindingInput>();

        var top = findings
            .OrderByDescending(x => x.EffectSize ?? 0)
            .Take(3)
            .ToArray();

        var lines = new List<string>
        {
            "Local diagnostic narrative generated offline.",
            "This is evidence for engineering review, not guaranteed root cause."
        };

        foreach (var finding in top)
        {
            var effect = finding.EffectSize.HasValue
                ? finding.EffectSize.Value.ToString("0.000", CultureInfo.InvariantCulture)
                : "n/a";

            var qValue = finding.QValue.HasValue
                ? finding.QValue.Value.ToString("0.000000", CultureInfo.InvariantCulture)
                : "n/a";

            lines.Add(
                $"- {finding.FeatureKey} vs {finding.OutcomeKey}: method={finding.Method}, effect={effect}, q={qValue}, effectiveN={finding.EffectiveN?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}.");
        }

        return Task.FromResult(new NarrativeResult(
            string.Join(Environment.NewLine, lines),
            ProviderKey,
            "deterministic-template",
            UsedExternalApi: false,
            RawPlantDataIncluded: false,
            SafetyNotes: new[]
            {
                "offline-provider",
                "abstracted-findings-only",
                "no-material-ids",
                "no-raw-source-rows",
                "no-equipment-log-payload"
            }));
    }
}
