"""The ProcessEncoder contract.

WHAT AN ENCODER IS HERE. Something that turns a window of multi-channel process
measurements into a fixed-length embedding. It is optional. Nothing in this package
decides that an encoder should be served, and training successfully is not evidence
that it should be.

THE THREE IDENTITIES, AND WHY THEY ARE SEPARATE.

    channel_set_version     which channels, in which order, this encoder expects
    training_input_identity which sealed data it was fitted on
    artifact_identity       what this encoder IS, derived from the two above plus
                            architecture, seed and framework

An encoder asked to embed a different channel set is not slightly wrong, it is
answering a different question with the same-shaped output, and that is the failure
this contract exists to make impossible.

WHAT REPRODUCIBILITY MEANS HERE, STATED PLAINLY. Two training runs on identical
input produce the same logical training input identity and embeddings that agree
within a declared numerical tolerance. They are NOT promised to produce byte-identical
serialized artifacts. A neural framework does not guarantee that across processes, and
a contract that claimed it would be making a promise the framework cannot keep.
"""

from __future__ import annotations

import hashlib
import json
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

#: Embeddings are compared within this tolerance, not for equality. Float32 arithmetic
#: on a different thread count or a different build can differ in the last bits, and a
#: test demanding exact equality would be testing the build rather than the encoder.
EMBEDDING_TOLERANCE = 1e-5


class EncoderContractError(Exception):
    """The request cannot be interpreted under this contract."""


class ChannelSetIncompatibleError(EncoderContractError):
    """The encoder was fitted for a different channel set than it was handed."""


class EncoderArtifactInvalidError(EncoderContractError):
    """The artifact is present but is not a readable encoder of this kind."""


class EncoderEligibilityError(EncoderContractError):
    """The population cannot support training or encoding, for a stated reason."""


@dataclass(frozen=True)
class ChannelSet:
    """Which channels an encoder consumes, in which order, under which version.

    The version is declared by the caller rather than derived from the names. Two
    deployments can carry the same channel names and mean different things by them,
    and a derived version would hide that.
    """

    version: str
    names: tuple[str, ...]

    def __post_init__(self) -> None:
        if not self.version.strip():
            raise EncoderContractError("A channel set must declare a version.")
        if not self.names:
            raise EncoderContractError("A channel set must name at least one channel.")
        if len(set(self.names)) != len(self.names):
            raise EncoderContractError("Channel names must be unique within a channel set.")

    @property
    def channel_count(self) -> int:
        return len(self.names)

    def identity(self) -> str:
        return hashlib.sha256(
            json.dumps(
                {"version": self.version, "names": list(self.names)}, sort_keys=True
            ).encode("ascii")
        ).hexdigest()

    def to_dict(self) -> dict[str, Any]:
        return {"version": self.version, "names": list(self.names)}


@dataclass(frozen=True)
class TrainingConfig:
    """Every value that changes what the fitted encoder is.

    All of it enters the artifact identity. A hyperparameter that could be changed
    without changing the identity would let two different encoders claim to be the
    same one.
    """

    embedding_dimension: int = 8
    hidden_channels: int = 16
    kernel_size: int = 5
    epochs: int = 4
    batch_windows: int = 16
    learning_rate: float = 0.001
    reconstruction_points: int = 8
    seed: int = 20260815

    def __post_init__(self) -> None:
        for name, value in (
            ("embedding_dimension", self.embedding_dimension),
            ("hidden_channels", self.hidden_channels),
            ("kernel_size", self.kernel_size),
            ("epochs", self.epochs),
            ("batch_windows", self.batch_windows),
            ("reconstruction_points", self.reconstruction_points),
        ):
            if value < 1:
                raise EncoderContractError(f"{name} must be at least 1; {value} was declared.")
        if self.learning_rate <= 0.0:
            raise EncoderContractError("The learning rate must be positive.")

    def to_dict(self) -> dict[str, Any]:
        return {
            "embedding_dimension": self.embedding_dimension,
            "hidden_channels": self.hidden_channels,
            "kernel_size": self.kernel_size,
            "epochs": self.epochs,
            "batch_windows": self.batch_windows,
            "learning_rate": self.learning_rate,
            "reconstruction_points": self.reconstruction_points,
            "seed": self.seed,
        }


