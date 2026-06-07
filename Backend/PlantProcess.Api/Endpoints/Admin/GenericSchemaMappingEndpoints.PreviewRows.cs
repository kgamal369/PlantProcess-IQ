using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_PREVIEW_ROWS_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static async Task<(int rowCount, IReadOnlyList<PreviewColumn> columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)> PreviewRowsAsync(
        PlantProcessDbContext db,
        string sqlText,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSelectSql(sqlText);
        var take = Math.Clamp(maxRows, 0, 5000);

        var wrapped = take == 0
            ? $"SELECT * FROM ({normalized}) ppiq_preview WHERE 1 = 0"
            : $"SELECT * FROM ({normalized}) ppiq_preview LIMIT {take}";

        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = wrapped;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 30;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = reader.GetColumnSchema()
            .Select((c, i) => new PreviewColumn(
                c.ColumnName ?? $"column_{i}",
                c.DataTypeName ?? c.DataType?.Name ?? "unknown",
                i))
            .ToList();

        var rows = new List<IReadOnlyDictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return (rows.Count, columns, rows);
    }
}
