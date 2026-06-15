import { expect, test, type Locator, type Page } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: Page) {
  expect(password, "PPIQ_SMOKE_PASSWORD must be configured").not.toBe("");
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), `Login failed: ${response.status()} ${await response.text()}`).toBeTruthy();
}

async function requireVisible(control: Locator, description: string) {
  await expect(control, `${description} is mandatory on the V1 demo path; absence is a failure, never a skip.`).toBeVisible();
}

async function clickWithEffect(page: Page, control: Locator, description: string, status?: Locator) {
  await requireVisible(control, description);
  const before = status ? await status.textContent() : null;
  const responses: string[] = [];
  const listener = (r: import("@playwright/test").Response) => {
    if (r.status() >= 200 && r.status() < 300) responses.push(`${r.status()} ${r.url()}`);
  };
  page.on("response", listener);
  await control.click();
  await page.waitForTimeout(600);
  page.off("response", listener);
  const after = status ? await status.textContent() : null;
  expect(responses.length > 0 || (status && before !== after), `${description} must produce a named 2xx request or change its own status region.`).toBeTruthy();
}

test.describe("PPIQ-201 mandatory demo controls", () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test("material search, investigation, risk and PDF actions are live", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", e => errors.push(String(e)));
    await page.goto("/materials", { waitUntil: "domcontentloaded" });
    const searchInput = page.getByLabel(/search material code/i).or(page.getByPlaceholder(/search material code/i)).first();
    await requireVisible(searchInput, "Material search input");
    await searchInput.fill(process.env.PPIQ_DEMO_MATERIAL_CODE || "C-0044170");
    await clickWithEffect(page, page.getByRole("button", { name: /^search$/i }).first(), "Search", page.locator('[role="status"]').first());

    const load = page.getByRole("button", { name: /load investigation|open investigation/i }).first();
    await clickWithEffect(page, load, "Load investigation", page.locator('[role="status"]').first());

    await clickWithEffect(page, page.getByRole("button", { name: /calculate risk/i }).first(), "Calculate Risk", page.locator('[role="status"]').first());

    const pdf = page.getByRole("link", { name: /pdf|export/i }).or(page.getByRole("button", { name: /pdf|export/i })).first();
    await requireVisible(pdf, "Generate/Export PDF");
    const [pdfResponse] = await Promise.all([
      page.waitForResponse(r => r.url().toLowerCase().includes("pdf") && r.status() >= 200 && r.status() < 300),
      pdf.click(),
    ]);
    expect((await pdfResponse.headerValue("content-type")) || "").toContain("application/pdf");
    expect(errors).toEqual([]);
  });

  test("page save is live and versioned", async ({ page }) => {
    await page.goto("/page-builder", { waitUntil: "domcontentloaded" });
    await clickWithEffect(page, page.getByTestId("ctl-save-page"), "Save page definition", page.getByRole("status"));
    await expect(page.getByRole("status")).toContainText(/saved/i);
  });

  test("dashboard filters and job run controls are reachable and live", async ({ page }) => {
    await page.goto("/dashboard", { waitUntil: "domcontentloaded" });
    const filter = page.getByRole("button", { name: /filter|apply/i }).first();
    await clickWithEffect(page, filter, "Dashboard filter/apply control", page.locator('[role="status"]').first());

    await page.goto("/admin", { waitUntil: "domcontentloaded" });
    const runJob = page.getByRole("button", { name: /run now|run job|trigger/i }).first();
    await clickWithEffect(page, runJob, "Run job", page.locator('[role="status"]').first());
  });
});