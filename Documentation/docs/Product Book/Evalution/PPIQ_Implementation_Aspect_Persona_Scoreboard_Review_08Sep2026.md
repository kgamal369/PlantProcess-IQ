# PlantProcess IQ — Implementation Aspect & Persona Scoreboard, Design Validation and Review

**Review date:** 2026-09-08  
**Implementation snapshot:** UltimateAudit generated 2026-09-08 13:53 (`2,698 files / 498,737 lines`)  
**Review basis:** latest implementation audit package + current Master Design chapters + Layer-B Architecture Design Pack + Backlog v2.19 execution authority + verified task/session state available to the review.  
**Review type:** source-grounded architecture/product/quality review. This is **not** a substitute for a fresh current-HEAD runtime certification.

---

## 0. Executive verdict

PlantProcess IQ has crossed an important threshold: it is no longer best described as a large prototype with many individually working features. The latest implementation shows a credible **governed product architecture** in several core areas: canonical database topology, immutable/versioned definitions, generic semantic authority, relationship resolution, temporal/aggregation governance, Page/Widget persistence, a unified Canvas authoring shell, SQL safety/versioning, and evidence-first analytics/Assistant contracts.

The strongest pattern in the repository is that **validation maturity is ahead of production-operational maturity**. The weighted review scores the implementation at approximately **67/100 against the complete final design target**, with **validation evidence ~76/100** but **production readiness ~53/100**. This is not inconsistent with the M2 backlog being ~51.7% complete by nominal hours: backlog completion and architecture maturity measure different things.

The product is therefore in a strong engineering position, but it is **not yet an enterprise-final Qlik/Power BI/Tableau-class BI platform, not yet a production-complete distributed job engine, not yet a complete MF-01..MF-07 intelligence plane, and not yet a customer-ready OPC/connector estate**. Those are the main boost areas.

The highest-priority correctness issue found in this review is a **canonical CI contradiction**: the Jenkins E2E stage is gated off by default while the repository's own CI truth test explicitly forbids a gated E2E stage. Two additional high-value regressions are the hardcoded `canonical_material_units` target in the shared SQL authoring shell and connector/marketing copy that can overstate current backend certification.

### Executive numbers

| Measure | Score / State | Interpretation |
|---|---:|---|
| Full-design implementation maturity | **67 / 100** | Strong foundation; substantial final-design work remains |
| Functional coverage | **69 / 100** | Many core user journeys exist; advanced execution/connectivity still incomplete |
| Design alignment | **66 / 100** | Generally strong, with several important authority contradictions/drifts |
| Validation evidence | **76 / 100** | A major strength; many known-answer/architecture/runtime certification assets |
| Production readiness | **53 / 100** | Held down by jobs, connector certification, security hardening, operations/CI |
| M2 nominal-hour completion (separate metric) | **513 / 993 h = 51.7%** | Project execution metric, not design-maturity score |
| M2-P1 strict completion | **205 / 217 h = 94.5%** | T-251 remains the final 12 h |
| M2-P2 strict completion | **80 / 320 h = 25.0%** | Worker-2 critical Canvas journey is further ahead than the whole phase |

---

## 1. Methodology

### 1.1 Scoring formula

Each Aspect score uses four dimensions:

- **Functional Coverage — 35%:** how much of the intended behavior is implemented in source and reachable through a coherent product path.
- **Design Alignment — 25%:** how closely the implementation follows the current architecture/product design rather than creating an alternate authority.
- **Validation Evidence — 20%:** strength of deterministic tests, runtime certification, architecture guards, negative controls and falsification evidence.
- **Production Readiness — 20%:** operational completeness, security, observability, scalability, deployability, customer configuration and release truth.

The score is an engineering judgment calibrated against the **complete design target**, not against only the tasks scheduled so far. A high score does not mean 'no work remains'; a low score does not mean 'bad code' when the capability is intentionally scheduled later.

### 1.2 Score bands

| Score | Meaning |
|---:|---|
| 90–100 | Design-close / celebration zone; only bounded residual work |
| 75–89 | Strong implementation; material but controlled remaining work |
| 60–74 | Substantial foundation; important enterprise gaps remain |
| 45–59 | Partial implementation; usable pieces but not design-complete |
| <45 | Major gap / boost zone |

### 1.3 Evidence confidence

- **Runtime-proven:** fresh execution/acceptance evidence exists for the specific claim.
- **Test-proven:** deterministic or integration/architecture tests directly hold the contract.
- **Source-proven:** implementation is visibly present, but this review did not execute it.
- **Planned:** design/backlog target exists but current implementation evidence is absent/incomplete.

The report deliberately avoids converting source presence into runtime-green status.

---

## 2. Aspect scoreboard

| # | Aspect | Functional | Alignment | Validation | Prod. readiness | Overall | Visual | Verdict |
|---|---|---:|---:|---:|---:|---:|---|---|
| 1.1 | **Front End HMI / BI UI/UX** | 75 | 72 | 80 | 60 | **72** | `███████░░░` | Strong functional BI foundation; not yet Qlik/Power BI/Tableau parity. Visual/authoring parity sub-score: ~58/100. |
| 1.2 | **Canvas / Wiring / SQL Editor** | 68 | 67 | 82 | 48 | **67** | `███████░░░` | Excellent shell, validation and SQL-mode foundation; T-243/T-245/T-246 keep end-to-end production journey incomplete. |
| 1.3 | **Jobs / Run / Monitor / Logging / Parallelism** | 50 | 42 | 58 | 38 | **47** | `█████░░░░░` | Basic execution, scheduling, history and logging exist; advanced DAG/admission/weighted pools/parallel load balancing remain major gaps. |
| 1.4 | **ML / Engine / Model Orchestration** | 66 | 60 | 78 | 45 | **63** | `██████░░░░` | Strong MF-01..MF-04/runtime/kernel foundation; not yet the complete MF-01..MF-07 production orchestration and serving model. |
| 1.5 | **Backend / API** | 86 | 84 | 88 | 74 | **84** | `████████░░` | One of the strongest areas: versioned definitions, relationship authority, generic contracts and broad test coverage. |
| 1.6 | **User / Role** | 61 | 56 | 70 | 48 | **59** | `██████░░░░` | Legacy/current identity is substantial, but final eight-role canonical contract and admin surface are not complete. |
| 1.7 | **DB Link / Schema Design / OPC / Interfaces** | 57 | 55 | 66 | 40 | **55** | `██████░░░░` | CSV/Excel and connector framework are real; certification breadth and OPC production runtime remain far from final target. |
| 1.8 | **DB / Schemas / Canonical Persistence** | 92 | 90 | 90 | 80 | **89** | `█████████░` | Very strong: governed three-schema topology, canonical migration authority, definition-store convergence. T-251 prevents 100%. |
| 1.9 | **License Control** | 70 | 65 | 70 | 55 | **66** | `███████░░░` | Tier/feature entitlement mechanisms are substantial; final canonical Release-1 enforcement, meters and admin completeness remain open. |
| 1.10 | **Security** | 72 | 68 | 78 | 52 | **68** | `███████░░░` | Good primitives and guards; production hardening is incomplete and CI/test-proof issues reduce confidence. |
| 1.11 | **ChatBot / LLM / Assistant** | 75 | 72 | 80 | 55 | **71** | `███████░░░` | Planner/retrieval/verifier/context/evidence foundation is strong; generic tool convergence and final governed intelligence integration are incomplete. |

