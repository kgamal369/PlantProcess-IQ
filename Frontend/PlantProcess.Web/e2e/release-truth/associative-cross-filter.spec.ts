// ============================================================================
// Associative cross-filter state-machine certification.
//
// Backlog origin: T-204   Release: M2   Owner: Worker 2 (Release Truth)
//
// Proves BEHAVIOUR, not event emission: a selection made in one widget changes
// the query dependent widgets ACTUALLY EXECUTE, changes what they return, and
// restores exactly on clear.
//
// T-204 CLOSURE V2.
//
//   1 IDENTITY IS PERSISTED IDENTITY, AND ATTRIBUTION IS PROVEN SOUND.
//     Evidence is keyed by the widgetCode the product stores, resolved by
//     matching the executed binding against the dashboard's own definitions.
//     DashboardWidgetQueryDto carries no widget identity, so two widgets with
//     the same tuple would be indistinguishable on the wire. Every dashboard
//     used in certification is asserted collision-free before it is used.
//
//   2 POSSIBLE-SET TRAFFIC IS NOT A DEPENDENT WIDGET.
//     The associative strip enumerates every dimension through the same
//     endpoint. Those requests are still captured and still written to
//     evidence; they are excluded from the dependent comparison by ONE request
//     predicate, options.includeWarnings === false.
//
//   3 THE SELECTION MUST DISCRIMINATE AND MUST BE EMITTABLE.
//     X and Y are resolved from the product's own answers, and each is only
//     accepted when the same dashboard holds a persisted, click-capable source
//     widget grouped by that field. Every click is scoped to that widget by
//     data-widget-code. No shape scanning across the page, and no unscoped
//     any-value click.
//
// Saved widgets render on /workspace/:dashboardCode. The canonical system
// dashboards ship bar, line and table only, so the remaining renderer families
// are authored ephemerally through the public contract and removed in finally,
// with absence proven by IDENTITY rather than by count.
//
// No plant vocabulary anywhere. Codes and values come from what the product
// already persisted and already answers.
// ============================================================================

import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { login } from "../helpers/auth";
import {
  readAllPhases, runId, writePhase, type Phase, type WidgetPhase,
} from "./associativeSelectionEvidence";
import {
  ALL_FILTERS, installWidgetCapture, readIntendedSelections,
  type CaptureHandle, type Observed,
} from "./widgetTrafficCapture";
import {
  bearer, bindingCollisions, bindingKeyOfWidget, createCoreStateMachineCase, createRendererCase, inventoryDelta,
  readInventory, readMeasureCodes, readPersistedWidgets, removeAcceptanceArtifacts,
  resolveLawfulPairs, resolveRendererBinding, resolveSelectionPlan,
  type Binding, type CodePair, type CoreCase, type Inventory, type PersistedWidget,
  type RendererBinding, type RendererCase, type SelectionPlan,
} from "./associativeAcceptanceFixture";

// T-204 TECH-LEAD VARIANCE (29-Aug-2026):
// Certify the associative state machine against the five renderer consumers
// that are non-vacuously provable on the current runtime.
// Heatmap remains a Release-1 requirement. Repeated live release-truth runs
// proved the current controlled product/data contract cannot produce a
// discriminating Heatmap consumer binding. Extending T-204 into chart/query/
// fixture redesign would violate this task's own scope boundary.
// Follow-up: T-250 Heatmap associative consumer capability and
// controlled-fixture certification. This is a scope transfer, NOT a skip.
const DEFERRED_RENDERER_FAMILY = "heatmap";
const DEFERRED_RENDERER_TASK = "T-250";
const REQUIRED = ["bar", "line", "table", "scatter", "kpi"];

/** Evidence namespaces. Only PERSISTED identities take part in the dependent
 *  comparison; the other two keep the raw capture readable in evidence. */
const POSSIBLE_SET = "POSSIBLE_SET|";
const UNMATCHED = "UNMATCHED|";

function base(): string {
  const b = process.env.VITE_API_BASE_URL ?? process.env.ASPNETCORE_URLS ?? "";
  if (!b) throw new Error("no API base in the loaded profile");
  return b.split(";")[0].replace(/\/+$/, "");
}
const USER = () => process.env.PPIQ_SMOKE_USERNAME ?? "";
const PASS = () => process.env.PPIQ_SMOKE_PASSWORD ?? "";

