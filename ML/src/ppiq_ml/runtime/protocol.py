"""Versioned execution protocol between the .NET job runner and the Python ML runtime.

Two rules govern this module.

The structured result manifest is the only authority. stdout and stderr are
diagnostics; a process that prints SUCCESS but writes no valid manifest has failed.

Job outcome and analysis terminal state are different axes and are never collapsed.
A job can succeed while the analysis it ran honestly refuses to produce a finding.
"""

from __future__ import annotations

from enum import Enum

#: Wire protocol identity. The .NET side pins the same string. A mismatch is refused
#: before any payload is interpreted, so an old runtime can never be fed a new spec.
PROTOCOL_NAME = "ppiq.mljob"
PROTOCOL_VERSION = 1
PROTOCOL_ID = f"{PROTOCOL_NAME}/{PROTOCOL_VERSION}"


class JobOutcome(str, Enum):
    """How the EXECUTION ended. Not what the analysis concluded."""

    SUCCEEDED = "succeeded"
    #: The runtime declined to compute, for a stated and governed reason.
    #: This is a valid outcome, not an error.
    REFUSED = "refused"
    #: Something went wrong that the runtime did not anticipate.
    FAILED = "failed"
    CANCELLED = "cancelled"
    #: Set by the caller when the wall clock exceeded the budget. The Python side
    #: never reports this about itself, because a timed-out process cannot report.
    TIMED_OUT = "timed_out"


class RefusalCode(str, Enum):
    """Why the runtime declined. Execution-side reasons only.

    Statistical-method reasons and capability shortfalls live in their own code sets
    on the .NET side and never appear here.
    """

    NONE = "none"
    PROTOCOL_VERSION_MISMATCH = "protocol_version_mismatch"
    MALFORMED_JOB_SPEC = "malformed_job_spec"
    ARTIFACT_MISSING = "artifact_missing"
    ARTIFACT_HASH_MISMATCH = "artifact_hash_mismatch"
    UNSUPPORTED_MODEL_FAMILY = "unsupported_model_family"
    ELIGIBILITY_NOT_MET = "eligibility_not_met"
    OUTPUT_LOCATION_UNWRITABLE = "output_location_unwritable"


class ProtocolError(Exception):
    """Raised when a payload cannot be interpreted under this protocol version."""

    def __init__(self, code: RefusalCode, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


def check_protocol(declared: str) -> None:
    """Refuse a payload whose protocol identity is not exactly ours."""
    if declared != PROTOCOL_ID:
        raise ProtocolError(
            RefusalCode.PROTOCOL_VERSION_MISMATCH,
            f"Job spec declares protocol '{declared}'; this runtime speaks '{PROTOCOL_ID}'. "
            "The payload was not interpreted.",
        )
