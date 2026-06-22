# PlantProcess IQ — Identity, Credentials, Topology & Run Reference (v2)

**Authoritative single source of truth.** Every value is read from the committed repo. Nothing is assumed. Local-dev credentials are non-secret and committed (in `deploy/compose/.env.dev`); real secrets live **only** in git-ignored `.env` files on the server/customer host. The same files deploy identically to laptop, server, and every customer — only the env values differ.

**Contents:** §1 Port map · §2 App database (2 schemas) · §3 Auth & JWT · §4 App users · §5 Demo sources · §6 Licenses · §7 Containers · §8 Frontend config · §9 Env profiles & customer modes · §10 Per-level run commands · §11 Local role×license matrix · §12 Deploy pipeline · §13 Cleanup checklist.

---

## §1. Complete port map (loopback unless noted)

| Service | Engine | Host port (local) | Container/internal port | Bind |
|---|---|---|---|---|
| App database | PostgreSQL 16 | `5432` | `5432` | `127.0.0.1` (server: internal only) |
| Backend API | .NET 9 / Kestrel | `5063` | `5063` | `127.0.0.1` (server: behind Caddy) |
| Frontend (HMI) | React/Vite | `5173` | `80` | `127.0.0.1` |
| Vite preview | Vite | `4173` | — | local |
| Website | React/Vite | `5080` | `80` | server |
| Caddy (server) | reverse proxy | `80` / `443` | — | public (server overlay only) |
| Demo · Melt shop | PostgreSQL 16 | `15432` | `5432` | `127.0.0.1` |
| Demo · Caster | Oracle Free 23 | `11521` | `1521` | `127.0.0.1` |
| Demo · HSM | Oracle Free 23 | `11522` | `1521` | `127.0.0.1` |
| Demo · PKL | SQL Server 2022 | `11433` | `1433` | `127.0.0.1` |
| Demo · Downtime | MySQL 8 | `13306` | `3306` | `127.0.0.1` |
| Demo · Parsytec | MySQL 8 | `13307` | `3306` | `127.0.0.1` |

---

## §2. Application database — the two schemas

One Postgres instance, two schemas: **`ppiq_meta`** (app metadata: dashboards, widgets, jobs, pages, users, roles, license) and **`ppiq_plant`** (customer data after the staging→conversion transform, RLS per tenant). Created idempotently by `Backend/database/scripts/000_schemas.sql`.

> ⚠️ **Model gap (clean-rebuild item):** no `HasDefaultSchema` in the DbContext → EF domain tables currently land in **`public`**, not in `ppiq_meta`/`ppiq_plant`. Mapping entities into the two schemas is part of the model-first rebuild.

### Credentials by run mode

| Mode | Host | Port | DB | User | Password | Defined in |
|---|---|---|---|---|---|---|
| **Local — native** (`dotnet run`, tests) | `localhost` | `5432` | `ppiq_app` | `ppiq_dev` | `ppiq_dev_local_only` | `deploy/compose/.env.dev` |
| **Local — containerised** (`ppiq.ps1 up`) | `plantprocess-postgres` → `127.0.0.1:5432` | `5432` | `ppiq_app` | `ppiq_dev` | `ppiq_dev_local_only` | `.env.dev` + `docker-compose.local.yml` |
| **Server** | `postgres` (service) / `ppiq-postgres` (container) | `5432` | `plantprocessiq` | `plantprocess` | *(git-ignored `.env`)* | `env/profiles/server.env.example` → real `.env` |
| **Customer** | per `PPIQ_MAIN_DB_MODE` (`native`/`docker`/`external`/`managed`) | per customer | per customer | per customer | *(git-ignored)* | `env/profiles/customer-<name>.env` |

> ⚠️ **Two drifts to reconcile:** (a) local uses `ppiq_dev`/`ppiq_app`, server uses `plantprocess`/`plantprocessiq` — fine *only* because the DSN is built from `POSTGRES_*` env vars, never hardcoded; (b) `server.env` sets `POSTGRES_HOST=postgres` but the compose service is `plantprocess-postgres` — the API reaches the DB only if the service has a `postgres` alias or `ConnectionStrings__PlantProcessDb` is set explicitly. Unify the service name and the host var.

