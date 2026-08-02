# PlantProcess IQ - Master Design Document

**Version 4.0 | 29 July 2026 | Author: Karim, SOU Industrial Software, Dusseldorf**

*Connect Your Plant Data. Understand Your Process.*

---

# CHAPTER 1 - THE CONSTITUTION

> **Audience:** every person who writes, reads, reviews, builds, sells or audits any part of this product, and every later chapter of this document.
>
> **Voice:** constitutional. This chapter states law. It does not describe, persuade, instruct or report status.

---

## Provenance of this chapter

Every section carries a tag so that the origin of each clause is auditable.

| Tag | Meaning |
|---|---|
| **[C]** | **Carried.** Present in the founding drafts. Raised to specification grade, not redesigned |
| **[E]** | **Enhanced.** The intent was in the drafts; the expression was thin, informal, incomplete or contradictory. Substantially rewritten |
| **[N]** | **New.** Present in no draft. Designed here, on the accumulated knowledge |

| Section | Tag | Note |
|---|---|---|
| 1.1.1 The single source of truth | [C] | |
| 1.1.2 The learning-curve rule | [C] | The author's own rule, 27 July |
| 1.1.3 The lineage | [N] | Traces the whole document to the compass |
| 1.1.4 Documents superseded | [E] | |
| 1.1.5 Chapter scope and the boundary rule | [N] | |
| 1.1.6 The status rule and the evidence rule | [E] | |
| 1.1.7 Change control and the derivation chain | [E] | |
| 1.2 What the product is | [C] | |
| 1.3 Settled trade-offs | [C] | |
| 1.4 The Four Product Rules | [E] | Rule 4 elevated from a pending amendment |
| 1.5 The Honesty Contract | [E] | Two clauses new |
| 1.6 The Emulation Doctrine | [C] | |
| 1.7 Platform boundaries | [E] | One clause new: capacity metering |
| 1.8 The Readiness Gate | [C] | |
| 1.9 The design system | [E] | Stated once, reconciled from two sets |
| 1.10 The quality bar | [E] | The severity doctrine leads it |
| 1.11 The ruling ledger | [E] | |

**Chapter total: roughly 60 percent carried, 35 percent enhanced, 5 percent new.** Chapter 1 is the most heavily documented part of the corpus, so it is the chapter with the least new design in it. The proportions invert sharply in Chapters 5, 6, 7 and 9.

---

## 1.1 Authority, scope and change control

### 1.1.1 The single source of truth **[C]**

This document is the single authoritative statement of what PlantProcess IQ is, what it must do, how it must behave, how it is built, how it is sold, and how it is judged.

Where any other artifact conflicts with this document, this document wins. That includes source code, code comments, backlog entries, slides, proposals, website copy, screenshots, conversations and prior specifications without exception.

Chapter 1 governs Chapters 2 to 10. Where a later chapter conflicts with this one, this one wins.

### 1.1.2 The learning-curve rule **[C]**

*The author's own rule. It governs how every earlier draft is to be read, and how every future amendment is to be made.*

> **The drafts are a learning curve, not a set of contradictions.** A document written on one date describing a design, and a document written a week later describing a different one, are not in conflict. The first produced a defect; the second records the correction.
>
> **Read by date. The later date wins. Never quote an earlier document against a later one.**

Two consequences, both binding.

**First, the ruling ledger of 1.11 is a record of learning, not a list of errors.** Each entry names the question, the answer, and the earlier formulation the answer retires.

**Second, an amendment that reverses a prior ruling does not delete it.** It supersedes it visibly. A specification that conceals its own reversals cannot be trusted about anything else.

### 1.1.3 The lineage **[N]**

*Written here because the corpus contained more than thirty documents and no statement of how they descend from one another. A reader who does not know the lineage cannot tell an origin from a derivative.*

Everything in this document descends from one founding artifact.

| Generation | Artifact | What it contributed |
|---|---|---|
| **Origin** | The Four-Track Vision, "the compass" | Founder intent. Four tracks: Workflow, Hardening, Demo, Website. The generic mandate, the plant blueprint, the brand identity in full, the never-say list, the downtime distinction, the demo doctrine, and the one open question that produced the entire authoring layer |
| **First formalisation** | The founding rules and the original Aspects of Review | The rules restated as numbered law; three review audiences that later became thirteen |
| **Specification** | The Product Doctrine | The deepest engineering expression: honesty mechanisms, readiness numbers, the value formula, the data boundary, the settled trade-offs |
| **Constitution** | The concept and constitution lineage | The three rules sharpened, the fifteen-step journey, the emulation doctrine, the five derived laws |
| **Design specifications** | The authoring layer and schema topology specifications | The genericity mechanism, the shared shell, the permanence of the authored model |
| **This document** | The Master Design Document | All of the above reconciled, with the contradictions ruled and the gaps designed |

**The supremacy inversion rule.** Derivation flows one way. A governing document may not cite a document younger than itself as authority, because the younger one is required to derive from it. Where that inversion appears, the younger document is a proposal awaiting ratification, not a source.

**The four tracks are still visible.** Chapter 2 and Chapter 10 are the Website track. Chapters 3, 4 and 5 are the Workflow track. Chapter 8 is the Hardening track. Chapter 6 and the emulation doctrine are the Demo track. The structure of this document is a refinement of the compass, not a replacement of it.

### 1.1.4 Documents superseded in full **[E]**

The following are absorbed into this document and retired on its ratification. They are kept in the archive as history and are never cited as authority again.

| Superseded document | Disposition |
|---|---|
| The Four-Track Vision (the compass) | Absorbed. **Preserved verbatim in the archive as the founding artifact.** Its brand identity becomes 1.9 and Chapter 10; its plant blueprint becomes emulation reference; its open design question is answered by Chapter 5 |
| The founding rules file | Absorbed. Rules raised to specification grade; the two embedded design specifications become Chapter 4 and 5 material; the appended review material becomes Chapter 8 |
| The original Aspects of Review | Absorbed into Chapter 8 as the origin of the persona instrument |
| The concept and constitution lineage, versions 1.0 through 3.0, with their amendment sheets | Absorbed in full and distributed across Chapters 1, 3, 4, 5, 7 and 8 |
| The Product Doctrine, Part I | Absorbed. The richest of the drafts |
| The Product Doctrine, Part II | **Not absorbed.** It is a realization plan and a status snapshot, and its own text retires it as such |
| The schema topology amendment and its version 2 contract | Absorbed into Chapter 4, with the permanence of the authored relational model elevated to 1.7 |
| The low-code shell amendment | Already absorbed upstream. Retired |
| The authoring layer specification | Absorbed. Its genericity mechanism is promoted into Rule 1; its remainder becomes Chapter 5 |
| The interactive workspace doctrine | Absorbed. Its seven standards become Chapter 5; its gate becomes Chapter 8 |
| The persona amendment, the founding-documents review, the product roadmap | Absorbed into Chapters 8 and 7. Open rulings carried into 1.11 rather than closed silently |
| The identity and topology reference, and the operational command notes | Absorbed into Chapter 7, complete and unredacted, per the author's ruling |
| The document outline | The source of this document's chapter structure. Retired on ratification |

