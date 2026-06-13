# PlantProcess IQ — Aspects of Review & Evaluation Framework

**Document type:** Independent assessment rubric for the PPIQ implementation
**Aligned to:** Doctrine v7.0 (16 gates · 4 waves · 4 tracks · honesty contract · 84-point ceiling)
**Audience:** Any reviewer handed the build — internal team lead, prospective engineering buyer, executive sponsor, or external auditor
**Purpose:** Define *who* evaluates PPIQ, *what each evaluator checks*, *what each is expected to find*, and *how a finding is scored* — so an assessment is reproducible, evidence-based, and traceable to the doctrine rather than to opinion.

---

## 0 · How to use this document

This is not a status report and not a task list. It is the **measuring instrument**. When the build is handed to a reviewer, this document tells them exactly what to inspect and what "good" looks like, from several independent points of view at once.

The framework has three layers:

1. **Evaluation personas (Part A).** PPIQ is judged from *six* professional vantage points, not one. Each persona carries the concerns of a real stakeholder who would, in real life, decide whether this software is trustworthy, buyable, or shippable. Each persona scores against **8–10 explicit criteria**.
2. **Capability expectations (Part B).** For every feature, workflow, and job, the document states *what it must do*, *how it must do it*, and *what the reviewer should find when it works correctly*. This is the objective backbone the personas score against.
3. **Scoring model (Part C).** A single, consistent rubric — per-criterion bands, evidence requirements, and reconciliation to the Doctrine v7 gate ledger — so the four persona scores and the doctrine score describe the same reality.

**The honesty contract governs the whole review.** Per Doctrine v7 §2 and the ten operating principles (§17), PPIQ's integrity *is* its strongest sales asset. A reviewer must therefore reward restraint ("suspected contributor, here is the evidence") and penalize over-claiming ("guaranteed root cause") — a confident wrong answer scores *below* an honest "collecting data." No score is awarded for a behavior that cannot be demonstrated live through the HMI on demo data.

---

# PART A — The Evaluation Personas

PPIQ must satisfy six reviewers, each asking different questions. A build that delights one and fails another is not ready. The six are grouped into the two families the doctrine cares about: **the people who build and ship it** (Developer, Security/IT Reviewer) and **the people who buy and use it** (Process/Quality Engineer, Reliability/Operations, Executive Sponsor, Commercial/Brand). Each persona below lists its criteria, what it expects to find, and the doctrine gates it implicitly enforces.

---

## A1 · The Developer / Maintainer

*"Can I work in this codebase a year from now without it fighting me — and is it honest about what it actually does?"*

| # | Criterion | What the reviewer expects to find | Doctrine anchor |
|---|-----------|-----------------------------------|-----------------|
| 1 | **Code hygiene & clean code** | Consistent style, no dead code, no commented-out blocks left as tombstones, no copy-paste duplication of logic that should have one home. | §17 P1, G10 |
| 2 | **Stability under change** | The build compiles clean; the full test suite is green; no flaky tests; no "works on my machine" config drift. | G10 |
| 3 | **Repo cleanliness & structure** | One canonical layout (`Domain / Application / Analytics.Core / Analytics.Engine / Infrastructure / Workers / Api`), no orphaned backup folders, no duplicate Jenkinsfiles or compose files, no unregistered test projects. | §3 |
| 4 | **Representative naming** | Classes, files, and namespaces name what they are; partial files follow one convention; no `*.runtime.cs` tombstones masquerading as refactors. | §3 |
| 5 | **Test architecture (unit/integration/e2e)** | Real assertions, not enumeration. Backend `dotnet test`, frontend `vitest`, and Playwright `e2e` all execute and *block* the pipeline. Skips are registered and justified, never used to hide a regression. | G10, Track 2 |
| 6 | **Generic / any-plant / any-DB** | No hardcoded plant, product, routing, schema, or domain. The same binary serves MSSQL / PostgreSQL / MySQL / Oracle / Excel / CSV sources via configuration, not code edits. | §4, §5, G1 |
| 7 | **Extensibility without fragility** | Adding a connector, a KPI, a widget, or a job is an additive change behind a stable seam (`IConnector`, mapping views, `LicenseFeature`), not a shotgun edit across the tree. | §3, §4.7, §12 |
| 8 | **Structural consistency** | The same patterns repeat everywhere — one error-boundary primitive, one save-hook pattern, one entitlement resolver, one validation contract. A reviewer can predict the shape of code they haven't read yet. | §3, §8 |
| 9 | **Pipeline & deployment honesty** | CI runs **only** the agreed sequence: `dotnet test → npm test → npm e2e → migrate (app + demo DBs) → seed → deploy` — tests blocking, nothing faked, deploy unreachable while any suite is red. One canonical compose stack; no orphaned parallel deployments. | §16, G9 |
| 10 | **Switchable test/run profiles** | A developer can exercise every auth/license combination locally and on the server **without code edits or DB surgery** — seeded role users and a forced-tier switch, refused in Production unless explicitly accepted. | §9.2, §10, G5/G12 |

