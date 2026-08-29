# PlantProcess IQ — Identity & Topology Reference (v5)

**Supersedes:** `PPIQ_Identity_and_Topology_v4.md`  
**Last updated:** 28 Aug 2026  
**Primary repository:** `C:\Workspace\PlantProcess-IQ`  
**Current live repository HEAD at this document cut:** `0fd79e5e3516cdfd0ab5a73a02e16638fd02693a` (`T-089 CLOSED`)  
**Three-schema topology closure:** `e7d82f078d7dfa4c1ca4ec6e88d0dbc7e1ecd6ba` (`T-087 CLOSED`)  
**Canonical migration authority historical closure:** `6a62c9d6` (`T-088`, current gate still 9/9 GREEN)  
**Current unresolved release blocker at this cut:** `T-204` is not yet closed. Its current runtime blocker is an authentication topology regression described in §8.

> **Purpose.** This file is the implementation-aware identity and topology reference for PlantProcess IQ across local development, presentation history, customer deployments, server infrastructure, canonical database topology, authentication, definitions, licensing, containers, CI/CD and release operation.
>
> It is intentionally stricter than v4 about the difference between:
>
> 1. **declared design**,  
> 2. **committed repository authority**,  
> 3. **fresh-build/replay certification**,  
> 4. **the current state of a specific long-lived database**, and  
> 5. **historical server facts that have not been recertified after later architectural changes**.
>
> Never collapse those five states into one statement.

---

# 0. EXECUTIVE STATUS — READ THIS FIRST

## 0.1 Product identity

PlantProcess IQ is the flagship **generic, cross-industry manufacturing intelligence platform** of SOU Industrial Software.

It is not a steel-specific product, and it is not the parent container of the other SOU industrial applications.

SOU Industrial Software product family:

1. **PlantProcess IQ** — plant intelligence / industrial BI / governed analytics.
2. **MES** — plant execution.
3. **QES** — quality execution.
4. **Yard & Warehouse Management** — material flow.
5. **Energy Management System** — energy/resource efficiency.

PlantProcess IQ's enduring architectural identity is:

```text
industrial data sources
        ↓
governed ingestion / canonical facts
        ↓
Layer A — deterministic calculations and exact facts
        ↓
Layer B — statistical / learned intelligence
        ↓
governed evidence
        ↓
LLM / assistant — explanation, retrieval, orchestration and citation
```

The LLM is not allowed to replace exact deterministic plant calculations with approximation.

The product is read-only toward customer operational systems by default. It does not silently write to PLC/MES/customer sources and does not become autonomous process control merely because AI capabilities exist.

## 0.2 Product roadmap identity as of 28 Aug 2026

Current release naming is deliberately simple:

- **M2 = first real production release — target 30 Sep 2026.**
- **M3 = second production release for heavier use — target 30 Oct 2026.**
- Prior `M2a`, `M2b`, `P10x` style naming is not the current planning language.
- Priority notation is P1–P5.

M2 must materially include, not merely scaffold:

- **Canvas**
- **DB Link**
- **Jobs**

Roles/licensing/security remain product capabilities, but are not allowed to displace the first-release core delivery work.

The current steel/demo dataset is historical presentation material. New product development must remain generic and metadata-driven.

## 0.3 Current implementation authority chain

```text
T-088 historical canonical migration authority
6a62c9d6
        ↓
T-087 governed three-schema topology
e7d82f078d7dfa4c1ca4ec6e88d0dbc7e1ecd6ba
        ↓
T-089 canonical Definition Store
0fd79e5e3516cdfd0ab5a73a02e16638fd02693a
        ↓
T-204 associative cross-filter closure
OPEN at this document cut
```

Do not reopen T-087, T-088 or T-089 while solving a later task unless a new, independently proven defect is explicitly assigned to their ownership.

## 0.4 The most important v4 corrections

v4 is no longer correct in several architectural statements.

### v4 statement now superseded

> EF tables land in `public`; canonical two-schema split is not yet enforced.

### v5 truth

The product now has a **committed and certified three-schema governed topology**:

```text
ppiq_meta
ppiq_plant
ppiq_staging
```

A generated `StorageTopologyMap` governs model placement and a single terminal physical convergence file relocates historical tables after their historical creators have executed.

The current topology rule is:

```text
historical creators remain historical
        ↓
current model/source references use governed schema names
        ↓
terminal topology convergence relocates existing physical tables
        ↓
read-model views are created after convergence
```

No product table is supposed to remain in `public` after the governed convergence. `public` may still contain infrastructure bookkeeping such as migration ledgers.

---

# 1. AUTHORITIES, TERMINOLOGY AND TRUTH LEVELS

## 1.1 Five truth levels

Every topology statement in PPIQ must be classified mentally into one of these levels.

### A. Product/design authority

What the product is intended to be.

Examples:

- generic cross-industry platform;
- customer source topology is configurable;
- three governed database schemas;
- read-only customer-source posture.

### B. Repository authority

What is currently committed in the live code tree.

Examples:

- `StorageTopologyMap.cs`;
- `000_storage_topology_convergence.sql`;
- `831_definition_store.sql`;
- `canonical-migration-order.json`.

### C. Certified fresh/replay authority

What was actually rebuilt or upgraded in a disposable/frozen certification database.

Examples:

- T-087 fresh build from zero;
- T-087 upgrade clone;
- T-089 disposable fresh canonical replay.

### D. Long-lived database state

What one specific database currently contains.

Examples:

- local `ppiq_app`;
- historical `ppiq_presentation`;
- server `plantprocessiq`.

A repository commit does **not** prove that every long-lived database has replayed that commit's SQL.

### E. Historical environment fact

A fact that was once observed but has not been recertified after later architectural changes.

The June server deployment is in this class until a new current deployment certification proves otherwise.

## 1.2 Authority order during conflict

Use this order:

```text
live current repository + current accepted task closure
        >
latest implementation audit
        >
latest central handover
        >
older design/reference document
        >
historical troubleshooting notes
```

A stale export never overrides a later accepted live commit.

---

# 2. ENVIRONMENT MATRIX

PPIQ currently has three practically relevant runtime contexts.

| Area | LOCAL DEVELOPMENT | PRESENTATION HISTORY | SERVER / CUSTOMER STYLE |
|---|---|---|---|
| Purpose | daily engineering, tests, debugging | frozen/legacy customer demonstration baseline | deployment/release/customer operation |
| Main repository | `C:\Workspace\PlantProcess-IQ` | same code lineage / preserved presentation baseline | deployed build from repository |
| Main DB | native PostgreSQL 16 | historically `ppiq_presentation` | Docker/managed/native depending deployment |
| Default local DB | `ppiq_app` | `ppiq_presentation` | server historically `plantprocessiq` |
| API | `http://localhost:5063` | local API against presentation profile when explicitly selected | reverse-proxied HTTPS |
| Web | `http://localhost:5173` | local web/presentation state | reverse-proxied app |
| Main DB container? | **NO** | normally local native DB | server historically yes |
| Demo source DBs | may be Dockerized | presentation/demo assets | optional; not product authority |
| Truth use | generic runtime default | populated presentation certification only | release/customer certification |

## 2.1 Non-negotiable local test distinction

