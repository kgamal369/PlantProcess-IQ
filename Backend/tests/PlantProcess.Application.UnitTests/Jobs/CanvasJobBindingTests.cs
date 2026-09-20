using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Canvas;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// T-245. The Canvas consumer of the governed job model, without a database: identity
/// resolved on the server, capability answered without executing anything, binding that
/// carries the exact version, and evidence reads that refuse a run belonging elsewhere.
/// </summary>
public sealed class CanvasJobBindingTests
{
    private static readonly Guid Tenant = Guid.Parse("11110000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-0000-0000-000000000002");
    private static readonly Guid DefinitionId = Guid.Parse("22220000-0000-0000-0000-000000000001");

    private const string Code = "t245_canvas";

    private sealed class FakeDefinitions : ICanonicalDefinitionWriter
    {
        public Dictionary<string, Guid> ByCode { get; } = new(StringComparer.Ordinal) { [Tenant + "|" + Code] = DefinitionId };
        public int PublishedVersion { get; set; } = 3;
        public CanonicalVersionStatus ExactStatus { get; set; } = CanonicalVersionStatus.Published;
        public bool ExactMissing { get; set; }
        public List<int> ExactRequests { get; } = new();

        public Task<ApplicationResult<Guid?>> FindByCodeAsync(Guid tenantId, string definitionCode, CancellationToken cancellationToken)
        {
            Guid found;
            bool known = ByCode.TryGetValue(tenantId + "|" + definitionCode, out found);
            return Task.FromResult(ApplicationResult<Guid?>.Success(known ? found : (Guid?)null));
        }

        public Task<ApplicationResult<CanonicalDefinitionVersion>> ResolveExactAsync(Guid definitionId, int versionNumber, CancellationToken cancellationToken)
        {
            ExactRequests.Add(versionNumber);

            if (ExactMissing)
            {
                return Task.FromResult(ApplicationResult<CanonicalDefinitionVersion>.Failure(
                    ApplicationError.NotFound("absent")));
            }

            return Task.FromResult(ApplicationResult<CanonicalDefinitionVersion>.Success(Version(versionNumber, ExactStatus)));
        }

        public Task<ApplicationResult<CanonicalDefinitionVersion>> ResolvePublishedAsync(Guid definitionId, CancellationToken cancellationToken)
            => Task.FromResult(ApplicationResult<CanonicalDefinitionVersion>.Success(Version(PublishedVersion, CanonicalVersionStatus.Published)));

        private static CanonicalDefinitionVersion Version(int number, CanonicalVersionStatus status) =>
            new(DefinitionId, Guid.NewGuid(), Code, DefinitionKind.Transformation, "S1", number, status,
                "{}", "hash-" + number, DateTime.UtcNow, null);