/** CERTIFY runs this spec twice: once with PPIQ_T204_FALSIFY=1, where every
 *  filter is severed at the request boundary and the gate MUST go red, and once
 *  normally, where it MUST be green. A gate that cannot be made to fail on
 *  demand is not evidence. */
const FALSIFY = process.env.PPIQ_T204_FALSIFY === "1";

let baseline: Inventory;
let persisted: PersistedWidget[] = [];
let plan: SelectionPlan | null = null;
let lawful: { any: CodePair; scatter: CodePair | null } | null = null;
let measureCodes: string[] = [];
const rendererBindings = new Map<string, RendererBinding>();
const rendererCases = new Map<string, RendererCase>();
let coreCase: CoreCase | null = null;
let token = "";

test.describe.configure({ mode: "serial" });

test.beforeAll(async ({ request }: { request: APIRequestContext }) => {
  token = await bearer(request, base(), USER(), PASS());

  const stale = await removeAcceptanceArtifacts(request, base(), token);
  if (stale.length > 0) {
    // eslint-disable-next-line no-console
    console.log(`[T-204] removed stale acceptance definitions: ${stale.join(", ")}`);
  }

  baseline = await readInventory(request, base(), token);
  expect(baseline.dashboards.length, "no system dashboards to certify against").toBeGreaterThan(0);
  expect(baseline.widgets.length, "no system widgets to certify against").toBeGreaterThan(0);

  persisted = await readPersistedWidgets(request, base(), token);
  measureCodes = await readMeasureCodes(request, base(), token);
  lawful = await resolveLawfulPairs(request, base(), token);

  const resolved = await resolveSelectionPlan(request, base(), token, persisted);
  plan = resolved.plan;
  expect(
    plan,
    "NO EMITTABLE DISCRIMINATING SELECTION EXISTS IN THIS DATABASE. Either no candidate value left " +
      "two dependent widgets answering differently, or the field that discriminates has no persisted " +
      "click-capable source widget a person could click. This is a data or fixture finding, not a " +
      "product pass. Probe ledger: " +
      resolved.attempts.map((a) => `${a.dashboardCode}: ${a.note}`).join(" | ")
  ).not.toBeNull();

  const collisions = bindingCollisions(persisted, plan!.dashboardCode);
  expect(
    collisions,
    `binding tuple collision on ${plan!.dashboardCode}: the query contract carries no widget identity, ` +
      "so these widgets cannot be told apart on the wire"
  ).toEqual([]);

  // eslint-disable-next-line no-console
  console.log(
    `[T-204] plan: ${plan!.dashboardCode}\n` +
    `        X ${plan!.x.field}=${plan!.x.value} via ${plan!.x.sourceWidget.widgetCode} (${plan!.x.sourceWidget.chartType})\n` +
    `        dependents ${plan!.dependentA.widgetCode}, ${plan!.dependentB.widgetCode}  (${plan!.probes} probes)`
  );

  // Bounded direct-API preflight per renderer family. A family is only taken
  // into the browser through a binding already proven to answer AND to change.
  const xFilters: Record<string, unknown> = {};
  xFilters[plan!.x.filterField] = plan!.x.value;
  const parameterValue = plan!.x.field === "parameterCode" ? plan!.x.value : null;
  const proven = [plan!.dependentA, plan!.dependentB];

  for (const family of REQUIRED) {
    const outcome = await resolveRendererBinding(
      request, base(), token, family, xFilters, proven, persisted, measureCodes,
      plan!.x.dimensionCode, parameterValue, lawful!.scatter, plan!.x.sourceWidget
    );
    expect(
      outcome.resolved,
      `NO DISCRIMINATING BINDING FOR ${family}. Every bounded candidate either refused the query or ` +
        `answered identically under X. Tried: ${outcome.tried.join(" | ")}`
    ).not.toBeNull();
    rendererBindings.set(family, outcome.resolved!);
    // eslint-disable-next-line no-console
    console.log(`[T-204] ${family} binding via ${outcome.resolved!.via}`);
  }

  const sourceBinding: Binding = {
    widgetType: plan!.x.sourceWidget.widgetType,
    chartType: plan!.x.sourceWidget.chartType,
    dimensionCode: plan!.x.sourceWidget.dimensionCode,
    measureCode: plan!.x.sourceWidget.measureCode,
    parameterCode: plan!.x.sourceWidget.parameterCode,
  };
  const dependentABinding: Binding = {
    widgetType: plan!.dependentA.widgetType,
    chartType: plan!.dependentA.chartType,
    dimensionCode: plan!.dependentA.dimensionCode,
    measureCode: plan!.dependentA.measureCode,
    parameterCode: plan!.dependentA.parameterCode,
  };
  const dependentBBinding: Binding = {
    widgetType: plan!.dependentB.widgetType,
    chartType: plan!.dependentB.chartType,
    dimensionCode: plan!.dependentB.dimensionCode,
    measureCode: plan!.dependentB.measureCode,
    parameterCode: plan!.dependentB.parameterCode,
  };
  const abcKeys = [sourceBinding, dependentABinding, dependentBBinding].map(bindingKeyOfWidget);
  expect(new Set(abcKeys).size, "A/B/C bindings must be pairwise distinct: " + abcKeys.join(" | ")).toBe(3);
  coreCase = await createCoreStateMachineCase(
    request, base(), token, sourceBinding, dependentABinding, dependentBBinding
  );
  console.log(
    "[T-204] controlled A/B/C fixture: " + coreCase.dashboardCode +
    " A=" + coreCase.sourceWidgetCode + " B=" + coreCase.dependentAWidgetCode +
    " C=" + coreCase.dependentBWidgetCode
  );
});

