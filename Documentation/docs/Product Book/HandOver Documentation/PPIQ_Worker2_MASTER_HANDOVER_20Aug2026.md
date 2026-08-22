# PPIQ — WORKER 2 MASTER HANDOVER

**Session** 16–20 Aug 2026 · **Author** Worker 2 (Claude) · **For** the next Worker-2 session
**Status** authoritative. Read §1 and §12 first; the rest is reference you will need mid-task.

This document exists so the next session does **not** re-investigate, re-read source it already
has answers for, or re-run tests that already passed. Everything below is measured, not assumed.
Where something is unverified, it says so explicitly.

---

# 1. START HERE — the single most important section

## 1.1 Where we stopped

```
LAST COMPLETED ACTION
  Application tab rebuild applied to the website     Apply-SOU-Application-Tab-v3.ps1
  RESULT: PASS  (all 5 anchors, all self-checks, npm run build exit 0)
  NOT COMMITTED.

IMMEDIATE NEXT ACTION
  Run  Apply-SOU-Application-Tab-Followup.ps1
       SHA256 040912E3745526A4BF1EE679532A616FCFC06BD671BD3B92BDE7648A38899C3C
  It does three things: world-map contrast fix, refresh the four cropped
  screenshots from docs\images, then build + stage + commit + push.
  It has NEVER been run. It is the only outstanding executable step.
```

## 1.2 Commits produced this session

| Commit | What |
|---|---|
| `cc4d88444a81b714436aa3f377c2042bb8212bbb` | T-049 dashboard layout persistence certified across three viewports |
| `6c99e0911b91a60c767c4db27d08fa9ccee28af1` | T-051 per-widget failure isolation and the seven canonical states |
| `d6870d0aac4719918864dd44798939a4980707b3` | T-050 presentation half — drawer tokens, RTL, reduced motion |
| `4fdd311ad99685779ff759cdeeca9feba218b46a` | T-050 provenance half — population + execution evidence |
| `228adbda769598c1fad5ffe7223a0612dcbdac7a` | T-052 remove hardcoded parameter default (API client) |
| `24868e3944e48dea99ac5501345b570972200fa3` | T-052 corrective — correlation page default |
| `40b56d99…` | Website corporate alignment (9 files) |
| `964608045942527c281ae32a05484d64ffaf8103` | **PR-050-01** (Worker 1) — governed widget result execution evidence |

## 1.3 Task status at close

```
T-049  CLOSED
T-050  CLOSED    (both halves)
T-051  CLOSED
T-052  CLOSED
Website corporate alignment      COMMITTED
Website Application tab rebuild  APPLIED, NOT COMMITTED  <-- resume point
Company Profile v1.1             delivered
Executive Profile v1.0           delivered
Sales deck (9 slides)            delivered
Presentation deck (19 slides)    delivered
```

---

# 2. HOW TO WORK — rules Karim gave, in his words and mine

These were issued as rulings during the session. They are not style preferences. Violating
them is what caused most of the wasted runs.

## 2.1 The STOP rule

STOP **only** for one of five conditions:

```
1. missing producer / missing backend contract
2. ownership or file-lock conflict
3. live source materially contradicts a frozen public product contract
4. a fix requires changing previously accepted product semantics
5. a fix requires work in another worker's subsystem
```

**Session length, fatigue, subjective confidence and "defect-rate concern" are NOT stop
conditions.** Karim ruled this explicitly and repeatedly. If you feel the work is degrading,
open a fresh session — do not defer the work back to him.

## 2.2 Findings are triaged, not escalated

```
finding
  ↓
Can I resolve it inside frozen semantics and my ownership?
  ├─ YES → resolve it, implement, test, report it WITH the commit
  └─ NO  → is it one of the five STOP categories?
            ├─ YES → STOP
            └─ NO  → resolve it yourself
```

Do not send one-finding-per-message. Karim's exact instruction: *"Do not return local findings
individually."*

## 2.3 Uncertainty maps to a test, not to a question

> "uncertainty → targeted test · defect risk → stronger gate · mapping ambiguity → explicit
> invariant. NOT: uncertainty → stop session."

When you are unsure whether a mapping is right (e.g. does visual index equal backend row
index?), write the test that would fail if you are wrong. Do not ask.

## 2.4 No design pass when the design is frozen

If the design is frozen and the live source matches the handover, **do not send a design
report**. Go straight to implementation. This was the single biggest source of wasted turns.

## 2.5 Evidence and honesty rules (product-level, non-negotiable)

- A refusal or a measured gap is a valid result. Never retrofit a band from observed data,
  never weaken a threshold to produce green.
- False PASS is never acceptable. Truthful exact result > explicit bounded refusal > partial value.
- Name your own defects before Karim finds them.
- A claim must trace to a file, a line number and a commit — never to a document date.
- Read source documents directly; never rely on a conversation summary of them.
- Genericity (Rule 1): no industry vocabulary, dashboard code, demo identity or equipment name
  in the engine or on a generic surface.

## 2.6 Communication

- Karim issues rulings in fenced code blocks. **Those blocks are the contract.**
- Informal direction in Egyptian Arabic; all technical content in English.
- He expects two lettered options (A/B) when a real choice exists, and a single next action
  when threads multiply.
- He wants the executable command block **every time** — including `Move-Item` from Downloads,
  `Unblock-File`, the parser gate, the hash check, and the run line. Omitting the Move-Item is
  a defect; he called it out.

---

# 3. TOOLING — the pack discipline, and every way I broke it

## 3.1 The pack contract

Every change is delivered as a PowerShell 5.1 apply-pack:

```
preflight (paths, pre-state hashes, product facts verified from source)
  → verify EVERY anchor before mutating anything
  → backup every touched file
  → apply
  → on-disk self-check
  → gated build / test
  → auto-revert on any failure
stages nothing · commits nothing · never reset/restore/clean/checkout/add -A
```

## 3.2 THE LESSON THAT COST THE MOST: no negative text scans

I wrote guards that scanned a file for the **absence** of a string. Every one of them failed
on the pack's own prose, five times:

| Attempt | Guard | Why it failed |
|---|---|---|
| T-050 v1 | ASCII-check whole stylesheet | `legacy-005.css` has a pre-existing em dash in a comment |
| T-050 v2 | ban raw colour literals file-wide | `.table-wrap` legitimately owns `rgba(90,194,255,.14)` |
| T-050 v3 | ban `ProvenanceHandleRef` in the spec | the spec's own header used it to say what it does NOT cover |
| T-050 v4 | ban `getBoundingClientRect` | same, in an explanatory comment |
| T-051 | — | (guard removed before it could repeat) |

**RULE: packs assert POSITIVE facts about the region they own.** Pre-state hashes, anchor
uniqueness, encoding round-trip, required content present, tests passing. A file that explains
a thing contains the word for that thing.

## 3.3 Anchor lessons

- **`}` matches inside `};`.** `IndexOf` found the closing brace of a `type X = {...};` alias
  when the anchor ended in `}`. Anchor on the whole `export interface Foo { ... }` instead.
- **A bare property line is never unique.** `options?: DashboardWidgetQueryOptions | null;\n}`
  appeared 3× in one file.
- **Verify anchors against the LIVE export, not against your reconstruction.** I extracted a
  block, edited my copy, then used my copy as the anchor — it no longer matched.
- **Re-extract verbatim** with `src.index(...)` from the export file, then assert `count == 1`
  before shipping.

## 3.4 Baseline before applying — judge on DELTA

I aborted a correct apply because a validator was **already red** on a clean tree. The pack
blamed itself. Fix: run validators BEFORE touching anything, record the failing set, and after
applying fail only on **new** failures.

```
pre-existing failure  → report as [pre-existing], continue
new failure           → [NEW], abort and revert
resolved failure      → report as resolved
```

## 3.5 Validators write failures to STDERR

`validate-commercial-v2.mjs` prints `[PASS]` to stdout and `[FAIL]` to **stderr**. Reading only
stdout found every success and no failure, which my delta logic then read as "nothing new" —
a false green. **Read stdout AND `<log>.err`.**

## 3.6 Non-zero exit with nothing parsed is NOT a pass

If a step exits non-zero and your parser extracts zero failures, that is an **unattributable**
failure. Abort and dump the log. Never report a pass.

## 3.7 Filename collisions defeat the SHA256 gate — 3 occurrences

`Move-Item` from `Downloads` picked up a **stale earlier file with the same name** three times.
Once it ran the wrong pack entirely and the hash mismatch was printed but not thrown on.

**RULE: every artifact revision gets a distinct filename (`-v2`, `-v3`, …), and the hash check
`throw`s rather than prints.**

## 3.8 Mixed line endings

Successive Python patches left **274 LF-only lines** inside a CRLF file. Split-by-line edits
then silently mis-targeted. **Normalise the whole file at the end of every generation.**

## 3.9 Guard-versus-implementation mismatch

Twice, I deleted a stage but left its self-check behind, or the marker the stage writes
(`SOU-ALIGN: /about hero type scale`) differed from the marker the check looks for
(`/* SOU about hero type scale */`). **The marker must be one string used in all three places:
idempotence guard, payload, self-check.**

## 3.10 Rollback must never pass `$null` to `-LiteralPath`

A file the pack **creates** has no backup. `Backup-File` records `$null`. Test the null before
any `-LiteralPath` call:

```powershell
if (-not [string]::IsNullOrWhiteSpace([string]$b.Backup)) { restore }
elseif (Test-Path -LiteralPath $b.Target)                 { remove }
```

## 3.11 Sweep acceptance is RESIDUAL, not replacement count

`Min = 1` failed correct runs where the source was already aligned. Correct rule: count
occurrences, replace, **rescan**, fail only if `residual > 0`. Report `already aligned;
residual 0` when initial was 0.

## 3.12 PowerShell is available in the sandbox

There is **no** pwsh by default. Install it — the self-tests then become real:

```bash
curl -sSL -o pwsh.tar.gz \
  https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/powershell-7.4.6-linux-x64.tar.gz
mkdir -p /opt/pwsh && tar -xzf pwsh.tar.gz -C /opt/pwsh && chmod +x /opt/pwsh/pwsh
```

Use it to parse every pack **and** to unit-test extracted functions via AST:

```powershell
$ast = [System.Management.Automation.Language.Parser]::ParseInput($src,[ref]$null,[ref]$null)
$fn  = $ast.FindAll({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] `
        -and $n.Name -eq 'Backup-File'}, $true)