```text
generic integration correctness
        → ppiq_app is the correct default

populated historical presentation certification
        → explicitly select ppiq_presentation
```

Never globally redirect ordinary integration tests to the presentation database merely to make one populated test convenient.

## 2.2 Current local canonical profile

The current supported operator pattern is:

```powershell
.\scripts\env\use-profile.ps1 -Profile local -WriteAppEnvFiles
```

The profile supplies, at minimum:

- API host/port;
- web host/port;
- PostgreSQL host/port/database/user/password;
- connection string;
- smoke identity;
- frontend environment output.

Automation should consume the profile rather than independently inventing another local topology.

Real customer/server secrets must never be copied into a tracked topology document.

---

# 3. LOCAL DEVELOPMENT TOPOLOGY

## 3.1 Current local runtime

```text
Windows development host
│
├─ Repository
│  └─ C:\Workspace\PlantProcess-IQ
│
├─ Native PostgreSQL 16
│  ├─ 127.0.0.1:5432
│  ├─ ppiq_app
│  └─ ppiq_presentation   [historical populated presentation baseline]
│
├─ PlantProcess.Api
│  └─ http://localhost:5063
│
├─ PlantProcess.Web
│  └─ http://localhost:5173
│
├─ Marketing website
│  └─ localhost:5080
│
└─ Optional Docker source emulators
   └─ ppiq-sources fleet
```

The main local application database is **not Docker PostgreSQL**.

## 3.2 Canonical local DB identity

Current development DB:

```text
database: ppiq_app
host:     127.0.0.1 / localhost
port:     5432
```

The current local topology is already physically governed by the T-087 three-schema convergence.

Current live topology evidence on 28 Aug 2026:

```text
ppiq_meta     169 base tables
ppiq_plant     33 base tables
ppiq_staging   22 base tables
public          2 infrastructure/base tables
```

The two `public` tables are not evidence of failed product topology. The hard rule is **zero product base tables in public**, not zero objects of every kind in the `public` schema.

## 3.3 Search path

Current observed PostgreSQL search path:

```text
"$user", public
```

`ppiq_meta` is **not** on the default search path.

This fact matters critically for raw SQL. Any runtime raw SQL referencing a governed object must either:

1. use the schema-qualified name; or
2. execute under a deliberately governed search-path contract.

The current architecture prefers explicit schema authority rather than relying on an implicit search path.

---

# 4. GOVERNED THREE-SCHEMA DATABASE TOPOLOGY

## 4.1 The governed schemas

### `ppiq_meta`

Control-plane and metadata authority.

Representative responsibilities include:

- users / tenants / authentication state;
- definitions / definition metadata;
- licensing/control metadata;
- jobs and configuration metadata where assigned;
- provenance/configuration/control-plane authorities.

### `ppiq_plant`

Plant/business/process facts and read models.

Representative responsibilities include:

- materials;
- observations;
- quality facts;
- risk facts;
- correlation facts;
- process/business data;
- current read-model dashboard views.

### `ppiq_staging`

Staging / import / temporary ingestion persistence.

Canvas and ingestion fallbacks that previously named older staging concepts must resolve here.

## 4.2 T-087 physical assignment

T-087 certified a frozen table assignment of:

```text
226 assigned tables
224 tables change schema
2 tables remain in their governed/current location
```

Target distribution:

```text
ppiq_meta      169
ppiq_plant      33
ppiq_staging    22
```

The assignment is carried by:

```text
Backend/PlantProcess.Infrastructure/Persistence/Topology/StorageTopologyMap.cs
Backend/PlantProcess.Infrastructure/Persistence/Topology/StorageTopologyConvention.cs
Backend/PlantProcess.Infrastructure/Persistence/PlantProcessDbContext.cs
```

The model and the physical database are therefore governed by the same assignment concept.

## 4.3 Terminal physical convergence

Canonical file:

```text
Backend/database/topology/000_storage_topology_convergence.sql
```

Its role is very specific:

- relocate existing product tables using `ALTER TABLE ... SET SCHEMA`;
- be existence-driven;
- run after historical table creators;
- not create a second copy of product tables;
- not create substitute tables;
- not silently duplicate authorities;
- remain idempotent.

Certified runtime behavior included:

```text
first application:
224 relocated
0 already in place
0 absent

idempotence replay:
0 relocated
224 already in place
0 absent
```

Legacy namespace objects such as historical schema shells may remain. The invariant is **zero legacy product/base-table residue**, not necessarily zero `pg_namespace` rows.

## 4.4 StorageTopology gate

Architecture gate:

```text
Gate=StorageTopology
```

Accepted current result:

```text
6 passed
0 failed
0 skipped
```

Its authority includes:

- every mapped EF entity lives in a governed schema;
- topology map values are only governed schemas;
- convergence relocates instead of creates/copies/drops;
- convergence is existence-driven;
- read-model views do not preserve a `public.*` compatibility alias;
- current view authority lives in `ppiq_plant`.

## 4.5 T-087 fresh/upgrade certification

T-087's accepted certification established:

### Fresh build

- canonical path replay completed;
- **208 physical tables** in the fresh certified database at that cut;
- no product table remained in `public`;
- required governed schemas existed;
- all declared read-model views existed in `ppiq_plant`;
- `ppiq_plant` row total of 956 was captured as an informational non-vacuity signal.

### Upgrade clone

- every pre-upgrade table still existed after upgrade;
- business/data row counts were unchanged by relocation;
- EF history advanced exactly one topology checkpoint;
- exactly one `StorageTopologyCheckpoint` migration was present;
- no product table remained at its old location;
- FK count was unchanged;
- all FKs remained validated;
- staging authority existed under `ppiq_staging`;
- no public compatibility read-model view survived.

### Canvas

- staging fallback = `ppiq_staging`;
- no `dump_store` dependency;
- fallback remains configuration-driven.

### Frozen behavioral oracle

Before/after payloads were byte-identical for the frozen migrated-clone oracle, including the previously known PASS / expected FAIL / INSUFFICIENT cases. The purpose was to prove relocation did not alter business semantics.

---

# 5. CANONICAL DATABASE MIGRATION AUTHORITY

## 5.1 Canonical authority file

```text
Backend/database/canonical-migration-order.json
```

The manifest is the database replay authority. Directory filename ordering alone is not sufficient.

The manifest classifies SQL into intended dispositions such as:

- Canonical
- HistoricalRepair
- FixtureOnly
- Superseded
- DeferredWithReason

Only the canonical path defines a clean replay.

## 5.2 T-088

T-088 established the canonical migration authority.

Historical closure:

```text
6a62c9d6
```

Current gate after later topology/definition changes remains:

```text
CanonicalMigrationOrder
9 passed
0 failed
0 skipped
```

Do not rewrite T-088 simply because later tasks legitimately insert new canonical steps. Later tasks must update the same manifest correctly.

## 5.3 Current canonical tail after T-089

As of HEAD `0fd79e5e...`:

```text
canonical path entries = 99

last earlier schema step      = 96
831_definition_store.sql      = 97
terminal storage convergence  = 98
first/current view authority  = 99
```

View order is duplicated structurally in:

```text
canonicalPath[].position
canonicalViews[].order
```

Both values must be rebuilt from the same numeric ordering calculation.