**What "lower" looks like:** green deploys sitting on a red test suite; a refactor "closed" by renaming a file into a shim; a feature that only works because a page was hardcoded; a connection target compiled into the binary.

---

## A2 · The Security & IT Reviewer (procurement gate)

*"Can my automation team and my DBA approve this without a control-systems risk review or a data-egress concern?"*

This persona is implied throughout your draft (the auth layers, where passwords are stored, two-session access) but deserves its own seat because in a real plant sale, **IT and OT security hold a veto**. Doctrine v7 makes this explicit (§4.6, §10.2, §18) and it is the entire content of Wave A.

| # | Criterion | What the reviewer expects to find | Doctrine anchor |
|---|-----------|-----------------------------------|-----------------|
| 1 | **Read-only is absolute** | No code path writes a setpoint, recipe, or command to any source or control system. Outbound is only message / export / webhook. | §2.1, §17 P5 |
| 2 | **OT-safe acquisition** | Sources are reached through a customer-controlled Edge Collector that pushes one-way; the core never initiates a connection into the OT network; a data-diode option exists. | §4.6, G11 |
| 3 | **Source-load protection** | Per-source row caps, statement timeouts, rate limits, and approved windows; backfill is throttled, resumable, checkpointed — never one giant query against the primary. | §4.6, §4.8 |
| 4 | **Token & session security** | Access token in memory + HttpOnly refresh cookie with rotation/revocation — **not** browser localStorage. Argon2id password hashing. MFA enforced for admins. | §10.2, G12 |
| 5 | **Secrets handling** | Source credentials live only in the collector's encrypted vault, masked on read-back, never in the browser or app config. No hardcoded signing key; per-environment signed keys. | §4.6, §18, G12 |
| 6 | **Tenant isolation** | SaaS uses TenantId + row-level security; dedicated/on-prem is physically isolated — one resolver, one rule set. Cross-tenant access returns 403/empty, proven by test. | §3, §10, G5 |
| 7 | **Per-endpoint authorization** | Every endpoint, page, job, and tool checks role + entitlement. No unguarded admin surface; no dev-seed or diagnostic endpoint reachable in a production build. | §10.1, G5/G12 |
| 8 | **Data boundary for the AI** | Engines compute in-tenant; the assistant model is self-hosted by default, or a zero-retention private endpoint receiving only the question + scoped evidence; per-tenant no-egress toggle. | §7.8, G13 |
| 9 | **Audit & encryption at rest** | An append-only audit log on sensitive actions; encryption at rest; immutable audit records. | §18, G12/G16 |
| 10 | **Deployment hardening** | No publicly exposed database port; bootstrap admin replaced; health/readiness endpoints; documented runbooks. | §16, G9 |

**What "lower" looks like:** any of the prior-audit findings still live — localStorage token, hardcoded JWT key, unconditional dev-seed in prod, an exposed PostgreSQL port, a bootstrap admin still active.

---

## A3 · The Process / Quality Engineer (primary daily user)

*"Will this help me investigate a quality problem faster than my spreadsheets — and can I trust what it tells me?"*

This is your "Engineer customer," sharpened. This persona *uses the product every day* and is the skeptical engineer the no-"85%" rule is written to survive.

