import { expect, type Page } from "@playwright/test";

export type Phase2RouteContract = {
  route: string;
  name: string;
  expectedText: RegExp;
  critical?: boolean;
};

export const phase2CriticalRoutes: Phase2RouteContract[] = [
  {
    route: "/dashboard",
    name: "Dashboard",
    expectedText: /dashboard|widget|quality|risk|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/materials",
    name: "Materials",
    expectedText: /material|investigation|genealogy|quality/i,
    critical: true,
  },
  {
    route: "/risk",
    name: "Risk",
    expectedText: /risk|score|quality/i,
    critical: true,
  },
  {
    route: "/data-quality",
    name: "Data Quality",
    expectedText: /data quality|issue|quality|validity|completeness/i,
    critical: true,
  },
  {
    route: "/correlations",
    name: "Correlations",
    expectedText: /correlation|parameter|signal|quality/i,
    critical: true,
  },
  {
    route: "/admin",
    name: "Admin",
    expectedText: /admin|configuration|connector|schema|job|import/i,
    critical: true,
  },
  {
    route: "/demo-lifecycle",
    name: "Demo Lifecycle",
    expectedText: /demo|lifecycle|connector|stage|map|monitor|report/i,
    critical: true,
  },
  {
    route: "/ml-readiness",
    name: "ML Readiness",
    expectedText: /ml|readiness|model|label|training|correlation/i,
    critical: false,
  },
  {
    route: "/commercial/license",
    name: "Commercial License",
    expectedText: /license|light|pro|enterprise|usage/i,
    critical: false,
  },
];

const ignoredConsolePatterns = [
  /Download the React DevTools/i,
  /vite/i,
  /hmr/i,
  /favicon/i,
];

const ignoredRequestPatterns = [
  /favicon/i,
  /sockjs-node/i,
  /__vite/i,
  /@vite/i,
];

export function installPhase2StrictGuard(page: Page) {
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  const failedRequests: string[] = [];

  page.on("console", (message) => {
    if (message.type() !== "error") return;

    const text = message.text();

    if (ignoredConsolePatterns.some((pattern) => pattern.test(text))) return;

    consoleErrors.push(text);
  });

  page.on("pageerror", (error) => {
    pageErrors.push(error.message);
  });

  page.on("requestfailed", (request) => {
    const url = request.url();

    if (ignoredRequestPatterns.some((pattern) => pattern.test(url))) return;

    failedRequests.push(`${request.method()} ${url}`);
  });

  return {
    async assertClean() {
      expect(pageErrors, `Page errors:\n${pageErrors.join("\n")}`).toEqual([]);
      expect(consoleErrors, `Console errors:\n${consoleErrors.join("\n")}`).toEqual([]);
      expect(failedRequests, `Failed requests:\n${failedRequests.join("\n")}`).toEqual([]);
    },
  };
}

export async function expectCustomerSafeShell(page: Page) {
  const body = page.locator("body");

  await expect(body).toBeVisible();

  const text = await body.innerText();

  expect(text).not.toMatch(/undefined is not a function/i);
  expect(text).not.toMatch(/cannot read properties/i);
  expect(text).not.toMatch(/white screen/i);
  expect(text).not.toMatch(/uncaught/i);
  expect(text).not.toMatch(/stack trace/i);
}

export async function gotoCustomerSafeRoute(
  page: Page,
  route: string,
  expectedText: RegExp
) {
  await page.goto(route, {
    waitUntil: "networkidle",
    timeout: 30_000,
  });

  await expectCustomerSafeShell(page);

  await expect(page.locator("body")).toContainText(expectedText, {
    timeout: 15_000,
  });
}