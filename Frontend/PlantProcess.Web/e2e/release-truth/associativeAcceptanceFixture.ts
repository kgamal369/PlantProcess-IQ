// ============================================================================
// Ephemeral acceptance fixture, authored through the public product contract.
//
// Backlog origin: T-204   Release: M2   Owner: Worker 2 (Release Truth)
//
// The canonical system templates ship bar, line and table only. scatter,
// heatmap and kpi are supported renderer AND authoring capabilities that no
// default template uses. To certify the associative engine across every
// renderer family, those three are authored the way a customer would author
// them: POST /analytics/dashboard/definitions/{id}/widgets.
//
// STRICT RULES, ASSERTED BY THE SPEC:
//   public API only - never a direct table write, never SQL
//   uniquely identifiable acceptance identity
//   removed in finally
//   proven absent afterwards by IDENTITY, not by count
//
// Dimension and measure codes are resolved from the product's OWN persisted
// definitions, so every code used is one the product already validated. No
// plant vocabulary appears in this file.
// ============================================================================

import type { APIRequestContext } from "@playwright/test";
import { semanticResultSignature } from "./associativeSelectionEvidence";

export const ACCEPTANCE_PREFIX = "ACCEPTANCE_ASSOCIATIVE_";

/** Scatter is measure-constrained by DashboardWidgetQuerySafetyRegistry
 *  .IsChartCompatibleWithMeasure. These are the only measures it admits. */
export const SCATTER_COMPATIBLE_MEASURES = [
  "avgParameterValue", "riskScore", "defectRate", "parameterRelationship",
];

export type WidgetIdentity = { dashboardCode: string; widgetCode: string; id: string };
export type Inventory = { dashboards: string[]; widgets: WidgetIdentity[] };

function auth(token: string) {
  return { Authorization: `Bearer ${token}`, "X-PPIQ-MFA-Verified": "true", Accept: "application/json" };
}

export async function bearer(api: APIRequestContext, base: string, user: string, pass: string): Promise<string> {
  const r = await api.post(`${base}/auth/login`, { data: { UserName: user, Password: pass } });
  if (!r.ok()) throw new Error(`fixture login failed: http ${r.status()}`);
  const j = (await r.json()) as Record<string, string>;
  const t = j.accessToken ?? j.token;
  if (!t) throw new Error("fixture login returned no bearer token");
  return t;
}

export async function readInventory(api: APIRequestContext, base: string, token: string): Promise<Inventory> {
  const r = await api.get(`${base}/analytics/dashboard/definitions`, { headers: auth(token) });
  if (!r.ok()) throw new Error(`cannot enumerate definitions: http ${r.status()}`);
  const defs = (await r.json()) as Record<string, unknown>[];
  const dashboards: string[] = [];
  const widgets: WidgetIdentity[] = [];
  for (const d of defs) {
    const code = String(d.dashboardCode ?? "");
    if (d.isActive === false) continue;
    dashboards.push(code);
    for (const w of ((d.widgets as Record<string, unknown>[]) ?? [])) {
      if (w.isActive === false) continue;
      widgets.push({ dashboardCode: code, widgetCode: String(w.widgetCode ?? ""), id: String(w.id ?? "") });
    }
  }
  dashboards.sort();
  widgets.sort((a, b) => (a.dashboardCode + a.widgetCode).localeCompare(b.dashboardCode + b.widgetCode));
  return { dashboards, widgets };
}

/** A lawful dimension/measure pair taken from a definition the product already
 *  persisted. Never a literal chosen by this test. */
export type CodePair = { dimensionCode: string; measureCode: string };

export async function resolveLawfulPairs(
  api: APIRequestContext, base: string, token: string
): Promise<{ any: CodePair; scatter: CodePair | null }> {
  const r = await api.get(`${base}/analytics/dashboard/definitions`, { headers: auth(token) });
  const defs = (await r.json()) as Record<string, unknown>[];
  const pairs: CodePair[] = [];
  for (const d of defs) {
    for (const w of ((d.widgets as Record<string, unknown>[]) ?? [])) {
      const dim = String(w.dimensionCode ?? "");
      const mea = String(w.measureCode ?? "");
      if (dim && mea) pairs.push({ dimensionCode: dim, measureCode: mea });
    }
  }
  if (pairs.length === 0) throw new Error("no persisted widget offers a lawful dimension/measure pair");
  const scatter = pairs.find((p) =>
    SCATTER_COMPATIBLE_MEASURES.some((m) => m.toLowerCase() === p.measureCode.toLowerCase())) ?? null;
  return { any: pairs[0], scatter };
}

export type Created = { definitionId: string; dashboardCode: string; widgetIds: string[] };

