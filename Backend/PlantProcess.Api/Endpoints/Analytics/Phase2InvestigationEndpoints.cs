using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class Phase2InvestigationEndpoints
{
    public static IEndpointRouteBuilder MapPhase2InvestigationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/phase2")
            .WithTags("Analytics - Phase 2 Investigation")
            .RequireAuthorization("PlantProcessViewer");

        // PPIQ-WF-015
        group.MapPost("/inspection-jobs/save-from-correlation", SaveInspectionJobFromCorrelationAsync)
            .WithName("SaveInspectionJobFromCorrelation")
            .Produces<InspectionJobRow>();

        group.MapGet("/inspection-jobs", GetInspectionJobsAsync)
            .WithName("GetInspectionJobs")
            .Produces<InspectionJobListResponse>();

        // PPIQ-WF-017
        group.MapPost("/rule-correlation/run", RunRuleBasedCorrelationAsync)
            .WithName("RunRuleBasedCorrelation")
            .Produces<RuleCorrelationResponse>();

        // PPIQ-WF-018
        group.MapGet("/ml-lifecycle", GetMlLifecycleStatesAsync)
            .WithName("GetMlLifecycleStates")
            .Produces<MlLifecycleListResponse>();

        group.MapPost("/ml-lifecycle/evaluate", EvaluateMlLifecycleAsync)
            .WithName("EvaluateMlLifecycle")
            .Produces<MlLifecycleRow>();

        return app;
    }

    private static async Task<IResult> SaveInspectionJobFromCorrelationAsync(
        [FromBody] SaveInspectionJobFromCorrelationRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InspectionJobCode))
            return Results.BadRequest(new { message = "InspectionJobCode is required." });

        if (string.IsNullOrWhiteSpace(request.InspectionJobName))
            return Results.BadRequest(new { message = "InspectionJobName is required." });

        var id = Guid.NewGuid();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO public.inspection_jobs
            (
                id,
                inspection_job_code,
                inspection_job_name,
                inspection_type,
                source_correlation_run_id,
                parameter_code,
                defect_type,
                site_id,
                equipment_id,
                rule_json,
                schedule_expression,
                is_enabled,
                honest_state,
                description,
                is_synthetic,
                source_system,
                source_record_id,
                created_at_utc
            )
            VALUES
            (
                {0},
                {1},
                {2},
                {3},
                {4},
                {5},
                {6},
                {7},
                {8},
                CAST({9} AS jsonb),
                {10},
                {11},
                {12},
                {13},
                {14},
                {15},
                {16},
                now()
            )
            """,
            id,
            NormalizeCode(request.InspectionJobCode),
            request.InspectionJobName.Trim(),
            request.InspectionType ?? "RuleBasedQualityWatch",
            request.SourceCorrelationRunId,
            request.ParameterCode,
            request.DefectType,
            request.SiteId,
            request.EquipmentId,
            string.IsNullOrWhiteSpace(request.RuleJson) ? "{}" : request.RuleJson,
            request.ScheduleExpression ?? "Manual",
            request.IsEnabled,
            "RuleBasedMonitoring",
            request.Description,
            request.IsSynthetic,
            "PlantProcessIQ.Phase2.InspectionJob",
            request.SourceCorrelationRunId?.ToString(),
            cancellationToken);

        return Results.Ok(new InspectionJobRow(
            id,
            NormalizeCode(request.InspectionJobCode),
            request.InspectionJobName.Trim(),
            request.InspectionType ?? "RuleBasedQualityWatch",
            request.ParameterCode,
            request.DefectType,
            request.IsEnabled,
            "RuleBasedMonitoring",
            request.ScheduleExpression ?? "Manual",
            null,
            null,
            DateTime.UtcNow));
    }

    private static async Task<IResult> GetInspectionJobsAsync(
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<InspectionJobRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                inspection_job_code,
                inspection_job_name,
                inspection_type,
                parameter_code,
                defect_type,
                is_enabled,
                honest_state,
                schedule_expression,
                last_run_at_utc,
                last_run_status,
                created_at_utc
            FROM public.inspection_jobs
            WHERE is_deleted = false
            ORDER BY created_at_utc DESC
            LIMIT 200
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadInspectionJob(reader));

        return Results.Ok(new InspectionJobListResponse(DateTime.UtcNow, rows));
    }

    private static async Task<IResult> RunRuleBasedCorrelationAsync(
        [FromBody] RuleCorrelationRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ParameterCode))
            return Results.BadRequest(new { message = "ParameterCode is required." });

        if (string.IsNullOrWhiteSpace(request.DefectType))
            return Results.BadRequest(new { message = "DefectType is required." });

        var fromUtc = request.FromUtc ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = request.ToUtc ?? DateTime.UtcNow;

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            WITH parameter_scope AS
            (
                SELECT
                    mu.id AS material_unit_id,
                    po.numeric_value,
                    ntile(5) OVER (ORDER BY po.numeric_value) AS value_bucket
                FROM parameter_observations po
                JOIN parameter_definitions pd
                    ON pd.id = po.parameter_definition_id
                JOIN material_units mu
                    ON mu.id = po.material_unit_id
                WHERE pd.parameter_code = @parameter_code
                  AND po.numeric_value IS NOT NULL
                  AND po.observed_at_utc >= @from_utc
                  AND po.observed_at_utc < @to_utc
                  AND po.is_deleted = false
                  AND pd.is_deleted = false
            ),
            defect_scope AS
            (
                SELECT DISTINCT
                    material_unit_id
                FROM quality_events
                WHERE is_deleted = false
                  AND event_type ILIKE '%' || @defect_type || '%'
            )
            SELECT
                p.value_bucket,
                COUNT(*) AS material_count,
                COUNT(d.material_unit_id) AS defect_count,
                CASE
                    WHEN COUNT(*) = 0 THEN 0
                    ELSE ROUND(COUNT(d.material_unit_id)::numeric / COUNT(*)::numeric * 100, 4)
                END AS defect_rate_pct,
                MIN(p.numeric_value) AS min_value,
                MAX(p.numeric_value) AS max_value,
                AVG(p.numeric_value) AS avg_value
            FROM parameter_scope p
            LEFT JOIN defect_scope d
                ON d.material_unit_id = p.material_unit_id
            GROUP BY p.value_bucket
            ORDER BY p.value_bucket
            """;

        command.Parameters.AddWithValue("parameter_code", NormalizeCode(request.ParameterCode));
        command.Parameters.AddWithValue("defect_type", request.DefectType.Trim());
        command.Parameters.AddWithValue("from_utc", fromUtc);
        command.Parameters.AddWithValue("to_utc", toUtc);

        var buckets = new List<RuleCorrelationBucket>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            buckets.Add(new RuleCorrelationBucket(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6)));
        }

        var strength = CalculateRuleStrength(buckets);

        return Results.Ok(new RuleCorrelationResponse(
            DateTime.UtcNow,
            NormalizeCode(request.ParameterCode),
            request.DefectType.Trim(),
            fromUtc,
            toUtc,
            strength,
            strength >= 0.35m
                ? "Suspected contributor pattern detected. Treat as investigation evidence, not guaranteed root cause."
                : "No strong rule-based signal detected in the selected window.",
            buckets));
    }

    private static async Task<IResult> GetMlLifecycleStatesAsync(
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = new List<MlLifecycleRow>();

        await using var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                ml_job_code,
                ml_job_name,
                state,
                state_reason,
                readiness_score,
                label_count,
                feature_count,
                last_evaluated_at_utc,
                next_recommended_action,
                no_production_prediction,
                metadata_json::text
            FROM public.ml_job_lifecycle_states
            WHERE is_deleted = false
              AND is_active = true
            ORDER BY ml_job_code
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            rows.Add(ReadMlLifecycle(reader));

        return Results.Ok(new MlLifecycleListResponse(DateTime.UtcNow, rows));
    }

    private static async Task<IResult> EvaluateMlLifecycleAsync(
        [FromBody] EvaluateMlLifecycleRequest request,
        [FromServices] PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var labelCount = await dbContext.QualityEvents.CountAsync(x => !x.IsDeleted, cancellationToken);
        var featureCount = await dbContext.ParameterObservations.CountAsync(x => !x.IsDeleted, cancellationToken);

        var readinessScore =
            labelCount < 500 || featureCount < 5000
                ? 35m
                : labelCount < 2000 || featureCount < 20000
                    ? 60m
                    : 75m;

        var state =
            readinessScore < 50
                ? "ReadinessOnly"
                : readinessScore < 70
                    ? "RuleBasedMonitoring"
                    : "TrainingCandidate";

        var reason =
            state == "ReadinessOnly"
                ? "Not enough validated labels/features for trained production ML."
                : state == "RuleBasedMonitoring"
                    ? "Enough data for rule-based/correlation monitoring; trained ML still requires label governance."
                    : "Training candidate only. Still no production prediction until a model is trained, validated, governed, and approved.";

        var id = request.Id ?? Guid.NewGuid();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO public.ml_job_lifecycle_states
            (
                id,
                ml_job_code,
                ml_job_name,
                state,
                state_reason,
                readiness_score,
                label_count,
                feature_count,
                last_evaluated_at_utc,
                next_recommended_action,
                no_production_prediction,
                metadata_json,
                is_active,
                created_at_utc,
                updated_at_utc
            )
            VALUES
            (
                {0},
                {1},
                {2},
                {3},
                {4},
                {5},
                {6},
                {7},
                now(),
                {8},
                true,
                CAST({9} AS jsonb),
                true,
                now(),
                now()
            )
            ON CONFLICT (lower(ml_job_code))
            WHERE is_deleted = false
            DO UPDATE SET
                state = EXCLUDED.state,
                state_reason = EXCLUDED.state_reason,
                readiness_score = EXCLUDED.readiness_score,
                label_count = EXCLUDED.label_count,
                feature_count = EXCLUDED.feature_count,
                last_evaluated_at_utc = now(),
                next_recommended_action = EXCLUDED.next_recommended_action,
                no_production_prediction = true,
                metadata_json = EXCLUDED.metadata_json,
                updated_at_utc = now()
            """,
            id,
            NormalizeCode(request.MlJobCode),
            request.MlJobName,
            state,
            reason,
            readinessScore,
            labelCount,
            featureCount,
            state == "TrainingCandidate"
                ? "Prepare offline training dataset and validation protocol."
                : "Continue data readiness, label governance and rule-based monitoring.",
            JsonSerializer.Serialize(new
            {
                task = "PPIQ-WF-018",
                evaluatedAtUtc = DateTime.UtcNow,
                labelCount,
                featureCount
            }),
            cancellationToken);

        return Results.Ok(new MlLifecycleRow(
            id,
            NormalizeCode(request.MlJobCode),
            request.MlJobName,
            state,
            reason,
            readinessScore,
            labelCount,
            featureCount,
            DateTime.UtcNow,
            state == "TrainingCandidate"
                ? "Prepare offline training dataset and validation protocol."
                : "Continue data readiness, label governance and rule-based monitoring.",
            true,
            "{}"));
    }

    private static InspectionJobRow ReadInspectionJob(IDataRecord reader)
    {
        return new InspectionJobRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetBoolean(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetDateTime(11));
    }

    private static MlLifecycleRow ReadMlLifecycle(IDataRecord reader)
    {
        return new MlLifecycleRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetInt32(7),
            reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetBoolean(10),
            reader.GetString(11));
    }

    private static decimal CalculateRuleStrength(IReadOnlyList<RuleCorrelationBucket> buckets)
    {
        if (buckets.Count < 2)
            return 0;

        var rates = buckets.Select(x => x.DefectRatePct).ToArray();
        var min = rates.Min();
        var max = rates.Max();

        if (max <= 0)
            return 0;

        return Math.Round((max - min) / 100m, 4);
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_").Replace("-", "_");
    }

    private static object DbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static object DbValue(Guid? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(DateTime? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object DbValue(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }
}

public sealed record SaveInspectionJobFromCorrelationRequest(
    string InspectionJobCode,
    string InspectionJobName,
    string? InspectionType,
    Guid? SourceCorrelationRunId,
    string? ParameterCode,
    string? DefectType,
    Guid? SiteId,
    Guid? EquipmentId,
    string? RuleJson,
    string? ScheduleExpression,
    bool IsEnabled,
    bool IsSynthetic,
    string? Description);

public sealed record InspectionJobListResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<InspectionJobRow> Rows);

public sealed record InspectionJobRow(
    Guid Id,
    string InspectionJobCode,
    string InspectionJobName,
    string InspectionType,
    string? ParameterCode,
    string? DefectType,
    bool IsEnabled,
    string HonestState,
    string ScheduleExpression,
    DateTime? LastRunAtUtc,
    string? LastRunStatus,
    DateTime CreatedAtUtc);

public sealed record RuleCorrelationRequest(
    string ParameterCode,
    string DefectType,
    Guid? SiteId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public sealed record RuleCorrelationResponse(
    DateTime GeneratedAtUtc,
    string ParameterCode,
    string DefectType,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal RuleStrength,
    string Interpretation,
    IReadOnlyList<RuleCorrelationBucket> Buckets);

public sealed record RuleCorrelationBucket(
    int BucketNumber,
    int MaterialCount,
    int DefectCount,
    decimal DefectRatePct,
    decimal? MinValue,
    decimal? MaxValue,
    decimal? AvgValue);

public sealed record MlLifecycleListResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<MlLifecycleRow> Rows);

public sealed record MlLifecycleRow(
    Guid Id,
    string MlJobCode,
    string MlJobName,
    string State,
    string StateReason,
    decimal? ReadinessScore,
    int? LabelCount,
    int? FeatureCount,
    DateTime? LastEvaluatedAtUtc,
    string? NextRecommendedAction,
    bool NoProductionPrediction,
    string MetadataJson);

public sealed record EvaluateMlLifecycleRequest(
    Guid? Id,
    string MlJobCode,
    string MlJobName);