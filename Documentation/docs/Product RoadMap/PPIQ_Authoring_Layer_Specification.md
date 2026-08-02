# PPIQ Authoring Layer - Design Specification

**Version 1.0 | 27 July 2026 | Author: Karim, SOU Industrial Software, Dusseldorf**

**Status: binding on the authoring layer. Intended for Part III of `PPIQ_Constitution_v3.md` as Specification D, superseding the shorter treatment in Part II.6.**

---

## HOW TO READ THIS

This specification covers four surfaces that are really one:

1. **The shared low-code shell** - schema browser, toolbox, mode toggle, debug log, SQL editor
2. **Adding a widget to a page**
3. **Editing an existing widget** - its query, its filters, its shape
4. **The three analysis pages** and the interactive workspace they live in

They are one because a user who learns any of them has learned all of them. That is not a convenience. It is the reason a plant engineer will use the product without training, and it is the single largest usability decision in the platform.

**Part I is the spine.** Everything else is an application of it. If you read only one part, read Part I, because it is the mechanism by which this product works at a steel mill, a tyre plant and a chemical batch line without a line of code changing.

---

# PART I - THE GENERICITY MECHANISM

## 1. The problem this solves

Every customer has a different plant, a different production line, different staff, different quality concerns and a different vocabulary. A product that ships with a filter called "Shift" has already assumed the customer runs shifts. A product that ships with a business purpose called "Downtime" has assumed downtime is how they think about loss.

**Any list of choices compiled into the product is a guess about the customer.** Some guesses are right for the customer you built it against and wrong for the next one, and the failure is silent: the product works, the customer just cannot express what they actually care about.

So the rule is not "avoid hardcoding" as hygiene. It is structural:

> **No authoring surface may contain a list. Every list is read from a registry the customer's own data populated.**

## 2. The three doors, and there is no fourth

Knowledge about a specific plant enters the product through exactly three doors:

| Door | What enters | Who opens it |
|---|---|---|
| **Import** | The customer's rows, tables, columns, keys, defect names, parameter names, equipment names | The customer, through the DB-link and staging pipeline |
| **Registry** | What the product will let you SELECT: which columns are dimensions, which are measures, which can be filtered, which chart types accept which | Derived from imported data plus explicit registration |
| **Authoring** | What the customer BUILDS from those: preparations, widgets, pages, filters, analyses, rules | The customer, through the surfaces in this document |

**There is no fourth door.** No configuration file that names a defect type. No enum of shifts. No seeded catalogue of equipment. If it is specific to a plant and it did not come through one of these three, it is a defect.

## 3. What a registry actually is here

The product already publishes one. `GET /analytics/dashboard/metadata` returns:

```
DashboardMetadata {
  dimensions[]        code, label, category, dataType,
                      requiresParameterCode, compatibleChartTypes[]
  measures[]          code, label, category, aggregation, unit,
                      requiresParameterCode, compatibleChartTypes[]
  chartTypes[]        code, label, category, supportsDimension,
                      supportsMeasure, supportsMultipleSeries,
                      supportsParameterSelection
  filters[]           code, label, category, dataType,
                      operatorMode, isRequired, sourceCatalog
  purposes[]          code, label, description,
                      recommendedDimensions[], recommendedMeasures[],
                      recommendedChartTypes[]
  compatibilityRules[] dimensionCode + measureCode -> allowedChartTypes[]
  safetyLimits        defaultMaxRows, absoluteMaxRows,
                      defaultRawRowLimit, absoluteRawRowLimit,
                      defaultLookbackDays, absoluteLookbackDays
}
```

And `GET /analytics/dashboard/reference-data` returns the VALUES behind those filters.

**This is the whole genericity mechanism.** Every dropdown in every authoring surface is a projection of this object. When a plant imports a table with a column the product has never seen, that column appears as a dimension, and every surface offers it, and nothing was rebuilt.

**The corollary, which is the acceptance test:** if you can point at a business word in the source code of any authoring surface - a defect name, a shift, an equipment class, a grade - the surface is broken, regardless of whether it works.

## 4. The four kinds of hardcoding, and which are permitted

Not all fixed lists are violations. The distinction matters because over-applying the rule produces a product that cannot validate anything.

| Kind | Example | Verdict |
|---|---|---|
| **Plant vocabulary** | `"shift"`, `"defectType"`, `"riskClass"`, `"heat"` | **FORBIDDEN.** This is the customer's world, not the product's |
| **Grammar and operators** | `= <> > >= < <= LIKE IS NULL`, `+ - * /` | **REQUIRED to be fixed.** These are the product's own language and they must be a closed whitelist, because that is what makes the surface safe |
| **Structural categories** | `dimension`, `measure`, `filter`, `chart type` | **PERMITTED.** These are the shape of analysis itself, not of any industry |
| **Presentation tokens** | colours, spacing, typography | **PERMITTED and required to be fixed.** A design system is not customer knowledge |

