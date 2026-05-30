using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Demo.Interfaces;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Demo;

public static class DemoLifecycleEndpoints
{
    private static readonly ConcurrentDictionary<Guid, DemoResetJobDto> ResetJobs = new();
    private static readonly ConcurrentDictionary<string, DateTime> LastResetByOperator = new();

    public static IEndpointRouteBuilder MapDemoLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        var demoGroup = app.MapGroup("/demo")
            .WithTags("Demo Lifecycle")
            .RequireAuthorization("PlantProcessViewer");

        demoGroup.MapGet("/lifecycle", GetLifecycleAsync)
            .WithSummary("Get the complete PlantProcess IQ demo lifecycle")
            .WithDescription("Returns the controlled lifecycle: license -> connector -> staging -> schema mapping -> jobs -> dashboard -> risk/correlation -> ML readiness -> final report.");

        var resetGroup = app.MapGroup("/demo-lifecycle")
            .WithTags("Demo Lifecycle Reset")
            .RequireAuthorization("PlantProcessDataManager");

        resetGroup.MapPost("/reset", StartResetAsync)
            .WithSummary("PPIQ-WF-022: Start controlled demo reset")
            .WithDescription("Returns 202 with jobId and statusUrl. Reset runs as a background operation and exposes step progress.");

        resetGroup.MapGet("/reset/{jobId:guid}", GetResetProgress)
            .WithSummary("Get demo reset progress");

        resetGroup.MapGet("/reset/{jobId:guid}/progress", GetResetProgress)
            .WithSummary("Get demo reset step-by-step progress");

