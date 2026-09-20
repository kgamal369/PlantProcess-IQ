# PlantProcess IQ - Definition and Design Book Control

| Item | Value |
|---|---|
| File | `PPIQ_Definition.md` |
| Functional Design | v4.10.3 (unchanged by this file) |
| Execution Backlog | v2.23.0 (unchanged by this file) |
| This edition | 15 September 2026 - full rewrite for structure and clarity only |
| Owner | Karim Gamal, SOU Industrial Software, Dusseldorf |
| Classification | Confidential (the book contains the Identity and Topology file) |

This file does three jobs and nothing else:

1. **Part A - Book control.** Which files form the design book, which one is authoritative for what, and where new information goes.
2. **Part B - Product definition.** The owner's original product brief, rewritten as clean requirements: the problem, the aim, the golden rules, and what each chapter must contain.
3. **Part C - Release intent.** The owner's milestone intent, the intelligence component catalogue, and the points where the original intent and the current Backlog disagree.

It is not a seventh chapter and it creates no product requirement of its own. Where this file and a chapter disagree on product behaviour, the chapter governs.

---

# Part A - Book control

## A1. Book register

The book contains exactly **12 document types in 14 physical files**. Only Chapters 3 and 4 have two formats (Markdown plus illustrated Word). Every other document type has exactly one current file.

| No. | Document | File | Owns |
|---|---|---|---|
| 1 | Definition and book control | `PPIQ_Definition.md` | This register, authority order, routing rules, product brief, release intent |
| 2 | Chapter 1 - Marketing and Sales | `PPIQ_Chapter1_Marketing_and_Sales_v4.10.3.md` | Problem, positioning, customer outcomes, commercial story |
| 3 | Chapter 2 - Technical Overview | `PPIQ_Chapter2_Technical_Overview_v4.10.3.md` | Architecture, subsystem boundaries, shared terminology, release split, canonical journey |
| 4 | Chapter 3 - General Technical Function Description | `PPIQ_Chapter3_General_Technical_Function_Description_v4.10.3.md` + `PPIQ_Chapter3_General_Technical_Function_Description_v4.10.3.docx` | Data-flow steps, endpoints, pages, dialogs, controls, screen states |
| 5 | Chapter 4 - Specific Technical Function Description | `PPIQ_Chapter4_Specific_Technical_Function_Description_v4.10.3.md` + `PPIQ_Chapter4_Specific_Technical_Function_Description_v4.10.3.docx` | Canvas, SQL and expression editors, toolboxes, inspectors, engine, jobs, statistics, AI/ML, assistant |
| 6 | Chapter 5 - Tutorial and User Journey | `PPIQ_Chapter5_Tutorial_User_Journey_v4.10.3.md` | Step-by-step tutorials across screens |
| 7 | Chapter 6 - Infrastructure, Website and Administration | `PPIQ_Chapter6_Infrastructure_Website_Administration_v4.10.3.md` | Deployment, testing, sizing, website, licensing, user and licence splitting, logging |
| 8 | Database Architecture and Data Dictionary | `PPIQ_Database_Architecture_and_Data_Dictionary.xlsx` | Objects, columns, keys, constraints, relationships |
| 9 | Database Architecture and Naming Standard | `PPIQ_Database_Architecture_and_Naming_Standard.md` | Schema placement and naming conventions |
| 10 | Identity and Topology | `PPIQ_Identity_and_Topology_v5_28Aug2026.md` | Environment identities, accounts, credential references, hosts, ports, topology |
| 11 | Product Analysis and Evaluation Standard | `PPIQ_Product_Analysis_and_Evaluation_Standard_v1.1.md` | Capability checklist, evidence grades, scoring rules, semantic UI acceptance |
| 12 | Execution Backlog | `PPIQ_Backlog_v2.23.0_14Sep2026.xlsx` | Tasks, dependencies, owners, status, estimates, evidence, UI coverage, handoffs, findings, change history |

Register rules:

- No other file belongs in the book: no README, START_HERE, CHANGELOG, audit or correction report, UI/Figma companion, HTML atlas, JSON/CSV register, manifest, or image folder.
- A ZIP is only a transport wrapper, never a book document.
- The register lists the required inventory. It does not certify that a given delivery contains all 14 files (see A7).
- Always keep the real filename of the approved Backlog. Never rename, rebuild or substitute it.

