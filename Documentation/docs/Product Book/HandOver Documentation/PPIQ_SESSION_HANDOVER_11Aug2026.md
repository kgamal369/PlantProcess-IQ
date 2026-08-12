# PPIQ SESSION HANDOVER - 10/11 AUGUST 2026
## Worker 2 implementation session: T-043 close-out, T-044 full cycle, D1 Layer A engine, T-045 Pack A

**Read this before touching anything. It exists so the next session does not re-investigate,
re-measure or re-run what is already proven here.**

Every number in this document was measured during this session against `ppiq_presentation`
on the development laptop, or read directly from source. Where something is an inference
rather than a measurement, it says so. Where I was wrong during the session, the correction
is recorded rather than the original claim.

---

# SECTION 0 - THE FASTEST POSSIBLE ORIENTATION

If you read nothing else, read this.

**What was true when this session started.** T-043 was mid-flight at S3. T-044 was believed to
be an 8-hour dashboard certification task. The aggregate engine was believed to work.

**What is true now.** The aggregate engine was proven to be returning **50,000 where the truth
was 301,560** - 83.4 percent of the plant's observations silently missing - and that was the
universal design of every measure, not one defect. A containment barrier was shipped, then a
generic Layer A aggregation engine replaced the broken execution model for two measures. T-044
finished at 8 PASS / 8 advisory / 0 FAIL. T-045 Pack A converged nine drifted widget
definitions. Seven measures still run on the old broken path.

**Commit chain from this session, oldest first:**

| Commit | What it is |
|---|---|
| `e5c2e6cc` | T-043 S1-S3 (from the prior session, present at start) |
| `cfd273cc` | T-044 A2 algebra audit - evidence only |
| `6ba204c1` | T-044 A1 evidence - the 301,560 vs 50,000 proof |
| `c8609735` | T-044 containment - refuse instead of returning a lower bound |
| `7b8d6e8e` | T-044 downtimeMinutes translation + semantics fix |
| `5e1929bd` | T-044 convergence - QM_SEV/EO_EQDEF + v1 seeder retirement |
| `1717ef13` | T-044 debt register (10 findings) |
| `895fb87a` | T-044 D7 - chart identity/display separation |
| `54ee883b` | **D1 Layer A - generic exact BI aggregation engine** |
| `6f424969` | **T-045 Pack A - nine definition convergences + deterministic parameter** |

**HEAD is `6f424969`.**

**The single most important thing to understand before writing code:** this codebase's failures
are almost never "it does not work". They are "it works, it looks right, and it is silently
wrong". Every technique in Section 7 exists because of that.

---

# SECTION 1 - IDENTITY, TOPOLOGY AND ROADMAP

## 1.1 What PPIQ is

A generic, read-only process-to-quality intelligence platform for manufacturing plants.
.NET 9, React/TypeScript, PostgreSQL. Five personas: plant engineer, process engineer, CEO,
infrastructure engineer, software configurator. Built by SOU Industrial Software, Dusseldorf.

**The three product rules that govern every decision:**

- **Rule 1 - Generic only.** No plant-specific vocabulary in product code. Fleet-v2 and steel
  are a *test dataset*, not the product. A customer may be oil, water, pharma, paper, tyres,
  cement or food.
- **Rule 2 - Starts empty.** Zero rows on install.
- **Rule 3 - The 15-step journey is the product.**

Plus the **Single Engine Implementation Law** (Constitution II.7.6): no duplicate
implementations of governance rules. And **deny-by-default** route access control via
`PlantAccessControl.cs`.

## 1.2 Schema topology (from `PPIQ_Schema_Topology_and_DataFlow_Contract_v2.md`)

Exactly three application schemas, no more:

| Schema | Role |
|---|---|
| **Plant Data** | Everything that exists because of this customer's data. Starts empty. Engine outputs live here. |
| **Meta Data** | Everything that ships identically to every customer: layouts, roles, credentials, widget catalogue, licence, job logging. |
| **Dump Store** | Landing zone. Data exactly as it arrived, before interpretation. Never displayed analytically. |

**The isolation rule:** no analytical surface may display a row that did not come from Plant
Data. A widget that could read Dump Store would show a customer their own unmapped source
columns and call it intelligence.

**Part C, the most consequential ruling:** the joins, keys and links a user declares on the
authoring canvas are **permanent**. They are the product's model of that plant for the life of
the installation, authored by the *customer's* engineer, not the vendor.

## 1.3 The engine, two layers

- **Layer A** - exact BI. Count, sum, distinct count, min/max, grouped KPI, filter, window,
  grouping, sorting, top-N. **Never predicts. Never uses ML to estimate a factual count.**
  If the population holds 301,560 observations, the answer is 301,560.
- **Layer B** - learned intelligence. Predictions, anomaly, practice learning, effect estimates.
  Design frozen, **not started**.

**This session built the first production-shaped slice of Layer A.**

## 1.4 Where the roadmap stood, and where it stands now

| Task | At session start | At session end |
|---|---|---|
| T-043 D1 workspace anatomy | S1-S3 committed, S4 pending | S1-S3 committed; S4 filter widget still pending |
| T-044 operational dashboard certification | Measurement started, 5 PASS / 7 advisory / 4 FAIL | **IMPLEMENTATION DONE - awaiting Worker 1 recertification.** 8 PASS / 8 advisory / 0 FAIL |
| D1 aggregate engine remediation | Did not exist as a task | **Foundation shipped.** 2 of 11 measures migrated |
| T-045 analytical page certification | Not started | Pack A committed. Pack B specified, not built |
| T-046, T-047 | Not started | Not started |

---

# SECTION 2 - THE CENTRAL DISCOVERY, IN FULL

This is the spine of the whole session. Everything else follows from it.

## 2.1 How it was found

T-044 was a certification task: measure sixteen widgets on three operational dashboards, five
identical runs each, and certify them. Two widgets - `PO_KPI_OBS` and `EO_OBS` - returned
**different row counts on identical requests**: 47/96/96/92/50 and 6/8/15/15/14.

Both bound `observationCount`. Everything else was deterministic. That was the thread.

## 2.2 The root cause

`DashboardWidgetQueryService` executed **every** aggregate measure in this order:

```
filter -> project to WidgetFact -> .Take(RawRowLimit) -> ToListAsync -> GroupBy in C# -> aggregate
```

The cap is applied to **raw fact rows, before grouping**. So every aggregate over a population
larger than the cap was computed from an arbitrary subset. And because PostgreSQL's `LIMIT`
without `ORDER BY` may return any rows, the subset changed between runs.

An audit of all 11 `.Take()` call sites found **zero** legitimate raw-detail operations. Every
one fed an aggregate.

## 2.3 The measurement that proved it

| Quantity | Value |
|---|---|
| Trusted `observationCount` (PostgreSQL, whole population) | **301,560** |
| Engine result, every one of five runs | **50,000** |
| Missing | **251,560 (83.4%)** |

**The number the widget displayed WAS THE SAFETY LIMIT.** Counting a capped 50,000-row sample
yields exactly 50,000 every time. The widget was not reporting a measurement of the plant; it
was reporting `DefaultRawRowLimit` with a chart drawn around it.

Group counts across five identical runs, against 97 trusted days: **96, 79, 36, 36, 73**.
Months, against 4: **4, 4, 4, 4, 2** - one run showed half the year.

## 2.4 Why adding ORDER BY would not have fixed it

This mattered enough to write into the evidence file. Adding `ORDER BY` before `.Take()` makes
the engine return the *same* 50,000 rows every time. The result becomes reproducible and is
**still missing 251,560 observations**. The instability would disappear and the wrong answer
would remain, now wearing the appearance of reliability.

**The instability was the only symptom by which the defect announced itself.** Fixing
determinism first would have hidden it permanently.

## 2.5 The population census

| Population | Rows | Cap | State |
|---|---:|---:|---|
| `parameter_observations` | **301,560** | 50,000 | TRUNCATED, 6x over |
| `process_step_executions` | **53,095** | 50,000 | TRUNCATED |
| `material_units` | 35,915 | 250,000 | under cap today |
| `quality_events` | 7,844 | 50,000 | under cap today |
| `risk_scores` | 500 | 50,000 | under cap |
| `data_quality_issues` | 7 | 50,000 | under cap |
| `downtime_events` | 630 | 50,000 | under cap |

