using System.Data;
using System.Data.Common;
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
            .WithSummary("Get ML learning acceptance/runtime status");

        group.MapGet("/jobs", GetJobsAsync)
            .WithSummary("List ML learning job definitions");

        group.MapPost("/jobs/{jobCode}/run", RunJobAsync)
            .WithSummary("Run one governed ML learning job");

        group.MapGet("/results", GetResultsAsync)
            .WithSummary("Get persisted ML learning results");

        return app;
    }

    private static async Task<IResult> GetStatusAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await QueryRowsAsync(
            dbContext,
            """
            SELECT
                'P04/P05'::text AS phase,
                now() AS generated_at_utc,
                acceptance_json::text AS acceptance_json
            FROM public.ppiq_ml_phase45_acceptance()
            """,
            cancellationToken);

        return Results.Ok(new
        {
            phase = "P04/P05",
            generatedAtUtc = DateTimeOffset.UtcNow,
            status = rows.FirstOrDefault()
        });
    }

    private static async Task<IResult> GetJobsAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await QueryRowsAsync(
            dbContext,
            """
            SELECT
                job_code,
                job_name,
                job_type,
                outcome_family,
                default_schedule,
                is_enabled,
                readiness_gate_code,
                backoff_minutes,
                max_backoff_minutes,
                last_status,
                last_run_id::text AS last_run_id,
                last_started_at_utc,
                last_finished_at_utc,
                next_run_hint_utc,
                last_error
            FROM public.ml_learning_job_catalog_v1
            ORDER BY job_code
            """,
            cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            jobs = rows
        });
    }

    private static async Task<IResult> RunJobAsync(
        string jobCode,
        MlLearningRunRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobCode))
        {
            return Results.BadRequest(new { message = "jobCode is required." });
        }

        var windowDays = request.WindowDays is > 0 ? request.WindowDays.Value : 365;

        var rows = await QueryRowsAsync(
            dbContext,
            """
            SELECT
                job_code,
                run_id::text AS run_id,
                result_count,
                status,
                readiness_status,
                readiness_reason
            FROM public.ppiq_ml_run_learning_job_governed_v1(@jobCode, @windowDays, 20, false)
            """,
            cancellationToken,
            ("jobCode", jobCode.Trim()),
            ("windowDays", windowDays));

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            jobCode = jobCode.Trim(),
            outcomeFamily = request.OutcomeFamily,
            windowDays,
            result = rows.FirstOrDefault(),
            honestPositioning = "Association results are evidence for review, not guaranteed root-cause proof."
        });
    }

    private static async Task<IResult> GetResultsAsync(
        int? limit,
        string? jobCode,
        string? findingStatus,
        string? method,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit ?? 50, 1, 200);

        var filters = new List<string>();
        var parameters = new List<(string Name, object? Value)>
        {
            ("limit", safeLimit)
        };

        if (!string.IsNullOrWhiteSpace(jobCode))
        {
            filters.Add("job_code = @jobCode");
            parameters.Add(("jobCode", jobCode.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(findingStatus))
        {
            filters.Add("finding_status = @findingStatus");
            parameters.Add(("findingStatus", findingStatus.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            filters.Add("method = @method");
            parameters.Add(("method", method.Trim()));
        }

        var where = filters.Count == 0
            ? ""
            : "WHERE " + string.Join(" AND ", filters);

        var sql = $"""
            SELECT
                id::text AS id,
                run_id::text AS run_id,
                job_code,
                outcome_family,
                feature_key,
                feature_grain,
                feature_value_type,
                outcome_key,
                outcome_type,
                method,
                coefficient,
                effect_size,
                effect_size_type,
                direction,
                raw_statistic,
                p_value,
                q_value,
                ci_low,
                ci_high,
                sample_size,
                effective_n,
                independent_unit_type,
                power_status,
                strength_bucket,
                stability_score,
                is_stable,
                confounding_note,
                vif_group_key,
                finding_status,
                evidence_json::text AS evidence_json,
                created_at_utc
            FROM public.ml_learning_results_v1
            {where}
            ORDER BY
                effect_size DESC NULLS LAST,
                q_value ASC NULLS LAST,
                created_at_utc DESC
            LIMIT @limit
            """;

        var rows = await QueryRowsAsync(
            dbContext,
            sql,
            cancellationToken,
            parameters.ToArray());

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            limit = safeLimit,
            count = rows.Count,
            filters = new
            {
                jobCode,
                findingStatus,
                method
            },
            results = rows,
            honestPositioning = "Diagnostic associations only. These rows are evidence for engineering review, not guaranteed root cause."
        });
    }

    private static async Task<List<Dictionary<string, object?>>> QueryRowsAsync(
        PlantProcessDbContext dbContext,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;

        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

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
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            decimal number => number,
            double number => double.IsFinite(number) ? number : null,
            float number => float.IsFinite(number) ? number : null,
            int number => number,
            long number => number,
            short number => number,
            bool boolean => boolean,
            string text => text,
            _ => value.ToString()
        };
    }

    private sealed record MlLearningRunRequest(
        string? OutcomeFamily,
        int? WindowDays);
}
