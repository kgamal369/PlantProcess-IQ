// Fixture integrity. This is what the task owns, and it must be GREEN.
//
// Backlog origin: T-208.
//
// These tests do not measure the product engine. They prove the oracle is sound:
// deterministic, populated, vocabulary-neutral, and that its stated answers actually
// follow from its data. Whether the engine can reproduce them is a separate,
// non-blocking probe - see CurrentEngineCompatibilityTests.
using System;
using System.Linq;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-208")]
public sealed class GenericProcessFixtureIntegrityTests
{
    private static double TimeWeightedLastValueHeld()
    {
        var s = GenericProcessFixture.ContinuousSignal;
        double area = 0, span = 0;

        for (var i = 0; i < s.Count - 1; i++)
        {
            var dt = (s[i + 1].At - s[i].At).TotalMinutes;
            area += s[i].NumericValue!.Value * dt;
            span += dt;
        }

        return area / span;
    }

    private static double TimeWeightedLinear()
    {
        var s = GenericProcessFixture.ContinuousSignal;
        double area = 0, span = 0;

        for (var i = 0; i < s.Count - 1; i++)
        {
            var dt = (s[i + 1].At - s[i].At).TotalMinutes;
            area += (s[i].NumericValue!.Value + s[i + 1].NumericValue!.Value) / 2 * dt;
            span += dt;
        }

        return area / span;
    }

    [Fact]
    public void One_signal_yields_three_defensible_and_different_means()
    {
        var samples = GenericProcessFixture.ContinuousSignal.Select(o => o.NumericValue!.Value).ToArray();

        Assert.Equal(ContinuousProcessKnownAnswers.ArithmeticMeanOfSamples.AsDouble, samples.Average(), 10);
        Assert.Equal(ContinuousProcessKnownAnswers.TimeWeightedMeanLastValueHeld.AsDouble, TimeWeightedLastValueHeld(), 10);
        Assert.Equal(ContinuousProcessKnownAnswers.TimeWeightedMeanLinear.AsDouble, TimeWeightedLinear(), 10);

        // The point: a kernel that does not carry the declared interpolation rule must
        // refuse, not quietly pick one of these.
        Assert.NotEqual(
            ContinuousProcessKnownAnswers.TimeWeightedMeanLastValueHeld.AsDouble,
            ContinuousProcessKnownAnswers.TimeWeightedMeanLinear.AsDouble,
            10);
    }

    [Fact]
    public void A_rate_integrates_and_does_not_sum()
    {
        var r = GenericProcessFixture.RateSignal;
        double integral = 0;

        for (var i = 0; i < r.Count - 1; i++)
        {
            integral += r[i].NumericValue!.Value * (r[i + 1].At - r[i].At).TotalHours;
        }

        Assert.Equal(ContinuousProcessKnownAnswers.RateIntegralOverWindow.AsDouble, integral, 10);
        Assert.Equal(ContinuousProcessKnownAnswers.NaiveSumOfRateSamples, r.Sum(x => x.NumericValue!.Value), 10);
        Assert.NotEqual(ContinuousProcessKnownAnswers.NaiveSumOfRateSamples, integral, 10);
    }

    [Fact]
    public void Mean_of_ratios_is_not_ratio_of_sums()
    {
        var subjects = GenericProcessFixture.RatioSubjects;

        Assert.Equal(ContinuousProcessKnownAnswers.MeanOfRatios.AsDouble,
            subjects.Average(s => s.Numerator / s.Denominator), 10);

        Assert.Equal(ContinuousProcessKnownAnswers.RatioOfSums.AsDouble,
            subjects.Sum(s => s.Numerator) / subjects.Sum(s => s.Denominator), 10);
    }

    [Fact]
    public void Grain_conversion_needs_the_weight()
    {
        var subjects = GenericProcessFixture.WeightedSubjects;

        Assert.Equal(ContinuousProcessKnownAnswers.UnweightedSubjectMean.AsDouble,
            subjects.Average(s => s.Mean), 10);

        Assert.Equal(ContinuousProcessKnownAnswers.DurationWeightedMean.AsDouble,
            subjects.Sum(s => s.DurationMinutes * s.Mean) / subjects.Sum(s => s.DurationMinutes), 10);
    }

    [Fact]
    public void Transition_confounding_changes_the_answer()
    {
        var stable = GenericProcessFixture.StableRegimeValues;
        var pooled = stable.Concat(GenericProcessFixture.TransitionRegimeValues).Average();

        Assert.Equal(ContinuousProcessKnownAnswers.StableRegimeOnly.AsDouble, stable.Average(), 10);
        Assert.Equal(ContinuousProcessKnownAnswers.PooledAcrossRegimes.AsDouble, pooled, 10);
        Assert.Contains(ProcessRegime.Transition, GenericProcessFixture.Regimes.Select(r => r.Regime));
        Assert.Contains(ProcessRegime.Stabilising, GenericProcessFixture.Regimes.Select(r => r.Regime));
    }

