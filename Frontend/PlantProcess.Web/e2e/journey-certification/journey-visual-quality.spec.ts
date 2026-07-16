import fs from "node:fs";
import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

type RouteCase = { tag: string; route: string; name: string };

const routes: RouteCase[] = [
  { tag: "J01", route: "/data-integration/connections", name: "connections" },
  { tag: "J02", route: "/data-integration/registry", name: "registry" },
  { tag: "J03", route: "/data-integration/importing", name: "importing" },
  { tag: "J04", route: "/data-integration/author-mapping", name: "mapping" },
  { tag: "J05", route: "/data-integration/jobs", name: "jobs" },
  { tag: "J06", route: "/materials", name: "materials" },
  { tag: "J07", route: "/dashboard", name: "dashboard" },
  { tag: "J08", route: "/investigate/analysis-jobs", name: "analysis-authoring" },
  { tag: "J09", route: "/investigate/analysis-jobs", name: "analysis-jobs" },
  { tag: "J10", route: "/correlations", name: "findings" },
  { tag: "J11", route: "/ml-readiness", name: "ml-readiness" },
  { tag: "J12", route: "/data-integration/jobs", name: "ml-jobs" },
  { tag: "J13", route: "/suggestions", name: "suggestions" },
  { tag: "J14", route: "/data-integration/supervisor", name: "supervisor" },
  { tag: "J15", route: "/assistant", name: "assistant" },
  { tag: "UI4", route: "/data-integration/alerting", name: "plant-data-log" },
];

const viewports = [
  { name: "desktop", width: 1440, height: 950 },
  { name: "compact", width: 1024, height: 768 },
];

async function auditPage(page: Page, item: RouteCase, viewport: { name: string; width: number; height: number }) {
  await page.setViewportSize({ width: viewport.width, height: viewport.height });
  await page.goto(item.route, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => !document.body.innerText.includes("Connecting to backend"), undefined, { timeout: 30_000 });
  await page.waitForLoadState("networkidle").catch(() => undefined);

  const result = await page.evaluate(() => {
    const visible = (element: Element) => {
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.visibility !== "hidden" && style.display !== "none" && rect.width > 0 && rect.height > 0;
    };

    const h1 = Array.from(document.querySelectorAll("h1")).filter(visible);
    const buttons = Array.from(document.querySelectorAll("button")).filter(visible);
    const unnamedButtons = buttons
      .map((button, index) => ({
        index,
        name: (button.getAttribute("aria-label") || button.getAttribute("title") || button.textContent || "").trim(),
      }))
      .filter((button) => !button.name);

    const overflowingTables = Array.from(document.querySelectorAll("table")).filter((table) => {
      const rect = table.getBoundingClientRect();
      if (rect.right <= document.documentElement.clientWidth + 2) return false;
      let parent: Element | null = table.parentElement;
      while (parent) {
        const style = getComputedStyle(parent);
        if (["auto", "scroll"].includes(style.overflowX)) return false;
        parent = parent.parentElement;
      }
      return true;
    }).length;

    const longTechnicalBlocks = Array.from(document.querySelectorAll("pre")).filter((pre) => {
      if ((pre.textContent || "").length < 500) return false;
      return !pre.closest("details");
    }).length;

    const titleBlock = document.querySelector(".ppiq-std-page-header__titles");
    const actionBlock = document.querySelector(".ppiq-std-page-header__actions");
    const titleRect = titleBlock && visible(titleBlock) ? titleBlock.getBoundingClientRect() : null;
    const actionRect = actionBlock && visible(actionBlock) ? actionBlock.getBoundingClientRect() : null;

    const subtitle = document.querySelector(".ppiq-std-page-header__subtitle")?.textContent?.trim() ?? "";
    const description = document.querySelector(".ppiq-std-page-header__description")?.textContent?.trim() ?? "";

    const forbiddenVisibleCopy = Array.from(document.querySelectorAll("body *"))
      .filter((element) => visible(element) && element.children.length === 0)
      .map((element) => (element.textContent || "").trim())
      .filter((text) => /\b(M1-|M2-|phase\s*\d+|fixture|two-stage import model)\b/i.test(text))
      .slice(0, 10);

    const cells = Array.from(document.querySelectorAll("th,td")).filter(visible);
    const misalignedCells = cells.filter((cell) => !["middle", "baseline"].includes(getComputedStyle(cell).verticalAlign)).length;

    return {
      h1Count: h1.length,
      unnamedButtons,
      pageOverflow: Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth),
      overflowingTables,
      longTechnicalBlocks,
      headerOverlaps: Boolean(titleRect && actionRect && !(titleRect.right <= actionRect.left || actionRect.right <= titleRect.left || titleRect.bottom <= actionRect.top || actionRect.bottom <= titleRect.top)),
      subtitleLength: subtitle.length,
      descriptionLength: description.length,
      forbiddenVisibleCopy,
      misalignedCells,
    };
  });

  expect(result.h1Count, `${item.route} must have exactly one visible h1`).toBe(1);
  expect(result.pageOverflow, `${item.route} page overflow`).toBeLessThanOrEqual(2);
  expect(result.overflowingTables, `${item.route} tables must be contained`).toBe(0);
  expect(result.unnamedButtons, `${item.route} has unnamed buttons`).toEqual([]);
  expect(result.longTechnicalBlocks, `${item.route} must fold long technical blocks`).toBe(0);
  expect(result.headerOverlaps, `${item.route} title and actions overlap`).toBeFalsy();
  expect(result.subtitleLength, `${item.route} subtitle is too verbose`).toBeLessThanOrEqual(220);
  expect(result.descriptionLength, `${item.route} description is too verbose`).toBeLessThanOrEqual(320);
  expect(result.forbiddenVisibleCopy, `${item.route} exposes internal delivery wording`).toEqual([]);
  expect(result.misalignedCells, `${item.route} has vertically misaligned table cells`).toBe(0);

  const screenshotDir = path.resolve(process.cwd(), "test-results", "journey-certification", "screenshots");
  fs.mkdirSync(screenshotDir, { recursive: true });
  await page.screenshot({
    path: path.join(screenshotDir, `${item.tag}-${item.name}-${viewport.name}.png`),
    fullPage: true,
  });
}

for (const item of routes) {
  for (const viewport of viewports) {
    test(`[UX-${item.tag}] ${item.name} is professionally composed at ${viewport.name}`, async ({ page }) => {
      await auditPage(page, item, viewport);
    });
  }
}
