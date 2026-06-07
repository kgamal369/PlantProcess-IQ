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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_PROFILES_SPLIT
public sealed partial class ConnectorConfigurationService
{
private static ApplicationError? ValidateConnectionProfileRequest(CreateConnectionProfileRequest request)
    {
        if (request.SourceSystemDefinitionId == Guid.Empty)
            return ApplicationError.Validation("SourceSystemDefinitionId is required.");

        if (string.IsNullOrWhiteSpace(request.ConnectionProfileCode))
            return ApplicationError.Validation("ConnectionProfileCode is required.");

        if (string.IsNullOrWhiteSpace(request.ConnectionProfileName))
            return ApplicationError.Validation("ConnectionProfileName is required.");

        if (string.IsNullOrWhiteSpace(request.ProviderType))
            return ApplicationError.Validation("ProviderType is required.");

        return null;
    }
}
