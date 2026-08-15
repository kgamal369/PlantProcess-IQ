"""What a supervised model is, what it is trained on, and what may be said about it.

THE BOUNDARY THIS MODULE DRAWS. A model implementation reports what it measured. It
does not select. There is no method here that returns a winner, and the comparison
record carries both results side by side with the deciding dimension left open.
Calibration, explanation stability and the three-dimensional selection kernel are
T-176's subject, and a selection made here would pre-empt a decision that has more
dimensions than this task can see.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

from .metrics import MetricSet
from .outcome import OutcomeDefinition, OutcomeKind


class ModelUnavailableError(Exception):
    """The model family cannot run in this environment, for a stated reason.

    Distinct from a refusal about the customer's data. An absent library is a
    property of the installation and is reported as such rather than as a finding
    about the population.
    """


@dataclass(frozen=True)
class Population:
    """An ordered, typed training population read from one sealed artifact.

    snapshot_identity is the format-independent logical hash of the projected
    columns. Both the baseline and the candidate carry it, which is what makes the
    statement that they trained on the same data checkable rather than asserted.
    """

    outcome: OutcomeDefinition
    feature_columns: tuple[str, ...]
    grains: tuple[Any, ...]
    order_values: tuple[Any, ...]
    labels: tuple[Any, ...]
    feature_rows: tuple[tuple[Any, ...], ...]
    snapshot_identity: str

    def __len__(self) -> int:
        return len(self.labels)

    def select(self, indices: Sequence[int]) -> "Population":
        return Population(
            outcome=self.outcome,
            feature_columns=self.feature_columns,
            grains=tuple(self.grains[i] for i in indices),
            order_values=tuple(self.order_values[i] for i in indices),
            labels=tuple(self.labels[i] for i in indices),
            feature_rows=tuple(self.feature_rows[i] for i in indices),
            snapshot_identity=self.snapshot_identity,
        )

    def observed_classes(self) -> tuple[Any, ...]:
        """Distinct label values in a stable order.

        Declared order wins where the contract declares one, so an ordinal model's
        class positions do not depend on which rows happened to arrive first.
        """
        if self.outcome.kind == OutcomeKind.CONTINUOUS:
            return ()
        declared = self.outcome.class_order
        distinct = set(self.labels)
        if declared:
            ordered = [c for c in declared if c in distinct]
            ordered += [c for c in self._first_seen() if c not in set(declared)]
            return tuple(ordered)
        return tuple(self._first_seen())

    def _first_seen(self) -> list[Any]:
        seen: list[Any] = []
        for label in self.labels:
            if label not in seen:
                seen.append(label)
        return seen


class TrainedModel(ABC):
    """A fitted model. It predicts and it describes itself. It never concludes."""

    @property
    @abstractmethod
    def model_code(self) -> str:
        ...

    @property
    @abstractmethod
    def classes(self) -> tuple[Any, ...]:
        """Class order the probability rows are aligned to. Empty when continuous."""

    @abstractmethod
    def predict(self, feature_rows: Sequence[Sequence[Any]]) -> tuple:
        """Rows of class probabilities, or a value per row when continuous."""

    @abstractmethod
    def describe(self) -> Mapping[str, Any]:
        """Hyperparameters and library identity, for reproducibility."""

    @abstractmethod
    def serialise(self) -> str:
        """A text form of the fitted model, written as the model artifact."""


class SupervisedOutcomeModel(ABC):
    """One trainable family behind the common runtime."""

    @property
    @abstractmethod
    def model_code(self) -> str:
        ...

    @property
    @abstractmethod
    def model_class(self) -> str:
        """Either the mandatory floor or a candidate measured against it."""

    @abstractmethod
    def supports(self, kind: OutcomeKind) -> bool:
        ...

    @abstractmethod
    def fit(self, data: Population, seed: int) -> TrainedModel:
        ...


MODEL_CLASS_FLOOR = "mandatory_simple_baseline"
MODEL_CLASS_CANDIDATE = "candidate"


@dataclass(frozen=True)
class ModelEvaluation:
    """One model's measured result on the holdout."""

    model_code: str
    model_class: str
    metrics: MetricSet
    training_seconds: float
    scoring_seconds: float
    description: Mapping[str, Any]
    snapshot_identity: str
    holdout_identity: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "model_code": self.model_code,
            "model_class": self.model_class,
            "metrics": self.metrics.to_dict(),
            "training_seconds": self.training_seconds,
            "scoring_seconds": self.scoring_seconds,
            "description": dict(self.description),
            "snapshot_identity": self.snapshot_identity,
            "holdout_identity": self.holdout_identity,
        }


@dataclass(frozen=True)
class ComparisonRecord:
    """Both results, the same holdout, and no decision.

    Every metric present for both models appears with both values and their
    difference. Which difference matters, and by how much, is decided by the
    three-dimensional selection kernel in T-176 against dimensions this task does
    not measure.
    """

    baseline_code: str
    candidate_code: str
    snapshot_identity: str
    holdout_identity: str
    differences: Mapping[str, Mapping[str, float]]
    selection_owner: str = "T-176 selection kernel"
    selection_made_here: bool = False

    def to_dict(self) -> dict[str, Any]:
        return {
            "baseline_code": self.baseline_code,
            "candidate_code": self.candidate_code,
            "snapshot_identity": self.snapshot_identity,
            "holdout_identity": self.holdout_identity,
            "differences": {k: dict(v) for k, v in self.differences.items()},
            "selection_owner": self.selection_owner,
            "selection_made_here": self.selection_made_here,
        }


def compare(
    baseline: ModelEvaluation, candidate: ModelEvaluation
) -> ComparisonRecord:
    """Place two evaluations side by side. Refuses to compare across holdouts."""
    if baseline.snapshot_identity != candidate.snapshot_identity:
        raise ValueError(
            "The baseline and the candidate were trained on different snapshots, so "
            "their metrics are not comparable."
        )
    if baseline.holdout_identity != candidate.holdout_identity:
        raise ValueError(
            "The baseline and the candidate were evaluated on different holdouts, so "
            "their metrics are not comparable."
        )

    shared = sorted(set(baseline.metrics.values) & set(candidate.metrics.values) - {"n"})
    differences = {
        name: {
            "baseline": float(baseline.metrics.values[name]),
            "candidate": float(candidate.metrics.values[name]),
            "candidate_minus_baseline": float(
                candidate.metrics.values[name] - baseline.metrics.values[name]
            ),
        }
        for name in shared
    }
    return ComparisonRecord(
        baseline_code=baseline.model_code,
        candidate_code=candidate.model_code,
        snapshot_identity=baseline.snapshot_identity,
        holdout_identity=baseline.holdout_identity,
        differences=differences,
    )
