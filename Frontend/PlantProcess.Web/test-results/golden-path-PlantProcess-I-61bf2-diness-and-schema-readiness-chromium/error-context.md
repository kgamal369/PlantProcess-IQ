# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: golden-path.spec.ts >> PlantProcess IQ Golden Path >> proves login, admin visibility, dashboard, data quality, risk API, connector readiness, and schema readiness
- Location: e2e\golden-path.spec.ts:76:3

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: locator('body')
Timeout: 20000ms
Expected pattern: /admin|jobs|configuration|schema|import/i
Received string:  "·············
"

Call log:
  - Expect "toContainText" with timeout 20000ms
  - waiting for locator('body')
    43 × locator resolved to <body>…</body>
       - unexpected value "
    
    
  
"

```

# Test source

```ts
  1   | import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
  2   | import { apiBaseUrl, login } from "./helpers/auth";
  3   | 
  4   | async function getJson<T>(
  5   |   request: APIRequestContext,
  6   |   path: string,
  7   |   token: string
  8   | ): Promise<T> {
  9   |   const response = await request.get(`${apiBaseUrl}${path}`, {
  10  |     headers: {
  11  |       Authorization: `Bearer ${token}`
  12  |     }
  13  |   });
  14  | 
  15  |   expect(
  16  |     response.ok(),
  17  |     `${path} should return 2xx but returned HTTP ${response.status()}`
  18  |   ).toBeTruthy();
  19  | 
  20  |   return response.json() as Promise<T>;
  21  | }
  22  | 
  23  | async function getControlled(
  24  |   request: APIRequestContext,
  25  |   path: string,
  26  |   token: string
  27  | ) {
  28  |   const response = await request.get(`${apiBaseUrl}${path}`, {
  29  |     headers: {
  30  |       Authorization: `Bearer ${token}`
  31  |     }
  32  |   });
  33  | 
  34  |   expect(
  35  |     response.status(),
  36  |     `${path} should not return 5xx but returned HTTP ${response.status()}`
  37  |   ).toBeLessThan(500);
  38  | 
  39  |   return response;
  40  | }
  41  | 
  42  | async function prepareAuthenticatedPage(page: Page, token: string) {
  43  |   await page.addInitScript((accessToken) => {
  44  |     window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
  45  |     window.localStorage.setItem("plantprocess.auth.userName", "admin");
  46  |     window.localStorage.setItem("plantprocess.auth.role", "Admin");
  47  |     window.localStorage.setItem(
  48  |       "plantprocess.auth.expiresAtUtc",
  49  |       new Date(Date.now() + 60 * 60 * 1000).toISOString()
  50  |     );
  51  |   }, token);
  52  | }
  53  | 
  54  | async function gotoAndExpectText(
  55  |   page: Page,
  56  |   url: string,
  57  |   expected: RegExp
  58  | ) {
  59  |   await page.goto(url, {
  60  |     waitUntil: "domcontentloaded",
  61  |     timeout: 30_000
  62  |   });
  63  | 
  64  |   await page.waitForLoadState("networkidle", {
  65  |     timeout: 15_000
  66  |   }).catch(() => {
  67  |     // Some app pages may keep polling; do not fail solely on networkidle.
  68  |   });
  69  | 
> 70  |   await expect(page.locator("body")).toContainText(expected, {
      |                                      ^ Error: expect(locator).toContainText(expected) failed
  71  |     timeout: 20_000
  72  |   });
  73  | }
  74  | 
  75  | test.describe("PlantProcess IQ Golden Path", () => {
  76  |   test("proves login, admin visibility, dashboard, data quality, risk API, connector readiness, and schema readiness", async ({
  77  |     page,
  78  |     request
  79  |   }) => {
  80  |     const unexpectedServerResponses: string[] = [];
  81  | 
  82  |     page.on("response", (response) => {
  83  |       const status = response.status();
  84  |       const url = response.url();
  85  | 
  86  |       if (status < 500) {
  87  |         return;
  88  |       }
  89  | 
  90  |       /*
  91  |        * This endpoint is a background self-healing/ensure action triggered by
  92  |        * dashboard pages. It is not the golden-path business assertion.
  93  |        *
  94  |        * Backend hardening task:
  95  |        * Make /analytics/dashboard/definitions/system-templates/ensure
  96  |        * idempotent/concurrency-safe and add a dedicated backend integration test.
  97  |        */
  98  |       if (url.includes("/analytics/dashboard/definitions/system-templates/ensure")) {
  99  |         return;
  100 |       }
  101 | 
  102 |       unexpectedServerResponses.push(`${status} ${url}`);
  103 |     });
  104 | 
  105 |     const token = await login(request);
  106 |     expect(token.length).toBeGreaterThan(20);
  107 | 
  108 |     await prepareAuthenticatedPage(page, token);
  109 | 
  110 |     // Platform health
  111 |     await getControlled(request, "/health", token);
  112 |     await getControlled(request, "/db-health", token);
  113 | 
  114 |     // Admin / operational visibility
  115 |     await gotoAndExpectText(
  116 |       page,
  117 |       "/admin",
  118 |       /admin|jobs|configuration|schema|import/i
  119 |     );
  120 | 
  121 |     const adminOverview = await getJson<unknown>(
  122 |       request,
  123 |       "/admin/overview",
  124 |       token
  125 |     );
  126 |     expect(adminOverview).toBeDefined();
  127 | 
  128 |     const jobsMonitor = await getJson<unknown>(
  129 |       request,
  130 |       "/admin/jobs-monitor",
  131 |       token
  132 |     );
  133 |     expect(jobsMonitor).toBeDefined();
  134 | 
  135 |     // Dashboard foundation
  136 |     await gotoAndExpectText(
  137 |       page,
  138 |       "/dashboard",
  139 |       /dashboard|widget|quality|risk/i
  140 |     );
  141 | 
  142 |     const dashboardOverview = await getJson<unknown>(
  143 |       request,
  144 |       "/analytics/dashboard/overview",
  145 |       token
  146 |     );
  147 |     expect(dashboardOverview).toBeDefined();
  148 | 
  149 |     const dashboardMetadata = await getJson<unknown>(
  150 |       request,
  151 |       "/analytics/dashboard/metadata",
  152 |       token
  153 |     );
  154 |     expect(dashboardMetadata).toBeDefined();
  155 | 
  156 |     const dashboardDefinitions = await getJson<unknown>(
  157 |       request,
  158 |       "/analytics/dashboard/definitions",
  159 |       token
  160 |     );
  161 |     expect(dashboardDefinitions).toBeDefined();
  162 | 
  163 |     // Data quality foundation
  164 |     await gotoAndExpectText(
  165 |       page,
  166 |       "/data-quality",
  167 |       /data quality|quality|issue|readiness|scan/i
  168 |     );
  169 | 
  170 |     const dataQualityDashboard = await getJson<unknown>(
```