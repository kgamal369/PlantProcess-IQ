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
  const target = p(".phase9_backup/t050_ai_regression_sweep_" + stamp + "/" + relativePath);
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

write("Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T050AssistantRegressionSweepTests.cs", `
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Assistant.ModelGateway;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP.
/// Final Phase 09 assistant regression sweep:
/// suggestions, grounding, private model gateway, citations, and refusal behavior.
/// </summary>
public sealed class Phase9_T050AssistantRegressionSweepTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly ProvenanceHandle EdgeFinding =
        ProvenanceHandle.Finding("finding-edge-caster-a", "approved edge-crack value suggestion");

    [Fact]
    public void T050_Demo_Assistant_Answers_Approved_Question_With_Citation()
    {
        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR for the edge-crack suggestion.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "What is the projected value range for the approved edge-crack suggestion?",
            modelOutput: "Approved projected value range is 28,000 to 56,000 EUR.",
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        Assert.False(result.IsRefusal);
        Assert.True(result.GroundingCertified);
        Assert.Contains("28,000", result.Text);
        Assert.Contains("56,000", result.Text);
        Assert.Contains(result.Citations, c => c.Id == EdgeFinding.Id);
        Assert.Empty(result.BlockedSentences);
    }

    [Fact]
    public void T050_Assistant_Blocks_Invented_Number_In_Demo_Response()
    {
        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR for the edge-crack suggestion.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "Give me the approved range and any extra estimate.",
            modelOutput: "Approved projected value range is 28,000 to 56,000 EUR. The assistant also estimates 99,999 EUR.",
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        Assert.False(result.IsRefusal);
        Assert.DoesNotContain("99,999", result.Text);
        Assert.Contains(result.BlockedSentences, s => s.Contains("99,999", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Citations, c => c.Id == EdgeFinding.Id);
    }

    [Fact]
    public async Task T050_SelfHosted_NoEgress_Gateway_Can_Feed_Grounded_Assistant_Demo()
    {
        var transport = new CapturingTransport();
        var gateway = new PrivateModelGatewayService(transport);

        var evidence = new[]
        {
            new ScopedEvidenceChunk(
                Handle: EdgeFinding.Token,
                Text: "Approved projected value range is 28,000 to 56,000 EUR.",
                SourceTable: "raw_hsm_rows",
                RawPlantRowJson: """{"secret":"must-not-egress"}""")
        };

        var gatewayResult = await gateway.AskAsync(
            new PrivateModelGatewayRequest(
                TenantId,
                "What is the approved value range?",
                evidence,
                new PrivateModelGatewayEndpoint(
                    EndpointCode: "self-hosted-no-egress",
                    ServingMode: PrivateModelServingMode.SelfHostedNoEgress,
                    ProviderType: "self-hosted-local",
                    ModelName: "ppiq-local-extractive",
                    ModelVersion: "phase09-t050",
                    EndpointUri: null,
                    ZeroDataRetentionConfirmed: true,
                    CustomerOwnedEndpoint: true,
                    NetworkBoundary: "local-process"),
                new PrivateModelGatewayTenantPolicy(
                    TenantId,
                    NoEgress: true,
                    AllowPrivateEndpoint: false,
                    AllowBringYourOwnModel: false)),
            CancellationToken.None);

        Assert.True(gatewayResult.Allowed);
        Assert.False(gatewayResult.OutboundCallAttempted);
        Assert.Equal(0, transport.CallCount);
        Assert.Contains("No outbound call was made", gatewayResult.Answer);

        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var grounded = GroundedAssistantGateway.Certify(
            prompt: "What is the approved value range?",
            modelOutput: gatewayResult.Answer,
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        Assert.False(grounded.IsRefusal);
        Assert.True(grounded.GroundingCertified);
        Assert.Contains(grounded.Citations, c => c.Id == EdgeFinding.Id);
        Assert.DoesNotContain("must-not-egress", grounded.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T050_Eval_Gate_Fails_If_Assistant_Tries_Causal_Or_Value_Overclaim()
    {
        var claim = new AssistantClaim(
            Text: "Approved projected value range is 28,000 to 56,000 EUR.",
            Handle: EdgeFinding,
            NumericTokens: new[] { "28000", "56000" });

        var result = GroundedAssistantGateway.Certify(
            prompt: "Explain why this happened.",
            modelOutput: "The root cause is caster-a and it will save 56,000 EUR.",
            retrievedClaims: new[] { claim },
            providerKey: AssistantGroundingEvalPromptSet.ProviderKey,
            modelKey: AssistantGroundingEvalPromptSet.ModelKey,
            modelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        var evalCase = new AssistantGroundingEvalCase(
            CaseKey: "t050_no_causal_value_overclaim",
            Prompt: "Explain why this happened.",
            ExpectedAnswerable: true,
            RequiredCitationTokens: new[] { EdgeFinding.Token },
            ForbiddenNumbers: Array.Empty<string>(),
            PinnedProviderKey: AssistantGroundingEvalPromptSet.ProviderKey,
            PinnedModelKey: AssistantGroundingEvalPromptSet.ModelKey,
            PinnedModelVersion: AssistantGroundingEvalPromptSet.ModelVersion);

        var evaluation = new AssistantGroundingEvalGate().Evaluate(evalCase, result);

        Assert.False(evaluation.Passed);
        Assert.DoesNotContain("root cause", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.BlockedSentences, s => s.Contains("root cause", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingTransport : IPrivateModelGatewayTransport
    {
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(
            Uri endpointUri,
            PrivateModelGatewayPayload payload,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult("This should not be called in no-egress mode.");
        }
    }
}
`);

