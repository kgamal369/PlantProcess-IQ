# PPIQ — Worker 2 Session Handover — T-051

**Written** 17 Aug 2026, end of the T-049/T-050 session.
**Scope** T-051 and T-052 only. Project authority lives in the backlog v2.10.4 and the
master charter; this document does not restate it.

**Status: FROZEN / ACCEPTED** — authoritative Worker-2 session handover, 17 Aug 2026,
incorporating three tech-lead tightenings: the two failure paths are separated (§3),
`hasEffectiveFilter` consumes existing normalization rather than inventing one (§3), and
the Playwright failure is forced through an existing external seam rather than a
production test flag (§8).

**The next session starts at implementation, not investigation.** Every design decision
below is a frozen tech-lead ruling. Do not reopen them.

---

## 1. Authoritative task state

| Task | Status | Commit |
|---|---|---|
| T-049 | **CLOSED** | `cc4d88444a81b714436aa3f377c2042bb8212bbb` |
| T-050 | **OPEN / PARTIAL** — presentation half committed | `d6870d0aac4719918864dd44798939a4980707b3` |
| T-051 | **GO** — design frozen, no implementation written | — |

**T-050 is not closed.** The presentation half (drawer tokens, logical positioning, RTL
mirroring and animation direction, reduced-motion verification) is committed and certified
by `e2e/t050-drilldown-drawer-presentation.spec.ts` — 4 tests plus `license.setup`, all
passing. The drill-down chain (clicked point → population → provenance handle → source
evidence) is **not built** and is blocked on **PR-050-01**, owned by Worker 1: the widget
query response carries no `ProvenanceHandleRef` for an aggregated row. When PR-050-01
commits, Worker 2 returns to T-050, integrates the *existing* provenance authority, and
only then closes it. Do not invent a handle to close it sooner.

Also carried, not owned by T-051:
- **W2-GRID-DEFAULTS-01** — `DashboardGridLayoutContext.tsx` merges nine hardcoded
  `defaultLayouts` keys (`defectTrend`, `qualityHeatmap`, `materialExplorer`, …) into every
  persisted layout on both serialize and load. Measured: 19 persisted entries against 10
  rendered widgets, every breakpoint, every viewport. Rule 1 genericity finding.
- **Presentation data hygiene** — the persisted `lg` layout of `PRODUCTION_OVERVIEW` has
  every widget one grid row tall, written by earlier unhydrated T-049 runs. Must be
  repaired **before T-078 / T-080**, or visual regression will baseline the damage.

---

## 2. T-051 — frozen design ruling

**Task:** widget failure isolation and the seven states. Priority Critical, M1 Contract,
2026-08-20 Presentation RC.

### Boundary placement — Option A, ruled

```
InteractiveWorkspacePage
    ↓
existing grid child <div data-widget-code=...>   <-- survives the crash
    ↓
WidgetErrorBoundary                              <-- renders Failed in the cell
    ↓
SavedDashboardWidget
```

The boundary does **not** go inside `SavedDashboardWidget` — a throw in the card chrome
would escape it. A failed widget must never unwind to the route-level boundary.
Do not move layout responsibility into the boundary. Do not modify
`DashboardGridLayout` semantics.

### No second state union

The canonical seven-state authority already exists from **T-040** at
`src/authoring/authoringStates.ts`. Creating `WidgetState`, `DashboardWidgetState` or
`WidgetStateResolver` is a **design regression** — it would pass today and drift later.

```ts
export type AuthoringState =
  | "empty" | "loading" | "populated"
  | "filtered-empty" | "blocked" | "refused" | "failed";

export type AuthoringStateTone = "muted" | "amber" | "danger" | "none";

export interface AuthoringStateFacts {
  running: boolean;
  failure: string | null;
  refusal: string | null;
  blocker: string | null;
  rowCount: number | null;
  filtered: boolean;
}

export function resolveAuthoringState(facts: AuthoringStateFacts): AuthoringState
export function describeAuthoringState(facts: AuthoringStateFacts): AuthoringStateDescriptor
```

### Existing precedence — consume it, do not restate it

```
failure  →  refusal  →  running  →  blocker
         →  rowCount null            → empty
         →  rowCount 0               → filtered ? filtered-empty : empty
         →  otherwise                → populated
```

