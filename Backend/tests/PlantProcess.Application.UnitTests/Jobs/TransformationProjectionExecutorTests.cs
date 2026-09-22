using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Application.Definitions.Transformations;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Execution.Transformations;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs;

/// <summary>
/// The governed Transformation executor without a database: admission refusals, the
/// deterministic block plan, identity handling, typed block failure and cancellation.
/// Every target and field name here is synthetic; nothing names a customer model.
/// </summary>
public sealed class TransformationProjectionExecutorTests
{
    private const string Target = "SyntheticTarget";
    private const string Table = "src_a";
    private const string SecondTable = "src_b";

    // ------------------------------------------------------------------ fakes --

    private sealed class FakeCatalog : ICanonicalEntityCatalog
    {
        public IReadOnlyList<CanonicalProjectionField> ProjectionFieldsOf(string canonicalEntityName) =>
            new[]
            {
                new CanonicalProjectionField("Code", "String", true, false),
                new CanonicalProjectionField("Id", "Guid", true, true),
            };

        public IReadOnlyList<string> ProjectionTargetNames() => new[] { Target };

        public bool IsProjectionTarget(string canonicalEntityName) => true;

        public string? NameOf(Type mappedEntityType) => null;

        public Type? FindType(string canonicalEntityName) => null;

        public string? PrimaryKeyMemberOf(Type mappedEntityType) => null;
    }

    private sealed class FakeWriter : ITransformationCanonicalWriter
    {
        public string Commissioned { get; set; } = Target;
        public string? Unwritable { get; set; }
        public ApplicationResult<CanonicalWriteResult>? Answer { get; set; }
        public List<CanonicalWriteRequest> Requests { get; } = new();
        public List<string> Calls { get; set; } = new();
        public long NextGeneration { get; set; } = 41;

        public Task<long> ReserveProjectionGenerationAsync(CancellationToken cancellationToken)
        {
            Calls.Add("reserve");
            long generation = NextGeneration;
            NextGeneration = NextGeneration + 1;
            return Task.FromResult(generation);
        }

        public bool IsCommissioned(string targetEntity) =>
            string.Equals(targetEntity, Commissioned, StringComparison.Ordinal);

        public string? FirstUnwritableField(string targetEntity, IReadOnlyCollection<string> boundFields) => Unwritable;

        public Task<ApplicationResult<CanonicalWriteResult>> WriteAsync(
            CanonicalWriteRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Calls.Add("write");
            return Task.FromResult(Answer ?? ApplicationResult<CanonicalWriteResult>.Success(
                new CanonicalWriteResult(request.Rows.Count, 0)));
        }
    }

    private sealed class FakeDefinitions : ICanonicalDefinitionWriter
    {
        public string ContentJson { get; set; } = string.Empty;
        public bool Missing { get; set; }

        public Task<ApplicationResult<CanonicalDefinitionVersion>> ResolveExactAsync(
            Guid definitionId, int versionNumber, CancellationToken cancellationToken)
        {
            if (Missing)
            {
                return Task.FromResult(ApplicationResult<CanonicalDefinitionVersion>.Failure(
                    ApplicationError.NotFound("absent")));
            }

            return Task.FromResult(ApplicationResult<CanonicalDefinitionVersion>.Success(
                new CanonicalDefinitionVersion(
                    definitionId, Guid.NewGuid(), "synthetic", DefinitionKind.Transformation, "S1",
                    versionNumber, CanonicalVersionStatus.Published, ContentJson, "hash-" + versionNumber,
                    DateTime.UtcNow, null)));
        }