### 2.1 Front End HMI / BI Layer UI/UX — **72/100**

**Design target.** The design explicitly aims for a Qlik-class interactive workspace rather than a generic dashboard shell: customer-created pages, widgets, filters, KPI/chart/table types, associative selection, persisted layout, drill-down/drill-through, chart compatibility, professional styling, property inspection, previews and self-service authoring without a developer.

**What is strong now.** The implementation has real Page Builder behavior, persisted page/dashboard relationships, widget editing, dynamic filters/selections, Recharts-based line/bar/area/pie/scatter surfaces, responsive containers, chart compatibility grammar, chart interaction tests, layout persistence, widget isolation and a substantial E2E corpus. Prior measured chart defects such as first-paint sizing, fabricated selection fields, KPI fallback and resize no-ops were actively repaired rather than hidden.

**Why this is not yet Qlik/Power BI/Tableau parity.** Mature BI products have years of refinement in property inspectors, cross-highlight/selection management, bookmarks, pivot/crosstab behavior, conditional formatting, rich theme/style governance, authoring ergonomics, accessibility, export/print fidelity, mobile composition, calculated fields/measures, recommendation UX and visual consistency. PlantProcess IQ has many of the correct foundations, but not yet the same breadth and polish. I therefore separate **HMI functional maturity (~72)** from **direct enterprise-BI visual/authoring parity (~58)**.

**Validation caution.** The audit package contains source and many E2E assets, but the canonical release pipeline currently has the E2E gating contradiction described later; therefore visual/runtime certification cannot receive a production-green score from this snapshot alone.

### 2.2 Canvas / Wiring / SQL Editor — **67/100**

The Canvas is one of the most impressive architectural areas. `SharedAuthoringShell` follows the design's single-shell model: mode bar, schema tree, board/SQL center, purpose-aware toolbox and persistent debug log. SQL mode hides the toolbox entirely, preserves schema discovery, runs bounded server validation/preview, shows result schema/rows and supports immutable SQL-version save. Typed ports and graph validation refuse incompatible connections. T-242 added deterministic arithmetic/comparison/Boolean evaluation and governed aggregate/window binding.

However the **whole requested Canvas product** is not finished. T-243 still owns canonical save/reopen/edit round-trip; T-245 owns compile-to-job, run monitor and per-block evidence; T-246 owns governed model/intelligence blocks; T-247 owns the first-customer Day-1 journey. That is why the core looks advanced while production readiness remains only ~48.

There is also a central authority conflict: Backlog T-242 asked for ForEach/RepeatN/WhileBounded board blocks and Worker 2 implemented them correctly, while the Layer-B design says FOR/WHILE are orchestration and do not belong on boards. The same Layer-B section says arithmetic/comparison/logic are expression blocks inside configuring blocks, whereas T-242 describes executable board block kinds. This is **not a Worker-2 quality failure**; it is a backlog/design governance contradiction that must be ruled centrally before S3/S4 expands.

### 2.3 Job execution / monitor / logging / multithreading / load balancing — **47/100**

Current product code has real job definitions, run-now actions, history, pause/resume/backfill/schedules and a customer-oriented job log stored in `ppiq_meta.job_log` plus Serilog. That is useful.

The advanced design, however, is much larger: dependency DAG, target/version policy, admission control, weighted CPU/GPU/IO pools, bounded parallelism, priorities, retry/reap behavior, resource hints, queue-depth/oldest metrics and deterministic per-run correlation. `JobRunOrchestratorService` still has a central job-type switch and explicitly returns 'no manual executor' / 'not implemented' for unsupported types. `ImportBatchQueueProcessorService` selects candidate batches and processes them in a serial `foreach`. This is safe but not the final distributed execution plane.

This area is therefore the largest systems-engineering gap and should be boosted after the immediate P1/T-243 blockers.

### 2.4 ML / Engine — **63/100**

The ML foundation is materially stronger than a typical prototype: a versioned C#↔Python protocol, artifact hashing/Arrow/Parquet, bounded sequence storage, MF-01 process encoder, MF-02 similarity, MF-03 novelty, MF-04 supervised runtime, holdout/leakage checks, TreeSHAP provider, promotion/governance and remediation contracts, plus extensive Python tests.

The final Layer-B target is larger: MF-01..MF-07, persistent data products, active model identity, promotion/rollback, statistical MF-06, practice MF-07, effect/practice layer, governed intelligence datasets exposed to Page Builder and Assistant through ordinary data paths, scheduler/orchestration and strict serving-vs-training separation. The current code is therefore a strong **engine foundation**, not a completed intelligence operating system.

### 2.5 Backend / API — **84/100**

This is a celebration area. The backend has broad domain/application/infrastructure separation, canonical definition contracts, customer assessment, relationship authority, analytics kernels, registry surfaces, safety validators, connectors, licensing/security and a very large test estate. Versioned definitions explicitly protect immutable history and allocate versions server-side. Relationship consumers and genericity have dedicated certification.

The main deductions are maintainability and convergence debt: several very large classes, compatibility APIs, direct SQL in some cross-cutting services, old V5/M1 surfaces and a few remaining material-specific paths. None of those erase the architectural progress.

### 2.6 User / Role — **59/100**

Authentication, claims, tenant context, effective entitlement structures, role-protected route families and identity/MFA capabilities exist. But the final design requires a canonical eight-role catalogue, three enforcement layers and an enterprise Users/Roles surface. Page Builder still presents only `Admin`, `DataManager`, `Engineer`, `Viewer` as its audience vocabulary. T-119/T-120 are therefore meaningful remaining product work rather than paperwork.

### 2.7 DB Link / schema discovery / OPC / interfaces — **55/100**

The connector architecture is substantial and includes CSV, Excel, PostgreSQL, SQL Server, MySQL, Oracle and historian-related classes. But 'class exists' is not the same as 'customer-certified available'. The backend capability catalogue remains appropriately conservative: CSV/Excel are current starter capabilities; PostgreSQL is conditional; SQL Server/MySQL are planned; Oracle requires certification/configuration; OPC-UA/Historian is future/conditional. T-224..T-226 remain the true production OPC work.

A notable current drift is that frontend descriptive text speaks as if PostgreSQL/SQL Server/MySQL browse and delta-import are simply available, creating a second capability truth that can disagree with the backend.

### 2.8 DB and schemas — **89/100**

The physical database model is now one of the strongest areas: canonical migration order, governed three-schema topology, definition-store convergence, database acceptance/replay work and semantic authorities. This is close to the design's intended topology.

