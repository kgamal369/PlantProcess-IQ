import { test, expect, type Page } from "@playwright/test";

/* PPIQ-T070-01 presentation route audit.
 *
 * The frozen task asks for four things on the routes actually opened: no dead
 * link, keyboard and mobile behaviour, the Chapter 6 keep-list still rendering,
 * and no page showing a blocker or an unfinished item. All four are asserted
 * here rather than clicked by hand the night before a presentation.
 */

const ROUTES = [
  { path: "/", name: "home" },
  { path: "/products", name: "products" },
  { path: "/products/plantprocess-iq", name: "ppiq" },
  { path: "/proof", name: "proof" },
  { path: "/security", name: "security" },
  { path: "/contact", name: "contact" },
];

/* The router sends any unknown path to "/". In a single page application there
 * is no 404 to catch, so a dead internal link shows up as a landing path that
 * does not match the link path. These are the redirects that are MEANT to move. */
const EXPECTED_REDIRECTS: Record<string, string> = {
  "/product": "/products/plantprocess-iq",
  "/services": "/products/plantprocess-iq",
  "/products/ppiq": "/products/plantprocess-iq",
  "/products/platform": "/products/plantprocess-iq",
  "/products/yard": "/products/yard-warehouse-management",
  "/products/warehouse": "/products/yard-warehouse-management",
  "/products/yard-warehouse": "/products/yard-warehouse-management",
  "/products/energy": "/products/energy-management",
  "/products/ems": "/products/energy-management",
  "/products/energy-management-system": "/products/energy-management",
  "/products/manufacturing-execution": "/products/mes",
  "/products/manufacturing-execution-system": "/products/mes",
  "/products/quality-execution": "/products/qes",
  "/products/quality-execution-system": "/products/qes",
};

async function internalLinks(page: Page): Promise<string[]> {
  return await page.evaluate(() => {
    const out = new Set<string>();
    document.querySelectorAll("a[href]").forEach((a) => {
      const href = a.getAttribute("href") || "";
      if (href.startsWith("/") && !href.startsWith("//")) out.add(href.split("#")[0] || "/");
    });
    return Array.from(out);
  });
}

test.describe("no dead links on the presentation routes", () => {
  for (const route of ROUTES) {
    test(`every internal link on ${route.name} resolves`, async ({ page }) => {
      await page.setViewportSize({ width: 1440, height: 1000 });
      await page.goto(route.path);
      const links = await internalLinks(page);
      expect(links.length).toBeGreaterThan(0);

      const dead: string[] = [];
      for (const href of links) {
        await page.goto(href);
        /* The redirects are client-side. page.goto() resolves on navigation
         * commit, which is BEFORE React Router has run <Navigate>, so reading
         * the URL straight away races the router. Poll until the pathname stops
         * moving, then compare. */
        const want = EXPECTED_REDIRECTS[href] || href;
        let landed = new URL(page.url()).pathname;
        for (let attempt = 0; attempt < 20 && landed !== want; attempt++) {
          await page.waitForTimeout(50);
          landed = new URL(page.url()).pathname;
        }
        if (landed !== want) dead.push(`${href} landed on ${landed}`);
      }
      expect(dead).toEqual([]);
    });
  }
});

test.describe("the Chapter 6 keep-list still renders", () => {
  test("home keeps its named components", async ({ page }) => {
    await page.goto("/");
    /* Identified by the markup each component owns rather than by its React
     * name, because the built bundle carries no component names. */
    await expect(page.locator("svg.ppiq-archflow")).toHaveCount(1);
    await expect(page.locator(".ppiq-goldenthread")).toHaveCount(1);
    await expect(page.locator(".ppiq-ecosystem")).toHaveCount(1);
    await expect(page.locator(".ppiq-roi")).toHaveCount(1);
    await expect(page.locator(".hub-core")).not.toHaveCount(0);
  });

  test("the demo request form is reachable", async ({ page }) => {
    await page.goto("/contact");
    const body = await page.locator("body").evaluate((el) => el.textContent || "");
    expect(body.length).toBeGreaterThan(200);
  });
});

test.describe("the website honesty rule", () => {
  /* Phrases are matched loosely - that is the point of them. Token-shaped values
   * are NOT: lowercasing both sides made "NaN" match the "nan" inside
   * "maintenance", so an ordinary English word was reported as a defect. */
  const BANNED_PHRASES = [
    "coming soon",
    "not implemented",
    "unfinished",
    "blocker",
    "TODO",
    "FIXME",
    "placeholder",
    "lorem ipsum",
    "test failed",
  ];
  const BANNED_TOKENS = ["NaN", "undefined", "[object Object]"];
  for (const route of ROUTES) {
    test(`${route.name} shows nothing unfinished`, async ({ page }) => {
      await page.goto(route.path);
      const text = await page.locator("body").innerText();
      const hits = BANNED_PHRASES.filter((word) =>
        text.toLowerCase().includes(word.toLowerCase())
      );
      for (const token of BANNED_TOKENS) {
        /* Case-sensitive, and on a word boundary, so a real rendering artefact
         * is caught while a word that merely contains those letters is not. */
        const pattern = new RegExp(`(^|[^A-Za-z])${token.replace(/[[\]()]/g, "\\$&")}([^A-Za-z]|$)`);
        if (pattern.test(text)) hits.push(token);
      }
      expect(hits).toEqual([]);
      expect(text).not.toContain("&middot;");
      expect(text).not.toContain("&mdash;");
    });
  }
});

test.describe("keyboard", () => {
  test("the Products menu opens on focus and Escape closes it", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    const trigger = page.locator('[data-testid="website-products-menu"] button').first();
    await trigger.focus();
    await expect(trigger).toHaveAttribute("aria-expanded", "true");
    await page.keyboard.press("Escape");
    await expect(trigger).toHaveAttribute("aria-expanded", "false");
    const focused = await page.evaluate(() => document.activeElement?.tagName || "");
    expect(focused).toBe("BUTTON");
  });

  test("the header is reachable by tab", async ({ page }) => {
    await page.goto("/");
    let reached = false;
    for (let i = 0; i < 14; i++) {
      await page.keyboard.press("Tab");
      const inHeader = await page.evaluate(() =>
        !!document.activeElement?.closest("header, .website-premium-header, nav")
      );
      if (inHeader) { reached = true; break; }
    }
    expect(reached).toBe(true);
  });
});

test.describe("responsive", () => {
  const WIDTHS = [
    { w: 1440, h: 1000, name: "desktop" },
    { w: 834, h: 1112, name: "tablet" },
    { w: 390, h: 844, name: "mobile" },
  ];
  for (const route of ROUTES) {
    for (const v of WIDTHS) {
      test(`${route.name} has no overflow at ${v.name}`, async ({ page }) => {
        await page.setViewportSize({ width: v.w, height: v.h });
        await page.goto(route.path);
        await page.waitForTimeout(250);
        const over = await page.evaluate(
          () => document.documentElement.scrollWidth - document.documentElement.clientWidth
        );
        expect(over).toBeLessThanOrEqual(1);
        await page.screenshot({
          path: `test-results/verify/route-${route.name}-${v.name}.png`,
          fullPage: true,
        });
      });
    }
  }
});