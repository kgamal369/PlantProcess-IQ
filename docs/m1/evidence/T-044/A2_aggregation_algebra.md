# T-044 / A2 - AGGREGATION ALGEBRA AUDIT

**Evidence only. No product code changed by this document.**

Ruling of 10-Aug: produce `measure -> aggregation family -> sufficient
statistics -> merge rule -> requested-grain requirement -> day-foldable` before
writing any A2 foundation code, and let the audit decide the minimum correct
number of families rather than asserting it in advance.

It decided five, not the three I proposed. Two of my earlier classifications
were wrong and are corrected below.

| Field | Value |
|---|---|
| Derived from | `Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs`, read method by method |
| HEAD | `6ba204c1` (A1 evidence commit) |
| Registry authority | `DashboardMetadataCodes.Measures`, eleven codes, all eleven present in `ExecutableMeasures` |

---

## 1. The audit table

| Measure | Current aggregation in code | Family | Sufficient statistics | Merge rule | Must execute at requested grain | Day-foldable |
|---|---|---|---|---|---|---|
| `materialCount` (non-relational dimensions) | `AggregateCount`, `g.Count()` over material rows | **Additive** | count | `SUM(count)` | no | **yes** |
| `materialCount` (equipment, shift, area) | `GroupBy(dimension, MaterialUnitId).Select(g.First())` then `AggregateCount` | **Distinct count** | the distinct material key SET | none: sets do not sum | **YES** | **no** |
| `defectCount` | `AggregateCount`, `g.Count()` over quality events | **Additive** | count | `SUM(count)` | no | **yes** |
| `observationCount` | `g.Count()` over observations | **Additive** | count | `SUM(count)` | no | **yes** |
| `dataQualityIssueCount` | `AggregateCount` | **Additive** | count | `SUM(count)` | no | **yes** |
| `downtimeMinutes` | `AggregateSum` over minute values | **Additive** | sum | `SUM(sum)` | no | **yes** |
| `avgParameterValue` | `g.Average(x => x.Value)` | **Weighted mean** | sum, count of contributing rows | `SUM(sum) / SUM(count)` | no | **yes**, only if both statistics travel |
| `riskScore` | `g.Average(x => x.Value)` | **Weighted mean** | sum, count | `SUM(sum) / SUM(count)` | no | **yes**, same condition |
| `processStepDuration` | `g.Average(x => x.Value)` | **Weighted mean** | sum, count | `SUM(sum) / SUM(count)` | no | **yes**, same condition |
| `maxParameterValue` | `g.Max(x => x.Value)` | **Extremal** | max | `MAX(max)` | no | **yes** |
| `minParameterValue` | `g.Min(x => x.Value)` | **Extremal** | min | `MIN(min)` | no | **yes** |
| `defectRate` | `defects / Select(MaterialUnitId).Distinct().Count()` | **Ratio over a distinct denominator** | numerator count, denominator distinct SET | none | **YES** | **no** |

### The five families

1. **Additive** - count, sum. Merge by summation.
2. **Extremal** - min, max. Merge by min/max. A semilattice, so it folds from any
   partition, including a day partition, with no loss.
3. **Weighted mean** - average. Sufficient statistics are sum AND the count of
   contributing rows. Merge is `SUM(sum) / SUM(count)`. **Averaging averages is
   forbidden** and is what a naive day fold would do.
4. **Distinct count** - `materialCount` on a relational dimension.
5. **Ratio over a distinct denominator** - `defectRate`.

Families 4 and 5 cannot fold from a day partition. A material observed on
Monday and again on Tuesday is ONE material in that week; summing two daily
distinct counts returns two. Both must execute `COUNT(DISTINCT ...)` in
PostgreSQL **at the grain the user asked for**.

### Corrections to my earlier classification

**I called `materialCount` additive. It is not, on three of the fourteen
dimensions.** When the dimension is Equipment, ShiftCode or Area,
`ExecuteMaterialCountAsync` reads `process_step_executions` and deliberately
de-duplicates so a material passing an equipment twice counts once. That is a
distinct count wearing a count's name, and `EO_TABLE` is bound to exactly this
path. Folding it at day grain would have silently double counted every material
that spans midnight.

