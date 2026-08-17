using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Services.Queries;

/// <summary>
/// PR-050-01. What each returned row REPRESENTS, derived from the query
/// semantics alone. Pure: no database, no clock, no vocabulary.
///
/// The whole point of this type is the distinction the drill-down rests on.
/// The execution evidence handle identifies ONE widget execution. This
/// identifies the POPULATION PREDICATE behind one point of that execution.
/// Neither is physical row lineage, and nothing here may be presented as such.
/// </summary>
public static class DashboardPopulationDescriptor
{
    /// <summary>
    /// The result contract's own count column. Read when the executing source
    /// supplies it, absent otherwise. It is never synthesised.
    /// </summary>
    public const string ObservationCountColumn = "observationCount";

    private const string DimensionLabelColumn = "dimensionLabel";
    private const string StringDataType = "string";

    private const char FieldSeparator = '\u001f';
    private const char GroupSeparator = '\u001e';

    private static readonly PropertyInfo[] FilterProperties =
        typeof(DashboardWidgetFiltersDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// The effective filter context, canonicalised so that ordering and
    /// serialisation noise cannot mint a second evidence identity for the same
    /// population.
    ///
    /// Fields are discovered by REFLECTION over the filter contract rather than
    /// listed here. That is deliberate on two counts. It keeps this file free of
    /// the plant vocabulary the current filter contract still carries, which is
    /// recorded debt owned elsewhere and must not be copied into the engine. And
    /// it means a change to the filter contract is followed automatically
    /// instead of silently dropping a filter out of the evidence identity - the
    /// failure mode where two genuinely different populations would share one
    /// fingerprint.
    ///
    /// No absent field is emitted, so a query with no filters canonicalises to
    /// the same "{}" the reindex path has always written, and evidence already
    /// in the store stays reusable.
    /// </summary>
    public static string CanonicaliseFilterContext(DashboardWidgetFiltersDto? filters)
    {
        if (filters is null) return "{}";

        var parts = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in FilterProperties)
        {
            var formatted = Format(property.GetValue(filters));
            if (formatted is not null) parts[Camel(property.Name)] = formatted;
        }

        if (parts.Count == 0) return "{}";

        var builder = new StringBuilder("{");
        var first = true;
        foreach (var pair in parts)
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append('"').Append(EscapeJson(pair.Key)).Append("\":\"").Append(EscapeJson(pair.Value)).Append('"');
        }

