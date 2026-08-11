# PPIQ SESSION HANDOVER - M1-P2 AUTHORING SHELL TRACK
## worker2, 05-Aug-2026, T-033 through T-037

READ THIS BEFORE DOING ANYTHING. Everything in it was measured, run, or ruled
on during the session. Nothing here needs re-deriving, re-investigating or
re-running. Where something was NOT done, this file says so in those words
rather than leaving you to discover it.

---

# 0. HOW TO USE THIS FILE

| If you are about to... | Read section |
|---|---|
| Start work at all | 1, 2, 3 |
| Continue T-037 | 8.5, 6, 12 |
| Write any PowerShell apply pack | 7 (all of it, twice) |
| Quote an expected test total | 6.1, and COUNT THE FILE |
| Touch the shell, the tree or the SQL pane | 5 |
| Answer a question about deployment or the pipeline | 9 - and read the warning at its top |
| Decide whether something is in scope | 2.3, 8 |

**THE SINGLE MOST IMPORTANT THING IN THIS FILE IS SECTION 7.** Ten defects of
mine cost roughly eighteen round trips in one session. Every one was mechanical
and every one was avoidable. If you read nothing else, read that.

---

# 1. IDENTITY AND TOPOLOGY

## 1.1 Who is doing what

- **Karim** - solo founder, SOU Industrial Software, Dusseldorf. 13 years
  industrial / MES / Level-2 automation. Owns every ruling in this file.
- **This track (worker2 / me)** - M1-P2, the No-Code Authoring Shell, starting
  at T-032. **FULL-STACK OWNERSHIP**: he ruled explicitly that this worker owns
  its tasks end to end INCLUDING backend and database defects that block
  acceptance. Four of the five tasks in this session turned out to have a real
  server defect underneath, so this is not theoretical.
- **A parallel worker (worker1)** - the database / analytics track, T-001
  through T-031, currently deep inside T-025. It edits the SAME repository at
  the same time. See section 11. This is why the byte-for-byte baseline guard
  in section 7.9 exists.

## 1.2 The governing documents, in precedence order

1. **The six Product Book chapters** - `PPIQ_Chapter1_Marketing_and_Sales.md`
   through `PPIQ_Chapter6_Infrastructure_Website_Administration.md`. His words:
   "my bible, follow 100 percent, deep detailed advanced professional." Chapter
   4 section 5.2.x is the authoring-shell specification and is quoted by task
   after task.
2. **`PPIQ_Backlog_v2_9_1_03Aug2026.md`** plus the `.xlsx`. 167 tasks,
   1,443 hours, FROZEN. **ABSOLUTE BACKLOG ADHERENCE**: if it is not written in
   the backlog, do not do it. Tasks execute in dependency order. A task is
   fully complete before the next starts.
3. **The PPIQ Constitution v3** - referenced in code comments (II.6.3 staging
   layer, II.7.6 no second implementation of a governance rule, III.16
   Amendment 6 renaming `dump_store` to `ppiq_staging` in M2).

## 1.3 The repository

- Root: `C:\Workspace\PlantProcess-IQ`
- Backend: .NET 9, `Backend/PlantProcessIQ.sln`
- Frontend: `Frontend/PlantProcess.Web`, Vite 8, React, React Flow (@xyflow),
  vitest, TypeScript project references (`tsc -b`)
- Databases: `ppiq_app` (dev) and `ppiq_presentation` (the demo population)
- API dev port **5063**, dev server **5173**

## 1.4 The authoring surface topology (measured, not assumed)

```
SharedAuthoringShell.tsx            the ONE authoring shell, ~838 lines now
  BLOCK-START   mode bar: [Block wiring | SQL], name, validity chip, Run, Publish
  INLINE-START  AuthoringSchemaTree  (unchanged in SQL mode, deliberately)
  CENTRE        CanvasShell (the board)  OR  the SQL pane
  INLINE-END    AuthoringToolbox     (ABSENT in SQL mode - see 5.4)
  BLOCK-END     CanvasDebugLog
```

Purposes: **S1** = prep/canvas (staging catalogue, the one this session worked
on), **S2** = widget authoring, **S3** = analysis toolbox.

**THE CONVERGENCE LADDER, his controlled debt, do not collapse it early:**
T-032 converged S1; **T-038 retires the standalone `WidgetAuthoringPanel`**;
**T-065 converges S3 `AnalysisToolboxPage`**. Both are still independent boards
today and both are covered by the pending-convergence ratchet in
`sharedAuthoringShell.test.tsx`, which fails if a new board appears silently or
if a listed board disappears without its allowlist entry being removed.

---

# 2. THE ROADMAP AND WHERE THIS SESSION LANDED

## 2.1 Position at session start

T-032 was **DONE and committed** (`57357ee4`, `92cf025e`). T-033a (the operator
contract) was done and frozen. Items 1-9 of T-033 were untouched.

## 2.2 Position at session end

| Task | Hours | Status at end of session |
|---|---|---|
| T-032 | - | **DONE, committed** |
| T-033 | 14 | **Implementation complete + evidence complete.** Browser rows deferred by him to the end of M1-P2. NOT committed. |
| T-034 | 10 | **Implementation complete.** Browser rows deferred. NOT committed. |
| T-035 | 8 | **Implementation complete.** Browser rows deferred. NOT committed. |
| T-036 | 12 | **Implementation complete.** Browser rows deferred. NOT committed. |
| T-037 | 3 | **Started - two measurements identified, not yet run.** |

**NOTHING FROM THIS SESSION IS COMMITTED.** Every change sits in the working
tree with a timestamped backup folder under `tools/packs/`. That was deliberate:
one commit at the end of the track, and the browser walk before it.

## 2.3 What he ruled about closure, and it changed mid-session

Originally: implement, targeted tests, browser walk, one build, one suite,
evidence, DONE. Then, after T-033's implementation went green, he changed it:

> **"I gonna go through the browser at the end of the P2."**

So the browser acceptance for T-033, T-034, T-035 and T-036 is **pooled into
one M1-P2 presentation walk that he will run himself.** Do not stop the backlog
for it, and do not re-offer to do it per task.

He also **reduced the walk** because the pure test suites already certify the
semantics exhaustively. See section 12.2 for the exact reduced walk he specified.

---

# 3. HIS STANDING RULES AND WAYS OF WORKING (carry all of these)

## 3.1 Delivery format - non-negotiable

- **ALWAYS deliver a PowerShell apply pack.** This covers diagnostics too, not
  only fixes. Never ask him to paste JS into DevTools or run ad-hoc commands.
- **Exception:** for a small ONE-LINE source edit, tell him exactly which line
  to change to what. Long pasted scripts get truncated by the console.
