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

test.describe("P05 scheduled learning acceptance", () => {
  test("scheduled learning jobs, run-now, correlation results and golden dataset pass", async ({ request }) => {
    const token = await login(request);

    const jobsBefore = await getJson<Array<{
      jobCode: string;
      enabled: boolean;
      readinessGateStatus: string;
      lastRunStatus: string;
    }>>(request, "/phase5/scheduled-learning/jobs", token);

    expect(jobsBefore.length).toBeGreaterThanOrEqual(4);
    expect(jobsBefore.every((job) => job.enabled)).toBeTruthy();
    expect(jobsBefore.every((job) => job.readinessGateStatus === "Passed")).toBeTruthy();

    const run = await request.post(apiBaseUrl + "/phase5/scheduled-learning/run-now", {
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      data: {
        jobCode: "P05-JOB-QUALITY-CORRELATION",
      },
    });

    expect(run.ok(), "run-now failed: " + run.status() + " " + await run.text()).toBeTruthy();

    const scheduled = await getJson<{
      status: string;
      jobCount: number;
      resultCount: number;
      results: Array<{
        qValue: number;
        sampleSize: number;
        stabilityScore: number;
      }>;
    }>(request, "/phase5/scheduled-learning/acceptance", token);

    expect(scheduled.status).toBe("Passed");
    expect(scheduled.jobCount).toBeGreaterThanOrEqual(4);
    expect(scheduled.resultCount).toBeGreaterThanOrEqual(4);
    expect(scheduled.results.every((result) => result.qValue <= 0.1)).toBeTruthy();
    expect(scheduled.results.every((result) => result.sampleSize >= 200)).toBeTruthy();
    expect(scheduled.results.every((result) => result.stabilityScore >= 0.7)).toBeTruthy();

    const golden = await getJson<{
      status: string;
      checkCount: number;
      passedCheckCount: number;
      checks: Array<{
        signalRecovered: boolean;
        noiseRejected: boolean;
        deterministic: boolean;
        fdrControlled: boolean;
      }>;
    }>(request, "/phase5/golden-dataset/acceptance", token);

    expect(golden.status).toBe("Passed");
    expect(golden.checkCount).toBeGreaterThanOrEqual(5);
    expect(golden.passedCheckCount).toBe(golden.checkCount);
    expect(golden.checks.every((check) => check.signalRecovered && check.noiseRejected && check.deterministic && check.fdrControlled)).toBeTruthy();

    const summary = await getJson<{
      status: string;
      honestPositioning: string;
    }>(request, "/phase5/acceptance-summary", token);

    expect(summary.status).toBe("Passed");
    expect(summary.honestPositioning).toMatch(/suspected contributors, not guaranteed root cause/i);
  });
});
