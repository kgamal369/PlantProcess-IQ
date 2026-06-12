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
                Description: "Available now. Reads CSV snapshot exports into the raw staging layer.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Csv"),
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new ProviderTypeDto(
                ProviderType: "Excel",
                DisplayName: "Excel Snapshot",
                Description: "Available now. Reads Excel workbook/sheet snapshots into the raw staging layer.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Excel"),
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new ProviderTypeDto(
                ProviderType: "PostgreSql",
                DisplayName: "PostgreSQL Read-only DB Link",
                Description: "Planned/conditional read-only connector for PostgreSQL source systems. Show as available only after demo-certification smoke tests are part of the API contract suite.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("PostgreSql"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "SqlServer",
                DisplayName: "Microsoft SQL Server Read-only DB Link",
                Description: "Planned/conditional read-only connector for SQL Server / MSSQL source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("SqlServer"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "MySql",
                DisplayName: "MySQL Read-only DB Link",
                Description: "Planned/conditional read-only connector for MySQL source systems and inspection devices.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("MySql"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "Oracle",
                DisplayName: "Oracle Read-only DB Link",
                Description: "Planned read-only Oracle connector for MES/L2/QMS source systems.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("Oracle"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "RestApi",
                DisplayName: "REST API Snapshot",
                Description: "Future API snapshot connector. Not part of the current demo availability.",
                IsAvailableNow: ProviderAvailability.IsAvailableNow("RestApi"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

                        // PPIQ_PACK_E2_GA_HISTORIAN_PROVIDER
            new ProviderTypeDto(
                ProviderType: "OpcUaHistorian",
                DisplayName: "OPC-UA / Historian Gateway",
                Description: "Read-only historian gateway onboarding: configuration validation, tag/point browse metadata, bounded sample reads, and mapping handoff. Presented as available only after intentional demo certification via PPIQ_CONNECTOR_CERTIFIED_OPCUAHISTORIAN (truth contract: stays Planned otherwise).",
                IsAvailableNow: ConnectorCertification.IsCertified("OpcUaHistorian"),
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: true)
        };
    }
}