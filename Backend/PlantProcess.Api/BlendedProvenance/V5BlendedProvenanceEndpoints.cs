using Npgsql;

namespace PlantProcess.Api.BlendedProvenance;

public static class V5BlendedProvenanceEndpoints
{
    public static IEndpointRouteBuilder MapV5BlendedProvenanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/blended-provenance")
            .WithTags("V5 Blended Provenance");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-blended-provenance",
            weightedGenealogy = true,
            transitionBadge = true,
            blendedAttribution = true,
            driftDetection = true,
            defectCatalogHarmonization = true
        }));

        group.MapGet("/child/{childMaterialUnitId:guid}/attribution", async (
            Guid childMaterialUnitId,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    parent_material_unit_id,
                    child_material_unit_id,
                    contribution_weight,
                    is_transition,
                    provenance_confidence,
                    evidence
                FROM public.ppiq_v5_blended_attribution_for_child(@child_material_unit_id)
                """;
            cmd.Parameters.AddWithValue("child_material_unit_id", childMaterialUnitId);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    parentMaterialUnitId = reader.GetGuid(0),
                    childMaterialUnitId = reader.GetGuid(1),
                    contributionWeight = reader.GetDecimal(2),
                    contributionPercent = decimal.Round(reader.GetDecimal(2) * 100m, 2),
                    isTransition = reader.GetBoolean(3),
                    provenanceConfidence = reader.GetDecimal(4),
                    evidence = reader.GetString(5)
                });
            }

            return Results.Ok(new
            {
                childMaterialUnitId,
                hasTransition = rows.Any(),
                parentContributions = rows
            });
        });

        group.MapGet("/weights/status", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    child_material_unit_id,
                    parent_count,
                    contribution_sum,
                    has_transition,
                    min_confidence,
                    is_green
                FROM public.ppiq_v5_child_weight_status
                ORDER BY has_transition DESC, parent_count DESC
                LIMIT 200
                """;

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    childMaterialUnitId = reader.GetGuid(0),
                    parentCount = reader.GetInt64(1),
                    contributionSum = reader.GetDecimal(2),
                    hasTransition = reader.GetBoolean(3),
                    minConfidence = reader.GetDecimal(4),
                    isGreen = reader.GetBoolean(5)
                });
            }

            return Results.Ok(rows);
        });

        return app;
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        return PlantProcess.Api.Security.TenantClaimReader.ResolveRequiredTenantId(http);
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        cmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}