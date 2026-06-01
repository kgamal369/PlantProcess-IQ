using System.Collections.Generic;
using PlantProcess.Analytics.Core.Kpi;
using Xunit;
namespace PlantProcess.Analytics.Core.Tests;
// T-025 (compute core): formula correctness, threshold alert, per-tenant targets, metadata, typed errors.
public sealed class P05_KpiEngineTests
{
private readonly KpiEngine _engine = new();
private static KpiDefinition Fpsy(KpiThreshold? th = null, IReadOnlyDictionary<string, double>? targets = null) =>
    new("FPSY", "First-Pass Saleable Yield", KpiKind.Formula,
        "firstPassGood / totalProduced * 100", null, null, "%", targets, th);

[Fact]
public void Fpsy_formula_computes_correctly()
{
    var r = _engine.EvaluateFormula(Fpsy(), "t1",
        new Dictionary<string, double> { ["firstPassGood"] = 920, ["totalProduced"] = 1000 },
        "canon.quality_event", new[] { "grade=DX51D" }, "last_30_days", DateTimeOffset.UtcNow, 1000);
    Assert.Equal(92.0, r.Value, 9);
    Assert.True(r.Metadata.MetadataCompleteSurrogate());
}

[Fact]
public void Threshold_breach_raises_alert_below_target()
{
    var th = new KpiThreshold(WarningAt: 95, CriticalAt: 90, Direction: AlertDirection.BelowTargetIsBad);
    var warn = _engine.EvaluateFormula(Fpsy(th), "t1",
        new Dictionary<string, double> { ["firstPassGood"] = 920, ["totalProduced"] = 1000 },
        "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1000);
    Assert.Equal(KpiSeverity.Warning, warn.Severity);
    Assert.True(warn.AlertRaised);

    var crit = _engine.EvaluateFormula(Fpsy(th), "t1",
        new Dictionary<string, double> { ["firstPassGood"] = 880, ["totalProduced"] = 1000 },
        "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1000);
    Assert.Equal(KpiSeverity.Critical, crit.Severity);

    var ok = _engine.EvaluateFormula(Fpsy(th), "t1",
        new Dictionary<string, double> { ["firstPassGood"] = 970, ["totalProduced"] = 1000 },
        "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1000);
    Assert.Equal(KpiSeverity.Ok, ok.Severity);
    Assert.False(ok.AlertRaised);
}

[Fact]
public void Per_tenant_targets_isolate()
{
    var def = Fpsy(targets: new Dictionary<string, double> { ["t1"] = 95, ["t2"] = 80 });
    var inputs = new Dictionary<string, double> { ["firstPassGood"] = 900, ["totalProduced"] = 1000 };
    var a = _engine.EvaluateFormula(def, "t1", inputs, "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1000);
    var b = _engine.EvaluateFormula(def, "t2", inputs, "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1000);
    Assert.Equal(95.0, a.Target!.Value, 9);
    Assert.Equal(80.0, b.Target!.Value, 9);
}

[Fact]
public void Invalid_formula_raises_typed_error()
{
    Assert.Throws<KpiFormulaException>(() => _engine.EvaluateFormula(
        new KpiDefinition("BAD", "Bad", KpiKind.Formula, "firstPassGood / ", null, null, null, null, null),
        "t1", new Dictionary<string, double> { ["firstPassGood"] = 1 }, "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1));

    Assert.Throws<KpiFormulaException>(() => _engine.EvaluateFormula(
        new KpiDefinition("BAD", "Bad", KpiKind.Formula, "unknownVar * 2", null, null, null, null, null),
        "t1", new Dictionary<string, double>(), "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1));

    Assert.Throws<KpiFormulaException>(() => _engine.EvaluateFormula(
        new KpiDefinition("BAD", "Bad", KpiKind.Formula, "1 / 0", null, null, null, null, null),
        "t1", new Dictionary<string, double>(), "ds", new[] { "x" }, "w", DateTimeOffset.UtcNow, 1));
}

[Fact]
public void Invalid_sql_view_is_rejected_with_typed_error()
{
    Assert.Throws<KpiFormulaException>(() => BasicSqlGuard.Validate("DROP TABLE material_units;"));
    Assert.Throws<KpiFormulaException>(() => BasicSqlGuard.Validate("SELECT * FROM x; DELETE FROM y"));
    var ex = Record.Exception(() => BasicSqlGuard.Validate("SELECT good, total FROM canon.fpsy_view"));
    Assert.Null(ex);
}

[Fact]
public void Sql_view_kpi_validates_then_uses_measured_value()
{
    var def = new KpiDefinition("FPSY_SQL", "FPSY (view)", KpiKind.SqlView, null, "SELECT 92.0 AS value FROM canon.fpsy_view", null, "%", null, null);
    var r = _engine.EvaluateMeasured(def, "t1", 92.0, "canon.fpsy_view", new[] { "grade=DX51D" }, "last_30_days", DateTimeOffset.UtcNow, 1000);
    Assert.Equal(92.0, r.Value, 9);
    Assert.StartsWith("SQL:", r.Metadata.Formula);
}
}
internal static class MetaSurrogate
{
// Small helper so the test reads naturally; mirrors AnalysisResult.MetadataComplete for KPI metadata.
public static bool MetadataCompleteSurrogate(this PlantProcess.Analytics.Core.Primitives.AnalysisMetadata m) =>
!string.IsNullOrWhiteSpace(m.Formula) && !string.IsNullOrWhiteSpace(m.Dataset) &&
m.Filters != null && !string.IsNullOrWhiteSpace(m.TimeWindow) && m.RefreshedAtUtc != default;
}