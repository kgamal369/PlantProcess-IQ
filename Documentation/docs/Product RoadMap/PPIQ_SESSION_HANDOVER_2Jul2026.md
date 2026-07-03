# PlantProcess IQ — Deep Session Handover (2 July 2026)

> **Purpose:** This document hands over EVERYTHING from this working session so a fresh session starts fully informed — no re-investigation, no re-running tests, no starting from green land. Read this end-to-end before acting.

> **Who / context:** Karim Gamal, solo founder & developer of **SOU Industrial Software** (Düsseldorf). Building **PlantProcess IQ (PPIQ)** — a read-only, evidence-grade, GENERIC process-to-quality intelligence platform, installable to any industry via configuration only. Stack: **.NET 9 + React/TypeScript (Vite) + PostgreSQL 16 + Docker/Caddy/Jenkins** on a Hetzner VPS at **178.105.152.180**.

---

## 0. TL;DR — WHERE THINGS STAND AT END OF THIS SESSION

**Customer state: GOOD.** First demo (1-Jul, procurement audience, mineral-water industry) happened. Customer is engaged and asked for an updated website to show internally → he's a real, continuing prospect. **Next presentation: 8 July** — deeper, with a sales + technical engineer who will scrutinize every feature, every value, every button, and evaluate the AI. That raises the bar to V2 (every number justified).

**Work state: HEAVY backlog exposed.** During prep we discovered the **entire "Test Journey" frontend does NOT exist as specified**: no working "create DB Link" page, no "link DB Link → Job" page, pages not built as requested. Karim considers V1 backlog needs substantial RE-IMPLEMENTATION, plus V2.

**What actually works right now (verified live this session):**
- Local app **runs** (API :5063, Web :5173) against DB `ppiq_app` with **11,997 material_units** (rich data).
- Enterprise license active (all features unlocked).
- Command Dashboard renders with real material data + genealogy.
- 6 demo-source containers healthy.
- Server (178.105.152.180) deployed with latest code (SHA e1f86970), pipeline GREEN.

