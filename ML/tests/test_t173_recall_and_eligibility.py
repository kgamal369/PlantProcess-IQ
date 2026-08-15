"""T-173 part two: the candidate measured against the oracle, and the recall floor.

Every recall figure in this file is computed against an exact index built on the same
vectors in the same run. None of them is a declared constant.
"""

import unittest

from ppiq_ml.similarity import (
    ExactFlatIndex,
    IndexContractError,
    Metric,
    PartitionedProbeIndex,
    RecallReport,
    ServingEligibility,
    percentile,
    recall_probe,
)
from tests.test_t173_index_contract import synthetic_population


def clustered_population(clusters=6, per_cluster=25, dimension=10, seed=99):
    """Vectors that genuinely group, so a partitioned index has something to find.

    Uniform noise would make every cell arbitrary and a recall figure would then be
    measuring the fixture rather than the index.
    """
    state = seed
    ids, vectors = [], []
    centres = []
    for cluster in range(clusters):
        centre = []
        for _ in range(dimension):
            state = (state * 1103515245 + 12345) & 0x7FFFFFFF
            centre.append(state / 0x7FFFFFFF * 4.0 - 2.0)
        centres.append(centre)
    for cluster, centre in enumerate(centres):
        for member in range(per_cluster):
            components = []
            for axis in range(dimension):
                state = (state * 1103515245 + 12345) & 0x7FFFFFFF
                components.append(centre[axis] + (state / 0x7FFFFFFF * 0.3 - 0.15))
            ids.append(f"vector_{cluster:02d}_{member:03d}")
            vectors.append(components)
    return ids, vectors


def build_pair(parameters, ids=None, vectors=None, metric=Metric.COSINE):
    if ids is None:
        ids, vectors = clustered_population()
    oracle = ExactFlatIndex()
    oracle.build(ids, vectors, metric)
    candidate = PartitionedProbeIndex()
    candidate.build(ids, vectors, metric, parameters)
    return oracle, candidate, ids, vectors


class ThePercentileMathematicsIsCertified(unittest.TestCase):
    def test_known_answers_on_a_hundred_ordered_values(self):
        values = [float(v) for v in range(1, 101)]
        self.assertEqual(50.0, percentile(values, 0.50))
        self.assertEqual(95.0, percentile(values, 0.95))
        self.assertEqual(99.0, percentile(values, 0.99))

    def test_a_single_measurement_is_every_percentile(self):
        self.assertEqual(7.0, percentile([7.0], 0.95))

    def test_a_percentile_over_nothing_is_refused(self):
        with self.assertRaises(IndexContractError):
            percentile([], 0.5)


class AWellConfiguredCandidateIsEligible(unittest.TestCase):
    def setUp(self):
        self.oracle, self.candidate, self.ids, self.vectors = build_pair(
            {"cells": 6, "probes": 4}
        )
        self.report = recall_probe(
            self.candidate, self.oracle, self.vectors[:40], k=5, recall_floor=0.90
        )

    def test_it_clears_the_declared_floor_and_may_serve(self):
        self.assertIsInstance(self.report, RecallReport)
        self.assertEqual(ServingEligibility.ELIGIBLE, self.report.eligibility)
        self.assertGreaterEqual(self.report.recall_at_k, 0.90)

    def test_the_report_carries_every_required_measurement(self):
        self.assertEqual(5, self.report.k)
        self.assertGreater(self.report.latency.p95_ms, 0.0)
        self.assertGreaterEqual(self.report.latency.p99_ms, self.report.latency.p50_ms)
        self.assertGreater(self.report.build_seconds, 0.0)
        self.assertGreater(self.report.peak_build_bytes, 0)
        self.assertEqual(40, self.report.latency.queries)
        self.assertEqual(40, len(self.report.per_query_recall))

    def test_the_report_names_both_generations_and_the_shared_vectors(self):
        self.assertEqual(self.oracle.manifest.generation_id, self.report.oracle_generation_id)
        self.assertEqual(
            self.candidate.manifest.generation_id, self.report.candidate_generation_id
        )
        self.assertEqual(
            self.oracle.manifest.vector_content_hash, self.report.vector_content_hash
        )

    def test_probing_every_cell_recovers_the_oracle_exactly(self):
        """The candidate's ceiling is the oracle, which is what makes it a candidate."""
        _, candidate, _, vectors = build_pair({"cells": 5, "probes": 5})
        oracle = ExactFlatIndex()
        ids, all_vectors = clustered_population()
        oracle.build(ids, all_vectors, Metric.COSINE)
        report = recall_probe(candidate, oracle, all_vectors[:30], k=5, recall_floor=0.99)
        self.assertEqual(1.0, report.recall_at_k)
        self.assertEqual(1.0, report.worst_query_recall)


