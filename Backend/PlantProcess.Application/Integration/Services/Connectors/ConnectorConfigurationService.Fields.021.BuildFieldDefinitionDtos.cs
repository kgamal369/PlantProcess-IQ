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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_FIELDS_SPLIT
public sealed partial class ConnectorConfigurationService
{
private static IReadOnlyList<SourceFieldDefinitionDto> BuildFieldDefinitionDtos(
        Guid sourceDatasetDefinitionId,
        CsvParseResult parsed)
    {
        var result = new List<SourceFieldDefinitionDto>();

        for (var index = 0; index < parsed.Headers.Count; index++)
        {
            var header = parsed.Headers[index];
            var values = parsed.Rows
                .Select(x => x.TryGetValue(header, out var value) ? value : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(100)
                .ToList();

            var inferredType = InferDataType(values);

            result.Add(new SourceFieldDefinitionDto(
                Guid.Empty,
                sourceDatasetDefinitionId,
                header,
                header,
                inferredType,
                index + 1,
                parsed.Rows.Any(x => !x.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value)),
                values.Count == 0 ? null : values.Max(x => x!.Length),
                inferredType == "Decimal" ? 18 : null,
                inferredType == "Decimal" ? 6 : null,
                values.FirstOrDefault(),
                LooksLikeKey(header),
                inferredType == "DateTime" || LooksLikeTimestamp(header),
                true));
        }

        return result;
    }
}
