"""Explanation stability, computed rather than asserted.

WHAT INSTABILITY LOOKS LIKE. Two explanation runs over the same model and the same
population produce different orderings of the same features. A model like that can
still score well, and a person acting on its explanation will be told a different
cause each time they ask. In a product whose claim is that an answer can be traced,
that is worse than a lower area under the curve.

TWO STATISTICS, BOTH MANDATORY, DELIBERATELY.

Rank agreement asks whether the whole ordering holds. It is a tie-corrected rank
correlation over the magnitudes, averaged across every pair of runs.

Top-k overlap asks whether the features a person would actually read hold. A model
can shuffle its irrelevant tail freely and still keep a respectable rank correlation
while its top three change every run, which is precisely the case a reader notices.

Neither is derived from the other, so neither is dropped.

This module computes from attribution vectors and does not produce them. That
boundary is why the explanation method is replaceable: the kernel judges the numbers
and never learns which library made them.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, Sequence


class StabilityError(Exception):
    """Stability cannot be computed from what was supplied."""


@dataclass(frozen=True)
class ExplanationStability:
    rank_agreement: float
    top_k_overlap: float
    sign_agreement: float
    repeats: int
    top_k: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "rank_agreement": self.rank_agreement,
            "top_k_overlap": self.top_k_overlap,
            "sign_agreement": self.sign_agreement,
            "repeats": self.repeats,
            "top_k": self.top_k,
        }


def midranks(values: Sequence[float]) -> list[float]:
    """Ranks with ties sharing the average position.

    Attribution vectors tie constantly, because a feature a model ignored scores
    zero in every run. A tie-blind ranking would order those zeroes arbitrarily and
    then report the arbitrary order as disagreement.
    """
    order = sorted(range(len(values)), key=lambda i: float(values[i]))
    ranks = [0.0] * len(values)
    index = 0
    while index < len(order):
        end = index
        while end + 1 < len(order) and float(values[order[end + 1]]) == float(values[order[index]]):
            end += 1
        shared = (index + end) / 2.0 + 1.0
        for position in range(index, end + 1):
            ranks[order[position]] = shared
        index = end + 1
    return ranks


def rank_correlation(left: Sequence[float], right: Sequence[float]) -> float:
    """Tie-corrected rank correlation. Returns 1.0 when both sides are constant.

    Two runs that both assign the same value to every feature agree completely. The
    textbook formula divides by zero there, and returning a not-a-number would put a
    value into a decision document that cannot be compared against a floor.
    """
    if len(left) != len(right):
        raise StabilityError("Two attribution vectors of different lengths cannot be compared.")
    if len(left) < 2:
        raise StabilityError("Rank correlation needs at least two features.")

    a = midranks(left)
    b = midranks(right)
    mean_a = sum(a) / len(a)
    mean_b = sum(b) / len(b)
    covariance = sum((x - mean_a) * (y - mean_b) for x, y in zip(a, b))
    spread_a = math.sqrt(sum((x - mean_a) ** 2 for x in a))
    spread_b = math.sqrt(sum((y - mean_b) ** 2 for y in b))

    if spread_a == 0.0 and spread_b == 0.0:
        return 1.0
    if spread_a == 0.0 or spread_b == 0.0:
        # One run separates the features and the other does not. That is a real
        # disagreement about whether any feature matters, reported as no agreement
        # rather than as an undefined quantity.
        return 0.0
    return covariance / (spread_a * spread_b)


def top_k_indices(values: Sequence[float], k: int) -> set[int]:
    """The k largest magnitudes, ties broken by position so the answer is stable."""
    ordered = sorted(range(len(values)), key=lambda i: (-abs(float(values[i])), i))
    return set(ordered[:k])


def evaluate_stability(
    attributions: Sequence[Sequence[float]], top_k: int
) -> ExplanationStability:
    """Average pairwise agreement across every repeat."""
    repeats = len(attributions)
    if repeats < 2:
        raise StabilityError(
            "Stability needs at least two repeats. A single explanation run cannot "
            "disagree with itself."
        )
    width = len(attributions[0])
    if any(len(vector) != width for vector in attributions):
        raise StabilityError("Every attribution vector must carry the same feature count.")
    if top_k < 1 or top_k > width:
        raise StabilityError(
            f"top_k must lie between 1 and the feature count {width}; {top_k} was declared."
        )

    magnitudes = [[abs(float(v)) for v in vector] for vector in attributions]

    rank_scores: list[float] = []
    overlap_scores: list[float] = []
    sign_scores: list[float] = []

    for i in range(repeats):
        for j in range(i + 1, repeats):
            rank_scores.append(rank_correlation(magnitudes[i], magnitudes[j]))

            left_top = top_k_indices(attributions[i], top_k)
            right_top = top_k_indices(attributions[j], top_k)
            union = left_top | right_top
            overlap_scores.append(len(left_top & right_top) / len(union) if union else 1.0)

            agreed = sum(
                1
                for a, b in zip(attributions[i], attributions[j])
                if (float(a) > 0) == (float(b) > 0) and (float(a) < 0) == (float(b) < 0)
            )
            sign_scores.append(agreed / width)

    return ExplanationStability(
        rank_agreement=sum(rank_scores) / len(rank_scores),
        top_k_overlap=sum(overlap_scores) / len(overlap_scores),
        sign_agreement=sum(sign_scores) / len(sign_scores),
        repeats=repeats,
        top_k=top_k,
    )