Refusal outranks loading because the server has already answered. Loading outranks a
stale result because the result is about to be replaced.

### Existing tones — no new tokens

| state | tone | token role |
|---|---|---|
| blocked, refused | `amber` | `--pp-amber` |
| failed | `danger` | `--pp-red` |
| empty, loading, filtered-empty | `muted` | `--pp-muted-2` / `--pp-muted-3` |
| populated | `none` | — |

Do **not** introduce `--widget-blocked`, `--widget-failed` or raw rgba. Colour is
supportive only: with CSS removed, the wording must still distinguish every state. The
T-040 module header states this explicitly (Golden Gate G17).

---

## 3. Facts mapping — agreed, implement exactly this

| fact | widget source |
|---|---|
| `running` | a query is in flight right now |
| `failure` | transport / unexpected **query** error |
| `refusal` | deliberate server or product rejection **only** — never an ordinary exception |
| `blocker` | a named unmet prerequisite (readiness is one producer, not the only one) |
| `rowCount` | `result.rows.length`; `null` before the first result |
| `filtered` | `hasEffectiveFilter(merged query filters)` |

### The two failure paths are separate, and the table above covers only one

```
query / transport failure
    → SavedDashboardWidget
    → AuthoringStateFacts.failure
    → resolveAuthoringState → "failed"

render exception
    → WidgetErrorBoundary
    → renders the Failed presentation using the same canonical semantics
```

A render exception happens **after the component has already come apart**, so the crashed
component cannot supply `AuthoringStateFacts`. The boundary does not fabricate a facts
object and does not call the resolver on behalf of a dead component — it renders the
Failed presentation directly, using the same wording, tone and token role as the resolved
`failed` state so the two paths are indistinguishable to the user and clearly distinct in
the code.

A deliberate refusal stays `refused` and must not become `failed` because its result path
is unusual.

### `hasEffectiveFilter`

Operates on **the same merged filter object actually sent to the query**, and consumes the
**existing** filter normalization semantics. Ignore `null`, `undefined`, `""` and `[]`. An
object-shaped filter whose value is structurally empty counts as empty **only if the
existing normalization already treats it as empty** — do not extend that judgement here.

**Do not invent a new generic filter-normalization framework inside T-051.** If the
existing semantics turn out to be insufficient, stop and report it as a finding rather than
widening scope. Do not create dashboard-only fake filter flags so a test can reach a branch.

---

## 4. Presenter ruling

One shared widget-state presenter is **allowed and preferred** over seven JSX branches,
seven components, or scattered ternaries. It must:

- consume `resolveAuthoringState` / `describeAuthoringState`
- **not** decide state independently
- **not** define another union
- **not** define different precedence

Wording:

| state | wording source |
|---|---|
| `failed`, `refused`, `blocked` | keep the descriptor's sentence — it carries the server's own reason and is surface-neutral |
| `loading`, `empty`, `filtered-empty` | widget-specific wording may replace the authoring text, which says "definition" and "run it again" |

`empty` must never read like `filtered-empty`. Empty means *there is nothing here yet* and
must not blame the user's selection; filtered-empty means *this selection returned nothing*.

Boundary fallback must keep the grid cell mounted, show `failed` semantics, identify which
widget failed, and offer a bounded retry only if a safe existing action exists. It must not
expose a raw exception, stack trace, or endpoint/database text to the presentation.
Diagnostic detail stays in the existing logging path — do not create another one.

---

## 5. Read these first, in this order

Do not rediscover the dashboard. Verify each hash against the live repo before relying on
this document — the repo has concurrent Worker-1 and Worker-3 activity.

| file | hash measured 16 Aug | why |
|---|---|---|
| `src/components/dashboard/SavedDashboardWidget.tsx` (457 lines) | `60D1E8578C96F0E74FE124761CD39D4EB1940F742854D1AA4C21D6208637C054` | the facts to map; **read the error/refusal section** |
| `src/authoring/authoringStates.ts` (219 lines) | `9892EC68D765CCCB6949CA9E4F6ACAB042B8D88587B133B27C1E46F689F9D170` | the authority; consume, do not copy |
| `src/authoring/AuthoringStateBanner.tsx` (43 lines) | `19C78D350E5F407989EB7EE9FDD7B351DEC731ADA2F9FEF0E27953C3D411F304` | tone→class mapping; reuse if surface-neutral |
| `src/authoring/authoringStates.test.tsx` (208 lines) | `6362054C236D033D338F1DA1F285C321B3BEA67D1255CA01511A5E5313C295DA` | existing precedence tests; follow this assertion style |
| `src/pages/Dashboard/InteractiveWorkspacePage.tsx` | — | the `visibleWidgets.map` grid-cell site where the boundary goes |

