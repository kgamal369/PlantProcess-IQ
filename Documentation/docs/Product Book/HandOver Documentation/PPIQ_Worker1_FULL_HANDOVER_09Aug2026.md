# PPIQ Worker 1 - full session handover
### 08-09 Aug 2026 | Assistant track | M1-P5

**Read this instead of re-investigating.** Everything below was measured in this
session against the real tree, the real database and the running API. Where a
thing was *not* verified, it says so. Do not re-run the tests listed in section 6
unless a file they cover has changed.

---

## 0. How to read this document

| Marker | Meaning |
|---|---|
| **MEASURED** | Observed directly from source, database or a run in this session |
| **INFERRED** | Reasoned from measured facts, not directly observed |
| **NOT VERIFIED** | Stated by someone else or assumed; nobody checked it here |
| **WITHDRAWN** | I claimed it, then measurement disproved it. Do not repeat it |

Four claims of mine were withdrawn during the session. They are listed in
section 5.7 because repeating them wastes a whole cycle.

---

## 1. What happened, in order, with the lesson from each step

### 1.1 Starting point

Session opened with a 07-Aug repository dump (2,262 files) plus a prior handover.
Track position at start: T-071 believed "code-complete, awaiting browser walk",
T-072 through T-076 not started.

### 1.2 T-071 - the dock (the 401 that was not a race)

The inherited handover said the dock's configuration 401 was a **timing race**
and proposed a lazy fetch. **That diagnosis was wrong**, and this was the first
significant finding of the session.

**MEASURED:** `src/api/assistantApi.ts` carried its *own* fetch helper sending
`credentials: "include"` and nothing else. The access token lives in module
memory in `src/api/http/apiClient.ts` and is attached as a bearer header only by
that module. Both `/api/assistant` and `/api/phase8` are
`MapGroup(...).RequireAuthorization()`, and `Program.cs` (~line 497) registers
JwtBearer with **no `OnMessageReceived` cookie bridge**. So every assistant call
was unauthenticated, always, on every page, including `ask`.

Lazy-on-expand would have moved the same 401 from page load to the moment the
dock is clicked in front of a customer - strictly worse.

**TIP:** when a symptom is described as a race, check whether the mechanism can
work *at all* before optimising *when* it runs.

**Second finding, same task:** `AppRoutes` returns `<BootstrapScreen />` while
`isBootstrapping`, and only then renders the `AppLayout` route.
`bootstrap("initial")` awaits `apiClient.refresh()` which calls
`applyLoginResponse` -> `setAccessToken`. So the token is in memory *before* the
layout mounts. **I had claimed a hard-reload race existed; it does not.**
Pack 02's on-need shape is still correct (v2.9.2 asks for it) but my
justification for it was wrong.

### 1.3 T-071 visible contract - "global shell, no route"

Ruling mid-session: the G1 contract is a global shell component, not a route, and
the old assertion only proved `AssistantDock` was not placed in a `<Route>`.

**MEASURED before acting:**
- `App.tsx` line ~691 routed `/assistant` to `AssistantRuntimePage`.
- `AppLayout` declared `NAV_ASSISTANT` **and never rendered it** - the sidebar
  renders only `NAV_DATA_INTEGRATION`, `NAV_ANALYTICS`, `NAV_INTELLIGENCE`,
  `NAV_SYSTEM`. The nav entry was already invisible.
- `noPhaseTokensOnDemoPath` asserts `App.tsx` still contains `path="/assistant"`,
  and `assistantChain` asserts `roleAccess.ts` still maps it.

That last point decided the design: a **hidden compatibility redirect** keeps both
guards green, which is why the route was retained as `<Navigate to="/dashboard">`
rather than deleted. `Sparkles` is also used by `/brand`, so removing the nav line
left no unused import.

**TIP:** before deleting a route, grep the architecture tests for it. Two guards
depended on the path existing.

### 1.4 T-072 - context envelope

**The central finding:** transport already existed end to end and the narrowing
never happened. `AskRequest` accepted `ContextChips`, `AssistantRequest` carried
them, and `AskAsync` built `new RetrievalQuery(TenantId, Role, Question)` -
dropping them. The client was sending `["grounded", "approved findings"]`, which
are labels, not context.

**MEASURED:** `NpgsqlRetrievalIndex` filters tenant and scope role in SQL, then
ranks by cosine over `_embedder.Embed(query.Text)`. So in this index the only
honest narrowing point is the embedded text.

Correction received and applied: hints must carry their **kind and value** -
`page:X`, `widget:Y`, `selection:f=v`, `filter:k=v`. Before it, a selection and a
filter on the same field produced an identical token.

### 1.5 T-073 - the widget chunk family (the largest task)

Three defects found here, each worth keeping:

**(a) The retrieval layer would have silently issued Dataset citations.**
`NpgsqlRetrievalIndex.SearchAsync` does **not** store the producer's handle; it
rebuilds one with `HandleFor(kind, sourceRef)` whose default arm is
`ProvenanceHandle.Dataset(sourceRef)`. A correct `WidgetResult` handle would have
arrived at the assistant as the exact substitution the ruling forbade. Fixed by
making `source_ref` the snapshot id and mapping the kind.

**(b) The anchor rule alone produced a false green.** A turn focused on `CF_RATE`
was answered describing `CF_TOP`, and the assertion passed because a `CF_RATE`
citation also existed. The extractive model turns *every* retrieved chunk into
prose, so a neighbouring widget spoke in place of the focused one.

**(c) `Dataset` cannot substitute for widget evidence.** `ResolveDatasetAsync`
resolves by checking `information_schema` for a table or view of that name. A
Dataset handle on a widget result would resolve **green** while proving only that
a table exists. A citation that certifies the wrong thing is worse than none.

### 1.6 T-074 - registry-typed quantity guard

**MEASURED from the live registry:** `parameter_definitions` holds 48 rows - 40
synthetic, 8 approved, 0 deleted. The only rows matching a casting-speed question
are `CASTING_SPEED` (m/min, 0.5-2.5) and `CASTING_SPEED_MPM` (m/min, 0.0-3.0),
**both synthetic**, both normalising to the same 13-character phrase, with
**different ranges**. Approved matches: zero.

So the correct live outcome is an honest refusal, and it is now literal.

### 1.7 T-075 - evidence UX, and the day's real cost

Nine pack revisions on the second half, **one** of which concerned the product.
The rest were the pack's own gate failing in four distinct ways. Full account in
section 5.8 - it is the most transferable material in this document.

---

## 2. Implementation as it stands, and every modification made

### 2.1 Commits from this session, in order

| Commit | Task | What it changed |
|---|---|---|
| (early) | T-071-01 | `assistantApi.ts` attaches the bearer token |
| `26a44741` | T-071-02 | configuration loads on need, not once at mount |
| (early) | T-071-03 | architecture ratchet for the auth-aware config clause |
| (early) | T-071-04 | G1 visible contract: standalone assistant route retired |
| `7a4dc615` | T-072-01 | typed context envelope, backend |
| `6a3937c1` | T-072-02 | the dock sends the envelope |
| `b30f97a9` | T-072-03 | prefixed retrieval hints with values |
| `976bf9bc` | T-073-01 | `canon.assistant_widget_result` table + manifest |
| `472ce274` | T-073-02 | `ProvenanceKind.WidgetResult` + resolver branch |
| `61dc1f98` | T-073-03a | producer, composite, chunk family |
| `354f91ea` | T-073-03b | presentation rebuild replays script 780 |
| `89f508a` | T-073-03c | evidence semantics + tenant-scoped read |
| `dbdbdbec` | T-073-04 | contextual evidence anchor |
| `b0bb9863` | T-073-05 | focused-widget composition |
| `0786a99c` | T-074-01 | registry-typed quantity guard |
| (late) | T-074-02 | question-level quantity refusal |
| `44aacf3d` | T-075-01 | evidence API + pure logic |
| `0609dab4` | T-075-02/04 | chips, strip on StandardTable, open in page, starters |

### 2.2 Files created

**Backend**
- `Backend/database/scripts/780_t073_widget_result_evidence.sql`
- `Backend/PlantProcess.Infrastructure/Assistant/WidgetResultChunkProducer.cs`
- `Backend/PlantProcess.Infrastructure/Assistant/CompositeChunkProducer.cs`
- `Backend/PlantProcess.Infrastructure/Assistant/NpgsqlWidgetResultEvidenceReader.cs`
- `Backend/PlantProcess.Infrastructure/Assistant/NpgsqlParameterQuantityRegistry.cs`
- `Backend/PlantProcess.Application/Assistant/WidgetResultEvidence.cs`
- `Backend/PlantProcess.Application/Assistant/QuantityRegistry.cs`
- `Backend/PlantProcess.Application/Assistant/TypedQuantityGuard.cs`

**Backend tests**
- `Assistant/T072ContextEnvelopeTests.cs` (7 facts)
- `Assistant/T073WidgetResultEvidenceTests.cs` (10 facts)
- `Assistant/T073EvidenceAnchorTests.cs` (6 facts)
- `Assistant/T073FocusedCompositionTests.cs` (7 facts)
- `Assistant/T074TypedQuantityGuardTests.cs` (17 facts)
- `Assistant/T074QuantityRefusalTests.cs` (4 facts)
- `Provenance/T073WidgetResultHandleTests.cs` (4 facts)
- `PlantProcess.Architecture.Tests/ProvenanceResolverExhaustivenessTests.cs` (2 facts)

**Frontend**
- `src/components/assistant/assistantPageContext.ts`
- `src/components/assistant/assistantEvidence.ts`
- `src/pages/Dashboard/evidenceFocus.ts` + `.css`
- `src/components/assistant/__tests__/assistantContextEnvelope.test.ts` (9)
- `src/components/assistant/__tests__/assistantEvidence.test.ts` (15)
- `src/components/assistant/__tests__/assistantEvidenceSurface.test.tsx` (12)

