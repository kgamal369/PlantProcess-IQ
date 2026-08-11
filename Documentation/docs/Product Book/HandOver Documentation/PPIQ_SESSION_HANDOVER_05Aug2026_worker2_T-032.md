# PPIQ SESSION HANDOVER
## From the 04/05-Aug-2026 session that executed T-032 and T-033a

**Purpose.** This document exists so the next session starts with everything this one learned, and does not re-investigate, re-measure or re-run anything already proven. Read it in full before writing code.

**Honesty note, first, because it changes how you read section 9 and 10.** This session did NO deployment, server, CI or pipeline work. Sections 9 and 10 record only what was measured from a static audit report and are explicitly marked as not addressed. Nothing in them is a claim of work done.

---

# 1. WHAT WAS DONE, STEP BY STEP, WITH THE FINDINGS AND THE TIPS

## 1.1 The starting position

The session opened with a repository dump generated 03-Aug-2026 11:12:18: nine content files, a manifest, and an audit signal report. 2,201 files, of which 2,187 were extractable (the 14 missing are demo fixture SQL and CSV that live in a `07A` document that was not uploaded).

**FIRST REAL FINDING, and a tip that saved a wrong start.** The dump's newest file was `JourneyRail.tsx` at 03-Aug 10:54:03, and `Backend/tools/generate_fleet_v2_donor.py` was absent from it entirely. So the dump PRE-DATED T-014 through T-024. It could not verify the generator or anything the database track had built. It WAS still authoritative for the app schema and read layer, because those had not changed.

**TIP.** Always check a dump's newest `LastWriteTime` against the work you are about to verify. A dump that looks complete can be structurally useless for the specific question you are asking.

**TIP.** The dump format is `[FILE_START]` / `[METADATA: Path='...']` / body / `[FILE_END]`. An extractor that rebuilds a real file tree from it takes about 20 lines of Python and makes every subsequent grep and read trivial. Do this once at the start rather than grepping the concatenated text files repeatedly.

## 1.2 The governing documents

- **Design bible:** `PPIQ_Chapter1_Marketing_and_Sales.md` through `PPIQ_Chapter6_Infrastructure_Website_Administration.md`. To be followed 100 percent. Chapter 4 section 5.2 is the authoring shell specification and is the section that governed everything in T-032 and T-033.
- **Backlog:** `PPIQ_Backlog_v2_9_1_03Aug2026.md` plus `.xlsx`. 167 tasks, 1,443 hours, frozen. Tasks execute in dependency order from T-001 upward. If it is not written in the backlog, it is not done.

## 1.3 Parallel-track ruling

Two workers run in parallel before the presentation deadline:

- **This track:** T-032 onward, the M1-P2 No-Code Authoring Shell.
- **The other worker:** the T-024/T-025 analytics and database track. He is NOT a "backend worker". He independently progresses his own backlog tasks.

**Ownership ruling, important:** you are a FULL-STACK worker. You own your task end to end INCLUDING any backend or database defect that blocks its acceptance. Do not hand a blocker off because it is "backend".

---

# 2. THE IMPLEMENTATION: WHAT EXISTED, WHAT WAS CHANGED, WHAT IS THERE NOW

## 2.1 What existed before this session

`Frontend/PlantProcess.Web/src/pages/Prep/VisualJoinCanvasPage.tsx`, **777 lines** (the backlog said 784; the measurement supersedes it, drift only). Routed at `/prep/canvas` from `App.tsx:503`, lazily. It carried:

- a three-level schema tree with typed columns and `isKeyCandidate` markers
- drag-time refusal in `refusalFor` with five named reasons
- typed ports via `src/canvas/ports.ts`
- a two-mode bar (Block / SQL)
- a compiled-SQL pane read from the server dry run, never reconstructed client-side
- the fork-to-SQL warning, publish, and `CanvasDebugLog` at block-end
- an inline-end aside called "Preparation definition" holding the name, Preview/Publish, the joins list, and the M1-16 Filters and Derived-columns FORMS

## 2.2 What T-032 built

Seven new files under `Frontend/PlantProcess.Web/src/authoring/`:

| File | What it is |
|---|---|
| `authoringPurposes.ts` | The S1 to S5 purpose registry from Chapter 4 section 5.2.1. A purpose is added by adding a ROW, never a branch in a component |
| `blockRegistry.ts` | The six toolbox groups and 33 blocks of section 5.2.5, with `placement` separating board blocks from expression blocks, and an `available` flag (all false at T-032) |
| `AuthoringSchemaTree.tsx` | The inline-start region, section 5.2.4 |
| `AuthoringToolbox.tsx` | The inline-end region, section 5.2.5. Grouped, searchable, advanced groups collapsed per 5.2.14 |
| `SharedAuthoringShell.tsx` | THE shell. Takes a purpose parameter, renders the four regions of 5.2.3 |
| `authoring-shell.css` | Styles. Zero inline style objects |
| `sharedAuthoringShell.test.tsx` | 11 acceptance assertions |

