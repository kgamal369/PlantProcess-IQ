using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using PlantProcess.Analytics.Core.Primitives;

namespace PlantProcess.Api.Endpoints.Analytics;

public sealed record SimpleAnalysisRequest(string Primitive, string DatasetKey, string? ParameterCode, IReadOnlyList<string>? Filters);

/// <summary>T-026: exposes the v4 6.1 transparent primitives over allowlisted canonical datasets.</summary>
public static class SimpleAnalysisEndpoints
{
    private static readonly string[] KnownPrimitives =
        { "count", "sum", "average", "min", "max", "median", "stddev", "trend", "distribution" };

    public static IEndpointRouteBuilder MapSimpleAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/simple")
            .WithTags("Analytics Simple")
            .RequireAuthorization();

        group.MapGet("/primitives", () => Results.Ok(KnownPrimitives));
        group.MapGet("/datasets", () => Results.Ok(new[] { "parameter_values", "downtime_minutes", "defect_count_daily", "daily_production" }));

        group.MapPost("/run", async (SimpleAnalysisRequest req, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            var primitive = (req.Primitive ?? "").Trim().ToLowerInvariant();
            if (!KnownPrimitives.Contains(primitive))
                return Results.BadRequest(new { error = "unknown_primitive", primitive = req.Primitive, supported = KnownPrimitives });

            var resolved = ResolveDataset(req.DatasetKey, req.ParameterCode);
            if (resolved is null)
                return Results.BadRequest(new { error = "unknown_dataset", datasetKey = req.DatasetKey });

            var values = await LoadSeriesAsync(ds, resolved.Value.Sql, resolved.Value.Param, ct);

            var filters = req.Filters
                ?? (string.IsNullOrWhiteSpace(req.ParameterCode) ? Array.Empty<string>() : new[] { $"parameter_code = {req.ParameterCode}" });
            var ctx = new AnalysisContext(resolved.Value.Label, filters, "all-time", DateTimeOffset.UtcNow, null);

            AnalysisResult result = primitive switch
            {
                "count"        => SimpleAnalysis.Count(values, ctx),
                "sum"          => SimpleAnalysis.SumOf(values, ctx),
                "average"      => SimpleAnalysis.Average(values, ctx),
                "min"          => SimpleAnalysis.Min(values, ctx),
                "max"          => SimpleAnalysis.Max(values, ctx),
                "median"       => SimpleAnalysis.MedianOf(values, ctx),
                "stddev"       => SimpleAnalysis.StdDev(values, ctx),
                "trend"        => SimpleAnalysis.Trend(values, ctx),
                "distribution" => SimpleAnalysis.Distribution(values, ctx),
                _              => SimpleAnalysis.Count(values, ctx)
            };

            return Results.Ok(new
            {
                primitive = result.Primitive,
                value = result.Value,
                status = result.Status.ToString(),
                label = result.Label,
                message = result.Message,
                extras = result.Extras,
                metadata = new
                {
                    formula = result.Metadata.Formula,
                    dataset = result.Metadata.Dataset,
                    filters = result.Metadata.Filters,
                    timeWindow = result.Metadata.TimeWindow,
                    refreshedAtUtc = result.Metadata.RefreshedAtUtc,
                    sampleSize = result.Metadata.SampleSize,
                    unit = result.Metadata.Unit
                },
                metadataComplete = result.MetadataComplete
            });
        });

        return app;
    }

    private static (string Sql, (string, object)? Param, string Label)? ResolveDataset(string? key, string? parameterCode)
        => (key ?? "").Trim().ToLowerInvariant() switch
        {
            "parameter_values" => (
                string.IsNullOrWhiteSpace(parameterCode)
                    ? "SELECT po.numeric_value FROM parameter_observations po WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL"
                    : "SELECT po.numeric_value FROM parameter_observations po " +
                      "JOIN parameter_definitions pd ON pd.id = po.parameter_definition_id " +
                      "WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL AND pd.parameter_code = @p",
                string.IsNullOrWhiteSpace(parameterCode) ? ((string, object)?)null : ("p", (object)parameterCode!),
                string.IsNullOrWhiteSpace(parameterCode) ? "parameter_observations.numeric_value" : $"parameter_observations.numeric_value[{parameterCode}]"),
            "downtime_minutes" => (
                "SELECT EXTRACT(EPOCH FROM (ended_at_utc - started_at_utc)) / 60.0 FROM downtime_events " +
                "WHERE is_deleted = false AND ended_at_utc IS NOT NULL",
                null, "downtime_events.minutes"),
            "defect_count_daily" => (
                "SELECT defect_count::double precision FROM mv_dashboard_defect_by_product_family",
                null, "mv_dashboard_defect_by_product_family.defect_count"),
            "daily_production" => (
                "SELECT material_count::double precision FROM mv_dashboard_daily_production",
                null, "mv_dashboard_daily_production.material_count"),
            _ => null
        };

    private static async Task<List<double?>> LoadSeriesAsync(NpgsqlDataSource ds, string sql, (string, object)? p, CancellationToken ct)
    {
        var values = new List<double?>();
        await using var cmd = ds.CreateCommand(sql);
        if (p.HasValue) cmd.Parameters.AddWithValue(p.Value.Item1, p.Value.Item2);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            values.Add(await reader.IsDBNullAsync(0, ct) ? (double?)null : Convert.ToDouble(reader.GetValue(0)));
        return values;
    }
}