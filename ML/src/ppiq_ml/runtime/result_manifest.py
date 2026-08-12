"""The structured result the Python runtime writes and the .NET runner reads.

This file is the authority on what happened. stdout and stderr are diagnostics.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field, asdict
from typing import Any, Mapping

from .protocol import PROTOCOL_ID, JobOutcome, RefusalCode

MANIFEST_FILENAME = "result_manifest.json"


@dataclass(frozen=True)
class ProducedArtifact:
    artifact_id: str
    uri: str
    content_hash: str
    artifact_kind: str
    byte_size: int = 0


@dataclass(frozen=True)
class ResultManifest:
    """What the runtime did, what it produced, and what it refused.

    The runtime reports facts. It never concludes that a model is production
    champion; that decision belongs to the .NET governance side.
    """

    protocol: str
    job_id: str
    outcome: str
    started_at_utc: str
    completed_at_utc: str
    duration_seconds: float
    code_identity: str
    seed: int
    runtime_version: str
    refusal_code: str = RefusalCode.NONE.value
    refusal_reason: str = ""
    artifacts: tuple[ProducedArtifact, ...] = ()
    metrics: Mapping[str, float] = field(default_factory=dict)
    #: The analysis-side terminal state, when the job ran an analysis. Distinct from
    #: outcome: a SUCCEEDED job may carry an honest INSUFFICIENT_DATA result.
    analysis_terminal_state: str | None = None
    input_hashes: Mapping[str, str] = field(default_factory=dict)
    warnings: tuple[str, ...] = ()
    resumed_from_checkpoint: str | None = None

    def to_json(self) -> str:
        return json.dumps(asdict(self), indent=2, sort_keys=True)

    def integrity_hash(self) -> str:
        """Hash over the manifest content, so the caller can detect a truncated write."""
        return hashlib.sha256(self.to_json().encode("ascii")).hexdigest()

    @staticmethod
    def from_json(text: str) -> "ResultManifest":
        raw = json.loads(text)
        return ResultManifest.from_dict(raw)

    @staticmethod
    def from_dict(raw: Mapping[str, Any]) -> "ResultManifest":
        if not isinstance(raw, Mapping):
            raise ValueError("The result manifest is not an object.")

        required = ("protocol", "job_id", "outcome", "duration_seconds")
        missing = [k for k in required if k not in raw]
        if missing:
            raise ValueError(
                f"The result manifest is missing required fields: {', '.join(sorted(missing))}."
            )
        if raw["protocol"] != PROTOCOL_ID:
            raise ValueError(
                f"The result manifest declares protocol '{raw['protocol']}'; "
                f"this runtime speaks '{PROTOCOL_ID}'."
            )
        if raw["outcome"] not in {o.value for o in JobOutcome}:
            raise ValueError(f"Unknown job outcome '{raw['outcome']}'.")

        return ResultManifest(
            protocol=str(raw["protocol"]),
            job_id=str(raw["job_id"]),
            outcome=str(raw["outcome"]),
            started_at_utc=str(raw.get("started_at_utc", "")),
            completed_at_utc=str(raw.get("completed_at_utc", "")),
            duration_seconds=float(raw["duration_seconds"]),
            code_identity=str(raw.get("code_identity", "")),
            seed=int(raw.get("seed", 0)),
            runtime_version=str(raw.get("runtime_version", "")),
            refusal_code=str(raw.get("refusal_code", RefusalCode.NONE.value)),
            refusal_reason=str(raw.get("refusal_reason", "")),
            artifacts=tuple(
                ProducedArtifact(
                    artifact_id=str(a["artifact_id"]),
                    uri=str(a["uri"]),
                    content_hash=str(a["content_hash"]),
                    artifact_kind=str(a["artifact_kind"]),
                    byte_size=int(a.get("byte_size", 0)),
                )
                for a in raw.get("artifacts", [])
            ),
            metrics=dict(raw.get("metrics", {})),
            analysis_terminal_state=raw.get("analysis_terminal_state"),
            input_hashes=dict(raw.get("input_hashes", {})),
            warnings=tuple(raw.get("warnings", [])),
            resumed_from_checkpoint=raw.get("resumed_from_checkpoint"),
        )


def validate_refusal_consistency(manifest: ResultManifest) -> None:
    """A refusal must carry a code and a sentence. A success must carry neither."""
    if manifest.outcome == JobOutcome.REFUSED.value:
        if manifest.refusal_code == RefusalCode.NONE.value:
            raise ValueError("A refused job must carry a refusal code.")
        if not manifest.refusal_reason.strip():
            raise ValueError("A refused job must carry a written reason.")
    if manifest.outcome == JobOutcome.SUCCEEDED.value:
        if manifest.refusal_code != RefusalCode.NONE.value:
            raise ValueError("A succeeded job must not carry a refusal code.")