**Not absorbed, and deliberately so.** The session handovers, implementation reviews, scoreboards, validation matrices, test passes, demo playbooks, hostile-hands protocols and the product backlog are living instruments with decaying timestamps. They sit beside this document, cite it, and are regenerated per milestone. **A quality instrument folded into a constitution puts a rotting timestamp inside permanent law.**

### 1.1.5 The scope of each chapter, and the boundary rule **[N]**

| # | Chapter | Contains | Never contains |
|---|---|---|---|
| 1 | Constitution | Permanent law | Dates, measured status, milestones, implementation detail |
| 2 | Marketing and sales | Claims a buyer hears | Any claim not grounded in Chapter 1 |
| 3 | Product overview | Concept, flows, journey, page inventory, administration | Endpoint or component detail |
| 4 | General technical specification | The journey to endpoint level, the genericity mechanism, page contracts, schemas and joins, topology and credentials | Surface-level interaction design |
| 5 | Specific surface design | The five authoring surfaces, the workspace, the engine, statistics, machine learning, the assistant, concurrency and load | Anything already ruled in Chapter 1 |
| 6 | Tutorial | Click paths for a non-engineer | New requirements |
| 7 | Infrastructure, security and operations | Deployment, pipeline, testing, the sizing model, secrets, operational topology | Product behaviour |
| 8 | Quality, personas and acceptance | How the product is judged | New product scope |
| 9 | Commercial and administration | Tiers, the cost model, entitlement, roles, logging | Marketing language |
| 10 | Website | The public surface | Product specification |
| - | Appendix | Implementation Status Register, merge ledger, glossary | Law |

**The boundary rule, binding.** No chapter restates a rule that Chapter 1 already holds. It cites it. A rule written twice will drift, and the entire reason this document exists is that it drifted across more than thirty files.

### 1.1.6 The status rule and the evidence rule **[E]**

**The status rule.** No chapter states what is currently built. Implementation status lives in exactly one place, the Implementation Status Register in the Appendix, where every row carries a date and an evidence handle.

The reason is written into the drafts themselves. One specification carried a hand-maintained doctrine-versus-build table; by its own later admission, that table was false on at least four rows before anyone noticed. **A hand-maintained delta table inside a specification is guaranteed to lie.**

**The evidence rule.** Where a status claim must appear at all, it names its evidence, a gate, a report file, an audit or a reproducible command, or it is written in the future tense. There is no third option.

**The gate corollary.** A gate claimed in this document without a recorded red run is a defect in this document. Every gate is falsified once, meaning seen to fail, before it is trusted.

**A guard satisfiable by its own prose is worse than no guard.** This is not hypothetical: a parity check once compared an object with itself and displayed a green verdict, and a pipeline guard forbidding a forbidden flag was satisfied by the header comment of the file it was inspecting. A guard must read what it judges, must strip comments before judging, and must be scoped to the region it judges and no wider.

**The working principle,** carried verbatim because no shorter formulation exists:

> **Anything not written here does not exist. Anything written here without its gate is not yet true.**

### 1.1.7 Change control and the derivation chain **[E]**

An edit to this document requires the author's explicit approval and a version increment. Every edit that changes meaning is recorded in the merge ledger with its origin and its date.

Every derived artifact carries a derivation line naming this document and its version, and re-validates against it. Any claim of enforcement or build status in a derived artifact names its evidence or is written in the future tense.

---

## 1.2 What PlantProcess IQ is

### 1.2.1 The core target statement **[C]**

**PlantProcess IQ is a generic, read-only, evidence-grade process-to-quality intelligence platform for manufacturing plants in any process industry.**

It connects fragmented plant data through a customer-controlled, one-way collector. It stages source-shaped copies without interpreting them. It maps them into a canonical manufacturing model through an explicit, versioned, customer-authored mapping. It composes dashboards and widgets from data rather than from code. It runs transparent simple analysis and credible advanced analysis. It quantifies business impact as a bounded range. It generates deterministic, evidence-ranked suggestions. And it answers through a grounded assistant that explains the mathematics but never performs it.

It does all of this without replacing the customer's manufacturing execution system, level-2 tracking, supervisory control, programmable controllers or business intelligence tools, under enterprise-grade security, identity and licensing, and provably on the customer's own data.

It installs empty. Nothing inside it knows any industry.

### 1.2.2 The North Star **[C]**

Three of these sensations must land in the first demonstration; the fourth is signed against at the pilot.

| Sensation | What it means | How it is proven |
|---|---|---|
| **"It already speaks my plant."** | It understood my data, my keys, my line, without me remodelling anything | Connect a real source, map it, walk a genealogy thread from a finished unit back to its origin, on the customer's own key names |
| **"It told me something I did not know, and proved it."** | A non-obvious quality or downtime driver, with the evidence and the method shown | Run an analysis on a real defect class; surface a stable, stratified, evidence-ranked contributor with a bounded value range |
| **"It will not embarrass me."** | I can click anything in front of my manager and nothing breaks or lies | Every control works; every slow call shows progress; the assistant refuses to guess and cites every number |
| **"And it pays for itself."** | The money it saves exceeds what it costs, and I can see the arithmetic | A worked business case on the customer's own pilot data, with every input drill-throughable |

### 1.2.3 The value proposition is cross-source **[C]**

*A categorical ruling by the author. It is stated here rather than in a design chapter because it decides what the product is for.*

> **The entire value proposition rests on cross-source correlation.** Correlating one source with itself defeats the purpose.

A single production unit's own historian already shows that unit's own data. The reason a plant needs this product at all is that the parameter recorded at an early stage and the defect detected at a late stage live in different databases, under different vocabularies, owned by different teams, and nobody has joined them.

Two consequences follow.

**Scope may never be reduced to a single source.** A demonstration, a pilot or a milestone that correlates one source with itself has demonstrated nothing, however well it runs.

**This is why the join is declared once and permanently.** The cross-source identity declaration made when staged data is mapped into the plant schema is the thing that makes every downstream correlation possible. See 1.7.

### 1.2.4 What it is not **[C]**

