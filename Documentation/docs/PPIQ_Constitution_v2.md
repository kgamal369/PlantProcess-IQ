# PlantProcess IQ - Product Constitution and Engineering Specification

**Version 2.0 | 25 July 2026 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

## Authority

This document is the single authoritative statement of what PlantProcess IQ is, what it must do, how it must behave, and how it is judged. Where any other document, comment, backlog entry, slide, or conversation conflicts with this file, this file wins.

It supersedes and replaces, in full:

| Superseded document | Disposition |
|---|---|
| `rules.txt` (v1, 1535 lines) | Absorbed. Rules 1-7 rewritten to specification grade; the two binding design specs preserved as Parts III.14 and III.15; the Aspects of Review material moved to Chapter A. |
| `concept.md` v1.0 (12-Jul-2026) | Absorbed. Its sharpened rule statements and the 15-step journey supersede the corresponding `rules.txt` prose. |
| `concept_Amendment_6_Schema_Topology_DRAFT.md` | Absorbed and hereby **ratified** as Part III.16. |
| `PPIQ_Identity_and_Topology_v4.md` | Absorbed as Chapter B. |
| `Aspects_of_Review_Personas_A11-A13.md` | Absorbed into Chapter A as personas A11-A13. |

**Change control.** Edits require the author's explicit approval and a version bump. Every derived document (Roadmap, Backlog, Doctrine, website copy, deck) must re-validate against this file and cite it.

---

## What changed in v2.0, and why

This version was produced after a full evidence-based reconciliation of the written concept against the built system on 25 July 2026. Three classes of change were made.

**1. Removed: superseded and junior-grade material.**
The original `rules.txt` was written as a founder's working notes. Its intent was correct throughout; its expression was frequently informal, sometimes contradictory across sections, and in places described a less advanced concept than the one already implemented. Every such passage has been rewritten to specification grade. No requirement was dropped. Where two sources disagreed, the more advanced formulation was kept and the weaker one deleted rather than preserved alongside it.

**2. Added: capabilities that exist in the implementation but appeared in no document.**
Several of the strongest properties of the built system were never written down, which meant they could have been deleted by a future cleanup as unexplained code. These are now constitutional:

- The **Readiness Gate** and its five named dimensions, with thresholds (Part II.8). This is the product's principal competitive moat and it appeared in no prior document.
- The **Outcome and Feature Registry** as a first-class metadata layer (Part II.9).
- The **multi-grain canonical model** with `native_grain` preservation, which is the mechanism by which Rule 1 is actually achieved (Part II.9.3).
- The **evidence and honest-framing contract** carried inside every stored finding (Part I.4).
- **Statistical rigour requirements** already implemented beyond the written concept: FDR q-values, bootstrap stability, effect sizes, and stratum survival (Part II.7.4).

**3. Added: laws derived from defects found on 25 July 2026.**
Each of the following is a rule whose absence caused a real, measured failure. They are stated as prohibitions so the same failure cannot recur:

- The **Single Engine Implementation Law** (Part II.7.6).
- The **Namespace Authority Law** (Part II.9.2).
- The **Window Anchoring Law** (Part II.7.5).
- The **Typed Outcome Reading Law** (Part II.9.4).
- The **Grain Assignment Law** (Part II.9.3).

A full merge ledger, recording what came from where and what was deleted, is in the Appendix.

---

# PART I - CONSTITUTION

The four chapters of Part I are permanent. They may be extended but not weakened.

---

## 1. Vision, Problem, and Value Proposition

### 1.1 The problem

Every large process plant, in every industry, shares one structural condition: the product passes through many stages, many machines, and many inspection devices, and each of those records what happened to it in its own database, its own spreadsheet, or its own log file, under its own vocabulary.

This produces three consequences that no plant escapes.

**Fragmentation.** Each production unit and each inspection device typically presents a human-machine interface that displays only its own data. There is no surface on which the whole plant is visible at once.

**Dependency on scarce expertise.** When a quality problem appears, the plant needs a person with many years of experience who can hold the whole process in their head, connect a defect observed at the end of the line to a parameter deviation that occurred at an early stage, and do so by memory or by reading logs manually. That person is rare, expensive, and a single point of failure.

**Unrealised operational value.** The same fragmentation blocks the systematic reduction of downtime and the systematic improvement of throughput and yield. The data required to find the cause exists; it is not joined, and nobody has the time to join it by hand.

### 1.2 The product

PlantProcess IQ (PPIQ) is a **generic, read-only, evidence-grade process-to-quality intelligence platform** for manufacturing plants of any industry.

It installs empty. It connects to the customer's existing databases through read-only links. It imports their data incrementally. Its Engine then discovers the relationships between process parameters and quality outcomes from the data alone, and explains what it found with citations, sample sizes, and honest statistics.

It is sold per plant in the EUR 100k class, across steel, paper, food and beverage, minerals, tyres, aluminium, pharmaceutical and other process industries, because nothing inside it knows any industry.

### 1.3 The five layers of assistance

The product delivers value in five ascending layers. Each layer is independently useful; each is license-gated at a defined tier.

| Layer | What it delivers | Available from tier |
|---|---|---|
| **1. Unified visibility** | Dashboards, widgets, charts, heatmaps and interactive filters that place data from every production unit and inspection device side by side, so an ordinary observer can see plant-wide patterns without expertise. | Light |
| **2. Statistical intelligence** | Correlation and statistics jobs relating every process parameter to every defect, downtime cause and KPI across the whole plant, rendered so that hard-to-see relationships become visible. | Pro |
| **3. Machine learning** | Model-based jobs that learn patterns in the data, identify probable root causes of quality problems, locate throughput bottlenecks, and identify the drivers of recurring failures. | Pro Plus |
| **4. Prediction** | Forward-looking statements grounded in learned patterns. Example: a material that ran hotter than normal at an early stage is flagged as elevated-risk for a specific downstream defect before that defect occurs. | Pro Plus |
| **5. Recommendation** | Suggested actions at later production stages to avoid the predicted outcome, and suggested operating adjustments where a failure mode is forecast. | Pro Plus |

Two capabilities cut across all five layers:

- **The assistant.** A retrieval-grounded conversational surface, so that a user who cannot configure a job, build a dashboard or read a chart can still reach an answer by asking. Enterprise tier.
- **The value engine.** Every suggestion, prediction and statistical finding carries a quantified economic consequence, expressed as a bounded range with every input traceable. This is what converts a finding into a purchasing argument.

### 1.4 What PPIQ is not

It is not a MES, an L2 tracking system, a SCADA, a historian, or a generic BI tool. It does not replace any of them. It reads from them.

It never writes to a customer system and never participates in control. Every statement it makes about cause is framed as a suspected contributor, never as a guaranteed root cause.

---

## 2. The Three Product Rules

These three rules are the constitution's core. A build that violates any of them is not shippable regardless of any other quality.

### Rule 1 - GENERIC ONLY

The product contains **no line, no word, no page, no component, no schema object, and no code branch prepared for any specific dataset, industry, plant, or customer.**

No demo content ships inside the product.

Industry knowledge enters the product by exactly two doors: as **customer data**, imported through the pipeline; or as **user configuration**, authored in the product's own low-code surfaces. There is no third door.

**Why the rule is absolute.** Every plant differs in database types, database structures and table shapes, inspection devices and the defect vocabularies they emit, production line structure, process and workflow, and in what each individual CEO and process engineer cares to monitor. A single binary must serve all of them by configuration, never by code. Any hardcoded value is an implicit assumption about one customer, and it is always wrong for the next one.

**What this covers explicitly:**

- Data model classes must accept pharmaceutical, food, steel and tyre data without alteration.
- Connectors must handle all six source classes without alteration.
- Workflows, process definitions and routings are configuration.
- Defect catalogues and parameter definitions are **customer data**, imported, never seeded. See Rule 2.
- Vocabulary in code, identifiers, and user-visible labels must be industry-neutral. A canonical entity named after one industry's product form is a violation even when it functions correctly.

**Enforcement.** The generic-only lint over the projection path; the migration-path gate that fails the build if `scripts/` or `seed/` create demo-named objects. Both gates must be falsified once, that is, seen to fail red, before they are trusted.

### Rule 2 - STARTS EMPTY, AND THE DB-LINK IS THE ONLY DOOR

On day one at the customer, the plant data schema contains **zero rows**.

Every row of plant data arrives exclusively through: DB-link import, then staging, then generic projection.

**Taxonomy is plant knowledge, not product knowledge.** Defect catalogues, parameter definitions, and every other reference vocabulary start empty and are imported from the customer's own definition tables through the same pipeline. Flat steel defects differ from paper defects differ from mineral water defects. Every production unit, every semi-product, and every inspection device carries its own vocabulary. `DefectCatalog` and `ParameterDefinition` are projector targets, not seed targets.

**The only pre-populated class is identity:** site and plant identity, the license artifact, and the SOU support account.

**The Admin Golden Rule.** The `sysadmin` account is the SOU support account. It is auto-provisioned at install and is undeletable. The customer's own administrator is created as a manual commissioning step and is never baked into the install image.

**Out-of-band writes are prohibited** in any documented workflow. Administrative resets are product endpoints that write audit records, never direct database statements.

**The one-line proof.** After the schema topology of Part III.16 is in force, an auditor proves this rule with a single query returning zero across the plant schema on a fresh install.

### Rule 3 - THE JOURNEY IS THE PRODUCT

The canonical journey of Part II.5 is the acceptance specification.

A milestone is complete when the journey's steps hold. A demonstration shows journey steps working, never staged substitutes.

**Honesty over spectacle.** Anything not real is stated as roadmap in one scripted sentence, and nothing else is hedged.

---

## 3. The Emulation Doctrine

The emulated factory is a stand-in for a customer's databases and it lives **outside the product**.

It consists of source containers that mirror the shape of real plant systems: a meltshop PostgreSQL, a downtime MySQL, a surface-inspection MySQL, a pickling-line MSSQL, caster and hot-strip-mill Oracle instances, and file-based CSV and Excel sources.

**Test data, including deliberately planted statistical relationships, is placed in the emulated source and never in the product.** A planted relationship, for example a pre-verified odds ratio between a process parameter and a specific defect together with a null control that must not produce a finding, exists so that the Engine can be proved to discover it blind, after import, having been told nothing.

Emulation assets are versioned, reproducible, and stored durably, never on a single laptop. Mappings for emulated sources ship as fixtures outside the product, never as code branches.

**The demo-versus-product doctrine.** The application is always the generic product. A demonstration is the real generic application running against emulated external source data. There is no demo build, no demo branch, and no demo code path.

---

## 4. The Honesty Contract

*This chapter is new in v2.0. It was implemented before it was written down.*

The product's principal competitive advantage is not that it computes. Competitors compute. The advantage is that it **refuses to compute when the data cannot support a defensible answer, and says exactly why.**

This is the moat. It is the first thing a technical evaluator will test and the last thing a competitor can copy quickly, because copying it requires being willing to show a customer a red status.

The contract has six binding clauses.

### 4.1 Abstention is a first-class result

An analysis that cannot be defended must abstain, name the dimension that blocked it, and state the measured value against the required threshold. A blocked run is a successful product behaviour, not a failure. It is recorded with a real run identifier and is fully explainable from the database alone.

### 4.2 No gate is ever weakened to produce a result

Readiness thresholds, refusal logic and evidence requirements may be **tuned by a human being through a governed configuration change with a recorded justification**. They may never be lowered to make a demonstration greener, to make a job pass, or by any automated process including the Supervisor of journey step 14.

Any change to a threshold is a provenance record naming who changed it, from what, to what, and why.

### 4.3 No number without resolvable evidence

Every number the product displays traces to a query, a definition, and a data population. The assistant may not render an uncited number: a no-fabrication guard rejects any figure lacking a resolvable evidence handle **before display**, not after.

### 4.4 Deterministic engines compute, the language model only explains

All arithmetic, ranking, correlation and scoring is performed by deterministic engines. The language model's role is to retrieve, cite and explain. It never computes, never ranks, and never originates a figure.

### 4.5 Every finding carries its framing

Findings are persisted with their honest framing attached as data, not as user-interface copy. The stored framing states that the result is a statistical association and not a guaranteed root cause, records the method selected, and records that no language model participated in the compute path.

Framing that lives only in the presentation layer can be lost in an export, a report, or a screenshot. Framing stored with the finding cannot.

### 4.6 Honest empty states everywhere

An empty result is displayed as an empty result. There is no fabricated content, no placeholder chart, and no sample data rendered as though it were the customer's. Where emulated or sample data is displayed at all, it carries a visible disclosure badge.

A filtered-to-empty state is distinguished from a genuinely-empty state, and tells the user what to relax.

---

# PART II - THE PRODUCT SPECIFICATION

---

## 5. The Canonical Journey

Fifteen numbered steps plus a fourth low-code surface. This is the acceptance specification of Rule 3.

