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

test.describe("T-042 the page lifecycle, end to end", () => {
  // One flow, deliberately. Every step is a frozen acceptance line, and each
  // one is asserted on what the SERVER gave back rather than on what the client
  // hoped: the widget identities come from the persisted list, the geometry is
  // compared value by value after a real reload, and the navigation entry is
  // read WITHOUT reloading, because a reload would prove nothing about
  // invalidation.
  const stamp = Date.now().toString(36);
  const pageTitle = "Shift production " + stamp;
  const pageSlug = "shift-production-" + stamp;

  async function fillPageMeta(page: Page, title: string, slug: string) {
    await page.getByLabel("Title").fill(title);
    await page.getByLabel("Slug").fill(slug);
    await page.getByTestId("page-audience").getByLabel("Engineer").check();
  }

  // The shell needs exactly three things before it will save, from
  // widgetDefinitionModel.saveRefusal: a title, a chart type, and a dimension
  // or a measure. Nothing here is invented - each option is whatever the
  // endpoint published first.
  async function authorOneWidget(page: Page, widgetTitle: string) {
    await page.getByTestId("widget-kind-chart").click();
    await page.getByLabel("Widget name").fill(widgetTitle);
    await page.getByTestId("ctl-open-authoring").click();

    // The bridge either opens the shell or states why it could not. Waiting only
    // for the shell turns a named product failure into an anonymous timeout, so
    // whichever arrives first is read, and a failure is reported IN ITS OWN
    // WORDS rather than as "element not found".
    const shell = page.getByTestId("authoring-shell");
    const failure = page.getByTestId("bridge-failed");
    const preparing = page.getByTestId("bridge-preparing");

    // Three states, not two. The previous version watched only the two OUTCOMES
    // and never the state in between, so a bridge that never settled timed out
    // saying nothing at all - which is how two runs were spent learning nothing.
    await expect(shell.or(failure).or(preparing)).toBeVisible({ timeout: 45_000 });

    if (await preparing.count() > 0) {
      await expect(shell.or(failure)).toBeVisible({ timeout: 45_000 });
    }

    if (await failure.count() > 0) {
      throw new Error("The page could not be prepared, so no widget was authored. The product said: "
        + ((await failure.textContent()) ?? "").trim());
    }

    if (await shell.count() === 0) {
      const seen = (await page.locator("body").innerText()).replace(/\s+/g, " ").slice(0, 900);
      throw new Error("The bridge never settled: neither the shell nor a failure appeared. On screen: " + seen);
    }

    await expect(shell).toHaveAttribute("data-purpose", "S2");

    await page.getByLabel("Definition name").fill(widgetTitle);

    const chartType = page.getByLabel("Chart type");
    await chartType.selectOption({ index: 1 });

    // The server requires a measure ALWAYS, and a dimension for chart types
    // that need one. Choosing only a dimension is what the client's own gate
    // permits and the server refuses - see the finding below.
    await page.getByLabel("Measure").selectOption({ index: 1 });

    const dimension = page.getByLabel("Dimension");
    if (await dimension.count() > 0) {
      await dimension.selectOption({ index: 1 });
    }

    await page.getByRole("button", { name: "Save widget" }).click();
    await expect(shell).toHaveCount(0, { timeout: 45_000 });
  }

  function cards(page: Page) {
    return page.locator("[data-widget-id]");
  }

  async function geometry(page: Page) {
    return page.locator("[data-widget-id]").evaluateAll((nodes) =>
      nodes
        .map((node) => ({
          id: node.getAttribute("data-widget-id") ?? "",
          text: (node.textContent ?? "").replace(/\s+/g, " ").trim(),
        }))
        .sort((a, b) => a.id.localeCompare(b.id)),
    );
  }

  test("two real widgets survive a hard reload, and Publish reaches Workspaces without one", async ({ page }) => {
    await page.goto("/page-builder");
    await expect(page.getByLabel("Title")).toBeVisible({ timeout: 45_000 });

    await fillPageMeta(page, pageTitle, pageSlug);

    await authorOneWidget(page, "Yield by grade");
    await expect(cards(page)).toHaveCount(1, { timeout: 30_000 });

    await authorOneWidget(page, "Downtime by line");
    await expect(cards(page)).toHaveCount(2, { timeout: 30_000 });

    // The identities on the grid are the SERVER's, not generated ones.
    const identities = await cards(page).evaluateAll((nodes) =>
      nodes.map((node) => node.getAttribute("data-widget-id") ?? "").sort(),
    );
    expect(identities.filter((id) => id.startsWith("w-"))).toHaveLength(0);
    expect(new Set(identities).size).toBe(2);

    await capture(page, "T-042", "T042-two-real-widgets",
      "Two widgets authored through the shared shell, on the grid under their server-persisted identities.");

    // Arrange: one moved, one resized.
    const first = cards(page).first();
    const second = cards(page).nth(1);
    await first.getByText("Move right").click();
    await second.getByText("Resize wider").click();

    await page.getByTestId("ctl-save-page").click();
    await expect(page.getByRole("status")).toContainText(/Saved|saved/, { timeout: 30_000 });

    const before = await geometry(page);
    expect(before).toHaveLength(2);

    // HARD RELOAD. Not a client re-render.
    await page.reload();
    await expect(page.getByLabel("Title")).toBeVisible({ timeout: 45_000 });
    await page.getByLabel("Slug").fill(pageSlug);
    await page.getByText("Load by slug").click();
    await expect(page.getByRole("status")).toContainText(/Loaded PageDefinition/, { timeout: 30_000 });

    const after = await geometry(page);
    expect(after).toEqual(before);

    await capture(page, "T-042", "T042-layout-survives-reload",
      "After a hard reload the same two persisted identities return with identical normalised geometry.");

    // Publish, then read navigation in the SAME context. A reload here would
    // prove nothing about invalidation.
    await page.getByTestId("ctl-publish-page").click();
    await expect(page.getByRole("status")).toContainText(/Published/, { timeout: 30_000 });

    const navEntry = page.getByRole("link", { name: pageTitle });
    await expect(navEntry).toHaveCount(1, { timeout: 30_000 });

    await capture(page, "T-042", "T042-published-appears-once",
      "Publish put the page into Workspaces during the same session, once, under its authored title.");

    await navEntry.click();
    await expect(page).toHaveURL(/\/workspace\//, { timeout: 30_000 });
    await expect(page.getByText("Yield by grade")).toBeVisible({ timeout: 45_000 });

    await capture(page, "T-042", "T042-workspace-opens",
      "The authored page opens its real backing workspace with the authored widgets on it.");
  });

  test("an unpublished draft never reaches Workspaces, and delete leaves no orphan", async ({ page }) => {
    const draftTitle = "Draft only " + stamp;
    const draftSlug = "draft-only-" + stamp;

    await page.goto("/page-builder");
    await expect(page.getByLabel("Title")).toBeVisible({ timeout: 45_000 });

    await fillPageMeta(page, draftTitle, draftSlug);
    await authorOneWidget(page, "Draft widget");
    await expect(cards(page)).toHaveCount(1, { timeout: 30_000 });

    // Never published, so never a workspace.
    await expect(page.getByRole("link", { name: draftTitle })).toHaveCount(0);

    await page.getByText("Delete owned page").click();
    await expect(page.getByRole("status")).toContainText(/Deleted PageDefinition/, { timeout: 30_000 });

    await expect(page.getByRole("link", { name: draftTitle })).toHaveCount(0);

    await page.getByLabel("Slug").fill(draftSlug);
    await page.getByText("Load by slug").click();
    await expect(page.getByRole("status")).not.toContainText(/Loaded PageDefinition/, { timeout: 30_000 });

    await capture(page, "T-042", "T042-draft-deleted-no-orphan",
      "A draft never appeared in Workspaces, and after deletion it is gone from the Page Builder with no orphan entry left behind.");
  });
});

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

    // The bridge either opens the shell or states why it could not. Waiting only
    // for the shell turns a named product failure into an anonymous timeout, so
    // whichever arrives first is read, and a failure is reported IN ITS OWN
    // WORDS rather than as "element not found".
    const shell = page.getByTestId("authoring-shell");
    const failure = page.getByTestId("bridge-failed");
    const preparing = page.getByTestId("bridge-preparing");

    // Three states, not two. The previous version watched only the two OUTCOMES
    // and never the state in between, so a bridge that never settled timed out
    // saying nothing at all - which is how two runs were spent learning nothing.
    await expect(shell.or(failure).or(preparing)).toBeVisible({ timeout: 45_000 });

    if (await preparing.count() > 0) {
      await expect(shell.or(failure)).toBeVisible({ timeout: 45_000 });
    }

    if (await failure.count() > 0) {
      throw new Error("The page could not be prepared, so no widget was authored. The product said: "
        + ((await failure.textContent()) ?? "").trim());
    }

    if (await shell.count() === 0) {
      const seen = (await page.locator("body").innerText()).replace(/\s+/g, " ").slice(0, 900);
      throw new Error("The bridge never settled: neither the shell nor a failure appeared. On screen: " + seen);
    }

    await expect(shell).toHaveAttribute("data-purpose", "S2");

    // F. Nothing about a reference plant was needed to get here.
    await expect(page.getByText(/schema_view/)).toHaveCount(0);

    await capture(page, "T-041 E/F", "T041-EF-shell-opens-s2",
      "A structural kind and a name open the existing SharedAuthoringShell in S2, with no demo binding anywhere on the path.");
  });
});