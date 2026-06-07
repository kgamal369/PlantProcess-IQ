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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_HELPERS_SPLIT
public sealed partial class ConnectorConfigurationService
{
private static string NormalizeProviderType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant() switch
        {
            "csv" => "Csv",
            "excel" => "Excel",
            "xlsx" => "Excel",
            "postgres" => "PostgreSql",
            "postgresql" => "PostgreSql",
            "sqlserver" => "SqlServer",
            "mssql" => "SqlServer",
            "oracle" => "Oracle",
            "api" => "RestApi",
            "restapi" => "RestApi",
            _ => value.Trim()
        };
    }
}
