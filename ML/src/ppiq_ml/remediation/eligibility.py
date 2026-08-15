"""Turning nine check outcomes into one of four states.

THE FROZEN RULES, IN PRECEDENCE ORDER.

    RM04 safety failure                                  -> suppressed
    all nine pass                                        -> actionable
    RM05..RM09 pass, one or more of RM01..RM04 fail      -> evidence_only
    RM01..RM06 pass, RM07 or RM08 fail                   -> exploratory

Safety suppression wins over any softer classification, and is therefore tested
first and named by code rather than by position.

THE FOURTH ROW IS THE CATCH-ALL, AND IT IS EXPLORATORY.

evidence_only is the narrow case: safety holds, every evidence check RM05..RM09
passes, and the only thing wrong is that acting is not possible. The finding is
sound and cannot be acted on, which is exactly what evidence means.

The moment any evidence check also fails, the finding is no longer sound, so it
cannot be offered as evidence however impossible acting may be. Every remaining
non-safety combination is therefore exploratory. No fifth state exists.
"""

from __future__ import annotations

from .checks import (
    ACTION_POSSIBILITY_CODES,
    EVIDENCE_STRENGTH_CODES,
    SAFETY_CHECK_CODE,
    evaluate_checks,
)
from .contract import CheckOutcome, EligibilityResult, EligibilityState, RemediationFacts


def classify(checks: tuple[CheckOutcome, ...]) -> EligibilityResult:
    """Apply the precedence to nine already-evaluated checks."""
    by_code = {c.code: c for c in checks}
    failed = tuple(c for c in checks if not c.passed)
    failed_codes = tuple(c.code for c in failed)

    safety = by_code[SAFETY_CHECK_CODE]
    if not safety.passed:
        return EligibilityResult(
            state=EligibilityState.SUPPRESSED,
            checks=checks,
            reason=(
                f"{SAFETY_CHECK_CODE} {safety.name} failed. {safety.reason} Safety "
                "suppression takes precedence over every softer classification, so no "
                "other check can raise this suggestion above suppressed."
            ),
        )

    if not failed:
        return EligibilityResult(
            state=EligibilityState.ACTIONABLE,
            checks=checks,
            reason="All nine eligibility checks passed.",
        )

    # RM04 has already passed, so a possibility failure here is RM01, RM02 or RM03.
    possibility_failures = [c for c in failed_codes if c in ACTION_POSSIBILITY_CODES]
    strength_failures = [c for c in failed_codes if c in EVIDENCE_STRENGTH_CODES]

    if possibility_failures and not strength_failures:
        return EligibilityResult(
            state=EligibilityState.EVIDENCE_ONLY,
            checks=checks,
            reason=(
                "Every evidence check passed, so the finding is sound, and "
                + ", ".join(possibility_failures)
                + " failed, so it cannot be acted on. It stands as evidence and not "
                "as an action."
            ),
        )

    return EligibilityResult(
        state=EligibilityState.EXPLORATORY,
        checks=checks,
        reason=(
            "The evidence is not strong enough to offer as a finding: "
            + ", ".join(strength_failures)
            + " failed"
            + (
                "."
                if not possibility_failures
                else ", and acting is not possible either because "
                + ", ".join(possibility_failures)
                + " failed. A finding that is not sound is not offered as evidence, "
                "however impossible acting may be."
            )
        ),
    )


def evaluate_eligibility(facts: RemediationFacts) -> EligibilityResult:
    """Run the nine checks and classify the result."""
    return classify(evaluate_checks(facts))