        public Task<ApplicationResult<CanonicalDefinitionVersion>> WriteVersionAsync(
            CanonicalDefinitionWrite write, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<CanonicalDefinitionVersion>> PublishAsync(
            Guid definitionId, int versionNumber, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<CanonicalDefinitionVersion>> ResolvePublishedAsync(
            Guid definitionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Execution never re-resolves the published version.");

        public Task<ApplicationResult> RetireAsync(Guid definitionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApplicationResult<Guid?>> FindByCodeAsync(
            Guid tenantId, string definitionCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeReader : ITransformationSourceReader
    {
        public List<string> Capable { get; set; } = new() { Table };
        public List<object?[]> Rows { get; set; } = new();
        public bool FailQuery { get; set; }
        public List<string> Statements { get; } = new();
        public List<string> Lineage { get; set; } = new();
        public List<string>? CallLog { get; set; }

        public Task<IReadOnlyList<string>> LineageColumnsAsync(
            string schema, string relation, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Lineage.OrderBy(x => x, StringComparer.Ordinal).ToList());

        public Task<IReadOnlyList<string>> ProvenanceCapableRelationsAsync(
            string schema, IReadOnlyList<string> relations, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Capable.Where(c => relations.Contains(c)).ToList());

        public Task<long> CountRowsAsync(string schema, string relation, CancellationToken cancellationToken) =>
            Task.FromResult(7L);

        public Task<IReadOnlyList<object?[]>> ReadAsync(
            string sql, IReadOnlyList<object> parameters, int expectedColumnCount, CancellationToken cancellationToken)
        {
            Statements.Add(sql);
            CallLog?.Add("read");
            if (FailQuery) { throw new InvalidOperationException("relation vanished"); }
            Assert.All(Rows, r => Assert.Equal(expectedColumnCount, r.Length));
            return Task.FromResult<IReadOnlyList<object?[]>>(Rows);
        }
    }

    private sealed class FakeEvidence : IJobRunBlockEvidenceStore
    {
        public List<JobRunBlockEvidence> Rows { get; } = new();
        public int Saves { get; private set; }

        public Task AddAsync(IReadOnlyList<JobRunBlockEvidence> evidence, CancellationToken cancellationToken)
        {
            Rows.AddRange(evidence);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }

        public Task DiscardAsync(JobRunBlockEvidence evidence, CancellationToken cancellationToken)
        {
            // Nothing is persisted here, so there is nothing to go back to.
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<JobRunBlockEvidence>> ListAsync(Guid jobRunHistoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JobRunBlockEvidence>>(Rows);
    }

    private sealed class FakeProbe : IJobRunCancellationProbe
    {
        public int RequestFromCall { get; set; } = int.MaxValue;
        public int Calls { get; private set; }

        public Task<bool> IsRequestedAsync(Guid jobRunHistoryId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Calls >= RequestFromCall);
        }
    }

    private sealed class World
    {
        public FakeWriter Writer { get; } = new();
        public FakeDefinitions Definitions { get; } = new();
        public FakeReader Reader { get; } = new();
        public FakeEvidence Evidence { get; } = new();
        public FakeProbe Probe { get; } = new();

        public TransformationProjectionJobExecutor Executor => new(
            new FakeCatalog(), Writer, Definitions, Reader,
            new CanvasStagingSchema("ppiq_staging"), Evidence, Probe);
    }

    private static readonly Guid DefinitionId = Guid.Parse("7a1f0000-0000-0000-0000-000000000001");

    private static ResolvedJobTarget Resolved(int version = 1) => new()
    {
        Kind = DefinitionKind.Transformation,
        DefinitionId = DefinitionId,
        ResolvedVersion = version,
        PolicyApplied = JobTargetVersionPolicy.Pinned,
    };

    private static string Content(
        string[]? tables = null,
        string joins = "[]",
        string? board = null,
        string? projection = null,
        string target = Target)
    {
        tables ??= new[] { Table };
        string tableList = string.Join(",", tables.Select(t => "\"" + t + "\""));
        board ??= "{\"nodes\":[" + string.Join(",", tables.Select(t => "{\"id\":\"" + t + "\",\"kind\":\"dataset\"}"))
            + ",{\"id\":\"f1\",\"kind\":\"filter\"}],\"edges\":[{\"source\":\"" + tables[0] + "\",\"target\":\"f1\"}]}";
        projection ??= "{\"targetEntity\":\"" + target + "\",\"fieldBindings\":[{\"targetField\":\"Code\",\"sourceKind\":\"column\",\"sourceTable\":\""
            + tables[0] + "\",\"sourceField\":\"code\"}]}";

        string graph = "{\"name\":\"synthetic\",\"targetEntity\":\"" + target + "\",\"tables\":[" + tableList + "],"
            + "\"joins\":" + joins + ","
            + "\"filters\":[{\"table\":\"" + tables[0] + "\",\"column\":\"qty\",\"op\":\">\",\"value\":\"1\"}]"
            + (board.Length == 0 ? string.Empty : ",\"board\":" + board)
            + ",\"projection\":" + projection + "}";

        return CanvasDefinitionContent.ForGraph(graph, target);
    }

    private static async Task<TransformationAdmittedPlan> AdmitAsync(World world)
    {
        var admitted = await world.Executor.AdmitAsync(Resolved(), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Code + " " + admitted.Error?.Message);
        return Assert.IsType<TransformationAdmittedPlan>(admitted.Value);
    }

    private static ResolvedJobTarget ResolvedWith(string? parametersJson, int version = 1) => new()
    {
        Kind = DefinitionKind.Transformation,
        DefinitionId = DefinitionId,
        ResolvedVersion = version,
        PolicyApplied = JobTargetVersionPolicy.Pinned,
        ParametersJson = parametersJson,
    };

    private static JobExecutionContext ContextFor(JobExecutionPlan plan) =>
        new(Guid.NewGuid(), "SYNTHETIC", JobDefinitionType.CanonicalRefresh, Guid.NewGuid(), plan, "corr");

    private static async Task<string> RefusalAsync(World world, int version = 1)
    {
        var admitted = await world.Executor.AdmitAsync(Resolved(version), CancellationToken.None);
        Assert.True(admitted.IsFailure);
        return admitted.Error!.Code;
    }

    // --------------------------------------------------------- declaration -----

    [Fact]
    public void The_executor_declares_exactly_one_family()
    {
        Assert.Equal(JobDefinitionType.CanonicalRefresh, new World().Executor.Executes);
    }

    [Fact]
    public void A_version_with_no_declaration_is_refused_with_the_frozen_code()
    {
        Assert.Equal("PROJECTION_DECLARATION_REQUIRED", TransformationProjectionJobExecutor.ProjectionRequiredCode);
        Assert.Equal(TransformationProjectionJobExecutor.ProjectionRequiredCode,
            new World().Executor.AdmitDeclaration(null, Target));
    }

    [Fact]
    public void Declaration_defects_keep_the_projection_vocabulary()
    {
        var executor = new World().Executor;

        Assert.Equal(TransformationProjectionJobExecutor.ProjectionTargetMismatchCode,
            executor.AdmitDeclaration(Declaration("Other", "Code"), Target));
        Assert.Equal(TransformationProjectionJobExecutor.ProjectionFieldUnknownCode,
            executor.AdmitDeclaration(Declaration(Target, "Missing"), Target));
        Assert.Equal(TransformationProjectionJobExecutor.ProjectionFieldSystemOwnedCode,
            executor.AdmitDeclaration(Declaration(Target, "Id"), Target));
        Assert.Null(executor.AdmitDeclaration(Declaration(Target, "Code"), Target));
    }

    private static CanvasProjectionDeclaration Declaration(string target, string field) =>
        new(target, new[] { new ProjectionFieldBinding(field, CanvasProjectionDeclaration.KindColumn, Table, "code") });

    // ---------------------------------------------------------- admission -------

    [Fact]
    public async Task A_resolution_without_an_exact_version_is_refused_with_its_own_code()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();

        Assert.Equal(JobExecutionDiagnosticCodes.ExactVersionRequired, await RefusalAsync(world, version: 0));
        Assert.NotEqual(JobExecutionDiagnosticCodes.ExecutorMissing, await RefusalAsync(world, version: 0));
    }

    [Fact]
    public async Task An_unreadable_exact_version_is_refused_before_any_run()
    {
        var world = new World();
        world.Definitions.Missing = true;

        Assert.Equal(JobExecutionDiagnosticCodes.ExactVersionRequired, await RefusalAsync(world));
    }

    [Fact]
    public async Task An_uncommissioned_target_is_refused_before_mutation()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Writer.Commissioned = "SomethingElse";

        Assert.Equal(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned, await RefusalAsync(world));
        Assert.Empty(world.Writer.Requests);
        Assert.Empty(world.Reader.Statements);
    }

    [Fact]
    public async Task A_bound_field_without_a_sanctioned_path_is_refused()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Writer.Unwritable = "Code";

        Assert.Equal(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned, await RefusalAsync(world));
    }

    [Fact]
    public async Task A_version_without_an_authored_board_is_refused()
    {
        var world = new World();
        world.Definitions.ContentJson = Content(board: string.Empty);

        Assert.Equal(JobExecutionDiagnosticCodes.BlocksUnavailable, await RefusalAsync(world));
    }

    [Fact]
    public async Task No_provenance_capable_relation_is_refused_as_unavailable()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Capable = new List<string>();

        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityUnavailable, await RefusalAsync(world));
    }

    [Fact]
    public async Task Two_independent_provenance_relations_are_refused_as_ambiguous()
    {
        var world = new World();
        world.Definitions.ContentJson = Content(
            tables: new[] { Table, SecondTable },
            joins: "[{\"leftTable\":\"src_a\",\"leftColumn\":\"k\",\"rightTable\":\"src_b\",\"rightColumn\":\"k\"}]");
        world.Reader.Capable = new List<string> { Table, SecondTable };

        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityAmbiguous, await RefusalAsync(world));
    }

