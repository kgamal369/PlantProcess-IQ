# PlantProcess IQ — Product Analysis and Evaluation Standard

**Document type:** Product Owner capability and evaluation standard  
**Version:** 1.1  
**Status:** Working authority for product-capability analysis, release readiness and backlog traceability  
**Product:** PlantProcess IQ  
**Purpose:** define *what the product must be able to do*, how each capability is validated, how maturity is scored, and how every backlog task is traced to the product capability it fulfils.

---

# 0. How to use this document

This document is deliberately **not a backlog**, **not a Jira task list**, and **not an implementation plan**.

It is the Product Owner's stable capability map.

The backlog answers:

> What work are we doing?

This standard answers:

> What product capability does that work exist to fulfil?

Every backlog item must therefore map to at least one capability ID in this document.

Every capability status record must also declare its **Release scope** (`R1`, `R2`, `Later`, or staged scope) so Product Owner reporting can answer both **"How complete is the whole product?"** and **"Are we ready for the next customer/release?"**.

Examples:

- `T-252` → **Primary: L8.2 — Test architecture with disposable isolated databases**
- `T-251` → **Primary: L5.7 — Physical database governance**
- `T-261` → **Primary: L2.3 — Canonical projection**, Secondary: **L3.8 — Canvas → job → run → per-block evidence**
- `T-245` → **Primary: L3.8 — Canvas → job → run → per-block evidence**

A task can contribute to more than one capability, but one capability must be declared **Primary** so that reporting does not double-count the same work.

---

# 0.1 Design authority and classification

Each capability carries:

- a permanent capability ID;
- product description;
- product concept / why it exists;
- validation standard;
- Master Design reference;
- requirement class.

Requirement class follows the Master Design classification:

| Class | Meaning | Product reporting rule |
|---|---|---|
| **Core** | Required product behaviour. Cannot be deferred merely for convenience. | Included in Core Completeness. |
| **Advanced** | Designed advanced capability, but not part of the minimum Core completeness denominator. | Report separately. |
| **Future extension** | Interface/boundary may be designed, but full feature is intentionally future. | Report separately, never count as current Core gap. |
| **Excluded** | Deliberate non-goal or prohibited behaviour. | Never score as a missing feature. |

---

# 0.2 Completeness evaluation standard

A capability is not considered complete because its name, page, API or table exists.

Every capability is evaluated against five completeness dimensions derived from the Master Design five-layer completeness rule.

To avoid confusion with the nine product layers in this document, the five completeness dimensions are named **D1–D5** here:

| Dimension | Question | Typical evidence |
|---|---|---|
| **D1 — Journey** | Is the capability correctly placed in the canonical customer journey/data flow? | J-step, DF-step, documented path |
| **D2 — UI & Controls** | Can the intended user perform and understand it through the correct product surface and states? | page/surface, controls, empty/loading/blocked/refused/failed states |
| **D3 — API & Service** | Is the real product service/API/executor implemented and wired? | route, handler, service, runtime execution |
| **D4 — Data & Persistence** | Is its data model, persistence, identity, versioning, retention and integrity implemented? | tables, keys, constraints, registry/definition entries |
| **D5 — Validation & Acceptance** | Has the behaviour been falsified/proven with named acceptance evidence, including refusal/failure paths? | tests, runtime evidence, DB proof, browser proof, release gate |

## Scoring

Per applicable completeness dimension:

- `Y` = 1.0
- `Partial` = 0.5
- `N` = 0.0
- `N/A` = excluded from that capability's denominator only when the design genuinely has no such dimension.

Capability score is normalized to a five-point scale:

`Capability score = earned applicable points / applicable points × 5`

Examples:

- `Y / Y / Partial / Y / N` = `3.5 / 5`
- a certification-only capability with `C2 = N/A`, `C3 = N/A`, `C4 = N/A`, and both applicable dimensions proven can still score `5 / 5`; N/A is not a failure.

## Aggregate reporting

Report Core, Advanced and Future separately.

**Core Completeness**

`sum(earned completeness points on Core capabilities) / sum(applicable completeness points on Core capabilities)`

Do not mix Advanced or Future capabilities into the Core denominator.

Excluded items are never scored.

---

# 0.3 Maturity status labels

| Score | Label | Meaning |
|---:|---|---|
| **5.0 / 5** | Product-complete | End-to-end capability proven, including acceptance |
| **4.0–4.5 / 5** | Operationally near-complete | One bounded layer/gate remains |
| **3.0–3.5 / 5** | Substantial but incomplete | Real product foundation exists; critical end-to-end seam remains |
| **1.5–2.5 / 5** | Foundation only | contracts/components exist but user outcome is not operational |
| **0–1.0 / 5** | Not product-active | design or minimal scaffold only |

A capability may not be called "complete" if D5 is not proven.

---

# 1. Layer 1 — Acquisition and Source Truth

**Scope:** J4–J6 · DF1–DF3 · source connection, discovery, ingestion and source truth  
**Primary surfaces:** B1–B6

## L1.1 — Read-only connection and source access

**Class:** Core  
**Design:** Chapter 3 §4.2 DF1

**Description:** Create and manage a source connection profile, credential reference, connectivity settings, read-only posture and customer-source load budget.

**Product concept:** PlantProcess IQ is an intelligence layer over systems the customer already owns. Source systems are never altered. A connection is not "working" merely because a socket opens; it must prove the configured source identity, read-only boundary and load posture.

**Validation:**
- connection can be created/tested from the product;
- secret is not exposed in frontend, logs or repository;
- source identity/provider is proven;
- read-only posture is enforced or demonstrably constrained;
- configured load budget/throttling is honoured;
- failure is typed and does not create staged data.

---

## L1.2 — Dataset registration and source selection

**Class:** Core  
**Design:** Chapter 3 §4.2 DF2

**Description:** Discover source schemas/tables/files/sheets and allow the customer to register selected datasets, columns, business keys, timestamp/watermark and acquisition configuration.

**Product concept:** PPIQ must adapt to the customer's source shape without requiring that customer to remodel their source system.

**Validation:**
- real source discovery returns only accessible source objects;
- customer can select specific tables/datasets and specific columns;
- business key and watermark can be declared;
- saved registration reopens identically;
- invalid/removed source objects are surfaced as drift rather than silently ignored.

---

## L1.3 — Incremental import into staging

**Class:** Core  
**Design:** Chapter 3 §4.2 DF3

**Description:** Perform initial and incremental imports into `ppiq_staging` with batch identity, cursor/watermark progression and source-load enforcement.

**Product concept:** Acquisition must be repeatable and cheap enough for continuous plant operation. Re-reading the whole source every cycle is not the product model.

**Validation:**
- first load creates a traceable batch;
- subsequent load reads only the valid delta;
- cursor advances only after successful durable processing;
- retry does not duplicate accepted records;
- partial failure has an explicit recoverable state;
- customer source load remains within configured bounds.

---

## L1.4 — Connector capability truth

**Class:** Core  
**Design:** Chapter 3 surface B6

**Description:** One runtime authority declares which operations each connector can actually execute now.

**Product concept:** The UI, API, sales/demo surface and runtime must never disagree about whether Oracle, PostgreSQL, SQL Server, MySQL, CSV, Excel, OPC-UA or another connector really supports discovery/import/incremental/live operations.

**Validation:**
- one capability registry/authority is consumed by all surfaces;
- declared executable capability has a real implementation and positive test;
- unavailable capability refuses with typed reason and no placeholder data;
- no page independently hardcodes connector readiness.

---

## L1.5 — Acquisition provenance and source lineage

