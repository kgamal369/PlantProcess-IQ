namespace PlantProcess.Domain.Common;

/// <summary>
/// Typed reasons a staged row cannot lawfully become canonical.
/// Chapter 3 DF5 / 4.5.14 defines the complete PV01..PV15 set.
/// </summary>
public enum ProjectionValidationCode
{
    PV01 = 1,
    PV02 = 2,
    PV03 = 3,
    PV04 = 4,
    PV05 = 5,
    PV06 = 6,
    PV07 = 7,
    PV08 = 8,
    PV09 = 9,
    PV10 = 10,
    PV11 = 11,
    PV12 = 12,
    PV13 = 13,
    PV14 = 14,
    PV15 = 15
}

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
        ProjectionValidationCode.PV09 => "Correct the value or the governed range/specification before reprocessing.",
        ProjectionValidationCode.PV10 => "Correct the relationship members so the declared cardinality is satisfied.",
        ProjectionValidationCode.PV11 => "Map the edge at the grains declared by the governed relationship.",
        ProjectionValidationCode.PV12 => "Remove or redirect the edge that would create a genealogy cycle.",
        ProjectionValidationCode.PV13 => "Correct the child attribution weights so the governed total equals 1.0.",
        ProjectionValidationCode.PV14 => "Publish one preferred relationship path, then reprocess the row.",
        ProjectionValidationCode.PV15 => "Load the referenced object from the other batch, then reprocess this quarantined row.",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "No correction is declared for this validation code.")
    };
}