It is not a manufacturing execution system, a level-2 tracking system, a supervisory control system, a historian, or a generic business intelligence tool. It does not replace any of them. It reads from them.

It never writes to a customer system and never participates in control. Every statement it makes about cause is framed as a suspected contributor, never as a guaranteed root cause.

The commercial case and the buyer argument are Chapter 2.

---

## 1.3 Settled trade-offs **[C]**

*Carried on the drafts' own stated principle: a doctrine shows its reasoning so that future contributors do not relitigate settled trade-offs. Reopening one of these requires an amendment under 1.1.7, not an argument.*

| Decision | Chosen path | Why, and what was rejected |
|---|---|---|
| **Where data lands first** | Source-shaped staging, mapped to canonical later | A plant will not remodel its databases for us. Rejected: forcing data into the canonical shape at import, which breaks on the first unusual source |
| **How joins are authored** | Explicit, versioned, customer-authored joins with a business-key dictionary | Real plants have no universal key. Rejected: automatic join inference, which is unauditable and fails silently |
| **Who authors the joins** | The customer's own engineer | He knows his data and its logic; the vendor cannot know the schema architecture of every customer's plant. Rejected: vendor-delivered mappings, which do not scale past a handful of plants |
| **How intelligence escalates** | Data, then information, then simple analysis, then advanced analysis, then suggestions, then the assistant | Trust is built in stages; the user sees transparent arithmetic before anything statistical. Rejected: raw data straight to a conversational agent |
| **Who computes numbers** | Deterministic engines compute and rank; the assistant retrieves, cites and explains | Every figure must be reproducible. Rejected: the language model performing arithmetic or ranking |
| **How pages are built** | Composed from page and widget definitions through the interface | One generic mechanism rather than an endpoint per chart. Rejected: bespoke pages built in source code |
| **What a demonstration is** | The real generic product running on prepared external data, built entirely through the interface | If we hardcode, we deceive ourselves and miss real defects. Rejected: a separate demonstration build or hardcoded screens |
| **Where the model runs** | A swappable gateway: self-hosted by default, a zero-retention private endpoint, or the customer's own model | Data sovereignty is not negotiable. Rejected: hardwiring one public model and sending plant data to it |
| **Multi-tenant versus on-premise** | One codebase: tenant identity plus row-level security when shared, the same code single-tenant when dedicated | Two forks diverge and double the defects. Rejected: a separate on-premise product |
| **Time-series at scale** | A time-series store with compression, downsampling and retention | Process parameters are billions of rows. Rejected: a naive row table |
| **How sources are reached** | A collector in the demilitarised zone pushing one way; change-data-capture or a replica preferred over polling | Plant IT will not permit an application to poll a live historian. Rejected: the core opening connections into the operational network |
| **Required configurator skill** | A no-code authoring surface for the common case; safe SQL for the long tail | A SQL-only product cannot scale to many plants. Rejected: requiring a SQL author for every dashboard |
| **How value is proven** | The demonstration proves the engine; a pilot proves the signal on the customer's own data | Synthetic data proves only that the machinery runs. Rejected: claiming the demonstration proves real discovery |

---

## 1.4 The Four Product Rules

These four rules are the core of the constitution. A build that violates any of them is not shippable regardless of any other quality it possesses.

### Rule 1 - GENERIC ONLY **[C]**, with the cleanliness clause **[E]**

The product contains **no line, no word, no page, no component, no schema object, no list and no code branch prepared for any specific dataset, industry, plant or customer.**

No demonstration content ships inside the product. No plant name and no company name is hardcoded anywhere; plant identity is configuration data, never code.

**The three doors, and there is no fourth.**

| Door | What enters | Who opens it |
|---|---|---|
| **Import** | The customer's rows, tables, columns, keys, defect names, parameter names, equipment names | The customer, through the read-only link and the staging pipeline |
| **Registry** | What the product will offer as a choice: which columns are dimensions, which are measures, which can be filtered, which chart types accept which | Derived from imported data plus explicit registration |
| **Authoring** | What the customer builds from those: preparations, widgets, pages, filters, analyses, rules | The customer, through the five authoring surfaces |

**The mechanism, which is structural rather than hygienic.**

> **No authoring surface may contain a list. Every list is read from a registry that the customer's own data populated.**

A product that ships with a filter called "Shift" has already assumed the customer runs shifts. A product that ships with a business purpose called "Downtime" has assumed downtime is how they think about loss. Some guesses are right for the customer you built against and wrong for the next one, and the failure is silent: the product works, and the customer simply cannot express what they actually care about.

**The configurability corollary.**

> **If it cannot be configured from the interface, it is not done.**

This applies without exception, including to the assistant: which tools it may use per role and tier, which knowledge sources are indexed, the plant glossary and its synonyms, the guardrail phrases and the verbosity.

**The four kinds of fixed value, and which are permitted.**

| Kind | Example | Verdict |
|---|---|---|
| **Plant vocabulary** | `shift`, `defectType`, `riskClass`, `heat`, `grade`, `coil` | **FORBIDDEN.** This is the customer's world, not the product's |
| **Grammar and operators** | `= <> > >= < <= LIKE IS NULL`, `+ - * /` | **REQUIRED to be fixed.** These are the product's own language and must be a closed whitelist, because that is what makes the surface safe |
| **Structural categories** | `dimension`, `measure`, `filter`, `chart type`, `widget kind` | **PERMITTED.** These are the shape of analysis itself, not of any industry |
| **Presentation tokens** | Colour, spacing, typography, radius | **PERMITTED and required to be fixed.** A design system is not customer knowledge |

**The test, in one sentence:** could a different plant reasonably need a different value here? If yes, it comes from the registry. If no, and changing it would break the product's own contract, it is fixed and closed.

`>` means greater than at every plant on earth. `defectType` means nothing at a plant that records deviations instead of defects.

**The failure shapes.** Genericity is a property of the structure, not of the contents. In the majority of observed violations the data was already generic and the structure was not. **A closed category set is a Rule 1 violation even when every value inside it is dynamic**, because a plant needing a filter on batch, recipe, tool number or ambient humidity cannot add one.

**The cleanliness clause.** A clean install is clean **by construction**, never by manual purge. Where an installer, a setup script or a seed creates demonstration-named objects, cleanliness becomes an artifact of somebody remembering to delete them, and that person will eventually be on holiday. **If cleanliness is not enforced by a gate on a fresh install, the product is not generic; the last operator was.**

**Enforcement.** A generic-only lint over the projection path; a migration-path gate that fails the build if setup scripts create demonstration-named objects; the prefilled-metadata lint of Rule 2; and the seven acceptance tests of Chapter 8. Each is falsified once before it is trusted.

