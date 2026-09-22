using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Application.Definitions.Transformations;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution.Transformations;

/// <summary>One authored block of an admitted plan, in the order the run executes it.</summary>
public sealed record TransformationBlock(string BlockId, string Kind, int ExecutionOrdinal);

/// <summary>
/// THE FACTS ADMISSION PROVED, FROZEN FOR THE RUN.
///
/// ExecutionGraph is the authored graph with its projection narrowed to the bound
/// source fields plus the governed provenance columns, in a deterministic order, so the
/// executor reads every value by ordinal and never by a column name two relations could
/// share. Nothing about joins, filters or derived columns is changed.
/// </summary>
public sealed record TransformationAdmittedPlan(
    ResolvedJobTarget AdmittedTarget,
    string DefinitionHash,
    string OutputTarget,
    MapperGraph ExecutionGraph,
    string StagingSchema,
    string ProvenanceRelation,
    IReadOnlyList<ProjectionFieldBinding> Bindings,
    IReadOnlyList<TransformationBlock> Blocks,
    CanonicalProjectionMode Mode = CanonicalProjectionMode.Ordinary,
    IReadOnlyList<string>? LineageColumns = null) : JobExecutionPlan(AdmittedTarget);

/// <summary>
/// THE GOVERNED TRANSFORMATION EXECUTOR.
///
/// It executes the exact immutable Transformation version that pre-admission resolved,
/// through the one shared compiler, writing only the business fields the authored
/// projection declaration binds, keyed by the governed source provenance of the one
/// provenance-capable input relation.
///
/// IT IS NOT A WRAPPER AROUND THE LEGACY MAPPING PATH. That path executes a different
/// contract over staged payloads; reusing it would make this family look commissioned
/// while executing something other than the declared version.
///
/// THREE VOCABULARIES, KEPT APART. A declaration defect is refused with the frozen
/// PROJECTION_* codes this class does not redefine. Row and data validation belongs to
/// the quarantine producer. Everything this class can break is JOB_EXEC_*.
///
/// EXECUTION MODEL. The authored blocks compile into one governed statement. Blocks run
/// in the deterministic plan order: a dataset block records the measured row count of
/// its relation, a relational chain block records that it was compiled into the
/// statement and measures nothing it did not measure, and the final block executes the
/// statement and the canonical write, recording rows read and rows accepted. The
/// cancellation request is observed before every block and before the write.
///
/// PROVENANCE (T-104). The run reserves its projection generation before it reads its
/// source, passes the exact version and immutable hash admission proved, and carries the
/// accepted source lineage of every row when the provenance relation exposes it. Whether
/// the run may replace an existing canonical effect is read once, at admission, from the
/// job's own target parameters, and is frozen into the plan like everything else.
/// </summary>
public sealed class TransformationProjectionJobExecutor : IJobExecutor
{
    // The frozen T-262 declaration vocabulary, reused unrenamed.
    public const string ProjectionRequiredCode = "PROJECTION_DECLARATION_REQUIRED";
    public const string ProjectionTargetMismatchCode = "PROJECTION_TARGET_MISMATCH";
    public const string ProjectionFieldUnknownCode = "PROJECTION_FIELD_UNKNOWN";
    public const string ProjectionFieldSystemOwnedCode = "PROJECTION_TARGET_FIELD_SYSTEM_OWNED";

    public const string KindDataset = "dataset";
    public const string KindFilter = "filter";
    public const string KindDerived = "derived";
    public const string KindSelect = "select";

    private static readonly string[] RelationalKinds = { KindDataset, KindFilter, KindDerived, KindSelect };

    private static readonly JsonSerializerOptions GraphOptions = new(JsonSerializerDefaults.Web);

    private readonly ICanonicalEntityCatalog _catalog;
    private readonly ITransformationCanonicalWriter _writer;
    private readonly ICanonicalDefinitionWriter _definitions;
    private readonly ITransformationSourceReader _reader;
    private readonly ICanvasStagingSchema _stagingSchema;
    private readonly IJobRunBlockEvidenceStore _evidence;
    private readonly IJobRunCancellationProbe _cancellation;

    public TransformationProjectionJobExecutor(
        ICanonicalEntityCatalog catalog,
        ITransformationCanonicalWriter writer,
        ICanonicalDefinitionWriter definitions,
        ITransformationSourceReader reader,
        ICanvasStagingSchema stagingSchema,
        IJobRunBlockEvidenceStore evidence,
        IJobRunCancellationProbe cancellation)
    {
        _catalog = catalog;
        _writer = writer;
        _definitions = definitions;
        _reader = reader;
        _stagingSchema = stagingSchema;
        _evidence = evidence;
        _cancellation = cancellation;
    }

