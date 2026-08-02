# PPIQ MASTER DESIGN - SESSION HANDOVER

**From:** design session of 29 July to 2 August 2026
**To:** the next session
**Author of the project:** Karim, SOU Industrial Software, Dusseldorf
**Handover written:** 2 August 2026

---

# 0. READ THIS FIRST - WHAT THIS SESSION WAS AND WAS NOT

**This was a DESIGN session. It produced documents. It did not touch the running system.**

The next session must not assume otherwise, because the handover request asked for test results, pipeline fixes and deployment work that **did not happen here**. Stating that plainly is more valuable than inventing it.

| Activity | Happened in this session? | Evidence |
|---|---|---|
| Read Karim's ~40 draft and handover documents | **YES** | Section 3 lists every one |
| Structurally mine the 23 MB repository dump | **YES** | Section 4, all counts are measured |
| Write the six Master Design chapters | **YES** | Section 2, ~9,900 lines delivered |
| Compute scale, sizing and capacity arithmetic | **YES** | Section 7, reproducible scripts included |
| Rule open design questions with Karim | **YES** | Section 6, 11 ruled + open list |
| **Modify any source file in his repository** | **NO** | Nothing was written to his codebase |
| **Run any test against his application** | **NO** | No test was executed at any point |
| **Deploy anything** | **NO** | No deployment occurred |
| **Touch Jenkins or any pipeline** | **NO** | The pipeline was read about, never run or fixed |
| **Access his server, database or app URL** | **NO** | No credential was ever used; `commands.txt` left context early and was never re-supplied |
| **Make the pipeline green or fix an app URL** | **NO** | This never occurred and no such work exists to hand over |

> **If the next session is asked "what were the test results" or "how did you make the pipeline green", the honest answer is: no tests were run and no pipeline work was done in the design session. The design specifies what tests must exist (Chapter 6 6.1.5) and what the pipeline must do (Chapter 6 6.1.3), but none of it has been executed.**

**What the next session inherits is a complete, internally consistent design corpus and a precise map of the implementation gap.** That is worth a great deal and it is not the same as an execution record.

---

# 1. THE PROJECT IN ONE PAGE

**PlantProcess IQ (PPIQ)** is a generic, read-only, evidence-grade process-to-quality intelligence platform for manufacturing plants in any process industry. It connects fragmented plant databases through a one-way collector, stages source-shaped copies, maps them into a canonical model through a customer-authored versioned mapping, and then provides unified visibility, statistics, machine learning, practice learning, prediction, historically supported remediation, a value engine and a grounded assistant - all behind a readiness gate that refuses to compute when the data cannot support a defensible answer.

**The task of this session** was to merge roughly forty design drafts, four implementation handovers and a 23 MB repository dump into **one Master Design Document**, chapter by chapter, at professional specification depth.

**Karim's own framing:** the existing drafts are his *first* draft; the Master Design is meant to be the *last* version. His stated reason for the merge: *"the only way to ensure we do not get lost between multiple files again."*

**Status at handover:** six chapters written, iterated through multiple review cycles, at ~9,900 lines. Chapters 3, 4 and 5 are freeze candidates. Chapter 6 is v4.6 and deliberately **not** frozen pending physical benchmark execution.

---

# 2. WHAT WAS DELIVERED - THE SIX CHAPTERS

**Location:** the outputs folder. **Exactly six files. Anything else found there is obsolete.**

| File | Karim's chapter | PPIQ.txt item | Sections | Lines | Version |
|---|---|---|---|---|---|
| `PPIQ_Chapter1_Marketing_and_Sales.md` | Chapter 1 | item 1 | 1.0-1.11 | 438 | 4.6-aligned |
| `PPIQ_Chapter2_Technical_Overview.md` | Chapter 2 | item 3 | 3.1-3.19 | 811 | 4.3 |
| `PPIQ_Chapter3_General_Technical_Function_Description.md` | Chapter 3 | item 4 | 4.0-4.8 | 3,162 | **4.5 FREEZE CANDIDATE** |
| `PPIQ_Chapter4_Specific_Technical_Function_Description.md` | Chapter 4 | item 5 | 5.1-5.8 | 2,392 | **4.5 FREEZE CANDIDATE** |
| `PPIQ_Chapter5_Tutorial_User_Journey.md` | Chapter 5 | item 6 | 6.0-6.12 | 978 | 4.5 |
| `PPIQ_Chapter6_Infrastructure_Website_Administration.md` | Chapter 6 | items 7, 8, 9 | 6.1-6.5 | 2,134 | **4.6 NOT FROZEN** |

**Total: 9,915 lines, ~126,000 words. All pure ASCII, verified.**

## 2.1 What is inside each chapter

**Chapter 1 - Marketing and Sales.** The problem (fragmentation, the expert who leaves, the unexploited asset); the positioning statement *"the in-house expert that learns your plant's own fingerprint and stays"*; the technical bounding of that phrase into eight governed customer-owned assets; the puzzle framing; five capability layers; three value stories (1.3.a quality, 1.3.b practice learning, 1.3.c predict-then-remediate); the buyer table; the language contract; the value case with the two-downtime-quantity rule; competitive position; the objection playbook; **1.9 selling before the value engine exists** (the CEO-gap script); the demonstration narrative; and **1.11 commercial promise traceability**, 28 promises each mapped to journey, page, API, persistence and acceptance.

**Chapter 2 - Technical Overview.** Declared the **naming, structure and positioning authority** for the whole document. Defines: the canonical user journey **J1-J15**; the technical data-flow codes **DF1-DF15**; the inventory of **40 route pages + 6 global shell components**; the product glossary (3.9); requirement classification Core/Advanced/Future/Excluded (3.10); the surface responsibility matrix (3.11); the 20-element UX contract (3.12); the five-layer completeness rule (3.13); **the plant model entity catalogue (3.14)**; **the permanent relationship model and its sixteen consumers (3.15)**; authoring freedom (3.16); dynamic filtering (3.17); **intelligence as a first-class analytical object (3.18)**; and the positioning rule with its 8+9 question review checklist (3.19).

**Chapter 3 - General Technical Function Description.** The largest chapter. All fifteen DF steps at endpoint level with an **eleven-field contract** each; the 40 pages and 6 shell components with a **ten-field contract** each; the complete database design including the relationship model (4.5.10), the definition store (4.5.11), the intelligence tables (4.5.12), `can_accept` as the complete acceptance authority (4.5.12a), escalations (4.5.12b), bindable intelligence (4.5.13), projection quarantine with fifteen `PV` codes (4.5.14), logging and retention (4.5.15), the six join paths JP1-JP6 (4.5.16), RLS (4.5.17), index rationale (4.5.18), **ten Mermaid ER diagrams (4.5.19)**, the data dictionary (4.5.20) and **the 24-domain error catalogue (4.5.21)**; then credentials as classes, topology, API governance, backup, upgrade and security posture.

