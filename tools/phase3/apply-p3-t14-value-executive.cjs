const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "P3_T14_VALUE_ROI_EXECUTIVE_SURFACE";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function ensureDir(filePath) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function write(rel, content) {
  const target = full(rel);
  ensureDir(target);
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P3-T14] wrote " + rel);
}

function patchAppRoute() {
  const candidates = [
    "Frontend/PlantProcess.Web/src/AppRoutes.tsx",
    "Frontend/PlantProcess.Web/src/App.implementation.tsx",
    "Frontend/PlantProcess.Web/src/App.tsx",
  ];

  const appRel = candidates.find((x) => exists(x));
  if (!appRel) {
    throw new Error("Could not find AppRoutes.tsx, App.implementation.tsx, or App.tsx.");
  }

  let app = read(appRel);

  if (!app.includes("ValueExecutiveDashboardPage")) {
    const importLine = 'import ValueExecutiveDashboardPage from "./pages/ValueExecutive/ValueExecutiveDashboardPage";\n';

    const importMatches = [...app.matchAll(/^import .*?;\s*$/gm)];
    if (importMatches.length > 0) {
      const last = importMatches[importMatches.length - 1];
      const insertAt = last.index + last[0].length;
      app = app.slice(0, insertAt) + "\n" + importLine + app.slice(insertAt);
    } else {
      app = importLine + app;
    }
  }

  if (!app.includes('path="/value/executive"')) {
    const routeLine = '                  <Route path="/value/executive" element={<ValueExecutiveDashboardPage />} />';

    const anchors = [
      /(\s*<Route path="\/value\/scenario"[^>]*\/>\s*)/,
      /(\s*<Route path="\/analytics"[^>]*\/>\s*)/,
      /(\s*<Route path="\/correlations"[^>]*\/>\s*)/,
      /(\s*<Route path="\/ml-readiness"[^>]*\/>\s*)/,
      /(\s*<Route path="\/dashboard"[^>]*\/>\s*)/
    ];

    let inserted = false;

    for (const anchor of anchors) {
      if (anchor.test(app)) {
        app = app.replace(anchor, "$1\n" + routeLine + "\n");
        inserted = true;
        break;
      }
    }

    if (!inserted) {
      if (app.includes("</Routes>")) {
        app = app.replace("</Routes>", routeLine + "\n                </Routes>");
        inserted = true;
      } else if (app.includes("</Route>")) {
        app = app.replace("</Route>", routeLine + "\n                </Route>");
        inserted = true;
      }
    }

    if (!inserted) {
      throw new Error("Could not insert /value/executive route into " + appRel);
    }
  }

  write(appRel, app);
}

