"""Deterministic promotion documents for the T-176 tests.

Every value here is synthetic and generic. The fixture builds a document that passes
cleanly, and each test bends exactly one number so that the check it is aiming at is
the only thing that changed.
"""

from __future__ import annotations

from ppiq_ml.governance import (
    EXPLANATION_METHOD_INITIAL_CANDIDATE,
    CandidateClass,
    CandidateEvidence,
    DeclaredThresholds,
    ExplanationEvidence,
    QualityEvidence,
    ServingEvidence,
    TrainingEvidence,
    build_document,
)

SNAPSHOT = "snapshot-fixture-0001"
HOLDOUT = "holdout-fixture-0001"

FEATURE_NAMES = ("feature_a", "feature_b", "feature_c", "feature_d", "feature_e")

STABLE_ATTRIBUTIONS = (
    (0.50, 0.30, 0.12, 0.05, 0.01),
    (0.49, 0.31, 0.13, 0.04, 0.02),
    (0.51, 0.29, 0.11, 0.06, 0.01),
)

#: The same five features, ordered differently in every run. A model like this can
#: score well and will tell a person a different cause each time they ask.
UNSTABLE_ATTRIBUTIONS = (
    (0.50, 0.30, 0.12, 0.05, 0.01),
    (0.01, 0.05, 0.50, 0.30, 0.12),
    (0.12, 0.50, 0.01, 0.11, 0.31),
)


def thresholds(**overrides) -> DeclaredThresholds:
    declared = dict(
        min_primary_metric=0.70,
        max_calibration_error=0.05,
        max_out_of_time_drop=0.06,
        max_subgroup_spread=0.10,
        max_missingness_drop=0.08,
        min_explanation_rank_agreement=0.80,
        min_explanation_top_k_overlap=0.60,
        explanation_top_k=3,
        max_p50_latency_ms=40.0,
        max_p95_latency_ms=90.0,
        max_p99_latency_ms=150.0,
        min_throughput_per_second=200.0,
        max_artifact_size_bytes=8_000_000,
        max_resident_memory_mb=512.0,
        max_warm_up_seconds=10.0,
        max_accelerator_memory_mb=None,
        max_training_seconds=1800.0,
        max_peak_memory_mb=4096.0,
        min_snapshot_rows_per_second=5000.0,
        declared_min_lift=0.03,
        declared_latency_budget_ms=25.0,
        declared_size_class_bytes=8_000_000,
    )
    declared.update(overrides)
    return DeclaredThresholds(**declared)


def quality(
    primary=0.82,
    proper_score=0.180,
    calibration_error=0.020,
    out_of_time=None,
    subgroups=None,
    missingness=None,
    attributions=STABLE_ATTRIBUTIONS,
    higher_is_better=True,
    primary_metric_name="auc",
) -> QualityEvidence:
    # Both robustness figures default to a fixed distance from the primary metric.
    # Holding them at an absolute value while the primary moves would silently turn
    # a test about latency into a test about an out-of-time shortfall, which is
    # exactly the confusion this fixture exists to prevent.
    if out_of_time is None:
        out_of_time = primary - 0.03 if higher_is_better else primary + 0.03
    if missingness is None:
        missingness = primary - 0.04 if higher_is_better else primary + 0.04
    explanation = None
    if attributions is not None:
        explanation = ExplanationEvidence(
            method=EXPLANATION_METHOD_INITIAL_CANDIDATE,
            feature_names=FEATURE_NAMES,
            attributions=tuple(attributions),
        )
    return QualityEvidence(
        primary_metric_name=primary_metric_name,
        primary_metric=primary,
        primary_higher_is_better=higher_is_better,
        proper_score_name="log_loss",
        proper_score=proper_score,
        calibration_error=calibration_error,
        out_of_time_primary_metric=out_of_time,
        subgroup_primary_metrics=(
            subgroups if subgroups is not None else {"regime_one": 0.83, "regime_two": 0.79}
        ),
        missingness_primary_metric=missingness,
        explanation=explanation,
    )


def serving(
    p50=20.0,
    p95=55.0,
    p99=95.0,
    throughput=450.0,
    artifact_size=1_200_000,
    resident=180.0,
    warm_up=3.0,
    accelerator=None,
) -> ServingEvidence:
    return ServingEvidence(
        p50_latency_ms=p50,
        p95_latency_ms=p95,
        p99_latency_ms=p99,
        throughput_per_second=throughput,
        artifact_size_bytes=artifact_size,
        resident_memory_mb=resident,
        warm_up_seconds=warm_up,
        accelerator_memory_mb=accelerator,
    )


def training(duration=420.0, peak=1500.0, rows_per_second=22000.0) -> TrainingEvidence:
    return TrainingEvidence(
        training_seconds=duration,
        peak_memory_mb=peak,
        snapshot_rows_per_second=rows_per_second,
    )


def candidate(
    code="candidate_under_test",
    candidate_class=CandidateClass.ENGINEERED_FEATURES,
    quality_evidence=None,
    serving_evidence=None,
    training_evidence=None,
    snapshot=SNAPSHOT,
    holdout=HOLDOUT,
) -> CandidateEvidence:
    return CandidateEvidence(
        candidate_code=code,
        candidate_class=candidate_class,
        quality=quality_evidence or quality(),
        serving=serving_evidence or serving(),
        training=training_evidence or training(),
        snapshot_identity=snapshot,
        holdout_identity=holdout,
    )


def incumbent(
    code="engineered_feature_path",
    primary=0.78,
    proper_score=0.200,
    p95=50.0,
    **quality_overrides,
) -> CandidateEvidence:
    return candidate(
        code=code,
        candidate_class=CandidateClass.ENGINEERED_FEATURES,
        quality_evidence=quality(primary=primary, proper_score=proper_score, **quality_overrides),
        serving_evidence=serving(p95=p95),
    )


def document(challenger=None, held=None, declared=None):
    return build_document(
        incumbent=held or incumbent(),
        challenger=challenger or candidate(),
        thresholds=declared or thresholds(),
    )
