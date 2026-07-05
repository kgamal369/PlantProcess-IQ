# PPIQ Journey-Walk Evidence - 2026-07-05 14:40

| Task | Check | Result | Detail |
|---|---|---|---|
| V1-17 | Login returns a real signed JWT | **FAIL** | Unable to connect to the remote server |
| V1-17 | Authorized /admin/overview | **FAIL** | Unable to connect to the remote server |
| V1-17 | Browser cold load: nav visible, populated panel, ZERO console errors, __Host- cookie present | **MANUAL** | Open the demo URL in a fresh browser profile; F12 console must be clean. |
| V1-13 | Readiness endpoint | **FAIL** | Unable to connect to the remote server |
| V1-18 | List connection profiles | **FAIL** | Unable to connect to the remote server |
| V1-18 | Unknown-profile test error typing | **FAIL** | HTTP 0 Unable to connect to the remote server |
| V1-18 | Registry rows (registered tables): 49 | **PASS** |  |
| V1-18 | Stopped-container test shows a typed error in the HMI (stop the meltshop container, click Test, restart it) | **MANUAL** | docker stop <meltshop>; HMI Test -> typed error; docker start <meltshop> |
| V1-19 | Stage-1 run | **FAIL** | Unable to connect to the remote server |
| V1-19 | Forced-failure shows an Error entry in monitor + log (stop container, run Stage-1 from HMI, restart) | **MANUAL** | Job-log Error entries arrive with V1-45; until then the monitor state is the evidence. |
| V1-21 | Stage-2 run | **FAIL** | Unable to connect to the remote server |
| V1-21 | Canonical populated: material_units=49 (was -1) | **PASS** |  |
| V1-21 | Canonical views present: 5 | **PASS** | canonical_downtime_events, canonical_equipment, canonical_genealogy_edges, canonical_material_units, canonical_quality_events |
| V1-22 | Seam-6 live re-import delta | **FAIL** | Unable to connect to the remote server |
| V1-22 | Safe-SQL preview | **FAIL** | Unable to connect to the remote server |
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

**Totals: 3 PASS / 10 FAIL / 6 MANUAL / 6 EVIDENCE**
