"""The frozen encoder promotion inequality, represented exactly.

    promote_encoder iff
        metric_lift            >= declared_min_lift
    AND p95_latency_delta      <= declared_latency_budget
    AND artifact_size          <= declared_size_class
    AND explanation_stability  >= floor

FOUR CLAUSES, ALL REQUIRED, NONE TRADEABLE. The inequality is a conjunction and is
implemented as one. There is no weighting and no compensation: an encoder that wins
enormously on lift and misses the size class has not satisfied the inequality.

WHY IT EXISTS. An encoder costs more to build, more to serve and more to explain than
an engineered-feature path. The default answer is therefore the simpler path, and the
encoder has to buy its place. When the lift clause fails, the simpler path is not
merely preferred: it wins, by the rule.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from .checks import Direction, MeasuredCheck, check, Dimension
from .evidence import CandidateClass, CandidateEvidence, DeclaredThresholds, lift

CLAUSE_METRIC_LIFT = "metric_lift"
CLAUSE_P95_LATENCY_DELTA = "p95_latency_delta"
CLAUSE_ARTIFACT_SIZE = "artifact_size"
CLAUSE_EXPLANATION_STABILITY = "explanation_stability"


@dataclass(frozen=True)
class EncoderRuleVerdict:
    """The four clauses and the single conjunction they form."""

    applicable: bool
    promote_encoder: bool
    simpler_path_wins: bool
    clauses: tuple[MeasuredCheck, ...]
    metric_lift: float | None
    p95_latency_delta: float | None

    @property
    def failed_clauses(self) -> tuple[str, ...]:
        return tuple(c.name for c in self.clauses if c.state.value != "satisfied")

    def to_dict(self) -> dict[str, Any]:
        return {
            "applicable": self.applicable,
            "promote_encoder": self.promote_encoder,
            "simpler_path_wins": self.simpler_path_wins,
            "metric_lift": self.metric_lift,
            "p95_latency_delta": self.p95_latency_delta,
            "failed_clauses": list(self.failed_clauses),
            "clauses": [c.to_dict() for c in self.clauses],
        }


def evaluate_encoder_rule(
    challenger: CandidateEvidence,
    incumbent: CandidateEvidence,
    thresholds: DeclaredThresholds,
    explanation_stability: float | None,
) -> EncoderRuleVerdict:
    """Apply the inequality. Applicable only when the challenger is an encoder."""
    applicable = challenger.candidate_class == CandidateClass.ENCODER

    metric_lift = lift(
        challenger.quality.primary_metric,
        incumbent.quality.primary_metric,
        challenger.quality.primary_higher_is_better,
    )
    p95_delta = challenger.serving.p95_latency_ms - incumbent.serving.p95_latency_ms

    clauses = (
        check(
            Dimension.QUALITY,
            CLAUSE_METRIC_LIFT,
            Direction.AT_LEAST,
            thresholds.declared_min_lift,
            metric_lift,
            "The encoder must beat the simpler path by the declared margin.",
        ),
        check(
            Dimension.SERVING,
            CLAUSE_P95_LATENCY_DELTA,
            Direction.AT_MOST,
            thresholds.declared_latency_budget_ms,
            p95_delta,
            "What the encoder adds to the slow answers must fit the declared budget.",
        ),
        check(
            Dimension.SERVING,
            CLAUSE_ARTIFACT_SIZE,
            Direction.AT_MOST,
            float(thresholds.declared_size_class_bytes),
            float(challenger.serving.artifact_size_bytes),
            "The encoder artifact must fit the declared size class.",
        ),
        check(
            Dimension.QUALITY,
            CLAUSE_EXPLANATION_STABILITY,
            Direction.AT_LEAST,
            thresholds.min_explanation_rank_agreement,
            explanation_stability,
            "An encoder that cannot be explained the same way twice is not promoted.",
        ),
    )

    satisfied = all(c.state.value == "satisfied" for c in clauses)
    lift_clause = clauses[0]

    return EncoderRuleVerdict(
        applicable=applicable,
        promote_encoder=bool(applicable and satisfied),
        # The simpler engineered-feature path wins exactly when the encoder fails to
        # clear the declared lift. Failing another clause is a rejection of the
        # encoder rather than a win for the simpler path, and the two are reported
        # differently because they call for different work.
        simpler_path_wins=bool(applicable and lift_clause.state.value == "failed"),
        clauses=clauses,
        metric_lift=metric_lift,
        p95_latency_delta=p95_delta,
    )
