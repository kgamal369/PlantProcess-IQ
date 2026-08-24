# PlantProcess IQ - Master Design Document

**Version 4.10 | Author: Karim, SOU Industrial Software, Dusseldorf**

> **Change log — Two-Release Production Roadmap and Day-1 Workbench Constitution (23 August 2026, v4.10).** v4.10 replaces retired internal programme codes with exactly two product releases: **M2 — Release 1, 30 September 2026**, for genuine early production and first-week customer work; and **M3 — Release 2, 30 October 2026**, for heavy production, higher data volume, more users and advanced intelligence. Each release uses only **P1, P2, P3, P4 and P5**. Release 1 makes DB Link/data onboarding, Canvas/data preparation, Jobs, enterprise BI reliability, read-only production OPC UA, governed References/Reconciliation/Assistant and minimum production hardening first-class release gates. Release 2 owns scale, advanced BI/authoring, deep enterprise administration, InsightBoard composition, multi-objective optimisation, customer-grade ROI convergence and heavy-production certification. Design and backlog are required to be one-to-one traceable: every designed product outcome has an execution owner and acceptance path, and every backlog task maps to an owning design contract.

> **Change log — Operational-Regime, Multi-Objective Practice and Period-Driver Hardening (22 August 2026, v4.9).** v4.9 closes the two generic gaps exposed by the first oil-plant requirement review without introducing oil-specific vocabulary: process transitions/changeovers and stabilisation become first-class governed context so statistics cannot mix distinct operating regimes; practice learning gains customer-declared multi-objective objective sets with Pareto/non-dominance and explicit preference resolution rather than silently choosing one KPI; exact period-to-period operational driver decomposition is added so the Assistant can explain changes in cost/productivity drivers from Layer-A facts before the monetary Value Engine is available. The release also binds the September checkpoint/fallback to the single v2.13 execution workbook. The six chapters remain the only design authority.


---

> **CURRENT AUTHORITY — Master Design v4.10.** PlantProcess IQ has exactly six current design-authority chapters and one current execution-authority backlog workbook. No other file may define, amend, override, supplement or reinterpret current product design or implementation scope. A design change edits the owning chapter directly; a scope change edits the backlog directly. Transitional reviews, amendment packs, ledgers, mandates and prior revisions are historical evidence only after their accepted content is integrated. Validation scripts are code/enforcement instruments, not design documentation.


# CHAPTER 2 - TECHNICAL OVERVIEW


## 2.0.0 Release roadmap constitution — only M2 and M3

PlantProcess IQ uses **two current product releases only**. Only `M2`, `M3` and phases `P1` through `P5` are current roadmap vocabulary. Earlier internal programme codes are historical evidence only and are not used by the current roadmap.

| Release | Date | Product meaning | P1 | P2 | P3 | P4 | P5 |
|---|---|---|---|---|---|---|---|
| **M2 — Release 1** | **30 Sep 2026** | Genuine early production. A customer can start work in week 1 without developer intervention. | Product foundation & Release Truth | Day-1 Data Workbench: DB Link, Canvas, Jobs, OPC and onboarding | Enterprise BI & customer experience | Industrial intelligence: References, reconciliation, governed Assistant | Release hardening & week-1 production acceptance |
| **M3 — Release 2** | **30 Oct 2026** | Heavy production: more users, larger volumes, sustained runtime and deeper product use. | Scale & OT resilience | Multi-user enterprise operations | Advanced BI, authoring & Insight Boards | Advanced ML/optimisation/value | Heavy-production certification |

### Release-1 Day-1 law

M2 does **not** mean a presentation build, pilot build or trial-only branch. It is the first production release. The same compiled product is used for the customer, and by the end of M2 the first-week journey must be executable through product surfaces:

`connect source → inspect schema → preview → configure/mapping → prepare in Canvas → validate/dry-run → save/version → compile/bind to Job → schedule/run → monitor/log → canonical result → BI interaction → governed evidence/Assistant`.

The journey fails release acceptance if customer-specific C#, TypeScript or product-SQL changes are needed.

### Release-2 heavy-production law

M3 deepens the same product rather than creating a different edition: larger data, higher concurrency, live-source endurance, advanced authoring, more users/roles, enterprise operations, model lifecycle, InsightBoard composition, multi-objective practice, customer-grade monetary value evidence and production capacity certification.

### Design/backlog mirror law

The six design chapters and the current backlog form one contract. **Design coverage and backlog coverage are not independent scores.** Every accepted design requirement must map to one or more backlog tasks with an owner, release, phase and executable acceptance; every current backlog task must map back to an owning design clause. A missing mapping in either direction is a governance defect.

**REVISION NEXT, 11 August 2026. Amendments C2-1 and C2-2 of the AI/ML/LLM target architecture are integrated into the active body below.**

## 2.0 Target architecture amendments integrated in this revision

### 2.0.1 Operational scoring latency (C2-1)

The actionable-prediction-latency guarantee is delivered by **hard-reserved online scoring capacity**, not by pool ordering or job priority. **A lane whose capacity can be consumed by training work cannot carry a latency guarantee**, because the guarantee would then depend on what else happened to be running. The reservation is specified in Chapter 4 5.3.2 and deployed per Chapter 6.

### 2.0.2 Glossary additions (C2-2)

| Term | Definition |
|---|---|
| **Semantic Contract Manifest** | An immutable, content-addressed record of exactly which canonical versions were in force when an artifact was produced: the published `definition_versions`, the relationship publication, and the governed registry and configuration state. A reproducibility pin. **It is not an authoring authority and has no lifecycle** |
| **Intelligence and engine families** | The seven analytical families MF-01 to MF-07, sub-typed as learned model (MF-01, MF-03, MF-04), retrieval and index (MF-02), statistical engine (MF-05, MF-06), practice engine (MF-07), plus orchestration and governance. **They are not seven ML models**; three of the seven are not models, and the sub-type determines lane, refresh policy and whether a champion/challenger gate applies |
| **Lane** | A physical execution context within a logical job class, carrying its own `max_concurrency` and `resource_capacity` |
| **Serving identity** | `(tenant_id, model_code, outcome_code, grain_code)`. The unit at which a model version is activated, retired and given a fallback. **There is no bundle object** |

**Withdrawn terminology.** The collective phrase "seven ML models" for MF-01 to MF-07 is withdrawn, as is `ModelBundle`.

**No product scope change. No capability is added or removed by this revision.**




### 2.0.3 Dataset-neutral product authority

No validation dataset is a product authority. Fleet/steel is a legacy regression fixture; continuous-process fixtures and customer-shaped fixtures are additional falsification instruments. **No industry noun, customer field name or prepared demo dimension may be required by product runtime.** Customer-visible dimensions, measures, grains, relationships, references and evidence authority are canonical definitions/registry rows, not compiled arrays.

The release-level genericity proof runs the same binary and migration set against at least one discrete fixture, one continuous-process fixture and one foreign/customer-shaped fixture. Any required product-code change for customer vocabulary fails the gate. A code change is allowed only when it adds a genuinely generic capability that remains applicable to future industries.

### 2.0.4 Analysis Subject, Grain and continuous-process semantics

The analytical subject is not universally a material unit. A result may concern a material unit, batch, lot, campaign, equipment, process window, flow interval or another customer-declared subject. A **Grain Definition** describes the identity and time semantics. An **Analysis Subject** is one resolved instance of that grain. Layer A and Layer B bind governed results to the Analysis Subject rather than assuming `coil`, `material` or another universal noun.

No engine supplies an implicit grain. Missing grain authority is a governed refusal.

### 2.0.5 Signal and aggregation semantics

A storage type such as numeric or boolean does not state how a signal may be aggregated. Every analytical parameter therefore carries governed signal semantics: signal kind, permitted/default aggregation when declared, interpolation, weighting basis, maximum carry gap, quality handling and counter-reset policy. A KPI binding may override aggregation for that KPI.

There is **no silent default `Average`**. When aggregation semantics are undeclared and the requested operation cannot be defended, the result is refused with `aggregation_semantics_undeclared`.

Initial required execution semantics are `SampleMean`, `TimeWeightedMean`, `Integral`, `Delta`, `StateDuration`, `Count`, `Min`, `Max` and `Last`; additional methods remain registry/engine grammar, not customer vocabulary.

### 2.0.6 Temporal authority

Cross-source analysis records which clock a timestamp represents. Per source the product governs plant timezone, timestamp basis, DST policy, clock reference, observed skew and maximum tolerated skew. Where available, machine observations preserve source timestamp, server timestamp and ingest timestamp separately.

A reconciliation whose clocks are not aligned inside the declared tolerance returns **Temporal Uncertain / clock alignment not established**. It never fabricates a conflict.

### 2.0.7 Performance Reference authority

**Performance Reference** is the umbrella term. The declared Layer-A kinds are `EngineeringStandard`, `ManagementTarget` and `OperatingEnvelope`; learned Layer-B kinds are `HistoricalBaseline`, `LearnedBestPractice` and `PeerReference`. Declared references are effective-dated, scoped and attributable to a declaring authority. Learned references carry window, population, support and confidence.

Scoring semantics are explicit: `HigherIsBetter`, `LowerIsBetter`, `InsideRange`, `ClosestToTarget`, `BinaryCompliance` or a governed expression. Derived measures include reference gap, normalised deviation, envelope compliance and attainment where mathematically valid.

### 2.0.8 Operational Evidence Reconciliation and causal confidence

A **Fact Evidence Authority** rule identifies which source is primary or supporting for one semantic fact; authority is fact-specific and tenant-configured. A PLC is not globally superior to MES, CMMS, a laboratory or a human record.

**Operational Evidence Reconciliation** compares independently sourced assertions over a temporally aligned interval. Its subject is the record/evidence case, never a person. Outcomes are `Aligned`, `PartiallyAligned`, `MissingEvidence`, `TemporalUncertain`, `ConflictingEvidence`, `LikelyMisclassified` or `Unresolved`.

The causal-confidence ladder is: **L0 Observed Fact; L1 Discrepancy; L2 Statistical Association; L3 Temporally Supported Hypothesis; L4 Mechanistically Supported Hypothesis; L5 Confirmed Cause.** L5 requires external/human confirmation or equivalent governed evidence. The Assistant may never linguistically promote a lower level.

### 2.0.9 OT acquisition capability truth

The customer-side collector remains outbound-only and read-only. **A connector advertises only capabilities that are executable in that build.** Configuration validation is not tag browse; a deterministic simulator is not live acquisition. Real OPC UA support is an edge-runtime capability comprising endpoint/security negotiation, session lifecycle, browse, subscription/monitored items, quality and timestamp preservation, reconnect/recovery and site certification. Until present, the connector reports the missing capability honestly.

### 2.0.10 Release Truth and governed investigation boards

Release evidence is layered: architecture/static truth; unit/semantic truth; persisted-definition replay; real API/database integration; customer-session behaviour; visual/resilience. Customer-facing routes fail release on unexpected 4xx/5xx, unhandled exceptions, error states or broken cross-filter behaviour. A browser-found defect becomes a failing automated test at the lowest suitable layer before the fix is accepted.

The future **Insight Board Plan** is a versioned, replayable governed artifact. It stores question, context, filters, planner version, definition versions, data-as-of, tool executions, widget specifications and evidence handles. The LLM may propose the plan; deterministic authorities validate it; the existing BI engine executes it. The LLM never emits raw SQL, React code or plant numbers as authority.


### 2.0.10a Operational Transition and Stabilisation Context

A production history is a sequence of **operating regimes**, not one homogeneous population. A customer-declared transition is a bounded event in which an operating context changes — product/recipe, tool, campaign, cleaning state, equipment configuration or another authored semantic. A **stabilisation window** is the declared period, subject-count or condition after that event during which operation is not yet steady-state.

The platform classifies analytical intervals as `Stable`, `Transition`, `Stabilising`, `Mixed` or `Unknown`. Statistical, aggregation, practice, reconciliation and Assistant consumers receive this regime context as evidence. A steady-state analysis does not silently pool `Transition`/`Stabilising` observations with `Stable`; it either partitions them under the authored policy or refuses `mixed_process_regime`.

This is generic context, not an oil feature. It covers any customer-declared process transition and has no shipped list of moulds, grades, recipes, products or cleaning cycles.

### 2.0.10b Multi-Objective Operating Practice

