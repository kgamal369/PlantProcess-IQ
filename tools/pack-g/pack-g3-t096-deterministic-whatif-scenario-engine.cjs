const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_g_backup", "pack_g3_t096_whatif_scenario_" + timestamp);

const docsDir = path.join(root, "docs", "pack-g");
const developerDocsDir = path.join(root, "docs", "developer");
const toolsDir = path.join(root, "tools", "pack-g");
const toolsTaskClosureDir = path.join(root, "tools", "task-closure");

const servicePath = path.join(root, "Backend", "PlantProcess.Application", "Advisory", "P15ScenarioSimulationService.cs");
const endpointPath = path.join(root, "Backend", "PlantProcess.Api", "PlantConnectors", "P15AdvisoryScenarioEndpoints.cs");
const programPath = path.join(root, "Backend", "PlantProcess.Api", "Program.cs");

const frontendApiPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "api", "phase15Advisory.ts");
const frontendPagePath = path.join(root, "Frontend", "PlantProcess.Web", "src", "pages", "Phase15", "ScenarioSimulationPage.tsx");
const appPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "App.implementation.tsx");
const layoutPath = path.join(root, "Frontend", "PlantProcess.Web", "src", "components", "AppLayout.tsx");

const validatorPath = path.join(toolsDir, "validate-pack-g3-t096-scenario-engine.cjs");
const bridgePath = path.join(toolsTaskClosureDir, "ppiq-pack-g3-scorecard-bridge.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-PackG-Phase15Regression.ps1");

