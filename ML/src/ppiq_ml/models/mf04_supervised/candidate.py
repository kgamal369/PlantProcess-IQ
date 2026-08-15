"""The LightGBM tabular candidate.

It is a candidate, not a replacement. It is fitted after the mandatory baseline, on
the same training rows, and evaluated on the same holdout, because a number produced
under different conditions is not a comparison.

THREE DECISIONS WORTH KNOWING.

The import is deferred to fit time. A machine that has never installed the family's
extra can still import this package, run the leakage gate and train the baseline. An
absent library is then reported as a named unavailability of this installation rather
than as a stack trace, and never as a property of the customer's data.

Determinism is configured explicitly. Row-wise histogram construction, a single
thread and the declared seed make two runs on identical input produce identical
bytes. Speed is deliberately traded for a reproducible artifact, because an artifact
whose hash moves between runs cannot serve as evidence.

Text-valued features are indexed on the training rows only. A value first seen in the
holdout maps to the unknown position rather than extending the index, because
extending it would let holdout content reach the fitted model.
"""

from __future__ import annotations

import json
from typing import Any, Mapping, Sequence

from .contract import (
    MODEL_CLASS_CANDIDATE,
    ModelUnavailableError,
    Population,
    SupervisedOutcomeModel,
    TrainedModel,
)
from .outcome import OutcomeKind

CANDIDATE_MODEL_CODE = "mf04.gbdt_tabular"

#: Small, explicit and stated. Every value here is part of the reproducibility
#: record and is written into the model artifact description.
DEFAULT_HYPERPARAMETERS: Mapping[str, Any] = {
    "num_leaves": 15,
    "max_depth": 5,
    "learning_rate": 0.1,
    "min_data_in_leaf": 5,
    "num_boost_round": 60,
    "feature_fraction": 1.0,
    "bagging_fraction": 1.0,
}

UNKNOWN_CATEGORY = -1


def _import_booster_library():
    try:
        import lightgbm  # noqa: PLC0415 - deferred on purpose, see module docstring
    except ImportError as missing:
        raise ModelUnavailableError(
            "The tabular candidate requires the 'lightgbm' package, which is not "
            "installed in this environment. Install the mf04_supervised extra to "
            "enable it. The mandatory baseline does not require it and is unaffected."
        ) from missing
    return lightgbm


class _CategoryIndex:
    """A deterministic text-to-integer index fitted on training rows only."""

    def __init__(self, values: Sequence[Any]) -> None:
        distinct = sorted({str(v) for v in values if v is not None})
        self._positions = {value: position for position, value in enumerate(distinct)}

    def encode(self, value: Any) -> int:
        if value is None:
            return UNKNOWN_CATEGORY
        return self._positions.get(str(value), UNKNOWN_CATEGORY)

    def to_dict(self) -> dict[str, int]:
        return dict(self._positions)


def _is_text(value: Any) -> bool:
    return isinstance(value, str)


class _TrainedGbdt(TrainedModel):
    def __init__(
        self,
        booster,
        kind: OutcomeKind,
        classes: tuple[Any, ...],
        feature_columns: tuple[str, ...],
        indexes: Mapping[int, _CategoryIndex],
        library_version: str,
        trained_rows: int,
        seed: int,
    ) -> None:
        self._booster = booster
        self._kind = kind
        self._classes = classes
        self._feature_columns = feature_columns
        self._indexes = indexes
        self._library_version = library_version
        self._trained_rows = trained_rows
        self._seed = seed

    @property
    def model_code(self) -> str:
        return CANDIDATE_MODEL_CODE

    @property
    def classes(self) -> tuple[Any, ...]:
        return self._classes

    def _encode(self, feature_rows: Sequence[Sequence[Any]]) -> list[list[float]]:
        encoded: list[list[float]] = []
        for row in feature_rows:
            cells: list[float] = []
            for position, value in enumerate(row):
                index = self._indexes.get(position)
                if index is not None:
                    cells.append(float(index.encode(value)))
                elif value is None:
                    cells.append(float("nan"))
                elif isinstance(value, bool):
                    cells.append(1.0 if value else 0.0)
                else:
                    cells.append(float(value))
            encoded.append(cells)
        return encoded

    def predict(self, feature_rows: Sequence[Sequence[Any]]) -> tuple:
        import numpy

        if not feature_rows:
            return ()
        matrix = numpy.array(self._encode(feature_rows), dtype=float)
        raw = self._booster.predict(matrix)

        if self._kind == OutcomeKind.CONTINUOUS:
            return tuple(float(v) for v in numpy.asarray(raw).reshape(-1))

        if len(self._classes) == 2:
            positive = numpy.asarray(raw, dtype=float).reshape(-1)
            return tuple((float(1.0 - p), float(p)) for p in positive)

        rows = numpy.asarray(raw, dtype=float).reshape(len(feature_rows), len(self._classes))
        return tuple(tuple(float(p) for p in row) for row in rows)

    def describe(self) -> Mapping[str, Any]:
        return {
            "model_code": CANDIDATE_MODEL_CODE,
            "library": "lightgbm",
            "library_version": self._library_version,
            "uses_features": True,
            "feature_columns": list(self._feature_columns),
            "text_indexed_positions": sorted(self._indexes),
            "hyperparameters": dict(DEFAULT_HYPERPARAMETERS),
            "seed": self._seed,
            "trained_rows": self._trained_rows,
            "outcome_kind": self._kind.value,
            "classes": [str(c) for c in self._classes],
        }

    def serialise(self) -> str:
        return json.dumps(
            {
                "model_code": CANDIDATE_MODEL_CODE,
                "description": dict(self.describe()),
                "category_indexes": {
                    str(position): index.to_dict() for position, index in self._indexes.items()
                },
                "booster": self._booster.model_to_string(),
            },
            indent=2,
            sort_keys=True,
        )


