import { test, expect } from "@playwright/test";
import {
  assertSyntheticBrokenControlFailsGuard,
  collectInteractiveIssues,
  phase9Routes,
  waitForPhase9PageReady,
} from "./helpers/phase9ActionMatrix";

test.describe("Phase 09 — P9-01 action-matrix coverage", () => {
  for (const route of phase9Routes) {
    test(`${route.name}: every visible control is labeled/actionable or explains lock`, async ({ page }) => {
      await waitForPhase9PageReady(page, route);

      const issues = await collectInteractiveIssues(page);

      expect(
        issues,
        [
          `${route.name} has dead/unexplained controls.`,
          "",
          ...issues.map((issue) => `- ${issue.selector}: ${issue.reason}`),
        ].join("\n"),
      ).toEqual([]);
    });
  }

  test("negative proof: deliberately broken synthetic button fails the action guard", async () => {
    assertSyntheticBrokenControlFailsGuard();
  });
});