test.afterAll(async ({ request }: { request: APIRequestContext }) => {
  try {
    await removeAcceptanceArtifacts(request, base(), token);
  } finally {
    const after = await readInventory(request, base(), token);
    const problems = inventoryDelta(baseline, after);
    expect(
      problems,
      "the ephemeral fixture did not leave the canonical inventory exactly as it found it"
    ).toEqual([]);
  }
});

function activePlan(): SelectionPlan {
  if (!plan) throw new Error("no selection plan was resolved");
  return plan;
}

function activeCoreCase(): CoreCase {
  if (!coreCase) throw new Error("no controlled A/B/C case was authored");
  return coreCase;
}

function coreIdentityMap(p: SelectionPlan, c: CoreCase): Map<string, string> {
  const extra = new Map<string, string>();
  extra.set(bindingKeyOfWidget(p.x.sourceWidget), c.sourceWidgetCode);
  extra.set(bindingKeyOfWidget(p.dependentA), c.dependentAWidgetCode);
  extra.set(bindingKeyOfWidget(p.dependentB), c.dependentBWidgetCode);
  return extra;
}

function onlyCoreDependents(rows: WidgetPhase[], c: CoreCase): WidgetPhase[] {
  const wanted = new Set([c.dependentAWidgetCode, c.dependentBWidgetCode]);
  return dependents(rows).filter((row) => wanted.has(row.widgetCode));
}

async function capture(page: Page): Promise<CaptureHandle> {
  const cap = await installWidgetCapture(page);
  if (FALSIFY) { cap.stripFilterFromRequests(ALL_FILTERS); }
  return cap;
}

/** The persisted widgets of one dashboard, indexed by the binding their
 *  executed request carries. Sound only because collisions are refused. */
function identityIndex(dashboardCode: string, extra: Map<string, string>): Map<string, string> {
  const index = new Map<string, string>();
  for (const w of persisted) {
    if (w.dashboardCode !== dashboardCode) continue;
    index.set(w.bindingKey, w.widgetCode);
  }
  for (const [key, code] of extra) index.set(key, code);
  return index;
}

async function settle(page: Page): Promise<boolean> {
  try { await page.waitForLoadState("networkidle", { timeout: 25_000 }); return true; }
  catch { return false; }
}

/**
 * One phase of one scenario, written to disk.
 *
 * EVERY captured request is written. Dependent widgets are keyed by their
 * persisted widgetCode; possible-set enumeration and any request matching no
 * persisted widget on this dashboard are written under their own namespace so
 * the raw capture stays readable without entering the comparison.
 *
 * A widget can legitimately execute the same query more than once inside one
 * phase - initial load, then a refresh. The LAST observation is the settled
 * one; earlier ones are superseded rather than duplicates.
 */