T-251 is exactly the right final P1 task because 'migration order exists' is not the same as 'every physical object is classified'. The remaining acceptance is a deterministic catalogue/graph with zero unknown product objects, lifecycle/owner/purpose/keys/readers/writers/design-clause/retention/RLS/naming state. Until that is proven, this stays below 100.

### 2.9 License control — **66/100**

The current implementation includes tier concepts, feature gates, effective-entitlement DB structures, license UI hints and tier-toggle E2E assets. This is a solid commercial foundation.

The final design's F2 surface and T-121 acceptance are broader: signed token truth, capability comparison, live meters, consumption limits, expiration lifecycle, role+tier intersection and production enforcement across all routes/jobs/authoring surfaces. The remaining work is visible and important for the CEO persona.

### 2.10 Security — **68/100**

Strengths include JWT/secret configuration structure, Argon2id options, tenant-aware APIs, audit immutability, security architecture tests, development-only dev-seed gating and an MFA middleware that refuses privileged routes when enabled. The non-production test bypass is explicitly blocked in Production.

The deductions are significant: MFA defaults off; final RLS/tenant-key/secret-hygiene tasks remain open; one tenant-isolation 'proof' test is vacuous; the audit package is generated unmasked; and the release CI contradicts its own gate. Security primitives are better than the production certification state.

### 2.11 ChatBot / LLM / Assistant — **71/100**

The Assistant is unusually disciplined for its maturity: typed context, permission-first retrieval, deterministic planner, evidence handles, answer verification, context that narrows retrieval without becoming evidence, and guarded private/self-hosted model paths. This is much better than 'send the whole database to an LLM'.

Remaining gaps include final reconciliation/reference tools (T-223), ordinary intelligence-dataset integration, a legacy material-specific `run_kpi` tool, and recorded context limitations (`pageCode` heuristic and unfiltered snapshot filter context). The right direction is to make every Assistant tool consume the same governed registry/subject/evidence authorities as the rest of the product.

---

## 3. Persona scoreboard

| Persona | Score | Visual | Main judgement | Diagnostic breakdown |
|---|---:|---|---|---|
| **Developer / Maintainer** | **72** | `███████░░░` | Strong architecture/test/genericity discipline; dragged down by residue, task-ID filenames, compatibility wrappers, giant files, CI contradiction and duplicated constants. | Code hygiene 61; architecture boundaries 86; genericity 84; tests 83; repo cleanliness 54; change safety 76 |
| **Customer Software / IT** | **61** | `██████░░░░` | Configuration, DB topology and diagnostic surfaces are promising, but connector certification, operational logging correlation, job scheduling/performance and deployment portability need work. | Configuration 66; DB clarity 82; connector readiness 55; observability 63; job operations 45; deployment portability 55 |
| **Customer Operations / Quality** | **75** | `████████░░` | Current strengths align well with this persona: dashboards, data-quality, correlation, genealogy, evidence/refusal discipline and interactive investigation. Advanced authoring and production intelligence remain incomplete. | Data trust 86; investigation 80; BI usability 73; associative interaction 78; no-code self-service 67; advanced intelligence 64 |
| **Customer CEO / Executive** | **64** | `██████░░░░` | Executive visibility and trust are decent; final role administration, commercial entitlement administration and production reliability/operational proof are not yet enterprise-finished. | Executive information 72; trust/evidence 80; license 66; roles 59; security 68; operational readiness 53 |

### 3.1 Developer / Maintainer — **72/100**

A developer inheriting this repository now has considerably more architecture protection than in a typical fast-moving prototype: semantic contracts, canonical definition authority, genericity gates, relationship tests, known-answer analytics, deterministic ML protocol tests and CI truth tests. That is valuable.

The pain points are repository entropy and maintainability: one-off task packs, task-ID filenames, historical backups, generated test reports in source audits, dynamic legacy API bridges, duplicated display/config constants and several 600–1,600 line classes. The next quality step is not a huge rewrite; it is a **ratcheted cleanup policy**: no new task-named files, no new compatibility calls, no new hardcoded provider/role/chart truth, and split large files only along stable domain boundaries.

### 3.2 Customer Software / IT — **61/100**

This persona will appreciate the three-schema discipline, configuration screens, connector profiles, schema/mapping workbench, job history/logs and container/deployment tooling. The product is becoming inspectable rather than mysterious.

But IT will quickly ask questions the final design already anticipates: Which connectors are certified? What exactly is this job waiting for? Why did it consume this much CPU/memory? What is queue depth? Can I cap it? Which run produced this log? Can I restore yesterday's version? Can I rebuild the DB and explain every table? Can I upgrade and roll back? Those are exactly where current scores fall. Job/resource governance, observability/SAR, connector certification and production operations need the strongest boost.

### 3.3 Customer Operations / Quality — **75/100**

This is currently the best-served customer persona. Data-quality checks, evidence-grounded analytics, risk/correlation/genealogy, explicit refusal states, associative filters and persistent dashboards directly support investigation and quality work. The semantic and temporal work materially improves trust.

The next gains are less about adding another static dashboard and more about completing no-code self-service, intelligence datasets, model/practice/effect layers and keeping interaction latency predictable under load.

### 3.4 Customer CEO / Executive — **64/100**

The executive can already see a credible product story: read-only industrial intelligence, dashboards, evidence, role/tier concepts, licensing and security controls. The product is not dependent on 'AI magic' claims.

What prevents an enterprise-executive score above ~70 is operational/commercial closure: final Users/Roles administration, signed licence lifecycle/meters, stronger production security defaults, customer-grade deployment/restore/upgrade proof and a production job plane with predictable resource use. Executives care less about how many classes exist and more about whether the platform can be governed, contracted and operated safely.

---

## 4. Top 15 celebration areas — implementation at or above ~90% of the relevant design slice

