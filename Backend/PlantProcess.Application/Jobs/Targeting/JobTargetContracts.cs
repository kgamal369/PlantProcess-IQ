using PlantProcess.Application.Definitions;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>
/// T-064. WHAT A JOB SAYS IT EXECUTES.
///
/// The reference carries the kind as a DefinitionKind rather than a table name,
/// which is the whole reason this contract survives the canonical convergence:
/// when T-089/T-090 establish definition_store and T-106 adds the physical
/// foreign key, the kind, the identity and the policy are unchanged and no
/// caller moves.
///
/// A reference is either coherent or it does not exist. Pinned without a version
/// and CurrentPublished with one are both refused at construction, so no code
/// downstream has to ask which field to believe.
/// </summary>
public sealed record JobTargetReference
{
    public required DefinitionKind Kind { get; init; }
    public required Guid DefinitionId { get; init; }
    public required JobTargetVersionPolicy VersionPolicy { get; init; }

    /// <summary>Set when and only when the policy is Pinned.</summary>
    public int? PinnedVersion { get; init; }

    /// <summary>
    /// Returns null when the reference is coherent, or a sentence naming the
    /// violation. Structural coherence only; whether the target exists is a
    /// question for the resolver, not for this record.
    /// </summary>
    public string? Validate()
    {
        if (DefinitionId == Guid.Empty)
        {
            return "A target reference must carry a definition identity.";
        }

        if (VersionPolicy == JobTargetVersionPolicy.Pinned)
        {
            if (!PinnedVersion.HasValue)
            {
                return "The pinned policy requires a version number.";
            }

            if (PinnedVersion.Value <= 0)
            {
                return "A pinned version number must be greater than zero.";
            }
        }
        else if (PinnedVersion.HasValue)
        {
            return "The current-published policy cannot carry a pinned version number.";
        }

        return null;
    }

    public static JobTargetReference CurrentPublished(DefinitionKind kind, Guid definitionId)
    {
        return new JobTargetReference
        {
            Kind = kind,
            DefinitionId = definitionId,
            VersionPolicy = JobTargetVersionPolicy.CurrentPublished
        };
    }

    public static JobTargetReference Pinned(DefinitionKind kind, Guid definitionId, int version)
    {
        return new JobTargetReference
        {
            Kind = kind,
            DefinitionId = definitionId,
            VersionPolicy = JobTargetVersionPolicy.Pinned,
            PinnedVersion = version
        };
    }
}

/// <summary>
/// T-064. WHAT THE JOB ACTUALLY RAN.
///
/// The resolved version number is the point of the record. Reproducibility is
/// the claim - the same stored job against the same target resolves to the same
/// version - and a later publication of another version cannot change what a
/// pinned job resolved to. This is the value the run history records.
/// </summary>
public sealed record ResolvedJobTarget
{
    public required DefinitionKind Kind { get; init; }
    public required Guid DefinitionId { get; init; }
    public required int ResolvedVersion { get; init; }
    public required JobTargetVersionPolicy PolicyApplied { get; init; }
}
