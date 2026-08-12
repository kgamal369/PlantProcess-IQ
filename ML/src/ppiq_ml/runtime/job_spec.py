"""The job specification the .NET runner writes and the Python runtime reads."""

from __future__ import annotations

import json
from dataclasses import dataclass, field, asdict
from typing import Any, Mapping

from .protocol import PROTOCOL_ID, ProtocolError, RefusalCode, check_protocol


@dataclass(frozen=True)
class ResourceBudget:
    """What the job is allowed to consume. Enforced by the caller, declared here."""

    max_wall_clock_seconds: float
    max_memory_mb: int = 0
    gpu_required: bool = False


@dataclass(frozen=True)
class ArtifactRef:
    """A sealed input artifact. The runtime never reads a database; it reads these."""

    artifact_id: str
    uri: str
    content_hash: str
    artifact_format: str
    byte_size: int = 0


@dataclass(frozen=True)
class JobSpec:
    """Identity, context and inputs for one ML execution.

    Carries no connection string, no table name and no SQL. The runtime's entire view
    of the customer's data is the sealed artifact list.
    """

    protocol: str
    job_id: str
    tenant_id: str
    site_id: str
    model_family: str
    inputs: tuple[ArtifactRef, ...]
    output_directory: str
    seed: int
    code_identity: str
    resources: ResourceBudget
    #: Present once the canonical Semantic Contract Manifest exists. Optional today,
    #: because SAFE-NOW work runs against fixture contracts.
    semantic_manifest_id: str | None = None
    checkpoint_directory: str | None = None
    cancellation_file: str | None = None
    parameters: Mapping[str, Any] = field(default_factory=dict)

    # ------------------------------------------------------------------ codec

    def to_json(self) -> str:
        return json.dumps(asdict(self), indent=2, sort_keys=True)

    @staticmethod
    def from_json(text: str) -> "JobSpec":
        try:
            raw = json.loads(text)
        except json.JSONDecodeError as exc:
            raise ProtocolError(
                RefusalCode.MALFORMED_JOB_SPEC,
                f"The job spec is not valid JSON: {exc}",
            ) from exc
        return JobSpec.from_dict(raw)

    @staticmethod
    def from_dict(raw: Mapping[str, Any]) -> "JobSpec":
        if not isinstance(raw, Mapping):
            raise ProtocolError(RefusalCode.MALFORMED_JOB_SPEC, "The job spec is not an object.")

        declared = raw.get("protocol")
        if declared is None:
            raise ProtocolError(
                RefusalCode.MALFORMED_JOB_SPEC,
                "The job spec declares no protocol. It was not interpreted.",
            )
        check_protocol(str(declared))

        required = (
            "job_id", "tenant_id", "site_id", "model_family",
            "output_directory", "seed", "code_identity", "resources",
        )
        missing = [k for k in required if k not in raw]
        if missing:
            raise ProtocolError(
                RefusalCode.MALFORMED_JOB_SPEC,
                f"The job spec is missing required fields: {', '.join(sorted(missing))}.",
            )

        res = raw["resources"]
        if not isinstance(res, Mapping) or "max_wall_clock_seconds" not in res:
            raise ProtocolError(
                RefusalCode.MALFORMED_JOB_SPEC,
                "The job spec declares no wall-clock budget.",
            )

        inputs = tuple(
            ArtifactRef(
                artifact_id=str(a["artifact_id"]),
                uri=str(a["uri"]),
                content_hash=str(a["content_hash"]),
                artifact_format=str(a["artifact_format"]),
                byte_size=int(a.get("byte_size", 0)),
            )
            for a in raw.get("inputs", [])
        )

        return JobSpec(
            protocol=PROTOCOL_ID,
            job_id=str(raw["job_id"]),
            tenant_id=str(raw["tenant_id"]),
            site_id=str(raw["site_id"]),
            model_family=str(raw["model_family"]),
            inputs=inputs,
            output_directory=str(raw["output_directory"]),
            seed=int(raw["seed"]),
            code_identity=str(raw["code_identity"]),
            resources=ResourceBudget(
                max_wall_clock_seconds=float(res["max_wall_clock_seconds"]),
                max_memory_mb=int(res.get("max_memory_mb", 0)),
                gpu_required=bool(res.get("gpu_required", False)),
            ),
            semantic_manifest_id=(
                str(raw["semantic_manifest_id"]) if raw.get("semantic_manifest_id") else None
            ),
            checkpoint_directory=(
                str(raw["checkpoint_directory"]) if raw.get("checkpoint_directory") else None
            ),
            cancellation_file=(
                str(raw["cancellation_file"]) if raw.get("cancellation_file") else None
            ),
            parameters=dict(raw.get("parameters", {})),
        )
