using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace PlantProcess.Api.Endpoints.MappingHealth;

public static class Phase34MappingHealthEndpoints
{
    public static IEndpointRouteBuilder MapPhase34MappingHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/mapping-health").WithTags("Phase 3/4 Mapping Health").RequireAuthorization();

        group.MapGet("/status", () => Results.Ok(new
        {
            status = "available",
            capability = "phase3-phase4-mapping-health",
            principle = "read-only",
            note = "Reports source-schema snapshot, mapping coverage and drift state. Does not write to MES, SCADA, L2, PLC or source systems."
        })).WithName("GetPhase34MappingHealthStatus");

        group.MapGet("/summary", GetSummaryAsync).WithName("GetPhase34MappingHealthSummary");
        return app;
    }

    private static async Task<IResult> GetSummaryAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var viewExists = await ScalarBoolAsync(dataSource, "SELECT to_regclass('public.ppiq_v_phase34_mapping_health_summary') IS NOT NULL;", cancellationToken);
        if (!viewExists)
        {
            return Results.Ok(new MappingHealthSummaryDto("NotConfigured", "Database script 430_phase3_phase4_certification_mapping_health.sql has not been applied yet.", Array.Empty<MappingHealthSourceDto>()));
        }

        const string sql = "SELECT source_system_code, source_kind, total_field_count, mapped_field_count, unmapped_required_count, drift_event_count, has_blocking_drift, health_status, last_snapshot_at_utc FROM public.ppiq_v_phase34_mapping_health_summary ORDER BY CASE health_status WHEN 'Blocked' THEN 1 WHEN 'NeedsMapping' THEN 2 WHEN 'Warning' THEN 3 WHEN 'NoFields' THEN 4 ELSE 5 END, source_system_code;";
        var rows = new List<MappingHealthSourceDto>();
        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MappingHealthSourceDto(
                reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetBoolean(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8).ToString("O")));
        }

        var status = rows.Any(x => x.HasBlockingDrift) ? "Blocked" : rows.Any(x => x.UnmappedRequiredCount > 0) ? "NeedsMapping" : rows.Any(x => x.DriftEventCount > 0) ? "Warning" : rows.Count == 0 ? "NoSnapshots" : "Healthy";
        return Results.Ok(new MappingHealthSummaryDto(status, "Computed live from ppiq_v_phase34_mapping_health_summary.", rows));
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlDataSource dataSource, string sql, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool b && b;
    }
}

public sealed record MappingHealthSummaryDto(string Status, string Evidence, IReadOnlyList<MappingHealthSourceDto> Sources);
public sealed record MappingHealthSourceDto(string SourceSystemCode, string SourceKind, long TotalFieldCount, long MappedFieldCount, long UnmappedRequiredCount, long DriftEventCount, bool HasBlockingDrift, string HealthStatus, string? LastSnapshotAtUtc);