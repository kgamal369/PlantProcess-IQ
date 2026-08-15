"""Three dimensions, evaluated independently, never combined into a score.

THE RULE THIS MODULE EXISTS TO ENFORCE. A model that ranks superbly and assigns
badly scaled probabilities has not earned a place in service, and no amount of
discrimination buys back a failed calibration. The same holds across dimensions: a
model that answers brilliantly in two seconds when the budget is fifty milliseconds
has failed, and its quality numbers do not enter that conversation.

So there is no weighting here, no total, and no arithmetic between dimensions. Each
returns its own verdict with its own failed checks, and the kernel above names every
dimension that failed rather than the first.

All three dimensions are evaluated even after one has already failed, because a
report that stops at the first failure sends a person to fix one thing and discover
the next one tomorrow.
"""

from __future__ import annotations

from .checks import Dimension, Direction, DimensionVerdict, MeasuredCheck, check
from .evidence import CandidateEvidence, DeclaredThresholds, lift
from .stability import ExplanationStability, evaluate_stability


def evaluate_quality(
    challenger: CandidateEvidence,
    incumbent: CandidateEvidence,
    thresholds: DeclaredThresholds,
) -> tuple[DimensionVerdict, ExplanationStability | None]:
    """Discrimination or error, calibration, out-of-time, robustness, explanations."""
    quality = challenger.quality
    checks: list[MeasuredCheck] = []

    checks.append(
        check(
            Dimension.QUALITY,
            "primary_metric",
            Direction.AT_LEAST if quality.primary_higher_is_better else Direction.AT_MOST,
            thresholds.min_primary_metric,
            quality.primary_metric,
            f"The declared floor for {quality.primary_metric_name}.",
        )
    )

    checks.append(
        check(
            Dimension.QUALITY,
            "calibration_error",
            Direction.AT_MOST,
            thresholds.max_calibration_error,
            quality.calibration_error,
            "Probabilities a person acts on must mean what they say.",
        )
    )

    # The rule that discrimination cannot buy back probability quality. A proper
    # score that is worse than the incumbent's is refused even when the ranking is
    # better, because the two answer different questions.
    checks.append(
        check(
            Dimension.QUALITY,
            "proper_score_not_worse_than_incumbent",
            Direction.AT_MOST,
            incumbent.quality.proper_score,
            quality.proper_score,
            f"A better ranking with a worse {quality.proper_score_name} is refused.",
        )
    )

    out_of_time_drop = lift(
        quality.primary_metric,
        quality.out_of_time_primary_metric,
        quality.primary_higher_is_better,
    )
    checks.append(
        check(
            Dimension.QUALITY,
            "out_of_time_drop",
            Direction.AT_MOST,
            thresholds.max_out_of_time_drop,
            out_of_time_drop,
            "How much of the result survives on units the model has never seen.",
        )
    )

    subgroup_values = list(quality.subgroup_primary_metrics.values())
    spread = (max(subgroup_values) - min(subgroup_values)) if subgroup_values else None
    checks.append(
        check(
            Dimension.QUALITY,
            "subgroup_spread",
            Direction.AT_MOST,
            thresholds.max_subgroup_spread,
            spread,
            "A result that holds on average and fails on one regime is not one result.",
        )
    )

    missingness_drop = lift(
        quality.primary_metric,
        quality.missingness_primary_metric,
        quality.primary_higher_is_better,
    )
    checks.append(
        check(
            Dimension.QUALITY,
            "missingness_drop",
            Direction.AT_MOST,
            thresholds.max_missingness_drop,
            missingness_drop,
            "Real populations arrive with gaps; the result must survive them.",
        )
    )

    stability: ExplanationStability | None = None
    if quality.explanation is not None:
        stability = evaluate_stability(
            quality.explanation.attributions, thresholds.explanation_top_k
        )
        checks.append(
            check(
                Dimension.QUALITY,
                "explanation_rank_agreement",
                Direction.AT_LEAST,
                thresholds.min_explanation_rank_agreement,
                stability.rank_agreement,
                "Repeated explanation runs must order the features the same way.",
            )
        )
        checks.append(
            check(
                Dimension.QUALITY,
                "explanation_top_k_overlap",
                Direction.AT_LEAST,
                thresholds.min_explanation_top_k_overlap,
                stability.top_k_overlap,
                "The features a person actually reads must not change between runs.",
            )
        )
    else:
        checks.append(
            check(
                Dimension.QUALITY,
                "explanation_rank_agreement",
                Direction.AT_LEAST,
                thresholds.min_explanation_rank_agreement,
                None,
                "No explanation evidence was supplied, so stability was never measured.",
            )
        )
        checks.append(
            check(
                Dimension.QUALITY,
                "explanation_top_k_overlap",
                Direction.AT_LEAST,
                thresholds.min_explanation_top_k_overlap,
                None,
                "No explanation evidence was supplied, so overlap was never measured.",
            )
        )

    return DimensionVerdict(Dimension.QUALITY, tuple(checks)), stability