| # | Step | What must be true |
|---|---|---|
| 1 | **Connect** | The user creates and configures DB-links to customer sources. Connectivity is tested before save. Credentials are masked on read-back. Read-only is enforced at the connection layer. Throttling applies: row caps, rate limits, approved time windows. |
| 2 | **Schedule imports** | Each DB-link or dataset binds to an import job with a schedule and a monitor. Registering a dataset makes it due. Cadence is admin-set from two minutes to several days. |
| 3 | **Incremental import** | Each job cycle pulls only the delta, tracked by a cursor or watermark per dataset, into the staging layer as import batches and staged records holding raw payloads. Staging holds a source-shaped copy, never the product's shape. |
| 4 | **UI-1: Data Preparation** | The user prepares, filters, links and groups staged data and maps it to the plant schema: field maps per target entity, with restrictions that route the right data to the right table. Customer defect rows reach the defect catalogue; readings reach parameter observations; units reach material units; genealogy keys reach edges. The output artifact is a Mapping Definition that the projector consumes. A step-4 act is not complete until a Mapping Definition exists. Full specification: Part III.14. |
| 5 | **Loading jobs** | Each mapping binds to a data-loading job with schedule and monitor. On import completion the projector runs automatically for the batch's active mapping. |
| 6 | **Loaded** | The generic projector writes canonical entities: material units, aliases, process step executions, parameter observations, quality events, genealogy edges, defect catalogue entries and parameter definitions. Idempotent per batch. Typed field-level errors. Not-null coverage from site configuration. Zero dataset identifiers in the canonical layer. |
| 7 | **UI-2: Dashboards and Widgets** | The user builds pages, widgets, charts and KPIs and binds them to canonical data with guided click-tools for select, filter and group-by plus expressions, formulas and casting. Live preview before commit. Full specification: Part III.15. |
| 8 | **UI-3: Analysis Authoring** | The user composes statistics and correlation analyses from a method toolbox, selecting parameters and outcomes from canonical data. Drag-and-drop composition is the target interaction. |
| 9 | **Analysis jobs** | Each analysis binds to a data-analysis job with schedule and monitor. Every run passes the Readiness Gate of Part II.8, which is never weakened to make a run pass. |
| 10 | **Results dashboards** | Findings render with population, method, effect size, odds ratio where applicable, and FDR q-value. Deduplicated to latest run per job. Nulls are shown honestly: "not a significant driver" is a first-class result. |
| 11 | **AI and ML tier** | The same authoring surface composes deeper model-based analyses. License-gated. |
| 12 | **AI and ML jobs** | Scheduled and monitored like all jobs. |
| 13 | **AI and ML results dashboards** | Same honesty contract as step 10. |
| 14 | **THE SUPERVISOR** | See Part II.7.3. One premade weekly job that reviews the whole dataset and every Engine job and re-tunes configurations so all jobs improve. |
| 15 | **Assistant** | See Part II.7.7. Retrieval-grounded, citation-bearing, role-scoped, audit-logged, refusal-first. License-gated. |

**UI-4: Plant Data Log and Alerting.** The fourth low-code surface, part of the journey's operational value though it postdates the fifteen numbered steps. The user defines rules that log or alert when a parameter exceeds a limit, a material takes a wrong routing, a chemistry value falls outside an expected range, or a process value reaches a defined condition. An evaluation job scans new observations and events and writes plant-data-log rows at info, warning or error severity. Delivery grows from in-app log to email and webhook with an acknowledgement workflow.

---

## 6. The Five Low-Code Authoring Surfaces

*Reconciliation note: the source documents variously described four and five surfaces. Both were correct at different levels of detail. The resolution is five surfaces sharing one authoring shell; analysis authoring and machine-learning authoring are distinct surfaces that share the same canvas because they differ only in the tool palette presented.*

| Surface | Purpose | Left panel shows | Palette shows |
|---|---|---|---|
| **S1 Data Preparation** | Move staged data into the plant schema: ETL, filtering, linking, aliasing, grouping | **Two** schema groups: the staging shapes and the plant schema | Relational transform nodes |
| **S2 Dashboard and Widget** | Bind canonical data to widgets, charts, KPIs and filters; author derived measures | Plant schema only | Chart and widget catalogue plus expression tools |
| **S3 Analysis Authoring** | Compose statistics, correlation and mathematical analyses; for example relate one hundred parameters in one correlation block, save it, and chart the result | Plant schema only | Statistical method toolbox |
| **S4 Machine Learning Authoring** | Compose model-based analyses over the same canonical data | Plant schema only | Model and feature toolbox |
| **S5 Plant Data Log** | Define threshold, routing-deviation and chemistry rules that emit info, warning and error log entries | Plant schema only | Condition and action blocks |

### 6.1 The shared authoring shell

All five surfaces present the same shell, because a user who learns one has learned all five.

**A mode toggle at the top: visual wiring, or SQL.**

**In visual mode.** A schema browser on the left listing available tables and their columns, from which whole tables or individual columns are dragged onto a whiteboard. A node palette on the right. A canvas in the centre on which nodes are wired. Illegal wiring is rejected at drag time with a stated reason, never accepted and failed later. The result is saved as a named, versioned definition.

**In SQL mode.** The schema browser remains. The user writes SQL, tests it against a capped read-only sample, sees the returned rows and the inferred result schema, and receives a precise error with line, column and a plain-language hint when it fails. The result is saved as the same kind of versioned definition.

### 6.2 The three-layer separation law

This is the most important correction ever made to the low-code concept, and it is binding on all five surfaces.

Arithmetic and logical operators, relational operators, and control flow operate at three different granularities and **must not share one wiring surface**. Mixing them is the root cause of ambiguous, unpredictable low-code tools.

| Layer | Lives where | Operates on | A wire carries |
|---|---|---|---|
| **Dataflow** | The canvas | Whole datasets, that is relations | A dataset: rows by typed columns |
| **Expression** | Inside a single node's editor | Scalars, columns, literals | Nothing on the canvas; expressions are node configuration |
| **Orchestration** | A separate pipeline surface | Steps and jobs | Control tokens: success, branch, iterate |

Consequences, which are binding:

- The canvas is a directed acyclic graph of relational transforms. Cycles are illegal and are rejected at drag time.
- `AND`, `OR`, `NOT`, the arithmetic operators, the comparison operators and value-level `if/else` are **not canvas blocks**. They are the vocabulary of the Expression editor that opens inside a Filter node, a Derived Column node, or a Join condition editor.
- Loops, flow-control branching and scheduling belong to orchestration, not to a row-level transform graph. A transform graph is declarative: it describes what the output relation is, not an imperative sequence of steps.

Full specification of the resulting builder: Part III.14.

---

## 7. The Engine

### 7.1 Definition

The Engine is the set of governed jobs that read canonical data, compute, and write results into shared stores. It is not a single service. It is a governed substrate with two analytical layers and one supervisory job on top.

### 7.2 The two layers

**Layer 1, Data Analysis.** Statistical jobs over the multi-grain feature and outcome store: correlation with false-discovery-rate control, genealogy graph analysis, distribution and capability analysis.

**Layer 2, Machine Learning.** Deeper model-based jobs: anomaly detection, model-based attribution, prediction. License-gated to the higher tiers.

**Jobs feed each other.** Outputs land in shared stores, findings and knowledge base, which other jobs and the assistant consume.

### 7.3 The Supervisor, journey step 14

One premade job that runs weekly. It reviews the whole dataset and every Engine job and re-tunes coefficients, feature windows and job configurations so that all jobs improve. It is the Engine's brain: the other jobs are its arms and legs, answering in minutes or hours, while the Supervisor thinks slowly about the whole picture.

**Constitutional guardrails, binding and not negotiable:**

- It may adjust job configurations, feature windows and thresholds **within configured bounds**.
- It may **never** weaken a readiness gate, refusal logic, or an evidence requirement. This is an absolute prohibition and follows directly from Part I.4.2.
- **Every adjustment is a provenance row**: job, parameter, value before, value after, justification, evidence handle.
- A dry-run mode exists and is the default for any newly introduced adjustment class.
- A known-answer drift test, in which drift is injected and the Supervisor is required to correct it, gates its release.

### 7.4 Statistical rigour requirements

*Elevated to constitutional status in v2.0 because the implementation already exceeds what the concept demanded.*

Every correlation-class finding must carry, as stored data:

- the **method** actually selected, and why it was selectable for that outcome type;
- an **effect size** with its named type;
- a **p-value** and a **false-discovery-rate q-value** under Benjamini-Hochberg correction;
- **sample size** and **effective sample size**, which differ whenever observations are not independent;
- **bootstrap stability**: sign consistency, confidence bounds, and a stability verdict;
- **stratum survival**: whether the relationship survives stratification by each visible confounder, and the named reason if it does not;
- the **population and exclusions** that produced it.

The method set spans, at minimum: Pearson and Spearman rank correlation, chi-square and Cramer's V for categorical associations, analysis of variance and Mann-Whitney for group comparisons, mutual information, Lasso for multivariate selection, and variance-inflation-factor screening for multicollinearity.

Results are deduplicated to the latest run per job. A null result, meaning "this is not a significant driver", is a first-class finding and is displayed as one.

### 7.5 The Window Anchoring Law

*New in v2.0. Derived from a measured defect.*

An analysis window is anchored to the **maximum observed timestamp in the dataset under analysis**, never to wall-clock time.

A window anchored to the current time silently returns nothing on any historical dataset, and does so without any error, which makes it one of the most expensive possible defects: it looks like an absence of signal rather than an absence of data.

Where a wall-clock anchor is genuinely wanted, it is an explicit, named, user-visible option, never a default.

### 7.6 The Single Engine Implementation Law

*New in v2.0. Derived from a measured defect.*

**There is exactly one implementation of any given analytical capability in the product at any time.**

A superseded engine implementation is **deleted** when its replacement is adopted. It is not left registered behind a configuration flag, not left callable by a request parameter, and not left as an unreferenced class.

The reasoning is threefold, and each part was demonstrated in practice:

1. A second implementation that does not enforce the same governance is an honesty risk. If it can be reached at all, it can produce findings the Readiness Gate never sanctioned.
2. Stored results from a retired implementation persist in the database long after the code is dead, in its vocabulary, under its own engine identifier. Anyone reading the data later cannot distinguish them from current results without a code archaeology exercise.
3. Duplicate implementations of one capability directly violate Rule 1's requirement that there be one home for each decision.

**The two layers of Part II.7.2 are not an exception to this law.** Two layers means two different analytical purposes, statistical and model-based. It does not mean two implementations of the same correlation.

### 7.7 The Assistant, journey step 15

Retrieval-grounded over dataset, document and finding chunks. Citation-bearing, with every citation resolving to a real canonical row. Role-scoped, so a viewer and an engineer receive different retrieval scopes. Audit-logged. Refusal-first when evidence is absent.

Model binding is pluggable: an extractive baseline, a self-hosted local model for on-premise deployments, or a zero-retention private endpoint where the customer permits it, receiving only the question and the scoped evidence.

**The assistant reads the Engine. It writes nothing but its own audit log.**

### 7.8 Scale doctrine

A plant may define one hundred or more jobs. Execution is a bounded-parallelism job executor with per-class pools for import, analysis and machine-learning work, statement timeouts, and telemetry. Never unbounded. Never serialised into drift.

---

## 8. The Readiness Gate

*This chapter is new in v2.0. The gate was implemented before it was specified. It is the product's principal moat and it is now constitutional.*

### 8.1 Purpose

Before any analytical job computes, the dataset it would compute on is evaluated against named dimensions with published thresholds. If any dimension fails, the job abstains, records a run with a Blocked status, and names the failing dimension with its measured value.

### 8.2 The five dimensions

| Dimension | Measures | Ready at | Partial at | Below partial |
|---|---|---|---|---|
| **Independent heats** | Count of independent upstream units in the population, which is the true statistical sample size when observations share a parent | 60 or more | 30 to 59 | Blocked |
| **Outcome events** | Count of outcome observations in the window | 40 or more | 15 to 39 | Blocked |
| **Minority-class balance** | Share of the smallest class in a categorical or binary outcome | 10 percent or more | 3 to 10 percent | Blocked |
| **Freshness factor** | Data age measured in units of the expected cadence | 1.0 or below | 1.0 to 2.0 | Blocked |
| **Required-field completeness** | Share of outcome samples that join to at least one feature sample | 95 percent or more | 85 to 95 percent | Blocked |

Thresholds are **per-tenant configurable**. The values above are the defaults. Changing one is a governed configuration act with a recorded justification, subject to Part I.4.2.

### 8.3 Behaviour

The overall verdict is the worst state across all five dimensions.

Every dimension returns a human-readable reason containing the measured value, the threshold, and the verdict. Every gate result carries an evidence string naming the outcome, the grain and the window, so that the verdict is reconstructable from the database alone without the application.

A blocked run is persisted with a real run identifier and a message stating that the analysis abstained. It is never silently skipped.

### 8.4 The single-source rule

Both the compute engine and the live readiness endpoint call **the same evaluation function**. The verdict a user sees in the interface can therefore never drift from the verdict the engine acts on.

