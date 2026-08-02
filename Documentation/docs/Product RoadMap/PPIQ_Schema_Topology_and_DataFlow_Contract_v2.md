# PPIQ — SCHEMA TOPOLOGY AND DATA FLOW CONTRACT
## Version 2 · 27 July 2026

**Ruled by Karim · Supersedes v1 of the same date**
**Status: AUTHORITATIVE for all M2 database, engine and analysis-surface work**

> **What changed from v1, and why it matters.** v1 described three schemas and a five-stage pipeline. It was correct and incomplete. Three rulings in v2 change the architecture rather than its detail:
>
> 1. **The relational model authored on the canvas is permanent.** It is not an import mapping that runs and is discarded. The joins, keys and links a user declares there are the product's model of that plant *for the life of the installation*.
> 2. **The customer's engineer authors it, not the vendor.** That is a product-philosophy ruling, not a services decision, and it determines what the surface must be capable of.
> 3. **The engine is not a set of jobs. It is a two-layer system with a supervisor that improves the others.** v1 treated it as a consumer of the plant schema. It is a compounding one.

---

## PART A — WHERE WE ARE TODAY

### A.1 Two databases, one maintained

| Database | Role | Status |
|---|---|---|
| `ppiq_app` | The main application database | **Paused for the presentation. Resumes after.** |
| `ppiq_presentation` | Purpose-built for the presentation | **The live one.** Everything demonstrated runs here |

Every fix made against `ppiq_presentation` between 22 and 27 July — including today's widget-code correction — exists there and **not** in `ppiq_app`. When work resumes, it starts from a state predating all of it. See **D.1**.

### A.2 The schemas already exist in abstract form — this is enhancement, not creation

`[RULED]` **The codebase already carries a basic, abstract schema for both Plant Data and Meta Data.** The M2 work is to **enhance and correct** what is there, not to design from a blank page.

This changes the shape of the task materially:

- There is an existing table set to measure against the generic requirement, not a hypothesis to invent.
- There is existing data to migrate, so the migration is real work rather than a fresh install.
- **The risk is different too.** Enhancing an abstract schema that was shaped around one reference industry can produce something that looks generic and is not — because the vocabulary survives even when the structure changes. That is why **B.1** requires proof against a second industry rather than a review.

`[MEASURED]` The legacy `plantprocessiq` database carries `canon`, `dump_store`, `src_caster_oracle_shape`, `src_hsm_oracle_shape` — emulated-source residue inside an application database, which Amendment 6 exists to eradicate. Those names are visible in the preparation canvas tree today.

---

## PART B — THE RULING: EXACTLY THREE SCHEMAS

Three application schemas. Not four, not "three plus a legacy one."

### B.1 Plant Data schema — the customer's world

Everything that exists **because of this customer's data**.

- **Starts empty.** Zero rows on install. The literal Rule 2 proof, testable on day one.
- **Generic by construction.** Accepts any plant, any industry, any source shape. Filled only by jobs.
- **Holds the raw data**, in the product's own sense: *the standardised form we compute on*.
- Engine outputs live here, because they exist because of this customer's data.

> **The proof obligation.** "Generic" is a claim earned against a second industry with a different unit of production, a different genealogy shape and a different quality vocabulary — not against a review of the current tables. Design before migration.

### B.2 Meta Data schema — everything that ships identically to every customer

**The only schema holding anything that is not customer plant data.**

| Contents | Ships as |
|---|---|
| Page layouts and dashboard definitions | Some prefilled |
| User roles | Prefilled |
| Credentials and authentication | Default users prefilled |
| Front-end design data — the toolbox, the widget catalogue | **Prefilled** |
| Licence data | Prefilled |
| Job logging | Starts empty |
| Any other non-plant data | As appropriate |

**One pre-made schema. Some tables start empty, some start prefilled.** That split is part of the contract and is declared **per table**, not left to whichever seed script ran last.

> **The test that decides which schema a row belongs in:** if two different customers would receive the identical row, it is Meta Data. If it exists because of what one customer imported, it is Plant Data.

### B.3 Dump Store schema — data in transit

The landing zone. Everything arriving from a customer source lands here **exactly as it arrived**, before interpretation.