- **NEVER deliver zip files.**
- Pure ASCII, UTF-8 **no BOM** (`[System.IO.File]::WriteAllText` with
  `UTF8Encoding($false)`), CRLF for PS/CS, LF for .sh.
- **No `&&` in PowerShell.** Cuddled `} else {`. Run from repo root.
- **No em-dashes, no curly quotes.**
- Uploaded attachments frequently arrive empty - workaround is pasting output
  as text.

## 3.2 The run block - STANDING RULE, every time

Every delivery opens with the two lines that put the script where it runs from:

```
cd C:\Workspace\PlantProcess-IQ
Get-ChildItem "$env:USERPROFILE\Downloads\<name>*.ps1" | Sort-Object LastWriteTime | Select-Object -Last 1 | Move-Item -Destination tools\packs\<name>.ps1 -Force
Unblock-File tools\packs\<name>.ps1
```

Then `-ReportOnly`, then apply, then the revert/commit steps. Destination is
`tools\packs\` for apply packs and `tools\run\` for verification runners.

The `Get-ChildItem | Sort | Select -Last 1` form replaced a plain `Move-Item`
because a browser `(1)` copy defeated the plain form twice. See 7.6.

## 3.3 The pack contract

ReportOnly -> banner with REVISION -> preflight -> anchor verification ->
backup -> apply -> self-check -> gate -> auto-revert on any failure ->
rollback commands printed.

## 3.4 Cadence

> "Come back ONLY on a contract contradiction, an unavoidable schema/API
> incompatibility, or a genuinely new architectural decision. Otherwise make the
> smallest permanent correct decision and continue. **Optimise for COMPLETED
> TASKS, not verification-tool refinement.**"

## 3.5 Quality standing rule (set 02-Aug, after his review found 8 wrong tasks in my own backlog)

Deep, detailed, advanced, professional work is the DEFAULT. He should not have
to run the assessment that finds my gaps. Concretely:
- **Re-read the actual code before every review or revision.** Never work from
  notes.
- Never let a cross-reference, an hour total or a dependency order rest on
  memory when it can be verified or machine-checked.
- **Build a mechanical guard whenever a defect class is mechanical** instead of
  promising to be careful.
- State arithmetic openly and never fit a number to a budget.
- **Name my own defects before he finds them.**

## 3.6 Closure mode

Correct permanent fixes with MINIMUM investigation loops. Targeted tests during
a fix, the full gate ONCE at the end. New unrelated findings are recorded
against their owning task and do not delay the current one.

## 3.7 Things he said this session that generalise

- "Mechanically count tests before quoting future expected totals."
- "The actual Vitest result is authoritative" - do not spend time on prose
  arithmetic once a run has happened.
- "`tsc -b` remains a mandatory frontend pack gate. No separate task is needed
  for that lesson."
- "Do not silently approximate SQL as blocks."
- "Never present a planner estimate as actual runtime."
- "Do not claim a specific cause unless the server has evidence for that cause."
- On an unrelated finding: record it against its owner and continue.
- On a bounded safety hotfix found while wiring another task: "Fix it now...
  This does not reopen [the frozen task]."

---

# 4. TASK-BY-TASK EXECUTION LOG

## 4.1 T-033 - Shared Authoring Shell part 2: relational block grammar

**His seven rulings** (carried from the previous session's handover, all
honoured):
1. Implement **Select, NOT Rename**. Minimum bounded `SelectSpec` using the
   existing `Ident()` discipline. No new operator surface. With no Select block,
   preserve `SELECT *`.
2. Filter model is `dataset -> Filter -> dataset`. `FieldLineage` preserved
   through Join. **Unresolvable lineage makes the block INVALID and Run is
   refused - NEVER infer the table.**
3. The operator contract is CLOSED. Do not harden it further unless it fails.
4. Only `FilterNode`, `DerivedNode`, `SelectNode`. Do not pull Rename, Group By,
   Sort, Union, Cast, Lookup or T-034 work in.
5. The board is the source of authoring truth. No resurrected side forms.
6. **DELETE: `CanvasShell` already owns removal via
   `deleteKeyCode={["Backspace", "Delete"]}` - add the VISIBLE affordance only,
   never a second mechanism.**
7. **ARRANGE: smallest deterministic arrangement, NOT an auto-layout subsystem.**

**Delivered in six packs:**

| Pack | What |
|---|---|
| `apply-T-033-01-select-spec.ps1` | server `SelectSpec` + 2 integration tests |
| `apply-T-033-02-graph-semantics.ps1` | `graphSemantics.ts` model + 19 tests |
| `apply-T-033-03-block-nodes.ps1` | `BlockNodes.tsx` + dataset flow port + registry + toolbox |
| `apply-T-033-04-derived-field.ps1` (r3) | his derived-field-identity correction |
| `apply-T-033-05-wiring-arrange.ps1` | `wiringRefusal` + `arrangeBoard` + 16 tests |
| `apply-T-036-06c-canvas-toolbar.ps1` (r3) | shell wiring, canvas-toolbar placement |

### The three projection states (the SelectSpec design point)

```
Selects is null    no Select block      -> SELECT * preserved byte-identically
Selects is EMPTY   block, nothing ticked-> REFUSED with a named sentence
Selects has rows   -> project exactly those qualified fields
```

Null-vs-empty gives that distinction for free. Emitting `SELECT *` for an empty
block would return the opposite of what the author asked for.

### HIS MID-TASK CORRECTION: the derived field identity

I originally HID a Derived block's alias from the downstream field pool. **He
overruled that.** A Derived block PRODUCES a field, so the downstream schema
must truthfully contain it:

```
name / type      known  ("numeric" - all four compilable operators produce a number)
origin kind      derived
physical table   none
physical column  none
```

Because the compatibility server model cannot address an alias in a
`FilterSpec` or a qualified `SelectSpec`, **any such use is INVALID with a
NAMED REFUSAL** - never hidden, never guessed. **Do NOT build nested subqueries
or CTEs to make aliases filterable.**

Implemented as `BoardField extends FieldLineage` with `originKind`, so
`operatorContract.ts` stays frozen and `FieldLineage` still means a physical
field.

**A consequence the correction exposed, which the model now states:** a Select
block CANNOT remove a derived field, because `BuildSafeSelect` emits the
projection and then APPENDS one column per derived expression. Dropping it from
the pool would describe an output the server does not produce.

**UI consequence:** derived fields are LISTED in every column dropdown and in
the Select chips, but rendered DISABLED with the reason on the row. Truthful
presence plus an unreachable illegal state, which is what 5.2.7 asks for.

### HIS PLACEMENT CORRECTION: Arrange and Delete go on the CANVAS TOOLBAR

Not the shell action bar. Section 5.2.6 puts Arrange beside zoom and fit. The
action bar keeps Run, Publish version and the lifecycle/mode controls only.

Implemented as ONE OPTIONAL `boardActions` prop on `CanvasShell`, rendered
inside the React Flow `Controls` bar, so every unrelated `CanvasShell` consumer
is untouched.

His instruction was that it **land once, not be relocated later.** His message
arrived after r1 had already applied the action-bar placement, so r2/r3 rebuilt
the shell FROM THE T-032 BASELINE in the r1 backup rather than patching on top.
The wrong placement never reached a commit. **This produced the reproduction
guard in 7.9, which is now the pattern for any pack that overwrites from a
baseline.**

### Deliberate behaviour change from T-032, named in advance

**The join cycle check is now UNDIRECTED.** T-032 followed join edges
source-to-target only, so a loop closed against the direction the author
happened to drag in was missed. This refuses strictly more, all of it correctly,
and there is a test for exactly the case the old check missed.

### One refusal I added that is not in the chapter

**One chain per source.** `serialiseGraph` walks a single chain, so a second
chain hanging off the same block would be SILENTLY DROPPED from the definition.
That is worse than refusing it. He approved it explicitly:

> "The additional second-chain refusal is acceptable for T-033 because the
> current serializer supports a single chain and silently dropping a branch
> would be materially worse than refusing it. Record richer branching as future
> grammar capability if required later; do not implement it now."

## 4.2 T-034 - Registry-driven schema, table and attribute tree

Task text: grouping, drag of a table AND of a single attribute, multi-select,
search across schema/table/column, column type AND **nullability**, approximate
row count per table. **"Nothing in this tree may be a hardcoded table or column
name."**

### THE REAL DEFECT THE TASK WAS POINTING AT WAS IN THE BACKEND

`/datasets` marked key columns like this:

```csharp
var isKey = c.EndsWith("_id") || c.EndsWith("_no")
         || c is "id" or "piece_id" or "material_id" or "heat_id" or "coil_id";
