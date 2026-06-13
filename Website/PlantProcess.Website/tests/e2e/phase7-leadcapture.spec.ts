import { test, expect } from "@playwright/test";

/* P7-T05: prove the Request-Demo lead capture works end to end - empty submit is
 * blocked, a complete submit renders the success panel with the fit score. */
test("request-demo lead capture submits and shows fit score", async ({ page }) => {
  await page.goto("/contact");
  const submit = page.getByRole("button", { name: /request|submit|send|demo/i }).first();
  await expect(submit).toBeVisible();

  await submit.click().catch(() => {});
  await expect(page.locator('[data-testid="lead-capture-success"]')).toHaveCount(0);

  const fill = async (re: RegExp, val: string) => {
    const el = page.getByLabel(re).first();
    if (await el.count()) await el.fill(val);
  };
  await fill(/name/i, "Jane Operator");
  await fill(/company/i, "Acme Steel");
  await fill(/email/i, "jane@acme-steel.com");
  await fill(/plant|industry/i, "Flat steel mill");
  await fill(/source|system/i, "Oracle, MSSQL, Excel");
  await fill(/pain|problem|challenge/i, "Slow defect root-cause across shifts");
  await fill(/timeline/i, "This quarter");
  const consent = page.getByRole("checkbox").first();
  if (await consent.count()) await consent.check().catch(() => {});

  await submit.click();
  await expect(page.locator('[data-testid="lead-capture-success"]')).toBeVisible({ timeout: 8000 });
  await expect(page.locator('[data-testid="lead-capture-success"]')).toContainText(/fit score/i);
});