using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Services.Queries;

// ============================================================================
// LAYER A - GENERIC EXACT BI AGGREGATION ENGINE.
//
// Charter frozen 11 Aug 2026. This file answers deterministic BI questions
// exactly. It never predicts and never estimates. If the authorised population
// holds 301,560 observations the answer is 301,560.
//
// GENERICITY IS THE POINT. Nothing here knows a dashboard code, a widget code,
// a seeded identity, a dataset size or an industry. It reasons only from
// governed semantics: measure, aggregation family, registered dimension,
// filters, time window, sort, MaxRows. A customer may be oil, water, pharma,
// paper, tyres, cement or food; a newly authored widget on any registered
// dimension runs here with no recompilation.
//
// WHAT IT REPLACES. The previous execution model was
//     filter -> Take(RawRowLimit) -> materialise -> GroupBy in C# -> aggregate
// which computed every aggregate over an arbitrary capped sample. Measured:
// 50,000 returned against 301,560 true, 83.4 percent missing, and unstable
// between identical calls because a LIMIT without ORDER BY may return any rows.
// See docs/m1/evidence/T-044/A1_aggregate_truth.md.
//
// MANDATORY ORDER, and it is the whole design:
//   authorised population -> filters -> time predicate -> dimension projection
//   -> GROUP/AGGREGATE in PostgreSQL -> deterministic ordering
//   -> MaxRows on AGGREGATE GROUPS -> display-label enrichment.
//
// Only aggregated groups cross into memory. Raw facts never do.
// ============================================================================

/// <summary>
/// The relational fact contract every measure projects into. It is an INTERNAL
/// analytical shape, not a customer schema: the customer's tables and columns
/// are mapped into it by the measure's own source projection, and the executor
/// below never learns a customer's schema.
/// </summary>
internal sealed record WidgetFact(
    Guid? MaterialUnitId,
    Guid? SiteId,
    Guid? AreaId,
    Guid? EquipmentId,
    string? MaterialCode,
    string? MaterialUnitType,
    string? ProductFamily,
    string? GradeOrRecipe,
    string? SourceSystem,
    string? ShiftCode,
    string? DefectType,
    string? ParameterCode,
    string? RiskClass,
    DateTime? EventTimeUtc,
    decimal Value);

internal sealed record DashboardAggregateRow(
    string DimensionKey,
    string DimensionLabel,
    decimal Value,
    int ObservationCount,
    int SecondaryCount);

internal sealed record DimensionValue(string Key, string Label);

/// <summary>
/// The SQL-side grouping key.
///
/// One shape for every dimension so the executor has ONE generic GroupBy rather
/// than one per dimension. Only the slots a given dimension uses are populated;
/// the rest stay null and PostgreSQL groups on the columns actually referenced.
///
/// Nullable GUID dimensions group by the NATIVE Guid. They are deliberately not
/// stringified inside the grouping expression: Npgsql translation of
/// Guid.ToString() is the known failure point, and a cast in the GROUP BY would
/// also defeat any index on the id column. Stringification and human labels
/// happen after aggregation, where they cost nothing.
/// </summary>
internal sealed class DashboardGroupKey
{
    public string? Text { get; set; }

    public Guid? Id { get; set; }

    public int? Year { get; set; }

    public int? Month { get; set; }

    public int? Day { get; set; }
}