Practice learning is multi-objective when the customer declares more than one outcome that must be considered together. An **Objective Set** names registered measures/outcomes, their directionality and units, optional hard constraints, and an optional preference policy.

Three resolution modes are governed:

1. `ParetoOnly` — return the supported non-dominated practices; do not choose one.
2. `DeclaredUtility` — resolve a single preference only from an explicit, versioned customer utility/weight policy after every objective has been normalised by its registered Performance Reference semantics.
3. `LexicographicOrConstrained` — respect declared hard constraints/priority order before ranking remaining objectives.

**There are no default weights and no hidden scalarisation.** If objectives conflict and no preference is declared, the correct result is the Pareto/non-dominated set plus a refusal to name one practice “best”. A multi-objective result carries its full objective vector, support, uncertainty, dominance relation and the preference version used.

### 2.0.10c Operational Period Driver Decomposition

Before learned explanation, Layer A may compare two governed periods and report **exact operational deltas** over registered measures and context: transition count/duration, stabilisation exposure, stable-run/sequence length, stopped minutes, production-impact minutes, yield/scrap, energy and further registered facts. The comparison is evidence-backed and makes no causal claim.

This exact decomposition is the preferred Assistant tool for questions such as “what was operationally different in this month?” The Value Engine may later convert supported quantities into bounded monetary impact using explicit assumptions; the exact period comparison itself does not require the Value Engine and does not invent cost.

## 2.0.11 Documentation and authority constitution

The **six chapters are the complete current design authority**. Chapter 1 owns market/product promise; Chapter 2 owns product laws, naming and architecture; Chapter 3 owns exact technical contracts, persistence and APIs; Chapter 4 owns execution behaviour, algorithms and engine rules; Chapter 5 owns the customer operating journey; Chapter 6 owns deployment, infrastructure, administration and operational topology.

The backlog workbook is the **only current execution authority**. It may reference chapter sections but may not redefine them. A worker message, implementation mandate, review, handover, architecture note, amendment pack, synchronisation ledger or design scan result is never a second authority.

**Standing rule.** A new design document is not created for a new capability. The owning chapter is amended directly, its change log is updated, and enforcement instruments are updated to prove the amended contract. Temporary analysis/review artifacts are archived after their accepted content is integrated.

> **Target audience (3.7):** middle managers, operations engineers, quality engineers, process engineers. Technically literate, not developers.
>
> **Voice (3.8):** senior product owner.
>
> **Authority of this chapter.** Chapter 2 is the **naming, structure and positioning authority** for the whole document. Seven things are defined here and every other chapter must use them without variation: the canonical user journey (3.3.1), the canonical data-flow codes (3.3.2), the page and component inventory (3.4), the product glossary (3.9), the plant model entity catalogue (3.14), the permanent relationship model and its sixteen consumers (3.15), and the positioning rule with its design review checklist (3.19). Where another chapter disagrees with this one on a name, a number, a journey step or the positioning, this chapter governs.
>
> **Target design only.** This chapter describes the target design. No statement about what is currently built appears here; build status lives in the Implementation Status Register.

---

## 3.1 Concept and idea of the software

Every large process plant records what happens to its product many times, in many systems, under many vocabularies. One stage in one database, the next in another, inspection in a third, the laboratory in a fourth, spread across lines and sometimes across sites. Each system is correct alone. **Nothing joins them**, and every question that matters lives in the join.

PlantProcess IQ is a **read-only intelligence layer** over the systems the plant already owns.

1. It **connects** through read-only links and imports continuously, changing no source system.
2. The plant's own engineer **declares once** how the sources join. That declaration is published as a queryable relationship model and becomes the plant's permanent model of itself.
3. From then on the plant is **one analysable thing**: dashboards, statistics, machine learning, practice learning, prediction, remediation and a grounded assistant, every figure citing its evidence.

It replaces nothing, writes to nothing, computes deterministically, refuses when the data cannot support a defensible answer, and prices findings in euro as a bounded range.

One binary serves any process industry because nothing inside it knows any industry. Industry knowledge arrives with the customer's data or is authored by the customer; it never ships in the product.

---

## 3.2 Technical features, key aims and added value

### 3.2.1 The five capability layers

| Layer | Capability | Added value | Tier |
|---|---|---|---|
| 1 | **Unified visibility** | Every source side by side on one surface; plant-wide patterns visible without expertise | Light |
| 2 | **Statistical intelligence** | Every parameter related to every outcome under honest statistics; hard-to-see relationships made visible | Pro |
| 3 | **Machine learning** | Probable contributors, bottlenecks and recurring-failure drivers found by model rather than memory | Pro Plus |
| 4 | **Prediction** | A unit that ran abnormally upstream is flagged for a specific downstream outcome **before it occurs** | Pro Plus |
| 5 | **Recommendation** | Historically supported later-stage practices to avoid the predicted outcome, plus **practice benchmarking**: the plant's own best demonstrated practice and the practices that preceded downtime | Pro Plus |

Cross-cutting: **the assistant dock** (plain-language questions, cited answers, never computes) and **the value engine** (bounded euro range with drill-through). Standing under all five: **the readiness gate**.

### 3.2.2 The technical features that carry those layers

| Feature | Why it matters technically |
|---|---|
| Read-only acquisition through a customer-controlled one-way collector | No write path exists; the plant's automation team can approve without a control-systems risk review |
| Source-shaped staging with watermarks and batch lineage | The plant never remodels its databases; the delta is cheap and the lineage exact |
| A versioned, customer-authored Transformation Definition | The join is declared once, auditable, exportable, permanent |
| **One published plant relationship model** | The declared join is queryable by every downstream consumer instead of being locked in a document |
| **Validation and quarantine before projection** | A mapping mistake fails individual rows with typed reasons; it never silently corrupts the canonical model |
| A canonical multi-grain model with genealogy and weighted attribution | A parent-grain parameter can be attributed to a child-grain outcome; this is the cross-source mechanism |
| Registry-driven authoring | Every list in every palette derives from the customer's own data, which is how one binary serves every industry |
| Deterministic engines behind a readiness gate | Numbers are reproducible; an undefendable answer is refused rather than produced |
| **One unified definition store** | Every no-code artifact shares one identity, version, dependency and permission model |
| Bounded-parallelism execution with weighted pools | Hundreds of defined jobs cannot starve the interactive read path |
| **Delta propagation end to end** | **Every job class is delta-scoped, not only import.** A plant with hundreds of source tables and jobs on a three-minute cadence is served by processing what changed, never by rescanning the model. This is what makes the product a multi-source hub rather than a small-data tool (Chapter 4 5.3.9) |
| Evidence handles on every rendered figure | Any number on any screen walks back to the source row it came from |
| **Log persistence in PostgreSQL with HMI-managed retention** | Logs are queryable, exportable, partitioned, and cleaned on a policy the customer sets and can audit |

### 3.2.3 The positioning, stated as a technical requirement

PlantProcess IQ is **not a generic business-intelligence product**, and its dashboard experience is **not a reduced version of one**. The authoring, charting, modelling, filtering and exploration experience must reach the professional flexibility and clarity a customer already expects from that category. What differs is what the product understands, learns and produces behind that experience.

| Layer | What it is |
|---|---|
| **The interaction and presentation layer** | Pages, sheets, charts, tables, KPIs, filters, selections, bookmarks, drill-down and drill-through. Professional-class, authored by the customer, no developer required |
| **The product** | The permanent plant model, the genealogy, the evidence chain, the statistical discipline, practice learning, prediction, remediation and the feedback loop |

Full statement of the rule, with the review checklist it imposes on every capability: **3.19**. The authoring inventory it obliges: **3.16**. The consequence for how intelligence is displayed: **3.18**.

---

## 3.3 Workflows, data flows and technical flow

### 3.3.1 The canonical user journey - J1 to J15

**There is exactly one user journey in this product.** It is numbered J1 to J15 and it is defined here. Every chapter, tutorial, playbook, demonstration and acceptance instrument refers to these numbers and no other set. A second journey written anywhere is deleted rather than reconciled.

| # | Journey step | What the user achieves | Principal surface |
|---|---|---|---|
| **J1** | Install and first login | The platform is reachable; the plant schema provably holds zero rows | Login; Home |
| **J2** | Activate the licence | Tier capabilities and the capacity envelope become visible | F2 Licence and Entitlement |
| **J3** | Create users and roles | The people who will use the system exist, with scoped permissions | F1 Users and Roles; F3 Quota |
| **J4** | Declare read-only connections | Each source database or file share is reachable and proven read-only | B1 Connections |
| **J5** | Register datasets | The tables, views and files that will enter the product are chosen, with their watermarks | B2 Dataset Registry; B3 Prepare Import |
| **J6** | First incremental import | The customer's rows arrive in staging with batch lineage | B4 Importing; B5 Jobs Monitor |
| **J7** | Author the transformation and publish the relationship model | The plant's own model of itself exists: joins, keys, aliases, grain | C1 Transformation Studio; C6 Relationship Browser |
| **J8** | Project to canonical, with validation | Staged rows become canonical plant data; invalid rows are quarantined with reasons, not silently accepted | B4 Importing; C2 Mapping Health; C3 Data Quality |
| **J9** | Walk the genealogy | Any unit is traceable backward and forward on the plant's own keys | C5 Genealogy Explorer; C4 Plant Model Explorer |
| **J10** | Build pages, widgets and filters | Analysis surfaces exist, authored without code | D2 Page Builder |
| **J11** | Explore associatively | Clicking any value narrows everything and shows what is possible and excluded | D1 Interactive Workspace |
| **J12** | Author and run analysis through the gate | Statistical, correlation and model analyses run, or abstain with a named reason | D3 Analysis Toolbox; D8 ML Readiness and Models |
| **J13** | Read findings, risk, practices and value | Evidence-ranked results, predicted risk, learned practices and euro impact are all readable | D4 Findings; D5 Risk Dashboard; D9 Early Warning; D10 Practice Insights; D7 Value Dashboard |
| **J14** | Decide, act and measure | A suggestion or remediation is accepted, assigned, performed, and its actual outcome and effectiveness recorded | D6 Suggestions; D9 Early Warning; D7 Value Dashboard |
| **J15** | Operate, govern and retain | Jobs run, logs raise entries and are routed, the Supervisor proposes, retention runs, reports export | E3 Plant Data Log; E6 Alert Routing; E4 Supervisor; E5 Reports; F4-F9 |

J1 to J3 commission the platform. J4 to J9 build the plant model. J10 to J15 are daily life.

### 3.3.2 The canonical data-flow codes - DF1 to DF15

**Chapter 3 does not renumber the journey.** It specifies the *technical* flow using a separate, unambiguous code set, DF1 to DF15, so that "step 1" can never mean two different things. Each DF code maps to a journey step; several DF codes are internal and have no user-visible step of their own.

| DF | Technical step | Journey | Owner |
|---|---|---|---|
| **DF1** | Source connection: profile, credential vaulting, read-only proof, load budget | J4 | Acquisition |
| **DF2** | Dataset registration: discovery, column selection, business key, watermark | J5 | Acquisition |
| **DF3** | Incremental import into staging: batch, cursor advance, source-load enforcement | J6 | Acquisition |
| **DF4** | Transformation authoring and **relationship publication** | J7 | Authoring |
| **DF5** | Canonical projection with **validation, quarantine and reprocessing** | J8 | Projection |
| **DF6** | Genealogy resolution: alias resolution, edge construction, attribution weights | J9 | Projection |
| **DF7** | Page and widget binding, and associative query compilation | J10, J11 | Presentation |
| **DF8** | Readiness evaluation | J12 | Engine |
| **DF9** | Statistical and correlation run, with the discipline chain | J12 | Engine |
| **DF10** | Incremental feature refresh and feature snapshot | J12 | Engine |
| **DF11** | Model training, evaluation and registration | J12 | Engine |
| **DF12** | Practice learning: signature generation, outcome linkage, benchmarking | J13 | Engine |
| **DF13** | Prediction scoring, driver persistence and remediation candidate generation | J13 | Engine |
| **DF14** | Decision, action tracking, outcome arrival, evaluation, value and feedback | J14 | Engine and governance |
| **DF15** | Assistant retrieval, Supervisor governance, logging, routing and retention | J15 | Platform |

### 3.3.3 The six operating workflows

**W1 Commission a plant** (once): J1 to J9. Exit criterion: the evidence chain of 3.3.5 holds on first data.