Any second implementation of readiness evaluation is a violation of Part II.7.6.

### 8.5 The gate is a sales asset

The readiness panel is shown to customers deliberately. Four green dimensions with real measured numbers and one honest red naming a specific data deficiency is a stronger demonstration than a fabricated result, because it tells the customer something true and actionable about their own data.

No competitor in this market shows a prospect a red status. That is the point.

---

## 9. The Outcome and Feature Registry

*This chapter is new in v2.0. The registry was implemented before it was specified.*

### 9.1 The registry

Analytical vocabulary is not hardcoded anywhere. It is declared in a registry that the product reads at runtime and exposes through an endpoint that every authoring surface consumes.

Each registered outcome declares: a stable key, a display name, a group, its **grain**, its statistical type (continuous, rate, count, duration, binary, ordinal, multinomial), its unit, its normalisation, its source binding, a taxonomy payload, a version, and a status.

The same principle governs feature definitions.

**No user-facing list of outcomes, grains, methods or measures may be hardcoded in the frontend.** Every such list is read from the registry. A hardcoded list is a Rule 1 violation.

### 9.2 The Namespace Authority Law

*New in v2.0. Derived from a measured defect.*

**The registry is the sole authority for outcome keys.** An engine may not write results under a key the registry does not declare.

When a key is renamed, the rename is a migration that moves the registry entry, the stored results, and every reference together, in one governed change. A rename applied to one side only produces a system in which the majority of stored results are addressed by keys the product cannot offer, and the failure is invisible: pages simply appear empty.

**Enforcement gate.** A repository or runtime check asserting that the set of distinct outcome keys present in the results store is a subset of the set declared active in the registry. Any orphan is a build or health failure.

### 9.3 The multi-grain canonical model and the Grain Assignment Law

*Elevated to constitutional status in v2.0. This mechanism is how Rule 1 is actually achieved and it was never written down.*

The canonical layer carries observations at multiple grains, and preserves for every row the **native grain** at which it was originally observed, alongside the canonical grain to which it has been attributed.

This is the mechanism by which one generic schema absorbs aluminium billets, tyre units, packaged lots, compound batches, raw material lots, customer rolls, slabs, heats and casts without a single industry-specific branch. The native grain is retained as evidence; the canonical grain carries the analysis.

**The Grain Assignment Law.** A row may be attributed to a canonical grain only if the attribution is genuine, meaning that features exist at that grain for that sample. Placing observations into a canonical grain for which no corresponding features exist produces a dataset that fails the completeness dimension of the Readiness Gate for a reason that has nothing to do with the customer's data quality.

Grain assignment happens in exactly one place: the refresh routine that populates the outcome store. It is not corrected by manual statements, because the refresh routine will overwrite them.

**Naming.** Canonical grain identifiers must be industry-neutral. A canonical grain named after one industry's product form, carrying another industry's units, is a Rule 1 violation even where it functions correctly.

### 9.4 The Typed Outcome Reading Law

*New in v2.0. Derived from a measured defect.*

An outcome's value is read from **the column that corresponds to its declared statistical type**.

A continuous or rate outcome reads its numeric column. A multinomial outcome reads its category column. An **ordinal outcome reads its ordinal column.** A binary outcome reads its boolean or numeric column per its declaration.

A loader that reads one column regardless of declared type will silently report a healthy outcome as unanalysable. The data is intact, the analysis is impossible, and no error is produced anywhere: the failure surfaces only as a readiness dimension reading zero.

**Vocabulary normalisation is part of import, not analysis.** Where a source emits mixed vocabularies or inconsistent casing for the same conceptual scale, the mapping surface of journey step 4 normalises them. Values that survive normalisation as isolated singletons must be reported to the user as a data-quality issue, because a single stray value in a categorical column can collapse a class-balance measure to zero.

---

## 10. Administration, Identity and Roles

### 10.1 Users and roles

The role catalogue contains at minimum Administrator, Engineer and Viewer, and is extensible.

Roles differ in both **view** scope and **edit** scope. An executive sees KPIs, value and trends. A process engineer sees investigation surfaces. An administrator sees configuration. A planner and a maintenance engineer each see their own concern. No role sees pages or holds edit rights outside its scope.

Access control is **deny by default**. Every endpoint and every action is explicitly granted. Retrieval scope for the assistant is bound to the same policy set.

Passwords are stored as Argon2id hashes and never in plaintext. Concurrent sessions across devices are supported and audited. Two users editing the same object receive an optimistic-concurrency conflict dialog, never a silent overwrite.

### 10.2 Connection administration

The administration surface configures DB-links: provider selection, credential entry with masking on read-back, connectivity test before save, per-table import selection, and per-object synchronisation cadence.

### 10.3 Job monitoring

Every job exposes: last run status including success, crash and timeout; last run timestamp; duration; and a manual re-run action. Every job is named and carries a cycle.

### 10.4 Logging

Four queryable layers, always available:

| Layer | Contents |
|---|---|
| **System log** | Login, logout, page creation, dashboard creation, widget relocation, DB-link creation |
| **Job log** | Job creation, execution start and end, duration, failure and its reason, timeouts, mapper field-level errors |
| **Data log** | The customer-authored rules of UI-4: threshold breaches, routing deviations, chemistry excursions |
| **Audit log** | Immutable, append-only, with database-level trigger enforcement |

Severity is info, warning or error throughout. Every page carries a collapsible log strip filterable by type, severity and free text; the administration area carries the full log page.

---

## 11. Licensing and Commercial Model

*Reconciliation note: source documents disagreed on tier names and figures. The four-tier model below is authoritative. Any deck, website or proposal carrying different figures must be corrected to match.*

### 11.1 The four tiers

| Tier | Level | Users / Sources / Jobs / Dashboards | Capability additions |
|---|---|---|---|
| **Light** | 1 | 3 / 1 / 1 / 3 | CSV and Excel connectors; dashboards and widgets |
| **Pro** | 2 | 10 / 3 / 5 / 8 | Adds SQL editor and PostgreSQL connector; statistics and correlation |
| **Pro Plus** | 3 | 25 / 8 / scheduled / extended | Adds KPI and widget authoring, scheduled correlations, machine learning, prediction and recommendation |
| **Enterprise** | 4 | Unlimited | All connectors, the assistant, branded reports |

### 11.2 Commercial terms

| Tier | Initial cost | Monthly subscription |
|---|---|---|
| Light | EUR 15,000 | EUR 2,500 |
| Pro | EUR 25,000 | EUR 3,500 |
| Pro Plus | EUR 40,000 | EUR 4,500 |
| Enterprise | EUR 50,000 | EUR 5,500 |

### 11.3 Enforcement

Entitlements derive from a **signed Ed25519 license token**, never from an editable database row. The token carries tenant, tier, issue and expiry timestamps, feature list and limits. An entitlement check that accepts a client-supplied tier override is a defect: overrides are ignored by design.

Seat limits, source limits, job limits and dashboard limits are enforced from the signed token at the endpoint layer.

### 11.4 The tier demonstration requirement

**Switching tier must visibly add and remove capability in the running application.** Moving from Enterprise down to Pro must cause features to disappear from the interface, not merely to return an error when invoked.

This is a demonstrable product requirement, not a marketing claim, and it is scored by persona A5.

---

## 12. The Value Engine

Every finding, prediction and suggestion converts to a quantified economic consequence.

**Requirements:**

- A **per-tenant cost table** supplies the economic inputs. No cost is hardcoded.
- The result is a **bounded range**, not a point estimate.
- **Every input is drill-throughable** to the data that produced it.
- The engine **abstains** rather than inventing a number when its inputs are insufficient. An abstention here is governed by Part I.4.1 exactly as an analytical abstention is.
- Downtime is modelled as **two distinct quantities**: equipment-stopped minutes and production-impact minutes. These are not interchangeable and the correct one is used per calculation.

**Enforcement.** A finding on the reference dataset reproduces a bounded range that is stable across runs, with every input traceable.

---

## 13. Platform Boundaries and Non-Negotiables

| Boundary | Statement |
|---|---|
| **Read-only toward the customer** | PPIQ never writes to customer systems and never participates in control. Any write-back path to a control system is an automatic critical failure regardless of every other strength. |
| **Honest statistics** | Multiple-testing correction always. Sample sizes always shown. Controls and nulls reported. No fabricated status in any surface. |
| **Provenance everywhere** | Every canonical row carries source system, source record identifier and import-batch lineage. Genealogy attribution weights sum to exactly 1.0 per child, enforced by database trigger. A synthetic-data flag separates emulation from production data. The projector accepts only registered connector source systems. |
| **Data boundary** | Self-hosted deployments leak nothing. A private model endpoint receives only the question and the scoped evidence, never the dataset. |
| **Accessibility and internationalisation** | WCAG AA. Right-to-left rendering for Arabic. Units and timezone per user. UTF-8 throughout with explicit date and number formats, never machine-locale dependent. |
| **Compliance** | Software development lifecycle controls evidenced. Audit trail and electronic signature where the regulated industries require them. |

### 13.1 Sizing doctrine

| Class | Volume | Topology |
|---|---|---|
| Small | approximately 750k observations per year | Single virtual machine |
| Medium | approximately 7.5M per year | Dedicated PostgreSQL, connection pooler, partitioned observation and feature tables, incremental feature refresh |
| Large | approximately 60M per year | Load-balanced application tier plus read replica |

Every sizing claim must trace to a measured run. Estimates presented as measurements are a persona A13 failure.

---

# PART III - BINDING DESIGN SPECIFICATIONS

The three specifications in this part are binding. They define surfaces in enough detail that a competent engineer who has never spoken to the author can build them correctly.

---

## 14. Specification A: Visual Data Preparation and Transformation Builder

A dual-mode, no-code and low-code surface for building ETL and data-preparation logic against linked source tables. The user either wires a visual transform graph or writes SQL, tests the result on a sample, and saves it as a named, versioned transformation definition.

The builder never mutates source data. It produces a saved definition linked to a job. Its output is a derived view or a governed materialised result, never a write-back.

The three-layer separation law of Part II.6.2 is a precondition for everything below.

### 14.1 The canonical artifact

A saved **Transformation Definition** contains:

| Field | Purpose |
|---|---|
| `id`, `name`, `description`, `author`, `created_utc`, `updated_utc`, `version`, `status` | Identity and lifecycle. Status is draft or published. |
| `authoring_mode` | `visual` or `sql` |
| `graph` | The node, port, wire, expression and layout model as JSON. Present in visual mode only. |
| `compiled_sql` | The SQL the graph compiles to, or the hand-written SQL in SQL mode, pinned to a target dialect. |
| `target_dialect` | postgres, mysql, sqlserver, or an internal logical plan that transpiles |
| `source_bindings` | References to source tables and columns **by stable internal identifier, never by display name**, so a source rename cannot silently break the definition. |
| `output_schema` | Inferred columns, types and nullability of the final output |
| `lineage` | Per-output-column provenance back to source columns and the operations applied |
| `validation_snapshot` | Status and warnings at save time |
| `definition_hash` | Hash over the normalised graph plus bindings, for change detection and reproducibility |

**Lifecycle rules.** A published version is immutable. Editing a published definition creates a new draft; publishing supersedes the previous version but never overwrites it. Names are descriptive only, with no phase, task or version codes embedded, because version is a separate field. A definition is linked to a job or owner object and does not float free.

### 14.2 Screen layout

```
+------------------------------------------------------------------+
|  TOP BAR: [Visual | SQL] toggle   name   Validate  Preview  Save  |
+-----------+--------------------------------------+---------------+
|  LEFT     |            CANVAS                    |   RIGHT       |
|  Schema   |   (node graph, pan/zoom, minimap)    |   Node        |
|  browser  |                                      |   palette     |
+-----------+--------------------------------------+---------------+
|  BOTTOM: Preview grid | Problems | Generated SQL | Lineage       |
+------------------------------------------------------------------+
```

**Top bar.** Mode toggle, definition name, Validate, Preview, Save and Publish, undo and redo, zoom to fit. In SQL mode the palette collapses and only the schema browser, the SQL editor and the result grid remain.

**Left, schema browser.** Linked source tables only, because the platform starts empty and only imported or linked tables appear. Per table: qualified name, source connector, row-count estimate, columns with type, nullability, primary key and foreign key markers, and sample values on hover. Search and filter. Multi-select columns and drag a whole table or selected columns onto the canvas to create a Source node. Foreign-key relationships are surfaced to suggest joins.

**Canvas.** The transform graph. Pan, zoom, snap to grid, minimap, multi-select, copy and paste, undo and redo, node comments, node search, keyboard shortcuts. Nodes are colour-coded by state: valid, warning, error. While a wire is being dragged, compatible input ports highlight and incompatible ones dim.

**Right, node palette.** The transform catalogue of 14.5, grouped and searchable.

**Bottom dock.** Four tabs: Preview showing the sample result for the selected node; Problems listing every validation error and warning, each linking to the offending node or port; Generated SQL as a read-only view of the compilation; Lineage showing provenance for a selected output column.

