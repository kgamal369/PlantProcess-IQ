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
private static IReadOnlyList<SourceFieldDefinition> BuildFieldDefinitions(
        Guid sourceDatasetDefinitionId,
        CsvParseResult parsed,
        bool isSynthetic)
    {
        var result = new List<SourceFieldDefinition>();

        for (var index = 0; index < parsed.Headers.Count; index++)
        {
            var header = parsed.Headers[index];
            var values = parsed.Rows
                .Select(x => x.TryGetValue(header, out var value) ? value : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(100)
                .ToList();

            var inferredType = InferDataType(values);
            var sampleValue = values.FirstOrDefault();

            result.Add(new SourceFieldDefinition(
                sourceDatasetDefinitionId,
                fieldName: header,
                displayName: header,
                sourceDataType: inferredType,
                ordinal: index + 1,
                isNullable: parsed.Rows.Any(x => !x.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value)),
                isSynthetic: isSynthetic,
                maxLength: values.Count == 0 ? null : values.Max(x => x!.Length),
                numericPrecision: inferredType == "Decimal" ? 18 : null,
                numericScale: inferredType == "Decimal" ? 6 : null,
                sampleValue: sampleValue,
                isPrimaryKeyCandidate: LooksLikeKey(header),
                isTimestampCandidate: inferredType == "DateTime" || LooksLikeTimestamp(header),
                sourceSystem: "PlantProcessIQ.CsvSchemaDiscovery"));
        }

        return result;
    }
}