### Connection string (env form; ASP.NET maps `__` → `:`)
```
ConnectionStrings__PlantProcessDb=Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only
ConnectionStrings__PlantProcessDb=…same…    # legacy mirror only; the app reads PlantProcessDb
PPIQ_TEST_CONNECTION_STRING=Host=localhost;Port=5432;Database=plantprocess_test_db;Username=ppiq_dev;Password=ppiq_dev_local_only
PPIQ_AUDIT_TRIGGER_TEST_CONNECTION=…test DSN…  # audit-immutability tests
```
The app **requires `ConnectionStrings__PlantProcessDb`** (validated at startup); the test suite requires a **separate** `plantprocess_test_db`.

### Native role/DB bootstrap (one-time, documented — not a per-run env var)
`ppiq.ps1 init-db` creates/aligns the `ppiq_dev` role + `ppiq_app` DB from `.env.dev`. It currently needs the Postgres **superuser** password once (`PPIQ_PG_SUPERPASSWORD`). For a product this should become a committed `000_bootstrap_role.sql` run by the superuser at install time, so no human ever sets that variable per run.

---

## §3. Authentication & JWT

| Setting | Local (`.env.dev`) | Server/Customer (git-ignored `.env`) |
|---|---|---|
| `PlantProcess__Auth__SigningKey` | `DEV_ONLY_local_signing_key_do_not_use_in_prod_0123456789abcdef0123456789` (≥64, `DEV_ONLY_` accepted in Development, **rejected** in Production) | real ≥64-char key |
| `PlantProcess__Auth__Issuer` | `plantprocess-iq` | same |
| `PlantProcess__Auth__Audience` | `plantprocess-iq-clients` | same |
| `PlantProcess__Auth__RequireAdminMfa` | `false` | per policy |
| `PlantProcess__Auth__BootstrapAdminUser` | `bootstrap-disabled` | `customer-owner` (first-run only) |
| `PlantProcess__Auth__BootstrapAdminPassword` | `__DISABLED__` | git-ignored, first-run only |
| Password hash | **Argon2id**, 64 MB (`Argon2MemoryKb=65536`), iterations `3`, parallelism `1` | same |

Login issues a JWT bearer; the role claim drives authorization. `__Host-` cookies + in-memory tokens on the client.

---

## §4. Application users (login identities)

✅ **Canonical five role users** — `deploy/compose/.env.dev`, `PlantProcess__Auth__Users__N`; hashed with Argon2id on load:

| Idx | Username | Password | Role | Typical scope |
|---|---|---|---|---|
| 0 | `admin` | `DevAdmin123!` | **Admin** | everything incl. `/admin/*`, license activation |
| 1 | `exec` | `DevExec123!` | **Executive** | exec dashboards, value/ROI views |
| 2 | `engineer` | `DevEng123!` | **Engineer** | investigation, correlation, dashboards |
| 3 | `operator` | `DevOp123!` | **Operator** | operational dashboards, read |
| 4 | `viewer` | `DevView123!` | **Viewer** | read-only |

- **Login:** `POST /auth/login` → body `{ "userName": "admin", "password": "DevAdmin123!", "requestedRole": null }` → `200` + bearer + role claim.
- **Bootstrap is disabled** — no user is created at runtime.

❌ **Delete the stale 4-user set** in `appsettings.Development.json` (`admin/Admin123!`, `engineer/Engineer123!`, `datamanager/DataManager123!`, `viewer/Viewer123!`). Bare `dotnet run` reads *that*, which is why role tests disagree. **Fix:** write these five (and the DSN) into `appsettings.Development.json` and remove the empty `ConnectionStrings__PlantProcessDb` from all three `launchSettings.json` profiles so both run paths see one identical identity.

---

## §5. Demo-source emulators (one emulated customer's systems)

