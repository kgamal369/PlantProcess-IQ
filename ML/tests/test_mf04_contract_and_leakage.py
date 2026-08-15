"""T-175 part one: the outcome contract, the leakage gate, eligibility and the metrics.

None of these tests needs a model or a file. They certify the decisions that are made
before anything is fitted, and the mathematics that judges what is fitted.
"""

import math
import unittest

from ppiq_ml.models.mf04_supervised import (
    FeatureDeclaration,
    FeatureLegality,
    Mf04RefusalCode,
    OutcomeContractError,
    OutcomeDefinition,
    OutcomeKind,
    PredictionPoint,
    evaluate_classification,
    evaluate_continuous,
    evaluate_eligibility,
    evaluate_leakage,
    roc_auc,
)


def outcome(
    kind=OutcomeKind.BINARY,
    cutoff=50,
    detection=100,
    features=((("f_early", 10),), ),
    class_order=None,
    positive_class="yes",
):
    declared = tuple(
        FeatureDeclaration(name, available_at_ordinal=ordinal)
        for name, ordinal in features[0]
    )
    return OutcomeDefinition(
        outcome_code="fixture_outcome",
        kind=kind,
        grain_column="unit_reference",
        order_column="observed_at",
        label_column="outcome_value",
        detection_position_ordinal=detection,
        prediction_point=PredictionPoint("fixture_point", cutoff),
        features=declared,
        positive_class=positive_class if kind == OutcomeKind.BINARY else None,
        class_order=class_order,
    )


class TheOutcomeContractRefusesWhatItCannotInterpret(unittest.TestCase):
    def test_an_ordinal_outcome_without_a_declared_rank_is_refused(self):
        with self.assertRaises(OutcomeContractError) as raised:
            outcome(kind=OutcomeKind.ORDINAL)
        self.assertIn("rank", str(raised.exception))

    def test_a_binary_outcome_must_declare_which_value_is_positive(self):
        with self.assertRaises(OutcomeContractError) as raised:
            outcome(positive_class=None)
        self.assertIn("positive class", str(raised.exception))

    def test_a_structural_column_may_not_also_be_a_feature(self):
        with self.assertRaises(OutcomeContractError) as raised:
            outcome(features=((("observed_at", 10),),))
        self.assertIn("not evidence about itself", str(raised.exception))

    def test_an_outcome_with_no_features_is_refused(self):
        with self.assertRaises(OutcomeContractError):
            OutcomeDefinition(
                outcome_code="fixture_outcome",
                kind=OutcomeKind.CONTINUOUS,
                grain_column="unit_reference",
                order_column="observed_at",
                label_column="outcome_value",
                detection_position_ordinal=100,
                prediction_point=PredictionPoint("fixture_point", 50),
                features=(),
            )

    def test_the_contract_survives_a_round_trip_through_a_job_parameter_block(self):
        original = outcome(
            kind=OutcomeKind.ORDINAL,
            features=((("f_early", 10), ("f_mid", 40)),),
            class_order=("low", "middle", "high"),
        )
        rebuilt = OutcomeDefinition.from_dict(original.to_dict())
        self.assertEqual(original, rebuilt)

    def test_a_parameter_block_without_a_prediction_position_is_refused(self):
        raw = outcome().to_dict()
        del raw["prediction_point"]["position_ordinal"]
        with self.assertRaises(OutcomeContractError) as raised:
            OutcomeDefinition.from_dict(raw)
        self.assertIn("lookup", str(raised.exception))


