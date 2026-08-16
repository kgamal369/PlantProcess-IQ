# PPIQ — Worker 2 Session Handover
**Session:** 13–16 August 2026 · **Lane:** Worker 2 (presentation) · **Author:** Claude

---

## 0. READ THIS FIRST — where to resume

**Current task: T-049. One thing is blocking it, and it is my test, not the product.**

State of T-049:

| Artefact | Status |
|---|---|
| `Frontend/PlantProcess.Web/src/state/__tests__/dashboardLayoutSerialisation.test.ts` | written, **6/6 green** |
| `Frontend/PlantProcess.Web/e2e/t049-layout-persistence.spec.ts` | written, **fails in my own `signIn` helper** |
| `tsc --noEmit` | green |
| architecture ratchets | 4/4 green |
| Playwright `license.setup` | green (was failing, now fixed — see §0.1) |
| Playwright T-049 specs | **3 failed** in `signIn`, product never exercised |

**Neither file is committed.** They are untracked/modified in the working tree.

### 0.1 The exact next action

The Playwright harness login is fixed by **environment variables only** — no file edit, no shared config change:

```powershell
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web
$env:PPIQ_SMOKE_USERNAME = 'e2eadmin'
$env:PPIQ_SMOKE_PASSWORD = 'E2EAdmin123!'
$env:PLAYWRIGHT_API_URL  = 'http://localhost:5063'
npx playwright test e2e/t049-layout-persistence.spec.ts --reporter=line
```

With those set, `license.setup.ts` passes. The three T-049 tests then fail inside **my own `signIn()`** function in the spec.

**Unresolved hypothesis (NOT verified — verify before acting):** `license.setup.ts` is a Playwright `[setup]` project. That pattern normally writes a `storageState` file which dependent projects consume, meaning **every spec starts already authenticated**. If true, my `signIn()` is not merely redundant but actively wrong: it navigates to `/login` on a live session where no login form exists, so `getByLabel(/user/i)` can only time out.

**The command that settles it** (was requested, output never received):

```powershell
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web
Select-String -Path playwright.config.ts -Pattern "projects|storageState|dependencies|baseURL" -Context 1,6
npx playwright test e2e/t049-layout-persistence.spec.ts --reporter=line 2>&1 | Select-Object -First 45
```

- **If `storageState` exists** → delete `signIn()` from the spec entirely and delete its call sites. The harness already authenticates. This makes the spec smaller and consistent with the other 80 specs.
- **If no `storageState`** → my `getByLabel(/user/i)` / `getByLabel(/pass/i)` selectors are wrong for the login form; read the form markup and fix the selectors.

**Do not conclude the product is broken.** Across this entire debugging sequence the product was never implicated once.

### 0.2 Prerequisites for running T-049

Both must be running, on the **presentation** profile:

```powershell
.\scripts\run\start-api.ps1 -Profile presentation   # API on 5063, DB ppiq_presentation
.\scripts\run\start-web.ps1 -Profile presentation   # Vite on 5173
```

**The profile is not optional.** Default profile points the API at `ppiq_app`, which has **no dashboard widgets**. The spec then finds fewer than three `.react-grid-item` elements and **SKIPS**. A skip is not a pass and must never be recorded as certification.

Acceptance is **`3 passed`**. Not `3 skipped`. Not a `license.setup` failure.

---

## 1. Commit ledger — everything closed this session

