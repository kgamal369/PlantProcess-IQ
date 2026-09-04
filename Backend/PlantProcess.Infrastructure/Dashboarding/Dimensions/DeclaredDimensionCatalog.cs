using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Dashboarding.Dimensions;

/// <summary>
/// Projects the tenant's PUBLISHED master_dimension definitions into executable
/// declarations, validated against the EF model at load time.
///
/// Governance is structural, not lexical. A declaration is bindable only when its
/// sourceCatalog names a mapped canonical entity and its sourceField names a mapped,
/// non-key, non-shadow string member of that entity. No allowlist of names exists
/// anywhere: the EF model is the authority for what can be bound, and the definition
/// store is the authority for what has been declared.
/// </summary>
public sealed class DeclaredDimensionCatalog : IDeclaredDimensionCatalog
{
    private readonly PlantProcessDbContext _db;

    public DeclaredDimensionCatalog(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DeclaredDimension>> GetPublishedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await ReadPublishedDimensionRowsAsync(tenantId, cancellationToken);
        var result = new List<DeclaredDimension>(rows.Count);

        foreach (var row in rows)
        {
            var declared = Project(row);
            if (declared is not null) result.Add(declared);
        }

        return result;
    }

    public async Task<DeclaredDimension?> FindAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var all = await GetPublishedAsync(tenantId, cancellationToken);
        return all.FirstOrDefault(d => string.Equals(d.Code, code.Trim(), StringComparison.Ordinal));
    }

    private DeclaredDimension? Project(PublishedRow row)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;

            var label = First(root, "label", "displayName", "name") ?? row.Name;
            var dataType = First(root, "dataType", "valueType") ?? "string";
            var grainCode = First(root, "grainCode", "analysisGrainCode") ?? Nested(root, "grain", "code") ?? string.Empty;
            var sourceCatalog = First(root, "sourceCatalog", "sourceEntity", "entity") ?? string.Empty;
            var sourceField = First(root, "sourceField", "fieldCode", "column") ?? string.Empty;

            Type? entityType = null;
            string? refusalCode = null;
            string? refusalReason = null;

            if (string.IsNullOrWhiteSpace(sourceCatalog) || string.IsNullOrWhiteSpace(sourceField))
            {
                refusalCode = DimensionBindingRefusalCodes.Unbindable;
                refusalReason = "the declaration carries no sourceCatalog/sourceField binding.";
            }
            else
            {
                var candidate = _db.Model.GetEntityTypes()
                    .FirstOrDefault(e => string.Equals(e.ClrType.Name, sourceCatalog.Trim(), StringComparison.Ordinal));

                if (candidate is null)
                {
                    refusalCode = DimensionBindingRefusalCodes.Unbindable;
                    refusalReason = "sourceCatalog '" + sourceCatalog + "' is not a mapped canonical entity.";
                }
                else
                {
                    var property = candidate.FindProperty(sourceField.Trim());

                    if (property is null || property.IsShadowProperty())
                    {
                        refusalCode = DimensionBindingRefusalCodes.Unbindable;
                        refusalReason = "sourceField '" + sourceField + "' is not a mapped member of " + candidate.ClrType.Name + ".";
                    }
                    else if (property.ClrType != typeof(string))
                    {
                        refusalCode = DimensionBindingRefusalCodes.Unbindable;
                        refusalReason = "sourceField '" + sourceField + "' is not a text member; only text members bind to a categorical dimension in this contract.";
                    }
                    else if (property.IsKey() || property.IsForeignKey())
                    {
                        refusalCode = DimensionBindingRefusalCodes.Unbindable;
                        refusalReason = "sourceField '" + sourceField + "' is an identity or reference member and is not a categorical value.";
                    }
                    else
                    {
                        entityType = candidate.ClrType;
                    }
                }
            }

            return new DeclaredDimension(
                row.DefinitionCode,
                label,
                dataType,
                grainCode,
                sourceCatalog,
                sourceField,
                entityType,
                entityType is not null,
                refusalCode,
                refusalReason,
                row.DefinitionId,
                row.VersionNumber);
        }
    }

    private static string? First(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(name, out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
        }

        return null;
    }

    private static string? Nested(JsonElement root, string objectName, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(objectName, out var nested) &&
            nested.ValueKind == JsonValueKind.Object)
        {
            return First(nested, propertyName);
        }

        return null;
    }

    private sealed record PublishedRow(
        Guid DefinitionId,
        string DefinitionCode,
        string Name,
        int VersionNumber,
        string PayloadJson);

    private async Task<IReadOnlyList<PublishedRow>> ReadPublishedDimensionRowsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
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
      AND d.definition_kind = 'master_dimension'
)
SELECT
    definition_id,
    definition_code,
    name,
    version_number,
    COALESCE(graph_json, '{}'::jsonb)::text
FROM published
WHERE rn = 1
ORDER BY definition_code
""";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenant_id";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);

            var rows = new List<PublishedRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PublishedRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.GetInt32(3),
                    reader.IsDBNull(4) ? "{}" : reader.GetString(4)));
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
}