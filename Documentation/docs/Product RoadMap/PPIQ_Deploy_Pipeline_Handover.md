# PlantProcess IQ — Deploy Pipeline & Demo Server: COMPLETE SESSION HANDOVER

**Date:** 26 June 2026
**Author of this handover:** AI assistant (Claude), end of the "ppiq-deploy-pipeline-debug" marathon session
**For:** Karim Elsayed, solo founder/dev, SOU Industrial Software (Düsseldorf)
**Outcome of session:** **FULL GREEN PIPELINE + WORKING LOGINABLE UI.** The demo server is live, healthy, and presentation-ready.

---

## 0. HOW TO USE THIS DOCUMENT (read first)

This is a complete, self-contained handover so a **new chat session starts from where we ended — not from green-field**. It captures: every fix, every commit, every root cause, every test/query run and its result, every "tip & trick" learned, the current state of the server/pipeline/DB, the improved architecture, the identity/topology model, the realization scorecard status, the backlog, and the working rules/mandates that govern all PPIQ work.

**If you (the next session) are tempted to re-investigate or re-run tests: STOP and read the relevant section here first.** The diagnostics below already establish the live state. Do not wipe volumes, re-trigger pipelines, or re-query the DB to "find out" something already documented here.

**Critical first principle confirmed this session (do not violate):** Do NOT delete `/var/lib/ppiq-preserve/.env` casually. The generator reuses it to keep the Postgres password stable. Deleting it forces a new generated password that will NOT match the existing Postgres data volume → `28P01 password authentication failed`. If you must regenerate `.env`, you MUST also wipe the `ppiq-app_plantprocess-postgres-data` volume in the same operation so the fresh DB initializes with the new password.

---

## 1. SESSION OUTCOME — WHAT IS NOW TRUE (the "don't re-discover this" state)

### 1.1 Pipeline
- The Jenkins job `plantprocessiq-deploy` reaches **`PIPELINE GREEN` / `Finished: SUCCESS`**. Proven on build **#96** (commit `94b8fb4f`), then again after the frontend fixes (commit `ec165699`).
- Full stage flow that now passes: checkout + ensure-env (1) → sweep (2) → backend tests (3) → frontend tests (4) → e2e gated-off (5) → app DB migrate+seed (6) → demo sources gated-off (7) → build + recreate canonical stack + health gate (8) → presentation smoke: sysadmin login + Enterprise activation (9).

### 1.2 What the green run proves end-to-end
- Backend builds; ~567 backend tests pass; frontend 51 files / 202 tests pass.
- App DB migrates (EF) + post-EF SQL scripts + seeds apply cleanly.
- **Dev Ed25519 license public key registers** (kid `ppiq-dev-ed25519`) — only because `PPIQ_PRESENTATION=on`.
- Images build (`plantprocess-api:local`, `plantprocess-web:local`), stack recreates **in place** on the isolated `ppiq-app` project.
- **Health gate** `GET http://plantprocess-api:5063/health` returns 200 → `== DEPLOY GREEN ==`.
- **sysadmin auto-provisions** at API startup (empty DB → FirstRunProvisioning creates it).
- **Stage-9 smoke**: logs in as `sysadmin` (gets bearer), activates the Enterprise signed token, confirms `hasVerifiedLicense:true, tier:Enterprise`, prints `Presentation ready`.

### 1.3 Frontend / public URL
- The UI at `https://app.178.105.152.180.sslip.io` **loads and logs in** (sysadmin auto-login) after the final frontend build-arg + host-derivation fixes.
- Earlier it showed "Backend connection failed / Demo login is not configured" — that was a **frontend build-time** problem (VITE vars not baked in + wrong API base URL + CORS not allowing the sslip host). All fixed.

### 1.4 Demo credentials (sysadmin)
- Username: `sysadmin`. Password: generated per fresh `.env` and stored in `/var/lib/ppiq-preserve/FIRST_LOGIN.txt` inside the `ppiq-jenkins` container, and as `PPIQ_SMOKE_PASSWORD` in `deploy/compose/.env`.
- Retrieve with:
  ```bash
  docker exec ppiq-jenkins sh -lc 'cat /var/lib/ppiq-preserve/FIRST_LOGIN.txt'
  docker exec ppiq-jenkins sh -lc 'grep -E "PPIQ_SMOKE_USERNAME|PPIQ_SMOKE_PASSWORD" /var/jenkins_home/workspace/plantprocessiq-deploy/deploy/compose/.env'
  ```
- Note: the password we saw most of the session was `a6369fdf5a74407789d4291f0a407b5b`, but it **rotates whenever `.env` is regenerated**, so always read it live rather than trusting this value.

---

## 2. PERSON & PERMANENT WORKING RULES / MANDATES (apply every turn — non-negotiable)

Karim is the solo founder & sole developer of SOU Industrial Software (Düsseldorf), building **PlantProcess IQ (PPIQ)** — a read-only, evidence-grade process-to-quality intelligence platform for steel plants (MENA + EU), ~€100k/customer, each customer with their OWN environment. Repo: `github.com/kgamal369/PlantProcess-IQ`. Local Windows box: `C:\Workspace\PlantProcess-IQ`. Server: Hetzner VPS `178.105.152.180` (Ubuntu, root SSH). Jenkins: `https://jenkins.178.105.152.180.sslip.io/`.

### 2.1 Working-style mandate (HONOR ALWAYS)
- **PowerShell ONLY for Karim's WINDOWS box** (never bash there). The assistant emits `& { ... }` PS5.1 paste-blocks; **pure ASCII**; UTF-8 **no-BOM** via `[System.IO.File]::WriteAllText`; **LF** line endings for `.sh` files; **backup-first** to `deploy\.ppiq-backups\<name>-<timestamp>\`; anchored edits with restore-on-miss; commits ONLY behind `$env:PPIQ_COMMIT='1'`; an explicit file list per commit. Server-side ops on the Ubuntu host ARE bash (correct there).
- **Zero preamble, no flattery, honest defect surfacing, complete copy-paste-ready deliverables only.**
- PowerShell 5.1 constraints: no PS7 ternary, no `&&`; cuddled `} else {`; no em-dashes or curly quotes in code; here-strings terminate at column 0; statically check every paste-block for non-ASCII, brace/paren/bracket balance, and here-string terminator.

