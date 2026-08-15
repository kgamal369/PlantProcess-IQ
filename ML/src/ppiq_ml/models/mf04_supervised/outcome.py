"""The typed outcome contract MF-04 trains against.

WHAT THIS IS NOT. This module is not the production owner of outcome semantics.
During SAFE-NOW work an outcome definition, its prediction point and its detection
position are fixture-declared typed contracts supplied by the caller. Production
supervised training consumes the canonical published SM-06 OutcomeDefinition only
after its M2a binding is certified, and that binding belongs to its existing owners.

WHY THE CONTRACT LOOKS LIKE THIS. Everything MF-04 needs in order to decide whether
a training population is a prediction or a lookup is a position on a declared
process order: where a feature becomes known, where the caller wants to predict,
and where the outcome becomes observable. Those three ordinals are the whole of the
leakage question, and they are declared here rather than inferred anywhere.

The ordinals are opaque integers on a caller-declared order. This module attaches no
meaning to any particular value and carries no vocabulary from any industry.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping


class OutcomeContractError(Exception):
    """The declared outcome contract is not internally consistent."""


class OutcomeKind(str, Enum):
    """The four supervised shapes MF-04 supports.

    ORDINAL is separate from MULTICLASS because the classes carry a declared rank.
    A model that confuses adjacent ranks is not making the same error as one that
    confuses the extremes, and reporting both as accuracy alone would hide that.
    """

    BINARY = "binary"
    MULTICLASS = "multiclass"
    ORDINAL = "ordinal"
    CONTINUOUS = "continuous"


@dataclass(frozen=True)
class FeatureDeclaration:
    """One candidate feature and the position at which its value becomes known.

    available_at_ordinal is the earliest position on the declared order at which the
    value exists. A feature that becomes known after the prediction point cannot be
    used, whatever its predictive strength.
    """

    column: str
    available_at_ordinal: int
    is_controllable: bool = False

    def __post_init__(self) -> None:
        if not self.column or not self.column.strip():
            raise OutcomeContractError("A feature declaration must name a column.")


@dataclass(frozen=True)
class PredictionPoint:
    """Where the caller intends to act, expressed on the same declared order."""

    code: str
    position_ordinal: int

    def __post_init__(self) -> None:
        if not self.code or not self.code.strip():
            raise OutcomeContractError("A prediction point must carry a code.")


@dataclass(frozen=True)
class OutcomeDefinition:
    """A typed, fixture-declared outcome. Not production authority.

    grain_column identifies the unit of observation. order_column carries the value
    the out-of-time split is taken on. label_column carries the outcome value.
    """

    outcome_code: str
    kind: OutcomeKind
    grain_column: str
    order_column: str
    label_column: str
    detection_position_ordinal: int
    prediction_point: PredictionPoint
    features: tuple[FeatureDeclaration, ...]
    positive_class: Any = None
    class_order: tuple[Any, ...] | None = None

    def __post_init__(self) -> None:
        if not self.outcome_code or not self.outcome_code.strip():
            raise OutcomeContractError("An outcome definition must carry an outcome code.")
        if not self.features:
            raise OutcomeContractError(
                f"Outcome '{self.outcome_code}' declares no candidate features. There is "
                "nothing to train on and nothing to refuse about."
            )
        names = [f.column for f in self.features]
        duplicates = sorted({n for n in names if names.count(n) > 1})
        if duplicates:
            raise OutcomeContractError(
                f"Outcome '{self.outcome_code}' declares duplicate feature columns: "
                + ", ".join(duplicates)
            )
        for reserved, role in (
            (self.grain_column, "grain"),
            (self.order_column, "order"),
            (self.label_column, "label"),
        ):
            if reserved in names:
                raise OutcomeContractError(
                    f"Column '{reserved}' is declared as the {role} column and also as a "
                    "feature. A structural column is not evidence about itself."
                )
        if self.kind == OutcomeKind.ORDINAL:
            if not self.class_order or len(self.class_order) < 3:
                raise OutcomeContractError(
                    f"Outcome '{self.outcome_code}' is ordinal and must declare a class "
                    "order of at least three ranks. Without a declared rank an ordinal "
                    "outcome is indistinguishable from a multiclass one."
                )
        if self.kind == OutcomeKind.BINARY and self.positive_class is None:
            raise OutcomeContractError(
                f"Outcome '{self.outcome_code}' is binary and must declare which value is "
                "the positive class. Inferring it from row order would make the reported "
                "prevalence depend on which rows arrived first."
            )
        if self.kind == OutcomeKind.CONTINUOUS and self.class_order:
            raise OutcomeContractError(
                f"Outcome '{self.outcome_code}' is continuous and may not declare classes."
            )

    @property
    def cutoff_ordinal(self) -> int:
        """The position after which no feature value may be used."""
        return self.prediction_point.position_ordinal

    @property
    def is_classification(self) -> bool:
        return self.kind != OutcomeKind.CONTINUOUS

    @property
    def feature_columns(self) -> tuple[str, ...]:
        return tuple(f.column for f in self.features)

    def required_columns(self) -> tuple[str, ...]:
        """Every column the artifact must carry, structural columns first."""
        return (self.grain_column, self.order_column, self.label_column) + self.feature_columns

    def to_dict(self) -> dict[str, Any]:
        return {
            "outcome_code": self.outcome_code,
            "kind": self.kind.value,
            "grain_column": self.grain_column,
            "order_column": self.order_column,
            "label_column": self.label_column,
            "detection_position_ordinal": self.detection_position_ordinal,
            "prediction_point": {
                "code": self.prediction_point.code,
                "position_ordinal": self.prediction_point.position_ordinal,
            },
            "features": [
                {
                    "column": f.column,
                    "available_at_ordinal": f.available_at_ordinal,
                    "is_controllable": f.is_controllable,
                }
                for f in self.features
            ],
            "positive_class": self.positive_class,
            "class_order": list(self.class_order) if self.class_order else None,
        }

    @staticmethod
    def from_dict(raw: Mapping[str, Any]) -> "OutcomeDefinition":
        """Rebuild a declaration from a job spec parameter block.

        Every field is read explicitly. A missing field is an error rather than a
        default, because a defaulted cutoff would silently authorise a leak.
        """
        if not isinstance(raw, Mapping):
            raise OutcomeContractError("The outcome definition is not an object.")
        required = (
            "outcome_code", "kind", "grain_column", "order_column", "label_column",
            "detection_position_ordinal", "prediction_point", "features",
        )
        missing = [k for k in required if k not in raw]
        if missing:
            raise OutcomeContractError(
                "The outcome definition is missing required fields: "
                + ", ".join(sorted(missing))
            )
        point = raw["prediction_point"]
        if not isinstance(point, Mapping) or "position_ordinal" not in point:
            raise OutcomeContractError(
                "The outcome definition declares no prediction position. Without it the "
                "runtime cannot tell a prediction from a lookup."
            )
        kinds = {k.value: k for k in OutcomeKind}
        declared_kind = str(raw["kind"])
        if declared_kind not in kinds:
            raise OutcomeContractError(
                f"Unknown outcome kind '{declared_kind}'. Supported: "
                + ", ".join(sorted(kinds))
            )
        class_order = raw.get("class_order")
        return OutcomeDefinition(
            outcome_code=str(raw["outcome_code"]),
            kind=kinds[declared_kind],
            grain_column=str(raw["grain_column"]),
            order_column=str(raw["order_column"]),
            label_column=str(raw["label_column"]),
            detection_position_ordinal=int(raw["detection_position_ordinal"]),
            prediction_point=PredictionPoint(
                code=str(point.get("code", "")),
                position_ordinal=int(point["position_ordinal"]),
            ),
            features=tuple(
                FeatureDeclaration(
                    column=str(f["column"]),
                    available_at_ordinal=int(f["available_at_ordinal"]),
                    is_controllable=bool(f.get("is_controllable", False)),
                )
                for f in raw["features"]
            ),
            positive_class=raw.get("positive_class"),
            class_order=tuple(class_order) if class_order else None,
        )
