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
private static string? ValidateSafePreviewSql(string? sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            return "SQL text is required.";

        var sql = sqlText.Trim();

        var lower = $" {sql.ToLowerInvariant()} ";

        var forbidden = new[]
        {
            " insert ",
            " update ",
            " delete ",
            " drop ",
            " alter ",
            " create ",
            " truncate ",
            " grant ",
            " revoke ",
            " execute ",
            " exec ",
            " call ",
            " copy ",
            " vacuum ",
            " analyze ",
            " set ",
            " reset ",
            " do ",
            " merge ",
            " pg_read_file",
            " pg_ls_dir",
            " dblink",
            " xp_",
            ";--"
        };

        if (forbidden.Any(lower.Contains))
            return "Only safe SELECT/WITH preview queries are allowed. Mutating, administrative, file-system and extension calls are blocked.";

        if (!lower.TrimStart().StartsWith("select ") && !lower.TrimStart().StartsWith("with "))
            return "Preview SQL must start with SELECT or WITH.";

        if (!lower.Contains("staging_records") &&
            !lower.Contains("import_batches") &&
            !lower.Contains("source_dataset_definitions") &&
            !lower.Contains("source_field_definitions") &&
            !lower.Contains("mapping_definitions") &&
            !lower.Contains("schema_view_definitions"))
        {
            return "Phase 1 schema-preview SQL must operate on staging/schema/mapping tables only.";
        }

        return null;
    }
}
