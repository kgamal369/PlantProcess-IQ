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

/// <summary>
/// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT
/// Thin route registration surface. Runtime implementation was decomposed into cohesive partial files.
/// </summary>
public static partial class GenericSchemaMappingEndpoints
{
private static readonly Regex SafeIdentifier = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DangerousSql = new(
        @"\b(insert|update|delete|drop|alter|truncate|grant|revoke|copy|execute|exec|merge|vacuum|analyze|call|do|listen|notify|set|reset|prepare|deallocate|create\s+(?!or\s+replace\s+view)|pg_read_file|pg_sleep|dblink|xp_cmdshell|openrowset|opendatasource)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

public static IEndpointRouteBuilder MapGenericSchemaMappingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/schema-mapping")
            .WithTags("Admin - Generic Schema Mapping")
            .RequireAuthorization("PlantProcessDataManager");

        group.MapGet("/catalog", GetCatalogAsync)
            .WithName("GetCanonicalSchemaViewCatalog")
            .WithSummary("PPIQ-T107: list canonical schema view catalog");

        group.MapPost("/catalog/register", RegisterCanonicalViewAsync)
            .WithName("RegisterCanonicalSchemaView")
            .WithSummary("PPIQ-T107: register and validate a canonical schema view");

        group.MapPost("/resolve", ResolveSchemaViewAsync)
            .WithName("ResolveCanonicalSchemaView")
            .WithSummary("PPIQ-T108: resolve a widget/mapping target to an approved physical view");

        group.MapPost("/joins/preview", PreviewJoinAsync)
            .WithName("PreviewGenericCrossSourceJoin")
            .WithSummary("PPIQ-T110: preview a cross-source join");

        group.MapPost("/joins/materialize", MaterializeJoinAsync)
            .WithName("MaterializeGenericCrossSourceJoin")
            .WithSummary("PPIQ-T110: materialize a cross-source join as a canonical view");

        group.MapPost("/kpi-views", CreateKpiViewAsync)
            .WithName("CreateGenericKpiView")
            .WithSummary("PPIQ-T111: create KPI-as-view and attach it to equipment/area/process");

        group.MapPost("/execute/{viewCode}", ExecuteMappingAsync)
            .WithName("ExecuteCanonicalSchemaMapping")
            .WithSummary("PPIQ-T112: execute/refresh a saved mapping view and log row counts");

        group.MapGet("/readiness", GetReadinessAsync)
            .WithName("GetGenericSchemaMappingReadiness")
            .WithSummary("PPIQ-T107-T112 readiness summary");

        return app;
    }
}