**Inspector.** Opens on node selection, carrying the node's configuration including its Expression editor.

### 14.3 The wiring correctness engine

This is what converts "if bad wiring, I should get an error" into a precise and predictable rule.

Every node exposes typed ports. A wire is legal only if the output port type is compatible with the input port type and the schema constraints hold.

**Port types.** Dataset, a relation with a known ordered schema. Column reference, a pointer to a column within a dataset. Scalar, a single typed value. Predicate, a boolean expression.

**Wire legality, checked live at drag time.** Dataset out to Dataset in is legal if the consuming node accepts the upstream schema; nodes that require specific columns or types have those constraints checked here. A Dataset may not be wired into a scalar input, and a Scalar may not be wired into a Dataset input: this is the type error that stops nonsense wiring. Multi-input nodes declare arity, so Filter has one dataset input, Join has two, Union has two or more, a Source has none, and a Sink has one input and no outputs.

**Structural rules, enforced continuously.** No cycles: a wire that would create one is rejected at drag time with a stated reason. Every node's required inputs must be connected before it is valid. At least one Sink is required before a definition can be published, though a draft may be saved incomplete. An orphan node that reaches no sink is a warning, not an error.

### 14.4 The dual-mode contract

Naive round-tripping between a visual graph and arbitrary SQL is lossy and a reliability trap. The contract is explicit and is stated to the user.

- **Visual to SQL is always available and deterministic.** The graph compiles to SQL for the target dialect and is viewable read-only at any time.
- **SQL mode is a first-class authoring mode**, not a transient view. The user writes SQL directly; it is validated by parse, schema inference and a sampled dry run, and produces the same artifact type.
- **Switching a visual definition to SQL mode forks it.** The generated SQL is handed to the editor, the graph detaches and becomes read-only history, and the user is warned that this direction is one-way.
- **SQL to visual reconstruction is best-effort and limited.** It is offered only for the subset the parser can faithfully map: simple select, filter, join, group and sort. For window functions, vendor-specific syntax, correlated subqueries or common table expressions, the tool keeps the definition in SQL mode rather than producing a wrong or partial graph. **The product does not pretend a full round trip exists.**

### 14.5 Node catalogue

**Sources.** Source Table, bound to a linked table, zero inputs. Reference to another saved definition, composing pipelines by consuming a previous transformation's output, with cycle protection across definitions.

**Row-level transforms**, one dataset in and one out, row count unchanged. Derived Column. Rename Column. Cast with explicit lossy-cast handling. Replace Nulls. String operations. Conditional value, a value-level CASE expression, which is where value-level `if/else` belongs.

**Filtering and sampling**, may reduce row count. Filter, keeping rows where a predicate is true. Distinct and Deduplicate with an optional key set. Limit, Top N, Sample.

**Aggregation.** Group By with one or more keys plus aggregate expressions: count, sum, average, minimum, maximum, count distinct, first and last with an explicit order. Selecting a non-aggregated, non-key column is a hard error surfaced at edit time.

**Relational and set**, two or more inputs. Join with inner, left, right and full variants, an explicit key mapping and a join predicate, warning on fan-out risk and on accidental cross join. Union and Union All, requiring column-compatible inputs and providing a mapping interface when names or order differ. Except and Intersect.

**Reshape.** Pivot and Unpivot. Window functions with partition, order and frame: running totals, rank, lag and lead. Window functions are available as explicit visual nodes and are one of the constructs that will not round-trip cleanly from SQL.

**Ordering.** Sort with one or more keys, direction and null ordering. Sort order is guaranteed only at a Sink or an explicit Limit boundary; intermediate ordering may not survive later operators, and the tool warns when it will not.

**Sinks.** Output, naming the final relation, defining the output schema, and acting as the target of Save. At least one is required to publish.

### 14.6 Expression editor and NULL semantics

Opened from inside Filter, Derived Column, Join condition and aggregate definitions. This is the home of the arithmetic and logical operators. Both a visual sub-builder and a text formula box are offered, backed by the same grammar and validated identically.

Operators: arithmetic, comparison, `AND`, `OR`, `NOT`, `IN`, `BETWEEN`, `LIKE`, `IS NULL`, and `CASE WHEN ... THEN ... ELSE ... END`, plus a curated function library covering string, numeric, date and time, and cast operations.

Static checks in the editor confirm that a referenced column exists in the input schema, that operand types are compatible, that functions receive the right argument types, and they display the inferred result type.

**Three-valued logic must be explicit**, because it is the single most common source of "the filter dropped rows I expected to keep":

| A | B | A AND B | A OR B | NOT A |
|---|---|---|---|---|
| T | T | T | T | F |
| T | F | F | T | F |
| T | NULL | NULL | T | F |
| F | NULL | F | NULL | T |
| NULL | NULL | NULL | NULL | NULL |

A Filter keeps a row only when the predicate is TRUE. NULL, meaning unknown, is not kept. The interface surfaces this with an explicit null-handling choice on comparisons and an `IS NULL` helper, so users do not silently lose rows.

### 14.7 Validation model and error taxonomy

Three levels, always distinguishable:

1. **Continuous static validation** while editing, with no execution: wiring legality, schema inference, expression checks. Results stream into the Problems panel and colour the nodes.
2. **Validate**, explicit and still without execution: full graph compile, confirming the definition is structurally and semantically sound end to end.
3. **Preview**, sampled execution, proving it runs against real data.

**Hard errors, blocking save and publish.** Structural: cycle, missing required input, no sink, disconnected required port. Type: dataset wired to a scalar port, incompatible union schemas, wrong operand types. Schema: unknown column, ambiguous column after a join, group-by selecting a bare non-key column, aggregate used outside an aggregation context.

**Warnings, allowed but flagged.** Cross join with no key. Fan-out join that multiplies rows. Lossy implicit cast. Float equality comparison. Orphan node. Sort followed by an order-destroying operator.

Every problem entry names the node and port, states the rule, and offers a reveal action.

### 14.8 Preview and test execution

Read-only always, physically prevented from writing by the enforcement of 14.11. Sample-based, with a row cap, a statement timeout, and never an unbounded result pulled to the client.

**Per-node preview** is mandatory: selecting any node shows the sample output at that node, not only at the end. This is the single largest debugging aid in the surface.

The inferred schema is shown alongside the sample so that type surprises appear before a full run. Preview reflects the compiled SQL, so what the user tests is what the definition will run. The interface states clearly that a sample can hide data-dependent issues, such as a cast that fails only on a row outside the sample.

### 14.9 Lineage and evidence-grade provenance

For an evidence-grade platform this is not optional and is a genuine differentiator.

Every output column records its provenance: which source columns it derives from and the ordered operations applied. Lineage is computed at compile time, stored on the definition, and browsable: clicking an output column shows the graph path back to source columns.

**Reproducibility.** The source snapshot used for a published run is pinned, so the same definition over the same snapshot yields the same output. The definition hash plus the snapshot identifier together identify a reproducible result.

### 14.10 Anticipated failure register

These are the conditions that break data-preparation tools in production. Each has a named owner during build.

**NULL and logic.** Three-valued logic dropping rows unexpectedly. Aggregates ignoring nulls versus counting them; count, count distinct and count-star must be explicit choices.

**Numeric.** Division by zero, with a defined result and a visible choice between error and null. Integer versus floating division and truncation. Overflow on sums over large columns, requiring a wide accumulator. Floating-point equality, warned with a tolerance suggestion.

**Types and casts.** Lossy or failing casts on dirty data, offering safe-cast to null versus strict-cast to error, with the choice visible. Implicit coercions that differ by dialect.

**Date, time, locale and encoding.** Timezone handling defined and consistent, never comparing a timestamp with and without timezone. Locale-sensitive date parsing, always with explicit formats and never machine locale. Text encoding rendered as UTF-8 with guards against code-page round-trip corruption.

**Relational.** Column name collisions after a join, requiring aliasing and treating ambiguity as a hard error. Identifier case-folding differences across dialects, resolved by consistent quoting in generated SQL. Join fan-out silently multiplying rows, warned with row-count deltas shown in preview. Accidental cross joins. Unions of schemas differing in count, order or type, requiring an explicit mapping.

**Ordering and determinism.** No guaranteed order without an explicit sort at the sink. Non-deterministic first and last in aggregation without an explicit order key.

**Data shape and scale.** Empty input defined as valid and producing empty output, not an error. All-null and single-value columns. Very wide tables virtualised in both browser and preview grid. Large previews hard-capped and paginated.

**Definition lifecycle.** Schema drift re-validated on load and before run, blocking with a clear diff rather than failing mid-run. Broken source bindings caught at validation, which stable-identifier binding already prevents for renames. Circular references across composed definitions detected at save. Expressions referencing a column not yet computed, rejected with a clear message.

**Authoring.** Autosave versus concurrent edit resolved by optimistic version checks, never last-writer-wins. Undo and redo across destructive operations restoring wires and configuration, not merely node position. Copy and paste never duplicating a stable identifier in a way that corrupts lineage.

**Security.** Identifiers quoted and literals parameterised in generated SQL, never string-concatenated. Single-read-statement enforcement by parse, not by string search. Timeouts and row or cost caps on every execution path.

### 14.11 Read-only enforcement at the execution layer

The interface is not permitted to be the guarantee. Enforcement is where queries run:

- Preview and run execute under a role or connection with read-only privileges on the source.
- Every statement is parsed and anything that is not a single SELECT is rejected.
- Preview is wrapped or rewritten to guarantee a limit, with timeout and maximum rows enforced.
- Outputs go only to the platform's own governed store, never back to the source.

### 14.12 Build order

Each phase ships as something that runs end to end, rather than half-building every node at once.

1. Schema browser, Source node, Output node, the wire type system, static validation, and the generated-SQL view. This proves the core model and the error engine.
2. Filter, Derived Column, Sort and Limit, plus the Expression editor with NULL semantics, plus per-node sampled preview.
3. Group By and Join with fan-out and collision handling, plus lineage capture.
4. Save, version and publish, plus read-only execution enforcement and governance.
5. SQL mode authoring, test and save, plus the one-way visual-to-SQL fork.
6. Reshape and window nodes, multi-dialect transpile, composed-definition references.

---

## 15. Specification B: Analytics Workspace and Widget Layer

Modelled on the Qlik Sense interaction model, adapted for a read-only, evidence-grade, process-to-quality platform.

The dashboard builder is itself a configuration surface: it saves a named, versioned dashboard definition consisting of sheets, widgets and bindings, consistent with the transformation-definition artifact of 14.1.

**CORE** marks the minimum for a credible product. **PPIQ** marks items that are domain-specific and matter more here than in generic business intelligence. **LATER** marks enhancements.

### 15.1 The associative selection engine

The chart list is the easy part. The associative model is the differentiator. It is built first, or the rest is only static charts.

- **CORE** Selection state per value: **selected**, **possible** meaning associated, **excluded**, and **alternative**, rendered with distinct visual semantics.
- **CORE** Click any data point to select; the selection filters every widget on the page.
- **CORE** A global selections bar showing all active selections as removable chips.
- **CORE** Clear-all, clear-one and clear-others actions.
- **CORE** Selection history with step back and step forward.
- **CORE** Possible versus excluded rendered visually distinct inside filter panes and list boxes, so the user sees what remains reachable.
- Lock and unlock a selection so it survives clear-all.
- Global smart search across dimensions: type a value, see where it exists.
- Alternate states: two independent selection sets for side-by-side comparison.
- Selection reflected in a shareable URL state.
- Debounced recompute so that rapid clicks do not fire one query per click.

### 15.2 Global filters and page controls

- **CORE** Global date and time range picker with presets and relative ranges.
- **CORE** Filter pane and list box, single and multi-select, with search-within, select-all, and associated-count display.
- **CORE** Dropdown and combo filter.
- Slider and range slider, numeric and date.
- Button set and toggle group.
- Variable input control driving an expression or threshold live.
- Hierarchical cascading filters, for example plant then line then unit.
- Filters that respect associative state, greying out excluded options.
- An applied-filters summary with one-click reset per widget and per page.

### 15.3 Chart catalogue

Selected by the question the chart answers, never by appearance.

**Comparison.** CORE bar, vertical and horizontal. CORE grouped, stacked and hundred-percent stacked bar. CORE combo chart with dual axis. Bullet chart against target and bands. Radar. Marimekko.

**Trend.** CORE line, single and multi-series. CORE area and stacked area. Spline and step line. Sparkline inline in tables and KPI tiles. Dual-axis time series for rate versus count.

**Part to whole.** CORE pie and donut. CORE treemap. Funnel. Waterfall. Sunburst.

**Distribution.** CORE histogram. Box plot per group. Density plot. Violin, LATER.

**Correlation and relationship.** CORE scatter with optional regression line. Bubble chart with a third measure as size. Heatmap matrix. **PPIQ correlation matrix**, measure to measure, the platform differentiator.

**Ranking.** Sorted bar with top-N. Pareto with cumulative line.