write("tools/phase9/validate-t050-ai-regression-sweep.cjs", `
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

const requiredFiles = [
  "tools/phase9/validate-t047-deterministic-suggestion-workflow.cjs",
  "tools/phase9/validate-t048-assistant-grounding-eval.cjs",
  "tools/phase9/validate-t049-model-gateway-serving-modes.cjs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Phase9_T047SuggestionWorkflowCertificationTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T048AssistantGroundingEvalGateTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T049ModelGatewayServingModesTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T050AssistantRegressionSweepTests.cs"
];

for (const item of requiredFiles) {
  if (!exists(item)) {
    failures.push({ file: item, reason: "missing required Phase 09 artifact" });
  }
}

const checks = [
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase9_T050AssistantRegressionSweepTests.cs",
    signals: [
      "PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP",
      "T050_Demo_Assistant_Answers_Approved_Question_With_Citation",
      "T050_Assistant_Blocks_Invented_Number_In_Demo_Response",
      "T050_SelfHosted_NoEgress_Gateway_Can_Feed_Grounded_Assistant_Demo",
      "T050_Eval_Gate_Fails_If_Assistant_Tries_Causal_Or_Value_Overclaim",
      "99,999",
      "root cause",
      "No outbound call was made",
      "GroundingCertified"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/AssistantGroundingEvalGate.cs",
    signals: [
      "PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE",
      "ForbiddenCausalOrValuePhrases",
      "Model version drift"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Assistant/ModelGateway/PrivateModelGatewayService.cs",
    signals: [
      "PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES",
      "SelfHostedNoEgress",
      "Tenant no-egress policy blocks"
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing marker file" });
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
  console.error("PPIQ-T050 failed: Phase 09 AI regression sweep evidence is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T050 passed: Phase 09 AI regression sweep artifacts, eval gate, gateway controls and demo assistant checks are present.");
`);