### Rule 2 - THE PLANT SCHEMA STARTS EMPTY, AND THE LINK IS THE ONLY DOOR **[E]**

On day one at the customer, the **plant data schema contains zero rows.**

Every row of plant data arrives exclusively through: read-only link import, then staging, then generic projection. There is no other path.

**The one-line proof.** An auditor proves this rule with a single query returning zero across the plant schema on a fresh install.

**Taxonomy is plant knowledge, not product knowledge.** Defect catalogues, parameter definitions and every other reference vocabulary start empty and are imported from the customer's own definition tables through the same pipeline. Flat steel defects differ from paper defects differ from mineral water defects.

**Where a source has no definition table**, and many do not, the customer's own database administrator exposes a read-only view in the source database. That is what a real plant administrator does, it keeps the product strictly read-only, and it preserves the rule: taxonomy still arrives through the one door. This is a per-source matter, not a universal one; some sources ship a real catalogue table.

**The metadata prefill contract.** The metadata schema ships with content, because a product whose roles, layouts, widget catalogue, toolbox and licence structures arrived empty would not start.

- **Every metadata table declares, in the schema definition itself, whether it is empty-on-install or prefilled-on-install.**
- **Every prefilled row ships from a versioned script.** See the reproducibility law under Rule 4.
- **Every prefilled row passes the generic-only lint before it ships.** A shipped dashboard definition whose widget groups by a defect type or a shift has placed one industry's vocabulary inside the install image, and it will pass every other check because it is data rather than code.

**The only pre-populated identity class** is site and plant identity, the licence artifact, and the vendor support account.

**The two-admin rule.** The vendor support account is auto-provisioned at first run, is undeletable, is used by the vendor for support, and is never seen by the customer. The customer's own administrator is created as a manual commissioning step and is never baked into the install image. Development-seed and test accounts are absent from every production path.

**Out-of-band writes are prohibited** in any documented workflow. Administrative resets are product endpoints that write audit records, never direct database statements.

### Rule 3 - THE JOURNEY IS THE PRODUCT **[C]**

The canonical journey specified in Chapter 3 is the acceptance specification.

A milestone is complete when the journey's steps hold. A demonstration shows journey steps working, never staged substitutes.

**There is exactly one journey.** A second journey written anywhere, in a tutorial, a playbook, a deck or a backlog, is a second acceptance specification and is deleted rather than reconciled. Chapter 6's tutorial expands the journey's steps; it never renumbers or replaces them.

**Honesty over spectacle.** Anything not real is stated as roadmap in one scripted sentence, and nothing else is hedged. **A deliberate cut is a decision, not an omission, and it is written down** with the sentence that will be spoken if a customer asks.

### Rule 4 - THE LATEST CONCEPT IS THE ONLY CONCEPT **[E]**

Where code, a schema object, a surface or a document carries an older, immature or superseded concept, it is **deleted, cleaned or corrected. It is never built upon.**

The project began as a backbone and a skeleton. The design and the concept were then enhanced, repeatedly. Work sticks to the latest concept and removes what the older one left behind. Building on superseded work leaves a growing accumulation of retired classes and abandoned concepts that mislead whoever reads the codebase next, and that reader is usually the author six months later.

**Four consequences, each binding.**

1. **A replacement lands together with the deletion of what it replaces, in the same change.** Not in a follow-up task, not in a cleanup milestone.
2. **A superseded implementation is never preserved behind a configuration flag, a request parameter or an unreferenced class.** If it can be reached at all, it can produce results that the current governance never sanctioned. Stored results from a retired implementation persist in the database long after the code is dead, under its own vocabulary, indistinguishable to a later reader without an archaeology exercise.
3. **Mounting a control on top of old-concept code makes a previously unreachable violation reachable.** Dead code that violates Rule 1 is a debt. The same code with a button in front of it is a defect a customer can open.
4. **The reproducibility law: a fix that exists only as data does not exist.** A correction applied by hand to a running database will not survive a rebuild, will not appear in the other environment, and will never reach a customer. **Every data correction is paired with the script change that makes it reproducible, in the same commit.**

**The naming corollary.** No artifact name contains a phase code, a task code, a version code or a bookkeeping label. Names describe content only; version is a separate field. **A file whose name claims to fix what it in fact breaks is a violation of this rule**, and it is corrected by renaming, not only by fixing.

**Rule 4 is the parent of three laws stated elsewhere:** the Single Engine Implementation Law, the schema eradication clause, and the naming rule above.

---

## 1.5 The Honesty Contract **[E]**

The product's principal competitive advantage is not that it computes. Competitors compute. The advantage is that it **refuses to compute when the data cannot support a defensible answer, and says exactly why.**

This is the moat. It is the first thing a technical evaluator tests and the last thing a competitor copies quickly, because copying it requires being willing to show a customer a red status.

### 1.5.1 Abstention is a first-class result **[C]**

An analysis that cannot be defended must abstain, name the dimension that blocked it, and state the measured value against the required threshold. A blocked run is a successful product behaviour. It is recorded with a real run identifier and is fully explainable from the database alone, without the application.

### 1.5.2 No gate is ever weakened to produce a result **[C]**

Readiness thresholds, refusal logic and evidence requirements may be **tuned by a human being through a governed configuration change with a recorded justification.** They may never be lowered to make a demonstration greener, to make a job pass, or by any automated process including the Supervisor.

Any change to a threshold is a provenance record naming who changed it, from what, to what, and why. **The honest-abstain machinery is outside the Supervisor's write scope by construction, not by convention.**

### 1.5.3 No number without resolvable evidence **[C]**

Every number the product displays traces to a query, a definition and a data population. Every claim carries an evidence handle resolving to a finding, a job run, a dataset, a source table or a document section. **A claim without a resolvable handle is not rendered.**

### 1.5.4 Deterministic engines compute, the language model only explains **[C]**

All arithmetic, ranking, correlation and scoring is performed by deterministic engines. The language model retrieves, cites and explains. It never computes, never ranks and never originates a figure.

A smaller or larger model changes how helpfully the product explains. It never changes whether the numbers are right.

### 1.5.5 Every finding carries its framing **[C]**

Findings are persisted with their honest framing attached **as data, not as interface copy.** The stored framing states that the result is a statistical association and not a guaranteed root cause, records the method selected, and records that no language model participated in the compute path.

Framing that lives only in the presentation layer is lost in an export, a report or a screenshot.

### 1.5.6 Honest empty states everywhere **[C]**

