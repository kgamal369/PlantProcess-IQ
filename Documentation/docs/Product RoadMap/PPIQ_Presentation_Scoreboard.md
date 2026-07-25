# PPIQ Presentation Readiness Scoreboard

**25 July 2026, 17:15 | Scored against the six points of the intended presentation and the five buyer viewpoints named by the presenter**

---

## How this is scored

Bands are the ones already in `rules.txt` Part C, unchanged:

| Band | Score | Meaning |
|---|---|---|
| **Critical** | below 55 | Missing, broken, or dishonest |
| **Needs work** | 55 to 69 | Present but incomplete or fragile; fails an edge state or a sceptical click |
| **Solid** | 70 to 84 | Complete, stable and honest for demonstration scope |
| **Strong** | 85 and above | Production-grade beyond demonstration |

Two of the standing rules apply throughout and change the reading of everything below:

1. **The headline is the lowest persona score, never an average.**
2. **A criterion with no reproducible evidence cannot exceed Needs work.** Almost nothing in this build has been walked in a browser since the fixes landed, so several scores are capped by that rule alone rather than by the code.

---

# PART 1 - THE SIX PRESENTATION POINTS

## Point 1 - Three premade analysis pages, one per type

**Intent:** three impressive, data-rich pages with charts, donuts, pies, KPI tiles, filters, graphs and heatmaps, in Qlik style.

| Type | Page | State | Score |
|---|---|---|---|
| 1.1 Raw data | PRODUCTION_OVERVIEW, 8 widgets | Widgets render. KPI tiles fixed today. Chart first-paint fixed. Associative panel live. **But roughly half of widget queries return zero rows and that has never been diagnosed.** Material Mix donut returned a 14x14 surface and was never re-measured. | **58 Needs work** |
| 1.2 Statistics and correlation | CORRELATION_EXPLORER | The page exists. **The data behind it does not.** All 320 correlation rows sit under outcome keys the registry does not declare, and no analysis run has ever completed. A statistics page with nothing to plot is not a statistics page. | **48 Critical** |
| 1.3 AI and ML | MODEL_INSIGHTS | Not found in the database scripts or the seed. Existence unconfirmed at this depth. | **Unverified** |

**Point 1 score: 52 Critical.**

The gap is not the charts. The charts are in good shape after today. The gap is **data behind them**. Two of your three showcase pages depend on engine output that does not exist.

### What has to happen

Walk the three pages in the browser and count. For every widget that returns zero rows, decide now: fix the query, or remove the widget from the page. A page of eight widgets where four are empty reads as a broken product; a page of four widgets that all carry data reads as a focused one.

---

## Point 2 - The five no-code authoring surfaces

This is the area your own A11 persona flagged hardest, and it has moved more than any other since that assessment.

| Surface | Route | Reality | Score |
|---|---|---|---|
| **S1 Data preparation** | `/prep/canvas` | **A genuine node canvas.** Typed ports, key/number/text/date colour coding matching your contract, minimap, controls, dark background. Reviewed as the best-built surface in the product. Routed today. | **78 Solid** |
| **S2 Dashboard and widget** | workspace | Grid, drag, resize, persistence all real. Chart-type switching real. Associative selection real. **But there is no Add-widget entry on the page.** `WidgetBuilderWizard` exists and is rendered by nothing but itself. The central no-code act, "create a widget without code", has no click path. | **55 Needs work** |
| **S3 Analysis authoring** | `/analysis/toolbox` | Blocks render, payload updates, parity line now genuine, run submits, gates return. Routed today. | **72 Solid** |
| **S4 AI and ML authoring** | none | **No distinct surface exists.** There is `/ml-readiness` and the shared analysis route. As a separate authoring experience with a model toolbox, it is absent. | **40 Critical** |
| **S5 Plant data log and alerting** | `/alerting` | Routed and present. **Never reviewed at any depth.** Unknown styling, unknown wiring, unknown whether rules actually evaluate. | **Unverified** |

**Point 2 score: 58 Needs work**, and it is being held up by S1 and S3.

### The correction worth making to your own assessment

Your A11 persona recorded, on 20 July, that "UI-1 and UI-3 are forms; no node-wiring foundation exists in the dependency tree". **That is no longer true.** A real wiring canvas with port typing landed and is now routed. That is a genuine advance and you should demonstrate it deliberately rather than apologising for the surface.

### The two honest sentences you need

For S2: *"Widget authoring is wired and the grid is live; the creation wizard mounts in the pilot."* Do not open a workspace and hunt for an Add button that is not there.

For S4: *"The machine-learning tier authors through the same governed surface; its dedicated toolbox is pilot scope."*

---

## Point 3 - The engine

**Intent:** show the engine.

**Measured today: the engine has never completed a single run.** Roughly forty-five runs are on record, spanning four dates, across every declared outcome. Every one of them is `Blocked`.

