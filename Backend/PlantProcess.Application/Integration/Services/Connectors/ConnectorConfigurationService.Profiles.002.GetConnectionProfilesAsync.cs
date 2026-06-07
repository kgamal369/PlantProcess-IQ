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
public async Task<ApplicationResult<IReadOnlyList<ConnectionProfileDto>>> GetConnectionProfilesAsync(
        Guid? sourceSystemDefinitionId,
        string? providerType,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query =
            from profile in _dbContext.ConnectionProfiles.AsNoTracking()
            join source in _dbContext.SourceSystemDefinitions.AsNoTracking()
                on profile.SourceSystemDefinitionId equals source.Id
            where !profile.IsDeleted && !source.IsDeleted
            select new { profile, source };

        if (sourceSystemDefinitionId.HasValue)
            query = query.Where(x => x.profile.SourceSystemDefinitionId == sourceSystemDefinitionId.Value);

        if (!string.IsNullOrWhiteSpace(providerType))
            query = query.Where(x => x.profile.ProviderType == NormalizeProviderType(providerType));

        if (!includeInactive)
            query = query.Where(x => x.profile.IsActive);

        var rows = await query
            .OrderBy(x => x.profile.ProviderType)
            .ThenBy(x => x.profile.ConnectionProfileCode)
            .Select(x => new ConnectionProfileDto(
                x.profile.Id,
                x.profile.SourceSystemDefinitionId,
                x.source.SourceSystemCode,
                x.source.SourceSystemName,
                x.profile.ConnectionProfileCode,
                x.profile.ConnectionProfileName,
                x.profile.ProviderType,
                x.profile.ConnectionMode,
                x.profile.HostName,
                x.profile.Port,
                x.profile.DatabaseName,
                x.profile.SchemaName,
                x.profile.FileRootPath,
                x.profile.ApiBaseUrl,
                x.profile.SecretReference,
                x.profile.ConnectionOptionsJson,
                x.profile.IsActive,
                x.profile.ReadOnlyEnforced,
                x.profile.Description,
                x.profile.LastTestedAtUtc,
                x.profile.LastTestStatus,
                x.profile.LastTestMessage,
                x.profile.IsSynthetic,
                x.profile.CreatedAtUtc,
                x.profile.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<ConnectionProfileDto>>.Success(rows);
    }
}
