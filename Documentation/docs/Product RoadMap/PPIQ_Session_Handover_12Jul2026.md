# PPIQ SESSION HANDOVER - 12-Jul-2026 (deep, authoritative)
**Purpose:** hand a fresh session the full state so it NEVER re-investigates, re-traces, or re-runs what is already proven below. Read this top to bottom before touching anything. Every fact here is either [P] proven live (console pasted this session), [T] source-traced (file/line in the dump), or [D] decided/doctrine. Trust [P] and [T] - do not re-verify.

---

## 0. WHO / WHAT / WHEN
- **Karim** - solo founder/dev, SOU Industrial Software (Dusseldorf). Product: **PlantProcess IQ (PPIQ)** - generic, read-only, industry-agnostic process-to-quality intelligence platform, ~EUR100k/plant.
- **Hard deadline:** 2nd customer meeting (CEO + technical engineer) **Thu 16-Jul-2026** - a live 15-step journey demo. ~3 days out.
- Repo `kgamal369/PlantProcess-IQ`, local `C:\Workspace\PlantProcess-IQ`.
- **Authoritative constitution = `concept.md`** (produced this session). It supersedes rules.txt. Everything derives from it.

## 1. ENVIRONMENT - EXACT, VERIFIED [P]
- **App DB:** native Postgres service `postgresql-x64-16` on `127.0.0.1:5432`, DB `ppiq_app`, user `ppiq_dev` / `ppiq_dev_local_only`. **~11,997+ material_units originally; now 40,148** (mostly seed). This is the rich DB. Do NOT use `plantprocessiq`/`plantprocess`/`plantprocess123` (~27 seed rows only).
- **API:** `http://localhost:5063`. Start: `.\scripts\run\start-api.ps1 -Profile local`. Login `e2eadmin / E2EAdmin123!`. Health/ready log line: `Now listening on: http://localhost:5063`.
- **Web:** `.\node_modules\.bin\vite --host localhost --port 5173` (bypass broken start-web launcher). `VITE_SMOKE_PASSWORD` must be `E2EAdmin123!` in BOTH `env\profiles\local.env` AND `Frontend\PlantProcess.Web\.env.local`.
- **Emulated sources (Docker):** `docker compose -f deploy\compose\docker-compose.sources.yml up -d`. **meltshop-postgres on `127.0.0.1:15432`, DB `meltshop`, user `ppiq_src` / `ppiq_src_local_only`, table `meltshop_heats` (1802 heat_ids).** NOTE: `up -d` may error on `ppiq-src-pkl-mssql` name conflict - HARMLESS, meltshop is what matters; it stays up. Network `ppiq-sources` pre-exists (warning only).
- **Solution:** `Backend\PlantProcessIQ.sln` (no .sln at repo root; fallback `dotnet build Backend`). Target framework net9.0. Build normally shows ~19-25 warnings (CS8604/CS0618/CS0162) - these are pre-existing, NOT from our changes.
- **Hetzner VPS:** `178.105.152.180`. Two-project server topology (plantprocessiq infra vs ppiq-app deploy) is PERMANENT/DELIBERATE - never merge.