**Single value.** CORE KPI tile with value, delta versus prior, delta versus target, trend arrow and conditional colour. Radial gauge with bands. Linear bullet gauge. KPI grid.

**Tabular.** CORE straight table: sortable, searchable, conditionally formatted, with totals and inline mini-bars. CORE pivot table with row and column dimensions, expand and collapse, and subtotals.

**Flow and relationship.** **PPIQ genealogy and provenance graph**: node-link, weighted edges, walk up and down lineage. First-class here, not optional. Sankey. Network graph. Gantt and timeline.

**Geospatial.** Point and bubble map. Choropleth. Density layer.

**Process-quality specific, PPIQ core and uncommon in generic business intelligence.** SPC control charts, individuals, X-bar-R and moving range, with control limits and rule violations flagged. Run chart with median and run-rule detection. Process capability histogram with specification limits and capability indices. Pareto of defect causes. Scatter with specification and target reference regions. Batch and heat comparison as small multiples.

**Text and media.** CORE text and image widget with dynamic values inline. Narrative auto-generated insight text, PPIQ, combining correlation output and assistant summary.

### 15.4 Non-chart widgets and containers

CORE tabbed container. Stacked and accordion container. CORE button with navigate, apply selection, clear, set variable, export and open-URL actions. Title, subtitle and footnote elements. Divider, shape and spacer. Standalone shared legend. Bookmark launcher. Search box. Governed embedded content, LATER.

### 15.5 Dashboard-level constructs

- **CORE** Sheets and pages: multiple per application, ordered, descriptively named.
- **CORE** Master items: master dimensions, master measures and master visualisations. Defined once, reused everywhere, edited in one place.
- **CORE** Master colour mapping, so a category is the same colour in every chart.
- **CORE** Bookmarks saving a selection plus sheet state, shareable by link.
- Default landing bookmark per application.
- Storytelling: snapshots and narrative slides built from live charts.
- Chart annotations.
- Alerts and thresholds.
- Subscriptions and scheduled delivery, LATER.
- **PPIQ** Insight advisor equivalent: auto-suggested charts and correlations.
- **CORE** Export: chart image, sheet PDF, and data, with the current selection applied and disclosed.

### 15.6 Page layout system

CORE responsive column grid with snap-to-grid placement. CORE drag, resize and reorder. CORE z-order and no-overlap rules. Fixed versus fluid layout per sheet. CORE breakpoints for desktop, tablet and mobile with reflow rules. Per-widget minimum size and hide-below-breakpoint. Sheet regions: header, global filter bar, body, footer. Compact and comfortable density modes. Consistent gutters, margins and spacing tokens. Full-screen focus mode per widget. Print layout distinct from screen layout, LATER.

### 15.7 Style and design system

- **CORE** Design tokens for colour, typography scale, spacing, radius, elevation and border, from one source of truth.
- **CORE** Theme system with Dark Industrial as default and an optional light theme.
- **CORE** Categorical, sequential and diverging palettes, all colourblind-safe.
- **CORE** Chart styling standards: axis titles, gridlines, tick formatting, legend placement, data labels, tooltip content.
- **CORE** Number, date and unit formatting: explicit, locale-aware, never machine-locale dependent.
- **CORE** Reference lines, target lines and control-limit bands as a shared styling primitive reused by KPI, SPC and bullet widgets.
- Conditional formatting rules shared across widgets.
- Consistent empty-state, loading-skeleton and error-state visuals.
- Status and trend iconography.
- Chart title, subtitle, footnote and source-timestamp caption pattern.
- Custom theme as JSON, so a customer can be re-skinned without code.

### 15.8 Interaction standards

- **CORE** One interaction grammar: click selects, hover shows a tooltip, drag range-selects, identically across every chart.
- **CORE** Tooltip carrying dimension, measure and formatted value.
- **CORE** Loading skeletons per widget, not spinners.
- **CORE** Empty state distinct from filtered-to-empty state, the latter telling the user what to relax.
- **CORE** Per-widget error state that does not break the page.
- **CORE** Drill down within a hierarchy and drill through to a detail sheet, each with a visible affordance.
- Cross-filter feedback: clicking here visibly updates there.
- Undo and redo of selections reachable by keyboard.
- Keyboard navigation and defined focus order.
- Accessibility: ARIA roles, screen-reader labels, minimum contrast, focus rings, never colour alone as a signal.
- Responsive reflow verified at tablet and phone widths.
- Latency budget: fast first paint, progressive data fill, virtualised tables.
- Sensible defaults: sort, top-N cap, label overlap avoidance, axis starting at zero where honest.
- Confirm before destructive layout edits.
- Autosave with version and conflict handling.

### 15.9 Cross-cutting analytical functionality

- **CORE** Calculated dimensions and measures through a schema-aware, validated expression editor.
- **CORE** Set-analysis equivalent: measures scoped to a sub-selection independent of page filters, for example versus-all and versus-previous-period.
- **CORE** Period-over-period and versus-target as reusable measures.
- Variables driving thresholds, targets and what-if inputs.
- Conditional show and hide of widgets based on selection or variable.
- Drill-to-evidence action from any chart.
- Share by link with selection state; governed single-widget embed.
- On-demand and scheduled refresh, still read-only, refreshing the cache or view and never the source.

### 15.10 Evidence-grade requirements

These separate the workspace from a generic business-intelligence clone and are non-negotiable.

- **PPIQ** Every widget traces to its underlying query and definition through a lineage view answering "what data and transforms produced this number".
- **PPIQ** Drill-to-evidence from a KPI or a bar down to the source rows behind it.
- **PPIQ** Sample-data disclosure badge whenever a widget renders emulated or sample data.
- **PPIQ** As-of timestamp and source snapshot identifier on every sheet and widget.
- **PPIQ** Read-only enforced at the query layer. Dashboards can never write back.
- **PPIQ** No hardcoded or demo content in any widget. An unconfigured application shows empty states, never fabricated charts.

### 15.11 Performance and scale

CORE server-side aggregation; raw rows are never pulled to the client for a chart. CORE row caps and pagination on tables, with virtualisation for long lists. Query result caching keyed by selection plus definition hash. Progressive rendering for heavy sheets. Debounced and coalesced filter recompute. Guardrails on maximum data points per chart, degrading gracefully with a sample and a warning. Wide-table handling in table widgets.

### 15.12 Access, governance and multi-tenancy

CORE per-tenant isolation of applications, sheets and data. CORE row-level and section-access security. Role-based edit, view and publish permissions. Audit of who created, edited, published and viewed which dashboard and when. Deny-by-default access control on every dashboard action and endpoint.

### 15.13 Build order

1. Associative engine, global selections bar, one filter pane, cross-filtering.
2. Core charts: bar, line, KPI, straight table, plus master items, the Dark Industrial theme, and tooltip, empty, loading and error states.
3. Sheets, responsive grid, dashboard definition persistence, export.
4. Drill-down, conditional formatting, reference and target lines.
5. The PPIQ pack: genealogy graph, SPC and capability charts, drill-to-evidence, sample-data badge, lineage view.
6. Bookmarks, storytelling, alerts, correlation auto-suggest, maps.

Each layer ships working end to end before the chart list widens.

---

## 16. Specification C: Schema Topology and Persistence Law

*Ratified in v2.0. Previously a draft amendment.*

### 16.1 The ruling

The product persists in **exactly three application schemas** per database, plus the database platform's own schema restricted to platform infrastructure.

| Schema | Class | Day-one state | Contents |
|---|---|---|---|
| **`ppiq_meta`** | Application metadata | Tables present; identity and product furniture seeded | Users, roles, sessions, license artifacts, tenants, site identity, jobs including the premade Supervisor, dashboard and page and widget definitions, widget and chart type catalogue, localisation, connection profiles, source registrations, audit log, alert rules |
| **`ppiq_plant`** | Customer plant data | Tables present; **zero rows** | All canonical entities and everything derived from them: material units, aliases, process steps, observations, quality events, genealogy, defect catalogues, parameter definitions, the feature and outcome store, correlation results, findings, suggestions, value impacts, assistant chunks and index, knowledge base, risk scores, data-quality issues |
| **`ppiq_staging`** | Plant data in transit | Tables present; zero rows | Import batches, staged records with raw payloads, cursor watermarks, schema-drift events, edge-collector buffers |
| Platform schema | Platform only | - | Extensions and migration history. Nothing owned by the application. |

**The classification test, binding, resolving every future case:** *if a table exists because of this customer's data, it is plant class, or staging class while in transit; if it ships identical to every customer, it is metadata class.*

Engine outputs derive from customer data and are therefore **plant class**.

### 16.2 Staging medium

Staging is **in-database**: transactional batches with raw payload rows, giving cursor atomicity, provenance joins and row-level security. Flat-file dumps are permitted only as an optional archival export, never as the pipeline.

### 16.3 Eradication

1. Every emulated-source table and schema inside any application database is deleted. Emulated sources exist only as external containers per Part I.3.
2. Any legacy staging schema is dropped after (1).
3. Any legacy application database is dropped after its contents are verified against an off-machine archive.
4. Demo-named seed scripts are deleted.

### 16.4 Implementation mandate

1. **Explicit schema mapping.** Every persistence entity carries an explicit schema. No entity relies on a framework default.
2. **Ordered SQL discipline.** Every create statement in the script directory names its schema explicitly. Bare or platform-schema creations fail the lint gate.
3. **Physical move.** One audited migration moves existing tables into their ruled schemas, preserving data and updating every schema-qualified reference in functions, views, the rebuild command and connection profiles.
4. **The table inventory.** A generated, never hand-written, inventory lists every application table with its origin script, constitutional class, target schema, and a ruling: enter the domain model, remain as ordered-SQL infrastructure with a named justification, or be deleted.

### 16.5 Gates, each falsified once before being trusted

| Gate | Type | Assertion |
|---|---|---|
| **Schema placement contract** | Architecture test | Every entity type declares a schema from the ruled set; the test fails on any unmapped entity. |
| **SQL schema lint** | Repository gate | Any create of a relation or function without an explicit ruled-schema qualifier fails the build. |
| **Platform schema empty** | Runtime probe | The count of application tables in the platform schema is zero on a fresh install. |
| **Rule 2 one-liner** | Runtime probe | The plant schema returns zero rows on day one. |

### 16.6 Scope limits

The domain model remains the single source of entity truth. The two-database practice, one development and one demonstration database, is unaffected: this specification governs schemas within a database, while the demo-versus-product doctrine governs databases.

---

# PART IV - QUALITY, GOVERNANCE AND ACCEPTANCE

---

## 17. The Hardening Bar

The standing instruction: **no error, no bug, no crash and no non-functional control is acceptable in front of a customer.**

Specifically prohibited: a panel that reports it could not load and then works on retry; a styling mismatch between one page's controls and another's; a control that does not perform its function.

Every row below is a test, not an aspiration.

| # | Requirement |
|---|---|
| 1 | Every control on every current and future page matches the standard style **and** performs its function completely. |
| 2 | Every table shares one style. |
| 3 | Tabs and wording are consistent across all current and future pages. |
| 4 | Alignment, orientation and component placement are correct everywhere. |
| 5 | Widget drag, drop, move, minimise and maximise behave correctly. |
| 6 | Every page renders correctly at every screen size, on every major browser, over both protocols, and reflows properly. |
| 7 | No hang and no unexplained lag. Large datasets show a progress indicator with a percentage, not an indefinite spinner. |
| 8 | Every endpoint and its underlying joins handle key and null conditions without an unhandled failure. |
| 9 | Nothing breaks unexpectedly in front of a customer. |
| 10 | Every interactive element performs its function correctly. |
| 11 | Every action on every page, sub-page and navigation path is tested. |
| 12 | Machine learning, correlation and recommendation surfaces are ready. |
| 13 | Every widget and chart is dynamic and interactive and responds to filter and sort. |
| 14 | Heatmap renders and interacts correctly. |

### 17.1 The no-partial-credit rule

A gate item is between ninety-five and one hundred percent complete, or it is not done. There is no eighty-five percent on a gate item.

### 17.2 The demonstration path is sacred

Every control on the demonstration path works, or the path is not ready. A control that is present but non-functional is worse than an absent one, because it invites a click that fails in front of the buyer.

---

## 18. Definitions of Done

| Class | Bar |
|---|---|
| **Presentable** | Every journey step can be shown working through the interface. Screens and a working path suffice. Accuracy depth, concurrency and full role and license enforcement are not required. **Nothing shown is fabricated.** |
| **Hardened** | This entire document holds completely: all rules, all fifteen steps plus the fourth low-code surface, the Supervisor with its guardrails, engine scale, roles, licensing and logging, executable end to end by a person who did not write it, following the runbook. |
| **Customer-shaped** | Scope re-prioritised from customer feedback within forty-eight hours of the meeting. Multi-industry proof: a second emulated industry ingests through the identical journey with zero application changes. |

---

## 19. The Sixteen Acceptance Gates

Every persona criterion in Chapter A maps to one or more of these. A review closes by scoring the ledger and confirming it agrees with the persona scores.

