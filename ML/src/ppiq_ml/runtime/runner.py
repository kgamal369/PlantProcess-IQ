"""The runtime entry point. Reads a job spec, runs a handler, writes a manifest.

The manifest is always written, including on refusal and on unexpected failure.
A caller that finds no manifest may conclude only that the process died, never that
the job succeeded.
"""

from __future__ import annotations

import hashlib
import os
import traceback
from datetime import datetime, timezone
from typing import Callable, Mapping

from .checkpoint import CheckpointStore
from .job_spec import JobSpec
from .protocol import PROTOCOL_ID, JobOutcome, ProtocolError, RefusalCode
from .result_manifest import MANIFEST_FILENAME, ProducedArtifact, ResultManifest

RUNTIME_VERSION = "ppiq_ml 0.1.0"

#: A handler receives the spec and a checkpoint store, and returns
#: (artifacts, metrics, analysis_terminal_state, warnings).
Handler = Callable[
    [JobSpec, CheckpointStore],
    tuple[tuple[ProducedArtifact, ...], Mapping[str, float], str | None, tuple[str, ...]],
]


class CancelledError(Exception):
    """Raised when the caller signalled cancellation between stages."""


class RefusalError(Exception):
    """Raised by a handler that declines to compute for a stated, governed reason."""

    def __init__(self, code: RefusalCode, reason: str) -> None:
        super().__init__(reason)
        self.code = code
        self.reason = reason


def _now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="microseconds")


def is_cancelled(spec: JobSpec) -> bool:
    """Cancellation is a file the caller creates. Checked between stages only."""
    return bool(spec.cancellation_file) and os.path.exists(spec.cancellation_file)


def hash_inputs(spec: JobSpec) -> dict[str, str]:
    """Record the declared hash of every input, so the caller can verify lineage."""
    return {a.artifact_id: a.content_hash for a in spec.inputs}


def verify_inputs(spec: JobSpec) -> None:
    """Refuse before computing if a declared artifact is absent or does not match."""
    for artifact in spec.inputs:
        if not os.path.exists(artifact.uri):
            raise RefusalError(
                RefusalCode.ARTIFACT_MISSING,
                f"Input artifact '{artifact.artifact_id}' is not present at its declared "
                f"location. The runtime reads sealed artifacts and never a database, so "
                f"a missing artifact is a refusal rather than a fallback.",
            )
        with open(artifact.uri, "rb") as handle:
            actual = hashlib.sha256(handle.read()).hexdigest()
        if artifact.content_hash and actual != artifact.content_hash:
            raise RefusalError(
                RefusalCode.ARTIFACT_HASH_MISMATCH,
                f"Input artifact '{artifact.artifact_id}' hashes to {actual[:16]} but the "
                f"job spec declares {artifact.content_hash[:16]}. The artifact is not the "
                f"one the job was authorised against.",
            )


def write_manifest(directory: str, manifest: ResultManifest) -> str:
    os.makedirs(directory, exist_ok=True)
    path = os.path.join(directory, MANIFEST_FILENAME)
    tmp = path + ".partial"
    with open(tmp, "w", encoding="ascii", newline="\n") as handle:
        handle.write(manifest.to_json())
    os.replace(tmp, path)
    return path


def run(spec: JobSpec, handler: Handler) -> ResultManifest:
    """Execute one job. Always returns a manifest; never raises to the caller."""
    started = _now()
    monotonic_start = datetime.now(timezone.utc)
    store = CheckpointStore(spec.checkpoint_directory)
    resumed = store.latest() if store.enabled else None

    def finish(
        outcome: JobOutcome,
        refusal_code: RefusalCode = RefusalCode.NONE,
        reason: str = "",
        artifacts: tuple[ProducedArtifact, ...] = (),
        metrics: Mapping[str, float] | None = None,
        analysis_state: str | None = None,
        warnings: tuple[str, ...] = (),
    ) -> ResultManifest:
        completed = _now()
        duration = (datetime.now(timezone.utc) - monotonic_start).total_seconds()
        manifest = ResultManifest(
            protocol=PROTOCOL_ID,
            job_id=spec.job_id,
            outcome=outcome.value,
            started_at_utc=started,
            completed_at_utc=completed,
            duration_seconds=duration,
            code_identity=spec.code_identity,
            seed=spec.seed,
            runtime_version=RUNTIME_VERSION,
            refusal_code=refusal_code.value,
            refusal_reason=reason,
            artifacts=artifacts,
            metrics=dict(metrics or {}),
            analysis_terminal_state=analysis_state,
            input_hashes=hash_inputs(spec),
            warnings=warnings,
            resumed_from_checkpoint=(resumed.stage if resumed else None),
        )
        write_manifest(spec.output_directory, manifest)
        return manifest

    try:
        if is_cancelled(spec):
            return finish(JobOutcome.CANCELLED, reason="Cancellation was signalled before the job began.")

        verify_inputs(spec)
        artifacts, metrics, analysis_state, warnings = handler(spec, store)

        if is_cancelled(spec):
            return finish(JobOutcome.CANCELLED, reason="Cancellation was signalled during execution.")

        return finish(
            JobOutcome.SUCCEEDED,
            artifacts=artifacts,
            metrics=metrics,
            analysis_state=analysis_state,
            warnings=warnings,
        )

    except RefusalError as refusal:
        return finish(JobOutcome.REFUSED, refusal.code, refusal.reason)
    except ProtocolError as protocol_error:
        return finish(JobOutcome.REFUSED, protocol_error.code, protocol_error.message)
    except CancelledError as cancelled:
        return finish(JobOutcome.CANCELLED, reason=str(cancelled))
    except Exception as unexpected:  # noqa: BLE001 - the manifest must survive anything
        return finish(
            JobOutcome.FAILED,
            reason=(
                f"{type(unexpected).__name__}: {unexpected}\n"
                + "".join(traceback.format_exception_only(type(unexpected), unexpected)).strip()
            ),
        )
