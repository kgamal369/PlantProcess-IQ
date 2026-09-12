using System;
using System.Collections.Generic;

namespace PlantProcess.Application.Definitions.Canvas;

/// <summary>
/// PPIQ T-262. ONE AUTHORITY ON WHETHER A VALUE MAY BE WRITTEN TO A FIELD.
///
/// T-262 authors the declaration and T-261 executes it. If each carried its own idea
/// of compatibility they would eventually disagree, and the disagreement would surface
/// as a run that fails on a mapping the author was told was valid. One pure function,
/// two callers, no browser copy: a surface may DISPLAY this answer and never compute it.
///
/// THE RULE IS DELIBERATELY NARROW. Widening that cannot lose information is allowed.
/// Narrowing is refused. Implicit parsing of text into a number, a date or a boolean is
/// refused, and so is the reverse - a plant that wants a conversion says so with an
/// explicit derived operation, because a silent coercion is a decision nobody recorded.
///
/// An unknown source type is refused rather than assumed. "I could not determine this"
/// and "this is fine" are different answers and only one of them is honest.
/// </summary>
public static class ProjectionTypeCompatibility
{
    /// <summary>Semantic buckets. Physical spellings collapse into these.</summary>
    public enum Family
    {
        Unknown = 0,
        Text = 1,
        Integer = 2,
        Decimal = 3,
        Boolean = 4,
        Timestamp = 5,
        Date = 6,
        Time = 7,
        Guid = 8,
        Binary = 9,
    }

    /// <summary>Widths, so widening and narrowing can be told apart.</summary>
    private static readonly Dictionary<string, int> IntegerRank =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["byte"] = 1, ["sbyte"] = 1, ["int16"] = 2, ["short"] = 2, ["smallint"] = 2,
            ["int32"] = 4, ["int"] = 4, ["integer"] = 4, ["serial"] = 4,
            ["int64"] = 8, ["long"] = 8, ["bigint"] = 8, ["bigserial"] = 8,
        };

    private static readonly Dictionary<string, int> DecimalRank =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["single"] = 4, ["float"] = 4, ["real"] = 4,
            ["double"] = 8, ["double precision"] = 8,
            ["decimal"] = 16, ["numeric"] = 16, ["money"] = 16,
        };

    /// <summary>
    /// Maps a CLR name or a database type name onto a semantic family. Both vocabularies
    /// arrive here - the canonical side speaks CLR, the staged side speaks PostgreSQL -
    /// and collapsing them in one place is what stops two half-tables existing.
    /// </summary>
    public static Family FamilyOf(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) { return Family.Unknown; }

        var t = typeName.Trim().TrimEnd('?').ToLowerInvariant();

        // Array and precision decorations do not change the family.
        var paren = t.IndexOf('(');
        if (paren > 0) { t = t.Substring(0, paren).Trim(); }

        if (IntegerRank.ContainsKey(t)) { return Family.Integer; }
        if (DecimalRank.ContainsKey(t)) { return Family.Decimal; }

        switch (t)
        {
            case "string":
            case "char":
            case "text":
            case "varchar":
            case "character varying":
            case "character":
            case "citext":
            case "name":
                return Family.Text;

            case "bool":
            case "boolean":
                return Family.Boolean;

            case "datetime":
            case "datetimeoffset":
            case "timestamp":
            case "timestamp without time zone":
            case "timestamp with time zone":
            case "timestamptz":
                return Family.Timestamp;

            case "dateonly":
            case "date":
                return Family.Date;

            case "timeonly":
            case "timespan":
            case "time":
            case "time without time zone":
            case "interval":
                return Family.Time;

            case "guid":
            case "uuid":
                return Family.Guid;

            case "byte[]":
            case "bytea":
                return Family.Binary;

            default:
                return Family.Unknown;
        }
    }

    /// <summary>
    /// Whether a value of the source type may be written to the target field.
    /// Nullability is deliberately not consulted: a nullable source feeding a required
    /// field is a data-quality outcome T-261 quarantines row by row, not a reason to
    /// forbid a declaration that is type-correct.
    /// </summary>
    public static bool IsCompatible(string? sourceType, string? targetType)
    {
        var source = FamilyOf(sourceType);
        var target = FamilyOf(targetType);

        if (source == Family.Unknown || target == Family.Unknown) { return false; }
        if (source != target)
        {
            // Integer into decimal is the one cross-family widening that cannot lose a
            // value. Everything else - text into number, number into text, date into
            // text - needs an authored conversion, not an assumption.
            return source == Family.Integer && target == Family.Decimal;
        }

        if (target == Family.Integer)
        {
            return RankOf(IntegerRank, sourceType) <= RankOf(IntegerRank, targetType);
        }

        if (target == Family.Decimal)
        {
            return RankOf(DecimalRank, sourceType) <= RankOf(DecimalRank, targetType);
        }

        return true;
    }

    /// <summary>The sentence an author reads when a pair is refused.</summary>
    public static string Explain(string canonicalField, string? sourceName, string? sourceType, string? targetType)
    {
        if (FamilyOf(sourceType) == Family.Unknown)
        {
            return "The type of '" + (sourceName ?? "the source output")
                + "' could not be determined, so it cannot be bound to '" + canonicalField
                + "'. Give the output a determinate type with an explicit derived operation.";
        }

        return "'" + (sourceName ?? "source") + "' is " + sourceType + " and '" + canonicalField
            + "' is " + targetType + ". Writing one to the other would change the value, so it is "
            + "refused. Add an explicit conversion on the board if that is what you intend.";
    }

    private static int RankOf(Dictionary<string, int> ranks, string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) { return int.MaxValue; }
        var t = typeName.Trim().TrimEnd('?').ToLowerInvariant();
        var paren = t.IndexOf('(');
        if (paren > 0) { t = t.Substring(0, paren).Trim(); }
        return ranks.TryGetValue(t, out var rank) ? rank : int.MaxValue;
    }
}