def evaluate_serving(
    challenger: CandidateEvidence, thresholds: DeclaredThresholds
) -> DimensionVerdict:
    """What it costs to answer. Quality never enters this dimension."""
    serving = challenger.serving
    checks = [
        check(Dimension.SERVING, "p50_latency_ms", Direction.AT_MOST,
              thresholds.max_p50_latency_ms, serving.p50_latency_ms,
              "The typical answer must arrive inside the budget."),
        check(Dimension.SERVING, "p95_latency_ms", Direction.AT_MOST,
              thresholds.max_p95_latency_ms, serving.p95_latency_ms,
              "The slow answers are the ones a person remembers."),
        check(Dimension.SERVING, "p99_latency_ms", Direction.AT_MOST,
              thresholds.max_p99_latency_ms, serving.p99_latency_ms,
              "The tail is where a timeout lives."),
        check(Dimension.SERVING, "throughput_per_second", Direction.AT_LEAST,
              thresholds.min_throughput_per_second, serving.throughput_per_second,
              "One answer at a time is not a service."),
        check(Dimension.SERVING, "artifact_size_bytes", Direction.AT_MOST,
              float(thresholds.max_artifact_size_bytes), float(serving.artifact_size_bytes),
              "An artifact must fit where it will be shipped and loaded."),
        check(Dimension.SERVING, "resident_memory_mb", Direction.AT_MOST,
              thresholds.max_resident_memory_mb, serving.resident_memory_mb,
              "Memory a serving host does not have is not a trade, it is a failure."),
        check(Dimension.SERVING, "warm_up_seconds", Direction.AT_MOST,
              thresholds.max_warm_up_seconds, serving.warm_up_seconds,
              "A model that is slow only after a restart is slow when it matters most."),
    ]

    # Accelerator memory is checked only when a budget is declared for it. A model
    # that needs none, on a host that has none, is not failing an absent budget.
    if thresholds.max_accelerator_memory_mb is not None:
        checks.append(
            check(Dimension.SERVING, "accelerator_memory_mb", Direction.AT_MOST,
                  thresholds.max_accelerator_memory_mb, serving.accelerator_memory_mb,
                  "A declared accelerator budget requires an accelerator measurement.")
        )

    return DimensionVerdict(Dimension.SERVING, tuple(checks))


def evaluate_training(
    challenger: CandidateEvidence, thresholds: DeclaredThresholds
) -> DimensionVerdict:
    """What it costs to produce. A model nobody can afford to retrain will not be."""
    training = challenger.training
    checks = [
        check(Dimension.TRAINING, "training_seconds", Direction.AT_MOST,
              thresholds.max_training_seconds, training.training_seconds,
              "A model that cannot finish inside its window will not be retrained."),
        check(Dimension.TRAINING, "peak_memory_mb", Direction.AT_MOST,
              thresholds.max_peak_memory_mb, training.peak_memory_mb,
              "Peak memory decides which machine can produce this at all."),
        check(Dimension.TRAINING, "snapshot_rows_per_second", Direction.AT_LEAST,
              thresholds.min_snapshot_rows_per_second, training.snapshot_rows_per_second,
              "Reading the population must not dominate producing the model."),
    ]
    return DimensionVerdict(Dimension.TRAINING, tuple(checks))