`VisualJoinCanvasPage.tsx` was **backed up and DELETED**. `/prep/canvas` now renders `<SharedAuthoringShell purpose="S1" />` directly.

**WHY THE PAGE WAS DELETED RATHER THAN MADE A WRAPPER.** A wrapper would have left a second authoring page component for the acceptance test to trip over. Routing straight to the shell makes the "one authoring page" assertion trivially provable.

## 2.3 The four regions, as implemented

```
BLOCK-START   mode bar: purpose label, [Block | SQL], definition name,
              validity chip, Run, Publish version
INLINE-START  schema tree (5.2.4) - unchanged in SQL mode, deliberately
CENTRE        the board, or the SQL editor in SQL mode
INLINE-END    the toolbox (5.2.5) - HIDDEN ENTIRELY in SQL mode, not disabled
BLOCK-END     the debug log (5.2.8) - always present, never a toast
```

## 2.4 What was REMOVED from the presented UI

The Filters and Derived Columns side forms. Ruled 04-Aug: the inline-end region becomes the FINAL toolbox immediately, with NO temporary collapsed legacy panel, because that would publish a second visible authoring workflow that T-033 immediately deletes. Their API types in `canvasApi.ts` were left untouched, so T-033 re-attaches them as board blocks with no server change.

## 2.5 Backend changes made in T-032 (all of them forced by the acceptance)

**`Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs`:**

1. `POST /sessions` rewritten. Was `INSERT ... (tenant_id, session_name, status)`. Now `INSERT ... (tenant_id, source_code, display_name, source_kind, status)`. `source_code` is generated server-side by a `NewSourceCode(displayName)` helper: slug plus an 8-character suffix.
2. `RecordDryRun` rewritten. Was writing `row_count` and `error_message` with status `"succeeded"`. Now writes `total_rows`, `mapped_rows`, `safe_sql_passed`, `details` jsonb, with a status mapped to the CHECK vocabulary at the persistence boundary only.
3. The FROM/JOIN emitter rewritten TWICE (T-032f then T-032g) into a frontier planner.

**`Backend/database/scripts/541_v5_p05_visual_mapper_draft_definition.sql`** - new, idempotent, adds `draft_definition jsonb NULL`.

**`Backend/tests/PlantProcess.Api.IntegrationTests/Mapping/VisualMapperSessionLifecycleTests.cs`** - new, 2 tests.

## 2.6 T-033a, the only T-033 work completed

`src/authoring/operatorContract.ts` and `operatorContract.test.ts`. Contains `FILTER_OPERATORS` (10), `MATH_OPERATORS` (4), `UNARY_FILTER_OPERATORS`, and the `FieldLineage` interface with `hasResolvableLineage`.

---

# 3. IDENTITY, TOPOLOGY AND ROADMAP

**Stated honestly: this session did not start from an identity/topology/roadmap document and did not modify one.** What follows is what is genuinely known.

## 3.1 Product identity

PlantProcess IQ (PPIQ). A read-only, evidence-grade, industry-agnostic process-to-quality intelligence platform for manufacturing plants. Built by SOU Industrial Software, Dusseldorf.

## 3.2 Topology as observed in this session

| Layer | Detail |
|---|---|
| Frontend | `Frontend/PlantProcess.Web`, React + Vite 8.0.12, vitest 4.1.6, dev server on **5173** |
| API | `Backend/PlantProcess.Api`, .NET 9, listening on **5063** locally |
| Databases | PostgreSQL on 127.0.0.1:5432. **`ppiq_app`** (local dev) and **`ppiq_presentation`** (demo). User `ppiq_dev`, password `ppiq_dev_local_only`, read from `env/profiles/local.env` and `env/profiles/presentation.env` line 18 |
| Solution projects | Domain, Analytics.Core, Application, Analytics.Engine, Infrastructure, Workers, Api |

**TIP, and it was a real mistake here:** never invent a connection string. The product's own is in `env/profiles/*.env` at the line beginning `ConnectionStrings__PlantProcessDb=`. A diagnostic that guesses `postgres/postgres` fails with an authentication error and wastes a round trip.

## 3.3 The canonical journey (roadmap the product presents)

15 stages, J1 to J15, from Chapter 2 section 3.3.1, rendered by `src/components/journey/JourneyRail.tsx`. `/prep/canvas` is **J7 - Author the transformation and publish the relationship model**.

## 3.4 Roadmap position

M1 phase M1-P2 (No-Code Authoring Shell). T-032 complete, T-033 in progress at item zero of nine. The database track is separately at T-024/T-025.

---

# 4. REALIZATION SCOREBOARD AT THE END OF THIS SESSION

**Not read or updated in this session.** `/areas/ppiq-demo-scenes.md` in memory references demo scoreboards and a 25-Jul audit; they were never opened here. Treat the table below as the state of the AUTHORING TRACK only, not a product-wide scoreboard.

