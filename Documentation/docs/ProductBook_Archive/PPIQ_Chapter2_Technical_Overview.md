# PlantProcess IQ - Master Design Document

**Version 4.1 | Author: Karim, SOU Industrial Software, Dusseldorf**

*PPIQ.txt item 3, "Chapter 2: Technical overview". Sections numbered 3.1 to 3.8 per the guideline.*

---

# CHAPTER 2 - TECHNICAL OVERVIEW

> **Audience (3.7):** middle managers, operations engineers, quality engineers, process engineers. Technically literate, not developers.
>
> **Voice (3.8):** senior product owner.
>
> **Version 2 changes:** 3.4 gains a per-page overview for all 34 pages. 3.5 is completed with jobs monitoring, page-creation limits, settings, translation and log-channel configuration. 3.6 gains the administration page list with an overview of each. The page count corrects to 36: four administration surfaces required by 3.5, plus Early Warning and Practice Insights required by guideline 1.3.b and 1.3.c.

---

## 3.1 Concept and idea of the software

Every process plant records what happens to its product many times, in many systems, under many vocabularies. The melt in one database, the casting sequence in another, the rolling pass in a third, the surface defect in a fourth, the laboratory result in a fifth. Each system is correct on its own. **Nothing joins them**, and every question that matters lives in the join: which upstream parameter drives which downstream defect, which stoppage actually cost production, which material is at risk before the defect appears.

PlantProcess IQ is a **read-only intelligence layer** over the systems the plant already owns.

1. It **connects** through read-only links and imports continuously, changing no source system.
2. The plant's own engineer **declares once** how the sources join. That declaration becomes the plant's permanent model of itself.
3. From then on the plant is **one analysable thing**: dashboards, statistics, machine learning, prediction, recommendation and a grounded assistant, every figure citing its evidence.

It replaces nothing, writes to nothing, computes deterministically, refuses honestly when the data cannot carry an answer, and prices findings in euro as a bounded range.

One binary serves any process industry because nothing inside it knows any industry. Industry knowledge arrives with the customer's data, through the import, and never ships in the product.

## 3.2 Technical features, key aims and added value

### 3.2.1 The five capability layers

| Layer | Capability | Added value to the reader of this chapter | Tier |
|---|---|---|---|
| 1 | **Unified visibility** | Every source side by side on one surface. Plant-wide patterns become visible without expertise | Light |
| 2 | **Statistical intelligence** | Every parameter related to every defect, downtime cause and performance measure, under honest statistics | Pro |
| 3 | **Machine learning** | Probable contributors, bottlenecks and recurring-failure drivers found by model, not by memory | Pro Plus |
| 4 | **Prediction** | Material that ran abnormal upstream flagged for a specific downstream defect **before it occurs** | Pro Plus |
| 5 | **Recommendation** | Suggested later-stage practices to avoid the predicted outcome, and **practice benchmarking**: your own best demonstrated practice, learned from your own history, and the practices that preceded downtime | Pro Plus |

Cross-cutting: **the assistant** (plain-language questions, cited answers, never computes) and **the value engine** (bounded euro range per finding with drill-through inputs). Standing under all five: **the readiness gate** (Chapter 1.8).

### 3.2.2 The technical features that carry those layers

| Feature | Why it matters technically |
|---|---|
| Read-only acquisition through a customer-controlled one-way collector | No write path exists; the plant's automation team can approve without a control-systems risk review |
| Source-shaped staging with watermarks and batch lineage | The plant never remodels its databases; the delta is cheap and the lineage is exact |
| A versioned, customer-authored Transformation Definition | The join is declared once, auditable, exportable, and permanent |
| A canonical multi-grain model with genealogy and weighted attribution | A parent-grain parameter can be attributed to a child-grain outcome; this is the cross-source mechanism |
| Registry-driven authoring | Every list in every palette comes from the customer's own data, which is how one binary serves every industry |
| Deterministic engines with a readiness gate | Numbers are reproducible; an undefendable answer is refused rather than produced |
| Bounded-parallelism job execution with weighted pools | A hundred defined jobs cannot starve the interactive read path |
| Evidence handles on every rendered figure | Any number on any screen walks back to the source row it came from |

