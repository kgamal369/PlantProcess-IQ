import { expect, test, type APIRequestContext } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

async function getJson<T>(request: APIRequestContext, path: string, token: string): Promise<T> {
  const response = await request.get(apiBaseUrl + path, {
    headers: {
      Accept: "application/json",
      Authorization: "Bearer " + token,
    },
  });

  expect(response.ok(), path + " failed: " + response.status() + " " + await response.text()).toBeTruthy();

  return (await response.json()) as T;
}

test.describe("P04 ML foundation acceptance", () => {
  test("feature, outcome, cascade and compute proof gates pass", async ({ request }) => {
    const token = await login(request);

    const features = await getJson<{
      status: string;
      featureCount: number;
      readyFeatureCount: number;
      distinctGrainCount: number;
      features: Array<{ grain: string; isReady: boolean }>;
    }>(request, "/phase4/features/acceptance", token);

    expect(features.status).toBe("Passed");
    expect(features.featureCount).toBeGreaterThanOrEqual(8);
    expect(features.readyFeatureCount).toBe(features.featureCount);
    expect(features.distinctGrainCount).toBeGreaterThanOrEqual(4);
    expect(features.features.map((feature) => feature.grain)).toEqual(
      expect.arrayContaining(["Heat", "Strand", "Coil", "Location"]),
    );

    const outcomes = await getJson<{
      status: string;
      outcomeCount: number;
      readyOutcomeCount: number;
    }>(request, "/phase4/outcomes/acceptance", token);

    expect(outcomes.status).toBe("Passed");
    expect(outcomes.outcomeCount).toBeGreaterThanOrEqual(5);
    expect(outcomes.readyOutcomeCount).toBe(outcomes.outcomeCount);

    const cascade = await getJson<{
      status: string;
      hasEquipmentOnly: boolean;
      hasBufferAbsorbed: boolean;
      hasCascadeAmplified: boolean;
    }>(request, "/phase4/cascade/acceptance", token);

    expect(cascade.status).toBe("Passed");
    expect(cascade.hasEquipmentOnly).toBeTruthy();
    expect(cascade.hasBufferAbsorbed).toBeTruthy();
    expect(cascade.hasCascadeAmplified).toBeTruthy();

    const compute = await getJson<{
      status: string;
      methodCount: number;
      readyMethodCount: number;
    }>(request, "/phase4/compute/acceptance", token);

    expect(compute.status).toBe("Passed");
    expect(compute.methodCount).toBeGreaterThanOrEqual(6);
    expect(compute.readyMethodCount).toBe(compute.methodCount);

    const summary = await getJson<{
      status: string;
      honestPositioning: string;
    }>(request, "/phase4/acceptance-summary", token);

    expect(summary.status).toBe("Passed");
    expect(summary.honestPositioning).toMatch(/No production ML prediction/i);
  });
});