**The census found a third victim nobody suspected.** `EO_TABLE` (materialCount by equipment)
reads `process_step_executions` at 53,095 rows. It had been certified **PASS** - nine stable
equipment rows, deterministic across five runs - because the cap lands on the same nine
equipment groups every time. The categories were stable while **the numbers underneath them
were computed from 94 percent of the rows**. Nothing in its behaviour revealed it.

**Lesson: for measures that count, the failure is at least detectable because the result equals
the cap. For measures that average, a capped sample yields a plausible average with nothing to
notice at all.**

## 2.6 Three compounding defects found while deriving the algebra

**(a) The time window was applied AFTER the cap, in memory.** `ApplyFactDateFilter` ran on the
materialised list. So a request for one week did not fetch that week - it fetched an arbitrary
50,000 rows from the whole population and kept whichever fell in the week. **The narrower the
window, the more wrong the answer.** All A1 measurements were taken with no window, so this is
strictly worse than what A1 recorded.

**(b) Rows with no timestamp pass every time filter.** `!x.EventTimeUtc.HasValue || x.EventTimeUtc >= from`
admits undated facts into *every* window, including windows excluding the whole dataset. They
group under `unknown`. **This is the source of the seven "unattributed bucket" advisories** -
not merely missing data, but undated rows deliberately admitted to every period.

**(c) The aggregate sort tie-breaks on the display label.** `ThenBy(x => x.DimensionLabel)`
means renaming a piece of equipment reorders a chart although no measurement changed, and two
groups sharing a value and a label fall back to incidental order.

---

# SECTION 3 - THE AGGREGATION ALGEBRA (A2)

**Committed as `docs/m1/evidence/T-044/A2_aggregation_algebra.md` at `cfd273cc`.**

The audit was ordered before any code, precisely so the number of families would be *derived*
rather than asserted. I had proposed three. **It is five.**

| Measure | Family | Sufficient statistics | Merge rule | Day-foldable |
|---|---|---|---|---|
| `materialCount` (non-relational dims) | Additive | count | SUM | yes |
| `materialCount` (equipment/shift/area) | **Distinct count** | the distinct key SET | none | **no** |
| `defectCount` | Additive | count | SUM | yes |
| `observationCount` | Additive | count | SUM | yes |
| `dataQualityIssueCount` | Additive | count | SUM | yes |
| `downtimeMinutes` | Additive | sum | SUM | yes |
| `avgParameterValue` | Weighted mean | sum, contributing count | SUM(sum)/SUM(count) | yes, if both travel |
| `riskScore` | Weighted mean | sum, count | SUM(sum)/SUM(count) | yes |
| `processStepDuration` | Weighted mean | sum, count | SUM(sum)/SUM(count) | yes |
| `maxParameterValue` | **Extremal** | max | MAX | yes |
| `minParameterValue` | Extremal | min | MIN | yes |
| `defectRate` | **Ratio over a distinct denominator** | numerator count + distinct SET | none | **no** |

**The two corrections that mattered:**

**`materialCount` is not additive on three of fourteen dimensions.** When the dimension is
Equipment, ShiftCode or Area, `ExecuteMaterialCountAsync` reads `process_step_executions` and
de-duplicates so a material passing an equipment twice counts once. **It is a distinct count
wearing a count's name**, and `EO_TABLE` is bound to exactly that path. Folding it at day grain
would have silently double-counted every material spanning midnight.

**Extremal is a separate family.** Min/max form a semilattice; they fold from any partition
without loss but must not be forced through the average path.

`defectRate` computes `defectCount / COUNT(DISTINCT material)`. A material observed Monday and
Tuesday is one material in the week. Summing daily distinct counts returns two. It must execute
`COUNT(DISTINCT ...)` at the grain the user asked for.

---

# SECTION 4 - WHAT WAS BUILT

## 4.1 The containment barrier (`c8609735`)

**Purpose:** stop the engine presenting the aggregate of a truncated population as truth, while
the real correction was designed. Not the fix - the barrier.

**Mechanism, and why it is exact.** Every raw fetch asks for `RawRowLimit + 1` rows. If the
extra row comes back, the population exceeded the limit. One comparison, no counting query, no
second round trip, cannot pass by accident.

**Why a returned-row count would not have worked:** because the window filter runs after the
cap, a narrow window can leave few rows on screen while the fetch behind it was truncated. Only
"did the fetch reach the ceiling" detects that.

**Refusal shape:** HTTP **422**, `business_rule.violation`, token
`aggregate_population_limit_exceeded`, carrying the measure, the limit and the reason.
**No partial value travels beside the refusal** - a number presented next to a warning is still
read as the answer.

**Result:** `PO_KPI_OBS`, `EO_OBS`, `EO_TABLE` began refusing. The other thirteen were
unchanged, same row counts and same fingerprints, which is what proved the barrier was
correctly scoped.

## 4.2 `downtimeMinutes` - two defects in one method (`7b8d6e8e`)

**Defect 1, translation.** `materialIds.Contains(downtime.MaterialUnitId.GetValueOrDefault())`
cannot be translated by Npgsql. **Every call to `downtimeMinutes` threw HTTP 500.** It was
registered, published by the metadata endpoint, offered in the authoring panel, listed in
`ExecutableMeasures` and fully implemented - and **no widget on any of the seven seeded
dashboards bound it, so it had never been executed once.** It was found only when T-044
proposed binding a widget to it.

The correct translatable pattern **already existed 138 lines below** in
`ExecuteDataQualityIssueCountAsync`: `!x.MaterialUnitId.HasValue || materialIds.Contains(x.MaterialUnitId.Value)`.
So it was a one-method omission, not a missing idiom.

**Defect 2, semantics - found by the regression test on its first run.** The measure summed
neither `StoppedMinutes` nor `ProductionImpactMinutes`. It computed
`EndedAtUtc - StartedAtUtc` - a wall-clock quantity the plant never recorded - and returned
**0** for any event with no end timestamp. Both governed decimal columns were discarded.

**Ruled: `downtimeMinutes` = recorded `StoppedMinutes`.** `ProductionImpactMinutes` is a
different question needing its own named measure; the entity's own comment explains that a
three-minute trip can cost six hours of production, which is why the two cannot be conflated.

**The test fixture makes all three quantities different on purpose** so it cannot pass on the
wrong one: stopped 51, wall-clock 50, impact 103. It asserts against the two wrong answers
**by name** before asserting the right one.

## 4.3 T-044 semantic convergence (`5e1929bd`)

**Track B - equipment attribution for quality events. CLOSED AS IMPOSSIBLE.**

`QualityEvent` carries `MaterialUnitId`, `DefectCatalogId`, `EventAtUtc`, `EventType`,
`Severity`, `Decision`, `Description` - **no `EquipmentId`, no `ProcessStepExecutionId`**. The
canonical quality event has no equipment relationship **by construction**.

Source-side scan: equipment identifiers exist on `downtime_events`,
`process_step_executions`, `parameter_observations`, and in landing shapes
`src_inspection_mysql_shape.downtime_events` and `.maintenance_events`. **Not one quality or
defect table in any schema has an equipment column.** `inspection_jobs` has an `equipment_id`
but 3 rows and **0** with a value.

**The inference was sized and then refused.** All 7,844 events belong to a material that passed
at least one equipment, so an inference was *available*. It is not defensible:
**4,234 of 4,528 materials passed two distinct equipment** (only 294 passed one), and **only
2,289 of 7,844 events (29%) fall inside any step time window** - surface inspection happens
after rolling, not during it. Time-based attribution would discard 71% and assign the rest
arbitrarily.

**Outcome:** `QM_SEV` -> *Quality Events by Type* / bar / `defectType` / `defectCount`
(15 real categories: SCALE 1,550, EDGE_CRACK 894, ROLLED_IN_SCALE 715, SLIVER 537 ...).
Title says "Quality Events" not "Defects" deliberately, because the largest bucket
(`Disposition`, 1,883) is **not a defect**, and "Defects by Type" would be semantically false.

`EO_EQDEF` -> *Downtime Minutes by Equipment* / bar / `equipment` / `downtimeMinutes`.
Preflight proved it: 630 events, **630 of 630 attributed**, 9 equipment, largest share 26.8%,
eight of nine carry at least 5%.

