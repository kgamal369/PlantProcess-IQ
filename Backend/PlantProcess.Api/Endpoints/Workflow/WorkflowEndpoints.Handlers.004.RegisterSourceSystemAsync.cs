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

// PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_HANDLERS_SPLIT
public static partial class WorkflowEndpoints
{
private static async Task<IResult> RegisterSourceSystemAsync(
        RegisterSourceSystemRequest request,
        ISourceSystemService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new RegisterSourceSystemCommand(
            SourceSystemCode: request.SourceSystemCode,
            SourceSystemName: request.SourceSystemName,
            SourceSystemType: request.SourceSystemType,
            IsReadOnlySource: request.IsReadOnlySource,
            Description: request.Description,
            Metadata: ToMetadata(
                request.IsSynthetic,
                request.SourceSystem,
                request.SourceRecordId,
                httpContext));

        var result = await service.RegisterAsync(command, cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created($"/workflow/source-systems/{id}", new
            {
                id,
                request.SourceSystemCode,
                request.SourceSystemName,
                request.SourceSystemType,
                request.IsReadOnlySource
            }));
    }
}