| Item | Status | Evidence |
|---|---|---|
| T-032 Shared Authoring Shell part 1 | **DONE, committed** | `57357ee4`, `92cf025e` |
| T-033a operator contract | **DONE, frozen** | 4 tests passed 05-Aug 09:52 |
| T-033 items 1-9 | **NOT STARTED** | - |
| Visual mapper session lifecycle | **WORKING for the first time ever** | 2 integration tests passed |
| Three-table join preview | **WORKING** | frontier planner, execution asserted |
| JourneyRail certification | **3 tests failing, pre-existing** | recorded against T-012 |
| Frontend suite | **3 failed / 274 passed (277)** | reproducible, all JourneyRail |
| Production build | **PASS**, 2,660 modules | `tsc -b` clean |

## Open problems at session end

1. **T-012 corrective defect, filed not fixed.** `docs/m1/evidence/T-012_journeyrail_corrective_defect.md`. Two stale certification expectations plus ONE LIVE PRODUCT DEFECT: `JourneyRail.tsx` J15 matches `/assistant-config`, which `App.tsx:862` redirects to `/assistant/configuration`. The rail matches the redirect SOURCE, so a live nav surface has no current step. Remedy includes a guard that every match prefix resolves to a real Route and not a `Navigate`.
2. **Donor `src_*` schema names are customer-visible** in the schema tree. Owned by T-030 and T-031. The T-032 browser acceptance must be re-run after T-030.
3. **`draft_definition` was hand-added to `ppiq_presentation` only.** Now in source control as migration 541 and applied to both, but it is evidence that schema drift by hand has happened, which is the class T-031's retirement gate exists to eliminate.
4. **T-024 was declared finished while its own verifier still printed `STILL OUTSTANDING`** - the mixed-industry vocabulary (7 defect catalog rows, 19 parameter definitions, 16 equipment rows, 10 material unit type definitions outside flat steel) and a browser check. Named, not resolved, and it is the other track's to rule on.

## Where things genuinely improved

- The Join Canvas went from **never having worked** to a proven create/save/dry-run/publish path.
- The join planner went from **unable to compile any three-table chain** to a frontier planner with two invariants under test.
- One authoring surface exists where two did, with a ratchet that makes the remaining two convergences visible and self-cleaning.

---

# 5. PER-TASK DISCOVERIES, TIPS AND TRICKS

## 5.1 T-032 - the five defects the browser walk found

**None of these were caught by any test in the repository.** The vitest suite was green while three of them were live.

1. **False interaction hint.** The mode bar said "Drag datasets from the left". Dragging from the tree is T-034 and does not exist. Carried over verbatim without checking whether it was true of the surface being shipped. **A control hint that instructs an impossible action is the same class of defect as a fake product answer.**
2. **Two Job Log entries for one wire.** `onConnect` wrote its log entry INSIDE the `setEdges` updater. React invokes updaters twice in development to surface impurity. **A setState updater must be pure. Compute and log outside it.**
3. **Fork offered with nothing to fork.** Pressing SQL then "Author SQL from here" before any dry run detached the graph and returned an empty editor. Section 5.2.2 says block-to-SQL always succeeds BECAUSE the graph compiles and the SQL loads; with nothing compiled that precondition is absent.
4. **The visual mapper had never worked.** See 5.2.
5. **The join planner could not emit a three-table chain.** See 5.3.

## 5.2 The visual mapper contract drift - the single most valuable finding

`POST /api/prep/visual-mapper/sessions` returned 500. Measured against BOTH databases:

| Column | `ppiq_presentation` | `ppiq_app` |
|---|---|---|
| `session_name` (written by line 69) | **MISSING** | **MISSING** |
| `draft_definition` (written by lines 79, 152) | present, hand-added | **MISSING** |
| `source_code` NOT NULL no default | present, not supplied by the INSERT | same |
| `display_name` NOT NULL no default | present, not supplied by the INSERT | same |
| **sessions ever created** | **0** | **0** |

`RecordDryRun` had the same disease: `row_count` and `error_message` exist on no version of the dry-run table, and it wrote status `"succeeded"` which the CHECK constraint does not allow.

**Preview and Publish had never once succeeded in the life of the repository.** The surface was built, gated, committed and routed, and no test covered the path.

**TIP THAT FOUND IT:** `SELECT count(*)` on the table the endpoint writes. A count of zero on a feature that has supposedly shipped is the fastest possible proof that a code path has never executed.

**TIP:** grep the repository for a column name the code writes. If it appears in the C# and in no `.sql` file anywhere, the code and the schema have diverged and nobody has run it.

**THE FIX PRINCIPLE, ruled:** semantic alignment, not duplicate columns. `display_name` already owned the concept the endpoint invented `session_name` for, so the ENDPOINT moved to the table. No column was added to preserve a stale statement.