```

**Four column names of the emulated plant, compiled into the product.** It would
have survived a grep of the tree file because it was never in the tree file.

Replaced by reading **PRIMARY KEY and UNIQUE constraints** from
`information_schema`, with a per-table STRUCTURAL fallback (`id`, `_id`, `_no`)
only where a table declares no key at all - because staged CSV loads often carry
none. Applied per table, so a table that declares its keys is never
second-guessed by a pattern.

### Unknown is not zero

`pg_class.reltuples` is **-1** on a table that has never been analysed. That is
reported as `null`, and the tree says "row count not analysed". "0 rows" is a
claim about the customer's data; "not analysed yet" is a claim about the
catalogue. Three states in the client: absent (older server), null (not
analysed), number (estimate).

The same principle for nullability: when the server did not send it, the type is
shown alone. The tree never invents "not null".

### HIS MULTI-SELECT DRAG RULING

The dropped SOURCE node carries `selectedColumns` as **AUTHORING METADATA AND
NOTHING ELSE.**

```
whole-table drag       -> selectedColumns absent
three-column drag      -> [colA, colB, colC]
single-attribute drag  -> [colA]
```

**No automatic Select block. Serialisation must NOT read it.** Projection stays
explicit: Source -> Select block, which is T-033's grammar. Two mechanical
guards hold it: `graphSemantics.ts` must contain no `selectedColumns`, and the
drop path must never mention a select node.

His placement ruling: reuse the existing deterministic source placement
(`x: 80 + ns.length * 300`); repeated drops visibly non-overlapping. **Do NOT
refactor the React Flow provider / `useReactFlow` structure for cursor-perfect
drop coordinates in T-034.** A later UX task may refine it.

### A shipped behaviour was REPLACED, not added

In T-032, clicking a column added its whole table. **A column row is now a
selection control.** The table row, a drag of either kind and double-click all
still add the table, so nothing became unreachable - but the click does
something different from what it did before.

## 4.3 T-035 - Compiled-SQL pane and debug log with rows and cost

### THREE REAL DEFECTS IN EXISTING CODE, all named by the task text

**(a) The dry-run `catch` returned `ex.Message` straight to the browser.** A
Postgres error - its SQLSTATE, its internal wording, sometimes a fragment of the
generated statement - was rendered in the Job Log. Now mapped by SQLSTATE to a
sentence about the author's DEFINITION (`42P01` vanished table, `42703` vanished
column, `42883`/`42804`/`42P08` type mismatch, `22P02`/`22003`/`22007` bad
filter value, `57014` timeout, `53300`/`53400` database busy, default = "the
definition compiled safely, this is a database problem, the reason is recorded").
**The real text is still recorded against the dry run** so support loses nothing.

**(b) The reported row count WAS THE CAP.** The reader stopped at 50 rows and
returned `rows.Count` as the row count, so a query returning four thousand rows
reported fifty. The cap stays - this is a preview - but the response now carries
`previewTruncated`, and the log reads "50 sample rows, stopped at the preview
limit".

**(c) FOUR shell handlers ended in `catch (e) { logError(name, String(e)); }`** -
preview, publish, run SQL, save SQL. I fixed one and the self-check caught the
other three. The no-raw-exception rule was only half kept.

### His terminology constraint on cost

PostgreSQL `EXPLAIN` gives a **planner cost estimate**, not runtime and not
money. **Plain EXPLAIN, never the ANALYZE form.** Fields named `plannerCost` and
`estimatedRows`. Labelled as an estimate in the UI: "planner cost estimate N",
"planner estimates about N rows". `TryExplain` returns nulls rather than
throwing, because a preview that works must not be lost because the estimate
could not be obtained.

### His zero-row contract, used verbatim

> "Preview completed successfully but returned 0 rows. Review the active filters
> or confirm that the selected source contains matching rows."

**Do NOT claim a specific cause such as "the filter is too restrictive" unless
the server has evidence for it.** A test asserts that phrase is absent.

## 4.4 T-036 - SQL mode: safe editor, run test, returned columns, reconstructability

Critical, 12 hours, the largest task on this track.

### The two bounded measurements he ordered, and their answers

1. **Provenance ALREADY EXISTS.** `saveSqlVersion` carries `forkedFromGraph`;
   the shell snapshots the originating graph into `forkedGraph`. What was
   missing was the compiled SQL AT THE MOMENT OF THE FORK. Added as ONE field,
   `forkedSql`, on the same mechanism. **No second provenance mechanism.**
2. **Types did NOT exist.** `RunSqlResponse` carried column NAMES only. Added
   `AuthoredColumn(Name, DatabaseType)` from `reader.GetDataTypeName(i)`,
   carried as `ColumnDetails` beside the existing `Columns`, defaulted to null
   so the refusal and failure paths are untouched and no caller breaks.

### HIS RECONSTRUCTABILITY RULING - fail closed

> "SQL -> Block without destructive confirmation ONLY when the product can PROVE
> a block representation exists. Do not use loose SQL regex matching as proof."

The one provable case: the authored SQL is still the SQL `forkedSql` recorded.
Normalisation is **line endings and outer whitespace ONLY** - no case folding,
no inner-whitespace collapsing, no comment stripping, because each of those
would be a claim about what SQL means and there is no parser to back it.

> "A harmless formatting edit producing a confirmation prompt is acceptable;
> silently reconstructing non-equivalent SQL is not."

Three verdicts: `reconstructable`, `diverged`, `no-origin`. A two-state boolean
would have got the third case silently wrong.

Cancel keeps the SQL and stays in SQL mode. Confirm discards and returns to the
block representation. **The warning is a RENDERED PANEL, not `window.confirm`** -
a guard forbids that by name, because a browser dialog is untestable and
unstyleable.

**Do NOT build a SQL parser. Do NOT pull future grammar (Group By, Sort, Union,
Cast, Lookup, Rename) in to make more SQL reconstructable.**

### The toolbox clause was ALREADY satisfied

The shell renders the toolbox inside `{mode === "block" && (...)}`, so in SQL
mode it is absent from the DOM. **T-036 needed a TEST, not an implementation.**
The test asserts `queryByTestId("authoring-toolbox-region")` is null, so changing
it to a disabled palette would fail.

### THE CLAUSE I NEARLY LOST: syntax highlighting

I delivered completions under a plain textarea and said "the editor is done."
**He caught it.** Autocomplete is not highlighting.

Closed with a **display classifier** - `sqlHighlight.ts` returns spans, no
grammar, no verdict - rendered under a transparent textarea sharing one metrics
class. **No editor platform**: no Monaco, no CodeMirror, nothing added to
`package.json`, and the pack re-reads `package.json` afterwards to prove it.

The invariant, asserted on seven samples including escaped quotes, block
comments and a keyword hidden inside a string: **the spans concatenate back to
the exact input.**

### AND THE REAL DEFECT THAT FOUND

`DROP` and `TABLE` were not in my keyword list, so **forbidden SQL rendered
completely unhighlighted.** That makes the highlighter a **covert validity
signal** - an author reads "not highlighted" as "not recognised" and is told
something about legality by a component that has no business saying it, and
whose opinion the server may not share.

All the DDL/DML words are now highlighted like any other, so a forbidden
statement looks exactly like a permitted one until `SafeSqlValidator` rules. A
test asserts it. **Fixing the test instead would have hidden a genuine
misfeature.**

### The safety hotfix he authorised inside T-036

The authored-SQL EXECUTION failure path appended `ex.MessageText` to the client
message - the same leak T-035 closed on the dry-run path, in a different file.
He ruled:

> "Fix it now... This does not reopen T-035. Treat it as a narrowly bounded
> safety hotfix discovered while wiring T-036. Reuse the existing
> `SafeDatabaseMessage` mechanism rather than creating another sanitizer."

`SafeDatabaseMessage` became `internal` for that reason and no other. A guard
asserts `AuthoringSupportEndpoints.cs` declares NO sanitiser of its own.

## 4.5 T-037 - Certify returned-column role mapping inside the S2 shell

**STARTED, NOT IMPLEMENTED.** 3 hours, Important, and **reduced in v2.1 from
implementation to hardening**. The task text says the mechanism already exists:
`widget-role-binding.ts` provides `readRoleBinding`, `writeRoleBinding`,
`staleRoles` and `describeStale`, persisting by column and detecting stale
mapped roles.

**The trap, named before starting:** the tempting move is to write a nicer
role-binding model because that pattern is warm after four of them. That would
be a rebuild of something the backlog says is done, and scope I was told not to
take.

**The two bounded measurements to run first:**
1. What `widget-role-binding.ts` actually exports and how it persists - by
   column, keyed on what.
2. Whether the S2 face of `SharedAuthoringShell` currently reaches it at all.
   S2 is the widget-authoring purpose; T-032 converged S1 and left
   `WidgetAuthoringPanel` standing until T-038, **so the honest question is
   whether the role binding lives in the panel that is about to be retired
   rather than in the shell that replaces it** - and if so, T-037 is where it
   moves.

**Validation rows:** assign roles in the shell, re-run the query unchanged,
roles persist; then edit the query to drop a mapped column, re-run, and the
stale-role warning **names the missing column**. The second is the assertion
worth building the test around - a warning that says "some roles are stale" is
exactly what the 5.2.8 contract forbids.

---

# 5. THE IMPLEMENTATION AS IT NOW STANDS

## 5.1 Files CREATED this session

### Frontend - `Frontend/PlantProcess.Web/src/authoring/`

| File | Purpose | Pure? |
|---|---|---|
| `graphSemantics.ts` | lineage, validity, refusal set, Arrange, serialisation | YES - no React, no network, no @xyflow |
| `graphSemantics.test.ts` | 35 tests | |
| `BlockNodes.tsx` | FilterNode, DerivedNode, SelectNode, `AUTHORING_NODE_TYPES` | no |
| `blockNodes.test.tsx` | 13 tests | |
| `schemaTreeModel.ts` | search, multi-select, drag payload, wording | YES |
| `schemaTreeModel.test.ts` | 19 tests | |
| `authoringSchemaTree.test.tsx` | 12 tests | |
| `previewReport.ts` | debug-log severity and wording | YES |
| `previewReport.test.ts` | 11 tests | |
| `sqlModeModel.ts` | reconstructability, completions, returned columns | YES |
| `sqlModeModel.test.ts` | 17 tests | |
| `sqlModeShell.test.tsx` | 3 tests - the toolbox rule | |
| `sqlHighlight.ts` | display classifier, spans only | YES |
| `SqlHighlighted.tsx` | the rendering, used twice | no |
| `sqlHighlight.test.tsx` | 9 tests | |

### Backend - architecture guards

| File | Tests | Guards |
|---|---|---|
| `T034CatalogueHasNoPlantLiteralsTests.cs` | 3 | no plant literal in the catalogue endpoint; keys from constraints; nullability + row estimate present |
| `T035DebugLogSafetyTests.cs` | 4 | no `message = ex.Message`; no ANALYZE form; no forbidden phrase; the cap is declared |
| `T036AuthoredSqlSafetyTests.cs` | 4 | no `ex.MessageText`; ONE sanitiser reused not copied; types from reader metadata; refusal still by name |

## 5.2 Files MODIFIED this session

| File | What changed |
|---|---|
| `Backend/.../Prep/VisualMapperEndpoints.cs` | `SelectSpec` record + `MapperGraph.Selects`; three-state projection; catalogue reads `is_nullable` + `reltuples` + real key constraints; `LooksLikeKey` structural fallback; `TryExplain`; `SafeDatabaseMessage` (now `internal`); `previewTruncated` |
| `Backend/.../Prep/AuthoringSupportEndpoints.cs` | `AuthoredColumn` record; `ColumnDetails` on the success response; execution failure uses the shared sanitiser |
| `Backend/tests/.../Mapping/VisualMapperSessionLifecycleTests.cs` | +3 tests (projection, empty-select refusal, catalogue contract) |
| `Frontend/.../src/api/canvasApi.ts` | `SelectSpec`, `StagedColumn.isNullable`, `StagedDataset.approxRowCount`, `previewTruncated`/`plannerCost`/`estimatedRows`, `AuthoredColumn`/`columnDetails` |
| `Frontend/.../src/authoring/SharedAuthoringShell.tsx` | the big one - see 5.3 |
| `Frontend/.../src/authoring/AuthoringSchemaTree.tsx` | REWRITTEN - search, multi-select, two drag sources, nullability, row count |
| `Frontend/.../src/authoring/AuthoringToolbox.tsx` | optional `addableBlockIds` + `onAddBlock` |
| `Frontend/.../src/authoring/blockRegistry.ts` | filter / select-columns / derived-column now `available: true`; **rename stays false per ruling 4** |
| `Frontend/.../src/authoring/sharedAuthoringShell.test.tsx` | ratchet list extended to `BlockNodes.tsx`; mock renders `boardActions`; +2 tests |
| `Frontend/.../src/canvas/CanvasShell.tsx` | THREE optional props: `boardActions`, `onBoardDragOver`, `onBoardDrop` |
| `Frontend/.../src/canvas/nodes/DatasetNode.tsx` | `flow:out` port; `selectedColumns` marking |
| `Frontend/.../src/authoring/authoring-shell.css` | node status, chips, canvas actions, tree search, discard panel, completions, highlighting |

## 5.3 `SharedAuthoringShell.tsx` - what it looks like now

Grew from 549 to ~838 lines. What was REMOVED matters as much as what was added:

- **REMOVED**: `portTypeOf` and `refusalFor` (replaced by `wiringRefusal`), the
  hand-built `MapperGraph` memo (replaced by `serialiseGraph`), the
  stranded-table check (replaced by `boardProblems`), the `@/canvas/ports`
  import that only those used, and all four `String(e)` handlers.
- **ADDED**: `boardNodes` / `boardEdges` (the entire adapter to the pure model),
  `renderNodes` (lineage injected per render, never stored), `addBlock`,
  `setNodeField`, `toggleSelectField`, `deleteSelected`, `doArrange`,
  `onBoardDrop` / `onBoardDragOver`, tree query + selection state, `forkedSql`,
  `requestBlockMode` / `confirmBlockMode` / `cancelBlockMode`, `sqlCompletions`,
  `applyCompletion`.

**The design that makes this maintainable:** every decision lives in a pure
module; the shell renders and adapts. `boardNodes` and `boardEdges` are the
whole bridge between React Flow and `graphSemantics`.

## 5.4 Contracts a future change must not break

- The toolbox is **conditionally rendered**, not disabled, in SQL mode.
- Deletion has **ONE mechanism**: the button dispatches the same `remove` change
  events the Delete key produces through `onNodesChange` / `onEdgesChange`.
  `deleteKeyCode` must not appear in the shell.
- Lineage is **derived every render**, never stored on nodes.
- `serialiseGraph` **throws** rather than emitting a partial definition.
- `selectedColumns` is metadata; `graphSemantics.ts` must never read it.
- The highlighter must **never** report a verdict.

---

# 6. EVERY TEST RUN AND ITS RESULT - DO NOT RE-RUN THESE

## 6.1 Targeted frontend suite (`src/authoring`), in order

| After | Passed | Failed | tsc -b |
|---|---|---|---|
| T-033 model pack | 19 | 0 | clean |
| T-033 node pack | 46 | 0 | clean |
| T-033 derived-field correction | 49 | 0 | clean |
| T-033 wiring + arrange | 65 | 0 | clean |
| T-033 shell wiring (final) | **67** | 0 | clean |
| T-034 tree model | 86 | 0 | clean |
| T-034 tree UI | **98** | 0 | clean |
| T-035 debug log | **109** | 0 | clean |
| T-036 sql model | 126 | 0 | clean |
| T-036 sql surface | 129 | 0 | clean |
| T-036 highlighting | **138** | 0 | clean |

**Baseline before this session: 15 authoring tests. Now 138. Delta +123.**

## 6.2 The ONE full frontend suite (his closure gate, 05-Aug 13:41)

```
test files (describe blocks + 1/file) : 154
passed : 330
failed : 3
total  : 333
```

The three failures are **exactly** the pre-existing JourneyRail T-012 baseline:
- `renders all 15 canonical stages plus the operational alerting entry`
- `marks the current route as the current journey step`
- `maps assistant configuration routes to the final assistant stage`

**ZERO T-033 regression.** He accepted automated closure on this.

**DO NOT RUN THE FULL SUITE AGAIN** unless browser acceptance forces a code
change. His words.

Note: my predicted 326/329 was 4 short. Four tests entered the suite from
outside `src/authoring` since the T-032 baseline of 277 was recorded - almost
certainly the parallel track. He ruled: **"Do not identify the four additional
passing tests unless needed for another reason."**

## 6.3 Backend integration tests (live API + `ppiq_presentation`)

| After | Result |
|---|---|
| T-033 `SelectSpec` | `total: 4, failed: 0` |
| T-034 catalogue | `total: 5, failed: 0` |

Run command (kept because it is easy to get wrong):

```
$env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST = "1"
$env:ConnectionStrings__PlantProcessDb = "Host=127.0.0.1;Port=5432;Database=ppiq_presentation;Username=ppiq_dev;Password=ppiq_dev_local_only"
dotnet test Backend\tests\PlantProcess.Api.IntegrationTests --filter VisualMapperSessionLifecycleTests
Remove-Item Env:\PPIQ_FORCE_EXTERNAL_API_TEST_HOST
Remove-Item Env:\ConnectionStrings__PlantProcessDb
```

## 6.4 Backend architecture tests

| After | Result |
|---|---|
| T-034 guard | 3 passed |
| T-035 guards | 4 passed |
| T-036 guards (run together with T-035's) | **8 passed** |

## 6.5 The ONE production build (his final build)

`npm run build` via `cmd`, 05-Aug. Vite 8, **2,663 modules transformed**, clean.
`SharedAuthoringShell-ChMi85Gu.js` 40.02 kB (11.33 kB gzip),
`CanvasShell-BvxEnE2_.js` 166.46 kB.

Later artefact check found `SharedAuthoringShell-aKTP9EWb.js` 39.2 kB and
`CanvasShell-SQwURWSF.js` 162.59 kB, and **no `VisualJoinCanvasPage` chunk** -
the independent proof the retired page is gone rather than merely unrouted.

**DO NOT RUN ANOTHER PRODUCTION BUILD FOR CEREMONY.** His words. Only if a code
change follows browser acceptance.

## 6.6 Environment check

API `http://localhost:5063/health` -> 200. Dev server `http://localhost:5173` ->
200 (after being started; the first walk attempt found it down).