export async function createAcceptanceDashboard(
  api: APIRequestContext, base: string, token: string,
  pairs: { any: CodePair; scatter: CodePair | null }
): Promise<Created> {
  // ONE identity per run, shared by the dashboard and every widget it authors.
  // ux_dashboard_widget_definitions_widget_code_active is UNIQUE(widget_code)
  // WHERE is_deleted = false - global, not dashboard-scoped. Deleting a fixture
  // clears is_active but leaves is_deleted false, so a fixed widget code
  // collides with the previous run's row and surfaces as HTTP 500. The
  // dashboard code was already unique per run; the widget codes were not.
  //
  // Per run, not per phase: BASELINE_A, BASELINE_B, SELECTED and CLEARED must
  // all address the same authored widgets.
  const runIdentity = String(Date.now());
  const dashboardCode = `${ACCEPTANCE_PREFIX}${runIdentity}`;
  const created = await api.post(`${base}/analytics/dashboard/definitions`, {
    headers: auth(token),
    data: {
      dashboardCode, name: "Associative acceptance fixture (ephemeral)",
      description: "Created through the public authoring contract by the associative certification. Removed after the run.",
      layoutJson: "{}", isDefault: false, isSystemTemplate: false, isSynthetic: true,
    },
  });
  if (!created.ok()) throw new Error(`cannot author acceptance dashboard: http ${created.status()}`);
  const definitionId = String(((await created.json()) as Record<string, unknown>).id ?? "");
  if (!definitionId) throw new Error("authoring returned no definition id");

  const wanted: { chartType: string; widgetType: string; pair: CodePair }[] = [
    { chartType: "kpi",     widgetType: "kpi",   pair: pairs.any },
    { chartType: "heatmap", widgetType: "chart", pair: pairs.any },
  ];
  if (pairs.scatter) wanted.push({ chartType: "scatter", widgetType: "chart", pair: pairs.scatter });

  const widgetIds: string[] = [];
  for (const w of wanted) {
    const res = await api.post(`${base}/analytics/dashboard/definitions/${definitionId}/widgets`, {
      headers: auth(token),
      data: {
        widgetCode: `${ACCEPTANCE_PREFIX}${w.chartType.toUpperCase()}_${runIdentity}`,
        widgetTitle: `Acceptance ${w.chartType}`,
        widgetType: w.widgetType, chartType: w.chartType,
        dimensionCode: w.pair.dimensionCode, measureCode: w.pair.measureCode,
        filterJson: "{}", layoutJson: "{}", displayOptionsJson: "{}", sortOrder: 0, isSynthetic: true,
      },
    });
    if (!res.ok()) {
      throw new Error(
        `BLOCKED BY AUTHORING CONTRACT: ${w.chartType} - renderer supports it but the public ` +
        `authoring contract refused it with http ${res.status()}: ${(await res.text()).slice(0, 300)}`
      );
    }
    widgetIds.push(String(((await res.json()) as Record<string, unknown>).id ?? ""));
  }
  return { definitionId, dashboardCode, widgetIds };
}

/** Public-API cleanup. Never SQL. Safe to call on a partially created fixture. */
export async function removeAcceptanceArtifacts(
  api: APIRequestContext, base: string, token: string
): Promise<string[]> {
  const removed: string[] = [];
  const r = await api.get(`${base}/analytics/dashboard/definitions`, { headers: auth(token) });
  if (!r.ok()) return removed;
  const defs = (await r.json()) as Record<string, unknown>[];
  for (const d of defs) {
    const code = String(d.dashboardCode ?? "");
    if (!code.startsWith(ACCEPTANCE_PREFIX)) continue;
    const id = String(d.id ?? "");
    for (const w of ((d.widgets as Record<string, unknown>[]) ?? [])) {
      await api.delete(`${base}/analytics/dashboard/definitions/${id}/widgets/${String(w.id)}`, { headers: auth(token) });
    }
    await api.delete(`${base}/analytics/dashboard/definitions/${id}`, { headers: auth(token) });
    removed.push(code);
  }
  return removed;
}

export function inventoryDelta(before: Inventory, after: Inventory): string[] {
  const problems: string[] = [];
  const bd = before.dashboards.join("|"), ad = after.dashboards.join("|");
  if (bd !== ad) problems.push(`dashboard identity set changed:\n  before ${bd}\n  after  ${ad}`);
  const bw = before.widgets.map((w) => `${w.dashboardCode}/${w.widgetCode}#${w.id}`).join("|");
  const aw = after.widgets.map((w) => `${w.dashboardCode}/${w.widgetCode}#${w.id}`).join("|");
  if (bw !== aw) problems.push("widget identity set changed");
  const stale = after.dashboards.filter((c) => c.startsWith(ACCEPTANCE_PREFIX));
  if (stale.length > 0) problems.push(`acceptance definitions remain: ${stale.join(", ")}`);
  return problems;
}