✅ **Canonical:** `deploy/compose/docker-compose.sources.yml` — project **`ppiq-sources`**, network **`ppiq-sources`**, loopback ports, deterministic SQL seeds under `deploy/fixtures/demo/<source>/init/`.

| # | Source | Engine | Container | Host port | DB / service | User | Password | Override env |
|---|---|---|---|---|---|---|---|---|
| 1 | Melt shop (heats) | PostgreSQL 16 | `ppiq-src-meltshop-postgres` | `15432` | `meltshop` | `ppiq_src` | `ppiq_src_local_only` | `SRC_PG_USER/PASSWORD/DB` |
| 2 | Caster (casts) | Oracle Free 23 | `ppiq-src-caster-oracle` | `11521` | app user `ppiq_src` | `ppiq_src` | `ppiq_src_local_only` | `SRC_ORA_USER/PASSWORD` |
| 3 | HSM (coils/surface) | Oracle Free 23 | `ppiq-src-hsm-oracle` | `11522` | app user `ppiq_src` | `ppiq_src` | `ppiq_src_local_only` | `SRC_ORA_USER/PASSWORD` |
| 4 | PKL (pickling line) | SQL Server 2022 | `ppiq-src-pkl-mssql` (+ `…-init`) | `11433` | master/seeded | `sa` | `Ppiq_Src_Local_Only1` | `SRC_MSSQL_PASSWORD` |
| 5 | Downtime | MySQL 8 | `ppiq-src-downtime-mysql` | `13306` | `downtime` | `ppiq_src` (root `ppiq_src_root_local`) | `ppiq_src_local_only` | `SRC_MYSQL_USER/PASSWORD/ROOT` |
| 6 | Parsytec (surface) | MySQL 8 | `ppiq-src-parsytec-mysql` | `13307` | `parsytec` | `ppiq_src` (root `ppiq_src_root_local`) | `ppiq_src_local_only` | `SRC_MYSQL_USER/PASSWORD/ROOT` |
| 7 | Yard inventory (~5,600 coils) | CSV file | *(file mount, no container)* | — | `deploy/fixtures/demo/excel-yard/yard_inventory.csv` | — | — | — |
| 8 | QA samples (~1,868) | CSV file | *(file mount, no container)* | — | `deploy/fixtures/demo/excel-qa/qa_samples.csv` | — | — | — |

- **Bring up:** `ppiq.ps1 up-sources`. First boot runs each engine's init scripts on the empty volume; counts are deterministic (≈630 heats, ≈5,670 coils, ≈1,868 QA).
- **Connector tier gate:** CSV/Excel = Light · PostgreSQL = Pro · Oracle/SQL Server/MySQL/REST/OPC-UA = **Enterprise**. A full 8-source demo therefore needs an **Enterprise** token (§6).

❌ **Delete the duplicate** `docker-compose.demo-sources.yml` (project `plantprocessiq-demo-sources`, containers `ppiq-source-*`, per-source owners `meltshop_owner`/`caster_owner`, image `oracle-xe:21`, network `ppiq-demo-sources`, volumes `ppiq_*_data`) and `…demo-sources.ports.yml`.

> ⚠️ The canonical `sources.yml` header admits its Oracle/MSSQL/MySQL blocks were reconstructed. **Before deleting the other file, confirm the app's seeded ConnectionProfiles + the `init/001_schema_seed.sql` GRANTs match the `ppiq_src` credential scheme**, then keep exactly one.

---

## §6. Licenses — tiers & activation

Ed25519-**signed tokens**, not editable rows. Dev fixtures in `deploy/fixtures/license/` (key id `ppiq-dev-ed25519`, tenant `00000000-0000-0000-0000-000000000001`, public key `dev_public.pem` / `publicKeyB64=DnycfAUUX263chT9G2UHQ6gbI6HUe5dX8W5KQL8E/Ss=`).

