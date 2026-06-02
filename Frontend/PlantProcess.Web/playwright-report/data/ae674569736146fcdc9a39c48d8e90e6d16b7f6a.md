# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: dimension7-brand-identity.spec.ts >> Dimension 7 Brand Identity >> brand identity page renders positioning, proof assets and forbidden-language-safe content
- Location: e2e\dimension7-brand-identity.spec.ts:4:3

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByText('Dimension 7 — Brand Identity')
Expected: visible
Timeout: 20000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" with timeout 20000ms
  - waiting for getByText('Dimension 7 — Brand Identity')

```

```yaml
- region "Notifications alt+T"
- img "SOU"
- paragraph: Backend connection failed
- paragraph: "Backend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetch"
- button "Retry connection"
- region "Notifications alt+T"
```

# Test source

```ts
  1  | import { expect, test } from "@playwright/test";
  2  | 
  3  | test.describe("Dimension 7 Brand Identity", () => {
  4  |   test("brand identity page renders positioning, proof assets and forbidden-language-safe content", async ({
  5  |     page,
  6  |   }) => {
  7  |     await page.goto("/brand");
  8  | 
> 9  |     await expect(page.getByText("Dimension 7 — Brand Identity")).toBeVisible();
     |                                                                  ^ Error: expect(locator).toBeVisible() failed
  10 |     await expect(page.getByText("Brand Identity & Market Positioning")).toBeVisible();
  11 |     await expect(page.getByText('Engineer brief', { exact: true })).toBeVisible();
  12 |     await expect(page.getByText('Architecture diagram', { exact: true })).toBeVisible();
  13 |     await expect(page.getByText("Not MES")).toBeVisible();
  14 |     await expect(page.getByText("Not SCADA")).toBeVisible();
  15 | 
  16 |     const body = await page.locator("body").innerText();
  17 |     const normalized = body.toLowerCase();
  18 | 
  19 |     expect(normalized).not.toContain("guaranteed root cause detection");
  20 |     expect(normalized).not.toContain("production-ready ai model");
  21 |     expect(normalized).not.toContain("replaces mes");
  22 |     expect(normalized).not.toContain("replaces scada");
  23 |   });
  24 | });
```