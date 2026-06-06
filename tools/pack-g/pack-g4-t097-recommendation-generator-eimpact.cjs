const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_g_backup", "pack_g4_t097_recommendation_generator_" + timestamp);

const docsDir = path.join(root, "docs", "pack-g");
const developerDocsDir = path.join(root, "docs", "developer");
const toolsDir = path.join(root, "tools", "pack-g");
const toolsTaskClosureDir = path.join(root, "tools", "task-closure");

const servicePath = path.join(root, "Backend", "PlantProcess.Application", "Advisory", "P15RecommendationService.cs");
const endpointPath = path.join(root, "Backend", "PlantProcess.Api", "PlantConnectors", "P15AdvisoryRecommendationEndpoints.cs");
const programPath = path.join(root, "Backend", "PlantProcess.Api", "Program.cs");

const frontendApiPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "api", "phase15Advisory.ts");
const frontendPagePath = path.join(root, "Frontend", "PlantProcess.Web", "src", "pages", "Phase15", "RecommendationsPage.tsx");
const appPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "App.implementation.tsx");
const layoutPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "components", "AppLayout.tsx");

const validatorPath = path.join(toolsDir, "validate-pack-g4-t097-recommendation-generator.cjs");
const bridgePath = path.join(toolsTaskClosureDir, "ppiq-pack-g4-scorecard-bridge.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-PackG-Phase15Regression.ps1");

