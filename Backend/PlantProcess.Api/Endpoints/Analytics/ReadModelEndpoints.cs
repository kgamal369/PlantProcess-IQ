using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>Read-only endpoints over the mv_dashboard_* materialized views (T-028).</summary>
public static class ReadModelEndpoints
{
    public static IEndpointRouteBuilder MapReadModelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics/read-models")
            .WithTags("Analytics Read Models")
            .RequireAuthorization();

        group.MapGet("/downtime-by-area", async (NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT site_id, area_id, area_code, area_name, downtime_event_count, downtime_minutes " +
                "FROM mv_dashboard_downtime_by_area ORDER BY downtime_minutes DESC", null, ct)));

        group.MapGet("/equipment-stoppage", async (NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT site_id, equipment_id, equipment_code, equipment_name, equipment_type, stoppage_count, stoppage_minutes " +
                "FROM mv_dashboard_equipment_stoppage ORDER BY stoppage_minutes DESC LIMIT 20", null, ct)));

        group.MapGet("/daily-production", async (NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT site_id, production_day_utc, product_family, material_count " +
                "FROM mv_dashboard_daily_production ORDER BY production_day_utc", null, ct)));

        group.MapGet("/defect-by-product-family", async (NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT site_id, product_family, event_day_utc, defect_count " +
                "FROM mv_dashboard_defect_by_product_family ORDER BY event_day_utc", null, ct)));

        group.MapGet("/parameter-by-grade", async (string? parameterCode, NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT site_id, parameter_code, parameter_name, unit_of_measure, grade_or_recipe, avg_value, sample_size " +
                "FROM mv_dashboard_parameter_by_grade " +
                (string.IsNullOrWhiteSpace(parameterCode) ? "" : "WHERE parameter_code = @p ") +
                "ORDER BY parameter_code, grade_or_recipe",
                string.IsNullOrWhiteSpace(parameterCode) ? null : ("p", (object)parameterCode!), ct)));

        group.MapGet("/parameter-trend", async (string? parameterCode, NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT parameter_code, parameter_name, unit_of_measure, parameter_category, observed_day_utc, avg_value, sample_size " +
                "FROM mv_dashboard_parameter_trend " +
                (string.IsNullOrWhiteSpace(parameterCode) ? "" : "WHERE parameter_code = @p ") +
                "ORDER BY parameter_code, observed_day_utc",
                string.IsNullOrWhiteSpace(parameterCode) ? null : ("p", (object)parameterCode!), ct)));

        group.MapGet("/staleness", async (NpgsqlDataSource ds, CancellationToken ct) =>
            Results.Ok(await QueryAsync(ds,
                "SELECT dataset, last_successful_refresh_utc, EXTRACT(EPOCH FROM age)::bigint AS age_seconds, is_stale " +
                "FROM v_read_model_staleness ORDER BY dataset", null, ct)));

        return app;
    }

    private static async Task<List<Dictionary<string, object?>>> QueryAsync(
        NpgsqlDataSource ds, string sql, (string Name, object Value)? p, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var cmd = ds.CreateCommand(sql);
        if (p.HasValue) cmd.Parameters.AddWithValue(p.Value.Name, p.Value.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}