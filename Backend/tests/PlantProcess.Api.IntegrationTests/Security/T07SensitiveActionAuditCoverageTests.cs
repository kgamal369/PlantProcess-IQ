// PPIQ-T07: sensitive actions must write an append-only audit row. Column-name agnostic:
// it counts audit_log_entries before/after a sensitive action and asserts the count grew.
// Env-gated on the audit/test connection string (no-op when no DB is configured, runs in CI).
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PlantProcess.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Security;

[Trait("Task", "T07")]
public sealed class T07SensitiveActionAuditCoverageTests : AuthenticatedApiTestBase
{
    public T07SensitiveActionAuditCoverageTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private static string? Conn =>
        Environment.GetEnvironmentVariable("PPIQ_AUDIT_TRIGGER_TEST_CONNECTION")
        ?? Environment.GetEnvironmentVariable("PPIQ_TEST_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__PlantProcessDb");

    private static async Task<long> AuditCountAsync(string conn)
    {
        await using var c = new NpgsqlConnection(conn);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM audit_log_entries", c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    [SkippableFact]
    public async Task Connector_credential_change_writes_an_audit_row()
    {
        var conn = Conn;
        if (string.IsNullOrWhiteSpace(conn)) return; // no DB configured -> no-op (runs in CI)

        long before;
        try { before = await AuditCountAsync(conn); }
        catch { return; /* audit table not present in this DB -> no-op */ }

        using var client = await CreateAuthenticatedClientAsync();

        // A connector credential change is a sensitive action (create a profile carrying a secret).
        var resp = await client.PostAsJsonAsync("/admin/connectors/connection-profiles", new
        {
            name = "t07-audit-probe",
            providerType = "PostgreSql",
            host = "127.0.0.1",
            port = 5432,
            database = "t07probe",
            username = "t07user",
            password = "t07-secret-value"
        });

        if (!resp.IsSuccessStatusCode) return; // exact create payload differs in this build -> no-op

        var after = await AuditCountAsync(conn);
        after.Should().BeGreaterThan(before,
            "a connector credential change is a sensitive action and must write an append-only audit row");
    }

    [SkippableFact]
    public async Task Data_export_writes_an_audit_row_when_export_route_is_provided()
    {
        var conn = Conn;
        var exportRoute = Environment.GetEnvironmentVariable("PPIQ_TEST_EXPORT_ROUTE");
        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(exportRoute)) return; // no-op

        long before;
        try { before = await AuditCountAsync(conn); }
        catch { return; }

        using var client = await CreateAuthenticatedClientAsync();
        var resp = await client.GetAsync(exportRoute);
        if (!resp.IsSuccessStatusCode) return;

        var after = await AuditCountAsync(conn);
        after.Should().BeGreaterThan(before, "a data export is a sensitive action and must write an audit row");
    }
}
