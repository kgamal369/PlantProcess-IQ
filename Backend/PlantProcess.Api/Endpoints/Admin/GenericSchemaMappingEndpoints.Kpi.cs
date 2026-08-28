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

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_KPI_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<IResult> CreateKpiViewAsync(
        [FromBody] KpiViewRequest request,
        PlantProcessDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await EnsureCatalogAsync(db, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ViewCode))
            return ApplicationProblems.Validation("viewCode is required.");

        if (string.IsNullOrWhiteSpace(request.ViewName))
            return ApplicationProblems.Validation("viewName is required.");

        if (string.IsNullOrWhiteSpace(request.KpiCode))
            return ApplicationProblems.Validation("kpiCode is required.");

        if (string.IsNullOrWhiteSpace(request.SqlText))
            return ApplicationProblems.Validation("sqlText is required.");

        var sql = NormalizeSelectSql(request.SqlText);
        var schema = CleanIdentifier(request.PhysicalSchema ?? "public", "physicalSchema");
        var physicalName = CleanIdentifier(
            string.IsNullOrWhiteSpace(request.PhysicalViewName)
                ? $"cv_{NormalizeCode(request.ViewCode)}"
                : request.PhysicalViewName!,
            "physicalViewName");

        var outputSchema = await PreviewSchemaOnlyAsync(db, sql, cancellationToken);
        await CreateOrReplaceViewAsync(db, schema, physicalName, sql, cancellationToken);

        var id = await UpsertCatalogAsync(
            db,
            request.ViewCode,
            request.ViewName,
            "KpiView",
            "KPI",
            schema,
            physicalName,
            sql,
            JsonSerializer.Serialize(outputSchema),
            request.MappingJson ?? JsonSerializer.Serialize(new
            {
                request.KpiCode,
                request.KpiName,
                request.KpiCategory,
                request.Unit
            }),
            "[]",
            request.AttachedScopeType,
            request.AttachedScopeCode,
            false,
            GetActor(user),
            "KPI-as-view registered by /admin/schema-mapping/kpi-views.",
            cancellationToken);

        await TryInsertKpiDefinitionAsync(db, request, id, cancellationToken);

        var row = await GetCatalogByIdAsync(db, id, cancellationToken);
        return Results.Ok(row);
    }

private static async Task TryInsertKpiDefinitionAsync(
        PlantProcessDbContext db,
        KpiViewRequest request,
        Guid schemaViewId,
        CancellationToken cancellationToken)
    {
        var exists = await QueryAsync(
            db,
            "SELECT to_regclass('ppiq_meta.kpi_definitions') AS table_name;",
            cancellationToken);

        if (exists.Count == 0 || exists[0]["table_name"] is null)
            return;

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO ppiq_meta.kpi_definitions
            (
                id,
                schema_view_definition_id,
                kpi_code,
                kpi_name,
                kpi_category,
                value_expression,
                unit,
                dimension_expression,
                filter_expression,
                aggregation_type,
                kpi_options_json,
                is_active,
                is_synthetic,
                source_system,
                source_record_id,
                created_at_utc,
                is_deleted
            )
            VALUES
            (
                gen_random_uuid(),
                NULL,
                @kpi_code,
                @kpi_name,
                @kpi_category,
                @value_expression,
                @unit,
                @dimension_expression,
                @filter_expression,
                @aggregation_type,
                CAST(@kpi_options_json AS jsonb)::text,
                true,
                @is_synthetic,
                'PlantProcessIQ.GenericSchemaMapping',
                @source_record_id,
                now(),
                false
            )
            ON CONFLICT DO NOTHING;
            """,
            cancellationToken,
            ("kpi_code", request.KpiCode.Trim()),
            ("kpi_name", (request.KpiName ?? request.ViewName).Trim()),
            ("kpi_category", request.KpiCategory ?? "Process"),
            ("value_expression", request.ValueExpression ?? "value"),
            ("unit", EmptyToNull(request.Unit)),
            ("dimension_expression", EmptyToNull(request.DimensionExpression)),
            ("filter_expression", EmptyToNull(request.FilterExpression)),
            ("aggregation_type", request.AggregationType ?? "Average"),
            ("kpi_options_json", request.KpiOptionsJson ?? "{}"),
            ("is_synthetic", request.IsSynthetic),
            ("source_record_id", schemaViewId.ToString()));
    }
}
