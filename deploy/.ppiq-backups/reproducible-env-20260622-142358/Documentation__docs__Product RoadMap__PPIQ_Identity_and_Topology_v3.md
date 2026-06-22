# PlantProcess IQ — Identity & Topology Reference (v3)

**Supersedes:** v2. Adds the model-first EF re-baseline, the exact working provision/deploy command sequence, the four config fixes that unblock a local boot, the quarantined-script ledger, the drift fixes applied, the naming golden rule, and the clean-rebuild backlog discovered while driving the stack to a green migration.

**Last updated:** 17 Jun 2026
**Local stack status:** EF baseline + full SQL decoration layer applied green (`ppiq> migrations applied`); Ed25519 licensing live; API boots on `:5063`.

> Single authoritative source for how PPIQ identifies itself across environments — ports, databases, auth, users, demo sources, licenses, containers — plus the exact commands to provision and run it, and the known gaps the clean-rebuild must close. Everything here is environment-configurable; nothing in the product should hardcode an environment.

---

## 0. TL;DR — provision & run locally (model-first)

Run from `C:\Workspace\PlantProcess-IQ`. Assumes native PostgreSQL 16 on `localhost:5432` with database `ppiq_app`, role `ppiq_dev` / `ppiq_dev_local_only`.

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

---

## 1. Ports & endpoints

| Service | Port | Notes |
|---|---|---|
| API (PlantProcess.Api) | **5063** | `http://localhost:5063` |
| Web / HMI (Vite dev) | 5173 | React/TypeScript |
| Vite preview | 4173 | |
| Marketing website | 5080 | |
| PostgreSQL (app DB) | 5432 | native local / container-published |

Key API routes used for M1 proof:
- `POST /auth/login` — body `{ userName, password }`
- `POST /api/v5/licensing/ed25519/activate` — body `{ token: "<compact JWS>" }`
- `POST /api/v5/licensing/ed25519/verify-offline` — returns tier from a JWS
- `GET  /api/v5/licensing/ed25519/current` — active entitlement source of truth
- `POST /api/v5/licensing/ed25519/entitlement-check` — body `{ Feature, DbTierOverride }` (DbTierOverride is **ignored** — tamper-proof by design)

> The Phase-10 `/offline-activation/verify` endpoint takes a different structured envelope, **not** the compact JWS. Do not use it for the `.token` fixtures.

---

## 2. Application database

### 2.1 Connection per environment

| Env | Host | Port | Database | User | Password | How the app reaches it |
|---|---|---|---|---|---|---|
| **Local (native run / EF / tests)** | `localhost` | 5432 | `ppiq_app` | `ppiq_dev` | `ppiq_dev_local_only` | direct |
| **Local (containerised app)** | container `plantprocess-postgres` (pub `127.0.0.1:5432`) | 5432 | `ppiq_app` | `ppiq_dev` | `ppiq_dev_local_only` | API reaches **native main DB** via `host.docker.internal` |
| **Server** | container `postgres` / `ppiq-postgres` | 5432 | `plantprocessiq` | `plantprocess` | (gitignored `deploy/compose/.env`) | container network |

Required config key outside Development is **`ConnectionStrings__PlantProcessDb`** — NOT `ConnectionStrings__DefaultConnection`.

### 2.2 Schemas

- `ppiq_meta` — metadata / control plane
- `ppiq_plant` — customer plant data
- **Known gap:** EF entities have **no `HasDefaultSchema`**, so EF tables land in `public`. Consistent and non-blocking, but the canonical two-schema split is not yet enforced for EF tables. Deferred to clean-rebuild.

### 2.3 EF migrations — model-first, re-baselined this session

Migrations live in `Backend\PlantProcess.Infrastructure\Migrations`. The DbContext uses `ApplyConfigurationsFromAssembly(...)` (config-only; no `DbSet<AuditLogEntry>`). EF Core pinned to **9.0.4**.

The history was re-baselined to two clean migrations (a prior squash had dropped the `audit_log_entries` CreateTable, causing `42P01` on the immutability migration):

