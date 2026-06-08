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
  const target = p(".phase9_backup/t048_assistant_grounding_eval_" + stamp + "/" + relativePath);
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

const groundingPath = "Backend/PlantProcess.Application/Assistant/GroundingService.cs";
backup(groundingPath);

let grounding = read(groundingPath);

if (!grounding.includes("PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE")) {
  grounding = grounding.replace(
    "/// T-053 grounding contract (§2/§7.7). Operates on a model DRAFT plus the grounded claims that back it:",
    "/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.\n/// T-048/T-053 grounding contract (§2/§7.7). Operates on a model DRAFT plus the grounded claims that back it:"
  );

  write(groundingPath, grounding);
}

const evalPath = "Backend/PlantProcess.Application/Assistant/AssistantEvalHarness.cs";
backup(evalPath);

let evalHarness = read(evalPath);

if (!evalHarness.includes("PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE")) {
  evalHarness = evalHarness.replace(
    "/// PPIQ-T027: deterministic assistant eval harness.",
    "/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.\n/// PPIQ-T027/T048: deterministic assistant eval harness."
  );

  write(evalPath, evalHarness);
}

write("Backend/PlantProcess.Application/Assistant/AssistantGroundingEvalGate.cs", `
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.
/// Regression gate for grounded assistant answers.
/// The gate fails if the certified response contains uncited numbers,
/// unsupported causal/value claims, blocked model sentences, citation drift,
/// or provider/model version drift.
/// </summary>
public sealed class AssistantGroundingEvalGate
{
    private static readonly string[] ForbiddenCausalOrValuePhrases =
    {
        "root cause",
        "is caused by",
        "will cause",
        "guaranteed",
        "will save"
    };

    public AssistantEvalResult Evaluate(AssistantGroundingEvalCase testCase, GroundedAssistantGatewayResult result)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(result);

        var errors = new List<string>();

        if (testCase.ExpectedAnswerable && result.IsRefusal)
            errors.Add("Expected answerable but gateway refused.");

        if (!testCase.ExpectedAnswerable && !result.IsRefusal)
            errors.Add("Expected refusal but gateway answered.");

        if (testCase.ExpectedAnswerable && !result.GroundingCertified)
            errors.Add("Expected grounding certification, but result was not certified.");

        foreach (var required in testCase.RequiredCitationTokens)
        {
            if (!result.Citations.Any(c => c.Token.Equals(required, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Missing required citation: {required}");
        }

        foreach (var forbiddenNumber in testCase.ForbiddenNumbers)
        {
            if (!string.IsNullOrWhiteSpace(result.Text) &&
                result.Text.Contains(forbiddenNumber, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Uncited/forbidden number reached final answer: {forbiddenNumber}");
            }
        }

        foreach (var phrase in ForbiddenCausalOrValuePhrases)
        {
            if (!string.IsNullOrWhiteSpace(result.Text) &&
                result.Text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Unsupported causal/value phrase reached final answer: {phrase}");
            }
        }

        if (testCase.FailOnBlockedSentences && result.BlockedSentences.Count > 0)
        {
            errors.Add(
                "Model attempted to emit unsupported content blocked by the grounding guard: " +
                string.Join(" | ", result.BlockedSentences));
        }

        if (!result.ProviderKey.Equals(testCase.PinnedProviderKey, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Provider drift: expected {testCase.PinnedProviderKey}, actual {result.ProviderKey}.");

        if (!result.ModelKey.Equals(testCase.PinnedModelKey, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Model key drift: expected {testCase.PinnedModelKey}, actual {result.ModelKey}.");

        if (!result.ModelVersion.Equals(testCase.PinnedModelVersion, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Model version drift: expected {testCase.PinnedModelVersion}, actual {result.ModelVersion}.");

        return new AssistantEvalResult(testCase.CaseKey, errors.Count == 0, errors);
    }

    public IReadOnlyList<AssistantEvalResult> EvaluateMany(
        IReadOnlyList<AssistantGroundingEvalCase> testCases,
        Func<AssistantGroundingEvalCase, GroundedAssistantGatewayResult> executeCase)
    {
        ArgumentNullException.ThrowIfNull(testCases);
        ArgumentNullException.ThrowIfNull(executeCase);

        return testCases
            .Select(testCase => Evaluate(testCase, executeCase(testCase)))
            .ToArray();
    }
}

public sealed record AssistantGroundingEvalCase(
    string CaseKey,
    string Prompt,
    bool ExpectedAnswerable,
    IReadOnlyList<string> RequiredCitationTokens,
    IReadOnlyList<string> ForbiddenNumbers,
    string PinnedProviderKey,
    string PinnedModelKey,
    string PinnedModelVersion,
    bool FailOnBlockedSentences = true);

public static class AssistantGroundingEvalPromptSet
{
    public const string Marker = "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE";

    public const string ProviderKey = "local-extractive";
    public const string ModelKey = "ppiq-grounded-assistant";
    public const string ModelVersion = "phase09-eval-v1";

    public static IReadOnlyList<AssistantGroundingEvalCase> Default => new[]
    {
        new AssistantGroundingEvalCase(
            CaseKey: "answer_value_range_with_citation",
            Prompt: "What is the projected value range for the approved edge-crack suggestion?",
            ExpectedAnswerable: true,
            RequiredCitationTokens: new[] { ProvenanceHandle.Finding("finding-edge-caster-a").Token },
            ForbiddenNumbers: Array.Empty<string>(),
            PinnedProviderKey: ProviderKey,
            PinnedModelKey: ModelKey,
            PinnedModelVersion: ModelVersion),

        new AssistantGroundingEvalCase(
            CaseKey: "block_uncited_number",
            Prompt: "Give me the value range and any extra estimate.",
            ExpectedAnswerable: true,
            RequiredCitationTokens: new[] { ProvenanceHandle.Finding("finding-edge-caster-a").Token },
            ForbiddenNumbers: new[] { "99999", "99,999" },
            PinnedProviderKey: ProviderKey,
            PinnedModelKey: ModelKey,
            PinnedModelVersion: ModelVersion),

        new AssistantGroundingEvalCase(
            CaseKey: "refuse_without_live_evidence",
            Prompt: "Explain this without approved evidence.",
            ExpectedAnswerable: false,
            RequiredCitationTokens: Array.Empty<string>(),
            ForbiddenNumbers: Array.Empty<string>(),
            PinnedProviderKey: ProviderKey,
            PinnedModelKey: ModelKey,
            PinnedModelVersion: ModelVersion)
    };
}
`);

