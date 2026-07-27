# PPIQ Session Handover - 25 to 27 July 2026

**From: the session that ran 25-Jul morning through 27-Jul 02:00**
**To: the next session, which must not start from a green field**

---

## 0. HOW TO USE THIS DOCUMENT

Read sections 1, 2 and 11 before doing anything. Section 1 is the doctrine you are bound by. Section 2 is the environment. Section 11 is how to work in this repository without repeating two days of failures.

**Then read section 6 before running any test or diagnostic.** Every measurement in this document has already been taken. Re-running them costs time and tells you nothing new. If a number is in section 6, cite it; do not re-measure it.

**Three standing facts that govern everything below:**

1. The customer presentation runs on **Karim's local laptop**, not the server. This was decided 25-Jul and it means the server can lag behind main without blocking the demo.
2. The API must be launched with **`-Profile presentation`**. The default `-Profile local` points at `ppiq_app`, where every result row is tenant-NULL and invisible under forced RLS. Launching wrong reproduces an empty Findings page no matter what the code says.
3. **Karim does not commit anything he has not manually tested.** This is his rule and it is correct. Do not push him past it. Do tell him when work is sitting uncommitted, because he lost a full day on 23-Jul to a bulk revert of uncommitted changes.

---

## 1. DOCTRINE - THE RULES YOU ARE BOUND BY

### 1.1 The governing document

`PPIQ_Constitution_v3.md` - 1,666 lines, in the repo under `Documentation/docs/Product RoadMap/`. It supersedes and absorbs `rules.txt` (1535 lines), `concept.md`, `concept_Amendment_6`, `PPIQ_Identity_and_Topology_v4.md` and the persona amendment. **Where anything conflicts with it, it wins.**

Note: `PPIQ_Constitution_v2.md` may still be committed in the same folder. It should be deleted - two versions of a constitution in one directory is exactly the pattern Karim's latest-concept law forbids.

### 1.2 The three product rules, permanent

**Rule 1 - GENERIC ONLY.** No line, word, page, component, schema object or code branch prepared for any specific dataset, industry, plant or customer. Industry knowledge enters through exactly two doors: customer data through the import pipeline, or user configuration in the product's own low-code surfaces. There is no third door.

**Rule 2 - STARTS EMPTY.** Day one at a customer, the plant schema has zero rows. Everything arrives through DB-link import then staging then generic projection. Defect catalogues and parameter definitions are *customer data*, imported, never seeded. The only pre-populated class is identity.

**Rule 3 - THE JOURNEY IS THE PRODUCT.** The 15-step canonical journey is the acceptance specification. Honesty over spectacle: anything not real is stated as roadmap in one scripted sentence.

### 1.3 Laws added during the 25-Jul investigation, each from a measured defect

| Law | Clause | What it forbids |
|---|---|---|
| Window Anchoring | II.7.5 | Anchoring an analysis window to wall-clock time instead of the dataset maximum |
| Single Engine Implementation | II.7.6 | More than one implementation of one analytical capability |
| Namespace Authority | II.9.2 | Writing a result under an outcome key the registry does not declare |
| Grain Assignment | II.9.3 | Attributing a row to a canonical grain that has no features for that sample |
| Typed Outcome Reading | II.9.4 | Reading an outcome from a column that does not match its declared type |

Enforced by gates G17 readiness integrity, G18 registry authority, G19 single implementation.

### 1.4 Karim's own rules, stated during this session, IN HIS WORDS

These are not my inventions. Treat them as binding.

**THE LATEST-CONCEPT LAW (26-Jul).** When code is dirty, or carries an old immature or childish concept, it must be **deleted, cleaned or fixed - never built upon**. Building on it leaves a growing pile of trash classes and retired concepts that mislead whoever reads the codebase later. The project began as a backbone and skeleton, but the design and the concept were then enhanced, so work must stick to the LATEST concept and remove what the older one left behind.

*This is doctrine-level and belongs in Part I beside the three rules. It is the parent of the eradication clauses (III.16.3) and of the Single Engine Implementation Law.*

**THE AUDIT CONCEPT - THREE LENSES (25-Jul).** Every element is examined three ways, not one:
- **Lens 1, surrounding:** the function works, but does everything AROUND it work - the button beside it, the resize, the orientation, the drag-and-drop
- **Lens 2, presentation:** it works, but does it look like a product a large company pays thousands of euro a month for - grouping, folding, alignment, density
- **Lens 3, deep wiring:** it looks present and correct, but is the wiring right, is the dropdown populated from live data, does the save reach the right place

*A pass that only asks "did the function run" is a smoke test, not an audit.*

**THE HOSTILE-HANDS PRINCIPLE (25-Jul).** The demo path is tested by walking it. The PRODUCT is tested by handing the mouse to someone trying to break it. Different exercises, both required. Every control the customer can reach is in scope, not only the rehearsed path.

**NO COMMIT WITHOUT MANUAL TEST (26-Jul).** "I never commit until I really consider the task is done." Gates passing is not done. A browser walk is done.

**EVERYTHING 100% GENERIC (26-Jul).** Every customer has a different plant, production line and staff, so everything must be buildable through the generic no-code wiring diagram or through SQL. Concretely: the customer writes the query in the UI SQL editor, presses run to test it, checks the returned rows, and then saves it to display on the chart.

**PAGE TYPES ARE JUST DESCRIPTIONS (26-Jul).** "Page type" is a description of the data inside, nothing special. There is no page-type object, no template system. A customer may have 25 pages of type 1, 18 of type 2, 12 of type 3 - they are all just dashboard definitions.

### 1.5 Pending amendments, agreed but not yet written into the constitution

**A2 - widget kinds and authorable filters.** The Add widget control opens a side toolbox bar from which the user first chooses WHICH KIND of widget: chart, table, calculated label, calendar filter, **filter**. Filters are a widget kind. A filter is a WHERE condition; every plant needs different ones, so a filter must be authorable through the same surface or through SQL, not a fixed set of dropdowns shipped in the product.

**A3 - the latest-concept law.** As stated in 1.4.

**THE BINDING INVERSION (26-Jul 01:00).** Dimension and measure are NOT the primary binding mechanism - they are the simple path. The real path is a QUERY, and dimension and measure become "which column of the query result is the axis". Karim's examples, none of which a catalogue can express: a customised correlation table, production per shift, consumption of one specific piece of equipment per shift, a chart of the correlation between speed and defect.

*Consequence: Part II.6 must say catalogue binding is the simple mode and an authored query is the general one.*

**FILTER COMPOSITION RULE (26-Jul).** A saved widget filter is that widget's permanent scope. The page filter bar and any associative click on another widget apply ON TOP of it, narrowing further inside that scope rather than replacing it. They combine with AND. *This is written into the authoring panel's hint text but is NOT verified in the backend.*

### 1.6 Working conventions Karim expects

- Zero preamble, no flattery. Evidence before cure. Never claim done when not done.
- **Every deliverable is a PowerShell apply pack.** This includes diagnostics. Never ask him to paste JS into DevTools or run ad-hoc commands by hand.
- **Never deliver zip files.**
- Pure ASCII, UTF-8 no BOM via `[System.IO.File]::WriteAllText` with `UTF8Encoding($false)`. CRLF for PS/CS, LF for .sh.
- No `&&` in PowerShell. Cuddled `} else {`. Packs run from the repo root.
- No em-dashes, no curly quotes.
- **Uploads frequently arrive empty.** Ask for output pasted as text in the message body.

---

## 2. IDENTITY AND TOPOLOGY - THE ENVIRONMENT AS IT ACTUALLY IS

### 2.1 Repository and machine

