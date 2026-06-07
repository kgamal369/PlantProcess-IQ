using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RESOLVER_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<IResult> ResolveSchemaViewAsync(
        [FromBody] ResolveSchemaViewRequest request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ViewCode) && string.IsNullOrWhiteSpace(request.TargetEntity))
        {
            return Results.BadRequest(new
            {
                errorType = "NoSuchView",
                message = "Provide either viewCode or targetEntity.",
                request.ViewCode,
                request.TargetEntity
            });
        }

        var sql = """
        SELECT
            id,
            view_code,
            view_name,
            view_kind,
            target_entity,
            physical_schema,
            physical_view_name,
            sql_text,
            output_schema_json::text,
            mapping_json::text,
            source_dataset_ids::text,
            attached_scope_type,
            attached_scope_code,
            is_registered,
            is_approved,
            is_active,
            is_system_seed,
            last_validated_at_utc,
            last_validation_status,
            last_validation_message,
            last_executed_at_utc,
            last_execution_status,
            last_execution_message,
            last_execution_row_count,
            created_by,
            created_at_utc,
            updated_at_utc
        FROM public.canonical_schema_views
        WHERE is_deleted = false
          AND is_active = true
          AND is_approved = true
          AND (
                (@view_code IS NOT NULL AND lower(view_code) = lower(@view_code))
             OR (@target_entity IS NOT NULL AND lower(target_entity) = lower(@target_entity))
          )
        ORDER BY
            CASE WHEN lower(view_code) = lower(COALESCE(@view_code, '')) THEN 0 ELSE 1 END,
            updated_at_utc DESC NULLS LAST,
            created_at_utc DESC
        LIMIT 1;
        """;

        var rows = await QueryAsync(
            db,
            sql,
            cancellationToken,
            ("view_code", EmptyToNull(request.ViewCode)),
            ("target_entity", EmptyToNull(request.TargetEntity)));

        if (rows.Count == 0)
        {
            return Results.NotFound(new
            {
                errorType = "NoSuchView",
                message = "No active approved canonical schema view matched the resolver request.",
                request.ViewCode,
                request.TargetEntity
            });
        }

        var resolved = rows[0];

        var resolverColumnError = ValidateRequestedResolverColumns(request, Convert.ToString(resolved["output_schema_json"]) ?? "[]");
        if (resolverColumnError is not null)
            return Results.BadRequest(resolverColumnError);

        var qualifiedName = $"{resolved["physical_schema"]}.{resolved["physical_view_name"]}";

        return Results.Ok(new
        {
            isResolved = true,
            message = "Schema view resolved.",
            qualifiedName,
            view = resolved
        });
    }

private static object? ValidateRequestedResolverColumns(
        ResolveSchemaViewRequest request,
        string outputSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(outputSchemaJson))
            return null;

        using var document = JsonDocument.Parse(outputSchemaJson);

        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var name = item.TryGetProperty("columnName", out var c1)
                    ? c1.GetString()
                    : item.TryGetProperty("ColumnName", out var c2)
                        ? c2.GetString()
                        : item.TryGetProperty("name", out var c3)
                            ? c3.GetString()
                            : null;

                var type = item.TryGetProperty("dataType", out var t1)
                    ? t1.GetString()
                    : item.TryGetProperty("DataType", out var t2)
                        ? t2.GetString()
                        : item.TryGetProperty("type", out var t3)
                            ? t3.GetString()
                            : "unknown";

                if (!string.IsNullOrWhiteSpace(name))
                    columns[name.Trim()] = string.IsNullOrWhiteSpace(type) ? "unknown" : type!;
            }
        }

        var dimension = request.DimensionCode?.Trim();
        if (!string.IsNullOrWhiteSpace(dimension) && !columns.ContainsKey(dimension))
        {
            return new
            {
                errorType = "NoSuchColumn",
                message = $"Dimension column ''{dimension}'' does not exist in the resolved canonical view.",
                offendingColumn = dimension
            };
        }

        var measure = request.MeasureCode?.Trim();
        if (string.IsNullOrWhiteSpace(measure))
            return null;

        var aggregate = "raw";
        var column = measure;

        var match = Regex.Match(measure, @"^(?<agg>sum|avg|mean|min|max|count)\((?<col>[A-Za-z_][A-Za-z0-9_]*)\)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            aggregate = match.Groups["agg"].Value.ToLowerInvariant();
            column = match.Groups["col"].Value;
        }

        if (!columns.TryGetValue(column, out var dataType))
        {
            return new
            {
                errorType = "NoSuchColumn",
                message = $"Measure column ''{column}'' does not exist in the resolved canonical view.",
                offendingColumn = column
            };
        }

        var numeric = dataType.Contains("int", StringComparison.OrdinalIgnoreCase) ||
                      dataType.Contains("numeric", StringComparison.OrdinalIgnoreCase) ||
                      dataType.Contains("decimal", StringComparison.OrdinalIgnoreCase) ||
                      dataType.Contains("double", StringComparison.OrdinalIgnoreCase) ||
                      dataType.Contains("real", StringComparison.OrdinalIgnoreCase);

        if ((aggregate is "sum" or "avg" or "mean") && !numeric)
        {
            return new
            {
                errorType = "InvalidAggregateForType",
                message = $"Aggregate ''{aggregate}'' is not valid for non-numeric column ''{column}''.",
                offendingColumn = column,
                dataType
            };
        }

        return null;
    }
}
