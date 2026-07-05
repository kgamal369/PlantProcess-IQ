# PPIQ Journey Proof - 2026-07-03 14:31

| Status | Task | Step | Evidence |
|---|---|---|---|
| PASS | V1-17 J1 | POST /auth/login returns accessToken (real signed session) | token length 827 |
| PASS | V1-17 J1 | GET /admin/overview 200 with populated payload | keys: cards,generatedAtUtc,latestImportBatch,status |
| MANUAL | V1-17 J1 | HMI: cold-load  | Open the web URL in a FRESH incognito window: lands authenticated on a populated home, no login prompt, F12 console shows zero errors. |
| PASS | V1-13 | GET /admin/two-stage-import/overview -> 200 | two-stage overview (registries) |
| PASS | V1-13 | GET /admin/connectors/connection-profiles -> 200 | connection profiles |
| FAIL | V1-13 | GET /admin/connectors/readiness | API GET /admin/connectors/readiness failed: HTTP 404  |
| PASS | V1-13 | GET /admin/jobs-monitor -> 200 | jobs monitor |
| PASS | V1-13 | GET /admin/schema-configuration/summary -> 200 | schema-configuration summary |
| PASS | V1-18 J2 | connection profiles listed (8) | , , , , , , ,  |
| FAIL | V1-18 J2 | meltshop profile not identified by code/name | expected a profile matching meltshop/CP-01 |
| PASS | V1-18 J2 | source_table_dump_registry rows = 1 | registry populated |
| MANUAL | V1-18 J2 | HMI walk | Admin > DB Configuration: open meltshop profile, click Test Connection (green), open the table picker (populates from live source), then break it once: stop the container, Test again -> typed error not a crash. |
| PASS | V1-19 J3 | POST /admin/two-stage-import/stage1/run (all registries) -> 200 | rows payload length 4057 |
| PASS | V1-19 J3 | staging populated: src_meltshop_pg.heats = 54 (was 54) |  |
| PASS | V1-19 J3 | WATERMARK PROOF: immediate second Stage-1 imports 0 new rows | count stable at 54 |
| PASS | V1-19 J3 | runs endpoint lists the import runs | payload length 38030 |
| MANUAL | V1-19 J3 | HMI walk | Admin > Importing Data: trigger Stage-1; Admin > Jobs Monitor: the run shows status/rows/duration; run again -> 0 new rows visible. |
| PASS | V1-20 J4 | schema-configuration summary returns source objects | payload length 2597 |
| FAIL | V1-20 J4 | mapper/preview seam | API POST /admin/schema-configuration/views/preview failed: HTTP 400 {"isSuccess":false,"message":"SQL safety validation failed: Table or view 'src_meltshop_pg.heats' is not in the configured SQL allowlist.","rowCount":0,"durationMs":0,"columns":[],"rows":[]} |
| MANUAL | V1-20 J4 | HMI walk | Admin > Schema Configuration: mapper lists the real staging tables/columns; define a view + join across two dump tables; preview shows real rows; paste a bad SQL -> typed validation error. |
| PASS | V1-21 J5 | POST stage2/run -> 200 | payload length 3594 |
| PASS | V1-21 J5 | canonical material_units = 49 (was 49) |  |
| FAIL | V1-22 J6 | seam-6 proof | docker exec insert failed (is the meltshop container running?) |
| MANUAL | V1-22 J6 | HMI walk | Page Builder: create a page, drag a widget, bind it to a canonical view, save+reload (persists). Note its number, re-run the import from Importing Data, refresh: the number changes. |
| PASS | V1-10 | walk(C-0044170, both) returns BOTH heats H-3361 + H-3362 | jsonb length 562 |
| PASS | V1-10 | walk direction=backward resolves | jsonb length 562 |
| PASS | V1-10 | walk direction=forward resolves | jsonb length 164 |
| MANUAL | V1-10 | HMI walk + clip | Material Investigation: search C-0044170, open it, walk coil->melt then melt->coils in the evidence panel. Record the clip (this is the dry-run artifact). |
| PASS | V1-11 | blended attribution returns weighted split: 0.700000,0.300000 | H-3361/H-3362 |
| MANUAL | V1-11 | HMI walk + clip | Material Investigation on the transition coil: the panel shows the weighted 70/30 provenance across both heats with the population stated. Record the clip. |
| MANUAL | V1-14 | action-matrix e2e (run on your machine with app up) | cd Frontend\PlantProcess.Web ; npx playwright test e2e/phase9-action-matrix.spec.ts --project=chromium   (matrix now enumerates the six admin tabs) |
