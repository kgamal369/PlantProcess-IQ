using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Analytics.Advanced;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Services.Queries;

// ============================================================================
// T-045 PACK B - THE CLASS-1 / CLASS-2 RESULT SOURCE SEAM.
//
// Class 1, fact-shaped: WidgetFact -> DashboardAggregateExecutor -> BuildResult.
// That path is untouched by this file and remains the default.
//
// Class 2, native-rich: the source declares its own columns and rows and hands
// back the SAME DashboardWidgetQueryResultDto. It never projects into
// WidgetFact and never calls BuildResult, because flattening a readiness
// dimension or a coverage denominator into one decimal Value would destroy the
// only thing those answers carry.
//
// THE DISCRIMINATOR IS THE MEASURE CODE AND NOTHING ELSE. There is no widget
// branch, no dashboard branch, no page branch and no industry vocabulary here.
// A future native source is a new class plus two registry lines.
//
// WHY THE VALIDATOR DOES NOT NAME THESE MEASURES. DashboardWidgetValidationService
// asks the registry a general question - MeasureProvidesOwnColumns(measureCode) -
// in exactly the shape of the existing MeasureRequiresParameterCode. Comparing
// against a measure literal inside the validator would make every future native
// source a validator edit.
// ============================================================================

/// <summary>
/// A source that answers a widget query in full. Implementations either delegate
/// to the Class-1 aggregate path or produce their own columns and rows.
/// </summary>
internal interface IWidgetResultSource
{
    /// <summary>The measure code this source owns. Compared case-sensitively,
    /// as the executable-measure gate already is.</summary>
    string MeasureCode { get; }

    Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken);
}

/// <summary>
/// Shared helper for native sources. Column roles are bound BY NAME; nothing
/// downstream may depend on ordinal position.
/// </summary>
internal static class NativeWidgetResult
{
    public static DashboardWidgetQueryResultDto Build(
        DashboardWidgetResolvedDto resolved,
        IReadOnlyList<DashboardWidgetColumnDto> columns,
        IReadOnlyList<IDictionary<string, object?>> rows,
        IReadOnlyList<string> warnings)
    {
        return new DashboardWidgetQueryResultDto(
            DateTime.UtcNow,
            resolved,
            columns,
            rows,
            warnings);
    }

    public static IDictionary<string, object?> Row(params (string Key, object? Value)[] cells)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var cell in cells)
            row[cell.Key] = cell.Value;

        return row;
    }
}

// ============================================================================
// FINDING STATUS.
//
// Zero rows in correlation_results means NO SUPPORTED FINDINGS ARE CURRENTLY
// PUBLISHED. It does NOT mean no correlation exists in the plant data, and the
// two must never be conflated in a customer-visible string. An exclusion is not
// a finding, and zero findings is an acceptable truthful state that still
// renders.
// ============================================================================
internal sealed class FindingStatusWidgetResultSource : IWidgetResultSource
{
    public const string StateNoSupportedFindings = "NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED";
    public const string StatePublished = "SUPPORTED_FINDINGS_PUBLISHED";

    private readonly IPlantProcessDbContext _dbContext;

    public FindingStatusWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.FindingStatus;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = new List<DashboardWidgetColumnDto>
        {
            new("state", "State", "string"),
            new("findingCount", "Published Findings", "number"),
            new("correlationType", "Method", "string"),
            new("subjectCode", "Subject", "string"),
            new("outcomeCode", "Outcome", "string"),
            new("score", "Effect", "number"),
            new("calculatedAtUtc", "Calculated (UTC)", "string"),
            new("reason", "Reason", "string")
        };

        var published = _dbContext.CorrelationResults
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            published = published.Where(x => x.CalculatedAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            published = published.Where(x => x.CalculatedAtUtc <= to);
        }

        var total = await published.CountAsync(cancellationToken);

        if (total == 0)
        {
            var lastAnyUtc = await _dbContext.CorrelationResults
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .MaxAsync(x => (DateTime?)x.CalculatedAtUtc, cancellationToken);

            var rows = new List<IDictionary<string, object?>>
            {
                NativeWidgetResult.Row(
                    ("state", StateNoSupportedFindings),
                    ("findingCount", 0),
                    ("correlationType", null),
                    ("subjectCode", null),
                    ("outcomeCode", null),
                    ("score", null),
                    ("calculatedAtUtc", lastAnyUtc?.ToString("O")),
                    ("reason",
                        "No supported findings are currently published for the selected scope. " +
                        "This states what the published result store contains; it is not a claim " +
                        "that no relationship exists in the plant data."))
            };

            return NativeWidgetResult.Build(resolved, columns, rows, warnings);
        }

