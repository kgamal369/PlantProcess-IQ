# PPIQ — WORKER 1 — FULL SESSION HANDOVER
### 19–20 August 2026 · T-065 closure + DEMO-BI-R1 emergency corrective

**Read this before touching anything.** Every fact below was measured in this
session against the real repository, the real API and `ppiq_presentation`. Where
something is unknown it says UNKNOWN. Where I got something wrong, the wrong
answer is recorded next to the right one, because the wrong answers are what
cost the runs.

---

## 0. START HERE — THE ONE OPEN THREAD

**Associative cross-filtering does not filter the charts.** Clicking a value in
the ASSOCIATIVE VIEW strip (e.g. `Coil`) registers — the strip re-enumerates
afterwards, proving the click reaches the backend — but the widgets do not
re-query. Karim reports this worked ~2 weeks ago and had tests.

**The next diagnostic, not yet run:** after clicking a value in the strip, look
at the GLOBAL FILTERS row above.

- If a filter chip (Material / Source / etc.) now carries a value → the
  selection reaches the filter state and the defect is that widgets do not
  re-fetch. Look at the widget query dependency array.
- If the chips stay empty → the selection stops inside the strip and never
  reaches `useDashboardFilters`. Look at `AssociativeContext` → filter bridge.

**I am a suspect.** My `AssociativePanel` pack changed two things:
1. `const [open, setOpen] = useState(true)` → `useState(false)`
2. `fields.map(...)` → `fields.filter((fa) => fa.available || fa.loading).map(...)`

Change 2 is the dangerous one: it removes fields from the rendered list. If any
selection wiring iterates the rendered fields rather than all fields, I broke it.

There is also a **latent state contradiction I did not introduce but did
expose**: the collapse button also calls `setEnabled(next)`:

```tsx
onClick={() => setOpen((o) => { const next = !o; setEnabled(next); return next; })}
```

With `open` now starting `false` and `enabled` starting `true`, the two are
inconsistent at mount, and collapsing the panel switches the whole mechanism off.
Karim tested with `live: on` AND `live: off` and neither filtered, so this is
probably **not** the whole story — but the collapse button should still only
collapse. The `live` button next to it already owns `enabled`.

**Fastest safety check:**
```powershell
cd C:\Workspace\PlantProcess-IQ
git restore --staged --worktree Frontend/PlantProcess.Web/src/components/dashboard/AssociativePanel.tsx
cd Frontend\PlantProcess.Web ; npm run build
```
If filtering returns, I caused it. If not, the fault is elsewhere and at least
that is now known.

**DO NOT COMMIT until this is resolved.** 23 files are staged and building green,
but committing a tree that may contain a regression to a working feature is
worse than waiting.

---

## 1. GROUND TRUTH — REPOSITORY AND ENVIRONMENT

```
Repo            C:\Workspace\PlantProcess-IQ      branch main
Solution        Backend\PlantProcessIQ.sln         (NOT PlantProcess.sln — cost one run)
Frontend        Frontend\PlantProcess.Web          Vite 8.0.12, React, TS
Database        ppiq_presentation                  localhost:5432
Credentials     ppiq_dev / ppiq_dev_local_only     (local dev, deliberately committed)
API             http://localhost:5063
Frontend dev    http://localhost:5173
Start API       .\scripts\run\start-api.ps1 -Profile presentation
```

**Last commit before this session's uncommitted work:**
`1d27d1997801ffa18cacfafcfef115302c0cf748` — the T-065 backend producer.

**Foreign uncommitted bytes — NEVER stage these:**
```
 M .gitignore                                            (adds node_modules/ — correct, not ours)
?? Frontend/PlantProcess.Web/e2e/m1p3-consolidated-acceptance.spec.ts
?? tools/.ppiq-restore/                                  (my pack backups — the only rollback path)
?? tools/packs/                                          (my packs)
```

---

## 2. WHAT WAS DELIVERED — T-065 (CLOSED)

**Producer commit `1d27d1997801ffa18cacfafcfef115302c0cf748`** — 12 files,
2175 insertions, 43 deletions.

```
Backend build        green, 31 warnings, delta 0
Application unit     731 / 731, 0 skipped
Architecture         181 / 181, 0 skipped
Pack A integration     9 / 9,  0 skipped
Pack B integration    21 / 21, 0 skipped
```

