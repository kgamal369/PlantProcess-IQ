using FluentAssertions;
using PlantProcess.Application;

namespace PlantProcess.Application.UnitTests;

public sealed class ApplicationTestEnvironmentTests
{
    [Fact]
    public void Application_test_project_should_be_available()
    {
        typeof(ApplicationAssemblyMarker)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("PlantProcess.Application");
    }
}