    public JobDefinitionType Executes => JobDefinitionType.CanonicalRefresh;

    // ------------------------------------------------------------------ admission --

    public async Task<ApplicationResult<JobExecutionPlan>> AdmitAsync(
        ResolvedJobTarget target,
        CancellationToken cancellationToken)
    {
        if (target is null
            || target.Kind != DefinitionKind.Transformation
            || target.DefinitionId == Guid.Empty
            || target.ResolvedVersion <= 0)
        {
            return Refuse(JobExecutionDiagnosticCodes.ExactVersionRequired,
                "Governed projection execution needs one exact immutable Transformation version, "
                + "and the resolution supplied does not name one.");
        }

        CanonicalProjectionMode mode;
        string? parameterProblem = CanonicalProjectionParameters.TryRead(target.ParametersJson, out mode);
        if (parameterProblem is not null)
        {
            return Refuse(JobExecutionDiagnosticCodes.ProjectionParametersInvalid, parameterProblem);
        }

        ApplicationResult<CanonicalDefinitionVersion> exact =
            await _definitions.ResolveExactAsync(target.DefinitionId, target.ResolvedVersion, cancellationToken);

        if (exact.IsFailure || exact.Value is null)
        {
            return Refuse(JobExecutionDiagnosticCodes.ExactVersionRequired,
                "Transformation " + target.DefinitionId + " version " + target.ResolvedVersion
                + " could not be read: " + (exact.Error?.Message ?? "no version was returned."));
        }

        CanonicalDefinitionVersion version = exact.Value;
        if (version.Kind != DefinitionKind.Transformation || version.VersionNumber != target.ResolvedVersion)
        {
            return Refuse(JobExecutionDiagnosticCodes.ExactVersionRequired,
                "The stored version is not the Transformation version admission resolved.");
        }

        CanvasDefinitionRepresentation content;
        try
        {
            content = CanvasDefinitionContent.Read(version.ContentJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Refuse(JobExecutionDiagnosticCodes.CompilationFailed,
                "The stored content of this version cannot be read: " + ex.Message);
        }

        CanvasProjectionDeclaration? declaration = null;
        string? parseError = CanvasProjectionDeclaration.TryParse(content.ProjectionJson, out declaration);
        if (parseError is not null)
        {
            return Refuse(ProjectionRequiredCode, parseError);
        }

        string outputTarget = content.OutputTarget ?? string.Empty;
        string? declarationCode = AdmitDeclaration(declaration, outputTarget);
        if (declarationCode is not null)
        {
            return Refuse(declarationCode,
                "The version's projection declaration cannot be executed as declared.");
        }

        if (!string.Equals(content.Representation, CanvasDefinitionContent.RepresentationGraph, StringComparison.Ordinal))
        {
            return Refuse(JobExecutionDiagnosticCodes.SourceIdentityUnavailable,
                "An authored-statement version exposes no governed source relation, so its output rows "
                + "carry no source identity this runtime could consume without inferring one.");
        }

        if (!_writer.IsCommissioned(outputTarget))
        {
            return Refuse(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned,
                "Canonical target " + outputTarget + " is a lawful authoring target, and this runtime "
                + "holds no proven write and idempotency path for it yet.");
        }

        var boundFields = declaration!.FieldBindings
            .Select(b => b.TargetField)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        string? unwritable = _writer.FirstUnwritableField(outputTarget, boundFields);
        if (unwritable is not null)
        {
            return Refuse(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned,
                "Field " + unwritable + " of " + outputTarget + " has no sanctioned write path in this runtime.");
        }

        MapperGraph? graph;
        try
        {
            graph = string.IsNullOrWhiteSpace(content.GraphJson)
                ? null
                : JsonSerializer.Deserialize<MapperGraph>(content.GraphJson, GraphOptions);
        }
        catch (JsonException ex)
        {
            return Refuse(JobExecutionDiagnosticCodes.CompilationFailed,
                "The stored graph cannot be read: " + ex.Message);
        }

        if (graph is null || graph.Tables is null || graph.Tables.Length == 0)
        {
            return Refuse(JobExecutionDiagnosticCodes.CompilationFailed,
                "The stored graph names no input relation.");
        }

        MapperGraph authored = graph with
        {
            Joins = graph.Joins ?? Array.Empty<JoinSpec>(),
            Filters = graph.Filters ?? Array.Empty<FilterSpec>(),
            Derived = graph.Derived ?? Array.Empty<DerivedSpec>(),
        };

        var bindings = declaration.FieldBindings
            .OrderBy(b => b.TargetField, StringComparer.Ordinal)
            .ToArray();

        string? bindingProblem = CheckBindings(authored, bindings);
        if (bindingProblem is not null)
        {
            return Refuse(JobExecutionDiagnosticCodes.CompilationFailed, bindingProblem);
        }

        string? blockCode;
        string? blockDetail;
        IReadOnlyList<TransformationBlock> blocks = PlanBlocks(content.BoardJson, authored, out blockCode, out blockDetail);
        if (blockCode is not null)
        {
            return Refuse(blockCode, blockDetail ?? "The authored blocks cannot be planned.");
        }

        await using var sourceScope = _reader is IDefinitionScopedTransformationSourceReader scoped
            ? await scoped.BindDefinitionAsync(target.DefinitionId, target.ResolvedVersion, cancellationToken)
            : null;

        string schema = _stagingSchema.Name;
        IReadOnlyList<string> capable =
            await _reader.ProvenanceCapableRelationsAsync(schema, authored.Tables, cancellationToken);

        if (capable.Count == 0)
        {
            return Refuse(JobExecutionDiagnosticCodes.SourceIdentityUnavailable,
                "No input relation of this version exposes the governed provenance columns "
                + TransformationProvenanceColumns.SourceSystem + " and "
                + TransformationProvenanceColumns.SourceRecordId + ".");
        }

        if (capable.Count > 1)
        {
            return Refuse(JobExecutionDiagnosticCodes.SourceIdentityAmbiguous,
                "More than one independent input relation exposes governed provenance ("
                + string.Join(", ", capable) + "), so one output identity cannot be resolved.");
        }

        string provenanceRelation = capable[0];

        // Accepted lineage is read only when the relation exposes the complete accepted
        // contract. A lone batch_id column on a source-shaped relation is an author
        // column, not lineage, and is never interpreted as one.
        IReadOnlyList<string> lineageFound =
            await _reader.LineageColumnsAsync(schema, provenanceRelation, cancellationToken);
        IReadOnlyList<string> lineage =
            TransformationLineageColumns.All.All(c => lineageFound.Contains(c, StringComparer.Ordinal))
                ? TransformationLineageColumns.All
                : Array.Empty<string>();

        MapperGraph execution = authored with
        {
            Selects = ExecutionSelects(bindings, provenanceRelation, lineage).ToArray(),
            Board = null,
            Projection = null,
        };

        var compiled = TransformationSafeSelect.BuildSafeSelect(execution, schema, TransformationReadPurpose.Execution);
        if (compiled.err is not null || compiled.sql is null)
        {
            return Refuse(JobExecutionDiagnosticCodes.CompilationFailed,
                "The governed compiler refused this version: " + (compiled.err ?? "no statement was produced."));
        }

        return ApplicationResult<JobExecutionPlan>.Success(new TransformationAdmittedPlan(
            target,
            version.DefinitionHash,
            outputTarget,
            execution,
            schema,
            provenanceRelation,
            bindings,
            blocks,
            mode,
            lineage));
    }

    /// <summary>
    /// ADMISSION OF THE AUTHORED DECLARATION, BEFORE ANYTHING IS WRITTEN.
    ///
    /// Separated so it can be falsified without a database, and so the refusal codes stay
    /// in the T-262 vocabulary that owns them. A binding onto a system-owned field is
    /// refused rather than filtered: silently dropping it would let an author believe
    /// they had mapped something they had not.
    /// </summary>
    public string? AdmitDeclaration(
        CanvasProjectionDeclaration? declaration,
        string outputTarget)
    {
        if (declaration is null || string.IsNullOrWhiteSpace(outputTarget))
        {
            return ProjectionRequiredCode;
        }

        if (!string.Equals(declaration.TargetEntity, outputTarget, StringComparison.Ordinal))
        {
            return ProjectionTargetMismatchCode;
        }

        IReadOnlyList<CanonicalProjectionField> fields =
            _catalog.ProjectionFieldsOf(declaration.TargetEntity);

        foreach (ProjectionFieldBinding binding in declaration.FieldBindings)
        {
            CanonicalProjectionField? field = null;
            foreach (CanonicalProjectionField candidate in fields)
            {
                if (string.Equals(candidate.Name, binding.TargetField, StringComparison.Ordinal))
                {
                    field = candidate;
                    break;
                }
            }

            if (field is null) { return ProjectionFieldUnknownCode; }
            if (field.IsSystemOwned) { return ProjectionFieldSystemOwnedCode; }
        }

        return null;
    }

    private static string? CheckBindings(MapperGraph graph, IReadOnlyList<ProjectionFieldBinding> bindings)
    {
        foreach (ProjectionFieldBinding b in bindings)
        {
            if (b.SourceKind == CanvasProjectionDeclaration.KindColumn)
            {
                if (string.IsNullOrWhiteSpace(b.SourceTable) || !graph.Tables.Contains(b.SourceTable, StringComparer.Ordinal))
                {
                    return "Binding for " + b.TargetField + " names a table that is not an input of this version.";
                }

                if (!TransformationSafeSelect.Ident(b.SourceField))
                {
                    return "Binding for " + b.TargetField + " names an illegal source column.";
                }
            }
            else if (b.SourceKind == CanvasProjectionDeclaration.KindDerived)
            {
                if (!graph.Derived!.Any(d => string.Equals(d.Alias, b.SourceField, StringComparison.Ordinal)))
                {
                    return "Binding for " + b.TargetField + " names a derived output this version does not author.";
                }
            }
            else
            {
                return "Binding for " + b.TargetField + " names a statement output, which a graph version cannot produce.";
            }
        }

        return null;
    }

    private static IEnumerable<SelectSpec> ExecutionSelects(
        IReadOnlyList<ProjectionFieldBinding> bindings,
        string provenanceRelation,
        IReadOnlyList<string> lineageColumns)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProjectionFieldBinding b in bindings)
        {
            if (b.SourceKind != CanvasProjectionDeclaration.KindColumn) { continue; }
            if (seen.Add(b.SourceTable + "\u001f" + b.SourceField))
            {
                yield return new SelectSpec(b.SourceTable!, b.SourceField);
            }
        }

