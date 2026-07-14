# PPIQ Canonical Journey Walk v2 - Deep Verification Script (M1-11)
**v2.0 - 13-Jul-2026.** Supersedes v1. Density contract: every journey step has 10-24 numbered
testing steps. Accuracy contract: labels/placeholders/toasts below marked [V] were read from
source; [C] = confirm exact wording on first walk and correct inline; [GAP-n] = Gap Register.

## Preflight (P1-P10)
P1. DB probe (must return `1` in <5s):
    `$env:PGPASSWORD='ppiq_dev_local_only'; & "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h 127.0.0.1 -p 5432 -U ppiq_dev -d ppiq_app -w -X -A -t -c "SELECT 1;"`
P2. Start API: `.\scripts\run\start-api.ps1 -Profile local`; wait for Kestrel on 5063 (via
    `deploy\scripts\ppiq.ps1 up` you get the M1-13 line `API is listening on :5063` [V]).
P3. Start web from `Frontend\PlantProcess.Web`: `.\node_modules\.bin\vite --host localhost --port 5173`.
P4. Browser `http://localhost:5173` -> login form. F12 DevTools -> Network tab ON for the whole walk.
P5. Login `e2eadmin` / `E2EAdmin123!` -> `POST /api/auth/login` -> 200 LoginResponse (token, user,
    entitlements); stored in localStorage key `plantprocess.auth.user` [V].
P6. Negative test: log out, wrong password -> 401 with clean problem body {title,errorCode,traceId},
    NO stack trace (M1-10 shape [V]). Log back in.
P7. App shell [V]: left sidebar, groups incl. Data Integration and Intelligence; Journey Rail
    (M1-17) at top of main content: 10 nodes `Connect Register Import Map Load Dashboards Analysis
    Findings Alerts Assistant`; current node highlighted.
