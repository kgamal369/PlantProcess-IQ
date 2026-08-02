# PlantProcess IQ - Master Design Document

**Version 4.1 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

# CHAPTER 4 - SPECIFIC SOFTWARE PRODUCT TECHNICAL FUNCTION DESCRIPTION

*Maps to PPIQ.txt section 5. Audience (5.8): the customer's advanced IT and software staff, and our developers taking hand-over. Voice (5.9): senior product owner and technical lead.*

**One file per chapter.** This chapter was briefly split across four files; that split is retired and this is the single authoritative Chapter 4.

## Contents

| Part | Covers | PPIQ.txt |
|---|---|---|
| 5.1 | The analysis page: features, components, widgets, style, filters, dynamic interaction, add page, add widget, edit widget query, link data to widget, chart types and style, page layout, KPI | 5.1 |
| 5.2 | The no-code / low-code shell: layout, schema table bar, toolbox drag-and-drop, wiring diagram with debugging and saving, transfer to code, joining and use in analysis, SQL editor, predefined and advanced toolboxes | 5.2 |
| 5.3, 5.4 | Multi-threading and load balancing of jobs; the gate and the engine, its rules and validation, coefficient adjustment with the learning curve, how the assistant draws from it, the engine as hub | 5.3, 5.4 |
| 5.5-5.7 | Statistics and correlation blocks; AI and ML blocks; each with inputs, outputs, validation and best chart; the AI Assistant as a persistent dock | 5.5, 5.6, 5.7 |

---

---

## 5.1 THE ANALYSIS PAGE (PPIQ.txt 5.1)

The Qlik-class interactive workspace: features, components, widgets, style, standard, UI/UX, filters, dynamic interaction, add page, add widget, edit widget query, link data to widget, chart types and chart style, page layout, KPI.

---

## 5.1.1 Design goals and the benchmark

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

## 5.1.2 Page anatomy

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

The associative base is the joined canonical model reachable from the page's bound datasets, resolved along join paths J1 to J4 of Section 5.5.7. **Counts are returned with the states**, because a value with two rows behind it and a value with two hundred thousand should not look identical.

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

Every output is **a named, versioned, saved file**. Same lifecycle, same permissions model, same export format.

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
| schemas       |   [staging.hsm_coils] --+                   |  in SQL      |
|  > tables     |                          \                  |  mode)       |
|    > columns  |   [staging.parsytec]-----[Join]--[Map]      |              |
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

The edge on the board is labelled with the equality, for example `piece_id = material_id`, so the declaration is readable at a glance.

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

Run executes under the safe-SQL contract, bounded by the row limits. The result renders **below the editor** as a table with column names and inferred types, plus the row count and the elapsed time. If correct, **Save as a named file** creates a version.

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
        > SELECT coil_id, avg(speed) FROM hsm_coils WHERE speed >
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

**Weights.** Each job definition carries a `compute_weight` (default 1). A pool admits while `sum(weight of running) + weight(candidate) <= parallelism`. A model-training job weighted 4 occupies its pool alone. **This is why the weight is edited behind a confirmation** that states the resulting utilisation.

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
   Findings        Risk scores     Suggestions     Value ranges    Assistant
   (D4)            (D5)            (D6)            (D7)            (E1, read-only)
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

**Genealogy-attributed correlation is the block no business-intelligence tool has.** It is the mechanism behind the product's central claim: a melt parameter related to a coil defect, correctly weighted where a coil descends from two heats.

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

**The remediation block is the product's most valuable output and its most dangerous.** A recommendation to change a later-stage practice is an instruction to a plant. Three constraints follow, and each is enforced:

1. **A minimum historical support of 20 cases.** Below that the block reports insufficient support and recommends nothing.
2. **Evidence handles on every suggestion**, resolving to the historical cases that justify it.
3. **Human approval before anything is acted on.** The product suggests. It never instructs and never acts.


## 5.6.4a Group G2 - Practice learning (guideline 1.3.b)

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Practice reconstruction** | canonical dataset | context (grade family, route), parameter set, period bucketing | practice signature per period: the parameter combination and sequence in force | Parameters registered; periods non-overlapping | **Table** of signatures; timeline |
| **Practice-outcome linkage** | practice signatures, outcome dataset | outcome (productivity measure, downtime, defect class) | outcome rate per practice with support count and confidence | **Support >= 20 periods per practice**, else reported observed-but-unproven | **Forest plot**; bar with intervals |
| **Best-practice benchmark** | linkage output | - | best demonstrated practice per context, with evidence | Ranked by outcome with confidence, never by point estimate alone | **Card** with support; slope versus current |
| **Failure-practice linkage** | practice signatures, downtime and failure dataset | - | practices that preceded downtime and failures, with lead time | Same support rule | **Pareto** of failure-associated practices |
| **Drift detection** | current operation, benchmark | tolerance per parameter | drift per parameter against own best practice | Benchmark exists | **Slope chart**; control-chart style band |

These write `practice_statistics` (Chapter 3, 4.5.12) and feed the Practice Insights page and the remediation search of Group G. The support threshold and the observed-but-unproven state are the honesty contract applied to practice claims: **the plant's own best practice is a measured fact with a support count, never an anecdote promoted to a rule.**

## 5.6.5 Model governance

| Requirement | Rule |
|---|---|
| Registry | Every trained model is registered with version, algorithm, feature list, training window, split strategy, missing-value policy, scaling parameters and metrics |
| Reproducibility | A registered model can be retrained to the same result from the recorded definition |
| Drift monitoring | Feature distribution and performance monitored; drift beyond a threshold moves the model to a review state and **stops scoring** |
| Retirement | A retired model stops scoring; its historical scores remain readable and labelled with the retired version |
| Determinism | Scoring is deterministic. The same input and the same model version produce the same score |
| **No language model in the compute path** | Recorded as data on every result |

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

*End of Chapter 4.*
