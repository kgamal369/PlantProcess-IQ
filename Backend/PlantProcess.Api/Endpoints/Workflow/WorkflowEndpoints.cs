using PlantProcess.Application.Analytics.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Contracts.Materials;
using PlantProcess.Application.Contracts.Process;
using PlantProcess.Application.Contracts.Quality;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Contracts.SourceSystems;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Materials;
using PlantProcess.Application.Services.Process;
using PlantProcess.Application.Services.Quality;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Workflow;

// PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_ROUTE_SPLIT
public static partial class WorkflowEndpoints
{
public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workflow")
            .WithTags("Workflow");

        group.MapGet("/overview", GetWorkflowOverview);
        group.MapGet("/status", GetWorkflowStatusAsync);

        group.MapPost("/source-systems", RegisterSourceSystemAsync);
        group.MapPost("/import-batches", CreateImportBatchAsync);
        group.MapPost("/mapping-definitions", CreateMappingDefinitionAsync);

        group.MapPost("/materials", CreateMaterialAsync);
        group.MapPost("/materials/{materialUnitId:guid}/aliases", AddMaterialAliasAsync);
        group.MapPost("/genealogy-edges", CreateGenealogyEdgeAsync);

        group.MapPost("/process-steps", AddProcessStepAsync);
        group.MapPost("/parameter-definitions", AddParameterDefinitionAsync);
        group.MapPost("/parameter-observations", AddParameterObservationAsync);

        group.MapPost("/process-events", AddProcessEventAsync);
        group.MapPost("/downtime-events", AddDowntimeEventAsync);

        group.MapPost("/defects", AddDefectCatalogAsync);
        group.MapPost("/quality-events", AddQualityEventAsync);

        group.MapPost("/data-quality-issues", RaiseDataQualityIssueAsync);
        group.MapPost("/risk-scores", StoreRiskScoreAsync);

        group.MapGet("/materials/{materialUnitId:guid}/investigation", InvestigateMaterialAsync);

        return app;
    }
}