        var findings = await published
            .OrderByDescending(x => x.CalculatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(resolved.MaxRows)
            .Select(x => new
            {
                x.CorrelationType,
                x.SubjectCode,
                x.OutcomeCode,
                x.Score,
                x.CalculatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var findingRows = findings
            .Select(x => NativeWidgetResult.Row(
                ("state", StatePublished),
                ("findingCount", total),
                ("correlationType", x.CorrelationType),
                ("subjectCode", x.SubjectCode),
                ("outcomeCode", x.OutcomeCode),
                ("score", x.Score),
                ("calculatedAtUtc", x.CalculatedAtUtc.ToString("O")),
                ("reason", null)))
            .ToList();

        return NativeWidgetResult.Build(resolved, columns, findingRows, warnings);
    }
}

// ============================================================================
// SCORING COVERAGE.
//
// A ratio over a distinct denominator. It is NOT additive and it is not folded
// anywhere: a Class-2 source never enters DashboardAggregateExecutor, so no
// merge rule can be applied to it by construction.
//
// PROVENANCE IS READ, NOT ASSUMED. risk_scores carries source_system,
// source_record_id, model_version and is_synthetic. The classification below is
// derived from what those columns actually hold on the scored rows. A row count
// proves that output exists; it never proves what produced it.
//
// The denominator is named referencePopulation, never eligiblePopulation:
// nothing here resolves scoring eligibility, so claiming an eligible population
// would name a denominator the product cannot defend.
//
// TEMPORARY LEGACY PRESENTATION ADAPTER. Chapter 3's target authority is
// predictions + prediction_current, and it states that a separate independent
// risk_scores store does not exist in the target design. No future contract may
// depend permanently on this source.
// ============================================================================
internal sealed class ScoringCoverageWidgetResultSource : IWidgetResultSource
{
    public const string SourceUnknown = "SCORING_SOURCE_UNKNOWN";
    public const string SourceSynthetic = "SCORING_SOURCE_SYNTHETIC";
    public const string SourceRecorded = "SCORING_SOURCE_RECORDED";
    public const string SourceMixed = "SCORING_SOURCE_MIXED";
    public const string ModelNotReady = "MODEL_NOT_READY";
    public const string ModelVersionRecorded = "MODEL_VERSION_RECORDED";

    private readonly IPlantProcessDbContext _dbContext;

    public ScoringCoverageWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.ScoringCoverage;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = new List<DashboardWidgetColumnDto>
        {
            new("scope", "Scope", "string"),
            new("scoredPopulation", "Scored Population", "number"),
            new("referencePopulation", "Reference Population", "number"),
            new("coverageAgainstReference", "Coverage Against Reference", "number"),
            new("syntheticPopulation", "Synthetic Rows", "number"),
            new("scoringSource", "Scoring Source", "string"),
            new("modelState", "Model State", "string"),
            new("lastScoredAtUtc", "Last Scored (UTC)", "string")
        };

        var scores = _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            scores = scores.Where(x => x.ScoredAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            scores = scores.Where(x => x.ScoredAtUtc <= to);
        }

        var referencePopulation = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        var byType = await scores
            .GroupBy(x => x.RiskType)
            .Select(g => new
            {
                Scope = g.Key,
                Scored = g.Select(x => x.MaterialUnitId).Distinct().Count(),
                Synthetic = g.Count(x => x.IsSynthetic),
                WithSourceSystem = g.Count(x => x.SourceSystem != null),
                WithModelVersion = g.Count(x => x.ModelVersion != null),
                Rows = g.Count(),
                LastScoredAtUtc = g.Max(x => x.ScoredAtUtc)
            })
            .ToListAsync(cancellationToken);

        if (byType.Count == 0)
        {
            var emptyRows = new List<IDictionary<string, object?>>
            {
                NativeWidgetResult.Row(
                    ("scope", "ALL"),
                    ("scoredPopulation", 0),
                    ("referencePopulation", referencePopulation),
                    ("coverageAgainstReference", 0m),
                    ("syntheticPopulation", 0),
                    ("scoringSource", SourceUnknown),
                    ("modelState", ModelNotReady),
                    ("lastScoredAtUtc", null))
            };

            return NativeWidgetResult.Build(resolved, columns, emptyRows, warnings);
        }

        var rows = byType
            .OrderByDescending(x => x.Scored)
            .ThenBy(x => x.Scope, StringComparer.Ordinal)
            .Take(resolved.MaxRows)
            .Select(x => NativeWidgetResult.Row(
                ("scope", x.Scope),
                ("scoredPopulation", x.Scored),
                ("referencePopulation", referencePopulation),
                ("coverageAgainstReference", referencePopulation == 0
                    ? 0m
                    : Math.Round((decimal)x.Scored / referencePopulation, 6)),
                ("syntheticPopulation", x.Synthetic),
                ("scoringSource", ClassifySource(x.Rows, x.Synthetic, x.WithSourceSystem)),
                ("modelState", ModelStateOf(x.Rows, x.Synthetic, x.WithModelVersion)),
                ("lastScoredAtUtc", x.LastScoredAtUtc.ToString("O"))))
            .ToList();

        return NativeWidgetResult.Build(resolved, columns, rows, warnings);
    }

    /// <summary>
    /// Derived from the rows, never asserted. Unknown is the honest answer when
    /// the scoring run recorded neither a synthetic flag nor a source system:
    /// the columns exist, so this states that nothing was written into them.
    /// </summary>
    /// <summary>
    /// MEASURED 12-Aug: every risk row in the presentation database carries a
    /// model_version AND is_synthetic = true. Reading the version alone reported
    /// MODEL_VERSION_RECORDED over a population that is entirely fabricated,
    /// which is the same error as inferring provenance from a row count: an
    /// attribute is present, so a claim was made about what produced the data.
    ///
    /// A version string on synthetic rows records how the FIXTURE was generated.
    /// It is not evidence that a model is ready, so it can never raise the model
    /// state above not-ready.
    /// </summary>
    private static string ModelStateOf(int rows, int synthetic, int withModelVersion)
    {
        if (rows == 0) return ModelNotReady;
        if (synthetic == rows) return ModelNotReady;
        if (withModelVersion > 0) return ModelVersionRecorded;
        return ModelNotReady;
    }

    private static string ClassifySource(int rows, int synthetic, int withSourceSystem)
    {
        if (rows == 0)
            return SourceUnknown;

        if (synthetic == rows)
            return SourceSynthetic;

        if (withSourceSystem == rows && synthetic == 0)
            return SourceRecorded;

        if (synthetic > 0 || withSourceSystem > 0)
            return SourceMixed;

        return SourceUnknown;
    }
}

// ============================================================================
// ANALYSIS READINESS - DF8.
//
// Bound to the CANONICAL authority: IAnalysisReadinessService ->
// AdvancedReadiness.Evaluate -> ReadinessGate. Five governed dimensions, and
// the overall state is the WORST dimension, never an average - that is
// ReadinessGate's own Worst() fold, not a rule reimplemented here. The Single
// Engine Implementation Law is why this source computes nothing itself.
//
// IMlReadinessService is deliberately NOT used. It returns a percentage-score
// legacy ML-foundation diagnostic and is a different question.
//
// PARAMETERISATION, AND THE ONE THING THAT IS NOT IN THE FROZEN SPEC.
// AdvancedAnalysisRequest needs OutcomeKey, Grain and WindowDays. The widget
// definition carries an outcome key on the existing ParameterCode field, which
// is why analysisReadiness is registered as requiring a parameter code. It
// carries no grain, and grain is NOT NULL on ml_outcome_definitions - it is a
// property of the outcome, not of the widget. So the grain is looked up from
// the outcome definition through IAnalysisOutcomeTargetResolver. Hardcoding a
// grain would have put plant vocabulary in engine code.
// ============================================================================
internal sealed class AnalysisReadinessWidgetResultSource : IWidgetResultSource
{
    public const string StateTargetNotResolved = "READINESS_TARGET_NOT_RESOLVED";