**Deleted:** `src/pages/Phase8/AssistantRuntimePage.tsx`

### 2.3 The assistant turn, end to end, as it now runs

```
POST /api/assistant/ask
  -> TenantClaims.TryResolve            tenant/role/licence from CLAIMS only
  -> AssistantService.AskAsync
       1. focused widget in envelope?
            -> FindActiveAnchorAsync(tenant, widget, page)
            -> no anchor  => honest refusal, no retrieval
       2. registry quantity resolution (T-074)
            NoMatch                      => unchanged path
            Resolved                     => guard armed
            KnownButUntrustedOrAmbiguous => honest refusal, no retrieval
       3. contextTerms = envelope.RetrievalTerms()   page:/widget:/selection:/filter:
       4. retrieval: tenant + scope SQL filter, cosine over question + terms
       5. focused turn? drop rival widget-result chunks,
          read the persisted snapshot, put it FIRST as a real grounded chunk
       6. model.Draft(request with { Context = null })   <- envelope cannot echo
       7. TypedQuantityGuard.Apply(draft.Text, resolution)
       8. GroundingService.Enforce(guarded text, claims)
       9. answer.BlockedSentences = grounding blocked + quantity blocked
```

### 2.4 Database objects added

`canon.assistant_widget_result` - 12 columns, 4 indexes (PK, unique, and three
explicit; the unique constraint creates its own, which is why psql reports 5).

```
id, tenant_id, page_code, widget_code, widget_definition_id,
query_fingerprint, generated_at_utc, filter_context_json,
population_count, result_json, result_fingerprint, created_at_utc
CONSTRAINT uq_assistant_widget_result UNIQUE (tenant_id, result_fingerprint)
```

**The unique constraint is load bearing.** The producer derives
`result_fingerprint` deterministically and inserts `ON CONFLICT DO NOTHING`, so an
unchanged reindex **reuses** the evidence id. Neither fingerprint nor the chunk
sentence contains the timestamp - that is what makes a repeat reindex silent.

**Debt:** the column is named `population_count` and stores the total of the
result's own `observationCount` column, or 0 when absent. Renaming needs a second
migration. Nothing reads it as a population.

### 2.5 New API surface

`GET /api/assistant/evidence/widget-result/{evidenceId:guid}` - inside the
existing authorised group. Predicate is `tenant_id = @tenant AND id = @id`;
another tenant's handle returns `available:false` with a reason, never content.

---

## 3. Identity, topology and roadmap - where it started, how far it moved

### 3.1 The product contract being protected

> customer data -> evidence-grade plant model -> deterministic analysis ->
> visible evidence -> grounded explanation

The engines calculate. **The assistant retrieves, grounds, explains and links
evidence, and never becomes the analytical engine.** Never claim guaranteed root
cause, guaranteed savings, or production-trained ML where no production model
exists. Honest refusal is a feature.

PPIQ is **not** a steel application. Fleet v2 is an emulated reference plant. The
next customer may be oil or mineral water. Therefore: no industry semantics in
product code, no hardcoded presentation answers, no assistant logic built around
Fleet-v2 wording, no page-specific fake intelligence.

### 3.2 Topology as measured

- **API** `.NET`, launched `-Profile presentation`, port 5063, JwtBearer with an
  in-memory access token; refresh via `/auth/refresh`.
- **Frontend** React + Vite. `AppRoutes` gates `AppLayout` behind auth bootstrap.
  `DashboardFilterProvider`, `DashboardSelectionProvider` and
  `DashboardGridLayoutProvider` wrap every authenticated route - which is why the
  dock can read live selections without touching Worker 2's pages.
- **Database** PostgreSQL `ppiq_presentation`. Assistant objects live in `canon`.
- **Canonical dashboard route** `/workspace/:dashboardCode` ->
  `RoutedInteractiveWorkspacePage` -> `loadDashboardByCode`. T-073's `pageCode`
  **is** `dashboard_definitions.dashboard_code`, which is why "open in page"
  resolves an identity rather than assembling a plausible string.
- **Registry** `parameter_definitions`, **global, not tenant scoped** -
  `ParameterDefinition : BaseEntity` and `BaseEntity` has **no `TenantId`**.

### 3.3 Roadmap movement this session

Started: T-071 believed nearly done, T-072 to T-076 untouched.
Ended: T-072, T-073, T-074 closed; T-071 and T-075 code-complete awaiting one
walk; T-076 deliberately not started.

The assistant went from *a chat that could not authenticate* to *a grounded
assistant that cites persisted evidence, resolves each citation to a
tenant-scoped snapshot, refuses when it cannot vouch for a quantity, and opens
the owning widget in its real page.*

---

## 4. Realisation scoreboard at session end