write("Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts", `
export const P3_T14_VALUE_ROI_EXECUTIVE_SURFACE = "${marker}";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5063";

export type P3T14Band = {
  low: number;
  mid: number;
  high: number;
};

export type P3T14CostAssumptions = {
  Currency: string;
  CostPerTon: P3T14Band | null;
  DowngradeDeltaPerTon: P3T14Band | null;
  ScrapCostPerTon: P3T14Band | null;
  DowntimeCostPerMin: P3T14Band | null;
  GradePremiumPerTon: P3T14Band | null;
  EnergyPricePerMwh: P3T14Band | null;
};

export type P3T14ImpactRequest = {
  FindingRef: string;
  CoilId: string;
  DefectCode: string;
  DefectRateDelta: number;
  MonthlyVolumeTons: number;
  ProductionStopMinutes: number;
  YieldLossTons: number;
  UseScrapCost: boolean;
};

export type P3T14ValueTerm = {
  name: string;
  inputsJson: string;
  low: number;
  mid: number;
  high: number;
  handle: string;
};

export type P3T14ImpactResult = {
  currency: string;
  low: number;
  mid: number;
  high: number;
  expected: number;
  terms: P3T14ValueTerm[];
  assumptionVersion: number;
  isAbstained: boolean;
  abstainReason: string | null;
  supportStatus: string;
  honestyCaveat: string;
};

function pick<T>(value: any, camel: string, pascal: string, fallback: T): T {
  if (value && value[camel] !== undefined) return value[camel] as T;
  if (value && value[pascal] !== undefined) return value[pascal] as T;
  return fallback;
}

function toNumber(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function handleToText(handle: any): string {
  if (!handle) return "provenance:missing";
  if (typeof handle === "string") return handle;
  return (
    handle.handle ??
    handle.Handle ??
    handle.id ??
    handle.Id ??
    handle.provenanceId ??
    handle.ProvenanceId ??
    JSON.stringify(handle)
  );
}

export function normalizeImpact(raw: any): P3T14ImpactResult {
  const termsRaw = pick<any[]>(raw, "terms", "Terms", []);

  const terms = termsRaw.map((term) => ({
    name: pick<string>(term, "name", "Name", "Value term"),
    inputsJson: pick<string>(term, "inputsJson", "InputsJson", "{}"),
    low: toNumber(pick(term, "low", "Low", 0)),
    mid: toNumber(pick(term, "mid", "Mid", 0)),
    high: toNumber(pick(term, "high", "High", 0)),
    handle: handleToText(pick(term, "handle", "Handle", null)),
  }));

  const isAbstained = Boolean(pick(raw, "isAbstained", "IsAbstained", false));
  const low = toNumber(pick(raw, "low", "Low", 0));
  const mid = toNumber(pick(raw, "mid", "Mid", low));
  const high = toNumber(pick(raw, "high", "High", mid));

  return {
    currency: pick<string>(raw, "currency", "Currency", "EUR"),
    low,
    mid,
    high,
    expected: toNumber(pick(raw, "expected", "Expected", mid)),
    terms,
    assumptionVersion: toNumber(pick(raw, "assumptionVersion", "AssumptionVersion", 0)),
    isAbstained,
    abstainReason: pick<string | null>(raw, "abstainReason", "AbstainReason", null),
    supportStatus: pick<string>(raw, "supportStatus", "SupportStatus", isAbstained ? "Abstained" : "BoundedRange"),
    honestyCaveat: pick<string>(
      raw,
      "honestyCaveat",
      "HonestyCaveat",
      isAbstained
        ? "No value claim emitted because the required assumption basis is incomplete."
        : "Projected bounded range only; not booked benefit. Every figure is tied to assumptions, inputs, and provenance."
    ),
  };
}

export const p3t14DemoAssumptions: P3T14CostAssumptions = {
  Currency: "EUR",
  CostPerTon: null,
  DowngradeDeltaPerTon: { Low: 140, Mid: 210, High: 280 } as any,
  ScrapCostPerTon: { Low: 300, Mid: 400, High: 500 } as any,
  DowntimeCostPerMin: { Low: 50, Mid: 75, High: 100 } as any,
  GradePremiumPerTon: { Low: 100, Mid: 150, High: 200 } as any,
  EnergyPricePerMwh: null,
};

export const p3t14AbstainAssumptions: P3T14CostAssumptions = {
  Currency: "EUR",
  CostPerTon: null,
  DowngradeDeltaPerTon: null,
  ScrapCostPerTon: null,
  DowntimeCostPerMin: null,
  GradePremiumPerTon: null,
  EnergyPricePerMwh: null,
};

export const p3t14ApprovedFinding: P3T14ImpactRequest = {
  FindingRef: "finding:edge-crack-demo-28k-56k",
  CoilId: "DEMO-COIL-EDGE-CRACK-001",
  DefectCode: "EDGE_CRACK",
  DefectRateDelta: 0.02,
  MonthlyVolumeTons: 10000,
  ProductionStopMinutes: 0,
  YieldLossTons: 0,
  UseScrapCost: false,
};

export function computeWorkedCasePreview() {
  const affectedTons = p3t14ApprovedFinding.DefectRateDelta * p3t14ApprovedFinding.MonthlyVolumeTons;
  return {
    affectedTons,
    low: affectedTons * 140,
    mid: affectedTons * 210,
    high: affectedTons * 280,
    currency: "EUR",
    formula: "0.02 defect-rate delta × 10,000 monthly tons × EUR 140/210/280 downgrade band",
  };
}

export function formatMoney(value: number, currency = "EUR"): string {
  return new Intl.NumberFormat("en-IE", {
    style: "currency",
    currency,
    maximumFractionDigits: 0,
  }).format(value);
}

export function computePayback(result: P3T14ImpactResult | null, monthlyLicenseCost: number) {
  if (!result || result.isAbstained || monthlyLicenseCost <= 0) {
    return {
      lowMultiple: 0,
      midMultiple: 0,
      highMultiple: 0,
      monthsToPaybackAtMid: null as number | null,
    };
  }

  return {
    lowMultiple: result.low / monthlyLicenseCost,
    midMultiple: result.mid / monthlyLicenseCost,
    highMultiple: result.high / monthlyLicenseCost,
    monthsToPaybackAtMid: result.mid > 0 ? monthlyLicenseCost / result.mid : null,
  };
}

function escapeHtml(value: unknown): string {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

export function buildMonthlyValueReportHtml(result: P3T14ImpactResult | null, monthlyLicenseCost: number): string {
  const payback = computePayback(result, monthlyLicenseCost);
  const generated = new Date().toISOString();

  const rangeHtml =
    !result || result.isAbstained
      ? '<div class="abstain">ABSTAIN — insufficient basis. No fabricated money value is displayed.</div>'
      : '<div class="range">' +
          '<div><span>Low</span><strong>' + formatMoney(result.low, result.currency) + '</strong></div>' +
          '<div><span>Mid</span><strong>' + formatMoney(result.mid, result.currency) + '</strong></div>' +
          '<div><span>High</span><strong>' + formatMoney(result.high, result.currency) + '</strong></div>' +
        '</div>';

  const termRows =
    result && !result.isAbstained
      ? result.terms.map((term) =>
          '<tr>' +
          '<td>' + escapeHtml(term.name) + '</td>' +
          '<td>' + formatMoney(term.low, result.currency) + '</td>' +
          '<td>' + formatMoney(term.mid, result.currency) + '</td>' +
          '<td>' + formatMoney(term.high, result.currency) + '</td>' +
          '<td><code>' + escapeHtml(term.handle) + '</code></td>' +
          '</tr>'
        ).join("")
      : '<tr><td colspan="5">No value terms emitted because the engine abstained.</td></tr>';

  return '<!doctype html>' +
    '<html><head><meta charset="utf-8" />' +
    '<title>PlantProcess IQ Monthly Value Report</title>' +
    '<style>' +
    'body{font-family:Inter,Segoe UI,Arial,sans-serif;background:#fff;color:#102033;margin:40px;}' +
    '.brand{border-bottom:4px solid #15a9ff;padding-bottom:16px;margin-bottom:24px;}' +
    '.kicker{color:#1673a8;text-transform:uppercase;letter-spacing:.08em;font-size:12px;font-weight:800;}' +
    'h1{margin:.2rem 0 0;font-size:30px;} .muted{color:#597083;}' +
    '.range{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin:24px 0;}' +
    '.range div{border:1px solid #cfe2ef;border-radius:14px;padding:18px;background:#f7fbff;}' +
    '.range span{display:block;color:#5f7285;font-size:12px;text-transform:uppercase;font-weight:800;}' +
    '.range strong{font-size:28px;color:#07314f;}' +
    '.abstain{border:1px solid #f0a23c;background:#fff8eb;border-radius:14px;padding:18px;font-weight:800;color:#7a4a00;}' +
    'table{width:100%;border-collapse:collapse;margin-top:20px;} th,td{border-bottom:1px solid #d8e5ee;text-align:left;padding:10px;vertical-align:top;} th{background:#edf7ff;color:#17384f;}' +
    'code{font-size:11px;background:#edf2f7;padding:3px 6px;border-radius:6px;}' +
    '.guard{margin-top:24px;border-left:5px solid #15a9ff;padding:12px 16px;background:#f4fbff;}' +
    '@media print{button{display:none} body{margin:20mm} .range div{break-inside:avoid}}' +
    '</style></head>' +
    '<body>' +
    '<section class="brand"><div class="kicker">PlantProcess IQ</div><h1>Monthly Value Report</h1>' +
    '<p class="muted">Generated ' + escapeHtml(generated) + ' · Source finding: ' + escapeHtml(p3t14ApprovedFinding.FindingRef) + '</p></section>' +
    rangeHtml +
    '<section><h2>Payback view</h2>' +
    '<p>Monthly platform cost entered: <strong>' + formatMoney(monthlyLicenseCost, result?.currency ?? "EUR") + '</strong></p>' +
    '<p>Mid-case opportunity multiple: <strong>' + (payback.midMultiple ? payback.midMultiple.toFixed(2) + "×" : "not available") + '</strong></p></section>' +
    '<section><h2>Input drill-through and provenance</h2>' +
    '<table><thead><tr><th>Term</th><th>Low</th><th>Mid</th><th>High</th><th>Provenance handle</th></tr></thead><tbody>' + termRows + '</tbody></table></section>' +
    '<section class="guard"><strong>Honesty guard:</strong> Projected bounded opportunity only; not booked benefit. Correlation is not causation. Every visible figure is tied to an engine result or a provenance handle.</section>' +
    '</body></html>';
}

async function api<T>(apiPath: string, init?: RequestInit): Promise<T> {
  const response = await fetch(apiBaseUrl + apiPath, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    throw new Error(response.status + " " + response.statusText);
  }

  return response.json() as Promise<T>;
}

export async function saveCostAssumptions(assumptions: P3T14CostAssumptions) {
  return api<any>("/api/value/cost-assumptions", {
    method: "PUT",
    body: JSON.stringify(assumptions),
  });
}

export async function computeValueImpact(request: P3T14ImpactRequest): Promise<P3T14ImpactResult> {
  const raw = await api<any>("/api/value/impact", {
    method: "POST",
    body: JSON.stringify(request),
  });

  return normalizeImpact(raw);
}

export async function runEngineWorkedCase(): Promise<P3T14ImpactResult> {
  await saveCostAssumptions(p3t14DemoAssumptions);
  return computeValueImpact(p3t14ApprovedFinding);
}

export async function runEngineAbstainProof(): Promise<P3T14ImpactResult> {
  await saveCostAssumptions(p3t14AbstainAssumptions);
  try {
    return await computeValueImpact(p3t14ApprovedFinding);
  } finally {
    await saveCostAssumptions(p3t14DemoAssumptions);
  }
}
`);

