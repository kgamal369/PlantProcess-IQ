# PlantProcess IQ - Master Design Document

**Version 4.3 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

# CHAPTER 1 - MARKETING AND SALES

> **Target audience (1.4):** the chief executive of a manufacturing group or plant, and the purchasing function that runs the commercial review.
>
> **Voice (1.5):** marketing and sales. Industrial, intelligent, trusted, calm, evidence-based. Plant operations plus data science, never consumer technology.
>
> **Governing constraint.** Every claim here is bound by the Honesty Contract and the forbidden-language table of the Rules. Two rules apply to this chapter specifically and are enforced in 1.11:
>
> 1. **No claim without a designed capability behind it.** Every commercial promise carries a traceability reference to the chapter and section that specifies how it is delivered. Marketing never leads engineering.
> 2. **No unbounded claim.** No fixed timeline, no universal statement about competitors, no promise whose truth depends on data the customer has not yet supplied. Capability statements are bounded by readiness, stability and governed review.

---

## 1.0 A note on the examples in this chapter

Some examples below name a material, a defect, a grade, a route or a piece of equipment, because an argument made entirely in abstractions does not persuade a plant manager.

**Every such term is illustrative only.** The product ships with no defect vocabulary, no grade list, no route model, no equipment names and no parameter names. All of it arrives with the customer's own data through the import pipeline, or is authored by the customer in the product's own surfaces. This is Rule 1 of the Rules chapter, and a sales conversation implying otherwise misrepresents the product.

---

## 1.1 The problem

### 1.1.1 One condition, every process plant

Every large process plant records what happens to its product many times, in many systems, under many vocabularies. One stage in one database, the next in another, inspection in a third, the laboratory in a fourth, spread across lines and sometimes across sites.

Each system is correct on its own. **Nothing joins them.** Every question that costs the plant money lives exactly in the join.

### 1.1.2 What that produces on the shop floor

| Symptom | What it looks like in the plant |
|---|---|
| **Repeated quality defects with unknown origin** | The same defect class returns month after month. Everyone has a theory. Nobody has the joined data to test one, so troubleshooting is long and the conclusion is "probably" |
| **Repeated downtime with unknown root cause** | The same equipment stops again and again. The maintenance record says what stopped, never what upstream condition drove it there |
| **Repeated equipment and operation failures** | Failures recur under conditions nobody has connected, because the conditions are recorded in a different system from the failures |
| **Key figures nobody knows how to move** | Productivity, yield and availability are measured and reported. What to change in order to improve them is not known, so improvement is attempted by opinion |
| **Time lost to troubleshooting** | Weeks of manual extracts per investigation, repeated at each recurrence, producing at best a partial improvement |

### 1.1.3 The expert problem

When an investigation matters enough, the plant does one of two things.

**It calls its own veteran** - the one person who holds the whole process in their head and can connect a late-stage defect to an early-stage deviation from memory. Rare, expensive, and a single point of failure. When they are unavailable the investigation waits. When they leave, what they knew leaves with them.

**Or it buys an external expert** - a consultant or vendor specialist who arrives, studies the case at a high daily rate, delivers an opinion on that one problem, **and leaves, taking everything he learned about the plant with him.** The next problem starts from zero. The plant paid for a conclusion, not a capability. Nothing accumulated.

Both share one defect: **the learning does not stay, does not grow and does not scale.**

### 1.1.4 The unexploited asset

The plant already owns what is required. The data exists, collected for years at real cost, by systems already paid for. It sits in databases nobody joins, answering questions nobody can ask.

**The plant is not short of data. It is short of the expert who can read all of it, every day, and does not leave.**

### 1.1.5 What plants try instead

| Approach | Why it stops working |
|---|---|
| Spreadsheets and manual extracts | Works once, for one question, by one person. Does not survive that person's absence and cannot be scheduled |
| A generic business-intelligence platform | Gives excellent authoring freedom over data **you have already joined and modelled**. The joining, the plant model and the intelligence remain your problem |
| A bespoke data-science project | Solves one question at consulting rates and leaves an artifact nobody can extend |
| Waiting for the MES vendor | Deepens commitment to one stack whose incentive is its own data, not the other five systems |

None of these is foolish. All are what a competent plant does in the absence of anything better.

---

## 1.2 The aim of the software

### 1.2.1 The positioning statement

> **PlantProcess IQ is the in-house expert that learns your plant's own fingerprint - and stays.**

Not a dashboard, not a consulting engagement. A resident intelligence layer that connects the data the plant already owns, builds a model of that specific plant, and keeps that model as an inspectable, governed asset belonging to the customer.

### 1.2.2 What "learning the plant fingerprint" means technically

