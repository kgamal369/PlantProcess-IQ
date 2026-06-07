const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replace(/\//g, path.sep));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function backup(relativePath) {
  const source = p(relativePath);
  if (!fs.existsSync(source)) return;

  const stamp = new Date().toISOString().replace(/[-:]/g, "").replace(/\..+/, "").replace("T", "_");
  const target = p(".phase7_backup/t039_value_realization_ledger_" + stamp + "/" + relativePath);
  ensureDir(path.dirname(target));
  fs.copyFileSync(source, target);
}

function run(name, command, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, {
    cwd: root,
    stdio: "inherit",
    shell: false
  });
}

backup("Backend/PlantProcess.Infrastructure/Analytics/ValueEngineExtensions.cs");
backup("Backend/PlantProcess.Api/Program.cs");

write("Backend/PlantProcess.Application/Analytics/Value/ValueRealizationContracts.cs", `
namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_CONTRACTS.
/// Value-realization tracking separates projected/potential value from tracked realized value.
/// It is baseline-vs-actual evidence, not automatic causal attribution.
/// </summary>
public enum ValueMetricDirection
{
    LowerIsBetter = 1,
    HigherIsBetter = 2
}

public sealed record ValueRealizationWindow(
    string MetricCode,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    decimal Value,
    string Unit)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(MetricCode) && EndUtc > StartUtc;
}

public sealed record ValueRealizationRequest(
    string TrackingCode,
    string? SourceRecommendationId,
    Guid? SourceValueImpactId,
    ValueRealizationWindow BaselineWindow,
    ValueRealizationWindow ActualWindow,
    ValueMetricDirection Direction,
    CostBand ValuePerUnit,
    CostBand PotentialValue,
    decimal InvestmentCost,
    string Currency = "EUR");

public sealed record ValueRealizationResult(
    string TrackingCode,
    string Currency,
    string? SourceRecommendationId,
    Guid? SourceValueImpactId,
    string MetricCode,
    decimal BaselineValue,
    decimal ActualValue,
    decimal ImprovementUnits,
    decimal RealizedLow,
    decimal RealizedMid,
    decimal RealizedHigh,
    decimal PotentialLow,
    decimal PotentialMid,
    decimal PotentialHigh,
    decimal? CaptureRateMid,
    decimal? RoiMid,
    string Status,
    bool IsAbstained,
    string? AbstainReason,
    string AttributionCaveat,
    string EvidenceJson)
{
    public decimal RealizedExpected => RealizedMid;

    public bool IsMonotonic => IsAbstained || (RealizedLow <= RealizedMid && RealizedMid <= RealizedHigh);

    public static ValueRealizationResult Abstained(
        ValueRealizationRequest request,
        string reason)
    {
        var metric = request.BaselineWindow?.MetricCode ?? request.ActualWindow?.MetricCode ?? "unknown";

        return new ValueRealizationResult(
            request.TrackingCode,
            string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.Trim().ToUpperInvariant(),
            request.SourceRecommendationId,
            request.SourceValueImpactId,
            metric,
            request.BaselineWindow?.Value ?? 0m,
            request.ActualWindow?.Value ?? 0m,
            0m,
            0m,
            0m,
            0m,
            request.PotentialValue?.Low ?? 0m,
            request.PotentialValue?.Mid ?? 0m,
            request.PotentialValue?.High ?? 0m,
            null,
            null,
            "Abstained",
            true,
            reason,
            ValueRealizationCaveats.AttributionCaveat,
            "{}");
    }
}

public sealed record ValueRealizationLedgerEntry(
    Guid Id,
    Guid TenantId,
    string TrackingCode,
    string? SourceRecommendationId,
    Guid? SourceValueImpactId,
    string MetricCode,
    string Currency,
    decimal RealizedLow,
    decimal RealizedMid,
    decimal RealizedHigh,
    decimal? RoiMid,
    string Status,
    string AttributionCaveat,
    DateTimeOffset RecordedAtUtc);

public static class ValueRealizationCaveats
{
    public const string AttributionCaveat =
        "Baseline-vs-actual tracked value is not automatic causal attribution. Correlation is not causation; review operating context before claiming savings.";
}
`);