Invoke-Expression $fn[0].Extent.Text
```

## 3.13 My brace/paren heuristic is unreliable — 4 false alarms

Counting braces in a file containing regex literals, string literals and comments produces
false imbalances. **The PowerShell parser is the authority.** Stop reporting heuristic counts.

## 3.14 Multi-line `git commit -m` breaks in PowerShell

It split the message and the fragments became redirection targets, creating two junk files in
the repo root. **Single-line `-m` only, or `-F`.**

---

# 4. T-049 — DASHBOARD LAYOUT PERSISTENCE

**Closed `cc4d8844`.** Files: `e2e/t049-layout-persistence.spec.ts`,
`src/state/__tests__/dashboardLayoutSerialisation.test.ts`.

## 4.1 Final evidence

```
license.setup   PASS
1920x1080       PASS
1440x900        PASS
1280x800        PASS
4 passed (43.8s), zero skipped, --workers=1
unit dashboardLayoutSerialisation  6/6
spec SHA256 AF64FF500CE02F1B7E12DE7B9E8F693B2D26747CC1F8268FAD40CAFF504AE5D3
```

## 4.2 The three acceptance laws (this is the pattern to reuse)

```
1. saved canonical        != original canonical
2. rendered-after-save    == saved canonical
3. rendered-after-reload  == saved canonical
```

"Changed" means the **stable post-save state**, never the pixels immediately after mouse-up.

## 4.3 What took five revisions, and why — read this before writing any Playwright spec

**R1 — the spec assumed a login page.** This build has **no `/login` route and no login page
component**. `AuthContext` self-bootstraps via `apiClient.refresh()` using the HttpOnly cookie,
falling back to `apiClient.login(DEMO_USER, DEMO_PASS)` from Vite env. The canonical browser
auth contract is:

```ts
import { prepareAuthenticatedPage } from "./helpers/hardening";
const token = await prepareAuthenticatedPage(page, request);   // returns the access token
```

`playwright.config.ts` has **no `storageState`** — the `setup` project is a plain dependency
running `license.setup.ts`. Every chromium test starts unauthenticated in the browser context.

**R2 — the pixel oracle was invalid.** react-grid-layout selects its breakpoint from the
**container** width, not the viewport. At 1440 the shell is 1108px, which is `md`, not `lg`.
And `getBoundingClientRect` is viewport-relative, so page scroll masquerades as layout change.
Fix: measure **shell-relative**, and compare **canonical documents** (PATCH body vs definition
GET) as the authority.

**R3 — the join between namespaces did not exist.** The canonical layout keys entries by the
React key, which `InteractiveWorkspacePage` sets to `widget.id` (a GUID). The DOM identity is
`data-widget-code`. They are different namespaces. Build the map by walking the definition
payload for objects carrying both `id` and `widgetCode`.

**R4 — the real defect was hydration.** The pre-interaction capture showed every widget at
1 column × 1 row stacked at 60px — that is react-grid-layout's **fallback placement for a child
with no layout entry**, i.e. the persisted document had not been applied yet. A fixed
`waitForTimeout` is not an acceptance mechanism. Replace with polling until the DOM converges
on the canonical geometry.

**R5 (final) — the interaction stopped working once hydration was fixed.** Two helper defects:

1. `page.mouse` works in **viewport** coordinates and does **not** scroll. Targets below the
   fold were "dragged" in empty space. → `await handle.scrollIntoViewIfNeeded()`.
2. Targets were chosen alphabetically (`PO_AREA`, `PO_BAR`) — the 1-row-tall widgets at the
   bottom. → choose by canonical size (tallest, then widest, then top-most).

## 4.4 The RGL geometry formula — verified to within 1px

```
GRID_MARGIN = 18 ; GRID_ROW_HEIGHT = 42 ; containerPadding = [0,0]
cols: lg 12 · md 10 · sm 6 · xs 4 · xxs 2
breakpoints: lg 1400 · md 1100 · sm 800 · xs 560 · xxs 0

