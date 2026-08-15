"""The MF-04 handler, executed behind the versioned T-168 job protocol.

THE ORDER IS THE CONTRACT.

    leakage gate -> population -> eligibility gate -> mandatory baseline
                 -> candidate -> one shared holdout -> evidence

Both gates run before any model is fitted. A blocked job therefore produces no model
artifact at all, which is a stronger statement than producing one and marking it
unusable: there is nothing on disk that a later stage could pick up by mistake.

The baseline is always fitted first and always fitted, including when the candidate
cannot run in this environment. The floor is the part of the answer that must exist.

WHAT THIS HANDLER NEVER DOES. It does not select. It writes both results, their
difference on a shared holdout, and the identity of the data both saw. Which model a
product should serve is decided against calibration, explanation stability and
serving cost, and those dimensions belong to T-176.
"""

from __future__ import annotations

import json
import os
import sys
import time
from typing import Any, Mapping, Sequence

from ...artifacts.hashing import artifact_byte_hash
from ...runtime.checkpoint import Checkpoint, CheckpointStore
from ...runtime.job_spec import JobSpec
from ...runtime.protocol import RefusalCode
from ...runtime.result_manifest import ProducedArtifact
from ...runtime.runner import RefusalError
from .baseline import PriorBaseline
from .candidate import GbdtTabularCandidate
from .contract import (
    ModelEvaluation,
    ModelUnavailableError,
    Population,
    SupervisedOutcomeModel,
    TrainedModel,
    compare,
)
from .eligibility import evaluate_eligibility
from .holdout import HOLDOUT_FRACTION, split_out_of_time
from .leakage import evaluate_leakage
from .metrics import MetricSet, evaluate_classification, evaluate_continuous
from .outcome import OutcomeContractError, OutcomeDefinition, OutcomeKind
from .population import PopulationContractError, load_population

MF04_MODEL_FAMILY = "mf04_supervised"

EVALUATION_ARTIFACT_NAME = "mf04_evaluation.json"
BASELINE_ARTIFACT_NAME = "mf04_baseline_model.json"
CANDIDATE_ARTIFACT_NAME = "mf04_candidate_model.json"

#: Written into the evidence record so a later reader knows which shape of document
#: it is holding.
EVALUATION_RECORD_VERSION = "ppiq.mf04.evaluation/1"


def _score(outcome: OutcomeDefinition, model: TrainedModel, holdout: Population) -> MetricSet:
    predictions = model.predict(holdout.feature_rows)
    if outcome.kind == OutcomeKind.CONTINUOUS:
        return evaluate_continuous(holdout.labels, predictions)
    return evaluate_classification(
        classes=model.classes,
        labels=holdout.labels,
        probabilities=predictions,
        class_order=outcome.class_order,
    )


def _fit_and_score(
    family: SupervisedOutcomeModel,
    split_train: Population,
    split_holdout: Population,
    seed: int,
    holdout_identity: str,
) -> tuple[TrainedModel, ModelEvaluation]:
    started = time.monotonic()
    trained = family.fit(split_train, seed)
    trained_at = time.monotonic()
    metrics = _score(split_train.outcome, trained, split_holdout)
    scored_at = time.monotonic()

    return trained, ModelEvaluation(
        model_code=family.model_code,
        model_class=family.model_class,
        metrics=metrics,
        training_seconds=trained_at - started,
        scoring_seconds=scored_at - trained_at,
        description=trained.describe(),
        snapshot_identity=split_train.snapshot_identity,
        holdout_identity=holdout_identity,
    )


def _write_text_artifact(
    directory: str, name: str, text: str, artifact_id: str, kind: str
) -> ProducedArtifact:
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


def _flatten(prefix: str, metrics: MetricSet) -> dict[str, float]:
    return {f"{prefix}.{name}": float(value) for name, value in metrics.values.items()}


