using System;
using System.Linq;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Application.UnitTests.Widgets;

// P6-04 — widget-script compiler unit tests (PPIQ-WF-012). No DB required.
public sealed class WidgetScriptSqlCompilerTests
{
    [Fact]
    public void Doctrine_example_compiles_to_safe_parameterized_sql()
    {
        var script =
            "source=casting_quality_v; " +
            "measure=avg(tundish_superheat_c); " +
            "group_by=shift; " +
            "filter=grade in [A36,A572,X65]; " +
            "having=count(*) > 20";

        var r = WidgetScriptSqlCompiler.Compile(script);

        Assert.True(r.IsValid, r.Error);
        Assert.NotNull(r.Sql);
        Assert.Contains("avg(tundish_superheat_c)", r.Sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("group by shift", r.Sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("having count(*)", r.Sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("limit", r.Sql!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A36", r.Sql!); // values are parameterized, never inlined
        Assert.Contains(r.Parameters, x => x.Value is object[] arr && arr.Length == 3);
        Assert.Contains(r.Parameters, x => x.Value is decimal d && d == 20m);
    }

    [Theory]
    [InlineData("source=demo_widget_v; measure=avg(x); group_by=shift); DROP TABLE foo")]
    [InlineData("source=demo_widget_v; measure=avg(x); filter=grade; DROP TABLE foo -- = 1")]
    [InlineData("source=demo_widget_v; measure=avg(x) ); DROP TABLE foo")]
    [InlineData("source=public.users; measure=count(*); filter=1=1 OR x=x")]
    public void Injection_attempts_are_rejected(string script)
    {
        var r = WidgetScriptSqlCompiler.Compile(script);
        Assert.False(r.IsValid);
        Assert.NotNull(r.ErrorCode);
        if (r.Sql is not null)
            Assert.DoesNotContain("DROP", r.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Row_limit_is_enforced_and_clamped()
    {
        var r = WidgetScriptSqlCompiler.Compile("source=demo_widget_v; measure=count(*)", requestedRowLimit: 999999);
        Assert.True(r.IsValid, r.Error);
        Assert.Equal(WidgetScriptSqlCompiler.MaxRowLimit, r.RowLimit);
        Assert.Contains($"LIMIT {WidgetScriptSqlCompiler.MaxRowLimit}", r.Sql!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Timeout_is_capped_at_30_seconds()
    {
        Assert.Equal(30, WidgetScriptSqlCompiler.Compile("source=demo_widget_v; measure=count(*)", requestedTimeoutSeconds: 120).TimeoutSeconds);
        Assert.Equal(30, WidgetScriptSqlCompiler.Compile("source=demo_widget_v; measure=count(*)").TimeoutSeconds);
    }

    [Fact]
    public void Aggregate_measures_emit_expected_sql()
    {
        var r = WidgetScriptSqlCompiler.Compile("source=demo_widget_v; measure=sum(qty) as total; measure=count(*); group_by=line");
        Assert.True(r.IsValid, r.Error);
        Assert.Contains("sum(qty) AS total", r.Sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("count(*)", r.Sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("group by line", r.Sql!, StringComparison.OrdinalIgnoreCase);
    }
}