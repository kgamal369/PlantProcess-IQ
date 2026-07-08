using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.ErrorHandling;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Licensing.Contracts;
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
                source_system, source_record_id, created_at_utc
            )
            VALUES
            (
                @id, @code, @name, 'AnalysisJobDefinition',
                NULL, @parameter_code, @defect_type,
                NULL, NULL, CAST(@rule_json AS jsonb), @schedule,
                @is_enabled, 'RuleBasedMonitoring', @description, false,
                'PlantProcessIQ.Surface3.AnalysisJobDefinition', NULL, now()
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
            ("description", NullableText(request.Description)));

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
        CancellationToken cancellationToken)
    {
        var definition = await LoadDefinitionAsync(dbContext, code, cancellationToken);
        if (definition is null)
            return ApplicationProblems.NotFound($"Analysis job definition '{code}' was not found.");

        // Resolve run parameters from the saved rule_json (definition drives the job).
        var windowDays = ClampWindow(request.WindowDaysOverride);
        var engineOutcomeKey = "defect.rate_per_m2";
        var engineJobCode = "ML_PROCESS_VS_DEFECT";
        var grain = "coil";

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(definition.RuleJson) ? "{}" : definition.RuleJson);
            var root = doc.RootElement;

            if (request.WindowDaysOverride is null
                && root.TryGetProperty("windowDays", out var w)
                && w.ValueKind == JsonValueKind.Number)
                windowDays = ClampWindow(w.GetInt32());

            if (root.TryGetProperty("engineOutcomeKey", out var ok) && ok.ValueKind == JsonValueKind.String)
            {
                var v = ok.GetString();
                if (!string.IsNullOrWhiteSpace(v)) engineOutcomeKey = v!;
            }

            if (root.TryGetProperty("engineJobCode", out var jc) && jc.ValueKind == JsonValueKind.String)
            {
                var v = jc.GetString();
                if (!string.IsNullOrWhiteSpace(v)) engineJobCode = v!;
            }

            if (root.TryGetProperty("grain", out var g) && g.ValueKind == JsonValueKind.String)
            {
                var v = g.GetString();
                if (!string.IsNullOrWhiteSpace(v)) grain = v!;
            }
        }
        catch (JsonException)
        {
            // Corrupt rule_json never blocks an honest run with defaults.
        }

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
            updated_at_utc
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
            reader.IsDBNull(15) ? null : reader.GetDateTime(15));
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
    DateTime? UpdatedAtUtc);

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
    string? Description);

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
    string? Description);

public sealed record RunAnalysisJobRequest(
    int? WindowDaysOverride);