    private readonly IAnalysisReadinessService _readinessService;
    private readonly IAnalysisOutcomeTargetResolver _targetResolver;

    public AnalysisReadinessWidgetResultSource(
        IAnalysisReadinessService readinessService,
        IAnalysisOutcomeTargetResolver targetResolver)
    {
        _readinessService = readinessService;
        _targetResolver = targetResolver;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.AnalysisReadiness;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = new List<DashboardWidgetColumnDto>
        {
            new("dimension", "Readiness Dimension", "string"),
            new("state", "State", "string"),
            new("reason", "Reason", "string"),
            new("overall", "Overall", "string"),
            new("canRun", "Can Run", "string"),
            new("outcomeKey", "Outcome", "string"),
            new("grain", "Grain", "string"),
            new("windowDays", "Window (days)", "number"),
            new("independentUnits", "Independent Units", "number"),
            new("outcomeEvents", "Outcome Events", "number"),
            new("evaluatedAtUtc", "Evaluated (UTC)", "string")
        };

        var outcomeKey = resolved.ParameterCode;
        var target = string.IsNullOrWhiteSpace(outcomeKey)
            ? null
            : await _targetResolver.ResolveAsync(outcomeKey!, cancellationToken);

        if (target is null)
        {
            var unresolved = new List<IDictionary<string, object?>>
            {
                NativeWidgetResult.Row(
                    ("dimension", "Analysis target"),
                    ("state", StateTargetNotResolved),
                    ("reason", string.IsNullOrWhiteSpace(outcomeKey)
                        ? "This widget has no analysis outcome selected, so readiness has nothing to evaluate."
                        : "No active outcome definition matches the selected analysis target, so its grain " +
                          "cannot be resolved and readiness was not evaluated. No readiness state is inferred."),
                    ("overall", StateTargetNotResolved),
                    ("canRun", bool.FalseString),
                    ("outcomeKey", outcomeKey),
                    ("grain", null),
                    ("windowDays", null),
                    ("independentUnits", null),
                    ("outcomeEvents", null),
                    ("evaluatedAtUtc", DateTime.UtcNow.ToString("O")))
            };

            return NativeWidgetResult.Build(resolved, columns, unresolved, warnings);
        }

        var windowDays = ResolveWindowDays(resolved);

        var report = await _readinessService.EvaluateAsync(
            new AdvancedAnalysisRequest(
                target.OutcomeKey,
                target.Grain,
                windowDays,
                AdvancedDefaults.DemoTenant),
            cancellationToken);

        var evaluatedAtUtc = DateTime.UtcNow.ToString("O");

        var rows = report.Dimensions
            .Select(d => NativeWidgetResult.Row(
                ("dimension", d.Name),
                ("state", d.State),
                ("reason", d.Reason),
                ("overall", report.Overall),
                ("canRun", report.CanRun ? bool.TrueString : bool.FalseString),
                ("outcomeKey", report.OutcomeKey),
                ("grain", report.Grain),
                ("windowDays", report.WindowDays),
                ("independentUnits", report.IndependentHeats),
                ("outcomeEvents", report.OutcomeEvents),
                ("evaluatedAtUtc", evaluatedAtUtc)))
            .Take(resolved.MaxRows)
            .ToList();

        return NativeWidgetResult.Build(resolved, columns, rows, warnings);
    }

    /// <summary>
    /// The requested window where one was given, the governed default where it
    /// was not. Derived, never a literal picked to make a number look better.
    /// </summary>
    private static int ResolveWindowDays(DashboardWidgetResolvedDto resolved)
    {
        if (resolved.FromUtc.HasValue && resolved.ToUtc.HasValue)
        {
            var span = (int)Math.Ceiling((resolved.ToUtc.Value - resolved.FromUtc.Value).TotalDays);
            if (span > 0)
                return Math.Min(span, Widgets.DashboardWidgetQuerySafetyRegistry.AbsoluteLookbackDays);
        }

        return Widgets.DashboardWidgetQuerySafetyRegistry.DefaultLookbackDays;
    }
}

// ============================================================================
// T-047 PACK A - DISTRIBUTION SOURCES.
//
// A NATIVE SOURCE IS A SEMANTIC ANALYTICAL QUESTION. It is not a chart type.
// Both classes below publish the same renderer-facing roles because a
// histogram is a visual capability that either question can be drawn with -
// not because they are "the histogram source". They remain different
// populations with different refusals, and either could later be drawn as a
// box plot without changing a line here.
//
// COLUMN ROLES, BOUND BY NAME:
//   state      terminal state of the answer
//   binLabel   human-readable interval
//   binLower   inclusive lower edge
//   binUpper   exclusive upper edge, inclusive on the last bin
//   count      observations in the interval
//
// WHY THE STATE COLUMN. A distribution that cannot be computed is not an empty
// chart. Zero observations under a filter, an unnamed parameter and a
// single-valued population are three different answers, and a blank panel says
// none of them. This mirrors the finding-status contract.
// ============================================================================

internal static class DistributionBinning
{
    /// <summary>
    /// Bin count from population size, clamped. Square-root choice, which is
    /// stable and explainable; the clamp keeps a small population from
    /// producing one bar and a large one from producing a comb.
    /// </summary>
    public const int MinimumBins = 2;
    public const int MaximumBins = 12;

    public const string StatePublished = "DISTRIBUTION_PUBLISHED";
    public const string StateParameterNotSelected = "PARAMETER_NOT_SELECTED";
    public const string StateNoObservations = "NO_OBSERVATIONS_IN_SELECTION";
    public const string StateSingleValue = "SINGLE_VALUE_POPULATION";

    public static int SuggestBins(int populationCount)
    {
        if (populationCount <= 0)
            return MinimumBins;

        var suggested = (int)Math.Round(Math.Sqrt(populationCount), MidpointRounding.AwayFromZero);
        return Math.Clamp(suggested, MinimumBins, MaximumBins);
    }

