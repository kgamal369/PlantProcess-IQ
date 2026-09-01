using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Registry;

/// <summary>
/// T-092. Customer dimensions and measures are projections of published canonical
/// definition versions. This endpoint is deliberately read-only: definition_store
/// and definition_versions remain the lifecycle authority.
/// </summary>
public static class RegistryEndpoints
{
    private static readonly HashSet<string> GovernedAggregations = new(StringComparer.Ordinal)
    {
        "SampleMean",
        "TimeWeightedMean",
        "Integral",
        "Delta",
        "StateDuration",
        "Count",
        "Min",
        "Max",
        "Last",
        "Percentile",
        "WeightedMean"
    };

    public static IEndpointRouteBuilder MapRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/registry")
            .WithTags("Registry")
            .RequireAuthorization();

        group.MapGet("/metadata", GetMetadataAsync);
        group.MapGet("/fields", SearchFieldsAsync);

        return app;
    }

    private static async Task<IResult> GetMetadataAsync(
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TenantClaims.TryResolve(user, out var tenantId) || tenantId == Guid.Empty)
        {
            return Results.Json(new { error = "no_tenant" }, statusCode: StatusCodes.Status403Forbidden);
        }

        var projection = await ProjectAsync(db, tenantId, cancellationToken);
        return Results.Ok(projection);
    }

    private static async Task<IResult> SearchFieldsAsync(
        string? search,
        ClaimsPrincipal user,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TenantClaims.TryResolve(user, out var tenantId) || tenantId == Guid.Empty)
        {
            return Results.Json(new { error = "no_tenant" }, statusCode: StatusCodes.Status403Forbidden);
        }

        var projection = await ProjectAsync(db, tenantId, cancellationToken);
        var term = search?.Trim() ?? string.Empty;

        bool Matches(RegistryItem item) =>
            term.Length == 0 ||
            item.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            item.Label.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (item.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

        var fields = projection.Dimensions.Cast<RegistryItem>()
            .Concat(projection.Measures.Cast<RegistryItem>())
            .Where(Matches)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToArray();

        return Results.Ok(new
        {
            fields,
            refusals = projection.Refusals
        });
    }

    private static async Task<RegistryProjection> ProjectAsync(
        PlantProcessDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await ReadPublishedRowsAsync(db, tenantId, cancellationToken);
        var dimensions = new List<RegistryDimension>();
        var measures = new List<RegistryMeasure>();
        var refusals = new List<RegistryRefusal>();

        foreach (var row in rows)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(row.PayloadJson);
            }
            catch (JsonException)
            {
                refusals.Add(new RegistryRefusal(
                    row.DefinitionCode,
                    row.DefinitionKind,
                    row.VersionNumber,
                    "definition_payload_invalid",
                    "Published definition payload is not valid JSON."));
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                var grainCode = FirstString(
                    root,
                    "grainCode",
                    "analysisGrainCode") ?? NestedString(root, "grain", "code");

                if (string.IsNullOrWhiteSpace(grainCode))
                {
                    refusals.Add(new RegistryRefusal(
                        row.DefinitionCode,
                        row.DefinitionKind,
                        row.VersionNumber,
                        "GR01",
                        "analysis_grain_undeclared"));
                    continue;
                }

                var label = FirstString(root, "label", "displayName", "name") ?? row.Name;
                var description = FirstString(root, "description");
                var chartTypes = StringArray(root, "compatibleChartTypes");

                if (row.DefinitionKind == "master_dimension")
                {
                    var dataType = FirstString(root, "dataType", "valueType");
                    if (string.IsNullOrWhiteSpace(dataType))
                    {
                        refusals.Add(new RegistryRefusal(
                            row.DefinitionCode,
                            row.DefinitionKind,
                            row.VersionNumber,
                            "field_type_undeclared",
                            "Dimension data type is required."));
                        continue;
                    }

                    dimensions.Add(new RegistryDimension(
                        row.DefinitionCode,
                        label,
                        description,
                        grainCode,
                        dataType,
                        FirstString(root, "sourceField", "fieldCode", "column"),
                        Bool(root, "filterable", true),
                        Bool(root, "drillable", false),
                        chartTypes,
                        row.DefinitionId,
                        row.VersionNumber));

                    continue;
                }

                var aggregation = FirstString(root, "aggregation", "aggregationSemantics")
                    ?? NestedString(root, "signalSemantics", "aggregation");

                if (string.IsNullOrWhiteSpace(aggregation))
                {
                    refusals.Add(new RegistryRefusal(
                        row.DefinitionCode,
                        row.DefinitionKind,
                        row.VersionNumber,
                        "AG01",
                        "aggregation_semantics_undeclared"));
                    continue;
                }

                if (!GovernedAggregations.Contains(aggregation))
                {
                    refusals.Add(new RegistryRefusal(
                        row.DefinitionCode,
                        row.DefinitionKind,
                        row.VersionNumber,
                        "AG02",
                        "aggregation_semantics_incompatible"));
                    continue;
                }

                var signalCode = FirstString(root, "signalCode")
                    ?? NestedString(root, "signalSemantics", "signalCode");
                var referenceRole = FirstString(root, "referenceRole", "referenceDerivedRole");

                if (!string.Equals(aggregation, "Count", StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(signalCode) &&
                    string.IsNullOrWhiteSpace(referenceRole))
                {
                    refusals.Add(new RegistryRefusal(
                        row.DefinitionCode,
                        row.DefinitionKind,
                        row.VersionNumber,
                        "signal_semantics_undeclared",
                        "A non-count measure must declare a signal or a reference-derived role."));
                    continue;
                }

                measures.Add(new RegistryMeasure(
                    row.DefinitionCode,
                    label,
                    description,
                    grainCode,
                    aggregation,
                    signalCode,
                    FirstString(root, "unit", "unitCode"),
                    referenceRole,
                    FirstString(root, "referenceDefinitionCode", "referenceCode"),
                    Bool(root, "requiresParameter", false),
                    chartTypes,
                    row.DefinitionId,
                    row.VersionNumber));
            }
        }

        return new RegistryProjection(
            "canonical-definition-projection",
            dimensions.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray(),
            measures.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray(),
            refusals.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    private static async Task<IReadOnlyList<PublishedDefinitionRow>> ReadPublishedRowsAsync(
        PlantProcessDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
WITH published AS (
    SELECT
        d.id AS definition_id,
        d.definition_code,
        d.definition_kind,
        d.name,
        v.version_number,
        v.graph_json,
        ROW_NUMBER() OVER (
            PARTITION BY d.id
            ORDER BY v.version_number DESC, v.id DESC
        ) AS rn
    FROM ppiq_meta.definition_store d
    JOIN ppiq_meta.definition_versions v
      ON v.definition_id = d.id
     AND v.tenant_id = d.tenant_id
    WHERE d.tenant_id = @tenant_id
      AND d.is_deleted = false
      AND v.is_deleted = false
      AND v.status = 'published'
      AND d.definition_kind IN ('master_dimension', 'master_measure')
)
SELECT
    definition_id,
    definition_code,
    definition_kind,
    name,
    version_number,
    COALESCE(graph_json, '{}'::jsonb)::text
FROM published
WHERE rn = 1
ORDER BY definition_kind, definition_code;
""";

            var tenant = command.CreateParameter();
            tenant.ParameterName = "@tenant_id";
            tenant.Value = tenantId;
            command.Parameters.Add(tenant);

            var rows = new List<PublishedDefinitionRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PublishedDefinitionRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetString(5)));
            }

            return rows;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string? FirstString(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!.Trim();
            }
        }

        return null;
    }

    private static string? NestedString(JsonElement root, string parent, string child)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(parent, out var nested) ||
            nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return FirstString(nested, child);
    }

    private static bool Bool(JsonElement root, string name, bool fallback)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(name, out var value) &&
            (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
        {
            return value.GetBoolean();
        }

        return fallback;
    }

    private static string[] StringArray(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
            .Select(x => x.GetString()!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record PublishedDefinitionRow(
        Guid DefinitionId,
        string DefinitionCode,
        string DefinitionKind,
        string Name,
        int VersionNumber,
        string PayloadJson);

    public abstract record RegistryItem(
        string Code,
        string Label,
        string? Description,
        string GrainCode,
        Guid DefinitionId,
        int DefinitionVersion);

    public sealed record RegistryDimension(
        string Code,
        string Label,
        string? Description,
        string GrainCode,
        string DataType,
        string? SourceField,
        bool Filterable,
        bool Drillable,
        string[] CompatibleChartTypes,
        Guid DefinitionId,
        int DefinitionVersion)
        : RegistryItem(Code, Label, Description, GrainCode, DefinitionId, DefinitionVersion);

    public sealed record RegistryMeasure(
        string Code,
        string Label,
        string? Description,
        string GrainCode,
        string Aggregation,
        string? SignalCode,
        string? Unit,
        string? ReferenceRole,
        string? ReferenceDefinitionCode,
        bool RequiresParameter,
        string[] CompatibleChartTypes,
        Guid DefinitionId,
        int DefinitionVersion)
        : RegistryItem(Code, Label, Description, GrainCode, DefinitionId, DefinitionVersion);

    public sealed record RegistryRefusal(
        string Code,
        string DefinitionKind,
        int DefinitionVersion,
        string ErrorCode,
        string Message);

    public sealed record RegistryProjection(
        string Authority,
        IReadOnlyList<RegistryDimension> Dimensions,
        IReadOnlyList<RegistryMeasure> Measures,
        IReadOnlyList<RegistryRefusal> Refusals);
}
