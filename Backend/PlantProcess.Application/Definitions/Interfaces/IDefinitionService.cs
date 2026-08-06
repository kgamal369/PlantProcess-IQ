using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions.Contracts;

namespace PlantProcess.Application.Definitions.Interfaces;

/// <summary>
/// PPIQ T-039. THE FINAL EXTERNAL CONTRACT FOR VERSIONED DEFINITIONS.
///
/// Chapter 3 section 4.5.11 specifies one definition store with its own version
/// and dependency tables. That table set does not exist and M1 DOES NOT BUILD
/// IT. What M1 builds is this contract, in front of whatever persistence each
/// kind already has, so that M2a can replace the storage without a single
/// caller moving.
///
/// The consequence is deliberate and worth stating plainly: the adapters behind
/// these six methods DO NOT SHARE A STORE in M1. A transformation's versions
/// live where transformation versions already live; a widget's live in the
/// minimal snapshot store T-039 adds beside the operational widget row.
/// Uniformity belongs to this interface, not to the tables under it, and
/// copying one kind's history into another kind's storage to make the inside
/// look tidy would be inventing a migration nobody asked for.
///
/// Two rules the implementations owe, because a contract that does not state
/// them gets an implementation that ignores them:
///
///   the current definition and its version snapshot are written in ONE
///   transaction, so a successful update cannot exist without its version; and
///
///   the version number is allocated by the server inside that transaction,
///   never read, incremented and written back from outside it.
/// </summary>
public interface IDefinitionService
{
    /// <summary>
    /// Creates a definition and its first version. The returned snapshot
    /// carries the identity the caller did not have yet.
    /// </summary>
    Task<ApplicationResult<DefinitionSnapshot>> CreateAsync(
        DefinitionKind kind,
        string payloadJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the current definition and appends the next version. The
    /// previous version stays readable, which is what makes it a version and
    /// not a backup.
    /// </summary>
    Task<ApplicationResult<DefinitionSnapshot>> UpdateAsync(
        DefinitionKind kind,
        Guid definitionId,
        string payloadJson,
        CancellationToken cancellationToken);

    /// <summary>The definition as it stands now.</summary>
    Task<ApplicationResult<DefinitionSnapshot>> GetCurrentAsync(
        DefinitionKind kind,
        Guid definitionId,
        CancellationToken cancellationToken);

    /// <summary>One immutable version, exactly as it was written.</summary>
    Task<ApplicationResult<DefinitionSnapshot>> GetVersionAsync(
        DefinitionKind kind,
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// The versions that actually exist. An implementation that has no version
    /// store yet returns a refusal rather than inventing a version one, because
    /// a fabricated history is worse than an absent one.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>> ListVersionsAsync(
        DefinitionKind kind,
        Guid definitionId,
        CancellationToken cancellationToken);

    /// <summary>Marks one existing version as the published one.</summary>
    Task<ApplicationResult<DefinitionSnapshot>> PublishAsync(
        DefinitionKind kind,
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken);
}