## 3.3 Workflows, data flows and technical flow

### 3.3.1 The canonical journey, fifteen steps

Acceptance specification of the product. There is exactly one journey; every tutorial and demonstration expands it.

| # | Step | Principal surface |
|---|---|---|
| 1 | Install and first login; plant schema provably empty | Login, Administration |
| 2 | Licence activation; tier and capacity envelope visible | Licence and Entitlement |
| 3 | Create users and roles | Users and Roles |
| 4 | Declare read-only connections and test them | Connections |
| 5 | Register datasets, including taxonomy sources | Dataset Registry, Prepare Import |
| 6 | First incremental import into staging | Importing, Jobs Monitor |
| 7 | Author the Transformation Definition (S1) | Transformation Studio |
| 8 | Project staged rows to canonical | Importing, Mapping Health |
| 9 | Walk the genealogy on the customer's own keys | Genealogy Explorer |
| 10 | Build pages, widgets and filters (S2) | Page Builder, Workspace |
| 11 | Explore associatively | Interactive Workspace |
| 12 | Run analysis through the readiness gate (S3) | Analysis Toolbox, Findings |
| 13 | Read findings and their euro value | Findings, Value Dashboard |
| 14 | Ask the assistant | Assistant |
| 15 | Operate: logs, alerts, Supervisor, reports | Plant Data Log, Supervisor, Reports |

Steps 1 to 6 commission. Steps 7 to 9 build the plant model. Steps 10 to 15 are daily life.

### 3.3.2 The technical data flow, six stages

```
 SOURCES        COLLECTOR      STAGING         CANONICAL       RESULTS        SURFACES
 customer's     customer's     source-shaped   one model of    findings,      pages, API,
 Oracle,        one-way push   copies, exact,  the plant with  risk, value,   assistant,
 MSSQL, MySQL,  DMZ, budgets   watermarked     provenance      gate records   reports
 PG, files
    |              |               |                |              |             |
    +-- read ----->+-- push ------>+-- Transform ---+-- analysis --+-- render -->+
        only                          Definition        jobs
                                      (S1, versioned)
```

| Stage | One job | The rule that protects the next stage |
|---|---|---|
| Sources | Remain untouched | Never written to; the core never connects inward |
| Collector | Move data one way inside a stated budget | Row caps, timeouts, windows; backfill checkpointed and resumable |
| Staging | Hold an exact source-shaped copy | No interpretation; what arrived is what is stored; no analytical surface may read it |
| Canonical | Hold the one model, produced only by the versioned definition | Provenance on every row; the authored model is permanent |
| Results | Hold what the engines computed, with method, population, framing, gate evidence | Deterministic engines only; reconstructable from the database |
| Surfaces | Show it | No number without an evidence handle; refusals are named |

### 3.3.3 The golden evidence chain

> **Fresh import batch -> authored mapping -> canonical projection -> gated analysis run -> evidence-ranked finding -> cited assistant answer.**

Every link inspectable. Cut it anywhere and the product refuses rather than bridging the gap silently.

### 3.3.4 The six operating workflows

**W1 Commission a plant** (once). Install, licence, administrator, users, connections, register datasets, first import, author mapping, project, verify genealogy. Exit: the golden chain holds on first data.

**W2 Daily engineer loop.** Open workspace, select and drill, notice a pattern, open the analysis toolbox, run a gated analysis, read finding and value, act or ask the assistant. Exit: a decision with evidence behind it.

**W3 Investigate a defect.** Quality event arrives, open Genealogy Explorer from the affected unit, read upstream parameters on the thread, correlate against the defect class, read the finding with population and framing, read the value range, export the report.

**W4 Extend the model** when a new source or column appears. Register dataset, import, extend the Transformation Definition as a new version, project, mapping health green, the registry offers the new columns everywhere automatically.