- Never displayed in an analytical surface.
- Read only by the authoring surface, where a human decides what it means.
- Its shape is the customer's shape, not ours. That is the point of it.

### B.4 Nothing else

No fourth schema. No emulated-source schemas inside an application database. No legacy database.

---

## PART C — THE RELATIONAL MODEL IS AUTHORED ONCE AND PERSISTS FOREVER

**This is the most consequential ruling in v2, and v1 did not contain it.**

### C.1 The ruling

The joins, foreign keys, primary keys and links a user declares on the authoring canvas when moving Dump Store data into Plant Data are **not** a transient import mapping. They are **the product's model of that plant**, and they persist through every downstream stage for the life of the installation.

> Karim's words: *"those linking and join and fk and pk happened to the data set at this stage continue with us along the whole project in all other stages and forever."*

This is the same principle as Amendment A1.7 — **the join is declared once, in S1, and never again** — stated from the data side rather than the interface side. A1.7 explains why arithmetic blocks may sit on S2–S5 boards but not on S1: because by S2 the row correspondence already exists. **C.1 is the reason it exists.**

### C.2 Worked examples, as given

**Cross-source identity join.** A dump file from the hot strip mill's Oracle database carries `piece_id`. A dump file from the surface-inspection device's MySQL database carries `material_id`. The user declares that these are the same physical object. **From that moment, every genealogy walk, every correlation between a mill process parameter and a surface defect, and every widget that shows both together depends on that one declaration.** Nothing downstream re-derives it.

**Normalised process parameters.** An electric arc furnace publishes its parameters normalised across two tables: a `process_definition` table holding `process_id` and `process_name`, and a values table holding `process_id`, a value, and a `heat_id`. The user — or an automatic script — declares the join that turns that pair into named, heat-scoped observations. **That declaration is what makes "superheat at heat 41207" a thing the product can reason about at all.**

### C.3 What follows from permanence

| Consequence | Requirement |
|---|---|
| A wrong join is wrong **forever**, silently, across every surface | This is why illegal wiring must be **refused at drag time with a sentence** — A1.5, delivered on S1, 27 July |
| A join must be **inspectable** long after it was authored | The published definition must be reopenable, readable and versioned — already true on S1 |
| A join must be **changeable** without rebuilding the installation | Immutable versions with a rollback pointer — already true on S1 |
| A join must **survive a schema migration** | The M2 migration must carry the authored model, not just the tables. **This is not currently in any task** |
| The model must be **exportable and reviewable** by the customer's own engineer | So that a second engineer can audit what the first declared |

> **The gap this exposes.** The M2 schema migration currently plans to move tables and update schema-qualified references. It says nothing about the **authored relational model** that sits above them. Migrating the tables while breaking the joins would destroy the customer's accumulated understanding of their own plant, which is the most valuable thing in the installation. **Added as `M2-Sf` in Part F.**

### C.4 Ownership — the customer's engineer authors this

`[RULED]` **Fitting customer source data into the Plant Data tables is the customer engineer's task, not the vendor's**, because he knows his data and its logic.

This is a product ruling, and Rule 6 surface 1 already states the reason: *each user knows his own plant, and the vendor cannot know the schema architecture of every customer's plant.*

**What it demands of the surface, and each of these is a testable requirement:**

| Requirement | Why |
|---|---|
| Achievable **without writing code** | A plant engineer is not a developer. This is the entire no-code premise |
| **Failures explain themselves in sentences** | There is no vendor engineer beside him to interpret a red outline. The debug log of A1.3 is not a nicety, it is the support model |
| **Refuses illegal work at authoring time**, not at run time | A mistake found a week later, in an engine result, is unattributable to the join that caused it |
| **A SQL path for the long tail** | A1.2: what a block palette will never cover must not become a support ticket |
| **Inspectable and versioned** | Because a second engineer will inherit it |

> **This is the strongest argument in the product for the M2 authoring shell**, and it should be the one used with a buyer. The shell is not a convenience feature. It is the mechanism by which the product becomes *this customer's* product without the vendor learning their plant.

---

## PART D — THE DATA FLOW CONTRACT