1. **`InitialBaseline`** — the entire current model. Confirmed to contain `CreateTable "audit_log_entries"` + the six `ix_audit_log_*` indexes.
2. **`AuditAppendOnlyTriggers`** — DB-only constructs in a thin migration via `migrationBuilder.Sql(...)`: function `prevent_audit_log_mutation()` + `trg_prevent_audit_log_update/delete/truncate` (BEFORE, `P0001`), with matching drops in `Down()`.

**Ordering rule (critical):** EF migrations create the tables; the SQL decoration scripts (`ppiq.ps1 migrate`) decorate them. **EF must run first.** The app applies EF migrations at startup (`Program.cs`), so a bare `ppiq.ps1 up` self-heals; but `ppiq.ps1 migrate` currently runs **only** the SQL scripts, so a fresh DB must get `dotnet ef database update` (or one app boot) before `migrate`. Folding `dotnet ef database update` into the start of `migrate`/`demo` is a pending fix (section 14).

**Design-time factory:** `PlantProcessDesignTimeDbContextFactory` reads the connection string. The committed version's error message named the wrong keys; the permanent fix reads, in order, `ConnectionStrings__PlantProcessDb` -> `PLANTPROCESS_DESIGNTIME_CONNECTION_STRING` -> `PLANTPROCESS_DB`. Until committed, set `PLANTPROCESS_DB` before any `dotnet ef` command (section 0).

**Re-baseline procedure (only when the model changes structurally):**
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

> **Doctrine:** never hand-write raw `CREATE TABLE` for schema. Entities + `IEntityTypeConfiguration` -> `dotnet ef migrations add`. DB-only constructs (triggers, functions) go in a thin separate migration via `migrationBuilder.Sql`.

---

## 3. Authentication & JWT

- Issuer `plantprocess-iq`, Audience `plantprocess-iq-clients`
- Password hashing: **Argon2id**, 64 MB (`65536`), iterations 3, parallelism 1
- Bootstrap admin: **DISABLED**
- `__Host-` prefixed auth cookie

### 3.1 Signing key — the startup-guard trap (fixed this session)

`Backend/PlantProcess.Api/Security/P01P02StartupGuard.cs` rejects "dangerous" signing keys in **all** environments (the `IsDevelopment()` branch still calls `RejectDangerous()`). Its blocklist includes the substrings `DEV_ONLY`, `DEFAULT`, `admin`, `password`, `plantprocess123`, `Admin123!`, etc. The committed `.env.dev` key began with `DEV_ONLY_...`, so it was rejected everywhere despite the comment claiming dev-only.

**Fix (applied):** `deploy/compose/.env.dev` now carries a token-free 68-char key:
```
PlantProcess__Auth__SigningKey=ppiq-local-signing-key-not-for-production-0a1b2c3d4e5f60718293a4b5c6
```
Guard passes (`signingKeyLen=68`, 5 users). Required key length outside Development is **>= 64 chars** (general validation floor is 32; production floor is 64). Rotate before any non-local use.

---

## 4. Role users (frontend logins)

Source of truth: `deploy/compose/.env.dev`, keys `PlantProcess__Auth__Users__N__{UserName,Password,Role}`. **Login uses these config users, not DB tables.**

| User | Password | Role |
|---|---|---|
| admin | DevAdmin123! | Admin |
| exec | DevExec123! | Executive |
| engineer | DevEng123! | Engineer |
| operator | DevOp123! | Operator |
| viewer | DevView123! | Viewer |

**To delete:** the stale 4-user set baked into `appsettings.Development.json` (admin/`Admin123!`, engineer/`Engineer123!`, datamanager/`DataManager123!`, viewer/`Viewer123!`). A bare `dotnet run` reads `appsettings.Development.json`, so this set shadows the canonical five. Closing T02 permanently means writing the 5 users + DSN into `appsettings.Development.json` and stripping the empty `ConnectionStrings__PlantProcessDb` from all three `launchSettings.json` profiles.

---

