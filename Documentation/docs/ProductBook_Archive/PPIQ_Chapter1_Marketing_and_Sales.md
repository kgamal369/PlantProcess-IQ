# PlantProcess IQ - Master Design Document

**Version 4.1 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

# CHAPTER 1 - MARKETING AND SALES

> **Target audience (1.4):** the chief executive of a manufacturing group or plant, and the purchasing function that runs the commercial review.
>
> **Voice (1.5):** marketing and sales. Industrial, intelligent, trusted, calm, evidence-based. Plant operations plus data science, never consumer technology.
>
> **The constraint:** every claim in this chapter is bound by the Honesty Contract and the forbidden-language table of the Rules (PPIQ.txt item 2). This is marketing that cannot overstate, and that is the product's central commercial argument, made in the way it is written.

---

## 1.1 The problem

### 1.1.1 One condition, every process plant

Every large process plant records what happens to its product many times, in many systems, under many vocabularies. The melt in one database, the casting sequence in another, the rolling pass in a third, the surface defect in a fourth, the laboratory result in a fifth, spread across lines and sometimes across plant locations.

Each system is correct alone. **Nothing joins them.** And every question that costs the plant money lives exactly in the join.

### 1.1.2 What that produces, on the shop floor

| Symptom | What it looks like in the plant |
|---|---|
| **Repeated defects with unknown source** | The same defect class returns month after month. Everyone has a theory. Nobody has the joined data to test one |
| **Repeated downtime with unknown root cause** | The same equipment stops again and again. The maintenance log says what stopped, never why the process drove it there |
| **Repeated equipment and operation failures** | Failures recur under conditions nobody has connected, because the conditions live in a different database from the failures |
| **Quality claims that cannot be attributed** | A customer rejects material; the investigation takes weeks of manual extracts and ends in "probably" |

### 1.1.3 The expert problem

When one of these investigations matters enough, the plant does one of two things.

**It calls its own veteran** - the one person who holds the whole process in their head and can connect a late-stage defect to an early-stage deviation from memory. That person is rare, expensive, close to retirement, and a single point of failure. When they are on leave, the investigation waits. When they retire, a decade of pattern recognition walks out of the gate.

**Or it hires an external expert** - a consultant or a vendor specialist who arrives, studies the case for days at a high daily rate, delivers an opinion on that one problem, **and leaves, taking everything he learned about your plant with him.** The next problem starts from zero. The plant has paid for a conclusion, not for capability. Nothing accumulated.

Both paths share the same defect: **the learning does not stay, does not grow, and does not scale.**

### 1.1.4 The unexploited asset

The bitter part is that the plant already owns everything required. The data exists. It has been collected for years, at real cost, by systems the plant already paid for. It sits in databases nobody joins, answering questions nobody can ask.

**The plant is not missing data. It is missing the expert who can read all of it at once, every day, and never leaves.**

### 1.1.5 What plants try instead

| The usual approach | Why it stops working |
|---|---|
| Spreadsheets and manual extracts | Works once, for one question, by one person. Does not survive that person's holiday and cannot be scheduled |
| A generic business-intelligence tool | Draws charts beautifully on data you have already joined. The joining is the entire problem, and it does not do it |
| A bespoke data-science project | Solves one plant's one question at consulting rates, and leaves an artifact nobody can extend |
| Waiting for the MES vendor | Deepens the commitment to one stack, whose incentive is its own data, not the other five systems |

None of these is stupid. All of them are what a competent plant does in the absence of anything better.

---

## 1.2 The aim of the software

### 1.2.1 The positioning sentence

> **PlantProcess IQ is the in-house expert that learns your exact plant's fingerprint - and never leaves.**

Not a dashboard. Not a consultant. A resident intelligence layer that connects the data you already own, learns from it day after day, and gets better at your plant specifically: your grades, your routes, your equipment, your defect vocabulary, your seasonal patterns. What it learns stays with you, accumulates, and is inspectable.

### 1.2.2 The puzzle

Your plant's data is a puzzle whose pieces are scattered across systems and locations: the MES in one database, the level-2 tracking in another, the historian in a third, the laboratory system in a fourth, the inspection system in a fifth, files in between - sometimes across several plants.

PlantProcess IQ **connects the pieces like a puzzle**: read-only links to each source, and then one declaration - made once, by your own engineer, in your own vocabulary - of how the pieces fit. From that moment the whole picture exists, and every analysis, chart, prediction and answer is drawn on the whole picture instead of on one piece.

