namespace PlantProcess.Application.Integration.Mapping.Faults;

/// <summary>PPIQ-802: the four foreseeable mapping faults - each maps to a typed error, never a generic 500.</summary>
public enum MappingFaultKind
{
    None,
    NoSuchView,
    NoSuchColumn,
    InvalidAggregateForType,
    AmbiguousJoinKey
}

public sealed record MappingFault(MappingFaultKind Kind, string AffectedView, string NextSafeStep);

public static class MappingFaultClassifier
{
    public static MappingFault? Classify(MappingFaultKind kind, string affectedView) => kind switch
    {
        MappingFaultKind.None => null,
        MappingFaultKind.NoSuchView => new(kind, affectedView,
            "Recreate or republish the view; verify it exists in the mapping catalog."),
        MappingFaultKind.NoSuchColumn => new(kind, affectedView,
            "Remove or remap the missing column in the mapping authoring panel."),
        MappingFaultKind.InvalidAggregateForType => new(kind, affectedView,
            "Choose an aggregate valid for the column type (e.g. count instead of sum on text)."),
        MappingFaultKind.AmbiguousJoinKey => new(kind, affectedView,
            "Qualify the join key with its source table to disambiguate the join."),
        _ => null
    };
}