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
  const target = p(".phase7_backup/t037_value_engine_depth_" + stamp + "/" + relativePath);
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

const filesToBackup = [
  "Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs",
  "Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs",
  "Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueImpactRepository.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactEngineDepthTests.cs"
];

for (const file of filesToBackup) backup(file);

write("Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs", `
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_CONTRACTS.
/// A low/expected/high assumption band. Complete only when Low <= Mid <= High.
/// Mid is intentionally retained as the historical API name; Expected is exposed as an alias.
/// </summary>
public sealed record CostBand(decimal Low, decimal Mid, decimal High)
{
    public decimal Expected => Mid;

    public bool IsComplete => Low <= Mid && Mid <= High;

    public string RangeText => $"{Low:N2} <= {Mid:N2} <= {High:N2}";
}

/// <summary>
/// Versioned, tenant-scoped set of cost assumptions. Nulls mean "missing basis", not zero.
/// </summary>
public sealed record CostAssumptionSet(
    int Version,
    string Currency,
    CostBand? CostPerTon,
    CostBand? DowngradeDeltaPerTon,
    CostBand? ScrapCostPerTon,
    CostBand? DowntimeCostPerMin,
    CostBand? GradePremiumPerTon,
    CostBand? EnergyPricePerMwh)
{
    public DateTimeOffset EffectiveFromUtc { get; init; } = DateTimeOffset.MinValue;

    public string? CreatedBy { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }
}

/// <summary>
/// Measured inputs for one finding. ProductionStopMinutes must be attributable production-stop time,
/// not raw equipment-stop time.
/// </summary>
public sealed record ValueImpactInputs(
    string FindingRef,
    string? CoilId,
    string? DefectCode,
    decimal DefectRateDelta,
    decimal MonthlyVolumeTons,
    decimal ProductionStopMinutes,
    decimal YieldLossTons,
    bool UseScrapCost = false)
{
    public decimal DefectAffectedTons => DefectRateDelta * MonthlyVolumeTons;
}

public sealed record ValueImpactTerm(
    string Name,
    string InputsJson,
    decimal Low,
    decimal Mid,
    decimal High,
    ProvenanceHandle Handle)
{
    public decimal Expected => Mid;

    public bool IsMonotonic => Low <= Mid && Mid <= High;

    public decimal RangeWidth => High - Low;
}

public sealed record ValueImpactResult(
    string Currency,
    decimal Low,
    decimal Mid,
    decimal High,
    IReadOnlyList<ValueImpactTerm> Terms,
    int AssumptionVersion,
    bool IsAbstained,
    string? AbstainReason)
{
    public decimal Expected => Mid;

    public bool IsMonotonic => IsAbstained || (Low <= Mid && Mid <= High && Terms.All(x => x.IsMonotonic));

    public decimal RangeWidth => High - Low;

    public string SupportStatus => IsAbstained ? "Abstained" : "BoundedRange";

    public string HonestyCaveat =>
        IsAbstained
            ? "No value claim emitted because the required assumption basis is incomplete."
            : "Projected value range only; not a guaranteed saving. Every figure is tied to assumptions, inputs, and provenance.";

    public static ValueImpactResult Abstained(string currency, int version, string reason)
        => new(currency, 0m, 0m, 0m, Array.Empty<ValueImpactTerm>(), version, true, reason);
}
`);

