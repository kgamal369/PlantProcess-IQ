import { expect, test } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("P03 page builder smoke", () => {
  test("page builder route exposes persisted metadata actions", async ({ page, request }) => {
    const token = await login(request);

    /* P01: no browser token seeding; AuthProvider performs cookie refresh/login bootstrap. */

    await page.goto("/page-builder");

    await expect(
      page.getByRole("heading", { name: /User-created pages, not coded pages/i }),
    ).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText("Page Builder", { exact: true })).toBeVisible();

    await expect(
      page.getByRole("heading", { name: /^Page properties$/i }),
    ).toBeVisible();

    await expect(
      page.getByRole("heading", { name: /^Widget library$/i }),
    ).toBeVisible();

    await expect(
      page.getByRole("heading", { name: /^Canvas$/i }),
    ).toBeVisible();

    await expect(page.getByLabel("Title", { exact: true })).toHaveValue(
      "Demo Quality Investigation",
    );

    await expect(page.getByLabel("Slug", { exact: true })).toHaveValue(
      "demo-quality-investigation",
    );

    await expect(page.getByRole("button", { name: /^Save page definition$/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /^Load by slug$/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /^Delete owned page$/i })).toBeVisible();
  });
});
