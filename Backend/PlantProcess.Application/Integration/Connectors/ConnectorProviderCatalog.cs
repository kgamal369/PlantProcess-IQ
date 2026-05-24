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
                IsAvailableNow: true,
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new ProviderTypeDto(
                ProviderType: "Excel",
                DisplayName: "Excel Snapshot",
                Description: "Available now. Reads Excel workbook/sheet snapshots into the raw staging layer.",
                IsAvailableNow: true,
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new ProviderTypeDto(
                ProviderType: "PostgreSql",
                DisplayName: "PostgreSQL Read-only DB Link",
                Description: "Planned/conditional read-only connector for PostgreSQL source systems. Show as available only after connector implementation and smoke test are wired.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "SqlServer",
                DisplayName: "Microsoft SQL Server Read-only DB Link",
                Description: "Planned/conditional read-only connector for SQL Server / MSSQL source systems.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "MySql",
                DisplayName: "MySQL Read-only DB Link",
                Description: "Planned/conditional read-only connector for MySQL source systems and inspection devices.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "Oracle",
                DisplayName: "Oracle Read-only DB Link",
                Description: "Planned read-only Oracle connector for MES/L2/QMS source systems.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "RestApi",
                DisplayName: "REST API Snapshot",
                Description: "Future API snapshot connector. Not part of the current demo availability.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new ProviderTypeDto(
                ProviderType: "OpcUaHistorian",
                DisplayName: "OPC-UA / Historian",
                Description: "Future historian/live-data path. Not part of the current demo availability.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: false,
                SupportsIncrementalImport: true),
        };
    }

    public static ProviderTypeDto? Find(string providerType)
    {
        return GetProviderTypes()
            .FirstOrDefault(x =>
                string.Equals(x.ProviderType, providerType, StringComparison.OrdinalIgnoreCase));
    }
}