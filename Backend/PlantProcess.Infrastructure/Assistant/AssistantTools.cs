using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>T-052 tool: fetch a single risk finding (tenant-checked); result carries a Finding handle.</summary>
public sealed class FetchFindingTool : ITool
{
    private readonly NpgsqlDataSource _ds;
    public FetchFindingTool(NpgsqlDataSource ds) => _ds = ds;
    public string Name => "fetch_finding";
    public string RequiredRole => "viewer";
    public string? RequiredLicense => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!args.TryGetValue("id", out var raw) || !Guid.TryParse(raw, out var id))
            return ToolResult.Refused(Name, "bad_args");

        await using var cmd = _ds.CreateCommand(
            "SELECT rs.id, rs.risk_type, rs.score, rs.risk_class FROM ppiq_plant.risk_scores rs " +
            "JOIN ppiq_plant.material_units mu ON mu.id = rs.material_unit_id " +
            "WHERE rs.id = @id AND COALESCE(mu.is_synthetic,false) = false");
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ToolResult.Refused(Name, "not_found");

        var payload = JsonSerializer.Serialize(new
        {
            id = reader.GetGuid(0),
            riskType = reader.GetString(1),
            score = reader.GetDecimal(2),
            riskClass = reader.IsDBNull(3) ? null : reader.GetString(3)
        });
        return new ToolResult(true, Name, payload, new[] { ProvenanceHandle.Finding(id.ToString(), "risk-score") }, null);
    }
}

/// <summary>T-052 tool: open a suggestion (tenant-checked); result carries the suggestion handle.</summary>
public sealed class OpenSuggestionTool : ITool
{
    private readonly NpgsqlDataSource _ds;
    public OpenSuggestionTool(NpgsqlDataSource ds) => _ds = ds;
    public string Name => "open_suggestion";
    public string RequiredRole => "viewer";
    public string? RequiredLicense => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!args.TryGetValue("id", out var raw) || !Guid.TryParse(raw, out var id))
            return ToolResult.Refused(Name, "bad_args");

        await using var cmd = _ds.CreateCommand(
            "SELECT id, title, status, confidence FROM ppiq_plant.suggestion WHERE id=@id AND tenant_id=@t");
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("t", ctx.TenantId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return ToolResult.Refused(Name, "not_found");

        var payload = JsonSerializer.Serialize(new
        {
            id = reader.GetGuid(0),
            title = reader.GetString(1),
            status = reader.GetString(2),
            confidence = reader.GetDecimal(3)
        });
        return new ToolResult(true, Name, payload, new[] { ProvenanceHandle.Finding(id.ToString(), "suggestion") }, null);
    }
}

/// <summary>T-052 tool: run a simple count KPI (tenant-checked); result carries a Dataset handle.</summary>
public sealed class RunKpiTool : ITool
{
    private readonly NpgsqlDataSource _ds;
    public RunKpiTool(NpgsqlDataSource ds) => _ds = ds;
    public string Name => "run_kpi";
    public string RequiredRole => "operator";
    public string? RequiredLicense => null;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(
            "SELECT COUNT(*) FROM public.canonical_material_units WHERE tenant_id = @t");
        cmd.Parameters.AddWithValue("t", ctx.TenantId);
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        var payload = JsonSerializer.Serialize(new { kpi = "material_unit_count", value = count });
        return new ToolResult(true, Name, payload, new[] { ProvenanceHandle.Dataset("canonical_material_units", $"tenant:{ctx.TenantId}") }, null);
    }
}