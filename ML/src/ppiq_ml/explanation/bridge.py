"""Turning explanation runs into the input the promotion kernel judges.

THE DIRECTION OF THIS DEPENDENCY IS THE POINT. This module knows about the promotion
kernel; the promotion kernel knows nothing about explanations beyond the numbers it
is handed. Reverse it and the kernel would import a producer, and a decision would
start depending on which library was installed.

Every run in a set must be about the same model and the same snapshot. Comparing
explanation runs taken from different models would report a difference between two
models as instability in one of them.
"""

from __future__ import annotations

from typing import Sequence

from ..governance.evidence import ExplanationEvidence
from .contract import ContributionEvidence, ExplanationError


def to_promotion_evidence(runs: Sequence[ContributionEvidence]) -> ExplanationEvidence:
    """Reduce repeated explanation runs to the attribution vectors stability needs."""
    if len(runs) < 2:
        raise ExplanationError(
            "Stability needs at least two explanation runs. A single run cannot "
            "disagree with itself."
        )

    first = runs[0]
    for index, run in enumerate(runs[1:], start=1):
        if run.feature_names != first.feature_names:
            raise ExplanationError(
                f"Run {index} names a different feature set from run 0. Two feature "
                "sets cannot be compared for stability."
            )
        if run.explanation_method != first.explanation_method:
            raise ExplanationError(
                f"Run {index} was produced by '{run.explanation_method}' and run 0 by "
                f"'{first.explanation_method}'. A difference between two methods is not "
                "instability in one model."
            )
        if run.identity.snapshot_identity != first.identity.snapshot_identity:
            raise ExplanationError(
                f"Run {index} was taken on a different snapshot from run 0."
            )

    return ExplanationEvidence(
        method=first.explanation_method,
        feature_names=first.feature_names,
        attributions=tuple(run.mean_absolute_contributions() for run in runs),
    )