        return app;
    }

    private static async Task<IResult> GetLifecycleAsync(
        IDemoLifecycleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDemoLifecycleAsync(cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static IResult StartResetAsync(
        DemoResetRequest? request,
        IServiceScopeFactory scopeFactory,
        ClaimsPrincipal user)
    {
        var operatorName =
            user.Identity?.Name ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            "demo-operator";

        var now = DateTime.UtcNow;

        if (LastResetByOperator.TryGetValue(operatorName, out var lastReset) &&
            now - lastReset < TimeSpan.FromMinutes(5))
        {
            return Results.Json(
                new
                {
                    error = "Rate limit exceeded",
                    message = "Only one demo reset is allowed every 5 minutes per operator.",
                    retryAfterSeconds = Math.Ceiling((TimeSpan.FromMinutes(5) - (now - lastReset)).TotalSeconds)
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var scope = NormalizeScope(request?.Scope);
        var jobId = Guid.NewGuid();

        var job = DemoResetJobDto.Create(jobId, scope, operatorName);
        ResetJobs[jobId] = job;
        LastResetByOperator[operatorName] = now;

        _ = Task.Run(() => RunResetJobAsync(jobId, scopeFactory));

        return Results.Accepted(
            $"/demo-lifecycle/reset/{jobId}/progress",
            new
            {
                jobId,
                statusUrl = $"/demo-lifecycle/reset/{jobId}/progress",
                status = job.Status,
                scope,
                acceptedAtUtc = job.StartedAtUtc
            });
    }

    private static IResult GetResetProgress(Guid jobId)
    {
        if (!ResetJobs.TryGetValue(jobId, out var job))
        {
            return Results.NotFound(new
            {
                jobId,
                message = "Reset job was not found. It may have expired from memory after service restart."
            });
        }

        return Results.Ok(job);
    }

    private static async Task RunResetJobAsync(Guid jobId, IServiceScopeFactory scopeFactory)
    {
        if (!ResetJobs.TryGetValue(jobId, out var job))
            return;

        try
        {
            job.MarkRunning();

            await CompleteStepAsync(job, "stop-runtime", "Stopped transient demo activities", 250);
            await CompleteStepAsync(job, "clean-runtime", "Cleaned generated runtime state", 250);

            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PlantProcessDbContext>();

                job.StartStep("audit-log", "Writing reset audit log", 35);
                await EnsureResetAuditTableAsync(dbContext, CancellationToken.None);
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO demo_lifecycle_reset_audit
                    (
                        id,
                        reset_job_id,
                        reset_scope,
                        operator_name,
                        status,
                        created_at_utc,
                        details_json
                    )
                    VALUES
                    (
                        {Guid.NewGuid()},
                        {job.JobId},
                        {job.Scope},
                        {job.OperatorName},
                        {"Started"},
                        {DateTime.UtcNow},
                        {job.ToAuditJson()}
                    );
                ");
                job.CompleteStep("audit-log", "Audit log entry written");

                job.StartStep("canonical-layout", "Re-seeding canonical layout and demo personas", 55);
                await dbContext.Database.ExecuteSqlRawAsync("""
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM pg_proc
                            WHERE proname = 'refresh_plantprocess_dashboard_read_models'
                        ) THEN
                            CALL refresh_plantprocess_dashboard_read_models();
                        END IF;
                    END $$;
                    """);
                job.CompleteStep("canonical-layout", "Canonical layout active and dashboard read models refreshed");

                job.StartStep("license-default", "Resetting demo license tier to default", 75);
                await dbContext.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS demo_runtime_settings
                    (
                        key text PRIMARY KEY,
                        value text NOT NULL,
                        updated_at_utc timestamptz NOT NULL DEFAULT NOW()
                    );

                    INSERT INTO demo_runtime_settings(key, value, updated_at_utc)
                    VALUES ('license.defaultTier', 'ProPlus', NOW())
                    ON CONFLICT (key)
                    DO UPDATE SET value = EXCLUDED.value, updated_at_utc = NOW();
                    """);
                job.CompleteStep("license-default", "Demo license default tier is ProPlus");

                job.StartStep("health-checks", "Verifying health and readiness state", 90);
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;");
                job.CompleteStep("health-checks", "Health checks verified");

                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE demo_lifecycle_reset_audit
                    SET status = {"Completed"},
                        details_json = {job.ToAuditJson()}
                    WHERE reset_job_id = {job.JobId};
                ");
            }

            job.MarkCompleted();
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PlantProcessDbContext>();
                await EnsureResetAuditTableAsync(dbContext, CancellationToken.None);

                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO demo_lifecycle_reset_audit
                    (
                        id,
                        reset_job_id,
                        reset_scope,
                        operator_name,
                        status,
                        created_at_utc,
                        details_json
                    )
                    VALUES
                    (
                        {Guid.NewGuid()},
                        {job.JobId},
                        {job.Scope},
                        {job.OperatorName},
                        {"Failed"},
                        {DateTime.UtcNow},
                        {job.ToAuditJson()}
                    );
                ");
            }
            catch
            {
                // Avoid hiding the original reset failure.
            }
        }
    }

    private static async Task CompleteStepAsync(
        DemoResetJobDto job,
        string code,
        string label,
        int delayMs)
    {
        job.StartStep(code, label, Math.Min(job.PercentComplete + 10, 95));
        await Task.Delay(delayMs);
        job.CompleteStep(code, label);
    }

    private static async Task EnsureResetAuditTableAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS demo_lifecycle_reset_audit
            (
                id uuid PRIMARY KEY,
                reset_job_id uuid NOT NULL,
                reset_scope text NOT NULL,
                operator_name text NOT NULL,
                status text NOT NULL,
                created_at_utc timestamptz NOT NULL DEFAULT NOW(),
                details_json jsonb NOT NULL DEFAULT '{}'::jsonb
            );

            CREATE INDEX IF NOT EXISTS ix_demo_lifecycle_reset_audit_job
            ON demo_lifecycle_reset_audit(reset_job_id, created_at_utc DESC);
            """, cancellationToken);
    }

    private static string NormalizeScope(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "data-only"
            : value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "data-only" => "data-only",
            "full" => "full",
            "identities-only" => "identities-only",
            _ => "data-only"
        };
    }

    private sealed record DemoResetRequest(string? Scope);

    private sealed class DemoResetJobDto
    {
        public Guid JobId { get; init; }
        public string Status { get; private set; } = "Queued";
        public string Scope { get; init; } = "data-only";
        public string OperatorName { get; init; } = "demo-operator";
        public int PercentComplete { get; private set; }
        public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; private set; }
        public string? FailureReason { get; private set; }
        public List<DemoResetStepDto> Steps { get; init; } = new();

        public static DemoResetJobDto Create(Guid jobId, string scope, string operatorName)
        {
            return new DemoResetJobDto
            {
                JobId = jobId,
                Scope = scope,
                OperatorName = operatorName,
                Steps =
                {
                    new DemoResetStepDto("stop-runtime", "Stopped transient demo activities", "Pending", 0, null),
                    new DemoResetStepDto("clean-runtime", "Cleaned generated runtime state", "Pending", 0, null),
                    new DemoResetStepDto("audit-log", "Writing reset audit log", "Pending", 0, null),
                    new DemoResetStepDto("canonical-layout", "Re-seeding canonical layout and demo personas", "Pending", 0, null),
                    new DemoResetStepDto("license-default", "Resetting demo license tier to default", "Pending", 0, null),
                    new DemoResetStepDto("health-checks", "Verifying health and readiness state", "Pending", 0, null)
                }
            };
        }

        public void MarkRunning()
        {
            Status = "Running";
            PercentComplete = 5;
        }

        public void MarkCompleted()
        {
            Status = "Completed";
            PercentComplete = 100;
            CompletedAtUtc = DateTime.UtcNow;
        }

        public void MarkFailed(string reason)
        {
            Status = "Failed";
            FailureReason = reason;

            var running = Steps.FirstOrDefault(x => x.Status == "Running");
            if (running is not null)
            {
                running.Status = "Failed";
                running.ExceptionDetail = reason;
            }
        }

        public void StartStep(string code, string label, int percent)
        {
            var step = Steps.FirstOrDefault(x => x.Code == code);
            if (step is null)
                return;

            step.Status = "Running";
            step.Label = label;
            step.PercentComplete = Math.Clamp(percent, 0, 100);
            PercentComplete = step.PercentComplete;
        }

        public void CompleteStep(string code, string label)
        {
            var step = Steps.FirstOrDefault(x => x.Code == code);
            if (step is null)
                return;

            step.Status = "Completed";
            step.Label = label;
            step.PercentComplete = 100;
            PercentComplete = Math.Min(PercentComplete + 10, 98);
        }

        public string ToAuditJson()
        {
            return JsonSerializer.Serialize(new
            {
                jobId = JobId,
                scope = Scope,
                operatorName = OperatorName,
                status = Status,
                percentComplete = PercentComplete,
                startedAtUtc = StartedAtUtc,
                completedAtUtc = CompletedAtUtc,
                failureReason = FailureReason,
                steps = Steps.Select(step => new
                {
                    step.Code,
                    step.Label,
                    step.Status,
                    step.PercentComplete,
                    step.ExceptionDetail
                }).ToArray()
            });
        }
    }

    private sealed class DemoResetStepDto
    {
        public DemoResetStepDto(
            string code,
            string label,
            string status,
            int percentComplete,
            string? exceptionDetail)
        {
            Code = code;
            Label = label;
            Status = status;
            PercentComplete = percentComplete;
            ExceptionDetail = exceptionDetail;
        }

        public string Code { get; init; }
        public string Label { get; set; }
        public string Status { get; set; }
        public int PercentComplete { get; set; }
        public string? ExceptionDetail { get; set; }
    }
}
