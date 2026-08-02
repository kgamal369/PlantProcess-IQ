# PlantProcess IQ - Master Design Document

**Version 4.0 | 29 July 2026 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

# CHAPTER 2 - MARKETING AND SALES

> **Audience:** the chief executive of a manufacturing group or plant, and the purchasing function that will run the commercial review.
>
> **Voice:** marketing and sales. Industrial, intelligent, trusted, technical, calm, evidence-based. It should read like plant operations plus data science, never like consumer technology.
>
> **The constraint this chapter operates under:** every claim in it is bound by the Honesty Contract of Chapter 1.5 and by the forbidden-language table of 1.5.9. **This is marketing that cannot overstate**, and that is not a limitation being worked around. It is the product's central commercial argument, made in the way it is written.

---

## Provenance of this chapter

| Section | Tag | Note |
|---|---|---|
| 2.1 How to use this chapter | [N] | |
| 2.2 The problem | [C] | From the compass and the constitution |
| 2.3 The aim | [C] | The five layers of assistance |
| 2.4 How it solves the problem | [E] | Assembled from fragments across four drafts |
| 2.5 The buyers | [E] | Buyer table carried; the per-buyer argument is new |
| 2.6 The value case | [E] | Formula carried; the honest framing rewritten |
| 2.7 Where we win and where we do not | [E] | Competitive table carried and extended |
| 2.8 Brand | [C] | From the compass, verbatim where possible |
| 2.9 The language contract | [C] | Cites Chapter 1.5.9 |
| 2.10 The objection playbook | [E] | The surprise-question gate, restructured by buyer |
| 2.11 Selling before the value engine exists | [N] | Exists in no draft. It is the chapter's most important section |
| 2.12 The demonstration narrative | [E] | The playbook exists as an execution artifact; the argument behind it does not |

**Chapter total: roughly 35 percent carried, 45 percent enhanced, 20 percent new.**

---

## 2.1 How to use this chapter **[N]**

This chapter is the source of every claim made about PlantProcess IQ anywhere: on the website, in a deck, in a proposal, in a demonstration, in an email, in a conversation.

Three rules govern its use.

**One. Nothing outside this chapter may claim more than this chapter claims.** If a slide says something this chapter does not, the slide is wrong, not the chapter.

**Two. Every claim here traces to something in Chapters 3 to 9.** A marketing chapter that runs ahead of the specification is how a company sells a product it has to build afterwards under a deadline it chose.

**Three. The honesty lint runs on this chapter too.** If any sentence here contains a phrase from the forbidden column of 2.9, this chapter has failed its own test.

---

## 2.2 The problem **[C]**

### 2.2.1 One structural condition, shared by every process plant

Every large process plant, in every industry, shares the same shape. The product passes through many stages, many machines and many inspection devices. **Each of them records what happened in its own database, its own spreadsheet or its own log file, under its own vocabulary.**

A steel plant records a melt in one system, a casting sequence in another, a rolling pass in a third, and a surface defect in a fourth. A pharmaceutical plant records a compounding batch, an environmental reading, an inspection result and a deviation in four different places. A paper mill, a tyre plant and a bottling line each do the same thing with different nouns.

This is not a failure of any plant. It is what happens when systems are bought over twenty years, from different vendors, to solve different problems, each correctly.

But it produces three consequences that no plant escapes.

### 2.2.2 Fragmentation

Each production unit and each inspection device typically presents an interface that shows **its own data only**. There is no surface anywhere on which the whole plant is visible at once.

An operator can see the furnace. A different operator can see the mill. The inspection engineer can see the defects. **Nobody can see the furnace and the defect on the same screen**, which is precisely the pair that matters.

### 2.2.3 Dependency on scarce expertise

When a quality problem appears, the plant needs a particular person: someone with many years of experience who can hold the whole process in their head, connect a defect observed at the end of the line to a parameter deviation that happened at an early stage, and do it from memory or by reading logs by hand.

That person exists in most plants. They are also **rare, expensive, close to retirement, and a single point of failure.** When they are on leave, the investigation waits. When they leave permanently, a decade of pattern recognition leaves with them.

### 2.2.4 Unrealised operational value

The same fragmentation blocks the systematic reduction of downtime and the systematic improvement of yield and throughput.

The data required to find the cause already exists. It is simply not joined, and nobody has the weeks it would take to join it by hand for one investigation, let alone for every investigation.

