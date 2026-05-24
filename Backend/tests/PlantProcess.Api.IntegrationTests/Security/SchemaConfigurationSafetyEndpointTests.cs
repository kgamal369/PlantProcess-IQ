// ============================================================
// File: Backend/tests/PlantProcess.Api.IntegrationTests/Security/SchemaConfigurationSafetyEndpointTests.cs
// Task: BE-FIX-002
//
// Purpose:
//   Prove SafeSqlValidator is not only unit-tested, but also wired
//   into the real API endpoint:
//     POST /admin/schema-configuration/views/preview
// ============================================================

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Security;

[Trait("Task", "BE-FIX-002")]
[Trait("Category", "Integration")]
public sealed class SchemaConfigurationSafetyEndpointTests : AuthenticatedApiTestBase
{
    public SchemaConfigurationSafetyEndpointTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("SELECT pg_read_file('/etc/passwd', 0, 1000);")]
    [InlineData("SELECT pg_sleep(30);")]
    [InlineData("SELECT * FROM information_schema.tables;")]
    [InlineData("SELECT * FROM pg_catalog.pg_user;")]
    [InlineData("SELECT * FROM material_units m JOIN audit_log_entries a ON 1 = 1;")]
    [InlineData("SELECT * FROM material_units, audit_log_entries;")]
    [InlineData("UPDATE material_units SET material_code = 'tampered';")]
    [InlineData("DELETE FROM material_units;")]
    [InlineData("DROP TABLE material_units;")]
    [InlineData("WITH RECURSIVE x AS (SELECT 1 AS n UNION ALL SELECT n + 1 FROM x) SELECT * FROM x;")]
    public async Task Ad_hoc_preview_endpoint_should_reject_unsafe_sql(string sqlText)
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/admin/schema-configuration/views/preview",
            new
            {
                SqlText = sqlText,
                MaxRows = 50,
                TimeoutSeconds = 5
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("SQL safety validation failed");
    }

    [Fact]
    public async Task Ad_hoc_preview_endpoint_should_require_sql_text()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/admin/schema-configuration/views/preview",
            new
            {
                SqlText = "",
                MaxRows = 50,
                TimeoutSeconds = 5
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SqlText is required");
    }
}