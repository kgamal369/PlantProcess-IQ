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
  const target = p(".phase7_backup/t040_value_scenario_page_" + stamp + "/" + relativePath);
  ensureDir(path.dirname(target));
  fs.copyFileSync(source, target);
}

function commandForHost(command) {
  if (process.platform === "win32" && (command === "npm" || command === "npx")) {
    return command + ".cmd";
  }

  return command;
}

function run(name, command, args, cwdRelative) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(commandForHost(command), args, {
    cwd: cwdRelative ? p(cwdRelative) : root,
    stdio: "inherit",
    shell: false
  });
}

const filesToBackup = [
  "Frontend/PlantProcess.Web/src/App.implementation.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx",
  "Frontend/PlantProcess.Web/src/components/AppLayout.tsx",
  "Frontend/PlantProcess.Web/src/api/plantProcessApi.ts"
];

for (const file of filesToBackup) backup(file);

write("Frontend/PlantProcess.Web/src/api/value/value.api.ts", `
import { apiClient } from "../http";

export type Phase7BandDto = {
  Low: number;
  Mid: number;
  High: number;
};

export type Phase7CostAssumptionDto = {
  Currency: string;
  CostPerTon: Phase7BandDto | null;
  DowngradeDeltaPerTon: Phase7BandDto | null;
  ScrapCostPerTon: Phase7BandDto | null;
  DowntimeCostPerMin: Phase7BandDto | null;
  GradePremiumPerTon: Phase7BandDto | null;
  EnergyPricePerMwh: Phase7BandDto | null;
};

export type Phase7ImpactRequest = {
  FindingRef: string;
  CoilId: string | null;
  DefectCode: string | null;
  DefectRateDelta: number;
  MonthlyVolumeTons: number;
  ProductionStopMinutes: number;
  YieldLossTons: number;
  UseScrapCost: boolean;
};

export type Phase7RealizationWindow = {
  MetricCode: string;
  StartUtc: string;
  EndUtc: string;
  Value: number;
  Unit: string;
};

export type Phase7RealizationRequest = {
  TrackingCode: string;
  SourceRecommendationId: string | null;
  SourceValueImpactId: string | null;
  BaselineWindow: Phase7RealizationWindow;
  ActualWindow: Phase7RealizationWindow;
  Direction: 1 | 2;
  ValuePerUnit: Phase7BandDto;
  PotentialValue: Phase7BandDto;
  InvestmentCost: number;
  Currency: string;
};

export const phase7ValueApi = {
  putCostAssumptions(body: Phase7CostAssumptionDto) {
    return apiClient.put<unknown>("/api/value/cost-assumptions", body);
  },

  calculateImpact(body: Phase7ImpactRequest) {
    return apiClient.post<Record<string, unknown>>("/api/value/impact", body);
  },

  getRealizationContract() {
    return apiClient.get<Record<string, unknown>>("/api/value/realization/contract");
  },

  calculateRealization(body: Phase7RealizationRequest) {
    return apiClient.post<Record<string, unknown>>("/api/value/realization/calculate", body);
  },

  recordRealization(body: Phase7RealizationRequest) {
    return apiClient.post<Record<string, unknown>>("/api/value/realization/record", body);
  },

  getRealizationLedger(take = 10) {
    return apiClient.get<Record<string, unknown>>("/api/value/realization/ledger", { take });
  },
};
`);

write("Frontend/PlantProcess.Web/src/api/value/index.ts", `
export * from "./value.api";
`);

const plantApiPath = "Frontend/PlantProcess.Web/src/api/plantProcessApi.ts";
if (exists(plantApiPath)) {
  let plantApi = read(plantApiPath);
  if (!plantApi.includes('export * from "./value";')) {
    plantApi = plantApi.trimEnd() + '\nexport * from "./value";\n';
    write(plantApiPath, plantApi);
  }
}