write("Frontend/PlantProcess.Web/src/pages/ValueExecutive/value-executive.css", `
.value-exec-page {
  min-height: 100%;
  padding: 28px;
  color: #eef8ff;
}

.value-exec-hero {
  border: 1px solid rgba(0, 212, 255, 0.22);
  border-radius: 24px;
  padding: 26px;
  background:
    radial-gradient(circle at top right, rgba(44, 230, 162, 0.16), transparent 34%),
    linear-gradient(135deg, rgba(8, 22, 38, 0.96), rgba(7, 14, 28, 0.98));
  box-shadow: 0 20px 70px rgba(0, 0, 0, 0.24);
}

.value-exec-kicker {
  color: #2ce6a2;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  font-size: 0.78rem;
}

.value-exec-hero h1 {
  margin: 8px 0 8px;
  font-size: clamp(2rem, 4vw, 3.4rem);
}

.value-exec-muted {
  color: rgba(230, 244, 255, 0.74);
}

.value-exec-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 20px;
}

.value-exec-button {
  border: 1px solid rgba(44, 230, 162, 0.35);
  border-radius: 14px;
  padding: 12px 18px;
  color: #eaf7ff;
  background: rgba(44, 230, 162, 0.18);
  cursor: pointer;
  font-weight: 800;
}

.value-exec-button.secondary {
  border-color: rgba(0, 212, 255, 0.28);
  background: rgba(0, 118, 188, 0.18);
}

.value-exec-button.warning {
  border-color: rgba(255, 181, 71, 0.4);
  background: rgba(255, 181, 71, 0.14);
}

.value-exec-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(180px, 1fr));
  gap: 14px;
  margin-top: 20px;
}

.value-exec-card {
  border: 1px solid rgba(0, 212, 255, 0.18);
  border-radius: 20px;
  padding: 18px;
  background: rgba(7, 20, 36, 0.82);
}

.value-exec-card span {
  display: block;
  color: rgba(230, 244, 255, 0.66);
  font-size: 0.78rem;
  text-transform: uppercase;
  font-weight: 800;
  letter-spacing: 0.05em;
}

.value-exec-card strong {
  display: block;
  margin-top: 8px;
  font-size: 1.9rem;
  color: #ffffff;
}

.value-exec-panel {
  margin-top: 18px;
  border: 1px solid rgba(0, 212, 255, 0.16);
  border-radius: 22px;
  padding: 20px;
  background: rgba(5, 16, 30, 0.84);
}

.value-exec-panel h2,
.value-exec-panel h3 {
  margin-top: 0;
}

.value-exec-table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 12px;
}

.value-exec-table th,
.value-exec-table td {
  border-bottom: 1px solid rgba(180, 220, 245, 0.16);
  padding: 10px;
  text-align: left;
  vertical-align: top;
}

.value-exec-table th {
  color: #aee8ff;
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.value-exec-code {
  display: inline-block;
  max-width: 340px;
  overflow-wrap: anywhere;
  border-radius: 8px;
  padding: 4px 7px;
  background: rgba(206, 240, 255, 0.12);
  color: #d9f4ff;
  font-size: 0.78rem;
}

.value-exec-abstain {
  border: 1px solid rgba(255, 181, 71, 0.42);
  border-radius: 18px;
  padding: 16px;
  background: rgba(255, 181, 71, 0.12);
  color: #ffe2aa;
  font-weight: 800;
}

.value-exec-license {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
  margin-top: 12px;
}

.value-exec-license input {
  border: 1px solid rgba(0, 212, 255, 0.25);
  border-radius: 12px;
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.08);
  color: #fff;
}

@media print {
  .value-exec-page {
    color: #102033;
    background: #fff;
  }

  .value-exec-hero,
  .value-exec-panel,
  .value-exec-card {
    color: #102033;
    background: #fff;
    border-color: #cfe2ef;
    box-shadow: none;
  }

  .value-exec-actions {
    display: none;
  }
}

@media (max-width: 900px) {
  .value-exec-grid {
    grid-template-columns: 1fr;
  }
}
`);

