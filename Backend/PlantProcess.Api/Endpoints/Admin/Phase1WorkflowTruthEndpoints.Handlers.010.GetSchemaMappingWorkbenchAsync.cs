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

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HANDLERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static async Task<IResult> GetSchemaMappingWorkbenchAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var datasets = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                select new WorkbenchDatasetRow(
                    dataset.Id,
                    dataset.DatasetCode,
                    dataset.DatasetName,
                    dataset.DatasetKind,
                    profile.ProviderType,
                    dataset.SourceSchemaName,
                    dataset.SourceObjectName,
                    dataset.IsActive))
            .OrderBy(x => x.ProviderType)
            .ThenBy(x => x.DatasetCode)
            .ToListAsync(cancellationToken);

        var fields = await dbContext.SourceFieldDefinitions
            .AsNoTracking()
            .OrderBy(x => x.SourceDatasetDefinitionId)
            .ThenBy(x => x.Ordinal)
            .Select(x => new WorkbenchSourceFieldRow(
                x.Id,
                x.SourceDatasetDefinitionId,
                x.FieldName,
                x.DisplayName,
                x.SourceDataType,
                x.Ordinal,
                x.IsNullable,
                x.SampleValue,
                x.IsPrimaryKeyCandidate,
                x.IsTimestampCandidate,
                x.IsActive))
            .ToListAsync(cancellationToken);

        var mappings = await dbContext.MappingDefinitions
            .AsNoTracking()
            .OrderBy(x => x.MappingCode)
            .Select(x => new WorkbenchMappingRow(
                x.Id,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.MappingJson,
                x.MappingVersion,
                x.IsActive,
                x.Description))
            .ToListAsync(cancellationToken);

        var schemaViews = await dbContext.SchemaViewDefinitions
            .AsNoTracking()
            .OrderBy(x => x.SchemaViewCode)
            .Select(x => new WorkbenchSchemaViewRow(
                x.Id,
                x.SchemaViewCode,
                x.SchemaViewName,
                x.ViewKind,
                x.PrimarySourceDatasetDefinitionId,
                x.SourceDatasetIdsJson,
                x.IsApproved,
                x.IsActive,
                x.LastValidationStatus,
                x.LastValidationMessage))
            .ToListAsync(cancellationToken);

        var canonicalTargets = BuildCanonicalTargets();

        return Results.Ok(new SchemaMappingWorkbenchResponse(
            DateTime.UtcNow,
            "Schema mapping is the centerpiece of genericity: source fields stay source-shaped, then approved mapping turns them into canonical PlantProcess IQ entities.",
            datasets,
            fields,
            canonicalTargets,
            mappings,
            schemaViews));
    }
}
