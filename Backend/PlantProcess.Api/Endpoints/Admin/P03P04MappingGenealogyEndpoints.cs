using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

public static class P03P04MappingGenealogyEndpoints
{
    public static IEndpointRouteBuilder MapP03P04MappingGenealogyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/p03p04")
            .WithTags("Admin - P03/P04 Mapping & Genealogy")
            .AllowAnonymous();

        group.MapGet("/readiness", async (
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var hasBusinessKeys = await dbContext.Database
                .SqlQueryRaw<bool>("SELECT to_regclass('public.ppiq_business_key_definitions') IS NOT NULL")
                .FirstAsync(cancellationToken);

            var hasMappingVersions = await dbContext.Database
                .SqlQueryRaw<bool>("SELECT to_regclass('public.ppiq_mapping_versions') IS NOT NULL")
                .FirstAsync(cancellationToken);

            var hasCanonicalMaterials = await dbContext.Database
                .SqlQueryRaw<bool>("SELECT to_regclass('public.canonical_material_units') IS NOT NULL")
                .FirstAsync(cancellationToken);

            var hasGenealogy = await dbContext.Database
                .SqlQueryRaw<bool>("SELECT to_regclass('public.canonical_genealogy_edges') IS NOT NULL")
                .FirstAsync(cancellationToken);

            return Results.Ok(new
            {
                generatedAtUtc = DateTimeOffset.UtcNow,
                hasBusinessKeys,
                hasMappingVersions,
                hasCanonicalMaterials,
                hasGenealogy
            });
        });

        return app;
    }
}