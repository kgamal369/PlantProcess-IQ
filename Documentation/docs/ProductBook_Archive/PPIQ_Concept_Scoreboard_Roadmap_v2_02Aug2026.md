# PLANTPROCESS IQ - CONCEPT, SCOREBOARD AND DELIVERY ROADMAP

**Version 2.0 - 2 August 2026**
**Supersedes:** Concept/Scoreboard/Roadmap v1.0 and Implementation Review Rev D, both of 2 August 2026
**Design authority:** Master Design Chapters 1 to 6 (v4.3 to v4.6)
**Implementation baseline:** `UltimateAudit_29Jul2026_233112` - 2,051 files, 343,024 lines, read file by file

> **Core law:** temporary data may be presentation-only; temporary internal implementation may sometimes be; **temporary product identity, UX, workflow or behaviour may never be.**

---

## 0. WHAT CHANGED IN VERSION 2, AND WHY

Version 2 merges two independent reviews. Rev D contributed four things that v1 lacked and that are adopted here in full:

| Adopted from Rev D | Why it is better |
|---|---|
| **A `Green when` acceptance clause on every epic** | An epic without an exit test is an intention, not a deliverable. Extended in v2 to M2 as well |
| **A classification verb on every Keep item** - KEEP, KEEP+extend, KEEP+harden, KEEP+migrate authority, KEEP as demo data | "Keep" alone hides the fact that six of the thirty need their *authority* moved even though their *code* survives |
| **Milestone and severity tags on every gap** | Turns a gap list into a routing table |
| **Eighteen concrete M3 topics organised around site reality** | Sharper than v1's reserved-capacity framing, and correctly centred on what a soft test actually produces |

Version 2 retains four things from v1 that Rev D does not carry, all of which you explicitly asked for:

| Retained from v1 | Why it matters |
|---|---|
| **Scoring by area and by persona** | You asked for scoring by aspect and persona. Rev D reports five headline metrics only |
| **Hour estimates on the enhancement list, grouped so groups can be cut whole** | An uncosted enhancement cannot be traded against anything |
| **File, class and line-level evidence** | Depth means artifact level. `SafeSqlValidator, 295 lines, token-boundary validation` is checkable; "Safe SQL validator" is not |
| **The NOT-list, the decision list and the phenomena manifest** | The cuts and the open rulings are as load-bearing as the plan |

And version 2 adds three things neither document has: **the M2 arithmetic audit** (section 3.3.2), **the parallel-lane structure** that 400 team-hours implies (section 3.2.3), and **the risk register** (section 3.6).

---
---

# CHAPTER 1 - CONCEPT AND STATUS

## 1.1 The product

PlantProcess IQ is a generic, read-only, evidence-grade process-to-quality intelligence platform for manufacturing plants in any process industry. It connects fragmented plant databases through a one-way collector, stages source-shaped copies, maps them into a canonical model through a customer-authored versioned mapping, then provides unified visibility, statistics, machine learning, practice learning, prediction, historically supported remediation, a value engine and a grounded assistant - all behind a readiness gate that refuses to compute when the data cannot support a defensible answer.

> Deliver a BI-class analytical and authoring experience over a **permanent, customer-authored plant model**, then use that model to learn the plant's process behaviour, discover evidence-backed relationships, predict downstream outcomes, identify historically supported remediation and measure what happened afterwards.

The dashboard is the interaction layer. **The plant model, the genealogy, the evidence chain, the statistical discipline, practice learning, prediction, remediation and the feedback loop are the product.**

## 1.2 The rules that govern every hour of the next 800

**Rule 1 - Generic only.** No line, word, page, component, schema object, list or code branch prepared for any specific dataset, industry, plant or customer. Three doors only: import, registry, authoring.

**Rule 2 - The plant schema starts empty**, provable in one query.

**Rule 3 - The journey is the product.** One journey, J1 to J15.

**Rule 4 - The latest concept is the only concept.** The replacement lands with the deletion, in the same change. A fix that exists only as data does not exist.

### The Visible Contract / Hidden Implementation Split

| Layer | M1 requirement |
|---|---|
| UI appearance | **100 percent final design** |
| UX flow and workflow | **100 percent final design** |
| Route, naming, terminology | **100 percent final design** |
| Button and control behaviour, and control location | **100 percent final design** |
| Visible state, refusal and error semantics | **100 percent final design** |
| API shape consumed by the UI | Prefer final |
| Internal service | May use a compatibility adapter |
| Physical database schema | May remain temporary |
| Algorithm implementation | May remain partial or prepared |
| Production scaling; RLS, HA, licence internals | Defer unless visible |
| Presentation data | May be synthetic and prepared |
| **A fake hardcoded product answer** | **Never allowed, at any milestone** |

The permitted temporary shape, and the only one:

```
FINAL UI  ->  FINAL API-shaped service  ->  compatibility adapter  ->  current persistence
                                            ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                                            M2 replaces these two. The top two never move.
```

### The Customer Contract Continuity Test

Snapshot every presentation surface on the last day of M1. Compare after M2.

| Allowed after M2 | Not allowed after M2 |
|---|---|
| The same Add Widget flow, faster, more options | Add Widget replaced by something else |
| The same assistant dock, deeper answers | The chat box became a separate page |
| The same readiness screen, prediction now from live ML | The engine screen turns out to have been removed |
| The same logging page on the real logging engine | Retention became editable where the design says read-only |

### The two gates every backlog item passes

**Gate A** - will the customer see it in one of the six beats, or does a visible feature depend on it? If no, it is M2 or M3.
**Gate B** - does the current implementation match final design? If no, **no patch on the wrong architecture**; build the smallest slice of the final architecture that serves the demonstration.

## 1.3 The prepared presentation database - ruled

> The presentation database may contain prepared **data** and prepared **state**. It may not contain unique product **behaviour** that exists nowhere in source control.

| Allowed | Never allowed |
|---|---|
| Synthetic plant data | A React change made only for the presentation |
| Precomputed correlations and risk rows | A trigger secretly generating answers |
| Seeded dashboards, analysis definitions, genealogy | Hand-edited widget SQL with no seed or migration behind it |
| A prepared assistant evidence corpus | A `/demo/get-perfect-answer` endpoint |
| | Presentation-only button behaviour |

**Three databases, one codebase, one migration chain:**

```
                    ONE CODEBASE
                          |
                 ONE MIGRATION CHAIN
                          |
     +--------------------+--------------------+
     |                    |                    |
  ppiq_app       ppiq_acceptance_empty   ppiq_presentation
    DEV               EMPTY TEST             DEMO DATA
```

`ppiq_acceptance_empty` exists to prove Rule 2 in one query, which nothing currently does.

**Open issue for M1-A:** `Rebuild-PresentationDb.ps1` restores from a **13 July** snapshot. Corrections made against live `ppiq_presentation` between 14 and 27 July survive a rebuild only if they became scripted steps. Rebuild into scratch, diff objects and row counts against live. That diff is the exact list of fixes that exist only as data.

## 1.4 Status, with denominators

A single number without a denominator is how two competent reviewers arrived at 28 and 56 for the same repository.