write("Backend/PlantProcess.Application/Analytics/Value/IValueRealizationService.cs", `
namespace PlantProcess.Application.Analytics.Value;

public interface IValueRealizationService
{
    ValueRealizationResult Calculate(ValueRealizationRequest request);
}
`);

write("Backend/PlantProcess.Application/Analytics/Value/ValueRealizationService.cs", `
using System.Globalization;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_TRACKING_SERVICE.
/// Computes baseline-vs-actual realized value and ROI while preserving attribution caveats.
/// </summary>
public sealed class ValueRealizationService : IValueRealizationService
{
    public ValueRealizationResult Calculate(ValueRealizationRequest request)
    {
        var validation = Validate(request);

        if (validation.Count > 0)
        {
            return ValueRealizationResult.Abstained(request, string.Join("; ", validation));
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "EUR"
            : request.Currency.Trim().ToUpperInvariant();

        var improvementUnits = request.Direction == ValueMetricDirection.LowerIsBetter
            ? request.BaselineWindow.Value - request.ActualWindow.Value
            : request.ActualWindow.Value - request.BaselineWindow.Value;

        var values = new[]
        {
            improvementUnits * request.ValuePerUnit.Low,
            improvementUnits * request.ValuePerUnit.Mid,
            improvementUnits * request.ValuePerUnit.High
        }
        .Select(RoundMoney)
        .OrderBy(x => x)
        .ToArray();

        var realizedMid = RoundMoney(improvementUnits * request.ValuePerUnit.Mid);

        var captureRateMid = request.PotentialValue.Mid == 0m
            ? null
            : RoundRatio(realizedMid / request.PotentialValue.Mid);

        var roiMid = request.InvestmentCost <= 0m
            ? null
            : RoundRatio(realizedMid / request.InvestmentCost);

        var status = realizedMid switch
        {
            > 0m => "PositiveTrackedValue",
            < 0m => "NegativeTrackedValue",
            _ => "NeutralTrackedValue"
        };

        var evidenceJson = Json(new Dictionary<string, object?>
        {
            ["trackingCode"] = request.TrackingCode,
            ["sourceRecommendationId"] = request.SourceRecommendationId,
            ["sourceValueImpactId"] = request.SourceValueImpactId,
            ["metricCode"] = request.BaselineWindow.MetricCode,
            ["direction"] = request.Direction.ToString(),
            ["baselineStartUtc"] = request.BaselineWindow.StartUtc,
            ["baselineEndUtc"] = request.BaselineWindow.EndUtc,
            ["baselineValue"] = request.BaselineWindow.Value,
            ["actualStartUtc"] = request.ActualWindow.StartUtc,
            ["actualEndUtc"] = request.ActualWindow.EndUtc,
            ["actualValue"] = request.ActualWindow.Value,
            ["improvementUnits"] = improvementUnits,
            ["unit"] = request.BaselineWindow.Unit,
            ["valuePerUnitLow"] = request.ValuePerUnit.Low,
            ["valuePerUnitExpected"] = request.ValuePerUnit.Mid,
            ["valuePerUnitHigh"] = request.ValuePerUnit.High,
            ["potentialLow"] = request.PotentialValue.Low,
            ["potentialExpected"] = request.PotentialValue.Mid,
            ["potentialHigh"] = request.PotentialValue.High,
            ["investmentCost"] = request.InvestmentCost,
            ["attributionCaveat"] = ValueRealizationCaveats.AttributionCaveat
        });

        return new ValueRealizationResult(
            request.TrackingCode,
            currency,
            request.SourceRecommendationId,
            request.SourceValueImpactId,
            request.BaselineWindow.MetricCode.Trim(),
            request.BaselineWindow.Value,
            request.ActualWindow.Value,
            improvementUnits,
            values[0],
            realizedMid,
            values[2],
            request.PotentialValue.Low,
            request.PotentialValue.Mid,
            request.PotentialValue.High,
            captureRateMid,
            roiMid,
            status,
            IsAbstained: false,
            AbstainReason: null,
            ValueRealizationCaveats.AttributionCaveat,
            evidenceJson);
    }

    private static IReadOnlyList<string> Validate(ValueRealizationRequest request)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TrackingCode))
        {
            failures.Add("tracking_code_required");
        }

        if (string.IsNullOrWhiteSpace(request.SourceRecommendationId) && request.SourceValueImpactId is null)
        {
            failures.Add("source_recommendation_or_value_impact_link_required");
        }

        if (request.BaselineWindow is null || !request.BaselineWindow.IsValid)
        {
            failures.Add("valid_baseline_window_required");
        }

        if (request.ActualWindow is null || !request.ActualWindow.IsValid)
        {
            failures.Add("valid_actual_window_required");
        }

        if (request.BaselineWindow is not null && request.ActualWindow is not null)
        {
            if (!string.Equals(request.BaselineWindow.MetricCode, request.ActualWindow.MetricCode, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("baseline_and_actual_metric_must_match");
            }

            if (!string.Equals(request.BaselineWindow.Unit, request.ActualWindow.Unit, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("baseline_and_actual_unit_must_match");
            }
        }

        if (!request.ValuePerUnit.IsComplete)
        {
            failures.Add("value_per_unit_band_must_satisfy_low_expected_high");
        }

        if (!request.PotentialValue.IsComplete)
        {
            failures.Add("potential_value_band_must_satisfy_low_expected_high");
        }

        if (request.InvestmentCost < 0m)
        {
            failures.Add("investment_cost_cannot_be_negative");
        }

        return failures;
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRatio(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string Json(IReadOnlyDictionary<string, object?> values)
    {
        static string Scalar(object? value)
        {
            return value switch
            {
                null => "null",
                string s => "\\\"" + s.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\"",
                Guid g => "\\\"" + g.ToString("D") + "\\\"",
                DateTimeOffset d => "\\\"" + d.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) + "\\\"",
                bool b => b ? "true" : "false",
                decimal d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => "\\\"" + value.ToString()?.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\""
            };
        }

        return "{" + string.Join(",", values.Select(x => "\\\"" + x.Key + "\\\":" + Scalar(x.Value))) + "}";
    }
}
`);

