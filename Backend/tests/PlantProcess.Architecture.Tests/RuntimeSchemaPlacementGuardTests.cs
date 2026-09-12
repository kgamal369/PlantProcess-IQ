// PPIQ T-256. RUNTIME SCHEMA-PLACEMENT CLASSIFICATION GUARD.
//
// The product moved its tables into ppiq_meta, ppiq_plant and ppiq_staging, and
// five runtime queries went on naming public. Nothing noticed, because a query
// against a relation that is not there fails only when it runs, and these ran
// only on a fresh install. This guard makes that class of defect fail at build
// time instead.
//
// It does NOT demand zero occurrences of "public." - that would be false. It
// demands zero UNAPPROVED ones, and the approved classes were measured against a
// canonical database rather than assumed:
//
//   public.ppiq_*       77 routines and 9 views live in public. A routine's
//                       schema is its own; the topology map governs tables.
//   public.canonical_material_units, public.canonical_genealogy_edges
//                       compatibility VIEWS in public over relocated tables.
//                       RETAIN - retirement needs its own consumer-convergence
//                       decision and is not T-256 work.
//   public.document_sections, public.demo_runtime_settings
//                       unprovisioned in a canonical install; both call sites
//                       already probe with to_regclass and degrade.
//
// Anything else that reads or writes a public relation from runtime code is a
// placement defect and fails here with its file and line.
//
// This is validation, not a second placement authority. StorageTopologyMap
// remains the product's statement of where a table belongs; this only refuses
// what no authority approves.
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("Gate", "RuntimeSchemaPlacement")]
public sealed class RuntimeSchemaPlacementGuardTests
{
    private static readonly string[] RuntimeProjects =
    {
        "Backend/PlantProcess.Api",
        "Backend/PlantProcess.Application",
        "Backend/PlantProcess.Infrastructure",
        "Backend/PlantProcess.Domain",
        "Backend/PlantProcess.Workers",
        "Backend/PlantProcess.Analytics.Core",
        "Backend/PlantProcess.Analytics.Engine"
    };

    /// <summary>Compatibility views that live in public by decision, not by accident.</summary>
    private static readonly string[] ApprovedCompatibilityViews =
    {
        "canonical_material_units",
        "canonical_genealogy_edges"
    };

    /// <summary>Optional surfaces absent from a canonical install; every consumer probes first.</summary>
    private static readonly string[] ApprovedGuardedOptional =
    {
        "document_sections",
        "demo_runtime_settings"
    };

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend", "database")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static bool IsApproved(string relation)
    {
        // Platform routines and views. Measured: all 77 ppiq_ routines and all 9
        // ppiq_v views are in public, and nothing places them elsewhere.
        if (relation.StartsWith("ppiq_", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var v in ApprovedCompatibilityViews)
        {
            if (string.Equals(v, relation, StringComparison.OrdinalIgnoreCase)) return true;
        }

        foreach (var o in ApprovedGuardedOptional)
        {
            if (string.Equals(o, relation, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    [Fact]
    public void No_runtime_code_reads_or_writes_an_unapproved_public_relation()
    {
        var root = RepositoryRoot();
        var pattern = new System.Text.RegularExpressions.Regex(
            @"(?<![A-Za-z0-9_])public\.([A-Za-z_][A-Za-z0-9_]*)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var offenders = new List<string>();

        foreach (var project in RuntimeProjects)
        {
            var full = Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) continue;

            foreach (var file in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
                if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")) continue;

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();

                    // Prose is not a consumer.
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

                    foreach (System.Text.RegularExpressions.Match m in pattern.Matches(lines[i]))
                    {
                        var relation = m.Groups[1].Value;
                        if (IsApproved(relation)) continue;

                        offenders.Add(
                            Path.GetRelativePath(root, file).Replace('\\', '/') +
                            ":" + (i + 1) + "  public." + relation);
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Runtime code names a public relation that no authority approves. A table moved to " +
            "ppiq_meta, ppiq_plant or ppiq_staging must be addressed there; see StorageTopologyMap. " +
            "Offenders:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_approved_classes_are_stated_and_not_silently_empty()
    {
        // A guard whose allowlist quietly emptied would pass by approving nothing
        // and finding nothing. State the counts so that change is visible.
        Assert.Equal(2, ApprovedCompatibilityViews.Length);
        Assert.Equal(2, ApprovedGuardedOptional.Length);
        Assert.True(IsApproved("ppiq_resolve_safe_sql"));
        Assert.True(IsApproved("canonical_material_units"));
        Assert.False(IsApproved("ml_feature_values"));
        Assert.False(IsApproved("some_table_nobody_approved"));
    }
}