### 2.2.5 What plants try instead, and why it does not hold

| The usual approach | Why it stops working |
|---|---|
| **Spreadsheets and manual extracts** | It works once, for one question, by one person. It does not survive that person's holiday and it cannot be scheduled |
| **A generic business intelligence tool** | It draws charts beautifully on data you have already joined. The joining is the entire problem, and it does not do it |
| **A bespoke data-science project** | It solves one plant's one question, at consulting rates, and produces an artifact nobody can extend without the consultant |
| **Waiting for the MES vendor to add it** | Ties the plant further into one stack, and the vendor's incentive is their own system's data, not the other five |

**None of these is stupid. All of them are what a competent plant does in the absence of anything better.**

---

## 2.3 What PlantProcess IQ is for **[C]**

### 2.3.1 The promise

> **Connect plant data. Discover quality drivers. Score risk earlier. Explain suspected contributors. Act with evidence.**

PlantProcess IQ is a **read-only intelligence layer** that connects to the databases the plant already has, imports their data continuously, joins them on the plant's own keys, and then makes the whole plant visible and analysable as one thing.

It does not replace anything. It reads.

### 2.3.2 The five layers of assistance

The product delivers value in five ascending layers. **Each one is independently useful.** A customer who never reaches layer three has still bought something that works.

| Layer | What it delivers | Available from |
|---|---|---|
| **1. Unified visibility** | Dashboards, charts, heatmaps and interactive filters that place data from every production unit and inspection device side by side, so an ordinary observer sees plant-wide patterns without expertise | Light |
| **2. Statistical intelligence** | Correlation and statistics relating every process parameter to every defect, downtime cause and performance measure across the whole plant, rendered so that hard-to-see relationships become visible | Pro |
| **3. Machine learning** | Model-based analysis that learns patterns, identifies probable contributors to quality problems, locates throughput bottlenecks and finds the drivers of recurring failures | Pro Plus |
| **4. Prediction** | Forward-looking statements grounded in learned patterns. A material that ran hotter than normal at an early stage is flagged as elevated-risk for a specific downstream defect **before that defect occurs** | Pro Plus |
| **5. Recommendation** | Suggested actions at later production stages to avoid the predicted outcome, and suggested operating adjustments where a failure mode is forecast | Pro Plus |

Two capabilities cut across all five.

**The assistant.** A conversational surface, so that a user who cannot configure a job, build a dashboard or read a statistical chart can still reach an answer by asking. It explains and cites. It never calculates. Available from Pro Plus.

**The value engine.** Every suggestion, prediction and statistical finding carries a quantified economic consequence, expressed as a bounded range with every input traceable. This is what converts a finding into a decision.

---

## 2.4 How it solves the problem **[E]**

### 2.4.1 The five steps a plant actually takes

| Step | What the plant does | What it gets |
|---|---|---|
| **Connect** | Points the product at its existing databases through read-only links. Oracle, SQL Server, MySQL, PostgreSQL, files, historians | Its own data, arriving continuously, without a single change to any source system |
| **Declare** | Its own engineer says what connects to what: this piece identifier is that material identifier; these readings belong to that batch | **The plant's own model of itself**, authored once, in its own vocabulary, and permanent |
| **See** | Builds pages and charts by dragging, with no code | The whole plant on one screen, for the first time |
| **Analyse** | Runs statistics and machine learning against outcomes it cares about | Suspected contributors, ranked by effect, with population, method and evidence |
| **Act** | Reads the finding, follows it back to the rows behind it, decides | A decision it can defend to its own management |

### 2.4.2 The differentiator: it joins what nobody else joins

**Correlating one system with itself proves nothing.** The furnace historian already shows the furnace. The value in this product exists entirely in the join: the parameter recorded at the start of the process and the defect detected at the end live in different databases, owned by different teams, described in different words.

PlantProcess IQ is built around that join. The customer's own engineer declares it once, because **he knows his plant and no vendor can know the schema architecture of every customer's plant.** From that moment every genealogy walk, every cross-source correlation and every chart that shows both together depends on that one declaration, and nothing downstream has to re-derive it.

This is the claim to make first, because it is the one a sceptical engineer can verify in ten minutes on his own data.

### 2.4.3 The differentiator that competitors cannot copy quickly

The product **refuses to compute when the data cannot support a defensible answer, and says exactly why.**

