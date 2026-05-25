# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: phase1-route-refresh.spec.ts >> PPIQ-HARD-001 / HARD-004 — route containment and refresh contract >> Admin should load directly and survive browser refresh
- Location: e2e\phase1-route-refresh.spec.ts:12:5

# Error details

```
Error: Failed requests:
GET http://localhost:5063/admin/jobs-monitor

expect(received).toEqual(expected) // deep equality

- Expected  - 1
+ Received  + 3

- Array []
+ Array [
+   "GET http://localhost:5063/admin/jobs-monitor",
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
      - main [ref=e194]:
        - generic [ref=e196]:
          - generic [ref=e197]:
            - img [ref=e198]
            - text: Administrator
          - heading "PlantProcess IQ Administrator" [level=1] [ref=e201]
          - paragraph [ref=e202]: Configure how each plant connects source data, stages raw snapshots, maps schemas into the canonical model, and monitors refresh jobs.
          - generic [ref=e203]:
            - generic [ref=e204]:
              - text: "Platform status:"
              - strong [ref=e205]: AttentionRequired
            - button "Refresh" [ref=e206]:
              - img [ref=e207]
              - text: Refresh
        - generic [ref=e212]:
          - generic [ref=e213] [cursor=pointer]:
            - img [ref=e215]
            - generic [ref=e220]:
              - generic [ref=e221]: Source Systems
              - strong [ref=e222]: "9"
              - generic [ref=e223]: 0 active
          - generic [ref=e224] [cursor=pointer]:
            - img [ref=e226]
            - generic [ref=e231]:
              - generic [ref=e232]: Import Batches
              - strong [ref=e233]: "6"
              - generic [ref=e234]: 1 running / 1 failed
          - generic [ref=e235] [cursor=pointer]:
            - img [ref=e237]
            - generic [ref=e242]:
              - generic [ref=e243]: Staging Records
              - strong [ref=e244]: "0"
              - generic [ref=e245]: 0 pending / 0 failed
          - generic [ref=e246] [cursor=pointer]:
            - img [ref=e248]
            - generic [ref=e252]:
              - generic [ref=e253]: Mappings
              - strong [ref=e254]: "5"
              - generic [ref=e255]: 5 active
          - generic [ref=e256] [cursor=pointer]:
            - img [ref=e258]
            - generic [ref=e260]:
              - generic [ref=e261]: Canonical Materials
              - strong [ref=e262]: "23"
              - generic [ref=e263]: 17 parameters
          - generic [ref=e264] [cursor=pointer]:
            - img [ref=e266]
            - generic [ref=e268]:
              - generic [ref=e269]: Quality Events
              - strong [ref=e270]: "6"
              - generic [ref=e271]: 7 DQ issues
          - generic [ref=e272] [cursor=pointer]:
            - img [ref=e274]
            - generic [ref=e277]:
              - generic [ref=e278]: Dashboards
              - strong [ref=e279]: "10"
              - generic [ref=e280]: Saved definitions
        - navigation [ref=e282]:
          - link "DB ConfigurationDB links and raw source snapshots" [ref=e283] [cursor=pointer]:
            - /url: /admin/db-configuration
            - img [ref=e284]
            - text: DB ConfigurationDB links and raw source snapshots
          - link "Schema ConfigurationMappings, views and canonical refresh" [ref=e289] [cursor=pointer]:
            - /url: /admin/schema-configuration
            - img [ref=e290]
            - text: Schema ConfigurationMappings, views and canonical refresh
          - link "Importing DataTwo-stage raw-to-canonical model" [ref=e292] [cursor=pointer]:
            - /url: /admin/importing-data
            - img [ref=e293]
            - text: Importing DataTwo-stage raw-to-canonical model
          - link "Jobs MonitorImport, canonical and ML jobs" [ref=e297] [cursor=pointer]:
            - /url: /admin/jobs-monitor
            - img [ref=e298]
            - text: Jobs MonitorImport, canonical and ML jobs
        - generic [ref=e300]:
          - generic [ref=e301]:
            - generic [ref=e302]:
              - img [ref=e304]
              - generic [ref=e315]:
                - heading "DB Link Configuration" [level=2] [ref=e316]
                - paragraph [ref=e317]: Connection profiles to customer source databases and files
            - button "New Connection Profile" [ref=e319]:
              - img [ref=e320]
              - text: New Connection Profile
            - generic [ref=e321]:
              - img [ref=e322]
              - strong [ref=e326]: No connection profiles yet
              - paragraph [ref=e327]: Click "New Connection Profile" to configure your first data source.
          - generic [ref=e328]:
            - generic [ref=e329]:
              - img [ref=e331]
              - generic [ref=e335]:
                - heading "Supported Connectors" [level=2] [ref=e336]
                - paragraph [ref=e337]: Available and planned data source provider types
            - generic [ref=e338]:
              - generic [ref=e339]:
                - generic [ref=e340]:
                  - strong [ref=e341]: CSV Snapshot
                  - generic [ref=e342]:
                    - img [ref=e343]
                    - text: Available
                - paragraph [ref=e346]: Available now. Reads CSV snapshot exports into the raw staging layer.
                - generic [ref=e347]: SchemaSnapshot
              - generic [ref=e348]:
                - generic [ref=e349]:
                  - strong [ref=e350]: Excel Snapshot
                  - generic [ref=e351]:
                    - img [ref=e352]
                    - text: Available
                - paragraph [ref=e355]: Available now. Reads Excel workbook/sheet snapshots into the raw staging layer.
                - generic [ref=e356]: SchemaSnapshot
              - generic [ref=e357]:
                - generic [ref=e358]:
                  - strong [ref=e359]: PostgreSQL Read-only DB Link
                  - text: Planned
                - paragraph [ref=e360]: Planned/conditional read-only connector for PostgreSQL source systems. Show as available only after demo-certification smoke tests are part of the API contract suite.
                - generic [ref=e361]: SchemaSnapshotIncremental
              - generic [ref=e362]:
                - generic [ref=e363]:
                  - strong [ref=e364]: Microsoft SQL Server Read-only DB Link
                  - text: Planned
                - paragraph [ref=e365]: Planned/conditional read-only connector for SQL Server / MSSQL source systems.
                - generic [ref=e366]: SchemaSnapshotIncremental
              - generic [ref=e367]:
                - generic [ref=e368]:
                  - strong [ref=e369]: MySQL Read-only DB Link
                  - text: Planned
                - paragraph [ref=e370]: Planned/conditional read-only connector for MySQL source systems and inspection devices.
                - generic [ref=e371]: SchemaSnapshotIncremental
              - generic [ref=e372]:
                - generic [ref=e373]:
                  - strong [ref=e374]: Oracle Read-only DB Link
                  - text: Planned
                - paragraph [ref=e375]: Planned read-only Oracle connector for MES/L2/QMS source systems.
                - generic [ref=e376]: SchemaSnapshotIncremental
              - generic [ref=e377]:
                - generic [ref=e378]:
                  - strong [ref=e379]: REST API Snapshot
                  - text: Planned
                - paragraph [ref=e380]: Future API snapshot connector. Not part of the current demo availability.
                - generic [ref=e381]: SnapshotIncremental
              - generic [ref=e382]:
                - generic [ref=e383]:
                  - strong [ref=e384]: OPC-UA / Historian
                  - text: Planned
                - paragraph [ref=e385]: Future historian/live-data path. Not part of the current demo availability.
                - generic [ref=e386]: Incremental
          - generic [ref=e387]:
            - generic [ref=e388]:
              - img [ref=e390]
              - generic [ref=e397]:
                - heading "Source Systems Overview" [level=2] [ref=e398]
                - paragraph [ref=e399]: Import batch statistics per source system
            - table [ref=e401]:
              - rowgroup [ref=e402]:
                - row "Code Name Type Status Batches Failed Last Import" [ref=e403]:
                  - columnheader "Code" [ref=e404]
                  - columnheader "Name" [ref=e405]
                  - columnheader "Type" [ref=e406]
                  - columnheader "Status" [ref=e407]
                  - columnheader "Batches" [ref=e408]
                  - columnheader "Failed" [ref=e409]
                  - columnheader "Last Import" [ref=e410]
              - rowgroup [ref=e411]:
                - row "API_WRITE_WARNING_TEST Intentional Non ReadOnly Test Source API Inactive 0 0 —" [ref=e412]:
                  - cell "API_WRITE_WARNING_TEST" [ref=e413]:
                    - strong [ref=e414]: API_WRITE_WARNING_TEST
                  - cell "Intentional Non ReadOnly Test Source" [ref=e415]
                  - cell "API" [ref=e416]
                  - cell "Inactive" [ref=e417]:
                    - generic [ref=e418]:
                      - img [ref=e419]
                      - text: Inactive
                  - cell "0" [ref=e422]
                  - cell "0" [ref=e423]
                  - cell "—" [ref=e424]
                - row "CMMS_ADV_DEMO Advanced Demo CMMS CMMS Inactive 1 1 Jan 1, 2026, 07:05 AM" [ref=e425]:
                  - cell "CMMS_ADV_DEMO" [ref=e426]:
                    - strong [ref=e427]: CMMS_ADV_DEMO
                  - cell "Advanced Demo CMMS" [ref=e428]
                  - cell "CMMS" [ref=e429]
                  - cell "Inactive" [ref=e430]:
                    - generic [ref=e431]:
                      - img [ref=e432]
                      - text: Inactive
                  - cell "1" [ref=e435]
                  - cell "1" [ref=e436]
                  - cell "Jan 1, 2026, 07:05 AM" [ref=e437]
                - row "ERP_ADV_DEMO Advanced Demo ERP ERP Inactive 0 0 —" [ref=e438]:
                  - cell "ERP_ADV_DEMO" [ref=e439]:
                    - strong [ref=e440]: ERP_ADV_DEMO
                  - cell "Advanced Demo ERP" [ref=e441]
                  - cell "ERP" [ref=e442]
                  - cell "Inactive" [ref=e443]:
                    - generic [ref=e444]:
                      - img [ref=e445]
                      - text: Inactive
                  - cell "0" [ref=e448]
                  - cell "0" [ref=e449]
                  - cell "—" [ref=e450]
                - row "HIST_ADV_DEMO Advanced Demo Historian Historian Inactive 1 0 Jan 1, 2026, 07:02 AM" [ref=e451]:
                  - cell "HIST_ADV_DEMO" [ref=e452]:
                    - strong [ref=e453]: HIST_ADV_DEMO
                  - cell "Advanced Demo Historian" [ref=e454]
                  - cell "Historian" [ref=e455]
                  - cell "Inactive" [ref=e456]:
                    - generic [ref=e457]:
                      - img [ref=e458]
                      - text: Inactive
                  - cell "1" [ref=e461]
                  - cell "0" [ref=e462]
                  - cell "Jan 1, 2026, 07:02 AM" [ref=e463]
                - row "L2_ADV_DEMO Advanced Demo Level 2 Level2 Inactive 1 0 Jan 1, 2026, 07:01 AM" [ref=e464]:
                  - cell "L2_ADV_DEMO" [ref=e465]:
                    - strong [ref=e466]: L2_ADV_DEMO
                  - cell "Advanced Demo Level 2" [ref=e467]
                  - cell "Level2" [ref=e468]
                  - cell "Inactive" [ref=e469]:
                    - generic [ref=e470]:
                      - img [ref=e471]
                      - text: Inactive
                  - cell "1" [ref=e474]
                  - cell "0" [ref=e475]
                  - cell "Jan 1, 2026, 07:01 AM" [ref=e476]
                - row "LAB_ADV_DEMO Advanced Demo Lab Lab Inactive 1 0 Jan 1, 2026, 07:04 AM" [ref=e477]:
                  - cell "LAB_ADV_DEMO" [ref=e478]:
                    - strong [ref=e479]: LAB_ADV_DEMO
                  - cell "Advanced Demo Lab" [ref=e480]
                  - cell "Lab" [ref=e481]
                  - cell "Inactive" [ref=e482]:
                    - generic [ref=e483]:
                      - img [ref=e484]
                      - text: Inactive
                  - cell "1" [ref=e487]
                  - cell "0" [ref=e488]
                  - cell "Jan 1, 2026, 07:04 AM" [ref=e489]
                - row "MES_ADV_DEMO Advanced Demo MES MES Inactive 1 0 Jan 1, 2026, 07:00 AM" [ref=e490]:
                  - cell "MES_ADV_DEMO" [ref=e491]:
                    - strong [ref=e492]: MES_ADV_DEMO
                  - cell "Advanced Demo MES" [ref=e493]
                  - cell "MES" [ref=e494]
                  - cell "Inactive" [ref=e495]:
                    - generic [ref=e496]:
                      - img [ref=e497]
                      - text: Inactive
                  - cell "1" [ref=e500]
                  - cell "0" [ref=e501]
                  - cell "Jan 1, 2026, 07:00 AM" [ref=e502]
                - row "QMS_ADV_DEMO Advanced Demo QMS QMS Inactive 1 0 Jan 1, 2026, 07:03 AM" [ref=e503]:
                  - cell "QMS_ADV_DEMO" [ref=e504]:
                    - strong [ref=e505]: QMS_ADV_DEMO
                  - cell "Advanced Demo QMS" [ref=e506]
                  - cell "QMS" [ref=e507]
                  - cell "Inactive" [ref=e508]:
                    - generic [ref=e509]:
                      - img [ref=e510]
                      - text: Inactive
                  - cell "1" [ref=e513]
                  - cell "0" [ref=e514]
                  - cell "Jan 1, 2026, 07:03 AM" [ref=e515]
                - row "SYNTHETIC_SEED Synthetic Seed Data SyntheticGenerator Inactive 0 0 —" [ref=e516]:
                  - cell "SYNTHETIC_SEED" [ref=e517]:
                    - strong [ref=e518]: SYNTHETIC_SEED
                  - cell "Synthetic Seed Data" [ref=e519]
                  - cell "SyntheticGenerator" [ref=e520]
                  - cell "Inactive" [ref=e521]:
                    - generic [ref=e522]:
                      - img [ref=e523]
                      - text: Inactive
                  - cell "0" [ref=e526]
                  - cell "0" [ref=e527]
                  - cell "—" [ref=e528]
          - generic [ref=e529]:
            - generic [ref=e530]:
              - img [ref=e532]
              - generic [ref=e535]:
                - heading "Raw Snapshot Import Schedule" [level=2] [ref=e536]
                - paragraph [ref=e537]: Configure how often each DB link copies new rows into the staging layer
            - paragraph [ref=e538]: Each import job reads from the source database and copies rows into the PlantProcess IQ raw staging layer. Use the cursor field on each dataset to enable delta (incremental) imports.
            - generic [ref=e539]:
              - text: Connection Profile
              - combobox [disabled] [ref=e540]:
                - option "No connections configured yet" [selected]
            - generic [ref=e541]:
              - text: Import Frequency
              - combobox [ref=e542]:
                - option "Every 2 min"
                - option "Every 5 min"
                - option "Every 10 min"
                - option "Every 15 min" [selected]
                - option "Every 30 min"
                - option "Every 1h"
                - option "Every 2h"
                - option "Every 6h"
                - option "Every 12h"
                - option "Once daily"
            - button "Save Import Schedule" [disabled] [ref=e544]:
              - img [ref=e545]
              - text: Save Import Schedule
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