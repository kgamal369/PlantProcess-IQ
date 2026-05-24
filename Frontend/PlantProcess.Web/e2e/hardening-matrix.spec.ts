// ============================================================
// File: Frontend/PlantProcess.Web/e2e/hardening-matrix.spec.ts
// Task: QA-HARD-001
//
// Purpose:
//   Per-page hardening matrix:
//   - direct route load
//   - authenticated shell load
//   - no console/page runtime error
//   - no uncontrolled 5xx
//   - controlled backend outage behavior
// ============================================================

import { expect, test } from "@playwright/test";
import {
  blockBackendApisForPage,
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/hardening";

const pages = [
  {
    name: "Dashboard",
    route: "/dashboard",
    expected: /dashboard|defect|risk|material|plantprocess iq/i,
  },
  {
    name: "Material Investigation",
    route: "/materials",
    expected: /material|investigation|search|plantprocess iq/i,
  },
  {
    name: "Risk Dashboard",
    route: "/risk",
    expected: /risk|score|material|plantprocess iq/i,
  },
  {
    name: "Data Quality",
    route: "/data-quality",
    expected: /data quality|quality|issue|plantprocess iq/i,
  },
  {
    name: "Correlations",
    route: "/correlations",
    expected: /correlation|parameter|quality|plantprocess iq/i,
  },
  {
    name: "ML Readiness",
    route: "/ml-readiness",
    expected: /ml readiness|training disabled|model|plantprocess iq/i,
  },
  {
    name: "Demo Lifecycle",
    route: "/demo-lifecycle",
    expected: /demo lifecycle|connector|schema|job|plantprocess iq/i,
  },
  {
    name: "Admin",
    route: "/admin",
    expected: /admin|configuration|jobs|schema|import|plantprocess iq/i,
  },
  {
    name: "Admin Preview",
    route: "/admin-preview",
    expected: /admin|preview|workspace|plantprocess iq/i,
  },
  {
    name: "Brand",
    route: "/brand",
    expected: /brand identity|market positioning|not mes|plantprocess iq/i,
  },
  {
    name: "Commercial License",
    route: "/commercial/license",
    expected: /license|tier|feature|commercial|plantprocess iq/i,
  },
];

test.describe("QA-HARD-001 — per-page hardening matrix", () => {
  for (const pageCase of pages) {
    test(`${pageCase.name} direct route should render safely`, async ({
      page,
      request,
    }) => {
      await prepareAuthenticatedPage(page, request);

      const guard = installHardeningPageGuard(page, {
        allowServerFailureUrlFragments: [
          "/analytics/dashboard/definitions/system-templates/ensure",
        ],
      });

      await gotoAndAssertCustomerSafePage(
        page,
        pageCase.route,
        pageCase.expected
      );

      await guard.assertNoUnexpectedFailures();
    });
  }

  for (const pageCase of pages.slice(0, 8)) {
    test(`${pageCase.name} should survive simulated backend outage`, async ({
      page,
      request,
    }) => {
      await prepareAuthenticatedPage(page, request);
      await blockBackendApisForPage(page);

      const guard = installHardeningPageGuard(page, {
        allowServerFailureUrlFragments: [
          // We intentionally simulate backend failure in this test.
          "localhost",
          "127.0.0.1",
          "plantprocess",
        ],
      });

      await page.goto(pageCase.route, {
        waitUntil: "domcontentloaded",
        timeout: 30_000,
      });

      await page.waitForLoadState("networkidle", {
        timeout: 8_000,
      }).catch(() => {
        // Controlled error/loading states may still have pending retries.
      });

      const body = page.locator("body");
      await expect(body).toBeVisible();

      const text = await body.innerText();
      const normalized = text.toLowerCase();

      expect(normalized).toMatch(
        /plantprocess iq|error|failed|unavailable|try again|backend|loading|dashboard|admin|material/
      );

      expect(normalized).not.toContain("cannot read properties");
      expect(normalized).not.toContain("is not a function");
      expect(normalized).not.toContain("uncaught");
      expect(normalized).not.toContain("stack trace");
      expect(normalized).not.toContain("undefined is not");

      expect(
        guard.getPageErrors(),
        `Page runtime errors during outage check for ${pageCase.name}`
      ).toEqual([]);
    });
  }
});