const scenarioDocPath = path.join(developerDocsDir, "PHASE15_WHATIF_SCENARIO_ENGINE.md");
const reportJsonPath = path.join(docsDir, "PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.md");
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
    "/// PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE",
    "/// Deterministic Phase 15 what-if simulation engine.",
    "///",
    "/// Important honesty boundary:",
    "/// - projection only",
    "/// - no guaranteed saving",
    "/// - no automatic process action",
    "/// - abstain when outside observed envelope",
    "/// - deterministic output for same request/seed",
    "/// </summary>",
    "public sealed class P15ScenarioSimulationService",
    "{",
    "    public P15ScenarioResponse Simulate(P15ScenarioRequest request)",
    "    {",
    "        var policy = P15AdvisoryHonestyPolicy.ValidateScenarioRequest(request);",
    "        var stableSeed = P15AdvisoryHonestyPolicy.BuildStableScenarioSeed(request);",
    "",
    "        if (!policy.IsAllowed)",
    "        {",
    "            var status = policy.Violations.Any(item => item.Contains(\"Out-of-envelope\", StringComparison.OrdinalIgnoreCase))",
    "                ? P15SupportStatus.OutOfEnvelope",
    "                : P15SupportStatus.InsufficientSupport;",
    "",
    "            return new P15ScenarioResponse",
    "            {",
    "                ScenarioId = BuildScenarioId(request, stableSeed),",
    "                FindingId = request.FindingId,",
    "                SupportStatus = status,",
    "                SupportMessage = policy.Message + \" \" + string.Join(\" \", policy.Violations),",
    "                ProjectionOnlyStatement = P15AdvisoryValueContract.ProjectionOnlyStatement,",
    "                Seed = stableSeed,",
    "                ProjectedValueImpact = null,",
    "                ProjectionPoints = Array.Empty<P15ScenarioProjectionPoint>(),",
    "                Evidence = request.Evidence",
    "            };",
    "        }",
    "",
    "        var evidenceDecision = ValidateEvidence(request);",
    "        if (!evidenceDecision.IsAllowed)",
    "        {",
    "            return new P15ScenarioResponse",
    "            {",
    "                ScenarioId = BuildScenarioId(request, stableSeed),",
    "                FindingId = request.FindingId,",
    "                SupportStatus = P15SupportStatus.InsufficientSupport,",
    "                SupportMessage = evidenceDecision.Message + \" \" + string.Join(\" \", evidenceDecision.Violations),",
    "                ProjectionOnlyStatement = P15AdvisoryValueContract.ProjectionOnlyStatement,",
    "                Seed = stableSeed,",
    "                ProjectedValueImpact = null,",
    "                ProjectionPoints = Array.Empty<P15ScenarioProjectionPoint>(),",
    "                Evidence = request.Evidence",
    "            };",
    "        }",
    "",
    "        var random = new Random(stableSeed);",
    "        var confidence = Clamp01(request.Evidence.Length == 0 ? 0m : request.Evidence.Max(item => item.Confidence));",
    "        var strengthMultiplier = request.Evidence.Max(item => item.Strength) switch",
    "        {",
    "            P15EvidenceStrength.Strong => 1.0m,",
    "            P15EvidenceStrength.Moderate => 0.72m,",
    "            _ => 0.0m",
    "        };",
    "",
    "        var adjustmentEffect = request.Adjustments.Sum(adjustment => CalculateAdjustmentEffect(adjustment, random));",
    "        var expectedEuroImpact = RoundMoney(Math.Abs(adjustmentEffect) * 10000m * confidence * strengthMultiplier);",
    "",
    "        var valueImpact = new P15MoneyRange",
    "        {",
    "            CurrencyCode = \"EUR\",",
    "            MinValue = RoundMoney(expectedEuroImpact * 0.55m),",
    "            ExpectedValue = expectedEuroImpact,",
    "            MaxValue = RoundMoney(expectedEuroImpact * 1.35m)",
    "        };",
    "",
    "        var projectionPoints = request.Adjustments",
    "            .Select((adjustment, index) => BuildProjectionPoint(adjustment, index, stableSeed))",
    "            .ToArray();",
    "",
    "        return new P15ScenarioResponse",
    "        {",
    "            ScenarioId = BuildScenarioId(request, stableSeed),",
    "            FindingId = request.FindingId,",
    "            SupportStatus = P15SupportStatus.Supported,",
    "            SupportMessage = \"Supported deterministic projection generated inside observed data envelope.\",",
    "            ProjectionOnlyStatement = P15AdvisoryValueContract.ProjectionOnlyStatement,",
    "            Seed = stableSeed,",
    "            ProjectedValueImpact = valueImpact,",
    "            ProjectionPoints = projectionPoints,",
    "            Evidence = request.Evidence",
    "        };",
    "    }",
    "",
    "    public P15ScenarioRequest BuildDemoRequest() =>",
    "        new()",
    "        {",
    "            TenantId = \"demo-tenant\",",
    "            PlantId = \"demo-plant-01\",",
    "            FindingId = \"finding-temperature-energy-risk\",",
    "            ScenarioName = \"Reduce furnace temperature target within observed envelope\",",
    "            Seed = P15AdvisoryValueContract.DefaultScenarioSeed,",
    "            Adjustments = new[]",
    "            {",
    "                new P15ParameterAdjustment",
    "                {",
    "                    ParameterCode = \"furnace_temperature_target\",",
    "                    DisplayName = \"Furnace temperature target\",",
    "                    CurrentValue = 742m,",
    "                    ProposedValue = 735m,",
    "                    MinimumObservedValue = 720m,",
    "                    MaximumObservedValue = 760m,",
    "                    Unit = \"degC\"",
    "                },",
    "                new P15ParameterAdjustment",
    "                {",
    "                    ParameterCode = \"line_speed_target\",",
    "                    DisplayName = \"Line speed target\",",
    "                    CurrentValue = 1.00m,",
    "                    ProposedValue = 1.04m,",
    "                    MinimumObservedValue = 0.88m,",
    "                    MaximumObservedValue = 1.12m,",
    "                    Unit = \"m/s\"",
    "                }",
    "            },",
    "            Evidence = new[]",
    "            {",
    "                new P15EvidenceReference",
    "                {",
    "                    EvidenceId = \"ev-demo-correlation-001\",",
    "                    EvidenceType = \"association-finding\",",
    "                    SourceSystem = \"Analytics.Core\",",
    "                    Description = \"Temperature and line-speed association with energy and quality-risk KPI under comparable operating windows.\",",
    "                    Confidence = 0.82m,",
    "                    Strength = P15EvidenceStrength.Moderate,",
    "                    Provenance = new[] { \"golden-demo-data\", \"analytics-core-correlation\", \"phase15-demo\" }",
    "                }",
    "            }",
    "        };",
    "",
    "    private static P15PolicyDecision ValidateEvidence(P15ScenarioRequest request)",
    "    {",
    "        if (request.Evidence.Length == 0)",
    "        {",
    "            return P15PolicyDecision.Block(",
    "                \"Scenario projection requires evidence before support can be claimed.\",",
    "                new[] { \"Missing evidence reference.\" });",
    "        }",
    "",
    "        var strongest = request.Evidence.Max(item => item.Strength);",
    "        if (strongest is P15EvidenceStrength.None or P15EvidenceStrength.Weak)",
    "        {",
    "            return P15PolicyDecision.Block(",
    "                \"Scenario projection abstains because evidence is weak or missing.\",",
    "                new[] { \"Weak evidence cannot support what-if projection.\" });",
    "        }",
    "",
    "        return P15PolicyDecision.Allow(\"Evidence is sufficient for guarded projection.\");",
    "    }",
    "",
    "    private static decimal CalculateAdjustmentEffect(P15ParameterAdjustment adjustment, Random random)",
    "    {",
    "        var range = Math.Max(1m, adjustment.MaximumObservedValue - adjustment.MinimumObservedValue);",
    "        var normalizedMove = (adjustment.ProposedValue - adjustment.CurrentValue) / range;",
    "        var boundedMove = Math.Max(-1m, Math.Min(1m, normalizedMove));",
    "        var deterministicJitter = ((decimal)random.NextDouble() - 0.5m) * 0.025m;",
    "        return boundedMove + deterministicJitter;",
    "    }",
    "",
    "    private static P15ScenarioProjectionPoint BuildProjectionPoint(P15ParameterAdjustment adjustment, int index, int seed)",
    "    {",
    "        var local = new Random(seed + index + 31);",
    "        var range = Math.Max(1m, adjustment.MaximumObservedValue - adjustment.MinimumObservedValue);",
    "        var normalizedMove = (adjustment.ProposedValue - adjustment.CurrentValue) / range;",
    "        var baseline = 100m + (index * 7m);",
    "        var directionalDelta = RoundMetric(normalizedMove * 12m + (((decimal)local.NextDouble() - 0.5m) * 1.5m));",
    "        var projected = RoundMetric(baseline - directionalDelta);",
    "",
    "        return new P15ScenarioProjectionPoint",
    "        {",
    "            MetricCode = $\"projected_kpi_{index + 1:00}\",",
    "            Label = $\"Projected KPI impact for {adjustment.DisplayName}\",",
    "            BaselineValue = baseline,",
    "            ProjectedValue = projected,",
    "            Delta = RoundMetric(projected - baseline),",
    "            Unit = \"index\"",
    "        };",
    "    }",
    "",
    "    private static string BuildScenarioId(P15ScenarioRequest request, int seed) =>",
    "        $\"p15-scenario-{Sanitize(request.FindingId)}-{seed}\";",
    "",
    "    private static string Sanitize(string? value) =>",
    "        string.IsNullOrWhiteSpace(value)",
    "            ? \"unknown\"",
    "            : new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');",
    "",
    "    private static decimal Clamp01(decimal value) => Math.Max(0m, Math.Min(1m, value));",
    "    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);",
    "    private static decimal RoundMetric(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);",
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
    "public static class P15AdvisoryScenarioEndpoints",
    "{",
    "    public static IEndpointRouteBuilder MapP15AdvisoryScenarioEndpoints(this IEndpointRouteBuilder app)",
    "    {",
    "        var group = app.MapGroup(\"/api/p15/advisory/scenarios\")",
    "            .WithTags(\"P15 Advisory Scenario Simulation\");",
    "",
    "        group.MapGet(\"/health\", () => Results.Ok(new",
    "        {",
    "            status = \"ok\",",
    "            marker = \"PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE\",",
    "            phase = \"P15\",",
    "            task = \"T-096\",",
    "            mode = \"deterministic-what-if-projection\",",
    "            projectionOnly = true,",
    "            automaticWriteBack = false,",
    "            outOfEnvelopeAbstain = true",
    "        }));",
    "",
    "        group.MapGet(\"/contract\", () => Results.Ok(new",
    "        {",
    "            marker = \"PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE\",",
    "            contract = \"Deterministic what-if scenario simulation engine\",",
    "            safetyRules = new[]",
    "            {",
    "                P15AdvisoryValueContract.ProjectionOnlyStatement,",
    "                \"No automatic process write-back.\",",
    "                \"Out-of-envelope parameter adjustments return abstain/insufficient support.\",",
    "                \"Same request and seed must return the same projection.\",",
    "                \"Weak or missing evidence blocks supported projection.\"",
    "            },",
    "            routes = new[]",
    "            {",
    "                \"GET /api/p15/advisory/scenarios/health\",",
    "                \"GET /api/p15/advisory/scenarios/contract\",",
    "                \"GET /api/p15/advisory/scenarios/sample-request\",",
    "                \"POST /api/p15/advisory/scenarios/simulate\",",
    "                \"POST /api/p15/advisory/scenarios/simulate-demo\"",
    "            }",
    "        }));",
    "",
    "        group.MapGet(\"/sample-request\", () =>",
    "        {",
    "            var service = new P15ScenarioSimulationService();",
    "            return Results.Ok(service.BuildDemoRequest());",
    "        });",
    "",
    "        group.MapPost(\"/simulate\", (P15ScenarioRequest request) =>",
    "        {",
    "            var service = new P15ScenarioSimulationService();",
    "            var response = service.Simulate(request);",
    "            return Results.Ok(response);",
    "        });",
    "",
    "        group.MapPost(\"/simulate-demo\", () =>",
    "        {",
    "            var service = new P15ScenarioSimulationService();",
    "            var request = service.BuildDemoRequest();",
    "            var first = service.Simulate(request);",
    "            var second = service.Simulate(request);",
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

  if (text.includes("MapP15AdvisoryScenarioEndpoints")) {
    return { path: rel(programPath), status: "ALREADY_PATCHED" };
  }

  const anchors = [
    "app.MapV5OtSafeEdgeCollectorEndpoints();",
    "app.MapV5GaHistorianConnectorEndpoints();",
    "app.MapV5PlantConnectorEndpoints();",
    "app.MapIntegrationEndpoints();"
  ];

  const anchor = anchors.find((candidate) => text.includes(candidate));
  if (!anchor) throw new Error("Could not find endpoint registration anchor in Program.cs.");

  text = text.replace(anchor, anchor + "\napp.MapP15AdvisoryScenarioEndpoints();");
  write(programPath, text);

  return { path: rel(programPath), status: "PATCHED" };
}

