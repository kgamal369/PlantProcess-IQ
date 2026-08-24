// Known answers for the generic controlled fixture.
//
// Backlog origin: T-208.
//
// Exact rationals, not decimals, so a kernel cannot pass by rounding luck. These
// constants were derived by hand from the fixture; the integrity tests recompute each
// one from the data with an independent reference implementation, so an accidental
// edit to the fixture is caught rather than silently absorbed.
namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

public readonly struct ExactRational
{
    public ExactRational(long numerator, long denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }

    public long Numerator { get; }
    public long Denominator { get; }

    public double AsDouble => (double)Numerator / Denominator;
}

public static class ContinuousProcessKnownAnswers
{
    // One signal, three defensible means.
    public static ExactRational ArithmeticMeanOfSamples => new(340, 3);       // 113.333... wrong for a continuous signal
    public static ExactRational TimeWeightedMeanLastValueHeld => new(715, 6); // 119.166...
    public static ExactRational TimeWeightedMeanLinear => new(1435, 12);      // 119.583...

    // A rate integrates; it does not sum.
    public const double NaiveSumOfRateSamples = 180d;                          // wrong
    public static ExactRational RateIntegralOverWindow => new(60, 1);

    // Mean-of-ratios is not ratio-of-sums.
    public static ExactRational MeanOfRatios => new(11, 15);   // 0.7333... wrong for a population question
    public static ExactRational RatioOfSums => new(45, 56);    // 0.8036...

    // Grain conversion needs the weight.
    public static ExactRational UnweightedSubjectMean => new(340, 3);   // wrong
    public static ExactRational DurationWeightedMean => new(365, 3);

    // Transition confounding.
    public static ExactRational PooledAcrossRegimes => new(615, 7);  // 87.857... must not be returned silently
    public static ExactRational StableRegimeOnly => new(100, 1);
    public const string RequiredRegimeRefusalCode = "RG01 mixed_process_regime";

    // Clock uncertainty is not conflict.
    public const string OverlappingVerdict = "TemporalUncertain";
    public const string DisjointVerdict = "ConflictingEvidence";

    // Fact-specific authority; never "machine wins".
    public static string[] RequiredEvidenceStates => new[]
    {
        "Aligned", "PartiallyAligned", "MissingEvidence", "ConflictingEvidence"
    };

    // Directionality is declared, never inferred.
    public static ExactRational AttainmentLowerIsBetter => new(10, 11);  // standard 100, actual 110
    public const double GapLowerIsBetter = 10d;

    // Required refusals.
    public const string InsufficientReference = "InsufficientReference";
    public const string CategoricalAverageUndefined = "AggregationUndefinedForCategorical";
    public const string InsufficientData = "InsufficientData";
}