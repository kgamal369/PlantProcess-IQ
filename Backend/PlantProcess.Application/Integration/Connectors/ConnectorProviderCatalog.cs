using PlantProcess.Application.Integration.Connectors;
using PlantProcess.Application.Integration.Contracts.Dtos;

namespace PlantProcess.Application.Integration.Connectors;

public static class ConnectorProviderCatalog
{
    public static IReadOnlyList<ProviderTypeDto> GetProviderTypes()
    {
        return new[]
        {
            new ProviderTypeDto(
                ProviderType: "Csv",
                DisplayName: "CSV Snapshot",
                Description: "Reads CSV exports from a file share into the staging layer.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Csv"),
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new ProviderTypeDto(
                ProviderType: "Excel",
                DisplayName: "Excel Snapshot",
                Description: "Reads Excel workbooks and sheets into the staging layer.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Excel"),
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new ProviderTypeDto(
                ProviderType: "PostgreSql",
                DisplayName: "PostgreSQL Read-only DB Link",
                Description: "Read-only DB link to PostgreSQL source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("PostgreSql"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "SqlServer",
                DisplayName: "Microsoft SQL Server Read-only DB Link",
                Description: "Read-only DB link to Microsoft SQL Server source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("SqlServer"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "MySql",
                DisplayName: "MySQL Read-only DB Link",
                Description: "Read-only DB link to MySQL source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("MySql"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "Oracle",
                DisplayName: "Oracle Read-only DB Link",
                Description: "Read-only DB link to Oracle source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Oracle"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "Sap",
                DisplayName: "SAP",
                Description: "Read-only connector for SAP source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Sap"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "RestApi",
                DisplayName: "REST API Snapshot",
                Description: "Reads snapshots from REST endpoints.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("RestApi"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

                        // PPIQ_PACK_E2_GA_HISTORIAN_PROVIDER
            new ProviderTypeDto(
                ProviderType: "OpcUaHistorian",
                DisplayName: "OPC-UA / Historian Gateway",
                Description: "Read-only gateway for OPC-UA historians and tag archives.",
                IsAvailableNow: ConnectorCertification.IsCertified("OpcUaHistorian"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: true)
        };
    }
}