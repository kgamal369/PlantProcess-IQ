"""T-173 part one: the contract, the exact oracle, and sealed generation identity."""

import math
import unittest

from ppiq_ml.similarity import (
    EXACT_FLAT_KIND,
    ExactFlatIndex,
    IndexContractError,
    IndexNotBuiltError,
    IndexSealedError,
    Metric,
    PartitionedProbeIndex,
    normalise,
    similarity,
)


def synthetic_population(count=120, dimension=8, seed=20260815):
    """Deterministic vectors with no dependency on any generator implementation."""
    state = seed
    ids, vectors = [], []
    for index in range(count):
        components = []
        for _ in range(dimension):
            state = (state * 1103515245 + 12345) & 0x7FFFFFFF
            components.append(state / 0x7FFFFFFF * 2.0 - 1.0)
        ids.append(f"vector_{index:05d}")
        vectors.append(components)
    return ids, vectors


class TheMetricMathematicsIsCertifiedAgainstKnownAnswers(unittest.TestCase):
    def test_a_vector_is_perfectly_similar_to_itself_under_cosine(self):
        self.assertAlmostEqual(1.0, similarity(Metric.COSINE, [3.0, 4.0], [3.0, 4.0]), places=12)

    def test_opposed_directions_are_minus_one_and_orthogonal_ones_are_zero(self):
        self.assertAlmostEqual(-1.0, similarity(Metric.COSINE, [1.0, 0.0], [-2.0, 0.0]), places=12)
        self.assertAlmostEqual(0.0, similarity(Metric.COSINE, [1.0, 0.0], [0.0, 5.0]), places=12)

    def test_cosine_ignores_length_and_euclidean_does_not(self):
        self.assertAlmostEqual(1.0, similarity(Metric.COSINE, [1.0, 1.0], [9.0, 9.0]), places=12)
        self.assertAlmostEqual(
            -math.sqrt(128.0), similarity(Metric.EUCLIDEAN, [1.0, 1.0], [9.0, 9.0]), places=12
        )

    def test_euclidean_is_negated_so_larger_always_means_closer(self):
        near = similarity(Metric.EUCLIDEAN, [0.0, 0.0], [1.0, 0.0])
        far = similarity(Metric.EUCLIDEAN, [0.0, 0.0], [5.0, 0.0])
        self.assertGreater(near, far)

    def test_normalising_produces_unit_length(self):
        self.assertAlmostEqual(1.0, math.sqrt(sum(v * v for v in normalise([3.0, 4.0]))), places=12)

    def test_a_vector_with_no_length_is_refused_rather_than_treated_as_close(self):
        with self.assertRaises(IndexContractError) as raised:
            normalise([0.0, 0.0, 0.0])
        self.assertIn("no direction", str(raised.exception))


class TheExactIndexIsTheOracle(unittest.TestCase):
    def setUp(self):
        self.ids, self.vectors = synthetic_population()
        self.index = ExactFlatIndex()
        self.manifest = self.index.build(self.ids, self.vectors, Metric.COSINE)

    def test_it_declares_itself_exact_and_the_candidate_does_not(self):
        self.assertTrue(self.index.is_exact)
        self.assertEqual(EXACT_FLAT_KIND, self.index.index_kind)
        self.assertFalse(PartitionedProbeIndex().is_exact)

    def test_a_stored_vector_is_its_own_nearest_neighbour(self):
        """The property that makes this an oracle rather than a good implementation."""
        for position in (0, 17, 63, 119):
            result = self.index.search([self.vectors[position]], k=1)[0]
            self.assertEqual(self.ids[position], result.hits[0].vector_id)
            self.assertAlmostEqual(1.0, result.hits[0].score, places=9)

    def test_it_returns_exactly_k_neighbours_ranked_from_zero(self):
        result = self.index.search([self.vectors[3]], k=5)[0]
        self.assertEqual(5, len(result.hits))
        self.assertEqual([0, 1, 2, 3, 4], [h.rank for h in result.hits])

    def test_scores_never_increase_down_the_ranking(self):
        result = self.index.search([self.vectors[9]], k=10)[0]
        scores = [h.score for h in result.hits]
        self.assertEqual(scores, sorted(scores, reverse=True))

    def test_it_agrees_with_a_hand_computed_ordering_on_a_tiny_population(self):
        index = ExactFlatIndex()
        index.build(
            ["north", "east", "north_east"],
            [[0.0, 1.0], [1.0, 0.0], [1.0, 1.0]],
            Metric.COSINE,
        )
        result = index.search([[0.0, 2.0]], k=3)[0]
        self.assertEqual(("north", "north_east", "east"), result.vector_ids)

    def test_ties_are_broken_by_identifier_so_the_answer_is_reproducible(self):
        index = ExactFlatIndex()
        index.build(
            ["zulu", "alpha", "mike"],
            [[1.0, 0.0], [1.0, 0.0], [1.0, 0.0]],
            Metric.COSINE,
        )
        first = index.search([[1.0, 0.0]], k=3)[0]
        self.assertEqual(("alpha", "mike", "zulu"), first.vector_ids)

    def test_asking_for_more_neighbours_than_exist_returns_what_exists(self):
        index = ExactFlatIndex()
        index.build(["one", "two"], [[1.0, 0.0], [0.0, 1.0]], Metric.COSINE)
        self.assertEqual(2, len(index.search([[1.0, 1.0]], k=50)[0].hits))

    def test_euclidean_and_cosine_are_different_generations_of_the_same_vectors(self):
        other = ExactFlatIndex()
        other_manifest = other.build(self.ids, self.vectors, Metric.EUCLIDEAN)
        self.assertEqual(
            self.manifest.vector_content_hash, other_manifest.vector_content_hash
        )
        self.assertNotEqual(self.manifest.generation_id, other_manifest.generation_id)


