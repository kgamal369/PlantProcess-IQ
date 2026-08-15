"""The VectorSimilarityIndex product contract.

WHAT A GENERATION IS. An index generation is immutable. It is built, it is sealed,
and from that moment it never changes. Adding vectors does not modify it; it produces
a new generation that records its parent. This is what makes a search result citable
a month later: the thing that produced it still exists exactly as it was.

WHY IDENTITY IS COMPUTED, NOT ASSIGNED. The generation identity is a hash over the
index kind, the metric, the dimension, the ordered identifiers, the vector content
and the build parameters. Two builds of the same vectors under the same parameters
are the same generation and say so. A build with one vector changed is a different
generation and says that too, without anyone having to remember to bump a number.

WHAT AN IMPLEMENTATION MAY AND MAY NOT DO. Exact flat search is the permanent
correctness oracle: it returns the true nearest neighbours by construction, so its
answer is not a result to be measured but the standard other results are measured
against. Every approximate family is a candidate behind this interface, measured
against that oracle on the same vectors, and replaceable without touching anything
above this line.
"""

from __future__ import annotations

import hashlib
import json
from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping, Sequence


class IndexContractError(Exception):
    """The request cannot be interpreted under this contract."""


class IndexNotBuiltError(IndexContractError):
    """A sealed generation was expected and none exists yet."""


class IndexSealedError(IndexContractError):
    """An attempt was made to change a generation after it was sealed."""


class Metric(str, Enum):
    """How closeness is measured. Part of the generation identity.

    Both are expressed as a similarity where a larger number means closer, so every
    implementation orders its results the same way and a caller never has to know
    which direction a particular metric runs.
    """

    COSINE = "cosine"
    EUCLIDEAN = "euclidean"


@dataclass(frozen=True)
class SearchHit:
    vector_id: str
    score: float
    rank: int

    def to_dict(self) -> dict[str, Any]:
        return {"vector_id": self.vector_id, "score": self.score, "rank": self.rank}


@dataclass(frozen=True)
class SearchResult:
    """One query's answer, plus the handle that ties it to the generation."""

    query_position: int
    hits: tuple[SearchHit, ...]
    generation_id: str
    evidence_handle: str

    @property
    def vector_ids(self) -> tuple[str, ...]:
        return tuple(h.vector_id for h in self.hits)

    def to_dict(self) -> dict[str, Any]:
        return {
            "query_position": self.query_position,
            "hits": [h.to_dict() for h in self.hits],
            "generation_id": self.generation_id,
            "evidence_handle": self.evidence_handle,
        }


@dataclass(frozen=True)
class GenerationManifest:
    """What a sealed generation is. Written once, never amended."""

    generation_id: str
    index_kind: str
    metric: Metric
    dimension: int
    vector_count: int
    vector_content_hash: str
    parameters: Mapping[str, Any]
    build_seconds: float
    peak_build_bytes: int
    parent_generation_id: str | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "generation_id": self.generation_id,
            "index_kind": self.index_kind,
            "metric": self.metric.value,
            "dimension": self.dimension,
            "vector_count": self.vector_count,
            "vector_content_hash": self.vector_content_hash,
            "parameters": dict(sorted(self.parameters.items())),
            "build_seconds": self.build_seconds,
            "peak_build_bytes": self.peak_build_bytes,
            "parent_generation_id": self.parent_generation_id,
        }

    def identity_inputs(self) -> dict[str, Any]:
        """Exactly the fields the identity is computed from.

        Build duration and peak memory are measurements of one machine on one day.
        They are recorded on the manifest and deliberately excluded here, because an
        identity that moved with the weather would not identify anything.
        """
        return {
            "index_kind": self.index_kind,
            "metric": self.metric.value,
            "dimension": self.dimension,
            "vector_count": self.vector_count,
            "vector_content_hash": self.vector_content_hash,
            "parameters": dict(sorted(self.parameters.items())),
            "parent_generation_id": self.parent_generation_id,
        }