| Capability | State | Evidence |
|---|---|---|
| Dock on every authenticated page, not a route | **Built, ratcheted** | `assistantDock.test.ts` 12 tests |
| Assistant authentication | **Fixed** | credential attached; 401 root cause was a missing header |
| Context envelope narrows retrieval | **Closed** | T-072 closure record |
| Context never becomes evidence | **Structural** | `request with { Context = null }` |
| Page/widget chunk family from real results | **Closed** | T-073 closure; 38 widgets, 38 chunks, all resolve |
| Every number carries a resolvable handle | **Closed** | `WidgetResult:<id>` resolves tenant-scoped |
| Honest refusal when evidence removed | **Proven** | negative proof in certification |
| Registry-typed quantity guard | **Closed and frozen** | T-074 closure; 21 facts |
| Citation chips + evidence strip + open in page | **Code complete** | 12 surface tests; walk pending |
| Context-derived starters | **Code complete** | global hardcoded list deleted |
| Certified question pack / offline fallback (T-076) | **Not started** | - |
| Retrieval relevance floor | **Absent** | recorded for T-076 |

### 4.1 What is genuinely strong now

1. **Evidence identity is deterministic and semantic.** Change a widget's
   measure, chart type, dimension, page, definition id or filter context and the
   fingerprint moves, so an old citation can never resolve to semantically
   different evidence. Tested per-semantic, not asserted in a comment.
2. **Refusal is real in three distinct ways** - no anchor for a focused widget,
   no approved definition for a named quantity, no evidence at all.
3. **The tenant boundary is in the SQL predicate**, not in a comment.
4. **Guards are mechanical.** Every rule that could rot is a test: no route for
   the assistant, no nav entry, no Dataset substitution, no vocabulary in generic
   code, no second data grid, no browser storage.

### 4.2 What is weak, and should be said aloud in the room

1. **No relevance floor.** A question naming no registry quantity and no widget
   is still answered from the best available chunks, however weak. This produced
   a policy document answering a casting-speed question. T-076.
2. **Widget instability upstream.** Four (then three) widget definitions return
   different numbers on repeated execution. Filed with Worker 2.
3. **Casting speed has no approved definition.** The demo story for it rests on
   synthetic vocabulary; the assistant honestly refuses.
4. **Snapshots are unfiltered.** `filter_context_json` is `{}` for every snapshot
   in this pass; filtered snapshots are T-075/T-076 territory.
5. **T-071 and T-075 are unproven in a browser.** Nine items, section 8.

---

## 5. Per-task findings, tips, and the traps

### 5.1 T-071

- The 401 was a **missing credential**, not timing. `assistantApi` had its own
  fetch helper bypassing the shared authenticated client.
- `AppRoutes` gates the layout behind bootstrap, so no pre-token mount is
  possible. **Do not re-open this.**
- `NAV_ASSISTANT` was declared and never rendered.
- `AssistantDock.tsx` already handles **Escape to collapse** - that Chapter 4
  5.7.1 item was already done.
- **Chapter 4 5.7.1 items with no backlog owner:** per-user remembered dock
  state, a keyboard shortcut that focuses the composer, docked-wide (640px) and
  full-viewport states, the mobile full-height sheet, RTL inline-end mirroring.
- **Predicted browser failure:** `AssistantDock.css`, `@media (max-width: 680px)`
  resets `bottom: 12px`, back into the corner occupied by the language pill, the
  theme pill and the JOB LOG bar. Fix is that single value -> `150px`.

### 5.2 T-072

- Transport existed; narrowing did not.
- Hints must carry kind **and** value or ranking cannot tell two hint kinds apart.
- `pageCode` is the **first** path segment, not the last: on `/materials/:id` the
  last segment is a row id. The full route travels beside it.
- The envelope is passed **into** `ask(question, context?)` rather than assembled
  in the provider - that keeps dashboard hooks out of the provider, which is what
  lets `assistantPersistence.test.tsx` mount it with no dashboard providers.
- `useDashboardSelection()` already exposes `sourceWidget` per selection. That is
  a real focused-widget signal; nothing had to be invented.

### 5.3 T-073

- `HandleFor` rebuilds handles at search time - see 1.5(a).
- **Persist, read back, then compose.** A sentence built in memory with a row
  written beside it proves nothing.
- The composite producer keeps `CanonicalChunkProducer` untouched and fails soft:
  if the widget family throws, the canonical families still reindex.
- The anchor requires a **live chunk** (`is_stale = false`, `is_synthetic =
  false`) joined to the snapshot - not merely a surviving row. That is exactly
  what makes the disable-and-ask negative proof work.
- **Discovery SQL** joins `dashboard_widget_definitions` to
  `dashboard_definitions` on `dashboard_definition_id`, filtered
  `is_deleted = false AND is_active = true`, ordered by dashboard, sort order,
  widget code. Read-only; the producer never writes a definition.
- The rebuild replays an **explicit list**, not a glob - see section 9.

### 5.4 T-074

- Registry is the only authority. Three outcomes, not two; the middle one
  (`KnownButUntrustedOrAmbiguous`) refuses **at the question, before retrieval**.