write("Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7ValueScenarioMath.ts", `
export type LooseApiObject = Record<string, unknown>;

export type ScenarioRange = {
  low: number;
  expected: number;
  high: number;
};

export type ScenarioImpactView = ScenarioRange & {
  currency: string;
  isAbstained: boolean;
  abstainReason: string | null;
  caveat: string;
};

export type ScenarioRealizationView = ScenarioRange & {
  currency: string;
  status: string;
  improvementUnits: number;
  captureRateMid: number | null;
  roiMid: number | null;
  caveat: string;
  isAbstained: boolean;
};

export function readNumber(source: LooseApiObject | null | undefined, ...keys: string[]): number {
  if (!source) return 0;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string" && value.trim() !== "" && Number.isFinite(Number(value))) {
      return Number(value);
    }
  }

  return 0;
}

export function readString(source: LooseApiObject | null | undefined, fallback: string, ...keys: string[]): string {
  if (!source) return fallback;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "string" && value.trim() !== "") return value;
  }

  return fallback;
}

export function readBoolean(source: LooseApiObject | null | undefined, ...keys: string[]): boolean {
  if (!source) return false;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "boolean") return value;
  }

  return false;
}

export function normalizeImpact(source: LooseApiObject | null): ScenarioImpactView {
  return {
    currency: readString(source, "EUR", "currency", "Currency"),
    low: readNumber(source, "low", "Low"),
    expected: readNumber(source, "expected", "Expected", "mid", "Mid"),
    high: readNumber(source, "high", "High"),
    isAbstained: readBoolean(source, "isAbstained", "IsAbstained"),
    abstainReason: readString(source, "", "abstainReason", "AbstainReason") || null,
    caveat: readString(
      source,
      "Projected value range only; not a guaranteed saving.",
      "honestyCaveat",
      "HonestyCaveat"
    ),
  };
}

export function normalizeRealization(source: LooseApiObject | null): ScenarioRealizationView {
  return {
    currency: readString(source, "EUR", "currency", "Currency"),
    low: readNumber(source, "realizedLow", "RealizedLow"),
    expected: readNumber(source, "realizedExpected", "RealizedExpected", "realizedMid", "RealizedMid"),
    high: readNumber(source, "realizedHigh", "RealizedHigh"),
    status: readString(source, "NotCalculated", "status", "Status"),
    improvementUnits: readNumber(source, "improvementUnits", "ImprovementUnits"),
    captureRateMid: readNullableNumber(source, "captureRateMid", "CaptureRateMid"),
    roiMid: readNullableNumber(source, "roiMid", "RoiMid"),
    caveat: readString(
      source,
      "Baseline-vs-actual tracked value is not automatic causal attribution.",
      "attributionCaveat",
      "AttributionCaveat"
    ),
    isAbstained: readBoolean(source, "isAbstained", "IsAbstained"),
  };
}

export function readNullableNumber(source: LooseApiObject | null | undefined, ...keys: string[]): number | null {
  if (!source) return null;

  for (const key of keys) {
    const value = source[key];
    if (value === null || value === undefined) continue;
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string" && value.trim() !== "" && Number.isFinite(Number(value))) {
      return Number(value);
    }
  }

  return null;
}

export function formatMoney(value: number, currency = "EUR"): string {
  return new Intl.NumberFormat("en", {
    style: "currency",
    currency,
    maximumFractionDigits: 0,
  }).format(value);
}

export function workedCaseLocalProjection(): ScenarioRange {
  const affectedTons = 0.02 * 10_000;

  return {
    low: affectedTons * 140,
    expected: affectedTons * 210,
    high: affectedTons * 280,
  };
}
`);

write("Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts", `
import { describe, expect, it } from "vitest";
import {
  formatMoney,
  normalizeImpact,
  normalizeRealization,
  workedCaseLocalProjection,
} from "./phase7ValueScenarioMath";

describe("T-040 Phase7 value scenario page math", () => {
  it("reproduces the EUR 28k-56k worked-case projection", () => {
    expect(workedCaseLocalProjection()).toEqual({
      low: 28_000,
      expected: 42_000,
      high: 56_000,
    });
  });

  it("normalizes camelCase and PascalCase impact results", () => {
    expect(
      normalizeImpact({
        Currency: "EUR",
        Low: 28_000,
        Expected: 42_000,
        High: 56_000,
        IsAbstained: false,
        HonestyCaveat: "not a guaranteed saving",
      })
    ).toMatchObject({
      currency: "EUR",
      low: 28_000,
      expected: 42_000,
      high: 56_000,
      isAbstained: false,
    });

    expect(
      normalizeImpact({
        currency: "EUR",
        low: 28_000,
        mid: 42_000,
        high: 56_000,
      })
    ).toMatchObject({
      expected: 42_000,
    });
  });

  it("normalizes realization result and caveat", () => {
    const result = normalizeRealization({
      currency: "EUR",
      realizedLow: 2_000,
      realizedMid: 3_000,
      realizedHigh: 4_000,
      improvementUnits: 20,
      captureRateMid: 0.0714,
      roiMid: 3,
      status: "PositiveTrackedValue",
      attributionCaveat: "Correlation is not causation",
    });

    expect(result.expected).toBe(3_000);
    expect(result.captureRateMid).toBe(0.0714);
    expect(result.roiMid).toBe(3);
    expect(result.caveat).toContain("Correlation is not causation");
  });

  it("formats money for the scenario cards", () => {
    expect(formatMoney(28_000, "EUR")).toContain("28,000");
  });
});
`);

