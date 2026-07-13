# PlantProcess IQ — COMPLETE SESSION HANDOVER
### 10 July 2026 (evening) · "Backlog v21 + the Architecture-A discovery + the connector cursor cures"

**Author:** Claude (AI assistant), end of session
**For:** Karim Elsayed, solo founder/dev, SOU Industrial Software (Düsseldorf)
**Predecessors (inherited, not re-verified):** `PPIQ_SESSION_HANDOVER_10Jul2026.md` (morning, "M1 execution + the assistant excavation") and `PPIQ_Deploy_Pipeline_Handover.md` (26-Jun, deployment). This document supersedes neither; it adds the afternoon's discoveries and the v21 replan on top of them.

---

## 0. HOW TO USE THIS DOCUMENT

A new session must start **where we ended**, not on green field. Every diagnostic below was actually executed and its output recorded. **Re-running costs money and tells you nothing new.**

**The three sentences that matter most:**

1. **In this codebase, an artefact existing — even with passing tests — implies nothing about it being reachable, registered, or executed. Grep for the *registration*, never for the class.** (Now proven SIX times; §3.)
2. **The generic remote import engine already exists and is wired to the wrong half.** Journey step 3 is ~90% built in C# (Architecture A), while the HMI drives a same-database SQL copy (Architecture B). This is the single most important technical fact in the project. (§4.)
3. **A guard that can be satisfied by its own prose or output is not a guard.** Falsify every gate once — see it RED — before trusting it. (§3, §9.)

**Karim's three rules and the 15-step journey are now permanent memory** (saved this session via the memory tool). They are scripture. By END OF M2 the product must satisfy them 100%. They are reproduced in §12.

---

## 1. WHAT THIS SESSION DELIVERED

| # | Work | Status | File |
|---|---|---|---|
| 1 | **Saved the 3 rules + 15-step journey to permanent memory** | done, persists across all future chats | memory entries #20 (rules), #27 (journey) |
| 2 | **Backlog v21** — 147 tasks, 3 sheets, Dark-Industrial, formula-driven | delivered | `PPIQ_Product_Backlog_v21.xlsx` (outputs) |
| 3 | **Roadmap v8** — journey-anchored, demo dated Thu 16-Jul, M2 dated 28-Aug | delivered | `PPIQ_Product_Roadmap_v8.md` (outputs) |
| 4 | **M1-01 live experiment** — proved Architecture A reaches the remote sources | RUN, evidence captured | §5 |
| 5 | **Apply-ScheduleBoardOrderingFix.ps1** — schedule-board 500 (EF constructor-projection OrderBy) | delivered, NOT yet run by Karim | outputs |
| 6 | **Apply-ConnectorCursorFamilyFix.ps1 (v2)** — 4 connector/DTO defects, generic | delivered v2 (v1 refused by own preflight — see §6.4), NOT yet run | outputs |
| 7 | **Apply-AssistantServiceGraphRegistration.ps1** (from morning) | built, NOT yet run | outputs (morning session) |

**NOT done, deliberately:** every build task in M1 P2/P3. This session was replan + the M1-01 verification experiment + the connector cures that experiment surfaced. No projector, no chunk producer, no i18n. Those are the next session's work.

**Key realisation that reshaped everything:** the v20 plan (128h, 30 tasks, "build a generic reader / FDW unification") was written against a wrong mental model. Architecture A already IS the reader. The real M1 is *verify + rewire + one projector*, not *build the pipeline*. v21 reflects this.

---

## 2. ENVIRONMENT TRUTHS (carried from the morning handover — trust this section over any doc)

### 2.1 The local application DB is NOT a container
```
ppiq_app = NATIVE Windows service postgresql-x64-16 on port 5432
```
- Container `ppiq-app-db` has been `Exited(255)` for weeks while everything worked. Red herring. Ignore it.
- `docker ps` shows only the **six source emulators**: `ppiq-src-meltshop-postgres`, `ppiq-src-caster-oracle`, `ppiq-src-parsytec-mysql`, `ppiq-src-downtime-mysql`, `ppiq-src-hsm-oracle`, `ppiq-src-pkl-mssql` (+ `-init`).

### 2.2 Credentials (`env/profiles/local.env`)
```
POSTGRES_HOST=127.0.0.1   POSTGRES_PORT=5432
POSTGRES_DB=ppiq_app      POSTGRES_USER=ppiq_dev
POSTGRES_PASSWORD=ppiq_dev_local_only
PPIQ_SMOKE_USERNAME=e2eadmin   PPIQ_SMOKE_PASSWORD=E2EAdmin123!
```
**DB user is `ppiq_dev`, NOT `ppiq`.** API on `http://localhost:5063`. Login `e2eadmin / E2EAdmin123!` (confirmed live this session).

### 2.3 Use `127.0.0.1`, never `localhost`
`psql -h localhost` → `::1` (IPv6) → fails `pg_hba`. `PGPASSWORD` is lost per new PowerShell window; set it again.

### 2.4 `ppiq.ps1 up` lies about readiness
Launches `dotnet run` in a separate window, returns immediately, prints the URL ~8s before it listens (plus ~36s build). **Always run `cd Backend\PlantProcess.Api ; dotnet run` in the FOREGROUND before declaring the API broken.** Backlog M1-11 fixes it with a readiness poll.

### 2.5 `ppiq.ps1 down` removes the source containers
Restart with `ppiq.ps1 up-sources`. This session Karim ran `docker start ppiq-src-*` directly — that works too.