**W5 Author a new page** as a non-programmer. Page Builder, add widget, choose kind, the shell opens in S2 mode, catalogue or query binding, preview, save, the page appears for its audience. Filters authored the same way.

**W6 Operate.** Scheduled imports and analyses run, the plant data log raises entries by authored rules, alerts are acknowledged, the Supervisor proposes and a human approves, the monthly value report exports.

---

## 3.4 The page inventory, with an overview of each

**Thirty-six pages in six groups.** Every page publishes the page contract (Section 4.7) and is specified control by control in Section 4.4. The overview below is what a manager or engineer needs in order to know why the page exists and when they will open it.

### Group A - Enter

**A1 Login** - `/login`. The authentication surface. It exists to prove identity before anything else and to make a failed attempt informative without being useful to an attacker: a wrong password says the credentials failed, never which half was wrong. Administrators are additionally challenged for a second factor. The page carries no navigation, because a user who is not authenticated has nowhere to go.

**A2 Home** - `/`. The landing view and the honest answer to "is this thing working?". It shows plant status at a glance, the readiness meter with its measured counts, the most recent findings, and the journey rail marking how far commissioning has progressed. On a young installation it deliberately shows a collecting-data state with a countdown rather than an empty screen, because a blank landing page is the fastest way to lose a new user.

### Group B - Connect and import

**B1 Connections** - `/data-integration/connections`. Where read-only links to the plant's own databases are created, tested and scheduled. It is the only door for plant data and it carries the read-only promise line permanently on screen. It also shows the connector catalogue with honest availability, so a buyer sees which sources are proven and which are planned without asking.

**B2 Dataset Registry** - `/data-integration/registry`. Where the customer chooses which of the source's tables, views and files enter the product. It browses the live source, so the engineer sees his own table and column names on screen. Registration is what makes a dataset due for import.

**B3 Prepare Import** - `/data-integration/prepare`. Where each registered dataset gets its imported columns, its business key and its watermark column. The watermark is the incremental cursor, and this page is where a plant decides how cheap or expensive its imports will be.

**B4 Importing** - `/data-integration/importing`. Where imports are run and watched: batches with counts, watermark ranges and outcomes, plus the per-mapping projection schedule. It answers "did my data arrive, and how much of it".

**B5 Jobs Monitor** - `/data-integration/jobs`. One monitor for every job family: import, projection, analysis, machine learning, supervisor, alert evaluation. Columns are job, type, target, status, last run, duration, runtime and actions. It exists so that a plant never has to ask which of six screens shows whether something ran, and it shows refusals with their reasons alongside successes.

**B6 Connector Truth** - `/data-integration/connector-truth`. The capability matrix per connector: what is proven, what is certified read-only, what is planned. It exists because a catalogue row is not a connector, and an honest matrix is worth more in a technical review than a marketing claim.

### Group C - Model the plant

**C1 Transformation Studio (S1)** - `/prep/canvas` and `/data-integration/author-mapping`. The most important authoring surface in the product. Here the customer's engineer declares how staged data becomes the canonical model: joins, keys, aliases, mappings. It offers a block-and-wire canvas for a plant user and SQL for an engineer, refuses an illegal wire at drag time with a written sentence, shows the compiled SQL so nothing is hidden, and publishes immutable versions. What is authored here is the plant's permanent model of itself.

**C2 Mapping Health** - `/mapping-health`. The health of that model over time: coverage, unmapped columns, orphan rates, drift since the last projection. It exists because a mapping that was correct in March can silently stop matching a source that changed in June, and this page is where that surfaces before it corrupts a finding.

**C3 Data Quality** - `/data-quality`. Issues by class and source: completeness, validity, freshness, duplicates. It is the page a process engineer opens when a finding looks wrong, and it is what turns "the data is bad" into a named, countable list.

**C4 Plant Model Explorer** - `/plant-model`. Sites, areas, equipment, routes and operations as imported and mapped. It is the structural view: what the product believes the plant physically is. Commissioning uses it to confirm the model matches reality before analysis begins.

