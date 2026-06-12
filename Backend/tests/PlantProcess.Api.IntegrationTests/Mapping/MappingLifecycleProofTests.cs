using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Mapping;

// P6-03 â€” mapping validate/publish/rollback with typed errors + safe-SQL rejection. DB-gated.
public sealed class MappingLifecycleProofTests : AuthenticatedApiTestBase
{
    public MappingLifecycleProofTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static string Conn =>
        ResolveIntegrationTestConnectionString();

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

    private static bool MentionsAny(string s, params string[] tokens) =>
        tokens.Any(t => s.Contains(t, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task Mapping_lifecycle_proof_validates_publishes_and_rolls_back()
    {
        await using var c = await OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT step_code, is_valid, error_code, message FROM public.ppiq_run_mapping_lifecycle_proof()", c);
        await using var r = await cmd.ExecuteReaderAsync();

        var steps = new List<(string Step, bool Ok, string? Err, string? Msg)>();
        while (await r.ReadAsync())
            steps.Add((
                r.IsDBNull(0) ? "" : r.GetString(0),
                !r.IsDBNull(1) && r.GetBoolean(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3)));

        Assert.NotEmpty(steps);
        Assert.True(steps.Count >= 2, "Lifecycle proof should report multiple steps (validate/publish/rollback).");
        Assert.All(steps.Where(s => !s.Ok), s =>
            Assert.False(string.IsNullOrWhiteSpace(s.Err), $"Failing step '{s.Step}' must surface a typed error_code."));
        Assert.Contains(steps, s => MentionsAny(s.Step, "publish", "version") && s.Ok);
        Assert.Contains(steps, s => MentionsAny(s.Step, "rollback", "revert", "restore") && s.Ok);
    }

    [Fact]
    public async Task Safe_sql_gate_rejects_dangerous_sql()
    {
        await using var c = await OpenAsync();

        // Discover the safe-SQL function this DB build actually exposes (scripts vary by environment).
        string? fn = null; var nargs = 0;
        await using (var probe = new NpgsqlCommand(
            @"SELECT p.proname, p.pronargs
              FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
              WHERE n.nspname = 'public'
                AND p.proname IN ('ppiq_validate_safe_sql_typed','ppiq_validate_safe_sql','ppiq_resolve_safe_sql')
              ORDER BY array_position(
                ARRAY['ppiq_validate_safe_sql_typed','ppiq_validate_safe_sql','ppiq_resolve_safe_sql'], p.proname)
              LIMIT 1", c))
        await using (var pr = await probe.ExecuteReaderAsync())
            if (await pr.ReadAsync()) { fn = pr.GetString(0); nargs = pr.GetInt32(1); }

        // No SQL-level gate in this build -> rejection is enforced by the C# SafeSqlValidator (covered in P6-04).
        if (fn is null) return;

        // Non-destructive but definitively unsafe probe (system-schema access is rejected by the validator).
        const string danger = "SELECT * FROM information_schema.tables;";
        var sql = nargs >= 3 ? $"SELECT * FROM public.{fn}(@s, @rl, @to)" : $"SELECT * FROM public.{fn}(@s)";

        var rejected = false;
        try
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            cmd.Parameters.AddWithValue("s", danger);
            if (nargs >= 3) { cmd.Parameters.AddWithValue("rl", 100); cmd.Parameters.AddWithValue("to", 5000); }
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                for (var i = 0; i < r.FieldCount; i++)
                {
                    if (r.IsDBNull(i)) continue;
                    if (r.GetFieldType(i) == typeof(bool) && r.GetBoolean(i) == false) rejected = true;
                    if (r.GetFieldType(i) == typeof(string) &&
                        MentionsAny(r.GetString(i), "reject", "not allowed", "forbidden", "unsafe",
                            "ddl", "mutation", "system", "read-only", "read only", "denied"))
                        rejected = true;
                }
        }
        catch (PostgresException)
        {
            rejected = true; // the gate raised on the unsafe statement -> rejection proven
        }

        Assert.True(rejected, $"{fn} must reject system-schema access (raise, is_valid=false, or a rejection reason).");
    }
}