**The test, in one sentence:** could a different plant reasonably need a different value here? If yes, it comes from the registry. If no - if changing it would break the product's own contract - it is fixed and closed.

`>` means greater than at every plant on earth. `defectType` does not mean anything at a plant that tracks deviations instead of defects.

## 5. The genericity failure modes seen in this codebase

Recorded because they are the shapes this failure actually takes, and none of them looks like hardcoding at first glance.

| Observed | Why it is a violation | The fix shape |
|---|---|---|
| A widget wizard offering six fixed "business purposes" | Quality, Productivity, Downtime, Risk are one industry's framing | Read `metadata.purposes` |
| A filter step with eight fixed categories, whose VALUES came from the server | The values were generic; the CATEGORY SET was closed. A plant needing a filter on batch or recipe cannot add one | Read `metadata.filters`; storage must be open-ended |
| An expression grammar whose allowed-key list contains `defect`, `shift`, `material` | Plant vocabulary compiled into the product's own language | Validate keys against the registry, not a static set |
| A canonical grain named `coil` carrying tyre units and chemical batches | One industry's product form naming another's | Rename to an industry-neutral identifier; keep `native_grain` as evidence |
| A default query example naming a specific view with a phase code | A specific artefact baked into the product as a sample | Use no example, or generate one from the registry |

**Note the pattern:** in four of five, the DATA was generic and the STRUCTURE was not. Genericity is a property of the shape, not of the contents.

## 6. What must be fixed for the product to be safe

The inverse rule, equally binding. These are closed sets, and opening them would be the defect:

- **Operator whitelists.** A filter operator outside `= <> > >= < <= LIKE, NOT LIKE, IS NULL, IS NOT NULL` is refused by name. Arithmetic outside `+ - * /` is refused by name
- **Identifier grammar.** Schema, table and column identifiers match `^[a-zA-Z0-9_]+$` and are quoted on emit
- **Values are never text in a statement.** Every literal is a bound parameter
- **Row and time ceilings** come from `safetyLimits` and cannot be exceeded by an authored query
- **Read-only enforcement** at the execution layer, not by convention

**The interface must not be able to offer a choice the server would refuse.** Where the client shows an operator list, that list is byte-identical to the server's whitelist, and a test asserts they cannot drift. Illegal states unreachable beats illegal states rejected.

---

# PART II - THE SHARED LOW-CODE SHELL

## 7. The ruling

There is **one authoring shell**. It is not five surfaces that resemble each other. It is one surface whose panels, palette, board semantics and validator are parameterised by which of five purposes it was opened for.

| Purpose | What is authored | Palette presented |
|---|---|---|
| **S1 Data preparation** | Staged data filtered, joined, aliased and mapped into the plant schema | Relational transform blocks |
| **S2 Widget binding** | The dataset a widget displays | Relational plus aggregation blocks |
| **S3 Analysis authoring** | Correlation, statistics, mathematics. Example: relate one hundred parameters in one block, save it, chart the result | Statistical and correlation method blocks |
| **S4 Model authoring** | Model-based analyses over the same canonical data | Model and feature blocks |
| **S5 Plant data log** | Rules emitting info, warning and error entries | Condition and action blocks |

**A user who learns the shell once has learned every authoring act in the product.** That is the design goal and everything below serves it.

## 8. Layout - and the one decision to settle

### 8.1 The four regions

```
+----------------------------------------------------------+
|  MODE BAR        [ Block wiring ] [ SQL ]        purpose  |
+------------+---------------------------+-----------------+
|            |                           |                 |
|  SCHEMA    |         BOARD             |    TOOLBOX      |
|  BROWSER   |     or SQL EDITOR         |    (palette)    |
|            |                           |                 |
|  schemas   |   drag from either side   |  grouped        |
|   tables   |   wire, run, save         |  searchable     |
|    columns |                           |  collapsible    |
|            |                           |                 |
+------------+---------------------------+-----------------+
|  DEBUG LOG      error / warning / success with a sentence |
+----------------------------------------------------------+
```

### 8.2 Which side is which

`rules.txt` places the schema browser on the **left** and the toolbox on the **right**. A later description reverses them. **This specification settles it as: schema browser LEFT, toolbox RIGHT**, for three reasons:

1. It matches the majority of the written specification and the surface already built
2. Reading order in a left-to-right language runs data first, then what you do to it - subject before verb
3. It matches every database tool the customer's engineer already knows

**And it is mirrored, not fixed.** The product carries an Arabic toggle. In a right-to-left locale the whole shell mirrors: schema browser right, toolbox left. This is a CSS logical-property concern (`inline-start` / `inline-end`), not two layouts.

**Consequence:** never write `left` or `right` in a component name, a class name or a prop. Write `schema-panel` and `tool-panel`. A name that encodes a side is wrong in half the world's languages.

## 9. The schema browser

### 9.1 Three levels, each unfolding