**C5 Genealogy Explorer** - `/materials/{id}`. Walk from any finished unit back to its origin and forward to its descendants, on the customer's own key names, with attribution weights where a unit spans two parents. This is the page that produces the "it already speaks my plant" reaction, and it needs no engine run to be impressive.

### Group D - See and analyse

**D1 Interactive Workspace** - `/workspace/:dashboardCode`. The analysis page. Pages of widgets on a twelve-column grid, associative selection where clicking any value narrows everything and shows what is possible and what is excluded, an always-present selections bar, and per-card tools. It is where most users spend most of their time.

**D2 Page Builder (S2)** - `/page-builder`. Where pages, widgets and filters are authored without code. Add widget, choose kind, the shared shell opens with the current definition loaded, bind by catalogue or by an authored query, test, preview, save. Filters are authored widgets here, not fixed furniture, because every plant filters by different things.

**D3 Analysis Toolbox (S3)** - `/analysis/toolbox`. Where an analysis is declared: outcome, grain, window, method. Every option comes from the registry. The payload panel shows exactly what the engine will receive, which is what lets an engineer trust the run before starting it.

**D4 Findings** - `/correlations`. Results ranked by effect size, never by p-value, with q-values, sample sizes, stability flags, stratum survival and the stored framing. Non-significant results appear as first-class honest answers. Every number drills through to the population behind it.

**D5 Risk Dashboard** - `/risk`. Predictive risk per unit, grade or route, with the drivers that produced the score and the horizon it applies to. It is the forward-looking view, and it exists only above the readiness gate.

**D6 Suggestions** - `/suggestions`. Recommended actions with their evidence, and the tracking of what was decided and what happened afterwards. It closes the loop between a finding and an action, which is what turns analysis into value.

**D7 Value Dashboard** - `/value`. The euro view: bounded impact ranges, the inputs behind them, and drill-through on every figure. Where inputs are missing it says insufficient basis rather than producing a number, which is what makes the numbers it does produce credible in a purchasing review.

**D8 ML Readiness and Models** - `/ml-readiness`. Readiness per outcome and grain, the model registry, training runs and scores. It is the honest answer to "can this plant do machine learning yet", per outcome, with numbers.


**D9 Early Warning** - `/early-warning`. The prediction queue: units currently mid-process that are flagged elevated-risk for a specific downstream defect, ranked by risk and time-to-stage, each with its drivers and, where the plant's own history supports one, a remediation card naming the later-stage practice that historically neutralised this early condition. This is the page where layer 4 and layer 5 become an operating routine rather than a report. It exists only above the readiness gate and never shows a score computed on insufficient data.

**D10 Practice Insights** - `/practice-insights`. The practice-learning view: the operating practices reconstructed from the plant's own history, linked to their outcomes - the combinations that coincided with maximum productivity without failure, and the combinations that preceded downtime - presented as benchmarks with their evidence and support counts, plus a drift panel showing where current operation has moved away from the plant's own best demonstrated practice.

### Group E - Ask and operate

**E1 Assistant (persistent dock)**. Not a destination page: a chat box anchored at the inline-end, block-end corner of **every** page, collapsed to a launcher by default, expanding to a panel that persists across navigation. It knows what page it is opened on and offers context-aware starters; it answers from tool results and permission-scoped retrieval with every number cited, and refuses where evidence is absent. A user who cannot build a dashboard or read a statistical chart can still reach an answer, from wherever they already are.

**E2 Assistant Configuration** - `/assistant-config`. Which tools the assistant may use per role and tier, which knowledge sources are indexed, the plant glossary and its synonyms, the guardrail phrases, the citation ceiling and the serving mode. It exists because a capability that cannot be configured from the interface is not finished.

**E3 Plant Data Log and Alerting (S5)** - `/data-integration/alerting`. Authored rules that raise info, warning and error entries against imported observations, the log they write, and acknowledgement. Evaluation is idempotent, so running it twice logs nothing twice.

**E4 Supervisor** - `/data-integration/supervisor`. The governed review that proposes improvements to other jobs, with provenance, dry-run and approval. It never changes a job automatically, and the page says so permanently in its own subtitle.

