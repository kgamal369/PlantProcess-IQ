"""Exact flat search. The permanent correctness oracle.

WHY THIS IS NOT A CANDIDATE. Every other family in this package is measured for
recall, and recall is a comparison against the true nearest neighbours. This
implementation compares the query against every stored vector, so its answer is the
true answer by construction. It is the standard, not a competitor to it, and a recall
figure computed without it is not a measurement of anything.

WHY IT HAS NO DEPENDENCY. The thing that defines correct cannot be allowed to move
because a numerical package changed its summation order. Standard library only, and
that is a requirement rather than a convenience.

WHY IT STAYS. It is the permanent benchmark on a representative sample. A candidate
that overtakes it on speed does not replace it, because speed was never what it was
for.
"""

from __future__ import annotations

import time
import tracemalloc
from typing import Any, Mapping, Sequence

from .contract import (
    GenerationManifest,
    IndexNotBuiltError,
    IndexSealedError,
    Metric,
    SearchHit,
    SearchResult,
    VectorSimilarityIndex,
    compute_generation_id,
    evidence_handle,
    validate_population,
    vector_content_hash,
)
from .metrics import prepared, prepared_similarity

EXACT_FLAT_KIND = "exact_flat"


def rank_hits(
    scored: Sequence[tuple[str, float]], k: int
) -> tuple[SearchHit, ...]:
    """Order by closeness, ties broken by identifier so the answer is reproducible.

    Tied neighbours are common: duplicate readings, repeated setpoints and quantised
    sensors all produce them. Without the tie break, two runs of the same search
    could return different neighbours and a recall comparison would measure the
    ordering accident rather than the index.
    """
    ordered = sorted(scored, key=lambda pair: (-pair[1], pair[0]))
    return tuple(
        SearchHit(vector_id=identifier, score=score, rank=position)
        for position, (identifier, score) in enumerate(ordered[:k])
    )


class ExactFlatIndex(VectorSimilarityIndex):
    """Brute-force search over every stored vector."""

    def __init__(self) -> None:
        self._manifest: GenerationManifest | None = None
        self._ids: tuple[str, ...] = ()
        self._raw: tuple[tuple[float, ...], ...] = ()
        self._prepared: tuple[tuple[float, ...], ...] = ()
        self._metric: Metric = Metric.COSINE

    @property
    def index_kind(self) -> str:
        return EXACT_FLAT_KIND

    @property
    def is_exact(self) -> bool:
        return True

    @property
    def manifest(self) -> GenerationManifest:
        if self._manifest is None:
            raise IndexNotBuiltError(
                "This index has no sealed generation yet, so there is nothing to "
                "describe and nothing to search."
            )
        return self._manifest

    @property
    def vector_ids(self) -> tuple[str, ...]:
        return self._ids

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

        tracemalloc.start()
        started = time.perf_counter()
        self._metric = metric
        self._ids = tuple(str(i) for i in ids)
        self._raw = tuple(tuple(float(v) for v in vector) for vector in vectors)
        self._prepared = tuple(prepared(metric, vector) for vector in self._raw)
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

    def search(
        self, queries: Sequence[Sequence[float]], k: int
    ) -> tuple[SearchResult, ...]:
        manifest = self.manifest
        if k < 1:
            raise IndexNotBuiltError("A search must ask for at least one neighbour.")

        results = []
        for position, query in enumerate(queries):
            probe = prepared(self._metric, query)
            scored = [
                (self._ids[i], prepared_similarity(self._metric, probe, self._prepared[i]))
                for i in range(len(self._ids))
            ]
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
    ) -> "ExactFlatIndex":
        """A new generation carrying the old vectors and the new ones.

        The generation this was called on is untouched and remains searchable, which
        is what immutable means here.
        """
        parent = self.manifest
        successor = ExactFlatIndex()
        successor.build(
            ids=self._ids + tuple(str(i) for i in ids),
            vectors=self._raw + tuple(tuple(float(v) for v in row) for row in vectors),
            metric=self._metric,
            parameters=parent.parameters,
            parent_generation_id=parent.generation_id,
        )
        return successor
