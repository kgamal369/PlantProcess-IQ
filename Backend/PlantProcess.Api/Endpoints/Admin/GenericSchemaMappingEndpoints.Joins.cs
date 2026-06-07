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

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_JOIN_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<IResult> PreviewJoinAsync(
        [FromBody] CrossSourceJoinRequest request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        var sql = BuildJoinSql(request, includeLimit: true);
        var preview = await PreviewRowsAsync(db, sql, request.MaxRows ?? 100, cancellationToken);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Cross-source join preview executed.",
            sqlText = sql,
            preview.rowCount,
            preview.columns,
            preview.rows
        });
    }

private static async Task<IResult> MaterializeJoinAsync(
        [FromBody] MaterializeJoinRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ViewCode))
            return ApplicationProblems.Validation("viewCode is required.");

        if (string.IsNullOrWhiteSpace(request.ViewName))
            return ApplicationProblems.Validation("viewName is required.");

        var sql = BuildJoinSql(request.Join, includeLimit: false);
        var physicalName = CleanIdentifier(
            string.IsNullOrWhiteSpace(request.PhysicalViewName)
                ? $"cv_{NormalizeCode(request.ViewCode)}"
                : request.PhysicalViewName!,
            "physicalViewName");

        var schema = CleanIdentifier(request.PhysicalSchema ?? "public", "physicalSchema");
        var outputSchema = await PreviewSchemaOnlyAsync(db, sql, cancellationToken);

        await CreateOrReplaceViewAsync(db, schema, physicalName, sql, cancellationToken);

        var id = await UpsertCatalogAsync(
            db,
            request.ViewCode,
            request.ViewName,
            "JoinView",
            request.TargetEntity ?? "MappingPreparation",
            schema,
            physicalName,
            sql,
            JsonSerializer.Serialize(outputSchema),
            request.MappingJson ?? "{}",
            request.SourceDatasetIdsJson ?? "[]",
            request.AttachedScopeType,
            request.AttachedScopeCode,
            false,
            GetActor(user),
            "Materialized by /admin/schema-mapping/joins/materialize.",
            cancellationToken);

        var row = await GetCatalogByIdAsync(db, id, cancellationToken);
        return Results.Ok(row);
    }

private static string BuildJoinSql(CrossSourceJoinRequest request, bool includeLimit)
    {
        var leftSchema = CleanIdentifier(request.LeftSchema, "leftSchema");
        var leftTable = CleanIdentifier(request.LeftTable, "leftTable");
        var rightSchema = CleanIdentifier(request.RightSchema, "rightSchema");
        var rightTable = CleanIdentifier(request.RightTable, "rightTable");
        var leftJoinColumn = CleanIdentifier(request.LeftJoinColumn, "leftJoinColumn");
        var rightJoinColumn = CleanIdentifier(request.RightJoinColumn, "rightJoinColumn");

        var selected = request.Columns.Count == 0
            ? new[]
            {
                new JoinColumnSelection("left", leftJoinColumn, "left_join_key"),
                new JoinColumnSelection("right", rightJoinColumn, "right_join_key")
            }
            : request.Columns;

        var columns = selected.Select((c, index) =>
        {
            var side = string.Equals(c.Side, "right", StringComparison.OrdinalIgnoreCase) ? "r" : "l";
            var column = CleanIdentifier(c.Column, $"columns[{index}].column");
            var alias = CleanIdentifier(
                string.IsNullOrWhiteSpace(c.Alias) ? $"{side}_{column}" : c.Alias!,
                $"columns[{index}].alias");

            return $"{side}.{QuoteIdentifier(column)} AS {QuoteIdentifier(alias)}";
        });

        var maxRows = Math.Clamp(request.MaxRows ?? 100, 1, 5000);
        var limit = includeLimit ? $" LIMIT {maxRows}" : "";

        return $"""
        SELECT
            {string.Join("," + Environment.NewLine + "            ", columns)}
        FROM {QuoteIdentifier(leftSchema)}.{QuoteIdentifier(leftTable)} l
        JOIN {QuoteIdentifier(rightSchema)}.{QuoteIdentifier(rightTable)} r
            ON l.{QuoteIdentifier(leftJoinColumn)} = r.{QuoteIdentifier(rightJoinColumn)}
        {limit}
        """;
    }
}
