# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: critical-shell-regression.spec.ts >> PlantProcess IQ critical shell regression >> material investigation route remains available
- Location: e2e\critical-shell-regression.spec.ts:17:3

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: locator('body')
Timeout: 20000ms
Expected pattern: /material|investigation|search/i
Received string:  "
    Backend connection failedBackend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetchRetry connection········
"

Call log:
  - Expect "toContainText" with timeout 20000ms
  - waiting for locator('body')
    43 × locator resolved to <body>…</body>
       - unexpected value "
    Backend connection failedBackend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetchRetry connection
    
  
"

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
  1  | ﻿import { expect, test } from "@playwright/test";
  2  | 
  3  | test.describe("PlantProcess IQ critical shell regression", () => {
  4  |   test("admin page exposes core operational sections", async ({ page }) => {
  5  |     await page.goto("/admin");
  6  | 
  7  |     await expect(page.locator("body")).toContainText(/admin/i);
  8  |     await expect(page.locator("body")).toContainText(/jobs|configuration|schema|import/i);
  9  |   });
  10 | 
  11 |   test("dashboard page exposes dashboard and widget experience", async ({ page }) => {
  12 |     await page.goto("/dashboard");
  13 | 
  14 |     await expect(page.locator("body")).toContainText(/dashboard|widget|quality|risk/i);
  15 |   });
  16 | 
  17 |   test("material investigation route remains available", async ({ page }) => {
  18 |     await page.goto("/materials");
  19 | 
> 20 |     await expect(page.locator("body")).toContainText(/material|investigation|search/i);
     |                                        ^ Error: expect(locator).toContainText(expected) failed
  21 |   });
  22 | });
  23 | 
```