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

// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_SQL_HELPERS_SPLIT
public static partial class GenericSchemaMappingEndpoints
{
private static string NormalizeSelectSql(string sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
            throw new InvalidOperationException("SQL text is required.");

        var trimmed = StripTrailingSemicolon(sqlText.Trim());

        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only SELECT or WITH SQL is allowed.");
        }

        if (trimmed.Contains(';'))
            throw new InvalidOperationException("Multiple SQL statements are not allowed.");

        if (DangerousSql.IsMatch(trimmed))
            throw new InvalidOperationException("SQL contains a forbidden command or function.");

        return trimmed;
    }

private static string StripTrailingSemicolon(string value)
    {
        while (value.EndsWith(";", StringComparison.Ordinal))
            value = value[..^1].TrimEnd();

        return value;
    }

private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 60;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

        return rows;
    }

private static async Task ExecuteNonQueryAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 60;

        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

private static string CleanIdentifier(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");

        var trimmed = value.Trim();

        if (!SafeIdentifier.IsMatch(trimmed))
            throw new InvalidOperationException($"{fieldName} contains an unsafe SQL identifier: {trimmed}");

        return trimmed;
    }

private static string QuoteIdentifier(string value)
    {
        var safe = CleanIdentifier(value, "identifier");
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

private static string NormalizeCode(string code)
    {
        var cleaned = Regex.Replace(code.Trim().ToLowerInvariant(), "[^a-z0-9_]+", "_");
        cleaned = Regex.Replace(cleaned, "_+", "_").Trim('_');

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "view";

        if (char.IsDigit(cleaned[0]))
            cleaned = "v_" + cleaned;

        return cleaned;
    }

private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

private static string GetActor(ClaimsPrincipal user)
    {
        return user.Identity?.Name
               ?? user.FindFirstValue(ClaimTypes.Name)
               ?? user.FindFirstValue("sub")
               ?? "unknown";
    }
}
