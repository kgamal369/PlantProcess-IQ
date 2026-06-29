# PlantProcess IQ — Identity & Topology Reference (v4)

**Supersedes:** v3. v4 adds the **server deployment reality** (the green Jenkins pipeline, the `ppiq-app` / `plantprocessiq` project split, the permanent `sysadmin` two-admin identity model, the host-derived public-URL system, the `.env`/Postgres-volume password coupling), corrects every fact that changed when the stack moved from laptop-only to a live demo server, and adds a dedicated **server & Jenkins host-access section**. It preserves, in full, the v3 local-development reference — because daily development, testing, and troubleshooting still happen on the local laptop.

**Last updated:** 26 Jun 2026
**Local stack status:** EF baseline + full SQL decoration layer applied green; Ed25519 licensing live; API boots on `localhost:5063`. (Daily dev/test/troubleshooting environment — still fully used.)
**Server stack status:** Deploy pipeline **GREEN end-to-end** (Jenkins job `plantprocessiq-deploy`, build #96, commit `94b8fb4f`; frontend fixes `ec165699`). Demo UI live at `https://app.178.105.152.180.sslip.io`; `sysadmin` auto-provisioned; Enterprise license active. (Release/demo environment.)

> Single authoritative source for how PPIQ identifies itself across environments — ports, databases, auth, users, demo sources, licenses, containers — plus the exact commands to provision and run it in **both** environments, and the known gaps the clean-rebuild must close. Everything here is environment-configurable; nothing in the product hardcodes an environment.

---

## 0. THE TWO ENVIRONMENTS — read this first

PPIQ runs in **two distinct environments that both matter every day**. This document documents BOTH, side by side, in every relevant section. Neither replaces the other.

| | **LOCAL (laptop)** | **SERVER (Hetzner VPS)** |
|---|---|---|
| **When used** | Daily development, testing, debugging, troubleshooting — Karim's working environment | Releasing a new version; the live demo; customer-facing presentations |
| **How reached** | `localhost` / `127.0.0.1` + local ports | `178.105.152.180.sslip.io` subdomains over HTTPS (Caddy) |
| **Main DB** | Native PostgreSQL 16 on `localhost:5432` (NOT a container); only demo-source DBs are containers | All DBs are containers; main = `plantprocess-postgres` |
| **API host** | `http://localhost:5063` (native `dotnet run` / `ppiq.ps1 up`) | `https://api.178.105.152.180.sslip.io` (Caddy → `plantprocess-api:5063`) |
| **DB reach (containerised API)** | API container → native main DB via `host.docker.internal` | API container → `plantprocess-postgres` on the app network |
| **Identity** | 5 dev-seed config users (admin/exec/engineer/operator/viewer) for local testing | Permanent `sysadmin` (auto-provisioned) + manual customer admin at commissioning |
| **Login source of truth** | Config-seeded users (local dev convenience) | DB-backed: `app_users` table (Argon2id/pbkdf2) |
| **Deploy mechanism** | `ppiq.ps1` verbs (`up`, `migrate`, `demo`, …) | `git push` → GitHub webhook → Jenkins `plantprocessiq-deploy` pipeline |
| **Secrets** | `deploy/compose/.env.dev` (committed; local-only, never real secrets) | `/var/lib/ppiq-preserve/.env` (git-ignored; real, persisted, password-stable) |
| **Encoding workaround** | `PGCLIENTENCODING=UTF8` before psql (until baked in) | Handled inside the pipeline scripts |

**Golden rule for this document:** wherever a port, connection string, credential, container name, or command differs between local and server, BOTH are documented and each is labeled with the phase it belongs to. Never collapse one into the other.

---

## 0.1 TL;DR — provision & run

### LOCAL (daily development) — run from `C:\Workspace\PlantProcess-IQ`
Assumes native PostgreSQL 16 on `localhost:5432`, database `ppiq_app`, role `ppiq_dev` / `ppiq_dev_local_only`.

```powershell
# 1) EF core schema (model-first migrations -> tables FIRST)
$env:PLANTPROCESS_DB = 'Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only'
dotnet ef database update -p Backend/PlantProcess.Infrastructure -s Backend/PlantProcess.Api

# 2) SQL decoration layer on top (read UTF-8 i18n scripts correctly)
$env:PGCLIENTENCODING = 'UTF8'
.\deploy\scripts\ppiq.ps1 migrate

# 3) Run the API (foreground; loads .env.dev, --no-launch-profile)
.\deploy\scripts\ppiq.ps1 up      # wait for: Now listening on: http://localhost:5063

# 4) In a SECOND terminal: role x license matrix (see section 10.3)
```

The two environment variables above (`PLANTPROCESS_DB`, `PGCLIENTENCODING`) are session work-arounds until their permanent homes land — see sections 2.3, 11, and 14.

### SERVER (release a new version)
The server is driven by the pipeline, not by hand. Normal release:

```bash
# from the local laptop, after committing:
git push origin main            # -> GitHub webhook -> Jenkins 'plantprocessiq-deploy' runs the full pipeline
# watch: https://jenkins.178.105.152.180.sslip.io/  (job plantprocessiq-deploy)
```

The pipeline checks out `main`, regenerates `.env`, migrates+seeds the app DB, registers the demo license key (presentation mode), builds + deploys the stack **in place** on the `ppiq-app` project, health-gates with rollback, and runs the presentation smoke (sysadmin login + Enterprise activation). See section 11 for the full stage map and section 17 for host access.

> **CRITICAL server rule:** do NOT delete `/var/lib/ppiq-preserve/.env`. It is reused across deploys to keep the Postgres password stable. Deleting it generates a new password that will NOT match the existing Postgres data volume (`28P01`). If you must regenerate it, wipe the `ppiq-app_plantprocess-postgres-data` volume in the same step. See section 2.4.

---

## 1. Ports & endpoints

### 1.1 LOCAL (development) ports

| Service | Port | Notes |
|---|---|---|
| API (PlantProcess.Api) | **5063** | `http://localhost:5063` |
| Web / HMI (Vite dev) | 5173 | React/TypeScript |
| Vite preview | 4173 | |
| Marketing website | 5080 | |
| PostgreSQL (app DB) | 5432 | **native local** (not a container) |

### 1.2 SERVER (release/demo) public routes — fronted by infra Caddy over HTTPS

| URL | Routes to (container) | Notes |
|---|---|---|
| `https://app.178.105.152.180.sslip.io` | `plantprocess-web:80` | the demo UI (sysadmin auto-login) |
| `https://api.178.105.152.180.sslip.io` | `plantprocess-api:5063` | the API |
| `https://website.178.105.152.180.sslip.io` | marketing site | |
| `https://jenkins.178.105.152.180.sslip.io` | `jenkins:8080` | the CI/CD pipeline UI |

**Server health-check quirk (documented so it isn't mistaken for a fault):** the **external** `https://api.178.105.152.180.sslip.io/health` returns **HTTP 401**, but `POST .../auth/login` works (it reaches the API and authenticates). The deploy health gate uses the **internal** `http://plantprocess-api:5063/health`, which returns **200**. Bare host `https://178.105.152.180.sslip.io/health` returns 000 (the bare host serves the app, not that path).

### 1.3 Key API routes (identical contract in both environments)
- `POST /auth/login` — body `{ userName, password }`. Returns `{ "accessToken": "...", "tokenType": "Bearer", ... }`. **The token field is `accessToken`** (camelCased `LoginResponse.AccessToken`), not `token`.
- `POST /api/v5/licensing/ed25519/activate` — body **`{ "licenseJws": "<compact JWS>" }`**. ⚠️ **CORRECTED in v4:** the request DTO field is `LicenseJws` (→ `licenseJws`); the old v3 value `{ token: ... }` is WRONG and produces `400 invalid_payload "Invalid compact JWS header."`.
- `POST /api/v5/licensing/ed25519/verify-offline` — returns tier from a JWS (field also `licenseJws`).
- `GET  /api/v5/licensing/ed25519/current` — active entitlement source of truth.
- `POST /api/v5/licensing/ed25519/entitlement-check` — body `{ Feature, DbTierOverride }` (DbTierOverride is **ignored** — tamper-proof by design).

> The Phase-10 `/offline-activation/verify` endpoint takes a different structured envelope, **not** the compact JWS. Do not use it for the `.token` fixtures.

---

## 2. Application database

### 2.1 Connection per environment

| Env | Host | Port | Database | User | Password | How the app reaches it |
|---|---|---|---|---|---|---|
| **LOCAL (native run / EF / tests)** | `localhost` | 5432 | `ppiq_app` | `ppiq_dev` | `ppiq_dev_local_only` | direct |
| **LOCAL (containerised app)** | container `plantprocess-postgres` (pub `127.0.0.1:5432`) | 5432 | `ppiq_app` | `ppiq_dev` | `ppiq_dev_local_only` | API reaches **native main DB** via `host.docker.internal` |
| **SERVER** | container **`plantprocess-postgres`** (project `ppiq-app`) | 5432 | `plantprocessiq` | `plantprocess` | git-ignored, **persisted** `/var/lib/ppiq-preserve/.env` (reused across deploys) | container network `ppiq-app_plantprocess-private` |

⚠️ **CORRECTED in v4 (two fixes):**
1. The server main-DB container is **`plantprocess-postgres`** in the **`ppiq-app`** project — NOT `postgres` / `ppiq-postgres` as v3 stated.
2. The required config key sentence in v3 listed the same key on both sides. Correct statement: **the required connection key outside Development is `ConnectionStrings__PlantProcessDb` — NOT `ConnectionStrings__DefaultConnection`.** (`DefaultConnection` is the wrong key the app must never use.)

### 2.2 Schemas (both environments)
- `ppiq_meta` — metadata / control plane
- `ppiq_plant` — customer plant data
- **Known gap (unchanged):** EF entities have **no `HasDefaultSchema`**, so EF tables land in `public`. Consistent and non-blocking, but the canonical two-schema split is not yet enforced for EF tables. Deferred to clean-rebuild.

### 2.3 EF migrations — model-first (applies to both environments)
Migrations live in `Backend\PlantProcess.Infrastructure\Migrations`. The DbContext uses `ApplyConfigurationsFromAssembly(...)` (config-only; no `DbSet<AuditLogEntry>`). EF Core pinned to **9.0.4**.

Re-baselined to two clean migrations (a prior squash had dropped the `audit_log_entries` CreateTable, causing `42P01` on the immutability migration):
1. **`InitialBaseline`** — the entire current model. Contains `CreateTable "audit_log_entries"` + the six `ix_audit_log_*` indexes.
2. **`AuditAppendOnlyTriggers`** — DB-only constructs in a thin migration via `migrationBuilder.Sql(...)`: function `prevent_audit_log_mutation()` + `trg_prevent_audit_log_update/delete/truncate` (BEFORE, `P0001`), with matching drops in `Down()`.

**Ordering rule (critical, both environments):** EF migrations create the tables; the SQL decoration scripts decorate them. **EF must run first.**
- **LOCAL:** the app applies EF migrations at startup (`Program.cs`), so a bare `ppiq.ps1 up` self-heals; but `ppiq.ps1 migrate` runs **only** the SQL scripts, so a fresh DB must get `dotnet ef database update` (or one app boot) before `migrate`.
- **SERVER:** pipeline stage 6 runs `migrate-and-seed.sh --app-only`, which generates+applies EF migrations in an SDK sibling container BEFORE the numbered SQL and seeds. The API also applies EF migrations at startup. So the server is covered both ways.

**Design-time factory:** `PlantProcessDesignTimeDbContextFactory` reads, in order, `ConnectionStrings__PlantProcessDb` → `PLANTPROCESS_DESIGNTIME_CONNECTION_STRING` → `PLANTPROCESS_DB`. Until committed, set `PLANTPROCESS_DB` before any `dotnet ef` command locally (section 0.1).

**Re-baseline procedure (LOCAL only; run when the model changes structurally):**
```powershell
git add -A; git commit -m "checkpoint before EF re-baseline"
Remove-Item Backend\PlantProcess.Infrastructure\Migrations\*.cs -Force
$env:PLANTPROCESS_DB = 'Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only'
dotnet ef migrations add InitialBaseline        -p Backend/PlantProcess.Infrastructure -s Backend/PlantProcess.Api
# verify the baseline contains audit_log_entries + ix_audit_log_*  (Select-String the generated .cs)
dotnet ef migrations add AuditAppendOnlyTriggers -p Backend/PlantProcess.Infrastructure -s Backend/PlantProcess.Api
# paste the trigger function + triggers into Up()/Down() via migrationBuilder.Sql(@"...")
# hard reset the DB schema, then apply:
$env:PGPASSWORD='ppiq_dev_local_only'
psql -h localhost -U ppiq_dev -d ppiq_app -c "DROP SCHEMA IF EXISTS ppiq_meta CASCADE; DROP SCHEMA IF EXISTS ppiq_plant CASCADE; DROP SCHEMA public CASCADE; CREATE SCHEMA public AUTHORIZATION ppiq_dev;"
dotnet ef database update -p Backend/PlantProcess.Infrastructure -s Backend/PlantProcess.Api
```

> **Doctrine:** never hand-write raw `CREATE TABLE` for schema. Entities + `IEntityTypeConfiguration` → `dotnet ef migrations add`. DB-only constructs (triggers, functions) go in a thin separate migration via `migrationBuilder.Sql`.

### 2.4 ⭐ NEW — the server `.env` ↔ Postgres-volume password coupling (a hard operational law)
**This bit Karim once this session and must never be forgotten.** Postgres sets the database password **only on first volume initialization**. On the server:
- The generator (`ensure-runtime-env.sh`) **preserves** the password by reusing the persisted `/var/lib/ppiq-preserve/.env` across deploys (its reuse-logic keys are `PPIQ_API_UPSTREAM`, `PPIQ_DEMO_SOURCES_MODE`). As long as you leave that file alone, the password is stable and matches the existing volume.
- If you **delete** the persisted `.env`, the next deploy generates a NEW `POSTGRES_PASSWORD`, but the existing Postgres volume still has the OLD password → `28P01: password authentication failed`.
- **Rule:** do not delete `/var/lib/ppiq-preserve/.env`. If you genuinely must regenerate it, wipe the volume in the SAME operation so a fresh DB initializes with the new password:
  ```bash
  docker exec ppiq-jenkins rm -f /var/lib/ppiq-preserve/.env /var/jenkins_home/workspace/plantprocessiq-deploy/deploy/compose/.env
  docker rm -f plantprocess-api plantprocess-web plantprocess-postgres
  docker volume rm ppiq-app_plantprocess-postgres-data
  # then trigger the pipeline: fresh .env + fresh volume from the same run = matching password
  ```
- **Known tech-debt:** the generator's "stale persisted .env → regenerate" path can rotate the password when keys change between versions. A future hardening should preserve `POSTGRES_PASSWORD` across regen. Tracked in section 16.

---

## 3. Authentication & JWT

- Issuer `plantprocess-iq`, Audience `plantprocess-iq-clients`
- Password hashing: **Argon2id**, 64 MB (`65536`), iterations 3, parallelism 1 (legacy pbkdf2-sha256 also present as `password_algorithm` default on older rows)
- Bootstrap admin: **DISABLED** (replaced on the server by the permanent `sysadmin` owner — see section 4.2)
- `__Host-` prefixed auth cookie

### 3.1 Login is DB-backed (the key clarification, server-critical)
⭐ **Sharpened in v4.** Login authenticates against the **`app_users` table** (Argon2id/pbkdf2 via `AuthStore.ValidateUserAsync`), **not** the config `PlantProcess:Auth:Users`. The config users serve two narrow purposes:
1. Satisfy `StartupConfigurationValidator` (requires ≥1 real admin, Role=Admin, IsBootstrapAdmin=false).
2. Seed the **first owner** into `app_users` via `FirstRunProvisioningHostedService` on an empty DB.

A dev-only fallback (`ResolveDevelopmentUser`) exists ONLY when `IsDevelopment()` is true — so on the LOCAL box, config users effectively work for login; on the SERVER (Production), login is strictly DB-backed. This is why a configured-but-not-provisioned admin returns 401 on the server.

### 3.2 Signing key — the startup-guard floor
`StartupConfigurationValidator` (historically `P01P02StartupGuard`) rejects "dangerous" signing keys (blocklist substrings `DEV_ONLY`, `DEFAULT`, `admin`, `password`, `plantprocess123`, `Admin123!`, …) and enforces a length floor (general 32; **production ≥64**).
- **LOCAL fix (applied):** `deploy/compose/.env.dev` carries a token-free 68-char key:
  ```
  PlantProcess__Auth__SigningKey=ppiq-local-signing-key-not-for-production-0a1b2c3d4e5f60718293a4b5c6
  ```
  Guard passes (`signingKeyLen=68`). Rotate before any non-local use.
- **SERVER:** the generator writes a fresh ≥64-char `PlantProcess__Auth__SigningKey` into `.env`; the green run reported `signingKeyLen=96`.

---

## 4. Identity & role users — **the two-admin model (server) + dev-seed users (local)**

⭐ **Most important correction in v4.** v3 said "login uses these config users, not DB tables" and listed five users. That is approximately true for LOCAL dev, but for the SERVER it is wrong: login is DB-backed and the identity model is the **two-admin rule**. Both are documented below, each labeled by environment.

### 4.1 LOCAL (development) — the five dev-seed config users
Source of truth: `deploy/compose/.env.dev`, keys `PlantProcess__Auth__Users__N__{UserName,Password,Role}`. These exist for **local testing of the role × license matrix** and are seeded into the local DB on app load (Argon2id). They are committed because they are local-only and must never "vanish" — they are NOT real secrets.

| User | Password | Role |
|---|---|---|
| admin | DevAdmin123! | Admin |
| exec | DevExec123! | Executive |
| engineer | DevEng123! | Engineer |
| operator | DevOp123! | Operator |
| viewer | DevView123! | Viewer |

**Local cleanup still pending (closes T02):** the stale 4-user set baked into `appsettings.Development.json` (admin/`Admin123!`, engineer/`Engineer123!`, datamanager/`DataManager123!`, viewer/`Viewer123!`) shadows the canonical five on a bare `dotnet run`. Write the 5 users + DSN into `appsettings.Development.json` and strip the empty `ConnectionStrings__PlantProcessDb` from all three `launchSettings.json` profiles.

### 4.2 SERVER (release/demo) — the TWO admin types (strict; never conflate)

> This is the canonical, permanent identity model for any real install. Saved as a standing rule.

**(1) `sysadmin` = System Owner / Support account.**
- PERMANENT and **UNDELETABLE**. Created from the very beginning during the automated system install/launch.
- The **FirstRunProvisioning path provisions THIS account ONLY.** On an empty DB, `FirstRunProvisioningHostedService.StartAsync` reads the first `PlantProcess:Auth:Users` entry and calls `AuthStore.CreateOwnerAsync(userName, password, displayName)`.
- Stored in `app_users`: `user_name='sysadmin'`, `display_name='PPIQ-System-Administrator'`, `is_owner=true`, `plant_role='TenantOwner'`, `compatibility_role='Admin'`, `force_password_change=true`, tenant `00000000-0000-0000-0000-000000000001`.
- **Strictly for SOU's / Karim's team** for on-call support and troubleshooting. **The customer must NEVER use or see `sysadmin`.** There is no delete-user API path, so it is effectively undeletable/protected.
- This is the account the deploy pipeline auto-provisions and the stage-9 presentation smoke logs in as.

**(2) "Customer Admin" (Tenant Admin) = the normal admin for the client.**
- Named `admin` or the company name or anything.
- **NOT created during the automated pipeline/system install.** Inserted **MANUALLY LATER** during the early commissioning phase to configure data sources and build/configure UI pages.

So: auto-provisioning creates **ONLY** the permanent `sysadmin`; customer/tenant admins are a separate, later, manual commissioning step. **Never auto-create a customer-named admin during install.**

### 4.3 ⚠️ REMOVED FROM PRODUCTION (was a defect): the `admin` / `e2eadmin` test users
v3's world had `admin`/`e2eadmin` test users with `is_owner=true` in the server DB. **These are now GONE from the production path:**
- The production script `Backend/database/scripts/301_p01_p02_authstore_compatibility_bridge.sql` **no longer seeds them** — it keeps only the table DDL and the canonical tenant seed (`00000000-...-001`, required for the owner FK).
- The test-seed file `Backend/database/test-seeds/900_clean_test_auth_seed.sql` (which creates admin/e2eadmin as FK anchors with `test-seed-placeholder` hashes) is correctly OUTSIDE the production seed path.
- Why this mattered: any pre-existing user makes `HasAnyUserAsync()` return true, which makes `FirstRunProvisioning` SKIP — so the test debris was preventing `sysadmin` from provisioning. Removing it is what let provisioning fire.
- `admin/DevAdmin123!` against the server → 401 (the old test users are unusable; do not rely on them).

### 4.4 `app_users` table — live schema (server, confirmed)
Columns: `id, tenant_id, user_name, normalized_user_name, display_name, password_hash, password_salt, password_iterations, plant_role, compatibility_role, is_owner, is_enabled, force_password_change, created_at_utc, updated_at_utc, password_algorithm (default 'pbkdf2-sha256'), password_hash_parameters (jsonb)`. Unique constraint `(tenant_id, normalized_user_name)`. **There is NO `role`, `is_active`, or `is_protected`/`is_system` column** (use `is_owner` + `is_enabled`). After a clean server deploy, the table contains **only `sysadmin`**.

---

## 5. Demo source fleet (8 sources)

### 5.1 LOCAL — the canonical fleet (project `ppiq-sources`)
Canonical compose: `deploy/compose/docker-compose.sources.yml`.

| Source | Container | Host port | DB / user |
|---|---|---|---|
| meltshop-postgres | ppiq-src-meltshop-postgres | 15432 | meltshop / ppiq_src / ppiq_src_local_only |
| caster-oracle | ppiq-src-caster-oracle | 11521 | oracle-free:23, APP_USER ppiq_src |
| hsm-oracle | ppiq-src-hsm-oracle | 11522 | (oracle-free) |
| pkl-mssql | ppiq-src-pkl-mssql | 11433 | sa / Ppiq_Src_Local_Only1 (+ init container) |
| downtime-mysql | ppiq-src-downtime-mysql | 13306 | downtime / root / ppiq_src_root_local |
| parsytec-mysql | ppiq-src-parsytec-mysql | 13307 | parsytec |
| excel-yard (CSV mount) | — | — | ~5,600 coils |
| excel-qa (CSV mount) | — | — | ~1,868 rows |

Connector tier gates: CSV/Excel = **Light**; PostgreSQL = **Pro**; Oracle / MSSQL / MySQL / REST / OPC-UA = **Enterprise**.

Local demo data confirmed seeded: 630 heats, 5,670 coils, 39,690 HSM passes, 1,987 surface defects, 17,010 QA results, 210 downtime events.

**To delete (duplicate fleet):** `deploy/compose/docker-compose.demo-sources.yml` (project `plantprocessiq-demo-sources`, containers `ppiq-source-*`, creds `meltshop_owner`/`caster_owner`, oracle-xe:21, network `ppiq-demo-sources`) and its `.ports.yml`.

### 5.2 SERVER — demo sources currently DISABLED
On the server the green pipeline runs with `PPIQ_DEMO_SOURCES_MODE=disabled` (stage 6 uses `--app-only`; stage 7 demo-sources is skipped). When enabling demo sources on the server later, the same `ppiq-sources` project/compose applies. (The 8-source fleet is a local-development asset today.)

---

## 6. Licensing — Ed25519, four tiers

### 6.1 Tiers (both environments)

| Tier | Level | Limits (users / sources / jobs / dashboards) | Extras |
|---|---|---|---|
| Light | 1 | 3 / 1 / 1 / 3 | CSV/Excel only |
| Pro | 2 | 10 / 3 / 5 / 8 | + SQL editor, PostgreSQL connector |
| ProPlus | 3 | 25 / 8 / scheduled / widgets | + KPI/widget, scheduled correlations, ML |
| Enterprise | 4 | unlimited | all connectors, branded reports |

### 6.2 Tokens & keys
`deploy/fixtures/license/{light,pro,proplus,enterprise}.token` — compact EdDSA JWS (`eyJhbG...`). `kid = ppiq-dev-ed25519`; tenant `00000000-0000-0000-0000-000000000001`; `publicKeyB64 = DnycfAUUX263chT9G2UHQ6gbI6HUe5dX8W5KQL8E/Ss=`. Dev key material `deploy/fixtures/license/dev_public.pem` / `dev_private.pem` and `deploy/fixtures/license/dev_public.b64` (dev-only — rotate for production).

The Enterprise fixture payload: `{tenantId:"00000000-...-001", licenseKey:"PPIQ-DEV-ENTERPRISE", tier:"Enterprise", issuedAtUtc:"2026-06-16...", expiresAtUtc:"2027-06-16...", features:[], limits:{}}`; header `{alg:"EdDSA", typ:"license+jws", kid:"ppiq-dev-ed25519"}`.

⚠️ **CORRECTED in v4:** activation request body is **`{ "licenseJws": ... }`** (the DTO field is `LicenseJws`), NOT `{ token: ... }`.

### 6.3 DB tables
`ppiq_ed25519_license_public_keys`, `ppiq_ed25519_activated_licenses`, `ppiq_ed25519_entitlement_audit`, view `ppiq_v_ed25519_current_entitlements`. The key table is **RLS-forced** (policy `tenant_id = ppiq_current_tenant()`), unique `(tenant_id, key_id)`. Created by `650_remaining_p10_ed25519_verified_license.sql` (NOT EF; self-contained, depends only on `pgcrypto`).

**LOCAL standalone apply:**
```powershell
$env:PGPASSWORD='ppiq_dev_local_only'
& 'C:\Program Files\PostgreSQL\16\bin\psql.exe' -h localhost -p 5432 -U ppiq_dev -d ppiq_app -v ON_ERROR_STOP=1 -f Backend\database\scripts\650_remaining_p10_ed25519_verified_license.sql
```

### 6.4 ⭐ NEW — server demo-gating of the dev key (`PPIQ_PRESENTATION`)
On the SERVER (Production), the dev public key is registered into `ppiq_ed25519_license_public_keys` ONLY when **`PPIQ_PRESENTATION=on`** (written into `.env` by the generator). The registration seed (`Backend/database/seed/dev_ed25519_public_key.sql`) sets `app.current_tenant` via `set_config` so the RLS policy permits the insert. This keeps the dev key out of a real customer Production (where `PPIQ_PRESENTATION` would be off).

> **BACKLOG (Option-1 → Option-3):** the demo activates Enterprise with the DEV key — acceptable ONLY for SOU's demo server. Before any real customer: generate a REAL production Ed25519 signing keypair, sign per-customer/per-tier tokens, register the real public key via the canonical licensing/ops flow, and never ship/register the dev key in a real Production install. Also: real customer frontends must NOT bake `VITE_SMOKE_*` (demo credentials) into the bundle. Tracked in section 16.

---

## 7. Containers & topology

⚠️ **REWRITTEN in v4.** v3 described a single `plantprocessiq` app project with a `plantprocess-caddy`. That is now wrong. The current topology is **two clearly separated compose projects**, and they must never be merged.

### 7.1 LOCAL topology
- **App stack** — native API (`localhost:5063`) + native main Postgres (`localhost:5432`); the API can also run containerised and reach the native DB via `host.docker.internal`.
- **Sources stack** — project `ppiq-sources` (canonical `docker-compose.sources.yml`), the 8-source fleet (section 5.1).

### 7.2 SERVER topology — TWO projects (do not merge)

**(A) Infrastructure project `plantprocessiq` — SACRED, never reaped.**
- `ppiq-jenkins` — Jenkins (Docker-out-of-Docker; mounts `/var/run/docker.sock`)
- `ppiq-caddy` — binds `0.0.0.0:80/443`, fronts EVERYTHING (app + Jenkins + website)
- `ppiq-backup-runner`
- Network: `plantprocessiq_ppiq-net`

**(B) Application project `ppiq-app` — deployed by the pipeline.**
- `plantprocess-postgres` — main app DB (volume `ppiq-app_plantprocess-postgres-data`)
- `plantprocess-api` — listens `:5063`
- `plantprocess-web` — nginx serving the Vite build, `:80`
- Network: `ppiq-app_plantprocess-private`; api+web ALSO join an external alias `ppiq-edge` → `plantprocessiq_ppiq-net` so the infra Caddy reaches them by name.

**WHY the split matters (the big bug it fixed):** when the app deploy used the project name `plantprocessiq` (same as infra), `docker compose -p plantprocessiq up -d --remove-orphans` **reaped `ppiq-jenkins` / `ppiq-caddy`** mid-deploy. Renaming the app project to `ppiq-app` makes `--remove-orphans` unable to touch infra. **There is no `plantprocess-caddy` in the app stack anymore** (Option 1: the infra `ppiq-caddy` fronts both).

### 7.3 ⚠️ Caddy tech-debt (known issue, server)
The live infra Caddyfile still references **stale targets** `plantprocess-app-web` and `plantprocess-website`, which do not match the real container names (`plantprocess-web`, `plantprocess-api`). Despite this, `app.*` returns 200 and the UI loads (resolved via the shared edge network) — but it is fragile, and the Caddyfile is an orphaned inode (host bind source was deleted; in-place edits fail "Resource busy"; hot-reloads are non-persistent). **Do NOT recreate `ppiq-caddy`** until a persistent host-bound Caddyfile with corrected targets is established. Tracked in section 16.

---

## 8. Frontend

React / TypeScript / Vite ("HMI"). Dark Industrial palette: Deep Navy `#050B18`, panel `#0B1730`, Cyan `#00D4FF`, Blue `#0A84FF`, ok `#2CE6A2`, warn `#FFB020`, crit `#FF4D6D`; fonts Inter + JetBrains Mono.

### 8.1 ⭐ NEW — VITE config is BUILD-TIME (server frontend wiring)
Vite inlines `VITE_*` variables at **build time** (`npm run build`), not runtime. For the server demo UI to work, three things were required (all fixed):
1. **Web Dockerfile** (`Frontend/PlantProcess.Web/Dockerfile`) declares `ARG`/`ENV` for `VITE_API_BASE_URL`, `VITE_SMOKE_USERNAME`, `VITE_SMOKE_PASSWORD` **before** `RUN npm run build`.
2. **Compose** (`deploy/compose/docker-compose.yml`) passes those as `build.args` from `.env`; `deploy-canonical.sh` builds with `--env-file .env` so they populate.
3. **The generated `.env`** sets them correctly (next section).

After changing any `VITE_*`, the web image must rebuild (Dockerfile change + changed ARG value bust the cache) and the browser must hard-refresh (cached bundle).

### 8.2 ⭐ NEW — host-derived public URLs + CORS (generic, `PPIQ_SITE_HOST`)
The generator derives ALL browser-facing URLs from a single variable `PUBLIC_HOST="${PPIQ_SITE_HOST:-178.105.152.180.sslip.io}"`:
```
SITE_HOST=${PUBLIC_HOST}
WEBSITE_HOST=website.${PUBLIC_HOST}
VITE_API_BASE_URL=https://api.${PUBLIC_HOST}
VITE_WEBSITE_API_BASE_URL=https://api.${PUBLIC_HOST}
PLANTPROCESS_ALLOWED_ORIGINS=https://app.${PUBLIC_HOST},https://${PUBLIC_HOST},https://website.${PUBLIC_HOST}
```
For a real customer/domain: set `PPIQ_SITE_HOST=their-domain.com` and every URL + CORS origin follows. (This replaced the old hardcoded `api.plantprocessiq.com` template values, which did not resolve to the demo server and broke the UI's backend connection + CORS.)

---

## 9. Environment profiles & customer modes

`env/profiles/customer-template.env.example`:
- `PPIQ_MAIN_DB_MODE = native | docker | external | managed`
- `PPIQ_DEMO_SOURCES_MODE = docker | external | disabled | mixed`

CORS via `PLANTPROCESS_ALLOWED_ORIGINS` (server: host-derived, section 8.2). Real admin must be `Role=Admin, IsBootstrapAdmin=false`.

`PPIQ_PRESENTATION = on | off` — server demo flag; `on` registers the dev license key and runs the presentation smoke (section 6.4). Real customer = `off`.

`PPIQ_SITE_HOST` — server public host that drives all URLs/CORS (section 8.2).

**To delete (leaks `plantprocess123`):** `env/profiles/local.env`.


## 10. Migration & deploy commands — the working sequences

### 10.1 LOCAL — provision from clean
See section 0.1. Order: **EF schema first, then SQL decoration, then app.**

### 10.2 LOCAL — run the app
```powershell
.\deploy\scripts\ppiq.ps1 up
# foreground; loads .env.dev via Import-DotEnv; runs --no-launch-profile; listens on :5063
```
`ppiq.ps1` verbs: `up | up-sources | migrate | seed | test | e2e | demo | reset | down | init-db`. `init-db` requires `PPIQ_PG_SUPERPASSWORD`.
First-run note: execution policy may need `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` + `Unblock-File` on repo `*.ps1`.

### 10.3 Role × license matrix (M1-T05 / T06 proof)
Works against EITHER environment — point `$base` at `http://localhost:5063` (local) or `https://api.178.105.152.180.sslip.io` (server). Reads the five users from `.env.dev` (local) or use `sysadmin` (server). `Set-StrictMode -Version 1.0`, `& { }` paste-block, pure ASCII:

```powershell
& {
  Set-StrictMode -Version 1.0
  $ErrorActionPreference = 'Continue'
  $base = 'http://localhost:5063'                     # LOCAL; for SERVER use https://api.178.105.152.180.sslip.io
  $repo = 'C:\Workspace\PlantProcess-IQ'
  $envFile = Join-Path $repo 'deploy\compose\.env.dev'
  $users = @{}
  foreach ($line in (Get-Content $envFile)) {
    if ($line -match '^\s*PlantProcess__Auth__Users__(\d+)__(\w+)\s*=\s*(.*)$') {
      $i=$matches[1]; $k=$matches[2]; $v=$matches[3].Trim()
      if (-not $users.ContainsKey($i)) { $users[$i] = @{} }
      $users[$i][$k] = $v
    }
  }
  function Invoke-Api($method,$url,$body,$token) {
    $headers = @{}; if ($token) { $headers['Authorization'] = "Bearer $token" }
    try {
      $p = @{ Method=$method; Uri=($base+$url); Headers=$headers; TimeoutSec=20; UseBasicParsing=$true }
      if ($null -ne $body) { $p['Body']=($body|ConvertTo-Json -Depth 6); $p['ContentType']='application/json' }
      $r = Invoke-WebRequest @p
      return @{ ok=$true; code=[int]$r.StatusCode; body=$r.Content }
    } catch { $code=-1; if ($_.Exception.Response) { try { $code=[int]$_.Exception.Response.StatusCode } catch {} }
      return @{ ok=$false; code=$code; body="$($_.Exception.Message)" } }
  }
  'ROLE LOGIN MATRIX'; '-----------------'
  $tokens=@{}
  foreach ($i in ($users.Keys | Sort-Object {[int]$_})) {
    $u=$users[$i]
    $r = Invoke-Api 'POST' '/auth/login' @{ userName=$u.UserName; password=$u.Password } $null
    $tok=$null
    if ($r.ok) { try { $j=$r.body|ConvertFrom-Json; $tok=$j.accessToken; if(-not $tok){$tok=$j.token} } catch {} }
    $tokens[$u.UserName]=$tok
    $okstr = if ($tok) {'OK'} else {"FAIL($($r.code))"}
    $tlen  = if ($tok) {"$($tok.Length)ch"} else {'-'}
    '{0,-10} role={1,-10} login={2,-12} token={3}' -f $u.UserName,$u.Role,$okstr,$tlen
  }
  $adminTok=$tokens['admin']
  if (-not $adminTok) { ''; 'ABORT: admin login failed.'; return }
  ''; 'LICENSE TIER ACTIVATION (admin)'; '-------------------------------'
  $licDir = Join-Path $repo 'deploy\fixtures\license'
  foreach ($tier in @('light','pro','proplus','enterprise')) {
    $tf = Join-Path $licDir "$tier.token"
    if (-not (Test-Path $tf)) { '{0,-12} MISSING token file' -f $tier; continue }
    $jws = (Get-Content $tf -Raw).Trim()
    $a = Invoke-Api 'POST' '/api/v5/licensing/ed25519/activate' @{ licenseJws=$jws } $adminTok   # CORRECTED: licenseJws (was token)
    if ($a.ok) { $tn='?'; try { $j=$a.body|ConvertFrom-Json; $tn=$j.tier } catch {}
      '{0,-12} OK   tier={1} code={2}' -f $tier,$tn,$a.code }
    else { $snip=$a.body.Substring(0,[Math]::Min(90,$a.body.Length)); '{0,-12} FAIL code={1} :: {2}' -f $tier,$a.code,$snip }
  }
  ''; 'CURRENT ENTITLEMENTS (after last activation)'; '--------------------------------------------'
  $c = Invoke-Api 'GET' '/api/v5/licensing/ed25519/current' $null $adminTok
  if ($c.ok) { $c.body } else { "failed: code=$($c.code) :: $($c.body)" }
}
```
⚠️ **CORRECTED in v4:** the activation body is `@{ licenseJws=$jws }` (was `@{ token=$jws }`). The login already reads `accessToken` first (correct — confirmed this session).

### 10.4 LOCAL — reset & re-provision
```powershell
$env:PGPASSWORD='ppiq_dev_local_only'
psql -h localhost -U ppiq_dev -d ppiq_app -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public AUTHORIZATION ppiq_dev;"
# then re-run section 0.1 (EF update -> ppiq.ps1 migrate -> ppiq.ps1 up)
```

### 10.5 SERVER — read demo credentials / verify state (do NOT re-provision)
```bash
# sysadmin password (rotates on .env regen)
docker exec ppiq-jenkins sh -lc 'cat /var/lib/ppiq-preserve/FIRST_LOGIN.txt'
docker exec ppiq-jenkins sh -lc 'grep -E "PPIQ_SMOKE_USERNAME|PPIQ_SMOKE_PASSWORD|VITE_API_BASE_URL|PPIQ_PRESENTATION|SITE_HOST" /var/jenkins_home/workspace/plantprocessiq-deploy/deploy/compose/.env'
# confirm only sysadmin in DB + canonical tenant + dev key registered
docker exec plantprocess-postgres psql -U plantprocess -d plantprocessiq -c "SELECT user_name, is_owner, is_enabled, display_name FROM app_users;"
docker exec plantprocess-postgres psql -U plantprocess -d plantprocessiq -c "SELECT id, tenant_code FROM tenants;"
docker exec plantprocess-postgres psql -U plantprocess -d plantprocessiq -c "SELECT key_id, status FROM ppiq_ed25519_license_public_keys;"
# internal health (200) ; containers
docker run --rm --network ppiq-app_plantprocess-private curlimages/curl:8.10.1 -ks -o /dev/null -w 'HTTP %{http_code}\n' http://plantprocess-api:5063/health
docker ps --format '{{.Names}}: {{.Status}}'
```

---

## 11. Deploy pipeline (server)

⚠️ **REWRITTEN in v4** to the current GREEN design (v3 described the pre-rename, single-project design).

**Trigger:** `git push origin main` → GitHub webhook → Jenkins job `plantprocessiq-deploy`.
**Projects:** app = **`ppiq-app`**; infra = **`plantprocessiq`** (SACRED — see section 7.2). The old v3 line "converge to the single live `plantprocessiq` project" is superseded: the canonical APP project is `ppiq-app`; `plantprocessiq` is reserved for infrastructure.

**Jenkinsfile env:** `COMPOSE_PROJECT='ppiq-app'`, `COMPOSE_BASE='deploy/compose/docker-compose.yml'`, `COMPOSE_SERVER='deploy/compose/docker-compose.server.yml'`, `ENV_FILE='deploy/compose/.env'`.

**Stages (confirmed green):**
1. Checkout + ensure-env — `ensure-runtime-env.sh` materializes `deploy/compose/.env` (preserving the persisted password; writing `PPIQ_PRESENTATION=on`, host-derived URLs/CORS, sysadmin smoke creds).
2. Sweep — workspace hygiene.
3. Backend tests (BLOCKING) — SDK sibling (`mcr.microsoft.com/dotnet/sdk:9.0`), ~567 tests (many `[SKIP]`).
4. Frontend tests (BLOCKING) — node sibling (`node:24-alpine sh -lc "set -e; npm ci; npm run test"`), 51 files / 202 tests.
5. E2E — gated off (`PPIQ_RUN_E2E != on`).
6. App DB migrate+seed — sources `.env`, brings up ONLY `plantprocess-postgres`, runs `migrate-and-seed.sh --app-only` (EF in sibling → numbered SQL → seeds → **register dev Ed25519 key when `PPIQ_PRESENTATION=on`**).
7. Demo sources — gated off (`PPIQ_DEMO_SOURCES_MODE=disabled`).
8. Build + recreate canonical stack — `deploy-canonical.sh`: tag images `:previous` (rollback), `dc build` (with `--env-file .env` → VITE args bake in), `dc up -d --remove-orphans`, then **health gate** on `http://plantprocess-api:5063/health` (200, 45 retries, else rollback to `:previous`).
9. Presentation smoke — gated by `PPIQ_PRESENTATION=on`; runs `presentation-smoke.sh` in a curl sibling: sysadmin login (extract `accessToken`) → activate Enterprise (`licenseJws`) → confirm `/current` → "Presentation ready".

**DooD tool-step pattern (server):** the Jenkins agent has NO dotnet/node/npm. Tool steps run in SIBLING containers: `docker run --rm --volumes-from $(cat /etc/hostname) -w "${PWD}" <image> sh -lc "..."`. alpine/curl images = busybox `sh` (no bash). Non-root images (`curlimages/curl`) need `--user 0:0` to read root-owned `.env`.

**Pipeline fixes from the v3 list — STATUS:**
1. `PGCLIENTENCODING=UTF8` before psql — carried (server scripts handle encoding).
2. EF before numbered SQL — DONE on the server (migrate-and-seed runs EF in a sibling first).
3. Jenkinsfile stage-1 hygiene — DONE.
4. Converge orphaned composes — **DONE, but the answer changed:** canonical APP project is `ppiq-app`; infra is `plantprocessiq`; conn key `ConnectionStrings__PlantProcessDb`; ≥64-char SigningKey + deployed `.env`; Caddy via Option 1 (infra fronts app); orphans retired.

(Full forensic detail — every commit hash and root cause — lives in the separate **PPIQ Deploy Pipeline Handover** document.)

---

## 12. Quarantined scripts & the lost foundation (LOCAL/decoration — unchanged)

`ppiq.ps1 migrate` globs `Backend/database/scripts/*.sql` sorted; `*.sql.quarantine` files are skipped. Six scripts quarantined:

| Quarantined | Why |
|---|---|
| `310_p03_p04_mapping_genealogy_foundation.sql` | Corrupted: a PowerShell generator wrapper saved over the file; its here-string body duplicates the 312 pack. **The real foundation is lost.** |
| `311_p03_p04_fix_genealogy_walk_and_safe_sql.sql` | Depends on the lost 310 foundation. |
| `312_p03_p04_completion_pack_a.sql` | Validators over the lost foundation tables. |
| `313_p03_p04_completion_pack_a_hotfix.sql` | Same. |
| `321_p3_golden_thread_and_missing_hop.sql` | Calls `ppiq_walk_genealogy(...)` — defined only in the lost 310/311. |
| `511_v5_p02_hotpath_explain_review.sql` | A manual EXPLAIN-review helper, wrongly auto-run; assumes `tenant_id` on core EF tables. Also `710_phase04_second_tenant_seed.sql` quarantined (multi-tenant test data). |

**LOST FOUNDATION (reconstruction task — model-first):** nothing now creates `ppiq_business_key_definitions`, `canonical_business_keys`, `canonical_mapping_versions`, the canonical genealogy tables, or `canonical_downtime_events`. Git history has no clean copy (squashed out). Gone: cross-source business-key dictionary/reconciliation, `ppiq_walk_genealogy` + genealogy validators, the typed safe-SQL resolver (`ppiq_resolve_safe_sql`), mapping lifecycle dry-run/publish/rollback proofs, golden-thread / downtime-value-impact proof views. `genealogy_edges` survives (EF-owned). Backlog: **"mapping / genealogy / safe-SQL foundation rebuild (model-first)."** Not on the M1 path.

---

## 13. Drift fixes applied (LOCAL decoration — unchanged)

The decoration layer has a recurring pattern: the same table created by two scripts with divergent columns; `CREATE TABLE IF NOT EXISTS` skips the newer definition, then an index/query on the new column fails. Fixed in place (idempotent ALTER-before-index):

| Script | Fix |
|---|---|
| `420_p3_value_evidence_hmi.sql` | `ALTER TABLE canon.cost_assumption ADD COLUMN IF NOT EXISTS effective_from_utc timestamptz NOT NULL DEFAULT now();` before its index. Also needs `PGCLIENTENCODING=UTF8` (German+Arabic i18n strings). |
| `540_v5_p05_visual_mapper_foundation.sql` | `ADD COLUMN IF NOT EXISTS source_code text NOT NULL DEFAULT ''` + `detected_at_utc timestamptz NOT NULL DEFAULT now()` on `public.ppiq_schema_drift_events` before its index. |
| `700_phase03_readonly_preview_role.sql` | Hardcoded server DB name + placeholder login password → generic `EXECUTE format('GRANT CONNECT ON DATABASE %I ...', current_database())` and a `NOLOGIN PASSWORD NULL` role (via `SET ROLE`). |

---

## 14. Pending consolidation & commit list

### 14.1 LOCAL items (still pending on the laptop)
- [ ] `.env.dev` token-free SigningKey (done — commit it).
- [ ] `PlantProcessDesignTimeDbContextFactory` rewrite (key order + honest error).
- [ ] EF re-baseline (`InitialBaseline` + `AuditAppendOnlyTriggers`) — commit.
- [ ] `PGCLIENTENCODING=UTF8` baked into `ppiq.ps1`, `migrate-and-seed.sh`, Jenkinsfile.
- [ ] `ppiq.ps1 migrate`/`demo`: run `dotnet ef database update` before the numbered SQL.
- [ ] The 420 / 540 / 700 script edits — commit.
- [ ] The six `*.sql.quarantine` renames — commit; lost-foundation rebuild tracked.
- [ ] Cleanup deletions: `docker-compose.demo-sources*.yml`, `env/profiles/local.env`, `scripts/docker/start|stop-demo-sources.ps1`, `.ppiq-script-backups/`, `tools/archive/`, `tools/_archive/`; rotate any leaked key.
- [ ] Write 5 users + DSN into `appsettings.Development.json`; strip empty `ConnectionStrings__PlantProcessDb` from all three `launchSettings` profiles (closes T02).
- [ ] Add `HasDefaultSchema` mapping EF entities into `ppiq_meta` / `ppiq_plant`.
- [ ] Unify container/network names to `plantprocess-*` (local).

### 14.2 ⭐ SERVER items — COMMITTED this session (done; commit hashes in the deploy handover)
- [x] App project rename `plantprocessiq` → `ppiq-app` (stops `--remove-orphans` reaping infra).
- [x] Option-1 Caddy (drop app-stack Caddy; infra `ppiq-caddy` fronts both; api+web join `ppiq-edge`).
- [x] `env_file: [.env]` on `plantprocess-api` (full `.env` reaches the container).
- [x] Health endpoints anonymous at BOTH layers (authorization policy `AllowAnonymous` + AccessControl matrix `"anonymous", true`).
- [x] `sysadmin` first-run provisioning (`FirstRunProvisioningHostedService` rewritten + registered via `AddHostedService`).
- [x] Remove `admin`/`e2eadmin` test-user seeding from production `301` script (keep canonical tenant).
- [x] Smoke login field `accessToken`; activation field `licenseJws`.
- [x] `PPIQ_PRESENTATION=on` written to `.env`; dev-key registration gate allows it.
- [x] Frontend VITE build args (`VITE_SMOKE_*`) + host-derived URLs/CORS from `PPIQ_SITE_HOST`.
- [x] Space-free `DisplayName` (`PPIQ-System-Administrator`) so `. .env` sources cleanly.

### 14.3 ⭐ SERVER items — STILL OPEN
- [ ] Option-1 → Option-3: real production Ed25519 license signing keypair; never ship the dev key to a real customer; real customer frontends must not bake `VITE_SMOKE_*`.
- [ ] Persistent infra Caddyfile host-bind + corrected route targets (`plantprocess-web`/website); do not recreate `ppiq-caddy` until fixed.
- [ ] Wrap `FirstRunProvisioningHostedService` provisioning in try/catch (resilience).
- [ ] Generator: preserve `POSTGRES_PASSWORD` across stale-key regen (remove the `.env`/volume sharp edge).
- [ ] Move Jenkins to a separate Docker network from the app project.
- [ ] Descriptive renaming of `p01_p02` / `v5_p0x` SQL files (naming golden rule, section 15).

---

## 15. Naming golden rule (permanent — both environments)

**Never** name any file, script, component, page, class, function, endpoint, table, or artifact with phase numbers, task numbers, milestone/sprint IDs, version-phase tags, or codes (`P02`, `phase2`, `T053`, `p03_p04`, `v6`, `hotfix`, …). Names describe **function/purpose only**. Two directives: (1) never emit such a name again; (2) when one is encountered during development/enhancement/cleanup, rename it to a representative name and update references.

**Ordering nuance:** a leading numeric prefix that purely controls execution order (ordered SQL scripts sorted by filename; EF migration timestamps) is a functional ordering token — preserve it, strip the embedded phase/task label, make the rest descriptive.

Representative-rename mapping (examples; full folder pass pending in clean-rebuild):

| Current | Rename to |
|---|---|
| `310_p03_p04_mapping_genealogy_foundation.sql` | `310_mapping_genealogy_foundation.sql` |
| `300_p01_p02_security_access_control_spine.sql` | `300_security_access_control_spine.sql` |
| `301_p01_p02_authstore_compatibility_bridge.sql` | `301_authstore_compatibility_bridge.sql` |
| `302_p02_authstore_runtime_lineage_lock.sql` | `302_authstore_runtime_lineage_lock.sql` |
| `203_phase02_ml_compute_v6_wrapper_hotfix.sql` | `203_ml_correlation_compute.sql` |
| `400_phase4_value_engine.sql` | `400_value_engine.sql` |
| `420_p3_value_evidence_hmi.sql` | `420_value_evidence_hmi.sql` |
| `540_v5_p05_visual_mapper_foundation.sql` | `540_visual_mapper_foundation.sql` |
| `650_remaining_p10_ed25519_verified_license.sql` | `650_ed25519_verified_license.sql` |
| `665_pack_b35_mostly_green_task_closure.sql` | (delete — task-closure bookkeeping, not schema) |
| `700_phase03_readonly_preview_role.sql` | `700_readonly_preview_role.sql` |

The Vx-prefixed endpoint groups fall under the same rule.

---

## 16. Known issues & clean-rebuild backlog

### 16.1 Carried from v3 (still valid)
1. **Duplicate-definition drift (7 tables):** `assistant_chunk`, `cost_assumption`, `dashboard_widget_expression_audit`, `page_definitions`, `ppiq_i18n_string_keys`, `ppiq_i18n_translations`, `ppiq_schema_drift_events`. Clean-rebuild: one canonical definition per table (model-first).
2. **`migrate` globs non-migration scripts** (manual helpers `511`, demo seeds, two-tenant probes `710`/`720`, test auth seeds `900`, `mostly_green_task_closure`). They belong to other verbs/`tools/`/`tests/`.
3. **Tenant scoping undecided.** V5 RLS assumes every table has `tenant_id`; core EF tables do not. Decide deliberately: RLS-tenant-scoped core vs single-tenant-per-deployment. Required for true multi-tenant; not an M1 blocker.
4. **i18n as raw SQL seeds** (`420`/`425`/`610`). Clean-rebuild: locale resource files, not SQL (also encoding-damaged).
5. **EF tables land in `public`** (no `HasDefaultSchema`).

### 16.2 ⭐ NEW server-side issues (this session)
6. **Stale infra Caddyfile targets** (`plantprocess-app-web`/`plantprocess-website`) — works but fragile; orphaned inode; needs persistent host-bind + correct targets. Do not recreate `ppiq-caddy` until fixed.
7. **`.env` ↔ Postgres-volume password coupling** — operator must not delete the persisted `.env`; generator should preserve `POSTGRES_PASSWORD` across regen.
8. **Dev license key in demo "Production"** (Option-1) — move to a real production signing keypair (Option-3); don't bake `VITE_SMOKE_*` into real customer frontends.
9. **Provisioning not wrapped in try/catch** — a provisioning failure could crash the API at startup; wrap it.
10. **Jenkins shares a Docker network with the app project** — move Jenkins to a separate network (defense in depth).

---

## 17. ⭐ NEW — Server & Jenkins host access / credentials

> **SENSITIVE.** This section names how to reach the server, Jenkins, GitHub, and the demo. **Do NOT commit real secrets to git.** The placeholders below are intentional — fill them in your own private copy, and keep the real secret values in a SEPARATE private store (a password manager or a git-ignored secrets file referenced here), so this topology reference itself stays shareable. **Never paste real passwords/keys into a tracked document.**

### 17.1 Hetzner VPS (the server)
| Item | Value |
|---|---|
| Public IP | `178.105.152.180` |
| OS | Ubuntu |
| SSH user | `root` |
| SSH auth method | _[record: key path or "password"; store the private key OUTSIDE git]_ |
| SSH command | `ssh root@178.105.152.180` _(add `-i <keyfile>` if key-based)_ |
| Hetzner console login | _[your Hetzner account — store in password manager]_ |

### 17.2 Jenkins (CI/CD)
| Item | Value |
|---|---|
| URL | `https://jenkins.178.105.152.180.sslip.io/` |
| Job | `plantprocessiq-deploy` |
| Container | `ppiq-jenkins` (infra project `plantprocessiq`) |
| Admin user | _[record username]_ |
| Admin password | _[store in password manager — NOT here]_ |
| Trigger | GitHub webhook on push to `main` (also "Build Now" in the UI) |
| Workspace path | `/var/jenkins_home/workspace/plantprocessiq-deploy` |
| Jenkins home (host) | `/data/plantprocess-iq/jenkins/` |

### 17.3 GitHub repository
| Item | Value |
|---|---|
| Repo | `github.com/kgamal369/PlantProcess-IQ` |
| Default branch | `main` |
| Release flow | `git push origin main` → webhook → Jenkins |
| Local clone | `C:\Workspace\PlantProcess-IQ` |
| Commit gate (local) | commits only when `$env:PPIQ_COMMIT='1'` |

### 17.4 Demo application login (sysadmin)
| Item | Value |
|---|---|
| URL | `https://app.178.105.152.180.sslip.io` |
| Username | `sysadmin` |
| Password | **rotates on `.env` regen** — read live: `docker exec ppiq-jenkins sh -lc 'cat /var/lib/ppiq-preserve/FIRST_LOGIN.txt'` (also `PPIQ_SMOKE_PASSWORD` in `.env`) |
| Role | System Owner (support only) — customer must never use it |

### 17.5 Server database (app)
| Item | Value |
|---|---|
| Container | `plantprocess-postgres` (project `ppiq-app`) |
| Database | `plantprocessiq` |
| User | `plantprocess` |
| Password location | `/var/lib/ppiq-preserve/.env` (key `POSTGRES_PASSWORD`) — **do not delete this file** (section 2.4) |
| psql in | `docker exec -it plantprocess-postgres psql -U plantprocess -d plantprocessiq` |

### 17.6 Persisted secrets & host override
| Item | Value |
|---|---|
| Persisted `.env` (server) | `/var/lib/ppiq-preserve/.env` — **DO NOT DELETE** (password coupling, section 2.4) |
| First-login file | `/var/lib/ppiq-preserve/FIRST_LOGIN.txt` (inside `ppiq-jenkins`) |
| Public-host override | set `PPIQ_SITE_HOST=<your-domain>` to move off sslip.io; then `CADDY_AUTO_HTTPS=on`, `ACME_EMAIL=<address>` for real TLS |
| Spamhaus/mail note | the VPS IP got a Hetzner abuse notice (Spamhaus, 23-Jun-2026); mail must go via an authenticated relay (587/465) with SPF+DKIM+PTR and port 25 blocked — never send mail directly from the VPS IP |

---

## 18. Quick reference card — local vs server at a glance

| | LOCAL (dev) | SERVER (release/demo) |
|---|---|---|
| API | `http://localhost:5063` | `https://api.178.105.152.180.sslip.io` (int. `plantprocess-api:5063`) |
| UI | Vite `:5173` | `https://app.178.105.152.180.sslip.io` |
| Main DB | native `localhost:5432` / db `ppiq_app` / `ppiq_dev` | container `plantprocess-postgres` / db `plantprocessiq` / `plantprocess` |
| Conn key | `ConnectionStrings__PlantProcessDb` (NOT DefaultConnection) | same |
| Login users | 5 dev-seed config users (`.env.dev`) | DB-backed `app_users`; `sysadmin` + manual customer admin |
| Secrets | `deploy/compose/.env.dev` (committed, local-only) | `/var/lib/ppiq-preserve/.env` (git-ignored, persisted) |
| Deploy | `ppiq.ps1` verbs | `git push` → Jenkins `plantprocessiq-deploy` |
| Compose projects | native + `ppiq-sources` | infra `plantprocessiq` + app `ppiq-app` |
| Activate body | `{ "licenseJws": "<JWS>" }` | same |
| Presentation flag | n/a | `PPIQ_PRESENTATION=on` (demo only) |

---

*End of v4. This reference is the single source of truth for PPIQ identity, topology, and the provision/deploy path across BOTH the local development environment and the live server. Local and server are both first-class and permanently documented side by side — neither replaces the other. Reconcile any divergence against committed files before acting. Real secrets live only in a private store, never in this document.*