write("Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueRealizationRepository.cs", `
using Npgsql;
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_REPOSITORY.
/// Persists tracked realized value separately from projected value impact.
/// </summary>
public sealed class NpgsqlValueRealizationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlValueRealizationRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("""
            CREATE SCHEMA IF NOT EXISTS canon;

            CREATE TABLE IF NOT EXISTS canon.value_realization_ledger (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id uuid NOT NULL,
                tracking_code text NOT NULL,
                source_recommendation_id text NULL,
                source_value_impact_id uuid NULL,
                metric_code text NOT NULL,
                currency text NOT NULL DEFAULT 'EUR',
                baseline_value numeric(18,4) NOT NULL,
                actual_value numeric(18,4) NOT NULL,
                improvement_units numeric(18,4) NOT NULL,
                potential_eur_low numeric(18,2) NOT NULL,
                potential_eur_mid numeric(18,2) NOT NULL,
                potential_eur_high numeric(18,2) NOT NULL,
                realized_eur_low numeric(18,2) NOT NULL,
                realized_eur_mid numeric(18,2) NOT NULL,
                realized_eur_high numeric(18,2) NOT NULL,
                capture_rate_mid numeric(18,4) NULL,
                roi_mid numeric(18,4) NULL,
                status text NOT NULL,
                attribution_caveat text NOT NULL,
                evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
                recorded_at_utc timestamptz NOT NULL DEFAULT now(),
                recorded_by text NULL,
                CONSTRAINT ck_value_realization_realized_band CHECK (realized_eur_low <= realized_eur_mid AND realized_eur_mid <= realized_eur_high),
                CONSTRAINT ck_value_realization_potential_band CHECK (potential_eur_low <= potential_eur_mid AND potential_eur_mid <= potential_eur_high),
                CONSTRAINT ck_value_realization_currency CHECK (char_length(currency) = 3)
            );

            CREATE INDEX IF NOT EXISTS ix_value_realization_tenant_recorded
            ON canon.value_realization_ledger(tenant_id, recorded_at_utc DESC);

            CREATE INDEX IF NOT EXISTS ix_value_realization_source_value_impact
            ON canon.value_realization_ledger(source_value_impact_id);
            """);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> RecordAsync(
        Guid tenantId,
        ValueRealizationResult result,
        string? actor,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);

        await using var cmd = _dataSource.CreateCommand("""
            INSERT INTO canon.value_realization_ledger (
                tenant_id,
                tracking_code,
                source_recommendation_id,
                source_value_impact_id,
                metric_code,
                currency,
                baseline_value,
                actual_value,
                improvement_units,
                potential_eur_low,
                potential_eur_mid,
                potential_eur_high,
                realized_eur_low,
                realized_eur_mid,
                realized_eur_high,
                capture_rate_mid,
                roi_mid,
                status,
                attribution_caveat,
                evidence,
                recorded_by)
            VALUES (
                @tenant_id,
                @tracking_code,
                @source_recommendation_id,
                @source_value_impact_id,
                @metric_code,
                @currency,
                @baseline_value,
                @actual_value,
                @improvement_units,
                @potential_low,
                @potential_mid,
                @potential_high,
                @realized_low,
                @realized_mid,
                @realized_high,
                @capture_rate_mid,
                @roi_mid,
                @status,
                @attribution_caveat,
                @evidence::jsonb,
                @recorded_by)
            RETURNING id;
            """);

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("tracking_code", result.TrackingCode);
        cmd.Parameters.AddWithValue("source_recommendation_id", (object?)result.SourceRecommendationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("source_value_impact_id", (object?)result.SourceValueImpactId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("metric_code", result.MetricCode);
        cmd.Parameters.AddWithValue("currency", result.Currency);
        cmd.Parameters.AddWithValue("baseline_value", result.BaselineValue);
        cmd.Parameters.AddWithValue("actual_value", result.ActualValue);
        cmd.Parameters.AddWithValue("improvement_units", result.ImprovementUnits);
        cmd.Parameters.AddWithValue("potential_low", result.PotentialLow);
        cmd.Parameters.AddWithValue("potential_mid", result.PotentialMid);
        cmd.Parameters.AddWithValue("potential_high", result.PotentialHigh);
        cmd.Parameters.AddWithValue("realized_low", result.RealizedLow);
        cmd.Parameters.AddWithValue("realized_mid", result.RealizedMid);
        cmd.Parameters.AddWithValue("realized_high", result.RealizedHigh);
        cmd.Parameters.AddWithValue("capture_rate_mid", (object?)result.CaptureRateMid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("roi_mid", (object?)result.RoiMid ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", result.Status);
        cmd.Parameters.AddWithValue("attribution_caveat", result.AttributionCaveat);
        cmd.Parameters.AddWithValue("evidence", result.EvidenceJson);
        cmd.Parameters.AddWithValue("recorded_by", (object?)actor ?? DBNull.Value);

        var value = await cmd.ExecuteScalarAsync(ct);
        return value is Guid id ? id : Guid.Empty;
    }

    public async Task<IReadOnlyList<ValueRealizationLedgerEntry>> ListRecentAsync(
        Guid tenantId,
        int take,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);

        await using var cmd = _dataSource.CreateCommand("""
            SELECT
                id,
                tenant_id,
                tracking_code,
                source_recommendation_id,
                source_value_impact_id,
                metric_code,
                currency,
                realized_eur_low,
                realized_eur_mid,
                realized_eur_high,
                roi_mid,
                status,
                attribution_caveat,
                recorded_at_utc
            FROM canon.value_realization_ledger
            WHERE tenant_id = @tenant_id
            ORDER BY recorded_at_utc DESC
            LIMIT @take;
            """);

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("take", Math.Clamp(take, 1, 100));

        var result = new List<ValueRealizationLedgerEntry>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(new ValueRealizationLedgerEntry(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetFieldValue<DateTimeOffset>(13)));
        }

        return result;
    }
}
`);

