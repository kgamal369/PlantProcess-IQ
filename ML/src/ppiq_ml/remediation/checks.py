"""The nine remediation eligibility checks, as a table rather than as a function.

WHY A TABLE. Every check carries its own code, its own name, its own sentence and
its own predicate over declared facts. Adding, reading or auditing one is a matter of
reading a row. A nine-branch function would let two checks share a failure sentence
by accident, and a person told that a suggestion failed would not be able to tell
which rule refused it.

THE CODES AND NAMES ARE FROZEN. They are not renamed, reordered or reinterpreted
here. The order is RM01 to RM09 and the evaluation order matches it, so two runs on
the same facts produce the same failed list in the same order.

    RM01  Controllability
    RM02  Remaining actionable stage
    RM03  Operating and specification limits
    RM04  Forbidden combinations and safety
    RM05  Historical support
    RM06  Contextual and confounder survival
    RM07  Uncertainty
    RM08  Causal and uplift evidence where data permits
    RM09  Sensitivity
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable, Mapping

from .contract import CausalEvidenceState, CheckOutcome, RemediationFacts

#: RM04 is the safety check. Its failure is not one refusal among nine; it
#: suppresses the suggestion outright, and the precedence rule names this code
#: rather than a position so a reordering could never move the safety rule.
SAFETY_CHECK_CODE = "RM04"

#: The first four decide whether acting is possible at all. The last five decide
#: whether the evidence is strong enough to act on. The precedence rule is built
#: from these two groups, so they are named once here rather than repeated.
ACTION_POSSIBILITY_CODES = ("RM01", "RM02", "RM03", "RM04")
EVIDENCE_STRENGTH_CODES = ("RM05", "RM06", "RM07", "RM08", "RM09")


@dataclass(frozen=True)
class CheckDefinition:
    code: str
    name: str
    predicate: Callable[[RemediationFacts], bool]
    pass_sentence: Callable[[RemediationFacts], str]
    fail_sentence: Callable[[RemediationFacts], str]
    detail: Callable[[RemediationFacts], Mapping[str, Any]]

    def evaluate(self, facts: RemediationFacts) -> CheckOutcome:
        passed = bool(self.predicate(facts))
        return CheckOutcome(
            code=self.code,
            name=self.name,
            passed=passed,
            reason=(self.pass_sentence if passed else self.fail_sentence)(facts),
            detail=dict(self.detail(facts)),
        )


CHECKS: tuple[CheckDefinition, ...] = (
    CheckDefinition(
        code="RM01",
        name="Controllability",
        predicate=lambda f: f.parameter_is_controllable,
        pass_sentence=lambda f: "The parameter can be changed by an operator.",
        fail_sentence=lambda f: (
            "The parameter cannot be changed by an operator, so a suggestion to change "
            "it is a description of the process rather than an action anyone can take."
        ),
        detail=lambda f: {"parameter_is_controllable": f.parameter_is_controllable},
    ),
    CheckDefinition(
        code="RM02",
        name="Remaining actionable stage",
        predicate=lambda f: f.remaining_stage_is_ahead_or_imminent,
        pass_sentence=lambda f: "A stage at which the change could be made still lies ahead.",
        fail_sentence=lambda f: (
            "Every stage at which this change could have been made has already passed "
            "for this unit. The suggestion is no longer an action, only a record."
        ),
        detail=lambda f: {
            "remaining_stage_is_ahead_or_imminent": f.remaining_stage_is_ahead_or_imminent
        },
    ),
    CheckDefinition(
        code="RM03",
        name="Operating and specification limits",
        predicate=lambda f: f.within_operating_limits and f.within_specification_limits,
        pass_sentence=lambda f: "The change sits inside both operating and specification limits.",
        fail_sentence=lambda f: (
            "The change falls outside "
            + " and ".join(
                part
                for part, ok in (
                    ("its operating limits", f.within_operating_limits),
                    ("its specification limits", f.within_specification_limits),
                )
                if not ok
            )
            + ". A suggestion that cannot lawfully be set is not a suggestion."
        ),
        detail=lambda f: {
            "within_operating_limits": f.within_operating_limits,
            "within_specification_limits": f.within_specification_limits,
        },
    ),
    CheckDefinition(
        code=SAFETY_CHECK_CODE,
        name="Forbidden combinations and safety",
        predicate=lambda f: (
            f.safety_constraints_satisfied and not f.violates_forbidden_combination
        ),
        pass_sentence=lambda f: "No forbidden combination is involved and safety constraints hold.",
        fail_sentence=lambda f: (
            "The change "
            + " and ".join(
                part
                for part, tripped in (
                    ("enters a forbidden combination", f.violates_forbidden_combination),
                    ("breaks a safety constraint", not f.safety_constraints_satisfied),
                )
                if tripped
            )
            + ". The suggestion is suppressed and is never shown, whatever its evidence."
        ),
        detail=lambda f: {
            "violates_forbidden_combination": f.violates_forbidden_combination,
            "safety_constraints_satisfied": f.safety_constraints_satisfied,
        },
    ),
    CheckDefinition(
        code="RM05",
        name="Historical support",
        predicate=lambda f: f.historical_support_units >= f.required_historical_support_units,
        pass_sentence=lambda f: (
            f"{f.historical_support_units} comparable unit(s) support the suggestion, "
            f"against {f.required_historical_support_units} required."
        ),
        fail_sentence=lambda f: (
            f"Only {f.historical_support_units} comparable unit(s) support the "
            f"suggestion, against {f.required_historical_support_units} required. The "
            "plant has not done this often enough for the result to mean much."
        ),
        detail=lambda f: {
            "historical_support_units": f.historical_support_units,
            "required_historical_support_units": f.required_historical_support_units,
        },
    ),
    CheckDefinition(
        code="RM06",
        name="Contextual and confounder survival",
        predicate=lambda f: f.survives_contextual_control,
        pass_sentence=lambda f: "The relationship survives control for the declared context.",
        fail_sentence=lambda f: (
            "The relationship disappears once the declared context is controlled for, "
            "so what looked like an effect of this parameter was something else."
        ),
        detail=lambda f: {"survives_contextual_control": f.survives_contextual_control},
    ),
    CheckDefinition(
        code="RM07",
        name="Uncertainty",
        predicate=lambda f: f.uncertainty_width <= f.maximum_uncertainty_width,
        pass_sentence=lambda f: (
            f"The interval spans {f.uncertainty_width:.6g} against a permitted "
            f"{f.maximum_uncertainty_width:.6g}."
        ),
        fail_sentence=lambda f: (
            f"The interval spans {f.uncertainty_width:.6g} against a permitted "
            f"{f.maximum_uncertainty_width:.6g}. The expected effect cannot be told "
            "apart from no effect at all."
        ),
        detail=lambda f: {
            "uncertainty_width": f.uncertainty_width,
            "maximum_uncertainty_width": f.maximum_uncertainty_width,
        },
    ),
    CheckDefinition(
        code="RM08",
        name="Causal and uplift evidence where data permits",
        # NOT_AVAILABLE does not fail. The check is qualified as applying where data
        # permits, and an absent measurement is a limit of the method rather than a
        # statement about the customer's process.
        predicate=lambda f: f.causal_evidence != CausalEvidenceState.CONTRADICTED,
        pass_sentence=lambda f: (
            "Causal evidence supports the suggestion."
            if f.causal_evidence == CausalEvidenceState.SUPPORTED
            else "No causal evidence is available, so this check does not refuse."
        ),
        fail_sentence=lambda f: (
            "The available causal evidence contradicts the suggestion: the units that "
            "received this change did not do better than comparable units that did not."
        ),
        detail=lambda f: {"causal_evidence": f.causal_evidence.value},
    ),
    CheckDefinition(
        code="RM09",
        name="Sensitivity",
        predicate=lambda f: f.conclusion_stable_under_sensitivity,
        pass_sentence=lambda f: "The conclusion holds under the declared sensitivity analysis.",
        fail_sentence=lambda f: (
            "The conclusion reverses under the declared sensitivity analysis, so it "
            "depends on a modelling choice rather than on the process."
        ),
        detail=lambda f: {
            "conclusion_stable_under_sensitivity": f.conclusion_stable_under_sensitivity
        },
    ),
)

CHECK_CODES = tuple(check.code for check in CHECKS)


def evaluate_checks(facts: RemediationFacts) -> tuple[CheckOutcome, ...]:
    """Run all nine, in RM order, always. Every outcome is reported."""
    return tuple(check.evaluate(facts) for check in CHECKS)


def definition_for(code: str) -> CheckDefinition:
    for check in CHECKS:
        if check.code == code:
            return check
    raise KeyError(f"No remediation check is named '{code}'.")