| # | Criterion | What the reviewer expects to find | Doctrine anchor |
|---|-----------|-----------------------------------|-----------------|
| 1 | **Feature stability — zero dead buttons** | Every button on every path performs its function: search, load, calculate risk, generate report, min/max, filter. Nothing "could not load" then works on retry. | §13, G6 |
| 2 | **Workflow smoothness end-to-end** | The full lifecycle flows without a crash or a stall: link a source → stage → map → build a page → configure widgets → run a job → read the dashboard → export. | §1, G3 |
| 3 | **UI/UX clarity & stability** | Coherent information architecture; consistent component styling everywhere (the Material-Investigation button-mismatch class of defect is gone, standardized for all current *and future* surfaces). | §8.1, §8.2 |
| 4 | **Widget customization for non-developers** | A drag-drop builder; widgets bind to data without writing an endpoint; a script layer (e.g. "group this bar chart by shift") handles the long tail. No source-code edit to make a chart. | §12, P1 |
| 5 | **Genealogy golden thread** | One navigable click from a surface defect on a finished coil back to the melt chemistry — on the customer's *own* key names, both directions, across all eight sources. | §5.1, G2 |
| 6 | **Honest analysis — population always stated** | Every analysis shows its population and exclusions ("22 of 60 heats needed"); no silent survivorship; a "collecting data" state instead of a fabricated answer. | §17 P6, G15 |
| 7 | **Correlation & ML jobs** | Learning jobs (parameter↔defect, ↔downtime, ↔KPI) run on a schedule or on demand, recompute on demo data, and surface which parameters most influence an outcome — under statistical discipline (FDR, stratification), framed as *suspected contributors*. | §7.3, §7.4, G4/G5 |
| 8 | **Blended provenance correctness** | A transition coil reports weighted shared attribution between two heats (e.g. 70/30), never a fabricated single-heat answer. | §5.3, G15 |
| 9 | **Performance under real data** | No hang or lag; large datasets show a loading state with progress; widgets virtualize; interactions stay responsive at demo scale (630 heats / 5,600 coils / 1 month). | §8.5 |
| 10 | **Interactivity** | Widgets and charts are dynamic and respond to filtering and sorting; heatmaps render and interact correctly; drag/move/min/max behave. | §8, §12 |

**What "lower" looks like:** a chart that needs a page reload to render; an analysis that states a driver without stating the population; an AI sentence carrying a number it cannot cite.

---

## A4 · The Reliability / Operations & Plant-Admin User

*"Does it keep running, tell me when something breaks, and let me configure the plant without calling the vendor?"*

A second buyer-side persona, distinct from the quality engineer: this is the person who **onboards the data, owns the jobs, and keeps the lights on**. Your draft's job-monitoring and admin-configuration requirements live here.

| # | Criterion | What the reviewer expects to find | Doctrine anchor |
|---|-----------|-----------------------------------|-----------------|
| 1 | **Source onboarding from the HMI** | Create a DB/file/historian link, pick a provider, test connectivity *before* save, mask credentials, choose objects and sync cadence (2 min → days) — all from the admin UI. | §4.1, G1 |
| 2 | **Mapping & schema configuration** | Author views/joins to fit the generic schema; reconcile mismatched keys (C-0044170 ≡ 44170); define a KPI as a versioned SQL view — tiered so the common case needs no SQL. | §4.2–§4.5, G2 |
| 3 | **Import jobs & delta logic** | Each sync is a named job with cycle and status; a delta cursor imports only new rows by comparing watermarks; every run writes an import batch (rows, duration, watermark, errors). | §4.1, §11 |
| 4 | **Jobs Monitor** | Every job (import, correlation, ML, demo-reset) shows last-run time, outcome (ok/crash/timeout), duration, and source-impact — in one place. | §11, G9 |
| 5 | **Schema-drift detection** | A new/renamed/removed column raises a typed Schema-Change event, flags dependent mapping views, and can pause dependent imports rather than produce wrong facts. A Mapping Health panel shows green/degraded/broken and *why*. | §4.9, G2 |
| 6 | **Fail loudly & specifically** | A broken join returns `NoSuchColumn` / `AmbiguousJoinKey` precisely, with the affected view and the next safe step — never a silent wrong number. | §17 P7, §4.3 |
| 7 | **Readiness meter** | The product never shows nothing while a gate is blocked: simple analysis plus an honest countdown ("X of Y heats needed"); a backfill collapses the timeline. | §6.3, G15 |
| 8 | **Backfill & source protection** | Historical load is throttled, idempotent, watermark-tracked, pausable/resumable, visible in the Jobs Monitor, and honours the source-impact budget. | §4.8, G11 |
| 9 | **Operational resilience** | A clean machine reaches a working login by runbook only; backup/restore is drilled; health/readiness endpoints exist; a failed deploy rolls back. | §16, G9 |
| 10 | **Concurrency & collaboration** | If two users edit the same page and one saves, the other sees an optimistic-concurrency conflict dialog — not a silent overwrite; draft/publish immutability holds. | §8.9, G6 |

