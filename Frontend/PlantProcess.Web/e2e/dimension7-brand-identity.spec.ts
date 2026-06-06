import { expect, test } from "@playwright/test";

test.describe("Dimension 7 Brand Identity", () => {
  test("brand identity page renders positioning, proof assets and forbidden-language-safe content", async ({
    page,
  }) => {
    await page.goto("/brand");

    await expect(page.getByText("Dimension 7 — Brand Identity")).toBeVisible();
    await expect(page.getByText("Brand Identity & Market Positioning")).toBeVisible();
    await expect(page.getByText('Engineer brief', { exact: true })).toBeVisible();
    await expect(page.getByText('Architecture diagram', { exact: true })).toBeVisible();
    await expect(page.getByText("Not MES")).toBeVisible();
    await expect(page.getByText("Not SCADA")).toBeVisible();

    const body = await page.locator("body").innerText();
    const normalized = body.toLowerCase();

    expect(normalized).not.toContain("evidence-backed root-cause investigation detection");
    expect(normalized).not.toContain("production-ready ai model");
    expect(normalized).not.toContain("complements MES");
    expect(normalized).not.toContain("complements SCADA");
  });
});