Verify with:

```powershell
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web
Get-FileHash src\components\dashboard\SavedDashboardWidget.tsx -Algorithm SHA256
Get-FileHash src\authoring\authoringStates.ts -Algorithm SHA256
Get-FileHash src\authoring\AuthoringStateBanner.tsx -Algorithm SHA256
```

### What was already measured, so you need not re-measure

- `InteractiveWorkspacePage` maps `visibleWidgets` into a plain `<div key=... data-widget-code=...>`
  wrapping `<SavedDashboardWidget>` — **no per-widget boundary exists today.** The nearest is
  route-level, so one widget throwing takes the whole dashboard down. This is the defect.
- `SavedDashboardWidget` holds only `result` and `error` — a two-valued model.
- The rendered vocabulary today is `EmptyInsightState` (one fixed sentence, *"No data for
  this selection…"*) plus the card's two T-046 refusal sentences, `RENDERER_UNAVAILABLE`
  and `REFUSAL_WITHOUT_REASON`.
- The `filters` memo in `SavedDashboardWidget` merges `widget.filterJson` with ten global
  filter keys and already skips `undefined`/`null`/`""` for the global side — but the
  `filterJson` base may still carry empty keys, so `hasEffectiveFilter` must normalise both.

---

## 6. The only investigation the next session should do

Answer this one question, then implement:

> How does `SavedDashboardWidget` currently distinguish transport failure, deliberate
> refusal, blocker, loading, and successful empty versus populated results?

Read `SavedDashboardWidget.tsx` around lines 40640–40760 of the code export (the `load()`
body and its `catch`) — that section was **not read** in the previous session and is the
one genuine unknown. Map what you find into `AuthoringStateFacts`. Do not start another
broad architecture investigation.

---

## 7. Required implementation shape

```
InteractiveWorkspacePage
    ↓
existing grid cell (unchanged)
    ↓
WidgetErrorBoundary
    ↓
SavedDashboardWidget
    ↓
existing AuthoringState authority (resolve + describe)
    ↓
thin widget presenter
```

---

## 8. T-051 acceptance

**Unit / component evidence** — all seven states reachable from real widget facts:

- exact state identity for each of the seven
- precedence proven (failure outranks all; refusal outranks loading; loading outranks blocker)
- `empty` wording ≠ `filtered-empty` wording
- `blocked` ≠ `refused`
- `refused` ≠ `failed`
- correct tone per the table in §2

**Playwright acceptance** — two scenarios, no single mega-test:

*Scenario 1 — failure isolation.* Load a page with multiple widgets, force a failure for
**one** widget only, confirm that grid cell shows `failed`, sibling widgets remain rendered,
and a sibling remains **interactive** (use an existing stable interaction). Must not depend
on the unfinished T-050 provenance chain.

**The failure must be forced from outside the product.** Use an existing deterministic
seam — Playwright request interception against that one widget's query, or an equivalent
already-present test seam. **Do not add a production-only `throw` flag, test hook, or
`data-force-error` prop to the widget.** Production code stays normal; the test creates the
condition. A seam that only exists so a test can pass is not evidence that the product
isolates failures.

*Scenario 2 — filtered-empty versus empty.* Apply a genuine narrowing filter that returns
zero rows → `filtered-empty`, wording says the selection matched nothing. Where practical,
prove the no-effective-filter zero result is `empty` with different wording.

No required test may be skipped. Scoped TypeScript, unit tests and Playwright all green.

**No manual visual walkthrough now.** Consolidated visual acceptance remains after
T-052 / M1-P3 as already ruled.

---

## 9. Tooling discipline — carried forward, non-negotiable

The T-050 presentation change took **five runs** to land. Its payload was correct on the
first attempt every time. All five failures were pack self-guards:

1. ASCII-checked the whole stylesheet — `legacy-005.css` has a pre-existing em dash.
2. Banned raw colour literals file-wide — `.table-wrap` legitimately owns one of them.
3. Banned `ProvenanceHandleRef` anywhere in the spec — the spec's own header used it to
   say what the pack does *not* certify.
4. The same class again, on `getBoundingClientRect` in an explanatory comment.
5. (Plus a test-oracle defect: geometry read from the painted rect mid-animation, and a
   colour compared as `rgb()` against Chromium's `color(srgb …)` serialisation.)

**The rule.** Packs assert **positive** facts about the region they own:

- pre-state hashes
- anchor uniqueness (exactly one occurrence, or abort)
- encoding round-trip proven lossless before writing
- required content present, scoped to the owned region
- tests actually pass

Packs do **not** attempt to prove semantic absence by scanning a file for a string. A file
that explains a thing contains the word for that thing. That is false safety and it burned
five of Karim's runs.

Also: **use a distinct filename per artifact revision.** A filename collision in `Downloads`
silently defeated the SHA256 gate once during T-049.

---

## 10. Ownership boundaries

**Worker 2 lock:** Dashboard / workspace / shared shell, AnalysisToolbox, Presentation E2E.

**Do not touch:** PR-050-01 or the widget provenance producer, Worker 1 verticals
(Connections / Dataset / Import / Relationships / Mapping Health / Genealogy), Worker 3
T-064, Findings, Assistant, ML.

---

## 11. Concurrent-work warning — read before any git command

The repo currently carries **uncommitted Worker-1 / Worker-3 work**, confirmed 16 Aug:
`DependencyInjection.cs`, `JobDefinition.cs`, `JobRunHistory.cs`, and two EF
configurations under `Infrastructure/Persistence/Configurations/Integration/`.

Exact-file staging only. **Never** run:

```
git add .        git add -A        git clean -fd
git reset --hard git restore .
```

Also note `tools/packs/` is gitignored, so packs are never committed. That is correct.

---

## 12. Next session — do this

```
NEXT SESSION:
verify live hashes
read SavedDashboardWidget error/refusal section
implement T-051 in one bounded pack
run targeted unit/component tests
run Playwright isolation + filtered-empty acceptance
exact-stage
commit
close T-051
immediately begin T-052
```

Do not start T-052 before the T-051 commit hash exists.

Run environment, unchanged:

```powershell
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web
$env:PPIQ_SMOKE_USERNAME = 'e2eadmin'
$env:PPIQ_SMOKE_PASSWORD = 'E2EAdmin123!'
$env:PLAYWRIGHT_API_URL  = 'http://localhost:5063'
```

API and web on `-Profile presentation`. Playwright with `--workers=1` when tests share a
dashboard resource.

---

## 13. ADDENDUM — rulings issued 17 Aug after the source read

The `SavedDashboardWidget` load/error/refusal section has been **read**. §6's unknown is
answered. Everything below is frozen; implement, do not re-ask.

### 13.1 How the live code supplies each fact

| fact | live source |
|---|---|
| `running` | **does not exist** — `load()` only calls `setError(null)`. Must be added, request-generation safe. |
| `failure` | `catch (loadError) → setError(loadError)`; `ApiError` carries `status`, `responseText`, `path`, `method` |
| `refusal` | **not from the query** — no refusal field on `DashboardWidgetQueryResult` and no server refusal convention. Producer is the existing T-046 client path: `RENDERER_UNAVAILABLE`, `REFUSAL_WITHOUT_REASON`. |
| `blocker` | `stale = staleRoles(roleBinding, resultColumns)`, conditionally — see 13.2 |
| `rowCount` | `result ? rows.length : null` |
| `filtered` | `hasEffectiveFilter(filters)` on the memo already sent to the query |

**Do not infer `refusal` from `ApiError.status`.** The product has established no HTTP
refusal convention; inventing one is the T-050 provenance mistake in another costume. Every
thrown error is `failure`.

**Do not reinterpret `compatibilityRule === null` as `blocked`** where it corresponds to the
existing `RENDERER_UNAVAILABLE` refusal path.

### 13.2 staleRoles is not automatically a blocker — RULED

```
stale roles exist + usable category/value bindings remain
  → keep the existing stale advisory banner
  → DO NOT set AuthoringStateFacts.blocker
  → state resolves normally; the chart may still render

stale roles exist + required usable bindings no longer remain
  → blocker = named stale-mapping prerequisite
  → canonical state = blocked
  → no misleading chart rendering
```

This preserves M1-16's deliberate *degrade honestly instead of going blank*. It determines
whether the `blocker` **fact** is true; it does not change what `blocked` **means**. Do not
alter canonical precedence, do not add a state, do not retire M1-16.

### 13.3 Raw exception must be removed — RULED

The current render block contains `<p>{String(error)}</p>`, which puts the raw exception in
front of the user. Remove it. Canonical `failed` wording only. Exception text, stack,
endpoint and database detail stay diagnostic — the existing boundary beacon already carries
them.

### 13.4 Running must be race-safe — RULED

The current loading test is `{!error && !result}`, so a refetch after a filter change keeps
the previous `result` mounted and presents it as current. Required:

```
refetch begins
  → running = true
  → canonical state = loading
  → previous result NOT presented as the current answer
```

Guard against an older request resolving after a newer one. Reuse the smallest existing
cancellation/generation pattern; do not introduce a query framework.

### 13.5 ErrorBoundary takes an additive fallback prop — RULED

`src/components/standard/ErrorBoundary.tsx` accepts only `children`, `fallbackTitle`,
`routePath`, and renders a fixed page-scale panel. Add an optional `fallback?: ReactNode`
and one branch in `render()`. Purely additive; every existing call site unaffected;
`componentDidCatch`, the diagnostics beacon and `reset` untouched. No new boundary class.
The custom fallback must not receive or display the raw `Error` or stack.

Confirmed map-site anchor in `InteractiveWorkspacePage.tsx` — cell **outside**, boundary
wrapping only the widget subtree:

```tsx
<div key={...} data-widget-code={...}>
  <ErrorBoundary fallback={/* canonical failed presentation */}>
    <SavedDashboardWidget ... />
  </ErrorBoundary>
</div>
```

### 13.6 Additional required test cases

Beyond §8:

```
CASE A  stale mapping + usable bindings remain
        → advisory visible, state populated, chart renders

CASE B  stale mapping + no usable required binding
        → state blocked, wording names the prerequisite, no chart

query exception → failed, and raw exception text NOT exposed

refetch while a previous result exists
        → loading, old result not presented as current
```

### 13.7 Operating rule — findings are triaged, not escalated

```
finding
  ↓
Can I resolve it inside frozen semantics and my ownership?
  ├─ YES → resolve, implement, test, report with the commit
  └─ NO  → is it one of the five STOP categories?
            ├─ YES → STOP
            └─ NO  → resolve it myself
```

**STOP only for:** a missing producer or backend contract; an ownership or file-lock
conflict; live source materially contradicting a frozen public product contract; a fix that
requires changing previously accepted product semantics; a fix that requires work in
another worker's subsystem.

Everything else — local state derivation, choosing between existing canonical states,
preserving an accepted degradation, additive shared-shell corrections, test-oracle fixes,
wording, race-safe loading — is engineering execution. Decide it and report it with the
commit.

The next Worker-2 message on T-051 is either a genuine STOP from those five, or:
implementation complete, tests green, files staged, commit hash, T-051 CLOSED, T-052 started.

### 13.8 Files under pre-state guard — five, not four

| path | change | hash (16 Aug export) |
|---|---|---|
| `src/components/standard/ErrorBoundary.tsx` | + `fallback` prop | verify live |
| `src/pages/Dashboard/InteractiveWorkspacePage.tsx` | boundary inside grid cell | verify live |
| `src/components/dashboard/SavedDashboardWidget.tsx` | running flag, facts, presenter | `60D1E857…` |
| `src/authoring/authoringStates.ts` | read-only, verify untouched | `9892EC68…` |
| `src/authoring/AuthoringStateBanner.tsx` | read-only reference | `19C78D35…` |

New files must not already exist: the widget state presenter, its unit test, and the
Playwright spec. Existing files get hash pre-state verification; new files get an
existence check. Abort before writing on any mismatch and report only that path.
