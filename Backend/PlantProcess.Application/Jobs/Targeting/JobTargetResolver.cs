using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Contracts;
using PlantProcess.Application.Definitions.Interfaces;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>Total outcome of asking a job what it executes.</summary>
public enum JobTargetOutcome
{
    /// <summary>The job declares no target, and its class does not require one.</summary>
    NoTargetDeclared = 1,

    /// <summary>The job declares a target and it resolved to one exact version.</summary>
    Resolved = 2
}

/// <summary>
/// The answer, with no third state. Resolved always carries a target and
/// NoTargetDeclared never does, so a caller cannot read absence as success by
/// accident.
/// </summary>
public sealed record JobTargetResolution
{
    public required JobTargetOutcome Outcome { get; init; }

    public ResolvedJobTarget? Target { get; init; }

    public static JobTargetResolution None()
    {
        return new JobTargetResolution { Outcome = JobTargetOutcome.NoTargetDeclared };
    }

    public static JobTargetResolution Of(ResolvedJobTarget target)
    {
        return new JobTargetResolution { Outcome = JobTargetOutcome.Resolved, Target = target };
    }
}

public interface IJobTargetResolver
{
    /// <summary>
    /// Resolves what a job executes into one exact version, or refuses with a
    /// governed JB code and a sentence.
    /// </summary>
    Task<ApplicationResult<JobTargetResolution>> ResolveAsync(
        JobDefinitionType jobClass,
        JobTargetReference? target,
        CancellationToken cancellationToken);

