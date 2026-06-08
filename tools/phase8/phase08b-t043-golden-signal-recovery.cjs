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

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
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
  const target = p(".phase8_backup/t043_golden_signal_recovery_" + stamp + "/" + relativePath);
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

const servicePath = "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedCorrelationComputeService.cs";
backup(servicePath);

let service = read(servicePath);

if (!service.includes("PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE")) {
  service = service.replace(
    "/// handle and the mandatory honesty caveat. Deterministic (fixed seed).",
    "/// handle and the mandatory honesty caveat. Deterministic (fixed seed).\n/// PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE certifies true-signal recovery and FDR rejection of injected spurious features."
  );

  write(servicePath, service);
}

write("Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T043GoldenSignalRecoveryTests.cs", `
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE.
/// Golden dataset with known true drivers and injected spurious features.
/// The engine must recover true signals, reject spurious features under BH-FDR,
/// report bootstrap stability, and rerun deterministically.
/// </summary>
public sealed class Phase8_T043GoldenSignalRecoveryTests
{
    private static readonly string[] TrueDrivers =
    {
        "param_true_temperature_driver",
        "param_true_pressure_driver"
    };

    private static readonly string[] InjectedSpurious =
    {
        "param_injected_spurious_alternating",
        "param_injected_spurious_periodic",
        "param_injected_spurious_hash"
    };

    private sealed class FakeLoader : IFeatureVectorLoader
    {
        private readonly AdvancedDataset _dataset;

        public FakeLoader(AdvancedDataset dataset) => _dataset = dataset;

        public Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest request, CancellationToken ct)
            => Task.FromResult(_dataset);
    }

    private sealed class CapturingWriter : IAdvancedResultWriter
    {
        public AdvancedAnalysisRunResult? Last { get; private set; }

        public Task<Guid> WriteAsync(AdvancedAnalysisRequest request, AdvancedAnalysisRunResult result, CancellationToken ct)
        {
            Last = result;
            return Task.FromResult(result.RunId);
        }
    }

    private static AdvancedCorrelationComputeService Service(AdvancedDataset dataset)
        => new(
            new FakeLoader(dataset),
            new CapturingWriter(),
            NullLogger<AdvancedCorrelationComputeService>.Instance);

    private static AdvancedAnalysisRequest Request()
        => new(
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            TenantId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FdrQ: 0.05,
            VifThreshold: 5.0,
            BootstrapIterations: 350,
            PermutationIterations: 120,
            CorrelationId: "PPIQ-T043-GOLDEN-SIGNAL-RECOVERY");

    private static FeatureSeries NumericSeries(string key, Func<int, double> value, int n)
        => new(
            key,
            VariableType.Numeric,
            Enumerable.Range(0, n)
                .Select(i => new FeatureSample($"sample-{i:000}", value(i), null))
                .ToList());

    private static AdvancedDataset GoldenDataset()
    {
        const int n = 180;

        static double Temperature(int i)
            => i + ((i % 7) * 0.01);

        static double Pressure(int i)
            => (37 * i) % 83 + ((i % 5) * 0.01);

        static double ControlledNoise(int i)
            => (((17 * i) % 11) - 5) * 0.03;

        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample(
                $"sample-{i:000}",
                1.4 * Temperature(i) + 3.2 * Pressure(i) + ControlledNoise(i),
                null,
                $"heat-{i:000}"))
            .ToList();

        var features = new List<FeatureSeries>
        {
            // Known true drivers.
            NumericSeries("param_true_temperature_driver", i => Temperature(i), n),
            NumericSeries("param_true_pressure_driver", i => Pressure(i), n),

            // Injected spurious features: deterministic, present in the dataset, but not causal drivers.
            NumericSeries("param_injected_spurious_alternating", i => i % 2 == 0 ? -1.0 : 1.0, n),
            NumericSeries("param_injected_spurious_periodic", i => (i % 9) - 4.0, n),
            NumericSeries("param_injected_spurious_hash", i => ((97 * i + 31) % 101) / 101.0, n),

            // Collinear duplicate to ensure the T-042 VIF guard still cooperates with T-043.
            NumericSeries("param_collinear_temperature_duplicate", i => Temperature(i) * 2.0 + 10.0, n)
        };

        return new AdvancedDataset(
            "defect.edge_crack_rate",
            VariableType.Numeric,
            outcomes,
            features,
            new Dictionary<string, string>(),
            IndependentHeats: n,
            FreshnessFactor: 0.0,
            RequiredFieldCompleteness: 1.0);
    }

    [Fact]
    public async Task T043_Recovers_AtLeastTwo_KnownTrueSignals_UnderFdr()
    {
        var request = Request();
        var result = await Service(GoldenDataset()).ComputeAsync(request, CancellationToken.None);

        Assert.True(result.CanRun);
        Assert.NotEmpty(result.Findings);

        var recovered = result.Findings
            .Where(f => TrueDrivers.Contains(f.FeatureKey))
            .Where(f => f.Significant)
            .Where(f => f.QValue <= request.FdrQ)
            .ToList();

        Assert.True(recovered.Count >= 2, $"Expected both true drivers to recover under FDR q={request.FdrQ}.");

        foreach (var signal in recovered)
        {
            Assert.True(signal.IsStable, $"{signal.FeatureKey} should be bootstrap-stable.");
            Assert.True(signal.StabilityConsistency >= 0.70, $"{signal.FeatureKey} should have strong bootstrap consistency.");
            Assert.True(Math.Abs(signal.EffectSize) >= 0.50, $"{signal.FeatureKey} should carry a practical effect size.");
            Assert.Contains("not causation", signal.HonestyCaveat, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task T043_Rejects_All_InjectedSpuriousFeatures_UnderBenjaminiHochbergFdr()
    {
        var request = Request();
        var result = await Service(GoldenDataset()).ComputeAsync(request, CancellationToken.None);

        Assert.True(result.CanRun);

        foreach (var featureKey in InjectedSpurious)
        {
            var finding = Assert.Single(result.Findings, f => f.FeatureKey == featureKey);

            Assert.False(finding.Significant, $"{featureKey} must be rejected under FDR.");
            Assert.True(finding.QValue > request.FdrQ, $"{featureKey} q-value must stay above FDR threshold.");
        }
    }

    [Fact]
    public async Task T043_Reports_BootstrapStability_For_Every_EmittedFinding()
    {
        var result = await Service(GoldenDataset()).ComputeAsync(Request(), CancellationToken.None);

        Assert.True(result.CanRun);

        Assert.All(result.Findings, finding =>
        {
            Assert.InRange(finding.StabilityConsistency, 0.0, 1.0);
            Assert.False(double.IsNaN(finding.StabilityLower));
            Assert.False(double.IsNaN(finding.StabilityUpper));
            Assert.True(finding.StabilityLower <= finding.StabilityUpper);
        });

        Assert.Contains(result.Findings, f => f.FeatureKey == "param_true_temperature_driver" && f.IsStable);
        Assert.Contains(result.Findings, f => f.FeatureKey == "param_true_pressure_driver" && f.IsStable);
    }

    [Fact]
    public async Task T043_Reruns_Are_Deterministic_Except_RunId()
    {
        var service = Service(GoldenDataset());
        var request = Request();

        var first = await service.ComputeAsync(request, CancellationToken.None);
        var second = await service.ComputeAsync(request, CancellationToken.None);

        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal(first.CanRun, second.CanRun);
        Assert.Equal(first.Findings.Count, second.Findings.Count);
        Assert.Equal(first.Excluded.Count, second.Excluded.Count);

        var firstByKey = first.Findings.ToDictionary(x => x.FeatureKey);
        var secondByKey = second.Findings.ToDictionary(x => x.FeatureKey);

        foreach (var key in firstByKey.Keys)
        {
            Assert.True(secondByKey.ContainsKey(key), $"Second run missed finding {key}.");

            var a = firstByKey[key];
            var b = secondByKey[key];

            Assert.Equal(a.Significant, b.Significant);
            Assert.Equal(a.IsStable, b.IsStable);
            Assert.Equal(Math.Round(a.EffectSize, 8), Math.Round(b.EffectSize, 8));
            Assert.Equal(Math.Round(a.PValue, 8), Math.Round(b.PValue, 8));
            Assert.Equal(Math.Round(a.QValue, 8), Math.Round(b.QValue, 8));
            Assert.Equal(Math.Round(a.StabilityConsistency, 8), Math.Round(b.StabilityConsistency, 8));
        }
    }

    [Fact]
    public async Task T043_Vif_Excludes_Collinear_Duplicate_Without_Losing_TrueRepresentative()
    {
        var result = await Service(GoldenDataset()).ComputeAsync(Request(), CancellationToken.None);

        Assert.True(result.CanRun);

        Assert.Contains(result.Findings, f => f.FeatureKey == "param_true_temperature_driver");
        Assert.DoesNotContain(result.Findings, f => f.FeatureKey == "param_collinear_temperature_duplicate");

        var excluded = Assert.Single(result.Excluded, e => e.FeatureKey == "param_collinear_temperature_duplicate");
        Assert.Contains("VIF", excluded.Reason);
    }
}
`);