**E5 Reports** - `/reports`. Scheduled and on-demand reports on the light print surface, with export and webhook delivery. It is how a finding leaves the product and reaches a management meeting.

### Group F - Administer

**F1 Users and Roles** - `/admin/users`. Accounts, role assignment and the permission matrix. Overview in 3.6.1.

**F2 Licence and Entitlement** - `/admin/license`. The signed token, the tier, the capability set and the live capacity meters. Overview in 3.6.2.

**F3 Authoring Quota and Limits** - `/admin/quota`. How many pages, widgets, jobs and datasets each user and each role may create. Overview in 3.6.3.

**F4 Jobs Administration** - `/admin/jobs`. Job definitions, schedules, pool assignment and compute weight. Distinct from Jobs Monitor, which watches; this configures. Overview in 3.6.4.

**F5 Logging and Audit** - `/admin/logs`. The four log layers, filterable and exportable. Overview in 3.6.5.

**F6 Log Channel Configuration** - `/admin/log-channels`. Where an administrator defines a new log channel, its severity mapping, retention and export target. Overview in 3.6.6.

**F7 System Settings** - `/admin/settings`. Site identity, units, time zone, date and number formats, and the plant time zone that shift analysis depends on. Overview in 3.6.7.

**F8 Translation and Language** - `/admin/translation`. Language packs, per-label review, right-to-left verification. Overview in 3.6.8.

---

## 3.5 Administration features

Eight administration domains. Each is a product capability configured from the interface, because a capability that needs a source edit to configure has failed Rule 1 (Chapter 1).

### 3.5.1 Users and roles

**What it does.** Creates accounts, assigns roles, and grants permissions per surface and per action.

**The model.** A shipped role catalogue with a floor of three - Administrator, Engineer, Viewer - and a fuller default set of eight: tenant owner, plant administrator, data engineer, process engineer, operator, viewer, commercial administrator, and a vendor support role that is scoped, time-boxed and audited. Permissions are granular: a role may read a page, act on a page, or neither.

**Two rules that are not negotiable.** A **viewer never authors SQL at any tier**, because SQL against the plant database is a security surface and not a feature. And the licence gate and the role gate **compose**: a capability is available when the tier allows it *and* the role allows it, never when only one does.

**At install** the vendor support account exists and is undeletable; the customer's own administrator is created as a commissioning step. No development or test account exists on any production path.

### 3.5.2 Licence configuration

**What it does.** Applies the signed licence token, displays the tier and its capability set, and shows live consumption against the capacity envelope.

**How entitlement is decided.** Only from the cryptographically signed, offline-verifiable token. Never from a database row an administrator could edit, and never from a value a client supplies - a client-side tier override is ignored by design.

**What the administrator sees.** The tier; the capability list; and **live meters** for the five metered dimensions: retained volume, ingest rate, minimum refresh interval, weighted compute slots, concurrent sessions. Approaching a meter warns and offers the upgrade path. Exceeding one **throttles** - an import queues, a job waits for a slot - and never destroys data or stops work mid-task.

**Expiry** follows warning, then a configurable grace period, then read-only access to what the customer built. Customer data is never destroyed by expiry.

**The tier demonstration.** Switching tier visibly adds and removes capability in the running product, which is a sales moment and also the only honest proof that the gating is real.

### 3.5.3 Authoring quota and page-creation limits

**What it does.** Bounds how much a single user may create, independently of what the tier permits in total.

**Why it exists separately from the licence.** The tier says the installation may hold one hundred pages. That does not mean one enthusiastic engineer should be able to create one hundred pages, each carrying a query the database has to serve. Quota is the per-user and per-role division of a tier's total.

**What is bounded.** Pages, widgets per page, saved queries, analysis definitions, log rules, datasets, and scheduled jobs. Each has a per-role default and a per-user override.

**How a limit behaves.** Soft by default: at eighty percent the authoring surface warns, at one hundred percent the create action is disabled with the reason and the administrator named. **Never a silent failure and never a lost draft.** An administrator may raise a single user's ceiling without changing the role default.