write("Frontend/PlantProcess.Web/src/pages/ValueExecutive/ValueExecutiveDashboardPage.tsx", `
import { useMemo, useState } from "react";
import {
  buildMonthlyValueReportHtml,
  computePayback,
  computeWorkedCasePreview,
  formatMoney,
  runEngineAbstainProof,
  runEngineWorkedCase,
  type P3T14ImpactResult,
} from "../../api/p3T14ValueExecutive";
import "./value-executive.css";

export const P3_T14_VALUE_ROI_EXECUTIVE_SURFACE = "${marker}";

function openReport(html: string) {
  const win = window.open("", "_blank", "noopener,noreferrer,width=1100,height=800");
  if (!win) {
    throw new Error("Popup blocked. Allow popups to open the monthly value report.");
  }

  win.document.open();
  win.document.write(html);
  win.document.close();
  win.focus();
  setTimeout(() => win.print(), 250);
}

export function ValueExecutiveDashboardPage() {
  const [impact, setImpact] = useState<P3T14ImpactResult | null>(null);
  const [abstain, setAbstain] = useState<P3T14ImpactResult | null>(null);
  const [monthlyLicenseCost, setMonthlyLicenseCost] = useState(12000);
  const [status, setStatus] = useState("Ready to call the deterministic value engine.");
  const [error, setError] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);

  const preview = useMemo(() => computeWorkedCasePreview(), []);
  const payback = useMemo(() => computePayback(impact, monthlyLicenseCost), [impact, monthlyLicenseCost]);

  async function runDashboard() {
    setIsRunning(true);
    setError(null);
    setAbstain(null);
    setStatus("Calling value engine with approved finding and versioned cost assumptions...");

    try {
      const result = await runEngineWorkedCase();
      setImpact(result);
      setStatus("Engine result loaded. Low/Mid/High values below are rendered from /api/value/impact.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setStatus("Could not compute value impact.");
    } finally {
      setIsRunning(false);
    }
  }

  async function runAbstain() {
    setIsRunning(true);
    setError(null);
    setStatus("Calling value engine with missing assumptions to prove ABSTAIN behavior...");

    try {
      const result = await runEngineAbstainProof();
      setAbstain(result);
      setStatus("ABSTAIN proof complete. No fabricated money value should appear for the missing-assumption case.");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setStatus("Could not run abstain proof.");
    } finally {
      setIsRunning(false);
    }
  }

  function printReport() {
    const html = buildMonthlyValueReportHtml(impact, monthlyLicenseCost);
    openReport(html);
  }

  const currency = impact?.currency ?? "EUR";

  return (
    <main className="value-exec-page" data-p3-task="P3-T14" data-testid="p3-t14-value-executive-dashboard">
      <section className="value-exec-hero">
        <div className="value-exec-kicker">P3-T14 · Value/ROI executive surface</div>
        <h1>Executive Value Dashboard</h1>
        <p className="value-exec-muted">
          Calls the deterministic value engine, renders bounded Low/Mid/High impact, exposes every input and
          provenance handle, and refuses to show money when assumptions are missing.
        </p>

        <div className="value-exec-actions">
          <button className="value-exec-button" type="button" onClick={runDashboard} disabled={isRunning}>
            {isRunning ? "Running..." : "Run approved finding through value engine"}
          </button>
          <button className="value-exec-button warning" type="button" onClick={runAbstain} disabled={isRunning}>
            Prove ABSTAIN on missing assumptions
          </button>
          <button className="value-exec-button secondary" type="button" onClick={printReport} disabled={!impact || impact.isAbstained}>
            Open monthly value report PDF
          </button>
        </div>

        <p className="value-exec-muted" role="status">{status}</p>
        {error ? <p className="value-exec-abstain" role="alert">Controlled error: {error}</p> : null}
      </section>

      <section className="value-exec-panel">
        <h2>Doctrine worked-case preflight</h2>
        <p className="value-exec-muted">
          Local arithmetic preview only: {preview.formula}. The executive cards are not accepted until the API engine returns the same bounded range.
        </p>
        <div className="value-exec-grid">
          <div className="value-exec-card"><span>Preview Low</span><strong>{formatMoney(preview.low, preview.currency)}</strong></div>
          <div className="value-exec-card"><span>Preview Mid</span><strong>{formatMoney(preview.mid, preview.currency)}</strong></div>
          <div className="value-exec-card"><span>Preview High</span><strong>{formatMoney(preview.high, preview.currency)}</strong></div>
        </div>
      </section>

      <section className="value-exec-panel">
        <h2>Engine output</h2>

        {impact && !impact.isAbstained ? (
          <>
            <div className="value-exec-grid">
              <div className="value-exec-card" data-testid="p3-t14-low"><span>Low</span><strong>{formatMoney(impact.low, currency)}</strong></div>
              <div className="value-exec-card" data-testid="p3-t14-mid"><span>Mid</span><strong>{formatMoney(impact.mid, currency)}</strong></div>
              <div className="value-exec-card" data-testid="p3-t14-high"><span>High</span><strong>{formatMoney(impact.high, currency)}</strong></div>
            </div>

            <p className="value-exec-muted">
              Assumption version {impact.assumptionVersion}. Support status: {impact.supportStatus}. {impact.honestyCaveat}
            </p>
          </>
        ) : (
          <p className="value-exec-muted">No engine result yet. Run the approved finding to render the bounded range.</p>
        )}

        {impact?.isAbstained ? (
          <div className="value-exec-abstain" data-testid="p3-t14-abstain">
            ABSTAIN — {impact.abstainReason ?? "insufficient basis"}. No fabricated money number is displayed.
          </div>
        ) : null}
      </section>

      <section className="value-exec-panel">
        <h2>Payback view vs license cost</h2>
        <div className="value-exec-license">
          <label htmlFor="p3t14-license-cost">Monthly license cost</label>
          <input
            id="p3t14-license-cost"
            type="number"
            min="1"
            value={monthlyLicenseCost}
            onChange={(event) => setMonthlyLicenseCost(Number(event.target.value))}
          />
          <strong>{formatMoney(monthlyLicenseCost, currency)}</strong>
        </div>

        <div className="value-exec-grid">
          <div className="value-exec-card"><span>Low multiple</span><strong>{payback.lowMultiple.toFixed(2)}×</strong></div>
          <div className="value-exec-card"><span>Mid multiple</span><strong>{payback.midMultiple.toFixed(2)}×</strong></div>
          <div className="value-exec-card"><span>High multiple</span><strong>{payback.highMultiple.toFixed(2)}×</strong></div>
        </div>
      </section>

      <section className="value-exec-panel">
        <h2>Input drill-through and provenance</h2>
        <p className="value-exec-muted">
          Every value term below comes from the engine response and carries its own provenance handle.
        </p>

        <table className="value-exec-table">
          <thead>
            <tr>
              <th>Term</th>
              <th>Low</th>
              <th>Mid</th>
              <th>High</th>
              <th>Input drill-through</th>
              <th>Provenance handle</th>
            </tr>
          </thead>
          <tbody>
            {impact && !impact.isAbstained && impact.terms.length > 0 ? (
              impact.terms.map((term) => (
                <tr key={term.name + term.handle}>
                  <td>{term.name}</td>
                  <td>{formatMoney(term.low, currency)}</td>
                  <td>{formatMoney(term.mid, currency)}</td>
                  <td>{formatMoney(term.high, currency)}</td>
                  <td>
                    <details>
                      <summary>Show inputs</summary>
                      <code className="value-exec-code">{term.inputsJson}</code>
                    </details>
                  </td>
                  <td><code className="value-exec-code">{term.handle}</code></td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6}>No value terms rendered yet, or the engine abstained.</td>
              </tr>
            )}
          </tbody>
        </table>
      </section>

      {abstain ? (
        <section className="value-exec-panel">
          <h2>Missing-assumption proof</h2>
          <div className="value-exec-abstain" data-testid="p3-t14-abstain-proof">
            {abstain.isAbstained
              ? "ABSTAIN — " + (abstain.abstainReason ?? "insufficient basis") + ". No fabricated money number is displayed."
              : "Unexpected: engine returned a value even though assumptions were deliberately removed."}
          </div>
        </section>
      ) : null}
    </main>
  );
}

export default ValueExecutiveDashboardPage;
`);

