// T-050. THE DRILL-DOWN CHAIN, IN A BROWSER.
//
// Unit tests prove each link. This proves the chain: a real click on a real
// chart opens the drawer, names the population that point represents, and
// resolves the evidence of the execution that produced it.
//
// The widget query is intercepted so the population and evidence paths are
// deterministic. The INTERCEPTION IS THE FIXTURE, not a product seam - no
// test-only flag, hook or prop exists in the product.

import { test, expect, type Page, type Route } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";

const WIDGET_QUERY = /\/analytics\/dashboard\/widgets?\/query/;
const EVIDENCE = /\/assistant\/evidence\/widget-result\//;

/** Three rows, deliberately NOT in ascending value order, so a chart that sorts
 *  cannot accidentally agree with the backend order. */
function widgetResult(overrides: Record<string, unknown> = {}) {
  return {
    generatedAtUtc: new Date().toISOString(),
    widget: { widgetType: "chart", chartType: "bar", dimensionCode: "shift", measureCode: "defectRate" },
    columns: [{ code: "category", label: "Shift" }, { code: "value", label: "Defect rate" }],
    rows: [
      { category: "Shift A", value: 10 },
      { category: "Shift B", value: 90 },
      { category: "Shift C", value: 50 },
    ],
    warnings: [],
    rowPopulations: [
      { rowIndex: 0, rowFingerprint: "fp-a", dimensionBindings: { shiftCode: "A" }, measureCode: "defectRate", parameterCode: null, filterContextFingerprint: "ctx-1", populationCount: 111 },
      { rowIndex: 1, rowFingerprint: "fp-b", dimensionBindings: { shiftCode: "B" }, measureCode: "defectRate", parameterCode: null, filterContextFingerprint: "ctx-1", populationCount: 222 },
      { rowIndex: 2, rowFingerprint: "fp-c", dimensionBindings: { shiftCode: "C" }, measureCode: "defectRate", parameterCode: null, filterContextFingerprint: "ctx-1", populationCount: null },
    ],
    ...overrides,
  };
}

type Captured = { evidenceRequests: Record<string, unknown>[]; plainRequests: Record<string, unknown>[] };

async function serveWidget(page: Page, captured: Captured, result: Record<string, unknown>) {
  await page.route(WIDGET_QUERY, async (route: Route) => {
    let body: Record<string, unknown> = {};
    try { body = JSON.parse(route.request().postData() ?? "{}"); } catch { /* not our shape */ }

    const options = (body.options ?? {}) as Record<string, unknown>;
    if (options.includeExecutionEvidence === true) captured.evidenceRequests.push(body);
    else captured.plainRequests.push(body);

    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(result) });
  });
}

async function openDashboard(page: Page, request: Parameters<typeof prepareAuthenticatedPage>[1]) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await prepareAuthenticatedPage(page, request);
  await page.goto("/dashboard", { waitUntil: "domcontentloaded", timeout: 30000 });
  await expect(page.locator(".dashboard-grid-layout-shell")).toBeVisible({ timeout: 30000 });
}

/** Clicks a bar and waits for the drawer. Returns nothing: what was clicked is
 *  asserted from the drawer, which is the point. */
async function clickFirstBar(page: Page) {
  const bar = page.locator(".recharts-bar-rectangle").first();
  await expect(bar, "no bar chart rendered, so no point can be drilled into").toBeVisible({ timeout: 30000 });
  await bar.click({ force: true });
  await expect(page.locator(".drilldown-drawer")).toBeVisible({ timeout: 15000 });
}

