// M1-P3 CONSOLIDATED BROWSER GATE.
//
// One walk over the finished slice, in the order a person meets it, rather than
// four separate re-certifications:
//
//   T-049  the dashboard renders a real layout, and PRODUCTION_OVERVIEW is fit
//          to show      (includes the deferred T049-VISUAL-01)
//   T-051  a broken widget stays broken inside its own cell, and every state
//          says which of the seven it is
//   T-050  a clicked point names the population it represents, asks once for
//          the evidence of the execution that drew it, and is honest about
//          each way that can be unavailable
//   T-052  no industry-specific parameter is ever invented on the customer's
//          behalf
//
// The per-task specs still own their fine-grained assertions. This one owns the
// question they cannot answer separately: does the assembled thing hold up.

import { test, expect, type Page, type Route } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";

const WIDGET_QUERY = /\/analytics\/dashboard\/widgets?\/query/;
const CORRELATION = /\/analytics\/correlations\/parameter-defect\/genealogy-aware/;
const EVIDENCE = /\/assistant\/evidence\/widget-result\//;

const GRID_ROW = 60;          // rowHeight 42 + margin 18
const VIEWPORTS = [
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "1440x900", width: 1440, height: 900 },
  { name: "1280x800", width: 1280, height: 800 },
];

async function openDashboard(page: Page, request: Parameters<typeof prepareAuthenticatedPage>[1], width = 1440, height = 900) {
  await page.setViewportSize({ width, height });
  await prepareAuthenticatedPage(page, request);
  await page.goto("/dashboard", { waitUntil: "domcontentloaded", timeout: 30000 });
  await expect(page.locator(".dashboard-grid-layout-shell")).toBeVisible({ timeout: 30000 });
  await expect(page.locator(".react-grid-item").first()).toBeVisible({ timeout: 30000 });
}

