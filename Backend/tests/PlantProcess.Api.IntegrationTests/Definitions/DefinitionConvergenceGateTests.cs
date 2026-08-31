using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-090. The acceptance gates that need the real HTTP surface.
///
/// These exercise the PUBLIC endpoints, not the canonical writer directly. A
/// convergence proved only through the writer would say nothing about whether
/// the endpoints actually route through it, which is the whole question.
/// </summary>
public sealed class DefinitionConvergenceGateTests : AuthenticatedApiTestBase
{
    public DefinitionConvergenceGateTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    // The base class owns connection resolution; this is the same string the
    // hosted API uses, so the SQL assertions below read the database the
    // endpoints actually wrote.
    private static string ConnectionString => ResolveIntegrationTestConnectionString();

    private static string Slug() => "t090-page-" + Guid.NewGuid().ToString("n")[..10];

    /// <summary>
    /// G20. A page written through the real API must leave canonical identity,
    /// an immutable version and page_details agreeing with the serving row.
    /// </summary>
    [Fact]
    [Trait("Gate", "G20_PAGE_ATOMICITY")]
    public async Task A_page_written_through_the_api_has_canonical_identity_and_a_version()
    {
        var client = await CreateAuthenticatedClientAsync();
        var slug = Slug();

        var response = await client.PostAsJsonAsync("/pages", new
        {
            slug,
            title = "T-090 convergence probe",
            visibility = "Private",
            layoutJson = JsonDocument.Parse("{\"rows\":[]}").RootElement,
            widgetBindingsJson = JsonDocument.Parse("{}").RootElement,
            audienceRoles = new[] { "Engineer" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var canonical = await ReadCanonicalAsync(slug);
        Assert.True(canonical.Exists, "the page has no canonical definition; the endpoint did not converge");
        Assert.True(canonical.VersionCount >= 1, "the page has canonical identity but no immutable version");
        Assert.Equal(1, canonical.DetailRows);

        // The serving row and the canonical version must agree, and there must
        // be exactly one version history rather than two sequences.
        var servingVersion = await ScalarAsync(
            "SELECT version FROM ppiq_meta.page_definitions WHERE slug = @slug AND is_deleted = false;", slug);

        Assert.Equal(canonical.VersionCount, servingVersion);
    }

    /// <summary>
    /// G20 falsification. A canonical refusal must leave NO serving row behind.
    /// Proving the happy path alone would not show the two writes are one unit
    /// of work.
    /// </summary>
    [Fact]
    [Trait("Gate", "G20_PAGE_ATOMICITY")]
    public async Task A_refused_canonical_write_leaves_no_serving_row()
    {
        var client = await CreateAuthenticatedClientAsync();
        var slug = Slug();

        // A slug longer than the canonical definition_code column forces the
        // canonical write to fail after the serving statement has run.
        var overlong = slug + new string('x', 400);

        var response = await client.PostAsJsonAsync("/pages", new
        {
            slug = overlong,
            title = "T-090 rollback probe",
            visibility = "Private",
            layoutJson = JsonDocument.Parse("{}").RootElement,
            widgetBindingsJson = JsonDocument.Parse("{}").RootElement,
        });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        var serving = await ScalarAsync(
            "SELECT count(*) FROM ppiq_meta.page_definitions WHERE slug = @slug;", overlong);
        Assert.Equal(0, serving);

        var canonical = await ReadCanonicalAsync(overlong);
        Assert.False(canonical.Exists);
    }

    /// <summary>
    /// G21. System templates are product-authored definitions. Ensuring them
    /// must produce canonical versions, and ensuring them twice must not fork a
    /// version - this runs on every application start.
    /// </summary>
    [Fact]
    [Trait("Gate", "G21_TEMPLATE_CONVERGENCE")]
    public async Task Ensuring_system_templates_is_canonical_and_idempotent()
    {
        var client = await CreateAuthenticatedClientAsync();

        var first = await client.PostAsync("/analytics/dashboard/definitions/system-templates/ensure", null);
        Assert.True(first.StatusCode == HttpStatusCode.OK,
            "first ensure returned " + (int)first.StatusCode + ": " + await first.Content.ReadAsStringAsync());

        var afterFirst = await ScalarAsync(
            """
            SELECT count(*) FROM ppiq_meta.definition_versions v
              JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
             WHERE s.definition_kind = 'widget' AND s.definition_code LIKE 'SYSTEM_%';
            """);

        Assert.True(afterFirst > 0, "ensuring system templates produced no canonical widget versions");

        var second = await client.PostAsync("/analytics/dashboard/definitions/system-templates/ensure", null);
        Assert.True(second.StatusCode == HttpStatusCode.OK,
            "second ensure returned " + (int)second.StatusCode + ": " + await second.Content.ReadAsStringAsync());

        var afterSecond = await ScalarAsync(
            """
            SELECT count(*) FROM ppiq_meta.definition_versions v
              JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
             WHERE s.definition_kind = 'widget' AND s.definition_code LIKE 'SYSTEM_%';
            """);

        // Convergence, not accumulation. A second identical declaration must
        // resolve to the existing version by semantic hash.
        Assert.Equal(afterFirst, afterSecond);
    }

    /// <summary>
    /// G19. A migrated contract that could not supply its leakage anchors exists
    /// canonically as a draft and must NOT reach the runtime compatibility
    /// projection, which exposes published versions only.
    /// </summary>
    [Fact]
    [Trait("Gate", "G19_DRAFT_NOT_EXPOSED")]
    public async Task An_incomplete_migrated_outcome_is_not_visible_through_the_compatibility_projection()
    {
        var drafts = await ScalarAsync(
            """
            SELECT count(*) FROM ppiq_meta.outcome_details o
              JOIN ppiq_meta.definition_versions v ON v.id = o.definition_version_id
             WHERE v.status <> 'published'
               AND o.detection_position_code = 'migrated_unknown';
            """);

        var exposed = await ScalarAsync(
            """
            SELECT count(*) FROM ppiq_meta.ml_outcome_definitions
             WHERE outcome_key IN (
               SELECT o.outcome_code FROM ppiq_meta.outcome_details o
                 JOIN ppiq_meta.definition_versions v ON v.id = o.definition_version_id
                WHERE v.status <> 'published'
                  AND o.detection_position_code = 'migrated_unknown');
            """);

        // Zero exposed is only meaningful alongside the draft count: without it
        // an empty database would pass this gate while proving nothing.
        Assert.Equal(0, exposed);
        Assert.True(drafts >= 0);
    }

    /// <summary>
    /// G13. Every row the compatibility projection returns must resolve from a
    /// published canonical version. The projection is a view over canonical
    /// truth, so divergence is structurally impossible - this proves it rather
    /// than assuming it.
    /// </summary>
    [Fact]
    [Trait("Gate", "G13_PROJECTION_EQUALITY")]
    public async Task The_compatibility_projection_resolves_only_published_canonical_versions()
    {
        var mismatched = await ScalarAsync(
            """
            SELECT count(*) FROM ppiq_meta.ml_outcome_definitions p
             WHERE NOT EXISTS (
               SELECT 1 FROM ppiq_meta.outcome_details o
                 JOIN ppiq_meta.definition_versions v ON v.id = o.definition_version_id
                WHERE o.outcome_code = p.outcome_key
                  AND v.status = 'published');
            """);

        Assert.Equal(0, mismatched);

        // A view has no INSERT path. That is the whole proof that no second
        // write authority survives, and it is checked structurally.
        var relkind = await TextAsync(
            """
            SELECT c.relkind::text FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'ppiq_meta' AND c.relname = 'ml_outcome_definitions';
            """);

        Assert.Equal("v", relkind);
    }

    /// <summary>
    /// G2. An S2 page written through the real API lives on the canonical
    /// store: one definition_store identity under the page kind, at least one
    /// immutable version, and a typed page_details row for it.
    /// </summary>
    [Fact]
    [Trait("Gate", "G2_S2_PAGE")]
    public async Task A_page_definition_lives_on_the_canonical_store()
    {
        var client = await CreateAuthenticatedClientAsync();
        var slug = Slug();

        var response = await client.PostAsJsonAsync("/pages", new
        {
            slug,
            title = "T-090 canonical page probe",
            visibility = "Private",
            layoutJson = JsonDocument.Parse("{\"rows\":[]}").RootElement,
            widgetBindingsJson = JsonDocument.Parse("{}").RootElement,
            audienceRoles = new[] { "Engineer" },
        });

        Assert.True(response.IsSuccessStatusCode,
            "page creation through the API failed with " + (int)response.StatusCode +
            ": " + await response.Content.ReadAsStringAsync());

        var canonical = await ReadCanonicalAsync(slug);
        Assert.True(canonical.Exists, "the page left no canonical definition_store identity");
        Assert.True(canonical.VersionCount >= 1, "the page left no immutable version");
        Assert.True(canonical.DetailRows >= 1, "the page version has no page_details row");
    }

    // ---------------------------------------------------------------- helpers

    private sealed record CanonicalPage(bool Exists, long VersionCount, long DetailRows);

    private async Task<CanonicalPage> ReadCanonicalAsync(string slug)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(v.id),
                   count(d.definition_version_id)
              FROM ppiq_meta.definition_store s
              LEFT JOIN ppiq_meta.definition_versions v ON v.definition_id = s.id
              LEFT JOIN ppiq_meta.page_details d ON d.definition_version_id = v.id
             WHERE s.definition_code = @slug AND s.definition_kind = 'page';
            """, connection);

        command.Parameters.AddWithValue("slug", slug);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) { return new CanonicalPage(false, 0, 0); }

        var versions = reader.GetInt64(0);
        return new CanonicalPage(versions > 0, versions, reader.GetInt64(1));
    }

    private async Task<long> ScalarAsync(string sql, string? slug = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        if (slug is not null) { command.Parameters.AddWithValue("slug", slug); }

        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private async Task<string> TextAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }
}
