using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Connectors;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Connectors;

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_SPLIT
public sealed partial class ConnectorConfigurationService
{
public async Task<ApplicationResult<CsvSchemaDiscoveryResult>> DiscoverCsvSchemaAsync(
        Guid sourceDatasetDefinitionId,
        CsvSchemaDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId && !x.IsDeleted, cancellationToken);

        if (dataset is null)
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.NotFound("Source dataset not found."));

        var profile = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.NotFound("Connection profile not found."));

        if (profile.ProviderType != "Csv")
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.Validation("CSV schema discovery is only available for Csv provider profiles in Phase 3."));

        var delimiter = ResolveDelimiter(request.Delimiter);
        var hasHeader = request.HasHeader ?? true;
        var maxRows = Math.Clamp(request.MaxRowsToAnalyze ?? 100, 1, 5000);

        var parsed = CsvTextParser.Parse(request.CsvText, delimiter, hasHeader, maxRows);

        if (parsed.Headers.Count == 0)
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.Validation("CSV contains no columns."));

        if (request.PersistFields)
        {
            var existingFields = await _dbContext.SourceFieldDefinitions
                .Where(x => x.SourceDatasetDefinitionId == sourceDatasetDefinitionId && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingFields)
                existing.SoftDelete("Replaced by new CSV schema discovery.");

            var fieldEntities = BuildFieldDefinitions(
                sourceDatasetDefinitionId,
                parsed,
                dataset.IsSynthetic);

            _dbContext.SourceFieldDefinitions.AddRange(fieldEntities);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var fields = await GetFieldDtosAsync(sourceDatasetDefinitionId, cancellationToken);

        if (fields.Count == 0)
        {
            fields = BuildFieldDefinitionDtos(sourceDatasetDefinitionId, parsed);
        }

        return ApplicationResult<CsvSchemaDiscoveryResult>.Success(
            new CsvSchemaDiscoveryResult(
                sourceDatasetDefinitionId,
                dataset.DatasetCode,
                dataset.SourceObjectName,
                delimiter.ToString(),
                hasHeader,
                parsed.Rows.Count,
                fields));
    }
}