| # | Area | Score | Task / authority | Why it deserves celebration | Remaining bounded gap | Evidence |
|---:|---|---:|---|---|---|---|
| 1 | **Governed three-schema topology + canonical migration authority** | **97** | T-087/T-088 | The implementation has moved from historical script accumulation toward an executable canonical order. Governed product data is converging on ppiq_meta / ppiq_plant / ppiq_staging, with duplicate creation adjudicated rather than ignored. | This is at or above 90% of the design intent for physical topology and rebuild authority. The remaining gap is the full zero-unknown physical catalogue in T-251. | IMP-DB; DESIGN-3 |
| 2 | **Unified versioned definition authority** | **96** | T-089/T-090 | Definition lifecycle has a serious create/update/current/version/list/publish contract, immutable previous versions and server-side version allocation. Convergence guards actively hunt residual compatibility-store usage. | The implementation is stronger than a simple CRUD reading of the design because it adds explicit anti-fabrication and convergence tests. | IMP-BE; IMP-TEST; DESIGN-3 |
| 3 | **Plant vocabulary / genericity authority** | **95** | T-093/T-094 | The product has moved industrial vocabulary out of generic runtime authority, added scoped genericity classification and created ratchets instead of relying on developer discipline. | The falsification/unknown-fingerprint discipline is arguably stronger than the prose design requirement. | IMP-BE; IMP-TEST; IMP-TOOLS |
| 4 | **Analysis Subject + Grain authority** | **95** | T-209/T-231 | Subject and grain are explicit semantic authorities rather than implicit table assumptions, enabling generic resolution and downstream relationship routing. | Very close to the semantic-wall design and a major prerequisite for customer-shaped data. | IMP-BE; IMP-DB; IMP-TEST; DESIGN-B |
| 5 | **Relationship model + preferred-path resolver convergence** | **95** | T-095/T-096/T-097 | Relationship members, cardinality/path authority and consumer convergence are represented in source and backed by runtime certification tests across the declared consumer families. | This is one of the clearest examples of replacing ad-hoc joins with a reusable product contract. | IMP-BE; IMP-TEST; BACKLOG-19 |
| 6 | **Canonical signal / aggregation semantics** | **94** | T-210 | The design rule that aggregation semantics are governed rather than guessed is now a reusable authority and is consumed by Canvas aggregate/window binding. | This materially improves trust over BI systems that silently default aggregation. | IMP-BE; IMP-FE; IMP-TEST |
| 7 | **Source Time Authority + temporal alignment discipline** | **94** | T-216/T-217 plus related kernels | Typed source-time semantics, known-answer tests and temporal alignment kernels are present rather than treating every timestamp as equivalent. | Excellent match to industrial-data truth requirements and a strong differentiator for operations/quality users. | IMP-BE; IMP-TEST |
| 8 | **Persisted page/widget definition replay gate** | **94** | T-202 | The product does not rely only on UI snapshots; persisted definitions are treated as replayable release truth. | This is stronger operational governance than a typical prototype BI implementation. | IMP-TEST; IMP-FEMISC |
| 9 | **Associative cross-filter state-machine core** | **91** | T-204 | Selection/filter behavior has dedicated release-truth assets and durable evidence rather than browser-walking alone. | Core associative semantics are close to the design, even though the whole Qlik-class UX is not yet at parity. | IMP-FEMISC; IMP-FE |
| 10 | **Page Builder persistence and layout round-trip** | **92** | M1 foundation consumed by M2 | Page creation/backing workspace, widget persistence, normalized geometry and reload behavior have substantive implementation and tests. | The functional persistence core is near the design; the remaining gap is enterprise authoring breadth/polish, not basic existence. | IMP-FE; IMP-FEMISC; DESIGN-3/4 |
| 11 | **One Shared Authoring Shell** | **94** | T-032/T-241 foundation | A single parameterized authoring shell carries schema tree, mode bar, board/SQL center, purpose-aware toolbox and debug log, instead of five divergent editors. | This is a major architecture win and closely follows the design's learning-once/use-everywhere philosophy. | IMP-FE; DESIGN-3/4 |
| 12 | **Typed Canvas ports and graph refusal** | **96** | T-241 | Required-port rules and incompatible-port refusal are explicit and testable before execution. | This is highly aligned with the design and is safer than a permissive drag-and-drop prototype. | IMP-FE; IMP-TEST; BACKLOG-19 |
| 13 | **Deterministic Canvas block algebra + governed aggregate/window contracts** | **92** | T-242 | Arithmetic/comparison/Boolean known answers, bounded-loop safeguards and governed aggregate/window refusal behavior exist with deterministic tests. | Within T-242's backlog scope this is a major achievement. A separate design-authority contradiction exists for board-level control flow and must be ruled centrally. | IMP-FE; IMP-TEST; BACKLOG-19; DESIGN-B |
| 14 | **SQL-mode compile/validate/dry-run/version lifecycle** | **94** | T-244 | Authored SQL has server validation, bounded preview, result feedback, immutable version semantics and no silent graph reconstruction. | Very close to the design's professional SQL-authoring safety contract. | IMP-FE; IMP-BE; IMP-TEST |
| 15 | **Assistant planner + permission-first retrieval + answer verifier** | **92** | T-179/T-180/T-181 | The Assistant has deterministic planning, permission-aware evidence packing, evidence handles and verification rather than a free-form LLM path. | For these task scopes, implementation is close to design and unusually strong on evidence discipline. | IMP-BE; IMP-TOOLS; DESIGN-B |

**Celebration interpretation.** These scores apply to the named design slice, not to the entire containing subsystem. For example T-242 is ~92% of its declared block-library contract while the **whole Canvas product** is only ~67% because persistence/job/model integration is still open.

---

## 5. Top 15 boost areas — <=45% of the relevant final-design slice

