"""MF-04 supervised outcome family.

A mandatory simple baseline and a gradient boosted tabular candidate, both fitted on
one sealed artifact, both scored on one shared out-of-time holdout, with the leakage
question answered before either is fitted.

Nothing in this package selects a model for service, and nothing in it reads a
database. Outcome semantics arrive as a typed contract from the caller.
"""

from .outcome import (
    FeatureDeclaration,
    OutcomeContractError,
    OutcomeDefinition,
    OutcomeKind,
    PredictionPoint,
)
from .leakage import FeatureLegality, FeatureLeakageDetail, LeakageVerdict, evaluate_leakage
from .eligibility import (
    EligibilityVerdict,
    MeasuredClause,
    Mf04RefusalCode,
    evaluate_eligibility,
)
from .metrics import MetricSet, evaluate_classification, evaluate_continuous, roc_auc
from .contract import (
    MODEL_CLASS_CANDIDATE,
    MODEL_CLASS_FLOOR,
    ComparisonRecord,
    ModelEvaluation,
    ModelUnavailableError,
    Population,
    SupervisedOutcomeModel,
    TrainedModel,
    compare,
)
from .holdout import HOLDOUT_FRACTION, Split, holdout_identity, split_out_of_time
from .baseline import BASELINE_MODEL_CODE, PriorBaseline
from .candidate import CANDIDATE_MODEL_CODE, GbdtTabularCandidate
from .population import PopulationContractError, load_population
from .runtime import (
    BASELINE_ARTIFACT_NAME,
    CANDIDATE_ARTIFACT_NAME,
    EVALUATION_ARTIFACT_NAME,
    EVALUATION_RECORD_VERSION,
    MF04_MODEL_FAMILY,
    build_job_parameters,
    run_mf04,
    supported_outcome_kinds,
)

__all__ = [
    "FeatureDeclaration", "OutcomeContractError", "OutcomeDefinition", "OutcomeKind",
    "PredictionPoint",
    "FeatureLegality", "FeatureLeakageDetail", "LeakageVerdict", "evaluate_leakage",
    "EligibilityVerdict", "MeasuredClause", "Mf04RefusalCode", "evaluate_eligibility",
    "MetricSet", "evaluate_classification", "evaluate_continuous", "roc_auc",
    "MODEL_CLASS_CANDIDATE", "MODEL_CLASS_FLOOR", "ComparisonRecord", "ModelEvaluation",
    "ModelUnavailableError", "Population", "SupervisedOutcomeModel", "TrainedModel",
    "compare",
    "HOLDOUT_FRACTION", "Split", "holdout_identity", "split_out_of_time",
    "BASELINE_MODEL_CODE", "PriorBaseline",
    "CANDIDATE_MODEL_CODE", "GbdtTabularCandidate",
    "PopulationContractError", "load_population",
    "BASELINE_ARTIFACT_NAME", "CANDIDATE_ARTIFACT_NAME", "EVALUATION_ARTIFACT_NAME",
    "EVALUATION_RECORD_VERSION", "MF04_MODEL_FAMILY", "build_job_parameters", "run_mf04",
    "supported_outcome_kinds",
]
