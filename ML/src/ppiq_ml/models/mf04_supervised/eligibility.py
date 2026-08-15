"""Whether this population may be trained on at all, and the numbers behind the answer.

Every refusal produced here names the clause that failed, what was required and what
was measured. A refusal without a number is an opinion, and a customer told that
their data is insufficient without being told how insufficient cannot act on it.

The thresholds below are declared constants rather than measured ones. They are
stated here so that a later task can replace them with values measured on real
populations, and so that nobody mistakes the current values for evidence.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Sequence

from .leakage import LeakageVerdict
from .outcome import OutcomeDefinition, OutcomeKind

#: Declared, not measured. See the module docstring.
MIN_LEGAL_FEATURES = 1
MIN_LABELLED_UNITS = 40
MIN_MINORITY_UNITS = 8
MIN_MINORITY_FRACTION = 0.02
MIN_DISTINCT_CONTINUOUS_VALUES = 10
MIN_TRAIN_UNITS = 24
MIN_HOLDOUT_UNITS = 8


class Mf04RefusalCode(str, Enum):
    """Why MF-04 declined to train. Analysis-side reasons, stated in one vocabulary."""

    NONE = "none"
    LEAKAGE_BLOCKED = "leakage_blocked"
    NO_LEGAL_FEATURES = "no_legal_features"
    TOO_FEW_LABELLED_UNITS = "too_few_labelled_units"
    SINGLE_CLASS_POPULATION = "single_class_population"
    TOO_FEW_MINORITY_UNITS = "too_few_minority_units"
    TOO_FEW_DISTINCT_VALUES = "too_few_distinct_values"
    TOO_FEW_TRAIN_UNITS = "too_few_train_units"
    TOO_FEW_HOLDOUT_UNITS = "too_few_holdout_units"


@dataclass(frozen=True)
class MeasuredClause:
    """One requirement, what it needed, and what was actually there."""

    name: str
    required: float
    observed: float
    satisfied: bool
    sentence: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "required": self.required,
            "observed": self.observed,
            "satisfied": self.satisfied,
            "sentence": self.sentence,
        }


@dataclass(frozen=True)
class EligibilityVerdict:
    eligible: bool
    code: Mf04RefusalCode
    reason: str
    clauses: tuple[MeasuredClause, ...]

    def to_dict(self) -> dict[str, Any]:
        return {
            "eligible": self.eligible,
            "code": self.code.value,
            "reason": self.reason,
            "clauses": [c.to_dict() for c in self.clauses],
        }

    @property
    def failed_clauses(self) -> tuple[MeasuredClause, ...]:
        return tuple(c for c in self.clauses if not c.satisfied)


def _clause(name: str, required: float, observed: float, sentence: str) -> MeasuredClause:
    return MeasuredClause(
        name=name,
        required=float(required),
        observed=float(observed),
        satisfied=observed >= required,
        sentence=sentence,
    )


def evaluate_eligibility(
    outcome: OutcomeDefinition,
    leakage: LeakageVerdict,
    labels: Sequence[Any],
    train_units: int,
    holdout_units: int,
) -> EligibilityVerdict:
    """Decide whether training may proceed, recording every clause it checked."""
    clauses: list[MeasuredClause] = []

    if not leakage.passed:
        return EligibilityVerdict(
            eligible=False,
            code=Mf04RefusalCode.LEAKAGE_BLOCKED,
            reason=leakage.reason,
            clauses=(
                _clause(
                    "illegal_features",
                    0,
                    -float(len(leakage.illegal_features)),
                    "No declared feature may become known after the prediction position.",
                ),
            ),
        )

    clauses.append(
        _clause(
            "legal_features",
            MIN_LEGAL_FEATURES,
            len(leakage.legal_features),
            "At least one feature must be knowable at the prediction position.",
        )
    )

    labelled = [v for v in labels if v is not None]
    clauses.append(
        _clause(
            "labelled_units",
            MIN_LABELLED_UNITS,
            len(labelled),
            "A supervised model needs a labelled population of at least the declared size.",
        )
    )

    if outcome.is_classification:
        distinct = sorted({str(v) for v in labelled})
        clauses.append(
            _clause(
                "distinct_classes",
                2,
                len(distinct),
                "A classifier needs at least two observed classes to separate.",
            )
        )
        if labelled and len(distinct) >= 2:
            counts: dict[str, int] = {}
            for value in labelled:
                counts[str(value)] = counts.get(str(value), 0) + 1
            minority = min(counts.values())
            clauses.append(
                _clause(
                    "minority_units",
                    MIN_MINORITY_UNITS,
                    minority,
                    "The rarest class must carry enough units to be learned rather than "
                    "memorised.",
                )
            )
            clauses.append(
                _clause(
                    "minority_fraction",
                    MIN_MINORITY_FRACTION,
                    minority / len(labelled),
                    "The rarest class must not be so rare that the population is "
                    "effectively single class.",
                )
            )
    else:
        distinct_values = len({float(v) for v in labelled}) if labelled else 0
        clauses.append(
            _clause(
                "distinct_outcome_values",
                MIN_DISTINCT_CONTINUOUS_VALUES,
                distinct_values,
                "A continuous outcome needs enough distinct values to carry variance.",
            )
        )

    clauses.append(
        _clause(
            "train_units",
            MIN_TRAIN_UNITS,
            train_units,
            "The training part of the split must be large enough to fit on.",
        )
    )
    clauses.append(
        _clause(
            "holdout_units",
            MIN_HOLDOUT_UNITS,
            holdout_units,
            "The holdout must be large enough for its metrics to mean anything.",
        )
    )

    failed = [c for c in clauses if not c.satisfied]
    if not failed:
        return EligibilityVerdict(
            eligible=True,
            code=Mf04RefusalCode.NONE,
            reason=(
                f"Outcome '{outcome.outcome_code}' satisfies all {len(clauses)} eligibility "
                f"clauses on {len(labelled)} labelled unit(s)."
            ),
            clauses=tuple(clauses),
        )

    first = failed[0]
    code = _code_for(first.name, outcome.kind)
    reason = (
        f"Outcome '{outcome.outcome_code}' is not eligible for supervised training. "
        + " ".join(
            f"{c.name}: required {_format(c.required)}, observed {_format(c.observed)}."
            for c in failed
        )
    )
    return EligibilityVerdict(False, code, reason, tuple(clauses))


def _format(value: float) -> str:
    if value == int(value):
        return str(int(value))
    return f"{value:.4f}"


def _code_for(clause_name: str, kind: OutcomeKind) -> Mf04RefusalCode:
    mapping = {
        "legal_features": Mf04RefusalCode.NO_LEGAL_FEATURES,
        "labelled_units": Mf04RefusalCode.TOO_FEW_LABELLED_UNITS,
        "distinct_classes": Mf04RefusalCode.SINGLE_CLASS_POPULATION,
        "minority_units": Mf04RefusalCode.TOO_FEW_MINORITY_UNITS,
        "minority_fraction": Mf04RefusalCode.TOO_FEW_MINORITY_UNITS,
        "distinct_outcome_values": Mf04RefusalCode.TOO_FEW_DISTINCT_VALUES,
        "train_units": Mf04RefusalCode.TOO_FEW_TRAIN_UNITS,
        "holdout_units": Mf04RefusalCode.TOO_FEW_HOLDOUT_UNITS,
    }
    return mapping.get(clause_name, Mf04RefusalCode.TOO_FEW_LABELLED_UNITS)