The T-089 failures proved why this matters: changing one representation while leaving the other stale correctly fails the T-088 view-order gate.

## 5.4 Read-model view authority

Canonical view file:

```text
Backend/database/views/006_dashboard_dataset_views.sql
```

Current view authority creates the dashboard views under:

```text
ppiq_plant
```

The four declared views are:

```text
vw_dashboard_quality_overview
vw_dashboard_latest_risk_by_material
vw_dashboard_data_quality_summary
vw_dashboard_correlation_summary
```

Views must execute after every schema-changing canonical step and after terminal topology convergence.

---

# 6. CANONICAL DEFINITION STORE — T-089

## 6.1 Repository authority

T-089 closed at:

```text
0fd79e5e
```

Owned files:

```text
Backend/database/scripts/831_definition_store.sql
Backend/tests/PlantProcess.Architecture.Tests/DefinitionStoreContractTests.cs
Backend/database/canonical-migration-order.json
```

T-089 did not modify T-204-owned frontend evidence files.

## 6.2 Canonical store tables

Script 831 creates:

```text
ppiq_meta.definition_store
ppiq_meta.definition_versions
ppiq_meta.definition_dependencies
```

### Definition Store

Key identity:

```text
PRIMARY KEY (id)
UNIQUE (tenant_id, definition_code)
```

Surfaces:

```text
S1
S2
S3
S4
S5
```

Canonical `definition_kind` values:

```text
transformation
page
widget
filter
master_dimension
master_measure
hierarchy
bookmark
saved_query
analysis
feature_set
model
practice
log_rule
report
scenario
```

### Definition Versions

Primary key:

```text
ppiq_meta.definition_versions(id)
```

Version identity:

```text
UNIQUE (definition_id, version_number)
```

Parent:

```text
definition_id
→ ppiq_meta.definition_store(id)
```

Representative version fields:

```text
version_number
status
mode
graph_json
sql_text
compiled_sql
definition_hash
input_bindings
output_schema
validation_result
validated_at_utc
published_at_utc
published_by
rollback_pointer
drift_detail
audit timestamps/users
soft-delete fields
is_synthetic
```

Status contract:

```text
draft
validated
published
paused_by_drift
rolled_back
superseded
```

Modes:

```text
block
sql
```

### Definition Dependencies

Canonical dependency table:

```text
ppiq_meta.definition_dependencies
```

It links definitions to required/optional dependencies and refuses cycles through its trigger/validation contract.

## 6.3 Store triggers

T-089 owns:

```text
trg_definition_versions_immutable
trg_definition_dependencies_no_cycle
```

Published definition/version immutability and dependency-cycle refusal are hard database behaviors, not UI conventions.

## 6.4 Important live-DB distinction

At the current document cut, the **repository authority contains T-089**, but the long-lived local `ppiq_app` has **not replayed script 831 yet**.

Observed live `ppiq_app`:

```text
ppiq_meta.definition_store          absent
ppiq_meta.definition_versions       absent
ppiq_meta.definition_dependencies   absent

ppiq_meta.ppiq_definition_versions  present
ppiq_meta.ml_outcome_definitions    present
```

Therefore:

```text
T-089 repository/fresh-replay authority = CLOSED
current ppiq_app replay state            = one canonical script behind
```

Do not call this a T-089 implementation defect.

## 6.5 Replay tracking gap — W1-DB-REPLAY-01

Current `public.schema_migrations` contains:

```text
0 rows
```

The local long-lived database therefore has no trustworthy row-level answer to:

> Which canonical SQL scripts have actually been applied here?

This is an infrastructure/install/upgrade tracking finding, not permission to expand Definition Store work into a replacement migration framework.

Until fixed, operator certification must distinguish:

```text
repository canonical path
vs
current physical DB state
```

---

# 7. AUTHENTICATION AND IDENTITY

## 7.1 Product identity model

PPIQ authentication is DB-backed through `AuthStore`.

Core physical authorities include:

```text
ppiq_meta.tenants
ppiq_meta.app_users
ppiq_meta.auth_refresh_tokens
```

The three-schema topology explicitly assigns those tables to `ppiq_meta`.

## 7.2 System owner vs customer administrator

The permanent identity model retains two distinct administrative concepts.

### System owner / support account

The intended permanent system-support identity is:

```text
sysadmin
```

Purpose:

- SOU support;
- system troubleshooting;
- initial system ownership;
- never presented as the customer's daily administrator.

First-run provisioning is intended to create only the permanent owner from configured first-run identity when no DB user exists.

### Customer administrator

A customer/tenant administrator is a separate business identity created during commissioning.

Do not conflate:

```text
system owner / support
```

with:

```text
customer tenant administrator
```

## 7.3 First-run provisioning resilience

`FirstRunProvisioningHostedService` intentionally catches provisioning exceptions, logs them, and permits API startup to continue.

This means:

```text
API /health = GREEN
```

does **not** prove:

```text
authentication = GREEN
```

Authentication requires an actual login certification.

## 7.4 Current critical auth topology regression — OPEN

This is the most important current runtime defect in the identity/topology layer.

### Physical truth

Live local DB:

```text
ppiq_meta.app_users
ppiq_meta.auth_refresh_tokens
ppiq_meta.tenants
```

Current search path:

```text
"$user", public
```

### Runtime source truth

Current `Backend/PlantProcess.Api/Security/AuthStore.cs` still contains raw SQL using unqualified names such as:

```sql
FROM app_users
JOIN tenants ...
INSERT INTO app_users ...
INSERT INTO auth_refresh_tokens ...
FROM auth_refresh_tokens ...
UPDATE auth_refresh_tokens ...
UPDATE app_users ...
```

### Runtime result

Current exact diagnostic on 28 Aug 2026:

```text
GET  /health      → HTTP 200
POST /auth/login  → HTTP 500
PostgreSQL        → 42P01 relation "app_users" does not exist
```

The exception is raised in `AuthStore.ValidateUserAsync`.

First-run provisioning also logs:

```text
42P01 relation "app_users" does not exist
```

from `AuthStore.HasAnyUserAsync`, but the hosted service catches it and allows the API to start.

### Root cause

```text
T-087 moved auth tables from historical/public placement
        ↓
AuthStore raw SQL remained unqualified
        ↓
search_path does not include ppiq_meta
        ↓
health starts
        ↓
auth queries cannot resolve app_users
```

### Correct remediation principle

Do **not** hide this by creating duplicate compatibility tables in `public`.

Do **not** repair it by undoing three-schema topology.

The runtime source must use the governed auth authority, preferably explicit schema-qualified SQL or an equally explicit topology abstraction.

Every auth-table reference in the runtime path must be reviewed together:

```text
app_users
tenants
auth_refresh_tokens
```

After repair, certification must prove:

- API build;
- first-run provisioning path;
- login;
- refresh token create/read/revoke;
- password update where applicable;
- current topology gates;
- no `public` auth compatibility authority added.

## 7.5 Local smoke identity

Smoke credentials are runtime-profile data.

The canonical operator path is to load the local profile and consume:

```text
PPIQ_SMOKE_USERNAME
PPIQ_SMOKE_PASSWORD
```

Do not duplicate customer/server passwords into this tracked topology reference.

---