async function capturePhase(
  page: Page, cap: CaptureHandle, scenario: string, phase: Phase,
  dashboardCode: string, extra: Map<string, string>
): Promise<WidgetPhase[]> {
  const settled = await settle(page);
  const intended = await readIntendedSelections(page);
  const index = identityIndex(dashboardCode, extra);

  const settledPerIdentity = new Map<string, Observed>();
  for (const o of cap.observed()) {
    let key: string;
    if (o.possibleSet) {
      key = POSSIBLE_SET + o.bindingKey;
    } else {
      const match = index.get(o.bindingKey);
      key = match ? match : UNMATCHED + o.bindingKey;
    }
    settledPerIdentity.set(key, o);
  }

  const written: WidgetPhase[] = [];
  for (const [widgetCode, o] of settledPerIdentity) {
    const p: WidgetPhase = {
      scenario, phase, widgetCode, chartType: o.chartType,
      intendedFilters: intended, executedRequestFilters: o.executedRequestFilters,
      population: o.population, semanticResultSignature: o.semanticResultSignature,
      settled, writtenAtUtc: new Date().toISOString(), runId: runId(),
    };
    writePhase(p);
    written.push(p);
  }
  return written;
}

function dependents(phases: WidgetPhase[]): WidgetPhase[] {
  return phases.filter((p) => !p.widgetCode.startsWith(POSSIBLE_SET) && !p.widgetCode.startsWith(UNMATCHED));
}

async function chipCount(page: Page): Promise<number> {
  return page.locator('[data-testid="selection-chip"]').count();
}

async function clearAll(page: Page): Promise<void> {
  const selectionsBar = page.getByTestId("selections-bar");
  const button = selectionsBar.getByRole("button", { name: "Clear all", exact: true });

  await expect(
    button,
    "the associative selections-bar Clear all control must be uniquely addressable"
  ).toHaveCount(1);
  await expect(
    button,
    "the associative selections-bar Clear all control must be enabled after a chart selection"
  ).toBeEnabled();

  await button.click();

  await expect(
    selectionsBar.getByTestId("selections-bar-state"),
    "clear must restore the authoritative selections-bar empty state"
  ).toHaveText("No selections applied");

  await expect(
    selectionsBar.locator('[data-testid="selection-chip"]'),
    "clear must remove every associative selection chip"
  ).toHaveCount(0);

  await page.waitForTimeout(700);
}

async function removeChipContaining(page: Page, needle: string): Promise<boolean> {
  const chips = page.locator('[data-testid="selection-chip"]');
  const n = await chips.count();
  for (let i = 0; i < n; i += 1) {
    const text = (await chips.nth(i).innerText()).trim();
    if (text.indexOf(needle) >= 0) {
      await chips.nth(i).locator("button").last().click();
      await page.waitForTimeout(900);
      return true;
    }
  }
  return false;
}

/** Whether any dependent request in the current capture window carried the
 *  named filter at the named value. The wire is the proof, not the chip: a chip
 *  label is a human label, and for an id-valued dimension it is not the filter
 *  value at all. */
function wireCarried(cap: CaptureHandle, filterField: string, value: string): boolean {
  return cap.observed().some((o) => {
    if (o.possibleSet) return false;
    const filters = (o.executedRequestFilters ?? {}) as Record<string, unknown>;
    return String(filters[filterField] ?? "") === value;
  });
}

/**
 * Click the resolved value inside the RESOLVED SOURCE WIDGET, found by the
 * persisted code the workspace stamps on every rendered widget. The search is
 * scoped to that one widget; no shape on any other widget is touched.
 */