### 2.6 RBAC matrix is deny-by-default, longest-prefix, read once at startup
`Backend/PlantProcess.Api/Security/PlantAccessControl.cs`, static `Matrix`. Unmapped POST → 403 naming the matrix; unmapped GET → falls through to anonymous. `("/admin", All(), "tenant.admin", false)` covers `/admin/*` by prefix — a new admin endpoint needs no new matrix line. **If you see a 403 you don't understand, read the response body.** Permissions (don't invent new ones): `anonymous, license.admin, tenant.admin, source.configure, job.manage, analysis.execute, page.design, assistant.use, report.export`.

### 2.7 PowerShell / packs
PS 5.1. Pure ASCII, UTF-8-no-BOM via `[System.IO.File]::WriteAllText` with `UTF8Encoding($false)` (or preserve existing BOM state), CRLF for PS/CS, no `&&`, cuddled `} else {`. `Unblock-File .\X.ps1` first (execution policy). **Uploads frequently arrive EMPTY — paste console output as text, or copy to a Desktop `.txt` first.** (Happened again this session.)

---

## 3. THE WORKING METHOD (apply-pack contract — keep it, it works)

Every code change is a **PowerShell apply pack** run by Karim:
1. **Preflight** — verify every anchor exists AND is unique. One miss → zero changes, exit 1.
2. **Backup** — copy each target to `deploy\.ppiq-backups\<pack>-<stamp>\`.
3. **Apply** — anchored string/regex replacement, never blind whole-file regex.
4. **Self-check** — assert the intended change happened and the old text is gone.
5. **Gate** — `dotnet build` and/or `tsc -b` and/or `vitest run src/test/architecture/`.
6. **Auto-revert** ALL files on any gate failure.

**"Tested is not wired" — now SIX instances** (was four in the morning; two more found this session):
1. `AddAssistant()` defined, never called.
2. `src/phase11` — four tested modules imported by nothing.
3. `GroundedAssistantPage` — tested page routed by nothing (deleted 09-Jul).
4. `AssistantRetrievalIndexBuildService` — in no container.
5. **NEW: Architecture A** (the entire remote import engine) — registered + scheduled + endpoint-mapped, but the HMI drives Architecture B instead.
6. **NEW: the schedule-board endpoint** — mapped and ungated, but 500s on every call (never returned 200 in its life; §5, §6.1).

**"Guard matches its own prose" — the CI truth-gate lesson.** The morning found `CiPipelineTruthGateTests` / `DeployRedPathProofTests` did raw `IndexOf` over the Jenkinsfile; the header comment `dotnet test (BLOCK) -> npm run test (BLOCK) -> e2e (BLOCK)` satisfied every assertion, and `Assert.Contains("rollback")` was satisfied by the failure-echo "no rollback was needed". You could delete stages 3/4/5 and stay green. The fix (a `StripComments()` helper + removing the e2e `when{}` gate) was designed but I must confirm whether it was applied — see §9. **Falsify every new gate once.**

---

## 4. THE ARCHITECTURE-A DISCOVERY (the central technical fact)

There are **two complete, parallel, incompatible import architectures.** The HMI is wired to the wrong one.

| | **Architecture A** (C#, correct) | **Architecture B** (SQL, demo-bound) |
|---|---|---|
| Entry | `ConnectionProfile` → `SourceDatasetDefinition` | `source_table_dump_registry` |
| Reads from | **the customer's remote database** | local `src_*` schemas, same DB |
| How | `IDataSourceConnectorFactory.GetDataSourceReader(providerType)` → `ThrottlingDataSourceReader` → connector `.ReadRowsSinceKeyAsync` | `ppiq_run_stage1_delta_import` = `INSERT...SELECT` |
| Cursor | `IncrementalCursorField` / `LastCursorValue` / `MaxCursor()` | `last_index_column`, strict `>` (loses ties) |
| Protection | **row cap + rate limit + approved window**, `SourceLoadRejectedException` | none |
| Writes to | `ImportBatch` + `StagingRecord(rawJson jsonb)` | `dump_store.<schema>__<table>` |
| Scheduling | `NextRunAtUtc`, `ScheduleNextRunAfterSuccess/Failure`, `Workers/Worker.cs` `SYSTEM_DELTA_IMPORT_JOB`, enabled by default | manual button |
| Providers | **all six** (Oracle/MySQL/MSSQL/PG/CSV/Excel) implement `IDataSourceReader` | one: local PG |
| Reaches canonical | **NO — nothing consumes `StagingRecords`** | yes, via the hardcoded IF/ELSIF ladder |
| Wired to HMI | **NO** | yes |

**Key files (all read this session, in `01_Backend_Core` dump):**
- `Backend\PlantProcess.Application\Integration\Services\SourceSystems\DeltaImportExecutionService.cs` — the orchestrator. Registered in `Application/DependencyInjection.cs`, called by admin endpoint `RunDueSourceImportsAsync`, driven by `Workers/Worker.cs`. Line 132 = `ExecuteSingleDatasetAsync` calls the connector.
- `Backend\PlantProcess.Infrastructure\Connectors\{PostgreSql,MySql,SqlServer,Oracle}\*Connector.cs` — all implement `IDataSourceReader.ReadRowsSinceKeyAsync`.
- `Backend\PlantProcess.Infrastructure\Connectors\Common\DataSourceConnectorFactory.cs` — wraps every reader in `ThrottlingDataSourceReader`.
- Endpoints mapped **ungated in Production** at `Program.cs:1034` via `MapPhase1WorkflowTruthEndpoints`:
  - `POST /admin/workflow-foundation/run-due-source-imports`
  - `GET  /admin/workflow-foundation/source-schedule-board`
  - `POST /admin/workflow-foundation/source-datasets/{id}/schedule-now`
  - `GET  /admin/workflow-foundation/staging/summary` and `/staging/records`
  - `GET  /admin/workflow-foundation/schema-mapping/workbench`, `POST .../preview-view`
  - `POST /admin/workflow-foundation/import-jobs/from-mapping` ← **journey step 5, literally**
- Dataset CRUD is the PRODUCT path: `POST /admin/connectors/datasets` (`ConnectorAdminEndpoints.cs` → `ConnectorConfigurationService.Datasets.013.CreateDatasetAsync.cs`). Request fields: `connectionProfileId, datasetCode, datasetName, datasetKind, sourceSchemaName, sourceObjectName, primaryTimestampField, incrementalCursorField, isSynthetic`.

**The ONE real gap:** Architecture A ends at `StagingRecords` (one jsonb `rawJson` per source row). Architecture B's ladder begins at `dump_store`. **Nothing bridges A → canonical.** The real M1 keystone (v21 M1-06) is a **generic projector: `StagingRecords` + saved mapping → canonical.** jsonb→columns via a saved mapping is *easier* than projecting a relational dump table. Once it exists, all of Architecture B becomes dead code deleted in M2 (v21 M2-02), and Rule 1 becomes true at the migration layer as a consequence.

**Why FDW was rejected** (the morning handover proposed "FDW unification = M2 keystone"): wrong tool — no CSV/Excel support, needs an extension per source type, puts customer credentials inside our Postgres. Architecture A already does the job correctly for all six families.

---

## 5. M1-01 — EVERY TEST RUN AND ITS RESULT (do not re-run)

Karim ran the experiment live on 10-Jul ~14:13. Full outputs are in the chat; here is what they proved.

### 5.1 Run 1 — before any dataset defined
- `GET source-schedule-board` → **500** (bug, §6.1).
- `GET staging/summary` → seed debris only: `DEMO-READY-BATCH-01` (2 pending records) + six `SyntheticSeed` batches dated 2026-01-01 (`ADV_MES_MATERIALS`, `ADV_L2_STEPS`, `ADV_HIST_PARAMETERS` (500 rows), `ADV_QMS_QUALITY`, `ADV_LAB_CHEMISTRY` **status "Running" since 01-Jan — a zombie ImportBatch nothing closes**, `ADV_CMMS_DOWNTIME` status Failed "intentional").
- `POST run-due-source-imports {25, 5000}` → **200, 149ms, datasetsProcessed:0**. Architecture A alive, authenticated, executing — it just had no due datasets. **Outcome (b): configuration, not a broken engine.**

### 5.2 Run 2 — Meltshop dataset created + import attempted
- `GET connection-profiles` → Meltshop = `dddd0000-0000-0000-0000-000000000201`, code `DEMO-READY-CP-01`, providerType `postgresql`.
- `GET .../tables` → **8 real remote tables**: `meltshop_heats, ms_additives, ms_components, ms_eaf_step_params, ms_eaf_steps, ms_equipment_counters, ms_sample_results, ms_samples`.
- `GET .../tables/public/meltshop_heats/columns` → 20 columns. Key candidates: `heat_id, crew_id, ladle_id`. **Timestamp candidates: `tap_start_utc, tap_end_utc, lf_start_utc, lf_end_utc`** (all `datetime`, `isTimestampCandidate=True`). Live discovery over the wire works perfectly.
- `POST /admin/connectors/datasets` (MELTSHOP_HEATS, cursor `tap_start_utc`) → **500 BUT THE ENTITY PERSISTED** (dataset `a02db0b6-06d8-4a7d-9df6-64c3e793db14` was processed by the next run). D4 bug, §6.2.
- `POST run-due-source-imports` → **200, datasetsProcessed:1, datasetsFailedCount:1, rowsImported:0**, error: `42883: operator does not exist: timestamp with time zone > text, POSITION: 63`. **The connector reached the remote Postgres and the remote server parsed its query.** D1/D2 bug, §6.3.
- Second `run-due` → datasetsProcessed:0 (`ScheduleNextRunAfterFailure` pushed `NextRunAtUtc` out — scheduler correct).

### 5.3 The console stack traces (captured — decisive)
- **Schedule-board & dataset-create 500** both: `System.InvalidOperationException: The LINQ expression '...Where(ti => new SourceDatasetDefinitionDto(...).Id == __entity_Id_0)' could not be translated.` — EF cannot evaluate a predicate/orderby on a **constructor-projected record**. Surfaced via `TenantContextAccessor.cs:56` → `AccessControlMiddleware:303` (i.e. auth passed; the 500 is downstream).
- **Import 42883** stack: `PostgreSqlConnector.ExecuteReadAsync:226` ← `ReadRowsSinceKeyAsync:202` ← `DeltaImportExecutionService.ExecuteSingleDatasetAsync:132`. PG Hint: "You might need to add explicit type casts." Position 63 = the `>` in `WHERE "tap_start_utc" > @lastCursor`.

### 5.4 Live facts from the morning session (still true, do not re-query)
```
ppiq_normalize_business_key('coil','C-0044170') = 44170   (works, first time ever)
material_units: 38,345 rows, plant_time_zone_id='Europe/Berlin', plant_utc_offset_minutes=60 (unanimous)
canonical genealogy: 35,906 edges / 38,346 materials; ppiq_validate_genealogy_graph() = 865 rows
C-0044170: HeatToCoil 0.70 + 0.30 = 1.0 (transition coil, NO slab edge)
canon.assistant_chunk: 0 rows (exists, never held a row)
ppiq_mapping_versions: Published 37, RolledBack 31
material_units NOT NULL w/o default: id, material_code, material_unit_type, site_id,
  plant_time_zone_id, plant_utc_offset_minutes, created_at_utc, is_synthetic, is_deleted
