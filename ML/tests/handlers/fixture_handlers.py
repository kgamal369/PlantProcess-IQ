"""Handlers that drive one falsification case each, invoked across the real
C# to Python process boundary. Test scaffolding: not part of the shipped runtime.
"""

from __future__ import annotations

import os
import sys
import time

from ppiq_ml.runtime.checkpoint import Checkpoint
from ppiq_ml.runtime.protocol import RefusalCode
from ppiq_ml.runtime.result_manifest import ProducedArtifact
from ppiq_ml.runtime.runner import RefusalError


def succeed(spec, store):
    """Ordinary success with artifacts, metrics and a warning."""
    return (
        (ProducedArtifact("model-1", "/tmp/model.bin", "deadbeef", "model", 1024),),
        {"auc": 0.834, "brier": 0.112},
        None,
        ("baseline beat the challenger on calibration",),
    )


def succeed_with_honest_analysis_refusal(spec, store):
    """The JOB succeeds; the ANALYSIS honestly refuses. Two different axes."""
    return ((), {}, "InsufficientData", ("population below the declared floor",))


def refuse(spec, store):
    """A governed refusal with a code and a sentence."""
    raise RefusalError(
        RefusalCode.ELIGIBILITY_NOT_MET,
        "The declared outcome carries 12 labelled units against a floor of 500.",
    )


def crash(spec, store):
    """An unhandled error. Must become failed, never refused."""
    raise ZeroDivisionError("division by zero deep inside a fit")


def hang(spec, store):
    """Sleeps past any sane budget so the caller must time it out."""
    time.sleep(600)
    return ((), {}, None, ())


def cancellable(spec, store):
    """Waits for the caller to create the cancellation file, then returns.

    The runner checks cancellation after the handler, so the manifest reports
    cancelled rather than succeeded.
    """
    deadline = time.time() + 60
    while time.time() < deadline:
        if spec.cancellation_file and os.path.exists(spec.cancellation_file):
            return ((), {}, None, ())
        time.sleep(0.05)
    return ((), {}, None, ())


def checkpointing(spec, store):
    """Resumes from the newest checkpoint, then advances one stage."""
    latest = store.latest()
    next_sequence = (latest.sequence + 1) if latest else 1
    store.write(Checkpoint(spec.job_id, f"stage-{next_sequence}", next_sequence,
                           {"completed_stages": next_sequence}))
    return ((), {"stages_completed": float(next_sequence)}, None, ())


def stdout_liar(spec, store):
    """Prints success loudly on both streams, then fails.

    The manifest must say failed. Console text is never authority.
    """
    sys.stdout.write("SUCCESS model trained, auc 0.99, promoted to champion\n")
    sys.stdout.flush()
    sys.stderr.write("INFO all gates passed\n")
    sys.stderr.flush()
    raise RuntimeError("the fit never converged")


def write_no_manifest(spec, store):
    """Never reached. See the CLI-level cases below."""
    raise AssertionError("unreachable")
