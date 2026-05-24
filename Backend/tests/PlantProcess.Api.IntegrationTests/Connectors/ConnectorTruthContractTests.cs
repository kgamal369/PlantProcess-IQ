using System.Net;
using System.Net.Http.Json;
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
    public async Task Connector_provider_types_should_return_single_truth_catalog()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/admin/connectors/provider-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providers = await response.Content.ReadFromJsonAsync<List<ConnectorProviderResponse>>();

        providers.Should().NotBeNull();
        providers!.Should().NotBeEmpty();

        providers.Select(x => x.ProviderType)
            .Should()
            .OnlyHaveUniqueItems("provider types must not be duplicated between stale DTO arrays and new catalog source");
    }

    [Fact]
    public async Task Csv_and_excel_should_be_available_now_because_smoke_tests_exist()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var providers = await GetProvidersAsync(client);

        providers.Single(x => x.ProviderType == "Csv")
            .IsAvailableNow
            .Should()
            .BeTrue("CSV connector is implemented and smoke-tested");

        providers.Single(x => x.ProviderType == "Excel")
            .IsAvailableNow
            .Should()
            .BeTrue("Excel connector is implemented and ExcelConnectorSmokeTests prove file connection and sheet discovery");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("MySql")]
    [InlineData("Oracle")]
    [InlineData("PostgreSql")]
    [InlineData("RestApi")]
    [InlineData("OpcUaHistorian")]
    public async Task Untested_or_not_demo_ready_connectors_should_remain_planned(string providerType)
    {
        using var client = await CreateAuthenticatedClientAsync();

        var providers = await GetProvidersAsync(client);

        providers.Single(x => x.ProviderType == providerType)
            .IsAvailableNow
            .Should()
            .BeFalse($"{providerType} must stay Planned until it is intentionally certified for customer demo availability");
    }

    [Fact]
    public async Task Provider_catalog_should_expose_honest_capabilities()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var providers = await GetProvidersAsync(client);

        var csv = providers.Single(x => x.ProviderType == "Csv");
        csv.RequiresSecretReference.Should().BeFalse();
        csv.SupportsSchemaDiscovery.Should().BeTrue();
        csv.SupportsSnapshotImport.Should().BeTrue();
        csv.SupportsIncrementalImport.Should().BeFalse();

        var excel = providers.Single(x => x.ProviderType == "Excel");
        excel.RequiresSecretReference.Should().BeFalse();
        excel.SupportsSchemaDiscovery.Should().BeTrue();
        excel.SupportsSnapshotImport.Should().BeTrue();
        excel.SupportsIncrementalImport.Should().BeFalse();

        var oracle = providers.Single(x => x.ProviderType == "Oracle");
        oracle.RequiresSecretReference.Should().BeTrue();
        oracle.SupportsSchemaDiscovery.Should().BeTrue();
        oracle.SupportsSnapshotImport.Should().BeTrue();
        oracle.SupportsIncrementalImport.Should().BeTrue();
    }

    private static async Task<List<ConnectorProviderResponse>> GetProvidersAsync(HttpClient client)
    {
        var response = await client.GetAsync("/admin/connectors/provider-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providers = await response.Content.ReadFromJsonAsync<List<ConnectorProviderResponse>>();

        providers.Should().NotBeNull();
        providers!.Should().NotBeEmpty();

        return providers;
    }

    private sealed record ConnectorProviderResponse(
        string ProviderType,
        string DisplayName,
        string Description,
        bool IsAvailableNow,
        bool RequiresSecretReference,
        bool SupportsSchemaDiscovery,
        bool SupportsSnapshotImport,
        bool SupportsIncrementalImport);
}