## 5. Demo source fleet (8 sources)

**Canonical compose:** `deploy/compose/docker-compose.sources.yml`, project `ppiq-sources`.

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

**To delete (duplicate fleet):** `deploy/compose/docker-compose.demo-sources.yml` (project `plantprocessiq-demo-sources`, containers `ppiq-source-*`, different creds `meltshop_owner`/`caster_owner`, oracle-xe:21, network `ppiq-demo-sources`) and its `.ports.yml`.

Local demo data confirmed seeded: 630 heats, 5,670 coils, 39,690 HSM passes, 1,987 surface defects, 17,010 QA results, 210 downtime events.

---

## 6. Licensing — Ed25519, four tiers

| Tier | Level | Limits (users / sources / jobs / dashboards) | Extras |
|---|---|---|---|
| Light | 1 | 3 / 1 / 1 / 3 | CSV/Excel only |
| Pro | 2 | 10 / 3 / 5 / 8 | + SQL editor, PostgreSQL connector |
| ProPlus | 3 | 25 / 8 / scheduled / widgets | + KPI/widget, scheduled correlations, ML |
| Enterprise | 4 | unlimited | all connectors, branded reports |

**Tokens:** `deploy/fixtures/license/{light,pro,proplus,enterprise}.token` — compact EdDSA JWS (`eyJhbG...`). `kid = ppiq-dev-ed25519`; tenant `00000000-0000-0000-0000-000000000001`; `publicKeyB64 = DnycfAUUX263chT9G2UHQ6gbI6HUe5dX8W5KQL8E/Ss=`. Dev key material `deploy/fixtures/license/dev_public.pem` / `dev_private.pem` (dev-only — rotate for production).

**DB tables (created by `650_remaining_p10_ed25519_verified_license.sql`, NOT EF):** `ppiq_ed25519_license_public_keys`, `ppiq_ed25519_activated_licenses`, `ppiq_ed25519_entitlement_audit`, view `ppiq_v_ed25519_current_entitlements`. The script is fully self-contained (zero foreign keys; depends only on `pgcrypto`), so it can be applied standalone:
```powershell
$env:PGPASSWORD='ppiq_dev_local_only'
& 'C:\Program Files\PostgreSQL\16\bin\psql.exe' -h localhost -p 5432 -U ppiq_dev -d ppiq_app -v ON_ERROR_STOP=1 -f Backend\database\scripts\650_remaining_p10_ed25519_verified_license.sql
```
All five Ed25519 checks confirmed green this session.

---

## 7. Containers & topology

- **App stack** — project `plantprocessiq`, network `plantprocess-private`: `plantprocess-postgres`, `plantprocess-api` (:5063), `plantprocess-web` (:5173), `plantprocess-caddy`.
- **Sources stack** — project `ppiq-sources` (canonical `docker-compose.sources.yml`).
- **Naming drift to unify -> `plantprocess-*`:** scripts/Caddy still default to `ppiq-app-api` / `ppiq-postgres` / `ppiq-network` in places.

---

## 8. Frontend

React / TypeScript / Vite ("HMI"). Dark Industrial palette: Deep Navy `#050B18`, panel `#0B1730`, Cyan `#00D4FF`, Blue `#0A84FF`, ok `#2CE6A2`, warn `#FFB020`, crit `#FF4D6D`; fonts Inter + JetBrains Mono.

---

## 9. Environment profiles & customer modes

`env/profiles/customer-template.env.example`:
- `PPIQ_MAIN_DB_MODE = native | docker | external | managed`
- `PPIQ_DEMO_SOURCES_MODE = docker | external | disabled | mixed`

CORS via `PLANTPROCESS_ALLOWED_ORIGINS`. Real admin must be `Role=Admin, IsBootstrapAdmin=false`.

**To delete (leaks `plantprocess123`):** `env/profiles/local.env`.

---

## 10. Migration & deploy commands — the working sequence

### 10.1 Local provision from clean
See section 0. The order is: **EF schema first, then SQL decoration, then app.**