def vector_content_hash(
    ids: Sequence[str], vectors: Sequence[Sequence[float]]
) -> str:
    """Identity of the vectors themselves, order included.

    Order is part of it because two indexes built from the same vectors in a
    different order can return different answers among tied neighbours, and a
    reader comparing them deserves to know they were not the same input.
    """
    digest = hashlib.sha256()
    digest.update(b"ppiq.similarity.vectors/1\n")
    for identifier, vector in zip(ids, vectors):
        digest.update(str(identifier).encode("utf-8"))
        digest.update(b"\x1f")
        digest.update("|".join(repr(float(v)) for v in vector).encode("ascii"))
        digest.update(b"\x1e")
    return digest.hexdigest()


def compute_generation_id(
    index_kind: str,
    metric: Metric,
    dimension: int,
    vector_count: int,
    content_hash: str,
    parameters: Mapping[str, Any],
    parent_generation_id: str | None,
) -> str:
    payload = {
        "index_kind": index_kind,
        "metric": metric.value,
        "dimension": dimension,
        "vector_count": vector_count,
        "vector_content_hash": content_hash,
        "parameters": dict(sorted(parameters.items())),
        "parent_generation_id": parent_generation_id,
    }
    return hashlib.sha256(
        json.dumps(payload, indent=2, sort_keys=True).encode("ascii")
    ).hexdigest()


def evidence_handle(
    generation_id: str, query: Sequence[float], k: int, hits: Sequence[SearchHit]
) -> str:
    """A stable handle for one answer from one generation.

    Repeating a search against a sealed generation produces the same handle, which is
    what lets a later reader confirm that a citation and a rerun are the same event
    rather than two that happen to agree.
    """
    digest = hashlib.sha256()
    digest.update(b"ppiq.similarity.search/1\n")
    digest.update(generation_id.encode("ascii"))
    digest.update(b"|")
    digest.update("|".join(repr(float(v)) for v in query).encode("ascii"))
    digest.update(f"|k={k}|".encode("ascii"))
    for hit in hits:
        digest.update(f"{hit.rank}:{hit.vector_id}:{repr(float(hit.score))}".encode("utf-8"))
        digest.update(b"\x1f")
    return digest.hexdigest()


class VectorSimilarityIndex(ABC):
    """One index family behind the product contract.

    build seals a generation. search answers from a sealed generation. extend
    produces a NEW generation and never alters the one it came from.
    """

    @property
    @abstractmethod
    def index_kind(self) -> str:
        ...

    @property
    @abstractmethod
    def is_exact(self) -> bool:
        """True only for the correctness oracle. Every other family is a candidate."""

    @property
    @abstractmethod
    def manifest(self) -> GenerationManifest:
        """The sealed generation. Raises before a build."""

    @abstractmethod
    def build(
        self,
        ids: Sequence[str],
        vectors: Sequence[Sequence[float]],
        metric: Metric = Metric.COSINE,
        parameters: Mapping[str, Any] | None = None,
        parent_generation_id: str | None = None,
    ) -> GenerationManifest:
        """Build and seal one generation. A second call on a sealed index is refused."""

    @abstractmethod
    def search(self, queries: Sequence[Sequence[float]], k: int) -> tuple[SearchResult, ...]:
        ...

    @abstractmethod
    def extend(
        self, ids: Sequence[str], vectors: Sequence[Sequence[float]]
    ) -> "VectorSimilarityIndex":
        """Return a NEW sealed generation carrying the old vectors plus the new ones."""


def validate_population(
    ids: Sequence[str], vectors: Sequence[Sequence[float]]
) -> int:
    """Shared checks every implementation runs before it builds anything."""
    if len(ids) != len(vectors):
        raise IndexContractError(
            f"{len(ids)} identifier(s) were supplied for {len(vectors)} vector(s)."
        )
    if not vectors:
        raise IndexContractError("An index cannot be built from an empty population.")
    if len(set(ids)) != len(ids):
        raise IndexContractError(
            "Vector identifiers must be unique. A repeated identifier makes a recall "
            "measurement meaningless, because two hits could be the same neighbour."
        )
    dimension = len(vectors[0])
    if dimension == 0:
        raise IndexContractError("A vector must carry at least one component.")
    for position, vector in enumerate(vectors):
        if len(vector) != dimension:
            raise IndexContractError(
                f"Vector {position} carries {len(vector)} component(s) against a "
                f"declared dimension of {dimension}."
            )
    return dimension