## 2. THE PERMANENT RULES KARIM GAVE (obey every turn) [D]
- **POWERSHELL-ONLY DELIVERY.** Every operational step is a ready-to-run `.ps1` - never raw psql/docker/git one-liners. Always lead the runbook with `powershell -NoProfile -ExecutionPolicy Bypass -File .\Script.ps1` (immune to execution policy + mark-of-the-web; his machine blocks unsigned scripts with PSSecurityException - that is NOT a script bug).
- **Apply-pack contract** (for code edits): preflight (anchor exists + unique) -> backup to `deploy\.ppiq-backups\` -> literal String.Replace -> self-check -> `dotnet build` gate -> **auto-revert on failure**. Pure ASCII, UTF-8-no-BOM (`[System.IO.File]::WriteAllText` with `UTF8Encoding($false)`), CRLF, no PowerShell-level `&&`.
- **Communication:** zero preamble, no flattery, evidence before cure, honest defect surfacing, never claim done when not done.
- **Autonomous Generic-Fix Mandate:** diagnose and fix at source, generically, without asking permission.
- **The Three Product Rules** (see concept.md): Generic Only / Starts Empty (DB-link only) / Journey is the Product.
- **THE TAXONOMY CLARIFICATION (constitutional, 12-Jul):** defect + parameter taxonomy is IMPORTED PLANT DATA, not product config. Flat-steel != paper != mineral-water defects; every unit/device has its own vocabulary. It must import via DB-link like any dataset. The config class shrinks to IDENTITY ONLY (Site, license, sysadmin). The `PPIQ_CONFIG` rows we inserted this session are a Rule-2 VIOLATION to purge in C.2.
- **Milestone definitions:** M1 = every journey step SHOWABLE (screens + working path; accuracy/multithreading/full-license not required; nothing fabricated). M2 = concept.md at 100%. M3 = post-customer feedback.
- **Guards must be falsified once (seen red) before trusted. "Tested is not wired" - grep for the registration, not the class.**

## 3. HARD-WON POWERSHELL / TOOLING TIPS (do not re-learn) [P]
- **psql NOTICE on stderr aborts under `$ErrorActionPreference='Stop'`.** Wrap native psql calls: save EAP, set `'Continue'`, call, capture `$LASTEXITCODE`, restore EAP, then check the code. Also put `SET client_min_messages TO WARNING;` at the top of DDL scripts to silence `DROP ... IF EXISTS` notices.
- **A `WITH` CTE lives for ONE statement only.** For multi-statement psql where two INSERTs share scoring, materialize into a `CREATE TEMP TABLE _scored AS ...` (temp tables persist across statements within one `psql -c` session). This bit us in M1-07 Phase 1 (defect INSERT failed: `relation "scored" does not exist`).
- **Build-gated packs MUST guard against a running API** (it locks `PlantProcess.Api.exe`): `Get-Process -Name 'PlantProcess.Api'` -> fail early telling the user `Stop-Process -Id <pid> -Force`.
- **Prefer SINGLE-LINE substring anchors.** Multi-line CRLF blocks reconstructed from the dump fail byte-match (this cost a retry on the connection-profile fix). If you must anchor multi-line, expect a preflight miss and fall back to substrings.
- **Avoid `-Db` param name** (collides with `-Debug` alias under CmdletBinding).
- **C# `&&` inside single-quoted PS strings is fine** (it is written into the .cs). Lint only flags PS-level `&&`. When checking paren balance, strip single-quoted C# literal lines first or you get false imbalances.
- **execute endpoint takes params as QUERY STRING not body:** `POST /integration/mapping-definitions/{id}/execute?importBatchId=&take=&stopOnFirstError=`.
- **Connector import throttles at ~5000 rows/dataset/run.** Large tables need multiple `run-due` passes; project EVERY batch, not just the largest. Failed staging rows (MarkFailed) are `is_processed=true` and will NOT auto-retry - reset with `UPDATE staging_records SET is_processed=false, processing_status='Pending', processing_error=NULL WHERE source_object_name=... AND processing_status='Failed';` then re-project.
- **Uploaded attachments sometimes arrive empty** - workaround: user pastes console output as text (they do this reliably).

## 4. ARCHITECTURE GROUND TRUTH (traced, do not re-discover) [T]
- **Two import architectures existed.** Architecture A = correct generic C# engine (6 `IDataSourceReader` connectors, throttled, cursor-tracked, scheduled). Architecture B = wrong same-DB SQL over `src_*` demo schemas. **C.1a DONE:** `MapTwoStageImportEndpoints()` unregistered in `Program.cs` -> ladder unreachable. Arch-B frontend page still orphaned (C.1b pending); `ppiq_run_stage1/2` SQL + `src_*` schemas still in DB (C.2 drop).
- **The generic projector already existed** (M1-06 was carded as 16h greenfield but was really an integration task): `Backend\PlantProcess.Application\Integration\Services\Mapping\MappingExecutionService.cs`. Flow: `RunAsync` -> `ParseFieldMap` -> selects Pending StagingRecords by ImportBatchId -> `MapOneRowAsync` routes on `mapping.TargetEntityName` -> entity mappers. We added `const:` literal support to `Optional(...)`.
- **Canonical read path:** `canonical_material_units` + `canonical_genealogy_edges` are VIEWS over `material_units`/`genealogy_edges` (relkind=v, counts match). Dashboards read `material_units` directly (85 refs); genealogy reads the canonical views. So imports are canonical-visible automatically. [P]
- **Auto import->project pipeline exists:** `ImportBatchQueueProcessorService` + `ImportWorkflowService` (registered ~Program.cs:60038) auto-execute the active MappingDefinition per batch, MarkFailed with a named reason when none exists.
- **ML pipeline:** feature refresh `ppiq_ml_refresh_feature_store_v6(windowDays)` reads `parameter_observations` (features) + `quality_events` (outcomes, keyed to `defect.rate_per_m2`) -> `ml_feature_values` + `ml_outcome_values`. Governed run `ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_DEFECT', windowDays, 20, false)` -> readiness gate -> `ppiq_ml_compute_basic_correlations` -> `ml_correlation_results_v2`. Stats = Pearson + Benjamini-Hochberg q-values ONLY (no Spearman/chi-square/ANOVA yet).
- **Assistant:** `AssistantService(IRetrievalIndex, ToolRegistry, IAssistantModel)`. Only model impl is `ExtractiveAssistantModel` (line ~51358) - NO LLM. `AddAssistant()` confirmed never called; `canon.assistant_chunk` has zero rows; three parallel assistant endpoint groups exist but the wired one was never registered.
- **Concurrency:** NO bounded-parallelism executor. 2 Channels, 1 ConcurrentQueue (reaper), ConcurrentDictionary. Jobs run serially on worker ticks against one Postgres. 100-job support = M2 architecture task.
- **The 4th low-code UI (alerting/plant-data-log) DOES NOT EXIST** anywhere (grep-confirmed: only error-boundary "alert" panels). Whole surface absent.

