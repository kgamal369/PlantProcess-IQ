"""Remediation eligibility and the can_accept server authority.

Nine named checks, four outcome states, and a seven-condition authority that cannot
be reconstructed from eligibility alone. Table driven, standard library only, and
pure: it reads declared facts and returns a decision.

Nothing here persists a decision, generates a candidate or reaches a plant.
"""

from .contract import (
    CanAcceptResult,
    CausalEvidenceState,
    CheckOutcome,
    ConditionOutcome,
    EligibilityResult,
    EligibilityState,
    ModelLifecycleState,
    PredictionState,
    RemediationContractError,
    RemediationDecision,
    RemediationFacts,
)
from .checks import (
    ACTION_POSSIBILITY_CODES,
    CHECK_CODES,
    CHECKS,
    EVIDENCE_STRENGTH_CODES,
    SAFETY_CHECK_CODE,
    CheckDefinition,
    definition_for,
    evaluate_checks,
)
from .eligibility import classify, evaluate_eligibility
from .authority import (
    CONDITION_CODES,
    CONDITIONS,
    NON_SERVING_LIFECYCLE,
    ConditionDefinition,
    evaluate_can_accept,
)
from .kernel import decide

__all__ = [
    "CanAcceptResult", "CausalEvidenceState", "CheckOutcome", "ConditionOutcome",
    "EligibilityResult", "EligibilityState", "ModelLifecycleState", "PredictionState",
    "RemediationContractError", "RemediationDecision", "RemediationFacts",
    "ACTION_POSSIBILITY_CODES", "CHECK_CODES", "CHECKS", "EVIDENCE_STRENGTH_CODES",
    "SAFETY_CHECK_CODE", "CheckDefinition", "definition_for", "evaluate_checks",
    "classify", "evaluate_eligibility",
    "CONDITION_CODES", "CONDITIONS", "NON_SERVING_LIFECYCLE", "ConditionDefinition",
    "evaluate_can_accept",
    "decide",
]