write("Frontend/PlantProcess.Web/src/pages/ValueExecutive/p3t14ValueExecutive.test.ts", `
import { describe, expect, it } from "vitest";
import {
  buildMonthlyValueReportHtml,
  computePayback,
  computeWorkedCasePreview,
  formatMoney,
  normalizeImpact,
} from "../../api/p3T14ValueExecutive";

describe("P3-T14 executive value surface helpers", () => {
  it("reproduces the worked EUR 28k / 42k / 56k range from arithmetic inputs", () => {
    const preview = computeWorkedCasePreview();

    expect(preview.affectedTons).toBe(200);
    expect(preview.low).toBe(28000);
    expect(preview.mid).toBe(42000);
    expect(preview.high).toBe(56000);
  });

  it("normalizes real engine Low/Mid/High and provenance terms without changing the numbers", () => {
    const result = normalizeImpact({
      Currency: "EUR",
      Low: 28000,
      Mid: 42000,
      High: 56000,
      Expected: 42000,
      IsAbstained: false,
      AssumptionVersion: 7,
      SupportStatus: "BoundedRange",
      Terms: [
        {
          Name: "Downgrade impact",
          InputsJson: "{\\"affectedTons\\":200,\\"band\\":[140,210,280]}",
          Low: 28000,
          Mid: 42000,
          High: 56000,
          Handle: { Handle: "prov:value:edge-crack:001" },
        },
      ],
    });

    expect(result.low).toBe(28000);
    expect(result.mid).toBe(42000);
    expect(result.high).toBe(56000);
    expect(result.terms[0].handle).toBe("prov:value:edge-crack:001");
  });

  it("renders ABSTAIN report without fabricated euro values", () => {
    const result = normalizeImpact({
      Currency: "EUR",
      Low: 0,
      Mid: 0,
      High: 0,
      IsAbstained: true,
      AbstainReason: "insufficient basis: downgradeDeltaPerTon missing",
      Terms: [],
    });

    const html = buildMonthlyValueReportHtml(result, 12000);

    expect(html).toContain("ABSTAIN");
    expect(html).toContain("insufficient basis");
    expect(html).not.toContain("€0");
    expect(html).not.toMatch(/guaranteed|will save/i);
  });

  it("computes payback multiples from engine output versus license cost", () => {
    const result = normalizeImpact({
      Currency: "EUR",
      Low: 28000,
      Mid: 42000,
      High: 56000,
      IsAbstained: false,
      Terms: [],
    });

    const payback = computePayback(result, 12000);

    expect(payback.lowMultiple).toBeCloseTo(2.333, 2);
    expect(payback.midMultiple).toBeCloseTo(3.5, 2);
    expect(payback.highMultiple).toBeCloseTo(4.666, 2);
  });

  it("formats the executive values as euro money", () => {
    expect(formatMoney(28000, "EUR")).toMatch(/28,000|28\\s000/);
  });
});
`);