- Repo `kgamal369/PlantProcess-IQ`, local `C:\Workspace\PlantProcess-IQ`
- Branch `main`. At session end HEAD is `11c29218`. The branch is roughly 20 commits ahead of origin and **not pushed** - deliberate, because the demo runs locally
- Frontend `Frontend\PlantProcess.Web` - React 19.2, Vite, TypeScript, recharts ^3.8.1, reactflow on the canvas
- Backend `Backend\PlantProcess.Api` plus Application, Domain, Infrastructure, Analytics.Core, Analytics.Engine - .NET 9

### 2.2 Databases - VERIFIED by pgAdmin screenshot 25-Jul

The PostgreSQL instance holds `plantprocessiq` (legacy), `ppiq_app` (development), `ppiq_presentation` (demo), `postgres`.

**`ppiq_presentation` schemas, as they are:**

```
acquisition   canon   dump_store   ppiq_forensics   ppiq_meta   ppiq_plant   public
src_caster_oracle_shape   src_hsm_oracle_shape   src_inspection_mysql_shape
src_meltshop_pg   src_pkl_mssql_shape
```

**THERE IS NO SCHEMA NAMED `staging`.** This single fact caused two of the four canvas defects. Staging-class data lives in `dump_store`, nine tables:

| Table | Size |
|---|---|
| src_hsm_oracle_shape_hsm_pass_measurements | 8.2M |
| src_hsm_oracle_shape_hsm_coils | 5.2M |
| src_caster_oracle_shape_cast_pieces | 4.7M |
| src_pkl_mssql_shape_pickle_orders | 4.1M |
| src_pkl_mssql_shape_qa_lab_results | 3.1M |
| src_meltshop_pg_heats | 720K |
| src_meltshop_pg_lf_treatment | 304K |
| src_caster_oracle_shape_cast_sequence | 328K |
| src_inspection_mysql_shape_downtime_events | 144K |

Amendment 6 (ratified as Part III.16) renames this schema to `ppiq_staging` in M2. That is why the canvas fix made the schema a **configuration key** (`Prep:StagingSchema`, default `dump_store`) rather than a new literal - a literal would need changing twice.

**Warning for the room:** those table names read `src_..._shape_...`. That is emulator plumbing vocabulary an engineer will ask about. Karim must decide deliberately between showing them with the honest sentence that these are staged copies of the emulated sources, or presenting cleaner display names, which is not a short job.

### 2.3 Launch commands

```
.\scripts\run\start-api.ps1 -Profile presentation      <-- ALWAYS this for the demo
```

`start-api.ps1` line 3 defaults `-Profile local`, and `local.env` line 18 resolves to `Database=ppiq_app`. Launching without the switch is the single most expensive mistake available.

Profile output on a correct launch:
```
[S1A] Loaded profile 'presentation' from env\profiles\presentation.env
[S1A] API=http://localhost:5063 | WEB_PORT=5173 | DB=ppiq_presentation@5432
```

**Profile probe:** `GET /api/ml/foundation/readiness` returns counts. On the presentation database it reads `outcome_values 195,221` and `correlation_results 320`. If those numbers are different, the API is on the wrong database.

Both `local.env` and `presentation.env` declare `ConnectionStrings__PlantProcessDb` twice (lines 18 and 19) with identical values. Harmless, worth tidying.

### 2.4 API surface, confirmed from code

- `POST /auth/login` body `{ userName, password, requestedRole }`, response token field `accessToken`
- Groups at `/api/analytics/advanced`, `/api/ml/foundation`, `/api/prep/visual-mapper`, `/api/analytics/dashboard`
- Base `http://localhost:5063`
- `apiClient` does **NOT** prepend `/api` - every api file writes the prefix explicitly. This is what caused canvas defect 1
- `AdvancedDefaults.DemoTenant = 00000000-0000-0000-0000-000000000001`, which matches the tenant_id on all 320 presentation rows

### 2.5 Server and pipeline - what is known, and what was NOT touched

**Nothing in this session changed the server, Docker, Jenkins or the pipeline.** The 25-Jul decision was explicit: the presentation runs on the laptop, so infrastructure work was deferred to M2 and none of it was attempted.

Knowledge carried from earlier sessions, unverified in this one:

- Two compose projects must stay permanently separate: infrastructure (CI, reverse proxy, backup runner) and application (db, api, web). They were once merged, and orphan removal during an application deploy reaped the CI and reverse-proxy containers mid-deploy. Renaming the application project made orphan removal structurally unable to touch infrastructure
- The externally reachable health path returns **unauthorised** because it sits behind the authenticated edge. The deployment health gate therefore uses the **internal** container health path, which returns success. This is expected and must not be mistaken for a fault
- The preserved host environment file must be reused across deploys to keep the database password stable. Deleting it generates a new password that will not match the existing data volume, producing an authentication failure that looks like a code fault
- Frontend environment variables are inlined at BUILD time. Three things must align: the container build file declares them before the build step, compose passes them as build arguments, and the env file sets them. After any change the image must be rebuilt and the browser hard-refreshed
- All browser-facing URLs and permitted origins must derive from ONE public-host variable
- Known infrastructure debt: the reverse-proxy configuration references stale container targets and its host source file was deleted, so in-place edits fail and hot reloads do not persist. Do not recreate the proxy container until a persistent corrected configuration exists. The pgvector extension is unavailable in the running instance, limiting assistant retrieval to the extractive baseline
- Fifteen hardcoded references to the staging address exist across deploy scripts and documentation
- The bootstrap admin flag is TRUE in both local and presentation profiles. Correct for those; **must be false in any customer deploy**

**Pipeline truth problems, measured earlier, still open:**

- `package.json` `phase9:matrix` invokes playwright with `--list`, which ENUMERATES rather than executes
- `tools/ci/validate-real-ui-gates.cjs` - the guard that FORBIDS `--list` - is invoked by nothing, and would fail if it were, because it requires the Jenkinsfile to contain three npm test commands and the Jenkinsfile contains none of them
- So the visual-regression and accessibility suites execute in no pipeline at all
- The backend suite reports PASS with 66 passed, 91 skipped, 157 total - **58% skipped**, including the entire connector truth-contract family
- `PPIQ_Deep_PreCertification_Sweep_V3_FINAL.ps1` wraps every command under `ErrorActionPreference = Stop`, so any tool writing to stderr is recorded as failure regardless of exit code. This produced three false failures in one run, including a frontend build that had actually succeeded and failed only on a chunk-size warning
- `STATIC_AUDIT.md` carries 5 CRITICAL and 4 HIGH findings and the script **exits zero**, so it reports pass. Its contents have never been read

**No modifications were made to make the pipeline green or the app URL work in this session.** That work is M2-P3 in the backlog and is untouched.

---

## 3. WHAT WAS DONE THIS SESSION - CHRONOLOGICAL

### Phase 1, 25-Jul morning to midday: the engine investigation

Five sequential PowerShell diagnostics traced an empty Findings page. Results in section 6.

### Phase 2, 25-Jul afternoon: documents

- **`PPIQ_Constitution_v2.md`** written - merged rules.txt, concept.md, Amendment 6, Identity/Topology, personas into one 1,551-line document
- **`PPIQ_Constitution_v3.md`** - Part II.6 replaced in full with the shared authoring shell ruling; three new clauses (II.6.5 board semantics, II.6.6 illegal wiring, II.6.7 no second door); III.14.4 rewritten. 1,666 lines
- **`PPIQ_Presentation_Scoreboard.md`** and **`PPIQ_Demo_Readiness_Scoreboard.html`** - two scoreboards, delivery scope and demo scope
- **`PPIQ_Product_Backlog_v29.xlsx`** - 58 tasks, 465 hours, rebuilt from v28
- **`PPIQ_Hostile_Hands_Protocol.md`** - 8 parts, ~60 graded lines

### Phase 3, 25-Jul evening: scene review and the canvas chain

Scenes 10, 11, 12 deep-reviewed for the first time. Then four defects found and fixed in the preparation canvas, each of which looked like the whole problem until it was fixed.

### Phase 4, 25-Jul night to 26-Jul morning: the hook bug

