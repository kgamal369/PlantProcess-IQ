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
public async Task<ApplicationResult<ConnectionProfileDto>> GetConnectionProfileByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var profile = await GetConnectionProfileDtoQuery(id)
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? ApplicationResult<ConnectionProfileDto>.Failure(ApplicationError.NotFound("Connection profile not found."))
            : ApplicationResult<ConnectionProfileDto>.Success(profile);
    }
}
