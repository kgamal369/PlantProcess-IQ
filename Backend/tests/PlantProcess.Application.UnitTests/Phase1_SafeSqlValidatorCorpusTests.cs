// PPIQ-GENERATED (T009)
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using PlantProcess.Application.Integration.Security;
using Xunit;

namespace PlantProcess.Phase1.Tests;

public class Phase1_SafeSqlValidatorCorpusTests
{
    private static bool IsValid(object r)
    {
        var t = r.GetType();
        var b = t.GetProperties().FirstOrDefault(pr => pr.PropertyType == typeof(bool) &&
            (pr.Name.Equals("IsValid", StringComparison.OrdinalIgnoreCase) || pr.Name.Equals("Valid", StringComparison.OrdinalIgnoreCase) || pr.Name.Equals("IsSafe", StringComparison.OrdinalIgnoreCase)));
        if (b != null) return (bool)b.GetValue(r)!;
        var e = t.GetProperties().FirstOrDefault(pr => pr.Name.ToLowerInvariant().Contains("error") && typeof(IEnumerable).IsAssignableFrom(pr.PropertyType) && pr.PropertyType != typeof(string));
        if (e != null) { var en = (IEnumerable)e.GetValue(r)!; return !en.Cast<object>().Any(); }
        throw new InvalidOperationException("Cannot determine validity from " + t.Name);
    }

    [InlineData("SELECT defect_code FROM defect_catalogs WHERE created_at > now() LIMIT 10")]
    [InlineData("SELECT m.id FROM material_units m JOIN quality_events q ON q.material_unit_id = m.id ORDER BY q.created_at OFFSET 50 LIMIT 100")]
    [InlineData("WITH recent AS (SELECT id, created_at FROM parameter_observations LIMIT 10) SELECT * FROM recent LIMIT 10")]
    [Theory] public void Legit_selects_pass(string sql) => Assert.True(IsValid(SafeSqlValidator.Validate(sql)));

    [InlineData("SELECT 1; DROP TABLE material_units")]
    [InlineData("SELECT * FROM material_units; DELETE FROM quality_events")]
    [InlineData("SELECT * FROM material_units /* x */ UNION SELECT table_name,2 FROM information_schema.tables LIMIT 10")]
    [InlineData("SELECT * FROM pg_catalog.pg_authid")]
    [InlineData("SELECT pg_read_file('/etc/passwd')")]
    [InlineData("SELECT dblink('h','SELECT 1')")]
    [InlineData("SELECT pg_sleep(10)")]
    [InlineData("SELECT * FROM material_units WHERE 1=1; EXEC xp_cmdshell 'dir'")]
    [InlineData("UpDaTe material_units SET x=1")]
    [InlineData("INSERT INTO material_units (id) VALUES (1)")]
    [InlineData("DELETE FROM quality_events")]
    [InlineData("DROP TABLE quality_events")]
    [InlineData("TRUNCATE quality_events")]
    [InlineData("COPY material_units TO '/tmp/x.csv'")]
    [InlineData("GRANT ALL ON material_units TO public")]
    [InlineData("SELECT * FROM nonexistent_secret_table LIMIT 10")]
    [InlineData("SELECT * FROM material_units WHERE id IN (SELECT openrowset('a','b','c')) LIMIT 10")]
    [InlineData("WITH RECURSIVE r AS (SELECT 1) SELECT * FROM r")]
    [InlineData("REVOKE SELECT ON material_units FROM public")]
    [Theory] public void Malicious_or_unknown_fails(string sql) => Assert.False(IsValid(SafeSqlValidator.Validate(sql)));
}