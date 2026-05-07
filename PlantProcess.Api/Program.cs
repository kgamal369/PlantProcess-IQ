using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    service = "PlantProcess IQ API",
    status = "Healthy",
    utc = DateTime.UtcNow
}));

app.MapGet("/db-health", async (PlantProcessDbContext dbContext) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync();

    return Results.Ok(new
    {
        database = "plantprocessiq",
        canConnect,
        utc = DateTime.UtcNow
    });
});

app.MapGet("/materials", async (PlantProcessDbContext dbContext) =>
{
    var materials = await dbContext.MaterialUnits
        .AsNoTracking()
        .OrderBy(x => x.MaterialCode)
        .Take(50)
        .Select(x => new
        {
            x.Id,
            x.MaterialCode,
            x.MaterialUnitType,
            x.ProductFamily,
            x.GradeOrRecipe,
            x.ProductionStartUtc,
            x.ProductionEndUtc,
            x.IsSynthetic
        })
        .ToListAsync();

    return Results.Ok(materials);
});

app.MapDevSeedEndpoints();

app.Run();
