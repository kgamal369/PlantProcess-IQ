"""Turning nine check outcomes into one of four states.

THE FROZEN RULES, IN PRECEDENCE ORDER.

    RM04 safety failure                                  -> suppressed
    all nine pass                                        -> actionable
    RM05..RM09 pass, one or more of RM01..RM04 fail      -> evidence_only
    RM01..RM06 pass, RM07 or RM08 fail                   -> exploratory

Safety suppression wins over any softer classification, and is therefore tested
first and named by code rather than by position.

WHAT THE FROZEN TABLE DOES NOT SAY, AND WHAT IS DONE ABOUT IT. Those four rules do
not cover every combination of nine checks. RM09 failing on its own matches none of
them, and neither does RM05 failing on its own. Since no fifth state may be invented,
the rules are completed rather than extended, along the distinction the two groups
already draw:

    a failure in RM01..RM04 means acting is not possible   -> evidence_only
    a failure only in RM05..RM09 means the evidence is weak -> exploratory

Every frozen row is reproduced exactly by this completion, and it is total over all
combinations. The derived rows are marked as derived in the reason sentence, so a
reader can tell which rule decided.
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

    possibility_failures = [c for c in failed_codes if c in ACTION_POSSIBILITY_CODES]
    strength_failures = [c for c in failed_codes if c in EVIDENCE_STRENGTH_CODES]

    if possibility_failures:
        frozen = not strength_failures
        return EligibilityResult(
            state=EligibilityState.EVIDENCE_ONLY,
            checks=checks,
            reason=(
                "Acting is not possible because "
                + ", ".join(possibility_failures)
                + " failed, so the finding stands as evidence and not as an action"
                + ("." if frozen else ", and the evidence is weak as well: "
                   + ", ".join(strength_failures) + " also failed (derived row).")
            ),
        )

    frozen = set(strength_failures) & {"RM07", "RM08"}
    return EligibilityResult(
        state=EligibilityState.EXPLORATORY,
        checks=checks,
        reason=(
            "Acting is possible, and the evidence is not strong enough to recommend it: "
            + ", ".join(strength_failures)
            + " failed"
            + ("." if frozen else " (derived row: no frozen combination names this "
               "failure on its own, and it is a weakness of the evidence rather than "
               "a barrier to acting).")
        ),
    )


def evaluate_eligibility(facts: RemediationFacts) -> EligibilityResult:
    """Run the nine checks and classify the result."""
    return classify(evaluate_checks(facts))