write("scripts/phase9/validate-phase9-rollup.ps1", `
$ErrorActionPreference = "Stop"

$Root = "C:\\Workspace\\PlantProcess-IQ"
$StartedAt = Get-Date

function Step($Name) {
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkCyan
    Write-Host $Name -ForegroundColor Cyan
    Write-Host "=================================================================================================" -ForegroundColor DarkCyan
}

function Run-Cmd($Name, $WorkingDirectory, $FilePath, $Arguments) {
    Step $Name
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Name failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

Step "PPIQ Phase 09 Rollup - preflight"

Write-Host "Root  : $Root"
Write-Host "Scope : Phase 09 Suggestions + Assistant Grounding Certification"

Run-Cmd "T-047 validator" $Root "node" @(".\\tools\\phase9\\validate-t047-deterministic-suggestion-workflow.cjs")
Run-Cmd "T-048 validator" $Root "node" @(".\\tools\\phase9\\validate-t048-assistant-grounding-eval.cjs")
Run-Cmd "T-049 validator" $Root "node" @(".\\tools\\phase9\\validate-t049-model-gateway-serving-modes.cjs")
Run-Cmd "T-050 validator" $Root "node" @(".\\tools\\phase9\\validate-t050-ai-regression-sweep.cjs")

Run-Cmd "Backend build" $Root "dotnet" @("build", ".\\Backend")

Run-Cmd "T-047 suggestion workflow tests" $Root "dotnet" @(
    "test",
    ".\\Backend\\tests\\PlantProcess.Application.UnitTests\\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T047SuggestionWorkflowCertificationTests",
    "--no-build"
)

Run-Cmd "T-048 assistant grounding eval tests" $Root "dotnet" @(
    "test",
    ".\\Backend\\tests\\PlantProcess.Application.UnitTests\\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T048AssistantGroundingEvalGateTests",
    "--no-build"
)

Run-Cmd "T-049 model gateway serving mode tests" $Root "dotnet" @(
    "test",
    ".\\Backend\\tests\\PlantProcess.Application.UnitTests\\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T049ModelGatewayServingModesTests",
    "--no-build"
)

Run-Cmd "T-050 assistant regression sweep tests" $Root "dotnet" @(
    "test",
    ".\\Backend\\tests\\PlantProcess.Application.UnitTests\\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T050AssistantRegressionSweepTests",
    "--no-build"
)

$FinishedAt = Get-Date
$Duration = New-TimeSpan -Start $StartedAt -End $FinishedAt

$ReportDir = Join-Path $Root "docs\\phase9"
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null
$ReportPath = Join-Path $ReportDir "T050_PHASE9_AI_REGRESSION_SWEEP_GREEN.md"

$Report = @"
# T-050 Phase 09 AI Regression Sweep Green

Marker: PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP

## Status

GREEN

## Completed Phase 09 tasks

- T-047 deterministic suggestion workflow
- T-048 assistant grounding eval gate
- T-049 model gateway serving modes and no-egress toggle
- T-050 AI regression sweep and deploy certification

## Certification proof

- T-047 validator passed
- T-048 validator passed
- T-049 validator passed
- T-050 validator passed
- Backend build passed
- T-047 suggestion workflow tests passed
- T-048 assistant grounding eval tests passed
- T-049 model gateway serving-mode tests passed
- T-050 assistant regression sweep tests passed

## Guardrails certified

- Suggestions are deterministic and evidence-backed.
- Assistant blocks invented numbers.
- Assistant blocks unsupported causal/value overclaims.
- Assistant answers demo question with citation.
- Self-hosted model mode makes zero outbound calls.
- Private/BYO modes send only scoped evidence.
- Tenant no-egress blocks external model calls.

Generated at: $($FinishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Duration: $([math]::Round($Duration.TotalSeconds, 1)) seconds
"@

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReportPath, $Report, $utf8)

Step "PPIQ Phase 09 Rollup Result"

Write-Host "PPIQ-T050 passed: Phase 09 AI regression sweep is green." -ForegroundColor Green
Write-Host "Report: $ReportPath" -ForegroundColor Green
`);

write("docs/phase9/T050_AI_REGRESSION_SWEEP.md", `
# T-050 AI Regression Sweep

Marker: PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP

## Purpose

Close Phase 09 by proving that suggestions, assistant grounding, private model gateway controls, and assistant demo behavior remain green together.

## Acceptance

- T-047 suggestion workflow green.
- T-048 assistant grounding eval gate green.
- T-049 model gateway serving modes green.
- Assistant answers approved demo question with citation.
- Assistant blocks invented number.
- Assistant blocks unsupported causal/value overclaim.
- Self-hosted no-egress model mode can feed grounded assistant answer.
- Phase 09 rollup report generated.

## Validation

Run:

    node tools/phase9/validate-t050-ai-regression-sweep.cjs
    scripts/phase9/validate-phase9-rollup.ps1
`);

run("node --check T-050 validator", "node", ["--check", "tools/phase9/validate-t050-ai-regression-sweep.cjs"]);
run("T-050 validator", "node", ["tools/phase9/validate-t050-ai-regression-sweep.cjs"]);
run("Backend build after T-050", "dotnet", ["build", "Backend"]);
run("T-050 unit tests", "dotnet", [
  "test",
  "Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj",
  "--filter",
  "FullyQualifiedName~Phase9_T050AssistantRegressionSweepTests",
  "--no-build"
]);

console.log("");
console.log("=================================================================================================");
console.log("T-050 implementation completed. Now run scripts/phase9/validate-phase9-rollup.ps1 for final Phase 09 closure.");
console.log("=================================================================================================");