**And the drift discovery.** `QM_SEV`'s live definition (*"Defects by Equipment"* on `equipment`)
was produced by **no source artifact in the repository**. Every seeder wrote
`'Severity Distribution' 'donut' 'severity'`. The live row was a manual mutation present since
at least the 27 July census. **A clean rebuild could not reproduce it.**

**And the fifth writer.** The convergence proof - not review - caught
`scripts/demo/Seed-PresentationDashboards.ps1`, a superseded v1 seeder writing the **same widget
UUIDs under different codes**, in **thirteen** cases (`QM_SEVERITY`/`QM_SEV`,
`EO_EQUIP_DEFECTS`/`EO_EQDEF`, `PO_KPI_MATERIALS`/`PO_KPI_MAT`, and ten more). Running it would
have restored exactly the definitions T-044 had just retired. **Retired, no archived `.ps1`** -
an archived runnable copy under the active tooling tree is still a runnable producer.

A **same-UUID invariant** now runs in the convergence proof: 29 UUIDs across four active
seeders, one code and one definition each. It also fails if the scan matches **zero** rows,
because a regex that silently matches nothing would report a clean tree forever.

## 4.4 D7 - chart identity/display separation (`895fb87a`)

`categoryKey` carried two responsibilities. `InteractiveCharts.tsx` binds
`XAxis dataKey={categoryKey}` (lines 125, 267, 289) and `nameKey={categoryKey}` (192), so
`EO_EQDEF` plotted nine bars labelled with equipment UUIDs while `Continuous caster 1`
travelled invisibly in the selection payload.

**The trap:** pointing the chart at `dimensionLabel` alone would have fixed the picture and
**silently broken filtering**, because `ChartExtras.tsx` writes that same value into page-level
filter state via `setFilter(field, cat)`. A label matches no row in the canonical column, so
every click would have selected nothing while looking perfectly correct.

**Solution:** two concepts. `categoryKey` stays canonical identity; `displayKey` resolves
`dimensionLabel` with fallback to identity. Charts receive `displayKey`; each `selection`
literal keeps `valueKey: categoryKey`. `ChartExtras` gains an optional `labelKey` defaulting to
`categoryKey`, materialises `{cat, label, val}`, displays `label`, and keeps `cat` for
`toggle()`, `setFilter` and the React key.

**Proof strategy, stated honestly in the test file itself:** the heatmap path renders plain
buttons and is **mounted for real** - it asserts the visible text is `Continuous caster 1`, that
clicking calls `setFilter("equipmentId", "1933641c-...")` and **explicitly not** with the label,
and that omitting `labelKey` falls back to the UUID. The Cartesian and pie paths go through
Recharts which will not lay out in jsdom without a sized container, so they are held by a
**comment-stripped source guard**. The test file says which half is which rather than letting
the second read as the first.

## 4.5 D1 LAYER A - the generic aggregation engine (`54ee883b`)

**The structural gift that made it small:** every measure already projected to a common
`WidgetFact` shape **inside the IQueryable**, before `.Take()`. So the foundation did not need
to know anything about measures or widgets - it operates on `IQueryable<WidgetFact>` plus one
governed dimension projection.

**New file `DashboardAggregateExecutor.cs` contains:**

- `WidgetFact`, `DashboardAggregateRow`, `DimensionValue` as **internal** contracts (they were
  private nested types, which is exactly why a generic executor could not exist - nothing
  outside the class could name the shape every measure already projected into)
- `DashboardGroupKey` - one shape for every dimension: `Text`, `Id`, `Year`, `Month`, `Day`
- `DashboardDimensionProjection` - **the single dimension authority**. The only place that turns
  a registered dimension code into an EF-translatable grouping expression, and the only place
  that turns a grouped key back into canonical identity plus fallback label
- `DashboardAggregationFamily` - `Additive`, `DistinctMaterial`
- `DashboardAggregateExecutor` - applies the time predicate **relationally**, groups in
  PostgreSQL, folds day-grain to week/month **only for additive**, orders by value then
  **canonical key**, takes `MaxRows` over **aggregate groups**
- `DashboardDimensionNotRegisteredException`, `DashboardNonMergeableFoldException`

**Nullable GUIDs group by the native `Guid`**, deliberately not stringified inside the grouping
expression: `Guid.ToString()` translation is the known Npgsql failure point, and a cast in the
`GROUP BY` also defeats any index on the id column. Stringification and labels happen after
aggregation.

**Week semantics preserved exactly.** The existing C# arithmetic
`ceil((dayOfYear + firstDayOfYear.DayOfWeek) / 7.0)` is kept. It is **not ISO 8601** and not
`date_trunc` - it uses the calendar year rather than the ISO week-year. Correcting it is a
separate ruled change: fixing truncation must not silently move a calendar boundary.
**On this dataset (April-July 2026, no year boundary) it agrees with ISO**, which is why the
week counts matched at 15.

**An unregistered dimension is now refused by name.** The old code returned
`("unknown", "Unknown")` and grouped an entire population under one bucket that looked like data.

**Migrated: `observationCount` (additive) and `materialCount` (additive, or `DistinctMaterial`
on equipment/shift/area).** The other nine keep the containment refusal.

---

# SECTION 5 - EVERY TEST AND MEASUREMENT RUN, WITH RESULTS

**Do not re-run these. The results are here.**

## 5.1 T-044 certification, three runs of the same instrument

`tools/packs/Measure-T044-v2.ps1` - read only, five identical runs per widget, three
fingerprints (key set sorted, key+value sorted, raw order), label state, stability class.

| Run | Context | PASS | Advisory | FAIL |
|---|---|---:|---:|---:|
| 1 | Before containment | 5 | 7 | 4 |
| 2 | After containment | 4 | 7 | **5** (three now refusing) |
| 3 | After QM_SEV/EO_EQDEF convergence | 6 | 7 | 3 |
| 4 | **After D1 Layer A** | **8** | **8** | **0** |

**Final state, all sixteen DETERMINISTIC across five runs:**

| Widget | Chart | Dimension | Rows | Verdict |
|---|---|---|---:|---|
| PO_KPI_MAT | kpi | day | 95 | advisory: 1 unattributed bucket |
| PO_KPI_OBS | kpi | day | 96 | advisory (was FAIL) |
| PO_KPI_DEF | kpi | day | 96 | advisory |
| PO_KPI_RATE | kpi | day | 95 | advisory |
| PO_TREND | line | day | 95 | advisory |
| PO_MIX | donut | materialUnitType | 3 | PASS |
| PO_WEEK | area | week | 15 | advisory |
| PO_TABLE | table | gradeOrRecipe | 6 | PASS |
| QM_TREND | line | day | 95 | advisory |
| QM_BREAK | bar | gradeOrRecipe | 6 | PASS |
| QM_SEV | bar | defectType | 15 | **PASS** |
| QM_TABLE | table | gradeOrRecipe | 6 | PASS |
| EO_EQDEF | bar | equipment | 9 | **PASS** |
| EO_OBS | line | week | 15 | **PASS** |
| EO_TABLE | table | equipment | 9 | **PASS** |
| EO_MONTH | bar | month | 5 | advisory |

**Critical observation from run 4 that must not be lost:** value fingerprints changed on widgets
that were never migrated - `PO_KPI_MAT` `efae59f601` -> `ac75e50d9a`, `PO_TABLE` `cc90656c23` ->
`b52d3187a7`, and four others. Same categories, different numbers. That is `materialCount` now
reporting over the whole population with the date window applied **relationally** rather than in
memory. **`material_units` at 35,915 was never truncated - the numbers moved because the
in-memory window and grouping were dropping rows the relational path keeps.**
`PO_KPI_DEF` and `QM_TREND` are unchanged (`11322cb590`, `d5fea61625`) because `defectCount` and
`defectRate` are unmigrated - which is the control that makes the above attributable.

**So the seven unmigrated measures are still wrong in that same way wherever a window applies.**

## 5.2 T-045 certification, first run

`Measure-T044-v2.ps1 -DashboardCodes PARAMETER_DEEP_ANALYSIS,CORRELATION_FINDINGS_BOARD,RISK_INTELLIGENCE,MODEL_INSIGHTS`

**13 widgets: 3 PASS, 4 advisory, 6 FAIL.**

