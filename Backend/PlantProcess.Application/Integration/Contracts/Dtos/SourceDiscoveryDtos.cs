// ============================================================
// FILE: Backend/PlantProcess.Application/Integration/Contracts/Dtos/SourceDiscoveryDtos.cs
// M1-04: Live source discovery + column-aware registration DTOs.
// Generic across all ISchemaReader providers (PostgreSql, MySql, Oracle, MsSql).
// ============================================================
namespace PlantProcess.Application.Integration.Contracts.Dtos;

public sealed record SourceTableDto(
    string SchemaName,
    string TableName,
    string Kind);

public sealed record SourceColumnDto(
    string ColumnName,
    string DataType,
    int Ordinal,
    bool IsNullable,
    bool IsPrimaryKeyCandidate,
    bool IsTimestampCandidate);

public sealed record RegisterSourceTableRequest(
    string SchemaName,
    string TableName,
    IReadOnlyList<string> PrimaryKeyColumns,
    string? WatermarkColumn,
    IReadOnlyList<string>? SelectedColumns,
    string? RowFilter);

public sealed record RegisterSourceTableResult(
    string SchemaName,
    string TableName,
    int RegisteredColumnCount,
    bool WatermarkResolved,
    string Message);