**What "lower" looks like:** a job that fails without surfacing why; a schema change that silently corrupts a mapping; a deploy with no rollback; a "last write wins" overwrite with no conflict warning.

---

## A5 · The Executive Sponsor (the economic buyer)

*"Will this pay back more than it costs, can I trust its recommendations, and does each role see the right scope?"*

Your "CEO customer," kept whole. This persona signs the purchase order and asks about value, trust, and price-to-value parity.

| # | Criterion | What the reviewer expects to find | Doctrine anchor |
|---|-----------|-----------------------------------|-----------------|
| 1 | **Quantified value** | A value engine that converts findings into a **bounded euro range** with an abstain path, computed on the customer's own pilot data, every input drill-throughable (the §7.4 finding → €28k–€56k reproduces on demo data). | §7.5, §19.1, G5 |
| 2 | **Role-scoped experience** | An executive sees KPIs, ROI, and trends; an engineer sees investigation tools; an admin sees configuration — each role a different scope of *view* and a different scope of *edit*. | §10.1, G5 |
| 3 | **License tiers demonstrable** | Four tiers (Light/Pro/ProPlus/Enterprise); toggling tier visibly adds or removes features live — entitlements from a signed token, not an editable row. | §9.1, §9.4 |
| 4 | **Trust in AI output** | Deterministic engines compute and rank; the assistant only *explains* with citations and cannot render an uncited number. The buyer can audit every claim to its evidence. | §7, §17 P2/P3, G4 |
| 5 | **Honest boundary as an asset** | The product says "suspected contributor," states what it does not know, and never claims guaranteed root cause — the reason a skeptical engineer recommends it upward. | §2, §17 P4/P10 |
| 6 | **Speed of insight** | Dashboards and KPIs work day one; advanced findings arrive as readiness gates turn ready; nothing makes the executive wait on a spinner with no progress. | §6.3, §8.5 |
| 7 | **Price-to-value parity** | The demonstrated value (loss reduction, quality lift, downtime avoided) is credibly *greater* than the tier price — otherwise the deal does not close. | §19, Track 4 |
| 8 | **Cross-device / cross-browser** | The product renders correctly at any screen size, on any major browser, over http/https, and re-flows properly. | §8.6–§8.8, G6 |
| 9 | **Competitive distinctiveness** | Clearly *not* "another BI dashboard": it carries manufacturing semantics — genealogy, defect-drivers, value, suggestions — that BI tools do not. | §19.2 |
| 10 | **Brand & trust posture** | The product and its collateral look like "plant operations + data science" — calm, industrial, evidence-based — never flashy consumer AI. | §15, Track 4 |

**What "lower" looks like:** value asserted but not computed on real data; an AI recommendation the buyer cannot trace; a tier toggle that does nothing visible; demonstrated value that does not clear the price.

---

## A6 · The Brand & Website Reviewer (the first impression)

*"Before anyone logs in, does the website make a serious buyer want to request a demo — and does the brand hold together across every surface?"*

Your Part-Two "App + Website Identity" and Part-Five website track, consolidated. The website is the **first and best salesperson**; the brand must be identical across website, app, and reports.