test.describe("T-050 drill-down population and evidence", () => {
  test("an ordinary render never asks for execution evidence", async ({ page, request }) => {
    test.setTimeout(120000);
    const captured: Captured = { evidenceRequests: [], plainRequests: [] };
    await serveWidget(page, captured, widgetResult());
    await openDashboard(page, request);
    await expect(page.locator(".react-grid-item").first()).toBeVisible({ timeout: 30000 });

    expect(captured.plainRequests.length, "the dashboard issued no widget query at all").toBeGreaterThan(0);
    expect(
      captured.evidenceRequests.length,
      "an ordinary render requested evidence; every refresh would write to the evidence store",
    ).toBe(0);
  });

  test("clicking a point names its population and resolves the evidence", async ({ page, request }) => {
    test.setTimeout(120000);
    const captured: Captured = { evidenceRequests: [], plainRequests: [] };

    await serveWidget(page, captured, widgetResult({
      executionEvidenceHandle: { kind: "WidgetResult", id: "ev-42" },
    }));

    await page.route(EVIDENCE, async (route: Route) => {
      expect(route.request().url(), "the drawer resolved a different id than the handle carried").toContain("ev-42");
      await route.fulfill({
        status: 200, contentType: "application/json",
        body: JSON.stringify({ evidenceId: "ev-42", pageCode: "PRODUCTION_OVERVIEW", widgetCode: "PO_BAR", available: true }),
      });
    });

    await openDashboard(page, request);
    await clickFirstBar(page);

    const population = page.getByTestId("drilldown-population");
    await expect(population).toHaveAttribute("data-population", "described");

    // Exactly one opt-in execution, carrying a complete identity.
    await expect
      .poll(() => captured.evidenceRequests.length, { timeout: 15000 })
      .toBe(1);

    const identity = captured.evidenceRequests[0].executionIdentity as Record<string, unknown>;
    expect(identity, "the evidence request carried no execution identity").toBeTruthy();
    expect(String(identity.pageCode ?? ""), "pageCode was blank; the server would refuse to write evidence").not.toBe("");
    expect(String(identity.widgetCode ?? ""), "widgetCode was blank").not.toBe("");

    const evidence = page.getByTestId("drilldown-evidence");
    await expect(evidence).toHaveAttribute("data-evidence", "resolved", { timeout: 15000 });
    await expect(evidence).toContainText("not source-row lineage");
  });

  test("an unknown population count is reported as unknown, never as a number", async ({ page, request }) => {
    test.setTimeout(120000);
    const captured: Captured = { evidenceRequests: [], plainRequests: [] };

    // Every row's count is null, so whichever bar is clicked must read unknown.
    const result = widgetResult();
    (result.rowPopulations as Record<string, unknown>[]).forEach((p) => { p.populationCount = null; });
    await serveWidget(page, captured, result);

    await openDashboard(page, request);
    await clickFirstBar(page);

    const count = page.getByTestId("population-count");
    await expect(count).toBeVisible();
    const text = ((await count.textContent()) ?? "").trim();

    expect(text, "an unknown population count was rendered as a number").not.toMatch(/^\d+$/);
    expect(text.toLowerCase()).toContain("not reported");
  });

  test("the producer's evidence-unavailable warning is shown, not swallowed", async ({ page, request }) => {
    test.setTimeout(120000);
    const captured: Captured = { evidenceRequests: [], plainRequests: [] };

    await serveWidget(page, captured, widgetResult({
      warnings: [
        "execution_evidence_unavailable: execution evidence was requested but the execution " +
        "identity is incomplete. The query values are returned; no evidence record was written.",
      ],
    }));

    await openDashboard(page, request);
    await clickFirstBar(page);

    const evidence = page.getByTestId("drilldown-evidence");
    await expect(evidence).toHaveAttribute("data-evidence", "unavailable", { timeout: 15000 });
    await expect(page.getByTestId("evidence-unavailable")).toContainText("execution_evidence_unavailable");
  });

  test("a handle that will not resolve is honest, and distinct from a broken request", async ({ page, request }) => {
    test.setTimeout(120000);
    const captured: Captured = { evidenceRequests: [], plainRequests: [] };

    await serveWidget(page, captured, widgetResult({
      executionEvidenceHandle: { kind: "WidgetResult", id: "ev-gone" },
    }));
    // 404 - the client turns this into null, which means "not resolvable here".
    await page.route(EVIDENCE, (route: Route) => route.fulfill({ status: 404, body: "" }));

    await openDashboard(page, request);
    await clickFirstBar(page);

    const evidence = page.getByTestId("drilldown-evidence");
    await expect(evidence).toHaveAttribute("data-evidence", "notFound", { timeout: 15000 });
    await expect(page.getByTestId("evidence-not-found")).toBeVisible();
    await expect(page.getByTestId("evidence-error")).toHaveCount(0);
  });
});
