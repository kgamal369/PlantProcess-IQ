// PPIQ-T14 - the headline paywalls must stay wired. Each entry pins a source file to
// the RequireLicenseFeature call it must carry; removing a paywall (accidentally or
// during a refactor shim rewrite - see the P2-T010 virtualization loss) turns this red.
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Task", "T14")]
public sealed class T14LicenseEnforcementGuardTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "Backend"))) d = d.Parent!;
        return d!.FullName;
    }

    public static IEnumerable<object[]> Wiring => new[]
    {
        // T14 group wiring
        new object[] { @"Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs",          "DbLinkConfiguration" },
        new object[] { @"Backend/PlantProcess.Api/Endpoints/Admin/ConnectorSchemaDriftEndpoints.cs",    "DbLinkConfiguration" },
        new object[] { @"Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs",     "SchemaSqlViewBuilder" },
        new object[] { @"Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs", "InvestigationWorkflow" },
    };

    [Theory]
    [MemberData(nameof(Wiring))]
    public void Headline_endpoint_group_carries_its_license_paywall(string relativePath, string feature)
    {
        var path = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"PPIQ-T14: wired file moved: {relativePath}");
        Assert.Contains($"RequireLicenseFeature(LicenseFeature.{feature}", File.ReadAllText(path));
    }

    [Fact]
    public void The_three_original_feature_filters_are_still_wired()
    {
        var sources = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "Backend", "PlantProcess.Api"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .ToList();

        foreach (var feature in new[] { "MlWorkspacePreview", "RiskDashboardView", "DataQualityFullScan" })
        {
            Assert.True(sources.Any(s => s.Contains($"RequireLicenseFeature(LicenseFeature.{feature}")),
                $"PPIQ-T14: pre-existing paywall for {feature} disappeared.");
        }
    }
}