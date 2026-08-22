# PlantProcess IQ - Master Design Document

**Version 4.9 | Author: Karim, SOU Industrial Software, Dusseldorf**

> **Change log — Operational-Regime, Multi-Objective Practice and Period-Driver Hardening (22 August 2026, v4.9).** v4.9 closes the two generic gaps exposed by the first oil-plant requirement review without introducing oil-specific vocabulary: process transitions/changeovers and stabilisation become first-class governed context so statistics cannot mix distinct operating regimes; practice learning gains customer-declared multi-objective objective sets with Pareto/non-dominance and explicit preference resolution rather than silently choosing one KPI; exact period-to-period operational driver decomposition is added so the Assistant can explain changes in cost/productivity drivers from Layer-A facts before the monetary Value Engine is available. The release also binds the September checkpoint/fallback to the single v2.13 execution workbook. The six chapters remain the only design authority.


> **Change log - Capacity, Pricing and Large-Data Final Hardening Pass (v4.5 to v4.6).** No architecture was redesigned. Eleven numerical and consistency corrections: the three sizing worked examples are recalculated and every one now satisfies the worst-dimension classification rule it is judged by; a canonical internal unit system of bytes, rows and seconds is defined with conversion at the input boundary once; the database RAM formula is replaced by a **cache-target model** whose definition, formula and examples agree, with an explicit cache cap per class; every performance constant is labelled **REFERENCE_ASSUMPTION** or **CALIBRATION_REQUIRED** with its reference hardware; benchmark profiles **C1 to C4** are added so each tier's hardware promise is certified before it is quotable; Chapter 4 5.3.9 is verified and extended with chunking, bounded parallelism, idempotency, checkpoint and resume, scan budgets and deterministic merging; a **Scan Amplification Ratio** metric with acceptance bands and a regression gate is added; Chapter 1 now distinguishes commercial packaging limits from technical cost meters; the stale "not priced per object" traceability entry is corrected to the contracted-quota model; the licence price function now derives the tier from all six commercial dimensions; and the acceptance table of 6.5 has been re-run rather than carried forward.
>
> **Status: not frozen.** Freeze is withheld until the C1 to C4 certification of 6.1.5.8 has executed and the reference constants of 6.1.9.4a are replaced by measured values.


## 6.0 Target architecture amendments integrated in this revision

**REVISION NEXT, 11 August 2026. Amendments C6-1 to C6-5 are integrated into the active body.**

### 6.0.1 Worker containers split by lane (C6-1)

| Container | Admits | Scaling | Notes |
|---|---|---|---|
| `ppiq-worker` | `import`, `projection`, `analysis`, `report`, **`ml.batch_scoring`** | 1 to N | Batch, backfill and rescore work |
| **`ppiq-ml-train`** | `ml.training` | 0 to N, GPU-capable | **Pre-emptible, checkpointed.** Yields at its next stage checkpoint when a reserved lane needs capacity |
| **`ppiq-ml-online`** | **`ml.online_scoring` only** | 1 to N, hard-reserved | Warm model cache keyed by serving identity. **No training imports. No batch admission** |

> **`ppiq-ml-online` runs operational event and micro-batch scoring and its required serving functions only.** Batch, backfill and rescore work runs on batch and training-class capacity.
>
> **Where a deployment physically shares hardware between lanes, online capacity is still hard-reserved**, and B-02 must prove the actionable-latency target holds while training and batch work are saturated. Sharing hardware is a sizing decision; it is never permission to consume the reservation.
>
> **`ppiq-ml-online` importing a trainer module is a build-time test failure.**

### 6.0.2 Resource model (C6-2)

The hard reservation for `ml.online_scoring` sits alongside the existing `interactive` reservation. **Both are subtracted from admissible capacity before any batch or training admission is considered.** Fraction: **B-02**.

### 6.0.3 Backup set (C6-3)

The **feature-snapshot artifact store** and the **sequence artifact store** are in the backup set, with retention driven by `feature_snapshots.retention_until_utc` and the sequence retention policy.

**Reason.** These artifacts are the authoritative training input and the authoritative sequence payload. An authoritative artifact outside the backup set is a reproducibility claim that does not survive a restore.

### 6.0.4 Model gateway (C6-4)

The payload sent to an external provider is the **minimum scoped evidence** needed for the phrasing task, never a whole retrieval set and never raw canonical rows. **A provider or model change is a governed release event**, recorded with a reason, because it changes answer behaviour with no code change. The existing behaviour of refusing rather than falling back to an unapproved model is unchanged and reinforced.

Self-hosted serving uses a replaceable **`ModelServingRuntime`** abstraction. Candidate runtimes are benchmarked as implementations (**B-09**); no serving library is the product contract.

### 6.0.5 Capacity model (C6-5)

Added sizing inputs: **snapshot read throughput** (B-03), **warm-model memory per active serving identity** (B-05), and **per-session VRAM for assistant serving** (B-09).

**Serving resources are bounded and GPU use is optional and benchmark-driven.** Serving carries no training dependency.

---

> **CURRENT AUTHORITY — Master Design v4.9.** PlantProcess IQ has exactly six current design-authority chapters and one current execution-authority backlog workbook. No other file may define, amend, override, supplement or reinterpret current product design or implementation scope. A design change edits the owning chapter directly; a scope change edits the backlog directly. Transitional reviews, amendment packs, ledgers, mandates and prior revisions are historical evidence only after their accepted content is integrated. Validation scripts are code/enforcement instruments, not design documentation.


# CHAPTER 6 - INFRASTRUCTURE, HOSTING, WEBSITE, ADMINISTRATION AND SALES

> **Authority.** Chapters 1 to 5 are frozen and are the authority for product capabilities, page codes, routes, roles, tiers, terminology, APIs, database architecture, engine behaviour, security principles and the user journey. **Chapter 6 does not redesign them and does not introduce a second architecture.** It answers the five remaining questions: how the product is built, tested, deployed, hosted, scaled, backed up and operated; how the infrastructure for a specific customer is calculated; how the public website sells it truthfully; how licences, packages, users, roles and capacity are administered; and how infrastructure cost, licence price and customer capacity relate.
>
> **No new product capability is introduced here.** Where a Chapter 1 or 2 promise has no technical owner in Chapters 3 to 5, it is listed in 6.4.3 as a **Master Design Gap** rather than solved silently.
>
> **Sections.** 6.1 infrastructure and hosting. 6.2 the website. 6.3 administration, licensing and sales. 6.4 cross-chapter traceability. 6.5 final acceptance.

---

# 6.1 INFRASTRUCTURE AND HOSTING

> **Target audience:** senior QA engineer, DevOps engineer, infrastructure engineer, cloud and platform engineer, and the customer's IT infrastructure team.
>
> **Voice:** senior QA engineer and senior infrastructure architect.

## 6.1.1 Infrastructure architecture

### 6.1.1.1 The component set, identical in every topology

Chapter 3 4.6.2 names the components. This section makes them executable. **The same component set runs in all four topologies**; what changes is where the boundary falls and who operates each side.

| # | Component | Process | Listens | Initiates to | Never initiates to |
|---|---|---|---|---|---|
| 1 | Reverse proxy | nginx or equivalent | 80, 443 external | web, API | database, workers |
| 2 | Web frontend | static bundle served by the proxy | - | none (browser calls the API) | - |
| 3 | API service | application runtime | 8080 internal | pooler, object storage, model gateway | customer sources |
| 4 | Background workers `ppiq-worker` | same image, worker role | none inbound | pooler, object storage, model gateway, collector queue | customer sources **directly** |
| 4a | **`ppiq-ml-train`** | same image, ML training role, GPU-capable | none inbound | pooler, object storage | none |
| 4b | **`ppiq-ml-online`** | same image, online scoring role | 8092 internal | pooler, object storage | none |
| 5 | Connection pooler | pgbouncer or equivalent | 6432 internal | PostgreSQL | - |
| 6 | PostgreSQL | database | 5432 internal | - | - |
| 7 | Object storage | S3-compatible or filesystem | 9000 internal or managed | - | - |
| 8 | Model gateway | serving proxy | 8090 internal | self-hosted model, or the configured private endpoint | - |
| 9 | Assistant model serving | self-hosted inference | 8091 internal | - | anything outside the tenant |
| 10 | Identity service | internal, or the customer's IdP | 8443 | directory or IdP | - |
| 11 | **Collector** | **customer-side, DMZ** | none inbound from the core | **customer sources (read), and outward to the API ingest endpoint** | **nothing in the core initiates to it** |
| 12 | Monitoring | metrics and alerting | 9090 internal | scrape targets | - |
| 13 | Log storage | PostgreSQL log tables, per Chapter 3 4.5.15 | - | - | - |
| 14 | Backup service | scheduled | none | PostgreSQL, object storage, backup target | - |
| 15 | Artifact registry | container and model artifacts | 443 | - | - |

### 6.1.1.2 Network zones and trust boundaries

```
  ZONE P - PLANT / OT                 ZONE D - CUSTOMER DMZ          ZONE C - PPIQ CORE
  ---------------------------         --------------------------     ------------------------------
  [ MES ] [ L2 ] [ Historian ]        [ COLLECTOR ]                  [ proxy ]--[ web ]
  [ LIMS ] [ ERP ] [ Inspection ]         |                              |
        ^                                 |                          [ API ]---[ pooler ]--[ PG ]
        |  read-only, credentialed        |                              |         |
        +---------------------------------+                          [ workers ]---+
                                          |                              |
                                          +====> outbound only ====> [ ingest ]
                                                 mTLS, one way            |
                                                                     [ object storage ]
                                                                     [ model gateway ]--[ serving ]
                                                                     [ monitoring ] [ backup ]

  DIRECTION RULE, structural in every topology:
    P <- D        the collector reads plant sources.        Read only. Never writes.
    D -> C        the collector pushes to the core.         Outbound only.
    C -> D        NEVER. No component in the core opens a connection toward the DMZ or the plant.
    C -> internet only through the model gateway, and only in private-endpoint serving mode.
```

**The read-only promise is structural, not procedural.** Three mechanisms enforce it independently, so no single mistake breaks it:

1. **No core component holds a customer source credential.** Credentials live in the collector's own vault on the customer side (credential class C1, Chapter 3 4.6.1).
2. **No core component has a network route to zone P.** Firewall policy, not configuration.
3. **The connector implementations expose no write verb.** `SELECT` and metadata reads only, enforced by the safe-SQL contract of Chapter 4 5.2.12.

### 6.1.1.3 The four topologies

| | **T1 Vendor-hosted multi-tenant** | **T2 Customer cloud** | **T3 Customer on-premise** | **T4 Air-gapped** |
|---|---|---|---|---|
| Zone C operated by | SOU | Customer, deployed by SOU | Customer | Customer, isolated |
| Tenancy | Many tenants, RLS enforced | One tenant | One tenant | One tenant |
| Collector | Customer DMZ | Customer DMZ | Same site as core | Same site |
| Model serving | Self-hosted default; private endpoint per tenant policy | Customer choice | Self-hosted | **Self-hosted only, structurally** |
| Egress | Per-tenant no-egress control available | Per-tenant | Typically none | **None possible** |
| Identity | SOU IdP or customer SSO | Customer SSO | Customer SSO or internal | Internal |
| Updates | SOU pipeline, rolling | SOU pipeline into customer subscription | Signed release bundle | **Offline signed bundle on physical media** |
| Backup | SOU-operated, per tier | Customer cloud native | Customer | Customer, offline |
| Monitoring | SOU | Shared | Customer, export available | Customer, local only |
| Licence update | Online | Online | Online or file | **File only, per 6.3.3** |

**One codebase, four topologies.** No topology has its own build, its own branch or its own code path. The differences above are configuration, deployment manifests and operating responsibility. **A second product for on-premise is prohibited** (Chapter 1.7).

---



### 6.1.1.4 Connector capability truth and production OPC boundary - v4.7

Connector metadata is an executable claim. A connector sets `supportsTagBrowse`, `supportsBoundedRead`, `supportsSubscription` or equivalent only when that build has an implementation path and a contract test that executes it. Configuration validation or deterministic sample generation does not satisfy a data-read capability.

Production OPC UA acquisition belongs in the customer-side collector/edge boundary. The target runtime covers endpoint/security negotiation, application certificate trust, sessions, browse, subscription/monitored items, quality codes, source/server timestamps, reconnect and sequence recovery, store-and-forward and canonical mapping. **The core still never opens a route toward plant OT.**

Site/customer certification is a trial/commissioning acceptance event because security policy, trust lists, namespace and network boundaries are customer-environment facts. Until certified, product and sales material say so explicitly.

## 6.1.2 Container architecture

### 6.1.2.1 Container inventory - one responsibility each

| Container | Image | Responsibility | Scales | State |
|---|---|---|---|---|
| `ppiq-proxy` | vendor base + config | TLS termination, routing, security headers | 1 to 2 | none |
| `ppiq-web` | build output on a static server | Serves the interface bundle | 1 to N | none |
| `ppiq-api` | application image, role `api` | The 27 API domains | 2 to N | none |
| `ppiq-worker-import` | **same image**, role `worker`, pool `import` | Import and backfill | 1 to N | none |
| `ppiq-worker-projection` | same image, pool `projection` | Projection and quarantine | 1 to N | none |
| `ppiq-worker-analysis` | same image, pool `analysis` | Statistics, features, practice | 1 to N | none |
| `ppiq-worker-ml` | same image, pool `ml` | Training, scoring | 1 to N | model cache |
| `ppiq-worker-report` | same image, pools `report` and `retention` | Reports, retention cleanup | 1 | none |
| `ppiq-migrator` | same image, role `migrate` | **Runs once per deployment, then exits** | job | none |
| `ppiq-pooler` | pgbouncer | Connection pooling, **separate identities for interactive and batch** | 1 | none |
| `ppiq-db` | PostgreSQL | The three schemas | 1 (+ replica) | **volume** |
| `ppiq-objectstore` | S3-compatible | Archives, exports, model artifacts | 1 or managed | **volume** |
| `ppiq-gateway` | model gateway | Serving mode routing, egress plan enforcement | 1 to 2 | none |
| `ppiq-serving` | inference runtime | Self-hosted assistant model | 0 to N | model volume |
| `ppiq-collector` | collector image | **Customer side.** One-way push | 1 to N | queue volume |
| `ppiq-monitoring` | metrics stack | Scrape, alert | 1 | volume |

**One image, many roles.** The API and all six worker containers are **the same image** started with a different role and pool. This guarantees they cannot drift in behaviour, and it means a single scan and a single signature cover them all.

### 6.1.2.2 Image policy

| Concern | Rule |
|---|---|
| Base image | A single approved, minimal, digest-pinned base per runtime. **No `latest` anywhere, in any environment** |
| Ownership | Every image declares its owner, its source commit and its build identity as labels |
| Versioning | `<major>.<minor>.<patch>` plus the short commit; the release tag is immutable |
| Non-root | Every container runs as a non-root user with a read-only root filesystem except its declared volumes |
| Layers | No secret, no credential, no test fixture and no emulated-source data in any layer, enforced by the scan gate of 6.1.3 |
| Reproducibility | The same commit produces the same image digest |
| Signing | Every released image is signed; deployment verifies the signature before it runs |

### 6.1.2.3 Health, readiness and lifecycle

| Container | Liveness | Readiness | Restart | CPU | Memory |
|---|---|---|---|---|---|
| `ppiq-api` | `GET /api/health/live` process responds | `GET /api/health/ready` - migration level correct, pooler reachable, object storage reachable | always, backoff | 1-4 cores | 2-8 GB |
| workers | worker heartbeat within 3 intervals | pool registered, pooler reachable | always, backoff | 1-8 cores | 2-16 GB (`ml` highest) |
| `ppiq-db` | `pg_isready` | accepting connections, replication lag under threshold | always | 2-16 cores | 8-128 GB |
| `ppiq-pooler` | port responds | upstream reachable | always | 1 core | 512 MB |
| `ppiq-gateway` | port responds | serving target reachable | always | 1 core | 1 GB |
| `ppiq-serving` | port responds | model loaded and warmed (Chapter 4 5.6.7) | always | 2-8 cores | 8-32 GB |
| `ppiq-collector` | port responds | source reachable, queue writable | always | 1-2 cores | 1-4 GB |
| `ppiq-migrator` | - | - | **never restart. Exit 0 or fail the deployment** | 1 core | 1 GB |

**Readiness is not liveness.** An API container that is alive but whose migration level is behind **must not receive traffic**, because it would answer against a schema it does not expect. The readiness probe checks the migration high-water mark explicitly.

### 6.1.2.4 Volumes, secrets and network segmentation

**Persistent volumes:** database data, object storage, model artifacts, collector queue, monitoring data. **Everything else is disposable** - a container that cannot be destroyed and recreated is a design fault.

**Secrets** are injected as environment variables or mounted files at start, from the vault or the platform secret store, per the eight credential classes of Chapter 3 4.6.1. **A process refuses to start with a missing or empty required secret** rather than falling back to a default. No secret appears in an image, a compose file, a manifest or a log.

**Three networks**, and containers join only what they need:

| Network | Members |
|---|---|
| `edge` | proxy, web |
| `app` | proxy, API, workers, gateway, serving, object storage |
| `data` | API, workers, pooler, database |

The database is on `data` only. **The proxy has no route to the database**, so a proxy compromise cannot reach it.

### 6.1.2.5 Migration as a deployment step

The `ppiq-migrator` container runs **before** any API or worker container starts, exits zero, and only then does the rest of the deployment proceed. A non-zero exit **fails the deployment** and the previous version keeps serving. It is idempotent, ordered, and never runs concurrently with itself, enforced by an advisory lock in the database.

### 6.1.2.6 The four configuration profiles

**No demonstration credential, dataset, behaviour or source configuration may exist in the production profile.** This is enforced by the profile lint in the pipeline, not by care.

| | **Development** | **Test / CI** | **Demonstration** | **Production** |
|---|---|---|---|---|
| Database | local, empty-start | ephemeral per run | populated demonstration database | customer database |
| Sources | emulated containers | emulated fixtures | emulated fleet | **customer sources only** |
| Secrets | local development vault | ephemeral | demonstration vault | **customer vault** |
| Seeding | none beyond the metadata prefill contract | fixtures outside the product | emulated source data, **outside the product** | **none** |
| Bootstrap admin | enabled | enabled | enabled | **disabled, verified by gate** |
| Debug endpoints | enabled | enabled | **disabled** | **absent from the image** |
| Log level | debug | debug | info | info |
| Profile lint | - | - | - | **fails the build if any demonstration marker is present** |

**The demonstration profile is a profile, not a build.** It is the production image pointed at a populated database, per Chapter 1.6.

### 6.1.2.7 Air-gapped image distribution

A release for T4 is a **signed offline bundle**: all images as an archive with their digests, the migration set, the signed manifest, the checksum file, the release notes, and the licence update file. It is transferred on physical media, its signature is verified on arrival **before** anything is loaded, and the load is recorded in the customer's own audit. **No air-gapped installation ever reaches a registry.**

---

## 6.1.3 Pipeline architecture

### 6.1.3.1 The twenty-two stages, in order

**A failed mandatory gate blocks deployment. There is no stage that reports green while a test below it failed**, and the pipeline itself is covered by a truth test that asserts no stage swallows a failure.