    public static IReadOnlyList<DashboardWidgetColumnDto> Columns()
    {
        return new List<DashboardWidgetColumnDto>
        {
            new("state", "State", "string"),
            new("binLabel", "Interval", "string"),
            new("binLower", "From", "number"),
            new("binUpper", "To", "number"),
            new("count", "Observations", "number")
        };
    }

    /// <summary>
    /// A terminal state that is not a distribution. One row, no bins, and the
    /// state says which answer this is.
    /// </summary>
    public static IReadOnlyList<IDictionary<string, object?>> StateOnly(string state)
    {
        return new List<IDictionary<string, object?>>
        {
            NativeWidgetResult.Row(
                ("state", state),
                ("binLabel", null),
                ("binLower", null),
                ("binUpper", null),
                ("count", 0))
        };
    }

    /// <summary>
    /// Turns SQL-side bin counts into contiguous rows. Every interval between
    /// the minimum and the maximum appears, including empty ones: a gap in a
    /// distribution is a reading, and dropping empty intervals would compress
    /// the axis and misstate the spread.
    /// </summary>
    public static IReadOnlyList<IDictionary<string, object?>> BuildRows(
        decimal minimum,
        decimal maximum,
        int binCount,
        IReadOnlyDictionary<int, int> countsByBin)
    {
        var width = (maximum - minimum) / binCount;
        var rows = new List<IDictionary<string, object?>>(binCount);

        for (var index = 0; index < binCount; index++)
        {
            var lower = minimum + (width * index);
            var upper = index == binCount - 1 ? maximum : minimum + (width * (index + 1));

            countsByBin.TryGetValue(index, out var count);

            rows.Add(NativeWidgetResult.Row(
                ("state", StatePublished),
                ("binLabel", Format(lower) + " to " + Format(upper)),
                ("binLower", lower),
                ("binUpper", upper),
                ("count", count)));
        }

        return rows;
    }

