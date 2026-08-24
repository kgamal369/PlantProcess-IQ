# PlantProcess IQ - Master Design Document

**Version 4.10 | Author: Karim, SOU Industrial Software, Dusseldorf** | **MASTER DESIGN FREEZE CANDIDATE**

> **Change log — Two-Release Production Roadmap and Day-1 Workbench Constitution (23 August 2026, v4.10).** v4.10 replaces retired internal programme codes with exactly two product releases: **M2 — Release 1, 30 September 2026**, for genuine early production and first-week customer work; and **M3 — Release 2, 30 October 2026**, for heavy production, higher data volume, more users and advanced intelligence. Each release uses only **P1, P2, P3, P4 and P5**. Release 1 makes DB Link/data onboarding, Canvas/data preparation, Jobs, enterprise BI reliability, read-only production OPC UA, governed References/Reconciliation/Assistant and minimum production hardening first-class release gates. Release 2 owns scale, advanced BI/authoring, deep enterprise administration, InsightBoard composition, multi-objective optimisation, customer-grade ROI convergence and heavy-production certification. Design and backlog are required to be one-to-one traceable: every designed product outcome has an execution owner and acceptance path, and every backlog task maps to an owning design contract.

> **Change log — Operational-Regime, Multi-Objective Practice and Period-Driver Hardening (22 August 2026, v4.9).** v4.9 closes the two generic gaps exposed by the first oil-plant requirement review without introducing oil-specific vocabulary: process transitions/changeovers and stabilisation become first-class governed context so statistics cannot mix distinct operating regimes; practice learning gains customer-declared multi-objective objective sets with Pareto/non-dominance and explicit preference resolution rather than silently choosing one KPI; exact period-to-period operational driver decomposition is added so the Assistant can explain changes in cost/productivity drivers from Layer-A facts before the monetary Value Engine is available. The release also binds the September checkpoint/fallback to the single v2.13 execution workbook. The six chapters remain the only design authority.


> **Change log - Second-Order Consistency Pass (v4.4 to v4.5). MASTER DESIGN FREEZE CANDIDATE.** No product capability added. The remediation block is restated as producing **historically supported candidates**, with operational recommendation status decided solely by the per-prediction gate of 5.6.4d; the contradictory sentence calling a recommendation "an instruction to a plant" is replaced by the rule that the product produces evidence-backed recommendations for human decision and never sends a control instruction; the benchmark claim becomes a stated **engineering target** verified by the acceptance criteria rather than an assertion of parity; model loading, fallback approval and fallback condition 5 all use the full serving identity `(tenant_id, model_code, outcome_code, grain_code)`; and the decision boundary is restated so that Accept, Reject and Defer are gated identically by `can_accept`, with `RM10` protecting all three. See Chapter 3 v4.5 for the matching schema, payload and page corrections.

---

> **CURRENT AUTHORITY — Master Design v4.10.** PlantProcess IQ has exactly six current design-authority chapters and one current execution-authority backlog workbook. No other file may define, amend, override, supplement or reinterpret current product design or implementation scope. A design change edits the owning chapter directly; a scope change edits the backlog directly. Transitional reviews, amendment packs, ledgers, mandates and prior revisions are historical evidence only after their accepted content is integrated. Validation scripts are code/enforcement instruments, not design documentation.


# CHAPTER 4 - SPECIFIC SOFTWARE PRODUCT TECHNICAL FUNCTION DESCRIPTION


## 5.0.0 Enterprise BI, authoring and Day-1 workbench completeness contract (v4.10)

The professional reference target is **Qlik Sense / Power BI / Tableau-class outcome quality**, not imitation of a competitor's proprietary syntax. Release acceptance is maintained as a traceability matrix covering the founder-approved families **F01–F15 (BI core), S01–S30 (visual/interaction behaviour), and E01–E30 (editor/data/jobs)**. Every row must be classified as either (a) implemented directly, or (b) implemented by an explicitly documented PPIQ-native equivalent that preserves the user outcome with equal or stronger governance. No row may remain `unowned` at M3 release exit.

The following outcomes are explicit PPIQ product capabilities and may not be left implicit:

- **Scoped Measure Context** — one measure may intentionally evaluate under a governed selection scope different from the page/global scope, providing the product outcome commonly served by set-analysis expressions without copying vendor syntax.
- **Named Comparison State** — a page/object group may bind to an independently named selection context for side-by-side comparisons while preserving provenance of both states.
- **Selection tools** — click and multi-select are core; lasso/range selection uses a pending selection state with explicit confirm/cancel where the renderer supports it.
- **Bookmarks and stories** — bookmarks preserve selection and page state; narrative snapshots retain a link back to their live evidence source.
- **Export contract** — governed exports cover current-state data and the release-owned image/document/tabular formats, and always carry the active selection/evidence context.
- **Reusable authoring subflows** — shared, versioned subflows/definitions provide the product outcome of script include/reuse without importing another product's script semantics.

### Safe Scratch-like Canvas control flow

The Canvas toolbox includes arithmetic (`+`, `-`, `*`, `/`), comparisons, Boolean `AND/OR/NOT`, `IF/ELSE`, relational/data-shaping operators, governed aggregates, window operators, model/intelligence invocation and bounded control-flow blocks.

To satisfy visual-programming use cases without creating non-terminating industrial jobs, loop semantics are **bounded by contract**:

- `ForEach` — iterate a finite declared collection/partition set;
- `RepeatN` — repeat a subflow a declared finite count;
- `WhileBounded` — Scratch-like while semantics **with mandatory `max_iterations`, runtime budget and cancellation token**;
- `Map/Window/Group` — preferred declarative data iteration for row/time-series work.

An unbounded `while(true)` or equivalent cannot be published. Validation must prove a finite bound before a graph can become a Job. This is a product safety rule, not a removal of the user's control-flow capability.

### M2 versus M3 authoring depth

**M2 Release 1** owns the Day-1 workbench: source connection, schema discovery, preview, mapping, Canvas creation/edit/reopen, SQL mode, the production arithmetic/logic/control/data-preparation library, validation/dry-run, result preview, persistence/versioning, Job binding, run/monitor/log and enterprise BI core reliability.

**M3 Release 2** owns advanced authoring depth and heavy-use parity: named comparison states at scale, scoped-measure authoring depth, advanced story/export/exploration behaviours, reusable subflow libraries across teams, InsightBoard composition, high-volume optimisation and the remaining enterprise parity matrix closure.

*Maps to PPIQ.txt section 5. Audience (5.8): the customer's advanced IT and software staff, and our developers taking hand-over. Voice (5.9): senior product owner and technical lead.*

**One file per chapter.** This is the single authoritative Chapter 4.

**Authority.** Chapter 2 is the naming, structure and positioning authority; Chapter 3 specifies the journey to endpoint level, the pages and the schema. This chapter uses **J1-J15**, **DF1-DF15**, the **40 route pages and 6 shell components**, the glossary of Ch2 3.9, the relationship model of Ch3 4.5.10, the definition store of Ch3 4.5.11, the intelligence tables of Ch3 4.5.12 and the error catalogue of Ch3 4.5.21 without variation. **Target design only**: no build state, no current namespace, no plaintext credential.

**Requirement classes** per Ch2 3.10 are marked where a section is not Core.

**Genericity of examples.** Every table, column, parameter, defect, grade and route name appearing in a diagram, a sample statement or an example message in this chapter is a **neutral placeholder** such as `source_a`, `param_a`, `<dimension_a>` or `<parameter_code>`. The product ships no such vocabulary. Any domain-flavoured example that survives elsewhere in this document is labelled illustrative only and **never defines product logic** (Rules, Rule 1).

## Contents

| Part | Covers | PPIQ.txt |
|---|---|---|
| 5.1 | The analysis page: features, components, widgets, style, filters, dynamic interaction, add page, add widget, edit widget query, link data to widget, chart types and style, page layout, KPI | 5.1 |
| 5.2 | The no-code / low-code shell: layout, schema table bar, toolbox drag-and-drop, wiring diagram with debugging and saving, transfer to code, joining and use in analysis, SQL editor, predefined and advanced toolboxes | 5.2 |
| 5.3, 5.4 | Multi-threading and load balancing of jobs; the gate and the engine, its rules and validation, coefficient adjustment with the learning curve, how the assistant draws from it, the engine as hub | 5.3, 5.4 |
| 5.5-5.7 | Statistics and correlation blocks; AI and ML blocks including the practice-learning engine, the predict-then-remediate pipeline and the model-serving path; the AI Assistant as a persistent dock | 5.5, 5.6, 5.7 |
| 5.8 | Additional designed capabilities: scenario simulation, alert routing, prediction explainability, the feedback loop, benchmarking, text evidence, inspection images, near-real-time scoring | 6.1-6.8 |

---

---

## 5.1 THE ANALYSIS PAGE (PPIQ.txt 5.1)

The Qlik-class interactive workspace: features, components, widgets, style, standard, UI/UX, filters, dynamic interaction, add page, add widget, edit widget query, link data to widget, chart types and chart style, page layout, KPI.

---

## 5.1.1 Design goals and the benchmark

The analysis page is judged against mature professional analytics platforms. **Not copied.**

> **The engineering target: match the core authoring and exploration quality expected from mature analytics platforms, then differentiate through the industrial intelligence, genealogy, evidence and remediation layers.**

This is a target to be verified by benchmark testing against the acceptance criteria of 5.1.15, not a claim of parity. **Parity is asserted only after it is measured.**

| Goal | Test |
|---|---|
| **Associative, not filtered** | Clicking a value tells you what is *possible* and what is *excluded*, not merely what remains |
| **Composed, never coded** | Every page, widget, filter and KPI is authored from the interface and stored as a definition |
| **Evidence-grade** | Every figure resolves to its query, its population and its source rows |
| **Resilient at widget granularity** | One widget failing never blanks the page |
| **Professional at thirty seconds** | Consistent, dense, aligned, grouped, no raw machine values, predictable keys |

**Where the differentiation lies**, and these are the capabilities the comparison set does not carry rather than claims of doing the same things better: as-of timestamp and snapshot identity per widget; drill from any point to the source rows through the provenance path; the readiness state visible on any analytical surface; intelligence bound as a first-class analytical object; and the honest-abstain behaviour instead of a confident answer on insufficient data.

---

## 5.1.2 Page anatomy

Six regions, in logical order. Every region is optional except the grid and the selections bar.

```
+--------------------------------------------------------------------------+
| PAGE HEADER      sheet selector | as-of | edit toggle | save/reset layout |
+--------------------------------------------------------------------------+
| SELECTIONS BAR   always present. "No selections applied" when empty.      |
|                  chips: [<dimension_a>: <value> x] [<dimension_b>: <value> x] |
|                  clear-all                                                |
+--------------------------------------------------------------------------+
| ASSOCIATIVE STRIP   field columns with tri-state values (collapsible)     |
+------------------+-------------------------------------------------------+
| SHEET NAVIGATOR  |  WIDGET GRID  12 columns, responsive, drag + resize    |
| layout thumbnails|                                                        |
| (collapsible,    |  [ KPI ][ KPI ][ KPI ][ KPI ]                          |
|  inline-start)   |  [   bar chart      ][  donut  ]                       |
|                  |  [        wide table with conditional formatting     ] |
+------------------+-------------------------------------------------------+
```

**The selections bar is never hidden.** It reads "No selections applied" when empty. Selection state that has no permanent home on screen becomes a surprise, and a surprised user distrusts the numbers.

---

## 5.1.3 The associative engine

This is the deepest difference between an analysis page and a dashboard.

### The state model

Every value of every field carries one of four states at all times:

| State | Colour | Meaning | Clickable |
|---|---|---|---|
| **Selected** | Cyan Green `#2CE6A2` | The user chose it | Yes, deselects |
| **Possible** | Near-White `#EAF6FF` | Consistent with the current selection | Yes, adds |
| **Excluded** | Muted Steel `#8EA7C1`, struck | Not consistent with the current selection | **Yes** - pivots the selection to it |
| **Alternative** | Muted Steel, not struck | Same field as a selection, not chosen | Yes, adds to the selection |

**Excluded values remain clickable.** A user who clicks an excluded value is telling the product they were wrong about the previous selection, and the correct response is to pivot, not to ignore the click. This is the single behaviour that most distinguishes an associative model from a filter chain.

### Computation

```
selection S = { field -> [values] }

possible(field f) =
    SELECT DISTINCT f.value, count(*)
      FROM <associative base>
     WHERE  <every selection EXCEPT selections on f itself>
     -- excluding f's own selection is what produces "alternative" rather
     -- than collapsing f to the single value the user picked

excluded(f) = all_values(f) - possible(f)
```

The associative base is the joined canonical model reachable from the page's bound datasets, resolved by the one path resolver over the published relationship model of Chapter 3 4.5.10, along the join paths JP1 to JP6 of Chapter 3 4.5.16. **Counts are returned with the states**, because a value with two rows behind it and a value with two hundred thousand should not look identical.

### Contract

| Endpoint | Meaning |
|---|---|
| `POST /api/workspace/state` | Body: current selection. Response: per field, the selected, possible, excluded and alternative sets with counts |
| `POST /api/workspace/select` | Add, remove or pivot one value. Returns the new state plus the affected widget list |
| `POST /api/workspace/clear` | Clear all, or clear one field |

**Live toggle.** The associative strip carries a `live: on / off` control. Off stops recomputation on a very large model so an engineer can make five selections without paying for four intermediate computations, then recompute once. This is honest performance management rather than a hidden limit.

### Degradation

A field whose dimension is not in the safety registry renders `n/a` **with a tooltip naming the missing registration**, and the rest of the strip keeps working. Honest degradation, never a broken strip.

---

## 5.1.4 Filters

**Filters are authored widgets, not product furniture.** A fixed row of dropdowns assumes which columns matter, and every plant filters by different things (Chapter 1, Rule 1).

### Filter widget kinds

| Kind | Control | Use |
|---|---|---|
| **List filter** | Scrollable value list with search, tri-state colouring, multi-select | The default. Any dimension |
| **Dropdown filter** | Single or multi select | Where screen space is tight |
| **Calendar / date-range filter** | Two date pickers plus presets (today, 7d, 30d, quarter, custom) | Any time column |
| **Numeric range filter** | Dual slider plus numeric inputs | Any measure or numeric dimension |
| **Search filter** | Free text against one or more text columns | Material codes, batch identifiers |
| **Button-group filter** | Fixed small value set as segmented buttons | Where a field has three or four values |

Each is authored through the same shell in S2 mode: choose kind, choose the field from the registry, choose default state, save.

### The composition rule, stated where the choice is made

> **A widget's saved filter is that widget's permanent scope.** The page filter bar and any associative selection on another widget apply **on top of it**, narrowing further inside that scope. They combine with AND; they never compete. Leave the saved filter empty and the widget follows the page alone.

This sentence appears in the authoring panel's own hint text, because a rule the user reads three screens away from the choice is a rule they will not follow.

### Filter bar behaviour

Global filters live in a bar above the grid. Clear-all sits at the inline-end. A filter widget dropped onto the grid behaves identically but occupies grid space; this is how a page gets a filter panel down one side.

---

## 5.1.5 The widget catalogue

### Structural sets, closed (product grammar)

**Widget kinds:** Chart, Table, KPI, Calculated label, Filter, Container, Text.

**Chart types** with their capability flags, served from `chart_type_registry`:

| Chart type | Dimension | Measure | Second measure | Best for |
|---|---|---|---|---|
| **Bar** | required | required | optional | Comparison across a category |
| **Column (stacked)** | required | required | series | Composition across a category |
| **Line** | required (time) | required | series | Trend |
| **Area** | required (time) | required | series | Cumulative trend |
| **Combo (bar + line)** | required | two required | - | Volume against rate |
| **Pie** | required | required | - | Share, few categories |
| **Donut** | required | required | - | Share with a centre figure |
| **Scatter** | optional | two required | size, colour | Relationship between two measures |
| **Heatmap** | two required | one required | - | Two-dimensional intensity |
| **Pareto** | required | required | - | The vital few: descending bars plus a cumulative percentage line |
| **Box plot** | required | required | - | Distribution and outliers per category |
| **Histogram** | one measure binned | count | - | Distribution of a single measure |
| **Gauge** | none | required | - | One value against a target |
| **Waterfall** | required | required | - | Contribution to a change |
| **Table** | any | any | - | The evidence layer |
| **Pivot table** | rows and columns | required | - | Cross-tabulation |
| **KPI tile** | none | required | comparison | One number, large |

**The switcher rule.** The chart-type switcher on a widget card offers **only the types the server registry accepts for that binding**. A type absent from the switcher is correct behaviour, not a missing feature, and the switcher says so on hover: "Scatter needs two measures."

### Measures and dimensions are registry rows

Dimension and measure lists are **derived from the canonical model and the customer's own mapping**, never a compiled set. A plant that needs to group by batch, recipe, tool number or ambient humidity registers it and it appears everywhere. See Section 5.5.5.

---

## 5.1.6 Chart style standard

One visual language across every chart. No page-local schemes; enforced by the design-system ratchet.

| Element | Rule |
|---|---|
| **Series colour order** | Electric Cyan `#00D4FF`, Electric Blue `#0A84FF`, Cyan Green `#2CE6A2`, Corporate Blue `#2F80ED`, Amber `#FFB020`, Muted Steel `#8EA7C1`. Colourblind-safe, in this order, always |
| **Semantic colour** | Cyan Green good, Amber warning, Hot Red bad. A semantic colour is never reused as a series colour on the same chart |
| **Background** | Transparent on Panel Navy `#0B1730`. No chart draws its own panel |
| **Gridlines** | Industrial Blue `#102A43`, horizontal only, behind the data |
| **Axes** | Muted Steel labels; axis titles present whenever the unit is not obvious; **a truncated axis is never allowed to start above zero on a bar chart** |
| **Data labels** | Shown when fewer than fifteen points and they fit; otherwise on hover |
| **Legend** | Only when more than one series; positioned block-end; carries the field name as a title |
| **Numbers** | IBM Plex Mono, thousands separated per the installation format, unit suffixed, never more precision than the source |
| **Null versus zero** | A gap in a line is a null. A zero is a plotted point at zero. **They never render identically** |
| **Sample data** | A widget rendering emulated data carries the disclosure badge |

### The five states every chart must implement

| State | Rendering |
|---|---|
| Loading | Skeleton of the chart's own shape, never a spinner past one second |
| Populated | The chart |
| Genuinely empty | "No data in this period", with the period stated |
| Filtered to empty | **Distinct wording**: "No data matches the current selection", plus the offending selection named and a clear-it action |
| Error | Contained in the card: what failed, and Retry |

---

## 5.1.7 Page layout

**Twelve-column responsive grid.** Row height 40 pixels, gutter 16.

| Behaviour | Rule |
|---|---|
| Drag | Live displacement of neighbours; the drop target is outlined in Electric Cyan |
| Resize | From every edge and corner; the chart re-renders to fit, never clips or overflows |
| Minimum size | Per widget kind; a KPI is 2x2, a chart 3x4, a table 6x4. **A donut may never be resized into a square smaller than its legend** |
| Breakpoints | Twelve columns wide, six on tablet, one on mobile; order is preserved, sizes collapse |
| Persistence | `layout_json` per dashboard definition, saved by Save layout, restored on load |
| Reset | Reset layout returns to the definition's default |
| Frame controls | Maximise to fill the grid, collapse to the header, restore, remove |

**Edit mode is explicit.** A view-mode user cannot accidentally drag a widget. Toggling edit mode reveals drag handles, resize corners and the add-widget control.

---

## 5.1.8 KPI design

A KPI tile is the most-read object in the product and the easiest to render badly.

| Element | Rule |
|---|---|
| **Value** | Largest type on the tile, IBM Plex Mono, unit suffixed |
| **Label** | Above the value, Muted Steel, the business name, never a column name |
| **Comparison** | Optional: delta against a prior period, with direction arrow and **the period named** ("vs previous 30 days") |
| **Direction semantics** | Configured per KPI: for a defect rate, down is Cyan Green; for yield, up is. **Never assumed** |
| **Sparkline** | Optional, behind the value, single series, no axes |
| **Target** | Optional, rendered as a marker with the target value stated |
| **Drill** | Click opens the evidence: the query, the population, the rows |
| **Empty** | "No data" in the value position with the period stated. **Never a zero**, because a zero is a measurement |

---

## 5.1.9 Add page

| Step | Surface | Result |
|---|---|---|
| 1 | Page Builder, **Create page** | Name, code, audience roles |
| 2 | Grid opens empty | Empty state: "This page has no widgets yet", with Add widget inline |
| 3 | Add widgets (5.1.10) | Definitions persist as they are saved |
| 4 | Arrange, Save layout | `layout_json` written |
| 5 | Publish to audience | The page appears in navigation for its roles |

Quota is checked at step 1 against the F3 limits: at eighty percent the action warns, at one hundred percent it is disabled with the reason and the administrator named.

---

## 5.1.10 Add widget

**The kind picker is a pre-step. The shared shell is where binding happens.** They are a sequence, not alternatives.

```
Add widget
   |
   +-- 1. KIND PICKER  (tiles: Chart, Table, KPI, Calculated label, Filter, Container, Text)
   |
   +-- 2. NAME
   |
   +-- 3. THE SHARED SHELL OPENS IN S2 MODE
   |        carrying the widget's current definition already loaded
   |        binding mode: [ Catalogue | Query ]
   |
   +-- 4. PREVIEW   (renders with live data, in the widget's real size)
   |
   +-- 5. SAVE      (definition versioned; widget appears on the grid)
```

**Catalogue mode is a simplified face of the same shell, not a second surface.** Switching Catalogue to Query carries the catalogue selection in as a starting query, so nothing is lost by exploring the more powerful mode.

---

## 5.1.11 Link data to a widget

Two binding modes. Both produce the same artifact class: a versioned widget definition.

### Catalogue binding - the simple path

Chart type, then dimension, then measure, each from `GET /api/registry/metadata`. **Dimension and measure hide themselves** for a chart type whose `supportsDimension` or `supportsMeasure` is false. The form refuses only when neither is chosen.

### Query binding - the general path

**This is the real mechanism.** Dimension and measure become "which returned column plays which role", not "pick an entry from a catalogue".

```
1. Write the query            monospace editor, IBM Plex Mono, schema tree beside it
2. Run test                   POST /api/workspace/widgets/execute
3. Inspect returned columns   name, inferred type, three sample values
4. Map columns to roles       axis <- column, value <- column, series <- column
5. Preview                    the widget renders at its real size
6. Save                       expression stored on the definition
```

Things a catalogue cannot express and a query can, which is why this path exists: a customised correlation table; production per shift; consumption of one specific piece of equipment per shift; a chart of the relationship between line speed and a defect class.

**Safety.** Every authored query passes the same safe-SQL contract as the authoring shell: `SELECT` and `WITH` only, whitelisted operators, identifiers validated and quoted, values always parameter-bound, refusals recorded with their reason. One implementation, two surfaces.

**Limits, enforced server-side:** default 100 returned rows, absolute 500; default raw row limit 50,000, absolute 250,000; default lookback 90 days, absolute 730.

---

## 5.1.12 Edit widget query

**Edit opens the same shell, with the definition already loaded.** There is no separate edit path and no second door.

| Rule | Reason |
|---|---|
| The shell opens carrying the current definition | The user edits what is there rather than starting again |
| Saving creates a new version | The previous version is recoverable |
| Changing the binding **requires re-mapping the axes** if the returned columns changed | A widget whose axes silently point at gone columns is worse than an error |
| **Retitle when you repoint** | The title field is highlighted when the binding changes; a widget whose title says one thing while it plots another is worse than a broken one |
| Preview before save is mandatory for query mode | A query that returns nothing is caught by the author, not the audience |

---

## 5.1.13 Dynamic interaction

| Interaction | Behaviour |
|---|---|
| **Click a bar, slice, cell or row** | Publishes a selection on that widget's dimension; every widget on the page re-queries; the associative strip re-shades; a chip appears in the selections bar |
| **Click the same element again** | Deselects cleanly |
| **Click an excluded value** | Pivots the selection to it |
| **Lasso or brush on a scatter** | Selects the enclosed points as a value set |
| **Hover** | Tooltip with the dimension value, the measure value, the unit, and the row count behind the point |
| **Double-click a point** | Opens the drill drawer with the underlying rows |
| **Chip x in the selections bar** | Removes that one selection |
| **Clear all** | Empties the selection; the bar returns to "No selections applied" |
| **Card maximise** | Fills the grid area; Escape restores |
| **Export** | CSV of exactly what the widget currently shows, with the selection stated in the header row |

**The publish-subscribe contract.** Widgets **publish** selections and **subscribe** to the merged filter set. No widget reads another widget's state directly, and no page-private fetch exists for an analytics visual. This is enforced by an architecture test.

---

## 5.1.14 Performance envelope

| Mechanism | Effect |
|---|---|
| Latest-only polling | A slow response can never overwrite a newer one |
| Widget-level isolation | One widget's endpoint failing shows an error in that card only; the page stays interactive |
| Server row caps | Every widget query is bounded before it reaches the database |
| Aggregate pushdown | Grouping and aggregation happen in the database, never in the browser |
| Associative live toggle | Recomputation deferred on very large models by explicit user choice |
| Selection debounce | Rapid clicking coalesces into one recomputation |

**The isolation behaviour is the one a buyer notices.** A page where one broken widget takes the whole screen with it reads as fragile within seconds.

---

## 5.1.15 Acceptance

1. Every widget renders; zero widgets return no data on a populated instance.
2. Clicking a bar cross-filters **every** widget, and the associative strip re-shades.
3. Clicking an excluded chip pivots the selection to it.
4. The chart-type switcher morphs a widget and offers only compatible types.
5. Drag, resize, Save layout, reload: the layout persists.
6. Maximise and minimise respond on every widget.
7. A donut resized small remains legible.
8. Forcing one widget's endpoint to fail leaves the page interactive.
9. A widget authored by query renders from its result columns and survives reload.
10. A filtered-to-empty state is worded differently from a genuinely-empty one.
11. Export reflects the current selection.
12. Every figure drills to its population.

---


---


## 5.1.16 Master items, bookmarks and saved views

Reusable objects are definitions in the one store (Ch3 4.5.11), not page-local settings.

| Object | Kind | Behaviour |
|---|---|---|
| **Master dimension** | `master_dimension` | A registry dimension plus a display name, format, default sort and drill hierarchy. Changing it propagates to every consumer, after an impact preview |
| **Master measure** | `master_measure` | Expression, aggregation, unit, format, direction of goodness. Same propagation rule |
| **Master filter** | `filter` | A named filter configuration insertable on any page |
| **Saved query** | `saved_query` | An authored query reusable as a widget source and as an analysis input |
| **Hierarchy** | `hierarchy` | Ordered dimension codes; drives drill-down everywhere |
| **Bookmark** | `bookmark` | Selection state plus page state plus as-of time; private, shared-with-role, or published |
| **Saved view** | `bookmark` with `is_default` | A bookmark a user or role opens the page in by default |

**Impact preview is mandatory before saving a master item change.** `GET /api/definitions/{id}/impact` names every page, widget, analysis and model that consumes it, and the count of each. A change touching more than a configured number of consumers additionally requires confirmation.

**Bookmark contract.** A bookmark stores field-value selections by **registry code, never by row identifier**, so it survives a re-projection. A bookmark whose field no longer exists opens with that selection dropped and a note naming it, rather than failing.

## 5.1.17 The associative strip at enterprise scale

A plant with hundreds of registered fields cannot use a flat strip. The design scales in seven ways.

| Mechanism | Behaviour |
|---|---|
| **Search** | Field search across name, business name and dictionary definition, with the matched term highlighted |
| **Pinned fields** | A user pins the fields they work with; pinned fields occupy the first column and persist per user per page |
| **Recently used** | The last used fields form a second group, so a returning user finds their working set |
| **Grouping** | Fields grouped by dataset or subject area, taken from the registry, collapsed by default beyond the pinned and recent groups |
| **Virtualised values** | Value lists render a window with a scroll-loaded remainder; the strip never materialises a hundred thousand values |
| **High-cardinality behaviour** | Above a configured distinct count a field switches from a value list to **search-only** mode with an approximate count badge, stated as approximate. The field remains selectable; it stops trying to enumerate |
| **Field statistics** | Distinct count, null rate, and possible-versus-total under the current selection, shown on hover, so a user can see why a field is worth using |

**Layouts.** Horizontal strip above the grid (default), vertical panel at the inline-start (for many fields), and a collapsed summary bar on small screens that opens as a sheet. The chosen layout is per user per page.

**Bookmarks integrate.** The strip's pinned set, group collapse state and layout are part of a saved view.

## 5.1.18 Best-chart propagation, end to end

The recommendation is a contract, not a hint, and it has five parts.

| Part | Specification |
|---|---|
| **1. Metadata field** | Every analytical result schema carries `recommendedChart` and `allowedCharts[]`, plus `chartReason` as a short sentence |
| **2. Who writes it** | The block that produced the result. Each statistical and model block in 5.5 and 5.6 declares its best chart and its alternatives; the engine copies the declaration onto the result schema |
| **3. How the result carries it** | `output_schema` on the definition version, and the query response envelope `{ columns, rows, warnings, recommendedChart, allowedCharts, chartReason }` |
| **4. How the switcher reads it** | The widget chart switcher defaults to `recommendedChart`, offers `allowedCharts`, and shows `chartReason` on hover - "two measures, no dimension: scatter" |
| **5. Fallback and override** | If the recommended chart is unavailable for the tier or the binding, the switcher falls back to the first allowed chart **and says why**. A user override is stored on the widget definition and is never silently reverted. Selecting a chart outside `allowedCharts` is permitted but carries a persistent warning on the card: "This chart may misrepresent this result", with the reason |

## 5.1.19 Intelligence widgets

Per Ch2 3.18 and Ch3 4.5.13, intelligence is a bindable source with the same freedom as canonical data.

| Behaviour | Specification |
|---|---|
| **Selecting a source** | The binding panel offers canonical entities and registered intelligence sources in one list, grouped, with the source's grain shown |
| **Joining to canonical** | The compiler resolves `entity_link_column` to `link_entity` through `plant_relationship_paths`; no path means `WD07` refusal, not an empty chart |
| **Mixed widgets** | One widget may bind a canonical measure and an intelligence measure - a parameter trend with the prediction risk overlaid |
| **Filtering** | Every registry dimension reachable from the intelligence source is offered as a filter, including canonical dimensions reached through the link |
| **Comparison** | Period, line, equipment, route and context comparison work identically to canonical measures |
| **Drill-down** | Into the population, the drivers, the comparable cases |
| **Drill-through** | To the run, the method, the population and the source rows, along JP5 (Ch3 4.5.16) |
| **Genealogy link** | Any intelligence row linked to a material unit offers "open in genealogy" |
| **Report inclusion** | Intelligence widgets appear in scheduled reports like any other |
| **Assistant explanation** | The dock explains any intelligence widget in place, with citations |
| **Outcome tracking** | A prediction or suggestion widget offers its decision and evaluation state as columns and as filters |
| **Read-only** | Intelligence sources are never writable from a widget; a decision is taken on D6 or D9, never inline in a chart |
| **Entitlement** | A source below the user's tier or role is **absent from the list**, not offered and refused |

---



## 5.1.20 Customer-data genericity and runtime registry authority

A page may display a customer dimension only if it was published through the canonical definition/registry authority. **No compiled array or switch statement is a customer dimension registry.** Adding a new customer dimension, measure, subject kind or grain requires metadata/definition changes only; no application rebuild is acceptable.

Release acceptance runs the same binary against discrete, continuous and foreign/customer-shaped fixtures. The allowed differences are mappings, definitions, relationships, registry rows, reference profiles and configuration. A new product-code branch for an industry fails the genericity gate.

A widget result identifies its **Analysis Subject and grain**. Material identity is optional. Equipment/time-window and process-window subjects are first-class and use the same widget/evidence path.

## 5.2 THE NO-CODE / LOW-CODE AUTHORING SHELL (PPIQ.txt 5.2)

Features, functionality, layout, UI/UX, the schema table bar, the toolbox drag-and-drop design, the wiring diagram with debugging, saving and transfer to code, joining and using the result in data analysis, the SQL editor with debugging, running, saving and use, and the predefined and advanced toolboxes.

---

## 5.2.1 The ruling: one shell, five purposes

There is **one authoring shell**. Not five surfaces that resemble each other: one surface whose left panel, palette, validator and board semantics are parameterised by which purpose it was opened for.

**A user who learns the shell once has learned every authoring act in the product.**

| Purpose | What is authored | Palette presented | Output artifact |
|---|---|---|---|
| **S1 Data preparation** | Staged data filtered, joined, aliased and mapped into the plant schema | Relational transform blocks | Transformation Definition |
| **S2 Widget and page binding** | The dataset a widget displays | Relational plus aggregation blocks | Widget definition |
| **S3 Analysis authoring** | Correlation, statistics, mathematics | Statistical and correlation method blocks | Analysis Definition |
| **S4 Model authoring** | Model-based analyses over the same canonical data | Model and feature blocks | Model Definition |
| **S5 Plant data log** | Rules emitting info, warning and error entries | Condition and action blocks | Rule Definition |

Every output is **a named, versioned definition in the unified definition store** of Chapter 3 4.5.11: an identity row in `definition_store`, an immutable version row in `definition_versions`, and where the kind requires it a one-to-one detail row. Same lifecycle, same permissions model, same dependency graph, same export format.

**The store is the source of truth, not a file.** Export produces a portable artifact for audit, transfer between installations and backup (Chapter 3 4.5.11, `definition_export_artifacts`); import validates that artifact against the receiving instance's schema and registry before it is accepted. **A definition is never edited as a file and a file is never authoritative.**

---

## 5.2.2 The two modes

A toggle sits at the block-start of every one of the five purposes. Always present, always exactly two modes.

| Mode | Intended user | Rationale |
|---|---|---|
| **Block and wiring diagram** | A plant user with no software experience | The authoring act must be achievable without writing a line of anything |
| **SQL** | A user with some database experience | The long tail a block palette will never cover must not become a support ticket |

**Neither mode is a lesser citizen.** Both produce the same artifact class. SQL mode is available from the second licence tier upward and requires an authoring role; a viewer never authors SQL at any tier.

**Switching modes.** Block to SQL always succeeds: the graph compiles to SQL and the SQL is loaded into the editor. SQL to block succeeds **only when the SQL is reconstructable** into the block grammar; where it is not, the toggle states so plainly - "This statement uses constructs the block palette cannot represent. Switching will keep the SQL and discard the diagram." - and requires confirmation. **The fork is stated at the point of the switch**, not buried in a specification.

---

## 5.2.3 Layout: the four regions

```
+---------------------------------------------------------------------------+
| BLOCK-START   [ Block | SQL ]   zoom - + fit arrange | Valid flow | Run    |
+---------------+--------------------------------------------+--------------+
| INLINE-START  |  CENTRE                                     | INLINE-END   |
|               |                                             |              |
| SCHEMA TABLE  |  THE BOARD                                  | TOOLBOX      |
| BAR           |  (or the SQL editor in SQL mode)            | (hidden      |
|               |                                             |  entirely    |
| schemas       |   [staging.source_a]   --+                   |  in SQL      |
|  > tables     |                          \                  |  mode)       |
|    > columns  |   [staging.source_b]-----[Join]--[Map]      |              |
|      (typed)  |                                             | grouped,     |
|               |                          (minimap)          | searchable   |
+---------------+--------------------------------------------+--------------+
| BLOCK-END     DEBUG LOG   [ Error | Warning | Success ]     rows, cost     |
+---------------------------------------------------------------------------+
```

All four regions are present in both modes except the toolbox, which is **hidden entirely and not merely disabled** in SQL mode. A disabled palette invites clicking; an absent one does not.

---

## 5.2.4 The schema table bar (inline-start)

A three-level tree, every level unfolding.

```
SCHEMA
  > TABLE
      > ATTRIBUTE  (name, type icon, nullability)
```

| Behaviour | Rule |
|---|---|
| **Two groups on S1 only** | Staging shapes and the plant schema, because S1's whole purpose is to move data between them. S2 to S5 show the canonical model only |
| **Drag one attribute** | Drops as a column reference |
| **Drag a whole table** | Drops as a source node with every column as a typed port |
| **Multi-select then drag** | Drops the selected columns together |
| **Search** | Filters across all three levels; matches are highlighted and their ancestors auto-expanded |
| **Type icons** | Key, number, text, date, boolean - the same four port colours used on the board |
| **Row count hint** | Approximate row count per table, so an author knows the cost of what they are dragging |
| **Never a hidden schema** | No emulated-source schema is ever visible on a customer instance |

**Why the tree matters more in SQL mode than in block mode:** a SQL author needs the column names and types constantly, which is why the tree is unchanged when the palette disappears.

---

## 5.2.5 The toolbox (inline-end)

Grouped, searchable, drag-and-drop onto the board. **Groups are extended by registry entry, never by a code branch.**

### Group 1 - Source and output

| Block | Inputs | Outputs |
|---|---|---|
| Source table | - | dataset |
| Output to canonical entity | dataset | - |
| Output to named dataset | dataset | - |

### Group 2 - Relational

| Block | Inputs | Outputs | Configuration |
|---|---|---|---|
| **Join** | two datasets | dataset | type (inner, left, right, full), key pairs from live schema |
| **Filter** | dataset | dataset | expression editor (double-click) |
| **Select columns** | dataset | dataset | column checklist |
| **Rename / alias** | dataset | dataset | name pairs |
| **Group by** | dataset | dataset | group keys, aggregate list |
| **Sort** | dataset | dataset | column, direction |
| **Union** | two datasets | dataset | column alignment |
| **Distinct** | dataset | dataset | - |
| **Limit** | dataset | dataset | n |
| **Pivot / unpivot** | dataset | dataset | key column, value column |
| **Derived column** | dataset | dataset | expression editor |
| **Cast** | dataset | dataset | column, target type |
| **Lookup** | dataset + dataset | dataset | key, returned columns |

### Group 3 - Arithmetic, comparison and logic

**These are expression blocks, not board blocks.** They live inside the block they configure, opened by double-click, on all five surfaces without exception (Chapter 1, ruling 2).

`+ - * /`, `= <> > >= < <=`, `AND OR NOT`, `IF / ELSE`, `IS NULL`, `LIKE`, `IN`, `BETWEEN`, `COALESCE`, `ROUND`, `ABS`, date arithmetic.

### Group 4 - Statistics and correlation (S3)

The method toolbox, registry-driven. Catalogued with inputs, outputs, validation and best chart in section 5.5.

### Group 5 - Model and feature (S4)

Feature assembly, split, train, score, evaluate. Catalogued in Part 4.

### Group 6 - Condition and action (S5)

Threshold condition, range condition, routing-deviation condition, emit info, emit warning, emit error.

### Control flow

`FOR` and `WHILE` are **orchestration and belong to neither board.** A saved definition describes what its output is; how often it runs and over what window belongs to the job that carries it. A transform graph is declarative.

---

## 5.2.6 The board: node and port model

### Node

| Element | Rule |
|---|---|
| Title | The user's name for this step, editable inline |
| Subtitle | The block type |
| **Status badge** | Cyan Green OK, Amber warning, Hot Red error - **on the node itself**, not only in a problems list |
| Ports | Typed, coloured, labelled; required inputs marked |
| Row estimate | Shown after a dry-run |
| Inspector | Opens inline-end on selection, with **typed controls fed from live schema** - a join key is a dropdown of real columns, never free text |

### Port types and colours

| Type | Colour |
|---|---|
| Key | `#00D4FF` |
| Number | `#0A84FF` |
| Text | `#7AA7C7` |
| Date | `#B48CFF` |
| Dataset | Electric Cyan outline |

**The colour communicates the type and the type enforces legality.** A coloured port that accepts any wire is a lie told by the interface, and it is the specific defect this specification exists to prevent.

### Canvas toolbar

Zoom in, zoom out, **Zoom fit**, **Arrange** (automatic layout), minimap, undo, redo. A **global validity indicator** sits beside Run: `Valid flow` in Cyan Green or `Invalid` in Hot Red, always visible, so the author never has to hunt for whether the graph can run.

---

## 5.2.7 Illegal wiring is refused at drag time

**This is the clause that separates a professional tool from a toy.**

A wire that is not legal is **rejected at drag time with a stated reason in the debug log**. It is never silently accepted and never allowed to fail later at run time.

### The illegal set, enumerated

**Universal, all five surfaces:**

| Illegal | Message |
|---|---|
| Dataset into a value input, or the reverse | "A wire from `<node>.<port>` carries rows; `<target>.<port>` expects a single value." |
| Type mismatch | "`<column>` is text; `<block>` expects a number." |
| Cycle | "This wire would create a loop: `<node A>` -> `<node B>` -> `<node A>`." |
| Required input unconnected at Run | "`<block>` cannot run: input `<port>` is not connected." |
| Aggregate outside an aggregation context | "`<function>` needs a Group by block above it." |
| Column not present in the upstream dataset | "`<column>` is not in the output of `<upstream node>`." |

**S1 additionally:**

| Illegal | Message |
|---|---|
| **Two tables compared with no join declared between them** | "`<table A>` and `<table B>` have no join. Add a Join block and declare the key pair before comparing their columns." |

That last one is **the mistake a plant engineer will actually make**, and it is the reason the rule is enumerated per surface rather than left general.

### Behaviour

The wire **does not land**. The source port pulses Hot Red for 400 ms, the debug log gains an Error row with the sentence, and focus moves to that row. **No modal.** An author making twenty wires must not be interrupted twenty times.

---

## 5.2.8 The debug log: debugging the wiring diagram

Always present at the block-end. Three severities, each with a written description.

| Severity | Meaning | Must state |
|---|---|---|
| **Error** | The definition cannot run | Which block or wire, what rule was broken, **and what would fix it** |
| **Warning** | It will run but the result may surprise | What the risk is, in plain language |
| **Success** | It ran | Rows returned, columns returned, **and the cost estimate** |

**A bare red outline with no sentence beside it is a failure of this specification.**

### Warnings worth having

- "This join produces more rows than either input. Check the key pair."
- "No watermark on this source; every run will read the whole table."
- "This filter removes 98 percent of rows. Intended?"
- "Two columns named `code` after this join. One has been aliased to `code_1`."
- "Estimated cost: 4.2 million rows scanned."

### Log behaviour

Each row carries a timestamp, a severity, a sentence and a **link to the offending node**, which selects and centres it. Filterable by severity. Clearable. **Persisted with the definition version**, so a colleague opening the definition tomorrow sees the last run's outcome.

---

## 5.2.9 Run, dry-run and preview

| Action | Scope | Result |
|---|---|---|
| **Preview node** | The selected node | Sample rows in the inspector, with the row estimate |
| **Dry-run** | The whole graph, bounded | Sample rows per node, no write, cost estimate in the log |
| **Run** | The whole graph | The real execution; on S1 it projects to canonical, on S2 it returns the widget dataset, on S3 and S4 it submits a governed job |
| **Compiled SQL** | The whole graph | The exact statement that will run |

**Run is disabled while the validity indicator reads Invalid.** The author never gets to start something the validator already knows will fail.

**Progress streams.** A run reports rows per node as it goes, never a bare spinner. On completion the log states rows, columns and cost.

---

## 5.2.10 Transfer to code: the compiled SQL view

**Every block graph compiles to SQL, and the author may always see it.**

| Property | Rule |
|---|---|
| Read-only | The compiled view is not editable. Editing happens in the graph or in SQL mode |
| Exact | It is the statement that will run, not an approximation |
| Formatted | Indented, keyword-cased, with a comment per source node naming its block |
| Copyable | One click copies it, so a DBA can run it against their own instance to verify |

**Why this matters commercially.** A plant DBA asked to trust a visual tool will ask what it actually executes. Being able to show them, immediately, converts the deepest objection into a demonstration. It is also how an author learns SQL from the block palette, which is a genuine adoption path.

---

## 5.2.11 Joining, and using the result in data analysis

**The join is declared once, in S1, and never again.** This is the property the whole product rests on.

### On S1

The author drags two staged tables, drops a **Join** block, and declares the key pair from typed dropdowns fed by live schema. The join may be composite, and where the customer's identifiers differ across sources the **business key dictionary** resolves them: the definition records which source field plays which member role, and the projector writes the resolution into `material_aliases`.

The edge on the board is labelled with the equality, for example `<source_a>.<key_column> = <source_b>.<key_column>`, so the declaration is readable at a glance. **Every identifier in this chapter's examples is a placeholder**; the product ships no table, column, defect, grade or parameter name.

### Downstream

Because the relationships already exist in the canonical model, **S2 to S5 operate on one prepared dataset with a single row context.** A comparison between two columns has exactly one meaning: within the same row of the prepared model. This is why expression blocks live inside board blocks on every surface - one board grammar, one validator, one error taxonomy.

### Reuse

A saved definition is selectable as a **source** in another definition. An analysis in S3 selects the prepared dataset a Transformation Definition produced; a widget in S2 selects the result of an analysis. **Definitions compose; they are not islands.**

---

## 5.2.12 SQL mode

### Layout

| Region | In SQL mode |
|---|---|
| Toolbox | **Hidden entirely** |
| Schema table bar | **Unchanged** - a SQL author needs it more than a block author does |
| Board | Becomes the SQL editor |
| Debug log | Unchanged - error, warning, success, each with a description, now describing the SQL |

### The editor

| Feature | Rule |
|---|---|
| Font | IBM Plex Mono |
| Line numbers | Always |
| Syntax highlighting | By token class |
| Autocomplete | From the live schema tree: schema, table, column, with types |
| Statement folding | For long statements |
| Format | One action, deterministic |
| Comment and uncomment | Keyboard |
| Multiple statements | Rejected with the reason; one statement per definition |

### Run and inspect

Run executes under the safe-SQL contract, bounded by the row limits. The result renders **below the editor** as a table with column names and inferred types, plus the row count and the elapsed time. If correct, **Save** creates a new immutable version of the definition in the store.

### The safe-SQL contract

| Rule | Detail |
|---|---|
| Statements allowed | `SELECT` and `WITH` only |
| Forbidden constructs | Every DDL and DML verb; `COPY`; large-object functions; database-link functions; sleep functions; catalog and information-schema access; extended stored procedures |
| Token boundaries | Matching is on token boundaries, so a legitimate column named `created_at` is never rejected for containing a forbidden substring |
| Comments | Stripped before validation, so a forbidden token cannot hide in a comment and a guard cannot be satisfied by one |
| Identifiers | Validated against the allowlist of registered tables and columns, then quoted |
| Values | **Always bound as parameters**, never concatenated |
| Refusal | A **first-class status**, persisted with its reason, not an exception |

### Error rendering

The house standard, and it is stricter than most editors:

```
Error   Invalid expression.

        The error occurred here:
        > SELECT unit_id, avg(param_a) FROM source_a WHERE param_a >
        >                                                       ^
        A comparison needs a value on the right-hand side.
```

**Message, then an echo of the offending fragment, then the offending token highlighted in place in the editor.** A bare "syntax error near line 3" is not acceptable output from a product that sells its willingness to say no.

---

## 5.2.13 Saving, versioning and transfer

| Property | Rule |
|---|---|
| Save | Creates or updates a **draft** version |
| Validate | Moves draft to **validated** |
| Publish | Freezes an **immutable** version with a version number and a publisher |
| Edit a published version | Forks the next draft; the published one is never mutated |
| Rollback | A pointer to a prior version; the rolled-back one remains inspectable |
| Drift | A source change that breaks a definition moves it to **paused by drift** with the changed column named |
| Lock | A published version renders locked with an explicit unlock and **a stated reason** |
| Export | The definition as a file: graph or SQL, plus declared keys, aliases and metadata |
| Import | The same file into another instance, validated against that instance's schema before it is accepted |

**Export and import exist for the second engineer.** A definition that only one person can read is a single point of failure; a definition that can be handed to a colleague, diffed and audited is an asset.

---

## 5.2.14 Predefined and advanced toolboxes

| Toolbox | Contents | Who uses it |
|---|---|---|
| **Predefined** | The relational group, the condition group, and templates: "map a source table to material units", "join two sources on a business key", "threshold rule on a parameter" | A plant user in the first week |
| **Advanced** | Window functions, recursive genealogy traversal, custom aggregate expressions, multi-key composite joins, statistical and model blocks | An engineer once fluent |

The advanced set is **collapsed by default** and expands with one click. It is not hidden behind a tier, because hiding capability behind a price is what makes SQL a support ticket.

**Templates are not content.** A template is a shape with no plant vocabulary in it; it names the roles a user must fill, and it ships only if it passes the genericity lint.

---

## 5.2.15 Acceptance

1. Two blocks that must not connect refuse **and write a sentence** naming the rule.
2. A node output wired into its own input is refused and the cycle is named.
3. Running with a required input unconnected is refused before execution, naming the input.
4. Two tables compared with no join between them is refused with the join named as the fix.
5. The tree unfolds three levels and column types are visible.
6. No emulated-source schema is visible anywhere.
7. Dragging one column and dragging a whole table both work.
8. Delete edge and duplicate node both work, or both are absent.
9. Zoom out then fit keeps the graph legible and the minimap tracking.
10. Publish then reopen returns the graph intact with a version number.
11. The mode toggle is present, and switching SQL to block states the fork before discarding anything.
12. Compiled SQL matches what runs.
13. A forbidden statement is refused with the fragment echoed and the token highlighted.
14. Export then import into a second instance reproduces the definition.

---


---

## 5.2.16 The expression editor

One editor, used by Filter, Derived Column, Calculated Label, Calculated Measure, Condition, Attribution and every other block that needs a value. Opened by double-clicking the block it configures.

**Layout.** A modal panel over the board: block-start the target and its expected return type; centre the editor; inline-end the function and field browser; block-end the validation line, the test result and the actions.

| Feature | Specification |
|---|---|
| **Syntax highlighting** | Tokens coloured by class: field, function, literal, operator, error |
| **Autocomplete** | Triggered on typing and on `.`; sources are the prepared dataset's live columns and the safe-function whitelist. Each entry shows its **type** and, for a field, its unit and its dictionary definition |
| **Type information** | The inferred return type of the expression is shown continuously and compared against the target's expected type; a mismatch is flagged before the user leaves the field |
| **Function documentation** | Selecting a function shows its signature, its arguments with types, its null behaviour, its unit behaviour and one example |
| **Inline validation** | Debounced, on every keystroke pause. Errors render in the validation line with **the position**, and the offending token is underlined in place |
| **Error position** | Reported as line and column and highlighted; the three-part rendering of 5.2.12 applies - message, echoed fragment, marker |
| **Parenthesis matching** | Matching pair highlighted; an unbalanced expression is a distinct error class |
| **Format expression** | One deterministic action; never changes semantics |
| **Test expression** | Runs against a bounded sample of the prepared dataset and shows the first rows of the result **with the input values beside them**, so the author sees why |
| **Sample result** | Includes a null row and a boundary row where the sample contains them, because null behaviour is where expressions actually fail |
| **Null behaviour** | Declared per function and shown; the editor warns where an expression will propagate null into a required target |
| **Unit behaviour** | Units are carried through arithmetic where declared; adding two different units is an error, not a silent number |
| **Safe-function whitelist** | The palette is the whitelist. There is no way to type a function that is not in it; an unknown identifier is an error naming the nearest match |
| **Keyboard shortcuts** | Run test, format, accept, cancel, next error, toggle browser - all documented in the panel |
| **Undo and redo** | Local to the editor, and the whole edit is one undo step on the board when accepted |
| **Accessible errors** | The validation line is an alert region; the error count and the first message are announced; the marker has a text equivalent |

**States.** Empty, valid, invalid with position, warning (runs but may surprise), tested with result, refused by the whitelist.

## 5.2.17 Per-block property inspectors

**One generic form for every block is a failure of this specification.** Each block type has its own inspector layout, fields, validation, preview and help. Fourteen are specified; the pattern extends by registry entry.

| Block | Inspector fields | Validation | Preview | Help |
|---|---|---|---|---|
| **Source** | Schema, table (typed pickers), row estimate, column checklist with types, sample toggle | Table must be registered; at least one column | First 20 rows | What a staged table is, and why the tree shows two groups on S1 |
| **Join** | Type (inner/left/right/full), **ordered key-pair rows** with add and reorder, grain on both sides, cardinality (declared, then observed after preview), **attribution rule when grain converts**, weight expression | `TR08` incomplete or unordered members; `TR09` conversion without attribution; `TR10` weights cannot sum to one; observed cardinality contradicting declared is a warning | Row counts in, out and expansion factor; **a fan-out warning when output exceeds either input** | Why the join is declared once and what a preferred path means |
| **Filter** | Expression (opens 5.2.16), null handling, case sensitivity | Return type must be boolean | Rows in, rows out, percentage removed; a warning above 95 percent removal | Why a filter here is not the same as a page filter |
| **Select columns** | Column checklist, rename pairs, output order | At least one column; no duplicate output name | Output schema | - |
| **Group by** | Group keys (multi), aggregate rows: function, column, alias | An aggregate over a grouped key; a non-aggregated column not in the keys | Group count and the largest group size | Why an aggregate outside a group is refused |
| **Derived column** | Name, expression (opens 5.2.16), declared type, unit | Name collision; type mismatch; unit mismatch inside arithmetic | The column beside its inputs | Null propagation |
| **Sort / Limit** | Column, direction, n | n within the absolute cap | - | Why a limit is not a filter |
| **Union** | Second input, column alignment map | Arity and type compatibility per aligned pair | Combined count | - |
| **Output to canonical** | Target entity (typed picker), field map rows, `const:` literals, provenance columns (read-only, shown) | Required target field unmapped; type mismatch; the fifteen `PV` classes previewed | **The projected error profile from the pre-flight sample**, grouped by code | What quarantine will do with a bad row |
| **Statistical method** | Method, outcome, factor set, window, stratification dimensions, method-auto toggle | `ST01` to `ST06`; sample-size precondition checked before run | Population size, and the readiness verdict inline | What the discipline chain will apply, and that it cannot be switched off |
| **Model** | Algorithm, feature set version, split strategy, missing-value policy, scaling, hyperparameters, acceptance floor | `ML01` to `ML07`; a random split on time-ordered data warns | Train and validation row counts, and the overlap count which must be zero | Why time-based splitting is the default |
| **Scoring** | Model version, scope rule, batch size, latency budget, trigger kind | `PD01` schema mismatch with counts; `PD02` no active model | Units in scope, and an estimated duration range | Why a schema mismatch refuses rather than coerces |
| **Prediction** | Outcome, horizon stage, risk banding thresholds, confidence display | Horizon stage must exist on the route; banding must be monotonic | Distribution of scores on a sample | What a horizon means and what it does not promise |
| **Remediation** | Comparable-condition rule, candidate practice set, **minimum support**, expected-effect measure, limitations text | Minimum support below the floor is refused; a proposed stage after the horizon is refused | Candidate count, and how many pass support | Why support of twenty is the floor |
| **Alert condition** | Parameter, comparator, limit or limit source (specification or operating limit), severity, message template with tokens | Comparator outside the set; a template token that does not resolve | The message rendered with sample values | Why a rule reads a limit rather than embedding one |
| **Action** | Action kind (log, notify, route), channel, recipients, dedup window | `AR01` no recipient | Rendered notification | Why the product never writes to the plant |

**Common to every inspector.** A title naming the block and its type; a validity badge; a help disclosure; **Preview** which never mutates; and a Reset that returns to the last saved configuration.

## 5.2.18 Undo, redo and dirty state

| Concern | Specification |
|---|---|
| **What enters history** | Node add, node delete, node move, edge add, edge delete, inspector field change (coalesced per field per focus session), expression accept, paste, arrange, and bulk delete |
| **What does not** | Selection, zoom, pan, panel collapse, log filter - view state is not history |
| **Operation boundaries** | One user gesture is one undo step. Dragging a node is one step, not one per pixel. An expression edit accepted in 5.2.16 is one step. **Arrange is one step and is fully reversible** |
| **Maximum history** | 100 steps per editing session, per definition, held client-side with the current draft |
| **Keyboard** | Undo and redo on the platform-standard shortcuts, plus a visible pair of toolbar buttons with tooltips showing what will be undone |
| **Undo after save** | Permitted. Save persists a draft; undo continues past the save point and the draft becomes dirty again |
| **Undo after publish** | **Not permitted across the publish boundary.** A published version is immutable; the history is cleared at publish and the reversal path is **Rollback**, which creates a new draft from the prior version. The toolbar states this rather than silently disabling |
| **What cannot be undone** | Publish, rollback, export, import, delete of a published definition, and any server-side run |
| **Restoration completeness** | An undo restores node positions, edge set, inspector configuration and expression text together, because a partially restored graph is worse than no undo |
| **Dirty state** | A dot on the definition title, a "Save" affordance in the primary position, and a navigation guard that names the unsaved change. Autosave writes the draft every 30 seconds and on blur; autosave never changes the status beyond `draft` |

## 5.2.19 Concurrency control

**Safe concurrency is the requirement. Live co-editing is an Enterprise enhancement, not the baseline.**

| Mechanism | Specification |
|---|---|
| **Optimistic concurrency** | Every draft save sends the version token it read. A mismatch returns `409` with the current token and the changed fields |
| **Edit session identity** | A save carries a session identifier, so two tabs of the same user are distinguished and the second is warned rather than silently overwriting |
| **Advisory lock** | On opening a definition for editing, an advisory lock is taken with a short lease renewed by activity. It **does not block** another engineer; it drives the presence indicator and the stale-save message |
| **Stale-save refusal** | Refused with `what changed`, `who changed it` and `when`, plus three actions: **Compare changes**, **Fork as new draft**, **Discard mine and reload** |
| **Compare changes** | A structural diff of the graph and a text diff of the compiled statement, per 5.2.20 |
| **Fork as new draft** | Creates a sibling draft from the user's own state, preserving both, with the fork recorded in the audit |
| **Conflict resolution** | Manual and explicit. **There is no automatic merge of a graph**, because a silently merged dataflow graph can be semantically wrong while structurally valid |
| **Presence indicator** | The other engineer's name and last-activity time in the header while their lease is live |
| **Audit history** | Every save, fork, publish, rollback and discard is an audit entry naming the actor and the session |
| **Enterprise enhancement** | Real-time co-editing with operational transforms is classified as an Enterprise enhancement and is out of scope for the baseline, which is safe refusal plus explicit resolution |

## 5.2.20 Compiled-SQL and version diff

| Feature | Specification |
|---|---|
| **Previous versus current** | Any two versions of a definition, defaulting to the current draft against the latest published |
| **Views** | Side-by-side and unified, switchable; the compiled statement is the diff subject, with the graph diff shown alongside |
| **Annotations** | Changed regions are annotated by **semantic class**: source added or removed, join changed, filter changed, output columns changed, aggregation changed, sort or limit changed. A whitespace-only or formatting-only difference is collapsed and labelled as such |
| **Estimated impact** | Row-scan estimate before and after, and the estimated cost delta from 5.3.8 |
| **Dependency impact** | The consumers from `definition_dependencies`, counted by kind, with the ones whose `output_schema` compatibility breaks listed first |
| **Approval before publishing a high-impact change** | A change is high-impact when it removes or retypes an output column, retires a relationship, or affects more than a configured number of consumers. Publishing then requires a **second approver** with a recorded justification, and the approval is an audit entry. The threshold is a governed setting |
| **Export** | The diff exports as a reviewable artifact for a change record |

## 5.2.21 First-run guidance

| Element | Specification |
|---|---|
| **Empty-shell explanation** | An overlay on a genuinely empty board: what the three regions are, what a definition is, and what publishing does. Dismissible, and re-openable from the help affordance |
| **Guided first definition** | An opt-in five-step guide: pick a staged table, pick a target entity, map the required fields, preview, publish. It **drives the real surface** rather than a simulation, and can be abandoned at any step leaving a valid draft |
| **Context-sensitive checklist** | A persistent panel listing what remains before this definition can publish, derived from the validator, each item linking to the node that needs attention |
| **Example structure without plant content** | Templates are **shapes with role slots and no plant vocabulary** - "map a source table to material units", "join two sources on a business key", "threshold rule on a parameter". Each ships only if it passes the genericity lint |
| **"Why is Run disabled?"** | The disabled Run carries a hover and a click-through that lists every blocking diagnostic with its node link. **A disabled control that will not say why is a defect** |
| **Link to live schema** | Every guidance step links to the actual tree node it refers to, so guidance and reality cannot diverge |
| **Safe sample preview** | Preview is always bounded and read-only, and says so, so a new user cannot fear breaking production by looking |
| **Progress through the lifecycle** | A visible draft, validated, published indicator with the current state highlighted and the next action named |

## 5.2.22 Keyboard authoring

Full keyboard operation is a requirement, not an accessibility footnote: a fluent author is faster on the keyboard.

| Act | Interaction |
|---|---|
| **Add a node** | Open the palette with a shortcut, type to filter, Enter to place at the focus point |
| **Move between nodes** | Arrow keys move focus in graph order; Tab moves to the next node in topological order |
| **Open ports** | Enter on a focused node enters port mode; arrows move between ports; the port's type and name are announced |
| **Create a wire** | In port mode, mark the source port, move focus to the target node and port, and confirm. An illegal target is announced with the rule before the confirmation is possible |
| **Select source and target ports** | Explicit two-step marking, so a wire is never created by an accidental keypress |
| **Delete a wire** | Focus the edge from either endpoint and delete; the edge is announced with both endpoints first |
| **Open the inspector** | A shortcut on the focused node; focus moves into the first field; Escape returns to the node |
| **Run validation** | A shortcut; the diagnostic count is announced and focus can be moved to the first diagnostic |
| **Undo and redo** | Platform-standard, with the undone action announced |
| **Zoom and fit** | Shortcuts for in, out, fit and reset; zoom never traps keyboard focus |
| **Screen-reader equivalent of the graph** | A parallel **nested-list rendering** of the graph: each node with its type, status, inputs and outputs named, navigable as a tree. It is generated from the same model, so it cannot drift from the visual |

## 5.3 and 5.4 - CONCURRENCY, LOAD BALANCING, GATE AND ENGINE

---

## 5.3 MULTI-THREADING AND LOAD BALANCING OF JOBS

## 5.3.1 The problem, stated exactly

> Hundreds of jobs running every two or three minutes, each capable of touching ten million rows. **Incremental import solves the import job only. Every other job class remains large.**

This is the correct statement of the problem and it deserves the arithmetic.

**A plant at the Medium capacity class.** 200 job definitions. Cadence spread between 2 minutes and daily. Say 80 of them run on a 3-minute cadence.

```
80 jobs / 3 min  =  26.7 job starts per minute  =  38,400 starts per day
```

If each start scans even 100,000 rows, that is **3.84 billion rows scanned per day**. If each start opens one connection and holds it for 20 seconds, the steady-state connection demand is `26.7 x 20 / 60 = 8.9` connections just for job starts, before a single user opens a page. If ten jobs happen to align on the same minute - and unmanaged cron-style schedules **always** align, because humans choose round numbers - the instantaneous demand is ten heavy queries at once on a machine sized for two.

**Three failure modes follow, and they are the ones that actually take a server down:**

1. **Thundering herd.** Every job whose cadence divides the hour fires at `:00`. The database sees a spike an order of magnitude above the mean.
2. **Connection exhaustion.** Each concurrent job holds a connection; PostgreSQL's process-per-connection model degrades badly past a few dozen, and the interactive read path starves behind the batch path.
3. **Run pile-up.** A 3-minute job that takes 5 minutes is re-triggered before it finishes. Runs accumulate, each slower than the last, until the machine stops.

**Incremental import is the right first answer and it is not sufficient.** It reduces the *import* class from full-table to delta. It does nothing for feature refresh, correlation scans, model scoring, alert evaluation or report generation, all of which read the accumulated canonical model rather than the delta.

## 5.3.2 The defence stack

Nine mechanisms, layered. Each one alone is insufficient; together they make the load bounded by construction rather than by luck.

| # | Mechanism | Stops |
|---|---|---|
| 1 | Incremental acquisition | Import reading the whole source every cycle |
| 2 | Schedule jitter and coalescing | The thundering herd |
| 3 | Skip-if-running and latest-only | Run pile-up |
| 4 | Admission control with weighted pools | Unbounded concurrency |
| 5 | Per-source load budget | The customer's production database being hammered |
| 6 | Incremental feature refresh | Every analysis rescanning history |
| 7 | Partitioning and retention | Scans growing without limit |
| 8 | Statement timeouts and circuit breakers | One pathological query holding everything |
| 9 | Backpressure and the degradation ladder | Silent collapse under overload |

### Mechanism 1 - Incremental acquisition

Per dataset: a watermark column, a stored cursor, and a read of `WHERE watermark > :last AND watermark <= :now` bounded by the row cap. A batch that hits the cap **advances the cursor to the last row it actually read** and reports itself as partial, so the next cycle continues rather than restarting.

Where a source has no usable watermark, the dataset is marked full-scan and its **minimum cadence is forced to daily**, because a full scan on a 3-minute cadence is not a configuration a plant should be able to create by accident.

### Mechanism 2 - Schedule jitter and coalescing

**Jitter.** Every job's next run time is `base + hash(job_id) mod cadence`. A 3-minute job does not fire at `:00, :03, :06`; it fires at its own offset. **This one line removes the herd.**

**Coalescing.** Two jobs with the same class, the same target and overlapping windows queued within the same tick are merged into one run covering the union. Feature refresh for the same outcome requested by three analyses becomes one refresh.

### Mechanism 3 - Skip-if-running and latest-only

| Policy | Behaviour | Applied to |
|---|---|---|
| **Skip if running** | The tick is dropped and recorded as skipped with the reason | Import, feature refresh |
| **Latest only** | Queued duplicates collapse to the newest request | Alert evaluation, scoring |
| **Queue** | Runs accumulate in order, bounded by queue depth | Reports |
| **Reject** | Refused with a named error | User-triggered runs when the pool is saturated |

A skipped tick is **visible in the monitor with its reason**. Silent skipping is how a plant discovers three weeks later that a job never ran.

### Mechanism 4 - Admission control with weighted pools

The core of the design. **No job runs because its schedule fired. A job runs because a pool admitted it.**

```
POOL              PARALLELISM   ADMITS                          RATIONALE
import                4         import, backfill                network-bound, cheap on CPU
projection            2         canonical projection            write-heavy, contends on indexes
analysis              4         correlation, statistics         read-heavy, bounded by row caps
ml                    1         training, scoring               memory-heavy, one at a time
report                2         report generation, export       bursty, low priority
interactive        reserved     never batch                     the read path is never starved
```

**Weights and admission (C4-1).** Each job definition carries a `compute_weight` (default 1). **Admission requires two predicates, not one:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

| Quantity | Meaning | Unit |
|---|---|---|
| `max_concurrency` | How many runs may be in flight in this lane | count |
| `resource_capacity` | How much of the lane's scarce resource exists | abstract units |
| `compute_weight` | How much of that resource one run consumes | abstract units |

**One number never expresses two quantities.** The previous single-predicate form made a weight-4 training job unadmittable into a capacity of 1, because `4 <= 1` is false at every moment. **This is why the weight is edited behind a confirmation** that states the resulting utilisation, and why a configuration in which a declared job could never be admitted fails validation.

Values per lane: **B-01**.

### 5.3.2a The `ml` class resolves to three lanes (C4-2)

**The six logical job classes are unchanged.** `ml` resolves to three physical lanes:

| Lane | `max_concurrency` | `resource_capacity` | Pre-emptible | Admits |
|---|---|---|---|---|
| `ml.training` | B-01 | B-01 | **Yes** | Encoder and supervised training, calibration, SHAP batch, index build and rebuild |
| `ml.batch_scoring` | B-01 | B-01 | Yes | Scheduled scoring, backfill, rescore after activation. **Batch-class capacity** |
| **`ml.online_scoring`** | B-01 | **hard-reserved, B-02** | **No** | **`event` and `micro_batch` scoring and its required serving functions only** |

- **The online reservation is never available to `ml.training` or `ml.batch_scoring` admission**, on the same principle as the `interactive` reservation.
- **Warm models.** Artifacts for every active serving identity `(tenant_id, model_code, outcome_code, grain_code, model_version)` are resident and reference-counted, with a declared eviction policy. A newly activated model is warmed before it serves, and first-score-after-activation latency is bounded and measured.
- **`latest-only` is unchanged** and now operates inside a lane that training cannot block.

> **Reason.** `predictions.actionable_deadline_utc` and `delivery_latency_seconds NOT NULL` exist, and 5.8.8 makes actionable latency Core. A design in which a nightly training run can delay an event-triggered score **defeats a Core requirement using the product's own scheduler**.

**Pre-emption (C4-3).** `ml.training` runs are checkpointed per stage. A training run **yields at its next checkpoint** when a reserved lane needs capacity, and resumes from it. Nothing is lost except elapsed time, the correct trade against an expiring prediction. Where the runtime cannot pre-empt, the lane falls back to admission-time reservation only, **and this is recorded rather than assumed**.

**The interactive reservation is the most important line in the table.** A fixed share of the connection pool and of CPU is reserved for user-facing queries. A plant where the dashboard becomes unusable whenever the engine is busy has failed, however correct the engine is.

**Connection discipline.** All pools sit behind a connection pooler. Job workers use a separate pooler identity from the interactive path, so batch work physically cannot exhaust the connections the interface needs.

### Mechanism 5 - Per-source load budget

Enforced **before a read reaches the source**, not after:

| Control | Meaning |
|---|---|
| Max rows per read | Hard cap per statement |
| Statement timeout | Cancelled at the source |
| Requests per minute | Token bucket per connection profile |
| Approved window | Reads refused outside it, with the window named |
| Concurrent reads per source | Usually 1 or 2 |

A read that would breach the budget is **refused with the budget named**, and the refusal is logged like a result. Backfill passes through with its own cumulative rate throttle, so a three-year history load never competes with the live delta.

### Mechanism 6 - Incremental feature refresh

**The mechanism that stops analysis from being the new problem.**

The feature and outcome store is materialised, not computed per run. It refreshes **only for the grains touched by the batches that landed since the last refresh**:

```
refresh_scope = distinct material_unit_id
                FROM canonical rows
                WHERE import_batch_id IN (batches since last_refresh_watermark)
```

An analysis then reads the materialised store rather than rescanning history. **The cost of an analysis becomes proportional to what changed, not to what exists.** Without this, every correlation run at a mature plant scans years of observations, and no amount of pool tuning saves it.

### Mechanism 7 - Partitioning and retention

`parameter_observations` and the results tables are range-partitioned by time, monthly, from the Medium class upward. Consequences: a windowed analysis touches only the partitions in its window; retention is a partition drop rather than a mass delete; index maintenance stays local.

Retention is per stage and configurable: staging short, canonical long, results long, logs per channel.

### Mechanism 8 - Statement timeouts and circuit breakers

Every statement carries a timeout appropriate to its class. A job exceeding it is cancelled and recorded as failed with the timeout named - **never left to run forever holding a connection**.

A **circuit breaker** per source and per job class opens after a threshold of consecutive failures, stops admitting that class, and records why. It half-opens after a cool-down and closes on a success. This is what stops one unreachable source from consuming every import slot with retries.

A **reaper** sweeps runs that are still marked running past a maximum duration and moves them to a terminal reaped state, so no run is stuck forever.

### Mechanism 9 - Backpressure and the degradation ladder

When the system is overloaded it **degrades in a stated order**, visibly, rather than collapsing:

| Level | Trigger | Action | Visible as |
|---|---|---|---|
| 0 Normal | - | All pools at configured parallelism | - |
| 1 Elevated | Queue depth > 2x parallelism | Report and ML pools reduced | A note in the monitor |
| 2 High | Queue depth > 5x, or database load high | Non-critical cadences stretched (3 min becomes 9); coalescing aggressive | Banner: "Analysis is running behind. Cadences temporarily stretched." |
| 3 Critical | Connection or CPU saturation | Only import and interactive admitted; everything else queued | Banner naming what is deferred |
| 4 Protective | Sustained saturation | New user-triggered runs refused with a named error and an estimated wait | The refusal sentence |

**Every level is announced.** A product that quietly stops doing analysis is worse than one that says it is behind.

## 5.3.3 The capacity model

The formula that connects the drivers to the machine, and therefore to the licence envelope (Chapter 1.7.1).

```
Concurrency demand  D  =  SUM over jobs ( weight_j  x  duration_j  /  cadence_j )

Row-scan rate       R  =  SUM over jobs ( rows_scanned_j / cadence_j )

Ingest rate         I  =  SUM over datasets ( delta_rows_d / cadence_d )

Storage growth      G  =  I  x  bytes_per_row  x  retention_days
```

A configuration is admissible when `D <= sum(pool parallelism)` and `R` is within the class's scan budget. **The product computes D and R from the actual job definitions and shows them on the Jobs Administration page beside the configured parallelism**, so an administrator sees the consequence of adding a job before adding it.

**This is also the honest answer to the licence question.** The tier does not buy a number of jobs. It buys a capacity envelope: retained volume, ingest rate, a **minimum refresh interval** (the floor on cadence), weighted compute slots, and concurrent sessions. Three connections pulling a hundred tables every minute and a hundred connections pulling a thousand rows a day are different machines, and only a metered envelope prices them correctly.

## 5.3.4 Telemetry

Per run: queued at, admitted at, started at, finished at, wait time, duration, rows read, rows written, peak memory, pool, weight. Per pool: utilisation, queue depth, admission refusals. Per source: reads, rows, refusals by budget rule, circuit-breaker state.

**Wait time is the number that matters.** Rising wait time is the earliest honest signal that the configuration has outgrown the machine, and it is the number that should drive an upgrade conversation rather than an outage.

## 5.3.5 Acceptance

1. 200 job definitions on mixed cadences run for 24 hours with no pile-up and no run stuck in a running state.
2. Ten jobs scheduled on the same nominal minute start spread across the cadence window.
3. A job that overruns its cadence skips rather than accumulating, and the skip is visible with its reason.
4. The interactive read path stays responsive with every pool saturated.
5. An over-budget read is refused before reaching the source, naming the rule.
6. An unreachable source opens its circuit breaker and does not consume import slots.
7. Forced overload walks the degradation ladder with each level announced.
8. Computed D and R appear on the Jobs Administration page and match measured behaviour.

---


## 5.3.6 The job dependency graph

"Jobs feed each other" becomes a real directed acyclic graph.

**`ppiq_meta.job_dependencies`**: `job_definition_id FK`, `depends_on_job_definition_id FK`, `dependency_kind varchar(20) NOT NULL` CHECK IN (`data`,`schedule`,`resource`), `is_required boolean NOT NULL DEFAULT true`, `depends_on_version integer NULL`, `staleness_tolerance_minutes integer`. UNIQUE `(job_definition_id, depends_on_job_definition_id)`; CHECK self-reference forbidden; **a trigger refuses an insert that would close a cycle.**

**`ppiq_meta.job_run_dependencies`** - the run-level instances: `run_id`, `depends_on_run_id`, `resolution varchar(20)` CHECK IN (`satisfied`,`stale_accepted`,`blocked`,`skipped_optional`,`failed_upstream`), `resolved_at_utc`, `watermark_inherited text`.

| Concern | Design |
|---|---|
| **Required versus optional** | A required dependency unsatisfied **blocks**; an optional one is skipped and recorded as skipped, and the downstream run states that it ran without it |
| **Dependency version** | A dependency may pin a definition version; a version mismatch is `blocked` with both versions named |
| **Conditions** | `satisfied` upstream succeeded within tolerance; `stale_accepted` upstream succeeded but older than tolerance and the dependency permits it; `blocked` upstream failed or never ran; `failed_upstream` upstream failed this cycle |
| **Fan-in and fan-out** | A run waits for all required parents; a completed run releases all children in one admission pass, subject to pool capacity |
| **Retry and skip** | A blocked child retries on the next tick up to a ceiling, then reports blocked with the upstream named. It never runs on stale data outside tolerance |
| **Cycle prevention** | At definition time by the trigger, and at admission time by a topological check, because a dependency added concurrently could otherwise slip through |
| **Dependency-aware scheduling** | The scheduler admits in topological order within a tick, so a parent and its child in the same tick run in order rather than racing |
| **Watermark propagation** | A child inherits the parent's high watermark, which is how incremental feature refresh knows exactly what changed |
| **Impact preview before publishing** | `GET /api/definitions/{id}/impact` includes the downstream job graph and the runs that will be triggered |
| **Visual DAG** | On F4 Jobs Administration: nodes as job definitions coloured by last outcome, edges by dependency kind, required edges solid and optional dashed, with the critical path highlighted and pool utilisation shown per node |

**Endpoints.** `GET`/`POST`/`DELETE /api/jobs/{id}/dependencies`; `GET /api/jobs/graph`; `GET /api/jobs/{id}/impact`; `GET /api/runs/{runId}/dependencies`.

## 5.3.7 The progress and streaming protocol

| Element | Specification |
|---|---|
| **Transport** | Server-sent events over the existing authenticated connection, `GET /api/runs/{runId}/stream`. **Fallback** to polling `GET /api/runs/{runId}/progress` at a server-advertised interval where events are unavailable |
| **Event envelope** | `{ runId, seq, emittedAtUtc, kind, stage, nodeId?, rowsRead?, rowsWritten?, percent?, message?, code?, terminalState? }` |
| **Run identifier** | The run's own identity; every event carries it, so a client reconnecting to the correct stream is unambiguous |
| **Sequence number** | Monotonic per run, starting at 1. A client detecting a gap requests replay from its last sequence |
| **Stage and node** | The pipeline stage, and the graph node where the work is happening, so progress is locatable on the board |
| **Rows read and written** | Cumulative, always, because a percentage without absolute counts is not diagnosable |
| **Percentage** | Only where a denominator is genuinely known. Otherwise omitted |
| **Indeterminate progress** | `kind = "working"` with a stage and cumulative rows and **no percentage**. A fabricated percentage is prohibited |
| **Heartbeat** | Every 10 seconds while running, so a silent stream is distinguishable from a stalled run |
| **Warning** | `kind = "warning"` with a message and a code; does not change the terminal outcome |
| **Refusal** | `kind = "refused"` with the code and the sentence; the terminal state is `Blocked` or `Failed`, never `Completed` |
| **Terminal states** | `Completed`, `Failed`, `Blocked`, `Cancelled`, `Reaped`. Exactly one terminal event per run, always emitted |
| **Reconnection and replay** | The last 500 events per run are retained for the run's duration plus 10 minutes; reconnect with `Last-Event-ID` and receive the gap |
| **Permission checking** | On subscribe **and on every replay**, against the run's tenant and the subscriber's role; a permission lost mid-stream terminates the stream |
| **Browser fallback** | The activity tray G6 degrades to polling automatically and states that it is polling |
| **Persistence of the final summary** | The terminal event's counts, duration and outcome are written to `job_run_history` and to the job log, so the summary outlives the stream |

## 5.3.8 The pre-run cost estimator

The interface promises an estimate; this is the estimator behind it.

**Inputs.** The compiled statement's plan shape, table statistics, partition pruning, declared cardinalities from `plant_relationships`, the observed expansion factor of prior runs of this definition version, and the pool's current utilisation.

| Output | Basis |
|---|---|
| **Rows scanned** | Partition-pruned row estimates per source, multiplied by join expansion |
| **Rows written** | Output cardinality estimate |
| **Join expansion factor** | From declared cardinality, corrected by observed expansion on prior runs of this version |
| **Expected memory** | From the largest hash or sort operator in the plan |
| **Expected execution time** | **A range**, from the prior runs of this version where they exist, otherwise from a scan-rate model, widened by pool contention |
| **Pool weight** | The definition's declared weight, and whether it fits the pool now |
| **Source impact** | Estimated statements and rows against each source, and whether that fits the source budget |
| **Within capacity?** | A verdict: `within`, `tight`, `exceeds`, with the constraining meter named |
| **Recommendation** | Sample, schedule off-peak, reduce the window, or request approval - whichever the constraint implies |

**Honesty rules.** The estimate always states its **basis** (`prior_runs`, `statistics_only`, `no_basis`) and its **confidence** (`high`, `medium`, `low`). With no prior run and stale statistics it returns `no_basis` and says so rather than producing a number. **It never presents itself as exact**, and the interface renders it as a range with the basis visible. An estimate that proves wrong by more than a configured factor is recorded, so the estimator's own accuracy is measurable.

**Endpoint.** `POST /api/definitions/{id}/estimate` returns the structure above. `GET /api/estimator/accuracy` returns the estimator's measured error distribution.


## 5.3.9 Designing for very large plant data - the delta propagation law

### 5.3.9.1 The scenario, worked

The product's purpose is to be **the hub that joins several large sources and correlates across them** (Chapter 1.2.3). That purpose is incompatible with a design that rescans the plant on every cadence. The following is a realistic mid-tier customer, and it is the case the architecture must survive.

```
INPUT      3 DB-links x 100 tables x 500 MB average        =  146 GB source footprint
           (individual tables observed at 1.4 GB and 1.9 GB)
JOBS       3 import jobs      - incremental, delta only
           7 non-import jobs  - projection, feature, analysis, practice,
                                prediction, value, report
CADENCE    every 3 minutes    = 480 runs per job per day = 3,360 non-import runs/day
```

**If a non-import job scans the model:**

```
3,360 runs  x  146 GB  =  481 TB scanned per day  =  5.7 GB/s sustained, forever
```

**No affordable server does that.** Not at this tier, not at any tier. A design that requires it has failed, and a licence that responds by forbidding the customer three DB-links has punished the customer for the design.

**If every non-import job is delta-scoped:**

```
daily change 0.5%  ->  0.7 GB delta/day  ->  1.6 MB per run  ->  5.1 GB/day scanned in total
daily change 2.0%  ->  2.9 GB delta/day  ->  6.2 MB per run  ->  20.5 GB/day scanned in total
```

**The ratio between the two designs is between 24,000 and 96,000 to one.** That ratio is not an optimisation. It is the difference between a product that can be sold at Pro tier and a product that cannot be operated at all.

> **THE DELTA PROPAGATION LAW.** **Incremental acquisition is necessary and is not sufficient. Every job class in the product is delta-scoped end to end. A full scan of the canonical model at cadence is a design defect, not a tuning problem.**

### 5.3.9.2 The watermark chain

Incrementality is only end to end if **the delta is propagated, not recomputed** at each stage. Each stage records what it consumed and hands the boundary to the next.

```
SOURCE
  |  cursor_watermarks.watermark_value                 per dataset
  v
STAGING          import_batches (batch id, watermark range)
  |  the batch id IS the delta token; nothing recomputes "what is new"
  v
CANONICAL        projection consumes unprocessed staging_records of that batch
  |  every canonical row carries import_batch_id                    <- the propagated delta
  v
CHANGED-ENTITY   distinct entity identities touched by batches since the last stage watermark,
  RESOLUTION     expanded through genealogy_paths to the consuming grain
  |
  +--> FEATURE      feature_refresh_watermarks.last_batch_watermark
  +--> ANALYSIS     analysis_scope_watermarks
  +--> PRACTICE     practice_context_watermarks   (context-level, per 5.6.4b)
  +--> PREDICTION   scoring scope = in-process units in the changed set
  +--> VALUE        impacted findings and predictions only
  +--> REPORT       period-bounded, never model-wide
```

**`ppiq_plant.stage_watermarks`** generalises the pattern: `stage_code varchar(30) NOT NULL`, `scope_key varchar(200) NOT NULL` (feature set version, analysis definition version, practice context, model), `last_batch_watermark text NOT NULL`, `last_run_id uuid`, `dirty_scope jsonb`, `advanced_at_utc`. UNIQUE `(tenant_id, stage_code, scope_key)`.

**Four rules that make the chain trustworthy.**

1. **A stage advances its watermark only on full success.** Partial success advances nothing and records the dirty scope, so the next run resumes rather than skipping.
2. **A late-arriving batch marks its entities dirty** rather than being ignored, and the next pass picks them up (Chapter 3 DF10).
3. **A definition or relationship change invalidates the stage**, because a delta computed under two different definitions is not one delta (`FS04`).
4. **Every run records the watermark range it consumed**, so a result is explainable and a gap is detectable.

### 5.3.9.3 Delta strategy per job class

**Every class has a named strategy. A class with no strategy may not run at cadence.**

| Class | Delta scope | Mechanism | Full scan permitted |
|---|---|---|---|
| **Import** | Source rows after the cursor | Watermark on the source column | Only on a declared backfill, throttled |
| **Projection** | Unprocessed `staging_records` of the batches since the last projection | `processing_status = 'Pending'` partial index | Only on a definition-version change, and then batch-bounded |
| **Feature refresh** | Entities touched by those batches, expanded through genealogy | Changed-entity resolution, `feature_store.is_dirty` | Only on feature-set invalidation |
| **Statistics and correlation** | **The analysis window, partition-pruned**, plus incrementally maintained sufficient statistics where the method permits | Windowed scan over pruned partitions; `analysis_sufficient_statistics` for additive measures | Only on a method or population change |
| **Practice learning** | Contexts whose periods changed | `practice_context_watermarks`; unchanged contexts carry forward with their prior statistics | Only on tolerance or definition change |
| **Prediction scoring** | In-process units in the changed set, not yet past horizon | Scope resolution against route position | Never at cadence |
| **Value** | Findings and predictions created or changed since the last run | Result-table watermark | Only on a cost-assumption change, and then bounded to the affected period |
| **Report** | The report's declared period | Period predicate, partition-pruned | Never |
| **Retention** | The partition or batch outside retention | Partition drop preferred over row delete | Never |
| **Supervisor** | Runs completed since the last review | Run-table watermark | Never |

**Sufficient statistics, and why they matter here.** For additive and semi-additive measures - counts, sums, sums of squares, cross-products, contingency cells - **the statistic is maintained incrementally rather than recomputed.** `ppiq_plant.analysis_sufficient_statistics` holds per outcome, grain, factor and time bucket: `n`, `sum_x`, `sum_x2`, `sum_xy`, `sum_y`, `sum_y2`, `contingency jsonb`, `bucket_from_utc`, `bucket_to_utc`, `source_batch_high_watermark`. A correlation over a 12-month window then **aggregates 12 monthly buckets instead of scanning a year of observations**, and adding today's data updates one bucket.

Where a method is not decomposable - a bootstrap, a rank correlation, a model fit - **the window is still partition-pruned and the population is still the changed-entity set where the method permits**, and where it does not, the job declares itself a **windowed recompute** and is scheduled accordingly rather than at three-minute cadence. **A method that cannot be incremental is not thereby exempt from the law; it is exempt from cadence.**

### 5.3.9.4 Storage layout for the hub case

Delta scoping bounds what is *read*. Storage layout bounds what is *stored* and how fast the read finds it.

| Mechanism | Design | Effect on the worked scenario |
|---|---|---|
| **Range partitioning by time** | Monthly on observations, events, quality, predictions, quarantine and every log family, from the Medium class upward | A 30-day analysis window touches 1 or 2 partitions of 24, not the table |
| **Partition pruning as a contract** | Every windowed query carries its time predicate on the partition key. **A query that cannot prune is refused by the estimator with the reason**, not run slowly | Turns a 146 GB scan into a 6 GB scan before any other mechanism applies |
| **Compression on cold partitions** | Partitions older than the hot window are compressed; typical 3:1 to 5:1 on observation data | 855 GB at 2 years becomes roughly 250 GB stored |
| **Downsampling** | Beyond a declared age, high-frequency observations are rolled to interval aggregates with the raw retained per policy | Bounds growth without losing trend |
| **Tiering** | Hot partitions on fast storage; cold on cheap; archive to object storage | Cost follows access, not volume |
| **Covering indexes on the hot path** | The genealogy covering index; `(parameter_definition_id, observed_at_utc)`; `(outcome_code, q_value)` | The hot query never touches the heap |
| **Partial indexes on open predicates** | Unprocessed staging, running runs, open queue rows | Index size independent of table size |
| **The working-set principle** | Database RAM is sized against the **hot partitions**, not the total (Chapter 6 6.1.9.4) | 2 years of data does not require 2 years of RAM |

**The retained-volume arithmetic for the scenario**, which is what actually sizes the machine:

```
source footprint            146 GB   (this is NOT what is stored)
daily canonical delta       0.7 GB   at 0.5% change
canonical at 24 months      855 GB   with index factor 1.6
compressed cold partitions  ~250 GB effective
staging at 14 days          ~10 GB
results + logs              ~60 GB
TOTAL RETAINED              ~320 GB  -> comfortably inside the Medium class
```

**The source footprint is not the storage requirement.** A customer with 146 GB of source across three links stores roughly 320 GB after two years, because the product stores the **model of the plant plus its history**, not a copy of every source table.

### 5.3.9.5 Multi-source hub scale

The purpose is cross-source correlation, so the mechanisms that make one source cheap must not become expensive when there are several.

| Concern | Design |
|---|---|
| Per-source isolation | Each DB-link has its own budget, its own circuit breaker and its own cursor. **One slow source never blocks the others** |
| Parallel acquisition | Sources import in parallel up to the `import` pool parallelism, each within its own budget |
| Join cost | Cross-source joins resolve through `plant_relationship_paths`, a **lookup**, not a graph search at query time |
| Identity resolution | Alias resolution is an indexed lookup on `(alias_system, alias_value)`, not a scan |
| Cross-grain attribution | Served by the genealogy covering index; the loader never touches the heap |
| Adding a fourth source | Adds its own delta, its own budget and its own partitions. **It does not multiply the cost of the existing three**, because nothing rescans |

**The scaling shape is the point.** Naive design scales as `sources x tables x size x cadence`. This design scales as `sum of per-source deltas`, and the number of sources affects the number of deltas, not the size of each.

### 5.3.9.6 Full-scan governance

A full scan is sometimes legitimate - an initial backfill, a definition change, a reindex, a migration compatibility scan. **It is never legitimate at cadence.**

| Rule | Enforcement |
|---|---|
| A full scan is an **explicitly declared operation**, never an accidental consequence of a missing predicate | The job definition declares `scan_mode: delta \| windowed \| full` |
| `full` may not be combined with a cadence below the declared floor | `JB05` refused at F4, naming the floor |
| A full scan is **throttled, checkpointed, resumable and pausable** | The backfill mechanism of 5.3.2 M1 |
| A full scan runs in a **reduced-priority pool share** so it cannot starve cadence work | Admission control |
| The estimator **refuses to start** a scan whose projected cost exceeds the remaining capacity in the current band | `QT04` with the estimate |
| **A query that cannot partition-prune is refused with the reason**, not executed | Compiled-query validation |

### 5.3.9.6a Execution mechanics of a delta run

**The delta scope says what to process. These six mechanics say how, and without them a delta run is still unsafe at scale.**

#### Chunking

A delta scope is **partitioned into bounded chunks before execution**, never processed as one statement.

| Property | Rule |
|---|---|
| Chunk key | The scope's natural ordering: `import_batch_id` then `row_number` for staging; `material_unit_id` range for entity scopes; partition boundary for time scopes |
| Chunk size | Declared per job class as a **row count and a byte ceiling**, whichever binds first. Default 50,000 rows or 256 MiB |
| Boundary alignment | A chunk **never spans a partition boundary**, so every chunk prunes to one partition |
| Adaptive sizing | A chunk exceeding its runtime budget causes the next chunk to halve; a chunk well inside it may grow to the ceiling. **Bounded by the declared minimum and maximum, never unbounded** |
| Memory | Chunk size is chosen so a chunk fits the worker's working memory. **A chunk that spills to temporary storage is a sizing finding**, reported and measured by profile C1-C4 (Chapter 6 6.1.5.8) |

#### Bounded parallelism

| Level | Bound |
|---|---|
| Across job classes | The weighted pools of 5.3.2 M4. Unchanged |
| **Within one delta run** | `chunk_parallelism`, declared per class, default 4, **counted against the run's own weighted slot allocation** - a run parallelising internally consumes more of its pool, it does not escape it |
| Per source | The per-source concurrent-read limit of 5.3.2 M5 |
| Per partition | **At most one writer per partition at a time**, which is what makes chunk-level idempotency provable |
| Interactive protection | The reserved interactive share is **never** available to chunk parallelism |

#### Idempotency

**Every chunk is individually idempotent. Re-running a chunk produces the same result and no duplicate.**

| Mechanism | Where |
|---|---|
| Filtered unique on the provenance pair | Every projected entity - re-projection supersedes in place |
| `ON CONFLICT DO NOTHING` on append-only targets | `plant_data_log`, `alert_deliveries` |
| `UNIQUE (material_unit_id, feature_set_version_id)` | `feature_store` |
| `UNIQUE (compute_run_id, factor_code, outcome_code)` | `correlation_results` |
| **Chunk receipt** | `ppiq_plant.delta_chunk_receipts`: `run_id`, `chunk_key_from`, `chunk_key_to`, `rows_in`, `rows_out`, `bytes_scanned`, `completed_at_utc`, `checksum`. UNIQUE `(run_id, chunk_key_from)`. **A chunk with a receipt is skipped on resume** |

#### Checkpoint and resume

A chunk receipt **is** the checkpoint. On restart, the run reads its receipts, skips completed chunks, and resumes at the first gap. **A run interrupted at any point resumes without reprocessing and without loss.** The watermark advances only when every chunk in the scope has a receipt (5.3.9.2 rule 1).

#### Scan budgets

**A delta run declares what it expects to read, and is stopped if it exceeds it.** This is the mechanism that catches a delta scope that has silently become a full scan.

| Budget | Default | On breach |
|---|---|---|
| `max_bytes_scanned_per_run` | 20x the changed bytes | **Abort the run**, record `SC-BUDGET` with measured against expected, raise the amplification finding of Chapter 6 6.1.12.2a |
| `max_rows_scanned_per_chunk` | 20x the chunk's changed rows | Abort the chunk, halve and retry once, then abort the run |
| `max_runtime_per_chunk_s` | class-declared | Adaptive resize, then abort |
| `max_temp_spill_bytes` | worker working memory | Abort with a sizing finding |

> **The budget is computed from the delta, not from the table.** That is what makes it a delta-integrity check rather than a timeout: a run reading twenty times its own delta is doing something the design did not intend, whatever its absolute size.

#### Deterministic merging

Chunks complete out of order and in parallel. **The merged result must be identical to a serial run.**

| Rule | Statement |
|---|---|
| **Commutative targets only** | A delta run may write only targets whose per-chunk writes commute: insert-or-supersede by key, or additive accumulation. **A non-commutative aggregation is computed in a final serial reduce step, never chunk by chunk** |
| **Sufficient statistics merge additively** | `n`, `sum_x`, `sum_x2`, `sum_xy` merge by addition; the derived statistic is computed once at the end from the merged sufficient statistics, so chunk order cannot affect it |
| **Ordering-sensitive work is serialised** | Genealogy path materialisation and weight-sum validation run in a final ordered pass per affected child, because their correctness depends on seeing the complete edge set |
| **Final reduce is single-writer** | One transaction, after all chunk receipts exist |
| **Determinism is testable** | The same scope processed with `chunk_parallelism` of 1 and of 8 **produces byte-identical results**, asserted in the reliability suite |

### 5.3.9.7 Acceptance

1. With 3 links, 100 tables each and 146 GB of source, seven non-import jobs at 3-minute cadence sustain **under 25 GB/day total scanned**, measured.
2. Every job class reports the watermark range it consumed; a gap is detectable.
3. A partial failure advances no watermark and the next run resumes from the dirty scope.
4. A late batch is picked up on the next pass, proven by the affected entity set.
5. A definition change invalidates its stage and refuses the delta run with the reason.
6. A correlation over 12 months aggregates buckets rather than scanning observations, proven by rows read.
7. A windowed query without a prunable predicate is refused by the estimator.
8. A declared full scan below the cadence floor is refused with `JB05`.
9. Adding a fourth source increases scanned volume by that source's delta only, measured against the three-source baseline.
10. Retained volume after a simulated two years matches the projection of 5.3.9.4 within 20 percent.
11. A run interrupted mid-scope resumes from its chunk receipts, reprocesses nothing and loses nothing.
12. The same scope run with `chunk_parallelism` of 1 and of 8 produces **byte-identical results**.
13. A chunk never spans a partition boundary, asserted on the executed plan.
14. A run reading more than its declared scan budget **aborts and raises the amplification finding**, rather than completing slowly.
15. **Scan Amplification Ratio stays inside its certified band** for every job family under the C2 profile load (Chapter 6 6.1.5.8, 6.1.12.2a).

## 5.4 THE GATE AND THE ENGINE

## 5.4.1 The Engine as the hub

Every analytical capability in the product is a client of one engine. There is **one implementation per capability**; a second implementation of any of these is a defect, not an option.

```
                          +---------------------------+
   canonical model  --->  |         THE ENGINE        |
   (ppiq_plant)           |                           |
                          |  1. Feature/outcome store |
                          |  2. Readiness gate        |
                          |  3. Compute engines       |
                          |  4. Results store         |
                          |  5. Value engine          |
                          |  6. Supervisor            |
                          +------------+--------------+
                                       |
       +---------------+---------------+---------------+---------------+
       |               |               |               |               |
   Findings        Risk scores     Suggestions     Value ranges    Assistant dock
   (D4)            (D5)            (D6)            (D7)            (G1, read-only)
```

**Nothing bypasses it.** A widget may query the canonical model directly for a chart, but no surface computes a statistic, a score or a euro figure of its own. This is what makes every number in the product reproducible from the database.

## 5.4.2 Rules and validation

Validation happens at four points, and each has a different job.

| Point | Validates | Refusal |
|---|---|---|
| **Authoring** | The definition is well-formed: types, joins, cycles, required inputs, registered outcome and grain | At drag time or at save, in the debug log, with the rule named |
| **Admission** | The pool has capacity; the licence allows this job class; the role allows this action | Named error, queued or refused with an estimated wait |
| **Gate** | The **data** can support a defensible answer | Blocked run, persisted, with the failing dimension and its measured value |
| **Result** | The computed result meets the statistical contract | A result that fails the contract is not stored as a finding |

**The critical distinction, and it is the one people get wrong:** authoring validation asks "is this definition legal?"; the gate asks "is this data sufficient?". A perfectly legal definition on insufficient data must be refused, and refused *by the data*, not by the author's judgement.

## 5.4.3 The readiness gate

Five dimensions, three states, evaluated before every analytical run.

| Dimension | Direction | Ready | Partial | Blocked |
|---|---|---|---|---|
| **Independent units** in the window | higher better | >= 60 | >= 30 | below 30 |
| **Outcome events** in the window | higher better | >= 40 | >= 15 | below 15 |
| **Minority-class balance** | higher better | >= 10% | >= 3% | below 3% |
| **Freshness factor** (data age / cadence) | **lower better** | <= 1.0 | <= 2.0 | above 2.0 |
| **Required-field completeness** | higher better | >= 95% | >= 85% | below 85% |

**Six binding clauses.**

1. **The overall state is the worst across dimensions.** Not an average, not a majority. One blocked dimension blocks the run.
2. **Every dimension returns a reason string** built from its measured value and its threshold, for example `42 in [30,60) (Partial)`. That string is why a blocked run is explainable from the database alone, without the application.
3. **The compute engine and the live readiness endpoint call the same function.** The verdict a user sees can never drift from the verdict the engine acts on. A second implementation violates the single-engine law.
4. **Thresholds are per-tenant and governed.** They may be tuned by a human through a change with a recorded justification. They may **never** be lowered to make a run pass, and no automated process may write them.
5. **A blocked run is a persisted run** with a real identifier, a blocked status, the failing dimension and the evidence string. Not an absence, not an error.
6. **The product never shows nothing while a gate is blocked.** It shows the simple analysis that needs no history, the readiness meter with measured counts, and an honest collecting-data state.

**Why these five and not others.** Independent units and outcome events bound sampling error. Minority balance stops a 99-to-1 class ratio producing a meaningless "accuracy". Freshness stops a conclusion drawn on stale data. Completeness stops a conclusion drawn on a column that is mostly null. **Each corresponds to a way a confident wrong answer is normally produced.**

## 5.4.4 Coefficient adjustment as the learning curve grows

> How the system enhances and adjusts its own coefficients as learning accumulates.

This is the Supervisor, and it is deliberately the most constrained component in the product.

### What it adjusts

| Adjustable | Bounds |
|---|---|
| Feature window length per outcome | Within a configured minimum and maximum |
| Lag offsets between a parameter and an outcome | Within a configured range |
| Model hyperparameters | Within a declared search space |
| Job cadence and compute weight | Within the pool's admissible range |
| Feature selection: which features earn a place | From the registered feature set only |
| Stratification variables | From registered dimensions only |

### What it may never touch

- Readiness thresholds.
- Refusal logic.
- Evidence requirements.
- The statistical contract: effect-size ranking, false-discovery control, stability requirements.
- The audit layer.

**Enforcement is by construction, not by convention.** The honesty machinery is outside the Supervisor's write scope: it holds no credential that can write those rows. A convention can be forgotten in a refactor; an absent permission cannot.

### The loop

```
1. OBSERVE     read completed runs, their effects, their stability, their stratum survival
2. PROPOSE     a bounded adjustment with a stated expected improvement
3. DRY-RUN     re-execute against held-out history with the proposed value
4. COMPARE     did the effect strengthen? did stability improve? did anything regress?
5. RECORD      a provenance row: job, parameter, before, after, justification, evidence handle
6. AWAIT       a human approves or rejects. NOTHING CHANGES AUTOMATICALLY
7. APPLY       on approval only; the provenance row is the audit trail
```

### The drift test that gates its release

Inject a known drift into a controlled dataset. The Supervisor must **detect it, propose the correction, and have the dry-run demonstrate recovery**. A Supervisor that cannot pass a known-answer drift test is not released, because a self-tuning component that tunes wrongly is worse than none.

### The learning curve, honestly

| Stage | What the Supervisor can do |
|---|---|
| Weeks 1-2 | Nothing useful. Too few completed runs. It says so |
| Month 1 | First proposals on window length and lag, low confidence, stated as such |
| Month 3 | Feature selection and cadence proposals with measurable dry-run improvement |
| Month 6+ | Stable coefficient sets per outcome; proposals become rare and specific |

**Stating this timeline before the sale is what stops it becoming a disappointment after it.**

## 5.4.5 How the assistant gets its data from the Engine

**The assistant is a read-only client of the Engine. It never computes.**

```
question
   |
   v
[ intent + entity resolution ]  -- plant glossary, synonyms, registry
   |
   +--> [ RETRIEVAL ]  permission-scoped chunks: findings, datasets, mappings, connectors, docs
   |
   +--> [ TOOLS ]      typed, role-scoped, deterministic:
   |                     fetch_finding(id)        -> the stored finding, framing included
   |                     run_kpi(code, filters)   -> the Engine computes; the tool returns
   |                     open_suggestion(id)      -> the stored suggestion with evidence
   |                     material_unit_count(...) -> a count from the canonical model
   |                     readiness(outcome,grain) -> the gate's own verdict
   |
   v
[ GROUNDING ]   every numeric claim must carry a resolvable evidence handle
   |
   v
[ NO-FABRICATION GUARD ]   a sentence containing an uncited number is REJECTED BEFORE DISPLAY
   |
   v
[ EGRESS PLAN ]   decides exactly what may leave the tenant, per serving mode
   |
   v
answer + citations, or a refusal with its reason
```

**Four rules.**

1. **Tools return Engine output. The model phrases it.** `run_kpi` does not ask the model for a number; it calls the deterministic engine and hands the result back.
2. **Retrieval is role-scoped at the chunk level.** Chunks carry a role scope; a viewer's retrieval physically cannot reach an engineer's chunks.
3. **The guard runs before display, not after.** A fabricated figure that reaches the screen and is retracted has already been read.
4. **A refusal is amber and evidential; a transport failure is red and says the request failed.** A transport fault dressed as an evidential abstention is a lie about the product's own state.

**What the assistant may never do:** compute, rank, originate a figure, write anything except its audit log, or answer outside the retrieval scope its role permits.

## 5.4.6 The Engine as the hub for AI and ML

Statistical jobs, model training, model scoring, suggestion generation and value computation are **all job classes on the same substrate**, sharing:

| Shared | Consequence |
|---|---|
| The feature and outcome store | A model and a correlation see the same features. They cannot disagree about the data |
| The readiness gate | An ML run is refused on insufficient data exactly as a statistical one is |
| The results store | Findings, scores and suggestions are queryable together and by the assistant |
| The job executor | ML competes for slots under the same admission control |
| The provenance model | Every output traces to its run, its definition version and its population |

**Jobs feed each other.** A correlation identifies candidate features; a model consumes them; scores generate suggestions; the value engine prices them; the Supervisor reviews all of it. **The hub is what makes that a loop rather than five disconnected tools.**

## 5.4.7 Acceptance

1. The gate blocks and completes correctly against known datasets, and the overall state equals the worst dimension every time.
2. The live readiness endpoint and the engine return identical verdicts for identical inputs.
3. A blocked run is reconstructable from the database alone, with no application running.
4. A threshold cannot be written by any automated path; the attempt is refused and audited.
5. The Supervisor's dry-run demonstrates recovery on injected drift.
6. Results counts before and after a Supervisor run are identical.
7. The assistant answers with resolving citations, refuses without evidence, and shows red for transport failure and amber for abstention.
8. No surface computes a statistic outside the Engine.

---


---

## 5.4.8 Execution placement policy

The single-engine law and the existence of two execution sites are reconciled explicitly.

> **One mathematical implementation and one result contract per capability. Placement may vary; results may not.**

| Factor | Effect on placement |
|---|---|
| Data volume | Above a threshold, in-database execution avoids moving rows |
| Supported operation | An operation the database cannot express runs in the managed engine |
| Data locality | Co-located data favours in-database |
| Memory | An operator exceeding the managed engine's budget goes in-database, or is refused with the reason |
| Database load | High load favours the managed engine to protect the interactive path |
| Latency requirement | Interactive requests favour whichever is faster for the shape |
| Tenant policy | A tenant may pin placement |
| Air-gapped deployment | Placement is pinned by the deployment profile |

**Four binding rules.**

1. **One implementation of the mathematics.** Where both sites can execute a capability, they execute the **same specification** and are proven equal by a parity test on known inputs, to a declared tolerance. A parity failure is a build failure.
2. **One result contract.** Identical inputs produce identical stored rows regardless of placement. Placement never appears in a result value.
3. **The policy is deterministic.** Given the same inputs and the same measured conditions it chooses the same site. It is not a heuristic that varies run to run.
4. **The decision is stored with the run.** `compute_runs.engine_placement` and `placement_reason`, so a result is explainable and a performance investigation has the facts.

**Endpoint.** `GET /api/engine/placement-policy` returns the current policy and its thresholds; `GET /api/engine/parity` returns the last parity-test result per capability.

## 5.4.9 Supervisor shadow execution

Dry-run runs in an isolated context, and the isolation is structural.

| Property | Specification |
|---|---|
| **Separate run type** | `run_kind = 'shadow'` on its own table `supervisor_shadow_runs`; a shadow run never appears in `compute_runs` |
| **Read-only inputs** | The shadow context connects with a **read-only database role** that has no write grant on any results table. Isolation by permission, not by discipline |
| **Temporary results** | Written to a temporary schema dropped at cleanup, or to the shadow row's `shadow_result` document |
| **No mutation of live state** | Models, thresholds, findings, scores, predictions and practices are untouched. The read-only role makes it impossible, not merely intended |
| **Held-out history** | A declared holdout window excluded from the data the current production configuration was tuned on |
| **Comparison** | Current production configuration against the proposal, on the same holdout, reporting effect, stability and stratum survival for both |
| **Automatic cleanup** | The temporary schema is dropped and the artifact retained; a failed cleanup raises an alert rather than leaking |
| **Approval record** | `supervisor_provenance` with actor, timestamp, before, after, justification and evidence handles |
| **Atomic application** | On approval the change is applied in one transaction; a partial application is impossible |
| **Proof of no mutation** | `live_row_counts_before` and `live_row_counts_after` captured across every results table, with a **CHECK constraint that they are equal** (Ch3 4.5.12). The proof is a database invariant |

**Protected targets.** The CHECK on `supervisor_proposals.target_kind` makes a readiness threshold, refusal logic or evidence requirement an impossible target at the database level. The Supervisor's own role holds no grant on `readiness_thresholds`.



## 5.4.10 Aggregation semantics authority and execution

The engine never chooses aggregation from storage type alone. Resolution order is deterministic:

1. KPI/measure binding override, if published;
2. parameter `aggregation_kind`, if published;
3. otherwise refuse `AG01 aggregation_semantics_undeclared`.

The executor validates the operation against `signal_kind`, `sampling_basis`, quality policy, interpolation policy, weight basis, maximum gap and counter-reset policy. There is **no universal Average** and no implicit resampling. All interval boundaries are clipped to `[requested_from_utc, requested_to_utc)`. Source values outside the requested interval may be used only to establish the lawful boundary value required by the declared interpolation policy; they never enlarge the denominator or the reported coverage.

### 5.4.10.1 Canonical aggregation algebra

Let the requested interval be `W = [a,b)`, duration `T = b-a`. After quality filtering and boundary clipping, let the lawful covered sub-intervals be `I_j=[s_j,e_j)` with duration `dt_j=e_j-s_j`; gaps larger than `maximum_gap_seconds` are uncovered. Let `C=sum(dt_j)` and **`coverage_fraction=C/T`**. Every interval result returns the coverage contract of Chapter 3 4.5.13a.

| Kind | Binding mathematical contract | Boundary / refusal rules |
|---|---|---|
| `SampleMean` | `(1/n) * sum(v_i)` | Lawful only when `sampling_basis = FixedCadence` (or an explicitly certified equivalent). `n=0` refuses. It is **not** a fallback for irregular/event/deadband data |
| `TimeWeightedMean` | `sum(v_j * dt_j) / C` for step-forward interpolation; for linear interpolation use trapezoids `sum(((v_j+v_{j+1})/2)*dt_j)/C` | Interpolation comes from `interpolation_kind`; `C=0` refuses. Gaps are excluded from numerator and denominator and reduce coverage |
| `Integral` | Step: `sum(v_j*dt_j)`; Linear: trapezoidal integral. Convert time units explicitly through the quantity/unit contract | No implicit unit conversion. Unknown unit-time conversion refuses |
| `Delta` | Reset-aware accumulated change over W. Monotonic no-reset: `last-first`; `ResetToZero`: sum positive segment deltas and treat valid negative transition as reset; `Rollover`: add declared counter modulus across rollover | Counter modulus/reset semantics must be declared when required. An unexplained negative delta refuses |
| `StateDuration` | `sum(dt_j)` for intervals whose state satisfies the requested predicate; duty cycle = state duration / `C`, **not** sample mean of booleans | `StepForward` is the normal state interpolation. An open state at a requested boundary is clipped exactly to the boundary when lawful history exists |
| `Count` | Count governed events/samples after filters | The counted object must be declared; does not imply time coverage |
| `Min` / `Max` | extrema over lawful observations | Empty population refuses; coverage is still reported for interval-scoped use |
| `Last` | latest lawful observation at or before the requested end within the staleness limit | If latest observation is older than staleness/maximum gap, refuse rather than carry forever |
| `Percentile(p)` | governed percentile over the declared sample population | Only valid where sample semantics are appropriate; percentile method/version is pinned |
| `MassWeightedMean` | `sum(v_i*m_i)/sum(m_i)` | Weight series must resolve and `sum(m_i)>0`; otherwise refuse |
| `VolumeWeightedMean` | `sum(v_i*q_i)/sum(q_i)` | Weight series must resolve and `sum(q_i)>0`; otherwise refuse |

### 5.4.10.2 Interpolation and gap semantics

`StepForward` means a value is valid from its source timestamp until the next lawful value **or** `maximum_gap_seconds`, whichever comes first. `Linear` means only the segment between two lawful endpoint samples is covered and is clipped to the request boundaries; it never bridges a gap larger than `maximum_gap_seconds`. `None` means no value is inferred between samples, so time-weighted methods requiring interval coverage refuse unless another declared source contract supplies intervals directly.

A quality-rejected sample terminates or prevents interpolation exactly as defined by the parameter `quality_policy`. Missing, bad-quality or stale intervals are gaps, not zeros.

### 5.4.10.3 Coverage and presentation law

Every interval aggregate exposes `covered_seconds`, `requested_seconds`, `coverage_fraction` and `gap_count`. The method definition may declare a `minimum_coverage_fraction`; if actual coverage is below it the result is a typed refusal. Above that threshold, the value may be returned **with its coverage**. Consumers must not hide coverage or make a 40%-covered and 99%-covered value visually/evidentially equivalent.

### 5.4.10.4 Known-answer proof

The controlled continuous-process fixture deliberately contains fixed-cadence, irregular, deadband-like, state and counter series for which `SampleMean != TimeWeightedMean` by construction. The fixture declares answers independently from the implementation. Acceptance proves boundary clipping, gaps, reset/rollover, state duration and coverage; the code under test may never read the planted answers at runtime.

## 5.5, 5.6 and 5.7 - STATISTICS, MACHINE LEARNING AND THE ASSISTANT

---

## 5.4.11 Operational transition, stabilisation and regime correctness

### 5.4.11.1 Why regime context is part of correctness

A process transition changes the data-generating regime. Pooling stable operation, changeover/setup and ramp-up in one population can produce a statistically strong and completely misleading attribution. The effect can survive p/q-value control because the difference is real; what is wrong is the explanation of **which regime produced it**.

The engine therefore resolves every analysis window against `process_transition_events` and returns one of `Stable`, `Transition`, `Stabilising`, `Mixed`, `Unknown`.

### 5.4.11.2 Stabilisation is declared, not guessed

A transition definition declares one stabilisation basis:

- `Time` — a bounded duration after the transition;
- `SubjectCount` — the first N governed subjects/units/windows after the transition;
- `Condition` — a governed expression such as all required parameters returning inside their declared envelope;
- `None` — no separate stabilisation period.

The engine may later learn that a declared window is conservative or insufficient, but it never silently moves the authoritative boundary. A learned estimate is Layer B evidence and must remain distinguishable from the declared Layer-A context.

### 5.4.11.3 Consumer law

For **steady-state** statistics, correlation, learned practice and reference comparison:

1. `Stable` intervals are eligible.
2. `Transition` and `Stabilising` intervals are excluded or analysed as explicit cohorts when the definition requests them.
3. `Mixed` refuses `RG01 mixed_process_regime` unless the analysis definition declares partitioning.
4. Every result discloses transition/stabilisation overlap and evidence handles.
5. A time-weighted aggregate clips at regime boundaries when the requested semantic is steady-state; it never averages two regimes merely because the values are adjacent in time.

For reconciliation, transition context is **evidence**, not an automatic explanation. A planned setup may support a recorded reason; a pressure/valve fault during the same interval may contradict it. The Fact Evidence Authority and causal ladder still govern the conclusion.

### 5.4.11.4 Known-answer proof

The controlled fixture contains at least one changeover with a known stabilisation interval, a steady-state interval with identical parameter values outside it, and a planted outcome concentrated in ramp-up. The proof must show that a naive pooled analysis finds the planted association, while the governed steady-state analysis excludes/partitions the regime and does not misattribute the ramp-up effect. `RG01` is falsified once by a deliberately mixed interval.

## 5.4.12 Operational Period Driver Decomposition — exact Layer A

This block answers **what changed between two periods** before any learned explanation.

Input: period A, period B, scope, and a set of registered exact measures. Output is a typed comparison containing population, exact value A/B/delta for each measure, evidence handles, and the following derived exact context when available:

- transition count and transition duration;
- stabilisation duration/subject exposure;
- stable-run/sequence count and length distribution;
- stopped minutes and production-impact minutes;
- yield/scrap/quality-event counts or rates under the registered denominator;
- energy/resource quantities where registered;
- any further registry-backed exact measure.

**No causality and no invented cost.** The block may say “period A had 32% fewer transition minutes and 18% longer median stable runs.” It may not say “that caused the saving” unless a higher claim class supports it. If direct cost facts exist they may be compared exactly; assumption-based euro attribution remains the Value Engine.

Assistant tool: `CompareOperationalPeriods`. It returns Layer A only and is verified like any numeric tool response.

## 5.5 STATISTICS, CORRELATION AND DATA ANALYSIS

## 5.5.1 How to read this catalogue

Every function is a **block on the S3 wiring diagram**. Each entry states, exactly as the guideline requires:

```
BLOCK          the palette name
INPUTS         what wires into it, with port types
CONFIG         what is set inside it (expression editor, dropdowns)
OUTPUTS        what wires out, with port types
VALIDATES      the rule that must hold, and the refusal sentence if it does not
BEST CHART     the chart that displays this function's output correctly
```

**The BEST CHART column is binding on the widget layer.** When a user charts an analysis result, the chart-type switcher defaults to the block's declared best chart and offers the alternatives it declares. This is how the wiring diagram and the analysis page stay coherent: **the block declares how its output should be seen.**

All blocks are registry rows, extensible without a code branch. All obey the statistical honesty discipline of Chapter 1.5.8, always on and never bypassable.

## 5.5.2 Group A - Descriptive

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Summary statistics** | dataset | measure column | table: n, mean, median, sd, min, p25, p75, max, nulls | Column is numeric. *"`<col>` is text; Summary statistics needs a number."* | **Table**; box plot as alternative |
| **Distribution** | dataset | measure, bin count or auto | dataset: bin, count | Numeric; bins >= 2 | **Histogram**; box plot |
| **Category counts** | dataset | dimension, optional measure | dataset: category, count or aggregate | Cardinality <= 500 else warn *"`<col>` has `<n>` distinct values; the chart will be unreadable. Group them first."* | **Bar**; pareto when the tail is long |
| **Time series** | dataset | time column, measure, aggregation, bucket | dataset: bucket, value | Time column is a date type; bucket >= source resolution | **Line**; area for cumulative |
| **Cross-tabulation** | dataset | two dimensions, measure | matrix | Both cardinalities <= 50 | **Heatmap**; pivot table |
| **Outlier detection (IQR / z-score)** | dataset | measure, method, threshold | dataset with `is_outlier` flag | n >= 20 | **Box plot**; scatter with outliers highlighted |
| **Missingness profile** | dataset | - | dataset: column, null count, null percent | - | **Bar**, descending |

## 5.5.3 Group B - Association and correlation

**The core group. This is what the product exists to compute.**

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Pearson correlation** | dataset | two measures, or one measure against a measure set | r, p, n, confidence interval | Both numeric; n >= 30; **warns on strong non-linearity** *"The relationship looks non-linear; Spearman may fit better."* | **Scatter** with fitted line; heatmap for a matrix |
| **Spearman rank correlation** | dataset | two measures | rho, p, n | Both ordinal or numeric; n >= 30 | **Scatter** of ranks; heatmap for a matrix |
| **Correlation matrix** | dataset | measure set, method | matrix of coefficients with q-values | Set size <= 200; **all pairs pass through false-discovery control** | **Heatmap**, diverging palette centred on zero |
| **Chi-square independence** | dataset | two dimensions | chi2, p, degrees of freedom, Cramer's V, contingency table | Every expected cell >= 5, else *"`<n>` cells have an expected count below 5. Merge categories or widen the window."* | **Heatmap** of standardised residuals; stacked bar |
| **ANOVA / Kruskal-Wallis** | dataset | dimension (groups), measure | F or H, p, eta-squared, per-group means | >= 2 groups, each n >= 10; normality checked and the non-parametric alternative substituted with a note | **Box plot** per group; bar of group means with error bars |
| **Odds ratio / relative risk** | dataset | binary outcome, binary or binned exposure | OR, confidence interval, p, 2x2 table | Every cell >= 5 | **Forest plot**; bar with confidence intervals |
| **Point-biserial** | dataset | binary outcome, measure | r, p, n | Outcome exactly two values | **Box plot** by outcome class |
| **Lagged correlation** | dataset | parameter, outcome, lag range | coefficient per lag with the best lag marked | Time column present; lag range within the window | **Line** of coefficient against lag |
| **Genealogy-attributed correlation** | dataset | parent-grain parameter, child-grain outcome | coefficient weighted by contribution weight, effective n | **Weights per child must sum to 1.0**; else *"Attribution weights for `<n>` units do not sum to 1. Fix the genealogy mapping before correlating across grain."* | **Scatter** with point size by weight |

**Genealogy-attributed correlation is the block no business-intelligence tool has.** It is the mechanism behind the product's central claim: a parameter observed at a parent grain related to an outcome recorded at a child grain, correctly weighted where a child descends from more than one parent. *Illustrative only: the grains, the parameter and the outcome are whatever the customer's own imported model declares them to be.*

## 5.5.4 Group C - Discipline (always applied, never optional)

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **False-discovery control** | p-value set | q threshold (default 0.05) | q-values, significance flags | Set size >= 2 | **Table** with significance; volcano plot |
| **Effect-size ranking** | results set | - | ordered by absolute effect, p as tie-break only | **Refuses to order by p-value** | **Bar** of effect size, descending |
| **Stratification** | dataset, results set | stratification dimensions | per-stratum effect, survival verdict, reason | Each stratum n >= 15, else the stratum is reported as under-powered rather than dropped silently | **Forest plot** by stratum |
| **Bootstrap stability** | dataset, results set | resamples (default 1000) | point estimate, lower, upper, sign consistency, stable flag | n >= 30 | **Interval plot**; histogram of resampled estimates |
| **Confounder check** | dataset, candidate confounders | - | effect before and after adjustment, delta | Confounders registered dimensions | **Slope chart** before to after |

**These five are not user-selectable steps.** They are applied to every association result, and their outputs are stored with the finding as data. A user may inspect them; a user may not switch them off.

## 5.5.5 Group D - Process and quality specific

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Control chart** | dataset | measure, subgroup, chart kind | centre line, control limits, violations by rule | n >= 25 subgroups | **Control chart** (line with limit bands) |
| **Capability** | dataset | measure, specification limits | Cp, Cpk, Pp, Ppk | Specification limits present; approximate normality checked | **Histogram** with specification limits |
| **Pareto of causes** | dataset | cause dimension, measure | descending contribution with cumulative percentage | - | **Pareto** |
| **Yield decomposition** | dataset | stage dimension, good and total measures | yield per stage, cumulative | Stages ordered | **Waterfall** |
| **Downtime impact split** | downtime dataset | - | stopped minutes and **production-impact minutes** side by side per cause | **Both quantities present**; else *"This dataset has no production-impact minutes. Impact cannot be computed from stopped minutes alone."* | **Grouped bar**, two series |
| **Transition analysis** | dataset with genealogy | - | outcome rates for transition versus non-transition units | `is_transition` present | **Bar** with confidence intervals |
| **Window comparison** | dataset | two windows, measure | difference, confidence interval, significance | Both windows non-empty | **Bar** with intervals; slope chart |

## 5.5.6 Validating the block diagram

The rules the S3 validator enforces at drag time, in addition to the universal wiring rules of Part 2:

| Rule | Refusal |
|---|---|
| A statistical block's measure input must be numeric | "`<col>` is text; `<block>` expects a number." |
| A grouping input must be a dimension of acceptable cardinality | "`<col>` has `<n>` distinct values; grouping needs fewer than `<limit>`." |
| An outcome must be registered in the outcome registry | "`<outcome>` is not a registered outcome. Register it or choose another." |
| Cross-grain analysis requires genealogy | "`<parameter>` is at `<grain A>` and `<outcome>` is at `<grain B>`. Add a Genealogy attribution block between them." |
| A discipline block cannot be deleted from the chain | "False-discovery control is applied to every association result and cannot be removed." |
| Sample-size preconditions are checked before Run | "This definition needs 30 units; the current window has 18. Widen the window or wait." |

**Every one of these is checked before the run, not after.** The author learns the constraint while authoring, which is the entire point of a debug log with described severities.

---

## 5.6 AI AND MACHINE LEARNING

**Reading order of 5.6.** 5.6.1 position; 5.6.2 feature blocks; 5.6.3 model blocks; 5.6.4 prediction and recommendation blocks; **5.6.4a practice authoring blocks**; **5.6.4b the practice-learning engine**; **5.6.4c the predict-then-remediate pipeline**; **5.6.4d the remediation eligibility and safety gate**; 5.6.5 model governance; 5.6.6 validating the ML block diagram; 5.6.7 the model-serving path; **5.6.7a the model fallback policy**. The four `5.6.4x` subsections run authoring, then engine, then pipeline, then gate, so each reads on from the one before it.

## 5.6.1 Position

Machine learning is the **fourth and fifth capability layers**: prediction and recommendation. It runs on the same feature store, behind the same readiness gate, under the same job executor and the same honesty contract. **It is not a separate product and it never bypasses the gate.**

Licence: Pro Plus upward.

## 5.6.2 Group E - Feature engineering blocks

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Feature assembly** | canonical dataset | grain, window, feature list from the registry | feature matrix: unit, features, label | Every feature registered; grain declared | **Table**; heatmap of feature coverage |
| **Genealogy roll-up** | parent-grain features | aggregation (weighted mean, min, max, sum) | features at child grain | Weights sum to 1.0 per child | **Bar** of contribution per parent |
| **Lag feature** | time series | lag steps | lagged columns | Time column present | **Line** with lagged overlay |
| **Rolling window feature** | time series | window, statistic | rolling column | Window <= series length | **Line** with band |
| **Binning** | measure | bin strategy, count | ordinal column | Numeric | **Histogram** with bin edges |
| **Encoding** | dimension | one-hot or ordinal | encoded columns | Cardinality <= 50 for one-hot | **Bar** of category frequency |
| **Missing-value policy** | feature matrix | drop, impute mean/median, flag | matrix plus indicator columns | **Refuses silent imputation**: the policy is explicit and recorded with the model | **Bar** of missingness before and after |
| **Scaling** | feature matrix | standard or min-max | scaled matrix plus stored parameters | Parameters stored with the model so scoring reuses them | **Box plot** before and after |

**The missing-value policy block is deliberately mandatory.** A model trained on silently imputed data produces a confident wrong answer, which is the exact failure this product exists not to commit.

## 5.6.3 Group F - Model blocks

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Train/validation split** | feature matrix | strategy: **time-based** (default) or stratified random; ratio | two matrices | **Time-based is the default and random is warned**: *"Random splitting leaks future information in a process dataset. Time-based split recommended."* | **Timeline** of the split |
| **Classification model** | training matrix | algorithm, hyperparameters | model artifact, metrics | Minority class >= 3 percent; n >= gate minimum | **ROC curve** and **confusion matrix** |
| **Regression model** | training matrix | algorithm, hyperparameters | model artifact, metrics | n >= gate minimum | **Predicted-versus-actual scatter**; residual plot |
| **Anomaly detection** | matrix | method, contamination | anomaly score per unit | n >= 100 | **Scatter** with anomalies highlighted; time series with markers |
| **Clustering** | matrix | method, k or auto | cluster label, silhouette | n >= 50 | **Scatter** on two components, coloured by cluster |
| **Feature importance** | trained model | method (permutation preferred) | importance per feature with confidence | Model trained; **permutation importance preferred over impurity**, which is biased toward high-cardinality features | **Bar**, descending, with intervals |
| **Partial dependence** | trained model, feature | grid resolution | response curve | Feature in the model | **Line** with confidence band |
| **Model evaluation** | model, validation matrix | metric set | accuracy, precision, recall, F1, AUC, or RMSE, MAE, R-squared | Validation set untouched by training | **ROC**, **precision-recall**, **calibration plot** |
| **Calibration** | model, validation matrix | method | calibrated model | Classification only | **Calibration plot** |
| **Scoring** | model, live matrix | - | score, class, drivers per unit | Feature schema matches training exactly, else *"The model was trained on `<n>` features; this dataset provides `<m>`. Retrain or align."* | **Distribution** of scores; **table** of top-risk units |

## 5.6.4 Group G - Prediction and recommendation

**This is the capability the guideline describes: predict early, then remediate downstream.**

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Early-stage risk score** | scoring output at an early grain | horizon, threshold | risk score and class per unit, with drivers | Unit has not yet reached the outcome stage | **Table** of at-risk units; **distribution** of scores |
| **Downstream remediation search** | risk output, historical practice dataset | candidate later-stage practice set | ranked practices with historical outcome rates and support | **Each candidate needs >= 20 historical cases**, else it is reported as insufficient support rather than recommended | **Bar** of outcome rate by practice, with support shown |
| **Practice comparison** | historical dataset | practice dimension, outcome | outcome rate per practice with confidence | Every practice n >= 20 | **Forest plot** |
| **Suggestion generation** | risk output, remediation output | thresholds | suggestions with evidence handles and expected effect | **Every suggestion carries resolvable evidence**; one without is not emitted | **Card list** with evidence links |
| **Value attachment** | suggestion, cost assumptions | - | bounded euro range | Cost inputs present, else `InsufficientBasis` | **Interval bar** |

**The remediation block produces historically supported remediation candidates - templates, not recommendations.** Whether any candidate becomes an operational recommendation for a specific unit is decided **solely by the nine-check eligibility and safety gate of 5.6.4d**, evaluated per prediction. Minimum support is one of those nine checks and is not by itself sufficient.

**This block is the product's most valuable output and the one requiring the most discipline**, because its output is read by a person who may act on it in a live plant. **The product produces evidence-backed recommendations for human decision. It never sends a control instruction and never performs a plant action.** Three constraints follow at generation time, and each is enforced:

1. **A minimum historical support of 20 cases.** Below that the block reports insufficient support and recommends nothing.
2. **Evidence handles on every suggestion**, resolving to the historical cases that justify it.
3. **Human approval before anything is acted on.** The product suggests. It never instructs and never acts.


## 5.6.4a Practice authoring blocks - Group G2 (guideline 1.3.b)

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Practice reconstruction** | canonical dataset | context (grade family, route), parameter set, period bucketing | practice signature per period: the parameter combination and sequence in force | Parameters registered; periods non-overlapping | **Table** of signatures; timeline |
| **Practice-outcome linkage** | practice signatures, outcome dataset | outcome (productivity measure, downtime, defect class) | outcome rate per practice with support count and confidence | **Support >= 20 periods per practice**, else reported observed-but-unproven | **Forest plot**; bar with intervals |
| **Best-practice benchmark** | linkage output | - | best demonstrated practice per context, with evidence | Ranked by outcome with confidence, never by point estimate alone | **Card** with support; slope versus current |
| **Failure-practice linkage** | practice signatures, downtime and failure dataset | - | practices that preceded downtime and failures, with lead time | Same support rule | **Pareto** of failure-associated practices |
| **Drift detection** | current operation, benchmark | tolerance per parameter | drift per parameter against own best practice | Benchmark exists | **Slope chart**; control-chart style band |

These write `practice_statistics` (Chapter 3, 4.5.12) and feed the Practice Insights page and the remediation search of Group G. The support threshold and the observed-but-unproven state are the honesty contract applied to practice claims: **the plant's own best practice is a measured fact with a support count, never an anecdote promoted to a rule.**

## 5.6.4b The practice-learning engine

The block catalogue of 5.6.4a is the authoring surface. This section is the engine behind it, and it is what Chapter 3 4.5.12 persists.

### What a practice is

> **A practice is a comparable signature of how the plant was operated over one production period, in one context.**

It is not a single parameter value and not a recipe. It is the combination of the operating parameters in force, the operation sequence executed, and the context in which both occurred.

### Signature construction

| Step | Specification |
|---|---|
| **1. Parameter-window selection** | The definition declares the parameter set and, per parameter, the aggregation over the period: mean, median, a declared percentile, or the modal band. **Aggregation is declared, never inferred** |
| **2. Continuous-value normalisation** | Each continuous parameter is binned by its **declared tolerance**. A tolerance is mandatory (`PR02`) because without it no two periods are ever comparable. Bins are absolute or relative to the specification band, declared per parameter |
| **3. Categorical values** | Taken as imported, with an optional declared grouping map |
| **4. Sequence representation** | The operation sequence is an ordered list of operation codes, optionally with declared dwell bands. Two periods with the same operations in a different order are **different practices** |
| **5. Hashing** | The normalised parameter bands plus the sequence plus the declared context dimensions are canonically ordered and hashed to `signature_hash`. Identical operation produces an identical hash, which is what makes a practice countable |

### Windowing

Periods are bounded by **process reality, not the clock**: an operation sequence boundary, a campaign or grade change, or a declared maximum duration, whichever comes first. Overlapping periods are a refusal (`PR03`), because an overlapping period double-counts its outcome. A period with insufficient parameter coverage is excluded and counted as excluded.

### Context and the comparison cohort

**Context** is the set of declared dimensions that must match for two periods to be comparable: typically grade family, route and specification. **The comparison cohort** for a signature is every other signature in the same context and window class, above a declared minimum cohort size (`PR05`). Comparing a practice against the whole plant rather than against its own context is the most common way this analysis produces nonsense, so the cohort is explicit.

### Outcome linkage

Per period: productivity (units or tonnes per available hour), yield, quality event rate by class, downtime minutes **and** production-impact minutes, energy where registered, and any other registered outcome. Each outcome's **direction of goodness comes from the registry** and is required (`PR07`).

### Confounders, lead and lag

Declared confounders are stratified and survival is reported per stratum. **Lead and lag rules**: a failure-practice association declares the maximum lead time within which a subsequent failure counts as associated, and a productivity association declares the lag within which the outcome is attributed. Both are declared per definition, never assumed.

### Support, confidence and ranking

| Rule | Specification |
|---|---|
| **Minimum support** | Declared per definition with a floor of 20 comparable periods. Below it the signature is stored `observed_unproven` and **the database CHECK forbids `benchmark`** |
| **Confidence** | A bootstrap interval over the periods in the signature, plus the cohort's own interval for comparison |
| **Best-practice ranking** | By outcome with confidence, never by point estimate alone. A signature whose interval overlaps the cohort's is not ranked above it |
| **Failure-practice ranking** | By association strength with lead time, under the same false-discovery control as any other association |
| **Stability** | A signature whose ranking does not survive resampling is flagged and excluded from the benchmark set |

### Similarity, back-off and tolerance sensitivity

Exact hashing of many parameter bands, an operation sequence and several context dimensions **fragments**: a plant can generate thousands of exact signatures that are each below minimum support while operationally similar practices plainly exist. The engine therefore carries a governed back-off, and it discloses everything it did.

**The back-off ladder.** Levels are declared per definition, ordered, and applied only to a signature that fails support at the level above. **Level 0 is always exact.**

| Level | Relaxation | Declared by |
|---|---|---|
| **L0 Exact** | The signature as constructed | Always present |
| **L1 Widened tolerance** | Each continuous parameter's band widened by a declared factor, applied to a declared subset of parameters in a declared order | `backoff.toleranceFactor`, `backoff.parameterOrder` |
| **L2 Coarsened dimensions** | Declared low-information parameters dropped from the signature, or collapsed to a coarser band set | `backoff.coarsenable[]` |
| **L3 Sequence generalisation** | The operation sequence generalised to a declared equivalence class - for example order-insensitive within a declared group, or dwell bands removed | `backoff.sequenceRule` |
| **L4 Context widening** | A declared context dimension widened to its parent level in a registry hierarchy | `backoff.contextHierarchy[]` |
| **L5 Weighted similarity** | Nearest-neighbour grouping by a declared weighted distance over the normalised bands, with a declared maximum distance | `backoff.weights`, `backoff.maxDistance` |

**Governance, so back-off cannot become a way to manufacture support.**

| Rule | Specification |
|---|---|
| **Ordered and declared** | The ladder is part of the practice definition version. It is never chosen at run time and never inferred |
| **Stops at first sufficiency** | The engine descends one level at a time and **stops at the first level that reaches minimum support**. It never continues to a level that produces a more flattering number |
| **Never crosses a declared boundary** | A dimension marked `mustMatch` in the definition - typically specification and route - is **never relaxed at any level** |
| **Support is never pooled across contexts** | Widening a context at L4 forms a new, explicitly labelled cohort; it does not merge two contexts' statistics |
| **Minimum support is unchanged** | The floor of 20 applies at whatever level was used. Back-off changes which periods are comparable, never how many are required |
| **State is distinct** | A benchmark reached by back-off is stored with `similarity_level > 0` and renders as **"benchmark (relaxed)"**, never as an exact benchmark |

**Mandatory disclosure.** Every practice statistic reached by back-off discloses, on the surface and in the export: **exact support** at L0, **relaxed support** at the level used, the **similarity level**, **which dimensions were relaxed and by how much**, and the **rule that was applied**. A relaxed benchmark that does not disclose its level is a defect.

**Tolerance-sensitivity test.** For every benchmark, the engine re-evaluates at the declared tolerance and at a declared sensitivity band around it - by default plus and minus one band step - and reports:

| Output | Meaning |
|---|---|
| `sensitivity_stable` | The signature remains a benchmark, and keeps its rank order against the cohort, across the whole band |
| `sensitivity_fragile` | It remains a benchmark but changes rank, or loses support at one end of the band |
| `sensitivity_unstable` | It ceases to be a benchmark within the band. **It is demoted to `observed_unproven`** and the reason is disclosed |

A `sensitivity_fragile` benchmark is presented with the flag visible on Chapter 3 4.4 D10 and **is not converted into a remediation candidate**. Only `sensitivity_stable` benchmarks are eligible for conversion.

**Persistence.** `practice_statistics` gains `similarity_level smallint NOT NULL DEFAULT 0`, `exact_support_count integer NOT NULL`, `relaxed_support_count integer`, `relaxed_dimensions jsonb`, `backoff_rule varchar(50)`, `sensitivity_state varchar(20)` CHECK IN (`stable`,`fragile`,`unstable`), `sensitivity_detail jsonb`. **CHECK `similarity_level = 0 OR relaxed_support_count IS NOT NULL`**; and the existing benchmark CHECK is extended to `state <> 'benchmark' OR (support_count >= 20 AND sensitivity_state <> 'unstable')`.

**Refusals.** `PR08` a back-off level that would relax a `mustMatch` dimension; `PR09` a weighted-similarity level with no declared weights or no maximum distance; `PR10` a sensitivity band wider than a declared ceiling.

### 5.6.4b.1 Multi-objective operating optimum — practice under trade-offs

Single-outcome practice learning remains valid for questions such as “which practice had the lowest defect rate?” It is **not** sufficient for the broader question “which operating combination is best overall?”

For an authored Objective Set, the practice engine evaluates each eligible signature as a vector `v = (o1, o2, ... on)` over the registered objectives. Every member keeps its native unit and direction; comparison uses its Performance Reference semantics rather than sample-dependent min/max scaling.

**Constraint pass.** Hard constraints are evaluated first. A practice violating a declared safety/quality/business constraint is `ConstraintFailed` and is not promoted merely because another objective is excellent.

**Pareto dominance.** Practice A dominates B iff A is no worse than B on every resolved objective under directionality and is materially better on at least one, after the declared tolerance/uncertainty rule. A non-dominated practice belongs to the supported frontier.

**Choosing one practice.**

- `ParetoOnly`: return every supported non-dominated practice; **no single winner**.
- `DeclaredUtility`: after reference-based normalisation, apply the exact versioned utility weights/functions declared by the customer.
- `LexicographicOrConstrained`: satisfy hard constraints and declared objective priority in order.

If a caller asks for one “best” practice while multiple non-dominated practices remain and no preference resolves the trade-off, return `MO01 objective_preference_undeclared`. **The engine never creates default weights and never averages productivity, quality, energy, downtime or cost into an undocumented score.**

Every `composite_practice_result` exposes: objective vector, support, uncertainty, dominance state, any resolved rank, preference version, cohort/context, semantic manifest and evidence. The Assistant describes the trade-off explicitly: e.g. “Practice A is higher-throughput; Practice B uses less energy; neither dominates under the current objective set.”

### Incremental recomputation

Only contexts whose periods changed since the last run are recomputed; others carry forward with their prior statistics and their `computed_at_utc`. **A definition change or a relationship change invalidates every context**, because a signature computed under two different tolerance sets is not one signature.

### Persistence, APIs, scheduling, evidence, conversion

Tables: `practice_signatures`, `practice_statistics` including the similarity, back-off and sensitivity columns, `practice_drift_observations`, `practice_learning_runs` (Ch3 4.5.12). APIs: the `/api/practices` family of Ch3 DF12, whose payload carries the full disclosure envelope. Scheduling: class `analysis`, **a required dependency on the practice feature refresh** so it never runs on stale features. Evidence: every statistic drills to its periods, then to units and source rows along JP5. Conversion: a `benchmark` practice whose context matches a predicted early condition becomes a `remediation_candidate`, carrying its support count, its expected effect and its limitations.

### Presentation

D10 Practice Insights and D12 Benchmarking (Ch3 4.4). **A benchmark always renders with its support count**; an `observed_unproven` signature renders as observed with its count and is never styled as a recommendation.

### Monitoring whether it worked

When a practice is recommended and adopted, `practice_drift_observations` records the movement toward the benchmark, and `remediation_effectiveness` records whether the outcome followed. **A benchmark that is adopted and does not deliver is surfaced**, because a benchmark that survives its own failure is a superstition.

## 5.6.4c The predict-then-remediate pipeline

The complete lifecycle, fifteen stages, each with its owner, its persistence and its refusal.

| # | Stage | Owner | Persists | Refusal |
|---|---|---|---|---|
| 1 | **Canonical delta arrives** | DF5 projection | canonical rows, batch lineage | quarantine per `PV` |
| 2 | **Affected feature grains refreshed** | DF10, dependency-aware | `feature_store`, watermark | `FS04` on relationship change |
| 3 | **Correct model version selected** | DF13 | `prediction_runs.model_registry_id` | `PD01` schema mismatch, `PD02` no active model |
| 4 | **Units scored** | DF13, `ml` pool | `prediction_runs` | `PD04` gate blocked; run persisted blocked |
| 5 | **Predictions and drivers stored** | DF13 | `predictions`, `prediction_drivers`, `prediction_comparables` | - |
| 6 | **Early Warning queue updates** | DF13 | `prediction_current` refreshed | a failed run leaves the prior projection intact |
| 7 | **Engineer opens the explanation** | D9 | - | driver absent renders as unexplained, not as zero |
| 8 | **Historically supported remediation generated** | DF13 remediation search | `remediation_candidates` | `PD05` insufficient support, count shown; `PD06` stage already passed, suppressed |
| 8a | **Eligibility and safety gate evaluated per prediction** | DF13, gate of 5.6.4d below | `remediation_candidates.eligibility_state`, `failed_checks` | `RM01` to `RM09`; only `actionable` is presented as a recommendation |
| 9 | **Engineer accepts, rejects or defers** | DF14 | `prediction_actions` | `DC01` rejection without a reason |
| 10 | **Action assigned** | DF14 | `prediction_actions.assignee_id`, `due_stage`, `prediction_current` | - |
| 11 | **Action performed at a downstream stage** | DF14 | `actual_action`, `action_at_utc`, `action_process_stage` | `DC02` stage already passed |
| 12 | **Actual outcome arrives** | DF5, then DF14 | canonical quality or downtime rows | `DC03` a hand-entered outcome is refused |
| 13 | **Prediction correctness evaluated** | DF14 | `prediction_evaluations` | `DC04` before the horizon elapsed; `pending` until then |
| 14 | **Remediation effectiveness evaluated** | DF14 | `remediation_effectiveness`, against the comparable cohort | `inconclusive` where the cohort is too small |
| 15 | **Feedback affects governed model and practice review** | DF14, then DF15 | `feedback_records`, then `supervisor_proposals` | `DC06` incomplete feedback excluded, visibly; `DC07` concentration flagged. **Nothing retrains automatically** |

**Two properties of the whole pipeline.** It is **restartable at any stage** because every stage's output is persisted and idempotent. And it is **explainable end to end**: from an evaluation row, JP5 reaches the source rows, the model version, the feature snapshot, the drivers, the decision, the action and the value.

---

## 5.6.4d The remediation eligibility and safety gate

**A historical difference is not an operational recommendation.** A parameter may be non-controllable at the remaining stages, outside the specification, part of a forbidden combination, merely correlated rather than useful to change, or unsafe. Every candidate therefore passes a formal gate **before it is presented as actionable**, and the gate is evaluated per prediction rather than once per candidate, because eligibility depends on where that unit currently is.

### The nine checks

| # | Check | Passes when | Refusal |
|---|---|---|---|
| 1 | **Controllability** | Every parameter the candidate would change is declared controllable at the proposed stage in the registry, with a declared adjustment range | `RM01` non-controllable parameter, naming it |
| 2 | **Remaining actionable stage** | The proposed stage is still ahead of the unit on its declared route, with a declared minimum lead time before it | `RM02` stage already passed or too close |
| 3 | **Operating and specification limits** | The proposed values sit inside `operating_limits` and `product_specifications` for that unit's specification | `RM03` proposed value outside a limit, naming the limit |
| 4 | **Forbidden combinations and safety constraints** | The proposed combination violates no declared safety rule or forbidden-combination rule | `RM04` forbidden combination, naming the rule |
| 5 | **Historical support** | Support count at or above the floor, at the disclosed similarity level | `RM05` insufficient support, count shown |
| 6 | **Contextual and confounder survival** | The association survives stratification by the declared confounders in the unit's own context | `RM06` does not survive stratification, naming the stratum |
| 7 | **Uncertainty** | The expected-effect interval excludes no-effect, or the candidate is explicitly marked exploratory | `RM07` effect interval spans no effect |
| 8 | **Causal or uplift evidence, where data permits** | Where enough natural variation exists, an uplift estimate over comparable non-adopters supports the effect; where it does not, the candidate is marked **association-only** | `RM08` uplift contradicts the association |
| 9 | **Sensitivity** | The source practice is `sensitivity_stable` per 5.6.4b | `RM09` fragile or unstable source practice; `RM10` accept attempted on a non-actionable candidate |

### Outcomes

| Outcome | Condition | How it is presented |
|---|---|---|
| **Actionable** | All nine pass | A **remediation card** with the proposed practice, support, expected-effect interval, evidence and limitations. The only outcome styled as a recommendation |
| **Evidence only** | Checks 5 to 9 pass, but 1 to 4 fail for this unit | Shown in the drill-down as **"observed historical difference - not actionable here"**, with the failing check named. **Never styled as a recommendation and never carrying an accept action** |
| **Suppressed** | Check 4 fails on a safety constraint | Not shown at all in the card list; recorded on the run with `RM04` so the suppression is auditable rather than invisible |
| **Exploratory** | Checks 1 to 6 pass, 7 or 8 fail | Shown behind an explicit **"exploratory"** disclosure with the uncertainty and the failed check stated. **No accept action, at any tier, for any role.** It may be inspected, compared, and escalated for engineering investigation - it may not be accepted as a remediation, because an uncertainty or causal check did not pass |

### Persistence, API and audit

`remediation_candidates` gains `eligibility_state varchar(20) NOT NULL` CHECK IN (`actionable`,`evidence_only`,`exploratory`,`suppressed`), `failed_checks jsonb`, `controllability_detail jsonb`, `uplift_estimate jsonb`, `uplift_basis varchar(20)` CHECK IN (`uplift`,`association_only`,`insufficient_data`), `gate_evaluated_at_utc`. **CHECK `eligibility_state <> 'actionable' OR failed_checks IS NULL OR jsonb_array_length(failed_checks) = 0`** - an actionable candidate with a failed check is a database-level impossibility.

Registry additions consumed by check 1 and check 4: `registry_dimensions.is_controllable`, `controllable_at_stages text[]`, `adjustment_range jsonb`; and `ppiq_plant.forbidden_combinations` (`rule_code`, `scope_kind`, `scope_id`, `expression`, `severity`, `justification`, provenance triple), which is **imported or authored by the customer, never shipped**.

`GET /api/predictions/{id}/remediations` returns every candidate with its `eligibility_state` and `failed_checks`; the client renders only `actionable` as cards. `GET /api/predictions/{id}/remediations/gate` returns the full nine-check evaluation for audit. Every gate evaluation is written to the job log with its outcome, so a suppressed candidate is recoverable in an investigation.

**The binding rule, stated once and without exception.** **The entire remediation decision boundary - Accept, Reject and Defer - exists only where `can_accept` is true**, which per Chapter 3 4.5.12a requires `eligibility_state = 'actionable'` and six further situational conditions. `evidence_only`, `exploratory` and `suppressed` candidates carry **no decision control of any kind, at any tier, for any role**.

They are not merely un-acceptable: they are **outside the decision record entirely**. Rejecting or deferring an observation would enter it into the effectiveness and feedback statistics as though it had been offered as a recommendation, which would corrupt exactly the measurements the product exists to produce.

An exploratory or evidence-only candidate may be **inspected**, **compared** against its cohort, and **escalated for engineering investigation** through `POST /api/predictions/{id}/escalate`, which records the escalation and its reason and **never creates a remediation decision**.

**`RM10` protects the complete boundary**, not Accept alone: any decision verb attempted where `can_accept` is false is refused with it. Chapter 3 4.5.12a, the D9 contract, and the error catalogue all carry this same meaning.

## 5.6.5 Model governance

| Requirement | Rule |
|---|---|
| Registry | Every trained model is registered with version, algorithm, feature list, training window, split strategy, missing-value policy, scaling parameters and metrics |
| Reproducibility | A registered model can be retrained to the same result from the recorded definition |
| Drift monitoring | Feature distribution and performance monitored; drift beyond a threshold moves the model to a review state and **stops scoring** |
| Retirement | A retired model stops scoring; its historical scores remain readable and labelled with the retired version |
| Determinism | Scoring is deterministic. The same input and the same model version produce the same score |
| **No language model in the compute path** | Recorded as data on every result |

## 5.6.5a Promotion is a three-dimensional gate (C4-4, C4-5)

A candidate is promoted only when it passes **all three groups** on the **same governed recent holdout** as the incumbent.

**QUALITY** - discrimination or error above the incumbent or within a declared non-inferiority margin; **calibration** at or below the declared error ceiling; out-of-time performance on a window after the training window; subgroup and regime stability with no variant below its floor; missingness robustness; **explanation stability**, contributor rank correlation across bootstrap resamples above a floor.

**SERVING** - p50, p95 and p99 inference latency; throughput; artifact size; RAM and VRAM; warm-up time.

**TRAINING** - training duration against the weekly window; peak memory against lane capacity; snapshot read throughput.

> **A better-discriminating, worse-calibrated model is not an improvement** for a product whose output is a risk band a human acts on.
>
> **An unstable explanation is worse than none**, because the product presents contributors as evidence.

**Encoder promotion inequality (C4-5):**

```
promote_encoder  iff  metric_lift            >= declared_min_lift
                 AND  p95_latency_delta      <= declared_latency_budget
                 AND  artifact_size          <= declared_size_class
                 AND  explanation_stability  >= floor
```

**If engineered features match the encoder within the lift threshold, the engineered features ship.** Deep learning being available is not a reason to deploy it.

`model_registry.metrics` and `acceptance_floor` are `jsonb` and carry these dimensions. **No schema change is required.** Values: **B-05**.

## 5.6.5b Family taxonomy (C4-8)

**MF-01 to MF-07 are seven intelligence and engine families, not seven ML models.** Three of the seven are not models, and the sub-type determines lane, refresh policy and whether a champion/challenger gate applies at all.

| ID | Family | Sub-type | Lane | Champion/challenger |
|---|---|---|---|---|
| MF-01 | Process encoder | Learned model | `ml.training` | Yes, plus the inequality above |
| MF-02 | Similarity index | **Retrieval and index** | `ml.training` to build | No. Gated on measured recall@k |
| MF-03 | Normal and novelty | Learned model | `ml.training` | Yes |
| MF-04 | Supervised outcome | Learned model | `ml.training` | Yes, three-dimensional |
| MF-05 | Effect and envelope | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-06 | Statistical intelligence | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-07 | Practice learning | **Practice engine** | `analysis` | No. Governed signature version |

Plus **orchestration and governance**: the capability profiler, the model-count governor and the supervisor.

## 5.6.6 Validating the ML block diagram

| Rule | Refusal |
|---|---|
| Feature schema mismatch at scoring | Named, with the counts on both sides |
| Validation set touched by training | "The validation set overlaps the training set by `<n>` rows." |
| Random split on a time-ordered dataset | Warning with the leakage explained |
| Class imbalance below the gate minimum | Blocked by the gate, with the measured balance |
| Silent imputation | Refused; a missing-value policy block is required |
| Recommending on insufficient support | Reported as insufficient support, never as a recommendation |

---

## 5.6.7 The model-serving path

| Concern | Specification |
|---|---|
| **Model loading** | Artifacts loaded from object storage on first use per worker, keyed by the full serving version `(tenant_id, model_code, outcome_code, grain_code, model_version)` per Chapter 3 4.5.12 |
| **Warm-up** | On activation, a warm-up scoring pass over a fixed sample runs before the model is marked servable, so the first real batch does not pay initialisation |
| **Version cache** | An LRU cache of loaded models per worker, bounded by memory, with the eviction recorded |
| **Batch and micro-batch** | Scheduled scoring uses full batches; the near-real-time path of 5.8.8 uses micro-batches with a declared maximum size and a maximum wait |
| **Maximum batch size** | Declared per model; a larger scope is split into batches, never one oversized call |
| **Queue** | Scoring requests queue in the `ml` pool with the weighted admission of 5.3; a full queue returns a stated wait rather than blocking |
| **Latency budget** | Declared per model and per trigger kind; exceeding it emits a warning event and is recorded on the run |
| **Timeout** | Per batch; a timed-out batch fails that batch only and the run reports partial with the batch identified |
| **Fallback when unavailable** | **Only to a version explicitly approved as `serving_fallback`**, never to "the last active version". See the fallback policy below |
| **Feature-schema compatibility** | Checked before loading, not after; `PD01` with counts on both sides |
| **Retirement** | Retirement stops serving immediately and evicts the cache; historical scores remain readable, labelled with their version |
| **Deployment and rollback** | Activation is a single audited transition with at most one active version per model code; rollback is activation of the prior version, equally audited |
| **Memory isolation** | Model inference runs in the `ml` pool with weight 4 by default, so one model cannot starve the import or interactive paths |
| **Tenant isolation** | A loaded artifact is keyed by tenant; a model artifact is never shared across tenants even when identical |
| **Scoring telemetry** | Per run: units scored, batches, latency percentiles, cache hits, fallbacks, timeouts. Exposed on D8 and in the job log |

## 5.6.7a The model fallback policy

"Fall back to the last active version" is too broad: a prior version may have been retired, may have drifted, may be incompatible with the current feature set, or may have been withdrawn for a quality reason. Falling back to it silently would score a plant on a model the platform itself had rejected.

**A fallback candidate is used only when all six conditions hold.**

| # | Condition | Checked against |
|---|---|---|
| 1 | **Explicitly approved as a fallback** | `model_registry.serving_role = 'serving_fallback'`, set by an audited approval action, never inferred |
| - | *The two axes, stated once* | **Lifecycle**: `status` in `trained`, `rejected`, `active`, `review`, `retired`. **Serving**: `serving_role` in `none`, `serving_fallback`. A model is the active primary when `status = 'active'`; it is an approved fallback when `serving_role = 'serving_fallback'`. The two are independent and are never encoded in one column |
| 2 | **Lifecycle status permits serving** | `status IN ('active','trained')`; a `retired`, `rejected` or `review` model is never eligible. **Lifecycle status and serving role are separate axes**: `status` is the model's life stage, `serving_role` is its explicit serving approval. There is no `fallback_approved` status |
| 3 | **Schema-compatible with the current feature set** | The model's `feature_set_version_id` matches, or its feature list is a verified subset with identical types |
| 4 | **Within current drift and validity limits** | The latest `model_drift_observations.verdict` is not `drifted`, and the model's validity window has not expired |
| 5 | **Same serving identity** | The candidate's `(tenant_id, model_code, outcome_code, grain_code)` equals the requested one. **The full identity, never a shorter match** |
| 6 | **Passes its own acceptance floor** | `acceptance_floor` met on its recorded metrics |

**If no candidate satisfies all six, there is no fallback.** The run records `PD08 no valid fallback`, the queue **states that scoring is unavailable for that outcome with the reason**, and the prior `prediction_current` projection is left intact. **Silently scoring on an unsafe model is prohibited.**

**When a fallback is used** it is recorded on `prediction_runs.fallback_model_registry_id` with `fallback_reason`, every prediction produced under it carries the fallback model version, and **the Early Warning queue displays a persistent notice naming the fallback version and the reason**. A fallback is a visible degraded mode, never a silent substitution.

**Approval.** `POST /api/models/{id}/approve-fallback` with a justification, audited, and refused where any of conditions 2 to 6 already fail at approval time. **The approval and removal workflow, the fallback readiness panel and the current-fallback summary are specified on Chapter 3 4.4 D8**, so the governance exists in the interface and not only at the API. `serving_role` and `fallback_approved_by` are added to `model_registry`; a partial unique index permits at most one `serving_fallback` **per serving identity `(tenant_id, model_code, outcome_code, grain_code)`**, and a CHECK forbids one version being simultaneously the active primary and the approved fallback for that identity.

## 5.7 THE AI ASSISTANT

## 5.7.1 The form factor: a persistent dock, not a page

> **A chat box at the inline-end, block-end corner, present on every page.**

This is a correction to an earlier specification that treated the assistant as a destination page. **It is not a page. It is a dock.**

| State | Appearance | Behaviour |
|---|---|---|
| **Collapsed** (default) | A circular launcher, 56 px, inline-end and block-end, offset 24 px, Electric Blue with the assistant glyph | Unread-answer badge; hover reveals "Ask about this page" |
| **Expanded** | A panel 400 px wide, 600 px tall, anchored to the same corner, Panel Navy with a 1 px Industrial Blue border and a soft shadow | The conversation, the composer, the evidence strip |
| **Docked wide** | 640 px, pinned; page content reflows rather than being covered | For an extended investigation |
| **Full** | The whole viewport | Only when the user chooses it |

**Rules that make it liveable:**

- **It persists across navigation.** Moving from the workspace to Findings does not lose the conversation.
- **It never covers a primary action.** Collapsed it occupies a corner; expanded it offsets the page's floating controls.
- **Its state is per user and remembered**: collapsed or expanded, width, and the last conversation.
- **Escape collapses it. A keyboard shortcut opens it and focuses the composer.**
- **On mobile it becomes a full-height sheet** rather than a floating panel.
- **It mirrors correctly** in a right-to-left locale: the dock moves to the other inline edge, because its position is expressed as inline-end and never as "right".

## 5.7.2 Page context awareness

**The dock knows what page it is on, and this is most of its value.**

On open, the client sends a context envelope: the route, the page definition code, the current associative selection, the visible time window, and the selected entity if there is one.

| Where it is opened | What it offers unprompted |
|---|---|
| Interactive Workspace | "This page is filtered to `<selection>`. Ask about what you are seeing." |
| Findings | "Ask why this finding was ranked first, or what it is worth." |
| Genealogy Explorer | "Ask about this unit's ancestry or its parameters." |
| Jobs Monitor | "Ask why a job was blocked." |
| Transformation Studio | "Ask what a block does or why a wire was refused." |

**Context narrows retrieval; it never widens permission.** A user who cannot see a page's data cannot reach it by asking from that page.

## 5.7.3 Composition and the honesty contract

```
question + page context
   |
   v
[ intent + entity resolution ]     glossary, synonyms, registry
   |
   +--> [ RETRIEVAL ]   role-scoped chunks: findings, datasets, mappings, connectors, documents
   |
   +--> [ TOOLS ]       typed, role-scoped, deterministic - the Engine computes, the tool returns
   |
   v
[ GROUNDING ]           every numeric claim carries a resolvable handle
   |
   v
[ NO-FABRICATION GUARD ]  a sentence with an uncited number is REJECTED BEFORE DISPLAY
   |
   v
[ EGRESS PLAN ]         what may leave the tenant, per serving mode
   |
   v
answer + citations   |   or a refusal with its reason
```

**The four rules.** Tools return Engine output and the model only phrases it. Retrieval is role-scoped at the chunk level. The guard runs before display. And **a refusal is amber and evidential while a transport failure is red and says the request failed** - a transport fault dressed as an abstention is a lie about the product's own state.

## 5.7.4 The panel, region by region

| Region | Contents |
|---|---|
| **Header** | Title "Assistant", context chip naming the current page, expand-width, full-screen, close |
| **Conversation** | Messages newest at the block-end. Each answer carries citation chips beneath it |
| **Citation chip** | Electric Cyan, labelled with the evidence kind and its identifier. Click opens the evidence strip |
| **Evidence strip** | Slides from the block-end: the finding, the run, the population, the rows. Includes **Open in page** |
| **Composer** | Auto-growing text area, Enter sends, Shift-Enter newlines, Send button inline-end |
| **Suggested questions** | Three context-derived starters on an empty conversation, from the registry, never a hardcoded list |
| **Footer note** | "Answers are assembled from your plant's data with citations. Figures are computed by the engine." |

## 5.7.5 States

| State | Rendering |
|---|---|
| Thinking | Streamed, with the tool being used named: "Reading findings...", "Computing KPI..." |
| Answered | Text plus citation chips |
| **Refused** | **Amber** card: "I don't have evidence for that." plus what would answer it |
| **Transport failed** | **Red** card: "Request failed." plus Retry |
| Out of scope | "That is outside the data I can see for your role." |
| Index empty | "Nothing is indexed yet." Administrators get a Reindex action |
| Tier locked | The dock is absent below Pro Plus, not present and broken |

## 5.7.6 Configuration, from the interface

Per Rule 1's configurability corollary, everything is administered from E2: tools per role and per tier, indexed knowledge sources, the plant glossary and its synonyms, guardrail phrases, the citation ceiling, verbosity, and the serving mode (self-hosted, private endpoint, customer model) with the per-tenant no-egress control.

## 5.7.7 What the assistant may never do

Compute. Rank. Originate a figure. Write anything except its audit log. Answer outside its role's retrieval scope. Render a number without a resolvable citation. **Dress a transport failure as an evidential abstention.**

## 5.7.8 Acceptance

1. The dock is present, collapsed, on every page, and persists across navigation.
2. Opening it on a filtered workspace offers context-aware starters naming the selection.
3. A grounded question returns cited answers whose citations resolve to real rows.
4. An unanswerable question refuses in amber; a stopped API fails in red.
5. Stopping and restarting the API recovers without a page reload.
6. A viewer cannot retrieve engineer-scoped chunks.
7. Every figure in an answer matches the Engine's stored value exactly.
8. The dock mirrors to the other inline edge in a right-to-left locale.
9. Below Pro Plus the dock is absent rather than broken.

---

---

## 5.7.9 The Assistant runtime (C4-7)

Sections 5.7.1 to 5.7.8 are unchanged. This specifies the runtime **between the question and the model**, which is where groundedness is won or lost.

### 5.7.9.1 The pipeline

```
  user question + page context envelope
  [1] PERMISSION AND TENANT CONTEXT     resolved once, carried throughout
  [2] INTENT AND ENTITY RESOLUTION      glossary, synonyms, registry codes
  [3] DETERMINISTIC TOOL PLANNER        question shape -> declared tool set
        +-- [4a] STRUCTURED TOOLS       Layer A exact, Layer B intelligence
        +-- [4b] EVIDENCE RETRIEVAL     hybrid, permission filter BEFORE ranking
  [5] EVIDENCE PACKING                  dedup, rank, token budget, handles retained
  [6] MODEL GATEWAY                     serving mode, egress plan, minimum payload
  [7] LLM (ModelServingRuntime)         phrasing only
  [8] ANSWER VERIFICATION               deterministic, does not call the LLM
  answer + citations   |   refusal with its reason
```

### 5.7.9.2 The deterministic tool planner

**The LLM does not choose tools.** A planner maps resolved intent plus entity types to a declared tool set from a registry. Tool-selection accuracy is measured against a labelled question set (Q-01). A model choosing tools freely produces a different plan on a rephrasing and cannot be gated. Where intent is ambiguous the planner **asks** rather than guessing, and the ambiguity is recorded.

### 5.7.9.3 Hybrid retrieval, and the order that matters

| Stage | Rule |
|---|---|
| **Permission filter** | **Applied before ranking, not after.** Filtering after ranking lets a high-scoring forbidden chunk displace a permitted one, so the answer silently loses evidence the user was entitled to. `assistant_chunks.role_scope` is the mechanism |
| Lexical retrieval | Full-text, for exact codes, identifiers and rare terms where embeddings underperform |
| Semantic retrieval | Embedding search over permitted chunks |
| Fusion | Reciprocal rank fusion over both lists |
| Re-ranking | **Optional, ships only if B-08 shows citation correctness improves enough to pay for its latency** |

**Structured tools take precedence over retrieval for facts and analytical results.** A number never comes from a retrieved chunk when a tool can compute it.

### 5.7.9.4 Evidence packing under a token budget

Deduplicate by content hash. Rank by tool-result priority, then fusion score; engine output outranks a document. **Hard token budget with a reserved answer allowance**, because context overflow silently drops evidence and produces a sentence the guard then rejects. Every packed item retains its evidence handle, or verification cannot resolve it. **Truncation is recorded and disclosed.** Budget: **B-07**.

### 5.7.9.5 Gateway and egress

The payload sent to an external provider is the **minimum scoped evidence** needed for the phrasing task, never a whole retrieval set and never raw canonical rows. **A provider or model change is a governed release event**, recorded with a reason, because it changes answer behaviour with no code change.

### 5.7.9.6 `ModelServingRuntime`

A replaceable abstraction: load, unload, generate, stream, health, capability reporting. Candidate runtimes are benchmarked as implementations (**B-09**). **No serving library is the product contract.**

### 5.7.9.7 Answer verification

The no-fabrication guard of 5.7.3, given an operational definition. **The verifier is deterministic and does not call the LLM**, because a model checking its own output is not a guard.

| Check | On failure |
|---|---|
| Every numeric claim resolves to a handle in the supplied evidence | Reject before display |
| No claim class upgraded by language: an association is not phrased as a cause | Reject before display |
| No refusal replaced by a phrased answer | Reject before display |
| Transport failure red, refusal amber | Never conflated |

### 5.7.9.8 Assistant quality gates

| ID | Gate |
|---|---|
| Q-01 | Tool-selection accuracy |
| Q-02 | Groundedness: fraction of claims with a resolving handle |
| Q-03 | Citation correctness: the handle supports the claim |
| Q-04 | Unsupported-claim rate |
| Q-05 | **Refusal correctness**, including unit-sanity probes |
| Q-06 | **Causal-overreach rate**: association phrased as cause |
| Q-07 | Multilingual fidelity |
| Q-08 | Time to first token |
| Q-09 | Total answer latency, p95, against the under-2-minute ceiling |
| Q-10 | Serving throughput |
| Q-11 | Memory and VRAM per concurrent session |

**Q-05 and Q-06 decide credibility.** A speed question answered in a unit of mass, or an association phrased as a cause, destroys the intelligence claim in one sentence, and both are testable against a fixed probe set before any customer sees them.

---

## 5.8 Additional designed capabilities

Classified per Chapter 2 3.10. Every item here is designed across UI, API, persistence, validation and acceptance; none is deferred as an idea.

### 5.8.1 What-if and scenario simulation - **Advanced**

Surface D11 (Ch3 4.4). Engine: the same model registry and feature machinery, run in a **simulation context** that writes only to `scenario_runs` and never to `predictions`.

| Element | Specification |
|---|---|
| Scenario definition | `definition_kind = 'scenario'`; variables, ranges, fixed assumptions, baseline, model version |
| Variables allowed to change | Registry parameters the selected model consumes, each with its **valid operating range** from `product_specifications` and `operating_limits` |
| Fixed assumptions | Everything not varied, taken from the baseline and listed explicitly so the reader knows what was held |
| Baseline | A period, a context or a saved state; named on every result |
| Valid operating ranges | Enforced; `SC01` refuses a value outside them, because simulating an impossible operating point produces confident nonsense |
| Model used | Named on the result with its version; `SC02` where the model does not consume a chosen variable |
| Uncertainty | Every result is an interval derived from the model's own uncertainty plus the variable ranges. **Never a single point figure** |
| Result comparison | Scenario against baseline, and scenario against scenario |
| Save and compare | Scenarios are definitions; results are `scenario_runs` rows, comparable and exportable |
| **No write path** | Structural: the simulation context has no grant on any canonical or prediction table |
| Disclaimer | Permanent on the page, in the result region's accessible name, and in any export: model-based decision support, not a prediction of what will happen |

Persistence: `ppiq_plant.scenario_runs` (`definition_version_id`, `baseline_description`, `variables jsonb`, `model_registry_id`, `result jsonb`, `uncertainty jsonb`, `run_at_utc`). Acceptance: an out-of-range value is refused; two scenarios compare; no row appears in `predictions`; the disclaimer is present in the export.

### 5.8.2 Alert routing and escalation - **Core**

Surface E6, tables `alert_routing_rules` and `alert_deliveries` (Ch3 4.4, 4.5.15). Every element required is specified there: recipient roles and users, severity routing, in-app, email and webhook channels, escalation after no acknowledgement, working hours and quiet periods, deduplication, suppression, grouping, rate limiting, delivery status, retry, dead-letter handling, acknowledgement, resolution and full audit.

**Three properties worth naming.** Delivery is **at-least-once with idempotent recipients**: the unique key on `(rule, entry, recipient, channel)` prevents a duplicate. A quiet period **holds rather than drops**, and the held entry delivers at the next permitted moment with its original timestamp. And a dead letter is **visible with its reason** on the surface, never a silent loss.

### 5.8.3 Prediction explainability - **Core**

A driver list is not an explanation. The full explanation object, rendered on D9 and available from `GET /api/predictions/{id}/explanation`:

| Element | Source |
|---|---|
| Driver contribution and direction | `prediction_drivers.contribution`, `direction` |
| Current value | `prediction_drivers.current_value` |
| Expected or normal range | `normal_range_low/high`, derived from specification, operating limit or observed distribution, with the basis named |
| Historical distribution | `historical_percentile` plus a distribution sparkline of the cohort |
| Comparable successful and failed cases | `prediction_comparables`, with the later-stage difference for each |
| Genealogy stage | `prediction_drivers.genealogy_stage`, linked to C5 |
| Data freshness | The gate's freshness dimension at scoring time, from `prediction_runs.gate_evidence` |
| Model version | `predictions.model_registry_id`, linked to D8 |
| Calibration and confidence | `confidence_low/high`, `calibration_note`, and the model's calibration curve |
| Known limitations | From the model definition's declared limitations and the feature set's declared gaps |
| **"Why this unit?"** | The drivers ranked, each against the cohort's normal range - what is unusual about this unit |
| **"Why now?"** | Which driver crossed which range at which stage and when - what changed to raise the score |

**Rule:** an explanation with no resolvable driver renders as **unexplained with the reason**, never as an empty list implying no drivers exist.

### 5.8.4 The feedback loop - **Core**

Specified in DF14 and `feedback_records` (Ch3 4.5.12). The elements:

| Element | Specification |
|---|---|
| Suggestion accepted or rejected | `suggestion_decisions`, reason required on rejection |
| Reason | Free text plus an optional registry-driven reason code |
| Action performed | `prediction_actions.actual_action`, with stage and timestamp |
| Actual result | Read from canonical data only; `DC03` refuses a hand-entered outcome |
| Prediction correct or incorrect | `prediction_evaluations.verdict` |
| Remediation successful, unsuccessful or inconclusive | `remediation_effectiveness.verdict`, against the cohort |
| **Feedback quality check** | `action_record_complete` and `outcome_from_canonical` must both hold for `quality_state = 'eligible'` |
| **Human review before retraining** | Only `eligible` feedback reaches `supervisor_proposals`; a proposal still requires approval. **No path retrains a model automatically** |
| Feedback provenance | `provided_by`, `provided_at_utc`, and the subject reference |
| **Poisoning prevention** | Three mechanisms: canonical-only outcomes remove the largest attack surface; `DC07` flags concentration where one actor supplies a disproportionate share for one model; and every proposal derived from feedback carries the feedback identifiers as evidence handles, so a reviewer can see whose feedback drove it |

### 5.8.5 Internal benchmarking - **Core**

Surface D12 (Ch3 4.4). **All comparison dimensions are registry-driven**; none is shipped.

| Comparison | Mechanism |
|---|---|
| Line against line, equipment against equipment | Registry dimensions from the structure cluster |
| Shift or crew against another **where the customer registers such a dimension** | Registry only. The product does not assume a plant has shifts |
| Route against route, product family against family | Registry dimensions |
| Current month against previous periods | The time hierarchy |
| **Current operation against the plant's own best demonstrated practice** | `practice_statistics` where `state = 'benchmark'` for the matching context |

**Two refusals that keep it honest.** `BM01` refuses a comparison where either population is below the declared minimum, showing both counts rather than a difference. `BM02` refuses to colour a difference where the measure's direction of goodness is not declared in the registry, because colouring requires knowing which way is better. Normalisation (per tonne, per hour, per unit) is offered where the measure declares support, and the chosen basis appears on the result and in the export.

### 5.8.6 Unstructured text evidence - **Future extension, interfaces designed**

Operator comments, shift notes, maintenance descriptions, quality investigation notes and external reports.

| Element | Design |
|---|---|
| Ingestion | Through the same DF1 to DF3 path as any dataset; a text column is a column. Documents arrive as file datasets with their text extracted at staging |
| Source lineage | The provenance triple, unchanged; every extracted passage carries its document, page and offset |
| Persistence | `ppiq_plant.text_documents` (metadata, `storage_uri`, language, source lineage) and `ppiq_plant.text_passages` (`document_id`, `offset`, `content`, `entity_links jsonb`) |
| Access control | RLS by tenant plus `role_scope` on the passage, as with assistant chunks. **A text passage can be more restricted than the row it describes** |
| Text indexing | Full-text plus the existing embedding path; passages become `assistant_chunks` with `chunk_family = 'DOC'` |
| Language handling | Per-document language; indexing configured per language; a document in an unsupported language is stored, marked, and excluded from retrieval rather than mis-indexed |
| Extraction | Entity linking from a passage to a material, equipment or event through the registry and the relationship model, stored as `entity_links` with a confidence |
| Evidence citation | A passage is citable by the assistant and appears in an evidence drawer, with its document and offset |
| **The boundary rule (C4-6)** | **No free-form or model-generated output may become a feature, a score, a statistic or a value.** Text and images may enter a learned result **only** through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring. Retrieval-derived and LLM-derived content is **evidence only**: it may corroborate a deterministic result and may never originate one |
| **Path A, evidence modality** | Operator notes, shift logs, maintenance text, documents. Indexed, retrieved, cited. Never a feature, never a score, never a plant fact the model originated |
| **Path B, governed multimodal ML** | The full training contract above. **This is how an inspection-image model produces an annotation with a confidence** under the same activation, retirement and drift rules as any model, which 5.8.7 requires and the previous absolute wording forbade |
| **Why the wording changed** | The hazard was never the modality. It is **ungoverned output entering a score**. A free-form LLM summary has no training snapshot, no held-out validation, no calibration, no drift monitor and no leakage control. An authored vision model has all five. **No implementation scope is added**; both modalities remain interface-designed, future implementation |
| First implementation | Scheduled, not assumed. The interfaces, persistence and permission model above are designed now so that adding it later is not a re-architecture |

### 5.8.7 Inspection images - **Future extension, interfaces designed**

| Element | Design |
|---|---|
| Metadata and source reference | `ppiq_plant.inspection_images`: `material_unit_id`, `quality_event_id`, `equipment_id`, `captured_at_utc`, `storage_uri`, `content_hash`, `width`, `height`, `format`, provenance triple |
| **Object storage, not database blobs** | `storage_uri` only; the database never holds image bytes |
| Material and inspection linkage | Foreign keys to the unit and the quality event, so an image is reachable from genealogy and from a finding |
| Thumbnail and full resolution | Two derived renditions with their own URIs; the interface loads thumbnails in a list and full resolution on demand |
| Model and version registry | Image models register in `model_registry` with `algorithm` naming the vision family; the same activation, retirement and drift rules apply |
| Annotation | `ppiq_plant.image_annotations`: `image_id`, `region jsonb`, `defect_catalog_id`, `confidence`, `annotated_by` (human or model version), `reviewed_by` |
| Defect region and confidence | On the annotation, with the model version that produced it |
| Retention | Independent of the row retention, because images dominate storage; policy-driven per tenant with archival to cold storage |
| Permission | RLS plus a role scope on the image; a signed, time-limited URL for every access, and every access audited |
| Evidence citation | An annotation is citable in a finding or a prediction driver as corroboration, under the same boundary rule as text |
| Training and validation separation | An image used in training is recorded in the snapshot; **an image in a validation partition is never in training**, enforced by the same `overlap_rows = 0` CHECK pattern |
| First implementation | Scheduled. Interfaces and persistence designed now |

### 5.8.8 Actionable prediction latency - **Core**; event-driven implementation - **Advanced**

**The requirement and the mechanism are separated, because they are not the same thing.**

> **CORE REQUIREMENT - the actionable-latency guarantee.** A prediction must reach the engineer **before the last stage at which it can still be acted upon has passed.** A prediction delivered after its remediation deadline has no operational value, so this is not an optional refinement of the prediction capability; it is a condition of the capability being real.

**How the deadline is defined.** For each unit and each predicted outcome, the **remediation decision deadline** is derived from the plant's own model: the earliest start time of the last stage on the unit's declared route at which any eligible remediation could still be applied (5.6.4d check 2), minus the declared minimum lead time for a human decision. It is a property of the route and the candidate set, not a fixed clock value.

**The Core obligations, which no implementation choice may waive.**

| # | Obligation |
|---|---|
| 1 | **Every prediction carries its deadline.** `predictions.actionable_deadline_utc`, computed at scoring time from the route and the eligible candidates |
| 2 | **Every prediction records whether it met it.** `predictions.met_actionable_deadline boolean` and `delivery_latency_seconds`, measured from the source batch's arrival to the queue refresh |
| 3 | **The scheduled path is acceptable only if it demonstrably meets the deadline.** A configuration whose measured end-to-end latency exceeds the deadline for a material proportion of units is a **defect**, whatever mechanism it uses |
| 4 | **The platform monitors and reports the miss rate.** Per outcome and per route, on D8 and D9, as a first-class figure |
| 5 | **A missed deadline is disclosed, never hidden.** A prediction that arrives after its deadline renders as **"past the actionable stage"** with no accept action, and its remediation candidates are suppressed by check 2 |
| 6 | **Cadence is derived, not guessed.** The scheduling recommendation for a scoring job is computed from the shortest observed deadline for its outcome, and F4 warns when a configured cadence cannot meet it |
| 7 | **Breach alerting.** A sustained rise in the miss rate raises an entry on the platform channel, routed by E6 |

**The Advanced part is the mechanism only.** Event-driven scoring, sub-minute latency and specialised streaming are **Advanced**: they are how a plant with short remaining stages meets the deadline. A plant whose stages are hours long meets the same Core obligation with a scheduled job. **The requirement is Core; the technology to satisfy it varies by plant.**

**Refusals.** `PD09` a scoring cadence configured such that the computed deadline cannot be met for the declared route, refused at F4 with the deadline and the cadence named. `PD10` a prediction generated after its deadline, stored with `met_actionable_deadline = false` and excluded from the actionable queue.

#### The Advanced implementation: event-driven and micro-batch scoring

Early Warning requires more than scheduled batch scoring, because a unit already past its remediable stage cannot be helped.

| Element | Specification |
|---|---|
| **Trigger** | Event-triggered on projection completion for a scope that includes in-process units, or micro-batch on a short interval, whichever the tenant configures |
| **Feature freshness** | The scoring request carries a maximum feature age; features older than it trigger a targeted refresh for the affected entities first, or the run reports the staleness on the prediction |
| **Scoring trigger** | `prediction_runs.trigger_kind = 'event'`, with the triggering batch recorded |
| **Maximum end-to-end latency** | Declared per tenant: source push to queue update. The measured latency is recorded per run and exposed on D8 |
| **Deduplication** | A unit already scored against the same feature snapshot and model version is not rescored; the existing prediction stands |
| **Ordering** | Per unit, scoring applies in batch-watermark order; a later batch never loses to an earlier one, enforced by comparing `source_batch_high_watermark` |
| **Late events** | A late batch marks its entities dirty; the next pass rescores them and the prediction supersedes, with both retained |
| **Model warm-up** | Per 5.6.7; the event path never triggers a cold load |
| **Backpressure** | The event path shares the `ml` pool; when the pool saturates, the event path degrades to micro-batch, then to scheduled, and **announces each degradation** |
| **Current-risk-state update** | `prediction_current` refreshed transactionally at the end of each micro-batch |
| **Notification trigger** | A prediction crossing a configured risk band raises an entry on the plant-data channel, which E6 routes |
| **Degradation to scheduled** | Explicit and visible: the queue shows the current scoring mode and the reason it is not in the event path |

**Acceptance for the Core requirement.** Every prediction carries a deadline and a measured latency; the miss rate is visible per outcome and per route; a prediction past its deadline renders as past the actionable stage with no accept action; a cadence that cannot meet the deadline is refused at F4 with `PD09`; a sustained miss-rate rise raises a routed alert. **A scheduled-only configuration passes this acceptance if and only if its measured latency meets the deadline.**

**Acceptance for the Advanced mechanism.** A projected batch containing in-process units updates the queue within the declared latency; a duplicate scope does not rescore; a late batch supersedes in the correct order; pool saturation degrades the path with an announcement; the queue always states its current scoring mode.

---

*End of Chapter 4.*

### 5.8.9 Performance Reference - **Core declared authority; learned extensions depend on data maturity**

A Performance Reference compares actual operation to a governed reference without conflating declaration with learning. Declared kinds are Engineering Standard, Management Target and Operating Envelope. Learned kinds are Historical Baseline, Learned Best Practice and Peer Reference.

Reference resolution is deterministic by scope specificity and effective date. Equal-precedence overlap refuses `RF01`. Every result names the reference id/version, source authority, effective window and scoring semantics. Higher-is-better, lower-is-better and inside-range are different equations; there is no universal percentage formula.

The Page Builder exposes derived measures such as gap, normalised deviation, in-envelope state and reference attainment where the scoring semantics permit it. Learned reference values remain Layer-B evidence and never overwrite declared Layer-A standards.

### 5.8.10 Operational Evidence Reconciliation - **Advanced, data-maturity gated**

Reconciliation is a governed engine over two or more independent evidence sources that describe the same semantic fact/interval. It first resolves source time authority, then evidence quality, then fact-specific authority, then classifies the case.

Canonical states: **Aligned, PartiallyAligned, MissingEvidence, TemporalUncertain, ConflictingEvidence, LikelyMisclassified, Unresolved.** `TemporalUncertain` is mandatory when clock alignment is not established. The engine never labels a person or infers intent.

Each case carries evidence handles and the causal-confidence level:

`L0 Observed Fact -> L1 Discrepancy -> L2 Statistical Association -> L3 Temporally Supported Hypothesis -> L4 Mechanistically Supported Hypothesis -> L5 Confirmed Cause`.

L5 requires a governed confirmation source/human confirmation. Below L5 the Assistant wording is **"strongest supported root-cause hypothesis"**. The UI shows what evidence would be required to move to the next level.

Reconciliation may run entirely from historical exports covering the same calendar period; live OPC is not a prerequisite. This is important for commissioning and customer evaluation.

### 5.8.11 Governed Investigation / Insight Board Composer - **Advanced**

The Assistant may propose a versioned `InsightBoardPlan` containing question, context, filters, evidence requirements, widget specifications and narrative sections. The plan is validated against definitions, registry, relationship paths, permissions, query safety and chart grammar. The existing widget/query engine produces every number.

The plan and result are reproducible: planner version, definition versions, data-as-of, tool executions and evidence handles are persisted. Re-asking later may create a new version because data changed; the historical investigation remains replayable as it was known at the time.

The LLM never emits executable React or arbitrary SQL and never inserts unverified plant numbers into a board.

---

# CHAPTER 4 — PART II: INTEGRATED LAYER-B DETAILED ARCHITECTURE

**Status: NORMATIVE TARGET DESIGN.** This is the former Layer-B Architecture Design Pack Revision 8 absorbed into the official Chapter 4. It is no longer a separate document or authority. Where this integrated Part II describes a contract owned by Chapter 3 persistence or Chapter 6 deployment, those chapters remain authoritative for the exact schema/topology; this Part II owns the deep engine flow, algorithms, gates, family contracts and integration behaviour.

### LB-A1. EXECUTIVE ARCHITECTURE SUMMARY

### 1.1 What Layer B is, in one paragraph

Layer B is a set of governed batch pipelines and read-only serving surfaces that consume a **versioned semantic contract** and seven **persistent data products**, produce **seven intelligence and engine families, MF-01 to MF-07**, registered and activated per serving identity in `model_registry`, and emit **seven governed analytical datasets** that the Page Builder and the Assistant consume through the same mechanisms they use for ordinary data. It contains no customer table name, no industry vocabulary, and no per-customer code path.

### 1.2 The three planes

The whole architecture is organised as three planes separated by two hard walls.

```
+---------------------------------------------------------------+
| AUTHORING PLANE                                               |
| customer sources -> wiring canvas / governed SQL              |
| -> semantic model version (published, immutable)              |
+---------------------------------------------------------------+
                    || SEMANTIC WALL
                    || Layer B reads semantic codes only.
                    || No physical table name crosses this wall.
+---------------------------------------------------------------+
| LEARNING PLANE                (training state)                |
| data products -> capability profile -> model training         |
| -> validation gates -> trained version -> activation          |
| GPUs, hours of compute, temporary artifacts, large scans      |
+---------------------------------------------------------------+
                    || SERVING WALL
                    || One-way. Activation writes; serving reads.
                    || No serving request can enter the left side.
+---------------------------------------------------------------+
| SERVING PLANE                 (serving state)                 |
| active models + evidence/prediction stores + vector index     |
| -> Page Builder datasets, Assistant tools                     |
| bounded memory, bounded time, no training dependency;         |
| GPU use is optional and benchmark-driven                      |
+---------------------------------------------------------------+
```

The two walls are the load-bearing elements of the design. The Semantic Wall is what makes the product generic. The Serving Wall is what makes the daytime latency contract enforceable rather than aspirational.

### 1.3 The ten architectural decisions

| ID | Decision | Consequence |
|---|---|---|
| **AD-01** | Layer B consumes **published, immutable canonical definition versions plus the published relationship model plus the governed registry state**, never a live authoring state. A **Semantic Contract Manifest** pins that set for reproducibility | A model can always be explained by the exact contract it was trained under, resolved through one hash rather than five references |
| **AD-02** | The **analytical spine is a node table**, one row per grain instance per process position, not one row per grain instance | Multi-stage plants, graph routes and continuous intervals share one shape |
| **AD-03** | Every feature carries an **availability position and offset**; `prediction_cutoff` is enforced by the catalogue, not by the modeller | Temporal leakage becomes a mechanical gate, not a review |
| **AD-04** | **Canonical.** The feature store is `ppiq_plant.feature_store`: one row per `(analysis_subject_id, feature_set_version_id)` with declared `grain_code`, `features jsonb`, labels, lineage and dirty state. `analysis_subject_id` is generic across material, equipment, campaign and process-window subjects. Refresh is incremental by watermark | No Layer-B path assumes a material/coil identity; idempotency is subject + feature-set version |
| **AD-05** | **Canonical.** `ppiq_plant.model_registry` governs per-model lifecycle. Serving identity is `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`. `status` and `serving_role` are independent axes, with partial-unique indexes giving at most one active and one approved fallback per serving identity | Activation is per serving identity |
| **AD-06** | The **vector index is generational and append-only within a generation**. Weekly inserts create a new immutable generation manifest, not a mutation | A retrieval result is reproducible against a named generation |
| **AD-07** | **Every Layer B output carries a claim class** and an evidence envelope. The claim class is a column, not documentation | The Assistant cannot blur association with effect because the data itself refuses to |
| **AD-08** | **Serving has no path to training.** Separate pooler identity, separate database role, separate pool. Tier 3 creates a job request row; it does not call the trainer | The daytime path cannot accidentally enter the training pipeline |
| **AD-09** | Layer B outputs are **ordinary bindable sources** declared in `ppiq_meta.registry_intelligence_sources`, with `sourceKind = intelligence`, an `entity_link_column` and `columnRoles`. Fact-shaped measures may project through WidgetFact; native-rich sources keep their declared columns | No ML-specific widget code. A prediction and the parameter that drove it can occupy one widget |
| **AD-10** | **Refusal is a materialised row**, not an exception. Every terminal state persists with its reason and population | A dashboard bound to an unready model shows a stated refusal, not an empty chart |

### 1.4 What is new in this pack relative to the frozen rule

The frozen rule states intent and constraints. This pack adds, and Worker 2 needs, the following that the rule does not contain:

- concrete field-level contracts for eleven semantic input objects and seven data products
- the **prediction point** concept as a first-class object, which the rule implies but never defines
- the **collapsed dimension** rule, without which single-shift and single-variant plants fail
- the **residence model** on process position, without which continuous-flow customers cannot be linked at all
- the **intervention flag** on events, without which the effect layer at Level 3 has no input
- the **abort ladder** for the weekly window, without which the 24 hour budget is a hope
- the **claim-class column** and **aggregation semantics**, which close two contradictions in the rule
- the **channel set version**, which determines when a frozen encoder becomes invalid rather than merely stale

---

### LB-A2. CONTEXT AND COMPONENT ARCHITECTURE (Deliverable A)

### 2.1 Component inventory

| # | Component | Plane | Responsibility | May read | May write |
|---|---|---|---|---|---|
| 1 | Customer sources | External | Systems of record | - | - |
| 2 | Import / DB-link jobs | Authoring | Land raw data | Sources | Dump Store |
| 3 | Wiring Canvas / governed SQL | Authoring | Human declares meaning | Dump Store | Semantic definitions |
| 4 | Definition Store and Registry | Authoring | Owns the authoring lifecycle: `definition_store`, `definition_versions`, `definition_dependencies`, plus the extensible role registry | Authored definitions | `definition_versions`, registry rows |
| 5 | Canonical projection jobs | Authoring | Materialise Plant Data from Dump Store per the published transformation definition, which also emits the relationship model | Dump Store, published `definition_versions` | Plant Data, `plant_relationships` |
| 6 | **Layer A Exact BI Engine** | Serving | Deterministic aggregation | Plant Data, intelligence datasets | Nothing |
| 7 | **Data Product Builder** | Learning | Builds spine, features, sequences, outcomes | Plant Data, published `definition_versions`, `plant_relationship_paths` | Data products |
| 8 | **Capability Profiler** | Learning | Measures what this installation supports | Data products | Capability profile |
| 9a | **Learned-model trainers** (MF-01, MF-03, MF-04) | Learning | Fit candidate model versions. `ml.training` lane. Champion/challenger applies | Snapshots, profile | `model_registry` at `trained` |
| 9b | **Retrieval index builder** (MF-02) | Learning | Builds and seals index generations. Not trained; gated on measured recall@k against exact Flat | Embeddings or standardised features | Index generations |
| 9c | **Statistical engines** (MF-05, MF-06) | Learning | Compute associations, effects and envelopes. `analysis` lane. Recomputed, never trained, no champion/challenger | Data products | `correlation_results`, evidence |
| 9d | **Practice engine** (MF-07) | Learning | Canonicalises signatures and computes matched statistics. `analysis` lane. Governed signature version, not a trained model | Data products, events | `practice_signatures`, `practice_statistics` |
| 10 | **Validation Gate Runner** | Learning | Runs G-01..G-55 | Candidate model versions, data products | Gate results |
| 11 | **Model Activator** | Learning -> Serving | Sets `status = 'active'` for a serving identity, retiring the previous holder. The only writer across the Serving Wall | `model_registry`, gate results | `model_registry`, serving stores |
| 12 | **Model Registry** | Both | Versions, lineage, aliases, rollback | - | Registry records |
| 13 | **Inference Service** | Serving | Scores instances with the active model for a serving identity | `model_registry` artifact, feature store | `prediction_runs`, `predictions`, `prediction_current` |
| 14 | **Evidence Materialiser** | Learning | Turns model outputs into governed datasets | Candidates, stores | Evidence store |
| 15 | **Intelligence Orchestrator** | Serving | Routes a question to tier 1, 2 or 3 | Serving stores | Job request rows only |
| 16 | **VectorSimilarityIndex** | Serving | ANN retrieval | Index generation files | Nothing at query time |
| 17 | **Drift Supervisor** | Learning | Monitors and decides actions | Serving and learning stores | Supervisor decisions |
| 18 | **Scheduler / job runtime** | Learning | Runs the three schedules; admits by `max_concurrency` and `resource_capacity` per lane; checkpoints and pre-empts `ml.training` | Job state | Job state |
| 21 | **Snapshot Materialiser** | Learning | **The only component permitted to read `feature_store` for sealing.** Seals the typed columnar artifact and records its hash | `feature_store` | `feature_snapshots`, artifact store |
| 22 | **Manifest Resolver** | Both | Resolves or creates the Semantic Contract Manifest for a run | Canonical version tables | `semantic_manifests` |
| 19 | **Page Builder** | Serving | Binds datasets to widgets | Intelligence metadata, datasets | Widget definitions |
| 20 | **Assistant** | Serving | Orchestrates tools, composes answers | Layer A tools, Layer B tools | Nothing |

### 2.2 The Semantic Wall

**Rule.** No component numbered 7 or higher may reference a customer physical table name, column name, schema name, or industry term. Their only vocabulary is the semantic code space defined in section 4.

**Enforcement, three layers, mirroring the existing isolation doctrine:**

| Layer | Mechanism |
|---|---|
| Database | The Layer B role holds grants on Plant Data and the intelligence schema only. No grant on Dump Store, no grant on any source-shaped schema |
| Application | Every source reference resolves through a published `definition_version`, and every entity correspondence through `RelationshipResolver`. A literal identifier in a Layer B code path has no resolution path and cannot execute |
| Test | An architecture test asserting that no file under the Layer B tree contains a customer identifier, an industry noun from the prohibited-vocabulary list, or a `switch` on tenant, site, or industry. Falsified once before it is trusted |

**The prohibited-vocabulary list is itself configuration**, seeded with the vocabulary of every installation encountered, so it grows as customers are added. It is not a hardcoded steel list.

### 2.3 The Serving Wall

**Rule.** The serving plane may read the active model version and the serving stores. It may never invoke a trainer, allocate a GPU, scan a raw source, or perform an unbounded scan of a data product.

**Enforcement:**

| Layer | Mechanism |
|---|---|
| Process | Serving runs in a separate process group with no import of any trainer module. A dependency test asserts the serving assembly does not reference the training assembly |
| Database | The serving role has SELECT only on serving stores, plus INSERT on the run and prediction tables. No DDL, no access to untrained-model artifacts |
| Runtime | Every serving query carries a cost estimate and a hard statement timeout. Tier 3 inserts a job request row and returns; it does not await |
| Test | A gate asserting no serving code path can reach a training entry point, and that the statement timeout is set on every serving connection |

### 2.4 Where the boundaries fall

| Boundary | Ends at | Starts at |
|---|---|---|
| Physical schema | Dump Store and the customer's own systems | - |
| Semantic model | - | Publication of the transformation and its `definition_version`, which emits the relationship model |
| Training data products | Canonical Plant Data | `journey_spine` materialisation |
| Learned intelligence | Data products | Model training |
| Serving | Activation | `status = 'active'` for a serving identity |

---

### LB-A3. END-TO-END DATA FLOW (Deliverable A, continued)

```
 (1) CUSTOMER SOURCES
      |  import jobs, read-only toward the customer
      v
 (2) DUMP STORE                          as it arrived, uninterpreted
      |  Wiring Canvas / governed SQL. A HUMAN declares meaning.
      |  Output: definitions, not data.
      v
 (3) PUBLISHED DEFINITION VERSIONS + RELATIONSHIP MODEL + REGISTRY STATE
     pinned for reproducibility by a SEMANTIC CONTRACT MANIFEST
      |         |
      |         +--> (4) CANONICAL PROJECTION -> PLANT DATA
      |                        |
      |                        v
      |         =============== SEMANTIC WALL ===============
      |                        |
      +----------------------->+
                               v
 (5) DATA PRODUCT BUILDER
      spine -> features -> sequences -> outcomes
                               |
                               v
 (6) CAPABILITY PROFILE       what this installation can support
                               |
                               v
 (7) MODEL TRAINING           encoder, index, novelty, supervised, effect
                               |
                               v
 (8) VALIDATION GATES         G-01..G-55
                               |
                               v
 (9) TRAINED MODEL VERSION --> champion/challenger --> ACTIVATION
                               |
      =============== SERVING WALL (one-way) ===============
                               |
                               v
(10) ACTIVE MODEL PER SERVING IDENTITY + EVIDENCE + PREDICTIONS + INDEX
                               |
              +----------------+----------------+
              v                                 v
(11) GOVERNED INTELLIGENCE DATASETS      (12) ASSISTANT TOOLS
              |                                 |
              v                                 v
     PAGE BUILDER WIDGETS                 ASSISTANT ANSWERS
     (same binding path as Layer A)       (fact + finding + evidence
                                           + qualification)
```

**The two facts a reader should take from this diagram.** First, the semantic model is the only thing that crosses from the customer's world into the engine, and it crosses as codes. Second, promotion is the only arrow that crosses the Serving Wall, and it points one way.

---

### LB-A4. LAYER B INPUT CONTRACT (Deliverable B)

Eleven objects. Layer B reads these and nothing else. Every one is tenant-scoped and site-scoped; those columns are omitted from the field lists below and assumed present on all.

### SM-01 Semantic Contract Manifest

**Canonical: `ppiq_meta.semantic_manifests` (Chapter 3 4.5.11 area, amendment C3-4).**

An **immutable, content-addressed reproducibility pin** over the canonical versions in force. A commit over the semantic contract. **It is not an authoring authority and has no lifecycle**: `definition_versions`, the relationship publication and `model_registry` retain their authority unchanged.

| Field | Type | Notes |
|---|---|---|
| `manifest_id` | uuid | **PK.** The handle artifacts reference |
| `tenant_id` | uuid NOT NULL | |
| `manifest_hash` | varchar(64) NOT NULL | Content hash over the referenced versions |
| `definition_versions` | jsonb NOT NULL | Array of `{definition_id, version_number}` |
| `relationship_source_definition_id` | uuid NOT NULL | |
| `relationship_source_definition_version` | integer NOT NULL | |
| `registry_snapshot_hash` | varchar(64) NOT NULL | Over the registry rows in force |
| `configuration_hash` | varchar(64) NULL | Governed configuration affecting semantics |
| `created_at_utc` | timestamptz NOT NULL | |

**UNIQUE `(tenant_id, manifest_hash)`.** Identical content within a tenant never creates a second row. Identical content across two tenants correctly creates two rows, because a manifest is tenant-owned evidence and a shared global row would be a cross-tenant object.

**No status column. No draft, validated, published or rolled-back state. Nothing updates a manifest.**

**Coverage rule.** Run and artifact tables carry `semantic_manifest_id uuid NULL FK`. **The column is nullable for legacy records only.** Every new governed AI/ML execution must resolve a manifest; a run that cannot is refused rather than recorded without one. Gate G-55.

### SM-02 GrainDefinition

| Field | Type | Notes |
|---|---|---|
| `grain_code` | text | PK within version |
| `grain_kind` | enum | discrete_item, batch, lot, campaign, process_window, flow_interval, custom |
| `semantic_role_code` | text | FK to SM-11 |
| `time_semantics` | enum | instant, interval |
| `identity_definition_ref` | text | Points at the authored definition producing the identity, never a table name in Layer B code |
| `is_primary_analytical_grain` | bool | Exactly one true per version |
| `parent_grain_code` | text | Nullable, for nested grains |
| `expected_cardinality_per_day` | bigint | Declared, used for sizing |

**No industry example appears in engine code.** `grain_kind` is the only thing the engine branches on, and only for time semantics.

### SM-03 ProcessPosition

| Field | Type | Notes |
|---|---|---|
| `position_code` | text | PK within version |
| `position_kind` | enum | unit, stage, operation, virtual |
| `ordinal` | int | Nullable when the route is a graph |
| `predecessor_position_codes` | text[] | Graph edges |
| `successor_position_codes` | text[] | |
| `is_terminal` | bool | |
| `residence_model_kind` | enum | **none, fixed_lag, lag_with_dispersion** |
| `residence_lag_seconds` | numeric | Required when kind is not none |
| `residence_dispersion_seconds` | numeric | Required for lag_with_dispersion |

**Why residence is here.** For a continuous or flow-interval grain there is no tracking system to say which material was at which position when. The residence model is the declared substitute. Without it, a continuous-process customer has genealogy strength zero and loses outputs A and C entirely. This field is the single cheapest thing that opens the continuous-process market.

### SM-04 RelationshipEdge

The genealogy contract. Consumed as a graph; never rediscovered.

| Field | Type | Notes |
|---|---|---|
| `edge_code` | text | PK within version |
| `parent_grain_code` / `child_grain_code` | text | |
| `edge_kind` | enum | identity, transformation, temporal, containment |
| `cardinality` | enum | one_to_one, one_to_many, many_to_one, many_to_many |
| `weight_semantics` | enum | none, proportion |
| `origin` | enum | authored, inferred |
| `confidence` | numeric | Required when origin is inferred |
| `temporal_validity_from` / `_to` | timestamptz | Nullable |
| `definition_ref` | text | The authored join, versioned |

**Genealogy strength is derived, not declared:** `none` when no edge connects two positions, `sequential` when only identity and temporal edges exist, `transformational` when any edge carries proportion weights or many_to_many cardinality.

### SM-05 ParameterDefinition

| Field | Type | Notes |
|---|---|---|
| `parameter_code` | text | PK within version |
| `semantic_role_code` | text | FK to SM-11 |
| `physical_quantity` | text | speed, temperature, pressure, mass_flow, ... from a registry |
| `unit_code` | text | UCUM-style code |
| `data_type` | enum | numeric, categorical, boolean, text |
| **`signal_kind`** | enum | analog, state, counter, event, lab_sample, composition, level, derived, unknown |
| **`aggregation_kind`** | enum/null | sample_mean, time_weighted_mean, integral, delta, state_duration, count, min, max, last, percentile, mass_weighted_mean, volume_weighted_mean. Null means undeclared, never Average-by-default |
| **`interpolation_kind`** | enum | none, linear, step_forward |
| **`weight_basis`** | enum | none, time, mass, volume |
| **`maximum_gap_seconds`** | int/null | Beyond this, carry-forward is invalid |
| **`quality_policy` / `counter_reset_policy` / `time_basis`** | text | Governed execution semantics |
| `value_kind` | enum | setpoint, actual, derived |
| `controllability` | enum | **controllable, observed, unknown** |
| `grain_code` / `process_position_code` | text | Where it lives |
| `timestamp_availability` | enum | per_sample, per_grain, none |
| `nominal_sampling_hz` | numeric | Nullable, used for sizing and sequence policy |
| `valid_range_min` / `_max` | numeric | Nullable |
| `missing_policy` | enum | drop, impute_declared, treat_as_category |
| `definition_ref` / `definition_version` | text | |

`physical_quantity` plus `unit_code` is what makes unit-sane answers structurally possible rather than hoped for. `controllability` is what separates an actionable finding from a true but useless one.

### SM-06 OutcomeDefinition

| Field | Type | Notes |
|---|---|---|
| `outcome_code` | text | PK within version |
| `outcome_type` | enum | binary, categorical, ordinal, continuous |
| `class_taxonomy_ref` | text | Required for categorical and ordinal |
| `ordinal_rank_map` | jsonb | Required for ordinal |
| `grain_code` | text | |
| `detection_position_code` | text | **Where it becomes known** |
| `detection_timestamp_field` | text | **When it becomes known** |
| `direction` | enum | higher_is_better, lower_is_better, target_band, none |
| `unit_code` | text | For continuous |
| `censoring_policy` | enum | none, right_censored, interval |
| `definition_ref` / `definition_version` | text | |

**`detection_position_code` and `detection_timestamp_field` are the leakage anchors.** Everything in gate G-06 derives from them.

### SM-07 EventDefinition

| Field | Type | Notes |
|---|---|---|
| `event_code` | text | PK within version |
| `event_kind` | enum | alarm, failure, stop, maintenance, operator_action, state_change, **intervention** |
| `scope` | enum | grain, position, resource, site |
| `start_timestamp_field` / `end_timestamp_field` | text | End nullable for instantaneous |
| `severity_field` | text | Nullable |
| `is_intervention` | bool | **True when the event records a deliberate corrective action** |
| `intervention_target_parameter_code` | text | Nullable, what the action was aimed at |
| `intervention_dose_field` | text | Nullable, magnitude of the action |

**Why `is_intervention` exists.** The frozen rule requires a Level 3 and Level 4 effect layer and lists intervention history in the capability profile, but the declaration contract as written has no object that identifies an intervention. Without this flag, the effect layer above Level 2 has no input and must always refuse. This is contradiction CT-05, closed here.

### SM-08 ContextDimension

| Field | Type | Notes |
|---|---|---|
| `dimension_code` | text | PK within version |
| `semantic_role_code` | text | variant, shift, crew, campaign, ambient, other |
| `applies_at` | enum | grain, position, time_window |
| `level_source_ref` | text | Where its levels come from |
| `is_variant_dimension` | bool | True for the dimension along which correct settings change |

**Derived at profile time, not declared:** `observed_level_count` and `collapse_status`.

**The collapse rule.** When `observed_level_count = 1`, the dimension is **COLLAPSED**. A collapsed dimension is removed from every conditioning set, every stratification and every subgroup check, and the removal is stated in the finding's `conditioning` field with reason `collapsed_single_level`. A collapsed dimension is never an error, never a warning, and never causes a method to refuse. A plant with one shift, one variant or one line is a normal customer, not a degraded one.

### SM-09 ResourceDefinition

| Field | Type | Notes |
|---|---|---|
| `resource_code` | text | PK within version |
| `resource_class` | text | From the role registry |
| `instance_identity_available` | bool | Do we know which physical unit |
| `process_position_code` | text | Where it acts |
| `life_counter_parameter_code` | text | Nullable, wear or usage counter |

### SM-10 SpecificationDefinition

| Field | Type | Notes |
|---|---|---|
| `spec_code` | text | PK within version |
| `variant_dimension_code` | text | FK to SM-08 |
| `target_parameter_code` | text | FK to SM-05 |
| `target_value` / `lower_bound` / `upper_bound` | numeric | |
| `applies_at_position_code` | text | |

Specifications are inputs to envelope comparison, never a substitute for learned envelopes.

### SM-11 SemanticRoleRegistry

| Field | Type | Notes |
|---|---|---|
| `role_code` | text | PK |
| `role_kind` | enum | position, unit, observation, practice, event, outcome, resource, input, specification, relationship, **extension** |
| `parent_role_code` | text | Nullable, allows specialisation |
| `is_core` | bool | Core roles ship with the product |
| `added_in_semantic_model_version` | uuid | Provenance for extensions |
| `label` / `description` | text | Human-facing only |

**The registry is extensible by configuration.** A new installation may add roles under `extension` or as children of a core role. Layer B code branches only on `role_kind`, never on `role_code`. Adding a role never requires a code change; this is the mechanical statement of section 3 of the frozen rule.

### SM-12 PredictionPoint (derived object, defined here because the rule assumes it without naming it)

| Field | Type | Notes |
|---|---|---|
| `prediction_point_code` | text | PK |
| `position_code` | text | Position after which prediction is made |
| `offset_seconds` | numeric | Zero means at position exit |
| `cutoff_rule` | enum | position_exit, position_entry, fixed_offset |
| `is_active` | bool | Governed, see the model-count governor in section 6.7 |

A prediction point is what turns "predict quality" into a trainable, gate-checkable object. Every supervised model is keyed on one. Prediction points are generated as candidates from the position graph and activated by the governor, never all at once.

### 4.1 Input validation contract (the relationship-quality gates)

The frozen rule states that a Wiring Canvas line is an authored hypothesis, not proof. These are the measurements that turn that principle into an executable gate. All run before the first training stage and are re-run weekly.

| ID | Metric | Definition | Consumed by |
|---|---|---|---|
| **IV-01** | Join coverage | Fraction of child rows resolving to a parent through the declared edge | Genealogy strength |
| **IV-02** | Orphan rate | Child rows with no parent | G-02 |
| **IV-03** | Duplicate explosion factor | Rows after join divided by rows before | G-02 |
| **IV-04** | Cardinality conformance | Observed cardinality versus declared | G-02 |
| **IV-05** | Temporal validity | Fraction of edges where child start is at or after parent start | G-02, G-06 |
| **IV-06** | Impossible edge count | Edges implying future-to-past causation | **Hard block** |
| **IV-07** | Weight closure | For proportion edges, sum per child within tolerance of 1.0 | G-02 |
| **IV-08** | Temporal alignment quality | Fraction of observations placeable inside a grain's residence window | Capability profile |

**IV-06 is the only one that hard-blocks the whole run.** An impossible edge is not a data quality score, it is a declared falsehood, and every downstream number inherits it.

---



### SM-12 AnalysisSubjectDefinition / resolved AnalysisSubject

Layer B's universal subject identity. The definition points at SM-02 grain; the resolved subject carries `analysis_subject_id`, `grain_code`, subject kind, optional entity reference, optional interval and lineage hash. **No mandatory material-unit field exists.** The analytical spine links its nodes to the resolved subject.

### SM-13 SourceTimeAuthority

Per source/site: timezone, timestamp basis, clock reference, DST policy, observed skew, alignment tolerance, source/server timestamp fields and effective dates. Any cross-source engine must resolve this contract before comparing intervals.

### SM-14 PerformanceReferenceContract

Declared reference kinds: engineering_standard, management_target, operating_envelope. Learned references remain governed outputs, not declarations. The contract carries scope, target/bounds, unit, directionality/scoring semantics, authority, reason and effective dates.

### SM-15 FactEvidenceAuthority

Fact-specific source authority: semantic `fact_code`, source-system definition, Primary/Supporting/Corroborating role, quality floor, effective dates. This is tenant configuration and cannot be replaced by a global source priority.

### LB-A5. INTELLIGENCE DATA PRODUCT SCHEMAS (Deliverable C)

Seven products. For each: logical contract first, physical implementation named as replaceable.

**Convention.** Every table carries `tenant_id`, `site_id` and `created_at_utc`, omitted from the field lists below.

**Reproducibility pin.** Run, artifact and evidence tables carry `semantic_manifest_id uuid NULL FK` and the canonical lifecycle identities they depend on: `source_definition_id` and `source_definition_version` for the relationship publication, and the relevant `definition_version_id`. **This is not a blanket column on every table** - a lookup or projection table that produces no governed result carries no pin.

### DP-1 Journey / analytical spine

**Purpose.** The one row everything else attaches to.

**Primary grain.** One row per (grain instance, process position). A grain instance visiting five positions produces five spine nodes.

| Field | Type | Notes |
|---|---|---|
| `spine_node_id` | uuid | PK, surrogate |
| `grain_code` | text | FK SM-02 |
| `grain_instance_key` | text | The customer's own identity, as a string, opaque to the engine |
| `process_position_code` | text | FK SM-03 |
| `position_ordinal` | int | Materialised from the route walk, null for pure graphs |
| `start_utc` / `end_utc` | timestamptz | The residence window at this position |
| `start_local` / `plant_tz` / `utc_offset_minutes` | | Local-time semantics preserved |
| `route_path_id` | text | Which route variant this instance took |
| `variant_key` | text | Value of the variant dimension |
| `context_keys` | jsonb | All other context dimension values |
| `parent_spine_node_ids` | uuid[] | Materialised from SM-04 walk |
| `parent_weights` | numeric[] | Aligned with the above, null when weight_semantics is none |
| `genealogy_strength` | enum | none, sequential, transformational |
| `window_origin` | enum | **tracked, residence_derived** |
| `is_superseded` | bool | Correction marker |

**`window_origin` matters.** A residence-derived window is an estimate. Any finding built on residence-derived windows inherits an uncertainty that a tracked window does not have, and the evidence envelope must say so.

**Mutability.** Append-only. A correction inserts a new node and sets `is_superseded` on the old. Never updated in place.

**Partitioning concept.** By tenant, then by `start_utc` month. Route and variant are secondary indexes, not partition keys, because their cardinality varies wildly between customers.

**Lineage.** The transformation `definition_version_id` plus `source_definition_id` and `source_definition_version` reproduce the node exactly. The `semantic_manifest_id` on the run that built it pins the whole contract in one value.

**Retention.** Full history. This is the cheapest product and the most reused.

### DP-2 Feature store

**Canonical: `ppiq_plant.feature_store` (Ch3 4.5.12).**

**Primary grain.** One row per `(analysis_subject_id, feature_set_version_id)`. **UNIQUE on tenant + that pair is the idempotency rule.**

| Column | Type | Notes |
|---|---|---|
| `analysis_subject_id` | uuid NOT NULL FK | FK to generic `analysis_subjects`; may represent discrete material, equipment/process interval, campaign or other declared subject |
| `grain_code` | text NOT NULL | FK SM-02 |
| `feature_set_version_id` | uuid NOT NULL FK -> `definition_versions(id)` ON DELETE RESTRICT | The feature-set definition version that produced it |
| `features` | jsonb NOT NULL | The assembled feature values |
| `label_value` | numeric(18,6) | Regression label |
| `label_class` | varchar(100) | Classification label |
| `assembled_at_utc` | timestamptz NOT NULL | |
| `source_batch_high_watermark` | text | |
| `lineage_hash` | varchar(64) NOT NULL | |
| `is_dirty` | boolean NOT NULL DEFAULT false | Marks rows needing recomputation |

Indexes: partial `(feature_set_version_id)` WHERE `is_dirty`; `(feature_set_version_id, assembled_at_utc)`. Partition: hash by `analysis_subject_id` above Large. Retention: while any active model or snapshot references the version.

**Incremental refresh is the mechanism, not an optimisation.** `feature_refresh_watermarks` holds `last_batch_watermark`, `dirty_entity_count`, `is_invalidated` and `invalidation_reason` per feature-set version. `feature_refresh_runs` records entities resolved, recomputed and dirty-remaining per run. The refresh scope is the distinct entities touched by batches landing since the last watermark:

```
refresh_scope = distinct analysis_subject_id
                FROM canonical rows
                WHERE import_batch_id IN (batches since last_refresh_watermark)
```

**The cost of an analysis becomes proportional to what changed, not to what exists.** Without this, every correlation run at a mature plant rescans years of observations and no pool tuning saves it.

**Reproducibility is `feature_snapshots`**, immutable. A snapshot records `feature_set_version_id`, `entity_count`, `taken_at_utc`, `source_batch_range`, `lineage_hash`, `storage_uri` and `retention_until_utc`; its optional audit rows carry `analysis_subject_id`, `grain_code`, `features`, `label_value`, `label_class` with UNIQUE `(snapshot_id, analysis_subject_id)`. **Training pins a snapshot id.** A model is therefore always explicable against the exact population it saw.

**The training read path is the sealed columnar artifact, not PostgreSQL.**

```
live governed feature state     feature_store, jsonb, incremental, RLS
      |  seal
      v
immutable snapshot manifest     feature_snapshots: storage_uri, artifact_format,
      |                         artifact_content_hash, artifact_byte_size
      |  materialise
      v
typed columnar artifact         object storage. Format selected by B-03
      |  bounded read, projection pushdown
      v
Python data loader              PyTorch / LightGBM input
```

`feature_store` owns current governed state, lineage, row-level security and incremental refresh. **The artifact owns high-throughput training input.** Deserialising millions of JSONB objects per epoch is bounded by round-trips and JSON parsing rather than by the model.

**G-48: no training or encoding code path queries `feature_store`.** The snapshot materialiser is **exempt by definition** - reading `feature_store` is precisely how it seals the artifact, and it is the only component permitted to do so.

`feature_snapshot_rows` is an **optional audit sample** with a declared sampling rate, not the authoritative copy (amendment C3-2, conditional on B-03).

**Feature availability and the prediction cutoff.** The legality of a feature at a prediction point is a property of the feature-set definition, held in `feature_set_details` (feature list, grain, window, missing-value policy, scaling policy) and enforced at training by `model_training_runs.overlap_rows = 0`. Gate G-06 proves the property; the CHECK is the mechanism.

### DP-3 Sequence store

**Split contract. PostgreSQL holds a manifest; object storage holds the payload.**

**`ppiq_plant.sequence_manifests`** (amendment C3-3). One row per `(subject, channel_set_version, chunk_index)`.

| Field | Type | Notes |
|---|---|---|
| `subject_kind`, `subject_id` | varchar, uuid | Grain identity |
| **`channel_set_version`** | integer NOT NULL | **The channel set the encoder was trained on** |
| `time_from_utc`, `time_to_utc` | timestamptz NOT NULL | |
| `sample_count`, `channel_count` | integer, smallint | |
| `completeness` | numeric(9,6) NOT NULL | Observed fraction |
| `content_hash` | varchar(64) NOT NULL | |
| `storage_uri` | varchar(1000) NOT NULL | Chunk or chunk set |
| `chunk_index` | integer NULL | Where a subject spans chunks |
| `feature_snapshot_id` | uuid NULL FK | Participation in a sealed snapshot |
| `semantic_manifest_id` | uuid NULL FK | |

**Object storage** holds immutable chunked typed numeric arrays: values, offsets where irregular, and a mask. Compressed, partitioned by tenant and time, memory-mappable where the format allows. **The loader consumes bounded chunks, never a giant database row.**

**No numeric payload is stored in PostgreSQL.** This is the largest data product in the system; array columns carry per-row overhead, defeat compression, and put the largest byte volume through WAL, replication, backup and restore.

**`channel_set_version` is the encoder compatibility anchor.** An encoder is not merely frozen or stale; it is compatible or incompatible with the current channel set. Adding a production unit or an instrument changes the set, and G-13 refuses to serve an encoder whose version does not match.

Chunk size and compression are **B-04**. Retention is policy-driven and is the largest storage item in the system; see section 14.4.

### DP-4 Outcome store

**Primary grain.** One row per (grain instance, outcome_code, outcome_definition_version).

| Field | Type | Notes |
|---|---|---|
| `outcome_row_id` | uuid | PK |
| `grain_instance_key` | text | |
| `spine_node_id` | uuid | The node where detection occurred |
| `outcome_code` | text | |
| `outcome_definition_version` | int | |
| `value_numeric` | numeric | Continuous |
| `value_class` | text | Categorical |
| `value_ordinal_rank` | int | Ordinal |
| `value_bool` | bool | Binary |
| `detected_at_utc` | timestamptz | **Leakage anchor** |
| `detection_position_code` | text | **Leakage anchor** |
| `observed` | bool | False means known-unobserved, not missing |
| `censored` | bool | |
| `label_confidence` | numeric | Nullable |

**The `observed` flag is not cosmetic.** A grain instance with no defect row may mean "inspected and clean" or "never inspected". Treating the second as the first is the most common silent labelling error in plant data and it inflates every model. The distinction must come from the semantic model, and where it cannot, the capability profile reports outcome availability as `ambiguous_negative` and gate G-08 blocks binary classification on that outcome.

### DP-5 Embedding store and index metadata

**DP-5a `embedding_store`** - one row per (spine_node_id or grain_instance, encoder_version).

| Field | Type |
|---|---|
| `embedding_id` | uuid PK |
| `subject_kind` | enum: spine_node, grain_instance |
| `subject_id` | text |
| `encoder_version` | text |
| `channel_set_version` | int |
| `vector` | float32[] |
| `input_completeness` | numeric |
| `encoded_at_utc` | timestamptz |

**DP-5b `index_generation`** - the manifest that makes AD-06 work.

| Field | Type | Notes |
|---|---|---|
| `index_version` | text | PK |
| `encoder_version` | text | |
| `generation_no` | int | Increments weekly |
| `base_index_version` | text | The generation this one extends |
| `distance_metric` | enum | cosine, l2, inner_product |
| `index_params` | jsonb | Implementation-specific, never a product contract |
| `vector_count` | bigint | |
| `population_filter` | text | Which spine nodes are indexed |
| `built_at_utc` | timestamptz | |
| `is_sealed` | bool | Sealed generations are immutable |

**How AD-06 resolves CT-02.** Weekly insertion does not mutate a published index. It creates generation N+1 whose manifest names generation N as its base and lists the delta. Search fans out over the base plus the delta and merges. A trained model version pins `index_version`, which names an exact generation chain. A full rebuild seals a new base and resets `generation_no` to zero. **OD-04 (open) sets the rebuild trigger: a generation count ceiling, a delta-fraction ceiling, or a measured recall floor.**

### DP-6 Prediction store

**Primary grain.** One row per (grain_instance, prediction_point, outcome_code, model_version).

| Field | Type |
|---|---|
| `prediction_id` | uuid PK |
| `grain_instance_key` | text |
| `spine_node_id` | uuid |
| `prediction_point_code` | text |
| `outcome_code` | text |
| `predicted_value` | numeric |
| `predicted_class` | text |
| `class_probabilities` | jsonb |
| `risk_band` | text |
| `calibrated` | bool |
| `calibration_version` | text |
| `model_version` | text |
| `model_version` | text |
| `feature_row_id` | uuid |
| `scored_at_utc` | timestamptz |
| `is_current` | bool |

**Mutability.** Append-only. A rescore under a new model version inserts a new row and clears `is_current` on the old. **This is what allows a customer to ask why an answer changed between two Mondays and receive both rows.**

### DP-7 Evidence store

The umbrella product. Every row of every sub-product carries the same envelope.

**The EvidenceEnvelope, embedded in all evidence rows:**

| Field | Type | Notes |
|---|---|---|
| `evidence_id` | uuid | PK |
| `evidence_kind` | enum | finding, envelope, contributor, neighbour, anomaly, readiness, refusal |
| **`claim_class`** | enum | **ASSOCIATION, PREDICTIVE_CONTRIBUTION, MATCHED_EFFECT_ESTIMATE, CAUSAL_EVIDENCE** |
| `method_code` | text | The exact method used |
| `terminal_state` | enum | FINDING, INSUFFICIENT_DATA, NOT_APPLICABLE, REFUSED_BY_GUARD, CONTRADICTED_BY_CONTROL, MODEL_NOT_READY |
| `refusal_reason` | text | **Required when terminal_state is not FINDING. Must name the limitation, never blame the data unless the data is the measured cause** |
| `population_n` | bigint | |
| `conditioning` | jsonb | Dimensions conditioned on, plus any marked `collapsed_single_level` |
| `effect_value` / `effect_lower` / `effect_upper` | numeric | |
| `uncertainty_method` | text | |
| `support_overlap` | numeric | For matched estimates |
| `window_origin_mix` | jsonb | Fraction tracked versus residence-derived |
| `model_registry_id`, `model_code`, `model_version` | uuid, varchar, integer | Canonical model identity per Ch3 4.5.12 |
| `semantic_manifest_id` | uuid NULL FK | The Semantic Contract Manifest pinning the contract in force |
| `training_window_from` / `_to` | timestamptz | |
| `computed_at_utc` | timestamptz | |
| `supersedes_evidence_id` | uuid | Nullable |

**AD-10 in mechanical form.** A refusal is a row in this table with a terminal state and a reason. It has a population, a method and a version like any finding. It renders. It is queryable. It is never an empty result set.

**The refusal-reason discipline, which exists because of a measured past defect.** When a method is unavailable, `refusal_reason` must attribute the limitation to the method. Attributing it to the data, for example reporting zero variance when the true cause is an unsupported type pairing, is a defect of the same class as a false finding. Gate G-19 asserts that every `NOT_APPLICABLE` row names a method-side cause and every `INSUFFICIENT_DATA` row carries the measured statistic that failed its threshold.

### 5.1 Product dependency order

```
DP-1 spine
  |-> DP-2 features -----------+
  |-> DP-3 sequences --+       |
  |-> DP-4 outcomes ---|-------+
                       v       v
                 DP-5 embeddings/index
                       |       |
                       +-------+--> DP-6 predictions
                                    |
                                    v
                               DP-7 evidence
```

Nothing later may be built before everything earlier is complete under the same Semantic Contract Manifest.

---



### DP-8 Learned reference results

Layer-B historical baseline, learned-best-practice and peer-reference results. Every row carries target code, scope, value/band, unit, window, population/support, confidence, method, evidence and semantic manifest. It never overwrites the declared SM-14 reference.

### DP-9 Operational reconciliation cases

One governed case per semantic fact/interval. Carries state, time-alignment evidence, resolved discrepancy, evidence handles, L0-L5 causal-confidence level and strongest supported hypothesis. Evidence rows retain the fact-authority rule and source that made each datum primary/supporting/corroborating.

### LB-A6. INTELLIGENCE AND ENGINE FAMILY REGISTRY (Deliverable D)

**Seven families, five sub-types. Three of the seven are not models**, and the sub-type is load-bearing: it determines refresh policy, lane assignment and whether a champion/challenger gate applies at all.

| ID | Family | Sub-type | Lane | Champion/challenger |
|---|---|---|---|---|
| MF-01 | Process encoder | **Learned model** | `ml.training` | Yes, plus the promotion inequality |
| MF-02 | Similarity index | **Retrieval and index** | `ml.training` to build | No. Gated on recall@k |
| MF-03 | Normal and novelty | **Learned model** | `ml.training` | Yes |
| MF-04 | Supervised outcome | **Learned model** | `ml.training` | Yes, three-dimensional |
| MF-05 | Effect and envelope | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-06 | Statistical intelligence | **Statistical engine** | `analysis` | No. Recomputed, not trained |
| MF-07 | Practice learning | **Practice engine** | `analysis` | No. Governed signature version |

Plus **orchestration and governance**: the capability profiler, the model-count governor and the supervisor.


### MF-01 Self-supervised process encoder

| Attribute | Value |
|---|---|
| **Purpose** | Learn a representation of a process journey without labels |
| **Eligible inputs** | DP-3 sequence store, at least 2 channels, and DP-1 spine for context |
| **Eligibility requirements** | Channel count at least 2; median sequence length at least 32; at least 20,000 sequences; sequence completeness median at least 0.6; `channel_set_version` stable across at least 80 percent of the training window |
| **Minimum population** | 20,000 sequences (initial threshold, **OD-05: confirm by benchmark**) |
| **Output** | Fixed-dimension embedding, 128 to 256, plus `encoder_version` and `channel_set_version` |
| **Training method** | Masked-value reconstruction as the baseline objective; contrastive and next-position variants benchmarked as alternates |
| **Candidate architectures** | 1D temporal convolution baseline versus Transformer-style encoder. **Selection by measured downstream lift, not by architecture preference.** The comparison is a required commissioning artifact |
| **Framework** | PyTorch, behind a `ProcessEncoder` abstraction |
| **Validation metrics** | Held-out reconstruction error; kNN outcome purity on labelled subset where labels exist; embedding stability under resampling; downstream lift when added to MF-04 |
| **Explainability** | None directly. The encoder never faces the user; its outputs are inputs to MF-02 and MF-04 |
| **Refresh policy** | **Frozen between governed refreshes.** Retrained on: scheduled quarterly window, representation drift above threshold, `channel_set_version` change, or major regime change declared by MF-supervisor |
| **Compute class** | GPU preferred, CPU acceptable at small scale. Hours to days at commissioning |
| **Promotion** | The encoder ships only when it earns its operational cost:<br>`promote_encoder iff metric_lift >= declared_min_lift AND p95_latency_delta <= declared_latency_budget AND artifact_size <= declared_size_class AND explanation_stability >= floor`<br>**If engineered features match it within the lift threshold, the engineered features ship.** Deep learning being available is not a reason to deploy it. Benchmark B-05 |
| **Refusal states** | MODEL_NOT_READY when population or channel eligibility fails. **A customer with no sequence store is a valid customer**; the whole family is skipped and MF-04 runs without embeddings |
| **Emitted datasets** | DP-5a embeddings. No user-facing dataset |

**Important consequence.** MF-01 is optional. Every downstream family must function without it. A customer with per-grain aggregates only and no time series still receives outputs from MF-02 through MF-05 using engineered features alone.

### MF-02 Vector similarity index

| Attribute | Value |
|---|---|
| **Purpose** | Retrieve historical journeys resembling a subject |
| **Eligible inputs** | DP-5a embeddings, or DP-2 standardised numeric features when MF-01 is skipped |
| **Eligibility** | At least 5,000 indexable subjects |
| **Output** | Neighbour lists with distance, plus the neighbour's available outcomes and practices |
| **Contract** | `VectorSimilarityIndex` with build, seal, extend, search, recall_probe. **FAISS, HNSW, IVF and PQ are implementations selected by measurement. No library name appears in the contract** |
| **Validation** | **Recall@k against exact Flat search on a representative sample, measured on every build and stored on the index generation record.** A build below `recall_floor` does not become the served index. Plus p95 latency and generation-chain recall after N extensions |
| **Index policy** | Selected by measurement from population size, vector dimension, available RAM, required recall@k, p95 latency target, build time and update pattern. **Exact Flat is retained permanently on the representative sample as the correctness baseline.** HNSW, IVF, PQ, quantised and GPU-backed variants ship only where B-06 shows them appropriate |
| **Explainability** | Distance plus the feature-space differences between subject and neighbour, rendered as the contributor dataset |
| **Refresh** | Extend weekly as a new sealed generation; full rebuild per OD-04 trigger; mandatory rebuild on encoder change |
| **Compute class** | CPU sufficient at small and medium; GPU optional at very large |
| **Refusal states** | MODEL_NOT_READY below population; NOT_APPLICABLE when embeddings and standardised features are both absent |
| **Emitted datasets** | Similarity dataset (section 10.3) |

**Fallback path matters.** When MF-01 is skipped, MF-02 runs on standardised engineered features with a declared distance metric. Similarity quality is lower and the evidence envelope says so through `method_code`. The customer still gets a fingerprint.

### MF-03 Normal / novelty model

| Attribute | Value |
|---|---|
| **Purpose** | Represent normal operation without labels; score novelty |
| **Eligible inputs** | DP-5a embeddings or DP-2 features |
| **Eligibility** | At least 5,000 subjects; a declarable reference window judged regime-stable by the profiler |
| **Output** | Novelty score, percentile, nearest normal regime, contributing channels or features |
| **Candidates** | Robust Mahalanobis on embeddings, isolation-based methods, reconstruction error, density or cluster methods. **Selected per installation by calibration and false-positive behaviour, recorded in the registry** |
| **Validation** | False-positive rate on a held-out stable window against a declared budget; score stability; separation on known-abnormal periods where they exist |
| **Explainability** | Per-feature or per-channel contribution to the novelty score |
| **Refresh** | Refit weekly on a rolling reference window; cheap |
| **Compute class** | CPU, minutes |
| **Refusal states** | INSUFFICIENT_DATA below population; MODEL_NOT_READY when no regime-stable reference window exists |
| **Emitted datasets** | Anomaly dataset (section 10.4) |

**Product rule carried from the frozen rule into the data.** The anomaly dataset carries `novelty_score` and, separately and only when an outcome model exists, `associated_outcome_rate`. There is no column named "bad". Unusual is not bad, and the schema refuses to imply it.

### MF-04 Supervised outcome family

| Attribute | Value |
|---|---|
| **Purpose** | Predict a declared outcome at a declared prediction point |
| **Eligible inputs** | DP-2 features legal for the prediction point, plus optional DP-5a embedding columns, plus context |
| **Eligibility** | See the eligibility expression below |
| **Output** | Probability or value, risk band, calibrated |
| **Primary implementation** | Gradient-boosted trees, LightGBM initially, behind `SupervisedOutcomeModel` |
| **Alternates** | XGBoost, CatBoost, regularised linear baseline. **A regularised linear or single-tree baseline is mandatory as the floor comparison** |
| **Validation** | **Three-dimensional promotion gate.** QUALITY: discrimination or error, **calibration**, out-of-time performance, subgroup and regime stability, missingness robustness, **explanation stability**. SERVING: p50/p95/p99 latency, throughput, artifact size, RAM and VRAM, warm-up time. TRAINING: duration against the weekly window, peak memory against lane capacity, snapshot read throughput. **A better-discriminating, worse-calibrated model is not an improvement**, and **an unstable explanation is worse than none** |
| **Explainability** | TreeSHAP, emitted as the contributor dataset with `claim_class = PREDICTIVE_CONTRIBUTION` |
| **Refresh** | Full retrain weekly on the governed rolling window; recalibrate weekly on recent holdout |
| **Compute class** | CPU, minutes to a few hours depending on model count |
| **Refusal states** | INSUFFICIENT_DATA, NOT_APPLICABLE, REFUSED_BY_GUARD (leakage), MODEL_NOT_READY |
| **Emitted datasets** | Prediction dataset, contributor dataset |

**Eligibility expression, evaluated per (outcome_code, prediction_point_code) candidate:**

```
labelled_n            >= 500
AND minority_fraction >= 0.03            (classification only)
AND distinct_values   >= 20              (regression only)
AND legal_feature_n   >= 5
AND leakage_gate_G06  == PASS
AND outcome_availability != ambiguous_negative
AND history_span      >= 90 days
AND regime_stability  >= threshold
```

Thresholds are initial and are **OD-06**: to be confirmed by measurement, not by preference. Any candidate failing the expression produces a `MODEL_NOT_READY` evidence row naming the failed clause and its measured value. It does not silently disappear.

### 6.7 The model-count governor

The frozen rule warns against training hundreds of models but sets no mechanism. Without one, the candidate space is `outcomes x prediction_points x variant_levels` and grows multiplicatively. This closes CT-01.

**Governor rules:**

1. Candidates are enumerated, not trained. The full candidate list with eligibility results is a commissioning artifact.
2. Prediction points are activated in order of **information gain per position**, computed once at commissioning from the staged-attribution curve, not guessed.
3. A per-site **model budget** caps active supervised models. Initial default 50 per site. **OD-07** sets whether the budget is a fixed count, a compute-time budget, or both.
4. Variant-level models are never created by default. A single model with variant as a feature is the default; per-variant models require measured evidence of interaction and consume budget.
5. A model that fails champion-challenger twice consecutively is **quarantined** and releases its budget slot.

### MF-05 Effect and practice layer

| Attribute | Value |
|---|---|
| **Purpose** | Operating envelopes, matched practice comparison, intervention effect, remediation evidence |
| **Eligible inputs** | DP-1, DP-2 (controllable features only for recommendations), DP-4, DP-7 events where `is_intervention` |
| **Output** | Envelope rows and finding rows |
| **Staged levels** | L1 conditioned association; L2 matched or stratified comparison; L3 observational effect estimation; L4 experimental evidence |
| **Level eligibility** | L1: population and conditioning available. L2: matched support and overlap above threshold. L3: intervention records present, positivity and overlap defensible, declared confounder set. L4: a declared trial or controlled campaign exists in the data |
| **Validation** | Negative control must land inside its declared band; placebo-in-time test; sensitivity to unmeasured confounding reported; overlap and support reported |
| **Explainability** | The estimate itself plus conditioning, support, and the confounder set are the explanation |
| **Refresh** | Recompute weekly |
| **Compute class** | CPU, minutes to hours |
| **Refusal states** | All six. **CONTRADICTED_BY_CONTROL is specific to this family** and fires when a negative control moves |
| **Emitted datasets** | Envelope dataset, finding dataset |

**The recommendation rule, stated as a hard constraint.** A remediation suggestion may only be emitted when: the driver parameter is `controllable`; the estimate is at L2 or above; the support overlap exceeds the declared floor; and the suggested value lies inside the observed support range. Extrapolating a recommendation beyond observed practice is prohibited. Failing any clause produces a finding with `terminal_state = INSUFFICIENT_DATA` and no recommendation, never a weaker recommendation.

---

### LB-A7. INITIAL COMMISSIONING SEQUENCE (Deliverable E, part 1)

**Budget: hours to days. Every stage checkpointed. Restart resumes at the last successful stage.**

### 7.1 Stage table

| # | Stage | Depends on | Checkpoint artifact | Restart token | Compute | Failure policy |
|---|---|---|---|---|---|---|
| C1 | Validate semantic model, run IV-01..IV-08 | published SMV | `validation_report` | SMV id | CPU, minutes | IV-06 fails: **abort run** |
| C2 | Materialise journey spine | C1 | DP-1 partitions | last partition key | CPU, hours | Retry partition |
| C3 | Build outcome store | C2 | DP-4 | outcome_code | CPU, minutes | Skip outcome, record |
| C4 | Publish the feature-set definition | C1, C2 | `definition_versions` row with `feature_set_details` | definition version | CPU, minutes | Abort |
| C5 | Materialise `ppiq_plant.feature_store` | C4 | `feature_store` rows plus `feature_refresh_watermarks` | watermark | CPU, hours | Retry scope |
| C6 | Build sequence store | C2 | DP-3 partitions plus `channel_set_version` | partition key | CPU or IO, hours | **Skip family, continue** |
| C7 | Compute capability profile | C2..C6 | `capability_profile` | none | CPU, minutes | Abort |
| C8 | Enumerate candidates, apply governor | C7 | `candidate_manifest` | none | CPU, seconds | Abort |
| C9 | Train encoder (MF-01) | C6, C7 | encoder artifact plus epoch checkpoints | epoch | **GPU, hours to days** | Skip family, continue |
| C10 | Encode historical population | C9 | DP-5a partitions | partition key | GPU or CPU, hours | Retry partition |
| C11 | Build vector index generation 0 | C10 or C5 | DP-5b sealed manifest | none | CPU, minutes to hours | Skip family, continue |
| C12 | Fit novelty model (MF-03) | C10 or C5 | model artifact | none | CPU, minutes | Skip family, continue |
| C13 | Train supervised models (MF-04) | C5, C8 | one artifact per model | model key | CPU, hours | Skip model, record |
| C14 | Calibrate | C13 | calibration artifacts | model key | CPU, minutes | Skip model, record |
| C15 | Compute SHAP contributors | C13, C14 | contributor rows | model key | CPU, minutes to hours | Skip model, record |
| C16 | Compute envelopes and effects (MF-05) | C5, C3, C7 | evidence rows | finding key | CPU, hours | Skip finding, record |
| C17 | Materialise evidence store and datasets | C11..C16 | DP-7, DP-6 | dataset key | CPU, minutes | Retry |
| C18 | Run validation gates G-01..G-55 | C17 | `gate_report` | none | CPU, minutes | Blocking gates abort promotion |
| C19 | Activate the first model version per eligible serving identity | C18 | model registry record, live alias set | none | seconds | Atomic; no partial publish |

The rule's fifteen conceptual stages become nineteen because feature catalogue, candidate enumeration and gate execution are separately restartable in practice.

### 7.2 The skip-and-continue principle

Stages C6, C9, C10, C11 and C12 are marked **skip family, continue**. This is the mechanism behind the poorest-customer requirement. A customer with no time series loses the encoder, the embedding store and the embedding-based index. Commissioning still completes and activates the supervised family, the effect layer, the feature-space similarity index and the novelty model on engineered features.

**A skipped family writes a `MODEL_NOT_READY` evidence row naming the failed eligibility clause.** It does not vanish, and the readiness dataset reports it.

### 7.3 Checkpoint contract

Every stage writes `{run_id, stage_code, status, restart_token, started_at, completed_at, rows_written, artifact_refs[]}`. Restart reads the last row per stage. **No stage may re-read raw sources; every stage reads only the artifacts of prior stages.** This is what makes a failure at hour 19 cost one stage rather than the run.

---

### LB-A8. WEEKLY UPDATE SEQUENCE (Deliverable E, part 2)

**Hard budget: 24 hours. The design must fit with margin, not exactly.**

### 8.1 Stage and budget table

| # | Stage | Mode | Nominal | Ceiling | Droppable |
|---|---|---|---|---|---|
| W1 | Incremental ingest and canonical projection | delta | 1.0 h | 4 h | No |
| W2 | Extend spine | delta | 0.5 h | 2 h | No |
| W3 | Extend outcomes | delta | 0.2 h | 1 h | No |
| W4 | Incremental feature refresh | delta | 1.0 h | 4 h | No |
| W5 | Extend sequence store | delta | 0.5 h | 2 h | Yes, tier 4 |
| W6 | Encode new subjects, **frozen encoder** | delta | 0.5 h | 2 h | Yes, tier 4 |
| W7 | Extend index, new sealed generation | delta | 0.2 h | 1 h | Yes, tier 4 |
| W8 | Recompute capability profile and drift | full | 0.3 h | 1 h | No |
| W9 | Refit novelty model | full on window | 0.2 h | 0.5 h | Yes, tier 3 |
| W10 | Retrain supervised models | **full on rolling window** | 3.0 h | 8 h | Yes, tier 2 |
| W11 | Recalibrate | full on recent holdout | 0.2 h | 0.5 h | **No** |
| W12 | Recompute contributors | full | 0.5 h | 2 h | Yes, tier 3 |
| W13 | Recompute envelopes and effects | full | 1.0 h | 3 h | Yes, tier 3 |
| W14 | Champion / challenger validation | full | 0.5 h | 1 h | No |
| W15 | Materialise evidence and datasets | full | 0.5 h | 2 h | No |
| W16 | Gate run and atomic publish | full | 0.3 h | 1 h | No |
| | **Total** | | **10.4 h** | 35 h | |

Nominal fits with better than two times margin. The ceiling column exceeds 24 hours, which is precisely why the abort ladder exists.

### 8.2 The abort ladder

At T+18h the orchestrator evaluates remaining stages against remaining budget and degrades in this order. Every drop writes a supervisor decision row with reason `weekly_budget`.

| Tier | Dropped | Consequence |
|---|---|---|
| 1 | Nothing | Full weekly refresh |
| 2 | W10 supervised retrain | **Champion is retained. W11 recalibration still runs.** Models stay current in confidence even when not retrained |
| 3 | W9, W12, W13 | Novelty, contributors and effects carry last week's values, marked stale in the evidence envelope |
| 4 | W5, W6, W7 | New subjects are not embedded this week. They are queued and encoded next week. **Similarity for those subjects returns MODEL_NOT_READY, not a wrong neighbour** |
| Floor | W1..W4, W8, W11, W14, W15, W16 always run | Data, drift, calibration, validation and publish are never dropped |

**Why W11 recalibration is undroppable while W10 retraining is droppable.** Recalibration costs minutes and corrects the model's confidence to current conditions. Retraining costs hours and changes its structure. If only one can run, calibration delivers more truth per minute by a wide margin.

### 8.3 What is delta and what is full

| Delta | Full |
|---|---|
| Ingest, spine, outcomes, feature refresh by watermark, sequences, embeddings, index generation | Novelty fit, supervised training, calibration, contributors, effects, profile, gates |

**No model weight is ever incrementally mutated.** Supervised models are retrained from scratch on the governed rolling window. This is cheaper than it sounds and infinitely more reproducible than online updates.

### 8.4 Encoder policy in the weekly window

The encoder is never retrained in W-stages. The Drift Supervisor may *request* an encoder refresh; the request enters the governed refresh queue and executes in a scheduled window with commissioning-class budget, not in the weekly window. On completion it triggers: re-encode reference population, rebuild index generation 0, revalidate, and activate the new encoder version with its dependent models together.

---

### LB-A9. DAYTIME SERVING SEQUENCE (Deliverable E, part 3)

**NO TRAINING. Enforced by AD-08 and the Serving Wall.**

### 9.1 Request path

```
question or widget load
   -> Intelligence Orchestrator
      -> resolve to a tool or dataset request
      -> COST ESTIMATOR
         -> tier decision
            T1: read serving stores            target < 1 s
            T2: bounded compute on prepared    target < 30 s
            T3: insert analysis_job_request    returns immediately
   -> response with evidence envelope
```

### 9.2 Cost estimator inputs

Estimated before any work begins: target dataset partition count, estimated rows after filters, whether an aggregation crosses partitions, whether a join crosses data products, index generation count, and the current hardware tier.

**The estimator is conservative by construction.** When it cannot bound the cost, the answer is tier 3. It never optimistically starts work it may not finish.

### 9.3 Tier routing

| Tier | Reads | Budget | Timeout | Example |
|---|---|---|---|---|
| **T1** | DP-6, DP-7, DP-5b, active model version metadata, Layer A summaries | target under 1 s | 5 s hard | Why is risk high; what resembles this; the approved envelope; contributors |
| **T2** | DP-2 recent slice, DP-1, DP-4 through the governed aggregate path | target under 30 s | **60 s hard statement timeout** | Compare two cohorts the user just defined; a bounded correlation on a filtered slice |
| **T3** | Nothing synchronously | returns in under 1 s | n/a | Anything the estimator cannot bound |

**The absolute synchronous ceiling is under 2 minutes**, and the T2 hard timeout of 60 seconds sits well inside it so that orchestration, serialisation and rendering cannot push a request past the ceiling.

### 9.4 The T2 exploratory classification

This closes CT-04. A tier 2 computation is a user-defined analysis executed at query time. It has not passed G-06 leakage checks, has no negative control, and its population was defined by a filter rather than by a governed manifest.

**Therefore every T2 result is emitted with `claim_class = ASSOCIATION` and `evidence_kind = exploratory`, is never written to the evidence store, and can never be cited by the Assistant as a finding.** It is shown to the user with the qualification that it is an exploratory calculation, not a governed finding. A T2 result may be *proposed* for promotion, which creates a manifest row for the governed pipeline to evaluate on the next weekly run. It is never promoted at query time.

### 9.5 Serving state versus training state, stated as a table

| | Training state | Serving state |
|---|---|---|
| Reads | All data products, full history, sealed snapshots | Serving stores, bounded slices |
| Writes | `model_registry` rows at `trained`, evidence, snapshots | Job request rows, plus `prediction_runs`, `predictions` and `prediction_current` from the inference service |
| Compute | GPU permitted, unbounded scans, hours | Bounded scans, seconds. **No training dependency; GPU use is optional and benchmark-driven** |
| DB role | `ppiq_layerb_train` | `ppiq_layerb_serve`, SELECT plus narrow INSERT |
| Process | Scheduler-invoked jobs | Request-response service |
| May call the other | Activation writes to serving stores | **Never** |

---

### LB-A10. GOVERNED OUTPUT DATASETS (Deliverable G)

Seven datasets. Each is an ordinary governed analytical source declared in `ppiq_meta.registry_intelligence_sources` with `sourceKind = 'intelligence'`, bindable in Page Builder with no ML-specific code.

**One result envelope, source-declared row shapes.** The widget execution contract is `columns + rows + warnings` (Ch3 DF7). A source declares its own columns and their `columnRoles`. **A fact-shaped measure may project through WidgetFact into the generic aggregate executor; a native-grain rich source keeps its declared columns and is never flattened into a single value column.** The two classes are specified in section 45.4.

**Every dataset carries the EvidenceEnvelope columns from section 5, DP-7.** They are not repeated per dataset below.

**Aggregation semantics, closing CT-03.** Every measure declares an `aggregation_policy`. This is what prevents a Layer B estimate from being summed into something that looks like a Layer A fact.

| Policy | Meaning |
|---|---|
| `additive` | May be summed across any dimension |
| `semi_additive` | May be averaged, never summed |
| `non_additive` | May only be displayed at its native grain; aggregation is refused with a named message |
| `count_only` | The rows may be counted; the value may not be aggregated |

### 10.1 Prediction dataset

**Grain:** grain instance, prediction point, outcome.

| Field | Role | Aggregation |
|---|---|---|
| `grain_instance_key` | dimension identity | |
| `prediction_point_code`, `outcome_code`, `risk_band`, `variant_key`, `route_path_id` | dimensions | |
| `predicted_probability` | measure | **semi_additive** (mean allowed, sum refused) |
| `predicted_value` | measure | semi_additive |
| `subject_count` | measure | additive |
| `calibration_error` | measure | non_additive |
| `scored_at_utc` | time | |
| `model_code`, `model_version` | dimensions | |

**A user may chart mean predicted probability by variant. A user may not chart the sum of predicted probabilities, because that number would look like a count of expected defects while carrying none of a count's guarantees.** The refusal is generated by the aggregation policy, not by a special case.

### 10.2 Contributor dataset

**Grain:** prediction or finding, feature.

Dimensions: `parent_evidence_id`, `feature_code`, `parameter_code`, `direction`, `is_controllable`, `physical_quantity`, `unit_code`, `rank`.
Measures: `contribution_value` (semi_additive), `feature_value` (non_additive), `abs_contribution` (semi_additive).

`claim_class` is fixed to `PREDICTIVE_CONTRIBUTION` for SHAP-derived rows. A contributor row is not an effect and the column says so on every row.

### 10.3 Similarity dataset

**Grain:** subject, neighbour.

Dimensions: `subject_key`, `neighbour_key`, `neighbour_variant_key`, `neighbour_outcome_class`, `encoder_version`, `index_version`, `rank`.
Measures: `distance` (non_additive), `similarity` (non_additive), `neighbour_outcome_value` (semi_additive), `neighbour_count` (additive).

### 10.4 Anomaly dataset

**Grain:** spine node.

Dimensions: `grain_instance_key`, `process_position_code`, `regime_code`, `variant_key`, `model_version`.
Measures: `novelty_score` (semi_additive), `novelty_percentile` (non_additive), `subject_count` (additive).

No column asserts that an anomaly is a defect. Where an outcome model exists, `associated_outcome_rate` may be joined; it is a separate measure with its own envelope.

### 10.5 Operating envelope dataset

**Grain:** parameter, context combination, position.

Dimensions: `parameter_code`, `process_position_code`, `variant_key`, `context_keys`, `is_controllable`, `evidence_level` (L1..L4), `unit_code`.
Measures: `lower_bound`, `upper_bound`, `centre` (all non_additive), `observed_outcome_rate` (semi_additive), `population_n` (additive), `confidence` (non_additive).

### 10.6 Finding and effect dataset

**Grain:** finding.

Dimensions: `finding_id`, `driver_code`, `outcome_code`, `method_code`, `claim_class`, `evidence_level`, `terminal_state`, `status`, `is_controllable`, `conditioning`.
Measures: `effect_value`, `effect_lower`, `effect_upper` (non_additive), `population_n` (additive), `support_overlap` (non_additive).

**This dataset carries refusals as rows.** A dashboard filtered to `terminal_state = FINDING` shows findings; unfiltered it shows the honest picture including what the engine could not do and why.

### 10.7 Model and readiness status dataset

**Grain:** model or family, per site.

Dimensions: `model_family`, `model_code`, `outcome_code`, `prediction_point_code`, `readiness_state`, `failed_clause`, `model_version`, `champion_status`.
Measures: `measured_value` and `required_threshold` for the failed clause (both non_additive), `training_population_n` (additive), `days_of_history` (non_additive).

**This dataset is what a dashboard binds to when nothing is ready.** It is the reason a fresh installation shows a stated readiness picture rather than empty widgets, and it is the direct product realisation of Rule 2 starting empty without looking broken.

**Seven families is canonical (CT-07 CLOSED).** Model and Readiness Status is the seventh, and it is what a new installation binds to before any model is ready.

---

### LB-A11. ASSISTANT TOOL CONTRACTS (Deliverable H)

### 11.1 Common response envelope

Every Layer B tool returns:

```
{
  "result": <tool-specific payload or null>,
  "terminal_state": "FINDING | INSUFFICIENT_DATA | NOT_APPLICABLE |
                     REFUSED_BY_GUARD | CONTRADICTED_BY_CONTROL | MODEL_NOT_READY",
  "claim_class": "ASSOCIATION | PREDICTIVE_CONTRIBUTION |
                  MATCHED_EFFECT_ESTIMATE | CAUSAL_EVIDENCE | null",
  "refusal_reason": "<sentence, required when terminal_state != FINDING>",
  "evidence": {
    "evidence_ids": [...], "method_code": "...", "population_n": 0,
    "conditioning": {...}, "uncertainty": {...},
    "window_origin_mix": {...}
  },
  "provenance": {
    "model_code": "...", "model_version": 0,
    "semantic_manifest_id": "...",
    "training_window": {"from": "...", "to": "..."},
    "computed_at_utc": "...", "staleness_days": 0
  },
  "layer": "B"
}
```

**`layer` is present on every response from both engines.** It is how the Assistant keeps an exact fact and a learned estimate distinguishable in its own context, which is the only way the final answer can label them correctly.

### 11.2 Tool catalogue

| Tool | Input | Returns | Tier |
|---|---|---|---|
| `GetModelReadiness` | scope | Readiness rows per family and model, failed clauses with measured values | T1 |
| `GetModelVersion` | none | Active model version manifest, component versions, training window, promotion time | T1 |
| `GetPrediction` | grain instance, optional prediction point and outcome | Prediction rows | T1 |
| `GetPredictionContributors` | prediction id, top n | Contributor rows, claim class fixed | T1 |
| `FindSimilarJourneys` | subject id, k, optional filters | Neighbour rows with their outcomes and practices | T1 |
| `GetAnomalyEvidence` | subject id or window | Novelty score, percentile, contributing channels, nearest regime | T1 |
| `GetOperatingEnvelope` | parameter, context | Envelope rows with evidence level and population | T1 |
| `GetFinding` | filter by driver, outcome, status | Finding rows including refusals | T1 |
| `ComparePractices` | two cohort definitions, outcome | Matched comparison with support, overlap, conditioning, confounder limits | **T2** |
| `GetCompositePractices` | objective set, scope/context | Supported Pareto/non-dominated practice set or preference-resolved ranking, full objective vectors and evidence; MO01 when one winner is not lawful | **T2** |
| `CompareOperationalPeriods` | period A, period B, scope, registered measures | Layer-A exact driver decomposition with transition/stabilisation/stable-run context and evidence; no causal promotion | **T1** |
| `ProposeRemediation` | subject id or condition | Suggestion **only if** MF-05 recommendation rule passes; otherwise refusal | T1 |
| `RequestAnalysisJob` | analysis spec | Job id and state | T3 |

### 11.2a The runtime the tools sit inside

The eleven tools are step 4a of a nine-step runtime specified in Chapter 4 5.7.9 (amendment C4-7):

```
[1] permission and tenant context   [2] intent and entity resolution
[3] DETERMINISTIC TOOL PLANNER      [4a] structured tools  [4b] evidence retrieval
[5] evidence packing, budgeted      [6] model gateway      [7] LLM, phrasing only
[8] deterministic answer verification                      [9] cited answer or refusal
```

Four properties of that runtime bear directly on the Layer B tool contract:

- **The LLM does not choose tools.** A planner maps resolved intent to a declared tool set. Tool-selection accuracy is gated (Q-01)
- **Permission filtering happens before ranking**, not after, so a forbidden chunk cannot displace a permitted one
- **Structured tools take precedence over retrieval for facts and analytical results.** A number never comes from a retrieved chunk when a tool can compute it
- **Verification is deterministic and does not call the LLM.** Every numeric claim must resolve to a handle the tools supplied

### 11.3 Assistant discipline rules

1. The Assistant never queries a model artifact, a training store, or a candidate model version. Only these eleven tools plus the Layer A tools.
2. The Assistant may not upgrade a claim class. A `PREDICTIVE_CONTRIBUTION` may not be phrased as a cause. A tool-response claim class maps to a fixed set of permitted phrasings.
3. When `terminal_state` is not `FINDING`, the Assistant states the refusal. It does not substitute a general-knowledge answer, and it does not soften the refusal into a hedge.
4. A numeric answer to a quantity question must carry the unit from `physical_quantity` plus `unit_code`. A response whose unit does not match the quantity class of the question is a **hard failure**, not an inaccuracy, and gate G-20 tests exactly this.
5. Every answer containing a learned claim carries at least one `evidence_id`.
6. When both layers contribute, the answer separates them explicitly: the exact fact, then the learned finding, then the evidence, then the qualification.

---

### LB-A12. PAGE BUILDER INTEGRATION (Deliverable I)

### 12.1 Registration contract

At activation, the Evidence Materialiser registers each of the seven datasets in the same dataset catalogue Layer A uses. Registration supplies: dataset code, grain, dimension list with semantic types and labels, measure list with units and **aggregation policy**, time field, default filters, and compatibility hints.

**From the Page Builder's perspective there is no difference between an intelligence dataset and any other governed dataset.** The metadata endpoint returns the same shape.

### 12.2 The customer journey, unchanged from ordinary data

```
Add Widget -> select dataset (an intelligence dataset appears in the same list)
  -> choose dimension (from metadata, not a compiled list)
  -> choose measure (aggregation policy enforced by the engine)
  -> chart types narrow automatically by compatibility
  -> filter, save, cross-filter
```

### 12.3 Execution and the two source classes

The widget execution contract is `columns + rows + warnings`, with `sourceKind` of `canonical` or `intelligence`, plus `intelligenceSource` and `columnRoles` (Ch3 DF7).

| Class | Examples | Execution |
|---|---|---|
| **Fact-shaped aggregate source** | Exact canonical measures; aggregateable intelligence measures such as mean risk score by variant, finding count by status | May project through `WidgetFact` into the generic aggregate executor |
| **Native-grain rich source** | Readiness rows, findings, prediction detail, contributors, similarity neighbours, practice matches, value derivation, remediation eligibility | **Keeps its governed multi-column shape.** Never flattened into a single decimal value |

Both classes use the same registry, the same authoring shell, the same selection and filter contract, the same result envelope, the same widget system and the same evidence rules.

The aggregate engine gains **no** knowledge that a measure is learned. It gains exactly one behaviour: it honours `aggregation_policy` and refuses a disallowed aggregation with a named message. That mechanism is generic and applies equally to any Layer A measure declared non-additive.

### 12.4 Prohibitions, testable

- No `if predictionDashboard`, no `if oilCustomer`, no branch on dataset origin
- No compiled list of intelligence fields anywhere in the Page Builder
- No React component required merely because data came from Layer B
- A special visualisation is permitted only when it expresses a genuinely different visual grammar. **The candidate list is short and is OD-09**: a neighbour-comparison view and a contribution waterfall are the two plausible cases. Both must be justified as grammar, not as origin

### 12.5 The one honest consequence

Because intelligence datasets are ordinary datasets, a customer can build a widget that is statistically misleading, for example an envelope chart over a population of eleven. The mitigation is not to restrict the builder. It is that `population_n` and `terminal_state` are ordinary fields the widget can display, and the default chart templates for intelligence datasets include them. **OD-10** decides whether a minimum-population warning renders automatically on intelligence widgets.

---

### LB-A13. GENERICITY PROOF (Deliverable J)

Two conceptual installations. Industry vocabulary appears **only** in the configuration column of the mapping tables below. It appears nowhere in any contract, schema, sequence, tool or dataset defined in sections 4 through 12.

### 13.1 Installation ONE - continuous process (illustrative customer vocabulary: oil/refining/blending only in this proof fixture)

| Contract | Configured value |
|---|---|
| SM-02 primary grain | `grain_kind = flow_interval`, one hour intervals per train |
| SM-02 secondary grain | `grain_kind = batch` for blended product tanks |
| SM-03 positions | Six: desalting, atmospheric distillation, hydrotreating, reforming, blending, tank certification. Route is a **graph**, not a chain |
| SM-03 residence | `lag_with_dispersion` on all flow positions. Lag 45 to 900 s, dispersion 20 to 300 s |
| SM-04 edges | `transformation`, many_to_many, `weight_semantics = proportion` for blending; `temporal` edges within trains |
| SM-05 parameters | Feed rate, reactor temperature, pressure, hydrogen partial pressure, reflux ratio, catalyst bed temperature. Mixed controllable and observed |
| SM-06 outcomes | `sulphur_content` continuous, detection at tank certification; `octane_index` continuous; `off_spec_flag` binary |
| SM-07 events | Trips, catalyst regeneration, valve interventions. `is_intervention = true` on operator setpoint changes with recorded dose |
| SM-08 variant dimension | Product quality class. Observed levels: 4 |
| SM-09 resources | Catalyst beds with age counters |
| SM-12 prediction points | After hydrotreating; after reforming |
| Genealogy strength | **transformational** |

### 13.2 Installation TWO - mineral water, bottling

| Contract | Configured value |
|---|---|
| SM-02 primary grain | `grain_kind = batch`, one production order per format |
| SM-02 secondary grain | `grain_kind = process_window`, fifteen minute windows for filler monitoring |
| SM-03 positions | Five: source treatment, ozonation, blow moulding, filling and capping, palletising. Route is a **chain** |
| SM-03 residence | `fixed_lag` on treatment; `none` on filling, where identity is tracked |
| SM-04 edges | `containment` from batch to pallet; `identity` through the filler; `transformation` at blow moulding with proportion weights from preform lots |
| SM-05 parameters | Ozone dose, preform temperature, blow pressure, fill volume, cap torque, line speed, conductivity, TDS |
| SM-06 outcomes | `microbio_result` binary, detection at lab release **days after production**; `fill_volume_deviation` continuous, detection at filling; `cap_leak_rate` continuous |
| SM-07 events | Line stops, CIP cycles, mould changes. `is_intervention = true` on CIP triggered outside schedule |
| SM-08 variant dimension | Bottle format. Observed levels: 3 |
| SM-08 shift dimension | **Observed levels: 1. Status COLLAPSED.** Removed from all conditioning with reason `collapsed_single_level` |
| SM-09 resources | Moulds and filling valves, instance identity available |
| SM-12 prediction points | After blow moulding; after filling |
| Genealogy strength | **sequential**, transformational at moulding |

### 13.3 The invariance table - what did NOT change

| Element | Oil | Water | Same? |
|---|---|---|---|
| Input contract objects SM-01..SM-12 | Used | Used | **Identical** |
| Data products DP-1..DP-7 schemas | Used | Used | **Identical** |
| Intelligence and engine family registry MF-01 to MF-07 | Used | Used | **Identical** |
| Commissioning stages C1..C19 | Used | Used | **Identical** |
| Weekly stages W1..W16 and abort ladder | Used | Used | **Identical** |
| Daytime tiers T1..T3 | Used | Used | **Identical** |
| Output datasets 10.1..10.7 | Used | Used | **Identical** |
| Assistant tools, all eleven | Used | Used | **Identical** |
| Page Builder binding path | Used | Used | **Identical** |
| Validation gates G-01 to G-55 | Used | Used | **Identical** |

**No `OilModel`. No `BottleModel`. No branch on industry anywhere.**

### 13.4 The three points where the installations genuinely differ, and how each is handled by configuration

**1. Route topology.** Oil is a graph, water is a chain. Handled by SM-03 `predecessor_position_codes`. The spine builder walks a graph in both cases; a chain is a graph with one path.

**2. Detection lag on the outcome.** Water's microbiological result arrives days after production; oil's sulphur result arrives at certification. Handled by SM-06 `detection_timestamp_field`, consumed by G-06. **The same leakage gate produces a different legal feature set for each installation with no code difference.** This is the sharpest single demonstration in the pack: leakage prevention is generic because the detection anchor is declared.

**3. A collapsed dimension.** Water runs one shift. Handled by the SM-08 collapse rule. Oil runs four; its shift dimension is active. Same code, different profile.

### 13.5 What the proof does not claim

It does not claim that the same *findings* appear, that the same *models* become eligible, or that both installations reach the same ladder level. Oil's transformational genealogy with proportion weights will produce weaker attribution than water's tracked identity through the filler. **That difference is measured and reported by the capability profile, not hidden.** Genericity means one architecture, not equal outcomes.

---

### LB-A14. SCALE AND HARDWARE SIZING STRATEGY (Deliverable K)

### 14.1 Raw data volume is not training feature volume

These are different quantities and conflating them is the most common sizing error.

```
RAW VOLUME       = sum over sources of rows x bytes
                   Dominated by high-frequency sensor history and text logs.
                   Frequently 90 percent application logging with near-zero
                   learning value.

FEATURE VOLUME   = grain_instances
                   x process_positions
                   x prediction_points
                   x features_per_row
                   x 8 bytes

SEQUENCE VOLUME  = grain_instances x positions x channels
                   x mean_sequence_length x 5 bytes
                   (float32 value plus mask, with compression)
```

**Worked illustration.** 25 million grain instances, 5 positions, 2 prediction points, 400 features gives a feature volume near 400 GB uncompressed and materially less columnar-compressed. The same installation's raw volume may be 75 TB. **The learning workload is sized by the middle number, not the first.**

### 14.2 Workload dimensions to measure per installation

Historical grain instances; new instances per week; process positions; prediction points; features per row; sequence channels; mean sequence length; distinct outcomes; active model count; history window in months; variant levels; and the four resource axes CPU cores, RAM, GPU memory, storage throughput.

### 14.3 Execution tiers

| Tier | Shape | What changes architecturally |
|---|---|---|
| **Small** | One CPU server, optional single GPU | Everything in-process. Feature build single-threaded per partition. Index in memory |
| **Medium** | Larger CPU and RAM, one dedicated GPU | Parallel partition workers. Encoder training on GPU. Index memory-mapped |
| **Large** | Partitioned processing, multiple workers, one or more GPUs | Feature build sharded by partition key across workers. Boosting with distributed histogram or per-shard models merged. Index sharded by generation |
| **Very large** | Distributed feature and training execution | Distributed PyTorch (DDP or FSDP) for the encoder only where measured to be necessary. Distributed boosting. Index sharded across nodes with a routing layer |

**No thresholds are stated here, deliberately.** The frozen rule forbids hardcoding them without measurement, and the honest position is that we have not measured. **OD-11** is the benchmark plan that produces them.

### 14.4 Sequence store retention is the dominant storage decision

The sequence store is typically the largest product and the one with the least reuse after the encoder is trained and the population encoded. Three policies, and this is **OD-12**:

| Policy | Storage | Cost |
|---|---|---|
| Full retention | Highest | Encoder retraining always possible on full history |
| Rolling window plus reservoir sample of older data | Moderate | Retraining on a sample; some loss of rare regimes |
| Encode and discard beyond window | Lowest | **Encoder can never be retrained on discarded history**, which conflicts with the quarterly refresh policy |

The third option is cheap and quietly destructive. It must not be selected by default.

### 14.5 What must never be scaled by raising a cap

Raising a row cap to make an aggregate complete is not remediation. Where a computation exceeds its budget the answer is partitioned execution, a governed pre-aggregate, or an explicit bounded refusal. **A plausible partial value is prohibited at every tier.**

---

### LB-A15. VALIDATION AND QUALITY GATES (Deliverable L)

| ID | Gate | When | Blocking | Evidence produced |
|---|---|---|---|---|
| **G-01** | Semantic model published and hash-stable | C1, W16 | Yes | Version and hash |
| **G-02** | Relationship quality IV-01..IV-08 within declared bounds | C1, W8 | **IV-06 yes**, others record | Metric table |
| **G-03** | Spine completeness: every instance has at least one node, no orphan nodes | C2, W2 | Yes | Counts |
| **G-04** | Feature schema hash matches the catalogue that produced it | C5, W4 | Yes | Hash pair |
| **G-05** | Target leakage: no feature derived from the outcome definition | C8, W10 | Yes | Feature-to-outcome dependency trace |
| **G-06** | **Temporal leakage**: for every model, every feature's availability position and offset precede the prediction cutoff, and the outcome detection time follows it | C13, W10 | **Yes** | Per-feature legality table |
| **G-07** | Class balance above floor, or the model is not created | C8, W10 | Yes | Measured fractions |
| **G-08** | Outcome sufficiency: population, observed-flag integrity, no ambiguous negatives | C3, W3 | Yes | Counts, ambiguity flag |
| **G-09** | Feature missingness below ceiling per feature and per row | C5, W4 | Records, blocks per-feature | Missingness table |
| **G-10** | Regime stability across the training window | C7, W8 | Records | Stability statistic |
| **G-11** | Input and representation drift within bounds | W8 | Triggers supervisor action | Drift metrics |
| **G-12** | Calibration error below ceiling on recent holdout | C14, W11 | Yes | Reliability curve summary |
| **G-13** | **Encoder compatibility**: the model registry record's `channel_set_version` equals current | C9, W6, W16 | **Yes** | Version pair |
| **G-14** | **Embedding-version compatibility**: index generation chain resolves to exactly one encoder version | C11, W7, W16 | **Yes** | Generation chain |
| **G-15** | Model performance above the mandatory baseline model | C13, W10 | Yes | Candidate versus baseline metrics |
| **G-16** | Subgroup and variant stability: no variant level below the declared floor | C13, W10 | Records, blocks promotion on severe | Per-variant metrics |
| **G-17** | Champion versus challenger on the same governed holdout | W14 | **Promotion gate** | Comparison table plus decision reason |
| **G-18** | Reproducibility: seeds, dataset manifest, code identity, environment, artifact hashes all present | C19, W16 | Yes | Manifest |
| **G-19** | **Refusal integrity**: every non-FINDING row names a method-side cause where the cause is method-side, and carries the measured statistic where the cause is data-side | C17, W15 | Yes | Refusal audit |
| **G-20** | **Unit sanity**: every Assistant numeric response unit matches the physical quantity class of the question, tested against a fixed probe set | C19, W16 | **Yes** | Probe results |
| **G-21** | **Tenant isolation**: no training population, index generation, embedding or evidence row crosses tenant boundary | C19, W16 | **Yes** | Boundary scan |
| **G-22** | Rollback drill: the previous model version can be reactivated and reproduces its recorded metrics | C19, monthly | Yes | Drill record |

**Every gate is falsified once before it is trusted.** A gate that has never failed on a known-bad input is not evidence that the property holds; it is evidence that the gate was never exercised. The falsification record is part of the gate's own artifact.

**No model reaches production because training completed.** Promotion requires G-17 plus every gate marked blocking.

---

### LB-A16. CROSS-CONTRACT RECONCILIATION

### 16.1 Layer A <-> Layer B

| Question | Resolution |
|---|---|
| Who owns exact counts? | **Layer A, always.** Layer B never estimates a fact that Layer A can compute exactly |
| Can Layer B read Layer A outputs? | Yes, as features, provided they pass G-06 |
| Can Layer A read Layer B outputs? | Yes. Intelligence datasets are ordinary governed datasets and the generic aggregate engine reads them, subject to `aggregation_policy` |
| Can a Layer B measure be summed into a fact-shaped number? | **No.** `aggregation_policy` on the measure refuses it by name |
| Where does the boundary appear to the user? | In the `layer` field on every tool response and in the Assistant's answer structure |

### 16.2 Layer B <-> Page Builder

Layer B emits registered datasets with metadata. Page Builder holds no compiled ML field list and no origin branch. The aggregate engine gains one generic behaviour, aggregation-policy enforcement, which is not ML-specific.

### 16.3 Layer B <-> Assistant

Eleven tools, one response envelope, claim class on every response, refusal as a first-class result, evidence ids mandatory on learned claims, and a fixed mapping from claim class to permitted phrasing.

### 16.4 Semantic model <-> ML

Every model version, feature snapshot, prediction and finding pins a **`semantic_manifest_id`**, plus the canonical identities it depends on: `feature_set_version_id`, `source_definition_id` and `source_definition_version`. A republished definition does not silently invalidate models; it creates a new manifest, and the supervisor decides whether to retrain, quarantine or continue. **Training against a mutable ad-hoc definition is structurally impossible because the trainer reads only published versions and sealed snapshots.**

### 16.5 Weekly scheduler <-> model registry

The scheduler produces candidate model versions and never sets `status = 'active'`. Only the Model Activator does, only after G-17 and all blocking gates pass. A failed weekly run leaves the current active version untouched.

---

### LB-A17. MODEL REGISTRY, ACTIVATION AND ROLLBACK (Deliverable F)

**Canonical: `ppiq_plant.model_registry` (Ch3 4.5.12), governed per serving identity.**

### 17.1 Serving identity

```
serving identity  = ( tenant_id , model_code , outcome_code , grain_code )
serving version   = serving identity + model_version
```

`outcome_code` and `grain_code` are **model identity, not metadata**. A model predicting one outcome at one grain is not interchangeable with one predicting another. Both are set at training from the model definition and are immutable for the version. Every uniqueness rule, activation rule, fallback rule, drift record, artifact cache key and compatibility check uses this five-part identity and never a shorter one.

### 17.2 The two independent axes

| Axis | Column | Values |
|---|---|---|
| **Lifecycle** | `status` | `trained`, `rejected`, `active`, `review`, `retired` |
| **Serving approval** | `serving_role` | `none`, `serving_fallback` |

A model is the active primary when `status = 'active'`. It is an approved fallback when `serving_role = 'serving_fallback'`. **There is no `fallback_approved` lifecycle status, and no state is ever encoded in both columns.**

### 17.3 Constraints that make the relationship unambiguous

| Constraint | Effect |
|---|---|
| Partial UNIQUE `(tenant_id, model_code, outcome_code, grain_code)` WHERE `status = 'active'` | **At most one active version per serving identity** |
| Partial UNIQUE `(tenant_id, model_code, outcome_code, grain_code)` WHERE `serving_role = 'serving_fallback'` | **At most one approved fallback per serving identity** |
| CHECK `serving_role = 'none' OR status IN ('active','trained')` | A retired, rejected or under-review model can never hold a fallback approval |
| CHECK `NOT (status = 'active' AND serving_role = 'serving_fallback')` | One version can never be both primary and fallback for the same identity, because a fallback that is already the primary would silently mask the absence of a safety net |

Every UNIQUE constraint on a tenant-owned table carries `tenant_id` as its first column. Row-level security filters what a query returns; **it does not make a UNIQUE constraint tenant-local**, and a constraint omitting `tenant_id` would leak across tenants through violation messages.

### 17.4 What a version records

`definition_version_id`, `algorithm`, `feature_set_version_id`, `feature_list`, `training_snapshot_id`, `split_strategy`, `missing_value_policy`, `scaling_params`, `hyperparameters`, `metrics`, `acceptance_floor`, `artifact_uri`, `trained_at_utc`, `activated_at_utc`, `retired_at_utc`, `validity_until_utc`.

Training runs are `model_training_runs`, carrying `policies_applied`, row counts, metrics, importance, calibration, and **CHECK `overlap_rows = 0`**, which makes leakage a database-level impossibility rather than a test.

### 17.5 Activation, fallback and rollback

**Activation** sets `status = 'active'` on a version and retires the previous holder of that serving identity. The partial unique index makes the transition atomic per identity.

**Fallback** is an explicit approval, never inferred from the last active version. The six conditions a fallback must satisfy are Ch4 5.6.7a. `prediction_runs.fallback_model_registry_id` and `fallback_reason` record its use, and `prediction_current.fallback_in_use` surfaces it.

**Rollback** activates the prior version of the same serving identity. Because activation is per identity, a rollback of one model never disturbs another.

### 17.6 What every artifact pins

`definition_version_id` and `feature_set_version_id` and `training_snapshot_id`, plus `source_definition_id` and `source_definition_version` for the relationship publication in force. A result is explicable from these alone.

### LB-A18. TRADEOFFS, OPEN DECISIONS, AND WHAT THIS DESIGN DOES NOT DECIDE

### 18.1 Deliberate tradeoffs, with what each costs

| # | Tradeoff | Chosen | Cost accepted |
|---|---|---|---|
| 1 | Boosting over deep networks for the decision | Boosting | Gives up some accuracy on strongly sequential effects; buys attribution, speed and defensibility |
| 2 | Full weekly retrain over incremental updates | Full retrain | Costs hours weekly; buys reproducibility and comparability |
| 3 | Materialised feature store over per-run computation | Materialised, incremental | Costs a refresh pipeline and watermark discipline; buys analysis cost proportional to what changed rather than to what exists |
| 4 | Generational index over live mutation | Generational | Search fans out over generations; buys a reproducible retrieval result |
| 5 | Per-serving-identity activation | Canonical | A dependent set (encoder, index, models) is activated together by procedure and proven by G-13 and G-14, not by a container object |
| 6 | Refusal as data | Materialised rows | Storage and query surface; buys honest dashboards and an auditable engine |
| 7 | Conservative cost estimator | Conservative | Some answerable questions go to tier 3; buys a latency contract that holds |
| 8 | Encoder optional | Optional | Two code paths in MF-02 and MF-03; buys the poorest customer a working product |

### 18.2 Architectural decisions - all closed

**No architectural decision remains open.** The three that governed storage, scope and output families are closed, and the closures are recorded in the Layer B Rule appendices and in section 43.

| ID | Decision | State |
|---|---|---|
| **OD-02** | Storage placement of Layer B outputs and artifacts | **CLOSED.** The three-schema law stands. Customer-derived analytical and intelligence datasets to Plant Data; operational and control-plane metadata to Meta Data; pre-semantic source-shaped data to Dump Store; model binaries, checkpoints and vector-index artifacts to object storage. No fourth application schema. Section 43.4 |
| **OD-13** | Scope authority between the Layer B Rule and the Master Design chapters | **CLOSED.** Chapter 2, then Chapter 3, then Chapter 4, then the Rule as a subsystem constitution. Where the Rule is narrower, the chapters govern. Rule Appendix A |
| **CT-07** | Six or seven governed output dataset families | **CLOSED.** **Seven**, including Model and Readiness Status, so a new installation renders `MODEL_NOT_READY` and `INSUFFICIENT_DATA` truthfully rather than appearing broken |

The remaining open items are **measured parameters with canonical homes**, listed in section 40.2. Each is a number to be measured and written into an existing canonical field. None is an architecture decision.

### 18.3 Rule reconciliation

How this pack reconciles with the Layer B Rule. Every item is closed. The Rule carries Appendix A, which subordinates it to the Master Design chapters and names the capabilities its body omitted.

| ID | Contradiction | Status |
|---|---|---|
| **CT-01** | Section 9 forbids training hundreds of models but sets no mechanism, while sections 12 and 13 imply a model per outcome per prediction point | **Closed here** by the model-count governor, section 6.7. Thresholds are OD-07 |
| **CT-02** | Weekly incremental index insertion versus reproducible retrieval. A mutating index cannot be a pinned artifact | **Closed** by AD-06 generational index |
| **CT-03** | Section 4 forbids using ML to approximate an exact BI fact; section 21 makes Layer B outputs ordinary datasets, which lets a user sum predicted probabilities into something shaped exactly like a fact | **Closed here** by `aggregation_policy` |
| **CT-04** | Section 19 permits tier 2 bounded calculation at query time, which bypasses every gate in section 13 and L, yet its output is presented alongside governed findings | **Closed here** by the exploratory classification, section 9.4 |
| **CT-05** | Sections 11 and 13 require intervention history for effect levels 3 and 4 and for the profile, but the section 2 declaration contract defines no intervention object | **Closed here** by SM-07 `is_intervention` |
| **CT-06** | Section 15 says freeze the encoder between refreshes, but a structural change to the instrument set makes a frozen encoder invalid rather than merely stale. The rule has no concept for this | **Closed here** by `channel_set_version` and G-13 |
| **CT-07** | Six versus seven governed output dataset families | **CLOSED. Seven**, including Model and Readiness Status, which is what an empty installation binds to |
| **CT-08** | Measured sizing versus the two-minute ceiling | **Not a defect, and stated to customers.** The latency contract holds at every tier; what varies by tier is how many questions are answerable synchronously |

### 18.4 What this design intentionally does NOT decide

So that design is never mistaken for implementation authorisation:

1. No physical storage product is selected. Placement is settled in section 43.4; the storage engine behind each placement is an OD-01 benchmark.
2. No hyperparameters, no architecture choice between temporal convolution and Transformer. That is a measured comparison, not a design decision.
3. No threshold in this pack is final. Every numeric eligibility value is a placeholder pending OD-05, OD-06 and OD-11.
4. No hardware sizing number is quoted. No customer may be given one until OD-11 completes.
5. No API route shapes, no class names, no module layout, no language-level interfaces.
6. No migration plan from the current codebase to this architecture.
7. No estimate. Nothing here states how long any work package takes.
8. No backlog task. Decomposition is Worker 2's, once the open decisions are ruled.
9. No decision on whether Layer B ships in a licence tier, or which.
10. No commitment that any specific customer's data supports any specific output. That is what the capability profile exists to measure, per installation.

---

### LB-A19. WHAT PART ONE ESTABLISHES

Part One is directly decomposable into work packages. Every item below states a canonical contract with no decision left to the implementer.

- the eleven input contract objects and their validation gates (section 4)
- all seven data products with field-level contracts, mutability, partitioning and lineage, bound to canonical objects in section 43 (section 5)
- the seven intelligence and engine families MF-01 to MF-07 with eligibility expressions, validation metrics and refresh policies (section 6)
- all three orchestration sequences with stage dependencies, checkpoints, budgets and the abort ladder (sections 7, 8, 9)
- the model registry, serving identity, activation, fallback and rollback design (section 17)
- the seven governed output dataset families under one result envelope with source-declared row shapes (sections 10, 45)
- the eleven Assistant tools with a common evidence-bearing response envelope (section 11)
- the Page Builder registration and execution contract (section 12)
- the complete gate inventory G-01 to G-55 (sections 15, 30.2, 40.1)

**Storage placement is settled** and is section 43.4: analytical and intelligence datasets to Plant Data, operational metadata to Meta Data, source-shaped data to Dump Store, binaries to object storage, no fourth application schema.

**What remains is measurement, not architecture.** Eligibility thresholds, the hardware benchmark, retention values and the reserved interactive capacity fraction are numbers to be measured and written into existing canonical fields. Section 40.2 names each field.

---
---

# PART TWO - MASTER DESIGN CONVERGENCE PASS

---

### LB-A20. RESPONSE TO THE TRACEABILITY AUDIT

### 20.1 What I accept without qualification

Seven of the eight gaps are real, and six of them are my errors of scope rather than errors of the audit.

| Gap | My assessment |
|---|---|
| **Statistical and correlation engine** | **Accepted, and it is the worst of the eight.** Part One reduced correlation to one line inside daytime tier 2. The Master Design treats it as a standing governed engine producing findings, running before ML, on its own schedule. A bounded query-time calculation is not that engine. This is a category error on my part, not an omission of detail |
| **Practice learning engine** | **Accepted.** MF-05 conflated two different things: envelope mining, which is a statistical summary, and practice signature learning, which is a canonicalisation and matching problem with its own data product. Collapsing them lost the exact-versus-relaxed matching, the back-off rule and the sensitivity state entirely |
| **Operational prediction contract** | **Accepted.** Part One designed how a prediction is produced and stored, and said nothing about whether it arrives while the plant can still act on it. That is the whole point of goal (c). A prediction delivered after the last actionable stage is a record, not an intervention |
| **Nine-check remediation gate** | **Accepted.** My four-clause rule was a simplification of a safety surface. In a live plant a recommendation reaches a person who may act on it. Four checks where the design requires nine is not a simplification, it is a reduction in safety margin |
| **Decision, outcome, effectiveness, feedback loop** | **Accepted, and this is the largest conceptual gap.** Part One has a learning lifecycle for models. It has no lifecycle for the industrial event: prediction, recommendation, human decision, action, actual outcome, evaluation. Without it the claim that the system becomes the in-house expert is unevidenced |
| **Value engine** | **Accepted as missing.** With one correction to the audit in 20.2 below |
| **Scenario simulation and full supervisor** | **Accepted.** My Drift Supervisor is a monitoring component. The Master Design supervisor proposes bounded adjustments, shadow-runs them, compares on held-out history and requires human approval. Those are different components with the same name |

### 20.2 Where I qualify the audit

**Q1. The percentages should not enter a governance document.** The audit states 85 to 90 percent of the Layer B rule and 65 to 75 percent of the full engine contract. The gap list is correct and actionable; the percentages are not measurable against any defined denominator. The traceability matrix in section 31 replaces them with a per-capability state, which is falsifiable.

**Q2. Text evidence and inspection-image intelligence are not gaps of the same class as the other seven.** The other seven are missing designs for capabilities whose inputs already exist in the semantic model. These two are new **modalities**: a different input contract, a different encoder family, a different storage profile, different eligibility, and for images a materially different compute cost. Designing them inside a convergence pass would produce a shallow design that looks complete and is not. **Section 29 therefore defines the modality extension contract** - how any new modality plugs into the semantic model, the sequence store analogue and the evidence envelope - and explicitly does not design vision or NLP. That keeps the genericity claim honest.

**Q3. The currency in the value engine must not be euro.** The audit says bounded euro impact. A generic product declares its currency per tenant. Section 26 uses `currency_code` throughout. This is a small thing that would have become a genericity defect on the first non-eurozone customer.

**Q4. The feedback loop introduces a write path, and that needs stating explicitly.** PPIQ is a read-only platform with respect to customer systems. Decisions, action assignments and effectiveness records are writes. They are writes into **PPIQ's own governed decision ledger**, never into a customer system and never into a control system. Section 25.6 states this as a hard boundary, because a reviewer encountering accept, reject, defer and assign for the first time will reasonably ask whether the product has started controlling the plant. It has not, and the architecture must say so in a place a reviewer will find it.

### 20.3 What the audit did not find, and I am raising myself

**F1. Closing these gaps makes the Architecture Pack exceed its own governing rule.** The frozen Layer B rule does not contain a statistical engine, a practice signature product, a decision ledger, a value engine or a scenario surface. If Part Two is added, the pack becomes broader than the constitution it was written to satisfy. That is a governance inconsistency, not a technical one, and it has exactly two honest resolutions:

- **Amend the frozen rule** with an appendix naming the eight capabilities as in-scope for Layer B, keeping the rule as the authority; or
- **Rule that the Master Design chapters outrank the Layer B rule** on scope, and record the Layer B rule as a subset instrument.

**OD-13 is CLOSED in favour of the chapters**, which predate the rule. The Rule carries Appendix A recording the authority order, so the two documents no longer claim scope over the same subsystem.

**F2. The eight gaps are not independent, and treating them as a flat list will produce rework.** Section 32 gives the dependency order. Three of the eight can be contract-reserved rather than fully designed now, without blocking backlog decomposition of the rest.

**F3. The statistical engine and the supervised family now overlap and need a boundary rule.** Both can answer "is parameter X related to outcome Y". Without a boundary they will produce two different numbers for the same question and the Assistant will have to choose. Section 21.6 defines the boundary.

---

### LB-A21. STATISTICAL INTELLIGENCE ENGINE (DF9) - MODEL FAMILY MF-06

**Governed correlation is this engine.** Tier 2 calculation in the daytime path (section 9.4) is exploratory only and produces no finding.

### 21.1 Position in the chain

DF9 runs **before** ML training, on its own schedule, and produces findings that stand alone. It is not a preprocessing step for MF-04 and it is not a query-time convenience. A customer whose data never becomes eligible for supervised models still receives DF9 findings, which is why it must not be downstream of the model families.

### 21.2 The method registry, DP-15 `statistical_method_registry`

> **BOUND TO `the registry-driven Group B, C and D block rows of Chapter 4 5.5, with results in ppiq_plant.correlation_results and runs in compute_runs`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Registry-driven, never a hardcoded switch. One row per method.

| Field | Type | Notes |
|---|---|---|
| `method_code` | text | PK |
| `method_family` | enum | correlation, association, group_difference, distribution, trend, lag, stability |
| `x_type` | enum | numeric, ordinal, categorical_binary, categorical_multi, time |
| `y_type` | enum | same domain |
| `assumptions` | text[] | normality, monotonicity, independence, homoscedasticity, expected_cell_count |
| `assumption_tests` | text[] | The test that decides whether each assumption holds |
| `min_population` | int | |
| `min_cell_count` | int | Nullable, categorical methods |
| `effect_size_measure` | text | r, rho, Cramer V, eta squared, Cliff delta, odds ratio, rate difference |
| `significance_measure` | text | p, exact p, permutation p |
| `supports_stratification` | bool | |
| `supports_lag` | bool | |
| `output_dataset` | text | Always the finding dataset |
| `is_enabled` | bool | |

**Seed method set.** Pearson, Spearman, Kendall, point-biserial, chi-square with Fisher exact fallback, Cramer V, ANOVA, Welch ANOVA, Kruskal-Wallis, Mann-Whitney, Cliff delta, logistic association, Theil-Sen trend, Mann-Kendall, cross-correlation with lag, and a distribution-shift test. **The registry ships with these and accepts more without a code change.**

### 21.3 The execution contract, in fixed order

```
1  candidate pair enumeration      (x_code, y_code, position, grain)
2  population alignment            join x and y at a common grain via DP-1
3  eligibility check               type pair has an enabled method, min population met
4  assumption testing              run the declared assumption tests
5  method selection                the eligible method whose assumptions hold
6  confounder and strata policy     stratify by declared conditioning set,
                                    excluding COLLAPSED dimensions
7  estimate                        effect size AND significance, never one alone
8  multiple-testing correction     Benjamini-Hochberg FDR across the family,
                                    q-value stored beside p-value
9  stability check                 bootstrap or split-half resampling;
                                    sign and magnitude stability recorded
10 lag scan                        where supports_lag, scan the declared window
11 negative control                the declared control pair must not move
12 terminal state                  FINDING or one of the five refusals
13 evidence row                    written to DP-7 with claim_class = ASSOCIATION
```

**Step 3 is the one that matters most.** When no enabled method exists for a type pair, the terminal state is `NOT_APPLICABLE` with `refusal_reason` naming the missing method and the pair. **It is never reported as a property of the data.** This is the exact defect class already measured once in this product, where a method gap was reported as zero variance in the customer's data. Gate G-19 already exists for it; G-23 below makes it specific to DF9.

### 21.4 Multiple testing is structural, not optional

A plant with 400 parameters and 6 outcomes generates 2,400 candidate pairs before stratification. At p below 0.05 with no correction, 120 false findings are expected from noise alone. **A finding without a q-value is not publishable by this engine.** The FDR family is defined as all pairs tested within one run for one outcome; the family definition is recorded on every finding so the correction can be reproduced.

### 21.5 Output extension to the finding dataset

Section 10.6 gains: `x_code`, `y_code`, `x_type`, `y_type`, `method_code`, `assumption_results`, `effect_size_measure`, `effect_size_value`, `p_value`, `q_value`, `fdr_family_id`, `fdr_family_size`, `stability_score`, `lag_seconds`, `strata_count`, `negative_control_result`.

Measures follow the aggregation policy of section 10: effect sizes and p-values are `non_additive`. **A dashboard cannot average p-values, and the schema is what stops it.**

### 21.6 The DF9 versus MF-04 boundary rule (finding F3)

| Question shape | Owner | Claim class |
|---|---|---|
| Is X related to Y in the population | **DF9** | ASSOCIATION |
| How much does X contribute to this specific prediction | **MF-04 plus SHAP** | PREDICTIVE_CONTRIBUTION |
| Does changing X change Y | **MF-05** | MATCHED_EFFECT_ESTIMATE or higher |

When both DF9 and MF-04 have something to say about the same pair, the Assistant returns **both**, labelled, and never reconciles them into one number. A population association and a model contribution disagreeing is information, not a defect, and the most common cause is a confounder that the model absorbed and the bivariate test did not.

### 21.7 Registry entry

| Attribute | Value |
|---|---|
| Eligible inputs | DP-1, DP-2, DP-4. Runs without sequences, without embeddings, without labels beyond the outcome itself |
| Minimum population | Per method, from the registry |
| Refresh | Weekly, full recompute. Cheap relative to training |
| Compute class | CPU, minutes to a few hours depending on pair count |
| Refusal states | All six |
| Emitted datasets | Finding dataset |

**DF9 is the engine that a level L0 or L1 customer lives on.** It requires no encoder, no labels beyond a declared outcome, and no genealogy. It should be the first thing built and the first thing demonstrated.

---

### LB-A22. PRACTICE LEARNING ENGINE (DF12) - MF-07 AND DP-16

**Scope boundary.** MF-07 produces practice signatures and matches. MF-05 consumes them for effect estimation and envelopes; it does not derive practices.

### 22.1 What a practice is, as a contract

A practice is the combination of operating parameter values, the operation sequence, and the context, over a defined production window. It is not a single parameter setting, which is why an envelope is not a practice.

### 22.2 DP-16 `practice_signature`

> **BOUND TO `ppiq_plant.practice_signatures`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `signature_id` | uuid | PK |
| `signature_hash` | text | **Canonical hash over the ordered, binned, normalised component set** |
| `signature_version` | int | Increments when the binning or component policy changes |
| `grain_code` / `spine_node_id` | | Where the practice was observed |
| `window_kind` | enum | process_based, time_based, campaign_based |
| `window_start_utc` / `window_end_utc` | timestamptz | |
| `components` | jsonb | Ordered array of `{parameter_code, aggregation, binned_value, bin_edges, unit_code}` |
| `operation_sequence` | text[] | Ordered event or position codes |
| `context_keys` | jsonb | Variant, crew, campaign, ambient, excluding COLLAPSED dimensions |
| `support_count` | bigint | Occurrences of this exact signature |
| `first_seen_utc` / `last_seen_utc` | timestamptz | |
| `is_active` | bool | Seen within the drift window |

**Canonicalisation rules, which are the whole difficulty of this product:**

1. Numeric components are binned by a **declared tolerance**, not by a learned clustering. Tolerance comes from SM-05 `valid_range` and a declared relative tolerance per parameter. Learned binning would make signatures unstable between weeks.
2. Components are ordered by `parameter_code` for hashing, so component ordering cannot change a hash.
3. Categorical components use the declared level code, never a display label.
4. The operation sequence is included in the hash only when the position graph permits ordering variation; on a fixed chain it is constant and adds nothing.
5. **The hash is over `signature_version` plus components plus sequence plus context.** A binning policy change produces a new version and does not silently merge or split historical signatures.

### 22.3 DP-17 `practice_match`

> **BOUND TO `ppiq_plant.practice_statistics`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

The exact-and-relaxed matching contract. One row per (subject, signature, similarity level).

| Field | Type | Notes |
|---|---|---|
| `match_id` | uuid | PK |
| `subject_spine_node_id` | uuid | |
| `signature_id` | uuid | |
| `similarity_level` | enum | **exact, relaxed_1, relaxed_2, relaxed_3** |
| `exact_support_count` | bigint | Population matching on all dimensions |
| `relaxed_support_count` | bigint | Population after back-off |
| `relaxed_dimensions` | text[] | **Which dimensions were dropped to reach support** |
| `backoff_rule_code` | text | The declared rule that governed the back-off |
| `sensitivity_state` | enum | **stable, sensitive, unstable** |
| `sensitivity_detail` | jsonb | Which dropped dimension changed the outcome estimate and by how much |
| `outcome_rate` | numeric | Observed outcome rate in the matched population |

**The back-off rule.** When exact support falls below the declared floor, dimensions are dropped in a **declared priority order**, never a discovered one. Each drop is recorded. Support is recomputed. Back-off stops at the first level meeting the floor, or exhausts and returns `INSUFFICIENT_DATA`.

**The sensitivity state is the honesty mechanism.** After back-off, the estimate is recomputed holding each dropped dimension fixed in turn. If the estimate moves more than the declared tolerance, the state is `sensitive` and the finding carries that word. If it moves more than the estimate itself, the state is `unstable` and **the finding is not emitted as a practice recommendation at all**. A relaxed match that is silently presented as an exact one is the single most misleading output this subsystem could produce.

### 22.4 MF-07 registry entry

| Attribute | Value |
|---|---|
| Purpose | Learn, canonicalise, match, rank and monitor operating practices |
| Eligible inputs | DP-1, DP-2 controllable features, DP-7 events, DP-4 outcomes, SM-08 context |
| Eligibility | At least 30 distinct signatures with support at or above floor; at least one controllable parameter; at least one outcome or productivity measure |
| Outputs | DP-16, DP-17, plus best-practice and failure-associated-practice findings |
| Ranking | Practices ranked by outcome rate **within matched context**, with support and sensitivity shown. **Never ranked by outcome rate alone across contexts** |
| Failure linkage | A practice is failure-associated when its outcome rate exceeds the matched-cohort baseline with the DF9 significance and FDR discipline applied |
| Drift | A signature whose outcome rate shifts beyond tolerance between windows raises a practice-drift finding |
| Benchmarking | Within tenant and site only. Cross-tenant benchmarking is prohibited by section 27 of the frozen rule |
| Refresh | Weekly. Signature version changes are a governed refresh, not weekly |
| Refusal states | All six, plus `INSUFFICIENT_DATA` when back-off exhausts |

### 22.5 What MF-05 keeps

MF-05 remains the effect and envelope layer. It now takes `practice_signature` and `practice_match` as inputs. Envelopes remain a per-parameter product; practices remain a combination product. **They answer different questions and both are needed:** an envelope says where one parameter should sit, a practice says which combination actually worked here.

---

### LB-A23. OPERATIONAL PREDICTION AND EARLY WARNING (DF13a)

**This section specifies delivery while action is still possible.** Production and storage of predictions are section 5 DP-6 and section 9.

### 23.1 DP-18 `prediction_current`

> **BOUND TO `ppiq_plant.prediction_current`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

One row per (grain instance, outcome), holding the live operational state. This is the table the operator surface and the alert interface read.

| Field | Type | Notes |
|---|---|---|
| `grain_instance_key` | text | PK part |
| `outcome_code` | text | PK part |
| `prediction_id` | uuid | FK to DP-6, the current prediction |
| `predicted_probability` / `predicted_value` | numeric | |
| `risk_band` | text | |
| `current_position_code` | text | Where the instance is now |
| **`last_actionable_position_code`** | text | **The last position at which a remediation exists** |
| **`actionable_deadline_utc`** | timestamptz | **When action stops being possible** |
| **`time_remaining_seconds`** | numeric | Derived, refreshed on read |
| **`actionability_state`** | enum | **actionable, closing, expired, not_applicable** |
| `scoring_mode` | enum | event, micro_batch, scheduled |
| `delivery_latency_ms` | int | From triggering data arrival to row write |
| `model_state` | enum | primary, fallback, unavailable |
| `fallback_reason` | text | Nullable |
| `queue_state` | enum | scored, queued, deferred, dropped |
| `stage_already_passed` | bool | True when the prediction arrived after its own prediction point |
| `scored_at_utc` / `valid_until_utc` | timestamptz | |

### 23.2 How the deadline is computed, generically

```
last_actionable_position = the latest position P in the instance's route where
    a remediation candidate exists whose controllable parameter is
    authored at P, and P precedes the outcome's detection_position

actionable_deadline_utc = predicted arrival time at last_actionable_position
    computed from SM-03 residence models along the remaining route

actionability_state =
    expired          when now > deadline
    closing          when time_remaining < declared_warning_fraction of residence
    actionable       otherwise
    not_applicable   when no remediation candidate exists at any remaining position
```

**Nothing in that computation names an industry.** It uses the position graph, the residence models and the controllability flags, all of which are declared.

### 23.3 Scoring modes

| Mode | Trigger | Latency target | When chosen |
|---|---|---|---|
| `event` | Arrival of the data completing a prediction point | seconds | Short residence, tight deadline |
| `micro_batch` | Fixed interval, typically 1 to 15 minutes | interval plus seconds | Moderate residence, high volume |
| `scheduled` | Weekly or daily batch | hours | No actionable window remains; the prediction is analytical, not operational |

Mode is **declared per (outcome, prediction point)** and constrained by the deadline: a prediction point whose remaining residence is shorter than the micro-batch interval may not use micro-batch mode. Gate G-26 asserts this.

### 23.4 Primary and fallback

When the active primary is unavailable, in `review` or `retired`, scoring falls back to the **explicitly approved fallback** for that serving identity, the version carrying `serving_role = 'serving_fallback'`. **A fallback is never inferred from the last active version**, and the six conditions it must satisfy are Ch4 5.6.7a. Use is recorded on `prediction_runs.fallback_model_registry_id` and `fallback_reason` and surfaced through `prediction_current.fallback_in_use`. **Silently serving a fallback as the primary is prohibited**; gate G-27 asserts the fields are populated and surfaced.

### 23.5 The serving-wall consequence

Event and micro-batch scoring run in the serving plane using the active model version. They are inference, not training, and they respect every constraint of section 9.5. **This is the only serving-plane component permitted to write to a data product**, and its write is confined to DP-6 and DP-18.

---

### LB-A24. REMEDIATION SAFETY ARCHITECTURE (DF13b)

**Canonical: Ch4 5.6.4d for the gate, Ch3 4.5.12 and 4.5.12a for persistence and acceptance, Ch3 4.5.12b for escalation.**

### 24.1 Two tables, deliberately separated

A remediation candidate is a **historical template**: a practice difference some population of comparable units benefited from. Whether it is **actionable** is a property of one specific prediction at one moment, because the same template is actionable for a unit two stages away and not for a unit that has passed the stage. **Storing eligibility on the template would be wrong.**

| Object | Role |
|---|---|
| **`ppiq_plant.remediation_candidates`** | The **global historical template**, computed once per condition and reused across predictions. CHECK `support_count >= 20`; CHECK `source_practice_sensitivity_state <> 'unstable'`. A template below support or from an unstable practice is never created, and the insufficiency is reported from the run record instead |
| **`ppiq_plant.prediction_remediation_evaluations`** | The **per-prediction nine-check evaluation**, produced at scoring time and re-evaluated whenever the unit's stage position changes. UNIQUE `(prediction_id, remediation_candidate_id)` |

**Five checks are properties of history** and are evaluated at template generation: support, stratification survival, uncertainty, uplift and source-practice sensitivity. **Four are situational and cannot be**, because they depend on where the unit is now.

### 24.2 The nine checks

| # | Check | Passes when | Refusal |
|---|---|---|---|
| 1 | **Controllability** | Every parameter the candidate would change is declared controllable at the proposed stage in `registry_dimensions.is_controllable` / `controllable_at_stages` / `adjustment_range` | `RM01`, naming the parameter |
| 2 | **Remaining actionable stage** | The proposed stage is still ahead of the unit on its declared route, with a declared minimum lead time | `RM02` |
| 3 | **Operating and specification limits** | Proposed values sit inside `operating_limits` and `product_specifications` for that unit's specification | `RM03`, naming the limit |
| 4 | **Forbidden combinations and safety** | The proposed combination violates no rule in `ppiq_plant.forbidden_combinations` | `RM04`, naming the rule |
| 5 | **Historical support** | Support at or above the floor, at the disclosed similarity level | `RM05`, count shown |
| 6 | **Contextual and confounder survival** | The association survives stratification by the declared confounders in the unit's own context | `RM06`, naming the stratum |
| 7 | **Uncertainty** | The expected-effect interval excludes no-effect, or the candidate is explicitly exploratory | `RM07` |
| 8 | **Causal or uplift evidence where data permits** | An uplift estimate over comparable non-adopters supports the effect; where variation is insufficient the candidate is marked `association_only` | `RM08` |
| 9 | **Sensitivity** | The source practice is `sensitivity_state = 'stable'` | `RM09` |

### 24.3 The four outcomes - canonical mapping

| Outcome | Condition | Presentation |
|---|---|---|
| **`actionable`** | **All nine pass** | A remediation card with the proposed practice, support, expected-effect interval, evidence and limitations. The only outcome styled as a recommendation |
| **`evidence_only`** | **Checks 5 to 9 pass, but one or more of checks 1 to 4 fail for this unit** | Shown in drill-down as an observed historical difference that is not actionable here, with the failing check named. Never styled as a recommendation, no decision control |
| **`exploratory`** | **Checks 1 to 6 pass, check 7 or 8 fails** | Shown behind an explicit exploratory disclosure with the uncertainty and the failed check stated. No accept action at any tier for any role |
| **`suppressed`** | **Check 4 fails on a safety constraint** | Not shown at all in the card list; recorded on the run with `RM04` so the suppression is auditable rather than invisible |

CHECK `eligibility_state <> 'actionable' OR failed_checks IS NULL OR jsonb_array_length(failed_checks) = 0` makes an actionable candidate with a failed check a database-level impossibility.

### 24.4 `can_accept` is NOT equal to actionable

**`can_accept` is the complete seven-condition server-side acceptance authority** (Ch3 4.5.12a). It is false unless every one of these holds:

| # | Condition |
|---|---|
| 1 | `eligibility_state = 'actionable'` - all nine checks passed for this prediction |
| 2 | `remaining_stage_state` is `ahead` or `imminent` - the proposed stage has not passed |
| 3 | The prediction's `actionable_deadline_utc` has not elapsed |
| 4 | The prediction is still open: not already decided, not superseded by a newer scoring run |
| 5 | No safety constraint has become invalidating since the evaluation - re-checked on read |
| 6 | The model that produced the prediction is not in `review` or `retired` |
| 7 | The tenant's entitlement and the caller's role permit a remediation decision |

**The client reads `can_accept` and nothing else.** It renders the Accept affordance from that single boolean and uses `can_accept_blockers` only to explain an absent affordance. **A client that additionally tests the deadline, the stage or the eligibility state has created a second authorisation rule, and the two will eventually disagree.** The server enforces the same boundary on the write path: a decision on an evaluation whose `can_accept` is false is refused with `RM10`, whatever the client believed.

CHECK `can_accept = false OR eligibility_state = 'actionable'`.

### 24.5 The decision boundary is wider than Accept

**Accept, Reject and Defer all exist only where `can_accept` is true.** `evidence_only`, `exploratory` and `suppressed` candidates carry no decision control of any kind, at any tier, for any role. They are not merely un-acceptable: they are **outside the decision record entirely**, because rejecting or deferring an observation would enter it into the effectiveness and feedback statistics as though it had been offered as a recommendation, corrupting exactly the measurements the product exists to produce.

### 24.6 Escalation is a record, never a decision

**`ppiq_plant.remediation_escalations`** carries an `evidence_only` or `exploratory` candidate to engineering investigation through `POST /api/predictions/{id}/escalate`. `actionable` can never be escalated because it can be decided; `suppressed` can never be escalated because it is not shown.

It records `failed_checks_at_escalation` frozen at escalation time, a required reason, the actor, and a resolution from `no_action`, `definition_changed`, `limit_changed`, `controllability_registered`, `data_gap_raised`, `promoted_to_actionable`, `withdrawn`. Partial UNIQUE `(tenant_id, prediction_id, remediation_candidate_id)` WHERE `resolved_at_utc IS NULL` gives at most one open escalation per pair and is also the idempotency rule.

**It creates no `prediction_actions` row, contributes to no `remediation_effectiveness` row, and is excluded from `feedback_records`.** An escalation says an engineer should look at this, not that we decided something. `promoted_to_actionable` is the one resolution that changes product behaviour and is audited as a governed change, not a data edit.

### LB-A25. DECISION, OUTCOME, EFFECTIVENESS AND FEEDBACK (DF14)

The industrial loop, which Part One did not contain.

```
prediction -> recommendation -> HUMAN DECISION -> action -> actual outcome
   -> prediction evaluation -> remediation effectiveness -> feedback record
   -> governed supervisor review -> (never automatic retraining)
```

### 25.1 DP-20 `decision_record`

> **BOUND TO `ppiq_plant.prediction_actions and suggestion_decisions`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `decision_id` | uuid | PK |
| `prediction_id` / `remediation_evaluation_id` | uuid | What was decided on |
| `decision` | enum | **accept, reject, defer** |
| `decided_by` / `decided_at_utc` | | Identity and time |
| `reason_code` / `reason_text` | | **Required on reject and defer** |
| `defer_until_utc` | timestamptz | Required on defer |
| `assigned_to` / `assigned_at_utc` | | Nullable, set on accept |
| `can_accept_at_decision_time` | bool | Snapshot, so a later rule change cannot rewrite history |
| `model_registry_id`, `model_version` | | Which model version produced the recommendation |

**Reject reasons are the highest-value data in this table.** An operator rejecting a recommendation because it is operationally impossible is telling the system something no dataset contains, and the reason taxonomy must be declared, not free text alone.

### 25.2 DP-21 `action_record`

> **BOUND TO `ppiq_plant.prediction_actions, same row`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type |
|---|---|
| `action_id` | uuid PK |
| `decision_id` | uuid |
| `performed` | bool |
| `performed_at_utc` | timestamptz |
| `performed_by` | text |
| `actual_parameter_value` | numeric |
| `deviation_from_suggested` | numeric |
| `performed_within_deadline` | bool |
| `notes` | text |

### 25.3 DP-22 `prediction_evaluation`

> **BOUND TO `ppiq_plant.prediction_evaluations`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Written when the actual outcome arrives from canonical data, never entered by hand.

| Field | Type | Notes |
|---|---|---|
| `evaluation_id` | uuid | PK |
| `prediction_id` | uuid | |
| `actual_outcome_row_id` | uuid | FK to DP-4 |
| `predicted_value` / `actual_value` | | As stored, not recomputed |
| `correctness_class` | enum | true_positive, false_positive, true_negative, false_negative, within_tolerance, outside_tolerance |
| `error` / `absolute_error` | numeric | Regression |
| **`intervened`** | bool | **Was an accepted action performed on this instance** |
| `evaluation_state` | enum | evaluable, **not_evaluable_intervened**, pending, censored |

**`not_evaluable_intervened` is the subtle and essential state.** If the system predicted a defect, a human acted, and no defect occurred, the prediction was not wrong. Counting it as a false positive would train the system to stop warning about the problems it successfully prevented. **Intervened instances are excluded from accuracy metrics and reported separately as prevented-event candidates.** Gate G-29 asserts the exclusion.

### 25.4 DP-23 `remediation_effectiveness`

> **BOUND TO `ppiq_plant.remediation_effectiveness`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Post-action measurement, which is not the same thing as the pre-action effect estimate in MF-05.

| Field | Type | Notes |
|---|---|---|
| `effectiveness_id` | uuid | PK |
| `action_id` | uuid | |
| `comparable_cohort_definition` | jsonb | Matched non-intervened instances |
| `cohort_n` / `cohort_outcome_rate` | | |
| `intervened_outcome` | | |
| `effectiveness_estimate` / `lower` / `upper` | numeric | |
| `claim_class` | enum | **MATCHED_EFFECT_ESTIMATE at best.** A single action never yields causal evidence |
| `terminal_state` | enum | Including INSUFFICIENT_DATA when the cohort is too small |

**One action is never an effectiveness measurement.** Effectiveness accumulates across actions on the same recommendation type, and the schema makes the population explicit so a single anecdote cannot be presented as proof.

### 25.5 DP-24 `feedback_record`

> **BOUND TO `ppiq_plant.feedback_records`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

The governed input to the supervisor.

| Field | Type | Notes |
|---|---|---|
| `feedback_id` | uuid | PK |
| `feedback_kind` | enum | prediction_evaluation, effectiveness, rejection_reason, drift_observation, practice_drift |
| `source_record_id` | uuid | |
| `eligibility_state` | enum | **eligible, insufficient, quarantined** |
| `aggregated_into_review_id` | uuid | Nullable |

**Nothing in this table triggers a retrain.** Feedback accumulates into a supervisor review, a human approves an action, and the action executes in a governed window. Section 28 defines that path. The rule is absolute because a feedback loop that retrains automatically is a loop with no human in it, and in a plant that is the wrong kind of loop.

### 25.6 The write-path boundary (finding Q4)

| PPIQ writes to | PPIQ never writes to |
|---|---|
| Its own decision ledger, DP-20 to DP-24 | Any customer source system |
| Its own data products and evidence | Any MES, LIMS, historian or ERP |
| Its own prediction stores | **Any control system, PLC, DCS or setpoint** |

**An accepted recommendation produces a record that a human acted, not an action.** The product remains read-only toward the plant. This paragraph exists so that the accept, reject, defer and assign vocabulary in this section is never mistaken for control.

---

### LB-A26. VALUE ENGINE

### 26.1 SM-14 `CostAssumptionContract`

> **BOUND TO `the cost inputs of Chapter 4 5.6.4 Value attachment, persisted in ppiq_plant.value_impacts.inputs`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Declared by the customer, versioned, and **never inferred**.

| Field | Type | Notes |
|---|---|---|
| `assumption_code` | text | PK |
| `assumption_version` | int | |
| `currency_code` | text | **Per tenant. Never hardcoded** |
| `basis` | enum | per_unit_scrap, per_unit_downgrade, per_hour_downtime, per_unit_rework, per_unit_yield_loss, per_unit_energy, custom |
| `outcome_code` | text | What this cost attaches to |
| `low_value` / `mid_value` / `high_value` | numeric | **A range, always. A single number is not accepted** |
| `confidence` | enum | declared, estimated, benchmark |
| `valid_from` / `valid_to` | date | |
| `declared_by` | text | |

### 26.2 DP-25 `value_impact`

> **BOUND TO `ppiq_plant.value_impacts`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `value_id` | uuid | PK |
| `subject_kind` | enum | finding, prediction, recommendation, practice |
| `subject_id` | uuid | |
| `assumption_code` / `assumption_version` | | |
| `currency_code` | text | |
| `impact_low` / `impact_mid` / `impact_high` | numeric | **Always a bounded range** |
| `basis_population_n` | bigint | |
| `time_basis` | enum | per_event, per_day, per_month, per_year |
| `derivation` | jsonb | **Every input to the arithmetic, for drill-through** |
| `terminal_state` | enum | Including `INSUFFICIENT_BASIS` |

### 26.3 DP-26 `value_realization_ledger`

> **BOUND TO `ppiq_plant.value_realization_ledger`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Predicted benefit against realised benefit, per accepted recommendation.

| Field | Type |
|---|---|
| `ledger_id` | uuid PK |
| `decision_id` | uuid |
| `predicted_impact_low/mid/high` | numeric |
| `realised_impact_low/mid/high` | numeric |
| `realisation_basis` | jsonb |
| `attribution_confidence` | enum: attributed, partially_attributed, unattributable |
| `payback_days` | numeric |
| `realised_at_utc` | timestamptz |

### 26.4 The abstention rules, which matter more than the arithmetic

1. **Bounds are mandatory when the basis is sufficient.** CHECK `basis_status = 'InsufficientBasis' OR (lower_bound IS NOT NULL AND upper_bound IS NOT NULL)`. A `point_estimate` may sit beside the bounds; it may never stand alone.
2. **No value without a declared assumption.** Missing assumption produces `INSUFFICIENT_BASIS`, never an industry default.
3. **No value on an unrealised recommendation** beyond a clearly labelled potential impact.
4. **No aggregation of potential impacts into a total saving.** The measure is `non_additive` by aggregation policy. **Summing potential savings across findings produces the single most dangerous number this product could display**, because it is the number a buyer will repeat and the one that will be tested against reality first.
5. **Attribution is explicit.** Where a benefit cannot be attributed to the accepted action against a comparable cohort, `attribution_confidence = unattributable` and the realised figure is not claimed.

> **Commercial note, stated plainly.** The value engine is the most persuasive output in the product and the most exposed. A defensible bounded range with a stated basis survives a CFO's questions. A confident total does not survive the first quarter in which it fails to appear in the accounts.

---

### LB-A27. SCENARIO SIMULATION

### 27.1 Contract

| Element | Rule |
|---|---|
| Scenario definition | Named, saved, versioned, owned by a user |
| Allowed variables | **Controllable parameters only.** An observed parameter cannot be set in a scenario |
| Baseline | An explicit population or instance, recorded with the scenario |
| Valid ranges | Each variable constrained to SM-05 `valid_range` intersected with **observed support range**. Extrapolation beyond observed practice is refused, not warned |
| Model pinning | An exact `model_registry_id` and `model_version`, recorded on `scenario_runs`. A scenario run against a different model version is a **new** run, never a silent recomputation |
| Output | Predicted outcome with uncertainty interval, plus contributor breakdown, plus the forbidden-combination check from section 24 |
| Comparison | Up to N scenarios side by side against the baseline |
| Export | Permitted |
| **Write path** | **None. A scenario never writes a setpoint, a recommendation, a decision or a plant value.** It is a read-only calculation over a pinned model |

### 27.2 Execution tier

Scenario evaluation is inference on a pinned model over a small input set. It is **tier 2**, target under 30 seconds. A scenario sweep across many combinations exceeds that and is **tier 3**, an asynchronous job. The cost estimator decides by combination count, and the ceiling is the same as everywhere else.

### 27.3 The honesty constraint

A scenario answer is a model prediction under a counterfactual input, which is a **weaker** claim than a matched effect estimate, because nothing was matched and nothing was observed. Its `claim_class` is `PREDICTIVE_CONTRIBUTION`, never `MATCHED_EFFECT_ESTIMATE`. **Scenario output must not be phrased as what will happen. It is what the model predicts under these inputs, given this training window.**

---

### LB-A28. THE FULL ENGINE SUPERVISOR

**Component 17 of section 2.** Monitoring is one of its six functions, not the whole of it.

### 28.1 The six functions

| # | Function | Description |
|---|---|---|
| 1 | **Observe** | Runs, gates, drift metrics, prediction evaluations, effectiveness, rejection reasons, practice drift |
| 2 | **Propose** | A **bounded** adjustment: threshold, calibration, refresh trigger, eligibility parameter, practice tolerance, model retirement |
| 3 | **Shadow** | Execute the proposal in a dry run against held-out history. Nothing published |
| 4 | **Compare** | Candidate against current on the same governed holdout, using the same metrics as champion-challenger |
| 5 | **Approve** | **Human approval required.** No proposal applies without it |
| 6 | **Apply** | Atomic, versioned, with provenance and a rollback pointer |

### 28.2 The prohibited set, which is the design's safety margin

The supervisor may **never** modify: readiness thresholds that would make an ineligible method eligible, refusal rules, evidence requirements, leakage gates, tenant isolation, the semantic model, or the forbidden-combination set.

**The reason is one sentence.** A component whose job is to improve results, and which can also lower the bar for what counts as a result, will eventually improve results by lowering the bar. The prohibition is what makes the compounding claim honest rather than self-fulfilling.

### 28.3 DP-27 `supervisor_proposal`

> **BOUND TO `ppiq_meta.supervisor_proposals, supervisor_shadow_runs, supervisor_provenance`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type |
|---|---|
| `proposal_id` | uuid PK |
| `trigger_kind` | enum: drift, performance, feedback, effectiveness, schedule |
| `evidence_ids` | uuid[] |
| `proposed_change` | jsonb |
| `shadow_run_ref` | text |
| `holdout_comparison` | jsonb |
| `state` | enum: proposed, shadowed, approved, rejected, applied, rolled_back |
| `approved_by` / `approved_at_utc` | |
| `applied_model_version` | text |
| `rollback_pointer` | text |

### 28.4 Abstention

When the evidence does not support a change, the supervisor **records that it considered and abstained**, with the reason. A supervisor that only speaks when it acts leaves no evidence that it was working during a quiet quarter.

---

### LB-A29. MODALITY EXTENSION CONTRACT AND EXTERNAL INTERFACES

### 29.1 Position, stated honestly

Text evidence and inspection-image intelligence are **not designed in this pack** (finding Q2). What is designed is the contract by which any new modality enters Layer B without a redesign. Designing vision inside a convergence pass would produce something that looks complete and is not.

### 29.2 The extension contract

A new modality is admissible when it supplies:

1. A semantic declaration under SM-11 with `role_kind = extension`, naming its subject grain and its timestamp
2. A **store analogous to DP-3**: subject key, content reference, a `modality_set_version`, a mask or completeness measure, and a content hash
3. An **encoder** registered as an MF family with its own version, eligibility, refusal states and compute class
4. Output that respects the governed-model boundary of Ch4 5.8.6 as amended: **no free-form or model-generated output may become a feature, a score, a statistic or a value.** A modality enters a learned result **only** through Path B below
5. Full participation in the evidence envelope, the claim classes and the terminal states

**Two paths, and the boundary is governance rather than modality.**

**Path A, evidence modality.** Retrieved and cited. Corroborates a deterministic result, never originates one. No feature, no score, no plant fact the model invented.

**Path B, governed multimodal ML.** An explicitly authored model definition, a versioned immutable training snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring, and a learned output carrying a claim class and provenance. **This is how an inspection-image model produces an annotation with a confidence**, which Ch4 5.8.7 requires and the previous absolute wording forbade.

**No implementation scope is added by this distinction.** Both modalities remain interface-designed, future implementation.

**No modality may bypass the leakage gate.** An inspection image carries a capture timestamp and a capture position, and G-06 applies to it exactly as to a numeric feature. An image captured after the prediction point is illegal input regardless of how informative it is.

### 29.3 Named candidates, deferred

| Modality | Canonical persistence and access | Authority | State |
|---|---|---|---|
| **Text evidence** | `ppiq_plant.text_documents` and `text_passages`; full-text plus the embedding path; passages become `assistant_chunks` with `chunk_family = 'DOC'`; `role_scope` per passage, so a passage can be more restricted than the row it describes | Ch4 5.8.6 | **INTERFACE-DESIGNED, future implementation** |
| **Inspection images** | `ppiq_plant.inspection_images` with `storage_uri` only, never database blobs; `image_annotations` with region, confidence and the model version; vision models register in `model_registry` under the same activation, retirement and drift rules; signed time-limited URLs, every access audited | Ch4 5.8.7 | **INTERFACE-DESIGNED, future implementation** |

### 29.4 External interface references

These functions exist in the product and are **not owned by Layer B**. Layer B publishes to them and does not implement them.

| Function | Owner | Layer B obligation |
|---|---|---|
| Alert routing and escalation | Notification and workflow subsystem | Publish DP-18 rows with `actionability_state` and deadline. **Layer B does not decide who is notified** |
| Assistant page context, permission-scoped retrieval, glossary and synonym resolution, no-fabrication guard, egress policy | **DF15 Assistant architecture** | Layer B owns only the eleven intelligence tool contracts of section 11 and their evidence envelope |
| User identity, roles and permissions | Platform | Layer B receives a scoped principal and filters by tenant and site |
| Job scheduling infrastructure | Platform scheduler | Layer B declares stages, dependencies and budgets |

**This table exists to prevent both gaps and duplication.** Anything in it is somebody's, and it is not Layer B's.

---

### LB-A30. REVISED CROSS-CONTRACTS AND ADDITIONAL GATES

### 30.1 New and revised interfaces

| Interface | Contract |
|---|---|
| **DF9 <-> MF-04** | Section 21.6 boundary. Both may speak about the same pair; neither reconciles the other; the Assistant returns both labelled |
| **MF-07 <-> MF-05** | Practice engine produces signatures and matches; effect layer consumes them. MF-05 no longer derives practices |
| **DP-18 <-> alert routing** | Layer B publishes state and deadline; the notification subsystem decides recipients |
| **DP-19 <-> decision surface** | `can_accept` is the only field the surface reads. No component re-derives eligibility |
| **DP-22 <-> model metrics** | Intervened instances are excluded from accuracy and reported separately |
| **DP-24 <-> supervisor** | Feedback accumulates into proposals; **nothing retrains automatically** |
| **Value engine <-> Page Builder** | Impact measures are `non_additive`; the aggregate engine refuses to sum them |
| **Scenario <-> model registry** | A scenario pins an exact `model_registry_id` and `model_version`; a version change makes a new run, never a silent recomputation |
| **Decision ledger <-> customer systems** | One-way and internal. PPIQ writes only to its own ledger, never to a plant system |

### 30.2 Additional gates G-23 to G-35

| ID | Gate | When | Blocking |
|---|---|---|---|
| **G-23** | DF9 method-gap integrity: every `NOT_APPLICABLE` names the missing type pair and method, never a data property | DF9 run | **Yes** |
| **G-24** | Multiple-testing correction applied; every finding carries `q_value` and a reproducible `fdr_family_id` | DF9 run | **Yes** |
| **G-25** | Practice signature stability: a hash recomputed from the same inputs under the same `signature_version` is identical | Practice run | **Yes** |
| **G-26** | Scoring mode feasibility: micro-batch interval is shorter than the remaining residence at the prediction point | Activation | **Yes** |
| **G-27** | Fallback transparency: `model_state` populated on every prediction and surfaced downstream | Serving, continuous | **Yes** |
| **G-28** | Remediation completeness: all nine checks evaluated and recorded, including passes | Recommendation emit | **Yes** |
| **G-29** | Intervened exclusion: no intervened instance contributes to an accuracy metric | Evaluation run | **Yes** |
| **G-30** | Value abstention: no point estimate, no value without a declared assumption version, no summed potential impact | Value run | **Yes** |
| **G-31** | Scenario containment: no scenario writes any store other than its own saved definition and result | Serving, continuous | **Yes** |
| **G-32** | Supervisor prohibition: no applied proposal touches readiness, refusal, evidence, leakage, isolation, semantic model or forbidden combinations | Proposal apply | **Yes** |
| **G-33** | Human approval present on every applied supervisor proposal | Proposal apply | **Yes** |
| **G-34** | Sensitivity honesty: no `unstable` practice match is emitted as a recommendation; every `sensitive` one carries the state | Practice run | **Yes** |
| **G-35** | Control-path absence: no Layer B component holds a write path to any customer or control system | Activation, continuous | **Yes** |

Every one is falsified once before it is trusted, per section 15.

---

### LB-A31. MASTER DESIGN CAPABILITY TRACEABILITY MATRIX

Replaces the audit's percentage estimates with a per-capability state that can be checked.

**States.** `COVERED` design complete here. `COVERED-EXT` design complete, extends a Part One section. `RESERVED` contract defined, detailed design deliberately deferred. `EXTERNAL` owned by another subsystem, interface defined here. `OPEN` needs a ruling.

| # | Capability | State | Section |
|---|---|---|---|
| 1 | Generic across any industry | COVERED | 2, 13 |
| 2 | Semantic model instead of physical tables | COVERED | 4 |
| 3 | Cross-stage linking and genealogy | COVERED | 4 SM-04, 5 DP-1 |
| 4 | Continuous-flow plants, residence model | COVERED | 4 SM-03 |
| 5 | Weighted transformational genealogy | COVERED | 5 DP-1 |
| 6 | Feature engineering and historical feature state | COVERED | 5 DP-2 |
| 7 | Time-series sequence processing | COVERED | 5 DP-3 |
| 8 | Computational fingerprint: embeddings, regimes, similarity | COVERED | 6 MF-01, MF-02, MF-03 |
| 9 | **Governed plant fingerprint: semantic model, genealogy, features, outcomes, practices, models, decisions, prediction outcomes, drift, feedback** | **COVERED-EXT** | **31.1 below** |
| 10 | Historical similarity | COVERED | 6 MF-02 |
| 11 | Normal and abnormal regime learning | COVERED | 6 MF-03 |
| 12 | Supervised prediction | COVERED | 6 MF-04 |
| 13 | Prediction explainability and contributors | COVERED | 6, 10.2 |
| 14 | **Statistics and correlation engine** | **COVERED-EXT** | **21** |
| 15 | **Practice signature engine** | **COVERED-EXT** | **22** |
| 16 | **Exact and relaxed practice matching, back-off, sensitivity** | **COVERED-EXT** | **22.3** |
| 17 | **Failure-practice linkage and best-practice ranking** | **COVERED-EXT** | **22.4** |
| 18 | **Operational prediction queue and current-state contract** | **COVERED-EXT** | **23.1** |
| 19 | **Actionable prediction deadline** | **COVERED-EXT** | **23.2** |
| 20 | **Near-real-time, event and micro-batch scoring** | **COVERED-EXT** | **23.3** |
| 21 | **Primary and fallback model state** | **COVERED-EXT** | **23.4** |
| 22 | **Nine-check remediation safety gate** | **COVERED-EXT** | **24** |
| 23 | **Accept, reject, defer** | **COVERED-EXT** | **25.1** |
| 24 | **Action assignment and performance recording** | **COVERED-EXT** | **25.2** |
| 25 | **Actual outcome arrival and evaluation lifecycle** | **COVERED-EXT** | **25.3** |
| 26 | **Prediction correctness evaluation** | **COVERED-EXT** | **25.3** |
| 27 | **Remediation effectiveness measurement** | **COVERED-EXT** | **25.4** |
| 28 | **Governed feedback loop** | **COVERED-EXT** | **25.5** |
| 29 | **Value and ROI impact engine** | **COVERED-EXT** | **26** |
| 30 | **Scenario and what-if simulation** | **COVERED-EXT** | **27** |
| 31 | **Full supervisor: shadow, holdout, approval** | **COVERED-EXT** | **28** |
| 32 | Model registry, versioning, rollback | COVERED | 17 |
| 33 | Initial heavy training | COVERED | 7 |
| 34 | Weekly governed retraining | COVERED | 8 |
| 35 | Daytime pre-trained serving under 2 minutes | COVERED | 9 |
| 36 | Drift detection | COVERED | 15 G-11, 28 |
| 37 | Capability, readiness, refusal | COVERED | 4.1, 10.7, 13 of the rule |
| 38 | Evidence and provenance | COVERED | 5 DP-7 |
| 39 | Intelligence into ordinary charts and widgets | COVERED | 10, 12 |
| 40 | Intelligence into the Assistant | COVERED | 11 |
| 41 | Tenant isolation | COVERED | 15 G-21 |
| 42 | No automatic plant control | COVERED-EXT | 25.6, G-35 |
| 43 | Large-data architecture | COVERED, benchmark pending | 14, OD-11 |
| 44 | Benchmarking | COVERED-EXT, within tenant only | 22.4 |
| 45 | **Text evidence** | **RESERVED** | **29.2, 29.3** |
| 46 | **Inspection-image intelligence** | **RESERVED** | **29.2, 29.3** |
| 47 | **Alert routing and escalation** | **EXTERNAL** | **29.4** |
| 48 | **DF15 Assistant page context, permissions, glossary, egress** | **EXTERNAL** | **29.4** |
| 49 | Scope authority between the Layer B rule and the Master Design chapters | **COVERED** | Rule Appendix A; section 18.2 |

### 31.1 The two fingerprints, reconciled

The audit is right that the pack narrowed the fingerprint to its computational form. Both definitions are now explicit and neither replaces the other.

**Computational fingerprint** - what the models learn: embeddings, learned regimes, similarity structure, model parameters. Owned by MF-01, MF-02, MF-03.

**Governed plant fingerprint** - what the installation accumulates: the semantic model and its versions, the genealogy graph, historical features and outcomes, practice signatures, model versions and their lineage, decisions with their reasons, prediction evaluations, effectiveness records, drift observations and feedback.

> **The commercial sentence follows from the second, not the first.** What makes the system the in-house expert is not the embedding. It is that a new engineer arriving in year three can read what was declared, what was learned, what was recommended, what was accepted, what was rejected and why, what actually happened, and what the system concluded from the difference. **No competitor's model file contains that, because it is not a model, it is an institutional memory.** It is also, not coincidentally, the hardest thing for a customer to leave behind.

---

### LB-A32. REVISED OPEN DECISIONS, SEQUENCING, AND THE COMPLETION TEST

### 32.1 Decision state

All architectural decisions are closed. The items below are **measured parameters**, each with a canonical field to be written into once measured. They are listed here for the benefit of the work packages that consume them.

| ID | Parameter | Canonical home |
|---|---|---|
| OD-05, OD-06 | Encoder and supervised eligibility thresholds | `model_details.acceptance_floor`; the gate minimums of Ch4 5.6.3 |
| OD-11 | Hardware sizing | The capacity model of Ch4 5.3.3 |
| OD-12, OD-21 | Sequence and snapshot retention | `feature_snapshots.retention_until_utc`; per-stage retention policy |
| OD-25 | Reserved interactive capacity fraction | The `interactive` reservation of Ch4 5.3.2 |
| OD-04 | Index rebuild trigger | `index_generation` policy |
| OD-07 | Model budget as count, compute time, or both | The `compute_weight` calibration of Ch4 5.3.2 |

### 32.2 Dependency order (finding F2)

The eight gaps are not a flat list. This is the order in which they can be built without rework.

```
TIER 1  no dependencies, build first
  21 Statistical engine         (needs only spine, features, outcomes)
  22 Practice engine            (needs spine, features, events)

TIER 2  depend on tier 1 and on Part One models
  23 Operational prediction     (needs MF-04, residence models)
  24 Remediation safety         (needs 22 sensitivity, MF-05 effect, SM-13)

TIER 3  depend on tier 2
  25 Decision and feedback loop (needs 23 and 24 to have something to decide on)

TIER 4  depend on tier 3
  26 Value engine               (needs 25 for realised value)
  28 Full supervisor            (needs 25 feedback records as input)

TIER 5  independent, deferrable without rework
  27 Scenario simulation        (needs only MF-04 and a pinned model version)
  29 Modality extensions        (contract only; design deferred)
```

**The consequence for backlog decomposition.** Tiers 1 and 2 can be decomposed and started now. Tier 5 can be contract-reserved indefinitely. Only tiers 3 and 4 must wait, and they wait on implementation of the tiers below them rather than on any decision.

**DF9, the statistical engine, should be first.** It has no dependencies, it is the engine a low-maturity customer lives on, it needs no encoder and no labels beyond a declared outcome, and it is the most demonstrable capability in the product without a completed training run.

### 32.3 Decomposition readiness

Every capability in Part Two states a behaviour and binds to a canonical persistence object in section 43. An implementation lead reads the behaviour here and the table there, and writes tasks against canonical columns.

The dependency order in 32.2 governs sequencing. Tiers 1 and 2 can start immediately. Tier 5 is contract-reserved. Tiers 3 and 4 wait on implementation of the tiers below them, not on any decision.

---
---

# PART THREE - PLATFORM INTEGRATION PASS

**Six ruled requirements, added for the source-reconciliation and freeze pass.**

---

### LB-A34. CANONICAL PLANT DATA TO VERSIONED INTELLIGENCE INPUT

### 34.1 The governed flow, stated as a boundary chain

```
Customer Sources
  -> Transformation / Mapping                     (authoring plane)
  -> Published definition_versions                (immutable)
  -> Governed registry and configuration state
  -> Published transformation, which EMITS the plant
     relationship model. Pinned by source_definition_id
     + source_definition_version. No separate publication act
     and no independent relationship-version object.
  -> CANONICAL PLANT DATA                         <-- INPUT BOUNDARY, mutable
  ================= SNAPSHOT SEAL =================
  -> Versioned Spine / Feature / Sequence / Outcome SNAPSHOTS  (immutable)
  -> Intelligence Engines                         (training)

  and separately:

  CANONICAL PLANT DATA
  -> Prepared Serving Features + Evidence + Live model registry entry   (serving)

  and back:

  Intelligence Outputs
  -> Governed analytical datasets in Plant Data
  -> Layer A / Page Builder / Assistant
```

### 34.2 The distinction, stated so it cannot be blurred

| | Canonical Plant Data | Versioned data products |
|---|---|---|
| Role | **Source of truth for customer data** | **The direct model-training contract** |
| Mutability | Mutable. Corrected, backfilled, extended continuously | **Immutable once sealed** |
| Who reads it | Data Product Builder, Layer A, serving feature preparation | Trainers only |
| Version identity | None. It is a live state | `snapshot_id` plus content hash |
| May a model bind to it | **No** | Yes, and only this |

**Why this is not pedantry.** A model trained directly against a mutable table cannot be reproduced, because the table it was trained on no longer exists. Every claim about why an answer changed between two Mondays depends on this seal.

### 34.3 DP-28 `dataset_snapshot`

> **BOUND TO `ppiq_plant.feature_snapshots and feature_snapshot_rows`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `snapshot_id` | uuid | PK |
| `snapshot_kind` | enum | spine, feature, sequence, outcome, practice, event |
| `semantic_manifest_id` | uuid NULL FK | Pinned. Required for every new governed execution |
| `source_definition_id`, `source_definition_version` | uuid, integer | The transformation publication that emitted the relationship model. **Not a relationship-version object** |
| `feature_set_version` | int | Feature snapshots |
| `window_from_utc` / `window_to_utc` | timestamptz | The population window |
| `scope_filter` | jsonb | Tenant, site, and any declared population restriction |
| `row_count` | bigint | |
| `content_hash` | text | Over the sealed content |
| `storage_ref` | text | Physical location, replaceable |
| `sealed_at_utc` | timestamptz | **After sealing, the snapshot is read-only forever** |
| `built_from_canonical_watermark` | timestamptz | The canonical state it captured |
| `retention_class` | enum | permanent, rolling, sample_retained |
| `superseded_by_snapshot_id` | uuid | Nullable |

**Sealing rule.** A snapshot is sealed by the builder, verified by content hash, and never rewritten. A correction to canonical data produces a **new** snapshot; the old one remains so that the model trained on it remains explicable.

### 34.4 Training binding versus serving binding

| | Training | Serving |
|---|---|---|
| Binds to | `snapshot_id` set, listed in the model registry record | Prepared serving features, evidence store, live `model registry entry` |
| May scan | The full sealed snapshot | A bounded recent slice only |
| Latency | Hours to days | Seconds |
| Consistency requirement | Reproducibility | Freshness |

**Serving never reads a training snapshot and training never reads the serving slice.** They are built from the same canonical source and the same feature definitions, and gate G-38 asserts they agree on a sampled overlap. That check is what stops training-serving skew, which is the most common cause of a model that validates well and performs badly.

### 34.5 The return path

Governed intelligence outputs are projected back as **analytical datasets in Plant Data**, where Layer A, the Page Builder and the Assistant read them through the ordinary path. This is consistent with the Schema Topology contract, which places engine outputs in Plant Data because they exist because of this customer's data, and with the isolation rule that no analytical surface may display a row from outside Plant Data.

**Non-analytical operational artifacts** - model binaries, index files, registry records, gate reports, snapshot manifests, job state and supervisor proposals - are never displayed in an analytical surface. Per section 43.4 they live in Meta Data or in object storage, and the analytical role holds no grant on object storage.

---

### LB-A35. THE RELATIONSHIP MODEL IS MANDATORY FOR EVERY CONSUMER

### 35.1 The ruling in one sentence

**No intelligence engine may compose, infer, or hardcode a join. Every engine resolves entity correspondence through the published canonical relationship publication, or it refuses.**

### 35.2 Why this is the highest-severity rule in the pack

A wrong join does not fail. It produces plausible numbers across every surface, forever, and nothing downstream can attribute the error to its cause. A private join inside one engine is worse still, because two engines then disagree and neither can be shown to be wrong.

The worked case, stated generically: two source systems each carry an identity for the same physical production entity under different names. The customer's engineer declares once, during preparation, that they are the same identity. **From that moment every genealogy walk, every cross-position correlation, every feature that spans the two systems, and every widget showing both together depends on that one declaration.** Nothing re-derives it. Nothing is permitted to.

### 35.3 The canonical relationship authority

**Chapter 2 3.15 positions it. Chapter 3 4.5.10 implements it. Layer B defines no relationship object of its own.**

| Canonical object | Holds |
|---|---|
| `ppiq_meta.plant_relationships` | Left and right entity, join type, cardinality, grain on both sides, `is_grain_converting`, `attribution_rule` NOT NULL when grain-converting, `attribution_expression`, `is_preferred_path`, `ambiguity_state`, `validation_state`, `validation_detail`, `source_definition_id`, `source_definition_version`, `effective_from_utc`, `retired_at_utc` |
| `ppiq_meta.plant_relationship_members` | Ordered composite key pairs, `member_order`, `comparison` |
| `ppiq_meta.plant_relationship_paths` | Materialised transitive paths: `hop_count`, `path_json`, `crosses_grain`, `is_preferred` |

**Publishing a transformation emits the model.** There is no separate publication act.

**Provenance pinning.** A trained model version, a snapshot, a JobRun, an evidence row and a prediction each pin `source_definition_id` and `source_definition_version` for the relationship publication in force. Gate G-36 asserts the pinned version is effective, not retired, and that its `validation_state` permits the caller's declared `purpose`.

**A relationship is deactivated, never deleted**, so a finding computed under a retired relationship stays explainable.

### 35.4 The single resolution authority

One component, `RelationshipResolver`, is the only code in Layer B that converts a declared relationship into an executable path.

| Consumer | Resolves through |
|---|---|
| Statistics and correlation (DF9) | RelationshipResolver |
| Feature engineering | RelationshipResolver |
| Model training | RelationshipResolver |
| Prediction and scoring | RelationshipResolver |
| Practice learning | RelationshipResolver |
| Remediation search | RelationshipResolver |
| Value engine | RelationshipResolver |
| Assistant retrieval | RelationshipResolver |
| Evidence assembly | RelationshipResolver |

**This is the Single Engine Implementation principle applied to joins.** One implementation, no duplicates, no private paths.

### 35.5 Refusal conditions

An analysis refuses, with terminal state `REFUSED_BY_GUARD` and a named reason, when the required relationship is:

| Condition | Reason text names |
|---|---|
| **Unpublished** | The relationship code and that it exists only as a draft |
| **Ambiguous** | The competing paths and why the resolver will not choose |
| **Invalidated** | The invalidation reason and the version that invalidated it |
| **Incompatible** | The model's pinned RMV against the current published RMV |
| **Below quality floor** | The failing IV metric and its measured value |

**The refusal names the relationship, not the data.** This is the same discipline as G-19 and G-23, applied to the join layer.

### 35.6 Provenance pinning

Every model registry entry, training run, evidence row and prediction pins: **`semantic_manifest_id`**, plus `source_definition_id` and **`source_definition_version`** for the relationship publication, `feature_set_version_id`, and the `snapshot_id` lineage. Section 17.1 is extended accordingly.

---

### LB-A36. INTELLIGENCE BLOCKS IN THE NO-CODE ANALYSIS CANVAS

### 36.1 The premise

The customer authors an analysis by dragging, wiring, configuring and saving. The saved graph compiles to one versioned `AnalysisDefinition`. No code, no developer, no per-customer branch.

### 36.2 DP-29 `block_definition` - the block registry

> **BOUND TO `the registry-driven toolbox groups of Chapter 4 5.2.5`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

Registry-driven, exactly like the statistical method registry. **A new block is a registry row plus an engine binding, never a new canvas.**

| Field | Type | Notes |
|---|---|---|
| `block_code` | text | PK |
| `block_version` | int | |
| `toolbox_group` | smallint | **1 source and output, 2 relational, 3 arithmetic and logic, 4 statistics and correlation, 5 model and feature, 6 condition and action** (Ch4 5.2.5) |
| **`engine_kind`** | enum | **statistical, learned, retrieval, orchestration, governance, projection** |
| `engine_binding` | text | DF9, MF-01..MF-07, value, scenario, or a governance evaluator |
| `input_ports` | jsonb | Ordered, each with a port type |
| `output_ports` | jsonb | Ordered, each with a port type |
| `config_schema` | jsonb | Declarative form definition, rendered by the canvas |
| `eligibility_requirements` | jsonb | Capability-profile clauses |
| `emitted_dataset_code` | text | Nullable for non-output blocks |
| `refusal_states` | enum[] | |
| `compute_class` | enum | interactive, batch_cpu, batch_gpu |
| `is_enabled` | bool | |

### 36.3 The canonical toolbox groups

**Chapter 4 5.2.5. Groups are extended by registry entry, never by a code branch.**

| Group | Contents | Surfaces |
|---|---|---|
| **1 Source and output** | Source table; output to canonical entity; output to named dataset | All |
| **2 Relational** | Join, filter, select columns, rename, group by, sort, union, distinct, limit, pivot, derived column, cast, lookup | S1, S2 |
| **3 Arithmetic, comparison and logic** | **Expression blocks, not board blocks.** They live inside the block they configure, opened by double-click, on all five surfaces without exception | All five |
| **4 Statistics and correlation** | The method catalogue of Ch4 5.5: Group A descriptive, Group B association, **Group C discipline**, Group D process and quality | **S3** |
| **5 Model and feature** | Feature engineering blocks (Ch4 5.6.2), model blocks (5.6.3), prediction and recommendation blocks (5.6.4), practice authoring blocks (5.6.4a) | **S4** |
| **6 Condition and action** | Threshold condition, range condition, routing-deviation condition, emit info, emit warning, emit error | S5 |

**Group C discipline blocks are always applied and never user-selectable.** False-discovery control, effect-size ranking, stratification, bootstrap stability and confounder check run on every association result, and their outputs are stored with the finding as data. A user may inspect them; a user may not switch them off, and a validator refusal states so.

**Control flow does not belong on any board.** `FOR` and `WHILE` are orchestration. A saved definition describes what its output is; how often it runs and over what window belongs to the job that carries it.

### 36.4 Naming discipline, ruled

**Not every intelligence block is an ML model, and the registry field `engine_kind` enforces the distinction in data rather than in documentation.**

| Block | `engine_kind` | What it actually is |
|---|---|---|
| Correlation | `statistical` | A method from the DF9 registry. No model, no training |
| Statistics | `statistical` | Same |
| Deep analysis | `orchestration` | **Composes several engines and returns a combined evidence set.** It is a plan, not an algorithm |
| Anomaly | `learned` | Consumes MF-03 |
| Similarity / fingerprint | `retrieval` | Consumes the encoder and the index. Retrieval, not inference |
| Supervised prediction | `learned` | Consumes a trained MF-04 model from the active model version |
| Practice learning | `statistical` with a learned component | MF-07 |
| Remediation search | `governance` plus `statistical` | The nine-check evaluation over candidates |
| Scenario | `learned` | Inference on a pinned model |
| Value | `projection` | Arithmetic over a declared assumption contract. **Never a model** |

Calling the value block a model would be the clearest possible way to destroy trust in it, because a customer would then ask what it was trained on and the answer is nothing.

### 36.5 Port typing and validation

Ports carry types: `Population`, `Grain`, `Outcome`, `FeatureSet`, `Window`, `ContextScope`, `RelationshipPath`, `ModelRef`, `Finding`, `Prediction`, `Contributor`, `Similarity`, `Anomaly`, `Practice`, `Envelope`, `Value`, `Evidence`.

**Illegal wiring is refused at drag time with a written sentence**, matching the discipline already shipped on the preparation canvas. Five refusal classes at authoring time: type mismatch, missing required scope, relationship not published for the declared path, eligibility not met by the capability profile, and aggregate used outside an aggregation context.

### 36.6 DP-30 `analysis_definition`

> **BOUND TO `ppiq_meta.definition_store + definition_versions + analysis_details`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `analysis_definition_id` | uuid | PK |
| `analysis_definition_version` | int | Immutable once published |
| `status` | enum | draft, validated, published, superseded, rolled_back |
| `graph` | jsonb | Nodes, edges, per-node config |
| `block_versions` | jsonb | **Every block code with its pinned version** |
| `required_definition_version_ids` | jsonb | The published definitions this graph depends on |
| `required_source_definition_version` | uuid | |
| `required_capabilities` | jsonb | Union of block eligibility clauses |
| `emitted_dataset_codes` | text[] | |
| `compiled_plan` | jsonb | Topologically ordered execution plan |
| `content_hash` | text | |

**Compilation is server-side and deterministic**, on the same principle as SQL compilation on the preparation canvas. The client never composes the plan.

### 36.7 Genericity constraint

No block implementation may contain a customer identifier, a source table name, or an industry noun. A block reads its scope from its input ports and its semantics from the semantic model. The Semantic Wall test of section 2.2 extends to the block tree.

---

### LB-A37. JOB DEFINITION, JOB RUN, AND THE MODEL-COPY DISTINCTION

### 37.1 The ruling

**A JobRun is an execution context. It is not a copy of a model.**

Eleven concurrent runs are eleven identities, eleven progress states, eleven lineages, and **one** loaded artifact per distinct model in the active model version.

### 37.2 DP-31 `job_definition`

> **BOUND TO `the job definition contract of Chapter 4 5.3`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type |
|---|---|
| `job_definition_id` | uuid PK |
| `analysis_definition_id` / `analysis_definition_version` | |
| `job_kind` | enum: analysis, training, scoring, evaluation, supervisor |
| `schedule_kind` | enum: manual, cron, event, micro_batch |
| `schedule_spec` | text |
| `scope` | jsonb: tenant, site, population filter, window policy |
| `priority_class` | enum: interactive, standard, background |
| `pool_class` | enum: import, projection, analysis, ml, report |
| `resource_hint` | jsonb |
| `enabled` | bool |

### 37.3 DP-32 `job_run`

> **BOUND TO `the run tables of Chapter 3 4.5.12 (compute_runs, prediction_runs, model_training_runs, practice_learning_runs, feature_refresh_runs)`.** The field list below states the semantic requirement. Canonical columns govern; names below that differ are dropped. See the Canonical Binding Register, section 43.

| Field | Type | Notes |
|---|---|---|
| `run_id` | uuid | PK |
| `job_definition_id` | uuid | |
| `tenant_id` / `site_id` | | |
| `analysis_definition_version` | int | **Pinned at admission, not at execution** |
| `semantic_manifest_id` | uuid | Pinned at admission. Required for every new governed execution |
| `snapshot_ids` | uuid[] | Training and analysis runs |
| `model_version` | text | Scoring and scenario runs |
| `run_scope` / `filters` / `window` | jsonb | |
| `state` | enum | queued, admitted, running, paused, cancelling, cancelled, succeeded, failed, refused |
| `progress` | jsonb | Stage, percent, rows processed, checkpoint token |
| `cancellation_requested_at` | timestamptz | |
| `retry_of_run_id` | uuid | Nullable |
| `resource_accounting` | jsonb | CPU seconds, peak RAM, GPU seconds, rows scanned |
| `output_evidence_ids` / `output_dataset_refs` | | Lineage |
| `refusal_reason` | text | Populated when state is refused |

**Every field above exists because a customer will ask about it while a job is running.** A run without independent progress and cancellation is a job the customer cannot manage.

### 37.4 The model instance pool

The distinction the requirement demands, made mechanical.

| Concept | Cardinality |
|---|---|
| Analysis definitions | Many. Authoring artifacts, cost nothing at rest |
| Job definitions | Many |
| Concurrent JobRuns | Bounded by admission control, section 38 |
| Distinct model artifacts in the active model version | Bounded by the model-count governor, section 6.7 |
| **Loaded model instances in memory** | **Bounded by a reference-counted replica pool, not by run count** |

The serving runtime keys loaded instances by `(model_version, model_code)`. Runs attach to an existing instance. Replica count is a **measured infrastructure decision** driven by throughput and latency, per section 14, and is invisible to the authoring surface.

**A customer authoring two hundred analyses has not created two hundred models.** Gate G-40 asserts that loaded-instance count is a function of the active model set and measured replica policy, never of run count.

### 37.5 Training runs are categorically separate

Training runs produce **candidate** artifacts in the learning plane. They never attach to the active model version instance pool, and no serving or scoring run may enter a training execution path. This is the Serving Wall of section 2.3 expressed in the job model, and gate G-41 asserts it.

---

### LB-A38. CONCURRENCY AND RESOURCE GOVERNANCE

**Canonical: Ch4 5.3.2. Layer B declares weights and constraints to the existing weighted job pool. It does not introduce a scheduler.**

### 38.1 Classes, lanes and admission

**Six logical job classes, unchanged. The `ml` class resolves to three physical lanes** (amendment C4-2).

```
CLASS         LANE                 CONCURRENCY  CAPACITY  PRE-EMPT  ADMITS
import        -                       B-01        B-01      yes     import, backfill
projection    -                       B-01        B-01      yes     canonical projection,
                                                                    spine, feature refresh,
                                                                    snapshot sealing
analysis      -                       B-01        B-01      yes     statistics, correlation,
                                                                    practice, evidence
ml            ml.training             B-01        B-01     *YES*    encoder + supervised
                                                                    training, calibration,
                                                                    SHAP batch, index build
ml            ml.batch_scoring        B-01        B-01      yes     scheduled scoring,
                                                                    backfill, rescore
ml            ml.online_scoring       B-01     RESERVED     *NO*    event + micro_batch
                                                            B-02    scoring only
report        -                       B-01        B-01      yes     report generation, export
interactive   -                    reserved    reserved     no      never batch
```

**Admission requires both predicates:**

```
admit  iff  running_count < max_concurrency
       AND  sum(compute_weight of running) + compute_weight(candidate) <= resource_capacity
```

`max_concurrency` is how many runs may be in flight. `resource_capacity` is how much scarce resource exists. `compute_weight` is how much one run consumes. **One number never expresses two quantities.** G-50 asserts that for every lane the heaviest declared job is admissible; a configuration where a declared job can never be admitted fails the gate.

**The online reservation is never available to `ml.training` or `ml.batch_scoring` admission**, on the same principle as the `interactive` reservation.

**`ml.online_scoring` carries operational event and micro-batch scoring and its required serving functions only.** Batch, backfill and rescore work runs on batch and training-class capacity. Where a deployment physically shares hardware, online capacity is still hard-reserved and B-02 must prove the actionable-latency target holds while training and batch work are saturated.

**Warm models.** Artifacts for every active serving identity are resident, reference-counted, with a declared eviction policy. A newly activated model is warmed before it serves, and first-score-after-activation latency is bounded and measured.

**Pre-emption.** `ml.training` runs are checkpointed per stage (section 7.3). A training run yields at its next checkpoint when a reserved lane needs capacity and resumes from it. **Nothing is lost except elapsed time, which is the correct trade against an expiring prediction.** Where the runtime cannot pre-empt, the lane falls back to admission-time reservation only, and this is recorded rather than assumed.

### 38.2 Admission

Each job definition carries a **`compute_weight`**, default 1. Admission uses **both predicates of section 38.1**: a run count against `max_concurrency`, and a weight sum against `resource_capacity`. The weight is edited behind a confirmation stating the resulting utilisation, and **G-50 refuses a configuration in which a declared job could never be admitted**.

**Connection discipline.** All pools sit behind a connection pooler, and job workers use a **separate pooler identity** from the interactive path, so batch work physically cannot exhaust the connections the interface needs.

### 38.3 Layer B workload mapped onto the pools

| Layer B workload | Pool |
|---|---|
| Ingest of new and changed source data | `import` |
| Canonical projection, spine build, feature refresh, sequence build, outcome build, snapshot sealing | `projection` |
| Statistical engine DF9, practice engine, envelope and effect computation, capability profiling, drift metrics, evidence materialisation | `analysis` |
| Encoder training, encoding, index build, novelty fit, supervised training, calibration, importance computation, champion evaluation | **`ml.training`** |
| Scheduled scoring, backfill scoring, rescore after activation | **`ml.batch_scoring`** |
| **Event and micro-batch operational scoring** | **`ml.online_scoring`, hard-reserved** |
| Evidence and dataset materialisation for delivery, scheduled report generation | `report` |
| Tier 1 reads, tier 2 bounded analysis, scenario evaluation, Assistant tool calls | `interactive`, reserved |

### 38.4 Run policies

| Policy | Behaviour | Applied to |
|---|---|---|
| **Skip if running** | The tick is dropped and recorded as skipped with the reason | Import, feature refresh |
| **Latest only** | Queued duplicates collapse to the newest request | Alert evaluation, **scoring** |
| **Queue** | Runs accumulate in order, bounded by queue depth | Reports |
| **Reject** | Refused with a named error | User-triggered runs when the pool is saturated |

**A skipped tick is visible in the monitor with its reason.** Silent skipping is how a plant discovers three weeks later that a job never ran.

### 38.5 Degradation

The canonical five-level ladder governs: normal, elevated, high, critical, protective. Report and ML pools reduce first; non-critical cadences stretch; at critical only import and interactive are admitted; at protective new user-triggered runs are refused with a named error and an estimated wait. **Every level is announced.** A product that quietly stops doing analysis is worse than one that says it is behind.

### 38.6 Relationship to the weekly window

The weekly sequence of section 8 executes as jobs in the `projection`, `analysis` and `ml` pools under this admission control. The abort ladder of section 8.2 is the degradation policy for that specific job family and operates above admission, never instead of it.

### LB-A39. THE END-TO-END CANVAS TO OUTPUT CONTRACT

### 39.1 The full flow, with the owning component and the failure mode at each step

| # | Step | Owner | Fails as |
|---|---|---|---|
| 1 | Drag intelligence blocks onto the canvas | Analysis canvas | Block absent from registry or disabled |
| 2 | Wire them | Canvas | **Refusal at drag time with a written sentence**, five classes |
| 3 | Validate semantic and relationship dependencies | Canvas plus RelationshipResolver | Named refusal identifying the unpublished or ambiguous relationship |
| 4 | Compile the graph | Server-side compiler | Cycle, unreachable output, unbound required port |
| 5 | Save `AnalysisDefinition` version | Definition store | Immutable publish, draft otherwise |
| 6 | Readiness check against the capability profile | Capability profiler | `MODEL_NOT_READY` naming the failed clause and its measured value |
| 7 | Create or schedule a `JobDefinition` | Job service | Invalid schedule for the declared scoring mode, gate G-26 |
| 8 | Scheduler admits a `JobRun` | Weighted job pool | Queued with visible position, or refused on quota |
| 9 | Engine resolves pinned versions | Engine host | Refusal on version incompatibility, gate G-36 |
| 10 | Execute | Engine plus pools | Checkpointed, cancellable, retryable |
| 11 | Persist governed result datasets | Evidence materialiser | Refusal rows persisted like findings |
| 12 | Expose to Page Builder, charts and Assistant | Dataset catalogue plus tools | Ordinary governed datasets, no ML-specific path |

### 39.2 The version pin, carried the whole way

An answer on a chart in step 12 resolves back through: dataset row, evidence id, run id, analysis definition version, block versions, `model_registry_id` and `model_version`, feature set version, snapshot id, `source_definition_version`, semantic model version, and the canonical watermark.

**That chain is the product's honesty claim made mechanical.** Any link missing makes "why did this number change" unanswerable.

### 39.3 What the customer never touches

Physical tables, joins, SQL for relationships, model selection, hyperparameters, training schedules, model artifacts, replica counts, pool weights.

### 39.4 What the customer fully controls

Which blocks, wired how, over which population, at which grain, in which window, against which outcome, with which context, on what schedule, at what priority, and whether to accept a recommendation.

---

### LB-A40. PLATFORM INTEGRATION GATES

### 40.1 Additional gates G-36 to G-46

| ID | Gate | When | Blocking |
|---|---|---|---|
| **G-36** | Relationship currency: the pinned `source_definition_version` is effective and not retired, and its `validation_state` permits the caller's `purpose` | Activation, run admission | **Yes** |
| **G-37** | Snapshot immutability: a sealed snapshot's content hash still matches its content | Every training run start | **Yes** |
| **G-38** | **Training-serving parity**: features computed by the serving path match the snapshot on a sampled overlap, within declared tolerance | Activation, weekly | **Yes** |
| **G-39** | **No private join**: no engine, block or query path outside `RelationshipResolver` composes an entity correspondence | Build-time architecture test, falsified once | **Yes** |
| **G-40** | Model instance economy: loaded instance count is a function of the active model set and replica policy, never of JobRun count | Serving, continuous | **Yes** |
| **G-41** | Job separation: no scoring, scenario or analysis run can enter a training execution path | Build-time plus runtime | **Yes** |
| **G-42** | Reserved interactive capacity is enforced and cannot be consumed by analysis or training admission | Admission, continuous | **Yes** |
| **G-43** | Block genericity: no block implementation contains a customer identifier, source table name or industry noun | Build-time | **Yes** |
| **G-44** | Definition pinning: every JobRun pins the definition version, block versions, semantic model version, `source_definition_version`, and its snapshot or model version at admission | Run admission | **Yes** |
| **G-45** | Authoring-time refusal: illegal wiring is refused on the canvas with a written sentence, all five classes exercised | Canvas, falsified once | **Yes** |
| **G-46** | Lineage completeness: every displayed intelligence value resolves to the full chain of section 39.2 with no missing link | Activation, sampled | **Yes** |

**G-39 is the single most important gate in this group.** A private join produces plausible numbers forever and is unattributable after the fact. It must be a build-time architecture test, and it must be falsified against a deliberately introduced private join before it is trusted.

### 40.1a Target architecture gates G-48 to G-55

| ID | Gate | When | Blocking |
|---|---|---|---|
| **G-48** | **Training reads no live feature state.** No training or encoding code path queries `feature_store`; training input resolves only through a sealed snapshot artifact. **The snapshot materialiser is exempt by definition** and is the only component permitted to read `feature_store` for sealing | Build-time and runtime | **Yes** |
| **G-49** | **Lane isolation.** No `ml.online_scoring` process imports a trainer module; the online reservation cannot be consumed by `ml.training` or `ml.batch_scoring` admission | Build-time and admission | **Yes** |
| **G-50** | **Admission predicate satisfiable.** For every lane, `max(compute_weight of any admissible job) <= resource_capacity`, and `max_concurrency >= 1`. A configuration where a declared job can never be admitted fails | Configuration validation | **Yes** |
| **G-51** | **ANN recall floor.** Every index build measures recall@k against exact Flat on the representative sample; a build below `recall_floor` does not become the served index | Index build | **Yes** |
| **G-52** | **Evidence budget integrity.** No packed evidence item lacks a resolvable handle; truncation is recorded and disclosed | Serving | **Yes** |
| **G-53** | **Claim-class integrity in language.** No answer phrases a lower claim class as a higher one. Measured by Q-06 against a fixed adversarial set | Release | **Yes** |
| **G-54** | **Governed-model-only learned output.** No feature, score, statistic or value derives from free-form or model-generated output | Build-time and training | **Yes** |
| **G-55** | **Manifest immutability and coverage.** A `semantic_manifests` row is never updated; identical content within a tenant never creates a second row; **every new governed AI/ML execution resolves a manifest**, legacy records excepted | Trigger plus run admission | **Yes** |

**Total inventory: G-01 to G-55.**

### 40.2 Open items

All remaining open items are **measurement decisions with a canonical home**, not architecture gaps:

| ID | Measurement | Written into |
|---|---|---|
| OD-05, OD-06 | Encoder and supervised eligibility thresholds | `model_details.acceptance_floor`, the gate minimums of Ch4 5.6.3 |
| OD-11 | Hardware sizing benchmark | The capacity model of Ch4 5.3.3 |
| OD-12, OD-21 | Sequence and snapshot retention | `feature_snapshots.retention_until_utc`, per-stage retention policy |
| OD-25 | Reserved interactive capacity fraction | The `interactive` reservation of Ch4 5.3.2 |

Each is a number to be measured and then recorded in an existing canonical field. **None requires a design decision.**

---
---

# PART SIX - DIRECT SOURCE BINDING

**The chapters were read. This part governs where it differs from anything above it.**

---

### LB-A41. SOURCE RECONCILIATION RECORD

### 41.1 Documents read

| Document | Size | Read |
|---|---|---|
| `PPIQ_Chapter1_Marketing_and_Sales.md` | 43 KB | Referenced |
| `PPIQ_Chapter2_Technical_Overview.md` | 74 KB | **Yes** |
| `PPIQ_Chapter3_General_Technical_Function_Description.md` | 324 KB | **Yes** |
| `PPIQ_Chapter4_Specific_Technical_Function_Description.md` | 186 KB | **Yes** |
| `PPIQ_Chapter5_Tutorial_User_Journey.md` | 70 KB | Referenced |
| `PPIQ_Chapter6_Infrastructure_Website_Administration.md` | 154 KB | Referenced |

### 41.2 Sections read directly and used

| Section | Content | Bound in |
|---|---|---|
| **Ch2 authority statement** | Chapter 2 is the naming, structure and positioning authority for seven things including the relationship model and its sixteen consumers | 50 |
| **Ch2 3.15.1 to 3.15.5** | The permanent plant relationship model: declared once, validated once, published once; the seven declarations; the record properties; the sixteen consumers, exhaustive and binding; the reviewer consequences | 50 |
| **Ch3 4.5.10** | `plant_relationships`, `plant_relationship_members`, `plant_relationship_paths` with full DDL; ambiguity refuses; unproven blocks automation not exploration; grain conversion requires attribution; retirement preserves history; **one resolver**; `GET /api/relationships/resolve?from=&to=&purpose=` | 50 |
| **Ch3 4.5.11** | `definition_store`, `definition_versions`, `definition_dependencies`, ten detail tables, export artifacts, one lifecycle | 51.2 |
| **Ch3 4.5.12** | The intelligence tables, full DDL: `compute_runs`, `correlation_results`, `feature_store`, `feature_snapshots`, `model_registry`, `model_training_runs`, `model_drift_observations`, `prediction_runs`, `predictions`, `prediction_drivers`, `prediction_comparables`, `prediction_current`, `practice_signatures`, `practice_statistics`, `practice_learning_runs`, `practice_drift_observations`, `remediation_candidates`, `prediction_remediation_evaluations`, `forbidden_combinations`, `prediction_actions`, `prediction_evaluations`, `remediation_effectiveness`, `suggestions`, `suggestion_decisions`, `feedback_records`, `value_impacts`, `value_realization_ledger`, `assistant_chunks`, `supervisor_proposals`, `supervisor_shadow_runs`, `supervisor_provenance` | 51 |
| **Ch3 4.5.12a** | `can_accept` as the complete acceptance authority, seven conditions, client never re-derives, `RM10` on the write path | 52.3 |
| **Ch3 4.5.12b** | `remediation_escalations` | 52.4 |
| **Ch3 4.5.13** | Intelligence as a bindable source: `registry_dimensions` with the three controllability columns, `registry_measures`, `registry_intelligence_sources`, the three design obligations | 53 |
| **Ch3 DF7 payload** | `columns + rows + warnings`; `sourceKind: canonical \| intelligence`; `intelligenceSource`; `columnRoles` | 53 |
| **Ch4 5.2.1 to 5.2.6** | One shell, five purposes S1 to S5; two modes; four regions; the schema table bar; **toolbox Groups 1 to 6** | 51.2 |
| **Ch4 5.5.1 to 5.5.6** | The statistics catalogue: Group A descriptive, Group B association, **Group C discipline, always applied and never optional**, Group D process and quality; the S3 validator rules | 51.1 |
| **Ch4 5.6.1 to 5.6.7a** | Position; Group E feature blocks; Group F model blocks; Group G prediction and recommendation; G2 practice blocks; the practice-learning engine; the predict-then-remediate pipeline; **the nine-check gate**; model governance; the serving path; the fallback policy | 52 |
| **Ch4 5.3.2** | The nine-mechanism defence stack; **the six pools with parallelism**; `compute_weight`; connection discipline; the five-level degradation ladder | 55 |
| **Ch4 5.4.9, 1539-1580** | The Supervisor: most constrained component; honesty machinery outside its write scope by absent permission; shadow execution | 51.3 |
| **Ch4 5.8.1 to 5.8.8** | Scenario simulation; alert routing; prediction explainability; the feedback loop; internal benchmarking; **unstructured text evidence, future extension with interfaces designed**; **inspection images, same**; actionable prediction latency | 54, 56.2 |

---

### LB-A42. THE RELATIONSHIP MODEL - FINAL AND BINDING

### 42.1 The canonical authority

**Chapter 2 3.15 positions it. Chapter 3 4.5.10 implements it.** Layer B defines no relationship version object of its own.

| Canonical object | Holds |
|---|---|
| `ppiq_meta.plant_relationships` | One declared relationship: left and right entity, join type, cardinality, grain on both sides, `is_grain_converting` generated, `attribution_rule` NOT NULL when grain-converting, `attribution_expression`, `is_preferred_path`, `ambiguity_state`, `validation_state`, `validation_detail`, `source_definition_id`, `source_definition_version`, `effective_from_utc`, `retired_at_utc` |
| `ppiq_meta.plant_relationship_members` | Ordered composite key pairs with `member_order` and `comparison` |
| `ppiq_meta.plant_relationship_paths` | Materialised transitive paths with `hop_count`, `path_json`, `crosses_grain`, `is_preferred` |

**Publishing a transformation emits the model.** Layer B pins `source_definition_id` and `source_definition_version`.

### 42.2 The four behavioural rules Layer B must obey

| Rule | Effect on Layer B |
|---|---|
| **Ambiguity refuses rather than guesses** | Two unretired paths with no preferred path returns `RL01` naming both. No engine picks one |
| **Unproven blocks automation, not exploration** | `validation_state = unproven` permits workspace exploration and **refuses statistical, feature, model, practice and prediction use** with `RL02`. This is stricter than my section 35.5 and it governs |
| **Grain conversion requires attribution** | Weights per child sum to exactly 1.0, enforced by CHECK and by `TR09` at publish. The genealogy-attributed correlation block of Ch4 5.5.3 refuses otherwise |
| **One resolver** | A single path-resolution service is the only code that reads these tables. Every consumer calls `GET /api/relationships/resolve?from=&to=&purpose=`, where `purpose` is one of the sixteen consumers |

**`purpose` is the mechanism I did not have.** An unproven relationship is usable by `explore` and not by `train`. The resolver enforces the distinction; the caller does not.

### 42.3 The sixteen consumers, binding and exhaustive

Canonical projection, registry generation, page and widget query compiler, associative filtering, drill-down, drill-through, genealogy, statistical analysis, correlation, feature engineering, model training, model scoring, practice learning, prediction and remediation search, value calculation, Assistant retrieval and tools. Plus evidence and provenance in reverse.

**A capability that re-derives a join instead of reading the model is a defect.** G-39 stands, now testable against the named service.

---

### LB-A43. CANONICAL BINDING REGISTER

Every semantic concept in this pack, its canonical object, and what the pack adds.

### 43.1 Statistics and correlation

| Pack concept | Canonical | Pack adds | Why no second authority |
|---|---|---|---|
| DF9 statistical engine, MF-06 | Ch4 5.5 Groups A to D as registry block rows; `ppiq_plant.compute_runs`; `ppiq_plant.correlation_results` | Nothing. Section 21's execution order is a restatement of Group C discipline | The blocks are registry rows; the pack adds no block |
| DP-15 method registry | The Group A to D catalogue rows | Nothing | Withdrawn as an object |
| My "FDR is structural" | **Group C discipline blocks are always applied and never user-selectable**; `correlation_results.q_value` NOT NULL in practice | Nothing | Canonical is stricter than my design |
| My effect-size ranking | **Effect-size ranking refuses to order by p-value** | Nothing | Canonical already states it |

**Two canonical columns I did not have:** `correlation_results.framing_text NOT NULL` and `llm_participated`, stored as data so the framing survives an export, a screenshot and a report. Adopted.

### 43.2 Authoring and definitions

| Pack concept | Canonical | Status |
|---|---|---|
| Analysis canvas | **One shell, five purposes**: S1 data preparation, S2 widget and page binding, S3 analysis authoring, S4 model authoring, S5 plant data log | My two-purpose framing withdrawn |
| Block categories | **Toolbox Groups 1 to 6**: source and output; relational; arithmetic, comparison and logic as expression blocks; statistics and correlation (S3); model and feature (S4); condition and action (S5) | My four invented categories withdrawn |
| DP-30 analysis definition | `definition_store` + `definition_versions` + `analysis_details` | Bound |
| DP-33 model definition | `definition_store` + `definition_versions` + `model_details` | Bound. **The object exists; it is a `definition_kind`, not a table** |
| Feature set | `definition_store` + `feature_set_details` | Bound |
| Practice definition | `definition_store` + `practice_details` | Bound |
| Scenario definition | `definition_store` + `scenario_details` | Bound |
| DP-29 block registry | Toolbox groups extended by registry entry, never by a code branch | Bound as the mechanism, not a new table |
| Definition lifecycle | `draft -> validated -> published -> paused_by_drift \| rolled_back -> superseded`, immutable published rows enforced by trigger | Adopted; my lifecycle enum replaced |

**`definition_dependencies` with a cycle-refusing trigger** is canonical and I did not have it. It is what makes the impact preview of Ch2 3.15.5 possible.

### 43.3 Models, predictions, remediation, decisions

| Pack concept | Canonical |
|---|---|
| Feature store DP-2 | `ppiq_plant.feature_store` (`features jsonb`, UNIQUE `(analysis_subject_id, feature_set_version_id)`, `lineage_hash`, `is_dirty`), plus `feature_refresh_watermarks`, `feature_refresh_runs` |
| Snapshots DP-28 | `feature_snapshots` + `feature_snapshot_rows`, immutable, with `storage_uri` |
| model registry entry | **`model_registry`.** Serving identity `(tenant_id, model_code, outcome_code, grain_code)` plus `model_version`; `status` and `serving_role` independent axes |
| Training run | `model_training_runs`, with **CHECK `overlap_rows = 0`** |
| Drift | `model_drift_observations` |
| Scoring run | `prediction_runs`, with `trigger_kind` and `scoring_mode` as separate columns |
| Predictions | `predictions`, with `actionable_deadline_utc`, `deadline_basis`, `met_actionable_deadline`, `delivery_latency_seconds NOT NULL` |
| Contributors DP-2 of section 10 | `prediction_drivers` |
| Similarity | `prediction_comparables` |
| Operational read model DP-18 | `prediction_current`, PK `(tenant_id, analysis_subject_id, outcome_code)` |
| Practice DP-16, DP-17 | `practice_signatures`, `practice_statistics`, `practice_learning_runs`, `practice_drift_observations` |
| Remediation template | `remediation_candidates`, CHECK `support_count >= 20` |
| Remediation gate DP-19 | `prediction_remediation_evaluations` |
| Safety rules SM-13 | `ppiq_plant.forbidden_combinations`, imported or customer-authored, never shipped |
| Decisions DP-20, actions DP-21 | `prediction_actions`, append-only |
| Escalation | `remediation_escalations` |
| Evaluation DP-22 | `prediction_evaluations`, CHECK `observed_from = 'canonical'` |
| Effectiveness DP-23 | `remediation_effectiveness` |
| Feedback DP-24 | `feedback_records`, with `quality_state` gating what reaches the Supervisor |
| Value DP-25, DP-26 | `value_impacts`, `value_realization_ledger` |
| Supervisor DP-27 | `supervisor_proposals`, `supervisor_shadow_runs`, `supervisor_provenance` |
| Assistant retrieval | `assistant_chunks` with `role_scope` |
| Scenario | `ppiq_plant.scenario_runs` |

### 43.4 Storage placement

The three-schema law stands. No fourth application schema.

| Content | Location |
|---|---|
| Customer-derived analytical and intelligence datasets: predictions, contributors, similarity, anomalies, envelopes, findings, readiness | **Plant Data** |
| Operational and control-plane metadata belonging in the application database: registry records, model registry records, snapshot manifests, job definitions, job runs, gate reports, supervisor proposals, block definitions, analysis definitions | **Meta Data** |
| Pre-semantic, source-shaped data | **Dump Store** |
| Model binaries, encoder checkpoints, vector index files, large binary artifacts | **Object / artifact storage** |

**Two consequences that must be built, not assumed:**

1. **Analytical surfaces do not read operational artifact storage.** The analytical role holds no grant on it, and the isolation architecture test extends to cover it.
2. Where operational metadata must become analytically visible, for example a readiness view or a job history chart, it is published as a **governed Plant Data read model or projection**. It is never read across the boundary. Gate G-47 asserts this.

**Placement of the thirty-two data products:**

| Products | Location |
|---|---|
| DP-1 spine, DP-2 features, DP-3 sequences, DP-4 outcomes, DP-6 predictions, DP-7 evidence, DP-16 practice signatures, DP-17 practice matches, DP-18 prediction current, DP-19 remediation eligibility, DP-20 decisions, DP-21 actions, DP-22 evaluations, DP-23 effectiveness, DP-25 value impact, DP-26 value ledger | **Plant Data** |
| DP-5a embedding rows, DP-5b index manifests, DP-15 method registry, DP-24 feedback, DP-27 supervisor proposals, DP-28 snapshot manifests, DP-29 block definitions, DP-30 analysis definitions, DP-31 job definitions, DP-32 job runs | **Meta Data** |
| Encoder checkpoints, index binary files, model artifacts, snapshot Parquet content | **Object / artifact storage** |

**DP-5a is the one judgement call in that table.** Embedding vectors are customer-derived and could sit in Plant Data, but they are not analytically displayable and are large. Placing the **rows** in Meta Data with the **vectors** in artifact storage keeps analytical surfaces clean. If Chapter 3 rules otherwise, Chapter 3 wins.

### 43.5 What the pack legitimately adds

Only implementation-neutral explanatory architecture, none of it a second authority:

| Addition | Why it is not a competing authority |
|---|---|
| The Semantic Wall and Serving Wall as named enforcement layers | Restates Ch3 grants and Ch4 pool isolation as testable rules |
| The capability profile and the intelligence ladder | An explanatory frame over canonical readiness gates. Emits no persistent object |
| The three-schedule budget tables and the abort ladder | Sits above Ch4 5.3.2 mechanism 9. The canonical degradation ladder governs; my budgets are planning figures |
| The fifty-five gates | Test specifications over canonical constraints. Where a canonical CHECK exists it is the stronger mechanism and the gate asserts it |
| The genericity proof | Explanatory only |
| The dependency order and sizing framework | Planning, not architecture |

---

### LB-A44. BEHAVIOURAL CORRECTIONS APPLIED FROM THE CHAPTER TEXT

Five material behavioural differences found. **In every case the chapter governs and the pack is corrected.**

### 44.1 The remediation gate outcome mapping was wrong

My section 24.2 mapped outcomes to check numbers incorrectly. The canonical mapping, from Ch4 5.6.4d:

| Outcome | Canonical condition |
|---|---|
| **Actionable** | All nine pass |
| **Evidence only** | **Checks 5 to 9 pass, but 1 to 4 fail for this unit.** Shown in drill-down as an observed historical difference, never styled as a recommendation |
| **Suppressed** | **Check 4 fails on a safety constraint.** Not shown at all; recorded on the run with `RM04` so the suppression is auditable |
| **Exploratory** | **Checks 1 to 6 pass, 7 or 8 fail.** Shown behind an explicit disclosure. No accept action, at any tier, for any role |

**The decision boundary is wider than I stated.** Accept, Reject **and** Defer all exist only where `can_accept` is true. `evidence_only`, `exploratory` and `suppressed` carry no decision control of any kind and are **outside the decision record entirely**, because rejecting or deferring an observation would enter it into the effectiveness and feedback statistics as though it had been offered as a recommendation.

### 44.2 The two-table separation of template and evaluation

`remediation_candidates` is the **global historical template**, computed once per condition. `prediction_remediation_evaluations` is the **per-prediction gate result**. Storing eligibility on the template would be wrong, because the same template is actionable for a unit two stages away and not for one that has passed the stage.

**Five checks are properties of history** and are evaluated at template generation; a template failing them is never created. **Four are situational** and cannot be. My DP-19 conflated the two.

### 44.3 `can_accept` has seven conditions, not one

It is not a synonym for `eligibility_state = 'actionable'`. It additionally requires the stage not passed, the deadline not elapsed, the prediction still open, no safety constraint invalidated since evaluation, the model not in `review` or `retired`, and the caller's entitlement and role. **The client renders the affordance from `can_accept` alone**; `can_accept_blockers` explains only. A UI that additionally tests the deadline has created a second authorisation rule.

### 44.4 `remediation_escalations` is an object I did not have

The record produced when a non-actionable candidate is escalated for engineering investigation. **It is a record, never a decision.** It creates no `prediction_actions` row, contributes to no effectiveness row, and is excluded from `feedback_records`. Only `evidence_only` and `exploratory` can be escalated. `promoted_to_actionable` is the one resolution that changes product behaviour and is audited as a governed change.

### 44.5 Practice enums differ from mine

`practice_statistics.sensitivity_state` is **`stable`, `fragile`, `unstable`, `not_tested`** - four values, not three. `not_tested` is the explicit state for an unevaluated band and **is treated as `fragile` for remediation conversion**. `state` is `benchmark`, `observed_unproven`, `failure_associated`. `backoff_rule` has six values: `exact`, `widened_tolerance`, `coarsened_dimensions`, `sequence_generalisation`, `context_widening`, `weighted_similarity`.

Canonical CHECKs the pack must not restate differently: `state <> 'benchmark' OR sensitivity_state = 'stable'`; `state <> 'benchmark' OR (support_count >= 20 AND sensitivity_state <> 'unstable')`; `similarity_level = 0 OR relaxed_support_count IS NOT NULL`.

### 44.6 Value: canonical permits a point estimate

`value_impacts` carries `lower_bound`, `upper_bound` **and `point_estimate`**, with `basis_status` in `Sufficient` or `InsufficientBasis` and CHECK `basis_status = 'InsufficientBasis' OR (lower_bound IS NOT NULL AND upper_bound IS NOT NULL)`. **My rule that no point estimate is ever emitted is stricter than canonical and is withdrawn as a rule.** The binding constraint is that bounds are mandatory when the basis is sufficient. `currency char(3) DEFAULT 'EUR'` is canonical and is a per-row column, so the genericity concern is already handled.

---

### LB-A45. THE BINDABLE INTELLIGENCE CONTRACT - FINAL

`ppiq_meta.registry_intelligence_sources` is the declaration that makes an intelligence table bindable: `source_code`, `physical_relation`, `grain`, `entity_link_column`, `link_entity`, `default_time_column`, `minimum_role`, `minimum_tier`.

**The three canonical design obligations:**

1. **Registry derivation writes both kinds.** When an intelligence run first produces results, dimension and measure rows are derived for that source. A palette offers `risk_class` beside a canonical dimension with no special case.
2. **The widget query compiler resolves into the results area** through `plant_relationship_paths`, so a prediction and the parameter that drove it can occupy one widget. No path means the binding is refused with `WD07`.
3. **Associative state reaches intelligence.** A selection on a canonical field propagates to intelligence widgets through the same path resolution.

Widget payload: `sourceKind: "canonical" | "intelligence"`, `intelligenceSource`, `columnRoles`. Execution returns **columns, rows and warnings**. Intelligence sources are **read-only and never writable from a widget**.

### 45.4 The two analytical source classes

**Class 1 - Aggregate / fact-shaped sources.** Exact canonical measures, and aggregateable intelligence measures. These may project through `WidgetFact` into the generic aggregate executor.

**Class 2 - Native-grain rich analytical sources.** Readiness rows, findings, prediction detail, evidence, contributors, similarity neighbours, practice matches, value derivation, remediation eligibility. **These retain their governed native multi-column shape and are never flattened into a single value column.**

Both classes are ordinary analytical sources. Both use the same registry, the same authoring shell, the same selection and filter contract where applicable, the same result envelope, the same widget system and the same evidence rules.

#### A dataset may register both classes

The class is a property of the **registered source**, not of the underlying data. One dataset may expose a native-rich source and an aggregate projection, and the customer picks in the authoring shell.

| Dataset | Native-rich source | Aggregate projection |
|---|---|---|
| Prediction | Yes, prediction detail | Yes: mean probability, subject count by dimension |
| Contributors | Yes | Yes: mean absolute contribution by feature |
| Similarity | Yes | Yes: neighbour count by outcome class |
| Anomaly | Yes | Yes: mean novelty score, count by position |
| Envelope | Yes, bounds are multi-column | Yes: population and outcome rate by parameter |
| Finding and effect | Yes | Yes: finding count by status and claim class |
| Readiness | Yes | Count only |

**`registry_dimensions.is_controllable`, `controllable_at_stages`, `adjustment_range`** are the three columns check 1 of the remediation gate reads. The product never assumes a measured parameter can be changed.

---

### LB-A46. TEXT AND IMAGE - FINAL

**State: `INTERFACE-DESIGNED / FUTURE IMPLEMENTATION`.** Ch4 5.8.6 and 5.8.7 specify both fully: persistence, access control, indexing, language handling, extraction, evidence citation, annotation, retention, permission and training separation.

**The boundary is governance, not modality.** An ungoverned output entering a score is the hazard; the modality is not. A free-form LLM summary has no training snapshot, no held-out validation, no calibration, no drift monitor and no leakage control. An authored vision model has all five, which is why Ch4 5.8.7 registers it in `model_registry` under the same activation and drift rules as any other model.

**The boundary rule, as amended (C4-6):**

> **No free-form or model-generated output may become a feature, a score, a statistic or a value.** Text and images enter a learned result only through an explicitly authored model definition carrying the full training contract: a versioned immutable snapshot, declared leakage controls, held-out validation, a `model_registry` entry, calibration and drift monitoring. Retrieval-derived and LLM-derived content is evidence only: it may corroborate a deterministic result and may never originate one.

Section 29.2 clause 4 states the two paths: **Path A evidence modality**, retrieved and cited, never a feature; and **Path B governed multimodal ML**, the full training contract above, permitted to produce a learned output with a claim class. Images register in `model_registry` with `algorithm` naming the vision family and obey the same activation, retirement, drift and `overlap_rows = 0` rules.

---

### LB-A47. POOL TOPOLOGY - FINAL

**Six logical classes. The `ml` class resolves to three physical lanes.** Full contract in section 38.1.

| Class | Lane | Reserved | Pre-emptible | Rationale |
|---|---|---|---|---|
| `import` | - | no | yes | Network-bound, cheap on CPU |
| `projection` | - | no | yes | Write-heavy, contends on indexes |
| `analysis` | - | no | yes | Read-heavy, bounded by row caps |
| `ml` | `ml.training` | no | **yes** | Memory-heavy, long, checkpointed |
| `ml` | `ml.batch_scoring` | no | yes | Bulk, deadline-insensitive |
| `ml` | **`ml.online_scoring`** | **yes, B-02** | **no** | **Carries the actionable-deadline contract** |
| `report` | - | no | yes | Bursty, low priority |
| `interactive` | - | **yes** | no | The read path is never starved |

**Why online scoring is a separate reserved lane.** `predictions.actionable_deadline_utc` and `delivery_latency_seconds NOT NULL` exist in Chapter 3, and Chapter 4 5.8.8 makes actionable latency Core. A lane whose capacity can be consumed by training work cannot carry a latency guarantee, because the guarantee would depend on what else happened to be running.

**Scoring keeps the `latest-only` policy.** A queued scoring request for a subject is superseded by a newer one rather than both executing, because a stale prediction has no value. It now operates inside a lane that training cannot block.

Admission is by both predicates of section 38.1, using `compute_weight`. Job workers use a **separate pooler identity** from the interactive path, so batch work physically cannot exhaust interface connections. The canonical five-level degradation ladder governs, and **every level is announced**.

**Deployment.** `ppiq-worker` carries import, projection, analysis, report and `ml.batch_scoring`. `ppiq-ml-train` carries `ml.training`, GPU-capable and pre-emptible. **`ppiq-ml-online` carries `ml.online_scoring` only**, with the warm model cache, no training imports and no batch admission (amendment C6-1).
