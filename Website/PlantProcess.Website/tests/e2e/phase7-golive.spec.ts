import { test, expect, Page } from "@playwright/test";

/* P7-T04 responsive (no horizontal overflow on real routes across widths) and
 * P7-T05 lead-capture proof, against the local static server. Chromium only.
 * On overflow we print the offending nodes so the cause is named, not guessed. */
const ROUTES = ["/", "/pricing", "/security", "/contact", "/products/yard-warehouse-management"];
const WIDTHS = [375, 768, 1440];

async function overflow(page: Page) {
  return await page.evaluate(() => {
    const vw = document.documentElement.clientWidth;
    const offenders: string[] = [];
    for (const el of Array.from(document.querySelectorAll("body *"))) {
      const r = el.getBoundingClientRect();
      if (r.width === 0 && r.height === 0) continue;
      if (r.right > vw + 2) {
        const id = (el as HTMLElement).id ? "#" + (el as HTMLElement).id : "";
        const cn = typeof el.className === "string" && el.className.trim()
          ? "." + el.className.trim().split(/\s+/).slice(0, 3).join(".") : "";
        offenders.push(`${el.tagName.toLowerCase()}${id}${cn} [right=${Math.round(r.right)} w=${Math.round(r.width)}]`);
        if (offenders.length >= 8) break;
      }
    }
    return { px: document.documentElement.scrollWidth - vw, vw, offenders };
  });
}

test.describe("P7-T04 responsive", () => {
  for (const w of WIDTHS) {
    for (const route of ROUTES) {
      test(`no overflow @ ${w}px ${route}`, async ({ page }) => {
        await page.setViewportSize({ width: w, height: 900 });
        const resp = await page.goto(route, { waitUntil: "networkidle" });
        expect(resp?.ok(), `navigation to ${route}`).toBeTruthy();
        const o = await overflow(page);
        expect(o.px, `horizontal overflow; offenders: ${JSON.stringify(o.offenders)}`).toBeLessThanOrEqual(2);
        await expect(page.locator("body")).toBeVisible();
      });
    }
  }
});

test("P7-T05 lead capture submits and shows fit score", async ({ page }) => {
  // No backend behind the static server: fulfill the lead POST so the success path runs.
  await page.route("**/api/v5/outbound/leads", (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ fitScore: 0.87, status: "new" }) }),
  );

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/contact", { waitUntil: "networkidle" });

  const form = page.locator('form[data-testid="demo-request-form"]');
  const submit = form.locator('button[type="submit"]');
  await expect(submit).toBeVisible();

  // empty submit is blocked by validation - no success panel yet
  await submit.click().catch(() => {});
  await expect(page.locator('[data-testid="lead-capture-success"]')).toHaveCount(0);

  const fill = async (re: RegExp, val: string) => {
    const el = form.getByLabel(re).first();   // scoped to the form: brand-link aria-label cannot match
    if (await el.count()) await el.fill(val);
  };
  await fill(/your name/i, "Jane Operator");
  await fill(/company/i, "Acme Steel");
  await fill(/work email/i, "jane@acme-steel.com");
  await fill(/plant|industry/i, "Flat steel mill");
  await fill(/source systems/i, "Oracle, MSSQL, Excel");
  await fill(/pain/i, "Slow defect root-cause across shifts");
  // Timeline is an optional <select>; leave default. Consent is required.
  const consent = form.getByRole("checkbox").first();
  if (await consent.count()) await consent.check().catch(() => {});

  await submit.click();
  const success = page.locator('[data-testid="lead-capture-success"]');
  await expect(success).toBeVisible({ timeout: 8000 });
  await expect(success).toContainText(/fit score/i);
});