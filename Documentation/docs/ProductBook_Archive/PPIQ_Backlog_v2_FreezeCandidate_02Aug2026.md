# PLANTPROCESS IQ - EXECUTABLE BACKLOG

**Version 2.0 - FREEZE CANDIDATE - 2 August 2026**  
**Supersedes:** Backlog v1.0 of the same date  
**Design authority:** Master Design Chapters 1 to 6 (v4.3 to v4.6)  
**Implementation baseline:** `UltimateAudit_29Jul2026_233112` - 2,051 files, 343,024 lines, re-read file by file  
**Companion:** Concept, Scoreboard and Delivery Roadmap v2.0 plus Amendment 1

---

## CHANGE LOG - THE SIX CORRECTIONS, ALL ACCEPTED

Every one of the six review corrections is applied. None was argued down.

| # | Correction | What changed in v2 |
|---|---|---|
| **1** | **M2a exit criterion contained a real contradiction.** It required J1 to J15 on canonical data, but Chapter 5 T8 covers J13 and J14 including acting on a prediction, and prediction and remediation are M2b work. The Definition of Done was unachievable inside M2a. | **The exit is split.** M2a ends when the canonical journey and every surface work on the customer database with the prediction and remediation steps legitimately reporting readiness-blocked or no-history-yet. **Full functional J1 to J15 acceptance, including acting on a prediction, is the M2b exit.** See the exit criteria below |
| **2** | **The 14-hour ceiling violated the stated 12-hour rule.** v1 silently rewrote the law and then used it eleven times. | **All eleven 14-hour tasks are split.** The maximum task in v2 is 12 hours. The law is restored to what was asked for |
| **3** | **Two phases were outside the 80 to 120 band** - M1-P5 at 62 and M2b-P2 at 126. | **Both fixed by redistribution, not by exception.** M1-P5 now holds the website presentation path plus two tasks that were missing entirely, and lands at 80. Assistant finalisation moved from M2b-P2 to M2b-P1, giving 116 and 114. **All thirteen phases are now inside the band** |
| **4** | **The Task Id law contradicted the ordering.** v1 said lower id means higher priority, then placed an Important task at T-013 and a Critical at T-014. | **The definition changed, which is the better fix.** Task Id is now **planned execution order**, dependency-aware. **Priority is severity** and is a separate column. Dependency genuinely does outrank severity when deciding what to build first |
| **5** | **Programme naming was stale after the option C decision.** The roadmap still said 800 hours and reported a single After M2 column. | Corrected in **Amendment 1 to Roadmap v2**: the programme before M3 is **M1 410 + M2a 400 + M2b 230 = 1,040 hours**, and every scoreboard now reads Today, After M1, After M2a, After M2b, After M3 |
| **6** | **`ppiq_acceptance_empty` sat in M1 but fails Gate A.** | **Kept in M1 with the justification written into the task itself**, as the M1 release gate rather than a demonstration item. The task text now states plainly that if that justification is not accepted it moves to M2a-P1 and M1 drops by four hours. The decision is yours and it is visible |

**One wording correction also accepted.** The claim that practice learning and prediction *cannot produce anything real in the first weeks* was too absolute - a high-throughput plant may reach 60 independent units and 40 outcome events quickly. The correct statement, used throughout v2: **they must not be relied upon to become statistically ready during the initial soft-test window.** Same decision, accurate science.

---

## M1 IS 410 HOURS, NOT 400 - AND THAT IS DELIBERATE

v1 was missing two artifacts that belong in M1: the **screen-by-screen demonstration script** and **presentation environment preparation with a clean-start verification**. Ten hours.

I am not shaving ten hours off other estimates to land on a round number. **That is exactly the fitted-to-budget tell I criticised in another review** - a plan whose phases sum to precisely the budget was fitted, not derived - and having made that criticism I then produced an exactly-400 M1 and an exactly-400 M2a twice. Correcting it in my own document is the only consistent position.

Two ways to close the gap if 400 is a hard envelope, both visible rather than hidden:

- Move `ppiq_acceptance_empty` to M2a-P1 as correction 6 offers, and drop the optional model-shim task which is already marked cut-first. That is 7 hours and lands at 403.
- Or accept 410, which is a 2.5 percent overrun on a ten-day sprint.

---

## MILESTONE EXIT CRITERIA - CORRECTED

| Milestone | Exit criterion |
|---|---|
| **M1** | From a clean laptop boot, with no database console, the six beats run end to end: Connection, Dataset, Import, Relationship and Mapping, Genealogy, Page Builder, Add Widget, Wiring, Compiled SQL, SQL edit, Preview, Save, six dashboards, cross-filter, engine readiness and finding, assistant on the current page, evidence, website. Two consecutive rehearsals with no surprise |
| **M2a** | The customer installs PPIQ on their own infrastructure, connects their own sources read-only, and runs **J1 to J12 plus every J13 to J15 surface** on the canonical database with no presentation shortcut and no demonstration-only code path. **The prediction and remediation steps of J13 and J14 are present as surfaces and legitimately report readiness-blocked or no-history-yet.** The Continuity comparison against the M1 snapshots shows no visible-contract change |
| **M2b** | **Full functional J1 to J15 acceptance, including acting on a prediction**: a prediction is produced from real history, carries drivers and comparables, generates a remediation candidate that passes or fails a named check, is accepted or rejected by a human, and its outcome is captured and evaluated |
| **M3** | The product can be installed at a second customer without anyone remembering how a laptop was configured. Chapter 6 is frozen because C1 to C4 have replaced the reference assumptions |

**Why M2a does not simply absorb M2b.** Moving the 230 hours forward would make M2a 630 hours and push the on-site date out by more than half. The engineering reason for the split stands: the readiness gate requires 60 independent units and 40 outcome events, so **practice learning and prediction must not be relied upon to become statistically ready during the initial soft-test window**. They are better shipped as a governed update partway through the pilot, and the customer sees no change because their surfaces are frozen in M1.

---

## HOW TO USE THIS BACKLOG

| Field | Meaning |
|---|---|
| **Task Id** | `T-001` upward. **Planned execution order, dependency-aware.** It is not severity order - severity is the Priority column. Ids restart at each backlog epoch |
| **Milestone Id** | `M1` presentation, `M2a` deployable core, `M2b` intelligence completion, `M3` site and production |
| **Phase Id** | A group of tasks on one topic, **80 to 120 hours, no exceptions**. Every phase ends pushable |
| **Module** | Frontend, Backend, Engine, AI+ML, Chatbot, Database, Infrastructure, Testing, Website, Documentation |
| **Sub-module** | The specific surface or subsystem |
| **Priority** | Critical, Very Important, Important, Optional. **Severity, not sequence** |
| **Hours** | **No task exceeds 12 hours.** Anything larger was split |

**Laws that govern every task below.**

1. **Temporary data is allowed. Temporary internal implementation is sometimes allowed. Temporary product identity, UX, workflow or behaviour is never allowed.**
2. In M1 the **visible contract is final**: UI appearance, UX flow, routes, terminology, control behaviour and placement, and visible state and refusal semantics. What sits behind it may be adapted.
3. **No architecture enters M1 unless a presentation-visible feature depends on it.** If one does, build the smallest slice of the *final* architecture, never a shortcut.
4. **No fake product answer at any milestone.** Prepared data is allowed and is presented honestly as demo-dataset output.
5. A task is **Done** only when its Validation column passes **in a browser or against a running system**. Compiling is not done. Gates passing is not done.
6. **No PARTIAL status.** A partially finished task is rewritten as its remainder with a fresh estimate.

**A note on freshness.** This backlog was written after re-reading the repository, not from earlier notes. Items that prior reviews still listed as open were dropped because the code already fixes them: the widget builder wizard was replaced by `WidgetAuthoringPanel.tsx` with complete CSS and a wired `onEdit`; the KPI branch and temporal cross-filtering are corrected; the Workspaces navigation group is fixed and uses the shared collapsible `NavGroup`; the Analysis Toolbox parity panel now builds its comparison payload independently; `DashboardFilterBar` has an All parameters option; and `AssociativeContext` already carries the time range into the panel. Tasks below are what is **actually open**.

---

## PHASE SUMMARY

| Phase | Milestone | Title | Tasks | Hours |
|---|---|---|---:|---:|
| **M1-P1** | M1 | Presentation Truth and Dataset Foundation | 13 | 84 |
| **M1-P2** | M1 | No-Code Authoring Shell - wiring, SQL and widget authoring | 9 | 84 |
| **M1-P3** | M1 | BI Workspace and Six Showcase Pages | 10 | 82 |
| **M1-P4** | M1 | Journey J4 to J15, Engine Slice and Website Path | 12 | 80 |
| **M1-P5** | M1 | Assistant Dock and Presentation Certification | 15 | 80 |
| **M2a-P1** | M2a | Canonical Schema Authority and the Unified Definition Store | 10 | 100 |
| **M2a-P2** | M2a | Permanent Relationship Model and Projection Quarantine | 11 | 92 |
| **M2a-P3** | M2a | Job Runtime, Delta Propagation and Security Hardening | 9 | 88 |
| **M2a-P4** | M2a | Commissioning, Roles, Licence and the On-Site Package | 10 | 120 |
| **M2b-P1** | M2b | Intelligence Substrate and Practice Learning | 10 | 116 |
| **M2b-P2** | M2b | Prediction, Remediation, Engine Consolidation and Gates | 12 | 114 |
| **M3-P1** | M3 | Site Stabilisation and Real-Data Performance | 8 | 96 |
| **M3-P2** | M3 | Production Certification, Enterprise Operations and Commercial Completion | 9 | 108 |
| | | **TOTAL** | **138** | **1244** |

Priority mix: Critical 80, Very Important 51, Important 7

---


# M1 - CUSTOMER PRESENTATION (410 h)


## PHASE M1-P1 - Presentation Truth and Dataset Foundation

**13 tasks / 84 hours.** Establish what will be shown, prove the environment rebuilds from source control, and plant the analytical phenomena the whole demonstration feeds on. Blocks M1-P3 and M1-P5.

### T-001 - Build the six-beat Design Traceability Matrix

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Documentation / Design contract |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Create docs/m1/M1_Traceability_Matrix.md. One row per screen that will be opened in the customer presentation. Columns: Screen, Route, Chapter 2 journey step (J4..J15), Chapter 3 page contract id (A1..F9 / G1..G6), Chapter 4 behaviour section (5.1..5.7), Chapter 5 tutorial step, Current implementation file, Classification. Classification is one of KEEP (matches final design), MODIFY (visible surface needs change), TEMP-ADAPTER (visible surface is final but persistence behind it is temporary and M2 replaces it), NEW. Get the route list from Frontend/PlantProcess.Web/src/App.tsx, which currently declares 69 route paths. Chapter 3 section 4.4 lists the target 40 route pages plus 6 shell components; every presentation screen must map to one of them.

**Validation.** The matrix has zero rows where the Chapter 3 column is blank. Walk the list with the App.tsx route table open: every route you intend to open in the room appears, and no route outside the list is reachable from the demo navigation. Reviewed and signed off before any other M1 task starts.

