"""The out-of-time split, and the identity that proves two models shared it.

WHY OUT OF TIME AND NOT RANDOM. A random split lets a model be evaluated on units
that sit between two units it trained on. Process conditions drift, so that flatters
every model and flatters a complex one most. The holdout is therefore the tail of the
declared order: the model is asked the question it will actually be asked in service,
which is about units it has never seen and which came later.

WHY THE IDENTITY EXISTS. Two models are comparable only if they were scored on the
same units. holdout_identity is a hash over the ordered unit identifiers of the
holdout, so the claim is checkable by a reader rather than asserted by the writer.
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass

from .contract import Population

#: The tail of the declared order held back from training.
HOLDOUT_FRACTION = 0.25

HOLDOUT_IDENTITY_PREFIX = "ppiq.mf04.holdout/1"


@dataclass(frozen=True)
class Split:
    train: Population
    holdout: Population
    holdout_identity: str
    fraction: float


def _ordered_indices(population: Population) -> list[int]:
    """Stable order by the declared order column, ties broken by unit identifier.

    Without the tie break, two units carrying the same order value could land on
    either side of the boundary depending on the order the rows arrived in, and the
    split would stop being reproducible.
    """
    return sorted(
        range(len(population)),
        key=lambda i: (population.order_values[i], str(population.grains[i])),
    )


def holdout_identity(population: Population, fraction: float) -> str:
    digest = hashlib.sha256()
    digest.update(HOLDOUT_IDENTITY_PREFIX.encode("ascii"))
    digest.update(f"|{fraction:.6f}|".encode("ascii"))
    for grain in population.grains:
        digest.update(str(grain).encode("utf-8"))
        digest.update(b"\x1f")
    return digest.hexdigest()


def split_out_of_time(population: Population, fraction: float = HOLDOUT_FRACTION) -> Split:
    """Hold back the last fraction of the declared order."""
    if not 0.0 < fraction < 1.0:
        raise ValueError("The holdout fraction must lie strictly between zero and one.")
    total = len(population)
    if total == 0:
        raise ValueError("An empty population cannot be split.")

    ordered = _ordered_indices(population)
    holdout_size = int(total * fraction)
    if holdout_size < 1:
        holdout_size = 1
    if holdout_size >= total:
        holdout_size = total - 1

    train_indices = ordered[: total - holdout_size]
    holdout_indices = ordered[total - holdout_size :]

    train = population.select(train_indices)
    holdout = population.select(holdout_indices)
    return Split(
        train=train,
        holdout=holdout,
        holdout_identity=holdout_identity(holdout, fraction),
        fraction=fraction,
    )