```
  ┌──────────────────────────────────────────────────────────────┐
  │ 1. CUSTOMER SOURCES                                          │
  │    Oracle · MySQL · SQL Server · Excel · SAP · OPC · historians│
  └───────────────────────────┬──────────────────────────────────┘
                              │  DB Link page → import jobs
                              ▼
  ┌──────────────────────────────────────────────────────────────┐
  │ 2. DUMP STORE SCHEMA              as it arrived, uninterpreted│
  └───────────────────────────┬──────────────────────────────────┘
                              │  the no-code canvas, or SQL.
                              │  THE CUSTOMER'S ENGINEER declares
                              │  meaning. Joins persist forever.
                              ▼
  ┌──────────────────────────────────────────────────────────────┐
  │ 3. PLANT DATA SCHEMA           the standard generic tables    │
  │    THIS IS THE RAW DATA. Everything computes from here.       │
  └───────┬──────────────────────────────────────┬───────────────┘
          │                                      │
          ▼                                      │
  ┌─────────────────────────────────┐            │
  │ 4. THE ENGINE — the brain       │            │
  │                                 │            │
  │   Layer 1  data analysis jobs   │            │
  │            statistics           │            │
  │            correlation          │            │
  │   Layer 2  AI + ML jobs         │            │
  │            deep, advanced       │            │
  │                                 │            │
  │   SUPERVISOR — one premade job, │            │
  │   nightly or weekly. Revises the│            │
  │   others' coefficients and      │            │
  │   parameters from an            │            │
  │   understanding of the WHOLE    │            │
  │   dataset and ALL jobs.         │            │
  │                                 │            │
  │   Results → Plant Data schema   │            │
  └───────┬─────────────────────────┘            │
          │                                      │
          ▼                                      ▼
  ┌──────────────────────────────────────────────────────────────┐
  │ 5. ANALYSIS SURFACES — Qlik-Sense style, three page types     │
  │    Type 1 raw · Type 2 statistics · Type 3 AI + ML            │
  └──────────────────────────────────────────────────────────────┘

  ┌──────────────────────────────────────────────────────────────┐
  │ META DATA SCHEMA ──▶ administration surfaces ONLY             │
  │ user roles · jobs · logging · licensing · layouts             │
  └──────────────────────────────────────────────────────────────┘

  LICENCE TIER ──▶ higher tiers unlock an assistant that
                   answers FROM THE ENGINE
```

### D.1 Stage rules

| # | Stage | Rule |
|---|---|---|
| 1 | Sources → Dump Store | Only through the DB Link page and its jobs. Read-only toward the customer, always. Nothing else writes to Dump Store |
| 2 | Dump Store → Plant Data | Only through the authoring surface. **A human declares the meaning, and that declaration is permanent** |
| 3 | Plant Data is the raw data | Every calculation, correlation, statistic and AI/ML operation reads from here. Nothing computes on Dump Store |
| 4 | Engine outputs | Written back into Plant Data, because they exist because of this customer's data |
| 5 | Analytical display | Reads **only** from Plant Data |
| 6 | Administrative display | Meta Data appears **only** in administration surfaces |

### D.2 The isolation rule

> **No analytical surface may display a row that did not come from the Plant Data schema.** Not from Dump Store. Not from Meta Data. Not from any other schema.

**Why this is not pedantry.** A widget that could read Dump Store would show a customer their own unmapped source columns and call it intelligence. The entire value claim is that data was *understood* before it was displayed — that a human declared what a column means. A chart reading Dump Store skips that declaration and shows raw import as insight. **It would look like it was working**, which makes it the most damaging thing the product could do.

The mirror clause: an administration surface displaying Plant Data has leaked production data into an operational screen where role scoping may not apply.

### D.3 Enforcement in three layers

| Layer | Mechanism |
|---|---|
| **Database** | The role the analytical engine connects as holds **no grant** on Dump Store or Meta Data. Cannot be bypassed by an application bug |
| **Application** | The widget query engine resolves table names only from the Plant Data catalogue. Anything else is refused with a **named** error — the shape of the unexecutable-measure guard shipped 27 July |
| **Test** | An architecture test asserting no analytical query path references a table outside Plant Data, and no administration path references one inside it. **Falsified once before it is trusted** |