# 8. CURRENT RELEASE-CRITICAL STATE — T-204

T-204 is the associative cross-filter behavioral closure.

It is **not closed** as of this v5 cut.

Important facts already proven before the current auth blocker:

- exact six T-204-owned files are preserved;
- TypeScript compatibility work reached green in prior runs;
- closure laws were verified;
- local topology was aligned;
- API `/health` became green;
- canonical system dashboard readiness has a public `ensure/repair` path;
- the frozen T-204 authority is one-shot `Close`.

The latest run stopped before T-204 behavior because login returned the auth 500 described in §7.4.

Therefore the truthful state is:

```text
T-204 implementation/evidence files     present
T-204 final certification/commit         not complete
current blocking precondition            AuthStore schema qualification
```

Do not mislabel the auth 500 as an associative-filter defect.

---

# 9. SERVER / DEPLOYMENT TOPOLOGY

## 9.1 Status classification

The June server environment is retained here because it is operationally important, but its latest green status is **historical evidence**, not automatic certification for the Aug three-schema build.

Do not state:

> the current Aug HEAD is production-certified on the VPS

unless a new deployment/release gate actually proves that.

## 9.2 Historical server endpoints

Last established host family:

```text
Host      178.105.152.180
App       https://app.178.105.152.180.sslip.io
API       https://api.178.105.152.180.sslip.io
Website   https://website.178.105.152.180.sslip.io
Jenkins   https://jenkins.178.105.152.180.sslip.io
```

Before customer use, revalidate:

- DNS/TLS;
- Caddy routes;
- current image versions;
- DB migration state;
- auth login;
- licensing;
- system-template readiness;
- browser application flow.

## 9.3 Two compose projects — never merge

### Infrastructure project

```text
plantprocessiq
```

Long-lived infrastructure historically includes:

- `ppiq-jenkins`
- `ppiq-caddy`
- backup/infrastructure helpers
- public ingress network

This project is not to be reaped by an app deployment.

### Application project

```text
ppiq-app
```

Historically includes:

- `plantprocess-postgres`
- `plantprocess-api`
- `plantprocess-web`

The app/API may also join a shared edge network so infrastructure Caddy can resolve them.

The split exists to prevent:

```text
docker compose ... --remove-orphans
```

for the application from removing Jenkins/Caddy infrastructure.

## 9.4 Public ingress

Caddy remains the intended public ingress.

General rule:

```text
Internet
   ↓
Caddy :80/:443
   ↓
app / api / website
```

The application database is not a public Internet service.

## 9.5 Customer topology remains configurable

Customer deployment is not hardcoded to the VPS layout.

Supported conceptual main DB modes include:

```text
native
docker
external
managed
```

Deployment may therefore use:

- on-host PostgreSQL;
- Docker PostgreSQL;
- managed PostgreSQL;
- VM-hosted PostgreSQL;
- Kubernetes service;
- customer-managed DB endpoint.

The product contract is the connection string/profile, not one server hostname.

---

# 10. SERVER SECRETS AND PASSWORD COUPLING

The v4 operational warning remains important.

For the historical Docker server, PostgreSQL password initialization is coupled to the data volume.

Persisted runtime environment:

```text
/var/lib/ppiq-preserve/.env
```

Operational law:

- do not casually delete the persisted environment file;
- PostgreSQL initializes its internal password on first volume creation;
- regenerating an environment password while retaining the old DB volume can produce authentication mismatch (`28P01`);
- if credentials must truly be regenerated, perform an intentional coordinated secret+volume operation.

Production secrets are not topology-document content.

The document may identify **where** a secret is governed. It must not become the secret store itself.

---

# 11. ENVIRONMENT PROFILES

## 11.1 Local

Canonical local loader:

```powershell
.\scripts\env\use-profile.ps1 -Profile local -WriteAppEnvFiles
```

This is now used by central certification tooling.

Do not follow the old v4 instruction to blindly delete `env/profiles/local.env`; the live tooling currently relies on the local profile authority.

If secrets in that file need governance improvement, solve that as a deliberate secrets-management task, not by deleting the active profile and silently breaking all launch/certification scripts.

## 11.2 Customer template

Customer profile must remain topology-neutral.

Important conceptual keys:

```text
PPIQ_MAIN_DB_MODE
PPIQ_DEMO_SOURCES_MODE
POSTGRES_HOST
POSTGRES_PORT
POSTGRES_DB
POSTGRES_USER
ConnectionStrings__PlantProcessDb
PLANTPROCESS_ALLOWED_ORIGINS
VITE_API_BASE_URL
```

Sensitive values belong in ignored/private environment material.

---

# 12. DATABASE BOOTSTRAP, EF AND CANONICAL SQL

## 12.1 Two mechanisms exist

PPIQ currently uses both:

1. **EF Core migrations**
2. **canonical SQL replay**

They solve different historical/schema responsibilities.

Do not assume that successful EF startup means the full canonical SQL path is current.

## 12.2 EF startup

Current API startup applies pending EF migrations.

The current auth diagnostic logged:

```text
Applying pending EF Core migrations...
No migrations were applied. The database is already up to date.
EF Core migrations applied successfully.
```

Immediately afterward the auth path still failed because canonical physical schema placement had changed relative to raw SQL.

Therefore:

```text
EF current
≠
all canonical SQL/current runtime assumptions correct
```

## 12.3 Historical migration immutability

T-087 deliberately preserved historical EF and canonical SQL bytes.

The governing model is:

```text
historical creators remain historical
current model/source is topology-qualified
terminal convergence corrects physical placement
```

Do not rewrite dozens of historical migrations merely to make them appear as if the three-schema topology existed from the beginning.

## 12.4 Fresh build vs long-lived upgrade

Every serious database task should distinguish:

```text
fresh replay
upgrade clone
long-lived current DB
```

A green fresh replay alone does not prove the current local/server DB has applied the new canonical step.

---

# 13. READ-MODEL / DASHBOARD SYSTEM TEMPLATE IDENTITY

The product has canonical system dashboard templates.

Current T-204 runtime preparation uses:

```text
POST /analytics/dashboard/definitions/system-templates/ensure
POST /analytics/dashboard/definitions/system-templates/repair
```

Current canonical acceptance inventory:

```text
5 system dashboards
13 active system widgets
```

The definitions endpoint supports system-template inclusion; the plain current route used by the T-204 fixture is expected to see the same system authority.

System templates are product/system templates, not customer demo records.

---

# 14. DATA SOURCES AND CONNECTOR TOPOLOGY

## 14.1 Product principle

Customer sources are external to the PPIQ canonical application DB.

The product should model source connection metadata rather than assume one fixed industrial stack.

Source examples include:

- PostgreSQL
- Oracle
- Microsoft SQL Server
- MySQL
- CSV / Excel
- REST
- OPC-UA and other industrial connectors as supported

## 14.2 Historical demo source fleet

The previous steel demonstration used a fleet of source emulators, including:

- meltshop PostgreSQL;
- caster Oracle;
- HSM Oracle;
- PKL MSSQL;
- downtime MySQL;
- Parsytec MySQL;
- Excel/CSV sources.

Those emulators are presentation/development assets.

They are **not** the product's domain model.

Do not derive:

- canonical entity names;
- generic UI contracts;
- cross-filter semantics;
- ML semantics;
- DB topology

from steel-specific demo assumptions.

## 14.3 Customer-source posture

Default:

```text
READ ONLY
```

PPIQ must never silently write into customer PLC/MES/process databases as part of analytics.

---

# 15. LICENSING IDENTITY

The Ed25519 licensing architecture remains the intended product licensing authority.

Logical tiers:

```text
Light
Pro
ProPlus
Enterprise
```

Current API contract historically includes:

```text
POST /api/v5/licensing/ed25519/activate
POST /api/v5/licensing/ed25519/verify-offline
GET  /api/v5/licensing/ed25519/current
POST /api/v5/licensing/ed25519/entitlement-check
```

Activation body uses:

```json
{
  "licenseJws": "<compact JWS>"
}
```

not an old `{ "token": ... }` shape.

Dev/presentation signing keys are demo/test assets only. Real customer releases require production key management.

Licensing must not become an excuse to embed private signing material into frontend bundles or tracked profiles.

---

# 16. FRONTEND / WEBSITE / API IDENTITY

## 16.1 Local

```text
API       http://localhost:5063
Web       http://localhost:5173
Website   http://localhost:5080
```

## 16.2 Vite build-time configuration

`VITE_*` values are build-time inputs.

Changing the runtime `.env` alone does not retroactively alter an already-built frontend bundle.

Any server/customer deployment that changes:

```text
VITE_API_BASE_URL
other VITE_* values
```

must rebuild the frontend artifact.

## 16.3 Product vs website

The marketing website and PlantProcess IQ application UI are separate surfaces.

Do not route the website root to the HMI accidentally.

Do not assume the marketing site's information architecture is the application's navigation contract.

---

# 17. PRESENTATION BASELINE VS GENERIC PRODUCT

The earlier M1 presentation work and the real generic product must remain conceptually separate.

## Presentation baseline

Purpose:

- customer demonstration;
- controlled populated data;
- polished journey;
- repeatable known-answer presentation.

## Generic product

Purpose:

- real customer onboarding;
- arbitrary customer source shape;
- metadata-driven BI;
- generic definitions;
- generic Canvas/DB Link/Jobs;
- generic deterministic/ML intelligence.

The current product roadmap has deliberately stopped further investment in the old steel demo-specific custom work unless required to preserve the presentation baseline.

Never pull presentation-only vocabulary into canonical product logic.

---

# 18. DEFINITION SURFACES — CURRENT IMPLEMENTATION RULING

The five Definition Store surfaces are:

```text
S1  transformation

S2  page
    widget
    filter
    master_dimension
    master_measure
    hierarchy
    bookmark
    saved_query
    report

S3  analysis
    feature_set
    practice
    scenario

S4  model

S5  log_rule
```

`report → S2` because it is a presentation/delivery definition.

`scenario → S3` because it is a saved analytical/counterfactual definition over a model, not model-authoring authority.

This is the current implementation ruling until an official design revision explicitly supersedes it.

---

# 19. OPERATIONAL CERTIFICATION COMMANDS

## 19.1 Load local profile

```powershell
cd C:\Workspace\PlantProcess-IQ
.\scripts\env\use-profile.ps1 -Profile local -WriteAppEnvFiles
```

## 19.2 Start API canonically

```powershell
powershell `
  -NoProfile `
  -ExecutionPolicy Bypass `
  -File .\scripts\run\start-api.ps1 `
  -Profile local
```

Expected health endpoint:

```text
http://localhost:5063/health
```

## 19.3 Canonical migration gate

```powershell
dotnet test `
  Backend\tests\PlantProcess.Architecture.Tests\PlantProcess.Architecture.Tests.csproj `
  --filter 'Gate=CanonicalMigrationOrder' `
  --nologo
```

Current accepted expectation at this cut:

```text
9 passed
0 failed
0 skipped
```

## 19.4 Storage topology gate

```powershell
dotnet test `
  Backend\tests\PlantProcess.Architecture.Tests\PlantProcess.Architecture.Tests.csproj `
  --filter 'Gate=StorageTopology' `
  --nologo
```

Current accepted expectation:

```text
6 passed
0 failed
0 skipped
```

## 19.5 Never use health alone as release proof

Minimum runtime release probe must include:

```text
/health
authentication
core data read
system definitions/templates
task-specific endpoint/browser proof
```

A 200 health response can coexist with a completely broken login, as the 28 Aug auth diagnostic proved.

---

# 20. CURRENT OPEN FINDINGS / RISKS

## CRITICAL — AUTH-TOPOLOGY-01

**State:** OPEN

Auth tables are in `ppiq_meta`, but raw `AuthStore` SQL uses unqualified table names. Login currently returns HTTP 500 / PostgreSQL 42P01.

Required direction:

- qualify governed auth references;
- no public compatibility tables;
- certify all auth operations;
- then resume T-204 closure.

## HIGH — DB-REPLAY-01

**State:** OPEN / deferred to install-upgrade ownership

`public.schema_migrations` has zero rows on current local `ppiq_app`.

There is no trustworthy canonical-SQL applied-state ledger for the long-lived database.

Do not solve opportunistically inside unrelated feature tasks.

## HIGH — LOCAL-DB-LAG-01

**State:** known current local condition

T-089 is committed and fresh-replay certified, but local `ppiq_app` has not applied script 831.

Before T-090 shared-runtime certification:

- apply the exact committed 831 authority if still missing;
- prove three store tables and both triggers;
- then apply/certify T-090 work.

## HIGH — SERVER-RECERT-01

The server topology recorded in v4 is historical.

After the Aug three-schema changes, recertify current server deployment before calling it production/customer ready.

Particularly recheck:

- database migration/convergence;
- auth schema qualification;
- login;
- reverse proxy;
- licensing;
- frontend API base URL;
- system templates;
- runtime smoke.

## MEDIUM — SECRET-GOVERNANCE-01

Profiles and runtime scripts must not create competing sources of secret truth.

Desired rule:

```text
tracked files describe keys and locations
private runtime files/password manager hold real production values
```

## MEDIUM — CANONICAL-POSITION-DUPLICATION

Manifest view position currently appears in more than one structure.

Future migration-authority updates must rebuild positions from one calculation and test the real T-088 gate.

---

# 21. ARCHITECTURAL GOLDEN LAWS

These are the concise laws to preserve across future sessions.

## Law 1 — Genericity

No steel/customer/demo-specific product logic in canonical product authority.

## Law 2 — Governed storage

Every product table belongs to exactly one governed schema:

```text
ppiq_meta
ppiq_plant
ppiq_staging
```

## Law 3 — No public product shadow

Do not solve schema regressions by recreating duplicate public tables/views.

## Law 4 — Historical creators stay historical

Do not rewrite accepted historical migrations merely to pretend the new topology always existed.

## Law 5 — One terminal convergence

Physical relocation is owned by the terminal convergence authority, not scattered migration-by-migration moves.

## Law 6 — Views last

Read-model views execute only after every schema-changing canonical step and topology convergence.

## Law 7 — Exact facts before AI

Deterministic facts are computed deterministically before learned/LLM explanation.

## Law 8 — Customer systems read-only by default

Analytics does not imply autonomous control/write-back.

