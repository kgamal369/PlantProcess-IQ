"""The pure remediation decision kernel.

    facts  ->  nine eligibility checks  ->  four-state classification
           ->  seven can_accept conditions  ->  server authority

Both authorities are returned side by side. Neither is derived from the other, and
nothing here persists a decision, generates a candidate, records effectiveness or
touches a prediction store. Those are downstream work under T-142 to T-145, and this
kernel exists to give them one authoritative answer to consume.

PPIQ records what a human decided. It never issues a plant control command, and
there is no client, socket or writer anywhere in this package through which one
could be issued.
"""

from __future__ import annotations

from .contract import RemediationDecision, RemediationFacts
from .eligibility import evaluate_eligibility
from .authority import evaluate_can_accept


def decide(facts: RemediationFacts) -> RemediationDecision:
    """Evaluate both authorities from one set of declared facts."""
    eligibility = evaluate_eligibility(facts)
    authority = evaluate_can_accept(facts, eligibility.state)
    return RemediationDecision(eligibility=eligibility, authority=authority)
