# Upstream finding: widget result instability across repeated executions

**Raised by:** Worker 1, during T-073 certification
**Owner:** Worker 2 (dashboard definitions, widget bindings, query path)
**Status:** recorded, not acted on. Nothing was changed in the query path, no
widget code is special-cased anywhere in assistant code, and Fleet v2 was not
touched.

---

## What was measured

T-073 executes every active widget definition once per reindex, through the
existing `IDashboardWidgetQueryService`, and persists the exact normalised result
as a `canon.assistant_widget_result` snapshot with a deterministic fingerprint
over the query identity and the result.

- 38 active widget definitions across 12 pages, discovered read-only from
  `dashboard_widget_definitions` joined to `dashboard_definitions`.
- Across repeated reindexes with no intervening change, a **subset** of those
  definitions produced materially different results.

**Repeated executions of these same widget definitions produced materially
different result and population metadata, and therefore correctly minted
different WidgetResult evidence identities.**

## The subset is not fixed

This matters, and a single list of "the broken widgets" would have been wrong.

| Run | Unstable pairs |
|---|---|
| 2026-08-08 19:56 | `EO_OBS`, `PA_BYP`, `PA_TABLE`, `PO_KPI_OBS` (4 of 38) |
| 2026-08-08 20:23 | `EO_OBS`, `PA_TABLE`, `PO_KPI_OBS` (3 of 38) |

`PA_BYP` was unstable in one run and stable in the next. The instability is
therefore intermittent rather than a fixed property of a fixed set.

## What the affected definitions have in common

- All are `observationCount`-shaped measures.
- The `population_count` recorded for them was **50000** in the 16:46 diagnostic
  and **5** in the 19:56 and 20:23 runs, for the same widget definitions.

## What is NOT claimed

- No root cause. The query path has not been traced and is not Worker 1's to
  trace.
- **No claim that 50000 is a cap.** It is a round number that appeared once; that
  is an observation, not a diagnosis. An earlier Worker 1 note called it a cap
  and that claim is withdrawn.
- No claim about the semantics of `population_count` or of the result's
  `observationCount` column. Worker 1 records that column literally and infers
  nothing from it.

## Why this is not a T-073 defect

The evidence fingerprint binds the query identity and the normalised result. When
a widget's real result changes, a **new** evidence identity is minted and the old
one is left intact rather than silently overwritten. That is the designed
behaviour and it is what surfaced this finding at all.

The consequence for the assistant is bounded and honest: a citation always
resolves to the exact snapshot the sentence was composed from. The consequence
for the product is not bounded, and is the reason this is being raised: **a user
asking the same question twice can be shown different numbers for these charts.**

## Requested

1. Trace the query path for these widget definitions and establish why repeated
   execution returns different results with no intervening data change.
2. Confirm the semantics of the population and `observationCount` fields, so the
   evidence snapshot can state them correctly rather than literally.

## Evidence

- `docs/m1/evidence/T-071_T-072_T-073_certification_20260808_195624.txt`
- `docs/m1/evidence/T-071_T-072_T-073_certification_20260808_202350.txt`
- `tools/run/Show-PpiqT073EvidenceState.ps1` (read-only, reproduces the table state)
