using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel.Capability;

using PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// Industry-neutral capability profiler. Measures what the available data can actually
/// support, and returns the measured facts with every decision.
/// <para>
/// Three rules govern the whole class:
/// a single-level context dimension is collapsed, not an error;
/// absent genealogy removes only capabilities that genuinely need genealogy;
/// missing outcomes remove only capabilities that genuinely need labelled outcomes.
/// </para>
/// <para>
/// Reads no database, holds no connection, knows no customer vocabulary.
/// </para>
/// </summary>
public static class CapabilityProfiler
{
    // Thresholds carried from the eligibility expressions of the frozen model-family
    // registry. They are named constants so a reviewer can see and challenge each one.
    public const int MinUnitsForStatistics = 30;
    public const int MinUnitsForSimilarity = 5000;
    public const int MinUnitsForNovelty = 5000;
    public const int MinLabelledForSupervised = 500;
    public const double MinMinorityClassFraction = 0.03;
    public const int MinDistinctValuesForContinuous = 20;
    public const double MinHistoryDays = 90.0;
    public const int MinPracticeSignatures = 30;
    public const int MinInterventionsForEffect = 20;
    public const double MinGenealogyCoverage = 0.80;

    public static CapabilityProfile Profile(CapabilityProfilerInput input)
    {
        var dimensions = ProfileDimensions(input.ContextDimensions);
        var eligibleDimensionCount = dimensions.Count(d => d.Status == DimensionStatus.Eligible);

        var populationFacts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("analytical_units", input.AnalyticalUnitCount, MinUnitsForStatistics, "units"),
            MeasuredFact.AtLeast("history_span", input.HistorySpanDays, MinHistoryDays, "days"),
            MeasuredFact.Informational("declared_outcomes", input.Outcomes.Count, "count"),
            MeasuredFact.Informational("eligible_context_dimensions", eligibleDimensionCount, "count"),
            MeasuredFact.Informational("collapsed_context_dimensions",
                dimensions.Count(d => d.Status == DimensionStatus.Collapsed), "count"),
            MeasuredFact.Informational("process_positions", input.Genealogy.ProcessPositionCount, "count")
        };

        var capabilities = new List<CapabilityVerdict>
        {
            EvaluateStatistics(input),
            EvaluateSimilarity(input),
            EvaluateNovelty(input),
            EvaluateSupervisedPrediction(input),
            EvaluatePracticeLearning(input)
        };

        var practice = capabilities.First(c => c.Capability == CapabilityCode.PracticeLearning);
        capabilities.Add(EvaluateRemediation(input, practice, eligibleDimensionCount));