| Widget | Binding | Rows | State |
|---|---|---:|---|
| PA_KAVG | avgParameterValue/day/`rolling.cooling_rate` | **0** | empty - parameter does not exist |
| PA_KOBS | observationCount/day/same | **0** | empty |
| PA_TREND | avgParameterValue/day/same | **0** | empty |
| PA_BYP | observationCount/parameterCode | 29 | meaningful |
| PA_TABLE | observationCount/parameterCode | 29 | meaningful but **duplicate of PA_BYP** |
| CF_RATE | defectRate/day | 95 | meaningful, **not a finding** |
| CF_TOP | defectCount/equipment | 1 | null attribution |
| RI_KPI | riskScore/day | 1 | one day |
| RI_TREND | riskScore/day | 1 | **one-point line** |
| RI_EQUIP | riskScore/equipment | 1 | null attribution |
| RI_TABLE | riskScore/materialUnitType | 3 | meaningful |
| MI_RATE | defectRate/day | 95 | **not a model result** |
| MI_SEV | defectCount/materialUnitType | 1 | one slice |

**Five root causes, not thirteen defects:** an invalid parameter literal; risk coverage of 1.4%
with one class on one day; two widgets bound to `equipment` on populations with no equipment
attribution; a Correlation board containing no correlation; a Model Insights page containing no
model output.

## 5.3 Published-measure smoke pass

Ten measures published by the registry, each called once (parameter measures called twice).

| Measure | Result |
|---|---|
| materialCount, defectCount, defectRate, downtimeMinutes, riskScore, dataQualityIssueCount | EXECUTES |
| avgParameterValue / maxParameterValue / minParameterValue with no parameter code | **400 validation refusal - correct behaviour** |
| same, with `ACID_CONC_PCT` | EXECUTES, 96 rows each |
| processStepDuration | **422 containment refusal** (53,095 over the 50,000 cap) |

**Corrected verdict: nine execute, one refuses on the cap, zero broken.** The instrument
initially reported "BROKEN: 3" because it classified any non-422 error as broken; a 400
validation refusal is the validator working. **Instrument correction owed (see Section 9).**

**Two findings from this pass:** `observationCount` is in `DashboardMetadataCodes.Measures`, in
`ExecutableMeasures`, and bound by two live widgets - **but absent from the ten measures the
metadata endpoint publishes**. And `riskScore` by day returns **one** category, as does
`dataQualityIssueCount`.

## 5.4 Parameter population census (T-045)

Top parameters by observation count, all with genuine variation:

| Parameter | Obs | Days | Min | Max | Stddev | Unit |
|---|---:|---:|---:|---:|---:|---|
| THICKNESS_MM | 17,010 | 94 | 1.42 | 4.08 | 0.864 | mm |
| WIDTH_MM | 17,010 | 94 | 940.25 | 1559.33 | 227.332 | mm |
| ROLL_SPEED_MPS | 17,010 | 94 | 6.01 | 17.12 | 1.579 | m/s |
| SUPERHEAT_C | 17,010 | 93 | **-2.71** | 51.03 | 8.472 | C |
| ROLL_FORCE_KN | 17,010 | 94 | 9787.94 | 22765.44 | 1794.912 | kN |
| **FDT_C** | **17,010** | **94** | **798.41** | **957.99** | **25.660** | **degC** |
| ROLL_GAP_MM | 17,010 | 94 | 1.37 | 5.52 | 0.587 | mm |
| MOULD_LEVEL_AVG | 17,010 | 93 | -8.75 | 8.99 | 2.726 | mm |
| CASTING_SPEED_MPM | 17,010 | 93 | 0.91 | 1.82 | 0.140 | m/min |
| ROLL_TEMP_C | 17,010 | 94 | 831.64 | 919.24 | 11.312 | degC |
| CT_C | 17,010 | 94 | 494.66 | 724.52 | 36.109 | degC |
| BATH_TEMP_C | 15,295 | 97 | 72.01 | 91.99 | 5.865 | degC |

48 parameter definitions exist in total.

**`FDT_C` was ruled the presentation parameter** for population, time coverage and physical
interpretability. **I initially recommended `SUPERHEAT_C` and claimed it had the strongest
variance-to-range ratio. That claim was wrong** - stddev/range puts `WIDTH_MM` at 0.367,
`MOULD_LEVEL_AVG` at 0.154, `SUPERHEAT_C` at 0.158 (fourth). And `SUPERHEAT_C`'s **-2.71 C
minimum is a genuine plausibility problem**: superheat is temperature above liquidus, so a
sustained negative means steel below its freezing point in the tundish.
**The variance claim is withdrawn and must not enter evidence.**

## 5.5 Correlation and risk population

| Query | Result |
|---|---|
| `correlation_results` rows | **0** |
| distinct correlation_type | 0 |
| latest calculated | none |
| job_run_histories by status | Ok 1,696 / Failed 1 / Running 1 |
| `risk_scores` | 500, all `risk_class = Low`, one scoring date (5 Aug, 28-second window) |
| risk score min/max/stddev | 0.050 / 0.193 / 0.031 |
| scored by type | Coil 308, Slab 175, Heat 17 |
| eligible by type | Coil 17,012, Slab 17,011, Heat 1,892 |
| **coverage** | **500 / 35,915 = 1.4%** (Coil 1.8%, Slab 1.0%, Heat 0.9%) |

**`correlation_results = 0` means "no supported findings are currently published". It does NOT
mean "no correlation exists in the plant data."** That distinction is a frozen ruling.

**And `risk_scores` has NO provenance columns at all** - no `source_system`, no `model_version`,
no `is_synthetic`. The provenance query returned `<no column>` three times. So the
classification is `SCORING_SOURCE_UNKNOWN` **because the schema records nothing**, not because
values are null. No amount of presentation work can recover what was never recorded.

## 5.6 `downtimeMinutes` preflight

630 events, **630 attributed to 9 equipment, zero unattributed**, largest share 26.8%, eight of
nine carry >=5%, categories unplanned 273 / quality 141 / planned 123 / logistics 93.

Trusted stopped-minutes by equipment (this is the correct reference; the impact-minutes column
in that same table is **not** what the engine sums):

Continuous caster 2 4,558.8 | Hot strip mill 4,133.7 | Pickling line 2 4,109.4 |
Ladle furnace 1 3,214.5 | Electric arc furnace 1 3,163.2 | Electric arc furnace 2 2,979.8 |
Continuous caster 1 2,974.6 | Pickling line 1 2,645.0 | Ladle furnace 2 2,535.8.
**Engine total after the fix: 30,314.9.**

## 5.7 Automated test suites run

| Suite | When | Result |
|---|---|---|
| `dotnet build PlantProcess.Application` | every backend pack | 0 (warnings only) |
| `DowntimeMinutesMeasureExecutionTests` (real PostgreSQL) | `7b8d6e8e` | green after two red iterations |
| `GenericAggregateEngineTests` (real PostgreSQL) | `54ee883b` | green after three red iterations |
| `GenericAggregateEngineGenericityTests` (architecture) | `54ee883b` | **green on first run** |
| `chartCategoryLabels.test.tsx` (8 tests) | `895fb87a` | green |
| `workspaceSelectionsBar` (4), `workspaceSheetSwitching` (4) | `895fb87a` | green |
| `uiConformanceRatchet` (1), `noRawStandardElements` (2) | `895fb87a` | green |
| `tsc -b` | `895fb87a` | 0 |
| Frontend total at D7 | | **19 tests, 5 files, all pass, 117s** |

**Persistent build warnings, present before and after this session, not introduced by it:**
`CS0618 CorrelationService is obsolete` in `DependencyInjection.cs:112`;
`CS8629 Nullable value type may be null` in `DashboardWidgetQueryService.cs` (line number moves
with edits: 559 -> 574 -> 585); a large family of `CS8604`/`CS8620` in
`Phase2OperationEndpoints`, `Phase2InvestigationEndpoints`, `JobLogService`, `AlertEndpoints`;
`CS0162` unreachable code in `V5EnterpriseSsoScimEndpoints:694`; `CS1998` in `CsvConnector` and
`VisualMapperEndpoints`.

---

# SECTION 6 - RULINGS AND OPERATING RULES TO CARRY FORWARD

These are Karim's, frozen. They are not suggestions and they resolve most implementation
questions without asking.

## 6.1 Truth hierarchy

**Truthful exact result > explicit bounded refusal > plausible partial value (FORBIDDEN).**

A resource limit may never silently redefine the mathematical population of an aggregate. If a
governed limit prevents a faithful computation, the permitted result is an explicit refusal
carrying its reason. No partial value beside it.

