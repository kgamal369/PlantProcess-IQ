# PlantProcess IQ — Aspects of Review, Evaluation Criteria & Scoring Doctrine (v4)

**Document type:** Strict, evidence-based audit instrument for the PPIQ implementation
**Supersedes:** v3. v4 keeps the entire methodology unchanged (it is deliberately stable) and applies only surgical updates where the **build reality moved on 26 Jun 2026**: the baseline score, two gate-proof annotations (G12 identity/security and G9 operations), and the deploy-correctness criteria (A1.9/A1.10) — which must now reflect the **two-project server topology** (`ppiq-app` for the app, `plantprocessiq` reserved for infrastructure) and the **local-vs-server dual environment**. No persona, criterion, or capability was removed; the headline persona score is deliberately held (the value engine is still unbuilt — see C3).
**Aligned to:** Doctrine v7.0 — 16 acceptance gates (G1–G16) · 4 realization waves (A–D) · 4 tracks · the honesty contract · ≈46→84 scoring scale (baseline moved this session — see C3)
**Last updated:** 26 Jun 2026
**Audience:** Any reviewer handed the build — internal team lead, prospective engineering buyer, security/procurement, executive sponsor, or external auditor
**Governing intent:** This rubric exists to make a *fake high score impossible*. Every criterion below carries a **hard pass/fail test**, a **reproduction method**, and an explicit **failure condition**. A reviewer who cannot produce the stated evidence must score the item in the failing band. Capability claimed in a document, a comment, or a conversation is worth **zero**; only behavior demonstrated live through the HMI on the demo dataset counts.

---

## 0 · How to use this document

Three layers, applied in order:

- **Part A — Six evaluation personas.** PPIQ is judged from six independent professional vantage points, each scoring **8–12 criteria**. Every criterion states *what must be true*, *the hard test that proves it*, and *the failure condition that fails it*.
- **Part B — Capability & domain specification.** The objective backbone the personas score against: every feature, workflow, job, the generic-platform mandate, and the complete flat-steel demo blueprint (equipment, every source database and its tables, the downtime model, the dataset scale). This is the ground truth; if a persona criterion and Part B disagree, Part B wins.
- **Part C — Scoring doctrine & the surprise-question gate.** The bands, the evidence rule, the persona-and-gate reconciliation, and the live buyer-objection playbooks the demo must survive.

**Four standing rules bind the entire review (expanded in C4):**
1. **No score without a live demonstration** through the HMI on the demo dataset.
2. **Honesty outranks capability** — a confident wrong answer scores *below* an honest "collecting data," and any forbidden commercial claim is automatically Critical.
3. **The demo path is sacred** — every button on it works, or the path is not demo-ready (the no-"85%" rule).
4. **Read-only and OT-safety are absolute** — any write-back path to a control system is an automatic Critical, regardless of all other strengths.

---

# PART A — The Six Evaluation Personas

A build that satisfies one reviewer and fails another is **not shippable**. The headline result is the **lowest** persona score, never an average that hides a failing dimension.

---

## A1 · The Developer / Maintainer

*"Can I extend this codebase a year from now without it fighting me — and is it honest about what it actually does?"*