## A2. Authority order

| Source | Authority | Is not |
|---|---|---|
| Chapters 1-6 | The target product: functional and architectural requirements | Proof that anything is built |
| Database documents | Their declared database-design and naming roles | A second product lifecycle |
| Repository, executed tests, measured runtime | What actually exists | A design decision |
| Latest Backlog | Execution path: decomposition, order, owners, status, closure evidence | A design authority; an older workbook is never a fallback |
| Evaluation Standard | How capability is assessed | A place for dated results (those go in the Backlog) |
| Illustrated Word (Ch 3, Ch 4) | A visual reading edition of the same chapter | A separate authority; a picture never overrides a clause |
| Identity and Topology | Operational environment facts | An alternative architecture |
| This file | Book organisation | A product specification |

Conflict rules:

- A later dated, owner-approved decision replaces an earlier one. Earlier documents are read as history, not quoted against later ones.
- A Markdown/Word mismatch is corrected in the Markdown chapter first; the Word edition is then regenerated. Until then the mismatch is recorded in the Backlog.
- A picture, a task status or a requirement sentence is never implementation proof.

## A3. Where information belongs

Decide the owning document first, then edit there. A topic that spans documents is owned once and cross-referenced everywhere else.

| Information | Sole home |
|---|---|
| File register, authority order, routing rules, version policy | This file |
| Problem, positioning, customer and commercial story | Chapter 1 |
| Architecture, shared terminology, release split, canonical journey | Chapter 2 |
| Pages, dialogs, drawers, windows, controls, screen states, their workflows | Chapter 3 MD; figures in Chapter 3 Word |
| Shared visual foundations, component states, accessibility, keyboard, RTL | One owning section in Chapter 3 or 4; consumers cross-reference it |
| Canvas, SQL and expression editors, toolbox items, inspectors, engine and job mechanics | Chapter 4 MD; figures in Chapter 4 Word |
| Multi-page walkthroughs and tutorials | Chapter 5, referencing Chapter 3/4 contracts |
| Infrastructure, deployment, security operations, administration, website | Chapter 6 |
| Accepted design correction | The owning chapter or database document, plus its revision section |
| Database objects, columns, relations | Data Dictionary workbook |
| Naming and placement rules | Naming Standard |
| Usernames, credential references, hosts, ports | Identity and Topology only; never copied elsewhere |
| Evaluation method, capability map, evidence grades | Evaluation Standard (capability IDs stay stable) |
| Tasks, estimates, status, commits, closure evidence | Backlog |
| Audit findings (confirmed, needing reproduction, withdrawn), bug and rework records | Backlog review/evidence sheets, with dates and sources |
| Unapproved proposals and unresolved conflicts | Backlog decision records; not design until approved and integrated |
| UI acceptance matrices, state/fixture coverage, frame traceability, pending visual work | Backlog UI Coverage and task acceptance/handoff records |
| Dependency checks, status reconciliation, estimate warnings | Existing Backlog governance sheets; no new registers |

Engineering assets (source code, tests, build scripts, raw test output, design-generation tooling) stay in the repository. The book keeps only their conclusions and exact references. Consolidation never deletes unresolved work, evidence or an approved acceptance obligation just to reduce file count.

## A4. Rules for the illustrated chapters (3 and 4)

Each page, dialog or editor is written in this order, keeping its stable identifier:

**Name and ID -> target image -> purpose and scope -> fields and controls -> actions and workflows -> validation and refusals -> states -> contract references.**

- Dialogs sit under their owning page. Toolbox items and inspectors sit under their authoring contract.
- Images are embedded in Word. Opening a Word edition must not need an atlas, SVG folder, JSON file or plugin.
- The Markdown chapter carries the full textual contract and stable figure references, and must be understandable without any removed companion.
- Functional content added to one format is added to the other, or recorded in the Backlog as a pending synchronisation.
- Embedded frames are target design, not proof of interaction depth or implementation. A template-based frame is not a finished user-flow design. Nothing in the book implies a live Figma file, an interactive prototype or runtime proof.