**Chapter 4 - Specific Technical Function Description.** The analysis page against the professional benchmark (5.1); the one-shell five-purpose authoring surface with the enumerated illegal-wiring set and the expression editor and fourteen per-block inspectors (5.2); **concurrency, load balancing and 5.3.9 the delta propagation law with execution mechanics** (5.3); the gate and engine including execution placement and Supervisor shadow isolation (5.4); 38+ statistical blocks each with inputs, outputs, validation and best chart (5.5); AI and ML including the practice-learning engine, the fifteen-stage predict-then-remediate pipeline, the nine-check remediation safety gate and the model fallback policy (5.6); the assistant as a persistent dock (5.7); and eight additional designed capabilities (5.8).

**Chapter 5 - Tutorial.** Eight tutorials T1-T8 covering J4-J15, **178 numbered steps**, at button level, written for a user with no engineering background, with a preface explaining the screen once, the three kinds of message, and why the product refuses.

**Chapter 6 - Infrastructure, Website, Administration and Sales.** Four topologies with network zones and the structural read-only proof; 16 containers; the 22-stage pipeline; deployment and upgrade with fourteen post-deployment acceptance checks; seven test layers plus **C1-C4 capacity certification profiles**; quality gates; the backlog standard; **the driver-based sizing model with three verified worked examples and hardware specification per tier**; capacity protection in four bands; backup with a **tested restore acceptance procedure**; monitoring including **Scan Amplification**; the website audit and five-product architecture; the licence and role matrices; the six-dimension price function; the sales-to-engineering handover; **the 37-promise traceability matrix**; and the 26-condition acceptance table.

---

# 3. THE SOURCE MATERIAL - WHAT WAS READ

**The next session does not need to re-read these unless it is looking for something specific.** Everything usable has been absorbed into the six chapters. This list exists so the next session knows what the corpus contains and what has already been mined.

## 3.1 Founding and doctrine

| Document | Lines | What it contributed |
|---|---|---|
| **`4_Track_Vision.txt`** | 285 | **THE COMPASS - the origin document.** Founder intent, four tracks (Workflow, Hardening, Demo, Website), the brand identity in full, the never-say list, the downtime distinction, the demo doctrine, the emulated-plant blueprint. Doctrine v8.1 cites it constantly but never reproduces it |
| `rules.txt` | 1,579 | The founding rules, raised to specification grade |
| `PlantProcessIQ_Doctrine_v8_1.md` | 1,719 | **The richest single draft.** Honesty mechanisms, readiness numbers, the value formula, the data boundary, settled trade-offs, the forbidden/approved language table |
| `concept.md` + Amendment Sheet v1.1 | - | Three sharpened rules, the fifteen-step journey, emulation doctrine, definitions of done |
| `PPIQ_Constitution_v2.md` / `v3.md` | 1,551 / 1,666 | v2 adds nothing v3 does not; v3 absorbed |
| `PPIQ_Constitution_Amendment_A1_LowCode_Shell.md` | - | Absorbed into v3 already |
| `concept_Amendment_6_Schema_Topology_DRAFT.md` | - | Ratified into the schema chapter |
| `PPIQ_Schema_Topology_and_DataFlow_Contract_v2.md` | 342 | **Authoritative for M2 DB work.** Explains the `dump_store` transitional state |
| `PPIQ_Authoring_Layer_Specification.md` | 764 | The genericity mechanism, promoted into Rule 1 |
| `Interactive_Workspace_Doctrine_v1.md` | - | Seven standards; *"anything not written here does not exist"* |
| `Founding_Docs_Review_rules_Doctrine.md` | - | Derivation rules; recommended the four-layer logging model |
| `Aspects_of_Review.txt` + `_Personas_A11-A13.md` | 379 | Origin of the persona instrument, three audiences grown to thirteen |
| `PPIQ_Identity_and_Topology_v4.md` | 687 | Environment reference |
| `PPIQ_Product_Roadmap_v9.md` | - | Standing rules incl. cleanliness by construction |

## 3.2 Implementation handovers - Karim's own end-of-day records

| Document | Lines | Key contribution |
|---|---|---|
| `PPIQ_HANDOVER_15Jul2026.md` | - | **The severity doctrine**, no false dichotomies, justify every click, cross-source ruling, taxonomy route, source-system vs connection-profile |
| `PPIQ_State_Review_14Jul2026.md`, `PPIQ_State_Assessment_and_4Day_Roadmap_16Jul2026.md`, `PPIQ_Session_Handover_17Jul2026.md` | - | Early state, concept material only per his instruction |
| `M1_Final_Validation_21Jul.md` | - | Validation shape |
| `PPIQ_Handover_22Jul2026.md` | 191 | Design-system discovery |
| `PPIQ_Session_Handover_25Jul2026.md` | 678 | **The three-lens audit**, hostile hands, no commit without manual test |
| `PPIQ_Session_Handover_27Jul2026.md` / `_1.md` | 893 / 356 | **The learning-curve rule**, the reproducibility law, the CEO gap, the six forgotten strengths, the CRLF ruling change |
| `PPIQ_Implementation_Review_27Jul2026.md` | - | Implementation review |
| **`PPIQ_Journey_Walk_M1-11_v2.md`** | 500 | **THE MOST IMPORTANT ONE I INITIALLY MISSED.** ~235 numbered testing steps, evidence-tagged, with real routes, button labels, placeholders, toast strings, validation messages, payload shapes, table names and a 16-row Gap Register |
| `PPIQ_Presentation_Scoreboard.md` | 264 | Scoreboard shape |
| `PPIQ_Consolidated_Test_Pass.md`, `Rules662_Validation_Matrix.md` | - | Quality instruments |

## 3.3 The repository dump (29 July, 23 MB, 12 files)

`00_Master_Index`, `01_Backend_Core`, `02_Backend_Database`, `03_Backend_Tests`, `04_Frontend_App`, `05_Frontend_Misc`, `06_Infrastructure`, `07_Tools_Validation_Misc`, `08_Website`, `10_Audit_Signals`, plus `manifest.csv` and `manifest.json`.

## 3.4 Later uploads

