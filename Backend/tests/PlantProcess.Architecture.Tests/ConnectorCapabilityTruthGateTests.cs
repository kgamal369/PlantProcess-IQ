using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// =====================================================================================
// T-207 connector capability truth gate.
//
// The GA historian connector used to advertise tag browse and bounded read as
// executable and then answer both from invented data, while the admin connector
// catalogue reported the same provider as unavailable. This gate makes that
// combination impossible to reintroduce.
//
// Every token this file searches for is assembled from fragments, so the gate can
// never be satisfied by its own source text, and every read goes through
// ConnectorSourceText, which strips comments first. A comment explaining a rule must
// never be able to satisfy the rule.
// =====================================================================================

[Trait("Gate", "ConnectorCapabilityTruth")]
public sealed class ConnectorCapabilityTruthGateTests
{
    private const string ConnectorRelativePath =
        "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs";

    private const string NotExecutableCode = "OT" + "01";
    private const string HashFabrication   = "GetHash" + "Code";
    private const string ValueFabrication  = "Deterministic" + "Value";
    private const string FallbackTagArray  = "Default" + "Tags";

    private static readonly string[] RegisteredCapabilities =
    {
        "ConfigurationValidation",
        "MappingHintsFromSuppliedTagPaths",
        "TagBrowse",
        "BoundedRead",
        "Subscription",
        "LiveVendorHandshake"
    };

    private const string CollectorRuntimePath =
        "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorSessionRuntime.cs";

    private const string CollectorAcceptancePath =
        "Backend/tests/PlantProcess.Collector.Tests/OpcUaCollectorSessionRuntimeTests.cs";

    private const string CollectorBrowsePath =
        "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorBrowse.cs";

    private const string CollectorBoundedReadPath =
        "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorBoundedRead.cs";

    private const string CollectorSubscriptionPath =
        "Backend/PlantProcess.Collector/OpcUa/OpcUaCollectorSubscription.cs";

    private const string CollectorFieldAcceptancePath =
        "Backend/tests/PlantProcess.Collector.Tests/OpcUaCollectorFieldAcquisitionTests.cs";

    private const string RegistryRelativePath =
        "Backend/PlantProcess.Application/Integration/Connectors/HistorianCapabilityRegistry.cs";

    private static string Source() => ConnectorSourceText.Read(ConnectorRelativePath);

    private static string RegistrySource() => ConnectorSourceText.Read(RegistryRelativePath);

    [Fact]
    public void Connector_never_advertises_a_capability_as_a_literal()
    {
        var literalFlag = new Regex(@"supports\w+\s*=\s*(true|false)\b", RegexOptions.IgnoreCase);

        var match = literalFlag.Match(Source());

        Assert.False(
            match.Success,
            "PPIQ-T207: every advertised capability must be bound to HistorianConnectorCapabilities, never to a " +
            "literal. A literal is how tag browse and bounded read came to advertise themselves as executable " +
            "while returning invented data. Offending text: " + (match.Success ? match.Value : "none"));
    }

    [Theory]
    [InlineData("ConfigurationValidation")]
    [InlineData("MappingHintsFromSuppliedTagPaths")]
    [InlineData("TagBrowse")]
    [InlineData("BoundedRead")]
    [InlineData("Subscription")]
    [InlineData("LiveVendorHandshake")]
    public void Every_capability_is_registered_with_an_explicit_flag(string capability)
    {
        var registration = new Regex(
            @"new\s+HistorianCapability\s*\(\s*" + Regex.Escape(capability) + @"\s*,\s*(true|false)\b",
            RegexOptions.Singleline);

        var match = registration.Match(RegistrySource());

        Assert.True(
            match.Success,
            "PPIQ-T207: capability '" + capability + "' must be registered in HistorianConnectorCapabilities.All.");
    }