Current content (from the 14-Sep consolidation): Chapter 3 holds the shared foundations, the page/frame index and 87 named dialog records, with 497 embedded images in Word. Chapter 4 holds the S1-S5 authoring-mode reference, 82 toolbox records and 16 inspector records, with 110 embedded images in Word. Remaining visual depth is owned by T-265, T-266 and T-267.

## A5. Version and change policy

- Reorganising the book does not create a new design version. Design stays **v4.10.3**, whose substantive corrections are: total-order tie-safe incremental cursors, one canonical machine scheduling grammar, and bounded dependency freshness with explicit stale-reuse semantics (detailed in Chapters 3 and 4; release interpretation in Chapter 2).
- Do not bump the Backlog version for status or evidence corrections, clarifications, navigation or consolidation. Task additions, retirements or scope changes need an explicit owner version decision.
- Keep one current copy per registered file. No `_new`, `_final`, `_corrected` or parallel workbook branches. A later approved version replaces its predecessor.
- Keep revision history short and inside the owning document. Preserve source history outside the live book; never delete it destructively.

## A6. Execution safeguards

These are index-level guards against falling back to superseded material. Full, mutable records live in the Backlog.

- **Backlog v2.23.0 is the only execution authority.** Do not return to v2.22.1 or v2.22.0, and never reconstruct a workbook from a summary.
- **T-099** is CLOSED GREEN / COMMITTED at `ca02b40ea1b69050a45cb35a3e1d64f15b1f66af` (parent `86e1b677212195f191697ecdca7e9441b3dbde33`), on owner-reported evidence. Its inherited DefinitionKind failure and release-wide limits stay recorded. A task closure is not a release-wide GREEN.
- Dependency and capability changes are keyed by Task ID. Derived sheets (dependency graph, coverage, dashboards) are regenerated and checked against the actual Backlog rows.
- Confirmed defects, items needing reproduction, withdrawn findings and completed work stay distinguishable. An audit observation never silently changes a task's status or scope.
- Contract-ready handoffs are not completion dependencies. Visual work may consume a frozen contract without claiming its producer is complete.
- UI coverage does not replace task ownership. Secondary capability labels do not imply extra implemented capability.

## A7. Delivery checklist

A consolidated delivery is complete only when all of these hold:

- [ ] Exactly 14 files and exactly the 12 document types in A1; no extra files or folders.
- [ ] All six chapters are complete current bodies, not summaries or deltas.
- [ ] Chapters 3 and 4 each have one Markdown and one illustrated Word file, images embedded, navigation stable.
- [ ] The database documents, Identity and Topology and the Evaluation Standard carry their real source versions; old bytes are not relabelled as new.
- [ ] The real latest Backlog is included. A missing workbook blocks delivery; an older one is never substituted.
- [ ] Every piece of formerly separate information has a named home and has actually been merged there.
- [ ] No remaining document needs a removed companion to be understood.
- [ ] Task, dependency, status and estimate totals are verified from the saved workbook itself.

The outcome and any missing input are recorded in this file or in the Backlog, never in a fifteenth file.

## A8. Confidentiality

The book includes Identity and Topology, so the whole book (and any ZIP of it) is confidential. It is never a sales handout. Consolidation never invents, rotates, verifies or copies a credential into another document.

---

# Part B - Product definition

## B1. The problem in the plant

Large multi-stage plants (steel, paper, tyres, food, mineral water, pharmaceuticals, oil and similar) move each product through many machines, inspection devices and inspectors. They share the same pains:

1. **Recurring quality defects** whose source is unknown and takes long troubleshooting to find.
2. **Recurring downtime** and repeated equipment or operating failures with no clear root cause.
3. **KPIs (productivity, availability, yield) that stall** because nobody knows which lever to pull.
4. **Fragmented data.** Every machine and inspection device stores its own history in its own database, spreadsheet or log file, and shows it on its own HMI page only. Nothing links the stages.
5. **Dependence on rare experts.** Linking an early-stage cause (for example a high temperature or a low speed) to a late-stage defect needs a very experienced person, or an expensive consultant who fixes a symptom and leaves.

