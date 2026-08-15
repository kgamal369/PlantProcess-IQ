"""What a remediation decision is made from, and what it may conclude.

This kernel is pure. It takes declared facts and returns a decision. It writes
nothing, reads nothing, and cannot reach a plant. PPIQ records what a human decided;
it never issues a control command, and there is no code path here through which one
could be issued.

TWO AUTHORITIES, DELIBERATELY SEPARATE.

    eligibility   is this suggestion worth showing, and in what class
    can_accept    may this caller act on it, right now, on this prediction

They are not the same question and one does not imply the other. Eligibility is a
property of the evidence. can_accept is a property of the moment: a suggestion that
was actionable an hour ago is not acceptable after its deadline, on a superseded
prediction, or by a caller whose role does not permit the decision. Collapsing them
would let a client re-derive server authority from a stale field, which is the
failure this separation exists to prevent.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping


class RemediationContractError(Exception):
    """The facts supplied cannot be interpreted under this contract."""


class EligibilityState(str, Enum):
    """The four outcomes. There is no fifth, and none is invented here."""

    ACTIONABLE = "actionable"
    EVIDENCE_ONLY = "evidence_only"
    EXPLORATORY = "exploratory"
    SUPPRESSED = "suppressed"


class CausalEvidenceState(str, Enum):
    """What the data supports about cause, including that it may support nothing.

    NOT_AVAILABLE is not a failure. The check is qualified as applying where data
    permits, and treating an absent measurement as a contradiction would report a
    method gap as a property of the customer's process.
    """

    SUPPORTED = "supported"
    CONTRADICTED = "contradicted"
    NOT_AVAILABLE = "not_available"


class ModelLifecycleState(str, Enum):
    ACTIVE = "active"
    UNDER_REVIEW = "under_review"
    RETIRED = "retired"


class PredictionState(str, Enum):
    OPEN = "open"
    SUPERSEDED = "superseded"
    DECIDED = "decided"


@dataclass(frozen=True)
class RemediationFacts:
    """Every fact the nine checks and the seven conditions read.

    All of it is declared by the caller. Nothing is defaulted into a passing value,
    because a fact that defaulted to true would be a check that passed by silence.
    """

    # ------------------------------------------------- RM01 controllability
    parameter_is_controllable: bool
    # -------------------------------------- RM02 remaining actionable stage
    remaining_stage_is_ahead_or_imminent: bool
    # ---------------------------------- RM03 operating and specification limits
    within_operating_limits: bool
    within_specification_limits: bool
    # ------------------------------- RM04 forbidden combinations and safety
    violates_forbidden_combination: bool
    safety_constraints_satisfied: bool
    # ----------------------------------------------- RM05 historical support
    historical_support_units: int
    required_historical_support_units: int
    # ------------------------------ RM06 contextual and confounder survival
    survives_contextual_control: bool
    # ---------------------------------------------------- RM07 uncertainty
    uncertainty_width: float
    maximum_uncertainty_width: float
    # ------------------------------------------ RM08 causal and uplift evidence
    causal_evidence: CausalEvidenceState
    # ----------------------------------------------------- RM09 sensitivity
    conclusion_stable_under_sensitivity: bool

    # ----------------------------------- can_accept, beyond eligibility alone
    actionable_deadline_elapsed: bool
    prediction_state: PredictionState
    safety_valid_on_recheck: bool
    producing_model_lifecycle: ModelLifecycleState
    tenant_entitled: bool
    caller_role_permits_decision: bool

    def __post_init__(self) -> None:
        if self.required_historical_support_units < 0:
            raise RemediationContractError(
                "A required historical support count may not be negative."
            )
        if self.historical_support_units < 0:
            raise RemediationContractError(
                "An observed historical support count may not be negative."
            )
        if self.maximum_uncertainty_width < 0.0:
            raise RemediationContractError(
                "A maximum uncertainty width may not be negative."
            )
        if self.uncertainty_width < 0.0:
            raise RemediationContractError(
                "An observed uncertainty width may not be negative."
            )

    def to_dict(self) -> dict[str, Any]:
        return {
            "parameter_is_controllable": self.parameter_is_controllable,
            "remaining_stage_is_ahead_or_imminent": self.remaining_stage_is_ahead_or_imminent,
            "within_operating_limits": self.within_operating_limits,
            "within_specification_limits": self.within_specification_limits,
            "violates_forbidden_combination": self.violates_forbidden_combination,
            "safety_constraints_satisfied": self.safety_constraints_satisfied,
            "historical_support_units": self.historical_support_units,
            "required_historical_support_units": self.required_historical_support_units,
            "survives_contextual_control": self.survives_contextual_control,
            "uncertainty_width": self.uncertainty_width,
            "maximum_uncertainty_width": self.maximum_uncertainty_width,
            "causal_evidence": self.causal_evidence.value,
            "conclusion_stable_under_sensitivity": self.conclusion_stable_under_sensitivity,
            "actionable_deadline_elapsed": self.actionable_deadline_elapsed,
            "prediction_state": self.prediction_state.value,
            "safety_valid_on_recheck": self.safety_valid_on_recheck,
            "producing_model_lifecycle": self.producing_model_lifecycle.value,
            "tenant_entitled": self.tenant_entitled,
            "caller_role_permits_decision": self.caller_role_permits_decision,
        }


@dataclass(frozen=True)
class CheckOutcome:
    """One named check, its verdict, and the sentence a person would read."""

    code: str
    name: str
    passed: bool
    reason: str
    detail: Mapping[str, Any]

    def to_dict(self) -> dict[str, Any]:
        return {
            "code": self.code,
            "name": self.name,
            "passed": self.passed,
            "reason": self.reason,
            "detail": dict(self.detail),
        }


@dataclass(frozen=True)
class EligibilityResult:
    state: EligibilityState
    checks: tuple[CheckOutcome, ...]
    reason: str

    @property
    def failed_checks(self) -> tuple[CheckOutcome, ...]:
        return tuple(c for c in self.checks if not c.passed)

    @property
    def failed_codes(self) -> tuple[str, ...]:
        return tuple(c.code for c in self.failed_checks)

    def to_dict(self) -> dict[str, Any]:
        return {
            "state": self.state.value,
            "reason": self.reason,
            "failed_codes": list(self.failed_codes),
            "checks": [c.to_dict() for c in self.checks],
        }


@dataclass(frozen=True)
class ConditionOutcome:
    """One of the seven conditions the server authority is built from."""

    ordinal: int
    code: str
    name: str
    satisfied: bool
    reason: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "ordinal": self.ordinal,
            "code": self.code,
            "name": self.name,
            "satisfied": self.satisfied,
            "reason": self.reason,
        }


@dataclass(frozen=True)
class CanAcceptResult:
    can_accept: bool
    conditions: tuple[ConditionOutcome, ...]
    reason: str

    @property
    def blockers(self) -> tuple[ConditionOutcome, ...]:
        return tuple(c for c in self.conditions if not c.satisfied)

    @property
    def blocker_codes(self) -> tuple[str, ...]:
        return tuple(c.code for c in self.blockers)

    def to_dict(self) -> dict[str, Any]:
        return {
            "can_accept": self.can_accept,
            "reason": self.reason,
            "blocker_codes": list(self.blocker_codes),
            "conditions": [c.to_dict() for c in self.conditions],
        }


@dataclass(frozen=True)
class RemediationDecision:
    """Both authorities, side by side, neither derived from the other."""

    eligibility: EligibilityResult
    authority: CanAcceptResult

    def to_dict(self) -> dict[str, Any]:
        return {
            "eligibility": self.eligibility.to_dict(),
            "can_accept": self.authority.to_dict(),
        }
