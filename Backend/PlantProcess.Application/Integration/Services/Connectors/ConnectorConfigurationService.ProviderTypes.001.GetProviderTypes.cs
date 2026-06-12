using PlantProcess.Application.Integration.Connectors;
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

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_PROVIDERTYPES_SPLIT
public sealed partial class ConnectorConfigurationService
{
public IReadOnlyList<ProviderTypeDto> GetProviderTypes()
    {
        return new List<ProviderTypeDto>
        {
            new(
                "Csv",
                "CSV Snapshot",
                "Available now. Reads CSV snapshot exports into the PlantProcess IQ raw staging layer.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Csv"),
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new(
                "Excel",
                "Excel Snapshot",
                "Available now. Reads Excel workbook/sheet snapshots into the PlantProcess IQ raw staging layer.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Excel"),
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new(
                "PostgreSql",
                "PostgreSQL Read-only DB Link",
                "Planned/conditional read-only connector for PostgreSQL source systems. Not marked demo-ready until customer-demo certification is complete.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("PostgreSql"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "SqlServer",
                "Microsoft SQL Server Read-only DB Link",
                "Planned read-only connector for SQL Server / MSSQL source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("SqlServer"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "MySql",
                "MySQL Read-only DB Link",
                "Planned read-only connector for MySQL source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("MySql"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "Oracle",
                "Oracle Read-only DB Link",
                "Implemented read-only Oracle connector for MES/L2/QMS/source databases. Available only after customer/demo certification connection is configured.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Oracle"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "RestApi",
                "REST API Snapshot",
                "Planned API snapshot connector. Not part of the current demo-ready connector set.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("RestApi"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "OpcUaHistorian",
                "OPC-UA / Historian",
                "Future historian/live-data connector. Not part of the current demo-ready connector set.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("OpcUaHistorian"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: true)
        };
    }
}