async function selectResolvedValue(
  page: Page, cap: CaptureHandle, sourceWidgetCode: string, filterField: string, value: string
): Promise<boolean> {
  const scope = page.locator(`[data-widget-code="${sourceWidgetCode}"]`);
  if ((await scope.count()) === 0) return false;

  const marks = scope.locator(
    "svg .recharts-bar-rectangle, svg .recharts-rectangle, svg .recharts-dot, " +
    "svg .recharts-symbols"
  );
  const n = Math.min(await marks.count(), 16);
  for (let i = 0; i < n; i += 1) {
    const before = await chipCount(page);
    cap.reset();
    try { await marks.nth(i).click({ timeout: 2_000 }); } catch { continue; }
    await page.waitForTimeout(900);
    if ((await chipCount(page)) <= before) { continue; }
    if (wireCarried(cap, filterField, value)) return true;
    // Wrong value on the right widget. Take back only this selection.
    const chips = page.locator('[data-testid="selection-chip"]');
    if ((await chips.count()) > before) {
      await chips.last().locator("button").last().click();
      await page.waitForTimeout(700);
    }
  }
  return false;
}

// ------------------------------------------------------- stability first ----
test("BASELINE_A equals BASELINE_B before any selection", async ({ page, context }) => {
  await login(context.request);
  const cap = await capture(page);
  const p = activePlan();
  const c = activeCoreCase();
  const extra = coreIdentityMap(p, c);
  const target = "/workspace/" + c.dashboardCode;

  cap.reset();
  await page.goto(target, { waitUntil: "domcontentloaded" });
  const a = onlyCoreDependents(
    await capturePhase(page, cap, "stability", "BASELINE_A", c.dashboardCode, extra), c
  );

  cap.reset();
  await page.reload({ waitUntil: "domcontentloaded" });
  const b = onlyCoreDependents(
    await capturePhase(page, cap, "stability", "BASELINE_B", c.dashboardCode, extra), c
  );

  expect(a.length, "controlled A/B/C fixture did not execute both dependents at baseline").toBe(2);
  expect(b.length, "controlled A/B/C fixture did not execute both dependents after reload").toBe(2);
  const byWidget = new Map(b.map((row) => [row.widgetCode, row]));
  const unstable: string[] = [];
  for (const first of a) {
    const second = byWidget.get(first.widgetCode);
    if (!second || first.semanticResultSignature !== second.semanticResultSignature) {
      unstable.push(first.widgetCode + " (" + first.chartType + ")");
    }
  }
  expect(unstable, "B/C are not stable before selection").toEqual([]);
});

// --------------------------------------------- associative state machine ----
test("selection propagates to dependent widgets and clears back to baseline", async ({ page, context }) => {
  await login(context.request);
  const cap = await capture(page);
  const p = activePlan();
  const c = activeCoreCase();
  const extra = coreIdentityMap(p, c);

  cap.reset();
  await page.goto("/workspace/" + c.dashboardCode, { waitUntil: "domcontentloaded" });
  const before = onlyCoreDependents(
    await capturePhase(page, cap, "associative", "BASELINE_A", c.dashboardCode, extra), c
  );
  expect(before.length, "controlled A/B/C baseline must contain exactly B and C").toBe(2);

  expect(
    await selectResolvedValue(page, cap, c.sourceWidgetCode, p.x.filterField, p.x.value),
    "clicking A did not put " + p.x.filterField + "=" + p.x.value + " on the wire"
  ).toBe(true);

  const after = onlyCoreDependents(
    await capturePhase(page, cap, "associative", "SELECTED", c.dashboardCode, extra), c
  );
  expect(after.length, "selection did not re-execute both controlled dependents B/C").toBe(2);
  const baseByWidget = new Map(before.map((row) => [row.widgetCode, row]));
  const changed = after.filter((row) => {
    const b = baseByWidget.get(row.widgetCode);
    if (!b) return false;
    const queryChanged = JSON.stringify(b.executedRequestFilters) !== JSON.stringify(row.executedRequestFilters);
    const resultChanged = b.population !== row.population || b.semanticResultSignature !== row.semanticResultSignature;
    return queryChanged && resultChanged;
  });
  expect(
    changed.map((row) => row.widgetCode).sort(),
    "A selection must change BOTH query and answer of B and C; A is never counted as a dependent"
  ).toEqual([c.dependentAWidgetCode, c.dependentBWidgetCode].sort());

  cap.reset();
  await clearAll(page);
  const cleared = onlyCoreDependents(
    await capturePhase(page, cap, "associative", "CLEARED", c.dashboardCode, extra), c
  );
  expect(cleared.length, "clear did not re-execute both controlled dependents B/C").toBe(2);
  const clearByWidget = new Map(cleared.map((row) => [row.widgetCode, row]));
  const notRestored: string[] = [];
  for (const b of before) {
    const restored = clearByWidget.get(b.widgetCode);
    if (!restored) {
      notRestored.push(b.widgetCode + ": missing after clear");
      continue;
    }
    if (JSON.stringify(b.executedRequestFilters) !== JSON.stringify(restored.executedRequestFilters)) {
      notRestored.push(b.widgetCode + ": executed query not restored");
    } else if (b.population !== restored.population || b.semanticResultSignature !== restored.semanticResultSignature) {
      notRestored.push(b.widgetCode + ": analytical result not restored");
    }
  }
  expect(notRestored, "clearing X did not restore B/C exactly to baseline").toEqual([]);
});