The phrase is commercially strong, so its technical meaning is bounded here and a salesperson may not extend it. **The fingerprint is not autonomous learning. It is a set of governed, inspectable, customer-owned assets**, each of which can be opened, audited, exported and rolled back:

| Asset | What it holds | Specified in |
|---|---|---|
| **The plant model** | The entity model of this plant as imported and mapped: sites, areas, units, equipment, inspection devices, materials and aliases, genealogy, routes, steps, operations, executions, parameters, quality events, defect classifications, equipment states, downtime and impact events, maintenance events, specifications, operating limits and imported taxonomy | Ch2 3.14; Ch3 4.5.4 |
| **The plant relationship model** | How this plant's sources join: entities, keys, cardinality, grain, attribution, preferred paths | Ch2 3.15; Ch3 4.5.10 |
| **Customer-authored definitions** | Every transformation, page, widget, filter, measure, analysis, model, practice and log rule the customer created, versioned | Ch3 4.5.11 |
| **Feature and outcome history** | The assembled history the analytics read, with lineage to source rows | Ch3 4.5.12 |
| **Practice statistics** | Which operating practices coincided with which outcomes, with support counts | Ch3 4.5.12; Ch4 5.6.4a |
| **Model versions** | Every trained model with its features, window, split policy and metrics | Ch3 4.5.12 |
| **Accepted and rejected suggestions** | What the plant chose to act on and what it declined, with reasons | Ch3 4.5.12 |
| **Prediction outcomes** | What was predicted, what actually happened, whether the prediction held | Ch3 4.5.12 |
| **Drift observations** | Where the data, the model or the operating practice moved, and when it was reviewed | Ch3 4.5.12 |

**Three limits, stated so they are not oversold.** Nothing in that list changes itself: model and threshold changes require governed review and human approval. Nothing is a black box: every asset is readable and exportable. And nothing is ours: the assets live in the customer's own database and survive removal of the product.

**The approved bounded capability statement:**

> As validated history grows, the platform can build a progressively richer model of the plant, subject to readiness, stability and governed model review.

### 1.2.3 The puzzle

The plant's data is a puzzle whose pieces sit in separate systems and sometimes separate locations: tracking in one database, the historian in another, the laboratory in a third, inspection in a fourth, files in between.

PlantProcess IQ **connects the pieces**: read-only links to each source, then one declaration - made once, by the customer's own engineer, in the customer's own vocabulary - of how the pieces fit. From then on every chart, analysis, prediction and answer is drawn on the whole picture instead of one piece.

**It replaces nothing and writes to nothing.** It reads what the plant already owns and makes it one thing.

### 1.2.4 The freedom of a professional analytics platform, and what sits behind it

**This is the positioning, and it must not be softened in either direction.**

PlantProcess IQ is **not a generic business-intelligence product**. But its dashboarding, charting, authoring, relationship modelling, filtering and exploration experience **must reach the professional flexibility, clarity and usability that a customer already expects** from platforms of that class. The difference is never a weaker dashboard. The difference is what the product understands, learns and produces behind that dashboard.

**The same class of freedom**, all of it through the interface and none of it requiring a developer:

connecting and modelling the customer's own data; declaring relationships between entities; creating pages and sheets; creating and editing charts, tables, pivot views, KPIs, calculated labels and filters; building calculated columns, measures and expressions; creating reusable dimensions, measures and filters; applying dynamic and associative filtering; drilling down and drilling through; creating bookmarks, saved selections and saved views; comparing periods, lines, equipment and operating contexts; designing responsive layouts; and exploring visually without programming.

**And then substantially beyond it.** The purpose of that freedom is not to draw charts. It is to build and continuously enrich a **governed, evidence-grade model of this specific plant**, and then to use that model to learn how the plant behaves, discover evidence-backed relationships, predict downstream outcomes, find historically supported remediation, and measure what actually happened afterwards.

> **The dashboard is the interaction and presentation layer. The plant model, the genealogy, the evidence chain, the statistical discipline, practice learning, prediction, remediation and the feedback loop are the product.**

**One consequence that a buyer should be told explicitly:** intelligence is not a separate report the platform emails you. A prediction, a finding, a practice benchmark, a remediation candidate, a value impact and a readiness state are **first-class analytical objects**. They can be filtered, compared, charted, tabulated, drilled into, linked back to material genealogy, placed on any page the customer builds, included in a report, explained by the assistant, and tracked to their eventual outcome - on exactly the same surfaces, with exactly the same freedom, as ordinary operational data (Ch2 3.18).

### 1.2.5 The five layers of assistance