```

---

## 6. THE FOUR CONNECTOR/DTO DEFECTS — DIAGNOSED FROM SOURCE, CURES DELIVERED

All four are cured in **`Apply-ConnectorCursorFamilyFix.ps1` (v2)**, generic across providers, in one pack. NOT yet run by Karim.

### 6.1 Schedule-board 500 (separate pack: `Apply-ScheduleBoardOrderingFix.ps1`)
`Phase1WorkflowTruthEndpoints.Handlers.004.GetSourceScheduleBoardAsync.cs` projects into the positional record `SourceScheduleRow` (23 fields) THEN does `.OrderBy(x => x.NextRunAtUtc ?? DateTime.MinValue).ThenBy(...).ToListAsync()` — ordering on projected members, untranslatable, **data-independent → 500 on every call, always has.** Fix: move the `orderby` into the query comprehension over entity columns (`dataset.NextRunAtUtc`, `profile.ProviderType`, `dataset.DatasetCode`) BEFORE the `select`. Pass = 200 with ≥0 rows.

### 6.2 D4 — dataset-create 500 (in the cursor pack)
`CreateDatasetAsync` persists the entity, then reads back via `GetDatasetDtoQuery().FirstAsync(x => x.Id == entity.Id)`. `GetDatasetDtoQuery` (file `...Datasets.018.GetDatasetDtoQuery.cs`) is a constructor projection into `SourceDatasetDefinitionDto`; the `.Id ==` predicate is on a projected member → untranslatable. **In-repo correct reference: `GetDatasetsAsync` (012) filters pre-projection.** Fix: `GetDatasetDtoQuery(Guid? datasetId = null)` with `&& (datasetId == null || dataset.Id == datasetId.Value)` in the `where`; callsite `GetDatasetDtoQuery(entity.Id).FirstAsync(ct)`.

### 6.3 D1/D2/D3 — the connector cursor family
- **D1 (null-branch):** PG/MySQL/MSSQL build `WHERE cursor > @lastCursor` **unconditionally**, even on the first run when `LastCursorValue` is null (sent as `?? ""`). **OracleConnector is the correct reference** — it branches on `string.IsNullOrWhiteSpace(request.LastCursorValue)` and omits the WHERE on the first run. Fix: all three adopt the null-branch (no WHERE first run).
- **D2 (typed param, PG only):** on subsequent runs Npgsql sends the cursor string as text; PG won't coerce text vs `timestamptz`. Fix: `command.Parameters.Add(new NpgsqlParameter("lastCursor", value) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Unknown })` — the server infers the column type and coerces. MySQL/MSSQL coerce by type precedence, so `AddWithValue` stays (guarded by the null-branch).
- **D3 (ISO serialization):** `ExecuteReadAsync` stores every value via `Convert.ToString(value, InvariantCulture)`. A `DateTime` becomes `"07/10/2026 12:13:03"` (month-first). That string becomes `LastCursorValue`, and `DeltaImportExecutionService.MaxCursor` re-parses it with `DateTime.TryParse` under **CurrentCulture** — on a de-DE machine that's 7 October. Cursor ordering silently wrong across months, per machine locale. Fix: a `FormatSourceValue` helper serializing `DateTime`/`DateTimeOffset` as ISO-8601 round-trip (`"o"`).
  - **FOLLOW-UP FILED, NOT FIXED:** OracleConnector's value path (`values[reader.GetName(i)] = value;`) was not in the reviewed slice — verify its ISO behaviour before Oracle's second-run demo.

### 6.4 Why v1 of the cursor pack was refused (instructive)
v1's PG anchor `var sql = $"SELECT * FROM ... LIMIT @limit;"` matched **twice** — `ReadRowsAsync` (the full-read path) has the same shape as `ReadRowsSinceKeyAsync`. Preflight counted 2, refused, changed nothing. **The guard worked; my anchor was wrong.** v2 anchors on the `WHERE ... > @lastCursor` fragment (unique per file); all 12 anchors verified ×1 against the dump before shipping. **Lesson reinforced: verify anchor uniqueness against the source dump before delivering a pack.**

### 6.5 Exact run order for the next session
1. `Apply-ConnectorCursorFamilyFix.ps1 -WhatIf` → apply (auto-reverts 5 files if build breaks).
2. `Apply-ScheduleBoardOrderingFix.ps1 -WhatIf` → apply.
3. Restart API. Then:
   - `POST source-datasets/a02db0b6-06d8-4a7d-9df6-64c3e793db14/schedule-now` then `run-due-source-imports` → **expect rowsImported ≈ 1797, errorMessage null.**
   - `schedule-now` + `run-due` again → **expect rowsImported 0** (cursor at max `tap_start_utc`) = **incremental proof = journey step 3 PROVEN over the wire.**
   - `GET source-schedule-board` → expect 200 with the MELTSHOP_HEATS row.
   - `psql: SELECT last_cursor_value FROM source_dataset_definitions;` → expect an ISO-8601 string (`2026-...T...Z`).
4. Repeat dataset-create for one Oracle + one MySQL table to prove zero code branches (same three calls, different profile id). **Watch for the Oracle schema-resolution 400 that bit discovery on 08-Jul** — if it appears, capture verbatim, file as a numbered task.

When the +0 lands, **M1-01 closes** and M1-05's scope shrinks (its backend is proven working; only the frontend Register-step repoint remains).

---

## 7. CURRENT IMPLEMENTATION STATE vs THE JOURNEY (what's built, what's missing)

| Journey step | Built? | Where / gap |
|---|---|---|
| 1. DB-link config | **yes** | connection profiles, test-connect, masked creds, live discovery (proven §5.2) |
| 2. DB-link → job | **yes** | `SYSTEM_DELTA_IMPORT_JOB`, schedule board (once 500 fixed) |
| 3. Incremental import → staging | **yes, once cursor pack applied** | Architecture A; proven to reach the wire; cures in §6 |
| 4. 1st no-code UI (prepare/map) | **partial** | `SourceImportPrepPage` does discovery+register; Register calls the WRONG path (`ppiq_register_dump_source`, validates local schema, RAISES on remote table). v21 M1-05 repoints to `POST /admin/connectors/datasets`. |
| 5. Prep → loading job | **exists** | `POST import-jobs/from-mapping`; per-view jobs = M2 (v21 M2-04) |
| 6. Loaded into plant schema | **the gap** | **no projector `StagingRecords`→canonical.** v21 M1-06 (16h keystone). |
| 7. 2nd no-code UI (dashboards) | **exists** | widget/page builder present |
| 8–10. 3rd no-code UI (analysis) | **exists, blocked** | Surface-3 shipped 08-Jul (13/13 accept); blocked by empty `parameter_observations` → `ReadinessGate` returns `BlockedTooFewRows` (honest, working). v21 M1-07 (data pack) + M1-08 (verify). |
| 11–13. ML/AI tier | **partial** | present, license-gated |
| 14. **THE SUPERVISOR** (weekly meta-job re-tuning all engine jobs) | **DOES NOT EXIST anywhere** | not in code, not in any backlog version before v21. **v21 M2-01, the M2 flagship, 24h.** Thursday's ONLY "next release" line. |
| 15. Chatbot from engine | **cannot answer** | `AddAssistant()` never called (pack ready); `canon.assistant_chunk`=0; chunk producer never written. v21 M1-03 (register) + M1-09 (producer+reindex). |

---

## 8. IDENTITY & TOPOLOGY — CORRECTIONS OWED (doc still due v5; deferred to M2 as non-blocking)

From the morning session, still outstanding:
1. `ppiq_app` is a native Windows service (`postgresql-x64-16`), not a container. `ppiq-app-db` Exited(255) for weeks.
2. DB creds are `ppiq_dev / ppiq_dev_local_only`; use `127.0.0.1`.
3. `genealogy_edges` (weighted provenance ledger, trigger `sum=1.0` per child across ALL relationship types) vs `canonical_genealogy_edges` (structural graph, unweighted). `ppiq_golden_thread`/`ppiq_walk_genealogy` read ONLY the canonical layer.
4. `("/api/assistant", All(), "assistant.use", false)` in the Matrix.
5. `migrate`'s `ON_ERROR_STOP` stops the FILE, not the RUN — a broken migration is invisible unless you look.
6. `PPIQ-DEMO-023` is the id of two different `DeploymentChecklistRow`s in `Phase2PilotReadinessEndpoints.cs` (copy-paste bug).

**Add from this session:**
7. **Architecture A vs B** (§4) — the single biggest topology fact. Document both, mark B for deletion.
8. **The six `IDataSourceReader` connectors** and the `DeltaImportExecutionService` → `StagingRecord` → (missing projector) → canonical path.
9. Meltshop connection profile is `dddd0000-...-201` / `DEMO-READY-CP-01`. The demo `src_*` schemas installed by `scripts/110`+`111` are the Rule-1 violation to delete (M2-02).

**Roadmap trajectory this session:** started from Roadmap v7 (dead — its M1 date 08-Jul passed, its M2 date 23-Jul was being used as the demo date). Produced **Roadmap v8**: demo Thu 16-Jul with 14/15 journey steps live + step 14 framed; M2 = rules+journey 100% by 28-Aug; M3 15-Oct; M4 contract-shaped. Backlog went v20 (30 M1 tasks/128h) → **v21 (13 M1 tasks/53h)** by re-centring on verify+rewire+projector instead of build-the-pipeline.

---

## 9. DEPLOYMENT / SERVER / PIPELINE — INHERITED, PARTIALLY RE-READ THIS SESSION

> No deployment work was done this session. The morning session did none either. Facts below are from the 26-Jun `PPIQ_Deploy_Pipeline_Handover.md` PLUS a fresh read of the actual `Jenkinsfile` (184 lines, SHA `F92353D9...`, from the 10-Jul dump). Treat runtime state as **last-known-good (26-Jun), not current.**

### 9.1 Last-known green state (26-Jun)
- Jenkins job `plantprocessiq-deploy` → `PIPELINE GREEN / Finished: SUCCESS` on build **#96** (commit `94b8fb4f`).
- UI live at `https://app.178.105.152.180.sslip.io`, sysadmin auto-login working.
- Health gate: internal `GET http://plantprocess-api:5063/health` → 200 → `== DEPLOY GREEN ==`.
- GitHub webhook: `https://jenkins.178.105.152.180.sslip.io/github-webhook/`.