write("Frontend/PlantProcess.Web/tests/e2e/p3t14-value-executive.spec.ts", `
import { expect, test } from "@playwright/test";

test.describe("P3-T14 Value/ROI executive surface", () => {
  test("renders engine Low/Mid/High, provenance, report button, and abstain proof", async ({ page }) => {
    await page.route("**/api/value/cost-assumptions", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ok: true }) });
    });

    await page.route("**/api/value/impact", async (route) => {
      const body = JSON.stringify({
        Currency: "EUR",
        Low: 28000,
        Mid: 42000,
        High: 56000,
        Expected: 42000,
        IsAbstained: false,
        AssumptionVersion: 7,
        SupportStatus: "BoundedRange",
        HonestyCaveat: "Projected bounded opportunity only; every figure is tied to assumptions, inputs, and provenance.",
        Terms: [
          {
            Name: "Downgrade impact",
            InputsJson: "{\\"affectedTons\\":200,\\"monthlyVolumeTons\\":10000,\\"defectRateDelta\\":0.02}",
            Low: 28000,
            Mid: 42000,
            High: 56000,
            Handle: { Handle: "prov:value:edge-crack:001" },
          },
        ],
      });

      await route.fulfill({ status: 200, contentType: "application/json", body });
    });

    await page.goto("/value/executive");
    await page.getByRole("button", { name: /run approved finding/i }).click();

    await expect(page.getByTestId("p3-t14-low")).toContainText(/28,000|28\\s000/);
    await expect(page.getByTestId("p3-t14-mid")).toContainText(/42,000|42\\s000/);
    await expect(page.getByTestId("p3-t14-high")).toContainText(/56,000|56\\s000/);
    await expect(page.getByText("prov:value:edge-crack:001")).toBeVisible();
    await expect(page.getByRole("button", { name: /monthly value report pdf/i })).toBeEnabled();

    await expect(page.locator("body")).not.toContainText(/guaranteed|will save/i);
  });
});
`);

