using System;
using System.Collections.Generic;
using PlantProcess.Application.Analytics.Calibration;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

public class RiskCalibrationServiceTests
{
    private static readonly RiskCalibrationService Service = new();

    [Fact]
    public void Brier_score_matches_hand_computed_value()
    {
        var samples = new List<CalibrationSample>
        {
            new(0.9, true), new(0.8, true), new(0.2, false), new(0.1, false)
        };
        // ((0.9-1)^2 + (0.8-1)^2 + (0.2-0)^2 + (0.1-0)^2) / 4 = (0.01 + 0.04 + 0.04 + 0.01) / 4 = 0.025
        var result = Service.Compute(samples, buckets: 10,
            thresholds: new CalibrationThresholds(MinOutcomes: 0, MaxBrier: 1.0, MaxGap: 1.0));

        Assert.Equal(0.025, result.BrierScore, 3);
        Assert.Equal(4, result.SampleSize);
        Assert.False(result.Abstain);
    }

    [Fact]
    public void Miscalibrated_model_abstains_instead_of_emitting_a_number()
    {
        // Predicts low risk (0.1) yet 70/100 outcomes are positive -> large reliability gap.
        var samples = new List<CalibrationSample>();
        for (var i = 0; i < 100; i++) samples.Add(new CalibrationSample(0.1, i < 70));

        var result = Service.Compute(samples, buckets: 10);

        Assert.True(result.Abstain);
        Assert.NotNull(result.AbstainReason);
        Assert.Contains("calibration", result.AbstainReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Too_few_outcomes_abstains()
    {
        var samples = new List<CalibrationSample> { new(0.9, true), new(0.1, false), new(0.5, true) };
        var result = Service.Compute(samples, buckets: 5, thresholds: new CalibrationThresholds(MinOutcomes: 50));

        Assert.True(result.Abstain);
        Assert.Contains("outcomes", result.AbstainReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reliability_buckets_are_correct()
    {
        var samples = new List<CalibrationSample>
        {
            new(0.05, false), new(0.05, false), // bucket 0 [0.0-0.1): observed 0.0
            new(0.95, true),  new(0.95, true)   // bucket 9 [0.9-1.0]: observed 1.0
        };
        var result = Service.Compute(samples, buckets: 10,
            thresholds: new CalibrationThresholds(MinOutcomes: 0, MaxBrier: 1.0, MaxGap: 1.0));

        Assert.Equal(2, result.ReliabilityCurve[0].Count);
        Assert.Equal(0.0, result.ReliabilityCurve[0].ObservedFrequency, 6);
        Assert.Equal(2, result.ReliabilityCurve[9].Count);
        Assert.Equal(1.0, result.ReliabilityCurve[9].ObservedFrequency, 6);
    }

    [Fact]
    public void Same_input_is_reproducible()
    {
        var samples = new List<CalibrationSample> { new(0.7, true), new(0.3, false), new(0.6, true), new(0.4, false) };
        var a = Service.Compute(samples, 10, new CalibrationThresholds(MinOutcomes: 0));
        var b = Service.Compute(samples, 10, new CalibrationThresholds(MinOutcomes: 0));

        Assert.Equal(a.BrierScore, b.BrierScore, 12);
        Assert.Equal(a.MaxCalibrationGap, b.MaxCalibrationGap, 12);
        Assert.Equal(a.Abstain, b.Abstain);
    }
}