    [Fact]
    public async Task An_authored_statement_version_exposes_no_source_identity()
    {
        var world = new World();
        string projection = "{\"targetEntity\":\"" + Target + "\",\"fieldBindings\":[{\"targetField\":\"Code\",\"sourceKind\":\"sql\",\"sourceField\":\"code\"}]}";
        world.Definitions.ContentJson = CanvasDefinitionContent.ForSql("SELECT 1 AS code", null, Target, projection);

        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityUnavailable, await RefusalAsync(world));
    }

    [Fact]
    public async Task A_lawful_version_is_admitted_with_a_deterministic_plan_and_provenance_projection()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();

        TransformationAdmittedPlan plan = await AdmitAsync(world);

        Assert.Equal(DefinitionId, plan.Target.DefinitionId);
        Assert.Equal(1, plan.Target.ResolvedVersion);
        Assert.Equal("hash-1", plan.DefinitionHash);
        Assert.Equal(Table, plan.ProvenanceRelation);
        Assert.Equal(new[] { Table, "f1" }, plan.Blocks.Select(b => b.BlockId).ToArray());
        Assert.Equal(new[] { 0, 1 }, plan.Blocks.Select(b => b.ExecutionOrdinal).ToArray());
        Assert.Equal(
            new[] { "code", "source_system", "source_record_id" },
            plan.ExecutionGraph.Selects!.Select(s => s.Column).ToArray());
        Assert.Null(plan.ExecutionGraph.Board);
        Assert.Null(plan.ExecutionGraph.Projection);
    }

    [Fact]
    public void The_execution_read_purpose_carries_no_preview_cap_and_preview_is_unchanged()
    {
        var graph = new MapperGraph("g", Target, new[] { Table }, Array.Empty<JoinSpec>());

        var preview = TransformationSafeSelect.BuildSafeSelect(graph, "ppiq_staging");
        var execution = TransformationSafeSelect.BuildSafeSelect(graph, "ppiq_staging", TransformationReadPurpose.Execution);

        Assert.EndsWith(" LIMIT 50;", preview.sql, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT", execution.sql, StringComparison.Ordinal);
        Assert.Equal(preview.sql, execution.sql + " LIMIT 50;");
    }

    // ---------------------------------------------------------- execution -------

    [Fact]
    public async Task A_lawful_run_writes_through_the_governed_writer_with_source_identity_and_evidence()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Rows = new List<object?[]>
        {
            new object?[] { "A-1", "sys", "r1" },
            new object?[] { "A-2", "sys", "r2" },
        };
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Succeeded);
        Assert.False(result.Value.Cancelled);
        Assert.Single(world.Writer.Requests);
        CanonicalWriteRequest request = world.Writer.Requests[0];
        Assert.Equal(DefinitionId, request.TargetDefinitionId);
        Assert.Equal(1, request.ResolvedVersion);
        Assert.Equal(new[] { "r1", "r2" }, request.Rows.Select(r => r.SourceRecordId).ToArray());
        Assert.Equal("A-1", request.Rows[0].Fields["Code"]);
        Assert.DoesNotContain("LIMIT", world.Reader.Statements.Single(), StringComparison.Ordinal);

        Assert.Equal(2, world.Evidence.Rows.Count);
        Assert.All(world.Evidence.Rows, e => Assert.Equal(JobRunBlockStatus.Succeeded, e.Status));
        Assert.Equal(7, world.Evidence.Rows[0].OutputRows);
        Assert.Equal(2, world.Evidence.Rows[1].InputRows);
        Assert.Equal(2, world.Evidence.Rows[1].OutputRows);
    }

    [Fact]
    public async Task A_row_without_identity_is_refused_before_the_writer_is_called()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Rows = new List<object?[]>
        {
            new object?[] { "A-1", "sys", "r1" },
            new object?[] { "A-2", "sys", "   " },
        };
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.False(result.Value!.Succeeded);
        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityInvalid, result.Value.DiagnosticCode);
        Assert.Empty(world.Writer.Requests);
        Assert.Equal(JobRunBlockStatus.Succeeded, world.Evidence.Rows[0].Status);
        Assert.Equal(JobRunBlockStatus.Failed, world.Evidence.Rows[1].Status);
        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityInvalid, world.Evidence.Rows[1].DiagnosticCode);
    }

    [Fact]
    public async Task A_duplicated_identity_is_refused_as_ambiguous_before_the_writer_is_called()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Rows = new List<object?[]>
        {
            new object?[] { "A-1", "sys", "r1" },
            new object?[] { "A-9", "sys", "r1" },
        };
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.Equal(JobExecutionDiagnosticCodes.SourceIdentityAmbiguous, result.Value!.DiagnosticCode);
        Assert.Empty(world.Writer.Requests);
    }

    [Fact]
    public async Task A_writer_refusal_is_a_typed_failure_of_the_final_block()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Rows = new List<object?[]> { new object?[] { "A-1", "sys", "r1" } };
        world.Writer.Answer = ApplicationResult<CanonicalWriteResult>.Failure(new ApplicationError(
            JobExecutionDiagnosticCodes.CanonicalIdentityConflict, "different effect", ApplicationErrorType.BusinessRule));
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.False(result.Value!.Succeeded);
        Assert.Equal(JobExecutionDiagnosticCodes.CanonicalIdentityConflict, result.Value.DiagnosticCode);
        Assert.Equal(JobRunBlockStatus.Failed, world.Evidence.Rows[1].Status);
        Assert.Equal("f1", result.Value.BlockResults.Last().BlockId);
    }

    [Fact]
    public async Task A_query_failure_is_typed_and_writes_nothing()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.FailQuery = true;
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.Equal(JobExecutionDiagnosticCodes.QueryFailed, result.Value!.DiagnosticCode);
        Assert.Empty(world.Writer.Requests);
    }

    [Fact]
    public async Task A_request_seen_before_the_first_block_stops_everything_and_writes_nothing()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Probe.RequestFromCall = 1;
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.True(result.Value!.Cancelled);
        Assert.False(result.Value.Succeeded);
        Assert.Empty(world.Writer.Requests);
        Assert.All(world.Evidence.Rows, e => Assert.Equal(JobRunBlockStatus.Cancelled, e.Status));
        Assert.All(world.Evidence.Rows, e => Assert.Null(e.OutputRows));
    }

    [Fact]
    public async Task A_request_seen_at_the_write_boundary_stops_before_the_write()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Rows = new List<object?[]> { new object?[] { "A-1", "sys", "r1" } };
        world.Probe.RequestFromCall = 3;
        TransformationAdmittedPlan plan = await AdmitAsync(world);

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.True(result.Value!.Cancelled);
        Assert.Empty(world.Writer.Requests);
        Assert.Equal(JobRunBlockStatus.Succeeded, world.Evidence.Rows[0].Status);
        Assert.Equal(JobRunBlockStatus.Cancelled, world.Evidence.Rows[1].Status);
    }

    [Fact]
    public async Task A_context_without_an_admitted_plan_is_refused()
    {
        var world = new World();
        var context = new JobExecutionContext(
            Guid.NewGuid(), "SYNTHETIC", JobDefinitionType.CanonicalRefresh, Guid.Empty, null!, null);

        var result = await world.Executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.ExactVersionRequired, result.Error!.Code);
        Assert.Empty(world.Evidence.Rows);
    }

    // ------------------------------------------------------------- plan order ---

    [Fact]
    public void Independent_blocks_execute_in_a_stable_order()
    {
        var deps = new Dictionary<string, IReadOnlyList<string>>();
        string? refusal;

        var first = TransformationExecutionPlan.Order(new[] { "node-c", "node-a", "node-b" }, deps, out refusal);
        var second = TransformationExecutionPlan.Order(new[] { "node-b", "node-c", "node-a" }, deps, out refusal);

        Assert.Null(refusal);
        Assert.Equal(first, second);
        Assert.Equal(new[] { "node-a", "node-b", "node-c" }, first);
    }

    [Fact]
    public void A_dependency_is_executed_before_the_block_that_consumes_it()
    {
        var deps = new Dictionary<string, IReadOnlyList<string>> { ["node-a"] = new[] { "node-z" } };
        string? refusal;

        var order = TransformationExecutionPlan.Order(new[] { "node-a", "node-z" }, deps, out refusal);

        Assert.Null(refusal);
        Assert.Equal(new[] { "node-z", "node-a" }, order);
    }

    [Fact]
    public void A_cycle_is_refused_rather_than_broken()
    {
        var deps = new Dictionary<string, IReadOnlyList<string>>
        {
            ["node-a"] = new[] { "node-b" },
            ["node-b"] = new[] { "node-a" }
        };
        string? refusal;

        var order = TransformationExecutionPlan.Order(new[] { "node-a", "node-b" }, deps, out refusal);

        Assert.NotNull(refusal);
        Assert.Empty(order);
    }

    // ------------------------------------------------ provenance and reprojection --

    [Theory]
    [InlineData(null, CanonicalProjectionMode.Ordinary)]
    [InlineData("", CanonicalProjectionMode.Ordinary)]
    [InlineData("{}", CanonicalProjectionMode.Ordinary)]
    [InlineData("{\"projection\":\"ordinary\"}", CanonicalProjectionMode.Ordinary)]
    [InlineData("{\"projection\":\"reproject\"}", CanonicalProjectionMode.Reproject)]
    public void The_canonical_refresh_vocabulary_reads_only_its_own_key(string? json, CanonicalProjectionMode expected)
    {
        Assert.Null(CanonicalProjectionParameters.TryRead(json, out CanonicalProjectionMode mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData("{\"projection\":\"Reproject\"}")]
    [InlineData("{\"projection\":true}")]
    [InlineData("{\"window_days\":7}")]
    [InlineData("{\"projection\":\"reproject\",\"projection\":\"reproject\"}")]
    [InlineData("[\"reproject\"]")]
    [InlineData("{broken")]
    public void Anything_outside_the_vocabulary_is_refused_and_never_read_as_authority(string json)
    {
        Assert.NotNull(CanonicalProjectionParameters.TryRead(json, out CanonicalProjectionMode mode));
        Assert.Equal(CanonicalProjectionMode.Ordinary, mode);
    }

    [Fact]
    public async Task Invalid_parameters_are_refused_with_their_own_code_before_the_version_is_read()
    {
        var world = new World();
        world.Definitions.Missing = true;

        var admitted = await world.Executor.AdmitAsync(ResolvedWith("{\"window_days\":7}"), CancellationToken.None);

        Assert.True(admitted.IsFailure);
        Assert.Equal(JobExecutionDiagnosticCodes.ProjectionParametersInvalid, admitted.Error!.Code);
        Assert.Empty(world.Reader.Statements);
    }

    [Fact]
    public async Task An_ordinary_run_carries_exact_version_hash_mode_and_a_generation_reserved_before_the_read()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.CallLog = world.Writer.Calls;
        world.Reader.Rows = new List<object?[]> { new object?[] { "A-1", "sys", "r1" } };

        TransformationAdmittedPlan plan = await AdmitAsync(world);
        Assert.Equal(CanonicalProjectionMode.Ordinary, plan.Mode);
        Assert.Empty(plan.LineageColumns ?? Array.Empty<string>());

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Succeeded);
        CanonicalWriteRequest request = Assert.Single(world.Writer.Requests);
        Assert.Equal("hash-1", request.DefinitionHash);
        Assert.Equal(1, request.ResolvedVersion);
        Assert.Equal(CanonicalProjectionMode.Ordinary, request.Mode);
        Assert.Equal(41, request.ProjectionGeneration);
        Assert.Null(request.Rows[0].Lineage);
        Assert.Equal(new[] { "reserve", "read", "write" }, world.Writer.Calls.ToArray());
    }

    [Fact]
    public async Task A_reproject_authority_is_frozen_into_the_plan_and_reaches_the_writer()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Rows = new List<object?[]> { new object?[] { "A-1", "sys", "r1" } };

        var admitted = await world.Executor.AdmitAsync(ResolvedWith("{\"projection\":\"reproject\"}"), CancellationToken.None);
        TransformationAdmittedPlan plan = Assert.IsType<TransformationAdmittedPlan>(admitted.Value);
        Assert.Equal(CanonicalProjectionMode.Reproject, plan.Mode);

        await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.Equal(CanonicalProjectionMode.Reproject, Assert.Single(world.Writer.Requests).Mode);
    }

    [Fact]
    public async Task Accepted_lineage_is_selected_and_carried_per_row_when_the_relation_exposes_it()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Lineage = new List<string> { "batch_id", "accepted_metadata" };
        Guid batch = Guid.NewGuid();
        Guid receipt = Guid.NewGuid();
        Guid dataset = Guid.NewGuid();
        Guid tenant = Guid.NewGuid();
        string metadata = "{\"tenantId\":\"" + tenant + "\",\"datasetGovernanceId\":\"" + dataset + "\",\"batchId\":\""
            + batch + "\",\"receiptId\":\"" + receipt + "\",\"contentHash\":\"" + new string('a', 64) + "\"}";
        world.Reader.Rows = new List<object?[]> { new object?[] { "A-1", "sys", "r1", metadata, batch } };

        TransformationAdmittedPlan plan = await AdmitAsync(world);
        Assert.Equal(
            new[] { "code", "source_system", "source_record_id", "accepted_metadata", "batch_id" },
            plan.ExecutionGraph.Selects!.Select(s => s.Column).ToArray());

        var result = await world.Executor.ExecuteAsync(ContextFor(plan), CancellationToken.None);

        Assert.True(result.Value!.Succeeded, result.Value.Message);
        CanonicalSourceLineage lineage = Assert.Single(world.Writer.Requests).Rows[0].Lineage!;
        Assert.Equal(batch, lineage.BatchId);
        Assert.Equal(receipt, lineage.ReceiptId);
        Assert.Equal(dataset, lineage.DatasetGovernanceId);
        Assert.Equal(tenant, lineage.TenantId);
        Assert.Equal(new string('a', 64), lineage.ContentHash);
    }

    [Fact]
    public async Task A_lone_batch_column_is_an_author_column_and_never_read_as_lineage()
    {
        var world = new World();
        world.Definitions.ContentJson = Content();
        world.Reader.Lineage = new List<string> { "batch_id" };

        TransformationAdmittedPlan plan = await AdmitAsync(world);

        Assert.Empty(plan.LineageColumns ?? Array.Empty<string>());
        Assert.Equal(
            new[] { "code", "source_system", "source_record_id" },
            plan.ExecutionGraph.Selects!.Select(s => s.Column).ToArray());
    }

    [Fact]
    public void Unreadable_or_inconsistent_lineage_is_refused_rather_than_guessed()
    {
        Guid batch = Guid.NewGuid();
        string complete = "{\"tenantId\":\"" + Guid.NewGuid() + "\",\"datasetGovernanceId\":\"" + Guid.NewGuid()
            + "\",\"receiptId\":\"" + Guid.NewGuid() + "\",\"contentHash\":\"abc\",\"batchId\":\"" + batch + "\"}";

        Assert.Null(TransformationProjectionJobExecutor.ReadLineage(batch, complete, out CanonicalSourceLineage? ok));
        Assert.NotNull(ok);
        Assert.NotNull(TransformationProjectionJobExecutor.ReadLineage(Guid.NewGuid(), complete, out _));
        Assert.NotNull(TransformationProjectionJobExecutor.ReadLineage(null, complete, out _));
        Assert.NotNull(TransformationProjectionJobExecutor.ReadLineage(batch, "{\"tenantId\":\"x\"}", out _));
        Assert.NotNull(TransformationProjectionJobExecutor.ReadLineage(batch, "not json", out _));
    }
}