| Task | Commit(s) | What |
|---|---|---|
| **T-046** | `fc146483` | Chart switcher converged on backend truth |
| **T-047 Pack A** | `bd3dbb93` | Distribution slice: histogram + 2 semantic sources |
| **T-047 Pack B** | `00745822` | Box plot: parameterValueSpread + R-7 quartile kernel |
| **T-047 Pack C1** | `dc0b0e16` | Parameter Deep Analysis distribution/spread bindings |
| **T-047 Pack C2** | `124276dc` | Parameter relationship scatter, two-axis renderer |
| **T-047 Pack D** | `413df51a` | Multi-series for Production + Quality |
| **T-047 Pack E** | `a16b7b31` | Seven-page composition, seed-only |
| **T-047 final** | `4b431463` | Positional heatmap + specification limits + equipment pair bindings |
| **T-047 styling** | `3cdd3a23` | Heatmap CSS + QM_SPEC conformance formatting |
| **T-045-R1-A** | `c56008c0` | Readiness parity — measured value + thresholds |
| **T-045-R1-D** | `283aae2c` | Equipment stoppage/impact independent quantities |
| **T-045-R1-C** | `39ce59ef` | Risk provenance, contributors, temporal history |
| **T-045-R1-B** | `dd9a6b04` | Duplicate canonical engine DI registration removed |
| **T-044-R1** | `f966719f` | Positional facts + product specifications materialised |
| **T-046-R1** | `56ade951` | Heatmap + paired renderers |
| **T-048** | `b687cba4` | Alternative associative state + registry-driven fields |
| **T-044 corrective** | `55157a98` | *(Another worker/session)* EF migration parity |

**Task status:** T-044-R1 ✅ · T-045-R1 ✅ (all four) · T-046 ✅ · T-046-R1 ✅ · T-047 ✅ · T-048 ✅ · **T-049 IN PROGRESS** · T-050 next

---

## 2. THE MOST IMPORTANT THING I LEARNED — the `ppiq_app` vs `ppiq_presentation` trap

**This invalidated five "proofs" I had already reported as green.** It is the single highest-value lesson in this handover.

`ResolveIntegrationTestConnectionString()` in `AuthenticatedApiTestBase` falls back to **`ppiq_app`**. Every `psql` probe I ran used **`ppiq_presentation`**. So for weeks of session time I reasoned about one database and proved against another.

```
                          ppiq_app   ppiq_presentation
risk_scores                      0                 500
downtime_events                  1                 630
parameter_observations          40             301,560
```

Integration suites that **accept a truthful terminal state** (`NO_DOWNTIME_IN_SELECTION`, etc.) pass **vacuously** on an empty table. I reported *"totals match the source table"* four times when the source held one row.

### The permanent rule that came out of it

> A **product-correctness test** may accept a truthful Empty/Refused state.
> A **populated-execution certification** may NOT — it must independently prove the population its claim depends on, and a pass with zero required population is **VACUOUS**, not PASS.

### The fix pattern (reuse this)

Set `PPIQ_TEST_CONNECTION_STRING` for that gate only — the resolver checks it first, so the service under test **and** the test's raw SQL hit the same database. Never change the shared resolver default.

```powershell
# resolve from env/profiles/presentation.env, assert database name, then:
$env:PPIQ_TEST_CONNECTION_STRING = $connection
```

### M1 populated recertification — ALREADY DONE, DO NOT REPEAT

Run against `ppiq_presentation`, **17 tests, 17 passed, 0 vacuous**:

| Suite | Population | Verdict |
|---|---|---|
| Parameter + Risk distribution | 301,560 obs · 500 risk scores | PASS |
| Parameter spread (box plot) | 301,560 obs · 35,915 graded materials | PASS |
| Parameter relationship (scatter) | 160 overlapping pairs | PASS |
| Production + Quality multi-series | 3,780 crew steps · 7,844 quality events | PASS |
| Equipment stoppage + impact | 630 downtime events | PASS |

**Carry this matrix into every future closure record.** The gate script is `tools/packs/run-m1-populated-pg-recertification-v1.ps1`.

---

## 3. Live database facts — measured, do not re-query

`ppiq_presentation` on `localhost:5432`, user `ppiq_dev`, password `ppiq_dev_local_only`.