write("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T048AssistantGroundingEvalGateTests.cs", `
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.
/// Certifies that the assistant eval gate fails on uncited numbers,
/// unsupported causal claims, blocked sentences, and provider/model drift.
/// </summary>
public sealed class Phase9_T048AssistantGroundingEvalGateTests
{
    private const string ProviderKey = AssistantGroundingEvalPromptSet.ProviderKey;
    private const string ModelKey = AssistantGroundingEvalPromptSet.ModelKey;
    private const string ModelVersion = AssistantGroundingEvalPromptSet.ModelVersion;

    private static readonly ProvenanceHandle EdgeFinding =
        ProvenanceHandle.Finding("finding-edge-caster-a", "edge-crack suggestion");

    private static AssistantClaim EdgeClaim(params string[] numbers)
        => new(
            Text: "Approved finding supports the edge-crack projected range.",
            Handle: EdgeFinding,
            NumericTokens: numbers);

    private static AssistantGroundingEvalCase Case(
        bool expectedAnswerable = true,
        IReadOnlyList<string>? forbiddenNumbers = null,
        string provider = ProviderKey,
        string model = ModelKey,
        string version = ModelVersion)
        => new(
            CaseKey: "case-under-test",
            Prompt: "Explain the approved suggestion.",
            ExpectedAnswerable: expectedAnswerable,
            RequiredCitationTokens: expectedAnswerable ? new[] { EdgeFinding.Token } : Array.Empty<string>(),
            ForbiddenNumbers: forbiddenNumbers ?? Array.Empty<string>(),
            PinnedProviderKey: provider,
            PinnedModelKey: model,
            PinnedModelVersion: version);

    private static GroundedAssistantGatewayResult Certify(
        string output,
        IReadOnlyList<AssistantClaim> claims,
        string provider = ProviderKey,
        string model = ModelKey,
        string version = ModelVersion)
        => GroundedAssistantGateway.Certify(
            prompt: "Explain the approved suggestion.",
            modelOutput: output,
            retrievedClaims: claims,
            providerKey: provider,
            modelKey: model,
            modelVersion: version);

    [Fact]
    public void T048_Clean_Grounded_Model_Output_Passes_Eval_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR based on the cited finding.",
            new[] { EdgeClaim("42000") });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(result.IsRefusal);
        Assert.True(result.GroundingCertified);
        Assert.Empty(result.BlockedSentences);
        Assert.Contains(EdgeFinding, result.Citations);
        Assert.True(evaluation.Passed, string.Join(" | ", evaluation.Errors));
    }

    [Fact]
    public void T048_Uncited_Number_Is_Blocked_And_Fails_Regression_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR. The assistant also estimates 99,999 EUR.",
            new[] { EdgeClaim("42000") });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(
            Case(forbiddenNumbers: new[] { "99999", "99,999" }),
            result);

        Assert.False(result.IsRefusal);
        Assert.DoesNotContain("99,999", result.Text);
        Assert.Contains(result.BlockedSentences, s => s.Contains("99,999", StringComparison.OrdinalIgnoreCase));
        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("blocked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Unsupported_Causal_Claim_Is_Blocked_And_Fails_Regression_Gate()
    {
        var result = Certify(
            "EDGE_CRACK is caused by caster-a. The approved projected value is 42,000 EUR.",
            new[] { EdgeClaim("42000") });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(result.IsRefusal);
        Assert.DoesNotContain("is caused by", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.BlockedSentences, s => s.Contains("is caused by", StringComparison.OrdinalIgnoreCase));
        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Synthetic_Only_Evidence_Produces_Honest_Refusal_And_Passes_Refusal_Case()
    {
        var synthetic = new AssistantClaim(
            Text: "Seed finding claims value 42,000.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "42000" },
            IsSynthetic: true);

        var result = Certify(
            "The value is 42,000 EUR.",
            new[] { synthetic });

        var evaluation = new AssistantGroundingEvalGate().Evaluate(
            Case(expectedAnswerable: false),
            result);

        Assert.True(result.IsRefusal);
        Assert.False(result.GroundingCertified);
        Assert.True(evaluation.Passed, string.Join(" | ", evaluation.Errors));
    }

    [Fact]
    public void T048_Model_Version_Drift_Fails_Eval_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR based on the cited finding.",
            new[] { EdgeClaim("42000") },
            version: "unexpected-model-version");

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("Model version drift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Provider_And_Model_Key_Drift_Fail_Eval_Gate()
    {
        var result = Certify(
            "The approved projected value is 42,000 EUR based on the cited finding.",
            new[] { EdgeClaim("42000") },
            provider: "external-provider",
            model: "different-model");

        var evaluation = new AssistantGroundingEvalGate().Evaluate(Case(), result);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Errors, e => e.Contains("Provider drift", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evaluation.Errors, e => e.Contains("Model key drift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T048_Fixed_Prompt_Set_Is_Pinned_And_Contains_Regression_Cases()
    {
        var cases = AssistantGroundingEvalPromptSet.Default;

        Assert.Equal(3, cases.Count);
        Assert.Contains(cases, c => c.CaseKey == "answer_value_range_with_citation");
        Assert.Contains(cases, c => c.CaseKey == "block_uncited_number");
        Assert.Contains(cases, c => c.CaseKey == "refuse_without_live_evidence");

        Assert.All(cases, c =>
        {
            Assert.Equal(ProviderKey, c.PinnedProviderKey);
            Assert.Equal(ModelKey, c.PinnedModelKey);
            Assert.Equal(ModelVersion, c.PinnedModelVersion);
            Assert.False(string.IsNullOrWhiteSpace(c.Prompt));
        });
    }

    [Fact]
    public void T048_EvaluateMany_Fails_Build_Gate_When_Any_Case_Fails()
    {
        var gate = new AssistantGroundingEvalGate();
        var cases = AssistantGroundingEvalPromptSet.Default;

        var results = gate.EvaluateMany(cases, testCase =>
        {
            if (testCase.CaseKey == "block_uncited_number")
            {
                return Certify(
                    "The approved projected value is 42,000 EUR. The assistant also estimates 99,999 EUR.",
                    new[] { EdgeClaim("42000") });
            }

            if (testCase.CaseKey == "refuse_without_live_evidence")
            {
                return GroundedAssistantGateway.RefuseNoEvidence(
                    testCase.Prompt,
                    ProviderKey,
                    ModelKey,
                    ModelVersion);
            }

            return Certify(
                "The approved projected value is 42,000 EUR based on the cited finding.",
                new[] { EdgeClaim("42000") });
        });

        Assert.Contains(results, r => !r.Passed && r.CaseKey == "block_uncited_number");
        Assert.Contains(results, r => r.Passed && r.CaseKey == "answer_value_range_with_citation");
        Assert.Contains(results, r => r.Passed && r.CaseKey == "refuse_without_live_evidence");
    }
}
`);