`who_am_I_.txt` (the manager's product definition - **changed the positioning materially**), `my_comments.txt` (1,070-line review), `Chapter_6_comments.txt` (twice - the second being the 11-point numerical pass), `PPIQ.txt` (the outline, re-supplied several times as it grew), `commands.txt` (credentials - **left context and was never re-supplied**).

---

# 4. THE REPOSITORY - MEASURED FACTS

**These numbers are measured, not estimated. The next session should not re-derive them.**

**Method that worked:** the dump is indexed by `[METADATA: Path='...']` block markers. Split on that regex in Python to build a path-to-content dictionary. **Do not grep the dump raw** - it pulls minified vendor bundles into context and wastes a great deal of budget. This mistake was made once and cost significant context.

## 4.1 Scale

**2,051 files / 343,024 lines.** Backend core 628 files / 85k lines; backend database 120 / 35k; backend tests 167 / 14k; frontend app 509 / 74k; frontend misc 102; tools and validation 426 / 67k; website 77 / 5k; **infrastructure only 8 files / 856 lines**; demo SQL seed 14 files / 56k lines.

Backend projects: Application 290, Api 184, Infrastructure 80, Domain 41, Analytics.Core 16, Workers 9, Analytics.Engine 7.

## 4.2 API surface

**83 endpoint groups** registered in `Program.cs`, **92 MapGroup prefixes**, **544 verb-level routes** - 279 GET, 226 POST, 21 PATCH, 10 PUT, 8 DELETE.

**18 prefixes under `/api/v5`, 6 under `/api/p15`**, plus `/phase2`, `/phase4`, `/phase5`, `/api/phase8`, `/api/p09`, `/admin/p03p04`.

## 4.3 Domain model - 37 entities

MaterialUnit, MaterialAlias, MaterialUnitTypeDefinition, GenealogyEdge, ProcessStepExecution, ProcessEvent, ParameterDefinition, ParameterObservation, QualityEvent, DefectCatalog, DowntimeEvent, DataQualityIssue, Site, Area, Equipment, Route, RouteStep, OperationDefinition, IndustryTemplate, ConnectionProfile, SourceSystemDefinition, SourceDatasetDefinition, SourceFieldDefinition, ImportBatch, StagingRecord, MappingDefinition, SchemaViewDefinition, JobDefinition, JobRunHistory, DashboardDefinition, DashboardWidgetDefinition, WidgetExpressionStatus, KpiDefinition, CorrelationResult, RiskScore, ModelRegistry, AuditLogEntry.

## 4.4 Frontend

108 page files in 42 groups; 48 lazy-loaded route components; 31 routes in `App.tsx` of which ~20 are legacy `Navigate` redirects. `src/components` 132, `src/pages` 104, `src/api` 56, `src/styles` 29, `src/state` 13.

**19 real custom hooks:** `useApiResource`, `useAssociative`, `useAuth`, `useCustomerSafeAction`, `useDashboardFilters`, `useDashboardGridLayout`, `useDashboardLayoutPersistence`, `useDashboardSelection(s)`, `useDataIntegration`, `useDebugLog`, `useEntitlements`, `useInlineFormValidation`, `useLatestOnlyPolling`, `useLicense`, `useOptimisticSave`, `usePlantProcessTheme`, `useStandardToast`, `useV5I18n`.

**10 providers**, **22 `Standard*` primitives**, `productCoreApiClient.runtime.ts` with **72 methods**, ~177 API client methods across 13 modules.

## 4.5 Database - the big finding

**193 distinct tables, 162 in `public`.** The ruled schemas `ppiq_plant` and `ppiq_meta` are CREATEd but **carry zero tables**. What exists instead: `canon` (16 tables), `acquisition` (5), `dump_store`, five `src_*_shape` emulator schemas, and a schema literally named `is`. **108 of the 162 public tables carry a `ppiq_` prefix** - the intended namespace became a name prefix.

Also: 38 views, 97 functions, 4 triggers, 211 indexes, 67 REFERENCES clauses, and **only ONE RLS policy**.

> **Karim's explanation, and it changes the interpretation:** this is the **documented transitional state** of Schema Topology v2, not chaos. `dump_store` is staging behind the config key `Prep:StagingSchema`, and the rename to `ppiq_staging` is planned for M2 as one configuration change. **Treat it as an anticipated migration, not a defect discovery.**

## 4.6 Real code worth knowing about - the strong parts

**Karim's own correction to me:** *"we don't believe that 23MB, 2,051 files, 343,024 lines are only trash."* He was right; I had reported gaps and missed the good material. These are genuinely strong:

| Artifact | What it actually is |
|---|---|
| **`ReadinessGate.cs`** | Complete and correct. `ReadinessState {Ready,Partial,Blocked}`; `ReadinessThresholds` record with defaults **Heats 60/30, Events 40/15, Minority 0.10/0.03, Freshness 1.0/2.0, Completeness 0.95/0.85**; five dimensions; **overall = worst via `Math.Max` over the enum**; each dimension returns a `Reason` string built from measured value and threshold; `CanRun => Overall != Blocked` |
| **`StatisticalDiscipline.cs`** | Records `Finding`, `FdrItem`, `StratumEffect`, `StratificationVerdict`, `BootstrapResult`. **`EffectRanking.RankByEffect` orders by `Math.Abs(EffectSize)` with p-value only as tie-breaker.** `BenjaminiHochberg.Adjust(pValues, q=0.05)` |
| `CorrelationEngineRegistry.Resolve` | Chooses between `managed` (`ManagedStatisticalComputeEngine`, `DotNetAdvancedCorrelationEngine`) and `postgres` (`PostgresCorrelationComputeEngine`) behind `ICorrelationComputeEngine` |
| **`SafeSqlValidator`** | 295 lines. SELECT/WITH only. **Token-boundary validation so `created_at` is legal.** Forbidden tokens cover DDL, DML, COPY, large-object functions, `dblink*`, `pg_sleep*`, `pg_catalog`, `information_schema`, `xp_*`. Plus `SqlAllowlistProvider` and `SafeSqlCommentStripper` |
| **`DashboardWidgetQuerySafetyRegistry`** | Limits: DefaultMaxRows 100 / Absolute 500; DefaultRawRowLimit 50k / Absolute 250k; DefaultLookbackDays 90 / Absolute 730. Widget types Kpi/Chart/Table. Chart types Kpi, Bar, Line, Area, Pie, Donut, Scatter, Heatmap, Pareto, Table. 14 dimensions, 11 measures |
| **`ThrottlingDataSourceReader`** | Wraps `IDataSourceReader`, evaluates every one-shot read against `ISourceLoadBudgetProvider` + `ISourceQueryRateLimiter` **before it reaches the source** |
| **Assistant layer** | Real tool-calling architecture: `AssistantService`, `AssistantTools` with `FetchFindingTool`/`RunKpiTool`/`OpenSuggestionTool`, role scopes viewer/operator, structured refusals `bad_args`/`not_found`, `IRetrievalIndex` (`NpgsqlRetrievalIndex`), `CanonicalChunkProducer`, `IEmbeddingProvider`, `GroundingService`, **`AssistantEgressGuard.Plan` -> `AssistantEgressPlan`**, `IAssistantModel` |
| **Schema details** | `material_units` unique `(site_id, material_code)` **plus a FILTERED unique on `(source_system, source_record_id)`**; `genealogy_edges.contribution_weight numeric(9,6)` with covering index `(child_material_unit_id, is_transition, contribution_weight)`; `parameter_observations` stores **both** `observed_at_utc` and `observed_at_local` plus `plant_time_zone_id`; `staging_records` FK to `import_batches` **ON DELETE RESTRICT** with `raw_json jsonb` |

## 4.7 The near-misses - strong code with a small defect

| Item | Issue | Fix size |
|---|---|---|
| `DashboardWidgetQuerySafetyRegistry` dimension set | It is a **compiled `HashSet` containing plant vocabulary** - `ShiftCode`, `DefectType`, `RiskClass`, `GradeOrRecipe`. This is a Rule 1 violation even though every value is dynamic | **Small.** Dimensions and measures become registry rows. **Chart types and numeric limits stay closed** - they are product grammar, not customer knowledge |
| `plant_data_log` denormalisation | Already correct and worth naming as design: it stores comparator and limit alongside the rule reference, so **editing a rule later does not rewrite history** | None - document it |
| The filtered unique index | Already correct: it makes projection idempotent **without forbidding rows that have no source identity** | None - document it |

## 4.8 Infrastructure - the thinnest area

**Only 8 files / 856 lines.** One `Jenkinsfile` of 183 lines **plus three backup copies under `deploy/.ppiq-backups`**, one Caddyfile, an edge-agent Dockerfile, a website Dockerfile, an 18-line GitHub workflow.

**Tests: 167 files** - Application unit 66, Api integration 48, Analytics.Core 17, Architecture 13, Infrastructure integration 13, Analytics.Engine 4, Domain 3, one performance test.

## 4.9 Audit signals (29 July, from the dump - NOT run by me)

60 total: 12 CRIT / 41 WARN / 7 INFO. **Most CRIT hits are the audit tool matching its own regex definitions.** The real ones:

1. **`phase9:matrix` runs Playwright with `--list`** - frontend tests are enumerated, not executed. **No visual or E2E gate actually runs today.**
2. 15 hardcoded server-IP references
3. 21 dev-seed endpoint references
4. 2 bootstrap-admin-enabled-in-config

## 4.10 Website implementation (77 files)

20 components, 5 content modules, 5 stylesheets, 3 brand assets, 4 Playwright suites, 7 validation scripts.

**Current routes:** `/`, `/product`, `/proof`, `/security`, `/pricing`, `/about`, `/contact`, `/packs/:code`, `/solutions/:code`, **`/products/:code` -> `LegacyProductRoute`**.

**Strong and preserved:** `HeroTopology`, `GoldenThread(Scroll)`, `TrustEngine`, `SignalVsNoise`, `ArchitectureFlowScroll`, `useScrollDraw` (73 lines, the signature motion), `RequestDemoForm` (339 lines), `RoiCalculator`, `PricingLicenseMatrix`, `ProductScreenshotShowcase`, `IntegrationEcosystem`, `ProofOfValueJourney`, `RolePaths`, `FounderAuthority`, **`ConnectorHonestyBlock`**, **`PositioningTruthBlock`**, `SOUBrand` + 171 lines of brand CSS.

**Content:** `model.ts` (the `ProductPageModel`), `mes.ts` (88 lines), `yardWarehouse.ts` (87), `index.generated.ts` (registry of **only two products**), `phase1WebsiteProof.ts` (179).

**The one structural error:** `/products/:code` redirects into PPIQ pack pages, encoding that the other four products are PPIQ capability packs. **They are not.**

---

# 5. KARIM'S RULES AND DOCTRINE - PASS THESE FORWARD INTACT

**These are his words and his rulings. They govern how the next session must work. Violating one of these is how the previous sessions lost his confidence.**

## 5.1 How to work with him - process rules

| # | Rule | His words or the operative form |
|---|---|---|
| 1 | **ONE FILE PER CHAPTER** | He rejected Chapter 4 delivered as four part-files. Never split a chapter. Never deliver a "part two" or a "complement" - deliver the whole chapter again |
| 2 | **HIS NUMBERING, NEVER MINE** | File names and section numbers follow PPIQ.txt's own labels. Extra sub-numbers may be appended at the end; **main numbers and chapter names never change** - *"otherwise we will lost and I can't revise"* |
| 3 | **THE LEARNING-CURVE RULE** | *"Documents are a learning curve, not a contradiction. A 21-Jul document describing a design and a 25-Jul document describing a different one are not in conflict - the first produced a bug, the second records the fix. Read by date. The later date wins. Never quote an earlier document against a later one"* |
| 4 | **Depth means artifact-level, not word count** | He rejected two chapters as *"not in the deep detailed advanced professional high tech way as I expect"*. Depth = real routes, real handler names, real column types, real button labels, real refusal sentences |
| 5 | **Do not invent, do not skip** | *"you can add to the guideline some part which I overlooked but don't skip some part"* |
| 6 | **Provenance tagging** | `[C]` carried / `[E]` enhanced / `[N]` new, with a per-chapter summary. His 45/25/25 expectation is **guidance, not a quota** - *"The priority is a professional, advanced design, regardless of the exact percentages"* |
| 7 | **Preserve his work** | *"Before deleting or replacing any substantial component, ask: does it conflict, or does it simply need to be reused in the correct place?"* |
| 8 | **Say what is missing** | He values an honest gap list far more than a smooth answer. Every review he sent found something; every time I flagged my own error first, he accepted it |

## 5.2 Product doctrine - the binding laws

| Law | Statement |
|---|---|
| **Rule 1 - Generic only** | No line, word, page, component, schema object, list or code branch prepared for any specific dataset, industry, plant or customer. **Three doors only: import, registry, authoring.** Test: *could a different plant reasonably need a different value here?* |
| **Rule 2 - Plant schema starts empty** | Provable in one query. The link is the only door. Metadata schema ships content under a declared per-table prefill contract, every prefilled row past the genericity lint |
| **Rule 3 - The journey is the product** | Exactly one journey. A second journey written anywhere is deleted, not reconciled |
| **Rule 4 - The latest concept is the only concept** | Dirty or superseded code is deleted, cleaned or corrected - never built upon. A replacement lands **with** the deletion, in the same change. **The reproducibility law: a fix that exists only as data does not exist** |
| **The severity doctrine** | *"Anything that causes a customer to lose trust and kills the deal IS a bug. If a CEO sees the word Demo, the deal is dead... Treat UI clutter and naming violations with the exact same severity as a Server 500 error"* |
| **No false dichotomies** | *"In enterprise B2B at this price point, customers do not choose between a working backend and a professional frontend. They demand both"* |
| **Justify every click** | *"There is no such thing as just keep moving or accepting placeholders. Every click, every label, every step must be 100% justified. I have to defend it live to a customer"* |
| **The verification law** | **Compiling is not done. Gates passing is not done. A browser walk is done.** *"built is not working - his screen is the only proof"* |
| **Cross-source is the value proposition** | Categorical ruling, never to be re-proposed against: *"The entire value proposition and the climax rest on cross-source correlation. Correlating Meltshop with itself defeats the purpose"* |
| **Honesty contract** | Abstention is a first-class result; no gate is ever weakened to produce a result; no number without resolvable evidence; deterministic engines compute and the model only explains; every refusal carries a sentence; **a red outline with no sentence beside it is a failure of the specification** |
| **The three-lens audit** | Lens 1 surrounding, lens 2 presentation, lens 3 deep wiring. *"A pass that only asks did the function run is a smoke test, not an audit"* |
| **Hostile hands** | The demo path is tested by walking it; the product is tested by handing the mouse to someone trying to break it |
| **A guard satisfiable by its own prose is worse than no guard** | Real case: a parity panel compared an object with itself and displayed green IDENTICAL |
| **Retitle when you repoint** | A widget whose title says one thing while it plots another is worse than a broken one |
| **Cleanliness by construction** | *"local and server cleanliness is an artifact of manual purges, not of the product"* - a clean install must be clean by construction |
| **The supremacy inversion rule** | A master specification cannot cite a document younger than itself |
| **The demo doctrine** | *"Demo for me it's not a separated app or extra layer... if we hard code some pages we will fake our self"* |
| **The honest money slide** | *"It recovered a planted validation signal and rejected a null control. That validates the METHOD. ROI is what the pilot measures"* |

## 5.3 The six strengths he forgets to sell under pressure

**Recorded in the 27-Jul handover because he defends weaknesses instead of leading with these:**

1. The readiness gate with five named dimensions, reconstructable from the database alone
2. The associative selection model with real possible-versus-excluded state
3. The genealogy layer walking both directions on the customer's own keys, with attribution weights summing to exactly 1.0 enforced by a database trigger
4. The multi-grain canonical model absorbing thirteen native product types
5. The visual join canvas as a genuine typed-port node canvas
6. Honesty carried as **stored data** - every finding persisting its own framing and recording that no language model participated in the compute path

## 5.4 The CEO gap - strategically the most important finding

> Every professional viewpoint rises materially with M1 work **except the economic buyer**, which stops near 60 because the value engine and live tier switching are M2 by his own split rule. **"The CEO gap is narrative work, not build work. It will not improve before the room no matter how many hours go into the product. Prepare that conversation deliberately; improvising it will read as evasion."**

This produced Chapter 1 section 1.9, the three-move script: show the model not the number; give the honest status unprompted; offer the pilot as the thing to sign against.

## 5.5 Backlog laws (his own, v27 onward)

Done tasks **deleted, never archived**. **No PARTIAL status** - a partial task is rewritten as its remainder with a fresh estimate. IDs restart sequential, lower means higher priority. Phases strictly P1..Pn. Phases 40-65h, critical first. Every phase ends pushable. Junior-ready text with paths, commands and exact acceptance.

**Two ID laws coexist and must be scoped so neither is misapplied:** the **frozen-ID law** governs audit persona identifiers (retired IDs never recycled, which is why the last three personas are A11-A13), while **backlog Law 3** restarts IDs sequentially each epoch.

## 5.6 Formatting and code-delivery preferences

Pure ASCII, UTF-8 no BOM. No em-dashes, no curly quotes. PowerShell apply packs with the preflight-backup-anchored-replace-self-check-gate-auto-revert contract. **Never zip files.** No `&&` in PowerShell; cuddled `} else {`; run from repo root. CRLF for `.ps1` and `.cs`; **preserve existing line endings for `.tsx` and `.css`** (changed 25-Jul to 27-Jul - forcing CRLF makes git report the whole file changed and buries the real diff). For a small one-line source edit, tell him exactly which line to change rather than sending a pack.

---

# 6. RULINGS - SETTLED AND OPEN

## 6.1 Settled (11 ruled + later additions)

| # | Question | Ruling |
|---|---|---|
| 1 | SQL authoring tier-gated? | **Yes, from the second tier (Pro) upward.** Role gate additionally: a viewer never authors SQL at any tier |
| 2 | Arithmetic blocks on the board? | **No, on every surface.** Expression blocks live inside the board block they configure. One board grammar, one validator, one error taxonomy |
| 3 | Widget binding door | **Kind picker is a pre-step; the shared shell is where binding happens.** Catalogue binding is a simplified face of the same shell |
| 4 | What starts empty? | **The plant schema, provably, in one query.** Metadata ships content under a declared per-table prefill contract |
| 5 | Typefaces | **Chakra Petch display, system sans body, IBM Plex Mono data** |
| 6 | Colour tokens | The thirteen-token set, Muted Steel `#8EA7C1`, plus four port colours |
| 7 | Currency | **EUR** |
| 8 | Schemas | **Three**: `ppiq_staging`, `ppiq_plant`, `ppiq_meta` |
| 9 | Panel sides | **inline-start / inline-end**, never left/right |
| 10 | Metering | **Capacity metering law** - then **refined 2-Aug**: the licence uses six commercial dimensions *and* a capacity envelope together (see 6.3) |
| 11 | Assistant tier | **Pro Plus**, moved down from Enterprise. Enterprise then sells on air-gap, all connectors, SSO, self-hosted model, HA |

## 6.2 Still open - the next session should get answers

| # | Item | Recommendation given |
|---|---|---|
| **15** | **The wordmark typeface.** The compass specifies "PlantProcess in Inter Bold, IQ in Electric Cyan"; ruling 5 moved the UI to Chakra Petch | **(A) keep the wordmark in Inter as a locked brand asset** (normal practice) or (B) redraw. **He has never answered this. Asked at least three times.** |
| 13 | Role catalogue | Eight roles as the shipped default, three-role minimum as the smallest legal configuration. **Used in Chapter 6 6.3.2 but never formally ruled** |
| 14 | Logging layers | Four: system, job, data, audit - **later extended to six families** in Chapter 3 4.5.15 (adding assistant and plant-data). Not formally ruled |
| 16 | Transit schema name | **Staging** over dump_store |
| 17 | S1 output artifact | **Transformation Definition** |
| 18 | Word for imported standardised data | **Canonical** |
| 19 | Surface numbering | **S1-S5** |

## 6.3 The most important reversal in the project - and how it resolved

**This is worth understanding because the next session may be tempted to "correct" it back.**

1. **Originally** the tier tables counted objects: users, sources, jobs, dashboards.
2. **29 July - Karim rejected count-based limits.** His engineering objection was correct: three DB-links could each import a hundred tables of a hundred million rows running ten jobs a minute. *"the formula and the tier limits MUST restrict the amount of data, the refresh frequency and the compute load, not just the raw count."* This produced the **capacity metering law** in Chapter 1.7.1.
3. **2 August - he then said pricing IS a function of six things** including counts: users, pages, jobs, DB-links, data transferred, and AI/ML/chatbot from tier three. Plus: hardware specs per tier must be documented.
4. **The resolution, and both are true at different layers:**

| Layer | Uses | Purpose |
|---|---|---|
| **Commercial packaging - what the customer buys** | The six counts | Legible, quotable, self-assessable |
| **Technical protection - what the platform enforces** | Retained volume, ingest rate, refresh floor, compute slots, sessions | Protects the machine from what the counts did not predict |

> **The reconciliation rule: a tier bounds all six counts AND its capacity envelope together, and the two are calibrated against each other. A count determines package eligibility. It never determines what the server must be.**

---

# 7. COMPUTATIONS PERFORMED - DO NOT REDO THESE

**These were computed in this session with Python. The scripts and results are reproduced so the next session does not spend budget re-deriving them.**

## 7.1 The scale scenario - the most important calculation in the project

Karim's stated Pro-tier scenario: 3 DB-links x 100 tables x 500 MB, 3 import jobs incremental, 7 other jobs at 3-minute cadence.

```
Source footprint      = 100 x 500 MB x 3        = 146 GB
Non-import runs/day   = 7 x (1440/3)            = 3,360 runs/day

NAIVE FULL SCAN       = 3,360 x 146 GB          = 481 TB/day
                                                = 5.7 GB/s sustained, forever

DELTA-SCOPED @0.5%/d  = 0.73 GB/day delta
                        1.6 MB per run
                        all jobs                = 5.1 GB/day scanned
DELTA-SCOPED @2.0%/d  = 2.9 GB/day delta        = 20.5 GB/day scanned

RATIO naive : delta   = 24,000 : 1  to  96,000 : 1
```

> **This single calculation is why Chapter 4 5.3.9 (the delta propagation law) exists.** It proves that the answer to large data is architecture, not tighter licence limits. Karim's own instinct - *"I should not punish the customer and make him pay the cost of my bad design"* - is vindicated by the arithmetic.

## 7.2 Sizing worked examples - corrected in the final pass

**Karim's review found that my first three examples contradicted the class boundaries they were judged by.** Root cause: I was treating **source footprint** as **retained volume**. Corrected:

| Example | Inputs | Retained | Class limit | Verdict |
|---|---|---|---|---|
| **A Small / Light** | 200k rows/day, 12 months | **51 GiB** | 250 GiB | **Small** OK |
| **B Medium / Pro** | **Karim's scenario**: 146 GiB source, 0.5%/day, 24 months | **929 GiB** | 2 TiB | **Medium** OK |
| **C Large / Enterprise** | 40M rows/day, 24 months | **18.3 TiB** | 20 TiB | **Large** OK |

**Reproducible script (Python):**

```python
GB=1024**3; BYTES_PER_ROW=400; IDX=1.6
def retained(rows_day, days): return rows_day*BYTES_PER_ROW/GB*IDX*days
# A: retained(200_000, 365)   = 44.6 GiB canonical
# B: 146*0.005 GiB/day -> retained = 146*0.005*1.6*730 = 853 GiB canonical
# C: retained(40_000_000, 730) = 17.0 TiB canonical
```

## 7.3 The database RAM model - also corrected

**Karim's review:** *"Example B declares an approximately 2 TB working set and then recommends only 32-64 GB RAM; those two statements cannot both be true."* He was right.

**The old formula was wrong**: `ram_db = max(8, 0.25 x working_set_GB + 2 x cpu_db)` where working_set was the whole 90-day partition set.

**The corrected cache-target model:**

```
hot_window_b   = ingest_bytes_ps * 86400 * index_factor * HOT_WINDOW_DAYS   (90)
hot_index_b    = HOT_INDEX_RATIO * hot_window_b                             (0.15)
recent_delta_b = ingest_bytes_ps * 86400 * index_factor * RECENT_DELTA_DAYS (7)
cache_want_b   = hot_index_b + recent_delta_b
cache_target_b = min( cache_want_b , CACHE_CAP_b(class) )   caps 8/48/96/256 GiB
ram_db_b       = max( 8*GiB, 1.25*cache_target_b + 0.75*GiB*cpu_db + 4*GiB )
```

**Verified to reproduce every tier:** A gives 9.3 -> 16 GiB; B gives 37 -> 64 GiB; C gives 130 -> 128 GiB. **Example C also demonstrates the cap doing its job**: it wants 489 GiB and gets 96, relying on partition pruning and NVMe instead - which is exactly why an unprunable query is refused rather than executed.

## 7.4 Website audit tally

| Verdict | Count |
|---|---|
| Keep as-is | 18 |
| Keep and enhance | 9 |
| Refactor and reuse | 4 |
| **Replace** | **1** (`LegacyProductRoute`) |
| **Remove** | **1** (the sibling-products-must-not-exist assertion) |
| Add | 7 |

---

# 8. THE REVIEW CYCLES - WHAT HE CAUGHT AND WHAT IT TAUGHT

**This is the most useful section for the next session, because it shows the failure modes he detects and how to avoid repeating them.**

| Cycle | What he found | The lesson |
|---|---|---|
| **1** | Chapters 3 and 4 too shallow | **Depth means real artifacts.** Not more words - actual route names, handler names, column types, button labels, refusal sentences |
| **2** | *"we don't believe 23MB, 2,051 files are only trash"* | I had mined structure and reported gaps. **Report the strong parts and the near-misses too** |
| **3** | Chapter 4 delivered as four files | **One file per chapter. Never split.** |
| **4** | 1,070-line review: ~200 requirements across four chapters | I told him honestly it would take multiple turns and did Ch1+Ch2 first. **Honesty about scope was accepted; a thin all-in-one attempt would not have been** |
| **5** | `who_am_I_.txt` - the manager's definition | **Changed the positioning materially.** My Chapter 1 implied "we do less dashboarding, we make up for it elsewhere". Corrected to: *the same class of authoring freedom, over something they do not have.* **The dashboard is the presentation layer; the plant model, genealogy, evidence chain, statistical discipline, practice learning, prediction, remediation and feedback loop are the product** |
| **6** | 14 second-order findings | The biggest: **I had put a per-prediction verdict on a global row.** `eligibility_state` on `remediation_candidates` was wrong - the same template is actionable for one unit and not for another that has passed the stage. Split into template + `prediction_remediation_evaluations` |
| **7** | 14 propagation findings | Contracts declared in one chapter but not carried into payloads and page contracts in another. **A design rule that is not propagated into every artifact that must implement it is not a design rule** |
| **8** | Chapter 5 consistency | Four blockers, including a **safety error**: I wrote that failing remediation candidates are "still shown to you". Corrected - **suppressed means suppressed**, because a reader under time pressure may act on what they see |
| **9** | 11-point numerical pass on Chapter 6 | **Worked examples contradicted the class boundaries they were judged by**; units mixed GB and bytes; the RAM formula contradicted its own examples. **All three were real arithmetic errors of mine** |

## 8.1 Findings I raised that he accepted

| Finding | Outcome |
|---|---|
| The `/products/:code` legacy redirect encodes the wrong product architecture | Confirmed; replacement is the one structural change to the website |
| The `phase9:matrix` Playwright `--list` flag means **frontend tests are enumerated, not executed** | Recorded; **no visual or E2E gate actually runs today** |
| Phase and version tokens are in the live URL space (`/phase8/...`, `/api/v5/...`) - a customer's IT reviewer sees them in the network tab | Chapter 3 specifies a clean 27-domain namespace; the migration is a Status Register item |
| One RLS policy against 193 tables | Chapter 3 4.5.17 specifies the target with an architecture test |
| The Jenkinsfile exists three more times as backups inside `deploy/.ppiq-backups` | Rule 4's superseded-artifact clause in miniature |
| **F4 Jobs Administration had no way to say which definition a job runs** | Real gap **found by writing Chapter 5** - a tutorial step had nothing to click. Fixed in Chapter 3 4.5.5a with `target_definition_id` and the `JB` error domain |
| **C2 Mapping Health had no Reprocess control** | Same origin - the tutorial needed it, the API existed, the control did not. Added |
| **`/materials` had no landing route** | The menu had nothing to open. Added as a two-state page contract |

> **Tip worth carrying: writing the tutorial found three real gaps in the technical chapters that no amount of re-reading the technical chapters would have found.** A user walking a path exposes what a specification omits.

---

# 9. THE REALIZATION SCOREBOARD - HONEST STATUS

## 9.1 Design completeness (what this session controlled)

| Chapter | PPIQ.txt | Design status | Freeze |
|---|---|---|---|
| 1 Marketing and Sales | item 1 | **Complete.** 28 traced promises | Aligned to v4.6 |
| 2 Technical Overview | item 3 | **Complete.** Naming, structure and positioning authority | Stable |
| 3 General Technical | item 4 | **Complete.** 15 DF steps, 46 page/component specs, full schema, 10 diagrams, 24 error domains | **FREEZE CANDIDATE** |
| 4 Specific Technical | item 5 | **Complete.** 5 surfaces, engines, 38+ blocks, delta law | **FREEZE CANDIDATE** |
| 5 Tutorial | item 6 | **Complete.** 8 tutorials, 178 steps | Stable |
| 6 Infra / Website / Admin | items 7, 8, 9 | **Complete but NOT frozen** | **Blocked on C1-C4 benchmark execution** |
| Rules / Constitution | item 2 | **Exists as `PPIQ_Master_Design_Chapter_01_Constitution_v3.md` from an earlier iteration.** It was superseded when numbering switched to Karim's scheme, and its content lives inside Chapters 1-6. **It has no file in the current six-file set** | **OPEN QUESTION - see 12.3** |

## 9.2 Product realization (from his own scoreboards, 27 July - NOT re-measured)

| Scope | Headline | Note |
|---|---|---|
| Delivery scope | **45, Critical** | Infrastructure persona lowest |
| Demo scope | **52**, ceiling **60** after all M1 work | His own bands put demo-ready in the **seventies**, not the nineties |
| Economic buyer | **~60 ceiling** | The CEO gap; narrative work, not build work |

**These are 27-July numbers from his handovers. Nothing in this session re-measured them.** No walk was performed, no gate was run.

## 9.3 The implementation gap - design against reality

| Area | Design says | Repository shows | Gap size |
|---|---|---|---|
| API namespace | 27 clean `/api/{domain}` domains | 92 prefixes with phase and version tokens | **Large - migration** |
| Schemas | 3 (`ppiq_staging`, `ppiq_plant`, `ppiq_meta`) | 12 schemas, 162 tables in `public`, target schemas empty | **Medium - anticipated M2 migration** |
| RLS | Forced on every tenant-owned table + architecture test | **1 policy** | **Large** |
| Relationship model | `plant_relationships` + members + paths, 16 consumers | Does not exist | **Large - new** |
| Definition store | Unified `definition_store` + versions + dependencies | Scattered per-artifact tables | **Large - new** |
| Intelligence tables | ~15 tables incl. predictions, drivers, practices, evaluations | Partial (`correlation_results`, `risk_scores`, `model_registry`) | **Large - new** |
| Quarantine | `projection_quarantine` + 15 `PV` codes | Does not exist | **Medium - new** |
| Delta propagation | Every job class delta-scoped | Import incremental; others unknown | **Unknown - must be measured** |
| Job target definition | `target_definition_id` + version policy | Does not exist | **Small but blocking** |
| Registry-driven dimensions | Registry rows | Compiled `HashSet` with plant vocabulary | **Small, high value** |
| Pages | 40 route + 6 shell | 108 page files, ~20 legacy redirects | **Medium - consolidation** |
| Assistant | Persistent dock, every page | Route page `/assistant` | **Small** |
| E2E gate | Golden journey J1-J15 blocking | **Playwright runs with `--list`** | **Large - the gate does not run** |
| Infrastructure | 22-stage pipeline, 16 containers, 4 topologies | 8 files, 1 Jenkinsfile + 3 backups | **Very large - mostly new** |
| Website | 5 products, `/products` portfolio, mega-menu | 2 products in registry, `/products/:code` redirects to packs | **Medium** |

---

# 10. BACKLOG - DERIVED FROM THE DESIGN

**No formal backlog was written in this session.** What follows is the ordered work the design implies, in Karim's own backlog format (Chapter 6 6.1.8), so the next session can start from it rather than re-deriving it.

**Status legend: all items are `Not Started` unless stated. Nothing in this list has been implemented, tested or verified in this session.**

## P1 - Foundations that everything else needs (est. 45-60 h)

| ID | Title | Class | Design ref | Why first |
|---|---|---|---|---|
| 1 | Relationship model tables + one path resolver + `purpose` gating | Product gap | Ch3 4.5.10 | **16 consumers depend on it.** Nothing cross-source is correct without it |
| 2 | Unified definition store + versions + dependencies + cycle trigger | Product gap | Ch3 4.5.11 | Every authored artifact needs one identity and version model |
| 3 | `job_definitions.target_definition_id` + version policy + `JB` codes | Product gap | Ch3 4.5.5a | **A job currently cannot say what it runs.** Blocks T7 |
| 4 | Registry dimensions and measures as rows, not a compiled HashSet | Technical debt | Ch3 4.5.13 | **Rule 1 violation reachable by a customer** |
| 5 | RLS forced on every tenant-owned table + architecture test | Security issue | Ch3 4.5.17 | **Highest severity class.** One policy against 193 tables |

## P2 - The data path made correct (est. 50-65 h)

| ID | Title | Class | Design ref |
|---|---|---|---|
| 6 | `projection_quarantine` + the 15 `PV` validation classes + reprocess | Product gap | Ch3 4.5.14, DF5 |
| 7 | C2 Mapping Health quarantine UI: grouped by code, examples, reprocess | Product gap | Ch3 4.4 C2 |
| 8 | `stage_watermarks` + delta propagation for projection, feature, analysis | Product gap | **Ch4 5.3.9** |
| 9 | Chunking, chunk receipts, scan budgets, deterministic merge | Product gap | Ch4 5.3.9.6a |
| 10 | Scan Amplification metric + baseline + regression gate | Infrastructure | Ch6 6.1.12.2a |

## P3 - Intelligence completion (est. 55-70 h)

| ID | Title | Class | Design ref |
|---|---|---|---|
| 11 | Intelligence tables: feature store, snapshots, predictions, drivers, comparables | Product gap | Ch3 4.5.12 |
| 12 | `prediction_current` as the complete operational read model | Product gap | Ch3 4.5.12 |
| 13 | Practice learning engine incl. back-off ladder and sensitivity test | Product gap | Ch4 5.6.4a/b |
| 14 | Remediation nine-check gate + `can_accept` + `RM` codes | Product gap | Ch4 5.6.4d, Ch3 4.5.12a |
| 15 | D9 Early Warning page with the full DF14 action lifecycle | Product gap | Ch3 4.4 D9 |
| 16 | Model `serving_role` + the six-condition fallback policy | Product gap | Ch4 5.6.7a |

## P4 - Surfaces and namespace (est. 40-55 h)

| ID | Title | Class | Design ref |
|---|---|---|---|
| 17 | API namespace migration to 27 domains, dual-serve window | Technical debt | Ch3 4.3 |
| 18 | Page consolidation 108 -> 40, legacy deletion under Rule 4 | Technical debt | Ch3 4.3 |
| 19 | Assistant page -> persistent dock G1 | Product gap | Ch3 4.4 G1 |
| 20 | The six shell components G1-G6 | Product gap | Ch3 4.4b |
| 21 | `/materials` landing state | Product gap | Ch3 4.4 C5 |

## P5 - Schema migration and infrastructure (est. 45-60 h)

| ID | Title | Class | Design ref |
|---|---|---|---|
| 22 | Three-schema physical migration (`Prep:StagingSchema` rename) | Technical debt | Ch3 4.5.2 |
| 23 | **Fix `phase9:matrix` to execute rather than `--list`** | **Bug** | Ch6 6.1.3.1 |
| 24 | The 22-stage pipeline | Infrastructure | Ch6 6.1.3 |
| 25 | Container architecture, 16 containers, 4 profiles | Infrastructure | Ch6 6.1.2 |
| 26 | Golden-journey E2E J1-J15 as a blocking gate | Infrastructure | Ch6 6.1.5.5 |
| 27 | Remove the 3 Jenkinsfile backups from `deploy/.ppiq-backups` | Technical debt | Rule 4 |
| 28 | Parameterise the 15 hardcoded server-IP references | Security issue | Ch6 6.1.2.4 |
| 29 | Remove the 21 dev-seed endpoint references from production paths | Security issue | Rule 2 |
| 30 | Disable bootstrap-admin in production config, verified by gate | Security issue | Rule 2 |

## P6 - Certification and commercial (est. 40-50 h)

| ID | Title | Class | Design ref |
|---|---|---|---|
| 31 | **C1-C4 benchmark profiles - the Chapter 6 freeze blocker** | Infrastructure | Ch6 6.1.5.8 |
| 32 | Replace the 10 `REFERENCE_ASSUMPTION` constants with measured values | Infrastructure | Ch6 6.1.9.4a |
| 33 | Capacity calculator / Sales Administration tool | Product gap | Ch6 6.3.8 |
| 34 | Website: five products, `/products` portfolio, mega-menu, tests | Enhancement | Ch6 6.2 |

---

# 11. DEPLOYMENT, SERVER AND PIPELINE - WHAT IS ACTUALLY KNOWN

**Restating the boundary because the handover request asked for this specifically:**

> **No deployment was performed. No server was accessed. No pipeline was run or repaired. No app URL was tested. No credential was ever used.**

## 11.1 What is known, and from where

| Fact | Source | Confidence |
|---|---|---|
| A `Jenkinsfile` exists, 183 lines | Repository dump manifest | Measured |
| **Three backup copies of it exist under `deploy/.ppiq-backups`** | Manifest | Measured |
| A Caddyfile, an edge-agent Dockerfile, a website Dockerfile, an 18-line GitHub workflow exist | Manifest | Measured |
| **Infrastructure is 8 files / 856 lines total** | Manifest | Measured |
| **`phase9:matrix` runs Playwright with `--list`** - tests enumerated, not executed | Audit signals in the dump | Measured, **not verified by running it** |
| 15 hardcoded server-IP references | Audit signals | Measured |
| 21 dev-seed endpoint references | Audit signals | Measured |
| 2 bootstrap-admin-enabled-in-config | Audit signals | Measured |
| `commands.txt` contains root SSH, application database and Jenkins credentials, six emulated source hosts and ports, two launch profiles, migration and E2E commands | Read once, early | **Left context and was never re-supplied.** Values are NOT in my knowledge now |
| **One password is reused between the application database and Jenkins** | Noted when `commands.txt` was read | Recorded as a rotation obligation |
| The first E2E run on a machine writes visual baselines and reports them as failures | `commands.txt` note | Not verified |
| Two launch profiles select the database: `local` (empty-start) and `presentation` (populated) | `commands.txt` | Not verified |

## 11.2 The credentials situation - important

Karim ruled on 29 July that **all credentials stay in the master document**, in one contiguous section, so a customer-safe extract is one deletion. I structured Chapter 3 4.9 / 4.6 with a marked insertion slot for them.

**Then in Chapter 6 the design moved to credential *classes* rather than values** (Chapter 3 4.6.1: eight classes with custody, injection, rotation and audit), on the reasoning that a master design carrying live secrets cannot be shared with a customer's IT department and becomes the largest single security liability in the project.

> **The next session must ask Karim which he now wants**, because the two positions coexist in the corpus. My recommendation, already written into Chapter 3 4.6.1: **classes in the design, values in a protected deployment runbook.** But this is his call and he ruled the other way once.

## 11.3 What the design says the pipeline must become

Chapter 6 6.1.3 specifies 22 stages with mandatory gates, and names two anti-patterns explicitly:

1. **A stage that catches an error and reports success.**
2. **A test command that enumerates rather than executes** - which is exactly the `--list` defect above, and the pipeline truth test asserts against it.

**None of this is implemented.** The design exists; the execution does not.

---

# 12. WHAT THE NEXT SESSION SHOULD DO FIRST

## 12.1 Do not redo

- Do not re-read the ~40 source drafts. Everything usable is in the six chapters and in section 5 of this handover.
- Do not re-mine the repository dump for structure. Section 4 has the measured numbers.
- Do not re-derive the scale, sizing or RAM arithmetic. Section 7 has them with scripts.
- Do not re-audit the website. Section 4.10 and Chapter 6 6.2.0 have the verdicts.
- **Do not claim any test was run or any pipeline made green in the design session.** It was not.

## 12.2 Open questions to put to Karim

1. **The wordmark, ruling 15.** Inter as a locked asset, or redraw in Chakra Petch? **Asked at least three times, never answered.**
2. **Credentials in the document or in a runbook?** See 11.2.
3. **The Rules/Constitution chapter** - PPIQ.txt item 2 has no numbered file in the current six-file set. Does he want it as a seventh file, or is its content adequately distributed?
4. **Formal ruling on roles (13) and logging families (14)**, both used in the chapters but never explicitly ruled.
5. **Does he want the six files assembled into one master document?** His original stated goal was one file. The six-file set exists because of his one-file-per-chapter rule, which is about chapters not about the final artifact.

## 12.3 Known documentation gaps

| Gap | Status |
|---|---|
| **Chapter 5 does not mention the per-tier cadence floor.** A Pro user setting a 1-minute schedule will be refused (`JB`/`QT` path). The tutorial should warn beforehand | **Small edit, not done** |
| The Implementation Status Register referenced by every chapter | **Never written.** It is the designed home for all build-state facts |
| The merge ledger and glossary appendix | Glossary exists as Ch2 3.9; merge ledger never written |

## 12.4 If Karim asks for implementation next

Start at **P1 item 3** (`job_definitions.target_definition_id`). It is the smallest item that unblocks a real user path, it is fully specified in Chapter 3 4.5.5a, and it demonstrates the design-to-code loop on something bounded before committing to the large P1 items.

**And follow his verification law:** compiling is not done, gates passing is not done, **a browser walk is done**.

---

# 13. THE ONE-PARAGRAPH SUMMARY FOR THE NEXT SESSION

Karim has a real, substantial PPIQ implementation - 2,051 files, 343k lines, with genuinely strong pieces including a correct readiness gate, a real statistical discipline module, a 295-line safe-SQL validator, a throttling source reader and a real assistant tool architecture. Over four days this session merged roughly forty design drafts, four implementation handovers and a 23 MB repository dump into a six-chapter Master Design Document of ~9,900 lines, through nine review cycles in which he caught real errors each time - including three arithmetic errors of mine in the sizing model, a per-prediction verdict wrongly stored on a global row, and a safety error where I said suppressed remediation candidates are still shown. Chapters 3, 4 and 5 are freeze candidates; Chapter 6 is v4.6 and deliberately not frozen because ten performance constants remain unmeasured and the C1-C4 benchmark profiles have not been executed. **Nothing was implemented, tested, deployed or fixed in the running system.** The next session inherits a complete design, a precise implementation gap map, an ordered backlog, and a short list of open questions - the most persistent being the wordmark typeface, which he has been asked three times and never answered.

---

*End of handover. Written 2 August 2026.*