| Layer | What it delivers | From tier | Specified in |
|---|---|---|---|
| 1 **Unified visibility** | Every source side by side: dashboards, charts, interactive filters; plant-wide patterns without expertise | Light | Ch4 5.1 |
| 2 **Statistical intelligence** | Parameters, materials, equipment conditions and operating context related to quality, downtime, throughput, yield, energy and any other registered outcome | Pro | Ch4 5.5 |
| 3 **Machine learning** | Probable contributors, bottlenecks and recurring-failure drivers, learned from the plant's own history through genealogy and route context | Pro Plus | Ch4 5.6 |
| 4 **Prediction** | An active material, equipment condition or production context that resembles historically risky conditions is flagged for a specific outcome, with a horizon and its uncertainty, before that outcome exists | Pro Plus | Ch4 5.6.4 |
| 5 **Recommendation** | Historically supported later-stage practices to avoid the predicted outcome, and practice benchmarking against the plant's own demonstrated best | Pro Plus | Ch4 5.6.4, 5.6.4a |

Cutting across all five: **the assistant dock** (plain-language questions, cited answers, never originates a figure; Ch4 5.7); and **the value engine** (bounded euro range with drill-through; Ch3 4.5.4).

Standing under all five: **the readiness gate**. Before any analysis runs the data is measured against published thresholds; where it cannot support a defensible answer the platform abstains and names the reason (Ch4 5.4.3).

### 1.2.6 Continuous, not one-off

The external expert gives one answer and leaves. This platform accumulates:

- Each import extends the validated history the analytics read.
- Each completed analysis contributes to what the platform examines next, **through governed review with human approval, never silently.**
- Each accepted or rejected suggestion is recorded, and its actual outcome measured against what was expected.
- Each relationship and definition the engineer declares becomes part of the plant's permanent model - versioned, exportable, the customer's property.

**How quickly that becomes useful depends on the plant's own data**: how much history exists, at what cadence it arrives, how complete it is, and how many outcome events the period contains. The platform publishes its measured readiness per outcome rather than promising a schedule.

---

## 1.3 How it solves the problem

Three stories, because a chief executive remembers stories, and each is a different stream of money. Each carries its technical reference.

### 1.3.a Quality: the early variation nobody could see

*Delivered by Ch3 DF6 to DF10; Ch4 5.5.3 and 5.5.4.*

A small variation at an early stage - a temperature slightly off pattern, a chemistry at the edge of its band, an unusual speed profile - contributes to a defect discovered much later, at final inspection, on different equipment, recorded in a different system under a different identifier.

No person holds thousands of parameter combinations across stages in mind. The platform can, because it holds the whole puzzle:

1. It walks the **genealogy** from the affected final unit back through every intermediate stage to its origin, on the plant's own keys.
2. It relates upstream conditions to the outcome **across the whole population**, not one case, under the statistical discipline: effect size first, multiple-testing control, stratification by visible confounders, stability checks.
3. It reports **suspected contributors, ranked, with the evidence** - population, method, framing - and the euro consequence as a range.
4. Where the data cannot support a defensible answer it abstains and names the deficient dimension with its measured value.
5. The result is not a static report. It is an object the engineer can filter, chart beside the process data it came from, drill into, and place on their own page.

The investigation that consumed weeks of manual extracts becomes a query the quality engineer runs the same morning.

### 1.3.b Productivity: learning which practices worked

*Delivered by Ch4 5.6.4a; persisted per Ch3 4.5.12; presented on Practice Insights, Ch3 4.4 D10.*

The same plant running the same products has good periods and bad periods. The difference is often **practice**: combinations of parameters, operations, sequences and operating decisions. Nobody records which combination was in force when things went well, because "well" is also spread across systems.

The platform reconstructs it from data the plant already has:

1. It derives the **operating practice in force** for each production period - the parameter combination and the operation sequence - as a comparable signature.
2. It links practices to outcomes across the available history: **which practices coincided with the highest productivity without failure, and which practices preceded defects, downtime and operational failures.**
3. It presents both as benchmarks with their **support counts and confidence** - the plant's own best demonstrated practice, from the plant's own record, not an external average.
4. It reports **where current operation is drifting away** from that demonstrated best.

**The discipline that makes this credible:** a practice becomes a benchmark only when the history contains enough comparable periods to support the claim. Below that threshold it is shown as observed but unproven, never promoted to a recommendation.

This is throughput and availability money, distinct from quality money, and it comes from data already collected.

### 1.3.c Prevention: predict early, remediate downstream

*Delivered by Ch4 5.6.4 and its fifteen-stage lifecycle; persisted per Ch3 4.5.12; presented on Early Warning, Ch3 4.4 D9.*

The capability that changes how a plant operates:

1. **Predict.** From the combination of early-stage parameters, material attributes and equipment condition, the platform identifies an active unit or production context that resembles historically risky conditions and flags a **specific predicted outcome with a defined horizon and its uncertainty** - while the unit is still in process, before the outcome exists.
2. **Explain.** The flag carries its drivers with contribution and direction, the current value against the normal operating range, the relevant stages and genealogy, the model version, the calibration context, the known limitations, and **comparable successful and unsuccessful historical cases**.
3. **Remediate.** The platform searches the plant's own history for comparable early conditions that later achieved a better outcome, and identifies **what was done differently in the remaining production stages.**
4. **Decide.** The remediation candidate carries the proposed later-stage practice, its historical support count, the expected-effect range, its comparable evidence and its limitations. **A human accepts, rejects or defers. The platform suggests; it never controls the plant.**
5. **Measure.** What was recommended, what was actually done, at which stage, what outcome followed, and whether the remediation worked, are all recorded, evaluated and fed back into governed review.

The plant moves from discovering outcomes to preventing them, using its own recorded experience.

### 1.3.d Why any of it should be believed

*Delivered by the Rules honesty contract; Ch4 5.4.3 and 5.7.3.*

Competitors compute. The difference here is what happens when the data is not good enough:

- The platform **refuses to compute when the data cannot support a defensible answer**, naming the failing dimension with its measured value against the published threshold.
- Every number traces to its query, its population and the source rows, through the same relationship model that produced it.
- Deterministic engines compute; the assistant explains with citations and **cannot render an uncited number** - the guard rejects the sentence before display.
- Every finding is stored with its framing as a suspected contributor and its named method, never as a guaranteed root cause.

**A tool that always produces an answer is a tool whose answers cannot be checked.** Showing a customer an honest red status with the measured reason is a deliberate design choice, and it is unusual in this market.

---

## 1.4 Target audience, and what each buyer needs to hear

| Buyer | Pain | What the platform gives them | Proof they accept |
|---|---|---|---|
| **Chief executive** | Recurring losses; dependence on scarce experts; consultants who leave | The in-house expert that learns the plant's fingerprint and stays | The three stories of 1.3, then a pilot measured on their own data |
| **Purchasing** | Justifying spend; supplier risk | Bounded value model; read-only architecture; exportable assets | The value model with traceable inputs, and the exit position in 1.8 |
| **Plant manager** | Losses and downtime recurring without explanation | Ranked, quantified priorities across the whole plant | A monthly value report on their own data |
| **Quality manager** | Defects and claims that cannot be attributed | Cross-source defect-driver investigation | A defect story walked end to end on their own material |
| **Process engineer** | Investigations by hand; dependence on a developer for every new view | Genealogy, cross-source analysis, practice benchmarks, **and the freedom to build their own pages, measures and filters without asking anyone** | One click from an outcome to the upstream conditions on their own key names, and a page they built themselves in the meeting |
| **IT and OT manager** | Security and integration risk | Read-only, one-way collector, never into the control network | The architecture, and the absence of any write path |
| **Finance** | Whether it pays back | A bounded range, every input traceable, abstains where inputs are missing | A payback case computed on their own pilot data |

**The order matters.** The process engineer and the OT manager can veto. Convince them first; the commercial conversation follows.

---

## 1.5 Voice, brand and the language contract

| Attribute | Definition |
|---|---|
| Product | PlantProcess IQ, by SOU Industrial Software |
| Primary tagline | Connect Your Plant Data. Understand Your Process. |
| Positioning line | The in-house expert that learns your plant's own fingerprint - and stays |
| Customer promise | Connect plant data. Discover quality drivers. Learn your best practices. Score risk earlier. Act with evidence |
| Personality | Industrial, intelligent, trusted, technical, calm, evidence-based |

**The language contract**, enforced by lint on every deliverable including this chapter:

| Never say | Say instead |
|---|---|
| Guaranteed root cause | Suspected contributor; evidence-ranked factor |
| AI-powered prediction; production-ready AI | Machine-learning readiness; statistical learning; risk scoring |
| Live support for an unproven connector | Connector capability shown honestly per source |
| We replace MES, level 2, SCADA or BI | We connect around existing systems |
| Autonomous optimisation; self-tuning | Decision support and governed review with human approval |
| It will save you X | A bounded range, with every input traceable |
| **A fixed learning timeline** - for example "in month six it knows your plant" | **As validated history grows, subject to readiness, stability and governed model review** |
| **A universal claim about competitors** - for example "no competitor shows a red status" | **A statement about our own design** - "we publish the measured readiness state, including when it blocks a run" |
| **"We are a BI tool" or "we replace your BI"** | **"A BI-class authoring experience over a permanent plant model, plus the intelligence that model makes possible"** |
| **"Our dashboards are simpler than BI"** | Nothing. **The dashboard experience is not a compromise and must not be sold as one** |

One forbidden phrase anywhere is a critical failure, regardless of the strength of the surface carrying it.

