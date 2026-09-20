// THE ONE TRANSFORMATION GRAMMAR.
//
// Moved out of the API endpoint that used to own it privately, so the HTTP preview
// path and the governed job executor compile through exactly the same code. The
// regions below were relocated byte-for-byte rather than retyped: a second grammar
// that merely looks the same is the defect this move exists to prevent.
//
// Only two members were made visible - identifier validation and safe SELECT
// construction. The operator whitelists stay private, because a caller able to widen
// them would be authoring SQL policy from outside the authority that owns it.
using System.Text;

namespace PlantProcess.Application.Definitions.Transformations;
public record JoinSpec(string LeftTable, string LeftColumn, string RightTable, string RightColumn);

// M1-16. A filter is a WHERE predicate. Op comes from a whitelist and Value
// is NEVER placed in the SQL string - it is bound as a parameter.
public record FilterSpec(string Table, string Column, string Op, string? Value);

// M1-16. A derived column is one arithmetic operation over two column
// references, or a column and a numeric constant. Alias is quoted on emit.
public record DerivedSpec(string Alias, string LeftTable, string LeftColumn, string Op,
                          string? RightTable, string? RightColumn, string? Constant);

// T-033 item 1. A SELECT block projects qualified fields and nothing
// more. There is deliberately NO alias on this record: naming an output
// column is a Rename, and ruling 1 of T-033 is "Select, NOT Rename".
// An alias is a later grammar expansion, not a change to this shape.
public record SelectSpec(string Table, string Column);

// Filters, Derived and Selects default to null so every earlier graph
// deserialises unchanged and compiles to byte-identical SQL.
// T-243. Board is the AUTHORED representation - blocks, positions, wiring, purpose.
// It rides on this record because the session draft is one jsonb blob and this task
// introduces no second transport; the endpoint that binds this record re-serialises
// it, so a property it does not declare would be silently dropped. Nothing in the
// SQL generator reads it. CanvasDefinitionContent lifts it to the content root at
// publish, which is where it becomes canonical.
// T-262. Projection is the authored business-field declaration. It rides here for
// the same reason Board does - the session draft is one jsonb blob and a property
// this record does not declare is silently dropped when the endpoint re-serialises.
// Nothing in the SQL generator reads it; CanvasDefinitionContent lifts it to the
// content root at save, which is where it becomes canonical.
public record MapperGraph(string Name, string TargetEntity, string[] Tables, JoinSpec[] Joins,
                          FilterSpec[]? Filters = null, DerivedSpec[]? Derived = null,
                          SelectSpec[]? Selects = null,
                          System.Text.Json.Nodes.JsonNode? Board = null,
                          System.Text.Json.Nodes.JsonNode? Projection = null);

/// <summary>
/// WHY A READ IS HAPPENING, WHICH IS NOT THE SAME QUESTION AS WHAT IT SELECTS.
///
/// The preview cap of fifty rows is a bounded-inspection behaviour: it exists so an
/// author can look at a board without pulling a dataset through a browser. It was written
/// into the only compiler, so governed execution inherited it and would have silently
/// projected the first fifty rows of a customer's data and called the run complete.
///
/// The cap therefore belongs to the PURPOSE, not to the grammar. Execution is bounded by
/// the dataset, window and job contract that selected it, and no second magic number is
/// invented here to replace the first.
/// </summary>
public enum TransformationReadPurpose
{
    Preview = 0,
    Execution = 1
}