**W2 Daily engineer loop**: open the workspace, select and drill, notice a pattern, open the analysis toolbox, run a gated analysis, read the finding and its value, act or ask the dock. Exit: a decision with evidence behind it.

**W3 Investigate an outcome**: an event arrives, open Genealogy Explorer from the affected unit, read upstream conditions on the thread, correlate against the outcome class, read the finding with population and framing, read the value range, export the report.

**W4 Extend the model**: a new source or column appears. Register the dataset, import, extend the Transformation Definition as a new version, review the impact preview, publish, project, mapping health green, and the registry offers the new columns everywhere automatically.

**W5 Author a page as a non-programmer**: Page Builder, add widget, choose kind, the shell opens in binding mode, catalogue or query binding, test, preview, save. Filters are authored the same way.

**W6 Operate and govern**: scheduled jobs run under admission control, the plant data log raises entries by authored rules, alerts route and escalate, the Supervisor proposes and a human approves, retention cleans on policy, the monthly value report exports.

### 3.3.4 The technical data flow, six stages

```
 SOURCES        COLLECTOR      STAGING         CANONICAL       RESULTS        SURFACES
 customer's     customer's     source-shaped   one model of    findings,      pages, dock,
 Oracle,        one-way push   copies, exact,  the plant with  risk, practice reports,
 MSSQL, MySQL,  DMZ, budgets   watermarked,    provenance      predictions,   exports
 PG, files                     quarantine                      value
    |              |               |                |              |             |
    +-- read ----->+-- push ------>+-- Transform ---+-- analysis --+-- render -->+
        only        DF1-DF3         Definition       DF8-DF14       DF7, DF15
                                   + relationship
                                   model  DF4-DF6
```

| Stage | One job | The rule that protects the next stage |
|---|---|---|
| Sources | Remain untouched | Never written to; the core never connects inward |
| Collector | Move data one way inside a stated budget | Row caps, timeouts, windows; backfill checkpointed and resumable |
| Staging | Hold an exact source-shaped copy | No interpretation; no analytical surface may read it |
| Canonical | Hold the one model, produced only by the published definition | Provenance on every row; invalid rows quarantined, never guessed |
| Results | Hold what the engines computed, with method, population, framing, gate evidence | Deterministic engines only; reconstructable from the database |
| Surfaces | Show it | No number without an evidence handle; refusals are named |

### 3.3.5 The evidence chain

The whole product as one unbroken thread, and the thing an acceptance walk follows:

> **Import batch -> published transformation and relationship model -> validated canonical projection -> gated analysis run -> evidence-ranked finding, prediction or practice -> recorded decision and measured outcome -> cited assistant answer.**

Every link is inspectable. Cut it anywhere and the platform refuses rather than bridging the gap silently.

### 3.3.6 The maturity statement

The platform publishes what it can do **now, measured**, rather than promising a schedule. Simple dashboards and key figures need no history. Statistical, model-based, practice and predictive capabilities each become available per outcome when that outcome's readiness dimensions pass their published thresholds, and the readiness meter shows the measured counts and the thresholds at all times. A historical backfill shortens the wait by supplying history directly. **While a gate blocks, the product shows the simple analysis, the meter and an honest collecting-data state - never a blank screen and never a fabricated result.**

---

## 3.4 The page and component inventory

### 3.4.1 How the inventory is counted

Two categories, counted separately, because they behave differently.

| Category | Definition | Count |
|---|---|---|
| **Route pages** | Reachable at a route, appear in navigation, have their own page contract | **40** |
| **Global shell components** | Present on every authenticated page, not routable, not navigable | **6** |

**The assistant is a global shell component, not a page.** It has no route. `/assistant-config` is a route page and is an administration surface (F-group).

### 3.4.2 Global shell components

| # | Component | Purpose |
|---|---|---|
| **G1** | **Assistant dock** | The chat, anchored inline-end block-end on every authenticated page; collapsed by default; persists across navigation; carries page context. Specified in Ch4 5.7 |
| **G2** | Application header and navigation | Site identity, tier badge, user menu, primary navigation, breadcrumb |
| **G3** | Global search and command palette | Search across fields, measures, definitions, pages and findings; keyboard-first command entry |
| **G4** | Notification and toast host | Transient confirmations, warnings and errors; never used for a refusal that needs a sentence in place |
| **G5** | Refusal and error boundary | The single component that renders every refusal and every load failure, so the pattern cannot drift |
| **G6** | Activity and progress tray | Running jobs, their progress and their refusals, reachable from any page without leaving it |

### 3.4.3 Route pages, with an overview of each

Every page publishes the page contract of Chapter 3, 4.7 and is specified control by control in Chapter 3, 4.4.

#### Group A - Enter (2)

**A1 Login** - `/login`. Authentication. A failed attempt is informative without being useful to an attacker: the credentials failed, never which half. Administrators are additionally challenged for a second factor. No navigation renders, because an unauthenticated user has nowhere to go.

**A2 Home** - `/`. The honest answer to "is this working, and how far has commissioning progressed". Plant status at a glance, the readiness meter with measured counts, recent findings, and the journey rail marking the current J-step. On a young installation it shows a collecting-data state naming the next action rather than an empty screen.

#### Group B - Connect and import (6)

**B1 Connections** - `/data-integration/connections`. Where read-only links are created, tested, scheduled and budgeted. The only door for plant data, carrying the read-only promise permanently on screen, plus the connector catalogue with honest availability so a buyer sees what is proven and what is planned without asking.

**B2 Dataset Registry** - `/data-integration/registry`. Where the customer chooses which source objects enter the product, by browsing the live source, so the engineer sees his own table and column names. Registration is what makes a dataset due for import.

**B3 Prepare Import** - `/data-integration/prepare`. Where each dataset gets its imported columns, business key and watermark column. This page is where a plant decides how cheap or expensive its imports will be.

**B4 Importing** - `/data-integration/importing`. Where imports run and are watched: batches with counts, watermark ranges and outcomes, projection results with mapped and quarantined counts, and the per-definition projection schedule.

**B5 Jobs Monitor** - `/data-integration/jobs`. One monitor for every job family: import, projection, feature refresh, analysis, model, practice, prediction, supervisor, alert evaluation, retention. It **watches**; it does not configure. A refused or blocked run appears here as a real run with its named reason.

**B6 Connector Truth** - `/data-integration/connector-truth`. The capability matrix per connector: what is proven, what is certified read-only, what is planned. It exists because a catalogue row is not a connector. No mutating control exists on this page.

#### Group C - Model the plant (6)

**C1 Transformation Studio** - `/prep/canvas`. The most important authoring surface. Here the engineer declares how staged data becomes canonical: joins, keys, aliases, grain, mappings. Block-and-wire for a plant user, SQL for an engineer, illegal wiring refused at drag time with a written sentence, compiled SQL always visible, immutable published versions, and an impact preview before publishing.

**C2 Mapping Health** - `/mapping-health`. Whether the authored model still matches the sources: coverage, unmapped columns, orphan rates, drift, and **the quarantine queue grouped by error code with example rows and the fix named**. This is where a mapping mistake surfaces before it becomes a wrong finding.

**C3 Data Quality** - `/data-quality`. Issues by class and source: completeness, validity, freshness, duplicates. It turns "the data is bad" into a named, countable list.

**C4 Plant Model Explorer** - `/plant-model`. Sites, areas, equipment, routes and operations as imported and mapped: the structural view, used at commissioning to confirm the model matches reality.

**C5 Genealogy Explorer** - `/materials/{id}`. Walk from any unit backward to its origin and forward to its descendants, on the customer's own keys, with attribution weights where a unit spans two parents, and the time-aligned thread of parameters, events and outcomes around it.

**C6 Relationship Browser** - `/relationships`. The published relationship model made visible and searchable: which entities join to which, on which key pairs, in which grain, by which preferred path, from which definition version, and which relationships are ambiguous or superseded. A customer authors joins on C1; **C6 is where they can see and audit the model they built**, and where every downstream consumer's view of the join can be confirmed.

#### Group D - See, analyse, predict (12)

**D1 Interactive Workspace** - `/workspace/:dashboardCode`. The analysis page. Widgets on a twelve-column grid, associative selection with possible and excluded states, an always-present selections bar, sheets, bookmarks and per-card tools. Where most users spend most of their time.

**D2 Page Builder** - `/page-builder`. Where pages, widgets and filters are authored without code, through the shared shell in binding mode. Filters are authored widgets here, not fixed furniture.

**D3 Analysis Toolbox** - `/analysis/toolbox`. Where a statistical or correlation analysis is declared - outcome, grain, window, method - with every option from the registry, the payload panel showing exactly what the engine will receive, and the readiness panel showing whether it can run.

**D4 Findings** - `/correlations`. Results ranked by effect size with q-values, sample sizes, stability, stratum survival and stored framing. **Findings are historical statistical associations across a population.** Non-significant results appear as first-class honest answers.

**D5 Risk Dashboard** - `/risk`. The **aggregate, current-state** view of predicted risk: distribution by class, by grade, by route, by stage, and trend over time. It answers "how much risk is in the plant right now". It **watches**; it does not queue work.

**D6 Suggestions** - `/suggestions`. Recommended actions with evidence, and the decision record: accepted, rejected or deferred, with reason. **A suggestion is a recommendation derived from a finding**, not from a single unit's prediction.

**D7 Value Dashboard** - `/value`. The euro view: bounded impact ranges, the inputs behind them, drill-through on every figure, scenario comparison, and the realisation ledger comparing expected against observed.

**D8 ML Readiness and Models** - `/ml-readiness`. Readiness per outcome and grain with measured values, the model registry, training runs, evaluation metrics and drift state.

**D9 Early Warning** - `/early-warning`. The **per-unit, actionable queue**: units in process now, flagged elevated risk for a specific downstream outcome, ranked by risk and time-to-stage, each with drivers, comparison against normal operating range, genealogy evidence, and where history supports one, a **remediation card with its support count**. It **acts**: acknowledge, assign, accept, reject, defer, and then track the action and its outcome.

**D10 Practice Insights** - `/practice-insights`. The **operating practices** reconstructed from the plant's own history, linked to outcomes, presented as benchmarks with support counts and confidence, plus a drift panel showing where current operation has moved away from the plant's own best demonstrated practice. **Practices are about how the plant operates; findings are about which parameters associate with which outcomes.**

**D11 Scenario Simulation** - `/scenarios`. Explicitly labelled **simulation, not prediction**: choose which variables may change within valid operating ranges, hold the rest fixed, run against a named model version, and compare the modelled result against a baseline with its uncertainty. No write path to the plant; every result carries the decision-support disclaimer.

**D12 Benchmarking** - `/benchmarking`. Internal comparison across registry-driven dimensions: line against line, equipment against equipment, route against route, product family against family, period against period, and current operation against the plant's own best demonstrated practice. All comparison dimensions come from the registry; none is shipped.

#### Group E - Operate (5)

**E2 Assistant Configuration** - `/assistant-config`. Which tools the dock may use per role and tier, indexed knowledge sources, the plant glossary and synonyms, guardrail phrases, the citation ceiling, verbosity, and the serving mode with the no-egress control.

**E3 Plant Data Log** - `/data-integration/alerting`. Where **plant-data rules are authored** and the entries they raise are read and acknowledged. This is plant reality: a parameter beyond a limit, a routing deviation, a chemistry out of range. It is not the platform's own operational log.

**E4 Supervisor** - `/supervisor`. The governed review that proposes bounded improvements to other jobs, with provenance, shadow dry-run and human approval. It changes nothing automatically and says so permanently in its own subtitle.

**E5 Reports** - `/reports`. Scheduled and on-demand reports on the light print surface, with export and webhook delivery.

**E6 Alert Routing and Escalation** - `/alert-routing`. Where log and alert entries become notifications: recipients by role and user, severity routing, channels, escalation on no acknowledgement, working hours and quiet periods, deduplication, suppression, grouping, rate limiting, delivery status, retry and dead-letter handling.

#### Group F - Administer (9)

**F1 Users and Roles** - `/admin/users`. Accounts, role assignment and the permission matrix, with inherited and overridden values visually distinct. Deactivation rather than deletion is the default, because an account referenced by an audit row must stay resolvable.

**F2 Licence and Entitlement** - `/admin/license`. The signed token, the tier, the capability set, and live capacity meters with measured consumption against the envelope.

