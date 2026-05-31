
using System.Data;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class MlLearningEndpoints
{
    public static IEndpointRouteBuilder MapMlLearningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ml/learning")
            .WithTags("ML Learning")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/status", GetStatusAsync)
            .WithSummary("Get Phase 4/5 ML learning acceptance status")
            .WithDescription("Returns deterministic ML learning proof status. This does not train or expose a production prediction model.");

        group.MapGet("/jobs", GetJobsAsync)
            .WithSummary("List the four ML learning job definitions");

        group.MapPost("/jobs/{jobCode}/run", RunJobAsync)
            .WithSummary("Run a deterministic ML learning job now")
            .WithDescription("Runs an in-house deterministic statistical association sweep over the golden dataset.");

        group.MapGet("/results", GetResultsAsync)
            .WithSummary("List recent ML learning results");

        return app;
    }

    private static async Task<IResult> GetStatusAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            db,
            "SELECT public.ppiq_ml_phase45_acceptance() AS acceptance_json;",
            cancellationToken);

        return Results.Ok(new
        {
            phase = "P04/P05",
            generatedAtUtc = DateTime.UtcNow,
            status = rows.FirstOrDefault(),
            honestPositioning = "Deterministic diagnostic learning proof only. No production prediction is active.",
        });
    }

    private static async Task<IResult> GetJobsAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            db,
            """
            SELECT
                job_code,
                job_name,
                job_type,
                outcome_family,
                default_schedule,
                default_window_days,
                is_enabled,
                readiness_gate_code,
                backoff_minutes,
                max_backoff_minutes,
                last_status,
                last_run_id,
                last_finished_at_utc,
                next_run_hint_utc,
                last_error
            FROM public.ml_learning_job_catalog_v1
            ORDER BY
                CASE job_code
                    WHEN 'ML_PROCESS_VS_DEFECT' THEN 1
                    WHEN 'ML_PROCESS_VS_DOWNTIME' THEN 2
                    WHEN 'ML_PROCESS_VS_KPI' THEN 3
                    WHEN 'ML_WEEKLY_OVERALL' THEN 4
                    ELSE 99
                END;
            """,
            cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            jobs = rows,
        });
    }

    private static async Task<IResult> RunJobAsync(
        string jobCode,
        RunLearningJobRequest? request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var outcomeFamily = string.IsNullOrWhiteSpace(request?.OutcomeFamily)
            ? null
            : request!.OutcomeFamily!.Trim();

        var windowDays = request?.WindowDays is > 0
            ? request.WindowDays!.Value
            : 365;

        var rows = await QueryAsync(
            db,
            """
            SELECT
                run_id,
                result_count,
                top_feature_key,
                top_outcome_key,
                top_effect_size,
                status
            FROM public.ppiq_ml_run_learning_job_v1(@job_code, @outcome_family, @window_days);
            """,
            cancellationToken,
            ("job_code", jobCode),
            ("outcome_family", outcomeFamily),
            ("window_days", windowDays));

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            jobCode,
            outcomeFamily,
            windowDays,
            result = rows.FirstOrDefault(),
            honestPositioning = "Association results are evidence for review, not guaranteed root-cause proof.",
        });
    }

    private static async Task<IResult> GetResultsAsync(
        string? jobCode,
        string? outcomeKey,
        int? limit,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit ?? 25, 1, 200);

        var rows = await QueryAsync(
            db,
            """
            SELECT
                r.id,
                r.run_id,
                r.job_code,
                r.outcome_family,
                r.feature_key,
                r.feature_grain,
                r.feature_value_type,
                r.outcome_key,
                r.outcome_type,
                r.method,
                r.coefficient,
                r.effect_size,
                r.effect_size_type,
                r.direction,
                r.p_value,
                r.q_value,
                r.ci_low,
                r.ci_high,
                r.sample_size,
                r.effective_n,
                r.power_status,
                r.strength_bucket,
                r.stability_score,
                r.is_stable,
                r.confounding_note,
                r.vif_group_key,
                r.finding_status,
                r.created_at_utc
            FROM public.ml_learning_results_v1 r
            WHERE (@job_code IS NULL OR lower(r.job_code) = lower(@job_code))
              AND (@outcome_key IS NULL OR lower(r.outcome_key) = lower(@outcome_key))
            ORDER BY r.effect_size DESC NULLS LAST, r.q_value ASC NULLS LAST
            LIMIT @limit;
            """,
            cancellationToken,
            ("job_code", string.IsNullOrWhiteSpace(jobCode) ? null : jobCode.Trim()),
            ("outcome_key", string.IsNullOrWhiteSpace(outcomeKey) ? null : outcomeKey.Trim()),
            ("limit", safeLimit));

        return Results.Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            count = rows.Count,
            results = rows,
        });
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);

                row[reader.GetName(i)] = value switch
                {
                    DateTime dt => dt.ToString("O"),
                    DateTimeOffset dto => dto.ToString("O"),
                    Guid guid => guid.ToString(),
                    _ => value?.ToString()
                };
            }

            rows.Add(row);
        }

        return rows;
    }

    private sealed record RunLearningJobRequest(
        string? OutcomeFamily,
        int? WindowDays);
}
