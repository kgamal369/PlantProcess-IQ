using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

public sealed class CanonicalRegistryProjectionTests
{
    private static string Root()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Frontend")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Frontend")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Registry_is_a_published_tenant_scoped_projection_not_a_second_store()
    {
        var source = Read("Backend/PlantProcess.Api/Endpoints/Registry/RegistryEndpoints.cs");

        Assert.Contains("MapGroup(\"/api/registry\")", source);
        Assert.Contains("MapGet(\"/metadata\"", source);
        Assert.Contains("MapGet(\"/fields\"", source);
        Assert.Contains("TenantClaims.TryResolve", source);
        Assert.Contains("ppiq_meta.definition_store", source);
        Assert.Contains("ppiq_meta.definition_versions", source);
        Assert.Contains("v.status = 'published'", source);
        Assert.Contains("d.tenant_id = @tenant_id", source);
        Assert.Contains("master_dimension", source);
        Assert.Contains("master_measure", source);
        Assert.DoesNotContain("current_version =", source);

        Assert.False(Regex.IsMatch(
            source,
            @"\b(CREATE|ALTER|DROP|INSERT|UPDATE|DELETE)\s+(TABLE|INTO|ppiq_meta\.definition|FROM\s+ppiq_meta\.definition)",
            RegexOptions.IgnoreCase),
            "Registry projection must remain read-only and must not create a competing registry store.");

        Assert.DoesNotContain("IDefinitionService", source);
    }

    [Fact]
    public void Registry_fails_closed_on_missing_grain_signal_or_aggregation_semantics()
    {
        var source = Read("Backend/PlantProcess.Api/Endpoints/Registry/RegistryEndpoints.cs");

        Assert.Contains("\"GR01\"", source);
        Assert.Contains("analysis_grain_undeclared", source);
        Assert.Contains("\"AG01\"", source);
        Assert.Contains("aggregation_semantics_undeclared", source);
        Assert.Contains("\"AG02\"", source);
        Assert.Contains("aggregation_semantics_incompatible", source);
        Assert.Contains("signal_semantics_undeclared", source);

        Assert.False(
            Regex.IsMatch(source, @"aggregation\s*=\s*""Average""", RegexOptions.IgnoreCase),
            "T-210 forbids a silent Average fallback.");
    }

    [Fact]
    public void Product_grammar_stays_closed_while_customer_semantics_come_from_registry()
    {
        var api = Read("Frontend/PlantProcess.Web/src/api/dashboarding/dashboarding.api.ts");
        var merge = Read("Frontend/PlantProcess.Web/src/api/dashboarding/registryMetadataMerge.ts");

        Assert.Contains("/api/registry/metadata", api);
        Assert.Contains("mergeCanonicalRegistryMetadata", api);
        Assert.Contains("chartTypes", merge);
        Assert.Contains("dimensions", merge);
        Assert.Contains("measures", merge);
        Assert.Contains("registryRefusals", merge);
        Assert.Contains("chartTypes,", merge);
    }

    [Fact]
    public void Program_maps_the_registry_once()
    {
        var program = Read("Backend/PlantProcess.Api/Program.cs");
        const string mapper =
            "PlantProcess.Api.Endpoints.Registry.RegistryEndpoints.MapRegistryEndpoints(app);";

        Assert.Equal(1, Regex.Matches(program, Regex.Escape(mapper)).Count);
    }
}