// ============================================================================
// T-204 CLOSURE V2. Everything above is unchanged; resolveLawfulPairs is
// retained and is still the compatibility authority for scatter.
//
// WHAT V2 ADDS, AND THE MEASURED REASONS.
//
//   readPersistedWidgets / bindingCollisions
//     DashboardWidgetQueryDto carries no widgetId and no widgetCode, so a
//     request can only be attributed to a widget by its binding tuple. That is
//     valid ONLY when the dashboard holds one widget per tuple. Collisions are
//     detected and refused rather than silently overwriting a Map key.
//
//   resolveSelectionPlan
//     A filter that changes two answers over HTTP is not yet a filter a person
//     can emit. X and Y are only accepted when the SAME dashboard holds a
//     persisted, click-capable source widget grouped by that field, so the
//     browser proof clicks a real value in a real widget rather than scanning
//     shapes.
//
//   resolveRendererBinding
//     DashboardChartGrammar.Evaluate runs at query time with
//     HasSecondCategoricalAxis: false for every one-dimension aggregate
//     binding. A heatmap on such a binding is refused STRUCTURALLY - no filter
//     or dimension fixes it. Measures whose source declares its own columns
//     return before that gate, which is the product's current authority for a
//     two-axis heatmap and for a numeric scatter. Nothing here assumes which
//     measure that is: every candidate is executed against the live API and
//     only a binding that answers AND discriminates is used.
//
// NO CANDIDATE VALUE AND NO MEASURE CODE IS HARDCODED. gradeOrRecipe stays out:
// it has no field in DashboardWidgetFiltersDto, so a selection on it cannot
// reach the engine.
// ============================================================================

export type PersistedWidget = {
  dashboardCode: string;
  widgetCode: string;
  id: string;
  widgetType: string;
  chartType: string;
  dimensionCode: string;
  measureCode: string;
  parameterCode: string | null;
  bindingKey: string;
};

export type Binding = {
  widgetType: string;
  chartType: string;
  dimensionCode: string;
  measureCode: string;
  parameterCode: string | null;
};

export function bindingKeyOfWidget(w: {
  widgetType?: unknown; chartType?: unknown; dimensionCode?: unknown;
  measureCode?: unknown; parameterCode?: unknown;
}): string {
  const part = (value: unknown): string => (value === null || value === undefined ? "" : String(value));
  return [
    part(w.widgetType), part(w.chartType), part(w.dimensionCode),
    part(w.measureCode), part(w.parameterCode),
  ].join("|");
}

/** Renderers that expose a governed clickable value. KPI is a consumer only:
 *  it has no grouping axis, so no click behaviour is invented for it. */
export const CLICKABLE_RENDERERS = ["bar", "line", "pie", "donut"];

export async function readPersistedWidgets(
  api: APIRequestContext, base: string, token: string
): Promise<PersistedWidget[]> {
  const r = await api.get(`${base}/analytics/dashboard/definitions`, { headers: auth(token) });
  if (!r.ok()) throw new Error(`cannot enumerate definitions: http ${r.status()}`);
  const defs = (await r.json()) as Record<string, unknown>[];
  const out: PersistedWidget[] = [];
  for (const d of defs) {
    if (d.isActive === false) continue;
    const dashboardCode = String(d.dashboardCode ?? "");
    for (const w of ((d.widgets as Record<string, unknown>[]) ?? [])) {
      if (w.isActive === false) continue;
      out.push({
        dashboardCode,
        widgetCode: String(w.widgetCode ?? ""),
        id: String(w.id ?? ""),
        widgetType: String(w.widgetType ?? ""),
        chartType: String(w.chartType ?? ""),
        dimensionCode: String(w.dimensionCode ?? ""),
        measureCode: String(w.measureCode ?? ""),
        parameterCode: w.parameterCode === null || w.parameterCode === undefined ? null : String(w.parameterCode),
        bindingKey: bindingKeyOfWidget(w),
      });
    }
  }
  out.sort((a, b) => (a.dashboardCode + "/" + a.widgetCode).localeCompare(b.dashboardCode + "/" + b.widgetCode));
  return out;
}

/** Binding tuples that more than one widget on the same dashboard would emit.
 *  Attribution by binding is only sound when this is empty. */
export function bindingCollisions(widgets: PersistedWidget[], dashboardCode: string): string[] {
  const seen = new Map<string, string[]>();
  for (const w of widgets) {
    if (w.dashboardCode !== dashboardCode) continue;
    const list = seen.get(w.bindingKey) ?? [];
    list.push(w.widgetCode);
    seen.set(w.bindingKey, list);
  }
  const collisions: string[] = [];
  for (const [key, codes] of seen) {
    if (codes.length > 1) collisions.push(`${key} -> ${codes.sort().join(", ")}`);
  }
  return collisions.sort();
}

/** The options the saved-widget render path sends. Mirrored so a probe executes
 *  the same query the browser will execute, not a different one. */