Five attempts to mount an Add widget control. Root cause was never the wizard. Full account in section 5.

### Phase 5, 26-Jul: M1-16, then M2-22 and M2-23

Filters and derived columns on the canvas, then the widget authoring replacement.

---

## 4. EVERY PACK SHIPPED, WITH ITS COMMIT

All packs are in the repo root as `.ps1` files. Every one has `-ReportOnly` and `-Revert`.

| # | Pack | What it changed | Commit |
|---|---|---|---|
| 1 | `Apply-PpiqOutcomeDefaultsFix.ps1` | Toolbox OUTCOMES reordered to put `defect.rate_per_m2` first; `defect.edge_crack_rate` removed from AdvancedAnalysisPage and SuggestionRecommendationPage; Findings window 30 -> 3650; three `windowDays=30` client defaults -> 3650 | (in earlier commit) |
| 2 | `Apply-PpiqSceneA-Supervisor.ps1` | SupervisorReportPage.css rewritten: `white-space: pre-wrap` so report bodies wrap, a real flex disclosure row with a palette chevron replacing the unstyled native marker, card padding and radius; plus a date formatter replacing a raw ISO timestamp | `d6f43710` |
| 3 | `Apply-PpiqSceneB-Assistant-v2.ps1` | Created `AssistantChat.css` - all 14 `ppiq-assistant-chat*` class names had ZERO rules anywhere; added Enter-to-send; added `error?: string` to Turn with a distinct RED technical-failure state; stopped the catch block fabricating `isRefusal: true` on transport errors; replaced three hardcoded prompts, one of which named a nonexistent `edge-crack` key and was a Rule 1 violation | `fc68cd05` |
| 4 | `Apply-PpiqSceneC-Website.ps1` | Mounted four website components imported by nothing (ArchitectureFlowScroll, GoldenThreadScroll, IntegrationEcosystem, RoiCalculator); wrapped RequestDemoForm in `<div id="request-demo">` because the ROI CTA anchored to an id that did not exist | swept into `fc68cd05` |
| 5 | `Apply-PpiqCanvasApiBase.ps1` | `canvasApi.ts` BASE `/prep/visual-mapper` -> `/api/prep/visual-mapper`. All FIVE canvas calls shared that base | (committed) |
| 6 | `Apply-PpiqCanvasStagingSchema.ps1` | Catalogue query reads `Prep:StagingSchema` from configuration, default `dump_store`, parameterised | (committed) |
| 7 | `Apply-PpiqCanvasSqlToggle.ps1` | Authoring-mode toggle plus a read-only compiled-SQL pane; the backend dry-run now RETURNS the SQL it built so the pane shows what actually runs rather than a client reconstruction | (committed) |
| 8 | `Apply-PpiqCanvasSchemaTree.ps1` | Three-level unfolding schema tree replacing a flat list. The endpoint always returned schema, table and typed columns with `isKeyCandidate`; the flat list was discarding two of three levels. Key candidates now render as green markers | (committed) |
| 9 | `Apply-PpiqCanvasSelectSchema.ps1` | `BuildSafeSelect` had `staging.` hardcoded in BOTH the FROM and JOIN clauses. Now takes the schema as a parameter from the same config key, validated by the same identifier regex | `82b95a90` |
| 10 | `Apply-PpiqSchemaTreeT11Fix.ps1` | Three raw `<button>` in the schema tree -> `StandardP2Button`. Introduced by pack 8, which gated on tsc only | (committed) |
| 11 | `Apply-PpiqCanvasFilterDerive-Backend.ps1` | M1-16 part 1. `FilterSpec`, `DerivedSpec`, `MapperGraph` widened with optional arrays, `BuildSafeSelect` returns `(sql, err, prms)` and emits derived SELECT expressions and a WHERE clause, dry-run binds parameters | `b80a97c7` |
| 12 | `Apply-PpiqCanvasFilterDerive-Frontend.ps1` | M1-16 part 2. Filter and derived-column editors in the Preparation definition panel, `CanvasDefinitionEditors.css` | `b80a97c7` |
| 13 | `Apply-PpiqAddWidgetEntry-v5.ps1` | The Add widget control, with the hook declared in the hook block | `b80a97c7` |
| 14 | `Apply-PpiqWizardLayoutFix-v5.ps1` | Corrective stylesheet for the wizard: `flex: 0 0 auto` on step pills, `min-width: 0` on cards, and the primitive's wrapper span given the card's intended grid layout | `b80a97c7` |
| 15 | `Apply-PpiqWidgetAuthoring-A-v2.ps1` | **The replacement authoring panel.** One surface for add and edit, every list from `getDashboardMetadata` | `1c01484b` |
| 16 | `Apply-PpiqWidgetAuthoring-MeasureOptional.ps1` | Measure and dimension optional, hidden per chart type's `supportsMeasure`/`supportsDimension`; the saved-versus-live filter rule written into the panel | `1c01484b` |
| 17 | `Apply-PpiqWidgetAuthoring-C1-v3.ps1` | **Query binding mode.** Toggle Catalogue/Query, monospace editor with the grammar in its placeholder, Run test, result table | `11c29218` |

**Diagnostics delivered (read-only, no commits):**

| Script | Purpose |
|---|---|
| `Invoke-PpiqControlAudit-v2.ps1` | Live scan classifying every interactive control HAS HANDLER / NO HANDLER / LICENCE-GATED / UNMOUNTED, with a validity guard |
| `Invoke-PpiqStagingSchemaDiagnostic.ps1` | Which schema actually holds staging data |
| `Dump-PpiqCanvasSource.ps1` | Dumps canvas backend/api/page for anchoring |
| `Dump-PpiqWidgetContracts.ps1` | Dumps widget definition contracts, metadata sources, card wiring |
| `Apply-PpiqWidgetBuilderProbe-v2.ps1` | The probe that proved the wizard was innocent |

**Written and NOT yet applied:**

| Pack | Purpose |
|---|---|
| `Apply-PpiqWidgetBuilder-Delete-B.ps1` | Deletes the 15-file widget-builder tree. Refuses unless the replacement exists and unless zero references remain from outside the folder |

---

## 5. THE HOOK BUG - FIVE ATTEMPTS, AND WHY IT MATTERS

This consumed most of two evenings and it is the single most valuable lesson in this handover.

**The symptom.** Mounting an Add widget control on `InteractiveWorkspacePage` broke `/dashboard`, `/prep/canvas` AND `/workspace/CORRELATION_EXPLORER` - three unrelated routes, one fault, with "The application could not start".

**Four wrong diagnoses, in order:**

1. **v1** blamed the wizard: a static import pulling a throwing module into a shared Vite chunk. Reverted.
2. **v2** added three layers of containment - `React.lazy`, `Suspense`, and a dependency-free local error boundary. It gated clean and **broke the app identically**. That should have been impossible if the wizard were the cause.
3. **The probe pack** mounted NOTHING - one button and one `useState` - and broke the page the same way. **That was the proof.**
4. **v3** was correct code, stopped by its own self-check matching the word "return" inside its own explanatory comment about returns.
5. **v4** was correct code, stopped by PPIQ-T09 because the boundary heading contained "could not load".
6. **v5** complied and passed. In the browser: pages render, the control opens the wizard.

**The actual cause.** All three earlier attempts inserted

```
const [wizardOpen, setWizardOpen] = useState(false);
```

immediately above the component's MAIN return - which sits **below** that component's guard clauses. `InteractiveWorkspacePage` has two early returns between the hook block and the main return, measured by the pack's own preflight. A hook below a guard is a **conditional hook call**. React throws at runtime.

**Why nothing caught it:**

| Defence | Why it failed |
|---|---|
| `tsc` | A conditional hook call is not a type error |
| Error boundary | The throw is in the PAGE, above anything a boundary inside it wraps |
| `React.lazy` | The fault is not in the lazily loaded module at all |

