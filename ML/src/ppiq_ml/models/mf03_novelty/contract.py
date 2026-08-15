"""What a novelty answer is, and the four things it must keep apart.

A novelty score is not a property of a unit. It is a statement about how far that
unit sits from a reference population, and it becomes false the moment the reference
stops representing the conditions the unit came from. Everything in this module
exists to make that dependency visible rather than implied.

FOUR SEPARATE PARTS, NEVER MERGED.

    score                   how far this unit sits from the reference
    threshold identity      where the line was drawn, by what method, on what data
    population context      what the reference actually was, and what was excluded
    refusal state           why no score exists, when none does

A reader given only a score cannot tell whether it means anything. A reader given all
four can. Merging any two of them, most temptingly the score and the threshold into a
single flag, throws away the part a person needs in order to disagree.

WHAT A REFUSAL IS. Not an error and not a zero. A population too small or too
uniform to support a novelty claim produces a refusal that names the measurement that
failed. The alternative is a number that looks like every other number and is not one.
"""

from __future__ import annotations

import hashlib
from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping, Sequence


class NoveltyContractError(Exception):
    """The request cannot be interpreted under this contract."""


class NoveltyRefusalCode(str, Enum):
    """Why no novelty claim can be made. Every member names a measurement."""

    NONE = "none"
    TOO_FEW_REFERENCE_UNITS = "too_few_reference_units"
    NO_USABLE_FEATURE = "no_usable_feature"
    DEGENERATE_POPULATION = "degenerate_population"
    TOO_FEW_DISTINCT_UNITS = "too_few_distinct_units"
    REFERENCE_NOT_COMPARABLE = "reference_not_comparable"


class ModelClass(str, Enum):
    """The mandatory floor, and what may run only once the floor has run."""

    MANDATORY_SIMPLE_BASELINE = "mandatory_simple_baseline"
    CANDIDATE = "candidate"


@dataclass(frozen=True)
class FeatureExclusion:
    """One feature the model could not use, and the measurement that says why."""

    feature: str
    reason: str
    observed: float

    def to_dict(self) -> dict[str, Any]:
        return {"feature": self.feature, "reason": self.reason, "observed": self.observed}


@dataclass(frozen=True)
class PopulationContext:
    """What the reference was. Without this a score is uninterpretable."""

    reference_units: int
    distinct_reference_units: int
    declared_features: tuple[str, ...]
    used_features: tuple[str, ...]
    excluded_features: tuple[FeatureExclusion, ...]
    reference_identity: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "reference_units": self.reference_units,
            "distinct_reference_units": self.distinct_reference_units,
            "declared_features": list(self.declared_features),
            "used_features": list(self.used_features),
            "excluded_features": [e.to_dict() for e in self.excluded_features],
            "reference_identity": self.reference_identity,
        }


@dataclass(frozen=True)
class ThresholdIdentity:
    """Where the line was drawn, and on what.

    reference_score_identity ties the threshold to the exact scores it was taken
    from. A threshold quoted without it is a number somebody remembers, and a later
    reader cannot tell whether it still applies.
    """

    method: str
    quantile: float
    value: float
    reference_score_identity: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "method": self.method,
            "quantile": self.quantile,
            "value": self.value,
            "reference_score_identity": self.reference_score_identity,
        }


@dataclass(frozen=True)
class ScoredUnit:
    unit_id: str
    score: float
    rank: int
    above_threshold: bool

    def to_dict(self) -> dict[str, Any]:
        return {
            "unit_id": self.unit_id,
            "score": self.score,
            "rank": self.rank,
            "above_threshold": self.above_threshold,
        }


@dataclass(frozen=True)
class RefusalState:
    """Present and populated exactly when no score exists."""

    refused: bool
    code: NoveltyRefusalCode
    reason: str
    required: float | None = None
    observed: float | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "refused": self.refused,
            "code": self.code.value,
            "reason": self.reason,
            "required": self.required,
            "observed": self.observed,
        }


NOT_REFUSED = RefusalState(False, NoveltyRefusalCode.NONE, "")


@dataclass(frozen=True)
class NoveltyResult:
    """One model's answer, with the four parts kept apart."""

    model_code: str
    model_class: ModelClass
    scored_units: tuple[ScoredUnit, ...]
    threshold: ThresholdIdentity | None
    population: PopulationContext | None
    refusal: RefusalState
    description: Mapping[str, Any]

    def __post_init__(self) -> None:
        if self.refusal.refused and self.scored_units:
            raise NoveltyContractError(
                "A refused result may not carry scores. A refusal exists precisely "
                "because no defensible score could be produced."
            )
        if not self.refusal.refused and self.threshold is None:
            raise NoveltyContractError(
                "A scored result must carry the threshold identity it was judged "
                "against. A score without a line is not an answer to anything."
            )

    @property
    def flagged(self) -> tuple[ScoredUnit, ...]:
        return tuple(u for u in self.scored_units if u.above_threshold)

    def to_dict(self) -> dict[str, Any]:
        return {
            "model_code": self.model_code,
            "model_class": self.model_class.value,
            "scored_units": [u.to_dict() for u in self.scored_units],
            "threshold": self.threshold.to_dict() if self.threshold else None,
            "population": self.population.to_dict() if self.population else None,
            "refusal": self.refusal.to_dict(),
            "description": dict(self.description),
        }


class NoveltyModel(ABC):
    """One novelty family behind a replaceable contract."""

    @property
    @abstractmethod
    def model_code(self) -> str:
        ...

    @property
    @abstractmethod
    def model_class(self) -> ModelClass:
        ...

    @abstractmethod
    def evaluate(
        self,
        reference_ids: Sequence[str],
        reference_rows: Sequence[Sequence[float]],
        feature_names: Sequence[str],
        quantile: float,
        seed: int,
    ) -> NoveltyResult:
        """Score the reference population against itself, or refuse with a reason."""


def population_identity(
    ids: Sequence[str], rows: Sequence[Sequence[float]], feature_names: Sequence[str]
) -> str:
    """Content identity of a reference population, order included."""
    digest = hashlib.sha256()
    digest.update(b"ppiq.novelty.reference/1\n")
    digest.update("|".join(str(n) for n in feature_names).encode("utf-8"))
    digest.update(b"\x1e")
    for unit, row in zip(ids, rows):
        digest.update(str(unit).encode("utf-8"))
        digest.update(b"\x1f")
        digest.update("|".join(repr(float(v)) for v in row).encode("ascii"))
        digest.update(b"\x1e")
    return digest.hexdigest()


def score_identity(scores: Sequence[float]) -> str:
    """Content identity of an ordered score vector, for the threshold to cite."""
    digest = hashlib.sha256()
    digest.update(b"ppiq.novelty.scores/1\n")
    for value in scores:
        digest.update(repr(float(value)).encode("ascii"))
        digest.update(b"\x1f")
    return digest.hexdigest()


def rank_units(
    ids: Sequence[str], scores: Sequence[float], threshold: float
) -> tuple[ScoredUnit, ...]:
    """Most novel first, ties broken by identifier so two runs cannot disagree."""
    order = sorted(range(len(ids)), key=lambda i: (-float(scores[i]), str(ids[i])))
    return tuple(
        ScoredUnit(
            unit_id=str(ids[position]),
            score=float(scores[position]),
            rank=place,
            above_threshold=float(scores[position]) > threshold,
        )
        for place, position in enumerate(order)
    )
