using PlantProcess.Analytics.Core.Methods;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// Kernel-side pairing decision. This does NOT modify the existing MethodSelector.
/// It records, in one place, that Numeric x Categorical is now a supported pairing,
/// and that a genuinely unsupported pairing refuses with a method-side reason that
/// never blames the customer's data.
/// </summary>
public enum KernelPairing
{
    NumericNumeric,
    BinaryNumeric,
    CategoricalCategorical,
    NumericCategorical,
    Unsupported
}

public sealed record KernelPairingChoice(
    KernelPairing Pairing,
    bool IsSupported,
    KernelExclusionReason ExclusionReason,
    ExclusionAttribution Attribution,
    string Rationale);

public static class KernelMethodSelector
{
    public static KernelPairingChoice Classify(VariableType a, VariableType b)
    {
        bool aNum = a == VariableType.Numeric;
        bool bNum = b == VariableType.Numeric;
        bool aCat = a == VariableType.Categorical || a == VariableType.Binary;
        bool bCat = b == VariableType.Categorical || b == VariableType.Binary;

        if (aNum && bNum)
            return Supported(KernelPairing.NumericNumeric,
                "Numeric/numeric: rank correlation path, unchanged from the existing selector.");

        if ((a == VariableType.Binary && bNum) || (aNum && b == VariableType.Binary))
            return Supported(KernelPairing.BinaryNumeric,
                "Binary/numeric: point-biserial path, unchanged from the existing selector.");

        if ((aNum && b == VariableType.Categorical) || (a == VariableType.Categorical && bNum))
            return Supported(KernelPairing.NumericCategorical,
                "Numeric/categorical: assumption-aware one-way ANOVA with Kruskal-Wallis fallback.");

        if (aCat && bCat)
            return Supported(KernelPairing.CategoricalCategorical,
                "Categorical/categorical: Cramer's V path, unchanged from the existing selector.");

        return new KernelPairingChoice(
            KernelPairing.Unsupported, false,
            KernelExclusionReason.UnsupportedMethodPairing,
            ExclusionAttribution.Method,
            $"No enabled statistical method exists for the variable-type pair ({a},{b}). "
            + "This is a limitation of the available method set, not a property of the data.");
    }

    private static KernelPairingChoice Supported(KernelPairing pairing, string rationale) =>
        new(pairing, true, KernelExclusionReason.None, ExclusionAttribution.None, rationale);
}
