using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Analytics.Core.Kernel.Capability;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

/// <summary>
/// T-171 Capability Profiler. Every test encodes one ruling:
/// a single-level dimension is collapsed, not an error;
/// absent genealogy removes only what genuinely needs genealogy;
/// missing outcomes remove only what genuinely needs labelled outcomes;
/// no capability is ever reported unavailable without the measured number behind it.
/// All inputs are typed fixtures. No database, no SQL, no production outcome semantics.
/// </summary>
public sealed class T171_CapabilityProfilerTests
{
    private static FixtureOutcomeDefinition GoodOutcome(string code = "outcome_a") =>
        new(code, OutcomeValueType.Binary, "grain_a", true, 6000, 0.12, 2);

    private static CapabilityProfilerInput Rich(
        int units = 20000,
        double historyDays = 400,
        IReadOnlyList<FixtureOutcomeDefinition>? outcomes = null,
        IReadOnlyList<ContextDimensionObservation>? dimensions = null,
        GenealogyObservation? genealogy = null,
        PracticeObservation? practice = null,
        int interventions = 60) =>
        new(units, historyDays,
            outcomes ?? new[] { GoodOutcome() },
            dimensions ?? new[]
            {
                new ContextDimensionObservation("dim_variant", 4, true),
                new ContextDimensionObservation("dim_context", 3, false)
            },
            genealogy ?? new GenealogyObservation(GenealogyStrength.Sequential, 0.95, 5),
            practice ?? new PracticeObservation(8, 40, 120),
            new InterventionObservation(interventions));

    // ---------------------------------------------------------------- baseline

    [Fact]
    public void A_rich_installation_supports_every_capability()
    {
        var profile = CapabilityProfiler.Profile(Rich());

        Assert.Equal(6, profile.Capabilities.Count);
        foreach (var verdict in profile.Capabilities)
        {
            Assert.Equal(CapabilityAvailability.Available, verdict.Availability);
            Assert.Equal(CapabilityShortfallCode.None, verdict.Shortfall);
            Assert.Equal(ExclusionAttribution.None, verdict.Attribution);
        }
    }

    // ---------------------------------------------------------------- collapsed dimensions

    [Fact]
    public void A_single_level_dimension_is_collapsed_and_is_not_an_error()
    {
        var profile = CapabilityProfiler.Profile(Rich(dimensions: new[]
        {
            new ContextDimensionObservation("dim_variant", 4, true),
            new ContextDimensionObservation("dim_context", 1, false)
        }));

        var collapsed = profile.Dimensions.Single(d => d.DimensionCode == "dim_context");
        Assert.Equal(DimensionStatus.Collapsed, collapsed.Status);
        Assert.Equal(1, collapsed.ObservedLevelCount);
        Assert.Contains("not an error", collapsed.Reason);

        // It is removed from the eligible set, not reported as a failure.
        Assert.DoesNotContain(profile.EligibleDimensions, d => d.DimensionCode == "dim_context");
        Assert.Single(profile.EligibleDimensions);
    }

    [Fact]
    public void A_collapsed_dimension_changes_no_capability_verdict()
    {
        var withAll = CapabilityProfiler.Profile(Rich());
        var withCollapsed = CapabilityProfiler.Profile(Rich(dimensions: new[]
        {
            new ContextDimensionObservation("dim_variant", 4, true),
            new ContextDimensionObservation("dim_context", 1, false)
        }));

        foreach (var code in System.Enum.GetValues<CapabilityCode>())
        {
            Assert.Equal(withAll.For(code).Availability, withCollapsed.For(code).Availability);
            Assert.Equal(withAll.For(code).Shortfall, withCollapsed.For(code).Shortfall);
        }
    }

    [Fact]
    public void An_unobserved_dimension_is_absent_and_is_not_reported_as_a_zero_level_eligible_one()
    {
        var profile = CapabilityProfiler.Profile(Rich(dimensions: new[]
        {
            new ContextDimensionObservation("dim_variant", 4, true),
            new ContextDimensionObservation("dim_missing", 0, false)
        }));

        var absent = profile.Dimensions.Single(d => d.DimensionCode == "dim_missing");
        Assert.Equal(DimensionStatus.Absent, absent.Status);
        Assert.DoesNotContain(profile.EligibleDimensions, d => d.DimensionCode == "dim_missing");
    }

    // ---------------------------------------------------------------- genealogy