**Class:** Core  
**Design:** Chapter 2 LB-5

**Description:** Preserve the chain source → connection → dataset → import batch → source record.

**Product concept:** Every downstream fact must be explainable back to where it came from.

**Validation:**
- imported record carries resolvable source identity;
- lineage survives projection into canonical data;
- duplicate/retry paths do not create ambiguous origin;
- lineage is queryable by downstream evidence consumers.

---

## L1.6 — Source schema drift and mapping-health signalling

**Class:** Core  
**Design:** Chapter 3 surface C2

**Description:** Detect added, removed, renamed or type-changed source fields and surface the effect before incorrect data reaches canonical tables.

**Product concept:** Customer systems evolve. Drift must become a visible governance event, not silent corruption.

**Validation:**
- drift is detected against the registered contract;
- breaking and non-breaking changes are distinguished;
- affected mapping/transformation is identified;
- execution blocks or warns according to policy;
- accepted remediation is versioned.

---

## L1.7 — Import monitoring, recovery and dead-letter visibility

**Class:** Core  
**Design:** Chapter 3 B4/B5

**Description:** Monitor import runs, progress, retry, cancellation, resumability, failed rows and recovery state.

**Product concept:** Acquisition is an operational subsystem, not a one-shot wizard.

**Validation:**
- current run state and progress are visible;
- cancellation is honoured;
- retry/resume is idempotent;
- failed/dead-letter rows remain inspectable;
- last successful watermark/cursor is visible;
- a failed import cannot masquerade as success.

---

## L1.8 — OPC-UA / historian / edge acquisition

**Class:** Advanced  
**Design:** Chapter 2 §3.10

**Description:** Read OPC-UA namespaces/tags or historian signals through a customer-side read-only collector and land governed source-shaped data in staging.

**Product concept:** Direct industrial signal acquisition extends the same read-only acquisition model to PLC/historian environments without turning PPIQ into a SCADA/control system.

**Validation:**
- secure live session/handshake;
- namespace/tag browse;
- bounded historical/current reads;
- subscription/monitored-item lifecycle where required;
- timestamp and quality semantics preserved;
- selected tags land in staging with lineage;
- absolutely no write/setpoint/control path.

---

# 2. Layer 2 — Canonical Platform, Projection and Jobs

**Scope:** J7–J9 · DF4–DF6 · governed transformation, canonical projection, genealogy and job execution

## L2.1 — Three-schema platform and placement law

**Class:** Core  
**Design:** Database Standard §1; Chapter 3 database architecture

**Description:** Maintain exactly the governed business schemas: `ppiq_staging`, `ppiq_plant`, and `ppiq_meta`, with `public` restricted to permitted platform infrastructure.

**Product concept:** Placement answers one question: "whose knowledge is this?" Source-shaped customer data belongs in staging; canonical plant and analytical facts belong in plant; product configuration/governance belongs in meta.

**Validation:**
- fresh install creates the governed schemas deterministically;
- customer facts do not leak into meta/public;
- analytical/business surfaces do not read staging directly;
- public has no unclassified product-business objects;
- physical catalogue shows zero unknown objects.

---

## L2.2 — Transformation authoring and relationship publication

**Class:** Core  
**Design:** Chapter 3 §4.2 DF4

**Description:** Customer-authored transformation joins and relationships are saved, validated, versioned and published as the plant's permanent relationship model.

**Product concept:** The customer declares once how their sources join; downstream consumers reuse that governed declaration instead of each dashboard/model recreating joins.

**Validation:**
- source entities/fields can be selected;
- relationships, keys, cardinality and grain are explicit;
- transformation can be validated before publication;
- published version is immutable;
- relationship model is queryable by downstream consumers.

---

## L2.3 — Canonical projection with validation, quarantine and reprocessing

**Class:** Core  
**Design:** Chapter 3 §4.2 DF5

**Description:** Execute a published Transformation definition against staged data and write valid records into fixed `ppiq_plant` canonical entities while quarantining invalid records with typed reasons.

**Product concept:** This is the central customer-generic bridge: arbitrary customer source shape → one fixed PPIQ canonical model without customer-specific product code.

**Validation:**
- exact published Transformation version is resolved;
- runtime executes that version, not generic fallback behaviour;
- target fields are explicit and governed;
- valid rows reach canonical targets;
- invalid rows are quarantined with reason and source lineage;
- corrected rows can be reprocessed safely;
- run evidence records exact definition/version and counts.

---

## L2.4 — Genealogy resolution and attribution

**Class:** Core  
**Design:** Chapter 3 §4.2 DF6

**Description:** Resolve aliases and cross-stage material identity, build genealogy edges, and govern attribution/weighting across grains.

**Product concept:** Industrial questions often join upstream parent-grain process data to downstream child-grain quality/performance outcomes. Genealogy is the mechanism that makes that relationship defensible.

**Validation:**
- aliases resolve deterministically;
- lineage edges obey allowed relationship rules;
- cycles/invalid links are refused;
- attribution weights satisfy governed constraints;
- downstream queries can traverse the chain to evidence.

---

## L2.5 — Unified definition store and immutable target/version governance

**Class:** Core  
**Design:** Chapter 3 §4.5

**Description:** Give governed artifacts identity, immutable versions, publication status, dependency information and CurrentPublished/Pinned resolution.

**Product concept:** A job or dashboard must never mean "whatever the definition happens to be today." The exact version used must be knowable and reproducible.

**Validation:**
- immutable version creation;
- idempotent identical redeclaration where required;
- current/published/pinned resolution;
- deleting/deprecated target is refused where unsafe;
- run history stores exact resolved version;
- rollback/fork semantics are explicit.

---

## L2.6 — Job definition, scheduling, execution monitor and history

**Class:** Core  
**Design:** Chapter 4 §5.3.7

**Description:** Define, schedule, manually run, monitor, cancel and inspect jobs with truthful lifecycle/history.

**Product concept:** Jobs are the operational execution layer that turns authored product definitions into repeatable plant workflows.

**Validation:**
- create/edit schedule and manual run;
- admission/refusal is explicit;
- genuine run row only exists when admission occurs;
- status/progress/history are observable;
- cancellation and terminal states are correct;
- failure evidence is retained.

---

## L2.7 — Job dependency DAG

**Class:** Core  
**Design:** Chapter 4 §5.3.6

**Description:** Govern required/optional upstream jobs, target versions, staleness tolerance, cycle refusal and blocked-run evidence.

**Product concept:** Real production workflows are dependency graphs, not independent timers.

**Validation:**
- required and optional edges persist;
- self/cycle creation is refused;
- pinned/current dependency semantics are enforced;
- stale-acceptable evidence is explicit;
- failed upstream work blocks dependants truthfully;
- optional skipped dependency is distinguished from failure.

---

## L2.8 — Delta propagation law

**Class:** Core  
**Design:** Chapter 4 §5.3.9; Chapter 6 §6.5 condition 23

**Description:** Propagate bounded data change through acquisition, projection, analytics and downstream recomputation rather than globally recomputing by default.

**Product concept:** Incremental acquisition is not enough if every downstream job later rescans the whole plant.

**Validation:**
- each job family declares its delta scope;
- changed source range produces bounded downstream recomputation;
- no stale dependent result is silently retained;
- exact delta lineage is observable;
- full rebuild remains an explicit exceptional mode.

---

# 3. Layer 3 — Authoring, BI and the Workbench

**Scope:** J7, J10–J11 · DF7 · enterprise analytical authoring

## L3.1 — Analysis page anatomy, layout and visual standard

