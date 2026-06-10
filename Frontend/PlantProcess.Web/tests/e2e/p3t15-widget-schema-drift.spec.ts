
import { expect, test } from "@playwright/test";

test.describe("P3-T15 widget schema-drift proof", () => {
  test("renders heatmap widget and updates filter/sort without reload", async ({ page }) => {
    await page.goto("/dashboard/widgets/schema-drift");

    await expect(page.getByTestId("p3-t15-contract-status")).toContainText("VALID");

    await page.getByRole("button", { name: /create heatmap widget/i }).click();

    const heatmap = page.getByTestId("p3-t15-heatmap");
    await expect(heatmap).toBeVisible();

    const initialSignature = await heatmap.getAttribute("data-series-signature");

    await page.getByTestId("p3-t15-filter-search").fill("Caster");
    await page.getByTestId("p3-t15-filter-min").fill("0.20");

    await expect(page.getByTestId("p3-t15-cell-count")).toContainText("2");

    const filteredSignature = await heatmap.getAttribute("data-series-signature");
    expect(filteredSignature).not.toBe(initialSignature);

    await page.getByTestId("p3-t15-sort-direction").selectOption("asc");
    const sortedSignature = await heatmap.getAttribute("data-series-signature");
    expect(sortedSignature).not.toBe(filteredSignature);

    await expect(page.locator("body")).not.toContainText(/white screen|cannot read properties|undefined is not/i);
  });
});
