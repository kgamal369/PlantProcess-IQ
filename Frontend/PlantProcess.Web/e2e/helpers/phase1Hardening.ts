// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/helpers/phase1Hardening.ts
//
// Phase 1 route/refresh hardening helper.
// Restores refreshAndAssertStillSafe export used by phase1-route-refresh.spec.ts.
// ============================================================

import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { apiBaseUrl, login } from "./auth";
import {
  formatRequestFailure,
  formatResponseFailure,
  isIgnorableConsoleMessage,
  shouldTrackFailedRequest,
  shouldTrackFailedResponse,
  type AllowedFailureOptions,
} from "./e2eFailureFilters";

export type Phase1HardeningPageGuard = {
  assertNoUnexpectedFailures: () => Promise<void>;
  getPageErrors: () => string[];
  getConsoleErrors: () => string[];
  getFailedRequests: () => string[];
};

export async function prepareAuthenticatedPage(
  page: Page,
  request: APIRequestContext
): Promise<string> {
  /*
   * Doctrine v5 P01:
   * Browser auth-token storage is retired. Login must happen through the
   * browser context request client so the HttpOnly refresh cookie belongs to
   * the same browser context that will open the HMI page.
   *
   * The returned access token is only for API-level assertions in Playwright.
   * It is never written to localStorage/sessionStorage.
   */
  const token = await login(page.context().request);

  await page.addInitScript((baseUrl) => {
    localStorage.setItem("ppiq-demo-mode", "true");
    localStorage.setItem("VITE_API_BASE_URL", baseUrl);
  }, apiBaseUrl);

  return token;
}

export function installHardeningPageGuard(
  page: Page,
  options: AllowedFailureOptions = {}
): Phase1HardeningPageGuard {
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  const failedRequests: string[] = [];

  page.on("console", (message) => {
    if (message.type() !== "error") return;
    if (isIgnorableConsoleMessage(message)) return;

    consoleErrors.push(message.text());
  });

  page.on("pageerror", (error) => {
    pageErrors.push(error.message);
  });

  page.on("requestfailed", (request) => {
    if (!shouldTrackFailedRequest(request, options)) return;

    failedRequests.push(formatRequestFailure(request));
  });

  page.on("response", (response) => {
    if (!shouldTrackFailedResponse(response, options)) return;

    failedRequests.push(formatResponseFailure(response));
  });

  return {
    async assertNoUnexpectedFailures() {
      expect(pageErrors, `Page errors:\n${pageErrors.join("\n")}`).toEqual([]);
      expect(consoleErrors, `Console errors:\n${consoleErrors.join("\n")}`).toEqual([]);
      expect(failedRequests, `Failed requests:\n${failedRequests.join("\n")}`).toEqual([]);
    },

    getPageErrors() {
      return [...pageErrors];
    },

    getConsoleErrors() {
      return [...consoleErrors];
    },

    getFailedRequests() {
      return [...failedRequests];
    },
  };
}

export async function gotoAndAssertCustomerSafePage(
  page: Page,
  route: string,
  expectedText: RegExp
): Promise<void> {
  await page.goto(route, {
    waitUntil: "domcontentloaded",
    timeout: 30_000,
  });

  await page
    .waitForLoadState("networkidle", {
      timeout: 8_000,
    })
    .catch(() => {
      // Background retries/polling should not fail the route check alone.
    });

  const body = page.locator("body");

  await expect(body).toBeVisible({
    timeout: 20_000,
  });

  await expect(body).toContainText(expectedText, {
    timeout: 20_000,
  });

  const normalized = (await body.innerText()).toLowerCase();

  expect(normalized).not.toContain("white screen");
  expect(normalized).not.toContain("cannot read properties");
  expect(normalized).not.toContain("is not a function");
  expect(normalized).not.toContain("uncaught");
  expect(normalized).not.toContain("stack trace");
  expect(normalized).not.toContain("undefined is not");
}

export async function refreshAndAssertStillSafe(
  page: Page,
  expectedText: RegExp
): Promise<void> {
  await page.reload({
    waitUntil: "domcontentloaded",
    timeout: 30_000,
  });

  await page
    .waitForLoadState("networkidle", {
      timeout: 8_000,
    })
    .catch(() => {
      // Polling/background retries are acceptable if the page remains usable.
    });

  const body = page.locator("body");

  await expect(body).toBeVisible({
    timeout: 20_000,
  });

  await expect(body).toContainText(expectedText, {
    timeout: 20_000,
  });

  const normalized = (await body.innerText()).toLowerCase();

  expect(normalized).not.toContain("white screen");
  expect(normalized).not.toContain("cannot read properties");
  expect(normalized).not.toContain("is not a function");
  expect(normalized).not.toContain("uncaught");
  expect(normalized).not.toContain("stack trace");
  expect(normalized).not.toContain("undefined is not");
}