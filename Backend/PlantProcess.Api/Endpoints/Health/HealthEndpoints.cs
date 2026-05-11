using PlantProcess.Infrastructure.Persistence;

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
        }));

        group.MapGet("/db-health", async (
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return Results.Ok(new
            {
                database = "plantprocessiq",
                canConnect,
                utc = DateTime.UtcNow
            });
        });

        return app;
    }
}