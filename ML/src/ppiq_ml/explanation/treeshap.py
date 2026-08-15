"""The TreeSHAP candidate, produced from a fitted tree model.

WHY NO NEW PACKAGE. TreeSHAP is an algorithm, not a library. The booster already
pinned by the supervised task computes exact tree contributions natively, and those
contributions carry the property that defines the method: for every unit, the base
value plus the feature contributions reconstructs the model's raw output exactly.
Adding a second implementation of the same algorithm would put a second version
number into an answer that has to be reproducible, and would buy nothing.

WHAT THE PROVIDER RETURNS. The producer emits one contribution per feature per unit
plus the base value that row extends. The values sit on the model's raw output scale.
For a binary objective that is the log-odds margin, not a probability, and the scale
is recorded so nobody adds a contribution to a probability.

WHAT IT REFUSES. A model that cannot produce contributions, a feature name list that
does not match the matrix, and a contribution table whose width does not match the
declared features plus one for the base value. Each of those is a sign that the
evidence is about a different model from the one the caller believes.
"""

from __future__ import annotations

from typing import Any, Sequence

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


def _import_numeric_library():
    try:
        import numpy  # noqa: PLC0415 - deferred so the contract imports without it
    except ImportError as missing:
        raise ExplanationUnavailableError(
            "The tree contribution provider requires the 'numpy' package, which is not "
            "installed in this environment. The promotion kernel does not require it "
            "and is unaffected."
        ) from missing
    return numpy


class LightGbmTreeShapExplanationProvider(ExplanationProvider):
    """Exact tree contributions from a fitted gradient boosted model.

    The model is accepted structurally rather than by type: anything that answers
    predict with a contribution request is explainable here. That is what keeps this
    provider replaceable and keeps a specific library out of the interface.
    """

    @property
    def method(self) -> str:
        return TREESHAP_METHOD

    def supports(self, model: Any) -> bool:
        return callable(getattr(model, "predict", None))

    def explain(
        self,
        model: Any,
        feature_rows: Sequence[Sequence[Any]],
        feature_names: Sequence[str],
        identity: EvidenceIdentity,
        output_index: int = 0,
    ) -> ContributionEvidence:
        if not self.supports(model):
            raise ExplanationUnavailableError(
                "The supplied model cannot be asked for contributions. This provider "
                "explains fitted tree models and refuses rather than guessing."
            )
        if not feature_rows:
            raise ExplanationError("There are no units to explain.")
        names = tuple(str(n) for n in feature_names)
        width = len(names)
        if width == 0:
            raise ExplanationError("Explanation requires at least one named feature.")
        for index, row in enumerate(feature_rows):
            if len(row) != width:
                raise ExplanationError(
                    f"Unit {index} carries {len(row)} feature values against {width} "
                    "declared feature name(s). The explanation would not line up with "
                    "the model input."
                )

        numeric = _import_numeric_library()
        matrix = numeric.array(
            [[_as_number(value) for value in row] for row in feature_rows], dtype=float
        )

        try:
            raw = numeric.asarray(model.predict(matrix, pred_contrib=True), dtype=float)
        except TypeError as unsupported:
            raise ExplanationUnavailableError(
                "The supplied model does not support a contribution request, so no "
                "tree contribution evidence can be produced from it."
            ) from unsupported

        block = width + 1
        if raw.ndim != 2 or raw.shape[1] % block != 0:
            raise ExplanationError(
                f"The model returned a contribution table of shape {tuple(raw.shape)}, "
                f"which is not a multiple of the {width} declared feature(s) plus one "
                "base value column. The evidence is about a different feature set."
            )

        outputs = raw.shape[1] // block
        if not 0 <= output_index < outputs:
            raise ExplanationError(
                f"Output index {output_index} was requested but the model produces "
                f"{outputs} output(s)."
            )

        start = output_index * block
        contributions = tuple(
            tuple(float(v) for v in raw[unit, start : start + width])
            for unit in range(raw.shape[0])
        )
        base_values = tuple(float(raw[unit, start + width]) for unit in range(raw.shape[0]))

        return ContributionEvidence(
            explanation_method=self.method,
            claim_class=ClaimClass.PREDICTIVE_CONTRIBUTION,
            contribution_scale=ContributionScale.RAW_MODEL_OUTPUT,
            identity=identity,
            feature_names=names,
            contributions=contributions,
            base_values=base_values,
            output_index=output_index,
        )


def _as_number(value: Any) -> float:
    if value is None:
        return float("nan")
    if isinstance(value, bool):
        return 1.0 if value else 0.0
    return float(value)
