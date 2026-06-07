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
private static string InferDataType(IReadOnlyList<string?> values)
    {
        if (values.Count == 0)
            return "String";

        var nonEmpty = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        if (nonEmpty.Count == 0)
            return "String";

        if (nonEmpty.All(x => bool.TryParse(x, out _)))
            return "Boolean";

        if (nonEmpty.All(x => long.TryParse(x, out _)))
            return "Integer";

        if (nonEmpty.All(x => decimal.TryParse(x, out _)))
            return "Decimal";

        if (nonEmpty.All(x => DateTime.TryParse(x, out _)))
            return "DateTime";

        return "String";
    }
}