    /// <summary>
    /// JB04. Refuses when a definition is the declared target of one or more
    /// jobs, naming them.
    /// </summary>
    Task<ApplicationResult> AssertNotTargetedByJobsAsync(
        DefinitionKind kind,
        Guid definitionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// T-064. THE M1 RESOLVER, THROUGH THE FINAL SEAM AND NOTHING ELSE.
///
/// Every question about versions is asked of IDefinitionService, which T-039
/// declares the final external contract. This class names no table, so when
/// T-089 and T-090 land the canonical definition-store authority and T-106 adds
/// the physical foreign key, the storage moves underneath and this file does not
/// change.
///
/// One reading of 4.5.5a is stated here rather than buried, because it decides
/// behaviour: a pinned version that still exists and is still published KEEPS
/// resolving after a later version is published. Refusing it as "superseded"
/// would make pinning meaningless and would contradict the reproducibility rule
/// that a later publication must not silently retarget a pinned job. JB03 is
/// therefore raised when the pinned version is absent from history, or is
/// present and not published.
/// </summary>
public sealed class JobTargetResolver : IJobTargetResolver
{
    private readonly IDefinitionService _definitions;
    private readonly IJobTargetClassPolicy _classPolicy;
    private readonly IJobTargetLookup _lookup;

    public JobTargetResolver(
        IDefinitionService definitions,
        IJobTargetClassPolicy classPolicy,
        IJobTargetLookup lookup)
    {
        _definitions = definitions;
        _classPolicy = classPolicy;
        _lookup = lookup;
    }

    public async Task<ApplicationResult<JobTargetResolution>> ResolveAsync(
        JobDefinitionType jobClass,
        JobTargetReference? target,
        CancellationToken cancellationToken)
    {
        JobTargetClassRule rule = _classPolicy.RuleFor(jobClass);

        if (target is null)
        {
            if (rule.RequiresTarget)
            {
                return ApplicationResult<JobTargetResolution>.Failure(
                    JobTargetErrors.NoTargetOnTargetRequiringClass(jobClass.ToString()));
            }

            return ApplicationResult<JobTargetResolution>.Success(JobTargetResolution.None());
        }

        string? structural = target.Validate();
        if (structural is not null)
        {
            return ApplicationResult<JobTargetResolution>.Failure(
                ApplicationError.Validation(structural));
        }

        if (rule.PermittedKinds is not null && !rule.PermittedKinds.Contains(target.Kind))
        {
            return ApplicationResult<JobTargetResolution>.Failure(
                JobTargetErrors.TargetSurfaceDoesNotMatchJobClass(
                    jobClass.ToString(), target.Kind.ToString()));
        }

        ApplicationResult<IReadOnlyList<DefinitionVersionSummary>> history =
            await _definitions.ListVersionsAsync(target.Kind, target.DefinitionId, cancellationToken);

        if (history.IsFailure)
        {
            // The version authority refused - most often because this kind has no
            // adapter in this build. That refusal is passed through unchanged
            // rather than reworded into a JB code it is not.
            return ApplicationResult<JobTargetResolution>.Failure(history.Error!);
        }

        IReadOnlyList<DefinitionVersionSummary> versions = history.Value!;

        if (versions.Count == 0)
        {
            // 4.5.5a as supplied names no JB code for a target identity that
            // resolves to no definition at all, so this uses the repository's
            // existing NotFound convention and says exactly what happened.
            return ApplicationResult<JobTargetResolution>.Failure(
                ApplicationError.NotFound(
                    "The job targets " + target.Kind + " definition " + target.DefinitionId
                    + ", which has no versions. The target identity resolves to no definition."));
        }

        if (target.VersionPolicy == JobTargetVersionPolicy.Pinned)
        {
            int pinned = target.PinnedVersion!.Value;

            DefinitionVersionSummary? match = null;
            foreach (DefinitionVersionSummary candidate in versions)
            {
                if (candidate.VersionNumber == pinned)
                {
                    match = candidate;
                    break;
                }
            }

            if (match is null)
            {
                return ApplicationResult<JobTargetResolution>.Failure(
                    JobTargetErrors.PinnedVersionNotPublishedOrSuperseded(
                        target.Kind.ToString(), target.DefinitionId, pinned,
                        "that version is not in the definition's history."));
            }

            if (!match.IsPublished)
            {
                return ApplicationResult<JobTargetResolution>.Failure(
                    JobTargetErrors.PinnedVersionNotPublishedOrSuperseded(
                        target.Kind.ToString(), target.DefinitionId, pinned,
                        "that version exists but has not been published."));
            }

            return ApplicationResult<JobTargetResolution>.Success(
                JobTargetResolution.Of(new ResolvedJobTarget
                {
                    Kind = target.Kind,
                    DefinitionId = target.DefinitionId,
                    ResolvedVersion = pinned,
                    PolicyApplied = JobTargetVersionPolicy.Pinned,
                    ParametersJson = target.ParametersJson
                }));
        }

        List<DefinitionVersionSummary> published = new();
        foreach (DefinitionVersionSummary candidate in versions)
        {
            if (candidate.IsPublished)
            {
                published.Add(candidate);
            }
        }

        if (published.Count == 0)
        {
            return ApplicationResult<JobTargetResolution>.Failure(
                JobTargetErrors.PinnedVersionNotPublishedOrSuperseded(
                    target.Kind.ToString(), target.DefinitionId, 0,
                    "the job follows the published version and this definition has none."));
        }

        if (published.Count > 1)
        {
            // Two published versions is a contradiction in the store, not a
            // choice for this resolver to make on the product's behalf.
            return ApplicationResult<JobTargetResolution>.Failure(
                ApplicationError.Conflict(
                    target.Kind + " definition " + target.DefinitionId + " has "
                    + published.Count + " published versions. Exactly one version can be "
                    + "published, and the job cannot be told which one it runs."));
        }

        return ApplicationResult<JobTargetResolution>.Success(
            JobTargetResolution.Of(new ResolvedJobTarget
            {
                Kind = target.Kind,
                DefinitionId = target.DefinitionId,
                ResolvedVersion = published[0].VersionNumber,
                PolicyApplied = JobTargetVersionPolicy.CurrentPublished,
                ParametersJson = target.ParametersJson
            }));
    }

    public async Task<ApplicationResult> AssertNotTargetedByJobsAsync(
        DefinitionKind kind,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        string kindName = kind.ToString();

        IReadOnlyList<string> jobCodes =
            await _lookup.JobCodesTargetingAsync(kindName, definitionId, cancellationToken);

        if (jobCodes.Count > 0)
        {
            return ApplicationResult.Failure(
                JobTargetErrors.DefinitionTargetedByJobs(kindName, definitionId, jobCodes));
        }

        return ApplicationResult.Success();
    }
}