public static class TransformationSafeSelect
{
    public static bool Ident(string? s)
        => s is not null && System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-zA-Z0-9_]+$");

    // Whitelists. Anything outside them is refused with a named reason and the
    // dry run is recorded as rejected_by_safe_sql, exactly as an illegal
    // identifier already is. This is the honest-refusal contract applied to
    // predicates rather than to identifiers.
    private static readonly string[] FilterOps =
        { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL" };
    private static readonly string[] MathOps = { "+", "-", "*", "/" };

    /// Server-side SQL from the graph: staging-only identifiers, equality joins,
    /// whitelisted predicates with bound values, whitelisted arithmetic, LIMIT.
    public static (string? sql, string? err, List<object>? prms) BuildSafeSelect(MapperGraph g, string schema)
        => BuildSafeSelect(g, schema, TransformationReadPurpose.Preview);

    public static (string? sql, string? err, List<object>? prms) BuildSafeSelect(
        MapperGraph g,
        string schema,
        TransformationReadPurpose purpose)
    {
        if (g.Tables.Length == 0) return (null, "graph has no tables", null);
        if (!Ident(schema)) return (null, $"illegal schema identifier '{schema}'", null);
        foreach (var t in g.Tables)
            if (!Ident(t)) return (null, $"illegal table identifier '{t}'", null);
        foreach (var j in g.Joins)
            foreach (var c in new[] { j.LeftColumn, j.RightColumn })
                if (!Ident(c)) return (null, $"illegal column identifier '{c}'", null);

        // Alias map is built first so the SELECT list can reference it.
        var alias = new Dictionary<string, string> { [g.Tables[0]] = "t0" };
        var i = 1;
        foreach (var t in g.Tables.Skip(1)) { alias[t] = $"t{i}"; i++; }

        var prms = new List<object>();

        // ---- SELECT list: the projection, plus one column per derived expression
        // T-033 item 1. THREE STATES, AND THE MIDDLE ONE IS THE POINT.
        //
        //   Selects is null       no Select block is on the board. The
        //                         projection stays SELECT *, so every graph
        //                         saved before T-033 compiles to the same
        //                         statement it compiled to before.
        //   Selects is empty      a Select block IS on the board with
        //                         nothing chosen. Emitting SELECT * here
        //                         would return the opposite of what the
        //                         author asked for, so it is refused with a
        //                         sentence rather than defaulted.
        //   Selects has entries   project exactly those qualified fields.
        //
        // Ruling 2: a field whose table is not on the board is refused. The
        // table is NEVER inferred from anything else in the graph.
        var select = new StringBuilder();
        if (g.Selects is null)
        {
            select.Append("SELECT *");
        }
        else if (g.Selects.Length == 0)
        {
            return (null, "the Select block has no columns chosen. Choose at least one column, or remove the block.", null);
        }
        else
        {
            var projected = new List<string>();
            foreach (var sel in g.Selects)
            {
                if (!alias.ContainsKey(sel.Table)) return (null, $"selected column references table '{sel.Table}' which is not on the board", null);
                if (!Ident(sel.Column)) return (null, $"illegal column identifier '{sel.Column}'", null);
                projected.Add($"{alias[sel.Table]}.\"{sel.Column}\"");
            }
            select.Append("SELECT ").Append(string.Join(", ", projected));
        }
        foreach (var d in g.Derived ?? Array.Empty<DerivedSpec>())
        {
            if (!Ident(d.Alias)) return (null, $"illegal derived alias '{d.Alias}'", null);
            if (!alias.ContainsKey(d.LeftTable)) return (null, $"derived column references table '{d.LeftTable}' which is not on the board", null);
            if (!Ident(d.LeftColumn)) return (null, $"illegal column identifier '{d.LeftColumn}'", null);
            if (!MathOps.Contains(d.Op)) return (null, $"operator '{d.Op}' is not permitted in a derived column", null);

            var left = $"{alias[d.LeftTable]}.\"{d.LeftColumn}\"";
            string right;
            if (!string.IsNullOrWhiteSpace(d.RightColumn))
            {
                var rt = string.IsNullOrWhiteSpace(d.RightTable) ? d.LeftTable : d.RightTable!;
                if (!alias.ContainsKey(rt)) return (null, $"derived column references table '{rt}' which is not on the board", null);
                if (!Ident(d.RightColumn)) return (null, $"illegal column identifier '{d.RightColumn}'", null);
                right = $"{alias[rt]}.\"{d.RightColumn}\"";
            }
            else if (double.TryParse(d.Constant, System.Globalization.NumberStyles.Any,
                                     System.Globalization.CultureInfo.InvariantCulture, out var cv))
            {
                // A numeric constant is emitted in invariant form. It cannot carry
                // a quote or an identifier, so it is safe without a parameter.
                right = cv.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return (null, $"derived column '{d.Alias}' needs a second column or a numeric constant", null);
            }

            // Division guards against divide-by-zero rather than failing the run.
            if (d.Op == "/") select.Append(", (").Append(left).Append(" / NULLIF(").Append(right).Append(", 0)) AS \"").Append(d.Alias).Append('"');
            else select.Append(", (").Append(left).Append(' ').Append(d.Op).Append(' ').Append(right).Append(") AS \"").Append(d.Alias).Append('"');
        }

        // ---- FROM and JOIN
        var sb = new StringBuilder();
        sb.Append(select).Append(" FROM \"").Append(schema).Append("\".\"").Append(g.Tables[0]).Append("\" t0");
        // T-032. A JOIN MAY ONLY REFERENCE TABLES ALREADY IN THE FROM CLAUSE.
        //
        // The previous filter tested alias.ContainsKey on both sides of every
        // join, but the alias map is built for EVERY table BEFORE this loop
        // starts, so that test was always true and filtered nothing.
        //
        // With three tables wired t0-t1 and t1-t2, the ON clause emitted while
        // joining t1 contained "t1.x = t2.y" - and t2 was not in the query yet.
        // PostgreSQL refused with 42P01, missing FROM-clause entry for table
        // "t2". Two tables always worked, which is why this survived: the path
        // had never been run, and a live check found ZERO sessions ever created.
        //
        // `emitted` tracks what is genuinely in the FROM clause so far.
        // T-032. THE PLANNER WORKS FROM A FRONTIER, NOT FROM LIST ORDER.
        //
        // Two things are being satisfied here and they are not the same thing.
        //
        // THE SCOPE INVARIANT. An ON clause may reference only aliases already
        // in the FROM clause plus the alias this JOIN introduces. Breaking it
        // produced 42P01, missing FROM-clause entry for table "t2", because an
        // earlier version filtered joins on alias.ContainsKey - and the alias
        // map is built for EVERY table before any SQL is emitted, so that test
        // was always true and filtered nothing.
        //
        // THE REACHABILITY INVARIANT. Which table is emitted next is decided by
        // CONNECTIVITY, never by position in g.Tables. The board sends tables in
        // the order the author dropped them, so a legal graph wired A-B and B-C
        // can arrive as [A, C, B]. Walking the list in order would reach C,
        // find no edge back to {A}, and refuse a graph that is perfectly valid.
        //
        // The loop below takes, on each pass, any pending table with an edge to
        // something already emitted, in either direction - LeftTable or
        // RightTable, it makes no difference. Only when NO pending table can be
        // reached is the graph genuinely disconnected, and then it is refused
        // with a sentence rather than compiled into SQL that cannot run.
        var emitted = new HashSet<string> { g.Tables[0] };
        var pending = new List<string>(g.Tables.Skip(1));
        while (pending.Count > 0)
        {
            string? next = null;
            var nextJoins = Array.Empty<JoinSpec>();
            foreach (var candidate in pending)
            {
                var edges = g.Joins
                    .Where(j => (j.LeftTable == candidate && emitted.Contains(j.RightTable))
                             || (j.RightTable == candidate && emitted.Contains(j.LeftTable)))
                    .ToArray();
                if (edges.Length > 0) { next = candidate; nextJoins = edges; break; }
            }
            if (next is null)
            {
                return (null, $"table '{pending[0]}' has no join reaching the rest of the board. Wire it to a table that is already connected, or remove it.", null);
            }
            sb.Append(" JOIN \"").Append(schema).Append("\".\"").Append(next).Append("\" ").Append(alias[next]).Append(" ON ");
            sb.Append(string.Join(" AND ", nextJoins.Select(j =>
                $"{alias[j.LeftTable]}.\"{j.LeftColumn}\" = {alias[j.RightTable]}.\"{j.RightColumn}\"")));
            emitted.Add(next);
            pending.Remove(next);
        }

        // ---- WHERE: whitelisted operators, values ALWAYS bound
        var preds = new List<string>();
        foreach (var f in g.Filters ?? Array.Empty<FilterSpec>())
        {
            if (!alias.ContainsKey(f.Table)) return (null, $"filter references table '{f.Table}' which is not on the board", null);
            if (!Ident(f.Column)) return (null, $"illegal column identifier '{f.Column}'", null);
            var op = (f.Op ?? string.Empty).Trim().ToUpperInvariant();
            if (op is "=" or "<>" or ">" or ">=" or "<" or "<=") { /* symbols keep their case */ }
            if (!FilterOps.Contains(op)) return (null, $"operator '{f.Op}' is not permitted in a filter", null);

            var colRef = $"{alias[f.Table]}.\"{f.Column}\"";
            if (op is "IS NULL" or "IS NOT NULL")
            {
                preds.Add($"{colRef} {op}");
                continue;
            }
            if (f.Value is null) return (null, $"filter on '{f.Column}' needs a value for operator '{op}'", null);

            // Bound as a number when it reads as one, so a comparison against a
            // numeric column works; otherwise as text.
            object val = double.TryParse(f.Value, System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out var nv)
                         ? nv : f.Value;
            prms.Add(val);
            preds.Add($"{colRef} {op} ${prms.Count}");
        }
        if (preds.Count > 0) sb.Append(" WHERE ").Append(string.Join(" AND ", preds));

        if (purpose == TransformationReadPurpose.Preview)
        {
            sb.Append(" LIMIT 50;");
        }
        return (sb.ToString(), null, prms);
    }
}
