// T-064. Falsification of the job target and version contract.
//
// Every test states an observable property of the contract Chapter 3 4.5.5a
// asks for. None of them touches a page, a dashboard or the presentation.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Contracts;
using PlantProcess.Application.Definitions.Interfaces;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

public sealed class JobTargetResolverTests
{
    private static readonly Guid TargetId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // --- the class policy ---------------------------------------------------

    [Fact]
    public void The_declared_class_policy_is_total_over_the_job_class_enum()
    {
        DeclaredJobTargetClassPolicy policy = new();

        foreach (JobDefinitionType jobClass in Enum.GetValues<JobDefinitionType>())
        {
            JobTargetClassRule rule = policy.RuleFor(jobClass);
            Assert.NotNull(rule);
            Assert.Contains(jobClass, DeclaredJobTargetClassPolicy.DeclaredClasses);
        }
    }

    [Fact]
    public void No_job_class_currently_declares_a_target_requirement()
    {
        // A measured statement about today, pinned so that declaring one later is
        // a deliberate change with a failing test beside it rather than a drift.
        DeclaredJobTargetClassPolicy policy = new();

        foreach (JobDefinitionType jobClass in Enum.GetValues<JobDefinitionType>())
        {
            JobTargetClassRule rule = policy.RuleFor(jobClass);
            Assert.False(rule.RequiresTarget);
            Assert.Null(rule.PermittedKinds);
        }
    }

    // --- A. a valid target resolves -----------------------------------------

    [Fact]
    public async Task A_pinned_target_resolves_to_that_exact_version()
    {
        JobTargetResolver resolver = ResolverWith(
            Version(1, published: false),
            Version(2, published: true));

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.Custom,
            JobTargetReference.Pinned(DefinitionKind.Analysis, TargetId, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(JobTargetOutcome.Resolved, result.Value!.Outcome);
        Assert.Equal(2, result.Value.Target!.ResolvedVersion);
        Assert.Equal(JobTargetVersionPolicy.Pinned, result.Value.Target.PolicyApplied);
        Assert.Equal(TargetId, result.Value.Target.DefinitionId);
    }

    [Fact]
    public async Task A_published_version_target_resolves_to_the_published_version()
    {
        JobTargetResolver resolver = ResolverWith(
            Version(1, published: false),
            Version(2, published: true),
            Version(3, published: false));

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.Custom,
            JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value!.Target!.ResolvedVersion);
        Assert.Equal(JobTargetVersionPolicy.CurrentPublished, result.Value.Target.PolicyApplied);
    }

    // --- B. determinism ------------------------------------------------------

    [Fact]
    public async Task Publishing_a_later_version_does_not_retarget_a_pinned_job()
    {
        JobTargetReference pinned = JobTargetReference.Pinned(DefinitionKind.Analysis, TargetId, 2);

        ApplicationResult<JobTargetResolution> before = await ResolverWith(
            Version(1, published: false),
            Version(2, published: true))
            .ResolveAsync(JobDefinitionType.Custom, pinned, CancellationToken.None);

        ApplicationResult<JobTargetResolution> after = await ResolverWith(
            Version(1, published: false),
            Version(2, published: true),
            Version(3, published: true))
            .ResolveAsync(JobDefinitionType.Custom, pinned, CancellationToken.None);

        Assert.True(before.IsSuccess);
        Assert.True(after.IsSuccess, after.Error?.Message);
        Assert.Equal(2, before.Value!.Target!.ResolvedVersion);
        Assert.Equal(2, after.Value!.Target!.ResolvedVersion);
    }

    [Fact]
    public async Task Publishing_a_later_version_does_retarget_a_published_version_job()
    {
        // The other half of the same claim: current_published means current, and a
        // test that only proved pinning would not have proved the distinction.
        JobTargetReference following =
            JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId);

        ApplicationResult<JobTargetResolution> before = await ResolverWith(
            Version(1, published: true))
            .ResolveAsync(JobDefinitionType.Custom, following, CancellationToken.None);

        ApplicationResult<JobTargetResolution> after = await ResolverWith(
            Version(1, published: false),
            Version(2, published: true))
            .ResolveAsync(JobDefinitionType.Custom, following, CancellationToken.None);

