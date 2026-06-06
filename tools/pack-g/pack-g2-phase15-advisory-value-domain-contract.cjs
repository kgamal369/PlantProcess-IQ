const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const docsDir = path.join(root, "docs", "pack-g");
const developerDocsDir = path.join(root, "docs", "developer");
const toolsDir = path.join(root, "tools", "pack-g");

const contractDir = path.join(root, "Backend", "PlantProcess.Application", "Advisory");
const contractPath = path.join(contractDir, "P15AdvisoryValueContracts.cs");
const policyPath = path.join(contractDir, "P15AdvisoryHonestyPolicy.cs");

const contractDocPath = path.join(developerDocsDir, "PHASE15_ADVISORY_VALUE_CONTRACT.md");
const runbookPath = path.join(developerDocsDir, "PHASE15_ADVISORY_VALUE_RUNBOOK.md");

const validatorPath = path.join(toolsDir, "validate-pack-g2-phase15-contract.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-PackG-Phase15Regression.ps1");

const reportJsonPath = path.join(docsDir, "PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.md");
const evidencePath = path.join(docsDir, "PACK_G_IMPLEMENTATION_EVIDENCE.md");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });

    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function writeContracts() {
  const content = [
    "namespace PlantProcess.Application.Advisory;",
    "",
    "/// <summary>",
    "/// PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT",
    "/// Shared Phase 15 contract spine for advisory, what-if, recommendation, value-realization, ROI and benchmarking features.",
    "///",
    "/// Scope guard:",
    "/// - projection-only simulation",
    "/// - no causal claim unless proven by a future certified causal engine",
    "/// - no automatic write-back",
    "/// - confidence/evidence/provenance required",
    "/// - explicit human approval required before downstream action",
    "/// </summary>",
    "public static class P15AdvisoryValueContract",
    "{",
    "    public const string Marker = \"PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT\";",
    "    public const string Phase = \"P15\";",
    "    public const string Mode = \"Prescriptive-Advisory-And-Value-Realization\";",
    "    public const string ProjectionOnlyStatement = \"Projection only. Not a guaranteed saving and not an automatic process action.\";",
    "    public const string AttributionCaveat = \"Correlation is not causation. Realized value may be influenced by confounders and operational context.\";",
    "    public const int DefaultMinimumBenchmarkCohortSize = 5;",
    "    public const int DefaultScenarioSeed = 15096;",
    "}",
    "",
    "public enum P15SupportStatus",
    "{",
    "    Supported = 1,",
    "    InsufficientSupport = 2,",
    "    OutOfEnvelope = 3,",
    "    BlockedByHonestyGuard = 4",
    "}",
    "",
    "public enum P15EvidenceStrength",
    "{",
    "    None = 0,",
    "    Weak = 1,",
    "    Moderate = 2,",
    "    Strong = 3",
    "}",
    "",
    "public enum P15RecommendationStatus",
    "{",
    "    Draft = 1,",
    "    ApprovalRequired = 2,",
    "    Approved = 3,",
    "    Dismissed = 4,",
    "    Blocked = 5",
    "}",
    "",
    "public enum P15ApprovalDecision",
    "{",
    "    None = 0,",
    "    Approve = 1,",
    "    Dismiss = 2",
    "}",
    "",
    "public enum P15ValueKind",
    "{",
    "    Potential = 1,",
    "    Realized = 2",
    "}",
    "",
    "public enum P15BenchmarkVisibility",
    "{",
    "    Visible = 1,",
    "    SuppressedMinimumCohort = 2,",
    "    SuppressedTenantIsolation = 3",
    "}",
    "",
    "public sealed record P15EvidenceReference",
    "{",
    "    public required string EvidenceId { get; init; }",
    "    public required string EvidenceType { get; init; }",
    "    public required string SourceSystem { get; init; }",
    "    public required string Description { get; init; }",
    "    public decimal Confidence { get; init; }",
    "    public P15EvidenceStrength Strength { get; init; }",
    "    public string[] Provenance { get; init; } = Array.Empty<string>();",
    "}",
    "",
    "public sealed record P15MoneyRange",
    "{",
    "    public required string CurrencyCode { get; init; }",
    "    public decimal MinValue { get; init; }",
    "    public decimal ExpectedValue { get; init; }",
    "    public decimal MaxValue { get; init; }",
    "",
    "    public bool IsValid =>",
    "        !string.IsNullOrWhiteSpace(CurrencyCode) &&",
    "        MinValue <= ExpectedValue &&",
    "        ExpectedValue <= MaxValue;",
    "}",
    "",
    "public sealed record P15ParameterAdjustment",
    "{",
    "    public required string ParameterCode { get; init; }",
    "    public required string DisplayName { get; init; }",
    "    public decimal CurrentValue { get; init; }",
    "    public decimal ProposedValue { get; init; }",
    "    public decimal MinimumObservedValue { get; init; }",
    "    public decimal MaximumObservedValue { get; init; }",
    "    public string? Unit { get; init; }",
    "",
    "    public bool IsInsideObservedEnvelope =>",
    "        ProposedValue >= MinimumObservedValue && ProposedValue <= MaximumObservedValue;",
    "}",
    "",
    "public sealed record P15ScenarioRequest",
    "{",
    "    public required string TenantId { get; init; }",
    "    public required string PlantId { get; init; }",
    "    public required string FindingId { get; init; }",
    "    public required string ScenarioName { get; init; }",
    "    public int Seed { get; init; } = P15AdvisoryValueContract.DefaultScenarioSeed;",
    "    public P15ParameterAdjustment[] Adjustments { get; init; } = Array.Empty<P15ParameterAdjustment>();",
    "    public P15EvidenceReference[] Evidence { get; init; } = Array.Empty<P15EvidenceReference>();",
    "}",
    "",
    "public sealed record P15ScenarioProjectionPoint",
    "{",
    "    public required string MetricCode { get; init; }",
    "    public required string Label { get; init; }",
    "    public decimal BaselineValue { get; init; }",
    "    public decimal ProjectedValue { get; init; }",
    "    public decimal Delta { get; init; }",
    "    public string? Unit { get; init; }",
    "}",
    "",
    "public sealed record P15ScenarioResponse",
    "{",
    "    public required string ScenarioId { get; init; }",
    "    public required string FindingId { get; init; }",
    "    public P15SupportStatus SupportStatus { get; init; }",
    "    public required string SupportMessage { get; init; }",
    "    public required string ProjectionOnlyStatement { get; init; }",
    "    public int Seed { get; init; }",
    "    public P15MoneyRange? ProjectedValueImpact { get; init; }",
    "    public P15ScenarioProjectionPoint[] ProjectionPoints { get; init; } = Array.Empty<P15ScenarioProjectionPoint>();",
    "    public P15EvidenceReference[] Evidence { get; init; } = Array.Empty<P15EvidenceReference>();",
    "",
    "    public bool IsActionableProjection => SupportStatus == P15SupportStatus.Supported && ProjectedValueImpact?.IsValid == true;",
    "}",
    "",
    "public sealed record P15RecommendationParameterWindow",
    "{",
    "    public required string ParameterCode { get; init; }",
    "    public required string DisplayName { get; init; }",
    "    public decimal RecommendedMinimum { get; init; }",
    "    public decimal RecommendedMaximum { get; init; }",
    "    public string? Unit { get; init; }",
    "    public required string Basis { get; init; }",
    "}",
    "",
    "public sealed record P15RecommendationCandidate",
    "{",
    "    public required string RecommendationId { get; init; }",
    "    public required string FindingId { get; init; }",
    "    public required string Title { get; init; }",
    "    public required string AdvisoryText { get; init; }",
    "    public P15RecommendationStatus Status { get; init; } = P15RecommendationStatus.ApprovalRequired;",
    "    public P15EvidenceStrength EvidenceStrength { get; init; }",
    "    public decimal Confidence { get; init; }",
    "    public P15MoneyRange? ExpectedImpact { get; init; }",
    "    public P15RecommendationParameterWindow[] ParameterWindows { get; init; } = Array.Empty<P15RecommendationParameterWindow>();",
    "    public P15EvidenceReference[] Evidence { get; init; } = Array.Empty<P15EvidenceReference>();",
    "    public string[] Provenance { get; init; } = Array.Empty<string>();",
    "    public required string HonestyCaveat { get; init; }",
    "    public bool RequiresHumanApproval { get; init; } = true;",
    "    public bool HasWriteBackPath { get; init; } = false;",
    "}",
    "",
    "public sealed record P15ApprovalCommand",
    "{",
    "    public required string RecommendationId { get; init; }",
    "    public required string ApproverUserId { get; init; }",
    "    public P15ApprovalDecision Decision { get; init; }",
    "    public required string Comment { get; init; }",
    "    public DateTimeOffset DecidedAtUtc { get; init; } = DateTimeOffset.UtcNow;",
    "}",
    "",
    "public sealed record P15ApprovalResult",
    "{",
    "    public required string RecommendationId { get; init; }",
    "    public P15RecommendationStatus Status { get; init; }",
    "    public required string Message { get; init; }",
    "    public required string ApprovalRecordId { get; init; }",
    "    public DateTimeOffset DecidedAtUtc { get; init; }",
    "}",
    "",
    "public sealed record P15ValueWindow",
    "{",
    "    public required string WindowId { get; init; }",
    "    public required string MetricCode { get; init; }",
    "    public required string Label { get; init; }",
    "    public DateTimeOffset FromUtc { get; init; }",
    "    public DateTimeOffset ToUtc { get; init; }",
    "    public decimal Value { get; init; }",
    "    public string? Unit { get; init; }",
    "}",
    "",
    "public sealed record P15ValueRealizationLedgerEntry",
    "{",
    "    public required string LedgerEntryId { get; init; }",
    "    public required string TenantId { get; init; }",
    "    public required string PlantId { get; init; }",
    "    public required string RecommendationId { get; init; }",
    "    public required string FindingId { get; init; }",
    "    public P15ValueWindow BaselineWindow { get; init; } = default!;",
    "    public P15ValueWindow ActualWindow { get; init; } = default!;",
    "    public P15MoneyRange RealizedValue { get; init; } = default!;",
    "    public required string AttributionCaveat { get; init; }",
    "    public string[] Provenance { get; init; } = Array.Empty<string>();",
    "    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;",
    "}",
    "",
    "public sealed record P15RoiSummary",
    "{",
    "    public required string TenantId { get; init; }",
    "    public required string PlantId { get; init; }",
    "    public P15MoneyRange PotentialValue { get; init; } = default!;",
    "    public P15MoneyRange RealizedValue { get; init; } = default!;",
    "    public decimal PaybackPeriodMonths { get; init; }",
    "    public int RecommendationCount { get; init; }",
    "    public int ApprovedRecommendationCount { get; init; }",
    "    public int RealizedLedgerEntryCount { get; init; }",
    "    public required string EvidencePackReference { get; init; }",
    "}",
    "",
    "public sealed record P15BenchmarkRequest",
    "{",
    "    public required string TenantId { get; init; }",
    "    public required string PlantId { get; init; }",
    "    public required string MetricCode { get; init; }",
    "    public required string IndustryCode { get; init; }",
    "    public int MinimumCohortSize { get; init; } = P15AdvisoryValueContract.DefaultMinimumBenchmarkCohortSize;",
    "}",
    "",
    "public sealed record P15BenchmarkBand",
    "{",
    "    public required string BandCode { get; init; }",
    "    public decimal P10 { get; init; }",
    "    public decimal P25 { get; init; }",
    "    public decimal P50 { get; init; }",
    "    public decimal P75 { get; init; }",
    "    public decimal P90 { get; init; }",
    "    public int CohortSize { get; init; }",
    "    public P15BenchmarkVisibility Visibility { get; init; }",
    "}",
    "",
    "public sealed record P15BenchmarkResponse",
    "{",
    "    public required string MetricCode { get; init; }",
    "    public required string IndustryCode { get; init; }",
    "    public P15BenchmarkVisibility Visibility { get; init; }",
    "    public required string Message { get; init; }",
    "    public P15BenchmarkBand? Band { get; init; }",
    "    public string[] PrivacyGuards { get; init; } = Array.Empty<string>();",
    "}",
    ""
  ].join("\n");

  write(contractPath, content);

  return { path: rel(contractPath), status: "WRITTEN" };
}