## 6.2 Source truth = rebuild truth = live truth

The live database is **never** the authority. Any product-semantic database change is incomplete
until the equivalent permanent definition exists in tracked source control. Every data
correction is paired with the script change that makes it reproducible, in the same commit.

## 6.3 Genericity is non-negotiable (D1 charter, memory #7)

No dashboard code, widget code, presentation seed identity, demo equipment identity, dataset
cardinality assumption or industry vocabulary in engine code. No `if widgetCode ==`. No
`if dashboardCode ==`. No Coil/Heat/grade/caster/Fleet-v2/steel literals. A customer may be oil,
water, pharma, paper, tyres, cement or food. Page and widget *definitions* may carry
presentation-specific configuration; generic engines may not.

## 6.4 Class 1 vs Class 2 result sources (frozen for T-045 Pack B)

- **Class 1** - aggregate/fact-shaped: `WidgetFact` -> aggregate executor -> `BuildResult` ->
  `DashboardWidgetQueryResultDto`.
- **Class 2** - native-rich: registered source executor -> source-declared `columns[] + rows[]`
  -> the **same** `DashboardWidgetQueryResultDto`. **Bypasses `WidgetFact` and `BuildResult`.**

Never fork the public envelope. Never flatten rich rows into one decimal `Value`. Never add a
`readinessWidget` / `predictionWidget` / `correlationWidget` visual kind - readiness is
intelligence semantics, not visual grammar.

## 6.5 Timebox rule

- up to 100% of estimate: normal execution
- 125%: report the reason for overrun
- 150%: mandatory scope review, extract substantial newly discovered work
- 200%: absolute stop unless explicitly authorised

A systemic defect, structural redesign or >1-hour newly discovered remediation **becomes a
separate dependency**, not absorbed. **T-044 is the example not to repeat**: a certification
task absorbed a systemic engine defect and consumed two days.

**Governing delivery rule:** `find -> prove -> disposition -> split -> continue`, never
`find -> absorb -> investigate -> redesign -> absorb again`.

## 6.6 Batch execution mode

The loop is `inspect -> measure -> classify -> implement -> test -> fix local failures ->
prove -> commit -> report`. Do not stop for a query result, a missing literal, seed/live drift,
naming, a deterministic tie-break, a test failure caused by your own change, or a local bug
below the one-hour boundary.

**Stop only for:** a genuine architecture contradiction with no existing ruling; scope expansion
beyond ~1 hour of new subsystem work; irreversible or destructive risk.

**And the pack must do its own measuring.** The delivery shape is: one self-contained pack ->
one user command -> the pack measures, decides, patches, proves, and commits or rolls back.
Do not ask for query output in advance unless it is technically impossible to obtain from the
repository or database inside the pack.

## 6.7 Reporting

Closure reports, not progress diaries. What changed, what was measured, what tests passed,
commit hash, remaining debt explicitly outside scope.

## 6.8 Terminology honesty (T-045)

Current risk rows must **not** be called ML predictions, learned-model scores or production
model output. Use `Rule-Based Risk Baseline` / `Deterministic Risk Score` / `Scored Population` /
`Coverage Against Reference Population` - **and only when provenance proves it**. With
`SCORING_SOURCE_UNKNOWN` measured, even "rule-based" is unproven. Model Insights must
distinguish the deterministic baseline state from `MODEL_NOT_READY` for the production learned
model, **data-driven, not a frontend literal**.

**Denominator naming:** `referencePopulation` or `totalMaterialPopulation`, never
`eligiblePopulation`, until a real scoring-eligibility resolver proves that denominator.

## 6.9 Pack delivery contract

PowerShell 5.1. Preflight checks, anchor verification against exact on-disk text, backup, apply,
on-disk self-check, gated build/test, auto-revert on any failure. Nothing written without the
apply switch. **No `git add .`, no `git add -A`** - exact-file staging only. No zip files, no
em-dashes, no curly quotes, no `&&` in PowerShell, no `style={{` inline style objects. Simulate
the pack against reconstructed on-disk state before delivery.

---

# SECTION 7 - TIPS, TRICKS AND TRAPS LEARNED THE HARD WAY

**This section is the highest-value part of the handover. Every entry cost a red round.**

## 7.1 PostgreSQL and SQL

- **`GROUP BY 1` where column 1 contains `count(*)` is rejected.** I wrote this **three times**
  in one session. Always group by the real columns. And build the group list from columns that
  actually exist, or a missing column puts a constant in the `GROUP BY`, which is also rejected.
- **UNION branches must be type-consistent.** `count(*)` (bigint) unioned with `::text` fails.
  Cast every branch to text.
- **`left(jsonb, int)` does not exist.** Cast to text first.
- **Embedded double quotes in SQL passed through `psql -c` from PowerShell get mangled.** Avoid
  `to_char(x, 'IYYY-"W"IW')`; use a form with no inner quotes.
- **`dimension_code` is NOT NULL** in `dashboard_widget_definitions`. A dimensionless KPI is
  `''`, not `NULL`. The seeders write `''`.

## 7.2 PowerShell

- **A function returning `@(...)` with one element unwraps to a scalar.** `$result[0]` then
  indexes the first *character*. This reported a 301,560-row total as **51** - the ASCII code of
  `'3'`. Use `return ,$rows` and route single values through a `Get-Scalar` helper.
- **An unassigned native command writes to the pipeline.** `& psql ... -f file` returned its
  whole transcript beside `$LASTEXITCODE`, so a failure printed as
  `psql exited BEGIN ROLLBACK ...`. Use `| Out-Host`.
- **`$q | ConvertTo-Json` passed to `curl.exe` loses its quotes.** Use `Invoke-RestMethod`.
- **`Invoke-RestMethod` discards the error body.** For a 4xx you get only "The remote server
  returned an error". Catch and read `$_.Exception.Response.GetResponseStream()`.
- **Windows saves a re-download as `name (1).ps1`.** `Move-Item` then moves the **old** file and
  you debug a fix that was never applied. Always `Remove-Item "$env:USERPROFILE\Downloads\name*.ps1"`
  first, or version the filename.
- Guard cuddled braces: PowerShell needs `} else {` on one line.

## 7.3 EF Core and Npgsql

- **`Guid?.GetValueOrDefault()` cannot be translated.** Use
  `!x.Id.HasValue || set.Contains(x.Id.Value)`.
- **EF cannot see through a constructor call to a column.** A **positional record** projection
  makes `new WidgetFact(...15 args...).EquipmentId` untranslatable and the whole `GroupBy` fails.
  **Init-only properties plus a parameterless constructor** let EF simplify
  `new X { A = col }.A` to `col`. This is the single most important EF lesson of the session.
- Grouping by a member-init custom class **does** translate once the projection is initialiser-shaped.
- Group nullable GUIDs **natively**; stringify after aggregation.

## 7.4 Testing

- **A proof that cannot fail the way the product failed is not a proof.** An in-memory or SQLite
  provider translates the broken expression happily. Real PostgreSQL, or nothing.
- **Make the wrong answers different from each other, and assert against them by name.** The
  downtime fixture uses stopped 51 / wall-clock 50 / impact 103 and asserts `!= 50` and `!= 103`
  before asserting `== 51`. A test that only checks the right number tells you nothing about
  which wrong number it got.
- **Recharts does not lay out in jsdom** without a sized container. Where you cannot mount,
  use a comment-stripped source guard **and say so in the test file** rather than letting a
  source proof read as a mounted one.
- **Self-contained fixtures with unique probe codes and a `finally` cleanup**, asserting only on
  their own group, so they cannot pass or fail because of production rows.
- **Use non-steel vocabulary in fixtures** - a filling line, a bottling batch - so nothing
  quietly certifies Fleet-v2 assumptions.

## 7.5 Guards and scans

- **Any scan for a forbidden construct must run against comment-stripped code.** A pack refused
  itself because the engine's header comment explained that it *replaces* `Take(RawRowLimit)`.
  The in-repo guard stripped comments; the pack preflight did not, and the two disagreed.
- **A guard must not quote the literal it forbids.**
- **An idempotence guard must be specific.** Testing for `MaterialUnitId.HasValue` anywhere in a
  file matched a *correct* pre-existing use elsewhere and refused the pack twice.
- **A `Pass` line printed unconditionally beside a `Fail` line is a defect.** Make it conditional
  on the failure count not moving. This recurred because a fix was applied to the shipped `.ps1`
  and not to the generator that produces it - **fix the generator, not the artifact**.
