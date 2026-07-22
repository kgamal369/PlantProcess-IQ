# PPIQ Consolidated Test Pass - M1 demo path + M2 Impress Sprint
**One sitting, ~45 minutes, screenshots once. Nothing is tested twice.**
Date run: ____________   Build/commit: ____________

Grades: **[P]** pass  |  **[B]** built, has an issue, one spoken sentence written  |  **[X]** broken -> fix or remove from the demo path

---

## PART 0 - Prerequisites (do these first, ~10 min)

These are wire-ups the packs deliberately did not auto-patch. Parts 4 and 5 cannot be
tested until 0.1 and 0.2 are done.

- [ ] **0.1 Program.cs**: add `app.MapVisualMapperEndpoints();` beside the sibling `Map*` calls
- [ ] **0.2 App.tsx**: lazy-import both canvas pages, add routes and nav entries
      - `<Route path="/prep/canvas" element={<VisualJoinCanvasPage />} />` (nav: "Join Canvas")
      - `<Route path="/analysis/toolbox" element={<AnalysisToolboxPage />} />` (nav: "Analysis Toolbox")
- [ ] **0.3 Website** (separate repo path): wire the enhancement imports into `NewHomePage`
      order: Hero -> ArchitectureFlowScroll -> packs -> GoldenThreadScroll -> IntegrationEcosystem -> RoiCalculator -> RequestDemoForm
- [ ] **0.4 Restart the API** with the presentation profile (picks up pareto validation + visual-mapper endpoints)
- [ ] **0.5 Frontend running**, log in as e2eadmin

Reference psql connection for every truth-check below:

    & 'C:\Program Files\PostgreSQL\16\bin\psql.exe' -d "host=127.0.0.1 dbname=ppiq_presentation user=ppiq_dev password=ppiq_dev_local_only" -c "<QUERY>"

---

## PART 1 - The demo path (M1-11 scope, ~12 min)

| # | Surface | Grade | Screenshot | Note if [B] |
|---|---------|-------|-----------|-------------|
| 1 | Login | [ ] | | |
| 2 | Connections incl. the two Oracle profiles | [ ] | | |
| 3 | Prepare Import + live registration path | [ ] | | |
| 4 | Importing / Jobs Monitor | [ ] | | |
| 5 | Genealogy: coil -> slab -> heat -> provenance | [ ] | | |
| 6 | Command Dashboard renders; KPIs show numbers (M1-23) | [ ] | | |
| 7 | Findings page (step 10) | [ ] | | |
| 8 | Supervisor page (step 14) - expect honest empty state | [ ] | | |
| 9 | Assistant: cited answer renders | [ ] | | |

**6 truth-check (M1-23)** - the two KPIs must equal:

    SELECT count(*) FROM quality_events;
    SELECT count(*) FROM risk_scores;

KPI quality = ______ (SQL ______)   KPI risk = ______ (SQL ______)

**9 note**: the predictive question does NOT reliably refuse. Do not promise a refusal in the room.
Lead with the cited answer; use an off-domain question if you want the refusal beat.

---

## PART 2 - Workspace interaction (M1 Qlik fixes + M2-43, ~8 min)

Open PRODUCTION_OVERVIEW.

- [ ] **2.1** All widgets render, no error banners
- [ ] **2.2** Chart-type switcher morphs a widget (bar -> pie -> table)
- [ ] **2.3** Global filter change -> every widget requeries
- [ ] **2.4** Drag + resize a widget, Save layout, reload -> layout persists
- [ ] **2.5 (DEF-005)** Click a slice on a **defectType** widget -> the **defect** filter applies,
      NOT materialCode. Other widgets re-query. "Clear all" restores.
- [ ] **2.6 (DEF-006)** Widget menu -> **Clone** -> a copy titled "<name> (copy)" appears
- [ ] **2.7 (DEF-006)** Widget menu -> **Remove** -> widget disappears AND stays gone after reload
- [ ] **2.8 (DEF-007)** Any chart click -> **drilldown drawer** opens with the row payload; closes cleanly

2.5 is the one that used to empty the workspace. If it still writes materialCode, note the widget's
dimensionCode here: ____________ (it needs a line in `state/widgetSelectionMap.ts`)

---

## PART 3 - Associative engine (M2-37, ~7 min)

The ASSOCIATIVE VIEW strip sits above the workspace grid.

- [ ] **3.1** Panel renders with field columns and chips
- [ ] **3.2** Click a **Material** chip -> it turns green; **Defect** column greys impossible values
- [ ] **3.3** Click a **Defect** chip -> Source / Equipment columns re-shade
- [ ] **3.4** Click a **Source** chip -> counts update everywhere; widgets requery too
- [ ] **3.5** Click an **excluded** (struck-grey) chip -> selection pivots to it (correct Qlik behaviour)
- [ ] **3.6** Toggle "live: off" -> panel stops recomputing; "live: on" -> resumes

