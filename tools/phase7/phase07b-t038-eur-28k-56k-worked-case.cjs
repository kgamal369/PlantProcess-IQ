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
  const target = p(".phase7_backup/t038_eur_28k_56k_worked_case_" + stamp + "/" + relativePath);
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

backup("Backend/PlantProcess.Application/Analytics/Value/Demo/Phase7WorkedCaseFixtures.cs");
backup("Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactWorkedCaseTests.cs");

write("Backend/PlantProcess.Application/Analytics/Value/Demo/Phase7WorkedCaseFixtures.cs", `
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Application.Analytics.Value.Demo;

/// <summary>
/// PPIQ_REALIZATION_T038_EUR_28K_56K_WORKED_CASE_FIXTURE.
/// Deterministic demo fixture for the doctrine worked case.
/// This is a demo/proof fixture, not a hard-coded production claim and not a guaranteed saving.
/// </summary>
public static class Phase7WorkedCaseFixtures
{
    public const string CaseCode = "PPIQ-P07-T038-EDGE-CRACK-EUR-28K-56K";
    public const string FindingRef = "finding:edge-crack-demo-28k-56k";
    public const string CoilId = "DEMO-COIL-EDGE-CRACK-001";
    public const string DefectCode = "EDGE_CRACK";

    public static Phase7WorkedCase EdgeCrackEur28k56k()
    {
        var assumptions = new CostAssumptionSet(
            Version: 38,
            Currency: "EUR",
            CostPerTon: null,
            DowngradeDeltaPerTon: new CostBand(140m, 210m, 280m),
            ScrapCostPerTon: new CostBand(300m, 400m, 500m),
            DowntimeCostPerMin: new CostBand(50m, 75m, 100m),
            GradePremiumPerTon: new CostBand(100m, 150m, 200m),
            EnergyPricePerMwh: null)
        {
            CreatedBy = "phase-07-t038-worked-case",
            CreatedAtUtc = new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero),
            EffectiveFromUtc = new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero)
        };

        var inputs = new ValueImpactInputs(
            FindingRef,
            CoilId,
            DefectCode,
            DefectRateDelta: 0.02m,
            MonthlyVolumeTons: 10_000m,
            ProductionStopMinutes: 0m,
            YieldLossTons: 0m,
            UseScrapCost: false);

        return new Phase7WorkedCase(
            CaseCode,
            "Edge-crack downgrade worked case: EUR 28k-56k/month bounded impact.",
            inputs,
            assumptions,
            ExpectedLow: 28_000m,
            ExpectedMid: 42_000m,
            ExpectedHigh: 56_000m,
            DoctrineNote:
                "200 affected tons/month × EUR 140/210/280 downgrade delta per ton = EUR 28k/42k/56k per month. This is a projected bounded range, not a guaranteed saving.");
    }
}

public sealed record Phase7WorkedCase(
    string CaseCode,
    string Title,
    ValueImpactInputs Inputs,
    CostAssumptionSet Assumptions,
    decimal ExpectedLow,
    decimal ExpectedMid,
    decimal ExpectedHigh,
    string DoctrineNote);
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactWorkedCaseTests.cs", `
using PlantProcess.Application.Analytics.Value;
using PlantProcess.Application.Analytics.Value.Demo;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase7_ValueImpactWorkedCaseTests
{
    [Fact]
    public void T038_Reproduces_Eur28kTo56k_WorkedCase_Exactly()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();

        var result = new ValueImpactEngine().Compute(fixture.Inputs, fixture.Assumptions);

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(28_000m, result.Low);
        Assert.Equal(42_000m, result.Expected);
        Assert.Equal(56_000m, result.High);
        Assert.Equal(fixture.ExpectedLow, result.Low);
        Assert.Equal(fixture.ExpectedMid, result.Expected);
        Assert.Equal(fixture.ExpectedHigh, result.High);
        Assert.Contains("not a guaranteed saving", result.HonestyCaveat);
    }

    [Fact]
    public void T038_WorkedCase_Is_Deterministic_WhenRerun()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();
        var engine = new ValueImpactEngine();

        var first = engine.Compute(fixture.Inputs, fixture.Assumptions);
        var second = engine.Compute(fixture.Inputs, fixture.Assumptions);

        Assert.Equal(first.Low, second.Low);
        Assert.Equal(first.Expected, second.Expected);
        Assert.Equal(first.High, second.High);
        Assert.Equal(first.Terms.Count, second.Terms.Count);
        Assert.Equal(first.Terms[0].InputsJson, second.Terms[0].InputsJson);
    }

    [Fact]
    public void T038_Changing_DriverInput_Changes_Range_Traceably()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();

        var changedInputs = fixture.Inputs with
        {
            MonthlyVolumeTons = 12_000m
        };

        var result = new ValueImpactEngine().Compute(changedInputs, fixture.Assumptions);

        Assert.False(result.IsAbstained);
        Assert.Equal(33_600m, result.Low);
        Assert.Equal(50_400m, result.Expected);
        Assert.Equal(67_200m, result.High);

        var defectTerm = Assert.Single(result.Terms, x => x.Name == "DefectDowngradeOrScrap");
        Assert.Contains("\\\"monthlyVolumeTons\\\":12000", defectTerm.InputsJson);
        Assert.Contains("\\\"affectedTons\\\":240.00", defectTerm.InputsJson);
    }

    [Fact]
    public void T038_Every_WorkedCase_Input_Is_Traceable_To_Provenance()
    {
        var fixture = Phase7WorkedCaseFixtures.EdgeCrackEur28k56k();

        var result = new ValueImpactEngine().Compute(fixture.Inputs, fixture.Assumptions);

        Assert.All(result.Terms, term =>
        {
            Assert.False(string.IsNullOrWhiteSpace(term.Handle.Id));
            Assert.Equal(fixture.Inputs.FindingRef, term.Handle.Id);
            Assert.Contains(fixture.Inputs.FindingRef, term.InputsJson);
        });

        var defectTerm = Assert.Single(result.Terms, x => x.Name == "DefectDowngradeOrScrap");
        Assert.Contains("downgrade_delta_per_ton", defectTerm.InputsJson);
        Assert.Contains("EDGE_CRACK", defectTerm.InputsJson);
        Assert.Equal(28_000m, defectTerm.Low);
        Assert.Equal(42_000m, defectTerm.Expected);
        Assert.Equal(56_000m, defectTerm.High);
    }
}
`);

write("tools/phase7/validate-t038-eur-28k-56k-worked-case.cjs", `
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
    file: "Backend/PlantProcess.Application/Analytics/Value/Demo/Phase7WorkedCaseFixtures.cs",
    signals: [
      "PPIQ_REALIZATION_T038_EUR_28K_56K_WORKED_CASE_FIXTURE",
      "PPIQ-P07-T038-EDGE-CRACK-EUR-28K-56K",
      "DefectRateDelta: 0.02m",
      "MonthlyVolumeTons: 10_000m",
      "new CostBand(140m, 210m, 280m)",
      "ExpectedLow: 28_000m",
      "ExpectedMid: 42_000m",
      "ExpectedHigh: 56_000m",
      "not a guaranteed saving"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactWorkedCaseTests.cs",
    signals: [
      "T038_Reproduces_Eur28kTo56k_WorkedCase_Exactly",
      "T038_WorkedCase_Is_Deterministic_WhenRerun",
      "T038_Changing_DriverInput_Changes_Range_Traceably",
      "T038_Every_WorkedCase_Input_Is_Traceable_To_Provenance",
      "Assert.Equal(28_000m, result.Low)",
      "Assert.Equal(56_000m, result.High)",
      "downgrade_delta_per_ton"
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
  console.error("PPIQ-T038 failed: EUR 28k-56k worked case fixture is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T038 passed: deterministic EUR 28k-56k worked case fixture and provenance tests are present.");
`);

write("docs/phase7/T038_EUR_28K_56K_WORKED_CASE.md", `
# T-038 EUR 28k-56k Worked Case

Marker: PPIQ_REALIZATION_T038_EUR_28K_56K_WORKED_CASE_FIXTURE

## Formula

Affected tons:

    0.02 defect-rate delta × 10,000 monthly tons = 200 affected tons/month

Downgrade delta assumption band:

    EUR 140 / 210 / 280 per ton

Bounded result:

    Low      = 200 × 140 = EUR 28,000/month
    Expected = 200 × 210 = EUR 42,000/month
    High     = 200 × 280 = EUR 56,000/month

## Honesty

This is a deterministic worked-case fixture for demo/proof. It is not a guaranteed saving and not a production claim.

## Validation

Run:

    node tools/phase7/validate-t038-eur-28k-56k-worked-case.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase7_ValueImpactWorkedCaseTests --no-build
`);

run("node --check T-038 validator", "node", ["--check", "tools/phase7/validate-t038-eur-28k-56k-worked-case.cjs"]);
run("T-038 validator", "node", ["tools/phase7/validate-t038-eur-28k-56k-worked-case.cjs"]);
run("Backend build after T-038", "dotnet", ["build", "Backend"]);
run("T-038 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase7_ValueImpactWorkedCaseTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-038 completed: EUR 28k-56k worked case fixture is green.");
console.log("=================================================================================================");