columnWidth = (shellWidth - MARGIN*(cols-1)) / cols
left   = round((columnWidth + MARGIN) * x)
top    = round((ROW_HEIGHT + MARGIN) * y)      // 60 * y
width  = round(columnWidth*w + max(0,w-1)*MARGIN)
height = round(ROW_HEIGHT*h + max(0,h-1)*MARGIN)
```

Validated at shell 1108 / md: predicts all ten widgets to within 1px (one sub-pixel width
rounding). **This is how you detect hydration deterministically instead of sleeping.**

## 4.5 Product facts confirmed

- `draggableHandle=".dashboard-widget__drag-handle"` — a press anywhere else is ignored by RGL.
- Grid shell class: `.dashboard-grid-layout-shell`; `data-edit-mode={isEditing ? "on":"off"}`.
- Stable test ids: `workspace-edit-toggle` (`aria-pressed`), `workspace-save-layout`,
  `workspace-reset-layout`. `StandardButton` spreads rest props, so `data-testid` reaches the DOM.
- Layout save endpoint: `PATCH /analytics/dashboard/definitions/{id}/layout`.
- `/dashboard` → `InteractiveWorkspacePage dashboardCode="PRODUCTION_OVERVIEW"`.
- `compactType="vertical"`, `preventCollision={false}` — the mouse's requested destination is
  not a durable row; compare the post-compaction state the product serialised.

## 4.6 Open findings from T-049

**`W2-GRID-DEFAULTS-01`** — `src/state/DashboardGridLayoutContext.tsx` hardcodes a nine-item
`defaultLayouts` and `enforceConstraints` merges those nine into every layout on **serialize
AND load**. Measured: **19 persisted entries against 10 rendered widgets**, at every breakpoint
and viewport. The nine keys are dataset-specific vocabulary in product code:

```
defectTrend · defectBreakdown · riskDistribution · sourceContribution
riskScatter · qualityHeatmap · topContributors · dataQuality · materialExplorer
```

Rule 1 violation. Structural. Not fixed.

**`PRODUCTION_OVERVIEW` lg layout damaged** — every widget is one grid row tall. This was
written by my own early unhydrated T-049 runs and then faithfully restored by the isolated
spec as "the baseline". Persisted data in `ppiq_presentation`, not code. **Must be repaired
before T-078 / T-080** or visual regression will baseline the damage.

---

# 5. T-051 — WIDGET FAILURE ISOLATION AND THE SEVEN STATES

**Closed `6c99e091`.** 9 files.

## 5.1 Evidence

```
WidgetStatePanel.test.tsx + hasEffectiveFilter.test.ts   10/10
Playwright t051-widget-isolation.spec.ts                  4/4
tsc -b                                                    GREEN
Vite production build                                     GREEN
```

## 5.2 The authority — do NOT create a second state model

`src/authoring/authoringStates.ts` (from T-040) already owns this:

```ts
type AuthoringState = "empty"|"loading"|"populated"|"filtered-empty"|"blocked"|"refused"|"failed";
type AuthoringStateTone = "muted"|"amber"|"danger"|"none";
interface AuthoringStateFacts {
  running: boolean; failure: string|null; refusal: string|null;
  blocker: string|null; rowCount: number|null; filtered: boolean;
}
resolveAuthoringState(facts) · describeAuthoringState(facts)   // heading, sentence, nextAction, tone
```

Precedence: `failure → refusal → running → blocker → rowCount null → rowCount 0
(filtered ? filtered-empty : empty) → populated`.
Tones: blocked/refused `amber`, failed `danger`, empty/loading/filtered-empty `muted`,
populated `none`. **No new tokens were needed.**

## 5.3 Facts mapping as implemented

| fact | source |
|---|---|
| `running` | new flag, set at request start, cleared only by the request the effect's `ignore` guard still considers current |
| `failure` | `catch (loadError)`; **safe sentence only**, never the exception |
| `refusal` | **null from the query path** — the product has no server refusal convention. Never inferred from HTTP status. |
| `blocker` | `stale.length > 0` **and** the bound value column absent from `resultColumns` |
| `rowCount` | `result ? rows.length : null` |
| `filtered` | `hasEffectiveFilter(filters)` |

## 5.4 The staleRoles ruling (important — it preserves M1-16)

```
stale roles + usable bindings remain  → advisory banner only, chart still renders
stale roles + required binding gone   → blocker, canonical state = blocked, no chart
```

M1-16 deliberately renders the stale advisory **and** the chart — "degrades honestly instead
of going blank". Mapping all stale roles to `blocked` would have regressed it.

## 5.5 Defects found and fixed during T-051

- `<p>{String(error)}</p>` **rendered the raw exception** into the presentation. Removed.
- Loading test was `{!error && !result}` — true only before the FIRST result, so a refetch
  showed the previous result as current. Replaced with the race-safe `running` flag.
- `ErrorBoundary` at `src/components/standard/ErrorBoundary.tsx` accepts only
  `children`/`fallbackTitle`/`routePath` and renders a **page-scale** panel. Added an additive
  `fallback?: ReactNode` — no new boundary class, `componentDidCatch` / diagnostics beacon /
  `reset` untouched.
- **`widgetState` was already taken** in that file by the layout context — a *display
  preference* record `{hidden, collapsed, fullscreen, chartType}`, not a state model. Local
  renamed `resolvedState`. `tsc` caught it; the pack's own checks could not.
- `tsconfig.app.json` **excludes `e2e`**, so the repo-wide `tsc --noEmit` never type-checks
  specs. Use a scoped invocation naming the spec path.

## 5.6 Structure

```
InteractiveWorkspacePage
  → existing grid cell <div data-widget-code>      ← stays OUTSIDE (keeps geometry on crash)
    → ErrorBoundary fallback={<WidgetStatePanel facts={WIDGET_RENDER_FAILURE_FACTS} />}
      → SavedDashboardWidget
        → resolveAuthoringState / describeAuthoringState
          → WidgetStatePanel
```

Query failure → `SavedDashboardWidget` → `AuthoringStateFacts.failure`.
Render exception → `ErrorBoundary` → same canonical Failed presentation, **without** pretending
the crashed component supplied facts.

---

# 6. T-050 — DRILL-DOWN POPULATION AND EXECUTION EVIDENCE

**Presentation half `d6870d0a` · provenance half `4fdd311a`.**

## 6.1 Presentation half evidence

```
Playwright t050-drilldown-drawer-presentation.spec.ts   4 passed + license.setup
```

Corrections: raw literals → tokens (`--pp-cyan`, `--pp-bg-0/1`, `--pp-border`,
`--pp-border-soft`); `top/right` → `inset-block-start` / `inset-inline-end`; `border-left` →
`border-inline-start`; `border-bottom` → `border-block-end`; the light-theme override in
`legacy-005.css` **deleted** (tokens already theme-flip).

**The RTL bug was in the keyframe, not only the position.** `translateX(100%)` is fixed, so in
Arabic the drawer sat on the left and slid in from the right. The distance is now a variable
that `html[dir="rtl"]` flips, along with the gradient origin and the box-shadow offset —
neither of which has a logical form.

## 6.2 Two test-oracle defects worth remembering

- Geometry read from `getBoundingClientRect` on an element mid-animation returned the
  keyframe's **opening frame** (`right = 1920` instead of 1440 = one width of
  `translateX(+100%)`; `left = -480` in RTL). Use `offsetLeft`/`offsetWidth` — the layout box,
  which ignores transforms.
- **Chromium serialises `color-mix` as `color(srgb 0.00784314 …)`**, never as `rgb(2,132,199)`.
  Produce the expected value by asking the browser to compute the same expression, then compare
  two browser-serialised strings.

## 6.3 PR-050-01 contract (Worker 1, `96460804`) — transcribed from the diff

```
REQUEST
  DashboardWidgetQueryDto
    + ExecutionIdentity? : { PageCode?, WidgetCode?, WidgetDefinitionId? }
  DashboardWidgetQueryOptionsDto
    + IncludeExecutionEvidence? : bool