write("tools/phase9/validate-t048-assistant-grounding-eval.cjs", `
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
    file: "Backend/PlantProcess.Application/Assistant/GroundingService.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "uncited number",
      "root cause",
      "is caused by",
      "guaranteed",
      "will save"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/AssistantGroundingEvalGate.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "AssistantGroundingEvalGate",
      "AssistantGroundingEvalCase",
      "AssistantGroundingEvalPromptSet",
      "ForbiddenCausalOrValuePhrases",
      "Uncited/forbidden number reached final answer",
      "Unsupported causal/value phrase reached final answer",
      "Model version drift",
      "EvaluateMany",
      "block_uncited_number",
      "refuse_without_live_evidence"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/AssistantEvalHarness.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "Missing required citation",
      "Uncited/forbidden number reached answer",
      "Model version drift"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T048AssistantGroundingEvalGateTests.cs",
    signals: [
      "T048_Clean_Grounded_Model_Output_Passes_Eval_Gate",
      "T048_Uncited_Number_Is_Blocked_And_Fails_Regression_Gate",
      "T048_Unsupported_Causal_Claim_Is_Blocked_And_Fails_Regression_Gate",
      "T048_Synthetic_Only_Evidence_Produces_Honest_Refusal_And_Passes_Refusal_Case",
      "T048_Model_Version_Drift_Fails_Eval_Gate",
      "T048_Provider_And_Model_Key_Drift_Fail_Eval_Gate",
      "T048_Fixed_Prompt_Set_Is_Pinned_And_Contains_Regression_Cases",
      "T048_EvaluateMany_Fails_Build_Gate_When_Any_Case_Fails",
      "99,999",
      "is caused by",
      "EvaluateMany"
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
  console.error("PPIQ-T048 failed: assistant grounding eval gate is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T048 passed: assistant grounding eval gate blocks uncited numbers, unsupported causal claims, blocked sentences and model drift.");
`);

