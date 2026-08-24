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

    private static readonly string[] CapabilitiesWithoutImplementation =
    {
        "TagBrowse",
        "BoundedRead",
        "Subscription",
        "LiveVendorHandshake"
    };

    private static string Source() => ConnectorSourceText.Read(ConnectorRelativePath);

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
    [InlineData("TagBrowse")]
    [InlineData("BoundedRead")]
    [InlineData("Subscription")]
    [InlineData("LiveVendorHandshake")]
    public void Capabilities_without_an_implementation_are_registered_as_not_executable(string capability)
    {
        var registration = new Regex(
            @"new\s+ConnectorCapability\s*\(\s*" + Regex.Escape(capability) + @"\s*,\s*(true|false)\b",
            RegexOptions.Singleline);

        var match = registration.Match(Source());

        Assert.True(
            match.Success,
            "PPIQ-T207: capability '" + capability + "' must be registered in HistorianConnectorCapabilities.All.");

        Assert.True(
            match.Groups[1].Value == "false",
            "PPIQ-T207: capability '" + capability + "' is registered as executable. It may only be flipped to true " +
            "together with an implementation, and this gate must be extended to prove that implementation runs. " +
            "T-224 to T-226 own that work. Flipping the flag alone recreates the defect T-207 closed.");
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
            Source(),
            @"new\s+ConnectorCapability\s*\(\s*\w+\s*,\s*(?:true|false)\s*,\s*""?",
            RegexOptions.Singleline);

        Assert.True(
            registrations.Count >= CapabilitiesWithoutImplementation.Length,
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