- **A scan that matches zero rows must fail.** A regex that silently matches nothing reports a
  clean tree forever.

## 7.6 Process

- **Simulate the preflight, not only the edit.** Several red rounds came from simulating the
  transformation and never simulating the guards that decide whether it runs.
- **A textual replay cannot see C# name resolution.** `ExecuteDefectRateAsync` names its list
  `materialFacts`, not `facts`; the anchor counts were perfect and the build failed CS0103. The
  simulation now checks that each inserted guard names a variable declared in the same method.
- **Verify constructor arities from the entity, never infer.** `ProcessStepExecution` is
  `(materialUnitId, operationType, startedAtUtc, endedAtUtc, isSynthetic, equipmentId)`.
- **`dotnet build` succeeding without a file-lock error means the API is not running from that
  output** - or is running a stale assembly. A stack trace naming a line that no longer exists
  is the tell. Check `(Get-NetTCPConnection -LocalPort 5063).OwningProcess` and compare
  `StartTime` to the dll's `LastWriteTime`.
- **Restarting the API invalidates every previously issued token** (dev signing key
  regenerates). A 401 with an **empty body** is authentication; the guard's refusal carries a body.
- **Instruments must log in for themselves.** A token expiring mid-sweep kills a measurement run.

---

# SECTION 8 - BACKLOG STATUS

## 8.1 T-043 - D1 workspace anatomy

**Committed at `e5c2e6cc` (S1-S3). Not closed.**

Done: permanent selections bar with per-chip removal; 5.1.2 region order (selections ->
associative strip -> filter bar -> grid, drill drawer outside the sequence); `WorkspaceHeader`
with sheet selector, UTC as-of, edit toggle, Save/Reset; `isDraggable`/`isResizable` gated on
edit flag defaulting false; sheets on `layout_json.sheets[]` via the T-039 path (Option A -
no new table, no migration, no new endpoint; Chapter 3 endpoint divergence recorded as M2a debt
inside `workspaceSheets.ts`).

**Outstanding:** S4 filter-widget rendering and composition (ruling B - filter widget uses
`DimensionCode` for binding identity, `FilterJson` for permanent scope only, and
`widgetType.startsWith("filter")` not strict equality); Option B auto-assign of a new widget to
the active sheet with auto-save; T-040 G13/G18/G19 re-run; closure evidence.

**Frozen: T-042 must never be reopened.**

## 8.2 T-044 - operational dashboard certification

**Status: IMPLEMENTATION DONE - ENGINE DEPENDENCY DISCHARGED - FINAL QA RECERTIFICATION ONLY.**

Owner of the remaining work is **Worker 1**: `EO_TABLE` browser presentation verification (it
can now execute), the three-page browser walk, closure evidence, QA verdict.

16 widgets: **8 PASS, 8 advisory, 0 FAIL**, all deterministic.

The eight advisories: four KPI widgets persisting a `day` dimension (**now proven to be seed/live
drift, corrected by T-045 Pack A**), and four unattributed date buckets caused by the
null-timestamp predicate.

## 8.3 D1 - aggregate engine truth remediation

**Foundation shipped at `54ee883b`. Not complete.**

Migrated: `observationCount`, `materialCount`.
**Not migrated (still `filter -> Take -> materialise -> C# GroupBy`):** `defectCount`,
`defectRate`, `avgParameterValue`, `maxParameterValue`, `minParameterValue`, `downtimeMinutes`,
`riskScore`, `processStepDuration`, `dataQualityIssueCount`.

Also outstanding: `GetFilteredMaterialIdsAsync` still materialises a capped material-id list at
`AbsoluteRawRowLimit + 1` and feeds it downstream as an `IN` list. Latent at 35,915 of 250,000.
The charter requires keeping that population relational (JOIN/EXISTS/subquery).

Families still needing implementation on the foundation: weighted mean (sufficient statistics),
extremal, ratio-over-distinct-denominator.

## 8.4 T-045 - analytical page certification

**Pack A committed at `6f424969`.** Nine of ten seed/live mismatches converged; deterministic
`FDT_C` presentation parameter with refusal fallback; `rolling.cooling_rate` removed.

**Pack B - specified in full, not built.** See Section 10.

## 8.5 T-046, T-047

Not started. T-046 owns chart grammar and the KPI/dimension contract (**much of which Pack A has
now dissolved**). T-047 owns the final histogram / box / scatter grammar.

## 8.6 T-146

Owns the missing Numeric x Categorical production statistical method. **Explicitly not to be
built inside T-045.**

---

# SECTION 9 - THE DEBT REGISTER

**Committed as `docs/m1/evidence/T-044/T-044_DEBT_REGISTER.md` at `1717ef13`.** Ten findings
with owner, required outcome and re-entry condition. Updated status:

| ID | Finding | Severity | Status |
|---|---|---|---|
| D1 | Aggregate-engine population/truth remediation | Critical | **Partially discharged** - 2 of 11 measures migrated |
| D2 | `observationCount` executable and used but not published to authoring | Medium | open |
| D3 | `MI_SEV` references unregistered `severity` | Medium | open - Pack B must converge it |
| D4 | `riskScore` by day is degenerate (one category) | Low-Med | open - affects `RI_TREND` |
| D5 | `dataQualityIssueCount` by day degenerate | Low | open |
| D6 | `EO_TABLE` customer presentation (raw GUID column in `MiniTable`) | Medium | **unblocked** - it executes now; Worker 1 verifies |
| D7 | Generic chart identity/display separation | High | **CLOSED** at `895fb87a` |
| D8 | Seeder generation drift (same UUIDs, different codes) | Medium | partially closed - v1 retired, invariant added; four scripts still each carry their own copy of 29 rows |
| D9 | Presentation database hygiene - 34 active `PAGE_*` test dashboards + 5 legacy system templates | Medium | open, demonstration risk |
| D10 | QA instrumentation debt | Low | open, **Worker 1 lane** |

**New findings from this session, to be added:**

| Finding | Detail |
|---|---|
| **`risk_scores` has no provenance columns** | No `source_system`, `model_version` or `is_synthetic`. Schema-level gap; no presentation work can recover it. Scoring source is `SCORING_SOURCE_UNKNOWN` by construction. |
| **Cardinality asserting provenance** | `ApplicationReadinessService.cs:524` - `if (e.RiskScoreCount > 0) reasons.Add("Rule-based risk scoring output exists.")`. A row count proves output existence, never origin. |
| **Seven measures still on the broken path** | And T-044 run 4 proved their values change once migrated, even below the cap. |
| **Week semantics are not ISO 8601** | `ceil((dayOfYear + firstDayOfYear.DayOfWeek)/7.0)` with the calendar year. Agrees with ISO on this dataset only because it spans no year boundary. |
| **`defectiveMaterialIds` is uncapped** | `.Distinct().ToListAsync()` with no limit in `ExecuteDefectRateAsync`. Not a truncation risk; an unbounded materialisation. |

**D10 detail (Worker 1):** `Measure-T044-v2.ps1` does not log in for itself and dies mid-sweep
when a token expires. `Measure-EOEQDEF-DowntimePreflight.ps1` truncates its engine output at
eight of nine rows and its section 5 header still claims production impact is "the column the
engine actually sums" - **now false**; the trusted reference is the stopped-minutes column.
`Measure-PublishedMeasureSmoke.ps1` classifies a 400 validation refusal as BROKEN; it must
distinguish EXECUTES / VALIDATION REFUSAL / CONTAINMENT REFUSAL / BROKEN 5xx / EMPTY SUCCESS.

---

# SECTION 10 - T-045 PACK B: FULLY SPECIFIED, NOT BUILT

**Everything below is ruled. No further decisions are needed. Execute it.**

## 10.1 What Pack A already did (`6f424969`)

Five files: four seeders (`Finish-PresentationWorkspace.ps1`, `Insert-Widgets-v4.ps1`,
`Rebuild-PresentationDb.ps1`, `Seed-PresentationDashboards.v2.ps1`) plus
`Backend/database/scripts/800_t045_canonical_widget_definitions.sql`. +98 / -11.

SQL result: `UPDATE 7` (KPIs to dimensionless `''`), `UPDATE 1` (`CF_TOP` back to `defectType`),
`UPDATE 1` (`PA_TABLE` to `gradeOrRecipe` / `avgParameterValue`).

