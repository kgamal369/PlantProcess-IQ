using System.Data;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions.Canvas;

/// <summary>
/// PPIQ T-262. THE TYPE OF AN AUTHORED OUTPUT, RESOLVED BY THE SERVER.
///
/// A browser could send a type with every binding and it would be easier. It would also
/// be a claim the author can edit, and the store would then hold a mapping validated
/// against a type nobody measured. So the type is never persisted and never trusted
/// from the wire: it is resolved here, from the authority that already owns each kind
/// of source.
///
///   column   the staged column catalogue - the same reader the dataset endpoint uses
///   derived  the arithmetic grammar the compiler already enforces
///   sql      the validated statement's own row description
///
/// UNKNOWN IS A REFUSAL, NOT A DEFERRAL. A source this cannot type is refused at save
/// with PROJECTION_SOURCE_TYPE_UNKNOWN rather than passed to execution to discover at
/// three in the morning. "I could not determine this" and "this is fine" are different
/// answers and only one of them is honest.
/// </summary>
public sealed class ProjectionSourceTypes
{
    private readonly Dictionary<string, string> _columns;
    private readonly Dictionary<string, string> _derived;
    private readonly Dictionary<string, string> _sql;

    private ProjectionSourceTypes(
        Dictionary<string, string> columns,
        Dictionary<string, string> derived,
        Dictionary<string, string> sql)
    {
        _columns = columns;
        _derived = derived;
        _sql = sql;
    }

    /// <summary>Nothing resolvable. Every binding refuses as unknown.</summary>
    public static ProjectionSourceTypes Empty() => new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>The database type of the output a binding names, or null.</summary>
    public string? Resolve(ProjectionFieldBinding binding)
    {
        switch (binding.SourceKind)
        {
            case CanvasProjectionDeclaration.KindColumn:
                return _columns.TryGetValue(
                    Key(binding.SourceTable, binding.SourceField), out var c) ? c : null;

            case CanvasProjectionDeclaration.KindDerived:
                return _derived.TryGetValue(binding.SourceField, out var d) ? d : null;

            case CanvasProjectionDeclaration.KindSql:
                return _sql.TryGetValue(binding.SourceField, out var s) ? s : null;

            default:
                return null;
        }
    }

    private static string Key(string? table, string column) => (table ?? string.Empty) + "." + column;

    // ------------------------------------------------------------------ GRAPH

    public static async Task<ProjectionSourceTypes> ForGraphAsync(
        string graphJson,
        PlantProcessDbContext db,
        string stagingSchema,
        CancellationToken cancellationToken)
    {
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        var derived = new Dictionary<string, string>(StringComparer.Ordinal);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        foreach (var fact in await StagedColumnCatalogue.ReadAsync(connection, stagingSchema, cancellationToken))
        {
            columns[Key(fact.Table, fact.Column)] = fact.SqlType;
        }

        if (JsonNode.Parse(graphJson) is JsonObject graph && graph["derived"] is JsonArray list)
        {
            foreach (var item in list)
            {
                if (item is not JsonObject d) { continue; }
                var alias = d["alias"]?.GetValue<string>() ?? d["Alias"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(alias)) { continue; }

                var type = DerivedType(d, columns);
                if (type is not null) { derived[alias!] = type; }
            }
        }

        return new ProjectionSourceTypes(
            columns, derived, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// T-262. The type of a derived column, from the grammar the compiler already
    /// enforces: one arithmetic operation over two column references, or a column and a
    /// numeric constant.
    ///
    /// Division always widens, because an integer divided by an integer is not an
    /// integer in general and rounding silently would be the compiler deciding a
    /// business question. Anything the grammar cannot type deterministically returns
    /// null and the binding is refused by name.
    /// </summary>
    private static string? DerivedType(JsonObject d, Dictionary<string, string> columns)
    {
        var op = d["op"]?.GetValue<string>() ?? d["Op"]?.GetValue<string>();
        var leftTable = d["leftTable"]?.GetValue<string>() ?? d["LeftTable"]?.GetValue<string>();
        var leftColumn = d["leftColumn"]?.GetValue<string>() ?? d["LeftColumn"]?.GetValue<string>();
        if (op is null || leftColumn is null) { return null; }

        if (!columns.TryGetValue(Key(leftTable, leftColumn), out var leftType)) { return null; }
        var left = ProjectionTypeCompatibility.FamilyOf(leftType);
        if (left is not (ProjectionTypeCompatibility.Family.Integer or ProjectionTypeCompatibility.Family.Decimal))
        {
            return null;
        }

        var rightColumn = d["rightColumn"]?.GetValue<string>() ?? d["RightColumn"]?.GetValue<string>();
        ProjectionTypeCompatibility.Family right;
        if (!string.IsNullOrWhiteSpace(rightColumn))
        {
            var rightTable = d["rightTable"]?.GetValue<string>() ?? d["RightTable"]?.GetValue<string>();
            var rt = string.IsNullOrWhiteSpace(rightTable) ? leftTable : rightTable;
            if (!columns.TryGetValue(Key(rt, rightColumn!), out var rightType)) { return null; }
            right = ProjectionTypeCompatibility.FamilyOf(rightType);
            if (right is not (ProjectionTypeCompatibility.Family.Integer or ProjectionTypeCompatibility.Family.Decimal))
            {
                return null;
            }
        }
        else
        {
            var constant = d["constant"]?.GetValue<string>() ?? d["Constant"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(constant)) { return null; }
            if (!double.TryParse(constant, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return null;
            }
            right = constant!.Contains('.', StringComparison.Ordinal)
                ? ProjectionTypeCompatibility.Family.Decimal
                : ProjectionTypeCompatibility.Family.Integer;
        }

        if (op == "/") { return "numeric"; }

        return left == ProjectionTypeCompatibility.Family.Decimal
            || right == ProjectionTypeCompatibility.Family.Decimal
            ? "numeric"
            : "bigint";
    }

    // -------------------------------------------------------------------- SQL

    /// <summary>
    /// T-262. The validated statement's own row description.
    ///
    /// SchemaOnly PREPARES the statement and returns its column metadata WITHOUT
    /// running it, so save-time typing costs the customer's database a plan and not a
    /// scan. The statement is the one the safe-SQL authority already normalised; this
    /// adds no second validator and weakens nothing.
    /// </summary>
    public static async Task<ProjectionSourceTypes> ForSqlAsync(
        string normalisedSql,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var sql = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) { await connection.OpenAsync(cancellationToken); }

        try
        {
            await using var command = new NpgsqlCommand(normalisedSql, connection);
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SchemaOnly, cancellationToken);

            var schema = await reader.GetColumnSchemaAsync(cancellationToken);
            foreach (var column in schema)
            {
                var name = column.ColumnName;
                if (string.IsNullOrWhiteSpace(name)) { continue; }

                // A duplicate output name does not identify one value. It is removed
                // rather than overwritten, so a binding to it resolves unknown and the
                // ambiguity is refused by name instead of silently taking the last one.
                if (!sql.TryAdd(name, column.DataTypeName ?? string.Empty))
                {
                    duplicates.Add(name);
                }
            }

            foreach (var name in duplicates) { sql.Remove(name); }
        }
        catch (NpgsqlException)
        {
            // The statement could not even be described. Every sql binding then
            // resolves unknown and refuses, which is the honest outcome.
            return Empty();
        }

        return new ProjectionSourceTypes(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            sql);
    }
}