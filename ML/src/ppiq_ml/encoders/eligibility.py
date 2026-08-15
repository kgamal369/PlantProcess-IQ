"""When an encoder may not be trained, and when it may not be used.

MF-01 is optional. That is a product statement, and it has a consequence here: the
honest answer to a population that cannot support an encoder is a refusal naming what
failed, not a trained object nobody should trust.

The thresholds below are declared constants, not measured ones. They are the smallest
shapes under which training is arithmetic rather than learning. Values that a
benchmark should decide are left for a benchmark to decide, and none of them is a
production population threshold.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Sequence

from .contract import (
    ChannelSet,
    ChannelSetIncompatibleError,
    EncoderEligibilityError,
    EncoderManifest,
)

#: Declared, not measured. See the module docstring.
MIN_CHANNELS = 2
MIN_WINDOW_STEPS = 8
MIN_TRAINING_WINDOWS = 16


class EncoderRefusalCode(str, Enum):
    NONE = "none"
    INSUFFICIENT_CHANNELS = "insufficient_channels"
    INVALID_SEQUENCE_SHAPE = "invalid_sequence_shape"
    INSUFFICIENT_TRAINING_WINDOWS = "insufficient_training_windows"
    INCOMPATIBLE_CHANNEL_SET_VERSION = "incompatible_channel_set_version"
    INVALID_ENCODER_ARTIFACT = "invalid_encoder_artifact"


@dataclass(frozen=True)
class EligibilityVerdict:
    eligible: bool
    code: EncoderRefusalCode
    reason: str
    required: float | None = None
    observed: float | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "eligible": self.eligible,
            "code": self.code.value,
            "reason": self.reason,
            "required": self.required,
            "observed": self.observed,
        }


ELIGIBLE = EligibilityVerdict(True, EncoderRefusalCode.NONE, "")


def evaluate_training_eligibility(
    windows: Sequence[Sequence[Sequence[float]]], channel_set: ChannelSet
) -> EligibilityVerdict:
    """Whether these windows can support fitting an encoder at all."""
    if channel_set.channel_count < MIN_CHANNELS:
        return EligibilityVerdict(
            False,
            EncoderRefusalCode.INSUFFICIENT_CHANNELS,
            (
                f"The channel set declares {channel_set.channel_count} channel(s) against "
                f"a declared minimum of {MIN_CHANNELS}. An encoder over a single channel "
                "learns nothing a simple summary of that channel does not already carry."
            ),
            float(MIN_CHANNELS),
            float(channel_set.channel_count),
        )

    if not windows:
        return EligibilityVerdict(
            False,
            EncoderRefusalCode.INSUFFICIENT_TRAINING_WINDOWS,
            "There are no training windows.",
            float(MIN_TRAINING_WINDOWS),
            0.0,
        )

    steps = len(windows[0][0]) if windows[0] else 0
    if steps < MIN_WINDOW_STEPS:
        return EligibilityVerdict(
            False,
            EncoderRefusalCode.INVALID_SEQUENCE_SHAPE,
            (
                f"A window carries {steps} step(s) against a declared minimum of "
                f"{MIN_WINDOW_STEPS}. There is no temporal shape in a window that short "
                "for a temporal model to find."
            ),
            float(MIN_WINDOW_STEPS),
            float(steps),
        )

    if len(windows) < MIN_TRAINING_WINDOWS:
        return EligibilityVerdict(
            False,
            EncoderRefusalCode.INSUFFICIENT_TRAINING_WINDOWS,
            (
                f"There are {len(windows)} training window(s) against a declared minimum "
                f"of {MIN_TRAINING_WINDOWS}. Fitting on fewer would memorise them."
            ),
            float(MIN_TRAINING_WINDOWS),
            float(len(windows)),
        )

    return ELIGIBLE


def require_compatible_channel_set(
    manifest: EncoderManifest, channel_set: ChannelSet
) -> None:
    """Refuse an encoder handed a channel set it was not fitted for.

    The version is checked first and on its own. Two channel sets can carry identical
    names under different versions and mean different things by them, and an encoder
    that accepted that would return well-formed embeddings of the wrong thing.
    """
    if manifest.channel_set_version != channel_set.version:
        raise ChannelSetIncompatibleError(
            f"Encoder '{manifest.artifact_identity[:12]}' was fitted for channel set "
            f"version '{manifest.channel_set_version}' and was handed version "
            f"'{channel_set.version}'. The embeddings would be well formed and would "
            "describe a different set of channels, so the encoder is refused as "
            "incompatible rather than used."
        )
    if manifest.channel_set_identity != channel_set.identity():
        raise ChannelSetIncompatibleError(
            f"Encoder '{manifest.artifact_identity[:12]}' declares channel set version "
            f"'{manifest.channel_set_version}' but a different set of channel names or "
            "a different order under that same version. The version was reused for a "
            "changed channel set, which is the one case a version cannot detect on its "
            "own."
        )


def refuse_training(verdict: EligibilityVerdict) -> None:
    if not verdict.eligible:
        raise EncoderEligibilityError(verdict.reason)