### 9.2 The actual Jenkinsfile (read this session — 9 stages)
```
1. Checkout (preserve server secrets: .env + Caddyfile → /var/lib/ppiq-preserve, restore after checkout)
2. Sweep stale processes & workspace locks
3. Backend tests — BLOCKING  (dotnet test Backend, inside SDK image via --volumes-from, on ci-test-db network)
4. Frontend unit tests — BLOCKING  (npm ci; npm run test, inside node image)
5. Frontend e2e  ← when{ PPIQ_RUN_E2E == 'on' }  (GATED OFF BY DEFAULT — see §9.3)
6. App DB: EF migrate → post-EF SQL → seed  (migrate-and-seed.sh --app-only)
7. Demo sources: migrate + seed  ← when{ PPIQ_DEMO_SOURCES_MODE != 'disabled' }
8. Build + recreate canonical stack  (deploy-canonical.sh — health gate + rollback to :previous)
9. Presentation defaults  ← when{ PPIQ_PRESENTATION == 'on' }  (Enterprise token + admin smoke)
post: failure → "nothing shipped, no rollback needed"; success → "GREEN"
```
env: `COMPOSE_PROJECT='ppiq-app'`, base/server/sources compose files, `ENV_FILE=deploy/compose/.env`, `PRESERVE_DIR=/var/lib/ppiq-preserve`.