| # | Stage | Mandatory | Blocks | Typical |
|---|---|---|---|---|
| 1 | Source checkout with submodules and the commit identity recorded | yes | all | 20 s |
| 2 | Dependency restore from a pinned lockfile | yes | all | 1-3 min |
| 3 | **Secret scan** over the diff and the full tree | **yes** | all | 30 s |
| 4 | Static analysis, backend and frontend | yes | all | 2-4 min |
| 5 | Format and lint, including the **genericity lint** and the **honesty language lint** | yes | all | 1 min |
| 6 | Backend compilation, warnings as errors | yes | all | 2-4 min |
| 7 | Frontend compilation and type check | yes | all | 2-3 min |
| 8 | Unit tests with coverage floor | yes | all | 3-6 min |
| 9 | Integration tests against an ephemeral database | yes | all | 8-15 min |
| 10 | **Migration validation**: forward, idempotency, and the compatibility scan of Chapter 3 4.6.6 | **yes** | all | 3-5 min |
| 11 | **Architecture-rule tests**: RLS coverage, route namespace, design-system ratchet, single-implementation, tenant-aware uniqueness | **yes** | all | 2 min |
| 12 | Dependency and licence scanning | yes | all | 2 min |
| 13 | Container build, all images | yes | deploy | 5-10 min |
| 14 | **Image scan**, critical and high vulnerabilities | **yes** | deploy | 3-5 min |
| 15 | **End-to-end**, including the Chapter 5 golden journey | **yes** | deploy | 20-40 min |
| 16 | Performance gates against the envelope of 6.1.5.7 | yes on release | deploy | 15-30 min |
| 17 | Packaging and **signing** | yes | deploy | 2 min |
| 18 | Deployment to the target environment | - | - | 3-8 min |
| 19 | **Migration execution** via `ppiq-migrator` | **yes** | promotion | 1-10 min |
| 20 | Smoke tests against the deployed instance | **yes** | promotion | 2-4 min |
| 21 | **Post-deployment verification**: the health and acceptance checks of 6.1.4.6 | **yes** | promotion | 3-6 min |
| 22 | Rollback decision, automatic on 20 or 21 failing | - | - | 3-8 min |

**Two anti-patterns are named and prohibited.** A stage that catches an error and reports success. A test command that **enumerates** tests rather than executing them - a listing flag in a test stage is a build failure, asserted by the pipeline truth test.

### 6.1.3.2 Branch, promotion and release

| Concern | Design |
|---|---|
| Branches | Trunk plus short-lived feature branches. **No long-lived parallel branch and no environment branch**, because a demonstration branch is what Chapter 1.6 forbids |
| Pull-request pipeline | Stages 1 to 12 plus a smoke E2E subset. Blocks merge |
| Main pipeline | All 22 stages to staging |
| Release pipeline | Main pipeline plus signing, plus **manual approval** for production |
| Versioning | Semantic version plus commit; an immutable tag per release; the schema version recorded separately |
| Artifact retention | Release artifacts 24 months; pull-request artifacts 14 days; the signed bundle for the life of any supported installation |
| Promotion | dev -> CI -> integration -> staging/UAT -> production. **Promotion moves the identical signed artifact**; it is never rebuilt for the next environment |
| Production approval | Two named approvers, recorded, with the change reference |
| Air-gapped release | Stage 17 additionally produces the offline bundle of 6.1.2.7 |
| Deployment audit | Every deployment writes an audit record: version, digest, approver, environment, migration level, start, finish, outcome |

---

## 6.1.4 Deployment and upgrade

### 6.1.4.1 Fresh installation

1. Provision per the sizing class from 6.1.9. 2. Deploy database and pooler; verify. 3. Run `ppiq-migrator` to head. 4. Verify the **empty-plant proof**: `SELECT count(*)` across `ppiq_plant` returns zero (Chapter 1, Rule 2). 5. Deploy API and workers. 6. Apply the signed licence. 7. Create the customer administrator as a commissioning step. 8. Deploy the collector on the customer side and verify one-way connectivity. 9. Run the post-deployment acceptance of 6.1.4.6. 10. Hand over.

### 6.1.4.2 Upgrade

Expand, migrate, contract. 1. Verify a restorable backup exists **and was verified** (6.1.11.5). 2. Deploy the new migrator; run the **compatibility scan** first, reporting affected definitions, relationships, feature sets and models **before** applying anything. 3. Apply migrations. 4. Roll API and workers with readiness gating. 5. Run post-deployment acceptance. 6. Review the compatibility report with the customer.

### 6.1.4.3 What an upgrade may never do

**Carried from Chapter 3 4.6.6 and restated as an operational rule:**

| Asset | Rule |
|---|---|
| Customer-authored definitions | **Never silently rewritten.** A definition whose compiled statement no longer validates moves to `paused_by_drift` with the changed column named, and appears on C2 as an action |
| Plant relationships | **Never silently dropped.** A relationship whose columns changed moves to `validation_state = failed`, blocking automated consumers while permitting exploration |
| Feature sets and models | Invalidated with the reason (`FS04`); models stay readable and stop scoring until retrained |
| Historical evidence | Findings, predictions, practices, decisions and evaluations are **never rewritten**; they remain explainable under the definition version that produced them |
| Audit history | Append-only. **No migration deletes or edits an audit row** |
| Quarantine | Preserved with its reasons |

### 6.1.4.4 Rollback

Within a major schema version: redeploy the previous signed artifact, and reverse the migration if the migration declared a reversal. Across a major version: **restore plus replay** is the documented path, because a destructive migration cannot be reversed by code. Every rollback is audited with its reason.

### 6.1.4.5 Reindex and rebuild

The retrieval index, `plant_relationship_paths`, `genealogy_paths` and `prediction_current` are all **derived** and rebuildable from authoritative data. Each has an explicit rebuild command, each is idempotent, and none is in the backup set (6.1.11.2).

### 6.1.4.6 Post-deployment acceptance - the installation is usable

**All fourteen must pass.** Any failure triggers the rollback decision of stage 22.

| # | Check | Pass condition |
|---|---|---|
| 1 | Migration level | Head, matching the artifact |
| 2 | API readiness | Ready on every instance |
| 3 | Worker registration | Every configured pool registered and heartbeating |
| 4 | Database connectivity | Through the pooler, both identities |
| 5 | **RLS coverage** | Every tenant-owned table has RLS enabled, forced and a policy |
| 6 | Object storage | Write, read and delete a probe object |
| 7 | Licence | Signature verifies; tier and envelope readable |
| 8 | Authentication | Login, refresh and logout succeed |
| 9 | Golden-journey smoke | A connection tests, an import runs, a projection completes, a page renders, a gated analysis returns a verdict |
| 10 | Assistant | Where the tier includes it: an answer returns with a resolvable citation, or refuses honestly |
| 11 | Scheduler | Due jobs are admitted; the reaper runs |
| 12 | Logging | An entry lands in each family; retention policies present |
| 13 | Backup | The next scheduled backup is registered and the last verification is within its window |
| 14 | **Genericity** | The plant schema of a fresh install returns zero rows; no demonstration marker anywhere |

---

## 6.1.5 Testing strategy

### 6.1.5.1 The pyramid, and what each layer is for

| Layer | Answers | Volume | Runs |
|---|---|---|---|
| Unit | Is this function correct in isolation? | thousands | every commit |
| Integration | Do the parts agree with the database and each other? | hundreds | every commit |
| Contract | Do UI, API and database agree about shape? | hundreds | every commit |
| End-to-end | Does the journey work for a human? | tens | every commit, full set on release |
| Security | Can it be abused? | tens | every commit |
| Reliability | Does it survive things going wrong? | tens | nightly and release |
| Performance | Does it hold under load? | tens | release |

### 6.1.5.2 Unit tests

| Area | What is proven |
|---|---|
| Backend services | Behaviour and refusal per specification |
| Engine functions | Deterministic output for known input |
| **Statistical methods** | **Known-answer tests against planted relations, and a planted null control that must not produce a finding** |
| Readiness rules | Each of the five dimensions at, above and below threshold; **overall equals the worst**, never an average |
| Relationship resolver | Path resolution, preferred path, **ambiguity refuses**, unproven blocks automation but permits exploration |
| Feature generation | Incremental scope correctness, late-arrival handling, idempotency |
| Practice learning | Signature stability, back-off ladder order, **stops at first sufficiency**, sensitivity verdicts |
| Prediction | Scoring determinism, driver ranking, horizon and deadline computation |
| **Remediation gate** | **All nine checks independently, and `can_accept` false when any of its seven conditions fails** |
| Value calculations | Bounded range, `InsufficientBasis` when an input is missing, **both downtime quantities used correctly** |
| Authorization | Role matrix per surface and action |
| Entitlement | Tier gate and role gate **compose**; a client-supplied override is ignored |
| Frontend hooks and components | State transitions, refusal rendering through G5, empty against filtered-empty |

### 6.1.5.3 Integration tests

API against PostgreSQL for every domain; definition store lifecycle including immutability of a published version and cycle refusal; imports with watermark advance, partial batches and budget refusal; projection with each of the fifteen `PV` classes and quarantine reprocessing; relationship publication and path materialisation; job admission, dependency resolution and the reaper; model registry activation, fallback approval and the six conditions; object storage round-trip; assistant retrieval scoping; every log family and its retention cleanup including the archive-fail-no-delete rule; and authentication with token rotation.

### 6.1.5.4 Contract tests

**This layer exists because Chapters 3 and 4 make explicit contracts, and a contract nobody tests will drift.**

| Contract | Assertion |
|---|---|
| UI payload against API payload | Every field the interface reads exists in the response schema |
| API against database schema | Every persisted field exists with a compatible type and nullability |
| **Nullability against mandatory** | A field the API guarantees is `NOT NULL` in the database, or has a declared unavailability state |
| Error codes | Every refusal path returns a code present in `error_catalogue`; **no message without a code** |
| Registry contracts | Every palette list is registry-derived; **no compiled plant vocabulary** |
| Job definitions | A target-requiring class has a target of the matching surface |
| Entitlement contracts | Every gated capability is enforced server-side, not only hidden |
| CHECK against API states | Every status an API can return is permitted by its column CHECK |

### 6.1.5.5 End-to-end - the golden journey

**The full Chapter 5 journey, J1 to J15, is one test.** J1 to J3 run as commissioning setup; J4 to J15 walk the eight tutorials.

| Segment | Proves |
|---|---|
| J1-J3 | Install empty, licence applies, users and roles created |
| J4 | Connection created, **read-only verification passes** |
| J5 | Dataset registered with business key and watermark |
| J6 | Import runs; **a second run is delta-only** |
| J7 | Transformation authored, illegal wire refused, published, **relationships emitted** |
| J8 | Projection completes; a deliberately bad row **quarantines with the right code**; reprocess clears only it |
| J9 | Genealogy walks both directions; **weights sum to exactly 1.0** |
| J10-J11 | Page authored; a click cross-filters every widget; an excluded value **pivots** |
| J12 | Analysis runs or **blocks with a named dimension and measured value** |
| J13 | Findings ranked by effect; practice benchmark discloses its similarity level; prediction carries drivers and a deadline |
| J14 | Acknowledge, assign, accept, record action, outcome arrives from canonical data, evaluation computes |
| J15 | Assistant cites or refuses; Supervisor dry-run **leaves live counts identical**; retention preview then cleanup |

Run at desktop and mobile viewports. **Any segment failing blocks deployment.**



### 6.1.5.4a Persisted-definition replay - product-contract truth

Before route E2E, the pipeline enumerates every active customer-facing page/widget/measure/filter definition from the database and executes its real query contract against the release database. A declared valid definition must return 2xx and a documented terminal state; an invalid combination must return a typed refusal, never 500. This gate catches drift between persisted customer definitions and current backend capabilities.

The first implementation replays what exists. After T-092 makes dimensions/measures registry rows, a generated compatibility matrix derives additional valid/invalid combinations from the registry and chart grammar.

### 6.1.5.5a Customer-session semantic gate

For every customer-reachable route the browser harness records network responses, console errors, widget states and selection propagation. Unexpected 4xx/5xx, unhandled exceptions, error toasts or failed required requests fail the route.

The cross-filter battery captures widgets A/B/C, clicks value X in A, proves selection state contains X, proves B/C issue new requests containing X, proves returned populations and visible results change, clears X and proves restoration. "The page did not crash" is not interaction acceptance.

**Regression law.** Any defect first discovered by manual browser walk is reproduced as a failing automated test at the lowest layer that can detect it before the fix is accepted.

### 6.1.5.6 Security and reliability tests

**Security:** authentication and lockout; authorisation per role; **tenant isolation including a direct API attempt with another tenant's identifier**; RLS enforced with `FORCE` so the owner is subject to it; SQL safety against the forbidden-token set and comment hiding; XSS and CSRF; **secret leakage in logs, errors and responses**; privilege escalation by direct endpoint call to a hidden page; **tier bypass by client-supplied entitlement**; assistant egress restricted to the plan, and no-egress genuinely preventing external calls.

**Reliability:** worker crash mid-job (the run reaps, no partial canonical write); API restart during a long run (the stream reconnects and replays); PostgreSQL reconnect; model gateway unavailable (**refuses, does not fall back to an unapproved model**); network interruption during collector push (resumes from queue); partially failed import (cursor advances only to the last row read); failed job (terminal with a reason); stuck job (reaped); backup and restore (6.1.11.5); event replay after reconnect.

### 6.1.5.7 Performance tests with pass thresholds

Measured on the reference Medium class of 6.1.9.6. **A threshold breach fails the release gate.**

| Scenario | Load | Metric | Pass |
|---|---|---|---|
| Interactive page load | 50 concurrent users | p95 time to interactive | <= 2.5 s |
| Widget query | 50 concurrent, 8 widgets each | p95 response | <= 1.5 s |
| Associative selection | 50 concurrent | p95 state recompute | <= 2.0 s |
| Import | 10 datasets, 1M rows total delta | throughput | >= 20k rows/s aggregate |
| Projection | 1M staged rows | throughput | >= 10k rows/s |
| Analysis job | 100k units, 50 factors | wall clock | <= 10 min |
| ML training | 100k rows, 40 features | wall clock | <= 30 min |
| Prediction scoring | 10k in-process units | p95 batch latency | <= 60 s |
| **Actionable latency** | scheduled path, Medium class | **deadline miss rate** | **<= 1%** |
| Assistant | 10 concurrent | p95 first token | <= 3 s |
| Genealogy walk | depth 6, 5k descendants | p95 | <= 2.0 s |
| Log search | 100M rows retained | p95 filtered query | <= 3 s |
| **Interactive under batch load** | every pool saturated | p95 widget query | **<= 3.0 s - the read path is never starved** |

### 6.1.5.8 Capacity and hardware certification - profiles C1 to C4

**A single Medium reference test does not certify four hardware promises.** Each commercial tier has a matching benchmark profile, and **a hardware profile becomes commercially quotable only after its profile passes.**

| Profile | Certifies | Envelope driven to | Hardware under test |
|---|---|---|---|
| **C1** | Light | Small boundary: 10 rows/s, 2 slots, 250 GiB, 10 sessions | 6.1.9.8 Tier 1 |
| **C2** | Pro | Medium boundary **and the Chapter 4 5.3.9.1 scenario**: 3 links, 300 objects, 146 GiB source, 10 jobs at a 180 s floor, 100 rows/s, 8 slots, 2 TiB, 50 sessions | 6.1.9.8 Tier 2 |
| **C3** | Pro Plus | 1,000 rows/s, 16 slots, 5 TiB, 100 sessions, ML and assistant active | 6.1.9.8 Tier 3 |
| **C4** | Enterprise | Above Large boundary, HA active, air-gapped variant | 6.1.9.8 Tier 4 |

**Every profile executes the same eight representative loads**, at that profile's envelope: ingest; projection including quarantine; analysis; ML training; prediction scoring; practice learning; dashboard interaction; and concurrent-user load.

**Recorded for every profile and every load:**

| Measurement | Why |
|---|---|
| CPU utilisation, per node and per pool | Validates `cpu_api`, `cpu_wrk`, `cpu_db` |
| RAM utilisation and **cache hit ratio** | Validates the cache-target model of 6.1.9.4 |
| Database I/O: read and write bytes/s, IOPS, queue depth | Validates the storage class |
| **Bytes and rows scanned per run** | Validates `D4` and the delta law |
| **Temporary spill volume** | Detects an under-sized `work_mem` or an unpruned query |
| Queue latency and admission wait per pool | Validates `D3` and the reserved interactive share |
| Job completion time by family | Validates the runtime constants |
| **Interactive p95 with every batch pool saturated** | The starvation test of 6.1.5.7 |
| **Scan Amplification Ratio** (6.1.12.2a) | Proves delta scoping held under load |

**Outputs.** Each profile produces a **measured constant set** which replaces the `REFERENCE_ASSUMPTION` values of 6.1.9.4a for that class, versioned, with the benchmark evidence attached.

**Gates.** A profile passes when every 6.1.5.7 threshold holds at its envelope, no measurement exceeds its node specification, and **Scan Amplification stays within its acceptance band**. **Until a profile passes, quotations for that tier carry the pending-certification label of 6.1.9.4a.**

---

## 6.1.6 Environments and test data

| Environment | Data | Lifetime | Who |
|---|---|---|---|
| Developer local | Empty-start plus controlled generic fixtures | Developer | Developer |
| CI | Ephemeral database per run; tiny deterministic fixture | Minutes | Pipeline |
| Integration | Controlled discrete + continuous-process fixtures, reset nightly | Nightly | Pipeline |
| E2E | Controlled generic fixture plus customer-shaped synthetic fixture where available | Per run | Pipeline |
| Performance | Synthetic workload at certified size classes, with declared signal/time semantics | Weekly | Pipeline |
| Staging / UAT | Production-shaped **anonymised** customer sample plus a customer-shaped synthetic fallback | Per release | Release manager |
| Production | Customer data | Permanent | Customer |

> **The genericity rule, restated as an operational rule:** **test data validates PPIQ; test data must never become product logic.** Emulated sources are containers **outside** the product (Chapter 1.6). Fixtures live outside the product tree. No test dataset, defect name, parameter name or plant vocabulary enters the product image, the migration set or the metadata prefill. **The profile lint and the genericity lint enforce this at stage 5**, and a violation fails the build.

---



**Required validation matrix.** Release certification does not rely on one demonstration dataset. The maintained set contains: A) tiny deterministic known-answer data; B) legacy discrete/Fleet regression fixture, isolated from product authority; C) controlled continuous-process fixture with irregular sampling, states and counters; D) foreign-schema/adversarial fixture; E) customer-shaped synthetic fixture; F) anonymised real customer sample when contractually available. The same binary and migrations execute across the matrix. Only mappings, definitions, relationships, registry rows, reference profiles and configuration may differ.

## 6.1.7 Quality gates and release definition of done

**A version cannot be released if any mandatory criterion fails. There is no partial credit and no override without a recorded exception approved by two named people.**