```
v  SCHEMA
   v  TABLE                              15 cols / 6 key
        piece_id          KEY   text
        heat_no           KEY   text
        width_mm                numeric
        cut_time                timestamptz
   >  TABLE                               9 cols / 3 key
>  SCHEMA
```

**Level 1 schemas.** For S1 only, two groups are shown: the staging shapes and the plant schema, because moving data between them is S1's entire purpose. For S2 to S5, one group: the prepared model.

**Level 2 tables.** Each shows a column count and a **key-candidate count**. That second number is the most useful thing on the panel - an engineer scanning for "what can I join on" reads it without opening anything.

**Level 3 attributes.** Every column with its SQL type. Key candidates carry a visible marker.

### 9.2 What the panel must never show

**Only what the customer owns.** Emulated-source schemas, staging residue, platform infrastructure, migration tables and the product's own metadata are never listed. A schema named after a source emulator appearing in a customer's tree is a Rule 1 violation the customer can see with their own eyes.

This is enforced by configuration, not by a filter list: the panel reads the schemas the connection profile declares as customer-visible.

### 9.3 Interaction

- Drag a **table** to the board - it becomes a node with all its columns as typed ports
- Drag a **single column** - it becomes a node scoped to that column, or attaches to a block's input if dropped on one
- Drag **a multi-selection of columns** - one node with only those columns
- Double-click a table - same as dragging it to a free position
- Search filters all three levels and keeps ancestors of matches visible

**Everything unfolds closed.** Ten tables must not become a wall of text on open.

## 10. The toolbox

### 10.1 Structure

Groups, collapsible, searchable, each block draggable to the board. **Groups and their contents come from a registry**, never from a code branch. Adding a statistical method to the product means adding a registry entry, not editing a component.

| Group | Contents | Available on |
|---|---|---|
| **Data** | Dataset, Join, Union | S1, S2 |
| **Shape** | Filter, Derive column, Group by, Sort, Limit | S1, S2, S3 |
| **Arithmetic and logic** | `+ - * /`, `= <> > >= < <=`, `AND OR NOT` | inside expression editors on every surface |
| **Statistics and correlation** | correlation, distribution, regression, outlier, capability - registry-driven | S3 |
| **Model** | feature selection, model type, training window, evaluation | S4 |
| **Condition and action** | threshold, transition, absence, emit info / warning / error | S5 |
| **Flow** | if / else, and nested conditions | inside expression editors |

### 10.2 The rule that decides where a block lives

**This is the design decision that keeps the surface unambiguous, and it was settled deliberately.**

> A wire on the board carries a **dataset**. A wire inside an expression editor carries a **value**.

Therefore:

- **Board blocks** are things that take datasets and produce datasets: Dataset, Join, Filter, Derive, Group by, Sort, and on S3 and S4 the method blocks
- **Expression blocks** are things that take values and produce values: arithmetic, comparison, logic, if / else. They live **inside** a board block, opened by double-clicking it

**Why this matters more than it looks.** Consider:

```
if  Schema1.Table2.temperature  >  Schema2.Table4.setpoint
```

Those are two different tables. Fifty thousand rows and thirty thousand rows. **Which row is compared with which?** There is no answer until a join declares the correspondence. A board that lets a comparison float beside two unjoined tables has drawn a question with no answer, and the rule "an illegal wire must show an error" becomes undefinable, because almost any combination is arguable.

Put the comparison **inside** the Filter it belongs to and every wire on the board has exactly one meaning. **The non-programmer still drags coloured blocks and never writes a character** - which is the entire point of the block mode. The comparison simply sits where it belongs.

### 10.3 The join is declared once

On **S1**, relationships between tables are created. That is S1's purpose: *the customer knows his own plant, and the vendor cannot know the schema architecture of every customer's plant.*

On **S2 to S5**, the relationships already exist. There is one prepared dataset and one row context, so a comparison between two columns has exactly one meaning and the board may carry values directly.

**So board semantics follow purpose** - exactly as the palette and the schema panel already do. One shell, three things that vary.

### 10.4 Flow control is not a board block

`for`, `while`, `loop` belong to neither board. A saved definition describes **what its output is**. How often it runs, over what window, and in what order relative to other definitions belongs to the **job** that carries it.

A transform graph is declarative. Putting iteration on it turns a description into a program and loses every property that made it safe.

## 11. The mode bar

Always present, always exactly two modes.

| Mode | Intended user | Why it exists |
|---|---|---|
| **Block wiring** | A plant user with no software experience | The authoring act must be achievable without writing a line of anything |
| **SQL** | A user with database experience | The long tail a palette will never cover must not become a support ticket |

**Neither is a lesser citizen.** Both produce the same artefact class: a named, versioned, saved definition. **Neither may be gated to a licence tier the other is not** - selling the block mode and charging for SQL would mean charging the customer for the product's own limitation.

### 11.1 The switching contract

