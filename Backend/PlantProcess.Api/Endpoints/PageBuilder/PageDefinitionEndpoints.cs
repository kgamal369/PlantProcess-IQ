using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;
using PlantProcess.Application.Definitions;
using Microsoft.EntityFrameworkCore.Storage;
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

        // PPIQ T-042. Publication is its own action, not a field on the upsert.
        // Sent through the upsert it would be indistinguishable from omission,
        // and a page could never be un-published without a second meaning for
        // null. Two verbs, one column, no ambiguity.
        group.MapPost("/{slug}/publish", PublishPageAsync);
        group.MapPost("/{slug}/unpublish", UnpublishPageAsync);

        return app;
    }

    /// PPIQ T-042 S6. THE WORKSPACE PROJECTION NEEDS TO SEE DELETED PAGES.
    ///
    /// Not to show them - to tell a dashboard that NEVER had a page from one
    /// whose page was deleted. Without that distinction a soft-deleted page
    /// stops being returned, its backing dashboard loses its join, the
    /// projection reads it as a seeded workspace and it RESURRECTS in
    /// navigation. The Page Builder listing keeps the default and never sees
    /// them.
    private static async Task<IResult> ListPagesAsync(
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        bool? includeDeleted,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        // The DbContext owns this connection. An earlier shape disposed it per
        // handler, which is survivable alone but fatal once the canonical write
        // and the serving write share one transaction.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id, tenant_id, slug, title, owner_user_name, visibility, version,
                   layout_json::text, widget_bindings_json::text, updated_at_utc,
                   audience_roles::text, backing_dashboard_definition_id, published_at_utc, is_deleted
            FROM ppiq_meta.page_definitions
            WHERE tenant_id = @tenant
              AND (@include_deleted OR is_deleted = false)
              AND (owner_user_name = @owner OR visibility IN ('Shared', 'Public'))
            ORDER BY updated_at_utc DESC, title ASC;
            """,
            connection);

        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("include_deleted", includeDeleted == true);

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

        // The DbContext owns this connection. An earlier shape disposed it per
        // handler, which is survivable alone but fatal once the canonical write
        // and the serving write share one transaction.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id, tenant_id, slug, title, owner_user_name, visibility, version,
                   layout_json::text, widget_bindings_json::text, updated_at_utc,
                   audience_roles::text, backing_dashboard_definition_id, published_at_utc, is_deleted
            FROM ppiq_meta.page_definitions
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
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
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

        // The DbContext owns this connection. An earlier shape disposed it per
        // handler, which is survivable alone but fatal once the canonical write
        // and the serving write share one transaction.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO ppiq_meta.page_definitions
                (tenant_id, slug, title, owner_user_name, visibility, audience_roles, backing_dashboard_definition_id, layout_json, widget_bindings_json, updated_at_utc)
            VALUES
                (@tenant, @slug, @title, @owner, @visibility, COALESCE(@audience_roles, '[]'::jsonb), @backing_dashboard, @layout_json, @widget_bindings_json, now())
            ON CONFLICT (tenant_id, slug)
            WHERE is_deleted = false
            DO UPDATE SET
                title = EXCLUDED.title,
                owner_user_name = EXCLUDED.owner_user_name,
                visibility = EXCLUDED.visibility,
                audience_roles = COALESCE(@audience_roles, page_definitions.audience_roles, '[]'::jsonb),
                backing_dashboard_definition_id = COALESCE(@backing_dashboard, page_definitions.backing_dashboard_definition_id),
                layout_json = EXCLUDED.layout_json,
                widget_bindings_json = EXCLUDED.widget_bindings_json,
                version = page_definitions.version + 1,
                updated_at_utc = now()
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
                      layout_json::text, widget_bindings_json::text, updated_at_utc,
                      audience_roles::text, backing_dashboard_definition_id, published_at_utc, is_deleted;
            """,
            connection);

        AddPageParameters(command, request, tenant, owner);

        // T-090. The serving row and the canonical definition are ONE decision.
        // Before this, the endpoint incremented its own version column and was a
        // second version authority; now page_definitions.version is a serving
        // projection of a canonical immutable version.
        return await PageCanonicalConvergence.InOneUnitOfWorkAsync(
            db,
            async () =>
            {
                command.Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                return Results.Ok(ReadDto(reader));
            },
            () => PageCanonicalConvergence.WritePageDefinitionAsync(
                canonical, identity, tenant, owner, request.Slug.Trim(), request.Title.Trim(),
                request, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> UpdatePageAsync(
        string slug,
        [FromBody] UpsertPageDefinitionRequest request,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
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

        // The DbContext owns this connection. An earlier shape disposed it per
        // handler, which is survivable alone but fatal once the canonical write
        // and the serving write share one transaction.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        if (normalized.ExpectedVersion is int expectedVersion)
        {
            await using var conflictCheck = new NpgsqlCommand(
                "SELECT version, owner_user_name, updated_at_utc FROM ppiq_meta.page_definitions " +
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
            UPDATE ppiq_meta.page_definitions
            SET title = @title,
                visibility = @visibility,
                audience_roles = COALESCE(@audience_roles, audience_roles),
                backing_dashboard_definition_id = COALESCE(@backing_dashboard, backing_dashboard_definition_id),
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
                      audience_roles::text, backing_dashboard_definition_id, published_at_utc, is_deleted;
            """,
            connection);

        AddPageParameters(command, normalized, tenant, owner);
        command.Parameters.Add(new NpgsqlParameter("expected_version", NpgsqlDbType.Integer)
        {
            Value = normalized.ExpectedVersion.HasValue
                ? normalized.ExpectedVersion.Value
                : DBNull.Value
        });

        return await PageCanonicalConvergence.InOneUnitOfWorkAsync(
            db,
            async () =>
            {
                command.Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return ApplicationProblems.NotFound(
                        "Page '" + slug + "' was not found or is not owned by '" + owner + "'.");
                }

                return Results.Ok(ReadDto(reader));
            },
            () => PageCanonicalConvergence.WritePageDefinitionAsync(
                canonical, identity, tenant, owner, normalized.Slug.Trim(), normalized.Title.Trim(),
                normalized, cancellationToken),
            cancellationToken);
    }

    private static Task<IResult> PublishPageAsync(
        string slug,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        return SetPublicationAsync(slug, user, db, canonical, identity, publish: true, cancellationToken);
    }

    private static Task<IResult> UnpublishPageAsync(
        string slug,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        return SetPublicationAsync(slug, user, db, canonical, identity, publish: false, cancellationToken);
    }

    /// PPIQ T-042. A page may not be published without a backing dashboard,
    /// because Workspaces routes to that dashboard's code. Publishing a page
    /// that points at nothing would put a dead entry in a customer's navigation.
    private static async Task<IResult> SetPublicationAsync(
        string slug,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        bool publish,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        // The DbContext owns this connection. An earlier shape disposed it per
        // handler, which is survivable alone but fatal once the canonical write
        // and the serving write share one transaction.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        if (publish)
        {
            await using var guard = new NpgsqlCommand(
                "SELECT backing_dashboard_definition_id FROM ppiq_meta.page_definitions "
                + "WHERE tenant_id = @tenant AND slug = @slug AND is_deleted = false LIMIT 1;",
                connection);
            guard.Parameters.AddWithValue("tenant", tenant);
            guard.Parameters.AddWithValue("slug", slug);

            var backing = await guard.ExecuteScalarAsync(cancellationToken);

            if (backing is null || backing is DBNull)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["backingDashboardDefinitionId"] = new[]
                    {
                        "This page has no backing workspace yet, so publishing it would put an entry "
                        + "in navigation that opens nothing. Add a widget first, then publish."
                    }
                });
            }
        }

        await using var command = new NpgsqlCommand(
            """
            UPDATE ppiq_meta.page_definitions
            SET published_at_utc = @published,
                version = version + 1,
                updated_at_utc = now()
            WHERE tenant_id = @tenant
              AND slug = @slug
              AND owner_user_name = @owner
              AND is_deleted = false
            RETURNING id, tenant_id, slug, title, owner_user_name, visibility, version,
                      layout_json::text, widget_bindings_json::text, updated_at_utc,
                      audience_roles::text, backing_dashboard_definition_id, published_at_utc, is_deleted;
            """,
            connection);

        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.Add(new NpgsqlParameter("published", NpgsqlDbType.TimestampTz)
        {
            Value = publish ? DateTimeOffset.UtcNow : (object)DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(ReadDto(reader));
    }

    private static async Task<IResult> DeletePageAsync(
        string slug,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(db, cancellationToken);

        var tenant = ResolveTenant(user);
        var owner = ResolveUserName(user);

        // The DbContext owns this connection. An earlier shape disposed it per
        // handler, which is survivable alone but fatal once the canonical write
        // and the serving write share one transaction.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            UPDATE ppiq_meta.page_definitions
            SET is_deleted = true,
                -- A deleted page is not a published one. The BACKING RELATION is
                -- deliberately kept: it is the only thing that stops the orphaned
                -- dashboard from later passing as a seeded workspace.
                published_at_utc = NULL,
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

        // T-090. Soft delete hides the serving row and withdraws canonical
        // publication. Published version history is NOT removed: it is the
        // replay evidence for everything that ran while the page existed.
        return await PageCanonicalConvergence.InOneUnitOfWorkAsync(
            db,
            async () =>
            {
                command.Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
                var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                return Results.Ok(new PageDeleteResponse(affected > 0));
            },
            () => PageCanonicalConvergence.RetirePageDefinitionAsync(
                canonical, identity, tenant, slug, cancellationToken),
            cancellationToken);
    }

    private static async Task EnsureSchemaAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        // Schema creation/evolution belongs to canonical migration/topology
        // authority. HTTP handlers only assert governed storage exists.
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass('ppiq_meta.page_definitions') IS NOT NULL;",
            connection);

        var exists = await command.ExecuteScalarAsync(cancellationToken);
        if (exists is not true)
        {
            throw new InvalidOperationException(
                "Governed page storage ppiq_meta.page_definitions is missing. " +
                "Apply canonical database migrations/topology convergence before serving /pages.");
        }
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

        // PPIQ T-042. The typed link to the operational dashboard this page is
        // backed by. It is an id, not a naming convention: a code can be renamed
        // and a convention can be broken, and the page would then point at
        // nothing while still looking correct. Omission preserves, exactly as
        // the audience does - an old caller must not unlink a page.
        command.Parameters.Add(
            new NpgsqlParameter("backing_dashboard", NpgsqlDbType.Uuid)
            {
                Value = request.BackingDashboardDefinitionId.HasValue
                    ? request.BackingDashboardDefinitionId.Value
                    : (object)DBNull.Value
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
            ReadAudienceRoles(reader, 10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.GetBoolean(13),
            reader.IsDBNull(12) ? null : ReadDateTimeOffset(reader, 12));
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
    IReadOnlyList<string> AudienceRoles,
    Guid? BackingDashboardDefinitionId,
    /// True only in the projection that asked for deleted rows. The Page
    /// Builder listing never sees one.
    bool IsDeleted,
    // PPIQ T-042. Null is a draft. Publication is not visibility and not
    // audience: it answers whether this authored page is eligible to appear as
    // a Workspace at all. It is deliberately NOT the backing dashboard's
    // IsActive flag, because that dashboard must be active for widgets to be
    // saved into it long before the page is ready to be seen.
    DateTimeOffset? PublishedAtUtc);

public sealed record UpsertPageDefinitionRequest(
    string Slug,
    string Title,
    string Visibility,
    JsonElement LayoutJson,
    JsonElement WidgetBindingsJson,
    int? ExpectedVersion = null,
    IReadOnlyList<string>? AudienceRoles = null,
    Guid? BackingDashboardDefinitionId = null);

public sealed record PageDeleteResponse(bool Deleted);

/// <summary>
/// PPIQ T-090. Canonical convergence for the Page write paths.
///
/// WHAT THIS DOES NOT CHANGE. Routes, DTOs, audience roles, backing-dashboard
/// semantics, optimistic version conflict, soft delete and the publish/unpublish
/// shape are all preserved exactly. ppiq_meta.page_definitions stays the
/// operational serving row and EnsureSchemaAsync stays a read-only existence
/// assertion - the T-204 corrections are upstream of this and are not touched.
///
/// WHAT IT CHANGES. Every semantic page write now also writes canonical
/// identity and an immutable version, inside ONE transaction with the serving
/// row. Before T-090 the endpoint owned page semantics outright and incremented
/// its own version column, which was a second version authority.
///
/// page_definitions.version REMAINS, because the HTTP contract exposes it for
/// optimistic concurrency. It is now a serving projection of the canonical
/// version rather than an independent sequence, and a gate proves the two agree.
/// </summary>
internal static class PageCanonicalConvergence
{
    /// <summary>
    /// Runs a page write and its canonical counterpart as one unit of work.
    /// The canonical writer refuses to mutate without an ambient transaction,
    /// so this is what makes the page path legal rather than merely tidy.
    /// </summary>
    internal static async Task<IResult> InOneUnitOfWorkAsync(
        PlantProcessDbContext db,
        Func<Task<IResult>> servingWrite,
        Func<Task<bool>> canonicalWrite,
        CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            var nested = await servingWrite();
            return await canonicalWrite() ? nested : CanonicalRefused();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var result = await servingWrite();

        if (!await canonicalWrite())
        {
            await transaction.RollbackAsync(cancellationToken);
            return CanonicalRefused();
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Writes the canonical page definition and version for one slug. Returns
    /// false rather than throwing so the caller rolls back deliberately.
    /// </summary>
    internal static async Task<bool> WritePageDefinitionAsync(
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        string tenantCode,
        string ownerUserName,
        string slug,
        string title,
        UpsertPageDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        // The claim NARROWS identity when it matches; it is not a second
        // identity authority. When the named code resolves nothing and exactly
        // one tenant (or user) is resolvable without it, that unambiguous
        // identity is the answer. Multi-tenant ambiguity still refuses inside
        // the resolver itself.
        var tenantId = await identity.ResolveTenantAsync(tenantCode, cancellationToken)
                       ?? await identity.ResolveTenantAsync(null, cancellationToken);
        var ownerId = await identity.ResolveOwnerAsync(ownerUserName, cancellationToken)
                      ?? await identity.ResolveOwnerAsync(null, cancellationToken);

        // Unknown identity refuses. A page written under an invented owner
        // would look governed and be untraceable.
        if (tenantId is null || ownerId is null)
        {
            return false;
        }

        var detail = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["layout_json"] = JsonSerializer.Serialize(request.LayoutJson),
            ["audience_roles"] = JsonSerializer.Serialize(request.AudienceRoles ?? Array.Empty<string>()),
            ["default_filters"] = JsonSerializer.Serialize(request.WidgetBindingsJson),
        };

        var written = await canonical.WriteVersionAsync(
            new CanonicalDefinitionWrite(
                DefinitionKind.Page,
                tenantId.Value,
                ownerId.Value,
                slug,
                title,
                JsonSerializer.Serialize(new
                {
                    slug,
                    title,
                    visibility = request.Visibility,
                    audienceRoles = request.AudienceRoles ?? Array.Empty<string>(),
                    backingDashboardDefinitionId = request.BackingDashboardDefinitionId,
                    layoutJson = request.LayoutJson,
                    widgetBindingsJson = request.WidgetBindingsJson,
                }),
                CanonicalVersionStatus.Published,
                detail),
            cancellationToken);

        return written.IsSuccess;
    }

    /// <summary>
    /// A page delete hides the serving row and withdraws canonical publication.
    /// The versions are NOT removed: they are the replay evidence for everything
    /// that ran while the page existed, and deleting them would make an old
    /// execution snapshot unresolvable. Only the publication moves.
    ///
    /// An earlier draft of this method returned a bare true. It looked like a
    /// retirement path, implemented nothing, and passed every static check -
    /// which is why the canonical contract now carries an explicit RetireAsync
    /// rather than leaving each caller to improvise one.
    /// </summary>
    internal static async Task<bool> RetirePageDefinitionAsync(
        ICanonicalDefinitionWriter canonical,
        ICanonicalIdentityResolver identity,
        string tenantCode,
        string slug,
        CancellationToken cancellationToken)
    {
        var tenantId = await identity.ResolveTenantAsync(tenantCode, cancellationToken);
        if (tenantId is null)
        {
            return false;
        }

        var found = await canonical.FindByCodeAsync(tenantId.Value, slug, cancellationToken);
        if (found.IsFailure)
        {
            return false;
        }

        // A page deleted before it ever reached the canonical store has nothing
        // to retire, and the desired end state already holds.
        if (found.Value is null)
        {
            return true;
        }

        var retired = await canonical.RetireAsync(found.Value.Value, cancellationToken);
        return retired.IsSuccess;
    }

    private static IResult CanonicalRefused() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["definition"] = new[]
            {
                "The canonical definition authority refused this page write, so nothing was saved. "
                + "This usually means the tenant or owner identity could not be resolved."
            }
        });
}
