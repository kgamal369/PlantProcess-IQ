import { expect, test, type Page } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

const evidenceDir = process.env.PPIQ_COMMERCIAL_EVIDENCE_DIR || "test-results/commercial-v2";
const routes = [
  ["home", "/", /Stop the Losses/i],
  ["platform", "/product", /governed line of reasoning/i],
  ["proof", "/proof", /shows the null/i],
  ["security", "/security", /without a control-system command path/i],
  ["pricing", "/pricing", /Transparent investment/i],
  ["about", "/about", /Industrial software built/i],
  ["quality", "/solutions/quality", /failed coil/i],
  ["quality-pack", "/packs/quality", /Trace recurring defects/i],
] as const;

async function assertProfessionalPage(page: Page, expectedHeading: RegExp) {
  await expect(page.locator("h1")).toHaveCount(1);
  await expect(page.locator("h1")).toContainText(expectedHeading);
  await expect(page.locator("header")).toBeVisible();
  await expect(page.locator("footer")).toHaveCount(1);
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
    bodyScrollWidth: document.body.scrollWidth,
    unnamedButtons: [...document.querySelectorAll("button")].filter((button) => !(button.textContent?.trim() || button.getAttribute("aria-label"))).length,
    emptyLinks: [...document.querySelectorAll("a")].filter((link) => !(link.textContent?.trim() || link.getAttribute("aria-label"))).length,
  }));
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 2);
  expect(metrics.bodyScrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 2);
  expect(metrics.unnamedButtons).toBe(0);
  expect(metrics.emptyLinks).toBe(0);
}

test.beforeAll(() => fs.mkdirSync(path.resolve(evidenceDir, "screenshots"), { recursive: true }));

test("homepage tells the complete commercial detective story", async ({ page }, testInfo) => {
  const consoleErrors: string[] = [];
  page.on("console", (message) => { if (message.type() === "error" && !message.text().includes("404")) consoleErrors.push(message.text()); });
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await assertProfessionalPage(page, /Stop the Losses/i);
  await expect(page.locator("body")).toContainText("The Crime Scene");
  await expect(page.locator("body")).toContainText("Tracing the Footprints");
  await expect(page.locator("body")).toContainText("The Trial & Verdict");
  await expect(page.locator("body")).toContainText("Execution & ROI");
  await expect(page.locator("body")).toContainText("9.3x");
  await expect(page.locator("body")).toContainText("1.0x");
  await expect(page.locator("body")).toContainText("The model explains. The engine computes.");
  expect(consoleErrors).toEqual([]);
  await page.screenshot({ path: path.resolve(evidenceDir, "screenshots", `home-${testInfo.project.name}.png`), animations: "disabled" });
});

test("all commercial routes are polished and responsive", async ({ page }, testInfo) => {
  for (const viewport of [{ name: "desktop", width: 1440, height: 1000 }, { name: "mobile", width: 412, height: 915 }]) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    for (const [name, route, heading] of routes) {
      await page.goto(route, { waitUntil: "domcontentloaded" });
      await assertProfessionalPage(page, heading);
      if (["proof", "security", "pricing"].includes(name)) {
        await page.screenshot({ path: path.resolve(evidenceDir, "screenshots", `${name}-${viewport.name}-${testInfo.project.name}.png`), animations: "disabled" });
      }
    }
  }
});

test("legacy routes and lead capture remain operational", async ({ page }) => {
  await page.goto("/products/qes", { waitUntil: "domcontentloaded" });
  await expect(page).toHaveURL(/\/packs\/quality$/);
  await page.goto("/products/mes", { waitUntil: "domcontentloaded" });
  await expect(page).toHaveURL(/\/packs\/reliability$/);
  await page.goto("/contact", { waitUntil: "domcontentloaded" });
  const form = page.getByTestId("demo-request-form");
  await expect(form).toBeVisible();
  await expect(form.locator("input").first()).toBeEditable();
});