### T-002 - Audit every presented route and control against final naming

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Frontend / Design contract |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** For each screen in the traceability matrix, compare the actual page title, button labels, control placement and terminology against Chapter 3 4.4 and Chapter 4 5.1/5.2. App.tsx still declares legacy phase-token routes (/phase8/assistant, /phase9/access, /phase9/executive, /phase15/benchmarking, /phase15/honesty-certification, /phase15/recommendations, /phase15/roi-cfo-dashboard, /phase15/scenario-simulation, /phase15/value-realization). None of these may be reachable from the navigation used in the demonstration. Do not delete them in M1; hide them from AppLayout navigation and record them on the retirement list for M2.

**Validation.** Open every navigation group in Frontend/PlantProcess.Web/src/components/AppLayout.tsx (NAV_DATA_INTEGRATION, NAV_ANALYTICS, NAV_INTELLIGENCE, NAV_ASSISTANT, NAV_SYSTEM plus the Workspaces group) and confirm no entry points to a route containing the strings phase8, phase9 or phase15. Add a vitest that asserts no nav entry `to` value matches /phase\d+/.

### T-003 - Lock the presentation profile as a data profile, not a branch

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Infrastructure / Environments |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** Chapter 6 forbids environment branches: the same artifact moves across environments. Confirm scripts/run/start-api.ps1 keeps its ValidateSet local/test/server/presentation and that the only difference between profiles is env/profiles/*.env. Fix the duplicated ConnectionStrings__PlantProcessDb declaration that appears twice (lines 18 and 19) in both local.env and presentation.env. Add a top-of-file comment to start-api.ps1 stating that the default is `local` and that the presentation must be launched with -Profile presentation.

**Validation.** Run `.\scripts\run\start-api.ps1 -Profile presentation` and hit GET /api/ml/foundation/readiness. It must report outcome_values around 195,221 and correlation_results around 320, which proves the API is on ppiq_presentation. Repeat with -Profile local and confirm the numbers differ. Record both outputs in the evidence folder.

### T-004 - Create ppiq_acceptance_empty and the one-query Rule 2 proof

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Schema |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Kept in M1 deliberately, and the reason must be written down because it does not pass Gate A on its own: the customer will not see this screen. It stays because it is the M1 RELEASE GATE - the proof that the artifact you are about to demonstrate is a product that starts empty, not a database someone filled by hand. If that justification is not accepted, move this task to M2a-P1 and drop M1 by four hours. Rule 2 says the plant schema starts empty and is provable in one query. Add a third database created by the same migration chain and nothing else. Write scripts/db/New-AcceptanceEmptyDb.ps1 that creates it, runs the EF migrations plus the post-EF SQL, and runs no seed. Then write the proof query: a single SELECT that returns zero for the sum of row counts across every plant-data table.

**Validation.** An integration test in Backend/tests/PlantProcess.Api.IntegrationTests that connects to ppiq_acceptance_empty, runs the proof query, and asserts the result is 0. The test must fail if a future seed script writes a plant row into a fresh database.

### T-005 - Create the M1 acceptance checklist and evidence folder

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Documentation / Quality |
| **Priority** | Important |
| **Hours** | 4 |

**Description.** Create docs/m1/ACCEPTANCE.md holding the UI/UX Golden Gate as a checklist applied per screen: Standard* components where one exists, no raw local styling, primary Electric Blue, selection Electric Cyan, secondary Corporate Blue, warning and refusal Amber, destructive Hot Red, muted Muted Steel, inline-start/inline-end never left/right, keyboard path, RTL mirror, all seven states (Empty, Loading, Populated, Filtered-empty, Blocked, Refused, Failed), widget failure isolation, registry-driven customer vocabulary, no number without evidence. Create docs/m1/evidence/ for screenshots and command output.

**Validation.** Every screen in the traceability matrix has a checklist instance in the folder. A screen is not Green until every line is ticked with an evidence file name beside it.

### T-006 - Rebuild ppiq_presentation into scratch and diff against live

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** scripts/demo/Rebuild-PresentationDb.ps1 rebuilds the demo database in one command, but it restores from deploy/.ppiq-snapshots/ppiq_app_20260713_203359.dump, a 13 July snapshot. Every correction made against the live presentation database between 14 and 27 July survives a rebuild only if it became one of the script's steps. Run the script with -TargetDb ppiq_presentation_scratch (the script's guard requires the name to contain 'presentation'), then produce a diff: object list (tables, views, functions, triggers, indexes) and row count per table, scratch versus live.

**Validation.** Produce docs/m1/evidence/presentation_db_diff.txt. Acceptance is either an empty diff, or a written list of every difference. Do not proceed to the next task until that list exists, and never run the rebuild against the live database before it does.

### T-007 - Convert every diff finding into a seed or migration script

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** For each row of the diff from the previous task, decide: is this a product fix (goes to Backend/database/scripts as a numbered migration) or presentation data (goes to scripts/demo as a seed step)? The governing law is that presentation DATA may be presentation-only but presentation FIXES may never be. Add each item in the correct place and re-run the rebuild.

**Validation.** Re-run Rebuild-PresentationDb.ps1 into a fresh scratch database and re-run the diff. It must now be empty. That output is the proof that no fix exists only as data.

### T-008 - Author the Presentation Phenomena Manifest

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Documentation / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Write docs/m1/PHENOMENA_MANIFEST.md listing 10 to 15 industrial behaviours the dataset must exhibit, each with the entities involved, the expected direction and strength, and the screen that will show it. The required set: casting speed by temperature by grade interaction; thickness changing the optimum speed; a real quality difference between two equipment units; a shift effect; a downtime pattern; a yield against throughput trade-off; defect probability rising above a speed band; a plausible correlation that disappears after conditioning; a good-practice operating band; a bad-practice band; one insufficient-support case the engine must refuse; one clean genealogy chain. Every phenomenon is planted in the DATA. None of them may be an answer written in code.

**Validation.** Peer review against the rule: for each entry, name the table and column where the effect will live. If any entry can only be satisfied by a code branch or a hardcoded string, it is rejected and rewritten.

### T-009 - Extend the emulated sources: speed, temperature and grade interaction

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** The emulated plant lives in the src_*_shape schemas and is loaded by Backend/database/scripts/110_phase1_demo_source_shapes.sql and the seed files under Backend/database/seed. Extend the generator so casting speed, tundish temperature and grade interact: within each grade, defect rate is lowest inside a speed band, and the band shifts with temperature. Do not write the answer anywhere. Generate rows so the relationship is discoverable by a correlation run.

**Validation.** Write a SQL verification script that groups the generated rows by grade and speed decile and prints mean defect rate. The band must be visible in the printed table without any smoothing. Save the output to the evidence folder.

### T-010 - Extend the emulated sources: equipment, shift, thickness and downtime

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Continue in the same generator. Add: a measurable quality difference between two equipment units on the same route step; a shift effect on at least one outcome; thickness shifting the optimum speed; and a downtime pattern where a short equipment stoppage sometimes does and sometimes does not become a production stoppage. That last one is the two-downtime-quantity rule from Chapter 1 1.6.2 and both quantities must be populated in downtime_events.

**Validation.** SQL verification script per effect, printing the group means and counts. For downtime, assert that the count of equipment stoppages exceeds the count of production stoppages, and that both columns are non-null on every row.

### T-011 - Extend the emulated sources: the confounded correlation and the refusal case

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Emulated sources |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Two phenomena carry more weight than the rest. First, plant a correlation that looks strong on a naive analysis and disappears once a visible confounder is conditioned on. Second, create one outcome that has genuine data but too few independent units or too few outcome events to pass the readiness gate, so the engine must refuse it. Gate thresholds are in ReadinessGate.cs: HeatsReady 60 / HeatsPartial 30, EventsReady 40 / EventsPartial 15, MinorityReady 0.10 / Partial 0.03, CompletenessReady 0.95 / Partial 0.85.

**Validation.** Run the analysis path on both. The first must produce a finding that survives naive analysis and is reported as not surviving stratification. The second must return Blocked with a reason string naming the measured value and its threshold. Both outputs saved to the evidence folder. Do not weaken any gate threshold to achieve either result.

### T-012 - Run the canonical import, projection and analysis path end to end

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Engine / Presentation data |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** With the regenerated sources in place, run the whole path rather than loading the target tables directly: connection test, dataset registration, incremental import into staging, canonical projection, genealogy, feature and outcome refresh, then an analysis run. Record row counts at every stage.

**Validation.** A single command log showing one row entering at the source and appearing in material_units and the canonical views, plus final populations for material_units, parameter_observations, quality_events, downtime_events, genealogy_edges, ml_outcome_values. Stage counts must be monotonic and explainable.

### T-013 - Data quality pass on the regenerated dataset

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P1 |
| **Module / Sub-module** | Database / Presentation data |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Check the generated data for anything an engineer in the room would catch: negative or zero values where the parameter registry declares a positive range; units inconsistent with the parameter definition; timestamps out of order or in the future; genealogy children whose contribution weights do not sum to 1.0; rows with no provenance; distributions that are obviously uniform where a real plant would be skewed.

**Validation.** A SQL QA script that returns zero rows for each of the above conditions. The genealogy check is already enforced by a database trigger on contribution_weight, so confirm the trigger fires by attempting an invalid insert in a transaction and rolling back.


## PHASE M1-P2 - No-Code Authoring Shell - wiring, SQL and widget authoring

**9 tasks / 84 hours.** Presentation beat 1 and half of beat 2. The one shell serving five purposes, in both modes, producing one governed definition. Visible contract is final; persistence behind it may be adapted.

### T-014 - Bring the authoring shell to the final four-region layout

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 4 5.2.3 specifies four regions: mode bar (Block | SQL) with Run and Validate at the top; schema tree on the inline-start side; board or SQL editor in the centre; toolbox on the inline-end side; debug log with rows and cost across the bottom. Frontend/PlantProcess.Web/src/pages/Prep/VisualJoinCanvasPage.tsx is 784 lines and already carries the board, a compiled-query pane, preview, publish and a debug log, but the regions are not yet the specified four. Restructure the page layout only. Do not change the graph model or the generator.

**Validation.** Screenshot at 1920x1080 and at 1280x800 against the Chapter 4 5.2.3 diagram. All four regions present, correct sides using inline-start and inline-end (never left/right), and the mode bar switching between Block and SQL without losing board state. Add a vitest asserting the page renders all four region containers.

### T-015 - Registry-driven schema, table and attribute tree

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** The canvas already receives a three-level schema tree with typed columns and isKeyCandidate markers from the backend catalogue endpoint, which reads the schema name from the configuration key Prep:StagingSchema (default dump_store). Add search across schema, table and column names; show the column type beside each column; show an approximate row count per table; keep key candidates rendered as green markers. Nothing in this tree may be a hardcoded table or column name.

**Validation.** Type a partial column name into the search box and confirm only matching tables expand. Confirm at least one key candidate renders green. Grep the file for any literal table name from the emulated plant; there must be none.

### T-016 - Wiring path: source to join, filter, derived and map, with validation

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** The board already supports datasets and joins, and filters and derived columns are edited in the Preparation Definition panel because the backend generator applies a flat Filters array as one WHERE clause. Keep that decision. Add drag-time validation so an illegal connection is refused at drag time with a named reason, per Chapter 4 5.2.7. The operator lists in the interface must remain byte-identical to the whitelist BuildSafeSelect enforces, so an illegal state is unreachable rather than rejected afterwards.

**Validation.** Attempt each illegal connection in the enumerated set from Chapter 4 5.2.7 and confirm each is refused with a sentence, not a silent no-op. Add a vitest that reads the operator list from the page and asserts it equals the server whitelist list.

### T-017 - Compiled-SQL pane and debug log with rows and cost

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / SQL Editor |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** The dry-run endpoint already returns the SQL it built, so the pane shows what actually runs rather than a client reconstruction. Keep that. Add the debug log contract from Chapter 4 5.2.8: entries typed Error, Warning or Success, each with a message written for a plant engineer, plus returned row count and an execution cost estimate. Never render a raw exception string.

**Validation.** Trigger three cases and confirm three distinct log entries: a valid preview (Success with row count), a preview returning zero rows (Warning with an explanation), and a rejected operator (Error naming the operator). Confirm no output contains a stack trace or the words 'could not load', 'failed to load' or 'unable to load', which the PPIQ-T09 architecture test forbids.

### T-018 - SQL mode: safe editor, run test, returned columns and sample values

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / SQL Editor |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 4 5.2.12 specifies the SQL face of the same shell. The backend contract already exists and is strong: SafeSqlValidator is 295 lines, allows SELECT and WITH only, validates on token boundaries so created_at stays legal, and forbids DDL, DML, COPY, large-object functions, dblink*, pg_sleep*, pg_catalog, information_schema and xp_*. Build the editor over it: syntax highlighting, schema and column autocomplete from the same catalogue the tree uses, a Run test button, the returned column list with inferred types, and a sample of returned values.

**Validation.** Run a valid SELECT and confirm columns and samples render. Then run each of these and confirm each is refused by name, not by a generic error: a DROP statement, a statement referencing pg_catalog, a statement calling pg_sleep, and a statement with a forbidden token hidden inside a comment (the comment stripper must catch it).

### T-019 - Returned-column role mapping

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** After a run test, the user assigns each returned column a role so the result can drive a widget or a downstream definition. The role binding module already exists at Frontend/PlantProcess.Web/src/api/product-core/widget-role-binding.ts with readRoleBinding, writeRoleBinding, staleRoles and describeStale. Wire the SQL-mode result grid to it so roles survive a re-run, and show the stale-role warning when a re-run removes a column that had a role.

**Validation.** Assign roles, re-run the query unchanged, confirm roles persist. Then edit the query to drop a mapped column, re-run, and confirm the stale-role warning names the missing column.

### T-020 - Add Widget and Edit Widget reopen the same shell

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 4 5.1.10 and 5.1.12 require one shell for both. Today Frontend/PlantProcess.Web/src/components/dashboard/widget-authoring/WidgetAuthoringPanel.tsx (549 lines) is lazy-imported at InteractiveWorkspacePage.tsx line 20 and rendered at line 203, and onEdit is passed at line 223 as `() => { setEditing(widget); setWizardOpen(true); }`. Verify that opening Edit loads the existing definition into every field rather than an empty form, and that the panel is reached by the same code path in both cases. Rename the local state `wizardOpen` to `authoringOpen`; the word wizard is a leftover from a deleted component and Chapter 4 does not use it.

**Validation.** Create a widget, save, reload the page, open Edit on the same widget, and confirm every field shows the saved value including the query, filters, chart type and role bindings. Change one field, save, reload and confirm the change persisted. Add a vitest asserting the panel receives a non-null existing definition when opened from Edit.

### T-021 - Final definition service interface with a compatibility adapter

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Backend / Definition store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 3 4.5.11 specifies one definition_store with definition_versions and definition_dependencies. That table set does not exist; a repository-wide search for definition_store returns zero hits. M1 does not build it. M1 builds the FINAL external contract in front of the current persistence, so M2 can replace the storage without the UI moving. Create IDefinitionService with Create, Update, GetCurrent, GetVersion, ListVersions and Publish, taking a definition kind (Transformation, Page, Widget, Analysis, LogRule). Implement it as an adapter over the existing per-artifact tables. Every write made by the authoring shell goes through this service.

**Validation.** An integration test that creates a widget definition through IDefinitionService, reads it back by version, updates it, and confirms two versions exist. The test must not reference any concrete table name, so that it still passes unchanged after M2 replaces the storage. That is the real acceptance criterion.

### T-022 - Authoring states, keyboard path, RTL and error wording

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P2 |
| **Module / Sub-module** | Frontend / UI/UX no-code frontend |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Apply the Golden Gate to both authoring modes. Implement all seven states. Add a full keyboard path: tab order through the four regions, Enter to run, Escape to close. Mirror the layout under RTL using inline-start and inline-end only. Replace any raw error string. Note that src/test/architecture/noRawErrorStrings.test.ts scans every .ts and .tsx under src and fails on the regexes /could ?n.?t load/i, /failed to load/i, /unable to load/i and /loading failed/i, allowlisting only DataFetchBoundary.tsx and ErrorBoundary.tsx.

**Validation.** Run `npm run test` (vitest) and the architecture suite; both must be green. Complete one full authoring scenario using only the keyboard. Switch the document direction to rtl and screenshot both modes.


## PHASE M1-P3 - BI Workspace and Six Showcase Pages

**10 tasks / 82 hours.** Presentation beat 2. Seven dashboards are already seeded; this phase is certification, differentiation and interaction quality, not construction.

### T-023 - Bring the workspace to the final D1 anatomy

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 4 5.1.2 specifies the page anatomy: header, global selections bar, widget grid, associative strip. InteractiveWorkspacePage.tsx currently renders a header with Save layout, Refresh widgets and Add widget, then DashboardFilterBar, DashboardGridLayout and the associative panel. Align the ordering, spacing and grouping to 5.1.2, and make the selections bar show active selections as removable chips.

**Validation.** Screenshot against the 5.1.2 diagram. Apply three selections and confirm three chips appear, each removable, and that removing one updates every widget on the page.

### T-024 - Widget row census across the seven seeded dashboards

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Testing / Dashboard |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** scripts/demo/Seed-PresentationDashboards.v2.ps1 seeds seven dashboards with roughly 29 widgets: PRODUCTION_OVERVIEW, QUALITY_MONITORING, EQUIPMENT_OPERATIONS, CORRELATION_FINDINGS_BOARD, PARAMETER_DEEP_ANALYSIS, RISK_INTELLIGENCE and MODEL_INSIGHTS. Write a script that calls the widget query endpoint for every seeded widget and records the returned row count, the chart type and any warning. Historically about half returned zero rows.

**Validation.** Produce docs/m1/evidence/widget_row_census.csv with one row per widget: dashboard, widget code, chart type, rows returned, error. Every widget on the six chosen pages must end this phase with rows greater than zero.

### T-025 - Fix every empty or wrong-shaped widget on the six chosen pages

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Working from the census, fix each failing widget. The order of preference is: correct the widget definition (dimension, measure, parameter code, filter) in the seed script; then correct the chart type; only then change code. Known historical cases to check: a widget grouped by the day dimension rendering as a donut with sixty slices, or a heatmap of dates; a widget using a measure that requires a parameterCode with the code left null; a widget bound to a dimension the server does not list as supported, which returns 400.

**Validation.** Re-run the census. Every widget on the six pages returns rows and renders the chart type its title implies. Manually confirm the rule 'retitle when you repoint': no widget title describes something other than what it plots.

### T-026 - Give the six pages six distinct visual grammars

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 10 |

**Description.** Six pages that are all bar charts read as one page shown six times. Assign each of the six a dominant visual language and adjust widget definitions to match: KPI tiles with trend; Pareto; stacked or combination; scatter; heatmap; distribution or box plot; table with conditional formatting; timeline or genealogy where appropriate. The chart catalogue is registry-driven and Chapter 4 5.1.5 lists the supported set.

**Validation.** Place the six pages side by side as screenshots. A reviewer who has not seen the product must be able to say what each page is for from the shapes alone, without reading the titles.

### T-027 - Associative states including the excluded pivot

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** The associative engine exists: Frontend/PlantProcess.Web/src/state/AssociativeContext.tsx, associativeFields.ts and components/dashboard/AssociativePanel.tsx. The context already carries every active workspace filter minus the field's own selection, so a time-range selection narrows the panel as well as the widgets. Verify the four states render distinctly: selected, possible, excluded, alternative. Implement the excluded pivot, which is selecting an excluded value and having the selection set invert accordingly.

**Validation.** Select a site, then select a value that the site excludes, and confirm the panel and every widget update consistently. Confirm a dimension with zero rows renders as not applicable rather than as available-but-empty. Capture the four state colours against the token set: selection Electric Cyan, excluded Muted Steel.

### T-028 - Chart compatibility switcher with reasons

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Important |
| **Hours** | 6 |

**Description.** A widget may only offer chart types that are compatible with its current dimension and measure shape. Offer the compatible set and, when a type is unavailable, say why in one sentence rather than hiding it silently. The supported chart set is closed product grammar and lives in DashboardWidgetQuerySafetyRegistry (Kpi, Bar, Line, Area, Pie, Donut, Scatter, Heatmap, Pareto, Table); do not extend it here.

**Validation.** On a widget with a temporal dimension, confirm Pie is either absent or shown with a reason. Switch a KPI to Bar and back to KPI and confirm the round trip works, since the switcher previously offered no path back to KPI.

### T-029 - Layout drag, resize, save, reload and responsive behaviour

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** useDashboardGridLayout and useDashboardLayoutPersistence already work. Harden the interaction: drag and resize must feel deliberate, the Save layout control must confirm success with a toast, and a reload must restore exactly what was saved. Check behaviour at 1920x1080, 1440x900 and 1280x800.

**Validation.** Move and resize three widgets, save, hard-reload the browser, and confirm the layout is identical. Repeat at each of the three widths. Record a short screen capture into the evidence folder.

### T-030 - Drill and evidence drawer

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Frontend/PlantProcess.Web/src/components/dashboard/DrilldownDrawer.tsx renders a drawer with a header and body but has no open transition and uses off-palette blues. Add a slide transition that respects prefers-reduced-motion, correct the colours to the token set, and make the drawer show the population behind the clicked point plus a path to its provenance.

**Validation.** Click a bar and confirm the drawer opens with a transition and lists the underlying rows. Set prefers-reduced-motion and confirm the transition is suppressed. Confirm every colour used appears in the token set.

### T-031 - Widget failure isolation and the seven states

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Chapter 4 requires that one widget failing never destroys the page, and that filtered-empty is distinguishable from genuinely empty. Wrap each widget in its own boundary and implement all seven states with the correct colour semantics: Blocked and Refused in Amber, Failed in Hot Red, Empty and Filtered-empty in Muted Steel with different wording.

**Validation.** Inject a failure into one widget query and confirm the other widgets on the page continue to render and interact. Apply a filter that returns no rows and confirm the widget says the selection returned nothing, not that there is no data.

### T-032 - Remove the hardcoded parameter default from the API client

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P3 |
| **Module / Sub-module** | Frontend / Dashboard |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** Frontend/PlantProcess.Web/src/api/productCoreApiClient.runtime.ts line 300 reads `parameterCode: filters.parameterCode || "CastingSpeed"`. That is a steel-specific literal in product logic and a Rule 1 violation reachable by a customer. Remove the fallback. When no parameter is selected, either omit the field or resolve a default from the parameter registry returned by the metadata endpoint.

**Validation.** Grep the whole src tree for CastingSpeed and confirm the only remaining hits are in demo content or test fixtures, never in product code paths. Load a parameter widget with no parameter selected and confirm it either shows a chooser or a registry-resolved default, and does not silently query a steel parameter.


## PHASE M1-P4 - Journey J4 to J15, Engine Slice and Website Path

**12 tasks / 80 hours.** Presentation beats 3, 4 and 6. Every visible transition credible with no dead end. J1 to J3 are commissioning per Chapter 5 and are narrated, not demonstrated.

### T-033 - J4 Connections: read-only proof and load budget made visible

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Connections |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** The connection page must show, for a configured source, that the connection is read-only enforced, what the load budget is, and the result of a live test. The backing facts already exist: connection_profiles carries read_only_enforced, and ThrottlingDataSourceReader evaluates every read against ISourceLoadBudgetProvider and ISourceQueryRateLimiter before it reaches the source. Surface those three facts on the page.

**Validation.** Open the page with one configured source, press Test, and confirm three visible outcomes: connection succeeded, read-only enforced true, and the current load budget. Then stop the emulated source container and confirm the failure state names the reason rather than showing a raw exception.

### T-034 - J5 and J6 Dataset registry browse and watermark suggestion

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Data integration |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** The Prepare Source for Import page already discovers tables from a live source. Improve schema, table and column search, and make the business key and watermark column suggestions explicit with the reason for each suggestion. Note the demonstration warning: staged tables carry emulator plumbing names such as src_caster_oracle_shape_cast_pieces. Decide deliberately whether to show them with an honest sentence or to present display names; do not discover this live in front of the customer.

**Validation.** Register one dataset end to end and confirm the suggested business key and watermark are shown with reasons and can be overridden. Record which naming decision was taken in the traceability matrix.

### T-035 - J6 Import progress visibility

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Data integration |
| **Priority** | Important |
| **Hours** | 4 |

**Description.** An import that runs with no visible progress reads as a hang. Add a named progress indication driven by the existing import batch records: batch started, rows staged, batch completed, with the dataset name. Use the activity tray pattern rather than a modal.

**Validation.** Start an incremental import of at least 100k rows and confirm progress updates at least every few seconds and ends in a completed state with a row count that matches the staging table delta.

### T-036 - J7 Relationship model vertical slice, part 1: publish one relationship

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 3 4.5.10 specifies plant_relationships plus members and paths, with sixteen declared consumers. A repository search for plant_relationships returns zero hits, so this does not exist. M1 does not build the whole model. M1 builds the smallest FINAL slice: the three tables with their real columns, and the ability to declare and publish one relationship between two source entities with its members and cardinality. Use the final table and column names from 4.5.10 so M2 extends rather than migrates.

**Validation.** An integration test that publishes one relationship, reads it back, and asserts it is versioned and marked published. A second test asserting an unpublished relationship is not returned to consumers.

### T-037 - J7 Relationship model vertical slice, part 2: one resolver consumer

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 10 |

**Description.** Chapter 3 says page and widget associative queries resolve through the published relationship model. Implement the path resolver for the single published relationship and make exactly one widget query use it for a cross-source join, instead of a join written into the widget's own query. This matters because the cross-source correlation is the categorical value proposition and a demonstration that joins inside one dashboard proves the opposite of the product design.

**Validation.** Point one widget at data spanning two sources, confirm it renders, then unpublish the relationship and confirm the widget refuses with a named reason instead of silently returning a partial result. Restore and confirm it renders again.

### T-038 - J8 Mapping Health: typed issues and the Reprocess control

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Mapping health |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Frontend/PlantProcess.Web/src/pages/MappingHealth/MappingHealthPage.tsx exists and Backend/PlantProcess.Api/Endpoints/MappingHealth/Phase34MappingHealthEndpoints.cs backs it. Chapter 3 4.4 C2 requires issues grouped by typed code with example rows and a Reprocess control; the API exists but the control does not. Add the control and group the issues by code. The full set of fifteen PV validation classes is M2; M1 needs the SHAPE to be final so the customer sees the same page after M2.

**Validation.** Introduce a deliberately malformed mapping, run the projection, and confirm the page lists the issue under a named code with an example row and offers Reprocess. Press Reprocess after correcting the mapping and confirm the issue clears.

### T-039 - J9 Genealogy search landing and thread

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Genealogy |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** The /materials route exists in App.tsx (line 556) along with /materials/:materialUnitId. Chapter 3 4.4 C5 specifies a two-state landing: a search state and a selected-unit state. Make the landing search work on the customer's own material identifiers and open a selected unit into the bidirectional genealogy thread with attribution weights and evidence.

**Validation.** Search a known material code, open it, walk backward to parents and forward to children, and confirm the attribution weights on each child sum to exactly 1.0. That sum is enforced by a database trigger, so a failure here means a data problem, not a UI problem.

### T-040 - Add job_definitions.target_definition_id and the JB error codes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Backend / Jobs |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** A repository search for target_definition_id returns zero hits. Chapter 3 4.5.5a specifies that a job must declare which definition it runs and under what version policy (pinned version, or current published). Without it a job cannot say what it executes, which blocks journey step J12 and tutorial T7. Add the column with a foreign key, add the version policy field, and add the JB error domain codes for the failure cases named in 4.5.5a.

**Validation.** Create a job targeting a specific analysis definition version, run it, and confirm the run history records the version actually used. Then delete the target definition and confirm the job fails with a JB code and a readable sentence rather than a null reference.

### T-041 - J12 Analysis definition: target, version, run and honest outcome

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** AnalysisJobConfigPage.tsx and AnalysisJobDefinitionEndpoints.cs already exist with definition-options, list, get, create, update, run and results routes. Wire the new target and version selector into the page, run the definition, and render the three honest outcomes distinctly: Completed, Blocked (the readiness gate refused, with the measured value and threshold) and Failed (something broke).

**Validation.** Run one definition that completes and one that the gate blocks. Confirm the blocked case shows a sentence of the form 'Blocked because outcome events = 12; Ready requires 40 or more' and is coloured Amber, not Hot Red. Confirm a genuinely failed run is Hot Red. Do not weaken any threshold to produce a completion.

### T-042 - One visible readiness authority on Home and Analysis

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** ReadinessGate.cs is complete and correct: five dimensions, thresholds as a record, overall equals the worst dimension via Math.Max over the enum, and every dimension returns a reason string built from the measured value and its threshold. Today that authority is not visible in one place. Build one readiness panel showing the five dimensions, each with its measured value beside its threshold and its state, and place it on both Home and the Analysis surface reading from the same endpoint.

**Validation.** Compare the panel against a direct call to GET /api/ml/foundation/readiness. Every number on screen must match the API response exactly. Change one threshold in configuration and confirm the panel moves, proving it is not a static rendering.

### T-043 - Findings evidence panel

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** A finding must open into its evidence: the method used, the population it was computed over, the effect size, the q-value after Benjamini-Hochberg correction, whether it survived stratification, and a path to the source rows. StatisticalDiscipline.cs already produces the ranking by absolute effect size with the p-value only as a tie-breaker, and BenjaminiHochberg.Adjust at q = 0.05. Render what the engine already computes.

**Validation.** Open the strongest finding and confirm all six elements are present. Confirm the ordering on the findings list is by effect size and not by p-value, by comparing against the API response.

### T-044 - Retire the hardcoded outcome and grain arrays

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P4 |
| **Module / Sub-module** | Frontend / Engine |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Frontend/PlantProcess.Web/src/pages/Analysis/AnalysisToolboxPage.tsx line 18 declares OUTCOMES as a literal array of four steel-specific keys, and line 19 declares GRAINS as ["coil", "slab", "heat"]. A server registry already exists: table public.ml_outcome_definitions is exposed at GET /ml/foundation/outcomes by MlFoundationEndpoints.cs and carries the grain per outcome. AnalysisJobConfigPage already consumes it. Replace both arrays with the registry call and take the grain from the selected outcome's registry row rather than defaulting.

**Validation.** Confirm the dropdown contents match the API response exactly. Add a registry row through the API and confirm it appears in the UI with no code change, which is the Rule 1 acceptance test. Also confirm no server code path falls back to the grain literal 'coil' when the outcome declares a different grain.


## PHASE M1-P5 - Assistant Dock and Presentation Certification

**15 tasks / 80 hours.** Presentation beat 5 plus the certification gate. Deliberately smaller than the other phases because it runs in the last week and must absorb slippage from M1-P1 and M1-P3.

### T-045 - Website presentation path polish

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Website / Public site |
| **Priority** | Important |
| **Hours** | 8 |

**Description.** Do not rewrite the website. Polish only the routes you will actually open: home, the PPIQ narrative, proof, security and the call to action. Preserve the components the Chapter 6 audit marked keep: HeroTopology, GoldenThread, TrustEngine, SignalVsNoise, useScrollDraw, RoiCalculator, RequestDemoForm, ConnectorHonestyBlock and PositioningTruthBlock. Do not open /products/:code during the presentation; it redirects into PPIQ pack pages and encodes the wrong product architecture, which is an M2 or M3 fix.

**Validation.** Click every link on every route you will open and confirm none is dead. Check desktop, mobile and keyboard navigation on those routes only. Confirm no page shows a blocker, an unfinished item or a failed test, per the standing website honesty rule.

### T-046 - Build the G1 persistent assistant dock

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Frontend / Chatbot |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Chapter 4 5.7.1 specifies a persistent dock present on every authenticated page, not a route. Today Frontend/PlantProcess.Web/src/components/assistant/AssistantChat.tsx is rendered by exactly one page, Phase8/AssistantRuntimePage.tsx. Move the chat into a dock shell mounted in AppLayout so it is available on every authenticated presentation surface, with a collapsed and expanded state. This is a visible-contract item: shipping a separate page in M1 and a dock after M2 would fail the Customer Contract Continuity Test.

**Validation.** Open the dock on at least five different pages and confirm the conversation persists across navigation. Confirm the collapsed state does not obscure any control. Add a vitest asserting the dock renders inside the authenticated layout and not as a route element.

### T-047 - Page and widget context envelope

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Backend / Chatbot |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** The assistant must know what the user is looking at. Extend the ask request so the client sends a context envelope: current route, page code, focused widget code, active selections and filters, and the widget's own last result summary with its evidence handles. The endpoint is POST /api/assistant/ask in Backend/PlantProcess.Api/Endpoints/Assistant/AssistantEndpoints.cs, which already accepts ContextChips. Use the context to narrow retrieval rather than to answer.

**Validation.** Ask the same question on two different pages and confirm the retrieved evidence differs. Assert in an integration test that the context reaches the retrieval call and that no context field is echoed into the answer text unverified.

### T-048 - Add the page and widget chunk family to the retrieval corpus

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | AI+ML / Chatbot |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** This is the single highest-value change in the sprint. Backend/PlantProcess.Infrastructure/Assistant/CanonicalChunkProducer.cs currently builds only five chunk families: CONNECTOR, DATASET and MAPPING from configuration, DOC for the honesty contract, and FINDING for the latest correlation results. Nothing describes what is on the page. Add a family that emits one true sentence per widget result, for example: 'On page Quality Monitoring, widget Defect rate by equipment shows EAF 3.4 per square metre and Caster 1.9 per square metre for June 2026, over 1,284 coils.' Every number in that sentence must come from a real query result and carry an evidence handle. Then rebuild the index through the existing POST /api/assistant/reindex endpoint, which is already wired to this producer through NpgsqlRetrievalIndex. This matters because GroundingService blocks any sentence containing a number not present in retrieved evidence, so without this family the assistant refuses every question about a chart.

**Validation.** Reindex, then ask 'what does this chart show' on three different pages and confirm each answer contains numbers that match the widget on screen and carries at least one citation. Then delete the new chunks, reindex, and confirm the assistant returns the honest refusal rather than inventing an answer. Both behaviours must be demonstrable.

### T-049 - Registry-typed quantity guard on assistant answers

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Backend / Chatbot |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** GroundingService.Enforce already blocks any sentence with a number absent from the retrieved claims and blocks the phrases 'root cause', 'is caused by', 'will cause', 'guaranteed' and 'will save'. Add a typed layer above it: when the question names a quantity that exists in the parameter registry, validate the answer's unit, sign and range against the registry row for that parameter. Reject a date where a speed was asked for, a mass where a speed was asked for, and a negative value where the registry declares a positive range.

**Validation.** Unit tests feeding a crafted draft for each rejection case and asserting the sentence is blocked. Then a live check: ask for a casting speed and confirm the answer either gives a speed with the registry's unit, gives an evidence band, or refuses. It must never return a date or a mass.

### T-050 - Citation chips, evidence strip and suggested questions

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Frontend / Chatbot |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Render each citation as a chip that expands to its evidence, add an evidence strip under the answer, and add an Open in page action that navigates to the surface the evidence came from. Add three to five suggested starter questions derived from the current page context so a live demonstration has a safe opening move.

**Validation.** Click a citation chip and confirm it expands to the underlying evidence. Click Open in page and confirm it navigates to the correct surface with the relevant selection applied. Confirm suggested questions change between two different pages.

### T-051 - Wire a real model behind the assistant provider seam

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | AI+ML / Chatbot |
| **Priority** | Important |
| **Hours** | 3 |

**Description.** AssistantInfrastructureExtensions.AddAssistant registers Top15RealAssistantModel when Top15ModelEndpointConfig.FromEnvironment().IsConfigured and otherwise falls back to ExtractiveAssistantModel. The configuration reads five environment variables, of which PPIQ_ASSISTANT_MODEL_ENDPOINT alone also enables the path. Top15HttpAssistantModelClient POSTs {Question, ProviderKey, ModelKey, ModelVersion, Evidence[{Handle, Text, SourceKind, SourceRef}]} and reads `answer` or `text` from the response. No commercial provider accepts that shape, so write a small local service that translates the request to the chosen provider and returns {"answer": "..."}. Only retrieved evidence is sent, never the database, and the output still passes GroundingService.

**Validation.** With the endpoint unset, confirm the extractive model answers and the demonstration still works. With it set, confirm answers improve and every number is still cited. Cut this task first if the phase runs short; the demonstration must not depend on network access in the room.

### T-052 - Certified question pack and offline fallback

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Documentation / Chatbot |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Prepare 10 to 15 questions whose evidence is known to exist, covering at least three pages, and record for each the expected answer shape: a value with a unit, an evidence band with a record count, a conditional answer naming what would narrow it, or an honest refusal. These are not scripted answers; they are a known-good evidence landscape. Select two or three for live use. Prepare the offline path so a network failure downgrades to the extractive model rather than to an error.

**Validation.** Run all 10 to 15 twice, once online and once with the model endpoint unreachable, and record both answers. Any question that refuses in both runs is removed from the live set.

### T-053 - One Playwright journey covering all six beats

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / E2E |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Write a single spec that walks the whole demonstration: connect, register a dataset, import, map and publish the relationship, open genealogy, create a page, add a widget through the shell, edit its query, save, open the six dashboards, cross-filter, run an analysis and see the readiness outcome, ask the assistant a question on a page, open the evidence, and open the website routes. Note that the existing E2E stage in the Jenkinsfile already runs the full Playwright suite through deploy/scripts/ci-e2e-stack.sh, so this spec joins a gate that actually executes.

**Validation.** The spec passes twice consecutively from a clean database. A deliberate break in any beat must fail the spec at that beat with a readable assertion message.

### T-054 - Execute visual regression and accessibility on the presented routes

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / Visual and a11y |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** package.json already defines test:visual and test:a11y as genuine Playwright invocations, but nothing in the pipeline calls them, and tools/ci/validate-real-ui-gates.cjs invokes them with --list, so it verifies the gates exist rather than that they pass. Also package.json line 84 defines phase9:matrix with --list. Point the visual and accessibility specs at the presentation routes and run them for real. Remove the --list flags from validate-real-ui-gates.cjs.

**Validation.** Both suites run and pass on every presented route. Confirm by deliberately introducing a contrast failure and checking the accessibility suite goes red, then reverting. The first run on a machine writes visual baselines and reports them as failures; that is expected and the second run is the real one.

### T-055 - Failure injection suite

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / E2E |
| **Priority** | Very Important |
| **Hours** | 3 |

**Description.** Rehearse the failures that are most likely to happen live: one widget query fails while the page keeps working; the assistant refuses because evidence is missing; the API is restarted mid-demonstration; a filter selection returns no rows; the model endpoint is unreachable. Each must produce a designed state, not a stack trace.

**Validation.** Five scripted injections, each with a screenshot of the resulting state in the evidence folder, each showing a sentence rather than a raw error. A red outline with no sentence beside it is a failure of the specification, not an acceptable outcome.

### T-056 - Capture the Customer Contract Continuity snapshots

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Testing / Continuity |
| **Priority** | Critical |
| **Hours** | 4 |

**Description.** On the last day of M1, capture the visible contract: a screenshot of every presented page, the navigation tree, control positions, the Add Widget and Edit Widget flow, the wiring and SQL modes, the assistant dock, the engine surfaces, any logging surface shown, and the website routes. Store them under docs/m1/continuity/ with a manifest naming each file and the route it came from.

**Validation.** The manifest covers every row of the traceability matrix. This set becomes the regression truth for M2: after M2 the comparison must show no change to navigation, control placement, authoring flow, terminology or refusal semantics. Additions and speed improvements are allowed; replacements are not.

### T-057 - Write the screen-by-screen demonstration script

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Documentation / Rehearsal |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** This was missing from backlog v1 and it is the single most valuable artifact in the sprint. Write docs/m1/DEMO_SCRIPT.md as a numbered list of screens in the order they will be opened, and for each screen the one or two sentences that will be said while it is on screen, the exact clicks in order, and the expected on-screen result. Mark each of the six beats and its boundary. Include the deliberate cuts as written decisions so they are decisions rather than discoveries in the room: J1 to J3 narrated as commissioning; no login screen; no euro value figure; no live licence tier toggle unless the 30-minute check on LicenseUsagePanel proves it works; /products/:code never opened. Add the two standing warnings: launch with -Profile presentation, and the staged tables carry emulator names such as src_caster_oracle_shape_cast_pieces.

**Validation.** Read the script aloud against a clock without touching the product; it must fit the meeting slot. Then walk the product with the script in hand and confirm every click named exists and produces the stated result. Any mismatch is a defect in the product or the script, and both are fixed before the first rehearsal.

### T-058 - Presentation environment preparation and clean-start verification

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Infrastructure / Rehearsal |
| **Priority** | Very Important |
| **Hours** | 4 |

**Description.** Prepare the machine that will run the demonstration and prove it starts clean, twice. Fixed browser profile with notifications, extensions and update prompts disabled; fixed window size and zoom matching the resolution rehearsed at; emulated source containers started and healthy; API launched with -Profile presentation and verified against GET /api/ml/foundation/readiness; the web app served from a production build rather than a dev server; ports free; screen sleep and screensaver disabled. Note that scripts/run/start-api.ps1 defaults to -Profile local, which resolves to ppiq_app and reproduces an empty Findings page in front of the customer.

**Validation.** Cold-boot the machine and reach the first demonstration screen following only a written checklist, twice, timing both. Record both timings. If the second run needs a step that is not on the checklist, the checklist is wrong.

### T-059 - Three rehearsals, hostile hands and the fallback package

| | |
|---|---|
| **Milestone / Phase** | M1 / M1-P5 |
| **Module / Sub-module** | Documentation / Rehearsal |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Three full timed rehearsals from a clean laptop boot, of which one is run by someone else holding the mouse and trying to break it. Then assemble the fallback package: a database backup, a short screen recording of each beat, and still images of the key screens, so a hardware or network failure does not end the meeting. Time the journey narration against a clock, since fifteen steps at two to three minutes each is thirty-seven minutes of continuous talking.

**Validation.** Two consecutive rehearsals complete with no surprise. The hostile-hands run produces a defect list that is either fixed or explicitly accepted in writing. The fallback package exists on a second device.


# M2a - DEPLOYABLE CORE, ENDS WITH THE ON-SITE INSTALLATION (400 h)


## PHASE M2a-P1 - Canonical Schema Authority and the Unified Definition Store

**10 tasks / 100 hours.** Replace the M1 compatibility adapter with the real definition store and move the schema to its final three-schema topology, without the customer-visible contract moving.

### T-060 - Physical three-schema migration

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Schema |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** The measured database carries twelve schemas with 162 tables in public, of which 108 use a ppiq_ name prefix, while the ruled schemas ppiq_plant and ppiq_meta exist and hold zero tables. Migrate to the three ruled schemas: ppiq_staging, ppiq_plant, ppiq_meta. The staging rename is already prepared, because the canvas reads its schema from the configuration key Prep:StagingSchema rather than a literal.

**Validation.** Rule 2 is provable in one query on a fresh database. All application tests pass. The canvas catalogue lists tables from the renamed schema with only a configuration change.

### T-061 - Canonical migration order and legacy script archival

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Schema |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Backend/database/scripts contains hotfix, repair, phase and drift-correction scripts accumulated over months, including cases where two scripts created the same table. Define one canonical ordered migration path, archive superseded scripts, and add a truth gate that no two scripts create the same table.

**Validation.** A fresh database builds from the canonical path with no manual step. The truth gate fails if a duplicate CREATE TABLE is introduced.

### T-062 - definition_store, definition_versions and definition_dependencies

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Database / Definition store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Create the tables specified in Chapter 3 4.5.11 with immutable versions and a dependency graph, plus a trigger that rejects a dependency cycle.

**Validation.** Integration tests: create, version, publish, and reject a cycle. Version rows must be immutable, proved by an update attempt that fails.

### T-063 - Move all five definition kinds onto the store

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Definition store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Replace the M1 compatibility adapter behind IDefinitionService with the real implementation for all five purposes: S1 Transformation, S2 Pages and widgets and filters and master items, S3 Analysis, S4 Model, S5 Log rule. The old per-artifact tables become a compatibility projection and then are retired.

**Validation.** The M1 integration test written against IDefinitionService must pass unchanged, since it references no table name. That is the proof that the visible contract did not move.

### T-064 - Impact preview, export and import

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Definition store |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Add dependency impact preview before a publish, and export and import of a definition with its dependencies, per Chapter 3 4.5.11.

**Validation.** Publish a change to a definition three others depend on and confirm the preview lists all three before the publish is confirmed. Export and reimport into an empty database and confirm equality.

### T-065 - Registry authority: dimensions and measures as rows

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** DashboardWidgetQuerySafetyRegistry declares SupportedDimensions as a compiled HashSet that includes ProductFamily, GradeOrRecipe, ShiftCode, DefectType and RiskClass, referenced through DashboardMetadataCodes.Dimensions. That is plant vocabulary compiled into the product and a Rule 1 violation reachable by a customer. Move dimensions and measures to registry rows. Chart types and the numeric limits stay closed, because they are product grammar rather than customer knowledge.

**Validation.** Add a dimension through the registry API and confirm it becomes selectable in the authoring shell with no code change and no redeploy. Confirm chart types remain closed by attempting to add one and being refused.

### T-066 - Plant-vocabulary sweep, part 1: build the term list and the architecture test

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Rule 1 needs an enforcement mechanism before the sweep, or the sweep is a one-off. Create a registry-held list of plant terms (the term list is itself DATA, never a compiled constant) and an architecture test that fails the build when any listed term appears in product code outside registry data, seed content or test fixtures. Seed the list from the dimension names already found compiled into DashboardWidgetQuerySafetyRegistry: ProductFamily, GradeOrRecipe, ShiftCode, DefectType, RiskClass.

**Validation.** Add a term to the list and confirm the build goes red on an existing violation, then goes green once that violation is fixed. Confirm removing a term from the list does not require a code change.

### T-067 - Plant-vocabulary sweep, part 2: clear the violations and rename the canonical grain

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Registry |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Run the new architecture test and clear every violation it names across backend and frontend. Include the canonical grain literal 'coil', which the canonical layer applies even to aluminium, tyre and batch product types - native grains observed in the data include slab, heat, cast, packagedlot, rawmaterial, aluminiumroll, tyreunit, batch and lot. Rename it to a generic term and migrate existing rows.

**Validation.** The architecture test passes with zero violations. Query the canonical layer for a non-steel product type and confirm its grain is no longer reported as 'coil'.

### T-068 - API namespace migration, part 1: map the 92 prefixes onto the 27 domains and stand up dual-serve

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Namespace |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Chapter 3 4.3 specifies 27 clean /api/{domain} domains. The repository registers 92 MapGroup prefixes and 544 verb-level routes, with 18 groups under /api/v5, 6 under /api/p15, plus /phase2, /phase4, /phase5, /api/phase8, /api/p09 and /admin/p03p04. Produce the mapping table, then serve both old and new paths during a transition window.

**Validation.** Every one of the 544 routes appears exactly once in the mapping table. Both the old and the new path return identical responses for a sample of twenty routes.

### T-069 - API namespace migration, part 2: migrate the clients and add the token gate

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P1 |
| **Module / Sub-module** | Backend / Namespace |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Move the ~177 frontend API client methods across 13 modules and the 72 methods on productCoreApiClient.runtime.ts onto the new paths. Then add a gate that fails the build when a registered route or a client base path contains a phase, version or task token. Schedule the removal of the dual-serve window as a named follow-up task rather than leaving it open indefinitely.

**Validation.** A test asserting no registered route matches /phase\d+|\/v\d+\/|p\d\d/. Open the browser network tab during the golden journey and confirm no request URL carries a phase or version token.


## PHASE M2a-P2 - Permanent Relationship Model and Projection Quarantine

**11 tasks / 92 hours.** Turn the M1 single-relationship slice into the permanent product mechanism, and make customer data failures visible, typed and recoverable.

### T-070 - Relationship members, cardinality, grain conversion and preferred paths

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Extend the M1 slice to the full model in Chapter 3 4.5.10: members, cardinality, grain conversion between related entities, preferred path selection when more than one path exists, and published versions.

**Validation.** Declare two paths between the same pair of entities, mark one preferred, and confirm the resolver chooses it. Change the preference and confirm the resolution changes with no other edit.

### T-071 - Path resolver, part 1: resolver core and the first eight consumers

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Chapter 2 3.15.4 names sixteen consumers of the relationship model. Build the resolver core, then route the first eight through it: canonical projection, page and widget queries, associative filtering, drill-down, drill-through, genealogy, statistics and correlation.

**Validation.** A regression test per consumer asserting it resolves through the published model, and refuses with a named reason when the relationship is unpublished. Eight tests, no exceptions.

### T-072 - Path resolver, part 2: the remaining eight consumers and the regression suite

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Relationship model |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Route the remaining eight through the resolver: feature engineering, model training, prediction, practice learning, remediation search, value calculation, assistant retrieval and evidence. Some of these are built in M2b; add the resolver seam now so they cannot be written against an ad-hoc join later.

**Validation.** Eight further regression tests. For consumers whose engine arrives in M2b, the test asserts the seam exists and refuses when unpublished, which is what stops a later shortcut.

### T-073 - Relationship Browser page and path evidence

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Frontend / Relationship model |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Build the C6 Relationship Browser from Chapter 3 4.4 with the ten-field page contract, showing declared relationships, their members, their paths and the evidence for each path.

**Validation.** Open a relationship and confirm its members, cardinality and path are shown with evidence. Confirm an unpublished relationship is visibly distinct from a published one.

### T-074 - Quarantine, part 1: the table, the reprocess API and the first eight PV classes

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Quarantine |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Chapter 3 4.5.14 specifies quarantine with fifteen typed PV codes. A repository search for projection_quarantine returns zero hits, so this does not exist. Build the table with its columns, the reprocess endpoint, and the first eight validation classes so a bad row is quarantined under a named code instead of corrupting the canonical layer or failing the whole batch.

**Validation.** Craft one malformed input per implemented class and assert each is quarantined under the correct code, while the good rows in the same batch still project.

### T-075 - Quarantine, part 2: the remaining seven PV classes and per-class tests

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Quarantine |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Implement the remaining seven validation classes and give every class its own test fixture, so the class set is provably complete against Chapter 3 4.5.14 rather than approximately complete.

**Validation.** Fifteen fixtures, fifteen codes, fifteen passing tests. A test that enumerates the PV enum and fails if any member has no fixture.

### T-076 - Quarantine retry, reprocess and Mapping Health completion

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Frontend / Quarantine |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Complete the C2 Mapping Health page against Chapter 3 4.4 C2: issues grouped by code, example rows, and Reprocess after the mapping is corrected. The M1 version delivered the shape; this delivers the full class set and the retry semantics.

**Validation.** Quarantine rows under three different codes, correct one mapping, reprocess, and confirm only the affected rows clear. The M1 continuity snapshot of this page must still match.

### T-077 - Identity resolution across sources

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Genealogy |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Harden material identity resolution using material_aliases so the same physical material arriving under two different source identifiers becomes one canonical unit. The schema already supports this: material_units carries a unique key on (site_id, material_code) plus a filtered unique on (source_system, source_record_id), which makes projection idempotent without forbidding rows that have no source identity.

**Validation.** Import the same material under two different source identifiers and confirm one canonical unit results with both aliases recorded. Re-run the import and confirm no duplicate appears.

### T-078 - Genealogy bidirectional walk hardening and weight proof

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Genealogy |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Confirm the genealogy layer walks both directions on the customer's own keys and that attribution weights are enforced to sum to exactly 1.0 per child by the database trigger on genealogy_edges.contribution_weight numeric(9,6).

**Validation.** Walk a chain backward and forward and confirm the same edges are traversed. Attempt an insert whose weights sum to 0.99 inside a transaction and confirm the trigger rejects it, then roll back.

### T-079 - Projection through the versioned mapping, with version stamping

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Projection |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Complete the DF5 contract: source-shaped staging projects into the canonical plant model through the customer-authored versioned mapping, and every projected row records the mapping version that produced it.

**Validation.** Project a batch, change the mapping version, project again, and confirm each row records the version that produced it. No row may carry a null mapping version.

### T-080 - Idempotent reprojection and mapping-version regression

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P2 |
| **Module / Sub-module** | Backend / Projection |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Make reprojection idempotent and add the regression suite: reprojecting the same batch must not duplicate rows, and rolling a mapping back to a previous version must produce the earlier result exactly.

**Validation.** Reproject the same batch three times and confirm row counts are unchanged. Roll a mapping back and confirm the canonical output matches the earlier snapshot byte for byte.


## PHASE M2a-P3 - Job Runtime, Delta Propagation and Security Hardening

**9 tasks / 88 hours.** Make execution bounded and tenancy real. Chapter 4 5.3.9 proves that the answer to large data is architecture, not tighter licence limits.

### T-081 - Job target version policy and dependency DAG

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Jobs |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build on the M1 target_definition_id work: add the full version policy (pinned or current published), the job dependency graph, and cycle validation.

**Validation.** Declare a three-job chain, run it, and confirm order. Introduce a cycle and confirm it is refused at save time with a named code.

### T-082 - Weighted pools, compute weights and admission control

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Jobs |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement mechanisms 3 and 4 from Chapter 4 5.3.2: skip-if-running, latest-only, and admission control with weighted pools per job class.

**Validation.** Schedule more jobs than the pool allows and confirm they queue rather than degrade the machine. Confirm a second instance of a running job is skipped, not queued twice.

### T-083 - stage_watermarks and delta-scoped projection

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Delta propagation |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Today only import is incremental. Chapter 4 5.3.9 requires every downstream stage to be delta-scoped. Add the stage_watermarks table and make canonical projection delta-scoped against it. The arithmetic that justifies this: a naive full scan for one Pro-tier customer is 481 TB per day, while delta-scoped is 5 to 20 GB, a ratio of 24,000 to 1.

**Validation.** Change one source row and confirm projection processes a bounded delta rather than a full scan, evidenced by rows-scanned telemetry.

### T-084 - Delta-scoped feature refresh and analysis, with telemetry

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Delta propagation |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Extend the delta strategy to the feature refresh and analysis job classes, and emit rows-scanned telemetry per stage so amplification can be measured.

**Validation.** Change one source row and confirm the feature and analysis stages each scan a bounded delta. Telemetry must report rows scanned per stage per run.

### T-085 - Chunk manifests, checkpoint, resume and deterministic merge

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Delta propagation |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement Chapter 4 5.3.9.6a: bounded chunks with receipts, checkpoint and resume after interruption, and a deterministic merge so a resumed run produces the same result as an uninterrupted one.

**Validation.** Kill a running job at 60 percent, restart it, and confirm the final result is byte-identical to an uninterrupted run over the same input.

### T-086 - Scan budget and the Scan Amplification metric

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Infrastructure / Monitoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Add scan admission and the Scan Amplification Ratio from Chapter 6 6.1.12.2a, with a baseline and a regression gate that fails the build when amplification rises beyond the baseline.

**Validation.** Record a baseline, then deliberately remove a delta scope and confirm the gate goes red. Restore and confirm green.

### T-087 - Force RLS on every tenant-owned table with an architecture test

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Database / Security |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** The measured database has one RLS policy against 193 tables, while migration scripts 510, 530, 540 and 560 contain dynamic CREATE POLICY loops, so the scripts exist but the coverage does not. Establish the true coverage, then force RLS on every tenant-owned table and add an architecture test that fails the build when a tenant table is added without a policy.

**Validation.** A query listing tenant-owned tables without a policy returns zero rows. Add a new tenant table in a branch and confirm the architecture test goes red before it is merged.

### T-088 - Secret and configuration hygiene

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Security |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Remove PlantProcess__Auth__Users__0__IsBootstrapAdmin=true from env/profiles/local.env and env/profiles/presentation.env, both at line 42. Move the hardcoded PPIQ_E2E_PASS and the CI signing key out of deploy/scripts/ci-e2e-stack.sh. Parameterise the fifteen hardcoded server-IP references. Add secret masking to the audit package generator, whose header currently reads 'Mask Secrets : False' while the package contains credentials. Also fix the duplicated ConnectionStrings__PlantProcessDb line in both env profiles.

**Validation.** A secret scan across the repository returns no live credential. Generate an audit package and confirm secrets are masked. Confirm the E2E stack still runs with credentials supplied from the environment.

### T-089 - Tenant keys, tenant-aware uniqueness and canonical namespace on new APIs

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P3 |
| **Module / Sub-module** | Backend / Security |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Apply tenant keys and tenant-aware uniqueness to every table introduced in M1 and M2a, and put every new endpoint on the canonical namespace from the outset so the migration does not have to catch up with new work.

**Validation.** Insert the same natural key for two tenants and confirm both persist. A test asserting every endpoint added after this date matches the canonical domain pattern.


## PHASE M2a-P4 - Commissioning, Roles, Licence and the On-Site Package

**10 tasks / 120 hours.** Everything required to install and operate at the customer site. The visible surfaces were frozen in M1; this is the backend and the operational package behind them.

### T-090 - J1 to J3 commissioning built for real

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Backend / Commissioning |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 5 classifies installation, licence activation and user provisioning as commissioning prerequisites, which is why M1 narrates rather than demonstrates them. Build them: first-run installation, licence activation with the Ed25519 signed token, and initial user provisioning. Respect the Admin Golden Rule: the SOU support account is auto-provisioned and undeletable, while the customer administrator is a manual commissioning step and is never auto-created.

**Validation.** Commission a site from an empty database following only the runbook, with no developer intervention and no database console. Confirm the customer administrator is not created automatically.

### T-091 - Eight-role catalogue with three enforcement layers

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Backend / Users and roles |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement the role catalogue as the shipped default with a three-role minimum as the smallest legal configuration, enforced at the API, the query and the UI layers. FormalRoleAccessMatrix already models capabilities including AssistantChat; extend rather than replace it.

**Validation.** A matrix test asserting every role against every capability, at all three layers. Confirm a viewer cannot author SQL at any licence tier, which is a standing ruling.

### T-092 - Users and Roles administration surface

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Frontend / Users and roles |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Build the F1 Users and Roles page from Chapter 3 4.4 with the ten-field page contract. The code itself records that Users and Roles and System Health are missing from the UI.

**Validation.** Create, edit, disable and re-enable a user, and change a role assignment, entirely through the interface. Confirm the audit layer records each change.

### T-093 - Licence and entitlement enforcement

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Backend / Licence |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 6 6.3 requires the six commercial dimensions and the capacity envelope to bound a tier together. LicenseLimits already carries AllowsSqlEditor per tier, set by both LicenseService and VerifiedEd25519LicenseService and exposed by LicenseAdminEndpoints. Extend to full metering: retained volume, ingest rate, refresh floor, weighted compute slots and concurrent sessions. Exceeding a meter throttles rather than destroys, and every meter is visible to the customer.

**Validation.** Exceed a meter and confirm the import queues and the job waits for a slot rather than failing. Confirm the customer can see their own approach to each meter in the interface, since a limit the customer cannot see is a trap.

### T-094 - Container architecture and configuration profiles

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Deployment |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 6 6.1.2 specifies sixteen containers with one responsibility each and four configuration profiles. Infrastructure today is eight files and 856 lines. Build the container set, the image policy, health and readiness endpoints, and volume and secret segmentation.

**Validation.** Bring up each of the four profiles from a clean machine. Every container reports healthy. Confirm no container has a responsibility that belongs to another.

### T-095 - Install package, migration runner, upgrade and rollback

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Deployment |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build the fresh installation and upgrade paths from Chapter 6 6.1.4, including what an upgrade may never do, and a rollback path. Migration runs as a deployment step rather than as a manual action.

**Validation.** Install on a clean machine, upgrade from the previous version, then roll back. All three complete without manual database intervention. Run the fourteen post-deployment acceptance checks from 6.1.4.6.

### T-096 - Backup with a tested restore acceptance procedure

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Backup |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 6 6.1.11 requires not just backup but a tested restore acceptance procedure with a consistency rule. Implement schedule, retention, encryption and the restore rehearsal.

**Validation.** Perform a real restore into a clean environment and run the acceptance procedure against it. A backup that has never been restored does not count as a backup.

### T-097 - Minimum monitoring, health and alerting

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Infrastructure / Monitoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Implement the minimum from Chapter 6 6.1.12 required to operate a soft test: per-component metrics, alert severity and escalation, and the operational dashboard. Full observability and SLOs are M3.

**Validation.** Trigger each alert condition deliberately and confirm it fires with the correct severity and reaches the configured channel.

### T-098 - Support runbook and UAT dataset and configuration import

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Documentation / Handover |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Write the runbook the customer's own operator will follow, and build the path to import the customer's UAT dataset and configuration. Include the operator, data engineer and administrator sections.

**Validation.** A person who has not worked on the project commissions a site and completes one import following only the runbook. Every step they had to ask about is a defect in the runbook.

### T-099 - Canonical journey regression and the Continuity comparison

| | |
|---|---|
| **Milestone / Phase** | M2a / M2a-P4 |
| **Module / Sub-module** | Testing / Continuity |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Run J1 to J15 against a normal ppiq_app database with no presentation shortcut and no demonstration-only code path. Then run the Customer Contract Continuity comparison against the snapshots captured at the end of M1.

**Validation.** The journey passes on the canonical database. The continuity comparison shows no change to navigation, control placement, authoring flow, terminology or refusal semantics. Any difference is a defect in M2, not an improvement.


# M2b - INTELLIGENCE COMPLETION, SHIPPED DURING THE SOFT TEST (230 h)


## PHASE M2b-P1 - Intelligence Substrate and Practice Learning

**10 tasks / 116 hours.** Shipped as a governed update during the soft-test period. The readiness gate requires 60 independent units and 40 outcome events, so these engines cannot produce a real answer in the first weeks of a pilot regardless of when they are built.

### T-100 - Feature store, outcome store and snapshots

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Intelligence store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build the versioned feature and outcome history with snapshots, per Chapter 3 4.5.12. This is prerequisite to everything downstream, and nothing downstream may invent its own persistence.

**Validation.** Compute a feature set, snapshot it, change the underlying data, and confirm the snapshot still reproduces the original training input exactly.

### T-101 - Compute runs and correlation result persistence

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Intelligence store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Unify compute run records with gate state and gate evidence, and move correlation results onto the common substrate with evidence handles.

**Validation.** Every result row resolves to its run, its gate state and its evidence. A result with an unresolvable evidence handle fails the test.

### T-102 - Model registry, serving identity and fallback

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Model lifecycle |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement serving_role and the six-condition fallback policy from Chapter 4 5.6.7a, with drift observations.

**Validation.** Promote a model, force each of the six fallback conditions, and confirm the correct fallback occurs and is recorded. Confirm no prediction is served by a model without a serving role.

### T-103 - Practice signature, windowing, context and cohorts

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Implement the practice-learning engine core from Chapter 4 5.6.4b: signature construction, parameter windowing, context, and the comparison cohort.

**Validation.** On the presentation dataset, confirm the engine recovers the good-practice band planted in M1-P1 and does not recover a band from the null control.

### T-104 - Support, confidence, back-off ladder and tolerance sensitivity

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Add support and confidence ranking, the back-off ladder for sparse cohorts, and the tolerance sensitivity test that flags a practice whose result depends on the tolerance chosen.

**Validation.** A practice that survives resampling is ranked; one that does not is flagged. Widen the tolerance and confirm any practice whose result flips is marked sensitive.

### T-105 - practice_statistics persistence, drift and D10 Practice Insights

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Persist practice statistics, implement drift detection against the plant's own demonstrated best, and build the D10 page from Chapter 3 4.4.

**Validation.** Shift the operating data away from the learned band and confirm drift is detected and surfaced with its evidence.

### T-106 - Bindable intelligence registry and evidence handles

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Intelligence store |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Chapter 3 4.5.13 requires intelligence to be a first-class analytical object: a prediction, a finding, a practice benchmark or a value impact must be bindable by an authored page or widget exactly like canonical data. Build the bindable-intelligence registry so those sources appear in the authoring shell alongside plant dimensions and measures, and so every bound value carries a resolvable evidence handle.

**Validation.** Bind a prediction to a chart through the normal authoring shell with no code change, filter it, drill into it, and open its evidence. Then break one evidence handle and confirm the widget refuses rather than rendering an uncited number.

### T-107 - Tenant-aware uniqueness across the intelligence tables

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | Database / Intelligence store |
| **Priority** | Very Important |
| **Hours** | 10 |

**Description.** Every intelligence table added in this phase is tenant-owned. Apply tenant keys and tenant-aware uniqueness consistently, so two tenants can hold the same natural key without collision, and add the tables to the RLS architecture test built in M2a-P3.

**Validation.** Insert the same natural key for two tenants and confirm both persist. Confirm the RLS architecture test fails if one of the new tables is added without a policy.

### T-108 - Incremental practice recomputation

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P1 |
| **Module / Sub-module** | AI+ML / Practice learning |
| **Priority** | Important |
| **Hours** | 10 |

**Description.** Make practice recomputation incremental against the stage watermarks built in M2a-P3, so a growing history does not become a full recompute.

**Validation.** Add one day of data and confirm the recomputation scans a bounded delta, evidenced by the Scan Amplification metric staying within baseline.

### T-109 - Assistant finalisation on canonical sources

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

### T-110 - prediction_runs, predictions and prediction_current

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Prediction |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build the operational prediction pipeline and prediction_current as the complete operational read model, per Chapter 3 4.5.12.

**Validation.** Score a live population and confirm prediction_current reflects exactly the active predictions with no stale rows.

### T-111 - Prediction drivers and comparables, persisted

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Prediction |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Persist the contributing drivers and comparable historical cases so explainability is drillable rather than narrated.

**Validation.** Open a prediction and confirm its drivers and comparables come from persisted rows, not from a UI computation. Delete the rows and confirm the UI refuses rather than inventing.

### T-112 - Actionable deadline and latency health

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Prediction |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Implement actionable_deadline_utc and met_actionable_deadline from Chapter 4 5.8.8, so a prediction that arrives after the stage that could act on it is visibly recorded as missed.

**Validation.** Force a late prediction and confirm it is recorded as having missed its deadline and is not presented as actionable.

### T-113 - Remediation candidate generation from the customer's own history

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Remediation |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Search the customer's own history for comparable early conditions that later achieved a better outcome, and identify what was done differently in the remaining production stages. Persist the candidate with its proposed later-stage practice, historical support count, expected-effect range, comparable evidence and limitations.

**Validation.** Generate candidates on the presentation dataset and confirm each carries a support count above the configured threshold, an effect range and resolvable comparable evidence. A candidate with insufficient support must not be generated at all.

### T-114 - The per-prediction nine-check eligibility gate, can_accept and suppression

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Remediation |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Implement the nine checks from Chapter 4 5.6.4d and the can_accept authority from Chapter 3 4.5.12a. The design correction that matters most: eligibility is evaluated PER PREDICTION, not stored as a global property of the template, because the same template is actionable for one unit and not for another that has already passed the stage. Suppressed means suppressed - a failing candidate is not shown at all.

**Validation.** Craft one input per check and confirm each fails by name. Confirm that a candidate suppressed for one prediction is still offered for another where the checks pass. Confirm a suppressed candidate does not appear anywhere in the UI, since a reader under time pressure may act on what they see.

### T-115 - Accept, Reject and Defer with action recording

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Feedback loop |
| **Priority** | Critical |
| **Hours** | 8 |

**Description.** Implement the human decision boundary from Chapter 3 DF14: Accept, Reject and Defer, each recorded with its actor, timestamp and reason, and each producing an action record. The product must never automatically control the plant.

**Validation.** Take each of the three decisions and confirm each is recorded with actor and reason and produces the correct downstream state. Confirm no code path issues a control instruction to any source system.

### T-116 - Outcome capture, evaluation and escalation

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | AI+ML / Feedback loop |
| **Priority** | Critical |
| **Hours** | 6 |

**Description.** Capture the observed outcome after the fact, write the evaluation that closes the loop, and implement remediation_escalations for the cases that need a human above the operator.

**Validation.** Accept a candidate, let the outcome arrive, and confirm the evaluation is written and feeds the next governed review. Trigger an escalation condition and confirm it routes correctly.

### T-117 - Retire the superseded correlation engine

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Engine / Consolidation |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Three ICorrelationComputeEngine implementations exist plus a fourth engine key that appears in result rows but has no C# class, because those rows were written by running a Postgres function directly. The gated .NET engine is the DI default. Delete the retired Postgres engine and its SQL function, and decide whether the managed keyed service stays.

**Validation.** One engine remains reachable. A test asserts no compute path can write a finding without passing the readiness gate.

### T-118 - Fix the outcome namespace, grain assignment and ordinal loader

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Engine / Consolidation |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Three related defects: the engine and the registry use different outcome namespaces; the ML refresh routine deletes and re-inserts outcome values while assigning grain itself, so a manual grain correction is silently undone; and the ordinal loader selects only the effective sample key, numeric value, category value and heat id, never reading the severity column, so an ordinal outcome always reports a zero minority fraction.

**Validation.** One namespace across engine and registry. Correct the grain in the refresh routine and confirm it survives a refresh. Load an ordinal outcome and confirm the minority fraction reflects the real class spread.

### T-119 - Map the 108 page files onto the 40 target pages

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Frontend / Namespace |
| **Priority** | Very Important |
| **Hours** | 8 |

**Description.** Chapter 3 4.3 specifies 40 route pages plus 6 shell components. The repository holds 108 page files in 42 groups with 48 lazy route components, and roughly 14 files are reachable by nobody. Produce the keep, merge and delete decision per file, then delete the unreachable ones under Rule 4, which requires the replacement to land with the deletion in the same change.

**Validation.** A test asserting every page file under src/pages is reachable from a declared route. The count of page files moves toward 40 and no orphan remains.

### T-120 - Delete the legacy redirects and re-verify continuity

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Frontend / Namespace |
| **Priority** | Very Important |
| **Hours** | 6 |

**Description.** Remove the roughly 20 legacy Navigate redirects and the live phase-token routes (/phase8/*, /phase9/*, /phase15/*) that App.tsx still declares among its 69 route paths, then re-run the Customer Contract Continuity comparison.

**Validation.** No route path matches /phase\d+/. The continuity comparison against the M1 snapshots still shows no change to any presented page.

### T-121 - Complete the test gates

| | |
|---|---|
| **Milestone / Phase** | M2b / M2b-P2 |
| **Module / Sub-module** | Testing / CI/CD |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Add the remaining pipeline stages from Chapter 6 6.1.3 to reach the specified twenty-two, make visual regression and accessibility blocking, and make the golden journey J1 to J15 a merge blocker.

**Validation.** A deliberately broken golden journey blocks a merge. A deliberately introduced contrast failure blocks a merge. Both then revert to green.


# M3 - SITE STABILISATION, CERTIFICATION AND COMMERCIAL COMPLETION (204 h reserved)


## PHASE M3-P1 - Site Stabilisation and Real-Data Performance

**8 tasks / 96 hours.** Reserved capacity. Half of M3 is written by the customer during the soft test; these phases exist so that work has a home and does not become unplanned scope.

### T-122 - Site defect burn-down

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Backend / Site findings |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Fix what soft testing finds, without changing the frozen visible contract unless a formal product decision is approved and recorded.

**Validation.** Each defect has a reproduction, a fix and a regression test. Any visible-contract change carries a written approval.

### T-123 - Customer data edge cases

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Database / Site findings |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** New source patterns, dirty data, unusual keys, timestamps, nulls, late arrivals and customer-specific mapping requirements, handled through the three doors.

**Validation.** Each case handled through import, registry or authoring. Any case requiring a code branch is escalated as a design gap.

### T-124 - Connector certification against real sources

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Backend / Connectors |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Enable and certify the customer's actual connectors. A catalogue row is not a connector; an unbuilt one stays dimmed and badged as planned.

**Validation.** Each certified connector completes a read-only import against the real source with the load budget enforced.

### T-125 - Query plans, indexes and partition boundaries

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Database / Performance |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Tune against real measurements from the customer's volumes, not against assumptions.

**Validation.** Before and after query plans recorded for the ten slowest queries, with the improvement measured.

### T-126 - Pool weights, scan amplification and model-serving memory

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Infrastructure / Performance |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Tune the concurrency and memory model to the site's measured load.

**Validation.** Scan Amplification stays within baseline under real load. No job class starves another.

### T-127 - Customer definitions built through the product

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | Frontend / Authoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Build and validate the customer's real pages, relationships, measures, analyses, models and log rules using the product's own authoring surfaces.

**Validation.** Every customer artifact exists as a definition in the store, created through the interface, with no manual database insert.

### T-128 - Practice and prediction calibration

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P1 |
| **Module / Sub-module** | AI+ML / Calibration |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Longer data window, retrain and validate under governance, tune thresholds, measure deadline health.

**Validation.** A governed retraining record exists for each model, with the drift test that gated its release.

### T-129 - Remediation validation against real process constraints

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

### T-130 - C1 to C4 capacity certification

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Certification |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Run the four capacity profiles and site benchmarks, and replace the ten REFERENCE_ASSUMPTION constants in the Chapter 6 sizing model with measured values.

**Validation.** Every worked example in Chapter 6 6.1.9.6 recomputes correctly from measured constants. Chapter 6 is then frozen.

### T-131 - HA, DR and restore rehearsal

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Resilience |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Production topology, RPO and RTO objectives, and a real disaster-recovery rehearsal.

**Validation.** A full recovery rehearsal completes within the stated RPO and RTO, witnessed and recorded.

### T-132 - SSO and identity integration

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Backend / Security |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Integrate the customer identity provider with the final role catalogue, account lifecycle and emergency access.

**Validation.** Account provisioning, de-provisioning and emergency access all tested against the customer directory.

### T-133 - Site security hardening and sign-off

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Security |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Network rules, secrets, certificate rotation, RLS and tenant proof, audit review.

**Validation.** A tenant-isolation proof from the database alone, plus a signed security review.

### T-134 - Monitoring, SLOs and support escalation

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Infrastructure / Monitoring |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Operational dashboards, alerts, queue and latency and backup and certificate health, and the escalation path.

**Validation.** Each SLO has a measured baseline and an alert that fires before it is breached.

### T-135 - The Value Engine

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Backend / Value |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Build value_impacts, cost_assumptions, the value realisation ledger and the D7 Value Dashboard, honouring the two-downtime-quantity rule. This is the only work that moves the economic buyer, and the pilot supplies the real numbers it needs.

**Validation.** A euro figure is only ever shown as a bounded range with its assumptions visible and its evidence resolvable. No number without evidence.

### T-136 - Commercial capacity finalisation and the sales calculator

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Backend / Commercial |
| **Priority** | Very Important |
| **Hours** | 12 |

**Description.** Validate the real user, page, job, DB-link and data bands against measured infrastructure, and build the Sales Administration and capacity calculator from Chapter 6 6.3.8.

**Validation.** A quote produced by the calculator matches the measured server class for the same inputs.

### T-137 - Five-product website and portfolio

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Website / Public site |
| **Priority** | Important |
| **Hours** | 12 |

**Description.** Replace the /products/:code redirect that encodes the other four SOU products as PPIQ capability packs, and build the portfolio page and mega-menu.

**Validation.** Each of the five products has its own page under the Golden Rule product-page contract, and the honesty rule holds on every claim.

### T-138 - Documentation, training and production acceptance

| | |
|---|---|
| **Milestone / Phase** | M3 / M3-P2 |
| **Module / Sub-module** | Documentation / Handover |
| **Priority** | Critical |
| **Hours** | 12 |

**Description.** Runbook, operator and data-engineer and administrator guides, release notes, known limitations, rollback plan and the formal acceptance suite.

**Validation.** The acceptance suite passes and is signed. Known limitations are written down before the customer finds them.


---

## VERIFICATION BOUNDARY

No test was run, no source file was modified and no server or database was accessed in producing this backlog. Every file path, line number, class name and measured count traces to the 29 July 2026 repository package. Every hour figure is an estimate.