| # | Criterion | Hard test (how the reviewer proves it) | Failure condition |
|---|-----------|----------------------------------------|-------------------|
| 1 | **Code hygiene & clean code** | Static scan + manual read of 10 random files: no commented-out code blocks, no `*.runtime.cs`/shim tombstones, no duplicated logic that should have one home, consistent formatting (UTF-8, LF). | Any tombstone shim, any duplicated decision (e.g. the same provider-availability array in 3 places), any dead code. |
| 2 | **Stability under change** | `dotnet test Backend` + `vitest` + `e2e` all run green twice consecutively; no flaky test; no environment-specific config required to build. | Any flaky test; any "works on my machine" config; a green deploy sitting on a red suite. |
| 3 | **Repo cleanliness & structure** | Confirm one canonical layout (`Domain / Application / Analytics.Core / Analytics.Engine / Infrastructure / Workers / Api`); grep for duplicate Jenkinsfiles, stray `docker-compose.yml`, committed backup folders, unregistered test projects. | Any duplicate pipeline/compose file, any orphaned backup folder, any test project missing from the solution. |
| 4 | **Representative naming & file structure** | Open any unfamiliar class; its name predicts its content; partial files follow one naming convention; one tier enum, not three. | A file whose name misrepresents content; >1 license-tier enum; inconsistent partial-file naming. |
| 5 | **Test architecture — execution, not enumeration** | Inspect the Jenkinsfile and the test projects: `dotnet test`, `npm run test`, and `npm run e2e` are invoked as **blocking** stages that **execute** (no `--list`, no `catchError(SUCCESS)`); skips are registered in a `TESTS.md`-style ledger with justification. | Tests enumerated not run; failures swallowed; an unregistered skip; e2e wrapped to always pass. |
| 6 | **Generic — any plant, any DB, any domain** | With no code change, link each of the six source types (MSSQL, PostgreSQL, MySQL, Oracle, Excel, CSV); confirm no plant name, product, routing, schema, or defect type is hardcoded anywhere. | Any hardcoded plant/product/schema/route; a connection target compiled into the binary; a defect type baked into code. |
| 7 | **Extensibility without fragility** | Trace what it takes to add a connector, a KPI, a widget, a job: each is an additive change behind a stable seam (`IConnector`, mapping views, `LicenseFeature`), not a cross-tree shotgun edit. | Adding any of the four requires editing many unrelated files; a new feature forces changes to the core engine. |
| 8 | **Structural consistency** | Verify one error-boundary primitive, one save-hook pattern, one entitlement resolver, one validation contract — reused everywhere. A reviewer can predict unread code's shape. | Multiple competing patterns for the same job; bespoke error handling per page. |
| 9 | **Pipeline & deployment correctness** | Read the Jenkinsfile end to end and confirm it runs **only** this sequence and nothing else: `pull → dotnet test → npm test → npm e2e → migrate (main Postgres app DB + demo/source DBs) → seed data → build → recreate the one canonical live stack in place → health gate + rollback → presentation smoke`. Confirm **two clearly separated compose projects** — the **application** project `ppiq-app` (postgres/api/web) that the infra Caddy serves, and the **infrastructure** project `plantprocessiq` (Jenkins/Caddy/backup-runner) that must **never** be reaped by an app deploy — with **zero** orphaned parallel app stacks. Prove the red path: break one test, push, confirm the deploy stages never run; revert, confirm green deploys. *(As of 26 Jun 2026 this pipeline is GREEN end-to-end on the server, build #96; the app-project rename `plantprocessiq`→`ppiq-app` is what stopped `--remove-orphans` from reaping Jenkins.)* | Any extra pipeline step beyond the agreed sequence; the app deploy sharing a project name with infra so `--remove-orphans` reaps Jenkins/Caddy; any orphaned/parallel app stack; a deploy reachable while a suite is red; wrong connection-string key (`DefaultConnection` instead of `PlantProcessDb`) or missing signing key. |
| 10 | **Switchable run/test profiles (local & server)** | Demonstrate exercising **every** auth/license combination locally and on the server **without code edits or DB surgery**: seeded one-user-per-role and a forced-tier switch, applied via config/overlay, **refused in Production** unless an explicit accept-risk flag is set. Confirm the reviewer can log in as Admin / Executive / Engineer / Operator and force Light→Enterprise on demand. **Both environments must be honoured as first-class:** LOCAL uses the five dev-seed config users (`deploy/compose/.env.dev`) for daily development/testing; SERVER login is DB-backed (`app_users`) with the permanent `sysadmin` (auto-provisioned, support-only) and the customer admin added manually at commissioning — never a customer-named admin baked into the install image. | Testing different auth/license states requires editing code or hand-editing the DB; a test-mode switch silently active in Production; a customer-usable admin shipped in the install image; the demo dev license key registered in a real customer Production. |
| 11 | **No silent failure anywhere** | Force one widget's data endpoint to 500: confirm a **contained, branded, retryable** error in that widget while the rest of the page stays interactive; confirm **zero** unhandled promise rejections in the console across the demo path. | Any "could not load … works on retry"; a single widget error blanking a page; an unhandled rejection in the console. |
| 12 | **Resilience of every endpoint/query** | Enumerate every API + its SQL/joins; confirm no expected failure from PK/FK issues, missing indexes, or null handling; long queries return a progress/loading state, never a hang. | Any endpoint that throws on a foreseeable PK/FK/null case; any query that hangs the UI with no loading state. |

**Why this persona cannot be gamed:** criteria 5, 9, and 11 are each an automated, parsable test (a guard test reads the Jenkinsfile; an e2e spec forces a 500; the suite runs twice). A reviewer either produces the green run and the red-path proof, or the item fails.

---

## A2 · The Security & IT / Procurement Reviewer

*"Can my automation team and my DBA approve this without a control-systems risk review or a data-egress concern?"*

The veto-holder in any real plant sale. This is the entire content of Doctrine Wave A and §18.

| # | Criterion | Hard test | Failure condition |
|---|-----------|-----------|-------------------|
| 1 | **Read-only is absolute** | Code-search every source/control path for any write/setpoint/command/DDL-on-source; confirm outbound is only message/export/webhook. | Any write-back path of any kind to a source or control system. |
| 2 | **OT-safe acquisition topology** | Confirm sources are reached through a customer-controlled Edge Collector that **pushes one-way**; the core never initiates a connection into OT; a data-diode option is documented. | The core connects *into* the OT network; any inbound path to control; no collector model. |
| 3 | **Source-load protection** | Confirm per-source row caps, statement timeouts, rate limits, approved windows; backfill is throttled, resumable, checkpointed — never one giant query against the primary. | An unbounded query against a production source; a backfill that is one giant SELECT. |
| 4 | **Token & session security** | Confirm the access token lives **in memory + an HttpOnly refresh cookie with rotation/revocation** — *not* localStorage; Argon2id password hashing; MFA enforced for admins. | A token in browser localStorage; a hardcoded/weak hash; no admin MFA path. |
| 5 | **Secrets handling** | Confirm source credentials live only in the collector's encrypted vault, masked on read-back, never in the browser or app config; per-environment signed keys, no hardcoded signing key. | Any credential in browser/config; a hardcoded JWT signing key; an unmasked secret on read-back. |
| 6 | **Tenant isolation** | Confirm SaaS = TenantId + row-level security and dedicated = physical isolation, one resolver/one ruleset; run a cross-tenant request and confirm **403/empty**. | Cross-tenant data leakage; separate code forks per topology. |
| 7 | **Per-endpoint authorization** | Enumerate endpoints/pages/jobs/tools via `EndpointDataSource`; confirm each checks role + entitlement; confirm **no** dev-seed or diagnostic/proof endpoint is reachable in a production build. | Any unguarded admin surface; any dev-seed/diagnostic endpoint exposed in prod. |
| 8 | **AI data boundary** | Confirm engines compute in-tenant; the assistant model is self-hosted by default, or a zero-retention private endpoint receiving **only** the question + scoped evidence; a per-tenant no-egress toggle exists. | Plant data sent to an external model for computation; no no-egress toggle; the assistant doing arithmetic. |
| 9 | **Audit & encryption at rest** | Confirm an append-only, immutable audit log on sensitive actions and encryption at rest. | Mutable/absent audit log; plaintext at rest. |
| 10 | **Deployment hardening** | Confirm the database port is bound to 127.0.0.1 (not publicly exposed), the bootstrap admin is replaced, health/readiness endpoints exist, and runbooks exist. *(As of 26 Jun 2026 the bootstrap admin is replaced by the permanent `sysadmin` owner auto-provisioned at first run — a support-only account the customer never uses; health endpoints are anonymous and the deploy is health-gated with rollback to `:previous`. Still verify the server DB-port binding.)* | A publicly exposed DB port; an active bootstrap admin; a customer-usable admin shipped in the image; no health/readiness; no runbook. |

**Why this persona cannot be gamed:** every prior-audit finding (localStorage token, hardcoded JWT key, unconditional dev-seed, exposed PostgreSQL port, bootstrap admin) is an explicit, individually testable failure condition. Each is binary.

---

## A3 · The Process / Quality Engineer (primary daily user)

*"Will this help me investigate a quality problem faster than my spreadsheets — and can I trust what it tells me?"*

The skeptical engineer the no-"85%" rule is written to survive.

| # | Criterion | Hard test | Failure condition |
|---|-----------|-----------|-------------------|
| 1 | **Zero dead buttons on every path** | Enumerate every button/action on every page and sub-page (search, load investigation, calculate risk, generate PDF, min/max, filter) and click each: every one performs its function. | Any button that does nothing, errors, or no-ops min/max/calculation/load. |
| 2 | **End-to-end workflow without crash** | Run the full lifecycle live: link source → stage → map → build page → configure widgets → run job → read dashboard → export. No crash, no stall, smooth data transfer. | Any crash, stall, or broken data transfer anywhere in the chain. |
| 3 | **UI/UX clarity & uniform styling** | Confirm consistent component styling across **all** surfaces — buttons, tables, tabs, wording, layout, orientation — current *and* future (the Material-Investigation button-mismatch class of defect is standardized away, not patched once). | Any styling mismatch (the search/load/calculate/PDF buttons looking different); inconsistent tables/tabs. |
| 4 | **Widget customization for non-developers** | Build a chart from the library by drag-drop, bind it to data with **no** endpoint written, and apply a script-layer transform (e.g. "group this bar chart by shift"). | Any chart that requires a new endpoint or a source-code edit; no script layer. |
| 5 | **Genealogy golden thread** | Click from a surface defect on a finished coil all the way back to the melt chemistry — on the **customer's own** key names — **both** directions, across all eight sources. | The thread breaks; requires PPIQ-internal keys; one direction missing. |
| 6 | **Population always stated** | Confirm every analysis shows its population and exclusions ("22 of 60 heats needed"); a "collecting data" state appears instead of a fabricated answer. | Any analysis that states a driver without stating its population; silent survivorship. |
| 7 | **Correlation & ML jobs, honestly** | Run the learning jobs (parameter↔defect, ↔downtime, ↔KPI) on demand and scheduled; confirm they recompute on demo data and surface most-influential parameters using the L4 method set — **Spearman, mutual information, Lasso, VIF, bootstrap** — under **Benjamini-Hochberg FDR control + stratification** by visible confounders, framed as *suspected contributors*. | A learning result presented as a screenshot/fixture; a causal claim; no FDR/stratification; correlation by a single naive method with no multiple-testing control. |
| 8 | **Blended provenance correctness** | On a casting-transition coil, confirm the system reports **weighted shared attribution** between two heats (e.g. H-3361 ≈70% / H-3362 ≈30%), never a single fabricated heat. | A transition coil attributed to one heat; no weighting. |
| 9 | **Performance under real data** | At demo scale (≈630 heats / ≈5,600 coils / 1 month) confirm no hang/lag; large datasets show a progress indicator; tables virtualize. | Any hang; a large table rendering every row; no progress state on a long load. |
| 10 | **Interactivity — filters, sorting, heatmap** | Confirm every widget/chart is dynamic and responds to filtering and sorting; the **heatmap renders and interacts correctly**; drag/move/min/max behave. | A static chart; a filter that does nothing; a broken heatmap. |
| 11 | **Correct & effective results** | Spot-check that analysis outputs are correct and useful against the known demo data (the demo's deliberate imperfections produce the expected findings). | Results that are wrong, meaningless, or contradicted by the known demo data. |

**Why this persona cannot be gamed:** criteria 1, 5, 8, and 10 are concrete, single-click demonstrations on named demo entities (e.g. coil C-0044170, transition coil). The reviewer either reproduces the exact behavior or fails the item.

---

## A4 · The Reliability / Operations & Plant-Admin User

*"Does it keep running, tell me when something breaks, and let me configure the plant without calling the vendor?"*

The person who onboards the data, owns the jobs, and keeps the lights on.

| # | Criterion | Hard test | Failure condition |
|---|-----------|-----------|-------------------|
| 1 | **Source onboarding from the HMI** | In the admin DB-Configuration page → DB-Link tab: create a link, pick a provider, **test connectivity before save**, confirm masked credentials, select which tables to import, set sync cadence (2 min → days) with off-hour windows. | Any of these requiring code/DB edits; no test-before-save; unmasked credentials. |
| 2 | **Mapping & schema configuration** | In the Schema-Configuration page: author a view/join to fit the generic schema; reconcile mismatched keys (join HSM Oracle `pieceId` to Parsytec MySQL `materialId`; C-0044170 ≡ 44170); define a KPI as a versioned SQL view; confirm tiered authoring (no-code/assisted/expert) so the common case needs no SQL. | Mapping only possible via code; keys silently merged; KPI hardcoded not a view. |
| 3 | **Import jobs & delta logic** | Confirm each import is a named job with cycle and status; the next scan compares the last index in the dump against the source's last index and imports **only** new rows; each run writes an import batch (rows, duration, watermark, errors). | A re-sync re-imports everything; no batch record; no delta cursor. |
| 4 | **Jobs Monitor** | In the Jobs-Monitor page confirm **every** job (import, correlation, ML, demo-reset) shows last-run time, outcome (ok/crash/timeout), duration, and source-impact. | Any job not monitored; missing outcome/duration; no crash/timeout surfaced. |
| 5 | **Schema-drift detection & mapping health** | Add/rename/remove a source column: confirm a typed Schema-Change event, dependent mapping views flagged, dependent imports paused rather than producing wrong facts; a Mapping-Health panel shows green/degraded/broken and **why**. | A schema change silently corrupting a mapping; no health panel; no typed event. |
| 6 | **Fail loudly & specifically** | Introduce a bad join: confirm a precise typed error (`NoSuchView`/`NoSuchColumn`/`InvalidAggregateForType`/`AmbiguousJoinKey`) with the affected view and the next safe step. | A generic/silent failure; a wrong number instead of a typed error. |
| 7 | **Readiness meter** | Confirm the product shows simple analysis + an honest countdown ("X of Y heats needed") while a gate is blocked, and that a backfill collapses the timeline. | A blank screen while collecting; a fabricated advanced result before readiness. |
| 8 | **Backfill & source protection** | Confirm historical load is throttled, idempotent, watermark-tracked, pausable/resumable, visible in the Jobs Monitor, honouring the source-impact budget; history can come from a DBA dump, a replica, or off-peak range reads. | An unthrottled backfill; a non-resumable load; no source-impact visibility. |
| 9 | **Operational resilience** | Confirm a clean machine reaches a working login by **runbook only**; backup/restore is drilled; health/readiness endpoints exist; a failed deploy rolls back to `:previous`. | No runbook path to login; no restore drill; a failed deploy with no rollback. |
| 10 | **Concurrency & collaboration** | Two users edit the same page; one saves; confirm the other gets an **optimistic-concurrency conflict dialog**, not a silent overwrite; draft/publish immutability holds. | A silent last-write-wins overwrite; no conflict dialog; mutable published definitions. |

**Why this persona cannot be gamed:** criteria 3, 5, 6, and 10 each require *inducing* a condition (re-sync, schema change, bad join, concurrent edit) and observing the exact specified behavior — not reading a claim.

---

## A5 · The Executive Sponsor (the economic buyer)

*"Will this pay back more than it costs, can I trust its recommendations, and does each role see the right scope?"*

| # | Criterion | Hard test | Failure condition |
|---|-----------|-----------|-------------------|
| 1 | **Quantified value (the euro engine)** | Confirm a value engine converts a finding into a **bounded euro range with an abstain path**, computed on demo data, every input drill-throughable (the §7.4 finding → a bounded range, e.g. €28k–€56k, reproduces). | Value asserted, not computed; no abstain path; an input that cannot be drilled. |
| 2 | **Role-scoped view & edit** | Log in as each role and confirm scope differs by **view** and by **edit**: an executive sees KPIs/ROI/trends; an engineer sees investigation; an admin sees configuration; a planner/maintenance engineer sees their own concern. | Any role seeing pages or edit rights outside its scope; identical scope for all roles. |
| 3 | **License tiers demonstrable live** | Toggle tier (Light/Pro/ProPlus/Enterprise) during the demo and confirm features **visibly appear/disappear**; confirm entitlements come from a **signed token, not an editable row**. | A tier toggle that changes nothing visible; entitlements editable in the DB. |
| 4 | **Trustworthy AI output** | Confirm deterministic engines compute/rank and the assistant only **explains with citations**; confirm the assistant **cannot render an uncited number**; audit a claim to its evidence handle. | The model doing arithmetic/ranking; any uncited number; a claim with no resolvable evidence. |
| 5 | **Honest boundary as an asset** | Confirm the product says "suspected contributor," states what it does not know, and never claims guaranteed root cause; confirm it stratifies by confounders and names an unmeasured likely one. | Any "guaranteed root cause" claim; a finding with no confounder discussion. |
| 6 | **Speed of insight** | Confirm dashboards/KPIs work day one; advanced findings arrive as readiness turns ready; no spinner without progress. | A wait on a spinner with no progress; nothing usable on day one. |
| 7 | **Price-to-value parity** | Confirm the demonstrated value (loss reduction, quality lift, downtime avoided) is credibly **greater** than the tier price. | Demonstrated value below the price; value not demonstrated at all. |
| 8 | **Cross-device / cross-browser** | Open the app at multiple screen sizes, on multiple major browsers, over http and https; confirm correct rendering and re-flow. | Any broken layout on a common browser/size; no re-flow. |
| 9 | **Competitive distinctiveness** | Confirm it is clearly **not** "another BI dashboard": it carries genealogy, defect-drivers, value, and suggestions BI tools lack. | Indistinguishable from a BI dashboard; charts on data the user must prepare. |
| 10 | **Trust posture & brand** | Confirm the product reads as "plant operations + data science" — calm, industrial, evidence-based — not flashy consumer AI. | Consumer-AI tone; over-claiming visuals; off-brand presentation. |

**Why this persona cannot be gamed:** criterion 1 names the exact figure (a bounded range reproducing on demo data); criteria 3 and 4 are live toggles and live audits. Each is reproducible or it fails.

---

## A6 · The Brand & Website Reviewer (the first impression)

*"Before anyone logs in, does the website make a serious buyer request a demo — and does the brand hold across every surface?"*

The website is the **first and best salesperson** (the Golden Rule); the brand must be identical across website, app, and reports.

| # | Criterion | Hard test | Failure condition |
|---|-----------|-----------|-------------------|
| 1 | **Brand name, tagline & voice** | Confirm exact use: product "PlantProcess IQ / SOU," short "PPIQ," primary tagline **"Connect Your Plant Data. Understand Your Process."**, secondary "Process-to-quality intelligence for manufacturing plants," and the customer promise/elevator pitch; confirm the industrial/evidence-based voice everywhere. | Tagline drift between surfaces; off-voice ("flashy consumer AI") copy. |
| 2 | **Forbidden vs approved claims (honesty-lint)** | Confirm **nothing** says "guaranteed root cause," "AI-powered prediction," "production-ready AI," "live Oracle/MSSQL ready today," or "we replace MES/L2/SCADA/BI"; confirm it **does** say "rule-based risk scoring," "correlation analysis," "suspected contributor," "statistical pattern," "evidence-based investigation," "read-only intelligence layer." | Any single forbidden claim anywhere on the site or in-app. |
| 3 | **Color palette fidelity** | Confirm the Dark Industrial Command Center palette exactly: Deep Navy Black `#050B18`, Panel Navy `#0B1730`, Industrial Blue `#102A43`, Electric Blue `#0A84FF`, Electric Cyan `#00D4FF`, Corporate Blue `#2F80ED`, Cyan Green `#2CE6A2`, Amber `#FFB020`, Hot Red `#FF4D6D`, Near-White `#EAF6FF`, Muted Steel `#8EA7C1`, light report surface `#F4F6F8`. | Any off-palette color; status colors misused. |
| 4 | **Typography** | Confirm Inter for UI, JetBrains Mono for SQL/code, minimum body 14px web / 12pt PDF, heading scale (web 48/36/28/20/16; app 24/18/14/12). | A playful font; body below minimum; inconsistent heading scale. |
| 5 | **Logo system** | Confirm a connected-node / hexagonal mark (4–6 nodes, one dominant central node), **industry-neutral** (works for aluminum/pharma/paper/automotive, not steel-only); variants full/icon/stacked in color/dark/light/mono; none rotated, recolored off-palette, stretched, shadowed, or boxed. | A steel-only mark; a missing variant; any prohibited logo treatment. |
| 6 | **Website UX & responsiveness** | Confirm advanced/professional/shiny UI; correct at every screen size, browser, and http/https; clear nav; fast. | Broken responsiveness; slow load; unclear nav. |
| 7 | **Product ecosystem — all five** | Confirm each product has description, benefit, interactive graphics/pictures, licensing, and full detail under the Golden Rule: **(1) PlantProcess IQ, (2) MES — Manufacturing Execution System, (3) QES — Quality Execution System, (4) Yard & Warehouse Management, (5) Energy Management.** | Any of the five missing or thin; no Golden-Rule CTA on a product page. |
| 8 | **In-app brand implementation** | Confirm the app embodies the identity: Deep-Navy full-bleed background (no white-space leakage), Panel-Navy nav (56px, logo left, Electric-Blue active, Muted-Steel inactive), Industrial-Blue sidebar (Electric-Cyan active border), Panel-Navy cards (cyan hover border, 12px radius), Electric-Blue primary buttons, the chart-color order, status-badge colors, and the tier badge (Light=Muted Steel, Pro=Amber, ProPlus=Electric Blue, Enterprise=Cyan Green). | White-space leakage on dark surfaces; off-spec nav/sidebar/cards/badges. |
| 9 | **Reports / PDF brand** | Confirm deliverables switch to the light-mode surface (`#F4F6F8`) with brand header/footer maintained. | A dark-theme PDF; missing brand header/footer. |
| 10 | **CTA & lead capture** | Confirm a working call-to-action that **captures a lead/inquiry** (the Golden Rule's measurable exit). | A CTA that goes nowhere; no inquiry capture. |

**Why this persona cannot be gamed:** criterion 2 is a literal string lint (forbidden vs approved phrases), criterion 3 is exact hex matching, and criterion 7 enumerates all five products by name — each is a concrete presence/absence check.

---

# PART B — Capability & Domain Specification (the objective backbone)

The personas score against this. It is organized on the V1 four-track skeleton the doctrine preserves.

## B0 · The product in one line

PPIQ is a **generic, read-only, evidence-grade plant-intelligence platform**: each plant connects its own sources, defines its own mappings, builds its own pages, monitors its own jobs, and investigates its own quality / downtime / KPI questions — carried from fragmented raw data to trustworthy understanding, **never asked to take a black box on faith**.

## B0.1 · The generic mandate (why nothing may be hardcoded)

The product installs in **any** plant, and every plant differs in: database **types**; database **structures and tables**; **inspection devices** that generate different defect types and structures; **production-line** structures; **processes and unique workflows**; and the **focus of each CEO and process engineer** — different parameters, KPIs, and correlations. Therefore the data domain model, every endpoint, and every surface must be generic, achieved by: (a) connecting to all six source types; (b) admin-defined DB links with per-table import selection and per-object sync cadence; (c) admin-authored mapping/views into one generic schema; and (d) HMI-built pages/widgets with a script layer — so the same binary serves every plant by **configuration, not code**.

---

## Track 1 — Workflow & Product

### B1.1 · Generic source integration (Admin → DB-Configuration → DB-Link tab)
**Must do:** connect MSSQL / PostgreSQL / MySQL / Oracle / Excel / CSV with no code change.
**How:** admin links a source, picks a provider, the system **tests connectivity before save**, masks credentials, the admin selects which tables to import and sets the read/import cadence (every **2 min up to several days**); the connector catalog declares **honest availability per source version**.
**Evidence to find:** all eight demo sources linkable through the HMI; a not-yet-certified connector says so rather than claiming "live Oracle ready today."
**Gate G1 · Track 1.**

### B1.2 · The staging / dump layer
**Must do:** hold the **latest source-shaped copy** of each customer object — *not* PPIQ's structure.
**How:** namespaced tenant/source staging; on each scan the system compares the **last index in the dump** against the **source table's last index** and imports only the new records; every sync writes an import batch (rows, duration, watermark, errors). Each import is a **named job with a cycle and a crash status**.
**Evidence to find:** a re-sync imports only the delta; staging stays source-shaped; canonical transformation happens later.
**Gate G1.**

### B1.3 · Mapping & joining engine — *the heart* (Admin → Schema-Configuration page, multiple tabs)
**Must do:** turn many foreign keys into one canonical fact, generically — the one place the compass explicitly asked for a complete, effective, advanced, professional, detailed, generic design.
**How:** a **layered join model** from business keys, strengthened through normalization, mapping views, genealogy, and a confidence score; a **business-key dictionary** reconciles mismatched IDs explicitly (HSM Oracle `pieceId` ≡ Parsytec MySQL `materialId`; "C-0044170" ≡ "44170") and **rejects conflicts rather than silently merging**; a mapping-authored **safe-SQL view** (read-only; INSERT/UPDATE/DELETE/DROP/DDL-on-source rejected; implicit row limit + statement timeout; `EXPLAIN` before publish; typed errors) produces canonical facts by walking slab → coil → defect; **KPIs are first-class versioned SQL views** (e.g. First-Pass Surface Yield by grade/day), consumed by widgets *and* the KPI-contributor learning job; a normalized EAF process model (a `processDefinition` table of process-id/name and a values-against-heat table) is joined by user or automatic script.
**Tiered authoring (the answer to "must my configurator be a programmer?" — for the common case, no):**

| Tier | Offers | Who |
|------|--------|-----|
| No-code visual mapper | drag-drop field mapping, point-and-click joins on detected keys, industry & KPI templates — the common majority, zero SQL | process / quality engineer |
| Assisted | profiling suggests keys/normalization/joins with confidence; the assistant drafts a view from a plain-language description for a human to review (execution stays deterministic) | engineer + reviewer |
| Expert SQL | the safe-SQL view builder for the complex long tail | data engineer (customer or SOU) |

**Evidence to find:** a coil resolves **both** genealogy directions on demo keys; a bad mapping returns a typed error and **rolls back**.
**Gate G2 · Track 1.**

### B1.4 · Import execution (Admin → DB-Configuration → Importing-Data tab)
**Must do:** after mapping/scripting/views/joins are configured, import from the dump files into PPIQ's generic schema on a cadence that drives HMI refresh when new data is ready.
**How:** the import is a **named job with cycle and crash status**; cadence is admin-set.
**Gate G1/G3.**

### B1.5 · Page & widget building (Front-End — config-from-HMI)
**Must do:** create/modify/delete pages and place widgets with **no** source edit.
**How — Creating Page:** a user creates/deletes/modifies any page; a **widget library** (all charts, KPIs, sorting/filtering tools — date-time, list-of-values) is drag-dropped onto the page, then **Save**; existing pages can be edited (add/delete a widget) and saved.
**How — Widget:** the selected widget has an **edit button** to bind it to a list of values from the DB; a **script layer** adjusts behavior (e.g. a bar chart "group by shift") so the product does **not** need an endpoint per widget — otherwise it would need 100k endpoints, which violates generic design.
**Evidence to find:** the **entire demo builds from empty through the HMI** — no hardcoded page or widget.
**Gate G3 · Track 1.**

### B1.6 · The engine's learning jobs (Functionality)
**Must do:** learn correlations across the plant on a schedule (typically off-hours) or on demand.
**How — four standing jobs, each monitored in the Jobs-Monitor page:**
1. parameter ↔ **all defect IDs** (e.g. daily / user-defined, off-hours, long-running);
2. parameter ↔ **downtime IDs** (same cadence);
3. parameter ↔ **all defined KPIs** (same cadence);
4. an **overall** job across all data (e.g. weekly / user-defined).
The correlation engine uses the L4 method set — **Spearman rank correlation, mutual information, Lasso, VIF (multicollinearity), and bootstrap** — under **Benjamini-Hochberg FDR control and stratification** by every visible confounder; results are framed as *suspected contributors*, never causes.
**Gate G4/G5 · Track 1.**

### B1.7 · Correlation page (ML area — one tab per generated job)
**Must do:** let a user investigate a specific outcome.
**How:** the user selects a specific **defect ID / downtime ID / KPI ID** and an inspection window (last week / month / year), names the inspection job, and runs it on the learning engine; on completion it **generates a page** of widgets/charts showing which parameters most influence the outcome; the user can **save** the page and run it **periodically** (e.g. hourly/daily) or on demand via a **Run** button.
**Gate G4 · Track 1.**

### B1.8 · Suggestion & recommendation page (ML area)
**Must do:** turn the four standing jobs' learning into plant-improvement suggestions.
**How:** a **deterministic** suggestion engine; the assistant **explains** with citations and **cannot render an uncited number** (a **no-fabrication guard** rejects any number without a resolvable **evidence handle** before display); an evaluation harness gates regressions.
**Evidence to find:** the assistant emits **no uncited number**; on a transition coil it answers with weighted shared attribution.
**Gate G4 · Track 1.**

### B1.9 · Value engine
**Must do:** convert a finding into money, honestly.
**How:** the §7.5 formula with a per-tenant cost table and an **abstain path**; downtime modeled as **two distinct quantities** — *equipment-stopped minutes* vs *production-impact minutes* — and the right one used (see the downtime model in B-demo).
**Evidence to find:** a finding reproduces a **bounded euro range** (e.g. €28k–€56k) on demo data, every input drill-throughable; the engine abstains rather than inventing a number. *(The single largest doctrine-to-build gap — score strictly.)*
**Gate G5 · Track 1.**

---

## Track 2 — Hardening (the go-live quality bar)

The standing instruction: **no error, bug, crash, or non-functional command is acceptable in front of the customer.** Specifically — never a "view could not load / this part of the page could not load" that then works on retry; never a styling mismatch (the Material-Investigation search/load/calculate/PDF buttons must be standard, and standard **everywhere**, current and future); never a button that doesn't perform its function (min/max, calculation, loading).

**Enumerated hardening checklist (every item is a test):**
1. **Buttons** — every button on every current and future page/tab matches the standard style **and** performs its function 100%.
2. **Tables** — all tables share one style.
3. **Tabs & wording** — consistent across all current and future pages/dashboards.
4. **Layout** — alignment, orientation, and component placement are perfect, advanced, professional from a UI/UX standpoint, everywhere.
5. **Widget interaction** — drag/drop/move/min/max done correctly.
6. **Responsiveness** — every page renders correctly at every screen size, on any browser, over http/https, and re-flows properly.
7. **No hang/lag** — large datasets show a loading icon with a progress percentage.
8. **Endpoint resilience** — every current and future API + its SQL/joins handles PK/FK/null without an expected failure.
9. **No surprises** — nothing breaks unexpectedly in front of the customer.
10. **Every interactive element** — every button, component, hook, and recall performs its function 100% correctly.
11. **Action coverage** — every action on every page/sub-page/navigation is tested.
12. **Intelligence ready** — ML, correlation, and suggestion/recommendation are ready.
13. **Dynamic widgets** — all widgets/charts are dynamic and interactive and work with filter/sort.
14. **Heatmap** — renders and interacts correctly.
**Gate G6/G10 · Track 2.**

Security & identity (persona A2) and operations/SLA (persona A4, criteria 8–9) are the other two hardening pillars. **Gates G12/G13, G9.**

---

## Track 3 — Demo (proving the engine)

### B3.1 · The Golden Rules (preserved verbatim from the compass)
The demo is **not a separate app or an extra layer** — it is the **emulation of an imaginary customer's data sources**. **Nothing is hardcoded.** The team prepares only the **different databases and data sources with the dataset**, then performs **every** DB-link, job, page creation, widget/chart configuration, data binding, and SQL-script binding **from the app HMI front-end** — so the team has the experience of a real user and **finds every bug and error**. Hardcoding a page is "faking ourselves." Seeded rows are tagged `origin=seed` and never presented as live computation; learning jobs recompute on demo data so the buyer sees the **engine running, not a screenshot**.

### B3.2 · The dataset (scale & realism)
**≥100,000 rows** of real, realistic, advanced, deep, professional data that looks like a real backup and represents **≈1 month** of production. Realism math (from the compass): a real EAF plant makes ≈**18–24 heats/day** (avg **21**) → **≈630 heats/month**; at ≈**150–170 t/heat** (avg **160 t**) and ≈**18 t/coil** → **≈5,600 slabs/coils/month**. The plant is a **flat-steel plant**.

### B3.3 · The plant — equipment & areas (the imaginary customer)
1. **EAF** — Electric Arc Furnace — 1 EAF, 160 t/heat, **35–55 min**/heat.
2. **LF** — Ladle Furnace — **4 LF stations** with **11 ladles** rotating.
3. **CC** — Caster (continuous casting) — one **thin-slab, 2-strand** caster; casting speed avg **5 m/min** (**3.6–5.6** by thickness); avg slab thickness **56 mm**, width **1100–1650 mm**, slab **8–22 t**.
4. **TF** — Tunnel Furnace.
5. **HSM** — Hot Strip Mill.
6. **SKP** — Skin-Pass Mill.
7. **LCT** — Light Cut-to-Length.
8. **HCT** — Heavy Cut-to-Length.
9. **STL** — Slitting Machine.
10. **PKL** — Pickling Line.
11. **GVL** — Galvanizing Line.
12. **Yard** — **11 yards**: a cooling yard after HSM; one before and one after SKP (entry/exit); entry/exit yards for (LCT, HCT, STL); entry/exit yards for (PKL, GVL); and 4 yards for selling/buffering/stock.
13. **Roll Shop.**

### B3.4 · The eight source systems & their structures (build the dataset from these)
**1 · Melt Shop — PostgreSQL (EAF, LF).** Contains: **equipment & equipment counters** (furnace, refractory, ladle-furnace ID, electrodes; how many heats each ladle makes before maintenance); **Heats** (HeatID in EAF and LF) with — a **summary** table (steel grade, start/end time, crew ID, total electricity, total duration, total power-on time, …); a **Sample** table (Sample ID, sample time, HeatID, EAF or LF), a **Sample-Result** table (Sample ID, ComponentID, component result), and a **Component** table (ComponentID, chemical name); an **additives** table (per heat, with quantity); a **steps** table (EAF step ID and which parameters were done per step); and **parameter IDs** for those steps (electricity, oxygen, argon).
**2 · Caster — Oracle.** Links HeatID, LadleID, TundishID, MouldID, SequenceID (a sequence spans several heats), SlabID (cut at caster exit), StrandID (casting can produce multiple strands), and manages/links all of them. Tables: **Heat** (HeatID, crew ID, ladleID, tundishID, steel grade, mould, sequenceID, avg casting speed, avg mould width, start/end time); **Slab** (source heatID, steel grade, cut time, strandID, sequenceID, piece-head width, piece-tail, slab thickness, slab length); **Sequence** (start/end time, avg speed, crew ID); **Sample** (Sample ID, sample time, HeatID); **Sample-Result** (Sample ID, ComponentID, component result); **Component** (ComponentID, chemical name).
**3 · HSM — Oracle.** Continue with the same depth (rolling parameters, coil identity, genealogy from slab to coil).
**4 · PKL — MSSQL.**
**5 · Yard Management — Excel sheet.**
**6 · Downtime — MySQL.** Every crew records what happened: **Area** (EAF/LF/CC/HSM/…), **downtime duration**, **start/end time**, **production-stoppage time** (distinct from equipment stoppage — *not every equipment stoppage causes a production stoppage*: a 20-min HSM problem may be absorbed by a buffer slab in the TF and caster slow-down with **no** production stoppage; conversely a 3-min water-pump stop in the caster can stop the caster, force a new-sequence rebuild and metal removal from the cooling header, causing **4–6 h** of production stoppage), **equipment-stoppage time**, **reason** (electrical / operation / production / mechanical / hydraulic — where "operation" = bad driver, "production" = defect or bad temperature, e.g. a caster break-out from non-metallic inclusion, not operator error), and a **reason description**.
**7 · Surface-inspection device (Parsytec) — MySQL** at the HSM exit.
**8 · QA — Excel sheet.** QA samples **every 3rd coil** after HSM, and for important coils / difficult steel grades up to **2 samples per coil**.

### B3.5 · Acceptance for the demo
A **one-click readiness check** passes and a **recorded clean dry run** exists; the entire demo was built from empty through the HMI; the genealogy thread walks both directions on the customer's own keys (e.g. coil C-0044170); a transition coil reports weighted shared attribution.
**Gate G3/G7 · Track 3.**

---

## Track 4 — Website & Commercial

### B4.1 · License & entitlements
**Four tiers — Light / Pro / ProPlus / Enterprise** — each granting a different range of functionality, features, and user count.
**Must answer the buyer's exact questions:** what each tier *exactly* grants; how SOU (the seller) controls the tier; whether it is one-time or renewed monthly/yearly/for-a-duration with an expiry; and whether each tier limits the number of **users** or **DB/source connections**.
**How:** entitlements come **only** from an **Ed25519-signed, offline-verifiable token** (tenant, tier, seat_cap, source_cap, env_cap, issue/expiry dates, feature_flags, allowed_deployment_mode), verified against SOU's public key, working fully air-gapped — **never an editable DB row**, so an on-prem customer **cannot UPDATE their own tier**; a broken/absent signature yields a clear invalid-license state; **soft caps** warn-and-upsell by default, **hard caps** only where contracted; on expiry → clear warnings → configurable **grace (read-only)** → read-only of existing dashboards, **data never destroyed**.
**Demo proof:** toggling tier visibly adds/removes features (switching Enterprise→Pro makes some existing features disappear).
**Track 4 · §9.**

### B4.2 · User, role & admin governance
**Roles (the matrix):** Tenant Owner · Plant Admin · Data Engineer · Process/Quality/Reliability Engineer · Operator · Viewer/Executive · Commercial Admin · Support/Super-Admin (SOU — scoped, time-boxed, fully audited).
**Must answer the buyer's exact questions:** which users exist and each one's **privileges**; how to **add and manage** them; the **user-count limit** (enforced by the signed seat cap); **where passwords are stored and how** (Argon2id-hashed, never plaintext); whether the **same user** can hold **two concurrent sessions on two devices**; and what a second user sees when the first **modifies the same page at the same time** (an optimistic-concurrency conflict dialog, not a silent overwrite).
**Which user can configure/modify; which role sees which pages:** developer, process engineer, planner, and CEO each have a different scope of pages, KPIs, charts, running jobs, and edit rights.
**Track 4 · §10 · Gates G5/G12.**

### B4.3 · PPIQ's own database — two schemas
- **MetaData Schema** — configuration: dashboards, widgets, job configuration, front-end/pages, user/role, ….
- **Plant-Data Schema** — the customer data **after** conversion and adjustment in the staging-area plant files (cleaning, filtering, joining, linking) to match PPIQ's generic schema.

### B4.4 · The website & product ecosystem
The website is the customer's **first window** before any deeper engagement: it must be amazing, shiny, advanced, professional, carry the brand identity, hold all the information a buyer seeks, and have an amazing UI/UX. **Golden Rule:** the website is SOU's first and best marketing-and-salesperson, making any customer want to buy and request an inquiry. Positioning: SOU brings AI/ML learning to all of manufacturing and takes it to another level — the optimum solution that surfaces answers and insights a plant never knew; **not** ordinary management/planning/execution/quality, but a new AI-based generation.
**Five products, each with description, the benefit behind it, all info, interactive graphics and pictures, licensing, and full detail, each under the Golden Rule:**
1. **PlantProcess IQ** (the first product).
2. **MES** — Manufacturing Execution System.
3. **QES** — Quality Execution System.
4. **Yard & Warehouse Management.**
5. **Energy Management.**
**Track 4 · §15 · Gate G8.**

---

# PART C — Scoring Doctrine

## C1 · Per-criterion bands

Each criterion is scored **/100**:

| Band | Score | Meaning |
|------|-------|---------|
| **Critical** | < 55 | Missing, broken, or dishonest. A dead button on the demo path, a fabricated/uncited number, a forbidden commercial claim, a write-back path to control, or any live prior-audit security finding lands here **regardless of other strengths**. |
| **Needs work** | 55–69 | Present but incomplete, fragile, or inconsistent — works in the happy path, fails an edge state, a second browser, an induced fault, or a skeptical click. |
| **Solid** | 70–84 | Complete, stable, and honest for the demo scope; meets the gate's measurable exit on demo data. |
| **Strong** | 85+ | Production-grade beyond the demo: enumerated, automated-tested, documented, robust under adversarial use. |

Each scored criterion records four things — **Present** (what exists), **Why not lower** (the evidence that earns the floor), **Why not higher** (the specific named gap to the next band), and **Evidence** (file\:line or a reproducible run/command). **A criterion with no reproducible evidence cannot exceed *Needs work*.**

## C2 · Persona score & overall

A persona's score is the evidence-weighted mean of its criteria, with a **hard cap**: a **Critical** on any safety, honesty, dead-button, or read-only criterion caps the whole persona at **Needs work** until fixed. The six persona scores are reported **side by side and never averaged into one number**; the **headline is the lowest persona score**, because the build ships only when the developer, the security reviewer, the quality engineer, the operations admin, the executive, **and** the brand can each sign.

## C3 · Reconciliation to the Doctrine v7 gate ledger

Every persona criterion maps to one or more of the 16 gates; the review closes by scoring the gate ledger and confirming it agrees with the persona scores.

| Gate | Wave | The one proof |
|------|------|---------------|
| G1 Source integration | C | connectors pass behavioural tests; credentials never on read-back |
| G2 Mapping & genealogy | C | a coil resolves both directions; a bad mapping returns a typed error and rolls back |
| G3 Workflow (HMI) | C | the full demo builds from empty via HMI only — no hardcoding |
| G4 Intelligence | B | golden dataset recovers true signals + rejects spurious under FDR; assistant emits no uncited number |
| G5 Access & value | A/B | authorization matrix green by identity + tier; cross-tenant 403/empty; the euro range reproduces |
| G6 UI/UX | D | action matrix green by enumeration; visual-regression + cross-browser pass |
| G7 Demo | C | one-click readiness passes; recorded clean dry run exists |
| G8 Website | — | honesty-lint passes; CTA captures a lead; brand audit matches the token reference |
| G9 Operations | D | a clean machine reaches login by runbook only; restore verified *(26 Jun 2026: a clean server stack now reaches a working login automatically via the green pipeline — sysadmin auto-provisioned; restore drill still open)* |
| G10 Quality bar | — | every hardening row green; no "85%" on a gate item |
| G11 OT-safe acquisition | C | collector pushes one-way; no inbound path to OT; load within budget |
| G12 Identity & security | A | localStorage token retired; admin MFA enforced; dev-seed absent in prod *(26 Jun 2026: dev-seed/test-users removed from the production path and the dev license key demo-gated; bootstrap admin replaced by sysadmin; localStorage-token retirement + admin MFA still pending)* |
| G13 Data boundary & model gov. | B/D | self-hosted leaks nothing; private endpoint sends only question + scoped evidence |
| G14 Accessibility & i18n | D | WCAG AA audit passes; Arabic RTL renders; units/timezone per user |
| G15 Coverage & honesty | C | every finding shows population & exclusions; a transition coil reports weighted attribution |
| G16 Compliance | D | SDLC controls evidenced; GxP audit-trail + e-signature where required |

**Baseline & ceiling:** the build sat at **≈46/100** against this bar at v3; as of **26 Jun 2026** a slice of **Wave A** has closed (per-environment signing keys; dev-seed/test-users removed from the production path; the dev license key demo-gated; bootstrap admin replaced by the permanent `sysadmin`; a green end-to-end deploy pipeline with in-place deploy, health gate, and rollback; the demo UI loginable), moving the baseline to **≈50–52/100**, with the **projected ceiling still ≈84/100** when fully built. **The headline persona score does NOT move on this** — the headline is gated on the Process/Quality Engineer (A3), which depends on the **value engine + live HMI signal** (Wave B), still unbuilt. A green *pipeline* is infrastructure, not product value. A wide doctrine-to-build gap is acceptable **because the route across it is written** — Wave A (security) → B (value/L4 stats) → C (acquisition/demo) → D (experience/compliance). The reviewer confirms each scored point traces to a gate, a wave, and reproducible evidence.

## C4 · The reviewer's standing rules (binding)

1. **No score without a live demonstration** through the HMI on the demo dataset. A claim in a document, a comment, or a conversation is worth zero.
2. **Honesty outranks capability.** An honest "collecting data" beats a confident wrong answer; any forbidden commercial claim (Appendix-E list) is Critical even on an otherwise strong surface; any uncited number from the assistant is Critical.
3. **The demo path is sacred.** Every button on the path works, or the path is not demo-ready — the no-"85%" rule (a gate item is 95–100% or it is not done).
4. **Read-only and OT-safety are absolute.** Any write-back path to a control system is an automatic Critical.
5. **Evidence is mandatory.** Every band above *Needs work* carries file\:line or a reproducible run/command; "looks done" is not evidence.
6. **Induce the fault.** Where a criterion concerns failure handling (a 500, a schema change, a bad join, a concurrent edit, a tier toggle, an expired license), the reviewer **induces the condition** and observes the exact specified behavior — never reads a claim.
7. **Score the lowest persona.** The product ships when all six reviewers can sign — not on average, but each.

## C5 · The surprise-question gate (the demo must survive these live)

A skeptical buyer will ask tricky questions; the demo is not ready until each is answered **honestly, on the build**. These are scored as part of personas A3/A5 (engineer/executive) and A2 (security).

**General**
- Describe briefly what the app is and why I should buy it.
- What is the app's workflow?
- Does the first-time configurator need strong database/programming experience? *(For the common case, no — the no-code mapper + templates; safe-SQL only for the long tail.)*
- Can I later add/remove/modify a page, widget, chart, job, or AI binding, and bind a widget to a specific query result, **easily from my HMI** — or only from source code? *(From the HMI.)*
- How much data do I need before your AI gives a mature answer? *(Simple dashboards/KPIs work day one; advanced findings arrive as readiness gates turn ready; a backfill collapses the timeline.)*

**License**
- What license types exist and what **exactly** does each grant?
- How do you (the seller) control the license type? *(A signed token, not an editable row.)*
- One-time purchase or renewed monthly/yearly/for-a-duration with an expiry?
- Does each license limit the number of users or the number of DB/source connections? *(Yes — enforced by the signed seat/source caps.)*

**User / Role / Admin**
- What user types exist and each one's privileges?
- How do I add and manage them?
- What is the user-count limit?
- Where are user passwords stored and how? *(Argon2id-hashed, never plaintext.)*
- Can the same user have two concurrent sessions on two devices?
- If two users open the same page and one modifies it, what does the other see? *(An optimistic-concurrency conflict dialog, not a silent overwrite.)*
- Do you ship a customer-usable admin inside the install image? *(No — the automated install provisions only the permanent `sysadmin` support account, which SOU uses for support and the customer never sees; the customer's own admin is created manually at commissioning. So nothing customer-facing is baked into the image.)*

**AI / ML**
- Does the system work on AI or ML?
- Is all analysis AI, or simple math? *(Deterministic engines compute; the assistant only explains.)*
- Does the AI assistant do all the analysis, or only suggestions/recommendations? *(Only explanation/suggestion; engines do the math.)*
- Describe the AI engine's workflow.
- Do you own the engine, or are you linked to GPT/Claude — sending data out and getting results back? *(Engines run in-tenant; the assistant model is self-hosted by default, or a zero-retention private endpoint receiving only the question + scoped evidence.)*
- An AI engine needs heavy resources and big data — not any laptop can handle it; where does this app run? *(Defined deployment topologies/sizing; engines are deterministic, not a giant LLM doing arithmetic.)*
- GPT-3 (~175B params) erred often; GPT-4 (~1.76T) is better — how can I depend on your assistant? *(Because the assistant does not compute or rank; deterministic engines do, and every assistant claim carries a resolvable evidence handle or is not rendered.)*

---

*Reconcile every assessment against Doctrine v7 — 16 gates · 4 waves · 4 tracks. Freeze the doctrine; move the delta. The doctrine is the target; the delta is the truth. No fake high score survives Part C. (v4 · 26 Jun 2026: baseline moved by the Wave-A deploy/identity slice; headline held pending the value engine.)*
