# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: phase1-route-refresh.spec.ts >> PPIQ-HARD-001 / HARD-004 — route containment and refresh contract >> Correlation should load directly and survive browser refresh
- Location: e2e\phase1-route-refresh.spec.ts:12:5

# Error details

```
Error: Failed requests:
404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3
404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3
404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3
404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3

expect(received).toEqual(expected) // deep equality

- Expected  - 1
+ Received  + 6

- Array []
+ Array [
+   "404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3",
+   "404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3",
+   "404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3",
+   "404 GET http://localhost:5063/analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3",
+ ]
```

# Page snapshot

```yaml
- generic [ref=e2]:
  - region "Notifications alt+T":
    - list:
      - listitem [ref=e3]:
        - button "Close toast" [ref=e4] [cursor=pointer]:
          - img [ref=e5]
        - generic [ref=e8]:
          - generic [ref=e9]: Parameter definition not found.
          - generic [ref=e10]: GET /analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3
  - generic [ref=e11]:
    - region "Notifications alt+T":
      - list:
        - listitem [ref=e12]:
          - button "Close toast" [ref=e13] [cursor=pointer]:
            - img [ref=e14]
          - generic [ref=e17]:
            - generic [ref=e18]: Parameter definition not found.
            - generic [ref=e19]: GET /analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3
    - complementary "PlantProcess IQ navigation" [ref=e20]:
      - generic [ref=e21]:
        - generic [ref=e22]:
          - img [ref=e24]
          - generic [ref=e25]:
            - generic [ref=e26]: SOU
            - generic [ref=e27]: Manufacturing Intelligence
        - generic [ref=e29]:
          - generic [ref=e30]:
            - text: PlantProcess
            - emphasis [ref=e31]: IQ
          - generic [ref=e32]: Process-to-Quality Intelligence
      - generic [ref=e33]:
        - img [ref=e35]
        - generic [ref=e37]: Demo Plant
        - generic [ref=e38]: DEMO
      - navigation [ref=e39]:
        - paragraph [ref=e40]: Analytics
        - link "Command Dashboard Interactive intelligence workspace" [ref=e41] [cursor=pointer]:
          - /url: /dashboard
          - img [ref=e43]
          - generic [ref=e48]:
            - generic [ref=e49]: Command Dashboard
            - generic [ref=e50]: Interactive intelligence workspace
        - link "Material Investigation Genealogy, quality and risk drilldown" [ref=e51] [cursor=pointer]:
          - /url: /materials
          - img [ref=e53]
          - generic [ref=e56]:
            - generic [ref=e57]: Material Investigation
            - generic [ref=e58]: Genealogy, quality and risk drilldown
        - link "Risk Intelligence Quality risk score and contributors" [ref=e59] [cursor=pointer]:
          - /url: /risk
          - img [ref=e61]
          - generic [ref=e64]:
            - generic [ref=e65]: Risk Intelligence
            - generic [ref=e66]: Quality risk score and contributors
        - link "Data Quality Readiness and validation findings" [ref=e67] [cursor=pointer]:
          - /url: /data-quality
          - img [ref=e69]
          - generic [ref=e71]:
            - generic [ref=e72]: Data Quality
            - generic [ref=e73]: Readiness and validation findings
        - link "Correlations Process-to-quality analytics" [ref=e74] [cursor=pointer]:
          - /url: /correlations
          - img [ref=e76]
          - generic [ref=e80]:
            - generic [ref=e81]: Correlations
            - generic [ref=e82]: Process-to-quality analytics
        - paragraph [ref=e83]: Intelligence
        - link "ML Readiness Labels, features and training gates" [ref=e84] [cursor=pointer]:
          - /url: /ml-readiness
          - img [ref=e86]
          - generic [ref=e98]:
            - generic [ref=e99]: ML Readiness
            - generic [ref=e100]: Labels, features and training gates
        - link "Demo Lifecycle Connector to ML result workflow" [ref=e101] [cursor=pointer]:
          - /url: /demo-lifecycle
          - img [ref=e103]
          - generic [ref=e106]:
            - generic [ref=e107]: Demo Lifecycle
            - generic [ref=e108]: Connector to ML result workflow
        - paragraph [ref=e109]: System
        - link "Admin Preview License, roles, ML scripts, report" [ref=e110] [cursor=pointer]:
          - /url: /admin-preview
          - img [ref=e112]
          - generic [ref=e114]:
            - generic [ref=e115]: Admin Preview
            - generic [ref=e116]: License, roles, ML scripts, report
        - link "Administrator DB config, schema mapping and jobs" [ref=e117] [cursor=pointer]:
          - /url: /admin
          - img [ref=e119]
          - generic [ref=e122]:
            - generic [ref=e123]: Administrator
            - generic [ref=e124]: DB config, schema mapping and jobs
        - link "Brand Identity, positioning and proof" [ref=e125] [cursor=pointer]:
          - /url: /brand
          - img [ref=e127]
          - generic [ref=e130]:
            - generic [ref=e131]: Brand
            - generic [ref=e132]: Identity, positioning and proof
      - generic [ref=e133]:
        - generic [ref=e134]:
          - img [ref=e135]
          - generic [ref=e140]: API
          - code [ref=e141]: http://localhost:5063
        - generic [ref=e142]:
          - img [ref=e143]
          - generic [ref=e148]: Phase 8–10 Interactive MVP
        - button "Dark mode" [ref=e149] [cursor=pointer]:
          - img [ref=e150]
          - text: Dark mode
    - main [ref=e152]:
      - generic [ref=e153]:
        - generic [ref=e155]:
          - generic [ref=e156]:
            - img [ref=e157]
            - generic [ref=e160]: Plant
            - strong [ref=e161]: Demo Plant
          - generic [ref=e162]:
            - img [ref=e163]
            - generic [ref=e166]: Status
            - strong [ref=e167]: Healthy
        - generic [ref=e168]:
          - generic [ref=e169]: Development
          - generic [ref=e170]: Demo
          - button "Logout" [ref=e171] [cursor=pointer]:
            - img [ref=e172]
            - text: Playwright E2E Admin
            - img [ref=e176]
      - generic [ref=e180]:
        - button "Demo mode on" [ref=e181] [cursor=pointer]:
          - img [ref=e182]
          - text: Demo mode on
        - generic [ref=e184] [cursor=pointer]:
          - img [ref=e185]
          - combobox [ref=e188]:
            - option "Light"
            - option "Pro"
            - option "Pro Plus" [selected]
            - option "Enterprise"
        - link "Run flow" [ref=e189] [cursor=pointer]:
          - /url: /demo-lifecycle
          - img [ref=e190]
          - text: Run flow
      - generic [ref=e193]:
        - generic [ref=e194]:
          - paragraph [ref=e195]:
            - img [ref=e196]
            - text: Process-to-Quality Intelligence Platform
          - heading "Industrial Analytics Command Center" [level=2] [ref=e199]
          - paragraph [ref=e200]: Digital plant data, genealogy, process history, quality events, risk scoring and correlation intelligence in one evidence-based manufacturing workspace.
        - generic [ref=e201]:
          - generic [ref=e202]:
            - img [ref=e203]
            - text: Rule-based intelligence
          - generic [ref=e206]:
            - img [ref=e207]
            - text: Interactive workspace
      - main [ref=e210]:
        - region "Dashboard filters" [ref=e211]:
          - generic [ref=e212]:
            - generic [ref=e213]:
              - img [ref=e214]
              - generic [ref=e215]: Global filters
            - button "Clear all" [disabled]:
              - img
              - text: Clear all
          - generic [ref=e216]:
            - generic [ref=e217]: Location
            - generic [ref=e218] [cursor=pointer]:
              - img [ref=e220]
              - generic: Site
              - combobox "Site" [ref=e224]:
                - option "All sites" [selected]
                - option "ADV_DEMO_PLANT — Advanced Demo Manufacturing Plant"
                - option "DEMO_PLANT_001 — Demo Manufacturing Plant"
                - option "DEMO_PLANT_002 — Demo Plant 002 - Aluminum / Multi-Site"
              - img
            - generic [ref=e225] [cursor=pointer]:
              - img [ref=e227]
              - generic: Area
              - combobox "Area" [ref=e229]:
                - option "All areas" [selected]
                - option "ALU_CAST_SHOP — Aluminum Casting Shop"
                - option "ALU_ROLLING — Aluminum Rolling Area"
                - option "CASTER_AREA — Caster Area"
                - option "HSM_AREA — Hot Strip Mill Area"
                - option "MANUFACTURING — Manufacturing"
                - option "MELT_SHOP — Melt Shop"
                - option "PHARMA_AREA — Pharma Demo Area"
                - option "QA_INSPECTION — Inspection Area"
                - option "QA_LAB — Quality Lab"
                - option "QUALITY — Quality"
                - option "TIRE_AREA — Tire Demo Area"
                - option "WAREHOUSE — Warehouse"
              - img
            - generic [ref=e230] [cursor=pointer]:
              - img [ref=e232]
              - generic: Equipment
              - combobox "Equipment" [ref=e234]:
                - option "All equipment" [selected]
                - option "ALU_CASTER_01 — Aluminum Caster 01"
                - option "ALU_FURNACE_01 — Aluminum Furnace 01"
                - option "ALU_MILL_01 — Aluminum Rolling Mill 01"
                - option "CASTER_1 — Continuous Caster 1"
                - option "CASTER_1_MOULD — Caster 1 Mould"
                - option "CASTER_1_SEGMENT_1 — Caster 1 Segment 1"
                - option "EAF_1 — Electric Arc Furnace 1"
                - option "HSM_1 — Hot Strip Mill 1"
                - option "HSM_1_F1 — Finishing Stand F1"
                - option "HSM_1_F2 — Finishing Stand F2"
                - option "LF_1 — Ladle Furnace 1"
                - option "PH_FILLER_1 — Pharma Filler 1"
                - option "PH_MIXER_1 — Pharma Mixer 1"
                - option "SURFACE_INSPECTION_1 — Surface Inspection 1"
                - option "TIRE_CURING_1 — Tire Curing Press 1"
                - option "TIRE_MIXER_1 — Tire Mixer 1"
              - img
            - generic [ref=e235] [cursor=pointer]:
              - img [ref=e237]
              - generic: Source
              - combobox "Source" [ref=e241]:
                - option "All source systems" [selected]
                - option "API_WRITE_WARNING_TEST — Intentional Non ReadOnly Test Source"
                - option "CMMS_ADV_DEMO — Advanced Demo CMMS"
                - option "ERP_ADV_DEMO — Advanced Demo ERP"
                - option "HIST_ADV_DEMO — Advanced Demo Historian"
                - option "L2_ADV_DEMO — Advanced Demo Level 2"
                - option "LAB_ADV_DEMO — Advanced Demo Lab"
                - option "MES_ADV_DEMO — Advanced Demo MES"
                - option "QMS_ADV_DEMO — Advanced Demo QMS"
                - option "SYNTHETIC_SEED — Synthetic Seed Data"
              - img
            - generic [ref=e242]:
              - img [ref=e244]
              - generic: Material
              - textbox "Material" [ref=e247]:
                - /placeholder: Search material / batch / lot
          - generic [ref=e248]:
            - generic [ref=e249]: Process & Quality
            - generic [ref=e250] [cursor=pointer]:
              - img [ref=e252]
              - generic: Parameter
              - combobox "Parameter" [ref=e254]:
                - option "CARBON_PCT — Carbon Percentage" [selected]
                - option "CASTING_SPEED — Casting Speed"
                - option "CoolingActive — Cooling Active"
                - option "CURING_PRESSURE_BAR — Curing Pressure"
                - option "CURING_TEMP_C — Curing Temperature"
                - option "FLATNESS_IUNIT — Flatness"
                - option "HUMIDITY_PCT — Humidity"
                - option "MOULD_ID — Mould ID"
                - option "PH_VALUE — pH Value"
                - option "RECIPE_CODE — Recipe Code"
                - option "ROLLING_FORCE — Rolling Force"
                - option "SUPERHEAT_C — Superheat"
                - option "UNIFORMITY_INDEX — Uniformity Index"
              - img
            - generic [ref=e255] [cursor=pointer]:
              - img [ref=e257]
              - generic: Defect
              - combobox "Defect" [ref=e259]:
                - option "All defects" [selected]
                - option "CENTER_BUCKLE — Center Buckle"
                - option "CONTAMINATION_RISK — Contamination Risk"
                - option "INCLUSION — Non-metallic Inclusion"
                - option "OOS_PH — Out of Specification pH"
                - option "SURFACE_CRACK — Surface Crack"
                - option "UNDER_CURE — Under Cure"
                - option "UNIFORMITY_DEFECT — Uniformity Defect"
              - img
            - generic [ref=e260] [cursor=pointer]:
              - img [ref=e262]
              - generic: Risk class
              - combobox "Risk class" [ref=e264]:
                - option "All risk classes" [selected]
                - option "High (3)"
                - option "Invalid (1)"
                - option "Low (2)"
                - option "Medium (1)"
              - img
            - generic [ref=e265] [cursor=pointer]:
              - img [ref=e267]
              - generic: Shift / Crew
              - combobox "Shift / Crew" [ref=e269]:
                - option "All shifts/crews" [selected]
                - option "CREW-B (1)"
                - option "Crew_A (3)"
                - option "Crew_B (1)"
                - option "Crew_C (3)"
                - option "PH_Crew_1 (2)"
                - option "QA_1 (1)"
                - option "TEST_CREW (1)"
                - option "TI_Crew_1 (2)"
              - img
            - generic [ref=e270] [cursor=pointer]:
              - img [ref=e272]
              - generic: Genealogy
              - combobox "Genealogy" [ref=e276]:
                - option "Same material only"
                - option "Downstream children" [selected]
                - option "Upstream parents"
                - option "Full genealogy"
              - img
          - generic [ref=e277]:
            - generic [ref=e278]: Time range
            - generic [ref=e279]:
              - img [ref=e281]
              - generic: From UTC
              - textbox "From UTC" [ref=e283]
            - generic [ref=e284]:
              - img [ref=e286]
              - generic: To UTC
              - textbox "To UTC" [ref=e288]
        - generic [ref=e289]: No active filters. Dashboard is showing the current available demo dataset.
        - generic [ref=e290]:
          - generic [ref=e291]:
            - generic [ref=e292]:
              - img [ref=e293]
              - text: Phase 10 Genealogy-aware correlation
            - heading "Process-to-Quality Correlation" [level=1] [ref=e298]
            - paragraph [ref=e299]: Connect upstream process parameters to downstream quality outcomes through material genealogy.
          - button "Recalculate" [ref=e300]:
            - img [ref=e301]
            - text: Recalculate
        - alert [ref=e306]:
          - img [ref=e308]
          - strong [ref=e310]: Could not load data
          - text: Parameter definition not found.
          - button "Retry" [ref=e311]:
            - img [ref=e312]
            - text: Retry
  - region "Notifications alt+T":
    - list:
      - listitem [ref=e317]:
        - button "Close toast" [ref=e318] [cursor=pointer]:
          - img [ref=e319]
        - generic [ref=e322]:
          - generic [ref=e323]: Parameter definition not found.
          - generic [ref=e324]: GET /analytics/correlations/parameter-defect/genealogy-aware?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8&minimumObservationsPerBin=3&linkMode=DownstreamChildren&genealogyDepth=3
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
  84  |       expect(consoleErrors, `Console errors:\n${consoleErrors.join("\n")}`).toEqual([]);
> 85  |       expect(failedRequests, `Failed requests:\n${failedRequests.join("\n")}`).toEqual([]);
      |                                                                                ^ Error: Failed requests:
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