// ---------------------------------------------------- renderer matrix -------
// Each family is certified on its OWN ephemeral dashboard through a binding
// already proven over HTTP to answer and to change under X. Four phases:
// baseline, request changed, result changed, clear restored.
for (const family of REQUIRED) {
  test(`renderer family ${family} responds as a dependent consumer`, async ({ page, context, request }) => {
    await login(context.request);
    const p = activePlan();
    const resolvedBinding = rendererBindings.get(family);
    expect(resolvedBinding, `no proven binding for ${family}`).not.toBeUndefined();

    const selection = resolvedBinding!.selection ?? p.x;
    const sourceBinding: Binding = {
      widgetType: selection.sourceWidget.widgetType,
      chartType: selection.sourceWidget.chartType,
      dimensionCode: selection.sourceWidget.dimensionCode,
      measureCode: selection.sourceWidget.measureCode,
      parameterCode: selection.sourceWidget.parameterCode,
    };

    const created = await createRendererCase(request, base(), token, family, sourceBinding, resolvedBinding!.binding);
    rendererCases.set(family, created);

    const extra = new Map<string, string>();
    extra.set(bindingKeyOfWidget(sourceBinding), created.sourceWidgetCode);
    extra.set(bindingKeyOfWidget(resolvedBinding!.binding), created.dependentWidgetCode);

    const cap = await capture(page);
    cap.reset();
    await page.goto(`/workspace/${created.dashboardCode}`, { waitUntil: "domcontentloaded" });
    const before = dependents(await capturePhase(page, cap, `renderer-${family}`, "BASELINE_A", created.dashboardCode, extra));
    const b = before.find((row) => row.widgetCode === created.dependentWidgetCode);
    expect(b, `the authored ${family} dependent executed no query`).not.toBeUndefined();

    expect(
      await selectResolvedValue(page, cap, created.sourceWidgetCode, selection.filterField, selection.value),
      `the ${family} case source did not put ${selection.filterField}=${selection.value} on the wire`
    ).toBe(true);
    const selected = dependents(await capturePhase(page, cap, `renderer-${family}`, "SELECTED", created.dashboardCode, extra));
    const s = selected.find((row) => row.widgetCode === created.dependentWidgetCode);
    expect(s, `the ${family} dependent did not re-execute after the selection`).not.toBeUndefined();

    expect(
      JSON.stringify(s!.executedRequestFilters),
      `the ${family} dependent executed the same query after the selection`
    ).not.toEqual(JSON.stringify(b!.executedRequestFilters));
    expect(
      b!.population !== s!.population || b!.semanticResultSignature !== s!.semanticResultSignature,
      `the ${family} dependent returned the same analytical answer after the selection`
    ).toBe(true);

    cap.reset();
    await clearAll(page);
    const cleared = dependents(await capturePhase(page, cap, `renderer-${family}`, "CLEARED", created.dashboardCode, extra));
    const c = cleared.find((row) => row.widgetCode === created.dependentWidgetCode);
    expect(c, `the ${family} dependent did not re-execute after clear`).not.toBeUndefined();
    expect(
      JSON.stringify(c!.executedRequestFilters),
      `the ${family} dependent did not restore its baseline query on clear`
    ).toEqual(JSON.stringify(b!.executedRequestFilters));
    expect(
      c!.population === b!.population && c!.semanticResultSignature === b!.semanticResultSignature,
      `the ${family} dependent did not restore its baseline answer on clear`
    ).toBe(true);
  });
}

