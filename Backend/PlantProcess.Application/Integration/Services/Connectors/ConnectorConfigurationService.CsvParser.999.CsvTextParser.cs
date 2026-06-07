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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_TEXT_PARSER_SPLIT
public sealed partial class ConnectorConfigurationService
{
private sealed record CsvParseResult(
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);
}