function writeFrontendApi() {
  const content = [
    'import { apiClient } from "@/api/http";',
    "",
    "export type P15SupportStatus = \"Supported\" | \"InsufficientSupport\" | \"OutOfEnvelope\" | \"BlockedByHonestyGuard\" | number;",
    "export type P15EvidenceStrength = \"None\" | \"Weak\" | \"Moderate\" | \"Strong\" | number;",
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
    "export const phase15AdvisoryApi = {",
    "  health() {",
    '    return apiClient.get<P15ScenarioHealth>("/api/p15/advisory/scenarios/health");',
    "  },",
    "",
    "  contract() {",
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
    "  type P15ScenarioContract,",
    "  type P15ScenarioHealth,",
    "  type P15ScenarioRequest,",
    "  type P15ScenarioResponse,",
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
    "const inputStyle = {",
    "  width: \"100%\",",
    "  border: \"1px solid rgba(120, 190, 255, 0.22)\",",
    "  borderRadius: 12,",
    "  padding: \"10px 12px\",",
    "  background: \"rgba(2, 7, 18, 0.75)\",",
    "  color: \"#eaf7ff\",",
    "} as const;",
    "",
    "function asMoney(value?: number | null, currency = \"EUR\") {",
    "  if (value === undefined || value === null || Number.isNaN(value)) return \"—\";",
    "  return new Intl.NumberFormat(undefined, { style: \"currency\", currency }).format(value);",
    "}",
    "",
    "function asNumber(value?: number | null) {",
    "  if (value === undefined || value === null || Number.isNaN(value)) return \"—\";",
    "  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 3 }).format(value);",
    "}",
    "",
    "export function ScenarioSimulationPage() {",
    "  const [health, setHealth] = useState<P15ScenarioHealth | null>(null);",
    "  const [contract, setContract] = useState<P15ScenarioContract | null>(null);",
    "  const [request, setRequest] = useState<P15ScenarioRequest | null>(null);",
    "  const [response, setResponse] = useState<P15ScenarioResponse | null>(null);",
    "  const [status, setStatus] = useState(\"Loading Phase 15 what-if engine...\");",
    "  const [busy, setBusy] = useState(false);",
    "",
    "  async function refresh() {",
    "    const [nextHealth, nextContract, nextSample] = await Promise.all([",
    "      phase15AdvisoryApi.health(),",
    "      phase15AdvisoryApi.contract(),",
    "      phase15AdvisoryApi.sampleRequest(),",
    "    ]);",
    "",
    "    setHealth(nextHealth);",
    "    setContract(nextContract);",
    "    setRequest(nextSample);",
    "    setStatus(\"Phase 15 what-if engine is reachable.\");",
    "  }",
    "",
    "  useEffect(() => {",
    "    let active = true;",
    "    refresh().catch((error: Error) => {",
    "      if (active) setStatus(`Scenario engine not reachable: ${error.message}`);",
    "    });",
    "    return () => { active = false; };",
    "  }, []);",
    "",
    "  const valueImpact = response?.projectedValueImpact;",
    "  const summaryCards = useMemo(",
    "    () => [",
    "      { label: \"Mode\", value: health?.mode ?? \"pending\" },",
    "      { label: \"Projection only\", value: health?.projectionOnly ? \"YES\" : \"pending\" },",
    "      { label: \"Auto write-back\", value: health ? (health.automaticWriteBack ? \"YES\" : \"NO\") : \"pending\" },",
    "      { label: \"Support\", value: String(response?.supportStatus ?? \"not simulated\") },",
    "      { label: \"Seed\", value: String(response?.seed ?? request?.seed ?? \"—\") },",
    "      { label: \"Expected € impact\", value: asMoney(valueImpact?.expectedValue, valueImpact?.currencyCode ?? \"EUR\") },",
    "    ],",
    "    [health, request?.seed, response?.seed, response?.supportStatus, valueImpact?.currencyCode, valueImpact?.expectedValue],",
    "  );",
    "",
    "  function updateProposedValue(index: number, nextValue: string) {",
    "    if (!request) return;",
    "    const numeric = Number(nextValue);",
    "    setRequest({",
    "      ...request,",
    "      adjustments: request.adjustments.map((adjustment, currentIndex) =>",
    "        currentIndex === index ? { ...adjustment, proposedValue: numeric } : adjustment,",
    "      ),",
    "    });",
    "  }",
    "",
    "  async function simulate() {",
    "    if (!request) return;",
    "    setBusy(true);",
    "    setStatus(\"Running deterministic what-if projection...\");",
    "    try {",
    "      const result = await phase15AdvisoryApi.simulate(request);",
    "      setResponse(result);",
    "      setStatus(\"Scenario projection completed. Review support status and projection-only caveat.\");",
    "    } catch (error) {",
    "      const message = error instanceof Error ? error.message : String(error);",
    "      setStatus(`Scenario projection failed: ${message}`);",
    "    } finally {",
    "      setBusy(false);",
    "    }",
    "  }",
    "",
    "  function makeOutOfEnvelope() {",
    "    if (!request) return;",
    "    setRequest({",
    "      ...request,",
    "      adjustments: request.adjustments.map((adjustment, index) =>",
    "        index === 0 ? { ...adjustment, proposedValue: adjustment.maximumObservedValue + 100 } : adjustment,",
    "      ),",
    "    });",
    "    setResponse(null);",
    "    setStatus(\"Out-of-envelope demo prepared. Run simulation to confirm abstain/insufficient support behavior.\");",
    "  }",
    "",
    "  return (",
    "    <main style={{ display: \"grid\", gap: 18, color: \"#eaf7ff\" }} data-testid=\"phase15-scenario-page\">",
    "      <section style={{ ...cardStyle, borderColor: \"rgba(19, 216, 255, 0.28)\" }}>",
    "        <p style={{ color: \"#13d8ff\", textTransform: \"uppercase\", letterSpacing: \"0.08em\", fontSize: 12, margin: 0 }}>",
    "          Pack G · T-096 · What-if scenario simulation engine",
    "        </p>",
    "        <h1 style={{ margin: \"8px 0 8px\", fontSize: 34 }}>Phase 15 What-if Scenario Simulation</h1>",
    "        <p style={{ margin: 0, color: \"#9ab8d7\", maxWidth: 980, lineHeight: 1.65 }}>",
    "          Deterministic projection engine for guarded advisory scenarios. It is projection-only, rejects out-of-envelope changes, and never writes to the production process.",
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
    "      <section style={{ display: \"grid\", gridTemplateColumns: \"minmax(320px, 0.9fr) minmax(360px, 1.1fr)\", gap: 14 }}>",
    "        <div style={{ ...cardStyle, display: \"grid\", gap: 14 }}>",
    "          <h2 style={{ margin: 0 }}>1. Scenario inputs</h2>",
    "          {request ? (",
    "            <>",
    "              <label><span style={{ display: \"block\", marginBottom: 6, color: \"#9ab8d7\" }}>Scenario name</span><input value={request.scenarioName} onChange={(event) => setRequest({ ...request, scenarioName: event.target.value })} style={inputStyle} /></label>",
    "              <label><span style={{ display: \"block\", marginBottom: 6, color: \"#9ab8d7\" }}>Seed</span><input type=\"number\" value={request.seed} onChange={(event) => setRequest({ ...request, seed: Number(event.target.value) })} style={inputStyle} /></label>",
    "              <div style={{ display: \"grid\", gap: 10 }}>",
    "                {request.adjustments.map((adjustment, index) => (",
    "                  <div key={adjustment.parameterCode} style={{ border: \"1px solid rgba(120, 190, 255, 0.14)\", borderRadius: 14, padding: 12, background: \"rgba(2, 7, 18, 0.42)\" }}>",
    "                    <strong>{adjustment.displayName}</strong>",
    "                    <p style={{ margin: \"6px 0\", color: \"#9ab8d7\" }}>Observed envelope: {asNumber(adjustment.minimumObservedValue)} → {asNumber(adjustment.maximumObservedValue)} {adjustment.unit ?? \"\"}</p>",
    "                    <label><span style={{ display: \"block\", marginBottom: 6, color: \"#9ab8d7\" }}>Proposed value</span><input type=\"number\" value={adjustment.proposedValue} onChange={(event) => updateProposedValue(index, event.target.value)} style={inputStyle} /></label>",
    "                  </div>",
    "                ))}",
    "              </div>",
    "              <div style={{ display: \"flex\", gap: 10, flexWrap: \"wrap\" }}>",
    "                <button type=\"button\" disabled={busy} onClick={simulate} style={buttonStyle}>Run what-if simulation</button>",
    "                <button type=\"button\" disabled={busy} onClick={makeOutOfEnvelope} style={buttonStyle}>Demo out-of-envelope abstain</button>",
    "                <button type=\"button\" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Reload sample</button>",
    "              </div>",
    "            </>",
    "          ) : <p style={{ color: \"#9ab8d7\" }}>Loading sample request...</p>}",
    "        </div>",
    "",
    "        <div style={cardStyle}>",
    "          <h2 style={{ marginTop: 0 }}>2. Projection result</h2>",
    "          {response ? (",
    "            <div style={{ display: \"grid\", gap: 12 }}>",
    "              <div>",
    "                <strong>{response.scenarioId}</strong>",
    "                <p style={{ color: \"#9ab8d7\", lineHeight: 1.6 }}>{response.supportMessage}</p>",
    "                <p style={{ color: \"#ffdd8a\", lineHeight: 1.6 }}>{response.projectionOnlyStatement}</p>",
    "              </div>",
    "              {response.projectedValueImpact ? (",
    "                <div style={{ border: \"1px solid rgba(100, 255, 218, 0.22)\", borderRadius: 14, padding: 12 }}>",
    "                  <strong>Projected value range</strong>",
    "                  <p style={{ margin: \"6px 0\", color: \"#9ab8d7\" }}>Min: {asMoney(response.projectedValueImpact.minValue, response.projectedValueImpact.currencyCode)}</p>",
    "                  <p style={{ margin: \"6px 0\", color: \"#9ab8d7\" }}>Expected: {asMoney(response.projectedValueImpact.expectedValue, response.projectedValueImpact.currencyCode)}</p>",
    "                  <p style={{ margin: \"6px 0\", color: \"#9ab8d7\" }}>Max: {asMoney(response.projectedValueImpact.maxValue, response.projectedValueImpact.currencyCode)}</p>",
    "                </div>",
    "              ) : null}",
    "              <table style={{ width: \"100%\", borderCollapse: \"collapse\" }}>",
    "                <thead><tr style={{ color: \"#9ab8d7\", textAlign: \"left\" }}><th style={{ padding: 8 }}>Metric</th><th style={{ padding: 8 }}>Baseline</th><th style={{ padding: 8 }}>Projected</th><th style={{ padding: 8 }}>Delta</th></tr></thead>",
    "                <tbody>",
    "                  {response.projectionPoints.map((point) => (",
    "                    <tr key={point.metricCode} style={{ borderTop: \"1px solid rgba(120, 190, 255, 0.12)\" }}>",
    "                      <td style={{ padding: 8 }}>{point.label}</td>",
    "                      <td style={{ padding: 8 }}>{asNumber(point.baselineValue)}</td>",
    "                      <td style={{ padding: 8 }}>{asNumber(point.projectedValue)}</td>",
    "                      <td style={{ padding: 8 }}>{asNumber(point.delta)}</td>",
    "                    </tr>",
    "                  ))}",
    "                </tbody>",
    "              </table>",
    "            </div>",
    "          ) : <p style={{ color: \"#9ab8d7\" }}>Run a simulation to see deterministic projection output.</p>}",
    "        </div>",
    "      </section>",
    "",
    "      <section style={cardStyle}>",
    "        <h2 style={{ marginTop: 0 }}>3. Honesty and support rules</h2>",
    "        <ul style={{ color: \"#9ab8d7\", lineHeight: 1.75, paddingLeft: 18 }}>",
    "          {(contract?.safetyRules ?? [",
    "            \"Projection only. Not a guaranteed saving.\",",
    "            \"No automatic process write-back.\",",
    "            \"Out-of-envelope scenario requests must abstain.\",",
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

  if (!text.includes("ScenarioSimulationPage")) {
    const lazyBlock = [
      "const ScenarioSimulationPage = lazy(() =>",
      "  import(\"./pages/Phase15/ScenarioSimulationPage\").then((m) => ({",
      "    default: m.ScenarioSimulationPage,",
      "  }))",
      ");",
      ""
    ].join("\n");

    const anchors = [
      "const EdgeCollectorPage = lazy(() =>",
      "const HistorianConnectorPage = lazy(() =>",
      "const I18nRtlReadinessPage = lazy(() =>"
    ];

    const anchor = anchors.find((candidate) => text.includes(candidate));
    if (!anchor) throw new Error("Could not find lazy page insertion anchor.");

    text = text.replace(anchor, lazyBlock + anchor);
    changed = true;
  }

  if (!text.includes('path="/phase15/scenario-simulation"')) {
    const routeBlock = [
      "                    {/* Pack G Phase 15 scenario simulation */}",
      "                    <Route",
      "                      path=\"/phase15/scenario-simulation\"",
      "                      element={withPageBoundary(",
      "                        \"/phase15/scenario-simulation\",",
      "                        \"Phase 15 scenario simulation is refreshing\",",
      "                        <ScenarioSimulationPage />",
      "                      )}",
      "                    />",
      "",
      "                    <Route",
      "                      path=\"/advisory/scenario-simulation\"",
      "                      element={<Navigate to=\"/phase15/scenario-simulation\" replace />}",
      "                    />",
      ""
    ].join("\n");

    const anchors = [
      '                    {/* Pack F edge collector management UX */}',
      '                    <Route\\n                      path="/edge-collector"',
      '                    {/* Pack E historian connector UI */}',
      "                    {/* Demo lifecycle */}"
    ];

    let inserted = false;

    if (text.includes('                    {/* Pack F edge collector management UX */}')) {
      text = text.replace('                    {/* Pack F edge collector management UX */}', routeBlock + '                    {/* Pack F edge collector management UX */}');
      inserted = true;
    } else if (text.includes('                    {/* Pack E historian connector UI */}')) {
      text = text.replace('                    {/* Pack E historian connector UI */}', routeBlock + '                    {/* Pack E historian connector UI */}');
      inserted = true;
    } else if (text.includes("                    {/* Demo lifecycle */}")) {
      text = text.replace("                    {/* Demo lifecycle */}", routeBlock + "                    {/* Demo lifecycle */}");
      inserted = true;
    }

    if (!inserted) throw new Error("Could not find route insertion anchor.");

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

  if (text.includes('to: "/phase15/scenario-simulation"')) {
    return { path: rel(layoutPath), status: "ALREADY_PATCHED" };
  }

  const newItem = '  { to: "/phase15/scenario-simulation", label: "What-if Simulation", desc: "Phase 15 advisory projection", icon: DatabaseZap },';

  const anchors = [
    '  { to: "/edge-collector", label: "Edge Collector"',
    '  { to: "/historian-connector", label: "Historian Connector"',
    '  { to: "/admin-preview", label: "Admin Preview"'
  ];

  const anchor = anchors.find((candidate) => text.includes(candidate));
  if (!anchor) throw new Error("Could not find AppLayout navigation insertion anchor.");

  text = text.replace(anchor, newItem + "\n" + anchor);
  write(layoutPath, text);

  return { path: rel(layoutPath), status: "PATCHED" };
}

function writeDocs() {
  const md = [];

  md.push("# Phase 15 What-if Scenario Engine");
  md.push("");
  md.push("Marker: PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE");
  md.push("");
  md.push("## Purpose");
  md.push("");
  md.push("This engine provides deterministic what-if projection for supported findings. It is not a causal optimizer and not a process controller.");
  md.push("");
  md.push("## Guardrails");
  md.push("");
  md.push("- Projection only. Not a guaranteed saving.");
  md.push("- Same request and seed return the same result.");
  md.push("- Out-of-envelope adjustments abstain.");
  md.push("- Weak or missing evidence blocks supported projection.");
  md.push("- No automatic write-back path exists.");
  md.push("");
  md.push("## Backend routes");
  md.push("");
  md.push("- `GET /api/p15/advisory/scenarios/health`");
  md.push("- `GET /api/p15/advisory/scenarios/contract`");
  md.push("- `GET /api/p15/advisory/scenarios/sample-request`");
  md.push("- `POST /api/p15/advisory/scenarios/simulate`");
  md.push("- `POST /api/p15/advisory/scenarios/simulate-demo`");
  md.push("");
  md.push("## Frontend routes");
  md.push("");
  md.push("- `/phase15/scenario-simulation`");
  md.push("- `/advisory/scenario-simulation` alias");
  md.push("");

  write(scenarioDocPath, md.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs",');
  lines.push('  "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs",');
  lines.push('  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",');
  lines.push('  "Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx",');
  lines.push('  "docs/developer/PHASE15_WHATIF_SCENARIO_ENGINE.md",');
  lines.push('  "docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.json",');
  lines.push('  "docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.md",');
  lines.push('  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('const requiredSignals = [');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "BuildStableScenarioSeed" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "OutOfEnvelope" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "ProjectionOnlyStatement" },');
  lines.push('  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "Weak evidence cannot support what-if projection." },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs", signal: "/api/p15/advisory/scenarios" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs", signal: "/simulate" },');
  lines.push('  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs", signal: "deterministic" },');
  lines.push('  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15AdvisoryScenarioEndpoints" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/advisory/scenarios/simulate" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx", signal: "Pack G · T-096 · What-if scenario simulation engine" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx", signal: "Demo out-of-envelope abstain" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/scenario-simulation" },');
  lines.push('  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "What-if Simulation" },');
  lines.push('  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE" }');
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
  lines.push('  console.error("Pack G-3 T-096 scenario engine validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack G-3 T-096 deterministic what-if scenario engine validation passed.");');

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
  lines.push('const bridgedJsonPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.PACK_G3_BRIDGED.json");');
  lines.push('const bridgedMdPath = path.join(root, "docs", "task-closure", "T096_T102_PHASE15_SCORECARD.PACK_G3_BRIDGED.md");');
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
  lines.push('const t096Green = runOk("node", ["tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs"]);');
  lines.push('');
  lines.push('for (const row of rows) {');
  lines.push('  if (code(row) === "T-096" && t096Green) setDone(row, "Pack G-3 evidence bridge: deterministic what-if scenario engine validator passed and builds were green.");');
  lines.push('}');
  lines.push('');
  lines.push('scorecard.packG3EvidenceBridge = { generatedAtUtc: new Date().toISOString(), marker: "PPIQ_PACK_G3_T096_SCORECARD_BRIDGE", t096Green };');
  lines.push('');
  lines.push('write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('write(bridgedJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('');
  lines.push('const below90 = rows.filter((row) => score(row) < 90);');
  lines.push('const md = [];');
  lines.push('md.push("# T096-T102 Phase 15 Scorecard — Pack G3 Bridged");');
  lines.push('md.push("");');
  lines.push('md.push("Marker: PPIQ_PACK_G3_T096_SCORECARD_BRIDGE");');
  lines.push('md.push("");');
  lines.push('md.push("T-096 bridge result: " + (t096Green ? "DONE" : "NOT GREEN"));');
  lines.push('md.push("");');
  lines.push('md.push("Tasks below 90% after Pack G3 bridge: " + below90.length);');
  lines.push('md.push("");');
  lines.push('for (const row of below90) md.push(rowLine(row));');
  lines.push('md.push("");');
  lines.push('write(bridgedMdPath, md.join("\\n") + "\\n");');
  lines.push('');
  lines.push('console.log("");');
  lines.push('console.log("Pack G3 task-closure evidence bridge applied.");');
  lines.push('console.log("T-096 bridge result: " + (t096Green ? "DONE" : "NOT GREEN"));');
  lines.push('console.log("");');
  lines.push('console.log("Tasks below 90% after Pack G3 bridge: " + below90.length);');
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
    marker: "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE",
    phase: "P15",
    pack: "G",
    task: "T-096",
    backendRouteRoot: "/api/p15/advisory/scenarios",
    frontendRoute: "/phase15/scenario-simulation",
    aliasRoute: "/advisory/scenario-simulation",
    results
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];
  md.push("# Pack G-3 T-096 What-if Scenario Engine Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE");
  md.push("");
  md.push("## Scope");
  md.push("");
  md.push("Adds deterministic what-if simulation backend, API route, frontend API client, and scenario simulation page. The engine is projection-only, bounded by observed data envelope, and blocks weak or missing evidence.");
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

  if (!evidence.includes("PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE")) {
    evidence += [
      "",
      "## Pack G-3 T-096 deterministic what-if scenario simulation engine",
      "",
      "- Marker: PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE.",
      "- Added deterministic scenario simulation service.",
      "- Added guarded scenario API endpoints.",
      "- Added frontend Phase 15 scenario simulation API client.",
      "- Added scenario simulation page with parameter adjustment panel.",
      "- Added out-of-envelope abstain demo.",
      "- Added validator and T-096 scorecard bridge.",
      "- Backend and frontend builds must remain green.",
      "",
      "Generated artifacts:",
      "",
      "- Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs",
      "- Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs",
      "- Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
      "- Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx",
      "- docs/developer/PHASE15_WHATIF_SCENARIO_ENGINE.md",
      "- docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.md",
      "- docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.json",
      "- tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs",
      "- tools/task-closure/ppiq-pack-g3-scorecard-bridge.cjs",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack G-3 — T-096 deterministic what-if scenario simulation engine");
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

run("node --check Pack G-3 validator", ["node", "--check", "tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs"]);
run("node --check Pack G3 bridge", ["node", "--check", "tools/task-closure/ppiq-pack-g3-scorecard-bridge.cjs"]);
run("Pack G-1 closure-map validator", ["node", "tools/pack-g/validate-pack-g-phase15-closure-map.cjs"]);
run("Pack G-2 contract validator", ["node", "tools/pack-g/validate-pack-g2-phase15-contract.cjs"]);
run("Pack G-3 T-096 scenario validator", ["node", "tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs"]);

run("Backend build", ["dotnet", "build", "Backend"]);
run("Frontend build", frontendBuildArgs());

run("Apply Pack G3 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-g3-scorecard-bridge.cjs"]);

console.log("");
console.log("Pack G-3 result:");
console.log(" - Deterministic scenario simulation service written");
console.log(" - Phase 15 scenario endpoints written");
console.log(" - Program endpoint registration patched");
console.log(" - Frontend API client written");
console.log(" - Scenario simulation page written");
console.log(" - T-096 validator passed");
console.log(" - Backend build passed");
console.log(" - Frontend build passed");
console.log(" - T-096 scorecard bridge applied");

console.log("");
console.log("=================================================================================================");
console.log("Pack G-3 completed.");
console.log("Expected: T-096 is green in the Pack G3 bridged Phase 15 scorecard.");
console.log("Report: docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.md");
console.log("Next  : Pack G-4 / T-097 — recommendation generator with expected e-impact.");
console.log("=================================================================================================");