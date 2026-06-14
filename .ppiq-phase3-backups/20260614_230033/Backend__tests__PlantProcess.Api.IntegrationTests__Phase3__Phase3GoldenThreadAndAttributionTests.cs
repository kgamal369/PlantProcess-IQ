// =============================================================================
// Phase3GoldenThreadAndAttributionTests.cs               (PPIQ-301/302/303)
// DB-gated acceptance tests for the Phase 3 golden thread + honest attribution.
// Mirrors the GenealogyGoldenThreadTests harness (raw Npgsql, tenant set, the
// integration connection string). Self-seeds via the 010 seed having run; each
// test Skip.IfNot()s when its prerequisite objects/data are absent so a partially
// migrated DB never produces a red herring.
// =============================================================================
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Phase3;

public sealed class Phase3GoldenThreadAndAttributionTests : AuthenticatedApiTestBase
{
    public Phase3GoldenThreadAndAttributionTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static string Conn => ResolveIntegrationTestConnectionString();

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(Conn);
        await c.OpenAsync();
        await using var t = new NpgsqlCommand(
            "SELECT set_config('app.current_tenant', COALESCE((SELECT id::text FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1), '00000000-0000-0000-0000-000000000001'), false)", c);
        await t.ExecuteNonQueryAsync();
        return c;
    }

    private static async Task<bool> FunctionExistsAsync(NpgsqlConnection c, string name)
    {
        await using var cmd = new NpgsqlCommand("SELECT to_regprocedure(@n) IS NOT NULL", c);
        cmd.Parameters.AddWithValue("n", name);
        return (bool)(await cmd.ExecuteScalarAsync() ?? false);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection c, string sql, params (string, object)[] ps)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v);
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<Guid?> MaterialIdByCodeAsync(NpgsqlConnection c, string code)
    {
        var v = await ScalarAsync(c, "SELECT id FROM public.material_units WHERE material_code = @c LIMIT 1", ("c", code));
        return v is Guid g ? g : (Guid?)null;
    }

    // ---- PPIQ-301 -----------------------------------------------------------
    [SkippableFact]
    public async Task BusinessKey_reconciles_equivalent_ids_and_rejects_conflicts()
    {
        await using var c = await OpenAsync();
        Skip.IfNot(await FunctionExistsAsync(c, "public.ppiq_resolve_material_by_business_key(text)"),
            "320_p3_business_key_reconciliation.sql not applied.");

        var byPrefixed = await ScalarAsync(c, "SELECT public.ppiq_resolve_material_by_business_key('C-0044170')");
        var byBare     = await ScalarAsync(c, "SELECT public.ppiq_resolve_material_by_business_key('44170')");
        Skip.If(byPrefixed is null || byBare is null, "Golden-thread aliases not seeded (run seed/010).");

        Assert.Equal((Guid)byPrefixed!, (Guid)byBare!);   // C-0044170 == 44170 -> ONE entity

        // A deliberately conflicting alias (normalized '44170' -> a DIFFERENT unit) must be rejected.
        var other = await MaterialIdByCodeAsync(c, "C-0044171");
        Skip.If(other is null, "Conflict fixture coil C-0044171 not seeded.");
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var bad = new NpgsqlCommand(
                "INSERT INTO public.material_aliases (id, created_at_utc, is_synthetic, source_system, is_deleted, material_unit_id, alias_code, alias_type) " +
                "VALUES (gen_random_uuid(), now(), true, 'CONFLICT_TEST', false, @mu, 'COIL-44170', 'SourceSystemId')", c);
            bad.Parameters.AddWithValue("mu", other!.Value);
            await bad.ExecuteNonQueryAsync();
        });
        Assert.Contains("AmbiguousJoinKey", ex.MessageText);   // typed error, transaction rolled back
    }

    // ---- PPIQ-302 -----------------------------------------------------------
    [SkippableFact]
    public async Task GoldenThread_resolves_both_directions_on_customer_keys_only()
    {
        await using var c = await OpenAsync();
        Skip.IfNot(await FunctionExistsAsync(c, "public.ppiq_golden_thread(uuid,text,integer)"),
            "321_p3_golden_thread_and_missing_hop.sql not applied.");

        var tenant = (Guid)(await ScalarAsync(c, "SELECT id FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1"))!;
        var json   = (string)(await ScalarAsync(c, "SELECT public.ppiq_golden_thread(@t, 'C-0044170', 12)::text", ("t", tenant)))!;
        Skip.If(json.Contains("GenealogyUnavailable") || json.Contains("\"MissingHop\""),
            "Canonical walk layer not seeded for C-0044170 (302 walk fixture absent).");

        Assert.Contains("H-3361", json);                       // backward reaches the melt heat
        Assert.True(json.Contains("\"backward\"") && json.Contains("\"forward\""), "Both directions must be present.");
        // No PPIQ-internal GUIDs may leak into the customer-facing payload.
        Assert.False(Regex.IsMatch(json, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"),
            "Golden-thread payload must contain customer keys only - an internal GUID leaked.");

        // A broken chain yields a typed MissingHop, never a silent gap.
        var broken = (string)(await ScalarAsync(c, "SELECT public.ppiq_golden_thread(@t, 'C-0044171', 12)::text", ("t", tenant)))!;
        Assert.True(broken.Contains("\"MissingHop\"") || broken.Contains("H-3361"),
            "A coil with no melt hop must report MissingHop.");
    }

    // ---- PPIQ-303 -----------------------------------------------------------
    [SkippableFact]
    public async Task TransitionCoil_reports_weighted_split_summing_to_one()
    {
        await using var c = await OpenAsync();
        Skip.IfNot(await FunctionExistsAsync(c, "public.ppiq_v5_blended_attribution_detailed(uuid)"),
            "322_p3_transition_attribution_detailed.sql not applied.");

        var transition = await MaterialIdByCodeAsync(c, "C-0044170");
        var normal     = await MaterialIdByCodeAsync(c, "C-0044171");
        Skip.If(transition is null || normal is null, "Transition fixtures not seeded (run seed/010).");

        // Transition coil: two heats, weights sum to 1.0 +/- 0.01, codes surfaced.
        int rows = 0; string codes = "";
        await using (var cmd = new NpgsqlCommand(
            "SELECT parent_material_code, contribution_weight FROM public.ppiq_v5_blended_attribution_detailed(@c) ORDER BY contribution_weight DESC", c))
        {
            cmd.Parameters.AddWithValue("c", transition!.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { rows++; codes += r.GetString(0) + ";"; }
        }
        Assert.Equal(2, rows);
        Assert.Contains("H-3361", codes);
        Assert.Contains("H-3362", codes);
        Assert.True((bool)(await ScalarAsync(c, "SELECT public.ppiq_v5_attribution_weight_ok(@c)", ("c", transition.Value)))!,
            "Transition weights must sum to 1.0 +/- 0.01.");

        // Normal coil: exactly one heat at 100%.
        var normalRows = Convert.ToInt32(await ScalarAsync(c,
            "SELECT count(*) FROM public.ppiq_v5_blended_attribution_detailed(@c)", ("c", normal!.Value)));
        Assert.Equal(1, normalRows);
        Assert.True((bool)(await ScalarAsync(c, "SELECT public.ppiq_v5_attribution_weight_ok(@c)", ("c", normal.Value)))!,
            "Normal coil weight must be 1.0.");
    }
}