class AWeakenedCandidateIsNotEligibleToServe(unittest.TestCase):
    """The central falsification: a recall floor that never refuses is not a floor."""

    def setUp(self):
        ids, vectors = clustered_population(clusters=12, per_cluster=20)
        self.oracle = ExactFlatIndex()
        self.oracle.build(ids, vectors, Metric.COSINE)
        self.weakened = PartitionedProbeIndex()
        self.weakened.build(ids, vectors, Metric.COSINE, {"cells": 24, "probes": 1})
        self.queries = vectors[:60]

    def test_the_weakened_build_falls_below_the_floor_and_is_refused(self):
        report = recall_probe(
            self.weakened, self.oracle, self.queries, k=10, recall_floor=0.90
        )
        self.assertLess(report.recall_at_k, 0.90)
        self.assertEqual(ServingEligibility.NOT_ELIGIBLE_TO_SERVE, report.eligibility)
        self.assertIn("not eligible to serve", report.reason)

    def test_the_refusal_names_the_generation_and_both_numbers(self):
        report = recall_probe(
            self.weakened, self.oracle, self.queries, k=10, recall_floor=0.90
        )
        self.assertIn(self.weakened.manifest.generation_id[:12], report.reason)
        self.assertIn("0.9000", report.reason)
        self.assertIn(f"{report.recall_at_k:.4f}", report.reason)

    def test_speed_does_not_buy_back_a_failed_recall(self):
        """It is genuinely faster, and it is still refused."""
        strong = PartitionedProbeIndex()
        ids, vectors = clustered_population(clusters=12, per_cluster=20)
        strong.build(ids, vectors, Metric.COSINE, {"cells": 24, "probes": 24})

        weak_report = recall_probe(
            self.weakened, self.oracle, self.queries, k=10, recall_floor=0.90
        )
        strong_report = recall_probe(
            strong, self.oracle, self.queries, k=10, recall_floor=0.90
        )

        self.assertLess(weak_report.latency.p95_ms, strong_report.latency.p95_ms)
        self.assertEqual(ServingEligibility.NOT_ELIGIBLE_TO_SERVE, weak_report.eligibility)
        self.assertEqual(ServingEligibility.ELIGIBLE, strong_report.eligibility)
        self.assertIn("answering a different question", weak_report.reason)

    def test_recall_rises_as_more_cells_are_probed(self):
        """The trade the measurement exists to expose, observed rather than asserted."""
        ids, vectors = clustered_population(clusters=12, per_cluster=20)
        observed = []
        for probes in (1, 3, 8, 24):
            candidate = PartitionedProbeIndex()
            candidate.build(ids, vectors, Metric.COSINE, {"cells": 24, "probes": probes})
            observed.append(
                recall_probe(
                    candidate, self.oracle, self.queries, k=10, recall_floor=0.0
                ).recall_at_k
            )
        self.assertEqual(observed, sorted(observed))
        self.assertLess(observed[0], observed[-1])
        self.assertEqual(1.0, observed[-1])


