"""Deterministic sealed populations for the MF-04 tests.

Every column name, outcome code and value here is synthetic and generic. Nothing in
this module names a product outcome, a canonical unit of production or a real
detection position, because a fixture that borrowed one would quietly make the model
package depend on a vocabulary it must never carry.

The generator is a fixed linear congruential sequence rather than the standard
random module, so the same population is produced on every interpreter version and a
known-answer assertion stays true next year.
"""

from __future__ import annotations

from datetime import datetime, timedelta, timezone

from ppiq_ml.artifacts import Field, LogicalSchema, LogicalType, adapter_for
from ppiq_ml.models.mf04_supervised import (
    FeatureDeclaration,
    OutcomeDefinition,
    OutcomeKind,
    PredictionPoint,
)

UTC = timezone.utc
EPOCH = datetime(2026, 1, 1, tzinfo=UTC)

GRAIN_COLUMN = "unit_reference"
ORDER_COLUMN = "observed_at"
LABEL_COLUMN = "outcome_value"

FEATURE_EARLY = "measurement_alpha"
FEATURE_MID = "measurement_beta"
FEATURE_TEXT = "setting_label"
FEATURE_AFTER_CUTOFF = "measurement_after_cutoff"

CUTOFF_ORDINAL = 50
DETECTION_ORDINAL = 100

SETTINGS = ("setting_p", "setting_q", "setting_r")


class _Sequence:
    """A small deterministic generator with no dependency on the platform."""

    def __init__(self, seed: int) -> None:
        self._state = seed & 0x7FFFFFFF or 1

    def next_unit(self) -> float:
        self._state = (self._state * 1103515245 + 12345) & 0x7FFFFFFF
        return self._state / 0x7FFFFFFF


def _schema(label_type: LogicalType) -> LogicalSchema:
    return LogicalSchema(
        (
            Field(GRAIN_COLUMN, LogicalType.STRING, nullable=False),
            Field(ORDER_COLUMN, LogicalType.TIMESTAMP_UTC, nullable=False),
            Field(LABEL_COLUMN, label_type, nullable=False),
            Field(FEATURE_EARLY, LogicalType.FLOAT64),
            Field(FEATURE_MID, LogicalType.FLOAT64),
            Field(FEATURE_TEXT, LogicalType.STRING),
            Field(FEATURE_AFTER_CUTOFF, LogicalType.FLOAT64),
        )
    )


def build_rows(kind: OutcomeKind, units: int = 200, seed: int = 20260813):
    """Rows carrying a genuine signal in the two legal numeric features."""
    generator = _Sequence(seed)
    rows = []
    for index in range(units):
        alpha = generator.next_unit() * 10.0
        beta = generator.next_unit() * 4.0
        setting = SETTINGS[index % len(SETTINGS)]
        setting_effect = SETTINGS.index(setting) * 0.6
        noise = generator.next_unit() - 0.5

        latent = 0.42 * alpha + 0.75 * beta + setting_effect + noise - 3.2

        if kind == OutcomeKind.BINARY:
            label = "outcome_present" if latent > 0.0 else "outcome_absent"
        elif kind in (OutcomeKind.MULTICLASS, OutcomeKind.ORDINAL):
            if latent < -0.8:
                label = "band_low"
            elif latent < 1.1:
                label = "band_middle"
            else:
                label = "band_high"
        else:
            label = latent

        rows.append(
            [
                f"unit_{index:05d}",
                EPOCH + timedelta(hours=index),
                label,
                alpha,
                beta,
                setting,
                # Known only after the prediction position. Correlated with the
                # outcome on purpose, so a test that admits it would score well and
                # the gate is the only thing preventing that.
                latent * 3.0,
            ]
        )
    return rows


def build_outcome(kind: OutcomeKind, include_after_cutoff_feature: bool = False):
    features = [
        FeatureDeclaration(FEATURE_EARLY, available_at_ordinal=10),
        FeatureDeclaration(FEATURE_MID, available_at_ordinal=40, is_controllable=True),
        FeatureDeclaration(FEATURE_TEXT, available_at_ordinal=10),
    ]
    if include_after_cutoff_feature:
        features.append(
            FeatureDeclaration(FEATURE_AFTER_CUTOFF, available_at_ordinal=90)
        )

    class_order = None
    positive_class = None
    if kind == OutcomeKind.ORDINAL:
        class_order = ("band_low", "band_middle", "band_high")
    if kind == OutcomeKind.BINARY:
        positive_class = "outcome_present"

    return OutcomeDefinition(
        outcome_code=f"fixture_outcome_{kind.value}",
        kind=kind,
        grain_column=GRAIN_COLUMN,
        order_column=ORDER_COLUMN,
        label_column=LABEL_COLUMN,
        detection_position_ordinal=DETECTION_ORDINAL,
        prediction_point=PredictionPoint("fixture_prediction_point", CUTOFF_ORDINAL),
        features=tuple(features),
        positive_class=positive_class,
        class_order=class_order,
    )


def seal_population(
    directory: str,
    kind: OutcomeKind,
    artifact_format: str = "parquet",
    units: int = 200,
    seed: int = 20260813,
    rows=None,
):
    """Write a sealed artifact and return its descriptor."""
    import os

    label_type = (
        LogicalType.FLOAT64 if kind == OutcomeKind.CONTINUOUS else LogicalType.STRING
    )
    adapter = adapter_for(artifact_format)
    path = os.path.join(directory, "population" + adapter.file_suffix)
    payload = build_rows(kind, units, seed) if rows is None else rows
    return adapter.write(path, _schema(label_type), payload, "fixture_population")