| Metric | Value | Purpose |
|---|---|---|
| **Product surface coverage** | **62%** | Explaining the estate |
| **Effort-weighted final-design conformance** | **31%** | Planning M2 and M3 hours |
| **Six-beat presentation readiness, today** | **62%** | The only number that matters this month |
| Design completeness of Chapters 1 to 6 | 94% | The map is finished; the territory is not |

**The four buckets, reconciled across three independent assessments:**

| Bucket | Karim | Senior | Claude | **Adopted** |
|---|---|---|---|---|
| Keep | 25% | 33% | 28% | **30%** |
| Rework, modify, enhance | 20% | 23% | 20% | **22%** |
| Retire | 15% | 9% | 13% | **10%** |
| Missing | 40% | 35% | 39% | **38%** |

All three converge on one correction: **the delete pile is smaller than feared.** Counted as functionality rather than files, retirement is 8 to 12 percent.

## 1.5 The status fact that matters most

**The hardest conceptual work finished before the hardest implementation work started.** Nearly every decision now has an answer in Chapters 1 to 6. The product is no longer being invented during coding, which is the condition that produced the drift now being corrected.

---
---

# CHAPTER 2 - SCOREBOARD

## 2.1 THIRTY TO KEEP

Classification verbs are load-bearing. **KEEP** means untouched. **KEEP+migrate authority** means the code survives but its source of truth moves in M2 - six items carry this and they are the ones most likely to be mistaken for finished.

| # | Item | Evidence | Verdict | Ref |
|---|---|---|---|---|
| 1 | Layered backend separation | Domain / Application / Infrastructure / Api / Workers / Analytics.Core / Analytics.Engine; 628 files, 7 projects | **KEEP** | Ch3 4.6 |
| 2 | `ReadinessGate.cs` | Five dimensions; thresholds as a record; overall = worst via `Math.Max`; every dimension returns a reason from measured value and threshold; `CanRun => Overall != Blocked` | **KEEP + single authority** | Ch4 5.4.3 |
| 3 | `StatisticalDiscipline.cs` | `RankByEffect` by absolute effect size, p-value only as tie-breaker; `BenjaminiHochberg.Adjust(q=0.05)` | **KEEP** | Ch4 5.5.4 |
| 4 | `SafeSqlValidator` | 295 lines; SELECT and WITH only; token-boundary validation so `created_at` stays legal; forbidden set covers DDL, DML, COPY, large-object functions, `dblink*`, `pg_sleep*`, `pg_catalog`, `information_schema`, `xp_*` | **KEEP + extend** | Ch4 5.2.12 |
| 5 | `SqlAllowlistProvider`, `SafeSqlCommentStripper` | Dynamic registered table names; comments stripped before validation | **KEEP** | Ch4 5.2.12 |
| 6 | `ThrottlingDataSourceReader` | Every one-shot read evaluated against load budget and rate limiter **before it reaches the customer source** | **KEEP + harden** | Ch4 5.3.2 |
| 7 | Read-only acquisition boundary | Edge collector, outbound-only posture | **KEEP** | Ch2, Ch6 6.1.1.2 |
| 8 | Incremental staging foundation | Watermark and batch-oriented acquisition; `staging_records` FK `ON DELETE RESTRICT`, `raw_json jsonb` | **KEEP + harden** | Ch3 DF2/DF3 |
| 9 | `material_units` key design | Unique `(site_id, material_code)` **plus a filtered unique** `(source_system, source_record_id)` - projection idempotent without forbidding rows lacking source identity | **KEEP** | Ch3 4.5.4 |
| 10 | `genealogy_edges` attribution | `contribution_weight numeric(9,6)`; **trigger enforcing sum = 1.0 per child**; covering index `(child_material_unit_id, is_transition, contribution_weight)` | **KEEP** | Ch3 DF6 |
| 11 | `parameter_observations` time model | Both `observed_at_utc` and `observed_at_local` plus `plant_time_zone_id` | **KEEP** | Ch3 4.5.4 |
| 12 | `plant_data_log` denormalisation | Stores comparator and limit beside the rule reference, so editing a rule does not rewrite history | **KEEP - and document as design** | Ch3 4.5.15 |
| 13 | Canonical entity foundation | 37 domain entities; genuine multi-grain absorption of 13 native product types | **KEEP + migrate authority** | Ch3 4.5.4 |
| 14 | Visual Join Canvas | `VisualJoinCanvasPage.tsx`, 784 lines; typed ports on the design's exact colours; MiniMap; schema tree with key-candidate markers; compiled-query pane; preview; publish; debug log | **KEEP + finish** | Ch4 5.2.6 |
| 15 | `BuildSafeSelect` generator | Operator and arithmetic whitelists; identifiers through one regex and quoted on emit; **values always bound as parameters**; `NULLIF(x,0)` around division | **KEEP** | Ch4 5.2.12 |
| 16 | Interface-server whitelist parity | The UI's operator list is byte-identical to what the server enforces - an illegal state is unreachable, not rejected after the fact | **KEEP - and reuse the pattern** | Ch4 5.2.7 |
| 17 | `Prep:StagingSchema` config key | Schema name is configuration, not a literal, so the M2 rename is one change | **KEEP** | Ch3 4.5.2 |
| 18 | `WidgetAuthoringPanel.tsx` + `.css` | 549 + 214 lines; every list from `GET /analytics/dashboard/metadata`, zero plant literals; all 23 rendered class names have rules in both directions; mounted at `InteractiveWorkspacePage.tsx:203`, `onEdit` wired at `:223` | **KEEP + align to final S2 shell** | Ch4 5.1.10 |
| 19 | Wiring-to-SQL dual mode and the compiled-SQL path | Strategically correct and the most sales-visible engineering in the product | **KEEP** | Ch4 5.2.2 |
| 20 | Widget query execution path | Run-test, preview, returned columns | **KEEP + normalise API** | Ch3 DF7 |
| 21 | KPI rendering correction | `PPIQ-WIDGETFIX` branch using `MetricCard`; rate, score and average measures averaged, max and min extreme, else sum | **KEEP** | Ch4 5.1.8 |
| 22 | Temporal cross-filter | `widgetSelectionMap.ts`: `TEMPORAL_DIMENSIONS`, `isTemporalDimension`, `timeDimensionRange` with correct ISO-8601 week arithmetic and null on unparseable values | **KEEP as canonical** | Ch4 5.1.13 |
| 23 | Associative engine | `useAssociative`, `AssociativeProvider`, cross-widget selection map, real possible-versus-excluded state | **KEEP + polish** | Ch4 5.1.3 |
| 24 | `DashboardWidgetQuerySafetyRegistry` limits and chart grammar | MaxRows 100/500, RawRowLimit 50k/250k, Lookback 90/730. Limits and chart types correctly **closed** as product grammar | **KEEP - but the dimension list inside it is a gap, see 2.2 #5** | Ch4 5.1.5 |
| 25 | Dashboard persistence API surface | `DashboardEndpoints.cs`, 24 routes with real handler names; `useDashboardGridLayout` and `useDashboardLayoutPersistence` genuinely work | **KEEP + migrate authority** | Ch3 DF7 |
| 26 | Design system and ratchets | 22 `Standard*` primitives; `uiConformance.baseline.json` as a **baseline** so existing debt freezes while any new file starts at zero; PPIQ-T09 and PPIQ-T11 | **KEEP + promote to CI** | Ch2 3.12 |
| 27 | Blocking CI stages and the truth gate | `Jenkinsfile` stage 3 backend BLOCKING, stage 4 frontend unit BLOCKING, stage 5 E2E BLOCKING via an ephemeral `ppiq-ci` stack that health-gates then runs the full Playwright suite; plus `CiPipelineTruthGateTests.cs` | **KEEP + add visual and a11y** | Ch6 6.1.3 |
| 28 | Assistant tool architecture | `AssistantService`, `ToolRegistry`, `ITool`, `FetchFindingTool`, `RunKpiTool`, `OpenSuggestionTool`; role scopes; structured refusals | **KEEP** | Ch4 5.7 |
| 29 | `GroundingService` and `AssistantEgressGuard`, plus the real-model seam | Blocks any sentence with a number absent from retrieved claims; blocks causal and value language; honest refusal when nothing grounded remains. `Top15RealAssistantModel` behind five environment variables with extractive fallback | **KEEP + productise** | Ch4 5.7.3 |
| 30 | Presentation reproducibility toolkit and website foundation | `Rebuild-PresentationDb.ps1` with a hard guard; `Seed-PresentationDashboards.v2.ps1` seeding **seven dashboards and ~29 widgets**; six emulated source engines. Website: 18 keep, 9 enhance, 1 replace, including `ConnectorHonestyBlock` and `PositioningTruthBlock` | **KEEP as demo data / KEEP + restructure** | Ch6 6.1.6, 6.2 |

