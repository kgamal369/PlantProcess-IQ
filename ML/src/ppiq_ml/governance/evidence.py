"""The frozen metrics document a promotion decision is made from.

WHY A DOCUMENT AND NOT A MODEL. This kernel never touches a fitted model, a training
run or a customer's data. It reads recorded measurements and decides. That boundary
is what makes a decision reproducible a year later, when the model that produced the
numbers no longer exists and the machine that served it has been replaced.

WHAT A MISSING NUMBER MEANS. A budget declared without a measurement to compare it
against makes the decision unevaluable. It never defaults, and it is never quietly
dropped from the check set, because a threshold that disappears when unmeasured is a
gate that passes by being unmeasured.

Both sides of a comparison must carry the same snapshot and holdout identity. Two
models measured on different populations are not comparable, and a kernel that
compared them anyway would produce a confident answer to a question nobody asked.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping, Sequence

DOCUMENT_VERSION = "ppiq.promotion.document/1"

#: The initial explanation candidate. It is a candidate, not a product contract:
#: the kernel judges attribution vectors and does not care which library produced
#: them, so a later task may replace the producer without touching this module.
EXPLANATION_METHOD_INITIAL_CANDIDATE = "treeshap"


class EvidenceError(Exception):
    """The document cannot be interpreted as a promotion evidence document."""


class CandidateClass(str, Enum):
    """What kind of path produced the numbers.

    The distinction matters because the frozen rule treats an encoder differently
    from an engineered-feature path: the encoder must earn its cost.
    """

    ENGINEERED_FEATURES = "engineered_features"
    ENCODER = "encoder"


@dataclass(frozen=True)
class ExplanationEvidence:
    """Attribution vectors from repeated explanation runs, and their producer.

    One vector per repeat, each aligned to feature_names. Stability is computed
    from these rather than asserted, so an explanation that moves between runs
    cannot be reported as stable.
    """

    method: str
    feature_names: tuple[str, ...]
    attributions: tuple[tuple[float, ...], ...]

    def __post_init__(self) -> None:
        if not self.method.strip():
            raise EvidenceError("Explanation evidence must name the method that produced it.")
        if len(self.attributions) < 2:
            raise EvidenceError(
                "Explanation stability needs at least two repeats. One vector cannot "
                "disagree with itself, and reporting it as stable would be a claim about "
                "a measurement that was never taken."
            )
        for index, vector in enumerate(self.attributions):
            if len(vector) != len(self.feature_names):
                raise EvidenceError(
                    f"Attribution vector {index} carries {len(vector)} values but "
                    f"{len(self.feature_names)} feature names were declared."
                )

    def to_dict(self) -> dict[str, Any]:
        return {
            "method": self.method,
            "feature_names": list(self.feature_names),
            "attributions": [list(v) for v in self.attributions],
        }


@dataclass(frozen=True)
class QualityEvidence:
    """Discrimination or error, probability quality, robustness and explanations."""

    primary_metric_name: str
    primary_metric: float
    primary_higher_is_better: bool
    proper_score_name: str
    proper_score: float
    calibration_error: float
    out_of_time_primary_metric: float
    subgroup_primary_metrics: Mapping[str, float]
    missingness_primary_metric: float
    explanation: ExplanationEvidence | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "primary_metric_name": self.primary_metric_name,
            "primary_metric": self.primary_metric,
            "primary_higher_is_better": self.primary_higher_is_better,
            "proper_score_name": self.proper_score_name,
            "proper_score": self.proper_score,
            "calibration_error": self.calibration_error,
            "out_of_time_primary_metric": self.out_of_time_primary_metric,
            "subgroup_primary_metrics": dict(sorted(self.subgroup_primary_metrics.items())),
            "missingness_primary_metric": self.missingness_primary_metric,
            "explanation": self.explanation.to_dict() if self.explanation else None,
        }


@dataclass(frozen=True)
class ServingEvidence:
    """What it costs to answer with this model."""

    p50_latency_ms: float
    p95_latency_ms: float
    p99_latency_ms: float
    throughput_per_second: float
    artifact_size_bytes: int
    resident_memory_mb: float
    warm_up_seconds: float
    accelerator_memory_mb: float | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "p50_latency_ms": self.p50_latency_ms,
            "p95_latency_ms": self.p95_latency_ms,
            "p99_latency_ms": self.p99_latency_ms,
            "throughput_per_second": self.throughput_per_second,
            "artifact_size_bytes": self.artifact_size_bytes,
            "resident_memory_mb": self.resident_memory_mb,
            "warm_up_seconds": self.warm_up_seconds,
            "accelerator_memory_mb": self.accelerator_memory_mb,
        }


@dataclass(frozen=True)
class TrainingEvidence:
    """What it costs to produce this model."""

    training_seconds: float
    peak_memory_mb: float
    snapshot_rows_per_second: float

    def to_dict(self) -> dict[str, Any]:
        return {
            "training_seconds": self.training_seconds,
            "peak_memory_mb": self.peak_memory_mb,
            "snapshot_rows_per_second": self.snapshot_rows_per_second,
        }


@dataclass(frozen=True)
class CandidateEvidence:
    """One path, measured on all three dimensions."""

    candidate_code: str
    candidate_class: CandidateClass
    quality: QualityEvidence
    serving: ServingEvidence
    training: TrainingEvidence
    snapshot_identity: str
    holdout_identity: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "candidate_code": self.candidate_code,
            "candidate_class": self.candidate_class.value,
            "quality": self.quality.to_dict(),
            "serving": self.serving.to_dict(),
            "training": self.training.to_dict(),
            "snapshot_identity": self.snapshot_identity,
            "holdout_identity": self.holdout_identity,
        }


@dataclass(frozen=True)
class DeclaredThresholds:
    """Every budget, declared before the numbers are seen.

    These are declared values, not measured ones. A later task may replace them with
    values measured on real populations and real hardware; nobody should mistake the
    current numbers for evidence.
    """

    # QUALITY
    min_primary_metric: float
    max_calibration_error: float
    max_out_of_time_drop: float
    max_subgroup_spread: float
    max_missingness_drop: float
    min_explanation_rank_agreement: float
    min_explanation_top_k_overlap: float
    explanation_top_k: int
    # SERVING
    max_p50_latency_ms: float
    max_p95_latency_ms: float
    max_p99_latency_ms: float
    min_throughput_per_second: float
    max_artifact_size_bytes: int
    max_resident_memory_mb: float
    max_warm_up_seconds: float
    max_accelerator_memory_mb: float | None
    # TRAINING
    max_training_seconds: float
    max_peak_memory_mb: float
    min_snapshot_rows_per_second: float
    # THE FROZEN ENCODER INEQUALITY
    declared_min_lift: float
    declared_latency_budget_ms: float
    declared_size_class_bytes: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "min_primary_metric": self.min_primary_metric,
            "max_calibration_error": self.max_calibration_error,
            "max_out_of_time_drop": self.max_out_of_time_drop,
            "max_subgroup_spread": self.max_subgroup_spread,
            "max_missingness_drop": self.max_missingness_drop,
            "min_explanation_rank_agreement": self.min_explanation_rank_agreement,
            "min_explanation_top_k_overlap": self.min_explanation_top_k_overlap,
            "explanation_top_k": self.explanation_top_k,
            "max_p50_latency_ms": self.max_p50_latency_ms,
            "max_p95_latency_ms": self.max_p95_latency_ms,
            "max_p99_latency_ms": self.max_p99_latency_ms,
            "min_throughput_per_second": self.min_throughput_per_second,
            "max_artifact_size_bytes": self.max_artifact_size_bytes,
            "max_resident_memory_mb": self.max_resident_memory_mb,
            "max_warm_up_seconds": self.max_warm_up_seconds,
            "max_accelerator_memory_mb": self.max_accelerator_memory_mb,
            "max_training_seconds": self.max_training_seconds,
            "max_peak_memory_mb": self.max_peak_memory_mb,
            "min_snapshot_rows_per_second": self.min_snapshot_rows_per_second,
            "declared_min_lift": self.declared_min_lift,
            "declared_latency_budget_ms": self.declared_latency_budget_ms,
            "declared_size_class_bytes": self.declared_size_class_bytes,
        }


@dataclass(frozen=True)
class PromotionDocument:
    """The incumbent, the challenger and the budgets. Nothing else is consulted."""

    document_version: str
    incumbent: CandidateEvidence
    challenger: CandidateEvidence
    thresholds: DeclaredThresholds

    def to_dict(self) -> dict[str, Any]:
        return {
            "document_version": self.document_version,
            "incumbent": self.incumbent.to_dict(),
            "challenger": self.challenger.to_dict(),
            "thresholds": self.thresholds.to_dict(),
        }

    def canonical_json(self) -> str:
        return json.dumps(self.to_dict(), indent=2, sort_keys=True)

    def document_identity(self) -> str:
        """Identity of the evidence, so a decision can name what it was made from."""
        return hashlib.sha256(self.canonical_json().encode("ascii")).hexdigest()


def build_document(
    incumbent: CandidateEvidence,
    challenger: CandidateEvidence,
    thresholds: DeclaredThresholds,
) -> PromotionDocument:
    return PromotionDocument(DOCUMENT_VERSION, incumbent, challenger, thresholds)


def lift(challenger_value: float, incumbent_value: float, higher_is_better: bool) -> float:
    """How much better the challenger is, signed so that positive always means better."""
    if higher_is_better:
        return float(challenger_value) - float(incumbent_value)
    return float(incumbent_value) - float(challenger_value)


def _require(raw: Mapping[str, Any], keys: Sequence[str], what: str) -> None:
    missing = [k for k in keys if k not in raw]
    if missing:
        raise EvidenceError(f"The {what} is missing required fields: {', '.join(sorted(missing))}.")