## 5. MAPPER / ENTITY CONTRACTS (exact, from source) [T]
Router `MapOneRowAsync` switches on `mapping.TargetEntityName.Trim()`; supported: MaterialUnit, MaterialAlias, ProcessStepExecution, ParameterObservation, QualityEvent, GenealogyEdge, **DefectCatalog + ParameterDefinition (added this session, M1-03)**. Fallback `_ => FailOrThrow(...)`.
- Helpers: `Required(fieldMap,sourceRow,"X")`, `Optional(...)`, `OptionalDateTime`, `OptionalInt`. `Optional` supports `const:LITERAL`. Success return: `new MappingExecutionRowResult(stagingRecord.Id, stagingRecord.RowNumber, "Mapped", entity.Id, "<Entity>", null)`. Skip: MarkSkipped + "Skipped". Fail: `FailOrThrow` (MarkFailed + "Failed").
- Code-keyed upsert pattern: `_dbContext.<DbSet>.AsNoTracking().Where(x => x.Code == code).Select(x => x.Id).FirstOrDefaultAsync(ct)` -> if `!= Guid.Empty` skip, else `new Entity(...)` + `_dbContext.<DbSet>.Add(entity)` + `stagingRecord.MarkMapped(entity.Id, "<Entity>")`.
- **DefectCatalog** ctor: `(string defectCode, string defectName, string? defectCategory, string? industryTemplate, string? sourceSystem=null, string? sourceRecordId=null)`; throws if code/name blank. DbSet `_dbContext.DefectCatalogs`. Entity at CORE ~79024.
- **ParameterDefinition** ctor: `(string parameterCode, string parameterName, string valueType, string? unitOfMeasure, string? parameterCategory, string? industryTemplate, string? sourceSystem=null, string? sourceRecordId=null)`; valueType defaults "Numeric" if blank. DbSet `_dbContext.ParameterDefinitions`.
- **ParameterObservation** mapper: resolves MaterialCode + ParameterCode; needs ObservedAtUtc (RequiredDateTime) + NumericValue (OptionalDecimal). Resolvers: `ResolveMaterialIdAsync` (MaterialCode/MaterialUnitId), `ResolveParameterDefinitionIdAsync` (ParameterCode).
- **QualityEvent** mapper: MaterialCode + DefectCode (resolves defect_catalogs by code via `ResolveOptionalDefectCatalogIdAsync` - returns null if absent, so the catalog row MUST pre-exist); EventType (Required), EventAtUtc (RequiredDateTime), Severity (Optional). quality_events has NO defect_code column - the code lives in `defect_catalogs` joined via `quality_events.defect_catalog_id`.
- **defect_catalogs** columns: id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id, is_deleted, deleted_at_utc, deleted_reason, defect_code, defect_name, defect_category, industry_template.
- **parameter_definitions** columns: (base) + parameter_code, parameter_name, value_type, unit_of_measure, parameter_category, industry_template, expected_min_value, expected_max_value.
- **RegisterSourceTableRequest** = (SchemaName, TableName, PrimaryKeyColumns[list], WatermarkColumn?, SelectedColumns?, RowFilter?). Register route: `POST /admin/connectors/connection-profiles/{id}/register`.
- **SourceDatasetDefinition** ctor: `(connectionProfileId, datasetCode, datasetName, datasetKind, sourceObjectName, isSynthetic, sourceSchemaName?, primaryTimestampField?, incrementalCursorField?, refreshIntervalSeconds=300, datasetOptionsJson?)`. Methods: `Update(...)`, `Activate()`, `ScheduleNextRunImmediately()` (sets NextRunAtUtc for "due now" - this is how schedule-now works).

