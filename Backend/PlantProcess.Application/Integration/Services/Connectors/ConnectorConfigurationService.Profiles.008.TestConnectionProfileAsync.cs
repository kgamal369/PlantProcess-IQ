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
public async Task<ApplicationResult<DataSourceConnectionTestResult>> TestConnectionProfileAsync(
        Guid connectionProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<DataSourceConnectionTestResult>.Failure(
                ApplicationError.NotFound("Connection profile was not found."));

        try
        {
            var connector = _connectorFactory.GetConnector(profile.ProviderType);
            var result = await connector.TestConnectionAsync(profile, cancellationToken);

            profile.MarkTestResult(result.IsSuccess, result.Message);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return ApplicationResult<DataSourceConnectionTestResult>.Success(result);
        }
        catch (Exception ex)
        {
            profile.MarkTestResult(false, ex.Message);
            await _dbContext.SaveChangesAsync(cancellationToken);

           return ApplicationResult<DataSourceConnectionTestResult>.Failure(
                 ApplicationError.Validation($"Some error message: {ex.Message}"));     
        }
    }
}