Before any analysis runs, the data behind it is measured against named thresholds: enough independent units, enough outcome events, enough class balance, fresh enough, complete enough. If any one fails, the analysis **abstains**, records the reason, and shows the customer which dimension blocked it and by how much.

That is not a limitation being disclosed. It is the feature.

**A tool that always produces an answer is a tool whose answers cannot be trusted**, because it produced one on the day it had nothing to work with. A plant engineer who has been given a confident wrong answer once will not believe the right one afterwards.

Competitors can copy the charts in a quarter. They cannot copy this quickly, because copying it requires being willing to show a prospect a red status. **No competitor in this market does that.**

---

## 2.5 Who buys it, and what each of them needs to hear **[E]**

Five people are in the room, or will be before a contract is signed. They want different things and are convinced by different evidence.

| Buyer | Their pain | What the product gives them | The proof they will accept |
|---|---|---|---|
| **Plant manager** | Losses and downtime that recur without explanation | Ranked, quantified priorities across the whole plant | A monthly value report on their own data |
| **Quality manager** | Defects and customer claims they cannot attribute | Defect-driver investigation across sources | A defect story walked end to end on their own material |
| **Process engineer** | Investigations done by hand in spreadsheets | Genealogy from finished unit back to origin, and cross-source analysis | One click from a defect to the upstream chemistry, on their own key names |
| **IT and operational-technology manager** | Security risk and integration burden | A read-only, one-way collector that never connects into the control network | The architecture, and the absence of any write path |
| **Chief financial officer and purchasing** | Uncertainty about whether it pays back | A bounded value model with every input traceable | A payback case computed on their own pilot data |

**The order matters.** The process engineer and the operational-technology manager are the two who can veto. Convince them and the commercial conversation becomes possible. Convince the chief executive first and then fail the engineer's inspection, and the deal dies more slowly and more expensively.

---

## 2.6 The value case **[E]**

### 2.6.1 The model

A finding becomes a decision only when it carries a number. The value engine computes that number deterministically from the plant's own cost inputs.

```
monthly impact =
      sum over affected material ( tonnes x penalty per tonne )
    + ( attributable production-impact minutes x downtime cost per minute )
    + ( yield loss x grade premium )

Every term cites its inputs.
A missing input produces "insufficient basis", not a guess.
The output is a RANGE derived from the assumption bands, never a single number.
```

Four properties make it defensible in a purchasing review.

| Property | Why it matters commercially |
|---|---|
| **Per-tenant cost inputs** | The plant's own euro-per-tonne, own downtime cost, own grade premium. Nothing is assumed by the vendor |
| **A bounded range, never a point** | A single confident number invites the question "prove that exact figure", which nobody can. A range with stated assumptions survives the question |
| **Every input drill-throughable** | The finance function can follow any figure back to the heats, units and events behind it |
| **It abstains** | Where the inputs are not configured, it returns "insufficient basis" instead of a number. A value model that always produces a number is a value model nobody believes |

### 2.6.2 The downtime distinction, which most tools get wrong

**Equipment-stopped minutes and production-impact minutes are different quantities**, and treating them as the same is the fastest way to produce a value figure a plant engineer will laugh at.

A twenty-minute stoppage at a rolling mill can be absorbed entirely by buffered material upstream and by slowing the casting machine. Nothing is lost. **Zero production-impact minutes.**

A three-minute stoppage on a casting machine's water pump can force the sequence to be rebuilt and the metal taken out of the cooling header. **Four to six hours of production-impact minutes.**

The product stores both quantities and uses the correct one per calculation. This detail is worth raising unprompted with an operations buyer, because it demonstrates that the product was designed by someone who has stood in a plant.

### 2.6.3 Price-to-value parity

The buyer's own test, and the right one:

> **The added value must be clearly greater than the price, or they will not buy.**

The commercial model is a per-site initial fee plus a monthly subscription, by tier, in euro. It is priced as a **plant platform, not as a per-seat analytics tool**, because that is what it is: one installation serving the whole site.

The full tier ladder, the capacity envelopes and the cost formula are Chapter 9. What matters commercially is the shape:

| Tier | What it is for |
|---|---|
| **Light** | A first line or a first department. Visibility, no statistics |
| **Pro** | One plant area, with statistics and correlation and SQL authoring |
| **Pro Plus** | A whole plant, with machine learning, prediction, recommendation, the value engine and the assistant |
| **Enterprise** | Multi-site, every connector, on-premise or air-gapped operation, self-hosted model, single sign-on, contracted support |

