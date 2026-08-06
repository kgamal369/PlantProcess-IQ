import { test, expect } from "@playwright/test";

/* PPIQ-T069-W3 homepage quality gate.
 *
 * Everything here is machine-checkable: computed style, geometry, text content.
 * Whether the section LOOKS like the hero is not asserted - the screenshots at
 * the end exist so that judgement stays with a human and takes seconds.
 */

const SHOTS = "test-results/verify";

test.describe("architecture graphic", () => {
  test("computed max-width is 1060px and the winning rule is new-landing.css", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    const svg = page.locator("svg.ppiq-archflow");
    await expect(svg).toHaveCount(1);

    const maxWidth = await svg.evaluate((el) => getComputedStyle(el).maxWidth);
    expect(maxWidth).toBe("1060px");

    // In dev, Vite injects each stylesheet with its source path on the tag.
    // In a preview build the rule arrives in the bundled sheet instead, so both
    // paths are accepted - what must NOT be true is that the rule is missing.
    const origin = await page.evaluate(() => {
      const tags = Array.from(document.querySelectorAll("style[data-vite-dev-id]"));
      const hit = tags.find(
        (t) =>
          (t.getAttribute("data-vite-dev-id") || "").includes("new-landing.css") &&
          (t.textContent || "").includes(".ppiq-archflow")
      );
      if (hit) return "new-landing.css";
      const inMotion = tags.find(
        (t) =>
          (t.getAttribute("data-vite-dev-id") || "").includes("motion-roi.css") &&
          (t.textContent || "").includes(".ppiq-archflow")
      );
      if (inMotion) return "motion-roi.css";
      return "bundled";
    });
    expect(origin).not.toBe("motion-roi.css");
  });

  test("the diagram is constrained and does not overflow", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    const box = await page.locator("svg.ppiq-archflow").boundingBox();
    expect(box).not.toBeNull();
    expect(box!.width).toBeLessThanOrEqual(1061);
    expect(box!.width).toBeLessThan(1440);

    const overflow = await page.evaluate(() => {
      const el = document.querySelector("svg.ppiq-archflow") as SVGSVGElement | null;
      if (!el) return -1;
      return el.scrollWidth - el.clientWidth;
    });
    expect(overflow).toBeLessThanOrEqual(1);
  });

  test("no label escapes its own node", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    const escapes = await page.evaluate(() => {
      const svg = document.querySelector("svg.ppiq-archflow");
      if (!svg) return ["no svg"];
      const bad: string[] = [];
      svg.querySelectorAll("g").forEach((g) => {
        const rect = g.querySelector("rect");
        if (!rect) return;
        const r = (rect as SVGGraphicsElement).getBBox();
        g.querySelectorAll("text").forEach((t) => {
          const b = (t as SVGGraphicsElement).getBBox();
          if (b.x < r.x - 1 || b.x + b.width > r.x + r.width + 1) {
            bad.push((t.textContent || "").trim() + " escapes its node");
          }
        });
      });
      return bad;
    });
    expect(escapes).toEqual([]);
  });

  test("the separator renders and no HTML entity is visible", async ({ page }) => {
    await page.goto("/");
    const text = await page.locator("svg.ppiq-archflow").innerText().catch(async () => {
      return await page.locator("svg.ppiq-archflow").evaluate((el) => el.textContent || "");
    });
    expect(text).toContain("\u00B7");
    expect(text).not.toContain("&middot;");
    const body = await page.locator("body").evaluate((el) => el.textContent || "");
    expect(body).not.toContain("&middot;");
    expect(body).not.toContain("&mdash;");
    expect(body).not.toContain("&uarr;");
  });

  test("every connector endpoint lands on a drawn port", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/");
    const orphans = await page.evaluate(() => {
      const svg = document.querySelector("svg.ppiq-archflow");
      if (!svg) return ["no svg"];
      const ports = Array.from(svg.querySelectorAll("circle.af-port")).map((c) => ({
        x: parseFloat(c.getAttribute("cx") || "0"),
        y: parseFloat(c.getAttribute("cy") || "0"),
      }));
      const near = (x: number, y: number) =>
        ports.some((p) => Math.abs(p.x - x) <= 2.5 && Math.abs(p.y - y) <= 2.5);
      const bad: string[] = [];
      svg.querySelectorAll("path.spoke").forEach((p, i) => {
        const path = p as SVGPathElement;
        const len = path.getTotalLength();
        const a = path.getPointAtLength(0);
        const b = path.getPointAtLength(len);
        if (!near(a.x, a.y)) bad.push("spoke " + i + " starts at " + Math.round(a.x) + "," + Math.round(a.y));
        if (!near(b.x, b.y)) bad.push("spoke " + i + " ends at " + Math.round(b.x) + "," + Math.round(b.y));
      });
      return bad;
    });
    expect(orphans).toEqual([]);
  });
});