| # | Criterion | What the reviewer expects to find | Doctrine anchor |
|---|-----------|-----------------------------------|-----------------|
| 1 | **Brand-name, tagline & voice consistency** | Product "PlantProcess IQ / SOU," short "PPIQ," primary tagline *"Connect Your Plant Data. Understand Your Process."* used consistently; the approved voice (industrial, evidence-based) everywhere. | §15, App. C/E |
| 2 | **Forbidden vs approved claims (honesty-lint)** | Nowhere says "guaranteed root cause," "AI-powered prediction," "live Oracle ready today," or "we replace MES/L2/SCADA/BI." Uses "rule-based risk scoring," "correlation," "suspected contributor," "read-only intelligence layer." | App. E, §2 |
| 3 | **Color palette fidelity** | The Dark Industrial Command Center palette (`#050B18`, `#0B1730`, `#0A84FF`, `#00D4FF`, status colors) applied exactly, no off-palette colors. | §15, App. C |
| 4 | **Typography** | Inter for UI, JetBrains Mono for code/SQL, minimum body sizes honored (14px web / 12pt PDF), consistent heading scale. | App. C |
| 5 | **Logo system** | Connected-node mark (industry-neutral, not steel-only); full/icon/stacked variants in color/dark/light/mono; never rotated, recolored, stretched, or boxed. | §15 |
| 6 | **Website UX & responsiveness** | Advanced, professional, shiny; works on any screen and browser; clear navigation; fast. | §15, G8 |
| 7 | **Product ecosystem narrative** | Core PPIQ plus the pack story (MES, QES, Yard/Warehouse, Energy) — each with benefit, visuals, and licensing, all under the Golden Rule (drive an inquiry). | §15.1, Track 4 |
| 8 | **Proof, pricing & CTA** | Credible proof points, transparent tiering, and a working call-to-action that *captures a lead*. | §15, G8 |
| 9 | **In-app brand implementation** | The app embodies the same identity — backgrounds, nav, sidebar, cards, buttons, chart colors, status badges, tier badge — no white-space leakage on dark surfaces. | §15 (In-App), App. C |
| 10 | **Report/PDF brand** | Deliverables switch to the light-mode surface with brand header/footer maintained. | App. C |

**What "lower" looks like:** a single forbidden claim on a page; an off-palette color; tagline drift between website and app; a CTA that goes nowhere.

---

# PART B — Capability Expectations (the objective backbone)

The personas score against this. For each capability the table states **what it must do** and **how**, plus the **evidence a reviewer should find**. These are organized on the V1 four-track skeleton the doctrine preserves (§23).

## B0 · The product in one line

PPIQ is a **generic, read-only, evidence-grade plant-intelligence platform**: each plant connects its own sources, defines its own mappings, builds its own pages, monitors its own jobs, and investigates its own quality / downtime / KPI questions — carried from fragmented raw data to trustworthy understanding, **never asked to take a black box on faith**.

---

## Track 1 — Workflow & Product (the journey)

### B1.1 · Generic source integration

**Must do:** connect to MSSQL, PostgreSQL, MySQL, Oracle, Excel, and CSV without code changes.
**How:** an admin links a source, picks a provider, the system tests connectivity *before* save, masks credentials on read-back, stores secrets encrypted, profiles each object (row count, PK candidate, timestamp candidate, nullables, types, sample), and snapshots the schema for drift.
**Reviewer should find:** the eight demo sources (PostgreSQL melt shop, two Oracle, MSSQL, two MySQL, two CSV) all linkable through the HMI; a connector catalog that declares **honest availability per source version** — a connector designed-but-not-certified says so, rather than claiming "live Oracle ready today."
**Gate:** G1 · **Track:** 1

### B1.2 · The staging / dump layer

**Must do:** hold the latest source-shaped copy of each customer object, not PPIQ's own structure.
**How:** namespaced tenant/source staging objects; a delta cursor (numeric index, timestamp, composite key, or full-snapshot) imports only new rows by comparing the staging watermark against the source; every sync writes an import batch (rows read/inserted, duration, watermark, errors, audit).
**Reviewer should find:** re-running a sync imports only the delta; staging stays source-shaped; canonical transformation happens *later*, in mapping.
**Gate:** G1

### B1.3 · The mapping & joining engine — *the heart*