**Why an administrator will actually use this.** A page with an expensive authored query has a real cost in compute slots. Quota is where a plant decides who is allowed to spend it.

### 3.5.4 Jobs monitoring and job administration

Two distinct capabilities, deliberately on two pages.

**Monitoring** answers "what ran, what is running, what failed and why". One monitor covers every family: import, canonical refresh, analysis, machine learning, supervisor, alert evaluation. Per job: type, target, status, last run, duration and runtime. Per run: the log, the row counts, and **the refusal with its reason where the run was refused** - a blocked analysis appears here as a real run with a named blocking dimension, not as an absence.

**Administration** answers "what should run, when, and how expensively". Per job definition: schedule, parameters, the pool it runs in (import, analysis, machine learning, report) and its **compute weight**, which is what the bounded-parallelism executor uses to decide how many may run at once. A statistical job and a model-training job do not cost the same, and the weight is where that is declared.

**Operations available from monitoring:** run now, pause, resume, cancel, and re-run a failed run against the same batch. Pausing is a first-class state and survives a restart.

**Why the separation matters.** An operator needs to watch without the ability to change a schedule. Splitting the surfaces is what makes that role possible.

### 3.5.5 Logging

Four layers, each with its own audience, retention and export path.

| Layer | Contents | Read by |
|---|---|---|
| **System** | Application events, errors, health, request outcomes | Operator, vendor support |
| **Job** | Per-run progress, outcomes, row counts, refusals with reasons | Engineer |
| **Data** | Import batches, watermarks, projection results, mapper field errors | Engineer, auditor |
| **Audit** | Who did what and when: logins, permission changes, threshold edits, resets. **Immutable** | Administrator, auditor |

Every layer is filterable by time, severity, actor and job family, and exportable. The audit layer is append-only at the database level; there is no product path that edits or deletes an audit row.

**The rule that makes the job layer worth reading:** a refusal is logged like a result. A blocked run, a rejected query, a refused wire and an over-budget read all appear with their reason, so the log answers "why not" as readily as "what".

### 3.5.6 Configuring new logs

**What it does.** Lets an administrator define a **new log channel** without a code change.

**What a channel is.** A named stream with a severity mapping, a retention period, an optional export target (file, syslog, webhook) and a visibility rule saying which roles may read it.

**Why it exists.** Every plant has something it wants recorded that the vendor did not anticipate: a specific operator action, a specific integration handshake, a specific approval. Requiring a release for that turns a five-minute administrative wish into a support ticket, which is exactly what Rule 1's configurability corollary forbids.

**The boundary, stated so it is not misread.** A new channel changes what is *recorded and routed*. It does not change what the product *does*, and it can never write to the audit layer, because the audit layer's value is that nothing configurable can touch it. Rules that raise plant-data entries against imported observations are a different feature and live on the Plant Data Log surface (S5).

### 3.5.7 Settings

**What it does.** Holds everything that is true of this installation rather than of this user.

**Contents.** Site and plant identity, including the site code that appears on every log entry. Units of measure. The **plant time zone**, which is not cosmetic: shift-boundary analysis is correct across daylight-saving transitions only because observations carry a local time alongside the universal time and an explicit zone. Date and number formats, stated explicitly and never inherited from the machine locale. Retention policy per stage. The no-egress control that forces self-hosted model serving for a tenant that requires it.

**Per-user preferences** - units, time zone, language - sit with the user, and the installation setting is the default they inherit.

### 3.5.8 Translation

**What it does.** Manages language packs and lets a reviewer approve labels per language.

**Contents.** The label inventory, per-language completion, a review state per label, and the fallback language for anything untranslated. Arabic requires right-to-left rendering, and the administration surface flags any label that has been translated but not yet verified in a mirrored layout.

**The boundary.** Direction neutrality is a build-time law: no component, class or property name may encode a physical side (Chapter 1.7). Translation administration chooses among the languages the build already renders correctly; it cannot fix a layout that hardcoded a side.

