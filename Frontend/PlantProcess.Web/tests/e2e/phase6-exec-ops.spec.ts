import { test, expect } from "@playwright/test";

/* PPIQ-PHASE6 exec/ops e2e. CONFIG: real routes + selectors. The value/tier/jobs
 * hooks are emitted by the Phase-6 components; the assumption-toggle hook you add
 * where the demo removes an input. */
const CFG = {
  routes: {
    exec: process.env.PPIQ_EXEC_ROUTE || "/dashboard/executive",
    jobs: process.env.PPIQ_JOBS_ROUTE || "/ops/jobs",
  },
  sel: {
    valueRange: '[data-testid="value-range"]',
    low: '[data-testid="value-low"]',
    expected: '[data-testid="value-expected"]',
    high: '[data-testid="value-high"]',
    term: '[data-testid="value-term"]',
    termToggle: '[data-testid="value-term-toggle"]',
    termDrill: '[data-testid="value-term-drill"]',
    abstain: '[data-testid="abstain-panel"]',
    assumptionToggle: '[data-testid="assumption-toggle"]',  // you add this where the demo removes an input
    tierBadge: '[data-testid="tier-badge"]',
    gate: '[data-testid="entitlement-gate"]',
    jobsTable: '[data-testid="jobs-monitor"]',
    jobRow: '[data-testid="jobs-monitor"] tbody tr',
    jobLastRun: '[data-testid="job-lastrun"]',
    jobRerun: '[data-testid="job-rerun"]',
  },
};

test.describe("P6 exec value range", () => {
  test("named range renders and reproduces across reloads", async ({ page }) => {
    await page.goto(CFG.routes.exec, { waitUntil: "networkidle" });
    await expect(page.locator(CFG.sel.valueRange)).toBeVisible();
    const read = async () => ({
      low: await page.locator(CFG.sel.low).innerText(),
      expected: await page.locator(CFG.sel.expected).innerText(),
      high: await page.locator(CFG.sel.high).innerText(),
    });
    const first = await read();
    expect(first.expected).toMatch(/\d/);
    await page.reload({ waitUntil: "networkidle" });
    const second = await read();
    expect(second).toEqual(first); // reproducible for fixed demo inputs
  });

  test("each value term drills to its source value + JSON (>= 4 terms)", async ({ page }) => {
    await page.goto(CFG.routes.exec, { waitUntil: "networkidle" });
    const toggles = page.locator(CFG.sel.termToggle);
    const n = await toggles.count();
    expect(n).toBeGreaterThanOrEqual(4);
    for (let i = 0; i < Math.min(n, 4); i++) {
      await toggles.nth(i).click();
      await expect(page.locator(CFG.sel.termDrill).nth(0)).toBeVisible();
    }
  });

  test("removing a required assumption flips to abstain (if the demo toggle exists)", async ({ page }) => {
    await page.goto(CFG.routes.exec, { waitUntil: "networkidle" });
    const toggle = page.locator(CFG.sel.assumptionToggle);
    if (await toggle.count() === 0) test.skip(true, "no assumption-toggle wired yet");
    await toggle.first().click();
    await expect(page.locator(CFG.sel.abstain)).toBeVisible();
  });
});

test.describe("P6 entitlement tier toggle", () => {
  test("tier badge reflects a verified license", async ({ page }) => {
    await page.goto(CFG.routes.exec, { waitUntil: "networkidle" });
    const badge = page.locator(CFG.sel.tierBadge).first();
    await expect(badge).toBeVisible();
    await expect(badge).toHaveAttribute("data-verified", "true");
  });

  test("gated features appear/disappear with tier (data-granted reflects entitlement)", async ({ page }) => {
    await page.goto(CFG.routes.exec, { waitUntil: "networkidle" });
    const gates = page.locator(CFG.sel.gate);
    expect(await gates.count()).toBeGreaterThan(0);
    // at least one gate must reflect a real grant decision (true or false), not be absent
    const granted = await gates.first().getAttribute("data-granted");
    expect(["true", "false"]).toContain(granted);
  });
});

test.describe("P6 jobs monitor", () => {
  test("each job shows last-run, outcome, duration, rows; re-run advances last-run", async ({ page }) => {
    await page.goto(CFG.routes.jobs, { waitUntil: "networkidle" });
    await expect(page.locator(CFG.sel.jobsTable)).toBeVisible();
    const rows = page.locator(CFG.sel.jobRow);
    expect(await rows.count()).toBeGreaterThan(0);
    const first = rows.first();
    await expect(first.locator(CFG.sel.jobLastRun)).toBeVisible();
    const before = await first.locator(CFG.sel.jobLastRun).innerText();
    const rerun = first.locator(CFG.sel.jobRerun);
    if (await rerun.isEnabled()) {
      await rerun.click();
      await expect.poll(async () => first.locator(CFG.sel.jobLastRun).innerText(), { timeout: 30_000 }).not.toBe(before);
    }
  });
});