**Class:** Core  
**Design:** Chapter 4 §5.1.2–§5.1.8

**Description:** Create and operate analysis pages/sheets with governed layout, KPI/card/chart/table behaviour and consistent design standards.

**Product concept:** The analytical surface is customer-authored enterprise BI, not a fixed demo dashboard.

**Validation:**
- create/reopen/edit page;
- multiple sheets/sections;
- responsive persisted layout;
- view/edit mode separation;
- required page states and accessibility;
- saved layout reopens identically.

---

## L3.2 — Associative engine and selection strip

**Class:** Core  
**Design:** Chapter 4 §5.1.3, §5.1.17

**Description:** Selections in one visual constrain compatible data across the analytical workspace with clear active/possible/excluded state.

**Product concept:** Users investigate interactively rather than repeatedly editing static filters.

**Validation:**
- cross-widget selection propagation;
- visible selection state;
- clear/remove individual/all selections;
- correct relationship-path use;
- large-population behaviour does not silently truncate analytical truth.

---

## L3.3 — Dynamic filter authoring

**Class:** Core  
**Design:** Chapter 4 §5.1.4

**Description:** Author global/page/widget filters including list, search, date/range, relative time, numeric range, hierarchy, Top-N and expression filters.

**Product concept:** Filter inventory comes from governed customer metadata, not hardcoded plant vocabulary.

**Validation:**
- create/edit/delete/reuse filters;
- field/type compatibility enforced;
- time-zone semantics correct;
- filter scope is explicit;
- persisted filters survive reopen;
- stale/missing field is reported, never silently rebound.

---

## L3.4 — Widget catalogue and intelligence visualisation

**Class:** Core  
**Design:** Chapter 4 §5.1.5, §5.1.18–§5.1.19

**Description:** Create charts, tables, KPIs and intelligence widgets from governed datasets/results with correct role binding and chart semantics.

**Product concept:** The same ordinary BI surface must display both canonical facts and governed intelligence outputs without special demo-only widget branches.

**Validation:**
- widget type/roles selected from governed metadata;
- dimension/measure compatibility enforced;
- preview and persisted execution match;
- stale role binding is reported;
- intelligence output keeps provenance/evidence handles.

---

## L3.5 — One authoring shell, five purposes, two modes

**Class:** Core  
**Design:** Chapter 4 §5.2.1–§5.2.2

**Description:** A shared governed authoring shell supports the designed authoring purposes using Guided/Canvas and Advanced/SQL paths that produce the same class of definition.

**Product concept:** No-code and SQL are two interfaces to the same product authority, not parallel products.

**Validation:**
- same definition identity/version model from both modes;
- Guided → SQL always produces compiled representation;
- SQL → Guided only when representable;
- non-representable switch refuses clearly;
- shell behaviour is purpose-aware but not duplicated.

---

## L3.6 — Board semantics, typed ports and property inspection

**Class:** Core  
**Design:** Chapter 4 §5.2.6–§5.2.7, §5.2.16–§5.2.17

**Description:** Drag/drop authored graph with typed blocks/ports, legal connection rules, property inspectors and expression editing.

**Product concept:** A visual line between boxes is not enough; the board must encode executable semantics.

**Validation:**
- illegal connections refused at authoring time;
- block identity is stable;
- required properties are explicit;
- copy/delete/reconnect preserves graph consistency;
- graph serialisation round-trips identically.

---

## L3.7 — Authoring professionalism

**Class:** Core  
**Design:** Chapter 4 §5.2.8–§5.2.22

**Description:** Provide preview, dry-run, compiled SQL, debug log, undo/redo, dirty-state handling, version comparison, concurrency handling, keyboard/accessibility and truthful refusal states.

**Product concept:** Customer authoring must behave like a professional engineering tool, not a prototype canvas.

**Validation:**
- preview/dry-run before publish;
- compiled representation inspectable;
- deterministic debug diagnostics;
- undo/redo and unsaved-change protection;
- concurrent edit/version conflict is explicit;
- keyboard path and refusal/failure states are complete.

---

## L3.8 — Canvas → compile → governed job → run → per-block evidence

**Class:** Core  
**Design:** Chapter 4 §5.2.9; Chapter 2 §3.16

**Description:** Compile a validated authored board, bind it to the correct governed job target/version, execute it through the real job runtime, monitor progress and correlate every block/node to evidence.

**Product concept:** The Canvas becomes a product only when pressing Run executes the exact authored definition and the user can see what every block did.

**Validation:**
- compiled artifact binds to exact published definition/version;
- job family actually supports that target;
- execution never falls back to unrelated generic processing;
- stable authored block IDs correlate to runtime evidence;
- cancellation and typed block failure work;
- one block failure is isolated and explainable;
- run monitor shows genuine progress/results.

---

## L3.9 — Master items, bookmarks and saved views

**Class:** Core  
**Design:** Chapter 4 §5.1.16

**Description:** Create reusable dimensions, measures, filters, queries, hierarchies, drill paths, bookmarks and saved analytical views.

**Product concept:** Enterprise BI authoring needs reusable governed assets so each page is not a hand-built island.

**Validation:**
- reusable item has identity/version/owner;
- dependent pages reference rather than copy it;
- change impact is visible;
- bookmark/saved view restores selections/layout/context;
- permission and deletion rules protect dependencies.

---

# 4. Layer 4 — Intelligence Engines

**Scope:** J12–J13 · DF8–DF13 · statistical and learned intelligence

## L4.1 — Readiness gate and statistical discipline chain

**Class:** Core  
**Design:** Chapter 4 §5.4.3, §5.5.4

**Description:** Evaluate whether a requested analysis is defensible using population, missingness, coverage, effect size, multiplicity and other governed readiness criteria.

**Product concept:** PPIQ must refuse unsupported intelligence rather than manufacture a plausible answer.

**Validation:**
- readiness dimensions are measured, not guessed;
- each refusal names measured value and threshold;
- effect size precedes significance interpretation;
- multiple-comparison policy is enforced;
- downstream engine cannot bypass the gate.

---

## L4.2 — Statistical engines MF-05 / MF-06

**Class:** Core  
**Design:** Chapter 2 LB-7; Chapter 4 §5.5

**Description:** Descriptive, association/correlation, process and quality analytical engines using governed subjects, grains, measures and outcomes.

**Product concept:** Deterministic statistics are a first-class intelligence family, not a fallback for ML.

**Validation:**
- known-answer datasets;
- correct grain/subject;
- honest effect/statistical output;
- no causal upgrade from association;
- result persists with run/evidence/provenance;
- refusal when prerequisites fail.

---

## L4.3 — Feature engineering and incremental feature refresh

**Class:** Core  
**Design:** Chapter 4 §5.6.2

**Description:** Build governed model-ready features from canonical data with exact feature identity, version, lineage and incremental refresh.

**Product concept:** Models must consume reproducible features, not ad-hoc query code hidden inside training jobs.

**Validation:**
- feature definition/version persisted;
- leakage controls;
- exact training/serving parity;
- incremental refresh;
- source lineage and time cut-off preserved;
- recomputation is reproducible.

---

## L4.4 — Learned models MF-01 / MF-03 / MF-04

**Class:** Core  
**Design:** Chapter 2 LB-7

**Description:** Train/evaluate/register learned model families with mandatory simple baseline and governed data/feature manifests.

**Product concept:** A complex model earns deployment only when it demonstrably improves on a simpler baseline under the same evidence contract.

**Validation:**
- reproducible training manifest;
- simple baseline included;
- held-out evaluation;
- no leakage;
- calibration/quality metrics appropriate to subtype;
- model artifact and environment identity retained.