## 6. WHAT WE BUILT / FIXED THIS SESSION (with proofs) [P]
All scripts delivered to the user as .ps1. State on his machine reflects everything below UNLESS noted.

1. **M1-06 projector close - BANKED.** `const:` support added; Stage-A bridge projected 1802 heats. Verify pasted: re-projection = 0 rows (idempotent); 1802 imported, tz_null=0, offset_null=0 (NOT-NULL held). Two M2 refinements logged: Site-config timezone (defaults "UTC"), job_log write of typed field errors.
2. **C.1a Arch-B ladder retirement - BANKED.** `MapTwoStageImportEndpoints()` unregistered; build green.
3. **M1-05 backend register repoint - BANKED (build green).** New `RegisterSourceDatasetAsync` handler in `ConnectorAdminEndpoints.cs` upserts a SourceDatasetDefinition from discovery (Arch A) instead of `ppiq_register_dump_source` (Arch B); route repointed; old handler left dormant (dies M2-02). Required usings added: `Microsoft.EntityFrameworkCore` + `PlantProcess.Domain.Entities.Integration`. **First build failed CS0246 (missing entity using) -> fixed -> green.** M1-05b (frontend page job-binding) still pending (small).
4. **Pre-existing D4 bug FIXED - BANKED (build green).** `GetConnectionProfileByIdAsync` filtered a projected `ConnectionProfileDto` by Id (untranslatable LINQ -> 500). Fix: `GetConnectionProfileDtoQuery` gained optional `Guid? profileId=null` + pre-projection `where`. Files: `ConnectorConfigurationService.Profiles.003.GetConnectionProfileByIdAsync.cs` + `...017.GetConnectionProfileDtoQuery.cs`. **First anchor (2-line block) missed -> switched to 2 substring edits -> green.** This is the same D4 anti-pattern (filter/order after DTO projection) seen in the schedule-board 500 and dataset-readback 500.
5. **M1-07 rigged data - COMPLETE (both phases).** See section 7 for the numbers.
6. **M1-08 engine discovery - PROVEN.** See section 7.
7. **M1-03 taxonomy mappers - PACK DELIVERED, awaiting his build result.** `Apply-M1-03-TaxonomyMappers.ps1` adds `MapDefectCatalogAsync` + `MapParameterDefinitionAsync` + 2 router cases. Not yet confirmed green on his machine (last action of the session). **The fresh session's first job: get his M1-03 build result; if green proceed, if red read the first error and fix in one pass.**

## 7. TEST RESULTS - ALREADY RUN, DO NOT RE-RUN [P]
- **Plant state (Diag-M1-07-PlantState):** material_units 40,148 total (Slab 18,070 caster + Coil 17,817 hsm + Heat 2,431 ladder + Heat 1,802 our clean postgresql import + seed remainder). CRACK_LONG base rate ~0 (only 1 stray SURFACE_CRACK). quality_events has NO quality_event_type / defect_code column (code is via defect_catalog_id join). parameter_definitions existing codes are SUPERHEAT_C/CARBON_PCT/CASTING_SPEED etc. (NOT the dotted codes).
- **M1-07 Phase 1 (rig in source, meltshop 15432):** generated `meltshop_param_readings` (14,416 = 8 params x 1802 heats) + `meltshop_defect_events` (420). **Realized odds ratio CRACK_LONG vs high-superheat = 9.51** (with `-P0PerMille 54`; raw default gave 10.02). SCRATCH control = 0.93. Constants: HighPct 30, P1PerMille 328, P0PerMille 54. Deterministic via hashtext (re-runnable).
- **M1-07 Phase 2 (through pipeline):** after adding taxonomy as PPIQ_CONFIG (the violation) + register + map + import + project: `parameter_observations = 14,433`, `quality_events` CRACK_LONG 245 / SCRATCH 175. Connection profile used: `dddd0000-0000-0000-0000-000000000201` (DEMO-READY-CP-01). Mapping ids: param `44e7cb8c-...`, QE `b3b72cef-...`.
- **M1-08 engine (PROVEN):** `ppiq_ml_refresh_feature_store_v6(365)` = 54,574 feature / 91,839 outcome rows. Governed run = Ready / Completed / 33 findings. Engine ORGANICALLY rediscovered `thermal.true_superheat -> quality.defect_rate_per_m2` at **effect 0.924, q=0.0001**; also `-> kpi.prime_yield` 0.961/0.0001, `-> defect_hold_binary` 0.655/0.002. Honest nulls: `-> downtime.*` q 0.47-0.63, `-> kpi.energy_per_ton` q 0.48-0.49. **CAVEAT: confounded** - the refresh still reads the `204` demo generator's `ml_learning_observations_v1` (findings like `operations.crew_shift`, `product.grade_family` we never imported). Engine reports Pearson coefficient (0.92), NOT the 9.5x odds ratio. **Both are cured by C.2 purge + retire the 204 feed, then one clean re-run.**