| # | Gate | Measure | Mandatory |
|---|---|---|---|
| 1 | Build | Backend and frontend compile, warnings as errors | yes |
| 2 | Unit tests | 100% pass, coverage at or above the committed floor | yes |
| 3 | Integration tests | 100% pass | yes |
| 4 | Contract tests | 100% pass | yes |
| 5 | Migrations | Forward, idempotent, compatibility scan reported | yes |
| 6 | Vulnerabilities | Zero critical, zero high without a recorded exception | yes |
| 7 | Secrets | Zero findings | yes |
| 8 | Architecture contracts | Zero violations | yes |
| 9 | Tenant isolation | Every tenant-owned table RLS-forced with a policy | yes |
| 10 | Genericity lint | Zero plant vocabulary in code, prefill or palette | yes |
| 11 | Honesty language lint | Zero forbidden phrases anywhere, including the website | yes |
| 12 | API / DB / UI consistency | Contract tests green | yes |
| 13 | Accessibility | WCAG AA on every route; zero critical findings | yes |
| 14 | RTL | Every route verified mirrored; no physical-side name | yes |
| 15 | Performance envelope | Every threshold of 6.1.5.7 met | yes on release |
| 16 | **Backup and restore verified** | A restore rehearsal passed within the window | yes |
| 17 | Install and upgrade rehearsal | Both passed against a production-shaped copy | yes |
| 18 | **Golden-journey E2E** | J1 to J15 green at desktop and mobile | yes |
| 19 | **Persisted-definition replay** | Every active page/widget/query definition executes or returns its documented refusal; zero unexplained 4xx/5xx | yes |
| 20 | **Customer-session semantics** | Network/console invariant plus cross-filter re-query/data-change proof | yes |
| 21 | **Aggregation known-answer** | Continuous/discrete controlled fixtures prove declared aggregation algebra and undeclared semantics refuse | yes |
| 22 | **Connector capability truth** | Every advertised connector capability has an executed contract test | yes |
| 23 | **Dataset-neutral same-binary gate** | Discrete, continuous and foreign/customer-shaped fixtures require zero product-code change | yes |
| 24 | Documentation | Chapter references and the Implementation Status Register updated | yes |
| 25 | Deployment audit | Record written with version, digest, approvers | yes |

---

## 6.1.8 Backlog standard

### 6.1.8.1 The item contract

**Every implementation item carries all twenty-two fields.** An item missing any of them is not ready to be worked.

| Field | Content |
|---|---|
| ID | Permanent, never recycled, never renumbered |
| Title | What it delivers, in the product's own vocabulary |
| Product capability | Which capability from Chapters 1 to 5 |
| Design reference | The chapter and section that specifies it |
| Problem | What is wrong or absent today |
| Requirement | What must become true |
| Scope | What is included |
| Out of scope | What is deliberately excluded, so it is not argued later |
| Backend impact | Services, endpoints |
| Database impact | Tables, columns, migrations, indexes |
| Frontend impact | Pages, components, states |
| Infrastructure impact | Containers, pipeline, sizing |
| Security impact | Authorisation, tenancy, secrets, egress |
| Acceptance criteria | Observable conditions, testable |
| Required tests | Which layers of 6.1.5 |
| Dependencies | Other items, by ID |
| Severity | Per 6.1.8.3 |
| Priority | Ordering within severity |
| Estimated effort | Hours or points, stated consistently |
| Owner | One person |
| Status | Per 6.1.8.2 |
| **Evidence required for closure** | What must be shown, not asserted |

### 6.1.8.2 States

`Not Started` -> `In Progress` -> `Review` -> `Validation` -> `Done`

| State | Exit condition |
|---|---|
| Not Started | Item complete per 6.1.8.1 and dependencies resolved |
| In Progress | Code and tests written; self-review done |
| Review | Peer review passed; contracts and design references checked |
| Validation | **Acceptance evidence produced and recorded** |
| Done | **Evidence accepted** |

> **No item is Done because code was written.** Done means the evidence named in its own contract exists and was accepted. **A gate item is between 95 and 100 percent complete, or it is not done** (Chapter 1.10.4).

### 6.1.8.3 Item classes

| Class | Definition | Design reference | Severity floor |
|---|---|---|---|
| **Product gap** | A capability the Master Design specifies that does not exist | Required | High |
| **Implementation gap** | Specified and partially built | Required | Medium |
| **Bug** | Built, and behaves against its specification | Required | Per impact |
| **Technical debt** | Works, but violates a design rule such as Rule 4 | Required | Medium |
| **Enhancement** | Beyond the Master Design | **Roadmap, not the Master Design** | Low |
| **Infrastructure task** | Pipeline, hosting, operations | 6.1 | Per impact |
| **Security issue** | Any security impact | 6.1.5.6, Chapter 3 4.6.7 | **Highest** |

**Severity is measured by trust cost, not function cost** (Chapter 1.10.1): a demonstration name, a phase token, encoding corruption, a placeholder string reaching a customer, or a control that does not work are all highest severity regardless of how small they look.

---

## 6.1.9 Server and database sizing model

### 6.1.9.1 The principle

> **Object counts are not hardware requirements.** A customer with 400 pages that are opened twice a week costs less than a customer with 12 pages refreshed by 60 concurrent users every minute. **The sizing model converts customer inputs into measurable workload drivers, and sizes against the drivers.**

This is the same principle as the capacity metering law of Chapter 1.7.1, applied to iron rather than to price. **6.1.9 and 6.3.5 use this one model.** Engineering and Sales calculating the same customer must arrive at the same infrastructure.

### 6.1.9.1a The canonical unit system

**One internal unit system. Conversion happens exactly once, at the input boundary.**

| Quantity | Canonical internal unit | Symbol suffix |
|---|---|---|
| Data volume | **bytes** | `_b` |
| Row counts | **rows** | `_rows` |
| Time | **seconds** | `_s` |
| Rate | **per second** | `_ps` |

**The rule.** The calculator and the interface accept human units - MB, GB, TB, minutes, hours, days - and **convert to bytes, rows and seconds at the input boundary, once**. **Every formula and every worked example in 6.1.9 operates in canonical units.** Presentation converts back for display only.

```
INPUT BOUNDARY          14 GB/day  ->  14 * 1024^3 / 86400  =  174,083 bytes_ps
                        3 min      ->  180 s
                        100 tables ->  100 (dimensionless count)
FORMULAS                operate only on bytes_ps, rows_ps, seconds, bytes
DISPLAY BOUNDARY        174,083 bytes_ps  ->  "14.0 GB/day"
```

> **A formula that mixes GB and bytes is a factor-of-a-billion sizing error waiting to be implemented.** The contract tests of 6.1.5.4 assert that every calculator input carries its declared unit and that every formula consumes canonical units only. Constants below are stated with their units explicitly.

**Conversion constants:** `KiB = 1024`, `MiB = 1024^2`, `GiB = 1024^3`, `TiB = 1024^4`, `day_s = 86400`, `hour_s = 3600`, `min_s = 60`. **Binary multiples throughout**, because storage and memory are quoted that way.

### 6.1.9.2 Inputs

**Acquisition:** DB links `L`; source objects `O`; rows imported per day `R_d`; bytes per day `B_d`; peak ingest rate `R_peak` rows/min; minimum refresh interval `I_min` minutes; simultaneous source reads `C_src`; staging retention `T_stg` days.

**Canonical and plant:** canonical rows `N_can`; genealogy edges `N_edge`; parameter observations `N_obs`; retention `T_can` days; registered outcomes `N_out`; intelligence result rows `N_int`.

**Jobs:** defined jobs `J`; job starts per hour `J_h`; peak concurrent `J_c`; per family `J_imp`, `J_proj`, `J_ana`, `J_ml`, `J_pred`, `J_rep`; mean and peak runtime `t_avg`, `t_peak` seconds; compute weight per family `w_f`.

**Users:** named `U_n`; concurrent `U_c`; dashboard-concurrent `U_d`; heavy authors `U_a`; assistant-concurrent `U_asst`.

**Authoring:** pages `P`; widgets `W`; definitions `D`; measures `M`; dimensions `Dim`; relationships `Rel`; models `Mod`.

### 6.1.9.3 Derived workload drivers

**The inputs become five drivers. Everything is sized from these, never from a raw count. All in canonical units.**

```
D1  INGEST LOAD     ingest_rows_ps  = delta_rows_day / 86400 * (1 + backfill_factor)
                    peak_rows_ps    = peak_rows_min / 60
                    ingest_bytes_ps = ingest_rows_ps * bytes_per_row_b

    NOTE: delta_rows_day is the NEW OR CHANGED rows entering staging per day.
          It is NOT the source footprint. A 146 GiB source changing at 0.5% per
          day presents 0.73 GiB of delta, not 146 GiB. (Chapter 4 5.3.9)

D2  QUERY LOAD      interactive_qps = U_d * widgets_per_page * refreshes_hour / 3600
                                    + U_asst * assistant_q_session / 3600

D3  COMPUTE LOAD    weighted_slots  = SUM over families ( starts_hour_f * runtime_s_f * weight_f ) / 3600
                    peak_slots      = same, with peak arrival and peak runtime

D4  SCAN LOAD       scanned_rows_ps = SUM over jobs ( rows_scanned_run_j / cadence_s_j )

    Under the delta-propagation law (Chapter 4 5.3.9) rows_scanned_run is the
    CHANGED set, not the retained model. A job declaring scan_mode = full is
    excluded from cadence and budgeted separately.

D5  RETAINED BYTES  canonical_b = ingest_bytes_ps * 86400 * index_factor * retention_days
                    staging_b   = ingest_bytes_ps * 86400 * staging_days * (1 + envelope_factor)
                    results_b   = canonical_b * results_ratio
                    logs_b      = log_rate_bytes_ps * 86400 * log_retention_days
                    retained_b  = canonical_b + staging_b + results_b + logs_b
```

**Constants, all `REFERENCE_ASSUMPTION` per 6.1.9.4a:** `bytes_per_row_b` 400; `index_factor` 1.6 canonical, 1.35 staging; `envelope_factor` 0.15; `results_ratio` 0.07; `backfill_factor` 0 steady state, up to 3 during initial load.

### 6.1.9.4 Resource formulas

```
APPLICATION
  cpu_api_cores  = ceil( interactive_qps / API_QPS_PER_CORE ) + 1
  ram_api_b      = ( 1.5 * cpu_api_cores + 1 ) * GiB

WORKERS
  cpu_wrk_cores  = max( ceil( weighted_slots ) , max_over_families( weight_f ) )
  ram_wrk_b      = ( 2 * cpu_wrk_cores ) * GiB + ram_model_b
  ram_model_b    = 8 * GiB if ML active else 0
  n_workers      = ceil( cpu_wrk_cores / cores_per_instance )

DATABASE CPU
  cpu_db_cores   = ceil( scanned_rows_ps / DB_SCAN_ROWS_PS_PER_CORE )
                 + ceil( interactive_qps / DB_QPS_PER_CORE ) + 2

DATABASE RAM - the cache-target model
  hot_window_b   = ingest_bytes_ps * 86400 * index_factor * HOT_WINDOW_DAYS
  hot_index_b    = HOT_INDEX_RATIO * hot_window_b
  recent_delta_b = ingest_bytes_ps * 86400 * index_factor * RECENT_DELTA_DAYS
  cache_want_b   = hot_index_b + recent_delta_b
  cache_target_b = min( cache_want_b , CACHE_CAP_b(class) )
  ram_db_b       = max( 8*GiB ,
                        1.25 * cache_target_b + 0.75*GiB * cpu_db_cores + 4*GiB )

STORAGE
  stor_db_b      = retained_b * 1.30
  stor_stg_b     = staging_b
  stor_obj_b     = archive_b + 0.5*GiB * models + snapshot_b
  stor_bak_b     = stor_db_b * daily_change_ratio * 30
                 + 12 * weekly_full_b + 12 * monthly_full_b

NETWORK
  bw_in_bps      = ingest_bytes_ps * 8 * 3
  bw_out_bps     = U_c * 200 * KiB * 8

CONNECTIONS
  pool_int       = ceil( interactive_qps / 8 ) + 4
  pool_bat       = cpu_wrk_cores + 2
  max_connections>= pool_int + pool_bat + 10

MODEL SERVING
  ram_srv_b      = model_artifact_b * cache_depth + 4*GiB

HEADROOM         every figure * (1 + growth_rate)^months , default *1.30 for 12 months
```

**What `working set` means, stated precisely, because the earlier formulation was wrong.**

> The database RAM target is **not** the physical size of the hot partition set. It is the **actively cached page working set**: the index pages of the hot partitions, plus the recent delta that is read repeatedly. **`HOT_WINDOW_DAYS` bounds which partitions are hot; `HOT_INDEX_RATIO` bounds how much of them is index; `CACHE_CAP` bounds what it is economically sensible to cache at each class.**
>
> **Beyond the cap, the design relies on partition pruning and NVMe rather than on RAM** - which is precisely why a query that cannot prune is refused by the estimator (Chapter 4 5.3.9.6) rather than executed. A Large installation does not cache 500 GB; it reads little because it prunes well.

### 6.1.9.4a Performance constants - calibration status

**These constants determine every hardware promise in 6.1.9.8. They are currently engineering estimates, not measured values, and they are labelled as such.**

| Constant | Value | Unit | Status | Reference hardware and workload |
|---|---|---|---|---|
| `API_QPS_PER_CORE` | 40 | interactive requests/s/core | **REFERENCE_ASSUMPTION** | x86-64 3.0 GHz, mixed widget-query workload, p95 under 1.5 s |
| `DB_SCAN_ROWS_PS_PER_CORE` | 250,000 | rows/s/core scanned | **REFERENCE_ASSUMPTION** | NVMe, partition-pruned range scan, 400-byte rows, warm cache |
| `DB_QPS_PER_CORE` | 60 | queries/s/core | **REFERENCE_ASSUMPTION** | Indexed point and small range queries |
| `HOT_WINDOW_DAYS` | 90 | days | **REFERENCE_ASSUMPTION** | Typical analysis window distribution |
| `HOT_INDEX_RATIO` | 0.15 | fraction | **REFERENCE_ASSUMPTION** | Observed index-to-heap ratio on the canonical model |
| `RECENT_DELTA_DAYS` | 7 | days | **REFERENCE_ASSUMPTION** | Delta re-read window under 5.3.9 |
| `CACHE_CAP` | 8 / 48 / 96 / 256 | GiB by class | **CALIBRATION_REQUIRED** | Economic cache ceiling per class |
| `bytes_per_row_b` | 400 | bytes | **REFERENCE_ASSUMPTION** | Canonical row with provenance triple |
| `index_factor` | 1.6 | ratio | **REFERENCE_ASSUMPTION** | Canonical plus its indexes |
| `results_ratio` | 0.07 | ratio | **REFERENCE_ASSUMPTION** | Results area against canonical |

> **No hardware profile is commercially quotable until its matching benchmark profile in 6.1.5.8 has passed.** On passing, each constant is replaced by a **versioned measured value** with its benchmark evidence recorded, the constant set version is incremented, and every quotation records which version produced it (6.3.8). **Until then, every sizing output carries the label "based on reference assumptions, pending performance certification."**

### 6.1.9.5 Deriving the classes

**The classes are boundaries in the driver space, not labels chosen first.**

| Class | D1 ingest | D3 compute | D5 retained | U_c | `CACHE_CAP` | Topology |
|---|---|---|---|---|---|---|
| **Small** | <= 10 rows/s | <= 2 slots | <= 250 GiB | <= 10 | 8 GiB | Single host |
| **Medium** | <= 100 rows/s | <= 8 slots | <= 2 TiB | <= 50 | 48 GiB | Separate database host, partitioned |
| **Large** | <= 1,000 rows/s | <= 24 slots | <= 20 TiB | <= 200 | 96 GiB | Load-balanced API, worker fleet, replica |
| **Very Large** | above | above | above | above | 256 GiB | Multi-node, HA, dedicated ML |

**A customer is placed in the class where every driver fits. One driver over the boundary promotes the whole class** - the same worst-dimension rule as the readiness gate, applied to iron. **Every worked example below is classified by this rule and no example contradicts it.**

### 6.1.9.6 Worked examples

> **Illustrative only, neutral synthetic values, computed in canonical units, and each verified against 6.1.9.5.** Not a benchmark and not a quotation.

#### Example A - Small / Light tier

```
INPUTS   L=2 links, O=15 objects, delta_rows_day=200,000, bytes_per_row=400
         retention 365 d, staging 14 d, J=12 jobs, starts_hour=20, runtime 45 s, weight 1
         U_n=10, U_c=4, U_d=3, widgets/page=6, refreshes/hour=4, ML none

DRIVERS  D1  ingest_rows_ps  = 200000/86400            = 2.31 rows/s      [Small]
         D2  interactive_qps = 3*6*4/3600              = 0.02 q/s
         D3  weighted_slots  = 20*45*1/3600            = 0.25 slots       [Small]
         D4  scanned_rows_ps = 12 * 50000 / 900        = 667 rows/s
         D5  ingest_bytes_ps = 2.31*400                = 924 B/s
             canonical_b     = 924*86400*1.6*365       = 46.6 GiB
             staging_b       = 924*86400*14*1.35       = 1.5 GiB
             results_b       = 46.6*0.07               = 3.3 GiB
             retained_b                                = 51.4 GiB         [Small]

SIZING   cpu_api  = ceil(0.02/40)+1                    = 1 core
         cpu_wrk  = max(ceil(0.25),1)                  = 1 core
         cpu_db   = ceil(667/250000)+ceil(0.02/60)+2   = 3 cores
         hot_window = 924*86400*1.6*90                 = 10.7 GiB
         cache_want = 0.15*10.7 + 924*86400*1.6*7      = 1.6 + 0.8 = 2.4 GiB
         cache_target = min(2.4, 8)                    = 2.4 GiB
         ram_db   = max(8, 1.25*2.4 + 0.75*3 + 4)      = 9.3 -> 16 GiB host total
         storage  = 51.4 * 1.30                        = 67 GiB -> 500 GB provisioned

CLASS    every driver Small.  ->  SMALL.  Matches Tier 1 specification of 6.1.9.8.
```

#### Example B - Medium / Pro tier. **The Chapter 4 5.3.9.1 scenario.**

