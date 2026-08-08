// PPIQ T-041. THE PAGE BUILDER REACHES AUTHORING - BROWSER ACCEPTANCE.
//
// Deliberately tiny, and deliberately mechanical. It proves acceptance C, D, E
// and F and nothing else. Save, hard reload, publish, navigation and arrange
// persistence are T-042's and are not touched here.
//
// A and B are already proved by suite: the endpoint publishes exactly seven
// kinds, and the reducer carries no demo library and no compiled grammar.
//
// This runs under the T-040 configuration, in the T-040 runner, against the
// already-authenticated presentation environment. No new framework.

import { expect, test, type Page } from "@playwright/test";
import { appendFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const EVIDENCE_DIR = join(process.cwd(), "..", "..", "docs", "m1", "evidence", "T-040");
const MANIFEST = join(EVIDENCE_DIR, "EVIDENCE.jsonl");

mkdirSync(EVIDENCE_DIR, { recursive: true });

async function capture(page: Page, gate: string, name: string, claim: string) {
  const file = name + ".png";
  await page.screenshot({ path: join(EVIDENCE_DIR, file) });
  appendFileSync(
    MANIFEST,
    JSON.stringify({ gate, evidence: file, claim, at: new Date().toISOString() }) + "\n",
    "utf8",
  );
}

// The seven Chapter 4 labels, in the order the endpoint publishes them. Written
// out rather than read from the endpoint on purpose: a test that asks the same
// source as the product proves only that they agree, not that either is right.
const SEVEN_KINDS = ["Chart", "Table", "KPI", "Calculated label", "Filter", "Container", "Text"];

test.describe("T-041 the Page Builder reaches the shared authoring shell", () => {
  test("C a new page opens empty, and says so", async ({ page }) => {
    await page.goto("/page-builder");

    const title = page.getByLabel("Title");
    await expect(title).toBeVisible({ timeout: 45_000 });
    await expect(title).toHaveValue("");
    await expect(page.getByLabel("Slug")).toHaveValue("");
    await expect(page.getByTestId("page-empty")).toHaveText("This page has no widgets yet");
    await expect(page.getByTestId("page-audience-required")).toBeVisible();

    await capture(page, "T-041 C", "T041-C-empty-page",
      "A new page starts with no title, no code, no audience and no widgets, and states that it has none.");
  });

  test("C the audience gate holds until a role is chosen", async ({ page }) => {
    await page.goto("/page-builder");
    await expect(page.getByLabel("Title")).toBeVisible({ timeout: 45_000 });

    await page.getByLabel("Title").fill("Shift production");
    await page.getByLabel("Slug").fill("shift-production");

    // Name and code alone are not enough. Audience is a required answer, not a
    // default, because T-042 reads it when Publish decides who sees the page.
    await expect(page.getByTestId("widget-kind-chart")).toBeDisabled();

    await page.getByTestId("page-audience").getByLabel("Engineer").check();
    await expect(page.getByTestId("page-audience-required")).toHaveCount(0);
    await expect(page.getByTestId("widget-kind-chart")).toBeEnabled();

    await capture(page, "T-041 C", "T041-C-audience-required",
      "Name and code alone leave the kinds disabled; choosing an audience role enables them.");
  });

  test("D exactly the seven structural kinds are offered, from the endpoint", async ({ page }) => {
    await page.goto("/page-builder");
    await expect(page.getByLabel("Title")).toBeVisible({ timeout: 45_000 });

    const picker = page.getByTestId("widget-kind-picker");
    await expect(picker).toBeVisible({ timeout: 30_000 });

    const labels = await picker.getByRole("button").allInnerTexts();
    expect(labels.map((label) => label.trim())).toEqual(SEVEN_KINDS);

    // The demo library that used to stand in this panel, named and proved gone.
    for (const retired of ["Risk KPI", "Defect breakdown", "Defect trend", "Date range filter", "List-of-values filter"]) {
      await expect(picker.getByText(retired, { exact: false })).toHaveCount(0);
    }

    await capture(page, "T-041 D", "T041-D-seven-kinds",
      "The picker offers exactly Chart, Table, KPI, Calculated label, Filter, Container and Text, and none of the retired demo widgets.");
  });

  test("E and F choosing a kind and naming it opens the SAME shell in S2", async ({ page }) => {
    await page.goto("/page-builder");
    await expect(page.getByLabel("Title")).toBeVisible({ timeout: 45_000 });

    await page.getByLabel("Title").fill("Shift production");
    await page.getByLabel("Slug").fill("shift-production");
    await page.getByTestId("page-audience").getByLabel("Engineer").check();

    await page.getByTestId("widget-kind-chart").click();
    await expect(page.getByTestId("widget-name-step")).toBeVisible();
    await page.getByLabel("Widget name").fill("Yield by grade");
    await page.getByTestId("ctl-open-authoring").click();

    const shell = page.getByTestId("authoring-shell");
    await expect(shell).toBeVisible({ timeout: 45_000 });
    await expect(shell).toHaveAttribute("data-purpose", "S2");

    // F. Nothing about a reference plant was needed to get here.
    await expect(page.getByText(/schema_view/)).toHaveCount(0);

    await capture(page, "T-041 E/F", "T041-EF-shell-opens-s2",
      "A structural kind and a name open the existing SharedAuthoringShell in S2, with no demo binding anywhere on the path.");
  });
});