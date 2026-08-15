"""T-176 Pack 2: the real TreeSHAP path, from a fitted model to a promotion decision.

Nothing here uses a fixture literal for a contribution. Every attribution in this
file is computed from a model that was actually trained in the test that uses it,
which is the difference between proving the stability kernel and proving the
candidate that feeds it.
"""

import unittest

from ppiq_ml.explanation import (
    TREESHAP_METHOD,
    ClaimClass,
    ContributionEvidence,
    ContributionScale,
    EvidenceIdentity,
    ExplanationError,
    ExplanationProvider,
    ExplanationUnavailableError,
    LightGbmTreeShapExplanationProvider,
    to_promotion_evidence,
)
from ppiq_ml.governance import (
    CandidateEvidence,
    CandidateClass,
    ExplanationEvidence,
    PromotionOutcome,
    QualityEvidence,
    build_document,
    decide,
    evaluate_stability,
)
from tests.t176_promotion_fixture import HOLDOUT, SNAPSHOT, incumbent, serving, thresholds, training

FEATURES = ("measurement_alpha", "measurement_beta", "measurement_gamma")
IDENTITY = EvidenceIdentity(
    model_identity="fitted-model-0001",
    artifact_identity="artifact-0001",
    snapshot_identity="snapshot-fixture-0001",
    holdout_identity="holdout-fixture-0001",
)


def real_challenger(attributions, code):
    """A challenger whose explanation evidence is the real thing, computed above.

    Built here rather than through the Pack 1 fixture helper, whose feature width is
    its own. A committed file is not edited to make a later test convenient.
    """
    return CandidateEvidence(
        candidate_code=code,
        candidate_class=CandidateClass.ENGINEERED_FEATURES,
        quality=QualityEvidence(
            primary_metric_name="auc",
            primary_metric=0.82,
            primary_higher_is_better=True,
            proper_score_name="log_loss",
            proper_score=0.180,
            calibration_error=0.020,
            out_of_time_primary_metric=0.79,
            subgroup_primary_metrics={"regime_one": 0.83, "regime_two": 0.79},
            missingness_primary_metric=0.78,
            explanation=ExplanationEvidence(
                method=TREESHAP_METHOD,
                feature_names=FEATURES,
                attributions=tuple(attributions),
            ),
        ),
        serving=serving(),
        training=training(),
        snapshot_identity=SNAPSHOT,
        holdout_identity=HOLDOUT,
    )


def training_rows(signal_position: int, units: int = 160, seed: int = 20260815):
    """A population whose outcome depends on exactly one declared feature.

    Which feature carries the signal is a parameter, so a second model can be built
    that genuinely disagrees with the first about what matters.
    """
    state = seed
    rows, labels = [], []
    for index in range(units):
        values = []
        for _ in range(len(FEATURES)):
            state = (state * 1103515245 + 12345) & 0x7FFFFFFF
            values.append(state / 0x7FFFFFFF * 10.0)
        rows.append(values)
        labels.append(1.0 if values[signal_position] > 5.0 else 0.0)
    return rows, labels


def fit_booster(signal_position: int, seed: int = 11):
    import lightgbm
    import numpy

    rows, labels = training_rows(signal_position)
    dataset = lightgbm.Dataset(
        numpy.array(rows, dtype=float), label=numpy.array(labels, dtype=float),
        free_raw_data=False,
    )
    booster = lightgbm.train(
        {
            "objective": "binary",
            "num_leaves": 7,
            "min_data_in_leaf": 5,
            "learning_rate": 0.15,
            "verbosity": -1,
            "seed": seed,
            "deterministic": True,
            "force_row_wise": True,
            "num_threads": 1,
        },
        dataset,
        num_boost_round=30,
    )
    return booster, rows