---

# 7. THE DEFECTS I MADE AND WHAT THEY COST - READ THIS TWICE

Ten distinct defects, roughly eighteen round trips. Every one mechanical. They
fall into four families.

## FAMILY A - a guard that matched a shape instead of the artifact (6 instances)

This is the single most expensive pattern of the session. **A guard must name
the exact artifact it forbids or requires, taken FROM THE FILE, never from my
memory of what I wrote.**

1. **`deleteKeyCode={["Backspace","Delete"]}`** - the file has a space after the
   comma. I took the needle from my own note instead of the file. Reverted a
   correct pack.
2. **A byte-exact import-line pin.** `expect(source).toContain('import { FLOW_IN, FLOW_OUT } from "./graphSemantics"')`
   - then a later correction legitimately widened that import and failed a test
   on correct work. **I broke my own guard.** Fixed with a regex naming the two
   identifiers and the module.
3. **`getByText("numeric, nullable")`** when TWO fixture columns matched. A DOM
   query identifying its target by content it does not uniquely own. Scope to
   the row's testid.
4. **A needle written from intent, not the file:** `"--[^\n]*"` while the file
   contains `"--[^\\n]*"` (doubled because it lives inside a JS string).
5. **A bare `parse` substring** that matched my own comment forbidding a parser.
   Replaced with three precise needles: `tokenize`, `parseSelect`, `buildAst`.