- **Visual to SQL is always available and deterministic.** The graph compiles to SQL and is viewable at any time. This is a **view**, not a switch, and it never alters the definition
- **SQL is a first-class authoring mode.** Written directly, validated by parse, schema inference and a sampled dry run, producing the same artefact with the same versioning
- **Switching a visual definition INTO SQL authoring forks it.** The generated SQL is handed to the editor, the graph detaches and is retained as read-only history, and the user is warned before the switch that this direction is one-way. **The graph is never silently discarded**
- **SQL to visual reconstruction is best-effort and bounded.** Offered only for the parseable subset - simple select, filter, join, group, sort. For window functions, vendor syntax, correlated subqueries or CTEs the tool stays in SQL mode and says so in the debug log

**The product does not pretend a full round trip exists.** Claiming one and then losing a user's work is worse than stating the limit at the moment of the switch.

## 12. The SQL editor

When SQL mode is selected:

1. **The toolbox is hidden entirely**, not disabled. There is nothing to drag
2. **The schema browser stays.** A SQL author needs it more than a block author does
3. **The debug log stays**, now describing the SQL
4. **The board becomes the editor** - monospace, line numbers, syntax highlight
5. **Run** executes against a bounded dry run and shows returned rows below
6. **Save** produces a named, versioned definition of the same class

### 12.1 What makes it safe

An editor that accepts arbitrary SQL against the plant database is a security surface, not a text box. Every one of these is required before it ships:

| Control | Rule |
|---|---|
| Statement shape | Exactly one statement, and it must be a SELECT. Anything else refused by name |
| Identity | Executed as a read-only role, enforced at the database, not by inspection |
| Row ceiling | `safetyLimits.absoluteMaxRows`, applied by the server regardless of any LIMIT written |
| Time ceiling | `safetyLimits.absoluteLookbackDays` |
| Statement timeout | Set on the connection |
| Schema scope | Only schemas the connection profile declares customer-visible |
| Refusal record | Every rejection persisted with its reason, the same way a rejected dry run already is |

**A refusal must name what it refused and why**, in a sentence. "Invalid query" is not acceptable output from a product that sells honesty.

## 13. The debug log

Always present. Three severities, each carrying a **written description**.

| Severity | Meaning | Must state |
|---|---|---|
| **Error** | The definition cannot run | Which block or wire, which rule was broken, what would fix it |
| **Warning** | It will run but the result may surprise | What the risk is, in plain language |
| **Success** | It ran | Rows returned, columns, cost estimate |

**This is the surface through which a non-programmer learns the tool.** A red outline with no sentence beside it is a failure of this specification, not a minor styling gap.

Examples of the standard required:

- *"`hsm_coils` has no join to the graph. Wire one of its key columns to a key column on a table already on the board."*
- *"Operator `IN` is not permitted in a filter. Permitted: = &lt;&gt; &gt; &gt;= &lt; &lt;= LIKE, NOT LIKE, IS NULL, IS NOT NULL."*
- *"Comparing `cast_pieces.superheat_c` with `hsm_coils.entry_temp` requires a join between those tables. Without one there is no row correspondence."*
- *"Returned 2,441 rows in 340 ms. Estimated cost: light."*

## 14. Illegal wiring is refused at drag time

**This is the clause that separates a professional tool from a toy.**

A wire that is not legal is **rejected as it is drawn**, with a stated reason in the debug log. Never silently accepted. Never allowed to fail later at run time.

**On S1**, a wire is illegal when:
- a dataset is connected to a value input, or the reverse
- a required input is unconnected
- the graph contains a cycle
- **two tables are compared with no join declared between them** - the mistake a plant engineer will actually make, and the message must name it

**On S2 to S5**, a wire is illegal when:
- a column type does not accept the operation - text into arithmetic
- a referenced column is not present in the prepared dataset
- an aggregate is used outside an aggregation context

Both sets are enumerable, both are checkable at drag time, and both produce a sentence in the debug log.

**Port typing is the mechanism.** Ports carry a type - key, number, text, date, dataset - and the type decides legality. Colour communicates the type to the user; it does not enforce anything. **A product where the colours are decoration and every wire is accepted has a typed-port diagram that lies.**

---

# PART III - WIDGETS: ADDING, BINDING, EDITING

## 15. Adding a widget

### 15.1 The control and what it opens

**Add widget** sits in the workspace header beside Save layout and Refresh. Pressing it opens a **side toolbox bar** from which the user first chooses **which kind of widget**, before anything else is asked.

**The kind comes first because it determines every subsequent question.** A calendar filter has no measure. A calculated label has no dimension. Asking for a chart type before knowing the kind is the wizard mistake.

### 15.2 The widget kinds

