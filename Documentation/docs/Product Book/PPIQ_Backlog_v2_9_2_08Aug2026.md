# PLANTPROCESS IQ - EXECUTABLE BACKLOG

**Version 2.9.2 - 8 August 2026**  
**Supersedes:** v2.9.1  
**Trigger:** Deep implementation review against the latest Chapters 1–6, Roadmap Amendment 1, the 07-Aug UltimateAudit and the 05/07-Aug handovers found one genuine executable-scope orphan in the statistics engine plus several findings whose existing task ownership was too implicit to guarantee they would be fixed. Audit-traceability hardening only: no new product architecture, no task-ID renumbering, no phase redesign and no programme-hour increase.

---

## 0. WHAT v2.9.2 CHANGES, AND WHY

**Audit-traceability hardening.** The architecture and milestone plan stay unchanged. The change is that every material finding from the 08-Aug deep review now has an executable owner or a permanent cross-cutting law.

**The critical orphan is closed without inventing a new task ID.** Runtime evidence shows the current gated correlation engine supports Numeric×Numeric, Binary×Numeric and Categorical×Categorical, but has no Numeric×Categorical method. That leaves `defect.severity` against numeric process features as `NotApplicable`, and the current exclusion text incorrectly describes the unsupported pairing as constant/zero-variance input. The old handover points to a stale `T216`; this backlog ends at T-167. T-146 already owns correlation-engine convergence, so v2.9.2 expands T-146 to complete the final statistical method matrix, including ANOVA/Kruskal–Wallis selection, effect size, population/group evidence, FDR/q-value and truthful exclusion reasons. T-147 remains the adjacent owner of outcome namespace/grain/ordinal loading.

**Why this is an expansion rather than a new task.** M2b-P2 is already 114 hours inside the mandatory 80–120 hour phase band. A separate 8–12 hour task would either push the phase beyond its own law or force renumbering T-147 through T-167 after those IDs have already become evidence and handover references. The work belongs semantically and technically inside T-146, which is not started and already owns the exact engine seam.

**Presentation quality findings are made falsifiable, not merely advisory.** T-044 to T-047 now reject semantically degenerate showcase charts and raw technical labels where human labels exist; T-070 and T-166 make the website's premium visual language, typography, motion, responsiveness and encoding integrity part of Definition of Done; T-071 explicitly rejects the first-open assistant configuration/authentication 401 path.

**Enterprise hardening findings are made explicit where they already belong.** T-113 now separates demo/test/local settings from Production mechanically rather than only removing known literals. T-150 now proves mandatory CI gates execute real tests, cannot be satisfied by `--list`, cannot swallow failure into success, and have one canonical pipeline owner.

**Four permanent execution laws are added.** Product-semantic DB changes require a tracked authoritative replay definition; mutating packs fail closed and auto-revert/rollback; parallel workers stage exact owned files only; and every future audit finding must map to `CLOSED`, `OWNED BY T-xxx`, `CONTROLLED BY GLOBAL LAW`, or `NEW TASK REQUIRED` before a backlog version can freeze.

**Scope arithmetic is unchanged.** 167 tasks, 1,443 programme hours, M1 574, M2a 432, M2b 233 and M3 204. This version closes coverage holes; it does not move the customer meeting or enlarge M1.

---

## 0A. HISTORICAL - WHAT v2.9.1 CHANGED, AND WHY

Consistency correction. **No new architecture, no phase redesign, no task reopened.**

**The finding.** v2.9 wrote the M1 fast path correctly into the new contracts, but legacy prose from v2.5 and v2.6 survived alongside it. The ordering law at the top still ended with reload the source engines, import to staging, publish the mappings and project to canonical - the M2a path presented as the M1 path. The M1-P1b phase intent still promised to regenerate through the full product path. Three regime tasks still positioned themselves before a regenerate-and-import cycle that M1 no longer contains. A developer reading the file rather than the conversation would have run the wrong path.

**The gap that mattered most.** Chapter 3 section 4.5.2a defines a four-condition retirement gate for the donor schemas, and **no task executed it**. T-031 certified consistency and stopped. It now also backs up, restores that backup successfully, dependency-checks, and only then deletes `src_*` and the stale artifacts that have replacements. That is the difference between a rule and a rule that happens: 6 hours becomes 10, and M1-P2 lands at 107, still inside the band.

**T-013 now reads as it actually closed** - 13 rows, 15 columns, three decision axes - without being reopened. Its old premise that the container generator was a starting point rather than a problem is replaced by what the task itself measured: a different plant sharing two of six defect codes and none of the downtime reason codes.

**Also corrected.** `src_*` is called source-shaped donor tables and never staging. T-014's validation demands all nine retirement-gate dimensions rather than three. The demonstration script no longer shows the customer a donor schema name that will not exist by then. The M2 reference dataset gains benchmark hardening - case ids, safe bounds, known-good and known-bad regions, a holdout the development runs cannot read, declared refusal conditions, and a test that test-only truth never reaches production code - and the schema migration must re-run that harness unchanged.

---

## 1. THE ORDERING DEFECT, AND WHY THE GUARD MISSED IT

In v2.3 the coverage census sat in M1-P3 while two M1-P1 tasks said they were driven by it. That is a forward dependency: work that closes gaps ran two phases before the audit that names them.

**The guard did not catch it because I wrote the dependency in prose - "named by the widget coverage census" - rather than as a reference token.** A guard only sees what it is given. The dependency is now a `{{ref:}}` token, the census has moved to M1-P1, and the generator enforces the order mechanically.

This is the second time a mechanical guard has caught something only after I stopped hand-writing what it was built to check. The lesson is the same both times: **if a rule is mechanical, express it in the form the machine reads, not in the form a person reads.**

### The ordering law, written into the task

> Current emulation inventory, then the chart blueprint, then map each chart to existing fields and phenomena, then classify, then CAPTURE THE DONOR STATE IN CODE, then the Fleet v2 target, then change the generator **only for true gaps**, then merge the historical generations into one truth, then scale, then MATERIALISE the presentation canonical operational entities, then COMPUTE the analysis entities with the real engines, then statistical QA and the phenomenon proof, then materialise the source-shaped presentation staging representation, then certify cross-layer consistency and retire the donor state.
>
> **Corrected in v2.9.1.** The earlier form ended with regenerate the fixtures, reload the source engines, import to staging, publish the mappings and project to canonical. That is the M2a path, not the M1 path, and Chapter 3 section 4.5.2a now fixes the difference. **Native fixture emission, DB-Link, import, mapping and projection are M2a work.**

The v2.6 wording put statistical QA before the import and left the mapping and projection steps out. That contradicted the tasks themselves, which are correct. Chapter 3 makes DF1 to DF6 strictly sequential and DF4 requires a successful DF3 batch, so nothing can be certified before it has travelled the full path.

**Adding data before the inventory can break relations that already work.** That is the whole reason the order matters, and it is why the generator is not touched until the matrix exists.

---

## 2. THE CENSUS BECOMES THE PHENOMENA AND WIDGET COVERAGE MATRIX

Thirty-six primary charts across six presentation pages at six each, with the seventh dashboard as the technical backup. One row per chart, fourteen fields:

| Field | Example |
|---|---|
| Phenomenon | Post-EAF-maintenance recovery |
| Source systems | Meltshop plus Downtime |
| Population | 90 repair events, 270 post-repair heats |
| Variables | repair flag, heats since repair, power-on, energy, temperature |
| Intended effect | decays across the first three heats |
| Noise | plus or minus normal operating variance |
| Primary chart | box or line |
| Secondary chart | scatter or table |
| Dashboard | Equipment or Parameter |
| Expected analysis | significant early-versus-steady difference |
| **Negative control** | an unrelated chemistry component |
| Genealogy requirement | heat to downstream coil |
| Current status | EXISTING / ENRICH / NEW |
| Validation | the exact automated assertion |

Every one of the 36 must point at a phenomenon or at a legitimate operational baseline. Every ENRICH or NEW row names the exact source field, so the generator work that follows has a closed list rather than an open brief.

---

## 3. THE TWO REGIMES THE EMULATION IS GENUINELY SHORT OF

Re-assessment scored the emulation 85 to 95 percent almost everywhere - scale, genealogy, planted correlations, physical realism. **Two areas scored 60 to 75, and they are the two new BEHAVIOURAL REGIME FAMILIES.** They are not the only new dataset work in M1: the T-013 reconciliation added grade specification, chemistry expansion, downtime production impact, maintenance events, campaign keys, the shift calendar, the QA distribution repair and equipment variation.

**Shift and crew practice.** Crew rotation exists structurally; the behaviour does not. Day shift operating more conservatively with lower variance, night shift wider. Made as **overlapping distributions** - 5.95 with low variance, 6.10, 6.20 with wide variance - never three separate numbers, which reads as cartoon data. And partly confounded: night shift shows a higher defect rate, conditioning on grade removes much of the difference, **and the higher variance remains**.

**Post-maintenance recovery.** Reline counters and cold-furnace reheat exist; the recovery curve does not. Heat one raised on power-on, cycle time, energy per tonne and temperature variance; heat two partially recovered; heats three to five approaching normal. Four different shapes - EAF warm-up, refractory campaign reset, an HSM roll change improving surface quality initially, and an ageing roll campaign gradually raising roll-mark and wavy-edge rates.

### The economy that makes 36 charts affordable

**One planted behaviour yields eight charts.** The shift phenomenon alone gives a shift bar, a box plot, a trend, a scatter, a correlation, a conditioned result, a defect Pareto and an assistant question. Ten to fifteen designed system behaviours create dozens of visual stories; thirty-six unrelated tricks create none.

And the target mix matters, because a plant where everything correlates reads as fake: roughly 18 to 25 strong discoverable relationships, 20 to 25 moderate or conditional, 15 to 20 regime behaviours, 8 to 12 temporal drifts, 8 to 12 outliers, and **15 to 20 negative controls and pure noise**.

---

## 4. M1 IS 574 HOURS OF BASELINE SCOPE, 482 OF THEM REMAINING

| Movement | Hours |
|---|---:|
| v2.3 | 447 |
| Two operating regimes - shift practice and post-maintenance recovery | +8 |
| Census expanded to the 36-chart matrix and moved to M1-P1 | 0 |
| v2.4 | 476 |
| v2.5 | 534 |
| v2.6 | 552 |
| v2.6.1 | 552 |
| v2.7 | 552 |
| Capture the source-shaped donor schemas in a committed generator | +8 |
| v2.8 | 560 |
| Rewrite the M1-P1b tail to the prepared fast path | +10 |
| v2.9 | 570 |
| T-031 also executes the retirement gate | +4 |
| v2.9.1 | 574 |
| **v2.9.2** | **574** |

**Programme:** M1 574 + M2a 432 + M2b 233 = **1,239 hours before M3**, plus 204 reserved, for **1,443 total**. 167 tasks, no task over 12 hours, all fourteen phases inside 80 to 120.

---

## 3. HOW TO USE THIS BACKLOG

| Field | Meaning |
|---|---|
| **Task Id** | `T-001` upward. **Planned execution order, dependency-aware.** Not severity order - severity is the Priority column |
| **Milestone Id** | `M1` presentation, `M2a` deployable core, `M2b` intelligence completion, `M3` site and production |
| **Phase Id** | One topic, **80 to 120 hours, no exceptions**. Every phase ends pushable |
| **Module / Sub-module** | The surface or subsystem that owns the work |
| **Priority** | Critical, Very Important, Important, Optional. **Severity, not sequence** |
| **Hours** | **No task exceeds 12 hours** |

**Laws that govern every task below.**

1. **Temporary data is allowed. Temporary internal implementation is sometimes allowed. Temporary product identity, UX, workflow or behaviour is never allowed.**
2. In M1 the **visible contract is final**: UI appearance, UX flow, routes, terminology, control behaviour and placement, and visible state and refusal semantics.
3. **No architecture enters M1 unless a presentation-visible feature depends on it.** If one does, build the smallest slice of the *final* architecture, never a shortcut.
4. **No fake product answer at any milestone.**
5. A task is **Done** only when its Validation column passes **in a browser or against a running system**.
6. **No PARTIAL status.** A partially finished task is rewritten as its remainder with a fresh estimate.
7. **Database semantic durability.** A live database mutation may prove a fix, but no product-semantic database change is complete until the equivalent permanent definition or migration exists in tracked source control and in the authoritative rebuild/deploy replay path.
8. **Fail-closed mutation tooling.** Any script or pack that mutates source, schema or live data must preflight the exact target and expected match count, take a backup or use a rollback-capable transaction, apply the change, self-check it, run the targeted validation, and automatically revert or roll back on failure. A silent anchor miss, zero-replacement no-op or ambiguous target is a hard failure.
9. **Parallel-worker repository isolation.** On a shared working tree, workers stage exact owned files only. `git add .` and `git add -A` are forbidden during parallel work. Before commit, inspect `git status` and `git diff --cached`; never reset, checkout or overwrite another worker's dirty path.
10. **Audit-finding traceability.** Before a backlog version freezes, every material review/audit/runtime finding is classified exactly one of `CLOSED`, `OWNED BY T-xxx`, `CONTROLLED BY GLOBAL LAW`, or `NEW TASK REQUIRED`. A frozen version with an orphan material finding is invalid.

**Milestone exits.** M1: the six beats run from a clean laptop boot and two consecutive rehearsals complete with no surprise. M2a: the customer installs on their own infrastructure and runs **J1 to J12 plus every J13 to J15 surface** on canonical data, with prediction and remediation legitimately reporting readiness-blocked. M2b: **full functional J1 to J15 including acting on a prediction.** M3: installable at a second customer without anyone remembering how a laptop was configured.

---

## PHASE SUMMARY

| Phase | Milestone | Title | Tasks | Hours |
|---|---|---|---:|---:|
| **M1-P1** | M1 | Presentation Truth and Dataset Foundation | 12 | 84 |
| **M1-P1b** | M1 | Presentation Fleet v2 - capture, reconcile, enhance, scale, materialise canonical, prove | 17 | 114 |
| **M1-P2** | M1 | No-Code Authoring Shell - wiring, SQL and widget authoring | 11 | 107 |
| **M1-P3** | M1 | BI Workspace and the Seven Showcase Pages | 12 | 80 |
| **M1-P4** | M1 | Journey J4 to J15 and the Engine Slice | 16 | 106 |
| **M1-P5** | M1 | Assistant Dock and Presentation Certification | 15 | 83 |
| **M2a-P1** | M2a | Canonical Schema Authority and the Unified Definition Store | 11 | 114 |
| **M2a-P2** | M2a | Permanent Relationship Model and Projection Quarantine | 11 | 92 |
| **M2a-P3** | M2a | Job Runtime, Delta Propagation and Security Hardening | 12 | 106 |
| **M2a-P4** | M2a | Commissioning, Roles, Licence and the On-Site Package | 10 | 120 |
| **M2b-P1** | M2b | Intelligence Substrate and Practice Learning | 11 | 119 |
| **M2b-P2** | M2b | Prediction, Remediation, Engine Consolidation and Gates | 12 | 114 |
| **M3-P1** | M3 | Site Stabilisation and Real-Data Performance | 8 | 96 |
| **M3-P2** | M3 | Production Certification, Enterprise Operations and Commercial Completion | 9 | 108 |
| | | **TOTAL** | **167** | **1443** |

Priority mix: Critical 104, Very Important 54, Important 8, Optional 1

---


# M1 - CUSTOMER PRESENTATION (574 h baseline scope, 482 h remaining)

## PHASE M1-P1 - Presentation Truth and Dataset Foundation

**12 tasks / 84 hours.** Establish what will be shown, prove the environment rebuilds from source control, and plant the analytical phenomena the whole demonstration feeds on. Blocks M1-P3 and M1-P5.

### T-001 - Build the six-beat Design Traceability Matrix

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Documentation / Design contract |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Create docs/m1/M1_Traceability_Matrix.md. One row per screen that will be opened in the customer presentation. Columns: Screen, Route, Chapter 2 journey step (J4..J15), Chapter 3 page contract id (A1..F9 / G1..G6), Chapter 4 behaviour section (5.1..5.7), Chapter 5 tutorial step, Current implementation file, Classification. Classification is one of KEEP (matches final design), MODIFY (visible surface needs change), TEMP-ADAPTER (visible surface is final but persistence behind it is temporary and M2 replaces it), NEW. Get the route list from Frontend/PlantProcess.Web/src/App.tsx, which currently declares 69 route paths. Chapter 3 section 4.4 lists the target 40 route pages plus 6 shell components; every presentation screen must map to one of them.

**Validation.** The matrix has zero rows where the Chapter 3 column is blank. Walk the list with the App.tsx route table open: every route you intend to open in the room appears, and no route outside the list is reachable from the demo navigation. Reviewed and signed off before any other M1 task starts.

### T-002 - Audit every presented route and control against the Chapter 3 page inventory

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Frontend / Design contract |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** CORRECTED IN v2.1. The v2.0 validation only forbade phase-token routes, which was too narrow - and the code proves it was also unnecessary, because every to: value in AppLayout.tsx is already canonical and the legacy /phase8, /phase9 and /phase15 paths are Navigate reverse redirects that no navigation entry points at. The real task is wider. Compare EVERY presented route, EVERY JourneyRail stage and EVERY navigation entry against the Chapter 3 4.4 canonical inventory of 40 route pages plus 6 shell components. For each, record the Chapter 3 contract id or record it as out-of-inventory. An out-of-inventory surface that is opened in the room is a frozen contract nobody chose. Note that PPIQ-T12 (navigationContract.test.ts) already ships from this task's own applied pack and covers the token rule; the audit below extends it, it does not repeat it.

**Validation.** A table with one row per presented route, per JourneyRail stage and per navigation entry, each carrying a Chapter 3 contract id or an explicit out-of-inventory decision. Zero rows left undecided. Cross-check the JourneyRail stage list against Chapter 2 3.3.1 J1 to J15 - a stage that names a journey step the design does not have is a defect.