---

## L4.5 — Retrieval and similarity index MF-02

**Class:** Core  
**Design:** Chapter 2 LB-9

**Description:** Governed nearest-neighbour/retrieval capability for similarity-based intelligence, enabled only after measured recall/quality.

**Product concept:** Similarity is an engine with measurable retrieval quality, not "vector search exists."

**Validation:**
- tenant isolation;
- deterministic index population/version;
- measured recall@k or governed equivalent;
- query result evidence;
- latency/capacity measured;
- fallback/refusal when retrieval quality is insufficient.

---

## L4.6 — Practice learning MF-07

**Class:** Core  
**Design:** Chapter 4 §5.6.4a–b

**Description:** Learn operating-practice signatures, link them to outcomes, compare current behaviour to historically demonstrated good practice and back off similarity when evidence weakens.

**Product concept:** The product should identify the plant's own demonstrated best practice, not import generic operating advice.

**Validation:**
- comparable cohort/regime selection;
- practice signature version;
- outcome linkage from canonical data;
- similarity back-off/refusal;
- evidence drill-through to historical examples;
- no cross-tenant benchmarking.

---

## L4.7 — Prediction, drivers, explainability and remediation candidates

**Class:** Core  
**Design:** Chapter 4 §5.6.4c–d, §5.8.3

**Description:** Produce early prediction/risk with horizon and uncertainty, explain drivers, and generate evidence-backed remediation candidates subject to the full safety gate.

**Product concept:** A prediction without horizon, uncertainty, drivers and safe action context is not an operational product outcome.

**Validation:**
- future-only target separation/no leakage;
- horizon and uncertainty explicit;
- driver explanation is faithful to the model/evidence;
- prediction never stated as certainty;
- remediation candidate passes all safety checks;
- no automatic plant/control action.

---

## L4.8 — Model registry, promotion, serving and fallback

**Class:** Core  
**Design:** Chapter 4 §5.6.5–§5.6.7a

**Description:** Register model versions, compare candidate/champion, promote/activate/rollback, serve the correct version and fall back safely.

**Product concept:** Training completion is not deployment approval.

**Validation:**
- immutable model/version identity;
- promotion criteria across required dimensions;
- human/governed approval where required;
- serving resolves exact active version;
- fallback behaviour tested;
- drift/rollback evidence retained.

---

# 5. Layer 5 — Semantic Governance and Truth

**Scope:** cross-cutting semantic and evidence authority

## L5.1 — Canonical entity catalogue

**Class:** Core  
**Design:** Chapter 2 §3.14

**Description:** Fixed generic canonical entity/field vocabulary with no plant-, customer- or industry-specific vocabulary embedded in product logic.

**Product concept:** One binary must serve different process industries.

**Validation:**
- new customer terminology comes from data/registry;
- no second hardcoded business vocabulary;
- canonical entities have governed identity/grain/purpose;
- additions require design authority rather than customer-specific patching.

---

## L5.2 — Permanent relationship model

**Class:** Core  
**Design:** Chapter 2 §3.15; LB-4

**Description:** Govern relationships, members, roles, cardinality, preferred paths and grain conversions as reusable product data.

**Product concept:** Downstream consumers should query one published relationship model rather than implement joins independently.

**Validation:**
- relationship membership/cardinality persisted;
- preferred path deterministic;
- ambiguous path refused or resolved by authority;
- downstream consumer uses the authority;
- no duplicate relationship registry.

---

## L5.3 — Analysis Subject, grain and signal aggregation authority

**Class:** Core  
**Design:** Chapter 2 LB-2 §2.1–§2.2

**Description:** Define exactly what entity/population/time/grain a computation analyses and how signals aggregate or convert across grains.

**Product concept:** "Temperature affects defects" is meaningless unless the subject, population, grain and aggregation path are explicit.

**Validation:**
- subject/grain required for governed analyses;
- illegal grain conversion refused;
- aggregation semantics centrally defined;
- result carries resolved subject/grain;
- same request reproduces same population semantics.

---

## L5.4 — Regime correctness

**Class:** Core  
**Design:** Chapter 4 §5.4.11

**Description:** Distinguish Stable, Transition, Stabilising, Mixed and Unknown operating regimes and apply declared stabilisation logic before attributing analytical meaning.

**Product concept:** A mathematically strong result on the wrong operating regime is an industrial correctness failure.

**Validation:**
- regime is derived from governed facts/rules;
- transitions are not silently mixed into stable cohorts;
- stabilisation period is declared;
- Mixed/Unknown handling is explicit;
- intelligence result carries regime context/refusal.

---

## L5.5 — Provenance, evidence chain, claim guard and refusal integrity

**Class:** Core  
**Design:** Chapter 2 LB-5

**Description:** Preserve evidence from source/import through canonical fact, analysis/model run, result, widget and Assistant answer.

**Product concept:** Every material number/claim must be able to answer "where did this come from?"

**Validation:**
- evidence handle resolves;
- numeric claim is supported by cited evidence;
- unit/quantity/subject match;
- unsupported causal/value upgrade refused;
- transport failure is not rewritten as "no evidence."

---

## L5.6 — Unified definition-store semantics

**Class:** Core  
**Design:** Chapter 3 §4.5

**Description:** Common identity/version/publication/dependency rules across governed authored artifacts.

**Product concept:** Transformation, Canvas, model, analysis, page/query and other authored assets must not each invent their own lifecycle semantics.

**Validation:**
- one canonical lifecycle contract;
- immutable versions;
- dependency impact;
- publication/rollback;
- consistent tenant/ownership/permission semantics.

---

## L5.7 — Physical database governance and zero-unknown catalogue

**Class:** Core  
**Design:** Database Standard §8

**Description:** Every physical product object is classified by schema, lifecycle, owner, purpose and creator; generated catalogue remains synchronized with executable schema authority.

**Product concept:** A table count is not a defect; an object of unknown authority is.

**Validation:**
- fresh canonical database catalogue is complete;
- zero unknown product objects;
- creator provenance resolves;
- governed-schema classification deterministic;
- new DDL without catalogue convergence turns the gate red;
- no automatic destructive cleanup of long-lived customer data.

---

## L5.8 — Truth contracts and claim boundaries

**Class:** Core  
**Design:** Chapter 2 LB-5; LB-2 §2.4

**Description:** Enforce semantic boundaries including association ≠ cause, prediction ≠ certainty, recommendation ≠ control, and evidence reconciliation confidence.

**Product concept:** The product must not become more confident merely because the wording layer is persuasive.

**Validation:**
- typed claim class accompanies evidence;
- language cannot upgrade claim class;
- refusal survives rendering;
- causal claims require their designed evidence standard;
- recommendation remains human-reviewed and non-control.

---

# 6. Layer 6 — Assistant, Operations and Global Shell

**Scope:** J15 · DF15 · Assistant plus operational/global experience surfaces

## L6.1 — Persistent Assistant dock and context envelope

**Class:** Core  
**Design:** Chapter 4 §5.7.1–§5.7.2

**Description:** Assistant is available as a persistent authenticated dock and receives current route/page/widget/selections/filter context.

**Product concept:** The Assistant should understand what the user is looking at while remaining grounded in product evidence.

**Validation:**
- dock persists across navigation;
- conversation survives intended navigation/session scope;
- context reaches retrieval;
- context is not trusted as evidence by itself;
- same question on different pages can retrieve different evidence.

---

## L6.2 — Governed Assistant runtime