## B2. The product aim

PlantProcess IQ is **the in-house expert that learns your exact plant fingerprint.** It reads the data the plant already owns but does not exploit, joins the pieces across every stage like a puzzle, and gives daily, evidence-based findings, predictions and recommendations using statistics, machine learning and AI.

## B3. How it solves the problem

1. **Cross-stage root cause.** Correlate defects and equipment and operating failures with every process and operating parameter across the whole plant, so a small deviation in an early unit can be tied to a failure or defect in a late unit.
2. **Practice learning.** Learn from months of history which operating practices give maximum output without failures, and which practices lead to downtime.
3. **Predict and remediate.** When a piece carries a known risky combination early (for example this raw-material amount with this high speed), predict the likely outcome from history and recommend a later-stage practice that historically avoided it.
4. **Deep analysis.** Combine statistics, ML and AI so the platform becomes the plant's own expert.

## B4. What the product provides

1. **Connect any source.** Read from any kind of database or data source, then link all data together.
2. **Five layers of help:**

| Layer | Purpose |
|---|---|
| L1 Dashboards | All units side by side in widgets, tables, charts and heat maps with interactive filters, so a normal eye finds easy information quickly |
| L2 Statistics and correlation jobs | Correlate every process parameter with every other and with defects across all units and devices; display results the same way, so hard information becomes easy to see |
| L3 AI/ML learning jobs | Learn patterns to find defect causes, productivity bottlenecks and the causes of repeated failures |
| L4 Prediction | Predict a later defect or failure from early conditions (for example high early temperature, or an operator running hot and fast) |
| L5 Recommendation | After a prediction, recommend what to do in a later unit to avoid the outcome |

3. **Plant-wide outcomes:** better quality through plant-wide root cause, prediction and avoidance; better KPIs through less downtime and more output.
4. **AI Assistant.** A chat assistant, available on every page, for users who cannot build jobs, dashboards or filters themselves.
5. **Value on every result.** Every statistic, prediction and recommendation carries its money effect: what applying it gains, what avoiding a downtime saves.

## B5. Golden rules

### B5.1 Rule 1 - Generic for every industry and plant

- The product is one standard product for all industries and plants.
- **The product never contains demo content.** No line of code, word, page or component is written for a specific dataset.
- The only dataset-specific material is the **external emulation**: separate source databases that imitate a customer's systems, used to test the product and to show in presentations that it has been tested. The product reads them exactly as it would read a customer.
- **Everything starts empty, including reference data.** Defect catalogues differ by industry, by plant, by unit, by semi-product and by inspection device. The customer configures the link to their own definition tables through the no-code/low-code authoring surfaces, and the product imports from there.

Genericity covers all of these:

1. Import classes accept pharmaceutical, food, steel or any other data.
2. Any database type, structure and table layout.
3. Any process and any workflow, including unique ones.
4. Any inspection device, defect type and defect structure.
5. Any focus: every CEO and engineer chooses their own parameters, KPIs and correlations.

### B5.2 Rule 2 - Enterprise standard

The product is a high-budget enterprise product sold to large companies. Speed, UI/UX, visual design, zero errors and accurate data are all mandatory, at the same time.

### B5.3 Rule 3 - One concept

All design follows one set of rules and one concept. Detailed rules live in the owning chapters and are cross-referenced, never restated with a different point of view.

## B6. What each chapter must contain

| Chapter | Must contain | Audience | Voice |
|---|---|---|---|
| 1 Marketing and Sales | B1-B4 told as a sales story: the problem, the in-house expert and plant fingerprint, the four solution mechanisms, value | Plant CEO, purchasing department | Marketing and sales lead |
| 2 Technical Overview | Concept; key technical features and value; workflows, data flows, technical flow; list and overview of all functional pages; administration features (users and roles, logging, job monitors, licence configuration, per-user page limits, settings, translation, log configuration) and their pages | Middle managers; operations, quality and process engineers | Senior product owner |
| 3 General Technical Function Description | Every data-flow step, from concept down to endpoints; every UI page from purpose down to component, button, layout, style, hook and call level; database schemas, tables, keys and joins (detail in the Data Dictionary) | Customer IT and software staff; our developers for handover | Senior product owner, senior engineer, tech lead |
| 4 Specific Technical Function Description | See the list below | Same as Chapter 3 | Same as Chapter 3 |
| 5 Tutorial and User Journey | See B7 | Configuring users, possibly with little or no software knowledge | Senior product owner |
| 6 Infrastructure, Website and Administration | See the list below | QA and infrastructure engineers; customer CEO and purchasing (website part) | Senior QA and infrastructure engineer; senior UI/UX and web developer for the website |