class TheLeakageGate(unittest.TestCase):
    def test_a_feature_known_at_the_cutoff_is_legal_and_one_after_it_is_not(self):
        verdict = evaluate_leakage(
            outcome(features=((("f_at_cutoff", 50), ("f_after", 51)),))
        )
        self.assertFalse(verdict.passed)
        self.assertEqual(("f_at_cutoff",), verdict.legal_features)
        self.assertEqual(("f_after",), verdict.illegal_features)
        self.assertIn("train on future information", verdict.reason)

    def test_every_declared_feature_produces_a_detail_row_whatever_the_verdict(self):
        verdict = evaluate_leakage(
            outcome(features=((("f_a", 10), ("f_b", 40), ("f_c", 80)),))
        )
        self.assertEqual(3, len(verdict.detail))
        legalities = {d.column: d.legality for d in verdict.detail}
        self.assertEqual(FeatureLegality.LEGAL, legalities["f_a"])
        self.assertEqual(FeatureLegality.LEGAL, legalities["f_b"])
        self.assertEqual(FeatureLegality.ILLEGAL_AFTER_CUTOFF, legalities["f_c"])

    def test_an_outcome_observable_before_the_cutoff_is_a_lookup_not_a_prediction(self):
        verdict = evaluate_leakage(outcome(cutoff=50, detection=50))
        self.assertFalse(verdict.passed)
        self.assertIn("lookup, not a prediction", verdict.reason)

    def test_the_lookup_verdict_outranks_the_illegal_feature_verdict(self):
        """A lookup is refused even when every feature is legal, and first."""
        verdict = evaluate_leakage(
            outcome(cutoff=50, detection=20, features=((("f_a", 10), ("f_late", 90)),))
        )
        self.assertFalse(verdict.passed)
        self.assertIn("lookup", verdict.reason)

    def test_a_clean_contract_passes_and_names_what_it_admitted(self):
        verdict = evaluate_leakage(outcome(features=((("f_a", 10), ("f_b", 40)),)))
        self.assertTrue(verdict.passed)
        self.assertEqual(("f_a", "f_b"), verdict.legal_features)
        self.assertEqual((), verdict.illegal_features)