### T-065 measured facts — do not re-investigate

- `/api/analysis-jobs` writes `public.inspection_jobs`; T-064 target columns live
  on `job_definitions` + `job_run_histories` (scripts 824/825/826). **No shared
  column, no FK, no link.** Ruling: `inspection_jobs` gets M1 compatibility
  columns, zero FK. T-106 owns physical convergence.
- Persisted policy vocabulary is `current_published` / `pinned` (lowercase).
  C# enum members stay `CurrentPublished` / `Pinned`.
- Live catalogue `job_type` values: `MlParamsVsDefects`, `MlParamsVsDowntime`,
  `MlParamsVsKpis`, `MlWeeklyFull`. `AnalysisJobClass.FromCatalogJobType`
  matches them exactly.
- `DefinitionService` supports **`DefinitionKind.Widget` ONLY**. Every other kind
  returns `OnlyWidget(...)`, a validation failure, from `ListVersionsAsync`.
  This is why the resolution tests had to be written against Widget.
- `public.ppiq_definition_versions` was **absent from ppiq_presentation**. Script
  `770_t039_definition_version_store.sql` creates it; `DefinitionVersionConfiguration`
  marks it `ExcludeFromMigrations()`, so only a numbered replay creates it.
  **I replayed 770 into ppiq_presentation during this session.** It is still
  absent anywhere else 770 has not been replayed (`ppiq_app`, the server).
  **This is an open finding larger than T-065**: without that table,
  `IDefinitionService.ListVersionsAsync/GetCurrentAsync/PublishAsync` all throw
  42P01, so T-039 versioning and the whole T-064 resolver were non-functional in
  that database.
- Licensing: `ppiq_presentation` holds an **Enterprise, active** entitlement for
  tenant `00000000-0000-0000-0000-000000000001`, expiring 2027-06-16.
  `InvestigationWorkflow` requires ProPlus. Without an activated licence the
  service falls back to Light and every `/api/analysis-jobs` request is refused.
- Integration harness env contract (verified against `AuthenticatedApiTestBase`):
  ```
  PPIQ_FORCE_EXTERNAL_API_TEST_HOST = 1
  PPIQ_TEST_API_PORT                = 15063     (its own default)
  PPIQ_TEST_CONNECTION_STRING       = <presentation>
  ConnectionStrings__PlantProcessDb = <presentation>
  ```
  The harness starts its own API. The dev API on 5063 is NOT used as a gate.

### T-065 findings raised, not fixed

- Pack A's new files carry a **UTF-8 BOM** while every pre-existing Application
  file has none. Violates the standing UTF8-no-BOM pack rule.
- Script 828 has no equivalent of 826's
  `CHECK (target_parameters IS NULL OR target_definition_id IS NOT NULL)`, so the
  compatibility store permits parameters beside no target identity. Pack B
  refuses that shape at the API boundary instead.
- `AnalysisJobRunResponse` does not exist — the run response is an anonymous
  object. Executed-identity fields were added to it.
- Ruling on executed identity: **option (c)** — response only. No migration 829,
  no `job_run_histories` row, no columns on `inspection_jobs`.

---

## 3. WHAT WAS DELIVERED — DEMO-BI-R1

Sole owner: Worker 1. Workers 2 and 3 frozen throughout.

### 3.1 Layout integrity — CLOSED

**Root cause, measured.** `dashboard_definitions.layout_json` for
`PRODUCTION_OVERVIEW` held **19 items**: ten real widgets
(`21000000-0000-0000-0000-0000000001{01..10}`) plus **nine ghosts** —
`defectTrend`, `defectBreakdown`, `riskDistribution`, `sourceContribution`,
`riskScatter`, `qualityHeatmap`, `topContributors`, `dataQuality`,
`materialExplorer`. Those nine are the hardcoded `defaultLayouts` ids in
`DashboardGridLayoutContext.implementation.tsx`. `enforceConstraints` merged them
into every page, `serializeLayouts` wrote them back, a Save persisted them.

In `lg`, every real widget was `w:1 h:1`. At `rowHeight: 42` that is the
title-only pill Karim photographed.

**Control that proved the renderer was fine:** `RISK_DASHBOARD` — two real
widgets, zero ghosts, `w:6 h:9` — rendered correctly through the same grid and
the same CSS.