**F3 Authoring Quota and Limits** - `/admin/quota`. How much each role and each user may create: pages, widgets, saved queries, analyses, log rules, datasets, jobs. Soft by default: warn at eighty percent, disable create at one hundred with the reason and the administrator named.

**F4 Jobs Administration** - `/admin/jobs`. The definition side of jobs: schedule, pool, compute weight, dependencies and the **visual dependency graph**. It **configures**; B5 watches. Pool and weight changes are confirmed because they change what the executor admits concurrently.

**F5 Logging and Audit** - `/admin/logs`. **Reads and exports** the log families, with live tail, run correlation, saved filters and self-describing export. The audit family has no edit or delete control anywhere on it.

**F6 Log Channel Configuration** - `/admin/log-channels`. **Defines** channels: severity mapping, routing target and reading roles. Built-in channels are visible but locked with the lock explained; the audit channel cannot be created, edited or targeted here.

**F7 System Settings** - `/admin/settings`. Site identity, units, formats, the plant time zone, retention defaults and the data-boundary controls. Each group states the consequence of its settings in one line.

**F8 Translation and Language** - `/admin/translation`. Language packs, per-label review, right-to-left verification, and the context panel showing where each label appears.

**F9 Log Retention and Archival** - `/admin/log-retention`. **Controls how long log history is stored**, whether it is archived before deletion, when cleanup runs, and what each cleanup did. Separate from F5 which reads and F6 which defines, because storage duration is a different decision from routing and a far more destructive one.

---

## 3.5 Administration features

Nine administration domains. Each is configured from the interface, because a capability requiring a source edit to configure has failed Rule 1.

### 3.5.1 Users and roles

Accounts, role assignment, and permissions granular per surface and per action. A shipped catalogue of eight roles with a floor of three (administrator, engineer, viewer). Two non-negotiables: **a viewer never authors SQL at any tier**; and **the licence gate and the role gate compose** - a capability is available when the tier allows it *and* the role allows it. At install only the vendor support account exists; the customer administrator is created at commissioning. No development or test account exists on any production path.

### 3.5.2 Licence configuration

Entitlement derives only from the signed, offline-verifiable token, never from an editable row and never from a client-supplied value. The surface shows the tier, the capability list, and **live meters** for the five metered dimensions: retained volume, ingest rate, minimum refresh interval, weighted compute slots, concurrent sessions. Approaching a meter warns and offers the upgrade path; exceeding one **throttles** and never destroys data or stops work mid-task. Expiry follows warning, grace, then read-only access to what the customer built. Switching tier visibly adds and removes capability in the running product.

### 3.5.3 Authoring quota and page-creation limits

The per-user and per-role division of the tier's total. Bounded objects: pages, widgets per page, saved queries, analysis definitions, model definitions, log rules, datasets, scheduled jobs. Soft by default with an eighty-percent warning and a hundred-percent disable naming the administrator; never a silent failure and never a lost draft. An administrator may raise one user's ceiling without changing the role default. The surface also lists top consumers by object type, which is how an administrator finds the one authored query consuming the compute slots.

### 3.5.4 Jobs monitoring and jobs administration

Two capabilities, deliberately two surfaces. **Monitoring** answers what ran, what is running, what failed and why, across every family, with refusals shown alongside successes. **Administration** answers what should run, when, in which pool, at what compute weight, and **with which dependencies** - including a visual dependency graph and an impact preview before a definition is published. Operations available from monitoring: run now, pause, resume, cancel, re-run a failed run against the same batch. Pausing is a first-class state and survives a restart.

### 3.5.5 Logging

Six families, each with its own audience, retention and export path, all persisted in PostgreSQL and all queryable from the HMI.

| Family | Contents | Read by |
|---|---|---|
| **System** | Application events, errors, health, request outcomes | Operator, vendor support |
| **Job** | Per-run progress, outcomes, row counts, refusals with reasons | Engineer |
| **Data** | Import batches, watermarks, projection results, quarantine reasons | Engineer, auditor |
| **Audit** | Who did what and when; immutable, append-only | Administrator, auditor |
| **Assistant** | Every question, retrieval scope, tool call, citation set, refusal | Administrator, auditor |
| **Plant data** | Entries raised by customer-authored rules against imported observations | Engineer, operator |

Plus **customer-authored channels** created in F6. Every family is filterable by time, severity, actor and family, and exportable. **The rule that makes the job family worth reading: a refusal is logged like a result**, so the log answers "why not" as readily as "what".

### 3.5.6 Configuring new log channels

An administrator defines a **named channel** with its severity mapping, routing target and reading roles, without a code change, because every plant wants something recorded that the vendor did not anticipate. **The boundary:** a channel changes what is recorded and routed, never what the product does, and **it can never target the audit family**, whose value is that nothing configurable can touch it.

### 3.5.7 Log retention and archival

The storage-duration decision, separated from reading and from routing because it is the only one of the three that destroys data. Per family or channel: a retention preset of one, two, three, six or twelve months or a custom day count; whether to archive before deletion and where; the cleanup schedule; the maximum rows per cleanup batch; and legal hold. **Before saving, the surface states the estimated rows to remove, the estimated storage recovered, the exact cutoff date and the channels affected**, and a destructive change is confirmed and audited. Cleanup runs automatically, prefers dropping a whole partition where one falls entirely outside retention, batches partial partitions, is idempotent, and **never deletes anything if its archive step failed**. Audit retention has a governed minimum that cannot be casually shortened. Retention-policy changes are themselves append-only audit events. **Deleting a rule or a channel never deletes the history it produced.**

### 3.5.8 Settings

Everything true of this installation rather than of one user: site and plant identity; units of measure; **the plant time zone**, which is not cosmetic because shift-boundary analysis is correct across daylight-saving transitions only through the stored local time and explicit zone; date and number formats stated explicitly and never inherited from the machine locale; retention defaults per stage; and the data-boundary controls including the per-tenant no-egress control. Per-user preferences inherit these as defaults.

### 3.5.9 Translation

Language packs, per-label review state, the fallback language, and mirrored verification for right-to-left languages. Direction neutrality is a build-time law; translation administration chooses among languages the build already renders correctly and cannot fix a layout that hardcoded a side.

---

## 3.6 The administration pages, with an overview of each

**F1 Users and Roles.** The account register and the permission matrix on one surface. The list shows every account with role, last login and state. Selecting an account opens permissions as a grid of surface against action, inherited from the role and overridable per user, with inherited and overridden values visually distinct so nobody has to guess why a user can do something. Creating an account is a two-field act followed by a role choice; the fuller grid sits behind that so a routine act stays routine. Every action writes an audit entry.

**F2 Licence and Entitlement.** Three regions. The token region shows what the signed licence says and accepts a new one. The capability region lists what this tier grants and what the next would add, which makes it a commercial surface as much as an administrative one. The meter region shows live consumption as five bars with measured numbers, each linking to the administration page that would relieve it. Pre-expiry shows a counting banner; post-expiry states that existing dashboards remain readable and no data has been destroyed.

**F3 Authoring Quota and Limits.** A matrix of creatable object type against role with per-user overrides beneath, each cell holding a limit and its current consumption, so an administrator sees that engineers are at seventy percent before anyone complains. Raising one ceiling is one audited action. Top consumers per object type are listed.

**F4 Jobs Administration.** Each row is a job definition with class, target, schedule, pool, compute weight and dependencies. Editing a schedule is a form; changing a pool or a weight is a separate confirmed action because it changes concurrency, and the confirmation states the resulting utilisation. A visual dependency graph shows fan-in and fan-out, and publishing a definition shows an impact preview of what downstream runs it will affect. A definition can be disabled without deletion, recording who and when.

**F5 Logging and Audit.** Tabs per family sharing a filter bar of time range, severity, actor and family. Rows expand to full context. **Live tail** streams new entries with a pause control. **Run correlation** pivots from any entry to every other entry sharing its run identifier, across families, which is how a failure is actually investigated. Saved filters can be pinned. Export produces a file in the light report style with the filter stated in its header, so an exported log is self-describing. The audit tab carries no edit or delete control at all.

**F6 Log Channel Configuration.** Channels listed with name, severity mapping, routing target and reading roles, and an editor that previews what an entry will look like before saving. Built-in channels are visible but locked with the lock explained in a sentence; the audit channel cannot be created, edited or targeted, and the page states that rather than leaving it to be discovered.

**F7 System Settings.** Five groups, each stating the consequence of its settings: identity, units and formats, time, retention defaults, data boundary. Changing the plant time zone is confirmed because it re-frames shift analysis. Toggling no-egress names which capabilities change behaviour. Format examples update live.

**F8 Translation and Language.** Labels against languages with a completion bar per language and a review state per cell. Filters isolate untranslated, translated-but-unreviewed, and verified-in-mirror. Selecting a label shows where it appears in the product, because a translator who cannot see the context will translate a button as a noun. Packs export and import.

**F9 Log Retention and Archival.** One row per family or channel, with columns: channel, current stored rows, current size, oldest entry, newest entry, retention policy, archive policy, legal hold, next cleanup, last cleanup result. Controls: retention preset or custom days; archive before deletion and its destination; cleanup schedule; maximum rows per batch; **dry-run preview**; save policy; run cleanup now; view cleanup history; place or remove legal hold. Saving shows the estimated rows removed, storage recovered, exact cutoff and channels affected, and requires confirmation. Cleanup history records cutoff, rows examined, archived and deleted, storage reclaimed, duration, status and failure reason.

---

## 3.7 Target audience

Middle managers, operations engineers, quality engineers and process engineers. Technical literacy and plant knowledge assumed; software development background not assumed. Endpoint-level detail is Chapter 3; surface-level interaction design is Chapter 4.

## 3.8 Voice

Senior product owner. Explanatory, concrete, and honest about what a page will and will not do.

---

## 3.9 The product glossary

**One meaning per term, across all four chapters.** A chapter using one of these words in another sense is wrong and is corrected against this table.

| Term | Definition | Grain | Produced by | Lives in |
|---|---|---|---|---|
| **Finding** | A statistical association between a factor and an outcome, measured across a population over a window, with effect size, q-value, stability and stratum survival | population | Statistical or correlation run (DF9) | `correlation_results` |
| **Prediction** | A forward-looking statement that one specific unit is at elevated risk of one specific downstream outcome, made before that outcome exists | one unit, one run | Scoring run (DF13) | `predictions` |
| **Risk score** | The numeric output of a prediction, on a defined scale, with a risk class and a horizon | one unit, one run | Scoring run (DF13) | `predictions.risk_score` |
| **Suggestion** | A recommended action derived from a **finding**, carrying evidence and expected effect, awaiting a human decision | population or class | Suggestion generation (DF13) | `suggestions` |
| **Remediation** | A specific later-stage practice, supported by comparable historical cases, offered against one **prediction** to avoid the predicted outcome | one unit, one prediction | Remediation search (DF13) | `remediation_candidates` |
| **Practice** | A comparable signature of how the plant was operated over a period: the parameter combination and sequence in force | one period, one context | Practice learning (DF12) | `practice_statistics` |
| **Definition** | Any customer-authored no-code or SQL artifact: transformation, widget binding, analysis, model, log rule. Has one identity and immutable versions | artifact | Authoring (DF4, DF7, DF9, DF11) | `definition_store`, `definition_versions` |
| **Relationship** | A published, queryable statement that two entities join, with keys, cardinality, grain and attribution rule | entity pair | Publishing a transformation (DF4) | `plant_relationships` |
| **Job** | A schedulable unit of work with a class, a schedule, a pool, a compute weight and dependencies. A **definition** of work | definition | Authoring | `job_definitions` |
| **Run** | One execution of a job, with a status including blocked and reaped, a gate verdict, and its own log and telemetry. An **instance** of work | execution | Executor | `job_run_history`, `compute_runs`, `prediction_runs` |
| **Log** | An entry in one of the six platform log families or a customer channel, recording what the **platform** did | entry | Platform | `system_log_entries`, `job_log_entries`, and so on |
| **Alert** | An entry raised by a customer-authored rule about **plant reality**, optionally routed to a person | entry | Rule evaluation (DF15) | `plant_data_log` |
| **Assistant dock** | The global shell component G1 that answers questions with citations. Not a page, has no route, present on every authenticated page | shell component | - | Ch4 5.7 |

