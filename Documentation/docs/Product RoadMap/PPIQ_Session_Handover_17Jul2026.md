# PPIQ SESSION HANDOVER - 17-Jul-2026 (evening)
## For the next session: read this FULLY before running anything. Every test
## here was already run; every number is evidenced. Do not re-investigate.

Customer meeting: ~Mon 20-Jul (postponed from 16-Jul, +4 days).
Governing docs: Backlog v23 (47 tasks, committed), Interactive Workspace
Doctrine v1 (Amendment 7), State Assessment 16-Jul, senior's 17 recs (adopted).

---

# 0. WHO/WHAT/WHERE (operating facts - memorize)

- Repo `C:\Workspace\PlantProcess-IQ`. **Branch: `main` ONLY** - presentation
  branch merged today (`ab9771f9`) per senior rec 1. Branch delete still
  pending (`git branch -d presentation` refused pre-commit; retry now, use
  `--merged` to verify first, never -D blind).
- **Merge commit message contains git's comment block** (committed with
  `--no-edit` on a prepared file). AMEND STILL OWED (not pushed):
  `git commit --amend -m "merge(presentation): demo profile, interactive workspace, website v2, tooling"`.
- Databases @127.0.0.1:5432, user `ppiq_dev` / `ppiq_dev_local_only`:
  - `ppiq_app` = dev (small; 5 provenance-NULL units pending purge M2-23)
  - `ppiq_presentation` = demo. CURRENT STATE: 40,148 material_units /
    51,691 quality_events / 35,906 genealogy_edges / 14,433 parameter_
    observations / 16,640 staging_records / 16 import_batches / 21 parameter_
    definitions / 9 defect_catalogs / 320 results_v2 rows / 20 distinct
    compute runs / 12 dashboards ALL with widgets.
  - `ppiq_acceptance_empty` = NOT YET CREATED (M2-19).
- API: `.\scripts\run\start-api.ps1 -Profile presentation` -> :5063, DB
  ppiq_presentation. Login `e2eadmin / E2EAdmin123!`. Web: vite :5173.
- Fleet source containers (all healthy, up 4+ days): ppiq-src-caster-oracle,
  ppiq-src-hsm-oracle (FREEPDB1, user ppiq_src/ppiq_src_local_only, schema
  PPIQ_SRC), meltshop PG :15432, downtime MySQL :13306, parsytec :13307.
- Demo DB rebuild fixture: `deploy\.ppiq-snapshots\ppiq_app_20260713_203359.dump`
  (29.4 MB). NOT in git. Archive off-machine still owed (M1-12).
- Git safety: tag `safety/pre-merge-20260717_092847`; full bundle
  `C:\Workspace\ppiq-git-bundles\ppiq_all_20260717_092847.bundle` (253.6 MB)
  - copy off laptop still owed.