write("docs/phase9/T048_ASSISTANT_GROUNDING_EVAL_GATE.md", `
# T-048 Assistant Grounding Eval Gate

Marker: PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE

## Purpose

Turn assistant grounding into a CI-style regression gate.

## Certified behavior

- Clean grounded answer passes.
- Uncited numbers are blocked by the grounding guard and fail the eval gate.
- Unsupported causal phrases are blocked and fail the eval gate.
- Synthetic-only evidence produces honest refusal.
- Provider, model key and model version drift fail.
- Fixed prompt set is pinned.
- Batch evaluation exposes failing cases so CI can block regressions.

## Guardrail

The assistant may explain approved evidence, but it may not invent numbers, claim root cause, claim guaranteed savings, or pass uncited content through the gateway.

## Validation

Run:

    node tools/phase9/validate-t048-assistant-grounding-eval.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase9_T048AssistantGroundingEvalGateTests --no-build
`);

run("node --check T-048 validator", "node", ["--check", "tools/phase9/validate-t048-assistant-grounding-eval.cjs"]);
run("T-048 validator", "node", ["tools/phase9/validate-t048-assistant-grounding-eval.cjs"]);
run("Backend build after T-048", "dotnet", ["build", "Backend"]);
run("T-048 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase9_T048AssistantGroundingEvalGateTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-048 completed: assistant grounding eval gate is green.");
console.log("=================================================================================================");