| Kind | What it is | Binds to | Notes |
|---|---|---|---|
| **Chart** | Any visual from the chart catalogue | a query result | Kind is fixed; the chart TYPE is switchable afterwards on the card |
| **Table** | Rows and columns | a query result | Sortable, paged, exportable |
| **Calculated label** | One number with a caption | a query result reduced to a scalar | The KPI tile. Aggregation declared, not guessed |
| **Calendar filter** | A date-range control | a date column | Publishes a selection to the page |
| **Filter** | A selector over any authored condition | a WHERE condition | **See 15.3 - this is the important one** |
| **Text and note** | Static commentary | nothing | Explanation and headings. Deliberately included: a dashboard nobody can annotate is a dashboard nobody trusts |

**The list of kinds is one of the few permitted fixed lists** - a kind is a structural category, not plant vocabulary. But **the chart catalogue inside the Chart kind comes from `metadata.chartTypes`.**

### 15.3 Filters are a widget kind, and this is the crux

A filter is a **WHERE condition**. Every plant needs different ones. A product that ships a fixed filter bar has assumed the customer's concerns.

So:

- A filter is **authored**, through the same shell, in block mode or SQL
- It is **saved as a definition** like any other artefact, named and versioned
- It can be **simple** - one column, one operator, one value - or **complex** - combined conditions, a subquery, a derived condition
- It is **placed on a page** as a widget, so different pages can carry different filter sets
- It **publishes a selection** to the page, which every other widget composes with

**What this replaces:** a fixed row of dropdowns labelled Site, Area, Equipment, Defect, Shift. Those are one plant's concerns. A plant that filters by recipe, or by batch, or by tool number, or by ambient humidity, can express it here and could not before.

**What must be true underneath for this to work:** filter storage cannot be a fixed set of typed keys. It must carry an authored condition. A closed key set makes the surface a lie no matter how the front end looks.

### 15.4 The add sequence

```
Add widget
  -> choose kind                    (structural, fixed list)
  -> name it
  -> choose binding mode            (catalogue or query)
     -> catalogue: chart type, dimension, measure, all from metadata,
                   lists narrowing by declared compatibility
     -> query:     write, run, inspect returned columns,
                   then map columns to the widget's roles
  -> optional saved filters         (this widget's permanent scope)
  -> preview
  -> add to page
```

**Every step is skippable except kind and name.** A widget with a query and no catalogue selection is valid. A widget with a catalogue selection and no query is valid. **A wizard that forces six steps for a two-step task is why the previous one was replaced.**

## 16. Binding - the two modes, and which is primary

### 16.1 The inversion

**Dimension and measure are not the primary binding mechanism. They are the simple path.**

The general path is a **query**, and dimension and measure become *which column of the query result plays which role*.

Consider what a real customer asks for:

- a customised correlation table
- production per shift
- consumption of one specific piece of equipment per shift
- a chart of the correlation between line speed and defect rate

**Not one of these is expressible as a dimension plus a measure from a catalogue.** They are queries. The catalogue path covers the common case and is worth having; it must not be mistaken for the mechanism.

### 16.2 Catalogue mode

For the common case. Every list from `metadata`:

- **Chart type** from `chartTypes`
- **Dimension** from `dimensions`, filtered by the chosen chart's `compatibleChartTypes`
- **Measure** from `measures`, filtered the same way
- **Parameter** shown only when the chosen dimension or measure declares `requiresParameterCode`
- **Both dimension and measure are optional**, and each is hidden entirely when the chart type declares it does not support one. Refuse only when neither is chosen, because then the widget has nothing to show

**Compatibility is declared by the server, not inferred by the client.** Choosing a chart narrows the other lists. This is how a user avoids an invalid combination without being told off for making one.

### 16.3 Query mode

```
source: v_prepared_view
dimension: shift_code
measure: avg(line_speed_mpm) as avg_speed
measure: count(*) as pieces
filter: entry_temp_c > 900
sort: avg_speed DESC
limit: 200
timeWindow: observed_at_utc last-30-days
```

**Write, run, inspect, then bind.** The returned columns become the choices for the widget's roles:

```
Returned columns:  shift_code (text)   avg_speed (numeric)   pieces (integer)

  Category axis  ->  shift_code
  Value          ->  avg_speed
  Secondary      ->  pieces
```

**This is the step that makes the loop complete.** Running proves the query. Inspecting shows what it returns. Binding maps returns to roles. Saving stores the expression on the widget so the chart renders from it forever after.

**Grammar properties that must hold:**
- Measures parse **aggregate, column and alias** - `avg(x) as y`, not a single token
- Dimensions repeat - more than one grouping column
- Filters parse **column, operator, value** against the whitelist
- Sort, limit and time window are first-class, not afterthoughts
- Failures are **typed**: unknown keyword, missing value, type mismatch, invalid grammar - each naming what it did not accept

**SQL mode is the same loop with SQL instead of the DSL**, subject to the controls in section 12.1.

## 17. Editing an existing widget

### 17.1 The rule

**Edit opens the same surface, carrying the widget's current definition already loaded.**

Not a different dialogue. Not a subset. Not a rename box. **The same panel**, so that the user who built it can change it with the knowledge they already have, and the user who inherited it can read how it was built.