**Verified with `grep`:** the nine ids appear nowhere in the application except
`defaultLayouts` itself and a legacy `DashboardWidgetId` union in
`DashboardSelectionContext.tsx`. They render no widget on any dashboard.

**Fixes shipped:**
- `enforceConstraints` normalises exactly the ids it is given; a matching default
  is a geometry source only. Ids that exist solely as defaults are dropped on
  read (`FOREIGN_DEFAULT_IDS`, derived from `defaultLayouts` so they cannot drift).
- `normalizeLayoutItem` applies a certified floor to what a row **states**, not
  only to what it omits — the pills carried explicit `w:1 h:1 minW:1 minH:1`,
  which a `??` fallback lets straight through.
- A breakpoint containing any degenerate item is **re-flowed whole**, because its
  coordinates are untrustworthy too. Resizing in place produced 432 overlapping
  cells.
- **Healthy layouts are never rewritten** (Karim's explicit ruling). Risk
  Dashboard's authored `minW:4 minH:5` and coordinates survive byte-identical.
- `loadLayouts()` returns empty instead of seeding `defaultLayouts`.
- `resetGridLayout` re-flows this dashboard at certified geometry.
- `DashboardGridLayout` completes the layout at render time from the widgets
  actually rendered (`completeLayoutsForWidgets`). Nothing is persisted.

**SQL repair applied** — `repair_production_overview_layout.sql`,
SHA256 `A958F4B4E35758996800B961BB2D7236591E4FE028CF87E26E78A8D6FD5688B5`.
Result: `lg_items=10 lg_min_w=6 lg_min_h=9 lg_ghosts=0`.
Backup: `tools\.ppiq-restore\PRODUCTION_OVERVIEW.layout.before.json` (9372 bytes).

### 3.2 Layout audit — 41 dashboards, ALL CLEAN

Ran a full audit across every dashboard. **Zero ghosts, zero degenerate, zero
off-canvas.** `CORRELATION_FINDINGS_BOARD` — which was expected to fail — is
`2 widgets / 2 items / min 6×9`. The pill Karim saw there was from before the
source fix; the code now cleans on read.

**But the audit found two new things:**

```
QUALITY_MONITORING        7 widgets → 4 layout items
EQUIPMENT_OPERATIONS      6 → 4
PARAMETER_DEEP_ANALYSIS   6 → 5
RISK_INTELLIGENCE         5 → 4
29 authored PAGE_*        no lg breakpoint at all
```

Both are closed by the render-time completion above.

### 3.3 Backend 500 on every workspace — CLOSED

**Root cause from the API log, verbatim:**
```
System.InvalidOperationException: The LINQ expression
'DbSet<ParameterObservation>() ... .GroupBy(ti1 => new DashboardGroupKey{
Text = new WidgetFact{ MaterialUnitId = ..., ParameterCode = ...,
EventTimeUtc = ..., Value = 1 }.RiskClass })' could not be translated.
```

`WidgetFact` uses init-only members so EF can fold
`new WidgetFact { EquipmentId = col }.EquipmentId` down to a column. **A member
the initialiser never assigns has no column to fold to.** The
parameter-observation source carries no `RiskClass`, `ShiftCode` or `DefectType`,
and the associative strip enumerates **every** dimension against
`observationCount` — so those three threw a 500 on the opening state of all 41
workspaces. They are also exactly the three columns the strip renders as N/A.

**No widget definition uses `riskClass`** — verified across 71 widgets and 37
dimension×measure pairings. No widget config was changed.

**Fix:** `DashboardSourceCapability` + `WidgetFactInitialiserFinder` walk the
query expression, read which members the projection actually assigns, and refuse
an uncarried dimension **by name** before the query is built, through the
existing governed-refusal path (now 400/422, not 500). A source shape the walker
cannot read (positional constructor) reports `null` and stands the guard down —
refusing a working widget would be worse than the defect.

### 3.4 Aggregate truth — PARTIALLY CLOSED

**The forbidden shape, found in the source:**
```csharp
.Take(resolved.RawRowLimit + 1)
.ToListAsync()                      // materialise raw rows
RequireCompletePopulation(...)      // refuse if over the limit
AggregateCount(facts, resolved)     // aggregate IN MEMORY
```
A raw-row **presentation** limit was deciding the mathematical **population**.

**Converted to PostgreSQL aggregation over the full authorised population:**
- `defectCount`
- `avgParameterValue` / `maxParameterValue` / `minParameterValue`
- `defectRate`

The executor gained `Average`, `Maximum`, `Minimum` families. None folds across
grains (the mean of two day means is not the mean of the readings unless both
days carry equal counts), so the existing non-mergeable refusal guards them.

`defectRate` is expressed as **the mean of a per-material indicator** — a
material with ≥1 quality event scores 100, one with none scores 0, and the mean
over a group IS the percentage defective. Same arithmetic, different place. Each
material appears once so the row count is the same denominator as before.

**STILL ON THE LEGACY RAW-CAP PATH — 4 measures remain:**
`materialCount`, `downtimeMinutes`, `riskScore`, `processStepDuration`,
`dataQualityIssueCount` (count the `RawRowLimit + 1` occurrences to confirm; it
was 4 at last measurement).

**No safety limit was raised, disabled or bypassed anywhere.**

### 3.5 Presentation fixes — CLOSED

- **Associative view opens collapsed** (`useState(false)`) — see §0, this is
  under suspicion.
- **Zero-information dimensions hidden** from the opening state — also under
  suspicion.
- **Presentation mode**: Save layout and Reset layout appear only in explicit
  Edit mode. Refresh stays, because reading again is not authoring. Two existing
  header tests were **updated to `isEditing: true`, not deleted**.
- **Chart selection frame**: Recharts' default `<Tooltip />` cursor on a
  categorical chart is a light filled rectangle spanning the whole category band
  — the white frame around the Slab bar. One shared `chartCursor.ts` constant now
  used by `InteractiveCharts` (5 tooltips), `LiveWidgetChart` (3),
  `ChartExtras` (2). The cursor is **restyled, not removed** — losing the hover
  cue would make dense charts harder to read. Keyboard focus untouched.
- **KPI tile**: `DashboardWidgetCard` chose the active type as
  `state.chartType ?? firstAvailableOption ?? firstOption`. The type the widget
  was **saved as** was not in that chain and the card was never given it, so a
  `kpi` widget drew whatever was first in the switcher. Added `savedChartType`
  prop; precedence is now reader's choice → saved type → first available.
  `MetricCard` gained a `kpi` variant showing only the figure (the card header
  above already carries the title and the `kpi · dim · measure` line).
- **Raw identifiers**: `displayValue()` shortens a 36-char UUID to `7922750e…`
  with the full value in the tooltip. **No name is invented.**

**CRITICAL BEHAVIOURAL NOTE:** the KPI fix was on disk and correct but the
browser still drew charts. The cause was **stale `localStorage` widget state**.
This cleared it:
```js
Object.keys(localStorage).filter(k=>k.includes('dashboard')||k.includes('widget'))
  .forEach(k=>localStorage.removeItem(k)); location.reload();
```
After that: `Material Units 35,915`, `Process Observations 301,560`,
`Quality Events 7,844`. **A persisted per-widget chart type overrides everything.
Remember this before diagnosing any "the fix didn't work" frontend report.**

---

## 4. MEASURED DATA — DO NOT RE-QUERY

### Equipment (18 of 18 named — the UUIDs are a label-join defect, not missing data)
```
1933641c-3014-5aaa-ac52-4b47077372de | CCM-01    | Continuous caster 1
c42c2508-f589-5b1e-8fed-39dca5583964 | CCM-02    | Continuous caster 2
7957ad3b-65c6-55d7-8327-bf67c6f0480a | EAF-01    | Electric arc furnace 1
8a519704-79b0-54cc-9ae5-66a7eb9aa2bd | EAF-02    | Electric arc furnace 2
7922750e-2768-5083-9cc3-cc0ab890b32b | HSM-01    | Hot strip mill
e22f6c16-307b-5524-851e-ff557c695ea9 | HSM-01-F1 | Finishing stand F1
... F2..F7
```

### Schema facts that cost wrong guesses
```
dashboard_definitions          key column is dashboard_code, NOT page_code
dashboard_widget_definitions   title column is widget_title, NOT title
layout_json                    jsonb — length() needs ::text
ppiq_definition_versions       columns: definition_kind, definition_id,
                               version_number, payload_json, created_by,
                               is_published. No tenant column.
```

### Widget inventory
71 active widgets, 37 distinct dimension×measure pairings, across 41 dashboards
(12 system + 29 authored `PAGE_*`).

### Observed request payload shape (from DevTools, not invented)
```json
{ "widgetType":"chart", "chartType":"bar", "dimensionCode":"equipment",
  "measureCode":"avgParameterValue", "parameterCode":"FDT_C", "filters":{},
  "options":{"maxRows":20,"rawRowLimit":1000,"sortDirection":"desc",
             "includeWarnings":true} }
```
KPI widgets send `dimensionCode: ""`, `maxRows: 50`, `rawRowLimit: 1000`.

---

## 5. EVERY TEST RUN AND ITS RESULT — DO NOT RE-RUN

```
T-065 Pack B v6
  Backend build            green, 31 warnings, delta 0
  Application unit         731/731,  0 skipped
  Architecture             181/181,  0 skipped
  Pack A integration         9/9,    0 skipped
  Pack B integration        21/21,   0 skipped

DEMO-BI-R1 layout pack v2
  tsc -b                   exit 0
  focused layout tests     18/18

DEMO-BI-R1 shell pack v1
  tsc -b                   exit 0
  focused shell tests      35/35

DEMO-BI-R1 backend 500 pack v3
  Backend build            Build succeeded
  Application unit         745/745
  Architecture             181/181

DEMO-BI-R1 aggregate truth v2
  Backend build            Build succeeded
  Application unit         761/761
  Architecture             181/181

Final state (after CSS newline fix)
  npm run build            built in 1.89s
  dotnet build             Build succeeded
```

**Layout audit** (read-only, 41 dashboards): 0 ghosts, 0 degenerate,
0 off-canvas. Do not re-run unless layouts are edited.

---

## 6. EVERY PACK SHIPPED — HASHES AND BACKUPS

```
repair_production_overview_layout.sql        A958F4B4…  applied
Apply-T065-TargetBridge-B-v6.ps1             3A8D8E2B…  applied, committed
Apply-DEMO-BI-R1-Layout-v2.ps1               53AB2733…  applied
Apply-DEMO-BI-R1-Shell-v1.ps1                94A12072…  applied
Apply-DEMO-BI-R1-ChartCursor-v1.ps1          68DDF9F8…  applied
Apply-DEMO-BI-R1-WidgetPlacement-v1.ps1      3CCFEB4D…  applied
Apply-DEMO-BI-R1-Backend500-v3.ps1           36F9F614…  applied
Apply-DEMO-BI-R1-AggregateTruth-v2.ps1       F344D2AF…  applied
Apply-DEMO-BI-R1-AssocFieldMeasure-v2.ps1    0E53BE55…  applied
Apply-DEMO-BI-R1-CeoThree-v2.ps1             162D6672…  applied
Apply-DEMO-BI-R1-FinalThree-v2.ps1           32C75545…  applied
Run-WidgetCensus.ps1                         A3D90389…  NEVER RAN (login failed)
```

Every pack has `-Revert`. Backups in `tools\.ppiq-restore\<PREFIX>-<timestamp>\`.

**`Run-WidgetCensus.ps1` never ran** because none of the guessed credentials
worked and the seeded users are in the database, not `appsettings`. If the census
is wanted, get a real username first:
```sql
SELECT user_name FROM public.app_users WHERE is_active = true;
```
or lift the bearer token from the browser and add it as a parameter.

---

## 7. HARD-WON LESSONS — READ BEFORE WRITING A PACK

### Pack authoring
1. **Whole-file base64 payloads gated on an exact pre-state SHA256 beat text
   anchors.** It eliminates the entire CRLF / anchor-drift failure class.
2. **When a file is edited by more than one hand, hash-gating fails forever.**
   Use anchored edits that assert the DEFECT is present, edit, then assert it is
   gone. `AssociativeContext.tsx` and `associativeFields.ts` are both like this.
3. **Assert every gate-target path exists in preflight**, not after a four-minute
   baseline build. One wrong solution path (`PlantProcess.sln` vs
   `Backend\PlantProcessIQ.sln`) cost a full run.
4. **Never wrap `dotnet`/`npm` in a PowerShell function that returns a value.**
   The function returns its whole output stream, so the returned path becomes an
   array of console lines. This cost a run where the TRX existed and was never
   read.
5. **`--logger 'trx;LogFileName=…'` resolves relative to the results directory.**
   Pass `--results-directory` plus a bare filename. An absolute path lands the
   TRX somewhere the gate never looks, and baseline-vs-post then compares absent
   to absent and reports zero introduced failures — a false green.
6. **Print compiler errors before rolling back.** A rollback that hides the
   reason costs an extra round trip every time.
7. **Simulate every guard against the exact embedded payload before shipping.**
   This caught four real defects in my own work before they cost a run.
8. Files may need `BOM + CRLF + no trailing newline` to reproduce their declared
   SHA. Brute-force the variants; do not assume.
9. **Section 0 self-test**: prove the hash gate sees a one-byte change, prove the
   comment stripper removes prose and keeps code, prove the exit-code gate
   reports both answers.

### The one rule that would have prevented most of it
> Any measurement that returns zero must first prove it can return non-zero.
> Any gate must prove it can pass. Any verdict must prove it read something.

### C# / EF
- `WidgetFact` **must** be projected with a **member initialiser**, never a
  positional constructor, when it feeds `DashboardAggregateExecutor`. EF folds
  `new WidgetFact { Value = 1m }.Value` to a column; it cannot fold
  `new WidgetFact(a,…,1m).Value`. This cost one run.
- `DashboardWidgetResolvedDto.MeasureCode` is **non-nullable**; `DimensionCode`
  is nullable. `resolved.MeasureCode ?? "x"` raises a warning that this build
  treats as an error. This cost one run.
- The Application assembly has no `InternalsVisibleTo`. To test internal logic,
  expose a small **public** surface (`DashboardSourceCapability`) that the
  internal class delegates to — never duplicate the mapping.

### Frontend
- **`localStorage` widget state overrides everything.** Clear it before
  concluding a frontend fix failed.
- Vite caches aggressively. `Get-Process node | Stop-Process`, delete
  `node_modules\.vite`, restart, then **Ctrl+Shift+R** (not F5) with
  DevTools "Disable cache" ticked.
- Use `node node_modules/typescript/bin/tsc -b`. Never `npx` inside a pack.
  Never `--noEmit` (solution-style tsconfig checks zero files).
- `MetricCard.tsx` lives at `src/components/`, **not** `src/components/dashboard/`.
  Getting this wrong cost a preflight refusal.

### Console / operator ergonomics
- Attached files repeatedly arrived **empty**. Paste console output as text.
- `allow pasting` must be typed into Chrome's console before pasting a script.
- `git diff -U0 | Select-String '^[+-]'` gives the smallest reviewable diff.

---

## 8. RULES AND CONCEPTS KARIM SET — CARRY THESE FORWARD

1. **Never raise, disable or bypass a safety limit** to make a widget green.
   `aggregate.population_limit` and readiness thresholds are product truth.
2. **A refusal is a valid result.** Truthful exact result > explicit bounded
   refusal > partial value. Never present a lower bound as a total.
3. **Repair invalid/degenerate geometry; never rewrite healthy authored
   constraints.** Risk Dashboard's `minH:5` is a decision the operator made.
4. **Name your own defects before Karim finds them.** Standing rule.
5. **Exact-file staging only.** Never `git add .` or `-A`. No commit without an
   explicit instruction after staging.
6. **Do not ask the operator to paste source that is in the worktree or in a
   hash-verified export.** Extract it yourself.
7. **Do not ask the operator to inspect a page for a failure that can be
   reproduced programmatically.**
8. Ordinary compile errors and test failures are **not** STOP conditions. Fix and
   re-ship. Return to Karim only for destructive-data risk, a frozen-contract
   contradiction, or ownership ambiguity.
9. **Session length is not a product blocker.** Write a handover, open a fresh
   session, continue as the same sole owner. Never hand to another worker.
10. **Messages should be packs.** Karim's explicit instruction: stop spending
    tokens on prose; every message should carry a fix pack.
11. **Visual acceptance is Karim's eyes.** The assistant cannot open a browser.
    DOM assertions are never the oracle. Saying otherwise is the same false-green
    that took six revisions to remove from the T-065 gate.
12. Never invent equipment names, findings, or licences. Never forge a
    `verification_status='valid'` row to make a gate pass.

---

## 9. CURRENT STATE — THE SCOREBOARD

### Working, verified in the browser
```
Grid geometry, all 41 dashboards          full-size cards, no pills, no overlap
Associative view collapsed by default     business content in first viewport
Save / Reset hidden in presentation mode  Edit layout + Refresh remain
White selection frame                     gone
KPI tiles                                 35,915 / 301,560 / 7,844 as figures
Equipment & Operations                    Downtime by Equipment, Observation Throughput
Risk Dashboard                            Risk Score by Grade — six real bars
Correlation Findings Board                both tables
Parameter Deep Analysis                   Observations 17,010
Production Volume Trend, Material Mix     real series
Volume by Grade                           HSLA-420 124, DX51D 115, DP600 84,
                                          S355MC 69, S235JR 62, IF-LOW-C 46
```

### Open
```
P0  associative cross-filtering does not filter charts   see §0
P0  4 measures still on the legacy raw-cap path
P1  Throughput by Shift renders empty                    materialThroughputByShift
P1  raw UUIDs still shown for equipment in the strip     shortened, not resolved
P1  ppiq_definition_versions absent outside presentation
P2  Pack A files carry a UTF-8 BOM
P2  828 lacks 826's target_parameters CHECK
P2  the strip asks every dimension for observationCount; three are refused
```

### Recommended demo path (no red cards on any of these)
```
1. Equipment & Operations
2. Risk Dashboard
3. Correlation Findings Board
```
Avoid Command Dashboard and Correlation Explorer until the remaining measures
are converted.

---

## 10. THE STAGED COMMIT — READY BUT HELD

23 files staged, both builds green. **Held pending §0.**

```
Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardAggregateExecutor.cs
Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs
Backend/tests/PlantProcess.Application.UnitTests/Dashboarding/DashboardDimensionSourceCapabilityTests.cs
Frontend/PlantProcess.Web/src/components/MetricCard.tsx
Frontend/PlantProcess.Web/src/components/charts/InteractiveCharts.tsx
Frontend/PlantProcess.Web/src/components/charts/chartCursor.ts
Frontend/PlantProcess.Web/src/components/charts/__tests__/chartCursor.test.ts
Frontend/PlantProcess.Web/src/components/dashboard/AssociativePanel.tsx        ← suspect
Frontend/PlantProcess.Web/src/components/dashboard/ChartExtras.tsx
Frontend/PlantProcess.Web/src/components/dashboard/DashboardGridLayout.tsx
Frontend/PlantProcess.Web/src/components/dashboard/DashboardWidgetCard.tsx
Frontend/PlantProcess.Web/src/components/dashboard/LiveWidgetChart.tsx
Frontend/PlantProcess.Web/src/components/dashboard/SavedDashboardWidget.tsx
Frontend/PlantProcess.Web/src/components/dashboard/__tests__/associativePanelPresentation.test.ts
Frontend/PlantProcess.Web/src/pages/Dashboard/WorkspaceHeader.tsx
Frontend/PlantProcess.Web/src/pages/Dashboard/__tests__/workspaceHeader.test.tsx
Frontend/PlantProcess.Web/src/state/AssociativeContext.tsx                     ← suspect
Frontend/PlantProcess.Web/src/state/DashboardGridLayoutContext.implementation.tsx
Frontend/PlantProcess.Web/src/state/__tests__/dashboardLayoutPollution.test.ts
Frontend/PlantProcess.Web/src/state/__tests__/layoutCompletion.test.ts
Frontend/PlantProcess.Web/src/state/associativeFields.ts
Frontend/PlantProcess.Web/src/styles/components/dashboard-components.css
Documentation/docs/CompanyProfile/SOU_PlantProcess_IQ_Presentation.pptx        ← Karim's, deliberate
```

A full commit message was drafted and is reproduced at the end of this file.

---

## 11. DEPLOYMENT, SERVER, PIPELINE — NOTHING WAS DONE

**This session touched none of it. I have no test results, no measurements and
no findings for deployment, the server, the Jenkins pipeline, or the app URL.**
I will not invent any.

What is carried forward from earlier project state only (NOT verified here):

- Hetzner VPS `178.105.152.180` is **compromised and requires a full rebuild**.
  All credentials that lived on it — Jenkins store, GitHub PATs, deploy keys, DB
  passwords — must be treated as compromised and rotated.
- GitHub PATs and deploy keys associated with Jenkins must be revoked.
- Website is live at `souindustrial.com` via Cloudflare Pages, project
  `souindustrial-website`, monorepo root `Website/PlantProcess.Website`,
  React + Vite + TypeScript, Node 22.
- `info@souindustrial.com` via Cloudflare Email Routing — **not yet verified**
  with an external send/receive test. No surface is operationally released until
  that passes.
- Website commercial validator + E2E spec have **12 pre-existing failures**
  asserting a retired "Crime Scene / Trial & Verdict" narrative and a
  `/products/qes → /packs/quality` redirect that contradicts the five-product
  architecture. These are findings for Karim, not something to weaken.
- Application tab rebuild pack is prepared and validated but requires two
  screenshots: `public/shots/canvas-editor.png`, `public/shots/bi-workspace.png`.
- `.de` domain registration deferred pending legal consultation.

**Points 9 and 10 of the handover request cannot be answered honestly beyond
this.** If the next session needs pipeline work, it starts from the server
rebuild and credential rotation, and it starts cold.

---

## 12. THE COMMIT MESSAGE (use when §0 is resolved)

```
fix(dashboard): restore workspace presentation and aggregate truth

Layout integrity
- enforceConstraints no longer merges defaultLayouts as members; the nine
  legacy ids that belong to no dashboard are dropped on read instead of being
  serialised back into layout_json. PRODUCTION_OVERVIEW was measured holding
  19 items: ten real widgets and those nine ghosts.
- normalizeLayoutItem applies a certified card floor to what a row states, not
  only to what it omits, so an explicit 1x1 is repaired. A breakpoint holding
  degenerate geometry is re-flowed whole, because its coordinates are
  untrustworthy too.
- resetGridLayout re-flows this dashboard rather than assigning the global
  defaults, so Reset can no longer destroy a healthy page.
- DashboardGridLayout completes the layout at render time from the widgets
  actually present. Four workspaces held more widgets than their saved layout
  named, and 29 authored pages carried no lg breakpoint at all.

Aggregate truth
- defectCount, avgParameterValue, maxParameterValue, minParameterValue and
  defectRate no longer materialise RawRowLimit raw rows and aggregate in
  memory. They group and aggregate in PostgreSQL over the full authorised
  population, so MaxRows bounds the answer instead of the input.
- The executor gains Average, Maximum and Minimum families. None folds across
  grains; the existing non-mergeable refusal guards them.
- defectRate is expressed as the mean of a per-material indicator, which is
  the same arithmetic evaluated in a different place.
- No safety limit is raised, disabled or bypassed. Four measures remain on the
  legacy raw-cap path.

Governed refusals
- A dimension a fact source does not carry is refused by name before the query
  is built, instead of reaching PostgreSQL untranslatable and throwing 500.
  The associative strip enumerates every dimension, so riskClass, shiftCode
  and defectType each produced a 500 on the opening state of every workspace.
- That enumeration now suppresses the central toast. The error is still thrown,
  still caught and still degrades the field honestly; it stops being announced
  as a fault.

Presentation
- The associative view opens collapsed, and dimensions with no values are
  hidden from the opening state.
- Save and Reset appear only in explicit Edit mode. Refresh stays, because
  reading again is not authoring.
- A KPI tile renders as a figure. The card had no access to the chart type a
  widget was saved as, so a kpi widget drew whatever was first in the switcher.
- One shared chart cursor replaces the Recharts default, whose categorical
  cursor painted a white band across the plot on a dark surface.
- Raw 36-character identifiers are shortened for display with the full value in
  the tooltip. No name is invented.

Tests
- Layout pollution, layout completion, chart cursor, associative presentation,
  workspace header presentation mode, and dimension source capability.

Also includes the company presentation deck.
```

---

## 13. HONEST ASSESSMENT

The product code was correct from early on. Almost every failure in this session
was in something **measuring** it: a threshold Pack A could satisfy on Pack B's
behalf, a solution path typed instead of read, a TRX logger argument resolved
against the wrong directory, a PowerShell function swallowing native output, a
table assumed applied because its DDL was authored, a fixture comparing a
lowercase marker to an uppercased column, a positional constructor EF could not
fold, and a stale browser cache that made three correct fixes look broken.

Same root cause every time: **an instrument was trusted without first proving it
measured what it was believed to measure.**

The single most valuable thing the next session can do is finish §0 before
anything else, then convert the four remaining measures using the now-proven
pattern — member-initialiser projection plus the canonical PostgreSQL executor.
