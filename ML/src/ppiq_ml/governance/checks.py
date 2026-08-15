"""One check, one number, one sentence.

Every gate in this kernel produces a record of what was required, what was observed
and whether the two are compatible. A gate that reports only pass or fail cannot be
argued with, and a decision nobody can argue with is a decision nobody can audit.

An unmeasured value is not a failure and not a pass. It is UNMEASURED, and it makes
the whole decision unevaluable, because a budget with nothing to compare against is
not a gate at all.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Sequence


class Direction(str, Enum):
    AT_LEAST = "at_least"
    AT_MOST = "at_most"


class CheckState(str, Enum):
    SATISFIED = "satisfied"
    FAILED = "failed"
    UNMEASURED = "unmeasured"


class Dimension(str, Enum):
    """Three independent dimensions. They are never merged into one number."""

    QUALITY = "quality"
    SERVING = "serving"
    TRAINING = "training"


@dataclass(frozen=True)
class MeasuredCheck:
    dimension: Dimension
    name: str
    direction: Direction
    required: float | None
    observed: float | None
    state: CheckState
    sentence: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "dimension": self.dimension.value,
            "name": self.name,
            "direction": self.direction.value,
            "required": self.required,
            "observed": self.observed,
            "state": self.state.value,
            "sentence": self.sentence,
        }


def check(
    dimension: Dimension,
    name: str,
    direction: Direction,
    required: float | None,
    observed: float | None,
    sentence: str,
) -> MeasuredCheck:
    """Build one check. A missing budget or a missing measurement is UNMEASURED."""
    if required is None or observed is None:
        return MeasuredCheck(
            dimension, name, direction, required, observed, CheckState.UNMEASURED, sentence
        )
    if direction == Direction.AT_LEAST:
        satisfied = float(observed) >= float(required)
    else:
        satisfied = float(observed) <= float(required)
    state = CheckState.SATISFIED if satisfied else CheckState.FAILED
    return MeasuredCheck(dimension, name, direction, required, observed, state, sentence)


@dataclass(frozen=True)
class DimensionVerdict:
    """One dimension's answer, with every check it ran."""

    dimension: Dimension
    checks: tuple[MeasuredCheck, ...]

    @property
    def failed(self) -> tuple[MeasuredCheck, ...]:
        return tuple(c for c in self.checks if c.state == CheckState.FAILED)

    @property
    def unmeasured(self) -> tuple[MeasuredCheck, ...]:
        return tuple(c for c in self.checks if c.state == CheckState.UNMEASURED)

    @property
    def passed(self) -> bool:
        return not self.failed and not self.unmeasured

    def to_dict(self) -> dict[str, Any]:
        return {
            "dimension": self.dimension.value,
            "passed": self.passed,
            "failed_checks": [c.name for c in self.failed],
            "unmeasured_checks": [c.name for c in self.unmeasured],
            "checks": [c.to_dict() for c in self.checks],
        }


def render(value: float | None) -> str:
    if value is None:
        return "unmeasured"
    if float(value) == int(float(value)):
        return str(int(float(value)))
    return f"{float(value):.6g}"


def describe_failures(checks: Sequence[MeasuredCheck]) -> str:
    """A sentence naming each failure with both of its numbers."""
    parts = []
    for c in checks:
        comparator = "at least" if c.direction == Direction.AT_LEAST else "at most"
        parts.append(
            f"{c.dimension.value}.{c.name} required {comparator} {render(c.required)}, "
            f"observed {render(c.observed)}"
        )
    return "; ".join(parts)
