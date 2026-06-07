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
private static string NormalizeProvider(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "csv" => "csv",
            "excel" => "excel",
            "xlsx" => "excel",

            "postgres" => "postgresql",
            "postgresql" => "postgresql",
            "pgsql" => "postgresql",

            "sqlserver" => "sqlserver",
            "sql_server" => "sqlserver",
            "mssql" => "sqlserver",
            "microsoftsqlserver" => "sqlserver",

            "mysql" => "mysql",
            "mariadb" => "mysql",

            "oracle" => "oracle",

            "restapi" => "restapi",
            "rest" => "restapi",
            "api" => "restapi",

            "opcua" => "opcua",
            "opc_ua" => "opcua",
            "historian" => "historian",

            _ => normalized
        };
    }
}