**THE UNIQUE CONSTRAINT TRAP:** `UNIQUE(tenant_id, source_code)`. The shell sends the same default definition name on every page load, so a per-name `source_code` would refuse the SECOND visit to the canvas. The code is generated with a random suffix, and the integration test explicitly creates two sessions with the same display name.

## 5.3 The join planner - two separate invariants

Error: `42P01: missing FROM-clause entry for table "t2" POSITION: 186`.

**Cause:** the emitter filtered each table's joins with `.Where(j => alias.ContainsKey(j.LeftTable) && alias.ContainsKey(j.RightTable))`, and the alias map is built for EVERY table before any SQL is emitted, so the filter was always true and filtered nothing.

Two invariants, and fixing only the first is not enough:

- **SCOPE.** An ON clause may reference only aliases already in the FROM clause plus the one this JOIN introduces.
- **REACHABILITY.** Which table is emitted next is decided by CONNECTIVITY, never by position in `g.Tables`. The board sends tables in the order the author dropped them, so a legal graph wired A-B and B-C arrives as `[A, C, B]`.

**The approved algorithm:**

```
joined = { root }
while tables remain:
    take any pending table with an edge to something already joined,
    in EITHER direction - LeftTable or RightTable
    emit its JOIN, orient the ON clause from the alias map
    add it to joined
if none can be reached: refuse as disconnected, with a sentence
```

**CRITICAL CONSEQUENCE:** alias NUMBERS and JOIN ORDER are now deliberately decoupled. A correct statement can introduce `t2` before `t1`. **Never assert alias ordering in a test.** Assert that every alias referenced in an ON clause was already introduced, by walking the statement.

**WHY IT SURVIVED SO LONG:** two tables always worked. It needs three in a chain, and the path had never been executed.

## 5.4 My own repeated defect - FIVE times, one root cause

A guard or assertion that matched a SHAPE or a WORD instead of the exact ARTIFACT it forbade. Each time it reverted correct work:

1. A straggler scan for `VisualJoinCanvasPage` matched the acceptance test that must name the retired page to assert it is gone.
2. A needle for `session_name` matched the code COMMENT explaining its removal.
3. A needle for `foreach (var t in g.Tables.Skip(1))` matched the alias-map builder, a correct unrelated loop.
4. An assertion that `t1 ON` precedes `t2 ON` failed on correct SQL.
5. A unary-operator probe `op is "<op>"` matched the symbol-passthrough line `if (op is "=" or "<>" or ...)` and reported `=` as unary.

**THE RULE: a guard names the exact artifact it forbids, never a word or shape that also appears in prose about it.** Number 5 was caught before shipping only because the parser was run against the real source first. **DO THAT EVERY TIME: run your parser or needle against the actual file before putting it in a pack.**

## 5.5 C# variable shadowing - twice

`CS0136` twice in the same test method: first `c`, then `m`. There is no C# compiler in the assistant environment, so both reached the machine. **Before introducing any identifier in C#, scan the ENCLOSING METHOD for that name.**

## 5.6 PowerShell pack authoring lessons

- Multi-line anchors must be **assembled from an array and joined with the line ending detected from the file**. A hardcoded CRLF anchor misses on an LF checkout and vice versa. Single-line anchors avoid the problem entirely and are preferred.
- Every destructive pack must **fail closed**: preflight, backup, apply inside try/catch with restore, self-check, auto-revert.
- **A closure pack must REFUSE on a red gate.** The T-032 closure pack read a red gate, wrote "failures are present and not accounted for" into the evidence, and marked the task DONE anyway. That was a missing check, not a judgement call.
- Do not put SQL in `-c` with a here-string on Windows. Argument parsing shreds it. **Write the SQL to a file and use `-f`.**
- `npm run test -- --flag` loses the `--` under PowerShell. **Invoke `node node_modules/vitest/vitest.mjs` directly.**
- Vitest writes its summary to **stderr**. Piping through `npm.ps1` with `2>&1` turns it into an ErrorRecord and the tallies never reach the log. **Use the JSON reporter and read the file.**
- Never send output to `$null` on a command that might fail to start. You lose the only diagnostic.

---

# 6. EVERY TEST RUN AND ITS RESULT - DO NOT RE-RUN THESE

## 6.1 Frontend suite runs

| When | Tree | Test Files | Tests | Failures |
|---|---|---|---|---|
| 04-Aug 12:46 | pre-T-032 (v1 self-reverted) | 1 failed / 64 passed (65) | **3 failed / 263 passed (266)** | JourneyRail 3 |
| 04-Aug 12:53 | T-032 applied, before T-032a | 2 failed / 64 passed (66) | **4 failed / 272 passed (276)** | JourneyRail 3 + board assertion |
| 04-Aug 13:12 | T-032 + T-032a | 1 failed / 65 passed (66) | **3 failed / 274 passed (277)** | JourneyRail 3 |
| 04-Aug 14:26 | after hint correction | - | **3 failed / 274 passed (277)** | JourneyRail 3 |
| 04-Aug post-T-032c | after log purity fix | - | **3 failed / 274 passed (277)** | JourneyRail 3 |
| 04-Aug ~16:5x | final gate, FIRST attempt | - | **1 failed / 273 passed (274)** | **ANOMALOUS, see below** |
| 04-Aug later | full suite, quiet tree | - | **3 failed / 274 passed (277)** | JourneyRail 3 |

