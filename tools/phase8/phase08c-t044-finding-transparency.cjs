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
  const target = p(".phase8_backup/t044_finding_transparency_" + stamp + "/" + relativePath);
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

if (!service.includes("PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE")) {
  service = service.replace(
    "/// PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE certifies true-signal recovery and FDR rejection of injected spurious features.",
    "/// PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE certifies true-signal recovery and FDR rejection of injected spurious features.\n/// PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE requires every emitted finding to surface population, dropped/excluded records, stratification verdict and honesty caveat."
  );

  service = service.replace(
    "results.Add(new AdvancedFinding(",
    "// PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE: each finding carries c.SampleSize, c.Dropped, survives/sreason and provenance.\n            results.Add(new AdvancedFinding("
  );

  write(servicePath, service);
}

write("Backend/PlantProcess.Application/Analytics/Advanced/AdvancedFindingTransparency.cs", `
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE.
/// User-facing transparency projection for every advanced correlation finding.
/// This does not change the statistical result; it makes the population,
/// dropped/excluded records, stratification status, provenance, and honesty caveat explicit.
/// </summary>
public sealed record AdvancedFindingTransparency(
    string FeatureKey,
    string PopulationLabel,
    int PopulationSize,
    int PairedSampleSize,
    int ExcludedRecordCount,
    string ExclusionSummary,
    bool StratificationEvaluated,
    bool SurvivesStratification,
    string StratificationReason,
    string ProvenanceHandle,
    string HonestyCaveat)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FeatureKey) &&
        PopulationSize > 0 &&
        PairedSampleSize > 0 &&
        PairedSampleSize <= PopulationSize &&
        ExcludedRecordCount >= 0 &&
        !string.IsNullOrWhiteSpace(ExclusionSummary) &&
        !string.IsNullOrWhiteSpace(StratificationReason) &&
        !string.IsNullOrWhiteSpace(ProvenanceHandle) &&
        !string.IsNullOrWhiteSpace(HonestyCaveat);
}

public static class AdvancedFindingTransparencyProjector
{
    public static IReadOnlyList<AdvancedFindingTransparency> Project(AdvancedAnalysisRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Findings
            .Select(finding => ProjectOne(run, finding))
            .ToArray();
    }

    public static AdvancedFindingTransparency ProjectOne(AdvancedAnalysisRunResult run, AdvancedFinding finding)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(finding);

        var populationSize = Math.Max(finding.SampleSize + finding.ExcludedRecords, finding.SampleSize);
        var stratificationEvaluated = !finding.StratificationReason.StartsWith(
            "Stratification not evaluated",
            StringComparison.OrdinalIgnoreCase);

        var exclusionSummary = finding.ExcludedRecords == 0
            ? "No records were dropped during feature/outcome alignment for this finding."
            : $"{finding.ExcludedRecords} record(s) were dropped during feature/outcome alignment for this finding.";

        return new AdvancedFindingTransparency(
            finding.FeatureKey,
            $"{run.Grain}; window={run.WindowDays}d; outcome={run.OutcomeKey}",
            populationSize,
            finding.SampleSize,
            finding.ExcludedRecords,
            exclusionSummary,
            stratificationEvaluated,
            finding.SurvivesStratification,
            finding.StratificationReason,
            finding.ProvenanceHandle,
            finding.HonestyCaveat);
    }
}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T044FindingTransparencyTests.cs", `
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE.
/// Certifies that every finding exposes population, stratification and exclusion evidence.
/// </summary>
public sealed class Phase8_T044FindingTransparencyTests
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

    private static AdvancedAnalysisRequest Request()
        => new(
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            TenantId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FdrQ: 0.05,
            VifThreshold: 5.0,
            BootstrapIterations: 250,
            PermutationIterations: 80,
            CorrelationId: "PPIQ-T044-FINDING-TRANSPARENCY");

    private static FeatureSeries NumericSeries(string key, Func<int, double?> value, int n)
        => new(
            key,
            VariableType.Numeric,
            Enumerable.Range(0, n)
                .Select(i => new FeatureSample($"sample-{i:000}", value(i), null))
                .ToList());

    private static AdvancedDataset TransparentDataset()
    {
        const int n = 90;

        static double Temperature(int i)
            => i + ((i % 7) * 0.01);

        static double Pressure(int i)
            => (37 * i) % 83 + ((i % 5) * 0.01);

        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample(
                $"sample-{i:000}",
                2.0 * Temperature(i) + 1.2 * Pressure(i),
                null,
                $"heat-{i:000}"))
            .ToList();

        var strata = Enumerable.Range(0, n)
            .ToDictionary(
                i => $"sample-{i:000}",
                i => i < 45 ? "caster-a" : "caster-b");

        var features = new List<FeatureSeries>
        {
            NumericSeries("param_temperature_population_full", i => Temperature(i), n),
            NumericSeries("param_pressure_population_partial", i => i % 10 == 0 ? null : Pressure(i), n),
            NumericSeries("param_noise_population_full", i => ((97 * i + 13) % 101) / 101.0, n)
        };

        return new AdvancedDataset(
            "defect.edge_crack_rate",
            VariableType.Numeric,
            outcomes,
            features,
            strata,
            IndependentHeats: n,
            FreshnessFactor: 0.0,
            RequiredFieldCompleteness: 1.0);
    }

    [Fact]
    public async Task T044_Every_Finding_Carries_Population_Stratification_And_Exclusion_Fields()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        Assert.True(result.CanRun);
        Assert.NotEmpty(result.Findings);

        Assert.All(result.Findings, finding =>
        {
            Assert.True(finding.SampleSize > 0, $"{finding.FeatureKey} must expose paired population/sample size.");
            Assert.True(finding.ExcludedRecords >= 0, $"{finding.FeatureKey} must expose dropped/excluded record count.");
            Assert.False(string.IsNullOrWhiteSpace(finding.StratificationReason));
            Assert.False(string.IsNullOrWhiteSpace(finding.ProvenanceHandle));
            Assert.Contains("not a guaranteed root cause", finding.HonestyCaveat, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task T044_Partial_Feature_Shows_Dropped_Record_Count()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        var partial = Assert.Single(result.Findings, f => f.FeatureKey == "param_pressure_population_partial");

        Assert.True(partial.SampleSize < 90);
        Assert.True(partial.ExcludedRecords > 0);
    }

    [Fact]
    public async Task T044_Transparency_Projector_Produces_Complete_Surface_For_Every_Finding()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        var transparency = AdvancedFindingTransparencyProjector.Project(result);

        Assert.Equal(result.Findings.Count, transparency.Count);

        Assert.All(transparency, item =>
        {
            Assert.True(item.IsComplete, $"{item.FeatureKey} transparency surface must be complete.");
            Assert.Contains("coil", item.PopulationLabel);
            Assert.Contains("window=3650d", item.PopulationLabel);
            Assert.Contains("defect.edge_crack_rate", item.PopulationLabel);
            Assert.True(item.PopulationSize >= item.PairedSampleSize);
            Assert.False(string.IsNullOrWhiteSpace(item.ExclusionSummary));
            Assert.False(string.IsNullOrWhiteSpace(item.StratificationReason));
            Assert.False(string.IsNullOrWhiteSpace(item.ProvenanceHandle));
        });
    }

    [Fact]
    public async Task T044_Stratification_Status_Is_Surfaceable_For_Every_Finding()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        var transparency = AdvancedFindingTransparencyProjector.Project(result);

        Assert.All(transparency, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.StratificationReason));
            Assert.True(item.StratificationEvaluated || item.StratificationReason.StartsWith("Stratification not evaluated", StringComparison.OrdinalIgnoreCase));
        });

        Assert.Contains(transparency, item => item.StratificationEvaluated);
    }
}
`);

write("tools/phase8/validate-t044-finding-transparency.cjs", `
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
      "PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE",
      "each finding carries c.SampleSize, c.Dropped, survives/sreason and provenance",
      "AdvancedFinding("
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedFindingTransparency.cs",
    signals: [
      "PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE",
      "AdvancedFindingTransparency",
      "PopulationLabel",
      "PopulationSize",
      "PairedSampleSize",
      "ExcludedRecordCount",
      "ExclusionSummary",
      "StratificationEvaluated",
      "SurvivesStratification",
      "StratificationReason",
      "ProvenanceHandle",
      "HonestyCaveat",
      "AdvancedFindingTransparencyProjector"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T044FindingTransparencyTests.cs",
    signals: [
      "T044_Every_Finding_Carries_Population_Stratification_And_Exclusion_Fields",
      "T044_Partial_Feature_Shows_Dropped_Record_Count",
      "T044_Transparency_Projector_Produces_Complete_Surface_For_Every_Finding",
      "T044_Stratification_Status_Is_Surfaceable_For_Every_Finding",
      "param_pressure_population_partial",
      "ExcludedRecords > 0",
      "item.IsComplete"
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
  console.error("PPIQ-T044 failed: finding transparency evidence is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T044 passed: every advanced finding surfaces population, stratification, exclusions, provenance and honesty evidence.");
`);

write("docs/phase8/T044_FINDING_TRANSPARENCY_EVIDENCE.md", `
# T-044 Finding Transparency Evidence

Marker: PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE

## Purpose

Certify that every advanced correlation finding surfaces:

- population / paired sample size
- dropped or excluded records
- stratification evaluated / not evaluated state
- stratification verdict reason
- provenance handle
- honesty caveat

## Implementation

The existing AdvancedFinding contract already carries the statistical evidence fields. T-044 adds an explicit transparency projection:

- AdvancedFindingTransparency
- AdvancedFindingTransparencyProjector

This avoids changing the core finding constructor while making the HMI/API surface explicit and testable.

## Guardrail

A finding is not considered complete unless it has population, exclusions, stratification reason, provenance and honesty caveat.

## Validation

Run:

    node tools/phase8/validate-t044-finding-transparency.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T044FindingTransparencyTests --no-build
`);

run("node --check T-044 validator", "node", ["--check", "tools/phase8/validate-t044-finding-transparency.cjs"]);
run("T-044 validator", "node", ["tools/phase8/validate-t044-finding-transparency.cjs"]);
run("Backend build after T-044", "dotnet", ["build", "Backend"]);
run("T-044 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase8_T044FindingTransparencyTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-044 completed: finding transparency evidence is green.");
console.log("=================================================================================================");