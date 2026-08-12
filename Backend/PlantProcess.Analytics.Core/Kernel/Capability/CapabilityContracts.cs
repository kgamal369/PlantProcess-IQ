namespace PlantProcess.Analytics.Core.Kernel.Capability;

using PlantProcess.Analytics.Core.Kernel;

/// <summary>The intelligence capabilities a customer installation may support.</summary>
public enum CapabilityCode
{
    Statistics,
    Similarity,
    Novelty,
    SupervisedPrediction,
    PracticeLearning,
    Remediation
}

/// <summary>
/// How far a capability is supported. Three states, not a boolean, because a missing
/// input rarely removes a capability outright. It usually narrows it.
/// </summary>
public enum CapabilityAvailability
{
    Available,

    /// <summary>Supported, but with a named part of it unavailable. The reason says which part.</summary>
    Degraded,

    Unavailable
}

/// <summary>
/// Why a capability is unavailable or degraded. CAPABILITY-PROFILER codes only.
/// Statistical-method reasons live in StatisticalExclusionReason and never appear here.
/// </summary>
public enum CapabilityShortfallCode
{
    None,
    InsufficientPopulation,
    InsufficientHistory,
    NoOutcomeDeclared,
    NoLabelledOutcomes,
    InsufficientLabelledPopulation,
    ClassImbalanceBelowFloor,
    InsufficientDistinctValues,
    DetectionAnchorsUndeclared,
    GenealogyAbsent,
    GenealogyCoverageBelowFloor,
    NoControllableParameters,
    InsufficientPracticeSignatures,
    NoInterventionHistory,
    NoEligibleContextDimension
}

/// <summary>
/// How strongly the installation links what enters a stage to what leaves it.
/// Derived from the declared relationship model, never guessed by the profiler.
/// </summary>
public enum GenealogyStrength
{
    None,
    Sequential,
    Transformational
}

/// <summary>
/// State of one context dimension. A single-level dimension is COLLAPSED: it is removed
/// from the eligible set and is not an error. A plant with one shift is a normal customer.
/// </summary>
public enum DimensionStatus
{
    Eligible,
    Collapsed,
    Absent
}

public enum OutcomeValueType
{
    Binary,
    Categorical,
    Ordinal,
    Continuous
}

// ---------------------------------------------------------------- INPUT

/// <summary>
/// A typed outcome contract, SAFE-NOW fixture-declared. This is the shape the canonical
/// SM-06 OutcomeDefinition will later supply. It binds to no database, no
/// ml_outcome_definitions table and no presentation semantics.
/// </summary>
public sealed record FixtureOutcomeDefinition(
    string OutcomeCode,
    OutcomeValueType ValueType,
    string GrainCode,
    bool DetectionAnchorsDeclared,
    int LabelledCount,
    double MinorityClassFraction,
    int DistinctValueCount);

public sealed record ContextDimensionObservation(
    string DimensionCode,
    int ObservedLevelCount,
    bool IsVariantDimension);

public sealed record GenealogyObservation(
    GenealogyStrength Strength,
    double LinkCoverage,
    int ProcessPositionCount);

public sealed record PracticeObservation(
    int ControllableParameterCount,
    int ObservedParameterCount,
    int DistinctPracticeSignatureCount);

public sealed record InterventionObservation(int RecordedInterventionCount);

/// <summary>
/// Everything the profiler measures. Industry-neutral: no plant vocabulary, no table
/// name, no product term. Every field is a count, a fraction or a declared contract.
/// </summary>
public sealed record CapabilityProfilerInput(
    int AnalyticalUnitCount,
    double HistorySpanDays,
    IReadOnlyList<FixtureOutcomeDefinition> Outcomes,
    IReadOnlyList<ContextDimensionObservation> ContextDimensions,
    GenealogyObservation Genealogy,
    PracticeObservation Practice,
    InterventionObservation Interventions);

// ---------------------------------------------------------------- OUTPUT

public sealed record DimensionVerdict(
    string DimensionCode,
    int ObservedLevelCount,
    DimensionStatus Status,
    bool IsVariantDimension,
    string Reason);

/// <summary>
/// One capability decision, always accompanied by the measured facts behind it.
/// A capability is never reported unavailable without the number that made it so.
/// </summary>
public sealed record CapabilityVerdict(
    CapabilityCode Capability,
    CapabilityAvailability Availability,
    TerminalState TerminalState,
    CapabilityShortfallCode Shortfall,
    ExclusionAttribution Attribution,
    string Reason,
    IReadOnlyList<MeasuredFact> Facts,
    string? Subject);

public sealed record CapabilityProfile(
    IReadOnlyList<CapabilityVerdict> Capabilities,
    IReadOnlyList<DimensionVerdict> Dimensions,
    GenealogyStrength GenealogyStrength,
    IReadOnlyList<MeasuredFact> PopulationFacts)
{
    public CapabilityVerdict For(CapabilityCode capability) =>
        Capabilities.First(c => c.Capability == capability);

    /// <summary>Dimensions that can actually condition an analysis. Collapsed ones are excluded.</summary>
    public IReadOnlyList<DimensionVerdict> EligibleDimensions =>
        Dimensions.Where(d => d.Status == DimensionStatus.Eligible).ToList();

    public IReadOnlyList<DimensionVerdict> CollapsedDimensions =>
        Dimensions.Where(d => d.Status == DimensionStatus.Collapsed).ToList();
}
