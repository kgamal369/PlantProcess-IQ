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
public async Task<ApplicationResult<ConnectionProfileDto>> CreateConnectionProfileAsync(

        CreateConnectionProfileRequest request,

        CancellationToken cancellationToken)

    {

        var validation = ValidateConnectionProfileRequest(request);



        if (validation is not null)

            return ApplicationResult<ConnectionProfileDto>.Failure(validation);



        var providerType = NormalizeProviderType(request.ProviderType);



        var provider = GetProviderTypes().FirstOrDefault(x => x.ProviderType == providerType);



        if (provider is null)

        {

            return ApplicationResult<ConnectionProfileDto>.Failure(

                ApplicationError.Validation($"Unsupported provider type '{request.ProviderType}'."));

        }
        // T-207 corrective. A provider can be listed for the roadmap and still have no
        // runtime in this build. Creating a profile against one produces a connection that
        // fails at first use with a type-loading error rather than a product answer, so it
        // is refused here with a reason that names the state.
        if (!PlantProcess.Application.Integration.Connectors.ConnectorProviderCatalog.HasRuntimeImplementation(providerType))
        {
            return ApplicationResult<ConnectionProfileDto>.Failure(
                ApplicationError.Validation(
                    $"Provider type '{provider.ProviderType}' is declared but has no connector implementation in this build, so a connection profile cannot be created for it."));
        }



        var sourceExists = await _dbContext.SourceSystemDefinitions

            .AnyAsync(x =>

                x.Id == request.SourceSystemDefinitionId &&

                !x.IsDeleted,

                cancellationToken);



        if (!sourceExists)

        {

            return ApplicationResult<ConnectionProfileDto>.Failure(

                ApplicationError.NotFound("Source system definition not found."));

        }



        var duplicate = await _dbContext.ConnectionProfiles

            .AnyAsync(x =>

                !x.IsDeleted &&

                x.ConnectionProfileCode == request.ConnectionProfileCode.Trim(),

                cancellationToken);



        if (duplicate)

        {

            return ApplicationResult<ConnectionProfileDto>.Failure(

                ApplicationError.Conflict("Connection profile code already exists."));

        }



        var entity = new ConnectionProfile(

            sourceSystemDefinitionId: request.SourceSystemDefinitionId,

            connectionProfileCode: request.ConnectionProfileCode,

            connectionProfileName: request.ConnectionProfileName,

            providerType: providerType,

            isSynthetic: request.IsSynthetic,

            connectionMode: request.ConnectionMode ?? "Snapshot",

            hostName: request.HostName,

            port: request.Port,

            databaseName: request.DatabaseName,

            schemaName: request.SchemaName,

            fileRootPath: request.FileRootPath,

            apiBaseUrl: request.ApiBaseUrl,

            secretReference: request.SecretReference,

            connectionOptionsJson: request.ConnectionOptionsJson,

            readOnlyEnforced: request.ReadOnlyEnforced ?? true,

            description: request.Description,

            sourceSystem: request.SourceSystem,

            sourceRecordId: request.SourceRecordId);



        _dbContext.ConnectionProfiles.Add(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);



        return await GetConnectionProfileByIdAsync(entity.Id, cancellationToken);

    }
}