const RENDER_OPTIONS = { maxRows: 100, rawRowLimit: 500, sortDirection: "desc", includeWarnings: true };

export const MAX_CANDIDATE_VALUES_PER_FIELD = 8;
export const MAX_RENDERER_CANDIDATES = 24;

/** The candidate fields, in probe order. A field is a candidate only when
 *  DashboardWidgetFiltersDto can carry it. */
export const DISCRIMINATING_CANDIDATE_FIELDS: ReadonlyArray<{
  field: string; dimensionCode: string; filterField: string; guidValued: boolean;
}> = [
  { field: "parameterCode", dimensionCode: "parameterCode", filterField: "parameterCode", guidValued: false },
  { field: "materialUnitType", dimensionCode: "materialUnitType", filterField: "materialUnitType", guidValued: false },
  { field: "site", dimensionCode: "site", filterField: "siteId", guidValued: true },
  { field: "sourceSystem", dimensionCode: "sourceSystem", filterField: "sourceSystem", guidValued: false },
];

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type Answer = { ok: boolean; status: number; population: number; semanticResultSignature: string };

export async function executeBinding(
  api: APIRequestContext, base: string, token: string,
  binding: Binding, filters: Record<string, unknown> | null
): Promise<Answer> {
  const r = await api.post(`${base}/analytics/dashboard/widgets/query`, {
    headers: auth(token),
    data: {
      widgetType: binding.widgetType, chartType: binding.chartType,
      dimensionCode: binding.dimensionCode, measureCode: binding.measureCode,
      parameterCode: binding.parameterCode, filters, options: RENDER_OPTIONS,
    },
  });
  if (!r.ok()) return { ok: false, status: r.status(), population: -1, semanticResultSignature: "" };
  const parsed = (await r.json()) as Record<string, unknown>;
  const rows = Array.isArray(parsed.rows) ? (parsed.rows as unknown[]) : [];
  return {
    ok: true, status: r.status(), population: rows.length,
    semanticResultSignature: semanticResultSignature(binding.chartType, rows),
  };
}

function answerChanged(before: Answer, after: Answer): boolean {
  if (!before.ok || !after.ok) return false;
  return before.population !== after.population ||
         before.semanticResultSignature !== after.semanticResultSignature;
}

/** The values a source widget actually renders, read back from the product's
 *  own engine using that widget's own binding. Never a literal. */
async function enumerateValues(
  api: APIRequestContext, base: string, token: string, source: PersistedWidget
): Promise<string[]> {
  const r = await api.post(`${base}/analytics/dashboard/widgets/query`, {
    headers: auth(token),
    data: {
      widgetType: source.widgetType, chartType: source.chartType,
      dimensionCode: source.dimensionCode, measureCode: source.measureCode,
      parameterCode: source.parameterCode, filters: null, options: RENDER_OPTIONS,
    },
  });
  if (!r.ok()) return [];
  const parsed = (await r.json()) as Record<string, unknown>;
  const rows = (Array.isArray(parsed.rows) ? parsed.rows : []) as Record<string, unknown>[];
  const seen: string[] = [];
  for (const row of rows) {
    const value = String(row[source.dimensionCode] ?? "");
    if (value === "") continue;
    if (seen.indexOf(value) < 0) seen.push(value);
  }
  seen.sort((a, b) => a.localeCompare(b));
  return seen;
}

export type SelectionLeg = {
  field: string;
  filterField: string;
  dimensionCode: string;
  value: string;
  sourceWidget: PersistedWidget;
};

export type SelectionPlan = {
  dashboardCode: string;
  x: SelectionLeg;
  dependentA: PersistedWidget;
  dependentB: PersistedWidget;
  probes: number;
};

export type PlanAttempt = { dashboardCode: string; note: string };

/**
 * ONE bounded resolver.
 *
 * Bound: candidate dashboards x 4 filterable fields x the first 8 values the
 * source widget renders. The first value where TWO of the dashboard's own
 * persisted widgets change population OR semanticResultSignature becomes X, and
 * probing stops. Y is then resolved on the same dashboard from a DIFFERENT
 * field with its own click-capable source, and must coexist with X.
 *
 * The dashboard is resolved rather than assumed. Picking the dashboard with the
 * most widgets is deterministic in the wrong way: three canonical dashboards
 * tie at three and the code-sorted tiebreak always returned the data-quality
 * one, whose measure answers identically whatever is selected on this data.
 */
