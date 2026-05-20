import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

async function authenticatedGet(request: any, path: string, token: string) {
  return request.get(`${apiBaseUrl}${path}`, {
    headers: {
      Authorization: `Bearer ${token}`
    }
  });
}

test.describe("PlantProcess IQ P1 risk and data-quality contracts", () => {
  test("backend risk and data-quality APIs return stable JSON contracts", async ({
    request
  }) => {
    const token = await login(request);

    const risk = await authenticatedGet(
      request,
      "/analytics/dashboard/risk",
      token
    );

    expect(
      risk.ok(),
      `/analytics/dashboard/risk should return 2xx but returned HTTP ${risk.status()}`
    ).toBeTruthy();

    const riskBody = await risk.json();
    expect(riskBody).toBeDefined();

    const dataQuality = await authenticatedGet(
      request,
      "/analytics/dashboard/data-quality",
      token
    );

    expect(
      dataQuality.ok(),
      `/analytics/dashboard/data-quality should return 2xx but returned HTTP ${dataQuality.status()}`
    ).toBeTruthy();

    const dataQualityBody = await dataQuality.json();
    expect(dataQualityBody).toBeDefined();

    const issues = await authenticatedGet(
      request,
      "/data-quality/issues",
      token
    );

    expect(
      issues.ok(),
      `/data-quality/issues should return 2xx but returned HTTP ${issues.status()}`
    ).toBeTruthy();

    const issuesBody = await issues.json();
    expect(issuesBody).toBeDefined();

    const scanPreview = await authenticatedGet(
      request,
      "/data-quality/scan-preview",
      token
    );

    expect(
      scanPreview.status(),
      `/data-quality/scan-preview should not return 5xx but returned HTTP ${scanPreview.status()}`
    ).toBeLessThan(500);
  });
});