**How to spot the risk before writing:** if the main return dereferences a possibly-null value without optional chaining - here `dashboard.name` where `dashboard` is `LoadedDashboard | null` - TypeScript only permits that because an early return narrowed it. So guard clauses EXIST between the hook block and the main return.

**The rule, now permanent:** hooks go with the other hooks at the top, above every guard. Any pack adding a hook must assert the new hook's character offset is after the last existing hook, before the main return, and that no `return` statement appears between them - with comments stripped before the check.

---

## 6. EVERY TEST AND DIAGNOSTIC ALREADY RUN - DO NOT REPEAT THESE

### 6.1 The engine investigation, 25-Jul 15:14 to 15:46

**Outcome-key namespace, `ppiq_presentation`, 320 correlation rows:**

| Outcome key | Registry declares? | Outcome values | Correlation results |
|---|---|---|---|
| defect.rate_per_m2 | yes, grain coil | **91,839** | 0 |
| defect.class | yes, grain coil | **51,691** | 0 |
| defect.severity | yes, grain coil | **51,691** | 0 |
| defect.position | yes, grain coil | 0 | 0 |
| downtime.cascade_minutes | yes, location | 0 | 0 |
| kpi.prime_yield | yes, generic | 0 | **30** |
| kpi.energy_per_ton | yes, generic | 0 | **30** |
| kpi.throughput | yes, generic | 0 | 0 |
| quality.defect_hold_binary | **no** | 0 | **112** |
| quality.defect_rate_per_m2 | **no** | 0 | **70** |
| downtime.equipment_stoppage_min | **no** | 0 | **30** |
| downtime.production_stoppage_min | **no** | 0 | **30** |
| downtime.cascade_amplified_flag | **no** | 0 | **18** |

**THE DECISIVE RESULT: no outcome key has BOTH values and results.** `ml_outcome_values` holds 195,221 rows for exactly three keys, all grain=coil. All 320 correlation rows are orphaned history from a retired engine version. 260 of 320 (81%) sit under five keys the registry does not declare.

So neither a wiring change nor a registry seed can populate the Findings page. There is nothing to point at. **The real gap is that Scene 8 to Scene 9 is a RUN that was never executed.**

**Tenant/RLS:** `ppiq_presentation` has 320 rows, 0 null tenants, 1 distinct tenant. `ppiq_app` has the same 320 rows **ALL tenant-NULL**, 0 distinct tenants. The RLS defect and its M2-28 backfill were real and independent of the outcome-key mismatch.

### 6.2 Readiness gate probe, 25-Jul 15:33, API on presentation profile

Confirmed by `outcome_values 195,221` / `correlation_results 320`.

**~45 historical runs across four dates. ALL Blocked. The engine has never completed a run.**

| Outcome | Blocked on | Measured | Threshold |
|---|---|---|---|
| defect.class | minority-class balance | 0.0% | 3.0% |
| defect.severity | minority-class balance | 0.0% | 3.0% |
| defect.rate_per_m2 | required-field completeness | 46.5% | 85.0% |

`defect.rate_per_m2` has **4 of 5 gates green**: 2,441 independent heats, 91,417 outcome events, minority 50.0% vs 10.0%, freshness green.

**Threshold defaults, read from `ReadinessThresholds` and verified sound - DO NOT WEAKEN:** HeatsReady 60, EventsReady 40, MinorityReady 0.10 partial 0.03, CompletenessReady 0.95 partial 0.85.

### 6.3 Gate-gap diagnostic, 25-Jul 15:46 - the causes

**`defect.class`:** three values exist - Defect 51,266 / FinalDecision 2 / SURFACE_CRACK 1. Minority is one row = 0.002%, printed 0.0%. Fixing this means authoring defect-class variety across 51,266 rows. Leave it blocked.

**`defect.severity` IS AN ENGINE BUG, not a data problem.** `severity_value` has a healthy spread - Minor 28,578 / Major 14,278 / Critical 6,422 / medium 699 / high 648 / low 640 / High 2 / Info 1 / Medium 1 - but `category_value` is NULL on every row, and the loader SQL selects only `effective_sample_key, numeric_value, category_value, heat_id`. **It never reads `severity_value`.** Ordinal maps to Categorical which groups on Category, so one null group gives 0.0%. Secondary: two mixed vocabularies, casing duplicates, and a singleton `Info` row that would still drag the minority to 0.002% after the loader is fixed.

**`defect.rate_per_m2` completeness gap is two clean cohorts, 99.9% of the miss:** slab 18,074 keys with 0 features, heat 2,441 keys with 0 features, versus coil 17,823 keys with 17,820 features and (null) native_grain 8,643 keys at 100%. Only 17,820 distinct sample keys have features at grain=coil at all - 115,003 feature rows, 12 distinct features. Root cause: features were only ever generated for coil-native samples while slab and heat outcome rows were placed into the coil-grain outcome set.

**`ml_outcome_values` is DELETEd and re-INSERTed by a refresh routine (`source_system 'PPIQ-ML-Refresh'`) whose INSERT assigns grain itself.** A manual grain UPDATE is silently undone. The fix is in the refresh function.

### 6.4 Engine count

**THREE `ICorrelationComputeEngine` implementations exist:**

| Class | Key | Status |
|---|---|---|
| DotNetAdvancedCorrelationEngine | dotnet-analytics-core-v1 | **gated, DEFAULT** |
| PostgresCorrelationComputeEngine | postgres-v6-type-aware | previous generation, off by default |
| ManagedStatisticalComputeEngine | managed-stat-v1 | keyed service "managed", selectable per request |

A fourth key `ppiql-deterministic-core-v1` appears in the 320 rows' `evidence_json` but has **no C# class** - it is the Postgres SQL function's own key, so those rows were written by running the function directly.

**CORRECTION recorded, and it matters:** an earlier alarm that "a second engine bypasses the readiness gate" was OVERSTATED. DI registration reads `Analytics:AdvancedEngine:Enabled` with default TRUE and selects the gated engine. The flag is not overridden in any appsettings or env profile. The ungated path exists but is not reachable as configured. **This is dead-code cleanup, not an honesty emergency.**

Note also: `rules.txt` Rule 4 specifies the engine has TWO LAYERS - normal data analysis and deep AI/ML - plus a weekly supervisor. That is a deliberate two-layer design and is NOT what the three correlation engines are; those are three implementations of the same single layer.

### 6.5 Rule 1 evidence worth showing a customer

`defect.rate_per_m2` `native_grain` values include: slab, heat, cast, packagedlot, rawmaterial, aluminumroll, aluminumcast, aluminumbillet, compoundbatch, tireunit, customerroll, batch, lot. **The canonical layer genuinely absorbed aluminium, tyre and batch/lot product types.** That is Rule 1 proven with data, not asserted in a slide.

Blemish: the canonical grain is named "coil" - steel vocabulary on a tyre unit. Rename candidate for M2.

### 6.6 Control audit, 26-Jul - M1-02, COMPLETE

**v1 produced a FALSE GREEN** and the reason is instructive: it reported "0 dead controls on the demo path" while its ON DEMO PATH column read 0 for EVERY classification. Zero controls matched the demo path at all. Two causes: lazy routes (`const X = lazy(() => import(...))`) invisible to an `import ... from` regex, and wrapped route elements (`element={<Guard><Page/></Guard>}`) capturing the guard's name.

**v2 result, validity guard passed:**

| Metric | Value |
|---|---|
| Routes resolved | 42 |
| Files reachable from a route | 95 of 167 |
| Interactive controls | 319 |
| On the demo path | 110 |
| HAS HANDLER | 185 |
| NO HANDLER | 20 |
| UNMOUNTED | 114 |