An empty result is displayed as an empty result. There is no fabricated content, no placeholder chart and no sample data rendered as though it were the customer's.

A filtered-to-empty state is distinguished from a genuinely-empty state, and tells the user what to relax.

Every surface has a designed empty state naming what to do next, rather than a blank area. An empty state is where a new user spends their first minute.

### 1.5.7 Every refusal carries a sentence **[E]**

Wherever the product declines to do something, it states **what it refused, why, and what would satisfy it**, in a sentence written for a plant engineer.

- A red outline with no sentence beside it is a failure of this specification, not a minor styling gap.
- "Invalid input", "Invalid query" and a bare status code are not acceptable output from a product that sells its willingness to say no.
- A refused wire names the rule it broke. A refused statement names what it refused and echoes the offending fragment. A blocked analysis names the dimension, the measured value and the threshold. **An unservable request returns a named error listing what can be served, never an empty success.**
- Every refusal is persisted with its reason, so that a refusal can be audited later exactly as a result can.

**The corollary: illegal states are unreachable rather than merely rejected.** The interface must not offer a choice the server would refuse. Where the interface presents an operator list, that list is identical to the server's whitelist and a test asserts that the two cannot drift.

**The silent-success prohibition,** stated separately because it is the most expensive failure this product has suffered. A capability that is declared, published, accepted by the validator, offered in the interface and then falls through to an empty result **answers with a success status and no data**, and no test can see it. Every declared capability is either implemented and served, or it is not declared.

### 1.5.8 Statistical honesty discipline, always on **[C]**

| Discipline | Rule |
|---|---|
| **Effect size first** | Rank by effect and impact, never by p-value. A tiny effect with a small p-value is not a headline |
| **Multiple-testing control** | False-discovery-rate control across every parameter scan. Report q-values, not raw p-values |
| **Confounding control** | Stratify by every visible confounder and report whether the finding survives. Where a likely confounder is unmeasured, say so |
| **Stability** | Bootstrap the ranking. Flag any contributor that does not survive resampling |
| **Method named** | The statistic is chosen to fit the data, never forced, and is always named with the finding |
| **Restraint** | A weak, non-significant association is **not surfaced**. A tool that surfaces it anyway generates the false leads that destroy an engineer's trust. Restraint under statistical discipline is a feature |

### 1.5.9 Forbidden and approved language **[C]**

*Originates in the compass. Enforced by lint across the product interface, the website, every report and every deliverable. A single forbidden claim anywhere is a critical failure regardless of the strength of the surface carrying it.*

| Forbidden | Approved |
|---|---|
| Guaranteed root cause | Suspected contributor; likely contributor; evidence-ranked factor |
| AI-powered prediction; production-ready AI | Machine-learning readiness; statistical learning; risk scoring; correlation analysis |
| Live support for a connector that is not proven | Connector capability and availability shown honestly per source |
| We replace MES, level 2, SCADA or business intelligence | We connect around existing systems and turn fragmented data into intelligence |
| Autonomous optimisation | Decision support and guided investigation with human approval |
| It will save you X | A bounded range, with every input drill-throughable |

**A catalogue entry is not a capability.** A source system named in a dropdown is a metadata tag; a connector is an implementation. Where a connector does not exist, its entry is visibly unavailable and badged as planned, and the catalogue is served by the backend so that the interface cannot invent one.

### 1.5.10 Every customer-visible string is written for the customer **[E]**

No internal engineering note, certification condition, task reference or developer shorthand ever reaches a surface a customer can see. This has failed in practice: connector descriptions once shipped internal certification conditions to buyers as product copy.

**And a label must match what it shows.** Where a chart is repointed at different data, its title is changed in the same edit. A widget whose title says one thing while it plots another is worse than a broken one, because a broken one is visibly broken.

### 1.5.11 The three enforcement mechanisms **[C]**

| Mechanism | What it does |
|---|---|
| **Provenance handles** | Every claim carries a handle resolving to a finding, run, dataset, source table or document section |
| **The no-fabrication guard** | The assistant assembles answers only from tool results and retrieval. Any sentence containing a number without a handle is rejected **before display** |
| **The anti-facade rule** | Any seeded or emulated row is tagged at the data layer and is never presented as live computation. Learning jobs recompute on demonstration data, so a demonstration proves the engine rather than a fixture. Where emulated data reaches a surface at all, it carries a visible disclosure badge |

---

## 1.6 The Emulation Doctrine **[C]**

The emulated factory is a stand-in for a customer's databases and it lives **outside the product**.

**A demonstration is not a separate application or an extra layer.** In the author's own words, it is the first release, running on a pre-prepared dataset. Nothing is hardcoded: the links, the jobs, the pages, the widgets and their bindings are all created through the interface exactly as a customer would create them. **If we hardcode some pages we will fake ourselves**, and we will miss the defects a real user would hit.

**Test data, including deliberately planted statistical relationships, is placed in the emulated source and never in the product.** A planted relationship, together with a null control that must not produce a finding, exists so that the Engine can be proved to discover it blind, after import, having been told nothing.

Emulation assets are versioned, reproducible and stored durably, never on a single machine. Mappings for emulated sources ship as fixtures outside the product, never as code branches.

**There is no demonstration build, no demonstration branch and no demonstration code path.** Where a second database is maintained for demonstration purposes, it is selected by a launch profile, never by a branch.

**The honest scope of a demonstration.**

> **The demonstration proves the machine. The pilot proves the money.**

Where a planted signal is recovered, the honest claim is that the product recovered a planted validation signal and rejected a null control, which validates the **method**. Return on investment is what the pilot measures. Claiming that a demonstration proves real discovery is a forbidden claim under 1.5.9.

**The disclosure obligation.** Where a demonstration runs on emulated data, that fact is stated aloud at the start, once, early and unprompted, and is visible in the interface through the disclosure badge.

---

## 1.7 Platform boundaries and non-negotiables **[E]**