**It replaces nothing. It writes to nothing. It reads what you already own and makes it one thing.**

### 1.2.3 The five layers of assistance

| Layer | What it delivers | From tier |
|---|---|---|
| 1 **Unified visibility** | Every source side by side: dashboards, charts, heatmaps, interactive filters. Plant-wide patterns visible without expertise | Light |
| 2 **Statistical intelligence** | Every parameter related to every defect, downtime cause and performance measure, under honest statistics | Pro |
| 3 **Machine learning** | Probable contributors, bottlenecks, recurring-failure drivers, learned from your own history | Pro Plus |
| 4 **Prediction** | Material that ran abnormal upstream is flagged for a specific downstream defect **before it occurs** | Pro Plus |
| 5 **Recommendation** | Suggested practices at later stages to avoid the predicted outcome, and practice benchmarking for productivity | Pro Plus |

Cutting across all five: **the assistant**, a chat that stays with you on every page, explains and cites, and never invents a number; and **the value engine**, which prices every finding in euro as a bounded, drill-throughable range.

Standing under all five: **the readiness gate.** Before any analysis runs, the data is measured against published thresholds. If it cannot support a defensible answer, the product says so, names the reason, and refuses - which is exactly what an honest expert does.

### 1.2.4 Day by day, not one-off

The external expert gives you one answer and leaves. This product gives you a compounding asset:

- Every import teaches it more of your history.
- Every completed analysis sharpens what it looks for, under governed, human-approved adjustment.
- Every accepted or rejected suggestion is remembered and measured against what actually happened.
- Every mapping your engineer declares becomes part of your plant's permanent model - versioned, exportable, yours.

**In month one it is a very good analyst. In month six it knows your plant.**

---

## 1.3 How it solves the problem

Three worked stories, because a chief executive remembers stories, and each one is a different stream of money.

### 1.3.a Quality: the slight early variation nobody could see

A slight variation at an early production stage - a temperature a few degrees off pattern, a chemistry at the edge of its band, a speed profile slightly unusual - causes a defect discovered much later, at final inspection, on a different machine, recorded in a different system, under a different identifier.

No human can hold thousands of parameter combinations across stages in their head. The product can, because it has the whole puzzle:

1. It walks the **genealogy** from the defective final unit back through every intermediate stage to its origin, on your own keys.
2. It relates every upstream parameter to the defect class across the whole population, not one case - with honest statistics: effect sizes first, multiple-testing control, stratification, stability checks.
3. It reports the **suspected contributors, ranked, with the evidence**, the population, the method and the euro consequence.
4. Where the data cannot support an answer, it says so and names what is missing.

The investigation that took the veteran three weeks of extracts becomes a query the quality engineer runs before lunch.

### 1.3.b Productivity: learning the practices that work

The same plant, running the same products, has good weeks and bad weeks. The difference is **practice**: combinations of settings, sequences, speeds, crews and operating decisions. Nobody records which combination was in force when things went well, because "well" is spread across five systems too.

The product learns it:

1. It reconstructs, from the data you already have, the **operating practice in force** for every production period: the parameter combinations, the sequences, the routes.
2. It links practices to outcomes across the whole history: **which practices coincided with maximum productivity without failure, and which practices preceded downtime and failures.**
3. It presents both as benchmarks: your own best demonstrated practice, from your own plant, with the evidence - not an industry average from a book.
4. It watches for the plant drifting away from its own best practice and says so.

This is throughput and downtime money, distinct from quality money, and it comes from nothing but the data already collected.

### 1.3.c Prediction and remediation: catch it early, fix it downstream

The deepest capability, and the one that changes how a plant operates:

1. **Predict.** From the combination of early-stage parameters, the product flags a specific unit as elevated-risk for a specific downstream defect - **while the unit is still mid-process, before the defect exists.**
2. **Explain.** The flag carries its drivers: which early parameters, how far off pattern, with what historical basis.
3. **Remediate.** The product searches your own history for cases where units in the same early condition were nevertheless finished successfully, and identifies **what was done differently at the later stages** - a practice adjustment downstream that historically neutralised this early condition.
4. **Suggest, with discipline.** A remediation is suggested only when your own history contains enough supporting cases; it carries the evidence and the expected effect; and **a human approves it - the product suggests, it never instructs and never acts.**

The plant stops discovering defects and starts preventing them, using nothing but its own recorded experience - the experience that was always there and never readable.

### 1.3.d Why believe any of it: the honesty machinery

Every competitor computes. The difference here is what happens when the data is not good enough:

- The product **refuses to compute when the data cannot support a defensible answer**, and names the failing dimension with its measured value.
- Every number on every screen traces to its query, its population and the source rows.
- Deterministic engines compute; the assistant only explains, with citations, and cannot render an uncited number.
- Every finding is framed as a suspected contributor with its method, never a guaranteed root cause.

**A tool that always produces an answer is a tool whose answers cannot be trusted.** No competitor in this market shows a prospect a red status. That is the point.

---

## 1.4 Target audience, and what each buyer needs to hear

| Buyer | Their pain | What the product gives them | The proof they accept |
|---|---|---|---|
| **Chief executive** | Recurring losses, dependence on scarce experts, expensive consultants who leave | The in-house expert that learns the plant's fingerprint and stays | The three stories of 1.3, then a pilot measured on their own data |
| **Plant manager** | Losses and downtime that recur without explanation | Ranked, quantified priorities across the whole plant | A monthly value report on their own data |
| **Quality manager** | Defects and claims they cannot attribute | Defect-driver investigation across sources | A defect story walked end to end on their own material |
| **Process engineer** | Investigations by hand in spreadsheets | Genealogy plus cross-source analysis plus practice benchmarks | One click from a defect to the upstream chemistry, on their own key names |
| **IT and OT manager** | Security risk and integration burden | Read-only, one-way collector, never into the control network | The architecture, and the absence of any write path |
| **CFO and purchasing** | Whether it pays back | A bounded value model, every input traceable | A payback case computed on their own pilot data |

**The order matters.** The process engineer and the OT manager can veto. Convince them first; the commercial conversation follows.

---

## 1.5 Voice, brand and the language contract

| Attribute | Definition |
|---|---|
| Product | PlantProcess IQ, by SOU Industrial Software |
| Primary tagline | Connect Your Plant Data. Understand Your Process. |
| Positioning line | The in-house expert that learns your plant's fingerprint - and never leaves |
| Customer promise | Connect plant data. Discover quality drivers. Learn your best practices. Score risk earlier. Act with evidence |
| Personality | Industrial, intelligent, trusted, technical, calm, evidence-based |

**The language contract**, enforced by lint on every deliverable including this chapter:

| Never say | Say instead |
|---|---|
| Guaranteed root cause | Suspected contributor; evidence-ranked factor |
| AI-powered prediction; production-ready AI | Machine-learning readiness; statistical learning; risk scoring |
| Live support for an unproven connector | Connector capability shown honestly per source |
| We replace MES, level 2, SCADA or BI | We connect around existing systems |
| Autonomous optimisation | Decision support with human approval |
| It will save you X | A bounded range, with every input traceable |

One forbidden phrase anywhere is a critical failure, regardless of the strength of the surface carrying it.

---

## 1.6 The value case

### 1.6.1 Three streams, one model

| Stream | Source | From 1.3 |
|---|---|---|
| Quality | Fewer downgrades and claims, attributed causes acted on | a |
| Productivity | Operating at your own demonstrated best practice; less downtime | b |
| Prevention | At-risk units remediated before the defect exists | c |

```
monthly impact =
      sum over affected material ( tonnes x penalty per tonne )
    + ( attributable production-impact minutes x downtime cost per minute )
    + ( yield loss x grade premium )

Every term cites its inputs.  A missing input produces "insufficient basis", never a guess.
The output is a RANGE derived from the assumption bands, never a single number.
```

### 1.6.2 The downtime distinction most tools get wrong

**Equipment-stopped minutes and production-impact minutes are different quantities.** A twenty-minute stoppage upstream can be absorbed entirely by buffered material and cost no production at all; a three-minute stoppage on a casting machine can force a sequence rebuild and cost hours. The product stores both and uses the correct one per calculation. Raising this unprompted with an operations buyer demonstrates the product was designed by someone who has stood in a plant.

### 1.6.3 Price-to-value parity

The buyer's own test, and the right one: **the added value must be clearly greater than the price, or they will not buy.** The commercial model is a per-site initial fee plus a monthly subscription in euro, priced as a plant platform, not a per-seat tool. Tier capabilities: Light for visibility on a first line; Pro adds statistics and SQL authoring; Pro Plus adds machine learning, prediction, recommendation, the value engine and the assistant; Enterprise adds every connector, on-premise and air-gapped deployment, self-hosted model, single sign-on and contracted support. Envelopes and formulas are the commercial chapter's.