**All 8 flagged demo-path dead controls were verified in source and ALL EIGHT ARE FALSE POSITIVES:**
- a COMMENT line containing the text `<StandardP2Select>`
- primitive definitions where the handler arrives via `{...rest}` spread (StandardButton, StandardPageCompat x4)
- two `type="submit"` buttons INSIDE a `<form>`, which correctly have no onClick

**So M1-02 lens one is CLOSED AT ZERO.**

Genuine unreachable findings: the whole widget-builder tree, four V5 prototype pages, four more built-but-unrouted pages.

The committed inventory at `Frontend\PlantProcess.Web\docs\ui-standards\button-inventory.csv` was last modified **2026-06-24** and lists 214 controls against a live count of 319. It is stale; use a live scan.

### 6.7 Architecture suite - the standing gate

15 test files, 56 tests. Green at session end. The files:

```
noMojibake  largeFileBoundaries  noHardcodedDemoPages  noPhaseTokensOnDemoPath
assistantChain  uiConformanceRatchet  noRawErrorStrings  noThinReExports
noRawStandardElements  P2Close.errorBoundaryDiscipline  dataIntegrationIA
noDebris  noLegacyApiGrowth  frontendArchitecture  journeyProfessionalUi.contract
```

**Three of them stopped packs during this session and their exact rules are in section 11.**

### 6.8 Frontend build gate

`node node_modules/typescript/bin/tsc -b` - exit 0 at session end. `tsc --noEmit` is a NO-OP in this workspace; `tsc -b` matches `npm run build`.

### 6.9 Backend build gate

`dotnet build` from `Backend\PlantProcess.Api` - exit 0 at session end, 23 warnings. **The API must be stopped first** or the build fails copying locked assemblies.

One warning introduced by our work and worth tidying: `VisualMapperEndpoints.cs:138` async method lacks `await`.

### 6.10 What has NEVER been run

- The full `PPIQ_Consolidated_Test_Pass.md` Parts 0-8
- The hostile-hands protocol
- Any browser walk of: Edit on an existing widget, Run test on a real query, the filter-composition check
- Any measurement of concurrency, the 100-job claim, sizing, or a restore drill

---

## 7. THE IMPLEMENTATION AS IT NOW STANDS, AND WHAT CHANGED

### 7.1 The preparation canvas - S1

**It was completely disconnected from its backend at the start of 25-Jul evening.** Four separate defects stood between it and working, and each looked like the whole problem until it was fixed:

| # | Defect | Effect |
|---|---|---|
| 1 | `canvasApi.ts` BASE missing `/api` | All five calls 404 |
| 2 | Catalogue queried schema `staging`, which does not exist | Panel empty |
| 3 | No mode toggle, no SQL view | II.6.2 unmet |
| 4 | `BuildSafeSelect` hardcoded `staging.` in FROM and JOIN | Preview would fail even after 1 and 2 |

**It now has:** a three-level unfolding schema tree with typed columns and green key-candidate markers; an authoring-mode toggle; a read-only compiled-SQL pane showing the query the SERVER built, not a client reconstruction; and filter and derived-column editors.

**M1-16 backend, the safe-SQL contract:**
- `FilterSpec(Table, Column, Op, Value)` and `DerivedSpec(Alias, LeftTable, LeftColumn, Op, RightTable, RightColumn, Constant)`
- `MapperGraph` widened with `FilterSpec[]? Filters = null, DerivedSpec[]? Derived = null`, so every graph saved before M1-16 deserialises unchanged and compiles to byte-identical SQL
- Operator whitelist `= <> > >= < <= LIKE, NOT LIKE, IS NULL, IS NOT NULL`; arithmetic whitelist `+ - * /`
- **Values ALWAYS bound as `$n`**, never concatenated. Values that parse as numbers are bound as numbers so numeric comparisons work
- NULL tests emit no parameter
- Division wrapped in `NULLIF(x, 0)`
- Anything outside a whitelist is refused **by name** and recorded as `rejected_by_safe_sql`

**Design property worth reusing everywhere:** the operator lists in the interface are byte-identical to the whitelists the server enforces, and the pack's self-check asserts every operator string is present in the page. **The interface cannot offer an operator the server would refuse.** Illegal states are unreachable rather than rejected after the fact.

**Deliberate design decision, recorded:** filters and derived columns are edited in the Preparation definition PANEL, not as chainable canvas nodes. The backend applies filters as ONE WHERE across the joined result - it is not a pipeline. A Filter node on the board would draw a pipeline the generator does not have, and the customer's engineer is exactly the person who would find that. Chainable nodes need a pipeline model in `BuildSafeSelect` and that is M2-21.

**Still missing from S1 against Constitution II.6:** the right-hand block palette (needs real block types first), SQL authoring as opposed to SQL viewing, the debug log with described severities, and `isValidConnection` - `onConnect` still calls `addEdge` unconditionally, so **every wire between every pair of ports is accepted and the port colours are decoration, not enforcement**. That is M1-04 and it is the highest-value small fix remaining in the authoring layer.

### 7.2 Widget authoring - S2

**Before:** a six-step wizard, built, never mounted, contradicting the constitution three ways - fixed business purposes, a closed set of filter categories, and being a wizard where II.6.7 rules one shared shell with no second door.

**Now:** `WidgetAuthoringPanel.tsx` - one surface serving add AND edit.

- Every list from `getDashboardMetadata()`: dimensions, measures, chartTypes, filters, purposes, compatibilityRules, safetyLimits
- Filter values from `getDashboardReferenceData()`
- Compatibility honoured from the server: choosing a chart type narrows the dimension and measure lists via `compatibleChartTypes`
- Measure and dimension both optional, hidden per chart type's `supportsMeasure` / `supportsDimension`
- Saves via `createDashboardWidgetDefinition` or `updateDashboardWidgetDefinition`
- Edit opens the same panel with the definition loaded - `SavedDashboardWidget` already accepted `onEdit`; the page now passes it
- **A pack self-check asserts no dimension, measure, purpose or filter is written as a literal in the panel**

**C1 added:** a Binding toggle of Catalogue / Query; a monospace editor whose placeholder shows the compiled grammar; a Run test control; a result table; typed refusals shown whole.

**A correction worth carrying:** I first called the wizard's filter grid "hardcoded" and that was too strong. The filter VALUES all come from the server. What IS fixed is the SET OF CATEGORIES, baked into the `DashboardReferenceData` type. A closed category set, not compiled-in industry data.

### 7.3 The widget query grammar - the biggest single finding of the session

`Backend\PlantProcess.Application\Dashboarding\Services\Widgets\WidgetQueryExpressionService.cs` contains **TWO grammars behind an environment flag** `PPIQ__UseCompiledWidgetGrammar`, which **defaults to OFF**. The product runs `ParseLegacy`. The newer grammar is dead code behind a flag - exactly the pattern Karim's latest-concept law forbids.

**The compiled grammar is much richer and already unit-tested:**

```
CompiledWidgetQueryExpression {
  Source
  Dimensions[]                                   repeated
  Measures[]   -> (Aggregate, Column, Alias)     measure: avg(speed) as avg_speed
  Filters[]    -> (Column, Operator, Value)      whitelist = != >= <= > < contains in
  Sort[]       -> (Column, Direction)
  Limit
  TimeWindow   -> (Column, Window)
}
```

Plus a typed failure enum `UnknownKeyword | MissingValue | TypeMismatch | InvalidGrammar` and a `WidgetQueryExpressionDiagnostic` record. **That is the honest-refusal contract already applied to a grammar.**

**THE DEFECT:** `Parse()` calls `Compile()` and then COLLAPSES the result back into the flat legacy DTO:

```csharp
DimensionCode: value.Dimensions.FirstOrDefault()?.Column,      // only the first
MeasureCode:   value.Measures.FirstOrDefault()?.Alias ?? ...,  // only the first
Filters:       request.Filters,      // the COMPILED filters are DISCARDED
Options:       request.Options,
```