Credentials and topology, originally requested inside Chapter 3, live only in the Identity and Topology file (A3). This later book rule replaces the original request.

Chapter 4 must design, in depth:

1. **Analysis pages in the style of Qlik Sense:** components, widgets, filters, associative interaction, add page, add widget, edit widget query, data binding, chart types and styles, layout, KPIs.
2. **No-code/low-code authoring:** layout, schema/table bar, drag-and-drop toolbox, wiring diagram with debugging, saving and conversion to code, joins and their reuse in analysis; SQL editor with run, debug and save; predefined and advanced tools.
3. **Job execution at scale:** multithreading and load balancing so hundreds of jobs every 2-3 minutes, each loading around 10 million rows, never crash the server. Incremental import from customer sources reduces the load, but the other jobs remain heavy.
4. **The gate and engine:** rules and validation; how system coefficients improve as learning grows; how the assistant reads from the engine; the engine as the hub of all analysis and AI/ML data and jobs.
5. **Statistics and correlation blocks** for the wiring diagram: inputs, outputs, validation, and the best chart for each function.
6. **AI/ML blocks** for the wiring diagram: same contents as item 5.
7. **The AI Assistant:** a chat box at the bottom right that stays open across all pages and answers from the engine.

Chapter 6 must contain:

1. **Infrastructure and hosting:** Jenkins, Docker, deployment, unit/E2E/other testing, the backlog standard.
2. **Sizing formula:** server and database requirements as a function of DB links, imported data volume, running jobs, users and page limits.
3. **Website** (acting as the first salesperson): the company, all SOU products, and the PPIQ overview.
4. **Commercial administration:** licence price per feature; the cost formula over the same drivers as the sizing formula.
5. **User splitting:** technical design of what each user can see and do, in UI and in code.
6. **Licence splitting:** technical design of what each licence tier can see and do, in UI and in code.
7. **Logging.**

## B7. Chapter 5 - required tutorials

Tutorial rules:

- Written for a junior user with no engineering background.
- Each journey is broken into **at least 15 concrete steps**, in the form: "In the main menu on the right, select X. Page Y opens. In the top bar, click Z. In the drop-down, choose W. Click Save."

Required journeys:

1. Create and configure a DB link to a customer source; select the tables and columns to import.
2. Create, configure and schedule incremental import jobs from the DB link into the dump store; monitor them.
3. Build data preparation (links, joins, filters, ETL, grouping) on dump-store data with the wiring diagram or SQL editor, so it fits the plant data model.
4. Create, configure and schedule jobs that load the prepared data from the dump store into the plant data schema.
5. Create an empty analysis page on plant data; drag filters, widgets, charts and KPI labels from the toolbox; bind and edit each one with the wiring diagram or SQL editor.
6. Build and save an advanced analysis definition (statistics, correlation or AI/ML) with the wiring diagram or SQL editor.
7. Create a job that runs that definition on a schedule, and monitor it.
8. Create an empty page and bind widgets to the output of that definition.

---

# Part C - Release intent and current allocation

Part C records the owner's milestone intent and shows where it now differs from Backlog v2.23.0. Task status and dates are read from the Backlog, not from this part. Milestones below were read from Backlog v2.23.0 on 15 September 2026.

## C1. M1 - Presentation track (historical)

Original target date: 20 August 2026. M1 tasks are not in the active v2.23.0 task table; this section is the intent record only.