### 10.2 Run the app
```powershell
.\deploy\scripts\ppiq.ps1 up
# foreground; loads .env.dev via Import-DotEnv; runs --no-launch-profile; listens on :5063
```
`ppiq.ps1` verbs: `up | up-sources | migrate | seed | test | e2e | demo | reset | down | init-db`. `init-db` requires `PPIQ_PG_SUPERPASSWORD`.

First-run note: execution policy may need `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` + `Unblock-File` on repo `*.ps1`.

### 10.3 Role x license matrix (M1-T05 / T06 proof)
Second terminal, with the API listening. Reads the five users from `.env.dev`, logs each in, then (as admin) activates each tier token and reads back entitlements. `Set-StrictMode -Version 1.0` (variable-shape HTTP responses), `& { }` paste-block, pure ASCII:

```powershell
& {
  Set-StrictMode -Version 1.0
  $ErrorActionPreference = 'Continue'
  $base = 'http://localhost:5063'
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
    $a = Invoke-Api 'POST' '/api/v5/licensing/ed25519/activate' @{ token=$jws } $adminTok
    if ($a.ok) { $tn='?'; try { $j=$a.body|ConvertFrom-Json; $tn=$j.tier } catch {}
      '{0,-12} OK   tier={1} code={2}' -f $tier,$tn,$a.code }
    else { $snip=$a.body.Substring(0,[Math]::Min(90,$a.body.Length)); '{0,-12} FAIL code={1} :: {2}' -f $tier,$a.code,$snip }
  }
  ''; 'CURRENT ENTITLEMENTS (after last activation)'; '--------------------------------------------'
  $c = Invoke-Api 'GET' '/api/v5/licensing/ed25519/current' $null $adminTok
  if ($c.ok) { $c.body } else { "failed: code=$($c.code) :: $($c.body)" }
}
```

### 10.4 Reset & re-provision
```powershell
$env:PGPASSWORD='ppiq_dev_local_only'
psql -h localhost -U ppiq_dev -d ppiq_app -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public AUTHORIZATION ppiq_dev;"
# then re-run section 0 (EF update -> ppiq.ps1 migrate -> ppiq.ps1 up)
```

---

## 11. Deploy pipeline (server & customer)

Generic, env-driven `Jenkinsfile` + `deploy/scripts/migrate-and-seed.sh` (both produced in prior session). Pipeline order: pull -> EF migrate app DB -> post-EF numbered SQL -> seed -> demo-source migrate+seed (gated by `PPIQ_DEMO_SOURCES_MODE`) -> `dotnet test` (blocking) -> `npm run test` / `npm run e2e` (blocking) -> build -> recreate **live** stack in place -> health gate + rollback -> activate Enterprise token + smoke admin login.

**Pipeline fixes carried from this session (must land):**
1. **`PGCLIENTENCODING=UTF8`** must be exported before every `psql` invocation (in `ppiq.ps1`, `migrate-and-seed.sh`, and the Jenkinsfile psql stage). Without it psql reads the UTF-8 i18n scripts as Windows-1252 — it hard-errors on undefined bytes (e.g. `0x81`) and silently corrupts German/Arabic strings into the DB on the scripts that don't error.
2. `ppiq.ps1 migrate` / `demo` must run `dotnet ef database update` **before** the numbered SQL (so a fresh DB provisions one-shot).
3. Jenkinsfile stage 1: `set -euo pipefail` and `mkdir -p` on **separate** lines; remove the redundant inline `dotnet ef database update` from stage 3 (the script owns it).
4. Converge the orphaned parallel composes to the single live `plantprocessiq` project; conn key `ConnectionStrings__PlantProcessDb`; 64-char SigningKey + deploy `.env`; align Caddy service/port; delete `plantprocess-*` orphans.

---

## 12. Quarantined scripts & the lost foundation

`ppiq.ps1 migrate` globs `Backend/database/scripts/*.sql` sorted; files renamed `*.sql.quarantine` are skipped. Six scripts are currently quarantined:

