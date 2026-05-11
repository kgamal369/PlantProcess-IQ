using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Services.Integration;

namespace PlantProcess.Api.Endpoints.Integration;

public static class ImportWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapImportWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workflow/import")
            .WithTags("Import Workflow");

        group.MapPost("/run", RunImportWorkflowAsync);
        group.MapPost("/process-queue", ProcessImportQueueAsync);

        return app;
    }

    private static async Task<IResult> RunImportWorkflowAsync(
        RunImportWorkflowRequest request,
        IImportWorkflowService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new RunImportWorkflowCommand(
            ImportBatchId: request.ImportBatchId,
            SourceSystemDefinitionId: request.SourceSystemDefinitionId,
            MappingDefinitionId: request.MappingDefinitionId,
            ImportBatchCode: request.ImportBatchCode,
            ImportType: request.ImportType ?? "WorkflowApi",
            SourceObjectName: request.SourceObjectName,
            FileName: request.FileName,
            Checksum: request.Checksum,
            Rows: request.Rows.Select(x => new RunImportWorkflowRawRow(x.RowNumber, x.RawJson, x.SourceRecordId)).ToList(),
            MappingTake: request.MappingTake ?? 5000,
            StopOnFirstError: request.StopOnFirstError ?? false,
            RunDataQualityScan: request.RunDataQualityScan ?? true,
            DataQualityMaxCandidatesPerRule: request.DataQualityMaxCandidatesPerRule ?? 500,
            Metadata: new CommandMetadata(
                IsSynthetic: request.IsSynthetic,
                SourceSystem: request.SourceSystem,
                SourceRecordId: request.SourceRecordId,
                RequestedBy: httpContext.User?.Identity?.Name,
                CorrelationId: httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier));

        var result = await service.RunAsync(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> ProcessImportQueueAsync(
        ProcessImportQueueRequest request,
        IImportBatchQueueProcessorService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ProcessPendingBatchesAsync(
            request.MaxBatches ?? 5,
            request.RowsPerBatch ?? 5000,
            request.StopOnFirstError ?? false,
            request.RunDataQualityScan ?? true,
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    public sealed record RunImportWorkflowRequest(
        Guid? ImportBatchId,
        Guid SourceSystemDefinitionId,
        Guid MappingDefinitionId,
        string? ImportBatchCode,
        string? ImportType,
        string SourceObjectName,
        string? FileName,
        string? Checksum,
        IReadOnlyCollection<RunImportWorkflowRawRowRequest> Rows,
        int? MappingTake,
        bool? StopOnFirstError,
        bool? RunDataQualityScan,
        int? DataQualityMaxCandidatesPerRule,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record RunImportWorkflowRawRowRequest(
        int RowNumber,
        string RawJson,
        string? SourceRecordId);

    public sealed record ProcessImportQueueRequest(
        int? MaxBatches,
        int? RowsPerBatch,
        bool? StopOnFirstError,
        bool? RunDataQualityScan);
}