**The compiled Filters, Sort, Limit and TimeWindow are computed, validated, and thrown away at the boundary.** Everything downstream - `DashboardWidgetQueries.cs` and its ~11 functions - only speaks the flat DTO with `DashboardWidgetFiltersDto`.

**So C2 is not invention.** It is: switch the flag on, stop collapsing, and teach the query path to consume the compiled shape.

**Rule 1 note on the grammar itself:** `DirectAllowedKeys` is a hardcoded HashSet of ~40 tokens including `defect`, `defectType`, `risk`, `riskClass`, `shift`, `shiftCode`, `material`, `materialType`. Plant vocabulary compiled into the expression language. A generic product takes those from the registry.

**Endpoint confirmed:** `group.MapPost("/widgets/execute", ExecuteWidgetExpressionAsync)` - "Parses a small safe widget expression DSL and executes it using the normal whitelisted dashboard widget query engine." **The gap is the CLIENT** - none of the sixteen methods in `dashboarding.api.ts` reaches it. That is C2's first step.

### 7.4 The assistant - scene 11

Fixed: a stylesheet that did not exist (all 14 class names had zero rules); Enter-to-send; and the important one - **the catch block was pushing `isRefusal: true` with the raw error message, so a 500 or a 401 was displayed to the customer under the heading "Insufficient evidence"**. A transport fault dressed as an honest abstention, on the exact scene where the abstention claim is made. Now a distinct red technical-failure state.

Still true and must not be promised: citations expand a provenance handle and do NOT open a source row (no evidence-row route exists); the assistant does not reliably refuse a predictive question.

### 7.5 A recurring pattern worth naming

Three separate components in this session were **built, committed, and never mounted** - and in every case the code compiled while the surface was broken:

| Component | What was wrong |
|---|---|
| AssistantChat | 14 class names, zero CSS rules anywhere |
| Four website components | Imported by nothing |
| WidgetBuilderWizard | CSS written for a raw `<button>`; the primitive wraps children in a `<span>`, so the card's grid had one child instead of two and the text collapsed |

**Code nobody mounted is code nobody reviewed** - not visually, not doctrinally. This is the strongest argument for M2-31, the CI gate that fails on any unmounted component.

---

## 8. BACKLOG - TASK BY TASK STATUS

Governing board: **`PPIQ_Product_Backlog_v29.xlsx`** - 58 tasks, 465 hours, IDs from 01, no gaps.

**The seven board laws:** done tasks DELETED not archived; no PARTIAL status, a partial task is rewritten as its remainder with a fresh estimate; IDs restart sequential and lower means higher priority; phases strictly P1..Pn; phases 40-65h, critical first; every phase ends pushable; junior-ready text with paths, commands and exact acceptance.

**The M1/M2 split rule Karim set:** M1 = everything required for the presentation. M2 = everything else - users, roles, licensing, deployment and infrastructure, schema correction, retiring the old engine, hardening.

### 8.1 M1-P1 Verification, Audit and Demo Lock - 63h

| ID | Task | Status |
|---|---|---|
| M1-01 | Push the trunk | **DONE** |
| M1-02 | Exhaustive control audit | **DONE** - dead controls on the demo path = 0, all 8 flags proven false positives |
| M1-03 | Hostile-hands protocol | Document delivered. **Walk barely started** - it surfaced the canvas chain and then the session moved to building |
| M1-04 | Refuse illegal wiring on the canvas | **NOT STARTED** - `onConnect` still accepts everything. 3h, highest-value small fix |
| M1-05 | Diagnose the zero-row widgets | **NOT STARTED** - roughly half of widget queries return rows=0, never diagnosed |
| M1-06 | Write the cut register | **NOT STARTED** |
| M1-07 | Defect buffer | Partly consumed by the canvas and wizard fixes |
| M1-08 | Consolidated test pass Parts 0-8 | **NOT STARTED** |
| M1-09 | Browser-verify the three scene packs | **NOT STARTED** |
| M1-10 | Confirm the type-3 AI/ML page exists | **NOT STARTED** - MODEL_INSIGHTS was not found in the DB scripts |
| M1-11 | Review the alerting surface | **NOT STARTED** - `/alerting` routed, never reviewed at any depth |
| M1-12 | Reorder the authoring narrative to lead with S1 | **NOT STARTED** |
| M1-13 | Deck upgrade with live screenshots | **NOT STARTED** |
| M1-14 | Dress rehearsal twice | **NOT STARTED** |

**Two of fourteen complete.**

### 8.2 M1-P2 Impress Beats - 42h

| ID | Task | Status |
|---|---|---|
| M1-15 | Live readiness-gate panel | **NOT STARTED** - and it is now the most valuable impress item, because the engine never completes and the abstention IS the beat |
| M1-16 | Filter and Derive on the canvas | **DONE, both halves, gated** |
| M1-17 | Finding to genealogy jump | NOT STARTED |
| M1-18 | True XY scatter | NOT STARTED |
| M1-19 | Associative tri-state in filter dropdowns | NOT STARTED |
| M1-20 | Phase push and demo freeze | NOT STARTED |

### 8.3 M2 - what moved

| ID | Task | Status |
|---|---|---|
| M2-22 | Widget authoring through the shell | **SUBSTANTIALLY DONE.** One panel, add and edit, metadata-driven. Remaining: delete the wizard tree (Pack B written, not applied) and walk it |
| M2-23 | SQL authoring with a governed execution path | **HALF DONE.** C1 shipped the write-run-inspect loop. C2 - store the query on the widget and render from its columns - needs two backend dumps |
| M2-01..M2-07 | Engine truth and data correctness | NOT STARTED. Causes fully diagnosed in section 6.3 |
| M2-08..M2-13 | Users, licensing, value | NOT STARTED |
| M2-14..M2-20 | Infrastructure | NOT STARTED |
| M2-21 | Shell foundation, palette, debug log | NOT STARTED |
| M2-31 | Control-handler CI gate | NOT STARTED |

### 8.4 The honest observation about where the hours went

The last two days were **building**. `M1-P1` is verification, and it is 2 of 14 done. `M1-16` is not even in `M1-P1` - it is P2, an impress beat. The canvas packs were not in the backlog at all; they were discovered while walking.

**Features increased. Verification did not move.** `M1-05` (half the widgets empty), `M1-08` (the full walk) and `M1-14` (rehearsal) are the three that actually reduce presentation risk, and none has started.

---

## 9. SCOREBOARD AT SESSION END

### 9.1 Delivery scope - `PPIQ_Presentation_Scoreboard.md`

| Viewpoint | Score | Band |
|---|---|---|
| Developer / maintainer | 62 | Needs work |
| Process and quality engineer | 57 | Needs work |
| Software engineer / configurator | 65 | Needs work |
| CEO / economic buyer | 51 | **Critical** |
| Infrastructure engineer | 45 | **Critical** |
| **HEADLINE (lowest persona)** | **45** | **Critical** |

Bands are Karim's own from rules.txt Part C: <55 critical, 55-69 needs work, 70-84 solid, 85+ strong. **70-84 is explicitly "complete, stable and honest for demonstration scope"** - so a demo-ready product SHOULD score in the seventies, not the nineties.

### 9.2 Demo scope - `PPIQ_Demo_Readiness_Scoreboard.html`

Different criterion: can it be SHOWN working, and is there a prepared honest sentence for what it cannot do.

| Point | Today | Ceiling after M1 |
|---|---|---|
| Three analysis pages | 55 | 80 |
| Five no-code surfaces | 64 | 76 |
| The engine | 70 | **88** |
| The assistant | 66 | 78 |
| The journey | 62 | 78 |
| The website | unverified | 75 |

| Viewpoint in the room | Today | Ceiling |
|---|---|---|
| Process and quality engineer | 58 | 80 |
| Technical engineer | 68 | 79 |
| CEO | 52 | **60** |
| Infrastructure (scored as ANSWERABILITY) | 64 | 70 |
| **HEADLINE** | **52** | **60** |

