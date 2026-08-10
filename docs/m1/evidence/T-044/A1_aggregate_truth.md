# T-044 / A1 - AGGREGATE TRUTH CHARACTERISATION

**DATA-TRUTH ARCHITECTURE DEFECT: aggregate-after-cap / population truncation**

This file is the BEFORE evidence. It records what the engine returned prior to
any remediation, so that A5 can later prove this state has been eliminated
rather than merely reported as fixed. It contains no fix and no source change.

---

## 1. Provenance

| Field | Value |
|---|---|
| Measured | 2026-08-10, approximately 13:36 to 13:52 UTC+2, on the development laptop |
| Repository | `C:\Workspace\PlantProcess-IQ` |
| HEAD at measurement | `e5c2e6cc` - "T-043 S1-S3: permanent selections bar with per-chip removal, 5.1.2 region order, page header with sheet seam and as-of, explicit edit mode gating drag resize and add widget, sheets on layout_json through the T-039 path" |
| Working tree | Clean for `Frontend/PlantProcess.Web`. No backend source modified. No product code changed by this measurement |
| Environment | API on profile `presentation`, `http://localhost:5063` |
| Database | `ppiq_presentation` on `127.0.0.1:5432`, confirmed by `SELECT current_database()` |
| Instrument | `tools/packs/Measure-A1-AggregateTruth.ps1`, read only |
| Authentication | `POST /auth/login` as `e2eadmin` |

**Instrument correction recorded for honesty.** The first execution of this
harness reported the trusted total as `51` and the database name as `p`. Both
were the same defect in the instrument, not in the product: `Invoke-Sql` ended
with `return @(...)`, and PowerShell unwraps a one-element array on return, so
a scalar query returned a bare string and `[0]` indexed its first CHARACTER.
`[int]'3'` is 51. The harness now returns `,$rows` and routes single-value
queries through a `Get-Scalar` helper. Every number below is from the corrected
run.

---

## 2. The caps under audit

Declared in `DashboardWidgetQuerySafetyRegistry`:

| Constant | Value | Where it is applied |
|---|---:|---|
| `DefaultMaxRows` | 100 | rows returned to the client, after aggregation |
| `AbsoluteMaxRows` | 500 | ceiling on the above |
| `DefaultRawRowLimit` | **50,000** | `.Take(resolved.RawRowLimit)` on the raw fact query, **before** aggregation |
| `AbsoluteRawRowLimit` | **250,000** | `.Take(...)` on the material id list in `GetFilteredMaterialIdsAsync`, which every measure filters through |

`MaxRows` is legitimate: it limits presentation after the mathematics is done.
The other two limit the mathematics itself.

---

## 3. Population census versus the caps

Measured directly against `ppiq_presentation`, `is_deleted = false`:

| Population | Rows | Cap | State |
|---|---:|---:|---|
| `parameter_observations` (observationCount, parameter aggregates) | **301,560** | 50,000 | **TRUNCATED, 6x over** |
| `process_step_executions` (materialCount by equipment/shift/area, processStepDuration) | **53,095** | 50,000 | **TRUNCATED** |
| `material_units` (the id list every measure filters through) | 35,915 | 250,000 | under the cap today |
| `quality_events` (defectCount, defectRate) | 7,844 | 50,000 | under the cap today |
| `risk_scores` (riskScore) | 500 | 50,000 | under the cap today |
| `data_quality_issues` (dataQualityIssueCount) | 7 | 50,000 | under the cap today |

Two populations already exceed their cap on this dataset. The rest are not
correct-by-design; they are correct-by-accident of size, and cross the same
cliff on a larger customer dataset.

---

## 4. Trusted reference

Computed by PostgreSQL over the whole population, with the same joins and
predicates as `ExecuteObservationCountAsync`, and no cap:

```sql
SELECT count(*)
FROM parameter_observations o
JOIN material_units m ON m.id = o.material_unit_id
JOIN parameter_definitions p ON p.id = o.parameter_definition_id
WHERE o.is_deleted = false AND m.is_deleted = false;
```

**Trusted observation total: 301,560**

Distinct group counts over the same population:

```sql
-- day
SELECT count(*) FROM (
  SELECT to_char(o.observed_at_utc, 'YYYY-MM-DD') AS k
  FROM parameter_observations o
  JOIN material_units m ON m.id = o.material_unit_id
  JOIN parameter_definitions p ON p.id = o.parameter_definition_id
  WHERE o.is_deleted = false AND m.is_deleted = false
  GROUP BY 1) d;
-- month uses 'YYYY-MM', ISO week uses 'IYYY-IW'
```

| Grain | Trusted distinct groups |
|---|---:|
| day | 97 |
| ISO week | 15 |
| month | 4 |

---

## 5. The engine, five identical requests per grain

Exact request, repeated without modification:

```json
{"parameterCode":null,"measureCode":"observationCount","widgetType":"chart","dimensionCode":"day","chartType":"bar"}
```