| Boundary | Statement |
|---|---|
| **Read-only toward the customer** | The product never writes to customer systems and never participates in control. No setpoint, recipe or command ever flows back. Any write-back path is an automatic critical failure regardless of every other strength. **This is a security and trust feature, not a limitation: it is why the plant's automation team can approve the product without a control-systems risk review** |
| **Outbound is message, export or webhook only** | Notifications, case and action tracking, and a read-only export interface for the customer's own tools are permitted. Never a command |
| **Acquisition safety** | Sources are reached through a customer-controlled collector that pushes one way. The core never initiates a connection into the operational network |
| **Source-load protection** | Per-source row caps, statement timeouts, rate limits and approved time windows. Historical backfill is throttled, idempotent, checkpointed, pausable and resumable, and honours a stated source-impact budget |
| **Honest statistics** | The discipline of 1.5.8, always on, with no surface permitted to bypass it |
| **Provenance everywhere** | Every canonical row carries source system, source record identifier and import-batch lineage. Genealogy attribution weights sum to exactly 1.0 per child, enforced at the database level. A synthetic-data flag separates emulation from production data |
| **The authored model is permanent** | The joins, keys and links a user declares when moving staged data into the plant schema are not a transient import mapping. They are the product's model of that plant, and they persist through every downstream stage for the life of the installation. They are versioned, inspectable, exportable and must survive any schema migration. A wrong join is wrong forever, silently, across every surface, which is why illegal wiring is refused at authoring time rather than at run time |
| **Schema isolation** | No analytical surface may display a row that did not come from the plant schema. A widget that could read staging would show a customer their own unmapped source columns and call it intelligence, and **it would look like it was working**. The mirror clause holds too: an administration surface displaying plant data has leaked production data into a screen where role scoping may not apply |
| **Two distinct downtime quantities** | Equipment-stopped minutes and production-impact minutes are different quantities and are never interchanged. A twenty-minute stoppage upstream may be absorbed entirely by buffering and cost no production; a three-minute stoppage at a casting machine may force a sequence rebuild and cost hours. The correct quantity is used per calculation and both are stored |
| **Data boundary** | Deterministic engines compute inside the tenant. No plant data is sent anywhere to be calculated. What may leave in private-endpoint mode: the question text, the specific retrieved evidence, and the tool schemas. What never leaves: credentials, full source or staging tables, another tenant's data, or anything beyond permission-scoped retrieval. A per-tenant no-egress control forces self-hosted serving. Every call is audited |
| **Tenant isolation is absolute** | Every row carries a tenant identity and row-level security enforces that no query, interface call or assistant retrieval crosses a tenant boundary. One resolver and one rule set serve both the shared and the dedicated topology |
| **One codebase, four topologies** | Vendor-hosted, customer-cloud, on-premise and air-gapped. Logical isolation when shared, physical when dedicated. Never a second product |
| **Identity and secrets** | Passwords hashed with a modern memory-hard function. Access tokens in memory only with a rotating refresh cookie, never in browser storage. Multi-factor authentication enforced for administrators. Source credentials in an encrypted vault, masked on read-back |
| **Entitlement integrity** | Entitlements derive only from a signed, offline-verifiable token, never from an editable database row. A client-supplied tier override is ignored by design |
| **Expiry never destroys data** | Clear pre-expiry warnings, a configurable grace period, then read-only access to existing dashboards. Customer data is never destroyed by expiry |
| **Accessibility and internationalisation** | WCAG AA. Right-to-left rendering for Arabic. Units and time zone per user. UTF-8 throughout with explicit date and number formats |
| **Direction neutrality** | Every layout is expressed in logical properties. **No component name, class name or property name encodes a physical side** |
| **Compliance** | Lifecycle controls evidenced. Audit trail and electronic signature where the regulated industries require them |

### 1.7.1 The capacity metering law **[N]**

*New. Present in no draft. It exists because the licence tables in the drafts metered object counts, and object counts are not cost.*

> **Entitlement is metered on capacity consumed, never on object counts.**

A count of connections, jobs, users or pages is a proxy that does not correlate with what the platform must actually carry. Three connections importing one hundred tables of one hundred million rows every minute cost more, by orders of magnitude, than one hundred connections importing a thousand rows a day. A licence that meters the first as smaller than the second has mispriced itself and has committed the vendor to an infrastructure bill it did not sell.

**The metered dimensions are the ones that drive machine cost:**

| Dimension | What it drives |
|---|---|
| **Retained volume** | Storage, index size, partition count, query cost |
| **Ingest rate** | Import compute, staging churn, storage growth, source load |
| **Minimum refresh interval** | Scheduler pressure and source-impact budget. A tier sets the floor on cadence |
| **Concurrent compute slots, by job class** | Worker pool sizing. Statistical and model-based classes are weighted differently because they cost differently |
| **Concurrent interactive sessions** | Query load on the read path |

**Object counts remain as soft guardrails.** They warn, they upsell, and they never block work in progress. They are commercial packaging, not the protection.

**Three clauses follow, and each is binding.**

1. **The tier table and the sizing model are one model, not two.** Every tier maps to a stated server class, and every limit in that tier is derived from what that class can carry. A tier that does not map to a server class is a promise the vendor cannot support.
2. **Exceeding a meter throttles; it does not destroy.** An import queues, a job waits for a slot, a warning is raised and an upgrade path is offered. Silent data loss and a hard stop mid-work are both prohibited.
3. **Every meter is measurable in the product and visible to the customer.** A limit the customer cannot see themselves approaching is a trap, and it will be discovered as a surprise invoice or a surprise outage.

The formula itself, the class definitions and the tier envelopes are Chapter 9, with the server-side derivation in Chapter 7.

---

## 1.8 The Readiness Gate as constitutional principle **[C]**

*The five dimensions, their thresholds and the gate's implementation are Chapter 5. What follows is the part that is law.*

Before any analytical job computes, the dataset it would compute on is evaluated against **named dimensions with published thresholds.** If any dimension fails, the job abstains, records a run with a blocked status, and names the failing dimension with its measured value.

Six clauses are constitutional.

1. **The overall verdict is the worst state across all dimensions.** Not an average, not a majority.
2. **The compute engine and the live readiness endpoint call the same evaluation function.** The verdict a user sees can never drift from the verdict the engine acts on. Any second implementation violates Rule 4.
3. **Every gate result carries an evidence string** naming the outcome, the grain and the window, so that the verdict is reconstructable from the database alone.
4. **The product never shows nothing while a gate is blocked.** It shows the simple analysis that needs no history, the readiness meter with its measured counts, and an honest collecting-data state.
5. **The maturity curve is stated to the customer before the sale, not after it.** Simple dashboards and key figures work on day one with no history. Mapping and genealogy stabilise within the first weeks. The first advanced findings arrive as gates turn ready. Mature suggestions and value ranges follow. A historical backfill collapses the timeline.
6. **The gate is shown to customers deliberately.** Four green dimensions with real measured numbers and one honest red naming a specific data deficiency is a stronger demonstration than a fabricated result. No competitor in this market shows a prospect a red status. That is the point.

---

## 1.9 The design system **[E]**

*Originates in the compass, which specified it in full. Reconciled here because a later document restated it with different values, and it is now stated once and cited by Chapters 4, 5, 7 and 10.*

### 1.9.1 The colour tokens