### 9.3 The CI truth-gate problem (the morning's headline pipeline finding)
`CiPipelineTruthGateTests.cs` + `DeployRedPathProofTests.cs` (in `Backend/tests/PlantProcess.Architecture.Tests`) parse the Jenkinsfile with raw `IndexOf`. Because the **header comment** contains `dotnet test (BLOCK) -> npm run test (BLOCK) -> e2e (BLOCK)`, `Pipeline_contains_every_blocking_suite` and `Tests_run_before_migrate_seed_and_deploy` pass **on the comment** — you could delete stages 3/4/5 and stay green. `PPIQ_101_Deploy_uses_remove_orphans_and_rolls_back` asserts `Contains("rollback")`, satisfied by the stage-8 comment and the failure-echo. **And stage 5 e2e is gated off by `when{PPIQ_RUN_E2E}`, so e2e never runs, while the header claims `(BLOCK)`.**

**The designed fix** (from the morning; MUST confirm whether it was applied and committed): a `PipelineSourceText.StripComments()` helper both suites call before asserting; new tests `E2e_stage_cannot_be_gated_off` and `Stage_tokens_exist_outside_comments`; the rollback assertion moved to `deploy-canonical.sh` behavioural tokens (`:previous`, `docker tag`, `HEALTH GATE FAILED`); and **removing stage 5's `when{PPIQ_RUN_E2E}` so e2e EXECUTES and BLOCKS every deploy**. A falsification harness (`Test-CiTruthGateFalsification.ps1`) proved mutations A/B/C all go RED.
**CONSEQUENCE FOR THE NEXT SESSION: if that pack was applied, e2e now blocks every deploy — do NOT `git push` until the e2e realignment (v21 M2 carryover of old M1-25) lands, or expect a correctly-red pipeline.** First action: `git status` + `git log` to see what's committed.