- Candidate identification is narrow: `value unit`, `low-high unit`,
  `low to high unit`, tied to the registry's own unit. Everything else in the
  sentence is contextual. A date, a mass and a bare number all fail for one
  reason - no candidate satisfies the contract - with no date rule and no unit
  dictionary anywhere.
- **The sign is part of the candidate.** The first version started the capture at
  a digit, so `-1.31 m/min` yielded `1.31` and passed a positive lower bound.
- Unitless quantities keep their bounds: one number in a sentence naming the
  parameter is checked; two numbers fail closed.

### 5.5 T-075

- `StandardTable` contract, read from the repository: `columns`, `data`,
  `getRowKey`; sorting, filtering, export, pagination all opt-in.
- A `div role="table"` is **evasion of the guard, not compliance**. Rejected.
- Unavailable (404) and failed (transport) must not share a code path.
- Starters come from context or there are none; "none" is a truthful line.
- `AssistantChat.tsx` had a header comment saying no evidence row route existed.
  T-073 made that false; the comment was updated rather than left to mislead.

### 5.6 Cross-cutting engineering tips

1. **Read the file's usings before naming a type.** `AssistantService.cs` had
   **zero** using directives; `ProvenanceHandle` failed to resolve.
2. **Never assert "zero non-ASCII".** `App.tsx` carries a section sign,
   `ProvenanceHandle.cs` a section sign. Compare **before against after**.
3. **Brace counting is meaningless** on files whose strings contain code.
   `assistantDock.test.ts` is 13 open against 12 close *before* any edit.
4. **`//` pragmas die in comment-stripped text.** Check `@vitest-environment`
   against the raw text.
5. **PowerShell case-insensitivity is a real hazard.** `$after`/`$After` and
   `$excluded`/`$Excluded` are the same variable. The latter would have silently
   dropped every exclusion but one.
6. **A function returns everything written to the output stream.** A bare native
   command inside a function makes its console text part of the return value.
   Use `| Out-Host`.
7. **Bound any surgical edit at a payload boundary.** One edit sliced from a
   helper to the Revert section and deleted every here-string between them - the
   pack would have written empty files over a working component.
8. **Simulate every pack against the real on-disk text before shipping.** Four
   defects were caught this way that no gate would have.

### 5.7 Claims I made and then withdrew - do not repeat them

| Claim | What measurement showed |
|---|---|
| The dock 401 is a hard-reload timing race | `AppRoutes` gates the layout behind bootstrap; impossible |
| `population_count = 50000` is a cap | The same widgets later reported 5. Not a cap; unexplained |
| 760 and 770 are absent from the apply-order manifest | Row count proves scripts are missing, but those two were never checked |
| The pipe character broke the psql command line | Removing every pipe changed nothing; cause still unknown |

### 5.8 The gate lessons - the most transferable material here

Four distinct ways a gate lied during T-075:

1. **Parsing console prose.** Vitest writes failures to stderr; `2>&1` turns them
   into PowerShell `ErrorRecord`s. The pattern found nothing, both sets were
   empty, and "no new failures" was trivially true. It printed *the suite is
   green* directly beneath two failures.
2. **The JSON reporter wrote no file.** `--reporter=json --outputFile` produced
   nothing. Cause unknown; do not spend time on it.
3. **Return-value contamination.** See tip 6. The gate rejected its own passing
   tests and read a null exit code as success.
4. **Coarse exit-code comparison on an already-red suite.** Let a real regression
   through - a raw table I introduced - and then reported that nothing was known
   to be mine.

**The rule:** *a gate that cannot detect its own author's mistake is worse than
no gate, because it converts a defect into confident assurance.*

**What finally worked, and what the next session should use:** name the files
that decide the task, run them directly through `vitest.mjs`, and read the exit
code. No exclusions, no parsing, no baseline arithmetic.

Two habits that paid for themselves: a transcript written to `%TEMP%` that
**survives the pack's own revert**, and simulating each pack against reconstructed
on-disk text before delivery.

---

## 6. Every test and measurement run - do not repeat these

### 6.1 Backend unit tests, final state

| Suite | Facts | Result |
|---|---|---|
| `T072ContextEnvelopeTests` | 7 | pass |
| `T073WidgetResultHandleTests` | 4 | pass |
| `T073WidgetResultEvidenceTests` | 10 | pass |
| `T073EvidenceAnchorTests` | 6 | pass |
| `T073FocusedCompositionTests` | 7 | pass |
| `T074TypedQuantityGuardTests` | 17 | pass |
| `T074QuantityRefusalTests` | 4 | pass |
| `ProvenanceResolverExhaustivenessTests` | 2 | pass |
| Wider `~Assistant` filter | **103** | pass |

`dotnet build` on `Backend/PlantProcessIQ.sln` was green at every commit.

### 6.2 Frontend targeted gate, final state (09:50)

`tsc -b` clean. Seven files, **58 tests, all passing**:

