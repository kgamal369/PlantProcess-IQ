using Npgsql;
using PlantProcess.Application.Assistant;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// T-074. Reads the parameter registry, which is the sole authority for quantity
/// semantics.
///
/// NOT tenant scoped, and that is a measured fact rather than an oversight:
/// ParameterDefinition derives from BaseEntity, which carries no TenantId, so
/// parameter_definitions has no tenant column to filter on. Adding a predicate
/// against a column that does not exist would fail; pretending in the signature
/// that it did would be worse.
///
/// Deleted rows are excluded here so no caller can forget to.
/// </summary>
public sealed class NpgsqlParameterQuantityRegistry : IParameterQuantityRegistry
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlParameterQuantityRegistry(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<IReadOnlyList<RegistryQuantity>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var rows = new List<RegistryQuantity>();

        await using var cmd = _dataSource.CreateCommand(
            "SELECT parameter_code, parameter_name, value_type, unit_of_measure, " +
            "       expected_min_value, expected_max_value, is_synthetic " +
            "FROM ppiq_meta.parameter_definitions " +
            "WHERE is_deleted = false");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new RegistryQuantity(
                reader.GetString(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? "Numeric" : reader.GetString(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3),
                await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetDecimal(4),
                await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetDecimal(5),
                !await reader.IsDBNullAsync(6, cancellationToken) && reader.GetBoolean(6)));
        }

        return rows;
    }
}