| **Analysis Subject** | A resolved instance of one customer-declared analytical grain; may be an entity, campaign or time interval. The generic identity for feature, prediction and intelligence results | one declared grain instance | Canonical projection / subject resolver | `analysis_subjects` |
| **Grain Definition** | The customer-authored/registry definition of analytical identity and time semantics. No implicit default exists | definition | Authoring / registry | `analysis_grain_definitions` |
| **Signal Semantics** | The governed rules that state how a parameter can be aggregated/interpolated/weighted and when it becomes stale | parameter definition | Customer definition + product grammar | `parameter_definitions` |
| **Performance Reference** | An effective-dated declared or learned reference against which an actual value is compared under explicit scoring semantics | scoped reference | Customer declaration or learned engine | `performance_references`, learned reference result |
| **Fact Evidence Authority** | A tenant-scoped rule saying which source is primary/supporting for one semantic fact | fact/source rule | Customer configuration | `fact_authority_rules` |
| **Reconciliation Case** | A governed comparison of independent evidence about the same fact/interval, with temporal state, discrepancy, evidence handles and confidence level | record/interval | Reconciliation engine | `reconciliation_cases` |
| **Causal Confidence Level** | L0-L5 evidence ladder from observation to externally confirmed cause; language may never upgrade the level | claim | Engine/governance | stored on governed finding/reconciliation output |
| **Insight Board Plan** | A versioned, reproducible plan for charts/tables/KPIs generated from a question and executed only through governed definitions/tools | investigation | Assistant planner + deterministic validator | definition/evidence store |

**The three distinctions most easily confused, stated plainly.** A **finding** is about a population and looks backward; a **prediction** is about one unit and looks forward. A **suggestion** comes from a finding; a **remediation** comes from a prediction. A **log** records what the platform did; an **alert** records what the plant did.

---

## 3.10 Requirement classification

Every requirement in this document carries one of four classifications, and the rule that governs them.

| Class | Meaning |
|---|---|
| **Core** | Stated in the product guideline. Must be fully designed: journey, UI, API, persistence, validation, acceptance. **Cannot be deferred** |
| **Advanced** | Designed in full but gated to a higher tier, or dependent on a customer's own data maturity |
| **Future extension** | Designed to the level of its interfaces and persistence so that adding it later does not require re-architecture, with the first implementation explicitly scheduled rather than assumed |
| **Excluded** | Out of scope, with a stated technical reason |

**The binding rule: a requirement present in the product guideline can never be classified as a future extension.** If it is stated, it is Core, and Core means designed across all five layers.

Current classification of the capabilities most often questioned:

| Capability | Class | Note |
|---|---|---|
| Practice learning | **Core** | Guideline 1.3.b |
| Prediction and downstream remediation | **Core** | Guideline 1.3.c |
| Log retention and archival from the HMI | **Core** | Explicit requirement |
| Relationship model and definition store | **Core** | Required by the journey itself |
| Validation and quarantine before projection | **Core** | Required to protect the canonical model |
| Alert routing and escalation | **Core** | Delivery beyond the in-app log |
| Prediction explainability | **Core** | A driver list alone is insufficient |
| Feedback loop | **Core** | Required to measure whether a recommendation worked |
| Internal benchmarking | **Core** | Registry-driven comparison |
| Scenario simulation | **Advanced** | Pro Plus and Enterprise; labelled simulation, never prediction |
| Real-time co-editing of a definition | **Advanced** | Enterprise; safe optimistic concurrency is Core, live co-editing is not |
| **Actionable prediction latency** | **Core** | A prediction must reach the engineer before the last actionable stage has passed. Every prediction carries its deadline and its measured latency, and the miss rate is reported. Ch4 5.8.8 |
| Event-driven and sub-minute scoring | **Advanced** | The *mechanism* by which a short-stage plant meets the Core deadline. A scheduled job that demonstrably meets the deadline is equally compliant |
| Practice similarity and back-off | **Core** | Governed relaxation ladder with mandatory disclosure and a tolerance-sensitivity test. Ch4 5.6.4a |
| Remediation eligibility and safety gate | **Core** | Nine checks before any candidate is styled as a recommendation. Ch4 5.6.4d |
| Unstructured text evidence | **Future extension** | Interfaces, lineage, permission and citation model designed; ingestion scheduled |
| Inspection images | **Future extension** | Metadata, object-storage location, linkage and annotation model designed; first model scheduled |

| Analysis Subject and Grain authority | **Core** | Required so continuous, batch and discrete plants use one binary without a universal material assumption |
| Signal / aggregation semantics | **Core** | A product may not emit a plausible aggregate whose mathematical meaning is undeclared |
| Performance Reference - declared half | **Core** | Customer standards/targets/envelopes are exact governed facts |
| Operational Evidence Reconciliation | **Advanced** | Requires overlapping independent evidence and established temporal authority; full design is Core architecture, activation is data-maturity gated |
| Production OPC UA edge acquisition | **Advanced** | Connector family capability; the read-only collector architecture is Core, live protocol support is certified per customer environment |
| Insight Board Composer | **Advanced** | Governed investigation composition; grounded Assistant Q&A remains independently usable |
| Any write path to a plant system | **Excluded** | Violates the read-only boundary; no design will be produced |
| Autonomous application of a model or threshold change | **Excluded** | Violates governed review; human approval is structural |

---

## 3.11 Surface responsibility matrix

Six pairs of surfaces that could be confused. For each, which surface **watches**, which **configures**, which **acts**, and which **stores history**.

| Pair | Watches | Configures | Acts | Stores history |
|---|---|---|---|---|
| **Risk Dashboard (D5) vs Early Warning (D9)** | D5 - aggregate current risk: distribution, trend, by class and route. Answers "how much risk is in the plant" | - | **D9** - per-unit queue: acknowledge, assign, accept, reject, defer a remediation. Answers "what do I do about this unit" | Both read `predictions`; D9 writes the action and decision records |
| **Suggestions (D6) vs remediation actions (D9)** | D6 lists suggestions from **findings** and their decisions | - | **D6** decides a suggestion; **D9** decides a remediation for one prediction | D6 -> `suggestions`, `suggestion_audit`; D9 -> prediction action and effectiveness records |
| **Findings (D4) vs Practice Insights (D10)** | D4 - which **factors** associate with which outcomes, backward-looking, per population. D10 - which **operating practices** coincided with which outcomes, with support counts and drift | - | Neither acts; both feed D6 and D9 | D4 -> `correlation_results`; D10 -> `practice_statistics` |
| **Plant Data Log (E3) vs Logging and Audit (F5)** | E3 - entries about **plant reality** raised by customer rules. F5 - entries about **what the platform did** | E3 authors the rules; F6 configures platform channels | **E3** acknowledges an alert | E3 -> `plant_data_log`; F5 reads the six platform families |
| **Log Channel Configuration (F6) vs Log Retention (F9)** | - | **F6** defines a channel: severity mapping, routing, reading roles. **F9** defines how long it is kept and whether it is archived | **F9** runs a cleanup or places a legal hold | F6 -> `log_channels`; F9 -> `log_retention_policies`, `log_cleanup_runs`, `log_archive_artifacts` |
| **Jobs Monitor (B5) vs Jobs Administration (F4)** | **B5** - what ran, is running, failed, was refused, with progress and logs | **F4** - schedule, pool, compute weight, dependencies | B5 runs now, pauses, resumes, cancels, re-runs. F4 enables and disables a definition | B5 reads `job_run_history`; F4 writes `job_definitions` |

**The pattern behind the matrix:** a surface that watches never configures, a surface that configures is confirmed when the change affects concurrency or destroys data, and a surface that acts always writes a record of who acted and why.

---

## 3.12 The user-experience contract

The product is measured against the professional usability baseline users already expect from mature analytics products, and then adds what those products do not have: genealogy, evidence, readiness, prediction, practice and remediation. **The objective is not to copy any product visually.** It is to owe the user nothing they would get elsewhere.

This contract is product-wide. Every page inherits it; Chapter 4 specifies its mechanics.

| # | Contract element | What the product must provide |
|---|---|---|
| 1 | **Global application shell** | Persistent header, primary navigation, breadcrumb, tier badge, user menu, the assistant dock, the activity tray, and the command palette - identical on every authenticated page |
| 2 | **Data and fields panel** | On every authoring surface: the three-level schema tree with types, row-count hints, search across all levels, and drag of one column or a whole table |
| 3 | **Assets and reusable objects panel** | Master dimensions, master measures, master filters, saved queries, published definitions and bookmarks, browsable by folder and tag, insertable by drag |
| 4 | **Context-sensitive properties inspector** | One inspector region whose contents follow the selected object, with **its own layout per object type**, not one generic form |
| 5 | **Global search** | One search across fields, measures, definitions, pages, findings and log entries, with typed results and keyboard navigation |
| 6 | **Drag-and-drop authoring** | Every compositional act is achievable by dragging: a column onto a board, a block onto a graph, a widget onto a grid, a field into a filter |
| 7 | **Calculated fields and expression editor** | A professional expression editor with highlighting, autocomplete from live columns and functions, type information, inline validation with error position, format, test and sample result |
| 8 | **Chart recommendation with transparent reasoning** | The recommended chart is shown with the reason ("two measures, no dimension: scatter"), the alternatives are offered, and a choice that misrepresents the result carries a warning |
| 9 | **Drill-down hierarchies** | Registry-declared hierarchies drill in place with a breadcrumb and a way back |
| 10 | **Drill-through to source evidence** | From any figure to its query, its population and the source rows, through the provenance path |
| 11 | **Selections, bookmarks and saved views** | Selections always visible and individually removable; a selection state saveable as a bookmark; a bookmark shareable within permissions |
| 12 | **Master items and reusable definitions** | A dimension, measure, filter or query defined once and reused across pages, with a change propagating to every consumer and an impact preview before it does |
| 13 | **Undo, redo, autosave and version history** | Undo and redo on every authoring surface with defined boundaries; drafts autosaved; every published version recoverable and diffable |
| 14 | **Responsive layout** | Twelve columns to six to one; nothing clips, overlaps or collapses below its content; the dock becomes a sheet on small screens |
| 15 | **Keyboard authoring** | Every authoring act reachable from the keyboard, including placing a node, creating a wire, opening an inspector and running validation |
| 16 | **Accessible colour and non-colour state** | WCAG AA contrast; colourblind-safe palettes; **state never carried by colour alone** - always an icon or a word as well |
| 17 | **Complete state coverage** | Every surface implements empty, loading, populated, filtered-to-empty, **blocked**, **refused** and failed, each with its own wording, and filtered-to-empty is never worded as genuinely empty |
| 18 | **Performance feedback** | A cost estimate before an expensive operation; streamed progress during it with rows and stage; a cancel control; and a stated reason when the platform defers or throttles the work |
| 19 | **Consistent interaction patterns** | The same control looks and behaves identically everywhere; Escape closes, Enter submits, Back returns, reload preserves; one refusal component renders every refusal |
| 20 | **Nothing decorative** | If a control appears, it works. If a colour carries meaning, that meaning is enforced. If a badge shows a state, the state is real |

---

## 3.13 The five-layer completeness rule

**A requirement is not closed because its name appears somewhere.** Every requirement in this document connects five layers, and it is complete only when all five are specified:

| Layer | The question it answers |
|---|---|
| **1. Journey** | Which J-step and which DF-step does this belong to? |
| **2. UI and controls** | Which page or shell component, and which specific controls, with their states? |
| **3. API and service** | Which endpoints, which handlers, which request and response content? |
| **4. Database and persistence** | Which tables, keys, constraints, indexes, retention and isolation? |
| **5. Validation and acceptance** | Which validation rules with which refusal sentences, and which observable acceptance test? |

Chapter 3 carries layers 1 to 5 for the general product function. Chapter 4 carries layers 1 to 5 for the specific surfaces and engines. Chapter 1 carries the traceability index that proves each commercial promise reaches all five.

---

## 3.14 The plant model: the entity catalogue

The intelligence in this product is only as good as the model of the plant beneath it. **That model is learned, never shipped.** This section states which entity classes the product understands structurally; every value, name, type, route, defect, parameter and rule inside them arrives from the customer.

### 3.14.1 The entity classes