---

## PART E — THE ENGINE

`[RULED]` **The engine is not a set of jobs that run. It is the brain, and its jobs compound.**

### E.1 Two layers

| Layer | Source | Character |
|---|---|---|
| **Normal analysis** | Data-analysis jobs | Statistics, correlation, mathematical analysis. Answers in minutes to hours |
| **Deep analysis** | AI + ML jobs | Model-based, advanced. Slower, richer |

Both write their results into defined tables in the Plant Data schema. **Every job's result improves every other job's result** — this is the property that distinguishes the engine from a scheduler.

### E.2 The supervisor — one premade job

> Karim's words: *"all jobs of engine are hands and arms and legs and can reply within minutes or hours, but once a day at night or once a week at the end, the engine job runs to enhance some coefficient or adjust some parameter across all engine jobs, based on deep detailed advanced professional thinking and understanding of the whole job and the whole dataset."*

| Property | Ruling |
|---|---|
| **Premade** | Ships with the product. Not authored by the customer |
| **Cadence** | Nightly, or weekly at the weekend |
| **Scope** | Every engine job, and the whole dataset — not one outcome |
| **Action** | Adjusts coefficients and parameters used by the other jobs |
| **Purpose** | The others answer. **This one makes the answers better over time** |

**What this demands, and none of it is optional:**

- Every adjustment is **recorded with its reason**, or the product cannot explain why an answer changed between two Mondays. A silent coefficient change in an evidence-grade product is a contradiction of the honesty claim.
- The adjustment must be **reversible**, because a supervisor that degrades results must be undoable without a restore.
- A **before-and-after** on the affected jobs must be inspectable, or nobody can tell improvement from drift.
- The supervisor must be able to **abstain**. If the dataset does not support an adjustment it changes nothing and says so — the same discipline as the readiness gate.

> **The commercial framing.** This is the difference between "we run analyses for you" and "the system gets better at your plant the longer it runs." It is the strongest claim in the product, and the one most exposed if it cannot be evidenced. **The audit trail is not overhead. It is the claim.**

### E.3 The assistant answers from the engine

Higher licence tiers unlock an assistant backed by an LLM that **answers from the engine** — from what the jobs and the supervisor have established, not from a general model's opinion.

**Two consequences that decide whether it is credible:**

- Its answers are **only as good as the engine's state**, so an assistant answering confidently while the engine has never completed a run is describing something that does not exist.
- It must remain **within the logical boundary of the question**. A speed question answered in a unit of mass is not an inaccuracy, it is evidence the answer was not grounded at all. *(This is the untested risk carried at the top of the implementation review.)*

---

## PART F — THE THREE ANALYSIS PAGE TYPES

All Qlik-Sense style. **All read only from Plant Data.** The types differ by *which* Plant Data they show and *which* charts suit it.

### F.1 Type 1 — Raw data analysis

Shows and links the customer's raw data: what arrived from their sources, passed through Dump Store, and was mapped into the standard tables.

> **The advantage, and it is the one to demonstrate:** a single chart or dashboard can show data **from several locations and several different customer databases together**. A mill parameter from Oracle beside a surface-inspection reading from MySQL, on one axis, because the join declared in Part C made them the same object.
>
> Karim's framing: *good UI/UX charts help the normal eye analyse and detect.* No statistics required. **This is the page type that proves the integration claim visually**, and it needs no completed engine run to be impressive — which makes it the safest strong beat in the presentation.

### F.2 Type 2 — Correlation, statistics and mathematical analysis

Shows the outputs of Layer 1 engine jobs. Chart types suited to that data: correlation plots, scatter, distributions, **large volumes of data in a single chart**.

### F.3 Type 3 — AI and ML analysis

Shows the outputs of Layer 2 engine jobs. Leans more on **table widgets** and result-shaped displays than on continuous plots.

> `[MEASURED 27-JUL]` `MODEL_INSIGHTS` exists in `ppiq_presentation` and both its widgets carry rows — 97 and 4. It appears in **no committed script**. So the type-3 page works today and would vanish on a clean rebuild. That is reproducibility debt, not a missing feature.

---

