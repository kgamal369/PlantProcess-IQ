import { expect, test } from "@playwright/test";
import { authenticatedGet, login } from "./helpers/auth";

test.describe("Dimension 6 ML readiness", () => {
  test("ML readiness endpoints expose score, labels, jobs and workspace", async ({
    request,
  }) => {
    const score = await authenticatedGet(request, "/analytics/ml-readiness/score");
    expect(score.ok()).toBeTruthy();

    const scoreBody = await score.json();
    expect(scoreBody.overallStatus).toBeTruthy();
    expect(scoreBody.trainingStatus).toContain("Training disabled");
    expect(scoreBody.honestPositioning).toContain("No trained production ML model is active");

    const labels = await authenticatedGet(
      request,
      "/analytics/ml-readiness/labels/preview?limit=5"
    );
    expect(labels.ok()).toBeTruthy();

    const labelsBody = await labels.json();
    expect(labelsBody.requestedLimit).toBe(5);
    expect(Array.isArray(labelsBody.labels)).toBeTruthy();

    const ensureJobs = await authenticatedGet(request, "/analytics/ml-readiness/jobs");
    expect(ensureJobs.ok()).toBeTruthy();

    const workspace = await authenticatedGet(
      request,
      "/analytics/ml-readiness/workspace?labelPreviewLimit=5"
    );
    expect(workspace.ok()).toBeTruthy();

    const workspaceBody = await workspace.json();
    expect(workspaceBody.readiness).toBeTruthy();
    expect(workspaceBody.labelPreview).toBeTruthy();
    expect(workspaceBody.mlJobs).toBeTruthy();
    expect(workspaceBody.modelRegistry).toBeTruthy();
    expect(workspaceBody.correlations).toBeTruthy();
    expect(workspaceBody.disclaimer).toContain("No trained production ML model is active");
  });

  test("ML readiness page renders honest training status", async ({ page, request }) => {
    const token = await login(request);

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
      window.localStorage.setItem("plantprocess.auth.userName", "admin");
      window.localStorage.setItem("plantprocess.auth.role", "Admin");
      window.localStorage.setItem(
        "plantprocess.auth.expiresAtUtc",
        new Date(Date.now() + 60 * 60 * 1000).toISOString()
      );
    }, token);

    await page.goto("/ml-readiness");

    await expect(page.getByText(/ML readiness before training/i)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/Training disabled/i)).toBeVisible();
    await expect(page.getByText(/No trained production ML model is active/i).first()).toBeVisible();

    const body = await page.locator("body").innerText();
    expect(body.toLowerCase()).not.toContain("guaranteed root cause detection");
    expect(body.toLowerCase()).not.toContain("production-ready ai model");
  });
});