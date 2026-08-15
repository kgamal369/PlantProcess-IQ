"""The MF-03 handler, executed behind the versioned T-168 job protocol.

    eligibility  ->  mandatory baseline  ->  density candidate  ->  evidence

The order is the contract. The baseline runs first and always; the candidate runs
only where the population was eligible, which is the same condition that let the
baseline produce scores at all.

A refusal produces no evidence artifact. Nothing is written that a later stage could
mistake for a novelty result, and the manifest carries the code and the sentence.
"""

from __future__ import annotations

import json
import os
import sys
from typing import Any, Mapping, Sequence

from ...artifacts.hashing import artifact_byte_hash
from ...runtime.checkpoint import Checkpoint, CheckpointStore
from ...runtime.job_spec import JobSpec
from ...runtime.protocol import RefusalCode
from ...runtime.result_manifest import ProducedArtifact
from ...runtime.runner import RefusalError
from .baseline import RobustDeviationBaseline
from .candidate import NeighbourDensityCandidate
from .contract import NoveltyContractError, NoveltyResult
from .threshold import DEFAULT_QUANTILE

MF03_MODEL_FAMILY = "mf03_novelty"

EVALUATION_ARTIFACT_NAME = "mf03_novelty_evaluation.json"
EVIDENCE_RECORD_VERSION = "ppiq.mf03.evaluation/1"


def _write_artifact(directory: str, name: str, text: str, artifact_id: str, kind: str):
    os.makedirs(directory, exist_ok=True)
    path = os.path.join(directory, name)
    with open(path, "w", encoding="ascii", newline="\n") as handle:
        handle.write(text)
    return ProducedArtifact(
        artifact_id=artifact_id,
        uri=path,
        content_hash=artifact_byte_hash(path),
        artifact_kind=kind,
        byte_size=os.path.getsize(path),
    )


def _population_from_parameters(parameters: Mapping[str, Any]):
    declared = parameters.get("reference_population")
    if declared is None:
        raise RefusalError(
            RefusalCode.MALFORMED_JOB_SPEC,
            "The job spec declares no reference population. A novelty score is a "
            "distance from a reference, and without one there is nothing to measure "
            "against.",
        )
    for field in ("unit_ids", "feature_names", "rows"):
        if field not in declared:
            raise RefusalError(
                RefusalCode.MALFORMED_JOB_SPEC,
                f"The reference population declares no '{field}'.",
            )
    return (
        [str(u) for u in declared["unit_ids"]],
        [[float(v) for v in row] for row in declared["rows"]],
        [str(n) for n in declared["feature_names"]],
    )


def run_mf03(
    spec: JobSpec, store: CheckpointStore
) -> tuple[tuple[ProducedArtifact, ...], Mapping[str, float], str | None, tuple[str, ...]]:
    """The T-168 handler for the novelty family."""
    if spec.model_family != MF03_MODEL_FAMILY:
        raise RefusalError(
            RefusalCode.UNSUPPORTED_MODEL_FAMILY,
            f"This handler serves the model family '{MF03_MODEL_FAMILY}' and the job "
            f"spec declares '{spec.model_family}'.",
        )

    ids, rows, feature_names = _population_from_parameters(spec.parameters)
    quantile = float(spec.parameters.get("quantile", DEFAULT_QUANTILE))

    try:
        baseline_result = RobustDeviationBaseline().evaluate(
            ids, rows, feature_names, quantile, spec.seed
        )
    except NoveltyContractError as invalid:
        raise RefusalError(RefusalCode.MALFORMED_JOB_SPEC, str(invalid)) from invalid

    if baseline_result.refusal.refused:
        raise RefusalError(
            RefusalCode.ELIGIBILITY_NOT_MET, baseline_result.refusal.reason
        )
    store.write(
        Checkpoint(spec.job_id, "baseline", 1, {"model_code": baseline_result.model_code})
    )

    candidate_result = NeighbourDensityCandidate().evaluate(
        ids, rows, feature_names, quantile, spec.seed
    )
    if candidate_result.refusal.refused:
        raise RefusalError(
            RefusalCode.ELIGIBILITY_NOT_MET, candidate_result.refusal.reason
        )
    store.write(
        Checkpoint(spec.job_id, "candidate", 2, {"model_code": candidate_result.model_code})
    )

    record: dict[str, Any] = {
        "record_version": EVIDENCE_RECORD_VERSION,
        "model_family": MF03_MODEL_FAMILY,
        "reference_population_source": "fixture_declared_typed_contract",
        "environment": {
            "python_version": sys.version.split()[0],
            "seed": spec.seed,
            "code_identity": spec.code_identity,
        },
        "evaluation_order": [baseline_result.model_code, candidate_result.model_code],
        "results": [baseline_result.to_dict(), candidate_result.to_dict()],
        "selection_note": (
            "This record reports measurements only. Which family should serve is not "
            "decided here, and reference-population drift over time is production work "
            "outside this task."
        ),
    }

    artifacts = (
        _write_artifact(
            spec.output_directory,
            EVALUATION_ARTIFACT_NAME,
            json.dumps(record, indent=2, sort_keys=True),
            f"{spec.job_id}.novelty_evaluation",
            "evaluation",
        ),
    )

    metrics: dict[str, float] = {
        "population.reference_units": float(len(rows)),
        "population.distinct_units": float(
            baseline_result.population.distinct_reference_units
        ),
        "population.used_features": float(len(baseline_result.population.used_features)),
        "population.excluded_features": float(
            len(baseline_result.population.excluded_features)
        ),
    }
    for label, result in (("baseline", baseline_result), ("candidate", candidate_result)):
        metrics[f"{label}.threshold"] = float(result.threshold.value)
        metrics[f"{label}.flagged_units"] = float(len(result.flagged))
        metrics[f"{label}.max_score"] = float(result.scored_units[0].score)

    warnings = tuple(
        f"Feature '{exclusion.feature}' was excluded: {exclusion.reason}"
        for exclusion in baseline_result.population.excluded_features
    )

    return artifacts, metrics, "Finding", warnings


def build_job_parameters(
    ids: Sequence[str],
    rows: Sequence[Sequence[float]],
    feature_names: Sequence[str],
    quantile: float = DEFAULT_QUANTILE,
) -> dict[str, Any]:
    """Convenience for a caller assembling a job spec parameter block."""
    return {
        "reference_population": {
            "unit_ids": [str(u) for u in ids],
            "feature_names": [str(n) for n in feature_names],
            "rows": [[float(v) for v in row] for row in rows],
        },
        "quantile": quantile,
    }
