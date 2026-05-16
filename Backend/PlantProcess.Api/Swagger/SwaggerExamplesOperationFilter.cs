// ============================================================
// PHASE 0 — TASK 17
// FILE: Backend/PlantProcess.Api/Swagger/SwaggerExamplesOperationFilter.cs
//
// PURPOSE:
//   Adds practical Swagger request-body examples for key PlantProcess IQ
//   endpoints and groups endpoint tags in Swagger UI.
//
// IMPORTANT:
//   This version is compatible with newer Swashbuckle / Microsoft.OpenApi
//   packages where OpenAPI types are under Microsoft.OpenApi directly.
//   Do NOT use Microsoft.OpenApi.Any or Microsoft.OpenApi.Models here.
// ============================================================

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace PlantProcess.Api.Swagger;

public sealed class SwaggerExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody?.Content is null || operation.RequestBody.Content.Count == 0)
            return;

        var path = context.ApiDescription.RelativePath ?? string.Empty;
        var method = context.ApiDescription.HttpMethod?.ToUpperInvariant() ?? string.Empty;

        var example = ResolveExample(path, method);

        if (example is null)
            return;

        foreach (var mediaType in operation.RequestBody.Content.Values)
        {
            mediaType.Example = example;
        }
    }

    private static JsonNode? ResolveExample(string path, string method)
    {
        if (method != "POST" && method != "PUT" && method != "PATCH")
            return null;

        if (Contains(path, "analytics/dashboard/widgets/query"))
            return FromJson("""
            {
              "widgetType": "chart",
              "chartType": "bar",
              "dimensionCode": "DefectType",
              "measureCode": "DefectCount",
              "parameterCode": null,
              "filters": {
                "fromUtc": "2026-01-01T00:00:00Z",
                "toUtc": "2026-05-01T00:00:00Z",
                "sourceSystem": "SYNTHETIC_SEED"
              },
              "options": {
                "maxRows": 20,
                "rawRowLimit": 10000,
                "sortDirection": "desc",
                "includeWarnings": true
              }
            }
            """);

        if (Contains(path, "analytics/dashboard/workspace"))
            return FromJson("""
            {
              "siteId": null,
              "areaId": null,
              "equipmentId": null,
              "materialCode": null,
              "sourceSystem": "SYNTHETIC_SEED",
              "defectType": null,
              "riskClass": null,
              "fromUtc": "2026-01-01T00:00:00Z",
              "toUtc": "2026-05-01T00:00:00Z",
              "shiftCode": null,
              "page": 1,
              "pageSize": 25,
              "sortBy": "latestRiskScore",
              "sortDirection": "desc"
            }
            """);

        if (Contains(path, "analytics/dashboard/definitions") &&
            Contains(path, "widgets") &&
            !Contains(path, "clone") &&
            !Contains(path, "deactivate"))
            return FromJson("""
            {
              "widgetCode": "CUSTOM_DEFECT_BREAKDOWN",
              "widgetTitle": "Custom Defect Breakdown",
              "widgetType": "chart",
              "chartType": "bar",
              "dimensionCode": "DefectType",
              "measureCode": "DefectCount",
              "parameterCode": null,
              "filterJson": "{}",
              "layoutJson": "{}",
              "displayOptionsJson": "{}",
              "sortOrder": 10,
              "isActive": true
            }
            """);

        if (Contains(path, "analytics/dashboard/definitions") &&
            Contains(path, "layout"))
            return FromJson("""
            {
              "layoutJson": "{\"lg\":[{\"i\":\"defectTrend\",\"x\":0,\"y\":0,\"w\":6,\"h\":9}]}"
            }
            """);

        if (Contains(path, "integrations/sources"))
            return FromJson("""
            {
              "sourceSystemCode": "EXAMPLE_MES",
              "sourceSystemName": "Example MES System",
              "sourceSystemType": "MES",
              "description": "Synthetic source system used for PlantProcess IQ demo data.",
              "isReadOnlySource": true,
              "isSynthetic": true,
              "sourceSystem": "PlantProcessIQ.Swagger",
              "sourceRecordId": "swagger-example-source"
            }
            """);

        if (Contains(path, "integrations/batches"))
            return FromJson("""
            {
              "sourceSystemDefinitionId": "00000000-0000-0000-0000-000000000001",
              "importBatchCode": "DEMO_BATCH_2026_05_16",
              "description": "Demo import batch for smoke testing.",
              "isSynthetic": true,
              "sourceSystem": "PlantProcessIQ.Swagger",
              "sourceRecordId": "swagger-example-batch"
            }
            """);

        if (Contains(path, "integrations/mappings"))
            return FromJson("""
            {
              "sourceSystemDefinitionId": "00000000-0000-0000-0000-000000000001",
              "mappingCode": "DEMO_QUALITY_EVENT_MAPPING",
              "mappingName": "Demo Quality Event Mapping",
              "targetEntityType": "QualityEvent",
              "mappingVersion": "1.0",
              "mappingRulesJson": "{}",
              "description": "Maps synthetic quality rows into canonical quality events.",
              "isActive": true,
              "isSynthetic": true,
              "sourceSystem": "PlantProcessIQ.Swagger",
              "sourceRecordId": "swagger-example-mapping"
            }
            """);

        if (Contains(path, "data-quality/scan"))
            return FromJson("""
            {
              "maxCandidatesPerRule": 500,
              "requestedBy": "phase0-smoke-test",
              "correlationId": "phase0-data-quality-scan"
            }
            """);

        if (Contains(path, "analytics/risk-scores"))
            return FromJson("""
            {
              "siteId": null,
              "riskType": "OverallQualityRisk",
              "maxMaterials": 50,
              "storeResult": true,
              "requestedBy": "phase0-smoke-test",
              "correlationId": "phase0-risk-score"
            }
            """);

        return null;
    }

    private static bool Contains(string value, string expected)
    {
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonNode? FromJson(string json)
    {
        return JsonNode.Parse(json);
    }
}

public sealed class SwaggerTagGroupingOperationFilter : IOperationFilter
{
    private static readonly Dictionary<string, string[]> TagPrefixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["health"] = ["Health"],
        ["db-health"] = ["Health"],
        ["analytics/dashboard"] = ["Analytics — Dashboard"],
        ["analytics/correlations"] = ["Analytics — Correlation"],
        ["analytics/risk-scores"] = ["Analytics — Risk Scores"],
        ["analytics/features"] = ["Analytics — Feature Engineering"],
        ["integrations"] = ["Integration"],
        ["workflow"] = ["Workflow"],
        ["materials"] = ["Materials"],
        ["plant-layout"] = ["Plant Layout"],
        ["process"] = ["Process"],
        ["quality"] = ["Quality"],
        ["data-quality"] = ["Data Quality"],
        ["reporting"] = ["Reporting"],
        ["validation"] = ["Validation"],
        ["admin"] = ["Admin"],
        ["dev"] = ["Development"]
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;

        foreach (var item in TagPrefixMap)
        {
            if (!path.StartsWith(item.Key, StringComparison.OrdinalIgnoreCase))
                continue;

            operation.Tags = item.Value
                 .Select(tag => new OpenApiTagReference(tag))
                 .ToHashSet();

            return;
        }
    }
}