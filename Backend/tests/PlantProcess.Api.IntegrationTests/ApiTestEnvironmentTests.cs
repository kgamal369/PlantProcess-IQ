using FluentAssertions;

namespace PlantProcess.Api.IntegrationTests;

public sealed class ApiTestEnvironmentTests
{
    [Fact]
    public void Api_integration_test_project_should_run()
    {
        true.Should().BeTrue();
    }
}