Dark Industrial Command Center. Not a dark theme applied to a light product. A control-room aesthetic, because that is the room this software is used in.

| Token | Value | Use |
|---|---|---|
| Deep Navy Black | `#050B18` | Full-bleed application background, website hero |
| Panel Navy | `#0B1730` | Panel and card background, navigation bar, modals |
| Industrial Blue | `#102A43` | Raised surface, sidebar, table header rows, section dividers |
| Electric Blue | `#0A84FF` | Primary action, active navigation, badge fill |
| Electric Cyan | `#00D4FF` | Accent, focus ring, active border, primary chart series, hover glow |
| Corporate Blue | `#2F80ED` | Secondary action, info badge, secondary chart series |
| Cyan Green | `#2CE6A2` | Success, readiness green, running job, possible values in the associative state |
| Amber | `#FFB020` | Warning, partial readiness, pending state |
| Hot Red | `#FF4D6D` | Error, blocked gate, refusal, failed job |
| Near-White | `#EAF6FF` | Primary text, headings, values |
| Text Secondary | `#C9DCEC` | Body text |
| Muted Steel | `#8EA7C1` | Captions, metadata, timestamps, placeholder and inactive text |
| Report Surface | `#F4F6F8` | Generated reports, print and email-friendly surfaces |

**Port colours, by data type**, used on the authoring canvas:

| Port type | Value |
|---|---|
| Key | `#00D4FF` |
| Number | `#0A84FF` |
| Text | `#7AA7C7` |
| Date | `#B48CFF` |

**The colour communicates the type. The type enforces legality.** A colour that does not correspond to an enforced rule is a lie told by the interface.

**Status badges:** running or healthy uses Cyan Green, warning uses Amber, failed uses Hot Red, planned uses Muted Steel.

**Tier badges:** Light uses Muted Steel, Pro uses Amber, Pro Plus uses Electric Blue, Enterprise uses Cyan Green.

All chart palettes derive from these tokens and are colourblind-safe. **Colour is never the only signal of a state.**

### 1.9.2 Typography

| Role | Family |
|---|---|
| Display and headings | Chakra Petch, a squared industrial face |
| Body and interface | System sans |
| Numbers, identifiers, SQL and grammar | IBM Plex Mono |
| **The wordmark** | **Open. See ledger item 15** |

**Monospace for data is a legibility requirement, not a style choice.** An engineer comparing identifier values or reading a compiled query needs digits and identifiers to align.

Minimum body size is 14 pixels on screen and 12 point in print. Heading scale is 48 / 36 / 28 / 20 / 16 on the web surface and 24 / 18 / 14 / 12 in the application.

### 1.9.3 The logo system

A connected-node or hexagonal mark: four to six nodes with one dominant central node, joined by clean geometric lines, referencing both process steps and data topology.

**Industry-neutral by requirement.** It must work for aluminium, pharmaceutical, paper and automotive equally. Steel-specific imagery is prohibited, because a mark that names one industry contradicts Rule 1 on the first screen a buyer sees.

Variants: full horizontal, icon, stacked. Each in full colour, dark, light and monochrome. Never rotated, recoloured outside the palette, stretched, shadowed, boxed, or placed on a low-contrast background.

### 1.9.4 The style laws enforced by tests

These are not conventions. The repository fails the build on each.

| Rule | Nature |
|---|---|
| No raw button or table element where a standard primitive exists | Repository gate |
| No raw input, select, textarea or label element outside the primitive layer | Ratchet |
| No inline style objects | Ratchet |
| No raw load-failure strings outside the shared error boundary | Repository gate |
| No phase or task tokens visible on any customer-facing surface | Repository gate |
| No encoding corruption | Repository gate |
| No thin re-export chains | Repository gate |
| No physical side encoded in a component, class or property name | Repository gate |

**The ratchet is a baseline comparison, not a zero rule.** An existing file may carry its committed count. A decrease passes. **Any new file starts at zero.** The allowlist may shrink and may never grow.

**Why a design-system ratchet exists at all:** a product priced at several thousand euro a month cannot have three different table styles on three pages. The primitives are the mechanism, and the ratchet is what stops the mechanism eroding one convenient exception at a time.

### 1.9.5 What "professional" means, concretely

| Property | Test |
|---|---|
| **Consistency** | The same control looks and behaves the same on every page |
| **Density** | Information-rich without crowding. An engineer scanning for a number finds it |
| **Grouping** | Long lists are grouped and collapsible, never a flat wall |
| **Alignment** | Shared baselines. Nothing half a line out |
| **No raw machine values** | No machine timestamps, no internal identifiers, no enumeration names, no status codes on a customer surface |
| **Responsive** | Nothing clips, overlaps or collapses below its content |
| **Predictable** | Escape closes, Enter submits, Back returns, reload preserves |

**Nothing on screen is decoration.** If a control appears, it works. If a colour carries meaning, that meaning is enforced. If a badge shows a state, the state is real.

---

## 1.10 The quality bar **[E]**

### 1.10.1 The severity doctrine **[C]**

*The author's own words, stated twice. It is placed first in this section because it is the reason every rule below it exists.*

> Anything that causes a customer to lose trust and kills the deal **is a bug**. If a chief executive sees the word "Demo", the deal is dead. If they see a cluttered interface, the deal is dead. **Treat interface clutter and naming violations with exactly the same severity as a server error.**

**Severity is measured by the trust it costs, not by the function it breaks.** Therefore, and without exception:

| Class | Severity |
|---|---|
| A server error on a demonstrated path | Highest |
| A demonstration name, phase token or internal string visible to a customer | Highest |
| Encoding corruption on any customer-visible surface | Highest |
| A placeholder or developer-written string reaching a customer | Highest |
| A control that is present and does not perform its function | Highest |
| A styling mismatch between two controls that should match | Highest |
| A flat, ungrouped list where the product looks unfinished | Highest |

A defect register that sorts by technical severity and puts these at the bottom has inverted the product's own priorities.

### 1.10.2 No false dichotomies **[C]**

> In enterprise business-to-business at this price point, customers do not choose between a working backend and a professional frontend. **They demand both.**

Sequencing that trades one against the other is rejected. Data correctness and interface quality are worked concurrently, not in phases. This is stated as law because it is the argument that will otherwise be made every time a deadline compresses.

### 1.10.3 Justify every click **[C]**

> There is no such thing as "just keep moving" or accepting placeholders. **Every click, every label and every step must be fully justified,** because it has to be defended live in front of a customer.

This is the test for whether a control should **exist**, which is a stricter test than whether it works. A control that works, that nobody can explain the purpose of, fails this rule and is removed rather than kept.

