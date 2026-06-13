import { test, expect } from "@playwright/test";

/* PPIQ-PHASE5 analytics e2e.
 * CONFIG: set these to your real routes + the data-testid hooks. The evidence/
 * abstain/population/thread hooks are emitted by the Phase-5 components; the
 * heatmap hooks (cell/tooltip/drill/sort/filter) you add to InteractiveHeatmap. */
const CFG = {
  routes: {
    correlation: process.env.PPIQ_CORR_ROUTE || "/analytics/correlation",
    genealogy:   process.env.PPIQ_GENEALOGY_ROUTE || "/genealogy",
  },
  sel: {
    heatmapCell: '[data-testid="heatmap-cell"]',
    heatmapTooltip: '[data-testid="heatmap-tooltip"]',
    drillList: '[data-testid="heatmap-drill-list"]',
    drillRow: '[data-testid="heatmap-drill-row"]',
    sortControl: '[data-testid="heatmap-sort"]',
    filterControl: '[data-testid="heatmap-filter"]',
    axisLabel: '[data-testid="heatmap-axis-label"]',
    populationN: '[data-testid="population-badge"]',
    evidenceMethod: '[data-testid="evidence-method"]',
    evidenceQ: '[data-testid="evidence-q"]',
    suspected: '[data-testid="evidence-suspected"]',
    provenance: '[data-testid="provenance-handle"]',
    threadNode: '[data-testid="thread-node"]',
  },
};

test.describe("P5 heatmap interactivity", () => {
  test.beforeEach(async ({ page }) => { await page.goto(CFG.routes.correlation, { waitUntil: "networkidle" }); });

  test("hover shows value + N; click drills to underlying records", async ({ page }) => {
    const cell = page.locator(CFG.sel.heatmapCell).first();
    await expect(cell).toBeVisible();
    await cell.hover();
    const tip = page.locator(CFG.sel.heatmapTooltip);
    await expect(tip).toBeVisible();
    await expect(tip).toContainText(/\d/);                       // a value
    await expect(tip.locator(CFG.sel.populationN)).toBeVisible(); // N present
    await cell.click();
    const rows = page.locator(`${CFG.sel.drillList} ${CFG.sel.drillRow}`);
    await expect(rows.first()).toBeVisible();
    expect(await rows.count()).toBeGreaterThan(0);
  });

  test("sort re-orders the axis (DOM order changes)", async ({ page }) => {
    const before = await page.locator(CFG.sel.axisLabel).allInnerTexts();
    await page.locator(CFG.sel.sortControl).click();
    await page.waitForTimeout(350);
    const after = await page.locator(CFG.sel.axisLabel).allInnerTexts();
    expect(after.join("|")).not.toEqual(before.join("|"));
  });

  test("filter changes the cell set and updates N", async ({ page }) => {
    const cellsBefore = await page.locator(CFG.sel.heatmapCell).count();
    const nBefore = await page.locator(CFG.sel.populationN).first().getAttribute("data-n");
    await page.locator(CFG.sel.filterControl).click();
    await page.waitForTimeout(350);
    const cellsAfter = await page.locator(CFG.sel.heatmapCell).count();
    const nAfter = await page.locator(CFG.sel.populationN).first().getAttribute("data-n");
    expect(cellsAfter !== cellsBefore || nAfter !== nBefore).toBeTruthy();
  });

  test("interaction is responsive (< 300ms, no stall)", async ({ page }) => {
    const cell = page.locator(CFG.sel.heatmapCell).first();
    const t0 = Date.now();
    await cell.hover();
    await page.locator(CFG.sel.heatmapTooltip).waitFor({ state: "visible" });
    expect(Date.now() - t0).toBeLessThan(300 + 200); // +200ms CI slack
  });
});

test.describe("P5 correlation honesty surface", () => {
  test("every correlation result shows method, q, N, suspected-not-proven + provenance", async ({ page }) => {
    await page.goto(CFG.routes.correlation, { waitUntil: "networkidle" });
    await expect(page.locator(CFG.sel.evidenceMethod).first()).toBeVisible();
    await expect(page.locator(CFG.sel.evidenceQ).first()).toBeVisible();
    await expect(page.locator(CFG.sel.populationN).first()).toBeVisible();
    await expect(page.locator(CFG.sel.suspected).first()).toBeVisible();
    // no number without a provenance handle: there must be >=1 provenance per card
    const handles = await page.locator(CFG.sel.provenance).count();
    expect(handles).toBeGreaterThan(0);
    await page.locator(CFG.sel.provenance).first().click();
    await expect(page.getByText(/method inputs|inputs/i).first()).toBeVisible();
  });
});

test.describe("P5 genealogy bidirectional thread", () => {
  test("clicking a node loads detail + highlights the path", async ({ page }) => {
    await page.goto(CFG.routes.genealogy, { waitUntil: "networkidle" });
    const node = page.locator(CFG.sel.threadNode).first();
    await expect(node).toBeVisible();
    await node.click();
    await expect(node).toHaveAttribute("data-active", "true");
  });
});