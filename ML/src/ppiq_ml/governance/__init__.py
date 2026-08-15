"""Model governance. A pure promotion kernel over frozen evidence.

Three independent dimensions, never merged into a score. Every refusal names the
dimension and the numbers behind it. Nothing here trains, serves, writes, connects
or activates: it reads a document and returns a decision, which is what lets the
same evidence produce the same decision on any machine at any later date.

Standard library only, on purpose. A decision that depended on a numerical package
would carry that package's version into its answer.
"""

from .checks import (
    CheckState,
    Dimension,
    DimensionVerdict,
    Direction,
    MeasuredCheck,
    check,
    describe_failures,
)
from .evidence import (
    DOCUMENT_VERSION,
    EXPLANATION_METHOD_INITIAL_CANDIDATE,
    CandidateClass,
    CandidateEvidence,
    DeclaredThresholds,
    EvidenceError,
    ExplanationEvidence,
    PromotionDocument,
    QualityEvidence,
    ServingEvidence,
    TrainingEvidence,
    build_document,
    lift,
)
from .stability import (
    ExplanationStability,
    StabilityError,
    evaluate_stability,
    midranks,
    rank_correlation,
    top_k_indices,
)
from .dimensions import evaluate_quality, evaluate_serving, evaluate_training
from .encoder_rule import (
    CLAUSE_ARTIFACT_SIZE,
    CLAUSE_EXPLANATION_STABILITY,
    CLAUSE_METRIC_LIFT,
    CLAUSE_P95_LATENCY_DELTA,
    EncoderRuleVerdict,
    evaluate_encoder_rule,
)
from .kernel import DECISION_VERSION, PromotionDecision, PromotionOutcome, decide

__all__ = [
    "CheckState", "Dimension", "DimensionVerdict", "Direction", "MeasuredCheck",
    "check", "describe_failures",
    "DOCUMENT_VERSION", "EXPLANATION_METHOD_INITIAL_CANDIDATE", "CandidateClass",
    "CandidateEvidence", "DeclaredThresholds", "EvidenceError", "ExplanationEvidence",
    "PromotionDocument", "QualityEvidence", "ServingEvidence", "TrainingEvidence",
    "build_document", "lift",
    "ExplanationStability", "StabilityError", "evaluate_stability", "midranks",
    "rank_correlation", "top_k_indices",
    "evaluate_quality", "evaluate_serving", "evaluate_training",
    "CLAUSE_ARTIFACT_SIZE", "CLAUSE_EXPLANATION_STABILITY", "CLAUSE_METRIC_LIFT",
    "CLAUSE_P95_LATENCY_DELTA", "EncoderRuleVerdict", "evaluate_encoder_rule",
    "DECISION_VERSION", "PromotionDecision", "PromotionOutcome", "decide",
]