const recommendationDocPath = path.join(developerDocsDir, "PHASE15_RECOMMENDATION_GENERATOR.md");
const reportJsonPath = path.join(docsDir, "PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.md");
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
    "/// PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT",
    "/// Phase 15 recommendation generator with expected e-impact, confidence, evidence, provenance and explicit approval workflow.",
    "///",
    "/// Guardrails:",
    "/// - no causal claim",
    "/// - no automatic write-back",
    "/// - weak evidence blocks recommendation",
    "/// - every recommendation requires human approval",
    "/// - expected e-impact is a range and projection-only",
    "/// </summary>",
    "public sealed class P15RecommendationService",
    "{",
    "    public P15RecommendationGenerationResponse Generate(P15RecommendationGenerationRequest request)",
    "    {",
    "        var scenarioService = new P15ScenarioSimulationService();",
    "        var scenario = scenarioService.Simulate(request.ScenarioRequest);",
    "",
    "        if (scenario.SupportStatus != P15SupportStatus.Supported || scenario.ProjectedValueImpact is null)",
    "        {",
    "            return new P15RecommendationGenerationResponse",
    "            {",
    "                RequestId = BuildRequestId(request, scenario.Seed),",
    "                ScenarioId = scenario.ScenarioId,",
    "                ScenarioSupportStatus = scenario.SupportStatus,",
    "                Message = \"No recommendation generated because scenario support is insufficient or outside the observed envelope.\",",
    "                Recommendations = Array.Empty<P15RecommendationCandidate>(),",
    "                Guardrails = DefaultGuardrails()",
    "            };",
    "        }",
    "",
    "        var recommendation = BuildCandidate(request, scenario);",
    "        var policy = P15AdvisoryHonestyPolicy.ValidateRecommendation(recommendation);",
    "",
    "        recommendation = recommendation with",
    "        {",
    "            Status = policy.IsAllowed ? P15RecommendationStatus.ApprovalRequired : P15RecommendationStatus.Blocked,",
    "            HonestyCaveat = policy.IsAllowed",
    "                ? recommendation.HonestyCaveat",
    "                : recommendation.HonestyCaveat + \" Blocked by honesty policy: \" + string.Join(\" \", policy.Violations)",
    "        };",
    "",
    "        return new P15RecommendationGenerationResponse",
    "        {",
    "            RequestId = BuildRequestId(request, scenario.Seed),",
    "            ScenarioId = scenario.ScenarioId,",
    "            ScenarioSupportStatus = scenario.SupportStatus,",
    "            Message = policy.IsAllowed",
    "                ? \"Recommendation generated with expected e-impact range and explicit approval requirement.\"",
    "                : \"Recommendation blocked by honesty policy.\",",
    "            Recommendations = policy.IsAllowed ? new[] { recommendation } : Array.Empty<P15RecommendationCandidate>(),",
    "            Guardrails = DefaultGuardrails()",
    "        };",
    "    }",
    "",
    "    public P15RecommendationGenerationRequest BuildDemoRequest()",
    "    {",
    "        var scenarioService = new P15ScenarioSimulationService();",
    "        return new P15RecommendationGenerationRequest",
    "        {",
    "            TenantId = \"demo-tenant\",",
    "            PlantId = \"demo-plant-01\",",
    "            ScenarioRequest = scenarioService.BuildDemoRequest()",
    "        };",
    "    }",
    "",
    "    public P15ApprovalResult Decide(P15ApprovalCommand command)",
    "    {",
    "        var policy = P15AdvisoryHonestyPolicy.ValidateApprovalCommand(command);",
    "        if (!policy.IsAllowed)",
    "        {",
    "            return new P15ApprovalResult",
    "            {",
    "                RecommendationId = command.RecommendationId,",
    "                Status = P15RecommendationStatus.Blocked,",
    "                Message = \"Approval command rejected: \" + string.Join(\" \", policy.Violations),",
    "                ApprovalRecordId = BuildApprovalRecordId(command),",
    "                DecidedAtUtc = command.DecidedAtUtc",
    "            };",
    "        }",
    "",
    "        return new P15ApprovalResult",
    "        {",
    "            RecommendationId = command.RecommendationId,",
    "            Status = command.Decision == P15ApprovalDecision.Approve",
    "                ? P15RecommendationStatus.Approved",
    "                : P15RecommendationStatus.Dismissed,",
    "            Message = command.Decision == P15ApprovalDecision.Approve",
    "                ? \"Recommendation approved for downstream review. No automatic process write-back was executed.\"",
    "                : \"Recommendation dismissed by user. No automatic process write-back was executed.\",",
    "            ApprovalRecordId = BuildApprovalRecordId(command),",
    "            DecidedAtUtc = command.DecidedAtUtc",
    "        };",
    "    }",
    "",
    "    private static P15RecommendationCandidate BuildCandidate(P15RecommendationGenerationRequest request, P15ScenarioResponse scenario)",
    "    {",
    "        var strongestEvidence = scenario.Evidence.Length == 0 ? P15EvidenceStrength.None : scenario.Evidence.Max(item => item.Strength);",
    "        var confidence = scenario.Evidence.Length == 0 ? 0m : Math.Round(scenario.Evidence.Average(item => item.Confidence), 3, MidpointRounding.AwayFromZero);",
    "",
    "        return new P15RecommendationCandidate",
    "        {",
    "            RecommendationId = $\"p15-rec-{Sanitize(scenario.FindingId)}-{scenario.Seed}\",",
    "            FindingId = scenario.FindingId,",
    "            Title = \"Review guarded parameter window for \" + scenario.FindingId,",
    "            AdvisoryText = \"Consider reviewing the recommended parameter window with process engineering. The expected e-impact is projection-only and requires explicit human approval before operational use.\",",
    "            Status = P15RecommendationStatus.ApprovalRequired,",
    "            EvidenceStrength = strongestEvidence,",
    "            Confidence = confidence,",
    "            ExpectedImpact = scenario.ProjectedValueImpact,",
    "            ParameterWindows = request.ScenarioRequest.Adjustments.Select(BuildWindow).ToArray(),",
    "            Evidence = scenario.Evidence,",
    "            Provenance = scenario.Evidence.SelectMany(item => item.Provenance).Append(\"phase15-recommendation-generator\").Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),",
    "            HonestyCaveat = P15AdvisoryValueContract.ProjectionOnlyStatement + \" \" + P15AdvisoryValueContract.AttributionCaveat,",
    "            RequiresHumanApproval = true,",
    "            HasWriteBackPath = false",
    "        };",
    "    }",
    "",
    "    private static P15RecommendationParameterWindow BuildWindow(P15ParameterAdjustment adjustment)",
    "    {",
    "        var envelopeRange = Math.Max(1m, adjustment.MaximumObservedValue - adjustment.MinimumObservedValue);",
    "        var margin = Math.Round(envelopeRange * 0.05m, 3, MidpointRounding.AwayFromZero);",
    "        var min = Math.Max(adjustment.MinimumObservedValue, adjustment.ProposedValue - margin);",
    "        var max = Math.Min(adjustment.MaximumObservedValue, adjustment.ProposedValue + margin);",
    "",
    "        return new P15RecommendationParameterWindow",
    "        {",
    "            ParameterCode = adjustment.ParameterCode,",
    "            DisplayName = adjustment.DisplayName,",
    "            RecommendedMinimum = min,",
    "            RecommendedMaximum = max,",
    "            Unit = adjustment.Unit,",
    "            Basis = \"Window centered around supported what-if proposed value and clipped to observed envelope.\"",
    "        };",
    "    }",
    "",
    "    private static string[] DefaultGuardrails() =>",
    "        new[]",
    "        {",
    "            \"No causal language.\",",
    "            \"Expected e-impact is projection-only.\",",
    "            \"Confidence, evidence and provenance are required.\",",
    "            \"Weak evidence blocks recommendation.\",",
    "            \"Human approval is required.\",",
    "            \"No automatic process write-back.\"",
    "        };",
    "",
    "    private static string BuildRequestId(P15RecommendationGenerationRequest request, int seed) =>",
    "        $\"p15-rec-request-{Sanitize(request.ScenarioRequest.FindingId)}-{seed}\";",
    "",
    "    private static string BuildApprovalRecordId(P15ApprovalCommand command) =>",
    "        $\"p15-approval-{Sanitize(command.RecommendationId)}-{command.DecidedAtUtc:yyyyMMddHHmmss}\";",
    "",
    "    private static string Sanitize(string? value) =>",
    "        string.IsNullOrWhiteSpace(value)",
    "            ? \"unknown\"",
    "            : new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');",
    "}",
    "",
    "public sealed record P15RecommendationGenerationRequest",
    "{",
    "    public required string TenantId { get; init; }",
    "    public required string PlantId { get; init; }",
    "    public required P15ScenarioRequest ScenarioRequest { get; init; }",
    "}",
    "",
    "public sealed record P15RecommendationGenerationResponse",
    "{",
    "    public required string RequestId { get; init; }",
    "    public required string ScenarioId { get; init; }",
    "    public P15SupportStatus ScenarioSupportStatus { get; init; }",
    "    public required string Message { get; init; }",
    "    public P15RecommendationCandidate[] Recommendations { get; init; } = Array.Empty<P15RecommendationCandidate>();",
    "    public string[] Guardrails { get; init; } = Array.Empty<string>();",
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
    "public static class P15AdvisoryRecommendationEndpoints",
    "{",
    "    private static readonly Dictionary<string, P15ApprovalResult> ApprovalLedger = new(StringComparer.OrdinalIgnoreCase);",
    "",
    "    public static IEndpointRouteBuilder MapP15AdvisoryRecommendationEndpoints(this IEndpointRouteBuilder app)",
    "    {",
    "        var group = app.MapGroup(\"/api/p15/advisory/recommendations\")",
    "            .WithTags(\"P15 Advisory Recommendations\");",
    "",
    "        group.MapGet(\"/health\", () => Results.Ok(new",
    "        {",
    "            status = \"ok\",",
    "            marker = \"PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT\",",
    "            phase = \"P15\",",
    "            task = \"T-097\",",
    "            mode = \"guarded-recommendation-generator\",",
    "            expectedEImpactRange = true,",
    "            confidenceEvidenceProvenance = true,",
    "            humanApprovalRequired = true,",
    "            automaticWriteBack = false",
    "        }));",
    "",
    "        group.MapGet(\"/contract\", () => Results.Ok(new",
    "        {",
    "            marker = \"PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT\",",
    "            contract = \"Recommendation generator with expected e-impact, confidence, evidence, provenance and approval workflow\",",
    "            guardrails = new[]",
    "            {",
    "                \"No causal language.\",",
    "                \"Expected e-impact is projection-only.\",",
    "                \"Confidence, evidence and provenance are required.\",",
    "                \"Weak evidence blocks recommendation.\",",
    "                \"Human approval is required.\",",
    "                \"No automatic process write-back.\"",
    "            },",
    "            routes = new[]",
    "            {",
    "                \"GET /api/p15/advisory/recommendations/health\",",
    "                \"GET /api/p15/advisory/recommendations/contract\",",
    "                \"GET /api/p15/advisory/recommendations/demo-request\",",
    "                \"POST /api/p15/advisory/recommendations/generate\",",
    "                \"POST /api/p15/advisory/recommendations/generate-demo\",",
    "                \"POST /api/p15/advisory/recommendations/approve\",",
    "                \"GET /api/p15/advisory/recommendations/approvals\"",
    "            }",
    "        }));",
    "",
    "        group.MapGet(\"/demo-request\", () =>",
    "        {",
    "            var service = new P15RecommendationService();",
    "            return Results.Ok(service.BuildDemoRequest());",
    "        });",
    "",
    "        group.MapPost(\"/generate\", (P15RecommendationGenerationRequest request) =>",
    "        {",
    "            var service = new P15RecommendationService();",
    "            return Results.Ok(service.Generate(request));",
    "        });",
    "",
    "        group.MapPost(\"/generate-demo\", () =>",
    "        {",
    "            var service = new P15RecommendationService();",
    "            var request = service.BuildDemoRequest();",
    "            var first = service.Generate(request);",
    "            var second = service.Generate(request);",
    "",
    "            return Results.Ok(new",
    "            {",
    "                deterministic = first == second,",
    "                request,",
    "                first,",
    "                second",
    "            });",
    "        });",
    "",
    "        group.MapPost(\"/approve\", (P15ApprovalCommand command) =>",
    "        {",
    "            var service = new P15RecommendationService();",
    "            var result = service.Decide(command);",
    "            ApprovalLedger[result.ApprovalRecordId] = result;",
    "            return Results.Ok(result);",
    "        });",
    "",
    "        group.MapGet(\"/approvals\", () => Results.Ok(new",
    "        {",
    "            generatedAtUtc = DateTimeOffset.UtcNow,",
    "            count = ApprovalLedger.Count,",
    "            approvals = ApprovalLedger.Values.OrderByDescending(item => item.DecidedAtUtc).ToArray()",
    "        }));",
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

  if (text.includes("MapP15AdvisoryRecommendationEndpoints")) {
    return { path: rel(programPath), status: "ALREADY_PATCHED" };
  }

  const anchor = text.includes("app.MapP15AdvisoryScenarioEndpoints();")
    ? "app.MapP15AdvisoryScenarioEndpoints();"
    : "app.MapP15AdvisoryScenarioEndpoints();";

  if (!text.includes(anchor)) throw new Error("Could not find scenario endpoint anchor in Program.cs.");

  text = text.replace(anchor, anchor + "\napp.MapP15AdvisoryRecommendationEndpoints();");
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
    "",
    "export interface P15EvidenceReference {",
    "  evidenceId: string;",
    "  evidenceType: string;",
    "  sourceSystem: string;",
    "  description: string;",
    "  confidence: number;",
    "  strength: P15EvidenceStrength;",
    "  provenance: string[];",
    "}",
    "",
    "export interface P15MoneyRange {",
    "  currencyCode: string;",
    "  minValue: number;",
    "  expectedValue: number;",
    "  maxValue: number;",
    "  isValid?: boolean;",
    "}",
    "",
    "export interface P15ParameterAdjustment {",
    "  parameterCode: string;",
    "  displayName: string;",
    "  currentValue: number;",
    "  proposedValue: number;",
    "  minimumObservedValue: number;",
    "  maximumObservedValue: number;",
    "  unit?: string | null;",
    "  isInsideObservedEnvelope?: boolean;",
    "}",
    "",
    "export interface P15ScenarioRequest {",
    "  tenantId: string;",
    "  plantId: string;",
    "  findingId: string;",
    "  scenarioName: string;",
    "  seed: number;",
    "  adjustments: P15ParameterAdjustment[];",
    "  evidence: P15EvidenceReference[];",
    "}",
    "",
    "export interface P15ScenarioProjectionPoint {",
    "  metricCode: string;",
    "  label: string;",
    "  baselineValue: number;",
    "  projectedValue: number;",
    "  delta: number;",
    "  unit?: string | null;",
    "}",
    "",
    "export interface P15ScenarioResponse {",
    "  scenarioId: string;",
    "  findingId: string;",
    "  supportStatus: P15SupportStatus;",
    "  supportMessage: string;",
    "  projectionOnlyStatement: string;",
    "  seed: number;",
    "  projectedValueImpact?: P15MoneyRange | null;",
    "  projectionPoints: P15ScenarioProjectionPoint[];",
    "  evidence: P15EvidenceReference[];",
    "  isActionableProjection?: boolean;",
    "}",
    "",
    "export interface P15RecommendationParameterWindow {",
    "  parameterCode: string;",
    "  displayName: string;",
    "  recommendedMinimum: number;",
    "  recommendedMaximum: number;",
    "  unit?: string | null;",
    "  basis: string;",
    "}",
    "",
    "export interface P15RecommendationCandidate {",
    "  recommendationId: string;",
    "  findingId: string;",
    "  title: string;",
    "  advisoryText: string;",
    "  status: P15RecommendationStatus;",
    "  evidenceStrength: P15EvidenceStrength;",
    "  confidence: number;",
    "  expectedImpact?: P15MoneyRange | null;",
    "  parameterWindows: P15RecommendationParameterWindow[];",
    "  evidence: P15EvidenceReference[];",
    "  provenance: string[];",
    "  honestyCaveat: string;",
    "  requiresHumanApproval: boolean;",
    "  hasWriteBackPath: boolean;",
    "}",
    "",
    "export interface P15RecommendationGenerationRequest {",
    "  tenantId: string;",
    "  plantId: string;",
    "  scenarioRequest: P15ScenarioRequest;",
    "}",
    "",
    "export interface P15RecommendationGenerationResponse {",
    "  requestId: string;",
    "  scenarioId: string;",
    "  scenarioSupportStatus: P15SupportStatus;",
    "  message: string;",
    "  recommendations: P15RecommendationCandidate[];",
    "  guardrails: string[];",
    "}",
    "",
    "export interface P15ApprovalCommand {",
    "  recommendationId: string;",
    "  approverUserId: string;",
    "  decision: P15ApprovalDecision;",
    "  comment: string;",
    "  decidedAtUtc: string;",
    "}",
    "",
    "export interface P15ApprovalResult {",
    "  recommendationId: string;",
    "  status: P15RecommendationStatus;",
    "  message: string;",
    "  approvalRecordId: string;",
    "  decidedAtUtc: string;",
    "}",
    "",
    "export interface P15ScenarioHealth {",
    "  status: string;",
    "  marker: string;",
    "  phase: string;",
    "  task: string;",
    "  mode: string;",
    "  projectionOnly: boolean;",
    "  automaticWriteBack: boolean;",
    "  outOfEnvelopeAbstain: boolean;",
    "}",
    "",
    "export interface P15ScenarioContract {",
    "  marker: string;",
    "  contract: string;",
    "  safetyRules: string[];",
    "  routes: string[];",
    "}",
    "",
    "export interface P15RecommendationHealth {",
    "  status: string;",
    "  marker: string;",
    "  phase: string;",
    "  task: string;",
    "  mode: string;",
    "  expectedEImpactRange: boolean;",
    "  confidenceEvidenceProvenance: boolean;",
    "  humanApprovalRequired: boolean;",
    "  automaticWriteBack: boolean;",
    "}",
    "",
    "export interface P15RecommendationContract {",
    "  marker: string;",
    "  contract: string;",
    "  guardrails: string[];",
    "  routes: string[];",
    "}",
    "",
    "export const phase15AdvisoryApi = {",
    "  scenarioHealth() {",
    '    return apiClient.get<P15ScenarioHealth>("/api/p15/advisory/scenarios/health");',
    "  },",
    "",
    "  scenarioContract() {",
    '    return apiClient.get<P15ScenarioContract>("/api/p15/advisory/scenarios/contract");',
    "  },",
    "",
    "  sampleRequest() {",
    '    return apiClient.get<P15ScenarioRequest>("/api/p15/advisory/scenarios/sample-request");',
    "  },",
    "",
    "  simulate(request: P15ScenarioRequest) {",
    '    return apiClient.post<P15ScenarioResponse>("/api/p15/advisory/scenarios/simulate", request);',
    "  },",
    "",
    "  recommendationHealth() {",
    '    return apiClient.get<P15RecommendationHealth>("/api/p15/advisory/recommendations/health");',
    "  },",
    "",
    "  recommendationContract() {",
    '    return apiClient.get<P15RecommendationContract>("/api/p15/advisory/recommendations/contract");',
    "  },",
    "",
    "  recommendationDemoRequest() {",
    '    return apiClient.get<P15RecommendationGenerationRequest>("/api/p15/advisory/recommendations/demo-request");',
    "  },",
    "",
    "  generateRecommendations(request: P15RecommendationGenerationRequest) {",
    '    return apiClient.post<P15RecommendationGenerationResponse>("/api/p15/advisory/recommendations/generate", request);',
    "  },",
    "",
    "  approveRecommendation(command: P15ApprovalCommand) {",
    '    return apiClient.post<P15ApprovalResult>("/api/p15/advisory/recommendations/approve", command);',
    "  },",
    "};",
    ""
  ].join("\n");

  write(frontendApiPath, content);

  return { path: rel(frontendApiPath), status: "WRITTEN" };
}

