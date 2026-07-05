# PPIQ Journey-Walk Evidence - 2026-07-05 14:51

| Task | Check | Result | Detail |
|---|---|---|---|
| V1-17 | Login returns a real signed 3-part JWT with future exp | **PASS** | len=827 exp=2026-07-05 13:51:19Z sub=00000000-0000-0000-0000-000000000102 |
| V1-17 | Authorized call to /admin/overview succeeds (no 401) | **PASS** |  |
| V1-17 | Browser cold load: nav visible, populated panel, ZERO console errors, __Host- cookie present | **MANUAL** | Open the demo URL in a fresh browser profile; F12 console must be clean. |
| V1-13 | Readiness endpoint 200 with no not-ready flags | **PASS** | bytes=729 |
| V1-18 | Connection profiles listed; meltshop profile identified | **PASS** | profile=DEMO-READY-CP-01 id=dddd0000-0000-0000-0000-000000000201 |
| V1-18 | Live connection test | **FAIL** | The remote server returned an error: (400) Bad Request. |
| V1-18 | Unknown-profile test returns a typed 4xx error (not a 500 crash) | **PASS** | HTTP 404 |
| V1-18 | Registry rows (registered tables): 49 | **PASS** |  |
| V1-18 | Stopped-container test shows a typed error in the HMI (stop the meltshop container, click Test, restart it) | **MANUAL** | docker stop <meltshop>; HMI Test -> typed error; docker start <meltshop> |
| V1-19 | Stage-1 run accepted (HTTP 200) | **PASS** | {"generatedAtUtc":"2026-07-05T12:51:28.4401466Z","stage":"Stage1DeltaImport","rows":[{"runId":"54341a80-2a1a-4e94-9bbf-ed0b16cc206b","registryId":"d9574038-c296-400b-8f43-9a56fbe1e97c","status":"Ok","insertedRows":0,"skippedExistingRows":0,"lastIndexBefore":"2026-05-01 20:20:00+03","lastIndexAfter": |
| V1-19 | Staging populated: heats=54 (was 54) | **PASS** |  |
| V1-19 | WATERMARK: second run adds 0 rows (expected 0) | **PASS** |  |
| V1-19 | Jobs Monitor payload contains the Stage-1 run | **PASS** | bytes=7513 |
| V1-19 | Forced-failure shows an Error entry in monitor + log (stop container, run Stage-1 from HMI, restart) | **MANUAL** | Job-log Error entries arrive with V1-45; until then the monitor state is the evidence. |
| V1-21 | Stage-2 run accepted (HTTP 200) | **PASS** | {"generatedAtUtc":"2026-07-05T12:51:31.2863316Z","stage":"Stage2CanonicalRefresh","rows":[{"runId":"4df25a89-3102-4033-987d-8f021ec7fccb","registryId":"d9574038-c296-400b-8f43-9a56fbe1e97c","status":"Ok","canonicalRows":0,"lastIndexBefore":"2026-05-01 20:20:00+03","lastIndexAfter":"2026-05-01 20:20: |
| V1-21 | Canonical populated: material_units=49 (was 49) | **PASS** |  |
| V1-21 | Canonical views present: 5 | **PASS** | canonical_downtime_events, canonical_equipment, canonical_genealogy_edges, canonical_material_units, canonical_quality_events |
| V1-22 | SEAM-6: injected 1 source heat -> canonical count 49 -> 49 | **FAIL** | A widget bound to canonical views WILL visibly change on refresh; injected heat_id=WALK-145132 |
| V1-22 | Safe-SQL preview | **FAIL** | The remote server returned an error: (400) Bad Request. |
| V1-22 | HMI: create page, drag widget, bind, save, reload persists; refresh after re-import changes the number | **MANUAL** | This is the customer-visible half of seam-6. |
| V1-11 | Blended attribution SQL | **FAIL** | ERROR:  invalid input syntax for type uuid: "3" |
| V1-11 | HMI clip: transition coil shows weighted provenance + population (Material Investigation) | **MANUAL** | Record the clip during rehearsal #1. |
| V1-23 | Run-status census | **EVIDENCE** | Running/347  //  Blocked/10  //  NoData/1 |
| V1-23 | Latest 3 runs | **EVIDENCE** | 76978b80/ppiql-deterministic-core-v1/defect/Running/2026-06-29 11:53:41.859983+03//0/<null><br>ce9164f1/ppiql-deterministic-core-v1/downtime/Running/2026-06-29 11:53:41.859983+03//0/<null><br>9d2d1860/ppiql-deterministic-core-v1/kpi/Running/2026-06-29 11:53:41.859983+03//0/<null> |
| V1-23 | Distinct failure messages (THE ROOT-CAUSE CANDIDATES) | **EVIDENCE** | Blocked/10/Blocked by the data-readiness gate; analysis refused (honest abstain).<br>NoData/1/Managed statistical engine. Findings=0. |
| V1-23 | ml_correlation_results_v2 rows: 0 | **EVIDENCE** |  |
| V1-23 | Zombie Running runs older than 1h: 3 | **EVIDENCE** | These become Failed(timeout-backfill) in V1-41. |
| V1-23 | No reachable job-list route auto-triggered a correlation run | **EVIDENCE** | Trigger one inspection from the HMI (Advanced Analysis) right after this script, then re-run ONLY the forensic SQL above. |
| V1-23 | HMI: inspection run from Advanced Analysis renders ranked contributor + honesty bar; assistant explains with citations | **MANUAL** | Gated on V1-42 fix; wire-or-frame decision 07-Jul noon. |

**Totals: 13 PASS / 4 FAIL / 6 MANUAL / 6 EVIDENCE**