class TheContributionsAreProducedByTheModel(unittest.TestCase):
    """Falsification one: these numbers cannot be fixture literals."""

    def setUp(self):
        self.provider = LightGbmTreeShapExplanationProvider()
        self.booster, self.rows = fit_booster(signal_position=0)
        self.evidence = self.provider.explain(
            self.booster, self.rows[:40], FEATURES, IDENTITY
        )

    def test_base_value_plus_contributions_reconstructs_the_model_raw_output(self):
        """The defining property of a tree contribution, checked on every unit.

        A fabricated table does not reconstruct a model's output. This assertion is
        what makes the evidence impossible to fake without the model.
        """
        import numpy

        raw = numpy.asarray(
            self.booster.predict(numpy.array(self.rows[:40], dtype=float), raw_score=True),
            dtype=float,
        )
        for index in range(self.evidence.unit_count):
            self.assertAlmostEqual(
                float(raw[index]), self.evidence.reconstructed_output(index), places=9
            )

    def test_the_model_signal_feature_carries_the_largest_attribution(self):
        """The outcome was built to depend on one feature and the method finds it."""
        attributions = self.evidence.mean_absolute_contributions()
        self.assertEqual(0, max(range(len(attributions)), key=lambda i: attributions[i]))
        self.assertGreater(attributions[0], 0.0)

    def test_the_record_names_its_method_scale_and_claim(self):
        self.assertEqual(TREESHAP_METHOD, self.evidence.explanation_method)
        self.assertEqual(ClaimClass.PREDICTIVE_CONTRIBUTION, self.evidence.claim_class)
        self.assertEqual(ContributionScale.RAW_MODEL_OUTPUT, self.evidence.contribution_scale)

    def test_the_record_carries_every_required_identity(self):
        recorded = self.evidence.identity.to_dict()
        for field in ("model_identity", "artifact_identity", "snapshot_identity", "holdout_identity"):
            self.assertTrue(recorded[field], field)


class TheDimensionsMustMatchTheModelInput(unittest.TestCase):
    """Falsification two."""

    def setUp(self):
        self.provider = LightGbmTreeShapExplanationProvider()
        self.booster, self.rows = fit_booster(signal_position=1)

    def test_one_contribution_per_feature_per_unit(self):
        evidence = self.provider.explain(self.booster, self.rows[:25], FEATURES, IDENTITY)
        self.assertEqual(25, evidence.unit_count)
        self.assertEqual(len(FEATURES), len(evidence.feature_names))
        for row in evidence.contributions:
            self.assertEqual(len(FEATURES), len(row))
        self.assertEqual(25, len(evidence.base_values))

    def test_a_feature_name_list_that_does_not_match_the_matrix_is_refused(self):
        with self.assertRaises(ExplanationError) as raised:
            self.provider.explain(
                self.booster, self.rows[:5], ("only_one_name",), IDENTITY
            )
        self.assertIn("would not line up with the model input", str(raised.exception))

    def test_a_model_that_cannot_be_asked_for_contributions_is_refused(self):
        class NotAskable:
            def predict(self, matrix):
                return [0.0] * len(matrix)

        with self.assertRaises(ExplanationUnavailableError):
            self.provider.explain(NotAskable(), self.rows[:5], FEATURES, IDENTITY)

    def test_an_object_that_cannot_predict_at_all_is_refused(self):
        with self.assertRaises(ExplanationUnavailableError):
            self.provider.explain(object(), self.rows[:5], FEATURES, IDENTITY)

    def test_an_out_of_range_output_index_is_refused(self):
        with self.assertRaises(ExplanationError):
            self.provider.explain(self.booster, self.rows[:5], FEATURES, IDENTITY, output_index=4)


