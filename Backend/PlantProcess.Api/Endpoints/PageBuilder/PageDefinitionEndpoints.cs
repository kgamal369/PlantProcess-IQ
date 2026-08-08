using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;
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
                   layout_json::text, widget_bindings_json::text, updated_at_utc,
                   audience_roles::text
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
                   layout_json::text, widget_bindings_json::text, updated_at_utc,
                   audience_roles::text
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
            return ApplicationProblems.NotFound("Page '" + slug + "' was not found.");
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
                (tenant_id, slug, title, owner_user_name, visibility, audience_roles, layout_json, widget_bindings_json, updated_at_utc)
            VALUES
                (@tenant, @slug, @title, @owner, @visibility, COALESCE(@audience_roles, '[]'::jsonb), @layout_json, @widget_bindings_json, now())
            ON CONFLICT (tenant_id, slug)
            WHERE is_deleted = false
            DO UPDATE SET
                title = EXCLUDED.title,
                owner_user_name = EXCLUDED.owner_user_name,
                visibility = EXCLUDED.visibility,
                audience_roles = COALESCE(@audience_roles, page_definitions.audience_roles, '[]'::jsonb),
                layout_json = EXCLUDED.layout_json,
                widget_bindings_json = EXCLUDED.widget_bindings_json,
                version = page_definitions.version + 1,
                updated_at_utc = now()
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
                      layout_json::text, widget_bindings_json::text, updated_at_utc,
                      audience_roles::text;
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

        if (normalized.ExpectedVersion is int expectedVersion)
        {
            await using var conflictCheck = new NpgsqlCommand(
                "SELECT version, owner_user_name, updated_at_utc FROM page_definitions " +
                "WHERE tenant_id = @tenant AND slug = @slug AND owner_user_name = @owner AND is_deleted = false",
                connection);
            conflictCheck.Parameters.AddWithValue("tenant", tenant);
            conflictCheck.Parameters.AddWithValue("slug", normalized.Slug);
            conflictCheck.Parameters.AddWithValue("owner", owner);
            await using var conflictReader = await conflictCheck.ExecuteReaderAsync(cancellationToken);
            if (await conflictReader.ReadAsync(cancellationToken))
            {
                var currentVersion = conflictReader.GetInt32(0);
                if (currentVersion != expectedVersion)
                {
                    var editorUser = conflictReader.GetString(1);
                    var updatedAtUtc = conflictReader.GetDateTime(2);
                    return Results.Json(new
                    {
                        code = "page_version_conflict",
                        message = "This page was changed by " + editorUser + " since you opened it.",
                        currentVersion,
                        editor = editorUser,
                        updatedAtUtc,
                    }, statusCode: 409);
                }
            }
        }
        await using var command = new NpgsqlCommand(
            """
            UPDATE page_definitions
            SET title = @title,
                visibility = @visibility,
                audience_roles = COALESCE(@audience_roles, audience_roles),
                layout_json = @layout_json,
                widget_bindings_json = @widget_bindings_json,
                version = version + 1,
                updated_at_utc = now()
            WHERE tenant_id = @tenant
              AND slug = @slug
              AND owner_user_name = @owner
              AND is_deleted = false
              AND (@expected_version IS NULL OR version = @expected_version)
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
                      layout_json::text, widget_bindings_json::text, updated_at_utc,
                      audience_roles::text;
            """,
            connection);

        AddPageParameters(command, normalized, tenant, owner);
        command.Parameters.Add(new NpgsqlParameter("expected_version", NpgsqlDbType.Integer)
        {
            Value = normalized.ExpectedVersion.HasValue
                ? normalized.ExpectedVersion.Value
                : DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ApplicationProblems.NotFound("Page '" + slug + "' was not found or is not owned by '" + owner + "'.");
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
            "    audience_roles jsonb NOT NULL DEFAULT '[]'::jsonb,",
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
            "WHERE is_deleted = false;",
            "",
            // An installation created before T-041 already has the table, so
            // CREATE TABLE IF NOT EXISTS reaches none of it. This is the line
            // that carries an existing page store forward.
            "ALTER TABLE page_definitions ADD COLUMN IF NOT EXISTS audience_roles jsonb NOT NULL DEFAULT '[]'::jsonb;"
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

        // PPIQ T-041. AUDIENCE IS NOT VISIBILITY, and neither replaces the other.
        // Visibility answers who may open the page. Audience answers which roles
        // the page was authored FOR, which is what T-042 reads when Publish puts
        // it into navigation. A legacy row that never declared one reads as an
        // empty list rather than as every role.
        // PPIQ T-041 S2c. OMISSION AND AN EMPTY ARRAY ARE DIFFERENT ANSWERS.
        // An old caller that never heard of this field sends nothing, and must
        // not thereby erase an audience someone authored. So omission travels
        // as SQL NULL and the statements decide what NULL means: an empty list
        // on insert, the value already stored on update.
        command.Parameters.Add(
            new NpgsqlParameter("audience_roles", NpgsqlDbType.Jsonb)
            {
                Value = request.AudienceRoles is null
                    ? DBNull.Value
                    : JsonSerializer.Serialize(NormaliseAudienceRoles(request.AudienceRoles))
            });

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

    /// PPIQ T-041. The four roles the API already authorises against, in
    /// Program.cs. The audience contract reuses that authority rather than
    /// inventing a parallel role list that could drift from it.
    private static readonly string[] AudienceRoleAuthority =
    {
        "Admin", "DataManager", "Engineer", "Viewer"
    };

    /// Trimmed, de-duplicated case-insensitively, ordered by the authority above
    /// so the stored list is stable, and unknown values dropped - the validator
    /// reports them, this only stores what survived.
    private static IReadOnlyList<string> NormaliseAudienceRoles(IReadOnlyList<string>? roles)
    {
        if (roles is null)
        {
            return Array.Empty<string>();
        }

        var chosen = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .ToArray();

        return AudienceRoleAuthority
            .Where(known => chosen.Contains(known, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadAudienceRoles(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return Array.Empty<string>();
        }

        var text = reader.GetString(ordinal);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(text) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            // A row whose audience cannot be read is reported as having none.
            // Inventing one would be worse than saying nothing.
            return Array.Empty<string>();
        }
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

        // PPIQ T-041. A caller that OMITS the field is a caller written before
        // this contract existed, and it keeps working with an empty audience.
        // A caller that SENDS the field is declaring an audience, so an empty
        // one is a mistake rather than a legacy shape - and the Page Builder
        // always sends it, which is where "at least one role" is enforced for a
        // new page.
        if (request.AudienceRoles is not null)
        {
            var audience = NormaliseAudienceRoles(request.AudienceRoles);

            if (audience.Count == 0)
            {
                errors["audienceRoles"] = new[]
                {
                    "Choose at least one audience role: " + string.Join(", ", AudienceRoleAuthority) + "."
                };
            }
            else
            {
                var unknown = request.AudienceRoles
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(role => role.Trim())
                    .Where(role => !AudienceRoleAuthority.Contains(role, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (unknown.Length > 0)
                {
                    errors["audienceRoles"] = new[]
                    {
                        "Unknown audience role(s): " + string.Join(", ", unknown)
                        + ". Allowed roles are " + string.Join(", ", AudienceRoleAuthority) + "."
                    };
                }
            }
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
            ReadDateTimeOffset(reader, 9),
            ReadAudienceRoles(reader, 10));
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
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> AudienceRoles);

public sealed record UpsertPageDefinitionRequest(
    string Slug,
    string Title,
    string Visibility,
    JsonElement LayoutJson,
    JsonElement WidgetBindingsJson,
    int? ExpectedVersion = null,
    IReadOnlyList<string>? AudienceRoles = null);

public sealed record PageDeleteResponse(bool Deleted);