**Must do:** turn many foreign keys into one canonical fact, generically.
**How:** a **layered join model** starting from business keys, strengthened through normalization, mapping views, genealogy, and a confidence score; a business-key dictionary reconciles mismatched IDs explicitly (HSM Oracle "C-0044170" ≡ Parsytec MySQL "44170"); a mapping-authored **safe-SQL view** (SafeSqlValidator: read-only, bounded, `EXPLAIN`-checked, typed errors) produces canonical `quality_event` facts by walking slab → coil → defect; KPIs are **first-class versioned SQL views**, not hardcoded metrics.
**Authoring is tiered (the answer to "must my configurator be a programmer?"):**

| Tier | Offers | Who |
|------|--------|-----|
| No-code visual mapper | drag-drop fields, point-and-click joins on detected keys, KPI/industry templates — the common majority, zero SQL | process/quality engineer |
| Assisted | profiling suggests keys/joins with confidence; assistant drafts a view from plain language for a human to review (execution stays deterministic) | engineer + reviewer |
| Expert SQL | the safe-SQL view builder for the complex long tail | data engineer |

**Reviewer should find:** a coil resolves **both** genealogy directions on demo keys; a *bad* mapping returns a typed error (`NoSuchColumn`/`AmbiguousJoinKey`) and rolls back — never a silent wrong fact.
**Gate:** G2 · **Track:** 1

### B1.4 · Page & widget building (config-from-HMI)

**Must do:** let a user create/modify/delete pages and place widgets with no source edit.
**How:** a page builder with a widget library (charts, KPIs, date/time and list filters); drag-drop to compose, then save; each widget has an edit affordance to bind to data; a **declarative script layer** handles transforms (e.g. "group this bar chart by shift") so the product does not need an endpoint per chart.
**Reviewer should find:** the **entire demo builds from empty through the HMI** — no hardcoded page or widget (Principle 1; the most honest test of G3).
**Gate:** G3 · **Track:** 1

### B1.5 · Jobs, scheduling & operational control

**Must do:** run and monitor every recurring task.
**How:** import jobs, correlation/ML learning jobs, and demo-reset jobs, each named, scheduled or on-demand, with cycle and status; a Jobs Monitor showing last-run time, outcome, duration, and source-impact.
**Reviewer should find:** a crashed/timed-out job surfaces *why*; learning jobs recompute on demo data (anti-facade: seeded rows tagged `origin=seed`, never shown as live computation).
**Gate:** G3/G7 · **Track:** 1

---

## Track 1 (Intelligence) — the engine

### B1.6 · Correlation engine & L4 statistics

**Must do:** find which process parameters are statistically associated with defects, downtime, and KPIs.
**How:** the §7.3 method table — **Spearman, mutual information, Lasso, VIF, bootstrap** — under **Benjamini-Hochberg FDR** control and **stratification** by visible confounders; framed always as *suspected contributors*.
**Reviewer should find:** on a golden dataset, the engine recovers true signals **and rejects spurious ones under FDR**; every finding states its population (Principle 6).
**Gate:** G4/G5 · **Track:** 1

### B1.7 · Value engine

**Must do:** convert a finding into money, honestly.
**How:** the §7.5 formula with a per-tenant cost table and an **abstain path** when inputs are insufficient; downtime modeled as *two distinct quantities* (equipment-stopped minutes vs production impact) and the right one used.
**Reviewer should find:** the §7.4 finding reproduces a **bounded €28k–€56k range** on demo data, every input drill-throughable; the engine abstains rather than inventing a number when it cannot compute one. *(This is the single largest doctrine-to-build gap — score it strictly.)*
**Gate:** G5 · **Track:** 1

### B1.8 · Suggestion engine & assistant

**Must do:** explain findings and suggest actions without fabricating.
**How:** a **deterministic** suggestion engine; the assistant assembles answers from tool results and retrieval only; a **no-fabrication guard** rejects any sentence containing a number without a resolvable **evidence handle** *before display*; an evaluation harness gates regressions.
**Reviewer should find:** the assistant emits **no uncited number**; asked "which heat caused this defect" on a transition coil, it answers with weighted shared attribution, not a fabricated single heat.
**Gate:** G4 · **Track:** 1

---

## Track 2 — Hardening (the quality bar)

### B2.1 · The Zero-Defect Quality Bar

