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
public async Task<ApplicationResult<ConnectionProfileDto>> DeactivateConnectionProfileAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<ConnectionProfileDto>.Failure(ApplicationError.NotFound("Connection profile not found."));

        entity.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetConnectionProfileByIdAsync(entity.Id, cancellationToken);
    }
}