    [Fact]
    public void The_fixture_carries_both_an_overlapping_and_a_disjoint_temporal_case()
    {
        static bool Overlaps(ProcessObservation a, ProcessObservation b) =>
            a.At - a.ClockUncertainty <= b.At + b.ClockUncertainty &&
            b.At - b.ClockUncertainty <= a.At + a.ClockUncertainty;

        var over = GenericProcessFixture.TemporalPairOverlapping;
        var dis = GenericProcessFixture.TemporalPairDisjoint;

        Assert.True(Overlaps(over[0], over[1]), "the overlapping pair must actually overlap");
        Assert.False(Overlaps(dis[0], dis[1]), "the disjoint pair must actually be disjoint");
        Assert.NotEqual(ContinuousProcessKnownAnswers.OverlappingVerdict, ContinuousProcessKnownAnswers.DisjointVerdict);
    }

    [Fact]
    public void Evidence_pairs_cover_every_required_state()
    {
        var bySubject = GenericProcessFixture.EvidencePairs.GroupBy(o => o.SubjectId).ToArray();

        Assert.Equal(4, bySubject.Length);
        Assert.Single(bySubject, g => g.Count() == 1);               // MissingEvidence
        Assert.Contains(bySubject, g => g.Any(o => o.Source == ObservationSourceKind.Manual));
        Assert.Equal(4, ContinuousProcessKnownAnswers.RequiredEvidenceStates.Length);
    }

    [Fact]
    public void Reference_precedence_holds_and_one_parameter_is_deliberately_uncovered()
    {
        var applicable = GenericProcessFixture.References
            .Where(r => r.ParameterId == GenericProcessFixture.ContinuousParameter)
            .OrderBy(r => r.Precedence)
            .ToArray();

        Assert.Equal("EngineeringStandard", applicable[0].Kind);
        Assert.True(applicable[0].LowerIsBetter);

        Assert.Equal(ContinuousProcessKnownAnswers.AttainmentLowerIsBetter.AsDouble,
            applicable[0].Value!.Value / 110d, 10);

        Assert.Equal(ContinuousProcessKnownAnswers.GapLowerIsBetter, 110d - applicable[0].Value!.Value, 10);

        // Insufficient, not zero.
        var rate = GenericProcessFixture.References.Single(r => r.ParameterId == GenericProcessFixture.RateParameter);
        Assert.True(rate.EffectiveTo <= rate.EffectiveFrom || rate.Value is null);
    }

    [Fact]
    public void The_categorical_parameter_carries_no_numeric_value()
    {
        Assert.All(GenericProcessFixture.StateSignal, o =>
        {
            Assert.Null(o.NumericValue);
            Assert.False(string.IsNullOrWhiteSpace(o.CategoryValue));
        });
    }

    [Fact]
    public void The_empty_window_sits_beside_a_populated_one()
    {
        Assert.Empty(GenericProcessFixture.EmptyWindow);
        Assert.NotEmpty(GenericProcessFixture.ContinuousSignal);
    }

    [Fact]
    public void Every_known_answer_rests_on_a_non_empty_population()
    {
        Assert.NotEmpty(GenericProcessFixture.ContinuousSignal);
        Assert.NotEmpty(GenericProcessFixture.RateSignal);
        Assert.NotEmpty(GenericProcessFixture.RatioSubjects);
        Assert.NotEmpty(GenericProcessFixture.WeightedSubjects);
        Assert.NotEmpty(GenericProcessFixture.StableRegimeValues);
        Assert.NotEmpty(GenericProcessFixture.TransitionRegimeValues);
        Assert.NotEmpty(GenericProcessFixture.EvidencePairs);
        Assert.NotEmpty(GenericProcessFixture.References);
        Assert.NotEmpty(GenericProcessFixture.StateSignal);

        // A known answer that runs on an empty population is a failed one, not a passed one.
        Assert.True(GenericProcessFixture.AllObservations.Count >= 20);
    }

    [Fact]
    public void The_fixture_is_deterministic_and_never_reads_the_wall_clock()
    {
        static string Signature() => string.Join("|", GenericProcessFixture.AllObservations.Select(o =>
            o.SubjectId + o.ParameterId + o.At.ToUnixTimeSeconds() + o.NumericValue + o.CategoryValue));

        Assert.Equal(Signature(), Signature());
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), FrozenTestEpoch.Origin);
    }

    [Fact]
    public void The_fixture_vocabulary_is_industry_neutral()
    {
        var text = string.Join(" ", GenericProcessFixture.AllObservations
            .Select(o => o.SubjectId + " " + o.UnitId + " " + o.ParameterId + " " + o.CategoryValue))
            .ToLowerInvariant();

        foreach (var forbidden in new[] { "coil", "heat", "slab", "caster", "mill", "furnace", "steel", "grade", "fleet" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Expected_answers_are_not_reachable_from_the_data_surface()
    {
        // The oracle is not a lookup table. Nothing a kernel consumes may expose the
        // answer it is being asked to compute.
        var leaks = typeof(GenericProcessFixture)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(ExactRational))
            .ToArray();

        Assert.Empty(leaks);
    }
}