**Must do:** guarantee that on the demo path, every action works.
**How:** the **no-dead-button rule** enumerated against every page and action; the **five edge states** (loading, empty, error, partial, success) on every data surface; **no-"85%"** — a gate item is 95–100% or the path is not demo-ready; standardized component styling across all current *and future* surfaces (buttons, tables, tabs, layout, drag/min/max).
**Reviewer should find:** an action matrix green by enumeration; visual-regression and cross-browser pass; no "could not load … works on retry."
**Gate:** G6/G10 · **Track:** 2

### B2.2 · Security & identity posture *(see persona A2 for the full criteria)*
**Gate:** G12/G13 · **Track:** 2

### B2.3 · Deployment, operations & SLA *(see persona A4 criteria 8–9)*
**Gate:** G9 · **Track:** 2

---

## Track 3 — Demo (proving the engine)

### B3.1 · The demo doctrine & Golden Rules

**Must do:** prove the *real* workflow, not a screenshot.
**How — the Golden Rules (preserved verbatim from the compass):** the demo is **not a separate app or an extra layer** — it is the emulation of an imaginary customer's data sources. **Nothing is hardcoded.** Every DB-link, job, page, widget, and SQL binding is created and saved **from the app HMI**, so the demo team faces every bug a real user would. Hardcoding a page is "faking ourselves."
**The dataset:** ≥100k rows of realistic, deep, ~1-month flat-steel production — **≈630 heats, ≈5,600 coils**, ≈160 t/heat, ≈18 t/coil, caster 3.6–5.6 m/min, 4 LF stations, 11 ladles, QA sampling every 3rd coil (up to 2/coil for difficult grades), thirteen plant areas (EAF, LF, CC, TF, HSM, SKP, two cooling/coil lines, slitting, PKL, GVL, yards, roll shop), with **deliberate imperfection** so the data looks real.
**The eight sources (Appendix A):** melt-shop PostgreSQL (EAF/LF), caster Oracle, HSM Oracle, PKL MSSQL, two MySQL (Parsytec surface inspection + downtime), and two Excel/CSV (QA samples + yard).
**Reviewer should find:** a **one-click readiness check** passes and a **recorded clean dry run** exists; the demo was built from empty through the HMI.
**Gate:** G3/G7 · **Track:** 3

---

## Track 4 — Website & Commercial *(see personas A5 and A6 for the full criteria)*

### B4.1 · License & entitlements

**Must do:** sell four tiers safely.
**How:** entitlements come **only** from an **Ed25519-signed, offline-verifiable token** (tenant, tier, seat_cap, source_cap, env_cap, expiry, feature_flags, deployment_mode) — never an editable DB row, so an on-prem customer **cannot UPDATE their own tier**; soft caps warn-and-upsell by default, hard caps only where contracted; expiry → clear warnings → grace (read-only) → read-only of existing dashboards, data never destroyed.
**Reviewer should find:** toggling tier visibly changes available features; a broken/absent signature yields a clear invalid-license state.
**Gate:** §9 · **Track:** 4

### B4.2 · User, role & tenant governance

**Roles (the matrix):** Tenant Owner · Plant Admin · Data Engineer · Process/Quality/Reliability Engineer · Operator · Viewer/Executive · Commercial Admin · Support/Super-Admin (SOU, scoped, time-boxed, audited).
**Must answer the buyer's exact questions:** what each role may do; how users are added/managed; seat limits (enforced by the signed cap); where passwords live (Argon2id-hashed, never plaintext); whether one user may hold two concurrent sessions on two devices; and what a second user sees when the first edits the same page (optimistic-concurrency conflict dialog).
**Gate:** G5/G12 · **Track:** 4

---

# PART C — The Scoring Model

## C1 · Per-criterion bands

Each of the 8–10 criteria per persona is scored **/100**, using consistent bands:

| Band | Score | Meaning |
|------|-------|---------|
| **Critical** | < 55 | Missing, broken, or dishonest. A dead button on the demo path, a fabricated number, a forbidden claim, or a live prior-audit security finding lands here regardless of other strengths. |
| **Needs work** | 55–69 | Present but incomplete, fragile, or inconsistent. Works in the happy path but fails an edge state, a second browser, or a skeptical click. |
| **Solid** | 70–84 | Complete, stable, and honest for the demo scope. Meets the gate's measurable exit on demo data. |
| **Strong** | 85+ | Production-grade beyond the demo: enumerated, tested, documented, and robust under adversarial use. |