export async function resolveSelectionPlan(
  api: APIRequestContext, base: string, token: string, widgets: PersistedWidget[]
): Promise<{ plan: SelectionPlan | null; attempts: PlanAttempt[] }> {
  const attempts: PlanAttempt[] = [];
  let probes = 0;
  const persisted = widgets.filter((w) => !w.dashboardCode.startsWith(ACCEPTANCE_PREFIX));

  const byDashboard = new Map<string, PersistedWidget[]>();
  for (const w of persisted) {
    const list = byDashboard.get(w.dashboardCode) ?? [];
    list.push(w);
    byDashboard.set(w.dashboardCode, list);
  }

  // Baseline every UNIQUE binding once. T-204 says A emits X and B/C are
  // dependents; therefore the source binding is explicitly excluded from B/C.
  // B/C may originate from any persisted definition because the browser proof
  // authors one controlled public-contract A/B/C dashboard for certification.
  const representativeByBinding = new Map<string, PersistedWidget>();
  const baselineByBinding = new Map<string, Answer>();
  for (const w of persisted) {
    if (representativeByBinding.has(w.bindingKey)) continue;
    representativeByBinding.set(w.bindingKey, w);
    baselineByBinding.set(w.bindingKey, await executeBinding(api, base, token, w, null));
    probes += 1;
  }

  for (const dashboardCode of [...byDashboard.keys()].sort()) {
    const own = byDashboard.get(dashboardCode) ?? [];
    for (const candidate of DISCRIMINATING_CANDIDATE_FIELDS) {
      const sources = own.filter(
        (w) =>
          w.dimensionCode === candidate.dimensionCode &&
          CLICKABLE_RENDERERS.indexOf((w.chartType || "").toLowerCase()) >= 0
      );
      if (sources.length === 0) {
        attempts.push({ dashboardCode, note: candidate.field + ": no CURRENTLY EMITTABLE source" });
        continue;
      }

      for (const source of sources) {
        const values = (await enumerateValues(api, base, token, source))
          .filter((v) => (candidate.guidValued ? GUID.test(v) : true))
          .slice(0, MAX_CANDIDATE_VALUES_PER_FIELD);
        probes += 1;
        if (values.length === 0) {
          attempts.push({ dashboardCode, note: candidate.field + ": source " + source.widgetCode + " renders no usable value" });
          continue;
        }

        const sourceKey = bindingKeyOfWidget(source);
        for (const value of values) {
          const filters: Record<string, unknown> = {};
          filters[candidate.filterField] = value;
          const changedByBinding = new Map<string, PersistedWidget>();

          for (const [bindingKey, w] of representativeByBinding) {
            if (bindingKey === sourceKey) continue; // A can never be B or C.
            const before = baselineByBinding.get(bindingKey);
            if (!before || !before.ok) continue;
            const after = await executeBinding(api, base, token, w, filters);
            probes += 1;
            if (after.ok && answerChanged(before, after)) changedByBinding.set(bindingKey, w);
          }

          const changed = [...changedByBinding.values()];
          attempts.push({
            dashboardCode,
            note: candidate.field + "=" + value + " via " + source.widgetCode +
              " changed " + changed.length + " DISTINCT dependent binding(s); source A excluded",
          });

          if (changed.length >= 2) {
            const dependentA = changed[0];
            const dependentB = changed[1];
            const aKey = bindingKeyOfWidget(dependentA);
            const bKey = bindingKeyOfWidget(dependentB);
            if (aKey === sourceKey || bKey === sourceKey || aKey === bKey) {
              throw new Error("internal A/B/C invariant violated: bindings are not pairwise distinct");
            }
            return {
              plan: {
                dashboardCode,
                x: {
                  field: candidate.field,
                  filterField: candidate.filterField,
                  dimensionCode: candidate.dimensionCode,
                  value,
                  sourceWidget: source,
                },
                dependentA,
                dependentB,
                probes,
              },
              attempts,
            };
          }
        }
      }
    }
  }
  return { plan: null, attempts };
}

export type RendererBinding = {
  family: string;
  binding: Binding;
  via: string;
  selection: SelectionLeg | null;
};

const NATIVE_SCATTER_MEASURE = "parameterRelationship";
const PARAMETER_SOURCE_MEASURE = "avgParameterValue";

function ephemeralSource(binding: Binding): PersistedWidget {
  return {
    dashboardCode: "",
    widgetCode: "",
    id: "",
    widgetType: binding.widgetType,
    chartType: binding.chartType,
    dimensionCode: binding.dimensionCode,
    measureCode: binding.measureCode,
    parameterCode: binding.parameterCode,
    bindingKey: bindingKeyOfWidget(binding),
  };
}

export async function readParameterCodes(
  api: APIRequestContext, base: string, token: string
): Promise<string[]> {
  const r = await api.get(`${base}/process/parameters/definitions`, { headers: auth(token) });
  if (!r.ok()) return [];
  const parsed = await r.json() as unknown;
  const rows = Array.isArray(parsed) ? parsed as Record<string, unknown>[] : [];
  const out: string[] = [];
  for (const row of rows) {
    const code = String(row.parameterCode ?? row.ParameterCode ?? "").trim();
    if (code && !out.includes(code)) out.push(code);
  }
  out.sort((a, b) => a.localeCompare(b));
  return out.slice(0, 64);
}