class AGenerationIsSealedAndImmutable(unittest.TestCase):
    def setUp(self):
        self.ids, self.vectors = synthetic_population(count=60)

    def build(self, **overrides):
        index = ExactFlatIndex()
        index.build(
            overrides.get("ids", self.ids),
            overrides.get("vectors", self.vectors),
            overrides.get("metric", Metric.COSINE),
            overrides.get("parameters", None),
        )
        return index

    def test_searching_before_a_build_is_refused_rather_than_empty(self):
        with self.assertRaises(IndexNotBuiltError):
            ExactFlatIndex().search([[1.0, 0.0]], k=1)
        with self.assertRaises(IndexNotBuiltError):
            ExactFlatIndex().manifest

    def test_building_twice_on_one_index_is_refused(self):
        index = self.build()
        with self.assertRaises(IndexSealedError) as raised:
            index.build(self.ids, self.vectors, Metric.COSINE)
        self.assertIn("already cites", str(raised.exception))

    def test_repeating_a_build_of_the_same_vectors_yields_the_same_identity(self):
        self.assertEqual(
            self.build().manifest.generation_id, self.build().manifest.generation_id
        )

    def test_one_changed_vector_produces_a_different_generation(self):
        altered = [list(v) for v in self.vectors]
        altered[7][0] += 0.001
        self.assertNotEqual(
            self.build().manifest.generation_id,
            self.build(vectors=altered).manifest.generation_id,
        )

    def test_a_different_vector_order_produces_a_different_generation(self):
        reversed_ids = list(reversed(self.ids))
        reversed_vectors = list(reversed(self.vectors))
        self.assertNotEqual(
            self.build().manifest.generation_id,
            self.build(ids=reversed_ids, vectors=reversed_vectors).manifest.generation_id,
        )

    def test_different_parameters_produce_different_generations(self):
        self.assertNotEqual(
            self.build(parameters={"cells": 4}).manifest.generation_id,
            self.build(parameters={"cells": 8}).manifest.generation_id,
        )

    def test_the_identity_does_not_move_with_build_duration_or_memory(self):
        """Two builds on one machine differ in timing and must not differ in identity."""
        first, second = self.build().manifest, self.build().manifest
        self.assertEqual(first.identity_inputs(), second.identity_inputs())
        self.assertNotIn("build_seconds", first.identity_inputs())
        self.assertNotIn("peak_build_bytes", first.identity_inputs())
        self.assertEqual(first.generation_id, second.generation_id)

    def test_extending_produces_a_new_generation_and_leaves_the_old_one_searchable(self):
        parent = self.build()
        parent_id = parent.manifest.generation_id
        parent_answer = parent.search([self.vectors[0]], k=3)[0]

        child = parent.extend(["vector_extra"], [[0.5] * len(self.vectors[0])])

        self.assertEqual(parent_id, parent.manifest.generation_id)
        self.assertNotEqual(parent_id, child.manifest.generation_id)
        self.assertEqual(parent_id, child.manifest.parent_generation_id)
        self.assertEqual(len(self.ids) + 1, child.manifest.vector_count)
        self.assertEqual(len(self.ids), parent.manifest.vector_count)
        self.assertEqual(
            parent_answer.evidence_handle,
            parent.search([self.vectors[0]], k=3)[0].evidence_handle,
        )

    def test_a_search_evidence_handle_is_stable_across_repeats(self):
        index = self.build()
        first = index.search([self.vectors[4]], k=5)[0]
        second = index.search([self.vectors[4]], k=5)[0]
        self.assertEqual(first.evidence_handle, second.evidence_handle)
        self.assertEqual(index.manifest.generation_id, first.generation_id)

    def test_a_handle_changes_with_the_query_the_depth_or_the_generation(self):
        index = self.build()
        base = index.search([self.vectors[4]], k=5)[0].evidence_handle
        self.assertNotEqual(base, index.search([self.vectors[5]], k=5)[0].evidence_handle)
        self.assertNotEqual(base, index.search([self.vectors[4]], k=4)[0].evidence_handle)
        other = self.build(metric=Metric.EUCLIDEAN)
        self.assertNotEqual(base, other.search([self.vectors[4]], k=5)[0].evidence_handle)


class ThePopulationContractIsEnforced(unittest.TestCase):
    def test_mismatched_identifier_and_vector_counts_are_refused(self):
        with self.assertRaises(IndexContractError):
            ExactFlatIndex().build(["a"], [[1.0], [2.0]])

    def test_an_empty_population_is_refused(self):
        with self.assertRaises(IndexContractError):
            ExactFlatIndex().build([], [])

    def test_a_repeated_identifier_is_refused_because_recall_would_be_meaningless(self):
        with self.assertRaises(IndexContractError) as raised:
            ExactFlatIndex().build(["a", "a"], [[1.0], [2.0]])
        self.assertIn("same neighbour", str(raised.exception))

    def test_a_ragged_dimension_is_refused(self):
        with self.assertRaises(IndexContractError):
            ExactFlatIndex().build(["a", "b"], [[1.0, 2.0], [1.0]])

    def test_a_search_for_fewer_than_one_neighbour_is_refused(self):
        index = ExactFlatIndex()
        index.build(["a", "b"], [[1.0, 0.0], [0.0, 1.0]])
        with self.assertRaises(IndexNotBuiltError):
            index.search([[1.0, 0.0]], k=0)


if __name__ == "__main__":
    unittest.main()
