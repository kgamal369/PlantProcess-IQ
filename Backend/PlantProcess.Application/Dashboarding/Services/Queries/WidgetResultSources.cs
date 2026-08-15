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