---

## 1.6 The value case

### 1.6.1 Three streams, one model

| Stream | Source | From |
|---|---|---|
| Quality | Fewer downgrades and claims; attributed causes acted on | 1.3.a |
| Productivity | Operating nearer the plant's own demonstrated best practice; less downtime | 1.3.b |
| Prevention | At-risk units remediated before the outcome exists | 1.3.c |

```
monthly impact =
      sum over affected material ( tonnes x penalty per tonne )
    + ( attributable production-impact minutes x cost per minute )
    + ( yield loss x grade premium )

Every term cites its inputs.  A missing input produces "insufficient basis", never a guess.
The output is a RANGE derived from the assumption bands, never a single number.
```

Persistence and abstention behaviour: Ch3 4.5.4. Presentation: Value Dashboard, Ch3 4.4 D7.

### 1.6.2 The downtime distinction most tools get wrong

**Equipment-stopped minutes and production-impact minutes are different quantities.** A stoppage upstream can be absorbed by buffered material and cost no production; a short stoppage at a constrained stage can force a sequence rebuild and cost hours. The platform stores both and uses the correct one per calculation (Ch3 4.5.4). Raising this unprompted with an operations buyer demonstrates the product was designed by someone who has stood in a plant.

### 1.6.3 Price-to-value parity

The buyer's own test, and the right one: **the added value must be clearly greater than the price.**

The commercial model is a per-site initial fee plus a monthly subscription in euro, priced as a plant platform rather than per seat. Capability by tier: Light for visibility; Pro adds statistics, correlation and SQL authoring; Pro Plus adds machine learning, prediction, remediation, practice learning, the value engine and the assistant; Enterprise adds every connector, on-premise and air-gapped deployment, self-hosted model serving, single sign-on and contracted support.

**A tier bounds two things together, and both appear in every offer.**

**Six commercial dimensions**, because these are what a customer can assess before buying and be held to afterwards: named users; pages; jobs; DB-links; amount of data transferred; and the capability step at the third tier, where machine learning, practice learning, prediction, remediation, the value engine and the assistant begin.

**And a capacity envelope**, metered on what actually consumes the machine: retained volume, ingest rate, minimum refresh interval, weighted compute slots and concurrent sessions.

**Both, because neither alone is honest.** Three DB-links can mean thirty gigabytes or a hundred and fifty; selling the count without the volume under-prices the platform, and selling only the meters gives a buyer nothing they can assess. **Every tier therefore states its counts, its capacity envelope and the server specification it requires** (Chapter 6 6.1.9.8, 6.3.4a).

**The distinction that keeps both honest.** Counts and capacity do different jobs and must never be confused:

| | **Commercial packaging limits** | **Technical cost meters** |
|---|---|---|
| Which | Users, pages, jobs, DB-links, feature tier | Retained volume, ingest rate, refresh floor, compute slots, concurrent sessions |
| Determine | **Package eligibility and authoring quotas** | **Hardware sizing and capacity protection** |
| Behave as | Soft guardrails: warn, then disable creation. Never interrupt work in progress | Throttle when exceeded: queue, wait for a slot, enforce the cadence floor |
| **Never** | **Never used as the hardware-sizing formula** | Never used as a price list a customer cannot self-assess |

> **A count determines which package a customer is eligible for. It never determines what the server must be.** Three DB-links tell you nothing about the machine; retained volume, ingest rate and compute slots tell you everything. Sizing is driven only by the meters (Chapter 6 6.1.9), and package eligibility only by the counts.

**What is deliberately never counted or priced by the object**: master dimensions and measures, filters, relationships, bookmarks, saved views, definition versions, genealogy depth and evidence drill-through. Those are the mechanisms that make authoring safe and reusable, and charging for them would sell the customer a worse product.

**And the design obligation behind the counts:** the customer is not charged for the vendor's scan strategy. Because every job class is delta-scoped end to end (Chapter 4 5.3.9), a Pro installation carrying a hundred and fifty gigabytes across three links with ten jobs on a three-minute cadence runs on a two-host deployment. **A design that rescanned the plant at cadence would need thousands of times more machine, and the customer would have paid for it.**

The honest form of the payback argument: **the platform does not need many findings.** A small number acted on covers the arithmetic comfortably at any tier - and what a specific plant recovers is what that plant's pilot measures.

---

## 1.7 Where we win, and what we do not claim

