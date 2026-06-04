using PlantProcess.Analytics.Core.Kpi;
using Xunit;

namespace PlantProcess.Application.UnitTests;

public sealed class Phase1_KpiSqlGuardTests
{
    [Theory]
    [InlineData("SELECT created_at, updated_at, created_by FROM quality_events")]
    [InlineData("SELECT id FROM material_units ORDER BY created_at OFFSET 10")]
    [InlineData("WITH x AS (SELECT created_by FROM quality_events) SELECT created_by FROM x")]
    [InlineData("SELECT pre_grade, created_at FROM quality_events")]
    public void Valid_read_only_views_with_forbidden_substrings_should_pass(string sql)
    {
        SafeSqlValidator.Validate(sql);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE material_units")]
    [InlineData("SELECT * FROM quality_events; SELECT * FROM material_units")]
    [InlineData("DROP TABLE material_units")]
    [InlineData("CREATE VIEW v AS SELECT * FROM material_units")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT * FROM information_schema.tables")]
    public void Dangerous_or_multi_statement_sql_should_be_rejected(string sql)
    {
        Assert.Throws<KpiFormulaException>(() => SafeSqlValidator.Validate(sql));
    }
}