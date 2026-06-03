// PPIQ-GENERATED (T009)
using PlantProcess.Analytics.Core.Kpi;
using Xunit;

namespace PlantProcess.Phase1.Tests;

public class Phase1_KpiSqlGuardTests
{
    [InlineData("SELECT created_at, updated_at FROM quality_events")]
    [InlineData("SELECT id FROM material_units ORDER BY created_at OFFSET 10")]
    [InlineData("WITH x AS (SELECT created_by FROM app_users) SELECT * FROM x")]
    [Theory] public void Valid_views_pass(string sql) => BasicSqlGuard.Validate(sql);

    [InlineData("SELECT 1; DROP TABLE quality_events")]
    [InlineData("SELECT * FROM pg_read_file('/etc/passwd')")]
    [InlineData("INSERT INTO quality_events VALUES (1)")]
    [InlineData("SELECT table_name FROM information_schema.tables")]
    [Theory] public void Dangerous_views_fail(string sql) => Assert.Throws<KpiFormulaException>(() => BasicSqlGuard.Validate(sql));
}