| Cluster | Entity classes the product models |
|---|---|
| **Structure** | Sites and plants; areas and production units; equipment; **inspection devices** as a distinct class from production equipment; **equipment states** |
| **Material** | Materials and material identifiers; **material aliases across different source systems**; material types and grains; parent and child materials; **material genealogy and tracking paths** |
| **Flow** | Production routes; route steps and process stages; operations and operation sequences; process executions |
| **Measurement** | Process parameters; operating parameters; parameter definitions with units and expected ranges; parameter observations |
| **Outcome** | Quality inspections and quality events; defects and defect classifications; downtime events with both stopped and production-impact minutes; **maintenance and operational events**; throughput, yield and energy as registered outcomes |
| **Specification and rule** | **Product specifications**; **production rules and operating limits** |
| **Vocabulary** | All taxonomy and master data imported from the customer's own systems |

### 3.14.2 The three doors, and only three

The schema provides **generic structural concepts**. Everything customer-specific enters through exactly one of three doors:

| Door | What enters | Who opens it |
|---|---|---|
| **1. Customer data import** | Rows, keys, names, vocabularies, taxonomy, master data | The customer, through the read-only link |
| **2. Customer-authored configuration** | Relationships, definitions, measures, filters, rules, limits, thresholds, cost inputs | The customer, in the product's own surfaces |
| **3. Registry derivation** | The dimensions, measures, hierarchies and chart compatibilities the palettes offer | The product, derived from doors 1 and 2 |

**There is no fourth door.** A value specific to one industry that did not arrive through one of these is a Rule 1 defect. This is why one binary serves steel, paper, food and beverage, minerals, tyres, aluminium and pharmaceutical plants without a code branch.

### 3.14.3 Why the entity classes matter beyond charting

Each class exists because an intelligence layer needs it, not because a dashboard wants a dimension:

| Class | Which capability depends on it |
|---|---|
| Genealogy and parent-child materials | Cross-grain correlation, feature assembly, prediction attribution, evidence walk-back |
| Aliases | Cross-source identity resolution; without it no join across systems is auditable |
| Route steps and operation sequences | Practice signatures, stage context for prediction, "which stage can still remediate this" |
| Equipment states and maintenance events | Downtime attribution, failure-practice linkage, equipment-condition features |
| Specifications and operating limits | Out-of-range validation, plant-data rules, capability analysis, impossible-value detection |
| Both downtime quantities | Every value calculation involving loss |

---

## 3.15 The permanent plant relationship model

### 3.15.1 The principle

When the customer's engineer declares a join, **that join must not live inside one dashboard or one temporary query.** It becomes the plant's permanent, versioned, queryable relationship model.

> **Declared once. Validated once. Published once. Then honoured consistently everywhere.**

### 3.15.2 The joins a real plant declares

These are the seven declarations that actually occur during commissioning, and each becomes a relationship record:

| # | The declaration | Crosses |
|---|---|---|
| 1 | A material identifier in one database is the same physical unit as a different identifier in an inspection system | Two source systems, two vocabularies |
| 2 | Equipment in a historian is the production unit recorded in the tracking system | Two source systems, two naming schemes |
| 3 | A process execution belongs to a material | Execution grain to material grain |
| 4 | A quality event belongs to a downstream material | Event to material, often a later grain |
| 5 | A parent material produced these child products | **Grain conversion**, with attribution |
| 6 | A route step is performed on this equipment | Flow to structure |
| 7 | An upstream parameter relates to a downstream quality outcome | **Across grain, through genealogy** |

Declaration 5 and declaration 7 are the ones no generic analytics platform can carry, because they require grain conversion with weighted attribution rather than a simple key equality.

### 3.15.3 What a relationship record holds

| Property | Why it is needed |
|---|---|
| Left and right entity | The two sides |
| Join type | Inner, left, right, full |
| Cardinality | One-to-one, one-to-many, many-to-many |
| **Ordered composite-key members** | Real plants key on two or three columns, and the order matters |
| **Grain on both sides** | So a cross-grain join is recognised as one |
| **Attribution or allocation rule** | How a parent's value is divided across children; weights per child sum to exactly one |
| Preferred path | Which route to use when several exist |
| Alternative paths | The others, retained rather than discarded |
| **Ambiguity state** | Explicitly flagged when two paths are equally valid, so a consumer refuses rather than guessing |
| **Validation state** | Whether it has been proven against real data |
| Source definition and version | Which authored definition published it |
| Effective and retirement timestamps | So a historical result remains explainable after the model changes |

Full specification with DDL: Chapter 3, 4.5.10.

### 3.15.4 The sixteen consumers

**A relationship is declared once and then read by every capability that needs to traverse the plant.** This list is exhaustive and binding: a capability that re-derives a join instead of reading the model is a defect.

| # | Consumer | What it reads the model for |
|---|---|---|
| 1 | **Canonical projection** | How staged rows resolve into related canonical entities |
| 2 | **Registry generation** | Which dimensions are reachable from which measures, and therefore what the palettes may offer |
| 3 | **Page and widget query compiler** | The join path between the columns an author chose |
| 4 | **Associative filtering** | The graph a selection propagates along to produce possible, excluded and alternative states |
| 5 | **Drill-down** | Which hierarchy steps exist and in what order |
| 6 | **Drill-through** | The path from a displayed figure to the rows behind it |
| 7 | **Genealogy** | Parent and child traversal with attribution |
| 8 | **Statistical analysis** | Which populations can legitimately be joined for a comparison |
| 9 | **Correlation** | The path between a factor and an outcome, including across grain |
| 10 | **Feature engineering** | How a parent-grain parameter reaches a child-grain label |
| 11 | **Model training** | The same paths, so training and scoring cannot disagree |
| 12 | **Model scoring** | Identical path resolution to training, by construction |
| 13 | **Practice learning** | Route and operation context for a practice signature |
| 14 | **Prediction and remediation search** | Stage context, comparable-case retrieval, and which stages remain downstream |
| 15 | **Value calculation** | From an outcome to the affected material and the impacted production |
| 16 | **Assistant retrieval and tools** | What may be traversed; the assistant cannot join what was never declared |

Plus **evidence and provenance**, which uses the model in reverse to walk any figure back to its source rows.

### 3.15.5 Consequences a reviewer should check

- A selection made on any visual propagates through **this** model, so every dependent chart, table, KPI, prediction and evidence view narrows consistently rather than each surface applying its own idea of the join.
- Training and scoring resolve paths through the same model, which is why a feature can never mean one thing at training and another at scoring.
- Changing a relationship shows an **impact preview** naming every definition, page, analysis and model that depends on it, before the change is published.
- A retired relationship is deactivated, never deleted, so a finding computed last year remains explainable this year.

---

## 3.16 Authoring freedom: what the customer can create, and how

### 3.16.1 The rule

**The customer must not depend on a developer for normal analytical needs.** Everything in the following inventory is created through the interface, within the customer's role and capacity envelope, with no code change and no request to the vendor.

### 3.16.2 The authoring inventory

| Group | The customer can create |
|---|---|
| **Pages** | Analysis pages; sheets and page sections; responsive layouts; bookmarks and saved views |
| **Visuals** | Charts; tables; pivot-style views; KPIs; calculated labels; text and container widgets |
| **Filters** | List, dropdown, search, date and date-range, relative-time, numeric-range, hierarchical, Top-N and expression-based filters |
| **Calculation** | Calculated columns; measures; expressions; derived variables |
| **Reusable assets** | Master dimensions; master measures; master filters; reusable saved queries; hierarchies; drill-down paths; drill-through targets |
| **Data model** | Connections; dataset registrations; transformation definitions; **relationships between entities** |
| **Intelligence** | Analysis definitions; model definitions; **practice-learning definitions**; prediction and remediation configuration within governed bounds |
| **Operations** | Plant-data log rules; alerts and routing rules; report definitions; job schedules within the capacity envelope |

### 3.16.3 The two authoring paths

Both paths produce **the same class of artifact**: a named, versioned, governed definition in the one definition store. Neither is a lesser citizen and neither is a different product.

| | **Guided path** | **Advanced path** |
|---|---|---|
| **For** | A plant engineer with no software background | An engineer with database experience |
| **Provides** | Drag and drop; live schema and field browser; registry-driven fields; visual relationship creation; chart recommendation with its reasoning; context-sensitive property inspector; immediate preview; clear validation; human-readable refusal messages | Professional expression editor; SQL editor with autocomplete; schema and column discovery; safe-function whitelist; query preview; result-schema inspection; execution estimate; compiled-SQL view; version comparison; dependency impact; controlled publication |
| **Tier and role** | Every tier; any authoring role | SQL from the second tier upward, and never for a viewer |
| **Output** | A definition | The same definition class |

**Switching between them is a first-class act, not a trap.** Guided to advanced always succeeds: the graph compiles and the compiled statement is loaded. Advanced to guided succeeds only where the statement is representable in the block grammar, and where it is not the product says so at the point of the switch and requires confirmation before discarding the diagram.

### 3.16.4 The professionalism test

For every authoring capability, the reviewer asks the eight questions of 3.19.2. A capability that cannot answer all eight is not finished, however well it demonstrates.

---

## 3.17 Dynamic filtering and associative exploration

### 3.17.1 Filters are authored objects

**Filtering is never a fixed row of dropdowns.** Every customer has different fields and different questions, so a filter is an authorable object whose field comes from the registry, which is derived from the customer's own validated model.

### 3.17.2 The filter catalogue

| Kind | Behaviour |
|---|---|
| **List** | Scrollable value list with search, tri-state colouring, multi-select. The default |
| **Dropdown** | Single or multi select, where space is tight |
| **Search** | Free text across one or more text columns |
| **Date and date-range** | Two pickers plus presets |
| **Relative time** | Rolling windows: last 7 days, last 30, current shift, current month, previous period |
| **Numeric range** | Dual slider plus numeric entry |
| **Hierarchical** | Registry-declared hierarchy, filtered at any level, narrowing the levels below |
| **Top-N** | The N highest or lowest by a chosen measure, recomputed under the current selection |
| **Expression-based** | An authored condition for anything the other kinds cannot express |

### 3.17.3 The scopes, and how they compose

| Scope | Meaning |
|---|---|
| **Widget-level permanent scope** | A filter saved on a widget is that widget's permanent boundary |
| **Page-level filter** | The filter bar, applied to every widget on the page |
| **Cross-widget selection** | Clicking a value on any visual publishes a selection to every widget |
| **User-level saved selection** | A named selection a user keeps for themselves |
| **Bookmark** | A saved selection plus page state, shareable within permissions |

**The composition rule, and it appears in the authoring panel's own hint text so a user reads it where the choice is made:** a widget's saved filter is its permanent scope; the page filter and any associative selection apply **on top of it**, narrowing further inside that scope, combined with AND. They compose; they never compete.

### 3.17.4 The associative states

Four states, always: **selected**, **possible**, **excluded** and **alternative**. Excluded values remain clickable and pivot the selection, because a user clicking an excluded value is saying the previous selection was wrong.

**The architectural point:** the state computation propagates **through the permanent relationship model of 3.15**, not through per-page join guesses. That is why a selection on a process parameter correctly narrows a quality chart, a prediction queue, a practice benchmark and an evidence view at the same time, and why the four states mean the same thing on every surface.

Execution strategy at scale, and behaviour for high-cardinality fields: Chapter 4, 5.1.3.

---

## 3.18 Intelligence as a first-class analytical object

### 3.18.1 The requirement

**The display layer presents intelligence, not only data.** A page the customer authors must be able to consume generated intelligence with exactly the freedom it has over canonical data. Intelligence that can only be seen on the page we designed for it is a report, not a product.

### 3.18.2 What an authored page or widget may bind to

| Bindable source | Produced by |
|---|---|
| Canonical plant data | Projection (DF5) |
| Statistical results | Statistical run (DF9) |
| Correlation findings | Correlation run (DF9) |
| Model results and metrics | Training and evaluation (DF11) |
| **Predictions** | Scoring run (DF13) |
| **Prediction drivers** | Scoring run (DF13) |
| **Practice benchmarks** | Practice learning (DF12) |
| **Practice drift** | Practice learning (DF12) |
| **Remediation candidates** | Remediation search (DF13) |
| **Suggestion decisions** | Decision capture (DF14) |
| **Value impacts** | Value engine (DF14) |
| Data-quality conditions | Quality scan (DF5) |
| **Readiness states** | Gate evaluation (DF8) |
| Evidence and provenance | The relationship model, in reverse |