RESPONSE
  DashboardWidgetQueryResultDto
    + ExecutionEvidenceHandle? : ProvenanceHandleRefDto { Kind, Id, Detail? }
    + RowPopulations?          : DashboardWidgetRowPopulationDto[]

  DashboardWidgetRowPopulationDto
    RowIndex, RowFingerprint?, DimensionBindings, MeasureCode,
    ParameterCode?, FilterContextFingerprint, PopulationCount?
```

Wire casing is camelCase.

**Semantics that constrain everything:**

- `rowPopulations` is **always computed** and writes nothing. Describing a point is free.
- `executionEvidenceHandle` is **opt-in**. An ordinary render is a READ. A dashboard that wrote
  evidence on every refresh would turn the evidence store into an event log.
- `pageCode` and `widgetCode` are **both mandatory** when evidence is requested; otherwise the
  service returns values, writes nothing, offers no handle, and adds a warning starting
  `execution_evidence_unavailable:`.
- `RowFingerprint` is **semantic identity only** — excludes the aggregate value, the label, the
  generation time and the row's position.
- **`PopulationCount` may be null and is NEVER the row count.** Five bars do not mean five of
  anything.
- Execution evidence is **not** physical source-row lineage. No consumer may present it as such.

## 6.4 The T-073 resolver — already exists, no new endpoint

```ts
assistantApi.getWidgetResultEvidence(evidenceId): Promise<AssistantWidgetResultEvidence|null>
GET /api/assistant/evidence/widget-result/{evidenceId}
404 → null      // "not available to this tenant" is a different thing from the request failing
```

Import path is **`@/api/assistantApi`** (I first guessed `../../api/assistant/assistant.api` —
wrong). `evidenceId` is `ProvenanceHandleRefDto.Id` verbatim. `EvidencePanel` is **not** the
consumer to reuse — it is correlation-specific and takes already-resolved evidence.

## 6.5 The row-identity invariant — the most important design decision

Recharts passes the **datum object** to `onClick`, not an index. So the backend index is
stamped on each row before any chart sees it:

```
result.rows[i] → { ...row, __ppiqSourceRowIndex: i } → chart data
               → sort / slice / project → onClick(datum) → datum.__ppiqSourceRowIndex