    private static string Format(decimal value)
    {
        return Math.Round(value, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The last bin is closed on the right, so the maximum value itself lands
    /// one index past the end. Clamping here rather than in SQL keeps the
    /// grouping expression translatable.
    /// </summary>
    public static int ClampIndex(int rawIndex, int binCount)
    {
        if (rawIndex < 0) return 0;
        if (rawIndex > binCount - 1) return binCount - 1;
        return rawIndex;
    }
}

/// <summary>
/// THE QUESTION: how often does this parameter's observed reading fall in each
/// interval, across the selected population?
/// </summary>
internal sealed class ParameterValueDistributionWidgetResultSource : IWidgetResultSource
{
    private readonly IPlantProcessDbContext _dbContext;

    public ParameterValueDistributionWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.ParameterValueDistribution;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = DistributionBinning.Columns();
        var parameterCode = resolved.ParameterCode ?? query.Filters?.ParameterCode;

        if (string.IsNullOrWhiteSpace(parameterCode))
        {
            return NativeWidgetResult.Build(
                resolved,
                columns,
                DistributionBinning.StateOnly(DistributionBinning.StateParameterNotSelected),
                warnings);
        }

        var observations =
            from observation in _dbContext.ParameterObservations.AsNoTracking()
            join material in _dbContext.MaterialUnits.AsNoTracking()
                on observation.MaterialUnitId equals material.Id
            join parameter in _dbContext.ParameterDefinitions.AsNoTracking()
                on observation.ParameterDefinitionId equals parameter.Id
            where
                !observation.IsDeleted &&
                !material.IsDeleted &&
                observation.NumericValue != null &&
                parameter.ParameterCode == parameterCode
            select new { observation.NumericValue, observation.ObservedAtUtc, material.SiteId, observation.EquipmentId };

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            observations = observations.Where(x => x.ObservedAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            observations = observations.Where(x => x.ObservedAtUtc <= to);
        }

        var siteFilter = query.Filters?.SiteId;
        if (siteFilter.HasValue)
        {
            var site = siteFilter.Value;
            observations = observations.Where(x => x.SiteId == site);
        }

        var equipmentFilter = query.Filters?.EquipmentId;
        if (equipmentFilter.HasValue)
        {
            var equipment = equipmentFilter.Value;
            observations = observations.Where(x => x.EquipmentId == equipment);
        }

        var values = observations.Select(x => x.NumericValue!.Value);
        return await BuildDistributionAsync(resolved, columns, values, warnings, cancellationToken);
    }

    /// <summary>
    /// Shared by both distribution sources. It knows nothing about which
    /// population it was handed, which is why the same routine can serve two
    /// different analytical questions without either becoming the other.
    /// </summary>
    internal static async Task<DashboardWidgetQueryResultDto> BuildDistributionAsync(
        DashboardWidgetResolvedDto resolved,
        IReadOnlyList<DashboardWidgetColumnDto> columns,
        IQueryable<decimal> values,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        // One round trip for the extent. Nothing is materialised: a population
        // large enough to matter must not be pulled into memory to be counted.
        var extent = await values
            .GroupBy(x => 1)
            .Select(g => new { Minimum = g.Min(), Maximum = g.Max(), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        if (extent is null || extent.Count == 0)
        {
            return NativeWidgetResult.Build(
                resolved,
                columns,
                DistributionBinning.StateOnly(DistributionBinning.StateNoObservations),
                warnings);
        }

        if (extent.Minimum == extent.Maximum)
        {
            // One bin at one hundred percent is the distribution equivalent of
            // the single-slice share chart, and it is a statement about the
            // population rather than a spread.
            return NativeWidgetResult.Build(
                resolved,
                columns,
                DistributionBinning.StateOnly(DistributionBinning.StateSingleValue),
                warnings);
        }

        var binCount = DistributionBinning.SuggestBins(extent.Count);
        var minimum = extent.Minimum;
        var width = (extent.Maximum - minimum) / binCount;

        // The bin index is produced by floor() in PostgreSQL and stays decimal
        // all the way back. An (int) cast here would be the one expression in
        // this file Npgsql might refuse to translate, and it would fail only at
        // runtime - the build cannot see it. floor() is translated by every
        // provider, and narrowing the handful of returned keys in memory costs
        // nothing.
        var grouped = await values
            .GroupBy(x => Math.Floor((x - minimum) / width))
            .Select(g => new { Index = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countsByBin = new Dictionary<int, int>();
        foreach (var group in grouped)
        {
            var index = DistributionBinning.ClampIndex((int)group.Index, binCount);
            countsByBin.TryGetValue(index, out var running);
            countsByBin[index] = running + group.Count;
        }

        return NativeWidgetResult.Build(
            resolved,
            columns,
            DistributionBinning.BuildRows(minimum, extent.Maximum, binCount, countsByBin),
            warnings);
    }
}

/// <summary>
/// THE QUESTION: how are risk scores spread across the scored population?
///
/// A different population and a different refusal surface from the parameter
/// distribution. It needs no parameter, and an unscored plant is a truthful
/// no-observations answer rather than a missing selection.
/// </summary>
internal sealed class RiskScoreDistributionWidgetResultSource : IWidgetResultSource
{
    private readonly IPlantProcessDbContext _dbContext;

    public RiskScoreDistributionWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.RiskScoreDistribution;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = DistributionBinning.Columns();

        var scores = _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            scores = scores.Where(x => x.ScoredAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            scores = scores.Where(x => x.ScoredAtUtc <= to);
        }

        var riskClass = query.Filters?.RiskClass;
        if (!string.IsNullOrWhiteSpace(riskClass))
        {
            scores = scores.Where(x => x.RiskClass == riskClass);
        }

        return await ParameterValueDistributionWidgetResultSource.BuildDistributionAsync(
            resolved,
            columns,
            scores.Select(x => x.Score),
            warnings,
            cancellationToken);
    }
}

// ============================================================================
// T-047 PACK B - THE SPREAD SOURCE AND ITS QUARTILE KERNEL.
// ============================================================================

/// <summary>
/// THE QUARTILE METHOD IS PINNED AND SAID OUT LOUD.
///
/// There are at least nine defensible definitions of a quartile, and they
/// disagree on small samples. Leaving the choice implicit would mean a customer
/// comparing our median against their spreadsheet's could find a difference
/// nobody could explain.
///
/// This is the linear-interpolation-between-closest-ranks method, R-7, which is
/// what Excel PERCENTILE.INC and NumPy's default produce. Public so it can be
/// tested directly against known answers rather than inferred from a chart.
/// </summary>
public static class DistributionQuartiles
{
    /// <summary>
    /// A box plot drawn from a handful of points asserts a shape the data
    /// cannot support. Groups below this are reported, not silently dropped:
    /// removing them would overstate the coverage of the groups that remain.
    /// </summary>
    public const int MinimumObservationsPerGroup = 5;

    public static decimal Percentile(IReadOnlyList<decimal> sortedAscending, decimal p)
    {
        if (sortedAscending.Count == 0)
            throw new ArgumentException("A percentile of an empty population is undefined.", nameof(sortedAscending));

        if (sortedAscending.Count == 1)
            return sortedAscending[0];

        var h = (sortedAscending.Count - 1) * p;
        var lower = (int)Math.Floor(h);
        var fraction = h - lower;

        if (lower >= sortedAscending.Count - 1)
            return sortedAscending[sortedAscending.Count - 1];

        return sortedAscending[lower] + (fraction * (sortedAscending[lower + 1] - sortedAscending[lower]));
    }

    public static (decimal Minimum, decimal Q1, decimal Median, decimal Q3, decimal Maximum) Summarise(
        IReadOnlyList<decimal> values)
    {
        var sorted = values.OrderBy(x => x).ToList();

        return (
            sorted[0],
            Percentile(sorted, 0.25m),
            Percentile(sorted, 0.50m),
            Percentile(sorted, 0.75m),
            sorted[sorted.Count - 1]);
    }
}

/// <summary>
/// THE QUESTION: how does this parameter's value spread differ between the
/// groups of a chosen dimension?
///
/// WHY THE VALUES ARE SUMMARISED IN MEMORY. Quartiles need percentile_cont,
/// which EF Core cannot translate. The alternatives were raw SQL inside a
/// source that otherwise speaks LINQ, or a bounded fetch with an honest
/// refusal when the population exceeds the safe limit. The second matches
/// RequireCompletePopulation, which the fact-shaped path already uses for
/// exactly this situation, so a truncated population REFUSES rather than
/// quietly reporting the quartiles of whichever rows arrived first.
/// </summary>
internal sealed class ParameterValueSpreadWidgetResultSource : IWidgetResultSource
{
    public const string StatePublished = "SPREAD_PUBLISHED";
    public const string StateParameterNotSelected = "PARAMETER_NOT_SELECTED";
    public const string StateGroupingNotSelected = "GROUPING_NOT_SELECTED";
    public const string StateNoObservations = "NO_OBSERVATIONS_IN_SELECTION";
    public const string StatePopulationTooLarge = "POPULATION_EXCEEDS_SAFE_LIMIT";
    public const string StateInsufficientObservations = "INSUFFICIENT_OBSERVATIONS";

    private readonly IPlantProcessDbContext _dbContext;

    public ParameterValueSpreadWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.ParameterValueSpread;

    private static IReadOnlyList<DashboardWidgetColumnDto> Columns()
    {
        return new List<DashboardWidgetColumnDto>
        {
            new("state", "State", "string"),
            new("category", "Group", "string"),
            new("label", "Group Label", "string"),
            new("minimum", "Minimum", "number"),
            new("q1", "Lower Quartile", "number"),
            new("median", "Median", "number"),
            new("q3", "Upper Quartile", "number"),
            new("maximum", "Maximum", "number"),
            new("observationCount", "Observations", "number")
        };
    }

    private static IReadOnlyList<IDictionary<string, object?>> StateOnly(string state)
    {
        return new List<IDictionary<string, object?>>
        {
            NativeWidgetResult.Row(
                ("state", state), ("category", null), ("label", null),
                ("minimum", null), ("q1", null), ("median", null),
                ("q3", null), ("maximum", null), ("observationCount", 0))
        };
    }

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = Columns();
        var parameterCode = resolved.ParameterCode ?? query.Filters?.ParameterCode;

        if (string.IsNullOrWhiteSpace(parameterCode))
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateParameterNotSelected), warnings);

        // A native measure is exempt from the validator's dimension check, so
        // the absence of a grouping is this source's own refusal to make.
        if (string.IsNullOrWhiteSpace(resolved.DimensionCode))
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateGroupingNotSelected), warnings);