| # | Gap area | Est. realization | Backlog owner | Current reality | Why boost it | Evidence |
|---:|---|---:|---|---|---|---|
| 1 | **Advanced job scheduler, DAG, admission control, weighted pools, multithreading/load balancing** | **30%** | T-106/T-107/T-118 | Current RunNow orchestration is a switch over a few job types, and the import processor walks selected batches sequentially. The final design requires dependency DAGs, dual-predicate admission, weighted resource pools, bounded parallelism and observable queue pressure. | Boost immediately after Canvas persistence because many downstream capabilities depend on a real execution plane. | IMP-BE; DESIGN-4/6; BACKLOG-19 |
| 2 | **Canvas-to-job compilation, binding, monitor and per-block evidence** | **15%** | T-245 | T-245 is queued behind T-243. T-242's binder explicitly says T-245 will later compile declarations to governed runtime contracts. | This is the missing bridge between a powerful authoring surface and production execution. | IMP-FE; BACKLOG-19 |
| 3 | **Canvas model/intelligence blocks** | **10%** | T-246 | Purpose S4 exists structurally, but governed model/intelligence block invocation is still an open task. | Without it, the toolbox cannot yet satisfy the user's end-state vision of dragging ML/statistical intelligence into production flows. | IMP-FE; IMP-ML; BACKLOG-19 |
| 4 | **Canvas immutable save/reopen/edit full round-trip** | **40%** | T-243 | Backend lifecycle/reopen pieces exist, but the complete semantic graph round-trip with immutable prior versions is not yet closure-proven. | Finish this before treating Canvas as a real file/document authoring product. | IMP-FE; IMP-BE; BACKLOG-19 |
| 5 | **Production OPC-UA edge runtime** | **20%** | T-224/T-225/T-226 | The provider exists conceptually and code assets exist, but production security/session, browse/subscription and quality/recovery/spool-to-canonical acceptance are still open. | Large gap for plants that expect live/historian connectivity. | IMP-BE; BACKLOG-19 |
| 6 | **Production-certified relational connector breadth** | **35%** | T-207 + future/onboarding acceptance | CSV/Excel are the honest starter paths; PostgreSQL is conditional and SQL Server/MySQL are not represented as fully certified current capabilities by the backend truth catalogue. | Do not confuse connector class existence with customer-certified production support. | IMP-BE; IMP-FE |
| 7 | **Full Qlik / Power BI / Tableau-class authoring parity** | **42%** | Distributed across P2/P3/P5 | There are real charts, dynamic filtering, Page Builder and persistence, but the full professional property inspector, bookmark/selection management, pivot/crosstab breadth, formatting depth, authoring ergonomics and comparative polish are not yet equivalent to mature BI products. | This is a product-quality gap, not a claim that current dashboards are poor. | IMP-FE; IMP-FEMISC; DESIGN-2/4 |
| 8 | **Enterprise chart styling/property inspector/recommendation breadth** | **38%** | Partial in chart registry / UI tasks | Chart grammar and compatible-type refusal are strong, but styling remains spread across components and fixed palettes; the complete context-sensitive property inspector and BI-grade formatting surface are not yet visible as a mature whole. | Prioritize after correctness-critical P1/P2 work, not before. | IMP-FE; IMP-TEST; DESIGN-2/4 |
| 9 | **MF-05/MF-06/MF-07 + complete active model registry/orchestration** | **40%** | P4/P5 future scope | MF-01..MF-04 foundations are substantial, but the final Layer-B design explicitly requires seven model families, activation identity, refresh policies and governed data products. | Do not market the current ML foundation as the final intelligence engine. | IMP-ML; DESIGN-B |
| 10 | **Ordinary Page Builder consumption of all governed intelligence datasets** | **30%** | T-221/T-223/T-246 and downstream | Layer-B design requires intelligence outputs to look like ordinary governed datasets with no ML-specific path. That final integration is not complete. | This is the key to making advanced intelligence feel like one product instead of a separate lab. | DESIGN-B; BACKLOG-19 |
| 11 | **Canonical eight-role enforcement model** | **35%** | T-119 | The current UI/server vocabulary still exposes a four-role authoring set in important places, while the design requires the eight-role catalogue and three enforcement layers. | Critical for enterprise customer governance. | IMP-FE; IMP-DB; BACKLOG-19; DESIGN-3 |
| 12 | **Users/Roles enterprise admin surface** | **30%** | T-120 | Identity surfaces exist, but the final customer-facing CRUD/governance experience is not yet accepted against the M2 role catalogue. | This is visible to IT and executives and cannot remain a backend-only capability. | IMP-BE; IMP-FE; BACKLOG-19 |
| 13 | **Final production licence/entitlement administration and enforcement** | **40%** | T-121 | Feature/tier mechanisms and effective-entitlement structures exist, but Release-1 final enforcement/meter/admin acceptance remains open. | Important commercial and CEO-facing gap. | IMP-DB; IMP-FE; BACKLOG-19 |
| 14 | **Production observability: worker pools, queue depth, SAR, latency/deadline metrics** | **25%** | T-125/T-150/T-227 related | Job logging exists, but the Chapter 6 operations model expects pool utilisation, queue depth/oldest, refusals, wait/runtime, deadline misses, scan amplification and more. | Customer IT cannot operate a serious plant platform from logs alone. | IMP-BE; DESIGN-6; BACKLOG-19 |
| 15 | **Customer-grade deploy/upgrade/backup/restore/rollback acceptance** | **35%** | T-123/T-124/T-126/T-150/T-227 | A deployment stack and scripts exist, but the final clean-machine, backup/restore, upgrade/rollback and first-week production proof remain materially open. | This is the difference between software that runs and software a customer's IT department can own. | IMP-INFRA; IMP-TOOLS; BACKLOG-19 |

---

## 6. Top 20 bugs / quick hotfixes / release-truth defects

Backlog labels:
- **Yes** — a current open backlog task directly owns the corrective scope.
- **Partial** — a current task is related, but the concrete defect is not fully explicit in its acceptance.
- **Regression** — evidence contradicts a closed contract; it is not currently counted in remaining hours and should be routed as a concrete regression/hotfix rather than silently expanding another task.
- **No** — not currently represented in the execution backlog.