| Compared with | Position |
|---|---|
| **Generic BI (Power BI, Qlik, Tableau and similar)** | **We do not offer less authoring freedom; we offer the same class of freedom over something they do not have.** They model data you have already joined and prepared. We give the customer's own engineer the tools to declare the plant's relationships once, keep that as a permanent versioned model, and then learn from it: genealogy across grains, practice learning, prediction, remediation, readiness and an evidence chain. The joining and the intelligence are the work they leave to you |
| A bespoke data-science project | A configurable product, authored from the interface, reusable and extensible by the customer's own staff rather than a consultant's artifact |
| A black-box analytics or AI tool | Named methods, resolvable evidence handles, a published readiness state, and an assistant that refuses rather than guesses |
| MES and control-vendor add-ons (Primetals, PSI, SST, Fero and similar) | Read-only and vendor-neutral. It reads the other systems too, which is where cross-source correlation lives |

**What we do not claim.** Stated early, because it buys belief for everything else. It does not replace any existing system, **including your BI platform - it can export to it.** It never writes to a plant system and never participates in control. It produces suspected contributors, never guaranteed root causes. Connectors not yet proven are visibly marked as planned. A demonstration on emulated data proves the method, not the customer's numbers. And it does not promise a date by which it will be clever; it publishes its measured readiness.

---

## 1.8 The objection playbook

| Question | Answer |
|---|---|
| Can this write to my systems? | No. No write path exists. Outbound is message, export or webhook only |
| Will it load my production databases? | Row caps, statement timeouts, rate limits and approved windows, enforced before a read reaches the source. Backfill is throttled and resumable |
| Where does my data go? | Deterministic engines compute inside your tenant. The assistant model is self-hosted by default; a private endpoint receives only the question and the scoped evidence, never your tables. A per-tenant no-egress control forces self-hosted serving |
| **We already have Power BI. Why this?** | **Keep it. This is not a reporting layer; it is the plant model and the intelligence underneath one. Your engineers get the same authoring freedom here, over a model that knows your genealogy, your routes and your operating history - and everything here exports to your existing BI if you want it there too** |
| Do I need programmers to configure it? | Not for the common case. Pages, charts, KPIs, filters, calculated measures, hierarchies, bookmarks, analyses and rules are all authored by drag and drop. SQL exists for the long tail so an unusual requirement does not become a support ticket |
| Can my engineer build his own page without us raising a ticket with you? | Yes, and that is the point. Within his role and the capacity envelope, he creates pages, sheets, widgets, filters, measures and saved views himself, and the intelligence results appear as objects he can chart alongside his own data |
| Your engine refused to run. Is it broken? | No. It measured five named dimensions against published thresholds and one failed. Here is the dimension, the measured value and the threshold. It computes when the data supports it |
| Is this artificial intelligence or arithmetic? | Deterministic engines compute and rank. The assistant explains with citations and cannot originate a figure |
| How do I know a number is right? | Follow it. Every figure resolves to its query, its population and the source rows |
| Can the system change itself without us knowing? | No. Model and threshold changes are proposals with recorded justification, applied only after human approval, and every change is an audit record |
| **You are a new company. Will you exist in three years?** | **A fair question, and the answers are structural rather than reassuring: the product is read-only, so removing it breaks nothing in your plant; your data stays in your own database; the plant model and every definition are exportable; on-premise and air-gapped deployment mean no dependence on our hosting. We are also building a five-product portfolio, not a single tool** |
| Will it pay back more than it costs? | The value model computes a bounded range from your own cost inputs, abstains where inputs are missing, and the figure that matters is the one your pilot measures |

---

## 1.9 Selling before the value engine is live on the customer's data

The economic buyer's figure - a euro saving computed on their own data - arrives with the pilot, not the first meeting. That conversation is designed rather than improvised.

**Move one: show the model, not a number.** Put the arithmetic of 1.6.1 on screen. The buyer inserts their own figures mentally, and the number they reach is one they trust.

**Move two: give the honest status unprompted.** "I will not quote you a saving from our emulated plant as though it were yours. The figure that matters is the one your pilot measures." Volunteering this converts the weakest point of the presentation into a demonstration of the product's central claim.

**Move three: offer the pilot as the thing to sign against.** Connect these sources, run for this period, and the value model reports on your own data. They sign against a method that produces their number, not against ours.

Until then five demonstrable things carry the weight, and each is present rather than promised: **the authoring freedom, shown by building a page in the room**; the readiness gate with its measured state including a red one; the cross-source join declared by their own engineer and then visible in the relationship browser; the genealogy walk on their own key names; and the honest abstain, live.

For a planted-signal demonstration the exact wording is: **"It recovered a planted validation signal and rejected a null control. That validates the method. Return on investment is what your pilot measures."** Any stronger statement about that result is a forbidden claim under 1.5.

---

## 1.10 The demonstration narrative

Open with the disclosure, once, early, unprompted: "This instance runs on our emulated multi-source plant. On your installation it starts empty and fills through the read-only links, which I will show you now."