```
parameter_observations (numeric)   301,560
quality_events                       7,844
  └ SurfaceDefect (FLEET_V2)         5,961  ← all carry positional facts
risk_scores                            500  ← ALL IsSynthetic, ONE scoring day, 27-second span
downtime_events                        630
process_step_executions (crew)       3,780
material_units (graded)             35,915
overlapping parameter pairs            160  ← FDT_C × CT_C = 17,010 shared materials
product_specifications                  36  ← 6 grades × 6 parameters, 13 have min_value
parameter_definitions                   48
correlation_results                      0  ← truthful: zero supported findings
ml_correlation_compute_runs            474
ml_correlation_results_v2               26  ← HISTORICAL, not current findings
```

### Seven pages — dashboard IDs `20000000-0000-0000-0000-00000000000N`

| N | Code | Shapes after T-047 |
|---|---|---|
| 1 | `PRODUCTION_OVERVIEW` | area, bar, donut, kpi, line, stackedColumn, table |
| 2 | `QUALITY_MONITORING` | heatmap, kpi, line, pareto, stackedColumn, table |
| 3 | `EQUIPMENT_OPERATIONS` | bar, combo, line, pareto, table |
| 4 | `CORRELATION_FINDINGS_BOARD` | table *(thin — honestly, zero findings)* |
| 5 | `PARAMETER_DEEP_ANALYSIS` | boxPlot, histogram, kpi, line, scatter |
| 6 | `RISK_INTELLIGENCE` | bar, histogram, kpi, table |
| 7 | `MODEL_INSIGHTS` | donut, table *(thin — no coverage values exist)* |

Widget IDs follow `21000000-0000-0000-0000-000000000<page><seq>`.

**Legacy `10000000-*` dashboards** (QUALITY_OVERVIEW, RISK_DASHBOARD, DATA_QUALITY, CORRELATION_EXPLORER, MATERIAL_INVESTIGATION_LAUNCHER) are **outside** the seven-page scope. Do not inventory them.

---

## 4. Architecture I built on — the Class-2 native source seam

**This is the single most important architectural mechanism in this session.** It is how every rich chart shape was delivered without touching the generic query engine.

### How it works

`IWidgetResultSource` — a measure code routes to a native source that builds its **own columns and rows** via `NativeWidgetResult.Build(...)`, **bypassing the aggregate path entirely**.

**Critical:** native sources `return` at ~line 82 of `DashboardWidgetQueryService`, **before** the `renderedShape` / `DashboardChartGrammar.Evaluate` gate at ~line 198. So a native-shape widget is never refused by generic-binding compatibility rules.

### The law (Karim's, enforced)

```
Renderer      = visual capability
Native source = semantic analytical question
NEVER          source = chart type
```

No `HistogramSource`. No `BoxPlotSource`. No `ScatterSource`. Pack self-checks **fail** if such a class name appears.

### Registration checklist — a measure needs ALL of these

1. `DashboardMetadataCodes.Measures.<Name>` constant (`Contracts/DashboardMetadataDtos.cs`)
2. `SupportedMeasures` (safety registry)
3. `MeasuresProvidingOwnColumns` (safety registry)
4. `MeasuresRequiringParameter` — only if it needs one
5. `ExecutableMeasures` (`DashboardWidgetQueryService`)
6. Source class registration in the `_nativeSources` array
7. **`SupportedChartTypes`** if the widget uses a new chart code ← *missed this in Pack A, cost a run*
8. `DashboardChartGrammar` availability flip — **only alongside a real renderer**
9. **NOT** `BuildMeasures` in `DashboardMetadataService` — native measures are deliberately **absent** from the authoring catalogue

### Sources I created (all committed)

