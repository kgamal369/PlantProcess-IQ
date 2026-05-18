using FluentAssertions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.IntegrationTests;

public sealed class InfrastructureTestEnvironmentTests
{
    [Fact]
    public void Infrastructure_test_project_should_reference_db_context()
    {
        typeof(PlantProcessDbContext)
            .Assembly
            .GetName()
            .Name
            .Should()
            .Be("PlantProcess.Infrastructure");
    }
}
