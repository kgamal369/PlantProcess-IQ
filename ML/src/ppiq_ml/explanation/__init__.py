"""Explanation producers.

TreeSHAP is the initial candidate and sits behind a provider interface, so the
promotion kernel judges attribution numbers without ever learning which method or
library produced them. Replacing the producer is a matter of implementing the
interface; nothing in the kernel changes.

Everything here claims a predictive contribution and nothing here claims a cause.
"""

from .contract import (
    TREESHAP_METHOD,
    ClaimClass,
    ContributionEvidence,
    ContributionScale,
    EvidenceIdentity,
    ExplanationError,
    ExplanationProvider,
    ExplanationUnavailableError,
)
from .treeshap import LightGbmTreeShapExplanationProvider
from .bridge import to_promotion_evidence

__all__ = [
    "TREESHAP_METHOD", "ClaimClass", "ContributionEvidence", "ContributionScale",
    "EvidenceIdentity", "ExplanationError", "ExplanationProvider",
    "ExplanationUnavailableError",
    "LightGbmTreeShapExplanationProvider",
    "to_promotion_evidence",
]