        public Task<ApplicationResult<CanonicalDefinitionVersion>> WriteVersionAsync(CanonicalDefinitionWrite write, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ApplicationResult<CanonicalDefinitionVersion>> PublishAsync(Guid definitionId, int versionNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ApplicationResult> RetireAsync(Guid definitionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeExecutor : IJobExecutor
    {
        public ApplicationResult<JobExecutionPlan>? Admission { get; set; }
        public List<ResolvedJobTarget> Admitted { get; } = new();
        public int Executions { get; private set; }

        public JobDefinitionType Executes => JobDefinitionType.CanonicalRefresh;

        private sealed record Plan(ResolvedJobTarget Resolved) : JobExecutionPlan(Resolved);

        public Task<ApplicationResult<JobExecutionPlan>> AdmitAsync(ResolvedJobTarget target, CancellationToken cancellationToken)
        {
            Admitted.Add(target);
            return Task.FromResult(Admission ?? ApplicationResult<JobExecutionPlan>.Success(new Plan(target)));
        }

        public Task<ApplicationResult<JobExecutionOutcome>> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            Executions++;
            throw new InvalidOperationException("Capability discovery must never execute.");
        }
    }

    private sealed class FakeJobs : IJobDefinitionService
    {
        public List<GovernedJobBindingRequest> Bindings { get; } = new();
        public ApplicationError? Refusal { get; set; }

        public Task<ApplicationResult<GovernedJobBindingResult>> BindGovernedTargetAsync(
            GovernedJobBindingRequest request, CancellationToken cancellationToken)
        {
            if (Refusal is not null)
            {
                return Task.FromResult(ApplicationResult<GovernedJobBindingResult>.Failure(Refusal));
            }

            Bindings.Add(request);
            bool first = Bindings.Count(b => b.JobCode == request.JobCode) == 1;

            var dto = new JobDefinitionDto(
                Guid.Parse("33330000-0000-0000-0000-000000000001"), request.JobCode, request.JobName, request.JobType,
                null, null, "Manual", true, null, null, null, JobRunStatus.Running, null, null, null, false,
                DateTime.UtcNow, first ? null : DateTime.UtcNow);

            return Task.FromResult(ApplicationResult<GovernedJobBindingResult>.Success(
                new GovernedJobBindingResult(dto, first)));
        }

        public Task<ApplicationResult<IReadOnlyList<JobDefinitionDto>>> GetJobsAsync(JobDefinitionType? jobType, bool includeDisabled, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult<JobDefinitionDto>> GetJobByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult<JobDefinitionDto>> CreateJobAsync(CreateJobDefinitionRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult<JobDefinitionDto>> UpdateJobAsync(Guid id, UpdateJobDefinitionRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult<JobDefinitionDto>> EnableJobAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult<JobDefinitionDto>> DisableJobAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult<JobDefinitionDto>> UpdateRunStatusAsync(Guid id, UpdateJobRunStatusRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationResult> EnsureSystemJobsSeededAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class World
    {
        public FakeDefinitions Definitions { get; } = new();
        public FakeExecutor Executor { get; } = new();
        public FakeJobs Jobs { get; } = new();
        public bool RegisterExecutor { get; set; } = true;

        public FakeReadModel ReadModel { get; } = new();

        public CanvasJobBindingService Service => new(
            Definitions,
            Jobs,
            new JobExecutionCapabilityAuthority(),
            new JobExecutorResolver(RegisterExecutor ? new IJobExecutor[] { Executor } : Array.Empty<IJobExecutor>()),
            ReadModel);
    }

    private sealed class FakeReadModel : ICanvasJobReadModel
    {
        public CanvasBoundJob? Bound { get; set; }
        public CanvasRunRecord? Run { get; set; }
        public CanvasRunRecord? ByCorrelation { get; set; }
        public List<CanvasRunBlockDto> Blocks { get; } = new();

        public Task<CanvasBoundJob?> FindBoundJobAsync(Guid targetDefinitionId, JobDefinitionType family, CancellationToken ct)
            => Task.FromResult(Bound);

        public Task<CanvasRunRecord?> FindRunAsync(Guid runId, CancellationToken ct)
            => Task.FromResult(Run);

        public Task<CanvasRunRecord?> FindRunByCorrelationAsync(Guid jobDefinitionId, string correlationId, CancellationToken ct)
            => Task.FromResult(ByCorrelation);

        public Task<IReadOnlyList<CanvasRunBlockDto>> ListBlocksAsync(Guid runId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CanvasRunBlockDto>>(Blocks);
    }

    private static readonly Guid JobId = Guid.Parse("33330000-0000-0000-0000-000000000001");

    private static CanvasBoundJob BoundJob() =>
        new(JobId, "CANVAS_T245_CANVAS", "Canvas projection", DefinitionKind.Transformation.ToString(),
            DefinitionId, 2, JobTargetVersionPolicy.Pinned);

    private static CanvasRunRecord RunFor(Guid jobId, string correlation, JobRunStatus status = JobRunStatus.Running) =>
        new(Guid.Parse("44440000-0000-0000-0000-000000000001"), jobId, "CANVAS_T245_CANVAS", status,
            DateTime.UtcNow, null, correlation, DefinitionId, 2, DefinitionKind.Transformation.ToString(),
            JobTargetVersionPolicy.Pinned, null, null, null, null);

    [Fact]
    public void A_job_code_is_derived_deterministically_from_the_definition_code()
    {
        Assert.Equal("CANVAS_T245_CANVAS", CanvasJobBindingService.JobCodeFor(Code));
        Assert.Equal(CanvasJobBindingService.JobCodeFor(Code), CanvasJobBindingService.JobCodeFor("  " + Code + " "));
        Assert.Equal("CANVAS_A_B_C", CanvasJobBindingService.JobCodeFor("a-b.c"));
    }

    [Fact]
    public async Task A_definition_another_tenant_owns_is_not_resolved_for_this_caller()
    {
        var world = new World();

        var result = await world.Service.DescribeExecutionAsync(OtherTenant, Code, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorType.NotFound, result.Error!.Type);
        Assert.Empty(world.Executor.Admitted);
    }

    [Fact]
    public async Task Capability_answers_without_executing_anything()
    {
        var world = new World();

        var result = await world.Service.DescribeExecutionAsync(Tenant, Code, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.StaticallyEligible);
        Assert.True(result.Value.ExecutorRegistered);
        Assert.Equal(3, result.Value.ResolvedVersion);
        Assert.Equal(0, world.Executor.Executions);
        Assert.Contains("reserves no capacity", result.Value.Assurance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pinned_request_is_described_against_that_exact_version()
    {
        var world = new World();

        var result = await world.Service.DescribeExecutionAsync(Tenant, Code, 2, CancellationToken.None);

        Assert.Equal(2, result.Value!.ResolvedVersion);
        Assert.Equal(new[] { 2 }, world.Definitions.ExactRequests.ToArray());
        Assert.Equal(JobTargetVersionPolicy.Pinned.ToString(), result.Value.VersionPolicy);
        Assert.Equal(2, world.Executor.Admitted.Single().ResolvedVersion);
    }

    [Fact]
    public async Task No_registered_executor_is_refused_before_any_launch()
    {
        var world = new World { RegisterExecutor = false };

        var result = await world.Service.DescribeExecutionAsync(Tenant, Code, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.ExecutorRegistered);
        Assert.False(result.Value.StaticallyEligible);
        Assert.Equal(JobExecutionDiagnosticCodes.ExecutorMissing, result.Value.RefusalCode);
    }

    [Fact]
    public async Task An_admission_refusal_is_carried_with_its_own_typed_code()
    {
        var world = new World();
        world.Executor.Admission = ApplicationResult<JobExecutionPlan>.Failure(new ApplicationError(
            JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned, "not commissioned", ApplicationErrorType.BusinessRule));

        var result = await world.Service.DescribeExecutionAsync(Tenant, Code, null, CancellationToken.None);

        Assert.False(result.Value!.StaticallyEligible);
        Assert.Equal(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned, result.Value.RefusalCode);
        Assert.Equal("not commissioned", result.Value.RefusalDetail);
    }

    [Fact]
    public async Task Binding_carries_the_pinned_version_and_never_substitutes_latest()
    {
        var world = new World();

        var result = await world.Service.BindAsync(Tenant, new CanvasJobBindingRequest(Code, 2, null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        GovernedJobBindingRequest binding = world.Jobs.Bindings.Single();
        Assert.Equal(JobTargetVersionPolicy.Pinned, binding.VersionPolicy);
        Assert.Equal(2, binding.PinnedVersion);
        Assert.Equal(DefinitionId, binding.TargetDefinitionId);
        Assert.Equal(JobDefinitionType.CanonicalRefresh, binding.JobType);
        Assert.Equal(DefinitionKind.Transformation.ToString(), binding.TargetDefinitionKind);
        Assert.True(result.Value!.Created);
    }

    [Fact]
    public async Task Binding_without_a_version_follows_the_current_published_policy()
    {
        var world = new World();

        await world.Service.BindAsync(Tenant, new CanvasJobBindingRequest(Code, null, "Nightly projection"), CancellationToken.None);

        GovernedJobBindingRequest binding = world.Jobs.Bindings.Single();
        Assert.Equal(JobTargetVersionPolicy.CurrentPublished, binding.VersionPolicy);
        Assert.Null(binding.PinnedVersion);
        Assert.Equal("Nightly projection", binding.JobName);
    }

    [Fact]
    public async Task Binding_the_same_definition_twice_addresses_the_same_job()
    {
        var world = new World();

        var first = await world.Service.BindAsync(Tenant, new CanvasJobBindingRequest(Code, 2, null), CancellationToken.None);
        var second = await world.Service.BindAsync(Tenant, new CanvasJobBindingRequest(Code, 3, null), CancellationToken.None);

        Assert.Equal(2, world.Jobs.Bindings.Count);
        Assert.Equal(world.Jobs.Bindings[0].JobCode, world.Jobs.Bindings[1].JobCode);
        Assert.True(first.Value!.Created);
        Assert.False(second.Value!.Created);
    }

    [Fact]
    public async Task An_unpublished_version_cannot_be_bound()
    {
        var world = new World();
        world.Definitions.ExactStatus = CanonicalVersionStatus.Draft;

        var result = await world.Service.BindAsync(Tenant, new CanvasJobBindingRequest(Code, 2, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(world.Jobs.Bindings);
    }

    [Fact]
    public async Task A_version_that_cannot_be_resolved_is_refused_rather_than_bound()
    {
        var world = new World();
        world.Definitions.ExactMissing = true;

        var result = await world.Service.BindAsync(Tenant, new CanvasJobBindingRequest(Code, 9, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(world.Jobs.Bindings);
    }

    [Fact]
    public async Task A_run_that_belongs_to_another_job_is_not_returned()
    {
        var world = new World();
        world.ReadModel.Bound = BoundJob();
        world.ReadModel.Run = RunFor(Guid.Parse("33330000-0000-0000-0000-0000000000ff"), "corr-1");

        var result = await world.Service.ReadRunAsync(Tenant, Code, Guid.Parse("44440000-0000-0000-0000-000000000001"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Evidence_is_read_back_and_never_reconstructed()
    {
        var world = new World();
        world.ReadModel.Bound = BoundJob();
        world.ReadModel.Run = RunFor(JobId, "corr-1", JobRunStatus.Failed);
        world.ReadModel.Blocks.Add(new CanvasRunBlockDto("src_a", 0, "Succeeded", null, 7, null, null, DateTime.UtcNow, DateTime.UtcNow));
        world.ReadModel.Blocks.Add(new CanvasRunBlockDto("only-large", 1, "Failed", 2, null,
            JobExecutionDiagnosticCodes.SourceIdentityInvalid, "row 2", DateTime.UtcNow, DateTime.UtcNow));

        var result = await world.Service.ReadRunAsync(Tenant, Code, Guid.Parse("44440000-0000-0000-0000-000000000001"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsTerminal);
        Assert.Equal(2, result.Value.TargetDefinitionVersion);
        Assert.Equal(new[] { "src_a", "only-large" }, result.Value.Blocks.Select(b => b.BlockId).ToArray());
        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityInvalid, result.Value.Blocks[1].DiagnosticCode);
    }

    [Fact]
    public async Task A_launch_is_attached_by_its_own_correlation_and_absence_is_not_an_error()
    {
        var world = new World();
        world.ReadModel.Bound = BoundJob();
        world.ReadModel.ByCorrelation = null;

        var pending = await world.Service.FindRunByCorrelationAsync(Tenant, Code, "corr-mine", CancellationToken.None);
        Assert.True(pending.IsSuccess);
        Assert.Null(pending.Value);

        world.ReadModel.ByCorrelation = RunFor(JobId, "corr-mine");
        var attached = await world.Service.FindRunByCorrelationAsync(Tenant, Code, "corr-mine", CancellationToken.None);

        Assert.Equal("corr-mine", attached.Value!.CorrelationId);
        Assert.False(attached.Value.IsTerminal);
        Assert.False(attached.Value.CancellationRequested);
    }

    [Fact]
    public async Task A_cancellation_request_is_not_a_terminal_result()
    {
        var world = new World();
        world.ReadModel.Bound = BoundJob();
        world.ReadModel.Run = RunFor(JobId, "corr-1") with { CancellationRequestedAtUtc = DateTime.UtcNow };

        var result = await world.Service.ReadRunAsync(Tenant, Code, Guid.Parse("44440000-0000-0000-0000-000000000001"), CancellationToken.None);

        Assert.True(result.Value!.CancellationRequested);
        Assert.False(result.Value.IsTerminal);
        Assert.Equal(JobRunStatus.Running.ToString(), result.Value.Status);
    }
}
