// T-106 B2.3b tests: one validation authority for every schedule write path.
using PlantProcess.Application.Jobs.Scheduling;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Scheduling;

[Trait("BacklogTask", "T-106")]
public sealed class JobScheduleWriteValidationTests
{
    [Theory]
    [InlineData("Every 2 minutes")]
    [InlineData("Every 15 minutes")]
    [InlineData("Daily 02:00")]
    [InlineData("Daily 02:30")]
    [InlineData("Daily 03:00")]
    [InlineData("Weekly Sunday 03:00")]
    public void Every_schedule_this_product_already_writes_is_accepted(string seed)
    {
        Assert.Null(JobScheduleWriteValidation.Validate(seed));
    }

    [Theory]
    [InlineData("*/15 * * * *")]
    [InlineData("0 2 * * *")]
    [InlineData("PT5M")]
    [InlineData("Manual")]
    public void Canonical_expressions_are_accepted(string expression)
    {
        Assert.Null(JobScheduleWriteValidation.Validate(expression));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_absent_schedule_is_still_refused_with_the_accepted_wording(string expression)
    {
        var error = JobScheduleWriteValidation.Validate(expression);
        Assert.NotNull(error);
        Assert.Equal("Schedule expression is required.", error!.Message);
    }

    [Fact]
    public void A_null_schedule_is_refused_with_the_same_wording()
    {
        var error = JobScheduleWriteValidation.Validate(null);
        Assert.NotNull(error);
        Assert.Equal("Schedule expression is required.", error!.Message);
    }

    [Theory]
    [InlineData("whenever")]
    [InlineData("soon")]
    [InlineData("Daily 25:00")]
    [InlineData("Weekly Funday 03:00")]
    [InlineData("Every 0 minutes")]
    public void A_string_nothing_can_execute_is_refused_and_says_why(string expression)
    {
        var error = JobScheduleWriteValidation.Validate(expression);
        Assert.NotNull(error);
        Assert.Contains("not executable", error!.Message, System.StringComparison.Ordinal);
    }
}