| Area | Intent |
|---|---|
| Presentation database | `ppiq_presentation` prefilled: source-shaped schemas emulating customer sources for the DB Link page; dump store for the Canvas; metadata (pages, jobs, wiring, users and roles, logs); plant data (canonical and analytics tables) for charts and the assistant |
| Dashboards, layout, widgets, charts, dynamic filters | About 95% of the Qlik / Power BI / Tableau standard |
| Canvas (wiring diagram, SQL editor) | About 85%; more datasets, use cases and complex queries still needed |
| Edit widget SQL, add widget, add page | About 95% |
| Jobs monitor and configuration | About 95%; create and schedule a Canvas-based job and read its logs; run and monitor incremental import from source to dump store; run and monitor projection from dump store to canonical tables using the saved Canvas definition |
| Prepared analysis pages | A few complete pages from canonical and analytics tables |
| Configuration pages | All present at enterprise level (jobs, DB links, logs, Canvas) |
| Administration pages | Front end only (users, roles, licence control) |
| AI/ML | Proof of concept: 3-4 basic models run from canonical and analytics tables and show results in charts; the data flow matters, not the accuracy |
| Assistant | Gives meaningful, in-context answers from plant data; precision not required |
| Journey | The whole presentation journey is healthy |

## C2. M2 - Release 1, installed at the customer (original intent)

Original target date: 30 September 2026. The current planning basis is in C4.

| Area | Intent |
|---|---|
| Emulation sources | Tested as separate databases |
| `ppiq_app` dump store | Starts empty; filled only by import jobs; fully generic |
| `ppiq_app` metadata | Well-defined generic schema; runtime tables (jobs, logs) start empty; only structural seed rows are prefilled (for example HMI structure, the initial administrator); acquisition and canon belong to the metadata schema |
| `ppiq_app` plant data | Canonical and analytics tables with a well-defined generic schema; start empty; filled only by jobs |
| BI layer | 100% at Qlik Sense level |
| Jobs | 100% at enterprise level; multithreading, load balancing and optimisation tested for hundreds of concurrent jobs on very large tables |
| Prepared pages | None; every user builds their own |
| Configuration pages | All present at enterprise level |
| Administration | Pages visible to the owner or to chosen roles only; roles can be denied widget configuration, widget editing, Canvas or job editing; monthly licence controlled by time or remotely; each tier bounds pages, users, jobs, imported data, available ML models and the assistant, with upgrade and downgrade at any time |
| Intelligence | All components in C3 working at enterprise level |
| Assistant | Answers any question, describes any chart, gives suggestions, predictions and correlations, describes the process; a true in-house expert, as strong as the engine behind it |
| Journey | The whole first-day journey at the customer is healthy |

## C3. Intelligence component catalogue

Layer A is the exact statistical engine. Layer B is the learned-model family. The Assistant components wrap the language model. Machinery components serve all of them. Of the 18 rows, 7 are analytical engines or model families; the rest are control, assistant and machinery components.