/**
 * Resolve one renderer through the actual product grammar.
 *
 * - bar/line/table: ordinary persisted/published one-axis bindings under global X.
 * - kpi: dimensionless by contract; first published measure whose answer changes.
 * - heatmap: transferred to T-250; this certification must not redesign its query/fixture contract.
 * - scatter: native parameterRelationship; X is its persisted parameter and Y is
 *   a dynamically discovered parameterCode selection emitted by an ephemeral
 *   BAR source. parameterCode+avgParameterValue is published as bar-compatible;
 *   no customer/data value is hardcoded.
 */
export async function resolveRendererBinding(
  api: APIRequestContext, base: string, token: string,
  family: string,
  xFilters: Record<string, unknown>,
  proven: PersistedWidget[],
  persisted: PersistedWidget[],
  measureCodes: string[],
  selectionDimension: string,
  parameterValue: string | null,
  scatterPair: CodePair | null,
  selectionSource: PersistedWidget
): Promise<{ resolved: RendererBinding | null; tried: string[] }> {
  void scatterPair;
  const tried: string[] = [];

  if (family === "scatter") {
    const parameters = await readParameterCodes(api, base, token);
    for (const xParameter of parameters) {
      for (const yParameter of parameters) {
        if (xParameter === yParameter) continue;

        const sourceBinding: Binding = {
          widgetType: "chart",
          chartType: "bar",
          dimensionCode: "parameterCode",
          measureCode: PARAMETER_SOURCE_MEASURE,
          parameterCode: yParameter,
        };
        const sourceAnswer = await executeBinding(api, base, token, sourceBinding, null);
        if (!sourceAnswer.ok) {
          tried.push(`scatter source ${yParameter} http ${sourceAnswer.status}`);
          continue;
        }
        const rendered = await enumerateValues(api, base, token, ephemeralSource(sourceBinding));
        if (!rendered.includes(yParameter)) {
          tried.push(`scatter source ${yParameter} did not render its own parameterCode`);
          continue;
        }

        const binding: Binding = {
          widgetType: "chart",
          chartType: "scatter",
          dimensionCode: "",
          measureCode: NATIVE_SCATTER_MEASURE,
          parameterCode: xParameter,
        };
        const before = await executeBinding(api, base, token, binding, null);
        if (!before.ok) {
          tried.push(`scatter X=${xParameter} baseline http ${before.status}`);
          continue;
        }
        const selectedFilters: Record<string, unknown> = { parameterCode: yParameter };
        const after = await executeBinding(api, base, token, binding, selectedFilters);
        if (!after.ok) {
          tried.push(`scatter X=${xParameter} Y=${yParameter} selected http ${after.status}`);
          continue;
        }
        if (!answerChanged(before, after)) {
          tried.push(`scatter X=${xParameter} Y=${yParameter} answered identically`);
          continue;
        }

        const selection: SelectionLeg = {
          field: "parameterCode",
          filterField: "parameterCode",
          dimensionCode: "parameterCode",
          value: yParameter,
          sourceWidget: ephemeralSource(sourceBinding),
        };
        return {
          resolved: {
            family,
            binding,
            via: `native ${NATIVE_SCATTER_MEASURE}; X=${xParameter}; dynamic Y source=${yParameter}`,
            selection,
          },
          tried,
        };
      }
    }
    return { resolved: null, tried };
  }

  if (family === "kpi") {
    const parameters = await readParameterCodes(api, base, token);
    for (const measureCode of measureCodes) {
      const parameterChoices: Array<string | null> = [null, ...parameters];
      for (const candidateParameter of parameterChoices) {
        const binding: Binding = {
          widgetType: "kpi",
          chartType: "kpi",
          dimensionCode: "",
          measureCode,
          parameterCode: candidateParameter,
        };
        const before = await executeBinding(api, base, token, binding, null);
        if (!before.ok) {
          tried.push(`kpi ${measureCode}/${candidateParameter ?? "-"} baseline http ${before.status}`);
          continue;
        }
        const after = await executeBinding(api, base, token, binding, xFilters);
        if (!after.ok) {
          tried.push(`kpi ${measureCode}/${candidateParameter ?? "-"} selected http ${after.status}`);
          continue;
        }
        if (!answerChanged(before, after)) {
          tried.push(`kpi ${measureCode}/${candidateParameter ?? "-"} answered identically`);
          continue;
        }
        return {
          resolved: {
            family,
            binding,
            via: `dimensionless KPI product grammar: ${measureCode}`,
            selection: null,
          },
          tried,
        };
      }
    }
    return { resolved: null, tried };
  }

  const candidates: { binding: Binding; via: string }[] = [];
  const selectionSourceKey = bindingKeyOfWidget(selectionSource);
  const push = (dimensionCode: string, measureCode: string, parameterCode: string | null, via: string) => {
    if (!dimensionCode || !measureCode) return;
    const binding: Binding = {
      widgetType: "chart",
      chartType: family,
      dimensionCode,
      measureCode,
      parameterCode,
    };
    const key = bindingKeyOfWidget(binding);
    if (key === selectionSourceKey) {
      tried.push("renderer " + family + " refused source-as-dependent collision " + key);
      return;
    }
    if (candidates.some((c) => bindingKeyOfWidget(c.binding) === key)) return;
    candidates.push({ binding, via });
  };

  for (const w of proven) {
    push(w.dimensionCode, w.measureCode, w.parameterCode, `proven dependent ${w.widgetCode}`);
  }
  for (const w of persisted) {
    push(w.dimensionCode, w.measureCode, w.parameterCode, `persisted binding ${w.widgetCode}`);
  }
  for (const m of measureCodes) {
    push(selectionDimension, m, parameterValue, `published measure ${m} on selection dimension`);
    push(selectionDimension, m, null, `published measure ${m} on selection dimension, no parameter`);
  }

  for (const candidate of candidates.slice(0, MAX_RENDERER_CANDIDATES)) {
    const before = await executeBinding(api, base, token, candidate.binding, null);
    if (!before.ok) {
      tried.push(`${bindingKeyOfWidget(candidate.binding)} baseline http ${before.status}`);
      continue;
    }
    const after = await executeBinding(api, base, token, candidate.binding, xFilters);
    if (!after.ok) {
      tried.push(`${bindingKeyOfWidget(candidate.binding)} selected http ${after.status}`);
      continue;
    }
    if (!answerChanged(before, after)) {
      tried.push(`${bindingKeyOfWidget(candidate.binding)} answers identically under X`);
      continue;
    }
    return {
      resolved: {
        family,
        binding: candidate.binding,
        via: candidate.via,
        selection: null,
      },
      tried,
    };
  }

  return { resolved: null, tried };
}