@dataclass(frozen=True)
class EncoderManifest:
    """What this encoder is, what it was fitted on, and what it cost to fit."""

    encoder_kind: str
    encoder_version: str
    artifact_identity: str
    channel_set_version: str
    channel_set_identity: str
    channel_names: tuple[str, ...]
    window_steps: int
    embedding_dimension: int
    seed: int
    framework: str
    framework_version: str
    training_input_identity: str
    training_windows: int
    training_seconds: float
    final_loss: float
    artifact_bytes: int
    artifact_byte_hash: str
    numerical_tolerance: float
    config: Mapping[str, Any]

    def to_dict(self) -> dict[str, Any]:
        return {
            "encoder_kind": self.encoder_kind,
            "encoder_version": self.encoder_version,
            "artifact_identity": self.artifact_identity,
            "channel_set_version": self.channel_set_version,
            "channel_set_identity": self.channel_set_identity,
            "channel_names": list(self.channel_names),
            "window_steps": self.window_steps,
            "embedding_dimension": self.embedding_dimension,
            "seed": self.seed,
            "framework": self.framework,
            "framework_version": self.framework_version,
            "training_input_identity": self.training_input_identity,
            "training_windows": self.training_windows,
            "training_seconds": self.training_seconds,
            "final_loss": self.final_loss,
            "artifact_bytes": self.artifact_bytes,
            "artifact_byte_hash": self.artifact_byte_hash,
            "numerical_tolerance": self.numerical_tolerance,
            "config": dict(self.config),
        }


def artifact_identity(
    encoder_kind: str,
    channel_set: ChannelSet,
    window_steps: int,
    config: TrainingConfig,
    framework: str,
    training_input_identity: str,
) -> str:
    """What this encoder IS, independent of how the framework serialised it.

    Deliberately excludes the framework's version and the serialized bytes. Two runs
    of the same architecture on the same data under the same seed are the same
    encoder, and a framework patch release must not make them look like two.
    """
    return hashlib.sha256(
        json.dumps(
            {
                "encoder_kind": encoder_kind,
                "channel_set": channel_set.to_dict(),
                "window_steps": window_steps,
                "config": config.to_dict(),
                "framework": framework,
                "training_input_identity": training_input_identity,
            },
            sort_keys=True,
        ).encode("ascii")
    ).hexdigest()


@dataclass(frozen=True)
class EncodeTelemetry:
    """What encoding costs. Reported, never judged."""

    windows: int
    p50_ms: float
    p95_ms: float
    p99_ms: float
    total_seconds: float

    def to_dict(self) -> dict[str, Any]:
        return {
            "windows": self.windows,
            "p50_ms": self.p50_ms,
            "p95_ms": self.p95_ms,
            "p99_ms": self.p99_ms,
            "total_seconds": self.total_seconds,
        }


@dataclass(frozen=True)
class EmbeddingResult:
    """Fixed-dimension embeddings, and the identities that make them citable."""

    artifact_identity: str
    channel_set_version: str
    input_identity: str
    embedding_dimension: int
    embeddings: tuple[tuple[float, ...], ...]
    telemetry: EncodeTelemetry

    def to_dict(self) -> dict[str, Any]:
        return {
            "artifact_identity": self.artifact_identity,
            "channel_set_version": self.channel_set_version,
            "input_identity": self.input_identity,
            "embedding_dimension": self.embedding_dimension,
            "window_count": len(self.embeddings),
            "telemetry": self.telemetry.to_dict(),
        }


class ProcessEncoder(ABC):
    """One encoder family behind a replaceable runtime."""

    @property
    @abstractmethod
    def encoder_kind(self) -> str:
        ...

    @property
    @abstractmethod
    def manifest(self) -> EncoderManifest:
        """The fitted encoder's description. Raises before training or loading."""

    @abstractmethod
    def train(
        self,
        windows: Sequence[Sequence[Sequence[float]]],
        channel_set: ChannelSet,
        training_input_identity: str,
        config: TrainingConfig,
    ) -> EncoderManifest:
        ...

    @abstractmethod
    def encode(
        self,
        windows: Sequence[Sequence[Sequence[float]]],
        channel_set: ChannelSet,
        input_identity: str,
    ) -> EmbeddingResult:
        ...

    @abstractmethod
    def save(self, path: str) -> EncoderManifest:
        ...


def percentile(sorted_values: Sequence[float], fraction: float) -> float:
    """Nearest-rank percentile over already sorted measurements."""
    import math

    if not sorted_values:
        raise EncoderContractError("A percentile over no measurements is undefined.")
    position = max(1, math.ceil(fraction * len(sorted_values))) - 1
    return sorted_values[min(position, len(sorted_values) - 1)]