```
INPUTS   L=3 links, O=300 objects, source footprint 146 GiB, daily change 0.5%
         -> delta_bytes_day = 0.73 GiB -> delta_rows_day = 1,959,579 at 400 B/row
         retention 730 d, staging 14 d, refresh floor 180 s
         J=10 jobs: 3 import(45s,w1,180s), 1 projection(90s,w2,180s),
                    3 analysis(240s,w2,900s), 1 practice(600s,w2,86400s),
                    1 prediction(120s,w4,900s), 1 report(300s,w1,86400s)
         U_n=25, U_c=15, U_d=12, widgets/page=8, refreshes/hour=12, ML active

DRIVERS  D1  ingest_rows_ps  = 1959579/86400           = 22.7 rows/s      [Medium]
         D2  interactive_qps = 12*8*12/3600            = 0.32 q/s
         D3  weighted_slots  = 3.92                    = 3.9 slots        [Medium]
         D4  scanned_rows_ps: DELTA-SCOPED per Ch4 5.3.9
             changed rows per 180 s window = 1959579*180/86400 = 4,082
             7 non-import jobs read the changed set: 7*4082/180 = 159 rows/s
             (a full-scan design would demand 5.7 GiB/s - Ch4 5.3.9.1)
         D5  ingest_bytes_ps = 22.7*400                = 9,073 B/s
             canonical_b     = 9073*86400*1.6*730      = 853 GiB
             staging_b       = 9073*86400*14*1.35      = 15.8 GiB
             results_b       = 853*0.07                = 60 GiB
             retained_b                                = 929 GiB          [Medium]

SIZING   cpu_api  = ceil(0.32/40)+1                    = 2 cores
         cpu_wrk  = max(ceil(3.92), 4)                 = 4 cores  (heaviest family weight 4)
         cpu_db   = ceil(159/250000)+ceil(0.32/60)+2   = 3 -> 4 with headroom
         hot_window = 9073*86400*1.6*90                = 105 GiB
         cache_want = 0.15*105 + 9073*86400*1.6*7      = 15.8 + 8.2 = 24.0 GiB
         cache_target = min(24.0, 48)                  = 24.0 GiB
         ram_db   = max(8, 1.25*24 + 0.75*4 + 4)       = 37 GiB -> 64 GiB provisioned
         ram_wrk  = 2*4 + 8 (model)                    = 16 GiB -> 16 GiB
         storage  = 929 * 1.30                         = 1,208 GiB -> 2 TB provisioned
         pool_int = ceil(0.32/8)+4 = 5 ; pool_bat = 6  ; max_connections >= 21

CLASS    D1 Medium, D3 Medium, D5 Medium, U_c Medium  ->  MEDIUM.
         Commercial: retained 929 GiB inside the Pro 1 TB envelope; 3 links,
         300 objects, 10 jobs, 180 s floor all inside Pro (6.3.4a).
         Matches Tier 2 specification of 6.1.9.8.

PROMOTION NOTE  extend retention to 36 months and canonical_b becomes 1,280 GiB,
                still Medium technically but ABOVE the Pro 1 TiB commercial
                envelope -> the customer buys Pro Plus capacity, not new hardware.
                This is the intended separation of commercial from technical.
```

#### Example C - Large / Enterprise tier

```
INPUTS   L=12, O=1,500, delta_rows_day=40,000,000, retention 730 d, staging 7 d
         J=400, U_n=400, U_c=150, U_d=120, widgets/page=8, refreshes/hour=20, ML heavy

DRIVERS  D1  = 40000000/86400                          = 463 rows/s       [Large]
         D2  = 120*8*20/3600                           = 5.3 q/s
         D3  weighted_slots (measured mix)             = 22 slots         [Large]
         D4  delta-scoped                              = 3,200 rows/s
         D5  ingest_bytes_ps = 463*400                 = 185,185 B/s
             canonical_b = 185185*86400*1.6*730        = 17.0 TiB
             staging_b   = 185185*86400*7*1.35         = 0.15 TiB
             results_b   = 17.0*0.07                   = 1.19 TiB
             retained_b                                = 18.3 TiB         [Large]

SIZING   cpu_api  = ceil(5.3/40)+1 = 2 per instance x 4 instances
         cpu_wrk  = max(ceil(22),4) = 22 -> 3 hosts x 8 cores
         cpu_db   = ceil(3200/250000)+ceil(5.3/60)+2   = 4 -> 8 with replica headroom
         hot_window = 185185*86400*1.6*90              = 2,146 GiB
         cache_want = 0.15*2146 + 185185*86400*1.6*7   = 322 + 167 = 489 GiB
         cache_target = min(489, 96)                   = 96 GiB   <- CAPPED
         ram_db   = max(8, 1.25*96 + 0.75*8 + 4)       = 130 GiB -> 128 GiB
         storage  = 18.3 TiB * 1.30                    = 23.8 TiB

CLASS    D1 Large, D3 Large, D5 Large, U_c Large  ->  LARGE, at the top of the band.
         Matches Tier 4 specification of 6.1.9.8.

CAP NOTE  cache_want 489 GiB exceeds CACHE_CAP 96 GiB. The design does not buy
          489 GiB of RAM; it relies on partition pruning and NVMe. This is why a
          query that cannot prune is REFUSED rather than executed (Ch4 5.3.9.6).
```

**All three examples are classified by the worst-dimension rule of 6.1.9.5, and each reproduces its tier specification in 6.1.9.8 from the formulas rather than by assertion.**

### 6.1.9.7 Capacity calculator specification

**One calculator, used by Engineering and by Sales, producing one answer.**

```
INPUT   the 6.1.9.2 inputs, with defaults per industry archetype and per class
   |
   +--> DRIVERS      D1..D5 computed and displayed, with the dominant driver named
   |
   +--> RESOURCES    every formula of 6.1.9.4, each showing its inputs
   |
   +--> CLASS        Small | Medium | Large | Very Large, with the promoting driver named
   |
   +--> DEPLOYMENT   recommended topology, node shapes, storage, pool sizes, worker counts
   |
   +--> COST         monthly infrastructure cost from the price book of 6.3.5
   |
   +--> ENVELOPE     the capacity envelope this deployment supports, in the
                     five metered dimensions of Chapter 1.7.1
```

| Property | Rule |
|---|---|
| Determinism | The same inputs always produce the same output. Version the constants; record which version produced a quotation |
| Transparency | Every figure shows its formula and its inputs. **No unexplained number** |
| Headroom | Applied explicitly and visibly, never hidden inside a constant |
| Sensitivity | Shows which driver dominates and what a 2x change in it would cost |
| Honesty | Where an input is a guess, it is marked an assumption and appears in the quotation's assumption list |
| Ownership | Specified in 6.3.8 as an internal Administration and Sales tool, not a customer-facing page |


### 6.1.9.8 Hardware and server specification per licence tier

**Every licence tier states the infrastructure it requires.** Sales quotes it, the customer provisions it in on-premise deployments, and the vendor provisions it in hosted ones. **A tier without a stated server specification is a promise nobody can cost.**

These specifications are **derived from the drivers of 6.1.9.3 at each tier's capacity envelope (6.3.4a)**, assuming the delta-propagation architecture of Chapter 4 5.3.9. They are not chosen and they are not marketing sizes.

#### Tier 1 - LIGHT

| Resource | Specification | Derived from |
|---|---|---|
| **Deployment** | Single host, all containers, one database | Small class |
| **CPU total** | **4 cores** | api 1 + workers 1 + db 2 |
| **RAM total** | **16 GB** | api 2.5 + workers 3 + db 8, rounded up |
| **Storage** | **500 GB SSD**, 3,000 IOPS | 250 GB retained + staging + 30% headroom |
| **Backup storage** | 250 GB | daily change x 30 + weekly + monthly |
| **Network** | 100 Mbit/s | ingest under 10 rows/s |
| **Database connections** | 25 | pool_int 5 + pool_bat 3 + overhead |
| **Worker instances** | 1, all pools co-resident | D3 under 2 slots |
| **Model serving** | none | assistant not included below Pro Plus |
| **Suits** | 1 line or 1 department, roughly 10-30 GB source footprint | |

#### Tier 2 - PRO

| Resource | Specification | Derived from |
|---|---|---|
| **Deployment** | Application host + separate database host | Medium class entry |
| **App host CPU / RAM** | **4 cores / 16 GB** | api 2 + workers 4 |
| **DB host CPU / RAM** | **8 cores / 32 GB** | working set = hot partitions of ~350 GB retained |
| **Storage** | **2 TB SSD**, 10,000 IOPS, partitioned monthly | 320-800 GB retained + staging + growth |
| **Backup storage** | 1 TB, with WAL archiving for point-in-time recovery | |
| **Network** | 500 Mbit/s | peak ingest |
| **Database connections** | 60 | pool_int 10 + pool_bat 12 + overhead |
| **Worker instances** | 2 (import+projection, analysis+report) | D3 up to 8 slots |
| **Model serving** | none | |
| **Suits the worked scenario of Chapter 4 5.3.9.1** | **3 DB-links, 300 tables, 146 GB source footprint, 10 jobs at 3-minute cadence** | **Only because every job class is delta-scoped. The same workload on a full-scan design would need roughly 5.7 GB/s of sustained scan and is not deployable at any price** |

#### Tier 3 - PRO PLUS

| Resource | Specification | Derived from |
|---|---|---|
| **Deployment** | Application host + database host + ML/serving host | Medium to Large |
| **App host CPU / RAM** | **8 cores / 32 GB** | api 3 + workers 8 |
| **DB host CPU / RAM** | **16 cores / 64 GB** | D4 with ML feature reads; working set of hot partitions |
| **ML / serving host CPU / RAM** | **8 cores / 32 GB** | ml pool weight 4 + model cache + assistant serving |
| **Storage** | **8 TB**, 20,000 IOPS, partitioned and compressed | up to ~5 TB retained + snapshots + artifacts |
| **Object storage** | 2 TB | archives, exports, model artifacts, feature snapshots |
| **Backup storage** | 4 TB with PITR | |
| **Network** | 1 Gbit/s | |
| **Database connections** | 120 | pool_int 20 + pool_bat 26 + overhead |
| **Worker instances** | 4, with `ml` isolated on its own host | D3 up to 16 slots |
| **Model serving RAM** | **16 GB reserved** | artifact size x cache depth + 4 |
| **Suits** | A whole plant with prediction, practice learning and the assistant | |

#### Tier 4 - ENTERPRISE

| Resource | Specification | Derived from |
|---|---|---|
| **Deployment** | Load-balanced app tier, primary + replica database, dedicated worker fleet, dedicated ML nodes | Large to Very Large |
| **App tier** | **4 instances x 4 cores / 16 GB** behind a balancer | D2 with 150+ concurrent |
| **DB primary** | **32 cores / 128 GB**, NVMe | D4 at 1,000+ rows/s ingest |
| **DB replica** | Same shape, serving the interactive read path | Read/write split |
| **Worker fleet** | **3 hosts x 8 cores / 32 GB**, plus **2 ML hosts x 8 cores / 64 GB** | D3 above 24 slots |
| **Storage** | **50 TB+**, tiered hot/cold, partitioned, compressed, downsampled | 20 TB+ retained |
| **Object storage** | 20 TB+ | |
| **Backup** | Full PITR, off-site copy, **automatic failover with fencing** | RPO 1 min, RTO 30 min |
| **Network** | 10 Gbit/s | |
| **Database connections** | 300+, separate poolers per path | |
| **High availability** | Synchronous or near-synchronous replica in a separate failure domain | |
| **Suits** | Multi-site, air-gapped, or very high ingest | |

#### The tier-to-hardware rule

| Rule | Statement |
|---|---|
| **Derived, never chosen** | Every figure above comes from the formulas of 6.1.9.4 evaluated at the tier's capacity envelope. Changing an envelope recomputes the specification |
| **Stated in the offer** | The specification for the quoted tier appears in the quotation and in the handover of 6.3.10 |
| **Minimum, not target** | A customer at the top of a tier's envelope needs the stated specification. A customer at the bottom may run smaller, and the calculator says so |
| **Headroom included** | Every figure carries the 30 percent twelve-month growth headroom of 6.1.9.4 |
| **On-premise parity** | An on-premise customer receives the same specification. **The product does not run better on our hardware than on theirs** |
| **Under-provisioning is visible** | An installation running below its stated specification is detected by monitoring and reported as a capacity finding, not silently endured |

---

## 6.1.10 Performance and capacity protection

### 6.1.10.1 How the Chapter 4 mechanisms protect the sized machine

**The sizing model says what to buy. These nine mechanisms, all specified in Chapter 4 5.3, keep the machine within what was bought.**

| Mechanism | Chapter 4 | Protects against |
|---|---|---|
| **Delta propagation across every job class** | **5.3.9** | **The 96,000:1 case: rescanning the model at cadence. This is the mechanism that makes the tier envelopes of 6.3.4a affordable** |
| Incremental acquisition | 5.3.2 M1 | Reading the whole source every cycle |
| Schedule jitter and coalescing | 5.3.2 M2 | The thundering herd at `:00` |
| Skip-if-running, latest-only | 5.3.2 M3 | Run pile-up |
| **Weighted pools with a reserved interactive share** | 5.3.2 M4 | Batch starving the read path |
| Per-source load budget | 5.3.2 M5 | Overloading the customer's database |
| Incremental feature refresh | 5.3.2 M6 | Analysis rescanning history |
| Partitioning and retention | 5.3.2 M7 | Scans growing without limit |
| Timeouts, circuit breakers, reaper | 5.3.2 M8 | One query holding everything |
| Pre-run cost estimator | 5.3.8 | Starting something that cannot fit |
| Execution placement | 5.4.8 | Moving rows that should be aggregated in place |
| Rate limiting | Ch3 4.6.4 | One client consuming the API |
| Connection pool separation | 6.1.2.4 | Batch exhausting interactive connections |
| Prediction latency deadlines | 5.8.8 | Work that arrives too late to matter |

### 6.1.10.2 The four utilisation bands

**The product queues, throttles and refuses honestly. It does not crash, and it does not degrade silently.** The band is computed from the worst of: worker pool utilisation, database CPU, database connections, disk, and interactive p95 latency.

| Band | Trigger | Behaviour | What the user sees |
|---|---|---|---|
| **Normal, below 70%** | - | All pools at configured parallelism | Nothing |
| **Elevated, 70-80%** | any measure >= 70% | Report and ML pools reduced; coalescing more aggressive; the estimator warns earlier | A note in the Jobs Monitor and the activity tray |
| **High, 80-90%** | any >= 80% | Non-critical cadences stretched by a factor of 3; new heavy authored queries warned before running; backfill paused | **Banner: "Analysis is running behind. Cadences temporarily stretched."** with what is deferred |
| **Critical, 90-100%** | any >= 90% | Only import and the interactive path admitted; everything else queued; **the interactive reservation is never released** | Banner naming exactly what is deferred and the estimated catch-up |
| **Protective, at 100%** | sustained saturation | New user-triggered runs **refused with `QT04`**, a named reason and an estimated wait. Scheduled runs queue | The refusal sentence, with what to do |

**Three rules.** Every band change is **announced**, logged on the system channel and routed by E6 where a rule exists. The interactive share is **never** surrendered, because a plant whose dashboards die when the engine is busy has failed. And a prolonged Critical or Protective band raises a **capacity review** signal that feeds the upgrade conversation of 6.3, with the measured driver named - so an upgrade is proposed from evidence rather than from a complaint.

---

## 6.1.11 Backup, restore, disaster recovery and high availability

### 6.1.11.1 What is protected, and why

| Asset | Why it cannot be lost | Recovery source |
|---|---|---|
| `ppiq_meta` | Definitions, relationships, licence, users, logs. **This is the customer's authored work** | Backup |
| `ppiq_plant` canonical | The plant model | Backup, or reprojection from staging where still retained |
| `ppiq_plant` results | Findings, predictions, practices, decisions, evaluations. **Historical evidence that cannot be recomputed identically** | Backup |
| `ppiq_staging` | Recent source-shaped copies | Backup, or re-import where the source retains history |
| Object storage | Archives, exports, model artifacts, snapshots | Object backup |
| Vault | Credentials | The vault's own mechanism, **never inside the database backup** |

### 6.1.11.2 What is deliberately not backed up

The retrieval index, `plant_relationship_paths`, `genealogy_paths`, `prediction_current`, and staging beyond its retention. **All are derived and rebuildable** (6.1.4.5). Backing them up would inflate the backup and slow the restore for no recovery benefit.

### 6.1.11.3 Schedule, retention and encryption

| Concern | Design |
|---|---|
| Continuous | Write-ahead archiving to object storage, enabling point-in-time recovery to any moment in the window |
| Base backup | Nightly, off-peak, throttled so it does not consume the interactive share |
| Object storage | Versioned with its own lifecycle; archives carry a content hash and a verification timestamp |
| Retention | Daily 30 days, weekly 12 weeks, monthly 12 months; configurable per contract |
| Encryption | At rest with a customer-scoped key; in transit; **the key is never in the same store as the backup** |
| Customer export | Where the contract provides it: a scheduled logical export of `ppiq_meta` definitions plus a canonical extract, RLS-scoped, with the data dictionary attached |

### 6.1.11.4 Consistency rule

**A restore restores `ppiq_meta`, `ppiq_plant` and `ppiq_staging` as one consistent set to one point in time.** A definition version without its relationships, or a finding without its run, is unexplainable. Object storage is restored to the same point, and any artifact referenced by a restored row but absent is reported rather than silently missing.

### 6.1.11.5 The tested restore acceptance procedure

> **A backup is not valid because a file exists. It is valid because a restore was performed and accepted.**

**Monthly, automated, into an isolated environment:**

1. Provision a clean environment. 2. Restore the most recent base plus WAL to a chosen point. 3. Restore object storage to the same point. 4. Run `ppiq-migrator` and confirm the level matches. 5. Run the fourteen post-deployment checks of 6.1.4.6. 6. **Verify integrity by sampling**: a definition opens with its versions; a relationship resolves; a genealogy walk returns weights summing to 1.0; a finding resolves to its run and population; a prediction resolves to its drivers and evaluation; an audit row is present and unmodified. 7. Record the outcome, the measured RTO and the achieved RPO. 8. Destroy the environment.

**Gate 16 of 6.1.7 fails the release if the last verification is outside its window or did not pass.**

### 6.1.11.6 Objectives, disaster recovery and high availability

| Class | RPO | RTO | Standby | Failover |
|---|---|---|---|---|
| Small | 24 h (nightly) or 15 min with WAL | 8 h | None | Restore |
| Medium | 15 min | 4 h | Warm standby optional | Manual, rehearsed |
| Large | 5 min | 2 h | Streaming replica | Manual with a documented runbook |
| Very Large / Enterprise | 1 min | 30 min | Synchronous or near-synchronous replica, separate failure domain | **Automatic, with fencing** |

**Disaster recovery** is a documented, rehearsed runbook naming the roles, the decision authority, the sequence, the communication and the acceptance checks. **Rehearsed at least annually**, and the rehearsal result recorded like a restore verification. **A DR plan that has never been executed is a document, not a capability.**

**Tenant-level restore.** A single tenant can be restored into an isolated instance without disturbing others, using RLS-scoped logical extraction. Every restore of any kind is an audit entry naming who, when, from which point, into which environment.

---

## 6.1.12 Monitoring and observability

### 6.1.12.1 The boundary with logging

**Logging is a product capability specified in Chapter 3 4.5.15 and read by administrators on F5.** Monitoring is an operations capability for whoever runs the infrastructure. They **integrate and do not duplicate**:

| | Logging (Chapter 3) | Monitoring (here) |
|---|---|---|
| Question | What did the platform do? | Is the platform healthy? |
| Storage | PostgreSQL log tables | Time-series metrics |
| Read on | F5, F9 | The operations dashboard |
| Retention | Policy per channel, F9 | Metric retention, 6.1.12.5 |
| Link | A metric alert **names the run identifier** so an operator pivots into F5's cross-family correlation | |

### 6.1.12.2 Metrics per component

| Component | Metrics |
|---|---|
| Web | Availability, bundle load time, client error rate |
| API | Request rate, p50/p95/p99 latency by domain, error rate by code, in-flight, rate-limit rejections |
| Workers | Pool utilisation, queue depth, **admission refusals**, wait time, runtime by family, weighted slots in use |
| PostgreSQL | CPU, memory, connections used against pool, cache hit, longest transaction, replication lag, deadlocks, bloat, partition count |
| Pooler | Client and server connections, wait time, per identity |
| Object storage | Capacity, request rate, errors, archive verification age |
| Collector | Queue depth, push success rate, source read latency, **budget refusals**, breaker state |
| Model gateway | Request rate, latency, serving mode, egress attempts, refusals |
| Job queues | Depth, oldest queued, blocked-by-dependency count, reaped count |
| **Prediction latency** | Delivery latency p50/p95, **deadline miss rate per outcome and route** |
| **Scan amplification** | **SAR per job family and per definition, against its certified baseline** (6.1.12.2a) |
| Import freshness | Watermark age per dataset, against its cadence |
| Host | Disk used and growth rate, memory, CPU, network |
| Backup | Last success, size, duration, **age of last verified restore** |
| Certificates | Days to expiry for every certificate |