/** The measure codes the product publishes. Read, never listed here. */
export async function readMeasureCodes(
  api: APIRequestContext, base: string, token: string
): Promise<string[]> {
  const r = await api.get(`${base}/analytics/dashboard/metadata`, { headers: auth(token) });
  if (!r.ok()) return [];
  const parsed = (await r.json()) as Record<string, unknown>;
  const measures = (Array.isArray(parsed.measures) ? parsed.measures : []) as Record<string, unknown>[];
  const codes: string[] = [];
  for (const m of measures) {
    const code = String(m.code ?? "");
    if (code !== "" && codes.indexOf(code) < 0) codes.push(code);
  }
  codes.sort((a, b) => a.localeCompare(b));
  return codes;
}

export type CoreCase = {
  definitionId: string;
  dashboardCode: string;
  sourceWidgetCode: string;
  dependentAWidgetCode: string;
  dependentBWidgetCode: string;
  widgetIds: string[];
};

export async function createCoreStateMachineCase(
  api: APIRequestContext, base: string, token: string,
  source: Binding, dependentA: Binding, dependentB: Binding
): Promise<CoreCase> {
  const sourceKey = bindingKeyOfWidget(source);
  const bKey = bindingKeyOfWidget(dependentA);
  const cKey = bindingKeyOfWidget(dependentB);
  if (sourceKey === bKey || sourceKey === cKey || bKey === cKey) {
    throw new Error(
      "A/B/C BINDING COLLISION: source=" + sourceKey + " B=" + bKey + " C=" + cKey +
      ". Backlog T-204 requires one emitter and TWO distinct dependent widgets."
    );
  }

  const runIdentity = "CORE_" + Date.now();
  const dashboardCode = ACCEPTANCE_PREFIX + runIdentity;
  const created = await api.post(base + "/analytics/dashboard/definitions", {
    headers: auth(token),
    data: {
      dashboardCode,
      name: "Associative A/B/C state-machine fixture (ephemeral)",
      description: "Public-contract controlled fixture for T-204. Removed after the run.",
      layoutJson: "{}", isDefault: false, isSystemTemplate: false, isSynthetic: true,
    },
  });
  if (!created.ok()) throw new Error("cannot author A/B/C dashboard: http " + created.status());
  const definitionId = String(((await created.json()) as Record<string, unknown>).id ?? "");
  if (!definitionId) throw new Error("A/B/C authoring returned no definition id");

  const sourceWidgetCode = ACCEPTANCE_PREFIX + "SRC_" + runIdentity;
  const dependentAWidgetCode = ACCEPTANCE_PREFIX + "B_" + runIdentity;
  const dependentBWidgetCode = ACCEPTANCE_PREFIX + "C_" + runIdentity;
  const wanted = [
    { code: sourceWidgetCode, binding: source, title: "Acceptance source A" },
    { code: dependentAWidgetCode, binding: dependentA, title: "Acceptance dependent B" },
    { code: dependentBWidgetCode, binding: dependentB, title: "Acceptance dependent C" },
  ];
  const widgetIds: string[] = [];
  for (const w of wanted) {
    const res = await api.post(base + "/analytics/dashboard/definitions/" + definitionId + "/widgets", {
      headers: auth(token),
      data: {
        widgetCode: w.code, widgetTitle: w.title,
        widgetType: w.binding.widgetType, chartType: w.binding.chartType,
        dimensionCode: w.binding.dimensionCode, measureCode: w.binding.measureCode,
        parameterCode: w.binding.parameterCode,
        filterJson: "{}", layoutJson: "{}", displayOptionsJson: "{}",
        sortOrder: widgetIds.length, isSynthetic: true,
      },
    });
    if (!res.ok()) {
      throw new Error(
        "A/B/C authoring refused " + w.code + " (" + bindingKeyOfWidget(w.binding) +"): http " +
        res.status() + ": " + (await res.text()).slice(0, 300)
      );
    }
    widgetIds.push(String(((await res.json()) as Record<string, unknown>).id ?? ""));
  }
  return { definitionId, dashboardCode, sourceWidgetCode, dependentAWidgetCode, dependentBWidgetCode, widgetIds };
}