write("Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs", `
using System.Globalization;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Value;

/// <summary>
/// PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_RANGE.
/// Deterministic bounded euro-value engine:
/// - emits Low / Expected(Mid) / High;
/// - preserves monotonic range ordering even for negative/improvement factors;
/// - abstains instead of fabricating a number when required assumptions are missing;
/// - attaches provenance handle + input JSON to every term.
/// </summary>
public sealed class ValueImpactEngine : IValueImpactEngine
{
    public ValueImpactResult Compute(ValueImpactInputs inputs, CostAssumptionSet assumptions)
    {
        var currency = string.IsNullOrWhiteSpace(assumptions.Currency) ? "EUR" : assumptions.Currency.Trim().ToUpperInvariant();

        var defectCostBand = inputs.UseScrapCost ? assumptions.ScrapCostPerTon : assumptions.DowngradeDeltaPerTon;
        var missing = RequiredAssumptionGaps(inputs, assumptions, defectCostBand);

        if (missing.Count > 0)
        {
            return ValueImpactResult.Abstained(
                currency,
                assumptions.Version,
                "Insufficient basis: missing or invalid required assumption(s): " + string.Join(", ", missing) + ".");
        }

        var findingHandle = ProvenanceHandle.Finding(
            inputs.FindingRef,
            inputs.CoilId is null ? null : "coil:" + inputs.CoilId);

        var terms = new[]
        {
            Term(
                "DefectDowngradeOrScrap",
                Json(new Dictionary<string, object?>
                {
                    ["findingRef"] = inputs.FindingRef,
                    ["coilId"] = inputs.CoilId,
                    ["defectCode"] = inputs.DefectCode,
                    ["defectRateDelta"] = inputs.DefectRateDelta,
                    ["monthlyVolumeTons"] = inputs.MonthlyVolumeTons,
                    ["affectedTons"] = inputs.DefectAffectedTons,
                    ["assumption"] = inputs.UseScrapCost ? "scrap_cost_per_ton" : "downgrade_delta_per_ton"
                }),
                inputs.DefectAffectedTons,
                defectCostBand!,
                findingHandle),

            Term(
                "AttributableProductionStop",
                Json(new Dictionary<string, object?>
                {
                    ["findingRef"] = inputs.FindingRef,
                    ["coilId"] = inputs.CoilId,
                    ["productionStopMinutes"] = inputs.ProductionStopMinutes,
                    ["assumption"] = "downtime_cost_per_min"
                }),
                inputs.ProductionStopMinutes,
                assumptions.DowntimeCostPerMin!,
                findingHandle),

            Term(
                "YieldLoss",
                Json(new Dictionary<string, object?>
                {
                    ["findingRef"] = inputs.FindingRef,
                    ["coilId"] = inputs.CoilId,
                    ["yieldLossTons"] = inputs.YieldLossTons,
                    ["assumption"] = "grade_premium_per_ton"
                }),
                inputs.YieldLossTons,
                assumptions.GradePremiumPerTon!,
                findingHandle)
        };

        var result = new ValueImpactResult(
            currency,
            RoundMoney(terms.Sum(x => x.Low)),
            RoundMoney(terms.Sum(x => x.Mid)),
            RoundMoney(terms.Sum(x => x.High)),
            terms,
            assumptions.Version,
            IsAbstained: false,
            AbstainReason: null);

        if (!result.IsMonotonic)
        {
            return ValueImpactResult.Abstained(
                currency,
                assumptions.Version,
                "Internal value-engine guard: computed value range was not monotonic.");
        }

        return result;
    }

    private static IReadOnlyList<string> RequiredAssumptionGaps(
        ValueImpactInputs inputs,
        CostAssumptionSet assumptions,
        CostBand? defectCostBand)
    {
        var missing = new List<string>();

        RequireBand(
            defectCostBand,
            inputs.UseScrapCost ? "scrap_cost_per_ton" : "downgrade_delta_per_ton",
            missing);

        RequireBand(assumptions.DowntimeCostPerMin, "downtime_cost_per_min", missing);
        RequireBand(assumptions.GradePremiumPerTon, "grade_premium_per_ton", missing);

        return missing;
    }

    private static void RequireBand(CostBand? band, string name, List<string> missing)
    {
        if (band is null)
        {
            missing.Add(name);
            return;
        }

        if (!band.IsComplete)
        {
            missing.Add(name + " must satisfy low <= expected <= high");
        }
    }

    private static ValueImpactTerm Term(
        string name,
        string inputsJson,
        decimal factor,
        CostBand band,
        ProvenanceHandle handle)
    {
        var candidates = new[]
        {
            factor * band.Low,
            factor * band.Mid,
            factor * band.High
        }
        .Select(RoundMoney)
        .OrderBy(x => x)
        .ToArray();

        var expected = RoundMoney(factor * band.Mid);

        return new ValueImpactTerm(
            name,
            inputsJson,
            candidates[0],
            expected,
            candidates[2],
            handle);
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Json(IReadOnlyDictionary<string, object?> values)
    {
        static string Scalar(object? value)
        {
            return value switch
            {
                null => "null",
                string s => "\\\"" + s.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\"",
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

write("Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueImpactRepository.cs", `
using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// PPIQ_REALIZATION_T037_VALUE_IMPACT_REPOSITORY_EVIDENCE.
/// Persists a computed value-impact result, including bounded range, abstain state, inputs and provenance evidence.
/// </summary>
public sealed class NpgsqlValueImpactRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlValueImpactRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<Guid> PersistAsync(
        Guid tenantId,
        string findingRef,
        string? coilId,
        string? defectCode,
        ValueImpactResult result,
        CancellationToken ct)
    {
        var evidence = JsonSerializer.Serialize(new
        {
            supportStatus = result.SupportStatus,
            result.Currency,
            result.Low,
            Expected = result.Expected,
            result.Mid,
            result.High,
            result.RangeWidth,
            result.IsMonotonic,
            result.HonestyCaveat,
            assumptionVersion = result.AssumptionVersion,
            terms = result.Terms.Select(t => new
            {
                t.Name,
                t.InputsJson,
                t.Low,
                Expected = t.Expected,
                t.Mid,
                t.High,
                t.RangeWidth,
                t.IsMonotonic,
                handle = new
                {
                    kind = t.Handle.Kind.ToString(),
                    id = t.Handle.Id,
                    detail = t.Handle.Detail
                }
            }),
            abstained = result.IsAbstained,
            abstainReason = result.AbstainReason
        });

        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO canon.value_impact (tenant_id, finding_ref, coil_id, defect_code, currency, " +
            "impact_eur_low, impact_eur_mid, impact_eur_high, assumption_version, is_abstained, evidence) " +
            "VALUES (@t,@f,@coil,@defect,@ccy,@low,@mid,@high,@ver,@abstain,@ev::jsonb) RETURNING id");

        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("f", findingRef);
        cmd.Parameters.AddWithValue("coil", (object?)coilId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("defect", (object?)defectCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ccy", result.Currency);
        cmd.Parameters.AddWithValue("low", result.Low);
        cmd.Parameters.AddWithValue("mid", result.Expected);
        cmd.Parameters.AddWithValue("high", result.High);
        cmd.Parameters.AddWithValue("ver", result.AssumptionVersion);
        cmd.Parameters.AddWithValue("abstain", result.IsAbstained);
        cmd.Parameters.AddWithValue("ev", evidence);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id is Guid g ? g : Guid.Empty;
    }
}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactEngineDepthTests.cs", `
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase7_ValueImpactEngineDepthTests
{
    private static CostAssumptionSet CompleteBands(int version = 7) => new(
        version,
        "EUR",
        CostPerTon: null,
        DowngradeDeltaPerTon: new CostBand(80m, 100m, 120m),
        ScrapCostPerTon: new CostBand(250m, 350m, 500m),
        DowntimeCostPerMin: new CostBand(40m, 60m, 80m),
        GradePremiumPerTon: new CostBand(150m, 200m, 250m),
        EnergyPricePerMwh: null);

    [Fact]
    public void T037_ComputesBoundedExpectedRange_WithProvenanceAndNoGuarantee()
    {
        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-001",
                "COIL-0001",
                "EDGE_CRACK",
                DefectRateDelta: 0.02m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 120m,
                YieldLossTons: 50m),
            CompleteBands());

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal(28300.00m, result.Low);
        Assert.Equal(37200.00m, result.Expected);
        Assert.Equal(46100.00m, result.High);
        Assert.Equal(result.Mid, result.Expected);
        Assert.Equal("BoundedRange", result.SupportStatus);
        Assert.Contains("not a guaranteed saving", result.HonestyCaveat);
        Assert.All(result.Terms, term =>
        {
            Assert.True(term.IsMonotonic);
            Assert.Contains("findingRef", term.InputsJson);
            Assert.False(string.IsNullOrWhiteSpace(term.Handle.Id));
        });
    }

    [Fact]
    public void T037_Abstains_WhenScrapBandRequiredButMissing()
    {
        var assumptions = CompleteBands() with { ScrapCostPerTon = null };

        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-002",
                "COIL-0002",
                "SCRAP_RISK",
                DefectRateDelta: 0.01m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 10m,
                YieldLossTons: 5m,
                UseScrapCost: true),
            assumptions);

        Assert.True(result.IsAbstained);
        Assert.Equal(0m, result.Expected);
        Assert.Contains("scrap_cost_per_ton", result.AbstainReason);
        Assert.Contains("No value claim emitted", result.HonestyCaveat);
    }

    [Fact]
    public void T037_Abstains_WhenBandIsNotLowExpectedHigh()
    {
        var assumptions = CompleteBands() with
        {
            DowntimeCostPerMin = new CostBand(80m, 60m, 40m)
        };

        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-003",
                null,
                null,
                DefectRateDelta: 0.01m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 60m,
                YieldLossTons: 10m),
            assumptions);

        Assert.True(result.IsAbstained);
        Assert.Contains("downtime_cost_per_min", result.AbstainReason);
        Assert.Contains("low <= expected <= high", result.AbstainReason);
    }

    [Fact]
    public void T037_NegativeImprovementFactor_StillProducesMonotonicBoundedRange()
    {
        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-004",
                "COIL-0004",
                "EDGE_CRACK",
                DefectRateDelta: -0.02m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 0m,
                YieldLossTons: 0m),
            CompleteBands());

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal(-24000.00m, result.Low);
        Assert.Equal(-20000.00m, result.Expected);
        Assert.Equal(-16000.00m, result.High);
    }
}
`);

write("tools/phase7/validate-t037-value-engine-depth.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs",
    signals: [
      "PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_CONTRACTS",
      "public decimal Expected => Mid",
      "public bool IsMonotonic",
      "HonestyCaveat"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs",
    signals: [
      "PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_RANGE",
      "RequiredAssumptionGaps",
      "missing or invalid required assumption",
      "OrderBy(x => x)",
      "not raw equipment-stop time"
    ]
  },
  {
    file: "Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueImpactRepository.cs",
    signals: [
      "PPIQ_REALIZATION_T037_VALUE_IMPACT_REPOSITORY_EVIDENCE",
      "supportStatus",
      "Expected = result.Expected",
      "HonestyCaveat",
      "RangeWidth"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactEngineDepthTests.cs",
    signals: [
      "T037_ComputesBoundedExpectedRange_WithProvenanceAndNoGuarantee",
      "T037_Abstains_WhenScrapBandRequiredButMissing",
      "T037_Abstains_WhenBandIsNotLowExpectedHigh",
      "T037_NegativeImprovementFactor_StillProducesMonotonicBoundedRange"
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
  console.error("PPIQ-T037 failed: value engine depth implementation is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T037 passed: bounded value engine, abstain guard, evidence persistence and tests are present.");
`);

write("docs/phase7/T037_VALUE_ENGINE_DEPTH.md", `
# T-037 Value Engine Depth

Marker: PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_RANGE

## Result

The value engine now emits bounded Low / Expected / High ranges, abstains when required assumptions are missing or invalid, preserves provenance handles, and persists richer evidence JSON.

## Honesty rule

This engine must never emit guaranteed savings. It emits a projected bounded range only.

## Validation

Run:

    node tools/phase7/validate-t037-value-engine-depth.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase7_ValueImpactEngineDepthTests
`);

run("node --check T-037 validator", "node", ["--check", "tools/phase7/validate-t037-value-engine-depth.cjs"]);
run("T-037 validator", "node", ["tools/phase7/validate-t037-value-engine-depth.cjs"]);
run("Backend build after T-037", "dotnet", ["build", "Backend"]);
run("T-037 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase7_ValueImpactEngineDepthTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-037 completed: bounded value engine depth is green.");
console.log("=================================================================================================");