write("Backend/PlantProcess.Api/Endpoints/Analytics/ValueRealizationEndpoints.cs", `
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PlantProcess.Application.Analytics.Value;
using PlantProcess.Infrastructure.Analytics;

namespace PlantProcess.Api.Endpoints.Analytics;

/// <summary>
/// PPIQ_REALIZATION_T039_VALUE_REALIZATION_ENDPOINTS.
/// Records tracked realized value separately from projected value impact.
/// </summary>
public static class ValueRealizationEndpoints
{
    public static IEndpointRouteBuilder MapValueRealizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/value/realization")
            .WithTags("Value / Realization")
            .RequireAuthorization();

        group.MapGet("/contract", () => Results.Ok(new
        {
            marker = "PPIQ_REALIZATION_T039_VALUE_REALIZATION_ENDPOINTS",
            caveat = ValueRealizationCaveats.AttributionCaveat,
            routes = new[]
            {
                "POST /api/value/realization/calculate",
                "POST /api/value/realization/record",
                "GET /api/value/realization/ledger"
            },
            guardrails = new[]
            {
                "Potential value and realized value are separated.",
                "Ledger recording requires tenant context.",
                "Baseline and actual windows must use same metric and unit.",
                "Correlation is not causation."
            }
        }));

        group.MapPost("/calculate", (ValueRealizationRequest request, IValueRealizationService service) =>
        {
            var result = service.Calculate(request);
            return Results.Ok(result);
        });

        group.MapPost("/record", async (
            ValueRealizationRequest request,
            ClaimsPrincipal user,
            IValueRealizationService service,
            NpgsqlValueRealizationRepository repo,
            CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tenant"] = new[] { "no_tenant" }
                });
            }

            var result = service.Calculate(request);

            if (result.IsAbstained)
            {
                return Results.Ok(new
                {
                    recorded = false,
                    result,
                    reason = result.AbstainReason
                });
            }

            var id = await repo.RecordAsync(tenantId, result, user.Identity?.Name ?? "unknown", ct);

            return Results.Ok(new
            {
                recorded = true,
                id,
                result
            });
        });

        group.MapGet("/ledger", async (
            int? take,
            ClaimsPrincipal user,
            NpgsqlValueRealizationRepository repo,
            CancellationToken ct) =>
        {
            if (!TryTenant(user, out var tenantId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tenant"] = new[] { "no_tenant" }
                });
            }

            var rows = await repo.ListRecentAsync(tenantId, take ?? 25, ct);

            return Results.Ok(new
            {
                caveat = ValueRealizationCaveats.AttributionCaveat,
                rows
            });
        });

        return app;
    }

    private static bool TryTenant(ClaimsPrincipal user, out Guid tenantId)
        => PlantProcess.Application.Security.Tenancy.TenantClaims.TryResolve(user, out tenantId);
}
`);

