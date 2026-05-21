import { expect, test } from "@playwright/test";

test.describe("Dimension 7 Brand Identity", () => {
  test("brand identity page renders positioning, proof assets and forbidden-language-safe content", async ({
    page,
  }) => {
    await page.goto("/brand");

    await expect(page.getByText("Dimension 7 — Brand Identity")).toBeVisible();
    await expect(page.getByText("Brand Identity & Market Positioning")).toBeVisible();
    await expect(page.getByText("Engineer brief")).toBeVisible();
    await expect(page.getByText("Architecture diagram")).toBeVisible();
    await expect(page.getByText("Not MES")).toBeVisible();
    await expect(page.getByText("Not SCADA")).toBeVisible();

    const body = await page.locator("body").innerText();
    const normalized = body.toLowerCase();

    expect(normalized).not.toContain("guaranteed root cause detection");
    expect(normalized).not.toContain("production-ready ai model");
    expect(normalized).not.toContain("replaces mes");
    expect(normalized).not.toContain("replaces scada");
  });
});