| Measure | Question | Roles published |
|---|---|---|
| `parameterValueDistribution` | How often does a parameter fall in each interval? | state, binLabel, binLower, binUpper, count |
| `riskScoreDistribution` | How are risk scores spread? | same |
| `parameterValueSpread` | How does spread differ between groups? | state, category, label, minimum, q1, median, q3, maximum, observationCount |
| `parameterRelationship` | How do two parameters relate per material? | state, materialUnitId, materialLabel, xValue, yValue, xParameterCode, yParameterCode |
| `materialThroughputByShift` | How does volume split between shifts over time? | state, category, categoryLabel, series, seriesLabel, value |
| `defectTypeMix` | How do defect types distribute across a grouping? | same |
| `equipmentStoppageAndImpact` | Stopped vs production impact per equipment | state, equipmentId, equipmentCode, equipmentLabel, stoppedMinutes, productionImpactMinutes **+ generic paired roles** |
| `riskScoringProvenance` | What is known about the scored population? | state, provenanceState, riskType, modelVersion, sourceSystem, populationCount, syntheticCount, syntheticFraction, sourceRecordCount, first/lastScoredAtUtc |
| `riskScoreContributions` | Which contributors were persisted? | state, materialUnitId, riskScore, riskClass, contributorCode/Name/Type, weight, direction, contribution, explanation |
| `riskScoreHistory` | How has risk moved over time? | state, period, periodStartUtc, scoredCount, average/minimum/maximumScore |
| `defectPositionDensity` | Where on the material do defects cluster? | state, x, y, value |
| `specificationLimits` | What limits apply, and what was observed? | state, gradeOrRecipe, parameterCode, min/target/maxValue, unitOfMeasure, provenance, actualValue, observationCount |

### Renderers I created

`HistogramChart` · `BoxPlotChart` · `ScatterXYChart` · `StackedSeriesChart` · `HeatmapChart` · `PairedSeriesChart` · `SpecificationTable`

**All routed by ROLE, never by chart code.** In `SavedDashboardWidget.tsx`:

```
hasSpecificationRoles → SpecificationTable
hasTwoAxisRoles       → HeatmapChart        (x + y + value)
hasPairedRoles        → PairedSeriesChart   (category + seriesAValue + seriesBValue)
hasSeriesRole         → StackedSeriesChart  (category + series + value)
hasTwoNumericAxes     → ScatterXYChart      (xValue + yValue)
chartType==="boxPlot" → BoxPlotChart
chartType==="histogram" → HistogramChart
isExtraChartType      → ExtraChart          (legacy)
```

**Order matters** — two-axis is checked before single-axis series.

**Why role routing, not chart code:** `"scatter"` already reaches `ExtraChart`, which resolves its value column by finding the first numeric one. For a relationship result that is `xValue`, so it would plot the axis against itself and **look plausible**.

---

## 5. Every test I ran, and its result — DO NOT RE-RUN THESE

### Backend PostgreSQL integration (against `ppiq_presentation`)

| Suite | Result |
|---|---|
| `DistributionSourceNpgsqlTranslationTests` | 3/3 — `floor()` binning translated over 301,560 values |
| `ParameterValueSpreadNpgsqlTests` | 3/3 — quartiles ordered min≤q1≤median≤q3≤max |
| `ParameterRelationshipNpgsqlTests` | 3/3 — pairing by material identity, no duplicates |
| `MultiSeriesSourcesNpgsqlTests` | 4/4 — grouped `Distinct().Count()` translated |
| `EquipmentStoppageAndImpactNpgsqlTests` | 4/4 — totals vs independent SQL `SUM` |
| `RiskEvidenceNpgsqlTests` | 8/8 — over 500 real risk rows |
| `FinalPageBindingsNpgsqlTests` | 4/4 — positional, specification, equipment pair, refusal preservation |

### Backend unit / architecture

| Suite | Result |
|---|---|
| `ReadinessDimensionParityTests` | 7/7 known answers |
| `RiskEvidenceKernelTests` | 19/19 (parser + fold + registration) |
| `DistributionQuartileKernelTests` | R-7 known answers, hand-computed |
| `NativeDistributionMeasureRegistrationTests` | green |
| `EquipmentImpactIndependenceTests` | 4 regex guards, green |
| `CorrelationEngineRegistrationTests` | 2/2 |
| Full `Analytics.Core` suite | green |
| Full `Architecture.Tests` suite | green |

### Frontend

