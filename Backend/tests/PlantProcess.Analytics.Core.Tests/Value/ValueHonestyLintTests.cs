using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

public class ValueHonestyLintTests
{
    [Theory]
    [InlineData("This change is guaranteed to help")]
    [InlineData("It is GUARANTEED")]
    [InlineData("This will save you money")]
    [InlineData("This will   save money")]   // spacing variant
    [InlineData("This Will Save money")]      // casing variant
    public void Forbidden_phrases_are_rejected(string text)
    {
        var result = ValueHonestyLint.Validate(text);
        Assert.False(result.IsClean);
        Assert.NotEmpty(result.Violations);
    }

    [Theory]
    [InlineData("Estimated impact range based on your assumptions.")]
    [InlineData("This may reduce downgrades; figures are a range, not a promise.")]
    [InlineData("")]
    public void Compliant_text_passes(string text)
    {
        Assert.True(ValueHonestyLint.Validate(text).IsClean);
    }
}