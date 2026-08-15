"""One approximate candidate, behind the same contract as the oracle.

WHAT IT DOES. The population is partitioned into cells around deterministic
centroids. A search compares the query against the centroids, then scans only the
cells whose centroids are closest. Fewer cells scanned means a faster answer and a
greater chance of missing a true neighbour that sits just across a boundary. That
trade is the whole subject of a recall measurement.

WHY IT IS DELIBERATELY SIMPLE. This is a candidate, not the product's answer to
similarity search. Its purpose is to make the contract real and the measurement chain
executable end to end with nothing installed. A production family such as HNSW, IVF
or PQ arrives later behind this same interface, and nothing above it changes when it
does.

WHY IT CAN BE WEAKENED ON PURPOSE. Setting cells high and probes low produces an
index that answers quickly and misses most of the true neighbours. A recall floor
that never rejects anything is not a floor, so the ability to build something that
must be refused is part of what this class is for.
"""

from __future__ import annotations

import time
import tracemalloc
from typing import Any, Mapping, Sequence

from .contract import (
    GenerationManifest,
    IndexContractError,
    IndexNotBuiltError,
    IndexSealedError,
    Metric,
    SearchResult,
    VectorSimilarityIndex,
    compute_generation_id,
    evidence_handle,
    validate_population,
    vector_content_hash,
)
from .exact_flat import rank_hits
from .metrics import prepared, prepared_similarity

PARTITIONED_PROBE_KIND = "partitioned_probe"

DEFAULT_CELLS = 8
DEFAULT_PROBES = 3
DEFAULT_REFINEMENT_PASSES = 5


def _mean(vectors: Sequence[Sequence[float]]) -> tuple[float, ...]:
    count = len(vectors)
    width = len(vectors[0])
    return tuple(sum(v[i] for v in vectors) / count for i in range(width))


