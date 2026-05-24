import { describe, expect, it } from "vitest";
import { plantProcessApi } from "@/api/plantProcessApi";

const requiredApiMethods = [
  "getDashboardMetadata",
  "queryDashboardWidget",
  "executeWidgetExpression",

  "getDashboardDefinitions",
  "getDashboardDefinitionById",
  "createDashboardDefinition",
  "updateDashboardDefinition",
  "updateDashboardLayout",
  "deleteDashboardDefinition",

  "createDashboardWidget",
  "updateDashboardWidget",
  "deleteDashboardWidget",
  "cloneDashboardWidget",

  "createDashboardWidgetDefinition",
  "updateDashboardWidgetDefinition",
  "deactivateDashboardWidgetDefinition",
  "cloneDashboardWidgetDefinition",

  "getAdminOverview",
  "getAdminJobs",
  "getAdminJobsMonitor",

  "getProviderTypes",
  "getConnectionProfiles",
  "createConnectionProfile",
  "updateConnectionProfile",

  "getLicenseStatus",
  "getLicensePlans",
  "getMlReadiness",
  "getDemoLifecycle",
] as const;

describe("legacy plantProcessApi public contract", () => {
  it("keeps all high-risk API methods exported", () => {
    const api = plantProcessApi as unknown as Record<string, unknown>;

    for (const method of requiredApiMethods) {
      expect(api, method).toHaveProperty(method);
      expect(typeof api[method], method).toBe("function");
    }
  });

  it("does not expose undefined members", () => {
    const entries = Object.entries(
      plantProcessApi as unknown as Record<string, unknown>
    );

    expect(entries.length).toBeGreaterThan(25);

    for (const [key, value] of entries) {
      expect(value, `${key} is undefined`).not.toBeUndefined();
    }
  });
});