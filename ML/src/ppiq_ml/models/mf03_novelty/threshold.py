"""Where the line is drawn, and what the line is drawn on.

The threshold is a quantile of the reference scores themselves rather than a constant.
A fixed number would mean different things on two populations, and would silently
mean something new the day the process changed.

That choice has a consequence worth stating plainly: taking the line from the same
population being scored guarantees that roughly the declared fraction of units sit
above it, whether or not anything unusual happened. The threshold answers "which of
these units are the most unusual", never "did something unusual occur". The second
question needs a reference the units were not drawn from, and that belongs to the
production drift work rather than here.
"""

from __future__ import annotations

import math
from typing import Sequence

from .contract import NoveltyContractError

THRESHOLD_METHOD = "reference_quantile"

#: Declared, not measured.
DEFAULT_QUANTILE = 0.95


def reference_quantile_threshold(scores: Sequence[float], quantile: float) -> float:
    """Nearest-rank quantile of the reference scores.

    Nearest rank rather than an interpolated one, so the threshold is always a value
    some unit actually scored. A line drawn between two observations is a number no
    measurement produced.
    """
    if not scores:
        raise NoveltyContractError("A threshold cannot be taken from no scores.")
    if not 0.0 < quantile < 1.0:
        raise NoveltyContractError(
            f"A quantile must lie strictly between zero and one; {quantile} was declared. "
            "A quantile of one would place the line above every unit and flag nothing."
        )
    ordered = sorted(float(v) for v in scores)
    position = max(1, math.ceil(quantile * len(ordered))) - 1
    return ordered[min(position, len(ordered) - 1)]