6. (Carried from the previous session - four earlier instances are recorded in
   the T-032 log.)

## FAMILY B - a file quoting the literal its own guard forbids (3 instances)

**A file that a guard scans MUST NOT QUOTE the literal the guard forbids, even
inside a comment explaining it.** Describe it, or assemble it from fragments.

- A comment reading "never `EXPLAIN ANALYZE`" tripped the ANALYZE guard.
- A comment quoting `"could not load"` while explaining that the phrase is
  banned tripped the forbidden-phrase guard.
- A comment saying `String(e)` while explaining its removal tripped that guard.

**Fix pattern:** `"could not " + "load"`, `"EXPLAIN " + "ANALYZE"`. This applies
to the guard file itself too, or a repository-wide scan flags the guard.

## FAMILY C - asserting a COUNT that was never counted (2 instances)

**An exact `-eq 1` belongs to an ANCHOR, where uniqueness is the point. A
revision or presence check asserts `-ge 1` on a string that is genuinely
unique.**

- `-eq 1` on `LooksLikeKey` - it appears three times (comment, declaration, call
  site). Failed a correct tree.
- `-eq 1` on `decodeSchemaDrag` - it appears twice (import, call site). Failed a
  correct tree one hour later.

Related: **I predicted test totals from estimates three times and was wrong
three times** (17 vs 19, 91 vs 86, 126 vs 129). His instruction:
**mechanically count the `it` blocks in the file before printing an expected
total.** Once counted, every prediction since has been exact.