| # | Priority | Finding | Type | Counted in backlog? | Description / required hotfix | Evidence |
|---:|---|---|---|---|---|---|
| 1 | **P0** | **Canonical Jenkins E2E is gated off by default while its own CI truth test forbids a gated E2E stage** | BUG / RELEASE BLOCKER | Partial — T-150/T-227; related historical CI gates | Jenkins has `PPIQ_RUN_E2E:-off` inside a `when {}`. `CiPipelineTruthGateTests.E2e_stage_cannot_be_gated_off()` explicitly states that this makes the stage non-blocking. Resolve the contract immediately: either E2E is truly blocking or the governing test/design must be deliberately changed. | IMP-INFRA; IMP-TEST |
| 2 | **P0** | **Shared SQL authoring hardcodes `canonical_material_units` as the output entity** | GENERICITY REGRESSION | Regression against closed T-094/T-241; not represented as open hours | `SharedAuthoringShell.doSaveSql()` sends `canonicalEntity: "canonical_material_units"`. A shared generic authoring shell cannot silently bind every customer SQL definition to a steel/material-specific legacy view. Resolve target entity from the authored definition/registry/subject authority. | IMP-FE |
| 3 | **P1** | **Frontend connector descriptions overclaim PostgreSQL/SQL Server/MySQL relative to backend provider truth** | TRUTH / UX BUG | Regression against closed T-207; not currently open | Frontend prose says these providers browse schemas and import deltas as current capability, while backend catalogue describes PostgreSQL as conditional and SQL Server/MySQL as planned. Render the backend availability/certification description instead of maintaining a second hardcoded marketing truth. | IMP-FE; IMP-BE |
| 4 | **P1** | **Website states data is unified from '6 live source systems' although connector truth is not six certified live systems** | COMMERCIAL HONESTY HOTFIX | Partial — T-207/T-150 | A website proof claim can outrun the actual certification state. Replace with wording tied to the demo fixture/source-shape truth unless six real/certified live sources are runtime-proven. | IMP-WEB; IMP-BE |
| 5 | **P0 external sharing** | **Ultimate audit generated with `Mask Secrets: False`** | SECURITY / PROCESS HOTFIX | Yes — T-113 is related; current artifact issue exists now | The audit inventory includes env, PEM and token fixture classes plus machine/user/path metadata. Keep this package internal and regenerate a masked external-safe package before sharing outside the controlled engineering context. | IMP-00; IMP-AUDIT; IMP-TOOLS |
| 6 | **P1 production** | **Admin MFA enforcement defaults to false and tests describe that as the production go-live setting** | SECURITY HARDENING | Partial — T-150; no sufficiently explicit corrective M2 task | The middleware is well designed when enabled, but a production profile can ship with privileged surfaces not requiring MFA. Add a production startup/release gate that requires MFA for enterprise/admin deployment unless a formally accepted exception exists. | IMP-BE; IMP-TEST |
| 7 | **P1** | **Tenant-isolation 'proof' test is vacuous (`Assert.True(true)`)** | TEST / EVIDENCE BUG | Partial/Yes — T-112 production RLS hardening is the natural owner | This file proves only that a test method can run. It must not count as tenant isolation evidence. Replace it with a true two-tenant behavioral negative/positive test or exclude it from scorecards. | IMP-TEST |
| 8 | **P1 governance** | **T-245 is marked In Progress in backlog while it depends on still-open T-243** | BACKLOG STATE BUG | No — this is backlog metadata itself | Keep T-245 queued until T-243 is closed. Otherwise active-work charts overstate work in flight and violate the one-dependency-chain execution model. | BACKLOG-19 |
| 9 | **P1** | **Run Now has job types with no executor / default 'not implemented' branch** | FUNCTIONAL GAP WITH USER-VISIBLE FAILURE | Yes — T-106/T-118 | The job surface can expose definitions that the generic RunNow orchestrator cannot execute. Until executor registration is complete, capability availability should be explicit and the scheduler must not pretend all job types have the same runtime. | IMP-BE |
| 10 | **P1** | **Import batch queue processes candidates sequentially** | PERFORMANCE / ARCHITECTURE HOTFIX PATH | Yes — T-107/T-118 | The current `foreach` loop serializes mapping and optional data-quality work. This is safe but does not satisfy the planned bounded parallelism/load balancing. Do not simply add `Task.WhenAll`; implement weighted admission and per-family limits. | IMP-BE; DESIGN-4 |
| 11 | **P2** | **Endpoint-style job log events write `runId = null`, weakening cross-family correlation** | OBSERVABILITY HOTFIX | Partial — T-125/T-245 | The design says operators should pivot from metrics/logs by run identifier. Generate/propagate a correlation/run identifier for every executable job-family event, not only some runtime paths. | IMP-BE; DESIGN-6 |
| 12 | **P2** | **`phase9:matrix` package script enumerates Playwright tests with `--list`** | TEST-TRUTH HOTFIX | Related to historical T-205/T-250; no open dedicated task | If this command is used as a gate, it is false assurance because it lists rather than executes. Either rename it clearly as enumeration-only or make the gate command run the matrix. | IMP-FE; IMP-AUDIT |
| 13 | **P2** | **Audit scanner reports its own regex definitions and prevention-test names as CRIT** | AUDIT TOOL BUG | No | Raw '20 CRIT' is not 20 production blockers. Add source-classification/exclusions for scanner self-reference, test guard text, backups and generated reports; reserve CRIT score for executable runtime/canonical CI. | IMP-AUDIT; IMP-TOOLS |
| 14 | **P1** | **Assistant `run_kpi` tool directly queries `public.canonical_material_units` and emits `material_unit_count`** | GENERICITY / SEMANTIC-WALL HOTFIX | Partial — T-223; regression against genericity doctrine | A generic Assistant tool should resolve a governed measure/dataset/subject from registry/definition authority rather than bake a material-specific KPI into infrastructure. Either retire this legacy tool or route it through canonical registry semantics. | IMP-BE; DESIGN-B |
| 15 | **P2** | **Assistant page context uses a known `pageCode` path heuristic** | CONTEXT CORRECTNESS HOTFIX | Partial — T-223 | The older closure evidence explicitly records that pageCode is derived from the first path segment. Bind to the canonical Page Definition identity when available so detail routes do not misidentify context. | IMP-TOOLS |
| 16 | **P2** | **Assistant snapshot evidence records empty filter context in the known legacy path** | CONTEXT / EVIDENCE HOTFIX | Partial — T-223 | The older closure evidence records `filter_context_json = {}` because snapshots execute unfiltered. Complete filtered snapshot semantics before claiming fully context-bound evidence. | IMP-TOOLS |
| 17 | **P1 portability** | **Deployment/smoke tooling retains hardcoded fallback server IP `178.105.152.180`** | PORTABILITY HOTFIX | Partial — T-123/T-150 | Examples in documentation are acceptable, but executable defaults in smoke/runtime-env/exposure scripts should require environment/customer profile values or a clearly named demo profile. | IMP-TOOLS; IMP-AUDIT |
| 18 | **P1** | **Page Builder audience-role selector still uses only Admin/DataManager/Engineer/Viewer** | ROLE CONTRACT HOTFIX / OPEN FEATURE | Yes — T-119/T-120 | Do not let locally hardcoded four-role vocabulary become the final authority. Bind role options to the canonical eight-role registry/entitlement contract. | IMP-FE; DESIGN-3 |
| 19 | **P2** | **Current audit source set includes generated Playwright report assets/backups, polluting scans and size** | AUDIT / REPO HYGIENE HOTFIX | No | Generated report bundles and historical backups create false TODO/security/colour findings and make reviews harder. Exclude generated artifacts from the canonical audit corpus or classify them as non-runtime evidence. | IMP-00; IMP-FE |
| 20 | **P0 release evidence** | **No single canonical current-HEAD release run is contained in this audit package after the discovered CI contradiction** | VALIDATION HOTFIX | Yes/Partial — T-150/T-227 | After fixing CI truth, produce one machine-readable current-HEAD certification: build, targeted architecture/security gates, frontend unit, mandatory E2E release truth, fresh DB replay, startup/smoke. Source presence alone cannot certify release green. | IMP-00; IMP-TEST; IMP-INFRA |

### 6.1 Important distinction: audit CRIT count

The raw audit says **20 CRIT / 43 WARN / 26 INFO**, but that is not the number of production blockers. The scanner catches its own regex definitions and guard tests that *forbid* unsafe patterns. Executive severity must therefore be based on runtime reachability and canonical authority, not raw regex hit count. The Jenkins E2E contradiction is a true high-priority defect; a test method named `Pipeline_never_swallows_failures...` is not evidence that the pipeline swallows failures.

---

## 7. Top 20 design drift / dirty implementation / genericity and enterprise-rework items