**The six carrying `migrate authority`** - items 13, 25 and the definition-bearing parts of 18, 20, 24 and 30 - are the ones most often mistaken for finished. Their code survives M2; their **source of truth** does not.

---

## 2.2 THIRTY URGENT GAPS

| # | Gap | Consequence today | Severity | Milestone |
|---|---|---|---|---|
| 1 | **Permanent plant relationship model** - `plant_relationships`, members, paths, resolver, purpose gating | Sixteen declared consumers have no foundation; every authored join dies inside its dashboard query | **Critical** | M1 slice + M2 |
| 2 | **Unified definition store** - identity, immutable versions, dependencies, cycle protection | Six per-artifact tables act as six sources of truth | **Critical** | M1 contract + M2 |
| 3 | **Canonical schema authority** - 12 schemas, 162 tables in `public`, 108 with a `ppiq_` prefix, `ppiq_plant` and `ppiq_meta` empty | Rule 2 not provable in one query | **Critical** | M2 |
| 4 | **Projection validation and quarantine** - typed `PV` classes, `projection_quarantine`, reprocess | A mapping mistake either silently corrupts the canonical layer or fails a whole batch. No third state | **Critical** | M1 shape + M2 |
| 5 | **Registry authority everywhere** - compiled `HashSet` holding `ShiftCode`, `DefectType`, `RiskClass`, `GradeOrRecipe` | **A Rule 1 violation reachable by a customer** | **Critical** | M2 (cheap - see 2.3 #25) |
| 6 | **Definition version, dependency and impact model** | No impact preview, no dependency graph, no export or import | **Critical** | M2 |
| 7 | **Job target-definition and version contract** - `target_definition_id`, pinned versus current-published, run-used-version truth | **A job cannot say what it runs.** Blocks J12 and tutorial T7 | **High** | **M1** |
| 8 | **Job dependency DAG, pools and weights** | No concurrency governance; nine designed defence mechanisms, few implemented | **High** | M2 |
| 9 | **End-to-end delta propagation** - watermarks per stage, delta strategy per job class | Naive full scan is 481 TB/day for one Pro customer; delta-scoped is 5 to 20 GB. Ratio 24,000 to 1 | **Critical** | M2 |
| 10 | **Chunking, checkpoint, resume, scan budget, Scan Amplification** | Large-data execution is unbounded | **Critical** | M2/M3 |
| 11 | **Intelligence result store** - one persistence for correlation, practices, predictions, value, evidence | Nine of 28 Chapter 1 promises have no persistence | **Critical** | M2 |
| 12 | **Feature and outcome store with snapshots** | Prerequisite to everything in ML, practice and prediction | **Critical** | M2 |
| 13 | **Model registry, serving identity and fallback** | No governed serving role, no six-condition fallback | **Critical** | M2 |
| 14 | **Practice Learning L0 to L5**, `practice_statistics` | Guideline 1.3.b, a central promise, entirely unbuilt | **Critical** | M2 |
| 15 | **Practice drift, back-off ladder, tolerance sensitivity** | Without these it is a one-off best-practice table, not a product | **High** | M2/M3 |
| 16 | **Prediction operational pipeline** - `prediction_runs`, `predictions`, `prediction_current` | Early Warning has nothing to read | **Critical** | M2 |
| 17 | **Actionable deadline and latency health** | The prediction must arrive before the stage that can act on it, and must expose miss truth | **High** | M2/M3 |
| 18 | **Prediction drivers and comparables, persisted** | Explainability must be drillable, not narrated | **High** | M2 |
| 19 | **Remediation candidates and the per-prediction nine-check gate**, `can_accept`, escalations | Guideline 1.3.c, the strongest promise in Chapter 1, entirely unbuilt | **Critical** | M2 |
| 20 | **Decision, action, outcome, evaluation loop** - Accept, Reject, Defer through to observed outcome | The feedback loop that makes the product continuous does not exist | **Critical** | M2/M3 |
| 21 | **Persistent G1 assistant dock** - `AssistantChat` renders on exactly one page | **The visible contract is wrong today.** Must be fixed in M1 or the Continuity Test fails | **Critical** | **M1 visible** |
| 22 | **Page and widget context tool for the assistant** - `CanonicalChunkProducer` has five chunk families, none describing the current page | The assistant cannot see what the user is looking at | **Critical** | **M1 visible** |
| 23 | **End-to-end evidence handles** - figure to population to source row | Citations expand a handle rather than opening a row | **High** | M1/M2 |
| 24 | **Canonical J4 to J15 surface continuity** - `/materials` has no landing state; C2 has no Reprocess control though the API exists | Tutorial steps with nothing to click | **Critical** | **M1 visible** |
| 25 | **Canonical endpoint and route convergence** - 92 prefixes, 544 routes, phase and version tokens in the URL; 108 page files, ~20 legacy redirects | A customer's IT reviewer sees `/phase8/...` in the network tab | **High** | M2 |
| 26 | **Users, roles and RLS finalisation** - one RLS policy against 193 tables; Users/Roles and System Health absent from the UI | Tenant isolation declared absolute, enforced almost nowhere | **High** | M2 |
| 27 | **Licence and capacity enforcement** - six commercial dimensions plus the capacity envelope | `LicenseLimits` exists as a record; nothing meters capacity | **High** | M2/M3 |
| 28 | **Value Engine** - `value_impacts`, `cost_assumptions`, realisation ledger, D7 | **The CEO gap in code form.** The only work that moves the economic buyer | **High** | M3 |
| 29 | **Production infrastructure certification** - containers, topologies, 22 stages, HA, DR, tested restore, observability, C1 to C4 | Infrastructure is 8 files and 856 lines. Chapter 6 cannot be frozen until C1 to C4 run | **Critical** | M2 minimum / M3 full |
| 30 | **Five-product website truth** - `/products/:code` redirects into PPIQ pack pages | Encodes that MES, QES, Yard and Energy are PPIQ capability packs. They are not | Medium-High | M2/M3, or M1 only if opened |

---

## 2.3 THIRTY HIGH-LEVERAGE ENHANCEMENTS - COSTED

**Total 95 to 120 hours - roughly a quarter of M1 - and it moves more perceived quality than any other quarter you could spend.** Grouped so a group can be cut whole.

### Group A - the assistant becomes the product (16h)

| # | Change | Hrs | Why it lands |
|---|---|---|---|
| 1 | **Page-and-widget chunk family in `CanonicalChunkProducer`, then `POST /api/assistant/reindex`** | 6 | **The single highest-value change in the sprint.** The corpus today is connectors, datasets, mappings, the honesty doc and findings - nothing about the page in front of the user. This makes "describe this chart" work, grounded and cited, **with or without an LLM** |
| 2 | Real-model shim behind `PPIQ_ASSISTANT_MODEL_ENDPOINT` - ~50 lines translating the request and returning `{"answer": ...}` | 3 | Better prose, identical safety, since grounding still applies |
| 3 | Registry-typed quantity guard - expected unit, type, sign, range | 5 | Closes the last hole in your own acceptance criteria. A date or a mass where a speed was asked is rejected by **type**, not by luck |
| 4 | Citation chips, evidence strip, Open in page | 2 | Turns invisible honesty machinery into the thing the customer remembers |

### Group B - the dashboards stop carrying small lies (18h)

| # | Change | Hrs | Why it lands |
|---|---|---|---|
| 5 | Eight filter chips read N/A - `associativeFields` codes do not match registry names | 3 | Eight visible "N/A" labels on the main demo screen |
| 6 | Point Findings and Analysis Toolbox at `/ml/foundation/outcomes`, already consumed by `AnalysisJobConfigPage` | 3 | Retires two hardcoded steel-specific arrays |
| 7 | Take grain from the registry row instead of falling back to `"coil"` | 2 | A generic-grain outcome silently gets a steel grain today |
| 8 | Analysis Toolbox parity panel: `const formPayload = canvasPayload` compares an object with itself and can never read DIFFERS | 2 | **A guard satisfiable by its own prose is worse than no guard** |
| 9 | Rename the canonical grain "coil" to a generic term | 3 | The canonical layer already absorbs aluminium, tyre and batch types; "coil" on a tyre unit is a Rule 1 blemish an engineer will spot |
| 10 | Parameter filter: drop the hardcoded `CastingSpeed` default and steel fallback list; add "All parameters" like its five neighbours | 2 | One filter behaving unlike the rest of its bar reads as unfinished |
| 11 | Material Mix donut renders 14x14 | 2 | If it resists 45 minutes, replace the widget rather than debug it |
| 12 | `minusOwn` drops `fromUtc`/`toUtc`, so the associative panel disagrees with the widgets after a temporal selection | 1 | Visible inconsistency during exactly the interaction you plan to demonstrate |
| 13 | Chart switcher offers only compatible types and says why others are absent | 2 | Intent, not a dropdown of failures |
| 14 | Six-dashboard visual differentiation and data shaping | 1 (planning; execution sits in M1-D) | Six bar-chart pages read as one page shown six times |

### Group C - the workspace stops looking hand-made (14h)

| # | Change | Hrs | Why it lands |
|---|---|---|---|
| 15 | WORKSPACES nav group permanently empty: `useWorkspaceLinks` uses the only raw `fetch` in the tree, relative so it hits Vite, authenticated via `window.__ppiqToken` which is never assigned, failing silently on `if (!res.ok) return` | 3 | An entire empty navigation group in front of the customer |
| 16 | The Workspaces nav block is hand-rolled - plain `<p>` and bare `NavLink` - instead of the collapsible `NavGroup` the other four groups use | 2 | Different indent, cannot fold |
| 17 | Adjacent "Reset layout" and "Reset grid" doing different things, one with an icon; Undo and Clear disabled at load | 2 | Justify every click |
| 18 | Associative strip fires eight sequential awaited calls on mount | 3 | Parallelise. First impression of speed |
| 19 | A zero-row dimension renders available-but-empty rather than n/a | 2 | Filtered-empty and genuinely-empty are different states in the design |
| 20 | `associative.css` off-contract colours and a duplicated rule set; `DrilldownDrawer` has no transition and off-palette blues | 2 | Motion and palette are the cheapest perceived-quality purchases in the product |

### Group D - the gaps that block a click path (20h)

| # | Change | Hrs | Why it lands |
|---|---|---|---|
| 21 | `job_definitions.target_definition_id` plus `JB` codes | 6 | **The smallest item that unblocks a whole journey step.** Fully specified in Ch3 4.5.5a. The best first design-to-code loop in the project |
| 22 | `/materials` landing route as a two-state page contract | 4 | The menu item opens nothing today |
| 23 | C2 Mapping Health Reprocess control - the API already exists | 4 | A tutorial step with nothing to click |
| 24 | Per-tier cadence-floor warning shown before the user sets a schedule | 3 | A Pro user setting a one-minute schedule meets a refusal the tutorial never mentioned |
| 25 | Retitle-when-you-repoint check on widget titles | 3 | A widget whose title says one thing while it plots another is worse than a broken one |

### Group E - what an IT reviewer will find in ten minutes (20h)

| # | Change | Hrs | Why it lands |
|---|---|---|---|
| 26 | Registry dimensions and measures as rows, replacing the compiled `HashSet` of plant vocabulary | 6 | Closes gap 2.2 #5 at a fraction of its apparent cost. Chart types and limits correctly stay closed |
| 27 | Add `test:visual` and `test:a11y` as real pipeline stages; replace the `--list` invocations in `validate-real-ui-gates.cjs` with execution | 4 | Chapter 6 names this exact anti-pattern. Today that script verifies the gates **exist**, not that they **pass** |
| 28 | Parameterise the 15 hardcoded server-IP references | 3 | Trivially found |
| 29 | Remove `IsBootstrapAdmin=true` from both env profiles; move `PPIQ_E2E_PASS` and the CI signing key out of `ci-e2e-stack.sh` | 3 | Harmless today, dangerous the day one of those files ships |
| 30 | Secret masking in the audit generator (header currently `Mask Secrets : False`); delete the three `Jenkinsfile` backups, one of which is **less strict** than the canonical file | 4 | **Never send the current audit package to a customer or external engineer.** A stale backup that disagrees with the live gate is a trap |

---

## 2.4 SCORING BY AREA

Bands: below 55 critical, 55 to 69 needs work, 70 to 84 solid, 85 or more strong.

| Area | Today | After M1 | After M2 | Chapters |
|---|---|---|---|---|
| Platform and backend architecture | **62** | 68 | 85 | Ch3 4.6 |
| Connect and import (DF1-DF3) | **66** | 78 | 88 | Ch3 DF1-3 |
| Model the plant (DF4-DF6) | **45** | 64 | 86 | Ch3 DF4-6, 4.5.10 |
| BI workspace and authoring (DF7) | **64** | **91** | 93 | Ch4 5.1, 5.2 |
| Engine, statistics, readiness (DF8-DF9) | **58** | 82 | 88 | Ch4 5.4, 5.5 |
| AI, ML and intelligence (DF10-DF14) | **18** | 46 | 74 | Ch4 5.6 |
| Assistant (DF15) | **50** | **86** | 89 | Ch4 5.7 |
| Administration, licence, security | **32** | 40 | 80 | Ch6 6.3, Ch3 4.5.17 |
| Infrastructure, CI/CD, testing | **30** | 46 | 76 | Ch6 6.1 |
| Website and commercial | **70** | 86 | 88 | Ch6 6.2 |
| Dataset, demo and reproducibility | **55** | **90** | 86 | Ch1 1.0, Ch6 6.1.6 |
| **Weighted product conformance** | **31** | **41** | **80** | |
| **Six-beat presentation readiness** | **62** | **93** | - | |

M1 moves four areas hard - BI, assistant, website, dataset - and barely moves administration and infrastructure. That is deliberate.

## 2.5 SCORING BY PERSONA

The lowest persona is the shipping headline. That law is not softened.

| Persona | Today | After M1 | After M2 | What moves them |
|---|---|---|---|---|
| A1 Developer / maintainer | 62 | 66 | 84 | Namespace, page consolidation, definition store |
| A2 Security / IT / procurement | **38** | 42 | **80** | RLS, tenant isolation, secrets, deployment |
| A3 Process / quality engineer | 55 | **86** | 88 | Dashboards, authoring, assistant, engine story |
| A4 Reliability / operations | **32** | 36 | 76 | Jobs, delta, monitoring, backup, restore |
| A5 Executive sponsor | 48 | 58 | 62 | **Capped until the Value Engine in M3** |
| A6 Brand / website | 72 | 88 | 90 | Website polish, five-product truth |
| A11 UI / UX auditor | 60 | **90** | 91 | The whole of 2.3 Groups B and C |
| A12 AI and engine auditor | 42 | 70 | 84 | Chunk family, readiness story, then the real engines |
| A13 Infrastructure engineer | **28** | **34** | **74** | Containers, pipeline, topologies, certification |
| **HEADLINE (lowest persona)** | **28** | **34** | **74** | |

### The single most important sentence in this chapter

**M1 raises the demonstration headline from 62 to 93 and the shipping headline from 28 to 34.** Those are two different scoreboards, and mixing them is how a team panics in week two.

M1 is not supposed to move A13 or A2. It moves A3, A11, A12 and A6 - **the personas who will be in the room.** A2 and A13 are the personas who arrive **after the sale**, and M2 exists for them, which is exactly right given that M2 ends with an on-site installation.

A5, the economic buyer, is capped near 60 until M3 regardless of build effort, because only the Value Engine moves it. That is narrative work before the room; Chapter 1 1.9 scripts it.

---
---

# CHAPTER 3 - DELIVERY ROADMAP

## 3.0 The three milestones

| Milestone | Hours | Definition | Exit |
|---|---|---|---|
| **M1** | 400 | Design-conformant customer demonstration vertical slice. Finish and freeze what the customer sees; underneath, the cheapest honest implementation that does not force a later visible change | The presentation is impressive, stable, honest and repeatable from a clean laptop start |
| **M2** | 400 (see 3.3.2) | Replace M1's temporary backends with the final canonical implementation **without changing the visible contract**, and make the product installable and operable on the customer's site | The customer installs it, connects their own sources, and runs the journey on their own data |
| **M3** | open | Site stabilisation, performance on real volumes, customer-specific work through the three doors, and the certification and commercial completion the sale depends on | Installable at a second customer with nobody remembering how a laptop was configured |

---

## 3.1 M1 - REQUIREMENTS, STATUS AND RULES

### 3.1.1 The six beats and where they stand

| # | Beat | Today | Target | Principal work |
|---|---|---|---|---|
| 1 | No-code, wiring and SQL editor with a live test | **75** | **95** | Four-region shell, registry-driven schema tree, both modes reaching one saved definition |
| 2 | Add page, edit widget SQL, add widgets, six prepared pages | **70** | **95** | Seven dashboards already seeded - certification and differentiation, plus the Page Builder surface |
| 3 | Full journey, 2 to 3 minutes per step | **70** | **93** | J4 to J15 with no dead end. J1 to J3 narrated as commissioning, not demonstrated |
| 4 | The engine, described | **60** | **92** | One readiness authority; one analysis definition; finding drill to method, population, effect, evidence |
| 5 | Assistant on the page, asked about a chart | **50** | **90** | G1 dock, context envelope, page-and-widget chunk family |
| 6 | Website walk | **75** | **92** | Polish only |
| | **Overall** | **62** | **93** | |

### 3.1.2 The special rules of M1

**M1-A - The Visible Contract is final.** If Chapter 4 says Add Widget is kind picker, name, shared S2 shell, catalogue or query, preview, save, then that is exactly what the customer sees now and receives after M2. A faster popup with a SQL textbox is **forbidden** even though it would save days.

**M1-B - Hidden implementation may be temporary only if replacing it changes nothing visible.** Use the four-layer shape in 1.2.

**M1-C - No fake product answer, ever.** Prepared data allowed and presented honestly as demo-dataset output. A hardcoded answer, a trigger that manufactures a result, or a perfect-reply endpoint is never allowed at any milestone.

**M1-D - The UI/UX Golden Gate.** Every shown surface must pass before it counts Green: `Standard*` components where one exists; no raw local styling; primary Electric Blue; selection Electric Cyan or Cyan Green; secondary Corporate Blue; warning and refusal Amber; destructive Hot Red; muted and excluded Muted Steel; `inline-start`/`inline-end` never left/right; a keyboard path; an RTL mirror; all seven states - Empty, Loading, Populated, Filtered-empty, Blocked, Refused, Failed; one widget failing must not kill the page; customer-specific dimensions and measures registry-driven; no plant vocabulary in product logic; no number without evidence where intelligence is claimed.

**M1-E - No environment branch.** One codebase, one migration chain. `ppiq_presentation` is a data profile, not a fork.

**M1-F - Feature freeze at end of day 8.** Day 9 certification. Day 10 rehearsal.

**M1-G - Clean-start rule.** No database console during the demonstration. No manually typed SQL seed. No hidden developer page. No manual database correction. No "do not click that button".

### 3.1.3 What M1 explicitly does NOT do

| Cut | Why | Goes to |
|---|---|---|
| J1 to J3 as a live demonstration | Chapter 5 classifies them as commissioning prerequisites | Narrated; built in M2 |
| Users and roles administration | Not in any beat | M2 |
| Licence backend and enforcement | Not visible unless tier toggling is shown | M2 |
| Whole-database RLS migration | Invisible | M2 |
| All 15 `PV` quarantine classes | The **shape** must be final if the page is shown; the full class set need not be | M2 |
| Practice learning, production prediction, the nine-check pipeline | **Surfaces** final if shown; engines behind them not | M2 |
| Value engine and any euro figure | Nothing to show. Ch1 1.9 scripts the conversation | M3 |
| HA, DR, C1 to C4, hardware constants | Chapter 6 is deliberately unfrozen until measured | M3 |
| Five-product website transformation | Unless one of those pages is actually opened | M2/M3 |
| Wide repository cleanup, database merge, design-system change, website rewrite | Risk without customer value at this distance | M2/M3 |

### 3.1.4 M1 Definition of Done

From a clean laptop boot, without a database console:

> Connection -> Dataset -> Import -> Relationship and Mapping -> Genealogy -> Page Builder -> Add Widget -> Wiring -> Compiled SQL -> SQL edit -> Preview -> Save -> Six dashboards -> Cross-filter -> Engine readiness and finding -> Assistant on the current page -> Evidence -> Website

---

## 3.2 M1 - THE 400 HOURS

**400 team-hours in parallel lanes, not 400 elapsed hours.**

| ID | Epic | Hrs | Headline topics | **Green when** |
|---|---|---|---|---|
| **M1-A** | Design-to-demo contract audit | **26** | Six-beat traceability matrix: screen -> Ch2 journey -> Ch3 page and API -> Ch4 behaviour -> Ch5 tutorial. Classify every touched item KEEP / MODIFY / TEMP-ADAPTER / NEW. Lock the presentation profile. Add `ppiq_acceptance_empty` and the one-query Rule 2 proof | Approved traceability matrix exists and **no visible screen lacks a final design owner** |
| **M1-B** | Presentation data and reproducible environment | **58** | Scratch rebuild and diff of `ppiq_presentation`. Phenomena manifest. Improve the emulated sources so the phenomena arise naturally. Regenerate dashboards and intelligence. Validate units, ranges, chronology, genealogy, provenance | The presentation environment **rebuilds from source control**, and 10 to 15 believable analytical phenomena exist in the data |
| **M1-C** | No-code, wiring and SQL - final visible shell | **70** | Final four-region shell; registry-driven schema tree with types, search and row hints; wiring blocks; debug log; compiled SQL; Query mode; safe run-test; returned-column role mapping; **Add and Edit reopen the same shell** | One golden authoring scenario runs **twice in a browser and survives reload** |
| **M1-D** | BI workspace and six showcase pages | **82** | Verify the seven seeded dashboards, choose six, differentiate the visual grammar; associative selected / possible / excluded / alternative including the excluded pivot; layout drag, resize, save, reload; chart switcher; drill and evidence; widget isolation | **Six premium pages, no empty widget, no dead action, six distinct chart stories** |
| **M1-E** | Visible journey J4 to J15 | **48** | Connections with read-only proof; registry; import with progress; mapping and quarantine shape; genealogy landing; **one published relationship and reusable path**; analysis definition with target and version; findings, risk, jobs and activity surfaces | A **fast walkthrough with no dead end**, source to intelligence, on final UI contracts |
| **M1-F** | Engine presentation slice | **26** | One readiness authority on Home and Analysis; one design-compliant analysis definition producing result or refusal; finding drill to method, population, effect, evidence; honest Blocked versus Failed; current-versus-target ML messaging | The engine story is **live, explainable and not overclaimed**; readiness reads as intelligence - "Blocked because outcome events = 12; Ready requires 40 or more" |
| **M1-G** | Persistent G1 assistant | **40** | Dock shell on authenticated surfaces - **never a separate page**; context envelope of route, page, widget, selections, result, evidence; the page-and-widget chunk family plus reindex; typed quantity sanity; citation chips and evidence strip; suggested questions; 10 to 15 certified questions; offline extractive fallback | **2 to 3 live questions on 2 to 3 pages return cited grounded answers or a useful refusal** |
| **M1-H** | Website presentation path | **14** | Polish only the routes you will open: header, PPIQ story, graphics, proof, security, CTA. Desktop, mobile and keyboard smoke on those routes | **No visible dead link** on any presented route |
| **M1-I** | Certification, visual QA and rehearsal | **36** | One automated E2E covering all six beats; visual and accessibility **executed** on shown routes; failure injection - a widget fails, the assistant refuses, the API restarts, filtered-empty, no network to the model; three rehearsals; hostile hands; backup and offline fallback; **Customer Contract Continuity snapshots** | **Two consecutive full rehearsals from a clean start with no surprise**, and the M1 snapshot set is captured |
| | **Total** | **400** | | |

### 3.2.1 Execution order

| Days | Lane |
|---|---|
| 1 to 2 | Establish truth: traceability matrix, presentation rebuild and diff, phenomena manifest, assistant context inspection |
| 3 to 4 | Authoring becomes final: the shell, wiring to SQL to preview to save, first golden authoring test |
| 5 to 6 | The visual product: six dashboards, associative behaviour, page and widget authoring, the relationship slice, J4 to J9 |
| 7 to 8 | The intelligence story: J12 to J15, engine, findings and evidence, G1 assistant, website polish |
| **End of 8** | **FEATURE FREEZE** |
| 9 | Certification only |
| 10 | Rehearsal only. No development |

### 3.2.2 The presentation phenomena manifest

The highest-leverage work in M1, because every other beat is fed by it. **Planted in the data, never as an answer in code:**

casting speed by temperature by grade interaction; thickness changing the optimum speed; a real quality difference between equipment A and B; a shift effect; a downtime pattern; a yield-against-throughput trade-off; rising defect probability above a speed band; **a plausible correlation that disappears after conditioning**; a good-practice operating band; a bad-practice band; **one insufficient-support case the engine must refuse**; one clean genealogy chain.

The eighth and eleventh are the two that make an engineer believe the product. A correlation that survives naive analysis and dies under stratification, shown live, is worth more than ten correlations that hold.

### 3.2.3 Parallel lanes - what 400 team-hours implies

400 hours over 10 days is 40 hours a day, which is four to five people. The epics are not equally parallelisable, so the lane structure matters:

| Lane | Epics | Hours | Dependency |
|---|---|---|---|
| **Lane 1 - Data** | M1-B | 58 | Starts day 1, **blocks M1-D and M1-G**. Must finish by end of day 4 |
| **Lane 2 - Authoring** | M1-C, part of M1-E | 90 | Starts day 2 after M1-A defines the contract |
| **Lane 3 - BI** | M1-D | 82 | Needs Lane 1 output from day 4 |
| **Lane 4 - Journey and engine** | M1-E remainder, M1-F | 74 | Runs days 4 to 8 |
| **Lane 5 - Assistant and web** | M1-G, M1-H | 54 | M1-G needs Lane 1 and Lane 3; start day 5 |
| **Lane 6 - Contract and certification** | M1-A, M1-I | 62 | M1-A days 1 to 2, M1-I days 8 to 10 across everyone |

**The critical path runs Lane 1 -> Lane 3 -> Lane 5.** If the dataset slips, the dashboards and the assistant both slip, and those are beats 2 and 5. **Protect M1-B above everything else.**

---

## 3.3 M2 - ON-SITE SOFT-TEST DELIVERY

### 3.3.1 Definition

> M2 replaces every M1 temporary backend with the final canonical implementation **without changing the customer-visible contract**, and makes the product installable, operable and supportable at the customer's site.

**Exit:** the customer installs PPIQ on their own infrastructure, connects their own sources read-only, and runs J1 to J15 against a normal canonical database with no presentation shortcut and no demonstration-only code path.

### 3.3.2 The M2 arithmetic - read this before committing to a date

Both prior plans put M2 at exactly 400 hours. That exactness is the tell: the numbers were fitted to the budget rather than derived from the design. The compression is visible when the two independent estimates are put side by side.

| Epic | Rev D | Independent estimate | Delta |
|---|---|---|---|
| Canonical schema and migration convergence | 40 | 56 | -16 |
| Unified definition store and registry authority | 45 | 48 | -3 |
| Permanent relationship model and path resolver | 45 | 56 | -11 |
| Projection, quarantine, genealogy completion | 35 | 40 | -5 |
| Jobs, delta and load balancing | 50 | 56 | -6 |
| Intelligence persistence and model lifecycle | 35 | 52 | **-17** |
| **Practice learning + prediction + remediation MVP** | **60** | **148** | **-88** |
| Security, users, roles, RLS, licence, logging | 30 | 70 | **-40** |
| Assistant finalisation and value integration | 20 | 20 | 0 |
| On-site package, QA, deployment acceptance | 40 | 78 | **-38** |
| **Total** | **400** | **624** | **-224** |

The three large deltas are not estimating noise:

- **Practice learning, prediction and remediation at 60 hours.** Chapter 4 specifies a practice engine with signature construction, windowing, cohorts, a back-off ladder and tolerance sensitivity; a **fifteen-stage** predict-then-remediate pipeline; and a **nine-check** eligibility gate with `can_accept` and escalations. Sixty hours delivers one of those three, not three.
- **Security, users, roles, RLS and licence at 30 hours.** One RLS policy exists against 193 tables, and the eight-role matrix has three enforcement layers. Thirty hours writes the policies; it does not also build the administration surfaces and the licence metering.
- **On-site package at 40 hours.** Install, upgrade, customer topology, smoke, E2E, **backup and restore rehearsal**, runbook, observability minimums and UAT import. Any one on-site installation consumes a week of that on its own.

### 3.3.3 Three options, and a recommendation

| Option | Shape | Assessment |
|---|---|---|
| **A** | Extend M2 to ~620 hours, single track | Honest, but pushes the on-site date out by roughly 55 percent |
| **B** | Hold 400 hours as scoped in Rev D | The compression falls on practice, prediction, remediation and on-site readiness - **the two things M2 exists to deliver.** Not recommended |
| **C** | **Split into M2a Deployable Core (400h) and M2b Intelligence Completion (~230h)** | **Recommended** |

**Why option C is right on the engineering, not just the budget.** During on-site soft testing the customer's data is still accumulating. The readiness gate requires 60 independent units and 40 outcome events. **Practice learning and prediction therefore cannot produce anything real in the first weeks of a pilot regardless of whether they are built.** Delivering them as a governed update partway through the soft test is better sequenced, and the Continuity Test still holds because their surfaces were frozen in M1.

### 3.3.4 M2a - Deployable Core - 400 hours - ends with the installation

| ID | Epic | Hrs | Headline topics | **Green when** |
|---|---|---|---|---|
| **M2a-A** | Canonical schema and migration convergence | **50** | Move authority to `ppiq_staging` / `ppiq_plant` / `ppiq_meta`; canonical migration order; archive legacy fixes; remove presentation-only manual state | **A fresh canonical database builds and upgrades with no hidden fix**, and Rule 2 is provable in one query |
| **M2a-B** | Unified definition store and registry authority | **48** | S1 to S5 share identity, immutable versions, dependency graph, cycle protection, impact preview, export and import; compiled plant vocabulary removed from production paths; **the M1 adapter is deleted and the UI does not move** | All five definition kinds resolve through **one** authority, and the Continuity snapshots still match |
| **M2a-C** | Permanent relationship model and path resolver | **52** | Relationships, members, published paths, purpose gating, cardinality, grain conversion, preferred paths, resolver, Relationship Browser, path evidence | **Every declared consumer resolves through one published model**, with regression tests |
| **M2a-D** | Projection, quarantine and genealogy completion | **40** | Final projection validation, all typed `PV` codes, `projection_quarantine`, retry and reprocess, C2 Mapping Health, identity resolution, genealogy hardening | **A deliberately malformed mapping quarantines rows with a named code** and is recoverable from the UI |
| **M2a-E** | Jobs, delta propagation and load balancing | **54** | Target and version policy, dependency DAG with cycle validation, pools and weights, skip-if-running, admission control, watermarks, delta per job class, chunk manifests, checkpoint and resume, deterministic merge, scan budget, Scan Amplification | **A large source change propagates incrementally end to end**, and Scan Amplification has a baseline with a regression gate |
| **M2a-F** | Commissioning, users, roles, licence | **62** | J1 to J3 built for real - install, licence activation, user provisioning; eight-role matrix with three enforcement layers; licence and entitlement across the six commercial dimensions plus the capacity envelope. **The surfaces were frozen in M1; this is the backend** | **A new site can be commissioned from zero by following the runbook**, with roles enforced server-side |
| **M2a-G** | Security hardening | **34** | RLS forced on every tenant-owned table with an architecture test that fails the build when a table is added without one; tenant keys and tenant-aware uniqueness; canonical namespace on new APIs; secret scan; no client-side permission authority | **The architecture test goes red when an unprotected tenant table is added** |
| **M2a-H** | On-site package and deployment acceptance | **48** | Container architecture and the four profiles, install package, migration runner, upgrade and rollback, backup with a **tested** restore acceptance, health and readiness endpoints, minimum monitoring and alerting, support runbook, UAT dataset and configuration import | **A clean machine reaches a working install and a successful restore rehearsal, twice** |
| **M2a-I** | Canonical journey regression and Continuity Test | **12** | J1 to J15 against normal `ppiq_app`; no presentation-specific code path; definition and relationship authority verified; assistant context and evidence regression; no-number-without-citation tests; one blocked run and one successful run | **The journey passes on the canonical database, and the Continuity comparison against the M1 snapshots shows no visible-contract change** |
| | **Total** | **400** | | |

### 3.3.5 M2b - Intelligence Completion - ~230 hours - shipped as an update during the soft test

| ID | Epic | Hrs | **Green when** |
|---|---|---|---|
| **M2b-A** | Intelligence persistence and model lifecycle - feature and outcome store, snapshots, compute runs, model registry, serving identity, primary and fallback, drift, bindable intelligence registry, evidence handles | **48** | Nothing downstream invents its own persistence; every result carries a resolvable evidence handle |
| **M2b-B** | Practice learning - signature, window, context, cohorts, support, confidence, back-off ladder, tolerance sensitivity, incremental refresh, `practice_statistics`, drift, D10 | **46** | A practice is discovered, supported and drift-monitored **on canonical data, not precomputed demo rows** |
| **M2b-C** | Prediction and remediation - `prediction_runs`, `predictions`, `prediction_current`, drivers, comparables, scoring mode, actionable deadline, candidate generation, per-prediction eligibility, the nine-check gate, `can_accept`, Accept / Reject / Defer, action recording, evaluation, escalation | **62** | A prediction arrives before its actionable stage, carries drivers and comparables, and produces a remediation candidate that passes or fails a named check |
| **M2b-D** | Engine consolidation and honesty regression - delete the retired Postgres engine and its SQL function; fix the outcome namespace divergence, the grain assignment in the ML refresh routine, the ordinal severity loader | **28** | One gated engine, one namespace, and no compute path that bypasses the readiness gate |
| **M2b-E** | Legacy retirement - API namespace to 27 domains with a dual-serve window; pages 108 to 40 under Rule 4; migration history to a canonical ordered path | **36** | **No phase or version token appears in the URL space** and no unreachable page remains |
| **M2b-F** | Test gate completion - remaining pipeline stages, visual and a11y as blocking, golden journey as a merge blocker | **30** | A red golden journey **blocks a merge** |
| | **Total** | **230** | |

---

## 3.4 M3 - SITE STABILISATION AND PRODUCTION READINESS

Half of M3 is written by the customer during the soft test. What can be specified now is the shape.

| # | Topic | Note |
|---|---|---|
| 1 | **Site defect burn-down** | Fix what soft testing finds **without changing the frozen visible contract** unless a formal product decision is approved |
| 2 | **Performance tuning** | Query plans, indexes, partition boundaries, pool weights, scan amplification, caching, model-serving memory - **from real measurements**, not assumptions |
| 3 | **Customer data edge cases** | New source patterns, dirty data, unusual keys, timestamps, nulls, late arrivals, customer-specific mapping needs |
| 4 | **Connector and configuration completion** | Certify the customer's actual connectors. **Never a customer-specific fork** |
| 5 | **Customer definitions** | Build and validate the customer's real pages, relationships, measures, analyses, models and log rules **through the product's own authoring surfaces** |
| 6 | **Practice and prediction calibration** | Longer data window, retrain and validate under governance, tune thresholds, measure deadline health |
| 7 | **Remediation validation** | Validate actionability against real process constraints; the human approval boundary stays intact |
| 8 | **SSO and identity integration** | Customer IdP, final role catalogue, account lifecycle, emergency access |
| 9 | **Site security hardening** | Network rules, secrets, certificate rotation, RLS and tenant proof, audit review, security sign-off |
| 10 | **HA, DR and backup** | Production topology, restore acceptance, RPO and RTO, disaster rehearsal |
| 11 | **Capacity certification** | Run C1 to C4 and site benchmarks; **replace the 10 Chapter 6 reference assumptions with measured constants.** This is what unfreezes Chapter 6 |
| 12 | **Monitoring and SLOs** | Operational dashboards, alerts, queue and latency and backup and certificate health, support escalation |
| 13 | **Reporting, export and notification integration** | Customer reports, alert channels, webhooks - **within the read-only product boundary** |
| 14 | **UI fine tuning from real users** | Only where it improves the final contract, never a second customer-specific UX |
| 15 | **Customer-requested features** | Triaged as configuration, generic product enhancement, or out of scope |
| 16 | **Commercial capacity finalisation** | Validate real user, page, job, DB-link and data bands against measured infrastructure and the contracted tier |
| 17 | **Documentation and training** | Runbook, operator and data-engineer and admin guides, support escalation, configuration handover |
| 18 | **Production acceptance** | Formal acceptance suite, known limitations, release notes, rollback plan, sign-off |
| 19 | **The Value Engine** | `value_impacts`, `cost_assumptions`, realisation ledger, D7. **The only work that moves the economic buyer - and the pilot supplies the real numbers it needs** |

### The M3 rule that protects everything else

> **A customer-specific request is satisfied through import, registry or authoring - or it is a design gap. It is never a code branch.**

The first `if (customer == X)` ends the ability to sell to the second customer. Rule 1 is hardest to hold exactly when the first customer is paying and asking.

---

## 3.5 THE TWO-SCOREBOARD RULE

| | Demonstration scoreboard | Shipping scoreboard |
|---|---|---|
| Measures | Six-beat presentation readiness | Lowest persona across nine |
| Today | 62 | 28 |
| After M1 | **93** | 34 |
| After M2 | - | **74** |
| Optimised by | M1 | M2 |

Report both, always, with their names. A team that watches only the shipping headline during M1 will conclude that 400 hours bought six points and lose confidence in a plan that is working exactly as designed.

## 3.6 RISK REGISTER

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| 1 | **M1-B dataset slips past day 4** | Medium | **Critical** - beats 2 and 5 both slip | It is the critical path. Staff it first and check it at end of day 2, not day 4 |
| 2 | **A surface is shown that was not intended to be frozen** | **High** | High - M2 inherits a contract nobody chose | Decide the exact route list in M1-A and do not open anything outside it |
| 3 | M2 committed at 400 hours as scoped in Rev D | **High** | High - the on-site date slips mid-pilot instead of in planning | Take option C now, in planning, where it costs nothing |
| 4 | The assistant refuses everything in the room | Medium | High - beat 5 fails silently | The chunk family in 2.3 #1 is the fix, and the certified-question pack is the safety net |
| 5 | The presentation rebuild loses the 14 to 27 July corrections | Medium | Medium | The scratch rebuild and diff in M1-A, run before touching the demo database for anything else |
| 6 | The audit package is shared externally with `Mask Secrets : False` | Low | **Critical** | 2.3 #30. Until then, treat the package as confidential |
| 7 | A customer-specific request is coded during M3 | Medium | **Critical** - ends genericity | The M3 rule above, enforced at triage, not at review |
| 8 | The economic buyer scores near 60 and the team reads it as failure | **High** | Medium | 3.5, plus the Ch1 1.9 script prepared deliberately before the room |

## 3.7 DECISIONS REQUIRED THIS WEEK

1. **M1 capacity and headcount.** 400 team-hours implies four to five people across the lanes in 3.2.3. If the real number is lower, cut epics **whole** rather than under-delivering ten.
2. **M2 shape** - option A, B or the recommended C. **This decides the on-site date.**
3. **The exact route list shown in the presentation.** Every surface opened becomes a frozen contract under M1-A. Showing the logging page costs the right to redesign it.
4. **The five standing rulings** - the wordmark typeface; credentials in the master document or a protected runbook; whether the Rules and Constitution chapter gets its own numbered file; the eight-role catalogue; and the logging families, four or the six Chapter 3 4.5.15 actually specifies.

---

## EVIDENCE BASIS

| Source | Version and date |
|---|---|
| Chapter 1 - Marketing and Sales | v4.6-aligned, 2 Aug 2026 |
| Chapter 2 - Technical Overview | v4.3 |
| Chapter 3 - General Technical Function Description | v4.5, freeze candidate |
| Chapter 4 - Specific Technical Function Description | v4.5, freeze candidate |
| Chapter 5 - Tutorial and User Journey | v4.5 |
| Chapter 6 - Infrastructure, Website, Administration and Sales | v4.6, **not frozen until C1 to C4 replace the reference assumptions** |
| Implementation | `UltimateAudit_29Jul2026_233112` - backend, frontend, infrastructure, website, tools, manifests; indexed by `[METADATA: Path=...]` and read file by file |
| Prior reviews | Rev A, Rev B, Rev C (merged) and Rev D, all 2 Aug 2026; superseded by this document where they disagree |
| Session handover | PPIQ Master Design Session Handover, 2 Aug 2026 |

**Verification boundary, stated plainly:** no test was run, no source file was modified, and no server or database was accessed in producing this document. Every measured number traces to the repository package. Every score is an estimate and is marked as one.

---

*End of document.*