```
assistantEvidenceSurface.test.tsx   12
assistantPersistence.test.tsx        2
assistantContextEnvelope.test.ts     9
assistantEvidence.test.ts           15
noRawStandardElements.test.ts        2
assistantDock.test.ts               12
assistantChain.test.ts               6
```

**This is the command to re-run, from `Frontend\PlantProcess.Web`:**

```powershell
node node_modules\typescript\bin\tsc -b

node node_modules\vitest\vitest.mjs run --config vitest.config.ts `
  src/components/assistant/__tests__/assistantEvidenceSurface.test.tsx `
  src/components/assistant/__tests__/assistantPersistence.test.tsx `
  src/components/assistant/__tests__/assistantContextEnvelope.test.ts `
  src/components/assistant/__tests__/assistantEvidence.test.ts `
  src/test/architecture/noRawStandardElements.test.ts `
  src/test/architecture/assistantDock.test.ts `
  src/test/architecture/assistantChain.test.ts
```

### 6.3 Wider frontend suite - known concurrent reds

`Test Files 2 failed | 22 passed`, and **both belong to Worker 2's T-042 tree**:

- `largeFileBoundaries.test.ts` - `pageBuilderBridge.test.tsx` and
  `pageBuilderLayout.test.tsx` import `../PageBuilderPage.implementation`
- `uiConformanceRatchet.test.ts` - `PageBuilderPage.implementation.tsx` D1 raw
  controls 2 > baseline 0

**Do not fix. Do not exclude to manufacture a green claim.** Any frontend commit
gated on the whole suite stays red until his T-042 closes.

### 6.4 T-071/T-072/T-073 runtime certification (20:23, 22 checks, 0 failures)

Evidence: `docs/m1/evidence/T-071_T-072_T-073_certification_20260808_202350.txt`

- Reindex produced **38 widget-result chunks** from **38 active widget
  definitions** across **12 pages**; all 38 chunks resolve to a real snapshot; one
  tenant throughout.
- Determinism per widget/page pair: **35 of 38 stable** across a repeated
  reindex.
- Three stable non-empty widgets on three distinct pages - `CF_RATE`,
  `DQ_BY_SOURCE`, `EO_EQDEF` - passed A through E:
  A the persisted sentence appears verbatim; B the first `WidgetResult` citation
  is the focused widget; C **every** `WidgetResult` citation is that widget on
  that page; D every number matches the snapshot (18, 2, 2 numbers checked);
  E the handle resolves through the tenant-scoped endpoint.
- An unknown evidence id reports unavailable, never content.
- Same question on two pages retrieved **different** evidence.
- A fabricated context marker never appeared in an answer.
- Chunks disabled -> **honest refusal**; restored afterwards; answering resumed.

### 6.5 T-074 live certification (21:44, 8 checks, 0 failures)

Evidence: `docs/m1/evidence/T-074_quantity_certification_20260808_214446.txt`

```
isRefusal : True
reason    : I don't have an approved definition for that quantity,
            so I can't answer it.