```

Survives reorder, sort, `MiniTable`'s 50-row slice and field projection — proven by 8/8 tests
including Karim's exact case (backend A,B,C rendered C,A,B; click first visual → index **2**).
`populationForRow` matches by `rowIndex`, **never by array position**.

The **execution snapshot** (filters, options, bindings, identity, and that render's
`rowPopulations`) travels the same way, which makes the stale-context invariant true by
construction:

```
render under A → filters move to B → click the old point → evidence request still executes A
```

## 6.6 Provenance half evidence

```
drilldownRowIdentity.test.ts        8/8
drilldownEvidence.test.ts           7/7
drilldownExecutionSnapshot.test.ts  7/7
src/state total                    48/48
src/components/dashboard           39/39
tsc -b                             GREEN
Playwright t050-drilldown-provenance.spec.ts  green (reported by Karim, not observed by me)
npm run build                      GREEN
```

## 6.7 Three evidence outcomes must stay distinct

```
unavailable  producer said so (execution_evidence_unavailable warning) — decisive, resolver not called
notFound     resolver returned null (404) — not available to this tenant / no longer retained
error        the request itself threw — transport failure, NOT an absence of evidence
```

`describePopulationCount(null)` → `"not reported by this source"`. Test asserts `0` still
prints `0`, so a real zero cannot become "unknown".

## 6.8 Where the types live

`DashboardWidgetQuery*` are declared in **`src/api/product-core/dashboard-widget-types.ts`**.
`productApiClient.ts` only re-exports them. I asserted the wrong owner and the pack aborted.

---

# 7. T-052 — REMOVE HARDCODED INDUSTRY PARAMETER

**Closed `228adbda` + `24868e39`.** Evidence: focused tests 5/5, tsc GREEN, build GREEN.

## 7.1 The defect

```ts
// src/api/productCoreApiClient.runtime.ts  (getGenealogyAwareCorrelation)
parameterCode: filters.parameterCode || "CastingSpeed"
```

A steel literal in a generic API client, reachable by any customer.

## 7.2 The fix and why omission is safe

`parameterCode: filters.parameterCode` — no fallback. **`buildQuery` already skips `null`,
`undefined` and `""`**, so an unselected parameter never reaches the wire. No registry fetch was
added: truthful absence beats an invented customer parameter.

There are **two** `buildQuery` implementations (`src/api/http/apiClient.ts` and
`productApiHardening.implementation.ts`). They express the same skip with the operands in a
different order. Follow the **client's own import** and test the **rule**, not one spelling.

## 7.3 The corrective — the fix moved the literal one layer up

`src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx` seeded
`useState("CastingSpeed")` and fed it straight into `getGenealogyAwareCorrelation`.
`/correlations` routes to that page, so the customer still received an answer about a parameter
they never chose. Changed to `useState("")`. **I originally misclassified this as a harmless
"page default" — Karim caught it.**

## 7.4 `CastingSpeed` classification (full tree)

| file | verdict |
|---|---|
| `src/api/productCoreApiClient.runtime.ts` | product path — **fixed** |
| `src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx` | product path — **fixed** |
| `src/components/analytics/ReadModelWidgets.tsx` | demo widget (`CastingSpeedByGradeWidget`) — allowed, `W2-GENERIC-03` |
| `src/pages/Analytics/AnalyticsWidgetsPage.tsx` ×2 | demo page, explicit prop — allowed |
| `tools/phase56/apply-…cjs` | migration tooling, not shipped — allowed |

Still open, explicitly out of T-052 scope: **`W2-GENERIC-02`** — `defectType || "SurfaceCrack"`
and `linkMode || "DownstreamChildren"` in the same call and on the same page.

---

# 8. WEBSITE — CORPORATE ALIGNMENT (committed `40b56d99`)

## 8.1 Nine files

```
index.html
src/App.tsx
src/brand/plantProcessBrand.ts
src/components/sections/FounderAuthority.tsx
src/components/sections/IntegrationEcosystem.tsx
src/components/seo/RouteMeta.tsx            (new)
src/content/phase1WebsiteProof.ts
src/pages/DeckPage.tsx
src/styles/phase10.css
```

## 8.2 What was wrong, and what it became

| # | was | now |
|---|---|---|
| 1 | root `<title>` / OG described one product on a company domain | company-level; PPIQ keeps product SEO on its own route |
| 2 | `SOU Industrial Intelligence` | `SOU Industrial Software` |
| 3 | `info@plantprocessiq.com` | `info@souindustrial.com` |
| 4 | `13+ YEARS`, "more than a decade", "thirteen years inside the plant" | 14 years |
| 5 | `$12k–$50k`, `$6k–$25k`, five plan figures | quotation wording, **no new price invented** |
| 6 | "If it has a database … can read it" | per-source-class honesty |
| 7 | `DÃ¼sseldorf`, `Â·` (mojibake) + ASCII `Duesseldorf` ×3 | `Düsseldorf`, `·` |
| 8 | founder name inconsistent | `Karim Gamal` everywhere (**approved form**) |

## 8.3 There was NO per-route metadata mechanism

No `document.title`, no Helmet, no SEO hook — one static `index.html` title served every path.
Correcting `index.html` alone would have made the PlantProcess IQ page describe the company.
`src/components/seo/RouteMeta.tsx` was created and mounted in `App`, longest-prefix matching,
corporate fallback.

## 8.4 The site contradicted ITSELF on pricing

`src/content` already stated *"no price appears anywhere - 6.3.8"* while the pricing page
published five figures.

## 8.5 12 PRE-EXISTING acceptance failures — a finding for Karim, NOT ours

```
validate-commercial-v2.mjs   9 failures
tests/e2e/commercial-v2.spec.ts  3 failures
```

They assert a retired **"Stop the Losses / The Crime Scene / Tracing the Footprints /
The Trial & Verdict / Execution & ROI"** narrative, `PlantProcess IQ keeps its own richer page`
(`product.isFlagship ? <PlatformPage />`), `trust language Read-only by design`, and
`/products/qes → /packs/quality`.

**The two instruments contradict each other:** the validator asserts *"no product redirects into
the capability pack /packs/quality"*; the E2E spec asserts `/products/qes` **must** redirect to
`/packs/quality`. The site follows the validator — the five-product architecture is correct and
the E2E spec was never updated.

**Do not delete assertions to manufacture green.** Either they are corrected to the approved
five-product truth, or the content returns. Karim's decision.

`renderedSource` in that validator = `App.tsx + graphics + phase1WebsiteProof.ts`.

---

# 9. WEBSITE — PRESENTATION / APPLICATION TAB (applied, NOT committed)

`Apply-SOU-Application-Tab-v3.ps1` — **RESULT: PASS**, 5 anchors, all self-checks, build exit 0.
Files: `src/pages/DeckPage.tsx`, `src/styles/phase10.css`.

## 9.1 What it did

1. **Generic by rule** — `FURNACE HMI / CASTER HMI / MILL HMI / GAUGE PC / LAB SHEET / YARD LIST`
   → `PRODUCTION UNIT A/B/C · INSPECTION DEVICE · LABORATORY SHEET · STORAGE LIST`.
   Source chain `EAF · LF · CCM · HSM · QA · YARD` → **source kinds**:
   `Excel · Log files · OPC · Oracle · SQL Server · PostgreSQL · MySQL · SAP`.
2. Tab **opens on the in-house expert**, lighter surface.
3. `LayersGraphic` — BI layer above, engine below, animated flow.
4. Data journey — source kinds → safe copy → one plant → engine → dashboards.
5. Two `ShotSection`s, **two screenshots each**, from `public/shots/{canvas1,canvas2,bi1,bi2}.png`
   (copied by the pack from `docs/images/{Canvas1,canvas2,BI1,BI2}.png`, case-insensitive).
6. **Six capabilities in the language of the result** (see §10).

## 9.2 Defects Karim reported and I fixed

- **Contrast** — cards were `rgba(47,179,201,.05)` on near-black with a `.22` border and
  `opacity:.82` body text. Now a real gradient fill `#17334D → #0E2134`, `.42` border, shadow,
  body `#BFD4E2` at full opacity, wiring `#6FE3F5` at 2.2px, all scoped to `.deck-app`.
- **Duplication ×2** — card 01 repeated the section lead verbatim; and the in-house expert
  section rendered **twice** (my opening + `STRENGTH THREE` in the `SECTIONS` loop). Now
  `SECTIONS.filter((s) => s.fig !== "expert")`, guarded by a self-check.
- **Icons** — six inline SVG marks, one per capability, drawn not imported.

## 9.3 Still outstanding — the follow-up pack

`Apply-SOU-Application-Tab-Followup.ps1` · SHA256
`040912E3745526A4BF1EE679532A616FCFC06BD671BD3B92BDE7648A38899C3C` — **never run.**

- **World map legibility** — land was `rgba(16,42,67,.55)` on near-black with a `.22` stroke;
  continents and coastlines both dissolved. Now `#16324F` land, `rgba(140,214,235,.70)`
  coastline at 1.1px, brighter pins.
- **Refreshes the four screenshots** from `docs\images` (Karim re-cropped them), comparing
  SHA256 and reporting which changed.
- **Builds, then stages and commits exactly 10 paths, then pushes.**

Run it with `-NoCommit` first if you want to review the page.

---