**Class:** Core  
**Design:** Chapter 4 §5.7.9; Chapter 2 LB-13

**Description:** Deterministic tool planning, retrieval, evidence packing, citations, answer verification and refusal over governed product tools.

**Product concept:** The Assistant orchestrates product intelligence; it does not invent independent calculations.

**Validation:**
- tool plan only invokes allowed tools;
- retrieved evidence is tenant-scoped;
- every numeric/material claim is verified;
- citations support the actual claim;
- unsupported question refuses;
- model/provider failure remains a distinct failure state.

---

## L6.3 — Supervisor and governed improvement proposals

**Class:** Core  
**Design:** Chapter 4 §5.4.9

**Description:** Supervisor observes performance, proposes bounded adjustments, shadow-runs them and requires human approval before activation.

**Product concept:** The system may improve its configuration, but never by silently weakening evidence/readiness/safety rules.

**Validation:**
- proposal is bounded and versioned;
- shadow comparison uses held-out evidence;
- protected rules cannot be modified;
- human approval mandatory;
- rollback pointer/provenance retained;
- abstention recorded.

---

## L6.4 — Alert routing and escalation

**Class:** Core  
**Design:** Chapter 4 §5.8.2

**Description:** Route governed alerts to configured channels with deduplication, quiet-period hold-not-drop semantics, escalation and dead-letter visibility.

**Product concept:** Operational intelligence must reach the right person without becoming noisy or silently disappearing.

**Validation:**
- routing rule authoring;
- deduplication;
- quiet period queues rather than loses alert;
- delivery result observable;
- failed delivery goes to visible dead-letter;
- escalation obeys policy.

---

## L6.5 — Reports, export and scheduled delivery

**Class:** Core  
**Design:** Chapter 3 E5

**Description:** Generate governed reports/exports and schedule delivery while preserving filters, time, provenance and evidence.

**Product concept:** Analysis must be distributable outside the live dashboard without becoming detached from its truth.

**Validation:**
- report is generated from governed result;
- selected filters/time scope preserved;
- citations/evidence survive export where applicable;
- scheduled delivery has audit trail;
- failed delivery is visible.

---

## L6.6 — Plant data log, audit, log channels, retention and archival

**Class:** Core  
**Design:** Chapter 3 E3, F5–F6, F9

**Description:** Expose product/plant-data operational logs, audit events, channel configuration, retention and archival.

**Product concept:** Users need operational truth without reading server files or database internals.

**Validation:**
- log categories/channels are explicit;
- retention policy enforced;
- audit events are attributable;
- archival/retrieval proven;
- sensitive values redacted;
- UI distinguishes audit, job and plant-data evidence.

---

## L6.7 — Global shell components G1–G6

**Class:** Core  
**Design:** Chapter 3 §4.4b

**Description:** Provide the designed global authenticated shell including search/command palette, notifications, refusal boundary and activity surfaces.

**Product concept:** Cross-cutting product actions and state should behave consistently on every authenticated page.

**Validation:**
- all six designed shell components exist on correct surfaces;
- keyboard/accessibility path;
- global search/command results obey permissions;
- refusal/error boundary is consistent;
- activity/notifications do not disappear on navigation.

---

## L6.8 — Settings, time zone, units, localisation and translation

**Class:** Core  
**Design:** Chapter 3 F7–F8

**Description:** Govern tenant/plant/user settings that affect time, units, locale and translated product text.

**Product concept:** Industrial interpretation depends on correct local time and units; localisation is not merely cosmetic.

**Validation:**
- plant time zone persisted and used consistently;
- UTC/local representations remain distinguishable;
- unit/quantity semantics remain valid;
- locale/translation switch is stable;
- untranslated/internal tokens are not exposed to users.

---

# 7. Layer 7 — Security, Licence and Commercial

**Scope:** J1–J3 · administration, entitlement, capacity and commercial handover

## L7.1 — Users, roles, permissions, MFA and enterprise identity

**Class:** Core  
**Design:** Chapter 6 §6.3.2

**Description:** Manage authenticated users, roles/permissions and enterprise identity integration including MFA/SSO/SCIM/OIDC where specified.

**Product concept:** "May this person do this?" is a separate question from whether the installation purchased the capability.

**Validation:**
- least-privilege role enforcement;
- unauthorized API and UI actions refuse;
- admin/user lifecycle;
- identity integration path;
- security events audited.

---

## L7.2 — Tenant isolation

**Class:** Core  
**Design:** Chapter 2 LB-15

**Description:** Prevent cross-customer access to data, jobs, definitions, models, embeddings, evidence and Assistant retrieval.

**Product concept:** Tenant boundary is absolute and must survive every new subsystem.

**Validation:**
- cross-tenant positive attack tests;
- model/retrieval indexes tenant-scoped;
- worker/job queries scoped;
- evidence handles cannot cross tenant;
- caches/object storage obey the same boundary.

---

## L7.3 — Licence and feature entitlement

**Class:** Core  
**Design:** Chapter 6 §6.3.1, §6.3.3

**Description:** Enforce signed installation entitlement independently from user role.

**Product concept:** A user may have permission for a feature that the installation did not purchase, or vice versa; both checks are required.

**Validation:**
- signed entitlement verification;
- feature unavailable when not entitled;
- role cannot bypass entitlement;
- licence state visible to admin;
- expired/invalid token has explicit behaviour.

---

## L7.4 — Authoring quota and capacity guardrail

**Class:** Core  
**Design:** Chapter 6 §6.3.4

**Description:** Enforce purchased/operational capacity limits independently from feature entitlement.

**Product concept:** "Feature is included" is different from "how much of it may this installation operate?"

**Validation:**
- metered dimensions are explicit;
- approaching/exceeding guardrail visible;
- refusal/degradation policy defined;
- no silent overrun;
- audit/usage evidence available.

---

## L7.5 — Secret and credential management

**Class:** Core  
**Design:** Chapter 3 §4.6

**Description:** Protect source credentials, certificates, signing keys and other secrets across storage, UI, logs and deployment.

**Product concept:** Read-only architecture is meaningless if credentials leak.

**Validation:**
- secrets never returned to frontend;
- logs redact;
- repository/artefact secret scan;
- secure storage/reference;
- rotation and invalid-secret failure path.

---

## L7.6 — Commercial capacity model and licence-price function

**Class:** Core  
**Design:** Chapter 6 §6.3.4, §6.3.6

**Description:** Calculate purchased capacity and licence price using the designed commercial dimensions.

**Product concept:** Price is a commercial function informed by capability/capacity, not the same thing as infrastructure cost.

**Validation:**
- canonical units;
- deterministic quote inputs;
- tier/capacity rules explicit;
- generated offer/handover reproduces same calculation;
- no infrastructure-cost value silently used as sale price.

---

## L7.7 — Infrastructure cost formula

**Class:** Core  
**Design:** Chapter 6 §6.3.5

**Description:** Calculate expected infrastructure requirements/cost independently from licence price.

**Product concept:** The product must know what it costs to run at a given envelope without confusing that number with what the customer pays.

**Validation:**
- canonical inputs/units;
- sizing inputs trace to measured/calibrated constants;
- cost components transparent;
- on-prem/private-cloud interpretation supported;
- unknown calibration is labelled, not guessed.

---

## L7.8 — Sales Administration and sales-to-engineering handover

**Class:** Core  
**Design:** Chapter 6 §6.3.8, §6.3.10

**Description:** Capture commercial/customer configuration and hand over an implementation-ready package to engineering.

**Product concept:** A signed customer should not restart discovery from emails/spreadsheets.