### 17.2 What must be editable

Everything that was authorable:

- the title and the widget kind
- the binding mode - **and switching catalogue to query is permitted**, carrying the catalogue selection in as a starting query
- the chart type, dimension, measure, parameter
- the query text, re-runnable and re-inspectable before saving
- the saved filters
- the column-to-role mapping

### 17.3 Versioning

Saving an edit produces a **new version**, not an overwrite. The previous definition stays retrievable. This is the same contract the preparation canvas already has, and for the same reason: a customer who breaks a dashboard at 2 a.m. must be able to go back.

### 17.4 Where the entry lives

On the widget card's action menu, beside Duplicate, Hide and Remove. **Edit and Rename are different actions and must not share a handler** - a card whose Edit renames is a control that lies about itself.

## 18. Filter composition - saved scope versus live selection

Two different things, and a user cannot guess which wins unless told.

| | What it is | Where it lives |
|---|---|---|
| **The widget's saved filter** | A **permanent scope**. "This chart always shows line 3" | In the widget definition |
| **The live selection** | The page filter bar, and clicks on other widgets | In the session |

**They compose with AND.** The saved filter is the narrower permanent boundary; a click elsewhere narrows further **inside** it. Leave the saved filters empty and the widget follows the page alone.

**This must be stated in the authoring surface itself**, where the user is making the choice - not in documentation nobody reads.

---

# PART IV - THE ANALYSIS PAGES AND THE INTERACTIVE WORKSPACE

## 19. The three page types are descriptions, not objects

A page is a **dashboard definition**: a name, a layout, and a set of widgets. There is no page-type object, no template class, no type-specific behaviour.

"Type 1 raw data analysis", "type 2 correlation and statistics", "type 3 AI and ML" describe **what the data inside is about**. A customer may have 25 of the first, 18 of the second and 12 of the third; they are 55 dashboard definitions.

**Why this matters:** the moment a page type becomes an object, it acquires type-specific behaviour, and type-specific behaviour is where plant assumptions hide. A page that "knows" it is a quality page will eventually assume what quality means.

**What is legitimately different between them** is the widgets the customer put on them and the analyses those widgets bind to. Nothing structural.

## 20. Qlik-class behaviour - what it means concretely

The workspace is not a set of charts on a grid. Six behaviours make it an analysis surface, and all six are specified because partial implementations of this are worse than none.

### 20.1 Associative selection - the tri-state

Click any value anywhere and the whole page re-evaluates. **Every other value in every other widget takes one of three states:**

| State | Meaning | Presentation |
|---|---|---|
| **Selected** | The user chose it | Strong, high contrast |
| **Possible** | Compatible with the current selection | Normal |
| **Excluded** | Cannot co-occur with the current selection | De-emphasised, and **still selectable** |

**Excluded values must remain visible and clickable.** That is the whole point, and it is what separates this from filtering. Seeing that a defect class is *impossible* for the selected material is an insight. Hiding it destroys the insight and leaves the user thinking the value does not exist.

**This is the product's strongest analytical differentiator** and it is already implemented in the associative panel. The requirement is that the same three states appear **inside every filter control and every chart**, not only in a dedicated panel.

### 20.2 Selection state as a first-class object

- Visible as a **breadcrumb** showing every active selection in order
- Each **individually removable**
- **Undo and redo** across selection steps
- **Clear all** as one action
- **Saveable** as a named investigation trail, so a root-cause session can be retraced and shared

### 20.3 Cross-filtering from any visual

Clicking a bar, a slice, a heatmap cell, a table row or a scatter point **applies that value as a selection**. Not a drill-down, not a modal - a selection, exactly as if it had been chosen in a filter.

**Every visual is an input device.** A chart you can only look at is a report, not an analysis surface.

### 20.4 Object-level scope

Each widget may carry its own permanent scope, composing with the live selection as specified in section 18. This is the equivalent of set analysis: a chart that always shows the previous year regardless of the current selection, sitting next to one that follows it.

### 20.5 Drill-down and the evidence path

From any point on any visual, reach **the rows behind it**. From those rows, reach the material, the genealogy, the source system and the import batch.

**The chain from a chart pixel to a source row must be unbroken.** A number a customer cannot trace to its origin is a number they will not act on, and the readiness-gate story is worthless without it.

### 20.6 Alternate states

Two selections held simultaneously for comparison - this month against last, line A against line B - with widgets bindable to either. Advanced, and correctly deferred, but the selection model must not make it impossible to add later.

## 21. Page layout

- **Grid based**, drag to move, edge and corner to resize
- **Layout persists per user per dashboard**. Two users may arrange the same dashboard differently
- **Reset layout** returns to the definition's default
- Widgets: **maximise** to full row, **fullscreen** as a real overlay, **collapse** to title, **hide** without deleting
- **Responsive**: the grid reflows at narrow widths rather than clipping

