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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_QUERIES_SPLIT
public sealed partial class ConnectorConfigurationService
{
private async Task<IReadOnlyList<SourceFieldDefinitionDto>> GetFieldDtosAsync(
        Guid sourceDatasetDefinitionId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SourceFieldDefinitions
            .AsNoTracking()
            .Where(x => x.SourceDatasetDefinitionId == sourceDatasetDefinitionId && !x.IsDeleted)
            .OrderBy(x => x.Ordinal)
            .Select(x => new SourceFieldDefinitionDto(
                x.Id,
                x.SourceDatasetDefinitionId,
                x.FieldName,
                x.DisplayName,
                x.SourceDataType,
                x.Ordinal,
                x.IsNullable,
                x.MaxLength,
                x.NumericPrecision,
                x.NumericScale,
                x.SampleValue,
                x.IsPrimaryKeyCandidate,
                x.IsTimestampCandidate,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