**Validation:**
- customer/site/tier/capacity/topology captured;
- connector/infrastructure prerequisites included;
- licence/entitlement package reproducible;
- handover completeness validation;
- engineering receives one governed record.

---

# 8. Layer 8 — Infrastructure, Scale and Release

**Scope:** Chapter 6 §6.1 · operability and release truth

## L8.1 — Fresh install and upgrade authority

**Class:** Core  
**Design:** Chapter 6 §6.1.4

**Description:** Install a new customer from zero through one canonical migration order and upgrade an existing customer without data loss.

**Product concept:** A product that only works on the developer's evolved database is not deployable.

**Validation:**
- empty database builds from canonical authority;
- no hidden manual repair/seed dependency;
- upgrade path from supported prior state;
- customer data preserved;
- migration/EF/SQL authority converges;
- repeatability proven.

---

## L8.2 — Test architecture with disposable isolated databases

**Class:** Core  
**Design:** Chapter 6 §6.1.5

**Description:** Certification/integration tests and corrective packs use owned disposable databases/environments, never mutate shared persistent runtime databases as fixtures, and always clean up.

**Product concept:** Test evidence is trustworthy only when tests cannot damage or inherit state from long-lived product databases.

**Validation:**
- unique disposable database per run/lane;
- induced test failure leaves zero orphan DBs;
- native-command failure leaves zero orphan DBs;
- timeout leaves zero orphan DBs;
- two parallel suites receive distinct DB identities;
- protected persistent DBs are unchanged;
- owned API/process resources are isolated and cleaned;
- machine-readable test result agrees with process verdict.

---

## L8.3 — Deployment topologies

**Class:** Core  
**Design:** Chapter 6 §6.1.2–§6.1.3

**Description:** Deploy the same component set across supported laptop/dev, on-premise, private-cloud and air-gapped/enterprise topologies.

**Product concept:** Topology changes placement/operation, not product semantics.

**Validation:**
- topology-specific configuration only;
- same product components/contracts;
- network boundaries enforced;
- customer-source connection model remains read-only;
- install/health path proven for each supported topology.

---

## L8.4 — Execution lanes, admission and resource isolation

**Class:** Core  
**Design:** Chapter 2 LB-12; Chapter 4 §5.3.2

**Description:** Schedule work through governed execution lanes using concurrency and compute-weight/capacity admission rules, with reserved interactive/online capacity.

**Product concept:** Hundreds of defined jobs must not starve interactive analysis or model serving.

**Validation:**
- admission predicate implemented from one authority;
- lane/class assignment deterministic;
- online lane cannot be consumed by batch/training;
- saturation/refusal/queue state visible;
- cancellation/pre-emption policy proven where designed.

---

## L8.5 — Sizing model and calibrated performance constants

**Class:** Core  
**Design:** Chapter 6 §6.1.9

**Description:** Size infrastructure from ingest rate, retained volume, concurrent users, job/model load and calibrated runtime constants.

**Product concept:** Hardware recommendations must be derived and labelled with calibration status, not guessed.

**Validation:**
- canonical input units;
- formula reproducible;
- constants tagged measured/estimated/unmeasured;
- 12-month headroom rule applied where designed;
- under-provisioning observable.

---

## L8.6 — Monitoring, observability, health and readiness

**Class:** Core  
**Design:** Chapter 6 §6.1.12

**Description:** Observe API, workers, DB, connectors, queues, model serving and scan/query amplification with actionable health/readiness signals.

**Product concept:** Production incidents must be diagnosable without attaching a debugger.

**Validation:**
- component health/readiness endpoints;
- metrics and logs correlated to run/request;
- capacity/saturation signals;
- failure alerts;
- scan amplification/performance bands visible;
- tenant/security-safe diagnostics.

---

## L8.7 — Backup, restore, PITR, DR and HA

**Class:** Core  
**Design:** Chapter 6 §6.1.11

**Description:** Protect PostgreSQL/object data with backup, restore, point-in-time recovery, disaster recovery and HA appropriate to deployment tier.

**Product concept:** Backups count only when restore is proven.

**Validation:**
- scheduled backup;
- restore rehearsal;
- PITR proof;
- RPO/RTO measured against tier;
- replica/failover/fencing where applicable;
- object/model artifact recovery included.

---

## L8.8 — C1–C4 performance certification and golden-journey release gate

**Class:** Core  
**Design:** Chapter 6 §6.1.5.8; §6.5 condition 26

**Description:** Execute the Master Design's **C1–C4 performance certification** profiles and the golden-journey release gate on a truthful, isolated environment. This design term is unrelated to this standard's D1–D5 completeness dimensions.

**Product concept:** Release status must be evidence from real end-to-end execution, not a collection of green unit suites.

**Validation:**
- C1–C4 profiles executed as designed;
- golden customer journey proven;
- machine-readable evidence envelope;
- no inherited failure hidden;
- no skipped required step;
- exact product/revision/environment captured;
- release verdict reproducible.

---

# 9. Layer 9 — Decision, Value and Business Outcome

**Scope:** J13–J14 · DF13–DF14 · turning intelligence into governed human action and measurable value

## L9.1 — Performance Reference

**Class:** Core  
**Design:** Chapter 2 §3.10

**Description:** Allow the customer to define exact governed standards, targets, operating envelopes and benchmarks per relevant machine/stage/product/context.

**Product concept:** "Good" must be a governed customer fact where the customer has a declared standard, not an AI assumption.

**Validation:**
- references are versioned and scoped;
- units/context/grain explicit;
- effective dates supported;
- analyses resolve the correct reference;
- no hidden default benchmark.

---

## L9.2 — Suggestion generation from findings

**Class:** Core  
**Design:** Chapter 3 §4.5

**Description:** Generate evidence-linked suggestions from governed findings with expected-effect context.

**Product concept:** A suggestion is a candidate for human consideration, not an automatic action or ungrounded recommendation.

**Validation:**
- source finding/evidence linked;
- applicability context explicit;
- expected effect bounded/qualified;
- unsafe or unsupported suggestion refused;
- no write path to plant/control system.

---

## L9.3 — Decision capture

**Class:** Core  
**Design:** Chapter 4 §5.8.4

**Description:** Record human accept/reject decision with governed reason handling.

**Product concept:** The product must know whether a recommendation was acted upon before later judging effectiveness.

**Validation:**
- accept/reject persisted with actor/time;
- rejection reason required when designed;
- reason codes registry-driven;
- decision immutable/audited appropriately.

---

## L9.4 — Action tracking

**Class:** Core  
**Design:** Chapter 4 §5.8.4

**Description:** Record the actual human action taken, stage/context and timestamp.

**Product concept:** Accepted suggestion and real plant action are not the same event.

**Validation:**
- action can reference decision;
- actual action details/time captured;
- no false implication that PPIQ executed the control action;
- evidence/audit trail retained.

---

## L9.5 — Canonical outcome capture

**Class:** Core  
**Design:** Chapter 4 §5.8.4

**Description:** Determine outcome from canonical plant data rather than a hand-entered "success/failure" result.

**Product concept:** Feedback cannot train the product if the outcome can be manually declared.

**Validation:**
- outcome is resolved from governed canonical facts;
- hand-entered outcome is refused;
- observation window/cohort defined;
- unresolved outcome remains unknown rather than guessed.

---

## L9.6 — Effectiveness evaluation

**Class:** Core  
**Design:** Chapter 4 §5.8.4

**Description:** Evaluate prediction and remediation effectiveness against the correct comparison cohort.

**Product concept:** The system should learn whether the prediction/remediation worked, not merely whether a user clicked Accept.

