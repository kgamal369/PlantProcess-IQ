"""The promotion decision. Pure, reproducible, and it names what failed.

THE ORDER OF THE ANSWER.

    document integrity  ->  three dimensions, all evaluated  ->  the encoder rule

Integrity comes first because two candidates measured on different populations are
not comparable, and a decision made anyway would be confident and meaningless.

All three dimensions are then evaluated, even after one has failed, so the report
names every dimension that failed rather than the first one encountered.

The encoder inequality is applied last and only to an encoder challenger. When its
lift clause fails, the simpler engineered-feature path wins by the rule.

WHAT THIS KERNEL NEVER DOES. It computes no total, applies no weight and performs no
arithmetic between dimensions. It writes nothing, reads no file, opens no connection
and activates no registry. It takes a document and returns a decision, which is what
makes the same document produce the same decision on any machine in any year.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from enum import Enum
from typing import Any

from .checks import CheckState, Dimension, DimensionVerdict, describe_failures
from .dimensions import evaluate_quality, evaluate_serving, evaluate_training
from .encoder_rule import EncoderRuleVerdict, evaluate_encoder_rule
from .evidence import PromotionDocument
from .stability import ExplanationStability

DECISION_VERSION = "ppiq.promotion.decision/1"


class PromotionOutcome(str, Enum):
    """Four honest answers. None of them is a score."""

    CHALLENGER_APPROVED = "challenger_approved"
    CHALLENGER_REJECTED = "challenger_rejected"
    SIMPLER_ALTERNATIVE_RETAINED = "simpler_alternative_retained"
    NOT_EVALUABLE = "not_evaluable"


@dataclass(frozen=True)
class PromotionDecision:
    decision_version: str
    outcome: PromotionOutcome
    reason: str
    failed_dimensions: tuple[str, ...]
    failed_checks: tuple[str, ...]
    unmeasured_checks: tuple[str, ...]
    dimensions: tuple[DimensionVerdict, ...]
    encoder_rule: EncoderRuleVerdict | None
    explanation_stability: ExplanationStability | None
    document_identity: str
    challenger_code: str
    incumbent_code: str
    #: Stated in the document itself so a reader never has to ask.
    weighted_score_used: bool = False

    def to_dict(self) -> dict[str, Any]:
        return {
            "decision_version": self.decision_version,
            "outcome": self.outcome.value,
            "reason": self.reason,
            "failed_dimensions": list(self.failed_dimensions),
            "failed_checks": list(self.failed_checks),
            "unmeasured_checks": list(self.unmeasured_checks),
            "dimensions": [d.to_dict() for d in self.dimensions],
            "encoder_rule": self.encoder_rule.to_dict() if self.encoder_rule else None,
            "explanation_stability": (
                self.explanation_stability.to_dict() if self.explanation_stability else None
            ),
            "document_identity": self.document_identity,
            "challenger_code": self.challenger_code,
            "incumbent_code": self.incumbent_code,
            "weighted_score_used": self.weighted_score_used,
        }

    def to_json(self) -> str:
        return json.dumps(self.to_dict(), indent=2, sort_keys=True)

    def decision_identity(self) -> str:
        return hashlib.sha256(self.to_json().encode("ascii")).hexdigest()


def _not_evaluable(document: PromotionDocument, reason: str) -> PromotionDecision:
    return PromotionDecision(
        decision_version=DECISION_VERSION,
        outcome=PromotionOutcome.NOT_EVALUABLE,
        reason=reason,
        failed_dimensions=(),
        failed_checks=(),
        unmeasured_checks=(),
        dimensions=(),
        encoder_rule=None,
        explanation_stability=None,
        document_identity=document.document_identity(),
        challenger_code=document.challenger.candidate_code,
        incumbent_code=document.incumbent.candidate_code,
    )


def decide(document: PromotionDocument) -> PromotionDecision:
    """Evaluate one frozen metrics document and return one reproducible decision."""
    challenger = document.challenger
    incumbent = document.incumbent
    identity = document.document_identity()

    if challenger.snapshot_identity != incumbent.snapshot_identity:
        return _not_evaluable(
            document,
            "The challenger and the incumbent were measured on different snapshots, so "
            "their numbers are not comparable and no decision is made from them.",
        )
    if challenger.holdout_identity != incumbent.holdout_identity:
        return _not_evaluable(
            document,
            "The challenger and the incumbent were evaluated on different holdouts, so "
            "their numbers are not comparable and no decision is made from them.",
        )
    if challenger.quality.primary_metric_name != incumbent.quality.primary_metric_name:
        return _not_evaluable(
            document,
            f"The challenger reports '{challenger.quality.primary_metric_name}' and the "
            f"incumbent reports '{incumbent.quality.primary_metric_name}'. Two different "
            "metrics cannot be subtracted from each other.",
        )

    quality, stability = evaluate_quality(challenger, incumbent, document.thresholds)
    serving = evaluate_serving(challenger, document.thresholds)
    training = evaluate_training(challenger, document.thresholds)
    verdicts = (quality, serving, training)

    unmeasured = tuple(
        f"{v.dimension.value}.{c.name}" for v in verdicts for c in v.unmeasured
    )
    if unmeasured:
        decision = _not_evaluable(
            document,
            "The decision cannot be made because "
            + str(len(unmeasured))
            + " declared check(s) have no measurement: "
            + ", ".join(unmeasured)
            + ". A budget with nothing to compare against is not a gate.",
        )
        return PromotionDecision(
            decision_version=DECISION_VERSION,
            outcome=PromotionOutcome.NOT_EVALUABLE,
            reason=decision.reason,
            failed_dimensions=(),
            failed_checks=(),
            unmeasured_checks=unmeasured,
            dimensions=verdicts,
            encoder_rule=None,
            explanation_stability=stability,
            document_identity=identity,
            challenger_code=challenger.candidate_code,
            incumbent_code=incumbent.candidate_code,
        )

    failed_dimensions = tuple(v.dimension.value for v in verdicts if v.failed)
    failed_checks = tuple(
        f"{v.dimension.value}.{c.name}" for v in verdicts for c in v.failed
    )

    encoder_rule = evaluate_encoder_rule(
        challenger,
        incumbent,
        document.thresholds,
        stability.rank_agreement if stability else None,
    )

    if failed_dimensions:
        every_failure = [c for v in verdicts for c in v.failed]
        return PromotionDecision(
            decision_version=DECISION_VERSION,
            outcome=PromotionOutcome.CHALLENGER_REJECTED,
            reason=(
                f"Challenger '{challenger.candidate_code}' is refused on "
                + str(len(failed_dimensions))
                + " dimension(s): "
                + ", ".join(failed_dimensions)
                + ". "
                + describe_failures(every_failure)
                + ". No result on another dimension compensates for these."
            ),
            failed_dimensions=failed_dimensions,
            failed_checks=failed_checks,
            unmeasured_checks=(),
            dimensions=verdicts,
            encoder_rule=encoder_rule,
            explanation_stability=stability,
            document_identity=identity,
            challenger_code=challenger.candidate_code,
            incumbent_code=incumbent.candidate_code,
        )

    if encoder_rule.applicable and not encoder_rule.promote_encoder:
        if encoder_rule.simpler_path_wins:
            outcome = PromotionOutcome.SIMPLER_ALTERNATIVE_RETAINED
            reason = (
                f"Encoder '{challenger.candidate_code}' clears every dimension but lifts "
                f"{challenger.quality.primary_metric_name} by only "
                + describe_lift(encoder_rule.metric_lift)
                + f" over '{incumbent.candidate_code}', against a declared minimum of "
                + describe_lift(document.thresholds.declared_min_lift)
                + ". The simpler engineered-feature path wins by the declared rule, "
                "because an encoder that is not materially better is not worth its cost."
            )
        else:
            outcome = PromotionOutcome.CHALLENGER_REJECTED
            reason = (
                f"Encoder '{challenger.candidate_code}' clears every dimension but fails "
                "the promotion inequality on: "
                + ", ".join(encoder_rule.failed_clauses)
                + ". The inequality is a conjunction and no clause is tradeable."
            )
        return PromotionDecision(
            decision_version=DECISION_VERSION,
            outcome=outcome,
            reason=reason,
            failed_dimensions=(),
            failed_checks=tuple(
                "encoder_rule." + name for name in encoder_rule.failed_clauses
            ),
            unmeasured_checks=(),
            dimensions=verdicts,
            encoder_rule=encoder_rule,
            explanation_stability=stability,
            document_identity=identity,
            challenger_code=challenger.candidate_code,
            incumbent_code=incumbent.candidate_code,
        )

    return PromotionDecision(
        decision_version=DECISION_VERSION,
        outcome=PromotionOutcome.CHALLENGER_APPROVED,
        reason=(
            f"Challenger '{challenger.candidate_code}' satisfies every check on all three "
            "dimensions and, where it applies, the promotion inequality."
        ),
        failed_dimensions=(),
        failed_checks=(),
        unmeasured_checks=(),
        dimensions=verdicts,
        encoder_rule=encoder_rule,
        explanation_stability=stability,
        document_identity=identity,
        challenger_code=challenger.candidate_code,
        incumbent_code=incumbent.candidate_code,
    )


def describe_lift(value: float | None) -> str:
    if value is None:
        return "an unmeasured amount"
    return f"{float(value):.6g}"
