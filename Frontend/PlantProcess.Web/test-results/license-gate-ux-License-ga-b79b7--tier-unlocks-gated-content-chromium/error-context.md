# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: license-gate-ux.spec.ts >> License gate UX >> Higher tier unlocks gated content
- Location: e2e\license-gate-ux.spec.ts:16:3

# Error details

```
TimeoutError: locator.click: Timeout 20000ms exceeded.
Call log:
  - waiting for getByRole('button', { name: /license/i })

```

# Page snapshot

```yaml
- generic [ref=e2]:
  - region "Notifications alt+T"
  - generic [ref=e3]:
    - img "SOU" [ref=e5]
    - generic [ref=e6]:
      - paragraph [ref=e7]: Backend connection failed
      - paragraph [ref=e8]: "Backend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetch"
      - button "Retry connection" [ref=e9] [cursor=pointer]
  - region "Notifications alt+T"
```

# Test source

```ts
  1  | import { expect, test } from "@playwright/test";
  2  | 
  3  | test.describe("License gate UX", () => {
  4  |   test("Light tier shows locked feature overlay instead of blank content", async ({ page }) => {
  5  |     await page.goto("/admin-preview");
  6  | 
  7  |     await page.getByRole("button", { name: /license/i }).click();
  8  |     await page.getByRole("button", { name: /light/i }).click();
  9  | 
  10 |     await page.getByRole("button", { name: /users/i }).click();
  11 | 
  12 |     await expect(page.getByText(/locked in the current license/i)).toBeVisible();
  13 |     await expect(page.getByText(/Enterprise/i)).toBeVisible();
  14 |   });
  15 | 
  16 |   test("Higher tier unlocks gated content", async ({ page }) => {
  17 |     await page.goto("/admin-preview");
  18 | 
> 19 |     await page.getByRole("button", { name: /license/i }).click();
     |                                                          ^ TimeoutError: locator.click: Timeout 20000ms exceeded.
  20 |     await page.getByRole("button", { name: /enterprise/i }).click();
  21 | 
  22 |     await page.getByRole("button", { name: /users/i }).click();
  23 | 
  24 |     await expect(page.getByText(/Users, Roles/i)).toBeVisible();
  25 |     await expect(page.getByText(/locked in the current license/i)).not.toBeVisible();
  26 |   });
  27 | });
```