| Gate | Domain | The one proof |
|---|---|---|
| **G1** | Source integration | Connectors pass behavioural tests; credentials never returned on read-back |
| **G2** | Mapping and genealogy | A unit resolves lineage in both directions; a bad mapping returns a typed error and rolls back |
| **G3** | Workflow through the interface | The full demonstration builds from empty through the interface only, with no hardcoding |
| **G4** | Intelligence | A golden dataset recovers planted signals and rejects spurious ones under FDR control; the assistant emits no uncited number |
| **G5** | Access and value | The authorisation matrix is green by identity and tier; cross-tenant access returns forbidden or empty; the value range reproduces |
| **G6** | Interface and experience | The action matrix is green by enumeration; visual-regression and cross-browser checks pass |
| **G7** | Demonstration readiness | One-click readiness passes; a recorded clean dry run exists |
| **G8** | Website | The honesty lint passes; the call to action captures a lead; the brand audit matches the token reference |
| **G9** | Operations | A clean machine reaches login by runbook alone; restore is verified |
| **G10** | Quality bar | Every hardening row green; no partial credit on any gate item |
| **G11** | Acquisition safety | The collector pushes one way; no inbound path to operational technology; load within budget |
| **G12** | Identity and security | Tokens not persisted in browser storage; administrator multi-factor enforced; development seed accounts absent from production |
| **G13** | Data boundary and model governance | Self-hosted deployment leaks nothing; a private endpoint receives only the question and scoped evidence |
| **G14** | Accessibility and internationalisation | WCAG AA passes; right-to-left renders; units and timezone per user |
| **G15** | Coverage and honesty | Every finding shows population and exclusions; a transition unit reports weighted attribution |
| **G16** | Compliance | Lifecycle controls evidenced; audit trail and electronic signature where required |

### 19.1 Gates added in v2.0

| Gate | Domain | The one proof |
|---|---|---|
| **G17** | Readiness integrity | Every readiness threshold in force matches the published default or carries a recorded justification for its deviation; no automated process has ever written a threshold change |
| **G18** | Registry authority | The set of outcome keys present in the results store is a subset of the set declared active in the registry; zero orphans |
| **G19** | Single implementation | Exactly one registered implementation exists per analytical capability; no superseded engine remains registered, callable or resident |

---

## 20. Scoring Doctrine

### 20.1 Per-criterion bands

Each criterion is scored out of one hundred.

| Band | Score | Meaning |
|---|---|---|
| **Critical** | Below 55 | Missing, broken or dishonest. A dead control on the demonstration path, a fabricated or uncited number, a forbidden commercial claim, a write-back path to control, or any live prior security finding lands here **regardless of other strengths**. |
| **Needs work** | 55 to 69 | Present but incomplete, fragile or inconsistent. Works on the happy path; fails an edge state, a second browser, an induced fault, or a sceptical click. |
| **Solid** | 70 to 84 | Complete, stable and honest for the stated scope; meets the gate's measurable exit. |
| **Strong** | 85 and above | Production-grade beyond demonstration: enumerated, automatically tested, documented, robust under adversarial use. |

Each scored criterion records four things: **Present**, what exists; **Why not lower**, the evidence that earns the floor; **Why not higher**, the specific named gap to the next band; and **Evidence**, a file and line reference or a reproducible command.

**A criterion with no reproducible evidence cannot exceed Needs work.**

### 20.2 Persona score and headline

A persona's score is the evidence-weighted mean of its criteria, with a hard cap: a Critical on any safety, honesty, dead-control or read-only criterion caps the whole persona at Needs work until fixed.

Persona scores are reported side by side and **never averaged into one number**. The headline is the **lowest** persona score, because the build ships only when every reviewer can sign, not when they average well.

### 20.3 The reviewer's standing rules

1. **No score without a live demonstration** through the interface on the reference dataset. A claim in a document, a comment or a conversation is worth zero.
2. **Honesty outranks capability.** An honest "collecting data" beats a confident wrong answer. Any forbidden commercial claim is Critical even on an otherwise strong surface. Any uncited number from the assistant is Critical.
3. **The demonstration path is sacred.** Every control on the path works, or the path is not ready.
4. **Read-only and operational-technology safety are absolute.** Any write-back path to a control system is an automatic Critical.
5. **Evidence is mandatory.** Every band above Needs work carries a file and line reference or a reproducible command. "Looks done" is not evidence.
6. **Induce the fault.** Where a criterion concerns failure handling, the reviewer **induces the condition** and observes the specified behaviour. A reviewer never reads a claim about failure handling.
7. **Score the lowest persona.**

---

## 21. The Surprise-Question Gate

A sceptical buyer asks difficult questions. The build is not ready until each is answered honestly, on the build. These are scored within personas A2, A3 and A5.

### 21.1 General

- Describe what the product is and why it should be bought.
- What is the workflow?
- Does the first-time configurator need strong database or programming experience? *Answer: for the common case, no. The no-code mapper and templates cover the majority; safe SQL exists for the long tail.*
- Can a page, widget, chart, job or model binding be added, removed or modified later, and can a widget be bound to a specific query result, from the interface rather than from source code? *Answer: from the interface.*
- How much data is needed before the intelligence layer gives a mature answer? *Answer: dashboards and KPIs work on day one. Advanced findings arrive as readiness dimensions turn ready. A historical backfill collapses the timeline.*

### 21.2 Licensing

- What license types exist and what exactly does each grant?
- How does the vendor control the license type? *Answer: a signed token, not an editable row.*
- One-time purchase, or renewed with an expiry?
- Does the license limit users or source connections? *Answer: yes, enforced from the signed seat and source caps.*

### 21.3 Users, roles and administration

- What user types exist and what are their privileges?
- How are they added and managed, and what is the user-count limit?
- Where are passwords stored and how? *Answer: Argon2id hashed, never plaintext.*
- Can one user hold two concurrent sessions on two devices?
- If two users open the same page and one modifies it, what does the other see? *Answer: an optimistic-concurrency conflict dialog, never a silent overwrite.*
- Does the install image ship a customer-usable administrator? *Answer: no. The automated install provisions only the permanent support account, which the vendor uses and the customer never sees. The customer's own administrator is created manually at commissioning.*

### 21.4 Intelligence

- Does the system work on artificial intelligence or on machine learning?
- Is all analysis intelligence, or simple mathematics? *Answer: deterministic engines compute; the assistant only explains.*
- Does the assistant do the analysis, or only the suggestions? *Answer: only explanation and suggestion. Engines do the mathematics.*
- Describe the Engine's workflow.
- Do you own the engine, or are you calling an external model, sending data out and receiving results back? *Answer: engines run in-tenant. The assistant model is self-hosted by default, or a zero-retention private endpoint that receives only the question and the scoped evidence.*
- An intelligence engine needs heavy resources; where does this run? *Answer: defined deployment topologies and sizing. The engines are deterministic computation, not a large language model performing arithmetic.*
- Large language models make mistakes. How can this assistant be depended on? *Answer: because the assistant neither computes nor ranks. Deterministic engines do, and every assistant claim carries a resolvable evidence handle or is not rendered at all.*

### 21.5 Added in v2.0

- Your engine refused to compute. Is it broken? *Answer: no. It evaluated five named readiness dimensions against published thresholds and one failed. Here is the dimension, the measured value and the threshold. It will compute when that dimension is satisfied, and not before. No competitor will show you this screen.*
- Can you lower that threshold so it runs? *Answer: a threshold is a governed configuration change with a recorded justification, made by a human being who owns the consequence. It is never lowered to produce a result, and the Supervisor is constitutionally forbidden from touching it.*
- Show me a completed analysis on my own data. *Answer, when true: here it is with its q-value, population, exclusions and stability. Answer, when not yet true: this dataset has not yet cleared the gate; here is the dimension that blocks it and what it would take.*

---

# CHAPTER A - ASPECTS OF REVIEW: THE NINE EVALUATION PERSONAS

PPIQ is judged from nine independent professional vantage points. **A build that satisfies one reviewer and fails another is not shippable.** The headline result is the lowest persona score, never an average that conceals a failing dimension.

Persona identifiers are frozen. Retired identifiers are never recycled, which is why the last three personas are numbered A11 to A13.

---

## A1 - The Developer and Maintainer

*"Can I extend this codebase a year from now without it fighting me, and is it honest about what it actually does?"*

| # | Criterion | Hard test | Fails when |
|---|---|---|---|
| 1 | Code hygiene | Static scan plus manual read of ten random files: no commented-out blocks, no shim tombstones, no duplicated logic that should have one home, consistent encoding and line endings | Any tombstone shim, any duplicated decision, any dead code |
| 2 | Stability under change | Backend, frontend and end-to-end suites run green twice consecutively; no flaky test; no environment-specific configuration required to build | Any flaky test; any works-on-my-machine configuration; a green deploy sitting on a red suite |
| 3 | Repository cleanliness | One canonical layout; no duplicate pipeline files, stray compose files, committed backup folders, or unregistered test projects | Any duplicate pipeline or compose file, any orphaned backup folder, any test project missing from the solution |
| 4 | Representative naming | Any unfamiliar class name predicts its content; one enumeration per concept, not three | A file whose name misrepresents its content; more than one tier enumeration |
| 5 | Tests execute, not enumerate | Pipeline invokes the suites as **blocking** stages that **run**: no discovery-only invocation, no error suppression forcing success; skips registered in a ledger with justification | Tests enumerated rather than run; failures swallowed; an unregistered skip; end-to-end wrapped to always pass |
| 6 | Generic across plant, database and domain | With no code change, link each of the six source classes; confirm no plant name, product, routing, schema or defect type is hardcoded anywhere | Any hardcoded plant, product, schema or route; a connection target compiled into the binary |
| 7 | Extensibility without fragility | Adding a connector, a KPI, a widget or a job is an additive change behind a stable seam, not a cross-tree edit | Any of the four requiring edits to many unrelated files |
| 8 | Structural consistency | One error-boundary primitive, one save-hook pattern, one entitlement resolver, one validation contract, reused everywhere | Multiple competing patterns for the same job; bespoke error handling per page |
| 9 | Pipeline correctness | The pipeline runs only the agreed sequence; application and infrastructure deployments are separated so an application deploy can never reap infrastructure; the red path is proved by breaking a test and confirming deployment never runs | Any extra pipeline step; shared project naming that lets orphan removal reap infrastructure; a deploy reachable while a suite is red |
| 10 | Switchable run and test profiles | Every authentication and license combination is exercisable in both environments without code edits or database surgery, applied by configuration, and refused in production unless an explicit accept-risk flag is set | Testing different states requires editing code or the database; a test-mode switch silently active in production; a customer-usable administrator shipped in the install image |
| 11 | No silent failure | Force one widget's endpoint to error: a contained, branded, retryable error appears in that widget while the rest of the page stays interactive; zero unhandled rejections across the demonstration path | Any could-not-load-then-works-on-retry; a single widget error blanking a page |
| 12 | Endpoint resilience | Every endpoint and its joins handle key and null conditions; long queries return a progress state, never a hang | Any endpoint throwing on a foreseeable condition; any query hanging the interface with no loading state |
| **13** | **Single implementation per capability** *(new in v2.0)* | Enumerate registered implementations of each analytical interface; confirm exactly one, and that no superseded implementation remains registered, callable by parameter, or resident as an unreferenced class | Any second implementation of one capability; a retired engine left behind a configuration flag |

**Why this persona cannot be gamed:** criteria 5, 9, 11 and 13 are each an automated, parsable test. The reviewer either produces the green run and the red-path proof, or the item fails.

---

## A2 - The Security, IT and Procurement Reviewer

*"Can my automation team and my database administrator approve this without a control-systems risk review or a data-egress concern?"*

The veto-holder in any real plant sale.

| # | Criterion | Hard test | Fails when |
|---|---|---|---|
| 1 | Read-only is absolute | Search every source and control path for any write, setpoint, command or schema statement against a source; outbound is only message, export or webhook | Any write-back path of any kind |
| 2 | Safe acquisition topology | Sources are reached through a customer-controlled collector that pushes one way; the core never initiates a connection into the operational network; a data-diode option is documented | The core connects into the operational network; any inbound path to control |
| 3 | Source-load protection | Per-source row caps, statement timeouts, rate limits and approved windows; backfill throttled, resumable and checkpointed | An unbounded query against a production source; a backfill that is one giant read |
| 4 | Token and session security | Access token in memory plus an HTTP-only refresh cookie with rotation and revocation, never in browser storage; Argon2id hashing; multi-factor enforced for administrators | A token in browser storage; a weak hash; no administrator multi-factor path |
| 5 | Secrets handling | Source credentials only in the collector's encrypted vault, masked on read-back, never in the browser or application configuration; per-environment signed keys | Any credential in browser or configuration; a hardcoded signing key; an unmasked secret on read-back |
| 6 | Tenant isolation | Multi-tenant uses tenant identity plus row-level security; dedicated uses physical isolation; one resolver and one rule set; a cross-tenant request returns forbidden or empty | Cross-tenant leakage; separate code forks per topology |
| 7 | Per-endpoint authorisation | Enumerate every endpoint, page, job and tool; each checks role and entitlement; no development-seed or diagnostic endpoint is reachable in a production build | Any unguarded administrative surface; any diagnostic endpoint exposed in production |
| 8 | Model data boundary | Engines compute in-tenant; the assistant model is self-hosted by default or a zero-retention private endpoint receiving only question and scoped evidence; a per-tenant no-egress toggle exists | Plant data sent externally for computation; no no-egress toggle; the assistant performing arithmetic |
| 9 | Audit and encryption at rest | Append-only immutable audit log on sensitive actions; encryption at rest | Mutable or absent audit log; plaintext at rest |
| 10 | Deployment hardening | Database port bound to loopback, bootstrap administrator replaced, health and readiness endpoints present, runbooks exist | A publicly exposed database port; an active bootstrap administrator; no runbook |