# 10. THE ENGINE / MODEL FAMILIES — design authority vs sales language

## 10.1 What the design documentation actually says

`PPIQ_AI_ML_LLM_Target_Architecture_Optimisation.md` §10 (cross-confirmed in
`PPIQ_Layer_B_Architecture_Design_Pack.md` §17):

```
MF-01  Process encoder            Learned model        governed refresh, frozen between
MF-02  Similarity index           Retrieval and index  generational extension
MF-03  Normal and novelty         Learned model        weekly refit
MF-04  Supervised outcome         Learned model        weekly retrain + recalibration
MF-05  Effect and envelope        Statistical engine   weekly recompute, NO training
MF-06  Statistical intelligence   Statistical engine   weekly recompute, NO training
MF-07  Practice learning          Practice engine      governed signature version

plus orchestration and governance: capability profiler, model-count governor, supervisor
```

The document states in bold: **"Do not describe MF-01 to MF-07 as seven ML models. Three of the
seven are not models."** Correct collective term: **seven intelligence and engine families**.

MF-01 is explicitly **optional** with a promotion rule expressed as an inequality. How many
families run is set by the **capability profiler** and **model-count governor** — what the
deployment can support — **not** by a purchase tier.

## 10.2 Karim's correction on framing (important)

> "انا مش هقدر اقول لناس من مبيعات و مديرين … الكلام ده كلام فني … make it sexy and shiney
> and marketing for sales and CEO"

He is right, and §9 supports it: a capability statement must trace to the **master design
documentation** — not every technical detail must appear on the page.

## 10.3 The agreed sales mapping (implemented on the website)

| on the page | behind it |
|---|---|
| Learns the fingerprint of your plant | MF-01 · Master |
| Connects cause to result across the whole plant | MF-02 + MF-06 · Correlation |
| Knows when the plant has left its own normal | MF-03 |
| Sees the problem coming before it happens | MF-04 · Prediction |
| Tells you which change matters, and what it is worth | MF-05 · Return of value |
| Turns your best days into the standard | MF-07 · Learned practice |

**No `MF-` codes, no sub-types, no refresh policies, and no model count on a sales page.** The
pack's self-check aborts if any appear. Karim's earlier sketch (6 groups, "18 models") summed
to 20 and does not match the authority; **his instinct to group was right, the grouping already
exists in the design**.

**Open:** Karim is certain a *Suggestion and recommendation* family exists. It is **not** in the
seven. Either it lives inside MF-07, or it is in a document not yet shared, or it is planned.
If planned, it must be written as *planned*, not *supported*.

---

# 11. COMMERCIAL COLLATERAL DELIVERED

| artifact | state |
|---|---|
| `SOU_Industrial_Software_Company_Profile_v1_1_FINAL.docx` | 20 pages, validation PASSED |
| `SOU_Industrial_Software_Executive_Profile_v1_0.docx` | 9 pages |
| `SOU_Industrial_Software_Sales_Deck.pptx` | 9 slides |
| `SOU_PlantProcess_IQ_Presentation.pptx` | 19 slides, all four Presentation tabs in full |

## 11.1 Canonical facts — all four surfaces agree

```
SOU Industrial Software · Karim Gamal · Founder and Chief Product Architect
souindustrial.com · info@souindustrial.com
14 years · 5 products · 8 industrial companies · 13 plants / 8 countries
Düsseldorf, Germany · Alexandria, Egypt
PlantProcess IQ = flagship, NOT a parent of the other four
"Industries addressed" — never "markets served" (no customer-reference claim)
No price anywhere. Quotation after the technical review.
```

## 11.2 Document-tooling notes

- Long profile edited by unzipping the docx and patching `word/document.xml` directly; validate
  with `/mnt/skills/public/docx/scripts/office/validate.py` (note the `office/` subdir).
- Removing table rows: locate the `<w:tbl>` containing the anchor, split `<w:tr …>…</w:tr>`,
  drop by label. Paragraph count 555 → 551 confirmed the two removals.
- Decks built with `pptxgenjs`. **Always render to JPEG and LOOK at every slide** — three table
  widths overflowed the 9360 DXA text column on first render, and Enterprise's tenth tier row
  printed outside its card.
- Cards holding variable row counts must derive row pitch from the available envelope, not use
  a fixed height.

---

# 12. DEPLOYMENT, SERVER AND PIPELINE — HONEST STATUS

**I did no deployment, server or CI/CD work this session, and I have no test results for any of
it.** Do not let the length of this document imply otherwise. What is known:

```
Website hosting        Cloudflare Pages, project souindustrial-website
Monorepo root          Website/PlantProcess.Website  (React + Vite + TypeScript, Node 22)
Domain                 souindustrial.com (Cloudflare Registrar)
Email                  info@souindustrial.com — Cloudflare Email Routing rule configured,
                       DNS "Not configured", status "Syncing"
                       *** NO external send/receive test has ever passed ***
Hetzner VPS 178.105.152.180   COMPROMISED — full rebuild from scratch required
Credentials formerly on it (Jenkins store, GitHub PATs, deploy keys, DB passwords)
                       must all be treated as compromised and rotated
Jenkins GitHub PATs / deploy keys   must be revoked
Backend API (local)    http://localhost:5063
Web (local)            http://localhost:5173 (dev) / 4173 (preview)
Presentation DB        ppiq_presentation  (canonical live DB — NOT ppiq_app)
```

**The only "pipeline" work I did was the website build/validator/E2E gates**, documented in §8.5.
`npm run build` is green; the 12 acceptance failures are pre-existing and are a content
decision, not a pipeline defect.

**Blocking go-live:** `info@souindustrial.com` is written into the Company Profile, the
Executive Profile, both decks and the website. **Nothing is operationally released until one
real external send/receive test reaches the inbox.**

