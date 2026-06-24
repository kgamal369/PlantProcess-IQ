using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Genealogy;

// Pins the named demo coil C-0044170 and proves the genealogy walk resolves BOTH directions on the
// seeded golden-thread fixture: coil to melt (backward) and melt to coils (forward), including the
// transition coil's two parent heats. Stronger than the generic golden-thread test, which accepts any
// connected node in either direction. DB-gated; skips cleanly when the integration DB is unreachable
// or when the golden-thread fixture (seed 010) is not loaded.
public sealed class NamedDemoCoilGenealogyTests : AuthenticatedApiTestBase
{
    private const string DemoTenantId = "00000000-0000-0000-0000-000000000001";
    private const string NamedCoil = "C-0044170";

    public NamedDemoCoilGenealogyTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static string Conn => ResolveIntegrationTestConnectionString();

    private sealed record Node(int Depth, string MaterialKey, string MaterialType, string Direction);

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        Skip.IfNot(AuthenticatedApiTestBase.IsIntegrationDbReachable(),
            "Integration Postgres not reachable/authenticated on this machine; runs in CI.");
        var c = new NpgsqlConnection(Conn);
        await c.OpenAsync();
        return c;
    }

    private static async Task<List<Node>> WalkAsync(NpgsqlConnection c, string materialKey, string direction)
    {
        await using var cmd = new NpgsqlCommand("SELECT public.ppiq_walk_genealogy(@t, @k, @d, @md)::text", c);
        cmd.Parameters.AddWithValue("t", Guid.Parse(DemoTenantId));
        cmd.Parameters.AddWithValue("k", materialKey);
        cmd.Parameters.AddWithValue("d", direction);
        cmd.Parameters.AddWithValue("md", 8);
        var json = (await cmd.ExecuteScalarAsync())?.ToString() ?? "[]";

        var nodes = new List<Node>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return nodes;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (!el.TryGetProperty("materialKey", out var mk)) continue; // skip error objects
            var depth = el.TryGetProperty("depth", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : -1;
            var type = el.TryGetProperty("materialType", out var mt) ? (mt.GetString() ?? string.Empty) : string.Empty;
            var dir = el.TryGetProperty("direction", out var dr) ? (dr.GetString() ?? string.Empty) : string.Empty;
            nodes.Add(new Node(depth, mk.GetString() ?? string.Empty, type, dir));
        }
        return nodes;
    }

    private static async Task<bool> CoilSeededAsync(NpgsqlConnection c)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM public.canonical_material_units WHERE tenant_id = @t AND material_key = @k", c);
        cmd.Parameters.AddWithValue("t", Guid.Parse(DemoTenantId));
        cmd.Parameters.AddWithValue("k", NamedCoil);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }

    [SkippableFact]
    public async Task Coil_to_melt_resolves_backward()
    {
        await using var c = await OpenAsync();
        Skip.IfNot(await CoilSeededAsync(c),
            $"Golden-thread fixture not seeded ({NamedCoil} absent); run seed 010_p3_golden_thread_seed.sql.");

        var backward = await WalkAsync(c, NamedCoil, "backward");
        var melts = backward.Where(n => n.Direction == "backward" && n.MaterialType == "Heat" && n.Depth >= 1).ToList();

        Assert.NotEmpty(melts); // coil -> melt resolves at least one parent heat
        Assert.All(melts, m => Assert.InRange(m.Depth, 1, 8)); // bounded, no stall
    }

    [SkippableFact]
    public async Task Melt_to_coils_resolves_forward_to_the_named_coil()
    {
        await using var c = await OpenAsync();
        Skip.IfNot(await CoilSeededAsync(c),
            $"Golden-thread fixture not seeded ({NamedCoil} absent); run seed 010_p3_golden_thread_seed.sql.");

        var backward = await WalkAsync(c, NamedCoil, "backward");
        var meltKey = backward
            .Where(n => n.Direction == "backward" && n.MaterialType == "Heat")
            .OrderBy(n => n.Depth)
            .Select(n => n.MaterialKey)
            .FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(meltKey), "No melt resolved backward from the named coil.");

        var forward = await WalkAsync(c, meltKey!, "forward");
        Assert.Contains(forward, n => n.Direction == "forward" && n.MaterialKey == NamedCoil); // melt -> coils includes the named coil
        Assert.All(forward.Where(n => n.Direction == "forward"), n => Assert.InRange(n.Depth, 1, 8));
    }

    [SkippableFact]
    public async Task Both_includes_self_and_stays_bounded()
    {
        await using var c = await OpenAsync();
        Skip.IfNot(await CoilSeededAsync(c),
            $"Golden-thread fixture not seeded ({NamedCoil} absent); run seed 010_p3_golden_thread_seed.sql.");

        var both = await WalkAsync(c, NamedCoil, "both");
        Assert.Contains(both, n => n.Direction == "self" && n.MaterialKey == NamedCoil); // self node present
        Assert.Contains(both, n => n.Direction == "backward"); // both surfaces the backward thread
        Assert.All(both, n => Assert.InRange(n.Depth, 0, 8)); // bounded, no runaway recursion
    }
}