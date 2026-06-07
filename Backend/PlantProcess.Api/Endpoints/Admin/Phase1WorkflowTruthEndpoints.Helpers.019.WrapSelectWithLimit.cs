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

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HELPERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static string WrapSelectWithLimit(string sqlText, int maxRows)
    {
        var trimmed = sqlText.Trim().TrimEnd(';');
        return $"SELECT * FROM ({trimmed}) AS phase1_preview LIMIT {maxRows}";
    }
}