/// <summary>
/// THE SINGLE DIMENSION AUTHORITY.
///
/// The only place in this engine that turns a registered dimension code into an
/// EF/Npgsql-translatable grouping expression, and the only place that turns a
/// grouped key back into a canonical identity and a fallback label. No measure
/// method may carry its own dimension switch.
///
/// A dimension code that is not registered here is not executable, and says so
/// rather than grouping everything under "unknown".
/// </summary>
internal static class DashboardDimensionProjection
{
    /// <summary>
    /// Temporal dimensions are grouped in SQL at DAY grain and folded to week or
    /// month afterwards. That fold is exact for mergeable families only, and the
    /// executor refuses to apply it to any other family. Grouping at day grain
    /// keeps one calendar authority - the existing week and month formatting -
    /// rather than reimplementing ISO arithmetic in SQL, which the charter
    /// forbids doing silently.
    /// </summary>
    public static bool IsTemporal(string? dimensionCode)
    {
        return IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Day)
            || IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Week)
            || IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Month);
    }

    public static bool IsRegistered(string? dimensionCode)
    {
        if (string.IsNullOrWhiteSpace(dimensionCode)) return true;
        return KeySelectorOrNull(dimensionCode) is not null;
    }

    public static Expression<Func<WidgetFact, DashboardGroupKey>> KeySelector(string? dimensionCode)
    {
        var selector = KeySelectorOrNull(dimensionCode);

        if (selector is null)
        {
            throw new DashboardDimensionNotRegisteredException(dimensionCode ?? "(null)");
        }

        return selector;
    }

    private static Expression<Func<WidgetFact, DashboardGroupKey>>? KeySelectorOrNull(string? dimensionCode)
    {
        if (string.IsNullOrWhiteSpace(dimensionCode))
            return f => new DashboardGroupKey { Text = "kpi" };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Site))
            return f => new DashboardGroupKey { Id = f.SiteId };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Area))
            return f => new DashboardGroupKey { Id = f.AreaId };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Equipment))
            return f => new DashboardGroupKey { Id = f.EquipmentId };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.SourceSystem))
            return f => new DashboardGroupKey { Text = f.SourceSystem };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.MaterialUnitType))
            return f => new DashboardGroupKey { Text = f.MaterialUnitType };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ProductFamily))
            return f => new DashboardGroupKey { Text = f.ProductFamily };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.GradeOrRecipe))
            return f => new DashboardGroupKey { Text = f.GradeOrRecipe };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ShiftCode))
            return f => new DashboardGroupKey { Text = f.ShiftCode };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.DefectType))
            return f => new DashboardGroupKey { Text = f.DefectType };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ParameterCode))
            return f => new DashboardGroupKey { Text = f.ParameterCode };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.RiskClass))
            return f => new DashboardGroupKey { Text = f.RiskClass };

        if (IsTemporal(dimensionCode))
        {
            return f => new DashboardGroupKey
            {
                Year = f.EventTimeUtc.HasValue ? f.EventTimeUtc.Value.Year : (int?)null,
                Month = f.EventTimeUtc.HasValue ? f.EventTimeUtc.Value.Month : (int?)null,
                Day = f.EventTimeUtc.HasValue ? f.EventTimeUtc.Value.Day : (int?)null
            };
        }

        return null;
    }

    /// <summary>
    /// Canonical identity and fallback label for a grouped key. Identity is what
    /// selections, filters and drill-through travel on; the label is only ever
    /// read by a person. They are separate concepts and are never swapped.
    ///
    /// The strings produced here are byte-identical to what the previous C#
    /// grouping produced, so no saved selection or bookmark changes meaning.
    /// </summary>
    public static DimensionValue Describe(string? dimensionCode, DashboardGroupKey key)
    {
        if (string.IsNullOrWhiteSpace(dimensionCode))
            return new DimensionValue("kpi", "KPI");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Site))
            return FromId(key.Id, "No site");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Area))
            return FromId(key.Id, "No area");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Equipment))
            return FromId(key.Id, "No equipment");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.SourceSystem))
            return FromText(key.Text, "No source system");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.MaterialUnitType))
            return FromText(key.Text, "No material type");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ProductFamily))
            return FromText(key.Text, "No product family");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.GradeOrRecipe))
            return FromText(key.Text, "No grade / recipe");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ShiftCode))
            return FromText(key.Text, "No shift");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.DefectType))
            return FromText(key.Text, "No defect");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ParameterCode))
            return FromText(key.Text, "No parameter");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.RiskClass))
            return FromText(key.Text, "No risk class");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Day))
            return FromDate(key, "yyyy-MM-dd", "No day");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Month))
            return FromDate(key, "yyyy-MM", "No month");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.Week))
            return FromWeek(key);

        throw new DashboardDimensionNotRegisteredException(dimensionCode);
    }

    private static bool IsCode(string? actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static DimensionValue FromId(Guid? id, string fallback)
    {
        if (!id.HasValue) return new DimensionValue("unknown", fallback);
        var text = id.Value.ToString();
        return new DimensionValue(text, text);
    }

    private static DimensionValue FromText(string? text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return new DimensionValue("unknown", fallback);
        var trimmed = text.Trim();
        return new DimensionValue(trimmed, trimmed);
    }

    private static DateTime? DateOf(DashboardGroupKey key)
    {
        if (!key.Year.HasValue || !key.Month.HasValue || !key.Day.HasValue) return null;
        return new DateTime(key.Year.Value, key.Month.Value, key.Day.Value);
    }

    private static DimensionValue FromDate(DashboardGroupKey key, string format, string fallback)
    {
        var date = DateOf(key);
        if (!date.HasValue) return new DimensionValue("unknown", fallback);
        var text = date.Value.ToString(format);
        return new DimensionValue(text, text);
    }

    private static DimensionValue FromWeek(DashboardGroupKey key)
    {
        var date = DateOf(key);
        if (!date.HasValue) return new DimensionValue("unknown", "No week");

        // The existing calendar authority, unchanged. Correcting week semantics
        // to ISO 8601 is a separate ruled change: fixing truncation must not
        // silently move a calendar boundary at the same time.
        var firstDayOfYear = new DateTime(date.Value.Year, 1, 1);
        var week = (int)Math.Ceiling((date.Value.DayOfYear + (int)firstDayOfYear.DayOfWeek) / 7.0);
        var text = date.Value.Year + "-W" + week.ToString("00");

        return new DimensionValue(text, text);
    }
}

