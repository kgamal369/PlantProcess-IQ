using PlantProcess.Application.Integration.Mapping.Faults;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class V2Policy_MappingFaultTests
{
    [Theory]
    [InlineData(MappingFaultKind.NoSuchView)]
    [InlineData(MappingFaultKind.NoSuchColumn)]
    [InlineData(MappingFaultKind.InvalidAggregateForType)]
    [InlineData(MappingFaultKind.AmbiguousJoinKey)]
    public void PPIQ_802_each_fault_yields_a_typed_error_with_view_and_next_step(MappingFaultKind kind)
    {
        var fault = MappingFaultClassifier.Classify(kind, "v_coil_quality");
        Assert.NotNull(fault);
        Assert.Equal(kind, fault!.Kind);
        Assert.Equal("v_coil_quality", fault.AffectedView);
        Assert.False(string.IsNullOrWhiteSpace(fault.NextSafeStep));
    }

    [Fact]
    public void PPIQ_802_none_is_not_a_fault()
    {
        Assert.Null(MappingFaultClassifier.Classify(MappingFaultKind.None, "v_any"));
    }

    [Fact]
    public void PPIQ_802_ambiguous_join_key_exists_as_a_distinct_typed_error()
    {
        var fault = MappingFaultClassifier.Classify(MappingFaultKind.AmbiguousJoinKey, "v_join");
        Assert.NotNull(fault);
        Assert.Contains("join", fault!.NextSafeStep, System.StringComparison.OrdinalIgnoreCase);
    }
}