# PPIQ Constitution - Amendment A1: The Shared Low-Code Authoring Shell

**Drafted 25 July 2026 | Origin: Karim's clarification of the M1 rule, 25-Jul session | Status: A1.7 resolved 25-Jul; ready for ratification**

Applies to `PPIQ_Constitution_v2.md`, replacing Part II.6 in full and extending Part III.14. Version bump to v2.1 on ratification.

This amendment exists because the original statement in `rules.txt` was compressed, and the compressed form lost the two most important properties of the surface: that **one shell serves all five pages**, and that **the same shell is what opens when a user binds data to a widget**.

---

## A1.1 - The ruling

There is **one authoring shell**. It is not five surfaces that resemble each other; it is one surface whose left panel, palette and validator are parameterised by which of the five authoring purposes it was opened for.

A user who learns the shell once has learned every authoring act in the product.

| Purpose | What is authored | Palette presented |
|---|---|---|
| **S1 Data preparation** | Staged data prepared, filtered, linked, aliased and mapped into the plant schema | Relational transform blocks |
| **S2 Widget and chart binding** | The dataset a widget displays: parameters, derived variables, grouping | Relational transform blocks plus aggregation |
| **S3 Analysis authoring** | Correlation, statistics, mathematical analysis. Example: relate one hundred parameters in one correlation block, save it, chart the result | Statistical and correlation method blocks |
| **S4 Machine-learning authoring** | Model-based analyses over the same canonical data | Model and feature blocks |
| **S5 Plant data log** | Rules that emit info, warning and error entries. Example: warn when a machine temperature exceeds a limit; error when a stage weight reaches zero; inform on a new chemical sample | Condition and action blocks |

## A1.2 - The two modes

A toggle sits at the top of every one of the five pages. It is always present and it always offers exactly two modes.

| Mode | Intended user | Rationale |
|---|---|---|
| **Block and wiring diagram** | A plant user with no software experience | The authoring act must be achievable without writing a line of anything |
| **SQL** | A user with some database experience | The long tail that a block palette will never cover must not become a support ticket |

Neither mode is a lesser citizen. Both produce the same artifact class: a named, versioned, saved definition.

## A1.3 - Mode one: block and wiring diagram

### Right panel: the block palette

Grouped, searchable, drag-and-drop onto the board. Groups include, and are extensible:

- **Arithmetic and logical**: addition, subtraction, comparison, `AND`, `OR`, `NOT`
- **Statistics, correlation and analysis functions**: the method toolbox, registry-driven
- **Control and flow**: `if / else`, `for`, `while`, `where` - authored as approachable nested blocks in the manner of a visual teaching language, not as text
- Further groups are added by registry entry, never by code branch

### Left panel: the schema browser

A three-level tree, each level unfolding:

1. **Schemas** - unfold to reveal
2. **Tables** - unfold to reveal
3. **Attributes** - each column with its type

The user drags **all attributes at once**, or selects and drags them **one by one**, onto the board.

For S1 only, the left panel presents **two** schema groups: the staging shapes and the plant schema, because S1's whole purpose is to move data between them.

### Centre: the board

The user drags parameters from the left, drags blocks from the right, and wires them together. Then **Run**. If the result is correct, **Save as a named file**.

### Bottom: the debug log

Always present. Three severities, each with a written description:

| Severity | Meaning | Must state |
|---|---|---|
| **Error** | The definition cannot run | Which block or wire, what rule was broken, what would fix it |
| **Warning** | It will run but the result may surprise | What the risk is, in plain language |
| **Success** | It ran | Rows returned, columns, and the cost estimate |

The log is the surface through which a non-programmer learns the tool. A bare red outline with no sentence is a failure of this specification.

## A1.4 - Mode two: SQL

1. **No right panel.** The palette is hidden entirely, not disabled.
2. **Left panel unchanged**: the same schema, table and attribute tree, because a SQL author needs it more than a block author does.
3. **Bottom debug log unchanged**: error, warning and success, each with a description, now describing the SQL rather than the wiring.
4. **The board becomes a SQL editor.** The user writes ordinary SQL.
5. **Run.** The returned rows are shown below. If correct, **Save as a named file**.

## A1.5 - Illegal wiring must be refused

**This is the clause that separates a professional tool from a toy, and it is currently the single largest defect in the built canvas.**

A wire that is not legal is **rejected at drag time**, with a stated reason in the debug log. It is never silently accepted and never allowed to fail later at run time.

Legality is decided by port type. A dataset may not be wired into a scalar input. A scalar may not be wired into a dataset input. A node's required inputs must all be connected before it is valid. Cycles are illegal.

**Measured status, 25 July 2026:** `VisualJoinCanvasPage` implements `onConnect` as an unconditional `addEdge`. There is no `isValidConnection`. **Every wire between every pair of ports is currently accepted.** The port colours are decoration, not enforcement. This clause is written, and not implemented.

## A1.6 - Widget authoring uses this shell

**This resolves the open question of how a user selects the dataset a widget displays.**

The shell opens, in S2 mode, in all three of these cases:

1. The user **edits an existing widget** on a page.
2. The user **drags a new widget** onto an existing page.
3. The user **creates a new empty page** and adds a widget to it.

In every case the shell opens carrying the widget's **current definition already loaded**, so the user edits what is there rather than starting from nothing. They may modify the existing SQL, write new SQL, or rewire the diagram. On save, the widget's definition is versioned like any other.