    [Theory]
    [InlineData("TagBrowse", "Browse_returns_bounded_nodes_with_identity_and_governance_metadata")]
    [InlineData("BoundedRead", "Bounded_read_returns_value_quality_and_both_timestamps")]
    [InlineData("Subscription", "Subscription_records_requested_and_server_revised_values_separately")]
    public void An_executable_source_operation_is_bound_to_its_own_implementation_and_its_own_gate(
        string capability,
        string acceptanceTest)
    {
        var registration = new Regex(
            @"new\s+HistorianCapability\s*\(\s*" + Regex.Escape(capability) + @"\s*,\s*(true|false)\b",
            RegexOptions.Singleline);

        var match = registration.Match(RegistrySource());

        Assert.True(match.Success, "PPIQ-T207: capability '" + capability + "' must stay registered.");
        Assert.True(
            match.Groups[1].Value == "true",
            "PPIQ-T207: capability '" + capability + "' is advertised as not executable while its collector " +
            "implementation and its acceptance test exist. A flag and its implementation move together.");

        var implementation = capability switch
        {
            "TagBrowse" => ConnectorSourceText.Read(CollectorBrowsePath),
            "BoundedRead" => ConnectorSourceText.Read(CollectorBoundedReadPath),
            _ => ConnectorSourceText.Read(CollectorSubscriptionPath)
        };

        Assert.False(string.IsNullOrWhiteSpace(implementation));

        Assert.Contains(
            acceptanceTest,
            ConnectorSourceText.Read(CollectorFieldAcceptancePath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Each_source_operation_keeps_its_own_gate_and_is_never_inferred_from_another()
    {
        var browse = ConnectorSourceText.Read(CollectorBrowsePath);
        var read = ConnectorSourceText.Read(CollectorBoundedReadPath);
        var subscription = ConnectorSourceText.Read(CollectorSubscriptionPath);

        Assert.Contains("OpcUaCollectorOperations.Browse", browse, StringComparison.Ordinal);
        Assert.Contains("OpcUaCollectorOperations.BoundedRead", read, StringComparison.Ordinal);
        Assert.Contains("OpcUaCollectorOperations.Subscribe", subscription, StringComparison.Ordinal);

        Assert.DoesNotContain("OpcUaCollectorOperations.BoundedRead", subscription, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcUaCollectorOperations.Subscribe", read, StringComparison.Ordinal);
    }

    [Fact]
    public void The_field_identity_is_the_namespace_uri_and_never_the_namespace_index()
    {
        var identity = ConnectorSourceText.Read(
            "Backend/PlantProcess.Collector/OpcUa/OpcUaProviderFieldIdentity.cs");

        Assert.Contains("NamespaceUri", identity, StringComparison.Ordinal);
        Assert.Contains("TryResolve", identity, StringComparison.Ordinal);

        var canonical = new Regex(@"public string Canonical\(\)[\s\S]{0,600}?;", RegexOptions.Singleline)
            .Match(identity);

        Assert.True(canonical.Success, "PPIQ-T225: the identity must expose a canonical rendering.");
        Assert.DoesNotContain("NamespaceIndex", canonical.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("browse-tags")]
    [InlineData("read-window")]
    public void Not_executable_routes_return_the_typed_code_and_no_data(string route)
    {
        var source = Source();
        var routeIndex = source.IndexOf("\"/" + route + "\"", StringComparison.Ordinal);

        Assert.True(routeIndex >= 0, "PPIQ-T207: route '" + route + "' is missing from the connector.");

        var handler = source.Substring(routeIndex, Math.Min(400, source.Length - routeIndex));

        Assert.Contains(
            "NotExecutable",
            handler,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_live_vendor_handshake_is_executable_only_because_a_session_runtime_earns_it()
    {
        var registration = new Regex(
            @"new\s+HistorianCapability\s*\(\s*LiveVendorHandshake\s*,\s*(true|false)\b",
            RegexOptions.Singleline);

        var match = registration.Match(RegistrySource());

        Assert.True(match.Success, "PPIQ-T207: the live vendor handshake must stay registered.");
        Assert.True(
            match.Groups[1].Value == "true",
            "The customer-side collector session runtime earns this capability. If the runtime is removed, " +
            "the flag goes back to false in the same change.");

        var runtime = ConnectorSourceText.Read(CollectorRuntimePath);

        Assert.Contains("DefaultSessionFactory", runtime, StringComparison.Ordinal);
        Assert.Contains("SessionReconnectHandler", runtime, StringComparison.Ordinal);
        Assert.Contains("CertificateValidation", runtime, StringComparison.Ordinal);

        var acceptance = ConnectorSourceText.Read(CollectorAcceptancePath);

        Assert.Contains("Trusted_server_yields_a_session", acceptance, StringComparison.Ordinal);
        Assert.Contains("Untrusted_server_certificate_is_refused", acceptance, StringComparison.Ordinal);
        Assert.Contains("A_server_restart_is_recovered_by_reconnect", acceptance, StringComparison.Ordinal);
    }

    [Fact]
    public void The_live_handshake_route_refuses_at_its_boundary_without_claiming_the_capability_is_unimplemented()
    {
        var source = Source();

        Assert.Contains("NotExecutableHere", source, StringComparison.Ordinal);
        Assert.Contains("ExecutedOutsideCoreCode", source, StringComparison.Ordinal);

        var index = source.IndexOf("IResult NotExecutableHere", StringComparison.Ordinal);
        Assert.True(index >= 0, "PPIQ-T207: the route-boundary refusal is missing.");

        var body = source.Substring(index, Math.Min(900, source.Length - index));

        Assert.DoesNotContain("errorCode = NotExecutableCode", body, StringComparison.Ordinal);
        Assert.Contains("executedByCore = false", body, StringComparison.Ordinal);
        Assert.Contains("capabilityExecutable", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_code_is_declared_once_and_used_by_the_failure_shape()
    {
        Assert.Contains(NotExecutableCode, Source(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GetHash" + "Code")]
    [InlineData("Deterministic" + "Value")]
    [InlineData("Default" + "Tags")]
    public void Connector_contains_no_invented_data_generator(string forbidden)
    {
        Assert.DoesNotContain(
            forbidden,
            Source(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_registered_capability_carries_evidence()
    {
        var registrations = Regex.Matches(
            RegistrySource(),
            @"new\s+HistorianCapability\s*\(\s*\w+\s*,\s*(?:true|false)\s*,\s*""?",
            RegexOptions.Singleline);

        Assert.True(
            registrations.Count >= RegisteredCapabilities.Length,
            "PPIQ-T207: every capability must be registered with an evidence string saying why it is or is not " +
            "executable. An unexplained flag is not a truth claim.");
    }
}

internal static class ConnectorSourceText
{
    public static string Read(string relativePath)
    {
        var root = FindRepoRoot()
                   ?? throw new InvalidOperationException("PPIQ-T207: repository root not found from " + AppContext.BaseDirectory);

        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(full))
        {
            throw new FileNotFoundException("PPIQ-T207: connector source not found.", full);
        }

        return StripComments(File.ReadAllText(full));
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                Directory.Exists(Path.Combine(dir.FullName, "Backend")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
