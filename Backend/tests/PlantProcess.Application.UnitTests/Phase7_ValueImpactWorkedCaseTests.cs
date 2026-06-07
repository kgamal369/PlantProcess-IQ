
using PlantProcess.Application.Analytics.Value;
using PlantProcess.Application.Analytics.Value.Demo;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase7_ValueImpactWorkedCaseTests
{
    [Fact]
    public void T038_Reproduces_Eur28kTo56k_WorkedCase_Exactly()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();

        var result = new ValueImpactEngine().Compute(fixture.Inputs, fixture.Assumptions);

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(28_000m, result.Low);
        Assert.Equal(42_000m, result.Expected);
        Assert.Equal(56_000m, result.High);
        Assert.Equal(fixture.ExpectedLow, result.Low);
        Assert.Equal(fixture.ExpectedMid, result.Expected);
        Assert.Equal(fixture.ExpectedHigh, result.High);
        Assert.Contains("not a guaranteed saving", result.HonestyCaveat);
    }

    [Fact]
    public void T038_WorkedCase_Is_Deterministic_WhenRerun()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();
        var engine = new ValueImpactEngine();

        var first = engine.Compute(fixture.Inputs, fixture.Assumptions);
        var second = engine.Compute(fixture.Inputs, fixture.Assumptions);

        Assert.Equal(first.Low, second.Low);
        Assert.Equal(first.Expected, second.Expected);
        Assert.Equal(first.High, second.High);
        Assert.Equal(first.Terms.Count, second.Terms.Count);
        Assert.Equal(first.Terms[0].InputsJson, second.Terms[0].InputsJson);
    }

    [Fact]
    public void T038_Changing_DriverInput_Changes_Range_Traceably()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();

        var changedInputs = fixture.Inputs with
        {
            MonthlyVolumeTons = 12_000m
        };

        var result = new ValueImpactEngine().Compute(changedInputs, fixture.Assumptions);

        Assert.False(result.IsAbstained);
        Assert.Equal(33_600m, result.Low);
        Assert.Equal(50_400m, result.Expected);
        Assert.Equal(67_200m, result.High);

        var defectTerm = Assert.Single(result.Terms, x => x.Name == "DefectDowngradeOrScrap");
        Assert.Contains("\"monthlyVolumeTons\":12000", defectTerm.InputsJson);
        Assert.Contains("\"affectedTons\":240.00", defectTerm.InputsJson);
    }

    [Fact]
    public void T038_Every_WorkedCase_Input_Is_Traceable_To_Provenance()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();

        var result = new ValueImpactEngine().Compute(fixture.Inputs, fixture.Assumptions);

        Assert.All(result.Terms, term =>
        {
            Assert.False(string.IsNullOrWhiteSpace(term.Handle.Id));
            Assert.Equal(fixture.Inputs.FindingRef, term.Handle.Id);
            Assert.Contains(fixture.Inputs.FindingRef, term.InputsJson);
        });

        var defectTerm = Assert.Single(result.Terms, x => x.Name == "DefectDowngradeOrScrap");
        Assert.Contains("downgrade_delta_per_ton", defectTerm.InputsJson);
        Assert.Contains("EDGE_CRACK", defectTerm.InputsJson);
        Assert.Equal(28_000m, defectTerm.Low);
        Assert.Equal(42_000m, defectTerm.Expected);
        Assert.Equal(56_000m, defectTerm.High);
    }
}