There is no separate widget-binding mechanism. There is no second way to select a dataset. **This shell is the only door**, exactly as the DB-link is the only door for plant data.

**Measured status, 25 July 2026:** no such surface exists. The only binding mechanism in the product is a plain text area accepting a key-value script, gated behind a higher licence tier and reachable from no button. The widget card's Edit control shares its handler with Rename. This clause is written, and not implemented.

## A1.7 - RESOLVED: board semantics differ by purpose, because the join happens once

**Resolved 25 July 2026 by Karim, from `rules.txt` Rule 4 step 6 and Rule 6 surface 1.**

The question was whether arithmetic and comparison blocks may sit on the same board as table blocks. The answer follows from a property of the journey that neither the question nor the earlier draft accounted for:

**The join is declared once, in S1, and never again.**

Rule 4 step 6.2 states it plainly: the user joins piece identity from the hot-strip-mill source with material identity from the surface-inspection source. Rule 6 surface 1 states why: *each user knows his own plant, and the vendor cannot know the schema architecture of every customer's plant.* Establishing those relationships is not a step inside every authoring act. It is the purpose of the first surface.

Therefore:

| Surface | What flows on the board | Consequence |
|---|---|---|
| **S1 Data preparation** | **Datasets.** Multiple source tables, joins between them, filters, mappings into the plant schema. This is where relationships between tables are created. | The board is a dataflow graph. A wire carries rows. Arithmetic and comparison are configuration inside a node, because two unrelated tables have no row correspondence until the join declares one. |
| **S2 to S5** | **One prepared dataset.** The relationships already exist, declared upstream in S1. There is a single row context. | A wire may carry a value. Comparison and arithmetic blocks sit on the board exactly as originally specified, because "this column against that column" now has one unambiguous meaning: within the same row of the prepared model. |

**This is not two products. It is one shell whose board semantics follow the purpose it was opened for** - which is the same principle as A1.1, where the palette and the left panel already vary by purpose. The board is simply the third thing that varies.

### What "illegal wiring" means, per surface

The A1.5 rule survives intact and becomes definable, which was the whole reason the question was asked:

**On S1**, a wire is illegal when: a dataset is connected to a value input or the reverse; a required input is unconnected; the graph contains a cycle; or two tables are compared with no join declared between them. That last one is the specific error the user must see named, because it is the mistake a plant engineer will actually make.

**On S2 to S5**, a wire is illegal when: a column type does not accept the operation, such as text into arithmetic; a referenced column is not in the prepared dataset; or an aggregate is used outside an aggregation context.

Both sets are enumerable, both are checkable at drag time, and both produce a written sentence in the debug log of A1.3.

### Control flow

`for` and `while` remain orchestration. A saved definition describes what its output is; how often it runs and over what window belongs to the job that carries it. This is unchanged and is not affected by the resolution above.

---

## A1.7-HISTORICAL - the question as originally posed

The specification above places arithmetic, logical and control-flow blocks on the same board as the relational blocks. Part II.6.2 of the constitution, drawn from a later passage of the same `rules.txt`, states the opposite: that those operate at a different granularity and must not share one wiring surface.

Both intentions are correct and they are not actually in conflict. The resolution is **two boards, one experience**:

| Board | Opened how | Wires carry | Blocks |
|---|---|---|---|
| **Outer: dataflow** | The page itself | A dataset: rows by typed columns | Source, filter, join, group-by, sort, output; and in S3 and S4, the method blocks |
| **Inner: expression** | Double-click any block that needs a value | A value | Arithmetic, comparison, `AND`, `OR`, `NOT`, `if / else` - nested, coloured, in the manner of a visual teaching language |

A non-programmer still builds everything by dragging coloured blocks and never writes a line, which is the whole intent of mode one. But a comparison block sits **inside** the filter it belongs to, rather than floating beside a table on the same surface, so "what does this wire mean" always has exactly one answer.

Control flow (`for`, `while`) belongs to neither board. It is orchestration: it governs how a saved definition is scheduled and repeated, and it lives on the job surface. A transform graph is declarative; it describes what the output is, not the order of steps taken to get there.

**Superseded by the resolution above. Recorded for history only. The options were:**

- **(a) Two boards as above.** Recommended. Keeps the visual-teaching-language feel for the non-programmer, and keeps every wire unambiguous.
- **(b) One board as originally written.** Faster to describe, and it is how some teaching tools work, but a board that mixes relations and values has no single meaning for a wire, and the "illegal wiring" rule of A1.5 becomes very hard to define, because almost any combination becomes arguable.

## A1.8 - Milestone placement

Honest statement of cost, so this amendment is not read as a two-day promise.

| Clause | Status | Milestone |
|---|---|---|
| A1.5 wiring legality on the existing S1 canvas | Not implemented; small and high value | **M1** |
| A1.3 left panel three-level unfolding tree | Partially present in S1 | M2 |
| A1.3 debug log with described severities | Not implemented as specified | M2 |
| A1.4 SQL mode on any surface | Not implemented anywhere | M2 |
| A1.6 widget authoring through the shell | Not implemented | M2 |
| A1.1 one shell serving all five purposes | Not implemented; five surfaces currently differ | M2 |
| A1.2 the mode toggle | Not implemented | M2 |

**For the customer presentation:** S1 is the surface to demonstrate, because it is the only one that genuinely carries a node canvas today. The remaining clauses are stated as the pilot roadmap in one sentence, per Rule 3.

---

*Ratification: A1.7 is resolved. This amendment is ready for Karim's approval and a version bump to v2.1.*
