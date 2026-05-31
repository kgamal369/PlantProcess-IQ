const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const relativePath = path.join(
  "Backend",
  "PlantProcess.Api",
  "Endpoints",
  "PageBuilder",
  "PageDefinitionEndpoints.cs"
);

const file = path.join(root, relativePath);

if (fs.existsSync(file)) {
  const backup = file.replace(/\.cs$/i, ".broken-backup.cs");
  fs.copyFileSync(file, backup);
  console.log("Backed up corrupted file to " + path.relative(root, backup));
}

const content = String.raw`using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.PageBuilder;

public static class PageDefinitionEndpoints
{
    private static readonly Regex SlugRegex = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapPageDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/pages")
            .WithTags("Page Builder")
            .RequireAuthorization();

        group.MapGet("", ListPagesAsync);
        group.MapGet("/{slug}", GetPageAsync);
        group.MapPost("", UpsertPageAsync);
        group.MapPut("/{slug}", UpdatePageAsync);
        group.MapDelete("/{slug}", DeletePageAsync);

        return app;
    }

    private static async Task<IResult> ListPagesAsync(
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id, tenant_id, slug, title, owner_user_name, visibility, version,
                   layout_json::text, widget_bindings_json::text, updated_at_utc
            FROM page_definitions
            WHERE tenant_id = @tenant
              AND is_deleted = false
              AND (owner_user_name = @owner OR visibility IN ('Shared', 'Public'))
            ORDER BY updated_at_utc DESC, title ASC;
            """,
            connection);

        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("owner", owner);

        var pages = new List<PageDefinitionDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pages.Add(ReadDto(reader));
        }

        return Results.Ok(pages);
    }

    private static async Task<IResult> GetPageAsync(
        string slug,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id, tenant_id, slug, title, owner_user_name, visibility, version,
                   layout_json::text, widget_bindings_json::text, updated_at_utc
            FROM page_definitions
            WHERE tenant_id = @tenant
              AND slug = @slug
              AND is_deleted = false
              AND (owner_user_name = @owner OR visibility IN ('Shared', 'Public'))
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("owner", owner);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.NotFound(new { message = "Page '" + slug + "' was not found." });
        }

        return Results.Ok(ReadDto(reader));
    }

    private static async Task<IResult> UpsertPageAsync(
        [FromBody] UpsertPageDefinitionRequest request,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO page_definitions
                (tenant_id, slug, title, owner_user_name, visibility, layout_json, widget_bindings_json, updated_at_utc)
            VALUES
                (@tenant, @slug, @title, @owner, @visibility, @layout_json, @widget_bindings_json, now())
            ON CONFLICT (tenant_id, slug)
            WHERE is_deleted = false
            DO UPDATE SET
                title = EXCLUDED.title,
                owner_user_name = EXCLUDED.owner_user_name,
                visibility = EXCLUDED.visibility,
                layout_json = EXCLUDED.layout_json,
                widget_bindings_json = EXCLUDED.widget_bindings_json,
                version = page_definitions.version + 1,
                updated_at_utc = now()
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
                      layout_json::text, widget_bindings_json::text, updated_at_utc;
            """,
            connection);

        AddPageParameters(command, request, tenant, owner);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return Results.Ok(ReadDto(reader));
    }

    private static async Task<IResult> UpdatePageAsync(
        string slug,
        [FromBody] UpsertPageDefinitionRequest request,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var normalized = request with { Slug = slug };

        var errors = Validate(normalized);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            UPDATE page_definitions
            SET title = @title,
                visibility = @visibility,
                layout_json = @layout_json,
                widget_bindings_json = @widget_bindings_json,
                version = version + 1,
                updated_at_utc = now()
            WHERE tenant_id = @tenant
              AND slug = @slug
              AND owner_user_name = @owner
              AND is_deleted = false
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
                      layout_json::text, widget_bindings_json::text, updated_at_utc;
            """,
            connection);

        AddPageParameters(command, normalized, tenant, owner);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.NotFound(new { message = "Page '" + slug + "' was not found or is not owned by '" + owner + "'." });
        }

        return Results.Ok(ReadDto(reader));
    }

    private static async Task<IResult> DeletePageAsync(
        string slug,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            UPDATE page_definitions
            SET is_deleted = true,
                updated_at_utc = now()
            WHERE tenant_id = @tenant
              AND slug = @slug
              AND owner_user_name = @owner
              AND is_deleted = false;
            """,
            connection);

        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("owner", owner);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        return Results.Ok(new PageDeleteResponse(affected > 0));
    }

    private static async Task EnsureSchemaAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var sql = string.Join(Environment.NewLine, new[]
        {
            "CREATE EXTENSION IF NOT EXISTS pgcrypto;",
            "",
            "CREATE TABLE IF NOT EXISTS page_definitions (",
            "    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),",
            "    tenant_id text NOT NULL DEFAULT 'demo',",
            "    slug text NOT NULL,",
            "    title text NOT NULL,",
            "    owner_user_name text NOT NULL,",
            "    visibility text NOT NULL DEFAULT 'Private',",
            "    version integer NOT NULL DEFAULT 1,",
            "    layout_json jsonb NOT NULL DEFAULT '{}'::jsonb,",
            "    widget_bindings_json jsonb NOT NULL DEFAULT '{}'::jsonb,",
            "    is_deleted boolean NOT NULL DEFAULT false,",
            "    created_at_utc timestamptz NOT NULL DEFAULT now(),",
            "    updated_at_utc timestamptz NOT NULL DEFAULT now(),",
            "    CONSTRAINT ck_page_definitions_slug CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),",
            "    CONSTRAINT ck_page_definitions_visibility CHECK (visibility IN ('Private', 'Shared', 'Public'))",
            ");",
            "",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_page_definitions_tenant_slug_active",
            "ON page_definitions (tenant_id, slug)",
            "WHERE is_deleted = false;",
            "",
            "CREATE INDEX IF NOT EXISTS ix_page_definitions_owner_visible",
            "ON page_definitions (tenant_id, owner_user_name, visibility)",
            "WHERE is_deleted = false;"
        });

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureOpenAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static void AddPageParameters(
        NpgsqlCommand command,
        UpsertPageDefinitionRequest request,
        string tenant,
        string owner)
    {
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("slug", request.Slug.Trim());
        command.Parameters.AddWithValue("title", request.Title.Trim());
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("visibility", request.Visibility.Trim());

        command.Parameters.Add(
            new NpgsqlParameter("layout_json", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(request.LayoutJson)
            });

        command.Parameters.Add(
            new NpgsqlParameter("widget_bindings_json", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(request.WidgetBindingsJson)
            });
    }

    private static Dictionary<string, string[]> Validate(UpsertPageDefinitionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.Slug) || !SlugRegex.IsMatch(request.Slug.Trim()))
        {
            errors["slug"] = new[]
            {
                "Slug must be URL-safe: lowercase letters, digits and hyphen-separated words only."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = new[] { "Title is required." };
        }

        if (request.Visibility is not ("Private" or "Shared" or "Public"))
        {
            errors["visibility"] = new[] { "Visibility must be Private, Shared or Public." };
        }

        if (request.LayoutJson.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            errors["layoutJson"] = new[] { "Layout JSON is required." };
        }

        if (request.WidgetBindingsJson.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            errors["widgetBindingsJson"] = new[] { "Widget bindings JSON is required." };
        }

        return errors;
    }

    private static string ResolveUserName(ClaimsPrincipal user)
    {
        return user.Identity?.Name
               ?? user.FindFirstValue(ClaimTypes.Name)
               ?? user.FindFirstValue("name")
               ?? user.FindFirstValue("preferred_username")
               ?? "unknown-user";
    }

    private static string ResolveTenant(ClaimsPrincipal user)
    {
        return user.FindFirstValue("tenant_id")
               ?? user.FindFirstValue("tenant")
               ?? "demo";
    }

    private static DateTimeOffset ReadDateTimeOffset(
        NpgsqlDataReader reader,
        int ordinal)
    {
        var value = reader.GetValue(ordinal);

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, out var parsed) => parsed.ToUniversalTime(),
            _ => DateTimeOffset.UtcNow
        };
    }

    private static PageDefinitionDto ReadDto(NpgsqlDataReader reader)
    {
        return new PageDefinitionDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            JsonSerializer.Deserialize<JsonElement>(reader.GetString(7)),
            JsonSerializer.Deserialize<JsonElement>(reader.GetString(8)),
            ReadDateTimeOffset(reader, 9));
    }
}

public sealed record PageDefinitionDto(
    Guid Id,
    string TenantId,
    string Slug,
    string Title,
    string OwnerUserName,
    string Visibility,
    int Version,
    JsonElement LayoutJson,
    JsonElement WidgetBindingsJson,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertPageDefinitionRequest(
    string Slug,
    string Title,
    string Visibility,
    JsonElement LayoutJson,
    JsonElement WidgetBindingsJson);

public sealed record PageDeleteResponse(bool Deleted);
`;

fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");

const finalText = fs.readFileSync(file, "utf8");

const checks = [
  ["MapGroup(\"/pages\")", finalText.includes('MapGroup("/pages")')],
  ["MapPost(\"\", UpsertPageAsync)", finalText.includes('MapPost("", UpsertPageAsync)')],
  ["NpgsqlCommand(sql, connection)", finalText.includes("NpgsqlCommand(sql, connection)")],
  ["no ExecuteSqlRawAsync", !finalText.includes("ExecuteSqlRawAsync")],
  ["no broken raw string in EnsureSchema", !finalText.includes('const string sql = @"')]
];

const failed = checks.filter(([, ok]) => !ok);
if (failed.length > 0) {
  console.error("Validation failed:");
  for (const [name] of failed) {
    console.error(" - " + name);
  }
  process.exit(1);
}

console.log("Rewrote PageDefinitionEndpoints.cs with clean compile-safe implementation.");