class TheProbeRefusesAnIncoherentComparison(unittest.TestCase):
    def test_an_approximate_index_may_not_stand_in_for_the_oracle(self):
        _, candidate, ids, vectors = build_pair({"cells": 4, "probes": 2})
        other = PartitionedProbeIndex()
        other.build(ids, vectors, Metric.COSINE, {"cells": 4, "probes": 1})
        with self.assertRaises(IndexContractError) as raised:
            recall_probe(other, candidate, vectors[:5], k=3, recall_floor=0.5)
        self.assertIn("not a recall", str(raised.exception))

    def test_the_oracle_may_not_be_measured_against_itself(self):
        oracle, _, _, vectors = build_pair({"cells": 4, "probes": 2})
        second = ExactFlatIndex()
        ids, all_vectors = clustered_population()
        second.build(ids, all_vectors, Metric.COSINE)
        with self.assertRaises(IndexContractError) as raised:
            recall_probe(second, oracle, vectors[:5], k=3, recall_floor=0.5)
        self.assertIn("means nothing", str(raised.exception))

    def test_two_different_populations_are_never_compared(self):
        oracle = ExactFlatIndex()
        ids, vectors = clustered_population()
        oracle.build(ids, vectors, Metric.COSINE)

        other_ids, other_vectors = synthetic_population(count=150, dimension=10)
        candidate = PartitionedProbeIndex()
        candidate.build(other_ids, other_vectors, Metric.COSINE, {"cells": 4, "probes": 2})

        with self.assertRaises(IndexContractError) as raised:
            recall_probe(candidate, oracle, vectors[:5], k=3, recall_floor=0.5)
        self.assertIn("different vectors", str(raised.exception))

    def test_two_different_metrics_are_never_compared(self):
        ids, vectors = clustered_population()
        oracle = ExactFlatIndex()
        oracle.build(ids, vectors, Metric.COSINE)
        candidate = PartitionedProbeIndex()
        candidate.build(ids, vectors, Metric.EUCLIDEAN, {"cells": 4, "probes": 2})
        with self.assertRaises(IndexContractError) as raised:
            recall_probe(candidate, oracle, vectors[:5], k=3, recall_floor=0.5)
        self.assertIn("different questions", str(raised.exception))

    def test_a_probe_with_no_queries_is_refused(self):
        oracle, candidate, _, _ = build_pair({"cells": 4, "probes": 2})
        with self.assertRaises(IndexContractError):
            recall_probe(candidate, oracle, [], k=3, recall_floor=0.5)


class TheCandidateIsReplaceableAndBoundedByTheContract(unittest.TestCase):
    def test_probing_more_cells_than_exist_is_refused_as_disguised_exact_search(self):
        ids, vectors = clustered_population()
        with self.assertRaises(IndexContractError) as raised:
            PartitionedProbeIndex().build(
                ids, vectors, Metric.COSINE, {"cells": 4, "probes": 9}
            )
        self.assertIn("wearing a candidate's name", str(raised.exception))

    def test_a_search_that_scans_nothing_is_refused(self):
        ids, vectors = clustered_population()
        with self.assertRaises(IndexContractError) as raised:
            PartitionedProbeIndex().build(
                ids, vectors, Metric.COSINE, {"cells": 4, "probes": 0}
            )
        self.assertIn("call it an answer", str(raised.exception))

    def test_the_partitioning_is_deterministic_across_builds(self):
        ids, vectors = clustered_population()
        first, second = PartitionedProbeIndex(), PartitionedProbeIndex()
        first.build(ids, vectors, Metric.COSINE, {"cells": 6, "probes": 2})
        second.build(ids, vectors, Metric.COSINE, {"cells": 6, "probes": 2})
        self.assertEqual(first.cell_sizes, second.cell_sizes)
        self.assertEqual(first.manifest.generation_id, second.manifest.generation_id)
        self.assertEqual(
            first.search([vectors[0]], k=5)[0].evidence_handle,
            second.search([vectors[0]], k=5)[0].evidence_handle,
        )

    def test_every_vector_lands_in_exactly_one_cell(self):
        ids, vectors = clustered_population()
        candidate = PartitionedProbeIndex()
        candidate.build(ids, vectors, Metric.COSINE, {"cells": 6, "probes": 2})
        self.assertEqual(len(ids), sum(candidate.cell_sizes))

    def test_the_candidate_honours_the_same_immutability_rule_as_the_oracle(self):
        ids, vectors = clustered_population()
        candidate = PartitionedProbeIndex()
        parent_id = candidate.build(
            ids, vectors, Metric.COSINE, {"cells": 6, "probes": 2}
        ).generation_id
        child = candidate.extend(["vector_extra"], [[0.1] * len(vectors[0])])
        self.assertEqual(parent_id, candidate.manifest.generation_id)
        self.assertEqual(parent_id, child.manifest.parent_generation_id)
        self.assertEqual(len(ids) + 1, child.manifest.vector_count)


if __name__ == "__main__":
    unittest.main()