## Committed tooling (paths changed today - tools live IN the repo now)
`scripts\demo\`: **Rebuild-PresentationDb.ps1** (THE only supported demo-DB
rebuild, 7 steps, kills API by name, ~2 min, proven 3x today), plus the
superseded step scripts + README.md with the three-database table.
`scripts\verify\`: Certify-Journey, Verify-ImportChain (v1.2), Verify-
OracleDiscovery, Trap-PresentationWipe (v1.1), Protect-And-Merge, Set-
OracleSchema (v1.1), Fix-ProviderTypeBinding, Finalize-Tree, Finish-M1-18,
Commit-Remaining-Units.
**UNCOMMITTED at repo root** (move to scripts\verify + commit): Fix-
MlFoundationAccess.ps1, Run-GoldenAnalysis.ps1 (v1.2), Diagnose-BlockedRun.ps1,
Inspect-JobLog.ps1, Inspect-ImportPipeline.ps1, Complete-Merge.ps1 (obsolete),
report txt files. ALSO POSSIBLY UNCOMMITTED: the access-matrix fix itself in
`Backend\PlantProcess.Api\Security\PlantAccessControl.cs` - the commit was
instructed but never confirmed. CHECK `git status` FIRST THING.

---

# 1. WHAT HAPPENED TODAY - CHRONOLOGY WITH FINDINGS

### 1a. M1-18 Protect & Merge - DONE (minus amend + branch delete)
Phase 1: safety tag, 253.6MB bundle, diff+stat exported to `_merge_review\`,
logical commits landed over several passes:
`30954daf` profile, `d2d86ac2` workspace (senior's InteractiveWorkspacePage
167L + LiveWidgetChart 151L), `bebc8b23` gitignore, `27bd52b0` livecharts,
`0bc09d2d` website commercial-v2 (senior's overnight rebuild: 20 files,
+1327/-3235, new graphics/sections components + Playwright commercial spec),
`ffe92d9b` docs (v23/doctrine/assessment/deck), `36a317f2` stale
JourneyRail.test.tsx deleted (M1-25 DONE), `84dd6abd` scripts relocation,
`578015f4` provider-binding fix, `76e891c7` final tooling.
Merge: aborted once (v23 xlsx open in EXCEL -> checkout could not unlink ->
untracked leftover blocked merge). Fix: close Excel, `git rm --cached` + del
+ `git checkout presentation -- <xlsx>` to restore into the staged merge,
then `git commit --no-edit` -> **`ab9771f9` on main, 62 files, +6237/-3265**.
Vitest on the tree: **62 files / 253 tests ALL GREEN** (first time ever).

### 1b. THE PHANTOM WIPE INCIDENT - my error, ~5h lost. CRITICAL LESSON.
Twice today I declared ppiq_presentation "wiped to 4 units". IT NEVER WAS.
Root cause: PowerShell single-element-array unrolling in Verify-ImportChain's
Q1: `$r = Q $q` unrolls to a STRING; `$r[0]` returns the FIRST CHARACTER.
40148->"4", 51691->"5", 35906->"3". The tell I missed: import_batches "1"
while "existing batch ids: 16" - same table, contradictory numbers - AND
Rebuild-PresentationDb (safe `Select-Object -First 1` pattern) printed 40,148
concurrently. I trusted the broken instrument over the working one.
Consequences: 2 unnecessary rebuilds (10:22, 11:15 - both fine, idempotent),
a forensics trap built for a phantom, "API auth failures" that were just the
API being down after rebuild killed it.
**FIXES SHIPPED**: Q1 now `$r = @(Q $q)` + `[string]` cast; Verify-ImportChain
v1.2 SELF-CHECKS (`SELECT 40148` literal must round-trip or it REFUSES to
report). Same bug found+fixed in Set-OracleSchema (columns came back as
"c","p","s") and Trap live-counts.
**RULES FOR NEXT SESSION**: (1) every metric-reporting tool self-checks a
known literal first; (2) when two tools disagree, RESOLVE THE CONTRADICTION
before acting; (3) verify before alarming - I alarmed-then-verified three
times today (wipe, provider "data risk", phase5 fake endpoint) and was wrong
or overstated each time.

### 1c. Trap-PresentationWipe - armed, keep it
v1.1 armed 12:27: statement triggers (DELETE/TRUNCATE) on 6 core tables +
sql_drop event trigger + audit table in `ppiq_forensics` schema (survives
pg_restore --clean) + **off-DB OID tripwire**: `wipetrap_state.json` holds
db oid **78122**; a changed oid at -Report = DROP DATABASE happened. -Report
prints live counts first. RE-ARM AFTER EVERY REBUILD. No real wipe ever
occurred; the trap stays as cheap insurance.

### 1d. M1-19 Oracle - EARNED (screenshot in HMI still owed)
- Schema fix: UI form is a trap (see D1/D2). Applied via
  `Set-OracleSchema.ps1 -Execute`: SQL UPDATE schema_name='PPIQ_SRC' on
  CP-04+CP-06 only; provider/secret untouched.
- Verify-OracleDiscovery 14:12: **EARNED x2**. CP-04 -> HSM_COILS, HSM_PASSES,
  V_PARAMETER_DEFINITIONS (3). CP-06 -> CC_HEATS, CC_SEQUENCES, CC_SLABS,
  V_PARAMETER_DEFINITIONS (4). test-connect PASS both. "Live Oracle
  connector" is now an earned sentence.
- REMAINING for v23 acceptance: dated screenshot of the Tables list in the
  HMI (click Tables on both Oracle rows).

### 1e. Connection-profile form defects (from the Edit screenshot)
- **D1 (FIXED, committed 578015f4)**: provider dropdown showed "CSV Snapshot"
  on Oracle profiles. Cause: catalog publishes PascalCase ("Oracle"), stored
  rows lowercase ("oracle"); `<select value>` matched no option -> browser
  fell back to first. DISPLAY-ONLY (state held "oracle"; select disabled on
  edit) - I initially overstated it as a data risk; it never was. Fix:
  case-insensitive resolution in AdminDbConfigurationTab.tsx form select,
  copying the file's own list-view pattern (line ~362). Gates green 253/253.
  BROWSER VERIFY still owed (hard-refresh, reopen Edit -> must read "Oracle
  Read-only DB Link").
- **D2 (OPEN - architectural, do NOT patch the form)**: SECRET REFERENCE is
  required-but-empty. THREE SOURCES DISAGREE: ConnectorProviderCatalog.cs
  declares Oracle RequiresSecretReference:true; ALL 8 profiles have
  secret_reference=NULL; connections succeed anyway. So the backend resolves
  credentials somewhere the catalog does not describe. Until answered, NO
  connection profile can be edited through the UI (validation blocks every
  save). Question for the code: where does OracleReader/etc actually get
  user/password? Likely the seeded conninfo or env. This is a real
  security-review question ("where do plant passwords live?").

### 1f. M1-20 import chain - baseline honest, PHASE A NEVER STARTED
- Honest baseline 13:18 in `importchain_state.json` (root): the 40k numbers
  above + **16 batch ids recorded** (so anything new is provably fresh).
  NOTE: state file also has a stale phase-A entry (deltas all 0, jobLog +4
  from my own ConnectorTest calls). The +4 job_log entries were MY
  Verify-OracleDiscovery /test calls - not user activity.
- **Pipeline verified WORKING** (Inspect-ImportPipeline 14:21):
  TOTAL ROWS EVER IMPORTED = **17,204** across 9 of 16 batches.
  DELTA_MELTSHOP_HEATS...136803 = 1,802 rows == material_units with
  source_system='postgresql' (exact match: those units came through the
  CONNECTOR, not the dump). PARAM_READINGS 5000+5000+4416=14,416 ~=
  parameter_observations 14,433. Zero-row delta batches = cursors caught up
  = CORRECT behavior (my morning "silent zero-row import" alarm was wrong -
  I had only looked at the newest batches). Staging: **0 orphan rows** -
  every staging_record links to a real batch => -Chain LINK2 will grade
  [STRONG]. ADV_* batches = old seed-era; one "Intentional failed import
  batch for endpoint/status testing" exists (Rule-1 smell, minor).
- Cursors: 3 Meltshop datasets, cursor values ~2026-07-02/04, none NULL.
- **Registered datasets: ONLY 3 (all Meltshop facts).** The four Phase-A
  taxonomy views are ALL [MISSING]: Meltshop v_parameter_definitions (26
  rows), Caster PPIQ_SRC V_PARAMETER_DEFINITIONS (4), HSM V_PARAMETER_
  DEFINITIONS (7, incl lube_viscosity_cst), Parsytec v_defect_definitions
  (20). **Karim never performed the HMI registration despite 4 attempts at
  -Phase A** - each run showed zero deltas because nothing was done in the
  UI. The step CANNOT be scripted for him (Rule 3: the journey is the
  product); he must click Prepare Import himself. Expected registrations ->
  run jobs in Jobs Monitor -> `-Phase A` shows parameter_definitions >21,
  defect_catalogs >9, source_dataset_definitions 3->7, NEW batch ids.
  Then B (cc_slabs, hsm_coils -> material_units+genealogy), C (cc_heats,
  params -> observations), D (parsytec defects -> quality_events), then
  `-Chain` (LINK1..5; LINK4 [STRONG] if material_units has a batch column,
  else honest [TIME-WINDOW] label; LINK5 = H->SL->C walk).
- IMPORTANT: demo data provenance honesty - the 40,148 did NOT arrive via
  the pipeline (dump restore + 1,802 connector rows). If asked "did this
  come through your connectors?" the honest answer pre-Phase-A is: partially;
  the golden chain exists to close exactly that gap.

### 1g. M1-21 engine - THE BIG FINDINGS
1. **ACCESS-MATRIX DEFECT (fixed)**: every POST to /api/ml/foundation/*
   returned 403; GETs (readiness/outcomes) slipped through the anonymous
   ("/",GET) entry. => **The correlation engine had NEVER been invokable
   through the API in this build; journey step 9 was undemonstrable; the
   320 findings are all historical (inserted via SQL in earlier phases).**
   Fix: one Matrix line in `Security\PlantAccessControl.cs` after the
   `"/api/ml/learning"` entry:
   `("/api/ml/foundation", All(), "analysis.execute", false),` with
   explanatory comment. Build green (0 errors). Fix-MlFoundationAccess v1.1
   kills the API by process name first (Ctrl+C does NOT kill it; DLL locks
   fail the build and the gate then auto-reverts a GOOD fix - happened once).
   **COMMIT MAY STILL BE PENDING - check git status.**
2. **Engine now works through the API**: feature-store refresh 200 ->
   `feature_rows=14416, outcome_rows=51685, run_id=6cab8f61-...`.
3. **All 8 governed runs return status=Blocked, results=0** (engine
   dotnet-analytics-core-v1; run ids recorded in
   M1-21_GoldenAnalysis_20260717_161250.txt). WHY is unknown -
   **Diagnose-BlockedRun.ps1 is delivered and NOT YET RUN** - it dumps
   ml_correlation_compute_runs rows verbatim (the engine's own reason),
   feature/outcome grain distributions, shared-entity join count, and
   effect_size_type per method. PRIME SUSPECTS: (a) grain mismatch - I
   passed grain='material'; store may hold another grain; re-run with
   `-Grain <x> -SkipFeatureRefresh`; (b) per-pair min-rows gate.
4. **Endpoints verified in source**: real engine =
   `POST /api/ml/foundation/compute/correlation` body
   {outcomeKey, grain, windowDays, filters?} -> ICorrelationComputeEngine ->
   {computeRunId, resultCount, engineKey, status}. Also /readiness,
   /outcomes, /feature-store/refresh. Outcome catalog (8): downtime.
   cascade_minutes, kpi.energy_per_ton, kpi.prime_yield, kpi.throughput,
   defect.class, defect.position, defect.rate_per_m2, defect.severity.
   **`POST /phase5/scheduled-learning/run-now` is a STATUS-FLIP** (UPDATEs
   phase5_learning_job_evidence to Completed/Passed, computes nothing) -
   reachable ONLY from e2e/phase5-scheduled-learning-proof.spec.ts, no
   product page. Not a demo lie; still an M2-20 eradication row (an API that
   reports Passed without working).
5. **results_v2 REAL schema** (memorize; my first harness used wrong names):
   id, compute_run_id, model_version_id, feature_key, feature_grain,
   outcome_key, outcome_type, method, coefficient, effect_size,
   effect_size_type, p_value, q_value, ci_low, ci_high, sample_size,
   effective_n, stratum, stability_score, is_stable, window_start_utc,
   window_end_utc, evidence_json, created_at_utc, tenant_id.
   (There is NO source_correlation_run_id, NO population_count. q/p/ci
   EXIST as columns => **M1-09 is narrower than backlogged: surface the
   existing columns + dedup view, nothing to compute**.)
6. **THE MONEY-SLIDE QUESTION (open, must answer before Monday)**: the 320
   historical findings have effect_size min=0.005 max=1.000 (129 distinct).
   That is a bounded 0-1 metric (Cramers-V-like), NOT an odds ratio - my
   "max=1.00 means no effect" read was WRONG (1.0 = maximal on that scale);
   effect_size_type per method (Diagnose section 4) settles it. The 9.3x
   superheat->CRACK_LONG number is an odds ratio from the FLEET emulation
   analysis and **is not expressed anywhere in results_v2**. Options:
   (a) the fresh engine run over imported fleet data reproduces the signal
   in the engine's own metric (whatever scale) - the honest path;
   (b) the deck's 9.3x is explicitly labeled as the emulation-validation
   number, separate from product findings. DO NOT let the deck imply the
   product computed 9.3x until (a) exists.
7. Readiness also says: **kb_items=0, pgvector_available=False** - directly
   relevant to M1-01 (assistant may need pgvector or degrade to keyword;
   canon.assistant_chunk was 0 rows historically). Assistant routes exist:
   `/api/assistant/ask`, `/api/assistant/reindex` (+ assistant-config).

---

# 2. POWERSHELL/TOOLING BUG PATTERNS LEARNED TODAY (bake into every script)
1. **@() around every function-return you index**; `([string]$r[0]).Trim()`.
   Self-check literals before reporting metrics.
2. `-f` alignment is `{0,10}` / `{0,-10}`; **`{0,>10}` throws**.
3. **No leading `+` line continuation** - operators must trail the line.
4. `ORDER BY <position>` fails when the SELECT is a single concatenated
   expression; `GROUP BY 1` with an aggregate inside expression 1 fails
   ("aggregate functions are not allowed in GROUP BY") - one instance still
   in Run-GoldenAnalysis section [BEFORE] (harmless, prints ERROR line).
5. **Ctrl+C does not kill PlantProcess.Api**; Stop-Process by name before
   any dotnet build or pg_restore (encoded in Rebuild + Fix-MlFoundation).
6. **Excel locks**: open xlsx -> checkout "Invalid argument" unlink failures
   + untracked leftovers that block merges; `~$*` lock files. Close Excel
   before git operations touching Documentation\.
7. gitignore edits must be LINE-EXACT checks (Contains() substring gave a
   false positive: '/.ppiq-backups/' "found" inside 'deploy/.ppiq-backups/').
8. `git add A B C` aborts ALL paths if one pathspec matches nothing.
9. `git commit --no-edit` on a template with comments keeps the comment
   text in the message. Amend before pushing.
10. Delete stale state json (importchain_state) after fixing the instrument
    that wrote it - poisoned baselines make false PASSES later.
11. Anchored-replace + gate + auto-revert kept saving us; keep the contract.
12. Access-matrix failure signature: POST 403 + GET works = unmapped route.

---

# 3. BACKLOG v23 STATUS BOARD (evidence-graded, end of 17-Jul)

M1-P1 Golden Chain:
- **M1-18** protect/merge: **DONE 95%** [P]. Remaining: amend merge msg,
  `git branch -d presentation`, commit root-level tools + (verify) the
  PlantAccessControl fix commit, copy bundle off laptop.
- **M1-19** Oracle live: **EARNED** [P via API + report]. Remaining: HMI
  Tables screenshot (2 min).
- **M1-20** chain part 1: **BLOCKED ON HUMAN** - baseline honest, pipeline
  proven working, taxonomy views not registered. Karim must do Prepare
  Import x4 in the browser. Everything scripted around it is ready.
- **M1-21** fresh run: **60%** - engine unblocked (matrix fix), feature
  store refreshes, runs execute but Blocked. Next: Diagnose-BlockedRun ->
  fix grain/gate -> Completed run with results -> re-run AFTER Phase A-D
  for the full "watched-data" claim. Wording earned so far: NONE of the
  money claims yet (runs blocked).
- **M1-01** assistant: NOT STARTED. kb_items=0, pgvector=False. Plan:
  POST /api/assistant/reindex -> chunkCount>=20 -> /api/assistant/ask
  grounded question with citation; predictive question must refuse.
- **M1-03** taxonomy-first: BLOCKED (= Phase A acceptance).
- **M1-09** finding hygiene: NARROWED - q/p/ci/stability columns already
  exist; build latest-run dedup read view + surface columns in payload.
- **M1-22** step-13 401: NOT STARTED. Same access-matrix class as today's
  fix - check the Suggestion page's route against the Matrix FIRST.
- **M1-23** KPI '-' wiring: NOT STARTED (quality/risk keys on Command
  Dashboard read '-' vs 51,691 events).
- **M1-11** browser cert (15 steps + 12 workspaces, S4/S7): NOT STARTED.
- **M1-24** senior-code review (InteractiveWorkspacePage 167L, 496-line
  journey-professional.css, website v2): NOT STARTED - merged UNREVIEWED.
- **M1-25** stale test: **DONE** [P] (deleted 36a317f2; suite 253/253).
- **M1-26** certifier v0.2 fixes (S06 information_schema, S08 pattern):
  NOT STARTED.
- **M1-12** continuity: partial (FLEET_RELATIONS.md rescued earlier -
  verify it is committed under docs/emulation/; fleet source SQLs only in
  session dumps).
- **M1-05** supervisor verify, **M1-06** alerting 2 rules, **M1-14**
  rehearsal x2, **M1-15** website/deck evidence-linking: NOT STARTED.
  M2-18 (rebuild command) DELIVERED EARLY + committed.

Day plan remaining (2.5 days): Day-2 = Diagnose+unblock engine, PHASE A-D
(Karim in HMI), fresh Completed run, assistant reindex+ask, M1-22/23 fixes.
Day-3 = M1-11 browser cert + M1-24 review + M1-05/06 + M1-09 + certifier
fixes. Day-4 = rehearse x2, freeze noon, website/deck.

---

# 4. SERVER / PIPELINE / DEPLOYMENT (no server work THIS session - standing
# knowledge from prior sessions, unchanged)
- Hetzner VPS 178.105.152.180. TWO-PROJECT TOPOLOGY IS PERMANENT:
  `plantprocessiq` = sacred infra (Jenkins/Caddy/backup-runner);
  `ppiq-app` = application deploy. NEVER merge them.
- Caddy defect history: Caddyfile once routed to non-existent container
  `plantprocess-app-web` (real name `plantprocess-web`); runtime docker
  network alias applied as workaround; permanent Caddyfile fix was blocked
  by a read-only bind-mount with missing host source file.
- Jenkins: GitHub webhook `https://jenkins.178.105.152.180.sslip.io/github-webhook/`;
  Jenkinsfile backs up .env/Caddyfile/docker-compose.demo.yml before git
  reset and restores after; one manual "Build Now" primes a new job.
  AuditLogImmutabilityTests are SkippableFacts (need live PG with triggers).