write("tools/phase8/validate-t043-golden-signal-recovery.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedCorrelationComputeService.cs",
    signals: [
      "PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE",
      "Benjamini-Hochberg FDR",
      "bootstrap stability",
      "Deterministic (fixed seed)"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T043GoldenSignalRecoveryTests.cs",
    signals: [
      "PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE",
      "T043_Recovers_AtLeastTwo_KnownTrueSignals_UnderFdr",
      "T043_Rejects_All_InjectedSpuriousFeatures_UnderBenjaminiHochbergFdr",
      "T043_Reports_BootstrapStability_For_Every_EmittedFinding",
      "T043_Reruns_Are_Deterministic_Except_RunId",
      "T043_Vif_Excludes_Collinear_Duplicate_Without_Losing_TrueRepresentative",
      "param_true_temperature_driver",
      "param_true_pressure_driver",
      "param_injected_spurious_alternating",
      "param_injected_spurious_periodic",
      "param_injected_spurious_hash",
      "QValue > request.FdrQ",
      "StabilityConsistency >= 0.70"
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
  console.error("PPIQ-T043 failed: golden signal-recovery fixture is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T043 passed: golden dataset recovers true signals, rejects spurious features under FDR, and proves deterministic bootstrap stability.");
`);

write("docs/phase8/T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE.md", `
# T-043 Golden Dataset Signal-Recovery Fixture

Marker: PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE

## Purpose

Certify the advanced correlation engine on a deterministic golden dataset.

## Dataset

Known true drivers:

- param_true_temperature_driver
- param_true_pressure_driver

Injected spurious features:

- param_injected_spurious_alternating
- param_injected_spurious_periodic
- param_injected_spurious_hash

Collinear duplicate:

- param_collinear_temperature_duplicate

## Acceptance

- Recover at least two known true signals.
- Reject all injected spurious features under Benjamini-Hochberg FDR.
- Report bootstrap stability on every emitted finding.
- Reruns are deterministic except RunId.
- VIF removes the collinear duplicate while preserving a representative feature.

## Validation

Run:

    node tools/phase8/validate-t043-golden-signal-recovery.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T043GoldenSignalRecoveryTests --no-build
`);

run("node --check T-043 validator", "node", ["--check", "tools/phase8/validate-t043-golden-signal-recovery.cjs"]);
run("T-043 validator", "node", ["tools/phase8/validate-t043-golden-signal-recovery.cjs"]);
run("Backend build after T-043", "dotnet", ["build", "Backend"]);
run("T-043 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase8_T043GoldenSignalRecoveryTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-043 completed: golden signal-recovery fixture is green.");
console.log("=================================================================================================");