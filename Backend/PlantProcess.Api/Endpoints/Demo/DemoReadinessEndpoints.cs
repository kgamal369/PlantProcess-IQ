using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Demo.Readiness;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Demo;

public static class DemoReadinessEndpoints
{
    public static IEndpointRouteBuilder MapDemoReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/demo-readiness")
            .WithTags("Admin - Demo Readiness")
            .RequireAuthorization();

        group.MapGet("/", EvaluateAsync)
            .WithName("GetDemoReadiness")
            .WithSummary("PPIQ-103 one-click readiness check")
            .Produces<DemoReadinessReport>();

        return app;
    }

    private static async Task<IResult> EvaluateAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var sourcesLinked = await db.ConnectionProfiles.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken);
        var stagingPopulated = await db.StagingRecords.AsNoTracking().AnyAsync(cancellationToken);
        var mappingsPublished = await db.MappingDefinitions.AsNoTracking().AnyAsync(x => x.IsActive, cancellationToken);
        var jobsRunnable = await db.JobDefinitions.AsNoTracking().CountAsync(x => x.IsEnabled, cancellationToken);
        var demoPages = await CountActiveDemoPagesAsync(db, cancellationToken);

        var inputs = new DemoReadinessInputs(
            SourcesLinked: sourcesLinked,
            SourcesExpected: 8,
            StagingPopulated: stagingPopulated,
            MappingsPublished: mappingsPublished,
            JobsRunnable: jobsRunnable,
            JobsExpected: 4,
            DemoPagesPresent: demoPages > 0);

        return Results.Ok(DemoReadinessEvaluator.Evaluate(inputs));
    }

    private static async Task<int> CountActiveDemoPagesAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT CASE
              WHEN to_regclass('ppiq_meta.page_definitions') IS NULL THEN 0
              ELSE (SELECT COUNT(*)::integer FROM page_definitions WHERE is_deleted = false)
            END;
            """, connection);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}