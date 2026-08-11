# T-044 DISCOVERED DEBT / DEPENDENCY REGISTER

**11 August 2026. Carry-forward only. No new investigation was performed to write this.**

Every item below was PROVEN during T-044 with evidence already in the repository
or in this register's references. None is being fixed inside T-044.

Governing rule this register serves: discovery without disposition is waste.

---

## D1 - Aggregate-engine population and truth remediation

**Severity:** Critical. Systemic.
**Blocks T-044:** YES. This is the only blocker.

Every aggregate measure executes as `filter -> Take(RawRowLimit) -> materialise ->
group in C# memory`. The cap is applied to raw fact rows BEFORE aggregation, so
any aggregate over a population larger than the cap is a lower bound. Measured:
trusted `observationCount` 301,560 against 50,000 returned, 251,560 missing,
83.4 percent. `GetFilteredMaterialIdsAsync` caps the material id list at 250,000
and feeds it downstream, so a cap there removes materials from every widget.

Three further defects in the same execution path, all proven:
the time window is applied in memory AFTER the cap, so a narrower window is a
more wrong answer; rows with a null timestamp satisfy every window predicate and
group under `unknown`; the aggregate sort tie-breaks on the display label rather
than the canonical key.

**Evidence:** `docs/m1/evidence/T-044/A1_aggregate_truth.md`,
`docs/m1/evidence/T-044/A2_aggregation_algebra.md` (five aggregation families,
sufficient statistics and merge rules), commit `c8609735` (containment).

**Current behaviour:** truthful containment refusal. HTTP 422,
`aggregate_population_limit_exceeded`, no partial value returned.

**Affected, proven:** `PO_KPI_OBS`, `EO_OBS`, `EO_TABLE`, `processStepDuration`.

**Required outcome:** aggregate over the authorised population in PostgreSQL
before any group or result limiting, with mathematically correct treatment per
aggregation family (additive, extremal, weighted mean, distinct count, ratio
over a distinct denominator), deterministic ordering with a canonical-key tie
break, and `MaxRows` applied to aggregated groups only.

**Owner:** Worker 2 / implementation backlog.
**Existing task:** none.
**Proposed task:** `M1-Ex AGGREGATE ENGINE TRUTH REMEDIATION`, Critical, scoped
by A2's slices A2 to A5. Estimated 20 to 40 hours. Not to be absorbed by any
certification task.
**Milestone impact:** M1. Any dashboard on a population above the cap is
refused until it lands.
**T-044 re-entry condition:** the three widgets above execute truthfully.

---

## D2 - observationCount registry drift

**Severity:** Medium. **Blocks T-044:** no.

`observationCount` is in `DashboardMetadataCodes.Measures`, in
`ExecutableMeasures`, and bound by `PO_KPI_OBS` and `EO_OBS` today. The metadata
endpoint publishes ten measures and this is not among them, so a measure two
live widgets depend on is invisible to the authoring panel.

**Evidence:** smoke pass, 11-Aug: published list is `materialCount, defectCount,
defectRate, avgParameterValue, maxParameterValue, minParameterValue,
downtimeMinutes, riskScore, processStepDuration, dataQualityIssueCount`.

**Required outcome:** one authority wins. Either publish it or stop treating it
as executable. Do not add it to the registry without a product reason.
**Owner:** metadata/registry contract owner.
**Proposed task:** `REGISTRY CONTRACT RECONCILIATION - published vs executable
measures`.

---

## D3 - MI_SEV references the unregistered `severity` dimension

**Severity:** Medium. **Blocks T-044:** no.

`DashboardMetadataCodes.Dimensions` publishes fourteen codes and `severity` is
not one of them. `QM_SEV` was corrected to `defectType` by T-044. `MI_SEV`
("Predicted Severity Mix", donut) still binds `severity` and is untouched by
ruling.

**Required outcome:** either a product reason for `severity` to become a
supported reusable dimension, or `MI_SEV` moved to a registered one. Do not add
`severity` merely to preserve a seed.
**Owner:** metadata/registry contract owner.
**Proposed task:** same as D2, or a sibling.

---

## D4 - riskScore by day is degenerate

**Severity:** Low to Medium. **Blocks T-044:** no.

`riskScore` grouped by `day` returns ONE category from 500 risk scores.
**Affected widget:** `RI_TREND` on the Risk Intelligence dashboard would draw a
one-point line.
**Required outcome:** a defensible dimension for the risk population, chosen
from measured truth, or acceptance with evidence.
**Owner:** Worker 2, under T-045 (Risk Intelligence is a T-045 dashboard).

---

## D5 - dataQualityIssueCount by day is degenerate

**Severity:** Low. **Blocks T-044:** no.

