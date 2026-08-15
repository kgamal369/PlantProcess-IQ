"""B-05 evidence hooks and result shape.

T-172 owns the HOOK and the RESULT SHAPE only. The common benchmark framework is
T-182's and the promotion decision is T-176's.

WHAT THIS PRODUCES. The three things a promotion decision needs from an encoder: the
embeddings a downstream model would consume as its metric-lift input, what encoding
costs at p95, and how large the artifact is.

WHAT IT DOES NOT PRODUCE. Any statement that an encoder is deployable, that it beats
engineered features, or that deep learning is worth its cost here. There is no field
on the record where such a statement could be written, which is deliberate.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Any, Sequence

from .contract import EmbeddingResult, EncoderManifest

B05_RESULT_SCHEMA_VERSION = 1


@dataclass(frozen=True)
class B05Measurement:
    """One encoder measured once. Machine readable, no verdict attached."""

    benchmark_id: str
    result_schema_version: int
    encoder_kind: str
    artifact_identity: str
    channel_set_version: str
    training_input_identity: str
    embedding_dimension: int
    training_windows: int
    training_seconds: float
    final_loss: float
    encoded_windows: int
    p50_encode_ms: float
    p95_encode_ms: float
    p99_encode_ms: float
    artifact_bytes: int
    framework: str
    framework_version: str
    seed: int
    numerical_tolerance: float
    #: The embeddings a downstream model would consume. This is the metric-lift
    #: INPUT; the lift itself is measured by whatever consumes it, not here.
    metric_lift_input: tuple[tuple[float, ...], ...] = field(default_factory=tuple)
    notes: tuple[str, ...] = field(default_factory=tuple)

    def to_dict(self, include_embeddings: bool = False) -> dict[str, Any]:
        record = asdict(self)
        if not include_embeddings:
            record["metric_lift_input"] = []
            record["metric_lift_input_windows"] = len(self.metric_lift_input)
        return record


def measure_encoder(
    manifest: EncoderManifest,
    embeddings: EmbeddingResult,
    notes: Sequence[str] = (),
) -> B05Measurement:
    """Assemble the B-05 inputs from one trained encoder and one encode pass."""
    return B05Measurement(
        benchmark_id="B-05",
        result_schema_version=B05_RESULT_SCHEMA_VERSION,
        encoder_kind=manifest.encoder_kind,
        artifact_identity=manifest.artifact_identity,
        channel_set_version=manifest.channel_set_version,
        training_input_identity=manifest.training_input_identity,
        embedding_dimension=manifest.embedding_dimension,
        training_windows=manifest.training_windows,
        training_seconds=manifest.training_seconds,
        final_loss=manifest.final_loss,
        encoded_windows=embeddings.telemetry.windows,
        p50_encode_ms=embeddings.telemetry.p50_ms,
        p95_encode_ms=embeddings.telemetry.p95_ms,
        p99_encode_ms=embeddings.telemetry.p99_ms,
        artifact_bytes=manifest.artifact_bytes,
        framework=manifest.framework,
        framework_version=manifest.framework_version,
        seed=manifest.seed,
        numerical_tolerance=manifest.numerical_tolerance,
        metric_lift_input=embeddings.embeddings,
        notes=tuple(notes),
    )