### 2.2 Solution Doctrine (apply to ALL PPIQ work)
Never give temporary / runtime / per-machine fixes. Every fix MUST be permanent, committed, and generic so it validates identically across many customer installs (not just Karim's machine). Each solution must: (1) finish the task to its written description + validation/acceptance — not just stop the error; (2) leave the software cleaner, more stable, better-structured, more generic — product-grade; (3) follow roadmap definitions; (4) provide permanent committed per-level run commands (dev/test). **"Make it green" is NOT the goal** — converting PPIQ into a stable, generic, sellable product is. If a fix would have to be repeated on every machine, design a different generic concept instead.

### 2.3 Autonomous Generic-Fix Mandate
Without asking permission, diagnose and solve EVERY bug/error/failing test at the SOURCE in a generic, professional, design-level way. NEVER make a test/error go away by: adding/clearing a local env var, passing creds on the command line per run, skipping/disabling/xfail-ing a test, loosening or deleting an assertion, suppressing a real error, or any per-machine workaround. If a test fails, determine whether the CODE is wrong (fix the product) or the TEST is genuinely wrong (fix the test's logic only if it tests the wrong thing — never to mask a defect). Brittle English-keyword/locale-specific checks are themselves defects (product sold in EN/DE/AR) — replace with structural, i18n-safe mechanisms.

### 2.4 Preventive-Maintenance Mandate (the lesson this session drove home hardest)
**NEVER wait for a failure to occur. Predict and trace the ENTIRE path up front.** Enumerate every workflow/stage/branch, statically walk through what will happen at each step by reading the ACTUAL files end-to-end (compose, Jenkinsfile, scripts, Dockerfiles, env, Caddy, etc.), cross-check for all mismatches (names, ports, networks, ordering, missing build context/files, sequencing), and surface ALL defects in one complete pass BEFORE running. Present the full predicted failure map, not a single next error. **This is what finally cracked the last failures** — the static "pre-flight" trace caught (a) the Jenkins workspace being a commit behind, and (b) the missing `PPIQ_PRESENTATION` env propagation — *before* another red run, instead of after.

> **Process honesty for the next session:** This session took ~18 pipeline iterations. The deploy itself went green early; the long tail was the **stage-9 auth/license/frontend chain**, where each layer only becomes reachable once the previous one passes (networking → permissions → login → token field → key registration → env-var propagation → frontend build args). That sequential reveal is *inherent* to a smoke that short-circuits on first failure — BUT it could have been compressed dramatically by doing the full static trace of the whole stage-9 chain up front. **Do that next time: read the entire downstream path before triggering.**

### 2.5 Naming Golden Rule
NEVER name any artifact using phase numbers, task numbers, milestone/sprint IDs, or codes like `P02`, `phase2`, `T053`, `P01P02`, `p03_p04`, `v5 phase`, `hotfix`, etc. Names must be DESCRIPTIVE of function/purpose only. Exception: a leading numeric prefix that purely controls execution order (ordered SQL migration scripts; EF migration timestamps) is a functional ordering token — preserve the ordering prefix but strip embedded phase/task labels and make the rest descriptive (e.g. `310_p03_p04_mapping_genealogy_foundation.sql` → `310_mapping_genealogy_foundation.sql`). **NOTE: there is still tech debt here** — files like `301_p01_p02_authstore_compatibility_bridge.sql`, `300_p01_p02_security_access_control_spine.sql`, `302_p02_authstore_runtime_lineage_lock.sql`, `500_v5_p01_*`, `510_v5_p02_*` still carry phase labels. Rename them descriptively in a future cleanup pass (and update all references).

### 2.6 Clean-Rebuild Direction + Single Canonical Identity Topology
Schema defined in C# (entity + IEntityTypeConfiguration), migrations generated via `dotnet ef migrations add` (never hand-written raw CREATE TABLE), ModelSnapshot in sync; DB-only constructs (append-only audit triggers, etc.) live in thin separate SQL migrations. ONE canonical, committed, documented identity & container topology — no transient env vars to boot, no raw CREATE TABLE without an EF migration, no hardcoded creds to "go green."

---

## 3. THE ADMIN / IDENTITY GOLDEN RULE (saved to memory this session — STRICTLY TWO ADMIN TYPES, never conflate)

This is the single most important architectural decision Karim made this session.

1. **`sysadmin` = System Owner / Support account.**
   - PERMANENT and UNDELETABLE. Created from the very beginning during automated system install/launch.
   - The **FirstRunProvisioning path provisions THIS account ONLY.**
   - Strictly for Karim's / SOU's team for on-call support & troubleshooting.
   - **The customer must NEVER use or see `sysadmin`.** Mark it internally as undeletable/protected.
   - This is the account the deploy pipeline auto-provisions, and the stage-9 presentation smoke logs in as.
   - It is `is_owner=true` in `app_users`; there is NO delete-user API path, so it is effectively undeletable.

2. **"Customer Admin" (Tenant Admin) = the normal admin for the client.**
   - Named `admin` or the company name or anything.
   - **NOT created during the automated pipeline/system install.**
   - Inserted **MANUALLY LATER** during the early commissioning phase to configure data sources and build/configure UI pages.

So: `FirstRunProvisioningHostedService` (and any auto-provisioning) provisions **ONLY** the permanent `sysadmin`; customer/tenant admins are a separate, later, **manual** commissioning step. **Never auto-create a customer-named admin during install.**

---

## 4. SERVER & PIPELINE TOPOLOGY (verified live this session — reuse, do not re-discover)

### 4.1 Two Docker Compose projects (CRITICAL distinction)
- **`plantprocessiq`** = the INFRASTRUCTURE project (SACRED — never reaped). From `/opt/PlantProcess-IQ/Infrastructure/deploy/docker-compose.demo.yml`. Owns:
  - `ppiq-jenkins` (Jenkins, Docker-out-of-Docker via mounted `/var/run/docker.sock`)
  - `ppiq-caddy` (binds 0.0.0.0:80/443, fronts EVERYTHING including `jenkins.*.sslip.io`, `app.*`, `api.*`, `website.*`)
  - `ppiq-backup-runner`
  - Network: `plantprocessiq_ppiq-net`
- **`ppiq-app`** = the APPLICATION DEPLOY project (renamed FROM `plantprocessiq` this lineage — see §6 history). Creates:
  - `plantprocess-postgres` (main app DB container; volume `ppiq-app_plantprocess-postgres-data`)
  - `plantprocess-api` (listens :5063)
  - `plantprocess-web` (nginx serving the Vite build, :80)
  - Network: `ppiq-app_plantprocess-private`
  - api + web ALSO joined to external network alias `ppiq-edge` → `plantprocessiq_ppiq-net`, so the infra Caddy can reach them by container name.

**WHY the rename mattered (THE big bug, earlier lineage):** when the app deploy used project name `plantprocessiq` (same as infra), `deploy-canonical.sh` ran `docker compose -p plantprocessiq up -d --remove-orphans`, which **reaped ppiq-jenkins / ppiq-caddy / ppiq-backup-runner** (Jenkins exited 143 mid-deploy → 502). Renaming the deploy project to `ppiq-app` means `--remove-orphans` can never touch infra. **Never merge these two projects again.**

### 4.2 Jenkins agent capabilities (DooD)
- Jenkins runs INSIDE container `ppiq-jenkins`. The agent itself has **NO dotnet, NO node, NO npm.**
- Tool steps run in SIBLING containers via:
  `docker run --rm --volumes-from $(cat /etc/hostname) -w "${PWD}" <image> sh -lc "..."`
  (`--volumes-from` inherits the workspace at identical paths).
- DooD path trap: bind-mount SOURCES resolve on the HOST daemon. Container path `/var/jenkins_home/...` does NOT exist on host (host-real is `/data/plantprocess-iq/jenkins/...`). The `--volumes-from` pattern avoids this.
- `node:24-alpine` and `curlimages/curl:8.10.1` have **busybox `sh` only** (NO bash → always `sh -lc`, never `bash -lc`). busybox DOES include `sed`, `cat`, `tr`, `base64`.
- Workspace path: `/var/jenkins_home/workspace/plantprocessiq-deploy`.

### 4.3 Environment topology (DB hosting)
- **LOCAL laptop:** main PlantProcess Postgres DB is installed NATIVELY on Windows (`localhost:5432`, NOT a container). Only demo/source-emulation DBs are Docker containers; the containerized API reaches the main DB via `Host=host.docker.internal`.
- **SERVER:** ALL DBs are Docker containers (main = `plantprocess-postgres` in the `ppiq-app` project). DB connection targets must ALWAYS be env-configurable, never hardcoded.

### 4.4 Caddy (infra) routing — and a KNOWN tech-debt issue
The live infra `ppiq-caddy` Caddyfile routes (confirmed via `docker exec ppiq-caddy cat /etc/caddy/Caddyfile`):
```
app.178.105.152.180.sslip.io       -> plantprocess-app-web:80      <-- STALE NAME (see below)
api.178.105.152.180.sslip.io       -> plantprocess-api:5063        <-- correct
website.178.105.152.180.sslip.io   -> plantprocess-website:80      <-- STALE NAME
jenkins.178.105.152.180.sslip.io   -> jenkins:8080
:80                                 -> plantprocess-app-web:80      <-- STALE NAME
```
**TECH DEBT / KNOWN ISSUE:** the live Caddyfile still references `plantprocess-app-web` and `plantprocess-website`, but the actual running containers are `plantprocess-web` and `plantprocess-api` only. Despite this, `app.*` currently returns HTTP 200 and the UI loads (Caddy is resolving it via the shared `ppiq-edge`/network aliasing, or a stale-but-working route). **It works now but is fragile.** Earlier in the broader lineage we noted the infra Caddyfile is an "orphaned inode" — the bind source `/opt/PlantProcess-IQ/Infrastructure/deploy/Caddyfile` was deleted on the host but the running container keeps serving the in-namespace copy; edits in place fail with "Resource busy", and hot-reloads via `caddy reload --config /etc/caddy/Caddyfile.new` are NOT persistent across container recreation.
- **PENDING follow-up (not done):** re-establish a persistent host bind for the infra Caddyfile, and correct the route targets to `plantprocess-web` / (website container name) so they survive a Caddy recreate. Until then, do NOT recreate `ppiq-caddy` casually — it may come back with broken routes.

### 4.5 The API external route returns 401 on /health but that's fine
External test results captured this session:
- `https://api.178.105.152.180.sslip.io/health` → HTTP 401 (external), while the **internal** `http://plantprocess-api:5063/health` → 200 (the deploy gate uses internal and passes). The external 401 did not block anything; `POST https://api.178.105.152.180.sslip.io/auth/login` with bad creds → 401 (i.e. it REACHED the API and rejected creds), confirming the public API path works for login. The browser logs in fine.
- `https://178.105.152.180.sslip.io/health` (bare host) → HTTP 000 (no route on bare host for that path; the bare `:80` serves the web app).

---

## 5. THE JENKINS PIPELINE — STAGE-BY-STAGE (current, working)

`Jenkinsfile` env block (key values):
```
COMPOSE_PROJECT = 'ppiq-app'
COMPOSE_BASE    = 'deploy/compose/docker-compose.yml'
COMPOSE_SERVER  = 'deploy/compose/docker-compose.server.yml'
ENV_FILE        = 'deploy/compose/.env'          # git-ignored runtime secrets
INFRA_PROJ      = 'Backend/PlantProcess.Infrastructure'
API_PROJ        = 'Backend/PlantProcess.Api'
FRONTEND_DIR    = 'Frontend/PlantProcess.Web'
```

Stages:
1. **Checkout + ensure-env** — checks out latest `origin/main`; runs `deploy/scripts/ensure-runtime-env.sh <ENV_FILE> <PRESERVE_DIR> env/profiles/server.env.example` to materialize `deploy/compose/.env`.
2. **Sweep** — workspace hygiene.
3. **Backend tests (BLOCKING)** — runs in an SDK sibling (`mcr.microsoft.com/dotnet/sdk:9.0`) via `ci-test-db.sh`/`dotnet test`. ~567 tests, many `[SKIP]` (they're gated to a running API / TestMode). Truth-gate tests live here.
4. **Frontend unit tests (BLOCKING)** — `docker run --rm --volumes-from <self> -w <FRONTEND_DIR> node:24-alpine sh -lc "set -e; npm ci; npm run test"`. 51 files / 202 tests.
5. **Frontend e2e** — gated OFF by default (`when` expression checks `PPIQ_RUN_E2E == on`; it's `off`). Skipped.
6. **App DB: EF migrate -> post-EF SQL -> seed** — sources `.env` (`set -a; . "${ENV_FILE}"; set +a`), brings up ONLY `plantprocess-postgres`, then `bash deploy/scripts/migrate-and-seed.sh --app-only`.
7. **Demo sources migrate+seed** — gated by `PPIQ_DEMO_SOURCES_MODE` (currently `disabled`). Skipped.
8. **Build + recreate canonical stack** — `bash deploy/scripts/deploy-canonical.sh`: tags current images as `:previous` (rollback anchors), `dc build`, `dc up -d --remove-orphans`, then the **health gate**.
9. **Presentation defaults (Enterprise + admin smoke)** — `when` expression sources `.env` and checks `PPIQ_PRESENTATION` (defaults `on` in the `when`); runs the smoke in a curl sibling.

### 5.1 deploy-canonical.sh key facts
- `dc()` = `docker compose -p "${COMPOSE_PROJECT}" --env-file "${ENV_FILE}" -f "${COMPOSE_BASE}" -f "${COMPOSE_SERVER}" "$@"`.
- `dc build` then `dc up -d --remove-orphans`. **Because it uses `--env-file .env`, the compose `build.args` (`VITE_*: ${VITE_*}`) are populated from `.env` at build time** — this is what makes the frontend build pick up the VITE values.
- Health gate: `HEALTH_NETWORK="${COMPOSE_PROJECT}_plantprocess-private"`, `HEALTH_TARGET="http://plantprocess-api:5063/health"`, runs `docker run --rm --network "$HEALTH_NETWORK" curlimages/curl:8.10.1 -ks -o /dev/null -w '%{http_code}' "$HEALTH_TARGET"`, requires HTTP 200, 45 retries, else `!! HEALTH GATE FAILED - rolling back to :previous`.

### 5.2 migrate-and-seed.sh key facts
- Sources `.env`: line ~35 `[ -f "${ENV_FILE}" ] && set -a && . "${ENV_FILE}" && set +a`.
- `DB_CONTAINER=${DB_CONTAINER:-plantprocess-postgres}`, `DB_USER=${POSTGRES_USER:?...}`, `DB_NAME=${POSTGRES_DB:?...}`.
- `psql_in() { docker exec -i "${DB_CONTAINER}" psql -v ON_ERROR_STOP=1 -U "${DB_USER}" -d "${DB_NAME}"; }`.
- Order: (1) EF migrations generated in an SDK sibling then applied; (2) apply `Backend/database/scripts/*.sql` (post-EF decoration, idempotent); (3) apply `Backend/database/seed/*.sql` (skipping `dev_ed25519_public_key.sql` from the plain pipe — it's `psql -v` driven); (4) register the dev Ed25519 key.
- **The dev-key registration gate (FIXED this session):**
  ```bash
  if { [ "${ASPNETCORE_ENVIRONMENT:-}" != "Production" ] || [ "${PPIQ_PRESENTATION:-off}" = "on" ]; } \
     && [ -f "${KEYSQL}" ] && [ -f "${DEV_PUB_FILE}" ]; then
     # register kid=ppiq-dev-ed25519 via: docker exec -i $DB_CONTAINER psql ... -v tenant_id=... -v key_id=... -v public_key_b64=... < $KEYSQL
  ```
  KEYSQL = `Backend/database/seed/dev_ed25519_public_key.sql`; DEV_PUB_FILE = `deploy/fixtures/license/dev_public.b64`; DEV_TENANT default `00000000-0000-0000-0000-000000000001`; DEV_KID default `ppiq-dev-ed25519`.


## 6. THE COMPLETE DEBUG CHAIN — EVERY RED AND ITS FIX (chronological, with commits)

This is the full forensic record. Each item: symptom → root cause → fix → commit. **Do not re-investigate these — they are solved and committed.**

### Commits this session (chronological, on `main`):
```
1287999d  api: health endpoints AllowAnonymous (global FallbackPolicy RequireAuthenticatedUser was 401ing /health)
afd929a9  deploy: env_file -> load full generated .env into plantprocess-api (seeded admin reaches container)
d79c381d  deploy(Option 1): drop app-stack Caddy; join api+web to infra edge network
(7c2ad377) api: /health,/db-health,/health/ready -> anonymous in AccessControlMiddleware matrix
efeb19bb  ci: extract stage-9 presentation smoke to deploy/scripts/presentation-smoke.sh, run in curl sibling on app net
3071b84a  ci: run stage-9 smoke sibling as root (--user 0:0) to read root-owned .env + token fixture
7864f43d  auth: auto-provision sysadmin from config at first run (FirstRunProvisioningHostedService.StartAsync rewrite)
a3aad25d  auth: register FirstRunProvisioningHostedService via AddHostedService (Program.cs)
4c520f58  deploy: setkv quotes .env values with spaces  (LATER REVERTED in 73db22f3 lineage; see below)
2cbd057b  db: stop seeding admin/e2eadmin test users in 301_authstore_compatibility_bridge (kept tenant seed)
e0e0e3dc  ci: presentation smoke extracts accessToken (was 'token') from login response
73db22f3  licensing(demo): register dev Ed25519 key when PPIQ_PRESENTATION=on; smoke activation field -> licenseJws
        (this commit also reverted the setkv-quoting and set DisplayName space-free)
94b8fb4f  deploy: generate PPIQ_PRESENTATION=on in .env (gate + smoke agree)   <-- FIRST GREEN at build #96
ec165699  frontend(demo): bake VITE_SMOKE_* + correct VITE_API_BASE_URL into web build; host-derived URLs+CORS  <-- UI works
```

### 6.1 [FIXED] Phase2RealismSourceSeedTests.FindRepoRoot()
- Symptom: test looked for sibling dirs Backend+Infrastructure that never coexist.
- Fix: honor `PPIQ_REPO_ROOT`/CWD then marker (Backend + deploy). File: `Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Demo/Phase2RealismSourceSeedTests.cs`.

### 6.2 [FIXED] Stage-4 frontend `&&` leaked to dotless agent (exit 127)
- Symptom: `sh -lc 'npm ci && npm run test'` — single quotes stripped by Jenkins → `&&` reached the dotless agent.
- Failed attempts: heredoc (`sh -s` got empty stdin), `bash -lc` (alpine has no bash).
- FINAL FIX: `docker run ... node:24-alpine sh -lc "set -e; npm ci; npm run test"` (whole command ONE double-quoted string).

### 6.3 [FIXED] migrate-and-seed EF generation + dev-key seed
- Agent has no dotnet → EF generation moved into SDK sibling. Seed loop skips the `-v`-only `dev_ed25519_public_key.sql` and registers the dev key separately via `psql -v`.

### 6.4 [FIXED] UTF-8 BOM in env template
- Symptom: BOM (`ef bb bf`) in `env/profiles/server.env.example` copied into generated `.env`, broke `. .env` sourcing ("﻿PPIQ_PROFILE not found").
- Fix: template rewritten no-BOM + generator hardened with `sed -i '1s/^\xEF\xBB\xBF//'`.

### 6.5 [FIXED] Jenkinsfile Groovy parse error
- Symptom: a comment inside an `sh '''...'''` block contained `'''` + parens → closed the Groovy string.
- LESSON (standing check): comments inside `sh '''...'''` must be plain ASCII prose — NO `'''`, NO parens, NO apostrophes. Before any Jenkinsfile push, scan for `'''`: every match must be ONLY an `sh '''` opener or a bare `'''` closer on its own line.

### 6.6 [FIXED — THE BIG ONE] Project-name collision reaping Jenkins
- See §4.1. Renamed deploy project `plantprocessiq` → `ppiq-app` in Jenkinsfile, `deploy/compose/docker-compose.yml` (`name: ppiq-app`), `deploy/scripts/deploy-canonical.sh`.

### 6.7 [FIXED] Container name collision
- New deploy created `plantprocess-api/web` whose names existed in an old `ppiq-demo` stack (Docker names are global). Retired the old stack: `docker compose -p ppiq-demo -f /opt/PlantProcess-IQ/deploy/compose/docker-compose.demo.yml down`.

### 6.8 [FIXED] Caddy port/mount conflict — Option 1 chosen
- The app deploy's own Caddy wanted 80/443 (conflict with infra) and its Caddyfile bind-mount failed (DooD: host auto-created a directory). DECISION (Option 1): DROP Caddy from the app deploy; the long-lived infra `ppiq-caddy` fronts BOTH app and Jenkins. api+web get `restart: unless-stopped` and join networks `plantprocess-private` AND `ppiq-edge`.

### 6.9 [FIXED] env_file propagation
- Symptom: generated `.env` admin keys never reached the API container (compose only injects keys listed under `environment:`).
- Fix: added `env_file: [.env]` to `plantprocess-api` so the WHOLE `.env` reaches the container. Fixed the `StartupConfigurationValidator` "requires real configured admin" crash-loop (env count 0→4).

### 6.10 [FIXED] HEALTH GATE 401 — TWO LAYERS (important: BOTH were needed)
- **Layer A (authorization policy):** global `FallbackPolicy.RequireAuthenticatedUser()` (Program.cs ~569) caught `/health`. Fix: `.AllowAnonymous()` on the health MapGroup in `Backend/PlantProcess.Api/Endpoints/Health/HealthEndpoints.cs` (commit 1287999d).
- **Layer B (pre-routing middleware) — the REAL 401:** `AccessControlMiddleware` (`Backend/PlantProcess.Api/Security/PlantAccessControl.cs`) has a `Matrix` of `(Prefix, Methods, Permission, Anonymous)`. `/health`, `/db-health`, `/health/ready` were mapped `"assistant.use", false` (Anonymous=false) → 401 before routing ever evaluated `AllowAnonymous`. Fix: change those three to `"anonymous", true` (commit 7c2ad377).
- **KEY INSIGHT:** endpoint-level `AllowAnonymous` cannot help when a middleware runs BEFORE endpoint routing and short-circuits. The `AdminMfaRequirementMiddleware` already path-exempts `/health`; the access-control matrix did not. Middleware order (Program.cs): SecurityHeaders → (UseCors) → CorrelationId → ExceptionHandler → RequestResponseLogging → RateLimiter → **UseAuthentication → UsePlantProcessAccessControl (AccessControlMiddleware) → UseAuthorization → TenantContextMiddleware → AdminMfaRequirementMiddleware → AuditLog → endpoints**.

### 6.11 [FIXED] Stage-9 smoke networking (exit 7)
- Symptom: smoke curled `http://127.0.0.1:5063` from the dotless agent → exit 7 (API is in the `plantprocess-api` container on the app network, not on the agent's loopback).
- Fix: extracted smoke to `deploy/scripts/presentation-smoke.sh`; stage 9 runs it in a curl sibling: `docker run --rm --user 0:0 --network ppiq-app_plantprocess-private --volumes-from $(cat /etc/hostname) -w ${PWD} -e ENV_FILE=... curlimages/curl:8.10.1 sh ./deploy/scripts/presentation-smoke.sh`. Targets `http://plantprocess-api:5063` (commit efeb19bb).

### 6.12 [FIXED] Stage-9 smoke permission denied (exit 2)
- Symptom: `can't open './deploy/compose/.env': Permission denied` — `curlimages/curl` runs as non-root (uid 100), `.env` is root-owned.
- Fix: added `--user 0:0` to the smoke sibling (commit 3071b84a).

### 6.13 [FIXED] Login 401 — the auth-provisioning saga (multi-part)
This was the deepest investigation. The full chain of discovery:
- **Login is DB-backed, NOT config-backed.** `AuthEndpoints.LoginAsync` calls `AuthStore.ValidateUserAsync(userName, password)`, which reads the `app_users` table (line ~102 `password_hash = reader.GetString(...)`) and verifies with `PasswordHasher.Verify` (Argon2id / pbkdf2). Config `PlantProcess:Auth:Users` only satisfies the `StartupConfigurationValidator`; login ignores it. A dev-fallback (`ResolveDevelopmentUser`) exists ONLY in `IsDevelopment()` (we're Production → no fallback).
- **The configured admin (`ppiq-owner`) was never in `app_users`** → login 401.
- **`FirstRunProvisioningHostedService` existed but was NEVER REGISTERED** (`Program.cs` had `FirstRunProvisioningState` as a singleton at ~215, but no `AddHostedService<FirstRunProvisioningHostedService>()`). And even its original `StartAsync` only generated a one-time manual-claim TOKEN (logged) and waited for `POST /auth/provisioning/claim` — it did NOT auto-create an owner.
- **`app_users` was polluted by test users `admin`/`e2eadmin`** with `is_owner=true`. These made `HasAnyUserAsync()` return true → provisioning (when later wired) would skip. Their password hashes were initially `test-seed-placeholder` (from `Backend/database/test-seeds/900_clean_test_auth_seed.sql`, which is correctly NOT in the production seed path) BUT also re-seeded every deploy by a PRODUCTION script `Backend/database/scripts/301_p01_p02_authstore_compatibility_bridge.sql` (real Argon2 hashes, unknown plaintext). `admin/DevAdmin123!` → 401 confirmed; these users are unusable.
- **FIXES (per the two-admin golden rule):**
  1. Rewrote `FirstRunProvisioningHostedService.StartAsync`: on empty DB, read the first `AuthOptions.Users` entry and call `store.CreateOwnerAsync(userName, password, displayName)` to create the permanent owner; keep token-generation only as a fallback. (commit 7864f43d)
  2. Registered it: `builder.Services.AddHostedService<PlantProcess.Api.Security.FirstRunProvisioningHostedService>();` at Program.cs ~314. (commit a3aad25d)
  3. Renamed the provisioned account to **`sysadmin`** in the generator (`AU="sysadmin"`, added `DisplayName`), pointed `PPIQ_SMOKE_*` at it.
  4. **Removed the `admin`/`e2eadmin` INSERT block from the production script `301_..._authstore_compatibility_bridge.sql`** — KEPT the table DDL and the canonical tenant seed (tenant `00000000-...-001`, REQUIRED for the `CreateOwnerAsync` FK). (commit 2cbd057b)
- `CreateOwnerAsync` (`AuthStore.cs` ~140): takes `(userName, password, displayName)`, hashes Argon2id, inserts into `app_users` with tenant `00000000-...-001`, `plant_role='TenantOwner'`, `compatibility_role='Admin'`, `is_owner=true`, `force_password_change=true`. Calls `EnsureOpenAsync` first (requires empty DB).

### 6.14 [FIXED] Smoke login token extraction (the accessToken field)
- Symptom: login SUCCEEDED server-side ("Login succeeded for sysadmin") but smoke reported `FATAL: admin login returned no token`.
- Root cause: the API returns `{"accessToken":"..."}` (the `LoginResponse` record's `AccessToken` → camelCased), but the smoke's `sed` extracted `"token"`.
- Fix: smoke `sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p'` (commit e0e0e3dc).

### 6.15 [FIXED] Enterprise activation 400 — field name
- Symptom: activation `POST /api/v5/licensing/ed25519/activate` → HTTP 400 `{"activated":false,"status":"invalid_payload","error":"Invalid compact JWS header."}`.
- Root cause: the request DTO `Ed25519ActivateLicenseRequest` (in `Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs`) has field **`LicenseJws`** (→ `licenseJws`), but the smoke sent `{"token":"..."}`. `request.LicenseJws` was null → `TryReadHeader(null)` → "Invalid compact JWS header."
- Fix: smoke body `{"licenseJws":"${JWS}"}` (commit 73db22f3).

### 6.16 [FIXED] Enterprise activation 400 — dev key not registered (THE PRESENTATION FLAG)
- Symptom: even with `licenseJws`, activation would 400 because `ppiq_ed25519_license_public_keys` was EMPTY (0 rows).
- Root cause chain:
  - The dev-key registration in `migrate-and-seed.sh` was gated `[ "${ASPNETCORE_ENVIRONMENT:-}" != "Production" ]` → skipped in Production.
  - We changed the gate to ALSO allow when `PPIQ_PRESENTATION=on` (commit 73db22f3).
  - BUT `PPIQ_PRESENTATION` was **never written to `.env` by the generator** → `${PPIQ_PRESENTATION:-off}` in migrate-and-seed defaulted OFF → still skipped. (Meanwhile the stage-9 `when` used `${PPIQ_PRESENTATION:-on}` → defaulted ON → inconsistent.)
  - **Final fix:** generator writes `setkv PPIQ_PRESENTATION "on"` (commit 94b8fb4f). Now both consumers agree, the key registers in stage 6, and activation succeeds → **FIRST GREEN (build #96).**
- The token fixture `deploy/fixtures/license/enterprise.token` is a valid compact JWS: header `{"alg":"EdDSA","typ":"license+jws","kid":"ppiq-dev-ed25519"}`, payload `{"tenantId":"00000000-...-001","licenseKey":"PPIQ-DEV-ENTERPRISE","tier":"Enterprise","issuedAtUtc":"2026-06-16...","expiresAtUtc":"2027-06-16...","features":[],"limits":{}}`. The dev public key (45-byte b64) is `deploy/fixtures/license/dev_public.b64`.

### 6.17 [FIXED] .env sourcing "System: not found" (exit 127)
- Symptom: stage 5 died with `deploy/compose/.env: 42: ... System: not found`.
- Root cause: the generated `.env` had `PlantProcess__Auth__Users__0__DisplayName=PPIQ System Administrator` — an **unquoted value with spaces**. `. .env` tried to run `System` as a command.
- Considered fix A: make `setkv` quote values with spaces (briefly applied as commit 4c520f58). REJECTED because docker-compose `env_file` does NOT strip quotes the way the shell does — quoting the connection string would corrupt it.
- **Final fix:** make `DisplayName` space-free: `PPIQ-System-Administrator`, and revert setkv to simple form (commit 73db22f3 lineage). The `.env` then sources cleanly with no quoting.

### 6.18 [FIXED] DB password divergence (28P01) — the volume/.env coupling
- Symptom: after deleting `.env` to force the sysadmin rename, the API crash-looped at startup with `28P01: password authentication failed for user "plantprocess"`.
- Root cause: regenerating `.env` created a NEW `POSTGRES_PASSWORD`, but the existing Postgres data volume still had the OLD password (Postgres only sets the password on FIRST volume init). They diverged.
- Fix (operational): wipe the `ppiq-app_plantprocess-postgres-data` volume so a fresh DB inits with the new `.env` password. Both come from the SAME deploy run → they match.
- **PERMANENT RULE (now a working rule):** the generator (`ensure-runtime-env.sh`) ALREADY preserves the password correctly — line 6 reuses the persisted `.env` if it has the validation keys (`PPIQ_API_UPSTREAM`, `PPIQ_DEMO_SOURCES_MODE`). The divergence only happens when you DELETE the persisted `.env`. **So: do NOT delete `/var/lib/ppiq-preserve/.env`; if you must, wipe the Postgres volume in the same step.**
- KNOWN MINOR TECH DEBT: line 7 of the generator ("persisted .env stale → regenerate") rotates the password when keys change between versions. A future hardening should preserve `POSTGRES_PASSWORD` across regen. Not blocking.

### 6.19 [FIXED] Frontend "Backend connection failed / Demo login not configured" (the final UI layer)
FOUR sub-defects, all fixed in commit ec165699:
- **(a) VITE_SMOKE_* never baked in.** Vite inlines `VITE_*` at BUILD time. The web `Dockerfile` only had `ARG/ENV VITE_API_BASE_URL`; no `VITE_SMOKE_USERNAME`/`VITE_SMOKE_PASSWORD`. The compose `build.args` only passed `VITE_API_BASE_URL`. So the bundle's `AuthContext` (`/usr/share/nginx/html/assets/AuthContext-*.js` checks exactly `VITE_SMOKE_USERNAME`) saw empty → "Demo login is not configured." FIX: added the two `ARG/ENV` lines to the Dockerfile (before `RUN npm run build`) AND the two build args to compose.
- **(b) Wrong VITE_API_BASE_URL.** Was `https://api.plantprocessiq.com` (from the template), which does not resolve to this server. Must be `https://api.178.105.152.180.sslip.io`.
- **(c) CORS would block the browser.** `PLANTPROCESS_ALLOWED_ORIGINS` was `https://app.plantprocessiq.com,...` — did not include the sslip host.
- **(d) Generic host derivation.** FIX (doctrine-correct): generator now derives ALL public URLs from one variable `PUBLIC_HOST="${PPIQ_SITE_HOST:-178.105.152.180.sslip.io}"`:
  ```bash
  setkv SITE_HOST "${PUBLIC_HOST}"; setkv WEBSITE_HOST "website.${PUBLIC_HOST}"
  setkv VITE_API_BASE_URL "https://api.${PUBLIC_HOST}"
  setkv VITE_WEBSITE_API_BASE_URL "https://api.${PUBLIC_HOST}"
  setkv PLANTPROCESS_ALLOWED_ORIGINS "https://app.${PUBLIC_HOST},https://${PUBLIC_HOST},https://website.${PUBLIC_HOST}"
  ```
  Any customer overrides `PPIQ_SITE_HOST`; all URLs + CORS follow.
- **Browser caching note:** after the rebuild, hard-refresh (Ctrl+Shift+R) — the old broken bundle may be cached client-side.

### 6.20 [FIXED, pre-flight catch] Jenkins workspace a commit behind
- Before the green run, the static pre-flight found the Jenkins workspace HEAD was `73db22f3`, NOT the just-pushed `94b8fb4f` → the generator in the workspace lacked `PPIQ_PRESENTATION`. The workspace updates on fresh checkout at build start, so triggering pulls it — but we VERIFIED `origin/main` had the flag (`git show origin/main:deploy/scripts/ensure-runtime-env.sh | grep PPIQ_PRESENTATION` → present) BEFORE triggering. This is the preventive-maintenance approach paying off.


## 7. EVERY TEST / QUERY RUN THIS SESSION AND ITS RESULT (so the next session does NOT re-run them)

### 7.1 Pipeline runs (logs _87 through _96)
| Run | Result | Where it died | Cause (fixed by) |
|---|---|---|---|
| _87 | FAIL exit 7 | stage 9 smoke | curl to 127.0.0.1 (efeb19bb networking) |
| _88 | FAIL exit 2 | stage 9 smoke | permission denied on .env (3071b84a --user 0:0) |
| _89 | FAIL exit 1 | stage 9 login | login 401 — sysadmin not in DB yet (provisioning saga) |
| _90 | FAIL exit 127 | stage 5 | unquoted DisplayName space (→ space-free name) |
| _91 | FAIL exit 1 | stage 9 login | accessToken field (e0e0e3dc) |
| _92 | FAIL exit 1 | stage 9 login | 301 re-seeded admin/e2eadmin → provisioning skipped (2cbd057b) |
| _93 | FAIL exit 1 | stage 9 login→activate | provisioning OK, but activation field `token` not `licenseJws` |
| _94 | FAIL exit 22 | stage 9 activate | 400 invalid JWS header (licenseJws field) |
| _95 | FAIL exit 22 | stage 9 activate | 400 — dev key not registered (PPIQ_PRESENTATION missing) |
| _96 | **GREEN** | — | all fixed; PIPELINE GREEN, Finished: SUCCESS |
| (post-#96, ec165699) | **GREEN** | — | frontend fixes; UI now loads + logs in |

In the GREEN run (_96): stage 6 printed `== [app] register dev Ed25519 license key (kid=ppiq-dev-ed25519) ==` then row `ppiq-dev-ed25519 | Ed25519 | active`. Stage 8 `== DEPLOY GREEN ==`. Stage 9: `admin login OK (bearer acquired)` → `Enterprise token activated` → `{"hasVerifiedLicense":true,"sourceOfTruth":"verified_ed25519_license","licenseKey":"PPIQ-DEV-ENTERPRISE","keyId":"ppiq-dev-ed25519","tier":"Enterprise",...}` → `Presentation ready: admin + Enterprise active at http://plantprocess-api:5063` → `PIPELINE GREEN`.

### 7.2 Live server queries/tests and their results (do NOT re-run to "check")
- `docker run --rm --network ppiq-app_plantprocess-private curlimages/curl ... http://plantprocess-api:5063/health` → **HTTP 401** (before the matrix fix) → **200** (after, in the green deploy).
- `git log origin/main` confirmed each push landed.
- `app_users` schema (live): columns `id, tenant_id, user_name, normalized_user_name, display_name, password_hash, password_salt, password_iterations, plant_role, compatibility_role, is_owner, is_enabled, force_password_change, created_at_utc, updated_at_utc, password_algorithm (default 'pbkdf2-sha256'), password_hash_parameters (jsonb)`. **NO `role` column, NO `is_active` column, NO `is_protected`/`is_system` column.** Unique constraint `ux_app_users_tenant_user (tenant_id, normalized_user_name)`.
- `app_users` content over time: started with `admin`/`e2eadmin` (test debris, is_owner=t, unusable passwords); after the 301 fix + fresh volume + provisioning → **only `sysadmin` (is_owner=t, display_name=PPIQ-System-Administrator).**
- `admin/DevAdmin123!` login → **401** (confirmed the test users are unusable).
- `tenants` table: row `00000000-0000-0000-0000-000000000001 | default-demo` exists (seeded by 301; REQUIRED for CreateOwnerAsync FK).
- `ppiq_ed25519_license_public_keys` schema (live): `id (uuid pk), tenant_id, key_id, public_key_b64, status (default 'active'), algorithm (check ='Ed25519'), ...`. **Forced row-level security**: policy `tenant_id = ppiq_current_tenant()`. Unique `(tenant_id, key_id)`. Was 0 rows until the PPIQ_PRESENTATION fix; the seed `dev_ed25519_public_key.sql` sets `app.current_tenant` via `set_config` so RLS allows the insert.
- Live activation test (manual curl) returned the 400 invalid-payload body that pinpointed the `licenseJws` field; after fixes, the green run shows full activation.
- `docker ps` app containers: `plantprocess-web`, `plantprocess-api`, `plantprocess-postgres`. Infra: `ppiq-jenkins` (Up ~21h), `ppiq-caddy` (Up ~4 weeks).
- Caddy config dump (live): see §4.4 (stale `plantprocess-app-web`/`plantprocess-website` targets, but app.* serves 200).
- docker-compose `env_file` quote test: confirmed compose does NOT reliably strip quotes → drove the "space-free DisplayName instead of quoting" decision.
- `deploy-canonical.sh` build path: `dc build` with `--env-file .env` → build args populated → VITE values bake in. Verified.
- External URL tests: `api.178.105.152.180.sslip.io/health` → 401; `api.../auth/login` (bad creds) → 401 (reached API); `app.../` → 200; bare `178.105.152.180.sslip.io/health` → 000.

### 7.3 Key source files read this session (paths + what they contain)
- `Backend/PlantProcess.Api/Program.cs` — middleware order (~329 CORS comment, 747 SecurityHeaders, 843 UseCors, 849 CorrelationId, 851 ExceptionHandler, 853 RequestResponseLogging, 859 RateLimiter, 867 UseAuthentication, 869 UsePlantProcessAccessControl, 871 UseAuthorization, 873 TenantContext, 874 AdminMfaRequirement, 880 AuditLog, 910+ endpoint maps); FirstRunProvisioningState singleton ~215; **AddHostedService<FirstRunProvisioningHostedService> added at 314**; EF MigrateAsync at ~779.
- `Backend/PlantProcess.Api/Security/PlantAccessControl.cs` — `AccessControlMiddleware` (class at ~173) with the permission `Matrix`; health routes now `"anonymous", true`. Deny-by-default 403 for unmapped endpoints; 401 if `!IsAuthenticated` for non-anonymous entries.
- `Backend/PlantProcess.Api/Security/AuthEndpoints.cs` — `LoginAsync` (DB-backed via AuthStore.ValidateUserAsync; dev fallback only in Development); `ClaimOwnerAsync` (`/auth/provisioning/claim`, validates token via FirstRunProvisioningState, requires empty DB, password ≥12, calls CreateOwnerAsync); `ProvisioningStatusAsync` (`/auth/provisioning/status`).
- `Backend/PlantProcess.Api/Security/AuthStore.cs` — `ValidateUserAsync` (reads app_users, PasswordHasher.Verify); `CreateOwnerAsync` (~140); `HasAnyUserAsync` (~35); `PasswordHasher` (Argon2id Hash/Verify, Sha256 for tokens).
- `Backend/PlantProcess.Api/Security/AuthOptions.cs` — `AuthOptions` (Users: List<BootstrapUserOptions>); `BootstrapUserOptions { UserName, Password, Role="Viewer", DisplayName?, IsBootstrapAdmin, ForcePasswordChangeOnFirstLogin }`.
- `Backend/PlantProcess.Api/Security/FirstRunProvisioningHostedService.cs` — `FirstRunProvisioningState` (token holder) + `FirstRunProvisioningHostedService` (StartAsync rewritten to auto-provision sysadmin from config).
- `Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs` — `Ed25519ActivateLicenseRequest(LicenseJws, ...)`; `/activate` handler: TryReadHeader → lookup key → VerifyCompactJws → persist; `/current`, `/verify-offline`.
- `Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs` — requires ConnectionStrings__PlantProcessDb, SigningKey ≥64, ≥1 real admin in Auth:Users (Role=Admin, IsBootstrapAdmin=false), rejects dev/default passwords in Production.
- `Backend/database/scripts/301_p01_p02_authstore_compatibility_bridge.sql` — creates tenants/app_users/auth_refresh_tokens compat tables + seeds canonical tenant; the admin/e2eadmin INSERT was REMOVED.
- `Backend/database/test-seeds/900_clean_test_auth_seed.sql` — test-only (correctly outside the production seed path); creates admin/e2eadmin with `test-seed-placeholder` hashes as FK anchors.
- `deploy/scripts/ensure-runtime-env.sh` — the `.env` generator (preserve logic lines 6-8; gen at 11; setkv at 15; host-derivation block at 31-36; PPIQ_PRESENTATION at 30).
- `deploy/scripts/migrate-and-seed.sh` — app/demo migrate+seed; dev-key gate at ~83.
- `deploy/scripts/deploy-canonical.sh` — build + recreate + health gate.
- `deploy/scripts/presentation-smoke.sh` — login (accessToken) + activate (licenseJws) + confirm.
- `Frontend/PlantProcess.Web/Dockerfile` — VITE ARG/ENV (now incl. SMOKE) before `npm run build`.
- `deploy/compose/docker-compose.yml` — web build.args (now incl. VITE_SMOKE_*); api env_file.
- `env/profiles/server.env.example` — the template the generator copies (still hardcodes plantprocessiq.com URLs at lines 23-24, but the generator now overrides them).


## 8. CURRENT IMPLEMENTATION — HOW IT WAS IMPROVED & ENHANCED THIS SESSION

### 8.1 Before vs After (deploy/pipeline)
**Before this session:** test stages passed but the deploy never actually ran cleanly; project-name collision could reap Jenkins; the app stack and Jenkins were tangled on the same project/Caddy; the API crash-looped on missing admin config; `/health` 401'd the gate; the stage-9 smoke had never run to completion; first-run provisioning was broken (unregistered + token-only); test users polluted production; the frontend wasn't configured for the public host.

**After this session:**
- The pipeline is **green end-to-end**, deploys in place on an isolated `ppiq-app` project that can never harm infra.
- The infra `ppiq-caddy` fronts both app and Jenkins (Option 1); app api+web join the edge network.
- The API loads its full `.env` via `env_file`, passes `StartupConfigurationValidator`, and `/health` is anonymous at BOTH the authorization-policy and pre-routing-middleware layers.
- **First-run provisioning works and is correct:** on an empty DB, `FirstRunProvisioningHostedService` auto-creates the permanent `sysadmin` (is_owner, undeletable) from config; customer admins remain a manual commissioning step.
- **Production DB is clean of test users** (301 no longer seeds admin/e2eadmin; the canonical tenant is kept for the owner FK).
- **License activation works** in the demo via the dev Ed25519 key, gated to `PPIQ_PRESENTATION=on` (demo-only).
- **The frontend builds with the correct, host-derived config** (VITE API base, website API base, CORS, demo-login creds), all from a single `PPIQ_SITE_HOST`.

### 8.2 Concrete modifications (file → change)
- `Jenkinsfile`: `COMPOSE_PROJECT='ppiq-app'`; stage-9 smoke calls `presentation-smoke.sh` in a curl sibling.
- `deploy/compose/docker-compose.yml`: `name: ppiq-app`; `plantprocess-api` gets `env_file: [.env]`; web `build.args` now include `VITE_SMOKE_USERNAME`, `VITE_SMOKE_PASSWORD` (+ existing `VITE_API_BASE_URL`).
- `deploy/compose/docker-compose.server.yml`: no Caddy service; api+web `restart: unless-stopped`, join `plantprocess-private` + `ppiq-edge`.
- `deploy/scripts/deploy-canonical.sh`: default project `ppiq-app`; build via `dc build` (env-file aware); health gate via container on the app network.
- `deploy/scripts/migrate-and-seed.sh`: dev-key gate allows `PPIQ_PRESENTATION=on`; updated skip message.
- `deploy/scripts/ensure-runtime-env.sh`: `AU="sysadmin"`; `DisplayName "PPIQ-System-Administrator"` (space-free); `PPIQ_SMOKE_*` → sysadmin; `PPIQ_PRESENTATION "on"`; host-derivation block (SITE_HOST/WEBSITE_HOST/VITE_API_BASE_URL/VITE_WEBSITE_API_BASE_URL/PLANTPROCESS_ALLOWED_ORIGINS from `PPIQ_SITE_HOST`); BOM strip; password preserved via persisted `.env`.
- `deploy/scripts/presentation-smoke.sh` (NEW): login (extract `accessToken`) → activate (`licenseJws`) → confirm `/current`.
- `Frontend/PlantProcess.Web/Dockerfile`: added `ARG/ENV VITE_SMOKE_USERNAME`, `ARG/ENV VITE_SMOKE_PASSWORD` before `RUN npm run build`.
- `Backend/PlantProcess.Api/Endpoints/Health/HealthEndpoints.cs`: health MapGroup `.AllowAnonymous()`.
- `Backend/PlantProcess.Api/Security/PlantAccessControl.cs`: `/health`,`/db-health`,`/health/ready` → `"anonymous", true`.
- `Backend/PlantProcess.Api/Security/FirstRunProvisioningHostedService.cs`: `StartAsync` auto-provisions sysadmin from `AuthOptions.Users[0]` via `CreateOwnerAsync`; token fallback retained.
- `Backend/PlantProcess.Api/Program.cs`: `AddHostedService<FirstRunProvisioningHostedService>()` (~314).
- `Backend/database/scripts/301_p01_p02_authstore_compatibility_bridge.sql`: removed the admin/e2eadmin INSERT; kept tables + canonical tenant + COMMIT + verify SELECT.
- `Backend/tests/.../Phase2RealismSourceSeedTests.cs`: repo-root resolution fixed.

### 8.3 Required API config outside Development (StartupConfigurationValidator)
- `ConnectionStrings__PlantProcessDb` (NOT `__DefaultConnection`).
- `PlantProcess__Auth__SigningKey` ≥ 64 chars (legacy `Auth:*`/`Jwt:*` rejected).
- ≥1 real admin in `PlantProcess:Auth:Users` (Role=Admin, IsBootstrapAdmin=false, non-empty UserName+Password). **This is the config that seeds `sysadmin` into the DB via FirstRunProvisioning.**
- CORS via `PLANTPROCESS_ALLOWED_ORIGINS`.
- Production rejects dev/default passwords.

---

## 9. IDENTITY & TOPOLOGY — WHERE WE STARTED vs WHERE WE ARE

### 9.1 The canonical identity model (now implemented for the demo)
- **App Postgres auth:** user `plantprocess`, DB `plantprocessiq`, container `plantprocess-postgres`, password in `.env` `POSTGRES_PASSWORD` (preserved across deploys via persisted `.env`). The app connects via `ConnectionStrings__PlantProcessDb`.
- **Frontend/app login users:** exactly two TYPES (see §3) — `sysadmin` (auto-provisioned, support-only) and the later manual customer/tenant admin. Login authenticates against `app_users` (DB), Argon2id/pbkdf2.
- **Demo-source emulator auth:** N/A currently (`PPIQ_DEMO_SOURCES_MODE=disabled` on the server).
- **License tier activation:** signed Ed25519 tokens POSTed to `/api/v5/licensing/ed25519/activate`; demo uses the dev key (kid `ppiq-dev-ed25519`), gated to `PPIQ_PRESENTATION=on`. Live tier-switching exists via `/admin/license/tier-override` (→ `license_overrides` table) and `/admin/license/effective-tier` (requires PlantProcessDataManager authz).
- **Canonical Docker container set:** infra (`ppiq-jenkins`, `ppiq-caddy`, `ppiq-backup-runner`) + app (`plantprocess-postgres`, `plantprocess-api`, `plantprocess-web`).

### 9.2 License & role enums (for reference)
- `LicenseTier { Light=1, Pro=2, ProPlus=3, Enterprise=4 }` (top = Enterprise). Activation fixtures: `deploy/fixtures/license/{light,pro,proplus,enterprise}.token`.
- `FormalPlantRole { Executive, ChiefExecutiveOfficer, ProcessEngineer, MaintenanceEngineer, Operator, Viewer, PlantAdmin, Developer }`.
- `CommercialTier { Starter, Professional, Enterprise, Developer }` (RBAC packaging, NOT the license).
- The auth-layer Role on a user is the simple string `"Admin"` (compatibility_role); plant_role for the owner is `TenantOwner`.
- Tenant `00000000-0000-0000-0000-000000000001` is the canonical demo tenant (tenant_code `default-demo`), seeded `license_tier='Enterprise'` in the golden-thread seed.

### 9.3 Identity/Topology doc versions
- The governing docs are `PPIQ_Identity_and_Topology_v3.md` (and a v4 draft exists). **They are NOT yet updated to reflect the sysadmin two-admin model or the `ppiq-app` project rename.** ACTION for a future session: produce `PPIQ_Identity_and_Topology_v5.md` capturing: the two-admin rule (§3), the ppiq-app/plantprocessiq project split (§4.1), the host-derivation (`PPIQ_SITE_HOST`) pattern, the `.env`/volume password coupling rule, and the dev-key/PPIQ_PRESENTATION demo gating.

---

## 10. ROADMAP STATUS — WHERE WE STARTED & HOW FAR WE GOT

This session was squarely in the **"environment from scratch" / deploy-convergence** track (M1 milestone family). What this session accomplished against that:
- ✅ Single canonical live stack that Caddy serves; orphaned parallel composes eliminated (the `ppiq-app` rename + Option-1 Caddy).
- ✅ Jenkins pipeline rebuilt to: pull → migrate (app DB) → seed → lint/test (npm + dotnet) → build → recreate LIVE stack in place → health gate → presentation smoke. **All green.**
- ✅ Connection key correct (`ConnectionStrings__PlantProcessDb`), 64-char SigningKey, `.env` deployed, Caddy service/port aligned (Option 1).
- ✅ First-run identity provisioning (sysadmin) implemented and working.
- ✅ License activation (Enterprise) working in the demo.
- ✅ Frontend wired to the public host (generic via `PPIQ_SITE_HOST`).
- ⏳ STILL OPEN (roadmap/maintenance, see §13): doc updates (Doctrine v8, Aspects v4, Roadmap v4, Identity v5); Spamhaus/mail-relay remediation (V1-22); customer-doc placeholder fills (V1-14/16/17/18/19); Option-3 real production license signing key; persistent infra Caddyfile bind + correct route targets; descriptive renaming of the `p01_p02`/`v5_p0x` SQL files; eventually move Jenkins to a fully separate Docker network from the app project.

The broader product roadmap (separate from deploy) was previously extended to 21 phases / 138 tasks / ~1,082 hours (P15–P21 cover prescriptive advisory, quality certification, enterprise integration, HA/DR, chaos/soak testing, multi-plant fleet, SOC2/ISO27001 readiness). None of those were touched this session — this was deploy/infra/identity only.


## 11. REALIZATION SCORECARD STATUS (deploy/infra/identity lens, end of session)

Using the standard PPIQ scorecard framing (Dark-Industrial, /100 per aspect, bands <55 crit / 55-69 needs-work / 70-84 solid / 85+ strong). This is a focused read on what THIS session touched, not a full re-score.

### Developer persona (deploy/hygiene/stability slice)
- **Deploy: STRONG (↑ from crit).** Was "test green but deploy never runs / reaps Jenkins." Now a single canonical in-place deploy on an isolated project, with rollback anchors (`:previous`) and a real health gate. Genuinely product-grade for the demo.
- **Stability: SOLID.** API no longer crash-loops; provisioning is resilient (token fallback if no config admin). Minor: provisioning runs at startup and would throw if the canonical tenant were absent — mitigated because 301 seeds it; a future hardening could wrap provisioning in try/catch so a provisioning failure never crashes the API.
- **Repo/structure & naming: NEEDS WORK.** The `p01_p02`/`v5_p0x` SQL filenames still violate the naming golden rule; descriptive renames pending. Backup folders under `deploy/.ppiq-backups/` are accumulating (every fix made a timestamped backup) — fine, but worth a periodic sweep.
- **Generic/any-customer: SOLID.** Host-derivation (`PPIQ_SITE_HOST`), env-configurable DB, no hardcoded creds in the green path. The dev-key/PPIQ_PRESENTATION gating keeps demo-only behavior out of real Production.
- **Testing: SOLID for the pipeline gates.** Backend truth-gate + unit, frontend unit, health gate, and the presentation smoke all enforce real behavior.

### Engineering Customer / CEO personas
- Not re-scored this session (no feature work). The licensing trust-posture improved in that Enterprise activation is now demonstrably real (verified Ed25519 signature, not a flag), though it uses the DEV key for the demo (Option-1; Option-3 is the production-correct target).

### Brand & Website
- The app frontend now serves and logs in. The marketing Website container (`plantprocess-website`) is referenced in Caddy by a stale name; verify/repair when the persistent Caddyfile is re-established.

### "Why not higher / why not lower" (deploy aspect)
- Why not lower: it actually deploys, is health-gated, rolls back, and provisions identity correctly — end to end, green, twice.
- Why not higher (to reach 85+ "strong, no caveats"): (a) the dev license key in a "Production" environment (demo-only but a smell), (b) the orphaned/stale infra Caddyfile and stale route targets, (c) SQL filename naming debt, (d) the `.env`/volume password coupling is a sharp edge that requires operator discipline (don't delete `.env`).

---

## 12. TIPS, TRICKS & HARD-WON LESSONS (pass these forward — they save hours)

1. **PowerShell anchored edits + CRLF:** `@'...'@` here-strings are built with LF; if the on-disk file is CRLF, `.Replace` silently misses (reports x0). Detect newline (`$nl = if($t.Contains("`r`n")){"`r`n"}else{"`n"}`) and build replacements with the matching newline, OR anchor on a single line. **Always show a `Select-String` verify after every edit.** When an edit reports `SKIP x0`, it usually means it was ALREADY applied (the old anchor no longer exists) — verify with `git show HEAD:<file>` / `Select-String` before re-editing.
2. **Jenkinsfile `sh '''...'''` comments:** must be plain ASCII prose — no `'''`, no parens, no apostrophes. Standing check: scan for `'''`; every match must be an `sh '''` opener or a bare `'''` closer.
3. **Multi-layer shell quoting in Jenkins is a minefield.** The robust pattern is to extract logic into a committed `deploy/scripts/*.sh` and have the Jenkinsfile just CALL it (as we did for the smoke). Avoid inlining escaped JSON/curl in Groovy.
4. **DooD tool steps:** `docker run --rm --volumes-from $(cat /etc/hostname) -w "${PWD}" <image> sh -lc "..."`. alpine/curl images = busybox `sh` (no bash). Non-root images (curlimages/curl) need `--user 0:0` to read root-owned files.
5. **`.env` sourcing vs docker-compose `env_file`:** the shell strips quotes on `. .env`; docker-compose `env_file` does NOT reliably strip them. So do NOT quote values to fix shell-sourcing — instead avoid spaces in values (we made DisplayName `PPIQ-System-Administrator`). Any value with a space breaks `. .env` ("<word>: not found", exit 127).
6. **VITE vars are BUILD-TIME.** They must be passed as Docker `ARG` (declared in the Dockerfile BEFORE `npm run build`) AND passed in compose `build.args` from `.env`. Runtime env does nothing for Vite. After changing them, the image must rebuild (Dockerfile change + changed ARG value bust the cache).
7. **Endpoint `AllowAnonymous` is useless against pre-routing middleware.** If a middleware runs before endpoint routing and 401s, it must itself exempt the path (or honor `IAllowAnonymous` metadata via `context.GetEndpoint()`). Liveness/readiness endpoints must be exempted in EVERY auth-ish middleware (we had two layers).
8. **Login is DB-backed.** Config `Auth:Users` satisfies the startup validator but login reads `app_users`. The bridge is `FirstRunProvisioningHostedService` (must be REGISTERED via `AddHostedService`). On empty DB it now creates the configured owner.
9. **`HasAnyUserAsync` gates provisioning.** ANY pre-existing user (even test debris) makes provisioning skip. Production seeds must NOT create login users. A "compatibility bridge" SQL script in the `scripts/` path was the sneaky culprit re-seeding test users every deploy — `test-seeds/` was correctly excluded, but a `scripts/` file was not.
10. **`CreateOwnerAsync` needs the canonical tenant** (`00000000-...-001`) to exist for its FK. Keep the tenant seed even when removing user seeds.
11. **DB password ↔ Postgres volume coupling:** Postgres sets the password only on first volume init. Regenerating `.env` (new password) against an old volume → `28P01`. Either preserve `.env` (don't delete it) or wipe the volume when you regenerate. The generator already preserves `.env` if you leave it alone.
12. **Env-var default consistency:** `${VAR:-off}` in one consumer and `${VAR:-on}` in another for the SAME variable will diverge if the var is unset. Set the variable explicitly in `.env` so both agree (the PPIQ_PRESENTATION bug).
13. **Login response field is `accessToken`; activation request field is `licenseJws`.** Verify JSON field names against the actual C# record (System.Text.Json camelCases) rather than assuming `token`.
14. **The signed-license activation order:** TryReadHeader (needs valid compact JWS in `LicenseJws`) → lookup public key by `kid`+tenant (RLS-scoped) → VerifyCompactJws (needs the registered key) → persist. A 400 "Invalid compact JWS header" means the field/body is wrong (header parse), BEFORE key/signature checks.
15. **Static pre-flight beats reactive debugging.** Before triggering, verify the WHOLE downstream chain against the live files (origin/main HEAD has the change; `.env` will contain the needed var; the script sources `.env` before the gate; fixtures exist; DB creds resolve; the consumer reads the right field). This caught the "workspace a commit behind" and "PPIQ_PRESENTATION missing" issues without burning a run.
16. **Believe the API's own error body.** `curl -s -w 'HTTP %{http_code}'` WITHOUT `-f` to see the JSON error (`{"status":"invalid_payload","error":"..."}`) — far faster than guessing.
17. **Spamhaus/Hetzner DNSBL:** Hetzner resolvers return error 127.255.255.254 for DNSBL queries — use the web checker at check.spamhaus.org, not `dig`.

---

## 13. BACKLOG — STATUS OF EACH TASK (deploy/infra/identity + carried-over)

### 13.1 DONE this session
- ✅ Converge orphaned parallel composes to ONE canonical live stack (ppiq-app rename + Option-1 Caddy).
- ✅ Rebuild Jenkins pipeline (pull→migrate→seed→lint→npm test→dotnet test→build→recreate live stack→health gate→presentation smoke), green.
- ✅ Correct connection key + 64-char SigningKey + deploy `.env` + Caddy alignment.
- ✅ First-run sysadmin provisioning (auto, undeletable, from config).
- ✅ Remove test-user seeding from production DB path (301).
- ✅ Enterprise license activation in the demo (dev key, PPIQ_PRESENTATION-gated).
- ✅ Frontend public-host wiring (VITE build args + host-derived URLs + CORS, generic via PPIQ_SITE_HOST).

### 13.2 OPEN — raised/confirmed this session
- **[NEW, HIGH] Option-1 → Option-3 license signing key.** The demo activates Enterprise using the DEV Ed25519 token (kid `ppiq-dev-ed25519`, fixture `deploy/fixtures/license/enterprise.token`) and registers the DEV public key when `PPIQ_PRESENTATION=on`. This is acceptable ONLY for SOU's demo server. **Task: generate a REAL production Ed25519 signing keypair, sign proper per-customer/per-tier license tokens, register the real public key via the canonical licensing/ops flow (not the dev fixture), and NEVER ship/register the dev key in any real customer Production install.** Also: real customer builds must NOT bake `VITE_SMOKE_*` (demo credentials) into the frontend bundle — fold this into the same hardening.
- **[OPEN] Persistent infra Caddyfile bind + correct route targets.** The live Caddyfile is an orphaned inode with stale targets (`plantprocess-app-web`, `plantprocess-website`). Re-establish a persistent host bind and correct targets to `plantprocess-web`/(website container). Do NOT recreate `ppiq-caddy` until this is fixed (it would come back with broken routes).
- **[OPEN] Descriptive renaming of SQL files** violating the naming golden rule (`301_p01_p02_*`, `300_p01_p02_*`, `302_p02_*`, `500_v5_p01_*`, `510_v5_p02_*`, etc.) — strip phase labels, keep ordering prefix, update references.
- **[OPEN] Provisioning resilience.** Wrap `FirstRunProvisioningHostedService` provisioning in try/catch so a provisioning failure logs but never crashes the API.
- **[OPEN] Generator password-preservation on stale-key regen.** Line 7 ("stale → regenerate") rotates `POSTGRES_PASSWORD`; preserve it across regen to remove the `.env`/volume sharp edge.
- **[OPEN] Move Jenkins to a fully separate Docker network** from the app project (defense in depth).

### 13.3 OPEN — carried from earlier sessions (not touched this session)
- **V1-22 Hetzner/Spamhaus mail relay:** PPIQ must NEVER send mail directly from the VPS IP. Route lead email through an authenticated transactional relay (587/465), publish SPF+DKIM for plantprocessiq.com, set valid matching PTR, block outbound port 25. Server `178.105.152.180` got a Hetzner abuse notice 23-Jun-2026 (Spamhaus). Unblocks V1-22 and V2-18.
- **Doc deliverables (path `C:\Workspace\PlantProcess-IQ\Documentation\docs\Documentation`):** V1-16 founder credibility pack, V1-17 pitch deck, V1-18 one-page product brief PDF, V1-19 pilot offer (EUR 30-40k → EUR 120k; needs Karim's real per-plant price + ROI model), V1-14 rehearse 9-step script.
- **Doc version bumps:** Doctrine v8, Aspects of Review v4, Roadmap v4, Identity & Topology v5 (see §9.3).
- **Live demo loop genealogy** (e.g. coil C-0044170) — demonstrate the golden-thread walk.
- **Public TLS for real domain:** when moving off sslip.io, set `PPIQ_SITE_HOST` to the real domain, `CADDY_AUTO_HTTPS=on`, `ACME_EMAIL`, and ensure DNS + Caddy issue real certs.
- **Layer-2 demo provisioning:** multiple FormalPlantRole users to switch live + tier-switching curl commands (deferred until pipeline green — now unblocked).

---

## 14. IMPORTANT ADVICE / WAYS OF THINKING / RULES TO CARRY FORWARD

- **The assistant CANNOT run Karim's Windows box or directly drive the server.** It READS uploaded exports + pasted logs and gives Karim exact commands. Always separate "files show X" from "X is proven to run." Own mistakes plainly.
- **Karim's terminal sometimes doubles pasted blocks** (a block ran twice this session, and `cat <<'EOF'` wrappers got echoed instead of executed). Prefer simple, single commands; avoid heredoc wrappers in pasted blocks; if output looks duplicated, that's the paste glitch, not a real double-run.
- **Karim explicitly demanded fewer iterations and full up-front analysis.** Honor the preventive-maintenance mandate aggressively: trace the entire downstream path before triggering anything. The static pre-flight is the model.
- **Two-admin golden rule (§3) is sacred.** Never auto-create a customer-named admin; only the permanent `sysadmin` during install.
- **Solution Doctrine + Autonomous Generic-Fix:** permanent, committed, generic, product-grade fixes only. Never per-machine workarounds, never skip/loosen tests, never "make it green" hacks. Fix the product unless the test is genuinely wrong.
- **Naming golden rule** applies to every new artifact.
- **Backups before edits** (`deploy\.ppiq-backups\<name>-<ts>\`), commits behind `$env:PPIQ_COMMIT='1'`, explicit file lists.
- **When a fix touches `.env` generation, remember the `.env`/Postgres-volume coupling** and the host-derivation. Don't casually delete the persisted `.env`.

---

## 15. EXACT COMMANDS THE NEXT SESSION WILL LIKELY NEED

```bash
# --- demo credentials (sysadmin) ---
docker exec ppiq-jenkins sh -lc 'cat /var/lib/ppiq-preserve/FIRST_LOGIN.txt'
docker exec ppiq-jenkins sh -lc 'grep -E "PPIQ_SMOKE_USERNAME|PPIQ_SMOKE_PASSWORD|VITE_API_BASE_URL|PPIQ_PRESENTATION|SITE_HOST" /var/jenkins_home/workspace/plantprocessiq-deploy/deploy/compose/.env'

# --- confirm sysadmin in DB (do NOT re-provision) ---
docker exec plantprocess-postgres psql -U plantprocess -d plantprocessiq -c "SELECT user_name, is_owner, is_enabled, display_name FROM app_users;"
docker exec plantprocess-postgres psql -U plantprocess -d plantprocessiq -c "SELECT id, tenant_code FROM tenants;"
docker exec plantprocess-postgres psql -U plantprocess -d plantprocessiq -c "SELECT key_id, status FROM ppiq_ed25519_license_public_keys;"

# --- health (internal = 200; external api.* = 401 but login works) ---
docker run --rm --network ppiq-app_plantprocess-private curlimages/curl:8.10.1 -ks -o /dev/null -w 'HTTP %{http_code}\n' http://plantprocess-api:5063/health

# --- containers / infra ---
docker ps --format '{{.Names}}: {{.Status}}'

# --- IF you must regenerate .env (rotates DB password) you MUST also wipe the volume: ---
# docker exec ppiq-jenkins rm -f /var/lib/ppiq-preserve/.env /var/jenkins_home/workspace/plantprocessiq-deploy/deploy/compose/.env
# docker rm -f plantprocess-api plantprocess-web plantprocess-postgres
# docker volume rm ppiq-app_plantprocess-postgres-data
# then trigger the pipeline (fresh .env + fresh volume from the same run = matching password)

# --- override the public host for a different domain (generic) ---
# set PPIQ_SITE_HOST in the deploy env so all VITE URLs + CORS derive from it
```

### Server identity quick card
- Server: `178.105.152.180` (Hetzner, Ubuntu, root SSH). Jenkins: `https://jenkins.178.105.152.180.sslip.io/`, job `plantprocessiq-deploy`.
- App URL: `https://app.178.105.152.180.sslip.io` (UI). API: `https://api.178.105.152.180.sslip.io`.
- Infra project `plantprocessiq` (jenkins, caddy, backup-runner) — SACRED. App project `ppiq-app` (postgres, api, web).
- Latest green commit lineage tip: `ec165699` (frontend) on top of `94b8fb4f` (first green pipeline, build #96).

---

*End of handover. The pipeline is green and the demo UI is working. The next session should NOT re-investigate the items above — they are documented, solved, and committed. Pick up from the OPEN backlog (§13.2/§13.3) using the working rules (§2) and the two-admin golden rule (§3).*