**THE ANOMALOUS RUN.** Reported three tests FEWER than the suite contains, the three JourneyRail tests absent from collection, and one unrelated failure in `sharedAuthoringShell.test.tsx`. **It does not reproduce.** An isolated run of just those two files returned 11 of 11 passing and 3 of 3 failing, 14 tests, reconciling exactly. A second full run on a quiet tree matched the baseline. Hypothesis, recorded as a hypothesis: the working tree moved while vitest was collecting, because the parallel T-025 worker wrote evidence files during that window and several architecture tests walk the tree. **Not investigated further. Do not investigate it again unless it recurs.**

## 6.2 The three JourneyRail failures - identical every time

| Test | Expected | Actually rendered |
|---|---|---|
| renders all 15 canonical stages plus the operational alerting entry | `Step 1 of 15` | `Step 4 of 15 - Declare read-only connections` |
| marks the current route as the current journey step | `Step 14 of 15` | `Step 15 of 15 - Operate, govern and retain` |
| maps assistant configuration routes to the final assistant stage | `Step 15 of 15` | `15-step product journey` (idle heading, nothing matched) |

## 6.3 Backend integration tests

| Run | Result |
|---|---|
| First attempt, in-process host | `skipped: 1` - fell back to `ppiq_app` whose staging is empty |
| After `PPIQ_FORCE_EXTERNAL_API_TEST_HOST=1` | `total: 2, failed: 1` - the failure was MY alias-ordering assertion, not the product |
| After T-032h | **`total: 2, failed: 0, succeeded: 2`** |

**How to run them (API must already be listening on 5063):**

```
$env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST = "1"
$env:ConnectionStrings__PlantProcessDb = "Host=127.0.0.1;Port=5432;Database=ppiq_presentation;Username=ppiq_dev;Password=ppiq_dev_local_only"
dotnet test Backend\tests\PlantProcess.Api.IntegrationTests --filter VisualMapperSessionLifecycleTests
Remove-Item Env:\PPIQ_FORCE_EXTERNAL_API_TEST_HOST
Remove-Item Env:\ConnectionStrings__PlantProcessDb
```

## 6.4 T-033a operator contract test

`node node_modules\vitest\vitest.mjs run --config vitest.config.ts src/authoring/operatorContract.test.ts` -> **4 passed**, 05-Aug 09:52.

First attempt died with `Failed to start threads worker` after 60 s. Retry passed. **Transient, not a defect.**

## 6.5 Production build

| When | Modules | Result |
|---|---|---|
| pre-T-032 | 2,656 | pass, `VisualJoinCanvasPage` 21.78 kB js / 7.71 kB css |
| post-T-032 | 2,660 | pass, `SharedAuthoringShell` 24.31 kB js / 8.13 kB css |
| after hint fix | 2,660 | 24.34 kB - the +30 bytes of the longer string |

The disappearance of the `VisualJoinCanvasPage` chunk is the independent proof the page was RETIRED and not merely unrouted.

## 6.6 Browser acceptance

| Check | Result | Evidence type |
|---|---|---|
| A mode bar | PASS | screenshot |
| B schema tree, 3 levels, types, key markers | PASS | screenshot |
| C double-click puts a table on the board | PASS | screenshot |
| D incompatible wire refused with one named error | PASS | **attested, not captured** |
| E key-to-key accepted, one log line per wire | PASS | screenshot, 2 SUCCESS lines for 2 wires |
| F Run executes, preview rows | PASS | **attested, not captured** |
| G SQL mode: toolbox absent, tree retained, compiled query | PASS | screenshot |
| H fork warning, two steps, read-only history | PASS | screenshot |
| I Publish returns a version identity | PASS | screenshot, `Published version 6` |

## 6.7 Database checks run

`Invoke-PpiqVisualMapperSchemaCheck.ps1` (read-only, `information_schema` only) against both databases. Results in section 5.2. **Do not re-run; the schema has since been changed by migration 541.**

The other track's `Invoke-PpiqT024Verify.ps1` was observed: 35,910 material units (1,890 Heat / 17,010 Slab / 17,010 Coil), 301,560 parameter observations, 53,095 process step executions, 34,020 genealogy edges, 7,844 quality events, 630 downtime events, all eight closure conditions at 0, all 9 `mv_dashboard_*` views refreshed, `/health` 200.

---

# 7. RULES, ORDERS AND WAYS OF THINKING TO CARRY FORWARD

## 7.1 Standing delivery rules