## Law 9 — One semantic authority

Compatibility stores may project canonical truth; they must not independently mutate semantic truth.

## Law 10 — Repo truth is not DB applied-state truth

Always prove whether a long-lived DB actually replayed the current canonical authority.

## Law 11 — Health is not authentication

A healthy process can contain broken runtime feature paths.

## Law 12 — Shared worktree ownership

Never stage or commit another worker's dirty files.

## Law 13 — No task drift

A discovered infrastructure defect is recorded and routed unless it is genuinely required to satisfy the active task's acceptance.

## Law 14 — No fake migration history

Never insert fabricated migration/applied-state rows simply to make a gate green.

## Law 15 — Naming is purpose-based

No new business artifact names based on task/phase/milestone identifiers. Numeric SQL prefixes may exist only as execution-order tokens.

---

# 22. SERVER ACCESS REFERENCE

> Keep real passwords/private keys outside tracked documentation.

Historical VPS:

```text
IP: 178.105.152.180
OS: Ubuntu
SSH: root@178.105.152.180
```

Historical Jenkins:

```text
https://jenkins.178.105.152.180.sslip.io/
job: plantprocessiq-deploy
container: ppiq-jenkins
```

Repository:

```text
github.com/kgamal369/PlantProcess-IQ
default branch: main
local clone: C:\Workspace\PlantProcess-IQ
```

Historical app:

```text
https://app.178.105.152.180.sslip.io
```

Historical server DB container:

```text
plantprocess-postgres
```

Before using any of these operationally, confirm the current environment still matches the historical record.

---

# 23. QUICK REFERENCE CARD

| Topic | Current v5 truth |
|---|---|
| Product | Generic cross-industry manufacturing intelligence platform |
| Local repo | `C:\Workspace\PlantProcess-IQ` |
| Current HEAD | `0fd79e5e...` (T-089 closed) |
| T-087 | CLOSED `e7d82f07...` |
| T-088 | historical CLOSED `6a62c9d6`; current gate 9/9 |
| T-089 | CLOSED `0fd79e5e...` |
| T-204 | OPEN |
| Local main DB | native PostgreSQL 16 / `ppiq_app` |
| Presentation DB | `ppiq_presentation` (historical populated baseline) |
| API | `http://localhost:5063` |
| Web | `http://localhost:5173` |
| Governed schemas | `ppiq_meta`, `ppiq_plant`, `ppiq_staging` |
| Assigned tables | 226 |
| Relocated by T-087 | 224 |
| Assignment distribution | 169 meta / 33 plant / 22 staging |
| Public product tables | 0 after governed convergence |
| StorageTopology gate | 6/6 GREEN |
| CanonicalMigrationOrder | 9/9 GREEN |
| Current canonical path | 99 entries after T-089 |
| Definition Store | position 97 |
| Storage convergence | position 98 |
| View authority | position/order 99 |
| Definition Store tables | `definition_store`, `definition_versions`, `definition_dependencies` in `ppiq_meta` |
| Live local 831 replay | NOT YET applied at this cut |
| Current auth tables | `ppiq_meta.app_users`, `ppiq_meta.tenants`, `ppiq_meta.auth_refresh_tokens` |
| Current search_path | `"$user", public` |
| Current auth defect | unqualified AuthStore SQL → login 500 / 42P01 |
| System dashboard target inventory | 5 dashboards / 13 widgets |
| M2 | first production release — 30 Sep 2026 |
| M3 | second/heavier release — 30 Oct 2026 |
| M2 must-haves | Canvas, DB Link, Jobs |

---

# 24. WHAT v5 REMOVES OR SUPERSEDES FROM v4

The following v4 ideas must no longer be used as current authority.

## Superseded: two-schema future gap

Old idea:

```text
ppiq_meta + ppiq_plant planned
EF still public
```

New truth:

```text
ppiq_meta + ppiq_plant + ppiq_staging implemented and gated
```

## Superseded: public product table acceptance

No product table may remain in `public` after current convergence.

## Superseded: delete local profile as a generic cleanup step

Current certification tooling uses the profile loader and active local profile. Secret governance must be improved deliberately rather than deleting the runtime authority blindly.

## Superseded: server "currently green" as a timeless fact

June deployment success is historical until recertified against the Aug architecture.

## Superseded: health as practical API readiness

Health may be green while authentication is broken.

## Superseded: repository commit implies long-lived DB replay

T-089 proves the opposite: committed/fresh-replay certified store, but local `ppiq_app` still lacks 831 until explicitly replayed.

---

# 25. CHANGE LOG — v4 → v5

Major additions/corrections:

1. Added product/portfolio identity and current M2/M3 release framing.
2. Replaced the obsolete two-schema/public-table statement with the implemented three-schema topology.
3. Recorded T-087 commit and certification facts.
4. Added `StorageTopologyMap`, `StorageTopologyConvention`, terminal convergence and current gate contract.
5. Added fresh/upgrade topology acceptance and the correct legacy-shell rule.
6. Added T-088 canonical migration authority and current gate.
7. Added T-089 Definition Store, its 16 kinds, versions/dependencies and triggers.
8. Added current canonical positions 97/98/99 after T-089.
9. Added explicit repository-vs-long-lived-DB applied-state distinction.
10. Added `schema_migrations=0` replay-tracking finding.
11. Added live local T-089 replay lag.
12. Added the exact current auth topology regression and 42P01 diagnosis.
13. Clarified that first-run provisioning catches failures and therefore health can remain green.
14. Removed the old blanket instruction to delete the current local profile authority.
15. Reclassified June server state as historical until post-three-schema recertification.
16. Added current release-critical T-204 status without falsely declaring it closed.
17. Added stronger operational and architectural golden laws.
18. Added a current quick-reference card.

---

# 26. DOCUMENT MAINTENANCE RULE

Update this file whenever any of the following changes:

- governed schema model;
- canonical DB ordering;
- definition authority;
- local default DB/profile;
- server compose topology;
- public ingress;
- authentication physical authority;
- identity provisioning model;
- licensing authority;
- release naming/meaning;
- current authoritative environment distinction.

Do **not** rewrite this file merely because one feature task closes.

Use exact dates and commit identifiers for significant topology transitions.

---


# 27. HISTORICAL SERVER PIPELINE — DETAILED OPERATIONAL REFERENCE

This section preserves the useful v4 deployment knowledge while changing its truth label from “always-current server state” to “last-known established server architecture requiring recertification after the Aug changes.”

## 27.1 Release trigger

Historical normal release flow:

```text
developer commit
     ↓
git push origin main
     ↓
GitHub webhook
     ↓
Jenkins job: plantprocessiq-deploy
     ↓
build / migrate / deploy / health / smoke
```

Manual “Build Now” is an operator fallback, not the primary release model.

## 27.2 Compose project boundary

Historical/current intended names:

```text
infrastructure project: plantprocessiq
application project:    ppiq-app
```

This separation is a hard operational boundary.

Reason:

A previous single-project deployment allowed:

```text
docker compose ... --remove-orphans
```

from the application deployment to treat long-lived Caddy/Jenkins containers as orphans and remove them.

The application deployment must never have ownership authority over the infrastructure project.

## 27.3 Application containers

Expected application identities:

