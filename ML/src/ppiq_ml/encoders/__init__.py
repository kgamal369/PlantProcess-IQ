"""MF-01 process encoders.

A replaceable ProcessEncoder contract with one PyTorch candidate. It consumes sealed
typed sequence artifacts and nothing else: there is no database client and no feature
store client anywhere in this package.

The encoder is optional. Training successfully is not evidence that it should be
served, no code here says otherwise, and T-176 owns the promotion decision against
the B-05 inputs this package produces.

Reproducibility here means the same artifact identity and embeddings agreeing within
a declared numerical tolerance. Byte-identical serialised artifacts across processes
are not promised, because the framework does not guarantee them.
"""

from .contract import (
    EMBEDDING_TOLERANCE,
    ChannelSet,
    ChannelSetIncompatibleError,
    EmbeddingResult,
    EncodeTelemetry,
    EncoderArtifactInvalidError,
    EncoderContractError,
    EncoderEligibilityError,
    EncoderManifest,
    ProcessEncoder,
    TrainingConfig,
    artifact_identity,
    percentile,
)
from .eligibility import (
    ELIGIBLE,
    MIN_CHANNELS,
    MIN_TRAINING_WINDOWS,
    MIN_WINDOW_STEPS,
    EligibilityVerdict,
    EncoderRefusalCode,
    evaluate_training_eligibility,
    refuse_training,
    require_compatible_channel_set,
)
from .windows import (
    collect_windows,
    iter_windows,
    training_input_identity,
    validate_windows,
)
from .torch_candidate import (
    ARTIFACT_FORMAT_VERSION,
    ENCODER_KIND,
    ENCODER_VERSION,
    FRAMEWORK,
    TemporalConvolutionEncoder,
    environment_identity,
    framework_environment,
)
from .b05 import B05_RESULT_SCHEMA_VERSION, B05Measurement, measure_encoder

__all__ = [
    "EMBEDDING_TOLERANCE", "ChannelSet", "ChannelSetIncompatibleError",
    "EmbeddingResult", "EncodeTelemetry", "EncoderArtifactInvalidError",
    "EncoderContractError", "EncoderEligibilityError", "EncoderManifest",
    "ProcessEncoder", "TrainingConfig", "artifact_identity", "percentile",
    "ELIGIBLE", "MIN_CHANNELS", "MIN_TRAINING_WINDOWS", "MIN_WINDOW_STEPS",
    "EligibilityVerdict", "EncoderRefusalCode", "evaluate_training_eligibility",
    "refuse_training", "require_compatible_channel_set",
    "collect_windows", "iter_windows", "training_input_identity", "validate_windows",
    "ARTIFACT_FORMAT_VERSION", "ENCODER_KIND", "ENCODER_VERSION", "FRAMEWORK",
    "TemporalConvolutionEncoder", "environment_identity", "framework_environment",
    "B05_RESULT_SCHEMA_VERSION", "B05Measurement", "measure_encoder",
]