**Validation:**
- prediction verdict and remediation verdict distinct;
- comparison cohort/regime governed;
- uncertainty/confounding handled according to design;
- result linked to original suggestion/decision/action.

---

## L9.7 — Value engine

**Class:** Core  
**Design:** Chapter 3 D7

**Description:** Convert measured operational effect into a bounded financial range with drill-through assumptions.

**Product concept:** Business value is an evidence-backed range, never an invented single "savings" number.

**Validation:**
- cost/value inputs traceable;
- lower/upper bound methodology;
- assumptions visible/editable where designed;
- value links to measured outcome;
- unsupported value claim refuses.

---

## L9.8 — Feedback quality and poisoning prevention

**Class:** Core  
**Design:** Chapter 4 §5.8.4

**Description:** Govern the quality of captured feedback before it can influence retraining or future recommendations.

**Product concept:** A feedback loop that trusts every click becomes a poisoning mechanism.

**Validation:**
- provenance of feedback;
- minimum quality/eligibility gates;
- human review before retraining where designed;
- anomalous/malicious feedback isolated;
- training manifest records accepted feedback set.

---

# 10. Layer 10 — Analytical Business Products

**Scope:** J13 · advanced and cross-cutting analytical business products and investigation surfaces

This layer is separated from Layer 9 because Decision/Feedback is an operational closed loop, while the capabilities below are analytical business products that package, compare or extend governed intelligence.

## L10.1 — Internal benchmarking

**Class:** Core  
**Design:** Chapter 4 §5.8.5

**Description:** Compare like-for-like plant units/products/practices using registry-driven internal cohorts.

**Product concept:** Benchmarking must compare comparable operating contexts, not simply rank raw numbers.

**Validation:**
- cohort dimensions registry-driven;
- tenant-local only;
- grain/context normalization;
- insufficient comparable population refuses;
- evidence drill-through.

---

## L10.2 — Period driver decomposition

**Class:** Core  
**Design:** Chapter 4 §5.4.12

**Description:** Deterministically decompose why a period improved or deteriorated into measured Layer-A drivers before narrative interpretation.

**Product concept:** "Why was this month worse?" should begin with exact measured contribution structure, not an LLM story.

**Validation:**
- exact period/population;
- deterministic driver decomposition;
- contributions reconcile to measured change within designed rules;
- residual/unknown contribution remains explicit;
- Assistant may explain but not alter the decomposition.

---

## L10.3 — Operational evidence reconciliation

**Class:** Advanced  
**Design:** Chapter 2 §3.10

**Description:** Compare independent operational evidence sources and identify agreement/contradiction/confidence.

**Product concept:** A plant event recorded differently in MES, historian and operator/audit evidence should not silently collapse into one "truth."

**Validation:**
- independent evidence origins retained;
- reconciliation rule explicit;
- contradiction visible;
- confidence cannot exceed evidence class;
- original evidence remains drillable.

---

## L10.4 — Scenario simulation

**Class:** Advanced  
**Design:** Chapter 4 §5.8.1

**Description:** Evaluate governed what-if scenarios with uncertainty rather than presenting a point estimate as fact.

**Product concept:** Scenario simulation is decision support, not prediction certainty.

**Validation:**
- scenario assumptions explicit;
- bounded valid input space;
- uncertainty interval;
- comparison baseline;
- no automatic control action;
- unsupported extrapolation refused.

---

## L10.5 — Insight Board Composer

**Class:** Advanced  
**Design:** Chapter 2 §3.10

**Description:** Compose findings, charts, evidence, predictions, recommendations and narrative into a governed reusable investigation/insight board.

**Product concept:** Complex investigations should become persistent, reviewable product artifacts rather than screenshots and chat transcripts.

**Validation:**
- every embedded item keeps source/evidence identity;
- board is versioned;
- stale underlying evidence indicated;
- sharing/permissions enforced;
- export preserves attribution.

---

# 11. Explicit exclusions and future extensions

These are not "red backlog gaps."

| Capability / boundary | Classification | Product rule |
|---|---|---|
| Write path from PPIQ to customer plant/control system | **Excluded** | PPIQ remains read-only to customer systems and never writes setpoints/control commands. |
| Autonomous application of model/threshold changes | **Excluded** | Human approval is structural. |
| MES / QES / Yard-Warehouse / Energy Management as PPIQ capabilities | **Boundary, not gap** | Separate SOU products, not hidden PPIQ backlog. |
| Unstructured text evidence | **Future extension** | May have designed boundary/lineage model but is not Core completion. |
| Inspection images | **Future extension** | May have metadata/storage/linkage design but is not Core completion. |

---

# 12. Backlog Traceability Standard

Every backlog row must declare which product capability it exists to fulfil.

## 11.1 Required backlog columns

Add the following columns to the backlog:

| Column | Required | Meaning |
|---|---|---|
| **Primary Capability ID** | Yes | One stable ID such as `L8.2`, or `NO-CAP — Execution Tooling` when the work is purely an execution/instrumentation aid and does not itself implement or certify a product capability. |
| **Secondary Capability IDs** | When applicable | Semicolon-separated IDs such as `L2.3; L3.8`. Do not duplicate the Primary ID. |
| **Capability Contribution** | Yes | `Build`, `Converge`, `Correct`, `Validate`, `Harden`, `Certify`, `Retire` or `Research`. |
| **Completeness Dimensions Targeted** | Yes | One or more of `D1; D2; D3; D4; D5`. |
| **Capability Requirement Class** | Derived | `Core`, `Advanced`, `Future`, `Excluded/Boundary`. Normally populated from this standard. |
| **Release Scope** | Yes for capability work | `R1`, `R2`, `Later`, or staged scope such as `R1 foundation; R2 full`. This is capability/release scope, not task due date. |
| **Capability Closure Effect** | Yes | `No maturity change`, `Advances`, or `Closes capability`. A task may close only with evidence. |
| **Capability Evidence** | At closure | Commit, runtime proof, test/gate, DB/browser evidence or evidence-pack reference. |

## 11.2 Mapping law

1. Every product implementation/validation task must have **one Primary Capability ID**.
2. A task may have multiple Secondary Capability IDs only when its acceptance truly advances them.
3. Do **not** map a task to a capability merely because it touches the same module.
4. Corrective/hardening tasks map to the capability whose product truth they restore.
5. **Pure execution tooling** (for example an apply runner, one-off migration runner, or evidence-harvesting instrument that changes no product capability and provides no release certification itself) uses `NO-CAP — Execution Tooling`; it must not be forced into Layer 8.
6. **Release/certification tooling that implements or proves a Layer-8 release capability** maps to that Layer-8 capability.
7. A task that discovers a defect in another capability does not automatically become owner of that capability.
8. One capability can require many tasks; capability completeness is calculated from evidence, not from task count.
9. Closing all mapped tasks does **not** automatically make the capability `5/5`; D1–D5 evidence still governs.

## 11.3 Examples

| Task | Primary Capability | Secondary | Contribution | Dimensions | Release | Rationale |
|---|---|---|---|---|---|---|
| **T-252** | **L8.2** | L8.8 when its evidence is consumed by release certification | Harden / Validate | D3; D4; D5 | R1 | Disposable DB/process isolation and truthful test lifecycle. |
| **T-251** | **L5.7** | L8.1 | Build / Validate | D4; D5 | R1 | Physical catalogue and zero-unknown schema governance. |
| **T-261** | **L2.3** | L3.8; L2.6 | Build | D3; D5 | R1 | Real governed Transformation executor enables canonical projection and Canvas-run path. |
| **T-245** | **L3.8** | L2.6 | Build / Validate | C2; D3; D5 | Compile/bind/run/monitor/per-block evidence from Canvas. |
| **T-106** | **L2.6** | L2.5; L2.7 | Build / Converge | D3; D4; D5 | R1 | Job target/version/capability/dependency runtime governance. |
| **T-243** | **L2.5** | L3.5; L3.6 | Build | D3; D4; D5 | R1 | Immutable Canvas definition persistence/reopen/version semantics. |