### 6.1.12.2a Scan Amplification - the delta-integrity metric

**Delta propagation is now one of the product's central economic assumptions** (Chapter 4 5.3.9). An implementation that quietly reverts to a full scan would not fail a functional test - it would simply make the customer buy larger hardware. **That must be directly measurable.**

```
Scan Amplification Ratio (SAR)
    SAR = downstream_bytes_scanned / bytes_changed_since_previous_run

  measured per run, aggregated per job family and per definition version
```

| Band | SAR | Interpretation | Action |
|---|---|---|---|
| **Ideal** | <= 5 | Reading the changed set plus its index and join overhead | None |
| **Acceptable** | 5 - 25 | Windowed recompute, or a method that is not decomposable | None; expected for declared windowed classes |
| **Warning** | 25 - 200 | Something is reading more than it should | **P3 observability finding**, naming the job family and definition |
| **Critical** | > 200 | Effectively a full scan at cadence | **P2 alert.** The delta law is being violated |
| **Regression** | any sustained increase above the recorded baseline for that definition | An implementation change reverted delta scoping | **Fails the performance regression gate** |

**Three obligations.**

1. **Every job run records `bytes_scanned` and `bytes_changed`**, so SAR is computed rather than estimated. Both are already available: the first from the execution plan, the second from the watermark range consumed.
2. **A per-definition SAR baseline is recorded at certification** and stored with the constant set version. **A release whose SAR exceeds its baseline by more than the declared tolerance fails gate 15** of 6.1.7.
3. **SAR appears on the Workload dashboard** (6.1.12.4) by family, so an operator sees amplification before it becomes a hardware conversation.

> **This metric exists to protect the customer from us.** If SAR rises, the correct response is to fix the implementation, not to sell a larger server.

### 6.1.12.3 Alerts, severity and escalation

| Severity | Meaning | Examples | Response | Escalation |
|---|---|---|---|---|
| **P1 Critical** | Product unusable or data at risk | API down, database down, disk above 95%, backup failed twice, replication broken, certificate expired | Immediate | Page on-call, then lead at 15 min |
| **P2 High** | Degraded or a deadline being missed | Sustained Critical band, **deadline miss rate above 5%**, **Scan Amplification above 200 for any family**, import freshness beyond twice cadence, restore verification overdue, breaker open above 1 h | 30 min | Ticket, lead notified at 2 h |
| **P3 Medium** | Trending toward a problem | Elevated band sustained, disk above 80%, queue depth rising, certificate within 30 days | Next business day | Ticket |
| **P4 Low** | Informational | Band transitions, deployments, capacity review signal | Review weekly | Ticket |

**Every alert names its metric, its measured value, its threshold, and the run identifier or component**, so the first action is investigation rather than discovery. An alert that cannot be acted on is deleted rather than tolerated.

### 6.1.12.4 Dashboards

Five, and no more, so that each is read rather than admired: **Platform health** (availability, error rate, latency, band); **Workload** (pool utilisation, queue depth, wait time, admission refusals, driver values against the sized envelope, **Scan Amplification by family against baseline**); **Data pipeline** (import freshness, batch outcomes, quarantine rate, projection throughput); **Intelligence** (gate verdicts, run outcomes, prediction latency and miss rate, model drift, fallback in use); **Operations** (backup and last verified restore, certificate expiry, capacity headroom, licence expiry).

### 6.1.12.5 Retention and service signals

Raw metrics 15 days at full resolution; 5-minute aggregates 90 days; hourly 13 months for capacity trending. Alert history 24 months.

| Signal | Definition | Target |
|---|---|---|
| Availability | Successful health checks over total | 99.5% Medium, 99.9% Large and above |
| Interactive latency | API p95 across interactive domains | Within 6.1.5.7 |
| Import freshness | Watermark age against cadence | 95% of datasets within 2x cadence |
| **Actionable latency** | Deadline miss rate | **<= 1%**, per 6.1.5.7 |
| Job success | Completed over started, excluding honest blocks | >= 99% |
| Backup verification | Age of the last passed restore | Within its window |

**A blocked run is not a failure and never counts against job success.** Counting an honest abstention as an incident would create pressure to weaken the gate, which is the one thing Chapter 1.5.2 forbids.

---

# 6.2 THE PUBLIC WEBSITE

> **Target audience:** the chief executive and purchasing function of a manufacturing customer, arriving to evaluate a supplier.
>
> **Voice:** senior UI/UX designer, senior frontend developer, product owner.
>
> **The governing instruction, stated first because it governs everything below:** an implementation of the website already exists and is **approximately 75 percent aligned with the intended vision**. It is the authoritative first draft and visual baseline. **It is not to be replaced.** The method is: audit, preserve what is strong, identify what is missing or inconsistent, enhance, complete the missing products and commercial content, and raise the whole to premium enterprise-industrial standard.

## 6.2.0 The current implementation, audited

**Deliverable required by the review before any change: Current -> Keep -> Enhance -> Refactor -> Replace -> Remove, with a reason for each.**

Audited against the implementation under `PlantProcess.Website`: 77 files, 20 components, 5 content modules, 5 stylesheets, 3 brand assets, 4 Playwright suites and 7 validation scripts.

### 6.2.0.1 Brand and shell

| Component | Current function | Verdict | Reason |
|---|---|---|---|
| `SOUBrand.tsx`, `sou-brand.css` | SOU corporate identity, 171 lines of brand CSS | **KEEP** | The corporate identity is correct and is about to become more important, not less |
| `sou-logo-horizontal.svg`, `sou-icon.svg` | Company marks | **KEEP** | |
| `plantprocess-iq-logo.svg` | Product mark | **KEEP** | PPIQ remains the flagship product and keeps its own mark |
| Premium header, solutions menu | Navigation | **ENHANCE** | Structurally sound and visually right. It must gain a **Products mega-menu** for five products, per 6.2.2. **Do not rebuild it** |
| `global.css`, `phase10.css`, responsive CSS | Dark industrial theme, cyan and blue language, responsive foundations, reduced-motion support | **KEEP** | This is the visual identity the review is satisfied with. It is also consistent with Chapter 1.9 |
| Accessibility handling | Reduced motion, focus, semantics | **KEEP and EXTEND** | Extend to the new pages rather than re-solving |

### 6.2.0.2 Graphics - the strongest existing asset

| Component | Current function | Verdict | Reason |
|---|---|---|---|
| `HeroTopology.tsx` | Animated plant topology hero | **KEEP, REFACTOR to reusable** | Excellent. It currently serves PPIQ; it should be parameterisable so the company hero and the product heroes can each use it with their own node set |
| `GoldenThread.tsx`, `GoldenThreadScroll.tsx` | Scroll-drawn genealogy thread | **KEEP** | This is PPIQ's single best visual argument. It stays on the PPIQ page |
| `TrustEngine.tsx` | The honesty and evidence machinery | **KEEP** | Visualises the readiness gate and the evidence chain, which is the moat |
| `SignalVsNoise.tsx` | Statistical discipline | **KEEP** | |
| `ArchitectureFlowScroll.tsx` | Architecture flow on scroll | **KEEP, REFACTOR** | The scroll-draw mechanism should become the shared basis for the four new product workflow graphics |
| `useScrollDraw.ts` | The scroll-draw hook | **KEEP, PROMOTE** | 73 lines carrying the site's signature motion. **Promote to a shared primitive** rather than duplicating it per product |

### 6.2.0.3 Commercial sections

| Component | Current function | Verdict | Reason |
|---|---|---|---|
| `RequestDemoForm.tsx` | 339-line lead capture | **KEEP, ENHANCE** | Substantial and working. Enhance to carry **product context**, so a demo request from the QES page arrives labelled QES |
| `RoiCalculator.tsx` | Interactive ROI | **KEEP, RELABEL** | Sound. Must be labelled a **PPIQ value estimator on your own assumptions**, never a promise (Chapter 1.9) |
| `PricingLicenseMatrix.tsx` | Tier matrix | **ENHANCE** | Currently one PPIQ matrix. Must become **product-selectable**, per 6.2.16 |
| `ProductScreenshotShowcase.tsx` | Screenshot frame | **KEEP, EXTEND** | The frame is reusable across all five products |
| `IntegrationEcosystem.tsx` | Integration groups | **KEEP** | |
| `ProofOfValueJourney.tsx` | Proof storytelling | **KEEP** | |
| `RolePaths.tsx` | Role-based commercial stories | **KEEP** | Maps to the buyer table of Chapter 1.4 |
| `FounderAuthority.tsx` | Founder credibility | **KEEP** | A young supplier's credibility is a real objection (Chapter 1.8) |
| `BrandProofSection.tsx` | Brand proof | **KEEP** | |
| `ConnectorHonestyBlock.tsx` | Honest connector availability | **KEEP - IMPORTANT** | This directly implements Chapter 1.5.9. **Do not remove it to make the site look stronger** |
| `PositioningTruthBlock.tsx` | Positioning honesty | **KEEP** | Same reason |

### 6.2.0.4 Content and product model

| Artifact | Current function | Verdict | Reason |
|---|---|---|---|
| `content/products/model.ts` | `ProductPageModel`: id, slug, name, category, headline, subTagline, problem, capabilities, benefits, diagram, licensing, cta, evidencePosture | **KEEP the idea, EXPAND the shape** | A shared product contract is exactly right. It is currently too small for the Golden Rule of 6.2.9 |
| `content/products/mes.ts` | 88 lines of MES content | **KEEP as source material, REPOSITION** | Its content is reusable but its positioning - "we read your MES, we do not replace it" - describes **PPIQ's integration stance, not an MES product**. That sentence belongs on the PPIQ page |
| `content/products/yardWarehouse.ts` | 87 lines | **KEEP, EXPAND** | Currently read-only material-flow visibility. Expand to the management product of 6.2.7 |
| `content/products/index.generated.ts` | Registry of two products | **EXPAND** | Must carry all five |
| `content/phase1WebsiteProof.ts` | 179 lines of proof content | **KEEP, AUDIT against Chapter 1** | Every claim must trace to Chapter 1.11 |

### 6.2.0.5 Routing - the one structural error

| Route | Current | Verdict | Reason |
|---|---|---|---|
| `/` | `NewHomePage` | **RESTRUCTURE** | Keep the visual language and most graphics; change the story from product-first to company-first (6.2.3) |
| `/product` | `PlatformPage` | **REDIRECT** | To `/products/plantprocess-iq` |
| `/products/:code` | **`LegacyProductRoute` redirecting into PPIQ pack pages** | **REPLACE** | **This is the fundamental error.** It encodes that the other products are PPIQ capability packs. See 6.2.1 |
| `/packs/:code` | `PackPage` | **KEEP, RESCOPE** | Packs are a legitimate PPIQ concept. They must live under PPIQ, not stand in for products |
| `/solutions/:code` | `RolePage` | **KEEP** | Solutions by problem is correct and complements products |
| `/proof`, `/security`, `/pricing`, `/about`, `/contact` | Pages | **KEEP, ENHANCE** | Security and Pricing need the per-product treatment of 6.2.16 and 6.2.17 |
| `/products` | **absent** | **ADD** | The portfolio page of 6.2.15 |

### 6.2.0.6 Tests

| Artifact | Verdict | Reason |
|---|---|---|
| `commercial-v2.spec.ts`, `phase7-golive`, `phase7-leadcapture`, `phase7-responsive` | **KEEP, EXTEND** | Good coverage of responsiveness and lead capture. Extend to five products at desktop and mobile |
| `validate-commercial-v2.mjs` | **AMEND** | It asserts the current route set including `path="/products/:code"`. That assertion now **protects the wrong architecture** and must be replaced per 6.2.19 |
| `validate-premium-header.mjs`, `validate-phase7-content.mjs`, `validate-website-content.mjs` | **KEEP, EXTEND** | |
| `website-soak.mjs`, `check-tagline.mjs`, `check-http-https.mjs` | **KEEP** | |

### 6.2.0.7 Audit summary

| Verdict | Count | Share |
|---|---|---|
| **Keep as-is** | 18 | The visual identity, the graphics, the honesty blocks, the proof and lead machinery |
| **Keep and enhance** | 9 | Header, pricing, security, demo form, screenshot frame, product model, content modules |
| **Refactor and reuse** | 4 | `useScrollDraw`, `HeroTopology`, `ArchitectureFlowScroll`, `ProductScreenshotShowcase` |
| **Replace** | 1 | `LegacyProductRoute` - the only structural replacement |
| **Remove** | 1 | The sibling-product-must-not-exist assertion |
| **Add** | 7 | `/products` portfolio, four product pages, the ecosystem graphic, the mega-menu |

**One component is replaced and one assertion is removed. Everything else is preserved, enhanced or reused.** That is the correct proportion for a baseline the review is 75 percent satisfied with.

---

## 6.2.1 The product-positioning correction

**The current architecture treats Quality, Reliability, Energy and Yard as capability packs of PlantProcess IQ, and redirects `/products/*` into PPIQ pack pages. That is not the company architecture.**

> **SOU Industrial Software has five separate industrial software products: PlantProcess IQ, MES, QES, Yard and Warehouse Management, and Energy Management. PPIQ is the flagship. PPIQ is not the company, and PPIQ is not a container around the other four.**

| Concept | Correct meaning |
|---|---|
| **Product** | One of the five. Independently sold, independently useful, its own page, its own licence model |
| **Pack** | A PPIQ capability grouping. Lives **under** PPIQ at `/products/plantprocess-iq` and `/packs/:code` |
| **Solution** | A problem a customer has - quality, reliability, energy, material flow - which one or more products address. Lives at `/solutions/:code` |

**The failure mode this corrects:** a visitor arriving at `/products/energy` currently learns that energy is a feature of an analytics product. A customer who wants an Energy Management System concludes SOU does not sell one, and leaves.

---

## 6.2.2 Information architecture

**Home** | **Products** (mega-menu) | **Solutions** | **Industries** | **Proof / Why SOU** | **Security** | **Pricing** | **Company** | **Contact** | **Request Demo** (primary)

**The Products mega-menu**, because five product names side by side in a header is not premium:

```
+---------------------------------------------------------------------------+
|  PRODUCTS                                                                  |
|                                                                            |
|  [icon] PlantProcess IQ          [icon] Quality Execution System           |
|         Plant intelligence               Quality governed and recorded      |
|                                                                            |
|  [icon] Manufacturing Execution  [icon] Yard & Warehouse Management        |
|         Production executed              Material located and moved         |
|                                                                            |
|  [icon] Energy Management         ------------------------------------      |
|         Consumption understood     View all products  ->  /products         |
+---------------------------------------------------------------------------+
```

Each entry: product icon, name, one-line value proposition. Opens on hover and on focus; **fully keyboard navigable and closable with Escape**; collapses to an accordion on mobile.

**Industries** - metals, paper, food and beverage, pharmaceutical, minerals, automotive, chemicals, other process industries - carries a genericity obligation: these are **markets served**, and the pages must not imply industry-specific product content, because Chapter 1, Rule 1 says none exists.

---

## 6.2.3 The home page - company first, then products

**Keep the current visual style and most of the current graphics. Restructure the story.**

| # | Section | Reuse | Change |
|---|---|---|---|
| 1 | **Hero** | `HeroTopology`, parameterised to a **company-level** node set showing all five domains | Headline becomes SOU-level, not PPIQ-level. Primary CTA **Explore our products**; secondary **Request a demo** |
| 2 | **Product portfolio** | New, using existing card styling | Five cards, each with name, one-line value proposition, the industrial problem it owns, three key capabilities, its own icon and an **Explore product** CTA. **Not five plain text boxes** - each must read as a serious industrial platform |
| 3 | **Industrial software ecosystem graphic** | New, built on `useScrollDraw` | Interactive on hover and scroll. Shows how the five coexist in a plant |
| 4 | **Why SOU** | `FounderAuthority`, `BrandProofSection` | Kept |
| 5 | **Evidence and honesty** | `TrustEngine`, `SignalVsNoise`, `PositioningTruthBlock` | Kept. These are company-level differentiators, not only PPIQ ones |
| 6 | **Solutions by problem** | `RolePaths` | Kept |
| 7 | **Integration ecosystem** | `IntegrationEcosystem`, `ConnectorHonestyBlock` | Kept |
| 8 | **Proof journey** | `ProofOfValueJourney` | Kept |
| 9 | **Request demo** | `RequestDemoForm` | Enhanced to carry product context |

**The ecosystem graphic**, and what it must and must not say:

```
                    PlantProcess IQ
                Intelligence / Analytics
                          |
        +-----------------+-----------------+
        |                 |                 |
       MES               QES             Energy
    Execution          Quality           Energy
        |                 |                 |
        +------- Yard / Warehouse ----------+
                 Material Flow
```

> **It must not imply that any product depends on PPIQ.** Every one of the five is independently useful and independently sold. The graphic shows that **data can be connected across the portfolio where that integration is implemented**, and hovering a product states plainly what it does alone. A visitor who wants only QES must not conclude they need PPIQ first.

---

## 6.2.4 Product 1 - PlantProcess IQ

**Chapters 1 to 5 are the authoritative source of truth for every claim on this page. No PPIQ capability may be invented from marketing imagination**, and every claim traces to the traceability table of Chapter 1.11.

**The journey the page communicates, in order:** fragmented plant data -> permanent plant model -> genealogy -> unified visibility -> statistics -> machine learning -> practice learning -> prediction -> explainability -> **safe, historically supported remediation** -> human decision -> outcome measurement -> value and ROI -> grounded assistant.

**Preserved and enhanced:** `HeroTopology`, `GoldenThread`, `TrustEngine`, `SignalVsNoise`, `ArchitectureFlowScroll`, `ProductScreenshotShowcase`, `ProofOfValueJourney`, the security narrative, `RoiCalculator`, `RequestDemoForm`. Add real screenshots as surfaces become demonstrable, and animated explanations of practice learning and the predict-then-remediate journey, which are the two newest and least understood capabilities.

**Three honesty obligations specific to this page**, each traceable to Chapter 1: PPIQ is an **evidence-grade intelligence product, not autonomous AI**; the remediation story must say that the product **suggests and a human decides, and that it never sends a control instruction**; and the ROI calculator is labelled an estimate on the visitor's own assumptions, never a promise.

---

## 6.2.5 Product 2 - Manufacturing Execution System

**Presented as a real MES product, not as "MES integration for PPIQ".** The existing `mes.ts` content is reusable, but its current framing - *we read your MES, we do not replace it* - describes PPIQ's integration stance. **That sentence moves to the PPIQ page and does not appear here.**

**The problem.** Without an MES, production teams work from printed orders and spreadsheets: what is actually in progress is unclear, confirmations are manual and late, traceability is assembled after the fact, and a disruption is discovered rather than managed.

**The solution.** SOU MES turns a production plan into governed plant execution: orders dispatched to work centres, operations confirmed as they happen, material consumption recorded, genealogy built as a by-product of execution rather than reconstructed afterwards.

