"""Command-line entry point. The .NET job runner invokes this process.

    python -m ppiq_ml.runtime.cli --job-spec <path> --handler <module:function>

The handler is named by the caller rather than discovered, so the .NET side controls
exactly which computation runs and a stray module cannot be executed by accident.

Exit codes are DIAGNOSTIC ONLY. The .NET side reads the result manifest to decide
what happened. A process that exits zero without a valid manifest has failed.
"""

from __future__ import annotations

import argparse
import importlib
import os
import sys

from .job_spec import JobSpec
from .protocol import JobOutcome, ProtocolError, RefusalCode
from .result_manifest import MANIFEST_FILENAME
from .runner import run, write_manifest

EXIT_SUCCEEDED = 0
EXIT_REFUSED = 10
EXIT_FAILED = 20
EXIT_CANCELLED = 30
EXIT_NO_SPEC = 40

_EXIT_BY_OUTCOME = {
    JobOutcome.SUCCEEDED.value: EXIT_SUCCEEDED,
    JobOutcome.REFUSED.value: EXIT_REFUSED,
    JobOutcome.FAILED.value: EXIT_FAILED,
    JobOutcome.CANCELLED.value: EXIT_CANCELLED,
}


def resolve_handler(reference: str):
    """Resolve 'package.module:function'. A bad reference is a refusal, not a crash."""
    if ":" not in reference:
        raise ProtocolError(
            RefusalCode.MALFORMED_JOB_SPEC,
            f"Handler reference '{reference}' is not in the form module:function.",
        )
    module_name, function_name = reference.split(":", 1)
    try:
        module = importlib.import_module(module_name)
    except ImportError as exc:
        raise ProtocolError(
            RefusalCode.UNSUPPORTED_MODEL_FAMILY,
            f"Handler module '{module_name}' could not be imported: {exc}",
        ) from exc
    handler = getattr(module, function_name, None)
    if handler is None:
        raise ProtocolError(
            RefusalCode.UNSUPPORTED_MODEL_FAMILY,
            f"Handler '{function_name}' is not defined in '{module_name}'.",
        )
    return handler


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="ppiq_ml.runtime.cli")
    parser.add_argument("--job-spec", required=True)
    parser.add_argument("--handler", required=True)
    args = parser.parse_args(argv)

    if not os.path.exists(args.job_spec):
        # No spec means no output directory either, so there is nowhere to write a
        # manifest. Report on stderr and exit non-zero. The caller treats a missing
        # manifest as a failure regardless of what is printed here.
        sys.stderr.write(f"job spec not found: {args.job_spec}\n")
        return EXIT_NO_SPEC

    with open(args.job_spec, encoding="ascii") as handle:
        text = handle.read()

    try:
        spec = JobSpec.from_json(text)
    except ProtocolError as error:
        # The spec could not be interpreted. Write the refusal where the caller can
        # find it, if the raw payload at least tells us where that is.
        directory = _best_effort_output_directory(text)
        if directory:
            _write_bare_refusal(directory, error)
        sys.stderr.write(f"{error.code.value}: {error.message}\n")
        return EXIT_REFUSED

    try:
        handler = resolve_handler(args.handler)
    except ProtocolError as error:
        _write_bare_refusal(spec.output_directory, error, job_id=spec.job_id)
        sys.stderr.write(f"{error.code.value}: {error.message}\n")
        return EXIT_REFUSED

    manifest = run(spec, handler)
    return _EXIT_BY_OUTCOME.get(manifest.outcome, EXIT_FAILED)


def _best_effort_output_directory(text: str) -> str | None:
    import json

    try:
        raw = json.loads(text)
    except Exception:  # noqa: BLE001 - the payload is already known to be suspect
        return None
    value = raw.get("output_directory") if isinstance(raw, dict) else None
    return str(value) if value else None


def _write_bare_refusal(directory: str, error: ProtocolError, job_id: str = "unknown") -> None:
    from datetime import datetime, timezone

    from .protocol import PROTOCOL_ID
    from .result_manifest import ResultManifest
    from .runner import RUNTIME_VERSION

    now = datetime.now(timezone.utc).isoformat(timespec="microseconds")
    write_manifest(
        directory,
        ResultManifest(
            protocol=PROTOCOL_ID,
            job_id=job_id,
            outcome=JobOutcome.REFUSED.value,
            started_at_utc=now,
            completed_at_utc=now,
            duration_seconds=0.0,
            code_identity="",
            seed=0,
            runtime_version=RUNTIME_VERSION,
            refusal_code=error.code.value,
            refusal_reason=error.message,
        ),
    )


if __name__ == "__main__":
    sys.exit(main())