write("Backend/database/scripts/421_phase7_value_realization_ledger.sql", `
-- ============================================================================
-- 421_phase7_value_realization_ledger.sql
-- PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_SQL
-- Tracks realized value separately from projected value impact.
-- Baseline-vs-actual tracking is not automatic causal attribution.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS canon;

CREATE TABLE IF NOT EXISTS canon.value_realization_ledger (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    tracking_code text NOT NULL,
    source_recommendation_id text NULL,
    source_value_impact_id uuid NULL,
    metric_code text NOT NULL,
    currency text NOT NULL DEFAULT 'EUR',
    baseline_value numeric(18,4) NOT NULL,
    actual_value numeric(18,4) NOT NULL,
    improvement_units numeric(18,4) NOT NULL,
    potential_eur_low numeric(18,2) NOT NULL,
    potential_eur_mid numeric(18,2) NOT NULL,
    potential_eur_high numeric(18,2) NOT NULL,
    realized_eur_low numeric(18,2) NOT NULL,
    realized_eur_mid numeric(18,2) NOT NULL,
    realized_eur_high numeric(18,2) NOT NULL,
    capture_rate_mid numeric(18,4) NULL,
    roi_mid numeric(18,4) NULL,
    status text NOT NULL,
    attribution_caveat text NOT NULL,
    evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
    recorded_at_utc timestamptz NOT NULL DEFAULT now(),
    recorded_by text NULL,
    CONSTRAINT ck_value_realization_realized_band CHECK (realized_eur_low <= realized_eur_mid AND realized_eur_mid <= realized_eur_high),
    CONSTRAINT ck_value_realization_potential_band CHECK (potential_eur_low <= potential_eur_mid AND potential_eur_mid <= potential_eur_high),
    CONSTRAINT ck_value_realization_currency CHECK (char_length(currency) = 3)
);

CREATE INDEX IF NOT EXISTS ix_value_realization_tenant_recorded
ON canon.value_realization_ledger(tenant_id, recorded_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_value_realization_source_value_impact
ON canon.value_realization_ledger(source_value_impact_id);
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueRealizationTrackingTests.cs", `
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase7_ValueRealizationTrackingTests
{
    private static ValueRealizationRequest DemoRequest(decimal actualValue = 80m) => new(
        TrackingCode: "T039-DEMO-EDGE-CRACK-REALIZATION",
        SourceRecommendationId: "rec-edge-crack-001",
        SourceValueImpactId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        BaselineWindow: new ValueRealizationWindow(
            "edge_crack_count",
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero),
            100m,
            "defects"),
        ActualWindow: new ValueRealizationWindow(
            "edge_crack_count",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            actualValue,
            "defects"),
        Direction: ValueMetricDirection.LowerIsBetter,
        ValuePerUnit: new CostBand(100m, 150m, 200m),
        PotentialValue: new CostBand(28_000m, 42_000m, 56_000m),
        InvestmentCost: 1_000m,
        Currency: "EUR");

    [Fact]
    public void T039_Computes_BaselineVsActual_TrackedValue_AndRoi()
    {
        var result = new ValueRealizationService().Calculate(DemoRequest());

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal(20m, result.ImprovementUnits);
        Assert.Equal(2_000m, result.RealizedLow);
        Assert.Equal(3_000m, result.RealizedExpected);
        Assert.Equal(4_000m, result.RealizedHigh);
        Assert.Equal(0.0714m, result.CaptureRateMid);
        Assert.Equal(3.0000m, result.RoiMid);
        Assert.Equal("PositiveTrackedValue", result.Status);
        Assert.Contains("Correlation is not causation", result.AttributionCaveat);
    }

    [Fact]
    public void T039_ChangingActualValue_ChangesRealizedValue()
    {
        var service = new ValueRealizationService();

        var first = service.Calculate(DemoRequest(actualValue: 80m));
        var second = service.Calculate(DemoRequest(actualValue: 70m));

        Assert.Equal(3_000m, first.RealizedExpected);
        Assert.Equal(4_500m, second.RealizedExpected);
        Assert.True(second.RealizedExpected > first.RealizedExpected);
    }

    [Fact]
    public void T039_Abstains_WhenMetricWindowsDoNotMatch()
    {
        var request = DemoRequest() with
        {
            ActualWindow = DemoRequest().ActualWindow with { MetricCode = "different_metric" }
        };

        var result = new ValueRealizationService().Calculate(request);

        Assert.True(result.IsAbstained);
        Assert.Contains("baseline_and_actual_metric_must_match", result.AbstainReason);
    }

    [Fact]
    public void T039_Abstains_WhenSourceLinkIsMissing()
    {
        var request = DemoRequest() with
        {
            SourceRecommendationId = null,
            SourceValueImpactId = null
        };

        var result = new ValueRealizationService().Calculate(request);

        Assert.True(result.IsAbstained);
        Assert.Contains("source_recommendation_or_value_impact_link_required", result.AbstainReason);
    }

    [Fact]
    public void T039_WorseActualPerformance_CreatesNegativeTrackedValue()
    {
        var result = new ValueRealizationService().Calculate(DemoRequest(actualValue: 120m));

        Assert.False(result.IsAbstained);
        Assert.Equal(-20m, result.ImprovementUnits);
        Assert.Equal(-4_000m, result.RealizedLow);
        Assert.Equal(-3_000m, result.RealizedExpected);
        Assert.Equal(-2_000m, result.RealizedHigh);
        Assert.Equal("NegativeTrackedValue", result.Status);
        Assert.True(result.IsMonotonic);
    }
}
`);

let extensionPath = "Backend/PlantProcess.Infrastructure/Analytics/ValueEngineExtensions.cs";
let extension = read(extensionPath);

if (!extension.includes("IValueRealizationService")) {
  extension = extension.replace(
    "services.AddScoped<NpgsqlValueImpactRepository>();",
    [
      "services.AddScoped<NpgsqlValueImpactRepository>();",
      "        services.AddSingleton<IValueRealizationService, ValueRealizationService>();",
      "        services.AddScoped<NpgsqlValueRealizationRepository>();"
    ].join("\n")
  );

  write(extensionPath, extension);
}

let programPath = "Backend/PlantProcess.Api/Program.cs";
let program = read(programPath);

if (!program.includes("MapValueRealizationEndpoints")) {
  if (program.includes("app.MapValueEndpoints();")) {
    program = program.replace(
      "app.MapValueEndpoints();",
      "app.MapValueEndpoints();\n    app.MapValueRealizationEndpoints();"
    );
  } else {
    throw new Error("Could not find app.MapValueEndpoints(); in Program.cs");
  }

  write(programPath, program);
}

write("tools/phase7/validate-t039-value-realization-ledger.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueRealizationContracts.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_CONTRACTS",
      "ValueRealizationRequest",
      "ValueRealizationResult",
      "ValueRealizationLedgerEntry",
      "Baseline-vs-actual tracked value is not automatic causal attribution"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueRealizationService.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_TRACKING_SERVICE",
      "source_recommendation_or_value_impact_link_required",
      "baseline_and_actual_metric_must_match",
      "CaptureRateMid",
      "RoiMid"
    ]
  },
  {
    file: "Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueRealizationRepository.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_REPOSITORY",
      "canon.value_realization_ledger",
      "RecordAsync",
      "ListRecentAsync",
      "attribution_caveat"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/Endpoints/Analytics/ValueRealizationEndpoints.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_ENDPOINTS",
      "/api/value/realization",
      "POST /api/value/realization/record",
      "GET /api/value/realization/ledger"
    ]
  },
  {
    file: "Backend/database/scripts/421_phase7_value_realization_ledger.sql",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_SQL",
      "CREATE TABLE IF NOT EXISTS canon.value_realization_ledger",
      "source_value_impact_id",
      "ck_value_realization_realized_band"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueRealizationTrackingTests.cs",
    signals: [
      "T039_Computes_BaselineVsActual_TrackedValue_AndRoi",
      "T039_ChangingActualValue_ChangesRealizedValue",
      "T039_Abstains_WhenMetricWindowsDoNotMatch",
      "T039_Abstains_WhenSourceLinkIsMissing",
      "T039_WorseActualPerformance_CreatesNegativeTrackedValue"
    ]
  },
  {
    file: "Backend/PlantProcess.Infrastructure/Analytics/ValueEngineExtensions.cs",
    signals: [
      "IValueRealizationService",
      "NpgsqlValueRealizationRepository"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/Program.cs",
    signals: [
      "app.MapValueRealizationEndpoints();"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing file" });
    continue;
  }

  const text = read(check.file);

  for (const signal of check.signals) {
    if (!text.includes(signal)) {
      failures.push({ file: check.file, reason: "missing signal: " + signal });
    }
  }
}

if (failures.length) {
  console.error("PPIQ-T039 failed: value-realization ledger implementation is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T039 passed: value-realization ledger, ROI tracking, API routes, SQL and tests are present.");
`);