| Order | Beat | Why here |
|---|---|---|
| 1 | Connections and a live import | The only door, watched working |
| 2 | The join declared by an engineer, then shown in the relationship browser | The puzzle assembled, and then **visible as a permanent model** - the moment it stops resembling BI |
| 3 | Genealogy walk on real key names | "It speaks my plant", and it needs no engine run |
| 4 | Workspace: cross-filter, drill down, drill through to source rows | The BI-class beat, and it depends on no engine state |
| 5 | **Build a widget live**: add page, add widget, bind by query, map columns, save | Proves the authoring freedom claim in ninety seconds. The single most persuasive beat for an engineer |
| 6 | Findings, or the honest abstain | Both are good beats: one shows a result, the other the discipline |
| 7 | Early Warning, a driver explanation, and a remediation card with its support count | The 1.3.c story, live |
| 8 | **Put a prediction on a chart beside process data** | Proves intelligence is a first-class object, not a separate report |
| 9 | The assistant from the corner of the same page, cited | Explains, never computes |
| 10 | The value model, per 1.9 | Last, because it is a conversation rather than a screen |

**The strongest artifact available** is a finding computed on data the customer watched arrive. If beat 1's import is the data beat 6 analyses, the demonstration stops being a tour and becomes a proof.

Contingencies are spoken, not debugged: a source down is "the connector reports it unreachable - the same honesty you get in production"; anything red is "I will note that and follow up."

---

## 1.11 Commercial promise traceability

**No promise in this chapter may exist without a row here.** A new claim requires a new row and a specified capability behind it. A row whose reference cannot be resolved is a defect in this chapter, not in the technical chapters.

Journey codes are the canonical user journey J1 to J15 and the canonical data-flow codes DF1 to DF15, both defined in Chapter 2, 3.3.