## FAMILY D - PowerShell and toolchain mechanics (4 instances)

1. **Inside a PowerShell `@( )` array a newline SEPARATES ELEMENTS.** A string
   continued onto the next line beginning with `+` is parsed as a unary plus and
   throws an `Int32` cast error. A continuation needs the operator at the END of
   the previous line, or must not be in an array literal.
2. **`npm.cmd` piped through PowerShell with `2>&1`.** Vite writes a reporter
   line to STDERR, and `$ErrorActionPreference = "Stop"` turned a SUCCESSFUL
   build into a terminating `NativeCommandError` AFTER the build completed. **Fix:
   run the whole command inside `cmd` with its own redirection and judge only
   the exit code.** Same class as the vitest-writes-to-stderr lesson.
3. **`{...(props as never)}` gives TS2698** "Spread types may only be created
   from object types" - and **vitest CANNOT catch it because esbuild strips
   types without checking them.** 98 tests passed over a file that did not
   type-check. Cast through `unknown` to the component's exported props type.
   **This was the second time in one session I wrote the never-spread.**
4. **A stale pack ran twice.** Once because the run block's `Move-Item` was
   skipped; once because I delivered a revision under the SAME FILENAME. Two
   guards now: every pack prints a `REVISION:` banner in its first three lines,
   and **every revision gets a DISTINCT FILENAME** (`-01b`, `-02c`, `-02d`).
   The banner only reports the problem after the fact; the filename prevents it.

## 7.9 THE GUARD HE REQUIRED, now the pattern for any overwrite-from-baseline pack

> "Before writing, prove that the current file is exactly the expected
> previously-applied state, or otherwise prove there are no unrelated local
> edits. If the current file contains an unrelated modification, abort rather
> than overwrite it. **This is particularly important because work is proceeding
> in parallel.**"

Implemented by REPRODUCING the previous applied state from the same baseline and
comparing **byte for byte, ordinal**, before the backup. Not a marker, not a
shape. It passed cleanly and is why the r3 rebuild was safe.

## 7.10 Two hygiene notes that are NOT defects

- vitest `Failed to start threads worker` is transient - retry once; if it
  repeats use `--pool=forks`.
- Before any `dotnet build`, stop `PlantProcess.Api` or locked DLLs produce
  MSB3027 errors that read like compile failures. Every backend pack does this.

---

# 8. BACKLOG STATUS

## 8.1 This track (M1-P2)

| Task | Hours | Status |
|---|---|---|
| T-032 | - | DONE, committed (`57357ee4`, `92cf025e`) |
| T-033 | 14 | Implementation + evidence complete; browser deferred; NOT committed |
| T-034 | 10 | Implementation complete; browser deferred; NOT committed |
| T-035 | 8 | Implementation complete; browser deferred; NOT committed |
| T-036 | 12 | Implementation complete; browser deferred; NOT committed |
| T-037 | 3 | Started - measurements identified, nothing written |
| T-038 | ? | NOT STARTED - retires standalone `WidgetAuthoringPanel` |
| T-065 | ? | NOT STARTED - converges S3 `AnalysisToolboxPage` |