internal sealed class DashboardDimensionNotRegisteredException : Exception
{
    public DashboardDimensionNotRegisteredException(string dimensionCode)
        : base("dimension_not_registered")
    {
        DimensionCode = dimensionCode;
    }

    public string DimensionCode { get; }
}

/// <summary>
/// Which algebra a measure obeys. A2_aggregation_algebra.md is the authority.
/// Only the families proven mergeable may be folded from day grain.
/// </summary>
internal enum DashboardAggregationFamily
{
    /// <summary>count and sum. Merges by summation, folds exactly from day grain.</summary>
    Additive,

    /// <summary>distinct count of the material identity. Does NOT fold: an entity
    /// seen on Monday and Tuesday is one entity in the week, not two.</summary>
    DistinctMaterial
}

/// <summary>
/// The generic executor. It takes an authorised relational population and a
/// declared aggregation, and executes it truthfully in PostgreSQL.
///
/// It contains no measure vocabulary, no widget vocabulary and no customer
/// vocabulary. What a measure MEANS is decided by the caller's projection into
/// WidgetFact; how a declared aggregation EXECUTES is decided here.
/// </summary>
internal static class DashboardAggregateExecutor
{
    public static async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteAsync(
        IQueryable<WidgetFact> facts,
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        DashboardAggregationFamily family,
        CancellationToken cancellationToken)
    {
        // TIME PREDICATE, RELATIONALLY. Previously this ran in memory AFTER the
        // raw cap, so a narrower window produced a MORE wrong answer: it kept
        // whichever of an arbitrary sample happened to fall inside it. It is now
        // part of the query the database plans.
        //
        // The null-timestamp rule is preserved exactly as it was: a fact with no
        // event time satisfies both bounds. Whether that is the right product
        // rule is recorded in A2 and is not decided by a truncation fix.
        if (filters?.FromUtc.HasValue == true)
        {
            var from = filters.FromUtc.Value;
            facts = facts.Where(f => !f.EventTimeUtc.HasValue || f.EventTimeUtc >= from);
        }

        if (filters?.ToUtc.HasValue == true)
        {
            var to = filters.ToUtc.Value;
            facts = facts.Where(f => !f.EventTimeUtc.HasValue || f.EventTimeUtc <= to);
        }

        var keySelector = DashboardDimensionProjection.KeySelector(resolved.DimensionCode);
        var temporal = DashboardDimensionProjection.IsTemporal(resolved.DimensionCode);

        // GROUP AND AGGREGATE IN POSTGRESQL. Only grouped rows are materialised.
        // There is no Take on the fact query, so no cap defines the population.
        List<GroupedAggregate> grouped;

        if (family == DashboardAggregationFamily.DistinctMaterial)
        {
            grouped = await facts
                .GroupBy(keySelector)
                .Select(g => new GroupedAggregate
                {
                    Key = g.Key,
                    Value = g.Select(x => x.MaterialUnitId).Distinct().Count(),
                    Rows = g.Count()
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            grouped = await facts
                .GroupBy(keySelector)
                .Select(g => new GroupedAggregate
                {
                    Key = g.Key,
                    Value = g.Sum(x => x.Value),
                    Rows = g.Count()
                })
                .ToListAsync(cancellationToken);
        }

        // DAY-GRAIN FOLD. Permitted only for a mergeable family, and only after
        // the whole authorised population has been aggregated. A distinct count
        // never reaches here.
        var folded = new Dictionary<string, DashboardAggregateRow>(StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var described = DashboardDimensionProjection.Describe(resolved.DimensionCode, group.Key);

            if (folded.TryGetValue(described.Key, out var existing))
            {
                if (family != DashboardAggregationFamily.Additive)
                {
                    // Reaching this line would mean a non-mergeable family had been
                    // grouped at a finer grain than the caller asked for, which
                    // would double count. Refuse rather than return a plausible
                    // number.
                    throw new DashboardNonMergeableFoldException(resolved.MeasureCode, resolved.DimensionCode ?? "kpi");
                }

                folded[described.Key] = existing with
                {
                    Value = existing.Value + group.Value,
                    ObservationCount = existing.ObservationCount + group.Rows
                };
            }
            else
            {
                folded[described.Key] = new DashboardAggregateRow(
                    described.Key,
                    described.Label,
                    group.Value,
                    group.Rows,
                    0);
            }
        }

        if (!temporal && folded.Count != grouped.Count)
        {
            // A non-temporal dimension must produce one group per key. If two SQL
            // groups collapsed onto one canonical key, the projection is lossy and
            // the numbers would be silently merged.
            throw new DashboardNonMergeableFoldException(resolved.MeasureCode, resolved.DimensionCode ?? "kpi");
        }

        // DETERMINISTIC ORDERING, then MaxRows over AGGREGATE GROUPS. The tie
        // break is the canonical key, never the display label: a label is a
        // presentation value, so ordering on it makes a rename reorder a chart.
        var ordered = resolved.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? folded.Values.OrderBy(x => x.Value).ThenBy(x => x.DimensionKey, StringComparer.Ordinal)
            : folded.Values.OrderByDescending(x => x.Value).ThenBy(x => x.DimensionKey, StringComparer.Ordinal);

        return ordered.Take(resolved.MaxRows).ToList();
    }

    private sealed class GroupedAggregate
    {
        public DashboardGroupKey Key { get; set; } = new();

        public decimal Value { get; set; }

        public int Rows { get; set; }
    }
}

internal sealed class DashboardNonMergeableFoldException : Exception
{
    public DashboardNonMergeableFoldException(string measureCode, string dimensionCode)
        : base("aggregation_family_not_mergeable_at_requested_grain")
    {
        MeasureCode = measureCode;
        DimensionCode = dimensionCode;
    }

    public string MeasureCode { get; }

    public string DimensionCode { get; }
}