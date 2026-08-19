using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.ErrorHandling;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>
/// M1-05 Surface-3: analysis-job DEFINITIONS over LIVE canonical data.
/// A definition is tenant data in public.inspection_jobs (generic, no dataset-
/// specific identifiers). Running a definition:
///   1. evaluates the ReadinessGate via ppiq_ml_run_learning_job_governed_v1
///      (readiness_status / readiness_reason surfaced honestly, never hidden);
///   2. executes the EXISTING deterministic correlation engine
///      (ICorrelationComputeEngine -> ml_correlation_compute_runs +
///       ml_correlation_results_v2);
///   3. ties results to the definition by stamping
///      inspection_jobs.source_correlation_run_id = compute_run_id
///      plus last_run_at_utc / last_run_status / last_result_json.
/// Population filters (e.g. grade=S355J2) are DECLARED SCOPE persisted in
/// rule_json; engine-level population filtering is the logged M2 remainder
/// (generic-projector keystone). This limitation is stated in every response.
/// </summary>
public static class AnalysisJobDefinitionEndpoints
{
    private const string HonestPositioning =
        "Suspected contributors, not guaranteed root cause. Association evidence for engineering review only.";

    private const string PopulationFilterNote =
        "Population filters are stored as declared scope in the definition (rule_json). " +
        "Engine-level population filtering ships with the M2 generic canonical projector.";

    public static IEndpointRouteBuilder MapAnalysisJobDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analysis-jobs")
            .RequireLicenseFeature(LicenseFeature.InvestigationWorkflow)
            .WithTags("Analytics - Surface 3 Analysis Job Definitions")
            .RequireAuthorization("PlantProcessViewer");

        group.MapGet("/definition-options", GetDefinitionOptionsAsync)
            .WithName("GetAnalysisJobDefinitionOptions")
            .WithSummary("Live selectable outcome/parameter/engine options from the tenant database");

        group.MapGet("/", ListDefinitionsAsync)
            .WithName("ListAnalysisJobDefinitions");

        group.MapGet("/{code}", GetDefinitionAsync)
            .WithName("GetAnalysisJobDefinition");

        group.MapPost("/", CreateDefinitionAsync)
            .WithName("CreateAnalysisJobDefinition");

        group.MapPut("/{code}", UpdateDefinitionAsync)
            .WithName("UpdateAnalysisJobDefinition");

        group.MapPost("/{code}/run", RunDefinitionAsync)
            .WithName("RunAnalysisJobDefinition")
            .WithSummary("ReadinessGate-governed learning run + deterministic compute tied to the definition");

        group.MapGet("/{code}/results", GetDefinitionResultsAsync)
            .WithName("GetAnalysisJobDefinitionResults");

