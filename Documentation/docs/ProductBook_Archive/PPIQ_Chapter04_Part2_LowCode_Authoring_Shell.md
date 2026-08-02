# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

*Covers PPIQ.txt 5.2. Audience 5.8. Voice 5.9.*

---

# CHAPTER 4, PART 2 - THE NO-CODE / LOW-CODE AUTHORING SHELL

Features, functionality, layout, UI/UX, the schema table bar, the toolbox drag-and-drop design, the wiring diagram with debugging, saving and transfer to code, joining and using the result in data analysis, the SQL editor with debugging, running, saving and use, and the predefined and advanced toolboxes.

---

## 4.2.1 The ruling: one shell, five purposes

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

## 4.2.2 The two modes

A toggle sits at the block-start of every one of the five purposes. Always present, always exactly two modes.

| Mode | Intended user | Rationale |
|---|---|---|
| **Block and wiring diagram** | A plant user with no software experience | The authoring act must be achievable without writing a line of anything |
| **SQL** | A user with some database experience | The long tail a block palette will never cover must not become a support ticket |

**Neither mode is a lesser citizen.** Both produce the same artifact class. SQL mode is available from the second licence tier upward and requires an authoring role; a viewer never authors SQL at any tier.

**Switching modes.** Block to SQL always succeeds: the graph compiles to SQL and the SQL is loaded into the editor. SQL to block succeeds **only when the SQL is reconstructable** into the block grammar; where it is not, the toggle states so plainly - "This statement uses constructs the block palette cannot represent. Switching will keep the SQL and discard the diagram." - and requires confirmation. **The fork is stated at the point of the switch**, not buried in a specification.

---

## 4.2.3 Layout: the four regions

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

## 4.2.4 The schema table bar (inline-start)

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

## 4.2.5 The toolbox (inline-end)

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

The method toolbox, registry-driven. Catalogued with inputs, outputs, validation and best chart in Chapter 4, Part 4.

### Group 5 - Model and feature (S4)

Feature assembly, split, train, score, evaluate. Catalogued in Part 4.

### Group 6 - Condition and action (S5)

Threshold condition, range condition, routing-deviation condition, emit info, emit warning, emit error.

### Control flow

`FOR` and `WHILE` are **orchestration and belong to neither board.** A saved definition describes what its output is; how often it runs and over what window belongs to the job that carries it. A transform graph is declarative.

---

## 4.2.6 The board: node and port model

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

## 4.2.7 Illegal wiring is refused at drag time

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

## 4.2.8 The debug log: debugging the wiring diagram

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

## 4.2.9 Run, dry-run and preview

| Action | Scope | Result |
|---|---|---|
| **Preview node** | The selected node | Sample rows in the inspector, with the row estimate |
| **Dry-run** | The whole graph, bounded | Sample rows per node, no write, cost estimate in the log |
| **Run** | The whole graph | The real execution; on S1 it projects to canonical, on S2 it returns the widget dataset, on S3 and S4 it submits a governed job |
| **Compiled SQL** | The whole graph | The exact statement that will run |

**Run is disabled while the validity indicator reads Invalid.** The author never gets to start something the validator already knows will fail.

**Progress streams.** A run reports rows per node as it goes, never a bare spinner. On completion the log states rows, columns and cost.

---

## 4.2.10 Transfer to code: the compiled SQL view

**Every block graph compiles to SQL, and the author may always see it.**

| Property | Rule |
|---|---|
| Read-only | The compiled view is not editable. Editing happens in the graph or in SQL mode |
| Exact | It is the statement that will run, not an approximation |
| Formatted | Indented, keyword-cased, with a comment per source node naming its block |
| Copyable | One click copies it, so a DBA can run it against their own instance to verify |

**Why this matters commercially.** A plant DBA asked to trust a visual tool will ask what it actually executes. Being able to show them, immediately, converts the deepest objection into a demonstration. It is also how an author learns SQL from the block palette, which is a genuine adoption path.

---

## 4.2.11 Joining, and using the result in data analysis

**The join is declared once, in S1, and never again.** This is the property the whole product rests on.

### On S1

The author drags two staged tables, drops a **Join** block, and declares the key pair from typed dropdowns fed by live schema. The join may be composite, and where the customer's identifiers differ across sources the **business key dictionary** resolves them: the definition records which source field plays which member role, and the projector writes the resolution into `material_aliases`.

The edge on the board is labelled with the equality, for example `piece_id = material_id`, so the declaration is readable at a glance.

### Downstream

Because the relationships already exist in the canonical model, **S2 to S5 operate on one prepared dataset with a single row context.** A comparison between two columns has exactly one meaning: within the same row of the prepared model. This is why expression blocks live inside board blocks on every surface - one board grammar, one validator, one error taxonomy.

### Reuse

A saved definition is selectable as a **source** in another definition. An analysis in S3 selects the prepared dataset a Transformation Definition produced; a widget in S2 selects the result of an analysis. **Definitions compose; they are not islands.**

---

## 4.2.12 SQL mode

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

## 4.2.13 Saving, versioning and transfer

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

## 4.2.14 Predefined and advanced toolboxes

| Toolbox | Contents | Who uses it |
|---|---|---|
| **Predefined** | The relational group, the condition group, and templates: "map a source table to material units", "join two sources on a business key", "threshold rule on a parameter" | A plant user in the first week |
| **Advanced** | Window functions, recursive genealogy traversal, custom aggregate expressions, multi-key composite joins, statistical and model blocks | An engineer once fluent |

The advanced set is **collapsed by default** and expands with one click. It is not hidden behind a tier, because hiding capability behind a price is what makes SQL a support ticket.

**Templates are not content.** A template is a shape with no plant vocabulary in it; it names the roles a user must fill, and it ships only if it passes the genericity lint.

---

## 4.2.15 Acceptance

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

*End of Chapter 4, Part 2.*
