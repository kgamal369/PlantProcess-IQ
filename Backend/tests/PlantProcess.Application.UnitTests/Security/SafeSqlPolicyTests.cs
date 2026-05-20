using System.Text.RegularExpressions;
using FluentAssertions;
using PlantProcess.Application;

namespace PlantProcess.Application.UnitTests.Security;

public sealed class SafeSqlPolicyTests
{
    [Fact]
    public void Application_layer_should_contain_sql_safety_component_for_customer_defined_schema_views()
    {
        var applicationAssembly = typeof(ApplicationAssemblyMarker).Assembly;

        var matchingTypes = applicationAssembly
            .GetTypes()
            .Where(type =>
                type.Name.Contains("Sql", StringComparison.OrdinalIgnoreCase)
                && (
                    type.Name.Contains("Validator", StringComparison.OrdinalIgnoreCase)
                    || type.Name.Contains("Guard", StringComparison.OrdinalIgnoreCase)
                    || type.Name.Contains("Safety", StringComparison.OrdinalIgnoreCase)
                    || type.Name.Contains("Policy", StringComparison.OrdinalIgnoreCase)
                ))
            .Select(type => type.FullName)
            .ToList();

        matchingTypes.Should()
            .NotBeEmpty(
                "PlantProcess IQ allows user-defined schema views and mapping queries, so the Application layer must expose an explicit SQL safety validator/guard/policy.");
    }

    [Theory]
    [InlineData("DROP TABLE material_units", "drop")]
    [InlineData("DELETE FROM quality_events", "delete")]
    [InlineData("UPDATE material_units SET material_code = 'x'", "update")]
    [InlineData("INSERT INTO material_units VALUES (1)", "insert")]
    [InlineData("ALTER TABLE material_units ADD COLUMN hacked text", "alter")]
    [InlineData("TRUNCATE TABLE parameter_observations", "truncate")]
    [InlineData("CREATE EXTENSION dblink", "create")]
    [InlineData("COPY material_units TO PROGRAM 'cat /etc/passwd'", "copy")]
    [InlineData("SELECT * FROM users; DROP TABLE users;", "drop")]
    [InlineData("SELECT pg_sleep(30)", "pg_sleep")]
    public void Dangerous_sql_examples_should_be_treated_as_rejection_cases(
        string sql,
        string expectedForbiddenToken)
    {
        ContainsForbiddenSqlToken(sql, expectedForbiddenToken)
            .Should()
            .BeTrue("the test data must represent SQL that a future SafeSqlValidator must reject");
    }

    [Theory]
    [InlineData("SELECT material_code, grade_or_recipe FROM material_units")]
    [InlineData("select qe.event_type, count(*) from quality_events qe group by qe.event_type")]
    [InlineData("SELECT po.observed_at_utc, po.numeric_value FROM parameter_observations po WHERE po.is_deleted = false")]
    public void Read_only_select_examples_should_represent_allowed_query_shape(string sql)
    {
        sql.TrimStart()
            .StartsWith("select", StringComparison.OrdinalIgnoreCase)
            .Should()
            .BeTrue("schema-view SQL should be read-only SELECT style only");

        var forbiddenTokens = new[]
        {
            "drop",
            "delete",
            "update",
            "insert",
            "alter",
            "truncate",
            "create",
            "copy",
            "pg_sleep"
        };

        foreach (var token in forbiddenTokens)
        {
            ContainsForbiddenSqlToken(sql, token)
                .Should()
                .BeFalse($"read-only SELECT examples must not contain the forbidden SQL command token '{token}'");
        }
    }

    private static bool ContainsForbiddenSqlToken(string sql, string token)
    {
        var pattern = token.Equals("pg_sleep", StringComparison.OrdinalIgnoreCase)
            ? @"(?i)\bpg_sleep\s*\("
            : $@"(?i)\b{Regex.Escape(token)}\b";

        return Regex.IsMatch(sql, pattern, RegexOptions.CultureInvariant);
    }
}