**44 of the 47 implementation hours in T-033 to T-036 are delivered.**

## 8.2 The parallel database track (worker1) - for coordination only

Do not act on this; it is recorded so you understand the repository you share.

- T-013 to T-023 DONE - the Fleet v2 donor generator.
- **T-024 reported finished**: canonical presentation population at 35,910
  material units, 301,560 parameter observations, 34,020 genealogy edges, all
  tagged FLEET_V2, 9 materialized views refreshed, API /health 200. **Two items
  the verifier itself still lists as outstanding**: mixed-industry vocabulary
  still customer-visible, and requirement 8 (the browser check) never performed.
- **T-025 IN PROGRESS and complicated.** The feature store was reopened once as
  a bounded correctness exception. Three producer defects were found
  (`defect.rate_per_m2` hardcoded to literal `1.0`; `defect.severity` written to
  a column the loader never reads; `defect.class` contaminated by
  `Disposition`). Insert-time lineage was required because a NOT NULL invariant
  had made the producer unrunnable. **Refresh budget: A + B, no third cycle.**

**COORDINATION HE SET:** during worker1's heavy authenticated refreshes, this
track should not run a full frontend/backend build or full test suite. Targeted
tests and coding continue. Resource contention only.

---

# 9. DEPLOYMENT, SERVER AND PIPELINE

## 9.1 READ THIS FIRST

**NO DEPLOYMENT, SERVER, CI OR PIPELINE WORK WAS PERFORMED IN THIS SESSION.**
Not one command was run against a server, a pipeline, or a deployed URL. **No
modification was made to make the pipeline green or to make the App URL work,
because none was attempted.**

Everything below is **STATIC KNOWLEDGE read from the audit signal report and the
repository dump**, never verified against a running pipeline. Treat every line
as unconfirmed.

## 9.2 What the 05-Aug audit signal report says

Package: 2,195 files / 375,759 lines / 27.62 MB. **56 signals** (03-Aug had 62).

**The entire drop is pack-backup churn, not remediation.** The
`_backup_M1-PREREQ-*` copies were removed (dev seed 21 -> 16, bootstrap admin
4 -> 3) and a new `_backup_T-025-engine-timeout_*` appeared carrying the same
two. **Every real finding is unchanged at the same file:line.**

| Signal | Hits | Severity |
|---|---|---|
| Security: dev seed endpoint reference | 16 | WARN |
| Config: hardcoded server IP `178.105.152.180` | 15 | WARN |
| **CI: frontend tests enumerated, not executed (`--list`)** | 8 | **CRIT** |
| Hygiene: TODO / FIXME / HACK | 7 | INFO |
| Security: bootstrap admin enabled in config | 3 | WARN |
| Refactor: gate-closing / shim wrapper comment | 3 | WARN |
| **CI: `catchError` forcing SUCCESS** | 3 | **CRIT** |
| **Config: wrong connection-string key** | 1 | **CRIT** |

## 9.3 The two live CI findings (unchanged since 03-Aug, still open)

**FINDING A - an orphan validator.** `tools/ci/validate-real-ui-gates.cjs`
declares the three `--list` invocations and **is invoked by nothing**.

**FINDING B - a script that re-injects the defect.**
`Frontend/PlantProcess.Web/tools/phase56/apply-phase5-phase6-full-ui-migration.cjs`
lines 74-76 patch `npm run test:visual -- --list`,
`npm run test:phase56:e2e -- --list` and `npm run test:a11y -- --list` into the
Jenkinsfile. **Anyone re-running that migration script re-creates the CRIT
finding.** Also `package.json:84`:
`"phase9:matrix": "playwright test --config=playwright.phase9.config.ts --list"`.

`--list` **enumerates** tests without running them. A pipeline stage using it
reports green having executed nothing.

**FOUR of the CRIT hits and several WARN hits are SELF-MATCHES** - the audit
scanner `tools/GeneratePlantProcessIQ_UltimateAudit.ps1` matching its own regex
definitions at lines 659, 663, 665, 671, 689, 693, 699, 705, 707. A one-line
exclusion for the scanner's own file was recommended on 03-Aug and **was never
applied** - the same hits fired again on 05-Aug.

## 9.4 Other unverified static facts

- **Hardcoded server IP `178.105.152.180`** in 15 places across
  `deploy/ci/post-deploy-smoke.sh`, `deploy/scripts/ensure-runtime-env.sh`,
  `deploy/server/README.md`, `deploy/server/verify-server-exposure.sh`,
  `scripts/deploy/Invoke-CleanMachineDeployAcceptance.ps1`,
  `docs/deployment/T007_CLEAN_MACHINE_DEPLOY_ACCEPTANCE.latest.md` and three
  validators. Public URLs are `https://app.178.105.152.180.sslip.io` and
  `https://api.178.105.152.180.sslip.io`.
- **`PlantProcess__Auth__Users__0__IsBootstrapAdmin=true` in
  `env/profiles/presentation.env`** (line 41) as well as `local.env`. The
  presentation profile is the demo one.
- A dev seed endpoint exists at
  `Backend/PlantProcess.Api/Endpoints/Development/DevSeedEndpoints.cs` with a
  release stub, gated by `app.Environment.IsDevelopment()` at `Program.cs:1011`,
  and `ProductionDevEndpointGuardTests` asserts that gating.
- **The most likely new pipeline break from this track:**
  `operatorContract.test.ts` reads
  `../../Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs`. **If
  CI builds the frontend in isolation without the Backend folder present, that
  test fails on a missing file.** Unverified. The three architecture tests added
  this session use a walk-up-to-repo-root pattern and would behave the same way.
  **Worth checking before the next pipeline run.**

## 9.5 What a future session should do about section 9

Nothing in it belongs to T-033 through T-037. All of it belongs to the
deployment and CI tasks. **Record findings against their owning task and
continue** - that is his standing instruction. Do not open a CI investigation
inside an authoring task.

---

# 10. THE PPIQ REALIZATION SCOREBOARD AS AT SESSION END

## 10.1 What genuinely improved