**Why this persona cannot be gamed:** every criterion is binary and individually testable.

---

## A3 - The Process and Quality Engineer

*"Will this help me investigate a quality problem faster than my spreadsheets, and can I trust what it tells me?"*

**This is the headline persona.** It is the sceptical daily user the no-partial-credit rule is written to survive.

| # | Criterion | Hard test | Fails when |
|---|---|---|---|
| 1 | Zero dead controls on every path | Enumerate and click every control on every page and sub-page; each performs its function | Any control that does nothing, errors, or no-ops |
| 2 | End-to-end workflow without crash | Run the full lifecycle live: link, stage, map, build page, configure widgets, run job, read dashboard, export | Any crash, stall or broken data transfer in the chain |
| 3 | Uniform styling and clarity | Consistent component styling across all surfaces, current and future, standardised away rather than patched once | Any styling mismatch; inconsistent tables or tabs |
| 4 | Widget customisation without a developer | Build a chart by drag and drop, bind it to data with no endpoint written, apply a transform from the script layer | Any chart requiring a new endpoint or a source edit |
| 5 | Genealogy golden thread | Click from a defect on a finished unit back to the upstream chemistry, on the **customer's own** key names, in **both** directions, across every source | The thread breaks; requires internal keys; one direction missing |
| 6 | Population always stated | Every analysis shows its population and exclusions; a collecting-data state appears instead of a fabricated answer | Any analysis stating a driver without stating its population |
| 7 | Correlation and learning jobs, honestly | Run the standing jobs on demand and scheduled; confirm recomputation and most-influential parameters using the full method set under FDR control and stratification, framed as suspected contributors | A result presented as a fixture; a causal claim; no multiple-testing control |
| 8 | Blended provenance correctness | On a transition unit spanning two upstream parents, the system reports weighted shared attribution, never a single fabricated parent | A transition unit attributed to one parent; no weighting |
| 9 | Performance under real data | At reference scale, no hang or lag; large datasets show progress; tables virtualise | Any hang; a large table rendering every row |
| 10 | Interactivity | Every widget responds to filtering and sorting; the heatmap renders and interacts; drag, move, minimise and maximise behave | A static chart; a filter that does nothing; a broken heatmap |
| 11 | Correct and effective results | Spot-check outputs against the known reference dataset; the deliberate planted imperfections produce the expected findings | Results that are wrong, meaningless, or contradicted by the known data |
| **12** | **Refusal is explainable from the data alone** *(new in v2.0)* | For any blocked analysis, reconstruct the verdict from the database without the application: the dimension, the measured value, the threshold | A blocked run with no persisted reason; a reason not reproducible from stored data |

---

## A4 - The Reliability, Operations and Plant Administrator

*"Does it keep running, tell me when something breaks, and let me configure the plant without calling the vendor?"*

| # | Criterion | Hard test | Fails when |
|---|---|---|---|
| 1 | Source onboarding from the interface | Create a link, pick a provider, test connectivity before save, confirm masked credentials, select tables, set cadence with off-hour windows | Any of these requiring code or database edits; no test before save |
| 2 | Mapping and schema configuration | Author a view or join into the generic schema; reconcile mismatched keys explicitly; define a KPI as a versioned view; confirm the three authoring tiers so the common case needs no SQL | Mapping possible only via code; keys silently merged; a KPI hardcoded rather than a view |
| 3 | Import jobs and delta logic | Each import is a named job with cycle and status; the next scan compares watermarks and imports only new rows; each run writes a batch record with rows, duration, watermark and errors | A re-sync re-importing everything; no batch record; no cursor |
| 4 | Jobs monitor | Every job shows last-run time, outcome including crash and timeout, duration and source impact | Any job unmonitored; missing outcome or duration |
| 5 | Schema-drift detection | Add, rename or remove a source column: a typed schema-change event is raised, dependent mappings are flagged, dependent imports pause rather than producing wrong facts; a mapping-health panel shows state and reason | A schema change silently corrupting a mapping; no health panel |
| 6 | Fail loudly and specifically | Introduce a bad join: a precise typed error names the affected view and the next safe step | A generic or silent failure; a wrong number instead of a typed error |
| 7 | Readiness meter | While a gate is blocked, the product shows simple analysis plus an honest countdown, and a backfill collapses the timeline | A blank screen while collecting; a fabricated advanced result before readiness |
| 8 | Backfill and source protection | Historical load is throttled, idempotent, watermark-tracked, pausable, resumable and visible, honouring the source-impact budget | An unthrottled or non-resumable backfill |
| 9 | Operational resilience | A clean machine reaches a working login by runbook only; backup and restore drilled; health endpoints exist; a failed deploy rolls back | No runbook path to login; no restore drill; no rollback |
| 10 | Concurrency and collaboration | Two users edit the same page; the second receives an optimistic-concurrency conflict dialog; published definitions remain immutable | A silent overwrite; no conflict dialog; mutable published definitions |

**Why this persona cannot be gamed:** criteria 3, 5, 6 and 10 each require inducing a condition and observing the specified behaviour, never reading a claim.

---

## A5 - The Executive Sponsor

*"Will this pay back more than it costs, can I trust its recommendations, and does each role see the right scope?"*

| # | Criterion | Hard test | Fails when |
|---|---|---|---|
| 1 | Quantified value | The value engine converts a finding into a bounded range with an abstain path, computed on reference data, every input drill-throughable | Value asserted rather than computed; no abstain path; an input that cannot be drilled |
| 2 | Role-scoped view and edit | Log in as each role and confirm scope differs by both view and edit | Any role seeing pages or holding rights outside its scope |
| 3 | License tiers demonstrable live | Toggle tier during a demonstration and confirm features visibly appear and disappear; entitlements come from a signed token, not an editable row | A tier toggle that changes nothing visible; entitlements editable in the database |
| 4 | Trustworthy output | Deterministic engines compute and rank; the assistant only explains with citations and cannot render an uncited number; audit a claim to its evidence handle | The model performing arithmetic or ranking; any uncited number |
| 5 | Honest boundary as an asset | The product says suspected contributor, states what it does not know, never claims guaranteed root cause, stratifies by confounders and names an unmeasured likely one | Any guaranteed-root-cause claim; a finding with no confounder discussion |
| 6 | Speed of insight | Dashboards and KPIs work on day one; advanced findings arrive as readiness turns ready; no spinner without progress | A wait with no progress; nothing usable on day one |
| 7 | Price-to-value parity | Demonstrated value is credibly greater than the tier price | Demonstrated value below the price, or not demonstrated |
| 8 | Cross-device and cross-browser | Correct rendering and reflow at multiple sizes, on multiple browsers, over both protocols | Any broken layout on a common browser or size |
| 9 | Competitive distinctiveness | Clearly not another business-intelligence dashboard: genealogy, defect drivers, value and suggestions that such tools lack | Indistinguishable from a dashboard tool |
| 10 | Trust posture and brand | Reads as plant operations plus data science: calm, industrial, evidence-based | Consumer-technology tone; over-claiming visuals |

---

## A6 - The Brand and Website Reviewer

*"Before anyone logs in, does the website make a serious buyer request a demonstration, and does the brand hold across every surface?"*

The website is the first and best salesperson. The brand must be identical across website, application and reports.

| # | Criterion | Hard test | Fails when |
|---|---|---|---|
| 1 | Name, tagline and voice | Exact and consistent use of product name, short form, primary and secondary taglines and the customer promise; industrial evidence-based voice everywhere | Tagline drift between surfaces; off-voice copy |
| 2 | Forbidden versus approved claims | Nothing says guaranteed root cause, AI-powered prediction, production-ready AI, or that the product replaces MES, L2, SCADA or BI. It does say rule-based risk scoring, correlation analysis, suspected contributor, statistical pattern, evidence-based investigation, read-only intelligence layer | Any single forbidden claim anywhere on the site or in the application |
| 3 | Colour palette fidelity | The Dark Industrial Command Center palette exactly, by hexadecimal value, for background, panel, raised surface, primary, accent, success, warning, critical, and the three text weights | Any off-palette colour; status colours misused |
| 4 | Typography | The defined display, body and monospace families; minimum body sizes for web and print; the defined heading scale | A playful font; body below minimum; inconsistent scale |
| 5 | Logo system | A connected-node mark, industry-neutral so that it works for aluminium, pharmaceutical, paper and automotive rather than one industry; full, icon and stacked variants in colour, dark, light and mono; none rotated, recoloured, stretched, shadowed or boxed | A single-industry mark; a missing variant; any prohibited treatment |
| 6 | Website experience | Professional, correct at every size and browser, clear navigation, fast | Broken responsiveness; slow load; unclear navigation |
| 7 | Product ecosystem | Each product in the portfolio carries description, benefit, interactive graphics, licensing and full detail with a call to action | Any product missing or thin |
| 8 | In-application brand | The application embodies the identity: full-bleed dark background with no white-space leakage, defined navigation and sidebar treatments, card and button treatments, chart colour order, status badges, tier badge colours | White-space leakage on dark surfaces; off-specification chrome |
| 9 | Report brand | Deliverables switch to the light report surface with brand header and footer maintained | A dark-theme report; missing brand furniture |
| 10 | Call to action and lead capture | A working call to action that captures an inquiry | A call to action that goes nowhere |

---

## A11 - The User Experience Auditor

Evaluates the product as a non-programming plant user experiences it.

**Scope.** User satisfaction and journey clarity. Intuitive navigation across the fifteen steps. Correct generic dark-industrial visual language: one token set, one shared palette, zero page-local colour schemes. The strict implementation of the five low-code surfaces: drag and drop, live displacement, edge resize, minimum and maximum framing, interactive wiring where specified, wizard completeness, and whether each low-code act is genuinely achievable without writing code.

**Evidence standard.** Browser-verified walks with screenshots. Every low-code claim demonstrated by a naive-user click path, or graded honestly as a form rather than a canvas.

---

## A12 - The Intelligence and Engine Auditor

Evaluates the Engine as a statistical system, not as a set of endpoints.

**Scope.** Method coverage against the registry. Correlation correctness: FDR control, effect sizes, honest nulls, deduplication to latest run. How the engine handles grain, and how it attributes parent-level parameters to child-level outcomes through genealogy. **Window anchoring: dataset maximum versus wall clock.** **Readiness gate integrity: never weakened, reasons persisted and explainable, exactly one implementation.** Assistant integration: retrieval grounding, citation resolution, refusal-first, read-only toward the Engine. Concurrency and scale: bounded-parallelism execution, one hundred or more concurrent jobs, statement timeouts, telemetry.

**Evidence standard.** Known-answer tests against planted relationships with a fixed seed. **A blocked run must name its blocking dimension from the database alone.**

**Additional scope, new in v2.0.** Registry authority: no result may exist under a key the registry does not declare. Typed outcome reading: each outcome type read from its correct column. Engine uniqueness: exactly one implementation per capability, with no superseded engine registered or callable.

---

## A13 - The Infrastructure Engineer

Evaluates whether the platform physically carries its promises.

**Scope.** Hosting and deployment topology against the sizing doctrine. Server sizing against declared volumes. Database scaling: partitioned observation and feature tables, incremental feature refresh, index discipline, row-level-security cost. Compute headroom for the job classes under one hundred or more defined jobs. Backup, restore and rebuild reproducibility. Container and source-emulation separation, meaning no source data inside the product database. Secrets, certificates and network exposure.

**Evidence standard.** Measured numbers from pilot-class telemetry or load rigs. **Never estimates presented as measurements.** Every sizing claim traceable to a run.

---

# CHAPTER B - IDENTITY AND TOPOLOGY REFERENCE

The single authoritative statement of how PPIQ identifies itself across environments: ports, databases, authentication, users, sources, licences and containers, plus the exact sequences to provision and run it.

**Everything here is environment-configurable. Nothing in the product hardcodes an environment.**

> **Secrets policy for this chapter.** Local development credentials are recorded because they are local-only by construction and are worthless outside the laptop. **No production or server secret appears in this document.** Server secrets live in the preserved environment file on the host and in a password manager, never in version control and never in a specification.

---

## B.1 The two environments

