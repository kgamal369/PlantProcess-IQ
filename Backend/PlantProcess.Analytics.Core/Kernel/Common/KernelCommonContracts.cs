namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// Terminal state of any Layer B engine evaluation. Shared across every engine family
/// so that one refusal language exists, not several. Refusal is a valid product result.
/// These are the six states named by the frozen Layer B contract.
/// </summary>
public enum TerminalState
{
    Finding,
    InsufficientData,
    NotApplicable,
    RefusedByGuard,
    ContradictedByControl,
    ModelNotReady
}

/// <summary>
/// Where a refusal originates. Shared across every engine family.
/// <para>
/// This distinction is the whole point of the type. A method gap must never be reported
/// as a property of the customer's data, and an undeclared contract must never be
/// reported as missing data. Collapsing these three is the defect class this product
/// has already produced once.
/// </para>
/// </summary>
public enum ExclusionAttribution
{
    None,

    /// <summary>A measured property of the customer's data. Reported with the number.</summary>
    Data,

    /// <summary>A limitation of the available method set. Never attributed to the data.</summary>
    Method,

    /// <summary>
    /// A contract the customer's engineer has not declared. The data may be perfectly
    /// adequate; nothing has said what it means.
    /// </summary>
    Declaration
}

/// <summary>
/// One measured fact behind a decision. Every engine returns these alongside a verdict,
/// so no capability is ever reported as unavailable without the number that made it so.
/// </summary>
public sealed record MeasuredFact(
    string Code,
    double Observed,
    double Required,
    string Unit,
    bool Satisfied)
{
    public static MeasuredFact AtLeast(string code, double observed, double required, string unit) =>
        new(code, observed, required, unit, observed >= required);

    public static MeasuredFact Informational(string code, double observed, string unit) =>
        new(code, observed, double.NaN, unit, true);
}
