# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: phase1-route-refresh.spec.ts >> PPIQ-HARD-001 / HARD-004 — route containment and refresh contract >> Risk Dashboard should load directly and survive browser refresh
- Location: e2e\phase1-route-refresh.spec.ts:12:5

# Error details

```
Error: Console errors:
%o

%s

%s
 SyntaxError: Duplicate export of 'RiskDashboardPage' The above error occurred in one of your React components. React will try to recreate this component tree from scratch using the error boundary you provided, ErrorBoundary.
[ErrorBoundary] bc391f6d-5060-4750-bbbb-6ceb5aae2f85 SyntaxError: Duplicate export of 'RiskDashboardPage' {componentStack: 
    at Lazy (<anonymous>)
    at ErrorBoundary (h…rc/components/hardening/AppErrorBoundary.tsx:5:8)}
%o

%s

%s
 SyntaxError: Duplicate export of 'RiskDashboardPage' The above error occurred in one of your React components. React will try to recreate this component tree from scratch using the error boundary you provided, ErrorBoundary.
[ErrorBoundary] 43f80081-8317-4aa5-aa1e-76965fd0c145 SyntaxError: Duplicate export of 'RiskDashboardPage' {componentStack: 
    at Lazy (<anonymous>)
    at ErrorBoundary (h…rc/components/hardening/AppErrorBoundary.tsx:5:8)}

expect(received).toEqual(expected) // deep equality

- Expected  -  1
+ Received  + 20

- Array []
+ Array [
+   "%o
+
+ %s
+
+ %s
+  SyntaxError: Duplicate export of 'RiskDashboardPage' The above error occurred in one of your React components. React will try to recreate this component tree from scratch using the error boundary you provided, ErrorBoundary.",
+   "[ErrorBoundary] bc391f6d-5060-4750-bbbb-6ceb5aae2f85 SyntaxError: Duplicate export of 'RiskDashboardPage' {componentStack: 
+     at Lazy (<anonymous>)
+     at ErrorBoundary (h…rc/components/hardening/AppErrorBoundary.tsx:5:8)}",
+   "%o
+
+ %s
+
+ %s
+  SyntaxError: Duplicate export of 'RiskDashboardPage' The above error occurred in one of your React components. React will try to recreate this component tree from scratch using the error boundary you provided, ErrorBoundary.",
+   "[ErrorBoundary] 43f80081-8317-4aa5-aa1e-76965fd0c145 SyntaxError: Duplicate export of 'RiskDashboardPage' {componentStack: 
+     at Lazy (<anonymous>)
+     at ErrorBoundary (h…rc/components/hardening/AppErrorBoundary.tsx:5:8)}",
+ ]
```

# Page snapshot