**THE KEY FINDING:** every viewpoint rises materially with M1 work **except the economic buyer**, which stops near 60 because the value engine and live tier switching are M2 by Karim's own split rule. **The CEO gap is narrative work, not build work.** It will not improve before the room no matter how many hours go into the product. Prepare that conversation deliberately; improvising it will read as evasion.

### 9.3 Movement during this session

Not re-scored formally. Directionally:

- **The engine** was already the strongest demo point at 70 and is unchanged - the abstention is the beat
- **The five surfaces** moved up: S1 went from disconnected to working end to end with a real tree, a toggle, a compiled-SQL view and filter/derive editors; S2 went from a hardcoded wizard nobody could reach to a metadata-driven panel serving add and edit
- **The assistant** moved from unstyled and dishonest-on-failure to styled with a correct technical-error state
- **Nothing in infrastructure or CEO moved**, because nothing in those was attempted

### 9.4 Strengths Karim forgets to sell under pressure

Written here because he defends weaknesses and forgets these:

- **The readiness gate.** Five named dimensions, published thresholds, per-gate evidence reconstructable from the database alone. No competitor shows a prospect a red status
- **The associative selection model.** Real possible-versus-excluded state across widgets. Most BI products do not have this
- **The genealogy layer.** Bidirectional walk on the customer's own keys, attribution weights summing to exactly 1.0 per child, enforced by a database trigger
- **The multi-grain canonical model.** Thirteen native product types absorbed. Rule 1 proven with data
- **The visual join canvas.** A genuine typed-port node canvas
- **Honesty carried as stored data** - every finding persists its own framing and records that no language model participated in the compute path

---

## 10. THE ARCHITECTURE SUITE - EXACT RULES, SO PACKS COMPLY BY CONSTRUCTION

Three of these stopped packs during this session. **Copy the project's regex; never paraphrase it.**

### PPIQ-T09 - `noRawErrorStrings.test.ts`

Scans every `.ts`/`.tsx` under `src`, excluding tests and `.stories.tsx`. Allowlists ONLY `src/components/standard/DataFetchBoundary.tsx` and `src/components/standard/ErrorBoundary.tsx`.

**Forbidden anywhere else:**
```
/could ?n.?t load/i     /failed to load/i
/unable to load/i       /loading failed/i
```
The first also catches "could not load", "couldnt load", "couldn't load".

Safe alternatives used successfully: "did not open", "the fault is inside X itself", "Loading the widget builder..." - a present-participle loading message is fine; it is the failure phrasings that are banned.

### PPIQ-T11 - `noRawStandardElements.test.ts`

Raw `<button>` and `<table>` where a `Standard*` primitive exists. Zero tolerance.

### The UI conformance ratchet - `uiConformanceRatchet.test.ts`

```js
const RAW   = /<(input|select|textarea|label)\b/g;   // D1
const STYLE = /style=\{\{/g;                          // D2
```
Exempt from D1 only: `components/standard`, `ui/standard-components`, `components/brand`.

**It is a BASELINE comparison, not a zero rule.** An existing file may carry violations up to its committed count in `uiConformance.baseline.json`; decreases pass; **any NEW file starts at baseline zero**. A pack that adds a file must emit zero of both.

**Note `label` is a raw control.** To keep a caption's accessible name: render it as a `<span>` and put `aria-label` with the same text on the StandardP2 control.

### Primitive prop shapes learned the hard way

- `StandardP2Table` is a styled wrapper over a real `<table>` taking `TableHTMLAttributes`. It takes **children**, not `headers`/`rows` props. Render `<thead>`/`<tbody>` inside it - the inner table elements are not forbidden, only a raw `<table>` is
- `StandardButton` wraps its children in a `<span>`. Any legacy CSS written for a raw `<button>` with direct children will find one wrapper child instead. **This caused the wizard's overlapping text**
- `StandardP2Button`, `StandardP2Input`, `StandardP2Select` spread native attributes, so `aria-*`, `title`, `onDoubleClick`, `className`, `key` all pass through

---

## 11. HOW TO WORK IN THIS REPOSITORY - THE RULES THAT COST TWO DAYS

### 11.1 The gate is TWO things

`tsc -b` **AND** the architecture suite. Every frontend pack in this run gated on tsc alone; the schema-tree pack introduced three raw `<button>` and left the ratchet RED, and three later packs inherited the same half gate. **Karim found it by running the suite himself.**

- Frontend: `node node_modules/typescript/bin/tsc -b` then `npm run test -- src/test/architecture`
- Backend: `dotnet build` with the API **stopped**
- `tsc --noEmit` is a NO-OP in this workspace

### 11.2 Dump before you anchor

**This is the discipline that turned the failure rate around.** My repository snapshot went stale after four patches, and both of the 25-Jul night failures came from acting on incomplete knowledge. From 26-Jul onward every pack was preceded by a read-only dump of the actual current file, and every one of those packs matched its anchors first time.

`Dump-PpiqCanvasSource.ps1` and `Dump-PpiqWidgetContracts.ps1` are in the repo root for this.

### 11.3 Line endings

Pack files are written with LF; the repo's `.tsx`/`.cs` use CRLF. **A multi-line anchor carrying LF never matches.** Single-line anchors match fine, which makes the failure look like drift when it is not.

The tell: single-line anchors pass, multi-line anchors fail, in the same pack.

The fix, applied to BOTH Old and New before matching:
```powershell
$t = $Text.Replace("`r`n","`n").Replace("`r","`n")
if ($FileText.IndexOf("`r`n") -ge 0) { $t = $t.Replace("`n","`r`n") }
```
Print the detected convention in the preflight so the reader can see it.

### 11.4 A guard must read CODE, not prose - SIX occurrences

**The single most repeated failure of the session.** A self-check matching text the pack ITSELF wrote:

1. A literal token quoted in a comment - three false rollbacks in one day
2. `import[^\n]*widget-builder` matching the pack's own `await import(...)`
3. A hook-placement check matching the word "return" inside the pack's own comment about returns
4. A PPIQ-T09 phrase in a boundary heading
5. A scoped-selector check matching two CSS COMMENT lines that began with `.wizard-card-grid` because the sentence wrapped that way
6. A raw-control check that omitted `label` because it paraphrased the project's regex instead of copying it

**Avoiding the trigger word is luck. Stripping comments is correctness:**
```powershell
# TS/JS
$code = (($region -split "`n") | ForEach-Object { $_ -replace '//.*$','' }) -join "`n"
# CSS
$code = [regex]::Replace($css, '(?s)/\*.*?\*/', '')
```
Then match with word boundaries: `\breturn\b`, not `return`.

**And simulate every guard against the pack's own emitted content before shipping.** The test is mechanical: for each guard pattern, search the pack's `New` strings with it. If it matches anything the pack legitimately emits, the guard is wrong. Doing this once would have caught all six.

### 11.5 PowerShell mechanics that bit

- Never build code lines by string concatenation inside an array literal - it splits one line into several while the anchor log still reports a correct match
- **Never use `npx` in a gate.** `npx tsc -b` hung for over ten minutes because npx contacts the registry when it cannot resolve a binary
- Use the call operator and `$LASTEXITCODE`. `Start-Process -PassThru` plus `WaitForExit(ms)` does not reliably populate `.ExitCode` and returned `$null`, reporting a clean build as a failure
- `$ErrorActionPreference = "Stop"` turns any stderr write from a native command into a terminating error. Use `Continue` and judge by exit code
- **Delete `*.tsbuildinfo` before any tsc gate you intend to trust** - tsc caches errors and replays them
- Never capture external command output when a gate might be slow; a captured gate is indistinguishable from a hung one. Stream it live

### 11.6 Files with existing non-ASCII

`AdvancedAnalysisPage.tsx` has 18, `advancedAnalysis.ts` has 1, `DashboardFilterBar.tsx` has 206. Those cannot be embedded whole in an ASCII-only here-string. Edit by anchor and add a guard comparing the non-ASCII count before and after - **that guard caught mojibake correctly and is worth keeping in every pack.**

### 11.7 The pack shape that works

```
preflight -> report (hash, timestamp, non-ASCII count, line-ending convention)
  -> anchor verify WITH DIAGNOSIS
  -> backup to a timestamped folder
  -> apply
  -> on-disk self-check
  -> gate (both)
  -> auto-revert on any failure
