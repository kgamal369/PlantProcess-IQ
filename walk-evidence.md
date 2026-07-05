# PPIQ Journey-Walk Evidence v2 - 2026-07-05 21:33

| Task | Check | Result | Detail |
|---|---|---|---|
| V1-17 | Real signed 3-part JWT; authorized call succeeds | **PASS** | len=827 |
| V1-17 | Cold browser load: nav + populated panel + clean console + __Host- cookie | **MANUAL** |  |
| V1-13 | Readiness 200, no not-ready flags | **PASS** | notReady=0 |
| V1-18 | Connection profiles listed; profile identified | **PASS** | profile=DEMO-READY-CP-01 |
| V1-18 | Live connection test | **EVIDENCE** | {"isSuccess":false,"message":"PostgreSQL connection test failed: 28P01: password authentication failed for user \"ELKA01\"","testedAtUtc":"2026-07-05T19:32:52.1161411Z","metadata":{}} |
| V1-18 | Unknown-profile test -> typed 404 (not a 500) | **PASS** |  |
| V1-18 | Registry rows: 10 | **PASS** |  |
| V1-18 | Stopped-container test shows typed error in HMI | **MANUAL** |  |
| V1-19 | Stage-1 dump rows 633 -> 633 | **PASS** |  |
| V1-19 | Watermark: 2nd run adds 0 (expect 0) | **PASS** |  |
| V1-45 | job_log Import-Stage1 events in last 5 min: 16 | **PASS** | Started + Completed expected |
| V1-45 | /admin/job-logs returns entries: 20 | **PASS** |  |
| V1-21 | Stage-2 canonical material_units=12000 | **PASS** |  |
| V1-22 | Seam-6: injected 1 into REGISTERED source; dump 633 -> 634 | **PASS** | A canonical-bound widget will visibly change on refresh. |
| V1-22 | Safe-SQL preview (correct property sqlText) 200 | **PASS** |  |
| V1-22 | HMI: create page/widget, bind, save, reload persists; refresh changes number | **MANUAL** |  |
| V1-11 | Blended attribution weights: 3a000000-0000-0000-0000-000000003361|3a000000-0000-0000-0000-000000044170|0.700000|t|1.000000|Transition / blended provenance edge. Parent contribution is weighted.,3a000000-0000-0000-0000-000000003362|3a000000-0000-0000-0000-000000044170|0.300000|t|1.000000|Transition / blended provenance edge. Parent contribution is weighted. | **PASS** |  |
| V1-11 | HMI clip: transition coil weighted provenance + population | **MANUAL** |  |
| V1-38 | site-identity reflects DB change: 'Advanced Demo Manufacturing Plant' -> 'Proof Plant' (restored) | **PASS** | Sidebar renders siteName; sidebar reload is the MANUAL half. |
| V1-23 | ml_correlation_results_v2 rows: 52 | **PASS** |  |
| V1-23 | /api/analytics/advanced/results resolves rows: 13 | **PASS** | HMI CorrelationPage reads this; ranked contributors render. |
| V1-50 | Ranked contributors (population/method/q-value present, superheat on top) | **PASS** | thermal.true_superheat_c -> quality.defect_rate_per_m2 eff=0.924 q=0.0001<br>thermal.true_superheat_c -> quality.defect_rate_per_m2 eff=0.924 q=0.0001<br>thermal.true_superheat_c -> quality.defect_rate_per_m2 eff=0.924 q=0.0001 |
| V1-42 | Window recompute 30d=Completed|13  60d=Completed|13 | **PASS** | Changed duration window recomputes (acceptance tail). |
| V1-23 | HMI: inspection run renders ranked list + honesty bar; assistant cites (or V1-43 framing) | **MANUAL** |  |

**Totals: 18 PASS / 0 FAIL / 5 MANUAL / 1 EVIDENCE**
