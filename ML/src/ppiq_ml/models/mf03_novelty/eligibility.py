"""Whether this reference population can support a novelty claim at all.

A novelty score is a distance from a reference. Three conditions make that distance
meaningless, and each of them produces a refusal rather than a number.

TOO FEW UNITS. With a handful of units, every unit is unusual relative to the rest.
The score would be arithmetic rather than evidence.

NO USABLE FEATURE. A feature whose values never vary cannot separate anything.
Excluding one is normal; finding that all of them are constant means the population
carries no information at all.

TOO FEW DISTINCT UNITS. A thousand rows that are twenty repeated readings is twenty
observations wearing a large number. Distance from that reference describes the
duplication, not the process.

The thresholds below are declared constants, not measured ones. They are stated here
so a later task can replace them with values measured on real populations, and so
nobody mistakes the current values for evidence.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Sequence

from .contract import (
    FeatureExclusion,
    NoveltyRefusalCode,
    RefusalState,
)

#: Declared, not measured. See the module docstring.
MIN_REFERENCE_UNITS = 30
MIN_DISTINCT_REFERENCE_UNITS = 12
MIN_USABLE_FEATURES = 1

#: Below this spread a feature is treated as constant. Sensors quantise, so an exact
#: zero is rarer than a value that is zero for every practical purpose.
CONSTANT_FEATURE_SPREAD = 1e-12


@dataclass(frozen=True)
class EligibilityOutcome:
    refusal: RefusalState
    used_features: tuple[str, ...]
    excluded_features: tuple[FeatureExclusion, ...]
    distinct_units: int

    @property
    def eligible(self) -> bool:
        return not self.refusal.refused


def _spread(values: Sequence[float]) -> float:
    return max(values) - min(values)


def evaluate_eligibility(
    ids: Sequence[str],
    rows: Sequence[Sequence[float]],
    feature_names: Sequence[str],
) -> EligibilityOutcome:
    """Decide whether a novelty claim is supportable, naming any measurement that fails."""
    units = len(rows)
    distinct = len({tuple(float(v) for v in row) for row in rows})

    if units < MIN_REFERENCE_UNITS:
        return EligibilityOutcome(
            RefusalState(
                True,
                NoveltyRefusalCode.TOO_FEW_REFERENCE_UNITS,
                (
                    f"The reference population carries {units} unit(s) against a declared "
                    f"minimum of {MIN_REFERENCE_UNITS}. Below that, every unit is unusual "
                    "relative to the rest and a novelty score would be arithmetic rather "
                    "than evidence."
                ),
                required=float(MIN_REFERENCE_UNITS),
                observed=float(units),
            ),
            (),
            (),
            distinct,
        )

    used: list[str] = []
    excluded: list[FeatureExclusion] = []
    for position, name in enumerate(feature_names):
        column = [float(row[position]) for row in rows]
        spread = _spread(column)
        if spread <= CONSTANT_FEATURE_SPREAD:
            excluded.append(
                FeatureExclusion(
                    feature=str(name),
                    reason=(
                        "The feature holds one value across the whole reference "
                        "population, so it cannot separate any unit from any other."
                    ),
                    observed=spread,
                )
            )
        else:
            used.append(str(name))

    if len(used) < MIN_USABLE_FEATURES:
        return EligibilityOutcome(
            RefusalState(
                True,
                NoveltyRefusalCode.DEGENERATE_POPULATION,
                (
                    f"All {len(feature_names)} declared feature(s) hold a single value "
                    "across the reference population. There is no dimension along which "
                    "one unit could be more unusual than another, so no score is produced."
                ),
                required=float(MIN_USABLE_FEATURES),
                observed=float(len(used)),
            ),
            (),
            tuple(excluded),
            distinct,
        )

    if distinct < MIN_DISTINCT_REFERENCE_UNITS:
        return EligibilityOutcome(
            RefusalState(
                True,
                NoveltyRefusalCode.TOO_FEW_DISTINCT_UNITS,
                (
                    f"The reference population carries {units} row(s) but only {distinct} "
                    f"distinct one(s), against a declared minimum of "
                    f"{MIN_DISTINCT_REFERENCE_UNITS}. A distance from that reference "
                    "describes the duplication rather than the process."
                ),
                required=float(MIN_DISTINCT_REFERENCE_UNITS),
                observed=float(distinct),
            ),
            tuple(used),
            tuple(excluded),
            distinct,
        )

    return EligibilityOutcome(
        RefusalState(False, NoveltyRefusalCode.NONE, ""),
        tuple(used),
        tuple(excluded),
        distinct,
    )


def validate_population(
    ids: Sequence[str], rows: Sequence[Sequence[float]], feature_names: Sequence[str]
) -> None:
    """Structural checks. These are contract violations, not honest refusals."""
    from .contract import NoveltyContractError

    if len(ids) != len(rows):
        raise NoveltyContractError(
            f"{len(ids)} identifier(s) were supplied for {len(rows)} row(s)."
        )
    if not feature_names:
        raise NoveltyContractError("A novelty model needs at least one declared feature.")
    if len(set(ids)) != len(ids):
        raise NoveltyContractError(
            "Unit identifiers must be unique, or two ranked units could be the same unit."
        )
    width = len(feature_names)
    for position, row in enumerate(rows):
        if len(row) != width:
            raise NoveltyContractError(
                f"Row {position} carries {len(row)} value(s) against {width} declared "
                "feature name(s)."
            )
        for value in row:
            if value is None:
                raise NoveltyContractError(
                    f"Row {position} carries an absent value. A missing measurement is "
                    "not a distance, and treating it as one would invent evidence."
                )


def summarise(outcome: EligibilityOutcome) -> dict[str, Any]:
    return {
        "eligible": outcome.eligible,
        "refusal": outcome.refusal.to_dict(),
        "used_features": list(outcome.used_features),
        "excluded_features": [e.to_dict() for e in outcome.excluded_features],
        "distinct_units": outcome.distinct_units,
    }