class GbdtTabularCandidate(SupervisedOutcomeModel):
    """Gradient boosted decision trees over the legal tabular features."""

    @property
    def model_code(self) -> str:
        return CANDIDATE_MODEL_CODE

    @property
    def model_class(self) -> str:
        return MODEL_CLASS_CANDIDATE

    def supports(self, kind: OutcomeKind) -> bool:
        return kind in (
            OutcomeKind.BINARY,
            OutcomeKind.MULTICLASS,
            OutcomeKind.ORDINAL,
            OutcomeKind.CONTINUOUS,
        )

    def fit(self, data: Population, seed: int) -> TrainedModel:
        booster_library = _import_booster_library()
        import numpy

        rows = len(data)
        if rows == 0:
            raise ValueError("The candidate cannot be fitted on an empty population.")

        indexes: dict[int, _CategoryIndex] = {}
        for position in range(len(data.feature_columns)):
            column_values = [row[position] for row in data.feature_rows]
            if any(_is_text(v) for v in column_values):
                indexes[position] = _CategoryIndex(column_values)

        encoded: list[list[float]] = []
        for row in data.feature_rows:
            cells: list[float] = []
            for position, value in enumerate(row):
                index = indexes.get(position)
                if index is not None:
                    cells.append(float(index.encode(value)))
                elif value is None:
                    cells.append(float("nan"))
                elif isinstance(value, bool):
                    cells.append(1.0 if value else 0.0)
                else:
                    cells.append(float(value))
            encoded.append(cells)
        matrix = numpy.array(encoded, dtype=float)

        kind = data.outcome.kind
        classes = data.observed_classes()
        parameters: dict[str, Any] = {
            "num_leaves": DEFAULT_HYPERPARAMETERS["num_leaves"],
            "max_depth": DEFAULT_HYPERPARAMETERS["max_depth"],
            "learning_rate": DEFAULT_HYPERPARAMETERS["learning_rate"],
            "min_data_in_leaf": DEFAULT_HYPERPARAMETERS["min_data_in_leaf"],
            "feature_fraction": DEFAULT_HYPERPARAMETERS["feature_fraction"],
            "bagging_fraction": DEFAULT_HYPERPARAMETERS["bagging_fraction"],
            "seed": seed,
            "deterministic": True,
            "force_row_wise": True,
            "num_threads": 1,
            "verbosity": -1,
        }

        if kind == OutcomeKind.CONTINUOUS:
            parameters["objective"] = "regression"
            targets = numpy.array([float(v) for v in data.labels], dtype=float)
        else:
            position_of = {c: i for i, c in enumerate(classes)}
            targets = numpy.array([position_of[v] for v in data.labels], dtype=float)
            if len(classes) == 2:
                parameters["objective"] = "binary"
            else:
                parameters["objective"] = "multiclass"
                parameters["num_class"] = len(classes)

        dataset = booster_library.Dataset(
            matrix,
            label=targets,
            categorical_feature=sorted(indexes) or "auto",
            free_raw_data=False,
        )
        booster = booster_library.train(
            parameters,
            dataset,
            num_boost_round=int(DEFAULT_HYPERPARAMETERS["num_boost_round"]),
        )

        return _TrainedGbdt(
            booster=booster,
            kind=kind,
            classes=classes,
            feature_columns=data.feature_columns,
            indexes=indexes,
            library_version=str(booster_library.__version__),
            trained_rows=rows,
            seed=seed,
        )
