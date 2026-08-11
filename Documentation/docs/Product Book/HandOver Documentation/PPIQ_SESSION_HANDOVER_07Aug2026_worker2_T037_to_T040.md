# PPIQ SESSION HANDOVER — Worker 2 — 06/07-Aug-2026
## T-037 → T-038 → T-039 → T-040, plus the presentation-eve data corrections

> **READ THIS BEFORE DOING ANYTHING.** Everything below was measured on this
> machine during this session. Where something is a measurement, it says so and
> gives the number. Where something is unproven, it says that too. Do not re-run
> what section 6 records as already run — every result there is real output from
> this session, not a prediction.

---

# 0. WHERE THINGS STAND RIGHT NOW (07-Aug, ~17:00)

| Item | Status |
|---|---|
| T-037 role-binding capability | **DONE, frozen by him** |
| T-038 S2 convergence | **IMPLEMENTATION COMPLETE**, browser rows deferred |
| T-039 IDefinitionService | **DONE**, closure record filed |
| T-040 Golden Gate | **PARTIAL** — 01d, 02b, 03a1, 03a2 applied and green. **03b2 keyboard pack NOT applied.** Browser convergence run NOT done |
| API | **RUNNING** after two corrections tonight |
| Presentation data | Equipment/Area labels fixed; six widgets re-bound |
| Frontend full suite | **481 passed / 3 failed** (the 3 are the known T-012 baseline) |
| Git | Track files show as **already committed** — see section 7.6, this is unresolved |

**THE SINGLE MOST IMPORTANT UNFINISHED THING:** T-040 pack 03b2 (keyboard path)
is written, validated and delivered but **was never applied**. The file is
`tools\packs\apply-T-040-03b2-keyboard-path.ps1` if it was downloaded; if not it
must be rebuilt from section 5.4.

---

# 1. SESSION ACTIVITY LOG, WITH WHAT EACH STEP TAUGHT

## 1.1 T-037 — certify returned-column role mapping in the S2 shell

**Two bounded measurements were run first, against the 05-Aug 23:21 dump rather
than from notes. This is the pattern he approved and it should continue.**

MEASUREMENT 1 — `src/api/product-core/widget-role-binding.ts`, 119 lines, SHA
`AE65BF79...`, untouched since 29-Jul. Exports `WidgetRole`
(`category|value|secondary`), `WidgetRoleBinding`, `EMPTY_ROLE_BINDING`,
`readRoleBinding`, `writeRoleBinding`, `staleRoles`, `describeStale`. It
persists **inside `displayOptionsJson` under key `roleBinding`**, merging so
every other key survives; keyed **by column CODE, never index**; a malformed
blob is treated as absent. `describeStale` already returns `category
(shift_code)` — it names the column. The task text was accurate: hardening, not
a build.

MEASUREMENT 2 — **the blocker.** `SharedAuthoringShell.tsx` (846 lines then) had
ZERO references to role binding. The module's only consumers were
`LiveWidgetChart.tsx` (reader) and `WidgetAuthoringPanel.tsx` (writer). Worse:
**the S2 face had no query surface at all.** S2 carries
`showsStagingCatalogue: false`, so the catalogue effect sets `catalogue` to
`[]`; `addableBlockIds` is `[]`; the board can hold nothing; `graph` is null;
`doFork` refuses with "There is no compiled definition to fork from yet."

**HIS RULING (important, it set the pattern for the rest of the session):** the
S2 door is T-038's, not T-037's. Do NOT create it one task early. T-037 =
capability; T-038 = adoption + door. **Extract the role-selection UI as a shared
capability; do not leave it trapped in the panel T-038 retires.**

**HIS PERSISTED-VS-VISIBLE RULING:** stored keys stay `category|value|secondary`
forever; presented labels are **Axis | Value | Series** (Chapter 4 §5.1.11).
A presentation mapping, never a migration.

DELIVERED: `roleBindingPresentation.ts` (words only), `RoleBindingFields.tsx`
(the shared capability), `role-binding.css`, `roleBinding.test.tsx`.
`WidgetAuthoringPanel` was rewired to consume the shared component.

**FINDING recorded, not fixed:** Chapter 4 §5.1.12 requires "retitle when you
repoint" and re-mapping when returned columns change. Filed against T-038.

**MY DEFECT (Family D, first of many):** the pack script is written CRLF, so
every PowerShell here-string carries CRLF between its lines, while `ReadLf`
normalised the FILES to LF. **A multi-line anchor could therefore never match.**
All three anchors returned 0 — including one in a different file, which is the
tell: content drift cannot hit two unrelated files at once. FIX: normalise BOTH
sides and assert no needle contains CR.

## 1.2 T-038 — S2 convergence (five packs)

**MEASURED POSITION.** Entry points already converge on one component, so T-038
is a SUBSTITUTION not a rewiring. `InteractiveWorkspacePage.tsx` holds
`wizardOpen` and `editing` at the top of the component (a comment records that
their position was itself a bug fix — they were below guard clauses and became
conditional hook calls). The panel is lazy-imported behind a `WizardBoundary`.

**THE FINDING THAT SHAPED THE TASK: S2's query is NOT SQL.** The panel calls
`executeWidgetQueryExpression` → `POST /analytics/dashboard/widgets/execute`,
backed by `IWidgetQueryExpressionService` (418 lines), which parses a keyword
expression into dimensions/measures/filters/sort/limit/time-window and refuses
with UnknownKeyword / MissingValue / TypeMismatch / InvalidGrammar. The shell's
SQL mode calls `runAuthoredSql` → `/api/prep/sql/run` → `SafeSqlValidator` over
**staging**. Routing S2 through the S1 path would change both the data boundary
and the safety contract.

**HIS ARCHITECTURE CORRECTION — remember this one:** I proposed making the mode
name purpose-specific (S1 "SQL" = SQL, S2 "SQL" = expression). He refused:
**a visible mode name must never stand for two different languages.** He also
refused my invented `Catalogue | Query Expression` toggle as a way to satisfy
§5.2.2 — until I produced evidence the shipped panel ALREADY had a
Catalogue/Query toggle, at which point he ruled it preserved, not invented.

**CHAPTER 4 §5.2.2 CONTRADICTION, still open:** the section is titled "The two
modes" and states a toggle sits at the block-start of **every one of the five
purposes**, always exactly two, and its table names them "Block and wiring
diagram" and "SQL". §5.2.1 gives S2 a palette of relational plus aggregation
blocks — the chapter expects S2 to have a board. Satisfying it literally would
give S2 a SQL mode it has no contract for. **RECORDED AS A BIBLE-VS-
IMPLEMENTATION DIVERGENCE owned by whichever later task gives S2 its board.**

### Pack sequence and what each produced

**01 — the definition model** (`widgetDefinitionModel.ts`, pure).
His invariant: existing saved widget → load into S2 state → compile with no
edits → the payload is contractually the same widget. Preserve widget identity,
dashboardDefinitionId, query expression, display options, roleBinding, title and
configuration, and **existing unrelated displayOptionsJson keys**.
The panel's randomness inside `slug()` is deliberately NOT carried — a pure
function cannot own randomness, so the caller supplies the code suffix, which is
also what makes the create path testable.
**EDGE NAMED OPENLY:** the panel nulls `parameterCode` when no chosen field
declares `requiresParameterCode`. Carried unchanged, so if server metadata ever
stopped requiring a parameter, an untouched reload-and-save would drop a saved
parameterCode. Shipped behaviour, recorded rather than quietly improved.

