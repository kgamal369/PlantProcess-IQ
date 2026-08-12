namespace PlantProcess.Analytics.Core.Kernel;

// TerminalState, ExclusionAttribution and MeasuredFact are cross-engine and live in
// Kernel/Common/KernelCommonContracts.cs. They are shared, not duplicated here.

/// <summary>
/// Why a statistical evaluation produced no finding. These are STATISTICAL-METHOD
/// reasons only. Capability-profiler shortfalls live in their own code set, so that
/// neither enum grows into a God-enum spanning unrelated engines.
/// <para>
/// The three below are deliberately distinct and are never collapsed. A method gap
/// must never be reported as a property of customer data.
/// </para>
/// </summary>
public enum StatisticalExclusionReason
{
    None,
    ConstantZeroVariance,
    UnsupportedMethodPairing,
    InsufficientGroups,
    InsufficientSample
}

/// <summary>Statistical method chosen by the kernel for a variable pair.</summary>
public enum KernelMethod
{
    None,
    Anova,
    KruskalWallis
}

/// <summary>One named group of numeric observations. The group key is opaque to the kernel.</summary>
public sealed record NumericGroup(string Key, IReadOnlyList<double> Values);

/// <summary>
/// Typed input to the Numeric x Categorical kernel. Carries no schema, no table name and
/// no industry vocabulary. Grouping is already performed by the caller.
/// </summary>
public sealed record GroupComparisonInput(IReadOnlyList<NumericGroup> Groups);

/// <summary>Diagnostics recorded for the assumption decision. Evidence, not the rule.</summary>
public sealed record AssumptionEvidence(
    double LeveneStatistic,
    double LevenePValue,
    IReadOnlyList<double> GroupStandardDeviations,
    double VarianceRatio,
    IReadOnlyList<double> GroupSkewness,
    bool ParametricAssumptionsSupported,
    string Rationale);

/// <summary>
/// Result of a kernel evaluation. Exposes the real analytical evidence: method, aligned
/// population, group sizes, effect size, p-value and an explicit terminal reason.
/// </summary>
public sealed record GroupComparisonResult(
    TerminalState TerminalState,
    KernelMethod Method,
    StatisticalExclusionReason ExclusionReason,
    ExclusionAttribution Attribution,
    string Reason,
    int AlignedPopulation,
    IReadOnlyList<int> GroupSizes,
    IReadOnlyList<string> GroupKeys,
    double Statistic,
    int DegreesOfFreedom1,
    int DegreesOfFreedom2,
    double PValue,
    string EffectSizeMeasure,
    double EffectSize,
    bool TieCorrectionApplied,
    AssumptionEvidence? Assumptions)
{
    public static GroupComparisonResult Refuse(
        TerminalState state,
        StatisticalExclusionReason reason,
        ExclusionAttribution attribution,
        string message,
        int alignedPopulation,
        IReadOnlyList<int> groupSizes,
        IReadOnlyList<string> groupKeys) =>
        new(state, KernelMethod.None, reason, attribution, message,
            alignedPopulation, groupSizes, groupKeys,
            double.NaN, 0, 0, double.NaN, string.Empty, double.NaN, false, null);
}