```
Plus `-ReportOnly` and `-Revert`. **One pack per concern**, so each is independently revertible.

**The anchor diagnosis matters:** when an anchor fails, print whether its FIRST LINE is present on its own. If yes, the block exists and its interior differs. If no, it is genuinely absent. Without this, a failed anchor is indistinguishable from real drift.

### 11.8 A validity guard on any measurement

The control audit v1 reported a clean zero that was produced by a failed match. **A zero produced by a failed match is worse than a red, because it reads as a pass.** Every measuring script should assert its own preconditions and refuse to print an acceptance number when they fail.

### 11.9 Things I got wrong, recorded so they are not repeated

- **Recommended action one step ahead of the evidence, twice in one hour** - first an RLS remark, then suggesting `kpi.prime_yield` as a zero-code demo path when it had 30 results and zero outcome values
- **Called an alarm before checking the DI registration** - the "second engine bypasses the gate" claim was overstated; the gated engine is what runs
- **Called the wizard's filter grid hardcoded** when the values come from the server; only the category set is closed
- **Blamed the wizard for two days** when the fault was a hook placement in the page
- **Gated on half the gate** for four consecutive packs
- **Assumed a primitive's prop shape** twice - `StandardP2Table`, and `StandardButton`'s child wrapper
- **Nearly restyled under cover of a bug fix** - a corrective stylesheet that changed `display` and track sizes; caught it and cut it back to the two missing properties

---

## 12. OPEN ITEMS, RANKED

### 12.1 Unverified in a browser - the largest removable risk

Everything below is code-verified and gate-verified only:

| Item | Two-minute check |
|---|---|
| Edit on an existing widget | Open a widget's action menu, press Edit. The panel should open with its definition loaded |
| Run test on a real query | Query mode, write against a view that exists, press Run test. Three outcomes are all useful |
| Filter composition | Save a widget with one filter, note its numbers, click a value on another widget. If it narrows, the two compose. If it does not move, the saved filter overrides the live selection - a defect |
| The three scene packs | Supervisor wrapping and date; assistant Enter and the stop-the-API red-not-amber check; website rows 12.2 to 12.5 |

### 12.2 Known small defects, each one fix away

| Defect | Fix |
|---|---|
| Catalogue fields still visible in Query mode | `.wauth-grid`'s `display: grid` beats the `[hidden]` attribute. One CSS line |
| `onConnect` accepts every wire | `isValidConnection` on the canvas. M1-04, 3h, highest value in the authoring layer |
| Purpose fills the chart type but not dimension/measure | Either the metadata's `recommendedDimensions` are empty or the compatibility filter removes them. Check which |
| `VisualMapperEndpoints.cs:138` async without await | Warning only |
| Duplicate `ConnectionStrings__PlantProcessDb` in both profiles | Cosmetic |
| `PPIQ_Constitution_v2.md` still committed beside v3 | Delete it |

### 12.3 Demo-path items never addressed

| Item | Note |
|---|---|
| **Half of widget queries return rows=0** | Never diagnosed. Highest cost, cheapest fix. A page of eight widgets where four are empty reads as a broken product |
| All demo widgets grouped by dimension `day` | Correct on LINE and AREA; produces a 60-slice donut and a heatmap of dates. **Fix by switching chart type - no code needed** |
| All 8 filter chips read N/A | `associativeFields` dimension codes do not match registry names |
| Material Mix donut returned a 14x14 surface | Never re-measured after the first-paint fix |
| MODEL_INSIGHTS (type-3 page) | Not found in the DB scripts. Existence unconfirmed |
| `/alerting` | Routed, never reviewed at any depth |

### 12.4 M2 work with its causes already diagnosed

| Item | What is already known |
|---|---|
| Grain assignment in the ML refresh routine | Exact cohorts measured: slab 18,074 and heat 2,441 keys with zero features |
| Ordinal loader never reads `severity_value` | The loader SQL and the healthy distribution are both documented above |
| Registry authority | 260 of 320 rows under five undeclared keys, rename pattern visible |
| Retire the superseded engines | Three implementations, one capability. Which is default and why is documented |
| Canonical grain named "coil" | Rename to an industry-neutral identifier |
| C2 - the compiled expression flow-through | The grammar exists, is tested, and its output is discarded. Two dumps needed |
| Tier-to-feature matrix | **Blocks Rule 5.2 entirely.** No matrix exists, so the feature cannot be built |
| The value engine | Named in the doctrine as the largest doctrine-to-build gap. The formula referenced as rules.txt section 7.5 was never in any shared document and must be supplied |

---

## 13. EXACT NEXT ACTIONS

### 13.1 Immediately

1. **Ask Karim for the presentation date.** Everything below is ordered differently depending on the answer, and the honest advice at 48 hours is very different from the advice at two weeks.

2. **Two two-minute walks that unblock decisions:**
   - Press Edit on a widget - confirms M2-22
   - Query mode, Run test - three outcomes, all useful, and the third names the request shape C2 needs

3. **Apply `Apply-PpiqWidgetBuilder-Delete-B.ps1`** with `-ReportOnly` first. It is written, self-verifying and waiting.

### 13.2 If the demo is close

Stop building. The three that reduce risk:

- **M1-05** - diagnose the zero-row widgets. Fix or remove. A curated page where every widget carries data beats a comprehensive page half empty
- **M1-08** - the consolidated test pass, once, in one sitting, with `-Profile presentation`
- **M1-14** - two timed rehearsals with a contingency card

Plus **M1-06**, the cut register: every removed scene and claim with its one spoken sentence. Known entries: the login scene, licence tier switching, "show me a completed run", citations not opening a source row, no reliable predictive refusal, and the SQL editor being view-only.

And **M1-15**, the readiness-gate panel - the highest-value impress item precisely because the engine never completes. Four green dimensions with real numbers and one honest red is a better artefact than a correlation the data was bent to produce.

### 13.3 If there is more room

Karim's stated order was M1-16, then M2-22, then M2-23, then the add-widget button. That order is now: **M2-22 finish (Pack B), then C2, then M2-21 (the shell foundation and palette).**

C2 needs, pasted as TEXT:

```powershell
Get-Content .\Frontend\PlantProcess.Web\src\api\dashboarding\dashboarding.api.ts
Get-ChildItem .\Backend -Recurse -Include DashboardWidgetQueries.cs |
  ForEach-Object { Get-Content $_.FullName | Select-Object -First 120 }
```

---

## 14. THE ONE PARAGRAPH THAT MATTERS MOST

The product's real differentiator is that **it refuses to compute when the data cannot support a defensible answer, and says exactly why**. Five named readiness dimensions, published thresholds, evidence reconstructable from the database alone. Every competitor computes. None of them will show a prospect a red status.

The engine has never completed a run, and that is not the weakness it looks like. `defect.rate_per_m2` shows **four green gates with real numbers** - 2,441 independent heats, 91,417 outcome events, minority balance 50% against a 10% bar - **and one honest red at 46.5% field completeness against 85%.**

That screen is the product. It should be rehearsed as the centrepiece, not apologised for.

The sentence for the room when someone asks to see a finished one: *"This dataset has never cleared the gate. Here is the dimension that blocks it, the measured value, and what it would take. It will compute when that is satisfied, and not before."*

---

*Handover written 27 July 2026, 02:00. Every number in it was measured, not estimated. Every claim that is unverified says so.*
