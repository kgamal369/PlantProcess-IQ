# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

*Covers PPIQ.txt 5.1. Audience 5.8: the customer's advanced IT and software staff, and our developers taking hand-over. Voice 5.9: senior product owner and technical lead.*

---

# CHAPTER 4, PART 1 - THE ANALYSIS PAGE

The Qlik-class interactive workspace: features, components, widgets, style, standard, UI/UX, filters, dynamic interaction, add page, add widget, edit widget query, link data to widget, chart types and chart style, page layout, KPI.

---

## 4.1.1 Design goals and the benchmark

The analysis page is judged against Qlik Sense and equivalent professional platforms. **Not copied. Matched in quality, then exceeded where our evidence layer allows.**

| Goal | Test |
|---|---|
| **Associative, not filtered** | Clicking a value tells you what is *possible* and what is *excluded*, not merely what remains |
| **Composed, never coded** | Every page, widget, filter and KPI is authored from the interface and stored as a definition |
| **Evidence-grade** | Every figure resolves to its query, its population and its source rows |
| **Resilient at widget granularity** | One widget failing never blanks the page |
| **Professional at thirty seconds** | Consistent, dense, aligned, grouped, no raw machine values, predictable keys |

**Where we exceed the benchmark:** as-of timestamp and snapshot identity per widget; drill from any point to the source rows through the provenance path; the readiness state visible on any analytical surface; and the honest-abstain behaviour instead of a confident answer on insufficient data.

---

## 4.1.2 Page anatomy

Six regions, in logical order. Every region is optional except the grid and the selections bar.

```
+--------------------------------------------------------------------------+
| PAGE HEADER      sheet selector | as-of | edit toggle | save/reset layout |
+--------------------------------------------------------------------------+
| SELECTIONS BAR   always present. "No selections applied" when empty.      |
|                  chips: [Material: C-700394 x] [Defect: CRACK_LONG x]     |
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

## 4.1.3 The associative engine

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

The associative base is the joined canonical model reachable from the page's bound datasets, resolved along join paths J1 to J4 of Section 4.5.7. **Counts are returned with the states**, because a value with two rows behind it and a value with two hundred thousand should not look identical.

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

## 4.1.4 Filters

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

## 4.1.5 The widget catalogue

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

Dimension and measure lists are **derived from the canonical model and the customer's own mapping**, never a compiled set. A plant that needs to group by batch, recipe, tool number or ambient humidity registers it and it appears everywhere. See Section 4.5.5.

---

## 4.1.6 Chart style standard

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

## 4.1.7 Page layout

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

## 4.1.8 KPI design

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

## 4.1.9 Add page

| Step | Surface | Result |
|---|---|---|
| 1 | Page Builder, **Create page** | Name, code, audience roles |
| 2 | Grid opens empty | Empty state: "This page has no widgets yet", with Add widget inline |
| 3 | Add widgets (4.1.10) | Definitions persist as they are saved |
| 4 | Arrange, Save layout | `layout_json` written |
| 5 | Publish to audience | The page appears in navigation for its roles |

Quota is checked at step 1 against the F3 limits: at eighty percent the action warns, at one hundred percent it is disabled with the reason and the administrator named.

---

## 4.1.10 Add widget

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

## 4.1.11 Link data to a widget

Two binding modes. Both produce the same artifact class: a versioned widget definition.

### Catalogue binding - the simple path

Chart type, then dimension, then measure, each from `GET /analytics/dashboard/metadata`. **Dimension and measure hide themselves** for a chart type whose `supportsDimension` or `supportsMeasure` is false. The form refuses only when neither is chosen.

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

## 4.1.12 Edit widget query

**Edit opens the same shell, with the definition already loaded.** There is no separate edit path and no second door.

| Rule | Reason |
|---|---|
| The shell opens carrying the current definition | The user edits what is there rather than starting again |
| Saving creates a new version | The previous version is recoverable |
| Changing the binding **requires re-mapping the axes** if the returned columns changed | A widget whose axes silently point at gone columns is worse than an error |
| **Retitle when you repoint** | The title field is highlighted when the binding changes; a widget whose title says one thing while it plots another is worse than a broken one |
| Preview before save is mandatory for query mode | A query that returns nothing is caught by the author, not the audience |

---

## 4.1.13 Dynamic interaction

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

## 4.1.14 Performance envelope

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

## 4.1.15 Acceptance

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

*End of Chapter 4, Part 1.*
