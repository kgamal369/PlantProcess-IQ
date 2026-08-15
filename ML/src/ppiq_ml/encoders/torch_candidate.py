"""The PyTorch process-encoder candidate.

THE SMALLEST TRUTHFUL ARCHITECTURE. Two one-dimensional convolutions over the step
axis, a pooled summary, and a linear projection to a fixed embedding. It is trained to
reconstruct a downsampled profile of the window it was given, so the objective needs
no labels and the embedding has to carry the shape of the window to satisfy it.

It is deliberately small. A temporal convolution is enough to prove the contract, the
artifact, the channel-set rule and the cost telemetry, and choosing between
architectures is a benchmark's job under B-05 rather than this task's.

WHAT DETERMINISM MEANS FOR THIS CANDIDATE, PRECISELY. The seed, the thread count and
the deterministic algorithm setting are all fixed, so two runs on identical input
produce the same artifact identity and embeddings agreeing within the declared
tolerance. They are not promised to produce byte-identical files. The framework does
not guarantee that across processes and this module does not claim it.

NO PROMOTION DECISION IS MADE HERE. Training successfully is not evidence that an
encoder should be served. T-176 owns that decision and B-05 supplies its inputs.
"""

from __future__ import annotations

import hashlib
import json
import os
import time
from typing import Any, Sequence

from .contract import (
    EMBEDDING_TOLERANCE,
    ChannelSet,
    EmbeddingResult,
    EncodeTelemetry,
    EncoderArtifactInvalidError,
    EncoderContractError,
    EncoderManifest,
    ProcessEncoder,
    TrainingConfig,
    artifact_identity,
    percentile,
)
from .eligibility import (
    evaluate_training_eligibility,
    refuse_training,
    require_compatible_channel_set,
)
from .windows import validate_windows

ENCODER_KIND = "mf01.temporal_convolution"
ENCODER_VERSION = "1"
FRAMEWORK = "torch"

#: Written into the artifact so a loader can refuse a file from a later shape.
ARTIFACT_FORMAT_VERSION = 1


def _import_framework():
    try:
        import torch  # noqa: PLC0415 - deferred so the contract imports without it
    except ImportError as missing:
        raise EncoderContractError(
            "The process encoder requires the 'torch' package, which is not installed "
            "in this environment. Install the mf01_process_encoder extra to enable it. "
            "MF-01 is optional and nothing else in this repository requires it."
        ) from missing
    return torch


def _configure_determinism(torch, seed: int) -> None:
    """Fix everything the framework lets us fix, before anything is allocated."""
    torch.manual_seed(seed)
    torch.use_deterministic_algorithms(True)
    torch.set_num_threads(1)


def _build_network(torch, channels: int, config: TrainingConfig):
    padding = config.kernel_size // 2
    encoder = torch.nn.Sequential(
        torch.nn.Conv1d(channels, config.hidden_channels, config.kernel_size, padding=padding),
        torch.nn.ReLU(),
        torch.nn.Conv1d(
            config.hidden_channels, config.hidden_channels, config.kernel_size, padding=padding
        ),
        torch.nn.ReLU(),
        torch.nn.AdaptiveAvgPool1d(1),
        torch.nn.Flatten(),
        torch.nn.Linear(config.hidden_channels, config.embedding_dimension),
    )
    decoder = torch.nn.Linear(
        config.embedding_dimension, channels * config.reconstruction_points
    )
    return encoder, decoder


def _as_tensor(torch, windows: Sequence[Sequence[Sequence[float]]]):
    return torch.tensor(
        [[list(map(float, channel)) for channel in window] for window in windows],
        dtype=torch.float32,
    )


def _standardise(torch, batch):
    """Centre and scale each channel within each window.

    Process channels arrive on wildly different scales, and without this the objective
    would be dominated by whichever channel happens to carry the largest numbers rather
    than by the shape of any of them.
    """
    mean = batch.mean(dim=2, keepdim=True)
    spread = batch.std(dim=2, keepdim=True).clamp(min=1e-6)
    return (batch - mean) / spread


