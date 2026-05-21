# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: p0-auth-pages-contract.spec.ts >> PlantProcess IQ P0 authenticated page contract >> core pages load without auth, route, DTO, or console failures
- Location: e2e\p0-auth-pages-contract.spec.ts:22:3

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: locator('body')
Timeout: 15000ms
Expected pattern: /PlantProcess|Dashboard|Risk|Data|Admin|Material|Correlation/i
Received string:  "·············
"

Call log:
  - Expect "toContainText" with timeout 15000ms
  - waiting for locator('body')
    33 × locator resolved to <body>…</body>
       - unexpected value "
    
    
  
"

```

# Test source

```ts
  1  | import { expect, test } from "@playwright/test";
  2  | import { installNetworkGuard } from "./helpers/networkGuard";
  3  | 
  4  | async function login(request: any) {
  5  |   const response = await request.post("http://localhost:5063/auth/login", {
  6  |     data: {
  7  |       userName: "admin",
  8  |       password: "ChangeMe123!",
  9  |     },
  10 |   });
  11 | 
  12 |   expect(response.ok()).toBeTruthy();
  13 | 
  14 |   const body = await response.json();
  15 |   expect(body.accessToken).toBeTruthy();
  16 |   expect(body.role).toBe("Admin");
  17 | 
  18 |   return body.accessToken as string;
  19 | }
  20 | 
  21 | test.describe("PlantProcess IQ P0 authenticated page contract", () => {
  22 |   test("core pages load without auth, route, DTO, or console failures", async ({ page, request }) => {
  23 |     const token = await login(request);
  24 | 
  25 |     await page.addInitScript((accessToken) => {
  26 |       window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
  27 |     }, token);
  28 | 
  29 |     const assertNoNetworkFailures = installNetworkGuard(page);
  30 | 
  31 |     for (const route of [
  32 |       "/dashboard",
  33 |       "/risk",
  34 |       "/data-quality",
  35 |       "/correlations",
  36 |       "/materials",
  37 |       "/admin",
  38 |     ]) {
  39 |       await page.goto(route);
> 40 |       await expect(page.locator("body")).toContainText(/PlantProcess|Dashboard|Risk|Data|Admin|Material|Correlation/i);
     |                                          ^ Error: expect(locator).toContainText(expected) failed
  41 |     }
  42 | 
  43 |     await assertNoNetworkFailures();
  44 |   });
  45 | });
```