**02a — WITHDRAWN.** I proposed a global `bindingSurface` taxonomy
(`preparation | widget-query | unbound`) across all five purposes. **He refused
it: T-038 owns S2 convergence, not a global redefinition of authoring modes.
Do not encode "S3-S5 = unbound but show preparation semantics" as architectural
truth.** It also failed its own `tsc -b` gate — see 1.5.

**02a2 — the reduced version.** ONE optional field on the S2 row only:
`queryContract?: "widget-query-expression"`. Optional so a purpose declaring
nothing behaves exactly as before, which is what lets S1/S3/S4/S5 stay untouched.

**02b3 — the S2 face** (`S2QueryBinding.tsx`) plus the legacy label adapter.

**THE DEFECT THIS UNCOVERED, and it is a real product bug:**
`WidgetAuthoringPanel` built its returned-column list as `columns.map(c => c.label)`
and stored a **LABEL**; `SavedDashboardWidget` resolves the binding with
`result.columns.map(c => c.code)` and runs `staleRoles` against **CODES**.
Unless label equals code for every column, every query-bound widget's saved
mapping reads as stale at render time and the chart falls back to inference —
the exact bug `widget-role-binding.ts` exists to close.

**HIS RULE FOR IT** (implemented in `roleBindingCompat.ts`):
```
token equals a returned column CODE      -> current binding
otherwise equals EXACTLY ONE column LABEL -> legacy, resolve to that code,
                                            persist the code on next save
otherwise                                 -> stale/ambiguous, explicit remap
```
**No fuzzy matching. No index matching. No first-column-wins. No silent
resolution when two columns share a label.** A self-check scans the adapter for
`toLowerCase`, `trim()`, `startsWith` and `includes(` and fails if any appears —
the rule is exact-or-nothing and that is mechanical.

**02c — shell wiring.** The gate is the REGISTRY FLAG, not a purpose name:
`definition.queryContract === "widget-query-expression"`. When true the CENTRE
renders `S2QueryBinding` instead of the board, and the mode bar drops the
Block/SQL toggle, the validity chip, Run, Publish and the block-mode hint — none
mean anything for a query purpose, and the SQL button would put the forbidden
word on S2.

**03a — definition in, save out.** Four new shell props, ALL OPTIONAL:
`dashboardDefinitionId`, `existingWidget`, `onSaved`, `onClose`. **The prop is
`existingWidget`, not `definition`, because `definition` already means the
purpose definition inside that component — a collision caught before writing.**
The face gained `onCatalogue` so the shell compiles the payload from the SAME
single fetch. **MECHANICAL GUARD:** the self-check fails if the shell contains
`widgetCode:`, `displayOptionsJson:` or `isSynthetic` — those would mean the
shell had started assembling a payload instead of compiling through
`toWidgetPayload`, which is the only reason Edit can be trusted.

**03b2 — substitution and retirement.** Page lazy-loads `SharedAuthoringShell`,
renders it once with `purpose="S2"`. `wizardOpen` → `authoringOpen`,
`WizardBoundary` → `AuthoringBoundary` with its two messages reworded (he ruled
the word is a leftover the design does not use, so it leaves the file entirely).
`WidgetAuthoringPanel.tsx` and `.css` DELETED after backup.
**Preflight walks every .ts/.tsx/.css under `src` and refuses if any module this
pack does not own still names the retiring surface.** That guard caught four
files of mine — see 1.5.

## 1.3 T-039 — IDefinitionService

**PREMISE VERIFIED:** `definition_store`, `definition_versions`,
`definition_dependencies` and `IDefinitionService` return **zero hits** across
every uploaded category file.

**THE CONTRADICTION I RAISED:** widget definitions are NOT versioned today.
`DashboardWidgetDefinition` has no version number, no immutable version row, no
history table. The only widget-side history is
`dashboard_widget_expression_audit`, which records expression EXECUTIONS, not
definition snapshots. What IS versioned: `ppiq_visual_mapper_versions`
(session_id keyed) and `ppiq_mapping_versions` (mapping_code keyed).
So the frozen validation — create a WIDGET, read by version, update, two
versions exist — could not pass over today's persistence.

**HIS RULING: Option A.** M1 is allowed a HIDDEN TRANSITIONAL adapter/storage
while exposing the final contract. A minimal additive Widget version store is
correct. Do NOT implement the Chapter-3/M2a `definition_store`. Smallest
snapshot storage: `definition_kind, definition_id, version_number, payload_json,
created_at, created_by` with UNIQUE on the first three.
**Current-definition mutation and snapshot insertion MUST be atomic. Version
allocation must be server-owned and concurrency-safe inside the same
transaction — no unprotected MAX+1 outside the serialised write.**

**THE PACK-03 REVERSAL — read this before touching Transformation.** I measured
that the two Transformation stores do not share a durable identity:
`ppiq_visual_mapper_versions` is keyed by **session_id** (an authoring
workspace, not a definition — two sessions over the same transformation each
start at 1); `ppiq_mapping_versions` is keyed by **mapping_code** (text) with a
per-VERSION uuid. `IDefinitionService` takes a `Guid definitionId` and **neither
store has a definition-level Guid.** The only bridge is the `MappingDefinition`
entity (Guid Id + MappingCode) and **nothing links it to
`ppiq_mapping_versions.mapping_code` by constraint** — they share a naming
convention, not a key.
**HE RULED: do NOT build the Transformation adapter in T-039.** Do not invent a
Guid-to-code convention, a new identity table, a session-as-definition semantic,
or a partial adapter presented as complete. Those are M2a decisions.

**CLOSURE WORDING HE DICTATED, use it verbatim if asked:** the contract
REPRESENTS eleven definition kinds; **it does NOT mean eleven persistence
adapters are implemented. Widget is the certified M1 implementation. Unsupported
kinds must continue to return an explicit refusal rather than a synthesised or
fictitious version history.**

## 1.4 T-040 — Golden Gate (in progress)

**DEFINED TERMS RESOLVED:** the seven states are **Empty, Loading, Populated,
Filtered-empty, Blocked, Refused, Failed** (Chapter 2 line 17, T-005 gate). The
Golden Gate is **`docs/m1/ACCEPTANCE.md`**, 23 lines G01–G23, created by T-005.
Its rule: a screen is not Green until every line is ticked **with an evidence
file name beside it** — a tick without evidence is an opinion.

**THE RAW-ERROR CLAUSE WAS ALREADY SATISFIED, and not by luck.**
`noRawErrorStrings.test.ts` scans only .ts/.tsx under src, skips tests and
stories, and allowlists exactly `DataFetchBoundary.tsx` and `ErrorBoundary.tsx`.
All dump occurrences are in test files, stylesheets or those two components.
T-040 KEEPS this green rather than fixing it.

**G09 WAS THE REAL FINDING: 19 physical-direction declarations** across seven
certified stylesheets. Two of them are NOT spacing — the schema tree's
`padding-left: 20px` indent is how a column reads as belonging to its table, and
the debug log's `border-left` severity stripe is how a severity reads at a
glance. Under RTL a physical left puts both on the wrong edge, which does not
look untidy — **it looks like different data**.

**THE SEVEN STATES:** `authoringStates.ts` decides from facts with deliberate
precedence — a failure hides everything because nothing below it is known; a
refusal outranks a blocker because the server already answered; loading outranks
a stale result about to be replaced. His three required distinctions are each a
test: Empty ≠ Filtered-empty, Blocked ≠ Refused, Refused ≠ Failed.
**Colour never carries meaning** — every non-populated state has a sentence and,
where the author can act, an action.

**THE EXECUTION-STATE FINDING:** the shell had **no in-flight state at all** —
no `busy`, no `running`, nothing. Transport failures went straight to the log and
were never held. So **two of the seven states could not be represented honestly**.
He agreed this is missing wiring, not scope expansion, and ruled the shape:
`activeRun = none | preview | sql | expression`, `lastRunFailure`, shell is the
single owner, **S2's local `running` removed** so the two can never disagree.

