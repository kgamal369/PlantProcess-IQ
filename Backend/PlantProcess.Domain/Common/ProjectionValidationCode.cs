namespace PlantProcess.Domain.Common;

/// <summary>
/// PPIQ T-099. THE TYPED REASONS A STAGED ROW CANNOT BECOME CANONICAL.
///
/// Chapter 3 DF5 declares fifteen validation classes. T-099 implements the first
/// eight; T-100 adds PV09 to PV15. There are deliberately no placeholders here:
/// a member that exists without an implementation and a fixture would let the
/// taxonomy look complete while a class silently classified nothing, which is
/// the one failure mode the completeness ratchet exists to prevent.
///
/// THE CODE IS THE FACT. A queue groups, counts and routes on the code; the
/// sentence beside it only explains the same fact to a person. Nothing here is
/// derived from a message, and no code is inferred from an exception type.
/// </summary>
public enum ProjectionValidationCode
{
    /// <summary>An expected mapped source column is absent from the staged payload.</summary>
    PV01 = 1,

    /// <summary>A supplied value cannot become the required target type.</summary>
    PV02 = 2,

    /// <summary>The source field exists but a required canonical value is null or empty.</summary>
    PV03 = 3,

    /// <summary>The resulting canonical business key conflicts with existing canonical truth.</summary>
    PV04 = 4,

    /// <summary>Two staged rows in this execution declare the same governed business key.</summary>
    PV05 = 5,

    /// <summary>A supplied referenced canonical or taxonomy object cannot be resolved.</summary>
    PV06 = 6,

    /// <summary>The submitted taxonomy value is not registered in the governed catalogue.</summary>
    PV07 = 7,

    /// <summary>The supplied unit is not permitted for the governed parameter or definition.</summary>
    PV08 = 8
}

/// <summary>
/// The deterministic correction hint for a code. Modest and fixed on purpose:
/// the contract asks for a suggested correction derived from the code, not for
/// generated prose about a particular row.
/// </summary>
public static class ProjectionValidationCorrection
{
    public static string For(ProjectionValidationCode code) => code switch
    {
        ProjectionValidationCode.PV01 => "Map or provide the declared source field.",
        ProjectionValidationCode.PV02 => "Correct the value or add an explicit conversion.",
        ProjectionValidationCode.PV03 => "Provide the required value.",
        ProjectionValidationCode.PV04 => "Correct the business key or resolve the canonical conflict.",
        ProjectionValidationCode.PV05 => "Remove or reconcile the duplicate staged row.",
        ProjectionValidationCode.PV06 => "Import or fix the referenced object.",
        ProjectionValidationCode.PV07 => "Use a registered taxonomy value.",
        ProjectionValidationCode.PV08 => "Use the governed unit for the parameter.",
        _ => throw new ArgumentOutOfRangeException(
                 nameof(code), code, "No correction is declared for this validation code.")
    };
}