## 8. BACKLOG v22 STATUS (standalone, fresh IDs; 38 tasks/225h/19 crit) [D]
Milestones: M1 = presentation (every step showable + website/slides). M2 = concept.md 100%. M3 = post-customer.
**Approved 4-tier M1 execution order (dependency-true, all M1 mandatory):**
- **Tier 1 (foundation):** M1-03 taxonomy mappers [PACK DELIVERED, awaiting build] -> M1-08 snapshot+C.2 purge+re-import+clean engine re-run [NOT STARTED] -> M1-04 step-4 data-prep UI authors MappingDefinition [NOT STARTED].
- **Tier 2 (marquee):** M1-01 assistant chunk producer + `/api/assistant/reindex` (10h, largest) -> M1-09 finding hygiene (latest-run view + odds ratio + population) -> M1-02 Jobs Monitor (4 job types).
- **Tier 3 (journey screens):** M1-05 Supervisor v0 (real weekly job writing a real report; NO auto-tune claim) -> M1-06 4th UI v0 alerting (fires on rigged superheat) -> M1-10 production error shape (stop leaking LINQ/stack traces) -> M1-07 Arch-B frontend removal (C.1b).
- **Tier 4 (P2 wrap):** M1-11 consolidated HMI walk (=old M1-02 + M1-14 merged) -> M1-14 dress rehearsal x2 -> M1-13 website+slides -> M1-12 continuity archive -> M1-16 register page job-binding (M1-05b) -> M1-15 ppiq.ps1 readiness. Plus **M1-17 journey progress affordance** (added this session).
NOTE: IDs above use v22 sheet numbering; earlier in-chat I mislabeled a couple (dress rehearsal is M1-14 in the sheet). Trust the sheet.
Done-and-removed from backlog (banked with evidence): old M1-05 backend, M1-06, M1-07, M1-08, C.1a, connector fixes, the D4 by-id fix.

## 9. DELIVERABLE DOCS PRODUCED THIS SESSION (in /mnt/user-data/outputs, user has them)
- `concept.md` - THE constitution (supersedes rules.txt). [DONE]
- `PPIQ_Implementation_Audit_12Jul2026.md` - 11 personas x 15 points = 165 findings, evidence-graded; headline **56/100 (A9 UI/UX)**; includes 15-step journey matrix + infra sizing model v1. [DONE]
- `PPIQ_Scoreboard_12Jul2026.html` - Dark-Industrial, donut gauges (currently shows 8 gauges; the A9/A10/A11 sync to 11 gauges was left pending - minor).
- `PPIQ_Product_Backlog_v22.xlsx` - 38 tasks, standalone, fresh IDs, computed Phase Summary. [DONE]
- **STILL OWED (documentation suite, deferred, Karim requested):** Roadmap v9 from scratch (v8 stale); Doctrine v9 TWO-PART (A sales/marketing, B deep technical for the customer engineer); Identity & Topology v5 (validate v4, patch); FOUR diagrams as SVG/HTML (1 full platform architecture, 2 user journey flowchart, 3 Engine/brain with supervisor loop, 4 sales/marketing). His 3 Gemini PNGs have ERRORS to correct: image3 shows `dump_store` (that's Arch-B; real is StagingRecords), labels projector as M1-03 (it's the assistant task), says CSV connector + Chatbot/LLM which don't exist yet. Karim chose to resume CODING before these; they remain owed.

