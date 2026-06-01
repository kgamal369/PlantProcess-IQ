using System.Linq;
using PlantProcess.Analytics.Core.Primitives;
using Xunit;
namespace PlantProcess.Analytics.Core.Tests;
// T-026 / T-029: golden-dataset exactness + metadata completeness + edge cases.
public sealed class P05_PrimitivesTests
{
private static readonly double[] Fixed = { 10, 20, 30, 40, 50 };
[Fact] public void Count_is_exact() => Assert.Equal(5, (int)SimpleAnalysis.Count(Ctx.N(Fixed), Ctx.Make()).Value!.Value);
[Fact] public void Sum_is_exact() => Assert.Equal(150.0, SimpleAnalysis.SumOf(Ctx.N(Fixed), Ctx.Make()).Value!.Value, 9);
[Fact] public void Average_is_exact() => Assert.Equal(30.0, SimpleAnalysis.Average(Ctx.N(Fixed), Ctx.Make()).Value!.Value, 9);
[Fact] public void Min_is_exact() => Assert.Equal(10.0, SimpleAnalysis.Min(Ctx.N(Fixed), Ctx.Make()).Value!.Value, 9);
[Fact] public void Max_is_exact() => Assert.Equal(50.0, SimpleAnalysis.Max(Ctx.N(Fixed), Ctx.Make()).Value!.Value, 9);
[Fact] public void Median_is_exact() => Assert.Equal(30.0, SimpleAnalysis.MedianOf(Ctx.N(Fixed), Ctx.Make()).Value!.Value, 9);

[Fact]
public void Sample_stdev_matches_hand_computed()
{
    // variance = (400+100+0+100+400)/4 = 250 ; std = 15.811388300841896
    var r = SimpleAnalysis.StdDev(Ctx.N(Fixed), Ctx.Make());
    Assert.Equal(AnalysisStatus.Ok, r.Status);
    Assert.Equal(15.811388, r.Value!.Value, 6);
}

[Fact]
public void Ratio_of_sums_is_exact()
{
    var r = SimpleAnalysis.Ratio(Ctx.N(3, 3), Ctx.N(4, 4), Ctx.Make()); // 6/8
    Assert.Equal(0.75, r.Value!.Value, 9);
}

[Fact]
public void Rate_per_thousand_is_exact()
{
    var r = SimpleAnalysis.Rate(5, 1000, 1000, Ctx.Make());
    Assert.Equal(5.0, r.Value!.Value, 9);
}

[Fact]
public void Trend_recovers_unit_slope_and_direction()
{
    var r = SimpleAnalysis.Trend(Ctx.N(1, 2, 3, 4, 5), Ctx.Make());
    Assert.Equal(1.0, r.Value!.Value, 9);
    Assert.Equal("Up", r.Label);
}

[Fact]
public void Threshold_flags_breach_and_ok()
{
    Assert.Equal("Breach", SimpleAnalysis.Threshold(80, 90, ThresholdMode.LowerBoundFloor, Ctx.Make()).Label);
    Assert.Equal("OK", SimpleAnalysis.Threshold(95, 90, ThresholdMode.LowerBoundFloor, Ctx.Make()).Label);
}

[Fact]
public void Distribution_quantiles_are_exact()
{
    var r = SimpleAnalysis.Distribution(Ctx.N(Fixed), Ctx.Make());
    Assert.Equal(20.0, r.Extras!["p25"], 9);
    Assert.Equal(30.0, r.Extras!["p50"], 9);
    Assert.Equal(40.0, r.Extras!["p75"], 9);
}

[Fact]
public void Comparison_difference_and_percent_are_exact()
{
    var r = SimpleAnalysis.Comparison(Ctx.N(10, 20, 30), Ctx.N(40, 50, 60), Ctx.Make()); // 20 vs 50
    Assert.Equal(-30.0, r.Value!.Value, 9);
    Assert.Equal(-60.0, r.Extras!["percentChange"], 9);
}

[Theory]
[InlineData(95, "Green")]
[InlineData(75, "Amber")]
[InlineData(50, "Red")]
public void Status_rag_is_correct(double value, string expected)
    => Assert.Equal(expected, SimpleAnalysis.Status(value, 90, 70, Ctx.Make()).Label);

[Fact]
public void Every_result_carries_complete_metadata()
{
    var r = SimpleAnalysis.Average(Ctx.N(Fixed), Ctx.Make());
    Assert.True(r.MetadataComplete);
    Assert.Equal(5, r.Metadata.SampleSize);
    Assert.Equal("units", r.Metadata.Unit);
}

[Fact]
public void Nulls_are_filtered_and_counted()
{
    var r = SimpleAnalysis.Average(new double?[] { 10, null, 30 }, Ctx.Make());
    Assert.Equal(20.0, r.Value!.Value, 9);
    Assert.Equal(2, r.Metadata.SampleSize);
}

[Fact]
public void Empty_and_single_and_zero_denominator_never_throw()
{
    Assert.Equal(AnalysisStatus.InsufficientData, SimpleAnalysis.Average(new double?[0], Ctx.Make()).Status);
    Assert.Equal(AnalysisStatus.InsufficientData, SimpleAnalysis.StdDev(Ctx.N(42), Ctx.Make()).Status);
    Assert.Equal(AnalysisStatus.InsufficientData, SimpleAnalysis.Ratio(Ctx.N(1, 2), Ctx.N(0), Ctx.Make()).Status);
    Assert.Equal(0, (int)SimpleAnalysis.Count(new double?[0], Ctx.Make()).Value!.Value);
}
}