### 9.4 Two Docker Compose projects — NEVER MERGE
- `plantprocessiq` = INFRASTRUCTURE (sacred): `ppiq-jenkins`, `ppiq-caddy` (binds `0.0.0.0:80/443`), backup-runner.
- `ppiq-app` = APPLICATION deploy: `plantprocess-postgres` (volume `ppiq-app_plantprocess-postgres-data`), api, web; network `ppiq-app_plantprocess-private`; api+web also joined to `ppiq-edge` → `plantprocessiq_ppiq-net` so infra Caddy reaches them.
- **Why:** when the app deploy used project name `plantprocessiq`, `--remove-orphans` reaped Jenkins/Caddy. The rename fixed it.

### 9.5 Known deployment tech debt (26-Jun, unverified since)
- Live `ppiq-caddy` Caddyfile routes `app.*` → **`plantprocess-app-web`** and `website.*` → **`plantprocess-website`** — **stale container names** (real: `plantprocess-web`). The Caddyfile is a read-only bind-mount whose host source doesn't exist on disk. Runtime network-alias workaround was applied; permanent fix pending. **Re-verify these routes before trusting any URL.** (The committed `deploy/caddy/Caddyfile` is fully env-var driven and clean — `PPIQ_API_UPSTREAM:plantprocess-api:5063`, `PPIQ_APP_UPSTREAM:plantprocess-web:80` — the drift is in the LIVE mounted file only.)
- `https://api.*.sslip.io/health` → 401 externally while internal is 200. Expected, not a bug.

### 9.6 The rule that must never be violated
**Do not delete `/var/lib/ppiq-preserve/.env`.** The generator reuses it to keep the Postgres password stable. Deleting it forces a new password that won't match the existing volume → `28P01 password authentication failed`. If you must regenerate `.env`, wipe `ppiq-app_plantprocess-postgres-data` in the same operation.

### 9.7 Jenkins agent (DooD)
Jenkins runs INSIDE `ppiq-jenkins`. The agent has **no dotnet, no node, no npm** — it runs toolchains as sibling containers: `docker run --rm --volumes-from $(cat /etc/hostname) -w "${PWD}" <image> sh -lc "..."`. Bind-mount sources resolve on the HOST daemon.

### 9.8 Sysadmin credentials
`sysadmin` password generated per fresh `.env`, stored in `/var/lib/ppiq-preserve/FIRST_LOGIN.txt` inside `ppiq-jenkins` and as `PPIQ_SMOKE_PASSWORD` in `deploy/compose/.env`.

### 9.9 What this session's work implies for the pipeline (untested on server)
- The connector cursor pack + schedule-board pack change backend `.cs` only → a normal redeploy picks them up. No migration.
- If the CI-truth-gate pack was applied and e2e now blocks, the server pipeline will go red on push until e2e specs are realigned to the new routes (`/data-integration/*`, `/assistant`, `/advisory/*`; removed `/license`).

---

## 10. WHAT WAS DONE TO MAKE THE PIPELINE GREEN / THE APP URL WORK

