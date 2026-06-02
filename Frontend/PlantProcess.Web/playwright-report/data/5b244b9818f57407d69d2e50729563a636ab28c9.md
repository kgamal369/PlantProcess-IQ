# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: nav-graph-refresh-survival.spec.ts >> Navigation graph and refresh survival >> route survives direct navigation and refresh: /risk
- Location: e2e\nav-graph-refresh-survival.spec.ts:18:5

# Error details

```
Error: expect(received).toEqual(expected) // deep equality

Expected: ""
Received: "Access to fetch at 'http://localhost:5063/auth/refresh' from origin 'http://localhost:5173' has been blocked by CORS policy: The value of the 'Access-Control-Allow-Credentials' header in the response is '' which must be 'true' when the request's credentials mode is 'include'.
Failed to load resource: net::ERR_FAILED
Access to fetch at 'http://localhost:5063/auth/login' from origin 'http://localhost:5173' has been blocked by CORS policy: Response to preflight request doesn't pass access control check: The value of the 'Access-Control-Allow-Credentials' header in the response is '' which must be 'true' when the request's credentials mode is 'include'.
Failed to load resource: net::ERR_FAILED
Access to fetch at 'http://localhost:5063/auth/refresh' from origin 'http://localhost:5173' has been blocked by CORS policy: The value of the 'Access-Control-Allow-Credentials' header in the response is '' which must be 'true' when the request's credentials mode is 'include'.
Failed to load resource: net::ERR_FAILED
Access to fetch at 'http://localhost:5063/auth/login' from origin 'http://localhost:5173' has been blocked by CORS policy: Response to preflight request doesn't pass access control check: The value of the 'Access-Control-Allow-Credentials' header in the response is '' which must be 'true' when the request's credentials mode is 'include'.
Failed to load resource: net::ERR_FAILED"
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
  3  | const routes = [
  4  |   "/",
  5  |   "/dashboard",
  6  |   "/quality",
  7  |   "/risk",
  8  |   "/data-quality",
  9  |   "/correlation",
  10 |   "/material-investigation",
  11 |   "/admin",
  12 |   "/admin-preview",
  13 |   "/demo-lifecycle",
  14 | ];
  15 | 
  16 | test.describe("Navigation graph and refresh survival", () => {
  17 |   for (const route of routes) {
  18 |     test(`route survives direct navigation and refresh: ${route}`, async ({ page }) => {
  19 |       const consoleErrors: string[] = [];
  20 |       const failedRequests: string[] = [];
  21 | 
  22 |       page.on("console", (message) => {
  23 |         if (message.type() === "error") {
  24 |           consoleErrors.push(message.text());
  25 |         }
  26 |       });
  27 | 
  28 |       page.on("requestfailed", (request) => {
  29 |         const url = request.url();
  30 | 
  31 |         if (!url.includes("favicon") && !url.includes(".map")) {
  32 |           failedRequests.push(`${request.method()} ${url}`);
  33 |         }
  34 |       });
  35 | 
  36 |       await page.goto(route, { waitUntil: "domcontentloaded" });
  37 |       await expect(page.locator("body")).toBeVisible();
  38 | 
  39 |       await page.reload({ waitUntil: "domcontentloaded" });
  40 |       await expect(page.locator("body")).toBeVisible();
  41 | 
  42 |       await expect(page.locator("body")).not.toContainText(/white screen|uncaught|undefined is not/i);
  43 | 
> 44 |       expect(consoleErrors.filter((x) => !x.includes("favicon")).join("\n")).toEqual("");
     |                                                                              ^ Error: expect(received).toEqual(expected) // deep equality
  45 |       expect(failedRequests.join("\n")).toEqual("");
  46 |     });
  47 |   }
  48 | 
  49 |   test("main navigation links are reachable", async ({ page }) => {
  50 |     await page.goto("/");
  51 | 
  52 |     const links = await page.locator("a[href^='/']").evaluateAll((items) =>
  53 |       items.map((item) => (item as HTMLAnchorElement).getAttribute("href")).filter(Boolean)
  54 |     );
  55 | 
  56 |     const uniqueLinks = [...new Set(links)].slice(0, 25);
  57 | 
  58 |     for (const href of uniqueLinks) {
  59 |       await page.goto(href!, { waitUntil: "domcontentloaded" });
  60 |       await expect(page.locator("body")).toBeVisible();
  61 |     }
  62 |   });
  63 | });
```