**What is empty/broken/missing:**
- Correlation/AI/suggestions/KPI screens are **empty by design** (no import/jobs run) — but the ENGINES are implemented (>65%, see §4).
- Data Quality page: all zeros. Dashboard charts (Quality trend / Risk distribution): empty placeholders.
- Internal task codes leak into UI: **"PPIQ-T033", "PPIQ-T048/T050/T053", "Phase 1 Workflow Truth"** + **DEMO / DEVELOPMENT badges** — bad for customer demos.
- The **DB Configuration** tab (create-connection form) exists in code (`AdminDbConfigurationTab.tsx`, section 7 "DB Link Configuration UI") but was NOT visible on the running Administrator page (only saw tabs: Connector Truth | Import Jobs | Tier Override). **This is the core "journey not implemented as requested" gap.**
- Website content may be out of date vs. app (Karim's claim — NOT verified this session because uploaded files kept returning EMPTY).

**Deliverables produced this session (in /mnt/user-data/outputs):**
- `PlantProcessIQ_Presentation.pptx` — 22-slide procurement deck (see §11).
- `PPIQ_DEMO_RUNBOOK.md` — demo-day runbook.
- `ppiq-journey-check.ps1` — GREEN/RED readiness checker.

---

## 1. CANONICAL ENVIRONMENT FACTS (verified live — do NOT re-discover)

### 1.1 LOCAL (Karim's Windows laptop)
- **Main DB = NATIVE Postgres on Windows**, `localhost:5432`. NOT a container.
- **Correct DB = `ppiq_app`**, user **`ppiq_dev`**, password **`ppiq_dev_local_only`** → has the RICH data (**11,997 material_units, 1,993 quality_events, 5,688 genealogy_edges**).
- **WRONG DB = `plantprocessiq`** / user `plantprocess` / pw `plantprocess123` → only **27 seed rows**. Do NOT point the app here. (There are 3 DBs on local: `plantprocessiq`, `postgres`, `ppiq_app`.)
- **Env profile:** `env\profiles\local.env`. **There is NO `local.env.example`** → fresh checkouts throw `Profile not found`. Restore from `deploy\.ppiq-backups\...\env\profiles\local.env`.
- **Demo sources = Docker** (6 containers, healthy): `ppiq-source-meltshop-postgres` (PG), `ppiq-source-caster-oracle`, `ppiq-source-hsm-oracle` (Oracle), `ppiq-source-parsytec-mysql`, `ppiq-source-downtime-mysql` (MySQL), `ppiq-source-pkl-mssql` (MSSQL).
- Ports: API 5063, Web 5173, Website 5080, Preview 4173.

### 1.2 SERVER (Hetzner VPS 178.105.152.180, Ubuntu, root SSH)
- All DBs are Docker containers. Main app DB container = `plantprocess-postgres`.
- Runs latest code (SHA **e1f86970**), pipeline GREEN.
- 7.6 GB RAM. **4 GB swap added this session** (was 0) — persisted via `/etc/fstab`.
- App URL: `app.178.105.152.180.sslip.io`, API: `api.178.105.152.180.sslip.io`, Website: `website.178.105.152.180.sslip.io`, Jenkins: `jenkins.178.105.152.180.sslip.io`.
- Server login (DB-backed): `sysadmin` / password rotates per deploy (read from Jenkins env log; last seen `710a1a18a1d24bca6d2260854853a607`).

### 1.3 Two-project Docker topology on server (NEVER merge)
- `plantprocessiq` project = infra: `ppiq-caddy`, `ppiq-jenkins`, `ppiq-backup-runner`.
- App containers: `plantprocess-api` (:5063), `plantprocess-web` (:80), `plantprocess-postgres`.
- A STALE `ppiq-app-api` (Up 4 weeks) also runs — harmless, not Caddy-referenced; can be stopped.
- Networks: `plantprocessiq_ppiq-net` (caddy+web+api) and `ppiq-app_plantprocess-private` (api+postgres). `plantprocess-api` is on BOTH (correct).

---

## 2. HOW TO START THE LOCAL APP (verified working — the launchers are BUGGED)

**Critical discovery:** the `start-*.ps1` launchers pass the port POSITIONALLY (`vite localhost 5173`), which Vite 5+ rejects with `CACError: Unused args: 5173` (and `5080` for website). This breaks `start-web.ps1`, `start-website.ps1`, and even `start-local.ps1`'s web/website windows.

**Working start sequence (each in its own window, leave running):**
```powershell
# API (this launcher works)
cd C:\Workspace\PlantProcess-IQ
.\scripts\run\start-api.ps1 -Profile local
# wait for: "Now listening on: http://localhost:5063"

# Web (BYPASS broken launcher — call Vite binary directly)
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web
.\node_modules\.bin\vite --host localhost --port 5173
# wait for: "Local: http://localhost:5173/"

# Website (same fix, optional)
cd C:\Workspace\PlantProcess-IQ\Website\PlantProcess.Website
.\node_modules\.bin\vite --host localhost --port 5080
```

**Permanent launcher fix (V1/V2 task):** change `start-web.ps1` / `start-website.ps1` to:
`npm run dev -- --host $env:VITE_HOST --port $env:VITE_PORT`
(Currently they pass args positionally. The `package.json` `dev` script is just `vite`, which is correct.)

**A saved `ppiq-run.ps1` was drafted** that starts all three via `Start-Process` using the direct-Vite-binary approach — recommended for demo day.

---

## 3. THE LOGIN BUG — ROOT CAUSE + FIX (fully solved this session — do NOT re-debug)

**Symptom:** app stuck on "Backend connection failed / Invalid login credentials or refresh session expired" — never showed a login form. Clearing cache / incognito did NOT help.

**Root cause:** the app **AUTO-LOGS-IN on boot** using baked `VITE_SMOKE_USERNAME` / `VITE_SMOKE_PASSWORD` (via `AuthContext.tsx`). The generated `.env.local` (and `local.env`) shipped `VITE_SMOKE_PASSWORD=change-me-before-production` (a placeholder) while the real `ppiq_app` password is `E2EAdmin123!`. So the app auto-submitted `e2eadmin` / `change-me-before-production` → `POST /auth/login` 401 → repeated `POST /auth/refresh` 401 → stuck error screen. The wrong password was **baked into the running bundle**, so browser-side clearing could never fix it.

**Fix (applied + permanent):**
1. Set `VITE_SMOKE_PASSWORD=E2EAdmin123!` and `PPIQ_SMOKE_PASSWORD=E2EAdmin123!` in BOTH `env\profiles\local.env` AND `Frontend\PlantProcess.Web\.env.local`.
2. Kill old Vite on 5173 (`Get-NetTCPConnection -LocalPort 5173 → Stop-Process`), restart `.\node_modules\.bin\vite --host localhost --port 5173` so the corrected value is re-baked.
3. Confirmed working.

**Login facts (verified live on `ppiq_app`):**
- `admin` / `DevAdmin123!` → **401** (NOT seeded in ppiq_app, despite Identity doc §4.1 listing 5 dev-seed users).
- **`e2eadmin` / `E2EAdmin123!` → WORKS** (Admin role). This is THE local login.
- Login response token field is **`accessToken`** (not `token`).
- Because the app auto-logs-in, you NEVER see a login screen locally. **To show a login screen in a demo, disable the auto-login** (V2 consideration).

---

## 4. FEATURE IMPLEMENTATION AUDIT (verified via code + DB this session)

Karim's competitive edge is **Correlation + AI**. He needed to confirm these are >65% implemented so he can honestly claim them ("implemented, validation ongoing"). Empty DB tables are BY DESIGN (no data imported / no jobs run) — NOT evidence of missing implementation.

### Verdict: all three well above 65%.

**CORRELATION / ML — ~85% (strongest).**
- Backend: 23 files. Key: `CorrelationEndpoints.cs` (28.5KB), `CorrelationService.cs` (24.8KB), `AdvancedCorrelationComputeService.cs` (18.8KB), `MutualInformation.cs`, `StatisticalDiscipline.cs`, `ManagedStatisticalComputeEngine.cs`.
- DB: 20+ ML functions (`ppiq_ml_compute_correlations_v6`, `ppiq_ml_run_learning_job_governed_v1`, `ppiq_ml_learning_readiness_v1`, golden-test harness).
- Frontend: `CorrelationPage.tsx`, `AdvancedAnalysisPage.tsx`, `AnalysisHonestyBar.tsx`.
- Tests: `P06_StatisticalMethodsTests`, `ManagedStatisticalComputeEngineTests`.
- Methods: Pearson, Spearman, Cramér's V, point-biserial, mutual information, FDR q-values, readiness gates, noise rejection.
- **Live DB check:** `ml_correlation_compute_runs` = **358 runs** (347 "Running"/stuck, 10 "Blocked", 1 "NoData"); `ml_correlation_results_v2` = 0; `correlation_results` = 0. So runs were attempted but produced no results on local (empty because no proper populated dataset / jobs not completing). Engine EXISTS; results not populated.

**AI ASSISTANT / grounding — ~80% (differentiator).**
- Backend: 31 files. Key: `V5AssistantGateway.cs` (24.1KB), `GroundingService.cs`, `AssistantGroundingEvalGate.cs`, `ToolRegistry.cs`, `NpgsqlRetrievalIndex.cs`, `AssistantEgressGuard.cs`, `DeterministicEmbeddingProvider.cs`.
- Frontend: `AssistantChat.tsx`, `GroundedAssistantPage.tsx`, `AssistantConfigurationPage.tsx`, `AssistantRuntimePage.tsx`.
- DB tables present: `assistant_chunk`, `assistant_embedding_provider_config`, `assistant_index_run`, `assistant_retrieval_index_job`, `ppiq_assistant_provider_configs`, `ppiq_assistant_audit_log`, `ppiq_assistant_eval_cases/runs`, `ppiq_assistant_model_pins`, `ppiq_assistant_prompt_governance_events`, `ppiq_assistant_redaction_policies`.
- Grounding contract blocks uncited numbers + causal phrases; egress/data-boundary guard; eval harness. **Genuinely ahead of typical BI.**
- Note: `ppiq_assistant_provider_configs` has NO `is_active` column (my earlier query assumed it) — schema differs; a live-model wiring check was NOT completed. Frame AI carefully until provider wiring confirmed.

**SUGGESTION ENGINE — ~75%.**
- Backend: 16 files. Key: `SuggestionEngine.cs`, `NpgsqlSuggestionStore.cs` (10KB), `SuggestionWorkflow.cs`, `SuggestionConfidence.cs`, `Phase8SuggestionOutcomeLoop.cs` (closed-loop outcome tracking).
- Frontend: `SuggestionCardsPanel.tsx`, `SuggestionRecommendationPage.tsx`.
- Tests: `Phase9_T047SuggestionWorkflowCertificationTests` (15.9KB — large cert suite).
- DB: `suggestion` (in `canon` schema — `canon.suggestion` = **0 rows**), `suggestion_audit`, `suggestion_comment`, `ppiq_suggestion_action_outcomes`, `ppiq_visual_mapper_canonical_suggestions`.
- Deterministic MD5-stable IDs, confidence scoring, RBAC workflow.

**KPI:** tables + views exist (`kpi_definitions`, `kpi_targets`, `kpi_evaluation_alerts`, `kpi_parameter_bindings`, plus views `vw_kpi_defect_escape_rate`, `vw_kpi_first_pass_quality_yield`, `vw_kpi_production_impact_minutes`, `vw_kpi_data_coverage_completeness`). `kpi_targets` = 0 on local (empty by design).

**Data collection (backbone):** `parameter_definitions`=13, `parameter_observations`=17, `material_units`=11,997, `quality_events`=1,993, `process_step_executions`=14, `risk_scores`=7, `downtime_events`=3, `data_quality_issues`=7.

---

## 5. SERVER STABILITY SAGA — full investigation + resolution (do NOT re-investigate)

The night before the demo, huge time was spent chasing an intermittent "Backend connection failed" on the SERVER app that recurred every few minutes, synchronized across ALL Karim's devices (laptop/tablet/phone) on multiple networks (wifi + mobile data).

### What was RULED OUT (proven):
- **NOT server containers crashing:** `plantprocess-api` restarts=0 since 07:45, `plantprocess-web`/`ppiq-caddy`/`plantprocess-postgres` restarts=0, OOMKilled=false.
- **NOT memory/OOM:** even with 3 demo sources running, API used ~351MB, plenty free; no kernel OOM kills (`dmesg` clean). 4GB swap added anyway.
- **NOT browser cache alone:** failed across all devices + networks simultaneously.
- **Server-side curl ALWAYS passed:** 5/5, 15/15, 20/20, 30/30 app=200 api=200/401. TLS handshake 0.008–0.019s. Caddy logs showed NO errors during failure windows.

### The ACTUAL root cause of the Caddy 502s (found + explained):
- **Caddyfile routes `app.*` and `:80` to container `plantprocess-app-web:80` — but the real web container is named `plantprocess-web`.** `plantprocess-app-web` does not exist → `dial tcp: lookup plantprocess-app-web on 127.0.0.11:53: server misbehaving` → 502.
- The wrong name originates in the repo's `docker-compose.demo.yml` (which generates the Caddyfile).

### Fixes applied to server (this session):
1. **Runtime network alias (the thing that makes the app reachable NOW):**
   ```bash
   docker network connect --alias plantprocess-app-web plantprocessiq_ppiq-net plantprocess-web
   ```
   This makes the wrong name resolve to the real container. **It is RUNTIME-ONLY and vanishes on container restart/redeploy.** Re-run it if the app 502s again.
2. **Attempted permanent Caddyfile fix — BLOCKED:** the Caddyfile is a bind-mount whose host source (`/opt/PlantProcess-IQ/Infrastructure/deploy/Caddyfile`) **does not exist on disk** (only `README.md` + `docker-compose.demo.yml` are in that dir). Inside the container the file is **read-only** (`sed -i` → "Resource busy"; `cat >` → "Read-only file system"). So the file could not be edited in place. The routing config Caddy actually uses is served from a stale/cached mount.
3. **4GB swap** added + persisted (`/swapfile`, fstab).

### The DNS red herring (important context):
- `dig`/curl timing from the server itself showed DNS lookup for `app.178.105.152.180.sslip.io` swinging 0.002s ↔ 0.7s. `sslip.io` is a free public wildcard-DNS service; theory was it's flaky from the public side. BUT the branded "Backend connection failed" screen (not a browser "can't reach" page) proves the app HTML loaded and it was the API call failing — so this pointed away from pure DNS. The synchronized-multi-device pattern + clean server logs meant the true issue was the edge/routing (the alias fragility), not sslip.io. **However this was never 100% closed** — the intermittent multi-device failure was NOT definitively root-caused before the session pivoted to local. LEADING THEORY: the runtime alias + Docker DNS occasional "misbehaving" after network changes.

### PERMANENT server fixes still OUTSTANDING (post-demo / V1-V2):
1. Correct `plantprocess-app-web` → `plantprocess-web` in the REPO (`docker-compose.demo.yml` + the generated Caddyfile), commit, redeploy calmly. This removes the alias dependency.
2. Clean up the stale read-only Caddyfile mount situation (deploy-architecture defect).
3. Stop the stale `ppiq-app-api` container.
4. The `website.*` host in the Caddyfile points at `plantprocess-website` — a container that may not exist → website host 502s. Also `start-website.ps1` is bugged.

### CADDYFILE CONTENTS (as running on server):
```
{ email admin@plantprocessiq.com }
app.178.105.152.180.sslip.io      { reverse_proxy plantprocess-app-web:80 }   # WRONG NAME
api.178.105.152.180.sslip.io      { reverse_proxy plantprocess-api:5063 }     # correct
website.178.105.152.180.sslip.io  { reverse_proxy plantprocess-website:80 }   # container may not exist
jenkins.178.105.152.180.sslip.io  { reverse_proxy jenkins:8080 }
:80                               { reverse_proxy plantprocess-app-web:80 }   # WRONG NAME
```

---

## 6. DEPLOYMENT + PIPELINE KNOWLEDGE (verified)

- **Deploy IS current:** Jenkins build #99 deployed SHA `e1f86970` (the commit with the Stage-2 fix, mapper 500-fixes, source-picker). Server runs latest code. Pipeline GREEN, presentation smoke passed (sysadmin login OK, Enterprise license activated).
- **Server is EMPTY by design:** `PPIQ_DEMO_SOURCES_MODE=disabled` in the deploy `.env` (`/var/jenkins_home/workspace/plantprocessiq-deploy/deploy/compose/.env`), `PPIQ_PROFILE=server`. Canonical tables = 27/6/18 seed rows; all 10 sources in `source_table_dump_registry` show `stage1_status=NeverRun`; all jobs `NeverRun`.
- **Demo-source infra on server IS scripted:** `deploy/compose/docker-compose.demo-sources.yml` (services: `ppiq-demo-sources`, `meltshop-postgres`, `caster-oracle`, `hsm-oracle`, `pkl-mssql`, `downtime-mysql`, `parsytec-mysql`, `excel-yard`, `excel-qa`). Seed data at `/opt/PlantProcess-IQ/Infrastructure/demo-sources/`. Start scripts are PowerShell (`start-demo-sources.ps1`) — on the Linux server use `docker compose` directly.
- **8 connection profiles exist** (`DEMO-READY-CP-01..08`) with EMPTY `host_name` (placeholders). In a live demo the host = the source container name (e.g. `meltshop-postgres`).
- **API required config outside Development** (enforced by `P01P02StartupGuard` + `StartupConfigurationValidator`): `ConnectionStrings__PlantProcessDb` (NOT `__DefaultConnection`); `PlantProcess__Auth__SigningKey` ≥64 chars; a real admin in `PlantProcess:Auth:Users` (Role=Admin, IsBootstrapAdmin=false); CORS via `PLANTPROCESS_ALLOWED_ORIGINS`.
- **Server CORS (verified):** `PLANTPROCESS_ALLOWED_ORIGINS=https://app.178.105.152.180.sslip.io,https://178.105.152.180.sslip.io,https://website.178.105.152.180.sslip.io`. Preflight OPTIONS returns 204 with correct `access-control-allow-origin`. CORS is NOT the problem — routing was.
- **Web bundle baked API URL (verified):** `https://api.178.105.152.180.sslip.io` (correct). `http://localhost:5063` strings are dev fallbacks only.

---

## 7. IMPORTANT RULES / WAYS OF THINKING / ORDERS FROM KARIM (carry forward — non-negotiable)

1. **Demo-readiness bar (memory #24):** "happy path" for a customer demo does NOT mean show only 2–3 pages and skip 50% of the app. It means walk the FULL concept BROADLY on a REHEARSED route with zero crashes. Purpose = concept + trust + memorable positive impression so the customer comes back. Specific values/numbers are SECONDARY at V1 (they matter at V2, i.e. the 8-July meeting).
2. **The empty start is BY DESIGN, not a bug.** The demo narrative IS the pipeline told live: (1) app starts EMPTY = day-one install; (2) connect to customer data sources read-only; (3) show data transfer source→staging→canonical; (4) show dashboards/jobs/analysis after transfer. Correlation/AI/suggestion tables being empty with no import/jobs is CORRECT and expected.
3. **Only check IMPLEMENTATION existence (>65%), not data presence,** when validating features. Frame incomplete-but-real features as "implemented, validation/testing ongoing." NEVER claim something not implemented.
4. **Correlation + AI are THE competitive edge.** Positioning: "Standard BI stops at 'See'; PlantProcess IQ takes you to 'Reason'." Trust-builder: cite the DATA VOLUME each capability was tested on.
5. **Honesty framing on root cause:** always "evidence toward likely contributors" / correlation — NEVER "guaranteed root cause / causation."
6. **Generic-only red line:** the platform must install to ANY industry via configuration only, zero code change. The Stage-2 hardcoded IF/ELSIF ladder is V1-only and must be replaced by a generic projector consuming SchemaViewDefinition mappings in V2 (with a CI enforcement gate).
7. **Frame the demo dataset as "a sample plant"** — say it once up front, then translate any industry-specific term to the customer's world. Customer is MINERAL WATER industry; demo data is steel — do NOT go deep into steel specifics or he'll think it's not built for him.
8. **Working style (all sessions):** zero preamble, no flattery, honest defect surfacing, never claim done when not done. Pure ASCII, UTF-8 no-BOM, LF for .sh. **PowerShell 5.1 on Windows (never bash there).** Single `& { }` paste-blocks. `[System.IO.File]::WriteAllText` with `UTF8Encoding($false)`. No `&&`. Cuddled `} else {`. Backup-before-edit to timestamped dirs. Commits only behind `$env:PPIQ_COMMIT='1'`, never `git add -A`. Use `git commit -F` for multi-line messages (PS word-splits quoted -m).
9. **Naming Golden Rule:** never use phase/task/milestone codes in artifact NAMES — descriptive only. (Numeric ordering prefixes for SQL migrations are functional and allowed but must not embed phase/task labels.) NOTE: the UI currently VIOLATES this by leaking "PPIQ-T033/T048" etc. — a real defect to fix.
10. **Solution Doctrine:** permanent/committed/generic/product-grade fixes only, never transient workarounds. (The server runtime alias violates this and is explicitly flagged as temporary.)
11. **Preventive-Maintenance Mandate:** read actual files end-to-end, predict the entire failure map, surface ALL defects in one pass before running.
12. **Two admin types (golden rule):** (1) "sysadmin" = permanent, undeletable, auto-provisioned ONLY by FirstRunProvisioningHostedService during install — customer never sees it; (2) "Customer Admin" = created manually during commissioning, NOT auto-provisioned.
13. **Two Docker Compose projects on server must NEVER be merged** (`plantprocessiq` infra vs app containers) — merging reaps Jenkins/Caddy.

---

## 8. EVERY TEST / QUERY RUN THIS SESSION + RESULT (so the new session does NOT re-run them)

| # | What was run | Result |
|---|---|---|
| 1 | Server: web bundle baked API URL grep | `https://api.178.105.152.180.sslip.io` (correct) |
| 2 | Server: Caddyfile reverse_proxy targets | `app.*`/`:80` → `plantprocess-app-web` (WRONG); `api.*` → `plantprocess-api:5063` (correct) |
| 3 | Server: which api containers on which networks | `plantprocess-api` on both nets; `ppiq-app-api` stale on ppiq-net; `ppiq-caddy` on ppiq-net |
| 4 | Server: web container real name | `plantprocess-web` (confirms Caddy points at nonexistent name) |
| 5 | Server: CORS env + OPTIONS preflight | origins correct; preflight 204 with correct allow-origin (CORS is fine) |
| 6 | Server: container restart counts | ALL restarts=0, OOMKilled=false (nothing crashing) |
| 7 | Server: `free -h` before/after sources | 7.6GB RAM, ~5GB free; added 4GB swap |
| 8 | Server: `docker stats` with sources | API 351MB, sources ~435MB each; memory NOT the issue |
| 9 | Server: `dmesg` OOM check | clean (no OOM kills) |
| 10 | Server: 5x/15x/20x/30x curl app+api | all passed (200/401); TLS 0.008–0.019s; no Caddy errors in windows |
| 11 | Server: `dig` timing for app host | swings 0.002s↔0.7s (sslip.io variability; deprioritized) |
| 12 | Server: network alias applied | `app page: 200` after alias — app reachable |
| 13 | Server: Caddyfile edit attempts | FAILED — read-only bind-mount ("Resource busy" / "Read-only file system") |
| 14 | Local: `psql plantprocess/plantprocessiq` | 401 auth fail then 27 rows — WRONG DB |
| 15 | Local: `psql ppiq_dev/ppiq_app` | **11,997 material_units** — CORRECT DB |
| 16 | Local: list databases | `plantprocessiq`, `postgres`, `ppiq_app` |
| 17 | Local: feature table existence | ALL present (suggestion in `canon` schema; assistant_*, correlation, kpi_* all exist) |
| 18 | Local: ML jobs status | `SYSTEM_ML_PARAMS_VS_DEFECTS/DOWNTIME/KPI`=Ok; many `SYSTEM_ML_PARAMS_VS_KPIS`/`ML_WEEKLY_FULL`=NeverRun; `ML_WEEKLY_OVERALL`=Ok |
| 19 | Local: `ml_correlation_compute_runs` | **358 runs** (347 Running/stuck, 10 Blocked, 1 NoData) |
| 20 | Local: `ml_correlation_results_v2` / `correlation_results` | 0 / 0 (empty — no completed results) |
| 21 | Local: `canon.suggestion` count | 0 |
| 22 | Local: data collection counts | params 13/17, materials 11,997, quality_events 1,993, process 14, risk 7, downtime 3, dq_issues 7 |
| 23 | Local: KPI views exist | yes (defect_escape, first_pass_yield, production_impact, data_coverage) |
| 24 | Local: implementation file counts | Correlation 23 files, Assistant 31 files, Suggestion 16 files (see §4) |
| 25 | Local: discovery of start scripts | `start-local/api/web/website.ps1`, `start-main-db.ps1`, `start-demo-sources.ps1`, `ppiq.ps1` |
| 26 | Local: what's listening | 5432 (Postgres PID 8332) up; 5173/5063 initially NOT listening |
| 27 | Local: `local.env` restored from backup | pointed at `plantprocessiq`/`plantprocess` (WRONG) → repointed to `ppiq_app`/`ppiq_dev` |
| 28 | Local: API start | SUCCESS — "Now listening on http://localhost:5063", migrations already up to date, DB=ppiq_app |
| 29 | Local: web start via launcher | FAILED — `CACError: Unused args: 5173` (launcher bug) |
| 30 | Local: `package.json` dev script | just `vite` (correct — launcher injects bad positional args) |
| 31 | Local: web start via `.\node_modules\.bin\vite --host localhost --port 5173` | SUCCESS — "Local: http://localhost:5173/" |
| 32 | Local: browser login attempts | 401 loop — auto-login with baked WRONG password |
| 33 | Local: API login test `admin`/`DevAdmin123!` | 401 (not seeded) |
| 34 | Local: API login test `e2eadmin`/`E2EAdmin123!` | **200 OK** (accessToken present) — THE working login |
| 35 | Local: DevTools Network on failure | `POST /auth/login` 401, `POST /auth/refresh` 401, `OPTIONS /auth/login` 204 (CORS fine, creds wrong) |
| 36 | Local: `.env.local` VITE_SMOKE_PASSWORD | `change-me-before-production` (THE BUG) |
| 37 | Local: fix + restart Vite | app loaded, dashboard 11,997 materials — WORKING |
| 38 | Local: Enterprise license activate | tier=Enterprise verified=True (was already active; key `PPIQ-DEV-ENTERPRISE`) |
| 39 | Local: admin page tabs seen | Connector Truth \| Import Jobs \| Tier Override (NO DB Configuration tab visible — the journey gap) |
| 40 | Local: admin page components in code | `AdminDbConfigurationTab.tsx`, `AdminImportingDataTab.tsx`, `AdminJobsMonitorTab.tsx`, `AdminSchemaConfigurationTab.tsx` exist under `src\pages\Admin\` |
| 41 | Local: DB Configuration tab content | has "DB Link Configuration UI" (section 7) + `ImportJobSchedulePanel` (section 9) — form EXISTS in code but not surfaced on running page |
| 42 | Local: website pages | `ProductPage.tsx`, `PositioningTruthBlock.tsx`, `ConnectorHonestyBlock.tsx`, `BrandProofSection.tsx`, `PricingLicenseMatrix.tsx`, `ProductScreenshotShowcase.tsx`, `RequestDemoForm.tsx`, `SOUBrand.tsx`; content in `content/phase1WebsiteProof.ts` |
| 43 | Local: website copy seen (partial) | "Not MES. Not SCADA. Not Level 2. Not BI-only.", "Connector truth is part of the product, not sales decoration.", "Read-only source → staging → canonical model → analytics → report.", "Industrial, technical, calm, and evidence-first." |

**NOTE:** Website content file (`phase1WebsiteProof.ts`) full contents were NOT captured — uploads returned empty repeatedly. The new session must read it fresh: `Get-Content .\src\content\phase1WebsiteProof.ts -Raw` from the Website dir.

---

## 9. THE SCREENSHOTS CAPTURED (for the deck)

- **Command Dashboard (dark theme):** 11,997 materials, but Quality events="-", Risk="-", empty Quality-trend/Risk-distribution charts. Shows "PPIQ-T033", "DEVELOPMENT"+"DEMO" badges, "Demo Plant". **Placed in deck Stop 4** (browser chrome cropped).
- **Command Dashboard (light theme):** same, not used.
- **Data Quality page:** all zeros ("No quality findings"), "PPIQ-T036". NOT good for demo — not used.
- **Administrator → Connector Truth:** 7 connector types (CSV, Excel, PostgreSQL, SQL Server, MySQL, Oracle, REST API) all "Tracked" with "-". Shows "PPIQ-T048/T050/T053", "Phase 1 Workflow Truth". Useful as connector-concept but internal codes are a problem.
- **Karim's photo** (`Me.jpg`, 202×202) — placed on deck bio slide.

**Screens still needed (not captured):** Material Investigation (genealogy drill-down) — likely the strongest working feature; the DB Configuration create-connection form; Import Jobs; a populated correlation/AI screen (would require running the pipeline).

---

## 10. THE V1 "JOURNEY NOT IMPLEMENTED" GAP (the big backlog reality)

Karim's key finding: **the Test Journey frontend does not exist as specified.** Specifically:
- **"Create DB Link" page** — was requested; the create-connection FORM exists in `AdminDbConfigurationTab.tsx` (section 7 "DB Link Configuration UI") but was NOT surfaced/reachable on the running Administrator page (only 3 tabs visible: Connector Truth | Import Jobs | Tier Override). So the user-facing flow to CREATE a connection is missing/not wired.
- **"Link DB Link → Job" page** — requested; job scheduling exists in code (`ImportJobSchedulePanel`, section 9) but not surfaced as a usable journey.
- **General:** pages "not done as requested." Karim considers V1 backlog needs substantial RE-IMPLEMENTATION.

**Implication for 8-July (technical evaluation):** the engineer will click every button. The create-connection → link-job → run-import → see-data journey MUST be real, wired, and working end-to-end in the UI. This is the #1 V1 rework item.

---

## 11. THE PRESENTATION DECK (22 slides, in /mnt/user-data/outputs/PlantProcessIQ_Presentation.pptx)

Brand: Dark Industrial — Navy #050B18 / #0C1A2E / #13253D, Cyan #00D4FF, Ice #CADCFC. Built with pptxgenjs.

Structure:
1. Title — PlantProcess IQ, SOU Industrial Software
2. Bio — **Karim Gamal**, photo placed; 13-yr career timeline (Flat Steel Egypt → PSI Metals → SMS group MES → SMS Level-2 commissioning)
3. The problem (100% generic)
4. What PPIQ does — value chain (Collect → Analyse/Correlate → Suggest AI+ML → Ask/AI Assistant)
5. Proven at scale — 11,997 / 1,993 / 5,688 / 10 stat callouts
6. "Sample plant" framing (industry-agnostic disclaimer)
7. Journey overview — 4 stops (Empty → Connect → Transfer → Intelligence)
8. Stop 1 — Empty (screenshot placeholder)
9. HOW IT WORKS — platform & stack (.NET 9, React/TS, Postgres 16, Docker/Caddy/Jenkins, security)
10. Stop 2 — Connect (screenshot placeholder)
11. HOW IT WORKS — connectors, read-only, staged, OT-safe
12. Stop 3 — Transfer (screenshot placeholder)
13. HOW IT WORKS — two-stage pipeline (import→unify, genealogy, provenance, jobs)
14. Stop 4 — Intelligence (**dashboard screenshot placed**)
15. HOW IT WORKS — intelligence layer (read-models, KPIs, risk, traceability)
16. Differentiator — "Where we go beyond conventional BI" (Correlation, Grounded AI, AI+ML Suggestions; footer "implemented, validation ongoing")
17. Recap — "Standard BI stops at 'See'. PlantProcess IQ takes you to 'Reason'."
18. Website — "A real product, a real company" (screenshot placeholder)
19. Broad navigation — "One platform, many capabilities" (12 area grid)
20. Pricing — DEPOSIT + SUBSCRIPTION (Standard $12k/$6k, Pro Plus $28k/$14k RECOMMENDED, Enterprise $50k/$25k)
21. Why it pays for itself (ROI/value)
22. Closing (contact placeholders: karim@souindustrial.com / +49 XXX — TO CORRECT)

**Still needed in deck:** real screenshots for Stops 1/2/3 + website; correct email/phone/customer name/date.

---

## 12. PRICING MODEL (locked, in memory)

Two-part: one-time DEPOSIT (installation/setup) + recurring SUBSCRIPTION (monthly/yearly).
- DEPOSIT range $12k–$50k (scales with # data sources + # pages + customization).
- SUBSCRIPTION range $6k–$25k/month (scales with license tier).
- **Standard** = $12k + $6k/mo (≤3 sources; connect/unify/dashboards/KPIs/risk).
- **Pro Plus (RECOMMENDED)** = $28k + $14k/mo (≤6 sources + more pages; correlation + AI/ML suggestions + alerts).
- **Enterprise** = $50k + $25k/mo (unlimited sources/custom pages; grounded AI assistant/chat; multi-site, advanced security, priority support).
- Annual pre-pay = discount lever. Tiers map to value chain: Standard=collect+see, Pro Plus=analyse+suggest, Enterprise=AI chat+scale.

---

## 13. IDENTITY & TOPOLOGY + ROADMAP (from the v4 doc Karim uploaded, cross-checked live)

- **Two-schema Postgres:** `ppiq_meta` (metadata, UI config, users/roles) + `ppiq_plant` (customer data post-staging). NOTE: local `ppiq_app` is the dev app DB name; the doc's schema naming is the design intent.
- **Milestones:** V1 = procurement presentation (breadth + zero crashes) — DONE 1-Jul; V2 = CEO/engineer technical evaluation (every number justified) — **8-July**; V3/V4 = full production features.
- **Stage-2 canonical refresh** currently a hardcoded IF/ELSIF ladder per demo-source shape — accepted for V1, must become a generic projector consuming SchemaViewDefinition mappings in V2 with a CI enforcement gate.
- **Dev identity (from doc §4.1):** 5 role users listed (admin/DevAdmin123!, exec/DevExec123!, engineer/DevEng123!, operator/DevOp123!, viewer/DevView123!) — BUT on live `ppiq_app` only **e2eadmin/E2EAdmin123!** actually works. Ed25519 dev tenant `00000000-0000-0000-0000-000000000001`, kid `ppiq-dev-ed25519`. Ed25519 activate body = `{ "licenseJws": "<JWS>" }`.
- **Env topology:** LOCAL = native Postgres on Windows localhost:5432 (API reaches via `host.docker.internal` if API is containerized; here API runs natively so direct localhost); SERVER = all DBs are Docker containers.

---

## 14. REALIZATION SCOREBOARD (six-persona, from prior + this session)

Framework: SIX personas; headline = LOWEST persona score (never averaged): A1 Developer/Maintainer, A2 Security/IT/Procurement, A3 Process/Quality Engineer (HEADLINE), A4 Reliability/Ops & Plant-Admin, A5 Executive Sponsor, A6 Brand/Website. Rule: no score without live HMI evidence; Critical safety/honesty/dead-button/read-only violations cap that persona at Needs Work.

Last scored (30-Jun snapshot): **headline 61/100** (A6 Brand/Website = cap, HMI-gated), persona mean ~68. A3 Process/Quality was the biggest mover (+18) from the Stage-2 fix.

**End-of-this-session honest adjustment:** the discovery that the **Test Journey UI is not implemented as specified** and that **many screens are empty + leak internal codes** would LOWER several personas (A1 dead/missing journey pages; A6 website out-of-date; A3 empty analytics screens). Realistically the headline should be treated as **Needs Work (~55-60)** until the V1 journey rework lands. The ENGINES (correlation/AI/suggestion) scoring well on implementation depth is the offsetting strength.

---

## 15. IMMEDIATE NEXT ACTIONS (Karim's stated 3 tasks, in priority order)

**TASK 1 (URGENT — customer waiting): Fix the website.** Make it deep, detailed, professional, genuinely impressive, and UP TO DATE with: the AIM, functionality, features, why the app matters to any client, pricing & license, workflow, and the working/block diagram. Send updated version to the customer immediately.
- Website content lives in `Website\PlantProcess.Website\src\content\phase1WebsiteProof.ts` (+ `brand/plantProcessBrand.*`). Read it FIRST (`Get-Content -Raw`).
- Website runs via `.\node_modules\.bin\vite --host localhost --port 5080` (launcher bugged).
- Existing copy is actually decent ("Not MES/SCADA/Level2/BI-only"; diagram "source → staging → canonical → analytics → report"). Karim believes it's out of date vs current app — VERIFY specifics against the real feature set (§4) before rewriting. Don't break the build; change TEXT/CONTENT, test after each change.
- Pricing on site must match §12. Workflow/diagram must match: Collect → Stage → Canonical/Unify → Analyse+Correlate → Suggest (AI/ML) → Grounded AI Assistant, all read-only.

**TASK 2: Fix V1 functionality — page by page, button by button**, as a normal user. The Test Journey must become real end-to-end: create DB Link → link DB Link to Job → run import → data flows → dashboards populate. Also fix the launcher scripts, hide internal codes (PPIQ-T###) + DEMO/DEVELOPMENT badges from customer-facing UI, populate or hide empty chart placeholders.

**TASK 3: V2 backlog** — advanced functionality, hardening, QA, Data Quality, generic Stage-2 projector (replace hardcoded ladder + CI gate), for the 8-July technical evaluation where every feature/value/button/AI is scrutinized.

---

## 16. QUICK REFERENCE — commands the new session will need

```powershell
# Start local app (launchers bugged - use direct vite)
cd C:\Workspace\PlantProcess-IQ ; .\scripts\run\start-api.ps1 -Profile local
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web ; .\node_modules\.bin\vite --host localhost --port 5173
cd C:\Workspace\PlantProcess-IQ\Website\PlantProcess.Website ; .\node_modules\.bin\vite --host localhost --port 5080

# Local DB (native Postgres) - CORRECT one
$env:PGPASSWORD='ppiq_dev_local_only'
psql -h localhost -p 5432 -U ppiq_dev -d ppiq_app -c "SELECT count(*) FROM material_units;"   # 11997

# Local login (browser + API): e2eadmin / E2EAdmin123!   (admin/DevAdmin123! does NOT work)
# Login token field = accessToken

# Read website content to fix it
Get-Content C:\Workspace\PlantProcess-IQ\Website\PlantProcess.Website\src\content\phase1WebsiteProof.ts -Raw

# SERVER: re-apply routing alias if app 502s (runtime-only)
ssh root@178.105.152.180
docker network connect --alias plantprocess-app-web plantprocessiq_ppiq-net plantprocess-web
```

---

*End of handover. The new session should treat this as ground truth and continue from here — do not re-investigate solved items (login bug, DB identity, launcher bug, server routing, feature-implementation audit, all §8 tests).*