        return new CapabilityProfile(capabilities, dimensions, input.Genealogy.Strength, populationFacts);
    }

    // ------------------------------------------------------------ DIMENSIONS

    private static IReadOnlyList<DimensionVerdict> ProfileDimensions(
        IReadOnlyList<ContextDimensionObservation> observed)
    {
        var verdicts = new List<DimensionVerdict>();
        foreach (var d in observed ?? new List<ContextDimensionObservation>())
        {
            if (d.ObservedLevelCount <= 0)
            {
                verdicts.Add(new DimensionVerdict(d.DimensionCode, d.ObservedLevelCount,
                    DimensionStatus.Absent, d.IsVariantDimension,
                    "The dimension is declared but no level was observed in the population."));
                continue;
            }

            if (d.ObservedLevelCount == 1)
            {
                verdicts.Add(new DimensionVerdict(d.DimensionCode, 1,
                    DimensionStatus.Collapsed, d.IsVariantDimension,
                    "One effective level. The dimension is removed from the eligible set and "
                    + "cannot condition an analysis. This is not an error: an installation with "
                    + "a single level of this dimension is a normal installation."));
                continue;
            }

            verdicts.Add(new DimensionVerdict(d.DimensionCode, d.ObservedLevelCount,
                DimensionStatus.Eligible, d.IsVariantDimension,
                "Two or more effective levels. The dimension can condition an analysis."));
        }
        return verdicts;
    }

    // ------------------------------------------------------------ CAPABILITIES

    private static CapabilityVerdict EvaluateStatistics(CapabilityProfilerInput input)
    {
        var facts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("analytical_units", input.AnalyticalUnitCount, MinUnitsForStatistics, "units")
        };

        if (input.AnalyticalUnitCount < MinUnitsForStatistics)
            return Unavailable(CapabilityCode.Statistics, CapabilityShortfallCode.InsufficientPopulation,
                ExclusionAttribution.Data,
                $"The aligned population holds {input.AnalyticalUnitCount} analytical units; "
                + $"{MinUnitsForStatistics} are required before any association can be estimated.",
                facts);

        // Genealogy is not required for statistics. Its absence removes only the part that
        // needs it: association between a value at one process position and an outcome at another.
        if (input.Genealogy.Strength == GenealogyStrength.None)
        {
            facts.Add(MeasuredFact.Informational("genealogy_link_coverage", input.Genealogy.LinkCoverage, "fraction"));
            return new CapabilityVerdict(CapabilityCode.Statistics, CapabilityAvailability.Degraded,
                TerminalState.Finding, CapabilityShortfallCode.GenealogyAbsent, ExclusionAttribution.Data,
                "Statistics are available within a single process position. Cross-position "
                + "association is unavailable because no genealogy links one position to another.",
                facts, null);
        }

        return Available(CapabilityCode.Statistics,
            "Population is sufficient and positions are linked. Association is available.", facts);
    }

    private static CapabilityVerdict EvaluateSimilarity(CapabilityProfilerInput input)
    {
        var facts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("indexable_subjects", input.AnalyticalUnitCount, MinUnitsForSimilarity, "subjects")
        };

        if (input.AnalyticalUnitCount < MinUnitsForSimilarity)
            return Unavailable(CapabilityCode.Similarity, CapabilityShortfallCode.InsufficientPopulation,
                ExclusionAttribution.Data,
                $"The population holds {input.AnalyticalUnitCount} indexable subjects; "
                + $"{MinUnitsForSimilarity} are required before neighbour retrieval is meaningful.",
                facts);

        // Similarity needs neither labels nor genealogy. Their absence changes nothing here.
        return Available(CapabilityCode.Similarity,
            "Population is sufficient for neighbour retrieval. Labels and genealogy are not required.",
            facts);
    }

    private static CapabilityVerdict EvaluateNovelty(CapabilityProfilerInput input)
    {
        var facts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("reference_subjects", input.AnalyticalUnitCount, MinUnitsForNovelty, "subjects")
        };

        if (input.AnalyticalUnitCount < MinUnitsForNovelty)
            return Unavailable(CapabilityCode.Novelty, CapabilityShortfallCode.InsufficientPopulation,
                ExclusionAttribution.Data,
                $"The population holds {input.AnalyticalUnitCount} subjects; {MinUnitsForNovelty} "
                + "are required before a normal-operating reference can be characterised.",
                facts);

        return Available(CapabilityCode.Novelty,
            "Population is sufficient to characterise normal operation. Labels are not required.",
            facts);
    }

    private static CapabilityVerdict EvaluateSupervisedPrediction(CapabilityProfilerInput input)
    {
        var outcomes = input.Outcomes ?? new List<FixtureOutcomeDefinition>();

        // No outcome DECLARED is an authoring gap, not a data shortfall. The two are
        // different facts and must never share a reason or an attribution.
        if (outcomes.Count == 0)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.NoOutcomeDeclared,
                ExclusionAttribution.Declaration,
                "No outcome definition has been declared. Supervised prediction has nothing to "
                + "predict. The data may be adequate; nothing has said what counts as an outcome.",
                new List<MeasuredFact> { MeasuredFact.AtLeast("declared_outcomes", 0, 1, "count") });

        CapabilityVerdict? best = null;
        foreach (var outcome in outcomes)
        {
            var verdict = EvaluateOneOutcome(input, outcome);
            if (best == null || Rank(verdict.Availability) > Rank(best.Availability))
                best = verdict;
        }
        return best!;
    }

    private static CapabilityVerdict EvaluateOneOutcome(
        CapabilityProfilerInput input, FixtureOutcomeDefinition outcome)
    {
        bool isClassification = outcome.ValueType != OutcomeValueType.Continuous;

        var facts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("labelled_units", outcome.LabelledCount, MinLabelledForSupervised, "units"),
            MeasuredFact.AtLeast("history_span", input.HistorySpanDays, MinHistoryDays, "days")
        };
        if (isClassification)
            facts.Add(MeasuredFact.AtLeast("minority_class_fraction",
                outcome.MinorityClassFraction, MinMinorityClassFraction, "fraction"));
        else
            facts.Add(MeasuredFact.AtLeast("distinct_outcome_values",
                outcome.DistinctValueCount, MinDistinctValuesForContinuous, "values"));

        // A declared outcome with zero labels is a MEASURED data shortfall, not an
        // authoring gap. Distinct from NoOutcomeDeclared above.
        if (outcome.LabelledCount <= 0)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.NoLabelledOutcomes,
                ExclusionAttribution.Data,
                $"Outcome '{outcome.OutcomeCode}' is declared but no unit in the population "
                + "carries a value for it.", facts, outcome.OutcomeCode);

        if (!outcome.DetectionAnchorsDeclared)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.DetectionAnchorsUndeclared,
                ExclusionAttribution.Declaration,
                $"Outcome '{outcome.OutcomeCode}' does not declare where and when it becomes "
                + "known. Without those anchors the temporal leakage gate cannot be evaluated, "
                + "and a model could be trained on information from after the prediction point.",
                facts, outcome.OutcomeCode);

        if (outcome.LabelledCount < MinLabelledForSupervised)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.InsufficientLabelledPopulation,
                ExclusionAttribution.Data,
                $"Outcome '{outcome.OutcomeCode}' carries {outcome.LabelledCount} labelled units; "
                + $"{MinLabelledForSupervised} are required.", facts, outcome.OutcomeCode);

        if (isClassification && outcome.MinorityClassFraction < MinMinorityClassFraction)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.ClassImbalanceBelowFloor,
                ExclusionAttribution.Data,
                $"Outcome '{outcome.OutcomeCode}' has a minority class fraction of "
                + $"{outcome.MinorityClassFraction:0.####}; the floor is {MinMinorityClassFraction}.",
                facts, outcome.OutcomeCode);

        if (!isClassification && outcome.DistinctValueCount < MinDistinctValuesForContinuous)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.InsufficientDistinctValues,
                ExclusionAttribution.Data,
                $"Outcome '{outcome.OutcomeCode}' takes {outcome.DistinctValueCount} distinct "
                + $"values; {MinDistinctValuesForContinuous} are required for a regression target.",
                facts, outcome.OutcomeCode);

        if (input.HistorySpanDays < MinHistoryDays)
            return Unavailable(CapabilityCode.SupervisedPrediction, CapabilityShortfallCode.InsufficientHistory,
                ExclusionAttribution.Data,
                $"The population spans {input.HistorySpanDays:0.#} days; {MinHistoryDays} are "
                + "required before an out-of-time evaluation window can be held back.",
                facts, outcome.OutcomeCode);

        // Genealogy is not required to predict an outcome at the position where it is
        // measured. It is required only to predict it from an earlier position.
        if (input.Genealogy.Strength == GenealogyStrength.None)
        {
            facts.Add(MeasuredFact.Informational("genealogy_link_coverage", input.Genealogy.LinkCoverage, "fraction"));
            return new CapabilityVerdict(CapabilityCode.SupervisedPrediction, CapabilityAvailability.Degraded,
                TerminalState.Finding, CapabilityShortfallCode.GenealogyAbsent, ExclusionAttribution.Data,
                $"Outcome '{outcome.OutcomeCode}' can be predicted at its own process position. "
                + "Early prediction from an upstream position is unavailable because no genealogy "
                + "links the positions.", facts, outcome.OutcomeCode);
        }

        if (input.Genealogy.LinkCoverage < MinGenealogyCoverage)
        {
            facts.Add(MeasuredFact.AtLeast("genealogy_link_coverage",
                input.Genealogy.LinkCoverage, MinGenealogyCoverage, "fraction"));
            return new CapabilityVerdict(CapabilityCode.SupervisedPrediction, CapabilityAvailability.Degraded,
                TerminalState.Finding, CapabilityShortfallCode.GenealogyCoverageBelowFloor, ExclusionAttribution.Data,
                $"Outcome '{outcome.OutcomeCode}' is predictable, but only "
                + $"{input.Genealogy.LinkCoverage:0.##} of units resolve to an upstream parent, "
                + $"below the {MinGenealogyCoverage} floor. Early prediction covers part of the population.",
                facts, outcome.OutcomeCode);
        }

        facts.Add(MeasuredFact.AtLeast("genealogy_link_coverage",
            input.Genealogy.LinkCoverage, MinGenealogyCoverage, "fraction"));
        return new CapabilityVerdict(CapabilityCode.SupervisedPrediction, CapabilityAvailability.Available,
            TerminalState.Finding, CapabilityShortfallCode.None, ExclusionAttribution.None,
            $"Outcome '{outcome.OutcomeCode}' has sufficient labels, balance, history and linkage.",
            facts, outcome.OutcomeCode);
    }

    private static CapabilityVerdict EvaluatePracticeLearning(CapabilityProfilerInput input)
    {
        var facts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("controllable_parameters", input.Practice.ControllableParameterCount, 1, "parameters"),
            MeasuredFact.AtLeast("distinct_practice_signatures",
                input.Practice.DistinctPracticeSignatureCount, MinPracticeSignatures, "signatures"),
            MeasuredFact.AtLeast("analytical_units", input.AnalyticalUnitCount, MinUnitsForStatistics, "units")
        };

        if (input.Practice.ControllableParameterCount <= 0)
            return Unavailable(CapabilityCode.PracticeLearning, CapabilityShortfallCode.NoControllableParameters,
                ExclusionAttribution.Data,
                "No parameter is declared controllable. A practice is a combination of things an "
                + "operator can set; with none, there is no practice to learn.", facts);

        if (input.AnalyticalUnitCount < MinUnitsForStatistics)
            return Unavailable(CapabilityCode.PracticeLearning, CapabilityShortfallCode.InsufficientPopulation,
                ExclusionAttribution.Data,
                $"The population holds {input.AnalyticalUnitCount} units; {MinUnitsForStatistics} are required.",
                facts);

        if (input.Practice.DistinctPracticeSignatureCount < MinPracticeSignatures)
            return Unavailable(CapabilityCode.PracticeLearning, CapabilityShortfallCode.InsufficientPracticeSignatures,
                ExclusionAttribution.Data,
                $"The population yields {input.Practice.DistinctPracticeSignatureCount} distinct "
                + $"practice signatures; {MinPracticeSignatures} are required before practices can "
                + "be compared.", facts);

        // Practices can be identified without outcomes. They just cannot be ranked by result.
        bool anyLabelled = (input.Outcomes ?? new List<FixtureOutcomeDefinition>()).Any(o => o.LabelledCount > 0);
        if (!anyLabelled)
            return new CapabilityVerdict(CapabilityCode.PracticeLearning, CapabilityAvailability.Degraded,
                TerminalState.Finding, CapabilityShortfallCode.NoLabelledOutcomes, ExclusionAttribution.Data,
                "Practices can be identified and counted. They cannot be ranked as better or worse "
                + "because no outcome carries a value in this population.", facts, null);

        return Available(CapabilityCode.PracticeLearning,
            "Controllable parameters, population and signature variety are sufficient, and outcomes "
            + "exist to rank practices by.", facts);
    }

    private static CapabilityVerdict EvaluateRemediation(
        CapabilityProfilerInput input, CapabilityVerdict practice, int eligibleDimensionCount)
    {
        var facts = new List<MeasuredFact>
        {
            MeasuredFact.AtLeast("controllable_parameters", input.Practice.ControllableParameterCount, 1, "parameters"),
            MeasuredFact.AtLeast("recorded_interventions",
                input.Interventions.RecordedInterventionCount, MinInterventionsForEffect, "interventions"),
            MeasuredFact.AtLeast("eligible_context_dimensions", eligibleDimensionCount, 1, "dimensions")
        };

        // Remediation stands on practice learning. If that is unavailable, so is this,
        // and the reason names the upstream cause rather than inventing a new one.
        if (practice.Availability == CapabilityAvailability.Unavailable)
            return Unavailable(CapabilityCode.Remediation, practice.Shortfall, practice.Attribution,
                "Remediation requires practice learning, which is unavailable: " + practice.Reason,
                facts);

        if (input.Practice.ControllableParameterCount <= 0)
            return Unavailable(CapabilityCode.Remediation, CapabilityShortfallCode.NoControllableParameters,
                ExclusionAttribution.Data,
                "No parameter is declared controllable. A remediation that changes nothing an "
                + "operator can set is not a remediation.", facts);

        if (input.Interventions.RecordedInterventionCount < MinInterventionsForEffect)
            return new CapabilityVerdict(CapabilityCode.Remediation, CapabilityAvailability.Degraded,
                TerminalState.Finding, CapabilityShortfallCode.NoInterventionHistory, ExclusionAttribution.Data,
                $"Candidates can be surfaced as observed historical differences. With "
                + $"{input.Interventions.RecordedInterventionCount} recorded interventions against a "
                + $"floor of {MinInterventionsForEffect}, no uplift can be estimated, so no candidate "
                + "can rise above evidence-only.", facts, null);

        if (eligibleDimensionCount == 0)
            return new CapabilityVerdict(CapabilityCode.Remediation, CapabilityAvailability.Degraded,
                TerminalState.Finding, CapabilityShortfallCode.NoEligibleContextDimension, ExclusionAttribution.Data,
                "Interventions exist, but no context dimension has two or more levels, so an effect "
                + "cannot be checked for survival under stratification.", facts, null);

        return Available(CapabilityCode.Remediation,
            "Controllable parameters, intervention history and at least one conditioning dimension "
            + "are present.", facts);
    }

    // ------------------------------------------------------------ HELPERS

    private static int Rank(CapabilityAvailability a) => a switch
    {
        CapabilityAvailability.Available => 2,
        CapabilityAvailability.Degraded => 1,
        _ => 0
    };

    private static CapabilityVerdict Available(
        CapabilityCode capability, string reason, IReadOnlyList<MeasuredFact> facts) =>
        new(capability, CapabilityAvailability.Available, TerminalState.Finding,
            CapabilityShortfallCode.None, ExclusionAttribution.None, reason, facts, null);

    private static CapabilityVerdict Unavailable(
        CapabilityCode capability, CapabilityShortfallCode shortfall, ExclusionAttribution attribution,
        string reason, IReadOnlyList<MeasuredFact> facts, string? subject = null) =>
        new(capability, CapabilityAvailability.Unavailable, TerminalState.NotApplicable,
            shortfall, attribution, reason, facts, subject);
}