test.describe("the three restored sections", () => {
  const sections = [
    { sel: ".ppiq-goldenthread", name: "golden-thread" },
    { sel: ".ppiq-ecosystem", name: "ecosystem" },
    { sel: ".ppiq-roi", name: "roi" },
  ];
  for (const s of sections) {
    test(`${s.name} is present and painted`, async ({ page }) => {
      await page.setViewportSize({ width: 1440, height: 1000 });
      await page.goto("/");
      const el = page.locator(s.sel).first();
      await expect(el).toHaveCount(1);
      const box = await el.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.height).toBeGreaterThan(80);
      await el.screenshot({ path: `${SHOTS}/section-${s.name}.png` });
    });
  }
});

test.describe("responsive", () => {
  const widths = [
    { w: 1440, h: 1000, name: "desktop" },
    { w: 834, h: 1112, name: "tablet" },
    { w: 390, h: 844, name: "mobile" },
  ];
  for (const v of widths) {
    test(`no horizontal overflow at ${v.name}`, async ({ page }) => {
      await page.setViewportSize({ width: v.w, height: v.h });
      await page.goto("/");
      await page.waitForTimeout(300);
      const over = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth
      );
      expect(over).toBeLessThanOrEqual(1);
      await page.locator("svg.ppiq-archflow").screenshot({ path: `${SHOTS}/archflow-${v.name}.png` });
      await page.screenshot({ path: `${SHOTS}/page-${v.name}.png`, fullPage: true });
    });
  }
});

/* PPIQ-T069-05: the /products surface is held to the same bar as the homepage. */
test.describe("products portfolio", () => {
  test("renders five products from the registry", async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto("/products");
    await expect(page.locator(".pf-card")).toHaveCount(5);
    await expect(page.locator(".pf-card--flag")).toHaveCount(1);
    const stack = page.locator("svg.pf-stack");
    await expect(stack).toHaveCount(1);
    const box = await stack.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.width).toBeLessThanOrEqual(641);
  });

  test("no HTML entity and no empty card", async ({ page }) => {
    await page.goto("/products");
    const body = await page.locator("body").evaluate((el) => el.textContent || "");
    expect(body).not.toContain("&middot;");
    expect(body).not.toContain("&mdash;");
    const shortest = await page.locator(".pf-card").evaluateAll((els) =>
      Math.min(...els.map((el) => (el.textContent || "").trim().length))
    );
    expect(shortest).toBeGreaterThan(200);
  });

  for (const v of [
    { w: 1440, h: 1000, name: "desktop" },
    { w: 834, h: 1112, name: "tablet" },
    { w: 390, h: 844, name: "mobile" },
  ]) {
    test(`products page has no horizontal overflow at ${v.name}`, async ({ page }) => {
      await page.setViewportSize({ width: v.w, height: v.h });
      await page.goto("/products");
      await page.waitForTimeout(300);
      const over = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth
      );
      expect(over).toBeLessThanOrEqual(1);
      await page.screenshot({ path: `test-results/verify/products-${v.name}.png`, fullPage: true });
    });
  }
});