**Every one of these controls must work.** A widget card carrying six action icons where two do nothing is exactly the lens-one failure the audit concept exists to catch, and a customer will find it inside a minute.

## 22. The chart catalogue

Every type is a **registry entry**, never a code branch. Adding one means registering it with its capabilities.

| Type | Uses dimension | Uses measure | Selection unit |
|---|---|---|---|
| KPI / label | no | yes | none |
| Bar, column | yes | yes | a bar |
| Line, area | yes, ordered | yes | a point |
| Pie, donut | yes, low cardinality | yes | a slice |
| Table | yes, many | yes, many | a row or a cell |
| Heatmap | two | yes | a cell |
| Scatter | numeric x | numeric y | a point |
| Pareto | yes | yes plus cumulative | a bar |
| Box plot | yes | distribution | a box |
| Gauge | no | yes plus target | none |
| Waterfall | yes | signed | a step |
| Combo | yes | two measures | either series |

**Each entry declares:** which roles it needs, which measure aggregations it accepts, its selection contract, and its cardinality guidance.

**Cardinality guidance is not cosmetic.** A pie chart with sixty daily slices is not a chart, it is a colour wheel. When a chosen dimension exceeds a type's sensible cardinality, the surface **warns before rendering** and suggests a type that suits it. The warning names the number: *"This dimension returns 74 values. A pie chart is readable to about 8. A bar or line chart will show this better."*

## 23. The filter bar

**Composed of authored filter widgets** - section 15.3 - not a fixed row.

- Each shows the tri-state on its values
- Each is removable and reorderable by the page's author
- The bar collapses to a summary line when scrolled
- A time-range control is a filter widget like any other

**A plant that filters by recipe puts a recipe filter here. A plant that filters by tool number puts a tool filter here. Neither required a release.**

---

# PART V - DESIGN, STYLE AND UI/UX

## 24. The design language

**Dark Industrial.** Not a dark theme applied to a light product - a control-room aesthetic, because that is the room this software is used in and the eyes it is used by.

| Token | Value | Use |
|---|---|---|
| Surface | `#0b1730` | Panel background |
| Surface raised | `#102a43` | Cards, headers, rows |
| Accent | `#00d4ff` | Interaction, focus, active state |
| Text primary | `#eaf6ff` | Headings, values |
| Text secondary | `#c9dcec` | Body |
| Text muted | `#8ba9c4` | Captions, hints, metadata |
| Ready | `#2ce6a2` | Gate green, possible values, success |
| Partial | `#ffb020` | Warning, partial readiness |
| Blocked | `#ff4d6d` | Error, blocked gate, refusal |

**Typography.** Display and headings: Chakra Petch, a squared industrial face. Body: system sans. **Numbers, identifiers, SQL and grammar: IBM Plex Mono.**

**Monospace for data is not decoration.** An engineer comparing `heat_no` values or reading a compiled query needs digits and identifiers to align. This is a legibility requirement, not a style choice.

**Port colours** by type: key `#00d4ff`, number `#0a84ff`, text `#7aa7c7`, date `#b48cff`. **The colour communicates the type. The type enforces legality. Colour that does not correspond to enforcement is a lie in the interface.**

## 25. Style rules that are enforced by tests

These are not conventions. The repository fails a build on each.

| Rule | Test |
|---|---|
| No raw `<button>` or `<table>` where a Standard primitive exists | PPIQ-T11 |
| No raw `<input>`, `<select>`, `<textarea>`, `<label>` outside the primitive layer | UI ratchet D1 |
| No inline style objects | UI ratchet D2 |
| No raw load-failure strings outside the shared boundary | PPIQ-T09 |
| No phase or task tokens visible on any customer-facing surface | noPhaseTokensOnDemoPath |
| No encoding corruption | noMojibake |
| No thin re-export chains | noThinReExports |

**The ratchet is a baseline comparison, not a zero rule.** An existing file may carry its committed count; decreases pass; **any new file starts at zero.**

**Why a design-system ratchet exists at all:** a product priced at several thousand euro a month cannot have three different table styles on three pages. The primitives are the mechanism, and the ratchet is what stops the mechanism eroding one convenient exception at a time.

## 26. UI/UX principles

### 26.1 Illegal states unreachable, not merely rejected

The interface must not offer a choice the server would refuse. Operator lists come from the same whitelist the server enforces, and a test asserts they cannot drift.

**Rejecting a user's action is a worse experience than never offering it** - and it is worse engineering, because it means two sources of truth about the same rule.

### 26.2 Every refusal carries a sentence

An error state names what was refused, why, and what would satisfy it. This is the honesty contract applied to the interface.

**"Invalid input" is not acceptable output from a product that sells its willingness to say no.**

### 26.3 Progressive disclosure

Trees unfold closed. Advanced options are collapsed. The first screen of any surface shows the common case.