| # | Component | Layer | Role | Owning tasks | Milestone |
|---|---|---|---|---|---|
| 1 | MF-01 Process Encoder | B | Turns process sequences into embeddings for the other families (PyTorch). Optional: ships only if it proves worth its cost | T-172, T-176 | M2 |
| 2 | MF-02 Vector Similarity Index | B | Answers "what does this resemble in history?". Exact Flat search is the permanent benchmark; HNSW, IVF-PQ and FAISS are candidates only | T-173 | M2 |
| 3 | MF-03 Novelty / Anomaly | B | Detects cases with no precedent. Simple baseline first, isolation and density methods after | T-174 | M2 |
| 4 | MF-04 Supervised Outcome | B | Predicts binary, multiclass, ordinal or continuous outcomes. A simple baseline is mandatory before LightGBM | T-175, T-176 | M2 |
| 5 | MF-05 / MF-06 Statistical engines | A | Numeric x numeric, binary x numeric, categorical x categorical, numeric x categorical (ANOVA and Kruskal-Wallis), with effect size, p-value and FDR q-value | T-177 (kernel); T-146, T-147 (production convergence) | M2 kernel; M3 convergence |
| 6 | MF-07 Practice Engine | A | Extracts the plant's actual operating practice from its own history (D10) | T-131, T-132, T-133, T-136 | M3 |
| 7 | Supervisor | Above A and B | One premade nightly or weekly job that tunes the coefficients of all jobs from an understanding of the whole dataset. Every change has a stated reason, is reversible, records before and after, and may abstain | None; referenced to Schema Contract v2 section E | Not allocated (see C4-5) |
| 8 | Tool Planner | Assistant | Decides which tool is called. The language model never chooses tools | T-179 | M2 |
| 9 | Retrieval and Evidence Packer | Assistant | Retrieves evidence within the user's permissions first, then packs it within a token budget | T-180 | M2 |
| 10 | Model Serving Runtime and gateway | Assistant | Runs the model self-hosted, private or bring-your-own; narrowest payload; provider identity recorded; no unapproved fallback | T-137 | M2 |
| 11 | Answer Verifier | Assistant | After the model answers: every number must match an evidence handle; correlation is never stated as causation; a refusal cannot be erased. This is the guard against invented figures | T-181 | M2 |
| 12 | Job Protocol | Machinery | `JobSpec.json` to `ResultManifest.json` contract between C# and Python | T-168 | M2 |
| 13 | Columnar and sequence artifacts | Machinery | Parquet / Arrow IPC artifacts and a memory-bounded chunked loader | T-169, T-170 | M2 |
| 14 | Capability Profiler | Machinery | Decides whether the data is sufficient at all; eligibility and refusal | T-171 | M2 |
| 15 | Snapshot Materialiser | Machinery | Seals feature state into closed artifacts; the only path into Python | T-184 | M3 |
| 16 | Promotion kernel | Machinery | Calibration, explanation stability and three-dimensional promotion. C# decides; Python only reports | T-176 | M2 |
| 17 | Benchmark harness B-01..B-09 | Machinery | The measuring machine itself, not the measured values | T-182 | M2 |
| 18 | Production cutover | Integration | T-138 assistant runtime cutover; T-187 snapshots to ML lanes to registry | T-138, T-187 | M3 |

Several M2 components are built in isolation from the production path. Their production use depends on the M3 cutover rows (5, 6, 15, 18). Current status of every row is in the Backlog.

## C4. Original intent versus current Backlog - owner rulings needed

Under the later-date rule the Backlog allocation currently governs. Each row below is recorded so the difference is decided deliberately, not discovered later.

| No. | Original intent (C1/C2) | Backlog v2.23.0 | Ruling needed |
|---|---|---|---|
| 1 | M2 ready on 30 Sep 2026 | M2 is "about one month from the owner planning checkpoint"; M3 is 45 days after M2; no absolute date committed | Confirm the relative basis replaces 30 Sep, or set a new fixed date |
| 2 | All 18 intelligence components working in M2 | Rows 5 (convergence), 6, 15 and 18 are M3 | Confirm the M3 split, or move named tasks back to M2 |
| 3 | Assistant answers any question in M2 | Assistant runtime cutover T-138 is M3; M2 has the isolated layers plus T-223 | Define the Release 1 assistant promise |
| 4 | Administration "front end only, without backend" in M2, yet 6.1-6.4 require working visibility, role restriction and licence tiers | Role catalogue T-119, Users and Roles surface T-120 and licence enforcement T-121 are M2 with real enforcement | Remove the "front end only" wording from the M2 intent |
| 5 | Supervisor is part of the intelligence set | No Backlog task owns it | Create a task, or record it as out of scope for M2 and M3 |
| 6 | Hundreds of concurrent heavy jobs tested in M2 | Bounded worker pools T-107 are M2; capacity certification T-159 is M3 | Confirm that M2 proves the mechanism and M3 certifies the load |

## C5. Known gaps in the book itself (15 September 2026)

1. The Project library holds only the Chapter 4 Word edition. The Chapter 3 Word edition must be present before any delivery is called complete (A7).
2. The Project library stores several files without the version suffixes used in A1 (for example `PPIQ_Chapter1_Marketing_and_Sales.md`, `PPIQ_Backlog_v2_23_0_14Sep2026.xlsx`). A1 follows the names recorded in the Backlog Book Register sheet. Align the library names or the register in one pass.
3. The previous edition of this file carried the original owner brief verbatim, twice, plus two conflicting file registers. This edition replaces them with Part B and a single register. The verbatim brief is preserved as provenance outside the live book.