The three outcomes that carry real data are blocked for real, defensible reasons:

| Outcome | Values | Blocking dimension |
|---|---|---|
| `defect.rate_per_m2` | 91,839 | Field completeness 46.5% against an 85% bar. Four of five gates green. |
| `defect.class` | 51,691 | Minority-class balance 0.002%. One real class plus three stray rows. |
| `defect.severity` | 51,691 | Same, but **this one is an engine defect**: the loader never reads the ordinal column, so a healthy nine-value distribution is invisible to it. |

**Point 3 score: 62 Needs work.**

That score is not lower for one reason, and it is an important one: **the abstention itself is demonstrable, and it is your strongest asset.** A panel showing four green dimensions with real numbers, 2,441 independent heats and 91,417 outcome events, and one honest red naming field completeness at 46.5 percent against an 85 percent bar, is a better artefact than a correlation you engineered the data to produce.

### What you cannot do

You cannot show a completed correlation with a q-value on this dataset. If asked "show me one that finished", the honest answer is that this dataset has never cleared the gate.

Prepare that sentence. Do not improvise it.

---

## Point 4 - The assistant

**Intent:** two or three questions, logical answers, then the learning-curve sentence.

Your bar here is realistic and reachable. Grounded questions were previously verified returning real cited answers with six citations naming actual mappings and connections.

Fixed today: the chat had **no stylesheet at all** and rendered as unstyled stacked divs; Enter did not send; a transport failure was displayed to the customer as "Insufficient evidence"; and one suggested prompt named a defect class that exists nowhere.

**Point 4 score: 68 Needs work.** Capped only because none of today's fix has been seen in a browser.

### Three things to know in the room

- **Citations expand a provenance handle. They do not open a source row.** No evidence-row route exists. This is honest and deliberate. Do not promise a click-through.
- **The predictive question does not reliably refuse.** Do not promise a refusal beat. Lead with the cited answer.
- The retrieval layer runs on the extractive baseline; the vector extension is unavailable in the running database.

---

## Point 5 - The full journey walk

Fifteen steps. Your own journey document carries over two hundred verification steps and a sixteen-item gap register.

| Journey segment | State |
|---|---|
| Steps 1 to 3, connect, schedule, import | Historically the riskiest, four connector cursor defects found and fixed. Not re-walked since. |
| Step 4, mapping | Real. The canvas now gives it a visual surface. |
| Steps 5 to 6, load and project | Real, seam-proved end to end previously. |
| Step 7, dashboards | See Point 1. |
| Steps 8 to 10, analysis and findings | Authoring works; **results are empty**, see Point 3. |
| Steps 11 to 13, AI and ML tier | Thin. No dedicated surface. |
| Step 14, supervisor | v0 read-only, honest empty state, styling fixed today. The page states its own guardrail in the interface, which is worth reading aloud. |
| Step 15, assistant | See Point 4. |

**Point 5 score: 60 Needs work.**

The journey holds structurally. It thins sharply at steps 8 to 13, which is exactly where a process engineer will press hardest.

---

## Point 6 - The website

Four components, built weeks ago, were mounted on the home page today: the architecture scroll graphic, the golden-thread scroll graphic, the integration ecosystem, and the ROI calculator. Until today they were imported by nothing.

A second defect surfaced while wiring them: the ROI call to action targets an anchor that did not exist on the page, so it would have scrolled nowhere even once mounted. That is now fixed.

**Point 6 score: Unverified.** These four components have never rendered in a browser. Until you scroll the page yourself, this point has no score.

---

# PART 2 - THE FIVE BUYER VIEWPOINTS

Scored across the whole product, not per scene. This is the view that decides whether the room says yes.

## Developer and maintainer - 62, Needs work

**Strong:** one canonical project layout; a design system that is enforced by an architecture test, not by convention; a truth gate that forbids swallowed test failures; typed error taxonomies.

**Gaps:**
- The continuous-integration suite **enumerates rather than executes** the visual, end-to-end and accessibility suites. The guard that forbids this is itself wired to nothing and would fail if run.
- The backend suite reports pass with **58 percent of tests skipped**, including the entire connector truth-contract family.
- A static contract audit carrying five critical and four high findings **exits zero**, so it reports pass. Its contents have never been read.
- **Three implementations of one correlation capability** are registered; two are superseded generations, one behind a configuration flag.

## Process and quality engineer - 57, Needs work

**This is your headline persona by your own scoring law, and it is the one in the room.**

**Strong:** genealogy walks both directions; the associative selection model is real, which most business-intelligence tools do not have; population and exclusions are shown; honest empty states everywhere.

**Gaps:**
- **Roughly half of widget queries return zero rows.** This persona will notice within a minute.
- **No analysis has ever completed**, so the discipline they care about most produces nothing yet.
- Widget creation has no click path.

