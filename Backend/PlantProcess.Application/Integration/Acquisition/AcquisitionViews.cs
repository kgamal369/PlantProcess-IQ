// Read shapes of the industrial acquisition authority. Values only; no behaviour.
namespace PlantProcess.Application.Integration.Acquisition;

public sealed record DatasetGovernanceView(
    Guid GovernanceId,
    Guid TenantId,
    Guid SourceDatasetDefinitionId,
    Guid ConnectionProfileId,
    string ProviderType,
    DateTime GovernedAtUtc);

public sealed record FieldRevisionView(
    Guid FieldId,
    int Revision,
    bool IsCurrent,
    string ProviderType,
    string LocatorKind,
    string SourceLocatorJson,
    string FieldKey,
    string DisplayName,
    string DeclaredType,
    string TypeShapeJson,
    string? SourceUnit,
    IReadOnlyList<string> Roles,
    int? LayoutRevision,
    string ChangeKind,
    string SemanticHash,
    DateTime CreatedAtUtc);

public sealed record FieldDeclarationResult(
    Guid FieldId,
    string FieldKey,
    int Revision,
    string ChangeKind,
    bool Created);

public sealed record LayoutRevisionView(
    int Revision,
    string LayoutKind,
    int RegionBytes,
    string DocumentJson,
    string SemanticHash,
    DateTime CreatedAtUtc);

public sealed record LayoutDeclarationResult(int Revision, bool Created, string SemanticHash);

public sealed record AcquisitionConfigurationVersionView(
    Guid DefinitionId,
    string DefinitionCode,
    int Version,
    string Status,
    string DefinitionHash,
    string ContentJson,
    DateTime CreatedAtUtc);

public sealed record AcquisitionValidationReport(
    bool IsValid,
    Guid DefinitionId,
    int Version,
    string Status,
    string DefinitionHash,
    IReadOnlyList<AcquisitionOperationTruth> Operations,
    IReadOnlyList<AcquisitionRefusal> Refusals);

/// <summary>
/// The activation boundary answer. Admitted is true only when a commissioned
/// continuous-acquisition runtime accepted the exact version; until one exists the
/// answer is an honest typed refusal that still names the exact target it resolved.
/// </summary>
public sealed record AcquisitionActivationAdmission(
    bool Admitted,
    string Code,
    string Detail,
    Guid DefinitionId,
    int Version,
    string DefinitionHash,
    string TargetDefinitionKind,
    IReadOnlyList<AcquisitionOperationTruth> Operations);
