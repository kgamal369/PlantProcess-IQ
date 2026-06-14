import { test, expect } from "@playwright/test";

// PPIQ-402: selecting a defect ID + window, naming and running an inspection job generates a
// page of driver widgets bound to live results; the page saves, reloads from MetaData, and
// re-runs via the Run button producing fresh results.
test.describe("PPIQ-402 inspection-job live widget page loop", () => {
  test("generate, save, reload and re-run an inspection job", async ({ page }) => {
    await page.goto("/analytics/inspection-jobs");
    await expect(page.getByTestId("inspection-jobs-page")).toBeVisible();

    await page.getByTestId("inspection-outcome-select").selectOption({ index: 1 });
    await page.getByTestId("inspection-window-input").fill("90");
    await page.getByTestId("inspection-job-name").fill("e2e-edge-crack-drivers");
    await page.getByTestId("inspection-run").click();

    const charts = page.getByTestId("driver-chart");
    await expect(charts.first()).toBeVisible({ timeout: 60_000 });
    expect(await charts.count()).toBeGreaterThanOrEqual(1);

    await page.getByTestId("inspection-save").click();
    await expect(page.getByText(/saved/i)).toBeVisible();

    await page.reload();
    await page.getByTestId("saved-job-e2e-edge-crack-drivers").click();
    await expect(page.getByTestId("driver-chart").first()).toBeVisible();

    const before = await page.getByTestId("inspection-run-id").textContent();
    await page.getByTestId("inspection-run").click();
    await expect
      .poll(async () => page.getByTestId("inspection-run-id").textContent(), { timeout: 60_000 })
      .not.toBe(before);
  });
});