using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HELPERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static ConnectorCertificationRow BuildCertification(
        string providerType,
        string environmentVariableName,
        IConfiguration configuration)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName)
            ?? configuration[environmentVariableName];

        var hasConnection = !string.IsNullOrWhiteSpace(value);

        return new ConnectorCertificationRow(
            ProviderType: providerType,
            EnvironmentVariableName: environmentVariableName,
            HasCertificationConnectionString: hasConnection,
            CertificationStatus: hasConnection
                ? "Ready to run smoke certification"
                : "Missing certification connection string",
            IsDemoCertified: false,
            Message: hasConnection
                ? "Run the provider smoke test and then flip IsDemoCertified in connector truth only after it passes."
                : $"Set {environmentVariableName} in the demo machine or CI secret store.");
    }
}
