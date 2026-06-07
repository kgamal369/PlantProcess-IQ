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
private static IResult GetWorkflowOverview()
    {
        return Results.Ok(new
        {
            product = "PlantProcess IQ",
            purpose = "Generic manufacturing process-to-quality intelligence workflow.",
            rule = "API contains no demo data. Demo/sample data must be inserted into the database through SQL scripts, imports, or synthetic generators.",
            architectureRule = "Workflow endpoints are thin API facades. Business/process logic belongs to PlantProcess.Application services.",
            workflow = new[]
            {
                "1. Register source system",
                "2. Create import batch",
                "3. Create mapping definition",
                "4. Create canonical material and material aliases",
                "5. Create genealogy edges",
                "6. Add process steps and parameter observations",
                "7. Add process events, downtime events and quality events",
                "8. Raise data-quality issues",
                "9. Store risk scores",
                "10. Investigate one material from genealogy to risk"
            }
        });
    }
}
