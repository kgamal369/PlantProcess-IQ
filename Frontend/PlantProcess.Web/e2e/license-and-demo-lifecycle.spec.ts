import { expect, test } from "@playwright/test";
import { authenticatedGet } from "./helpers/auth";

test.describe("PlantProcess IQ license gates and demo lifecycle", () => {
  test("license current endpoint returns tier, limits and features", async ({ request }) => {
    const response = await authenticatedGet(request, "/admin/license/current");

    expect(
      response.ok(),
      `/admin/license/current should return 200 but returned ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();

    expect(body.tier).toBeTruthy();
    expect(body.displayName).toBeTruthy();
    expect(body.limits).toBeTruthy();
    expect(Array.isArray(body.features)).toBeTruthy();
    expect(Array.isArray(body.allowedConnectorProviderTypes)).toBeTruthy();
    expect(Array.isArray(body.blockedConnectorProviderTypes)).toBeTruthy();
  });

  test("license features endpoint returns feature matrix", async ({ request }) => {
    const response = await authenticatedGet(request, "/admin/license/features");

    expect(
      response.ok(),
      `/admin/license/features should return 200 but returned ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();

    expect(Array.isArray(body)).toBeTruthy();
    expect(body.length).toBeGreaterThan(5);
  });

  test("license limits endpoint returns commercial usage limits", async ({ request }) => {
    const response = await authenticatedGet(request, "/admin/license/limits");

    expect(
      response.ok(),
      `/admin/license/limits should return 200 but returned ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();

    expect(body.tier).toBeTruthy();
    expect(body.maxDataSources === null || typeof body.maxDataSources === "number").toBeTruthy();
    expect(body.maxScheduledJobs === null || typeof body.maxScheduledJobs === "number").toBeTruthy();
    expect(body.maxDashboards === null || typeof body.maxDashboards === "number").toBeTruthy();
  });

  test("demo lifecycle endpoint returns one connected product story", async ({ request }) => {
    const response = await authenticatedGet(request, "/demo/lifecycle");

    expect(
      response.ok(),
      `/demo/lifecycle should return 200 but returned ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();

    expect(body.demoMode).toBe("ControlledProductLifecycle");
    expect(body.license).toBeTruthy();
    expect(Array.isArray(body.steps)).toBeTruthy();
    expect(body.steps.length).toBeGreaterThanOrEqual(8);

    const stepCodes = body.steps.map((step: { code: string }) => step.code);

    expect(stepCodes).toContain("LICENSE");
    expect(stepCodes).toContain("CONNECT");
    expect(stepCodes).toContain("STAGE");
    expect(stepCodes).toContain("MAP");
    expect(stepCodes).toContain("MONITOR");
    expect(stepCodes).toContain("DASHBOARD");
    expect(stepCodes).toContain("ML_READINESS");
    expect(stepCodes).toContain("REPORT");

    expect(body.connectorTruth).toBeTruthy();
    expect(body.stagingSummary).toBeTruthy();
    expect(body.schemaMapping).toBeTruthy();
    expect(body.jobChain).toBeTruthy();
    expect(body.dashboardOutput).toBeTruthy();
    expect(body.mlReadiness).toBeTruthy();
    expect(body.reportClose).toBeTruthy();

    expect(body.mlReadiness.modelStatus).toContain("NoTrainedProductionModelActive");
    expect(body.reportClose.disclaimer).toContain("read-only intelligence layer");
  });
});