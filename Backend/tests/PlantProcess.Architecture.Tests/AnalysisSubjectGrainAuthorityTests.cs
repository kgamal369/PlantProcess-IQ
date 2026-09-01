using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>T-231 durable guards. Runtime behavioural proof is in the T-231 closure pack.</summary>
public sealed class T231AnalysisSubjectGrainAuthorityTests
{
    [Fact]
    public void T231_migration_has_one_generic_authority_and_no_material_assumption()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "Backend", "database", "scripts", "835_analysis_subject_grain_authority.sql"));

        Assert.Contains("ppiq_meta.analysis_grain_definitions", sql);
        Assert.Contains("ppiq_plant.analysis_subjects", sql);
        Assert.Contains("identity_definition_version", sql);
        Assert.Contains("GR01 subject_not_declared", sql);
        Assert.Contains("GR06 conflicting_declaration", sql);
        Assert.Contains("GR07 lineage_cycle", sql);
        Assert.False(sql.Contains("material_unit_id", StringComparison.OrdinalIgnoreCase));
        Assert.False(sql.Contains("REFERENCES ppiq_plant.material", StringComparison.OrdinalIgnoreCase));
        Assert.False(StripFunctionBodiesBeforeSeedCheck(sql).Contains("INSERT INTO ppiq_meta.analysis_grain_definitions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T231_api_is_tenant_claim_scoped_and_uses_one_route_for_all_grain_kinds()
    {
        var root = FindRepositoryRoot();
        var api = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Api", "Endpoints", "AnalysisSubjects", "AnalysisSubjectEndpoints.cs"));

        Assert.Contains("TenantClaims.Resolve(httpContext.User)", api);
        Assert.Contains("/api/analysis-subjects", api);
        Assert.Contains("/grains", api);
        Assert.Contains("/subjects", api);
        Assert.Contains("tenant_id", api);
        Assert.False(api.Contains("default-demo", StringComparison.OrdinalIgnoreCase));
        Assert.False(api.Contains("material_unit", StringComparison.OrdinalIgnoreCase));
        Assert.False(api.Contains("MaterialUnit", StringComparison.Ordinal));
        Assert.False(Regex.IsMatch(api, @"switch\s*\(\s*request\.GrainKind", RegexOptions.IgnoreCase));
    }

    private static string StripFunctionBodiesBeforeSeedCheck(string sql)
    {
        // Seeding would be a top-level INSERT. The declaration functions necessarily
        // contain INSERT statements, so remove function bodies before checking.
        return Regex.Replace(sql, @"CREATE\s+OR\s+REPLACE\s+FUNCTION[\s\S]*?\$fn\$;", string.Empty,
            RegexOptions.IgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found from test base directory.");
    }
}
