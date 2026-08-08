using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// T-073. The pure half of widget-result evidence: normalisation, fingerprints
/// and the one sentence a chunk carries. No IO, no database, no vocabulary.
///
/// Everything here is deterministic on purpose. Neither fingerprint includes the
/// generation timestamp and neither does the sentence, so an unchanged reindex
/// re-derives the same identity and reuses the evidence row that already exists
/// rather than minting a new one. The timestamp is still recorded in the
/// snapshot and is recoverable through the handle.
/// </summary>
public sealed record WidgetEvidenceIdentity(
    string PageCode,
    string WidgetCode,
    Guid? WidgetDefinitionId,
    string WidgetType,
    string ChartType,
    string? DimensionCode,
    string MeasureCode,
    string? ParameterCode);

/// <summary>The returned result reduced to text, in a fixed order.</summary>
public sealed record NormalisedWidgetResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int PopulationCount);

public static class WidgetResultEvidence
{
    /// <summary>Rows carried into a sentence. The snapshot keeps all of them.</summary>
    public const int MaxRowsInSentence = 6;

    private const string LabelColumn      = "dimensionLabel";
    private const string ValueColumn      = "value";
    private const string PopulationColumn = "observationCount";

    /// <summary>
    /// Reduces the executed result to ordered text. Values are formatted with
    /// the invariant culture and never rounded: a rounded number in evidence is
    /// a different number.
    /// </summary>
    public static NormalisedWidgetResult Normalise(
        IReadOnlyList<string> columns,
        IReadOnlyList<IDictionary<string, object?>> rows)
    {
        var orderedColumns = columns is null ? new List<string>() : columns.ToList();
        var normalisedRows = new List<IReadOnlyList<string>>();
        var population = 0;

        foreach (var row in rows ?? new List<IDictionary<string, object?>>())
        {
            var cells = new List<string>();
            foreach (var column in orderedColumns)
            {
                row.TryGetValue(column, out var value);
                cells.Add(Text(value));
            }

            normalisedRows.Add(cells);

            if (row.TryGetValue(PopulationColumn, out var populationValue) &&
                int.TryParse(Text(populationValue), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                population += parsed;
            }
        }

        return new NormalisedWidgetResult(orderedColumns, normalisedRows, population);
    }

    /// <summary>Identity of the question that was asked, without its answer.</summary>
    public static string QueryFingerprint(WidgetEvidenceIdentity identity, string filterContextJson)
    {
        var parts = new List<string>
        {
            identity.PageCode,
            identity.WidgetCode,
            identity.WidgetDefinitionId?.ToString() ?? string.Empty,
            identity.WidgetType,
            identity.ChartType,
            identity.DimensionCode ?? string.Empty,
            identity.MeasureCode,
            identity.ParameterCode ?? string.Empty,
            filterContextJson ?? "{}"
        };

        return Hash(string.Join("\u001f", parts));
    }

    /// <summary>Identity of the question AND the exact answer it returned.</summary>
    public static string ResultFingerprint(string queryFingerprint, NormalisedWidgetResult result)
    {
        var builder = new StringBuilder();
        builder.Append(queryFingerprint);
        builder.Append("\u001e");
        builder.Append(string.Join("\u001f", result.Columns));

        foreach (var row in result.Rows)
        {
            builder.Append("\u001e");
            builder.Append(string.Join("\u001f", row));
        }

        builder.Append("\u001e");
        builder.Append(result.PopulationCount.ToString(CultureInfo.InvariantCulture));

        return Hash(builder.ToString());
    }

    /// <summary>
    /// One true sentence about what a widget returned. Every number in it comes
    /// from the result or is counted from it - nothing is authored, and no
    /// industry, plant or demo word appears, because every noun is a code that
    /// the installation itself defined.
    /// </summary>
    public static string Sentence(WidgetEvidenceIdentity identity, NormalisedWidgetResult result)
    {
        var builder = new StringBuilder();

        builder.Append("On page ").Append(identity.PageCode);
        builder.Append(", widget ").Append(identity.WidgetCode);
        builder.Append(" shows ").Append(identity.MeasureCode);

        if (!string.IsNullOrWhiteSpace(identity.DimensionCode))
        {
            builder.Append(" by ").Append(identity.DimensionCode);
        }

        builder.Append(" as a ").Append(identity.ChartType);

        var labelIndex = IndexOfColumn(result, LabelColumn);
        var valueIndex = IndexOfColumn(result, ValueColumn);

        if (result.Rows.Count == 0 || labelIndex < 0 || valueIndex < 0)
        {
            builder.Append(", and returned no rows.");
            return builder.ToString();
        }

        builder.Append(": ");

        var written = 0;
        foreach (var row in result.Rows)
        {
            if (written >= MaxRowsInSentence) break;
            if (written > 0) builder.Append("; ");

            builder.Append(row[labelIndex]).Append(' ').Append(row[valueIndex]);
            written = written + 1;
        }

        builder.Append(". That is ").Append(result.Rows.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append(result.Rows.Count == 1 ? " result row" : " result rows");

        if (result.PopulationCount > 0)
        {
            builder.Append(" over a population of ");
            builder.Append(result.PopulationCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(result.PopulationCount == 1 ? " observation" : " observations");
        }

        builder.Append('.');
        return builder.ToString();
    }

    private static int IndexOfColumn(NormalisedWidgetResult result, string column)
    {
        for (var i = 0; i < result.Columns.Count; i++)
        {
            if (string.Equals(result.Columns[i], column, StringComparison.Ordinal)) return i;
        }

        return -1;
    }

    private static string Text(object? value)
    {
        if (value is null) return string.Empty;

        return value switch
        {
            string s          => s,
            double d          => d.ToString("R", CultureInfo.InvariantCulture),
            float f           => f.ToString("R", CultureInfo.InvariantCulture),
            decimal m         => m.ToString(CultureInfo.InvariantCulture),
            DateTime dt       => dt.ToString("O", CultureInfo.InvariantCulture),
            IFormattable other => other.ToString(null, CultureInfo.InvariantCulture),
            _                 => value.ToString() ?? string.Empty
        };
    }

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}