| Tier (enum) | Token | License key | Max users | Max sources | Max jobs | Max dashboards | SQL editor | KPI builder | Widget script | Sched. corr. | ML jobs | Branded reports |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Light** (1) | `light.token` | `PPIQ-DEV-LIGHT` | 3 | 1 | 1 | 3 | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Pro** (2) | `pro.token` | `PPIQ-DEV-PRO` | 10 | 3 | 5 | 8 | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **ProPlus** (3) | `proplus.token` | `PPIQ-DEV-PROPLUS` | 25 | 8 | *(LicenseService.cs)* | *(LicenseService.cs)* | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| **Enterprise** (4) | `enterprise.token` | `PPIQ-DEV-ENTERPRISE` | unlimited | unlimited | unlimited | unlimited | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

**Feature → minimum tier (selected):** read-only registry / CSV / Excel / dashboard builder / basic DQ = Light; PostgreSQL connector / incremental import / SQL-view builder / cross-source joins / manual correlation / full DQ / basic PDF = Pro; KPI builder / widget script / scheduled correlation / ML jobs / investigation workflow / risk contributors / full genealogy PDF = ProPlus; SQL Server / Oracle / MySQL / REST / OPC-UA / branded reports = Enterprise.

The `*.token` files are **EdDSA JWS** strings (`eyJhbG…`), consumed by the **V5 Ed25519** endpoints (group `/api/v5/licensing/ed25519`), not the Phase10 envelope endpoint. **Activate a tier** (needs Admin bearer — log in as `admin` first):
1. `POST /api/v5/licensing/ed25519/verify-offline` with the JWS token → returns the token's tier (the M1-T06 "verify TRUE" check, 4×).
2. `POST /api/v5/licensing/ed25519/activate` with the JWS token → activates that tier for the tenant (persists to `ppiq_ed25519_activated_licenses`).
3. `GET /api/v5/licensing/ed25519/current` → confirms the active tier; `POST /api/v5/licensing/ed25519/entitlement-check` with a feature → allowed/denied for the active tier.
4. **Tamper proof:** `entitlement-check` explicitly *ignores* any `DbTierOverride`, and `Phase5_LicenseTierTamperTests` proves editing the DB tier row changes nothing — the effective tier is the signed token only. (The admin UI's `/admin/license/effective-tier` reflects a soft override for display, but enforcement uses the activated signed token.)

> Exact request field names for the Ed25519 activate/verify/entitlement requests live in `V5Ed25519LicenseEndpoints.cs` (records `Ed25519ActivateLicenseRequest` / `Ed25519OfflineVerifyRequest` / `Ed25519EntitlementCheckRequest`) — confirm the token field name before scripting.

> ⚠️ Production uses SOU's **real** Ed25519 key — never `dev_private.pem` (a committed dev-only fixture; never sign production tokens with it).

---

## §7. Docker containers — canonical set

### App stack — project `plantprocessiq`
`deploy/compose/docker-compose.yml` (base) + `docker-compose.local.yml` / `docker-compose.server.yml`. Network `plantprocess-private`.

| Container | Role | Local port |
|---|---|---|
| `plantprocess-postgres` | app DB (two schemas) | `127.0.0.1:5432` |
| `plantprocess-api` | .NET 9 API | `127.0.0.1:5063` |
| `plantprocess-web` | React/Vite HMI | `127.0.0.1:5173`→80 |
| `plantprocess-caddy` | reverse proxy (server overlay) | 80/443 |

### Sources stack — project `ppiq-sources` (network `ppiq-sources`)
`ppiq-src-meltshop-postgres`, `ppiq-src-caster-oracle`, `ppiq-src-hsm-oracle`, `ppiq-src-pkl-mssql` (+ `ppiq-src-pkl-mssql-init`), `ppiq-src-downtime-mysql`, `ppiq-src-parsytec-mysql`.

> ⚠️ **Naming drift:** the Caddyfile + `deploy/scripts/*.sh` default to `ppiq-app-api` / `ppiq-postgres` / `ppiq-network`, but the compose creates `plantprocess-api` / `plantprocess-postgres` / `plantprocess-private`. The server deploy works only if `.env` overrides `PPIQ_API_UPSTREAM`, `DB_CONTAINER`, `HEALTH_TARGET`, `HEALTH_NETWORK`. **Unify on the `plantprocess-*` names** (or set them once in `.env`).

---

## §8. Frontend configuration

| Var | Local (`.env.dev` / `.env.development`) | Server (`server.env`) |
|---|---|---|
| `VITE_API_BASE_URL` | `/api` (proxied) | `https://api.plantprocessiq.com` |
| `VITE_WEBSITE_API_BASE_URL` | `/api` | `https://api.plantprocessiq.com` |
| `VITE_HOST` / `VITE_PORT` | `localhost` / `5173` | `0.0.0.0` / `5173` |
| `VITE_PREVIEW_PORT` | `4173` | `4173` |

CORS allow-list comes from `PLANTPROCESS_ALLOWED_ORIGINS` (local: `http://localhost:5173,http://localhost:5174`; server: `https://app.plantprocessiq.com,https://plantprocessiq.com`).

---

## §9. Environment profiles & customer deployment modes

| File | Purpose | Secrets |
|---|---|---|
| `deploy/compose/.env.dev` | committed local dev (non-secret) | none |
| `deploy/compose/.env` | server/customer runtime | **git-ignored** |
| `env/profiles/server.env.example` | server template | placeholders only |
| `env/profiles/customer-template.env.example` | per-customer template | placeholders only |
| `env/profiles/test.env.example` | CI/test template | placeholders only |

**Customer modes (`customer-template`):** `PPIQ_MAIN_DB_MODE = native | docker | external | managed`; `PPIQ_DEMO_SOURCES_MODE = docker | external | disabled | mixed`; `PPIQ_START_MAIN_DB` / `PPIQ_START_DEMO_SOURCES` toggles. `PPIQ_BOOTSTRAP_ADMIN_USER=customer-owner` provisions the first admin on first run only. A customer changes only their `POSTGRES_*`, domains, signing key, and bootstrap admin — nothing in code.

---

## §10. Per-level run commands (no env juggling)

> `ppiq.ps1` lives at `deploy/scripts/ppiq.ps1`. Invoke it as `.\deploy\scripts\ppiq.ps1 <verb>` (or add a one-line root wrapper `ppiq.ps1` that dot-sources it, so `.\ppiq.ps1 demo` works from repo root).

| Level | Command | Result |
|---|---|---|
| **Dev — full seeded stack** | `.\deploy\scripts\ppiq.ps1 demo` | loads `.env.dev`, brings up sources + app DB, migrates, seeds, starts API+web. Zero shell vars. |
| **Dev — native hot reload** | `.\deploy\scripts\ppiq.ps1 up` *(or `dotnet run … --no-launch-profile`)* | API on `5063`, web on `5173` |
| **One-time DB bootstrap** | `.\deploy\scripts\ppiq.ps1 init-db` | creates/aligns `ppiq_dev` + `ppiq_app` |
| **Test (all)** | `.\deploy\scripts\ppiq.ps1 test` | `dotnet test` + `vitest run` |
| **E2E** | `.\deploy\scripts\ppiq.ps1 e2e` | `playwright test` |
| **Reset** | `.\deploy\scripts\ppiq.ps1 reset` | tear down volumes, rebuild clean |

> 🔧 **Required `migrate` fix (model-first ordering):** today `ppiq.ps1 migrate` runs only the numbered SQL scripts, which depend on EF tables that don't exist yet. It must run **`dotnet ef database update` first**, then the post-EF SQL. See §12.

---

## §11. Local run & test matrix — every role × every license (your ask #2)

The app runs once; you exercise **role** by logging in as a different user (§4) and **license** by activating a different signed token (§6). For a **customer presentation, run at the top of both axes** so everything is visible:

```powershell
# presentation default: Enterprise license + admin — shows every feature
.\deploy\scripts\ppiq.ps1 demo -License enterprise
# then log in to the HMI as `admin` / `DevAdmin123!`
```

To **systematically test all 20 combinations** (5 roles × 4 tiers), the `matrix` verb (delivered in the updated `ppiq.ps1`): with the app up, for each tier activate its token as `admin`, then for each role log in and assert the gated features match the expected tier. The expected grid:

| Feature ↓ \ Tier → | Light | Pro | ProPlus | Enterprise |
|---|---|---|---|---|
| CSV / Excel import, dashboards | ✓ | ✓ | ✓ | ✓ |
| PostgreSQL connector, SQL editor, manual correlation | ✗ | ✓ | ✓ | ✓ |
| KPI builder, widget script, scheduled correlation, ML jobs | ✗ | ✗ | ✓ | ✓ |
| Oracle/MSSQL/MySQL/REST/OPC-UA connectors, branded reports | ✗ | ✗ | ✗ | ✓ |

Role expectation is orthogonal: `admin` reaches `/admin/*` and license activation; `exec/engineer/operator/viewer` are denied `/admin/*` (403) and see role-scoped dashboards. A `matrix` run prints a 5×4 PASS/FAIL grid so a regression in either axis is caught in one command. (Activation posts each JWS `*.token` to `POST /api/v5/licensing/ed25519/activate`; gating is read via `POST /api/v5/licensing/ed25519/entitlement-check`.)

---

## §12. Deploy pipeline — server & any customer (your ask #1)

Your current `Jenkinsfile` already does: checkout (preserve `.env`/Caddyfile) → sweep → **dotnet test (BLOCK)** → **npm test (BLOCK)** → **e2e (BLOCK)** → migrate+seed → build+deploy with health-gate + rollback, and a T05 guard test forbids `catchError(SUCCESS)`/`--list`. The accompanying `Jenkinsfile` deliverable adds the missing pieces for your seven requirements:

1. **Main app DB — structure + latest data.** Split migrate into **EF first, SQL second**: `dotnet ef database update` (model-derived schema, incl. `audit_log_entries`) → then the numbered `Backend/database/scripts/*.sql` as guarded post-EF decoration → then seed the latest committed dataset. This removes the ordering 42P01 you hit.
2. **Demo DBs (server).** Bring up `docker-compose.sources.yml`, apply each engine's `init/` seeds, regenerate the deterministic fixtures — only when `PPIQ_DEMO_SOURCES_MODE != disabled`.
3–5. **All tests blocking:** `dotnet test Backend`, `npm run test`, `npm run e2e` (each gates the deploy).
6. **Build + install + run:** `npm ci` + build, `dotnet publish`, recreate the canonical stack `--remove-orphans` with a `/health` gate and auto-rollback to the previous image.
7. **Presentation defaults:** a post-deploy step activates the **Enterprise** token for the demo tenant and smoke-verifies **`admin`** login, so the live URL shows every feature to the customer.

Generic across server and customer: everything reads `POSTGRES_*`, domains, signing key, bootstrap admin, and the two `PPIQ_*_MODE` toggles from the git-ignored `.env`; no credential or DB target is hardcoded.

---

## §13. Cleanup checklist (delete the mess)

- [ ] Delete `deploy/compose/docker-compose.demo-sources.yml` + `…demo-sources.ports.yml` (duplicate source stack).
- [ ] Delete `env/profiles/local.env` (keep `local.env.example`).
- [ ] Delete `scripts/docker/start-demo-sources.ps1` + `stop-demo-sources.ps1` (use `ppiq.ps1 up-sources`).
- [ ] Delete `.ppiq-script-backups/`, `tools/archive/`, `tools/_archive/`; add backup globs to `.gitignore`.
- [ ] Rotate the signing key once committed in a backup; purge that backup from history.
- [ ] Write the five users + DSN into `appsettings.Development.json`; remove empty `ConnectionStrings__PlantProcessDb` from all three `launchSettings.json` profiles.
- [ ] Map EF entities into `ppiq_meta`/`ppiq_plant` (add `HasDefaultSchema` + per-entity schema).
- [ ] Unify container/network names to `plantprocess-*` / `plantprocess-private` across compose, Caddy, and `deploy/scripts/*.sh`.
- [ ] Fix `ppiq.ps1 migrate` to run EF migrations before the SQL scripts.
- [ ] Re-baseline EF migrations from the C# model (single clean baseline + a thin triggers migration).
