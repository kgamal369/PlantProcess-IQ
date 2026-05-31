using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.OpenApi;

public sealed class OpenApiMlAndDynamicEndpointContractTests : AuthenticatedApiTestBase
{
    public OpenApiMlAndDynamicEndpointContractTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Swagger_document_should_include_ml_learning_and_dynamic_page_surfaces()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.TryGetProperty("paths", out var paths)
            .Should()
            .BeTrue();

        var pathNames = paths.EnumerateObject()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        pathNames.Should().Contain("/api/ml/learning/status");
        pathNames.Should().Contain("/api/ml/learning/jobs");
        pathNames.Should().Contain("/api/ml/learning/results");
        pathNames.Should().Contain("/api/ml/providers/narrative/proof");
        pathNames.Should().Contain("/api/suggestions");
        pathNames.Should().Contain("/api/pages/{slug}");
    }
}
