namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ-T020: coverage is shown with every finding.
/// It must reconcile: Population = Included + Excluded.
/// </summary>
public sealed record FindingCoverageEvidence(
    int Population,
    int Included,
    int Excluded,
    IReadOnlyDictionary<string, int> ExcludedReasons)
{
    public double Completeness =>
        Population <= 0 ? 0.0 : Math.Round(Included / (double)Population, 6);

    public bool IsArithmeticallyConsistent =>
        Population == Included + Excluded &&
        Excluded == ExcludedReasons.Values.Sum() &&
        Completeness >= 0.0 &&
        Completeness <= 1.0;

    public string Summary =>
        $"based on {Included:N0} of {Population:N0} records ({Completeness:P1}); {Excluded:N0} excluded";
}

/// <summary>
/// PPIQ-T018: value range belongs to the finding and must keep provenance + abstain state.
/// </summary>
public sealed record FindingValueEvidence(
    string FindingRef,
    string Currency,
    decimal Low,
    decimal Mid,
    decimal High,
    bool IsAbstained,
    string? AbstainReason,
    string ProvenanceHandle,
    int AssumptionVersion,
    string HonestyCaveat);

/// <summary>
/// PPIQ-T021: blended provenance for material-transition attribution.
/// Generic manufacturing model: parent material batches/contributors, not steel-hardcoded.
/// </summary>
public sealed record BlendedProvenanceContributor(
    string ParentMaterialId,
    decimal Weight,
    string ContributionBasis,
    string ProvenanceConfidence);

public sealed record BlendedProvenanceEvidence(
    string MaterialId,
    bool IsTransition,
    IReadOnlyList<BlendedProvenanceContributor> Contributors,
    string HonestyCaveat)
{
    public decimal WeightTotal => Contributors.Sum(x => x.Weight);

    public bool IsValid =>
        Contributors.Count > 0 &&
        Math.Abs(WeightTotal - 1.0m) <= 0.0001m &&
        Contributors.All(x => x.Weight >= 0m && x.Weight <= 1m);
}