## PART G — WHAT THIS MEANS IN PRACTICE

### G.1 The code-versus-data rule

Today's five broken widget codes were corrected in the C# seeder and by a live `UPDATE` against `ppiq_presentation`. The SQL script that installs the defect was **not** corrected.

> **A fix that exists only as data in `ppiq_presentation` does not exist.** It will not survive a rebuild, will not appear in `ppiq_app`, and will not reach a customer. Every data correction is paired with the script change that makes it reproducible, **in the same commit**.

This is the strongest practical argument for **B.2's prefilled contract**: shipped metadata must be a versioned artefact, not whatever the last hand-run script left behind.

### G.2 Coverage against Backlog v30

| Concern | v30 M2-30 | v2 |
|---|---|---|
| Three schemas, explicit declarations, migration, eradication | ✅ | carried |
| Plant schema zero rows on day one | ✅ | carried |
| Existing abstract schemas need **enhancement** not creation | ❌ | **A.2** |
| Generic tables proven against a second industry | ❌ | **B.1** |
| Meta Data ownership and prefilled-per-table contract | ❌ | **B.2** |
| **The authored relational model persists forever** | ❌ | **Part C** |
| **The model must survive migration** | ❌ | **C.3 → M2-Sf** |
| **Customer engineer authors it** | ❌ | **C.4** |
| Five-stage flow contract | ❌ | **D.1** |
| Isolation rule, three enforcement layers | ❌ | **D.2, D.3** |
| **Engine as two layers with a compounding supervisor** | ❌ | **Part E** |
| **Supervisor audit trail, reversibility, abstention** | ❌ | **E.2 → M2-Sg** |
| Three page types and what each displays | ❌ | **Part F** |
| `ppiq_app` reconciliation | ❌ | **G.1 → M2-Se** |

**Twelve of fourteen concerns were absent from the single 16-hour line.**

---

## PART H — BACKLOG DELTA

**Retire** v30 `M2-30` (16h). Replace with seven tasks, **76h** — the honest cost.

| ID | Task | Pri | Owner | Est |
|---|---|---|---|---:|
| **M2-Sa** | **Schema topology and migration.** Exactly three schemas; every entity declares one explicitly; one audited migration preserving data; emulated-source tables deleted; legacy database dropped after archive verification | Critical | Claude | 16h |
| **M2-Sb** | **Enhance the abstract plant-data schema and prove it generic against a second industry** — different unit of production, genealogy shape and quality vocabulary. **Design before migration** | Critical | Both | 16h |
| **M2-Sc** | **Meta Data ownership and the prefilled contract.** Layouts, roles, credentials, front-end design data including toolbox and widget catalogue, licensing, job logging. Each table declares empty-on-install or prefilled-on-install; every prefilled row ships from a versioned script | Critical | Claude | 12h |
| **M2-Sd** | **The isolation rule, enforced in three layers.** Grants, catalogue-only resolution with a named refusal, and an architecture test falsified once before it is trusted | Critical | Claude | 10h |
| **M2-Se** | **Reconcile `ppiq_app` with everything fixed in `ppiq_presentation`.** Enumerate every 22–27 July correction; establish for each whether it exists as a script or only as data; close the gap | Critical | Both | 4h |
| **M2-Sf** | **Carry the authored relational model through the migration.** The joins, keys and links declared on the canvas are the customer's accumulated understanding of their own plant. Migrating tables while breaking them destroys the most valuable thing in the installation. Includes export and re-import so a second engineer can audit what the first declared | Critical | Claude | 10h |
| **M2-Sg** | **Supervisor accountability.** Every coefficient adjustment recorded with its reason, reversible, with a before-and-after on the affected jobs, and able to abstain when the dataset does not support a change. Without this the compounding claim cannot be evidenced, and it is the strongest claim in the product | Critical | Claude | 8h |

> **Sequencing that matters: Sb before Sa.** Migrating tables into a topology before knowing whether their shape survives a second industry means migrating twice. The design question is the expensive one; the move is mechanical.

---

*Version 2 ruled 27 July 2026. Parts B, C, D, E and F are Karim's rulings formalised. Parts A, G and H are measured or derived, and say so where they are neither.*