### 2.6.4 What the payback argument actually is

One well-founded finding on a mid-size plant, combining downgraded material and the production minutes attributable to it, lands in the tens of thousands of euro per month. A mature installation surfaces several such findings.

**The honest form of that argument is this:** the platform does not need to find many of them. It needs to find a small number, and the plant needs to act on a fraction of those, for the arithmetic to work comfortably in the plant's favour at any tier.

**And the honest limit of that argument is this:** those figures are the model's output on emulated data. **What a specific plant will recover is what that plant's pilot measures.** See 2.11.

---

## 2.7 Where we win, and where we do not **[E]**

### 2.7.1 Against the alternatives

| Compared with | The position |
|---|---|
| **Generic business intelligence** (Power BI, Qlik and similar) | They draw charts on data you have already joined. This product carries manufacturing semantics: genealogy, defect drivers, readiness, value and suggestions. The joining is the work, and it is the part they leave to you |
| **A bespoke data-science project** | A configurable product rather than a one-plant build. The mappings, pages and analyses are authored from the interface and are reusable, not delivered as a consultant's artifact |
| **A black-box analytics or AI tool** | Transparent mathematics, named methods, resolvable evidence handles, and an assistant that refuses to guess. This is what a sceptical engineer needs in order to trust any of it |
| **An add-on from a control or MES vendor** (Primetals, PSI, SST, Fero and similar) | Read-only and vendor-neutral. It connects around existing systems rather than deepening a commitment to one stack, and it reads the other five systems too, which is where the correlation lives |

### 2.7.2 What we do not claim

Stating this plainly is a selling advantage, not a concession. It is also required by Chapter 1.

- It does not replace the manufacturing execution system, the level-2 system, supervisory control or business intelligence.
- It does not write to any plant system and never participates in control.
- It does not produce guaranteed root causes. It produces suspected contributors with their evidence.
- It does not claim that any connector is available before that connector is proven. Unavailable connectors are visibly marked as planned.
- It does not claim that a demonstration on emulated data proves discovery on the customer's data.

**A buyer who hears these five sentences early will believe the rest of the presentation.** A buyer who discovers any one of them later will re-examine everything.

---

## 2.8 Brand **[C]**

| Attribute | Definition |
|---|---|
| **Product name** | PlantProcess IQ |
| **Company** | SOU Industrial Software |
| **Short name** | PPIQ |
| **Primary tagline** | Connect Your Plant Data. Understand Your Process. |
| **Secondary tagline** | Process-to-quality intelligence for manufacturing plants. |
| **Customer promise** | Connect plant data. Discover quality drivers. Score risk earlier. Explain suspected contributors. Act with evidence. |
| **Personality** | Industrial, intelligent, trusted, technical, calm, evidence-based. Plant operations plus data science, never flashy consumer technology |

**The elevator pitch.**

> PlantProcess IQ connects fragmented manufacturing, tracking, inspection, laboratory and enterprise data into one generic intelligence layer, so that process engineers can investigate quality problems faster, detect suspected process contributors, and build their own monitoring dashboards, without replacing any existing system.

**The portfolio context.** PlantProcess IQ is the first of five products from SOU Industrial Software, alongside manufacturing execution, quality execution, yard and warehouse management, and energy management. For a chief executive evaluating a young supplier, this matters: the company is building a plant-systems portfolio, not a single tool.

The visual identity, the palette, the typography and the logo system are ruled in Chapter 1.9 and applied in Chapter 10.

---

## 2.9 The language contract **[C]**

Bound by Chapter 1.5.9. Reproduced here because this is the chapter where the temptation to break it is strongest.

| Never say | Say instead |
|---|---|
| Guaranteed root cause | Suspected contributor, likely contributor, evidence-ranked factor |
| AI-powered prediction; production-ready AI | Machine-learning readiness; statistical learning; risk scoring; correlation analysis |
| Live support for a connector not yet proven | Connector capability shown honestly per source |
| We replace MES, level 2, SCADA or BI | We connect around existing systems and turn fragmented data into intelligence |
| Autonomous optimisation | Decision support and guided investigation, with human approval |
| It will save you X | A bounded range, with every input traceable |

**One forbidden phrase anywhere is a critical failure**, on the website, in a deck, in the product or in a room, regardless of how strong the surface carrying it is.