```text
plantprocess-postgres
plantprocess-api
plantprocess-web
```

Expected application-private network concept:

```text
ppiq-app_plantprocess-private
```

The API reaches PostgreSQL over the application-private network.

The web/API may join an additional edge network for Caddy ingress.

## 27.4 Infrastructure containers

Historical infrastructure identities include:

```text
ppiq-jenkins
ppiq-caddy
ppiq-backup-runner
```

Infrastructure owns public host ports.

The app project must not bind a competing Caddy instance to the same public ports.

## 27.5 Last-known Jenkins stage model

The historical green pipeline used a flow equivalent to:

### Stage 1 — checkout / runtime environment

- checkout `main`;
- materialize runtime `.env`;
- retain/preserve server secret state;
- derive host-facing URLs;
- set presentation/demo flags where appropriate.

### Stage 2 — workspace hygiene

- sweep stale generated/build state;
- prevent prior workspace residue from silently becoming release input.

### Stage 3 — backend tests

Jenkins agent itself was not assumed to contain a .NET SDK.

Tool execution used sibling containers where needed.

Backend tests were blocking.

### Stage 4 — frontend tests

Node-based tests executed in an appropriate sibling container.

Frontend tests were blocking.

### Stage 5 — E2E

Historically feature-flagged/gated depending pipeline mode.

For a real M2 production release, the release process should evolve toward explicit blocking browser/runtime certification for critical paths rather than relying indefinitely on a disabled E2E stage.

### Stage 6 — app DB migrate/seed

The migration stage historically:

```text
starts / waits for app PostgreSQL
        ↓
applies EF authority
        ↓
applies ordered/canonical SQL
        ↓
applies required seeds
```

v5 adds an important requirement:

> After T-087/T-089, this stage must be certified against the **canonical migration manifest and terminal topology model**, not merely an old “glob all numbered SQL” assumption.

The current replay-tracking gap (`schema_migrations=0` locally) should be addressed by installation/upgrade ownership before claiming robust production upgrade observability.

### Stage 7 — demo sources

Historically gated by:

```text
PPIQ_DEMO_SOURCES_MODE
```

Demo sources are optional environment assets, not a production dependency.

### Stage 8 — application build/recreate

Historical deployment used:

- image build;
- stable application project;
- recreate/up;
- health gate;
- rollback imagery where configured.

v5 adds:

> Health alone must not be the final deployment gate. Authentication and one authenticated product path must be tested because the Aug auth regression proves that a 200 health endpoint can coexist with a broken login.

### Stage 9 — presentation smoke

Historical presentation mode used an authenticated smoke flow and Enterprise license activation.

For a modern release smoke, minimum identity/topology proof should include:

```text
health
login
current user/tenant context
system definitions/templates
one governed plant-data read
license state where licensing is enabled
```

## 27.6 Docker-out-of-Docker / sibling-tooling model

The historical Jenkins environment used Docker access to run build tools in sibling containers.

Important operational points retained from v4:

- Jenkins host/container may not contain dotnet/node/npm directly;
- tool containers must mount/access the Jenkins workspace deliberately;
- shell availability varies by image (`sh` vs `bash`);
- non-root tool images may need explicit user handling when reading root-owned files;
- pipeline success must depend on child process exit codes, not merely log text.

## 27.7 Server recertification checklist after Aug architecture

Before calling the current server customer-ready, prove all of the following on the deployed current commit:

```text
[ ] expected commit/image is deployed
[ ] ppiq_meta exists
[ ] ppiq_plant exists
[ ] ppiq_staging exists
[ ] zero product base tables remain in public
[ ] terminal topology convergence is represented/applied
[ ] current canonical SQL tail is applied
[ ] auth tables are in ppiq_meta
[ ] AuthStore queries resolve governed auth tables
[ ] /health = 2xx
[ ] /auth/login = success for intended system owner
[ ] refresh-token flow works
[ ] system template inventory = expected canonical inventory
[ ] ppiq_plant read-model views exist
[ ] frontend resolves current API URL
[ ] CORS matches current public host
[ ] licensing flow works if enabled
[ ] no development signing/license private key is treated as production authority
[ ] no customer-facing frontend bundle contains inappropriate smoke secrets
```

---

# 28. DEMO SOURCE FLEET — HISTORICAL DEVELOPMENT/PRESENTATION ASSET

The v4 source fleet remains useful as a development fixture but must no longer be described as PlantProcess IQ's domain.

## 28.1 Historical source topology

The established emulator fleet included:

| Logical source | Technology | Historical purpose |
|---|---|---|
| Meltshop | PostgreSQL | upstream production/process source |
| Caster | Oracle | caster/process source |
| HSM | Oracle | hot-mill/process source |
| PKL | Microsoft SQL Server | process/line source |
| Downtime | MySQL | downtime/events |
| Parsytec | MySQL | inspection/surface-defect source |
| Yard file source | CSV/Excel | material/yard data |
| QA file source | CSV/Excel | quality/inspection data |

Earlier local host ports included non-default mappings to avoid conflicts with the native app DB.

Do not copy those ports into customer product logic.

## 28.2 Source ownership boundary

These source emulators are **external source systems from PPIQ's perspective**.

PPIQ application storage is separate:

```text
external source DB/file
        ↓
connector / DB Link
        ↓
staging / governed ingestion
        ↓
canonical PPIQ plant/meta persistence
```

Do not join directly from customer-source databases into arbitrary product UI logic as a permanent architectural shortcut.

## 28.3 Current customer direction

The old steel emulation is not the current product-design driver.

The current customer/prospect work is moving toward a different industrial domain and a generic customer-provided data structure.

Therefore the architecture must support:

- unknown table names;
- customer-specific business keys;
- metadata-driven mapping;
- configurable units;
- configurable time semantics;
- configurable grains;
- configurable relationships;
- metadata-driven Canvas and BI authoring.

---

# 29. LICENSING — DETAILED REFERENCE

## 29.1 Tiers

The established licensing tier vocabulary remains:

| Tier | Relative level | Intended posture |
|---|---:|---|
| Light | 1 | entry/basic source and limited capability |
| Pro | 2 | broader data/connectivity/analysis |
| ProPlus | 3 | advanced analytical/ML/scheduling capability |
| Enterprise | 4 | highest/unlimited enterprise posture |

Exact commercial limits must be treated as product/pricing authority, not inferred from an old topology document if pricing changes later.

## 29.2 Ed25519 architecture

License authority is based on signed license material rather than client-provided tier overrides.

Important objects historically include:

```text
ppiq_ed25519_license_public_keys
ppiq_ed25519_activated_licenses
ppiq_ed25519_entitlement_audit
ppiq_v_ed25519_current_entitlements
```

After three-schema convergence, any physical table/view references must be resolved according to the governed topology rather than assumed `public`.

## 29.3 Activation identity

Compact signed JWS is supplied under:

```json
{
  "licenseJws": "..."
}
```

Never regress to the incorrect old `token` property.

## 29.4 Presentation/dev key rule

A development signing key may be used for an explicitly marked presentation environment.

It must not become the production commercial trust root.

Production/customer licensing requires:

- production private-key custody outside source control;
- registered production public key;
- explicit key identifiers/rotation;
- customer/tier-scoped signed entitlement;
- audit of activation/current entitlement state.