**Capabilities**, each carrying an internal status per 6.2.10: production order execution; work-order management; scheduling and dispatching; route and operation execution; WIP visibility; material consumption; material genealogy; equipment and station execution; operator interaction; production declarations; shift visibility; downtime and events; production KPIs; traceability; electronic production records; ERP integration; the level-2 and automation integration boundary; quality integration; warehouse and material-flow integration; reporting; audit and history; roles and permissions; dashboards; operator and mobile views.

**Business benefits:** execution visibility, higher schedule adherence, less manual reporting, better traceability, faster production reconciliation, lower WIP uncertainty, better order genealogy, faster response to disruption.

**The interactive workflow graphic**, built on `useScrollDraw`:

```
ERP Order -> Production Order -> Route -> Work Centre -> Operator / Machine
          -> Material Consumption -> Production Confirmation -> Genealogy -> KPI / Reporting
```

Each node opens a short explanation on hover or focus; the flow draws as the visitor scrolls.

**Licensing dimensions**, deliberately **not** a copy of PPIQ's Light / Pro / Pro Plus, because MES value scales with production scope rather than analytical depth: per plant or site; per line or work centre; concurrent users; functional modules; integration scope; support tier. **Commercial figures remain Contact Sales until 6.3 approves them.**

**Control boundary**, stated truthfully per 6.2.17: **MES carries execution authority by design.** It dispatches, confirms and records production. It is not read-only, and the PPIQ read-only rule must not be copied onto it.

---

## 6.2.6 Product 3 - Quality Execution System

**A complete, independent product page. QES is not a PPIQ quality pack.**

**The purpose.** A Quality Execution System governs, executes and records quality activity across production: it decides what must be inspected, ensures it is inspected, captures the result, evaluates it against specification, and holds or releases the material accordingly.

**The problem.** Inspection plans live in documents, checks are missed under pressure, results sit in a laboratory system disconnected from the material, nonconformances are handled by email, and audit preparation is an archaeology exercise.

**Capabilities:** inspection plans; quality plans; inspection execution; sample management; test and result capture; specification limits; acceptance and rejection workflows; defect and nonconformance recording; hold and release status; disposition; deviation handling; quality genealogy; laboratory and LIMS interaction; operator quality checks; automated inspection-device integration; certificates and quality records; trends and SPC; audit trail; role approval; electronic signatures where regulated use cases require them; reporting; quality dashboards.

**Business benefits:** standardised inspection execution; prevention of missing quality checks; faster nonconformance response; complete traceability; one quality record across production; reduced paper and manual administration; faster release decisions; easier audit preparation.

**The interactive workflow graphic:**

```
Material / Production Step -> Quality Plan -> Inspection Point -> Measurement / Test
   -> Specification Evaluation -> Accept / Hold / Reject -> Disposition -> Quality Record / Certificate
```

**Licensing dimensions:** plant or site; inspection scope, such as inspection points or plans; users; modules; integration scope; support tier. Figures per 6.3.

**Control boundary:** **QES carries quality execution and approval authority by design** - it holds and releases material according to its own design. Truthful, and different from PPIQ's.

---

## 6.2.7 Product 4 - Yard and Warehouse Management

**The existing `yardWarehouse.ts` is a useful starting point and is not discarded.** Its current definition is primarily read-only material-flow visibility; it expands into the management product, **provided the management functions are genuinely part of the target design**.

**The problem.** Material is somewhere in a yard or a warehouse and nobody is certain where; searching consumes handling hours; stock ages and is forgotten; yard utilisation is guessed; shipping readiness is discovered at the last moment; inventory accuracy drifts.

**Capabilities:** yard map; warehouse map; material location; bay, rack and bin visibility; inbound; outbound; put-away; movement requests; stock transfer; material reservation; staging; loading and unloading; dwell time; aging; FIFO and FEFO; occupancy; capacity; material genealogy; batch, lot and serial tracking; truck and loading workflow; inventory reconciliation; search; movement history; stock status; handling constraints; quality-status integration; MES and ERP integration; dashboards and KPIs.

**Business benefits:** find material immediately; reduce handling and search time; reduce aging and forgotten stock; improve yard and warehouse utilisation; improve shipping readiness; improve inventory accuracy; maintain full material history.

**The interactive workflow graphic:**

```
Inbound -> Yard -> Storage assignment -> Internal movement -> Production staging
        -> Warehouse -> Order allocation -> Loading -> Outbound
```

> **The Golden Rule for this page.** **Do not claim crane, AGV or PLC control unless that function is genuinely part of this product's design.** If the target system issues **human-approved movement requests**, the page says exactly that, in those words. A false control claim here would be discovered in the first technical review and would cost the credibility of all five products.

**Licensing dimensions:** site; yard and warehouse scope such as locations or bays; users; modules; integration scope; support tier.

---

## 6.2.8 Product 5 - Energy Management System

**A full independent product page.** The current homepage contains some energy-intelligence language; that is not an Energy Management product.

**The problem.** Energy is a large controllable cost that is measured at the meter and managed at the invoice. Consumption per line, per machine, per tonne and per batch is unknown; abnormal consumption is invisible until the bill arrives; peak demand charges are incurred without warning; and nobody can say which change actually saved anything.

**Capabilities:** electricity, gas, steam, compressed air, water and other utilities; meter hierarchy; plant, line and machine consumption; production-normalised consumption; specific energy consumption; baselines; peak demand; demand monitoring; tariff periods; energy cost; energy balance; anomalies; targets; budget against actual; energy KPIs; energy-to-production correlation; energy-loss identification; alerts; reports; sustainability and emissions information where supported; **ISO 50001-supporting workflows where appropriate, without claiming certification**.

**Business benefits:** reduce energy cost; detect abnormal consumption; understand energy per tonne, unit or batch; identify inefficient lines and equipment; reduce peak demand; connect consumption to production conditions; quantify savings; improve management visibility.

**The interactive energy-flow graphic**, animated rather than text cards:

```
Utility Sources -> Meter Hierarchy -> Plant / Area / Line / Machine -> Production Context
    -> Baseline -> Actual Consumption -> Deviation / Cost -> Action / Verification
```

**Licensing dimensions:** site; meters or data points; modules; analytics depth; deployment. Figures per 6.3.

**Two honesty obligations:** **supporting ISO 50001 workflows is not the same as being certified**, and the page must not imply certification; and any control claim - load shedding, demand response - appears only if it is genuinely in the design, with its boundary stated.

---

## 6.2.9 The Golden Rule product-page contract

**Every one of the five products satisfies the same twenty-section contract. A product page missing any section is incomplete.** All five use the same structure without becoming visually identical - the graphics, the colour emphasis within the palette and the workflow diagrams differ per product.

| # | Section | Requirement |
|---|---|---|
| 1 | Product identity | Name, category, one-line value proposition, product mark |
| 2 | Industrial problem | What the plant struggles with today, in the plant's language |
| 3 | Product solution | How this product addresses it |
| 4 | Main capabilities | Grouped, not a flat list of twenty |
| 5 | Business benefits | Outcome-shaped, not feature-shaped |
| 6 | Who uses it | Personas, matching the buyer table pattern of Chapter 1.4 |
| 7 | Typical workflow | The operational sequence |
| 8 | **Interactive graphical explanation** | Built on the shared scroll-draw primitive |
| 9 | Screenshots or honest illustrative graphics | Real where available; **clearly illustrative diagrams where not - never a fake screenshot** |
| 10 | Integration architecture | What it connects to, and how |
| 11 | Security and trust | Including its own control boundary per 6.2.17 |
| 12 | Deployment options | Cloud, on-premise, air-gapped where applicable |
| 13 | Roles and personas | Who does what in the product |
| 14 | KPI and value examples | **Illustrative and labelled as such** |
| 15 | Licence and package structure | Dimensions, and Contact Sales where figures are unapproved |
| 16 | CTA | Request demo, carrying product context |
| 17 | FAQ | Where it removes a real objection |
| 18 | Cross-product integrations | How it works with the other four |
| 19 | Mobile and responsive experience | Verified at mobile viewport |
| 20 | Evidence and honesty statement | What is implemented, what is design, what is planned |

**Prohibited:** a thin page of a headline and six feature cards. Each product must read like a serious enterprise platform worth discussing in a high-value procurement.

---

## 6.2.10 The honesty rule

| Product | Claim basis |
|---|---|
| **PPIQ** | Chapters 1 to 5 exist. **Every claim is directly traceable to the Master Design**, via Chapter 1.11 |
| **MES, QES, Yard and Warehouse, Energy** | No equivalent Master Design chapter exists yet. **A capability is not claimed as implemented merely because it belongs naturally in that software category** |

**Every capability of every product carries an internal status:** `Implemented` | `Target Product Design` | `Planned` | excluded from public claims.

**The status labels need not be displayed publicly**, but the content source of truth must know which is which, and **the public wording must differ**: an implemented capability is described in the present tense; a target-design capability is described as what the product does by design; a planned capability is either marked as planned or omitted. The honesty language lint of 6.1.7 gate 11 runs over the website content.

**Absolutely prohibited on any page:** a fake customer result, a fake ROI figure, a fake certification, a fake integration, or a claim of autonomous control.

---

## 6.2.11 Preserving and raising the visual language

**Preserve:** the dark premium industrial theme; the cyan, blue and green language; technical diagrams; animated flows; the premium enterprise feel; SOU branding; the typography hierarchy; the hero composition; graphics rather than excessive stock photography; the responsive header; the evidence and trust presentation.

**Raise the remaining 25 percent with:** richer diagrams; process animations; real product screenshots; considered hover states; micro-interactions; page transitions where they aid orientation; a product comparison; the ecosystem graphic; interactive workflows; visual KPI stories; and industrial photography **only where it adds credibility**.

> **Avoid turning the website into a generic SaaS template.** The current identity is the asset. Every addition must look as though it was always part of it.

---

## 6.2.12 Routes

**Canonical, each rendering its own full product page:**

`/products/plantprocess-iq` | `/products/mes` | `/products/qes` | `/products/yard-warehouse-management` | `/products/energy-management`

**Portfolio:** `/products`

**Compatibility redirects, permanent, preserving inbound links and search equity:** `/product` -> `/products/plantprocess-iq`; `/products/yard` -> `/products/yard-warehouse-management`; `/products/energy` -> `/products/energy-management`; any other legacy product URL to its canonical target.

**`LegacyProductRoute` is removed.** `/packs/:code` remains as a PPIQ concept and is reachable from the PPIQ page, not from the Products menu.

---

## 6.2.13 The expanded product page model

**Keep `ProductPageModel` as the shared contract. Expand it from seven fields to the twenty sections of 6.2.9**, so that every page is structurally complete by construction and the completeness test asserts on the model rather than on rendered text.

```
ProductPageModel {
  id, slug, name, category, productMark
  hero            { headline, subTagline, visual, primaryCta, secondaryCta }
  problem         { title, body, symptoms[] }
  solution        { title, body }
  capabilities    [ { group, title, body, status } ]        // status per 6.2.10
  benefits        [ { metricLabel, body, basis } ]          // basis: illustrative | traceable
  personas        [ { role, need, whatTheyDo } ]
  workflow        { caption, steps[], note }
  interactiveDiagram { kind, nodes[], edges[], hoverContent[] }
  screenshots     [ { src, caption, isIllustrative } ]      // isIllustrative is mandatory
  kpiExamples     [ { label, value, isIllustrative } ]
  integrations    [ { system, direction, status } ]
  security        { posture, controlBoundary, deployment[] }
  deployment      [ { mode, availability } ]
  licensing       { model, dimensions[], tiers[], note }
  relatedProducts [ slug ]
  cta             { heading, body, buttonLabel, productContext }
  faq             [ { question, answer } ]
  seo             { title, description, canonical, ogImage }
  evidencePosture { statement, claimStatusSummary }
}
```

**`isIllustrative` and `status` are mandatory fields, not optional ones**, because the honesty rule of 6.2.10 must be enforceable by a test rather than by editorial care.

---

## 6.2.14 The product registry

The registry currently holds full models for MES and Yard and Warehouse only. **Create complete entries for all five.** The registry then becomes the single source for: the Products mega-menu; the `/products` portfolio page; the five product routes; related-product cards; the sitemap; SEO generation; product comparison; and the E2E tests.

**One registry, one truth.** A product added to the registry appears everywhere automatically; a product not in the registry does not exist on the site. This is the same principle as the product's own registry-driven authoring (Chapter 1, Rule 1) applied to the website.

---

## 6.2.15 The portfolio page

`/products` is a **major sales page, not a directory.**

For each of the five: what problem it owns; the typical buyer; where it sits operationally; primary benefits; **what it does independently**; and its relationship with the other four.

Plus an interactive **SOU Industrial Software Stack** graphic:

```
   PLANT INTELLIGENCE      PlantProcess IQ
   PLANT EXECUTION         MES  +  QES
   MATERIAL FLOW           Yard & Warehouse Management
   RESOURCE EFFICIENCY     Energy Management System
```

> **This is a commercial visualisation of where each product sits operationally. It is not a hard architectural dependency** unless the products genuinely integrate that way, and where they do the integration is stated per pair with its status.

Also on this page: a **product comparison** answering "which of these do I need?" by problem rather than by feature checklist.

---

## 6.2.16 Pricing

**Keep the current pricing work; change its architecture.** One PPIQ Light / Pro / Pro Plus / Enterprise matrix cannot describe five independent products.

The page opens with a **product selector**: `PPIQ | MES | QES | Yard & Warehouse | Energy`. Selecting one shows that product's licence model - its dimensions, its packages and what each includes.

**Where a figure is not approved, `Contact Sales` or `Configure your deployment` is correct and inventing a number is not.** The commercial calculator of 6.3 remains the authoritative pricing logic; the website never becomes a second pricing source.

---

## 6.2.17 Security

**Keep and enhance the current Security page**, with an explicit split.

**Company-wide security posture** - identity, encryption, deployment options, audit, secure development lifecycle, support - drawn from Chapter 3 4.6.7.

**Product-specific control boundary**, and this is the part that must not be copied between products:

| Product | Truthful control boundary |
|---|---|
| **PPIQ** | **Read-only toward customer systems. No control write, ever.** Outbound is message, export or webhook only |
| **MES** | **Execution authority according to the MES design**: dispatch, confirm, record |
| **QES** | **Quality execution and approval authority according to the QES design**: hold, release, disposition |
| **Yard and Warehouse** | **Movement authority according to the Yard design** - and if the design issues only human-approved movement requests, that is what is stated |
| **Energy** | **Monitoring, and control only within the Energy design's stated boundary** |

> **Do not copy PPIQ's read-only Golden Rule onto products whose legitimate function includes execution.** Doing so would be both false and commercially damaging: an MES that claims it cannot execute is not an MES.

---

## 6.2.18 Graphics

The custom SVG and React graphics are the right direction and should be expanded significantly. **Each product needs at least four visuals:** one premium hero visual; one interactive workflow diagram; one architecture and integration visual; one value and KPI visual. Plus real product screenshots where they exist.

**Preference order:** actual application screenshots; purpose-built product diagrams; purpose-built industrial illustrations; and generic stock photography only where it genuinely adds credibility.

> **Where a screenshot does not exist yet, use a clearly illustrative diagram rather than a mock-up that implies an implemented interface.** The `isIllustrative` flag of 6.2.13 makes this checkable.

---

## 6.2.19 Website tests

**Remove** the assertion whose intent is *sibling MES and QES product navigation must not exist*. **It now protects the wrong product architecture.** Also remove the `path="/products/:code"` route assertion, which pins the legacy redirect.

**Replace with tests proving:**

| # | Assertion |
|---|---|
| 1 | All five products exist in the registry |
| 2 | All five canonical routes render |
| 3 | The Products menu exposes all five plus View all |
| 4 | Each product page contains **all twenty Golden Rule sections** |
| 5 | Each product has a CTA carrying its product context |
| 6 | Each product has licensing information |
| 7 | Each product has an interactive graphic |
| 8 | **No unsupported claim**: the honesty lint passes over all website content |
| 9 | No dead links, internal or external |
| 10 | Responsive layout at desktop, tablet and mobile |
| 11 | Keyboard navigation of the whole site including the mega-menu |
| 12 | Accessible menu: roles, focus management, Escape closes |
| 13 | **No horizontal overflow at any breakpoint** |
| 14 | Exactly one H1 per page |
| 15 | Valid metadata and canonical URL per page |
| 16 | Lead capture carries product context through to submission |
| 17 | Legacy redirects resolve to canonical targets |
| 18 | Every `isIllustrative: false` screenshot resolves to a real asset |

**Run the browser suite for all five products at desktop and mobile viewports.**

---

## 6.2.20 What must not be removed

**Before deleting or replacing any substantial component, the question is: does it conflict with the new company and product architecture, or does it simply need to be reused in the correct place?**

Likely to be retained and reused rather than rebuilt: the premium header, SOU brand, `RequestDemoForm`, `HeroTopology`, `GoldenThread`, `TrustEngine`, `SignalVsNoise`, `ArchitectureFlowScroll`, `ProductScreenshotShowcase`, the ROI calculator, the security content, the proof storytelling, the responsive foundations, the accessibility foundation, the colour system and the motion system.

**Refactor the reusable pieces into shared primitives rather than duplicating them into five disconnected mini-sites.** `useScrollDraw`, the screenshot frame, the diagram shell and the section layouts are shared; only the content and the node sets differ per product.

---

## 6.2.21 Acceptance

The website section is complete when all fifteen hold:

1. The existing visual concept is preserved and polished, not discarded. 2. The homepage represents SOU as a software company with five products. 3. PPIQ is the flagship, not the parent of the others. 4. MES has a complete standalone page. 5. QES has a complete standalone page. 6. Yard and Warehouse Management has a complete standalone page. 7. Energy Management has a complete standalone page. 8. `/products` is a premium portfolio experience. 9. Every product satisfies the twenty-section Golden Rule. 10. Every product includes benefits, workflow, graphical explanation, licensing and CTA. 11. Product claims remain technically honest, with PPIQ's traceable to Chapters 1 to 5. 12. The proof, security, lead-capture and ROI strengths are preserved. 13. Website validation is updated to the five-product truth. 14. Desktop and mobile E2E tests pass for every major page. 15. **No part of the website looks like a generic unfinished SaaS template.**

---

# 6.3 ADMINISTRATION, LICENSING AND SALES

> **Target audience:** the commercial administrator, the sales engineer, and the engineering lead receiving a signed customer.
>
> **The five concepts this section must never confuse:**

| # | Concept | Question it answers | Enforced by |
|---|---|---|---|
| 1 | **User authorization** | May *this person* do this? | Role and permission, Chapter 3 4.8.2 |
| 2 | **Licence entitlement** | Does *this installation* include this capability? | The signed token, Chapter 3 4.6.1 class C4 |
| 3 | **Capacity guardrail** | Is this installation within what it bought? | The five metered dimensions, Chapter 1.7.1 |
| 4 | **Commercial pricing** | What does the customer pay? | 6.3.6 |
| 5 | **Infrastructure cost** | What does it cost us to run? | 6.1.9 and 6.3.5 |

**They are related and they are not the same.** A capability may be entitled but not permitted for a role; a role may be permitted but the installation over its capacity; and price is a commercial decision informed by cost, not equal to it.

## 6.3.1 The licence and feature matrix

**The authoritative matrix.** For every capability: minimum tier, any additional role requirement, whether it is hidden or visible-but-locked, its metered dimension, and its infrastructure implication.

