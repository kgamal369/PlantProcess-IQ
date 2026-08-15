"""The mandatory simple baseline. It runs first, and it always runs.

WHAT IT MEASURES. For each usable feature, how far a unit sits from the reference
median, expressed in median absolute deviations. A unit's score is the mean of those
across features. That is all.

WHY THE MEDIAN AND NOT THE MEAN. The reference population is the thing being used to
define normal, and it contains the outliers. A mean and a standard deviation are
moved by exactly the units the model is supposed to find, so a small number of
extreme units raises the bar until they no longer look extreme. The median and the
median absolute deviation are not moved by them.

WHY IT IS MANDATORY. A candidate that flags the same units as this does has earned
nothing, and there is no way to know that without running this first on the same
population. It also always exists: no library, no fitting that can fail, and a
defined answer for any population that passed eligibility.
"""

from __future__ import annotations

from typing import Any, Mapping, Sequence

from .contract import (
    ModelClass,
    NoveltyModel,
    NoveltyResult,
    ThresholdIdentity,
    population_identity,
    rank_units,
    score_identity,
)
from .eligibility import evaluate_eligibility, validate_population
from .threshold import reference_quantile_threshold

BASELINE_MODEL_CODE = "mf03.robust_deviation_baseline"

#: Converts a median absolute deviation into a comparable scale. A feature whose
#: deviation is below this is treated as carrying no usable spread for this unit.
MINIMUM_DEVIATION = 1e-12


def median(values: Sequence[float]) -> float:
    ordered = sorted(float(v) for v in values)
    count = len(ordered)
    middle = count // 2
    if count % 2 == 1:
        return ordered[middle]
    return (ordered[middle - 1] + ordered[middle]) / 2.0


def median_absolute_deviation(values: Sequence[float], centre: float) -> float:
    return median([abs(float(v) - centre) for v in values])


class RobustDeviationBaseline(NoveltyModel):
    """Distance from the reference median, in median absolute deviations."""

    @property
    def model_code(self) -> str:
        return BASELINE_MODEL_CODE

    @property
    def model_class(self) -> ModelClass:
        return ModelClass.MANDATORY_SIMPLE_BASELINE

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

        from .contract import PopulationContext

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
        centres, deviations = [], []
        for position in positions:
            column = [float(row[position]) for row in reference_rows]
            centre = median(column)
            centres.append(centre)
            deviations.append(max(median_absolute_deviation(column, centre), MINIMUM_DEVIATION))

        scores = []
        for row in reference_rows:
            total = 0.0
            for offset, position in enumerate(positions):
                total += abs(float(row[position]) - centres[offset]) / deviations[offset]
            scores.append(total / len(positions))

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
            description=self._describe(seed, tuple(outcome.used_features)),
        )

    def _describe(self, seed: int, used: Sequence[str]) -> Mapping[str, Any]:
        return {
            "model_code": self.model_code,
            "library": "python standard library",
            "statistic": "mean absolute deviation from the reference median, "
                         "scaled by the median absolute deviation",
            "used_features": list(used),
            "seed": seed,
        }
