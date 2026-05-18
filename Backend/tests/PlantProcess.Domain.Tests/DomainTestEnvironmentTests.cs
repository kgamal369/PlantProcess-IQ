using FluentAssertions;

namespace PlantProcess.Domain.Tests;

public sealed class DomainTestEnvironmentTests
{
    [Fact]
    public void Domain_test_project_should_be_available()
    {
        typeof(PlantProcess.Domain.Common.BaseEntity)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("PlantProcess.Domain");
    }
}
