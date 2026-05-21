# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: route-smoke.spec.ts >> PlantProcess IQ route smoke regression >> opens /data-quality without browser errors
- Location: e2e\route-smoke.spec.ts:15:5

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: locator('body')
Timeout: 15000ms
Expected pattern: /data quality|quality|plantprocess iq/i
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
  1  | ﻿import { expect, test } from "@playwright/test";
  2  | 
  3  | const routes = [
  4  |   { path: "/dashboard",      text: /dashboard|plantprocess iq/i },
  5  |   { path: "/materials",      text: /material|investigation|plantprocess iq/i },
  6  |   { path: "/risk",           text: /risk|plantprocess iq/i },
  7  |   { path: "/data-quality",   text: /data quality|quality|plantprocess iq/i },
  8  |   { path: "/correlations",   text: /correlation|plantprocess iq/i },
  9  |   { path: "/admin",          text: /admin|jobs|configuration|plantprocess iq/i },
  10 |   { path: "/demo-lifecycle", text: /lifecycle|connector|ML|PlantProcess IQ/i },
  11 | ];
  12 | 
  13 | test.describe("PlantProcess IQ route smoke regression", () => {
  14 |   for (const route of routes) {
  15 |     test(`opens ${route.path} without browser errors`, async ({ page }) => {
  16 |       const pageErrors: string[] = [];
  17 | 
  18 |       page.on("pageerror", (error) => {
  19 |         pageErrors.push(error.message);
  20 |       });
  21 | 
  22 |       await page.goto(route.path);
> 23 |       await expect(page.locator("body")).toContainText(route.text);
     |                                          ^ Error: expect(locator).toContainText(expected) failed
  24 | 
  25 |       expect(pageErrors, `Browser page errors on ${route.path}`).toEqual([]);
  26 |     });
  27 |   }
  28 | });
```