---

## 2.10 The objection playbook **[E]**

A sceptical buyer asks difficult questions. Each of these has an honest answer that is also a good one. **An answer that is honest but weak means the product needs work; an answer that is strong but false ends the relationship later.**

### 2.10.1 From the operational-technology and IT reviewer

| Question | Answer |
|---|---|
| Can this write to my systems? | No. There is no write path of any kind. Every outbound action is a message, an export or a webhook |
| Will it open a connection into my control network? | No. A collector you control pushes one way. The core never initiates a connection inward |
| What load will it put on my production databases? | Per-source row caps, statement timeouts, rate limits and approved time windows. Historical loading is throttled, resumable and visible, inside a stated budget |
| Where does my data go? | The analytical engines run inside your tenant. Nothing is sent anywhere to be calculated. The assistant model is self-hosted by default; where a private endpoint is used it receives only the question and the specific evidence, never your tables |
| Do you ship an administrator account we do not control? | No. The install provisions only the vendor support account. Your own administrator is created at commissioning |

### 2.10.2 From the process and quality engineer

| Question | Answer |
|---|---|
| Do I need to be a programmer to configure it? | For the common case, no. The authoring surface is drag-and-drop. SQL exists for the long tail so that an unusual requirement does not become a support ticket |
| Can I add a page, a chart or a filter later, myself? | Yes, from the interface. Filters are authored objects, not a fixed row of dropdowns, because every plant filters by different things |
| How much data before it gives a mature answer? | Dashboards and key figures work on day one. Advanced findings arrive as the readiness dimensions turn ready. A historical backfill collapses that timeline |
| Your engine refused to run. Is it broken? | No. It evaluated five named dimensions against published thresholds and one failed. Here is the dimension, the measured value and the threshold. It will compute when that is satisfied and not before |
| Can you lower the threshold so it runs? | A threshold is a governed change with a recorded justification, made by a person who owns the consequence. It is never lowered to produce a result |
| How do I know the number is right? | Follow it. Every figure traces to its query, its population and the rows behind it |

### 2.10.3 From the chief executive and purchasing

| Question | Answer |
|---|---|
| Is this artificial intelligence or simple mathematics? | Deterministic engines compute. The assistant only explains, with citations. That division is the reason the numbers are reproducible |
| Large language models make mistakes. How can I depend on it? | Because it neither computes nor ranks. It cannot render a number it cannot cite; the guard rejects the sentence before display |
| What does each licence actually grant? | A stated capability set and a stated capacity envelope. Chapter 9. Switching tier visibly adds and removes capability in the running product, which we will show you |
| How do you control the licence? | A cryptographically signed token, not a database row anyone can edit |
| What happens at expiry? | Warnings, then a grace period, then read-only access to what you built. Your data is never destroyed by expiry |
| **You are a new company. Will you exist in three years?** | **A fair question, and the honest answers are structural: the product is read-only, so removing it breaks nothing in your plant; your data stays in your database; the authored model is exportable; and on-premise and air-gapped deployment mean you are not dependent on our hosting. We are also building a five-product portfolio, not a single tool** |
| Will it pay back more than it costs? | The value model computes a bounded range from your own cost inputs, with every input traceable, and abstains where it cannot. The figure that matters is the one your pilot measures |

---

## 2.11 Selling before the value engine exists **[N]**

*This section exists in no draft. It is written because an honest assessment of the product found one gap that is narrative, not technical, and improvising it in a room will read as evasion.*

### 2.11.1 The gap, stated plainly

Of the professional viewpoints that judge this product, every one improves materially as engineering work lands. **One does not: the economic buyer.**

The reason is structural. What convinces a chief executive is a euro figure computed on their own data and a licence tier switching live in front of them. Both are scheduled work rather than present capability. **No amount of additional engineering on anything else moves that viewpoint**, so the conversation has to be designed rather than left to the moment.

### 2.11.2 What not to do

**Do not improvise.** A chief executive asking "what will this save me?" and receiving a hesitant answer assembled on the spot concludes that the number does not exist. That conclusion is correct, and it is much worse than the truth.

**Do not produce a figure the product cannot yet reproduce.** A number stated in a room and not reproducible in the product two weeks later costs more than saying nothing.

**Do not defend the gap.** Defending it makes it the subject of the meeting.

### 2.11.3 The three-move sequence that works