`dimensionCode` varied across `day`, `week`, `month`. No page-level selection
was applied, so each run is the same request by construction. The resolved
window (`fromUtc`, `toUtc`) was null and identical on every run: nothing about
the request or its resolution moved between executions.

| Grain | Trusted groups | Engine groups, five runs | Trusted total | Engine total, every run | Missing |
|---|---:|---|---:|---:|---:|
| day | 97 | 96, 79, 36, 36, 73 | 301,560 | 50,000 | 251,560 |
| week | 15 | 15, 15, 14, 6, 6 | 301,560 | 50,000 | 251,560 |
| month | 4 | 4, 4, 4, 4, 2 | 301,560 | 50,000 | 251,560 |

**Missing: 251,560 of 301,560, or 83.4 percent.**

One month-grain run returned two groups where four exist: half the year absent
from a chart that gave no indication anything was missing.

**The displayed number is the safety limit.** `observationCount` counts rows,
so summing a capped 50,000-row sample yields exactly 50,000 every time. The
widget was not reporting a measurement of the plant. It was reporting
`DefaultRawRowLimit` with a chart drawn around it.

---

## 6. Earlier per-widget evidence from the same defect

From the T-044 v2 certification instrument, five identical runs per widget:

| Widget | Measure | Behaviour |
|---|---|---|
| `PO_KPI_OBS` | observationCount | row counts 47, 96, 96, 92, 50; four distinct key fingerprints |
| `EO_OBS` | observationCount | row counts 6, 8, 15, 15, 14; four distinct key fingerprints |
| `EO_TABLE` | materialCount by equipment | stable 9 rows on all five runs, and **still truncated**: it reads `process_step_executions` at 53,095 rows against the 50,000 cap |

`EO_TABLE` is the most dangerous of the three. The cap lands on the same nine
equipment groups every time, so the categories are stable and the widget looks
correct, while the counts underneath them are computed from 94 percent of the
rows. Nothing in its behaviour reveals the defect. It was certified PASS by the
instrument before the census was taken.

For measures that count, the failure is at least detectable, because the result
equals the cap. For measures that average - `parameterAggregate`, `riskScore`,
`processStepDuration` - a capped sample yields a plausible average with nothing
to notice at all.

---

## 7. Root cause

`DashboardWidgetQueryService` executes every aggregate measure in this order:

```
filter -> project to WidgetFact -> .Take(RawRowLimit) -> ToListAsync -> GroupBy in C# memory -> aggregate
```

The cap is applied to the RAW FACT ROWS, before grouping. The aggregate is
therefore computed over an arbitrary subset of the population rather than over
the population. This is the execution model of every measure method in the
service, not a defect in one of them:

`ExecuteMaterialCountAsync`, `ExecuteDefectCountAsync`, `ExecuteDefectRateAsync`,
`ExecuteObservationCountAsync`, `ExecuteParameterAggregateAsync`,
`ExecuteDowntimeMinutesAsync`, `ExecuteRiskScoreAsync`,
`ExecuteProcessStepDurationAsync`, `ExecuteDataQualityIssueCountAsync`.

Upstream of all of them, `GetFilteredMaterialIdsAsync` materialises the material
id list with `.Take(AbsoluteRawRowLimit)` and hands it downstream as an `IN`
list, so a cap there silently removes materials from every widget on every
dashboard before any measure begins.

There is no class of legitimate raw-detail operation among these call sites.
Every one of them feeds an aggregate.

---

## 8. Why adding ORDER BY alone would not correct this

The absence of `ORDER BY` before `.Take()` explains only the INSTABILITY. In
PostgreSQL a `LIMIT` without `ORDER BY` may return any rows, so the sample
changes between executions and the same request answers differently.

Adding `ORDER BY` would make the engine return the SAME 50,000 rows every time.
The result would become reproducible and would still be missing 251,560
observations. The instability would disappear and the wrong answer would remain,
now with the appearance of reliability.

The defect is the cap before the aggregate. Determinism is a separate and
lesser property, and fixing it first would remove the only symptom by which this
defect announced itself.

---

## 9. Standing principle established by this finding

A resource limit may never silently redefine the mathematical population of an
aggregate. If a governed limit prevents PPIQ from computing an aggregate
faithfully, the permitted result is an explicit refusal carrying its reason. A
plausible partial number presented as complete truth is not permitted.

---

## 10. What A5 must prove has been eliminated

1. Trusted total 301,560 returned for `observationCount` with no cap effect.
2. Five identical executions returning identical row counts, identical key
   fingerprints, identical value fingerprints and identical ordering.
3. No population loss at any grain: 97 day groups, 15 ISO week groups, 4 month
   groups.
4. The same proof above the former 250,000 material boundary, using a controlled
   fixture, since `material_units` is only 35,915 today and that cliff is latent
   rather than active.
5. A distinct-count measure proved not to double count an entity appearing on
   several days when aggregated to week or month.
6. Performance captured: generated SQL, round trips, duration, plan on the large
   population.

Only after A5 is green may the sixteen-widget T-044 certification be rerun. The
current T-044 results are pre-remediation evidence and must not be reused as
post-fix evidence.