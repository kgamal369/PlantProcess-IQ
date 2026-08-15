"""T-176 part two: the three dimensions, the encoder inequality, and the decision.

The four mandatory falsifications are carried by the class named for them at the
bottom of this file. Each one bends exactly one number away from a document that is
otherwise clean, so the check being aimed at is the only thing that could have caused
the verdict.
"""

import unittest

from ppiq_ml.governance import (
    CLAUSE_ARTIFACT_SIZE,
    CLAUSE_EXPLANATION_STABILITY,
    CLAUSE_METRIC_LIFT,
    CLAUSE_P95_LATENCY_DELTA,
    CandidateClass,
    Dimension,
    PromotionOutcome,
    decide,
)
from tests.t176_promotion_fixture import (
    HOLDOUT,
    SNAPSHOT,
    STABLE_ATTRIBUTIONS,
    UNSTABLE_ATTRIBUTIONS,
    candidate,
    document,
    incumbent,
    quality,
    serving,
    thresholds,
    training,
)


class ACleanDocumentIsApproved(unittest.TestCase):
    def test_a_challenger_clearing_every_check_is_approved(self):
        decision = decide(document())
        self.assertEqual(PromotionOutcome.CHALLENGER_APPROVED, decision.outcome)
        self.assertEqual((), decision.failed_dimensions)
        self.assertEqual((), decision.failed_checks)
        self.assertEqual((), decision.unmeasured_checks)

    def test_the_decision_states_that_no_weighted_score_was_used(self):
        decision = decide(document())
        self.assertFalse(decision.weighted_score_used)

    def test_all_three_dimensions_are_evaluated_and_reported(self):
        decision = decide(document())
        self.assertEqual(
            [Dimension.QUALITY, Dimension.SERVING, Dimension.TRAINING],
            [v.dimension for v in decision.dimensions],
        )
        for verdict in decision.dimensions:
            self.assertTrue(verdict.passed)
            self.assertGreaterEqual(len(verdict.checks), 3)