| # | Drift / rework | Category | Backlog coverage | Why it needs rework | Evidence |
|---:|---|---|---|---|---|
| 1 | **Task-ID-based repository filenames** | Repo naming governance | No dedicated cleanup task; future work must obey filename law | Tests/tools such as `T171_*`, `T177_*`, `T065*`, `Apply-PPIQ-T213-*` encode backlog identity into repository structure. Keep task IDs in commits/evidence, not implementation filenames. Rename opportunistically without reopening functional tasks. | IMP-TEST; IMP-TOOLS |
| 2 | **Root-level task application packs mixed with enduring tooling** | Repo hygiene / lifecycle | No | Large one-off Apply/Close scripts remain beside long-lived tools. Archive immutable closure evidence outside runtime/tool roots after task closure, or keep only semantic reusable validators. | IMP-TOOLS |
| 3 | **Historical backups, `.broken`, `.quarantine`, `.t04bak`, timestamped source copies** | Repo cleanliness | Partial via T-251/production hardening, but mostly No | Some quarantine is legitimate evidence, but the repository/audit surface contains enough residue to make code ownership and scans noisy. Define a retention policy: runtime source, executable migration authority, evidence archive, and disposable residue must be separate. | IMP-00; IMP-DB; IMP-FE |
| 4 | **Generated Playwright report assets present inside audited frontend tree** | Build artifact hygiene | No | These bundles should not influence source quality metrics or lint/audit signals. Move/exclude generated reports from source-audit classification. | IMP-FE; IMP-FEMISC |
| 5 | **Very large orchestration/components ('god files')** | Maintainability | No explicit refactor task | Examples include SharedAuthoringShell (~1.1k lines), CustomerAssessmentEngine (~1.6k), CanonicalDefinitionWriter (~1k), large endpoint classes and identity surfaces. Split by stable domain responsibilities without fragmenting contracts. | IMP-BE; IMP-FE |
| 6 | **`productApi as legacyApi` dynamic wrappers with `any` calls** | Type safety / compatibility debt | No explicit final removal task | Several new API slices dynamically dispatch into the legacy monolithic client. Good as a migration bridge, poor as an enduring enterprise boundary because missing methods become runtime errors. Ratchet consumers onto typed modules. | IMP-FE |
| 7 | **Frontend provider descriptions are a second capability registry** | Single-source-of-truth drift | Regression against T-207 | Availability, certification and copy should come from backend/provider registry metadata; only UI-friendly layout belongs locally. | IMP-FE; IMP-BE |
| 8 | **Hardcoded chart/theme colours distributed across components** | Design-system drift | Partial — design-system work exists, no single cleanup task | The app has tokens and a design system, but many components still embed hex palettes/tooltip styles. Enterprise BI polish improves when colour roles, accessible contrast and semantic palettes are registry/theme driven. | IMP-FE |
| 9 | **Canvas SQL save hardcodes a material-centric canonical output** | Genericity drift | Regression against T-094/T-241 | Same finding as hotfix #2, categorized here as architectural debt: the authoring model must carry an explicit output subject/entity selected from registry authority. | IMP-FE |
| 10 | **Assistant legacy KPI tool is material-specific** | Semantic-wall drift | Partial — T-223 | Use governed measures/subjects instead of `material_unit_count` and `canonical_material_units`. This is especially important because Assistant tools become public semantic interfaces. | IMP-BE; DESIGN-B |
| 11 | **Job orchestration is a switch statement over concrete job families** | Extensibility / registry drift | Yes — T-106/T-107 | Move toward registered executor capabilities and job target/version policy so adding a family is data/DI-driven rather than editing the central switch. | IMP-BE |
| 12 | **Import queue processing couples candidate selection, mapping, status mutation and optional DQ scan** | Separation/resource-governance drift | Yes — T-107/T-118 | Keep orchestration thin; executor work, admission, retry, metrics and resource slots should be explicit services/policies. | IMP-BE |
| 13 | **JobLogService writes SQL directly from the API observability layer** | Layering/maintainability drift | Partial — T-125 | The behavior is useful and the table is now correctly in `ppiq_meta`, but a repository/port contract would improve testability, retention policy enforcement and migration safety. | IMP-BE |
| 14 | **Page Builder role options are hardcoded to four current roles** | Registry drift | Yes — T-119/T-120 | Role vocabulary should be server/registry driven and entitlement-filtered, not locally compiled into Page Builder. | IMP-FE |
| 15 | **T-242 board-level FOR/WHILE conflicts with Layer-B design authority** | DESIGN/BACKLOG CONTRADICTION | Backlog itself created the contradiction | Backlog v2.19 explicitly requested ForEach/RepeatN/WhileBounded and Worker 2 implemented them correctly; Layer-B says control flow belongs to job orchestration and never on a board. Central must rule and then update one authority. Do not blame implementation for following backlog. | BACKLOG-19; DESIGN-B; IMP-FE |
| 16 | **T-242 board-level arithmetic/logic blocks conflict with Layer-B 'expression blocks, not board blocks' rule** | DESIGN/BACKLOG CONTRADICTION | Backlog itself created the contradiction | Layer-B puts arithmetic/comparison/logic inside the block they configure, while T-242 asks for executable board block kinds. Reconcile UX/graph semantics before expanding this pattern into S3/S4. | BACKLOG-19; DESIGN-B; IMP-FE |
| 17 | **Method palette endpoint is still code/enum driven and says so** | Registry-completeness drift | Future registry work; not fully covered | The source explicitly admits adding a method still requires code and becomes compliant only when `ml_method_definitions` drives it. Keep the wire contract but replace the implementation authority. | IMP-BE |
| 18 | **Multiple compatibility/legacy physical views and old public canonical names remain referenced** | Database convergence debt | Yes/Partial — T-251 and later migrations | Some references are intentional compatibility views, but final zero-unknown catalogue must label every one as platform/compatibility/retired and prevent new product logic from depending on grandfathered names. | IMP-DB; IMP-BE; IMP-TEST |
| 19 | **Marketing/proof copy can drift independently of connector/runtime truth** | Product honesty drift | Partial — T-207/T-150 | Website claims should be generated or verified against a capability snapshot, especially for 'live', 'available', model and connector statements. | IMP-WEB; IMP-BE |
| 20 | **Audit severity model mixes runtime, tests, tooling, backups and self-reference** | Governance/review drift | No | A professional audit needs source class + exploitability/runtime reachability + canonical-gate relevance. Keep raw hits, but calculate the executive severity score only from executable/current authority paths. | IMP-AUDIT |

---

## 8. Central architecture rulings recommended

### R1 — Keep T-243 current; keep T-245 queued
T-245 depends on T-243 + T-244. T-244 is closed, T-243 is not. Do not represent T-245 as active until the immutable Canvas round-trip is closed.

### R2 — Resolve the T-242 control-flow authority contradiction before extending the toolbox
Do **not** delete Worker-2 implementation as a reflex. The backlog explicitly requested bounded loops, so the implementation followed execution authority. Decide whether the Layer-B rule ('FOR/WHILE are orchestration') supersedes T-242. If yes, migrate loop semantics to jobs/orchestration and keep any expression-level repeat semantics only if separately justified; then correct backlog/design together.

### R3 — Resolve arithmetic/logic placement before S3/S4
Layer-B says arithmetic/comparison/logic are expression blocks inside the block they configure; T-242 treats them as executable block kinds. Pick one compositional model before model/statistical blocks expand, otherwise the Canvas will accumulate two competing expression languages.

### R4 — Backend capability catalogue is the connector truth
Frontend and website copy must consume or be validated against the backend capability/certification state. A UI description must never make a planned connector sound live.

### R5 — Genericity applies to authoring and Assistant tools too
The closed vocabulary sweep must be a ratchet. `canonical_material_units` in a shared authoring save path and a hardcoded Assistant material KPI are concrete new regression candidates; genericity is not complete if only dashboard filters are generic.

### R6 — Fix CI truth before any 'release green' statement
Until the E2E gate contradiction is resolved and a fresh current-HEAD pipeline proof is executed, source maturity can be high but release certification must remain conditional.

---

## 9. Recommended execution sequence

### Immediate — P0 / next working session

1. **Fix Jenkins E2E gate contradiction** and run the exact CI truth test that detects it.
2. **Hotfix shared Canvas SQL target genericity** — replace `canonical_material_units` with governed target/subject resolution.
3. **Fix connector/website capability truth copy** so frontend/marketing cannot overclaim certification.
4. **Keep the current audit package internal**; generate a masked external-safe package.
5. **Correct backlog state T-245 → Queued** while T-243 remains open.

### Current critical tasks