**The KPI-dimension advisory that followed every certification run since T-044 was drift, not a
contract dispute.** All four `PO_KPI_*` rows seed `'kpi' ''`; the live rows carried `day`.
Seven KPI widgets across three dashboards had been mutated in the database.

**`$TopParam` correction.** `Rebuild-PresentationDb.ps1:304-307` derived the presentation
parameter as `ORDER BY COUNT(*) DESC LIMIT 1` with a fallback literal `rolling.cooling_rate`
when the query returned nothing. That fallback is why three Parameter widgets bound a code
existing in no registry and no data - **the seeder ran before observations were loaded and
invented one**. And the ordering had **no tie-break across at least eleven parameters tied at
17,010 rows**, so a rebuild could silently rebind the page. Now: `FDT_C` as explicit preference,
`, pd.parameter_code ASC` tie-break, and a `throw` instead of an invented code.

**Deviation:** nine converged, not ten. `MI_SEV`'s seed binds unregistered `severity`, so
converging live to seed would install a definition the engine refuses. Pack B must handle it.

## 10.2 Pack B design - frozen

**The discriminator.** `DashboardWidgetValidationService` checks only `widgetType`, `chartType`,
`dimensionCode`, `measureCode` and `parameterCode` against
`DashboardWidgetQuerySafetyRegistry`. There is no source or dataset field. **`measureCode` is
therefore the discriminator**: Class 2 registers `findingStatus`, `scoringCoverage` and
`analysisReadiness` as measures, and the dispatcher in `ExecuteAsync` routes on measure code
alone - no widget branch, no page branch.

**The validator must stay generic.** `ChartRequiresDimension(chartType)` keeps its signature.
Add a registry declaration - `MeasureProvidesOwnColumns(measureCode)` - in exactly the shape of
the existing `MeasureRequiresParameterCode`, and have the validator ask the registry a general
question. **Never compare against `findingStatus` or `scoringCoverage` literally inside the
validator.** A future native source is then a registry entry, not a validator edit.

**The seam.**

```
IWidgetResultSource
  AggregateWidgetResultSource      -> WidgetFact -> aggregate executor -> BuildResult   [UNTOUCHED]
  NativeIntelligenceWidgetResultSource -> source-declared columns[] + rows[]            [BYPASSES BOTH]
        both -> the same DashboardWidgetQueryResultDto
```

**Three result families.**

*Readiness.* **Bind `IAnalysisReadinessService`** - `EvaluateAsync(AdvancedAnalysisRequest(OutcomeKey, Grain, WindowDays, TenantId, ...))`
-> `AdvancedReadiness.Evaluate(dataset)` -> canonical `ReadinessGate`. Returns
`AnalysisReadinessDto(Overall, CanRun, Dimensions[Name/State/Reason], OutcomeKey, Grain,
WindowDays, IndependentHeats, OutcomeEvents)`.

**It IS registered and live.** `AddAdvancedAnalysis` is invoked at
`Backend/PlantProcess.Infrastructure/DependencyInjection.cs:134`, registering
`IFeatureVectorLoader -> NpgsqlFeatureVectorLoader` and
`IAnalysisReadinessService -> AnalysisReadinessService`. **I initially reported this path as
dead code - that was wrong, from checking only the Application DI file.**

**Do NOT bind `IMlReadinessService` as DF8 readiness.** It is registered and returns
`MlReadinessScoreDto(GeneratedAtUtc, OverallStatus, ScorePercent, CanStartTraining,
TrainingStatus, HonestPositioning, Metrics, Blockers, NextActions)` - a percentage-score legacy
ML-foundation diagnostic. DF8 readiness is **five dimensions** (independent units, outcome
events, minority-class balance, freshness factor, required-field completeness), each measured
against governed per-tenant thresholds, with the overall state being the **worst dimension,
never an average**. Keep `IMlReadinessService` separately labelled or refactor it to delegate to
the same gate. **One readiness authority.**

Note: `MlReadinessScoreDto` has a `HonestPositioning` field - the product already intends to
state its own limits. Feed that intent; do not let it carry a hardcoded assertion that the
existing rows are rule-based.

*Finding status.* Read `correlation_results` (currently **0 rows**). Zero findings must still
produce a renderable row: **`NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED`**, never
`NO_CORRELATION_EXISTS`. Expose finding count, state, refusal/exclusion reason, last evaluated
time, method/population/evidence where available.

*Scoring coverage.* Read `risk_scores` + `material_units` **live**. Expose `scoringSource`
(measured `SCORING_SOURCE_UNKNOWN`), `modelState` (`MODEL_NOT_READY`), `scoredPopulation`,
`referencePopulation`, `coverageAgainstReference`, `syntheticPopulation`. Only expose
`eligiblePopulation` / `coverageAgainstEligible` when a real eligibility resolver proves that
denominator. **Register as a non-additive ratio.**

**Mark the risk source explicitly as a temporary legacy presentation adapter.** Chapter 3's
final authority is `predictions` + `prediction_current`, and it states that a separate
independent `risk_scores` store does not exist in the target design. No future contract may
depend permanently on it.

**Fix the cardinality-as-provenance defect** at `ApplicationReadinessService.cs:524`.

**Converge `MI_SEV`** - inspect all seeders; if any still binds `severity`, move it to
`materialUnitType`.

## 10.3 Page targets

**Parameter Deep Analysis** - `PA_KAVG` average `FDT_C`; `PA_KOBS` observation population;
`PA_TREND` temporal behaviour; `PA_BYP` observation volume by parameter; `PA_TABLE`
`avgParameterValue` by `gradeOrRecipe` for `FDT_C`. `PA_BYP` and `PA_TABLE` now answer different
questions.

**Correlation / Statistical Findings** - live findings when available; otherwise the dynamic
no-supported-findings / readiness / exclusion state. **An exclusion is not a finding. Zero
findings is an acceptable truthful state.** Never restore `defectRate` or `defectCount` under a
correlation title.

**Risk Intelligence** - KPI on scored coverage or clearly-labelled average over the scored
population; average risk score by material unit type (three real categories); coverage by type
with the denominator correctly named; table of type / scored / denominator / coverage / average.
**No one-point trend. No fake risk-class distribution. No equipment attribution.**

**Model Insights** - the five DF8 readiness dimensions with measured value, threshold, unit,
state, reason code, reason text, population evidence, evaluated timestamp. **No prediction
curve. No relabelled defect data.** Model Insights stays in the product as the **technical
backup page**; the six primary presentation pages are Production Overview, Quality Monitoring,
Equipment and Operations, Parameter Deep Analysis, Correlation, Risk Intelligence.

## 10.4 Pack B acceptance

Class-1 widgets byte-compatible; D1 probes still green; Class-2 rich rows through the same public
envelope; Class 2 does not pass through `WidgetFact`/`BuildResult`; no readiness or coverage
value is a seed literal; a readiness change appears without reseeding; a scoring rerun changes
coverage without reseeding; zero findings still renders; denominator semantics explicit; column
roles bound **by name, never by index**; no ML-specific visual widget kind; no page/widget/Fleet
branch in generic execution; clean rebuild reproduces all T-045 definitions; browser walk proves
all four pages.

## 10.5 Files the next session will need pasted in its first message

```powershell
Get-Content Backend\PlantProcess.Application\Dashboarding\Services\Queries\DashboardWidgetQueryService.cs -Raw
Get-Content Backend\PlantProcess.Application\Dashboarding\Services\Widgets\DashboardWidgetQuerySafetyRegistry.cs -Raw
```

`DashboardWidgetValidationService.cs` and `DependencyInjection.cs` are already transcribed into
this handover in substance; the two above are needed **verbatim** because Pack B edits them and
anchors must match on-disk text exactly.

---

# SECTION 11 - DEPLOYMENT, SERVER AND PIPELINE

**Read this section carefully: it is the one place where I have almost nothing to report, and
saying so is the honest answer.**

## 11.1 What this session did NOT do

**No pipeline work. No deployment work. No server work. No CI configuration was changed, and no
pipeline was made green.** Requests 9 and 10 of the handover brief cannot be answered with
session evidence because none was produced. Everything below is **inherited state** from
`PPIQ_Implementation_Review_27Jul2026.md` and the standing repository findings, not measured
here.

