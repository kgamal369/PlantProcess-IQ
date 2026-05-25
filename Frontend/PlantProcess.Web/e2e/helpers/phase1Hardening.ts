import { expect, type APIRequestContext, type Page } from "@playwright/test";

export async function prepareAuthenticatedPage(
  page: Page,
  request: APIRequestContext
) {
  const apiBase =
    process.env.PLAYWRIGHT_API_URL ||
    process.env.VITE_API_BASE_URL ||
    "http://localhost:5063";

  const health = await request.get(`${apiBase}/health`).catch(() => null);

  if (health && health.ok()) {
    await page.addInitScript(() => {
      window.localStorage.setItem("ppiq-demo-mode", "true");
    });
  }
}

export function installHardeningPageGuard(page: Page) {
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  const failedRequests: string[] = [];

  page.on("console", (message) => {
    if (message.type() === "error") {
      consoleErrors.push(message.text());
    }
  });

  page.on("pageerror", (error) => {
    pageErrors.push(error.message);
  });

  page.on("requestfailed", (request) => {
    const url = request.url();

    if (
      url.includes("/sockjs-node") ||
      url.includes("/@vite") ||
      url.includes("__vite") ||
      url.includes("favicon")
    ) {
      return;
    }

    failedRequests.push(`${request.method()} ${url}`);
  });

  return {
    async assertNoUnexpectedFailures() {
      expect(pageErrors, `Page errors: ${pageErrors.join("\n")}`).toEqual([]);
      expect(consoleErrors, `Console errors: ${consoleErrors.join("\n")}`).toEqual([]);
      expect(failedRequests, `Failed requests: ${failedRequests.join("\n")}`).toEqual([]);
    },
  };
}

export async function gotoAndAssertCustomerSafePage(
  page: Page,
  route: string,
  expectedText: RegExp
) {
  await page.goto(route, { waitUntil: "networkidle" });

  await expect(page.locator("body")).toBeVisible();
  await expect(page.locator("body")).not.toContainText(/white screen|undefined is not a function/i);
  await expect(page.locator("body")).toContainText(expectedText, {
    timeout: 15_000,
  });
}

export async function refreshAndAssertStillSafe(
  page: Page,
  expectedText: RegExp
) {
  await page.reload({ waitUntil: "networkidle" });

  await expect(page.locator("body")).toBeVisible();
  await expect(page.locator("body")).toContainText(expectedText, {
    timeout: 15_000,
  });
}