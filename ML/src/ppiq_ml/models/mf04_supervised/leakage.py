"""The leakage gate. It runs before any model is fitted, and it can stop the job.

Two questions are answered here, and both are answered from the declared outcome
contract rather than from any constant of a particular plant or product.

Is every candidate feature knowable at the prediction point? A feature whose value
becomes known after the cutoff would let a model train on future information. Such a
model scores well in evaluation and is worthless in service, which is the failure
mode that is hardest to detect after the fact and easiest to detect here.

Is this a prediction at all? If the outcome is already observable at or before the
cutoff, the caller is asking for a lookup rather than a prediction. Answering it with
a trained model would dress a known value as an estimate.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from .outcome import OutcomeDefinition


class FeatureLegality(str, Enum):
    LEGAL = "legal"
    ILLEGAL_AFTER_CUTOFF = "illegal_after_cutoff"


@dataclass(frozen=True)
class FeatureLeakageDetail:
    """One row per declared feature, whatever the verdict.

    Every feature gets a row so the caller can see what was admitted as well as what
    was rejected. A gate that reports only its rejections cannot be audited.
    """

    column: str
    available_at_ordinal: int
    cutoff_ordinal: int
    legality: FeatureLegality
    reason: str


@dataclass(frozen=True)
class LeakageVerdict:
    passed: bool
    reason: str
    legal_features: tuple[str, ...]
    illegal_features: tuple[str, ...]
    detail: tuple[FeatureLeakageDetail, ...]
    cutoff_ordinal: int
    detection_ordinal: int

    def to_dict(self) -> dict:
        return {
            "passed": self.passed,
            "reason": self.reason,
            "legal_features": list(self.legal_features),
            "illegal_features": list(self.illegal_features),
            "cutoff_ordinal": self.cutoff_ordinal,
            "detection_ordinal": self.detection_ordinal,
            "detail": [
                {
                    "column": d.column,
                    "available_at_ordinal": d.available_at_ordinal,
                    "cutoff_ordinal": d.cutoff_ordinal,
                    "legality": d.legality.value,
                    "reason": d.reason,
                }
                for d in self.detail
            ],
        }


def evaluate_leakage(outcome: OutcomeDefinition) -> LeakageVerdict:
    """Decide whether this outcome contract may be trained at all."""
    cutoff = outcome.cutoff_ordinal
    detection = outcome.detection_position_ordinal

    detail: list[FeatureLeakageDetail] = []
    legal: list[str] = []
    illegal: list[str] = []

    for feature in outcome.features:
        if feature.available_at_ordinal <= cutoff:
            legal.append(feature.column)
            detail.append(
                FeatureLeakageDetail(
                    column=feature.column,
                    available_at_ordinal=feature.available_at_ordinal,
                    cutoff_ordinal=cutoff,
                    legality=FeatureLegality.LEGAL,
                    reason=(
                        f"Known at position {feature.available_at_ordinal}, at or before "
                        f"the prediction position {cutoff}."
                    ),
                )
            )
        else:
            illegal.append(feature.column)
            detail.append(
                FeatureLeakageDetail(
                    column=feature.column,
                    available_at_ordinal=feature.available_at_ordinal,
                    cutoff_ordinal=cutoff,
                    legality=FeatureLegality.ILLEGAL_AFTER_CUTOFF,
                    reason=(
                        f"Not known until position {feature.available_at_ordinal}, which is "
                        f"after the prediction position {cutoff}. Using it would train on "
                        "future information."
                    ),
                )
            )

    if detection <= cutoff:
        return LeakageVerdict(
            passed=False,
            reason=(
                f"Outcome '{outcome.outcome_code}' is already observable at position "
                f"{detection}, at or before the prediction position {cutoff}. That is a "
                "lookup, not a prediction, and no model is trained for it."
            ),
            legal_features=tuple(legal),
            illegal_features=tuple(illegal),
            detail=tuple(detail),
            cutoff_ordinal=cutoff,
            detection_ordinal=detection,
        )

    if illegal:
        return LeakageVerdict(
            passed=False,
            reason=(
                "Training is blocked because "
                + str(len(illegal))
                + " declared feature(s) become known after the prediction position "
                + str(cutoff)
                + ": "
                + ", ".join(illegal)
                + ". A model built on them would train on future information."
            ),
            legal_features=tuple(legal),
            illegal_features=tuple(illegal),
            detail=tuple(detail),
            cutoff_ordinal=cutoff,
            detection_ordinal=detection,
        )

    return LeakageVerdict(
        passed=True,
        reason=(
            f"All {len(legal)} declared feature(s) are known at or before position "
            f"{cutoff}, and the outcome is not observable until position {detection}."
        ),
        legal_features=tuple(legal),
        illegal_features=(),
        detail=tuple(detail),
        cutoff_ordinal=cutoff,
        detection_ordinal=detection,
    )