---

# 13. EVERY TEST RUN THIS SESSION — do not re-run these

| test | result | when |
|---|---|---|
| `t049-layout-persistence.spec.ts` | **4 passed (43.8s)** | final |
| `dashboardLayoutSerialisation.test.ts` | 6/6 | prior session |
| `WidgetStatePanel.test.tsx` + `hasEffectiveFilter.test.ts` | 10/10 | T-051 |
| `t051-widget-isolation.spec.ts` | 4/4 | T-051 |
| `drilldownRowIdentity.test.ts` | 8/8 | T-050 step 1 |
| `drilldownEvidence.test.ts` | 7/7 | T-050 step 2a |
| `drilldownExecutionSnapshot.test.ts` | 7/7 | T-050 step 2b |
| `npx vitest run src/state` | **48/48** | T-050 final |
| `npx vitest run src/components/dashboard` | **39/39** | T-050 final |
| `t050-drilldown-drawer-presentation.spec.ts` | 4 passed + setup | presentation half |
| `t050-drilldown-provenance.spec.ts` | green (Karim-reported) | provenance half |
| `genericParameterSelection.test.ts` | 5/5 | T-052 |
| `tsc -b` / `tsc --noEmit -p tsconfig.app.json` | GREEN throughout | all |
| Vite production build (HMI) | GREEN | T-051, T-050 |
| Website `npm run build` | exit 0 | corporate alignment + app tab |
| Website `validate:commercial:v2` | 9 pre-existing failures, **0 introduced** | corporate alignment |
| Website `test:commercial:e2e` | 3 pre-existing failures, **0 introduced** | corporate alignment |
| PowerShell pack self-tests (rollback, sweep, delta, encoding, parser) | all PASS | tooling |

**Not run:** the consolidated `m1p3-consolidated-acceptance.spec.ts` (written, never executed —
it needs port 5063 free of Worker 1 / Worker 3), and the Application-tab follow-up pack.

---

# 14. BACKLOG — Worker 2 queue

From `PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx`, sheets
`Backlog` (`Task Id` — note the lowercase `d`) and `AI Agent Task Map` (`Agent`, `Agent Queue #`).

```
q1  T-049  CLOSED
q2  T-050  CLOSED
q3  T-051  CLOSED
q4  T-052  CLOSED
q5  T-053  NEXT
q6  T-059
q7  T-068 → q8 T-065 → q9 T-066
P5  T-077 … T-083
```

**Worker 2 lock:** Dashboard / workspace / shared shell · AnalysisToolbox · Presentation E2E ·
the website. **Never** enter Worker-1 verticals (Connections, Dataset, Import, Relationships,
Mapping Health, Genealogy) or Worker-3 (Jobs, Findings, Assistant, ML, T-064).

**Deferred gates:** `T049-VISUAL-01` and the consolidated M1-P3 browser gate;
`PRODUCTION_OVERVIEW` layout repair **before** T-078 / T-080.

---

# 15. CONCURRENT WORK — read before ANY git command

Worker 1 and Worker 3 have **uncommitted backend work in the same tree**, confirmed repeatedly:
`DependencyInjection.cs`, `JobDefinition.cs`, `JobRunHistory.cs`, two EF configurations under
`Infrastructure/Persistence/Configurations/Integration/`, plus a SQL script.

```
Exact-file staging ONLY.
NEVER: git add .   git add -A   git clean -fd   git reset --hard   git restore .
Always: git status --short   before staging, and git diff --cached --stat before committing.
tools/packs/ is gitignored — packs are never committed. That is correct.
```

---

# 16. OPEN FINDINGS REGISTER

| id | finding | owner |
|---|---|---|
| `W2-GRID-DEFAULTS-01` | nine hardcoded `defaultLayouts` keys merged into every persisted layout (19 entries vs 10 widgets) | Rule 1, structural |
| — | `PRODUCTION_OVERVIEW` lg layout damaged (all widgets 1 row tall) — persisted data | before T-078/T-080 |
| `W2-GENERIC-02` | `defectType \|\| "SurfaceCrack"`, `linkMode \|\| "DownstreamChildren"` | endpoint semantics owner |
| `W2-GENERIC-03` | `CastingSpeedByGradeWidget` under `components/analytics` | demo-boundary decision |
| — | 12 pre-existing website acceptance failures (validator ↔ E2E contradiction) | Karim's ruling |
| — | `info@souindustrial.com` unverified externally | **blocks go-live** |
| — | screenshots show `SMS Digital Jira` / `SMS Group Jira` bookmarks bar + Windows taskbar | crop before public; relevant to the open `Nebentätigkeit` legal question |
| — | Suggestion/recommendation model family not present in the seven | needs the document, or write it as *planned* |
| — | two junk files created in repo root by my multi-line `git commit -m` | remove by exact name only |

---

# 17. RESUME CHECKLIST FOR THE NEXT SESSION

```
1. Run Apply-SOU-Application-Tab-Followup.ps1   (map, screenshots, build, commit, push)
      SHA256 040912E3745526A4BF1EE679532A616FCFC06BD671BD3B92BDE7648A38899C3C
      Move-Item from Downloads → tools\packs\ → Unblock-File → parser gate → hash THROW → run
2. Report the commit hash.
3. Start T-053 from the backlog (verify its row in the xlsx first — Task Id column).
4. Do NOT re-run any test in §13.
5. Do NOT re-derive anything in §4–§10.
```

**If a pack aborts:** the abort is usually correct. Read the message, fix the *named* thing,
bump the filename version, re-hash. Do not weaken the guard to get past it — unless the guard
is asserting something outside the pack's ownership, which is the mistake I made five times.
