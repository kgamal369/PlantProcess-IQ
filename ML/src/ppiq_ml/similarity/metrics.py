"""How closeness is computed, in one place so every family agrees.

Both metrics return a similarity where a larger number means closer. Euclidean
distance is therefore negated. Doing this once here is what lets the oracle and every
candidate order their results identically, and it removes a whole class of recall
measurement that is wrong only because two implementations sorted opposite ways.
"""

from __future__ import annotations

import math
from typing import Sequence

from .contract import IndexContractError, Metric

#: Below this length a vector has no direction, so a cosine similarity against it is
#: undefined rather than zero.
ZERO_NORM = 1e-12


def norm(vector: Sequence[float]) -> float:
    return math.sqrt(sum(float(v) * float(v) for v in vector))


def normalise(vector: Sequence[float]) -> tuple[float, ...]:
    length = norm(vector)
    if length < ZERO_NORM:
        raise IndexContractError(
            "A vector with no length has no direction, so cosine closeness to it is "
            "undefined. It is refused rather than treated as equally close to "
            "everything."
        )
    return tuple(float(v) / length for v in vector)


def dot(left: Sequence[float], right: Sequence[float]) -> float:
    return sum(float(a) * float(b) for a, b in zip(left, right))


def similarity(metric: Metric, left: Sequence[float], right: Sequence[float]) -> float:
    """Larger is closer, for every metric."""
    if metric == Metric.COSINE:
        return dot(normalise(left), normalise(right))
    if metric == Metric.EUCLIDEAN:
        return -math.sqrt(sum((float(a) - float(b)) ** 2 for a, b in zip(left, right)))
    raise IndexContractError(f"Unsupported metric '{metric}'.")


def prepared(metric: Metric, vector: Sequence[float]) -> tuple[float, ...]:
    """The stored form of a vector for a metric.

    Cosine stores the unit vector so that a search is a dot product rather than a
    division per comparison. Euclidean stores the vector as it arrived.
    """
    if metric == Metric.COSINE:
        return normalise(vector)
    return tuple(float(v) for v in vector)


def prepared_similarity(
    metric: Metric, prepared_left: Sequence[float], prepared_right: Sequence[float]
) -> float:
    """Closeness between two already-prepared vectors."""
    if metric == Metric.COSINE:
        return dot(prepared_left, prepared_right)
    return -math.sqrt(
        sum((float(a) - float(b)) ** 2 for a, b in zip(prepared_left, prepared_right))
    )