| Capability | Min tier | Role also required | Below tier | Metered by | Infrastructure implication |
|---|---|---|---|---|---|
| Data connections, file and PostgreSQL | Light | Data Engineer to author | visible-locked | ingest rate, retained volume | collector, staging storage |
| Data connections, all database classes | Enterprise | Data Engineer | visible-locked | as above | connector runtime |
| Dashboards and workspace, read | Light | any authenticated | - | concurrent sessions | API and database query load `D2` |
| Page and widget authoring | Light | Engineer + quota | visible-locked | authoring quota | definition storage, query load |
| Master items, bookmarks, saved views | Light | Engineer | visible-locked | authoring quota | negligible |
| **SQL authoring** | **Pro** | **Engineer, never Viewer** | **visible-locked** | authoring quota | query load, safe-SQL validation |
| Statistics and correlation | Pro | Engineer | visible-locked | compute slots | `analysis` pool, `D3` |
| Practice learning | Pro Plus | Engineer | visible-locked | compute slots | `analysis` pool, feature store |
| Machine learning: features, training | Pro Plus | Data Engineer | visible-locked | compute slots | `ml` pool, model memory, snapshots |
| Prediction and scoring | Pro Plus | Engineer to act | visible-locked | compute slots, refresh interval | `ml` pool, latency budget |
| Remediation and the safety gate | Pro Plus | Engineer to decide | visible-locked | compute slots | included above |
| Value engine | Pro Plus | Engineer; Administrator for assumptions | visible-locked | - | negligible |
| **Assistant** | **Pro Plus** | permission-scoped retrieval | **hidden entirely** | assistant concurrency | model gateway, serving memory |
| Model serving, self-hosted | Pro Plus | - | hidden | serving resources | `ppiq-serving` node |
| Model serving, customer's own model | Enterprise | Administrator | hidden | - | gateway configuration |
| Reports and scheduled delivery | Pro | Engineer | visible-locked | compute slots | `report` pool |
| Alert routing and escalation | Pro | Administrator | visible-locked | - | negligible |
| Log retention beyond default | Pro Plus | Administrator | visible-locked | retained volume | storage |
| **SSO and provisioning** | **Enterprise** | Administrator | visible-locked | - | identity integration |
| On-premise deployment | Enterprise | - | - | - | customer infrastructure |
| **Air-gapped deployment** | **Enterprise** | - | - | - | offline release process, 6.1.2.7 |
| High availability and automatic failover | Enterprise | - | - | - | replica, fencing |
| Support and SLA tiers | per contract | - | - | - | operations effort |

**Hidden against visible-but-locked, and the rule for choosing:** a capability that a customer might reasonably buy is **visible-but-locked**, with what it does and how to obtain it - this is honest and it sells. A capability whose mere presence would confuse, such as the assistant dock appearing as a broken button, is **hidden entirely** (Chapter 3 4.4 G1).

> **No feature gate exists only in the frontend.** Every gate is enforced **server-side at the endpoint** and reflected in the interface. Hiding a control without enforcing the endpoint is a security defect, not a packaging choice, and the contract tests of 6.1.5.4 assert both halves.

## 6.3.2 User and role splitting

### 6.3.2.1 The authorization equation

> **Allowed = Tenant scope AND Licence entitlement AND Role permission AND Object permission AND Current object and state rules**

**All five must hold. Failing any one refuses**, and each refuses with its own code so the user learns which: tenancy silently excludes (RLS); entitlement returns `QT02`; role returns `QT03`; object permission returns the object's own refusal; state rules return the state's code, such as `RM10` for a decision on a non-actionable candidate.

**The fifth term is why `can_accept` exists** (Chapter 3 4.5.12a): a user with the right tenant, tier, role and object may still be refused because the object's current state forbids the action. **State is part of authorization, not a separate concern.**

### 6.3.2.2 The role matrix

`R` read, `A` author, `X` act, `-` hidden.

| Surface group | Tenant Owner | Plant Admin | Data Engineer | Process Engineer | Operator | Viewer | Commercial Admin | Vendor Support |
|---|---|---|---|---|---|---|---|---|
| A1 A2 Enter | R | R | R | R | R | R | R | R |
| B1-B6 Connect and import | R | R+A+X | R+A+X | R | - | - | - | R |
| C1 Transformation Studio | R | R+A+**publish** | R+A | R | - | - | - | R |
| C2-C4 Health, quality, model | R | R+X | R+X | R | - | - | - | R |
| C5 Genealogy | R | R | R | R | R | R | - | R |
| C6 Relationship Browser | R | R+X | R+X | R | - | - | - | R |
| D1 Workspace | R | R | R | R | R | R | - | R |
| D2 Page Builder | R | R+A | R+A | R+A | - | - | - | - |
| D3 Analysis Toolbox | R | R+A+X | R+A+X | R+A+X | - | - | - | - |
| D4-D8 Findings, risk, suggestions, value, ML | R | R+X | R | R+X | R (D5 only) | R (D4 only) | R (D7 only) | R |
| **D9 Early Warning** | R | R+X | R | **R+X** | R | - | - | R |
| D10-D12 Practice, scenario, benchmark | R | R | R | R+A | - | R (D10) | - | - |
| E2 Assistant config | R | R+A | - | - | - | - | - | R |
| E3 Plant Data Log | R | R+A+X | R+A | R+A+X | R+X ack | R | - | R |
| E4 Supervisor | R | R+**approve** | R | R | - | - | - | R |
| E5 Reports | R | R+A+X | R+A | R+A+X | R | R | R | - |
| E6 Alert routing | R | R+A+X | - | - | - | - | - | R |
| F1 Users and roles | **R+A+X** | R+A+X | - | - | - | - | - | R |
| F2 Licence | **R+X** | R | - | - | - | - | **R+X** | R |
| F3 Quota | R+A+X | R+A+X | - | - | - | - | - | R |
| F4 Jobs admin | R+A+X | **R+A+X (create, pool, weight, target)** | R+A (schedule, dependencies, enable) | - | - | - | - | R |
| F5 Logging and audit | R | R | R (job, data) | R (job, data) | R (system, job) | - | - | R |
| F6 Log channels | R+A | R+A | - | - | - | - | - | R |
| **F9 Log retention** | R+X | **R+X** | - | - | - | - | - | **R only** |
| F7 Settings, F8 Translation | R+A | R+A | - | - | - | - | - | R |

**Five standing rules.** A **Viewer never authors SQL at any tier**. A **hidden page is never reachable by URL** - the endpoint refuses independently. **Vendor Support is time-boxed, scoped and fully audited**, and can read almost everything but change almost nothing; it can never delete log history. **Commercial Admin sees licence and value, and no plant data.** And **publishing, approving and retention are separated from authoring** because each is a governed act.

### 6.3.2.3 Enforcement layers

| Layer | Enforces | Failure mode it prevents |
|---|---|---|
| **Database RLS, forced** | Tenant scope | Cross-tenant read by any path, including a bug in the application |
| **Endpoint filter** | Entitlement and role, before the handler | Direct API call to a capability the interface hid |
| **Handler state check** | Object and state rules | Acting on an object whose state forbids it |
| **Interface** | Visibility and affordance | Offering a control that would be refused |
| **Contract test** | That all four agree | Drift between what is hidden and what is enforced |

> **A user must never gain a capability by navigating directly to an endpoint the interface hid.** The interface is the last layer, never the only one.

## 6.3.3 Licence enforcement architecture

| Element | Design |
|---|---|
| **Format** | A signed token: tenant identity, site binding where applicable, tier, capability set, **capacity envelope in the five metered dimensions**, issue and expiry, licence identifier, issuer, signature |
| **Signing** | Private key in a hardware or managed key store, credential class C4. **The private key never leaves it** |
| **Verification** | **Offline, against an embedded public key.** No call home is required, which is what makes air-gapped operation possible |
| **Tenant binding** | The token names the tenant; a token applied to another tenant is refused |
| **Site binding** | Where the contract is per site, the token names the site identity |
| **Validity** | Explicit start and expiry, with the customer's own time zone recorded |
| **Grace period** | Configurable after expiry; capability continues, with escalating warning |
| **Expiry** | After grace: **read-only access to what the customer built. Customer data is never destroyed by expiry** |
| **Renewal** | A new token supersedes; the previous remains in the audit |
| **Upgrade** | New token, effective immediately; newly entitled capability becomes available without redeployment |
| **Downgrade** | New token. **Capability is withdrawn, data is not.** Content created under a higher tier becomes read-only rather than deleted, and the interface says why |
| **Revocation** | A revocation list checked where connectivity exists; for air-gapped, revocation is contractual and enforced at the next licence file |
| **Air-gapped update** | A licence file transferred on media, verified offline, applied through F2, audited |

> **The browser is never the entitlement authority.** A client-supplied tier is ignored by design (Chapter 1.7). Entitlement derives only from the verified token, evaluated server-side.

## 6.3.4 The commercial capacity model

### 6.3.4.0 The six commercial dimensions, and how they reconcile with the metering law

**The licence is a function of six dimensions**, because these are the six a customer understands, can self-assess before buying, and can be held to afterwards:

| # | Dimension | Why it is in the licence |
|---|---|---|
| 1 | **Number of users** | The customer knows how many people will use it |
| 2 | **Number of pages** | Visible, countable, and a proxy for analytical breadth |
| 3 | **Number of jobs** | Visible, and the customer's own scheduling decision |
| 4 | **Number of DB-links** | The customer knows how many systems they want joined |
| 5 | **Amount of data transferred** | Retained volume and ingest rate |
| 6 | **Special features - AI, ML and the assistant, from the third tier** | The capability step change |

**How this reconciles with Chapter 1.7.1.** That law says entitlement is *metered* on capacity consumed rather than on object counts, and it was written after a worked example showed that three DB-links can mean 146 GB. **Both statements are true and they operate at different layers:**

| Layer | Uses | Purpose |
|---|---|---|
| **Commercial packaging - what the customer buys** | **All six dimensions of this section** | Legible, quotable, self-assessable |
| **Technical protection - what the platform enforces** | The five metered dimensions of Chapter 1.7.1: retained volume, ingest rate, minimum refresh interval, weighted compute slots, concurrent sessions | Protects the machine from a workload the counts did not predict |

> **The reconciliation rule: a tier bounds all six commercial dimensions AND its capacity envelope together, and the two are calibrated against each other.** Selling "3 DB-links" without also stating the volume and cadence those links may carry is exactly the mistake the metering law was written to prevent. **Counting alone under-prices; metering alone is unquotable. Every tier does both.**

**And the design obligation that makes generous counts affordable:** the delta-propagation law of Chapter 4 5.3.9. **The customer is not charged for the vendor's scan strategy.** Three DB-links carrying 146 GB across 300 tables, with ten jobs at three-minute cadence, is a Pro-tier workload on a two-host deployment - **because no job class rescans the model.** On a full-scan design the same workload would require 5.7 GB/s of sustained scan and would be unsellable at any tier.

### 6.3.4a The tier envelopes

**All figures are derived from the workload model of 6.1.9 and are calibrated so that a customer at the top of a tier fits the server specification of 6.1.9.8 for that tier.** Illustrative for the commercial framework of 6.3.6; the counts and capacities themselves are the operative envelope.

| Dimension | **Light** | **Pro** | **Pro Plus** | **Enterprise** |
|---|---|---|---|---|
| **1. Named users** | 5 | 25 | 100 | unlimited |
| Concurrent sessions *(metered)* | 3 | 15 | 50 | 200+ |
| **2. Pages** | 15 | 100 | 400 | unlimited |
| Widgets per page | 12 | 20 | 30 | 30 |
| **3. Jobs** | 5 | 25 | 100 | unlimited |
| Minimum refresh interval *(metered)* | **60 min** | **3 min** | **1 min** | **15 s** |
| Weighted compute slots *(metered)* | 2 | 8 | 16 | 24+ |
| **4. DB-links** | 1 | **3** | 10 | unlimited |
| Source objects per link | 25 | **150** | 500 | unlimited |
| **5. Retained volume** *(metered)* | 250 GB | **1 TB** | 5 TB | 20 TB+ |
| Ingest rate *(metered)* | 10 rows/s | **100 rows/s** | 1,000 rows/s | above |
| Source footprint it comfortably serves | ~30 GB | **~150 GB** | ~1 TB | above |
| **6. Statistics and correlation** | - | **yes** | yes | yes |
| **6. SQL authoring** | - | yes | yes | yes |
| **6. Machine learning** | - | - | **yes** | yes |
| **6. Practice learning** | - | - | **yes** | yes |
| **6. Prediction and remediation** | - | - | **yes** | yes |
| **6. Value engine** | - | - | **yes** | yes |
| **6. Assistant / chatbot** | - | - | **yes** | yes |
| Connectors | files, PostgreSQL | + MySQL, SQL Server | + Oracle, historian | **all** |
| Deployment | hosted | hosted, customer cloud | + on-premise | **+ air-gapped** |
| SSO | - | - | - | **yes** |
| High availability | - | - | optional | **yes** |
| **Server specification** | **6.1.9.8 Tier 1** | **6.1.9.8 Tier 2** | **6.1.9.8 Tier 3** | **6.1.9.8 Tier 4** |

**Pro is calibrated directly against the worked scenario**: 3 DB-links, 150 objects each, 146 GB source footprint, 10 jobs at a 3-minute floor, sitting inside a 1 TB retained envelope on a 4-core application host and an 8-core database host. **That is the design target, not a coincidence.**

### 6.3.4b How the two sets interact at run time

| Situation | Behaviour |
|---|---|
| Object count approached (80%) | **Soft guardrail**: warn on the authoring surface, offer the upgrade path. **Never block work in progress** |
| Object count reached (100%) | Create action disabled with the reason and the administrator named. **Existing work is untouched** |
| Metered dimension approached | Warn on F2 with the measured value and the envelope |
| **Metered dimension exceeded** | **Throttle, never destroy**: an import queues, a job waits for a slot, the cadence floor is enforced. The reason is stated and the upgrade path offered |
| Counts inside, meters exceeded | **The meters govern.** The customer has fewer objects than allowed but is moving more data than bought - the honest conversation is a capacity upgrade, not a count upgrade |
| Meters inside, counts exceeded | **The counts govern the commercial conversation** and the guardrail applies; nothing is throttled, because the machine is not under pressure |

**Both directions are visible to the customer on F2**, showing counts against limits **and** meters against the envelope, so the upgrade conversation starts from measured facts rather than from a surprise.

### 6.3.4c What is deliberately not restricted

| Not limited | Reason |
|---|---|
| Master dimensions, measures and filters | These are the reuse mechanism. Limiting them pushes users to copy-paste, which costs *more* |
| Relationships | The plant model must be complete to be correct. **Charging for accuracy would be perverse** |
| Bookmarks and saved views | Free. They cost nothing and they drive daily use |
| Definition versions | Versioning is a safety mechanism. Charging for it discourages it |
| Genealogy depth or breadth | It is the product's reason to exist |
| Evidence and drill-through | Never limited, at any tier |

> **Chapter 1.2.4 sells authoring freedom as the product.** Limiting the six dimensions is legitimate commercial packaging. **Limiting the mechanisms that make authoring safe and reusable would sell the customer a worse product and cost the platform more to run.**

## 6.3.5 The infrastructure cost formula

**One workload model. 6.1.9 and this section are the same calculation.**

```
CUSTOMER WORKLOAD (6.1.9.2 inputs)
        |
        v
DRIVERS D1..D5 (6.1.9.3)  ------------------------------+
        |                                               |
        v                                               v
RESOURCE DEMAND (6.1.9.4)                        CAPACITY ENVELOPE
        |                                        (the five dimensions of 6.3.4)
        v                                               |
DEPLOYMENT CLASS (6.1.9.5)                              |
        |                                               |
        v                                               |
INFRASTRUCTURE MONTHLY COST                             |
   = compute + storage + backup + network               |
   + object storage + model serving                     |
        |                                               |
        v                                               |
OPERATIONS COST                                         |
   = monitoring + on-call share + backup verification   |
   + support tier effort                                |
        |                                               |
        v                                               v
TARGET GROSS MARGIN  -------------->  RECOMMENDED COMMERCIAL PACKAGE
```

| Term | Basis |
|---|---|
| Compute | Node shapes from 6.1.9.4 at the price book rate for the deployment region |
| Storage | `stor_db + stor_stg + stor_obj` at the storage rate, plus IOPS class. **Sized on retained volume, not on the customer's source footprint** - the product stores the plant model and its history, not a copy of every source table (Chapter 4 5.3.9.4) |
| Backup | `stor_bak` at the backup rate, plus egress for off-site copies |
| Network | `bw_in` and `bw_out` at the transfer rate |
| Model serving | Only where the tier includes the assistant; a serving node or the private-endpoint rate |
| Operations | A share of monitoring, on-call and the monthly restore verification, allocated per installation by class |
| Support | The contracted tier's expected effort |

> **The same customer must produce the same infrastructure recommendation whether Engineering or Sales runs it. One formula. One source of truth.** The calculator of 6.3.8 is the only implementation, its constants are versioned, and every quotation records which constant version produced it.

**Where the deployment is on-premise or customer-cloud**, the infrastructure cost is the **customer's**, and the calculation still runs - it produces the sizing the customer must provision, which is what the handover of 6.3.10 carries.

## 6.3.6 The licence price function

**A framework, not a price list.** Final figures are a commercial decision; this defines how they are constructed so that two quotations for similar customers are consistent.

| Component | Basis | Recurrence |
|---|---|---|
| **Initial implementation and onboarding** | Deployment mode, connector count, data-model complexity, training | One-off |
| **Software subscription** | Tier value plus capacity consumed | Monthly or annual |
| **Infrastructure and hosting** | 6.3.5, where vendor-hosted | Monthly, passed through or bundled |
| **Enterprise features** | SSO, air-gap, HA, customer model serving | Monthly |
| **Support and SLA** | Response and availability commitments | Monthly |
| **Professional services** | Optional, at day rate | As used |

**The subscription is a function of all six commercial dimensions, and the tier is derived rather than chosen.**

```
STEP 1 - TIER SELECTION from the six commercial dimensions
  required_tier = min over tiers T such that ALL of:
        users_required      <= T.users
        pages_required      <= T.pages
        jobs_required       <= T.jobs
        db_links_required   <= T.db_links
        refresh_floor_req_s >= T.min_refresh_interval_s
        required_features   subset of T.features        (ML, practice, prediction,
                                                         remediation, value, assistant
                                                         from tier 3; SSO, air-gap,
                                                         HA from tier 4)
  -> the LOWEST tier satisfying every one. One dimension over promotes the tier,
     which is the same worst-dimension rule the sizing model uses (6.1.9.5).

STEP 2 - CAPACITY COMPONENT from the metered dimensions
  capacity_component =
        f_volume ( retained_bytes            over T.included_retained_bytes )
      + f_ingest ( ingest_bytes_ps           over T.included_ingest_bytes_ps )
      + f_slots  ( weighted_slots            over T.included_slots )
      + f_session( concurrent_sessions       over T.included_sessions )
      + f_refresh( refresh_floor_s           below T.min_refresh_interval_s )
  Each f is zero inside the tier's included envelope and rises in declared bands
  beyond it. A customer inside the envelope pays tier_base only.

STEP 3 - SUBSCRIPTION
  subscription = tier_base( required_tier )
               + capacity_component
               + enterprise_options
               + support_tier

STEP 4 - THE MARGIN FLOOR, for vendor-hosted deployments
  subscription >= infrastructure_cost( 6.3.5 ) / ( 1 - target_margin )
  A configuration failing this is REFUSED by the calculator with the shortfall named.
```