### 1.10.4 The standing instruction **[C]**

**No error, no defect, no crash and no non-functional control is acceptable in front of a customer.**

Specifically prohibited: a panel that reports it could not load and then works on retry; a styling mismatch between one page's controls and another's; a control that is present and does not perform its function.

**The no-partial-credit rule.** A gate item is between ninety-five and one hundred percent complete, or it is not done.

**The demonstration path is sacred.** Every control on the demonstration path works, or the path is not ready. A control that is present but non-functional is worse than an absent one, because it invites a click that fails in front of the buyer.

**The lowest-score rule.** The product is judged from independent professional vantage points, and **the headline result is the lowest of them, never an average.** The build ships when every reviewer can sign, not when they average well.

### 1.10.5 The three-contracts rule **[C]**

Every capability has three matching contracts:

| Contract | What it is |
|---|---|
| **Backend contract** | The data and the endpoint |
| **Frontend contract** | The workflow and its states, including empty, loading, error, filtered-to-empty and refused |
| **Validation contract** | The proof test |

**A feature is complete only when all three are green.**

### 1.10.6 Definitions of done, and the verification law **[E]**

**The verification law.** Compiling is not done. Gates passing is not done. **A browser walk is done.** Code-verified and gate-verified are intermediate states and are named as such; only a surface a human has operated is verified. Every claim of completion states which of the three it is.

| Class | Bar | Verified by |
|---|---|---|
| **Presentable** | Every journey step can be **shown working through the interface**. Screens and a working path suffice. Accuracy depth, concurrency and full role and licence enforcement are not required. **Nothing shown is fabricated** | A walked demonstration path |
| **Hardened** | This entire document holds completely, executable end to end by a person who did not write it, following the runbook | A walk by someone who did not build it |
| **Customer-shaped** | Scope re-prioritised from customer feedback within forty-eight hours. Multi-industry proof: a second emulated industry ingests through the identical journey with zero application changes | A second industry walked end to end |

---

## 1.11 The ruling ledger **[E]**

*Per 1.1.2, a record of learning. Each entry names the question, the ruling, and the earlier formulation the ruling retires.*

### 1.11.1 Ruled

| # | Question | Ruling | What it retires |
|---|---|---|---|
| 1 | Is SQL authoring gated by licence tier? | **Yes, from the second tier upward.** A role gate applies in addition at every tier: a viewer never authors SQL | The clause that neither mode may be tier-gated |
| 2 | May arithmetic and comparison blocks sit on the authoring board? | **No, on every surface without exception.** A wire on the board carries a dataset; a wire inside an expression editor carries a value. Expression blocks live inside the board block they configure. One board grammar, one validator, one error taxonomy | The split that permitted values on some boards |
| 3 | How does a user bind data to a widget? | **The kind picker is a pre-step; the shared shell is where binding happens.** Catalogue binding is a simplified face of the same shell, not a second surface | The reading that these were competing designs |
| 4 | What exactly starts empty? | **The plant schema, provably, in one query.** The metadata schema ships content under a declared per-table prefill contract, from versioned scripts, each row passing the generic-only lint | The claim that identity is the only pre-populated class |
| 5 | Which typefaces? | **Chakra Petch for display, system sans for body, IBM Plex Mono for data** | Inter and JetBrains Mono for the interface |
| 6 | Which colour tokens? | **The thirteen-token set of 1.9.1**, with Muted Steel at `#8EA7C1`, plus the four port colours | The nine-token subset and the value `#8ba9c4` |
| 7 | Which currency? | **Euro** | The dollar figures of the founding document |
| 8 | How many application schemas? | **Three**, plus the database platform's own schema restricted to platform infrastructure | The two-schema statement of the founding document |
| 9 | Which side is the schema browser on? | **Inline-start**, mirroring in right-to-left locales. Never expressed as left or right in any name | Both contradictory statements in the founding document |
| **10** | **How are licence limits metered?** | **On capacity consumed, never on object counts.** Retained volume, ingest rate, minimum refresh interval, weighted concurrent compute slots and concurrent sessions are the meters. Object counts remain as soft guardrails that warn rather than block. See 1.7.1 | **Both draft tier tables, which metered users, sources, jobs and pages as though those were cost** |
| **11** | **Where does the assistant start?** | **Pro Plus.** Machine learning, prediction, recommendation, the value engine and the assistant all begin at Pro Plus. **Enterprise sells on deployment and integration instead:** all connectors, on-premise and air-gapped operation, self-hosted model, single sign-on and provisioning, branded reports and contracted support | The placement of the assistant at Enterprise only |

### 1.11.2 Open, awaiting ruling

| # | Subject | Positions | Recommendation |
|---|---|---|---|
| 12 | **The tier capacity envelopes** | The numbers behind ruling 10 | Pending. To be proposed with Chapter 9, derived from the sizing model rather than chosen |
| 13 | **The role catalogue** | Three roles extensible, or eight named roles | The eight-role catalogue as the shipped default, with the three-role minimum retained as the smallest legal configuration |
| 14 | **The logging layers** | Three types, or four layers, with two different fourth layers proposed | Four: system, job, data, audit. The founding-documents review already recommends ratifying the four-layer model as the successor |
| 15 | **The wordmark typeface** | The compass specifies the wordmark in Inter Bold; ruling 5 moved the interface to Chakra Petch | **(A)** Keep the wordmark in Inter as a locked brand asset, which is normal practice, or **(B)** redraw it in Chakra Petch. Recommend (A) |
| 16 | **The transit schema name** | Staging, dump store | **Staging.** "Dump" reads as discardable, and this schema carries the cursor watermarks and batch lineage on which every provenance claim depends |
| 17 | **The first surface's output artifact** | Mapping Definition, Transformation Definition | **Transformation Definition**, everywhere |
| 18 | **The word for imported, standardised data** | Canonical, raw data, the standard tables | **Canonical.** Calling the standardised plant schema "raw" makes "raw" mean two opposite things, since the genuinely raw copy sits in staging |
| 19 | **The authoring surface numbering** | UI-1 to UI-4, S1 to S5 | **S1 to S5**, five purposes. The UI-n numbering counted four because two purposes shared a journey step |

---

## 1.12 The working principle **[C]**

> **Anything not written here does not exist.**
>
> **Anything written here without its gate is not yet true.**
>
> **The demonstration proves the machine; the pilot proves the money.**

---

*End of Chapter 1.*

*Chapter 1 is permanent. It may be extended but not weakened. Every later chapter re-validates against it and cites it. Where any chapter, document, comment or conversation conflicts with this chapter, this chapter wins.*
