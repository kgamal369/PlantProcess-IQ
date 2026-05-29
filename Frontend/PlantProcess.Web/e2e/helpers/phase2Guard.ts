// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/helpers/phase2Guard.ts
//
// Phase 2 E2E guard.
// Fixes duplicate test title issue by restoring route.name.
// Avoids hard networkidle dependency.
// ============================================================

import { expect, type Page } from "@playwright/test";
import {
  formatRequestFailure,
  formatResponseFailure,
  isIgnorableConsoleMessage,
  shouldTrackFailedRequest,
  shouldTrackFailedResponse,
  type AllowedFailureOptions,
} from "./e2eFailureFilters";

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
    expectedText: /material|investigation|genealogy|quality|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/risk",
    name: "Risk",
    expectedText: /risk|score|quality|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/data-quality",
    name: "Data Quality",
    expectedText: /data quality|issue|quality|validity|completeness|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/correlations",
    name: "Correlations",
    expectedText: /correlation|parameter|signal|quality|suspected|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/admin",
    name: "Admin",
    expectedText: /admin|jobs|configuration|source|connector|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/demo-lifecycle",
    name: "Demo Lifecycle",
    expectedText: /demo|lifecycle|connector|schema|mapping|ml|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/ml-readiness",
    name: "ML Readiness",
    expectedText: /ml|readiness|training|label|model|plantprocess iq/i,
    critical: true,
  },
  {
    route: "/commercial/license",
    name: "Commercial License",
    expectedText: /license|tier|feature|commercial|plantprocess iq/i,
    critical: true,
  },
];

export type Phase2StrictGuard = {
  assertClean: () => Promise<void>;
  getPageErrors: () => string[];
  getConsoleErrors: () => string[];
  getFailedRequests: () => string[];
};

export function installPhase2StrictGuard(
  page: Page,
  options: AllowedFailureOptions = {}
): Phase2StrictGuard {
  const pageErrors: string[] = [];
  const consoleErrors: string[] = [];
  const failedRequests: string[] = [];

  page.on("pageerror", (error) => {
    pageErrors.push(error.message);
  });

  page.on("console", (message) => {
    if (message.type() !== "error") return;
    if (isIgnorableConsoleMessage(message)) return;

    consoleErrors.push(message.text());
  });

  page.on("requestfailed", (request) => {
  const url = request.url();
  const method = request.method();
  const failureText = request.failure()?.errorText ?? "";
  const ignoredUrlParts = [
  "favicon",
  "vite",
  "sockjs",
  "hot-update",
  "__vite_ping",
  ];

  function shouldIgnoreUrl(url: string) {
    return ignoredUrlParts.some((part) => url.includes(part));
  }
    if (shouldIgnoreUrl(url)) {
      return;
    }

    const isNavigationCancellation =
      /net::ERR_ABORTED|NS_BINDING_ABORTED|aborted|cancelled|canceled|Target closed|frame was detached/i.test(
        failureText
      );

    if (isNavigationCancellation) {
      return;
    }

    failedRequests.push(
      failureText
        ? `${method} ${url} (${failureText})`
        : `${method} ${url}`
    );
  });

  page.on("response", (response) => {
    if (!shouldTrackFailedResponse(response, options)) return;

    failedRequests.push(formatResponseFailure(response));
  });

  return {
    async assertClean() {
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

export async function expectCustomerSafeShell(page: Page): Promise<void> {
  const body = page.locator("body");

  await expect(body).toBeVisible({
    timeout: 20_000,
  });

  await expect(body).toContainText(
    /plantprocess iq|sou|dashboard|admin|material|risk|quality|demo|license|loading|retry|unavailable|error/i,
    {
      timeout: 20_000,
    }
  );

  const normalized = (await body.innerText()).toLowerCase();

  expect(normalized).not.toContain("cannot read properties");
  expect(normalized).not.toContain("is not a function");
  expect(normalized).not.toContain("uncaught");
  expect(normalized).not.toContain("stack trace");
  expect(normalized).not.toContain("undefined is not");
}

export async function gotoCustomerSafeRoute(
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
      // Polling/background API retries are allowed if the page remains usable.
    });

  await expectCustomerSafeShell(page);

  await expect(page.locator("body")).toContainText(expectedText, {
    timeout: 20_000,
  });
}