Everything this session touched ran **locally**: `C:\Workspace\PlantProcess-IQ`, the API on
`http://localhost:5063` with `-Profile presentation`, and PostgreSQL `ppiq_presentation` on
`127.0.0.1:5432`. This is a laptop, not a server.

## 11.2 Recorded pipeline state (27 July, unverified since)

- **`validate-real-ui-gates.cjs` is invoked by nothing and would fail if it were.** The guard
  that forbids `--list` requires three `npm test` commands in the Jenkinsfile; the Jenkinsfile
  has none. Two suites run in no pipeline at all.
- **`post-deploy-smoke.sh` says "run by Jenkins stage 5b". There is no stage 5b.** Nothing
  verifies the public HTTPS surface after a deploy.
- **`STATIC_AUDIT.md` carries 5 CRITICAL and 4 HIGH and has never been read**, because the
  script exits 0. A gate that always passes is not a gate.
- **The reverse-proxy config references stale targets and its source file was deleted.** The
  committed Caddyfile does not match the URLs in use. Recorded advice: do not recreate until a
  persistent config exists.
- A `phase56` script still patches `--list` into the Jenkinsfile.
- Scanner self-match: `Get-AuditSignalsForContent` has no path exclusion, flagged across five
  consecutive dumps.

## 11.3 Recorded pipeline strengths

- Two architecture tests parse the Jenkinsfile inside `dotnet test`: no `--list`, no
  `catchError` forcing success, tests textually before deploy, e2e stage cannot be gated off.
  The needles are assembled from fragments so a scanner cannot match the guard forbidding them.
- Health-gated deploy with rollback: every image tagged `:previous`, a 45x2s health probe from
  **inside** the network, automatic retag and redeploy on failure.
- Two compose projects permanently separated, because a shared project name once let orphan
  removal reap the CI and proxy containers mid-deploy. The fix is structural, not procedural.
- `POSTGRES_PASSWORD` bound to the volume's first init, so regeneration harvests it rather than
  rotating it. The reasoning is written into the script.
- Ed25519-signed licence tokens with RLS-forced tables; the dev key registered only when
  `PPIQ_PRESENTATION=on`.

## 11.4 What must happen before any pipeline claim is made

The **gate-wiring order rule** is frozen: `inventory -> execute directly -> assess validity ->
repair or retire -> then wire only the instruments proven valid`. **Never wire gates in bulk.**
A stale or broken gate must not be wired permanently just because it already exists. Adding new
gates to a pipeline whose existing gates are never invoked produces additional gates that always
pass - that is not an improvement.

**Do not report the pipeline as green until each gate has been executed directly and shown to
be capable of failing.**

---

# SECTION 12 - REALIZATION SCOREBOARD

Baselines are from the 27 July review. **Movement is my assessment against measured evidence
from this session, not a re-scored survey**, and it is stated as approximate on purpose.

| Viewpoint | 27-Jul | Now (approx) | Why |
|---|---:|---|---|
| Plant / process engineer | 68 | **~78** | Sixteen operational widgets truthful and deterministic; two semantic redesigns onto defensible questions; charts show names instead of GUIDs |
| Data / BI engineer | 60 | **~72** | The aggregate engine computes over the authorised population for two measures; one dimension authority; five aggregation families documented; deterministic ordering on canonical keys |
| Buyer / CEO | 48 | **~50** | Little narrative work; the honest-refusal story is a stronger asset than it was |
| IT / security | 50 | **50** | Untouched |
| Infrastructure | 45 | **45** | Untouched. **No pipeline or deployment work was done** |
| **Headline (lowest)** | **45** | **45** | Infrastructure remains the floor |
| **Demo-scope (excl. infra)** | **60** | **~72** | |

**The honest reading.** The number that decides a technical meeting is the process engineer, and
it moved for one reason: **the widgets stopped lying**. The buyer number has still not moved and
no code will move it - that is the cut register, the rehearsal and the deck.

## 12.1 What is genuinely strong now

- **Truth-first architecture with proof.** Not a claim; there is an evidence file recording
  301,560 vs 50,000 and a containment barrier that refuses rather than approximating.
- **The generic aggregation foundation**, with a genericity guard that fails the build on any
  dashboard, widget or industry literal, and a non-seeded runtime proof.
- **Convergence discipline.** `source truth = rebuild truth = live truth`, with a same-UUID
  invariant that has already caught a producer nobody knew existed.
- **Refusal as a first-class product state**: 422 `aggregate_population_limit_exceeded`, named
  dimension refusal, named parameter refusal, validation refusal.
- **Identity and display are separate concepts** across the whole chart layer.

## 12.2 What is genuinely weak

- **Seven of eleven measures still compute on the broken path.** This is the largest single
  correctness gap in the product.
- **Infrastructure has never been measured.** Sizing is estimates, the 100-job claim untested,
  backup/restore never drilled.
- **Existing CI gates are not invoked**, and one would fail if it were.
- **Presentation database hygiene**: 34 test dashboards and 5 legacy system templates a customer
  could navigate into.
- **`risk_scores` records no provenance**, so the product cannot say what produced its own risk
  numbers.
- **Layer B does not exist.** Model Insights must say so, professionally.

## 12.3 Suggested order of attack

1. **Migrate the remaining measures onto the D1 foundation**, family by family, with the same
   trusted-SQL comparison per family. The weighted-mean family is next in value.
2. **T-045 Pack B**, which is fully specified and needs no decisions.
3. **Presentation database hygiene** - highest value per hour before any customer demonstration.
4. **The `GetFilteredMaterialIdsAsync` relational rework**, which removes the last latent cliff.
5. **Pipeline gate inventory** - execute each gate directly before wiring anything.

---

# SECTION 13 - REPOSITORY AND TOOLING FACTS

- Repository: `C:\Workspace\PlantProcess-IQ`. Local laptop.
- API: always `-Profile presentation`; credentials `env\profiles\presentation.env`. Port 5063.
- Database: `ppiq_presentation`, `127.0.0.1:5432`, user `ppiq_dev`. Staging-class data lives in
  `dump_store` (nine tables). **There is no schema named `staging`.**
- Login for instruments: `POST /auth/login` as `e2eadmin` / `E2EAdmin123!`.
- Real-PostgreSQL integration tests read `PPIQ_TEST_PG_CONNSTRING`.
- Packs live in `tools/packs/`; backups are written as `tools/packs/_backup_<name>_<stamp>/`.
- Numbered SQL corrections: `Backend/database/scripts/`. This session added **790** (T-044
  convergence) and **800** (T-045 Pack A convergence).
- Evidence: `docs/m1/evidence/T-044/` now holds `A1_aggregate_truth.md`,
  `A2_aggregation_algebra.md` and `T-044_DEBT_REGISTER.md`.
- Retirement note: `scripts/demo/RETIRED_Seed-PresentationDashboards.md`.

**Active authoritative presentation writers (four):** `Rebuild-PresentationDb.ps1` (the rebuild
path), `Seed-PresentationDashboards.v2.ps1`, `Insert-Widgets-v4.ps1`,
`Finish-PresentationWorkspace.ps1`. The v1 `Seed-PresentationDashboards.ps1` is **retired and
deleted**; recovery is git history.

**Instruments in `tools/packs/`:** `Measure-T044-v2.ps1` (the certification instrument, takes
`-DashboardCodes`), `Measure-A1-AggregateTruth.ps1`, `Measure-TrackB-EquipmentAttribution-v3.ps1`,
`Measure-EOEQDEF-DowntimePreflight.ps1`, `Measure-PublishedMeasureSmoke.ps1`.

---

# SECTION 14 - CLOSING NOTE ON HOW TO WORK HERE

Three habits earned their place this session and should survive it.

**Name your own defects before they are found.** Nearly every red round in this session was my
error, and each was reported as mine with its cause. The instrument that reported a total as
`51` was mine. The guard that refused its own pack was mine, twice. The variance ranking I
asserted without computing was mine. That accounting is not ceremony - it is what stops the same
class recurring, and several of Section 7's entries exist only because the cause was named
rather than worked around.

**A measurement beats an inference every time.** "Rule-based scoring exists because a rule
scorer exists in the codebase" is exactly the reasoning the product itself was doing at
`ApplicationReadinessService.cs:524`, and it is wrong for the same reason.

**Refusal is a feature.** The most valuable artefact produced this session is not a chart. It is
a 422 that says the engine could not compute the answer faithfully and therefore did not
pretend to.
