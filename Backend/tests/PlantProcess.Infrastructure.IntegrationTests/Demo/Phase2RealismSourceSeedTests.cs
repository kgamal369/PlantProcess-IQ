using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Demo;

public sealed class Phase2RealismSourceSeedTests
{
    [Fact]
    public void Realism_source_files_contain_expected_scale_and_reference_thread()
    {
        var root = FindRepoRoot();
        var infra = Path.Combine(root, "Infrastructure", "demo-sources");

        var meltshop = File.ReadAllText(Path.Combine(infra, "meltshop-postgres", "init", "001_schema_seed.sql"));
        var caster = File.ReadAllText(Path.Combine(infra, "caster-oracle", "init", "001_schema_seed.sql"));
        var hsm = File.ReadAllText(Path.Combine(infra, "hsm-oracle", "init", "001_schema_seed.sql"));
        var pkl = File.ReadAllText(Path.Combine(infra, "pkl-mssql", "init", "001_schema_seed.sql"));
        var parsytec = File.ReadAllText(Path.Combine(infra, "parsytec-mysql", "init", "001_schema_seed.sql"));
        var qa = File.ReadAllText(Path.Combine(infra, "excel-qa", "qa_samples.csv"));
        var yard = File.ReadAllText(Path.Combine(infra, "excel-yard", "yard_inventory.csv"));

        Assert.Contains("generate_series(1, 630)", meltshop);
        Assert.Contains("FOR i IN 1..5600 LOOP", caster);
        Assert.Contains("FOR i IN 1..5600 LOOP", hsm);
        Assert.Contains("TOP (5600)", pkl);
        Assert.Contains("i < 5600", parsytec);

        // C-0044170 is a coil/thread marker, not a meltshop-native identifier.
        // Meltshop proves the reference thread through H-3361; caster through S-0044170;
        // downstream coil-bearing sources prove C-0044170.
        var coilBearingSources = new[] { hsm, pkl, parsytec, qa, yard };
        Assert.True(
            coilBearingSources.Count(source => source.Contains("C-0044170", StringComparison.OrdinalIgnoreCase)) >= 3,
            "At least three downstream coil-bearing demo sources must contain C-0044170.");

        Assert.Contains("H-3361", meltshop);
        Assert.Contains("H-3361", caster);
        Assert.Contains("S-0044170", caster);
        Assert.Contains("S-0044170", hsm);
        Assert.Contains("EDGE_CRACK", parsytec);
        Assert.Contains("UNMAPPED_DEMO_DEFECT", parsytec);
        Assert.Contains("C-ORPHAN-0001", hsm);
    }

    [Fact]
    public void Yard_csv_trim_regression_fixture_has_no_leading_whitespace()
    {
        var root = FindRepoRoot();
        var yardPath = Path.Combine(root, "Infrastructure", "demo-sources", "excel-yard", "yard_inventory.csv");
        var rows = File.ReadAllLines(yardPath);

        Assert.True(rows.Length >= 5401, $"Expected realism-scale yard rows. Actual lines: {rows.Length}");

        var reference = rows.Single(x => x.Contains("C-0044170", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(",BAY-03,", reference);
        Assert.DoesNotContain(", BAY-03,", reference);
        Assert.DoesNotContain(", POS-17,", reference);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Infrastructure")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root could not be found.");
    }
}