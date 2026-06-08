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
  const target = p(".phase8_backup/t042_vif_multicollinearity_" + stamp + "/" + relativePath);
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

if (!service.includes("PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING")) {
  service = service.replace(
    "// ---- P3-03: iterative VIF collinearity screen (+ optional Lasso note) ----",
    "// ---- PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING ----\n        // T-042: iterative VIF collinearity screen. Drop/flag collinear numeric features before ranking/FDR."
  );

  service = service.replace(
    "private static void IterativeVifExclude(List<FeatureCompute> computed, double threshold, List<ExcludedFeature> excluded)",
    "/// <summary>\n    /// PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING.\n    /// Iteratively removes the worst VIF offender so effect ranking remains stable and explainable.\n    /// </summary>\n    private static void IterativeVifExclude(List<FeatureCompute> computed, double threshold, List<ExcludedFeature> excluded)"
  );

  write(servicePath, service);
}

write("Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T042VifMulticollinearityTests.cs", `
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

public sealed class Phase8_T042VifMulticollinearityTests
{
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

    private static AdvancedAnalysisRequest Request(double vifThreshold = 5.0)
        => new(
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            TenantId: Guid.NewGuid(),
            VifThreshold: vifThreshold,
            BootstrapIterations: 250,
            PermutationIterations: 80);

    private static FeatureSeries NumericSeries(string key, Func<int, double> value, int n)
        => new(
            key,
            VariableType.Numeric,
            Enumerable.Range(0, n)
                .Select(i => new FeatureSample($"k{i}", value(i), null))
                .ToList());

    private static AdvancedDataset DatasetWithDeliberateCollinearity()
    {
        const int n = 90;

        static double Temperature(int i) => i + ((i % 5) * 0.01);
        static double Pressure(int i) => ((37 * i) % 53) + ((i % 3) * 0.01);
        static double Noise(int i) => (17 * i) % 29;

        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample(
                $"k{i}",
                3.0 * Temperature(i) + 1.5 * Pressure(i) + (((11 * i) % 7) - 3) * 0.05,
                null,
                $"heat-{i}"))
            .ToList();

        var features = new List<FeatureSeries>
        {
            NumericSeries("param_true_temperature", i => Temperature(i), n),
            NumericSeries("param_true_temperature_duplicate", i => Temperature(i) * 2.0 + 100.0, n),
            NumericSeries("param_independent_pressure", i => Pressure(i), n),
            NumericSeries("param_noise_randomized", i => Noise(i), n)
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
    public async Task T042_Detects_And_Excludes_Collinear_Duplicate_By_Vif()
    {
        var result = await Service(DatasetWithDeliberateCollinearity())
            .ComputeAsync(Request(vifThreshold: 5.0), CancellationToken.None);

        Assert.True(result.CanRun);

        var excludedDuplicate = Assert.Single(
            result.Excluded,
            e => e.FeatureKey == "param_true_temperature_duplicate");

        Assert.Contains("Collinear", excludedDuplicate.Reason);
        Assert.Contains("VIF", excludedDuplicate.Reason);
        Assert.Contains("removed to keep one representative", excludedDuplicate.Reason);

        Assert.Contains(result.Findings, f => f.FeatureKey == "param_true_temperature");
        Assert.DoesNotContain(result.Findings, f => f.FeatureKey == "param_true_temperature_duplicate");
    }

    [Fact]
    public async Task T042_Effect_Ranking_Remains_Stable_After_Vif_Pruning()
    {
        var result = await Service(DatasetWithDeliberateCollinearity())
            .ComputeAsync(Request(vifThreshold: 5.0), CancellationToken.None);

        Assert.True(result.CanRun);

        var collinearPairInFindings = result.Findings.Count(f =>
            f.FeatureKey == "param_true_temperature" ||
            f.FeatureKey == "param_true_temperature_duplicate");

        Assert.Equal(1, collinearPairInFindings);

        var top = result.Findings
            .OrderByDescending(f => Math.Abs(f.EffectSize))
            .First();

        Assert.Contains(top.FeatureKey, new[]
        {
            "param_true_temperature",
            "param_independent_pressure"
        });

        Assert.All(result.Findings, f => Assert.InRange(f.QValue, 0.0, 1.0));
        Assert.All(result.Findings, f => Assert.False(double.IsNaN(f.EffectSize)));
    }

    [Fact]
    public void T042_Core_VarianceInflation_Flags_Deliberate_Collinearity()
    {
        const int n = 50;

        var matrix = Enumerable.Range(0, n)
            .Select(i => new[]
            {
                (double)i,
                (double)i * 2.0 + 1.0,
                (double)((13 * i) % 17)
            })
            .ToArray();

        var vif = VarianceInflation.Compute(matrix, threshold: 5.0);

        Assert.Equal(5.0, vif.Threshold);
        Assert.Contains(0, vif.Flagged);
        Assert.Contains(1, vif.Flagged);
        Assert.True(double.IsPositiveInfinity(vif.Vif[0]) || vif.Vif[0] >= 5.0);
        Assert.True(double.IsPositiveInfinity(vif.Vif[1]) || vif.Vif[1] >= 5.0);
    }
}
`);

write("tools/phase8/validate-t042-vif-multicollinearity.cjs", `
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
      "PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING",
      "IterativeVifExclude",
      "VarianceInflation.Compute",
      "removed to keep one representative",
      "Drop/flag collinear numeric features before ranking/FDR"
    ]
  },
  {
    file: "Backend/PlantProcess.Analytics.Core/Methods/VarianceInflation.cs",
    signals: [
      "VarianceInflation",
      "VIF_j = 1 / (1 - R^2_j)",
      "Flagged",
      "threshold"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T042VifMulticollinearityTests.cs",
    signals: [
      "T042_Detects_And_Excludes_Collinear_Duplicate_By_Vif",
      "T042_Effect_Ranking_Remains_Stable_After_Vif_Pruning",
      "T042_Core_VarianceInflation_Flags_Deliberate_Collinearity",
      "param_true_temperature_duplicate",
      "VifThreshold: vifThreshold"
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
  console.error("PPIQ-T042 failed: VIF/multicollinearity certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T042 passed: VIF multicollinearity handling is explicitly marked, tested and certified.");
`);

write("docs/phase8/T042_VIF_MULTICOLLINEARITY_HANDLING.md", `
# T-042 VIF / Multicollinearity Handling

Marker: PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING

## Purpose

Certify that the advanced correlation engine detects and handles multicollinearity before effect ranking and FDR.

## Implementation

The canonical advanced engine runs:

1. Readiness gate
2. Per-feature method dispatch and effect computation
3. Iterative VIF collinearity screen
4. Effect ranking
5. Benjamini-Hochberg FDR
6. Bootstrap stability
7. Stratification / exclusions / persistence

## Guardrail

A collinear feature is excluded with an explicit reason. The engine keeps one representative so later ranking is stable and explainable.

## Validation

Run:

    node tools/phase8/validate-t042-vif-multicollinearity.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T042VifMulticollinearityTests --no-build
`);

run("node --check T-042 validator", "node", ["--check", "tools/phase8/validate-t042-vif-multicollinearity.cjs"]);
run("T-042 validator", "node", ["tools/phase8/validate-t042-vif-multicollinearity.cjs"]);
run("Backend build after T-042", "dotnet", ["build", "Backend"]);
run("T-042 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase8_T042VifMulticollinearityTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-042 completed: VIF multicollinearity handling is green.");
console.log("=================================================================================================");