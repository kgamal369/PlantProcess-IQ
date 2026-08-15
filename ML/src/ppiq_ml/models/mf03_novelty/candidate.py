"""The density candidate. It runs only where the population is eligible.

WHAT IT MEASURES. How far a unit sits from its own nearest neighbours in the
reference population. A unit inside a dense group has close neighbours and scores
low; a unit alone in a region has distant ones and scores high.

WHY IT IS A CANDIDATE AND NOT THE ANSWER. It finds a kind of unusual the baseline
cannot: a unit whose every individual measurement is ordinary but whose combination
occurs nowhere else. It also costs more, and on a population where the baseline
already separates the same units it has earned nothing. Which of the two should serve
is not decided here.

WHY IT REUSES THE SEALED EXACT INDEX. The neighbour search is exactly the problem
T-173 solved, and its exact index returns the true nearest neighbours by construction.
Writing a second neighbour search here would mean two definitions of nearest in one
repository, and the day they disagreed nobody would know which was right.
"""

from __future__ import annotations

from typing import Any, Mapping, Sequence

from ...similarity import ExactFlatIndex, Metric
from .contract import (
    ModelClass,
    NoveltyModel,
    NoveltyResult,
    PopulationContext,
    ThresholdIdentity,
    population_identity,
    rank_units,
    score_identity,
)
from .eligibility import evaluate_eligibility, validate_population
from .threshold import reference_quantile_threshold

CANDIDATE_MODEL_CODE = "mf03.neighbour_density_candidate"

#: How many neighbours define local density. Declared, not measured.
DEFAULT_NEIGHBOURS = 5


class NeighbourDensityCandidate(NoveltyModel):
    """Mean distance to the nearest reference neighbours."""

    def __init__(self, neighbours: int = DEFAULT_NEIGHBOURS) -> None:
        if neighbours < 1:
            raise ValueError("At least one neighbour is needed to measure density.")
        self._neighbours = neighbours

    @property
    def model_code(self) -> str:
        return CANDIDATE_MODEL_CODE

    @property
    def model_class(self) -> ModelClass:
        return ModelClass.CANDIDATE

    def evaluate(
        self,
        reference_ids: Sequence[str],
        reference_rows: Sequence[Sequence[float]],
        feature_names: Sequence[str],
        quantile: float,
        seed: int,
    ) -> NoveltyResult:
        validate_population(reference_ids, reference_rows, feature_names)
        outcome = evaluate_eligibility(reference_ids, reference_rows, feature_names)

        context = PopulationContext(
            reference_units=len(reference_rows),
            distinct_reference_units=outcome.distinct_units,
            declared_features=tuple(str(n) for n in feature_names),
            used_features=outcome.used_features,
            excluded_features=outcome.excluded_features,
            reference_identity=population_identity(
                reference_ids, reference_rows, feature_names
            ),
        )

        if not outcome.eligible:
            return NoveltyResult(
                model_code=self.model_code,
                model_class=self.model_class,
                scored_units=(),
                threshold=None,
                population=context,
                refusal=outcome.refusal,
                description=self._describe(seed, ()),
            )

        positions = [
            index
            for index, name in enumerate(feature_names)
            if str(name) in set(outcome.used_features)
        ]
        projected = [tuple(float(row[p]) for p in positions) for row in reference_rows]

        # Scaled to the reference spread so a feature measured in thousands does not
        # dominate one measured in units purely by its unit of measurement.
        spans = []
        for offset in range(len(positions)):
            column = [row[offset] for row in projected]
            span = max(column) - min(column)
            spans.append(span if span > 0.0 else 1.0)
        scaled = [
            tuple(value / spans[offset] for offset, value in enumerate(row))
            for row in projected
        ]

        index = ExactFlatIndex()
        index.build(list(reference_ids), scaled, Metric.EUCLIDEAN)

        # One extra neighbour, because the nearest neighbour of a stored unit is
        # itself and a distance of zero to itself says nothing about density.
        wanted = min(self._neighbours + 1, len(scaled))
        scores = []
        for row in scaled:
            hits = index.search([row], k=wanted)[0].hits
            distances = [-hit.score for hit in hits[1:]] or [0.0]
            scores.append(sum(distances) / len(distances))

        threshold_value = reference_quantile_threshold(scores, quantile)
        threshold = ThresholdIdentity(
            method="reference_quantile",
            quantile=quantile,
            value=threshold_value,
            reference_score_identity=score_identity(scores),
        )

        return NoveltyResult(
            model_code=self.model_code,
            model_class=self.model_class,
            scored_units=rank_units(reference_ids, scores, threshold_value),
            threshold=threshold,
            population=context,
            refusal=outcome.refusal,
            description=self._describe(
                seed, tuple(outcome.used_features), index.manifest.generation_id
            ),
        )

    def _describe(
        self, seed: int, used: Sequence[str], generation: str | None = None
    ) -> Mapping[str, Any]:
        return {
            "model_code": self.model_code,
            "library": "python standard library",
            "statistic": "mean distance to the nearest reference neighbours, "
                         "each feature scaled to its reference span",
            "neighbours": self._neighbours,
            "used_features": list(used),
            "seed": seed,
            "neighbour_index_generation": generation,
        }