**Nothing this session, nothing the morning session.** The green pipeline was achieved 26-Jun and is documented in `PPIQ_Deploy_Pipeline_Handover.md`. Its five levers: (a) separating the two compose projects so the deploy stopped reaping infra; (b) baking `VITE_*` vars into the frontend **build** not runtime; (c) correcting the API base-URL derivation; (d) allowing the sslip host in CORS; (e) gating the Ed25519 dev license key behind `PPIQ_PRESENTATION=on`.
**Before touching the pipeline, read that document and re-verify §9.5's stale Caddy routes.**

---

## 11. PPIQ REALIZATION SCOREBOARD — HONEST STATE

Scoring instrument: `PPIQ_Aspects_of_Review_v4.md` — six personas, **headline = the LOWEST persona, never averaged.** Bands: <55 crit / 55-69 needs-work / 70-84 solid / 85+ strong. Doctrine v7 gate: baseline ≈50-52, ceiling ≈84. Headline persona is **A3 (Process/Quality Engineer)**, gated on the value engine + live HMI signal.

**Standing rule that governs every score (Aspects C4.1): NO score without a live demonstration through the HMI. A claim in a doc/comment/conversation is worth ZERO.** This is why v21 M1-02 (browser verification of the nine 09-Jul surfaces) must run FIRST — until it does, all seven 09-Jul packs score zero.

| Area | State at end of this session | Note |
|---|---|---|
| Journey step 3 (remote import) | **proven reachable, cure pending** | 4 defects found+cured this session; +0 incremental run will close it |
| Journey step 6 (projector) | **not built** | the real M1 keystone, v21 M1-06 |
| Journey step 14 (supervisor) | **does not exist** | v21 M2-01 flagship; Thursday's only "next release" line |
| Assistant (step 15) | **cannot answer** | AddAssistant pack ready; producer unbuilt |
| Rule 1 (generic only) | **violated by installer** | `scripts/110`+`111` create 5 demo schemas on every customer DB; delete in M2-02 |
| Rule 2 (starts empty, DB-link only) | **true once step 6 lands** | today data only reaches canonical via the demo `src_*` copy |
| Browser verification | **still zero** | biggest open risk; v21 M1-02 |
| Deployment/pipeline | **green 26-Jun, unverified since; CI gates were fake, fix designed** | §9.3 |
| CI truth gates | **hardened design exists, apply/commit status UNKNOWN** | confirm via git first |

**The gates caught six of my mistakes across the two 10-Jul sessions** (including v1 of the cursor pack this session). Not one reached the working tree. That is the argument for tightening gates, never loosening them.

---

## 12. RULES, MANDATES, WAYS OF THINKING (Karim's — honour these)

