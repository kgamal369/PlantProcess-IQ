using PlantProcess.Application.Integration.Security;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

public sealed class SafeSqlValidatorPhase1Tests
{
    [Theory]
    [InlineData("SELECT created_at, updated_at, created_by FROM quality_events")]
    [InlineData("SELECT id FROM material_units ORDER BY created_at OFFSET 10")]
    [InlineData("WITH safe_cte AS (SELECT created_by FROM quality_events) SELECT created_by FROM safe_cte")]
    [InlineData("SELECT pre_grade FROM quality_events")]
    public void Valid_read_only_sql_with_forbidden_substrings_should_pass(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE material_units")]
    [InlineData("DROP TABLE quality_events")]
    [InlineData("CREATE VIEW unsafe AS SELECT * FROM quality_events")]
    [InlineData("SELECT * FROM pg_catalog.pg_tables")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT * FROM information_schema.tables")]
    public void Dangerous_sql_should_fail(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Dynamic_allowlist_should_allow_registered_runtime_view()
    {
        var result = SafeSqlValidator.Validate(
            "SELECT created_at FROM tenant_registered_view OFFSET 10",
            new[] { "tenant_registered_view" });

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void Bootstrap_allowlist_should_not_contain_duplicates()
    {
        var values = SqlAllowlistProvider.DefaultBootstrapAllowlist.ToArray();

        Assert.Equal(values.Length, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}