write("Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7-value-scenario.css", `
.phase7-value-scenario {
  display: grid;
  gap: 1rem;
}

.phase7-value-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
}

.phase7-value-card {
  border: 1px solid rgba(78, 203, 255, 0.18);
  border-radius: 18px;
  background: rgba(6, 18, 35, 0.72);
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.22);
  padding: 1rem;
}

.phase7-value-card strong {
  display: block;
  color: #eaf7ff;
  font-size: 1.35rem;
  margin-top: 0.35rem;
}

.phase7-value-card span,
.phase7-value-card p {
  color: rgba(223, 244, 255, 0.72);
}

.phase7-value-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.phase7-value-button {
  border: 1px solid rgba(80, 210, 255, 0.35);
  border-radius: 999px;
  background: linear-gradient(135deg, rgba(0, 170, 255, 0.28), rgba(0, 90, 170, 0.4));
  color: #eaf7ff;
  cursor: pointer;
  font-weight: 700;
  padding: 0.8rem 1rem;
}

.phase7-value-button:disabled {
  cursor: not-allowed;
  opacity: 0.58;
}

.phase7-value-formula {
  border-left: 3px solid rgba(80, 210, 255, 0.55);
  padding-left: 0.9rem;
}

.phase7-value-caveat {
  border: 1px solid rgba(255, 202, 95, 0.24);
  border-radius: 16px;
  background: rgba(255, 202, 95, 0.08);
  color: rgba(255, 241, 202, 0.94);
  padding: 0.9rem;
}

.phase7-value-status {
  color: rgba(163, 244, 197, 0.95);
  font-weight: 700;
}

.phase7-value-error {
  border: 1px solid rgba(255, 106, 106, 0.3);
  border-radius: 16px;
  background: rgba(255, 106, 106, 0.08);
  color: rgba(255, 222, 222, 0.95);
  padding: 0.9rem;
}
`);