### T-003 - Lock the presentation profile as a data profile, not a branch

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Infrastructure / Environments |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** Chapter 6 forbids environment branches: the same artifact moves across environments. Confirm scripts/run/start-api.ps1 keeps its ValidateSet local/test/server/presentation and that the only difference between profiles is env/profiles/*.env. Fix the duplicated ConnectionStrings__PlantProcessDb declaration that appears twice (lines 18 and 19) in both local.env and presentation.env. Add a top-of-file comment to start-api.ps1 stating that the default is `local` and that the presentation must be launched with -Profile presentation.

**Validation.** Run `.\scripts\run\start-api.ps1 -Profile presentation` and hit GET /api/ml/foundation/readiness. It must report outcome_values around 195,221 and correlation_results around 320, which proves the API is on ppiq_presentation. Repeat with -Profile local and confirm the numbers differ. Record both outputs in the evidence folder.

### T-004 - Create the M1 acceptance checklist and evidence folder

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Documentation / Quality |
| **Priority** | Important |
| **Hours** | 4 |

**Description.** Create docs/m1/ACCEPTANCE.md holding the UI/UX Golden Gate as a checklist applied per screen: Standard* components where one exists, no raw local styling, primary Electric Blue, selection Electric Cyan, secondary Corporate Blue, warning and refusal Amber, destructive Hot Red, muted Muted Steel, inline-start/inline-end never left/right, keyboard path, RTL mirror, all seven states (Empty, Loading, Populated, Filtered-empty, Blocked, Refused, Failed), widget failure isolation, registry-driven customer vocabulary, no number without evidence. Create docs/m1/evidence/ for screenshots and command output.

**Validation.** Every screen in the traceability matrix has a checklist instance in the folder. A screen is not Green until every line is ticked with an evidence file name beside it.

### T-005 - Rebuild ppiq_presentation into scratch and diff against live

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** scripts/demo/Rebuild-PresentationDb.ps1 rebuilds the demo database in one command, but it restores from deploy/.ppiq-snapshots/ppiq_app_20260713_203359.dump, a 13 July snapshot. Every correction made against the live presentation database between 14 and 27 July survives a rebuild only if it became one of the script's steps. Run the script with -TargetDb ppiq_presentation_scratch (the script's guard requires the name to contain 'presentation'), then produce a diff: object list (tables, views, functions, triggers, indexes) and row count per table, scratch versus live.

**Validation.** Produce docs/m1/evidence/presentation_db_diff.txt. Acceptance is either an empty diff, or a written list of every difference. Do not proceed to the next task until that list exists, and never run the rebuild against the live database before it does.

### T-006 - Convert every diff finding into a seed or migration script

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** For each row of the diff from the previous task, decide: is this a product fix (goes to Backend/database/scripts as a numbered migration) or presentation data (goes to scripts/demo as a seed step)? The governing law is that presentation DATA may be presentation-only but presentation FIXES may never be. Add each item in the correct place and re-run the rebuild.

**Validation.** Re-run Rebuild-PresentationDb.ps1 into a fresh scratch database and re-run the diff. It must now be empty. That output is the proof that no fix exists only as data.

### T-007 - Presentation Phenomena and Widget Coverage Matrix, part 1: inventory and the 36-chart blueprint

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Documentation / Presentation data |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** REOPENED IN v2.5 - source truth changed after runtime measurement. The artifacts stand, but they were written against FLEET_RELATIONS.md, and measurement proved that document describes a larger, richer emulation than the one staged: 630 heats not 1,802, 5,670 coils not 18,661, 1,987 defects not 34,312, SIX defect codes not twenty, and THREE chemistry elements at one station not twelve at three. This task does not restart, and it does NOT stay half-open until the fleet is rebuilt - a task that closes twenty tasks later violates both the ordering law and the no-PARTIAL rule. It closes HERE as a PRE-GENERATION SPECIFICATION: the measured current inventory plus the 36-chart blueprint. Post-generation re-measurement and final certification belong to the phenomenon-proof task in M1-P1b, which cites this task by name so the link is machine-checked from that end. REWRITTEN IN v2.4. The previous version documented seventeen relations; that was necessary but not sufficient. Two halves, and the ORDER IS THE POINT. First, inventory what the emulation already carries - chemistry by grade at whatever element count and sampling stations MEASUREMENT SHOWS - the v2.4 text said twelve elements across three stations and that was taken from a document; measurement found THREE elements at ONE station, so the inventory records what is there, additives by grade, sequence grouping, crew rotation, ladle reline counters, cold-furnace and maintenance reheating, downtime propagation, roll wear, lubrication windows, and the seventeen planted relations with their negative controls. Second, write the 36-CHART BLUEPRINT: six primary presentation pages times six visual stories, with the seventh dashboard kept as the technical backup page. For each chart state what it must SHOW - not which correlation it must prove. The acceptance standard for the dataset changes here: every one of the 36 widgets needs an intentional visual story, sufficient population and cardinality, realistic variation, and at least one meaningful drill or filter interaction. NOT every chart must reveal a correlation. Planting a hundred clean correlations would make the plant look fabricated, which costs more credibility than a flat chart does.

**Validation.** Two artifacts in the evidence folder: the emulation inventory with a source and a row count per structure, and the blueprint with 36 rows each naming its page, chart type, and the visual story it must tell. A reviewer must be able to say, for each of the 36, what would make that chart boring - because that is the specification the data has to beat.

### T-008 - Presentation Phenomena and Widget Coverage Matrix, part 2: map, classify, close

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Testing / Dashboard |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** REOPENED IN v2.5 alongside part one, for the same reason: it cites effect sizes and defect codes that do not exist in the staged data. R1 CRACK_LONG at 9.3x, R2 INCLUSION at 4.5x and R4 SLIPPAGE_MARK at 28.9x have NO corresponding staged defect code. Aluminium, sulfur, niobium and nitrogen are absent, so four relation families cannot exist here whatever the document says. The 104 rows are kept as the SPECIFICATION OF WHAT THE FLEET MUST CARRY, and become the input to the Fleet v2 target rather than a description of what exists. This task ALSO closes here, before any generator work, and it carries one addition: PREDECLARE the expected direction, the acceptable effect band and the negative control for every phenomenon. Declaring them before the data exists is what stops the later proof from being self-fulfilling. REWRITTEN IN v2.4 as part two of the matrix. One row per phenomenon, with these columns: phenomenon; source systems; population; variables; intended effect; noise; primary chart; secondary chart; dashboard; expected analysis result; NEGATIVE CONTROL; genealogy requirement; current status EXISTING, ENRICH or NEW; and the exact automated assertion that validates it. Target roughly 85 to 110 micro-variations, distributed as 18-25 strong discoverable relationships, 20-25 moderate or conditional, 15-20 shift, crew, campaign and maintenance regimes, 8-12 temporal drifts or regime changes, 8-12 outliers or short abnormal events, and 15-20 negative controls and noise. Most of those will be extensions of the seventeen existing relation families rather than new inventions. Then map each of the 36 charts to at least one phenomenon or to a legitimate operational baseline, and give every widget a decision: KEEP, ENHANCE, REBIND, ADD WIDGET, EXTEND EMULATION, NEEDS RELATIONSHIP SLICE, or REMOVE. Weak bindings are not data shortages - Defects by Equipment returned one row in the last audit and the remedy is canonical equipment attribution or relationship resolution, never more rows.

**Validation.** docs/m1/evidence/phenomena_widget_matrix.csv with no blank cells. Every one of the 36 charts references at least one phenomenon or a named operational baseline. Every EXTEND EMULATION row names the exact field, so the generator work has a closed list. Every phenomenon carries its automated assertion, so the statistical QA task has nothing left to invent.

### T-009 - Downtime two-quantity contract: final schema and domain slice

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Backend / Canonical model |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** CORRECTED IN v2.1, and this was a materially wrong task. v2.0 treated the downtime work as data seeding only. It is not. The canonical DowntimeEvent entity carries timestamps, equipment, a DowntimeType and a reason, but it does NOT carry the two quantities the design requires. Chapter 3 4.5.4 specifies stopped_minutes and production_impact_minutes as separate columns, both NOT NULL and both greater than or equal to zero, because they are different quantities and one may never stand in for the other - a twenty-minute mill stoppage absorbed by buffer slabs costs no production, while a three-minute caster pump stoppage can force a sequence rebuild and cost six hours. Add both fields to the domain entity, write the migration, and extend the projection mapping so a projected downtime row carries both. Use the final column names so M2a extends rather than migrates.

**Validation.** Project a downtime batch and assert every row has both columns populated and non-negative. Attempt an insert with a null production_impact_minutes and confirm the database rejects it. Confirm no code path derives one quantity from the other.

### T-010 - Run the canonical semantic path end to end through the M1 compatibility boundaries

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Engine / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** CORRECTED IN v2.1. The v2.0 wording implied the final physical topology is in place. It is not, and it is not supposed to be before M2a. What M1 proves is the canonical SEMANTIC path: connection test, dataset registration, incremental import into staging, canonical projection through the customer-authored mapping, genealogy, feature and outcome refresh, then an analysis run - each step reached through the product's own services, never by loading a target table directly. Record row counts at every stage. Note the sequencing: the final external definition contract is built later in M1-P2, so this walk is repeated once it lands - the second walk is what proves the contract did not change behaviour.

**Validation.** One command log showing a single row entering at the source and appearing in material_units and the canonical views, with stage counts that are monotonic and explainable. Every write on the path goes through the final service interface, not a table name - which is what lets M2a replace the storage without this test changing.

### T-011 - Establish and fix the architecture test pool reliability

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Testing / CI/CD |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** NEW IN v2.1, raised by a real failure. The navigation contract pack auto-reverted on a gate that reported 15 files and 57 tests passing with one error: a vitest worker failed to START for noMojibake.test.ts and never ran. A re-run went green, which means the gate is intermittent. The diagnostic inventory found the cause candidate: all 16 architecture test files declare no environment and none needs a DOM, yet the suite spends 40 to 45 seconds of a 62 to 68 second run on environment setup and teardown - roughly two thirds of the wall time paid for a browser environment nothing uses. Add // @vitest-environment node to the architecture test files, or set it for that directory in the vitest config. Do not raise a timeout and do not add a retry: a gate that is argued with is a gate that gets switched off, and one that fails at random teaches the team to re-run until green, which is how a real failure gets ignored.

**Validation.** Ten consecutive runs of the architecture suite on a clean tree, all green, with zero worker-start timeouts, recorded in the evidence folder. Report the wall time before and after; the environment share should fall sharply. This matters because M1-P5 certification depends on this pool - the golden journey and the visual and accessibility gates run on it, on the two days when there is no time left to diagnose anything.

### T-012 - Canonicalise the JourneyRail to J1 to J15

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Frontend / Journey |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.3, raised by the presented-surface audit and it is a Rule 3 violation that has been on every authenticated page. JourneyRail.tsx declares its OWN fifteen-stage journey in a hardcoded STAGES array and renders it as Step N of 15. It is not the canonical J1 to J15: different steps, different order, same numbering. Rule 3 says a second journey written anywhere is deleted rather than reconciled. Four further defects sit on top. Stage 4 Prepare mapping has only /data-integration/prepare as its match prefix, which stage 2 already claims, and the longest-prefix tie-break takes the first, so stage 4 can never become the current stage. Stage 5 points at /data-integration/author-mapping, which the audit rules out of inventory. Stage 8 points at AnalysisJobConfigPage, which leaves the M1 navigation. Stage 12 is labelled AI/ML jobs and points at the Jobs Monitor. Stage 15 points at /assistant, but Chapter 3 gives the assistant no route because G1 is a shell component. Rewrite STAGES to the canonical journey with the Chapter 2 3.3.1 labels, render J1 to J3 as completed commissioning so the count is honest, give each stage exactly one unambiguous match prefix, and repoint stages 5, 8 and 15.

**Validation.** A test asserting the rail's stage count and labels equal the canonical journey, in the same shape as PPIQ-T12. Walk every presented route and confirm the rail highlights exactly one stage, and that the stage it highlights is the journey step that route serves. No stage may be unreachable.

## PHASE M1-P1b - Presentation Fleet v2 - capture, reconcile, enhance, scale, materialise canonical, prove

**17 tasks / 114 hours.** Capture the donor state in code before anything touches it, reconcile what the donor schemas actually contain against what the 36 charts require, extend the generator rather than replacing it, merge the historical generations into one Fleet v2 truth, scale it, materialise the presentation canonical operational entities and compute the analysis entities with the real engines, and only then re-measure and prove every phenomenon. **The source-shaped staging representation and the cross-layer certification open M1-P2; native fixture emission and the full external path are M2a.**

### T-013 - Three-way source reconciliation: KEEP, EXTEND or ADD

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.5, and it exists because the runtime measurement proved the source-shaped donor schemas are not the plant the documents describe. Reconcile THREE things against each other, one row per source structure: what the running source-shaped donor schemas and their data actually contain, measured; what the committed generator and fixtures produce - Backend/tools/generate_demo_dataset.py is 372 lines, deterministic, and emits all eight sources - but MEASUREMENT DURING THIS TASK PROVED IT PRODUCES A DIFFERENT PLANT from the donor schemas, sharing two of six defect codes and none of the downtime reason codes, so it is a THIRD GENERATION to reconcile rather than a starting point; and what the 36-chart blueprint requires. NOTHING IS THROWN AWAY WITHOUT A RECORDED DECISION. Every row gets KEEP, EXTEND or ADD with a reason, and a KEEP is as much a decision as an ADD. The measured baseline is already in the evidence folder and is the input, not a thing to re-derive: ten staged tables, 630 heats, 630 sequences, 5,670 slabs, 5,670 coils, 39,690 stand passes, 1,987 defects across SIX codes, 210 downtime events, 17,010 QA results, and chemistry limited to carbon, manganese and silicon on heats alone.

**Validation.** CLOSED 03-Aug-2026. docs/m1/evidence/source_reconciliation.csv, 13 rows and 15 columns, zero blank cells, one row per source structure. THREE DECISION AXES rather than one, because two findings fit none of the original buckets: structural `decision` KEEP 7 / EXTEND 3 / ADD 3; `data_action` VARY 3 / FIX_DISTRIBUTION 2 / NONE 8, for columns that are populated but carry a single value; and `binding_action` BIND 7 / REBIND 6 / NONE 0, for structures that are complete but reach no chart. Every EXTEND and ADD names the exact field or table; every KEEP names the chart it serves. Supporting evidence: docs/m1/evidence/T-013_source_measurement_20260803_132228.txt.

### T-014 - Capture the current source-shaped donor schemas in a committed generator

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.8, and it exists because the emulation the whole presentation reads has no producer in source control. Backend/database/scripts/110_phase1_demo_source_shapes.sql creates the ten source-shaped donor tables EMPTY and nothing in the repository fills them - the only INSERT INTO src_ anywhere is a single one-row probe in scripts/run/Invoke-PpiqJourneyWalk.ps1. Those rows exist solely inside deploy/.ppiq-snapshots/ppiq_app_20260713_203359.dump, a 29.4 MB binary that scripts/demo/Rebuild-PresentationDb.ps1 restores. Backend/tools/generate_demo_dataset.py DOES have a producer, but it emits the CONTAINER fixtures under deploy/fixtures/demo, and those carry a DIFFERENT plant: five defect codes against the staged six with only SCALE and EDGE_CRACK shared, five downtime reason codes with NONE shared, carbon only against carbon-manganese-silicon, no lf_treatment table at all, and a QA CSV of 1,868 rows against 17,010 staged lab results. The source-shaped donor schemas received several manual enhancement passes during the run-up to the presentation and is therefore AHEAD of the committed generator, not behind it. Extending the committed generator before capturing the staged state would regress the dataset. Write ONE deterministic generator, in source control, that reproduces the CURRENT source-shaped donor schemas exactly - every table, every column, every distribution, every catalogue - from a fixed seed. DO NOT IMPROVE ANYTHING HERE. Improvement belongs to the target specification and to the generator-extension tasks that follow it. This task only makes what already exists reproducible from code rather than from a binary snapshot, which is also what the M2a clean-room rebuild depends on.

**Validation.** Generate into an empty database and diff against the live source-shaped donor schemas with the same tool that closed the presentation reproducibility diff. All ten source-shaped donor tables match on ALL NINE DIMENSIONS the Chapter 3 section 4.5.2a retirement gate requires, because three are not enough to delete an irreplaceable dataset against: schema; row counts; key and cardinality checks including the DISTRIBUTION of children per parent rather than its average; null and population profiles; categorical distributions covering defect_code, reason_code, downtime_category, equipment_code, steel_grade and test_code; numeric ranges and declared quantiles; timestamp ranges and per-unit ordering; genealogy and conservation checks; and identifier shapes, without which a later cross-layer identity check cannot hold. TOTAL DIFFERENCES must read 0. The generator, its seed and its outputs are committed. A second run with the same seed is byte-identical to the first. No figure in the generator is hand-copied from a document: every one derives from the measurement evidence produced by T-013.

### T-015 - Presentation Fleet v2 target specification

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Documentation / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.5. Write the dataset target the fixtures must reach, and write it FROM THE 36 CHARTS, not from FLEET_RELATIONS.md. That document has been wrong three times in one day and it is no longer executable truth. It is a source of ideas, nothing more. Chemistry elements are added because a chart needs them and for no other reason - if the conditional-format table needs sulfur, phosphorus, aluminium or niobium then those are added, and if it does not need twelve elements then twelve are not added. THE DEFECT CATALOGUE IS THE HEADLINE. The current six codes at 351, 347, 341, 335, 319 and 294 are nearly uniform, which is a flat Pareto and exactly the boring condition chart 8 was written against. The target is a real plant distribution - one dominant code, two or three meaningful, a moderate, several smaller, and a long tail of rare defects - plus pure-noise codes that exist to be REJECTED, because a correlation page with nothing to reject proves nothing. State the target scale too, and state whether the current roughly one-third scale is sufficient for the population each chart needs.

**Validation.** docs/m1/evidence/presentation_fleet_v2_target.md naming, for every structure: target row count, target cardinality, and the chart or phenomenon that justifies it. No figure appears without the chart that needs it. The defect catalogue is a table of code, target share and role, where role is one of dominant, meaningful, moderate, rare or negative control.

### T-016 - Extend the generator: defect catalogue and chemistry elements

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** NEW IN v2.5. EXTEND T-015 into the existing generator. The word is extend: the generator already produces a coherent, deterministic, genealogy-consistent plant and that structure is kept. What changes is the defect code catalogue, its distribution shape, and the chemistry element set on the meltshop source, which today carries only carbon_pct, manganese_pct and silicon_pct. Keep the existing seed and determinism so a regeneration is reproducible. Do not renumber or restructure what already works.

**Validation.** Regenerating with the same seed produces the same output twice. The defect distribution matches the target table within a stated tolerance, with a dominant code and a visible tail. Every chemistry element named in the target exists as a column with a plausible per-grade distribution. The existing genealogy conservation rules still hold: coil width, weight and length against their slab.

### T-017 - Extend the generator: grade specification, and shift as BEHAVIOUR

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** REWRITTEN IN v2.6. TWO ADDITIONS to the emulated customer world, never to the product. FIRST a grade_specification source table with grade_code, element_code, min_value, target_value, max_value and unit, so the conditional-format demonstration draws its limits from the customer's own data and never from a literal in PPIQ. SECOND, and this is the part v2.5 had wrong: SHIFT IS BEHAVIOUR, NOT A LABEL. Do NOT add a shift column to a source system that would not realistically record one, just because a chart wants it - that is a fabricated convenience and a customer engineer will smell it. Generate the BEHAVIOUR against a shift and crew calendar: day runs conservatively with lower parameter variance, evening sits between, night carries wider variance or a different operating bias. EXPOSE a crew or shift field ONLY in the source systems where such a field is realistic. Everywhere else the value is DERIVED IN THE TRANSFORMATION from the local timestamp plus a shift_calendar table in the EMULATED CUSTOMER WORLD carrying shift_code, start_local_time, end_local_time, crew_code, effective_from, effective_to and timezone - so the derivation reads customer configuration and never a literal buried in code or in a transformation expression - and mapped onto the existing canonical semantics - process_step_executions.crew_code already exists and no new canonical column is invented. That derivation is also the stronger demonstration: a timestamp, a no-code derived column, shift A B or C, save the transformation, in front of the customer. A ready-made shift column makes the example look easy in the wrong way. Chapter 3 already requires local time and zone handling wherever shift matters, so the derivation is on-contract.

**Validation.** The specification table covers every element the Fleet v2 target names, with at least one heat outside its grade band and at least one inside. The three shift populations are realistic rather than exact thirds. Where a source exposes the field, it is justified in one sentence; where it does not, the derivation exists as a saved transformation and NOT as a hand-written column. The behavioural difference is present in the data before any chart is bound to it.

### T-018 - Extend the generator: downtime two quantities and buffer posture

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.5, absorbing the downtime half of the old field-gap task. The 210 staged downtime events are GOOD MATERIAL and are kept: nine equipment units with 18 to 32 events each, five reason codes across four categories, durations from 196 to 5,374 seconds. What is missing is the second quantity. The source carries only duration_seconds, so stopped_minutes derives from it, and production_impact_minutes is GENERATED INDEPENDENTLY. Both shapes must exist in the data: stopped 45 with impact 0, where a buffer absorbed it, and stopped 3 with impact 260, where a short stoppage forced a sequence rebuild. TWO DERIVED METRICS, NEITHER STORED, AND NEITHER CAN GO NEGATIVE. buffer_absorbed_minutes = MAX(stopped_minutes - production_impact_minutes, 0) and cascade_amplification_minutes = MAX(production_impact_minutes - stopped_minutes, 0). A plain subtraction was wrong: stopped 3 with impact 260 would report minus 257 minutes of buffer absorption, which is not a quantity that exists. Stopped 45 with impact 0 gives 45 absorbed and 0 cascade; stopped 3 with impact 260 gives 0 absorbed and 257 cascade. The canonical model still stores exactly TWO columns.

**Validation.** A distribution of both quantities showing the two shapes present in meaningful numbers, not as single planted rows. At least one event has stopped minutes above zero with production impact zero. At least one has impact materially exceeding stopped. No code path computes either quantity from the other or from the timestamps.

### T-019 - Shift and crew operating-practice regimes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** RE-BASELINED IN v2.5: this extends the FLEET V2 generator, not the old one. The behaviour belongs in the generator and the PROOF stays here. It lands BEFORE the frozen M1 Fleet v2 is merged, scaled and materialised into the presentation layers, so that one materialisation carries every regime rather than three separate ones. There is no import cycle inside M1 - the full external path returns at M2a. NEW IN v2.4, and it exists because correlation alone does not make 36 charts interesting. Crew rotation is structurally present but the behavioural stories are not. Give each shift an operating PERSONALITY rather than a different mean: day runs more conservatively with lower parameter variance, evening sits between, night carries wider variance or a different operating bias. The critical constraint is that the effect must NOT be global. Night shift should show a higher apparent defect rate, and conditioning on grade must reveal that a large part of the difference was night running a harder grade family - while the higher VARIANCE remains after conditioning, and that residual variance is what actually contributes to defect probability. One phenomenon, eight charts: shift bar, box plot, trend, scatter, correlation, conditioned result, defect Pareto, and an assistant question.

**Validation.** The naive shift comparison shows a difference. The grade-conditioned comparison shrinks the mean difference materially while the variance difference survives. Both recorded in the evidence folder. If the conditioned difference vanishes entirely the confounder is too strong and the phenomenon is uninteresting; if it does not move the confounder is absent and the story does not exist. Both are failures worth naming.

### T-020 - Post-maintenance recovery and campaign-ageing regimes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** RE-BASELINED IN v2.5: this extends the FLEET V2 generator, not the old one. The behaviour belongs in the generator and the PROOF stays here. It lands BEFORE the frozen M1 Fleet v2 is merged, scaled and materialised into the presentation layers, so that one materialisation carries every regime rather than three separate ones. There is no import cycle inside M1 - the full external path returns at M2a. NEW IN v2.4. Ladle reline counters and cold-furnace reheat structure exist, but an explicit post-repair recovery regime does not, and it is one of the most visually convincing behaviours available. After a maintenance event the first heat shows raised power-on time, cycle time, energy per tonne and temperature variance; the second is partly recovered; heats three to five approach normal; then the campaign runs at baseline. Not every repair produces the same effect: an EAF warm-up, a ladle refractory campaign reset, an HSM roll change that improves surface quality initially, and an ageing roll campaign that gradually raises roll-mark and wavy-edge rates are four different shapes. Campaign ageing runs the other way - roll wear, refractory age and tundish age increase gradually across a campaign. Together these make time-series charts show pattern CHANGES rather than random noise, which is the difference between a chart that looks alive and one that looks generated.

**Validation.** A box plot of the first N heats after a maintenance event against steady-campaign heats shows a decaying difference across three to five units. A campaign-age trend shows gradual degradation, not a step. At least two repair types produce materially different recovery shapes. Every assertion is the one written in the matrix, not invented here.

### T-021 - Equipment personality and temporal regime changes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Important |
| **Hours** | 6 |

**Description.** RE-BASELINED IN v2.5: this extends the FLEET V2 generator, not the old one. The behaviour belongs in the generator and the PROOF stays here. It lands BEFORE the frozen M1 Fleet v2 is merged, scaled and materialised into the presentation layers, so that one materialisation carries every regime rather than three separate ones. There is no import cycle inside M1 - the full external path returns at M2a. NEW IN v2.4, the remaining two variation families. EQUIPMENT PERSONALITY: the same parameter set behaves differently between two units, so an aggregate view hides a unit-level truth that appears the moment the customer filters by equipment. TEMPORAL REGIME CHANGE: a maintenance event or a change of operating practice shifts behaviour mid-period, so a trend chart shows a regime boundary rather than drift. Both are extensions of structures that already exist rather than new subsystems.

**Validation.** Filtering by equipment changes the shape of at least two charts materially. A trend chart shows a visible regime boundary whose date matches a recorded event in the downtime or maintenance source, so the boundary is explainable rather than decorative.

### T-022 - Merge the best existing material into one Fleet v2 truth

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.9. Chapter 3 section 4.5.2a fixes the M1 fast path: the presentation database is materialised from one frozen Fleet v2 generator, Docker is connection-test only during M1, and the full external path returns as the sole authority at M2a. This is the ONE controlled merge, and it happens once. The T-013 measurement found the presentation database carrying three generations of the same plant: the enhanced source-shaped donor schemas captured by T-014, an older and roughly three times larger dump population, and canonical rows matching that older generation exactly. Take the SEMANTICS from the captured baseline - the six defect codes, the three chemistry elements, the ladle furnace stage, casting speed, superheat, the richer downtime vocabulary - and take the SCALE LESSONS from the older larger generation. Produce one reconciled Fleet v2 definition in the generator, with a written decision per conflict. DO NOT copy rows between layers to achieve this. Copying a piece here and fixing a piece there is what produced three generations in the first place; the merge is expressed in the generator or it is not expressed.

**Validation.** One reconciled Fleet v2 definition exists in the committed generator with a recorded decision for every conflict between the captured baseline and the older generation. No row was copied between layers to reach it. The captured baseline of T-014 still regenerates byte-identically from its own seed, proving the merge added a definition rather than overwriting the capture. Every semantic element named in the source reconciliation survives the merge or carries a written reason for being dropped.

### T-023 - Scale Fleet v2 to the target plant size

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.9. The captured baseline is 630 heats and 5,670 coils. That is the size the manual enhancement work happened at, and T-014 deliberately captured it unchanged so that captured existing work stays distinguishable from newly designed enhancement. Scale is a SEPARATE, LATER and VISIBLE step for that reason. Raise the generated plant to the target size - about 2,400 heats and about 17,000 coils - carrying the full enhanced field set. Scale is a generator parameter, never a copy of the older generation's rows. A phenomenon that only appears at one size was an artefact of that size, so every planted behaviour is re-proved after scaling.

**Validation.** The generator produces the target size from a parameter, deterministically, from the same seed family. Every planted phenomenon declared so far still holds after scaling, measured rather than assumed, and any that does not is recorded as a finding before the phase continues. Row counts, cardinalities and categorical distributions are reported at both sizes side by side. The smaller captured baseline remains reproducible, so the two sizes are two outputs of one generator rather than two datasets.

### T-024 - Emit and populate the presentation canonical operational entities

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.9, replacing the import-and-project contracts that assumed a live M1 pipeline. Chapter 3 section 4.5.2a fixes the M1 fast path: the presentation database is materialised from one frozen Fleet v2 generator, Docker is connection-test only during M1, and the full external path returns as the sole authority at M2a. Materialise the canonical operational entities of the presentation database - material units, parameter observations, quality events, downtime events, genealogy edges and their identity resolution - directly from the frozen Fleet v2 generator. PREPARED ROWS ARE PERMITTED; PRESENTATION-ONLY PRODUCT BEHAVIOUR IS NOT. A surface that would refuse on real customer data refuses here too, and no code path may exist that only runs because this is the presentation database. The older canonical population and its provenance residue - the un-neutralised source system label, the unexplained baseline rows, the stale import batches and the validation-fixture and DEMO-vocabulary mappings - are replaced, not merged with, the new population, and only after the replacement rows exist.

**Validation.** Every canonical operational entity is populated from the generator and from nothing else. No row of the older generation survives, proved by a provenance query returning zero rows outside the Fleet v2 label. Genealogy is complete and conserved: every coil resolves to a slab, a sequence and a heat, with no orphan and no cycle. The downtime two-quantity contract closed in T-009 is populated from a real source field rather than defaulted. No widget bound before this task is left pointing at a deleted row.

### T-025 - Compute and populate the presentation analysis entities with the real engines

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Analytics / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.9. The analysis surfaces need correlation results, feature values, outcome values, readiness states and findings present in the presentation database before the customer meeting. THESE ARE COMPUTED, NEVER AUTHORED. Run the product's own statistical, correlation, feature and readiness engines over the canonical operational entities and persist what they return. Writing plausible-looking analysis rows by hand would make every intelligence surface in the demonstration theatre, and would break the design law that prepared data is permitted while presentation-only behaviour is not. Where an engine legitimately refuses - insufficient support, readiness not met - THE REFUSAL IS THE RESULT AND IS PERSISTED AS SUCH, because a readiness gate reporting a measured value beside its threshold is the product working rather than the product missing.

**Validation.** Every analysis entity row carries a compute run identity that names the engine, its inputs and its version. No analysis row exists without one. Re-running the engines over the same canonical data reproduces the same results, so the analysis layer is derived rather than stored opinion. At least one genuine refusal is present and rendered honestly on its surface. No hand-authored row is present anywhere in the analysis entities, proved by the compute-run coverage query returning full coverage.

### T-026 - Phenomenon test harness: manifest schema and runner

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Testing / Presentation data |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.6. v2.5 allowed six hours to prove roughly 104 phenomena, which is achievable ONLY if the validation is parameterised and is nowhere near enough if a junior writes sixty to a hundred bespoke SQL tests one at a time. Build the harness instead. Manifest columns: phenomenon_id, population_query, expected_direction, minimum_population, expected_effect_band, conditioning_variable, expected_after_conditioning, negative_control. The runner walks the manifest and reports per row: population met or not, direction matched, effect inside its band, conditioned result inside its band, and negative control silent. A NEGATIVE CONTROL THAT STARTS CORRELATING IS A FAILURE, not a curiosity. This is what keeps the fleet changeable later without re-doing the whole QA pass from scratch.

**Validation.** The harness runs against a manifest of at least three hand-checked phenomena and produces a pass, a fail and a refusal, all three demonstrated. A phenomenon whose population is below its minimum reports INSUFFICIENT rather than passing quietly. The runner exits non-zero if any row fails.

### T-027 - Populate the manifest and prove every phenomenon

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Testing / Presentation data |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.6, and it CLOSES the reopened matrix. Fill T-026 from the phenomena matrix declared in T-008, whose parts one and two closed as PRE-GENERATION SPECIFICATIONS before any generator work, one manifest row per phenomenon, and run it against the prepared presentation canonical operational and analysis entities, which under the M1 fast path of Chapter 3 section 4.5.2a are materialised from the frozen Fleet v2 generator rather than imported during M1. THE BANDS ARE NOT WRITTEN FROM THIS DATA. Expected direction, acceptable band and negative control were PREDECLARED in T-008 and refined in T-015, both before the data existed. This task MEASURES the observed effect and tests it against that predeclared target. Writing the band from the data and then testing the data against it is a self-fulfilling test and proves nothing. Where an observed effect falls outside its PREDECLARED band, the finding is recorded and the phenomenon is either fixed in the generator - triggering a corrective regeneration - or removed from the matrix with a written reason. THE BAND IS NEVER WIDENED TO MAKE A ROW PASS. Widening a predeclared band after seeing the result is the same defect as writing it from the result.

**Validation.** Every phenomenon in the matrix has a manifest row and a result. The reopened parts one and two of the coverage matrix are rewritten against measured reality and closed. Every one of the 36 charts references at least one phenomenon that the harness proves. Every negative control is silent. A failure here may trigger a corrective rerun of T-024 and everything downstream of it; that rerun is recorded as a correction, never treated as routine.

### T-028 - Verify the confounded correlation and the insufficient-support refusal

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 2 |

**Description.** RESCOPED IN v2.3 FROM 8 HOURS TO 2. Both phenomena appear to be present already: the seventeen relations include several where an aggregate association changes materially once grade or thickness is conditioned on, and the control defects SCRATCH, DENT and SEAM are deliberately uncorrelated. So the work is to PROVE both, not to plant them. Run the naive analysis, then the conditioned analysis, and record the difference. Then find or create one outcome that has genuine data but too few independent units or outcome events to pass the readiness gate - thresholds in ReadinessGate.cs are heats 60 ready and 30 partial, events 40 and 15, minority 0.10 and 0.03, completeness 0.95 and 0.85. Do not weaken any threshold to produce either result.

**Validation.** A recorded finding that survives naive analysis and is reported as not surviving stratification, and a recorded Blocked outcome whose reason names the measured value and its threshold. Both outputs in the evidence folder. If either cannot be produced from the existing data, that is the finding, and the remedy is a small generator change with its own estimate rather than a threshold change.

### T-029 - Five-layer realism audit of the emulated plant

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1b |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** REPLACES the previous data-quality pass and is broader for the same hours. Five layers, in order. STRUCTURAL: key continuity across the eight independent sources - Meltshop PostgreSQL, Caster Oracle, HSM Oracle, PKL MSSQL, Downtime MySQL, Parsytec MySQL, Yard file and QA file - so heat to slab to coil to inspection resolves end to end. PHYSICAL: dimensions, weight, temperature, speed and chemistry plausible AND cross-field consistent, because a slab whose width, thickness and length imply a volume that contradicts its stated weight is wrong even when every single value looks industrial on its own. TEMPORAL: EAF then LF then caster then HSM then inspection then yard, with no step preceding its predecessor. STATISTICAL: natural variation, noise, outliers and shifts rather than uniform random. ANALYTICAL: every phenomenon in the manifest is discoverable through the product rather than asserted anywhere.

**Validation.** One SQL script per layer returning zero offending rows. The physical layer must include the density cross-check: derived volume times steel density compared against stated weight, within a stated tolerance. The genealogy weight check is already enforced by a database trigger, so confirm it fires by attempting an invalid insert inside a transaction and rolling back.

## PHASE M1-P2 - No-Code Authoring Shell - wiring, SQL and widget authoring

**11 tasks / 103 hours.** Presentation beat 1 and half of beat 2. The one shell serving five purposes, in both modes, producing one governed definition. Visible contract is final; persistence behind it may be adapted.

### T-030 - Emit and populate the presentation staging representation, source-shaped

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.9, and it opens M1-P2 because it is the direct prerequisite of the authoring shell. Chapter 3 section 4.5.2a fixes the M1 fast path: the presentation database is materialised from one frozen Fleet v2 generator, Docker is connection-test only during M1, and the full external path returns as the sole authority at M2a. Materialise the staging representation of the presentation database from the same frozen Fleet v2 generator, SOURCE-SHAPED AND UNPREPARED: the selected tables and the selected columns as a customer system would expose them, before any join, derivation or linking. This is what the schema tree, the no-code canvas, the wiring surface, the SQL editor and the preview read, so it must look like customer data rather than like a finished model. Its row counts are NOT expected to equal the canonical layer, because one is source-shaped and one is canonical, and a test asserting equality would be wrong.

**Validation.** The staging representation is populated from the generator alone and is genuinely unprepared: no derived column, no pre-joined view, no canonical vocabulary leaking into a source-shaped table. The schema tree, canvas, SQL editor and preview all read it successfully. Identities match the canonical layer exactly - a coil visible here is the same coil there - while row counts differ as expected and the difference is recorded rather than treated as a defect.

### T-031 - Certify cross-layer consistency and retire the obsolete donor state

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** NEW IN v2.9, and it is the gate that makes the fast path safe rather than merely fast. THE CUSTOMER MUST NEVER SEE ONE PLANT IN THE CANVAS AND ANOTHER PLANT IN THE DASHBOARD. Certify that the staging, canonical operational and analysis layers describe one Fleet v2 plant: same grades, same equipment identities, same defect vocabulary, same downtime semantics, same chemistry vocabulary, same QA definitions and units, same genealogy, same time horizon where both layers carry one, and the same planted phenomena. The certification is a TEST that fails the build, not a document. AND ONLY AFTER IT PASSES, THIS TASK EXECUTES THE RETIREMENT GATE, because Chapter 3 section 4.5.2a defines that gate and no task carried it out: back up the pre-retirement state, RESTORE THAT BACKUP SUCCESSFULLY AT LEAST ONCE, dependency-check the obsolete artifacts, then delete the `src_*` donor schemas and remove the stale registered datasets, dead import batches and validation-fixture and DEMO-vocabulary mappings that now have replacements. Nothing is deleted whose replacement is not already generated and certified. Row counts across layers are explicitly NOT compared, because the layers are shaped differently by design; what is compared is the plant universe.

**Validation.** A named test asserts every consistency dimension above and fails the build on any divergence. A deliberate injected divergence - one defect code present in one layer only - makes it fail, proving the gate is switched on. The generator version and seed behind all three layers are identical and recorded. The certification runs in CI rather than by hand, so a later change that breaks consistency is caught by the pipeline rather than in the room. All four retirement-gate conditions are then evidenced in order: the generator reproduces the captured baseline on all nine dimensions; both presentation representations were regenerated from it; this certification passed; and one backup was taken AND RESTORED SUCCESSFULLY. Only then is `src_*` gone, proved by a query returning zero matching schemas, and no obsolete parallel data world remains active anywhere in the presentation database.

### T-032 - Shared Authoring Shell, part 1: the shell contract and the four regions

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** CORRECTED IN v2.1, and this is the largest correction in the backlog. v2.0 said 'restructure the page layout only, do not change the graph model'. That was wrong: the current graph model does not express the final block grammar, and freezing three separate authoring surfaces in front of the customer is exactly what the Visible Contract law forbids. Chapter 4 5.2.1 rules ONE shell serving FIVE purposes - S1 transformation, S2 page and widget, S3 analysis, S4 model, S5 log rule - with the same board semantics, the same lifecycle and the same definition concept. Build SharedAuthoringShell as the single component, with the four regions from 5.2.3: mode bar (Block or SQL) with Run and Validate; schema tree on the inline-start side; board or SQL editor in the centre; toolbox on the inline-end side; debug log across the bottom. Take a purpose parameter S1 to S5. Do not throw away VisualJoinCanvasPage - its 784 lines of typed ports, compiled-SQL pane, dry run, publish and debug log are the strongest asset here. CONVERGE it into the shell as the S1 face.

**Validation.** Open the shell in S1 mode and confirm every capability that VisualJoinCanvasPage had still works: schema tree, join, preview, compiled SQL, publish, debug log. Open it in S2 mode and confirm the same four regions render with the widget toolbox. A vitest asserting one component serves both purposes and that no second authoring page component is exported.

### T-033 - Shared Authoring Shell, part 2: relational block grammar on the board

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** CORRECTED IN v2.1. v2.0 said 'keep that decision' about editing filters and derived columns in the Preparation Definition side panel. That contradicts the design: Chapter 4 5.2.5 puts Join, Filter, Select, Rename, Group by, Sort, Union, Derived Column, Cast and Lookup on the BOARD as relational blocks, not in a side form. A side form is a different product shape and the customer would receive something else after M2. Implement the block set the six-beat demonstration actually needs - source, join, filter, derived column, select or rename - as board nodes with typed ports, and keep drag-time refusal of an illegal connection with a named reason per 5.2.7. The operator lists in the interface must stay byte-identical to the whitelist BuildSafeSelect enforces, so an illegal state is unreachable rather than rejected afterwards. Blocks the design defines but the demonstration does not need are declared in the registry and rendered as unavailable, not omitted.

**Validation.** Build a preparation on the board using source, join, filter and derived column as nodes, preview it, and confirm the compiled SQL matches. Attempt each illegal connection from the enumerated set and confirm each is refused with a sentence. A vitest asserting the interface operator list equals the server whitelist.

### T-034 - Registry-driven schema, table and attribute tree

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Very Important |
| **Hours** | 10 |

**Description.** EXPANDED IN v2.1. The current tree is real - three levels, typed columns, isKeyCandidate markers, schema name from the configuration key Prep:StagingSchema. The final contract asks for more: grouping, drag of a table or a single attribute onto the board, multi-select, search across schema, table and column, the column type AND its nullability, and an approximate row count per table. Nothing in this tree may be a hardcoded table or column name.

**Validation.** Drag one attribute and one whole table onto the board and confirm both produce a valid source node. Multi-select three columns and confirm the selection reaches the block. Search a partial column name and confirm only matching tables expand. Grep the file for any literal table name from the emulated plant; there must be none.

### T-035 - Compiled-SQL pane and debug log with rows and cost

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / SQL Editor |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** The dry-run endpoint already returns the SQL it built, so the pane shows what actually runs rather than a client reconstruction. Keep that. Add the debug log contract from Chapter 4 5.2.8: entries typed Error, Warning or Success, each with a message written for a plant engineer, plus returned row count and an execution cost estimate. Never render a raw exception string.

**Validation.** Trigger three cases and confirm three distinct log entries: a valid preview (Success with row count), a preview returning zero rows (Warning with an explanation), and a rejected operator (Error naming the operator). Confirm no output contains a stack trace or the words 'could not load', 'failed to load' or 'unable to load', which the PPIQ-T09 architecture test forbids.

### T-036 - SQL mode: safe editor, run test, returned columns and the reconstructability rule

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / SQL Editor |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** EXPANDED IN v2.1. The backend contract is strong already: SafeSqlValidator is 295 lines, SELECT and WITH only, token-boundary validation so created_at stays legal, forbidding DDL, DML, COPY, large-object functions, dblink*, pg_sleep*, pg_catalog, information_schema and xp_*. Build the editor over it with syntax highlighting, schema and column autocomplete from the same catalogue the tree uses, a Run test button, the returned column list with inferred types and a sample of returned values. Two rules v2.0 missed and Chapter 4 5.2.12 requires: the toolbox DISAPPEARS entirely in SQL mode, and switching back from SQL to Block is offered only when the SQL is reconstructable as blocks - otherwise the user gets a warning and must confirm explicitly that the block representation will be discarded.

**Validation.** Run a valid SELECT and confirm columns and samples render. Run a DROP, a pg_catalog reference, a pg_sleep call and a forbidden token hidden inside a comment, and confirm each is refused by name. Switch to SQL mode and confirm the toolbox is gone, not merely disabled. Author SQL that cannot be reconstructed, switch back to Block, and confirm the warning appears and requires explicit confirmation.

### T-037 - Certify returned-column role mapping inside the S2 shell

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Important |
| **Hours** | 3 |

**Description.** REDUCED IN v2.1 from implementation to hardening. The mechanism already exists: widget-role-binding.ts provides readRoleBinding, writeRoleBinding, staleRoles and describeStale, persisting the binding by column and detecting stale mapped roles. The remaining work is integration into the S2 face of the shared shell plus certification, not a build.

**Validation.** Assign roles inside the shell, re-run the query unchanged, confirm roles persist. Edit the query to drop a mapped column, re-run, and confirm the stale-role warning names the missing column.

### T-038 - Add Widget and Edit Widget open the shared shell in S2 mode

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** CORRECTED IN v2.1. v2.0 said Add and Edit should both reopen WidgetAuthoringPanel. That is the wrong architecture: Chapter 4 5.1.10 says Add Widget is kind picker, then name, then THE SHARED SHELL IN S2 MODE, then preview, then save - not a standalone panel. WidgetAuthoringPanel.tsx is good code and its Rule 1 discipline is exemplary, since every list comes from GET /analytics/dashboard/metadata with zero plant literals. Carry that discipline into the S2 face of the shell rather than preserving the panel as a separate surface. Wire both entry points - the workspace Add widget control and the per-widget Edit control - to the same shell with the current definition loaded. Rename the local state wizardOpen to authoringOpen; wizard is a leftover from a deleted component and the design does not use the word.

**Validation.** Create a widget through Add, save, reload, then open Edit on the same widget and confirm every field shows the saved value including query, filters, chart type and role bindings. Confirm both entry points reach the same component - a vitest asserting WidgetAuthoringPanel is no longer rendered as a standalone surface anywhere.

### T-039 - Final definition service interface with a compatibility adapter

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Backend / Definition store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 3 4.5.11 specifies one definition_store with definition_versions and definition_dependencies. That table set does not exist; a repository-wide search for definition_store returns zero hits. M1 does not build it. M1 builds the FINAL external contract in front of the current persistence, so M2a can replace the storage without the UI moving. Create IDefinitionService with Create, Update, GetCurrent, GetVersion, ListVersions and Publish, taking a definition kind. CORRECTED IN v2.2: the kind enum was missing S4. The one-shell contract runs S1 to S5, so the enum must carry Transformation (S1), Page, Widget (S2), Analysis (S3), Model (S4) and LogRule (S5), plus the S2 sub-kinds the design also versions - MasterDimension, MasterMeasure, Filter, Hierarchy and Bookmark. Declare the full enum now even where M1 stores only some of them: an external contract that has to gain a member in M2a was never final.

**Validation.** An integration test that creates a widget definition through IDefinitionService, reads it back by version, updates it, and confirms two versions exist. The test must not reference any concrete table name, so that it still passes unchanged after M2a replaces the storage - that is the real acceptance criterion. A second test asserting the kind enum contains all eleven members, so the contract cannot quietly narrow.

### T-040 - Authoring states, keyboard path, RTL and error wording

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** SEQUENCED IN v2.1: this runs AFTER shell convergence, never before, or it certifies a surface that is about to change. Apply the Golden Gate to both modes of the shared shell. Implement all seven states. Add a full keyboard path: tab order through the four regions, Enter to run, Escape to close. Mirror under RTL using inline-start and inline-end only. Replace any raw error string - src/test/architecture/noRawErrorStrings.test.ts fails on could not load, failed to load, unable to load and loading failed, allowlisting only DataFetchBoundary.tsx and ErrorBoundary.tsx.

**Validation.** Run npm run test and the architecture suite; both green. Complete one full authoring scenario using only the keyboard. Switch the document direction to rtl and screenshot both modes.

## PHASE M1-P3 - BI Workspace and the Seven Showcase Pages

**12 tasks / 80 hours.** Presentation beat 2. Seven dashboards are already seeded; this phase is certification, differentiation and interaction quality, not construction.

### T-041 - D2 Page Builder, part 1: create a page and reach the shared shell

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.1 and it was the single largest omission. Customer beat 2 is add a page, add a widget, edit its SQL, arrange it, show it - and there was no task for the first half. The current PageBuilderPage saves and loads, which is worth keeping, but its widget library is a hardcoded demo set (Risk KPI, Defect breakdown, Defect trend, Date range, List filter) bound to hardcoded sources such as schema_view:risk_summary, and its reducer knows only kpi, bar, line, filter-date and filter-list. That is not the final D2 surface. Build the flow Chapter 4 5.1.9 specifies: Create Page, then page name, code and audience, then an empty 12-column grid, then Add Widget opening the FINAL kind picker, then the shared shell in S2 mode. CORRECTED IN v2.2: widget KINDS are structural product grammar and are FIXED, exactly like chart types and the numeric safety limits - what is customer-driven is dimensions, measures, filters and data, and the chart catalogue arrives from metadata. Serve the kinds from the metadata endpoint if you wish, but do not build an extension path for them. M1 does not need the final definition_store; the adapter from T-039 carries the persistence.

**Validation.** From an empty state, create a page, give it a name and code, and land on an empty 12-column grid. Press Add Widget and confirm the picker offers exactly the final structural kind set returned by the metadata endpoint - no more, no fewer, and no compiled list in the reducer. VALIDATION CORRECTED IN v2.2: do not test by adding a kind through the registry, because a customer adding a structural kind is not a design contract. Test instead that the endpoint returns the final allowed kinds and that the reducer contains no hardcoded widget library.

### T-042 - D2 Page Builder, part 2: arrange, save layout and publish

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** NEW IN v2.1, second half of the same flow. After a widget is saved from the shared shell it lands on the grid. Implement arrange, save layout and publish per Chapter 4 5.1.9, reusing the existing layout persistence which already serialises and reloads through the API. Publish makes the page visible in the Workspaces navigation group, which is how the customer sees the loop close.

**Validation.** Create a page, add two widgets through the shell, arrange them, save the layout, hard-reload, and confirm the arrangement is identical. Publish it and confirm it appears in the Workspaces navigation group. Delete the draft and confirm it does not.

### T-043 - Bring the workspace to the final D1 anatomy

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** EXPANDED IN v2.1. v2.0 described header, selections bar, grid and associative strip, and treated the current DashboardFilterBar as the final architecture. Chapter 4 5.1.2 asks for more: header, PERMANENT selection bar, associative strip, SHEET NAVIGATOR, and a 12-column widget grid. It also defines the relationship between a filter WIDGET placed on the page and the page-level selection state, and the as-of and edit semantics. Do not freeze DashboardFilterBar as the final shape - align it to the specified selection bar, and add the sheet navigator so a page with more than one sheet is navigable.

**Validation.** Screenshot against the 5.1.2 diagram. Apply three selections and confirm three removable chips appear and that removing one updates every widget. Add a second sheet and confirm the navigator switches between them while the selection bar persists. Place a filter widget on the page and confirm it composes with the page-level selection rather than competing with it.

### T-044 - Certify the three operational dashboards and fix their bindings

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** SPLIT IN v2.3 to respect the twelve-hour rule. Part one covers the three OPERATIONAL pages: Production and Shift, Quality and Chemistry, Equipment and Downtime and Flow. The seeded set is already seven and it already matches the intended story, so this is certification, not construction. Working from the coverage matrix, fix each failing widget in this order of preference: correct the widget definition in the seed script; then the chart type; only then the code. A single-row bar chart is a binding defect, not a data shortage - the last audit found Defects by Equipment returning one row, and the remedy there is canonical equipment attribution or relationship resolution, never generating more rows. AUDIT HARDENING IN v2.9.2. Treat presentation semantics as part of the binding contract. A widget is not certified merely because it returns rows: do not present raw GUIDs/technical identifiers when a registry/customer label exists, do not promote `unknown` into a meaningful business category unless the source genuinely carries an unknown bucket, and do not use a dimension whose usable cardinality makes the chart analytically degenerate. Fix the reusable binding/relationship/label path rather than Fleet-v2 screenshot-patching.

**Validation.** Every widget on these three pages returns rows and renders the chart type its title implies. Confirm no widget title describes something other than what it plots. The chemistry conditional format draws its limits from the grade specification source data, never from a literal in the product. For every presented widget, record dimension cardinality and the human-readable label source. No raw UUID/GUID is visible where a registry label is resolvable. A Pie/Donut with one effective category, a Heatmap with only one meaningful axis, or a trend whose x-axis is merely an arbitrary date bucket for a non-temporal question fails acceptance even if it renders.

### T-045 - Certify the analysis and model dashboards and choose the six shown

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** SPLIT IN v2.3, part two. Covers Parameter Deep Analysis, Correlation and Statistical Findings, Risk Intelligence and Model Insights. Correlation is the weakest today - roughly two widgets - and it is the page that should exploit the planted relations hardest. Model Insights must stay honest: if the production model is not ready, show the five readiness dimensions with their measured values beside their thresholds rather than a fabricated prediction curve, because that refusal IS the selling point. Then record the decision: ALL SEVEN are certified, SIX are shown, and which six depends on the audience - an operations-heavy customer sees Risk and Model Insights is held back; a technical customer sees Model Insights and Risk is shortened. Nothing is deleted from the product to make a number. AUDIT HARDENING IN v2.9.2. A technically rendered but information-free visual is not an insight. A flat-zero series, single-slice donut or constant risk-class chart must be rebound to a meaningful supported question, or replaced by the honest readiness/coverage/refusal state that explains why no analytical variation exists. Prefer measured readiness, population, evidence coverage and contributor/refusal semantics over decorative empty charts.

**Validation.** Every widget on these four pages returns rows. Confirm Model Insights makes no claim a live model has predicted anything unless one has. Record the primary six, the backup, and the reason for each choice, in the demonstration script. The six customer-facing pages contain no chart whose visible distribution is constant or single-category unless the visual is intentionally and explicitly a readiness/refusal/coverage statement. Model Insights and Risk must communicate measured state and evidence, not create the appearance of intelligence from a constant series.

### T-046 - Register the final chart grammar and implement the presentation subset

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** CORRECTED IN v2.1, and this was a real design conflict. v2.0 declared the ten currently implemented chart types as the closed grammar and said do not extend it. Chapter 4 5.1.5 defines seventeen: Bar, stacked Column, Line, Area, Combo, Pie, Donut, Scatter, Heatmap, Pareto, Box plot, Histogram, Gauge, Waterfall, Table, Pivot table and KPI. Freezing ten would show the customer a smaller product than the one they receive. The correction is not to build seventeen renderers in ten days: declare the FINAL seventeen in the registry with an implemented or not-yet-available state, implement only the subset the six presentation pages need, and have the switcher offer compatible implemented types while saying in one sentence why an unavailable or incompatible type is not offered. ORDERED IN v2.2 to run before the six-grammar task, which selects from this registry. AUDIT HARDENING IN v2.9.2. Compatibility is semantic, not only renderer-capability metadata. Encode and test at minimum: temporal+numeric -> Line/Area or appropriate Bar; categorical+numeric -> Bar/Pareto; two meaningful categorical axes plus a measure -> Heatmap; numeric×numeric -> Scatter; numeric distribution -> Histogram/Box; low-cardinality categorical share -> Pie/Donut. Do not offer Pie/Donut-by-date, a Heatmap without two meaningful axes and an intensity, or a one-value categorical Pie merely because the renderer can draw it.

**Validation.** The registry returns seventeen chart types. The switcher on a temporal-dimension widget offers only compatible implemented types and gives a reason for each omission. Flip one type from not-yet-available to implemented in the registry and confirm it becomes selectable with no code change. Switch a KPI to Bar and back to KPI and confirm the round trip works. Add known-compatible and known-incompatible binding tests for those pairings. A switcher must explain why an incompatible type is unavailable. Deliberately pass Date+measure to Pie/Donut and one-axis data to Heatmap and prove both are refused before rendering.

### T-047 - Give the seven pages distinct visual grammars from the registered grammar

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 10 |

**Description.** Runs AFTER the chart grammar registry, because the grammars are chosen from the FINAL registered set rather than from whichever types happen to be implemented today. Seven pages that are all bar charts read as one page shown seven times. The intended languages, page by page: Production takes KPI with trend, stacked column by shift, bar by grade, area for weekly throughput and a detail table; Quality takes KPI with sparkline, Pareto by defect type, stacked bar by grade, a positional heatmap of defects along coil length and width, and a conditionally formatted chemistry table; Equipment takes bar, Pareto and a paired-column comparison of equipment stoppage against production impact; Parameter Deep Analysis takes histogram, box plot by grade and scatter; Correlation takes a parameter-by-outcome heatmap, a ranked contributor bar and a before-and-after conditioning pair; Risk takes KPI, trend, distribution and a contribution table; Model Insights takes the five readiness dimensions as status cards with coverage bars. AUDIT HARDENING IN v2.9.2. Distinct analytical grammars must still look like one product. Every page uses the same application typography, spacing scale, control primitives, card anatomy, state colours, focus language and motion rules; variation comes from the analytical story and chart grammar, not from page-local design systems.

**Validation.** Place the seven pages side by side as screenshots. A reviewer who has not seen the product must be able to say what each page is for from the shapes alone. Every chart type used appears in the registry as implemented. The conditional-formatting table must draw its limits from the grade specification source data, never from a literal in the product. In the seven-page screenshot comparison, reject any page that introduces a different font scale, card language, control styling or motion vocabulary. The pages must be recognisably different in analytical purpose and recognisably identical in product identity.

### T-048 - Associative model, part 1: the alternative state and registry-driven fields

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** SPLIT IN v2.2 by dependency. Part 1 has no dependency outside this phase and runs here; part 2 needs the relationship resolver and runs in M1-P4 after it exists. Do not rebuild the excluded pivot - it already works, an excluded value can be clicked and the selection pivots. The work here is twofold: add the ALTERNATIVE state, which is the fourth state Chapter 4 5.1.3 requires and which is missing today; and make the associative field set registry-driven instead of a fixed list.

**Validation.** All four states render distinctly - selected, possible, excluded, alternative - on the token colours, selection Electric Cyan and excluded Muted Steel. Add a dimension through the registry and confirm it appears in the strip with no code change.

### T-049 - Certify layout drag, resize, save, reload and responsive behaviour

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Important |
| **Hours** | 4 |

**Description.** REDUCED IN v2.1 from implementation to certification. useDashboardGridLayout and useDashboardLayoutPersistence already serialise, save and reload the grid through the API. The work is hardening and proof, not building: confirm the Save layout control confirms with a toast, that a reload restores exactly what was saved, and that the behaviour holds at 1920x1080, 1440x900 and 1280x800.

**Validation.** Move and resize three widgets, save, hard-reload, and confirm the layout is identical. Repeat at each of the three widths. Record a short screen capture into the evidence folder.

### T-050 - Drill to population, provenance and evidence

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** RETARGETED IN v2.1. The v2.0 assessment of the drawer's visual state was stale, and the real value is not the transition. Focus on the chain: click a point, see the POPULATION behind it, see its PROVENANCE, and reach the source evidence. Correct the off-palette colours to the token set and position the drawer logically with inline-start and inline-end so RTL mirrors correctly. Add the open transition and respect prefers-reduced-motion, but treat that as the smallest part of the task.

**Validation.** Click a bar and confirm the drawer lists the underlying population with a resolvable provenance handle and a path to the source rows. Set prefers-reduced-motion and confirm the transition is suppressed. Switch to RTL and confirm the drawer opens from the correct edge. Confirm every colour used appears in the token set.

### T-051 - Widget failure isolation and the seven states

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Chapter 4 requires that one widget failing never destroys the page, and that filtered-empty is distinguishable from genuinely empty. Wrap each widget in its own boundary and implement all seven states with the correct colour semantics: Blocked and Refused in Amber, Failed in Hot Red, Empty and Filtered-empty in Muted Steel with different wording.

**Validation.** Inject a failure into one widget query and confirm the other widgets on the page continue to render and interact. Apply a filter that returns no rows and confirm the widget says the selection returned nothing, not that there is no data.

### T-052 - Remove the hardcoded parameter default from the API client

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** Frontend/PlantProcess.Web/src/api/productCoreApiClient.runtime.ts line 300 reads `parameterCode: filters.parameterCode || "CastingSpeed"`. That is a steel-specific literal in product logic and a Rule 1 violation reachable by a customer. Remove the fallback. When no parameter is selected, either omit the field or resolve a default from the parameter registry returned by the metadata endpoint.

**Validation.** Grep the whole src tree for CastingSpeed and confirm the only remaining hits are in demo content or test fixtures, never in product code paths. Load a parameter widget with no parameter selected and confirm it either shows a chooser or a registry-resolved default, and does not silently query a steel parameter.

## PHASE M1-P4 - Journey J4 to J15 and the Engine Slice

**16 tasks / 106 hours.** Presentation beats 3 and 4. RENAMED IN v2.2: the website tasks live in M1-P5, so this phase no longer carries beat 6. Every visible transition credible with no dead end. J1 to J3 are commissioning per Chapter 5 and are narrated, not demonstrated.

### T-053 - Reduce the demonstration navigation and add the inventory ratchet

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Design contract |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** NEW IN v2.3, also from the presented-surface audit. Thirty-five navigation targets were classified and ELEVEN have no Chapter 3 contract, nine of them in the System group: the widget schema-drift diagnostic, author-mapping, four advisory pages, edge collector, historian connector, admin preview, brand, and /assistant once the dock lands. Every one is a surface the customer can open, and any surface opened is a frozen contract. Reduce the demonstration navigation to the entries backing the eighteen presented screens. HIDE the rest behind a configuration flag rather than deleting them - they are M2 retirement debt, not demo-day deletions. Then extend PPIQ-T12 with a fourth assertion: every navigation target resolves to a Chapter 3 contract id read from a checked-in inventory file. The inventory is DATA, so adding a page in M2a is a data change and not a code change.

**Validation.** The architecture suite fails when a navigation entry is added whose target is not in the inventory file. Open every navigation group and confirm every entry maps to one of the eighteen screens. Confirm the hidden entries are still reachable by direct URL, so nothing is lost, only unlisted.

### T-054 - J4 Connections: read-only proof and load budget made visible

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Connections |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** The connection page must show, for a configured source, that the connection is read-only enforced, what the load budget is, and the result of a live test. The backing facts already exist: connection_profiles carries read_only_enforced, and ThrottlingDataSourceReader evaluates every read against ISourceLoadBudgetProvider and ISourceQueryRateLimiter before it reaches the source. Surface those three facts on the page.

**Validation.** Open the page with one configured source, press Test, and confirm three visible outcomes: connection succeeded, read-only enforced true, and the current load budget. Then stop the emulated source container and confirm the failure state names the reason rather than showing a raw exception.

### T-055 - J5 and J6 Dataset registry browse and watermark suggestion

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Data integration |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** The Prepare Source for Import page already discovers tables from a live source. Improve schema, table and column search, and make the business key and watermark column suggestions explicit with the reason for each suggestion. Note the demonstration warning: staged tables carry emulator plumbing names such as the generated presentation-staging display name for cast pieces (NOT the donor schema name - `src_*` is retired by then and Chapter 3 forbids calling it staging). Decide deliberately whether to show them with an honest sentence or to present display names; do not discover this live in front of the customer.

**Validation.** Register one dataset end to end and confirm the suggested business key and watermark are shown with reasons and can be overridden. Record which naming decision was taken in the traceability matrix.

### T-056 - J6 Import progress visibility

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Data integration |
| **Priority** | Important |
| **Hours** | 4 |

**Description.** An import that runs with no visible progress reads as a hang. Add a named progress indication driven by the existing import batch records: batch started, rows staged, batch completed, with the dataset name. Use the activity tray pattern rather than a modal.

**Validation.** Start an incremental import of at least 100k rows and confirm progress updates at least every few seconds and ends in a completed state with a row count that matches the staging table delta.

### T-057 - J7 Relationship model vertical slice, part 1: publish one relationship

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 3 4.5.10 specifies plant_relationships plus members and paths, with sixteen declared consumers. A repository search for plant_relationships returns zero hits, so this does not exist. M1 does not build the whole model. M1 builds the smallest FINAL slice: the three tables with their real columns, and the ability to declare and publish one relationship between two source entities with its members and cardinality. Use the final table and column names from 4.5.10 so M2 extends rather than migrates.

**Validation.** An integration test that publishes one relationship, reads it back, and asserts it is versioned and marked published. A second test asserting an unpublished relationship is not returned to consumers.

### T-058 - J7 Relationship model vertical slice, part 2: one resolver consumer

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 3 says page and widget associative queries resolve through the published relationship model. Implement the path resolver for the single published relationship and make exactly one widget query use it for a cross-source join, instead of a join written into the widget's own query. This matters because the cross-source correlation is the categorical value proposition and a demonstration that joins inside one dashboard proves the opposite of the product design.

**Validation.** Point one widget at data spanning two sources, confirm it renders, then unpublish the relationship and confirm the widget refuses with a named reason instead of silently returning a partial result. Restore and confirm it renders again.

### T-059 - Associative model, part 2: cross-source state through the published relationship

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** SPLIT IN v2.2 and placed here because it DEPENDS on the resolver built immediately above. Make the cross-source associative state resolve through the published relationship model rather than through a join a dashboard owns. Chapter 3 DF7 requires page and widget queries and associative states to resolve through the published model; a selection that spans two sources and works by a dashboard-local join proves the opposite of the product design.

**Validation.** Apply a selection that spans two sources and confirm the strip and every widget narrow consistently. Unpublish the relationship and confirm the cross-source state refuses with a named reason rather than silently narrowing to one source.

### T-060 - C6 Relationship Browser, minimal read-only slice

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Relationship model |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** NEW IN v2.3. Decision taken at traceability sign-off: show it. The relationship model slice is funded and it is the mechanism that separates this product from a dashboard with joins - a join declared once, published, versioned, and resolved by every consumer. Until now nothing rendered it. Build the smallest honest C6 from Chapter 3 4.4: a read-only list of published relationships, their members, their cardinality and their resolved path, with the evidence for each path. No authoring in M1; authoring is the shared shell in S1. Four hours to make the strongest differentiator visible is the best-value decision on this surface.

**Validation.** Open a published relationship and confirm its members, cardinality and path render with evidence. Confirm an unpublished relationship is visibly distinct from a published one. Confirm the page is read-only - no control on it can modify a relationship.

### T-061 - C2 Mapping Health, part 1: the typed issue contract and the reprocess API

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Mapping health |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** CORRECTED IN v2.1. v2.0 said the API exists and only the control is missing. That was wrong. The current MappingHealthPage consumes a source-level SUMMARY only - mapped field count, drift counts, blocking state - from /mapping-health/summary. There is no issue-row model and no typed quarantine or reprocess contract at all. Build the minimal FINAL slice: a typed issue record with a named code, the offending source row as an example, and a reprocess endpoint. Use the final Chapter 3 4.5.14 code names for the classes you implement so M2a adds the remaining PV classes without the contract or the page changing.

**Validation.** Introduce a deliberately malformed mapping, run the projection, and confirm the API returns issues grouped by a named code with an example row. Call reprocess after correcting the mapping and confirm only the affected rows clear. An integration test asserting the response shape matches the final contract.

### T-062 - C2 Mapping Health, part 2: the final visible page

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Mapping health |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** Build the C2 page shape from Chapter 3 4.4 over the contract above: issues grouped by code, an example row per group, and a Reprocess control. The full fifteen PV classes are M2a work; M1 needs the SHAPE to be final so the customer sees the same page after M2a with more codes in it, not a different page.

**Validation.** Walk the page with three issue codes present, reprocess one, and confirm the group clears while the others remain. Confirm the empty state says the mapping is clean rather than that there is no data.

### T-063 - C5 Genealogy: converge the legacy workbench onto the final two-state surface

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Genealogy |
| **Priority** | Very Important |
| **Hours** | 10 |

**Description.** EXPANDED IN v2.1. v2.0 said make the landing search work, and estimated six hours. The real gap is larger: /materials currently renders a legacy investigation workbench, which is a different surface from the Chapter 3 4.4 C5 contract of a two-state page - a search state and a selected-unit state opening the bidirectional genealogy thread with attribution weights and evidence. Converge the workbench onto that contract rather than adding a search box to it. Keep the genealogy walk itself, which is strong and trigger-enforced.

**Validation.** Search a known material code, open it, walk backward to parents and forward to children, and confirm the attribution weights on each child sum to exactly 1.0 - enforced by a database trigger, so a failure here is a data problem. Confirm the page has exactly two states and that no legacy workbench panel remains reachable from it.

### T-064 - Add job_definitions.target_definition_id and the JB error codes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Jobs |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** A repository search for target_definition_id returns zero hits. Chapter 3 4.5.5a specifies that a job must declare which definition it runs and under what version policy - a pinned version, or the current published one. Without it a job cannot say what it executes, which blocks J12 and tutorial T7. Add the column with a foreign key, the version policy field, and the JB error domain codes for the failure cases named in 4.5.5a.

**Validation.** VALIDATION CORRECTED IN v2.1: do not test by physically deleting the target definition. Definitions are an immutable versioned authority and a physical delete is not a state the design permits. Test the three states that can actually occur - the target is unpublished, the target id is missing, and a pinned version no longer exists - and confirm each fails with a JB code and a readable sentence. Also confirm the run history records the version actually used.

### T-065 - J12 Analysis authoring: converge onto D3 Analysis Toolbox in S3 mode

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** CORRECTED IN v2.1, and this was a Continuity Test blocker. v2.0 said wire the target and version selector into AnalysisJobConfigPage.tsx. That page has no Chapter 3 owner and is not the canonical surface: Chapter 3 4.4 D3 names /analysis/toolbox as the analysis authoring surface, and the design uses one shared authoring model, so analysis is authored in the shared shell in S3 mode. Showing the customer AnalysisJobConfigPage now and replacing it in M2 breaks the contract they were shown. Keep the existing backend services - AnalysisJobDefinitionEndpoints already provides definition-options, list, get, create, update, run and results - and converge the VISIBLE authoring onto D3 in S3 mode, with the target and version selector from the task above. Remove AnalysisJobConfigPage from the M1 navigation and record it on the M2a retirement list.

**Validation.** Author one analysis definition entirely through /analysis/toolbox in S3 mode, attach a target and version, and run it. Confirm the three honest outcomes render distinctly: Completed, Blocked with the measured value beside its threshold in Amber, and Failed in Hot Red. Confirm AnalysisJobConfigPage is unreachable from the navigation used in the presentation. Do not weaken any gate threshold to produce a completion.

### T-066 - One visible readiness authority on Home and Analysis

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** ReadinessGate.cs is complete and correct: five dimensions, thresholds as a record, overall equals the worst dimension via Math.Max over the enum, and every dimension returns a reason string built from the measured value and its threshold. Today that authority is not visible in one place. Build one readiness panel showing the five dimensions, each with its measured value beside its threshold and its state, and place it on both Home and the Analysis surface reading from the same endpoint.

**Validation.** Compare the panel against a direct call to GET /api/ml/foundation/readiness. Every number on screen must match the API response exactly. Change one threshold in configuration and confirm the panel moves, proving it is not a static rendering.

### T-067 - Findings evidence panel, registry-driven throughout

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** EXPANDED IN v2.1. A finding must open into its evidence: method, population, effect size, q-value after Benjamini-Hochberg, whether it survived stratification, and a path to the source rows. StatisticalDiscipline.cs already produces the ranking by absolute effect size with the p-value only as a tie-breaker at q equal to 0.05. Render what the engine computes. ADDED IN v2.1: the page's initial outcome and parameter state must come from the registry as well, not from a hardcoded default - a page that opens on a literal is the same Rule 1 defect as a dropdown built from one.

**Validation.** Open the strongest finding and confirm all six elements are present. Confirm the ordering is by effect size and not p-value, by comparing against the API response. Change the registry's default outcome and confirm the page opens on the new one with no code change.

### T-068 - Retire the hardcoded outcome and grain arrays

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Frontend/PlantProcess.Web/src/pages/Analysis/AnalysisToolboxPage.tsx line 18 declares OUTCOMES as a literal array of four steel-specific keys, and line 19 declares GRAINS as ["coil", "slab", "heat"]. A server registry already exists: table public.ml_outcome_definitions is exposed at GET /ml/foundation/outcomes by MlFoundationEndpoints.cs and carries the grain per outcome. AnalysisJobConfigPage already consumes it. Replace both arrays with the registry call and take the grain from the selected outcome's registry row rather than defaulting.

**Validation.** Confirm the dropdown contents match the API response exactly. Add a registry row through the API and confirm it appears in the UI with no code change, which is the Rule 1 acceptance test. Also confirm no server code path falls back to the grain literal 'coil' when the outcome declares a different grain.

## PHASE M1-P5 - Assistant Dock and Presentation Certification

**15 tasks / 83 hours.** Presentation beat 5 plus the certification gate. Deliberately smaller than the other phases because it runs in the last week and must absorb slippage from M1-P1 and M1-P3.

### T-069 - Website, part 1: the five-product information architecture

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Website / Public site |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** CORRECTED IN v2.1, and this was a material visible-contract error. v2.0 said do not open /products/:code because the wrong architecture is an M2 or M3 fix. That is demo avoidance, not design conformance: the customer sees the header, the navigation and the company positioning whether or not a product route is clicked, and Chapter 6 6.2 states plainly that SOU has FIVE separate products - PlantProcess IQ, MES, QES, Yard and Warehouse Management, Energy Management - with PPIQ as the flagship but NOT as the company and NOT as a container around the other four. The current site is deliberately the opposite: it removes direct product navigation and a validator asserts they are removed. Correct the visible architecture now. This is not a rebuild - Chapter 6 6.2.0 keeps 18 components as-is, enhances 9, and replaces exactly one, LegacyProductRoute. Add the Products mega-menu and the portfolio page, remove the assertion that the sibling products must not exist, and replace the redirect.

**Validation.** The header shows a Products menu listing five products. The portfolio page presents five, with PPIQ as flagship and none of the other four described as a PPIQ capability pack. Update the validator that currently asserts their removal - a test that pins the wrong architecture is worse than no test. Confirm no claim on any product page violates the Chapter 6 6.2.10 honesty rule.

### T-070 - Website, part 2: polish the presentation routes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Website / Public site |
| **Priority** | Important |
| **Hours** | 6 |

**Description.** With the architecture corrected, polish only the routes you will actually open: home, the PPIQ narrative, proof, security and the call to action. Preserve the components the Chapter 6 audit marked keep: HeroTopology, GoldenThread, TrustEngine, SignalVsNoise, useScrollDraw, RoiCalculator, RequestDemoForm, ConnectorHonestyBlock and PositioningTruthBlock. AUDIT HARDENING IN v2.9.2. The strongest existing polished website section is the minimum visual authority for every route touched here. Preserve one premium industrial/high-tech system across typography, spacing, node/card geometry, border radius, glow depth, cyan/green accent behaviour, graphical connector language and motion. Fix visible mojibake or HTML entities on the presented routes rather than carrying them into the meeting. New sections may extend the design system but never introduce a weaker second visual language.

**Validation.** Click every link on every route you will open and confirm none is dead. Check desktop, mobile and keyboard navigation on those routes. Confirm no page shows a blocker, an unfinished item or a failed test, per the standing website honesty rule. Add a side-by-side visual comparison against the strongest existing website section at desktop and mobile. No text overflow, low-contrast text, disconnected graphical wires, raw HTML entities or encoding damage. Motion communicates data/system flow and respects `prefers-reduced-motion`. If a new section makes the company look less sophisticated than the reference section, T-070 is not done.

### T-071 - Build the G1 persistent assistant dock

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Frontend / Chatbot |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Chapter 4 5.7.1 specifies a persistent dock present on every authenticated page, not a route. Today Frontend/PlantProcess.Web/src/components/assistant/AssistantChat.tsx is rendered by exactly one page, Phase8/AssistantRuntimePage.tsx. Move the chat into a dock shell mounted in AppLayout so it is available on every authenticated presentation surface, with a collapsed and expanded state. This is a visible-contract item: shipping a separate page in M1 and a dock after M2 would fail the Customer Contract Continuity Test. AUDIT HARDENING IN v2.9.2. The dock's configuration/provider path is authentication-aware. Do not fire a protected assistant-configuration request before the authenticated token/session is available; lazy-load or gate it through the authenticated layout lifecycle so the first open does not produce a transient 401 that disappears only after retry.

**Validation.** Open the dock on at least five different pages and confirm the conversation persists across navigation. Confirm the collapsed state does not obscure any control. Add a vitest asserting the dock renders inside the authenticated layout and not as a route element. From a fresh browser session, log in and open the assistant for the first time. The network log contains no assistant/configuration 401, the dock reaches its ready state without manual retry, and the same remains true after hard reload.

### T-072 - Page and widget context envelope

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Backend / Chatbot |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** The assistant must know what the user is looking at. Extend the ask request so the client sends a context envelope: current route, page code, focused widget code, active selections and filters, and the widget's own last result summary with its evidence handles. The endpoint is POST /api/assistant/ask in Backend/PlantProcess.Api/Endpoints/Assistant/AssistantEndpoints.cs, which already accepts ContextChips. Use the context to narrow retrieval rather than to answer.

**Validation.** Ask the same question on two different pages and confirm the retrieved evidence differs. Assert in an integration test that the context reaches the retrieval call and that no context field is echoed into the answer text unverified.

### T-073 - Add the page and widget chunk family to the retrieval corpus

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | AI+ML / Chatbot |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** This is the single highest-value change in the sprint. Backend/PlantProcess.Infrastructure/Assistant/CanonicalChunkProducer.cs currently builds only five chunk families: CONNECTOR, DATASET and MAPPING from configuration, DOC for the honesty contract, and FINDING for the latest correlation results. Nothing describes what is on the page. Add a family that emits one true sentence per widget result, for example: 'On page Quality Monitoring, widget Defect rate by equipment shows EAF 3.4 per square metre and Caster 1.9 per square metre for June 2026, over 1,284 coils.' Every number in that sentence must come from a real query result and carry an evidence handle. Then rebuild the index through the existing POST /api/assistant/reindex endpoint, which is already wired to this producer through NpgsqlRetrievalIndex. This matters because GroundingService blocks any sentence containing a number not present in retrieved evidence, so without this family the assistant refuses every question about a chart.

**Validation.** Reindex, then ask 'what does this chart show' on three different pages and confirm each answer contains numbers that match the widget on screen and carries at least one citation. Then delete the new chunks, reindex, and confirm the assistant returns the honest refusal rather than inventing an answer. Both behaviours must be demonstrable.

### T-074 - Registry-typed quantity guard on assistant answers

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Backend / Chatbot |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** GroundingService.Enforce already blocks any sentence with a number absent from the retrieved claims and blocks the phrases 'root cause', 'is caused by', 'will cause', 'guaranteed' and 'will save'. Add a typed layer above it: when the question names a quantity that exists in the parameter registry, validate the answer's unit, sign and range against the registry row for that parameter. Reject a date where a speed was asked for, a mass where a speed was asked for, and a negative value where the registry declares a positive range.

**Validation.** Unit tests feeding a crafted draft for each rejection case and asserting the sentence is blocked. Then a live check: ask for a casting speed and confirm the answer either gives a speed with the registry's unit, gives an evidence band, or refuses. It must never return a date or a mass.

### T-075 - Citation chips, evidence strip and suggested questions

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Frontend / Chatbot |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Render each citation as a chip that expands to its evidence, add an evidence strip under the answer, and add an Open in page action that navigates to the surface the evidence came from. Add three to five suggested starter questions derived from the current page context so a live demonstration has a safe opening move.

**Validation.** Click a citation chip and confirm it expands to the underlying evidence. Click Open in page and confirm it navigates to the correct surface with the relevant selection applied. Confirm suggested questions change between two different pages.

### T-076 - Certified question pack and offline fallback

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Documentation / Chatbot |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Prepare 10 to 15 questions whose evidence is known to exist, covering at least three pages, and record for each the expected answer shape: a value with a unit, an evidence band with a record count, a conditional answer naming what would narrow it, or an honest refusal. These are not scripted answers; they are a known-good evidence landscape. Select two or three for live use. Prepare the offline path so a network failure downgrades to the extractive model rather than to an error.

**Validation.** Run all 10 to 15 twice, once online and once with the model endpoint unreachable, and record both answers. Any question that refuses in both runs is removed from the live set.

### T-077 - One Playwright journey covering all six beats

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / E2E |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Write a single spec that walks the whole demonstration: connect, register a dataset, import, map and publish the relationship, open genealogy, create a page, add a widget through the shared shell, edit its query, save, open the six dashboards, cross-filter, run an analysis and see the readiness outcome, ask the assistant a question on a page, open the evidence, and open the website routes. The existing E2E stage in the Jenkinsfile already runs the full Playwright suite through deploy/scripts/ci-e2e-stack.sh, so this spec joins a gate that actually executes.

**Validation.** VALIDATION CORRECTED IN v2.1: clean database means a FRESHLY REBUILT ppiq_presentation, produced by Rebuild-PresentationDb.ps1 - not an empty installation. The demonstration runs on prepared data by design, so an empty-install precondition would test something the presentation never does. The spec passes twice consecutively from a fresh rebuild. A deliberate break in any beat fails the spec at that beat with a readable assertion message.

### T-078 - Execute visual regression and accessibility on the presented routes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / Visual and a11y |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** package.json already defines test:visual and test:a11y as genuine Playwright invocations, but nothing in the pipeline calls them, and tools/ci/validate-real-ui-gates.cjs invokes them with --list, so it verifies the gates exist rather than that they pass. Also package.json line 84 defines phase9:matrix with --list. Point the visual and accessibility specs at the presentation routes and run them for real. Remove the --list flags from validate-real-ui-gates.cjs.

**Validation.** Both suites run and pass on every presented route. Confirm by deliberately introducing a contrast failure and checking the accessibility suite goes red, then reverting. The first run on a machine writes visual baselines and reports them as failures; that is expected and the second run is the real one.

### T-079 - Failure injection suite

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / E2E |
| **Priority** | Very Important |
| **Hours** | 3 |

**Description.** Rehearse the failures that are most likely to happen live: one widget query fails while the page keeps working; the assistant refuses because evidence is missing; the API is restarted mid-demonstration; a filter selection returns no rows; the model endpoint is unreachable. Each must produce a designed state, not a stack trace.

**Validation.** Five scripted injections, each with a screenshot of the resulting state in the evidence folder, each showing a sentence rather than a raw error. A red outline with no sentence beside it is a failure of the specification, not an acceptable outcome.

### T-080 - Capture the Customer Contract Continuity snapshots

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / Continuity |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** On the last day of M1, capture the visible contract: a screenshot of every presented page, the navigation tree, control positions, the Add Widget and Edit Widget flow, the wiring and SQL modes, the assistant dock, the engine surfaces, any logging surface shown, and the website routes. Store them under docs/m1/continuity/ with a manifest naming each file and the route it came from.

**Validation.** The manifest covers every row of the traceability matrix. This set becomes the regression truth for M2: after M2 the comparison must show no change to navigation, control placement, authoring flow, terminology or refusal semantics. Additions and speed improvements are allowed; replacements are not.

### T-081 - Write the screen-by-screen demonstration script

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Documentation / Rehearsal |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Write docs/m1/DEMO_SCRIPT.md as a numbered list of screens in the order they will be opened, and for each screen the one or two sentences said while it is on screen, the exact clicks in order, and the expected on-screen result. Mark each of the six beats and its boundary. Include the deliberate cuts as written decisions so they are decisions rather than discoveries in the room: J1 to J3 narrated as commissioning; no login screen; no euro value figure; no live licence tier toggle unless the 30-minute check on LicenseUsagePanel proves it works. CORRECTED IN v2.1: the line 'never open /products/:code' is REMOVED. After the website architecture correction the product pages are part of the story, not a hazard to avoid. Add the two standing warnings: launch with -Profile presentation, and the staged tables carry emulator names such as the generated presentation-staging display name for cast pieces (NOT the donor schema name - `src_*` is retired by then and Chapter 3 forbids calling it staging).

**Validation.** Read the script aloud against a clock without touching the product; it must fit the meeting slot. Then walk the product with the script in hand and confirm every click named exists and produces the stated result. Any mismatch is a defect in the product or the script, and both are fixed before the first rehearsal.

### T-082 - Presentation environment preparation and clean-start verification

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Infrastructure / Rehearsal |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Prepare the machine that will run the demonstration and prove it starts clean, twice. Fixed browser profile with notifications, extensions and update prompts disabled; fixed window size and zoom matching the resolution rehearsed at; emulated source containers started and healthy; API launched with -Profile presentation and verified against GET /api/ml/foundation/readiness; the web app served from a production build rather than a dev server; ports free; screen sleep and screensaver disabled. Note that scripts/run/start-api.ps1 defaults to -Profile local, which resolves to ppiq_app and reproduces an empty Findings page in front of the customer.

**Validation.** Cold-boot the machine and reach the first demonstration screen following only a written checklist, twice, timing both. Record both timings. If the second run needs a step that is not on the checklist, the checklist is wrong.

### T-083 - Three rehearsals, hostile hands and the fallback package

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Documentation / Rehearsal |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Three full timed rehearsals from a clean laptop boot, of which one is run by someone else holding the mouse and trying to break it. Then assemble the fallback package: a database backup, a short screen recording of each beat, and still images of the key screens, so a hardware or network failure does not end the meeting. Time the journey narration against a clock, since fifteen steps at two to three minutes each is thirty-seven minutes of continuous talking.

**Validation.** Two consecutive rehearsals complete with no surprise. The hostile-hands run produces a defect list that is either fixed or explicitly accepted in writing. The fallback package exists on a second device.

# M2a - DEPLOYABLE CORE, ENDS WITH THE ON-SITE INSTALLATION (432 h)

## PHASE M2a-P1 - Canonical Schema Authority and the Unified Definition Store

**11 tasks / 114 hours.** Replace the M1 compatibility adapter with the real definition store and move the schema to its final three-schema topology, without the customer-visible contract moving.

### T-084 - Emit the frozen Fleet v2 into native customer-source fixtures

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Infrastructure / Emulated sources |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** NEW IN v2.9, and it is the M1 to M2 convergence operation. The certified Fleet v2 truth must become the customer-source dataset, and the sources are heterogeneous, so THIS IS GENERATION AND NOT BACKUP AND RESTORE. A PostgreSQL dump of the presentation staging schema cannot be restored into Oracle or SQL Server, and attempting it is how a single-engine shortcut becomes an architecture. Emit native fixtures for each source from the SAME frozen generator: PostgreSQL, Oracle, SQL Server, MySQL, the QA file source and the yard file source, each in that engine's own types, identifier rules and file conventions. Estimated at 10 hours as four database emitters at about two hours each plus two file emitters at about one hour, derived from the target count rather than fitted to the phase.

**Validation.** One frozen generator emits all six native fixture sets from one seed. Each fixture set loads cleanly into its own engine and the loaded content matches the certified Fleet v2 truth on row counts, column sets and every categorical distribution. No fixture is produced by exporting another engine's data. A second emission from the same seed is byte-identical. The emitter is committed with its fixtures, so the customer sources are reproducible from source control.

### T-085 - Clean-room rebuild of the Fleet v2 emulator sources from source control

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** NEW IN v2.7, and it exists because the dataset changed job. Presentation Fleet v2 is not disposable presentation data that ends with the customer meeting - from here it is the CONTROLLED REFERENCE PLANT that every M2 intelligence feature is developed and benchmarked against. That only means something if it is reproducible, so this task proves it is, BEFORE substantial M2 feature work starts. Destroy the emulator state and rebuild it: recreate the emulated PostgreSQL, Oracle, MSSQL, MySQL and file-based customer sources from a CLEAN state using only the committed generator and fixtures - down with volumes, not stop - then prove their structures and key populations. NO IMPORTANT STATE MAY EXIST ONLY IN A DOCKER VOLUME OR ONLY IN ppiq_presentation. Then consume the rebuilt fleet into a FRESH PPIQ database through the product path: discovery, register, import, staging, mappings, canonical, genealogy. DIRECT CANONICAL SEEDING IS NOT A VALID PATH FOR THIS PROOF. THE T-010 READING THAT 106,272 CANONICAL AGAINST 16,640 STAGED PROVED DIRECT SEEDING IS WITHDRAWN - the T-013 measurement showed the M1 database held THREE GENERATIONS of the same fleet at once: the newer enhanced source-shaped donor schemas, an older and roughly three times larger dump population, and canonical rows corresponding to that older imported generation. This task exists to eliminate GENERATION DRIFT, so that one Fleet v2 truth reaches every layer through the one path. This task does NOT redesign the dataset; T-027 already validated it.

**Validation.** Every emulator source comes up from nothing and reports its expected structures and populations. Every M1 source-side enhancement survives the rebuild - the enhanced defect catalogue and its Pareto shape, the required chemistry elements, the customer grade specifications, the shift and crew calendar and its behaviour, maintenance and campaign regimes, equipment personality, the downtime stopped-time and production-impact semantics, stable source identities and provenance, negative controls, confounders, temporal regimes and complete genealogy. Row counts at source, staging and canonical with every difference explained. A sampled unit resolves from canonical back to its source record.

### T-086 - Freeze and certify Fleet v2 as the M2 reference validation dataset

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Testing / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** NEW IN v2.7, and it carries the rule that matters more than the manifest. Version and freeze the phenomenon manifest from M1 with its PREDECLARED expected direction, effect bands, minimum populations, conditioning variables and negative controls. M2 ALGORITHMS MUST DISCOVER OR HONESTLY REFUSE THESE BEHAVIOURS; THEY MUST NEVER KNOW THE ANSWERS FROM CODE. Write a machine-readable M2 baseline manifest recording dataset version, generator version and seed, source schema versions, important source row counts and cardinalities, phenomenon ids, expected population ranges, negative controls, genealogy and conservation checks, and fixture identity hashes where useful. THEN THE IMMUTABILITY CONTRACT, which is the point of the whole task: during M2 the baseline is NOT modified every time an algorithm disappoints. If a feature cannot recover a behaviour the certified dataset genuinely contains, THAT IS A PRODUCT FINDING, not permission to tune the data until the test passes. Adding correlation to the generator so the optimiser succeeds is how a benchmark stops measuring anything. If M2 later needs new scenarios, create a VERSIONED EXTENSION - Fleet v2.1 or v3 - and never silently change the certified M1 baseline.

**Validation.** The manifest exists, is machine-readable, and a harness run against the certified fleet reproduces the M1 result. The immutability contract is written into the repository where a developer will meet it, not only in the backlog. A named test asserts the baseline fixture identity, so a silent change to the certified dataset fails rather than passing quietly. State plainly which questions this baseline is meant to answer - does the engine recover the known good operating region while accounting for grade, equipment, shift and confounding; does practice learning recover the planted behaviour and reject the negative control; does readiness answer where support is sufficient and refuse where it is not. BENCHMARK HARDENING, because a reference dataset that only freezes phenomena cannot support an optimiser: every case carries a `benchmark_case_id`; the controllable parameters are named with their safe bounds; a known-good and a known-bad operating region are declared; an optimisation tolerance states how close a result must land to count as recovered; the units are split deterministically into development, validation and HOLDOUT, and the holdout is not readable by any development run; the expected refusal conditions are declared so an honest refusal is a pass rather than a failure; and TEST-ONLY TRUTH MUST NOT ENTER THE PRODUCTION RUNTIME - a named test asserts that no planted answer, holdout label or benchmark expectation is reachable from product code.

### T-087 - Physical three-schema migration

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Schema |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** The measured database carries twelve schemas with 162 tables in public, of which 108 use a ppiq_ name prefix, while the ruled schemas ppiq_plant and ppiq_meta exist and hold zero tables. Migrate to the three ruled schemas: ppiq_staging, ppiq_plant, ppiq_meta. The staging rename is already prepared, because the canvas reads its schema from the configuration key Prep:StagingSchema rather than a literal.

**Validation.** Rule 2 is provable in one query on a fresh database. All application tests pass. The canvas catalogue lists tables from the renamed schema with only a configuration change. AND THE BENCHMARK SURVIVES THE MOVE: the phenomenon and benchmark harness frozen in T-086 is re-run UNCHANGED after the schema migration and produces identical results. A migration that silently alters the reference dataset destroys every comparison made against it.

### T-088 - Canonical migration order and legacy script archival

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Schema |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Backend/database/scripts contains hotfix, repair, phase and drift-correction scripts accumulated over months, including cases where two scripts created the same table. Define one canonical ordered migration path, archive superseded scripts, and add a truth gate that no two scripts create the same table.

**Validation.** A fresh database builds from the canonical path with no manual step. The truth gate fails if a duplicate CREATE TABLE is introduced.

### T-089 - definition_store, definition_versions and definition_dependencies

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Definition store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Create the tables specified in Chapter 3 4.5.11 with immutable versions and a dependency graph, plus a trigger that rejects a dependency cycle.

**Validation.** Integration tests: create, version, publish, and reject a cycle. Version rows must be immutable, proved by an update attempt that fails.

### T-090 - Move all five definition kinds onto the store

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Definition store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Replace the M1 compatibility adapter behind IDefinitionService with the real implementation for all five purposes: S1 Transformation, S2 Pages and widgets and filters and master items, S3 Analysis, S4 Model, S5 Log rule. The old per-artifact tables become a compatibility projection and then are retired.

**Validation.** The M1 integration test written against IDefinitionService must pass unchanged, since it references no table name. That is the proof that the visible contract did not move.

### T-091 - Impact preview, export and import

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Definition store |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Add dependency impact preview before a publish, and export and import of a definition with its dependencies, per Chapter 3 4.5.11.

**Validation.** Publish a change to a definition three others depend on and confirm the preview lists all three before the publish is confirmed. Export and reimport into an empty database and confirm equality.

### T-092 - Registry authority: dimensions and measures as rows

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** DashboardWidgetQuerySafetyRegistry declares SupportedDimensions as a compiled HashSet that includes ProductFamily, GradeOrRecipe, ShiftCode, DefectType and RiskClass, referenced through DashboardMetadataCodes.Dimensions. That is plant vocabulary compiled into the product and a Rule 1 violation reachable by a customer. Move dimensions and measures to registry rows. Chart types and the numeric limits stay closed, because they are product grammar rather than customer knowledge.

**Validation.** Add a dimension through the registry API and confirm it becomes selectable in the authoring shell with no code change and no redeploy. Confirm chart types remain closed by attempting to add one and being refused.

### T-093 - Plant-vocabulary sweep, part 1: build the term list and the architecture test

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Rule 1 needs an enforcement mechanism before the sweep, or the sweep is a one-off. Create a registry-held list of plant terms (the term list is itself DATA, never a compiled constant) and an architecture test that fails the build when any listed term appears in product code outside registry data, seed content or test fixtures. Seed the list from the dimension names already found compiled into DashboardWidgetQuerySafetyRegistry: ProductFamily, GradeOrRecipe, ShiftCode, DefectType, RiskClass.

**Validation.** Add a term to the list and confirm the build goes red on an existing violation, then goes green once that violation is fixed. Confirm removing a term from the list does not require a code change.

### T-094 - Plant-vocabulary sweep, part 2: clear the violations and rename the canonical grain

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Run the new architecture test and clear every violation it names across backend and frontend. Include the canonical grain literal 'coil', which the canonical layer applies even to aluminium, tyre and batch product types - native grains observed in the data include slab, heat, cast, packagedlot, rawmaterial, aluminiumroll, tyreunit, batch and lot. Rename it to a generic term and migrate existing rows.

**Validation.** The architecture test passes with zero violations. Query the canonical layer for a non-steel product type and confirm its grain is no longer reported as 'coil'.

## PHASE M2a-P2 - Permanent Relationship Model and Projection Quarantine

**11 tasks / 92 hours.** Turn the M1 single-relationship slice into the permanent product mechanism, and make customer data failures visible, typed and recoverable.

### T-095 - Relationship members, cardinality, grain conversion and preferred paths

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Extend the M1 slice to the full model in Chapter 3 4.5.10: members, cardinality, grain conversion between related entities, preferred path selection when more than one path exists, and published versions.

**Validation.** Declare two paths between the same pair of entities, mark one preferred, and confirm the resolver chooses it. Change the preference and confirm the resolution changes with no other edit.

### T-096 - Path resolver, part 1: resolver core and the first eight consumers

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Chapter 2 3.15.4 names sixteen consumers of the relationship model. Build the resolver core, then route the first eight through it: canonical projection, page and widget queries, associative filtering, drill-down, drill-through, genealogy, statistics and correlation.

**Validation.** A regression test per consumer asserting it resolves through the published model, and refuses with a named reason when the relationship is unpublished. Eight tests, no exceptions.

### T-097 - Path resolver, part 2: the remaining eight consumers and the regression suite

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Route the remaining eight through the resolver: feature engineering, model training, prediction, practice learning, remediation search, value calculation, assistant retrieval and evidence. Some of these are built in M2b; add the resolver seam now so they cannot be written against an ad-hoc join later.

**Validation.** Eight further regression tests. For consumers whose engine arrives in M2b, the test asserts the seam exists and refuses when unpublished, which is what stops a later shortcut.

### T-098 - Relationship Browser page and path evidence

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Frontend / Relationship model |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Build the C6 Relationship Browser from Chapter 3 4.4 with the ten-field page contract, showing declared relationships, their members, their paths and the evidence for each path.

**Validation.** Open a relationship and confirm its members, cardinality and path are shown with evidence. Confirm an unpublished relationship is visibly distinct from a published one.

### T-099 - Quarantine, part 1: the table, the reprocess API and the first eight PV classes

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Quarantine |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Chapter 3 4.5.14 specifies quarantine with fifteen typed PV codes. A repository search for projection_quarantine returns zero hits, so this does not exist. Build the table with its columns, the reprocess endpoint, and the first eight validation classes so a bad row is quarantined under a named code instead of corrupting the canonical layer or failing the whole batch.

**Validation.** Craft one malformed input per implemented class and assert each is quarantined under the correct code, while the good rows in the same batch still project.

### T-100 - Quarantine, part 2: the remaining seven PV classes and per-class tests

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Quarantine |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Implement the remaining seven validation classes and give every class its own test fixture, so the class set is provably complete against Chapter 3 4.5.14 rather than approximately complete.

**Validation.** Fifteen fixtures, fifteen codes, fifteen passing tests. A test that enumerates the PV enum and fails if any member has no fixture.

### T-101 - Quarantine retry, reprocess and Mapping Health completion

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Frontend / Quarantine |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Complete the C2 Mapping Health page against Chapter 3 4.4 C2: issues grouped by code, example rows, and Reprocess after the mapping is corrected. The M1 version delivered the shape; this delivers the full class set and the retry semantics.

**Validation.** Quarantine rows under three different codes, correct one mapping, reprocess, and confirm only the affected rows clear. The M1 continuity snapshot of this page must still match.

### T-102 - Identity resolution across sources

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Genealogy |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Harden material identity resolution using material_aliases so the same physical material arriving under two different source identifiers becomes one canonical unit. The schema already supports this: material_units carries a unique key on (site_id, material_code) plus a filtered unique on (source_system, source_record_id), which makes projection idempotent without forbidding rows that have no source identity.

**Validation.** Import the same material under two different source identifiers and confirm one canonical unit results with both aliases recorded. Re-run the import and confirm no duplicate appears.

### T-103 - Genealogy bidirectional walk hardening and weight proof

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Genealogy |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Confirm the genealogy layer walks both directions on the customer's own keys and that attribution weights are enforced to sum to exactly 1.0 per child by the database trigger on genealogy_edges.contribution_weight numeric(9,6).

**Validation.** Walk a chain backward and forward and confirm the same edges are traversed. Attempt an insert whose weights sum to 0.99 inside a transaction and confirm the trigger rejects it, then roll back.

### T-104 - Projection through the versioned mapping, with version stamping

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Projection |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Complete the DF5 contract: source-shaped staging projects into the canonical plant model through the customer-authored versioned mapping, and every projected row records the mapping version that produced it.

**Validation.** Project a batch, change the mapping version, project again, and confirm each row records the version that produced it. No row may carry a null mapping version.

### T-105 - Idempotent reprojection and mapping-version regression

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Projection |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Make reprojection idempotent and add the regression suite: reprojecting the same batch must not duplicate rows, and rolling a mapping back to a previous version must produce the earlier result exactly.

**Validation.** Reproject the same batch three times and confirm row counts are unchanged. Roll a mapping back and confirm the canonical output matches the earlier snapshot byte for byte.

## PHASE M2a-P3 - Job Runtime, Delta Propagation and Security Hardening

**12 tasks / 106 hours.** Make execution bounded and tenancy real. Chapter 4 5.3.9 proves that the answer to large data is architecture, not tighter licence limits.

### T-106 - Job target version policy and dependency DAG

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Jobs |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build on the M1 target_definition_id work: add the full version policy (pinned or current published), the job dependency graph, and cycle validation.

**Validation.** Declare a three-job chain, run it, and confirm order. Introduce a cycle and confirm it is refused at save time with a named code.

### T-107 - Weighted pools, compute weights and admission control

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Jobs |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement mechanisms 3 and 4 from Chapter 4 5.3.2: skip-if-running, latest-only, and admission control with weighted pools per job class.

**Validation.** Schedule more jobs than the pool allows and confirm they queue rather than degrade the machine. Confirm a second instance of a running job is skipped, not queued twice.

### T-108 - stage_watermarks and delta-scoped projection

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Delta propagation |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Today only import is incremental. Chapter 4 5.3.9 requires every downstream stage to be delta-scoped. Add the stage_watermarks table and make canonical projection delta-scoped against it. The arithmetic that justifies this: a naive full scan for one Pro-tier customer is 481 TB per day, while delta-scoped is 5 to 20 GB, a ratio of 24,000 to 1.

**Validation.** Change one source row and confirm projection processes a bounded delta rather than a full scan, evidenced by rows-scanned telemetry.

### T-109 - Delta-scoped feature refresh and analysis, with telemetry

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Delta propagation |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Extend the delta strategy to the feature refresh and analysis job classes, and emit rows-scanned telemetry per stage so amplification can be measured.

**Validation.** Change one source row and confirm the feature and analysis stages each scan a bounded delta. Telemetry must report rows scanned per stage per run.

### T-110 - Chunk manifests, checkpoint, resume and deterministic merge

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Delta propagation |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement Chapter 4 5.3.9.6a: bounded chunks with receipts, checkpoint and resume after interruption, and a deterministic merge so a resumed run produces the same result as an uninterrupted one.

**Validation.** Kill a running job at 60 percent, restart it, and confirm the final result is byte-identical to an uninterrupted run over the same input.

### T-111 - Scan budget and the Scan Amplification metric

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Infrastructure / Monitoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Add scan admission and the Scan Amplification Ratio from Chapter 6 6.1.12.2a, with a baseline and a regression gate that fails the build when amplification rises beyond the baseline.

**Validation.** Record a baseline, then deliberately remove a delta scope and confirm the gate goes red. Restore and confirm green.

### T-112 - Force RLS on every tenant-owned table with an architecture test

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Database / Security |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** The measured database has one RLS policy against 193 tables, while migration scripts 510, 530, 540 and 560 contain dynamic CREATE POLICY loops, so the scripts exist but the coverage does not. Establish the true coverage, then force RLS on every tenant-owned table and add an architecture test that fails the build when a tenant table is added without a policy.

**Validation.** A query listing tenant-owned tables without a policy returns zero rows. Add a new tenant table in a branch and confirm the architecture test goes red before it is merged.

### T-113 - Secret and configuration hygiene

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Security |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Remove PlantProcess__Auth__Users__0__IsBootstrapAdmin=true from env/profiles/local.env and env/profiles/presentation.env, both at line 42. Move the hardcoded PPIQ_E2E_PASS and the CI signing key out of deploy/scripts/ci-e2e-stack.sh. Parameterise the fifteen hardcoded server-IP references. Add secret masking to the audit package generator, whose header currently reads 'Mask Secrets : False' while the package contains credentials. Also fix the duplicated ConnectionStrings__PlantProcessDb line in both env profiles. AUDIT HARDENING IN v2.9.2. Do not stop at deleting today's literals. Make environment separation structural: Production must reject demo/test/bootstrap users, development signing keys, presentation-only source/database defaults and hardcoded host fallbacks. Local, test and presentation conveniences remain available only through explicitly selected non-Production profiles; the same deployable artifact moves environments.

**Validation.** A secret scan across the repository returns no live credential. Generate an audit package and confirm secrets are masked. Confirm the E2E stack still runs with credentials supplied from the environment. Add a fail-closed Production configuration test that deliberately injects a bootstrap admin, a known demo/test credential marker, a development signing key and a presentation/local host fallback one at a time; each must stop startup or fail the configuration gate by name. A clean Production profile passes with all required values externally supplied.

### T-114 - Tenant keys, tenant-aware uniqueness and canonical namespace on new APIs

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Security |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Apply tenant keys and tenant-aware uniqueness to every table introduced in M1 and M2a, and put every new endpoint on the canonical namespace from the outset so the migration does not have to catch up with new work.

**Validation.** Insert the same natural key for two tenants and confirm both persist. A test asserting every endpoint added after this date matches the canonical domain pattern.

### T-115 - Fresh-install Rule 2 acceptance test, ephemeral

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Database / Schema |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** MOVED FROM M1 IN v2.3, and the move is correct on two counts. First, it fails Gate A on its own terms: the customer never sees it, and no presentation-visible feature depends on it. Second, the shape I proposed was wrong. A third PERSISTENT database is a third environment somebody has to remember to maintain, and this project already has exactly two working databases that earn their keep - ppiq_app for development and ppiq_presentation for the prepared demonstration state. Note also that ppiq_dev is a PostgreSQL LOGIN, not a database, which is worth writing down because the name invites the confusion. The correct implementation is an EPHEMERAL fixture created inside the integration test: create a uniquely named clean database, apply exactly the migrations a customer installation applies, apply NO seed, run the one-query Rule 2 proof, assert zero, then drop the database. Nothing survives the test run. The proof is the artifact; the database is not. What it protects is real: a customer's IT reviewer who opens a fresh installation and finds another company's plant rows inside it has found a dead deal, and ninety-four post-EF SQL scripts are where that could hide.

**Validation.** A fresh migration chain produces zero rows outside the declared prefill allowlist. The allowlist is data, at Backend/database/acceptance/rule2_prefill_allowlist.txt, so a table added later is plant data BY DEFAULT and the test goes red until somebody classifies it deliberately. The test fails if any future migration or seed introduces plant, customer or demonstration rows. The temporary database is dropped whether the assertion passes or fails.

### T-116 - API namespace migration, part 1: map the 92 prefixes onto the 27 domains and stand up dual-serve

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Namespace |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Chapter 3 4.3 specifies 27 clean /api/{domain} domains. The repository registers 92 MapGroup prefixes and 544 verb-level routes, with 18 groups under /api/v5, 6 under /api/p15, plus /phase2, /phase4, /phase5, /api/phase8, /api/p09 and /admin/p03p04. Produce the mapping table, then serve both old and new paths during a transition window.

**Validation.** Every one of the 544 routes appears exactly once in the mapping table. Both the old and the new path return identical responses for a sample of twenty routes.

### T-117 - API namespace migration, part 2: migrate the clients and add the token gate

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Namespace |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Move the ~177 frontend API client methods across 13 modules and the 72 methods on productCoreApiClient.runtime.ts onto the new paths. Then add a gate that fails the build when a registered route or a client base path contains a phase, version or task token. Schedule the removal of the dual-serve window as a named follow-up task rather than leaving it open indefinitely.

**Validation.** A test asserting no registered route matches /phase\d+|\/v\d+\/|p\d\d/. Open the browser network tab during the golden journey and confirm no request URL carries a phase or version token.

## PHASE M2a-P4 - Commissioning, Roles, Licence and the On-Site Package

**10 tasks / 120 hours.** Everything required to install and operate at the customer site. The visible surfaces were frozen in M1; this is the backend and the operational package behind them.

### T-118 - J1 to J3 commissioning built for real

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Backend / Commissioning |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 5 classifies installation, licence activation and user provisioning as commissioning prerequisites, which is why M1 narrates rather than demonstrates them. Build them: first-run installation, licence activation with the Ed25519 signed token, and initial user provisioning. Respect the Admin Golden Rule: the SOU support account is auto-provisioned and undeletable, while the customer administrator is a manual commissioning step and is never auto-created.

**Validation.** Commission a site from an empty database following only the runbook, with no developer intervention and no database console. Confirm the customer administrator is not created automatically.

### T-119 - Eight-role catalogue with three enforcement layers

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Backend / Users and roles |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement the role catalogue as the shipped default with a three-role minimum as the smallest legal configuration, enforced at the API, the query and the UI layers. FormalRoleAccessMatrix already models capabilities including AssistantChat; extend rather than replace it.

**Validation.** A matrix test asserting every role against every capability, at all three layers. Confirm a viewer cannot author SQL at any licence tier, which is a standing ruling.

### T-120 - Users and Roles administration surface

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Frontend / Users and roles |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Build the F1 Users and Roles page from Chapter 3 4.4 with the ten-field page contract. The code itself records that Users and Roles and System Health are missing from the UI.

**Validation.** Create, edit, disable and re-enable a user, and change a role assignment, entirely through the interface. Confirm the audit layer records each change.

### T-121 - Licence and entitlement enforcement

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Backend / Licence |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 6 6.3 requires the six commercial dimensions and the capacity envelope to bound a tier together. LicenseLimits already carries AllowsSqlEditor per tier, set by both LicenseService and VerifiedEd25519LicenseService and exposed by LicenseAdminEndpoints. Extend to full metering: retained volume, ingest rate, refresh floor, weighted compute slots and concurrent sessions. Exceeding a meter throttles rather than destroys, and every meter is visible to the customer.

**Validation.** Exceed a meter and confirm the import queues and the job waits for a slot rather than failing. Confirm the customer can see their own approach to each meter in the interface, since a limit the customer cannot see is a trap.

### T-122 - Container architecture and configuration profiles

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Deployment |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 6 6.1.2 specifies sixteen containers with one responsibility each and four configuration profiles. Infrastructure today is eight files and 856 lines. Build the container set, the image policy, health and readiness endpoints, and volume and secret segmentation.

**Validation.** Bring up each of the four profiles from a clean machine. Every container reports healthy. Confirm no container has a responsibility that belongs to another.

### T-123 - Install package, migration runner, upgrade and rollback

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Deployment |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build the fresh installation and upgrade paths from Chapter 6 6.1.4, including what an upgrade may never do, and a rollback path. Migration runs as a deployment step rather than as a manual action.

**Validation.** Install on a clean machine, upgrade from the previous version, then roll back. All three complete without manual database intervention. Run the fourteen post-deployment acceptance checks from 6.1.4.6.

### T-124 - Backup with a tested restore acceptance procedure

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Backup |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 6 6.1.11 requires not just backup but a tested restore acceptance procedure with a consistency rule. Implement schedule, retention, encryption and the restore rehearsal.

**Validation.** Perform a real restore into a clean environment and run the acceptance procedure against it. A backup that has never been restored does not count as a backup.

### T-125 - Minimum monitoring, health and alerting

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Monitoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Implement the minimum from Chapter 6 6.1.12 required to operate a soft test: per-component metrics, alert severity and escalation, and the operational dashboard. Full observability and SLOs are M3.

**Validation.** Trigger each alert condition deliberately and confirm it fires with the correct severity and reaches the configured channel.

### T-126 - Support runbook and UAT dataset and configuration import

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Documentation / Handover |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Write the runbook the customer's own operator will follow, and build the path to import the customer's UAT dataset and configuration. Include the operator, data engineer and administrator sections.

**Validation.** A person who has not worked on the project commissions a site and completes one import following only the runbook. Every step they had to ask about is a defect in the runbook.

### T-127 - Canonical journey regression and the Continuity comparison

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Testing / Continuity |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Run J1 to J15 against a normal ppiq_app database with no presentation shortcut and no demonstration-only code path. Then run the Customer Contract Continuity comparison against the snapshots captured at the end of M1.

**Validation.** The journey passes on the canonical database. The continuity comparison shows no change to navigation, control placement, authoring flow, terminology or refusal semantics. Any difference is a defect in M2, not an improvement.

# M2b - INTELLIGENCE COMPLETION, SHIPPED DURING THE SOFT TEST (233 h)

## PHASE M2b-P1 - Intelligence Substrate and Practice Learning

**11 tasks / 119 hours.** Shipped as a governed update during the soft-test period. The readiness gate requires 60 independent units and 40 outcome events, so these engines must not be relied upon to become statistically ready during the initial soft-test window - a high-throughput plant can reach those thresholds quickly, and the wording matters because the absolute version was corrected once already in v2.0.

### T-128 - Feature store, outcome store and snapshots

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Intelligence store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build the versioned feature and outcome history with snapshots, per Chapter 3 4.5.12. This is prerequisite to everything downstream, and nothing downstream may invent its own persistence.

**Validation.** Compute a feature set, snapshot it, change the underlying data, and confirm the snapshot still reproduces the original training input exactly.

### T-129 - Compute runs and correlation result persistence

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Intelligence store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Unify compute run records with gate state and gate evidence, and move correlation results onto the common substrate with evidence handles.

**Validation.** Every result row resolves to its run, its gate state and its evidence. A result with an unresolvable evidence handle fails the test.

### T-130 - Model registry, serving identity and fallback

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Model lifecycle |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement serving_role and the six-condition fallback policy from Chapter 4 5.6.7a, with drift observations.

**Validation.** Promote a model, force each of the six fallback conditions, and confirm the correct fallback occurs and is recorded. Confirm no prediction is served by a model without a serving role.

### T-131 - Practice signature, windowing, context and cohorts

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement the practice-learning engine core from Chapter 4 5.6.4b: signature construction, parameter windowing, context, and the comparison cohort.

**Validation.** On the certified Fleet v2 M2 reference dataset frozen in T-086, confirm the engine recovers the good-practice band generated in M1-P1b and does not recover a band from the null control.

### T-132 - Support, confidence, back-off ladder and tolerance sensitivity

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Add support and confidence ranking, the back-off ladder for sparse cohorts, and the tolerance sensitivity test that flags a practice whose result depends on the tolerance chosen.

**Validation.** A practice that survives resampling is ranked; one that does not is flagged. Widen the tolerance and confirm any practice whose result flips is marked sensitive.

### T-133 - practice_statistics persistence, drift and D10 Practice Insights

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Persist practice statistics, implement drift detection against the plant's own demonstrated best, and build the D10 page from Chapter 3 4.4.

**Validation.** Shift the operating data away from the learned band and confirm drift is detected and surfaced with its evidence.

### T-134 - Bindable intelligence registry and evidence handles

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Intelligence store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 3 4.5.13 requires intelligence to be a first-class analytical object: a prediction, a finding, a practice benchmark or a value impact must be bindable by an authored page or widget exactly like canonical data. Build the bindable-intelligence registry so those sources appear in the authoring shell alongside plant dimensions and measures, and so every bound value carries a resolvable evidence handle.

**Validation.** Bind a prediction to a chart through the normal authoring shell with no code change, filter it, drill into it, and open its evidence. Then break one evidence handle and confirm the widget refuses rather than rendering an uncited number.

### T-135 - Tenant-aware uniqueness across the intelligence tables

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | Database / Intelligence store |
| **Priority** | Very Important |
| **Hours** | 10 |

**Description.** Every intelligence table added in this phase is tenant-owned. Apply tenant keys and tenant-aware uniqueness consistently, so two tenants can hold the same natural key without collision, and add the tables to the RLS architecture test built in M2a-P3.

**Validation.** Insert the same natural key for two tenants and confirm both persist. Confirm the RLS architecture test fails if one of the new tables is added without a policy.

### T-136 - Incremental practice recomputation

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Important |
| **Hours** | 10 |

**Description.** Make practice recomputation incremental against the stage watermarks built in M2a-P3, so a growing history does not become a full recompute.

**Validation.** Add one day of data and confirm the recomputation scans a bounded delta, evidenced by the Scan Amplification metric staying within baseline.

### T-137 - Wire a real model behind the assistant provider seam

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Chatbot |
| **Priority** | Optional |
| **Hours** | 3 |

**Description.** MOVED FROM M1 IN v2.1 as optional and cut-first. AssistantInfrastructureExtensions.AddAssistant registers Top15RealAssistantModel when Top15ModelEndpointConfig.FromEnvironment().IsConfigured and otherwise falls back to ExtractiveAssistantModel. Five environment variables, of which PPIQ_ASSISTANT_MODEL_ENDPOINT alone also enables the path. Top15HttpAssistantModelClient POSTs {Question, ProviderKey, ModelKey, ModelVersion, Evidence[{Handle, Text, SourceKind, SourceRef}]} and reads answer or text. No commercial provider accepts that shape, so the work is a small local translating service. Only retrieved evidence is sent, never the database, and the output still passes GroundingService.

**Validation.** With the endpoint unset the extractive model answers and the product still works. With it set, answers improve and every number is still cited. The demonstration must never depend on network access in the room, which is why this is not M1 work.

### T-138 - Assistant finalisation on canonical sources

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Chatbot |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Move the assistant off any M1-specific retrieval or prepared state and onto the canonical intelligence sources, keeping the M1 dock UX unchanged.

**Validation.** The continuity snapshot of the assistant dock still matches. Every answer resolves to canonical evidence with no prepared corpus in the path.

## PHASE M2b-P2 - Prediction, Remediation, Engine Consolidation and Gates

**12 tasks / 114 hours.** The differentiating intelligence, plus the retirement of superseded engines and the completion of the test gates.

### T-139 - prediction_runs, predictions and prediction_current

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Prediction |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build the operational prediction pipeline and prediction_current as the complete operational read model, per Chapter 3 4.5.12.

**Validation.** Score a live population and confirm prediction_current reflects exactly the active predictions with no stale rows.

### T-140 - Prediction drivers and comparables, persisted

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Prediction |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Persist the contributing drivers and comparable historical cases so explainability is drillable rather than narrated.

**Validation.** Open a prediction and confirm its drivers and comparables come from persisted rows, not from a UI computation. Delete the rows and confirm the UI refuses rather than inventing.

### T-141 - Actionable deadline and latency health

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Prediction |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Implement actionable_deadline_utc and met_actionable_deadline from Chapter 4 5.8.8, so a prediction that arrives after the stage that could act on it is visibly recorded as missed.

**Validation.** Force a late prediction and confirm it is recorded as having missed its deadline and is not presented as actionable.

### T-142 - Remediation candidate generation from the customer's own history

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Remediation |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Search the customer's own history for comparable early conditions that later achieved a better outcome, and identify what was done differently in the remaining production stages. Persist the candidate with its proposed later-stage practice, historical support count, expected-effect range, comparable evidence and limitations.

**Validation.** Generate candidates on the certified Fleet v2 M2 reference dataset frozen in T-086 and confirm each carries a support count above the configured threshold, an effect range and resolvable comparable evidence. A candidate with insufficient support must not be generated at all.

### T-143 - The per-prediction nine-check eligibility gate, can_accept and suppression

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Remediation |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Implement the nine checks from Chapter 4 5.6.4d and the can_accept authority from Chapter 3 4.5.12a. The design correction that matters most: eligibility is evaluated PER PREDICTION, not stored as a global property of the template, because the same template is actionable for one unit and not for another that has already passed the stage. Suppressed means suppressed - a failing candidate is not shown at all.

**Validation.** Craft one input per check and confirm each fails by name. Confirm that a candidate suppressed for one prediction is still offered for another where the checks pass. Confirm a suppressed candidate does not appear anywhere in the UI, since a reader under time pressure may act on what they see.

### T-144 - Accept, Reject and Defer with action recording

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Feedback loop |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Implement the human decision boundary from Chapter 3 DF14: Accept, Reject and Defer, each recorded with its actor, timestamp and reason, and each producing an action record. The product must never automatically control the plant.

**Validation.** Take each of the three decisions and confirm each is recorded with actor and reason and produces the correct downstream state. Confirm no code path issues a control instruction to any source system.

### T-145 - Outcome capture, evaluation and escalation

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Feedback loop |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Capture the observed outcome after the fact, write the evaluation that closes the loop, and implement remediation_escalations for the cases that need a human above the operator.

**Validation.** Accept a candidate, let the outcome arrive, and confirm the evaluation is written and feeds the next governed review. Trigger an escalation condition and confirm it routes correctly.

### T-146 - Converge the correlation engine and complete the statistical method matrix

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Engine / Consolidation |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** EXPANDED IN v2.9.2 from the superseded-engine retirement task because the 08-Aug review found a genuine executable-scope orphan. Three `ICorrelationComputeEngine` implementations exist plus a fourth engine key written by a Postgres function; the gated .NET engine is the DI default. Converge to one reachable readiness-gated engine and remove the retired direct-SQL compute path. In that final engine, complete the method selector for the matrix the product claims: keep the proven Numeric×Numeric, Binary×Numeric and Categorical×Categorical paths, and add Numeric×Categorical using a parametric one-way ANOVA only when its assumptions are supported by the available evidence and a non-parametric Kruskal-Wallis fallback otherwise. Persist the selected method, aligned population and per-group sizes, p-value, FDR/q-value and an appropriate effect size (`eta-squared` for ANOVA and epsilon-squared or the documented non-parametric equivalent for Kruskal-Wallis). Method selection is explicit and fail-closed; do not silently reinterpret one statistic as another. Also correct exclusion taxonomy: unsupported pairing, insufficient groups/sample, constant/zero-variance input and other non-computable states are distinct reasons. The current runtime defect where Numeric×Categorical is `NotApplicable` yet the evidence says 'constant / zero-variance input' must disappear.

**Validation.** One engine remains reachable and no compute path can write a finding without the readiness gate. A known-answer Numeric×Categorical fixture selects ANOVA when assumptions are satisfied and Kruskal-Wallis when they are deliberately violated, with method, population/group counts, effect size, p-value and q-value persisted. Run the current `defect.severity` × numeric-feature population and prove the pairing is no longer excluded merely because it is Numeric×Categorical. Then use three falsification cases: a deliberately constant numeric feature must be labelled constant/zero-variance; a deliberately unsupported synthetic pairing must be labelled unsupported method; and a too-small grouped population must be labelled insufficient sample/groups. None may reuse another reason. Existing Numeric×Numeric, Binary×Numeric and Categorical×Categorical regression cases remain green.

### T-147 - Fix the outcome namespace, grain assignment and ordinal loader

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Engine / Consolidation |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Three related defects: the engine and the registry use different outcome namespaces; the ML refresh routine deletes and re-inserts outcome values while assigning grain itself, so a manual grain correction is silently undone; and the ordinal loader selects only the effective sample key, numeric value, category value and heat id, never reading the severity column, so an ordinal outcome always reports a zero minority fraction. v2.9.2 adjacency contract: this task must leave ordinal/categorical outcomes in a shape T-146 can consume without manual repair. `defect.severity` is persisted and loaded as a real multi-class categorical/ordinal outcome with its true class spread, not converted to a fake numeric target solely to make a statistical method available.

**Validation.** One namespace across engine and registry. Correct the grain in the refresh routine and confirm it survives a refresh. Load an ordinal outcome and confirm the minority fraction reflects the real class spread. After a full outcome refresh, pass an ordinal severity outcome through the correlation loader and assert the engine receives the same class labels/counts the store reports; no manual grain or category correction is required between refresh and compute.

### T-148 - Map the 108 page files onto the 40 target pages

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Frontend / Namespace |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Chapter 3 4.3 specifies 40 route pages plus 6 shell components. The repository holds 108 page files in 42 groups with 48 lazy route components, and roughly 14 files are reachable by nobody. Produce the keep, merge and delete decision per file, then delete the unreachable ones under Rule 4, which requires the replacement to land with the deletion in the same change.

**Validation.** A test asserting every page file under src/pages is reachable from a declared route. The count of page files moves toward 40 and no orphan remains.

### T-149 - Delete the legacy redirects and re-verify continuity

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Frontend / Namespace |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Remove the roughly 20 legacy Navigate redirects and the live phase-token routes (/phase8/*, /phase9/*, /phase15/*) that App.tsx still declares among its 69 route paths, then re-run the Customer Contract Continuity comparison.

**Validation.** No route path matches /phase\d+/. The continuity comparison against the M1 snapshots still shows no change to any presented page.

### T-150 - Complete the test gates

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Testing / CI/CD |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Add the remaining pipeline stages from Chapter 6 6.1.3 to reach the specified twenty-two, make visual regression and accessibility blocking, and make the golden journey J1 to J15 a merge blocker. AUDIT HARDENING IN v2.9.2. Completion means executed truth, not the presence of gate files. A mandatory gate may not be satisfied by `--list`/enumeration, may not wrap a failing test in `catchError` or equivalent logic that forces SUCCESS, and may not exist as an orphan validator nobody calls. One canonical pipeline owns each mandatory gate; duplicate/superseded injectors are removed or made non-authoritative.

**Validation.** A deliberately broken golden journey blocks a merge. A deliberately introduced contrast failure blocks a merge. Both then revert to green. Add a mechanical pipeline-truth test that inspects the actual commands for every mandatory stage and rejects enumeration-only invocations and failure-swallowing wrappers. Deliberately make one unit/integration gate fail, one visual/a11y gate fail and one golden-journey gate fail; each must make the canonical pipeline non-green. Any mandatory validator not reachable from that pipeline is either wired in or deleted.

# M3 - SITE STABILISATION, CERTIFICATION AND COMMERCIAL COMPLETION (204 h reserved)

## PHASE M3-P1 - Site Stabilisation and Real-Data Performance

**8 tasks / 96 hours.** Reserved capacity. Half of M3 is written by the customer during the soft test; these phases exist so that work has a home and does not become unplanned scope.

### T-151 - Site defect burn-down

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Backend / Site findings |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Fix what soft testing finds, without changing the frozen visible contract unless a formal product decision is approved and recorded.

**Validation.** Each defect has a reproduction, a fix and a regression test. Any visible-contract change carries a written approval.

### T-152 - Customer data edge cases

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Database / Site findings |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** New source patterns, dirty data, unusual keys, timestamps, nulls, late arrivals and customer-specific mapping requirements, handled through the three doors.

**Validation.** Each case handled through import, registry or authoring. Any case requiring a code branch is escalated as a design gap.

### T-153 - Connector certification against real sources

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Backend / Connectors |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Enable and certify the customer's actual connectors. A catalogue row is not a connector; an unbuilt one stays dimmed and badged as planned.

**Validation.** Each certified connector completes a read-only import against the real source with the load budget enforced.

### T-154 - Query plans, indexes and partition boundaries

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Database / Performance |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Tune against real measurements from the customer's volumes, not against assumptions.

**Validation.** Before and after query plans recorded for the ten slowest queries, with the improvement measured.

### T-155 - Pool weights, scan amplification and model-serving memory

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Infrastructure / Performance |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Tune the concurrency and memory model to the site's measured load.

**Validation.** Scan Amplification stays within baseline under real load. No job class starves another.

### T-156 - Customer definitions built through the product

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Frontend / Authoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Build and validate the customer's real pages, relationships, measures, analyses, models and log rules using the product's own authoring surfaces.

**Validation.** Every customer artifact exists as a definition in the store, created through the interface, with no manual database insert.

### T-157 - Practice and prediction calibration

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | AI+ML / Calibration |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Longer data window, retrain and validate under governance, tune thresholds, measure deadline health.

**Validation.** A governed retraining record exists for each model, with the drift test that gated its release.

### T-158 - Remediation validation against real process constraints

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | AI+ML / Remediation |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Validate actionability rules against the customer's real process constraints. The human approval boundary stays intact.

**Validation.** Each of the nine checks validated against a real constraint. No path permits automatic plant control.

## PHASE M3-P2 - Production Certification, Enterprise Operations and Commercial Completion

**9 tasks / 108 hours.** What the sale depends on. Chapter 6 cannot be frozen until C1 to C4 replace the reference assumptions.

### T-159 - C1 to C4 capacity certification

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Certification |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Run the four capacity profiles and site benchmarks, and replace the ten REFERENCE_ASSUMPTION constants in the Chapter 6 sizing model with measured values.

**Validation.** Every worked example in Chapter 6 6.1.9.6 recomputes correctly from measured constants. Chapter 6 is then frozen.

### T-160 - HA, DR and restore rehearsal

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Resilience |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Production topology, RPO and RTO objectives, and a real disaster-recovery rehearsal.

**Validation.** A full recovery rehearsal completes within the stated RPO and RTO, witnessed and recorded.

### T-161 - SSO and identity integration

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Backend / Security |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Integrate the customer identity provider with the final role catalogue, account lifecycle and emergency access.

**Validation.** Account provisioning, de-provisioning and emergency access all tested against the customer directory.

### T-162 - Site security hardening and sign-off

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Security |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Network rules, secrets, certificate rotation, RLS and tenant proof, audit review.

**Validation.** A tenant-isolation proof from the database alone, plus a signed security review.

### T-163 - Monitoring, SLOs and support escalation

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Monitoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Operational dashboards, alerts, queue and latency and backup and certificate health, and the escalation path.

**Validation.** Each SLO has a measured baseline and an alert that fires before it is breached.

### T-164 - The Value Engine

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Backend / Value |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build value_impacts, cost_assumptions, the value realisation ledger and the D7 Value Dashboard, honouring the two-downtime-quantity rule. This is the only work that moves the economic buyer, and the pilot supplies the real numbers it needs.

**Validation.** A euro figure is only ever shown as a bounded range with its assumptions visible and its evidence resolvable. No number without evidence.

### T-165 - Commercial capacity finalisation and the sales calculator

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Backend / Commercial |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Validate the real user, page, job, DB-link and data bands against measured infrastructure, and build the Sales Administration and capacity calculator from Chapter 6 6.3.8.

**Validation.** A quote produced by the calculator matches the measured server class for the same inputs.

### T-166 - Five-product website production completion

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Website / Public site |
| **Priority** | Important |
| **Hours** | 12 |

**Description.** CORRECTED IN v2.2. The redirect replacement, the portfolio page and the mega-menu are now M1 work, because the customer sees the company architecture in the room. What remains here is production completion of the five product pages: full content, proof sections, SEO and metadata, analytics, lead flows, localization, and the final honesty certification across every claim. AUDIT HARDENING IN v2.9.2. Production completion also certifies one coherent visual identity across all five product pages and shared public-site sections. Typography, UI primitives, graphical language, motion, responsive hierarchy and accessibility must come from the same design system used by the strongest PPIQ sections. Remove mojibake, visible HTML entities and locally invented low-quality box/flowchart styling; motion must reinforce meaning and degrade cleanly under reduced-motion preferences.

**Validation.** Each of the five products has a complete page under the Golden Rule product-page contract. Every claim passes the Chapter 6 6.2.10 honesty rule and the certification is recorded. Lead flow tested end to end on each page. Run a five-product visual-system audit at desktop, tablet and mobile. Zero mojibake/visible entities, text overflow, disconnected graphical connectors or page-local font/control systems. Automated visual/a11y checks are green, reduced-motion is honoured, and a reviewer comparison against the strongest PPIQ reference section records no page as visually less sophisticated.

### T-167 - Documentation, training and production acceptance

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Documentation / Handover |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Runbook, operator and data-engineer and administrator guides, release notes, known limitations, rollback plan and the formal acceptance suite.

**Validation.** The acceptance suite passes and is signed. Known limitations are written down before the customer finds them.


## v2.9.2 AUDIT FINDING -> EXECUTABLE OWNER TRACEABILITY

This table is a freeze gate, not commentary. A finding may have more than one owner when one task closes the immediate presentation symptom and a later task closes the production form.

| Finding | Owner in v2.9.2 | Disposition |
|---|---|---|
| Numeric×Categorical correlation method missing | **T-146** | **EXPANDED - orphan closed** |
| Unsupported Numeric×Categorical mislabeled zero-variance | **T-146** | **EXPANDED - orphan closed** |
| Outcome namespace / grain / ordinal loader defects | **T-147** | Existing owner, validation strengthened |
| Page Builder demo-shaped widget/source assumptions | **T-041 / T-042** | Existing explicit owner |
| Operational dashboard weak bindings, raw technical labels and degenerate dimensions | **T-044** | Expanded |
| Analysis/Model pages showing flat or information-free charts | **T-045** | Expanded |
| Chart renderer compatibility without analytical compatibility | **T-046** | Expanded |
| Seven pages need distinct stories but one product design language | **T-047** | Expanded |
| Persistent Assistant first-open protected-config 401 risk | **T-071** | Expanded |
| Assistant page/widget context | **T-072** | Existing explicit owner |
| Assistant page/widget evidence corpus | **T-073** | Existing explicit owner |
| Typed quantity safety | **T-074** | Existing explicit owner |
| Citation/evidence UX | **T-075** | Existing explicit owner |
| Certified live questions/offline fallback | **T-076** | Existing explicit owner |
| Website visual regression / inconsistent font, boxes, graphical and motion language | **T-070 / T-166** | Expanded M1 + production closure |
| Mojibake / visible HTML entities on public site | **T-070 / T-166** | Expanded |
| Visual/a11y test definitions invoked with `--list` | **T-078 / T-150** | Existing immediate owner + strengthened final gate |
| CI failure swallowed / orphan mandatory validators | **T-150** | Expanded |
| Demo/local/bootstrap/secrets/hardcoded-host leakage into Production | **T-113** | Expanded |
| Incomplete RLS coverage | **T-112** | Existing explicit owner |
| `src_*` retirement, dependency proof and backup/restore | **T-031** | Existing owner; deferred items remain neither done nor waived |
| Final `ppiq_staging` / `ppiq_plant` / `ppiq_meta` authority | **T-087 / T-088** | Existing explicit owner |
| Unified definition authority | **T-089 to T-094** | Existing explicit owner |
| Permanent relationship model and projection quarantine | **T-095 to T-105** | Existing explicit owner |
| Production feature/outcome snapshots and intelligence substrate | **T-128 to T-136** | Existing explicit owner |
| Prediction/remediation/feedback loop | **T-139 to T-145** | Existing explicit owner |
| Economic Value Engine / auditable ROI | **T-164** | Existing explicit owner |
| Product-semantic DB change exists only live / in an ignored pack | **Global Law 7** | Controlled permanently |
| Mutating PowerShell/apply pack can silently miss anchors or fail after mutation | **Global Law 8** | Controlled permanently |
| Two workers can sweep each other's dirty files into a commit | **Global Law 9** | Controlled permanently |
| Future review finding can exist without an executable owner | **Global Law 10** | Controlled permanently |

**Freeze condition:** zero material finding rows may remain without an owner/disposition.

---

## VERIFICATION BOUNDARY

v2.9.2 is a planning/traceability revision only; it does not claim that the newly strengthened validations have already run. Its amendments are grounded in the 08-Aug deep implementation review, the 07-Aug UltimateAudit and the 05/07-Aug handovers. Every hour figure remains an estimate; task/phase/programme hours are intentionally unchanged.

## M1 progress - strict closure view at v2.9.2 freeze

| Metric | Hours | Meaning |
|---|---:|---|
| M1 baseline scope | 574 | every M1 task, complete or not |
| **Done** | **295** | 39 tasks whose frozen validation is currently recorded Done |
| **In Progress** | **32** | T-031, T-040, T-070 and T-071; none of these hours count as Done until their remaining validation closes |
| **Not yet Done** | **279** | baseline minus strict Done; includes the full estimate of In Progress tasks under the no-PARTIAL law |

T-070 is deliberately returned to **In Progress** in this version because the latest handover says its route/visual work exists but the frozen task was never formally closed. This is a status-truth correction, not reopened scope.
