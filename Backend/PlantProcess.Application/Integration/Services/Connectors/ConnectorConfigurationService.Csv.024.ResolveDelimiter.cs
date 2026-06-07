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
private static char ResolveDelimiter(string? delimiter)
    {
        if (string.IsNullOrWhiteSpace(delimiter))
            return ',';

        var value = delimiter.Trim();

        return value.ToLowerInvariant() switch
        {
            "\\t" => '\t',
            "tab" => '\t',
            "semicolon" => ';',
            ";" => ';',
            "pipe" => '|',
            "|" => '|',
            "," => ',',
            "comma" => ',',
            _ => value[0]
        };
    }
}