function writePolicy() {
  const content = [
    "namespace PlantProcess.Application.Advisory;",
    "",
    "/// <summary>",
    "/// PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT",
    "/// Central honesty policy for Phase 15 advisory/value features.",
    "///",
    "/// This is deliberately conservative. It blocks causal wording, write-back bypasses,",
    "/// weak-evidence recommendations, and out-of-envelope scenario requests.",
    "/// </summary>",
    "public static class P15AdvisoryHonestyPolicy",
    "{",
    "    private static readonly string[] CausalClaimPhrases =",
    "    {",
    "        \"will cause\",",
    "        \"causes\",",
    "        \"guarantees\",",
    "        \"guaranteed\",",
    "        \"proves causation\",",
    "        \"root cause is\",",
    "        \"definitely saves\",",
    "        \"automatic saving\",",
    "        \"must change the process\"",
    "    };",
    "",
    "    public static P15PolicyDecision ValidateScenarioRequest(P15ScenarioRequest request)",
    "    {",
    "        var violations = new List<string>();",
    "",
    "        if (string.IsNullOrWhiteSpace(request.TenantId))",
    "            violations.Add(\"TenantId is required.\");",
    "",
    "        if (string.IsNullOrWhiteSpace(request.PlantId))",
    "            violations.Add(\"PlantId is required.\");",
    "",
    "        if (string.IsNullOrWhiteSpace(request.FindingId))",
    "            violations.Add(\"FindingId is required.\");",
    "",
    "        if (request.Adjustments.Length == 0)",
    "            violations.Add(\"At least one parameter adjustment is required.\");",
    "",
    "        var outOfEnvelope = request.Adjustments",
    "            .Where(adjustment => !adjustment.IsInsideObservedEnvelope)",
    "            .Select(adjustment => adjustment.ParameterCode)",
    "            .Distinct(StringComparer.OrdinalIgnoreCase)",
    "            .ToArray();",
    "",
    "        if (outOfEnvelope.Length > 0)",
    "            violations.Add(\"Out-of-envelope projection must abstain for parameters: \" + string.Join(\", \", outOfEnvelope));",
    "",
    "        return violations.Count == 0",
    "            ? P15PolicyDecision.Allow(\"Scenario request is inside the observed data envelope.\")",
    "            : P15PolicyDecision.Block(\"Scenario request is not safe for supported projection.\", violations);",
    "    }",
    "",
    "    public static P15PolicyDecision ValidateRecommendation(P15RecommendationCandidate recommendation)",
    "    {",
    "        var violations = new List<string>();",
    "",
    "        if (string.IsNullOrWhiteSpace(recommendation.RecommendationId))",
    "            violations.Add(\"RecommendationId is required.\");",
    "",
    "        if (recommendation.Confidence < 0m || recommendation.Confidence > 1m)",
    "            violations.Add(\"Confidence must be between 0 and 1.\");",
    "",
    "        if (recommendation.EvidenceStrength is P15EvidenceStrength.None or P15EvidenceStrength.Weak)",
    "            violations.Add(\"Weak or missing evidence must block recommendation.\");",
    "",
    "        if (recommendation.Evidence.Length == 0)",
    "            violations.Add(\"Recommendation must include evidence references.\");",
    "",
    "        if (recommendation.ExpectedImpact is null || !recommendation.ExpectedImpact.IsValid)",
    "            violations.Add(\"Recommendation must include a valid expected impact range.\");",
    "",
    "        if (!recommendation.RequiresHumanApproval)",
    "            violations.Add(\"Recommendation must require explicit human approval.\");",
    "",
    "        if (recommendation.HasWriteBackPath)",
    "            violations.Add(\"Recommendation contract must not expose an automatic write-back path.\");",
    "",
    "        violations.AddRange(FindCausalLanguageViolations(recommendation.Title));",
    "        violations.AddRange(FindCausalLanguageViolations(recommendation.AdvisoryText));",
    "        violations.AddRange(FindCausalLanguageViolations(recommendation.HonestyCaveat));",
    "",
    "        return violations.Count == 0",
    "            ? P15PolicyDecision.Allow(\"Recommendation satisfies honesty and approval guardrails.\")",
    "            : P15PolicyDecision.Block(\"Recommendation failed honesty guardrails.\", violations);",
    "    }",
    "",
    "    public static P15PolicyDecision ValidateApprovalCommand(P15ApprovalCommand command)",
    "    {",
    "        var violations = new List<string>();",
    "",
    "        if (string.IsNullOrWhiteSpace(command.RecommendationId))",
    "            violations.Add(\"RecommendationId is required.\");",
    "",
    "        if (string.IsNullOrWhiteSpace(command.ApproverUserId))",
    "            violations.Add(\"ApproverUserId is required.\");",
    "",
    "        if (command.Decision == P15ApprovalDecision.None)",
    "            violations.Add(\"Decision must be approve or dismiss.\");",
    "",
    "        if (string.IsNullOrWhiteSpace(command.Comment))",
    "            violations.Add(\"Approval/dismissal comment is required.\");",
    "",
    "        return violations.Count == 0",
    "            ? P15PolicyDecision.Allow(\"Approval command is valid.\")",
    "            : P15PolicyDecision.Block(\"Approval command is incomplete.\", violations);",
    "    }",
    "",
    "    public static P15PolicyDecision ValidateBenchmarkVisibility(P15BenchmarkRequest request, int cohortSize)",
    "    {",
    "        if (cohortSize < request.MinimumCohortSize)",
    "        {",
    "            return P15PolicyDecision.Block(",
    "                \"Benchmark suppressed because cohort is below minimum privacy threshold.\",",
    "                new[] { $\"CohortSize={cohortSize}; MinimumCohortSize={request.MinimumCohortSize}\" });",
    "        }",
    "",
    "        return P15PolicyDecision.Allow(\"Benchmark can be shown as anonymized aggregate.\");",
    "    }",
    "",
    "    public static int BuildStableScenarioSeed(P15ScenarioRequest request)",
    "    {",
    "        unchecked",
    "        {",
    "            var hash = 17;",
    "            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.TenantId ?? string.Empty);",
    "            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.PlantId ?? string.Empty);",
    "            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.FindingId ?? string.Empty);",
    "            hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(request.ScenarioName ?? string.Empty);",
    "            hash = (hash * 31) + request.Seed;",
    "",
    "            foreach (var adjustment in request.Adjustments.OrderBy(item => item.ParameterCode, StringComparer.OrdinalIgnoreCase))",
    "            {",
    "                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(adjustment.ParameterCode ?? string.Empty);",
    "                hash = (hash * 31) + adjustment.ProposedValue.GetHashCode();",
    "            }",
    "",
    "            return hash == int.MinValue ? P15AdvisoryValueContract.DefaultScenarioSeed : Math.Abs(hash);",
    "        }",
    "    }",
    "",
    "    private static IEnumerable<string> FindCausalLanguageViolations(string? text)",
    "    {",
    "        if (string.IsNullOrWhiteSpace(text))",
    "            yield break;",
    "",
    "        foreach (var phrase in CausalClaimPhrases)",
    "        {",
    "            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))",
    "                yield return $\"Causal/overclaim phrase is not allowed: '{phrase}'.\";",
    "        }",
    "    }",
    "}",
    "",
    "public sealed record P15PolicyDecision",
    "{",
    "    public bool IsAllowed { get; init; }",
    "    public required string Message { get; init; }",
    "    public string[] Violations { get; init; } = Array.Empty<string>();",
    "",
    "    public static P15PolicyDecision Allow(string message) =>",
    "        new()",
    "        {",
    "            IsAllowed = true,",
    "            Message = message,",
    "            Violations = Array.Empty<string>()",
    "        };",
    "",
    "    public static P15PolicyDecision Block(string message, IEnumerable<string> violations) =>",
    "        new()",
    "        {",
    "            IsAllowed = false,",
    "            Message = message,",
    "            Violations = violations.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()",
    "        };",
    "}",
    ""
  ].join("\n");

  write(policyPath, content);

  return { path: rel(policyPath), status: "WRITTEN" };
}