Both matter every day. Neither replaces the other. Wherever a port, connection string, credential, container name or command differs, both are documented and each is labelled.

| | **Local (laptop)** | **Server (release and demonstration)** |
|---|---|---|
| **When used** | Daily development, testing, debugging | Releasing a version; the live demonstration |
| **How reached** | Loopback plus local ports | Public host subdomains over HTTPS behind a reverse proxy |
| **Main database** | Native PostgreSQL, not a container; only emulated sources are containers | All databases are containers |
| **Application host** | Native process on the local application port | Reverse proxy to the application container |
| **Identity** | Configuration-seeded development users, one per role | Permanent support account auto-provisioned, plus a manually created customer administrator |
| **Login source of truth** | Configuration-seeded users | Database-backed user table with modern password hashing |
| **Deploy mechanism** | Local script verbs | Push to the default branch, webhook, pipeline |
| **Secrets** | Committed local-only development file | Preserved host file, excluded from version control, password-stable across deploys |

---

## B.2 Ports and routes

### Local ports

| Service | Port |
|---|---|
| Application API | 5063 |
| Web interface, development server | 5173 |
| Web interface, preview build | 4173 |
| Marketing website | 5080 |
| PostgreSQL, application database | 5432 |

### Server routes

Four public subdomains, all fronted by the infrastructure reverse proxy over HTTPS: the application, the API, the marketing website, and the continuous-integration interface.

**Documented health-check behaviour.** The externally reachable health path returns unauthorised because it sits behind the authenticated edge, while authentication itself works. The deployment health gate therefore uses the **internal** container health path, which returns success. This is expected behaviour and must not be mistaken for a fault.

### Key API contracts, identical in both environments

- `POST /auth/login` with a body carrying user name, password and an optional requested role. The response token field is `accessToken`.
- The licensing group exposes activation, offline verification, current entitlement and entitlement check. The activation request field carries the compact signed license. An entitlement check that supplies a tier override has that override **ignored by design**, because entitlement is tamper-proof.
- The analytics group exposes readiness, readiness gates, results and runs.
- The machine-learning foundation group exposes readiness counts, the outcome registry, and correlation compute.

---

## B.3 Application database

One PostgreSQL instance per environment. Schema topology is governed by Part III.16.

**Two databases are maintained deliberately** and this practice is permanent: a development database for daily work, and a demonstration database carrying the emulated dataset. They are not merged. The profile selected at launch determines which one the application reads.

**The profile is not optional and is not a detail.** Launching against the wrong database produces a system that appears to work while returning nothing, because tenant scoping and dataset population differ between them. The launch command always names its profile explicitly.

**Migration order, both environments.** Entity-framework schema first, then the ordered SQL decoration layer, then the application. Reversing this order fails.

**The password coupling law, server.** The preserved environment file is reused across deploys to keep the database password stable. Deleting it generates a new password that will not match the existing data volume, producing an authentication failure that looks like a code fault. If it must be regenerated, the data volume is wiped in the same operation.

---

## B.4 Authentication and identity

Login is database-backed on the server and configuration-seeded locally. The signing key is per-environment and is enforced by a startup guard: the application refuses to start with a missing or default key.

**The two-admin model, server.** A permanent support account is auto-provisioned at first run. It is the vendor's account, undeletable, and the customer never uses it. The customer's own administrator is created manually at commissioning, with the bootstrap flag cleared. **No customer-usable administrator is baked into the install image**, which is the enforcement of Rule 2's identity clause.

**Development-seed and test accounts are removed from the production path.** Their presence in a production build is a persona A2 critical failure.

---

## B.5 Emulated source fleet

The reference fleet spans six database sources plus two file sources, covering PostgreSQL, Oracle, SQL Server and MySQL, together with CSV and Excel. This exercises every connector class without a code change and is the practical proof of Rule 1.

**Connector tier gating.** File sources at Light. PostgreSQL at Pro. Oracle, SQL Server, MySQL, REST and OPC-UA at Enterprise.

The fleet is a local development asset. On the server it is disabled by a mode flag, because the server exists to demonstrate the product, not the emulation.

---

## B.6 Licensing implementation

Four tiers per Part II.11, enforced by Ed25519-signed compact tokens.

Token fixtures exist per tier for development. The development key is registered into the public-key table **only when the presentation flag is on**, which keeps it out of any real customer production install.

The licensing tables are row-level-security forced, scoped by tenant.

> **Mandatory before any customer installation.** Generate a production signing keypair. Sign per-customer, per-tier tokens. Register the real public key through the canonical operations flow. Never ship or register the development key. Additionally, a customer frontend must never have demonstration credentials compiled into its bundle: build-time variables are inlined and are readable in the shipped assets.

---

## B.7 Container topology

**Two compose projects, permanently separated. They are never merged.**

| Project | Contents | Rule |
|---|---|---|
| **Infrastructure** | Continuous integration, reverse proxy, backup runner | Sacred. Never reaped by an application deploy. |
| **Application** | Database, API, web | Deployed by the pipeline, in place, with health gate and rollback |

**Why the separation exists.** When the application deploy shared a project name with the infrastructure, orphan removal during deployment reaped the continuous-integration and reverse-proxy containers mid-deploy. Renaming the application project makes orphan removal structurally unable to touch infrastructure.

The application containers additionally join an external edge network so the infrastructure proxy can reach them by name.

---

## B.8 Frontend

React, TypeScript and Vite. Dark Industrial palette and the defined typography per persona A6 criteria 3 and 4.

**Build-time variable law.** Frontend environment variables are inlined at build time, not read at runtime. Three things must therefore align: the container build file declares them before the build step; the compose file passes them as build arguments; and the environment file sets them. After any change, the image must be rebuilt and the browser hard-refreshed, because both the layer cache and the browser cache will otherwise serve stale values.

**Host-derived URL law.** All browser-facing URLs and all permitted cross-origin values derive from a **single** public-host variable. Setting one variable to a customer domain makes every URL and origin follow. Hardcoded template hostnames are a defect: they do not resolve at the customer and they break both the interface and cross-origin policy.

---

## B.9 Environment profiles and customer modes

| Variable | Values | Purpose |
|---|---|---|
| Main database mode | native, docker, external, managed | How the application database is provided |
| Emulated sources mode | docker, external, disabled, mixed | Whether the emulated fleet runs |
| Presentation flag | on, off | Registers the development licence key and runs the presentation smoke test. **Off for every real customer.** |
| Public host | hostname | Drives all URLs and cross-origin values |
| Allowed origins | list | Derived from the public host |

A real customer administrator carries the administrator role with the bootstrap flag cleared.

**Profile hygiene.** Any profile that leaks a legacy or shared credential is deleted rather than corrected, because a corrected file with history still leaks. Duplicate connection-string declarations within one profile are removed.

---

## B.10 The naming golden rule

**Permanent, both environments.**

No artifact name contains a phase code, a task code, a version code or a bookkeeping label. Names are descriptive of content only. Version is a separate field, never part of a name.

Numeric ordering prefixes on ordered SQL scripts are **functional tokens** and are preserved; the phase and task labels embedded alongside them are stripped.

A script whose only purpose was task-closure bookkeeping rather than schema definition is deleted, not renamed.

This rule applies to files, database objects, routes, classes, test names and backlog artifacts.

---

## B.11 Deployment pipeline

Push to the default branch triggers a webhook, which runs the pipeline. The pipeline checks out, regenerates the environment file, migrates and seeds the application database, registers the demonstration licence key when the presentation flag is on, builds, deploys the application project **in place**, health-gates with rollback to the previous image, and runs a presentation smoke test.

**Stage ordering law.** The test stages are blocking and are textually ordered ahead of every migrate, seed, build and deploy stage. A deployment stage that can be reached while a test stage is red is a persona A1 criterion 9 failure regardless of any other property.

**The pipeline runs the agreed sequence and nothing else.** Extra steps are a failure condition, not an enhancement.

---

## B.12 Known technical debt

Recorded here so it is inherited rather than rediscovered. Each item belongs to a backlog entry.

| Item | Nature |
|---|---|
| Reverse-proxy configuration references stale container targets and its host-bound source file was deleted, making in-place edits fail and hot reloads non-persistent | Fragile but currently functioning. Do not recreate the proxy container until a persistent, corrected configuration is established. |
| Hardcoded host address in deployment scripts and documentation | Configuration hygiene. Parameterise to the public-host variable. |
| Bootstrap administrator enabled in local and demonstration profiles | Correct for those profiles. **Must be disabled in any customer deploy.** |
| Development signing key used for demonstration entitlement | Acceptable only on the vendor's own demonstration host. See B.6. |
| Vector extension unavailable in the running database instance | Limits assistant retrieval to the extractive baseline until provisioned. |
| Legacy database and emulated-source residue inside application databases | Governed by the eradication clause of Part III.16.3. |

---

# APPENDIX - MERGE LEDGER

Recorded so that a future reader can verify nothing was lost.

## Sources absorbed

| Source | Content | Disposition in v2.0 |
|---|---|---|
| `rules.txt` Rules 1-3 | Product definition, generic mandate, launch state | Rewritten to specification grade as Part I.1, I.2 |
| `rules.txt` Rule 4 | First-day user journey, twelve informal steps | Superseded by the fifteen-step canonical journey, Part II.5, which is the more advanced formulation |
| `rules.txt` Rule 4 closing paragraph | The two engine layers and the weekly supervisor | Preserved and expanded as Part II.7.2 and II.7.3, with constitutional guardrails added |
| `rules.txt` Rule 5 | Administration, logging, four licence tiers with figures | Preserved as Part II.10 and II.11. Tier figures are authoritative. |
| `rules.txt` Rule 6 | Four low-code surfaces | Reconciled with the later five-surface passage; both were correct at different granularities. Part II.6. |
| `rules.txt` Rule 7 and the Qlik style notes | Interface standards | Absorbed into Part III.15 |
| `rules.txt` Aspects of Review v4, Parts A, B, C | Six personas, capability backbone, scoring doctrine, surprise questions | Personas to Chapter A; capability backbone distributed into Parts II and IV; scoring to Part IV.20; surprise questions to Part IV.21 |
| `rules.txt` lines 693-1213 | Visual Data Preparation and Transformation Builder specification | Preserved substantially intact as Part III.14 |
| `rules.txt` lines 1236-1535 | Analytics dashboard and widget checklist | Preserved substantially intact as Part III.15 |
| `concept.md` v1.0 | Sharpened three rules, fifteen-step journey, emulation doctrine, engine, boundaries, definitions of done | Absorbed throughout; its formulations generally superseded the `rules.txt` equivalents |
| `concept_Amendment_6` | Schema topology and persistence law | **Ratified** as Part III.16 |
| `PPIQ_Identity_and_Topology_v4.md` | Environment reference | Chapter B, with server secrets removed and transient operational detail condensed |
| `Aspects_of_Review_Personas_A11-A13.md` | Three additional personas | Chapter A, personas A11 to A13 |

## Deleted, and why

| Deleted | Reason |
|---|---|
| The informal twelve-step journey of `rules.txt` Rule 4 | Superseded in full by the fifteen-step journey. Retaining both would create two acceptance specifications. |
| The three-tier pricing formulation found in derived documents | Contradicted `rules.txt` Rule 5. The four-tier model is authoritative. |
| Verbatim server credentials, host addresses and container-level operational transients from the topology reference | A specification is not a secret store. These live in the operations runbook and the password manager. |
| Duplicated statements of the generic mandate appearing in three source documents | Consolidated into Rule 1. |
| Task-closure and phase-token bookkeeping references | Governed by the naming rule of Chapter B.10. |
| The claim that flow-control blocks belong on the transform canvas | Explicitly corrected by the three-layer separation law of Part II.6.2. |

## Added, with origin

| Added | Origin |
|---|---|
| Part I.4, the Honesty Contract | Implemented and never documented |
| Part II.8, the Readiness Gate with five dimensions and thresholds | Implemented and never documented |
| Part II.9, the Outcome and Feature Registry | Implemented and never documented |
| Part II.9.3, the multi-grain model with native grain preservation | Implemented and never documented; it is the mechanism by which Rule 1 is achieved |
| Part II.7.4, statistical rigour requirements | Implementation exceeded the written concept |
| Part II.7.5, the Window Anchoring Law | Derived from a measured defect |
| Part II.7.6, the Single Engine Implementation Law | Derived from a measured defect |
| Part II.9.2, the Namespace Authority Law | Derived from a measured defect |
| Part II.9.4, the Typed Outcome Reading Law | Derived from a measured defect |
| Part II.9.3, the Grain Assignment Law | Derived from a measured defect |
| Gates G17, G18 and G19 | Enforcement for the five laws above |
| Persona criteria A1.13, A3.12, and the A12 additions | Enforcement for the same |
| Part IV.21.5, three added surprise questions | The refusal behaviour is now a sales asset and must be rehearsed |

---

*End of the constitution.*

*Change control: edits require the author's explicit approval and a version bump. Every derived document re-validates against this file and cites it. Where any document conflicts with this one, this one wins.*