        Assert.Equal(1, before.Value!.Target!.ResolvedVersion);
        Assert.Equal(2, after.Value!.Target!.ResolvedVersion);
    }

    [Fact]
    public async Task The_same_reference_resolves_identically_when_nothing_changed()
    {
        JobTargetReference pinned = JobTargetReference.Pinned(DefinitionKind.Analysis, TargetId, 1);
        JobTargetResolver resolver = ResolverWith(Version(1, published: true));

        ApplicationResult<JobTargetResolution> first =
            await resolver.ResolveAsync(JobDefinitionType.Custom, pinned, CancellationToken.None);
        ApplicationResult<JobTargetResolution> second =
            await resolver.ResolveAsync(JobDefinitionType.Custom, pinned, CancellationToken.None);

        Assert.Equal(first.Value!.Target, second.Value!.Target);
    }

    // --- C. JB01, and the absence that is not a refusal ----------------------

    [Fact]
    public async Task An_unconstrained_class_with_no_target_is_not_a_refusal()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith()
            .ResolveAsync(JobDefinitionType.DbLinkImport, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(JobTargetOutcome.NoTargetDeclared, result.Value!.Outcome);
        Assert.Null(result.Value.Target);
    }

    [Fact]
    public async Task JB01_refuses_a_target_requiring_class_saved_with_no_target()
    {
        JobTargetResolver resolver = ResolverWith(
            policy: new StubClassPolicy(new JobTargetClassRule
            {
                RequiresTarget = true,
                PermittedKinds = null
            }));

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.MlParamsVsDefects, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobTargetErrorCodes.NoTargetOnTargetRequiringClass, result.Error!.Code);
        Assert.Contains("MlParamsVsDefects", result.Error.Message);
    }

    // --- JB02 ----------------------------------------------------------------

    [Fact]
    public async Task JB02_refuses_a_target_surface_the_job_class_cannot_run()
    {
        JobTargetResolver resolver = ResolverWith(
            new[] { Version(1, published: true) },
            new StubClassPolicy(new JobTargetClassRule
            {
                RequiresTarget = true,
                PermittedKinds = new[] { DefinitionKind.Analysis }
            }));

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.MlParamsVsDefects,
            JobTargetReference.CurrentPublished(DefinitionKind.Widget, TargetId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobTargetErrorCodes.TargetSurfaceDoesNotMatchJobClass, result.Error!.Code);
        Assert.Contains("MlParamsVsDefects", result.Error.Message);
        Assert.Contains("Widget", result.Error.Message);
    }

    [Fact]
    public async Task A_permitted_surface_passes_the_class_check()
    {
        JobTargetResolver resolver = ResolverWith(
            new[] { Version(1, published: true) },
            new StubClassPolicy(new JobTargetClassRule
            {
                RequiresTarget = true,
                PermittedKinds = new[] { DefinitionKind.Analysis }
            }));

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.MlParamsVsDefects,
            JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    // --- D and E. JB03 -------------------------------------------------------

    [Fact]
    public async Task JB03_refuses_a_pinned_version_that_is_not_in_history()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith(
            Version(1, published: true))
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.Pinned(DefinitionKind.Analysis, TargetId, 7),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobTargetErrorCodes.PinnedVersionNotPublishedOrSuperseded, result.Error!.Code);
        Assert.Contains("7", result.Error.Message);
    }

    [Fact]
    public async Task JB03_refuses_a_pinned_version_that_exists_but_is_unpublished()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith(
            Version(1, published: true),
            Version(2, published: false))
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.Pinned(DefinitionKind.Analysis, TargetId, 2),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobTargetErrorCodes.PinnedVersionNotPublishedOrSuperseded, result.Error!.Code);
        Assert.Contains("not been published", result.Error.Message);
    }

    [Fact]
    public async Task JB03_refuses_a_published_version_policy_when_nothing_is_published()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith(
            Version(1, published: false),
            Version(2, published: false))
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobTargetErrorCodes.PinnedVersionNotPublishedOrSuperseded, result.Error!.Code);
        Assert.Contains("has none", result.Error.Message);
    }

    [Fact]
    public async Task Two_published_versions_are_refused_rather_than_silently_chosen_between()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith(
            Version(1, published: true),
            Version(2, published: true))
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("published versions", result.Error!.Message);
    }

    // --- a target identity that resolves to nothing --------------------------

    [Fact]
    public async Task A_target_identity_with_no_definition_behind_it_is_refused()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith()
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("resolves to no definition", result.Error!.Message);
    }

    [Fact]
    public async Task A_refusal_from_the_version_authority_is_passed_through_unchanged()
    {
        // The Analysis kind has no version adapter in this build. That refusal is
        // the version authority's to make, and rewording it into a JB code would
        // claim a governed meaning the specification did not give it.
        JobTargetResolver resolver = new(
            new RefusingDefinitionService("no version adapter for this kind"),
            new DeclaredJobTargetClassPolicy(),
            new StubLookup());

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.Custom,
            JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("no version adapter", result.Error!.Message);
    }

    // --- structural coherence -------------------------------------------------

    [Fact]
    public void A_pinned_reference_without_a_version_is_incoherent()
    {
        JobTargetReference reference = new()
        {
            Kind = DefinitionKind.Analysis,
            DefinitionId = TargetId,
            VersionPolicy = JobTargetVersionPolicy.Pinned
        };

        Assert.NotNull(reference.Validate());
    }

    [Fact]
    public void A_published_version_reference_carrying_a_pinned_number_is_incoherent()
    {
        JobTargetReference reference = new()
        {
            Kind = DefinitionKind.Analysis,
            DefinitionId = TargetId,
            VersionPolicy = JobTargetVersionPolicy.CurrentPublished,
            PinnedVersion = 3
        };

        Assert.NotNull(reference.Validate());
    }

    [Fact]
    public async Task An_incoherent_reference_is_refused_before_the_version_store_is_asked()
    {
        CountingDefinitionService counting = new();
        JobTargetResolver resolver = new(
            counting, new DeclaredJobTargetClassPolicy(), new StubLookup());

        ApplicationResult<JobTargetResolution> result = await resolver.ResolveAsync(
            JobDefinitionType.Custom,
            new JobTargetReference
            {
                Kind = DefinitionKind.Analysis,
                DefinitionId = TargetId,
                VersionPolicy = JobTargetVersionPolicy.Pinned
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, counting.Calls);
    }

    // --- JB04 -----------------------------------------------------------------

    [Fact]
    public async Task JB04_refuses_deletion_of_a_definition_that_jobs_target_and_names_them()
    {
        JobTargetResolver resolver = new(
            new StubDefinitionService(Array.Empty<DefinitionVersionSummary>()),
            new DeclaredJobTargetClassPolicy(),
            new StubLookup("NIGHTLY_LEARNING", "WEEKLY_FULL"));

        ApplicationResult result = await resolver.AssertNotTargetedByJobsAsync(
            DefinitionKind.Analysis, TargetId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobTargetErrorCodes.DefinitionTargetedByJobs, result.Error!.Code);
        Assert.Contains("NIGHTLY_LEARNING", result.Error.Message);
        Assert.Contains("WEEKLY_FULL", result.Error.Message);
    }

    [Fact]
    public async Task An_untargeted_definition_may_be_deleted()
    {
        JobTargetResolver resolver = new(
            new StubDefinitionService(Array.Empty<DefinitionVersionSummary>()),
            new DeclaredJobTargetClassPolicy(),
            new StubLookup());

        ApplicationResult result = await resolver.AssertNotTargetedByJobsAsync(
            DefinitionKind.Analysis, TargetId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- G. existing jobs stay compatible -------------------------------------

    [Fact]
    public void A_job_created_the_existing_way_carries_no_target()
    {
        JobDefinition job = new(
            "NIGHTLY_LEARNING", "Nightly learning", JobDefinitionType.MlWeeklyFull,
            "Daily 02:00", isSynthetic: false);

        Assert.False(job.HasTargetDefinition);
        Assert.Null(job.TargetDefinitionId);
        Assert.Null(job.TargetDefinitionKind);
        Assert.Null(job.TargetVersionPolicy);
        Assert.Null(job.TargetDefinitionVersion);
    }

    [Fact]
    public void Assigning_and_clearing_a_target_moves_all_four_fields_together()
    {
        JobDefinition job = new(
            "NIGHTLY_LEARNING", "Nightly learning", JobDefinitionType.MlWeeklyFull,
            "Daily 02:00", isSynthetic: false);

        job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.Pinned, 4);

        Assert.True(job.HasTargetDefinition);
        Assert.Equal(nameof(DefinitionKind.Analysis), job.TargetDefinitionKind);
        Assert.Equal(JobTargetVersionPolicy.Pinned, job.TargetVersionPolicy);
        Assert.Equal(4, job.TargetDefinitionVersion);

        job.ClearTargetDefinition();

        Assert.False(job.HasTargetDefinition);
        Assert.Null(job.TargetDefinitionKind);
        Assert.Null(job.TargetVersionPolicy);
        Assert.Null(job.TargetDefinitionVersion);
    }

    [Fact]
    public void A_job_cannot_pin_without_a_version_or_follow_publication_with_one()
    {
        JobDefinition job = new(
            "NIGHTLY_LEARNING", "Nightly learning", JobDefinitionType.MlWeeklyFull,
            "Daily 02:00", isSynthetic: false);

        Assert.Throws<ArgumentException>(() => job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.Pinned));

        Assert.Throws<ArgumentException>(() => job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.CurrentPublished, 3));
    }

    // --- the run history records what ran -------------------------------------

    [Fact]
    public void The_run_history_records_the_version_that_actually_ran()
    {
        JobRunHistory run = new(
            Guid.NewGuid(), "NIGHTLY_LEARNING", "Nightly learning",
            JobDefinitionType.MlWeeklyFull, "Scheduler", null, null,
            isSynthetic: false, sourceSystem: null, sourceRecordId: null);

        Assert.Null(run.TargetDefinitionVersion);

        run.RecordResolvedTarget(
            nameof(DefinitionKind.Analysis), TargetId, 2, JobTargetVersionPolicy.CurrentPublished);

        Assert.Equal(2, run.TargetDefinitionVersion);
        Assert.Equal(TargetId, run.TargetDefinitionId);
        Assert.Equal(JobTargetVersionPolicy.CurrentPublished, run.TargetVersionPolicy);
    }

    [Fact]
    public void The_run_history_refuses_a_version_number_that_is_not_a_version()
    {
        JobRunHistory run = new(
            Guid.NewGuid(), "NIGHTLY_LEARNING", "Nightly learning",
            JobDefinitionType.MlWeeklyFull, "Scheduler", null, null,
            isSynthetic: false, sourceSystem: null, sourceRecordId: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => run.RecordResolvedTarget(
            nameof(DefinitionKind.Analysis), TargetId, 0, JobTargetVersionPolicy.Pinned));
    }

    // --- target parameters ----------------------------------------------------

    [Fact]
    public void Absent_parameters_stay_absent_and_are_never_an_empty_object()
    {
        Assert.Null(JobTargetParameters.Normalise(null));
        Assert.Null(JobTargetParameters.Normalise(string.Empty));
        Assert.Null(JobTargetParameters.Normalise("   "));

        // The distinction the contract turns on.
        Assert.Equal("{}", JobTargetParameters.Normalise("{}"));
        Assert.NotEqual(JobTargetParameters.Normalise("{}"), JobTargetParameters.Normalise(null));
    }

    [Fact]
    public void Malformed_parameters_are_refused_before_persistence()
    {
        Assert.False(JobTargetParameters.IsValid("{not json"));
        Assert.True(JobTargetParameters.IsValid(null));
        Assert.True(JobTargetParameters.IsValid("{\"window\":7}"));

        JobDefinition job = NewJob();

        Assert.Throws<ArgumentException>(() => job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.Pinned, 2, "{not json"));

        // The refusal leaves no half-written target behind.
        Assert.Null(job.TargetParametersJson);
        Assert.False(job.HasTargetDefinition);
    }

    [Fact]
    public void Valid_parameters_round_trip_exactly_as_supplied()
    {
        JobDefinition job = NewJob();
        const string payload = "{\"window_days\":7,\"threshold\":0.82}";

        job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.Pinned, 2, payload);

        Assert.Equal(payload, job.TargetParametersJson);

        job.ClearTargetDefinition();
        Assert.Null(job.TargetParametersJson);
    }

    [Fact]
    public async Task A_pinned_target_carries_its_parameters_into_the_resolution()
    {
        const string payload = "{\"window_days\":7}";

        ApplicationResult<JobTargetResolution> result = await ResolverWith(
            Version(1, published: false),
            Version(2, published: true))
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.Pinned(DefinitionKind.Analysis, TargetId, 2, payload),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(payload, result.Value!.Target!.ParametersJson);
    }

    [Fact]
    public async Task A_published_version_target_carries_its_parameters_into_the_resolution()
    {
        ApplicationResult<JobTargetResolution> result = await ResolverWith(
            Version(1, published: true))
            .ResolveAsync(
                JobDefinitionType.Custom,
                JobTargetReference.CurrentPublished(DefinitionKind.Analysis, TargetId, "{}"),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("{}", result.Value!.Target!.ParametersJson);
    }

    [Fact]
    public async Task A_reference_carrying_malformed_parameters_is_refused()
    {
        JobTargetReference reference = new()
        {
            Kind = DefinitionKind.Analysis,
            DefinitionId = TargetId,
            VersionPolicy = JobTargetVersionPolicy.CurrentPublished,
            ParametersJson = "{broken"
        };

        Assert.NotNull(reference.Validate());

        ApplicationResult<JobTargetResolution> result = await ResolverWith(Version(1, published: true))
            .ResolveAsync(JobDefinitionType.Custom, reference, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void The_run_history_records_the_parameters_actually_used()
    {
        JobRunHistory run = NewRun();
        const string payload = "{\"window_days\":7}";

        run.RecordResolvedTarget(
            nameof(DefinitionKind.Analysis), TargetId, 2,
            JobTargetVersionPolicy.CurrentPublished, payload);

        Assert.Equal(payload, run.TargetParametersJson);
        Assert.Equal(2, run.TargetDefinitionVersion);
    }

    [Fact]
    public void Editing_the_definition_afterwards_cannot_rewrite_a_recorded_run()
    {
        // The reproducibility claim, stated as an experiment rather than a comment.
        JobDefinition job = NewJob();
        job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.Pinned, 2,
            "{\"window_days\":7}");

        JobRunHistory run = NewRun();
        run.RecordResolvedTarget(
            job.TargetDefinitionKind!, job.TargetDefinitionId!.Value,
            job.TargetDefinitionVersion!.Value, job.TargetVersionPolicy!.Value,
            job.TargetParametersJson);

        job.AssignTargetDefinition(
            nameof(DefinitionKind.Analysis), TargetId, JobTargetVersionPolicy.Pinned, 9,
            "{\"window_days\":90}");

        Assert.Equal("{\"window_days\":7}", run.TargetParametersJson);
        Assert.Equal(2, run.TargetDefinitionVersion);
        Assert.Equal("{\"window_days\":90}", job.TargetParametersJson);
        Assert.Equal(9, job.TargetDefinitionVersion);
    }

    [Fact]
    public void A_run_history_refuses_malformed_parameters()
    {
        JobRunHistory run = NewRun();

        Assert.Throws<ArgumentException>(() => run.RecordResolvedTarget(
            nameof(DefinitionKind.Analysis), TargetId, 1,
            JobTargetVersionPolicy.Pinned, "{broken"));

        // The refusal records nothing at all, not merely no parameters.
        Assert.Null(run.TargetParametersJson);
        Assert.Null(run.TargetDefinitionId);
        Assert.Null(run.TargetDefinitionKind);
        Assert.Null(run.TargetDefinitionVersion);
        Assert.Null(run.TargetVersionPolicy);
    }

    // --- helpers ---------------------------------------------------------------

    private static JobDefinition NewJob()
    {
        return new JobDefinition(
            "NIGHTLY_LEARNING", "Nightly learning", JobDefinitionType.MlWeeklyFull,
            "Daily 02:00", isSynthetic: false);
    }

    private static JobRunHistory NewRun()
    {
        return new JobRunHistory(
            Guid.NewGuid(), "NIGHTLY_LEARNING", "Nightly learning",
            JobDefinitionType.MlWeeklyFull, "Scheduler", null, null,
            isSynthetic: false, sourceSystem: null, sourceRecordId: null);
    }

    private static DefinitionVersionSummary Version(int number, bool published)
    {
        return new DefinitionVersionSummary(number, DateTime.UtcNow, "test", published);
    }

    private static JobTargetResolver ResolverWith(params DefinitionVersionSummary[] versions)
    {
        return new JobTargetResolver(
            new StubDefinitionService(versions), new DeclaredJobTargetClassPolicy(), new StubLookup());
    }

    private static JobTargetResolver ResolverWith(
        DefinitionVersionSummary[] versions, IJobTargetClassPolicy policy)
    {
        return new JobTargetResolver(new StubDefinitionService(versions), policy, new StubLookup());
    }

    private static JobTargetResolver ResolverWith(IJobTargetClassPolicy policy)
    {
        return new JobTargetResolver(
            new StubDefinitionService(Array.Empty<DefinitionVersionSummary>()), policy, new StubLookup());
    }

    private sealed class StubClassPolicy : IJobTargetClassPolicy
    {
        private readonly JobTargetClassRule _rule;

        public StubClassPolicy(JobTargetClassRule rule)
        {
            _rule = rule;
        }

        public JobTargetClassRule RuleFor(JobDefinitionType jobClass) => _rule;
    }

    private sealed class StubLookup : IJobTargetLookup
    {
        private readonly string[] _jobCodes;

        public StubLookup(params string[] jobCodes)
        {
            _jobCodes = jobCodes;
        }

        public Task<IReadOnlyList<string>> JobCodesTargetingAsync(
            string targetDefinitionKind, Guid definitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(_jobCodes);
        }
    }

    private class StubDefinitionService : IDefinitionService
    {
        private readonly IReadOnlyList<DefinitionVersionSummary> _versions;

        public StubDefinitionService(IReadOnlyList<DefinitionVersionSummary> versions)
        {
            _versions = versions;
        }

        public virtual Task<ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>> ListVersionsAsync(
            DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>.Success(_versions));
        }

        public Task<ApplicationResult<DefinitionSnapshot>> CreateAsync(
            DefinitionKind kind, string payloadJson, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotUsed());
        }

        public Task<ApplicationResult<DefinitionSnapshot>> UpdateAsync(
            DefinitionKind kind, Guid definitionId, string payloadJson, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotUsed());
        }

        public Task<ApplicationResult<DefinitionSnapshot>> GetCurrentAsync(
            DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotUsed());
        }

        public Task<ApplicationResult<DefinitionSnapshot>> GetVersionAsync(
            DefinitionKind kind, Guid definitionId, int versionNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotUsed());
        }

        public Task<ApplicationResult<DefinitionSnapshot>> PublishAsync(
            DefinitionKind kind, Guid definitionId, int versionNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult(NotUsed());
        }

        private static ApplicationResult<DefinitionSnapshot> NotUsed()
        {
            return ApplicationResult<DefinitionSnapshot>.Failure(
                ApplicationError.Validation("This stub answers version history only."));
        }
    }

    private sealed class RefusingDefinitionService : StubDefinitionService
    {
        private readonly string _reason;

        public RefusingDefinitionService(string reason)
            : base(Array.Empty<DefinitionVersionSummary>())
        {
            _reason = reason;
        }

        public override Task<ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>> ListVersionsAsync(
            DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>.Failure(
                    ApplicationError.Validation(_reason)));
        }
    }

    private sealed class CountingDefinitionService : StubDefinitionService
    {
        public CountingDefinitionService()
            : base(Array.Empty<DefinitionVersionSummary>())
        {
        }

        public int Calls { get; private set; }

        public override Task<ApplicationResult<IReadOnlyList<DefinitionVersionSummary>>> ListVersionsAsync(
            DefinitionKind kind, Guid definitionId, CancellationToken cancellationToken)
        {
            Calls++;
            return base.ListVersionsAsync(kind, definitionId, cancellationToken);
        }
    }
}