6. **Finish T-251** and close P1 only when the deterministic physical catalogue reports zero unknown product objects.
7. **Finish T-243** with a real create/save/close/reopen/edit/new-version/old-version-unchanged/rollback proof.
8. Then **T-245** — compile Canvas to governed jobs, run/monitor, per-block evidence.
9. Then **T-246** — model/intelligence block invocation after the Canvas expression/control-flow ruling.
10. Then **T-247** — same-binary first-customer Day-1 Golden Journey.

### Parallel architectural boost lane

11. Start job-plane convergence (T-106/T-107/T-118): executor registry, target/version policy, dependency DAG, admission, weighted pools, bounded parallelism and metrics.
12. Start production connector/OPC acceptance only when it does not steal ownership from the critical Canvas/job lane.
13. Prepare T-119/T-120/T-121 so enterprise role/licence administration consumes one canonical entitlement authority.
14. Ratchet repository hygiene: no new task-ID filenames, no new legacy API dynamic calls, no new product vocabulary literals.

---

## 10. What I would *not* do

- I would **not** spend a major sprint polishing colours before T-243/job execution/release truth are fixed.
- I would **not** rewrite the working definition/relationship/database architecture; those are current strengths.
- I would **not** count every raw audit CRIT as a product defect.
- I would **not** claim SQL Server/MySQL/OPC production readiness because connector classes exist.
- I would **not** call T-242 bad work because the design/backlog authorities disagree.
- I would **not** reopen closed tasks broadly. Only concrete regressions such as hardcoded genericity/capability truth should be routed narrowly.
- I would **not** parallelize import jobs with unbounded `Task.WhenAll`; the design is right to demand weighted/bounded admission.

---

## 11. Evidence source index

| Evidence ID | Source | Role in this review |
|---|---|---|
| IMP-00 | `00_Master_Index_08Sep2026_134853.txt` | Repository size/classification/audit generation settings |
| IMP-BE | `01_Backend_Core_08Sep2026_134853.txt` | Backend/API/jobs/security/Assistant/connectors/domain implementation |
| IMP-DB | `02_Backend_Database_08Sep2026_134853.txt` | SQL/migration/schema/DB authority and security persistence |
| IMP-TEST | `03_Backend_Tests_08Sep2026_134853.txt` | Integration/unit/architecture test evidence and proof quality |
| IMP-ML | `03A_ML_Runtime_08Sep2026_134853.txt` | Python ML runtime/model/artifact/test foundation |
| IMP-FE | `04_Frontend_App_08Sep2026_134853.txt` | HMI/Page Builder/Canvas/charts/roles/connector UI |
| IMP-FEMISC | `05_Frontend_Misc_08Sep2026_134853.txt` | Playwright/E2E/release-truth assets |
| IMP-INFRA | `06_Infrastructure_08Sep2026_134853.txt` | Jenkins/Caddy/deployment pipeline |
| IMP-TOOLS | `07_Tools_Validation_Misc_08Sep2026_134853.txt` | validation/deployment/evidence/handover material |
| IMP-WEB | `08_Website_08Sep2026_134853.txt` | commercial/product proof and public claims |
| IMP-AUDIT | `10_Audit_Signals_08Sep2026_134853.txt` | raw audit signals; reviewed for true/false/contextual positives |
| DESIGN-2 | `PPIQ_Chapter2_Technical_Overview(2).md` | product/UX/architecture target |
| DESIGN-3 | `PPIQ_Chapter3_General_Technical_Function_Description(2).md` | end-to-end flows/surfaces/data contracts |
| DESIGN-4 | `PPIQ_Chapter4_Specific_Technical_Function_Description(2).md` | detailed Canvas/BI/jobs/analytics design |
| DESIGN-6 | `PPIQ_Chapter6_Infrastructure_Website_Administration(2).md` | production operations/monitoring/deployment target |
| DESIGN-B | `PPIQ_Layer_B_Architecture_Design_Pack(10).md` | full intelligence/model/orchestration/Canvas registry target |
| BACKLOG-19 | `PPIQ_Backlog_v2_19_0_TwoReleaseProduction_04Sep2026(3).xlsx` | current M2 execution authority; corrected by later verified closures where session evidence supersedes stale status |

---

## 12. High-value evidence excerpts / paths to inspect first

For a fast engineering follow-up, inspect these exact implementation paths in the audit package:

- `Jenkinsfile` — E2E `when { ... PPIQ_RUN_E2E:-off ... }`.
- `Backend/tests/PlantProcess.Architecture.Tests/CiPipelineTruthGateTests.cs` — `E2e_stage_cannot_be_gated_off()`.
- `Frontend/PlantProcess.Web/src/authoring/SharedAuthoringShell.tsx` — SQL run/save path and `canonicalEntity: "canonical_material_units"`.
- `Frontend/PlantProcess.Web/src/.../DbConfigurationTab...` — `PROVIDER_DETAIL` hardcoded connector claims.
- `Backend/PlantProcess.Application/Integration/Services/Jobs/JobRunOrchestratorService.cs` — supported/unsupported RunNow families.
- `Backend/PlantProcess.Application/Integration/Services/Import/ImportBatchQueueProcessorService.cs` — sequential candidate loop.
- `Backend/PlantProcess.Api/Observability/JobLogService.cs` — DB + Serilog job events, correlation/runId contract.
- `Backend/tests/PlantProcess.Api.IntegrationTests/Security/Phase04TenantIsolationProofTests.cs` — vacuous `Assert.True(true)` evidence.
- `Backend/PlantProcess.Api/Security/AuthOptions.cs` + `AdminMfaRequirementMiddleware.cs` — MFA default and production behavior.
- `Backend/PlantProcess.Infrastructure/Assistant/AssistantTools.cs` — legacy material-specific `run_kpi` tool.
- `Backend/PlantProcess.Application/Definitions/Interfaces/IDefinitionService.cs` and canonical definition writer/tests — definition maturity.
- `Frontend/PlantProcess.Web/src/canvas/...` + T-241/T-242 tests — typed graph/block contract.
- `ML/src/ppiq_ml/...` + `ML/tests/...` — MF-01..MF-04 and protocol/artifact foundation.

---

## 13. Final management statement

The current implementation deserves real credit. The project has achieved difficult architectural work that is easy to underestimate because it is less visible than a polished dashboard: genericity ratchets, canonical migration authority, immutable definitions, relationship resolution, temporal/aggregation semantics, deterministic ML protocol and evidence-grounded Assistant behavior. Those are expensive foundations and several are already in the 90%+ design-close category.

The next stage should be **convergence, not feature scattering**. Close P1 with T-251; make Canvas a real persistent executable document with T-243→T-245→T-246→T-247; build the governed job plane; then invest in connector/OPC production proof, role/licence operations and BI polish. If those lanes are sequenced correctly, the project can turn its strong architecture into a genuinely customer-operable enterprise product without throwing away the progress already made.

**Central review status:** implementation direction accepted; no broad rollback recommended. P0 release-truth/genericity/capability-copy hotfixes required, followed by the already-defined critical path.
