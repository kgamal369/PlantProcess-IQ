"""The mandatory simple baseline.

It is trained first, always, and it uses no feature at all. A classification
baseline predicts the training prior for every unit; a continuous baseline predicts
the training mean.

WHY IT IS MANDATORY. Without it there is no scale on which a candidate's number
means anything. An area under the curve of 0.71 is impressive against 0.50 and
worthless against 0.70, and the difference is invisible unless the floor was measured
on the same holdout by the same code path. A product that reports the second case as
intelligence has told a customer that a constant is a finding.

It is also the fallback that always exists. It cannot fail to fit, it has no library
dependency, and its predictions are defined for any population that passed
eligibility.
"""

from __future__ import annotations

import json
from typing import Any, Mapping, Sequence

from .contract import (
    MODEL_CLASS_FLOOR,
    Population,
    SupervisedOutcomeModel,
    TrainedModel,
)
from .outcome import OutcomeKind

BASELINE_MODEL_CODE = "mf04.prior_baseline"


class _TrainedPriorBaseline(TrainedModel):
    def __init__(
        self,
        kind: OutcomeKind,
        classes: tuple[Any, ...],
        priors: tuple[float, ...],
        constant_value: float | None,
        trained_rows: int,
    ) -> None:
        self._kind = kind
        self._classes = classes
        self._priors = priors
        self._constant_value = constant_value
        self._trained_rows = trained_rows

    @property
    def model_code(self) -> str:
        return BASELINE_MODEL_CODE

    @property
    def classes(self) -> tuple[Any, ...]:
        return self._classes

    def predict(self, feature_rows: Sequence[Sequence[Any]]) -> tuple:
        if self._kind == OutcomeKind.CONTINUOUS:
            return tuple(float(self._constant_value or 0.0) for _ in feature_rows)
        return tuple(self._priors for _ in feature_rows)

    def describe(self) -> Mapping[str, Any]:
        return {
            "model_code": BASELINE_MODEL_CODE,
            "library": "python standard library",
            "uses_features": False,
            "trained_rows": self._trained_rows,
            "outcome_kind": self._kind.value,
        }

    def serialise(self) -> str:
        payload: dict[str, Any] = {
            "model_code": BASELINE_MODEL_CODE,
            "outcome_kind": self._kind.value,
            "trained_rows": self._trained_rows,
        }
        if self._kind == OutcomeKind.CONTINUOUS:
            payload["constant_value"] = self._constant_value
        else:
            payload["classes"] = [str(c) for c in self._classes]
            payload["priors"] = list(self._priors)
        return json.dumps(payload, indent=2, sort_keys=True)


class PriorBaseline(SupervisedOutcomeModel):
    """The floor. Supports every outcome shape, because every shape needs one."""

    @property
    def model_code(self) -> str:
        return BASELINE_MODEL_CODE

    @property
    def model_class(self) -> str:
        return MODEL_CLASS_FLOOR

    def supports(self, kind: OutcomeKind) -> bool:
        return True

    def fit(self, data: Population, seed: int) -> TrainedModel:
        kind = data.outcome.kind
        rows = len(data)
        if rows == 0:
            raise ValueError("The baseline cannot be fitted on an empty population.")

        if kind == OutcomeKind.CONTINUOUS:
            mean = sum(float(v) for v in data.labels) / rows
            return _TrainedPriorBaseline(kind, (), (), mean, rows)

        classes = data.observed_classes()
        counts = {c: 0 for c in classes}
        for label in data.labels:
            counts[label] += 1
        priors = tuple(counts[c] / rows for c in classes)
        return _TrainedPriorBaseline(kind, classes, priors, None, rows)
