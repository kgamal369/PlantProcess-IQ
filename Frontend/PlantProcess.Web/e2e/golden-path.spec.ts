import { expect, test } from "@playwright/test";

const API = "http://localhost:5063";

async function login(request: any) {
  const response = await request.post(`${API}/auth/login`, {
    data: {
      userName: "admin",
      password: "ChangeMe123!",
    },
  });

  expect(response.ok()).toBeTruthy();

  const body = await response.json();
  expect(body.accessToken).toBeTruthy();

  return body.accessToken as string;
}

async function getJson(request: any, url: string, token: string) {
  const response = await request.get(`${API}${url}`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  expect(response.ok(), `${url} returned ${response.status()}`).toBeTruthy();
  return response.json();
}

async function postJson(request: any, url: string, token: string, data: unknown = {}) {
  const response = await request.post(`${API}${url}`, {
    headers: { Authorization: `Bearer ${token}` },
    data,
  });

  expect(response.ok(), `${url} returned ${response.status()}`).toBeTruthy();
  return response.json();
}

test.describe("PlantProcess IQ Golden Path", () => {
  test("proves login, admin visibility, dashboard, data quality, risk, readiness, and reporting path", async ({ page, request }) => {
    const token = await login(request);

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    }, token);

    const failedResponses: string[] = [];

    page.on("response", (response) => {
      const status = response.status();
      const url = response.url();

      if ([401, 403, 404, 500].includes(status) && !url.includes("favicon")) {
        failedResponses.push(`${status} ${url}`);
      }
    });

    await page.goto("/admin");
    await expect(page.locator("body")).toContainText(/admin|jobs|configuration|schema/i);

    const jobs = await getJson(request, "/admin/jobs-monitor", token);
    expect(jobs).toBeDefined();

    await page.goto("/dashboard");
    await expect(page.locator("body")).toContainText(/dashboard|widget|risk|quality/i);

    await page.goto("/data-quality");
    await expect(page.locator("body")).toContainText(/data quality|quality/i);

    await page.goto("/risk");
    await expect(page.locator("body")).toContainText(/risk/i);

    const riskDashboard = await getJson(request, "/analytics/dashboard/risk", token);
    expect(riskDashboard).toBeDefined();

    const dataQualityDashboard = await getJson(request, "/analytics/dashboard/data-quality", token);
    expect(dataQualityDashboard).toBeDefined();

    const readiness = await getJson(request, "/readiness", token);
    expect(readiness.overallScore).toBeDefined();
    expect(readiness.dimensions?.length).toBe(7);

    const readinessReport = await postJson(request, "/readiness/report", token, {
      customerName: "Golden Path Customer",
      preparedBy: "PlantProcess IQ",
    });

    expect(readinessReport.assessmentId).toBeTruthy();
    expect(readinessReport.dimensions?.length).toBe(7);
    expect(readinessReport.executiveSummary).toBeTruthy();

    const pdfResponse = await request.post(`${API}/readiness/report/pdf`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        customerName: "Golden Path Customer",
        preparedBy: "PlantProcess IQ",
      },
    });

    expect(pdfResponse.ok()).toBeTruthy();
    expect(pdfResponse.headers()["content-type"]).toContain("application/pdf");

    await expect.poll(() => failedResponses).toEqual([]);
  });
});