// ============================================================================
// T-013 - Tenant isolation under FORCE ROW LEVEL SECURITY.
// Self-contained: builds a probe table that uses the SAME mechanism as the
// real policies (public.ppiq_current_tenant() reading the app.current_tenant
// GUC, per script 510), proves a second tenant cannot see or write the first
// tenant's rows, then drops the probe table.
//
// Precondition: the test DB must connect as a NON-superuser, non-BYPASSRLS role
// (script 510's design). A superuser bypasses RLS even under FORCE, so the test
// fails loudly with guidance rather than passing falsely.
// ============================================================================
using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Security;

[Trait("Category", "Integration")]
[Trait("Task", "T-013")]
public sealed class RlsTenantIsolationTests
{
    private static string ConnString =>
        Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb")
        ?? "Host=127.0.0.1;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123";

    [Fact]
    public async Task Forced_rls_isolates_rows_by_app_current_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(ConnString);
        await conn.OpenAsync(CancellationToken.None);

        Assert.False(
            await ScalarBoolAsync(conn, "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user"),
            "Test DB role is superuser or BYPASSRLS, so FORCE RLS is bypassed. " +
            "Connect the test DB as a non-superuser role (per script 510) to validate isolation.");

        Assert.True(
            await ScalarBoolAsync(conn, "SELECT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'ppiq_current_tenant')"),
            "public.ppiq_current_tenant() (script 510) must exist.");

        try
        {
            await ExecAsync(conn, "DROP TABLE IF EXISTS public._ppiq_rls_probe");
            await ExecAsync(conn, "CREATE TABLE public._ppiq_rls_probe (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, note text)");
            await ExecAsync(conn, "ALTER TABLE public._ppiq_rls_probe ENABLE ROW LEVEL SECURITY");
            await ExecAsync(conn, "ALTER TABLE public._ppiq_rls_probe FORCE ROW LEVEL SECURITY");
            await ExecAsync(conn, "CREATE POLICY _ppiq_rls_probe_pol ON public._ppiq_rls_probe USING (tenant_id = public.ppiq_current_tenant()) WITH CHECK (tenant_id = public.ppiq_current_tenant())");

            await SetTenantAsync(conn, tenantA);
            await ExecAsync(conn, "INSERT INTO public._ppiq_rls_probe (tenant_id, note) VALUES (public.ppiq_current_tenant(), 'tenant-a-row')");
            Assert.Equal(1L, await CountProbeAsync(conn));

            await SetTenantAsync(conn, tenantB);
            Assert.Equal(0L, await CountProbeAsync(conn));

            var crossTenantInsertBlocked = false;
            try
            {
                await using var bad = new NpgsqlCommand("INSERT INTO public._ppiq_rls_probe (tenant_id, note) VALUES (@a, 'cross')", conn);
                bad.Parameters.AddWithValue("a", tenantA);
                await bad.ExecuteNonQueryAsync();
            }
            catch (PostgresException)
            {
                crossTenantInsertBlocked = true;
            }
            Assert.True(crossTenantInsertBlocked, "WITH CHECK must block inserting a row for another tenant.");

            await SetTenantAsync(conn, tenantA);
            Assert.Equal(1L, await CountProbeAsync(conn));
        }
        finally
        {
            try { await ExecAsync(conn, "SELECT set_config('app.current_tenant', '', false)"); } catch { }
            try { await ExecAsync(conn, "DROP TABLE IF EXISTS public._ppiq_rls_probe"); } catch { }
        }
    }

    private static async Task SetTenantAsync(NpgsqlConnection conn, Guid tenant)
    {
        await using var cmd = new NpgsqlCommand("SELECT set_config('app.current_tenant', @t, false)", conn);
        cmd.Parameters.AddWithValue("t", tenant.ToString());
        await cmd.ExecuteScalarAsync();
    }

    private static async Task<long> CountProbeAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM public._ppiq_rls_probe", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var v = await cmd.ExecuteScalarAsync();
        return v is bool b && b;
    }
}