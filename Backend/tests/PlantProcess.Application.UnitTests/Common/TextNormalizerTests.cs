using FluentAssertions;
using PlantProcess.Application.Common.Text;

namespace PlantProcess.Application.UnitTests.Common;

public sealed class TextNormalizerTests
{
    [Fact]
    public void RequiredTrim_should_trim_value()
    {
        var result = TextNormalizer.RequiredTrim("  ABC  ", "Code");

        result.Should().Be("ABC");
    }

    [Fact]
    public void RequiredTrim_should_throw_when_value_is_empty()
    {
        var act = () => TextNormalizer.RequiredTrim("   ", "Code");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Code is required*");
    }

    [Fact]
    public void OptionalTrim_should_return_null_for_empty_value()
    {
        var result = TextNormalizer.OptionalTrim("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeCode_should_trim_and_uppercase()
    {
        var result = TextNormalizer.NormalizeCode("  defect_a  ", "DefectCode");

        result.Should().Be("DEFECT_A");
    }
}