### 3.18.3 The ten behaviours every intelligence object must support

A prediction or a finding behaves like any other analytical object. All ten are required, not aspirational:

| # | Behaviour |
|---|---|
| 1 | It can be **filtered** - by any registered dimension reachable through the relationship model |
| 2 | It can be **compared** - across periods, lines, equipment, routes or contexts |
| 3 | It can be **drilled into** - to its population, its drivers and its comparable cases |
| 4 | It can be **placed in a chart** - on any page the customer builds |
| 5 | It can be **included in a table** - with its own columns and conditional formatting |
| 6 | It can **open its evidence** - the run, the method, the population, the source rows |
| 7 | It can be **linked to material genealogy** - which unit, which stage, which ancestry |
| 8 | It can be **included in a report** - scheduled or on demand |
| 9 | It can be **explained by the assistant** - with resolvable citations |
| 10 | It can be **tracked to its eventual outcome** - what was decided, what was done, what happened |

### 3.18.4 What this requires of the design

Three consequences that Chapters 3 and 4 must carry, and which are the reason this section exists:

1. **Intelligence tables are registered as bindable sources** with their own registry dimensions and measures, so the palettes offer them exactly as they offer canonical columns.
2. **The widget query compiler resolves join paths into the results area**, so a prediction can be charted beside the process parameter that drove it in one widget.
3. **Associative selection reaches intelligence.** Selecting a defect class narrows the findings list, the prediction queue, the practice benchmarks and the value figures, because all of them are joined through the same relationship model.

---

## 3.19 The positioning rule, and the design review checklist

### 3.19.1 The cross-chapter design rule

**This is a design rule, not a marketing statement, and every chapter is reviewed against it.**

> The target is not "build a smaller business-intelligence product inside PlantProcess IQ."
>
> The target is: **deliver a professional-class analytical and authoring experience over a permanent, customer-authored plant model, then use that model to learn the plant's process behaviour, discover evidence-backed relationships, predict downstream outcomes, identify historically supported remediation, and measure what happened afterwards.**

Two failure modes are equally serious. Shipping a weaker dashboard than the category expects fails the customer's engineer on the first day. Shipping only a dashboard fails the entire product thesis.

### 3.19.2 The eight questions for every dashboard, charting, filtering and authoring capability

| # | Question |
|---|---|
| 1 | Is it as generic and flexible as a mature analytics product? |
| 2 | Can the customer create it without a code change? |
| 3 | Is every customer-specific option registry-driven? |
| 4 | Does it use the permanent plant relationship model? |
| 5 | Can it consume both canonical data **and** generated intelligence? |
| 6 | Does every result carry evidence and provenance? |
| 7 | Does the experience remain clear at enterprise scale? |
| 8 | Is the complete backend, database and validation contract specified? |

### 3.19.3 The nine questions for every intelligence capability

| # | Question |
|---|---|
| 1 | Which plant entities does it use? |
| 2 | How are features assembled through genealogy and route context? |
| 3 | Which model or method computes it? |
| 4 | Where is the result stored? |
| 5 | How is it displayed and filtered? |
| 6 | How does the user inspect the evidence? |
| 7 | How is the decision recorded? |
| 8 | How is the eventual outcome captured? |
| 9 | How does validated feedback contribute to future governed learning? |

### 3.19.4 How the checklist is used

Every capability specified in Chapters 3 and 4 carries its answers, and a capability that cannot answer every applicable question is **not finished** regardless of how well it demonstrates. The checklist is applied before a chapter is presented, not after it is challenged.

---

*End of Chapter 2. This chapter is the naming and structure authority: J1-J15, DF1-DF15, the 40 route pages and 6 shell components, the glossary of 3.9, the plant model of 3.14, the relationship model of 3.15 and the positioning rule of 3.19 govern every other chapter.*

---

# CHAPTER 2 — PART II: INTEGRATED LEARNED INDUSTRIAL INTELLIGENCE CONSTITUTION

**Status: NORMATIVE TARGET DESIGN.** This Part II is the former Layer-B Rule Revision 8 absorbed into the official Chapter 2 so there is no separate design authority. The main Chapter 2 body governs naming and product architecture; this Part II gives the deep industrial-intelligence constitution. Chapter 3 remains the persistence/API authority, Chapter 4 the execution/algorithm authority, and Chapter 6 the deployment authority.

### LB-1. PURPOSE

Layer B turns a customer's historical and continuously arriving plant data into: plant fingerprint, anomaly and novelty knowledge, process-outcome relationships, attributable risk, practice learning, operating envelopes, early prediction, historical similarity, evidence-supported remediation suggestions, machine-readable intelligence datasets for dashboards, and evidence endpoints for the Assistant.

**It must be generic across industries.** Fleet-v2 and steel are one test dataset. Oil, mineral water, pharma, paper, tyres, food and unknown future industries use the same engine architecture.

---

### LB-2. THE SEMANTIC WALL

**Layer B contains no customer table name, no column name, no schema name and no industry term.**

Forbidden in any Layer B code path:

```
read Coil table        read Heat table        read CastingSpeed
if (customer == ...)   class OilModel         class BottleModel
```

Customer physical schemas are handled above the intelligence engine. The customer configures data through the PPIQ authoring surfaces: the no-code wiring canvas, relationship declarations, governed SQL where needed, the dataset registry, and parameter and outcome definitions.

**Layer B consumes only published canonical contracts:**

```
CUSTOMER SOURCES
  -> NO-CODE WIRING / GOVERNED SQL
  -> PUBLISHED definition_versions
     + PUBLISHED plant_relationships (emitted by publishing the transformation)
     + GOVERNED registry and configuration state
  -> pinned for reproducibility by a SEMANTIC CONTRACT MANIFEST
  -> CANONICAL / SPINE / FEATURE REPRESENTATION
  -> LEARNED INTELLIGENCE ENGINE
```

Enforced in three layers: the Layer B database role holds grants on Plant Data and the intelligence schema only; every source reference resolves through a published `definition_version` and every entity correspondence through `RelationshipResolver`; and an architecture test asserts no Layer B file contains a customer identifier or an industry noun, falsified once before it is trusted.

---



### 2.1 Generic Analysis Subject law

Layer B receives `analysis_subject_id + grain_code`, not an industry identity. An Analysis Subject may resolve to a material unit, batch, campaign, equipment interval, process window, flow interval or customer extension. `material_unit_id` may exist in canonical discrete-manufacturing data but is **not** a mandatory key in Layer-B feature, prediction or evidence contracts.

There is no default `coil`, `material`, `batch` or other universal grain. If the published contract does not resolve a grain/subject, the execution refuses.

### 2.2 Signal aggregation law

`data_type` is storage, not mathematics. SM-05 carries signal/aggregation semantics. The engine may aggregate only through an allowed published semantic. No component may silently use Average because nothing else was declared. `AG01 aggregation_semantics_undeclared` is a valid terminal result.

Continuous-time semantics must account for irregular sampling, quality and maximum carry gaps when the declared method requires them. SampleMean remains legal for data whose governed semantics make it legal; time weighting is not applied blindly.

### 2.3 Temporal and evidence authority law

Cross-source claims first resolve source time authority and alignment tolerance. If clocks cannot be aligned, the output is TemporalUncertain, not conflict.

Evidence authority is fact-specific and tenant-configured. PLC, historian, MES, CMMS, laboratory and manual records may each be primary for different facts. A global `PLC > everything` priority is forbidden.

### 2.4 Reconciliation and causal-confidence law

Operational Evidence Reconciliation may emit Aligned, PartiallyAligned, MissingEvidence, TemporalUncertain, ConflictingEvidence, LikelyMisclassified or Unresolved. The subject is a case/record, never a person; intent is never inferred.

Every root-cause statement carries L0-L5 causal confidence. Below L5 the approved phrase is **strongest supported root-cause hypothesis**. L5 requires governed confirmation outside the model's own inference.

### LB-3. THE SEMANTIC CONTRACT MANIFEST

**`ppiq_meta.semantic_manifests` is an immutable, content-addressed reproducibility pin. It is not an authoring authority and has no lifecycle.**

| Column | Type | Notes |
|---|---|---|
| `manifest_id` | uuid **PRIMARY KEY** | The handle artifacts reference |
| `tenant_id` | uuid NOT NULL | |
| `manifest_hash` | varchar(64) NOT NULL | Content hash over the referenced versions |
| `definition_versions` | jsonb NOT NULL | `{definition_id, version_number}` array |
| `relationship_source_definition_id` | uuid NOT NULL | |
| `relationship_source_definition_version` | integer NOT NULL | |
| `registry_snapshot_hash` | varchar(64) NOT NULL | Over the registry rows in force |
| `configuration_hash` | varchar(64) NULL | Governed configuration affecting semantics |
| `created_at_utc` | timestamptz NOT NULL | |

**UNIQUE `(tenant_id, manifest_hash)`.** Identical content within a tenant never creates a second row. Identical content across two tenants correctly creates two rows, because a manifest is tenant-owned evidence.

**No status column. No draft, validated, published or rolled-back state. Nothing updates a manifest.**

`definition_versions`, the relationship publication and `model_registry` retain their lifecycle authority unchanged. The manifest records which versions were in force; it does not govern them.

**Coverage.** Run, artifact and evidence tables carry `semantic_manifest_id uuid NULL FK`. **Nullable for legacy records only. Every new governed AI/ML execution must resolve a manifest**; a run that cannot is refused rather than recorded without one.

---

### LB-4. THE RELATIONSHIP MODEL

**Chapter 2 3.15 positions it. Chapter 3 4.5.10 implements it: `plant_relationships`, `plant_relationship_members`, `plant_relationship_paths`, versioned by `source_definition_id` and `source_definition_version` with an effective and retired lifecycle. Publishing the transformation emits the model.**

**No statistical, feature, ML, prediction, practice, remediation, value, Assistant or evidence engine owns a private join.** One resolver serves all sixteen consumers through `GET /api/relationships/resolve?from=&to=&purpose=`.

Four behavioural rules: ambiguity refuses rather than guesses; `validation_state = unproven` permits `explore` and refuses `train`; grain conversion requires attribution weights summing to 1.0; and a relationship is deactivated, never deleted.

---

### LB-5. TRUTH CONTRACTS

**Layer A** produces exact facts: count, sum, grouped KPI, historical totals, exact filtered population.

**Layer B** produces learned estimates: risk, similarity, anomaly, attribution, prediction, operating envelope, learned effect, recommendation confidence.

**Never use ML to approximate an exact BI fact because the dataset is large.** The Assistant may combine both, clearly labelled.

Every output classifies itself as **ASSOCIATION**, **PREDICTIVE CONTRIBUTION**, **MATCHED EFFECT ESTIMATE** or **CAUSAL EVIDENCE**. **A claim class is never upgraded by language.**

Terminal states: **FINDING, INSUFFICIENT DATA, NOT APPLICABLE, REFUSED BY GUARD, CONTRADICTED BY CONTROL, MODEL NOT READY**, expressed through the canonical error codes. **Never turn a method limitation into a false statement about customer data.**

---

### LB-6. DATA PRODUCTS AND THE TRAINING PATH

Persistent governed products, not repeated scans of source tables: journey spine, feature store, sequence store, outcome store, evidence store, prediction store, embedding and index metadata.

### 6.1 The training read path

**PostgreSQL JSONB is not the training read path.**

```
live governed feature state     ppiq_plant.feature_store, jsonb, incremental, RLS
      |  seal
      v
immutable snapshot manifest     feature_snapshots: storage_uri, artifact_format,
      |                         artifact_content_hash, artifact_byte_size
      |  materialise
      v
typed columnar artifact         object storage. Format selected by B-03
      |  bounded read, projection pushdown
      v
Python data loader              PyTorch / LightGBM input
```

`feature_store` owns current governed state, lineage, row-level security and incremental refresh by watermark. **The sealed artifact owns high-throughput training input.**

**No training or encoding component queries `feature_store`. The snapshot materialiser is exempt by definition** and is the only component permitted to read it for sealing.

`feature_snapshot_rows` is an optional audit sample, not the authoritative copy.

### 6.2 The sequence path

**`ppiq_plant.sequence_manifests`** in PostgreSQL holds the manifest: subject identity, `channel_set_version`, time range, sample and channel counts, completeness, content hash, storage URI, chunk index.