Returns ONE category. The population is 7 rows.
**Affected:** the Data Quality dashboard (`DATA_QUALITY`, system template).
**Required outcome:** accept with evidence as a genuinely small population, or
rebind. Not a defect of the engine.
**Owner:** Worker 2, presentation-readiness pass.

---

## D6 - EO_TABLE customer presentation

**Severity:** Medium. **Blocks T-044:** yes, but behind D1.

Query-layer labels resolve correctly. The table path renders
`<MiniTable rows={rows} />`, and `MiniTable` renders `Object.keys(rows[0])`, so
a customer would see the raw `equipment` UUID column beside `dimensionLabel`,
plus internal helper columns. NOT verified in a browser, because `EO_TABLE`
currently refuses under D1 and inventing a smaller dataset to inspect it was
ruled out.

**Required outcome:** internal identity columns remain available for selection
but are not rendered as ordinary business columns.
**Owner:** Worker 2.
**T-044 re-entry condition:** after D1, inspect and correct if proven.

---

## D7 - Generic chart identity/display separation

**Severity:** High. **Blocks T-044:** yes, and it is the ONE remaining permitted
T-044 implementation change.

`categoryKey` carries both responsibilities. `InteractiveCharts.tsx` binds
`XAxis dataKey={categoryKey}` (125, 267, 289) and `nameKey={categoryKey}` (192),
so `EO_EQDEF` plots nine bars labelled with equipment UUIDs. `ChartExtras.tsx`
uses the same `cat` for display AND for `setFilter(field, cat)`, so a blind
substitution would have fixed the visible chart and silently broken filtering.

**Required outcome:** identity key and display key as two concepts; display uses
`dimensionLabel` with fallback to identity; selection continues to carry the
canonical value; no widget-code branch.
**Owner:** Worker 2. Timeboxed to approximately 60 minutes.
**Status:** specified and approved, not yet implemented.

---

## D8 - Seeder generation drift

**Severity:** Medium. **Blocks T-044:** no. Partially closed.

The retired v1 seeder wrote the same widget UUIDs under different codes, in
thirteen cases. It is retired (commit `5e1929bd`) and a same-UUID invariant now
runs in the convergence proof. The class remains: four active scripts each carry
their own copy of the same twenty-nine widget rows, and only that proof keeps
them equal.

**Evidence:** `scripts/demo/RETIRED_Seed-PresentationDashboards.md`.
**Required outcome:** one authority for presentation widget definitions.
**Owner:** Worker 2, presentation tooling.

---

## D9 - Presentation database hygiene

**Severity:** Medium for the demonstration. **Blocks T-044:** no.

`ppiq_presentation` carries 34 active `PAGE_*` dashboards from page-builder test
runs ("Draft only mslgnrs1", "Shift production mslmcyp8"), 16 of them with zero
widgets, plus 5 active system-template dashboards overlapping the current seven.
A customer can navigate into test debris or the wrong dashboard generation.

**Required outcome:** the demonstration database contains only intended
dashboards.
**Owner:** Worker 2, before final presentation certification.

---

## D10 - QA instrumentation debt

**Severity:** Low. **Blocks T-044:** no. **Owner: Worker 1, QA/QC lane.**

`Measure-T044-v2.ps1` does not log in for itself and dies mid-sweep when a token
expires. `Measure-EOEQDEF-DowntimePreflight.ps1` truncates its engine output at
eight of nine rows and its section 5 header still says production impact is "the
column the engine actually sums", which is now false. `Measure-PublishedMeasureSmoke.ps1`
classifies a 400 validation refusal as BROKEN; it must distinguish EXECUTES,
VALIDATION REFUSAL, CONTAINMENT REFUSAL, BROKEN 5xx and EMPTY SUCCESS.

---

## Also recorded, no action proposed

`CS8629 Nullable value type may be null` at `DashboardWidgetQueryService.cs:559`,
present in every build of that project. Not introduced by T-044 work.

---

## T-044 status

**T-044 IMPLEMENTATION COMPLETE - BLOCKED / READY FOR RECERTIFICATION**, once D7
lands.

16 widgets measured: **6 PASS, 7 advisory, 3 engine-blocked.**

Commits: `5e1929bd` convergence and v1 seeder retirement, `7b8d6e8e`
downtimeMinutes translation and semantics, `c8609735` containment, `6ba204c1`
A1 evidence, `cfd273cc` A2 algebra audit.

The seven advisories: four KPI widgets persist a `day` dimension the registry
says KPI does not support (owned by T-046); three unattributed date buckets
beside valid populations, caused by the null-timestamp predicate recorded in D1.
None breaks a customer-facing dashboard.