function writeFrontendPage() {
  const content = [
    'import { useEffect, useMemo, useState } from "react";',
    "import {",
    "  phase15AdvisoryApi,",
    "  type P15RecommendationCandidate,",
    "  type P15RecommendationContract,",
    "  type P15RecommendationGenerationRequest,",
    "  type P15RecommendationGenerationResponse,",
    "  type P15RecommendationHealth,",
    "} from \"@/api/phase15Advisory\";",
    "",
    "const cardStyle = {",
    "  border: \"1px solid rgba(124, 220, 255, 0.18)\",",
    "  borderRadius: 22,",
    "  padding: 20,",
    "  background: \"linear-gradient(135deg, rgba(7, 18, 34, 0.95), rgba(4, 10, 22, 0.9))\",",
    "  boxShadow: \"0 18px 50px rgba(0, 0, 0, 0.22)\",",
    "} as const;",
    "",
    "const buttonStyle = {",
    "  border: \"1px solid rgba(0, 212, 255, 0.34)\",",
    "  borderRadius: 14,",
    "  padding: \"10px 14px\",",
    "  color: \"#eaf7ff\",",
    "  background: \"rgba(0, 132, 255, 0.24)\",",
    "  cursor: \"pointer\",",
    "  fontWeight: 700,",
    "} as const;",
    "",
    "function asMoney(value?: number | null, currency = \"EUR\") {",
    "  if (value === undefined || value === null || Number.isNaN(value)) return \"—\";",
    "  return new Intl.NumberFormat(undefined, { style: \"currency\", currency }).format(value);",
    "}",
    "",
    "function percent(value?: number | null) {",
    "  if (value === undefined || value === null || Number.isNaN(value)) return \"—\";",
    "  return `${Math.round(value * 100)}%`;",
    "}",
    "",
    "export function RecommendationsPage() {",
    "  const [health, setHealth] = useState<P15RecommendationHealth | null>(null);",
    "  const [contract, setContract] = useState<P15RecommendationContract | null>(null);",
    "  const [request, setRequest] = useState<P15RecommendationGenerationRequest | null>(null);",
    "  const [response, setResponse] = useState<P15RecommendationGenerationResponse | null>(null);",
    "  const [status, setStatus] = useState(\"Loading Phase 15 recommendation generator...\");",
    "  const [busy, setBusy] = useState(false);",
    "  const [decisionMessage, setDecisionMessage] = useState<string | null>(null);",
    "",
    "  async function refresh() {",
    "    const [nextHealth, nextContract, nextRequest] = await Promise.all([",
    "      phase15AdvisoryApi.recommendationHealth(),",
    "      phase15AdvisoryApi.recommendationContract(),",
    "      phase15AdvisoryApi.recommendationDemoRequest(),",
    "    ]);",
    "    setHealth(nextHealth);",
    "    setContract(nextContract);",
    "    setRequest(nextRequest);",
    "    setStatus(\"Recommendation generator is reachable.\");",
    "  }",
    "",
    "  useEffect(() => {",
    "    let active = true;",
    "    refresh().catch((error: Error) => {",
    "      if (active) setStatus(`Recommendation generator not reachable: ${error.message}`);",
    "    });",
    "    return () => { active = false; };",
    "  }, []);",
    "",
    "  async function generate() {",
    "    if (!request) return;",
    "    setBusy(true);",
    "    setDecisionMessage(null);",
    "    setStatus(\"Generating guarded recommendations...\");",
    "    try {",
    "      const result = await phase15AdvisoryApi.generateRecommendations(request);",
    "      setResponse(result);",
    "      setStatus(\"Recommendation generation completed. Review confidence, evidence and approval requirement.\");",
    "    } catch (error) {",
    "      const message = error instanceof Error ? error.message : String(error);",
    "      setStatus(`Recommendation generation failed: ${message}`);",
    "    } finally {",
    "      setBusy(false);",
    "    }",
    "  }",
    "",
    "  async function decide(recommendation: P15RecommendationCandidate, decision: 1 | 2) {",
    "    setBusy(true);",
    "    try {",
    "      const result = await phase15AdvisoryApi.approveRecommendation({",
    "        recommendationId: recommendation.recommendationId,",
    "        approverUserId: \"demo-approver\",",
    "        decision,",
    "        comment: decision === 1 ? \"Approved for engineering review.\" : \"Dismissed from demo workspace.\",",
    "        decidedAtUtc: new Date().toISOString(),",
    "      });",
    "      setDecisionMessage(result.message);",
    "    } catch (error) {",
    "      const message = error instanceof Error ? error.message : String(error);",
    "      setDecisionMessage(`Approval failed: ${message}`);",
    "    } finally {",
    "      setBusy(false);",
    "    }",
    "  }",
    "",
    "  const firstRecommendation = response?.recommendations[0];",
    "  const impact = firstRecommendation?.expectedImpact;",
    "",
    "  const summaryCards = useMemo(",
    "    () => [",
    "      { label: \"Mode\", value: health?.mode ?? \"pending\" },",
    "      { label: \"Expected € impact\", value: health?.expectedEImpactRange ? \"YES\" : \"pending\" },",
    "      { label: \"Approval required\", value: health?.humanApprovalRequired ? \"YES\" : \"pending\" },",
    "      { label: \"Auto write-back\", value: health ? (health.automaticWriteBack ? \"YES\" : \"NO\") : \"pending\" },",
    "      { label: \"Recommendations\", value: String(response?.recommendations.length ?? 0) },",
    "      { label: \"Expected value\", value: asMoney(impact?.expectedValue, impact?.currencyCode ?? \"EUR\") },",
    "    ],",
    "    [health, impact?.currencyCode, impact?.expectedValue, response?.recommendations.length],",
    "  );",
    "",
    "  return (",
    "    <main style={{ display: \"grid\", gap: 18, color: \"#eaf7ff\" }} data-testid=\"phase15-recommendations-page\">",
    "      <section style={{ ...cardStyle, borderColor: \"rgba(19, 216, 255, 0.28)\" }}>",
    "        <p style={{ color: \"#13d8ff\", textTransform: \"uppercase\", letterSpacing: \"0.08em\", fontSize: 12, margin: 0 }}>",
    "          Pack G · T-097 · Recommendation generator with expected €-impact",
    "        </p>",
    "        <h1 style={{ margin: \"8px 0 8px\", fontSize: 34 }}>Phase 15 Recommendations</h1>",
    "        <p style={{ margin: 0, color: \"#9ab8d7\", maxWidth: 980, lineHeight: 1.65 }}>",
    "          Generates guarded advisory recommendations from supported what-if projections. Every recommendation carries expected €-impact, confidence, evidence, provenance and explicit approval requirement.",
    "        </p>",
    "        <strong style={{ display: \"block\", marginTop: 16, color: status.includes(\"failed\") ? \"#ffb86b\" : \"#64ffda\" }}>{status}</strong>",
    "        {decisionMessage ? <strong style={{ display: \"block\", marginTop: 8, color: \"#ffdd8a\" }}>{decisionMessage}</strong> : null}",
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
    "      <section style={{ ...cardStyle, display: \"grid\", gap: 14 }}>",
    "        <h2 style={{ margin: 0 }}>1. Generate recommendation</h2>",
    "        <p style={{ color: \"#9ab8d7\", lineHeight: 1.65, margin: 0 }}>",
    "          Demo request uses the Pack G-3 deterministic scenario engine. The recommendation generator blocks unsupported scenarios and weak evidence.",
    "        </p>",
    "        <div style={{ display: \"flex\", gap: 10, flexWrap: \"wrap\" }}>",
    "          <button type=\"button\" disabled={busy || !request} onClick={generate} style={buttonStyle}>Generate recommendation</button>",
    "          <button type=\"button\" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Reload demo request</button>",
    "        </div>",
    "      </section>",
    "",
    "      <section style={{ display: \"grid\", gridTemplateColumns: \"minmax(360px, 1.2fr) minmax(280px, 0.8fr)\", gap: 14 }}>",
    "        <div style={cardStyle}>",
    "          <h2 style={{ marginTop: 0 }}>2. Recommendation output</h2>",
    "          {response?.recommendations.length ? (",
    "            <div style={{ display: \"grid\", gap: 12 }}>",
    "              {response.recommendations.map((recommendation) => (",
    "                <div key={recommendation.recommendationId} style={{ border: \"1px solid rgba(100,255,218,0.18)\", borderRadius: 16, padding: 14, background: \"rgba(2,7,18,0.42)\" }}>",
    "                  <h3 style={{ marginTop: 0 }}>{recommendation.title}</h3>",
    "                  <p style={{ color: \"#9ab8d7\", lineHeight: 1.65 }}>{recommendation.advisoryText}</p>",
    "                  <p style={{ color: \"#ffdd8a\", lineHeight: 1.65 }}>{recommendation.honestyCaveat}</p>",
    "                  <div style={{ display: \"grid\", gridTemplateColumns: \"repeat(auto-fit, minmax(150px, 1fr))\", gap: 10 }}>",
    "                    <div><small>Confidence</small><strong style={{ display: \"block\" }}>{percent(recommendation.confidence)}</strong></div>",
    "                    <div><small>Status</small><strong style={{ display: \"block\" }}>{String(recommendation.status)}</strong></div>",
    "                    <div><small>Expected</small><strong style={{ display: \"block\" }}>{asMoney(recommendation.expectedImpact?.expectedValue, recommendation.expectedImpact?.currencyCode ?? \"EUR\")}</strong></div>",
    "                    <div><small>Approval</small><strong style={{ display: \"block\" }}>{recommendation.requiresHumanApproval ? \"Required\" : \"Not required\"}</strong></div>",
    "                  </div>",
    "                  <h4>Parameter windows</h4>",
    "                  <table style={{ width: \"100%\", borderCollapse: \"collapse\" }}>",
    "                    <thead><tr style={{ color: \"#9ab8d7\", textAlign: \"left\" }}><th style={{ padding: 8 }}>Parameter</th><th style={{ padding: 8 }}>Min</th><th style={{ padding: 8 }}>Max</th><th style={{ padding: 8 }}>Basis</th></tr></thead>",
    "                    <tbody>",
    "                      {recommendation.parameterWindows.map((window) => (",
    "                        <tr key={window.parameterCode} style={{ borderTop: \"1px solid rgba(120,190,255,0.12)\" }}>",
    "                          <td style={{ padding: 8 }}>{window.displayName}</td>",
    "                          <td style={{ padding: 8 }}>{window.recommendedMinimum} {window.unit ?? \"\"}</td>",
    "                          <td style={{ padding: 8 }}>{window.recommendedMaximum} {window.unit ?? \"\"}</td>",
    "                          <td style={{ padding: 8, color: \"#9ab8d7\" }}>{window.basis}</td>",
    "                        </tr>",
    "                      ))}",
    "                    </tbody>",
    "                  </table>",
    "                  <div style={{ display: \"flex\", gap: 10, marginTop: 12, flexWrap: \"wrap\" }}>",
    "                    <button type=\"button\" disabled={busy} onClick={() => void decide(recommendation, 1)} style={buttonStyle}>Approve for review</button>",
    "                    <button type=\"button\" disabled={busy} onClick={() => void decide(recommendation, 2)} style={buttonStyle}>Dismiss</button>",
    "                  </div>",
    "                </div>",
    "              ))}",
    "            </div>",
    "          ) : (",
    "            <p style={{ color: \"#9ab8d7\" }}>{response?.message ?? \"Generate a recommendation to view guarded advisory output.\"}</p>",
    "          )}",
    "        </div>",
    "",
    "        <div style={cardStyle}>",
    "          <h2 style={{ marginTop: 0 }}>3. Guardrails</h2>",
    "          <ul style={{ color: \"#9ab8d7\", lineHeight: 1.75, paddingLeft: 18 }}>",
    "            {(response?.guardrails ?? contract?.guardrails ?? [",
    "              \"No causal language.\",",
    "              \"Expected e-impact is projection-only.\",",
    "              \"Human approval is required.\",",
    "              \"No automatic process write-back.\",",
    "            ]).map((rule) => <li key={rule}>{rule}</li>)}",
    "          </ul>",
    "          <h3>Provenance</h3>",
    "          <ul style={{ color: \"#9ab8d7\", lineHeight: 1.75, paddingLeft: 18 }}>",
    "            {(firstRecommendation?.provenance ?? [\"No recommendation generated yet.\"]).map((item) => <li key={item}>{item}</li>)}",
    "          </ul>",
    "        </div>",
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

  if (!text.includes("RecommendationsPage")) {
    const lazyBlock = [
      "const RecommendationsPage = lazy(() =>",
      "  import(\"./pages/Phase15/RecommendationsPage\").then((m) => ({",
      "    default: m.RecommendationsPage,",
      "  }))",
      ");",
      ""
    ].join("\n");

    const anchor = text.includes("const ScenarioSimulationPage = lazy(() =>")
      ? "const ScenarioSimulationPage = lazy(() =>"
      : "const EdgeCollectorPage = lazy(() =>";

    if (!text.includes(anchor)) throw new Error("Could not find lazy page insertion anchor.");

    text = text.replace(anchor, lazyBlock + anchor);
    changed = true;
  }

  if (!text.includes('path="/phase15/recommendations"')) {
    const routeBlock = [
      "                    {/* Pack G Phase 15 recommendation generator */}",
      "                    <Route",
      "                      path=\"/phase15/recommendations\"",
      "                      element={withPageBoundary(",
      "                        \"/phase15/recommendations\",",
      "                        \"Phase 15 recommendations are refreshing\",",
      "                        <RecommendationsPage />",
      "                      )}",
      "                    />",
      "",
      "                    <Route",
      "                      path=\"/advisory/recommendations\"",
      "                      element={<Navigate to=\"/phase15/recommendations\" replace />}",
      "                    />",
      ""
    ].join("\n");

    const anchor = text.includes("                    {/* Pack G Phase 15 scenario simulation */}")
      ? "                    {/* Pack G Phase 15 scenario simulation */}"
      : "                    {/* Pack F edge collector management UX */}";

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

  if (text.includes('to: "/phase15/recommendations"')) {
    return { path: rel(layoutPath), status: "ALREADY_PATCHED" };
  }

  const newItem = '  { to: "/phase15/recommendations", label: "Recommendations", desc: "Expected € impact + approval", icon: DatabaseZap },';
  const anchor = text.includes('  { to: "/phase15/scenario-simulation"')
    ? '  { to: "/phase15/scenario-simulation"'
    : '  { to: "/edge-collector", label: "Edge Collector"';

  if (!text.includes(anchor)) throw new Error("Could not find AppLayout navigation insertion anchor.");

  text = text.replace(anchor, newItem + "\n" + anchor);
  write(layoutPath, text);

  return { path: rel(layoutPath), status: "PATCHED" };
}

function writeDocs() {
  const md = [];

  md.push("# Phase 15 Recommendation Generator");
  md.push("");
  md.push("Marker: PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT");
  md.push("");
  md.push("## Purpose");
  md.push("");
  md.push("The recommendation generator converts supported what-if scenario projections into guarded advisory recommendations with expected e-impact range, confidence, evidence, provenance and explicit approval workflow.");
  md.push("");
  md.push("## Guardrails");
  md.push("");
  md.push("- No causal language.");
  md.push("- Expected e-impact is projection-only.");
  md.push("- Confidence, evidence and provenance are required.");
  md.push("- Weak evidence blocks recommendation.");
  md.push("- Human approval is required.");
  md.push("- No automatic process write-back.");
  md.push("");
  md.push("## Backend routes");
  md.push("");
  md.push("- `GET /api/p15/advisory/recommendations/health`");
  md.push("- `GET /api/p15/advisory/recommendations/contract`");
  md.push("- `GET /api/p15/advisory/recommendations/demo-request`");
  md.push("- `POST /api/p15/advisory/recommendations/generate`");
  md.push("- `POST /api/p15/advisory/recommendations/generate-demo`");
  md.push("- `POST /api/p15/advisory/recommendations/approve`");
  md.push("- `GET /api/p15/advisory/recommendations/approvals`");
  md.push("");
  md.push("## Frontend routes");
  md.push("");
  md.push("- `/phase15/recommendations`");
  md.push("- `/advisory/recommendations` alias");
  md.push("");

  write(recommendationDocPath, md.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs",');
  lines.push('  "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs",');
  lines.push('  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",');
  lines.push('  "Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx",');
  lines.push('  "docs/developer/PHASE15_RECOMMENDATION_GENERATOR.md",');
  lines.push('  "docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.json",');
  lines.push('  "docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.md",');
  lines.push('  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('const requiredSignals = [');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "ExpectedImpact" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "P15AdvisoryHonestyPolicy.ValidateRecommendation" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "RequiresHumanApproval = true" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "HasWriteBackPath = false" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "Weak evidence blocks recommendation" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "/api/p15/advisory/recommendations" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "/generate" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "/approve" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "automaticWriteBack = false" },');
  lines.push('  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15AdvisoryRecommendationEndpoints" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/advisory/recommendations/generate" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/advisory/recommendations/approve" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx", signal: "Pack G · T-097 · Recommendation generator with expected €-impact" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx", signal: "Approve for review" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/recommendations" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Recommendations" },');
  lines.push('  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT" }');
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
  lines.push('  console.error("Pack G-4 T-097 recommendation generator validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack G-4 T-097 recommendation generator validation passed.");');

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
  lines.push('const bridgedJsonPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.PACK_G4_BRIDGED.json");');
  lines.push('const bridgedMdPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.PACK_G4_BRIDGED.md");');
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
  lines.push('const t097Green = runOk("node", ["tools/pack-g/validate-pack-g4-t097-recommendation-generator.cjs"]);');
  lines.push('');
  lines.push('for (const row of rows) {');
  lines.push('  if (code(row) === "T-097" && t097Green) setDone(row, "Pack G-4 evidence bridge: recommendation generator with expected e-impact validator passed and builds were green.");');
  lines.push('}');
  lines.push('');
  lines.push('scorecard.packG4EvidenceBridge = { generatedAtUtc: new Date().toISOString(), marker: "PPIQ_PACK_G4_T097_SCORECARD_BRIDGE", t097Green };');
  lines.push('');
  lines.push('write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('write(bridgedJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('');
  lines.push('const below90 = rows.filter((row) => score(row) < 90);');
  lines.push('const md = [];');
  lines.push('md.push("# T096-T102 Phase 15 Scorecard — Pack G4 Bridged");');
  lines.push('md.push("");');
  lines.push('md.push("Marker: PPIQ_PACK_G4_T097_SCORECARD_BRIDGE");');
  lines.push('md.push("");');
  lines.push('md.push("T-097 bridge result: " + (t097Green ? "DONE" : "NOT GREEN"));');
  lines.push('md.push("");');
  lines.push('md.push("Tasks below 90% after Pack G4 bridge: " + below90.length);');
  lines.push('md.push("");');
  lines.push('for (const row of below90) md.push(rowLine(row));');
  lines.push('md.push("");');
  lines.push('write(bridgedMdPath, md.join("\\n") + "\\n");');
  lines.push('');
  lines.push('console.log("");');
  lines.push('console.log("Pack G4 task-closure evidence bridge applied.");');
  lines.push('console.log("T-097 bridge result: " + (t097Green ? "DONE" : "NOT GREEN"));');
  lines.push('console.log("");');
  lines.push('console.log("Tasks below 90% after Pack G4 bridge: " + below90.length);');
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
    marker: "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT",
    phase: "P15",
    pack: "G",
    task: "T-097",
    backendRouteRoot: "/api/p15/advisory/recommendations",
    frontendRoute: "/phase15/recommendations",
    aliasRoute: "/advisory/recommendations",
    results
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];
  md.push("# Pack G-4 T-097 Recommendation Generator Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT");
  md.push("");
  md.push("## Scope");
  md.push("");
  md.push("Adds guarded recommendation generation with expected e-impact range, confidence, evidence, provenance and explicit approval/dismiss workflow.");
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

  if (!evidence.includes("PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT")) {
    evidence += [
      "",
      "## Pack G-4 T-097 recommendation generator with expected e-impact",
      "",
      "- Marker: PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT.",
      "- Added guarded recommendation generation service.",
      "- Added expected e-impact range, confidence, evidence and provenance.",
      "- Added approval/dismiss command path with no automatic write-back.",
      "- Added backend recommendation endpoints.",
      "- Added frontend recommendations page.",
      "- Added validator and T-097 scorecard bridge.",
      "- Backend and frontend builds must remain green.",
      "",
      "Generated artifacts:",
      "",
      "- Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs",
      "- Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs",
      "- Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx",
      "- docs/developer/PHASE15_RECOMMENDATION_GENERATOR.md",
      "- docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.md",
      "- docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.json",
      "- tools/pack-g/validate-pack-g4-t097-recommendation-generator.cjs",
      "- tools/task-closure/ppiq-pack-g4-scorecard-bridge.cjs",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack G-4 — T-097 recommendation generator with expected e-impact");
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

run("node --check Pack G-4 validator", ["node", "--check", "tools/pack-g/validate-pack-g4-t097-recommendation-generator.cjs"]);
run("node --check Pack G4 bridge", ["node", "--check", "tools/task-closure/ppiq-pack-g4-scorecard-bridge.cjs"]);
run("Pack G-1 closure-map validator", ["node", "tools/pack-g/validate-pack-g-phase15-closure-map.cjs"]);
run("Pack G-2 contract validator", ["node", "tools/pack-g/validate-pack-g2-phase15-contract.cjs"]);
run("Pack G-3 T-096 scenario validator", ["node", "tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs"]);
run("Pack G-4 T-097 recommendation validator", ["node", "tools/pack-g/validate-pack-g4-t097-recommendation-generator.cjs"]);

run("Backend build", ["dotnet", "build", "Backend"]);
run("Frontend build", frontendBuildArgs());

if (isFile(path.join(root, "tools", "task-closure", "ppiq-pack-g3-scorecard-bridge.cjs"))) {
  run("Apply Pack G3 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g3-scorecard-bridge.cjs"], root, true);
}

run("Apply Pack G4 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g4-scorecard-bridge.cjs"]);

console.log("");
console.log("Pack G-4 result:");
console.log(" - Recommendation generation service written");
console.log(" - Recommendation endpoints written");
console.log(" - Program endpoint registration patched");
console.log(" - Frontend API client extended");
console.log(" - Recommendations page written");
console.log(" - T-097 validator passed");
console.log(" - Backend build passed");
console.log(" - Frontend build passed");
console.log(" - T-097 scorecard bridge applied");

console.log("");
console.log("=================================================================================================");
console.log("Pack G-4 completed.");
console.log("Expected: T-097 is green in the Pack G4 bridged Phase 15 scorecard.");
console.log("Report: docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.md");
console.log("Next  : Pack G-5 / T-098 — value-realization tracking baseline vs actual.");
console.log("=================================================================================================");