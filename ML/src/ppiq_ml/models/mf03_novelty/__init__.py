"""MF-03 novelty family.

A mandatory simple baseline that always runs, and a density candidate that runs only
where the reference population can support a novelty claim at all.

Every answer keeps four things apart: the score, the threshold identity, the
population context and the refusal state. A population too small, too uniform or too
repetitive produces a refusal naming the measurement that failed, never a number.
"""

from .contract import (
    NOT_REFUSED,
    FeatureExclusion,
    ModelClass,
    NoveltyContractError,
    NoveltyModel,
    NoveltyRefusalCode,
    NoveltyResult,
    PopulationContext,
    RefusalState,
    ScoredUnit,
    ThresholdIdentity,
    population_identity,
    rank_units,
    score_identity,
)
from .eligibility import (
    CONSTANT_FEATURE_SPREAD,
    MIN_DISTINCT_REFERENCE_UNITS,
    MIN_REFERENCE_UNITS,
    MIN_USABLE_FEATURES,
    EligibilityOutcome,
    evaluate_eligibility,
    summarise,
    validate_population,
)
from .threshold import DEFAULT_QUANTILE, THRESHOLD_METHOD, reference_quantile_threshold
from .baseline import (
    BASELINE_MODEL_CODE,
    RobustDeviationBaseline,
    median,
    median_absolute_deviation,
)
from .candidate import (
    CANDIDATE_MODEL_CODE,
    DEFAULT_NEIGHBOURS,
    NeighbourDensityCandidate,
)
from .runtime import (
    EVALUATION_ARTIFACT_NAME,
    EVIDENCE_RECORD_VERSION,
    MF03_MODEL_FAMILY,
    build_job_parameters,
    run_mf03,
)

__all__ = [
    "NOT_REFUSED", "FeatureExclusion", "ModelClass", "NoveltyContractError",
    "NoveltyModel", "NoveltyRefusalCode", "NoveltyResult", "PopulationContext",
    "RefusalState", "ScoredUnit", "ThresholdIdentity", "population_identity",
    "rank_units", "score_identity",
    "CONSTANT_FEATURE_SPREAD", "MIN_DISTINCT_REFERENCE_UNITS", "MIN_REFERENCE_UNITS",
    "MIN_USABLE_FEATURES", "EligibilityOutcome", "evaluate_eligibility", "summarise",
    "validate_population",
    "DEFAULT_QUANTILE", "THRESHOLD_METHOD", "reference_quantile_threshold",
    "BASELINE_MODEL_CODE", "RobustDeviationBaseline", "median",
    "median_absolute_deviation",
    "CANDIDATE_MODEL_CODE", "DEFAULT_NEIGHBOURS", "NeighbourDensityCandidate",
    "EVALUATION_ARTIFACT_NAME", "EVIDENCE_RECORD_VERSION", "MF03_MODEL_FAMILY",
    "build_job_parameters", "run_mf03",
]