```

No number in the refusal, no date, no mass, neither synthetic range surfaced, no
value carrying any registry unit.

**Optional section could not run:** one psql query in the runner returns a single
character while every other query in the same function works. Cause unknown; my
pipe-character explanation was measured to be wrong. It cannot decide
certification and is reported as a warning. **Do not spend a cycle on it.**

### 6.6 Database measurements

- `canon.assistant_widget_result` after reindex: **50 rows**, 38 distinct query
  fingerprints, 50 distinct result fingerprints, one tenant.
- Section 7 of the diagnostic showed the first reindex writing 38 rows, then
  **exactly four per subsequent run**: `PO_KPI_OBS`, `PA_BYP`, `PA_TABLE`,
  `EO_OBS` - later three, `PA_BYP` having stabilised. All `observationCount`
  measures.
- `parameter_definitions`: 48 rows, 40 synthetic, 8 approved, 0 deleted, 20
  distinct units.
- Script 780 applied **twice** against `ppiq_presentation` - idempotent.
  Inspection returned `columns=12, unique=1, indexes=5, rows=0`.

### 6.7 Read-only runners left in the tree

- `tools/run/Invoke-PpiqAssistantCertification.ps1` - T-071/72/73, `-Apply`
  needed for the negative proof, which restores in a `finally`.
- `tools/run/Invoke-PpiqT074QuantityCertification-v3.ps1` - T-074 live.
- `tools/run/Show-PpiqT073EvidenceState.ps1` - evidence table state, read-only.
- `tools/run/Show-PpiqT074RegistryResolution.ps1` - registry resolution for a
  question, read-only, `-Question` parameter.

---

## 7. Rules, orders and concepts to carry forward

### 7.1 Standing rules given during this session

1. **Absolute backlog adherence.** Frozen task text governs. If something is not
   in the backlog, name it and ask - do not absorb it.
2. **Implementation over investigation**, but **measure narrowly first**. Come
   back for a ruling only for: a true architecture contradiction, a destructive
   data or schema operation, a cross-worker ownership collision, or a frozen
   acceptance that is literally impossible.
3. **The dataset is frozen.** Do not regenerate or enrich Fleet v2. Touch data
   only if a frozen acceptance is impossible because of a proven data defect.
4. **Ownership boundary.** Worker 2 owns page definitions, widget bindings, chart
   semantics, dashboard truth. Do not change his files to make retrieval easier;
   the assistant adapts to the final surfaces.
5. **Exact-file staging only.** No `git add -A`, not even scoped to a directory.
6. **Record a finding once, give it an owner, leave it alone.**
7. **A refusal, a gap or zero findings is a valid result.** No band retrofitted
   from observed data; no threshold weakened to produce green.
8. **Name your own defects first**, before they are found.
9. **Source-level pass is not runtime pass is not visual acceptance.**
10. **Any scan runs against comment-stripped code**, never against prose
    describing the scan.

### 7.2 Concepts that decided designs

- **Context narrows retrieval; context is never evidence.** Enforced
  structurally by `request with { Context = null }`, not by a string check.
- **An old citation must never silently resolve to semantically different
  evidence.** Hence the semantic fingerprint.
- **Persist first, then compose from the persisted representation.** That is what
  makes a citation prove the exact sentence.
- **Evasion is not compliance.** A `div role="table"` that stops a scanner
  matching is not the design system.
- **Do not invent an artifact merely to make a chunk groundable.** A Dataset
  handle proving a table exists is worse than no handle.
- **Three outcomes, not two**: unknown, known-and-vouched, known-and-unvouched.
  The middle case is where honest products differ from confident ones.

### 7.3 Pack contract, unchanged

Preflight -> report (sha, lines, line endings, non-ASCII) -> anchor verification
against exact on-disk text with a diagnosis on failure -> backup -> apply ->
on-disk self-check -> gated build/test -> auto-revert on any failure. Nothing
applied without `-Apply`. Simulate against reconstructed on-disk state before
delivery. PowerShell 5.1: no `&&`, no em-dashes, no curly quotes.

---

## 8. Backlog task status

Authoritative backlog: `PPIQ_Backlog_v2_9_2_08Aug2026` (v2.9.1 superseded and
moved to `Related Documentation`).

| Task | Status | Detail |
|---|---|---|
| **T-071** G1 persistent dock | **InProgress** | Code complete, ratcheted. v2.9.2 added an audit-hardening clause: no assistant-config 401 on a fresh session, before or after hard reload. Satisfied by construction. **4 browser items outstanding.** |
| **T-072** context envelope | **Closed** | `docs/m1/evidence/T-072_CLOSURE.md`. Live two-page difference proven during T-073 certification. |
| **T-073** page/widget chunk family | **Closed** | `docs/m1/evidence/T-073_CLOSURE.md`. All seven validation points met. |
| **T-074** registry-typed quantity guard | **Closed, frozen** | `docs/m1/evidence/T-074_CLOSURE.md`. Live casting-speed question returns option C, honest refusal. |
| **T-075** citation chips / evidence strip | **InProgress** | Code complete, targeted gates green. **5 browser items outstanding.** |
| **T-076** certified question pack + offline fallback | **Not started** | Also where the relevance floor should be measured rather than guessed. |

### 8.1 The deferred walk - nine items, one session

Fresh browser profile or private window. Network tab filtered `assistant-config`.

1. Fresh login: no 401 on the dock's first open.
2. Hard reload: still no 401, ready without a manual retry.
3. Ask once, navigate five pages: the turn survives.
4. Collapse obscures no control; at ~390 px the launcher clears the language
   pill, theme pill and JOB LOG bar. **Expected to fail** - see 5.1.
5. Citation chip opens and closes; one strip at a time.
6. Strip shows the persisted sentence, page and widget codes, measure, dimension,
   rows in a `StandardTable`, and **no** occurrence of "Population".
7. Open in page navigates to `/workspace/<pageCode>`.
8. The owning widget scrolls into view and is outlined ~4s.
9. Starters differ between two pages and name real codes; with no widget context,
   one truthful line rather than three invented questions.

Items 1-4 close T-071. Items 5-9 close T-075.

---

## 9. Deployment, server and pipeline - what is measured, and what is NOT

**Read this section carefully: I did not work on the pipeline or the server this
session. Most of what follows is measurement of files, not of a running system.**

### 9.1 The replay chain - MEASURED

`deploy/scripts/ci-test-db.sh` runs the EF idempotent script, then **globs and
executes every** `Backend/database/scripts/*.sql` in filename order, then the
seeds. **The numbered scripts directory IS the replay chain.**

`Backend/database/database.apply-order.manifest.csv` is a **classification
register beside it, not the executor**, and it is **stale**: 84 rows against 97
numbered scripts. Groups: `90_review` 35, `70_demo_source_systems` 19,
`30_schema` 15, `91_high_risk_review` 5, `10_security_admin` 4, `80_validation` 4,
`40_views` 1.

Notably `420_phase6_assistant.sql`, which creates `canon.assistant_chunk`, is
classified `90_review / DO_NOT_AUTO_APPLY`.

Script numbering: the highest number is **999** (`grant_runtime_app_role_
privileges`), which must run last. `M2-28_results_v2_tenant_backfill.sql` sorts
**after** 999 in ASCII order - a pre-existing oddity, **not investigated**.

### 9.2 The presentation rebuild - MEASURED, and corrected this session

`scripts/demo/Rebuild-PresentationDb.ps1`:
- Step 1 is `pg_restore --clean` from a snapshot dump - it **rewinds everything**.
- Step 1b replays an **explicit list**, not a glob. Before this session that list
  was 741, 742, 750, 760.

**Therefore any numbered script not named in that list does not exist after a
rebuild.** Script 780 was added to it, plus a post-replay check that reports
whether `canon.assistant_widget_result` exists and increments `PpiqFailCount` if
not.

**RECORDED, NOT FIXED:** `770_t039_definition_version_store.sql` is also missing
from that list. Same defect class, Worker 2's task.

### 9.3 The server apply script - MEASURED, deliberately untouched

`deploy/server/apply-server-db-scripts.sh` applies **five** scripts - 200, 201,
202, 203 and 760 - followed by an ML proof. It is a **bounded ML correction
list, not the owner of every migration**, so 780 was deliberately **not** added.

### 9.4 The Jenkinsfile - MEASURED, and this is an open finding

Root `Jenkinsfile`: 183 lines, 9 stages, last modified 10-Jul.

**FINDING A, still open across five consecutive dumps:** `tools/ci/validate-real-
ui-gates.cjs` (SHA `073F360D9998`, untouched since 09-Jun) demands three suites -
`test:visual`, `test:phase56:e2e`, `test:a11y`. The Jenkinsfile contains **zero**
of them, and zero `--list` or `catchError`.

**FINDING B, still open:** the phase56 migration script (SHA `FFACDA041A3F`,
untouched since 30-May) patches the Jenkinsfile.

**NOT VERIFIED:** whether the pipeline currently runs, whether it is green, and
what it deploys. No pipeline run was observed in this session.

### 9.5 Environment facts - MEASURED

- API profile `presentation`, port **5063**. `start-api.ps1 -Profile presentation`.
- Credentials read from `env/profiles/presentation.env`
  (`PPIQ_SMOKE_USERNAME` / `PPIQ_SMOKE_PASSWORD`, `POSTGRES_*`).
- Database `ppiq_presentation` on `127.0.0.1:5432`, user from the profile.
- **A running API locks the build output.** `CS2012` / `MSB3021` on
  `PlantProcess.Api.dll` means an instance is alive. Every pack that builds now
  refuses to write while a `PlantProcess.Api` process exists or port 5063 is
  listening. Stop it with:
  `Get-Process -Name PlantProcess.Api -ErrorAction SilentlyContinue | Stop-Process -Force`
- **psql quirk, unexplained:** one query in the T-074 runner returns a single
  character while every other query in the same function works. Two explanations
  were tried and both disproved.

---

## 10. Pipeline-green and app-URL work

**Nothing was done on this in this session, and no claim should be inherited that
it was.**

No CI run was observed, no deployment executed, no application URL exercised, and
no change made to the Jenkinsfile, to `deploy/`, or to any hosting configuration.
The only deployment-adjacent change is section 9.2 - adding script 780 to the
presentation rebuild's replay list so the assistant's evidence table survives a
rebuild, with a post-replay presence check.

What the next session would need to establish before claiming anything here:

1. Does the pipeline run at all today, and is it green.
2. FINDING A: either the Jenkinsfile gains the three suites `validate-real-ui-
   gates.cjs` demands, or that validator is retired. Right now the validator
   describes a pipeline that does not exist.
3. The manifest is stale (84 of 97). Either it is authoritative and must be
   completed, or it is documentation and should say so.
4. Two frontend architecture tests are red from Worker 2's T-042 tree. Any CI
   stage running the whole frontend suite is red until they clear.
5. `M2-28_...sql` sorting after the grants script.

**Do not let anyone infer from this handover that the pipeline or a deployed URL
was validated. It was not.**

---

## 11. First moves for the next session

1. Read this file. Do not re-run section 6's tests unless their files changed.
2. Confirm Worker 2 has cleared the two PageBuilder reds.
3. Run the nine-item walk (section 8.1). Expect item 4 to fail; the fix is one
   CSS value.
4. If green: close T-071 and T-075, write both closure records, commit by exact
   path.
5. Then T-076 - certified question pack and offline fallback - and measure the
   retrieval relevance floor there rather than guessing a threshold.

**Uncommitted at handover:** verify with `git status --short docs/m1/evidence`.
The closure records for T-072/T-073/T-074, the two findings, the certification
evidence files and this handover may still need staging, each by full path.
