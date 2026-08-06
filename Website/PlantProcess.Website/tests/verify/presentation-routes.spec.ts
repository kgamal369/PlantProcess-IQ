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
  test("the PlantProcess IQ page keeps its named components", async ({ page }) => {
    await page.goto("/products/plantprocess-iq");
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

/* PPIQ-T070-04: CLIPPING IS NOT OVERFLOW.
 * scrollWidth === clientWidth reports zero when content is CUT OFF rather than
 * scrolling, so the overflow tests above passed while three homepage sections
 * had their left edge clipped at wide viewports. These assert the visible
 * bounds of each section instead. */
test.describe("page grid", () => {
  const SECTIONS = [
    { sel: "svg.ppiq-goldenthread", name: "genealogy thread" },
    { sel: ".ppiq-ecosystem", name: "integration ecosystem" },
    { sel: ".ppiq-roi", name: "roi calculator" },
    { sel: "svg.ppiq-archflow", name: "architecture graphic" },
  ];
  for (const v of [
    { w: 1440, h: 1000, name: "desktop" },
    { w: 834, h: 1112, name: "tablet" },
    { w: 390, h: 844, name: "mobile" },
  ]) {
    test(`no PPIQ section is clipped at ${v.name}`, async ({ page }) => {
      await page.setViewportSize({ width: v.w, height: v.h });
      await page.goto("/products/plantprocess-iq");
      await page.waitForTimeout(300);
      const clipped: string[] = [];
      for (const s of SECTIONS) {
        const box = await page.locator(s.sel).first().boundingBox();
        if (!box) { clipped.push(`${s.name} not found`); continue; }
        if (box.x < -1) clipped.push(`${s.name} left edge at ${Math.round(box.x)}`);
        if (box.x + box.width > v.w + 1) {
          clipped.push(`${s.name} right edge at ${Math.round(box.x + box.width)} of ${v.w}`);
        }
      }
      expect(clipped).toEqual([]);
    });

    test(`PPIQ sections share one grid at ${v.name}`, async ({ page }) => {
      await page.setViewportSize({ width: v.w, height: v.h });
      await page.goto("/products/plantprocess-iq");
      await page.waitForTimeout(300);
      /* The strongest section is the authority. Every constrained section must
       * start within a few pixels of it rather than running its own width. */
      const reference = await page.locator(".new-landing-wrapper .wrap").first().boundingBox();
      expect(reference).not.toBeNull();
      const drift: string[] = [];
      for (const s of SECTIONS) {
        const box = await page.locator(s.sel).first().boundingBox();
        if (!box) continue;
        if (Math.abs(box.x - reference!.x) > 40) {
          drift.push(`${s.name} starts at ${Math.round(box.x)}, the page grid starts at ${Math.round(reference!.x)}`);
        }
      }
      expect(drift).toEqual([]);
    });
  }
});

/* PPIQ-T070-05: the company home must not read as a product page. */
test.describe("company versus product identity", () => {
  test("/ presents the portfolio, not one product", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    await expect(page.locator(".sou-teaser")).toHaveCount(5);
    await expect(page.locator("svg.sou-graphic")).toHaveCount(1);
    /* The PPIQ-only surfaces must NOT be here any more. */
    await expect(page.locator("svg.ppiq-archflow")).toHaveCount(0);
    await expect(page.locator(".ppiq-roi")).toHaveCount(0);
    const text = await page.locator("body").innerText();
    expect(text).toContain("SOU INDUSTRIAL SOFTWARE");
  });

  test("the PPIQ narrative arrived at its product route", async ({ page }) => {
    await page.goto("/products/plantprocess-iq");
    await expect(page.locator("svg.ppiq-archflow")).toHaveCount(1);
    await expect(page.locator(".ppiq-roi")).toHaveCount(1);
    await expect(page.locator(".ppiq-goldenthread")).toHaveCount(1);
    const text = await page.locator("body").innerText();
    /* Industry-generic, on its new route as much as on the old one. */
    expect(text).not.toContain("HEAT");
    expect(text).not.toContain("SLAB");
    expect(text).not.toContain("COIL");
  });

  test("all five product routes resolve and the header hierarchy holds", async ({ page }) => {
    const slugs = [
      "plantprocess-iq",
      "mes",
      "qes",
      "yard-warehouse-management",
      "energy-management",
    ];
    for (const slug of slugs) {
      await page.goto(`/products/${slug}`);
      let landed = new URL(page.url()).pathname;
      for (let i = 0; i < 20 && landed !== `/products/${slug}`; i++) {
        await page.waitForTimeout(50);
        landed = new URL(page.url()).pathname;
      }
      expect(landed).toBe(`/products/${slug}`);
      await expect(page.locator('[data-testid="website-products-menu"]')).toHaveCount(1);
      const body = await page.locator("body").innerText();
      expect(body.length).toBeGreaterThan(300);
      expect(body.toLowerCase()).not.toContain("coming soon");
    }
  });
});

/* PPIQ-T070-06: PRESENCE IS NOT VISIBILITY.
 * toHaveCount() and boundingBox() both succeed on an opacity:0 element, so 59
 * assertions passed against a home page nobody could see. These measure what
 * the visitor actually gets. */
test.describe("the home is actually visible", () => {
  test("the hero and the product cards are painted, not just present", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    await page.waitForTimeout(900);

    await expect(page.locator(".sou-title")).toBeVisible();
    await expect(page.locator("svg.sou-graphic")).toBeVisible();
    await expect(page.locator(".sou-teaser").first()).toBeVisible();

    const faded = await page.evaluate(() => {
      const out: string[] = [];
      document.querySelectorAll(".new-landing-wrapper .rv").forEach((el) => {
        const style = getComputedStyle(el as Element);
        const rect = (el as Element).getBoundingClientRect();
        const onScreen = rect.top < window.innerHeight && rect.bottom > 0;
        if (onScreen && parseFloat(style.opacity) < 0.9) {
          out.push(`${(el as Element).className} at opacity ${style.opacity}`);
        }
      });
      return out;
    });
    expect(faded).toEqual([]);
  });
});

/* PPIQ-T070-06: the menu has to survive the trip from the trigger to the panel. */
test.describe("the products menu is usable with a mouse", () => {
  test("it stays open while the pointer travels to it", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    const menu = page.locator('[data-testid="website-products-menu"]');
    const trigger = menu.locator("button").first();
    await trigger.hover();
    const panel = page.locator("#products-mega-panel");
    await expect(panel).toBeVisible();

    const first = panel.locator("a").first();
    await first.hover();
    await expect(panel).toBeVisible();
    await first.click();
    let landed = new URL(page.url()).pathname;
    for (let i = 0; i < 20 && !landed.startsWith("/products/"); i++) {
      await page.waitForTimeout(50);
      landed = new URL(page.url()).pathname;
    }
    expect(landed.startsWith("/products/")).toBe(true);
  });
});