class RepeatedRunsOnOneModelAreStable(unittest.TestCase):
    """Falsification three."""

    def test_two_runs_over_the_same_model_and_units_are_identical(self):
        provider = LightGbmTreeShapExplanationProvider()
        booster, rows = fit_booster(signal_position=2)
        first = provider.explain(booster, rows[:50], FEATURES, IDENTITY)
        second = provider.explain(booster, rows[:50], FEATURES, IDENTITY)
        self.assertEqual(first.evidence_identity(), second.evidence_identity())
        self.assertEqual(first.contributions, second.contributions)

    def test_repeated_real_runs_clear_the_declared_stability_floor(self):
        provider = LightGbmTreeShapExplanationProvider()
        booster, rows = fit_booster(signal_position=0)
        runs = [
            provider.explain(booster, rows[0:50], FEATURES, IDENTITY),
            provider.explain(booster, rows[50:100], FEATURES, IDENTITY),
            provider.explain(booster, rows[100:150], FEATURES, IDENTITY),
        ]
        evidence = to_promotion_evidence(runs)
        stability = evaluate_stability(evidence.attributions, top_k=2)
        self.assertGreaterEqual(stability.rank_agreement, 0.8)
        self.assertEqual(1.0, stability.top_k_overlap)


class UnstableRealAttributionsFailTheQualityGate(unittest.TestCase):
    """Falsification four, carried all the way into the promotion decision."""

    def build(self, positions):
        provider = LightGbmTreeShapExplanationProvider()
        runs = []
        for position in positions:
            booster, rows = fit_booster(signal_position=position)
            runs.append(provider.explain(booster, rows[:60], FEATURES, IDENTITY))
        return to_promotion_evidence(runs)

    def test_models_that_disagree_about_what_matters_fail_the_stability_floor(self):
        evidence = self.build((0, 1, 2))
        stability = evaluate_stability(evidence.attributions, top_k=1)
        self.assertLess(stability.rank_agreement, 0.8)

    def test_the_promotion_kernel_rejects_the_unstable_evidence(self):
        unstable = self.build((0, 1, 2))
        decision = decide(
            build_document(
                incumbent=incumbent(),
                challenger=real_challenger(unstable.attributions, "unstable_real_explanations"),
                thresholds=thresholds(explanation_top_k=1),
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_REJECTED, decision.outcome)
        self.assertEqual(("quality",), decision.failed_dimensions)
        self.assertIn("quality.explanation_rank_agreement", decision.failed_checks)

    def test_the_promotion_kernel_accepts_the_stable_evidence_from_one_model(self):
        provider = LightGbmTreeShapExplanationProvider()
        booster, rows = fit_booster(signal_position=0)
        stable = to_promotion_evidence(
            [
                provider.explain(booster, rows[0:50], FEATURES, IDENTITY),
                provider.explain(booster, rows[50:100], FEATURES, IDENTITY),
                provider.explain(booster, rows[100:150], FEATURES, IDENTITY),
            ]
        )
        decision = decide(
            build_document(
                incumbent=incumbent(),
                challenger=real_challenger(stable.attributions, "stable_real_explanations"),
                thresholds=thresholds(explanation_top_k=2),
            )
        )
        self.assertEqual(PromotionOutcome.CHALLENGER_APPROVED, decision.outcome)
        self.assertGreaterEqual(decision.explanation_stability.rank_agreement, 0.8)


class TheProviderIsReplaceable(unittest.TestCase):
    """Falsification five: TreeSHAP is a candidate, not a framework contract."""

    class RecordedContributionProvider(ExplanationProvider):
        """A second provider that produces no tree and touches no library."""

        @property
        def method(self):
            return "recorded_contribution_table"

        def supports(self, model):
            return isinstance(model, dict)

        def explain(self, model, feature_rows, feature_names, identity, output_index=0):
            names = tuple(feature_names)
            return ContributionEvidence(
                explanation_method=self.method,
                claim_class=ClaimClass.PREDICTIVE_CONTRIBUTION,
                contribution_scale=ContributionScale.RAW_MODEL_OUTPUT,
                identity=identity,
                feature_names=names,
                contributions=tuple(tuple(model[n] for n in names) for _ in feature_rows),
                base_values=tuple(0.0 for _ in feature_rows),
                output_index=output_index,
            )

    def test_a_second_provider_satisfies_the_same_interface(self):
        provider = self.RecordedContributionProvider()
        self.assertIsInstance(provider, ExplanationProvider)
        self.assertNotEqual(TREESHAP_METHOD, provider.method)

    def test_the_stability_kernel_judges_the_second_provider_identically(self):
        provider = self.RecordedContributionProvider()
        model = {"measurement_alpha": 0.9, "measurement_beta": 0.2, "measurement_gamma": 0.05}
        runs = [
            provider.explain(model, [[0, 0, 0]] * 5, FEATURES, IDENTITY),
            provider.explain(model, [[0, 0, 0]] * 5, FEATURES, IDENTITY),
        ]
        evidence = to_promotion_evidence(runs)
        self.assertEqual("recorded_contribution_table", evidence.method)
        stability = evaluate_stability(evidence.attributions, top_k=2)
        self.assertAlmostEqual(1.0, stability.rank_agreement, places=12)

    def test_the_promotion_kernel_names_no_explanation_library(self):
        import ppiq_ml.governance.kernel as kernel_module
        import ppiq_ml.governance.dimensions as dimensions_module

        for module in (kernel_module, dimensions_module):
            with open(module.__file__, encoding="ascii") as handle:
                source = handle.read().lower()
            self.assertNotIn("lightgbm", source)
            self.assertNotIn("import numpy", source)

    def test_runs_from_two_different_methods_are_never_compared(self):
        tree_provider = LightGbmTreeShapExplanationProvider()
        booster, rows = fit_booster(signal_position=0)
        other = self.RecordedContributionProvider().explain(
            {"measurement_alpha": 1.0, "measurement_beta": 0.5, "measurement_gamma": 0.1},
            [[0, 0, 0]] * 5,
            FEATURES,
            IDENTITY,
        )
        with self.assertRaises(ExplanationError) as raised:
            to_promotion_evidence([tree_provider.explain(booster, rows[:20], FEATURES, IDENTITY), other])
        self.assertIn("not instability in one model", str(raised.exception))

    def test_runs_from_two_different_snapshots_are_never_compared(self):
        provider = LightGbmTreeShapExplanationProvider()
        booster, rows = fit_booster(signal_position=0)
        elsewhere = EvidenceIdentity(
            model_identity="fitted-model-0001",
            artifact_identity="artifact-0002",
            snapshot_identity="a-different-snapshot",
        )
        with self.assertRaises(ExplanationError):
            to_promotion_evidence(
                [
                    provider.explain(booster, rows[:20], FEATURES, IDENTITY),
                    provider.explain(booster, rows[:20], FEATURES, elsewhere),
                ]
            )


class TheClaimIsContributionAndNeverCause(unittest.TestCase):
    """Falsification six."""

    def test_only_one_claim_class_exists(self):
        self.assertEqual(1, len(list(ClaimClass)))
        self.assertEqual("PREDICTIVE_CONTRIBUTION", ClaimClass.PREDICTIVE_CONTRIBUTION.value)

    def test_evidence_cannot_be_built_with_any_other_claim(self):
        with self.assertRaises(ExplanationError) as raised:
            ContributionEvidence(
                explanation_method=TREESHAP_METHOD,
                claim_class="a_stronger_claim",
                contribution_scale=ContributionScale.RAW_MODEL_OUTPUT,
                identity=IDENTITY,
                feature_names=FEATURES,
                contributions=((0.1, 0.2, 0.3),),
                base_values=(0.0,),
            )
        self.assertIn("predictive contribution", str(raised.exception))

    def test_the_contribution_scale_is_recorded_and_is_not_a_probability(self):
        provider = LightGbmTreeShapExplanationProvider()
        booster, rows = fit_booster(signal_position=0)
        evidence = provider.explain(booster, rows[:10], FEATURES, IDENTITY)
        self.assertEqual("raw_model_output", evidence.contribution_scale.value)
        # A raw margin is unbounded; a probability is not. At least one reconstructed
        # output falling outside the unit interval proves these are not probabilities.
        outputs = [evidence.reconstructed_output(i) for i in range(evidence.unit_count)]
        self.assertTrue(any(v < 0.0 or v > 1.0 for v in outputs))


if __name__ == "__main__":
    unittest.main()
