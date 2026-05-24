// ============================================================
// File: Frontend/PlantProcess.Web/e2e/helpers/hardening.ts
// Tasks:
//   QA-HARD-001
//   FE-HARD-010
//   FE-HARD-012
//
// Purpose:
//   Shared hardening helper for:
//   - authenticated direct route loading
//   - console/page error detection
//   - customer-safe state assertions
//   - API failure-mode checks
// ============================================================

import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { apiBaseUrl, login } from "./auth";

export type HardeningPageGuard = {
  assertNoUnexpectedFailures: () => Promise<void>;
  getConsoleErrors: () => string[];
  getPageErrors: () => string[];
  getServerFailures: () => string[];
};

const ignoredConsoleFragments = [
  "favicon",
  "vite",
  "sockjs",
  "hmr",
  "ResizeObserver loop",
];

const ignoredResponseUrlFragments = [
  "favicon",
  "vite",
  "sockjs",
  "hot-update",
];

export async function prepareAuthenticatedPage(
  page: Page,
  request: APIRequestContext
) {
  const token = await login(request);

  await page.addInitScript((accessToken) => {
    window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    window.localStorage.setItem("plantprocess.auth.userName", "admin");
    window.localStorage.setItem("plantprocess.auth.role", "Admin");
    window.localStorage.setItem(
      "plantprocess.auth.expiresAtUtc",
      new Date(Date.now() + 60 * 60 * 1000).toISOString()
    );
  }, token);

  return token;
}

export function installHardeningPageGuard(
  page: Page,
  options: {
    allowServerFailureUrlFragments?: string[];
    allowConsoleErrorFragments?: string[];
  } = {}
): HardeningPageGuard {
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  const serverFailures: string[] = [];

  page.on("console", (message) => {
    if (message.type() !== "error") return;

    const text = message.text();

    if (
      ignoredConsoleFragments.some((fragment) =>
        text.toLowerCase().includes(fragment.toLowerCase())
      )
    ) {
      return;
    }

    if (
      options.allowConsoleErrorFragments?.some((fragment) =>
        text.toLowerCase().includes(fragment.toLowerCase())
      )
    ) {
      return;
    }

    consoleErrors.push(text);
  });

  page.on("pageerror", (error) => {
    pageErrors.push(error.message);
  });

  page.on("response", (response) => {
    const url = response.url();
    const status = response.status();

    if (status < 500) return;

    if (
      ignoredResponseUrlFragments.some((fragment) =>
        url.toLowerCase().includes(fragment.toLowerCase())
      )
    ) {
      return;
    }

    if (
      options.allowServerFailureUrlFragments?.some((fragment) =>
        url.toLowerCase().includes(fragment.toLowerCase())
      )
    ) {
      return;
    }

    serverFailures.push(`${status} ${url}`);
  });

  return {
    getConsoleErrors: () => consoleErrors,
    getPageErrors: () => pageErrors,
    getServerFailures: () => serverFailures,

    async assertNoUnexpectedFailures() {
      expect(
        pageErrors,
        `Unexpected page runtime errors:\n${pageErrors.join("\n")}`
      ).toEqual([]);

      expect(
        consoleErrors,
        `Unexpected browser console errors:\n${consoleErrors.join("\n")}`
      ).toEqual([]);

      expect(
        serverFailures,
        `Unexpected uncontrolled server failures:\n${serverFailures.join("\n")}`
      ).toEqual([]);
    },
  };
}

export async function gotoAndAssertCustomerSafePage(
  page: Page,
  route: string,
  expectedText: RegExp
) {
  await page.goto(route, {
    waitUntil: "domcontentloaded",
    timeout: 30_000,
  });

  await page.waitForLoadState("networkidle", {
    timeout: 12_000,
  }).catch(() => {
    // Polling or long-lived requests should not fail the hardening check alone.
  });

  const body = page.locator("body");

  await expect(body).toContainText(expectedText, {
    timeout: 20_000,
  });

  const text = await body.innerText();
  const normalized = text.toLowerCase();

  expect(normalized).not.toContain("cannot read properties");
  expect(normalized).not.toContain("is not a function");
  expect(normalized).not.toContain("uncaught");
  expect(normalized).not.toContain("stack trace");
  expect(normalized).not.toContain("undefined is not");
  expect(normalized).not.toContain("failed to fetch");
}

export async function blockBackendApisForPage(page: Page) {
  await page.route("**/*", async (route) => {
    const url = route.request().url();

    if (
      url.startsWith(apiBaseUrl) &&
      !url.includes("/auth/login") &&
      !url.includes("/diagnostics/client-error")
    ) {
      await route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({
          message: "Simulated backend outage from hardening test.",
        }),
      });
      return;
    }

    await route.continue();
  });
}