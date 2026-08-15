"""The complete can_accept authority. Seven conditions, all of them required.

WHY THIS IS NOT ELIGIBILITY. Eligibility asks whether a suggestion is worth showing.
This asks whether this caller may act on it, on this prediction, at this moment. A
suggestion can be perfectly eligible and unacceptable: its deadline has passed, the
prediction was superseded, the model that produced it went under review, or the
caller's role does not permit the decision.

WHY ALL SEVEN ARE EVALUATED INDEPENDENTLY. The requirement is that the authority
cannot be reconstructed from fewer fields. Each condition is therefore its own row
with its own blocker code, each is checked whatever the others say, and every failure
is reported. A short circuit would make the answer depend on evaluation order and
would hide the second reason a decision was refused.

WHY TWO CONDITIONS LOOK LIKE CHECKS THAT ALREADY RAN. The remaining stage and the
safety constraint are re-examined here on purpose. Eligibility was computed when the
suggestion was produced; acceptance happens later, and both facts move in between. A
server that trusted the earlier answer would be accepting a decision on the strength
of a measurement taken before the thing it measures changed.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from .contract import (
    CanAcceptResult,
    ConditionOutcome,
    EligibilityState,
    ModelLifecycleState,
    PredictionState,
    RemediationFacts,
)

#: Lifecycle states that may not produce an acceptable decision.
NON_SERVING_LIFECYCLE = (ModelLifecycleState.UNDER_REVIEW, ModelLifecycleState.RETIRED)


@dataclass(frozen=True)
class ConditionDefinition:
    ordinal: int
    code: str
    name: str
    predicate: Callable[[RemediationFacts, EligibilityState], bool]
    pass_sentence: str
    fail_sentence: str

    def evaluate(
        self, facts: RemediationFacts, state: EligibilityState
    ) -> ConditionOutcome:
        satisfied = bool(self.predicate(facts, state))
        return ConditionOutcome(
            ordinal=self.ordinal,
            code=self.code,
            name=self.name,
            satisfied=satisfied,
            reason=self.pass_sentence if satisfied else self.fail_sentence,
        )


CONDITIONS: tuple[ConditionDefinition, ...] = (
    ConditionDefinition(
        ordinal=1,
        code="CA1_ELIGIBILITY_ACTIONABLE",
        name="Eligibility state is actionable",
        predicate=lambda f, state: state == EligibilityState.ACTIONABLE,
        pass_sentence="The eligibility result is actionable.",
        fail_sentence=(
            "The eligibility result is not actionable, so there is nothing here a "
            "caller is permitted to accept."
        ),
    ),
    ConditionDefinition(
        ordinal=2,
        code="CA2_STAGE_STILL_AHEAD",
        name="Remaining stage is ahead or imminent",
        predicate=lambda f, state: f.remaining_stage_is_ahead_or_imminent,
        pass_sentence="A stage at which the change could be made still lies ahead.",
        fail_sentence=(
            "No stage at which the change could be made remains for this unit, so "
            "accepting it now would record a decision that cannot be carried out."
        ),
    ),
    ConditionDefinition(
        ordinal=3,
        code="CA3_DEADLINE_NOT_ELAPSED",
        name="Actionable deadline has not elapsed",
        predicate=lambda f, state: not f.actionable_deadline_elapsed,
        pass_sentence="The actionable deadline has not passed.",
        fail_sentence=(
            "The actionable deadline has passed. A suggestion that was acceptable "
            "earlier is not acceptable now, and the earlier answer is not reused."
        ),
    ),
    ConditionDefinition(
        ordinal=4,
        code="CA4_PREDICTION_STILL_OPEN",
        name="Prediction is open, not superseded and not decided",
        predicate=lambda f, state: f.prediction_state == PredictionState.OPEN,
        pass_sentence="The prediction is still open.",
        fail_sentence=(
            "The prediction is no longer open, so a decision against it would attach "
            "to a prediction that has already been superseded or decided."
        ),
    ),
    ConditionDefinition(
        ordinal=5,
        code="CA5_SAFETY_VALID_ON_RECHECK",
        name="Safety is still valid on re-check",
        predicate=lambda f, state: f.safety_valid_on_recheck,
        pass_sentence="Safety constraints still hold on re-check.",
        fail_sentence=(
            "Safety no longer holds on re-check. The earlier eligibility answer was "
            "computed against conditions that have since changed."
        ),
    ),
    ConditionDefinition(
        ordinal=6,
        code="CA6_MODEL_SERVING_LIFECYCLE",
        name="Producing model is neither under review nor retired",
        predicate=lambda f, state: f.producing_model_lifecycle not in NON_SERVING_LIFECYCLE,
        pass_sentence="The producing model is in an active lifecycle state.",
        fail_sentence=(
            "The model that produced this suggestion is under review or retired, so "
            "its output may not be acted on until that is resolved."
        ),
    ),
    ConditionDefinition(
        ordinal=7,
        code="CA7_ENTITLEMENT_AND_ROLE",
        name="Tenant entitlement and caller role permit the decision",
        predicate=lambda f, state: f.tenant_entitled and f.caller_role_permits_decision,
        pass_sentence="The tenant is entitled and the caller's role permits the decision.",
        fail_sentence=(
            "The tenant is not entitled or the caller's role does not permit this "
            "decision. Authority is decided by the server and never by the client."
        ),
    ),
)

CONDITION_CODES = tuple(condition.code for condition in CONDITIONS)


def evaluate_can_accept(
    facts: RemediationFacts, eligibility_state: EligibilityState
) -> CanAcceptResult:
    """Evaluate all seven conditions and combine them as a conjunction."""
    outcomes = tuple(
        condition.evaluate(facts, eligibility_state) for condition in CONDITIONS
    )
    blockers = [c for c in outcomes if not c.satisfied]

    if not blockers:
        return CanAcceptResult(
            can_accept=True,
            conditions=outcomes,
            reason=(
                "All seven conditions are satisfied. The decision may be accepted by "
                "this caller, on this prediction, now."
            ),
        )

    return CanAcceptResult(
        can_accept=False,
        conditions=outcomes,
        reason=(
            "The decision may not be accepted. "
            + str(len(blockers))
            + " of seven condition(s) are not satisfied: "
            + "; ".join(f"{c.code} {c.reason}" for c in blockers)
        ),
    )