**But nothing is hidden that changes the meaning of what is shown.** A filter silently applied to a chart is a lie by omission; the chart states its scope.

### 26.4 The empty state is designed

Every surface has a designed empty state naming **what to do next**, not a blank area:

- *"No staged datasets. Register a source and run Stage-1 from the Importing Data area, then reopen this page."*
- *"Put a dataset on the board first."*
- *"This dataset has never cleared the readiness gate. Here is the dimension that blocks it."*

**An empty state is where a new user spends their first minute.** Treating it as an edge case is treating the first minute as an edge case.

### 26.5 Loading is bounded and honest

A spinner without progress after two seconds is replaced by something that names what is happening. A query that will be slow says so before it runs, using the cost estimate the server already returns.

### 26.6 Nothing on screen is decoration

If a control appears, it works. If a colour carries meaning, that meaning is enforced. If a badge shows a state, the state is real.

**A widget card with six action icons where two do nothing is a professional failure**, and the customer's engineer will find it in the first minute with the mouse.

### 26.7 Language and direction

The product carries an Arabic toggle. Every layout is expressed in logical properties, so the whole shell mirrors. **No component, class or prop name encodes a physical side.**

## 27. What "professional" means here, concretely

Karim's own criterion is that the product must look like something a large company pays several thousand euro a month for. That is not a vague aspiration; it decomposes:

| Property | Test |
|---|---|
| **Consistency** | The same control looks and behaves the same on every page |
| **Density** | Information-rich without crowding. An engineer scanning for a number finds it |
| **Grouping** | Long lists are grouped and collapsible, never a flat wall |
| **Alignment** | Shared baselines. Nothing half a line out |
| **No raw machine values** | No ISO timestamps, no UUIDs, no enum names, no status codes on a customer surface |
| **Responsive** | Nothing clips, overlaps or collapses below its content |
| **Predictable** | Escape closes, Enter submits, Back returns, reload preserves |

**The failure mode to watch for:** each of these is individually small and collectively decisive. A product can pass every functional test and still look like a prototype, and a buyer decides in the first thirty seconds.

---

# PART VI - ACCEPTANCE

## 28. How you know the authoring layer is generic

Run these against any surface in this document. **Each is a yes-or-no with no interpretation.**

| # | Test | Pass condition |
|---|---|---|
| 1 | Search the surface's source for business words - defect, shift, heat, coil, grade, batch, equipment class | **Zero occurrences** outside data flowing from the registry |
| 2 | Search for arrays of user-selectable options | **Zero.** Every list resolves to a metadata or registry call |
| 3 | Import a table with a column the product has never seen | It appears as a dimension and every surface offers it, with no code change |
| 4 | Point at any dropdown and ask where its contents came from | A registry call, traceable in the network panel |
| 5 | Author a filter on a category the product was not built with | It saves, it applies, it composes with the live selection |
| 6 | Deploy against a second industry's data | Every surface functions. Only labels differ, and they came from the data |
| 7 | Read every string a customer can see | None names a specific plant, industry, source system or dataset |

**Test 3 and test 5 are the ones that actually bite**, and they are the ones a demonstration to a sceptical engineer will exercise.

## 29. How you know the shell is complete

| Clause | Complete when |
|---|---|
| One shell, five purposes | The same component serves all five, differing only in palette, schema scope and validator |
| Two modes | Both present on every surface, neither licence-gated differently |
| Schema browser | Three levels unfold, columns typed, key candidates marked, customer-owned schemas only |
| Toolbox | Groups registry-driven, searchable, draggable, and expression blocks live inside board blocks |
| Debug log | Three severities, each carrying a sentence naming cause and remedy |
| SQL editor | Present, safe by the section 12.1 controls, saving a versioned artefact |
| Illegal wiring | Refused at drag time with a named reason, on every surface |
| Widget authoring | Add and edit through one surface, catalogue and query modes, filters authored not fixed |
| Workspace | Tri-state on every control, cross-filter from every visual, unbroken drill path to source rows |

## 30. The order to build in, and why

1. **Illegal wiring refusal.** Small, and it is what an engineer tests first. Until it exists, the typed ports are decoration
2. **The debug log.** Everything else is only usable if failures explain themselves
3. **Authored filters end to end**, including open-ended storage. This is the largest genericity gap and it blocks the whole filter-as-widget model
4. **Query binding saved and rendered.** Completes write-run-inspect-save-render, which is the whole customer story
5. **The toolbox palette.** Now worth building, because there are real blocks to hold
6. **Tri-state everywhere**, not only in the associative panel
7. **Alternate states.** Last, and only when the rest is solid

**The reason for this order:** items 1 and 2 make everything else diagnosable by the user. Building features on a surface that cannot explain its own refusals produces a tool people abandon rather than report.

---

*Specification written 27 July 2026, drawn from the requirements Karim stated across the 25 to 27 July session and grounded in the metadata surface the product already publishes. Every genericity claim in it is testable by the seven checks in section 28.*
