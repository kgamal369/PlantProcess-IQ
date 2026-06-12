using PlantProcess.Application.Integration.Connectors;
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
private static ConnectorProviderTruthRow[] BuildProviderTruthRows()
    {
        return new[]
        {
            new ConnectorProviderTruthRow(
                SortOrder: 10,
                ProviderType: "Csv",
                DisplayName: "CSV",
                Description: "Flat-file export connector. Good for first demo and quick plant data diagnostic.",
                IsImplemented: true,
                IsDemoCertified: true,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Csv"),
                RequiresSecretReference: false,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Available now",
                Limitation: "Best for snapshots and controlled exports; not real-time streaming.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 20,
                ProviderType: "Excel",
                DisplayName: "Excel",
                Description: "Excel workbook/sheet connector for manual QA, lab, yard and business files.",
                IsImplemented: true,
                IsDemoCertified: true,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Excel"),
                RequiresSecretReference: false,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false,
                StatusLabel: "Available now",
                Limitation: "Snapshot import only; no continuous database cursor.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 30,
                ProviderType: "PostgreSql",
                DisplayName: "PostgreSQL",
                Description: "Read-only SQL database connector for process/MES-like source databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("PostgreSql"),
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Show as available only after PPIQ-WF-003 smoke certification passes in your demo environment.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 40,
                ProviderType: "SqlServer",
                DisplayName: "Microsoft SQL Server",
                Description: "Read-only connector for MES, QA, ERP or Level-3 databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("SqlServer"),
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Requires certified local/demo SQL Server connection before frontend marks it selectable.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 50,
                ProviderType: "MySql",
                DisplayName: "MySQL",
                Description: "Read-only connector for inspection, downtime, small MES or device-side databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("MySql"),
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Requires certified MySQL smoke test before customer demo.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 60,
                ProviderType: "Oracle",
                DisplayName: "Oracle",
                Description: "Implemented read-only connector for Oracle MES/L2/QMS and legacy manufacturing source databases.",
                IsImplemented: true,
                IsDemoCertified: false,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Oracle"),
                RequiresSecretReference: true,
                SupportsConnectionTest: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true,
                StatusLabel: "Implemented / certification pending",
                Limitation: "Use Oracle-shaped demo source tables until a real Oracle smoke certification connection is configured and passes.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0),

            new ConnectorProviderTruthRow(
                SortOrder: 70,
                ProviderType: "RestApi",
                DisplayName: "REST API",
                Description: "Planned connector for API-based systems.",
                IsImplemented: false,
                IsDemoCertified: false,
                IsAvailableNow: ProviderAvailability.IsAvailableNow("RestApi"),
                RequiresSecretReference: true,
                SupportsConnectionTest: false,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: false,
                StatusLabel: "Planned",
                Limitation: "Not demo-certified in Phase 1.",
                ActiveConnectionProfiles: 0,
                TotalConnectionProfiles: 0,
                ActiveSourceDatasets: 0,
                TotalSourceDatasets: 0)
        };
    }
}
