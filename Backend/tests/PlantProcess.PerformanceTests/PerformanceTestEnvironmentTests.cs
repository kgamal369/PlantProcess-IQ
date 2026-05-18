using FluentAssertions;

namespace PlantProcess.PerformanceTests;

public sealed class PerformanceTestEnvironmentTests
{
    [Fact]
    public void Performance_test_project_should_run()
    {
        true.Should().BeTrue();
    }
}
