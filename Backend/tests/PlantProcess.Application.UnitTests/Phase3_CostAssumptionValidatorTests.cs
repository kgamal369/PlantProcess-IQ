using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase3_CostAssumptionValidatorTests
{
    [Fact]
    public void Validate_AcceptsCompleteNonNegativeBands()
    {
        var set = new CostAssumptionSet(1, "EUR",
            new CostBand(10m, 20m, 30m), new CostBand(0m, 0m, 0m), null,
            new CostBand(1m, 1m, 1m), null, null);

        Assert.Empty(CostAssumptionValidator.Validate(set));
    }

    [Fact]
    public void Validate_RejectsNegativeValues()
    {
        var set = new CostAssumptionSet(1, "EUR",
            new CostBand(-1m, 20m, 30m), null, null, null, null, null);

        var errors = CostAssumptionValidator.Validate(set);
        Assert.Contains(errors, e => e.Contains("costPerTon") && e.Contains("non-negative"));
    }

    [Fact]
    public void Validate_RejectsOutOfOrderBand()
    {
        var set = new CostAssumptionSet(1, "EUR",
            null, new CostBand(50m, 20m, 30m), null, null, null, null);

        var errors = CostAssumptionValidator.Validate(set);
        Assert.Contains(errors, e => e.Contains("downgradeDeltaPerTon") && e.Contains("low <= mid <= high"));
    }
}