class EligibilityNamesTheNumberBehindEveryRefusal(unittest.TestCase):
    def clean_leakage(self, count=2):
        names = tuple((f"f_{i}", 10) for i in range(count))
        return evaluate_leakage(outcome(features=(names,)))

    def test_a_blocked_leakage_verdict_short_circuits_eligibility(self):
        blocked = evaluate_leakage(outcome(features=((("f_late", 90),),)))
        verdict = evaluate_eligibility(
            outcome(), blocked, ["yes"] * 100, train_units=75, holdout_units=25
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(Mf04RefusalCode.LEAKAGE_BLOCKED, verdict.code)

    def test_a_population_below_the_labelled_floor_is_refused_with_both_numbers(self):
        verdict = evaluate_eligibility(
            outcome(),
            self.clean_leakage(),
            ["yes"] * 10 + ["no"] * 10,
            train_units=15,
            holdout_units=5,
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(Mf04RefusalCode.TOO_FEW_LABELLED_UNITS, verdict.code)
        self.assertIn("required 40, observed 20", verdict.reason)

    def test_a_single_class_population_is_refused_as_such(self):
        verdict = evaluate_eligibility(
            outcome(),
            self.clean_leakage(),
            ["yes"] * 120,
            train_units=90,
            holdout_units=30,
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(Mf04RefusalCode.SINGLE_CLASS_POPULATION, verdict.code)

    def test_a_rare_class_is_refused_on_the_count_not_on_the_fraction_alone(self):
        verdict = evaluate_eligibility(
            outcome(),
            self.clean_leakage(),
            ["yes"] * 3 + ["no"] * 117,
            train_units=90,
            holdout_units=30,
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(Mf04RefusalCode.TOO_FEW_MINORITY_UNITS, verdict.code)

    def test_a_continuous_outcome_with_too_few_distinct_values_is_refused(self):
        verdict = evaluate_eligibility(
            outcome(kind=OutcomeKind.CONTINUOUS, positive_class=None),
            self.clean_leakage(),
            [1.0, 2.0] * 30,
            train_units=45,
            holdout_units=15,
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(Mf04RefusalCode.TOO_FEW_DISTINCT_VALUES, verdict.code)

    def test_an_empty_holdout_is_refused_even_when_the_population_is_large(self):
        verdict = evaluate_eligibility(
            outcome(),
            self.clean_leakage(),
            ["yes"] * 60 + ["no"] * 60,
            train_units=120,
            holdout_units=0,
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(Mf04RefusalCode.TOO_FEW_HOLDOUT_UNITS, verdict.code)

    def test_every_clause_carries_a_required_and_an_observed_value(self):
        verdict = evaluate_eligibility(
            outcome(),
            self.clean_leakage(),
            ["yes"] * 60 + ["no"] * 60,
            train_units=90,
            holdout_units=30,
        )
        self.assertTrue(verdict.eligible)
        self.assertEqual((), verdict.failed_clauses)
        for clause in verdict.clauses:
            self.assertIsInstance(clause.required, float)
            self.assertIsInstance(clause.observed, float)
            self.assertTrue(clause.sentence.strip())


class TheMetricsAreCertifiedAgainstKnownAnswers(unittest.TestCase):
    def test_a_published_four_point_case_gives_the_published_area(self):
        self.assertAlmostEqual(0.75, roc_auc([0, 0, 1, 1], [0.1, 0.4, 0.35, 0.8]), places=12)

    def test_perfect_separation_is_one_and_reversed_separation_is_zero(self):
        self.assertEqual(1.0, roc_auc([0, 0, 1, 1], [0.1, 0.2, 0.8, 0.9]))
        self.assertEqual(0.0, roc_auc([1, 1, 0, 0], [0.1, 0.2, 0.8, 0.9]))

    def test_a_constant_score_is_exactly_one_half_because_ties_are_corrected(self):
        self.assertEqual(0.5, roc_auc([0, 1, 0, 1], [0.3, 0.3, 0.3, 0.3]))

    def test_the_area_is_undefined_when_one_class_is_absent(self):
        self.assertIsNone(roc_auc([1, 1, 1], [0.2, 0.5, 0.9]))

    def test_binary_metrics_match_hand_calculation(self):
        result = evaluate_classification(
            classes=("no", "yes"),
            labels=["no", "yes"],
            probabilities=[(0.9, 0.1), (0.1, 0.9)],
        )
        self.assertAlmostEqual(0.01, result.values["brier"], places=12)
        self.assertAlmostEqual(-math.log(0.9), result.values["log_loss"], places=12)
        self.assertAlmostEqual(0.5, result.values["prevalence"], places=12)
        self.assertEqual(1.0, result.values["accuracy"])

    def test_an_undefined_area_is_omitted_and_explained_rather_than_reported(self):
        result = evaluate_classification(
            classes=("no", "yes"),
            labels=["yes", "yes"],
            probabilities=[(0.4, 0.6), (0.3, 0.7)],
        )
        self.assertNotIn("auc", result.values)
        self.assertIn("one class only", result.undefined["auc"])
        for value in result.values.values():
            self.assertFalse(math.isnan(value))

    def test_multiclass_log_loss_matches_hand_calculation(self):
        result = evaluate_classification(
            classes=("a", "b", "c"),
            labels=["a", "b"],
            probabilities=[(0.5, 0.25, 0.25), (0.5, 0.25, 0.25)],
        )
        expected = (-math.log(0.5) - math.log(0.25)) / 2.0
        self.assertAlmostEqual(expected, result.values["log_loss"], places=12)
        self.assertNotIn("auc", result.values)

    def test_an_ordinal_outcome_reports_the_distance_between_ranks(self):
        result = evaluate_classification(
            classes=("low", "middle", "high"),
            labels=["low", "low"],
            probabilities=[(0.1, 0.2, 0.7), (0.7, 0.2, 0.1)],
            class_order=("low", "middle", "high"),
        )
        # One prediction is two ranks away, the other is exact.
        self.assertAlmostEqual(1.0, result.values["mean_absolute_rank_error"], places=12)

    def test_a_label_the_model_never_saw_produces_no_metric_at_all(self):
        result = evaluate_classification(
            classes=("a", "b"), labels=["a", "c"], probabilities=[(0.6, 0.4), (0.6, 0.4)]
        )
        self.assertNotIn("accuracy", result.values)
        self.assertIn("never saw", result.undefined["all"])

    def test_continuous_metrics_match_hand_calculation(self):
        result = evaluate_continuous([1.0, 2.0, 3.0], [1.0, 2.0, 5.0])
        self.assertAlmostEqual(2.0 / 3.0, result.values["mae"], places=12)
        self.assertAlmostEqual(math.sqrt(4.0 / 3.0), result.values["rmse"], places=12)
        self.assertAlmostEqual(1.0 - 4.0 / 2.0, result.values["r2"], places=12)

    def test_a_constant_truth_leaves_the_explained_proportion_undefined(self):
        result = evaluate_continuous([5.0, 5.0, 5.0], [5.0, 4.0, 6.0])
        self.assertNotIn("r2", result.values)
        self.assertIn("undefined", result.undefined["r2"])


if __name__ == "__main__":
    unittest.main()