```yaml
- generic [ref=e2]:
  - region "Notifications alt+T"
  - generic [ref=e3]:
    - region "Notifications alt+T"
    - complementary "PlantProcess IQ navigation" [ref=e4]:
      - generic [ref=e5]:
        - generic [ref=e6]:
          - img [ref=e8]
          - generic [ref=e9]:
            - generic [ref=e10]: SOU
            - generic [ref=e11]: Manufacturing Intelligence
        - generic [ref=e13]:
          - generic [ref=e14]:
            - text: PlantProcess
            - emphasis [ref=e15]: IQ
          - generic [ref=e16]: Process-to-Quality Intelligence
      - generic [ref=e17]:
        - img [ref=e19]
        - generic [ref=e21]: Demo Plant
        - generic [ref=e22]: DEMO
      - navigation [ref=e23]:
        - paragraph [ref=e24]: Analytics
        - link "Command Dashboard Interactive intelligence workspace" [ref=e25] [cursor=pointer]:
          - /url: /dashboard
          - img [ref=e27]
          - generic [ref=e32]:
            - generic [ref=e33]: Command Dashboard
            - generic [ref=e34]: Interactive intelligence workspace
        - link "Material Investigation Genealogy, quality and risk drilldown" [ref=e35] [cursor=pointer]:
          - /url: /materials
          - img [ref=e37]
          - generic [ref=e40]:
            - generic [ref=e41]: Material Investigation
            - generic [ref=e42]: Genealogy, quality and risk drilldown
        - link "Risk Intelligence Quality risk score and contributors" [ref=e43] [cursor=pointer]:
          - /url: /risk
          - img [ref=e45]
          - generic [ref=e48]:
            - generic [ref=e49]: Risk Intelligence
            - generic [ref=e50]: Quality risk score and contributors
        - link "Data Quality Readiness and validation findings" [ref=e51] [cursor=pointer]:
          - /url: /data-quality
          - img [ref=e53]
          - generic [ref=e55]:
            - generic [ref=e56]: Data Quality
            - generic [ref=e57]: Readiness and validation findings
        - link "Correlations Process-to-quality analytics" [ref=e58] [cursor=pointer]:
          - /url: /correlations
          - img [ref=e60]
          - generic [ref=e64]:
            - generic [ref=e65]: Correlations
            - generic [ref=e66]: Process-to-quality analytics
        - paragraph [ref=e67]: Intelligence
        - link "ML Readiness Labels, features and training gates" [ref=e68] [cursor=pointer]:
          - /url: /ml-readiness
          - img [ref=e70]
          - generic [ref=e82]:
            - generic [ref=e83]: ML Readiness
            - generic [ref=e84]: Labels, features and training gates
        - link "Demo Lifecycle Connector to ML result workflow" [ref=e85] [cursor=pointer]:
          - /url: /demo-lifecycle
          - img [ref=e87]
          - generic [ref=e90]:
            - generic [ref=e91]: Demo Lifecycle
            - generic [ref=e92]: Connector to ML result workflow
        - paragraph [ref=e93]: System
        - link "Admin Preview License, roles, ML scripts, report" [ref=e94] [cursor=pointer]:
          - /url: /admin-preview
          - img [ref=e96]
          - generic [ref=e98]:
            - generic [ref=e99]: Admin Preview
            - generic [ref=e100]: License, roles, ML scripts, report
        - link "Administrator DB config, schema mapping and jobs" [ref=e101] [cursor=pointer]:
          - /url: /admin
          - img [ref=e103]
          - generic [ref=e106]:
            - generic [ref=e107]: Administrator
            - generic [ref=e108]: DB config, schema mapping and jobs
        - link "Brand Identity, positioning and proof" [ref=e109] [cursor=pointer]:
          - /url: /brand
          - img [ref=e111]
          - generic [ref=e114]:
            - generic [ref=e115]: Brand
            - generic [ref=e116]: Identity, positioning and proof
      - generic [ref=e117]:
        - generic [ref=e118]:
          - img [ref=e119]
          - generic [ref=e124]: API
          - code [ref=e125]: http://localhost:5063
        - generic [ref=e126]:
          - img [ref=e127]
          - generic [ref=e132]: Phase 8–10 Interactive MVP
        - button "Dark mode" [ref=e133] [cursor=pointer]:
          - img [ref=e134]
          - text: Dark mode
    - main [ref=e136]:
      - generic [ref=e137]:
        - generic [ref=e139]:
          - generic [ref=e140]:
            - img [ref=e141]
            - generic [ref=e144]: Plant
            - strong [ref=e145]: Demo Plant
          - generic [ref=e146]:
            - img [ref=e147]
            - generic [ref=e150]: Status
            - strong [ref=e151]: Healthy
        - generic [ref=e152]:
          - generic [ref=e153]: Development
          - generic [ref=e154]: Demo
          - button "Logout" [ref=e155] [cursor=pointer]:
            - img [ref=e156]
            - text: Playwright E2E Admin
            - img [ref=e160]
      - generic [ref=e164]:
        - button "Demo mode on" [ref=e165] [cursor=pointer]:
          - img [ref=e166]
          - text: Demo mode on
        - generic [ref=e168] [cursor=pointer]:
          - img [ref=e169]
          - combobox [ref=e172]:
            - option "Light"
            - option "Pro"
            - option "Pro Plus" [selected]
            - option "Enterprise"
        - link "Run flow" [ref=e173] [cursor=pointer]:
          - /url: /demo-lifecycle
          - img [ref=e174]
          - text: Run flow
      - generic [ref=e177]:
        - generic [ref=e178]:
          - paragraph [ref=e179]:
            - img [ref=e180]
            - text: Process-to-Quality Intelligence Platform
          - heading "Industrial Analytics Command Center" [level=2] [ref=e183]
          - paragraph [ref=e184]: Digital plant data, genealogy, process history, quality events, risk scoring and correlation intelligence in one evidence-based manufacturing workspace.
        - generic [ref=e185]:
          - generic [ref=e186]:
            - img [ref=e187]
            - text: Rule-based intelligence
          - generic [ref=e190]:
            - img [ref=e191]
            - text: Interactive workspace
      - alert [ref=e194]:
        - generic [ref=e195]:
          - img [ref=e197]
          - heading "Risk dashboard could not load" [level=2] [ref=e199]
          - paragraph [ref=e200]: This part of the page could not load. Our team has been notified.
          - paragraph [ref=e201]:
            - text: "Reference:"
            - code [ref=e202]: 43f80081-8317-4aa5-aa1e-76965fd0c145
          - button "Try again" [ref=e203] [cursor=pointer]:
            - img [ref=e204]
            - text: Try again
  - region "Notifications alt+T"
```

