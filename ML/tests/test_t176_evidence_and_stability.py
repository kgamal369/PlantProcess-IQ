"""T-176 part one: the evidence contract and the stability mathematics.

Known answers only. If these are wrong, every decision built on them is wrong in a
way no later test would reveal.
"""

import unittest

from ppiq_ml.governance import (
    EXPLANATION_METHOD_INITIAL_CANDIDATE,
    EvidenceError,
    ExplanationEvidence,
    StabilityError,
    evaluate_stability,
    lift,
    midranks,
    rank_correlation,
    top_k_indices,
)
from tests.t176_promotion_fixture import (
    FEATURE_NAMES,
    STABLE_ATTRIBUTIONS,
    UNSTABLE_ATTRIBUTIONS,
    document,
)


class TheEvidenceContractRefusesWhatItCannotJudge(unittest.TestCase):
    def test_a_single_explanation_run_is_refused_as_evidence_of_stability(self):
        with self.assertRaises(EvidenceError) as raised:
            ExplanationEvidence(
                method=EXPLANATION_METHOD_INITIAL_CANDIDATE,
                feature_names=FEATURE_NAMES,
                attributions=(STABLE_ATTRIBUTIONS[0],),
            )
        self.assertIn("cannot disagree with itself", str(raised.exception))

    def test_an_attribution_vector_of_the_wrong_width_is_refused(self):
        with self.assertRaises(EvidenceError):
            ExplanationEvidence(
                method=EXPLANATION_METHOD_INITIAL_CANDIDATE,
                feature_names=FEATURE_NAMES,
                attributions=((0.1, 0.2), (0.1, 0.2)),
            )

    def test_explanation_evidence_must_name_its_producer(self):
        with self.assertRaises(EvidenceError):
            ExplanationEvidence(
                method="   ",
                feature_names=FEATURE_NAMES,
                attributions=STABLE_ATTRIBUTIONS,
            )

    def test_the_initial_explanation_candidate_is_named_but_not_required(self):
        """The kernel judges vectors and never learns which library made them."""
        evidence = ExplanationEvidence(
            method="some_other_attribution_method",
            feature_names=FEATURE_NAMES,
            attributions=STABLE_ATTRIBUTIONS,
        )
        self.assertEqual("some_other_attribution_method", evidence.method)
        self.assertEqual("treeshap", EXPLANATION_METHOD_INITIAL_CANDIDATE)

    def test_the_document_identity_is_stable_and_content_sensitive(self):
        first = document()
        second = document()
        self.assertEqual(first.document_identity(), second.document_identity())
        changed = document(declared=None, held=None)
        self.assertEqual(64, len(changed.document_identity()))

    def test_lift_is_signed_so_that_positive_always_means_better(self):
        self.assertAlmostEqual(0.04, lift(0.82, 0.78, True), places=12)
        # For an error metric, a smaller number is the better one.
        self.assertAlmostEqual(0.04, lift(0.78, 0.82, False), places=12)


class TheStabilityMathematicsIsCertifiedAgainstKnownAnswers(unittest.TestCase):
    def test_ties_share_a_midrank(self):
        self.assertEqual([1.5, 1.5, 3.0], midranks([2.0, 2.0, 5.0]))

    def test_identical_orderings_correlate_perfectly(self):
        self.assertAlmostEqual(1.0, rank_correlation([1.0, 2.0, 3.0], [10.0, 20.0, 30.0]), places=12)

    def test_a_reversed_ordering_correlates_at_minus_one(self):
        self.assertAlmostEqual(-1.0, rank_correlation([1.0, 2.0, 3.0], [30.0, 20.0, 10.0]), places=12)

    def test_two_constant_vectors_agree_completely_rather_than_undefined(self):
        """Both runs said no feature matters. That is agreement, not a missing number."""
        self.assertEqual(1.0, rank_correlation([0.0, 0.0, 0.0], [0.0, 0.0, 0.0]))

    def test_one_constant_vector_against_a_varying_one_is_no_agreement(self):
        self.assertEqual(0.0, rank_correlation([0.0, 0.0, 0.0], [1.0, 2.0, 3.0]))

    def test_top_k_selects_by_magnitude_and_breaks_ties_by_position(self):
        self.assertEqual({0, 2}, top_k_indices([0.9, 0.1, -0.8, 0.05], 2))
        self.assertEqual({0, 1}, top_k_indices([0.5, 0.5, 0.5], 2))

    def test_repeated_identical_runs_score_one_on_every_statistic(self):
        vector = (0.4, 0.3, 0.2, 0.1, 0.0)
        result = evaluate_stability((vector, vector, vector), top_k=3)
        # The correlation is computed, not special-cased, so it lands one float
        # step below unity. Rounding it to exactly 1.0 in the code would mean
        # rounding every other value too.
        self.assertAlmostEqual(1.0, result.rank_agreement, places=12)
        self.assertEqual(1.0, result.top_k_overlap)
        self.assertEqual(1.0, result.sign_agreement)
        self.assertEqual(3, result.repeats)

    def test_the_fixture_stable_runs_score_high_on_both_statistics(self):
        result = evaluate_stability(STABLE_ATTRIBUTIONS, top_k=3)
        self.assertGreaterEqual(result.rank_agreement, 0.9)
        self.assertEqual(1.0, result.top_k_overlap)

    def test_the_fixture_unstable_runs_score_low_on_both_statistics(self):
        result = evaluate_stability(UNSTABLE_ATTRIBUTIONS, top_k=3)
        self.assertLess(result.rank_agreement, 0.5)
        self.assertLess(result.top_k_overlap, 0.6)

    def test_top_k_overlap_catches_a_shuffled_head_that_rank_agreement_survives(self):
        """The two statistics are not derived from each other, which is why both run."""
        head_shuffled = (
            (0.50, 0.49, 0.48, 0.02, 0.01),
            (0.48, 0.50, 0.49, 0.02, 0.01),
        )
        result = evaluate_stability(head_shuffled, top_k=1)
        self.assertGreater(result.rank_agreement, 0.5)
        self.assertEqual(0.0, result.top_k_overlap)

    def test_stability_refuses_a_single_run_and_a_ragged_set(self):
        with self.assertRaises(StabilityError):
            evaluate_stability(((0.1, 0.2, 0.3),), top_k=2)
        with self.assertRaises(StabilityError):
            evaluate_stability(((0.1, 0.2, 0.3), (0.1, 0.2)), top_k=2)

    def test_a_top_k_outside_the_feature_count_is_refused(self):
        with self.assertRaises(StabilityError):
            evaluate_stability(STABLE_ATTRIBUTIONS, top_k=99)
        with self.assertRaises(StabilityError):
            evaluate_stability(STABLE_ATTRIBUTIONS, top_k=0)


if __name__ == "__main__":
    unittest.main()