P8. Sidebar Data Integration order [V]: `Plant Data Log`, `Engine Supervisor`, `Load to Plant Data`
    (this session's inserts, head of group), then `Connections`, `Table Registry`, `Prepare Import`,
    `Importing`, `Jobs Monitor`, `Connector Truth`.
P9. Intelligence group [V]: `Command Dashboard`, `Widget Drift`, `Material Investigation`,
    `Risk Intelligence`, `Data Quality`, `Correlations`, value pages, `ML Readiness`, `Suggestions`,
    `Assistant`, `Assistant Configuration`.
P10. Rule for the whole walk: any 500 -> open the response body; it must be the M1-10 problem
    shape. Record the traceId; that pairs it to the server log line.

---

## Journey Step 1 - Connect (create/configure DB-links; all database kinds) - 22 steps
1. Sidebar -> Data Integration -> **Connections** (icon DatabaseZap, desc "DB links: connect and
   test plant sources" [V]). Route `/data-integration/connections`. Rail: **Connect** highlighted.
2. Layout note [V]: the Data Integration pages share one parent (`DataIntegrationLayout`) with
   header title "Data Integration", subtitle "Connect plant sources, map them to the canonical
   model, run imports and watch every job.", a **Refresh** button (secondary, RefreshCw icon), and
   the read-only promise line "Connections are read-only toward your source systems at all times."
3. Page fires in parallel [V]: `GET /admin/connectors/connection-profiles?includeSecrets=true?`
   (productApi.getConnectionProfiles(true)) and `GET /admin/connectors/provider-types` -> both 200.
4. Expect panel **"DB Link Configuration"** ("Connection profiles to customer source databases and
   files") in LIST mode: table of existing profiles [V]. Emulated-factory profiles (meltshop-PG,
   parsytec-MySQL, pkl-MSSQL, caster/HSM-Oracle) should be present if previously created.
5. Expect second panel **"Supported Connectors"** ("Available and planned data source provider
   types") - a grid of provider types [V]. Verify it includes PostgreSQL, MySQL, SQL Server,
   Oracle, CSV [C exact display names] - this is the "all kinds of database" evidence on screen.
6. Click **"New Connection Profile"** (primary button, Plus icon) [V]. The panel switches to FORM
   mode (same page; a Back/secondary button returns to list [V]).
7. Form fields [V by placeholder]: Name ("e.g. Production MES Database"), Code ("Auto-generated if
   empty"), Provider type (dropdown from step 3), Host ("e.g. 192.168.1.100 or db.plant.local"),
   Database ("e.g. mes_production"), Schema ("e.g. public / dbo"), user/password, and for CSV a
   File path ("e.g. /data/imports or C:\Imports").
8. Validation test: click Save with Name empty -> expect blocked with an inline error, no network
   call [C exact message].
9. Fill the form against a LIVE emulated source (e.g. meltshop Postgres container host/port).
10. Click **Save**. FE uses an optimistic-save hook [V]: success -> green toast, auto-dismiss,
    return to list. Network: `POST /admin/connectors/connection-profiles` -> 200/201 {id,...}.
11. Confirm the new profile row appears in the list without manual refresh [V - onSaved reloads].
12. Credentials masking: open the row's Edit [V - edit mode exists]; the password must render
    masked, never plaintext [C rendering].
13. Click the row's **Test** action -> `POST /admin/connectors/connection-profiles/{id}/test` [V]
    -> 200 with a ConnectionTestResult (success flag + message/latency [C fields]). Expect a green
    result indicator.
14. Negative test: stop the source container (`docker stop <meltshop>`), Test again -> expect a
    clean failure result rendered in-page (red pill/message), NOT an exception page. Restart the
    container, Test -> green again.
15. Read-only enforcement statement: confirm the layout line from step 2 is visible on screen
    (constitution talking point).
16. Import-schedule config on the connection [V - UpdateConnectionImportScheduleRequest exists]:
    locate the schedule control on the profile (interval/window) [C placement], set a value, Save
    -> expect a toast and a PUT/POST to the connection schedule endpoint [C exact route].
17. Throttling fields (row caps / rate limits / approved windows): check whether the form exposes
    them [C]; if absent, that is GAP-16 (record) - the backend contract supports throttled
    connectors.
18. Edit round-trip: change the profile Name, Save -> `PUT/POST updateConnectionProfile` [V method
    exists] -> list shows the new name.
19. Repeat steps 6-13 once for a SECOND provider type (e.g. the MySQL parsytec source) to prove
    multi-DB on screen.
20. DevTools check: none of the above requests may exceed ~2s against local sources [C]; slow =
    note it for the demo pacing.
21. DB truth: `SELECT count(*) FROM connection_profiles;` [C table name - if different, correct
    here] via the P1 psql pattern; expect it to have grown by 2.
22. PASS Step 1: two live profiles (different providers) created in the UI, both Test green,
    masked credentials, clean failure on dead source, schedule saved.

## Journey Step 2 - Register datasets + schedule imports - 16 steps
1. Sidebar -> **Table Registry** (`/data-integration/registry`, desc "Map source tables to the
   canonical model" [V]). Rail: **Register**.
2. Page renders the schema-configuration surface (SchemaConfigurationTab [V]) reading the layout's
   single load; click the layout **Refresh** if stale.
3. Select the Step-1 connection profile [C selector placement].
4. Expect a live SOURCE table browse: `GET /admin/connectors/connection-profiles/{id}/tables` [V]
   -> 200 schema/table list. Seeing the customer's own tables is the demo beat.
5. Select a table (e.g. meltshop `heats`); columns load:
   `GET /admin/connectors/connection-profiles/{id}/tables/{schema}/{table}/columns` [V] -> 200
   names+types. Verify a few columns match the real source.
6. Click **Register** [C label] on the table -> `POST
   /admin/connectors/connection-profiles/{id}/register` [V] -> 200/201; a registered dataset
   (source_table_dump_registry) is created; constitution: registration makes it DUE.
7. [GAP-1 watch] If a dataset call returns 500 but the dataset appears after Refresh, data
   persisted - record as UI-only defect and continue.
8. Registered dataset appears in the registry list with its source binding [C columns shown].
9. Sidebar -> **Prepare Import** (`/data-integration/prepare`, desc "Pick columns, keys and
   watermark" [V]). Rail: **Register** (or Import [C]).
10. For the registered dataset: choose imported columns, business key, and the WATERMARK column
    (timestamp) [C exact form controls]; this is the incremental cursor of step 3.
11. Save the prep -> expect a toast + a POST [C route]; re-open to confirm persistence.
12. Register a SECOND dataset (the taxonomy source, e.g. `meltshop_defect_definitions`) - needed
    for step 4's taxonomy-first discipline (M1-03).
13. Sidebar -> **Jobs Monitor** (`/data-integration/jobs`). Table columns [V]: Job, Type, Target,
    Status, Last Run, Duration, Runtime, Actions.
14. Expect an import job row for the registered dataset(s), Status due/idle [C wording].
15. Actions test [V]: **Pause** -> productApi.pauseJob -> StatusPill shows "Paused"; **Resume** ->
    back to normal. (Run now is step 3.)
16. PASS Step 2: two datasets registered from a live browse; watermark configured; jobs visible,
    pause/resume works.

## Journey Step 3 - Incremental import (delta -> staging) - 14 steps
1. In **Jobs Monitor**, click **Run now** on the taxonomy dataset job first [V:
   productApi.runJobNow(jobId, "Admin UI")]. Expect a toast + the row's Status/Last Run updating.
2. Then Run now on the readings dataset job.
3. Alternative surface: **Importing** (`/data-integration/importing`). After the GAP-2 pack its
   nav desc reads "Run import jobs and watch batches" [V]. NOTE [GAP-13]: this tab still shows two
   dead Arch-B panels ("Two-Stage Import Model" - "The two-stage model is not available yet." and
   "Current import metrics" - "No import metrics available yet." [V placeholders]). They are
   benign placeholders; ignore, do not screen-share closely.
4. Verify batch via API: `GET /integration/import-batches` [V] -> 200 array; newest rows carry
   your sourceObjectName + status + startedAtUtc.
5. DB truth (P1 pattern): `SELECT count(*) FROM import_batches; SELECT count(*) FROM
   staging_records;` -> both grew.
6. Inspect one staging row: `SELECT raw_json FROM staging_records ORDER BY created_at_utc DESC
   LIMIT 1;` [C column names] -> rawJson mirrors a real source row.
7. Delta discipline: Run now AGAIN with no new source rows -> expect zero/small delta (cursor
   honored), fast completion.
8. [GAP-3 watch] If the re-run errors: 42883 = cursor-as-text-vs-timestamptz defect; invalid WHERE
   = null-cursor defect; wrong date parse on German locale = locale defect. Record which fired -
   these MUST be fixed before rehearsal.
9. Seam test: insert ONE row into the emulated SOURCE table (psql/mysql into the container), Run
   now -> staging grows by exactly 1.
10. Monitor: the import run appears in Jobs Monitor Last Run/Duration [V columns], and in
    `GET /admin/job-logs` filtered to the import job family [V endpoint].
11. Throttling observance [C]: with a row cap configured in step 1.16, a large table import stops
    at the cap - verify if configured.
12. Journey rail on these pages highlights **Import** [V].
13. Negative test: Run now with the source container stopped -> job fails CLEANLY (status +
    message in monitor), no unhandled exception; restart container.
14. PASS Step 3: batches + staging grow; second run is delta-only; seam row propagates; failures
    are clean.

## Journey Step 4 - UI-1 Data Preparation / author the mapping (M1-04) - 24 steps
1. Sidebar -> **Load to Plant Data** (desc "Author a mapping and project staged rows", icon
   Network [V]). Route `/data-integration/author-mapping`. Rail: **Map**.
2. Mount fires `GET /integration/import-batches` [V]; pending text `Loading import batches...` [V].
3. Empty-state check (only if DB had no batches): "No import batches yet. Connect a source and
   import data first (steps 1-3), then return here." [V]. If this shows WITH batches existing,
   the response is wrapper-shaped -> report; page degrades by design.
4. Expect dropdown **Import batch** [V], options formatted `<sourceObjectName> - <status> -
   <startedAtUtc>` [V], defaulting to the first batch.
5. Expect dropdown **Target entity** [V] with EXACTLY 8 options: DefectCatalog,
   ParameterDefinition, MaterialUnit, MaterialAlias, ProcessStepExecution, ParameterObservation,
   QualityEvent, GenealogyEdge [V].
6. Select the TAXONOMY batch (defect definitions) first - M1-03 discipline.
7. Hint line renders [V]: `Source object <name> from system <system>. Source can be a column name
   or const:VALUE for a literal.`
8. Pick Target entity **DefectCatalog** -> grid seeds suggested targets [V]: DefectCode,
   DefectName, DefectCategory.
9. Fill Source cells with REAL source column names from Step 2.5's column browse; use
   `const:VALUE` for one field to prove literals.
10. Grid mechanics [V]: **Add field** appends an empty row; the `x` button removes a row.
11. Validation 1 [V]: clear the batch selection impossible (always one selected) - instead test
    with zero complete rows: blank all Source cells, click **Save mapping** -> error "Add at least
    one field map (target field + source column or const:VALUE)."
12. Refill, click **Save mapping** [V] -> `POST /integration/mapping-definitions` with body
    { sourceSystemDefinitionId, mappingCode "UI-<obj>-<entity>-<ts>", mappingName,
    sourceObjectName, targetEntityName, mappingJson, mappingVersion "v1", description,
    isSynthetic false, sourceSystem, sourceRecordId null } [V] -> 200 { id }.
13. Green notice [V]: `Mapping saved (id ...). Now Execute to project this batch.`
14. Click **Execute (project)** [V] -> `POST /integration/mapping-definitions/{id}/execute?
    importBatchId=<batch>&stopOnFirstError=false` [V] -> 200.
15. Result panel [V]: `Projection result` + Mapped/Failed/Total when present + ALWAYS the raw JSON
    (defensive display).
16. DB truth: `SELECT count(*), min(source_system) FROM defect_catalogs;` -> grew, and
    source_system is your connector's system (NOT 'PPIQ_CONFIG').
17. Repeat 6-15 for `..._param_definitions -> ParameterDefinition`.
18. Now the READINGS batch -> Target **ParameterObservation**; seeded targets [V]: MaterialCode,
    ParameterCode, ObservedAtUtc, NumericValue. Map from real columns; Save; Execute.
19. DB truth: `SELECT count(*) FROM parameter_observations;` grew;
    `SELECT count(*) FROM staging_records WHERE status='Mapped';` grew (rows flipped) [C status
    literal].
20. Mapper field-error honesty: map one target to a nonsense column, Save+Execute -> expect
    Failed>0 in the result with typed field errors in job logs, not a crash; then fix the map.
21. Quality events flow: map the defects/events source -> **QualityEvent** (targets [V]:
    MaterialCode, DefectCode, EventType, EventAtUtc); Execute; verify quality_events grew AND
    `SELECT count(*) FROM quality_events WHERE defect_catalog_id IS NULL;` did not grow (resolver
    found the imported catalog - the M1-03 acceptance).
22. Genealogy: map parent/child keys -> **GenealogyEdge** (targets [V]: ParentMaterialCode,
    ChildMaterialCode, RelationshipType); verify genealogy_edges grew and the sum=1.0 trigger did
    not reject (no constraint error).
23. Idempotency: Execute the SAME mapping+batch again -> no duplicate canonical rows (projector is
    idempotent per batch) - counts unchanged.
24. PASS Step 4: taxonomy-first imports produce real catalogs; observations/events/edges project;
    both client validations + field-error path behave; idempotent re-execute.

## Journey Step 5 - Loading jobs (schedule + monitor per mapping) - 12 steps
1. Surface [V - found in code]: **Importing** tab hosts a "mapping refresh schedule" control:
   a mapping selector + interval minutes (default 15) + Save.
2. Select the ParameterObservation mapping from step 4 [C selector label].
3. Set interval (e.g. 15) and Save -> [V] `productApi.updateMappingRefreshSchedule(mappingId,
   { scheduleExpression: "Every 15 minutes", refreshIntervalMinutes: 15 })`.
4. Expect toast [V]: "Canonical refresh schedule saved and JobDefinition updated".
5. Jobs Monitor: expect a job row of Type **CanonicalRefresh** [V - the tab filters this type]
   targeting the mapping.
6. Run now on that CanonicalRefresh job -> completes; Last Run updates.
7. Auto-run-on-import check: insert one source row, run the import job, then verify the projector
   ran for the batch's active mapping (canonical count +1 without manual Execute) - the seam-6
   behavior. If it does not auto-run, record it: demo uses schedule/manual [GAP-5 downgraded].
8. `GET /admin/job-logs?jobType=CanonicalRefresh` [C exact type string] -> entries present.
9. Pause the CanonicalRefresh job -> StatusPill "Paused" [V]; Resume.
10. DB: `SELECT count(*) FROM job_definitions WHERE ...mapping...;` [C] - definition row exists.
11. Rail highlights **Load** on this flow [V node exists].
12. PASS Step 5: per-mapping schedule saved in the HMI; CanonicalRefresh job visible, runnable,
    pausable; (auto-on-import verified or recorded).

## Journey Step 6 - Loaded: canonical verification through the product - 11 steps
1. Sidebar -> Intelligence -> **Material Investigation** (`/materials` [V]).
2. Search a material code imported in step 4 [C search box]; unit resolves.
3. Drill: its observations list shows the imported readings; timestamps match source.
4. Its quality events show the imported defects with catalog names (not codes only) - proves
   DefectCatalog joined.
5. Genealogy view: parent/child edges from step 4.22 render.
6. Provenance spot-check (constitution): unit detail shows source_system / lineage [C where
   displayed]; if not surfaced in UI, verify by DB:
   `SELECT source_system, source_record_id FROM material_units WHERE material_code='<code>';`
7. `SELECT source_system, count(*) FROM material_units GROUP BY source_system ORDER BY 2 DESC;`
   [GAP-6] pre-purge: phase3-dump/*_SEED present (38,346) beside postgresql (1,802). Post-M1-08:
   ONLY postgresql.
8. is_synthetic flag: imported rows have is_synthetic=false; any synthetic remainder is flagged
   true [C column presence per entity].
9. NOT-NULL coverage: no nulls in required canonical columns for the new rows (spot 2-3 columns).
10. Honest empty state: search a nonexistent code -> friendly "not found", no crash.
11. PASS Step 6: imported unit fully navigable in the HMI with observations, events, genealogy,
    provenance.

## Journey Step 7 - UI-2 Dashboards & Widgets - 12 steps
1. Sidebar -> **Command Dashboard** (`/dashboard` [V]). Rail: **Dashboards**.
2. Page renders existing dashboard; identify the add/edit-widget entry point [C label; record it].
3. Create a widget bound to canonical data via the widget-script builder (lite-SQL helpers:
   select/filter/group-by click-tools) [C builder control labels].
4. Point it at `parameter_observations` (e.g. count by parameter_code for your imported
   parameter).
5. Expect LIVE PREVIEW before commit [constitution; C preview control].
6. Commit/save the widget -> toast [C]; widget renders the real number.
7. Sample-data disclosure: if any widget uses synthetic/sample data, the disclosure badge shows
   [V badge shipped 09-Jul]; your new widget must NOT show it (real data).
8. Re-import one source row + run CanonicalRefresh -> the widget number moves on refresh (end-to-
   end liveness).
9. **Widget Drift** page (sidebar [V]): opens without error; shows drift status for widgets [C
   content].
10. Negative: author a widget with an invalid expression -> clean validation error, not a crash
    [C message].
11. Permissions sanity: widgets save under your user; reload page -> widget persists.
12. PASS Step 7: a real-data widget authored, previewed, committed, live-updating; disclosure
    badge only on sample data.

## Journey Step 8 - UI-3 Analysis Authoring (Surface-3) - 11 steps
1. Navigate to `/investigate/analysis-jobs` (AnalysisJobConfigPage). If no sidebar entry, use the
   URL directly and RECORD that as a nav gap [C].
2. Mount -> `GET /api/analysis-jobs` [V matrix row exists] -> 200 array of definitions.
3. Click create/new definition [C label]; form offers parameter(s) and outcome selection from
   canonical data [C controls].
4. Choose the rigged pair: feature = superheat parameter, outcome = CRACK_LONG events; method:
   Pearson correlation.
5. Save -> `POST /api/analysis-jobs` [V] -> 200/201 {id}; definition appears in the list.
6. Edit round-trip: reopen the definition; selections persisted.
7. Author a SECOND definition for the null control (superheat -> SCRATCH) - needed for step 10's
   honesty check.
8. [GAP-7] Method picker shows only the basic set; Spearman/chi-square/ANOVA registry is M2-04 -
   one roadmap sentence in the demo.
9. Validation: try saving with no outcome selected -> blocked with a message [C].
10. `SELECT count(*) FROM analysis_job_definitions;` [C table name] grew by 2.
11. PASS Step 8: two definitions (target + null control) authored and persisted from the HMI.

## Journey Step 9 - Analysis jobs run (readiness gate) - 10 steps
1. On the definition row, click **Run** [C label] -> governed run starts; response ties results to
   the definition via source_correlation_run_id = compute_run_id [V design].
2. Expect a run indicator then completion state on the page [C].
3. Gate honesty: with current data (parameter_observations=14,433) the readiness gate should PASS;
   if it returns **BlockedTooFewRows**, the UI must show that verbatim as an honest state, not an
   error [V gate name].
4. Run the null-control definition too.
5. DB: `SELECT id,status,window_days,started_at_utc FROM ml_correlation_compute_runs ORDER BY
   started_at_utc DESC LIMIT 3;` -> your runs, status completed.
6. `SELECT count(*) FROM ml_correlation_results_v2 WHERE compute_run_id='<id>';` -> rows exist for
   the completed run.
7. Jobs Monitor / job-logs: the analysis run family appears [V generic monitor].
8. Reaper sanity: no run stuck in 'running' older than the reaper window
   (`SELECT count(*) FROM ml_correlation_compute_runs WHERE status='running';` -> 0 after
   completion).
9. Negative: trigger a run twice quickly -> second either queues or rejects cleanly [C behavior];
   no duplicate half-runs.
10. PASS Step 9: governed runs complete (or block honestly); compute_runs + results_v2 populated;
    visible in monitor.

## Journey Step 10 - Results dashboards (honest findings) - 11 steps
1. Sidebar -> **Correlations** (`/correlations` [V]). Rail: **Findings**.
2. Findings list renders: feature -> outcome, method, effect size, q-value, sample size [C column
   headers; record exact].
3. THE acceptance: superheat -> CRACK_LONG visible with q < 0.01.
4. The SCRATCH null control shows as NOT significant - displayed as a first-class honest result,
   not hidden.
5. [GAP-8] Expect duplicate rows across historical runs (no latest-run dedup yet) and NO
   odds-ratio/population columns - M1-09 adds them. For the demo, filter/scroll to the latest run
   and state OR~9.5 from the verified rig verbally.
6. Click into the finding detail [C]: population/sample and method shown; every number traceable.
7. Cross-check the number: results_v2 row for the latest run matches the UI effect/q exactly.
8. **Data Quality** page (sidebar [V]) opens; readiness/validation findings render honestly.
9. **Risk Intelligence** page opens; risk scores render (or honest empty).
10. No fabricated status anywhere: scan the three pages for placeholder/fake numbers - all values
    must trace to canonical/results data [constitution].
11. PASS Step 10: rigged finding + null control shown with honest statistics; (post-M1-09) dedup +
    OR column re-verified.

## Journey Steps 11-13 - AI+ML tier (license-gated) - 12 steps
1. Sidebar -> **ML Readiness** (`/ml-readiness` [V]): labels/features/training gates reported
   honestly (BlockedTooFewRows-class states are legitimate displays).
2. License display: current tier from the dev Ed25519 token shows truthfully [C where displayed;
   GAP-9: production keys deferred - roadmap sentence].
3. Author/enable an ML-tier analysis via the same Surface-3 path (deeper method) [C what v1
   exposes; record].
4. Run it; expect the same governed-run machinery (compute_runs row).
5. ml_correlation_results_v2 gains rows tied to that run.
6. **Suggestions** page (sidebar [V]) opens: guarded recommendations render with evidence
   references, or an honest empty state.
7. Job telemetry: duration/rows visible in monitor/job-logs for the ML run [V job_log fields].
8. Reaper: kill the API mid-run once (optional, brave): restart; the run must be reaped to a
   terminal state, not stuck 'running' [V ComputeRunReaperHostedService exists].
9. Gate integrity: attempt an ML run on a tiny filtered population -> Blocked, shown honestly.
10. License-gate negative [C if a viewer/lower-tier account exists]: ML authoring hidden/denied
    cleanly; if no such account, record as untested.
11. Results dashboard for ML mirrors step 10's honesty contract.
12. PASS Steps 11-13: ML-tier run executes under the same gates/telemetry/honesty as Layer 1.

## Journey Step 14 - THE SUPERVISOR (M1-05 v0) - 22 steps
1. Sidebar -> **Engine Supervisor** (desc "Weekly engine review (step 14)", icon BrainCircuit
   [V]). Route `/data-integration/supervisor`.
2. Mount -> `GET /api/supervisor/reports` [V] (module `src/api/engine/supervisor.api.ts`).
3. Loading text `Loading reports...` [V] while pending.
4. First visit empty state [V]: `No supervisor reports yet. Click "Run review now" to generate the
   first one.`
5. Header shows title "Engine Supervisor" and subtitle containing "Read-only: it never changes a
   job automatically." [V].
6. Header button **Run review now** [V]; disabled with spinner while busy [V isLoading].
7. Click it -> `POST /api/supervisor/run` [V] -> 200
   `{ id, itemKey, title, body, findings, significant }` [V shape].
8. List refreshes automatically [V refresh after run].
9. Newest card title [V]: `Supervisor report <yyyy-MM-dd HH:mm> UTC`.
10. Body MUST contain [V generated text]: the latest run's window (`covered a N-day window`),
    `produced N evaluated associations, of which M were significant (q < 0.05)`.
11. Body lists up to 3 `Top associations:` lines `feat -> outcome (effect X, q Y)` [V].
12. Body ends with a Recommendation line (keep window vs widen window) [V both variants exist].
13. Constitutional honesty line present verbatim [V]: `NOTE (v0): this report is a read-only
    review. No job configuration was changed automatically; automatic tuning is a later release.`
14. Edge case: if NO completed analysis run existed, body instead says `No completed analysis run
    found yet. Run an analysis job first, then re-run the supervisor.` [V] - verify by reading,
    not by faking.
15. KB persistence: `SELECT item_key,title FROM ml_knowledge_base_items WHERE
    item_type='SUPERVISOR_REPORT' ORDER BY created_at_utc DESC LIMIT 3;` -> the new row,
    item_key `supervisor-report-<timestamp>` [V].
16. Run it a SECOND time -> a second report card (item_key differs); list ordered newest-first
    (LIMIT 20) [V].
17. Monitor row (M1-02): `GET /admin/job-logs?jobType=SUPERVISOR` [V] -> entry `Supervisor review
    completed`, severity Info, with the report as context payload [V WriteAsync args].
18. Jobs Monitor UI shows the SUPERVISOR family row(s) [V generic table].
19. Error honesty: stop the DB service briefly (optional) or use a bad state -> Run review now
    surfaces the error box [V error state renders], M1-10 shape in Network.
20. Guardrail evidence for the demo: open results_v2 counts before/after supervisor runs - IDENTICAL
    (writes only the KB report + job_log; tuning is M2-01) - say it, show it.
21. [GAP-10] No weekly schedule yet (manual trigger) - scripted roadmap sentence; the schedule row
    joins the monitor in M2-01.
22. PASS Step 14: two real reports generated from live results data; persisted; monitored; the
    no-auto-tuning claim verifiably true.

## Journey Step 15 - Assistant (grounded, cited, refusal-first) - 14 steps
1. Reindex first (M1-01): `POST /api/assistant/reindex` [V endpoint added this session] via the
   Assistant Configuration page button if present [C] else authorized API call -> 200 with chunk
   counts (families: CONNECTOR/DATASET/MAPPING/DOC viewer-scoped; FINDING engineer-scoped [V]).
2. Sidebar -> **Assistant Configuration** (`/assistant/configuration` [V]). Verify grounding
   policy dropdown shows `strict-citations-required` and evidence policy
   `citations-and-provenance-required` [V options], plus **Max citations** control [V].
3. Save config -> toast [V saveAssistantConfig]; Reset control exists [V resetAssistantConfig].
4. Sidebar -> **Assistant** (`/assistant`, desc "Grounded chat runtime" [V]). Rail: **Assistant**.
5. Chat surface with input + send [C labels]; onAsk wires to assistantApi.askAssistant -> the
   ONLY ask path [V comment in code].
6. Ask grounded Q1: `Which datasets are connected?` -> `POST /api/assistant/ask` [V] -> 200 answer
   + citations.
7. Every citation must RESOLVE: click each [C rendering]; target exists (dataset/mapping/doc).
8. Ask grounded Q2 about your imported data: `What mappings exist for <sourceObjectName>?` ->
   cited answer naming the step-4 mapping.
9. Ask finding Q3 (engineer role): `What drives CRACK_LONG?` -> answer cites the FINDING chunk
   (superheat), with q-value language, suspected-contributor phrasing (never "root cause").
10. Refusal test: `Which coils will fail next week?` -> REFUSAL (no evidence), polite, no guess
    [V refusal-first doctrine].
11. Off-corpus test: `What is the capital of France?` -> refusal/deflection to grounded scope [C
    exact behavior; record].
12. Audit: assistant writes NOTHING but its audit log - verify no canonical table counts changed
    after the chat session (spot-check one).
13. [GAP-11] Extractive baseline model; Ollama/hosted binding is M2-02 - roadmap sentence.
14. PASS Step 15: 3 grounded cited answers, citations resolve, 2 refusals, zero writes.

## UI-4 - Plant-Data-Log / Alerting (M1-06) - 24 steps
1. Sidebar -> **Plant Data Log** (desc "Threshold alerts on imported observations", icon
   AlertTriangle [V]). Route `/data-integration/alerting`. Rail: **Alerts** [V node].
2. Mount fires parallel `GET /api/alerts/rules` + `GET /api/alerts/log` [V module
   `src/api/engine/alerts.api.ts`].
3. Header title "Plant Data Log", subtitle mentions the evaluator scanning imported observations
   [V].
4. Header button **Run evaluation** [V], busy state disables it with spinner [V].
5. Rule form card fields [V]: Rule name (placeholder "Superheat high"), Parameter code
   (placeholder "SUPERHEAT_C"), Comparator dropdown EXACTLY `> >= < <= =` [V], Limit (placeholder
   "36", numeric inputMode), Severity dropdown Info/Warning/Critical [V].
6. First-visit empty states [V]: Rules -> "No rules yet. Add one above."; Log -> "No breaches
   logged yet. Create a rule and run evaluation."
7. Validation 1 [V]: empty name or parameter -> "Rule name and parameter code are required."
8. Validation 2 [V]: Limit = `abc` -> "Limit must be a number."
9. Get a REAL parameter code first:
   `SELECT parameter_code FROM parameter_definitions LIMIT 10;` - use the superheat one.
10. Create the demo rule: name `Superheat high`, that code, `>`, limit `36`, Warning. Click
    **Add rule** [V].
11. Network: `POST /api/alerts/rules` -> 200 `{ id, ruleName, parameterCode, comparator,
    limitValue, severity }` [V shape].
12. Rules table gains the row: Name / Parameter / Condition (`> 36`) / Severity [V columns].
13. Notice renders [V]: "Rule created. Click 'Run evaluation' to scan observations."
14. Server-side validation: via DevTools re-send the POST with comparator `!=` -> 400 clean error
    "comparator must be one of > >= < <= =" [V backend check], NOT a 500.
15. Click **Run evaluation** -> `POST /api/alerts/evaluate` [V] -> 200 `{ logged: N }`; N > 0 with
    the rigged high-superheat observations.
16. Notice [V]: `Evaluation complete: N new log row(s).`
17. Log table fills [V columns]: Time, Rule, Material, Parameter, Value, Condition, Severity -
    Material shows REAL material_codes joined from material_units.
18. Idempotency demo beat: click **Run evaluation** AGAIN -> `{ logged: 0 }` (unique
    (rule, observation) index; ON CONFLICT DO NOTHING) [V design]. Zero double-logging.
19. Liveness: import one new high-superheat source row (seam pattern), project it (step 5 job),
    Run evaluation -> `{ logged: 1 }`, new log row on top.
20. Create a second rule that matches NOTHING (limit 99999) -> evaluate -> logged 0; rules table
    shows both; log unchanged.
21. Monitor row (M1-02): `GET /admin/job-logs?jobType=ALERT_EVAL` [V] -> entry message
    `N breach(es) logged` [V WriteAsync args]; visible in Jobs Monitor.
22. DB truth: `SELECT count(*) FROM plant_data_log;` matches the UI row count;
    `SELECT count(*) FROM alert_rules;` = 2.
23. [GAP-12] threshold-only v0: routing-deviation + chemistry-range rules, email/webhook, ack =
    M2-06 - roadmap sentence.
24. PASS UI-4: rule authored in the HMI; evaluation logs real breaches idempotently; live seam row
    alerts; monitored; validations clean.

## Cross-check - One monitor, every job family (M1-02) - 10 steps
1. Sidebar -> **Jobs Monitor** (`/data-integration/jobs`). Columns [V]: Job, Type, Target, Status,
   Last Run, Duration, Runtime, Actions.
2. Data source [V]: `GET /admin/job-logs` -> 200 `{ entries: [...] }` from public.job_log
   (occurred_at_utc, job_type, job_name, run_id, severity, message, site_code).
3. After the walk expect job_type families >= : import family, CanonicalRefresh, analysis/ML
   family, SUPERVISOR, ALERT_EVAL.
4. API probes: `?jobType=SUPERVISOR` -> >=2 entries (you ran it twice); `?jobType=ALERT_EVAL` ->
   >=3 entries.
5. Row actions Run now / Pause / Resume work on at least one job of each runnable type [V
   handlers].
6. [V] The Two-Stage (Arch-B) monitor PANEL is gone from the jobs tab (M1-07 removed it).
7. [GAP-13] The Importing tab still shows the two dead Arch-B placeholder panels - known,
   deferred; do not screen-share them closely.
8. Severity rendering: force one failed run (dead source) -> its entry shows Error severity and a
   clean message.
9. site_code populated consistently on entries [C value].
10. PASS: one monitor, all families, no Arch-B panel in jobs tab.

---

# GAP REGISTER v2
| # | Gap | Severity for 16-Jul | Disposition |
|---|-----|--------------------|-------------|
| GAP-1 | dataset 500-after-persist (historical) | Medium | fix if reproduced on walk |
| GAP-2 | Importing nav desc Arch-B wording | FIXED (pack shipped) | verify label on walk |
| GAP-3 | four connector cursor defects (42883 / null-WHERE / locale / dataset-500) | HIGH - can block step 3 | reproduce on walk steps 3.7-3.8; fix before rehearsal |
| GAP-4 | M1-03b taxonomy re-import not yet executed; PPIQ_CONFIG rows may remain | High (Rule 2) | executed as part of walk step 4.6-4.17 + M1-08 re-import |
| GAP-5 | auto-projection on import completion unverified (schedule UI EXISTS - step 5.1-5.4 [V]) | Low-Med (downgraded in v2) | verify walk 5.7; else schedule/manual |
| GAP-6 | demo-seed data in canonical (38,346 units) | CRITICAL honesty | M1-08 purge; snapshot verified |
| GAP-7 | Pearson-only method picker | Medium | M2-04; roadmap sentence |
| GAP-8 | findings dedup + odds_ratio/population missing | High for deck | M1-09 after M1-08 |
| GAP-9 | dev license keys; tier matrix not fully enforced | Low | deferred; truthful display |
| GAP-10 | supervisor: no weekly schedule; no auto-tuning (by design v0) | Low | M2-01; scripted sentence |
| GAP-11 | assistant extractive baseline | Low | M2-02; roadmap sentence |
| GAP-12 | alerting threshold-only; no delivery/ack | Low | M2-06; roadmap sentence |
| GAP-13 | Arch-B residue: TWO dead placeholder panels on Importing tab ("Two-Stage Import Model" + "Current import metrics") + TwoStageImportModel type threaded through 8 files (admin.api.ts, admin-mapping-types.ts, types.ts, manifest.json, productCoreApiClient.runtime.ts, AdminImportingDataTab, AdminSharedComponents, DataIntegrationLayout) | Medium if that tab screen-shared | M1-07b post-walk surgical pass; panels are benign placeholders (Promise.allSettled -> null) |
| GAP-14 | M1-10 error shape not yet exercised with ASPNETCORE_ENVIRONMENT=Production | Low | preflight P6 covers dev; one Production 500 test before rehearsal |
| GAP-15 | remaining [C] labels (registry buttons, prep form, widget builder, analysis run, assistant chat, citations) | n/a | first walk fills them; bump doc to v2.1 = committed M1-11 |
| GAP-16 | throttling controls (row caps/rate/windows) possibly not exposed in the connection form UI | Medium (constitution names them) | check walk 1.17; backend supports throttled connectors |

**Execution order:** walk this doc once on FULL data (fills [C], smokes GAP-1/3/5/16) -> fix what
fired -> M1-08 purge + taxonomy-first re-import -> re-walk end-to-end on imported-only data
(= M1-14 baseline) -> M1-09 -> re-verify step 10 -> two timed dress runs.

*v2.0. On first execution correct every [C] inline and bump to v2.1 - commit under docs/ as the
M1-11 checklist.*