        return app;
    }

    // ------------------------------------------------------------------
    // GET /definition-options - everything the config UI needs, LIVE
    // ------------------------------------------------------------------
    private static async Task<IResult> GetDefinitionOptionsAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var defectRows = await QueryRowsAsync(
            dbContext,
            """
            SELECT event_type, count(*)::bigint AS event_count
            FROM public.quality_events
            WHERE is_deleted = false
            GROUP BY event_type
            ORDER BY count(*) DESC
            LIMIT 200
            """,
            cancellationToken);

        var parameterRows = await QueryRowsAsync(
            dbContext,
            """
            SELECT
                pd.parameter_code,
                pd.parameter_name,
                count(po.id)::bigint AS observation_count
            FROM public.parameter_definitions pd
            LEFT JOIN public.parameter_observations po
                ON po.parameter_definition_id = pd.id
               AND po.is_deleted = false
            WHERE pd.is_deleted = false
            GROUP BY pd.parameter_code, pd.parameter_name
            ORDER BY count(po.id) DESC
            LIMIT 200
            """,
            cancellationToken);

        var outcomeRows = await QueryRowsAsync(
            dbContext,
            """
            SELECT outcome_key, display_name, outcome_type, grain
            FROM public.ml_outcome_definitions
            WHERE is_deleted = false
            ORDER BY outcome_group, outcome_key
            """,
            cancellationToken);

        var engineJobRows = await QueryRowsAsync(
            dbContext,
            """
            SELECT job_code, job_name, outcome_family, is_enabled
            FROM public.ml_learning_job_catalog_v1
            ORDER BY job_code
            """,
            cancellationToken);

        var windowRows = await QueryRowsAsync(
            dbContext,
            """
            SELECT
                min(observed_at_utc) AS min_observed_at_utc,
                max(observed_at_utc) AS max_observed_at_utc,
                count(*)::bigint     AS observation_count
            FROM public.parameter_observations
            WHERE is_deleted = false
            """,
            cancellationToken);

        var window = windowRows.FirstOrDefault();

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            defectTypes = defectRows.Select(r => new
            {
                eventType = (string?)r["event_type"] ?? "",
                eventCount = ToLong(r["event_count"])
            }),
            parameters = parameterRows.Select(r => new
            {
                parameterCode = (string?)r["parameter_code"] ?? "",
                parameterName = (string?)r["parameter_name"] ?? "",
                observationCount = ToLong(r["observation_count"])
            }),
            engineOutcomes = outcomeRows.Select(r => new
            {
                outcomeKey = (string?)r["outcome_key"] ?? "",
                displayName = (string?)r["display_name"] ?? "",
                outcomeType = (string?)r["outcome_type"] ?? "",
                grain = (string?)r["grain"] ?? ""
            }),
            engineJobs = engineJobRows.Select(r => new
            {
                jobCode = (string?)r["job_code"] ?? "",
                jobName = (string?)r["job_name"] ?? "",
                outcomeFamily = (string?)r["outcome_family"] ?? "",
                isEnabled = r["is_enabled"] is bool b && b
            }),
            dataWindow = new
            {
                minObservedAtUtc = window is null ? null : window["min_observed_at_utc"],
                maxObservedAtUtc = window is null ? null : window["max_observed_at_utc"],
                observationCount = window is null ? 0L : ToLong(window["observation_count"])
            },
            populationFilterNote = PopulationFilterNote
        });
    }

    // ------------------------------------------------------------------
    // GET / - list definitions
    // ------------------------------------------------------------------
    private static async Task<IResult> ListDefinitionsAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<AnalysisJobDefinitionRow>();

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = DefinitionSelectSql +
            " WHERE is_deleted = false ORDER BY created_at_utc DESC LIMIT 200";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadDefinition(reader));

        return Results.Ok(new AnalysisJobListResponse(DateTime.UtcNow, rows));
    }

    // ------------------------------------------------------------------
    // GET /{code}
    // ------------------------------------------------------------------
    private static async Task<IResult> GetDefinitionAsync(
        string code,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var row = await LoadDefinitionAsync(dbContext, code, cancellationToken);
        if (row is null)
            return ApplicationProblems.NotFound($"Analysis job definition '{code}' was not found.");

        return Results.Ok(row);
    }

    // ------------------------------------------------------------------
    // POST / - create definition
    // ------------------------------------------------------------------
    private static async Task<IResult> CreateDefinitionAsync(
        [FromBody] CreateAnalysisJobDefinitionRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return ApplicationProblems.Validation("Code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApplicationProblems.Validation("Name is required.");
        if (string.IsNullOrWhiteSpace(request.DefectType))
            return ApplicationProblems.Validation("DefectType is required (pick a live quality event type).");

        var code = NormalizeCode(request.Code);
        var windowDays = ClampWindow(request.WindowDays);

        var exists = await ScalarBoolAsync(
            dbContext,
            "SELECT EXISTS (SELECT 1 FROM public.inspection_jobs WHERE lower(inspection_job_code) = lower(@code) AND is_deleted = false)",
            cancellationToken,
            ("code", code));

        if (exists)
            return ApplicationProblems.Conflict($"An active definition with code '{code}' already exists.");

        // T-065. The target is validated before anything is written, so the three
        // CHECK constraints in script 828 are never the first thing to notice an
        // incoherent target. Absent is a valid answer and is not an empty target.
        var targetRefusal = TargetFromApi(
            request.TargetDefinitionKind,
            request.TargetDefinitionId,
            request.TargetDefinitionVersion,
            request.TargetVersionPolicy,
            request.TargetParameters,
            out var target);

        if (targetRefusal is not null)
            return ApplicationProblems.Validation(targetRefusal);

        var id = Guid.NewGuid();
        var ruleJson = BuildRuleJson(windowDays, request.PopulationFilters, request.EngineOutcomeKey, request.EngineJobCode, request.Grain);

        await ExecuteAsync(
            dbContext,
            """
            INSERT INTO public.inspection_jobs
            (
                id, inspection_job_code, inspection_job_name, inspection_type,
                source_correlation_run_id, parameter_code, defect_type,
                site_id, equipment_id, rule_json, schedule_expression,
                is_enabled, honest_state, description, is_synthetic,
                source_system, source_record_id, created_at_utc,
                target_definition_kind, target_definition_id,
                target_definition_version, target_version_policy, target_parameters
            )
            VALUES
            (
                @id, @code, @name, 'AnalysisJobDefinition',
                NULL, @parameter_code, @defect_type,
                NULL, NULL, CAST(@rule_json AS jsonb), @schedule,
                @is_enabled, 'RuleBasedMonitoring', @description, false,
                'PlantProcessIQ.Surface3.AnalysisJobDefinition', NULL, now(),
                @target_kind, @target_id,
                @target_version, @target_policy, CAST(@target_parameters AS jsonb)
            )
            """,
            cancellationToken,
            ("id", id),
            ("code", code),
            ("name", request.Name.Trim()),
            ("parameter_code", NullableText(request.ParameterCode)),
            ("defect_type", request.DefectType.Trim()),
            ("rule_json", ruleJson),
            ("schedule", string.IsNullOrWhiteSpace(request.ScheduleExpression) ? "Manual" : request.ScheduleExpression.Trim()),
            ("is_enabled", request.IsEnabled ?? true),
            ("description", NullableText(request.Description)),
            ("target_kind", TargetKindColumn(target)),
            ("target_id", TargetIdColumn(target)),
            ("target_version", TargetVersionColumn(target)),
            ("target_policy", TargetPolicyColumn(target)),
            ("target_parameters", TargetParametersColumn(target)));

        var created = await LoadDefinitionAsync(dbContext, code, cancellationToken);
        return Results.Ok(created);
    }

    // ------------------------------------------------------------------
    // PUT /{code} - edit definition (enables rerun-after-edit recompute)
    // ------------------------------------------------------------------
    private static async Task<IResult> UpdateDefinitionAsync(
        string code,
        [FromBody] UpdateAnalysisJobDefinitionRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApplicationProblems.Validation("Name is required.");
        if (string.IsNullOrWhiteSpace(request.DefectType))
            return ApplicationProblems.Validation("DefectType is required.");

        var windowDays = ClampWindow(request.WindowDays);
        var ruleJson = BuildRuleJson(windowDays, request.PopulationFilters, request.EngineOutcomeKey, request.EngineJobCode, request.Grain);

        // T-065. The target is replaced wholesale by what this request states,
        // including being cleared when it states none. A partial update would
        // leave a kind beside an identity from an earlier edit.
        var targetRefusal = TargetFromApi(
            request.TargetDefinitionKind,
            request.TargetDefinitionId,
            request.TargetDefinitionVersion,
            request.TargetVersionPolicy,
            request.TargetParameters,
            out var target);

        if (targetRefusal is not null)
            return ApplicationProblems.Validation(targetRefusal);

        var affected = await ExecuteAsync(
            dbContext,
            """
            UPDATE public.inspection_jobs
            SET inspection_job_name = @name,
                defect_type = @defect_type,
                parameter_code = @parameter_code,
                rule_json = CAST(@rule_json AS jsonb),
                schedule_expression = @schedule,
                is_enabled = @is_enabled,
                description = @description,
                target_definition_kind = @target_kind,
                target_definition_id = @target_id,
                target_definition_version = @target_version,
                target_version_policy = @target_policy,
                target_parameters = CAST(@target_parameters AS jsonb),
                updated_at_utc = now()
            WHERE lower(inspection_job_code) = lower(@code)
              AND is_deleted = false
            """,
            cancellationToken,
            ("name", request.Name.Trim()),
            ("defect_type", request.DefectType.Trim()),
            ("parameter_code", NullableText(request.ParameterCode)),
            ("rule_json", ruleJson),
            ("schedule", string.IsNullOrWhiteSpace(request.ScheduleExpression) ? "Manual" : request.ScheduleExpression.Trim()),
            ("is_enabled", request.IsEnabled ?? true),
            ("description", NullableText(request.Description)),
            ("target_kind", TargetKindColumn(target)),
            ("target_id", TargetIdColumn(target)),
            ("target_version", TargetVersionColumn(target)),
            ("target_policy", TargetPolicyColumn(target)),
            ("target_parameters", TargetParametersColumn(target)),
            ("code", code.Trim()));

        if (affected == 0)
            return ApplicationProblems.NotFound($"Analysis job definition '{code}' was not found.");

        var updated = await LoadDefinitionAsync(dbContext, code, cancellationToken);
        return Results.Ok(updated);
    }

    // ------------------------------------------------------------------
    // POST /{code}/run - the engine linkage
    // ------------------------------------------------------------------
    private static async Task<IResult> RunDefinitionAsync(
        string code,
        [FromBody] RunAnalysisJobRequest request,
        PlantProcessDbContext dbContext,
        [FromServices] ICorrelationComputeEngine computeEngine,
        [FromServices] IJobTargetResolver targetResolver,
        CancellationToken cancellationToken)
    {
        var definition = await LoadDefinitionAsync(dbContext, code, cancellationToken);
        if (definition is null)
            return ApplicationProblems.NotFound($"Analysis job definition '{code}' was not found.");

        // T-065. The run declaration is READ, never manufactured.
        //
        // Three literals used to be assigned here as defaults and only then
        // overwritten if the stored declaration happened to carry them. A
        // definition that named no engine job code therefore ran as
        // ML_PROCESS_VS_DEFECT, acquired that code's class from the catalogue, and
        // executed against a target nobody declared. A declaration that could not
        // be read did the same thing silently, because the parse failure was
        // swallowed by a catch that said corruption never blocks a run.
        //
        // A missing declaration is a blocked run, not a defaulted one. This is
        // decided BEFORE the class lookup, the resolver and either engine.
        var windowDays = ClampWindow(request.WindowDaysOverride);

        string? declaredOutcomeKey;
        string? declaredJobCode;
        string? declaredGrain;

        try
        {
            using var doc = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(definition.RuleJson) ? "{}" : definition.RuleJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return BlockedRunResult(
                    definition, windowDays, null, null, "BlockedDefinition",
                    "RunDeclarationMalformed",
                    "The stored run declaration is not a JSON object, so nothing about this "
                    + "run can be read from it.");
            }

            if (request.WindowDaysOverride is null
                && root.TryGetProperty("windowDays", out var w)
                && w.ValueKind == JsonValueKind.Number)
                windowDays = ClampWindow(w.GetInt32());

            declaredOutcomeKey = ReadDeclaredString(root, "engineOutcomeKey");
            declaredJobCode = ReadDeclaredString(root, "engineJobCode");
            declaredGrain = ReadDeclaredString(root, "grain");
        }
        catch (JsonException ex)
        {
            return BlockedRunResult(
                definition, windowDays, null, null, "BlockedDefinition",
                "RunDeclarationMalformed",
                "The stored run declaration could not be read as JSON: " + ex.Message);
        }

        var missingKeys = new List<string>();
        if (declaredJobCode is null) { missingKeys.Add("engineJobCode"); }
        if (declaredOutcomeKey is null) { missingKeys.Add("engineOutcomeKey"); }
        if (declaredGrain is null) { missingKeys.Add("grain"); }

        if (missingKeys.Count > 0)
        {
            return BlockedRunResult(
                definition, windowDays, declaredJobCode, declaredOutcomeKey, "BlockedDefinition",
                "RunDeclarationIncomplete",
                "The stored run declaration does not state " + string.Join(", ", missingKeys)
                + ". A run is not started on values this definition never declared.");
        }

        var engineOutcomeKey = declaredOutcomeKey!;
        var engineJobCode = declaredJobCode!;
        var grain = declaredGrain!;

        // --- T-065: what this definition executes, resolved BEFORE any engine side effect ---
        //
        // The class is not the analysis definition's own. It is the committed
        // catalogue's job_type for the engine job code this definition names, so
        // the mapping is a reuse of a ratified table rather than a second
        // authority. An unmappable class is refused, never defaulted to Custom:
        // a defaulted class would inherit whatever target policy that class later
        // carries, and nobody would go looking for it.
        var catalogJobType = await ScalarStringAsync(
            dbContext,
            "SELECT job_type FROM public.ml_learning_job_catalog_v1 WHERE job_code = @jobCode LIMIT 1",
            cancellationToken,
            ("jobCode", engineJobCode));

        var jobClass = AnalysisJobClass.FromCatalogJobType(catalogJobType);
        if (jobClass is null)
        {
            return BlockedTargetResult(
                definition, windowDays, engineJobCode, engineOutcomeKey,
                "TargetClassUnmappable",
                AnalysisJobClass.UnmappableMessage(engineJobCode, catalogJobType));
        }

        // The stored target is decoded through the one policy codec. A stored
        // value outside the closed vocabulary is refused rather than read as
        // current-published, which is what the EF converter used to do.
        var storedRefusal = TargetFromStorage(definition, out var declaredTarget);
        if (storedRefusal is not null)
        {
            return BlockedTargetResult(
                definition, windowDays, engineJobCode, engineOutcomeKey,
                "TargetStateIncoherent", storedRefusal);
        }

        var resolution = await targetResolver.ResolveAsync(jobClass.Value, declaredTarget, cancellationToken);
        if (resolution.IsFailure)
        {
            return BlockedTargetResult(
                definition, windowDays, engineJobCode, engineOutcomeKey,
                resolution.Error!.Code, resolution.Error!.Message);
        }

        // Absent is a valid answer: a definition that declares no target and
        // whose class does not require one runs exactly as it did before T-065.
        ResolvedJobTarget? executedTarget = resolution.Value!.Target;
        var targetStatus = resolution.Value!.Outcome.ToString();

        // --- Step 1: ReadinessGate-governed learning run (never bypassed, never forced) ---
        string readinessStatus = "Unavailable";
        string readinessReason = "Governed learning function unavailable.";
        string? learningRunId = null;
        string learningStatus = "NotRun";
        var learningResultCount = 0;

        try
        {
            var governedRows = await QueryRowsAsync(
                dbContext,
                """
                SELECT job_code, run_id::text AS run_id, result_count, status, readiness_status, readiness_reason
                FROM public.ppiq_ml_run_learning_job_governed_v1(@jobCode, @windowDays, 20, false)
                """,
                cancellationToken,
                ("jobCode", engineJobCode),
                ("windowDays", windowDays));

            var governed = governedRows.FirstOrDefault();
            if (governed is not null)
            {
                readinessStatus = (string?)governed["readiness_status"] ?? "Unknown";
                readinessReason = (string?)governed["readiness_reason"] ?? "";
                learningRunId = (string?)governed["run_id"];
                learningStatus = (string?)governed["status"] ?? "Unknown";
                learningResultCount = (int)ToLong(governed["result_count"]);
            }
        }
        catch (Exception ex)
        {
            readinessStatus = "Error";
            readinessReason = "Governed learning run failed: " + ex.Message;
            learningStatus = "Failed";
        }

        // --- Step 2: deterministic correlation compute (existing engine, results_v2) ---
        Guid computeRunId = Guid.Empty;
        var computeStatus = "NotRun";
        var computeMessage = "";
        var computeResultCount = 0;
        var computeEngineKey = computeEngine.EngineKey;

        try
        {
            var computeResult = await computeEngine.ComputeAsync(
                new CorrelationComputeRequest(engineOutcomeKey, grain, windowDays),
                cancellationToken);

            computeRunId = computeResult.ComputeRunId;
            computeStatus = computeResult.Status;
            computeMessage = computeResult.Message;
            computeResultCount = computeResult.ResultCount;
            computeEngineKey = computeResult.EngineKey;
        }
        catch (Exception ex)
        {
            computeStatus = "Failed";
            computeMessage = "Deterministic compute failed: " + ex.Message;
        }

        // --- Step 3: stamp the definition (results tied to the definition) ---
        var definitionStatus =
            computeStatus == "Ok" && readinessStatus == "Ready" ? "Completed"
            : computeStatus == "Ok" ? "CompletedDeterministicOnly"
            : readinessStatus != "Ready" ? "BlockedReadiness"
            : "Failed";

        var lastResultJson = JsonSerializer.Serialize(new
        {
            ranAtUtc = DateTime.UtcNow,
            windowDays,
            engineOutcomeKey,
            engineJobCode,
            grain,
            readinessStatus,
            readinessReason,
            learningRunId,
            learningStatus,
            learningResultCount,
            computeEngineKey,
            computeRunId = computeRunId == Guid.Empty ? null : computeRunId.ToString("D"),
            computeStatus,
            computeMessage,
            computeResultCount,
            populationFilterNote = PopulationFilterNote
        });

        await ExecuteAsync(
            dbContext,
            """
            UPDATE public.inspection_jobs
            SET last_run_at_utc = now(),
                last_run_status = @status,
                last_result_json = CAST(@result_json AS jsonb),
                source_correlation_run_id = @compute_run_id,
                updated_at_utc = now()
            WHERE id = @id
            """,
            cancellationToken,
            ("status", definitionStatus),
            ("result_json", lastResultJson),
            ("compute_run_id", computeRunId == Guid.Empty ? (object)DBNull.Value : computeRunId),
            ("id", definition.Id));

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            code = definition.Code,
            definitionStatus,
            windowDays,
            readinessStatus,
            readinessReason,
            learningJobCode = engineJobCode,
            learningRunId,
            learningStatus,
            learningResultCount,
            computeEngineKey,
            computeRunId = computeRunId == Guid.Empty ? null : computeRunId.ToString("D"),
            computeStatus,
            computeMessage,
            computeResultCount,
            engineOutcomeKey,
            targetStatus,
            targetRefusalCode = (string?)null,
            targetRefusalReason = (string?)null,
            executedTargetDefinitionKind = executedTarget?.Kind.ToString(),
            executedTargetDefinitionId = executedTarget?.DefinitionId.ToString("D"),
            executedTargetDefinitionVersion = executedTarget?.ResolvedVersion,
            populationFilterNote = PopulationFilterNote,
            honestPositioning = HonestPositioning
        });
    }

    // ------------------------------------------------------------------
    // GET /{code}/results - results_v2 rows tied to the definition
    // ------------------------------------------------------------------
    private static async Task<IResult> GetDefinitionResultsAsync(
        string code,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var definition = await LoadDefinitionAsync(dbContext, code, cancellationToken);
        if (definition is null)
            return ApplicationProblems.NotFound($"Analysis job definition '{code}' was not found.");

        if (definition.SourceCorrelationRunId is null)
        {
            return Results.Ok(new
            {
                generatedAtUtc = DateTimeOffset.UtcNow,
                code = definition.Code,
                computeRunId = (string?)null,
                count = 0,
                results = Array.Empty<object>(),
                message = "No engine run is tied to this definition yet. Run the definition first.",
                honestPositioning = HonestPositioning
            });
        }

        var rows = await QueryRowsAsync(
            dbContext,
            """
            SELECT
                id::text AS id,
                compute_run_id::text AS compute_run_id,
                feature_key,
                feature_grain,
                outcome_key,
                outcome_type,
                method,
                coefficient,
                effect_size,
                effect_size_type,
                p_value,
                q_value,
                ci_low,
                ci_high,
                sample_size,
                effective_n,
                stratum,
                stability_score,
                is_stable,
                created_at_utc
            FROM public.ml_correlation_results_v2
            WHERE compute_run_id = @run_id
            ORDER BY q_value ASC NULLS LAST, effect_size DESC NULLS LAST
            LIMIT 100
            """,
            cancellationToken,
            ("run_id", definition.SourceCorrelationRunId.Value));

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            code = definition.Code,
            computeRunId = definition.SourceCorrelationRunId.Value.ToString("D"),
            count = rows.Count,
            results = rows,
            honestPositioning = HonestPositioning
        });
    }

    // ------------------------------------------------------------------
    // Shared helpers (mirrors MlLearningEndpoints raw-ADO pattern)
    // ------------------------------------------------------------------
    private const string DefinitionSelectSql =
        """
        SELECT
            id,
            inspection_job_code,
            inspection_job_name,
            inspection_type,
            parameter_code,
            defect_type,
            rule_json::text AS rule_json,
            schedule_expression,
            is_enabled,
            honest_state,
            source_correlation_run_id,
            last_run_at_utc,
            last_run_status,
            description,
            created_at_utc,
            updated_at_utc,
            target_definition_kind,
            target_definition_id,
            target_definition_version,
            target_version_policy,
            target_parameters::text AS target_parameters
        FROM public.inspection_jobs
        """;

    private static async Task<AnalysisJobDefinitionRow?> LoadDefinitionAsync(
        PlantProcessDbContext dbContext,
        string code,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = DefinitionSelectSql +
            " WHERE lower(inspection_job_code) = lower(@code) AND is_deleted = false LIMIT 1";
        AddParameter(command, "code", code.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadDefinition(reader);
    }

    private static AnalysisJobDefinitionRow ReadDefinition(DbDataReader reader)
    {
        return new AnalysisJobDefinitionRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? "{}" : reader.GetString(6),
            reader.GetString(7),
            reader.GetBoolean(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetDateTime(14),
            reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetGuid(17),
            reader.IsDBNull(18) ? null : reader.GetInt32(18),
            reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20));
    }

    private static string BuildRuleJson(
        int windowDays,
        Dictionary<string, string>? populationFilters,
        string? engineOutcomeKey,
        string? engineJobCode,
        string? grain)
    {
        return JsonSerializer.Serialize(new
        {
            windowDays,
            populationFilters = populationFilters ?? new Dictionary<string, string>(),
            engineOutcomeKey = string.IsNullOrWhiteSpace(engineOutcomeKey) ? "defect.rate_per_m2" : engineOutcomeKey.Trim(),
            engineJobCode = string.IsNullOrWhiteSpace(engineJobCode) ? "ML_PROCESS_VS_DEFECT" : engineJobCode.Trim(),
            grain = string.IsNullOrWhiteSpace(grain) ? "coil" : grain.Trim(),
            declaredScope = PopulationFilterNote
        });
    }

    private static int ClampWindow(int? windowDays)
    {
        var value = windowDays.GetValueOrDefault(30);
        if (value < 1) value = 1;
        if (value > 3650) value = 3650;
        return value;
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_").Replace("-", "_");
    }

    private static object NullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static long ToLong(object? value)
    {
        if (value is null) return 0;
        return Convert.ToInt64(value);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<int> ExecuteAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var p in parameters)
            AddParameter(command, p.Name, p.Value);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // T-065 target helpers. One coherence rule and one policy codec, used by
    // create, update and run alike, so the three cannot disagree about what a
    // target is.
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds the reference a request states, or returns the sentence naming why
    /// it is not a target. A null reference with a null return means the request
    /// states no target, which is a valid answer and not an empty one.
    /// </summary>
    private static string? TargetFromApi(
        string? kindText,
        Guid? definitionId,
        int? version,
        string? policyText,
        string? parametersJson,
        out JobTargetReference? target)
    {
        target = null;

        JobTargetVersionPolicy? policy = null;
        if (!string.IsNullOrWhiteSpace(policyText))
        {
            if (!JobTargetVersionPolicyCodec.TryFromApi(policyText, out var parsed))
                return JobTargetVersionPolicyCodec.UnknownPolicyMessage(policyText);

            policy = parsed;
        }

        return BuildTarget(kindText, definitionId, version, policy, parametersJson, out target);
    }

    /// <summary>
    /// Builds the reference a stored definition carries. The persisted policy is
    /// decoded through the same codec, so a value outside the closed vocabulary
    /// is refused here rather than silently read as one of the two.
    /// </summary>
    private static string? TargetFromStorage(
        AnalysisJobDefinitionRow definition,
        out JobTargetReference? target)
    {
        target = null;

        JobTargetVersionPolicy? policy = null;
        if (!string.IsNullOrWhiteSpace(definition.TargetVersionPolicy))
        {
            if (!JobTargetVersionPolicyCodec.TryFromStorage(definition.TargetVersionPolicy, out var parsed))
                return JobTargetVersionPolicyCodec.UnknownPolicyMessage(definition.TargetVersionPolicy);

            policy = parsed;
        }

        return BuildTarget(
            definition.TargetDefinitionKind,
            definition.TargetDefinitionId,
            definition.TargetDefinitionVersion,
            policy,
            definition.TargetParameters,
            out target);
    }

    private static string? BuildTarget(
        string? kindText,
        Guid? definitionId,
        int? version,
        JobTargetVersionPolicy? policy,
        string? parametersJson,
        out JobTargetReference? target)
    {
        target = null;

        var parameters = JobTargetParameters.Normalise(parametersJson);
        var statesSomething =
            !string.IsNullOrWhiteSpace(kindText) || definitionId.HasValue || policy.HasValue;

        if (!statesSomething)
        {
            // Half a target is not a target. A version or a parameter payload
            // beside no identity is the shape script 826 already refuses on the
            // canonical store, and it is refused here for the same reason.
            if (version.HasValue)
                return "A target version cannot be stated without a target definition.";

            if (parameters is not null)
                return "Target parameters cannot be stated without a target definition.";

            return null;
        }

        if (string.IsNullOrWhiteSpace(kindText) || !definitionId.HasValue || !policy.HasValue)
            return "A target is a kind, an identity and a version policy together, or it is absent.";

        if (!TryParseDefinitionKind(kindText, out var kind))
            return "'" + kindText.Trim() + "' is not a declared definition kind.";

        var candidate = new JobTargetReference
        {
            Kind = kind,
            DefinitionId = definitionId.Value,
            VersionPolicy = policy.Value,
            PinnedVersion = version,
            ParametersJson = parameters
        };

        var structural = candidate.Validate();
        if (structural is not null)
            return structural;

        target = candidate;
        return null;
    }

    /// <summary>
    /// Exact, case-sensitive, and never numeric. Enum.TryParse alone would accept
    /// "4" as a kind, which is a definition surface nobody typed.
    /// </summary>
    private static bool TryParseDefinitionKind(string? value, out DefinitionKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (!Enum.TryParse(trimmed, false, out kind))
            return false;

        return string.Equals(Enum.GetName(kind), trimmed, StringComparison.Ordinal);
    }

    private static object? TargetKindColumn(JobTargetReference? target) => target?.Kind.ToString();

    private static object? TargetIdColumn(JobTargetReference? target) => target?.DefinitionId;

    private static object? TargetVersionColumn(JobTargetReference? target) => target?.PinnedVersion;

    private static object? TargetPolicyColumn(JobTargetReference? target) =>
        target is null ? null : JobTargetVersionPolicyCodec.ToStorage(target.VersionPolicy);

    /// <summary>Absent stays SQL NULL and "{}" stays "{}". The two are different statements.</summary>
    private static object? TargetParametersColumn(JobTargetReference? target) => target?.ParametersJson;

    /// <summary>
    /// Exact string or nothing. A blank value and a non-string value are both
    /// absence: a declaration that says the empty string is not a declaration.
    /// </summary>
    private static string? ReadDeclaredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
    }

    private static IResult BlockedTargetResult(
        AnalysisJobDefinitionRow definition,
        int windowDays,
        string engineJobCode,
        string engineOutcomeKey,
        string refusalCode,
        string refusalReason)
    {
        return BlockedRunResult(
            definition, windowDays, engineJobCode, engineOutcomeKey,
            "BlockedTarget", refusalCode, refusalReason);
    }

    /// <summary>
    /// The honest refusal. Nothing is written and no engine runs, so the executed
    /// identity is null rather than an echo of what was requested - a requested
    /// selector is not proof that anything executed.
    /// </summary>
    private static IResult BlockedRunResult(
        AnalysisJobDefinitionRow definition,
        int windowDays,
        string? engineJobCode,
        string? engineOutcomeKey,
        string definitionStatus,
        string refusalCode,
        string refusalReason)
    {
        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            code = definition.Code,
            definitionStatus,
            windowDays,
            readinessStatus = "NotEvaluated",
            readinessReason = "The run was refused before any engine side effect, so readiness was not evaluated.",
            learningJobCode = engineJobCode,
            learningRunId = (string?)null,
            learningStatus = "NotRun",
            learningResultCount = 0,
            computeEngineKey = (string?)null,
            computeRunId = (string?)null,
            computeStatus = "NotRun",
            computeMessage = "",
            computeResultCount = 0,
            engineOutcomeKey,
            targetStatus = "Refused",
            targetRefusalCode = refusalCode,
            targetRefusalReason = refusalReason,
            executedTargetDefinitionKind = (string?)null,
            executedTargetDefinitionId = (string?)null,
            executedTargetDefinitionVersion = (int?)null,
            populationFilterNote = PopulationFilterNote,
            honestPositioning = HonestPositioning
        });
    }

    private static async Task<string?> ScalarStringAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var p in parameters)
            AddParameter(command, p.Name, p.Value);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull ? null : value.ToString();
    }

    private static async Task<bool> ScalarBoolAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var p in parameters)
            AddParameter(command, p.Name, p.Value);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool b && b;
    }

    private static async Task<List<Dictionary<string, object?>>> QueryRowsAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        foreach (var p in parameters)
            AddParameter(command, p.Name, p.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : NormalizeValue(reader.GetValue(i));
                row[name] = value;
            }
            rows.Add(row);
        }

        return rows;
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            DBNull => null,
            Guid guid => guid.ToString("D"),
            DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            double number => double.IsFinite(number) ? number : null,
            float number => float.IsFinite(number) ? number : null,
            _ => value
        };
    }
}

