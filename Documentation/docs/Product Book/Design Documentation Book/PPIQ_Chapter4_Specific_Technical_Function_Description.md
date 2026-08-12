# PlantProcess IQ - Master Design Document

**Version 4.5 | Author: Karim, SOU Industrial Software, Dusseldorf** | **MASTER DESIGN FREEZE CANDIDATE**

> **Change log - Second-Order Consistency Pass (v4.4 to v4.5). MASTER DESIGN FREEZE CANDIDATE.** No product capability added. The remediation block is restated as producing **historically supported candidates**, with operational recommendation status decided solely by the per-prediction gate of 5.6.4d; the contradictory sentence calling a recommendation "an instruction to a plant" is replaced by the rule that the product produces evidence-backed recommendations for human decision and never sends a control instruction; the benchmark claim becomes a stated **engineering target** verified by the acceptance criteria rather than an assertion of parity; model loading, fallback approval and fallback condition 5 all use the full serving identity `(tenant_id, model_code, outcome_code, grain_code)`; and the decision boundary is restated so that Accept, Reject and Defer are gated identically by `can_accept`, with `RM10` protecting all three. See Chapter 3 v4.5 for the matching schema, payload and page corrections.

---

# CHAPTER 4 - SPECIFIC SOFTWARE PRODUCT TECHNICAL FUNCTION DESCRIPTION

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

## 5.5, 5.6 and 5.7 - STATISTICS, MACHINE LEARNING AND THE ASSISTANT

---

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