**I said "three shapes". It is five.** The extremal family is genuinely separate
and must not be forced through the average path, as the ruling anticipated.

---

## 2. Three defects found while deriving the table

These are not part of the algebra. They were found by reading the same methods
and are recorded here because they compound the aggregate-after-cap defect.

### 2.1 The time window is applied AFTER the cap, in memory

`ApplyFactDateFilter` is called on the materialised list, after
`.Take(RawRowLimit)`:

```
filter -> project -> Take(50,000) -> ToListAsync -> ApplyFactDateFilter -> GroupBy
```

So a request for one week of data does not fetch that week. It fetches an
arbitrary 50,000 rows from the whole population and then keeps whichever of them
happen to fall in the week. On the 301,560-row observation population, a
one-week window would return roughly the fraction of an arbitrary sixth of the
data that lands in that week. **The narrower the window, the more wrong the
answer**, which is the opposite of what a reader would assume.

Every A1 measurement was taken with no window, so this defect is not visible in
the A1 evidence. It is strictly worse than what A1 recorded.

### 2.2 Rows with no timestamp pass every time filter

```csharp
result = result.Where(x => !x.EventTimeUtc.HasValue || x.EventTimeUtc >= filters.FromUtc.Value);
```

A fact with a null `EventTimeUtc` satisfies both the from and the to predicate,
so undated facts are admitted into **every** window, including windows that
exclude the entire dataset. They then group under the `unknown` key.

This explains the seven "unattributed bucket" advisories in the T-044 v2
certification: the single `unknown` day, week and month bucket beside 94 real
categories is not merely missing data, it is undated rows deliberately admitted
to every period. Whether that is intended is a product ruling, not a defect
finding, and it is recorded rather than judged.

### 2.3 The aggregate sort tie-breaks on the display label, not the key

```csharp
rows.OrderByDescending(x => x.Value).ThenBy(x => x.DimensionLabel)
```

Two consequences. Ordering is a function of PRESENTATION, so renaming a piece of
equipment reorders a chart although no measurement changed. And the tie-break is
not total: two groups sharing a value AND a label - which the `No equipment`
fallback label makes reachable - fall back to incidental order. The ruling
requires a stable secondary ordering on the canonical group key; the current
code orders on the label instead.

---

## 3. What this means for the A2 foundation

1. The projection authority maps `dimension code -> translatable grouping key`,
   and must expose whether a dimension is temporal, because the day-fold path is
   available only for temporal dimensions and only for families 1, 2 and 3.
2. The aggregation foundation carries **sufficient statistics**, not values:
   every group returns sum, count, min and max as appropriate, and the scalar the
   widget displays is derived at the end. This is what makes the weighted mean
   correct and makes averaging averages impossible by construction.
3. Families 4 and 5 bypass the fold entirely and group in SQL at the requested
   grain. They are the reason the foundation cannot be a single code path, and
   the reason `EO_TABLE` and `defectRate` need their own proof cases.
4. Ordering moves to value then canonical key, with the label applied only during
   enrichment.
5. The date window moves into the relational query, before aggregation, and the
   null-timestamp policy is made explicit rather than implicit in a predicate.

---

## 4. Proof cases this table demands

| Case | Why |
|---|---|
| Weighted mean with null values | denominator must count contributing rows only, not all rows |
| Weighted mean folded from day to month | must equal the direct month aggregate exactly |
| Extremal folded from day to month | must equal the direct month aggregate exactly |
| Distinct count, entity present on several days, requested at week | must NOT double count |
| `defectRate` at week grain | denominator must be distinct materials in the week, not the sum of daily distinct counts |
| Any additive measure above 50,000 rows | must equal the trusted total, not the cap |
| Narrow time window on a large population | must return the window, not the window's intersection with an arbitrary sample |
| Two groups with equal value and equal label | order must still be deterministic |