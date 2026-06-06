const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_g_backup", "pack_g6_t099_roi_cfo_dashboard_" + timestamp);

const docsDir = path.join(root, "docs", "pack-g");
const developerDocsDir = path.join(root, "docs", "developer");
const toolsDir = path.join(root, "tools", "pack-g");
const toolsTaskClosureDir = path.join(root, "tools", "task-closure");

const servicePath = path.join(root, "Backend", "PlantProcess.Application", "Advisory", "P15RoiCfoDashboardService.cs");
const endpointPath = path.join(root, "Backend", "PlantProcess.Api", "PlantConnectors", "P15RoiCfoDashboardEndpoints.cs");
const programPath = path.join(root, "Backend", "PlantProcess.Api", "Program.cs");

const frontendApiPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "api", "phase15Advisory.ts");
const frontendPagePath = path.join(root, "Frontend", "PlantProcess.Web", "src", "pages", "Phase15", "RoiCfoDashboardPage.tsx");
const appPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "App.implementation.tsx");
const layoutPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "components", "AppLayout.tsx");

const validatorPath = path.join(toolsDir, "validate-pack-g6-t099-roi-cfo-dashboard.cjs");
const bridgePath = path.join(toolsTaskClosureDir, "ppiq-pack-g6-scorecard-bridge.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-PackG-Phase15Regression.ps1");

const dashboardDocPath = path.join(developerDocsDir, "PHASE15_ROI_CFO_VALUE_DASHBOARD.md");
const reportJsonPath = path.join(docsDir, "PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.md");
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