class TemporalConvolutionEncoder(ProcessEncoder):
    """A bounded one-dimensional convolutional encoder over process windows."""

    def __init__(self) -> None:
        self._manifest: EncoderManifest | None = None
        self._encoder = None
        self._decoder = None
        self._config: TrainingConfig | None = None

    @property
    def encoder_kind(self) -> str:
        return ENCODER_KIND

    @property
    def manifest(self) -> EncoderManifest:
        if self._manifest is None:
            raise EncoderContractError(
                "This encoder has not been trained or loaded, so there is nothing to "
                "describe and nothing to encode with."
            )
        return self._manifest

    # ------------------------------------------------------------------- train

    def train(
        self,
        windows: Sequence[Sequence[Sequence[float]]],
        channel_set: ChannelSet,
        training_input_identity: str,
        config: TrainingConfig | None = None,
    ) -> EncoderManifest:
        if self._manifest is not None:
            raise EncoderContractError(
                "This encoder is already fitted. Training again would change an encoder "
                "that an embedding may already cite."
            )
        config = config or TrainingConfig()
        refuse_training(evaluate_training_eligibility(windows, channel_set))
        window_steps = validate_windows(windows, channel_set)

        torch = _import_framework()
        _configure_determinism(torch, config.seed)

        encoder, decoder = _build_network(torch, channel_set.channel_count, config)
        parameters = list(encoder.parameters()) + list(decoder.parameters())
        optimiser = torch.optim.Adam(parameters, lr=config.learning_rate)
        loss_function = torch.nn.MSELoss()

        data = _standardise(torch, _as_tensor(torch, windows))
        target = torch.nn.functional.adaptive_avg_pool1d(
            data, config.reconstruction_points
        ).flatten(1)

        started = time.perf_counter()
        final_loss = 0.0
        for _ in range(config.epochs):
            for start in range(0, data.shape[0], config.batch_windows):
                batch = data[start : start + config.batch_windows]
                expected = target[start : start + config.batch_windows]
                optimiser.zero_grad()
                loss = loss_function(decoder(encoder(batch)), expected)
                loss.backward()
                optimiser.step()
                final_loss = float(loss.item())
        training_seconds = time.perf_counter() - started

        encoder.eval()
        decoder.eval()
        self._encoder, self._decoder, self._config = encoder, decoder, config

        self._manifest = EncoderManifest(
            encoder_kind=ENCODER_KIND,
            encoder_version=ENCODER_VERSION,
            artifact_identity=artifact_identity(
                ENCODER_KIND, channel_set, window_steps, config, FRAMEWORK,
                training_input_identity,
            ),
            channel_set_version=channel_set.version,
            channel_set_identity=channel_set.identity(),
            channel_names=channel_set.names,
            window_steps=window_steps,
            embedding_dimension=config.embedding_dimension,
            seed=config.seed,
            framework=FRAMEWORK,
            framework_version=str(torch.__version__),
            training_input_identity=training_input_identity,
            training_windows=len(windows),
            training_seconds=training_seconds,
            final_loss=final_loss,
            artifact_bytes=0,
            artifact_byte_hash="",
            numerical_tolerance=EMBEDDING_TOLERANCE,
            config=config.to_dict(),
        )
        return self._manifest

    # ------------------------------------------------------------------ encode

    def encode(
        self,
        windows: Sequence[Sequence[Sequence[float]]],
        channel_set: ChannelSet,
        input_identity: str,
    ) -> EmbeddingResult:
        manifest = self.manifest
        require_compatible_channel_set(manifest, channel_set)
        steps = validate_windows(windows, channel_set)
        if steps != manifest.window_steps:
            raise EncoderContractError(
                f"This encoder was fitted on windows of {manifest.window_steps} step(s) "
                f"and was handed windows of {steps}."
            )

        torch = _import_framework()
        _configure_determinism(torch, manifest.seed)

        durations: list[float] = []
        embeddings: list[tuple[float, ...]] = []
        started = time.perf_counter()
        with torch.no_grad():
            for window in windows:
                begin = time.perf_counter()
                batch = _standardise(torch, _as_tensor(torch, [window]))
                vector = self._encoder(batch)[0]
                durations.append((time.perf_counter() - begin) * 1000.0)
                embeddings.append(tuple(float(v) for v in vector))
        total = time.perf_counter() - started

        ordered = sorted(durations)
        return EmbeddingResult(
            artifact_identity=manifest.artifact_identity,
            channel_set_version=manifest.channel_set_version,
            input_identity=input_identity,
            embedding_dimension=manifest.embedding_dimension,
            embeddings=tuple(embeddings),
            telemetry=EncodeTelemetry(
                windows=len(windows),
                p50_ms=percentile(ordered, 0.50),
                p95_ms=percentile(ordered, 0.95),
                p99_ms=percentile(ordered, 0.99),
                total_seconds=total,
            ),
        )

    # ---------------------------------------------------------------- artifact

    def save(self, path: str) -> EncoderManifest:
        manifest = self.manifest
        torch = _import_framework()
        directory = os.path.dirname(os.path.abspath(path))
        if directory:
            os.makedirs(directory, exist_ok=True)

        torch.save(
            {
                "artifact_format_version": ARTIFACT_FORMAT_VERSION,
                "manifest": manifest.to_dict(),
                "encoder_state": self._encoder.state_dict(),
                "decoder_state": self._decoder.state_dict(),
            },
            path,
        )

        size = os.path.getsize(path)
        digest = hashlib.sha256()
        with open(path, "rb") as handle:
            for piece in iter(lambda: handle.read(128 * 1024), b""):
                digest.update(piece)

        # The byte hash and size are observations of one serialisation, recorded
        # rather than promised. The artifact identity above is what identifies the
        # encoder, and it does not move when the framework's writer does.
        self._manifest = EncoderManifest(
            **{
                **{
                    k: v for k, v in vars(manifest).items()
                    if k not in ("artifact_bytes", "artifact_byte_hash")
                },
                "artifact_bytes": size,
                "artifact_byte_hash": digest.hexdigest(),
            }
        )
        return self._manifest

    @classmethod
    def load(cls, path: str) -> "TemporalConvolutionEncoder":
        torch = _import_framework()
        if not os.path.exists(path):
            raise EncoderArtifactInvalidError(
                f"No encoder artifact exists at '{path}'."
            )
        try:
            payload = torch.load(path, weights_only=False)
        except Exception as broken:  # noqa: BLE001 - any failure here is one refusal
            raise EncoderArtifactInvalidError(
                f"The encoder artifact could not be read: {type(broken).__name__}. The "
                "file is present and is not a readable encoder of this kind."
            ) from broken

        if not isinstance(payload, dict) or "manifest" not in payload:
            raise EncoderArtifactInvalidError(
                "The artifact does not carry an encoder manifest."
            )
        if payload.get("artifact_format_version") != ARTIFACT_FORMAT_VERSION:
            raise EncoderArtifactInvalidError(
                f"The artifact declares format version "
                f"{payload.get('artifact_format_version')}; this build reads "
                f"{ARTIFACT_FORMAT_VERSION}."
            )

        raw = payload["manifest"]
        config = TrainingConfig(**raw["config"])
        instance = cls()
        _configure_determinism(torch, int(raw["seed"]))
        encoder, decoder = _build_network(torch, len(raw["channel_names"]), config)
        try:
            encoder.load_state_dict(payload["encoder_state"])
            decoder.load_state_dict(payload["decoder_state"])
        except Exception as broken:  # noqa: BLE001
            raise EncoderArtifactInvalidError(
                f"The artifact's weights do not fit the architecture it declares: "
                f"{type(broken).__name__}."
            ) from broken
        encoder.eval()
        decoder.eval()

        instance._encoder, instance._decoder, instance._config = encoder, decoder, config
        instance._manifest = EncoderManifest(
            encoder_kind=str(raw["encoder_kind"]),
            encoder_version=str(raw["encoder_version"]),
            artifact_identity=str(raw["artifact_identity"]),
            channel_set_version=str(raw["channel_set_version"]),
            channel_set_identity=str(raw["channel_set_identity"]),
            channel_names=tuple(raw["channel_names"]),
            window_steps=int(raw["window_steps"]),
            embedding_dimension=int(raw["embedding_dimension"]),
            seed=int(raw["seed"]),
            framework=str(raw["framework"]),
            framework_version=str(raw["framework_version"]),
            training_input_identity=str(raw["training_input_identity"]),
            training_windows=int(raw["training_windows"]),
            training_seconds=float(raw["training_seconds"]),
            final_loss=float(raw["final_loss"]),
            artifact_bytes=int(raw["artifact_bytes"]),
            artifact_byte_hash=str(raw["artifact_byte_hash"]),
            numerical_tolerance=float(raw["numerical_tolerance"]),
            config=dict(raw["config"]),
        )
        return instance


def framework_environment() -> dict[str, Any]:
    """What produced these numbers, recorded rather than assumed."""
    torch = _import_framework()
    import sys

    return {
        "framework": FRAMEWORK,
        "framework_version": str(torch.__version__),
        "python_version": sys.version.split()[0],
        "threads": int(torch.get_num_threads()),
        "accelerator_available": bool(torch.cuda.is_available()),
    }


def environment_identity() -> str:
    return hashlib.sha256(
        json.dumps(framework_environment(), sort_keys=True).encode("ascii")
    ).hexdigest()