public sealed record AnalysisJobDefinitionRow(
    Guid Id,
    string Code,
    string Name,
    string InspectionType,
    string? ParameterCode,
    string? DefectType,
    string RuleJson,
    string ScheduleExpression,
    bool IsEnabled,
    string HonestState,
    Guid? SourceCorrelationRunId,
    DateTime? LastRunAtUtc,
    string? LastRunStatus,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? TargetDefinitionKind = null,
    Guid? TargetDefinitionId = null,
    int? TargetDefinitionVersion = null,
    string? TargetVersionPolicy = null,
    string? TargetParameters = null);

public sealed record AnalysisJobListResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<AnalysisJobDefinitionRow> Rows);

public sealed record CreateAnalysisJobDefinitionRequest(
    string Code,
    string Name,
    string DefectType,
    string? ParameterCode,
    int? WindowDays,
    Dictionary<string, string>? PopulationFilters,
    string? EngineOutcomeKey,
    string? EngineJobCode,
    string? Grain,
    string? ScheduleExpression,
    bool? IsEnabled,
    string? Description,
    string? TargetDefinitionKind = null,
    Guid? TargetDefinitionId = null,
    int? TargetDefinitionVersion = null,
    string? TargetVersionPolicy = null,
    string? TargetParameters = null);

public sealed record UpdateAnalysisJobDefinitionRequest(
    string Name,
    string DefectType,
    string? ParameterCode,
    int? WindowDays,
    Dictionary<string, string>? PopulationFilters,
    string? EngineOutcomeKey,
    string? EngineJobCode,
    string? Grain,
    string? ScheduleExpression,
    bool? IsEnabled,
    string? Description,
    string? TargetDefinitionKind = null,
    Guid? TargetDefinitionId = null,
    int? TargetDefinitionVersion = null,
    string? TargetVersionPolicy = null,
    string? TargetParameters = null);

public sealed record RunAnalysisJobRequest(
    int? WindowDaysOverride);