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
private IQueryable<ConnectionProfileDto> GetConnectionProfileDtoQuery(Guid? profileId = null)
    {
        return
            from profile in _dbContext.ConnectionProfiles.AsNoTracking()
            join source in _dbContext.SourceSystemDefinitions.AsNoTracking()
                on profile.SourceSystemDefinitionId equals source.Id
            where !profile.IsDeleted && !source.IsDeleted && (profileId == null || profile.Id == profileId)
            select new ConnectionProfileDto(
                profile.Id,
                profile.SourceSystemDefinitionId,
                source.SourceSystemCode,
                source.SourceSystemName,
                profile.ConnectionProfileCode,
                profile.ConnectionProfileName,
                profile.ProviderType,
                profile.ConnectionMode,
                profile.HostName,
                profile.Port,
                profile.DatabaseName,
                profile.SchemaName,
                profile.FileRootPath,
                profile.ApiBaseUrl,
                profile.SecretReference,
                profile.ConnectionOptionsJson,
                profile.IsActive,
                profile.ReadOnlyEnforced,
                profile.Description,
                profile.LastTestedAtUtc,
                profile.LastTestStatus,
                profile.LastTestMessage,
                profile.IsSynthetic,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc);
    }
}
