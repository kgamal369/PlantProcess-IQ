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

// PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_HELPERS_SPLIT
public static partial class WorkflowEndpoints
{
private static CommandMetadata ToMetadata(
        bool isSynthetic,
        string? sourceSystem,
        string? sourceRecordId,
        HttpContext httpContext)
    {
        var correlationId = httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var value)
            ? value.ToString()
            : httpContext.TraceIdentifier;

        return new CommandMetadata(
            IsSynthetic: isSynthetic,
            SourceSystem: sourceSystem,
            SourceRecordId: sourceRecordId,
            RequestedBy: httpContext.User?.Identity?.Name,
            CorrelationId: correlationId);
    }
}