**Move one: state the model, not the number.**

> "The way this pays back is arithmetic, not opinion. A finding names affected material and attributable production minutes. Your own cost per tonne and cost per minute turn that into a range. Every input is one click from the rows behind it. Here is the model."

Showing the arithmetic is stronger than showing a figure, because the buyer can put their own numbers in it mentally while you are talking, and the figure they arrive at is one they trust.

**Move two: give the honest status of the number, unprompted.**

> "What I am not going to do is quote you a saving from our emulated plant as though it were yours. That number would be arithmetic on somebody else's data. The figure that matters is the one your pilot measures, on your material, with your costs."

Volunteering this converts the weakest point in the presentation into a demonstration of the product's central claim. A buyer who has been in many vendor meetings has never heard this, and it is the moment the honesty argument stops being a slogan.

**Move three: give them something to sign against instead.**

> "So what I would propose you evaluate is a pilot with a defined measurement: connect these sources, run for this period, and the value model reports on your own data. You are not signing against my number. You are signing against a method that produces yours."

### 2.11.4 What carries the commercial weight in the meantime

Until the value figure is real, four things carry the argument, and each is present, demonstrable and genuinely uncommon:

| What | Why a buyer should care |
|---|---|
| **The readiness gate** | Five named dimensions with published thresholds, and a refusal that explains itself from the database alone. It is the most direct evidence available that this product will not fabricate the number it eventually gives them |
| **The cross-source join** | A parameter from one vendor's database and a defect from another's, on one axis, joined by their own engineer on their own keys |
| **The genealogy thread** | Walking from a finished unit back to its origin in both directions, with shared attribution correctly weighted where a unit spans two parents |
| **The honest abstain, live** | Running an analysis in the room that refuses, and explaining why that is the product working |

### 2.11.5 The sentence to have ready

For the planted-signal demonstration, the exact wording matters, and this is it:

> "It recovered a planted validation signal and rejected a null control. **That validates the method.** Return on investment is what your pilot measures."

Any stronger claim about that result is a forbidden claim under 2.9.

---

## 2.12 The demonstration narrative **[E]**

The order of a demonstration is a commercial decision, not a technical one. This is the argument behind the sequence; the executable script is a separate, dated instrument.

### 2.12.1 Open with the disclosure

Once, early, unprompted:

> "This instance runs on our emulated multi-source plant, so that we spend our time on the product. On your installation it starts empty and fills through the read-only links, which I will show you now."

Said first, it is confidence. Discovered later, it is a credibility problem.

### 2.12.2 The order, and why

| Order | Beat | Why here |
|---|---|---|
| 1 | Connections and a live import | Proves the only door. The customer watches their kind of data arrive |
| 2 | The join, declared by an engineer | The differentiator. This is the moment the product stops resembling a business intelligence tool |
| 3 | Genealogy walk on the customer's own key names | The "it speaks my plant" sensation, and it needs no engine run |
| 4 | The workspace: cross-filter, drill, chart from several sources at once | Visually the strongest beat and it requires no completed analysis |
| 5 | Findings, or the honest abstain | Whichever is true on the day. **Both are good beats.** One shows a result, the other shows the moat |
| 6 | The assistant, cited | Reinforces that the model explains and the engines compute |
| 7 | The value model, per 2.11 | Last, because it is a conversation rather than a screen |

**Beat 4 is the safest strong beat in the presentation** because it depends on no engine state, and beat 5 is the only beat that is strong whichever way it goes.

### 2.12.3 The strongest artifact you can build

> **A finding computed on data the customer watched arrive is the strongest sales artifact available.**

If the demonstration can be sequenced so that the import at beat 1 is the data the analysis at beat 5 runs on, the presentation stops being a tour and becomes a proof. This is worth substantial preparation, because nothing else in the meeting has the same effect.

### 2.12.4 The four contingencies

Say the line, do not debug in the room.

| If | Say |
|---|---|
| A source is down | "The connector reports it unreachable. That is the same honesty you get in production" |
| The service needs restarting | "Let me restart the service, one moment" |
| A view is not certified | "That view is still being certified. Here is the one beside it" |
| Anything red appears | "I will note that and follow up" |

---

*End of Chapter 2.*

*Every claim in this chapter is bound by Chapter 1.5. Where a later chapter specifies a capability differently, the later chapter governs the product and this chapter is corrected to match it. Marketing never leads engineering in this document.*