## Software engineer and configurator - 65, Needs work

**Strong:** six connector classes; real staging with cursors and batches; mapping definitions as versioned artifacts with immutability and rollback; safe-SQL enforcement at the execution layer, not the interface; a real node canvas with port typing.

**Gaps:**
- Add-widget entry absent.
- No dedicated model-authoring surface.
- Throttling controls may not be exposed in the connection form even though the backend supports them.
- Schema-drift detection unverified on this build.

## CEO and economic buyer - 51, Critical

**This is your weakest buyer-facing score and it is not close.**

- **License tier switching is not demonstrable.** Your own Rule 5.2 requires showing features appear and disappear as tiers change. There is no tier-to-feature matrix, so the feature cannot be built, let alone shown.
- **The value engine is the largest doctrine-to-build gap in the product.** No bounded euro range reproduces on demand.
- Several advisory surfaces load canned request data rather than real pending work.
- Roles exist; role-scoped view and edit differences are unverified on this build.

**If the economic buyer is in the room, this is where you will be weakest.** Prepare the roadmap framing for licensing and value deliberately, because improvising it will sound like evasion.

## Infrastructure engineer - 45, Critical

**Nothing in this persona has been measured.**

- Concurrency and the hundred-job claim: never tested.
- Sizing: no telemetry, no load rig. Every figure in the sizing doctrine is an estimate.
- Backup and restore: never drilled.
- The server runs older code; ten commits sit unpushed on the laptop.
- The reverse-proxy configuration references stale container targets and its host source file was deleted.
- The vector extension is unavailable in the running instance.

**By your own scoring law the headline for the whole product is this number: 45.**

---

# PART 3 - WHERE YOU ACTUALLY ARE

| Viewpoint | Score | Band |
|---|---|---|
| Developer | 62 | Needs work |
| Process and quality engineer | 57 | Needs work |
| Software engineer and configurator | 65 | Needs work |
| CEO and economic buyer | 51 | **Critical** |
| Infrastructure engineer | 45 | **Critical** |
| **Headline, lowest persona** | **45** | **Critical** |

That headline matches the external senior assessment of 45 for shipping readiness. Nothing today changed it, because nothing today touched infrastructure, and infrastructure was already your first M2 item by your own decision.

**But the headline is not the demo score.** For a demonstration where you control the path, the number that matters is the process and quality engineer at 57, because that is the person in the room and the one who will click where you did not plan.

---

# PART 4 - THE GAPS, RANKED BY WHAT THEY COST YOU IN THE ROOM

### 1. Half the widgets return no data
Highest cost, cheapest fix. This is a data or filter question that has never been diagnosed. Either fix the queries or remove the empty widgets. A curated page of widgets that all carry data beats a comprehensive page that is half empty.

### 2. No completed engine run
You cannot show a finished correlation. Turn this into the abstention beat, which is genuinely strong, and prepare the exact sentence for "show me one that finished".

### 3. No add-widget click path
Doctrine surface two has no creation act. One scripted sentence, or mount the wizard.

### 4. License switching not demonstrable
Rule 5.2 is your own requirement and it cannot be met. Write the cut down, as you did for the login scene, so it is a decision rather than an omission.

### 5. Nothing is runtime-verified
Everything fixed today is code-verified and gate-verified only. **This is the single largest risk to the presentation**, and it is entirely removable by walking the consolidated pass once.

### 6. Scene 12 never rendered
Four components mounted today, unseen. Walk it or cut it.

### 7. Infrastructure unmeasured
Not fixable before the demonstration. Prepare the roadmap framing and do not over-claim any number.

---

# PART 5 - WHAT IS GENUINELY STRONG

You should walk in knowing these, because under pressure people defend their weaknesses and forget to sell their strengths.

- **The readiness gate.** Five named dimensions, published thresholds, per-gate evidence reconstructable from the database alone. No competitor shows a prospect a red status. This is the moat.
- **The associative selection model.** Real possible-versus-excluded state across widgets. Most business-intelligence products do not have this.
- **The genealogy layer.** Bidirectional walk, weighted attribution summing to exactly one per child, enforced by a database trigger.
- **The multi-grain canonical model.** Your outcome store carries native grains for slab, heat, cast, packaged lot, raw material, aluminium roll and billet, compound batch, tyre unit, customer roll, batch and lot. That is Rule 1 proven with data, not asserted in a slide.
- **The visual join canvas.** A real typed-port node canvas, reviewed as the best-built surface in the product.
- **The honesty contract carried as stored data**, not as interface copy: every finding persists its own framing and records that no language model participated in the compute path.

---

*Scored from the source and from live database and API measurement taken between 15:14 and 17:15 on 25 July 2026. Every claim is traceable to a file, a query result, or an endpoint response.*