class PartitionedProbeIndex(VectorSimilarityIndex):
    """Deterministic cell partitioning with a probe subset at search time."""

    def __init__(self) -> None:
        self._manifest: GenerationManifest | None = None
        self._ids: tuple[str, ...] = ()
        self._raw: tuple[tuple[float, ...], ...] = ()
        self._prepared: tuple[tuple[float, ...], ...] = ()
        self._metric: Metric = Metric.COSINE
        self._centroids: tuple[tuple[float, ...], ...] = ()
        self._cells: tuple[tuple[int, ...], ...] = ()
        self._probes: int = DEFAULT_PROBES

    @property
    def index_kind(self) -> str:
        return PARTITIONED_PROBE_KIND

    @property
    def is_exact(self) -> bool:
        return False

    @property
    def manifest(self) -> GenerationManifest:
        if self._manifest is None:
            raise IndexNotBuiltError(
                "This index has no sealed generation yet, so there is nothing to search."
            )
        return self._manifest

    @property
    def cell_sizes(self) -> tuple[int, ...]:
        return tuple(len(cell) for cell in self._cells)

    def build(
        self,
        ids: Sequence[str],
        vectors: Sequence[Sequence[float]],
        metric: Metric = Metric.COSINE,
        parameters: Mapping[str, Any] | None = None,
        parent_generation_id: str | None = None,
    ) -> GenerationManifest:
        if self._manifest is not None:
            raise IndexSealedError(
                "This generation is sealed. Building again would change an index that "
                "a search result already cites. Use extend to produce a new generation."
            )
        dimension = validate_population(ids, vectors)

        declared = dict(parameters or {})
        cells = int(declared.get("cells", DEFAULT_CELLS))
        probes = int(declared.get("probes", DEFAULT_PROBES))
        passes = int(declared.get("refinement_passes", DEFAULT_REFINEMENT_PASSES))
        if cells < 1:
            raise IndexContractError("An index needs at least one cell.")
        if probes < 1:
            raise IndexContractError(
                "A search must scan at least one cell. Zero probes would return "
                "nothing and call it an answer."
            )
        if probes > cells:
            raise IndexContractError(
                f"{probes} probe(s) were declared against {cells} cell(s). Probing more "
                "cells than exist is exact search wearing a candidate's name."
            )
        cells = min(cells, len(vectors))
        probes = min(probes, cells)
        declared.update({"cells": cells, "probes": probes, "refinement_passes": passes})

        tracemalloc.start()
        started = time.perf_counter()

        self._metric = metric
        self._probes = probes
        self._ids = tuple(str(i) for i in ids)
        self._raw = tuple(tuple(float(v) for v in vector) for vector in vectors)
        self._prepared = tuple(prepared(metric, vector) for vector in self._raw)
        self._centroids, self._cells = self._partition(cells, passes)

        build_seconds = time.perf_counter() - started
        _, peak = tracemalloc.get_traced_memory()
        tracemalloc.stop()

        content = vector_content_hash(self._ids, self._raw)
        self._manifest = GenerationManifest(
            generation_id=compute_generation_id(
                self.index_kind, metric, dimension, len(self._ids), content,
                declared, parent_generation_id,
            ),
            index_kind=self.index_kind,
            metric=metric,
            dimension=dimension,
            vector_count=len(self._ids),
            vector_content_hash=content,
            parameters=declared,
            build_seconds=build_seconds,
            peak_build_bytes=peak,
            parent_generation_id=parent_generation_id,
        )
        return self._manifest

    def _partition(
        self, cells: int, passes: int
    ) -> tuple[tuple[tuple[float, ...], ...], tuple[tuple[int, ...], ...]]:
        """Deterministic partitioning. No random seed, so no run-to-run difference.

        Initial centroids are taken by striding the stored order rather than by
        sampling, because a sampled start would make the sealed generation depend on
        a generator's implementation and two machines could produce different indexes
        from identical vectors.
        """
        total = len(self._prepared)
        stride = max(1, total // cells)
        centroids = [self._prepared[min(i * stride, total - 1)] for i in range(cells)]

        assignment = [0] * total
        for _ in range(max(1, passes)):
            for position, vector in enumerate(self._prepared):
                best, best_score = 0, None
                for index, centroid in enumerate(centroids):
                    score = prepared_similarity(self._metric, vector, centroid)
                    if best_score is None or score > best_score:
                        best, best_score = index, score
                assignment[position] = best

            members: list[list[int]] = [[] for _ in range(cells)]
            for position, cell in enumerate(assignment):
                members[cell].append(position)
            for index, group in enumerate(members):
                if group:
                    centroids[index] = _mean([self._prepared[p] for p in group])

        members = [[] for _ in range(cells)]
        for position, cell in enumerate(assignment):
            members[cell].append(position)
        return tuple(centroids), tuple(tuple(group) for group in members)

    def search(
        self, queries: Sequence[Sequence[float]], k: int
    ) -> tuple[SearchResult, ...]:
        manifest = self.manifest
        if k < 1:
            raise IndexNotBuiltError("A search must ask for at least one neighbour.")

        results = []
        for position, query in enumerate(queries):
            probe = prepared(self._metric, query)
            cell_scores = sorted(
                (
                    (prepared_similarity(self._metric, probe, centroid), index)
                    for index, centroid in enumerate(self._centroids)
                ),
                key=lambda pair: (-pair[0], pair[1]),
            )
            visited = [index for _, index in cell_scores[: self._probes]]

            scored = []
            for cell in visited:
                for member in self._cells[cell]:
                    scored.append(
                        (
                            self._ids[member],
                            prepared_similarity(self._metric, probe, self._prepared[member]),
                        )
                    )
            hits = rank_hits(scored, k)
            results.append(
                SearchResult(
                    query_position=position,
                    hits=hits,
                    generation_id=manifest.generation_id,
                    evidence_handle=evidence_handle(manifest.generation_id, query, k, hits),
                )
            )
        return tuple(results)

    def extend(
        self, ids: Sequence[str], vectors: Sequence[Sequence[float]]
    ) -> "PartitionedProbeIndex":
        parent = self.manifest
        successor = PartitionedProbeIndex()
        successor.build(
            ids=self._ids + tuple(str(i) for i in ids),
            vectors=self._raw + tuple(tuple(float(v) for v in row) for row in vectors),
            metric=self._metric,
            parameters=parent.parameters,
            parent_generation_id=parent.generation_id,
        )
        return successor
