// T-207 corrective gate. Connector truth, held at source level so it cannot drift back.
//
// The three defects this exists to prevent all shipped once: a second provider list that
// disagreed with the one the customer sees, providers advertised as creatable with no
// runtime behind them, and connector metadata claiming capabilities the routes refuse.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Gate", "ConnectorProviderImplementation")]
[Trait("BacklogTask", "T-207")]
public sealed class ConnectorProviderImplementationGateTests
{
    private const string CatalogPath =
        "Backend/PlantProcess.Application/Integration/Connectors/ConnectorProviderCatalog.cs";

    private const string ServiceProviderListPath =
        "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.ProviderTypes.001.GetProviderTypes.cs";

    private const string FactoryPath =
        "Backend/PlantProcess.Infrastructure/Connectors/Common/DataSourceConnectorFactory.cs";

    private const string HistorianConnectorPath =
        "Backend/PlantProcess.Infrastructure/Connectors/Historian/OpcUaHistorianConnector.cs";

    private const string HistorianEndpointsPath =
        "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs";

    private const string RegistryPath =
        "Backend/PlantProcess.Application/Integration/Connectors/HistorianCapabilityRegistry.cs";

    [Fact]
    public void Exactly_one_place_produces_the_provider_list()
    {
        var service = Read(ServiceProviderListPath);

        Assert.DoesNotContain("new ProviderTypeDto(", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportsSchemaDiscovery:", service, StringComparison.Ordinal);
        Assert.Contains("ConnectorProviderCatalog.GetProviderTypes()", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_provider_declared_implemented_has_a_runtime_behind_it()
    {
        var factory = Read(FactoryPath).ToLowerInvariant();

        foreach (var provider in DeclaredImplementedProviders())
        {
            Assert.True(
                factory.Contains("\"" + provider.ToLowerInvariant() + "\"", StringComparison.Ordinal),
                "The catalogue declares " + provider + " implemented, but the connector factory has no case for it. "
                    + "A provider a customer can select and nothing can open is worse than one that is absent.");
        }
    }

    [Fact]
    public void A_provider_with_no_runtime_is_not_declared_implemented()
    {
        var implemented = DeclaredImplementedProviders();

        foreach (var roadmap in new[] { "Sap", "RestApi" })
        {
            Assert.DoesNotContain(roadmap, implemented, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_implemented_set_is_not_empty()
    {
        // A gate that matches nothing passes for the wrong reason.
        Assert.NotEmpty(DeclaredImplementedProviders());
    }

    [Fact]
    public void The_historian_connector_reads_its_capabilities_and_declares_none_itself()
    {
        var connector = Read(HistorianConnectorPath);

        Assert.Contains("HistorianCapabilityRegistry", connector, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"supportsTagBrowse\"] = \"true\"", connector, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"supportsBoundedRead\"] = \"true\"", connector, StringComparison.Ordinal);
    }

    [Fact]
    public void The_endpoint_capabilities_come_from_the_same_registry()
    {
        var endpoints = Read(HistorianEndpointsPath);

        Assert.Contains("HistorianCapabilityRegistry", endpoints, StringComparison.Ordinal);
    }

    [Fact]
    public void Browse_bounded_read_and_subscription_are_declared_executable_only_with_a_collector_runtime()
    {
        var registry = Read(RegistryPath);

        foreach (var capability in new[] { "TagBrowse", "BoundedRead", "Subscription" })
        {
            var match = Regex.Match(registry, capability + @",\s*(true|false)");
            Assert.True(match.Success, "The registry no longer declares " + capability + ".");
            Assert.Equal("true", match.Groups[1].Value);
        }

        foreach (var implementation in new[]
        {
            "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorBrowse.cs",
            "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorBoundedRead.cs",
            "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorSubscription.cs"
        })
        {
            Assert.False(
                string.IsNullOrWhiteSpace(Read(implementation)),
                "The declared capability needs its collector implementation: " + implementation);
        }
    }

    private static IReadOnlyList<string> DeclaredImplementedProviders()
    {
        var catalog = Read(CatalogPath);
        var block = Regex.Match(catalog, @"ImplementedProviderTypes\s*=\s*new\(?[^{]*\{([^}]*)\}", RegexOptions.Singleline);

        Assert.True(block.Success, "The catalogue no longer declares which providers have a runtime implementation.");

        var providers = new List<string>();
        foreach (Match literal in Regex.Matches(block.Groups[1].Value, "\"([A-Za-z0-9_]+)\""))
        {
            providers.Add(literal.Groups[1].Value);
        }

        return providers;
    }

    private static string Read(string relativePath)
    {
        var root = FindRepositoryRoot();
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(full), "Expected source file is missing: " + relativePath);
        return File.ReadAllText(full);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}