write("docs/phase7/T039_VALUE_REALIZATION_LEDGER.md", `
# T-039 Value-Realization / Tracked ROI Recording

Marker: PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_CONTRACTS

## Purpose

Track realized value separately from projected value impact.

## Guardrails

- Potential value and realized value are separated.
- Baseline and actual windows must use the same metric and unit.
- Ledger recording requires a source recommendation or value-impact link.
- ROI is based on tracked realized value, not projected value.
- Correlation is not causation.
- Baseline-vs-actual tracked value is not automatic causal attribution.

## Backend routes

- GET /api/value/realization/contract
- POST /api/value/realization/calculate
- POST /api/value/realization/record
- GET /api/value/realization/ledger

## Validation

Run:

    node tools/phase7/validate-t039-value-realization-ledger.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase7_ValueRealizationTrackingTests --no-build
`);

run("node --check T-039 validator", "node", ["--check", "tools/phase7/validate-t039-value-realization-ledger.cjs"]);
run("T-039 validator", "node", ["tools/phase7/validate-t039-value-realization-ledger.cjs"]);
run("Backend build after T-039", "dotnet", ["build", "Backend"]);
run("T-039 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase7_ValueRealizationTrackingTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-039 completed: value-realization ledger and tracked ROI are green.");
console.log("=================================================================================================");