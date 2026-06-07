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

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HANDLERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static Task<IResult> GetConnectorCertificationAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var providers = new[]
        {
            BuildCertification("PostgreSql", "PPIQ_CERT_POSTGRES_CONNECTION", configuration),
            BuildCertification("SqlServer", "PPIQ_CERT_MSSQL_CONNECTION", configuration),
            BuildCertification("MySql", "PPIQ_CERT_MYSQL_CONNECTION", configuration),
            BuildCertification("Oracle", "PPIQ_CERT_ORACLE_CONNECTION", configuration)
        };

        var response = new ConnectorCertificationResponse(
            GeneratedAtUtc: DateTime.UtcNow,
            Message:
            "Certification is environment-driven. A provider is demo-certified only when implementation exists, smoke tests pass, and the related certification connection variable is configured.",
            Providers: providers);

        return Task.FromResult<IResult>(Results.Ok(response));
    }
}