        var grouping = resolved.DimensionCode;

        var observations =
            from observation in _dbContext.ParameterObservations.AsNoTracking()
            join material in _dbContext.MaterialUnits.AsNoTracking()
                on observation.MaterialUnitId equals material.Id
            join parameter in _dbContext.ParameterDefinitions.AsNoTracking()
                on observation.ParameterDefinitionId equals parameter.Id
            where
                !observation.IsDeleted &&
                !material.IsDeleted &&
                observation.NumericValue != null &&
                parameter.ParameterCode == parameterCode
            select new
            {
                Value = observation.NumericValue!.Value,
                observation.ObservedAtUtc,
                material.GradeOrRecipe,
                material.ProductFamily,
                material.MaterialUnitType,
                material.SourceSystem
            };

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            observations = observations.Where(x => x.ObservedAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            observations = observations.Where(x => x.ObservedAtUtc <= to);
        }

        // Bounded, with one extra row so an over-limit population is DETECTED
        // rather than silently truncated.
        var limit = resolved.RawRowLimit;
        var fetched = await observations.Take(limit + 1).ToListAsync(cancellationToken);

        if (fetched.Count > limit)
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StatePopulationTooLarge), warnings);

        if (fetched.Count == 0)
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateNoObservations), warnings);

        // The grouping is resolved from the dimension the widget declares. No
        // page, widget or industry term reaches this switch.
        var grouped = fetched
            .GroupBy(x => grouping switch
            {
                DashboardMetadataCodes.Dimensions.GradeOrRecipe => x.GradeOrRecipe,
                DashboardMetadataCodes.Dimensions.ProductFamily => x.ProductFamily,
                DashboardMetadataCodes.Dimensions.MaterialUnitType => x.MaterialUnitType,
                DashboardMetadataCodes.Dimensions.SourceSystem => x.SourceSystem,
                _ => null
            })
            .Where(g => g.Key != null)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        if (grouped.Count == 0)
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateGroupingNotSelected), warnings);

        var rows = new List<IDictionary<string, object?>>(grouped.Count);

        foreach (var group in grouped)
        {
            var values = group.Select(x => x.Value).ToList();

            if (values.Count < DistributionQuartiles.MinimumObservationsPerGroup)
            {
                // Reported, not dropped. A missing group would misstate how
                // much of the population the chart actually covers.
                rows.Add(NativeWidgetResult.Row(
                    ("state", StateInsufficientObservations),
                    ("category", group.Key), ("label", group.Key),
                    ("minimum", null), ("q1", null), ("median", null),
                    ("q3", null), ("maximum", null), ("observationCount", values.Count)));
                continue;
            }

            var summary = DistributionQuartiles.Summarise(values);

            rows.Add(NativeWidgetResult.Row(
                ("state", StatePublished),
                ("category", group.Key), ("label", group.Key),
                ("minimum", summary.Minimum), ("q1", summary.Q1), ("median", summary.Median),
                ("q3", summary.Q3), ("maximum", summary.Maximum),
                ("observationCount", values.Count)));
        }

        return NativeWidgetResult.Build(resolved, columns, rows, warnings);
    }
}

// ============================================================================
// T-047 PACK C2 - THE PARAMETER RELATIONSHIP SOURCE.
//
// THE QUESTION: over the materials where BOTH parameters were measured, how
// does one parameter's value relate to the other's?
//
// WHERE THE SECOND PARAMETER TRAVELS, AND WHY. No DTO carries a free-form
// configuration slot, and adding a persisted column would be a migration. The
// widget's own parameter_code names X, and the filter envelope's parameterCode
// names Y. That is an OVERLOAD of a field whose meaning elsewhere is "narrow
// to this parameter", and it is confined to this one measure: native sources
// dispatch on measure code alone, so no other measure sees this reading.
//
// PAIRING IS BY MATERIAL IDENTITY AND NOTHING ELSE. One aggregated value per
// material per parameter, inner-joined on the material. Pairing by row order
// or by nearest timestamp would manufacture a relationship out of collection
// order, which is the most convincing lie a scatter can tell.
// ============================================================================
internal sealed class ParameterRelationshipWidgetResultSource : IWidgetResultSource
{
    public const string StatePublished = "RELATIONSHIP_PUBLISHED";
    public const string StateParameterNotSelected = "PARAMETER_NOT_SELECTED";
    public const string StateSecondParameterNotSelected = "SECOND_PARAMETER_NOT_SELECTED";
    public const string StateSameParameterSelected = "SAME_PARAMETER_SELECTED";
    public const string StateNoOverlap = "NO_OVERLAPPING_MATERIALS";
    public const string StatePopulationTooLarge = "POPULATION_EXCEEDS_SAFE_LIMIT";

    /// <summary>
    /// Below this the cloud is not a relationship, it is a handful of points a
    /// reader will draw a line through anyway.
    /// </summary>
    public const int MinimumPairedMaterials = 5;

    private readonly IPlantProcessDbContext _dbContext;

    public ParameterRelationshipWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.ParameterRelationship;

    private static IReadOnlyList<DashboardWidgetColumnDto> Columns()
    {
        return new List<DashboardWidgetColumnDto>
        {
            new("state", "State", "string"),
            new("materialUnitId", "Material Unit", "string"),
            new("materialLabel", "Material", "string"),
            new("xValue", "X Value", "number"),
            new("yValue", "Y Value", "number"),
            new("xParameterCode", "X Parameter", "string"),
            new("yParameterCode", "Y Parameter", "string")
        };
    }

    private static IReadOnlyList<IDictionary<string, object?>> StateOnly(
        string state, string? x, string? y)
    {
        return new List<IDictionary<string, object?>>
        {
            NativeWidgetResult.Row(
                ("state", state), ("materialUnitId", null), ("materialLabel", null),
                ("xValue", null), ("yValue", null),
                ("xParameterCode", x), ("yParameterCode", y))
        };
    }

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = Columns();