    [Fact]
    public void Absent_genealogy_does_not_make_the_whole_product_unready()
    {
        var profile = CapabilityProfiler.Profile(Rich(
            genealogy: new GenealogyObservation(GenealogyStrength.None, 0.0, 1)));

        // Capabilities that do not need genealogy are untouched.
        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.Similarity).Availability);
        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.Novelty).Availability);
        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.PracticeLearning).Availability);

        // Capabilities that genuinely use genealogy are degraded, not removed.
        Assert.Equal(CapabilityAvailability.Degraded, profile.For(CapabilityCode.Statistics).Availability);
        Assert.Equal(CapabilityAvailability.Degraded, profile.For(CapabilityCode.SupervisedPrediction).Availability);
        Assert.Equal(CapabilityShortfallCode.GenealogyAbsent, profile.For(CapabilityCode.Statistics).Shortfall);
    }

    [Fact]
    public void Degraded_genealogy_names_which_part_is_unavailable()
    {
        var statistics = CapabilityProfiler.Profile(Rich(
            genealogy: new GenealogyObservation(GenealogyStrength.None, 0.0, 1)))
            .For(CapabilityCode.Statistics);

        Assert.Contains("within a single process position", statistics.Reason);
        Assert.Contains("Cross-position", statistics.Reason);
    }

    [Fact]
    public void Partial_genealogy_coverage_degrades_rather_than_removes_prediction()
    {
        var profile = CapabilityProfiler.Profile(Rich(
            genealogy: new GenealogyObservation(GenealogyStrength.Sequential, 0.40, 5)));

        var supervised = profile.For(CapabilityCode.SupervisedPrediction);
        Assert.Equal(CapabilityAvailability.Degraded, supervised.Availability);
        Assert.Equal(CapabilityShortfallCode.GenealogyCoverageBelowFloor, supervised.Shortfall);
        Assert.Contains(supervised.Facts, f => f.Code == "genealogy_link_coverage" && !f.Satisfied);
    }

    // ---------------------------------------------------------------- outcomes

    [Fact]
    public void No_outcome_DECLARED_is_attributed_to_the_declaration_not_the_data()
    {
        var supervised = CapabilityProfiler.Profile(
            Rich(outcomes: new List<FixtureOutcomeDefinition>()))
            .For(CapabilityCode.SupervisedPrediction);

        Assert.Equal(CapabilityAvailability.Unavailable, supervised.Availability);
        Assert.Equal(CapabilityShortfallCode.NoOutcomeDeclared, supervised.Shortfall);
        Assert.Equal(ExclusionAttribution.Declaration, supervised.Attribution);
        Assert.Contains("The data may be adequate", supervised.Reason);
    }

    [Fact]
    public void An_outcome_declared_with_zero_labels_is_attributed_to_the_data()
    {
        var supervised = CapabilityProfiler.Profile(Rich(outcomes: new[]
            {
                new FixtureOutcomeDefinition("outcome_a", OutcomeValueType.Binary, "grain_a", true, 0, double.NaN, 0)
            }))
            .For(CapabilityCode.SupervisedPrediction);

        Assert.Equal(CapabilityShortfallCode.NoLabelledOutcomes, supervised.Shortfall);
        Assert.Equal(ExclusionAttribution.Data, supervised.Attribution);
    }

    [Fact]
    public void Undeclared_outcome_and_unlabelled_outcome_never_share_a_reason_or_an_attribution()
    {
        var undeclared = CapabilityProfiler.Profile(Rich(outcomes: new List<FixtureOutcomeDefinition>()))
            .For(CapabilityCode.SupervisedPrediction);

        var unlabelled = CapabilityProfiler.Profile(Rich(outcomes: new[]
            {
                new FixtureOutcomeDefinition("outcome_a", OutcomeValueType.Binary, "grain_a", true, 0, double.NaN, 0)
            }))
            .For(CapabilityCode.SupervisedPrediction);

        Assert.NotEqual(undeclared.Shortfall, unlabelled.Shortfall);
        Assert.NotEqual(undeclared.Attribution, unlabelled.Attribution);
        Assert.NotEqual(undeclared.Reason, unlabelled.Reason);
    }

    [Fact]
    public void Undeclared_detection_anchors_block_prediction_and_name_the_leakage_gate()
    {
        var supervised = CapabilityProfiler.Profile(Rich(outcomes: new[]
            {
                new FixtureOutcomeDefinition("outcome_a", OutcomeValueType.Binary, "grain_a", false, 6000, 0.12, 2)
            }))
            .For(CapabilityCode.SupervisedPrediction);

        Assert.Equal(CapabilityAvailability.Unavailable, supervised.Availability);
        Assert.Equal(CapabilityShortfallCode.DetectionAnchorsUndeclared, supervised.Shortfall);
        Assert.Equal(ExclusionAttribution.Declaration, supervised.Attribution);
        Assert.Contains("leakage", supervised.Reason);
    }

    [Fact]
    public void Class_imbalance_below_the_floor_reports_the_measured_fraction()
    {
        var supervised = CapabilityProfiler.Profile(Rich(outcomes: new[]
            {
                new FixtureOutcomeDefinition("outcome_a", OutcomeValueType.Binary, "grain_a", true, 6000, 0.004, 2)
            }))
            .For(CapabilityCode.SupervisedPrediction);

        Assert.Equal(CapabilityShortfallCode.ClassImbalanceBelowFloor, supervised.Shortfall);
        Assert.Contains(supervised.Facts,
            f => f.Code == "minority_class_fraction" && !f.Satisfied && f.Observed == 0.004);
    }

    [Fact]
    public void The_best_supported_outcome_decides_the_capability()
    {
        var supervised = CapabilityProfiler.Profile(Rich(outcomes: new[]
            {
                new FixtureOutcomeDefinition("weak", OutcomeValueType.Binary, "grain_a", true, 10, 0.4, 2),
                new FixtureOutcomeDefinition("strong", OutcomeValueType.Binary, "grain_a", true, 6000, 0.12, 2)
            }))
            .For(CapabilityCode.SupervisedPrediction);

        Assert.Equal(CapabilityAvailability.Available, supervised.Availability);
        Assert.Equal("strong", supervised.Subject);
    }

    [Fact]
    public void Missing_outcomes_do_not_remove_capabilities_that_never_needed_them()
    {
        var profile = CapabilityProfiler.Profile(Rich(outcomes: new List<FixtureOutcomeDefinition>()));

        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.Similarity).Availability);
        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.Novelty).Availability);
        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.Statistics).Availability);

        // Practices can still be identified. They just cannot be ranked by result.
        var practice = profile.For(CapabilityCode.PracticeLearning);
        Assert.Equal(CapabilityAvailability.Degraded, practice.Availability);
        Assert.Contains("cannot be ranked", practice.Reason);
    }

    // ---------------------------------------------------------------- practice and remediation

    [Fact]
    public void No_controllable_parameter_removes_practice_and_remediation_only()
    {
        var profile = CapabilityProfiler.Profile(Rich(practice: new PracticeObservation(0, 40, 120)));

        Assert.Equal(CapabilityAvailability.Unavailable, profile.For(CapabilityCode.PracticeLearning).Availability);
        Assert.Equal(CapabilityShortfallCode.NoControllableParameters,
            profile.For(CapabilityCode.PracticeLearning).Shortfall);
        Assert.Equal(CapabilityAvailability.Unavailable, profile.For(CapabilityCode.Remediation).Availability);

        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.Statistics).Availability);
        Assert.Equal(CapabilityAvailability.Available, profile.For(CapabilityCode.SupervisedPrediction).Availability);
    }

    [Fact]
    public void Remediation_without_intervention_history_is_capped_at_evidence_only()
    {
        var remediation = CapabilityProfiler.Profile(Rich(interventions: 0))
            .For(CapabilityCode.Remediation);

        Assert.Equal(CapabilityAvailability.Degraded, remediation.Availability);
        Assert.Equal(CapabilityShortfallCode.NoInterventionHistory, remediation.Shortfall);
        Assert.Contains("evidence-only", remediation.Reason);
    }

    [Fact]
    public void Remediation_names_the_upstream_cause_when_practice_is_unavailable()
    {
        var profile = CapabilityProfiler.Profile(Rich(practice: new PracticeObservation(8, 40, 3)));

        var practice = profile.For(CapabilityCode.PracticeLearning);
        var remediation = profile.For(CapabilityCode.Remediation);

        Assert.Equal(CapabilityShortfallCode.InsufficientPracticeSignatures, practice.Shortfall);
        Assert.Equal(practice.Shortfall, remediation.Shortfall);
        Assert.Contains("requires practice learning", remediation.Reason);
    }

    [Fact]
    public void Remediation_with_no_eligible_dimension_cannot_stratify_and_says_so()
    {
        var remediation = CapabilityProfiler.Profile(Rich(dimensions: new[]
            {
                new ContextDimensionObservation("dim_context", 1, false)
            }))
            .For(CapabilityCode.Remediation);

        Assert.Equal(CapabilityAvailability.Degraded, remediation.Availability);
        Assert.Equal(CapabilityShortfallCode.NoEligibleContextDimension, remediation.Shortfall);
        Assert.Contains("stratification", remediation.Reason);
    }

    // ---------------------------------------------------------------- honesty invariants

    [Fact]
    public void No_capability_is_ever_unavailable_without_an_unsatisfied_measured_fact()
    {
        var inputs = new[]
        {
            Rich(units: 5),
            Rich(outcomes: new List<FixtureOutcomeDefinition>()),
            Rich(practice: new PracticeObservation(0, 0, 0)),
            Rich(historyDays: 3),
            Rich(units: 500, outcomes: new List<FixtureOutcomeDefinition>(),
                 genealogy: new GenealogyObservation(GenealogyStrength.None, 0.0, 1),
                 practice: new PracticeObservation(0, 0, 0), interventions: 0)
        };

        foreach (var input in inputs)
        {
            foreach (var verdict in CapabilityProfiler.Profile(input).Capabilities)
            {
                if (verdict.Availability == CapabilityAvailability.Available) continue;

                Assert.NotEmpty(verdict.Facts);
                Assert.NotEqual(CapabilityShortfallCode.None, verdict.Shortfall);
                Assert.False(string.IsNullOrWhiteSpace(verdict.Reason));
                Assert.NotEqual(ExclusionAttribution.None, verdict.Attribution);
            }
        }
    }

    [Fact]
    public void Every_verdict_carries_the_numbers_behind_it()
    {
        foreach (var verdict in CapabilityProfiler.Profile(Rich()).Capabilities)
        {
            Assert.NotEmpty(verdict.Facts);
            foreach (var fact in verdict.Facts)
                Assert.False(string.IsNullOrWhiteSpace(fact.Code));
        }
    }

    [Fact]
    public void The_profile_speaks_the_common_refusal_language()
    {
        var supervised = CapabilityProfiler.Profile(
            Rich(outcomes: new List<FixtureOutcomeDefinition>()))
            .For(CapabilityCode.SupervisedPrediction);

        // Shared TerminalState and shared attribution, capability-specific shortfall code.
        Assert.Equal(TerminalState.NotApplicable, supervised.TerminalState);
        Assert.Equal(ExclusionAttribution.Declaration, supervised.Attribution);
        Assert.IsType<CapabilityShortfallCode>(supervised.Shortfall);
    }

    [Fact]
    public void Capability_shortfall_codes_contain_no_statistical_method_concepts()
    {
        var names = System.Enum.GetNames(typeof(CapabilityShortfallCode));

        foreach (var forbidden in new[] { "Variance", "Pairing", "Anova", "Kruskal", "Method" })
            Assert.DoesNotContain(names,
                n => n.Contains(forbidden, System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_profiler_is_deterministic()
    {
        var a = CapabilityProfiler.Profile(Rich());
        var b = CapabilityProfiler.Profile(Rich());

        for (int i = 0; i < a.Capabilities.Count; i++)
        {
            Assert.Equal(a.Capabilities[i].Capability, b.Capabilities[i].Capability);
            Assert.Equal(a.Capabilities[i].Availability, b.Capabilities[i].Availability);
            Assert.Equal(a.Capabilities[i].Shortfall, b.Capabilities[i].Shortfall);
            Assert.Equal(a.Capabilities[i].Reason, b.Capabilities[i].Reason);
        }
    }

    [Fact]
    public void The_profiler_holds_no_database_or_presentation_dependency()
    {
        var referenced = typeof(CapabilityProfiler).Assembly
            .GetReferencedAssemblies().Select(r => r.Name ?? string.Empty).ToArray();

        foreach (var forbidden in new[]
                 { "Npgsql", "Microsoft.EntityFrameworkCore", "PlantProcess.Infrastructure", "PlantProcess.Api" })
            Assert.DoesNotContain(referenced,
                r => r.StartsWith(forbidden, System.StringComparison.OrdinalIgnoreCase));
    }
}
