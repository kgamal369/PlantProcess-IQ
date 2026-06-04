using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class P2_CanonicalAnalyticsArchitectureTests
{
    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "PlantProcess.Api")) &&
                Directory.Exists(Path.Combine(dir.FullName, "PlantProcess.Application")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Backend root from test output directory.");
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { BackendRoot() }.Concat(parts).ToArray()));

    private static IEnumerable<string> ReadAllCsFiles(string relativeFolder)
    {
        var folder = Path.Combine(BackendRoot(), relativeFolder);

        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)
            : Array.Empty<string>();
    }

    [Fact]
    public void T010_canonical_engine_registry_has_explicit_default()
    {
        var engine = Read("PlantProcess.Application", "Analytics", "Engines", "CanonicalCorrelationEngine.cs");
        var registry = Read("PlantProcess.Application", "Analytics", "Engines", "CorrelationEngineRegistry.cs");
        var iface = Read("PlantProcess.Application", "Analytics", "Engines", "ICorrelationEngine.cs");

        Assert.Contains("public interface ICorrelationEngine", iface);
        Assert.Contains("Task<AdvancedAnalysisRunResult>", iface);
        Assert.Contains("CanonicalKey = \"canonical\"", engine);
        Assert.Contains("IAdvancedCorrelationService", engine);
        Assert.Contains("Default => _byKey[CanonicalCorrelationEngine.CanonicalKey]", registry);
    }

    [Fact]
    public void T010_result_contract_carries_required_evidence_fields()
    {
        var advancedFiles = string.Join("\n", ReadAllCsFiles(Path.Combine("PlantProcess.Application", "Analytics", "Advanced")));

        Assert.Contains("public sealed record AdvancedAnalysisRunResult", advancedFiles);
        Assert.Contains("public sealed record AdvancedFinding", advancedFiles);
        Assert.Contains("EffectSize", advancedFiles);
        Assert.Contains("QValue", advancedFiles);
        Assert.Contains("StabilityLower", advancedFiles);
        Assert.Contains("StabilityUpper", advancedFiles);
        Assert.Contains("StabilityConsistency", advancedFiles);
        Assert.Contains("SurvivesStratification", advancedFiles);
        Assert.Contains("ProvenanceHandle", advancedFiles);
        Assert.Contains("HonestyCaveat", advancedFiles);
    }

    [Fact]
    public void T011_correlation_api_exposes_canonical_run_endpoint()
    {
        var endpoints = Read("PlantProcess.Api", "Endpoints", "Analytics", "CorrelationEndpoints.cs");

        Assert.Contains("/canonical/run", endpoints);
        Assert.Contains("RunCanonicalCorrelationAsync", endpoints);
        Assert.Contains("ICorrelationEngineRegistry", endpoints);
        Assert.Contains("AdvancedAnalysisRequest", endpoints);
        Assert.Contains("honestyCaveat", endpoints);
    }

    [Fact]
    public void T012_legacy_correlation_service_is_obsolete_for_inferential_claims()
    {
        var service = Read("PlantProcess.Application", "Analytics", "Services", "CorrelationService.cs");

        Assert.Contains("Obsolete", service);
        Assert.Contains("PPIQ-T012", service);
    }

    [Fact]
    public void T013_narrative_provider_contract_exists_and_strategies_are_present()
    {
        var iface = Read("PlantProcess.Application", "Analytics", "Interfaces", "INarrativeProvider.cs");
        var services = string.Join("\n", ReadAllCsFiles(Path.Combine("PlantProcess.Application", "Analytics", "Services")));

        Assert.Contains("interface INarrativeProvider", iface);
        Assert.Contains("ProviderKey", iface);
        Assert.Contains("LocalNarrativeProvider", services);
        Assert.Contains("ApiNarrativeProvider", services);
        Assert.Contains("ConfiguredNarrativeProvider", services);
    }

    [Fact]
    public void T014_advanced_engine_is_deterministic_and_uses_core_method_discipline()
    {
        var advancedFiles = string.Join("\n", ReadAllCsFiles(Path.Combine("PlantProcess.Application", "Analytics", "Advanced")));

        Assert.Contains("Seed = 20260603UL", advancedFiles);
        Assert.Contains("BenjaminiHochberg", advancedFiles);
        Assert.Contains("Bootstrap", advancedFiles);
        Assert.Contains("EvaluateStrata", advancedFiles);
        Assert.Contains("ProvenanceHandle", advancedFiles);
        Assert.Contains("HonestyCaveat", advancedFiles);
    }
}