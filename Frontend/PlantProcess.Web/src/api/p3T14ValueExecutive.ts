
export const P3_T14_VALUE_ROI_EXECUTIVE_SURFACE = "P3_T14_VALUE_ROI_EXECUTIVE_SURFACE";

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