The honest form of the payback argument: the platform does not need many findings. A small number acted on covers the arithmetic comfortably at any tier - **and what a specific plant recovers is what that plant's pilot measures.**

---

## 1.7 Where we win, and what we do not claim

| Compared with | Position |
|---|---|
| Generic BI (Power BI, Qlik and similar) | They chart data you already joined. We carry manufacturing semantics: genealogy, practice learning, readiness, prediction, value. The joining is the work, and they leave it to you |
| A bespoke data-science project | A configurable product, reusable, authored from the interface - not a consultant's one-plant artifact |
| A black-box AI tool | Named methods, resolvable evidence, an assistant that refuses to guess |
| MES-vendor add-ons (Primetals, PSI, SST, Fero and similar) | Read-only and vendor-neutral; reads the other five systems too, which is where the correlation lives |

**What we do not claim**, stated early because it buys belief for everything else: it does not replace any existing system; it never writes to a plant system; it produces suspected contributors, never guaranteed root causes; unproven connectors are visibly marked planned; a demonstration on emulated data proves the method, not your plant's numbers.

---

## 1.8 The objection playbook

| Question | Answer |
|---|---|
| Can this write to my systems? | No. No write path of any kind exists. Outbound is message, export or webhook only |
| Will it load my production databases? | Row caps, statement timeouts, rate limits, approved windows, all visible. Backfill is throttled and resumable |
| Where does my data go? | Engines compute inside your tenant. The assistant model is self-hosted by default; a private endpoint receives only the question and the scoped evidence, never your tables |
| Do I need programmers to configure it? | No. Drag-and-drop authoring for the common case; SQL for the long tail so an unusual need is not a support ticket |
| Your engine refused to run. Broken? | No. It measured five named dimensions and one failed; here is the dimension, the value, the threshold. It computes when the data supports it |
| Is this AI or mathematics? | Deterministic engines compute. The assistant explains with citations and cannot invent a figure |
| You are a new company. In three years? | The product is read-only, so removing it breaks nothing; your data stays in your database; the authored model is exportable; on-premise and air-gapped mean no dependence on our hosting |
| Will it pay back? | The value model computes a bounded range from your own cost inputs, abstains where it cannot, and the figure that matters is the one your pilot measures |

---

## 1.9 Selling before the value engine is live

The economic buyer's figure - a euro saving on their own data - arrives with the pilot, not the first meeting. That conversation is designed, never improvised:

**Move one: show the model, not a number.** The arithmetic of 1.6.1, on screen. The buyer mentally inserts their own numbers, and the figure they reach is one they trust.

**Move two: give the honest status unprompted.** "I will not quote you a saving from our emulated plant as though it were yours. The figure that matters is the one your pilot measures." No vendor says this; it is the moment the honesty argument stops being a slogan.

**Move three: offer the pilot as the thing to sign against.** Connect these sources, run this period, the value model reports on your own data. They sign against a method that produces their number, not against ours.

Until then, four demonstrable things carry the weight: the readiness gate with its measured red; the cross-source join made by their own engineer; the genealogy walk on their own keys; and the honest abstain, live. For the planted-signal demonstration, the exact sentence: **"It recovered a planted validation signal and rejected a null control. That validates the method. Return on investment is what your pilot measures."**

---

## 1.10 The demonstration narrative

Open with the disclosure, once, early, unprompted: "This instance runs on our emulated multi-source plant. On your installation it starts empty and fills through the read-only links, which I will show you now."

| Order | Beat | Why here |
|---|---|---|
| 1 | Connections and a live import | The only door, watched working |
| 2 | The join, declared by an engineer | The puzzle assembled; the moment it stops resembling BI |
| 3 | Genealogy walk on real key names | "It speaks my plant", no engine needed |
| 4 | The workspace: cross-filter and drill across sources | The strongest safe beat |
| 5 | Findings, or the honest abstain | Both are good beats; one shows a result, the other shows the moat |
| 6 | Prediction and the remediation card | The 1.3.c story, live |
| 7 | The assistant, cited, from the corner of the same page | Explains, never computes |
| 8 | The value model, per 1.9 | Last, because it is a conversation |

**The strongest artifact:** a finding computed on data the customer watched arrive. If beat 1's import is the data beat 5 analyses, the demonstration stops being a tour and becomes a proof.

Contingencies, spoken not debugged: a source down is "the connector reports it unreachable - the same honesty you get in production"; anything red is "I will note that and follow up."

---

*End of Chapter 1. Every claim here is bound by the Rules. Marketing never leads engineering in this document.*
