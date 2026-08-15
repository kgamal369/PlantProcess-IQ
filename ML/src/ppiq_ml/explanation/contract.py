"""What an explanation is, and what may be claimed from one.

THE CLAIM THIS EVIDENCE MAKES, AND THE ONE IT DOES NOT. A contribution says how much
a feature moved this model's output for this unit. It does not say that changing the
feature would change the outcome in the plant. Those are different statements, and a
product that presents the first as the second has told a customer to act on
something it never measured. The claim class is therefore carried on every record,
there is exactly one of them, and it is named for what it actually is.

WHY A PROVIDER BOUNDARY. TreeSHAP is the initial candidate for producing these
numbers. It is not a product contract, and a later task may replace it with another
method for another model family. So the producer sits behind this interface, the
promotion kernel consumes only the evidence, and neither knows the other's library.
"""

from __future__ import annotations

import hashlib
import json
from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum
from typing import Any, Sequence

#: The initial candidate. A string on a record, not a dependency in an import.
TREESHAP_METHOD = "treeshap"


class ExplanationError(Exception):
    """The explanation cannot be produced or cannot be interpreted."""


class ExplanationUnavailableError(ExplanationError):
    """The provider cannot run in this environment, for a stated reason."""


class ClaimClass(str, Enum):
    """What an explanation record is entitled to assert.

    One member, deliberately. A second one naming causation would be an invitation
    to write it into a record, and no method behind this interface measures it.
    """

    PREDICTIVE_CONTRIBUTION = "PREDICTIVE_CONTRIBUTION"


class ContributionScale(str, Enum):
    """The scale the contributions are expressed on.

    Tree contributions sum to the model's raw output, not to a probability. Recording
    the scale prevents a reader from adding a contribution to a probability and
    getting a number that looks plausible and means nothing.
    """

    RAW_MODEL_OUTPUT = "raw_model_output"


@dataclass(frozen=True)
class EvidenceIdentity:
    """What this evidence is about, so a later reader can tie it to its source."""

    model_identity: str
    artifact_identity: str
    snapshot_identity: str
    holdout_identity: str | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "model_identity": self.model_identity,
            "artifact_identity": self.artifact_identity,
            "snapshot_identity": self.snapshot_identity,
            "holdout_identity": self.holdout_identity,
        }


@dataclass(frozen=True)
class ContributionEvidence:
    """Per-unit, per-feature contributions plus the base value they extend."""

    explanation_method: str
    claim_class: ClaimClass
    contribution_scale: ContributionScale
    identity: EvidenceIdentity
    feature_names: tuple[str, ...]
    contributions: tuple[tuple[float, ...], ...]
    base_values: tuple[float, ...]
    output_index: int = 0

    def __post_init__(self) -> None:
        if not self.explanation_method.strip():
            raise ExplanationError("Contribution evidence must name the method that produced it.")
        if self.claim_class != ClaimClass.PREDICTIVE_CONTRIBUTION:
            raise ExplanationError(
                "The only claim this evidence supports is a predictive contribution."
            )
        if not self.feature_names:
            raise ExplanationError("Contribution evidence must name its features.")
        if not self.contributions:
            raise ExplanationError("Contribution evidence must carry at least one unit.")
        if len(self.base_values) != len(self.contributions):
            raise ExplanationError(
                f"There are {len(self.contributions)} contribution rows and "
                f"{len(self.base_values)} base values. Every row extends its own base."
            )
        width = len(self.feature_names)
        for index, row in enumerate(self.contributions):
            if len(row) != width:
                raise ExplanationError(
                    f"Contribution row {index} carries {len(row)} values against "
                    f"{width} declared feature name(s). A contribution that does not "
                    "line up with a feature explains nothing."
                )

    @property
    def unit_count(self) -> int:
        return len(self.contributions)

    def reconstructed_output(self, row_index: int) -> float:
        """Base value plus every contribution. Equals the model's raw output.

        This is the property that makes the evidence checkable: a fabricated table
        of numbers does not reconstruct a model's output, and a real one does.
        """
        return float(self.base_values[row_index]) + sum(
            float(v) for v in self.contributions[row_index]
        )

    def mean_absolute_contributions(self) -> tuple[float, ...]:
        """One attribution vector per explanation run, for the stability kernel.

        Magnitudes are averaged across units because stability asks whether the model
        keeps saying the same features matter, not whether one unit is unusual.
        """
        units = len(self.contributions)
        return tuple(
            sum(abs(float(row[position])) for row in self.contributions) / units
            for position in range(len(self.feature_names))
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "explanation_method": self.explanation_method,
            "claim_class": self.claim_class.value,
            "contribution_scale": self.contribution_scale.value,
            "identity": self.identity.to_dict(),
            "feature_names": list(self.feature_names),
            "contributions": [list(row) for row in self.contributions],
            "base_values": list(self.base_values),
            "output_index": self.output_index,
            "unit_count": self.unit_count,
        }

    def evidence_identity(self) -> str:
        return hashlib.sha256(
            json.dumps(self.to_dict(), indent=2, sort_keys=True).encode("ascii")
        ).hexdigest()


class ExplanationProvider(ABC):
    """One way of producing contribution evidence. Replaceable by construction."""

    @property
    @abstractmethod
    def method(self) -> str:
        """The identifier written onto every record this provider produces."""

    @abstractmethod
    def supports(self, model: Any) -> bool:
        """Whether this provider can explain the given fitted model."""

    @abstractmethod
    def explain(
        self,
        model: Any,
        feature_rows: Sequence[Sequence[Any]],
        feature_names: Sequence[str],
        identity: EvidenceIdentity,
        output_index: int = 0,
    ) -> ContributionEvidence:
        """Produce contribution evidence for the supplied units."""