**Why the tier is derived and not selected.** If Sales could choose a tier independently of the six dimensions, the tier table and the quotation would drift apart within a quarter. **Deriving it makes the commercial logic deterministic**: the same six inputs always produce the same tier, the same capacity component and the same hardware specification (6.1.9.8), whether Engineering or Sales runs the calculator.

**The relationship to hardware, stated once.** The **tier** determines the hardware specification the customer needs (6.1.9.8). The **capacity component** prices consumption within or beyond that tier's envelope. **The counts never enter the sizing formula** (Chapter 1.6.3); they enter only tier selection.

**The floor rule matters:** the subscription may never be priced below what the infrastructure costs at the target margin. **A deal that is commercially attractive and structurally loss-making is refused by the calculator**, with the shortfall named, rather than discovered a year later.

**Simple enough to quote.** Sales chooses a tier and a capacity band; the calculator produces the figure and the assumption list. The four-tier ladder plus a capacity band is two decisions, not twenty.

> **Worked examples use synthetic assumptions only and are labelled illustrative. No example figure in this document is an approved commercial price.**

## 6.3.7 The value-to-price sales model

**The conversation, per Chapter 1.9:**

> Estimated platform cost, against **customer-measured** potential or recovered value.

Sales may use, as **categories of value with the customer's own numbers**: quality loss reduction, downtime reduction, productivity improvement, yield improvement, avoided failure.

| Permitted | Prohibited |
|---|---|
| The value **model**, with the customer's own cost inputs | A saving figure computed on emulated data presented as theirs |
| A **bounded range** with every input traceable | A single confident number |
| "Insufficient basis" where an input is missing | A default the vendor invented |
| A pilot with a defined measurement to produce their number | **Converting an unvalidated demonstration result into a promised saving** |
| "It recovered a planted validation signal and rejected a null control. That validates the method." | Any stronger statement about a planted-signal result |

**The three-move sequence of Chapter 1.9 is the approved script**, and the language contract of Chapter 1.5 applies to every word of it.

## 6.3.8 The Sales Administration tool

**Yes, it should exist, and it is internal.**

| Inputs | Outputs |
|---|---|
| Selected licence tier | Recommended infrastructure and deployment class |
| Required Enterprise capabilities | Estimated infrastructure monthly cost |
| Deployment mode | Licence and package recommendation |
| Data volume and retention | **Capacity envelope** in the five dimensions |
| Ingest cadence and minimum refresh | Commercial assumptions, listed |
| Concurrency | Indicative price range |
| Job workload by family | **Gross-margin view, authorised internal users only** |
| Support level | The quotation artifact, versioned |

| Property | Rule |
|---|---|
| **Audience** | **Internal only.** Commercial Admin and authorised sales. **Never exposed to a plant user or to the public website** |
| Determinism | Same inputs, same outputs, with the constant version recorded |
| Traceability | Every output shows its formula and inputs |
| Margin visibility | Only to authorised internal users; never in a customer-facing artifact |
| Versioning | Every quotation is a versioned record; a re-quote creates a new version and both remain |
| Authority | It is the **only** pricing implementation. The website links to Contact Sales rather than reproducing its logic |

## 6.3.9 Commercial audit

**A commercial or licence override must never happen silently.** Every one of these writes an immutable audit entry naming actor, timestamp, before, after and justification:

licence activation; licence renewal; tier upgrade; **tier downgrade**; entitlement change; **capacity envelope change**; a pricing calculation that produced a quotation; a quotation version; a commercial assumption change; **an exceptional or manual override, which additionally requires a second approver and a recorded reason**; and a support entitlement change.

These entries live in the audit family (Chapter 3 4.5.15), which is append-only with a governed retention minimum, and they are readable on F5 by the Administrator and the auditor. **A downgrade and an override are the two most consequential and are the two most often done informally**, which is precisely why both are named here.

## 6.3.10 The sales-to-engineering handover

**Produced when a customer signs. Engineering must never receive a signed contract containing a promise that cannot be traced to a technical capability or an infrastructure requirement.**

| Section | Contents |
|---|---|
| **Identity** | Customer, site or sites, contacts, contract reference |
| **Deployment** | Topology T1 to T4, region, who operates what |
| **Licence** | Tier, capability set, Enterprise options selected |
| **Connectors** | Which source classes, with their availability status per Chapter 3 |
| **Data sources** | Estimated count, kinds, and where each lives |
| **Ingest** | Estimated rows and bytes per day, peak, minimum refresh interval |
| **Retention** | Canonical, staging, results and logs, with the audit minimum |
| **Users** | Named and concurrent estimates, roles required, SSO requirement |
| **Job workload** | Expected count by family, cadence, weight |
| **Sizing calculation** | The full 6.1.9 output, **including the constant version and the assumption list** |
| **Infrastructure** | Required nodes, storage, network, pool sizes, worker counts |
| **Backup and DR** | RPO, RTO, HA requirement, verification schedule |
| **Security** | Encryption, key custody, network constraints, egress policy |
| **SSO** | Provider, protocol, provisioning expectation |
| **No-egress and air-gap** | Whether required, and the operational consequences accepted |
| **Support and SLA** | Tier, response commitments, escalation contacts |
| **Commercial exclusions and assumptions** | Everything the price assumed, **so that an assumption that proves wrong is a change request rather than an argument** |
| **Traceability** | Every commercial promise mapped to its capability, per 6.4 |

**Acceptance of the handover is a two-signature act**: the commercial owner and the engineering lead. **An unsigned handover blocks provisioning**, because provisioning against an untraceable promise is how a project fails in month four.

---

## 6.3.11 Pilot data intake — transition, objectives and operational-cost-driver evidence

The customer-data request for a pilot must collect enough structure to prevent regime and objective ambiguity before analysis begins. In addition to source schemas, timestamps, references and evidence sources already required, request where available:

- the customer's definition of production/changeover/setup/cleaning/configuration/campaign transitions;
- start/end evidence for those transitions and the related equipment/operation/subject scope;
- the declared stabilisation rule: time, first-N subjects, or condition for steady-state;
- sequence/run/campaign identifiers and boundaries;
- setup/preparation duration and direct loss quantities if recorded;
- scrap/yield/quality during ramp-up and stable operation;
- registered objectives the customer wants optimised together, including hard constraints and preference policy if one exists;
- direct cost facts where available; otherwise cost assumptions remain explicitly outside the source truth and are owned by the Value Engine.

**Readiness rule.** Customer data is not admitted to steady-state statistical certification while transition/stabilisation semantics materially affect the process and remain undeclared. The Customer Data Capability Assessment marks this as a named gap rather than treating the rows as one homogeneous population.

**30-September programme checkpoint.** `T-087` is a hard programme checkpoint on **5 September 2026**. If it is not green, the customer demonstration uses the declared compatibility-read fallback for the already-proven kernels and labels canonical references/surfaces as configured rather than executed. No presenter may blur that distinction. This fallback protects truth; it does not waive the canonical chain.

# 6.4 CROSS-CHAPTER TRACEABILITY

## 6.4.1 The rule

**Every promise in Chapters 1 and 2 maps through to its technical owner.** A promise with no owner is marked **UNRESOLVED** and is never silently assumed to exist.

> Commercial promise -> Product capability -> Chapter 3 technical owner -> Chapter 4 engine or design -> Chapter 5 user journey where applicable -> Chapter 6 deployment, licence or commercial requirement where applicable.

## 6.4.2 The matrix

| Commercial promise (Ch1) | Capability | Ch3 owner | Ch4 design | Ch5 | Ch6 | Status |
|---|---|---|---|---|---|---|
| Connect existing sources read-only | Acquisition | DF1-DF3, B1-B4 | - | T1, T2 | 6.1.1.2 direction rule; connector entitlement 6.3.1 | **RESOLVED** |
| Join the plant like a puzzle, once | Transformation and relationships | DF4, C1, C6, 4.5.10 | 5.2 | T3 | Definition backup 6.1.11.1; upgrade compatibility 6.1.4.3 | **RESOLVED** |
| The plant model is permanent, versioned, queryable | Relationship model | 4.5.10, 4.5.11 | 5.2.13 | T3 | 6.1.4.3, 6.1.11.4 | **RESOLVED** |
| Mapping mistakes caught before corruption | Validation and quarantine | DF5, 4.5.14, C2 | - | T4 | E2E J8 6.1.5.5 | **RESOLVED** |
| Genealogy on the customer's own keys | Genealogy | DF6, C5, 4.5.4 | - | T4 | Performance threshold 6.1.5.7 | **RESOLVED** |
| BI-class authoring without a developer | Authoring | DF7, D1, D2, 4.5.13 | 5.1, 5.2 | T5, T8 | Authoring quota 6.3.1; Light tier | **RESOLVED** |
| Author pages, charts, measures and filters freely within the contracted package | Authoring freedom | D2, 4.5.11 | 5.1.16 | T5 | **Pages, jobs and DB-links are contracted commercial quotas (6.3.4a), enforced as soft guardrails that never interrupt work in progress. Reusable measures, dimensions, relationships, evidence and definition-safety mechanisms are not restricted at any tier (6.3.4c)** | **RESOLVED** |
| Dynamic and associative exploration | Associative engine | DF7, D1 | 5.1.3, 5.1.17 | T5 | Query load `D2` 6.1.9.3 | **RESOLVED** |
| Drill down and drill through to evidence | Evidence and provenance | 4.5.16 JP5, D4 | 5.1.19 | T4, T6 | - | **RESOLVED** |
| Bookmarks, saved selections, saved views | Reusable objects | 4.5.11 | 5.1.16 | T5 | - | **RESOLVED** |
| Statistical intelligence, honest statistics | Statistics engine | DF9, D3, D4 | 5.5 | T6 | `analysis` pool; Pro tier | **RESOLVED** |
| Refuses when data is insufficient | Readiness gate | DF8, 4.5.12 | 5.4.3 | T6 | Never counted as failure 6.1.12.5 | **RESOLVED** |
| Machine learning on the plant's own history | ML | DF10, DF11, D8 | 5.6 | T6 | `ml` pool; Pro Plus | **RESOLVED** |
| Practice learning for productivity | Practice engine | DF12, D10, 4.5.12 | 5.6.4a, 5.6.4b | T6, T8 | `analysis` pool; Pro Plus | **RESOLVED** |
| Early prediction before the outcome exists | Prediction | DF13, D9, 4.5.12 | 5.6.4c | T8 | **Latency sizing 6.1.5.7, 6.1.12.2**; Pro Plus | **RESOLVED** |
| Driver explanation, why this unit and why now | Explainability | DF13, D9 | 5.8.3 | T8 | - | **RESOLVED** |
| Historically supported downstream remediation | Remediation | DF13, 4.5.12 | 5.6.4d | T8 | Pro Plus | **RESOLVED** |
| Only safe, controllable, still-actionable recommendations | The nine-check gate | 4.5.12a, D9 | 5.6.4d | T8 | Unit test coverage 6.1.5.2 | **RESOLVED** |
| The prediction arrives before the stage that can act | Actionable latency | 4.5.12, D9 | 5.8.8 | T8 | **6.1.5.7 threshold, 6.1.12.3 P2 alert** | **RESOLVED** |
| Human approval, never automatic control | Decision boundary | DF14, 4.5.12a | 5.6.4d | T8 | Role matrix 6.3.2.2 | **RESOLVED** |
| Measured effectiveness after the fact | Feedback loop | DF14, 4.5.12 | 5.8.4 | T8 | - | **RESOLVED** |
| Intelligence is a first-class analytical object | Bindable intelligence | 4.5.13 | 5.1.19 | T8 | - | **RESOLVED** |
| Euro value as a bounded range | Value engine | DF14, D7 | - | T8 | Value-to-price model 6.3.7 | **RESOLVED** |
| Two downtime quantities never confused | Canonical model | 4.5.4 | - | - | - | **RESOLVED** |
| Compare periods, lines, equipment, contexts | Benchmarking | D12 | 5.8.5 | - | - | **RESOLVED** |
| Explanations with resolvable citations | Assistant | DF15, G1 | 5.7 | T5, T8 | Serving 6.1.1.1; **Pro Plus, hidden below** 6.3.1 | **RESOLVED** |
| Governed change, nothing self-modifying | Supervisor | DF15, E4 | 5.4.9 | T7 | Audit 6.3.9 | **RESOLVED** |
| Nothing industry-specific ships | Genericity | Rule 1, registry | - | - | **Genericity lint 6.1.7 gate 10; test-data rule 6.1.6** | **RESOLVED** |
| The customer keeps and can export everything | Definition export | 4.5.11 | - | T3 | **Backup 6.1.11.1; downgrade keeps data 6.3.3** | **RESOLVED** |
| Logs retained under the customer's own policy | Retention | 4.5.15, F9 | - | T7 | Storage sizing 6.1.9.4; audit minimum 6.3.3 | **RESOLVED** |
| Read-only toward customer systems | Boundary | 1.7, 4.6.2 | - | T1 | **Structural in all four topologies 6.1.1.2**; website 6.2.17 | **RESOLVED** |
| Air-gapped Enterprise deployment | Deployment | 4.6.2 T4 | - | not a user action | **6.1.1.3, 6.1.2.7, 6.3.3 offline licence** | **RESOLVED** |
| One codebase, four topologies | Deployment | 1.7, 4.6.2 | - | - | 6.1.1.3, 6.1.3.2 promotion | **RESOLVED** |
| Tenant isolation is absolute | Tenancy | 4.5.17 | - | - | **Gate 9 6.1.7; security tests 6.1.5.6** | **RESOLVED** |
| Expiry never destroys data | Entitlement | 1.7 | - | - | 6.3.3 | **RESOLVED** |
| Five products, PPIQ the flagship | Portfolio | - | - | - | **6.2.1, 6.2.12, 6.2.14** | **RESOLVED** |

## 6.4.3 Master Design Gaps

**Every Chapter 1 and 2 promise has a technical owner. Zero UNRESOLVED.**

Two items are recorded not as gaps in the promises but as **scope boundaries carried forward**, so they are not mistaken for silent omissions:

| # | Item | Status | Where it belongs |
|---|---|---|---|
| 1 | **MES, QES, Yard and Warehouse, and Energy Management have no Master Design chapters.** Chapter 6.2 specifies their **website presentation** and their licensing dimensions; it does not specify the products | **Deliberate boundary, not a gap.** Their website claims are governed by the status classification of 6.2.10 | A future Master Design per product |
| 2 | Unstructured text evidence and inspection images | **Future extension with interfaces designed** (Chapter 2 3.10) | Roadmap, with the interfaces already in Chapter 4 5.8.6 and 5.8.7 |

**No capability was invented in Chapter 6.** Where Chapter 6 needed something Chapters 3 to 5 did not carry - the job target definition, and the `/materials` landing state - it was **added to Chapter 3 as a correction** rather than solved inside Chapter 6.

---

# 6.5 FINAL MASTER-DESIGN ACCEPTANCE

**Re-run for v4.6. Values are not carried forward from v4.5.**

| # | Condition | Where | v4.5 | **v4.6** |
|---|---|---|---|---|
| 1 | Pipeline fully specified | 6.1.3, 22 stages | MET | **MET** |
| 2 | Container and runtime architecture | 6.1.2 | MET | **MET** |
| 3 | Deployment and upgrade paths | 6.1.4 | MET | **MET** |
| 4 | Test architecture across seven layers | 6.1.5 | MET | **MET**, extended with C1-C4 certification 6.1.5.8 |
| 5 | Backlog governance | 6.1.8 | MET | **MET** |
| 6 | A defensible sizing model | 6.1.9 | MET | **MET, corrected.** Canonical units 6.1.9.1a; cache-target RAM model 6.1.9.4; **all three worked examples recalculated and each satisfies its own classification rule** |
| 7 | Capacity protection and overload behaviour | 6.1.10 | MET | **MET** |
| 8 | Backup, restore, DR and HA | 6.1.11 | MET | **MET** |
| 9 | Monitoring and observability | 6.1.12 | MET | **MET**, extended with Scan Amplification 6.1.12.2a |
| 10 | Website structure and architecture | 6.2 | MET | **MET** |
| 11 | Website claims traceable to Chapter 1 | 6.2.4, 6.2.10 | MET | **MET** |
| 12 | User and role authorization mapped | 6.3.2 | MET | **MET** |
| 13 | Licence tier enforcement mapped | 6.3.1, 6.3.3 | MET | **MET** |
| 14 | Commercial capacity and infrastructure cost use one workload model | 6.1.9 and 6.3.5 | MET | **MET, strengthened.** Both operate in canonical units; tier selection derived from the six dimensions in 6.3.6 |
| 15 | Licence and pricing framework | 6.3.6 | MET | **MET, corrected.** The subscription now derives the tier from all six commercial dimensions |
| 16 | Sales-to-engineering handover | 6.3.10 | MET | **MET** |
| 17 | Every Chapter 1 and 2 promise traced | 6.4.2 | MET | **MET**, one entry corrected to the contracted-quota model |
| 18 | Zero unresolved promises | 6.4.3 | MET | **MET** |
| **19** | **Numerical self-consistency: every worked example passes the rule it is judged by** | 6.1.9.6 | - | **MET** |
| **20** | **One unit system, converted once at the boundary** | 6.1.9.1a | - | **MET** |
| **21** | **RAM formula, its definition and its examples agree** | 6.1.9.4, 6.1.9.6 | - | **MET** |
| **22** | **Chapter 1 and Chapter 6 commercial semantics reconciled** | Ch1 1.6.3, 6.3.4.0 | - | **MET** |
| **23** | **Delta architecture authoritative in Chapter 4, with execution mechanics** | Ch4 5.3.9, 5.3.9.6a | - | **MET** |
| **24** | **Scan Amplification measurable, with acceptance bands and a regression gate** | 6.1.12.2a | - | **MET** |
| **25** | **Performance constants labelled with calibration status** | 6.1.9.4a | - | **MET** |
| **26** | **C1 to C4 certification executed and constants replaced by measured values** | 6.1.5.8 | - | **NOT MET - execution pending** |

## 6.5.1 Freeze status

**Chapter 6 is NOT marked MASTER DESIGN FROZEN.**

Twenty-five of twenty-six conditions are met. **Condition 26 requires physical execution, not further design**: the C1 to C4 benchmark profiles must run, and the ten `REFERENCE_ASSUMPTION` and `CALIBRATION_REQUIRED` constants of 6.1.9.4a must be replaced by versioned measured values with their evidence recorded.

| Until then | Rule |
|---|---|
| Sizing outputs | Carry the label **"based on reference assumptions, pending performance certification"** |
| Hardware profiles | **A tier is not commercially quotable until its matching profile has passed** (6.1.5.8) |
| Constant set | Version 0, unmeasured. Every quotation records this |
| The design itself | Complete and internally consistent. **What is missing is measurement, not specification** |

**On passing C1 to C4:** replace the constants, increment the constant set version, re-run this table, and only then mark Chapter 6 **MASTER DESIGN FROZEN**.

---

*End of Chapter 6. Chapters 1 to 6 constitute the complete PlantProcess IQ Master Design, pending capacity certification.*
