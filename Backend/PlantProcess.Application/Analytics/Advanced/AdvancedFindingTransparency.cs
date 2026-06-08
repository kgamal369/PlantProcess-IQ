
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE.
/// User-facing transparency projection for every advanced correlation finding.
/// This does not change the statistical result; it makes the population,
/// dropped/excluded records, stratification status, provenance, and honesty caveat explicit.
/// </summary>
public sealed record AdvancedFindingTransparency(
    string FeatureKey,
    string PopulationLabel,
    int PopulationSize,
    int PairedSampleSize,
    int ExcludedRecordCount,
    string ExclusionSummary,
    bool StratificationEvaluated,
    bool SurvivesStratification,
    string StratificationReason,
    string ProvenanceHandle,
    string HonestyCaveat)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FeatureKey) &&
        PopulationSize > 0 &&
        PairedSampleSize > 0 &&
        PairedSampleSize <= PopulationSize &&
        ExcludedRecordCount >= 0 &&
        !string.IsNullOrWhiteSpace(ExclusionSummary) &&
        !string.IsNullOrWhiteSpace(StratificationReason) &&
        !string.IsNullOrWhiteSpace(ProvenanceHandle) &&
        !string.IsNullOrWhiteSpace(HonestyCaveat);
}

public static class AdvancedFindingTransparencyProjector
{
    public static IReadOnlyList<AdvancedFindingTransparency> Project(AdvancedAnalysisRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Findings
            .Select(finding => ProjectOne(run, finding))
            .ToArray();
    }

    public static AdvancedFindingTransparency ProjectOne(AdvancedAnalysisRunResult run, AdvancedFinding finding)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(finding);

        var populationSize = Math.Max(finding.SampleSize + finding.ExcludedRecords, finding.SampleSize);
        var stratificationEvaluated = !finding.StratificationReason.StartsWith(
            "Stratification not evaluated",
            StringComparison.OrdinalIgnoreCase);

        var exclusionSummary = finding.ExcludedRecords == 0
            ? "No records were dropped during feature/outcome alignment for this finding."
            : $"{finding.ExcludedRecords} record(s) were dropped during feature/outcome alignment for this finding.";

        return new AdvancedFindingTransparency(
            finding.FeatureKey,
            $"{run.Grain}; window={run.WindowDays}d; outcome={run.OutcomeKey}",
            populationSize,
            finding.SampleSize,
            finding.ExcludedRecords,
            exclusionSummary,
            stratificationEvaluated,
            finding.SurvivesStratification,
            finding.StratificationReason,
            finding.ProvenanceHandle,
            finding.HonestyCaveat);
    }
}