Each scored criterion is recorded with three lines — **Present** (what exists), **Why not lower** (the evidence that earns the floor), **Why not higher** (the specific gap to the next band) — plus **file\:line or run evidence**. A criterion with no demonstrable evidence cannot exceed *Needs work*.

## C2 · Persona score & overall

A persona's score is the evidence-weighted mean of its criteria (a **Critical** on any safety, honesty, or dead-button criterion caps the persona at **Needs work** until fixed). The six persona scores are reported side by side — they are *not* averaged into a single number that hides a failing dimension. The headline is the **lowest persona score**, because the build ships only when every reviewer can sign.

## C3 · Reconciliation to the Doctrine v7 gate ledger

The persona view and the doctrine view must describe the same reality. Every persona criterion maps to one or more of the **16 gates**; the review closes by scoring the gate ledger and confirming it agrees with the persona scores.

| Gate | Closes in wave | The one thing that proves it |
|------|----------------|------------------------------|
| G1 Source integration | C | connectors pass behavioural tests; credentials never on read-back |
| G2 Mapping & genealogy | C | a coil resolves both directions; a bad mapping returns a typed error and rolls back |
| G3 Workflow (HMI) | C | the full demo builds from empty via HMI only — no hardcoding |
| G4 Intelligence | B | golden dataset recovers true signals + rejects spurious under FDR; no uncited number |
| G5 Access & value | A/B | authorization matrix green by identity+tier; cross-tenant 403/empty; €28k–€56k reproduces |
| G6 UI/UX | D | action matrix green by enumeration; visual-regression + cross-browser pass |
| G7 Demo | C | one-click readiness passes; recorded clean dry run exists |
| G8 Website | — | honesty-lint passes; CTA captures a lead; brand audit matches Appendix C |
| G9 Operations | D | a clean machine reaches login by runbook only; restore verified |
| G10 Quality bar | — | every §13 row green; no "85%" on a gate item |
| G11 OT-safe acquisition | C | collector pushes one-way; no inbound path to OT; load within budget |
| G12 Identity & security | A | localStorage token retired; admin MFA enforced; dev-seed absent in prod |
| G13 Data boundary & model gov. | B/D | self-hosted leaks nothing; private endpoint sends only question + scoped evidence |
| G14 Accessibility & i18n | D | WCAG AA audit passes; Arabic RTL renders; units/timezone per user |
| G15 Coverage & honesty | C | every finding shows population & exclusions; transition coil reports weighted attribution |
| G16 Compliance | D | SDLC controls evidenced; GxP audit-trail + e-signature where required |

**Baseline & ceiling.** Per Doctrine v7 Part II, the current build sits at **≈46/100** against this bar, with a **projected ceiling of ≈84/100** when fully built. A wide doctrine-to-build gap is *acceptable* — the four waves (A security → B value → C acquisition/demo → D experience/compliance) are the written route across it. The reviewer's job is not to punish the gap but to confirm each scored point traces to a gate, a wave, and demonstrable evidence.

## C4 · The reviewer's standing rules

1. **No score without a live demonstration** through the HMI on demo data. A claim in a doc is worth zero.
2. **Honesty outranks capability.** An honest "collecting data" beats a confident wrong answer; a forbidden claim is Critical even on an otherwise strong page.
3. **The demo path is sacred.** Every button on it works, or the path is not demo-ready (no-"85%").
4. **Read-only and OT-safety are non-negotiable.** Any write-back path to a control system is an automatic Critical.
5. **Evidence is required, not optional.** Every band above *Needs work* carries file\:line or a reproducible run.
6. **Score the lowest persona.** The product ships when the developer, the security reviewer, the engineer, the operator, the executive, *and* the brand can all sign — not on average, but each.

---

*Reconcile every assessment against Doctrine v7 (16 gates · 4 waves · 4 tracks). Freeze the doctrine; move the delta. The doctrine is the target; the delta is the truth.*