write("Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/Phase7ValueScenarioPage.tsx", `
import { useMemo, useState } from "react";
import { phase7ValueApi, type Phase7RealizationRequest } from "../../api/value";
import {
  formatMoney,
  normalizeImpact,
  normalizeRealization,
  workedCaseLocalProjection,
  type ScenarioImpactView,
  type ScenarioRealizationView,
} from "./phase7ValueScenarioMath";
import "./phase7-value-scenario.css";

const T040_MARKER = "PPIQ_REALIZATION_T040_VALUE_SCENARIO_PAGE";

const demoAssumptions = {
  Currency: "EUR",
  CostPerTon: null,
  DowngradeDeltaPerTon: { Low: 140, Mid: 210, High: 280 },
  ScrapCostPerTon: { Low: 300, Mid: 400, High: 500 },
  DowntimeCostPerMin: { Low: 50, Mid: 75, High: 100 },
  GradePremiumPerTon: { Low: 100, Mid: 150, High: 200 },
  EnergyPricePerMwh: null,
};

const impactRequest = {
  FindingRef: "finding:edge-crack-demo-28k-56k",
  CoilId: "DEMO-COIL-EDGE-CRACK-001",
  DefectCode: "EDGE_CRACK",
  DefectRateDelta: 0.02,
  MonthlyVolumeTons: 10_000,
  ProductionStopMinutes: 0,
  YieldLossTons: 0,
  UseScrapCost: false,
};

const realizationRequest: Phase7RealizationRequest = {
  TrackingCode: "T040-EDGE-CRACK-SCENARIO-REALIZATION",
  SourceRecommendationId: "rec-edge-crack-001",
  SourceValueImpactId: "11111111-1111-1111-1111-111111111111",
  BaselineWindow: {
    MetricCode: "edge_crack_count",
    StartUtc: "2026-05-01T00:00:00Z",
    EndUtc: "2026-05-31T00:00:00Z",
    Value: 100,
    Unit: "defects",
  },
  ActualWindow: {
    MetricCode: "edge_crack_count",
    StartUtc: "2026-06-01T00:00:00Z",
    EndUtc: "2026-06-30T00:00:00Z",
    Value: 80,
    Unit: "defects",
  },
  Direction: 1,
  ValuePerUnit: { Low: 100, Mid: 150, High: 200 },
  PotentialValue: { Low: 28_000, Mid: 42_000, High: 56_000 },
  InvestmentCost: 1_000,
  Currency: "EUR",
};

export function Phase7ValueScenarioPage() {
  const [impact, setImpact] = useState<ScenarioImpactView | null>(null);
  const [realization, setRealization] = useState<ScenarioRealizationView | null>(null);
  const [ledgerStatus, setLedgerStatus] = useState<string>("Not recorded");
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const localProjection = useMemo(() => workedCaseLocalProjection(), []);

  async function runScenario() {
    setIsRunning(true);
    setError(null);
    setLedgerStatus("Preparing assumptions...");

    try {
      await phase7ValueApi.putCostAssumptions(demoAssumptions);

      setLedgerStatus("Calculating projected value impact...");
      const impactResponse = await phase7ValueApi.calculateImpact(impactRequest);
      const normalizedImpact = normalizeImpact(impactResponse);
      setImpact(normalizedImpact);

      setLedgerStatus("Calculating tracked baseline-vs-actual value...");
      const realizationResponse = await phase7ValueApi.calculateRealization(realizationRequest);
      const normalizedRealization = normalizeRealization(realizationResponse);
      setRealization(normalizedRealization);

      setLedgerStatus("Ready. Use Record tracked value to persist the ledger row.");
    } catch (scenarioError) {
      const message =
        scenarioError instanceof Error
          ? scenarioError.message
          : "Could not run value scenario.";
      setError(message);
      setLedgerStatus("Scenario failed safely.");
    } finally {
      setIsRunning(false);
    }
  }

  async function recordTrackedValue() {
    setIsRunning(true);
    setError(null);
    setLedgerStatus("Recording tracked value...");

    try {
      const response = await phase7ValueApi.recordRealization(realizationRequest);
      const recorded = Boolean(response.recorded ?? response.Recorded);
      setLedgerStatus(recorded ? "Tracked value recorded in realization ledger." : "Backend abstained from recording; check returned reason.");
    } catch (recordError) {
      const message =
        recordError instanceof Error
          ? recordError.message
          : "Could not record tracked value.";
      setError(message);
      setLedgerStatus("Record failed safely.");
    } finally {
      setIsRunning(false);
    }
  }

  return (
    <main className="page-shell phase7-value-scenario" data-testid="phase7-value-scenario-page">
      <section className="dashboard-hero">
        <div>
          <div className="eyebrow">{T040_MARKER}</div>
          <h1>Value Scenario Workbench</h1>
          <p>
            Run the EUR 28k-56k edge-crack worked case against the real value APIs,
            then compare projected value with tracked baseline-vs-actual value.
          </p>
          <div className="dashboard-subtitle-row">
            <span>Scenario: EDGE_CRACK downgrade</span>
            <span className="status-chip">Not MES / not guaranteed savings</span>
          </div>
        </div>

        <div className="phase7-value-actions">
          <button className="phase7-value-button" type="button" onClick={runScenario} disabled={isRunning}>
            {isRunning ? "Running..." : "Run value scenario"}
          </button>
          <button className="phase7-value-button" type="button" onClick={recordTrackedValue} disabled={isRunning || !realization}>
            Record tracked value
          </button>
        </div>
      </section>

      <section className="phase7-value-card phase7-value-formula">
        <span>Formula</span>
        <strong>0.02 Ã— 10,000 tons = 200 affected tons</strong>
        <p>
          200 affected tons Ã— EUR 140 / 210 / 280 downgrade delta per ton =
          EUR 28k / 42k / 56k per month.
        </p>
      </section>

      <section className="phase7-value-grid">
        <RangeCard title="Local expected fixture" range={localProjection} currency="EUR" />
        <RangeCard title="Projected API impact" range={impact} currency={impact?.currency ?? "EUR"} />
        <RangeCard title="Tracked realized value" range={realization} currency={realization?.currency ?? "EUR"} />
      </section>

      <section className="phase7-value-grid">
        <div className="phase7-value-card">
          <span>Realization status</span>
          <strong className="phase7-value-status">{realization?.status ?? "Not calculated"}</strong>
          <p>Improvement units: {realization?.improvementUnits ?? 0}</p>
          <p>Capture rate: {realization?.captureRateMid == null ? "n/a" : (realization.captureRateMid * 100).toFixed(2) + "%"}</p>
          <p>ROI ratio: {realization?.roiMid == null ? "n/a" : realization.roiMid.toFixed(2) + "x"}</p>
        </div>

        <div className="phase7-value-card">
          <span>Ledger status</span>
          <strong>{ledgerStatus}</strong>
          <p>
            Recording is intentionally separate from projection. The page never claims automatic causal savings.
          </p>
        </div>
      </section>

      <section className="phase7-value-caveat">
        <strong>Honesty caveat</strong>
        <p>
          {realization?.caveat ??
            impact?.caveat ??
            "Projected value range only; not a guaranteed saving. Baseline-vs-actual tracked value is not automatic causal attribution."}
        </p>
      </section>

      {error ? (
        <section className="phase7-value-error">
          <strong>Controlled error</strong>
          <p>{error}</p>
        </section>
      ) : null}
    </main>
  );
}

function RangeCard({
  title,
  range,
  currency,
}: {
  title: string;
  range: { low: number; expected: number; high: number } | null;
  currency: string;
}) {
  return (
    <div className="phase7-value-card">
      <span>{title}</span>
      <strong>{range ? formatMoney(range.expected, currency) : "Not calculated"}</strong>
      <p>
        Low: {range ? formatMoney(range.low, currency) : "-"} Â· Expected:{" "}
        {range ? formatMoney(range.expected, currency) : "-"} Â· High:{" "}
        {range ? formatMoney(range.high, currency) : "-"}
      </p>
    </div>
  );
}
`);