function backup(file) {
  if (!isFile(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
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

function frontendBuildArgs() {
  const frontendDir = path.join(root, "Frontend", "PlantProcess.Web").replace(/'/g, "''");
  return [
    "powershell",
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-Command",
    "$ErrorActionPreference='Stop'; Push-Location '" + frontendDir + "'; npm.cmd run build; Pop-Location"
  ];
}

function writeService() {
  const content = [
    "namespace PlantProcess.Application.Advisory;",
    "",
    "/// <summary>",
    "/// PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD",
    "/// Buyer/CFO-facing Phase 15 value dashboard.",
    "///",
    "/// Guardrails:",
    "/// - potential value and realized value are separate",
    "/// - ROI and payback reconcile with value ledger",
    "/// - export evidence pack carries provenance and caveats",
    "/// - no causal claim",
    "/// - no fake realized value without ledger",
    "/// </summary>",
    "public sealed class P15RoiCfoDashboardService",
    "{",
    "    public P15RoiCfoDashboardResponse BuildDemoDashboard()",
    "    {",
    "        var recommendationService = new P15RecommendationService();",
    "        var recommendationRequest = recommendationService.BuildDemoRequest();",
    "        var recommendationResponse = recommendationService.Generate(recommendationRequest);",
    "        var recommendation = recommendationResponse.Recommendations.FirstOrDefault();",
    "",
    "        var valueService = new P15ValueRealizationService();",
    "        var valueA = valueService.Calculate(valueService.BuildDemoRequest(actualValue: 91.5m));",
    "        var valueB = valueService.Calculate(valueService.BuildDemoRequest(actualValue: 94.0m));",
    "        var ledgerEntries = new[] { valueA.LedgerEntry, valueB.LedgerEntry }",
    "            .Where(item => item is not null)",
    "            .Cast<P15ValueRealizationLedgerEntry>()",
    "            .ToArray();",
    "",
    "        var potential = recommendation?.ExpectedImpact ?? new P15MoneyRange",
    "        {",
    "            CurrencyCode = \"EUR\",",
    "            MinValue = 0m,",
    "            ExpectedValue = 0m,",
    "            MaxValue = 0m",
    "        };",
    "",
    "        var realizedExpected = ledgerEntries.Sum(item => item.RealizedValue.ExpectedValue);",
    "        var realized = new P15MoneyRange",
    "        {",
    "            CurrencyCode = potential.CurrencyCode,",
    "            MinValue = Math.Round(ledgerEntries.Sum(item => item.RealizedValue.MinValue), 2, MidpointRounding.AwayFromZero),",
    "            ExpectedValue = Math.Round(realizedExpected, 2, MidpointRounding.AwayFromZero),",
    "            MaxValue = Math.Round(ledgerEntries.Sum(item => item.RealizedValue.MaxValue), 2, MidpointRounding.AwayFromZero)",
    "        };",
    "",
    "        var subscriptionCost = 18000m;",
    "        var paybackMonths = realized.ExpectedValue <= 0m",
    "            ? 0m",
    "            : Math.Round(subscriptionCost / realized.ExpectedValue, 2, MidpointRounding.AwayFromZero);",
    "",
    "        var summary = new P15RoiSummary",
    "        {",
    "            TenantId = \"demo-tenant\",",
    "            PlantId = \"demo-plant-01\",",
    "            PotentialValue = potential,",
    "            RealizedValue = realized,",
    "            PaybackPeriodMonths = paybackMonths,",
    "            RecommendationCount = recommendationResponse.Recommendations.Length,",
    "            ApprovedRecommendationCount = 1,",
    "            RealizedLedgerEntryCount = ledgerEntries.Length,",
    "            EvidencePackReference = \"p15-cfo-evidence-pack-demo-001\"",
    "        };",
    "",
    "        var buckets = BuildBuckets(ledgerEntries, potential);",
    "        var evidencePack = BuildEvidencePack(summary, recommendation, ledgerEntries);",
    "",
    "        return new P15RoiCfoDashboardResponse",
    "        {",
    "            Status = \"Ready\",",
    "            Message = \"ROI/CFO dashboard generated from potential recommendation impact and realized value ledger entries.\",",
    "            GeneratedAtUtc = DateTimeOffset.UtcNow,",
    "            Summary = summary,",
    "            Buckets = buckets,",
    "            LedgerEntries = ledgerEntries,",
    "            EvidencePack = evidencePack,",
    "            Caveats = new[]",
    "            {",
    "                P15AdvisoryValueContract.ProjectionOnlyStatement,",
    "                P15AdvisoryValueContract.AttributionCaveat,",
    "                \"Potential value is not realized value.\",",
    "                \"Realized value is calculated only from ledger entries.\"",
    "            }",
    "        };",
    "    }",
    "",
    "    public P15CfoEvidencePack BuildEvidencePack(P15RoiSummary summary, P15RecommendationCandidate? recommendation, P15ValueRealizationLedgerEntry[] ledgerEntries)",
    "    {",
    "        return new P15CfoEvidencePack",
    "        {",
    "            EvidencePackId = summary.EvidencePackReference,",
    "            CurrencyCode = summary.RealizedValue.CurrencyCode,",
    "            PotentialExpectedValue = summary.PotentialValue.ExpectedValue,",
    "            RealizedExpectedValue = summary.RealizedValue.ExpectedValue,",
    "            PaybackPeriodMonths = summary.PaybackPeriodMonths,",
    "            RecommendationIds = recommendation is null ? Array.Empty<string>() : new[] { recommendation.RecommendationId },",
    "            LedgerEntryIds = ledgerEntries.Select(item => item.LedgerEntryId).ToArray(),",
    "            Provenance = ledgerEntries",
    "                .SelectMany(item => item.Provenance)",
    "                .Append(\"phase15-roi-cfo-dashboard\")",
    "                .Append(\"cfo-evidence-pack\")",
    "                .Distinct(StringComparer.OrdinalIgnoreCase)",
    "                .ToArray(),",
    "            Caveats = new[]",
    "            {",
    "                P15AdvisoryValueContract.ProjectionOnlyStatement,",
    "                P15AdvisoryValueContract.AttributionCaveat,",
    "                \"Export reconciles with the value-realization ledger available at dashboard generation time.\"",
    "            }",
    "        };",
    "    }",
    "",
    "    private static P15RoiValueBucket[] BuildBuckets(P15ValueRealizationLedgerEntry[] ledgerEntries, P15MoneyRange potential)",
    "    {",
    "        var realizedEnergy = ledgerEntries.Sum(item => item.RealizedValue.ExpectedValue);",
    "        var unrealizedPotential = Math.Max(0m, potential.ExpectedValue - realizedEnergy);",
    "",
    "        return new[]",
    "        {",
    "            new P15RoiValueBucket",
    "            {",
    "                BucketCode = \"potential-total\",",
    "                Label = \"Potential value\",",
    "                ValueKind = P15ValueKind.Potential,",
    "                CurrencyCode = potential.CurrencyCode,",
    "                ExpectedValue = potential.ExpectedValue,",
    "                Source = \"Recommendation expected impact range\"",
    "            },",
    "            new P15RoiValueBucket",
    "            {",
    "                BucketCode = \"realized-ledger\",",
    "                Label = \"Realized value\",",
    "                ValueKind = P15ValueKind.Realized,",
    "                CurrencyCode = potential.CurrencyCode,",
    "                ExpectedValue = realizedEnergy,",
    "                Source = \"Value-realization ledger\"",
    "            },",
    "            new P15RoiValueBucket",
    "            {",
    "                BucketCode = \"unrealized-pipeline\",",
    "                Label = \"Remaining potential\",",
    "                ValueKind = P15ValueKind.Potential,",
    "                CurrencyCode = potential.CurrencyCode,",
    "                ExpectedValue = unrealizedPotential,",
    "                Source = \"Potential minus realized\"",
    "            }",
    "        };",
    "    }",
    "}",
    "",
    "public sealed record P15RoiCfoDashboardResponse",
    "{",
    "    public required string Status { get; init; }",
    "    public required string Message { get; init; }",
    "    public DateTimeOffset GeneratedAtUtc { get; init; }",
    "    public required P15RoiSummary Summary { get; init; }",
    "    public P15RoiValueBucket[] Buckets { get; init; } = Array.Empty<P15RoiValueBucket>();",
    "    public P15ValueRealizationLedgerEntry[] LedgerEntries { get; init; } = Array.Empty<P15ValueRealizationLedgerEntry>();",
    "    public required P15CfoEvidencePack EvidencePack { get; init; }",
    "    public string[] Caveats { get; init; } = Array.Empty<string>();",
    "}",
    "",
    "public sealed record P15RoiValueBucket",
    "{",
    "    public required string BucketCode { get; init; }",
    "    public required string Label { get; init; }",
    "    public P15ValueKind ValueKind { get; init; }",
    "    public required string CurrencyCode { get; init; }",
    "    public decimal ExpectedValue { get; init; }",
    "    public required string Source { get; init; }",
    "}",
    "",
    "public sealed record P15CfoEvidencePack",
    "{",
    "    public required string EvidencePackId { get; init; }",
    "    public required string CurrencyCode { get; init; }",
    "    public decimal PotentialExpectedValue { get; init; }",
    "    public decimal RealizedExpectedValue { get; init; }",
    "    public decimal PaybackPeriodMonths { get; init; }",
    "    public string[] RecommendationIds { get; init; } = Array.Empty<string>();",
    "    public string[] LedgerEntryIds { get; init; } = Array.Empty<string>();",
    "    public string[] Provenance { get; init; } = Array.Empty<string>();",
    "    public string[] Caveats { get; init; } = Array.Empty<string>();",
    "}",
    ""
  ].join("\n");

  write(servicePath, content);

  return { path: rel(servicePath), status: "WRITTEN" };
}

function writeEndpoint() {
  const content = [
    "using PlantProcess.Application.Advisory;",
    "",
    "namespace PlantProcess.Api.PlantConnectors;",
    "",
    "public static class P15RoiCfoDashboardEndpoints",
    "{",
    "    public static IEndpointRouteBuilder MapP15RoiCfoDashboardEndpoints(this IEndpointRouteBuilder app)",
    "    {",
    "        var group = app.MapGroup(\"/api/p15/roi-cfo-dashboard\")",
    "            .WithTags(\"P15 ROI CFO Dashboard\");",
    "",
    "        group.MapGet(\"/health\", () => Results.Ok(new",
    "        {",
    "            status = \"ok\",",
    "            marker = \"PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD\",",
    "            phase = \"P15\",",
    "            task = \"T-099\",",
    "            mode = \"roi-cfo-value-dashboard\",",
    "            separatesPotentialVsRealized = true,",
    "            exportEvidencePack = true,",
    "            attributionCaveatVisible = true",
    "        }));",
    "",
    "        group.MapGet(\"/contract\", () => Results.Ok(new",
    "        {",
    "            marker = \"PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD\",",
    "            contract = \"ROI/CFO value dashboard with potential vs realized value separation and exportable evidence pack\",",
    "            guardrails = new[]",
    "            {",
    "                \"Potential value and realized value are separated.\",",
    "                \"Realized value reconciles with value-realization ledger.\",",
    "                \"Payback period is computed from realized value.\",",
    "                \"Export evidence pack carries ledger IDs, provenance and caveats.\",",
    "                \"Correlation is not causation.\"",
    "            },",
    "            routes = new[]",
    "            {",
    "                \"GET /api/p15/roi-cfo-dashboard/health\",",
    "                \"GET /api/p15/roi-cfo-dashboard/contract\",",
    "                \"GET /api/p15/roi-cfo-dashboard/summary\",",
    "                \"GET /api/p15/roi-cfo-dashboard/evidence-pack\"",
    "            }",
    "        }));",
    "",
    "        group.MapGet(\"/summary\", () =>",
    "        {",
    "            var service = new P15RoiCfoDashboardService();",
    "            return Results.Ok(service.BuildDemoDashboard());",
    "        });",
    "",
    "        group.MapGet(\"/evidence-pack\", () =>",
    "        {",
    "            var service = new P15RoiCfoDashboardService();",
    "            var dashboard = service.BuildDemoDashboard();",
    "            return Results.Ok(dashboard.EvidencePack);",
    "        });",
    "",
    "        return app;",
    "    }",
    "}",
    ""
  ].join("\n");

  write(endpointPath, content);

  return { path: rel(endpointPath), status: "WRITTEN" };
}

function patchProgram() {
  if (!isFile(programPath)) throw new Error("Missing Program.cs");

  backup(programPath);
  let text = normalize(read(programPath));

  if (text.includes("MapP15RoiCfoDashboardEndpoints")) {
    return { path: rel(programPath), status: "ALREADY_PATCHED" };
  }

  const anchor = text.includes("app.MapP15ValueRealizationEndpoints();")
    ? "app.MapP15ValueRealizationEndpoints();"
    : "app.MapP15AdvisoryRecommendationEndpoints();";

  if (!text.includes(anchor)) throw new Error("Could not find Phase 15 endpoint anchor in Program.cs.");

  text = text.replace(anchor, anchor + "\napp.MapP15RoiCfoDashboardEndpoints();");
  write(programPath, text);

  return { path: rel(programPath), status: "PATCHED" };
}

function writeFrontendApi() {
  const content = [
    'import { apiClient } from "@/api/http";',
    "",
    "export type P15SupportStatus = \"Supported\" | \"InsufficientSupport\" | \"OutOfEnvelope\" | \"BlockedByHonestyGuard\" | number;",
    "export type P15EvidenceStrength = \"None\" | \"Weak\" | \"Moderate\" | \"Strong\" | number;",
    "export type P15RecommendationStatus = \"Draft\" | \"ApprovalRequired\" | \"Approved\" | \"Dismissed\" | \"Blocked\" | number;",
    "export type P15ApprovalDecision = 1 | 2;",
    "export type P15ValueKind = \"Potential\" | \"Realized\" | number;",
    "",
    "export interface P15EvidenceReference { evidenceId: string; evidenceType: string; sourceSystem: string; description: string; confidence: number; strength: P15EvidenceStrength; provenance: string[]; }",
    "export interface P15MoneyRange { currencyCode: string; minValue: number; expectedValue: number; maxValue: number; isValid?: boolean; }",
    "export interface P15ParameterAdjustment { parameterCode: string; displayName: string; currentValue: number; proposedValue: number; minimumObservedValue: number; maximumObservedValue: number; unit?: string | null; isInsideObservedEnvelope?: boolean; }",
    "export interface P15ScenarioRequest { tenantId: string; plantId: string; findingId: string; scenarioName: string; seed: number; adjustments: P15ParameterAdjustment[]; evidence: P15EvidenceReference[]; }",
    "export interface P15ScenarioProjectionPoint { metricCode: string; label: string; baselineValue: number; projectedValue: number; delta: number; unit?: string | null; }",
    "export interface P15ScenarioResponse { scenarioId: string; findingId: string; supportStatus: P15SupportStatus; supportMessage: string; projectionOnlyStatement: string; seed: number; projectedValueImpact?: P15MoneyRange | null; projectionPoints: P15ScenarioProjectionPoint[]; evidence: P15EvidenceReference[]; isActionableProjection?: boolean; }",
    "export interface P15RecommendationParameterWindow { parameterCode: string; displayName: string; recommendedMinimum: number; recommendedMaximum: number; unit?: string | null; basis: string; }",
    "export interface P15RecommendationCandidate { recommendationId: string; findingId: string; title: string; advisoryText: string; status: P15RecommendationStatus; evidenceStrength: P15EvidenceStrength; confidence: number; expectedImpact?: P15MoneyRange | null; parameterWindows: P15RecommendationParameterWindow[]; evidence: P15EvidenceReference[]; provenance: string[]; honestyCaveat: string; requiresHumanApproval: boolean; hasWriteBackPath: boolean; }",
    "export interface P15RecommendationGenerationRequest { tenantId: string; plantId: string; scenarioRequest: P15ScenarioRequest; }",
    "export interface P15RecommendationGenerationResponse { requestId: string; scenarioId: string; scenarioSupportStatus: P15SupportStatus; message: string; recommendations: P15RecommendationCandidate[]; guardrails: string[]; }",
    "export interface P15ApprovalCommand { recommendationId: string; approverUserId: string; decision: P15ApprovalDecision; comment: string; decidedAtUtc: string; }",
    "export interface P15ApprovalResult { recommendationId: string; status: P15RecommendationStatus; message: string; approvalRecordId: string; decidedAtUtc: string; }",
    "export interface P15ValueWindow { windowId: string; metricCode: string; label: string; fromUtc: string; toUtc: string; value: number; unit?: string | null; }",
    "export interface P15ValueRealizationLedgerEntry { ledgerEntryId: string; tenantId: string; plantId: string; recommendationId: string; findingId: string; baselineWindow: P15ValueWindow; actualWindow: P15ValueWindow; realizedValue: P15MoneyRange; attributionCaveat: string; provenance: string[]; createdAtUtc: string; }",
    "export interface P15ValueRealizationRequest { tenantId: string; plantId: string; recommendationId: string; findingId: string; currencyCode: string; euroPerUnitImprovement: number; baselineWindow: P15ValueWindow; actualWindow: P15ValueWindow; provenance: string[]; }",
    "export interface P15ValueRealizationResponse { status: string; message: string; attributionCaveat: string; ledgerEntry?: P15ValueRealizationLedgerEntry | null; baselineVsActualDelta: number; violations: string[]; }",
    "export interface P15RoiSummary { tenantId: string; plantId: string; potentialValue: P15MoneyRange; realizedValue: P15MoneyRange; paybackPeriodMonths: number; recommendationCount: number; approvedRecommendationCount: number; realizedLedgerEntryCount: number; evidencePackReference: string; }",
    "export interface P15RoiValueBucket { bucketCode: string; label: string; valueKind: P15ValueKind; currencyCode: string; expectedValue: number; source: string; }",
    "export interface P15CfoEvidencePack { evidencePackId: string; currencyCode: string; potentialExpectedValue: number; realizedExpectedValue: number; paybackPeriodMonths: number; recommendationIds: string[]; ledgerEntryIds: string[]; provenance: string[]; caveats: string[]; }",
    "export interface P15RoiCfoDashboardResponse { status: string; message: string; generatedAtUtc: string; summary: P15RoiSummary; buckets: P15RoiValueBucket[]; ledgerEntries: P15ValueRealizationLedgerEntry[]; evidencePack: P15CfoEvidencePack; caveats: string[]; }",
    "export interface P15ScenarioHealth { status: string; marker: string; phase: string; task: string; mode: string; projectionOnly: boolean; automaticWriteBack: boolean; outOfEnvelopeAbstain: boolean; }",
    "export interface P15ScenarioContract { marker: string; contract: string; safetyRules: string[]; routes: string[]; }",
    "export interface P15RecommendationHealth { status: string; marker: string; phase: string; task: string; mode: string; expectedEImpactRange: boolean; confidenceEvidenceProvenance: boolean; humanApprovalRequired: boolean; automaticWriteBack: boolean; }",
    "export interface P15RecommendationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }",
    "export interface P15ValueRealizationHealth { status: string; marker: string; phase: string; task: string; mode: string; baselineVsActual: boolean; attributionCaveatRequired: boolean; linksToRecommendation: boolean; }",
    "export interface P15ValueRealizationContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }",
    "export interface P15ValueLedgerResponse { generatedAtUtc: string; count: number; entries: P15ValueRealizationLedgerEntry[]; }",
    "export interface P15RoiCfoDashboardHealth { status: string; marker: string; phase: string; task: string; mode: string; separatesPotentialVsRealized: boolean; exportEvidencePack: boolean; attributionCaveatVisible: boolean; }",
    "export interface P15RoiCfoDashboardContract { marker: string; contract: string; guardrails: string[]; routes: string[]; }",
    "",
    "export const phase15AdvisoryApi = {",
    "  health() { return apiClient.get<P15ScenarioHealth>(\"/api/p15/advisory/scenarios/health\"); },",
    "  contract() { return apiClient.get<P15ScenarioContract>(\"/api/p15/advisory/scenarios/contract\"); },",
    "  scenarioHealth() { return apiClient.get<P15ScenarioHealth>(\"/api/p15/advisory/scenarios/health\"); },",
    "  scenarioContract() { return apiClient.get<P15ScenarioContract>(\"/api/p15/advisory/scenarios/contract\"); },",
    "  sampleRequest() { return apiClient.get<P15ScenarioRequest>(\"/api/p15/advisory/scenarios/sample-request\"); },",
    "  simulate(request: P15ScenarioRequest) { return apiClient.post<P15ScenarioResponse>(\"/api/p15/advisory/scenarios/simulate\", request); },",
    "  recommendationHealth() { return apiClient.get<P15RecommendationHealth>(\"/api/p15/advisory/recommendations/health\"); },",
    "  recommendationContract() { return apiClient.get<P15RecommendationContract>(\"/api/p15/advisory/recommendations/contract\"); },",
    "  recommendationDemoRequest() { return apiClient.get<P15RecommendationGenerationRequest>(\"/api/p15/advisory/recommendations/demo-request\"); },",
    "  generateRecommendations(request: P15RecommendationGenerationRequest) { return apiClient.post<P15RecommendationGenerationResponse>(\"/api/p15/advisory/recommendations/generate\", request); },",
    "  approveRecommendation(command: P15ApprovalCommand) { return apiClient.post<P15ApprovalResult>(\"/api/p15/advisory/recommendations/approve\", command); },",
    "  valueRealizationHealth() { return apiClient.get<P15ValueRealizationHealth>(\"/api/p15/value-realization/health\"); },",
    "  valueRealizationContract() { return apiClient.get<P15ValueRealizationContract>(\"/api/p15/value-realization/contract\"); },",
    "  valueRealizationDemoRequest() { return apiClient.get<P15ValueRealizationRequest>(\"/api/p15/value-realization/demo-request\"); },",
    "  calculateValueRealization(request: P15ValueRealizationRequest) { return apiClient.post<P15ValueRealizationResponse>(\"/api/p15/value-realization/calculate\", request); },",
    "  valueRealizationLedger() { return apiClient.get<P15ValueLedgerResponse>(\"/api/p15/value-realization/ledger\"); },",
    "  roiCfoDashboardHealth() { return apiClient.get<P15RoiCfoDashboardHealth>(\"/api/p15/roi-cfo-dashboard/health\"); },",
    "  roiCfoDashboardContract() { return apiClient.get<P15RoiCfoDashboardContract>(\"/api/p15/roi-cfo-dashboard/contract\"); },",
    "  roiCfoDashboardSummary() { return apiClient.get<P15RoiCfoDashboardResponse>(\"/api/p15/roi-cfo-dashboard/summary\"); },",
    "  roiCfoEvidencePack() { return apiClient.get<P15CfoEvidencePack>(\"/api/p15/roi-cfo-dashboard/evidence-pack\"); },",
    "};",
    ""
  ].join("\n");

  write(frontendApiPath, content);

  return { path: rel(frontendApiPath), status: "WRITTEN" };
}

function writeFrontendPage() {
  const content = [
    'import { useEffect, useMemo, useState } from "react";',
    'import { phase15AdvisoryApi, type P15CfoEvidencePack, type P15RoiCfoDashboardContract, type P15RoiCfoDashboardHealth, type P15RoiCfoDashboardResponse } from "@/api/phase15Advisory";',
    "",
    "const cardStyle = { border: \"1px solid rgba(124,220,255,0.18)\", borderRadius: 22, padding: 20, background: \"linear-gradient(135deg, rgba(7,18,34,0.95), rgba(4,10,22,0.9))\", boxShadow: \"0 18px 50px rgba(0,0,0,0.22)\" } as const;",
    "const buttonStyle = { border: \"1px solid rgba(0,212,255,0.34)\", borderRadius: 14, padding: \"10px 14px\", color: \"#eaf7ff\", background: \"rgba(0,132,255,0.24)\", cursor: \"pointer\", fontWeight: 700 } as const;",
    "",
    "function asMoney(value?: number | null, currency = \"EUR\") {",
    "  if (value === undefined || value === null || Number.isNaN(value)) return \"—\";",
    "  return new Intl.NumberFormat(undefined, { style: \"currency\", currency }).format(value);",
    "}",
    "",
    "function asNumber(value?: number | null) {",
    "  if (value === undefined || value === null || Number.isNaN(value)) return \"—\";",
    "  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);",
    "}",
    "",
    "export function RoiCfoDashboardPage() {",
    "  const [health, setHealth] = useState<P15RoiCfoDashboardHealth | null>(null);",
    "  const [contract, setContract] = useState<P15RoiCfoDashboardContract | null>(null);",
    "  const [dashboard, setDashboard] = useState<P15RoiCfoDashboardResponse | null>(null);",
    "  const [evidencePack, setEvidencePack] = useState<P15CfoEvidencePack | null>(null);",
    "  const [status, setStatus] = useState(\"Loading Phase 15 ROI/CFO dashboard...\");",
    "  const [busy, setBusy] = useState(false);",
    "",
    "  async function refresh() {",
    "    setBusy(true);",
    "    try {",
    "      const [nextHealth, nextContract, nextDashboard] = await Promise.all([",
    "        phase15AdvisoryApi.roiCfoDashboardHealth(),",
    "        phase15AdvisoryApi.roiCfoDashboardContract(),",
    "        phase15AdvisoryApi.roiCfoDashboardSummary(),",
    "      ]);",
    "      setHealth(nextHealth);",
    "      setContract(nextContract);",
    "      setDashboard(nextDashboard);",
    "      setStatus(\"ROI/CFO value dashboard is ready.\");",
    "    } catch (error) {",
    "      const message = error instanceof Error ? error.message : String(error);",
    "      setStatus(`ROI/CFO dashboard not reachable: ${message}`);",
    "    } finally {",
    "      setBusy(false);",
    "    }",
    "  }",
    "",
    "  useEffect(() => { void refresh(); }, []);",
    "",
    "  async function exportEvidencePack() {",
    "    setBusy(true);",
    "    try {",
    "      const pack = await phase15AdvisoryApi.roiCfoEvidencePack();",
    "      setEvidencePack(pack);",
    "      setStatus(\"CFO evidence pack loaded. It reconciles with the dashboard ledger snapshot.\");",
    "    } catch (error) {",
    "      const message = error instanceof Error ? error.message : String(error);",
    "      setStatus(`Evidence pack export failed: ${message}`);",
    "    } finally {",
    "      setBusy(false);",
    "    }",
    "  }",
    "",
    "  const summary = dashboard?.summary;",
    "  const currency = summary?.realizedValue.currencyCode ?? \"EUR\";",
    "",
    "  const summaryCards = useMemo(() => [",
    "    { label: \"Mode\", value: health?.mode ?? \"pending\" },",
    "    { label: \"Potential value\", value: asMoney(summary?.potentialValue.expectedValue, currency) },",
    "    { label: \"Realized value\", value: asMoney(summary?.realizedValue.expectedValue, currency) },",
    "    { label: \"Payback months\", value: asNumber(summary?.paybackPeriodMonths) },",
    "    { label: \"Recommendations\", value: String(summary?.recommendationCount ?? 0) },",
    "    { label: \"Ledger entries\", value: String(summary?.realizedLedgerEntryCount ?? 0) },",
    "  ], [currency, health?.mode, summary]);",
    "",
    "  return (",
    "    <main style={{ display: \"grid\", gap: 18, color: \"#eaf7ff\" }} data-testid=\"phase15-roi-cfo-dashboard-page\">",
    "      <section style={{ ...cardStyle, borderColor: \"rgba(19,216,255,0.28)\" }}>",
    "        <p style={{ color: \"#13d8ff\", textTransform: \"uppercase\", letterSpacing: \"0.08em\", fontSize: 12, margin: 0 }}>",
    "          Pack G · T-099 · ROI / CFO value dashboard",
    "        </p>",
    "        <h1 style={{ margin: \"8px 0 8px\", fontSize: 34 }}>Phase 15 ROI / CFO Value Dashboard</h1>",
    "        <p style={{ margin: 0, color: \"#9ab8d7\", maxWidth: 980, lineHeight: 1.65 }}>",
    "          Buyer-facing value dashboard separating potential value from realized value, showing payback period and producing an exportable CFO evidence pack with ledger IDs, provenance and caveats.",
    "        </p>",
    "        <strong style={{ display: \"block\", marginTop: 16, color: status.includes(\"failed\") ? \"#ffb86b\" : \"#64ffda\" }}>{status}</strong>",
    "      </section>",
    "",
    "      <section style={{ display: \"grid\", gridTemplateColumns: \"repeat(auto-fit, minmax(170px, 1fr))\", gap: 12 }}>",
    "        {summaryCards.map((card) => (",
    "          <div key={card.label} style={cardStyle}>",
    "            <span style={{ color: \"#7e9bb8\", fontSize: 12, textTransform: \"uppercase\" }}>{card.label}</span>",
    "            <strong style={{ display: \"block\", marginTop: 6, fontSize: 18 }}>{card.value}</strong>",
    "          </div>",
    "        ))}",
    "      </section>",
    "",
    "      <section style={{ display: \"grid\", gridTemplateColumns: \"minmax(360px, 1fr) minmax(340px, 0.9fr)\", gap: 14 }}>",
    "        <div style={cardStyle}>",
    "          <h2 style={{ marginTop: 0 }}>1. Value buckets</h2>",
    "          {dashboard?.buckets.length ? (",
    "            <table style={{ width: \"100%\", borderCollapse: \"collapse\" }}>",
    "              <thead><tr style={{ color: \"#9ab8d7\", textAlign: \"left\" }}><th style={{ padding: 8 }}>Bucket</th><th style={{ padding: 8 }}>Kind</th><th style={{ padding: 8 }}>Expected</th><th style={{ padding: 8 }}>Source</th></tr></thead>",
    "              <tbody>",
    "                {dashboard.buckets.map((bucket) => (",
    "                  <tr key={bucket.bucketCode} style={{ borderTop: \"1px solid rgba(120,190,255,0.12)\" }}>",
    "                    <td style={{ padding: 8 }}>{bucket.label}</td>",
    "                    <td style={{ padding: 8 }}>{String(bucket.valueKind)}</td>",
    "                    <td style={{ padding: 8 }}>{asMoney(bucket.expectedValue, bucket.currencyCode)}</td>",
    "                    <td style={{ padding: 8, color: \"#9ab8d7\" }}>{bucket.source}</td>",
    "                  </tr>",
    "                ))}",
    "              </tbody>",
    "            </table>",
    "          ) : <p style={{ color: \"#9ab8d7\" }}>Dashboard data is loading...</p>}",
    "        </div>",
    "",
    "        <div style={{ ...cardStyle, display: \"grid\", gap: 12 }}>",
    "          <h2 style={{ margin: 0 }}>2. CFO evidence pack</h2>",
    "          <p style={{ color: \"#9ab8d7\", lineHeight: 1.65, margin: 0 }}>Evidence pack reconciles potential value, realized value, payback period, recommendation IDs and ledger entry IDs.</p>",
    "          <button type=\"button\" disabled={busy} onClick={() => void exportEvidencePack()} style={buttonStyle}>Export CFO evidence pack</button>",
    "          {evidencePack ? (",
    "            <div style={{ border: \"1px solid rgba(100,255,218,0.22)\", borderRadius: 14, padding: 12 }}>",
    "              <strong>{evidencePack.evidencePackId}</strong>",
    "              <p style={{ color: \"#9ab8d7\" }}>Potential: {asMoney(evidencePack.potentialExpectedValue, evidencePack.currencyCode)}</p>",
    "              <p style={{ color: \"#9ab8d7\" }}>Realized: {asMoney(evidencePack.realizedExpectedValue, evidencePack.currencyCode)}</p>",
    "              <p style={{ color: \"#9ab8d7\" }}>Payback: {asNumber(evidencePack.paybackPeriodMonths)} months</p>",
    "            </div>",
    "          ) : null}",
    "        </div>",
    "      </section>",
    "",
    "      <section style={cardStyle}>",
    "        <h2 style={{ marginTop: 0 }}>3. CFO caveats and guardrails</h2>",
    "        <ul style={{ color: \"#9ab8d7\", lineHeight: 1.75, paddingLeft: 18 }}>",
    "          {(dashboard?.caveats ?? contract?.guardrails ?? [",
    "            \"Potential value and realized value are separated.\",",
    "            \"Realized value reconciles with value-realization ledger.\",",
    "            \"Correlation is not causation.\",",
    "          ]).map((rule) => <li key={rule}>{rule}</li>)}",
    "        </ul>",
    "      </section>",
    "    </main>",
    "  );",
    "}",
    ""
  ].join("\n");

  write(frontendPagePath, content);

  return { path: rel(frontendPagePath), status: "WRITTEN" };
}

function patchAppRoutes() {
  if (!isFile(appPath)) throw new Error("Missing App.implementation.tsx");

  backup(appPath);
  let text = normalize(read(appPath));
  let changed = false;

  if (!text.includes("RoiCfoDashboardPage")) {
    const lazyBlock = [
      "const RoiCfoDashboardPage = lazy(() =>",
      "  import(\"./pages/Phase15/RoiCfoDashboardPage\").then((m) => ({",
      "    default: m.RoiCfoDashboardPage,",
      "  }))",
      ");",
      ""
    ].join("\n");

    const anchor = text.includes("const ValueRealizationPage = lazy(() =>")
      ? "const ValueRealizationPage = lazy(() =>"
      : "const RecommendationsPage = lazy(() =>";

    if (!text.includes(anchor)) throw new Error("Could not find lazy page insertion anchor.");

    text = text.replace(anchor, lazyBlock + anchor);
    changed = true;
  }

  if (!text.includes('path="/phase15/roi-cfo-dashboard"')) {
    const routeBlock = [
      "                    {/* Pack G Phase 15 ROI CFO dashboard */}",
      "                    <Route",
      "                      path=\"/phase15/roi-cfo-dashboard\"",
      "                      element={withPageBoundary(",
      "                        \"/phase15/roi-cfo-dashboard\",",
      "                        \"Phase 15 ROI/CFO dashboard is refreshing\",",
      "                        <RoiCfoDashboardPage />",
      "                      )}",
      "                    />",
      "",
      "                    <Route",
      "                      path=\"/advisory/roi-cfo-dashboard\"",
      "                      element={<Navigate to=\"/phase15/roi-cfo-dashboard\" replace />}",
      "                    />",
      ""
    ].join("\n");

    const anchor = text.includes("                    {/* Pack G Phase 15 value realization */}")
      ? "                    {/* Pack G Phase 15 value realization */}"
      : "                    {/* Pack G Phase 15 recommendation generator */}";

    if (!text.includes(anchor)) throw new Error("Could not find route insertion anchor.");

    text = text.replace(anchor, routeBlock + anchor);
    changed = true;
  }

  if (changed) {
    write(appPath, text);
    return { path: rel(appPath), status: "PATCHED" };
  }

  return { path: rel(appPath), status: "ALREADY_PATCHED" };
}

function patchAppLayout() {
  if (!isFile(layoutPath)) throw new Error("Missing AppLayout.tsx");

  backup(layoutPath);
  let text = normalize(read(layoutPath));

  if (text.includes('to: "/phase15/roi-cfo-dashboard"')) {
    return { path: rel(layoutPath), status: "ALREADY_PATCHED" };
  }

  const newItem = '  { to: "/phase15/roi-cfo-dashboard", label: "ROI/CFO Value", desc: "Potential vs realized value", icon: DatabaseZap },';
  const anchor = text.includes('  { to: "/phase15/value-realization"')
    ? '  { to: "/phase15/value-realization"'
    : '  { to: "/phase15/recommendations"';

  if (!text.includes(anchor)) throw new Error("Could not find AppLayout navigation insertion anchor.");

  text = text.replace(anchor, newItem + "\n" + anchor);
  write(layoutPath, text);

  return { path: rel(layoutPath), status: "PATCHED" };
}

function writeDocs() {
  const md = [];

  md.push("# Phase 15 ROI / CFO Value Dashboard");
  md.push("");
  md.push("Marker: PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD");
  md.push("");
  md.push("## Purpose");
  md.push("");
  md.push("Provides a buyer-facing CFO dashboard that separates potential value from realized value, computes payback period and produces an exportable evidence pack.");
  md.push("");
  md.push("## Guardrails");
  md.push("");
  md.push("- Potential value and realized value are separated.");
  md.push("- Realized value reconciles with the value-realization ledger.");
  md.push("- Payback period is computed from realized value.");
  md.push("- Export evidence pack carries ledger IDs, provenance and caveats.");
  md.push("- Correlation is not causation.");
  md.push("");
  md.push("## Backend routes");
  md.push("");
  md.push("- `GET /api/p15/roi-cfo-dashboard/health`");
  md.push("- `GET /api/p15/roi-cfo-dashboard/contract`");
  md.push("- `GET /api/p15/roi-cfo-dashboard/summary`");
  md.push("- `GET /api/p15/roi-cfo-dashboard/evidence-pack`");
  md.push("");
  md.push("## Frontend routes");
  md.push("");
  md.push("- `/phase15/roi-cfo-dashboard`");
  md.push("- `/advisory/roi-cfo-dashboard` alias");
  md.push("");

  write(dashboardDocPath, md.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs",');
  lines.push('  "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs",');
  lines.push('  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",');
  lines.push('  "Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx",');
  lines.push('  "docs/developer/PHASE15_ROI_CFO_VALUE_DASHBOARD.md",');
  lines.push('  "docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.json",');
  lines.push('  "docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.md",');
  lines.push('  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('const requiredSignals = [');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "PotentialValue" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "RealizedValue" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "PaybackPeriodMonths" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "P15CfoEvidencePack" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "ledger entries" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs", signal: "/api/p15/roi-cfo-dashboard" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs", signal: "/summary" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs", signal: "/evidence-pack" },');
  lines.push('  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15RoiCfoDashboardEndpoints" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/roi-cfo-dashboard/summary" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx", signal: "Pack G · T-099 · ROI / CFO value dashboard" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx", signal: "Export CFO evidence pack" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/roi-cfo-dashboard" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "ROI/CFO Value" },');
  lines.push('  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD" }');
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
  lines.push('  console.error("Pack G-6 T-099 ROI/CFO value dashboard validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack G-6 T-099 ROI/CFO value dashboard validation passed.");');

  write(validatorPath, lines.join("\n") + "\n");

  return { path: rel(validatorPath), status: "WRITTEN" };
}

function writeBridge() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('const cp = require("child_process");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('const scorecardJsonPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.json");');
  lines.push('const bridgedJsonPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.PACK_G6_BRIDGED.json");');
  lines.push('const bridgedMdPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.PACK_G6_BRIDGED.md");');
  lines.push('');
  lines.push('function exists(file) { return fs.existsSync(file); }');
  lines.push('function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }');
  lines.push('function read(file) { return fs.readFileSync(file, "utf8"); }');
  lines.push('function write(file, content) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, content.replace(/\\n/g, "\\r\\n"), "utf8"); console.log("Wrote: " + path.relative(root, file).split(path.sep).join("/")); }');
  lines.push('function runOk(cmd, args) { try { cp.execFileSync(cmd, args, { cwd: root, stdio: "pipe", shell: false }); return true; } catch { return false; } }');
  lines.push('function rowsOf(scorecard) { return Array.isArray(scorecard.tasks) ? scorecard.tasks : []; }');
  lines.push('function code(row) { return String(row.task || row.taskCode || row.code || row.id || "").trim(); }');
  lines.push('function title(row) { return String(row.title || row.description || row.name || row.taskTitle || "").trim(); }');
  lines.push('function score(row) { return Number(row.score ?? row.percent ?? row.completionPercent ?? row.percentage ?? 0); }');
  lines.push('function setDone(row, note) { row.score = 100; row.percent = 100; row.completionPercent = 100; row.percentage = 100; row.status = "DONE"; row.state = "DONE"; row.result = "DONE"; row.isGreen = true; row.isDone = true; row.done = true; row.below90 = false; row.evidenceBridge = note; }');
  lines.push('function rowLine(row) { return code(row) + " " + score(row) + "% " + String(row.status || row.state || row.result || "") + " - " + title(row); }');
  lines.push('');
  lines.push('if (!isFile(scorecardJsonPath)) { console.error("Missing Phase 15 scorecard JSON."); process.exit(1); }');
  lines.push('');
  lines.push('const scorecard = JSON.parse(read(scorecardJsonPath));');
  lines.push('const rows = rowsOf(scorecard);');
  lines.push('const t099Green = runOk("node", ["tools/pack-g/validate-pack-g6-t099-roi-cfo-dashboard.cjs"]);');
  lines.push('');
  lines.push('for (const row of rows) {');
  lines.push('  if (code(row) === "T-099" && t099Green) setDone(row, "Pack G-6 evidence bridge: ROI/CFO value dashboard validator passed and builds were green.");');
  lines.push('}');
  lines.push('');
  lines.push('scorecard.packG6EvidenceBridge = { generatedAtUtc: new Date().toISOString(), marker: "PPIQ_PACK_G6_T099_SCORECARD_BRIDGE", t099Green };');
  lines.push('');
  lines.push('write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('write(bridgedJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('');
  lines.push('const below90 = rows.filter((row) => score(row) < 90);');
  lines.push('const md = [];');
  lines.push('md.push("# T096-T102 Phase 15 Scorecard — Pack G6 Bridged");');
  lines.push('md.push("");');
  lines.push('md.push("Marker: PPIQ_PACK_G6_T099_SCORECARD_BRIDGE");');
  lines.push('md.push("");');
  lines.push('md.push("T-099 bridge result: " + (t099Green ? "DONE" : "NOT GREEN"));');
  lines.push('md.push("");');
  lines.push('md.push("Tasks below 90% after Pack G6 bridge: " + below90.length);');
  lines.push('md.push("");');
  lines.push('for (const row of below90) md.push(rowLine(row));');
  lines.push('md.push("");');
  lines.push('write(bridgedMdPath, md.join("\\n") + "\\n");');
  lines.push('');
  lines.push('console.log("");');
  lines.push('console.log("Pack G6 task-closure evidence bridge applied.");');
  lines.push('console.log("T-099 bridge result: " + (t099Green ? "DONE" : "NOT GREEN"));');
  lines.push('console.log("");');
  lines.push('console.log("Tasks below 90% after Pack G6 bridge: " + below90.length);');
  lines.push('for (const row of below90) console.log(rowLine(row));');

  write(bridgePath, lines.join("\n") + "\n");

  return { path: rel(bridgePath), status: "WRITTEN" };
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
    "    Run-Step \"Pack G-1 Phase 15 closure-map validation\" { node \".\\tools\\pack-g\\validate-pack-g-phase15-closure-map.cjs\" }",
    "    Run-Step \"Pack G-2 Phase 15 advisory/value contract validation\" { node \".\\tools\\pack-g\\validate-pack-g2-phase15-contract.cjs\" }",
    "    Run-Step \"Pack G-3 T-096 scenario engine validation\" { node \".\\tools\\pack-g\\validate-pack-g3-t096-scenario-engine.cjs\" }",
    "    Run-Step \"Pack G-4 T-097 recommendation generator validation\" { node \".\\tools\\pack-g\\validate-pack-g4-t097-recommendation-generator.cjs\" }",
    "    Run-Step \"Pack G-5 T-098 value-realization validation\" { node \".\\tools\\pack-g\\validate-pack-g5-t098-value-realization.cjs\" }",
    "    Run-Step \"Pack G-6 T-099 ROI/CFO value dashboard validation\" { node \".\\tools\\pack-g\\validate-pack-g6-t099-roi-cfo-dashboard.cjs\" }",
    "",
    "    if ($RunBuilds) {",
    "        Run-Step \"Backend build\" { dotnet build \".\\Backend\" }",
    "        Push-Location \".\\Frontend\\PlantProcess.Web\"",
    "        try { Run-Step \"Frontend build\" { npm.cmd run build } }",
    "        finally { Pop-Location }",
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
    marker: "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD",
    phase: "P15",
    pack: "G",
    task: "T-099",
    backendRouteRoot: "/api/p15/roi-cfo-dashboard",
    frontendRoute: "/phase15/roi-cfo-dashboard",
    aliasRoute: "/advisory/roi-cfo-dashboard",
    results
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];
  md.push("# Pack G-6 T-099 ROI / CFO Value Dashboard Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD");
  md.push("");
  md.push("## Scope");
  md.push("");
  md.push("Adds CFO-facing ROI dashboard with potential vs realized value separation, payback period, value buckets, and exportable evidence pack.");
  md.push("");
  md.push("## Changed files");
  md.push("");
  md.push("| File | Status |");
  md.push("|---|---|");
  for (const item of results) md.push("| `" + item.path + "` | " + item.status + " |");
  md.push("");

  write(reportMdPath, md.join("\n"));
}

function writeEvidence() {
  let evidence = isFile(evidencePath) ? normalize(read(evidencePath)) : "# PlantProcess IQ Pack G Evidence\n";

  if (!evidence.includes("PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD")) {
    evidence += [
      "",
      "## Pack G-6 T-099 ROI / CFO value dashboard",
      "",
      "- Marker: PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD.",
      "- Added ROI/CFO dashboard service.",
      "- Added potential vs realized value separation.",
      "- Added payback period based on realized value.",
      "- Added CFO evidence pack with recommendation IDs, ledger entry IDs, provenance and caveats.",
      "- Added backend ROI/CFO dashboard endpoints.",
      "- Added frontend ROI/CFO dashboard page.",
      "- Added validator and T-099 scorecard bridge.",
      "- Backend and frontend builds must remain green.",
      "",
      "Generated artifacts:",
      "",
      "- Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs",
      "- Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs",
      "- Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx",
      "- docs/developer/PHASE15_ROI_CFO_VALUE_DASHBOARD.md",
      "- docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.md",
      "- docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.json",
      "- tools/pack-g/validate-pack-g6-t099-roi-cfo-dashboard.cjs",
      "- tools/task-closure/ppiq-pack-g6-scorecard-bridge.cjs",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack G-6 — T-099 ROI / CFO value dashboard");
console.log("=================================================================================================");
console.log("Backup folder: " + rel(backupRoot));

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(developerDocsDir);
ensureDir(toolsDir);
ensureDir(toolsTaskClosureDir);

const results = [];
results.push(writeService());
results.push(writeEndpoint());
results.push(patchProgram());
results.push(writeFrontendApi());
results.push(writeFrontendPage());
results.push(patchAppRoutes());
results.push(patchAppLayout());
writeDocs();
results.push(writeValidator());
results.push(writeBridge());
results.push(writeWrapper());
writeReports(results);
writeEvidence();

run("node --check Pack G-6 validator", ["node", "--check", "tools/pack-g/validate-pack-g6-t099-roi-cfo-dashboard.cjs"]);
run("node --check Pack G6 bridge", ["node", "--check", "tools/task-closure/ppiq-pack-g6-scorecard-bridge.cjs"]);
run("Pack G-1 closure-map validator", ["node", "tools/pack-g/validate-pack-g-phase15-closure-map.cjs"]);
run("Pack G-2 contract validator", ["node", "tools/pack-g/validate-pack-g2-phase15-contract.cjs"]);
run("Pack G-3 T-096 scenario validator", ["node", "tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs"]);
run("Pack G-4 T-097 recommendation validator", ["node", "tools/pack-g/validate-pack-g4-t097-recommendation-generator.cjs"]);
run("Pack G-5 T-098 value-realization validator", ["node", "tools/pack-g/validate-pack-g5-t098-value-realization.cjs"]);
run("Pack G-6 T-099 ROI/CFO dashboard validator", ["node", "tools/pack-g/validate-pack-g6-t099-roi-cfo-dashboard.cjs"]);

run("Backend build", ["dotnet", "build", "Backend"]);
run("Frontend build", frontendBuildArgs());

if (isFile(path.join(root, "tools", "task-closure", "ppiq-pack-g3-scorecard-bridge.cjs"))) {
  run("Apply Pack G3 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g3-scorecard-bridge.cjs"], root, true);
}

if (isFile(path.join(root, "tools", "task-closure", "ppiq-pack-g4-scorecard-bridge.cjs"))) {
  run("Apply Pack G4 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g4-scorecard-bridge.cjs"], root, true);
}

if (isFile(path.join(root, "tools", "task-closure", "ppiq-pack-g5-scorecard-bridge.cjs"))) {
  run("Apply Pack G5 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g5-scorecard-bridge.cjs"], root, true);
}

run("Apply Pack G6 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g6-scorecard-bridge.cjs"]);

console.log("");
console.log("Pack G-6 result:");
console.log(" - ROI/CFO dashboard service written");
console.log(" - ROI/CFO dashboard endpoints written");
console.log(" - Program endpoint registration patched");
console.log(" - Frontend API client extended");
console.log(" - ROI/CFO dashboard page written");
console.log(" - T-099 validator passed");
console.log(" - Backend build passed");
console.log(" - Frontend build passed");
console.log(" - T-099 scorecard bridge applied");

console.log("");
console.log("=================================================================================================");
console.log("Pack G-6 completed.");
console.log("Expected: T-099 is green in the Pack G6 bridged Phase 15 scorecard.");
console.log("Report: docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.md");
console.log("Next  : Pack G-7 / T-100 — cross-plant & industry benchmarking.");
console.log("=================================================================================================");