| Suite | Result |
|---|---|
| `chartSwitcherConvergence.test.tsx` | 17/17 |
| `histogramChart` | 5/5 |
| `boxPlotChart` | 5/5 |
| `scatterXYChart` | 5/5 |
| `stackedSeriesChart` | 6/6 |
| `heatmapChart` | 12/12 (incl. absent≠b0) |
| `pairedSeriesChart` | 7/7 |
| `specificationTable` | 9/9 |
| `associativeFields` | 8/8 |
| `dashboardLayoutSerialisation` | 6/6 |
| `uiConformanceRatchet` + `largeFileBoundaries` | 4/4 every run |
| **T-049 Playwright** | **FAILING in my `signIn` — see §0** |

### Canonical correlation execution (T-045-R1-B)

All 8 governed outcomes executed. `accepted findings = 0`.

```
defect.class              Blocked   readiness gate, honest abstain
defect.position           Blocked
defect.rate_per_m2        Blocked
defect.severity           NoData    0 findings, 26 excluded
downtime.cascade_minutes  Blocked
kpi.energy_per_ton        Blocked
kpi.prime_yield           Blocked
kpi.throughput            Blocked
```

`NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED` is **correct current truth**. Do not build a projection.

---

## 6. Pack engineering — hard-won lessons

### 6.1 Anchor failures cost ~8 runs. Every cause:

| Cause | Fix |
|---|---|
| Anchor spans a **blank line** | Single-line anchors only. Blank lines carry invisible trailing whitespace. |
| Anchor quoted from a **stale export** | Always `git show HEAD:<file>` first. |
| Anchor quoted from **my own memory** of an earlier pack | Later packs inserted lines between; the pair no longer adjacent. |
| **Indentation off by one level** | Dot-render it: `($_ -replace ' ', '.')` |
| Anchor built from **post-change intention** | Anchor the PRE state, obviously. |

**The winning pattern:** anchor on **structural landmarks** — a set declaration + `{`, a method signature, a unique single line — never on neighbouring content.

```powershell
git show HEAD:path/to/File.cs | Select-String "Pattern" -Context 2,6 |
  ForEach-Object { ($_ -replace ' ', '.') -replace "`t", '[TAB]' }