function patchAppRoute() {
  const appPath = exists("Frontend/PlantProcess.Web/src/App.implementation.tsx")
    ? "Frontend/PlantProcess.Web/src/App.implementation.tsx"
    : "Frontend/PlantProcess.Web/src/App.tsx";

  let app = read(appPath);

  if (!app.includes("Phase7ValueScenarioPage")) {
    const lazyBlock = [
      "",
      "const Phase7ValueScenarioPage = lazy(() =>",
      "  import(\"./pages/Phase7ValueScenario/Phase7ValueScenarioPage\").then((module) => ({",
      "    default: module.Phase7ValueScenarioPage,",
      "  }))",
      ");",
      ""
    ].join("\n");

    const exportIndex = app.indexOf("export default function App");
    if (exportIndex < 0) {
      throw new Error("Could not find export default function App in " + appPath);
    }

    app = app.slice(0, exportIndex) + lazyBlock + app.slice(exportIndex);
  }

  if (!app.includes('path="/value/scenario"')) {
    const routeLine = '                  <Route path="/value/scenario" element={<Phase7ValueScenarioPage />} />';

    const anchors = [
      /(\s*<Route path="\/commercial\/license"[^>]*\/>\s*)/,
      /(\s*<Route path="\/demo-lifecycle"[^>]*\/>\s*)/,
      /(\s*<Route path="\/ml-readiness"[^>]*\/>\s*)/,
      /(\s*<Route path="\/admin-preview"[^>]*\/>\s*)/,
      /(\s*<Route path="\/correlations"[^>]*\/>\s*)/
    ];

    let inserted = false;

    for (const anchor of anchors) {
      if (anchor.test(app)) {
        app = app.replace(anchor, "$1\n" + routeLine);
        inserted = true;
        break;
      }
    }

    if (!inserted) {
      app = app.replace("</Route>", routeLine + "\n                </Route>");
    }
  }

  write(appPath, app);
}

