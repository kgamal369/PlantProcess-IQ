import { expect, test } from "@playwright/test";
import {
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/hardening";

test.describe("PPIQ Phase 1 — Golden Demo & Workflow Truth", () => {
  test("golden demo APIs and core routes should be customer safe", async ({
    page,
    request,
  }) => {
    const token = await prepareAuthenticatedPage(page, request);

    const authHeaders = {
      Authorization: `Bearer ${token}`,
    };

    const guard = installHardeningPageGuard(page, {
      allowServerFailureUrlFragments: [
        "/analytics/dashboard/definitions/system-templates/ensure",
      ],
    });

    await gotoAndAssertCustomerSafePage(
      page,
      "/dashboard",
      /dashboard|widget|quality|risk|plantprocess iq/i
    );

    const apiBase =
      process.env.PLAYWRIGHT_API_URL ||
      process.env.VITE_API_BASE_URL ||
      "http://localhost:5063";

    const endpoints = [
      "/admin/phase1/connector-truth",
      "/admin/phase1/source-schedule-board",
      "/admin/phase1/staging/summary",
      "/admin/phase1/schema-mapping/workbench",
      "/admin/phase1/import-jobs/configuration-board",
      "/reports/customer-demo/phase1-summary",
    ];

    for (const endpoint of endpoints) {
    const response = await request.get(`${apiBase}${endpoint}`, {
      headers: authHeaders,
    });    
      expect(response.ok(), `${endpoint} should return 2xx`).toBeTruthy();
    }

    const widgetResponse = await request.post(
      `${apiBase}/analytics/dashboard/widgets/execute`,
      {
        headers: authHeaders,
        data: {
          expression:
            "widget=chart; chart=bar; dimension=defectType; measure=defectCount; maxRows=20; sort=desc;",
          filters: {},
          options: {
            maxRows: 20,
            rawRowLimit: 10000,
            sortDirection: "desc",
            includeWarnings: true,
          },
        },
      }
    );

    expect(widgetResponse.ok()).toBeTruthy();

    const pdfResponse = await request.get(
      `${apiBase}/reports/customer-demo/phase1.pdf`,
      {
        headers: authHeaders,
      }
    );

    expect(pdfResponse.ok()).toBeTruthy();
    expect(pdfResponse.headers()["content-type"]).toContain("application/pdf");

    await guard.assertNoUnexpectedFailures();
  });
});