class TheDimensionsAreIndependent(unittest.TestCase):
    def test_excellent_quality_does_not_buy_back_a_failed_serving_budget(self):
        """The rule this kernel exists for, stated as one test."""
        decision = decide(
            document(
                challenger=candidate(
                    quality_evidence=quality(primary=0.99, proper_score=0.05),
                    serving_evidence=serving(p95=400.0, p99=900.0),
                )
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_REJECTED, decision.outcome)
        self.assertEqual(("serving",), decision.failed_dimensions)
        self.assertIn("serving.p95_latency_ms", decision.failed_checks)
        self.assertIn("No result on another dimension compensates", decision.reason)

    def test_a_failed_training_budget_is_named_on_its_own_dimension(self):
        decision = decide(
            document(challenger=candidate(training_evidence=training(duration=9999.0)))
        )
        self.assertEqual(("training",), decision.failed_dimensions)
        self.assertIn("training.training_seconds", decision.failed_checks)

    def test_every_failed_dimension_is_named_not_only_the_first(self):
        decision = decide(
            document(
                challenger=candidate(
                    quality_evidence=quality(calibration_error=0.9),
                    serving_evidence=serving(p50=999.0, p95=999.0, p99=999.0),
                    training_evidence=training(peak=99999.0),
                )
            )
        )
        self.assertEqual(("quality", "serving", "training"), decision.failed_dimensions)

    def test_a_failure_sentence_carries_both_numbers(self):
        decision = decide(
            document(challenger=candidate(serving_evidence=serving(p95=120.0)))
        )
        self.assertIn("serving.p95_latency_ms required at most 90", decision.reason)
        self.assertIn("observed 120", decision.reason)

    def test_out_of_time_and_missingness_shortfalls_are_separate_checks(self):
        decision = decide(
            document(challenger=candidate(quality_evidence=quality(out_of_time=0.50)))
        )
        self.assertIn("quality.out_of_time_drop", decision.failed_checks)
        self.assertNotIn("quality.missingness_drop", decision.failed_checks)

        decision = decide(
            document(challenger=candidate(quality_evidence=quality(missingness=0.40)))
        )
        self.assertIn("quality.missingness_drop", decision.failed_checks)
        self.assertNotIn("quality.out_of_time_drop", decision.failed_checks)

    def test_a_result_that_holds_on_average_and_fails_on_one_regime_is_refused(self):
        decision = decide(
            document(
                challenger=candidate(
                    quality_evidence=quality(
                        subgroups={"regime_one": 0.92, "regime_two": 0.55}
                    )
                )
            )
        )
        self.assertIn("quality.subgroup_spread", decision.failed_checks)


class TheDocumentMustBeComparableAndComplete(unittest.TestCase):
    def test_two_different_snapshots_produce_no_decision_at_all(self):
        decision = decide(
            document(challenger=candidate(snapshot="a-different-snapshot"))
        )
        self.assertEqual(PromotionOutcome.NOT_EVALUABLE, decision.outcome)
        self.assertIn("different snapshots", decision.reason)

    def test_two_different_holdouts_produce_no_decision_at_all(self):
        decision = decide(document(challenger=candidate(holdout="a-different-holdout")))
        self.assertEqual(PromotionOutcome.NOT_EVALUABLE, decision.outcome)
        self.assertIn("different holdouts", decision.reason)

    def test_two_different_primary_metrics_are_never_subtracted(self):
        decision = decide(
            document(
                challenger=candidate(
                    quality_evidence=quality(primary_metric_name="rmse", higher_is_better=False)
                )
            )
        )
        self.assertEqual(PromotionOutcome.NOT_EVALUABLE, decision.outcome)
        self.assertIn("cannot be subtracted", decision.reason)

    def test_a_declared_budget_with_no_measurement_makes_the_decision_unevaluable(self):
        """A gate that disappears when unmeasured is a gate that passes by silence."""
        decision = decide(
            document(declared=thresholds(max_accelerator_memory_mb=2048.0))
        )
        self.assertEqual(PromotionOutcome.NOT_EVALUABLE, decision.outcome)
        self.assertIn("serving.accelerator_memory_mb", decision.unmeasured_checks)
        self.assertIn("not a gate", decision.reason)

    def test_an_accelerator_budget_that_is_measured_is_checked_normally(self):
        decision = decide(
            document(
                challenger=candidate(serving_evidence=serving(accelerator=1024.0)),
                declared=thresholds(max_accelerator_memory_mb=2048.0),
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_APPROVED, decision.outcome)

    def test_absent_explanation_evidence_is_unmeasured_rather_than_a_pass(self):
        decision = decide(
            document(challenger=candidate(quality_evidence=quality(attributions=None)))
        )
        self.assertEqual(PromotionOutcome.NOT_EVALUABLE, decision.outcome)
        self.assertIn("quality.explanation_rank_agreement", decision.unmeasured_checks)


class TheEncoderInequalityIsAConjunction(unittest.TestCase):
    def encoder(self, **overrides):
        return candidate(
            code="process_encoder_path",
            candidate_class=CandidateClass.ENCODER,
            **overrides,
        )

    def test_the_rule_does_not_apply_to_an_engineered_feature_challenger(self):
        decision = decide(document())
        self.assertFalse(decision.encoder_rule.applicable)
        self.assertFalse(decision.encoder_rule.promote_encoder)
        self.assertEqual(PromotionOutcome.CHALLENGER_APPROVED, decision.outcome)

    def test_an_encoder_clearing_all_four_clauses_is_approved(self):
        decision = decide(document(challenger=self.encoder()))
        self.assertEqual(PromotionOutcome.CHALLENGER_APPROVED, decision.outcome)
        self.assertTrue(decision.encoder_rule.promote_encoder)
        self.assertEqual((), decision.encoder_rule.failed_clauses)

    def test_a_latency_delta_beyond_the_budget_defeats_a_large_lift(self):
        decision = decide(
            document(
                challenger=self.encoder(
                    quality_evidence=quality(primary=0.95, proper_score=0.10),
                    serving_evidence=serving(p95=85.0),
                )
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_REJECTED, decision.outcome)
        self.assertIn(CLAUSE_P95_LATENCY_DELTA, decision.encoder_rule.failed_clauses)
        self.assertIn("no clause is tradeable", decision.reason)

    def test_an_artifact_outside_the_declared_size_class_defeats_a_large_lift(self):
        decision = decide(
            document(
                challenger=self.encoder(
                    quality_evidence=quality(primary=0.95, proper_score=0.10),
                    serving_evidence=serving(artifact_size=7_500_000),
                ),
                declared=thresholds(declared_size_class_bytes=4_000_000),
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_REJECTED, decision.outcome)
        self.assertIn(CLAUSE_ARTIFACT_SIZE, decision.encoder_rule.failed_clauses)

    def test_the_four_clauses_are_all_recorded_whatever_the_verdict(self):
        decision = decide(document(challenger=self.encoder()))
        names = [c.name for c in decision.encoder_rule.clauses]
        self.assertEqual(
            [
                CLAUSE_METRIC_LIFT,
                CLAUSE_P95_LATENCY_DELTA,
                CLAUSE_ARTIFACT_SIZE,
                CLAUSE_EXPLANATION_STABILITY,
            ],
            names,
        )


class TheFourMandatoryFalsifications(unittest.TestCase):
    def test_one_better_discrimination_with_worse_calibration_is_rejected(self):
        challenger = candidate(
            code="better_ranking_worse_probabilities",
            quality_evidence=quality(primary=0.94, proper_score=0.260, calibration_error=0.140),
        )
        decision = decide(document(challenger=challenger))

        # It genuinely does discriminate better than the incumbent.
        self.assertGreater(
            challenger.quality.primary_metric, incumbent().quality.primary_metric
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_REJECTED, decision.outcome)
        self.assertEqual(("quality",), decision.failed_dimensions)
        self.assertIn("quality.calibration_error", decision.failed_checks)
        self.assertIn("quality.proper_score_not_worse_than_incumbent", decision.failed_checks)

    def test_two_unstable_explanations_are_rejected(self):
        decision = decide(
            document(
                challenger=candidate(
                    code="unstable_explanations",
                    quality_evidence=quality(attributions=UNSTABLE_ATTRIBUTIONS),
                )
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_REJECTED, decision.outcome)
        self.assertEqual(("quality",), decision.failed_dimensions)
        self.assertIn("quality.explanation_rank_agreement", decision.failed_checks)
        self.assertLess(decision.explanation_stability.rank_agreement, 0.8)

    def test_three_an_encoder_within_the_lift_threshold_loses_to_the_simpler_path(self):
        """The costly path must be materially better, not merely ahead."""
        challenger = candidate(
            code="process_encoder_path",
            candidate_class=CandidateClass.ENCODER,
            quality_evidence=quality(primary=0.79, proper_score=0.195),
        )
        decision = decide(document(challenger=challenger))

        self.assertEqual(PromotionOutcome.SIMPLER_ALTERNATIVE_RETAINED, decision.outcome)
        self.assertEqual((), decision.failed_dimensions)
        self.assertTrue(decision.encoder_rule.simpler_path_wins)
        self.assertFalse(decision.encoder_rule.promote_encoder)
        self.assertIn(CLAUSE_METRIC_LIFT, decision.encoder_rule.failed_clauses)
        self.assertIn("simpler engineered-feature path wins", decision.reason)
        self.assertAlmostEqual(0.01, decision.encoder_rule.metric_lift, places=10)

    def test_four_the_same_document_decided_twice_is_byte_identical(self):
        for challenger in (
            candidate(),
            candidate(quality_evidence=quality(attributions=UNSTABLE_ATTRIBUTIONS)),
            candidate(
                candidate_class=CandidateClass.ENCODER,
                quality_evidence=quality(primary=0.79),
            ),
            candidate(serving_evidence=serving(p95=400.0), training_evidence=training(peak=1e9)),
        ):
            first = decide(document(challenger=challenger))
            second = decide(document(challenger=challenger))
            self.assertEqual(first.to_json(), second.to_json())
            self.assertEqual(first.decision_identity(), second.decision_identity())
            self.assertEqual(first.failed_dimensions, second.failed_dimensions)
            self.assertEqual(first.failed_checks, second.failed_checks)

    def test_four_the_decision_names_the_document_it_was_made_from(self):
        built = document()
        decision = decide(built)
        self.assertEqual(built.document_identity(), decision.document_identity)
        self.assertEqual(SNAPSHOT, built.challenger.snapshot_identity)
        self.assertEqual(HOLDOUT, built.challenger.holdout_identity)

    def test_four_a_changed_number_changes_the_document_and_the_decision_identity(self):
        clean = decide(document())
        bent = decide(document(challenger=candidate(serving_evidence=serving(p95=120.0))))
        self.assertNotEqual(clean.document_identity, bent.document_identity)
        self.assertNotEqual(clean.decision_identity(), bent.decision_identity())


class TheStableFixtureIsNotAccidentallyPassing(unittest.TestCase):
    def test_the_stable_attributions_really_do_clear_the_declared_floor(self):
        decision = decide(
            document(challenger=candidate(quality_evidence=quality(attributions=STABLE_ATTRIBUTIONS)))
        )
        self.assertGreaterEqual(decision.explanation_stability.rank_agreement, 0.8)
        self.assertEqual(PromotionOutcome.CHALLENGER_APPROVED, decision.outcome)


if __name__ == "__main__":
    unittest.main()