## 10. SCORECARD SNAPSHOT (end of session) [D]
Headline **56/100 (A9 UI/UX & Journey Experience)**. Personas: A1 Dev 76, A2 Security 68, A3 Quality 61, A4 Ops 67, A5 Exec 72, A6 Brand 63, A7 Governance 59, A8 Commercial 64, **A9 UI/UX 56**, A10 AI/Engine 58, A11 Infra 62. Top strengths: falsifiable engine discovery with honest nulls; projector/connectors better than carded; honest-refusal assistant shape. Worst gaps: step-4 UI drives wrong subsystem (M1-14), 4th UI absent, taxonomy import (now being fixed via M1-03), engine confound (C.2), supervisor absent, assistant mute (M1-01). We improved the trajectory sharply this session (4 of 6 original M1 build tasks banked live) but the DATA still fails governance until C.2.

## 11. IMMEDIATE NEXT ACTIONS (in order) for the fresh session
1. **Get Karim's M1-03 build result** (`Apply-M1-03-TaxonomyMappers.ps1`). Green -> proceed. Red -> read first error, one-pass fix (likely a missing using or member; the file already imports the entity namespace via its six siblings, so probably green).
2. **Extend the rig + import taxonomy through the pipeline:** add `meltshop_defect_definitions` + `meltshop_param_definitions` source tables; author DefectCatalog + ParameterDefinition mappings; import them FIRST (resolvers depend on them). This makes taxonomy DB-link-sourced and unblocks purge.
3. **M1-08 = snapshot ppiq_app -> C.2 purge** (seed material_units/edges, PPIQ_CONFIG rows, demo site rename, drop src_* schemas + stage1/2 functions, RETIRE the 204 `ml_learning_observations_v1` feed) **-> re-import everything via DB-link (definitions first) -> clean engine re-run.** Snapshot BEFORE purge (no restore drill exists yet). The clean re-run should show superheat->defect with q<0.01 on 100% imported data, no crew_shift/grade_family confounds.
4. Then Tier 2: M1-01 assistant chunk producer (biggest build) -> M1-09 finding hygiene -> M1-02 Jobs Monitor. Then Tiers 3-4.

## 12. DEPLOYMENT / PIPELINE / SERVER KNOWLEDGE [T from memory + prior handovers]
- **CI/CD:** Jenkins on the server; GitHub webhook `https://jenkins.178.105.152.180.sslip.io/github-webhook/`; Jenkinsfile backs up `.env`/`Caddyfile`/`docker-compose.demo.yml` before `git reset` and restores after. One manual "Build Now" primes it.
- **Two server stacks:** `plantprocessiq` = sacred infra (Jenkins/Caddy/backup-runner). `ppiq-app` = app deploy. NEVER merge.
- **Known server-side "green" fixes (prior sessions):** Caddyfile routed to non-existent container name `plantprocess-app-web` (real: `plantprocess-web`) - runtime Docker network alias applied as workaround; permanent fix blocked by read-only bind-mount with missing host source. Launcher bug: `vite localhost 5173` positional args rejected by Vite 5+ (use `--host --port`). Smoke bug: `VITE_SMOKE_PASSWORD=change-me-before-production` baked into bundle caused 401 auto-login loop (must be E2EAdmin123!).
- **Deferred infra (M2):** production Ed25519 keypair (currently dev Option 1); Hetzner/Spamhaus mail remediation (SPF/DKIM/PTR, relay 587/465, block outbound 25); PgBouncer; partition parameter_observations/ml_feature_values (monthly + BRIN on observed_at_utc); incremental feature refresh; tested restore drill; job telemetry.
- **This session touched NO server/deployment** - all work was local (ppiq_app + meltshop container + local build). No push happened. The "make pipeline green + App URL work" items above are prior-session knowledge, not re-verified today.

## 13. THINGS NOT TO DO (learned the hard way)
- Do NOT hand-insert taxonomy or any plant data via psql in a "real" workflow (it is a Rule-2 violation; the PPIQ_CONFIG rows are already flagged for purge). It was acceptable only as a temporary rig step.
- Do NOT re-run the M1-07/M1-08 investigations or the plant-state diagnostics - the numbers are in section 7.
- Do NOT reconstruct multi-line CRLF anchors from the dump - use substrings.
- Do NOT assume a project builds because code "looks right" - the build gate is the test; write defensively and let auto-revert protect the tree.
- Do NOT frame the assistant as "LLM" or the supervisor as "auto-tuning" for the demo - grounded assistant + roadmap framing (honesty doctrine).

*End of handover. Source dumps (read-only) live at /mnt/user-data/uploads: 01_Backend_Core, 02_Backend_Database, 03_Backend_Tests, 04_Frontend_App, 07_Tools_Validation_Misc (all 12Jul2026_133640). concept.md is the constitution. Backlog v22 is the plan. Section 11 is where to start.*