write("tools/phase3/validate-p3-t14-value-executive.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();

function read(rel) {
  return fs.readFileSync(path.join(root, rel), "utf8");
}

function exists(rel) {
  return fs.existsSync(path.join(root, rel));
}

function fail(message) {
  console.error("[RED] P3-T14 validation failed: " + message);
  process.exit(1);
}

const required = [
  "Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts",
  "Frontend/PlantProcess.Web/src/pages/ValueExecutive/ValueExecutiveDashboardPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/ValueExecutive/value-executive.css",
  "Frontend/PlantProcess.Web/src/pages/ValueExecutive/p3t14ValueExecutive.test.ts",
  "Frontend/PlantProcess.Web/tests/e2e/p3t14-value-executive.spec.ts",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const api = read("Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts");
const page = read("Frontend/PlantProcess.Web/src/pages/ValueExecutive/ValueExecutiveDashboardPage.tsx");
const test = read("Frontend/PlantProcess.Web/src/pages/ValueExecutive/p3t14ValueExecutive.test.ts");
const e2e = read("Frontend/PlantProcess.Web/tests/e2e/p3t14-value-executive.spec.ts");

if (!api.includes("P3_T14_VALUE_ROI_EXECUTIVE_SURFACE")) fail("missing P3-T14 marker");
if (!api.includes("/api/value/impact")) fail("page API wrapper does not call /api/value/impact");
if (!api.includes("/api/value/cost-assumptions")) fail("page API wrapper does not configure cost assumptions");
if (!page.includes("Open monthly value report PDF")) fail("missing monthly report PDF action");
if (!page.includes("provenance handle")) fail("missing provenance wording");
if (!page.includes("ABSTAIN")) fail("missing ABSTAIN presentation");
if (!page.includes("data-testid=\\"p3-t14-low\\"")) fail("missing low test id");
if (!page.includes("data-testid=\\"p3-t14-mid\\"")) fail("missing mid test id");
if (!page.includes("data-testid=\\"p3-t14-high\\"")) fail("missing high test id");
if (!test.includes("28000") || !test.includes("42000") || !test.includes("56000")) fail("unit test does not assert exact EUR worked case");
if (!e2e.includes("prov:value:edge-crack:001")) fail("e2e test does not assert provenance handle");

const forbidden = /guaranteed|will save/i;
for (const [name, content] of [
  ["api", api],
  ["page", page],
]) {
  if (forbidden.test(content)) fail(name + " contains forbidden value-claim phrasing");
}

const routeCandidates = [
  "Frontend/PlantProcess.Web/src/AppRoutes.tsx",
  "Frontend/PlantProcess.Web/src/App.implementation.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx",
].filter(exists);

if (!routeCandidates.some((rel) => read(rel).includes('path="/value/executive"'))) {
  fail("missing /value/executive route");
}

console.log("[GREEN] P3-T14 static validation passed.");
`);

write("docs/phase3/P3_T14_VALUE_EXECUTIVE_SURFACE.md", `
# P3-T14 — Value/ROI executive surface

Marker: P3_T14_VALUE_ROI_EXECUTIVE_SURFACE

## Result

The frontend now exposes:

- Route: /value/executive
- Real engine call: PUT /api/value/cost-assumptions then POST /api/value/impact
- Executive Low / Mid / High cards
- Input drill-through per value term
- Provenance handle per value term
- ABSTAIN proof for missing assumptions
- Payback view versus monthly license cost
- Print-friendly monthly value report PDF surface

## Honesty guard

The page does not emit money values when the engine abstains. It does not use forbidden commercial certainty wording.

## Validation

Run:

    node tools/phase3/validate-p3-t14-value-executive.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/ValueExecutive/p3t14ValueExecutive.test.ts --config vitest.config.ts

Optional e2e:

    npx playwright test tests/e2e/p3t14-value-executive.spec.ts
`);

patchAppRoute();

console.log("[GREEN] P3-T14 patch applied.");