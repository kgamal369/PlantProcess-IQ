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

// P6-01 â€” genealogy bidirectional walk ("golden thread"): proves ppiq_walk_genealogy resolves
// connected nodes, respects the depth bound, rejects bad directions, and that the graph-safety
// check is wired. DB-gated (inherits AuthenticatedApiTestBase).
public sealed class GenealogyGoldenThreadTests : AuthenticatedApiTestBase
{
    public GenealogyGoldenThreadTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static string Conn =>
        Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
        ?? throw new InvalidOperationException("PPIQ_TEST_CONNECTION_STRING not set.");

        private static async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(Conn);
        await c.OpenAsync();

        await using var tenantCmd = new NpgsqlCommand("""
SELECT set_config(
    'app.current_tenant',
    COALESCE(
        (SELECT id::text FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1),
        '00000000-0000-0000-0000-000000000001'
    ),
    false
)
""", c);
        await tenantCmd.ExecuteNonQueryAsync();

        return c;
    }

    private static IEnumerable<int> CollectDepths(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
            foreach (var p in el.EnumerateObject())
                if (p.NameEquals("depth") && p.Value.ValueKind == JsonValueKind.Number)
                    yield return p.Value.GetInt32();
                else
                    foreach (var d in CollectDepths(p.Value)) yield return d;
        else if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                foreach (var d in CollectDepths(item)) yield return d;
    }

    private static bool ContainsStringKey(JsonElement el, string key, string value)
    {
        if (el.ValueKind == JsonValueKind.Object)
            foreach (var p in el.EnumerateObject())
            {
                if (p.NameEquals(key) && p.Value.ValueKind == JsonValueKind.String &&
                    string.Equals(p.Value.GetString(), value, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (ContainsStringKey(p.Value, key, value)) return true;
            }
        else if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                if (ContainsStringKey(item, key, value)) return true;
        return false;
    }

    private static async Task<string> WalkAsync(NpgsqlConnection c, Guid tenant, string key, string dir, int depth)
    {
        await using var cmd = new NpgsqlCommand("SELECT public.ppiq_walk_genealogy(@t, @k, @d, @md)::text", c);
        cmd.Parameters.AddWithValue("t", tenant);
        cmd.Parameters.AddWithValue("k", key);
        cmd.Parameters.AddWithValue("d", dir);
        cmd.Parameters.AddWithValue("md", depth);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? "[]";
    }

    private static async Task<Guid?> FirstTenantAsync(NpgsqlConnection c)
    {
        await using var cmd = new NpgsqlCommand("SELECT id FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1", c);
        return (await cmd.ExecuteScalarAsync()) is Guid g ? g : (Guid?)null;
    }

    private static async Task<List<string>> MaterialKeysAsync(NpgsqlConnection c, Guid tenant, int take)
    {
        var keys = new List<string>();
        await using var cmd = new NpgsqlCommand(
            "SELECT material_key FROM public.canonical_material_units WHERE tenant_id=@t LIMIT @n", c);
        cmd.Parameters.AddWithValue("t", tenant);
        cmd.Parameters.AddWithValue("n", take);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) keys.Add(r.GetString(0));
        return keys;
    }

    [Fact]
    public async Task Walk_resolves_a_connected_bidirectional_thread_on_demo_data()
    {
        await using var c = await OpenAsync();
        var tenant = await FirstTenantAsync(c);
        Assert.True(tenant.HasValue, "No tenant seeded in the local DB.");

        var keys = await MaterialKeysAsync(c, tenant!.Value, 150);
        Assert.NotEmpty(keys); // demo data must seed canonical_material_units

        List<int>? connectedDepths = null;
        string? connectedKey = null;
        foreach (var k in keys)
        {
            using var doc = JsonDocument.Parse(await WalkAsync(c, tenant.Value, k, "both", 8));
            var depths = CollectDepths(doc.RootElement).ToList();
            if (depths.Count > 1) { connectedDepths = depths; connectedKey = k; break; }
        }

        Assert.True(connectedDepths is not null,
            "No demo material has connected genealogy edges; seed the genealogy spine to exercise the golden thread.");
        Assert.True(connectedDepths!.Count > 1, $"Walk of '{connectedKey}' should return more than the self node.");
        Assert.All(connectedDepths, d => Assert.InRange(d, 0, 8)); // bounded
        Assert.Contains(connectedDepths, d => d > 0);              // at least one traversed (non-self) node
    }

    [Fact]
    public async Task Walk_respects_depth_bound_and_rejects_invalid_direction()
    {
        await using var c = await OpenAsync();
        var tenant = await FirstTenantAsync(c);
        Assert.True(tenant.HasValue, "No tenant seeded in the local DB.");
        var key = (await MaterialKeysAsync(c, tenant!.Value, 1)).FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(key), "No material units seeded.");

        using (var doc = JsonDocument.Parse(await WalkAsync(c, tenant.Value, key!, "both", 1)))
            Assert.All(CollectDepths(doc.RootElement).ToList(), d => Assert.True(d <= 1, "Depth bound (1) must be respected."));

        using (var doc = JsonDocument.Parse(await WalkAsync(c, tenant.Value, key!, "sideways", 8)))
            Assert.True(ContainsStringKey(doc.RootElement, "errorCode", "InvalidDirection"),
                "An invalid direction must yield an InvalidDirection error code.");
    }

    [Fact]
    public async Task Genealogy_graph_safety_check_is_callable()
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM public.ppiq_validate_genealogy_graph()", c);
        await using var r = await cmd.ExecuteReaderAsync();
        var rows = 0;
        while (await r.ReadAsync()) rows++;
        Assert.True(rows >= 0); // cycle/orphan diagnostics are wired and callable
    }
}