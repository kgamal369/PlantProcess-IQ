using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.OpenApi;

public sealed class OpenApiContractTests : AuthenticatedApiTestBase
{
    public OpenApiContractTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Swagger_document_should_be_generated_in_development_or_test_mode()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        document.RootElement.TryGetProperty("paths", out var paths)
            .Should()
            .BeTrue("Swagger document must contain paths");

        paths.EnumerateObject()
            .Should()
            .NotBeEmpty("Swagger paths must not be empty");
    }

    [Fact]
    public async Task Swagger_document_should_include_critical_endpoint_groups()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("health");
        json.Should().Contain("dashboard");
        json.Should().Contain("admin");
        json.Should().Contain("quality");
        json.Should().Contain("material");
    }
}