---

# 30. AUTHENTICATION — FULL TOPOLOGY CHECKLIST AFTER THE CURRENT REGRESSION

The current 42P01 regression is important enough to define a permanent auth-topology certification pattern.

## 30.1 Physical auth objects

At minimum, certify:

```text
ppiq_meta.tenants
ppiq_meta.app_users
ppiq_meta.auth_refresh_tokens
```

## 30.2 Raw SQL audit

Search every runtime source that performs raw SQL against auth tables.

For each query prove one of:

```text
explicit ppiq_meta.table
```

or:

```text
a deliberate, tested topology abstraction that resolves to ppiq_meta
```

Do not accept accidental resolution through:

```text
"$user", public
```

## 30.3 First-run provisioning

Test two states.

### Empty auth DB

Expected:

- no user exists;
- first-run identity is provisioned or a deliberate claim workflow is exposed;
- provisioning uses governed auth tables;
- no `42P01`;
- owner identity is distinguishable from customer admin.

### Existing owner

Expected:

- provisioning detects existing user;
- does not duplicate owner;
- API starts cleanly.

## 30.4 Login

Prove:

- valid login succeeds;
- invalid login is rejected without internal exception;
- tenant/user data is returned/resolved correctly;
- password hash algorithm is honored;
- disabled user is rejected.

## 30.5 Refresh tokens

Prove:

- create;
- resolve;
- expiry;
- revoke;
- revoked token refusal.

All persistence remains in governed schema.

## 30.6 Password change / account mutation

Any raw SQL account update must use the same governed authority.

A partial fix to `ValidateUserAsync` is insufficient if other methods still use unqualified `app_users` or `auth_refresh_tokens`.

---

# 31. DATABASE OPERATION — SAFE RUNBOOKS

## 31.1 Read-only topology proof

A safe topology probe should answer:

```sql
SELECT n.nspname, count(*)
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname NOT IN ('pg_catalog','information_schema')
GROUP BY n.nspname
ORDER BY n.nspname;
```

Interpretation must distinguish product vs infrastructure tables.

## 31.2 Locate a physical authority

Never assume a missing qualified relation is absent everywhere.

Use catalog lookup:

```sql
SELECT n.nspname, c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname IN ('target_table_name');
```

This prevents the recurring troubleshooting error:

```text
lookup failed at expected schema
→ incorrectly conclude object does not exist
```

## 31.3 Current canonical Definition Store check

```sql
SELECT n.nspname, c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname IN (
  'definition_store',
  'definition_versions',
  'definition_dependencies'
)
ORDER BY n.nspname, c.relname;
```

At the v5 cut, local `ppiq_app` currently returns none of those because 831 has not been applied there yet.

## 31.4 Auth-table location check

```sql
SELECT n.nspname, c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname IN (
  'tenants',
  'app_users',
  'auth_refresh_tokens'
)
ORDER BY c.relname, n.nspname;
```

Current expected governed result:

```text
ppiq_meta app_users
ppiq_meta auth_refresh_tokens
ppiq_meta tenants
```

## 31.5 Search path check

```sql
SHOW search_path;
```

Current local result:

```text
"$user", public
```

Therefore raw SQL must not rely on unqualified governed-table lookup.

## 31.6 Canonical manifest tail check

PowerShell:

```powershell
$M = Get-Content `
  Backend\database\canonical-migration-order.json `
  -Raw | ConvertFrom-Json

$M.canonicalPath |
  Sort-Object {[int]$_.position} |
  Select-Object -Last 8 position,path

$M.storageTopology
$M.canonicalViews
```

At the current v5 cut, expected significant tail:

```text
831_definition_store.sql           97
terminal convergence               98
view authority                     99
```

If T-090 lands later, this document must be updated again.

---

# 32. ENGINEERING WORKTREE / MULTI-WORKER TOPOLOGY

PPIQ currently uses multiple worker lanes against one repository, which creates a topology of ownership as important as the runtime topology.

## 32.1 Shared-worktree law

One worker's dirty file is not another worker's staging opportunity.

Every closure pack must:

1. freeze expected owned files;
2. inspect the Git index;
3. stage only owned paths;
4. compare staged set to exact expected set;
5. refuse foreign staged files;
6. verify commit file scope.

## 32.2 Cross-task movement of HEAD

Later accepted commits legitimately move HEAD.

A closure script pinned to an older HEAD should stop rather than silently run against a different tree.

The correct response is:

```text
prove intervening commit
prove ownership non-overlap
adopt new HEAD
continue
```

not:

```text
reset/revert another accepted task just to satisfy an old wrapper
```

The T-089 → T-204 sequence is the current concrete example.

## 32.3 Shared database law

Only one lane should own mutation of the shared long-lived `ppiq_app` during a sensitive runtime certification window.

Other workers may continue:

- source work;
- compile;
- architecture tests;
- disposable DB replay.

But they should not concurrently change the same shared DB while another central closure depends on stable runtime state.

## 32.4 Evidence hierarchy

For expensive certifications:

- preserve immutable evidence;
- do not rerun heavy phases after a later trivial finalization crash unless necessary;
- rerun only the gate needed to prove current state;
- never reuse evidence after source/hash/authority drift.

---

# 33. UPDATED OPERATOR DECISION TREE

Use this when a runtime path fails.

```text
Does repository HEAD match expected authority?
        │
        ├─ no → identify intervening accepted commit and ownership
        │
        └─ yes
             ↓
Is Git index clean?
        │
        ├─ no → stop; classify staged ownership
        │
        └─ yes
             ↓
Is DB physical topology governed?
        │
        ├─ no → topology/install issue
        │
        └─ yes
             ↓
Is canonical SQL step actually applied to this DB?
        │
        ├─ unknown → query physical objects; do not infer from HEAD
        │
        └─ yes
             ↓
Does API health pass?
        │
        ├─ no → startup/runtime infrastructure
        │
        └─ yes
             ↓
Does authentication pass?
        │
        ├─ no → auth/runtime topology; health is insufficient
        │
        └─ yes
             ↓
Does task-specific runtime gate pass?
        │
        ├─ no → fix the exact product mechanism
        │
        └─ yes → exact stage / commit / post-commit proof
```

---

# 34. FUTURE v6 TRIGGERS

Create a new Identity & Topology revision when one of these happens:

1. Auth topology regression is fixed and certified.
2. T-204 closes and Worker 2 moves to the next release task.
3. T-090 changes the canonical Definition Store tail.
4. Canonical SQL applied-state tracking becomes real.
5. M2 production server is deployed and freshly recertified.
6. Customer topology materially differs from the historical VPS architecture.
7. A new canonical secrets manager replaces file-based runtime secret governance.

*End of v5 — 28 Aug 2026.*

**PlantProcess IQ identity:** generic industrial intelligence.  
**Storage identity:** `ppiq_meta / ppiq_plant / ppiq_staging`.  
**Current repository authority:** T-087 + T-088 + T-089 accepted; T-204 still open.  
**Current critical topology defect:** AuthStore raw SQL has not yet converged to the governed `ppiq_meta` auth-table placement.  
**Operational rule:** never confuse a green repository/fresh replay with the applied state of a particular long-lived database.
