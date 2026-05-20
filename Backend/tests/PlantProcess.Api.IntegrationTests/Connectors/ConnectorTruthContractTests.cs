using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using PlantProcess.Api.IntegrationTests.Infrastructure;

namespace PlantProcess.Api.IntegrationTests.Connectors;

public sealed class ConnectorTruthContractTests : AuthenticatedApiTestBase
{
    public ConnectorTruthContractTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Connector_provider_types_should_return_provider_list()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/connectors/provider-types");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("Csv");
        json.Should().Contain("Excel");
    }

    [Fact]
    public async Task Connector_provider_types_should_not_mark_unimplemented_enterprise_connectors_as_available_now()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/connectors/provider-types");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var providers = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : document.RootElement.TryGetProperty("providers", out var providersElement)
                ? providersElement.EnumerateArray().ToList()
                : new List<JsonElement>();

        providers.Should().NotBeEmpty("connector provider type endpoint must return provider definitions");

        foreach (var provider in providers)
        {
            var providerType = GetStringProperty(provider, "providerType")
                ?? GetStringProperty(provider, "ProviderType")
                ?? string.Empty;

            var isAvailableNow =
                GetBooleanProperty(provider, "isAvailableNow")
                ?? GetBooleanProperty(provider, "IsAvailableNow")
                ?? false;

            if (providerType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("MSSQL", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("Oracle", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("MySql", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("MySQL", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("OpcUa", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("Historian", StringComparison.OrdinalIgnoreCase))
            {
                isAvailableNow
                    .Should()
                    .BeFalse($"{providerType} must remain Planned until a tested implementation exists");
            }
        }
    }

    [Fact]
    public async Task Csv_and_excel_provider_types_should_be_visible_for_demo_import_story()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/connectors/provider-types");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("Csv");
        json.Should().Contain("CSV Snapshot");

        json.Should().Contain("Excel");
        json.Should().Contain("Excel Snapshot");
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool? GetBooleanProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;
    }
}