**THE CENTRE WRAPPER.** Before adding it he required a dependency check. Result:
**nothing depends on the three centre faces being direct grid children.**
`.canvas-page` is the grid (`250px 1fr 320px`, `height: calc(100vh - 130px)`);
`.canvas-sqlpane` has NO block-level rule at all; `.ppiq-canvas` uses
`height: 100%`; no selector uses a direct-child combinator. Exactly one
responsibility moves — occupying the 1fr cell — and no rule is duplicated.


## 1.5 MY OWN DEFECTS THIS SESSION — the most valuable section for you

Fourteen pack defects. **Every one was caught by a gate, a preflight or a
self-check. None reached a bad commit.** They cluster into five classes, and
knowing them will save you hours.

### CLASS A — a guard reading the prose that describes it (SIX instances)

This was the dominant failure. A pack forbids a literal, and then a comment in
the delivered file *explains* the rule by naming that literal, so the guard
condemns correct work.

1. T-038 pack 02: the S2 face's header comment spelled `/api/prep/sql/run` while
   saying the face does not use it. Self-check reverted a correct pack.
2. T-039 pack 02: the 770 script's header named `definition_store` and
   `definition_dependencies` while disclaiming them. Preflight refused.
3. T-039 pack 04: the self-check **counted** `ExcludeFromMigrations` and demanded
   exactly one; the replacement has two — the call and the comment explaining it.
   **This one reverted a working fix while the API was down.**
4. T-040 pack 03a1: the CSS guard scanned for `overflow` and matched the comment
   saying `.preview-scroll` remains the only overflow in the centre.
5. T-040 pack 03b: the guard scanned for `tabIndex` and matched the comment
   saying nothing there assigns a tabIndex.
6. T-038 pack 03b: four files I had written in earlier packs NAMED the retiring
   surface in their comments, so the retirement ratchet would have failed
   immediately after apply.

**THE RULE, now implemented everywhere:** any scan — forbidden literal, count
guard, CSS check, code check — runs against the artifact's **meaning-bearing
text with comments stripped**. A `StripComments` helper removes block comments
and `//` / `///` lines. Never scan prose that describes the scan.

### CLASS B — PowerShell syntax the generator should have caught (THREE)

7. A trailing comma on the last element of an array literal. PowerShell rejects
   `@( ..., )`.
8. `CountOf $after "<div className=" + [char]34 + ...` — **an unparenthesised
   concatenation passed as a command argument becomes SEVERAL arguments**, so the
   needle silently became its first fragment and the count was meaningless.
9. A stray `),` inside a `Say` line.