**Object storage holds the payload**: immutable chunked typed numeric arrays, compressed, partitioned, memory-mappable where the format allows. The loader consumes bounded chunks. **No numeric sequence payload is stored in PostgreSQL.**

---

### LB-7. THE SEVEN INTELLIGENCE AND ENGINE FAMILIES

**Not seven ML models. Three of the seven are not models, and the sub-type determines lane, refresh policy and whether a champion/challenger gate applies.**

| ID | Family | Sub-type | Lane | Champion/challenger |
|---|---|---|---|---|
| MF-01 | Process encoder | Learned model | `ml.training` | Yes, plus the promotion inequality |
| MF-02 | Similarity index | **Retrieval and index** | `ml.training` to build | No. Gated on measured recall@k |
| MF-03 | Normal and novelty | Learned model | `ml.training` | Yes |
| MF-04 | Supervised outcome | Learned model | `ml.training` | Yes, three-dimensional |
| MF-05 | Effect and envelope | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-06 | Statistical intelligence | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-07 | Practice learning | **Practice engine** | `analysis` | No. Governed signature version |

Plus **orchestration and governance**: the capability profiler, the model-count governor and the supervisor.

**Framework and implementation choices are replaceable and benchmark-driven.** PyTorch behind a `ProcessEncoder` abstraction; **`VectorSimilarityIndex` is the contract and FAISS is one implementation candidate**; `SupervisedOutcomeModel` with LightGBM as the initial tabular candidate; TreeSHAP as the initial explanation mechanism.

**A mandatory simple baseline is trained first.** A complex model ships only when it beats the baseline on the three-dimensional gate.

---

### LB-8. MODEL REGISTRY, ACTIVATION AND ROLLBACK

**`ppiq_plant.model_registry`, governed per serving identity. There is no bundle object.**

```
serving identity = ( tenant_id , model_code , outcome_code , grain_code )
serving version  = serving identity + model_version
```

`outcome_code` and `grain_code` are model identity, not metadata.

**Two independent axes:** `status` in `trained, rejected, active, review, retired`, and `serving_role` in `none, serving_fallback`.

Constraints: at most one `active` per serving identity; at most one `serving_fallback` per serving identity; a retired, rejected or under-review model can never hold a fallback approval; **one version can never be both primary and fallback**, because a fallback that is already the primary masks the absence of a safety net. Every UNIQUE carries `tenant_id` first.

**A fallback is never inferred from the last active version.** Use is recorded and surfaced through `prediction_current.fallback_in_use`; silently serving a fallback as primary is prohibited.

`model_training_runs` carries **CHECK `overlap_rows = 0`**, making leakage a database-level impossibility rather than a test.

### 8.1 Promotion is a three-dimensional gate

On the same governed recent holdout as the incumbent:

**QUALITY** - discrimination or error, **calibration**, out-of-time performance, subgroup and regime stability, missingness robustness, **explanation stability**.

**SERVING** - p50, p95 and p99 inference latency, throughput, artifact size, RAM and VRAM, warm-up time.

**TRAINING** - duration against the weekly window, peak memory against lane capacity, snapshot read throughput.

**A better-discriminating, worse-calibrated model is not an improvement** for a product whose output is a risk band a human acts on. **An unstable explanation is worse than none**, because contributors are presented as evidence.

**The encoder ships only when it earns its operational cost:**

```
promote_encoder  iff  metric_lift            >= declared_min_lift
                 AND  p95_latency_delta      <= declared_latency_budget
                 AND  artifact_size          <= declared_size_class
                 AND  explanation_stability  >= floor
```

If engineered features match it within the lift threshold, **the engineered features ship**.

---

### LB-9. VECTOR SEARCH

`VectorSimilarityIndex` with build, seal, extend, search and recall_probe is the product contract. **FAISS, HNSW, IVF, PQ, quantised and GPU-backed variants are implementations selected by measurement.** No library name appears in the contract.

Index family is selected from population size, vector dimension, available RAM, required recall@k, p95 latency target, build time and update pattern.

**Exact Flat search is retained permanently on a representative sample as the recall baseline.** Recall@k is measured on every build and stored. **A build below the declared recall floor does not become the served index.** An approximate index whose recall has never been measured is an unquantified error source presented as a plant fingerprint.

---

### LB-10. PREDICTION, REMEDIATION AND DECISION

**Prediction cutoff is structural.** Features available after the cutoff are prohibited; a model with excellent metrics caused by future information is a failed model.

**Operational delivery.** `prediction_current` carries the actionable deadline, remaining stage state, scoring mode, delivery latency and fallback state. A prediction that arrives after its last actionable stage is a record, not an intervention.

**Remediation is a nine-check gate** with four outcomes: **actionable** (all nine pass), **evidence_only** (checks 5 to 9 pass, one or more of 1 to 4 fail for this unit), **exploratory** (checks 1 to 6 pass, 7 or 8 fails), **suppressed** (safety check 4 fails, not shown, audited).

**`can_accept` is the complete seven-condition server-side acceptance authority and is not a synonym for actionable.** The client reads `can_accept` alone and must not re-derive any condition. Accept, Reject and Defer exist only where it is true.

**Escalation is a record, never a decision.** It creates no action row, contributes to no effectiveness row and is excluded from feedback.

**Prediction evaluation excludes intervened instances** from accuracy metrics and reports them separately, because a prevented event is not a false positive.

**Value** carries mandatory bounds when the basis is sufficient, with a point estimate permitted beside them, per-tenant currency, and abstention when the basis is absent. Potential impacts are non-additive and are never summed into a total saving.

---

### LB-11. THE MODALITY BOUNDARY

**The boundary is governance, not modality.**

> **No free-form or model-generated output may become a feature, a score, a statistic or a value.** Text and images may enter a learned result **only** through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring.
>
> Retrieval-derived and LLM-derived content is **evidence only**: it may corroborate a deterministic result and may never originate one.

**Path A, evidence modality.** Operator notes, shift logs, maintenance text, documents. Indexed, retrieved, cited. Never a feature, never a score, never a plant fact the model originated.

**Path B, governed multimodal ML.** The full contract above. This is how an inspection-image model produces an annotation with a confidence under the same activation, retirement and drift rules as any model.

**No implementation scope is added by this boundary.** Both modalities remain interface-designed, future implementation.

---

### LB-12. EXECUTION LANES AND ADMISSION

**Six logical job classes. The `ml` class resolves to three physical lanes.**

| Lane | Reserved | Pre-emptible | Admits |
|---|---|---|---|
| `ml.training` | no | **yes** | Encoder and supervised training, calibration, SHAP batch, index build |
| `ml.batch_scoring` | no | yes | Scheduled scoring, backfill, rescore after activation |
| **`ml.online_scoring`** | **yes** | **no** | **Event and micro-batch scoring and its required serving functions only** |

**Admission requires both predicates:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

`max_concurrency` is how many runs may be in flight; `resource_capacity` is how much scarce resource exists; `compute_weight` is what one run consumes. **One number never expresses two quantities.**

**The online reservation is never available to training or batch admission.** Batch, backfill and rescore work runs on batch and training-class capacity, never on the online container. Where hardware is physically shared, online capacity remains hard-reserved and **B-02 must prove the actionable-latency target while training and batch are saturated**.

**Warm models** for every active serving identity are resident and reference-counted; a newly activated model is warmed before it serves. **Training yields at its next checkpoint** when a reserved lane needs capacity.

**Daytime serving performs no training.** Tier 1 precomputed reads target seconds; tier 2 bounded computation on prepared stores targets under 30 seconds; tier 3 schedules or refuses. **The absolute synchronous ceiling is under 2 minutes.**

---

### LB-13. THE ASSISTANT RUNTIME

**The Assistant is an orchestrator and communicator over governed tools. It never computes, never originates a figure and never replaces an engine refusal.**

```
[1] permission and tenant context      [2] intent and entity resolution
[3] DETERMINISTIC TOOL PLANNER         [4a] structured tools  [4b] evidence retrieval
[5] token-budgeted evidence packing    [6] model gateway      [7] LLM, phrasing only
[8] deterministic answer verification  [9] cited answer or refusal
```

**The LLM does not choose tools.** A planner maps resolved intent to a declared tool set; tool-selection accuracy is gated. Where intent is ambiguous the planner asks rather than guessing.

**Hybrid retrieval with the permission filter applied before ranking**, not after, so a forbidden chunk cannot displace a permitted one. **Structured tools take precedence over retrieval for facts and analytical results**; a number never comes from a retrieved chunk when a tool can compute it. Re-ranking is optional and ships only if it earns its latency.

**Evidence packing** deduplicates, ranks engine output above documents, enforces a hard token budget with a reserved answer allowance, retains every evidence handle, and **records and discloses truncation**.

**The gateway sends the minimum scoped evidence** to an external provider, never a whole retrieval set and never raw canonical rows. **A provider or model change is a governed release event.**

**`ModelServingRuntime`** is a replaceable abstraction; no serving library is the product contract.

**Answer verification is deterministic and does not call the LLM**, because a model checking its own output is not a guard. Every numeric claim must resolve to a supplied handle; no claim class is upgraded; no refusal is replaced by a phrased answer; a transport failure is never dressed as an abstention.

Quality gates Q-01 to Q-11. **Q-05 refusal correctness and Q-06 causal-overreach rate decide credibility.**

---

### LB-14. OUTPUT DATASETS AND BINDING

**Seven governed intelligence dataset families**: prediction, contributor, similarity, anomaly, envelope, finding and effect, and **model and readiness status**. The seventh is what a new installation binds to before any model is ready, so it renders truthfully rather than appearing broken.

Intelligence sources are declared in `registry_intelligence_sources` with `sourceKind = 'intelligence'`, an entity link column and `columnRoles`. The widget execution contract is **columns, rows and warnings**.

**Two source classes.** Fact-shaped aggregate sources may project through `WidgetFact` into the generic aggregate executor. **Native-grain rich sources keep their declared columns and are never flattened into a single value column.** Aggregation policy governs which native columns may be aggregated.

**No ML-specific widget type. No branch on dataset origin.**

---

### LB-15. GOVERNANCE

**Tenant isolation is absolute.** Models, embeddings, neighbours and evidence are tenant-scoped. No cross-tenant vector index, no cross-tenant training population, no cross-tenant benchmarking.

**Reproducibility.** Given tenant, manifest, feature set version, training window and model version, the model is reproducible and a changed answer is explicable. Deterministic seeds where practical, immutable dataset manifests, code identity, environment manifest, artifact hashes.

**The supervisor** observes, proposes a bounded adjustment, shadow-runs it against held-out history, compares, requires **human approval**, and applies atomically with provenance and a rollback pointer. It may **never** modify readiness thresholds, refusal rules, evidence requirements, leakage gates, tenant isolation, the semantic contract or the forbidden-combination set. **A component that can improve results by lowering the bar for what counts as a result will eventually do so.** It records abstention as well as action.

**PPIQ writes only to its own governed stores.** Never to a customer source system, never to a control system, never to a setpoint. An accepted recommendation records that a human acted; it does not act.

**Gate inventory: G-01 to G-55.** No model reaches production because training completed. Every gate is falsified once before it is trusted.

---

### LB-16. BENCHMARK PARAMETERS

Nine values stay open until measured. **No number in this integrated constitution is guessed.**

| ID | Question |
|---|---|
| B-01 | `max_concurrency`, `resource_capacity`, `compute_weight` per lane |
| B-02 | Online scoring reservation fraction |
| B-03 | Columnar snapshot format; whether the audit sample can be demoted |
| B-04 | Sequence chunk size and compression |
| B-05 | Encoder lift versus its serving cost |
| B-06 | ANN family per size class |
| B-07 | Token budget and evidence-set size |
| B-08 | Whether re-ranking earns its latency |
| B-09 | Serving runtime and concurrency |

---

### LB-17. ACCEPTANCE PRINCIPLE

> A completely new industrial customer can map their own data through PPIQ's no-code semantic authoring, run commissioning, obtain only the intelligence their data genuinely supports, receive weekly governed model updates, ask questions during production hours in less than two minutes, and bind learned outputs to ordinary PPIQ widgets without a developer writing industry-specific ML code.

**Build the generic learned intelligence contract. The algorithms sit behind it and can evolve.**

---

*This Layer-B constitution is part of Chapter 2 and is current design authority only in this integrated form. Prior standalone Layer-B rule documents are historical evidence and are not implementation authority.*