| Quarantined | Why |
|---|---|
| `310_p03_p04_mapping_genealogy_foundation.sql` | Corrupted: a PowerShell *generator* wrapper saved over the file; its here-string body is a duplicate of the 312 completion pack. **The real foundation is lost** (see below). |
| `311_p03_p04_fix_genealogy_walk_and_safe_sql.sql` | Depends on the lost 310 foundation. |
| `312_p03_p04_completion_pack_a.sql` | Validators over the lost foundation tables. |
| `313_p03_p04_completion_pack_a_hotfix.sql` | Same. |
| `321_p3_golden_thread_and_missing_hop.sql` | Calls `ppiq_walk_genealogy(...)` — a function defined only in the lost 310/311. |
| `511_v5_p02_hotpath_explain_review.sql` | A **manual** EXPLAIN-review helper ("does not mutate data"); was wrongly auto-run, and assumes `tenant_id` on core EF tables that don't have it. Also `710_phase04_second_tenant_seed.sql` quarantined (multi-tenant test data). |

**LOST FOUNDATION (reconstruction task — model-first):** nothing in the script set now creates `ppiq_business_key_definitions`, `canonical_business_keys`, `canonical_mapping_versions`, the canonical genealogy tables, or `canonical_downtime_events`. Git history has no clean copy (squashed out). What's gone: the cross-source business-key dictionary/reconciliation, `ppiq_walk_genealogy` + genealogy cycle/orphan validators, the typed safe-SQL resolver (`ppiq_resolve_safe_sql` — behind the HMI SQL editor), mapping lifecycle dry-run/publish/rollback proofs, and the golden-thread / downtime-value-impact proof views. `genealogy_edges` itself survives (EF-owned). Backlog item: **"mapping / genealogy / safe-SQL foundation rebuild (model-first)"** — entities + `IEntityTypeConfiguration` for the business-key / canonical-mapping tables; traversal + safe-SQL functions in a thin SQL migration. Not on the M1 path.

---

## 13. Drift fixes applied this session

The decoration layer has a recurring pattern: the same table is created by two scripts with divergent columns; `CREATE TABLE IF NOT EXISTS` skips the newer definition, then an index/query on the new column fails. Fixed in place (idempotent ALTER-before-index):

| Script | Fix |
|---|---|
| `420_p3_value_evidence_hmi.sql` | `ALTER TABLE canon.cost_assumption ADD COLUMN IF NOT EXISTS effective_from_utc timestamptz NOT NULL DEFAULT now();` before its index (400 created the table without it). Also requires the `PGCLIENTENCODING=UTF8` fix — it carries German + Arabic i18n strings. |
| `540_v5_p05_visual_mapper_foundation.sql` | `ADD COLUMN IF NOT EXISTS source_code text NOT NULL DEFAULT ''` + `detected_at_utc timestamptz NOT NULL DEFAULT now()` on `public.ppiq_schema_drift_events` before its index (430 created the table without them). |
| `700_phase03_readonly_preview_role.sql` | Hardcoded server DB name + placeholder login password -> generic `EXECUTE format('GRANT CONNECT ON DATABASE %I ...', current_database())` and a `NOLOGIN PASSWORD NULL` role (used via `SET ROLE`), idempotent for fresh-or-existing. |

---

## 14. Pending consolidation & commit list

All of the following are session work-arounds or edits that must become permanent + committed (doctrine: no transient per-run fixes):