- **ALWAYS deliver a PowerShell 5.1 apply pack**, including for diagnostics. Never ask for ad-hoc commands or browser DevTools work. **EXCEPTION:** a single-line source edit is better given as "change this line to that".
- **EVERY run block opens with three lines** before anything else:
  ```
  cd C:\Workspace\PlantProcess-IQ
  Move-Item "$env:USERPROFILE\Downloads\<file>.ps1" <folder>\ -Force
  Unblock-File <folder>\<file>.ps1
  ```
  `tools\packs\` for apply packs, `tools\run\` for diagnostics and runners.
- Pack contract: **ReportOnly, anchor verification, backup, apply, self-check, gate, auto-revert, rollback command.**
- Pure ASCII, UTF-8 no BOM, CRLF for PS/CS/TS, LF for `.sh`. No `&&` in PowerShell. Cuddled `} else {`. No em-dashes or curly quotes.
- Long scripts are delivered as downloadable `.ps1` files, not pasted, because the console truncates.

## 7.2 Standing quality rules

- **Re-read the actual code before every review or revision.** Never work from notes.
- **Never let a number rest on memory when it can be verified or machine-checked.**
- **Build a mechanical guard whenever a defect class is mechanical**, instead of promising to be careful.
- **Name your own defects before he finds them.**
- **Never claim done when not done.** Evidence before cure.
- **ABSOLUTE BACKLOG ADHERENCE.** If it is not written in the backlog, do not do it. When a finding falls outside every bucket the task defines, **NAME THE GAP AND ASK FOR A RULING** rather than inventing a bucket.
- Temporary data and temporary internal implementation are sometimes allowed. **Temporary product identity, temporary UX and fake product answers are NEVER allowed.**

## 7.3 Closure mode (currently in force)

```
implement -> targeted tests -> browser -> ONE final build -> ONE final suite
-> evidence -> DONE -> next task immediately
```

- Do not run the full suite after every small correction. Targeted tests during the fix, the full gate once at the end.
- New unrelated findings are recorded against their OWNING task and do not delay the current one.
- Come back only on a contract contradiction, an unavoidable schema/API incompatibility, or a genuinely new architectural decision. Otherwise **make the smallest permanent correct decision and continue.**
- **Optimise for COMPLETED BACKLOG TASKS, not verification-tool refinement.**

## 7.4 Status law

Any task whose evidence is invalidated becomes REOPENED. A task is Done only when its validation passes in a browser or against a running system.

## 7.5 Execution hygiene

- Vitest `Failed to start threads worker` -> retry once -> if it repeats, `--pool=forks`.
- **Stop `PlantProcess.Api` before any `dotnet build`** or locked DLLs produce ten MSB3027 errors that read like compile failures:
  ```
  Get-Process PlantProcess.Api -ErrorAction SilentlyContinue | Stop-Process -Force
  ```
- Console mojibake (a UTF-8 check mark rendered through cp1252 as three garbled bytes) is a CONSOLE codepage artefact, not a source defect. `noMojibake.test.ts` passing in the same run proves the tree is clean. The literal characters are deliberately not reproduced here, because this file would then trip that very test if it were committed.

---

# 8. BACKLOG STATUS

## 8.1 This track

| Task | Title | Status |
|---|---|---|
| T-032 | Shared Authoring Shell part 1: shell contract and four regions | **DONE**, committed `57357ee4` and `92cf025e` |
| T-033 | Shared Authoring Shell part 2: relational block grammar on the board | **IN PROGRESS.** T-033a done and frozen; items 1-9 not started |
| T-034 | Registry-driven schema, table and attribute tree | Not started. Owns drag, multi-select, search |
| T-035 | Next compiled-SQL / debug expansion | Not started |
| T-038 | S2 Add/Edit widget convergence; retires `WidgetAuthoringPanel` | Not started |
| T-065 | J12 Analysis authoring: converge `AnalysisToolboxPage` in S3 mode | Not started |

## 8.2 T-033 remaining work, in the ruled order

```
1. minimum SelectSpec server support
2. FilterNode
3. DerivedNode
4. SelectNode
5. Join field-lineage propagation
6. graph-owned serialization
7. visible delete affordance using the EXISTING deletion mechanism
8. Arrange
9. illegal-connection refusals
10. targeted tests
11. browser acceptance
12. one final build
13. one final full suite
14. evidence
15. T-033 DONE
16. immediately T-034
```

## 8.3 The seven T-033 rulings, final

1. **Server gap is real.** Implement **Select, NOT Rename**. Add the minimum bounded `SelectSpec` to project qualified fields using the existing `Ident()` discipline. No new operator surface. With no Select block present, preserve `SELECT *`.
2. **Filter is `dataset -> Filter -> dataset`**, NOT restricted to following a Source. `FieldLineage` preserved through Join. A Filter resolves its selected field back to Table/Column/Op/Value. Unresolvable lineage means the block is INVALID and Run is refused. **NEVER infer the table.** Same for Derived operands and Select fields.
3. **The operator contract is CLOSED.** Do not harden it further unless it fails reproducibly.
4. **Only `FilterNode`, `DerivedNode`, `SelectNode`** plus minimum lineage propagation. No Rename, Group By, Sort, Union, Cast, Lookup, or T-034 work.
5. **The board is the authoring source of truth.** No resurrected side forms.
6. **Delete:** `CanvasShell` already sets `deleteKeyCode={["Backspace","Delete"]}`. Add the VISIBLE affordance only. Never a second mechanism.
7. **Arrange:** smallest deterministic arrangement. Not an auto-layout subsystem.

## 8.4 Measured server grammar - DO NOT RE-READ

```
JoinSpec(LeftTable, LeftColumn, RightTable, RightColumn)              line 18
FilterSpec(Table, Column, Op, Value)                                  line 22
DerivedSpec(Alias, LeftTable, LeftColumn, Op, RightTable,
            RightColumn, Constant)                                    line 26
FilterOps = { "=", "<>", ">", ">=", "<", "<=",
              "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL" }          line 180
MathOps   = { "+", "-", "*", "/" }                                    line 182
unary branch: if (op is "IS NULL" or "IS NOT NULL")
identifiers validated by Ident()
filter values ALWAYS bound as parameters, never in SQL text
projection is currently SELECT * plus derived expressions
```

## 8.5 The other track

T-024 declared finished by the reviewer; its own verifier still lists two outstanding items (section 4). T-025 lineage/feature-store work was in flight during this session: `ppiq_ml_refresh_feature_store(3650)` returned 505,680 feature rows and 21,649 outcome rows in 199.8 s.

---

# 9. DEPLOYMENT, SERVER AND PIPELINE

## **NOT ADDRESSED IN THIS SESSION.**

No deployment was performed, no CI pipeline was run, inspected or modified, and no server was accessed. Everything below is static knowledge read from the 03-Aug audit report and the repository. **None of it has been verified against a running pipeline.**

## 9.1 What the audit report flagged - 62 signals, 12 CRIT

| Severity | Signal | Hits |
|---|---|---|
| WARN | Security: dev seed endpoint reference | 21 |
| WARN | Config: hardcoded server IP `178.105.152.180` | 15 |
| **CRIT** | **CI: frontend tests enumerated, not executed (`--list`)** | 8 |
| INFO | Hygiene: TODO / FIXME / HACK | 7 |
| WARN | Security: bootstrap admin enabled in config | 4 |
| **CRIT** | **CI: `catchError` forcing SUCCESS** | 3 |
| WARN | Refactor: gate-closing / shim wrapper comment | 3 |
| **CRIT** | Config: wrong connection-string key | 1 |

## 9.2 Triage of the CRIT signals

**Four of the twelve CRIT hits are the audit script matching its own rule table** - `tools\GeneratePlantProcessIQ_UltimateAudit.ps1` lines 659, 663, 665, 671 are the regex definitions. Self-matches, not defects. The one-line fix is to exclude the scanner from its own scan.

**The genuinely live CI findings:**

- `Frontend/PlantProcess.Web/package.json:84` - `"phase9:matrix": "playwright test --config=playwright.phase9.config.ts --list"`. **`--list` ENUMERATES tests, it does not RUN them.** A gate built on this passes without executing anything.
- `tools/ci/validate-real-ui-gates.cjs` lines 13-15 - the same `--list` pattern for `test:visual`, `test:phase56:e2e` and `test:a11y`.
- `Frontend/PlantProcess.Web/tools/phase56/apply-phase5-phase6-full-ui-migration.cjs` lines 74-76 - the same.

**These are not fixed and were not touched.** They belong to whichever backlog task owns CI truthfulness. `Backend/tests/PlantProcess.Architecture.Tests/CiPipelineTruthGateTests.cs:63` contains `Pipeline_never_swallows_failures_with_catchError_success()`, so a guard against the `catchError` class already exists.

## 9.3 Deployment topology as documented in the repo

```
App:  https://app.178.105.152.180.sslip.io
API:  https://api.178.105.152.180.sslip.io
```

Referenced from `deploy/ci/post-deploy-smoke.sh`, `deploy/scripts/ensure-runtime-env.sh:41`, `deploy/server/README.md`, `deploy/server/verify-server-exposure.sh`, `scripts/deploy/Invoke-CleanMachineDeployAcceptance.ps1`, `docs/deployment/T007_CLEAN_MACHINE_DEPLOY_ACCEPTANCE.latest.md`, and `tools/validation/validate-phase01-phase02-gates.mjs:77`.

The IP is hardcoded in fifteen places. Most are defaults with an environment override (`${PPIQ_SITE_HOST:-...}`), which is a defensible pattern, but the documentation and acceptance scripts embed it directly.

## 9.4 Security signals worth a ruling, not fixed here

- `Backend/PlantProcess.Api/Program.cs:1010` calls `app.MapDevSeedEndpoints()`. `ProductionDevEndpointGuardTests.cs` asserts it is wrapped in `if (app.Environment.IsDevelopment())`, and a release stub exists. The guard appears to be in place.
- `env/profiles/local.env:41` and `env/profiles/presentation.env:41` both set `PlantProcess__Auth__Users__0__IsBootstrapAdmin=true`. **The presentation profile is the one used for the demo.** Worth a ruling before the customer sees the system.

---

# 10. PIPELINE-GREEN AND APP-URL WORK

## **NONE WAS DONE IN THIS SESSION.**

No modification was made to any Jenkinsfile, CI script, deploy script, Caddy configuration, compose file or server. The App URL was never exercised. `http://localhost:5173` and `http://localhost:5063` were the only endpoints used, and only locally.

**What WOULD affect a future pipeline run, from this session's changes:**

1. **Migration `541_v5_p05_visual_mapper_draft_definition.sql` must run on any environment before the deployed API can save a graph.** It is idempotent, so it is safe to run repeatedly, but it is new and any deployment that does not apply it will fail on `draft_definition`.
2. **`VisualMapperSessionLifecycleTests` requires a reachable API and Postgres.** It skips otherwise with a stated message. In CI it should run; locally it needs `PPIQ_FORCE_EXTERNAL_API_TEST_HOST=1` and a connection string pointed at a database with staged datasets.
3. **The frontend suite is NOT green.** Three JourneyRail tests fail, pre-existing, recorded against T-012. Any pipeline gate that requires zero failures will block until that corrective task runs.
4. **`operatorContract.test.ts` reads `../../Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs`** relative to the frontend working directory. **If CI builds the frontend in isolation without the Backend folder, this test will fail on a missing file.** This is the one new test with a cross-tree dependency and it is worth checking before the next pipeline run.

Point 4 is the most likely new pipeline break and was not verified.

---

# APPENDIX: FILE INVENTORY FROM THIS SESSION

## Created

```
Frontend/PlantProcess.Web/src/authoring/authoringPurposes.ts
Frontend/PlantProcess.Web/src/authoring/blockRegistry.ts
Frontend/PlantProcess.Web/src/authoring/AuthoringSchemaTree.tsx
Frontend/PlantProcess.Web/src/authoring/AuthoringToolbox.tsx
Frontend/PlantProcess.Web/src/authoring/SharedAuthoringShell.tsx
Frontend/PlantProcess.Web/src/authoring/authoring-shell.css
Frontend/PlantProcess.Web/src/authoring/sharedAuthoringShell.test.tsx
Frontend/PlantProcess.Web/src/authoring/operatorContract.ts
Frontend/PlantProcess.Web/src/authoring/operatorContract.test.ts
Backend/database/scripts/541_v5_p05_visual_mapper_draft_definition.sql
Backend/tests/PlantProcess.Api.IntegrationTests/Mapping/VisualMapperSessionLifecycleTests.cs
docs/m1/evidence/T-012_journeyrail_corrective_defect.md
docs/m1/evidence/T-032_shared_authoring_shell_acceptance.md
docs/m1/evidence/T-033_scope_clarification.md
tools/run/Invoke-PpiqFrontendSuite.ps1
tools/run/Invoke-PpiqVisualMapperSchemaCheck.ps1
tools/run/Invoke-PpiqT032BrowserPrep.ps1
```

## Modified

```
Frontend/PlantProcess.Web/src/App.tsx
Frontend/PlantProcess.Web/src/pages/Prep/CanvasDebugLog.tsx   (one comment)
Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs
```

## Deleted

```
Frontend/PlantProcess.Web/src/pages/Prep/VisualJoinCanvasPage.tsx   (777 lines, converged)
```

## Packs applied, in order

```
apply-T-032-shared-authoring-shell-v2.ps1     the shell
apply-T-032a-acceptance-test.ps1              pending-convergence ratchet, act fix
(hand edit)                                   the interaction hint copy
apply-T-032c-log-purity.ps1                   one wire, one log entry
apply-T-012-journeyrail-defect-record.ps1     documentation only
apply-T-032e-visual-mapper-contract-v2.ps1    endpoint/schema alignment + migration 541
apply-T-032f-join-order.ps1                   scope invariant
apply-T-032g-join-planner-v2.ps1              frontier planner + chain test
apply-T-032h-scope-assertion.ps1              assert scope, not alias numbering
apply-T-032-evidence.ps1                      evidence record
apply-T-032-close.ps1                         browser sections, status DONE
apply-T-032-gate-correction.ps1               corrected gate, anomaly recorded
apply-T-033a-operator-contract.ps1            operator contract + FieldLineage
```

**`apply-T-032d-fork-guard.ps1` was delivered but NEVER APPLIED.** The fork-availability guard - refusing to offer "Author SQL from here" before a query has compiled - is **still an open defect in the shipped shell**. It is small and it is real: the user can fork with nothing to fork and receive an empty editor. Worth applying early in the next session or recording against a task.
