using PlantProcess.Application.Integration.Security;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

// Covers the V1-20 defect found live 03-Jul-2026: the mapper preview rejected registered
// staging dump tables because the dynamic-allowlist overload was never fed. The dynamic
// allowlist must open READ access to registered dump tables and must never relax the
// write/DDL protection.
public sealed class SafeSqlDynamicAllowlistTests
{
    [Fact]
    public void Staging_dump_table_is_rejected_without_dynamic_allowlist()
    {
        var result = SafeSqlValidator.Validate("SELECT heat_no FROM src_meltshop_pg.heats LIMIT 5");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Staging_dump_table_is_accepted_when_registered_dynamically()
    {
        var result = SafeSqlValidator.Validate(
            "SELECT heat_no FROM src_meltshop_pg.heats LIMIT 5",
            new[] { "heats" });

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void Dynamic_allowlist_never_relaxes_write_protection()
    {
        var result = SafeSqlValidator.Validate(
            "DROP TABLE src_meltshop_pg.heats",
            new[] { "heats" });

        Assert.False(result.IsValid);
    }
}
