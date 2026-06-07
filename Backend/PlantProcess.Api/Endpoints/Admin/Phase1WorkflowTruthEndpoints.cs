using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_ROUTE_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
public static IEndpointRouteBuilder MapPhase1WorkflowTruthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/phase1")
            .WithTags("Admin - Phase 1 Workflow Truth");

        // ------------------------------------------------------------
        // PPIQ-WF-001
        // Connector Truth Matrix API
        // ------------------------------------------------------------
        group.MapGet("/connector-truth", GetConnectorTruthAsync)
            .WithName("GetPhase1ConnectorTruth")
            .Produces<ConnectorTruthMatrixResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-003
        // Connector certification status for PostgreSQL / MSSQL / MySQL
        // ------------------------------------------------------------
        group.MapGet("/connector-certification", GetConnectorCertificationAsync)
            .WithName("GetPhase1ConnectorCertification")
            .Produces<ConnectorCertificationResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-004 / PPIQ-WF-005
        // DB-driven source scheduling board and due-run trigger.
        // Real execution is delegated to your existing IDeltaImportExecutionService.
        // ------------------------------------------------------------
        group.MapGet("/source-schedule-board", GetSourceScheduleBoardAsync)
            .WithName("GetPhase1SourceScheduleBoard")
            .Produces<SourceScheduleBoardResponse>();

        group.MapPost("/run-due-source-imports", RunDueSourceImportsAsync)
            .WithName("RunPhase1DueSourceImports")
            .Produces<RunDueSourceImportsResponse>();

        group.MapPost("/source-datasets/{sourceDatasetDefinitionId:guid}/schedule-now", ScheduleSourceDatasetNowAsync)
            .WithName("SchedulePhase1SourceDatasetNow")
            .Produces<SourceScheduleRow>();

        group.MapPost("/source-datasets/{sourceDatasetDefinitionId:guid}/cursor", UpdateDatasetCursorAsync)
            .WithName("UpdatePhase1DatasetCursor")
            .Produces<SourceScheduleRow>();

        // ------------------------------------------------------------
        // PPIQ-WF-006
        // Raw/staging latest-copy viewer.
        // ------------------------------------------------------------
        group.MapGet("/staging/summary", GetStagingSummaryAsync)
            .WithName("GetPhase1StagingSummary")
            .Produces<StagingSummaryResponse>();

        group.MapGet("/staging/records", GetStagingRecordsAsync)
            .WithName("GetPhase1StagingRecords")
            .Produces<StagingRecordsResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-007
        // Schema Mapping Workbench / Cross-source join helper.
        // ------------------------------------------------------------
        group.MapGet("/schema-mapping/workbench", GetSchemaMappingWorkbenchAsync)
            .WithName("GetPhase1SchemaMappingWorkbench")
            .Produces<SchemaMappingWorkbenchResponse>();

        group.MapPost("/schema-mapping/preview-view", PreviewSchemaViewAsync)
            .WithName("PreviewPhase1SchemaView")
            .Produces<SchemaViewPreviewResponse>();

        // ------------------------------------------------------------
        // PPIQ-WF-010
        // Importing Data Job Configuration page support.
        // ------------------------------------------------------------
        group.MapGet("/import-jobs/configuration-board", GetImportJobConfigurationBoardAsync)
            .WithName("GetPhase1ImportJobConfigurationBoard")
            .Produces<ImportJobConfigurationBoardResponse>();

        group.MapPost("/import-jobs/from-mapping", CreateImportJobFromMappingAsync)
            .WithName("CreatePhase1ImportJobFromMapping")
            .Produces<ImportJobConfigurationRow>();

        return app;
    }
}