        return builder.Append('}').ToString();
    }

    /// <summary>
    /// The population predicate a point represents, hashed.
    ///
    /// It depends on semantic identity ONLY: the effective filter context, the
    /// measure, the parameter and the dimension bindings with their values. It
    /// deliberately excludes the aggregate value, the rendered label, the
    /// generation time and the row's position, so that re-ordering a result
    /// cannot invent new populations and a moved number cannot masquerade as a
    /// moved population.
    /// </summary>
    public static string Fingerprint(
        string canonicalFilterJson,
        string measureCode,
        string? parameterCode,
        IEnumerable<KeyValuePair<string, string?>> orderedBindings)
    {
        var builder = new StringBuilder();
        builder.Append(canonicalFilterJson);
        builder.Append(GroupSeparator);
        builder.Append(measureCode).Append(FieldSeparator).Append(parameterCode ?? string.Empty);

        foreach (var binding in orderedBindings)
        {
            builder.Append(GroupSeparator);
            builder.Append(binding.Key).Append(FieldSeparator).Append(binding.Value ?? string.Empty);
        }

        return Hash(builder.ToString());
    }

    /// <summary>
    /// One descriptor per returned row, in row order, for EVERY execution path.
    ///
    /// Bindings are the categorical coordinates of the row: the string-typed
    /// result columns, minus the presentation label. That rule is the same for a
    /// grouped aggregate and for a native source, which is what lets one
    /// post-processing step serve both without rewriting either.
    ///
    /// A descriptor may be partially populated, but never untruthful:
    ///   PopulationCount is null unless the result itself supplied a count.
    ///   RowFingerprint is null when this execution genuinely cannot express a
    ///   distinct predicate for the row - either because no binding could be
    ///   derived from a multi-row result, or because two rows would otherwise
    ///   collide on one identity. A missing identity is honest; a colliding one
    ///   would send a drill-down to the wrong population.
    /// </summary>
    public static IReadOnlyList<DashboardWidgetRowPopulationDto> Describe(
        DashboardWidgetResolvedDto resolved,
        IReadOnlyList<DashboardWidgetColumnDto> columns,
        IReadOnlyList<IDictionary<string, object?>> rows,
        string canonicalFilterJson)
    {
        var filterFingerprint = Hash(canonicalFilterJson);

        var bindingColumns = columns
            .Where(column => string.Equals(column.DataType, StringDataType, StringComparison.Ordinal))
            .Where(column => !string.Equals(column.Code, DimensionLabelColumn, StringComparison.Ordinal))
            .Select(column => column.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var descriptors = new List<DashboardWidgetRowPopulationDto>(rows.Count);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var bindings = new SortedDictionary<string, string?>(StringComparer.Ordinal);

            foreach (var code in bindingColumns)
            {
                if (row.TryGetValue(code, out var raw))
                    bindings[code] = raw is null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);
            }

            var fingerprint = bindings.Count == 0 && rows.Count > 1
                ? null
                : Fingerprint(canonicalFilterJson, resolved.MeasureCode, resolved.ParameterCode, bindings);

            descriptors.Add(new DashboardWidgetRowPopulationDto(
                index,
                fingerprint,
                bindings,
                resolved.MeasureCode,
                resolved.ParameterCode,
                filterFingerprint,
                ReadPopulationCount(row)));
        }

        return WithdrawColliding(descriptors);
    }

    /// <summary>
    /// Two rows that resolved to the same identity are two rows this execution
    /// cannot tell apart. Both lose the identity rather than sharing one.
    /// </summary>
    private static IReadOnlyList<DashboardWidgetRowPopulationDto> WithdrawColliding(
        List<DashboardWidgetRowPopulationDto> descriptors)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (descriptor.RowFingerprint is null) continue;
            counts[descriptor.RowFingerprint] = counts.TryGetValue(descriptor.RowFingerprint, out var seen) ? seen + 1 : 1;
        }

        if (!counts.Values.Any(count => count > 1)) return descriptors;

        for (var index = 0; index < descriptors.Count; index++)
        {
            var descriptor = descriptors[index];
            if (descriptor.RowFingerprint is not null && counts[descriptor.RowFingerprint] > 1)
                descriptors[index] = descriptor with { RowFingerprint = null };
        }

        return descriptors;
    }

    /// <summary>
    /// The count the result itself carried, or nothing. It is NEVER the number
    /// of returned rows: five bars do not mean five of anything.
    /// </summary>
    private static int? ReadPopulationCount(IDictionary<string, object?> row)
    {
        if (!row.TryGetValue(ObservationCountColumn, out var raw) || raw is null) return null;

        return raw switch
        {
            int value => value,
            long value when value >= int.MinValue && value <= int.MaxValue => (int)value,
            short value => value,
            decimal value when value == Math.Floor(value) && value >= int.MinValue && value <= int.MaxValue => (int)value,
            double value when Math.Abs(value - Math.Floor(value)) < double.Epsilon && value >= int.MinValue && value <= int.MaxValue => (int)value,
            _ => null
        };
    }

    private static string? Format(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return string.IsNullOrWhiteSpace(text) ? null : text;
            case DateTime moment:
                return moment.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            case Guid id:
                return id.ToString("D", CultureInfo.InvariantCulture);
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string EscapeJson(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':  builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b");  break;
                case '\f': builder.Append("\\f");  break;
                case '\n': builder.Append("\\n");  break;
                case '\r': builder.Append("\\r");  break;
                case '\t': builder.Append("\\t");  break;
                default:
                    if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    else builder.Append(character);
                    break;
            }
        }
        return builder.ToString();
    }

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}