# Test source

```ts
  1   | // ============================================================
  2   | // FILE: Frontend/PlantProcess.Web/e2e/helpers/phase1Hardening.ts
  3   | //
  4   | // Phase 1 route/refresh hardening helper.
  5   | // Restores refreshAndAssertStillSafe export used by phase1-route-refresh.spec.ts.
  6   | // ============================================================
  7   | 
  8   | import { expect, type APIRequestContext, type Page } from "@playwright/test";
  9   | import { apiBaseUrl, login } from "./auth";
  10  | import {
  11  |   formatRequestFailure,
  12  |   formatResponseFailure,
  13  |   isIgnorableConsoleMessage,
  14  |   shouldTrackFailedRequest,
  15  |   shouldTrackFailedResponse,
  16  |   type AllowedFailureOptions,
  17  | } from "./e2eFailureFilters";
  18  | 
  19  | export type Phase1HardeningPageGuard = {
  20  |   assertNoUnexpectedFailures: () => Promise<void>;
  21  |   getPageErrors: () => string[];
  22  |   getConsoleErrors: () => string[];
  23  |   getFailedRequests: () => string[];
  24  | };
  25  | 
  26  | export async function prepareAuthenticatedPage(
  27  |   page: Page,
  28  |   request: APIRequestContext
  29  | ): Promise<string> {
  30  |   const token = await login(request);
  31  | 
  32  |   await page.addInitScript(
  33  |     ({ accessToken, baseUrl }) => {
  34  |       localStorage.setItem("plantprocess.auth.accessToken", accessToken);
  35  |       localStorage.setItem("plantprocess.auth.token", accessToken);
  36  |       localStorage.setItem("plantprocess.accessToken", accessToken);
  37  |       localStorage.setItem("accessToken", accessToken);
  38  |       localStorage.setItem("ppiq-demo-mode", "true");
  39  |       localStorage.setItem("VITE_API_BASE_URL", baseUrl);
  40  |     },
  41  |     {
  42  |       accessToken: token,
  43  |       baseUrl: apiBaseUrl,
  44  |     }
  45  |   );
  46  | 
  47  |   return token;
  48  | }
  49  | 
  50  | export function installHardeningPageGuard(
  51  |   page: Page,
  52  |   options: AllowedFailureOptions = {}
  53  | ): Phase1HardeningPageGuard {
  54  |   const consoleErrors: string[] = [];
  55  |   const pageErrors: string[] = [];
  56  |   const failedRequests: string[] = [];
  57  | 
  58  |   page.on("console", (message) => {
  59  |     if (message.type() !== "error") return;
  60  |     if (isIgnorableConsoleMessage(message)) return;
  61  | 
  62  |     consoleErrors.push(message.text());
  63  |   });
  64  | 
  65  |   page.on("pageerror", (error) => {
  66  |     pageErrors.push(error.message);
  67  |   });
  68  | 
  69  |   page.on("requestfailed", (request) => {
  70  |     if (!shouldTrackFailedRequest(request, options)) return;
  71  | 
  72  |     failedRequests.push(formatRequestFailure(request));
  73  |   });
  74  | 
  75  |   page.on("response", (response) => {
  76  |     if (!shouldTrackFailedResponse(response, options)) return;
  77  | 
  78  |     failedRequests.push(formatResponseFailure(response));
  79  |   });
  80  | 
  81  |   return {
  82  |     async assertNoUnexpectedFailures() {
  83  |       expect(pageErrors, `Page errors:\n${pageErrors.join("\n")}`).toEqual([]);
> 84  |       expect(consoleErrors, `Console errors:\n${consoleErrors.join("\n")}`).toEqual([]);
      |                                                                             ^ Error: Console errors:
  85  |       expect(failedRequests, `Failed requests:\n${failedRequests.join("\n")}`).toEqual([]);
  86  |     },
  87  | 
  88  |     getPageErrors() {
  89  |       return [...pageErrors];
  90  |     },
  91  | 
  92  |     getConsoleErrors() {
  93  |       return [...consoleErrors];
  94  |     },
  95  | 
  96  |     getFailedRequests() {
  97  |       return [...failedRequests];
  98  |     },
  99  |   };
  100 | }
  101 | 
  102 | export async function gotoAndAssertCustomerSafePage(
  103 |   page: Page,
  104 |   route: string,
  105 |   expectedText: RegExp
  106 | ): Promise<void> {
  107 |   await page.goto(route, {
  108 |     waitUntil: "domcontentloaded",
  109 |     timeout: 30_000,
  110 |   });
  111 | 
  112 |   await page
  113 |     .waitForLoadState("networkidle", {
  114 |       timeout: 8_000,
  115 |     })
  116 |     .catch(() => {
  117 |       // Background retries/polling should not fail the route check alone.
  118 |     });
  119 | 
  120 |   const body = page.locator("body");
  121 | 
  122 |   await expect(body).toBeVisible({
  123 |     timeout: 20_000,
  124 |   });
  125 | 
  126 |   await expect(body).toContainText(expectedText, {
  127 |     timeout: 20_000,
  128 |   });
  129 | 
  130 |   const normalized = (await body.innerText()).toLowerCase();
  131 | 
  132 |   expect(normalized).not.toContain("white screen");
  133 |   expect(normalized).not.toContain("cannot read properties");
  134 |   expect(normalized).not.toContain("is not a function");
  135 |   expect(normalized).not.toContain("uncaught");
  136 |   expect(normalized).not.toContain("stack trace");
  137 |   expect(normalized).not.toContain("undefined is not");
  138 | }
  139 | 
  140 | export async function refreshAndAssertStillSafe(
  141 |   page: Page,
  142 |   expectedText: RegExp
  143 | ): Promise<void> {
  144 |   await page.reload({
  145 |     waitUntil: "domcontentloaded",
  146 |     timeout: 30_000,
  147 |   });
  148 | 
  149 |   await page
  150 |     .waitForLoadState("networkidle", {
  151 |       timeout: 8_000,
  152 |     })
  153 |     .catch(() => {
  154 |       // Polling/background retries are acceptable if the page remains usable.
  155 |     });
  156 | 
  157 |   const body = page.locator("body");
  158 | 
  159 |   await expect(body).toBeVisible({
  160 |     timeout: 20_000,
  161 |   });
  162 | 
  163 |   await expect(body).toContainText(expectedText, {
  164 |     timeout: 20_000,
  165 |   });
  166 | 
  167 |   const normalized = (await body.innerText()).toLowerCase();
  168 | 
  169 |   expect(normalized).not.toContain("white screen");
  170 |   expect(normalized).not.toContain("cannot read properties");
  171 |   expect(normalized).not.toContain("is not a function");
  172 |   expect(normalized).not.toContain("uncaught");
  173 |   expect(normalized).not.toContain("stack trace");
  174 |   expect(normalized).not.toContain("undefined is not");
  175 | }
```