test.describe("M1-P3 consolidated acceptance", () => {

  // ---------------------------------------------------------------- T-049
  test("T049-VISUAL-01 PRODUCTION_OVERVIEW is fit to show at every viewport", async ({ page, request }) => {
    test.setTimeout(180000);

    for (const viewport of VIEWPORTS) {
      await openDashboard(page, request, viewport.width, viewport.height);

      const boxes = await page.locator(".react-grid-item").evaluateAll((nodes) =>
        nodes.map((node) => {
          const box = node.getBoundingClientRect();
          return { code: node.getAttribute("data-widget-code") ?? "", w: Math.round(box.width), h: Math.round(box.height) };
        }),
      );

      expect(boxes.length, viewport.name + ": too few widgets to certify").toBeGreaterThanOrEqual(3);

      // A board where EVERY widget is one grid row tall is not a dashboard, it
      // is a list of empty bars. This is the deferred visual acceptance, and it
      // is also what catches the PRODUCTION_OVERVIEW lg layout damaged by the
      // early T-049 runs.
      const tall = boxes.filter((b) => b.h > GRID_ROW * 2);
      expect(
        tall.length,
        viewport.name + ": every widget is one grid row tall, so nothing can actually be read. "
        + "Sizes: " + boxes.map((b) => b.code + " " + b.w + "x" + b.h).join(", "),
      ).toBeGreaterThan(0);

      // Nothing collapsed to nothing, nothing spilling off the shell.
      const shell = await page.locator(".dashboard-grid-layout-shell").evaluate((n) => Math.round(n.getBoundingClientRect().width));
      for (const box of boxes) {
        expect(box.w, viewport.name + ": " + box.code + " has no width").toBeGreaterThan(0);
        expect(box.h, viewport.name + ": " + box.code + " has no height").toBeGreaterThan(0);
        expect(box.w, viewport.name + ": " + box.code + " is wider than the grid").toBeLessThanOrEqual(shell + 4);
      }

      await page.screenshot({ path: "test-results/m1p3-production-overview-" + viewport.name + ".png", fullPage: true });
    }
  });

  // ---------------------------------------------------------------- T-051
  test("one broken widget stays broken inside its own cell", async ({ page, request }) => {
    test.setTimeout(180000);

    let firstBody: string | null = null;
    await page.route(WIDGET_QUERY, async (route: Route) => {
      const body = route.request().postData() ?? "";
      if (firstBody === null) firstBody = body;
      if (body === firstBody) {
        await route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"forced"}' });
        return;
      }
      await route.continue();
    });

    await openDashboard(page, request);

    const failed = page.locator("[data-widget-state='failed']");
    await expect(failed.first(), "no widget reported the canonical failed state").toBeVisible({ timeout: 30000 });

    const total = await page.locator(".react-grid-item").count();
    const broken = await page.locator(".react-grid-item").filter({ has: failed }).count();
    expect(broken, "the failure spread beyond its own cell").toBeLessThan(total);

    // A sibling is still usable, not merely still painted.
    const toggle = page.getByTestId("workspace-edit-toggle");
    await toggle.click();
    await expect(page.locator("[data-edit-mode='on']")).toBeVisible({ timeout: 10000 });
    await expect(page.locator(".react-grid-item .dashboard-widget__drag-handle").first()).toBeVisible();

    const text = (await failed.first().textContent()) ?? "";
    expect(text, "a raw exception reached the presentation").not.toContain("Exception");
  });

  // ---------------------------------------------------------------- T-050
  test("a clicked point names its population and resolves its evidence once", async ({ page, request }) => {
    test.setTimeout(180000);

    const evidenceRequests: unknown[] = [];
    let resolverCalls = 0;

    await page.route(WIDGET_QUERY, async (route: Route) => {
      let body: Record<string, unknown> = {};
      try { body = JSON.parse(route.request().postData() ?? "{}"); } catch { /* other shape */ }
      const options = (body.options ?? {}) as Record<string, unknown>;
      if (options.includeExecutionEvidence === true) evidenceRequests.push(body);

      await route.fulfill({
        status: 200, contentType: "application/json",
        body: JSON.stringify({
          generatedAtUtc: new Date().toISOString(),
          widget: { widgetType: "chart", chartType: "bar", dimensionCode: "shift", measureCode: "defectRate" },
          columns: [{ code: "category", label: "Shift" }, { code: "value", label: "Rate" }],
          rows: [{ category: "A", value: 10 }, { category: "B", value: 90 }, { category: "C", value: 50 }],
          warnings: [],
          rowPopulations: [
            { rowIndex: 0, rowFingerprint: "fp-a", dimensionBindings: { shiftCode: "A" }, measureCode: "defectRate", parameterCode: null, filterContextFingerprint: "ctx", populationCount: 111 },
            { rowIndex: 1, rowFingerprint: "fp-b", dimensionBindings: { shiftCode: "B" }, measureCode: "defectRate", parameterCode: null, filterContextFingerprint: "ctx", populationCount: 222 },
            { rowIndex: 2, rowFingerprint: "fp-c", dimensionBindings: { shiftCode: "C" }, measureCode: "defectRate", parameterCode: null, filterContextFingerprint: "ctx", populationCount: null },
          ],
          executionEvidenceHandle: { kind: "WidgetResult", id: "gate-ev-1" },
        }),
      });
    });

    await page.route(EVIDENCE, async (route: Route) => {
      resolverCalls += 1;
      expect(route.request().url()).toContain("gate-ev-1");
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ evidenceId: "gate-ev-1", available: true }) });
    });

    await openDashboard(page, request);
    expect(evidenceRequests.length, "an ordinary render asked for evidence").toBe(0);

    const bar = page.locator(".recharts-bar-rectangle").first();
    await expect(bar, "no bar chart rendered, so no point can be drilled into").toBeVisible({ timeout: 30000 });
    await bar.click({ force: true });

    await expect(page.locator(".drilldown-drawer")).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId("drilldown-population")).toHaveAttribute("data-population", "described");

    await expect.poll(() => evidenceRequests.length, { timeout: 15000 }).toBe(1);
    const identity = (evidenceRequests[0] as Record<string, unknown>).executionIdentity as Record<string, unknown>;
    expect(String(identity?.pageCode ?? ""), "the evidence request had no page code").not.toBe("");
    expect(String(identity?.widgetCode ?? ""), "the evidence request had no widget code").not.toBe("");

    await expect(page.getByTestId("drilldown-evidence")).toHaveAttribute("data-evidence", "resolved", { timeout: 15000 });
    await expect(page.getByTestId("drilldown-evidence")).toContainText("not source-row lineage");
    expect(resolverCalls, "the drawer resolved the evidence more than once").toBe(1);
  });

  // ---------------------------------------------------------------- T-052
  test("no industry-specific parameter is invented on the customer's behalf", async ({ page, request }) => {
    test.setTimeout(180000);

    const correlationUrls: string[] = [];
    await page.route(CORRELATION, async (route: Route) => {
      correlationUrls.push(route.request().url());
      await route.fulfill({
        status: 200, contentType: "application/json",
        body: JSON.stringify({ generatedAtUtc: new Date().toISOString(), bins: [], message: "no run" }),
      });
    });

    await page.setViewportSize({ width: 1440, height: 900 });
    await prepareAuthenticatedPage(page, request);
    await page.goto("/correlations", { waitUntil: "domcontentloaded", timeout: 30000 });

    const parameter = page.getByLabel("Parameter");
    await expect(parameter, "the correlation page has no parameter field").toBeVisible({ timeout: 30000 });
    await expect(parameter, "the page pre-selected a parameter nobody chose").toHaveValue("");

    for (const url of correlationUrls) {
      expect(url, "a parameter reached the wire before one was selected").not.toContain("parameterCode=");
      expect(url).not.toContain("CastingSpeed");
    }

    // A chosen parameter must still flow through untouched.
    correlationUrls.length = 0;
    await parameter.fill("ROLL_FORCE");
    await page.getByRole("button", { name: /run correlation/i }).click();

    await expect.poll(() => correlationUrls.length, { timeout: 15000 }).toBeGreaterThan(0);
    expect(correlationUrls.some((u) => u.includes("parameterCode=ROLL_FORCE")),
      "the selected parameter did not reach the query: " + correlationUrls.join(" | ")).toBe(true);
  });
});