---

# 13. Product Capability Status Register template

The Product Owner status should be maintained separately from the backlog but may live in the same workbook.

Recommended columns:

| Field | Meaning |
|---|---|
| Capability ID | Stable key, e.g. `L2.3` |
| Layer | Layer name |
| Capability | Short name |
| Requirement Class | Core / Advanced / Future / Excluded |
| **Release** | `R1`, `R2`, `Later`, or staged scope such as `R1 foundation; R2 full` |
| Journey / DF | Canonical J/DF reference |
| D1 Journey | Y / Partial / N / N/A |
| D2 UI | Y / Partial / N / N/A |
| D3 API | Y / Partial / N / N/A |
| D4 Data | Y / Partial / N / N/A |
| D5 Acceptance | Y / Partial / N / N/A |
| Score | normalized x/5 |
| Current Evidence | strongest current proof |
| Current Gap | exact missing product outcome |
| Primary Open Tasks | backlog items currently advancing the gap |
| Last Evaluated | date |
| **Evidence Freshness** | Current / Stale — re-evaluation required |
| Product Owner Verdict | Complete / Near-complete / Incomplete / Foundation / Not active |

---

# 14. Product reporting standard

When reporting "How far is PlantProcess IQ?", do not use backlog task completion as the product-completion metric.

Report:

1. **Core Completeness** — evidence-weighted D1–D5 completeness for Core capabilities.
2. **Core capability distribution** — count at 5/5, 4–4.5, 3–3.5, below 3.
3. **Advanced Completeness** — separate.
4. **Future Extensions** — inventory only, not scored against Core.
5. **Excluded/Boundary** — explicitly listed so they are never mistaken for debt.
6. **Top product gaps** — the lowest or most journey-critical Core capabilities, named by Capability ID.
7. **Release-specific readiness** — subset of capability IDs required for that release/customer journey.

The executive release metric is:

`R1 Core Completeness = earned applicable D-points on Core capabilities in R1 scope / total applicable D-points on Core capabilities in R1 scope`

The long-term product metric remains **Overall Core Completeness**. R1/R2/Later assignment must be explicit in the Capability Status Register and must never be inferred from task due dates.

Example:

```text
Overall Core completeness: <TBD>%

R1 Core completeness: <TBD>%
R2 Core completeness: <TBD>%

Core capabilities:
- <TBD> complete at 5/5
- <TBD> near-complete at 4–4.5/5
- <TBD> substantial but incomplete at 3–3.5/5
- <TBD> below 3/5

Advanced completeness: <TBD>%
Future extensions: not included in Core score
Excluded boundaries: <TBD>

Largest Core product gaps:
- <TBD capability ID and name>
- <TBD capability ID and name>
- <TBD capability ID and name>
```

---

# 15. Design-gap and evidence-freshness rules

## 15.1 Design-gap state

A capability may exist clearly in the Master Design while one required design artifact needed to satisfy its validation is still unspecified.

Typical shape:

- the capability itself exists and remains valid;
- current implementation may satisfy some D1–D5 dimensions;
- a required contract, producer, mapping, persistence shape or authority is absent or ambiguous;
- implementation cannot truthfully close the capability without inventing design.

Record this as:

`DESIGN GAP — capability validation cannot be completed because required design artifact is unspecified`

Rules:

1. Keep the capability in its existing Requirement Class.
2. Score only what is actually implemented/proven; do not automatically zero unrelated dimensions.
3. Record the exact missing design artifact in `Current Gap`.
4. Route the missing artifact to Design authority.
5. Do not invent the missing contract in implementation merely to close a task.
6. The capability remains incomplete until the design artifact is specified, implemented and accepted.

This state is different from:
- an implementation defect;
- a failed acceptance test;
- a backlog omission;
- a Future extension.

## 15.2 Evidence freshness

A capability score is a statement about a particular implemented state and therefore expires when the underlying product changes.

**Freshness rule:** when a task mapped to a capability as Primary or Secondary closes, that capability's current score becomes **Stale — re-evaluation required**.

It must be re-evaluated before:
- the next Product Owner scorecard is published;
- a release-readiness metric using that capability is reported;
- the capability is claimed complete to a customer.

A task explicitly classified `NO-CAP — Execution Tooling` does not expire a capability score unless it materially changes the evidence instrument used to establish that score. If the evidence instrument changes materially, affected capability evidence must be revalidated.

Re-evaluation may preserve the same score. The rule requires re-proof, not an automatic score reduction.

---

# 16. Governance of this standard

This standard should change only when:

- the Master Design adds/removes/reclassifies a product capability;
- a capability was incorrectly split/merged and the Design proves the correction;
- a new canonical journey/DF requirement is formally added;
- the evaluation methodology itself is formally revised;
- a capability's validation exposes an unspecified required design artifact **and the resulting Design decision changes that capability's contract, validation standard or taxonomy**.

A discovered Design Gap by itself does not silently rewrite this standard. It is first recorded under §15.1 and routed to Design.

It should **not** change because:

- a backlog task was added;
- a worker chose a different implementation;
- a test failed;
- a task moved between workers;
- a corrective commit was required.

Backlog tasks change frequently.

**Product capability IDs should be stable.**

That stability is what allows the backlog, roadmap, release reports and future customer-readiness scorecards to speak the same Product Owner language.

## Integrated visual-assessment and book-delivery rules

**Owner review incorporated 14 September 2026.** This is a clarification of assessment and storage, not an expansion of product scope. Stable capability IDs and the existing D1–D5 definitions are retained.

1. UI acceptance uses the semantic chain `requirement -> control/interaction -> frame/state -> action -> observable result -> owner -> evidence`. Literal label matches, word counts and frame counts are triage signals, not proof of feature completion.
2. Reuse of shell/components is valid. C1 hosts S1; they do not need artificially different rectangles. Review selected Source/Join/Output states, typing/wiring/refusal, diagnostics-to-node focus and the complete authoring transition.
3. The quarantine code catalogue comes from its governed authority; no fixed eight-group UI acceptance limit is permitted.
4. M2 selection, clear and propagation remain in T-265's visual scope. Advanced M3 parity remains in T-267. A visual gap does not reopen the already accepted associative engine without concrete regression.
5. OPC designs distinguish the target operational state from the currently unavailable/refused state. Neither diagram proves live acquisition.
6. The allegation of an incorrect discovery citation to §C4 is withdrawn: DLG-C4-entity belongs to §C4. Checking all 87 citations remains preventive coverage, not evidence that all are defective.
7. Source inspection, automated execution, browser evidence and prototype/Figma evidence remain separate grades. Producing or importing a design does not constitute product acceptance.
8. The book consists only of the files registered in PPIQ_Definition.md. Findings, scores, issue dispositions, validation results and coverage live in the latest Backlog workbook; the present document owns the evaluation method, not another current-status ledger.

The v4.10.3 drawings are retained as the existing illustrative baseline. Their improvement is not claimed complete by file consolidation. The latest Backlog remains the owner of T-265/T-266/T-267 delivery and acceptance.
