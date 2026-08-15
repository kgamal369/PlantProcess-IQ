"""The recall probe. A candidate measured against the oracle, on the same vectors.

WHAT RECALL IS HERE. For each query, the fraction of the oracle's true top-k that the
candidate also returned. Averaged across queries, that is recall@k. It is the only
number in this package that says whether an approximate index is any good, and it
cannot be computed without the oracle, which is why the oracle is permanent.

WHAT IS COMPARED, AND WHAT IS NOT. The comparison is of identifiers, not scores and
not order within the k. A candidate that returns the same neighbours in a different
order has found them; a candidate that returns different neighbours has not.

WHAT MAKES A BUILD INELIGIBLE. A recall below the declared floor. Speed does not buy
it back: an index that answers in a microsecond and finds a third of the neighbours
is answering a different question quickly. The probe therefore returns eligibility as
a named state with the numbers behind it, and never a score.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Sequence

import math
import time

from .contract import (
    GenerationManifest,
    IndexContractError,
    SearchResult,
    VectorSimilarityIndex,
)


class ServingEligibility(str, Enum):
    """Whether this build may answer a customer's question."""

    ELIGIBLE = "eligible"
    NOT_ELIGIBLE_TO_SERVE = "not_eligible_to_serve"


@dataclass(frozen=True)
class LatencyProfile:
    p50_ms: float
    p95_ms: float
    p99_ms: float
    queries: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "p50_ms": self.p50_ms,
            "p95_ms": self.p95_ms,
            "p99_ms": self.p99_ms,
            "queries": self.queries,
        }


@dataclass(frozen=True)
class RecallReport:
    """One candidate, one oracle, one set of queries, and the verdict."""

    candidate_kind: str
    candidate_generation_id: str
    oracle_generation_id: str
    vector_content_hash: str
    k: int
    recall_at_k: float
    worst_query_recall: float
    per_query_recall: tuple[float, ...]
    recall_floor: float
    eligibility: ServingEligibility
    reason: str
    latency: LatencyProfile
    build_seconds: float
    peak_build_bytes: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "candidate_kind": self.candidate_kind,
            "candidate_generation_id": self.candidate_generation_id,
            "oracle_generation_id": self.oracle_generation_id,
            "vector_content_hash": self.vector_content_hash,
            "k": self.k,
            "recall_at_k": self.recall_at_k,
            "worst_query_recall": self.worst_query_recall,
            "per_query_recall": list(self.per_query_recall),
            "recall_floor": self.recall_floor,
            "eligibility": self.eligibility.value,
            "reason": self.reason,
            "latency": self.latency.to_dict(),
            "build_seconds": self.build_seconds,
            "peak_build_bytes": self.peak_build_bytes,
        }


def percentile(sorted_values: Sequence[float], fraction: float) -> float:
    """Nearest-rank percentile over an already sorted list."""
    if not sorted_values:
        raise IndexContractError("A percentile over no measurements is undefined.")
    # Nearest rank: the smallest value at or above the requested fraction of the
    # ordered measurements. A rounded half-step lands one position too high and
    # would report a p95 that no query actually took.
    position = max(1, math.ceil(fraction * len(sorted_values))) - 1
    return sorted_values[min(position, len(sorted_values) - 1)]


def recall_of(oracle_hits: SearchResult, candidate_hits: SearchResult) -> float:
    truth = set(oracle_hits.vector_ids)
    if not truth:
        return 1.0
    found = truth & set(candidate_hits.vector_ids)
    return len(found) / len(truth)


def recall_probe(
    candidate: VectorSimilarityIndex,
    oracle: VectorSimilarityIndex,
    queries: Sequence[Sequence[float]],
    k: int,
    recall_floor: float,
) -> RecallReport:
    """Measure a candidate against the oracle. Refuses to compare two populations."""
    if not oracle.is_exact:
        raise IndexContractError(
            f"The oracle must be an exact index; '{oracle.index_kind}' is a candidate. "
            "A recall measured against an approximation is not a recall."
        )
    if candidate.is_exact:
        raise IndexContractError(
            "The candidate is an exact index, so this measurement would compare the "
            "oracle with itself and report a perfect result that means nothing."
        )

    candidate_manifest: GenerationManifest = candidate.manifest
    oracle_manifest: GenerationManifest = oracle.manifest

    if candidate_manifest.vector_content_hash != oracle_manifest.vector_content_hash:
        raise IndexContractError(
            "The candidate and the oracle were built from different vectors, so the "
            "candidate's misses cannot be told apart from the difference between the "
            "two populations."
        )
    if candidate_manifest.metric != oracle_manifest.metric:
        raise IndexContractError(
            f"The candidate measures closeness as {candidate_manifest.metric.value} and "
            f"the oracle as {oracle_manifest.metric.value}. Their neighbours are "
            "answers to different questions."
        )
    if not queries:
        raise IndexContractError("A recall probe needs at least one query.")

    truth = oracle.search(queries, k)

    durations: list[float] = []
    candidate_results: list[SearchResult] = []
    for query in queries:
        started = time.perf_counter()
        result = candidate.search([query], k)[0]
        durations.append((time.perf_counter() - started) * 1000.0)
        candidate_results.append(result)

    per_query = tuple(
        recall_of(truth[position], candidate_results[position])
        for position in range(len(queries))
    )
    mean_recall = sum(per_query) / len(per_query)
    worst = min(per_query)

    ordered = sorted(durations)
    latency = LatencyProfile(
        p50_ms=percentile(ordered, 0.50),
        p95_ms=percentile(ordered, 0.95),
        p99_ms=percentile(ordered, 0.99),
        queries=len(durations),
    )

    if mean_recall >= recall_floor:
        eligibility = ServingEligibility.ELIGIBLE
        reason = (
            f"Recall at {k} is {mean_recall:.4f} against a declared floor of "
            f"{recall_floor:.4f}, measured against the exact oracle on the same "
            f"{candidate_manifest.vector_count} vector(s)."
        )
    else:
        eligibility = ServingEligibility.NOT_ELIGIBLE_TO_SERVE
        reason = (
            f"Build '{candidate_manifest.generation_id[:12]}' of kind "
            f"'{candidate_manifest.index_kind}' returns recall at {k} of "
            f"{mean_recall:.4f} against a declared floor of {recall_floor:.4f}. It is "
            f"not eligible to serve. Its p95 search of {latency.p95_ms:.3f} ms does not "
            "change that: an index that answers quickly and misses the true neighbours "
            "is answering a different question."
        )

    return RecallReport(
        candidate_kind=candidate_manifest.index_kind,
        candidate_generation_id=candidate_manifest.generation_id,
        oracle_generation_id=oracle_manifest.generation_id,
        vector_content_hash=candidate_manifest.vector_content_hash,
        k=k,
        recall_at_k=mean_recall,
        worst_query_recall=worst,
        per_query_recall=per_query,
        recall_floor=recall_floor,
        eligibility=eligibility,
        reason=reason,
        latency=latency,
        build_seconds=candidate_manifest.build_seconds,
        peak_build_bytes=candidate_manifest.peak_build_bytes,
    )
