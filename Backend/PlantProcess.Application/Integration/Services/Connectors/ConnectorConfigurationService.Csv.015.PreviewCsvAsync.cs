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
public async Task<ApplicationResult<CsvPreviewResult>> PreviewCsvAsync(
        Guid sourceDatasetDefinitionId,
        CsvPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId && !x.IsDeleted, cancellationToken);

        if (dataset is null)
            return ApplicationResult<CsvPreviewResult>.Failure(ApplicationError.NotFound("Source dataset not found."));

        var delimiter = ResolveDelimiter(request.Delimiter);
        var hasHeader = request.HasHeader ?? true;
        var maxRows = Math.Clamp(request.MaxRows ?? 25, 1, 500);

        var parsed = CsvTextParser.Parse(request.CsvText, delimiter, hasHeader, maxRows);

        return ApplicationResult<CsvPreviewResult>.Success(
            new CsvPreviewResult(
                delimiter.ToString(),
                hasHeader,
                parsed.Headers,
                parsed.Rows));
    }
}