- **TODAY'S MERGE IS NOT PUSHED.** Pushing main will trigger the pipeline;
  before pushing: amend the merge message, commit the root tools + matrix
  fix, and expect the deploy to carry the workspace + website v2 live.
- Mail/Spamhaus remediation, prod Ed25519 keypair: still deferred (M2).
- **Deployment-relevant risk**: test isolation (M2-21) - backend suites
  write fixtures into whatever DB the connstring names; PPIQ_TEST_PG_
  CONNSTRING isolation still not implemented. Never run `dotnet test`
  while profile points at ppiq_presentation.

---

# 5. KARIM'S RULES + HOW TO WORK WITH HIM (verbatim discipline)
- Zero preamble, no flattery, evidence before cure, honest defect surfacing,
  NEVER claim done when not done (M1-20 pressure today: he asked "is
  everything done so we go further?" - answer was NO with a table; keep
  doing that).
- Everything as PS 5.1 apply-packs: bypass launcher line
  `powershell -NoProfile -ExecutionPolicy Bypass -File .\X.ps1`, preflight
  unique anchors, byte backups, tsc/vitest or dotnet-build gates,
  auto-revert, pure ASCII, UTF-8-no-BOM, CRLF, no `&&`, cuddled `} else {`,
  run from repo root. Dry-run default, -Execute to act.