| Area | Before this session | After |
|---|---|---|
| Authoring test coverage | 15 tests | **138 tests** |
| Relational grammar on the board | none - blocks declared, unavailable | Filter, Derived, Select live with lineage and refusals |
| Illegal wiring | partial, directed cycle check only | full 5.2.7 set in ONE tested function, undirected cycles |
| Schema tree | group + type only | search, multi-select, two drag kinds, nullability, row estimate |
| Catalogue key detection | **four plant column names hardcoded** | real PK/UNIQUE constraints + structural fallback |
| Debug log | raw exception strings in four places | typed Error/Warning/Success, engineer-facing, zero raw text |
| Preview honesty | cap reported as the row count | `previewTruncated` + planner estimate, both labelled |
| SQL mode | read-only pane + plain textarea | highlighting, catalogue completions, Run Test types+samples, fail-closed discard rule |
| Provenance | `forkedFromGraph` only | `forkedSql` too, on the same mechanism |

## 10.2 What is still open, honestly

**On this track:**
1. **Browser acceptance for T-033/T-034/T-035/T-036** - pooled into his M1-P2
   walk. Until it runs, none of the four is Done under law 5.
2. **The drop path has NO headless test** - the acceptance suite mocks
   `CanvasShell` and the mock never receives a real drag event. Browser row.
3. **Glyph alignment** between the highlighted ghost copy and the textarea is a
   rendering property no headless test can prove. Browser row.
4. **Nothing is committed.**
5. T-037 not implemented; T-038 and T-065 not started.

**Adjacent, recorded against owners:**
6. The three JourneyRail failures - **T-012**, with the specific diagnosis
   already recorded: two stale certification expectations after the T-012
   renumbering, plus one live J15 route-match defect where `/assistant-config`
   redirects to `/assistant/configuration`, so the rail matches a redirect
   source rather than the canonical route. **The agreed guard: every JourneyRail
   match prefix must resolve to a canonical non-redirect route.**
7. The two CI findings and the unapplied scanner self-match exclusion -
   deployment/CI tasks.
8. T-024 requirement 8 (browser check) and the mixed-industry vocabulary -
   worker1.

## 10.3 My honest assessment

The authoring shell is now a genuinely defensible product surface: every
decision it makes is in a pure module with tests, every refusal states a reason
a plant engineer can act on, and four real defects that had shipped were found
and fixed by taking the task text literally rather than treating it as a wish
list.

**The weakest part of this session was my own execution, not the design.** Ten
mechanical defects cost roughly eighteen round trips. Section 7 exists so the
next session does not pay that again.

**The largest remaining risk is not technical.** It is that four tasks are
implementation-complete but unverified in a browser and uncommitted. A crash or
a conflicting parallel edit would cost all of it. **Consider proposing a commit
of the implementation work before starting T-037**, framed as a checkpoint
rather than a closure - his call, since his rule is one commit at the end.

---

# 11. WORKING WITH THE PARALLEL TRACK

- The other worker edits the SAME repository. `git status` will show changes you
  did not make.
- **Never write a pack that overwrites a file from a baseline without the
  byte-for-byte reproduction guard in 7.9.**
- Do not run full builds or full suites while worker1 runs heavy authenticated
  refreshes.
- Their backup folders appear in `tools/packs/_backup_T-025-*`. Do not touch
  them.

---

# 12. WHAT TO DO NEXT

## 12.1 Immediate

1. **Do not re-run anything in section 6.**
2. Continue **T-037**: run the two measurements in 4.5, then implement, targeted
   tests, `tsc -b`. Expect it to be small - the task says hardening, not a build.
3. Then T-038, honouring the convergence ladder.

## 12.2 His reduced browser walk (for when he runs it)

**Automated exhaustive evidence already covers:** D1-D8 refusal semantics, the
operator contract, lineage propagation, serialisation, deterministic Arrange,
invalid-board rules. **Record that as the source; do not click them again.**

**The browser proves integration only:**
- **A** shell anatomy: `/prep/canvas` renders; Filter/Derived/Select enabled;
  future grammar blocks visibly unavailable; Arrange + Delete on the CANVAS
  TOOLBAR; Run + Publish in the action bar.
- **B** one complete authored flow: two tables -> valid key join -> Filter ->
  Derived Column -> Select Columns. Confirm joined fields retain qualified
  lineage; the derived alias appears as derived/non-physical; an empty Select
  makes the flow Invalid; choosing two real fields restores Valid.
- **C** real execution: Run succeeds; preview rows; SQL mode read-only; exact
  selected projection not `SELECT *`; derived expression present; filter value
  parameterised; correct FROM/JOIN.
- **D** three representative refusals ONLY: **D1** column -> dataset mismatch,
  **D6** text -> numeric join, **D8** join-cycle closure. Each: the edge must
  not land AND exactly one named refusal sentence must reach the Job Log.
- **E** board editing: Arrange once, Arrange again -> **nothing moves**; Delete
  selected; Delete key -> same semantics.
- **F** Publish version succeeds, version identity logged.

Plus, for the tasks after T-033: the schema-tree search and multi-select, both
drag kinds, the foreign-drag refusal, the SQL editor highlighting and
completions, the Run Test column/type/sample panel, and the discard confirmation
with Cancel preserving the SQL.

The skeleton runner is `tools/run/Invoke-PpiqT033BrowserWalk.ps1`. **One label
in it is wrong and he ruled not to re-run the suite to fix it:** change

```
Say ("  test files : " + $rep.numTotalTestSuites)
```

to

```
Say ("  suites     : " + $rep.numTotalTestSuites + " (describe blocks plus one per file, not a file count)")
```

## 12.3 If a browser row fails

Only a failure of the implemented contract of the owning task blocks that task.
Unrelated findings go to their owner and do not reopen anything.

---

# 13. ONE-PAGE CRIB

```
OWNERSHIP     full-stack; backend and DB defects blocking my task are mine
SCOPE         backlog only; if it is not written, do not do it
CADENCE       smallest permanent correct decision, continue, do not stop
DELIVERY      PowerShell apply pack, ASCII, CRLF, no &&, run block on top
              distinct filename per revision, REVISION banner in the first 3 lines
GATES         targeted vitest + tsc -b (frontend); build + arch tests (backend)
              full suite and production build: ONCE, already spent, do not repeat
GUARDS        name the artifact from the FILE, never from memory
              never quote in a file the literal its guard forbids
              -eq 1 for anchors; -ge 1 for presence
              count it blocks before quoting a total
NEVER         infer a lineage table; claim a cause the server cannot see;
              show a raw exception; present an estimate as a runtime;
              silently approximate SQL as blocks; add an editor platform;
              build a SQL parser; create a second sanitiser or a second
              provenance mechanism
STATE         T-033..T-036 implemented, 138 authoring tests green, uncommitted,
              browser deferred to his M1-P2 walk; T-037 next
```

---

*End of handover. Everything above was measured, run, or ruled on during the
session of 05-Aug-2026. Where it was not, the file says so.*
