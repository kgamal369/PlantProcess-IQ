# T-073 CLOSURE - Page and widget chunk family

**Status:** API certification complete. Closed.
**Commits:** `976bf9bc` (evidence table), `472ce274` (provenance kind),
`61dc1f98` (producer), `354f91ea` (rebuild replay), `89f508a` (semantics and
tenant-scoped read), `dbdbdbec` (evidence anchor), `b0bb9863` (focused
composition)
**Evidence:** `docs/m1/evidence/T-071_T-072_T-073_certification_20260808_202350.txt`

## The chain, as ruled

    active widget definition
    -> executed ONCE through the existing IDashboardWidgetQueryService
    -> normalised
    -> PERSISTED as canon.assistant_widget_result
    -> READ BACK
    -> the chunk sentence composed FROM THE PERSISTED ROW
    -> reindexed through the existing path
    -> retrieved, answered, cited
    -> the citation resolves to that exact snapshot

Persist first, then compose. A sentence built in memory with an evidence row
written beside it afterwards would look identical and prove nothing, because
nothing would tie the words to the row.

## Three defects found on the way, each worth keeping

1. **The retrieval layer rebuilt handles and would have issued Dataset
   citations.** `NpgsqlRetrievalIndex.HandleFor` discards the producer's handle
   and reconstructs one, defaulting to `Dataset`. A perfectly correct
   `WidgetResult` handle would have reached the assistant as the exact
   substitution the ruling forbids. The chunk's `source_ref` is now the snapshot
   id and the index maps the kind.
2. **The anchor alone produced a false green.** A turn focused on `CF_RATE` was
   answered describing `CF_TOP`, and the assertion passed because a `CF_RATE`
   citation also existed. The extractive model turns every retrieved chunk into
   prose, so a neighbouring widget spoke in place of the focused one.
3. **A negative value passed a positive lower bound** in the first quantity
   guard, because the candidate capture started at a digit and dropped the sign.

## Certified, per criterion

| Criterion | Result |
|---|---|
| Three pages, real matching numbers, citations | CF_RATE, DQ_BY_SOURCE, EO_EQDEF, all A-E green |
| A: persisted sentence is the primary statement | verbatim in the answer |
| B: first WidgetResult citation is the focused widget | yes, all three |
| C: EVERY WidgetResult citation is that widget on that page | yes - the assertion the false green slipped past |
| D: numbers match the snapshot | 18, 2 and 2 numbers checked digit by digit |
| E: the handle resolves tenant-scoped | yes; an unknown id reports unavailable, never content |
| Unchanged reindex does not silently change evidence | 35 of 38 pairs stable; the rest named, never silent |
| Chunks removed, honest refusal | refused with the anchor reason, no fallback to connector evidence |

Selection required stable AND non-empty widgets: an empty result is honest
behaviour but is not one of the three numeric examples the frozen task asks for.

## Durability

`Rebuild-PresentationDb.ps1` replays an explicit list, not a glob, so 780 was
added to it and the rebuild now reports whether the evidence table exists.
`apply-server-db-scripts.sh` was measured and deliberately left alone: it is a
bounded ML correction list, not the owner of every migration.

## Recorded debt

- The column is named `population_count` and stores the total of the result's
  own `observationCount` column. Renaming needs a second migration; nothing
  reads it as a population.
- Widget instability is handed to Worker 2 in
  `docs/m1/evidence/T-073_UPSTREAM_FINDING_widget_result_instability.md`.