**Truth check (3.2)** - pick the material you clicked:

    SELECT count(DISTINCT defect_type) FROM quality_events WHERE material_code = '<picked>';

Panel Defect possible-count = ______   SQL = ______   (must match)

**Fields showing "n/a"**: list them here -> ____________________________
Each means its dimensionCode is not in the safety registry; fix is a rename in
`state/associativeFields.ts`. Honest degradation, not a break.

---

## PART 4 - Canvas foundation (M2-31, ~8 min)  [needs 0.1 + 0.2]

**/prep/canvas**
- [ ] **4.1** Palette lists staging tables
- [ ] **4.2** Click two cross-source tables onto the canvas; columns show as typed ports
- [ ] **4.3** Wire a key column to a key column -> edge labels the equality (`piece_id = material_id`)
- [ ] **4.4** **Preview (dry-run)** -> sample rows render in the right panel
- [ ] **4.5** Add a third table with NO join -> Preview returns the honest rejection
      ("table X has no join to the graph"), not a crash
- [ ] **4.6** **Publish version** -> returns a version number

**Truth check (4.6)**:

    SELECT version_number, version_status, published_by FROM public.ppiq_visual_mapper_versions ORDER BY version_number DESC LIMIT 3;

**/analysis/toolbox**
- [ ] **4.7** Three blocks render; changing Outcome / Grain / Window updates the payload panel
- [ ] **4.8** Parity line reads **IDENTICAL**
- [ ] **4.9** "Run governed analysis" submits (engine gate behaviour unchanged - blocked is fine and honest)

---

## PART 5 - Chart catalogue (M2-38-lite, ~5 min)

- [ ] **5.1** Switcher offers **heatmap** and **pareto** on any widget
- [ ] **5.2** Switcher offers **scatter** ONLY on avgParameterValue / riskScore / defectRate widgets
      (server registry rule - absence elsewhere is correct, not a bug)
- [ ] **5.3** **Pareto**: descending bars + cumulative % line on the right axis
- [ ] **5.4** Click a pareto bar -> filters by the widget's dimension; associative panel re-shades
- [ ] **5.5** **Heatmap**: intensity grid; click a cell -> same cross-filter
- [ ] **5.6** **Scatter** on a compatible widget: click a dot -> same cross-filter
- [ ] **5.7** Save a widget as **pareto**, reload -> persists (server accepted the type)

---

## PART 6 - Findings + three page-types (M2-28 + M1-39, ~5 min)

- [ ] **6.1** Findings page lists rows (was empty before the tenant backfill)

**Truth check**:

    SELECT count(*) FROM ml_correlation_results_v2 WHERE tenant_id IS NOT NULL;

Page rows = ______   SQL = ______ (expect 320)
If the page is still empty while SQL says 320, the API's session tenant does not match
`ppiq_current_tenant()`; note it here and it becomes a one-line config fix: ____________

- [ ] **6.2 Type 1 (linked data)**: PRODUCTION_OVERVIEW - full walk clean
- [ ] **6.3 Type 2 (statistics)**: correlation / findings board - renders with the new charts
- [ ] **6.4 Type 3 (AI/ML)**: model insights + assistant - renders

---

## PART 7 - Website (M2-42, ~3 min)  [needs 0.3]

- [ ] **7.1** Home page loads; scroll -> Architecture and Golden Thread SVGs **draw as you scroll**
      (draw once and hold, no looping)
- [ ] **7.2** Integration Ecosystem section renders its six groups
- [ ] **7.3** ROI calculator: move all three sliders -> the annual figure updates live
- [ ] **7.4** ROI CTA scrolls to the demo request form
- [ ] **7.5** No blockers, no failure states, no "unfinished" language anywhere on the page
- [ ] **7.6** Contact email is the real address, not the placeholder

---

## PART 8 - Full test suite (run once, at the end)

    cd Frontend\PlantProcess.Web
    npm run build
    npm test

- [ ] **8.1** `npm run build` -> zero errors
- [ ] **8.2** `npm test` -> PPIQ-T11 and the UI conformance ratchet both pass
- [ ] **8.3** Note any NEW failures here (pre-existing ones are not sprint regressions):

______________________________________________________________

---

## CLOSE-OUT

Total [X] items: ______  -> each is fixed or removed from the demo path before rehearsal
Total [B] items: ______  -> each has its one spoken sentence written in the playbook

**Known and accepted (do not file as defects):**
- Engine may return Blocked with a real run id. That is the honest-abstain moat, not a failure.
- Assistant does not reliably refuse predictive questions. Demo script avoids promising it.
- Scatter is category-vs-measure. True XY scatter is full-catalogue scope.
- Dimensions outside the selection map keep legacy materialCode behaviour and open the
  drilldown without filtering.

Rehearsal (M1-14) uses exactly the path graded [P] above.