function writeDocs() {
  const contract = [];

  contract.push("# Phase 15 Advisory/Value Contract");
  contract.push("");
  contract.push("Marker: PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT");
  contract.push("");
  contract.push("## Purpose");
  contract.push("");
  contract.push("This contract is the shared spine for Phase 15: what-if simulation, recommendation generation, value-realization ledger, ROI/CFO dashboard, benchmarking and recommendation honesty certification.");
  contract.push("");
  contract.push("## Non-negotiable rules");
  contract.push("");
  contract.push("- Projection only. Not a guaranteed saving.");
  contract.push("- No causal language unless a future certified causal engine explicitly proves causality.");
  contract.push("- No automatic process write-back.");
  contract.push("- Every recommendation must include confidence, evidence and provenance.");
  contract.push("- Human approval is required before any downstream operational action.");
  contract.push("- Weak evidence must block the recommendation.");
  contract.push("- Out-of-envelope scenario requests must abstain.");
  contract.push("- Benchmarking must be privacy-preserving and cohort-size protected.");
  contract.push("");
  contract.push("## Backend contract files");
  contract.push("");
  contract.push("- `Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs`");
  contract.push("- `Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs`");
  contract.push("");
  contract.push("## Contract families");
  contract.push("");
  contract.push("- Scenario request/response contracts.");
  contract.push("- Recommendation and approval contracts.");
  contract.push("- Value-realization ledger contracts.");
  contract.push("- ROI/CFO summary contracts.");
  contract.push("- Benchmark request/response contracts.");
  contract.push("- Honesty policy decision contracts.");
  contract.push("");

  write(contractDocPath, contract.join("\n"));

  const runbook = [];

  runbook.push("# Phase 15 Advisory/Value Runbook");
  runbook.push("");
  runbook.push("Marker: PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT");
  runbook.push("");
  runbook.push("## Implementation order");
  runbook.push("");
  runbook.push("1. Use `P15ScenarioRequest` and `P15ScenarioResponse` in Pack G-3.");
  runbook.push("2. Use `P15RecommendationCandidate` and approval contracts in Pack G-4.");
  runbook.push("3. Use `P15ValueRealizationLedgerEntry` in Pack G-5.");
  runbook.push("4. Use `P15RoiSummary` in Pack G-6.");
  runbook.push("5. Use `P15BenchmarkRequest` and `P15BenchmarkResponse` in Pack G-7.");
  runbook.push("6. Use `P15AdvisoryHonestyPolicy` for adversarial certification in Pack G-8.");
  runbook.push("");
  runbook.push("## Safety checks");
  runbook.push("");
  runbook.push("- Call `ValidateScenarioRequest` before scenario projection.");
  runbook.push("- Call `ValidateRecommendation` before exposing a recommendation.");
  runbook.push("- Call `ValidateApprovalCommand` before approving or dismissing.");
  runbook.push("- Call `ValidateBenchmarkVisibility` before cross-plant/industry benchmark exposure.");
  runbook.push("- Use `BuildStableScenarioSeed` for deterministic what-if behavior.");
  runbook.push("");

  write(runbookPath, runbook.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs",');
  lines.push('  "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs",');
  lines.push('  "docs/developer/PHASE15_ADVISORY_VALUE_CONTRACT.md",');
  lines.push('  "docs/developer/PHASE15_ADVISORY_VALUE_RUNBOOK.md",');
  lines.push('  "docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.json",');
  lines.push('  "docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.md",');
  lines.push('  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('const requiredSignals = [');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ScenarioRequest" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ScenarioResponse" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15RecommendationCandidate" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ApprovalCommand" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ValueRealizationLedgerEntry" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15RoiSummary" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15BenchmarkResponse" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "P15AdvisoryHonestyPolicy" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateScenarioRequest" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateRecommendation" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateApprovalCommand" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateBenchmarkVisibility" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "BuildStableScenarioSeed" },');
  lines.push('  { file: "docs/developer/PHASE15_ADVISORY_VALUE_CONTRACT.md", signal: "No automatic process write-back" },');
  lines.push('  { file: "docs/developer/PHASE15_ADVISORY_VALUE_RUNBOOK.md", signal: "BuildStableScenarioSeed" },');
  lines.push('  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT" }');
  lines.push('];');
  lines.push('');
  lines.push('function isFile(relativePath) {');
  lines.push('  const absolute = path.join(root, relativePath);');
  lines.push('  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();');
  lines.push('}');
  lines.push('');
  lines.push('function read(relativePath) {');
  lines.push('  return fs.readFileSync(path.join(root, relativePath), "utf8");');
  lines.push('}');
  lines.push('');
  lines.push('const failures = [];');
  lines.push('');
  lines.push('for (const file of requiredFiles) {');
  lines.push('  if (!isFile(file)) failures.push({ file, reason: "missing" });');
  lines.push('}');
  lines.push('');
  lines.push('for (const item of requiredSignals) {');
  lines.push('  if (!isFile(item.file)) { failures.push({ file: item.file, signal: item.signal, reason: "missing-file" }); continue; }');
  lines.push('  if (!read(item.file).includes(item.signal)) failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });');
  lines.push('}');
  lines.push('');
  lines.push('if (failures.length) {');
  lines.push('  console.error("Pack G-2 Phase 15 advisory/value contract validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack G-2 Phase 15 advisory/value contract validation passed.");');

  write(validatorPath, lines.join("\n") + "\n");

  return { path: rel(validatorPath), status: "WRITTEN" };
}

function writeWrapper() {
  const content = [
    "[CmdletBinding()]",
    "param(",
    "    [string]$ProjectRoot = (Resolve-Path \".\").Path,",
    "    [switch]$RunBuilds",
    ")",
    "",
    "$ErrorActionPreference = \"Stop\"",
    "",
    "function Run-Step([string]$Name, [scriptblock]$Block) {",
    "    Write-Host \"\"",
    "    Write-Host \"---- $Name\" -ForegroundColor Cyan",
    "    & $Block",
    "    if ($LASTEXITCODE -ne 0) { throw \"$Name failed with exit code $LASTEXITCODE\" }",
    "}",
    "",
    "Push-Location $ProjectRoot",
    "try {",
    "    Run-Step \"Pack G-1 Phase 15 closure-map validation\" {",
    "        node \".\\tools\\pack-g\\validate-pack-g-phase15-closure-map.cjs\"",
    "    }",
    "",
    "    Run-Step \"Pack G-2 Phase 15 advisory/value contract validation\" {",
    "        node \".\\tools\\pack-g\\validate-pack-g2-phase15-contract.cjs\"",
    "    }",
    "",
    "    if ($RunBuilds) {",
    "        Run-Step \"Backend build\" { dotnet build \".\\Backend\" }",
    "    }",
    "",
    "    Write-Host \"\"",
    "    Write-Host \"Pack G Phase 15 regression wrapper completed.\" -ForegroundColor Green",
    "}",
    "finally { Pop-Location }",
    ""
  ].join("\n");

  write(wrapperPath, content);

  return { path: rel(wrapperPath), status: "WRITTEN" };
}

function writeReports(results) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT",
    phase: "P15",
    pack: "G",
    step: "G-2",
    purpose: "Create shared Phase 15 advisory/value contract spine before scenario, recommendation, value-realization, dashboard, benchmarking and honesty certification implementation.",
    results
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];

  md.push("# Pack G-2 Phase 15 Advisory/Value Contract Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT");
  md.push("");
  md.push("## Scope");
  md.push("");
  md.push("Pack G-2 adds the shared contract layer for Phase 15. This prevents later packs from creating disconnected DTOs and keeps advisory, simulation, recommendation, value-realization, benchmarking and governance aligned.");
  md.push("");
  md.push("## Changed files");
  md.push("");
  md.push("| File | Status |");
  md.push("|---|---|");

  for (const item of results) {
    md.push("| `" + item.path + "` | " + item.status + " |");
  }

  md.push("");

  write(reportMdPath, md.join("\n"));
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack G Evidence\n";

  if (!evidence.includes("PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT")) {
    evidence += [
      "",
      "## Pack G-2 Phase 15 advisory/value domain contract",
      "",
      "- Marker: PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT.",
      "- Added shared Phase 15 contract spine under PlantProcess.Application.Advisory.",
      "- Added scenario request/response contracts for T-096.",
      "- Added recommendation and approval contracts for T-097 and T-101.",
      "- Added value-realization ledger and ROI summary contracts for T-098 and T-099.",
      "- Added privacy-preserving benchmark contracts for T-100.",
      "- Added honesty policy for no causal language, no automatic write-back, approval requirement, out-of-envelope abstain and cohort privacy.",
      "- Updated Pack G regression wrapper.",
      "",
      "Generated artifacts:",
      "",
      "- Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs",
      "- Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs",
      "- docs/developer/PHASE15_ADVISORY_VALUE_CONTRACT.md",
      "- docs/developer/PHASE15_ADVISORY_VALUE_RUNBOOK.md",
      "- docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.md",
      "- docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.json",
      "- tools/pack-g/validate-pack-g2-phase15-contract.cjs",
      "- tools/pack-g/Invoke-PackG-Phase15Regression.ps1",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack G-2 — Phase 15 advisory/value domain contract");
console.log("=================================================================================================");

ensureDir(contractDir);
ensureDir(docsDir);
ensureDir(developerDocsDir);
ensureDir(toolsDir);

const results = [];
results.push(writeContracts());
results.push(writePolicy());
writeDocs();
results.push(writeValidator());
results.push(writeWrapper());
writeReports(results);
writeEvidence();

run("node --check Pack G-2 validator", ["node", "--check", "tools/pack-g/validate-pack-g2-phase15-contract.cjs"]);
run("Pack G-2 Phase 15 contract validator", ["node", "tools/pack-g/validate-pack-g2-phase15-contract.cjs"]);
run("Backend build", ["dotnet", "build", "Backend"]);

console.log("");
console.log("Pack G-2 result:");
console.log(" - Phase 15 advisory/value contracts written");
console.log(" - Phase 15 honesty policy written");
console.log(" - Phase 15 contract docs written");
console.log(" - Pack G regression wrapper updated");
console.log(" - Pack G-2 validator passed");
console.log(" - Backend build passed");

console.log("");
console.log("=================================================================================================");
console.log("Pack G-2 completed.");
console.log("Report: docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.md");
console.log("Next  : Pack G-3 / T-096 — deterministic what-if scenario simulation engine.");
console.log("=================================================================================================");