        var xParameter = resolved.ParameterCode;
        var yParameter = query.Filters?.ParameterCode;

        if (string.IsNullOrWhiteSpace(xParameter))
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateParameterNotSelected, null, null), warnings);

        if (string.IsNullOrWhiteSpace(yParameter))
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateSecondParameterNotSelected, xParameter, null), warnings);

        if (string.Equals(xParameter, yParameter, StringComparison.Ordinal))
        {
            // A parameter scattered against itself is a straight diagonal. It
            // looks like a perfect correlation and means nothing.
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateSameParameterSelected, xParameter, yParameter), warnings);
        }

        var xs = await AverageByMaterialAsync(resolved, xParameter, cancellationToken);
        if (xs is null)
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StatePopulationTooLarge, xParameter, yParameter), warnings);

        var ys = await AverageByMaterialAsync(resolved, yParameter, cancellationToken);
        if (ys is null)
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StatePopulationTooLarge, xParameter, yParameter), warnings);

        var labels = await _dbContext.MaterialUnits.AsNoTracking()
            .Where(m => !m.IsDeleted && xs.Keys.Contains(m.Id))
            .Select(m => new { m.Id, m.MaterialCode })
            .ToListAsync(cancellationToken);

        var labelById = labels.ToDictionary(x => x.Id, x => x.MaterialCode);

        // The inner join IS the overlap rule. A material measured for only one
        // of the two parameters contributes no point, and is not filled in.
        var rows = new List<IDictionary<string, object?>>();

        foreach (var entry in xs.OrderBy(x => x.Key))
        {
            if (!ys.TryGetValue(entry.Key, out var yValue))
                continue;

            labelById.TryGetValue(entry.Key, out var label);

            rows.Add(NativeWidgetResult.Row(
                ("state", StatePublished),
                ("materialUnitId", entry.Key.ToString()),
                ("materialLabel", label ?? entry.Key.ToString()),
                ("xValue", entry.Value),
                ("yValue", yValue),
                ("xParameterCode", xParameter),
                ("yParameterCode", yParameter)));
        }

        if (rows.Count < MinimumPairedMaterials)
            return NativeWidgetResult.Build(resolved, columns, StateOnly(StateNoOverlap, xParameter, yParameter), warnings);

        return NativeWidgetResult.Build(resolved, columns, rows, warnings);
    }

    /// <summary>
    /// One value per material, aggregated in PostgreSQL. Returns null when the
    /// population exceeds the safe limit, so the caller refuses rather than
    /// scattering whichever materials happened to arrive first.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>?> AverageByMaterialAsync(
        DashboardWidgetResolvedDto resolved,
        string parameterCode,
        CancellationToken cancellationToken)
    {
        var observations =
            from observation in _dbContext.ParameterObservations.AsNoTracking()
            join material in _dbContext.MaterialUnits.AsNoTracking()
                on observation.MaterialUnitId equals material.Id
            join parameter in _dbContext.ParameterDefinitions.AsNoTracking()
                on observation.ParameterDefinitionId equals parameter.Id
            where
                !observation.IsDeleted &&
                !material.IsDeleted &&
                observation.NumericValue != null &&
                parameter.ParameterCode == parameterCode
            select new { observation.MaterialUnitId, Value = observation.NumericValue!.Value, observation.ObservedAtUtc };

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            observations = observations.Where(x => x.ObservedAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            observations = observations.Where(x => x.ObservedAtUtc <= to);
        }

        var aggregated = await observations
            .GroupBy(x => x.MaterialUnitId)
            .Select(g => new { MaterialUnitId = g.Key, Value = g.Average(x => x.Value) })
            .Take(resolved.RawRowLimit + 1)
            .ToListAsync(cancellationToken);

        if (aggregated.Count > resolved.RawRowLimit)
            return null;

        return aggregated.ToDictionary(x => x.MaterialUnitId, x => Math.Round(x.Value, 4));
    }
}

// ============================================================================
// T-047 PACK D - THE MULTI-SERIES SOURCES.
//
// Two different analytical questions that happen to publish the same shape.
// The shared roles are category / categoryLabel / series / seriesLabel / value,
// and a stacked renderer is one way to draw them. Neither class knows that.
//
// A SINGLE SERIES IS REFUSED. A stack of one is a bar wearing a legend, and it
// invites a reader to see a composition that does not exist - the same defect
// the single-slice share chart was refused for.
// ============================================================================

internal static class SeriesResult
{
    public const string StatePublished = "SERIES_PUBLISHED";
    public const string StateNoObservations = "NO_OBSERVATIONS_IN_SELECTION";
    public const string StateSingleSeries = "SINGLE_SERIES_POPULATION";
    public const string StateGroupingNotSelected = "GROUPING_NOT_SELECTED";
    public const string StatePopulationTooLarge = "POPULATION_EXCEEDS_SAFE_LIMIT";

    public static IReadOnlyList<DashboardWidgetColumnDto> Columns()
    {
        return new List<DashboardWidgetColumnDto>
        {
            new("state", "State", "string"),
            new("category", "Category", "string"),
            new("categoryLabel", "Category Label", "string"),
            new("series", "Series", "string"),
            new("seriesLabel", "Series Label", "string"),
            new("value", "Value", "number")
        };
    }

    public static IReadOnlyList<IDictionary<string, object?>> StateOnly(string state)
    {
        return new List<IDictionary<string, object?>>
        {
            NativeWidgetResult.Row(
                ("state", state), ("category", null), ("categoryLabel", null),
                ("series", null), ("seriesLabel", null), ("value", 0))
        };
    }

    public static IDictionary<string, object?> Cell(
        string category, string categoryLabel, string series, string seriesLabel, decimal value)
    {
        return NativeWidgetResult.Row(
            ("state", StatePublished),
            ("category", category), ("categoryLabel", categoryLabel),
            ("series", series), ("seriesLabel", seriesLabel),
            ("value", value));
    }
}

/// <summary>
/// THE QUESTION: how does production volume split between shifts over time?
///
/// The shift lives on the process step, not on the material, so a material
/// worked by two crews contributes to both. Counting DISTINCT materials per
/// (day, crew) is what keeps that honest: counting steps would report activity
/// and call it throughput.
/// </summary>
internal sealed class MaterialThroughputByShiftWidgetResultSource : IWidgetResultSource
{
    private readonly IPlantProcessDbContext _dbContext;

    public MaterialThroughputByShiftWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.MaterialThroughputByShift;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = SeriesResult.Columns();

        var steps =
            from step in _dbContext.ProcessStepExecutions.AsNoTracking()
            join material in _dbContext.MaterialUnits.AsNoTracking()
                on step.MaterialUnitId equals material.Id
            where !step.IsDeleted && !material.IsDeleted && step.CrewCode != null
            select new { step.MaterialUnitId, step.CrewCode, step.StartedAtUtc, material.SiteId };

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            steps = steps.Where(x => x.StartedAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            steps = steps.Where(x => x.StartedAtUtc <= to);
        }

        var siteFilter = query.Filters?.SiteId;
        if (siteFilter.HasValue)
        {
            var site = siteFilter.Value;
            steps = steps.Where(x => x.SiteId == site);
        }

        var aggregated = await steps
            .GroupBy(x => new { Day = x.StartedAtUtc.Date, x.CrewCode })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.CrewCode,
                Materials = g.Select(x => x.MaterialUnitId).Distinct().Count()
            })
            .Take(resolved.RawRowLimit + 1)
            .ToListAsync(cancellationToken);

        if (aggregated.Count > resolved.RawRowLimit)
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StatePopulationTooLarge), warnings);

        if (aggregated.Count == 0)
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StateNoObservations), warnings);

        if (aggregated.Select(x => x.CrewCode).Distinct().Count() < 2)
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StateSingleSeries), warnings);

        var rows = aggregated
            .OrderBy(x => x.Day).ThenBy(x => x.CrewCode, StringComparer.Ordinal)
            .Select(x => SeriesResult.Cell(
                x.Day.ToString("yyyy-MM-dd"),
                x.Day.ToString("yyyy-MM-dd"),
                x.CrewCode!,
                x.CrewCode!,
                x.Materials))
            .ToList();

        return NativeWidgetResult.Build(resolved, columns, rows, warnings);
    }
}

/// <summary>
/// THE QUESTION: how do defect types distribute across grades?
///
/// A grade with many defects of one type and a grade with the same total spread
/// across five are different problems, and a single bar per grade reports them
/// identically. That difference is the whole reason this source exists.
/// </summary>
internal sealed class DefectTypeMixWidgetResultSource : IWidgetResultSource
{
    private readonly IPlantProcessDbContext _dbContext;

    public DefectTypeMixWidgetResultSource(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MeasureCode => DashboardMetadataCodes.Measures.DefectTypeMix;

    public async Task<DashboardWidgetQueryResultDto> ExecuteAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetQueryDto query,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var columns = SeriesResult.Columns();

        // The grouping is read from the dimension the widget declares. Naming
        // one here would tie the seam to a customer's vocabulary, which is
        // exactly what the Class-2 genericity guard forbids.
        if (string.IsNullOrWhiteSpace(resolved.DimensionCode))
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StateGroupingNotSelected), warnings);

        var grouping = resolved.DimensionCode;

        var events =
            from qualityEvent in _dbContext.QualityEvents.AsNoTracking()
            join material in _dbContext.MaterialUnits.AsNoTracking()
                on qualityEvent.MaterialUnitId equals material.Id
            join defect in _dbContext.DefectCatalogs.AsNoTracking()
                on qualityEvent.DefectCatalogId equals defect.Id into defectJoin
            from defect in defectJoin.DefaultIfEmpty()
            where !qualityEvent.IsDeleted && !material.IsDeleted
            select new
            {
                GroupKey =
                    grouping == DashboardMetadataCodes.Dimensions.GradeOrRecipe ? material.GradeOrRecipe :
                    grouping == DashboardMetadataCodes.Dimensions.ProductFamily ? material.ProductFamily :
                    grouping == DashboardMetadataCodes.Dimensions.MaterialUnitType ? material.MaterialUnitType :
                    null,
                DefectCode = defect != null ? defect.DefectCode : null,
                DefectName = defect != null ? defect.DefectName : null,
                qualityEvent.EventAtUtc,
                material.SiteId
            };

        events = events.Where(x => x.GroupKey != null);

        if (resolved.FromUtc.HasValue)
        {
            var from = resolved.FromUtc.Value;
            events = events.Where(x => x.EventAtUtc >= from);
        }

        if (resolved.ToUtc.HasValue)
        {
            var to = resolved.ToUtc.Value;
            events = events.Where(x => x.EventAtUtc <= to);
        }

        var siteFilter = query.Filters?.SiteId;
        if (siteFilter.HasValue)
        {
            var site = siteFilter.Value;
            events = events.Where(x => x.SiteId == site);
        }

        var aggregated = await events
            .GroupBy(x => new { x.GroupKey, x.DefectCode, x.DefectName })
            .Select(g => new { g.Key.GroupKey, g.Key.DefectCode, g.Key.DefectName, Count = g.Count() })
            .Take(resolved.RawRowLimit + 1)
            .ToListAsync(cancellationToken);

        if (aggregated.Count > resolved.RawRowLimit)
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StatePopulationTooLarge), warnings);

        if (aggregated.Count == 0)
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StateNoObservations), warnings);

        if (aggregated.Select(x => x.DefectCode).Distinct().Count() < 2)
            return NativeWidgetResult.Build(resolved, columns, SeriesResult.StateOnly(SeriesResult.StateSingleSeries), warnings);

        // An event with no catalogue entry is a real event. Folding it into a
        // named type would misattribute it; dropping it would understate the
        // grade's total. It is named for what it is.
        const string Uncatalogued = "(uncatalogued)";

        var rows = aggregated
            .OrderBy(x => x.GroupKey, StringComparer.Ordinal)
            .ThenBy(x => x.DefectCode ?? Uncatalogued, StringComparer.Ordinal)
            .Select(x => SeriesResult.Cell(
                x.GroupKey!,
                x.GroupKey!,
                x.DefectCode ?? Uncatalogued,
                x.DefectName ?? Uncatalogued,
                x.Count))
            .ToList();

        return NativeWidgetResult.Build(resolved, columns, rows, warnings);
    }
}