```

### 6.2 `W2-PACK-LE01` — line-ending churn

Appending by normalising the whole file made git report `1124 deletions` for a one-class addition. **Fix:** detect the file's dominant newline convention, append with it, rewrite no existing byte.

```powershell
$crlfCount = ([regex]::Matches($raw, "`r`n")).Count
$lfCount = ([regex]::Matches($raw, "(?<!`r)`n")).Count
$eol = if ($crlfCount -ge $lfCount) { "`r`n" } else { "`n" }
[System.IO.File]::WriteAllText($F, $raw + $eol + $eol + $body.Replace("`n",$eol), $enc)
```

### 6.3 `W2-PACK-KILL01` — revert only runs on a *caught* failure

Closing the terminal mid-run left 7 files applied and unverified. Recovery was clean only because the file set was knowable. **Pack tooling debt, recorded, not fixed.**

### 6.4 THE STDERR TRAP — cost two runs

```powershell
$ErrorActionPreference = 'Stop'
$output = & psql @args 2>&1     # ← NOTICE on stderr becomes TERMINATING
```

`psql` writes `NOTICE` to stderr and exits **0**. `DROP DATABASE IF EXISTS` on an absent database killed a whole pack.

**Fix — relax the preference around the call only:**

```powershell
$oldPreference = $ErrorActionPreference
try { $ErrorActionPreference = 'Continue'; & $FilePath @Arguments; $code = $LASTEXITCODE }
finally { $ErrorActionPreference = $oldPreference }
```

**My own follow-on defect:** I *also* prepended `SET client_min_messages TO WARNING; ` to every `$Sql`. That turned each `-c` into **two statements**, putting `DROP DATABASE` inside an implicit transaction → `DROP DATABASE cannot run inside a transaction block`. **Lesson: one mechanism per problem. A second "belt and braces" fix created a new failure.**

### 6.5 Gate discipline

- **TRX counts are authoritative**, never exit codes. `dotnet test` returns 0 when everything skips.
- Assert a **minimum pass count**, so a skipped suite fails the gate.
- **Resolve build paths in preflight**, not at the gate — hardcoding `PlantProcess.sln` (real name: `Backend/PlantProcessIQ.sln`) failed *after* 11 edits had landed.
- **Discover project paths** by pattern; never hardcode.

### 6.6 Test-writing lessons

- **A failing assertion must say what it saw.** Two of my assertions didn't, costing a whole diagnostic round trip.
- **Don't hardcode domain values in tests** — I used `"site"`/`"gradeOrRecipe"` and the test asserted *which dimensions exist* rather than *that the projection follows the registry*. Fix: read from `FILTERABLE_DIMENSIONS` itself.
- **Ambiguous fixtures hide bugs** — `target: 5, actual: 5` made `getByText("5")` throw on ambiguity. Use distinct values and query by `data-testid`.
- **Invented fixtures prove genericity** — `x1`/`y1`, `alpha`/`beta`. A renderer needing real domain names would not be exercised by them.

---

## 7. Genericity enforcement (Rule 1)

`NativeWidgetSourceGenericityTests` scans `WidgetResultSources.cs` **comments-stripped, word-boundary, case-insensitive**:

```
widgetCode dashboardCode PO_KPI QM_ EO_ MI_ RI_ PA_ CF_
coil slab heat caster steel mill grade fleet
```

**Gotchas measured the hard way:**
- `GradeOrRecipe` **passes** (no word boundary after "Grade")
- `Measures.DefectMixByGrade;` **fails** (semicolon = boundary)
- Column label `"Grade or Recipe"` **fails** → renamed to `"Product Scope"`
- Anonymous field `Grade = ...` **fails** → renamed `GroupKey`

**The guard caught a real design flaw**, not just a name: I had baked one customer's grouping into the seam. Fix was to read `resolved.DimensionCode` — strictly better design.

**Run the guard's own regex inside the pack's self-check** so the failure is instant rather than a 4-minute build cycle.

---

## 8. Findings register

### Open, recorded, unactioned

| ID | Finding |
|---|---|
| `T045-R1-A-F01` | Readiness contract carries no structured **unit** metadata. `0.95` vs `60` — consumer can't tell fraction from count. Blocks Model Insights coverage bars. |
| `W2-PACK-LE01` | Pack line-ending churn (fixed in practice from R1-C onward) |
| `W2-JOB-STATUS-01` | Four `Ml*` jobs report `Ok` with `completed_at` **before** `started_at`, zero `job_run_histories` rows, and **no executor arm** in `JobRunOrchestratorService` (`default:` returns `Failed`). Status written by something other than the orchestrator. |
| `W2-PACK-KILL01` | Pack revert doesn't run when the process is killed |
| `W2-ASSOC-FILTER01` | `gradeOrRecipe` and probably `productFamily` are backend dimensions with **no field in `DashboardFilters`** → can never appear in the associative strip. Dropped honestly, asserted by test. |
| `F2 (T-046)` | `ChartCompatibility.DependsOnQueryState` exists in domain but `DashboardChartRefusalDto` carries only `(ChartTypeCode, Reason)` — structural and query-state refusals arrive identical. **Do not act without executable proof of a transport defect.** |

### Deliberately absent from T-047 (data doesn't exist)

- **Correlation ranked contributor** — no ranked-contributor result source
- **Correlation parameter×outcome heatmap** — zero supported findings
- **Model Insights coverage bars** — `ReadinessDimension` has no per-dimension coverage value or threshold (`independentUnits`/`outcomeEvents`/`windowDays` are report-level and repeat identically)
- **Quality KPI sparkline** — needs a second series in one widget result = new semantics

### My own errors — recorded so they aren't repeated

1. **Reasoned from an obsolete artefact.** I generated migration `20260816101433`, it failed on an empty DB, and I concluded *"there is no migration chain"* — describing my own failed artefact, not the repo. Authoritative is `20260816112200` at commit `55157a98`.
2. **Overstated a data gap.** Said "no specification source data exists" after reading three entities. `grade_specification` and positional contracts existed all along.
3. **Inferred from an aggregate.** Read `ml_correlation_results_v2 = 26` as current supported findings; it was a historical total. Correct check is `evidence_json->>'findingStatus' = 'EvidenceForReview'`.
4. **Claimed another lane's work.** Called the dirty `ML/` tree mine; it is Worker 3's.
5. **Invented an API.** `UseP4SoftDelete()` — real name `UsePostgresXminConcurrencyToken()`.
6. **Wrong hypothesis, tested and disproved.** Blamed `IsBootstrapAdmin=true` for the 401. Direct call returned **HTTP 200** with `isBootstrapAdmin: false`. Real cause: `auth.ts` reads `PPIQ_SMOKE_PASSWORD`, default `""`.

---

## 9. Deployment / server / pipeline — **NO KNOWLEDGE FROM THIS SESSION**

**I have nothing first-hand to report here, and I will not invent it.**

This session touched **zero** deployment surface:
- No Hetzner, Docker Compose, Caddy, or sslip.io work
- No Jenkins pipeline run, inspected, or modified
- No server deployment, no remote database
- Every command ran on `localhost`; every commit is local

Everything I know is second-hand from `PPIQ_Identity_and_Topology_v4.md`:
- Two-layer schema: EF migrations first, numbered SQL scripts as decoration
- Server credentials live in `/var/lib/ppiq-preserve/.env`, git-ignored, reused across deploys
- Volume–password coupling: never hardcode server credentials in committed scripts
- Local dev credentials **are** committed deliberately (§2.1) and are not secrets

**Section 10 (pipeline-green modifications): nothing was done. There is nothing to hand over.**

For real pipeline status, consult the Worker 3 handovers or the audit signals report — not this document.

---

## 10. Rules and conventions Karim enforced — carry these forward

### Absolute
- **Rule 1 (genericity):** no plant/industry/dataset vocabulary in product code. Proven against oil and mineral-water installations.
- **No-PARTIAL:** one task to full closure before the next.
- **Latest-concept law:** dirty or outdated code is deleted or fixed, never built upon.
- **Source documents read directly**, never from summary or memory.
- **Name your own defects before Karim finds them.** Standing rule.
- **A refusal, gap or zero-result is a valid measured outcome**, never masked.
- **False PASS is unacceptable. Plausible partial values are forbidden.**

### Execution
- Every deliverable is a **PowerShell 5.1 apply-pack**: preflight → anchor verify → backup → apply → self-check → gated build/test → auto-revert
- **Karim runs every pack himself** and verifies manually before committing
- **Never `git add .` or `git add -A`** — exact-file staging only
- **Credentials rule (ruled 15 Aug):** no command may prompt. `psql` always gets explicit `-h/-U/-d` plus `$env:PGPASSWORD`. Omitting `-U` falls back to Windows account `ELKA01`, not a Postgres role. Packs expose `-PgHost/-PgPort/-PgUser/-PgPassword/-PgDatabase` defaulting to documented local values. **Server credentials never in committed scripts.**
- Pure ASCII, CRLF, no BOM
- `git show` returns arrays → `Out-String` before scalar comparison

### Communication
- Karim issues rulings in **fenced code blocks** — those are the contract
- **Options labelled A/B** when a decision is needed
- Direct, structured responses; **he dislikes lengthy governance documents**
- Technical content in English; conversational in Arabic (Egyptian)
- **Do not stop for another ruling** unless: architecture contradiction, irreversible choice, cross-worker collision, or impossible acceptance condition

### Scope discipline (learned painfully)
Karim repeatedly and correctly stopped me from expanding scope. Patterns he rejected:
- Redesigning the generic query engine to add a second axis → **use the native seam**
- Adding `SeriesDimensionCode` + migration → **rejected outright**
- Building a `ml_correlation_results_v2 → correlation_results` projection → **rejected**
- Creating six `ParameterDefinitions` when they already existed as `CARBON_PCT` etc. → **rejected**
- Touching `ppiq_app` from the presentation lane → **rejected**

**When you find a gap: report the FIRST MISSING LAYER with its owner. Do not fix upward.**

```
generator/source missing        → correct only the source regression
canonical missing               → correct only the materialisation
analysis/result missing         → correct only the analytical layer
renderer missing                → implement only the registered renderer
everything exists, page unbound → composition only
```

---

## 11. Domain semantics worth preserving

Decisions that took real thought and should not be silently reversed:

- **Quartiles are R-7** (Excel `PERCENTILE.INC` / NumPy default). Nine definitions exist and disagree on small samples. Pinned with hand-computed known answers.
- **Availability outranks compatibility.** A chart both unbuilt and refused reports `unavailable`. Reporting a structural refusal for a chart we never built sends an author to change a binding that is correct.
- **Absent ≠ zero.** Heatmap absent cells are hatched, never the lowest bucket — otherwise unmeasured regions read as the cleanest.
- **Unobserved ≠ conforming.** Specification table has four states; colouring an unmeasured parameter green would be its most damaging possible defect.
- **A stack of one is a bar with a legend.** `StackedColumn` was separated from the `Bar` grammar arm to require a second axis.
- **A parameter scattered against itself is a perfect diagonal** — refused as `SAME_PARAMETER_SELECTED`.
- **Pairing by material identity only.** Never by row order or nearest timestamp — that manufactures a relationship out of collection order.
- **Stopped vs impact are independent.** Four regex guards forbid arithmetic naming both in one expression. The DISTANCE between them is the finding.
- **Extent preserved, not reduced.** Positional facts keep start AND end; the midpoint is a display projection computed at render, never stored.
- **Alternative ≠ possible.** Alternative means the field already has a selection. Collapsing them hides which field the reader is steering by.
- **Malformed contributor JSON refuses the whole row.** Silently dropping one entry publishes a shorter list that still looks complete.
- **One batch is not a trend.** 500 scores in 27 seconds → `INSUFFICIENT_TEMPORAL_RISK_HISTORY`. Proved in both directions with a two-period fixture, without altering the database.

---

## 12. T-050 preview (next after T-049)

**T-050 — drill to population, provenance and evidence.** Marked Very Important. Has a testable chain rather than a visual one.

Do not start it until T-049 closes.

---

## 13. Deferred to `M1-P3-VISUAL-GATE` (after T-052)

- All manual browser walkthroughs
- Subjective visual acceptance
- Screen/video capture
- `T049-VISUAL-01` — manual drag + resize + save + reload demonstration
- Seven-page side-by-side screenshot comparison

**No separate backlog task for these.** They attach to the single phase gate.

---

## 14. Resume checklist

1. Confirm `git rev-parse HEAD` — expect `b687cba4` or later
2. Confirm `55157a98` is an ancestor: `git merge-base --is-ancestor 55157a98... HEAD`
3. **Do not reopen** T-044, T-045-R1, T-046, T-046-R1, T-047, T-048
4. Start API + web on the **presentation** profile
5. Export `PPIQ_SMOKE_USERNAME` / `PPIQ_SMOKE_PASSWORD` / `PLAYWRIGHT_API_URL`
6. Read `playwright.config.ts` for `storageState` — **this is the open question**
7. Fix `signIn()` in `e2e/t049-layout-persistence.spec.ts` accordingly
8. Run until **`3 passed`** — not skipped
9. Exact-stage the two T-049 files, commit, report hash
10. Proceed to T-050

---

*Every commit hash, test count and row count in this document was observed in this session. Sections 9 and 10 are marked as having no first-hand knowledge rather than filled with plausible content.*
