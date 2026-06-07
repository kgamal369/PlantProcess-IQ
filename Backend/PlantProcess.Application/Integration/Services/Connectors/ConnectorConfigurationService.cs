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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_SURFACE_SPLIT
public sealed partial class ConnectorConfigurationService : IConnectorConfigurationService
{
private readonly IDataSourceConnectorFactory _connectorFactory;

private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

private readonly IPlantProcessDbContext _dbContext;

public ConnectorConfigurationService(IPlantProcessDbContext dbContext,
    IDataSourceConnectorFactory connectorFactory)
    {
        _dbContext = dbContext;
        _connectorFactory = connectorFactory;

    }
}