function patchLayoutNav() {
  const layoutPath = "Frontend/PlantProcess.Web/src/components/AppLayout.tsx";
  if (!exists(layoutPath)) return;

  let layout = read(layoutPath);

  if (layout.includes("/value/scenario")) return;

  const navLine = '  { to: "/value/scenario", label: "Value Scenario", desc: "Projected vs tracked ROI", icon: BarChart3 },';

  if (layout.includes("const NAV_INTELLIGENCE = [")) {
    layout = layout.replace(
      "const NAV_INTELLIGENCE = [",
      "const NAV_INTELLIGENCE = [\n" + navLine
    );
    write(layoutPath, layout);
    return;
  }

  if (layout.includes("const navItems = [")) {
    layout = layout.replace(
      "const navItems = [",
      "const navItems = [\n" + navLine
    );
    write(layoutPath, layout);
  }
}

patchAppRoute();
patchLayoutNav();

write("tools/phase7/validate-t040-value-scenario-page.cjs", `
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

function findAppFile() {
  if (exists("Frontend/PlantProcess.Web/src/App.implementation.tsx")) {
    return "Frontend/PlantProcess.Web/src/App.implementation.tsx";
  }
  return "Frontend/PlantProcess.Web/src/App.tsx";
}

const checks = [
  {
    file: "Frontend/PlantProcess.Web/src/api/value/value.api.ts",
    signals: [
      "phase7ValueApi",
      "/api/value/cost-assumptions",
      "/api/value/impact",
      "/api/value/realization/calculate",
      "/api/value/realization/record"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/Phase7ValueScenarioPage.tsx",
    signals: [
      "PPIQ_REALIZATION_T040_VALUE_SCENARIO_PAGE",
      "Value Scenario Workbench",
      "Run value scenario",
      "Record tracked value",
      "not a guaranteed saving",
      "not automatic causal attribution"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7ValueScenarioMath.ts",
    signals: [
      "workedCaseLocalProjection",
      "normalizeImpact",
      "normalizeRealization",
      "formatMoney"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts",
    signals: [
      "reproduces the EUR 28k-56k worked-case projection",
      "normalizes camelCase and PascalCase impact results",
      "normalizes realization result and caveat"
    ]
  },
  {
    file: findAppFile(),
    signals: [
      "Phase7ValueScenarioPage",
      "path=\\"/value/scenario\\""
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

if (exists("Frontend/PlantProcess.Web/src/components/AppLayout.tsx")) {
  const layout = read("Frontend/PlantProcess.Web/src/components/AppLayout.tsx");
  if (!layout.includes("/value/scenario")) {
    failures.push({ file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", reason: "navigation missing /value/scenario" });
  }
}

if (failures.length) {
  console.error("PPIQ-T040 failed: value scenario page wiring is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T040 passed: value scenario page, API client, route, navigation and tests are present.");
`);

write("docs/phase7/T040_VALUE_SCENARIO_PAGE.md", `
# T-040 Value Scenario Page

Marker: PPIQ_REALIZATION_T040_VALUE_SCENARIO_PAGE

## Result

The frontend now has a real Value Scenario Workbench route:

    /value/scenario

The page wires the Phase 07 value APIs:

- PUT /api/value/cost-assumptions
- POST /api/value/impact
- POST /api/value/realization/calculate
- POST /api/value/realization/record

## Guardrails

- Projected impact and tracked realized value are separated.
- The page reproduces the EUR 28k / 42k / 56k worked case.
- The page shows that projected value is not a guaranteed saving.
- The page shows that baseline-vs-actual value is not automatic causal attribution.

## Validation

Run:

    node tools/phase7/validate-t040-value-scenario-page.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts --config vitest.config.ts
`);

run("node --check T-040 validator", "node", ["--check", "tools/phase7/validate-t040-value-scenario-page.cjs"]);
run("T-040 validator", "node", ["tools/phase7/validate-t040-value-scenario-page.cjs"]);
run("Frontend build after T-040", "npm", ["run", "build"], "Frontend/PlantProcess.Web");
run("T-040 frontend tests", "npx", [
  "vitest",
  "run",
  "src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts",
  "--config",
  "vitest.config.ts"
], "Frontend/PlantProcess.Web");

console.log("");
console.log("=================================================================================================");
console.log("T-040 completed: value scenario page is wired and frontend is green.");
console.log("=================================================================================================");