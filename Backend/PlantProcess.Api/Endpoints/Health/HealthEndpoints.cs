// ============================================================
// PHASE 0 — TASK 3 / TASK 4
// FILE: Backend/PlantProcess.Api/Endpoints/Health/HealthEndpoints.cs
//
// PURPOSE:
//  1. /health       : API liveness.
//  2. /db-health    : DB connectivity + EF migration alignment.
//  3. /health/ready : combined readiness gate for smoke tests / CI.
//
// WHY:
//  Phase 0 must prove that the API is alive, DB is reachable,
//  and the EF schema is aligned before Phase 1 dashboard validation.
// ============================================================

using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;
using System.Diagnostics;

namespace PlantProcess.Api.Endpoints.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("")
            .WithTags("Health");

        group.MapGet("/health", () => Results.Ok(new
        {
            service = "PlantProcess IQ API",
            status = "Healthy",
            utc = DateTime.UtcNow
        }))
        .WithName("GetHealth")
        .WithSummary("API liveness check.")
        .WithDescription("Returns 200 if the API process is running.");

        group.MapGet("/db-health", GetDbHealthAsync)
            .WithName("GetDbHealth")
            .WithSummary("Database connectivity and migration-state check.")
            .WithDescription(
                "Returns 200 only when the DB is reachable and fully migrated. " +
                "Returns 503 when connection fails or migrations are pending.");

        group.MapGet("/health/ready", GetReadinessAsync)
            .WithName("GetReadiness")
            .WithSummary("Combined readiness gate.")
            .WithDescription(
                "Returns 200 only when API + DB + migrations are ready. " +
                "This endpoint is intended for smoke tests, CI and demo readiness.");

        return app;
    }

    private static async Task<IResult> GetDbHealthAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return Results.Json(
                    new DbHealthResponse(
                        Database: "plantprocessiq",
                        CanConnect: false,
                        PendingMigrations: Array.Empty<string>(),
                        AppliedCount: 0,
                        PingMs: sw.ElapsedMilliseconds,
                        Status: "Unhealthy",
                        Detail: "CanConnectAsync returned false. Check PostgreSQL status and connection string.",
                        Utc: DateTime.UtcNow),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();

            var appliedCount = (await dbContext.Database
                    .GetAppliedMigrationsAsync(cancellationToken))
                .Count();

            sw.Stop();

            if (pendingMigrations.Length > 0)
            {
                return Results.Json(
                    new DbHealthResponse(
                        Database: "plantprocessiq",
                        CanConnect: true,
                        PendingMigrations: pendingMigrations,
                        AppliedCount: appliedCount,
                        PingMs: sw.ElapsedMilliseconds,
                        Status: "MigrationPending",
                        Detail:
                            $"{pendingMigrations.Length} migration(s) pending. " +
                            "Run: dotnet ef database update --project PlantProcess.Infrastructure --startup-project PlantProcess.Api",
                        Utc: DateTime.UtcNow),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var status = sw.ElapsedMilliseconds > 2_000
                ? "Degraded"
                : "Healthy";

            var detail = sw.ElapsedMilliseconds > 2_000
                ? $"Database ping took {sw.ElapsedMilliseconds} ms. Investigate DB load, network, or connection pooling."
                : null;

            return Results.Ok(new DbHealthResponse(
                Database: "plantprocessiq",
                CanConnect: true,
                PendingMigrations: Array.Empty<string>(),
                AppliedCount: appliedCount,
                PingMs: sw.ElapsedMilliseconds,
                Status: status,
                Detail: detail,
                Utc: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            sw.Stop();

            return Results.Json(
                new DbHealthResponse(
                    Database: "plantprocessiq",
                    CanConnect: false,
                    PendingMigrations: Array.Empty<string>(),
                    AppliedCount: 0,
                    PingMs: sw.ElapsedMilliseconds,
                    Status: "Unhealthy",
                    Detail: ex.Message,
                    Utc: DateTime.UtcNow),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> GetReadinessAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var checks = new List<ReadinessCheck>();
        var ready = true;

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            checks.Add(new ReadinessCheck(
                Name: "database_connectivity",
                Result: canConnect ? "pass" : "fail",
                Detail: canConnect ? null : "Database connection failed."));

            if (!canConnect)
                ready = false;
        }
        catch (Exception ex)
        {
            checks.Add(new ReadinessCheck(
                Name: "database_connectivity",
                Result: "fail",
                Detail: ex.Message));

            ready = false;
        }

        if (ready)
        {
            try
            {
                var pendingMigrations = (await dbContext.Database
                        .GetPendingMigrationsAsync(cancellationToken))
                    .ToArray();

                var hasPending = pendingMigrations.Length > 0;

                checks.Add(new ReadinessCheck(
                    Name: "database_migrations",
                    Result: hasPending ? "fail" : "pass",
                    Detail: hasPending
                        ? $"{pendingMigrations.Length} pending migration(s): {string.Join(", ", pendingMigrations)}"
                        : null));

                if (hasPending)
                    ready = false;
            }
            catch (Exception ex)
            {
                checks.Add(new ReadinessCheck(
                    Name: "database_migrations",
                    Result: "warn",
                    Detail: $"Could not read migration state: {ex.Message}"));
            }
        }

        sw.Stop();

        var response = new ReadinessResponse(
            Ready: ready,
            Status: ready ? "Ready" : "NotReady",
            Checks: checks,
            TotalMs: sw.ElapsedMilliseconds,
            Utc: DateTime.UtcNow);

        return ready
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private sealed record DbHealthResponse(
        string Database,
        bool CanConnect,
        string[] PendingMigrations,
        int AppliedCount,
        long PingMs,
        string Status,
        string? Detail,
        DateTime Utc);

    private sealed record ReadinessCheck(
        string Name,
        string Result,
        string? Detail);

    private sealed record ReadinessResponse(
        bool Ready,
        string Status,
        IReadOnlyList<ReadinessCheck> Checks,
        long TotalMs,
        DateTime Utc);
}