**THE THREE PRODUCT RULES (now permanent memory; scripture; 100% by end of M2):**
1. **GENERIC ONLY** — the product is standard/generic for all industries. Never a single line, word, page, or component that is demo-specific or dataset-specific. The emulated external source fleet exists ONLY to test the product and prove in presentations it was tested; it lives OUTSIDE the product, where a customer's real DBs would be.
2. **STARTS EMPTY** — day one at the customer, the plant schema is empty. The customer configures a DB-link, the product imports, then analyses/suggests/predicts. **The only source of data is import via DB-link.** Data present without a DB-link import is a defect.
3. **THE JOURNEY IS THE PRODUCT** — the 15-step journey (memory entry #27) is authoritative and must be satisfiable end to end.

**THE 15-STEP JOURNEY (headlines; full text in memory #27):** (1) configure DB-link to customer sources → (2) link each to a db-link job, schedule+monitor → (3) incremental import to per-link staging files → (4) 1st no-code UI: prepare/filter/link/group across staging files into prep files that map to our schema → (5) link each prep file to a loading job → (6) loaded into our plant schema → (7) 2nd no-code UI: pages/dashboards/widgets/KPIs, drop a chart, bind to DB params, formulas/casts → (8) 3rd no-code UI: analysis/correlation/statistics with a drag-drop toolbox (material id vs defect) → (9) link each to a data-analysis job → (10) pages showing analysis-job results → (11) higher license: ML+AI files via the same 3rd UI → (12) link each to an AI+ML job → (13) pages showing ML+AI results → (14) **THE ENGINE = all analysis + all AI+ML jobs; two layers (normal stats + deep AI+ML) that improve each other; plus ONE premade SUPERVISOR job running nightly/weekly that reviews the whole dataset and every job's history and re-tunes their coefficients — the jobs are hands/arms/legs replying in minutes/hours, the supervisor makes them all permanently better** → (15) higher license: a chatbot/LLM answering FROM THE ENGINE.

**Working conventions:**
- Zero preamble, no flattery, honest defect surfacing, never claim done when not done.
- **Solution Doctrine:** permanent, committed, generic, product-grade fixes only. Never temp workarounds, never per-machine env vars to boot, **never skip/loosen an assertion to go green.**
- **Autonomous Generic-Fix Mandate:** diagnose and fix at source, generically, without asking permission. "Make it green" is forbidden.
- **Preventive-Maintenance Mandate:** read the whole path up front, enumerate every stage, surface ALL defects in one pass before running. (This session's M1-01 experiment embodied it — four defects found in one pass.)
- **Naming Golden Rule:** no phase/task/version/pack codes in artifact names — descriptive only. Numeric ordering prefixes for SQL migrations are functional tokens (preserve); embedded phase labels are not (strip).
- **Two admin types, never conflated:** `sysadmin` (SOU internal, auto-provisioned, undeletable, customer never sees) vs Customer Admin (manual commissioning, never auto-created).
- **Two Docker Compose projects on the server are deliberate. Never merge.**
- **Evidence before cure.** Karim's instinct has been right every time — he refused to commit red, stopped at a RESET that would have dropped 38,345 material_units, spotted stale keys a gate couldn't see. This session he ran the experiment that proved Architecture A instead of letting me build an 18h reader that already existed.
- **-WhatIf / dry runs before writes.** Every destructive pack gets one.
- **A guard satisfiable by its own prose/output is not a guard. Falsify every gate once.**
- **Tested is not wired. Grep for the registration, never the class.**
- Pricing (for ROI slides): Standard $12k deposit + $6k/mo; Pro Plus $28k + $14k/mo; Enterprise $50k + $25k/mo.

---

## 13. FOR THE NEXT SESSION — DO THESE, IN THIS ORDER

1. **`git status` + `git log --oneline -15`.** Confirm what's committed: the morning's `AddAssistant`/CI-truth-gate work, and whether the e2e-blocking Jenkinsfile change landed (§9.3). **If e2e now blocks, do not push until e2e realignment lands.**
2. **Run `Apply-ConnectorCursorFamilyFix.ps1 -WhatIf` then apply** (§6). Then `Apply-ScheduleBoardOrderingFix.ps1`.
3. **Restart API, run the M1-01 close-out** (§6.5): schedule-now → run-due (≈1797 rows) → run-due again (0 rows) → schedule-board 200 → `last_cursor_value` ISO string. **That +0 closes M1-01 and proves journey step 3 over the wire.**
4. **Run `Apply-AssistantServiceGraphRegistration.ps1`** (v21 M1-03, pack ready) → `/assistant` returns 200 isRefusal=true instead of 500.
5. **v21 M1-02 — browser verification of the nine 09-Jul surfaces** (blocks the rehearsal; nothing built 09-Jul has been seen).
6. **v21 M1-05** — repoint `SourceImportPrepPage` Register to `POST /admin/connectors/datasets` (backend proven working; mostly frontend).
7. **v21 M1-06 — the generic projector `StagingRecords`→canonical** (16h keystone).
8. Then M1-07 (parameter-observations pack), M1-08 (readiness verify), M1-09 (chunk producer + reindex), M1-10 (Jobs Monitor 4 types), M1-12 (rehearsal ×2), M1-13 (script + supervisor framing).

### Things you must NOT do
- Do NOT re-run the M1-01 experiment — the results are in §5. Just apply the cures and run the close-out.
- Do NOT re-run the frontend/backend suites to "check" — results in the morning handover §4 (full frontend 55 files/232 tests green; backend integration 665 total/0 failed/590 passed/75 skipped; Phase3GoldenThread 3/3).
- Do NOT re-investigate `ppiq_validate_genealogy_graph` (865 rows, triage is a task) or `MappingLifecycleProof`.
- Do NOT `reset-app-database.ps1` without confirming the 38,345 material_units can be regenerated. It runs `scripts/*.sql` only, NEVER `seed/`. The reload path (`loadA.sql.gz` + `Apply-SessionA-PlantThroughPipeline.ps1`) is gitignored — **run `git ls-files --error-unmatch loadA.sql.gz` first** (v21 M1-04). If untracked, the demo dataset exists on ONE laptop with no reproduction path.
- Do NOT build a generic reader or pursue FDW — Architecture A IS the reader (§4).
- Do NOT chase `V5AssistantGateway` for the chat — the frontend calls `AssistantService` (`/api/assistant/ask`).
- Do NOT seed `canon.assistant_chunk` by hand — a chunk with no canonical row is a fabricated citation.
- Do NOT "fix" the OracleConnector ISO serialization blindly — verify it first (§6.3 follow-up).

---

## 14. DELIVERABLES INDEX (this session, in /mnt/user-data/outputs)

| File | Purpose | Status |
|---|---|---|
| `PPIQ_Product_Backlog_v21.xlsx` | 147 tasks, 3 sheets, formula-driven. M1=13/53h/10Crit; M2 opens with P0 KEYSTONE (supervisor, delete Arch B, purge seeds, per-view loading) | current backlog |
| `PPIQ_Product_Roadmap_v8.md` | demo 16-Jul (14/15 steps live + step 14 framed); M2 28-Aug rules+journey 100%; M3 15-Oct | current roadmap |
| `Apply-ConnectorCursorFamilyFix.ps1` | v2 — 4 defects (D1 null-branch, D2 PG typed param, D3 ISO serialization, D4 dataset-create DTO), generic across PG/MySQL/MSSQL + the DTO query | NOT yet run |
| `Apply-ScheduleBoardOrderingFix.ps1` | schedule-board 500 (EF projection OrderBy) | NOT yet run |
| `Apply-AssistantServiceGraphRegistration.ps1` (morning) | AddAssistant() one-line registration | NOT yet run |

**Backlog v21 M1 tasks (for quick reference):** M1-01 verify Arch A (2h) · M1-02 browser check (1.5h) · M1-03 AddAssistant (0.5h) · M1-04 evidence pack/git-ls-files (0.5h) · M1-05 rewire Surface-1 (6h) · M1-06 generic projector KEYSTONE (16h) · M1-07 param-observations pack (4h) · M1-08 readiness verify (2h) · M1-09 chunk producer + reindex (10h) · M1-10 Jobs Monitor 4 types (3h) · M1-11 ppiq.ps1 readiness poll (0.5h) · M1-12 rehearsal ×2 + video (4h) · M1-13 script + supervisor framing (3h).

---

*End of handover. The next session should be able to apply two packs, run five API calls, and close M1-01 — then go straight to the projector. No re-investigation required.*