| Commercial promise | Here | Journey | Page or surface | Backend and API | Persistence | Acceptance |
|---|---|---|---|---|---|---|
| Connect existing sources read-only | 1.2.3 | J4-J6 / DF1-DF3 | Connections; Dataset Registry; Prepare Import; Importing | `/api/connections`, `/api/datasets`, `/api/imports` | `connection_profiles`, `source_dataset_definitions`, `import_batches`, `staging_records` | Ch3 DF1-DF3 |
| Join the plant like a puzzle, declared once | 1.2.3 | J7 / DF4 | Transformation Studio; **Relationship Browser** | `/api/transformations`, `/api/relationships` | `definition_store`, `definition_versions`, `plant_relationships`, `..._members`, `..._paths` | Ch3 DF4 |
| **The plant model is permanent, versioned and queryable** | 1.2.2, 1.2.4 | J7 / DF4 | Relationship Browser; Plant Model Explorer | `/api/relationships`, `/api/plant/layout` | relationship model; canonical entity model | Ch2 3.15; Ch3 4.5.10 |
| Mapping mistakes caught before they corrupt anything | 1.2.2 | J8 / DF5 | Mapping Health | `/api/transformations/{id}/validate`, `/api/quarantine` | `projection_quarantine` | Ch3 DF5 |
| Genealogy on the customer's own keys | 1.3.a | J9 / DF6 | Genealogy Explorer | `/api/plant/materials/{id}/genealogy` | `material_units`, `material_aliases`, `genealogy_edges` | Ch3 DF6 |
| **BI-class authoring freedom without a developer** | 1.2.4, 1.4, 1.8 | J10 / DF7 | Page Builder; Interactive Workspace; the shared shell | `/api/pages`, `/api/registry/metadata`, `/api/definitions` | `definition_store`, `dashboard_definitions`, `dashboard_widget_definitions`, master items | Ch2 3.16; Ch4 5.1.15, 5.2.15 |
| **Any number of pages, sheets, charts, KPIs, measures and filters** | 1.2.4 | J10 / DF7 | Page Builder | `/api/pages`, `/api/definitions` | `definition_store`, master dimensions and measures | Ch2 3.16 |
| **Dynamic and associative exploration** | 1.2.4 | J11 / DF7 | Interactive Workspace | `/api/workspace/state`, `/api/workspace/select` | selection state; `plant_relationship_paths` | Ch2 3.17; Ch4 5.1.15 |
| Drill down and drill through to source evidence | 1.2.4, 1.3.d | J11 / DF7 | Interactive Workspace; Findings; Early Warning | `/api/workspace`, `/api/findings/{id}/evidence` | registry hierarchies; provenance path | Ch2 3.12 items 9-10 |
| Bookmarks, saved selections and saved views | 1.2.4 | J11 / DF7 | Interactive Workspace | `/api/pages/bookmarks` | `definition_store` (bookmark kind) | Ch2 3.17 |
| Statistical intelligence, honest statistics | 1.2.5 L2, 1.3.a | J12 / DF8-DF9 | Analysis Toolbox; Findings | `/api/analyses`, `/api/findings` | `compute_runs`, `correlation_results` | Ch4 5.5.6 |
| Refuses when data is insufficient | 1.3.d | J12 / DF8 | every analytical surface | `/api/readiness/evaluate` | `compute_runs.gate_state`, `gate_evidence` | Ch4 5.4.7 |
| Machine learning on the plant's own history through genealogy and route context | 1.2.5 L3 | J12 / DF10-DF11 | ML Readiness and Models | `/api/features`, `/api/models` | `feature_store`, `model_registry`, `model_training_runs` | Ch4 5.6.6 |
| Practice learning for productivity, and drift from own best | 1.3.b | J13 / DF12 | Practice Insights; Benchmarking | `/api/practices` | `practice_statistics` | Ch4 5.6.4a |
| Early prediction with horizon and uncertainty | 1.3.c.1 | J13 / DF13 | Early Warning; Risk Dashboard | `/api/predictions` | `prediction_runs`, `predictions` | Ch4 5.6.4 |
| Driver explanation, "why this unit, why now", comparable cases | 1.3.c.2 | J13 / DF13 | Early Warning drill-down | `/api/predictions/{id}/drivers`, `/api/predictions/{id}/comparables` | `prediction_drivers` | Ch4 5.6.4 |
| Historically supported downstream remediation with limitations | 1.3.c.3 | J13 / DF13 | Early Warning remediation card | `/api/predictions/{id}/remediations` | `remediation_candidates`, support threshold enforced | Ch4 5.6.4 |
| Only safe, controllable, still-actionable remediations are recommended | 1.3.c.4 | J13 / DF13 | Early Warning remediation card | `/api/predictions/{id}/remediations/gate` | `remediation_candidates.eligibility_state`, `failed_checks` | Ch4 5.6.4d |
| The prediction arrives before the stage that can still act on it | 1.3.c.1 | J13 / DF13 | Early Warning | `/api/predictions/queue` | `predictions.actionable_deadline_utc`, `met_actionable_deadline` | Ch4 5.8.8 |
| Human approval, never automatic control | 1.3.c.4 | J13-J14 / DF14 | Early Warning; Suggestions | `/api/suggestions/{id}/decide`, `/api/predictions/{id}/decide` | `suggestions`, `suggestion_audit`, prediction action records | Ch3 DF14 |
| Measured effectiveness after the fact, and feedback into governed review | 1.3.c.5, 1.2.6 | J14 / DF14 | Suggestions; Value Dashboard; Supervisor | `/api/value`, `/api/predictions/evaluations` | `value_realization_ledger`, prediction evaluations, feedback records | Ch3 DF14 |
| **Intelligence is a first-class analytical object** | 1.2.4 | J13 / DF7, DF13 | any authored page | `/api/workspace/widgets/execute` over intelligence sources | `correlation_results`, `predictions`, `practice_statistics`, `value_impacts` exposed as bindable sources | Ch2 3.18 |
| Euro value as a bounded range | 1.6.1 | J14 / DF14 | Value Dashboard | `/api/value/impacts` | `value_impacts`, `cost_assumptions` | Ch3 DF14 |
| Two downtime quantities never confused | 1.6.2 | J8 / DF5 | Value Dashboard; Findings | `/api/value` | `downtime_events`, both columns | Ch3 4.5.4 |
| Compare periods, lines, equipment and contexts | 1.2.4 | J13 / DF12 | Benchmarking | `/api/benchmarks` | registry dimensions; `practice_statistics` | Ch2 3.16 |
| Explanations with resolvable citations | 1.3.d | J15 / DF15 | The assistant dock, every page | `/api/assistant/ask` | `assistant_chunks`, `assistant_audit_log` | Ch4 5.7.8 |
| Governed change, nothing self-modifying | 1.2.2, 1.2.6 | J15 / DF15 | Supervisor | `/api/supervisor` | provenance rows, audit layer | Ch4 5.4.7 |
| Nothing industry-specific ships in the product | 1.0 | all | every authoring surface | `/api/registry/metadata` | `registry_dimensions`, `registry_measures` | Rules, Rule 1 gates |
| The customer keeps and can export everything | 1.8 | J7 / DF4 | Transformation Studio export; Reports | `/api/definitions/{id}/export` | `definition_versions`, export artifacts | Ch3 4.5.11 |
| Logs and history retained under the customer's own policy | 1.8 | J15 | Log Retention and Archival | `/api/admin/log-retention` | `log_retention_policies`, `log_cleanup_runs` | Ch3 4.4 F9 |

---

*End of Chapter 1. Every claim here is bound by the Rules chapter and traced in 1.11. Marketing never leads engineering in this document.*
