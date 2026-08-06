namespace PlantProcess.Application.Definitions.Contracts;

/// <summary>
/// PPIQ T-039. One immutable version of one definition.
///
/// The payload is carried as JSON on purpose. This contract has to outlive the
/// storage behind it - M1 keeps the operational widget row where it is and adds
/// only a snapshot beside it, and M2a replaces that entirely - so the contract
/// may not name a column, a table or a concrete definition type.
/// </summary>
public sealed record DefinitionSnapshot(
    DefinitionKind Kind,
    Guid DefinitionId,
    int VersionNumber,
    string PayloadJson,
    DateTime CreatedAtUtc,
    string? CreatedBy);

/// <summary>
/// One row of the version list. Deliberately not the payload: listing versions
/// is a navigation question, and answering it with every payload would make the
/// cheap call the expensive one.
/// </summary>
public sealed record DefinitionVersionSummary(
    int VersionNumber,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    bool IsPublished);