---

## 3.6 The administration pages, with an overview of each

### 3.6.1 F1 Users and Roles - `/admin/users`

The account register and the permission matrix on one surface. The list shows every account with its role, last login and state. Selecting an account opens its permissions as a grid of surface against action, inherited from the role and overridable per user, with inherited and overridden values visually distinct so nobody has to guess why a user can do something. Creating an account is a two-field act followed by a role choice; the fuller permission grid is deliberately behind that, so a routine act stays routine. Deactivating rather than deleting is the default, because an account referenced by an audit row must remain resolvable. Every action here writes an audit entry.

### 3.6.2 F2 Licence and Entitlement - `/admin/license`

Three regions. The **token region** shows what the signed licence says: tier, seat count, issue and expiry dates, verification state, and a paste-and-apply action for a new token. The **capability region** lists what this tier grants and what the next tier would add, which is a sales surface as much as an administrative one. The **meter region** shows live consumption against the envelope, as five bars with measured numbers, each linking to the administration page that would relieve it. On approaching expiry the page carries a banner counting down; after expiry it states that existing dashboards remain readable and that no data has been destroyed.

### 3.6.3 F3 Authoring Quota and Limits - `/admin/quota`

A matrix of creatable object type against role, with per-user overrides listed beneath. Each cell holds a limit and shows current consumption, so an administrator can see that engineers are at seventy percent of their page allowance before anyone complains. Raising a single user's ceiling is one action and is audited. The page also lists the top consumers by object type, which is how an administrator finds the one authored query costing the installation its compute slots.

### 3.6.4 F4 Jobs Administration - `/admin/jobs`

The definition side of jobs. Each row is a job definition with its class, target, schedule, pool and compute weight. Editing a schedule is a form; changing a pool or a weight is deliberately a separate action with a confirmation, because it changes what the executor will admit concurrently. The page shows each pool's configured parallelism beside its current utilisation, so the consequence of adding a job is visible before it is added. A definition can be disabled without being deleted, and a disabled definition states who disabled it and when.

### 3.6.5 F5 Logging and Audit - `/admin/logs`

Four tabs, one per layer, sharing a filter bar of time range, severity, actor and job family. Rows expand to their full context payload. The audit tab has no edit or delete action anywhere on it, which is the point. Export produces a file in the light report surface style with the filter stated in its header, so an exported log is self-describing. A saved filter can be pinned, because an operator investigating one recurring fault will use the same filter twenty times.

### 3.6.6 F6 Log Channel Configuration - `/admin/log-channels`

A list of channels with, per channel, its name, severity mapping, retention, export target and reading roles. Creating a channel is a short form and the page previews what an entry in the new channel will look like before it is saved. Built-in channels are visible but locked, with the lock explained in a sentence rather than merely rendered - and the audit channel cannot be created, edited or targeted from here at all, which the page states rather than leaving to be discovered.

### 3.6.7 F7 System Settings - `/admin/settings`

Grouped, not a single long form: identity, units and formats, time, retention, and data boundary. Each group states the consequence of its settings in one line, and the time group states plainly that changing the plant time zone re-frames shift analysis and therefore requires a confirmation. The data-boundary group holds the per-tenant no-egress control, and toggling it names which capabilities change behaviour as a result.

### 3.6.8 F8 Translation and Language - `/admin/translation`

A table of labels against languages with a completion bar per language and a review state per cell. Filters isolate untranslated, translated-but-unreviewed, and verified-in-mirror. Selecting a label shows where it appears in the product, because a translator who cannot see the context will translate a button as a noun. Exporting and importing a language pack is supported so translation can happen outside the product and return to it.

---

## 3.7 Target audience

Middle managers, operations engineers, quality engineers and process engineers. This chapter assumes technical literacy and plant knowledge, and assumes no software development background. Endpoint-level detail is Section 4; surface-level interaction design is Section 5.

## 3.8 Voice

Senior product owner. Explanatory, concrete, and honest about what a page will and will not do.

---

*End of Section 3.*