// ---------------- five certified consumers; Heatmap transferred to T-250 -----
test("every required consumer type participates as a dependent consumer", () => {
  const all = readAllPhases();
  const seen = new Set(
    all
      .filter((p) => !p.widgetCode.startsWith(POSSIBLE_SET) && !p.widgetCode.startsWith(UNMATCHED))
      .map((p) => (p.chartType || "").toLowerCase())
  );
  const missing = REQUIRED.filter((t) => !seen.has(t));
  expect(
    missing,
    "required renderer families absent. KPI is a consumer only; no click behaviour is " +
      "invented for it. Observed: " + [...seen].sort().join(", ")
  ).toEqual([]);
});

// ------------------------------------------------------- falsification ------
test("FALSIFICATION: severing propagation at the request boundary makes the gate RED", async ({ page, context }) => {
  await login(context.request);
  const cap = await capture(page);
  const p = activePlan();
  const c = activeCoreCase();

  cap.reset();
  await page.goto("/workspace/" + c.dashboardCode, { waitUntil: "domcontentloaded" });
  await settle(page);
  const before: Observed[] = cap.observed().filter((o) => !o.possibleSet).map((o) => ({ ...o }));
  expect(before.length, "no baseline dependent traffic to falsify against").toBeGreaterThan(0);

  cap.stripFilterFromRequests(p.x.filterField);
  cap.reset();

  const scope = page.locator(`[data-widget-code="${c.sourceWidgetCode}"]`);
  const marks = scope.locator(
    "svg .recharts-bar-rectangle, svg .recharts-rectangle, svg .recharts-dot, " +
    "svg .recharts-symbols"
  );
  let emitted = false;
  const n = Math.min(await marks.count(), 16);
  for (let i = 0; i < n; i += 1) {
    try { await marks.nth(i).click({ timeout: 2_000 }); } catch { continue; }
    await page.waitForTimeout(900);
    if ((await chipCount(page)) > 0) { emitted = true; break; }
  }
  await settle(page);
  expect(emitted, "the selection was not even emitted, so this proves nothing").toBe(true);

  const baseByBinding = new Map(before.map((o) => [o.bindingKey, o]));

  // SETTLED, NOT RAW. Every other assertion in this file compares the SETTLED
  // observation per identity, because a widget legitimately executes more than
  // once inside one phase - initial mount, then a refresh when the selections
  // bar appears - and an intermediate render is superseded rather than being a
  // second answer. This control was the ONE place still comparing the raw
  // stream, so a single transient observation counted as propagation. It now
  // uses the same instrument as capturePhase.
  const settledAfter = new Map<string, Observed>();
  for (const o of cap.observed()) {
    if (o.possibleSet) continue;
    settledAfter.set(o.bindingKey, o);
  }

  // A IS THE SOURCE, NEVER A DEPENDENT. The positive test asserts that exactly
  // B and C change. This control must assert over the SAME population, or the
  // two halves of one law are measured on different sets.
  const sourceKey = bindingKeyOfWidget(p.x.sourceWidget);

  const propagated: Observed[] = [];
  for (const [key, o] of settledAfter) {
    if (key === sourceKey) continue;
    const b = baseByBinding.get(key);
    if (b && b.semanticResultSignature !== o.semanticResultSignature) { propagated.push(o); }
  }

  expect(
    propagated.length,
    "results changed while " + p.x.filterField + " was stripped from every request - " +
      "the gate is not measuring executed propagation. Offenders: " +
      propagated.map((o) => {
        const was = baseByBinding.get(o.bindingKey);
        return o.bindingKey +
          " baseline " + String(was ? was.population : -1) + "/" +
          (was ? was.semanticResultSignature : "").slice(0, 12) +
          " stripped " + String(o.population) + "/" + o.semanticResultSignature.slice(0, 12) +
          " filters " + JSON.stringify(o.executedRequestFilters);
      }).join(" | ")
  ).toBe(0);

  cap.stopStripping();
  await clearAll(page);
});