def run_mf04(
    spec: JobSpec, store: CheckpointStore
) -> tuple[tuple[ProducedArtifact, ...], Mapping[str, float], str | None, tuple[str, ...]]:
    """The T-168 handler for the supervised outcome family."""
    if spec.model_family != MF04_MODEL_FAMILY:
        raise RefusalError(
            RefusalCode.UNSUPPORTED_MODEL_FAMILY,
            f"This handler serves the model family '{MF04_MODEL_FAMILY}' and the job "
            f"spec declares '{spec.model_family}'.",
        )

    declared = spec.parameters.get("outcome_definition")
    if declared is None:
        raise RefusalError(
            RefusalCode.MALFORMED_JOB_SPEC,
            "The job spec declares no outcome definition. Supervised training without a "
            "declared outcome, prediction position and detection position has no way to "
            "tell a prediction from a lookup.",
        )
    try:
        outcome = OutcomeDefinition.from_dict(declared)
    except OutcomeContractError as invalid:
        raise RefusalError(RefusalCode.MALFORMED_JOB_SPEC, str(invalid)) from invalid

    if len(spec.inputs) != 1:
        raise RefusalError(
            RefusalCode.MALFORMED_JOB_SPEC,
            f"MF-04 trains on exactly one sealed population artifact; the job spec "
            f"declares {len(spec.inputs)}.",
        )

    # ---------------------------------------------------------------- gate one
    leakage = evaluate_leakage(outcome)
    if not leakage.passed:
        raise RefusalError(RefusalCode.ELIGIBILITY_NOT_MET, leakage.reason)
    store.write(
        Checkpoint(spec.job_id, "leakage", 1, {"legal_features": list(leakage.legal_features)})
    )

    # ------------------------------------------------------------- population
    artifact = spec.inputs[0]
    try:
        population = load_population(
            uri=artifact.uri,
            artifact_format=artifact.artifact_format,
            outcome=outcome,
            legal_features=leakage.legal_features,
        )
    except PopulationContractError as mismatch:
        raise RefusalError(RefusalCode.ELIGIBILITY_NOT_MET, str(mismatch)) from mismatch

    total = len(population)
    if total >= 2:
        split = split_out_of_time(population, HOLDOUT_FRACTION)
        train_units, holdout_units = len(split.train), len(split.holdout)
    else:
        split = None
        train_units, holdout_units = total, 0

    # ---------------------------------------------------------------- gate two
    eligibility = evaluate_eligibility(
        outcome=outcome,
        leakage=leakage,
        labels=population.labels,
        train_units=train_units,
        holdout_units=holdout_units,
    )
    if not eligibility.eligible or split is None:
        raise RefusalError(RefusalCode.ELIGIBILITY_NOT_MET, eligibility.reason)
    store.write(
        Checkpoint(
            spec.job_id,
            "eligibility",
            2,
            {"train_units": train_units, "holdout_units": holdout_units},
        )
    )

    # ------------------------------------------------ the mandatory floor first
    baseline_model, baseline_evaluation = _fit_and_score(
        PriorBaseline(), split.train, split.holdout, spec.seed, split.holdout_identity
    )
    store.write(Checkpoint(spec.job_id, "baseline", 3, {"model_code": baseline_model.model_code}))

    # ------------------------------------------------------ then the candidate
    try:
        candidate_model, candidate_evaluation = _fit_and_score(
            GbdtTabularCandidate(), split.train, split.holdout, spec.seed, split.holdout_identity
        )
    except ModelUnavailableError as unavailable:
        raise RefusalError(RefusalCode.UNSUPPORTED_MODEL_FAMILY, str(unavailable)) from unavailable
    store.write(Checkpoint(spec.job_id, "candidate", 4, {"model_code": candidate_model.model_code}))

    comparison = compare(baseline_evaluation, candidate_evaluation)

    # ------------------------------------------------------------------ evidence
    record: dict[str, Any] = {
        "record_version": EVALUATION_RECORD_VERSION,
        "model_family": MF04_MODEL_FAMILY,
        "outcome_definition": outcome.to_dict(),
        "outcome_definition_source": "fixture_declared_typed_contract",
        "leakage": leakage.to_dict(),
        "eligibility": eligibility.to_dict(),
        "snapshot": {
            "input_artifact_id": artifact.artifact_id,
            "input_artifact_format": artifact.artifact_format,
            "input_content_hash": artifact.content_hash,
            "snapshot_identity": population.snapshot_identity,
            "population_units": total,
        },
        "holdout": {
            "strategy": "out_of_time_tail",
            "fraction": split.fraction,
            "holdout_identity": split.holdout_identity,
            "train_units": train_units,
            "holdout_units": holdout_units,
        },
        "environment": {
            "python_version": sys.version.split()[0],
            "seed": spec.seed,
            "code_identity": spec.code_identity,
        },
        "training_order": [baseline_evaluation.model_code, candidate_evaluation.model_code],
        "models": [
            {
                "model_code": baseline_evaluation.model_code,
                "model_class": baseline_evaluation.model_class,
                "metrics": baseline_evaluation.metrics.to_dict(),
                "description": dict(baseline_evaluation.description),
            },
            {
                "model_code": candidate_evaluation.model_code,
                "model_class": candidate_evaluation.model_class,
                "metrics": candidate_evaluation.metrics.to_dict(),
                "description": dict(candidate_evaluation.description),
            },
        ],
        "comparison": comparison.to_dict(),
        "selection_note": (
            "This record reports measurements only. Selection between the floor and the "
            "candidate is decided by the T-176 kernel against calibration, explanation "
            "stability and serving cost, which this task does not measure."
        ),
    }

    artifacts = (
        _write_text_artifact(
            spec.output_directory,
            BASELINE_ARTIFACT_NAME,
            baseline_model.serialise(),
            f"{spec.job_id}.baseline_model",
            "model",
        ),
        _write_text_artifact(
            spec.output_directory,
            CANDIDATE_ARTIFACT_NAME,
            candidate_model.serialise(),
            f"{spec.job_id}.candidate_model",
            "model",
        ),
        _write_text_artifact(
            spec.output_directory,
            EVALUATION_ARTIFACT_NAME,
            json.dumps(record, indent=2, sort_keys=True),
            f"{spec.job_id}.evaluation",
            "evaluation",
        ),
    )

    metrics: dict[str, float] = {
        "population.units": float(total),
        "train.units": float(train_units),
        "holdout.units": float(holdout_units),
        "baseline.training_seconds": baseline_evaluation.training_seconds,
        "candidate.training_seconds": candidate_evaluation.training_seconds,
    }
    metrics.update(_flatten("baseline", baseline_evaluation.metrics))
    metrics.update(_flatten("candidate", candidate_evaluation.metrics))

    warnings: list[str] = []
    for label, evaluation in (
        ("baseline", baseline_evaluation),
        ("candidate", candidate_evaluation),
    ):
        for name, sentence in evaluation.metrics.undefined.items():
            warnings.append(f"{label}.{name} is undefined: {sentence}")

    return artifacts, metrics, "Finding", tuple(warnings)


def build_job_parameters(outcome: OutcomeDefinition) -> dict[str, Any]:
    """Convenience for a caller assembling a job spec parameter block."""
    return {"outcome_definition": outcome.to_dict()}


def supported_outcome_kinds() -> Sequence[str]:
    return tuple(kind.value for kind in OutcomeKind)