**GENERATOR-SIDE CHECKS NOW RUN BEFORE EVERY DELIVERY** (and one of them caught
#9 before it shipped):
- no `,` followed by whitespace and `)` **in the scaffolding only** — the payload
  is TypeScript/CSS where that is ordinary and correct, and checking it was a
  false alarm waiting to happen
- here-string opens == here-string closes
- pure ASCII
- no bare concatenation following a command name
- every `-ne 1` count guard holds against the **comment-stripped** replacement

### CLASS C — the test was wrong, not the code (THREE)

10. A **vacuous** assertion: `queryByLabelText("Dimension, optional")` returns
    null whether or not the control renders, because the visible text is a
    `<span>` and the accessible name is the `aria-label`. Its positive twin
    failed loudly, which is the only reason the vacuous one was found.
11. A **vocabulary** assertion: I asserted the word `filters` must be absent from
    the Empty sentence, but Empty deliberately says "carries no filters" — that
    IS the distinction. Testing the vocabulary instead of the distinction.
12. A **wait on a fixture that was never provided**: four tests awaited
    `staging_one` while the mock returned an empty array, so the tree renders
    "No staged datasets".

**RULE: when a component test fails, read the rendered DOM in the failure output
BEFORE touching the component. It usually shows the component was already right.**

### CLASS D — types and environment (TWO)

13. `authoringModes.ts` imported `AuthoringBindingSurface` from
    `authoringPurposes` without re-exporting it, while the test imported that
    type **from `authoringModes`**. vitest passed 179 because esbuild strips
    types without checking; `tsc -b` exited 2. **Import every type from the
    module that DECLARES it.** This is why `tsc -b` is a mandatory gate.
14. The T-039 integration test built `DbContextOptions` with `UseNpgsql` alone
    while `AddInfrastructure` builds it with
    `UseNpgsql(...).UseSnakeCaseNamingConvention().AddInterceptors(...)`. Without
    the convention EF emits PascalCase columns →
    `42703: column "Id" of relation "dashboard_definitions" does not exist`.
    **A test that builds a context by hand must build it the way production does.**

### CLASS E — the one that reached him

15. **Mapping an EF entity to a table created outside migrations REQUIRES
    `ExcludeFromMigrations()`, or the application will not boot.** Adding
    `DefinitionVersion` to `PlantProcessDbContext` while its DDL lives in
    numbered script 770 left the model with "pending changes" and `Migrate()`
    refused. **My pack gates were build-and-test only; nothing started the API,
    so this survived to his first start and cost him a crash.**
    **ADD AN API-START CHECK TO ANY PACK THAT TOUCHES THE DbContext.**

### TWO SURPRISES THAT WERE NOT MINE

- **A revert reported "done" and did not take.** 03a1d's auto-revert printed
  success while the shell, the stylesheet and the test file all stayed applied.
  Cause unknown — possibly a file handle held by the just-finished vitest run.
- **Files changed between two runs where the first wrote nothing.** 03a2 aborted
  in preflight before any write, yet the shell contained `activeRun` on the next
  invocation.

**CONSEQUENCE, now in the pack contract:** a revert that reports success without
verifying it is a false assurance. Every pack from 03a2 onward **re-reads each
file after restoring, confirms its marker is gone, and says plainly if it is not,
naming what to restore by hand.** And every pack **reads the tree first and
reports whether it is already applied** rather than only refusing.

**A shared working tree cannot be reasoned about from what a pack printed. Read it.**

### THE CREATE-GUARD LESSON

A leftover from a reverted run is not a collision. The guard now decides by
**content**: byte-identical to what the pack writes means it is our own leftover
and is safe to rewrite; anything else still refuses. Existence alone was never
the question.

### THE ANCHOR-DESIGN LESSON

**An anchor that is a PREFIX of its own replacement cannot detect whether the
pack already ran.** The T-040 CSS anchor appends after itself, so it matched
whether or not the pack had applied. Append-style edits need a separate
already-applied marker. The shell had one (`canvas-centre`); the stylesheet did not.

### THE SHARED-REPOSITORY LESSON

**A pack gate must certify the pack's own surface.** Running the whole
architecture folder made a correct pack fail on the parallel assistant track's
in-flight files. A gate wide enough to fail on another worker's work blocks
correct work and tempts the wrong fix. The full folder still runs at the
convergence boundary, where a failure can be assigned to the file's owner.

---

# 2. THE IMPLEMENTATION AS IT NOW STANDS

## 2.1 Files CREATED this session

**`Frontend/PlantProcess.Web/src/authoring/`**
| File | Purpose |
|---|---|
| `roleBindingPresentation.ts` | Words only. ROLE_ORDER, roleLabel (Axis/Value/Series), rolePlaceholder, describeStaleBinding, ROLE_STABLE_HINT. Delegates detection to `staleRoles` |
| `RoleBindingFields.tsx` | The shared role-binding capability. Free of every S2 assumption |
| `role-binding.css` | Own `ppiq-rolebind` namespace so the retired panel's stylesheet is not needed |
| `roleBinding.test.tsx` | 9 tests — T-037 acceptance |
| `widgetDefinitionModel.ts` | Pure. loadS2State, toWidgetPayload, saveTarget, saveRefusal, chartCapabilities, requiresParameter, widgetCodeFor, parseFilterJson/toFilterJson |
| `widgetDefinitionModel.test.ts` | 21 tests including the round-trip invariant |
| `s2QueryContract.test.ts` | 5 tests — only S2 declares a query contract |
| `S2QueryBinding.tsx` | The S2 face: catalogue form, expression editor, Run test, returned columns, RoleBindingFields |
| `s2-query-binding.css` | `ppiq-s2` namespace + `.canvas-s2pane` |
| `s2QueryBinding.test.tsx` | 17 tests |
| `roleBindingCompat.ts` | The legacy label adapter — exact-or-nothing |
| `roleBindingCompat.test.ts` | 12 tests |
| `s2ShellSave.test.tsx` | 8 tests — Edit round trip at the surface |
| `workspaceEntryPoints.test.tsx` | 7 tests — Add and Edit reach one surface |
| `authoringStates.ts` | The seven states + `toAuthoringStateFacts` |
| `AuthoringStateBanner.tsx` | One component for all seven |
| `authoring-states.css` | Palette roles only, born G09-clean |
| `authoringStates.test.tsx` | 23 tests (13 + 10 derivation) |
| `authoringCentreRegion.test.tsx` | 5 tests — the centre wrapper |
| `authoringKeyboard.test.tsx` | 8 tests — **NOT YET APPLIED** |

**`Frontend/PlantProcess.Web/src/test/architecture/`**
- `authoringLogicalDirection.test.ts` — 4 tests, G09 ratchet over 7 certified stylesheets

**`Backend/`**
- `PlantProcess.Application/Definitions/DefinitionKind.cs` — 11 members, **0 reserved** so an unset value cannot pass as a kind
- `PlantProcess.Application/Definitions/Contracts/DefinitionSnapshot.cs`
- `PlantProcess.Application/Definitions/Interfaces/IDefinitionService.cs` — six methods, with the two implementation rules stated IN the contract
- `PlantProcess.Domain/Entities/Definitions/DefinitionVersion.cs` — immutable by construction; only `MarkPublished` can change
- `PlantProcess.Infrastructure/Persistence/Configurations/Definitions/DefinitionVersionConfiguration.cs`
- `PlantProcess.Infrastructure/Definitions/DefinitionService.cs` — Widget only, everything else refused
- `database/scripts/770_t039_definition_version_store.sql` — idempotent, UNIQUE(kind,id,version), guarded grants
- `tests/PlantProcess.Application.UnitTests/Definitions/DefinitionKindTests.cs` — 4 facts
- `tests/PlantProcess.Infrastructure.IntegrationTests/Definitions/WidgetDefinitionVersioningTests.cs` — 2 SkippableFacts

**`docs/m1/evidence/T-039_CLOSURE.md`** — the closure record with the two-store evidence table.

## 2.2 Files MODIFIED

| File | What changed |
|---|---|
| `SharedAuthoringShell.tsx` | +queryContract gate, S2 face in centre, mode bar suppression, four optional props, save path, `activeRun`/`lastRunFailure`/`s2RowCount`, the six facts, centre wrapper, banner. **Keyboard handler still pending (03b2)** |
| `S2QueryBinding.tsx` | onCatalogue, then local `running` REMOVED in favour of a prop + `onRunLifecycle` |
| `authoringPurposes.ts` | `WidgetQueryContract` type + optional `queryContract` on the S2 row only |
| `IPlantProcessDbContext.cs` / `PlantProcessDbContext.cs` | `DbSet<DefinitionVersion> DefinitionVersions` + the namespace import in BOTH |
| `Infrastructure/DependencyInjection.cs` | `IDefinitionService` → `DefinitionService` |
| `InteractiveWorkspacePage.tsx` | Lazy-loads the shell, `authoringOpen`, `AuthoringBoundary`, one `<SharedAuthoringShell purpose="S2">` |
| `sharedAuthoringShell.test.tsx` | Ratchet list extended, S2 region test rewritten, retirement tests added, dashboarding mock added |
| 7 stylesheets | 19 physical-direction declarations → logical |
| `AuthoringSupportEndpoints.cs` | One stale comment corrected (comment-only, proved by residue) |
| `DashboardWidgetQueryService.cs` | **Dimension label resolution** — equipment and area show names, keys unchanged |
| `DefinitionVersionConfiguration.cs` | `ExcludeFromMigrations()` |

## 2.3 Files DELETED

- `Frontend/PlantProcess.Web/src/components/dashboard/widget-authoring/WidgetAuthoringPanel.tsx`
- `...WidgetAuthoringPanel.css`

Backed up in `tools\packs\_backup_T-038-03b2-retirement_*`.


---

# 3. IDENTITY, TOPOLOGY AND ROADMAP — where we started and how far we moved

## 3.1 Product identity (unchanged, and it governs every decision)

PlantProcess IQ is a **read-only, evidence-grade, industry-agnostic
process-to-quality intelligence platform** for manufacturing plants. Read-only is
not a limitation to apologise for — it is the reason a plant will let it near
their data.

**The laws that actually bit this session:**
- **Rule 1 — no plant vocabulary in the product.** Every list comes from the
  server. The ratchet enforces it by scanning six authoring sources for raw
  `<select>`, `<input>`, `<label>`, `<button>`, `<table>`, `<textarea>` and
  inline `style={{`.
- **§5.2.8 — the debug log is the authoritative surface for every refusal, every
  preview and every publish. Never a toast.**
- **No number without evidence** (Golden Gate G23). A tick without an evidence
  filename is an opinion.
- **No fabricated answers.** Temporary data and temporary internal
  implementation are sometimes allowed; **temporary product identity, temporary
  UX and fake product answers never are.**

## 3.2 The five authoring purposes (the S-topology)

| Purpose | Authors | Status after this session |
|---|---|---|
| S1 Data preparation | Staged data → plant schema | Shipping. Board + SQL, staged catalogue |
| **S2 Widget and page binding** | The dataset a widget displays | **CONVERGED THIS SESSION.** Add/Edit both open the shared shell; query expression over the canonical model; role binding; save |
| S3 Analysis | Analysis definitions | No entry point. Its own convergence task (T-065) |
| S4 Model | Model definitions | No entry point |
| S5 Log rules | Info/warning/error rules | No entry point |

**The shell's four regions (T-032 contract, preserved):** mode bar at
block-start, schema tree at inline-start, the centre, the toolbox at inline-end.
T-040 added a **local container inside the centre** — not a fifth region.

## 3.3 Data topology as measured tonight on `ppiq_presentation`

| Table | Rows |
|---|---|
| ppiq_t025_rowprint | 527,329 |
| ml_feature_values | 505,680 |
| parameter_observations | 301,560 |
| process_step_executions | 53,095 |
| material_units | 35,915 |
| genealogy_edges | 34,024 |
| staging_records | 16,640 |
| quality_events | 7,844 |
| downtime_events | 630 |
| risk_scores | 500 |
| equipment | 34 |
| canonical_equipment | 18 |
| areas | 12 |

**Dimensions with genuine spread (0% unknown) — these are the demo-safe ones:**
`material_unit_type` 3 · `grade_or_recipe` 6 · `operation_code` 5 ·
`operation_type` 5 · `unit_of_measure` 10 · `parameter_definition_id` 29 ·
`quality_events.description` 15 · `equipment_type` 14 · `areas.area_name` 12

**Dimensions that CANNOT carry a chart, and why:**
- `risk_scores.risk_class` — **exactly ONE distinct value across all 500 rows**
- **shift** — no column exists anywhere. `crew_code` is 92.9% null
- `equipment.area_id` — 52.9% null, so half the equipment has no area
- `quality_events.defect_catalog_id` — 14 values but 24% null, and the fact's
  `DefectType` string appears empty (the associative bar reads `unknown`)

**These are GENERATOR gaps, not UI gaps.** Do not try to fix them in the
frontend.

## 3.4 Roadmap position

- **M1** — the customer-visible milestone. **M1-P2 (the authoring track) is where
  this session lived.**
- **M1-P1b** — the Fleet v2 generator track, run by the parallel worker.
- **M2a** — replaces the definition storage behind `IDefinitionService` without
  changing its contract, and **must decide the durable Transformation identity
  before either existing version sequence is migrated**.

**Distance covered this session:** T-037 → T-038 → T-039 closed; T-040 four of
five packs. Frontend suite grew from **333 tests to 484** — **+151 tests in one
session**, all of them proving behaviour rather than shape.

---

# 4. REALIZATION SCOREBOARD AT SESSION END

## 4.1 Green and proved

| Item | Evidence |
|---|---|
| T-037 role binding | 147/147 src/authoring, `tsc -b` clean |
| T-038 definition model round trip | 21 tests |
| T-038 S2 face + legacy adapter | 26 tests |
| T-038 shell wiring | 201 → 209 → 218 |
| T-038 Add/Edit substitution + retirement | 218/218 |
| T-039 contract | 4 facts, build clean |
| T-039 Widget adapter | **Failed 0, Passed 2, Skipped 0** against a live DB |
| T-040 G09 | 19 declarations removed, 291 passed |
| T-040 seven states | 304 passed |
| T-040 centre wrapper | 240 passed |
| T-040 execution state | 253 passed |
| **Full frontend suite** | **481 passed / 3 failed / 484 total** |
| API startup | Recovered after `ExcludeFromMigrations` |
| Equipment/Area labels | Applied, build clean |
| Six widgets re-bound | 6 rows updated, verified |

## 4.2 Amber — done but not fully certified

- **T-038 browser rows.** Run test against the live expression service, and the
  visual walk of both entry points. Pooled into the M1-P2 walk.
- **T-040.** Pack 03b2 written and NOT applied. Convergence run not done. **No
  Golden Gate line can be ticked yet, because no evidence file exists.**
- **The commit.** See 7.6.

## 4.3 Red — known and unfixed

| Problem | Owner | Note |
|---|---|---|
| 3 T-012 JourneyRail failures | T-012 | Baseline since 05-Aug, reproduced on a reverted pre-T-032 tree. NOT a regression |
| `AssistantDock.tsx` uses a raw `<button>` | parallel track | PPIQ-T11. One-line fix: `StandardButton` |
| `assistantDock.test.ts` has no `@vitest-environment` | parallel track | PPIQ-T14. Add `// @vitest-environment node` as line 1 |
| `risk_class` single-valued | generator | No chart possible |
| shift absent | generator | No column exists |
| `defectType` empty | generator | Every defectType chart was one bar — six widgets re-bound away from it |
| 4 scanner self-matches in the audit | unowned | The one-line `RelativePath -like` exclusion in `Get-AuditSignalsForContent` has been skipped across THREE dumps |
| `validate-real-ui-gates.cjs` invoked by nothing | unowned | Asserts three npm scripts the root Jenkinsfile does not contain |
| phase56 script patches `--list` into the Jenkinsfile | unowned | Three enumerations as its only test commands |
| Mid-file BOM in `DevSeedEndpoints.cs` line 3 | unowned | Untouched since 07-Jun |
| 15 hardcoded `178.105.152.180` references | unowned | |
| Bootstrap admin enabled in `local.env` and `presentation.env` line 41 | unowned | |

**THE STANDING GAP:** none of the audit findings has a backlog task, so under
ABSOLUTE BACKLOG ADHERENCE they cannot be executed without a ruling. They have
survived three dumps for exactly this reason.


---

# 5. PER-TASK DISCOVERIES, TIPS AND WHAT IS STILL MISSING

## 5.1 T-037
**Discovered:** the mechanism was complete and untouched since 29-Jul; the S2
face had no query surface; the panel was the only writer.
**Tip:** `describeStale` lost its only caller when the wording moved to the
presentation module. Named openly rather than deleted, because he ruled the
module is not to be rewritten.
**Missing:** nothing. Frozen.

## 5.2 T-038
**Discovered:** the label-vs-code binding defect; the expression contract is not
SQL; §5.2.2 contradicts the implemented S2.
**Tips:**
- The ratchet forbids raw controls in six named sources — any new authoring UI
  must use `StandardP2*` and be added to that list.
- `ApplyConfigurationsFromAssembly` means a new EF configuration needs no
  `OnModelCreating` edit.
- `PlantProcessDbContext` has a single-arg constructor, so a test can build one.
- The `DashboardWidgetDefinition` ctor requires non-empty dimension AND measure.
**Missing:** the two browser rows; Chapter 4 §5.1.12 retitle/repoint (recorded,
not built); the §5.2.2 mode-bar ruling.

## 5.3 T-039
**Discovered:** no definition store anywhere; widgets unversioned; two
Transformation stores with incompatible identities; **`ExcludeFromMigrations` is
mandatory when EF maps a table created by the numbered chain.**
**Tips:**
- The numbered SQL chain runs to `760_t025_...` with `999` holding grants, so
  **770 was the free slot**. Runtime roles are `plantprocess_app` and
  `plantprocess_readonly_preview`.
- Infrastructure integration tests use `Xunit.SkippableFact` and resolve the
  connection via `PPIQ_TEST_PG_CONNSTRING` → `PPIQ_TEST_CONNECTION_STRING` →
  `ConnectionStrings__PlantProcessDb` → the local dev string in
  `appsettings.Test.json`. **They RUN on his machine.**
- **`PlantProcess.Api.IntegrationTests` SKIPS everything** unless
  `PPIQ_FORCE_EXTERNAL_API_TEST_HOST=1` or
  `PPIQ_USE_WEBAPPLICATION_FACTORY_TEST_HOST=1`. A frozen validation placed
  there would report "Passed! 0 failed" while proving nothing. **Gate on
  `Skipped: 0`, not just `Failed: 0`.**
**Missing:** Transformation adapter (ruled out, M2a).

## 5.4 T-040 — WHAT THE NEXT SESSION MUST FINISH

**PACK 03b2 — THE KEYBOARD PATH. Written, validated, NOT APPLIED.**

Three anchors in `SharedAuthoringShell.tsx` plus one new test file:
1. the React import → add `type KeyboardEvent as ReactKeyboardEvent`
2. the shell root `<div className="canvas-modeshell" ...>` → add `onKeyDown={onShellKeyDown}`
3. before `const emptyTreeMessage = definition.showsStagingCatalogue` → the handler

The handler, exactly as he ruled it:
- **A React `onKeyDown` on the shell root, never a window or document listener.**
- **ENTER runs only when focus is outside a `textarea`, `input`, `select` or
  contenteditable** — those own their Enter key. Also ignored while
  `activeRun !== "none"`, ignored on S2 (its face owns its button), and on the
  board it refuses through the SAME `invalidReason` the disabled Run uses.
- **ESCAPE dismisses innermost first** — `pendingBlockSwitch`, then `forkAsked` —
  and closes the shell only when nothing is left AND `onClose` was supplied.
- **No positive tabIndex anywhere.** `tabIndex={0}` and `{-1}` are legitimate;
  any positive value reorders one control and strands every later one.

`authoringKeyboard.test.tsx`, 8 tests: no positive tabIndex across all authoring
sources; the handler is on the root not the window; Enter outside a text control
runs; Enter inside the name input does not; Enter does not run a refusing board;
Escape closes when opened as a dialog; Escape does nothing when there is nothing
to dismiss; the definition survives the close.

**THEN THE CONVERGENCE RUN.** He was explicit: **do not defer the browser rows
back to him.** T-040 must close the accumulated objective rows from T-033
through T-040 in one walk, producing NAMED EVIDENCE FILES for: RTL and LTR
rendering, all seven states, keyboard-only navigation, Add and Edit S2 entry
points, returned-column role binding, role persistence after re-run, the
stale/missing-column warning naming the column, and the live Run path.
**No evidence filename = no Golden Gate tick.**

**MY HONEST CORRECTION TO HIM, which he accepted:** I cannot drive a browser on
his machine. What I own is the VERIFICATION — a Playwright spec plus a runner
that executes it and writes the named evidence files. He runs one command; the
assertions, captures and filenames are mine, and a failure is mine to fix.
Playwright is already in the repo (`playwright.phase9.config.ts`,
`playwright-report-journey/`).

---

# 6. EVERY TEST RUN THIS SESSION AND ITS RESULT — DO NOT RE-RUN THESE

## 6.1 Frontend, scope `src/authoring` only

| After | Passed | Failed | Note |
|---|---|---|---|
| session start | 138 | 0 | 10 files: graphSemantics 37, schemaTreeModel 19, sqlModeModel 17, blockNodes 13, sharedAuthoringShell 13, authoringSchemaTree 12, previewReport 11, sqlHighlight 9, operatorContract 4, sqlModeShell 3 |
| T-037-01b | **147** | 0 | +9 |
| T-038-01 | **168** | 0 | +21 |
| T-038-02a2 | **173** | 0 | +5 |
| T-038-02b3 | **199** | 0 | +26 |
| T-038-02c | **201** | 0 | +2 |
| T-038-03a | **209** | 0 | +8 |
| T-038-03b2 | **218** | 0 | +9 |

## 6.2 Frontend, scope `src/authoring` + `src/test/architecture`

| Run | Result |
|---|---|
| T-040-01d | **291 passed, 0 failed**, `tsc -b` 0 |
| T-040-02b | **304 passed, 0 failed**, `tsc -b` 0 |

## 6.3 Frontend, scope `src/authoring` + the G09 ratchet (narrowed)

| Run | Result |
|---|---|
| T-040-03a1d | **240 passed, 20 files**, `tsc -b` clean |
| T-040-03a2 | **253 passed, 20 files**, `tsc -b` clean |

## 6.4 FULL frontend suite — run ONCE, at the convergence boundary

```
481 passed, 3 failed, 484 total, 206 suites, 279s
```
The three failures are the T-012 JourneyRail baseline, by exact name:
- `renders all 15 canonical stages plus the operational alerting entry`
- `marks the current route as the current journey step`
- `maps assistant configuration routes to the final assistant stage`

**Verdict: REGRESSION-CLEAN.** Baseline was 330/333 on 05-Aug, so the track
added 151 tests. **Do not re-run this without a reason — it takes ~5 minutes.**

## 6.5 Backend

| Run | Result |
|---|---|
| T-039-01b unit | **Passed 4, Failed 0**, build 0 |
| T-039-02d integration | **Failed 0, Passed 2, Skipped 0**, 10s, live DB |
| T-039-04b build | Application project, exit 0 |
| PRES-LABEL-01 build | Application project, exit 0 |

## 6.6 Database queries run against `ppiq_presentation`

1. **Widget inventory** — 38 widgets across 12 dashboards, all `bound_by = catalogue`.
2. **Row counts** — section 3.3.
3. **Dimension health** — section 3.3.
4. **The re-bind** — `INSERT 0 6`, five UPDATEs (1,2,1,1,1), COMMIT. Verified
   `gradeOrRecipe` 6 bars / 35,915 rows, `equipment` 9 bars / 53,095 rows.

**A FAILED QUERY WORTH KNOWING ABOUT:** my first dimension-health query filtered
on `pg_stat_user_tables.n_live_tup`, which is an **estimate that stays 0 until
autovacuum or ANALYZE has run**. On a freshly seeded database that excludes every
table and the query returned zero rows. **Use `count(*)` in a DO loop instead.**

## 6.7 Failed gates worth remembering (all recovered)

| Pack | Failure |
|---|---|
| T-037-01 | 3 anchors returned 0 — CRLF vs LF needles |
| T-038-02a | vitest 179 passed, `tsc -b` exit 2 — type imported from a module that did not export it |
| T-038-02b2 | 197 passed / 2 failed — vacuous label query and a pre-load race |
| T-039-02c | `42703 column "Id"` — test context built without the naming convention |
| T-040-01c | ratchet caught `border-left-width` missing from my own map |
| T-040-03a1d | 5 layout tests passed; 2 foreign failures from the assistant track |


---

# 7. HIS RULES, ORDERS AND WAYS OF THINKING — CARRY ALL OF THESE

## 7.1 Standing rules that governed every delivery

1. **ABSOLUTE BACKLOG ADHERENCE.** If it is not written in the backlog, do not do
   it. Tasks execute in dependency order from T-001 upward; a task is fully
   complete, sign-off questions included, before the next starts. Temporary data
   and temporary internal implementation are sometimes allowed; **temporary
   product identity, temporary UX and fake product answers never are.**
   **When a finding falls outside every bucket the task text defines, NAME THE
   GAP AND ASK FOR A RULING rather than inventing a bucket.**
2. **Deep, detailed, advanced, professional work is the DEFAULT.** He should not
   have to run the assessment that finds my gaps. Concretely: re-read the actual
   code before every review rather than working from notes; never let a
   cross-reference or an hour total rest on memory when it can be verified;
   **build a mechanical guard whenever a defect class is mechanical instead of
   promising to be careful**; state arithmetic openly; **name my own defects
   before he finds them.**
3. **Evidence before cure. Surface defects honestly. Never claim done when not
   done.**
4. **Every pack ships with its run block** — full copy-paste, in order, starting
   with `cd`, the `Move-Item` from Downloads and `Unblock-File`, then report-only,
   then apply. **I broke this once tonight and he hit "file does not exist".**
5. **PowerShell 5.1, pure ASCII, UTF-8 no BOM, CRLF for PS/CS, LF for .sh, no
   `&&`, cuddled `} else {`, no em-dashes or curly quotes, run from repo root.**
6. **Destination folders:** `tools\packs\` for apply packs, `tools\run\` for
   verification and diagnostic runners.
7. **Never ask him to paste JS into DevTools or run ad-hoc commands by hand.**
   Diagnostics are PowerShell too. **Exception: a small one-line source edit —
   tell him exactly which line to change to what.**
8. **Distinct filename per revision.** Never reuse a pack filename.

## 7.2 The operating cadence he set for presentation-readiness mode

```
known design + captured anchors   -> IMPLEMENT
bounded test/code defect          -> FIX AND RERUN
independent next work             -> CONTINUE
genuine architecture contradiction -> ASK FOR A RULING
```
**"A concern that a pack may contain a typo is not an architecture contradiction;
that is what ReportOnly, backups and gates are for."**

He also said, twice: **do not stop between packs for approval.** When I stopped
before 03a because the pack was large, he split it into 03a1 and 03a2 rather
than accept the pause — **convert execution risk into smaller guarded packs, not
into approval cycles.**

## 7.3 Architectural rulings he made — these are law now

- **A visible mode name must never stand for two different languages.**
- **The contract represents eleven kinds; that is not eleven adapters. Widget is
  the certified M1 implementation. Unsupported kinds return an explicit refusal,
  never a synthesised history.**
- **Do not invent a Guid-to-code identity convention, a new identity table, a
  session-as-definition semantic, or a partial adapter presented as complete.**
- **Persisted keys never change; presented labels follow doctrine.**
- **Scope the ratchet to what is being certified. Do not create repository-wide
  bans.**
- **No fake or demo-only state branches to satisfy tests. The presentation must
  consume the real shell state.**
- **Natural DOM focus order. No positive tabIndex sequencing.**
- **Objective browser/runtime behaviour is worker-owned verification. Only
  genuinely subjective visual quality is for him.**

## 7.4 The pack contract as it now stands (upgraded four times tonight)

```
REVISION banner in the first three lines, distinct filename
-> preflight: read the tree, REPORT already-applied state, verify every anchor
   byte-for-byte, run the pack's own guards against its own payload
-> backup to tools\packs\_backup_<REV>_<stamp>\ with a RESTORE.txt
-> apply
-> self-check on the artifact, not on intent
-> gates: targeted vitest + tsc -b, or dotnet build + dotnet test
-> on failure: print the FAILURE BLOCK from the JSON report or the test log,
   never the summary lines
-> auto-revert, then VERIFY THE REVERT TOOK and name what to restore by hand
-> print the manual rollback commands
```

**Additions forced by tonight's failures:**
- scans run on comment-stripped text
- a create-guard decides by CONTENT, not existence
- a gate certifies the pack's own surface, not the whole repository
- **any pack touching the DbContext must check the API can still start**

## 7.5 How he wants findings reported

Name the defect before he finds it. Give the measurement, not the impression.
When three candidate causes exist, say they are candidates and let the log
decide — **tonight all three of my candidate causes for one failure were wrong,
and saying so was better than picking one.** When a guard fires, ask first
whether the guard or the artifact is wrong.

## 7.6 UNRESOLVED — the git question

`Invoke-PpiqTrackStaging.ps1` reported **THIS TRACK (0 paths)**: only two Website
files modified and 39 untracked `tools/run` artifacts. So the T-033→T-038 source
changes were **already committed by someone** — most likely the parallel worker
sweeping them in with `git add -A`. **The diagnostic
`tools\run\Show-PpiqTrackGitState.ps1` was delivered to answer who and in which
commit, and was never run.** Run it before any staging.

Later, `Show-PpiqT040ExecutionState.ps1` showed all five 03a2 files as ` M`
against HEAD — so **T-040's work is uncommitted while T-038's is not.**

**NEVER `git add .` — another worker shares this checkout.**

---

# 8. BACKLOG POSITION

Authoritative scope: `PPIQ_Backlog_v2_9_1_03Aug2026.md` / `.xlsx`.

| Task | Title | Status |
|---|---|---|
| T-033..T-036 | Operator contract, graph semantics, block nodes, shell wiring, canvas toolbar, schema tree, dry-run, debug-log safety, authored SQL, SQL mode, highlight | Implementation complete, browser deferred |
| **T-037** | Certify returned-column role mapping in the S2 shell | **DONE, frozen** |
| **T-038** | Add/Edit Widget open the shared shell in S2 mode | **IMPLEMENTATION COMPLETE.** 6 of 8 acceptance lines proved by the suite; 2 are browser rows |
| **T-039** | IDefinitionService with a compatibility adapter | **DONE.** Closure record at `docs/m1/evidence/T-039_CLOSURE.md` |
| **T-040** | Authoring states, keyboard path, RTL, error wording | **IN PROGRESS.** 01d, 02b, 03a1, 03a2 green. **03b2 not applied. Convergence run not done** |
| T-041+ | Not started | |
| T-065 | S3 convergence | Owns S3's entry point |
| M2a | Definition identity convergence | Carries the Transformation item from T-039 |

**T-038's eight acceptance lines:**
1. Add opens S2 in SharedAuthoringShell — PROVED
2. Edit opens the SAME surface with the definition loaded — PROVED
3. Run test executes the canonical expression contract — PROVED (mocked); **live path is a browser row**
4. Returned columns appear — PROVED
5. Axis/Value/Series persist — PROVED
6. A removed mapped column is detected and named — PROVED
7. Saving an unchanged widget preserves its definition — PROVED
8. The old panel is no longer a customer-visible surface — PROVED (gone from the tree AND unreferenced)

---

# 9. DEPLOYMENT, SERVER AND PIPELINE — WHAT IS KNOWN

**BE HONEST WITH HIM ABOUT THIS: no deployment or pipeline work was done in this
session.** Everything below is read from the 05-Aug 23:21 audit dump and from
tonight's local runs. Nothing here has been verified against the live server.

## 9.1 Local topology (verified tonight)

- API `http://localhost:5063`, web `5173`, Postgres `5432`
- Profiles in `env\profiles\` — `local.env`, `presentation.env`
- `.\scripts\run\start-api.ps1 -Profile presentation` → DB `ppiq_presentation`
- Startup applies **pending EF Core migrations** — this is what made the missing
  `ExcludeFromMigrations` fatal
- CORS: `5173`, `3000`, `5080`. Auth binds 1 user, `signingKeyLen=51`,
  `bootstrapCollision=False`. TimeZone Europe/Berlin
- Dev DB credentials in `appsettings.Test.json`:
  `Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only`
- Roles: `plantprocess_app` (runtime), `plantprocess_readonly_preview`,
  `ppiq_query_preview_login`, `ppiq_query_preview_readonly`

## 9.2 Server and deployment surface (from the dump — UNVERIFIED)

- Public IP **178.105.152.180**, addressed via `sslip.io`:
  `https://app.178.105.152.180.sslip.io`, `https://api.178.105.152.180.sslip.io`
- `deploy/ci/post-deploy-smoke.sh`, `deploy/scripts/ensure-runtime-env.sh`,
  `deploy/server/verify-server-exposure.sh`, `deploy/compose/`
- `scripts/deploy/Invoke-CleanMachineDeployAcceptance.ps1` defaults to those URLs
- Acceptance record: `docs/deployment/T007_CLEAN_MACHINE_DEPLOY_ACCEPTANCE.latest.md`
- **15 hardcoded IP references across 8 files.** Not parameterised.

## 9.3 Pipeline findings — OPEN, UNOWNED, NO BACKLOG TASK

| # | Finding | Detail |
|---|---|---|
| 1 | **FINDING A** | `tools/ci/validate-real-ui-gates.cjs` is invoked by NOTHING — `grep -rI` returns zero content references — and it asserts three npm scripts (`test:visual`, `test:phase56:e2e`, `test:a11y`) that the root `Jenkinsfile` contains ZERO occurrences of |
| 2 | **FINDING B** | `Frontend/.../tools/phase56/apply-phase5-phase6-full-ui-migration.cjs:74-76` inserts `stage('2b. Phase 5/6 UI quality gates')` ahead of `stage('3. Build images')` with three `--list` **enumerations** as its only test commands. **`--list` enumerates tests; it does not execute them** |
| 3 | `package.json:84` | `"phase9:matrix": "playwright test --config=playwright.phase9.config.ts --list"` — referenced nowhere |
| 4 | Scanner self-matches | 4 of the 12 CRIT hits are `GeneratePlantProcessIQ_UltimateAudit.ps1` matching its own rule table. **The one-line fix — a `RelativePath -like '*GeneratePlantProcessIQ_UltimateAudit.ps1'` exclusion in `Get-AuditSignalsForContent` (line 712) — has been skipped across THREE dumps** |
| 5 | catchError forcing SUCCESS | 3 hits; one is the architecture test that forbids it |
| 6 | `__DefaultConnection` | 1 hit, wrong connection-string key |
| 7 | Dev seed endpoints | 16 references. `Program.cs:1011` calls `MapDevSeedEndpoints()`; a guard test asserts it sits inside `if (app.Environment.IsDevelopment())` |
| 8 | Bootstrap admin | `PlantProcess__Auth__Users__0__IsBootstrapAdmin=true` in `local.env` AND `presentation.env` line 41 |
| 9 | Mid-file BOM | `DevSeedEndpoints.cs` line 3 begins U+FEFF. SHA `CA30A715...`, untouched since 07-Jun |
| 10 | 7 mojibake files | A UTF-8 em-dash read as latin-1 and rewritten. Worst is `tools/realization/continue-phase03-phase04-from-t016.cjs` (double-encoded). Also `Website/.../App.tsx`, `pack-r12-phase01-phase02-closure.cjs`, `scripts/test/validate-current-green.ps1`, `420_p3_value_evidence_hmi.sql`, `430_phase3_phase4_certification_mapping_health.sql`, the journey-certification scorer |

**The signal report has been 56 signals with identical category counts across
three consecutive dumps while the repository grew by 228 files. Zero remediation.**

---

# 10. MODIFICATIONS MADE TO GET THINGS RUNNING

## 10.1 The API startup fix — THE ONE THAT MATTERED TONIGHT

**Symptom:** `PendingModelChangesWarning` → `PlantProcess IQ API terminated
unexpectedly`, twice.

**Cause (mine):** adding `DefinitionVersion` to `PlantProcessDbContext` changed
the EF model while its table is created by numbered script 770. `Migrate()` sees
a model with no matching migration and refuses.

**Fix:** `builder.ToTable("ppiq_definition_versions", t => t.ExcludeFromMigrations())`

**Why this and not a migration:** he ruled the numbered replay chain owns the
DDL and there is to be **no live-only DDL**. `ExcludeFromMigrations` is the EF
feature for a table managed outside migrations — the chain stays the sole owner
and EF only maps. The pack refuses if 770 is absent, since that would leave EF
mapping a table nothing creates.

**Result: API starts and serves.**

## 10.2 The dimension-label fix — GUIDs became names

**Symptom:** `EQUIPMENT` showed `7922750e-2768-5083-9cc3-...` in the associative
bar and equipment charts.

**Cause:** `DashboardWidgetQueryService.cs:862`
```csharp
DashboardMetadataCodes.Dimensions.Equipment =>
    BuildDimension(fact.EquipmentId?.ToString(), fact.EquipmentId?.ToString(), "No equipment"),
```
Key AND label were both the GUID. The `Equipment` join existed in five queries
but took only `AreaId`.

**Fix, four regions in one file:** a `LoadDimensionLabelsAsync` that loads names
once per query for the dimension actually being drawn, a `LabelFor` helper, one
extra `BuildResult` parameter, and one projection line.
**THE KEY STAYS THE ID** — selections, drill-through and filters all travel on
it. Only the label changes. A row whose id has no matching record keeps showing
the id: nothing is invented, nothing is blanked.

**Why not join the name into the facts:** that would have meant editing ten
`new WidgetFact(...)` sites. One lookup, one call site, four anchors.

**Covers:** `equipment` → 34 names (9 referenced), `area` → 12 names.

## 10.3 The widget re-bind — six charts made meaningful

Applied via `tools\run\ppiq-presentation-rebind.sql`. Result:
`INSERT 0 6`, five UPDATEs, COMMIT.

| Dashboard | Was | Now |
|---|---|---|
| RISK_DASHBOARD | Risk Score by Class — riskClass, **1 bar possible** | **Risk Score by Grade** — gradeOrRecipe, 6 |
| QUALITY_MONITORING | Defect Breakdown — defectType, empty | **Defects by Grade** — 6 |
| QUALITY_MONITORING | Defects by Type (table) | **Defects by Grade** — 6 rows |
| QUALITY_OVERVIEW | Defect Breakdown | **Defects by Grade** — 6 |
| CORRELATION_FINDINGS_BOARD | Defect Landscape | **Defect Landscape by Equipment** — 9 names |
| PRODUCTION_OVERVIEW | Volume by Type — 3 | **Volume by Grade** — 6 |

**Reversible in one statement**, and the old bindings are in `ppiq_rebind_backup`:
```sql
UPDATE dashboard_widget_definitions w
SET dimension_code = b.dimension_code, widget_title = b.widget_title
FROM ppiq_rebind_backup b WHERE b.id = w.id;
```

**Nothing was fabricated.** Every target is a published dimension code whose
column was measured on that database tonight.

## 10.4 The date-charts non-problem — explain this if he raises it again

`Material Units`, `Process Observations`, `Quality Events` and `Defect Rate` are
stored as **`chart_type = 'kpi'` on the `day` dimension**, and
`display_options_json` is `{"maxRows": 50, "rawRowLimit": 1000}` — **no chart
type is persisted at all.** The HEATMAP and PIE selections that produced grids
of `2026-0...` and two-slice pies are **client-side toggles held in the browser**,
not stored state.

**The rule for the demo: any widget whose subtitle reads `day` must be shown as
KPI, LINE or AREA. Never PIE or HEATMAP — each date becomes a slice.**
If a bad selection sticks after refresh, use **Reset layout** in the VISUAL
SELECTIONS bar, or clear site data for `localhost:5173`.

## 10.5 Demo path recommended to him

**Safe:** `PRODUCTION_OVERVIEW`, `QUALITY_MONITORING`, `PARAMETER_DEEP_ANALYSIS`
(29 real parameters — the richest page), `MATERIAL_INVESTIGATION_LAUNCHER`,
`RISK_DASHBOARD` (now 6 bars), `CORRELATION_FINDINGS_BOARD`.

**Avoid:** anything relying on SHIFT or RISK CLASS in the associative bar — those
are generator gaps and the honest answer if asked is that the generator does not
populate them yet.

---

# 11. IMMEDIATE NEXT ACTIONS, IN ORDER

1. **Apply T-040 pack 03b2** (keyboard). Rebuild from 5.4 if the file is gone.
2. **Run the scoped gate:** `npx vitest run src/authoring src/test/architecture/authoringLogicalDirection.test.ts --config vitest.config.ts` then `npx tsc -b`. Expect **261** in `src/authoring` plus 4 ratchet tests.
3. **Build and run the Golden Gate convergence spec** — Playwright, producing the named evidence files listed in 5.4.
4. **Write the T-040 closure record** into `docs/m1/evidence/` with an evidence filename against every Golden Gate line touched.
5. **Run `Show-PpiqTrackGitState.ps1`** and resolve the commit question BEFORE any staging. Never `git add .`.
6. **Route the two assistant-track failures** to their owner: `AssistantDock.tsx` raw `<button>`, `assistantDock.test.ts` missing `@vitest-environment`.
7. **Ask for a ruling on the §5.2.2 mode-bar contradiction** — it is the only open architecture question in T-040.
8. **Ask for a ruling on the audit findings**, which have no backlog task and have survived three dumps.

---

# 12. IF YOU READ ONLY FIVE LINES

1. **T-040 pack 03b2 is written and NOT applied.** That is the gap.
2. **Never scan text that contains the prose describing the scan.** Six failures.
3. **The tree moves without you.** Read it; never trust what the last pack printed.
4. **`tsc -b` and `Skipped: 0` are not optional** — vitest cannot see type errors, and a skipped test proves nothing.
5. **Measure before you write.** Every pack that matched its anchors first time was one where I had extracted them byte-for-byte from the actual file.
