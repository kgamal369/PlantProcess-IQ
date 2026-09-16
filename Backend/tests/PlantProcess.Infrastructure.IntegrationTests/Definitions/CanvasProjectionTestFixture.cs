using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Infrastructure.Canonical;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// Shared integration fixture for Canvas projection declarations. It creates source
/// metadata only inside the runner-owned disposable database and derives every binding
/// from the canonical catalogue rather than hardcoding customer or industry fields.
/// </summary>
internal sealed class CanvasProjectionTestFixture : IAsyncDisposable
{
    private const string SourceSchema = "ppiq_staging";
    private const string SourceTable = "t0";
    private readonly DefinitionStoreFixture _fixture;
    private readonly string _sqlProjectionExpressions;

    private CanvasProjectionTestFixture(
        DefinitionStoreFixture fixture,
        string graphDeclarationJson,
        string alternateGraphDeclarationJson,
        string sqlDeclarationJson,
        string sqlProjectionExpressions)
    {
        _fixture = fixture;
        GraphDeclarationJson = graphDeclarationJson;
        AlternateGraphDeclarationJson = alternateGraphDeclarationJson;
        SqlDeclarationJson = sqlDeclarationJson;
        _sqlProjectionExpressions = sqlProjectionExpressions;
    }

    public string GraphDeclarationJson { get; }
    public string AlternateGraphDeclarationJson { get; }
    public string SqlDeclarationJson { get; }

    public static async Task<CanvasProjectionTestFixture> CreateAsync(
        DefinitionStoreFixture fixture, string target)
    {
        await using var db = fixture.NewContext();
        var catalog = new CanonicalEntityCatalog(db);
        var fields = catalog.ProjectionFieldsOf(target)
            .Where(f => !f.IsSystemOwned)
            .ToArray();

        if (fields.Length == 0)
            throw new InvalidOperationException(target + " exposes no authorable projection fields.");

        var required = fields.Where(f => f.IsRequired).ToArray();
        var unsupportedRequired = required.Where(f => SqlTypeFor(f.ClrTypeName) is null).ToArray();
        if (unsupportedRequired.Length != 0)
            throw new InvalidOperationException("Required projection fields have unsupported test types: "
                + string.Join(", ", unsupportedRequired.Select(f => f.Name + ":" + f.ClrTypeName)));

        var selected = required.Length != 0
            ? required
            : fields.Where(f => SqlTypeFor(f.ClrTypeName) is not null).Take(1).ToArray();
        if (selected.Length == 0)
            throw new InvalidOperationException(target + " has no authorable field with a type the integration fixture can represent.");

        var definitions = new List<string> { "quantity bigint" };
        var graphA = new List<ProjectionFieldBinding>();
        var graphB = new List<ProjectionFieldBinding>();
        var sqlBindings = new List<ProjectionFieldBinding>();
        var sqlExpressions = new List<string>();

        for (var i = 0; i < selected.Length; i++)
        {
            var field = selected[i];
            var sqlType = SqlTypeFor(field.ClrTypeName)!;
            var a = "t262_f" + i + "_a";
            var b = "t262_f" + i + "_b";
            var output = "t262_o" + i;
            definitions.Add(Quote(a) + " " + sqlType);
            definitions.Add(Quote(b) + " " + sqlType);
            graphA.Add(new ProjectionFieldBinding(field.Name, CanvasProjectionDeclaration.KindColumn, SourceTable, a));
            graphB.Add(new ProjectionFieldBinding(field.Name, CanvasProjectionDeclaration.KindColumn, SourceTable, b));
            sqlBindings.Add(new ProjectionFieldBinding(field.Name, CanvasProjectionDeclaration.KindSql, null, output));
            sqlExpressions.Add(LiteralFor(field.ClrTypeName) + " AS " + Quote(output));
        }

        await db.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS " + SourceSchema + "." + SourceTable + ";");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE " + SourceSchema + "." + SourceTable + " (" + string.Join(",", definitions) + ");");

        return new CanvasProjectionTestFixture(
            fixture,
            new CanvasProjectionDeclaration(target, graphA).ToJson(),
            new CanvasProjectionDeclaration(target, graphB).ToJson(),
            new CanvasProjectionDeclaration(target, sqlBindings).ToJson(),
            string.Join(", ", sqlExpressions));
    }

    public string SqlFor(string requested)
    {
        if (!CanWrap(requested)) return requested;
        var inner = requested.Trim();
        while (inner.EndsWith(";", StringComparison.Ordinal)) inner = inner[..^1].TrimEnd();
        return "WITH t262_original AS (" + inner + ") SELECT t262_original.*, "
            + _sqlProjectionExpressions + " FROM t262_original";
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = _fixture.NewContext();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS " + SourceSchema + "." + SourceTable + ";");
    }

    private static bool CanWrap(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var t = sql.Trim();
        if (!(t.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
              || t.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))) return false;
        var core = t.TrimEnd();
        while (core.EndsWith(";", StringComparison.Ordinal)) core = core[..^1].TrimEnd();
        if (core.Contains(';')) return false;
        foreach (var forbidden in new[] { " INSERT ", " UPDATE ", " DELETE ", " DROP ", " ALTER ", " TRUNCATE ", " CREATE " })
            if ((" " + core.ToUpperInvariant() + " ").Contains(forbidden, StringComparison.Ordinal)) return false;
        return true;
    }

    private static string? SqlTypeFor(string clr) => clr switch
    {
        "String" => "text",
        "Guid" => "uuid",
        "Boolean" => "boolean",
        "Int16" => "smallint",
        "Int32" => "integer",
        "Int64" => "bigint",
        "Single" => "real",
        "Double" => "double precision",
        "Decimal" => "numeric",
        "DateTime" => "timestamp with time zone",
        "DateTimeOffset" => "timestamp with time zone",
        "DateOnly" => "date",
        "TimeOnly" => "time without time zone",
        "TimeSpan" => "interval",
        "Byte[]" => "bytea",
        _ => null,
    };

    private static string LiteralFor(string clr) => clr switch
    {
        "String" => "CAST('t262' AS text)",
        "Guid" => "CAST('00000000-0000-0000-0000-000000000001' AS uuid)",
        "Boolean" => "CAST(true AS boolean)",
        "Int16" => "CAST(1 AS smallint)",
        "Int32" => "CAST(1 AS integer)",
        "Int64" => "CAST(1 AS bigint)",
        "Single" => "CAST(1.25 AS real)",
        "Double" => "CAST(1.25 AS double precision)",
        "Decimal" => "CAST(1.25 AS numeric)",
        "DateTime" => "CAST('2026-01-01T00:00:00Z' AS timestamp with time zone)",
        "DateTimeOffset" => "CAST('2026-01-01T00:00:00Z' AS timestamp with time zone)",
        "DateOnly" => "CAST('2026-01-01' AS date)",
        "TimeOnly" => "CAST('12:00:00' AS time without time zone)",
        "TimeSpan" => "CAST('00:01:00' AS interval)",
        "Byte[]" => "decode('01','hex')",
        _ => throw new InvalidOperationException("No SQL literal for CLR type " + clr),
    };

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}