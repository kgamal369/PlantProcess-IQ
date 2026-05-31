using FluentAssertions;

namespace PlantProcess.Infrastructure.IntegrationTests.Database;

public sealed class SqlScriptHygieneApplyTests
{
    [Fact]
    public void Ordered_database_scripts_should_be_non_empty_and_bom_free()
    {
        var scriptsRoot = Path.Combine(FindBackendRoot(), "database", "scripts");

        Directory.Exists(scriptsRoot)
            .Should()
            .BeTrue("database/scripts must exist");

        var scripts = Directory.GetFiles(scriptsRoot, "*.sql")
            .OrderBy(Path.GetFileName)
            .ToList();

        scripts.Should().NotBeEmpty();

        foreach (var script in scripts)
        {
            var bytes = File.ReadAllBytes(script);
            var text = File.ReadAllText(script);

            bytes.Should().NotBeEmpty(Path.GetFileName(script));
            HasUtf8Bom(bytes).Should().BeFalse($"{Path.GetFileName(script)} must be BOM-free for psql automation");
            text.Trim().Should().NotBeEmpty($"{Path.GetFileName(script)} must not be an unexplained empty placeholder");
            text.Should().NotContain("\0", $"{Path.GetFileName(script)} must not contain null bytes");
        }
    }

    [Fact]
    public void Critical_ml_learning_scripts_should_expose_acceptance_and_governance_functions()
    {
        var scriptsRoot = Path.Combine(FindBackendRoot(), "database", "scripts");

        var script204 = File.ReadAllText(Path.Combine(scriptsRoot, "204_phase04_phase05_ml_learning_core.sql"));
        var script205 = File.ReadAllText(Path.Combine(scriptsRoot, "205_phase04_phase05_completion_governance_jobs_tests.sql"));

        script204.Should().Contain("ppiq_ml_seed_phase45_golden_dataset");
        script204.Should().Contain("ppiq_ml_run_learning_job_v1");
        script204.Should().Contain("ppiq_ml_phase45_acceptance");

        script205.Should().Contain("ppiq_ml_run_phase45_golden_tests_v1");
        script205.Should().Contain("ppiq_ml_phase45_completion_acceptance_v1");
        script205.Should().Contain("v_ml_learning_jobs_monitor_v1");
    }

    [Fact]
    public void Runtime_role_script_should_require_explicit_password_variable()
    {
        var script = File.ReadAllText(Path.Combine(
            FindBackendRoot(),
            "database",
            "scripts",
            "095_create_runtime_app_role_admin_only.sql"));

        script.Should().Contain("\\set ON_ERROR_STOP on");
        script.Should().Contain("plantprocess_app_password");
        script.Should().Contain("\\quit 1");
    }

    [Fact]
    public void Sql_scripts_should_not_contain_raw_unmasked_local_password()
    {
        var scriptsRoot = Path.Combine(FindBackendRoot(), "database");
        var sqlFiles = Directory.GetFiles(scriptsRoot, "*.sql", SearchOption.AllDirectories);

        foreach (var file in sqlFiles)
        {
            var text = File.ReadAllText(file);

            text.Should().NotContain(
                "plantprocess123",
                $"{Path.GetFileName(file)} must not persist local development password in SQL scripts");
        }
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 &&
               bytes[0] == 0xEF &&
               bytes[1] == 0xBB &&
               bytes[2] == 0xBF;
    }

    private static string FindBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "database", "scripts")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Backend root from test output directory.");
    }
}