- [ ] `.env.dev` token-free SigningKey (done — commit it).
- [ ] `PlantProcessDesignTimeDbContextFactory` rewrite (read `ConnectionStrings__PlantProcessDb` -> `PLANTPROCESS_DESIGNTIME_CONNECTION_STRING` -> `PLANTPROCESS_DB`, honest error).
- [ ] EF re-baseline (`InitialBaseline` + `AuditAppendOnlyTriggers`) — commit.
- [ ] `PGCLIENTENCODING=UTF8` baked into `ppiq.ps1`, `migrate-and-seed.sh`, Jenkinsfile (replace the session env var).
- [ ] `ppiq.ps1 migrate`/`demo`: run `dotnet ef database update` before the numbered SQL.
- [ ] The 420 / 540 / 700 script edits — commit.
- [ ] The six `*.sql.quarantine` renames — commit, with the lost-foundation rebuild tracked in the backlog.
- [ ] `Jenkinsfile` + `migrate-and-seed.sh` (prior session) — commit.
- [ ] Cleanup deletions: `docker-compose.demo-sources*.yml`, `env/profiles/local.env`, `scripts/docker/start|stop-demo-sources.ps1`, `.ppiq-script-backups/`, `tools/archive/`, `tools/_archive/`; rotate any leaked key.
- [ ] Write 5 users + DSN into `appsettings.Development.json`; strip empty `ConnectionStrings__PlantProcessDb` from all three `launchSettings` profiles (closes T02).
- [ ] Add `HasDefaultSchema` mapping EF entities into `ppiq_meta` / `ppiq_plant`.
- [ ] Unify container/network names to `plantprocess-*`.

---

## 15. Naming golden rule (permanent)

**Never** name any file, script, component, page, class, function, endpoint, table, or artifact with phase numbers, task numbers, milestone/sprint IDs, version-phase tags, or codes (`P02`, `phase2`, `T053`, `p03_p04`, `v6`, `hotfix`, ...). Names describe **function/purpose only**. Two directives: (1) never emit such a name again; (2) when one is encountered during development/enhancement/cleanup, rename it to a representative name and update references.

**Ordering nuance:** a leading numeric prefix that purely controls execution order (ordered SQL scripts sorted by filename; EF migration timestamps) is a functional ordering token — preserve it, strip the embedded phase/task label, make the rest descriptive. `migrate`, `migrate-and-seed.sh`, and the Jenkinsfile all glob `*.sql` sorted with no per-file references, so renames are safe.

Representative-rename mapping (examples; full folder pass pending in clean-rebuild):

| Current | Rename to |
|---|---|
| `310_p03_p04_mapping_genealogy_foundation.sql` | `310_mapping_genealogy_foundation.sql` |
| `300_p01_p02_security_access_control_spine.sql` | `300_security_access_control_spine.sql` |
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

1. **Duplicate-definition drift (7 tables).** Tables created by 2+ scripts with divergent columns: `assistant_chunk`, `cost_assumption`, `dashboard_widget_expression_audit`, `page_definitions`, `ppiq_i18n_string_keys`, `ppiq_i18n_translations`, `ppiq_schema_drift_events`. Each is a latent ALTER-before-index drift. Clean-rebuild: one canonical definition per table (model-first).

2. **`migrate` globs non-migration scripts.** Manual helpers (`511`), demo-only seeds, two-tenant probes (`710`/`720`), test auth seeds (`900`), and the `mostly_green_task_closure` script run unconditionally. They belong to other verbs (or `tools/` / `tests/`), not schema migration.

3. **Tenant scoping is undecided.** V5 RLS (`510`/`511`) assumes every table has `tenant_id`; the core EF tables (`genealogy_edges`, `material_units`, `quality_events`, `parameter_observations`, ...) do not. RLS is enabled+forced only on the `uuid tenant_id` tables that have the column; locally `ppiq_dev` is a superuser with `bypassrls=true`, so RLS is not enforced. Decide deliberately: RLS-tenant-scoped core (add `tenant_id` to those entities, model-first) vs single-tenant-per-deployment core. Required for true multi-tenant; not an M1 blocker.

4. **i18n as raw SQL seeds.** `420` / `425` / `610` inline German/Arabic/RTL translation strings as SQL inserts. Clean-rebuild: locale resource files (`.resx` / JSON locales), not SQL — and these files were also encoding-damaged (mojibake in comments), reinforcing the move.

5. **EF tables land in `public`** (no `HasDefaultSchema`) — see 2.2.

---

*End of v3. This reference is the single source of truth for PPIQ identity, topology, and the provision/deploy path; reconcile any divergence against committed files before acting.*
