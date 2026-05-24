import { expect, test } from "@playwright/test";

const routes = [
  "/",
  "/dashboard",
  "/quality",
  "/risk",
  "/data-quality",
  "/correlation",
  "/material-investigation",
  "/admin",
  "/admin-preview",
  "/demo-lifecycle",
];

test.describe("Navigation graph and refresh survival", () => {
  for (const route of routes) {
    test(`route survives direct navigation and refresh: ${route}`, async ({ page }) => {
      const consoleErrors: string[] = [];
      const failedRequests: string[] = [];

      page.on("console", (message) => {
        if (message.type() === "error") {
          consoleErrors.push(message.text());
        }
      });

      page.on("requestfailed", (request) => {
        const url = request.url();

        if (!url.includes("favicon") && !url.includes(".map")) {
          failedRequests.push(`${request.method()} ${url}`);
        }
      });

      await page.goto(route, { waitUntil: "domcontentloaded" });
      await expect(page.locator("body")).toBeVisible();

      await page.reload({ waitUntil: "domcontentloaded" });
      await expect(page.locator("body")).toBeVisible();

      await expect(page.locator("body")).not.toContainText(/white screen|uncaught|undefined is not/i);

      expect(consoleErrors.filter((x) => !x.includes("favicon")).join("\n")).toEqual("");
      expect(failedRequests.join("\n")).toEqual("");
    });
  }

  test("main navigation links are reachable", async ({ page }) => {
    await page.goto("/");

    const links = await page.locator("a[href^='/']").evaluateAll((items) =>
      items.map((item) => (item as HTMLAnchorElement).getAttribute("href")).filter(Boolean)
    );

    const uniqueLinks = [...new Set(links)].slice(0, 25);

    for (const href of uniqueLinks) {
      await page.goto(href!, { waitUntil: "domcontentloaded" });
      await expect(page.locator("body")).toBeVisible();
    }
  });
});