- Three Product Rules: Generic Only / Starts Empty / the 15-step Journey is
  the product. Demo = real app on emulated source data + ONE framing
  sentence ("this instance runs on our emulated multi-source plant - on
  your install it starts empty and fills via the DB-link imports").
- Senior's 17 recs are adopted doctrine: one branch, code/data by profile,
  three DBs, reproducible demo DB, golden evidence chain (fresh batch ->
  ... -> cited answer), evidence grades [P]/[B]/[T]/[D]/[X], no M1-complete
  claims, careful 9.3x wording ("recovered a planted validation signal and
  rejected a null control - validates the method; ROI is what the pilot
  measures"), stop breadth / certify the golden path.
- He works in parallel with a senior session; collisions happened (workspace
  page, website v2, mojibake reintroduction). Before editing frontend files,
  CHECK CURRENT STATE (the convergent/drift-aware pack pattern), never
  overwrite unreviewed parallel work, never assume the 15-Jul dumps match
  the tree.
- The two repo dumps (15-Jul 10:53 + 20:40, files 00-08) were this session's
  source-of-truth for code reads; the tree has since drifted (merge). Prefer
  live `git show`/file reads next session where possible.

# 6. FIRST 30 MINUTES OF THE NEXT SESSION (exact order)
1. `git status` + `git log --oneline -5` -> commit PlantAccessControl.cs fix
   if dirty; move root tools to scripts\verify; commit; amend merge message;
   `git branch --merged main` then `git branch -d presentation`.
2. `powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-BlockedRun.ps1`
   (or from scripts\verify after the move) -> read section 1 (engine's own
   block reason) + section 2 (grain) -> re-run
   `Run-GoldenAnalysis.ps1 -Execute -Grain <actual> -SkipFeatureRefresh`
   until a run COMPLETES with results; record the new compute_run_id.
3. Get Karim into the HMI for Phase A (four registrations) - this is the
   critical path and only he can click it. Verify with
   `scripts\verify\Verify-ImportChain.ps1 -Phase A` (self-checking v1.2).
4. Then B, C, D, -Chain; re-run the analysis on the imported window; then
   M1-01 reindex+ask citing the new run id.
Do not re-run: Inspect-JobLog, Inspect-ImportPipeline, Verify-OracleDiscovery
(unless HMI state changed), any rebuild (DB is healthy - trap will prove it).