        foreach (string column in new[] { TransformationProvenanceColumns.SourceSystem, TransformationProvenanceColumns.SourceRecordId })
        {
            if (seen.Add(provenanceRelation + "\u001f" + column))
            {
                yield return new SelectSpec(provenanceRelation, column);
            }
        }

        foreach (string column in lineageColumns)
        {
            if (seen.Add(provenanceRelation + "\u001f" + column))
            {
                yield return new SelectSpec(provenanceRelation, column);
            }
        }
    }

    /// <summary>
    /// The authored board is the block identity. A version without one cannot be reported
    /// block by block, and inventing ids would break the link between what a person
    /// authored and what the runtime reports back.
    /// </summary>
    private static IReadOnlyList<TransformationBlock> PlanBlocks(
        string? boardJson,
        MapperGraph graph,
        out string? code,
        out string? detail)
    {
        code = null;
        detail = null;

        if (string.IsNullOrWhiteSpace(boardJson))
        {
            code = JobExecutionDiagnosticCodes.BlocksUnavailable;
            detail = "This version carries no authored board, so there is no stable block identity to record evidence against.";
            return Array.Empty<TransformationBlock>();
        }

        JsonObject? board;
        try
        {
            board = JsonNode.Parse(boardJson) as JsonObject;
        }
        catch (JsonException ex)
        {
            code = JobExecutionDiagnosticCodes.BlocksUnavailable;
            detail = "The authored board cannot be read: " + ex.Message;
            return Array.Empty<TransformationBlock>();
        }

        if (board?["nodes"] is not JsonArray nodes || nodes.Count == 0)
        {
            code = JobExecutionDiagnosticCodes.BlocksUnavailable;
            detail = "The authored board declares no blocks.";
            return Array.Empty<TransformationBlock>();
        }

        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonNode? node in nodes)
        {
            string? id = Text(node?["id"]);
            string? kind = Text(node?["kind"]);
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, id.Trim(), StringComparison.Ordinal) || kind is null)
            {
                code = JobExecutionDiagnosticCodes.BlocksUnavailable;
                detail = "An authored block has no stable id or no kind.";
                return Array.Empty<TransformationBlock>();
            }

            if (kinds.ContainsKey(id))
            {
                code = JobExecutionDiagnosticCodes.BlocksUnavailable;
                detail = "Block id " + id + " is authored more than once.";
                return Array.Empty<TransformationBlock>();
            }

            if (!RelationalKinds.Contains(kind, StringComparer.Ordinal))
            {
                code = JobExecutionDiagnosticCodes.CompilationFailed;
                detail = "Block " + id + " of kind " + kind + " has no transformation execution in this runtime.";
                return Array.Empty<TransformationBlock>();
            }

            kinds[id] = kind;
        }

        var datasetIds = kinds.Where(k => k.Value == KindDataset).Select(k => k.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var tables = graph.Tables.Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToArray();
        if (!datasetIds.SequenceEqual(tables, StringComparer.Ordinal))
        {
            code = JobExecutionDiagnosticCodes.CompilationFailed;
            detail = "The authored dataset blocks and the compiled input relations disagree, so the board is not the graph this version executes.";
            return Array.Empty<TransformationBlock>();
        }

        var dependsOn = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (board["edges"] is JsonArray edges)
        {
            foreach (JsonNode? edge in edges)
            {
                string? source = Text(edge?["source"]);
                string? target = Text(edge?["target"]);
                if (source is null || target is null || !kinds.ContainsKey(source) || !kinds.ContainsKey(target))
                {
                    code = JobExecutionDiagnosticCodes.BlocksUnavailable;
                    detail = "An authored wire references a block the board does not declare.";
                    return Array.Empty<TransformationBlock>();
                }

                List<string>? needs;
                if (!dependsOn.TryGetValue(target, out needs))
                {
                    needs = new List<string>();
                    dependsOn[target] = needs;
                }

                if (!needs.Contains(source, StringComparer.Ordinal)) { needs.Add(source); }
            }
        }

        var readOnlyDeps = dependsOn.ToDictionary(
            p => p.Key,
            p => (IReadOnlyList<string>)p.Value,
            StringComparer.Ordinal);

        string? refusal;
        IReadOnlyList<string> order = TransformationExecutionPlan.Order(kinds.Keys.ToList(), readOnlyDeps, out refusal);
        if (refusal is not null)
        {
            code = JobExecutionDiagnosticCodes.CompilationFailed;
            detail = refusal;
            return Array.Empty<TransformationBlock>();
        }

        var blocks = new List<TransformationBlock>(order.Count);
        for (int i = 0; i < order.Count; i++)
        {
            blocks.Add(new TransformationBlock(order[i], kinds[order[i]], i));
        }

        return blocks;
    }

    private static string? Text(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out string? s))
        {
            return s;
        }

        return null;
    }

    // ------------------------------------------------------------------ execution --

    public async Task<ApplicationResult<JobExecutionOutcome>> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.CarriesExactVersion || context.Plan is not TransformationAdmittedPlan plan)
        {
            return ApplicationResult<JobExecutionOutcome>.Failure(new ApplicationError(
                JobExecutionDiagnosticCodes.ExactVersionRequired,
                "The execution context does not carry an admitted plan for an exact Transformation version.",
                ApplicationErrorType.BusinessRule));
        }

        await using var sourceScope = _reader is IDefinitionScopedTransformationSourceReader scoped
            ? await scoped.BindDefinitionAsync(context.TargetDefinitionId, context.ResolvedVersion, cancellationToken)
            : null;

        Guid runId = context.JobRunHistoryId;
        var evidence = plan.Blocks
            .Select(b => new JobRunBlockEvidence(runId, b.BlockId, b.ExecutionOrdinal))
            .ToList();

        await _evidence.AddAsync(evidence, cancellationToken);
        await _evidence.SaveAsync(cancellationToken);

        var results = new List<JobExecutionBlockResult>();
        int last = evidence.Count - 1;
        int current = -1;

        try
        {
            for (int i = 0; i <= last; i++)
            {
                current = i;
                TransformationBlock block = plan.Blocks[i];
                JobRunBlockEvidence ev = evidence[i];

                if (await _cancellation.IsRequestedAsync(runId, cancellationToken))
                {
                    return await StopForCancellationAsync(plan, evidence, results, i, cancellationToken);
                }

                ev.MarkRunning();
                await _evidence.SaveAsync(cancellationToken);

                if (i < last)
                {
                    if (block.Kind == KindDataset)
                    {
                        long count = await _reader.CountRowsAsync(plan.StagingSchema, block.BlockId, cancellationToken);
                        ev.MarkSucceeded(null, (int)Math.Min(count, int.MaxValue));
                    }
                    else
                    {
                        ev.MarkSucceeded(null, null);
                    }

                    await _evidence.SaveAsync(cancellationToken);
                    results.Add(Result(ev));
                    continue;
                }

                TerminalOutcome terminal = await ExecuteTerminalAsync(plan, context, cancellationToken);

                if (terminal.Cancelled)
                {
                    return await StopForCancellationAsync(plan, evidence, results, i, cancellationToken);
                }

                if (terminal.Code is not null)
                {
                    ev.MarkFailed(terminal.Code, terminal.Detail);
                    await _evidence.SaveAsync(cancellationToken);
                    results.Add(Result(ev));
                    return ApplicationResult<JobExecutionOutcome>.Success(new JobExecutionOutcome(
                        false, false,
                        "Block " + block.BlockId + " failed: " + terminal.Code,
                        0, results, terminal.Code, terminal.Detail));
                }

                ev.MarkSucceeded(terminal.RowsRead, terminal.Written!.RowsAccepted);
                await _evidence.SaveAsync(cancellationToken);
                results.Add(Result(ev));

                string message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Transformation {0} version {1} executed into {2}: {3} rows read, {4} inserted, {5} unchanged, "
                    + "{6} superseded, {7} reattributed (projection {8}, generation {9}).",
                    plan.Target.DefinitionId, plan.Target.ResolvedVersion, plan.OutputTarget,
                    terminal.RowsRead, terminal.Written.RowsInserted, terminal.Written.RowsUnchanged,
                    terminal.Written.RowsSuperseded, terminal.Written.RowsReattributed,
                    plan.Mode, terminal.Generation);

                return ApplicationResult<JobExecutionOutcome>.Success(new JobExecutionOutcome(
                    true, false, message, terminal.Written.RowsEffected, results, null, null));
            }

            return ApplicationResult<JobExecutionOutcome>.Failure(new ApplicationError(
                JobExecutionDiagnosticCodes.BlocksUnavailable,
                "The admitted plan contains no block to execute.",
                ApplicationErrorType.BusinessRule));
        }
        catch (Exception ex)
        {
            // A BLOCK WHOSE OUTCOME WAS NOT PERSISTED DID NOT SUCCEED.
            //
            // The failure may be the persistence of the success itself, in which case the
            // entity already says Succeeded in memory while the database still says
            // Running. That in-memory value is not evidence. The row is reloaded from the
            // database first, and the honest failure is recorded against what was really
            // stored. Host shutdown is not an operator cancellation and is recorded the
            // same way; the cooperative cancellation path returned long before here.
            if (current >= 0 && current <= last)
            {
                await RecordUnexpectedBlockFailureAsync(evidence[current], ex);
            }

            throw;
        }
    }

    private async Task RecordUnexpectedBlockFailureAsync(JobRunBlockEvidence evidence, Exception ex)
    {
        try
        {
            await _evidence.DiscardAsync(evidence, CancellationToken.None);
        }
        catch (Exception)
        {
            // The store could not restore the persisted row. The original failure is what
            // matters and is rethrown by the caller.
        }

        if (IsTerminal(evidence.Status)) { return; }

        evidence.MarkFailed(JobExecutionDiagnosticCodes.BlockFailed, ex.Message);

        try
        {
            await _evidence.SaveAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Best effort. A store that cannot record the failure cannot be made to.
        }
    }

    private sealed record TerminalOutcome(
        bool Cancelled,
        string? Code,
        string? Detail,
        int RowsRead,
        CanonicalWriteResult? Written,
        long Generation = 0);

    private async Task<TerminalOutcome> ExecuteTerminalAsync(
        TransformationAdmittedPlan plan,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        MapperGraph graph = plan.ExecutionGraph;
        var compiled = TransformationSafeSelect.BuildSafeSelect(graph, plan.StagingSchema, TransformationReadPurpose.Execution);
        if (compiled.err is not null || compiled.sql is null)
        {
            return new TerminalOutcome(false, JobExecutionDiagnosticCodes.CompilationFailed,
                compiled.err ?? "No statement was produced.", 0, null);
        }

        SelectSpec[] selects = graph.Selects ?? Array.Empty<SelectSpec>();
        DerivedSpec[] derived = graph.Derived ?? Array.Empty<DerivedSpec>();
        int columnCount = selects.Length + derived.Length;

        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < selects.Length; i++)
        {
            ordinals[selects[i].Table + "\u001f" + selects[i].Column] = i;
        }

        int systemOrdinal = ordinals[plan.ProvenanceRelation + "\u001f" + TransformationProvenanceColumns.SourceSystem];
        int recordOrdinal = ordinals[plan.ProvenanceRelation + "\u001f" + TransformationProvenanceColumns.SourceRecordId];

        IReadOnlyList<string> lineageColumns = plan.LineageColumns ?? Array.Empty<string>();
        int batchOrdinal = -1;
        int metadataOrdinal = -1;
        if (lineageColumns.Count > 0)
        {
            batchOrdinal = ordinals[plan.ProvenanceRelation + "\u001f" + TransformationLineageColumns.BatchId];
            metadataOrdinal = ordinals[plan.ProvenanceRelation + "\u001f" + TransformationLineageColumns.AcceptedMetadata];
        }

        // THE GENERATION IS RESERVED BEFORE THE SOURCE IS READ. A run that read its source
        // earlier therefore always holds the lower generation, whatever order the writes
        // arrive in, and the writer can refuse a delayed run without consulting a clock.
        long generation = await _writer.ReserveProjectionGenerationAsync(cancellationToken);

        IReadOnlyList<object?[]> rows;
        try
        {
            rows = await _reader.ReadAsync(
                compiled.sql,
                (IReadOnlyList<object>?)compiled.prms ?? Array.Empty<object>(),
                columnCount,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new TerminalOutcome(false, JobExecutionDiagnosticCodes.QueryFailed, ex.Message, 0, null);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var writeRows = new List<CanonicalWriteRow>(rows.Count);

        for (int r = 0; r < rows.Count; r++)
        {
            object?[] row = rows[r];
            string? system = AsIdentity(row[systemOrdinal]);
            string? record = AsIdentity(row[recordOrdinal]);

            if (system is null || record is null)
            {
                return new TerminalOutcome(false, JobExecutionDiagnosticCodes.SourceIdentityInvalid,
                    "Output row " + (r + 1) + " carries no usable governed source identity. Nothing was written.",
                    rows.Count, null);
            }

            if (!seen.Add(system + "\u001f" + record))
            {
                return new TerminalOutcome(false, JobExecutionDiagnosticCodes.SourceIdentityAmbiguous,
                    "Source identity " + system + " / " + record + " produces more than one output row. Nothing was written.",
                    rows.Count, null);
            }

            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (ProjectionFieldBinding b in plan.Bindings)
            {
                int ordinal;
                if (b.SourceKind == CanvasProjectionDeclaration.KindColumn)
                {
                    ordinal = ordinals[b.SourceTable + "\u001f" + b.SourceField];
                }
                else
                {
                    ordinal = selects.Length + Array.FindIndex(derived, d => string.Equals(d.Alias, b.SourceField, StringComparison.Ordinal));
                }

                object? value = row[ordinal];
                fields[b.TargetField] = value is DBNull ? null : value;
            }

            CanonicalSourceLineage? lineage = null;
            if (lineageColumns.Count > 0)
            {
                string? lineageProblem = ReadLineage(row[batchOrdinal], row[metadataOrdinal], out lineage);
                if (lineageProblem is not null)
                {
                    return new TerminalOutcome(false, JobExecutionDiagnosticCodes.SourceIdentityInvalid,
                        "Output row " + (r + 1) + " (" + system + " / " + record + ") carries unreadable accepted lineage: "
                        + lineageProblem + " Nothing was written.",
                        rows.Count, null, generation);
                }
            }

            writeRows.Add(new CanonicalWriteRow(system, record, fields, lineage));
        }

        if (await _cancellation.IsRequestedAsync(context.JobRunHistoryId, cancellationToken))
        {
            return new TerminalOutcome(true, null, null, rows.Count, null, generation);
        }

        ApplicationResult<CanonicalWriteResult> written = await _writer.WriteAsync(
            new CanonicalWriteRequest(
                plan.OutputTarget,
                plan.Target.DefinitionId,
                plan.Target.ResolvedVersion,
                plan.DefinitionHash,
                context.JobRunHistoryId,
                plan.Mode,
                generation,
                writeRows),
            cancellationToken);

        if (written.IsFailure || written.Value is null)
        {
            return new TerminalOutcome(false,
                written.Error?.Code ?? JobExecutionDiagnosticCodes.CanonicalWriteFailed,
                written.Error?.Message ?? "The canonical write returned no result.",
                rows.Count, null, generation);
        }

        return new TerminalOutcome(false, null, null, rows.Count, written.Value, generation);
    }

    /// <summary>
    /// Reads the accepted lineage of one row: the sealed batch identity and the accepted
    /// record's receipt, content hash, dataset governance and tenant. Every value comes from
    /// the accepted-record authority's own metadata; none is inferred.
    /// </summary>
    public static string? ReadLineage(object? batchValue, object? metadataValue, out CanonicalSourceLineage? lineage)
    {
        lineage = null;

        Guid? batch = batchValue switch
        {
            null => null,
            DBNull => null,
            Guid g => g,
            string text when Guid.TryParse(text, out Guid batchParsed) => batchParsed,
            _ => null,
        };

        if (!batch.HasValue)
        {
            return "the accepted batch identity is absent.";
        }

        string? json = metadataValue switch
        {
            null => null,
            DBNull => null,
            string text => text,
            JsonDocument document => document.RootElement.GetRawText(),
            JsonElement element => element.GetRawText(),
            _ => Convert.ToString(metadataValue, CultureInfo.InvariantCulture),
        };

        if (string.IsNullOrWhiteSpace(json))
        {
            return "the accepted metadata is absent.";
        }

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(json);
            JsonElement root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return "the accepted metadata is not an object.";
            }

            Guid? receipt = GuidMember(root, "receiptId");
            Guid? dataset = GuidMember(root, "datasetGovernanceId");
            Guid? tenant = GuidMember(root, "tenantId");
            Guid? metadataBatch = GuidMember(root, "batchId");
            string? contentHash = root.TryGetProperty("contentHash", out JsonElement hash) && hash.ValueKind == JsonValueKind.String
                ? hash.GetString()
                : null;

            if (!receipt.HasValue || !dataset.HasValue || !tenant.HasValue || string.IsNullOrWhiteSpace(contentHash))
            {
                return "the accepted metadata does not name its receipt, content hash, dataset and tenant.";
            }

            if (metadataBatch.HasValue && metadataBatch.Value != batch.Value)
            {
                return "the accepted metadata names a different batch from the relation.";
            }

            lineage = new CanonicalSourceLineage(batch, receipt, contentHash, dataset, tenant);
            return null;
        }
        catch (JsonException ex)
        {
            return "the accepted metadata cannot be read: " + ex.Message;
        }
    }

    private static Guid? GuidMember(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && Guid.TryParse(value.GetString(), out Guid parsed))
        {
            return parsed;
        }

        return null;
    }

    private async Task<ApplicationResult<JobExecutionOutcome>> StopForCancellationAsync(
        TransformationAdmittedPlan plan,
        List<JobRunBlockEvidence> evidence,
        List<JobExecutionBlockResult> results,
        int from,
        CancellationToken cancellationToken)
    {
        for (int k = from; k < evidence.Count; k++)
        {
            if (!IsTerminal(evidence[k].Status))
            {
                evidence[k].MarkCancelled();
            }

            results.Add(Result(evidence[k]));
        }

        await _evidence.SaveAsync(cancellationToken);

        return ApplicationResult<JobExecutionOutcome>.Success(new JobExecutionOutcome(
            false, true,
            "Cancellation observed before block " + plan.Blocks[from].BlockId + "; execution stopped there.",
            0, results, JobExecutionDiagnosticCodes.Cancelled, null));
    }

    private static bool IsTerminal(JobRunBlockStatus status) =>
        status == JobRunBlockStatus.Succeeded
        || status == JobRunBlockStatus.Failed
        || status == JobRunBlockStatus.Blocked
        || status == JobRunBlockStatus.Cancelled;

    private static JobExecutionBlockResult Result(JobRunBlockEvidence ev) =>
        new(ev.BlockId, ev.ExecutionOrdinal, ev.Status, ev.InputRows, ev.OutputRows, ev.DiagnosticCode, ev.DiagnosticDetail);

    private static string? AsIdentity(object? value)
    {
        if (value is null || value is DBNull) { return null; }

        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        text = text.Trim();
        return text.Length == 0 ? null : text;
    }

    private static ApplicationResult<JobExecutionPlan> Refuse(string code, string detail)
    {
        return ApplicationResult<JobExecutionPlan>.Failure(
            new ApplicationError(code, detail, ApplicationErrorType.BusinessRule));
    }
}