export type RendererCase = {
  definitionId: string;
  dashboardCode: string;
  sourceWidgetCode: string;
  dependentWidgetCode: string;
  widgetIds: string[];
};

/**
 * ONE renderer family on its own tiny dashboard: the resolved source binding,
 * and ONE dependent of the family through its proven binding.
 *
 * The two bindings MUST differ. Because the query contract carries no widget
 * identity, two widgets with the same tuple are indistinguishable on the wire
 * and the evidence for one would silently overwrite the other.
 */
export async function createRendererCase(
  api: APIRequestContext, base: string, token: string,
  family: string, source: Binding, dependent: Binding
): Promise<RendererCase> {
  if (bindingKeyOfWidget(source) === bindingKeyOfWidget(dependent)) {
    throw new Error(
      `BINDING COLLISION in the ${family} case: the source and the dependent would emit the identical ` +
      `request tuple ${bindingKeyOfWidget(source)}, so their traffic cannot be told apart. Refused.`
    );
  }

  const runIdentity = `${family.toUpperCase()}_${Date.now()}`;
  const dashboardCode = `${ACCEPTANCE_PREFIX}${runIdentity}`;
  const created = await api.post(`${base}/analytics/dashboard/definitions`, {
    headers: auth(token),
    data: {
      dashboardCode, name: `Associative renderer case: ${family} (ephemeral)`,
      description: "Created through the public authoring contract by the associative certification. Removed after the run.",
      layoutJson: "{}", isDefault: false, isSystemTemplate: false, isSynthetic: true,
    },
  });
  if (!created.ok()) throw new Error(`cannot author renderer case ${family}: http ${created.status()}`);
  const definitionId = String(((await created.json()) as Record<string, unknown>).id ?? "");
  if (!definitionId) throw new Error(`authoring returned no definition id for ${family}`);

  const sourceWidgetCode = `${ACCEPTANCE_PREFIX}SRC_${runIdentity}`;
  const dependentWidgetCode = `${ACCEPTANCE_PREFIX}DEP_${runIdentity}`;
  const wanted = [
    { code: sourceWidgetCode, binding: source, title: `Acceptance source ${source.chartType}` },
    { code: dependentWidgetCode, binding: dependent, title: `Acceptance ${family}` },
  ];

  const widgetIds: string[] = [];
  for (const w of wanted) {
    const res = await api.post(`${base}/analytics/dashboard/definitions/${definitionId}/widgets`, {
      headers: auth(token),
      data: {
        widgetCode: w.code, widgetTitle: w.title,
        widgetType: w.binding.widgetType, chartType: w.binding.chartType,
        dimensionCode: w.binding.dimensionCode, measureCode: w.binding.measureCode,
        parameterCode: w.binding.parameterCode,
        filterJson: "{}", layoutJson: "{}", displayOptionsJson: "{}", sortOrder: widgetIds.length, isSynthetic: true,
      },
    });
    if (!res.ok()) {
      throw new Error(
        `BLOCKED BY AUTHORING CONTRACT: ${w.binding.chartType} - the query engine answers this binding but the ` +
        `public authoring contract refused it with http ${res.status()}: ${(await res.text()).slice(0, 300)}`
      );
    }
    widgetIds.push(String(((await res.json()) as Record<string, unknown>).id ?? ""));
  }

  return { definitionId, dashboardCode, sourceWidgetCode, dependentWidgetCode, widgetIds };
}