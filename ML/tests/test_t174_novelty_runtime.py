"""T-174: known ranking, honest refusal, and the T-168 evidence shape.

The ranking tests use a population whose outliers are placed by construction, so the
expected answer is known before the model runs rather than read off afterwards.
"""

import json
import os
import shutil
import tempfile
import unittest

from ppiq_ml.models.mf03_novelty import (
    BASELINE_MODEL_CODE,
    CANDIDATE_MODEL_CODE,
    EVALUATION_ARTIFACT_NAME,
    MF03_MODEL_FAMILY,
    MIN_DISTINCT_REFERENCE_UNITS,
    MIN_REFERENCE_UNITS,
    ModelClass,
    NeighbourDensityCandidate,
    NoveltyContractError,
    NoveltyRefusalCode,
    RobustDeviationBaseline,
    build_job_parameters,
    evaluate_eligibility,
    median,
    median_absolute_deviation,
    reference_quantile_threshold,
    run_mf03,
)
from ppiq_ml.runtime import (
    MANIFEST_FILENAME,
    JobOutcome,
    JobSpec,
    RefusalCode,
    ResourceBudget,
    run,
)
from ppiq_ml.runtime.protocol import PROTOCOL_ID

FEATURES = ("measurement_alpha", "measurement_beta", "measurement_gamma")
OUTLIER_IDS = ("unit_outlier_a", "unit_outlier_b", "unit_outlier_c")


def normal_and_outlier_population(normal_units=90, seed=20260815):
    """A tight normal group plus three units placed far outside it, by construction.

    The identifiers of the planted units are known before anything is scored, so the
    expected ranking is a statement made in advance rather than a description of
    whatever the model happened to return.
    """
    state = seed
    ids, rows = [], []
    for index in range(normal_units):
        values = []
        for _ in range(len(FEATURES)):
            state = (state * 1103515245 + 12345) & 0x7FFFFFFF
            values.append(10.0 + (state / 0x7FFFFFFF) * 0.8)
        ids.append(f"unit_{index:05d}")
        rows.append(values)

    rows.append([40.0, 10.4, 10.4])
    rows.append([10.4, 40.0, 10.4])
    rows.append([10.4, 10.4, 40.0])
    ids.extend(OUTLIER_IDS)
    return ids, rows


class TheSupportingMathematicsIsCertified(unittest.TestCase):
    def test_the_median_of_an_odd_and_an_even_set(self):
        self.assertEqual(3.0, median([5.0, 1.0, 3.0]))
        self.assertEqual(2.5, median([1.0, 2.0, 3.0, 4.0]))

    def test_the_median_absolute_deviation_of_a_known_set(self):
        values = [1.0, 2.0, 3.0, 4.0, 100.0]
        self.assertEqual(3.0, median(values))
        self.assertEqual(1.0, median_absolute_deviation(values, 3.0))

    def test_the_median_is_not_moved_by_the_units_the_model_must_find(self):
        """The property that makes a robust statistic the right floor here."""
        clean = [10.0] * 20
        contaminated = clean + [900.0, 950.0]
        self.assertEqual(10.0, median(contaminated))
        self.assertEqual(
            sum(clean) / len(clean), sum(clean) / len(clean)
        )
        self.assertGreater(sum(contaminated) / len(contaminated), 70.0)

    def test_the_threshold_is_a_value_some_unit_actually_scored(self):
        scores = [1.0, 2.0, 3.0, 4.0, 5.0]
        self.assertIn(reference_quantile_threshold(scores, 0.80), scores)
        self.assertEqual(4.0, reference_quantile_threshold(scores, 0.80))
        self.assertEqual(5.0, reference_quantile_threshold(scores, 0.95))

    def test_a_quantile_outside_the_open_interval_is_refused(self):
        for bad in (0.0, 1.0, -0.5, 1.5):
            with self.assertRaises(NoveltyContractError):
                reference_quantile_threshold([1.0, 2.0], bad)


class BothFamiliesRankThePlantedUnitsFirst(unittest.TestCase):
    def setUp(self):
        self.ids, self.rows = normal_and_outlier_population()

    def evaluate(self, model):
        return model.evaluate(self.ids, self.rows, FEATURES, quantile=0.95, seed=7)

    def test_the_mandatory_baseline_puts_all_three_planted_units_on_top(self):
        result = self.evaluate(RobustDeviationBaseline())
        self.assertFalse(result.refusal.refused)
        self.assertEqual(set(OUTLIER_IDS), {u.unit_id for u in result.scored_units[:3]})

    def test_the_density_candidate_puts_all_three_planted_units_on_top(self):
        result = self.evaluate(NeighbourDensityCandidate())
        self.assertFalse(result.refusal.refused)
        self.assertEqual(set(OUTLIER_IDS), {u.unit_id for u in result.scored_units[:3]})

    def test_both_families_flag_the_planted_units_above_the_threshold(self):
        for model in (RobustDeviationBaseline(), NeighbourDensityCandidate()):
            result = self.evaluate(model)
            flagged = {u.unit_id for u in result.flagged}
            self.assertTrue(set(OUTLIER_IDS).issubset(flagged), model.model_code)

    def test_a_planted_unit_scores_far_above_the_ordinary_ones(self):
        result = self.evaluate(RobustDeviationBaseline())
        by_id = {u.unit_id: u.score for u in result.scored_units}
        ordinary = [s for i, s in by_id.items() if i not in OUTLIER_IDS]
        self.assertGreater(min(by_id[i] for i in OUTLIER_IDS), max(ordinary))

    def test_ranks_run_from_zero_and_scores_never_increase(self):
        result = self.evaluate(NeighbourDensityCandidate())
        self.assertEqual(list(range(len(self.ids))), [u.rank for u in result.scored_units])
        scores = [u.score for u in result.scored_units]
        self.assertEqual(scores, sorted(scores, reverse=True))

    def test_the_two_families_declare_different_classes(self):
        self.assertEqual(
            ModelClass.MANDATORY_SIMPLE_BASELINE, RobustDeviationBaseline().model_class
        )
        self.assertEqual(ModelClass.CANDIDATE, NeighbourDensityCandidate().model_class)


class TheFourPartsOfAnAnswerStaySeparate(unittest.TestCase):
    def setUp(self):
        self.ids, self.rows = normal_and_outlier_population()
        self.result = RobustDeviationBaseline().evaluate(
            self.ids, self.rows, FEATURES, quantile=0.95, seed=7
        )

    def test_the_threshold_cites_the_scores_it_was_taken_from(self):
        self.assertEqual("reference_quantile", self.result.threshold.method)
        self.assertEqual(0.95, self.result.threshold.quantile)
        self.assertEqual(64, len(self.result.threshold.reference_score_identity))

    def test_the_population_context_records_what_the_reference_was(self):
        context = self.result.population
        self.assertEqual(len(self.ids), context.reference_units)
        self.assertEqual(FEATURES, context.declared_features)
        self.assertEqual(tuple(FEATURES), context.used_features)
        self.assertEqual((), context.excluded_features)
        self.assertEqual(64, len(context.reference_identity))

    def test_a_scored_result_carries_an_unrefused_refusal_state(self):
        self.assertFalse(self.result.refusal.refused)
        self.assertEqual(NoveltyRefusalCode.NONE, self.result.refusal.code)

    def test_a_result_may_not_carry_both_a_refusal_and_scores(self):
        from ppiq_ml.models.mf03_novelty import NoveltyResult, RefusalState, ScoredUnit

        with self.assertRaises(NoveltyContractError) as raised:
            NoveltyResult(
                model_code="anything",
                model_class=ModelClass.CANDIDATE,
                scored_units=(ScoredUnit("unit_one", 1.0, 0, True),),
                threshold=None,
                population=None,
                refusal=RefusalState(
                    True, NoveltyRefusalCode.TOO_FEW_REFERENCE_UNITS, "too small"
                ),
                description={},
            )
        self.assertIn("may not carry scores", str(raised.exception))

    def test_a_scored_result_may_not_omit_its_threshold(self):
        from ppiq_ml.models.mf03_novelty import NOT_REFUSED, NoveltyResult

        with self.assertRaises(NoveltyContractError) as raised:
            NoveltyResult(
                model_code="anything",
                model_class=ModelClass.CANDIDATE,
                scored_units=(),
                threshold=None,
                population=None,
                refusal=NOT_REFUSED,
                description={},
            )
        self.assertIn("not an answer to anything", str(raised.exception))


class AnUnsupportablePopulationIsRefusedNotScored(unittest.TestCase):
    """The central falsification: no fabricated novelty score, ever."""

    def small_population(self):
        ids = [f"unit_{i:03d}" for i in range(8)]
        rows = [[float(i), float(i) * 2.0, 1.0 + i] for i in range(8)]
        return ids, rows

    def degenerate_population(self):
        ids = [f"unit_{i:03d}" for i in range(60)]
        rows = [[4.0, 9.0, 2.0] for _ in range(60)]
        return ids, rows

    def repetitive_population(self):
        ids = [f"unit_{i:03d}" for i in range(60)]
        rows = [[float(i % 4), float(i % 4) + 1.0, 3.0 + (i % 4)] for i in range(60)]
        return ids, rows

    def test_a_population_below_the_unit_floor_is_refused_by_both_families(self):
        ids, rows = self.small_population()
        for model in (RobustDeviationBaseline(), NeighbourDensityCandidate()):
            result = model.evaluate(ids, rows, FEATURES, quantile=0.95, seed=7)
            self.assertTrue(result.refusal.refused, model.model_code)
            self.assertEqual(
                NoveltyRefusalCode.TOO_FEW_REFERENCE_UNITS, result.refusal.code
            )
            self.assertEqual((), result.scored_units)
            self.assertIsNone(result.threshold)
            self.assertEqual(float(MIN_REFERENCE_UNITS), result.refusal.required)
            self.assertEqual(8.0, result.refusal.observed)

    def test_a_population_with_no_varying_feature_is_refused(self):
        ids, rows = self.degenerate_population()
        result = RobustDeviationBaseline().evaluate(ids, rows, FEATURES, 0.95, 7)
        self.assertTrue(result.refusal.refused)
        self.assertEqual(NoveltyRefusalCode.DEGENERATE_POPULATION, result.refusal.code)
        self.assertEqual((), result.scored_units)
        self.assertIn("no dimension along which", result.refusal.reason)
        self.assertEqual(3, len(result.population.excluded_features))

    def test_a_population_of_a_few_repeated_rows_is_refused(self):
        ids, rows = self.repetitive_population()
        result = RobustDeviationBaseline().evaluate(ids, rows, FEATURES, 0.95, 7)
        self.assertTrue(result.refusal.refused)
        self.assertEqual(NoveltyRefusalCode.TOO_FEW_DISTINCT_UNITS, result.refusal.code)
        self.assertEqual((), result.scored_units)
        self.assertEqual(4.0, result.refusal.observed)
        self.assertEqual(float(MIN_DISTINCT_REFERENCE_UNITS), result.refusal.required)

    def test_a_refused_result_still_records_the_population_it_judged(self):
        ids, rows = self.degenerate_population()
        result = RobustDeviationBaseline().evaluate(ids, rows, FEATURES, 0.95, 7)
        self.assertEqual(60, result.population.reference_units)
        self.assertEqual(1, result.population.distinct_reference_units)
        self.assertEqual(64, len(result.population.reference_identity))

    def test_one_constant_feature_is_excluded_rather_than_refusing_the_whole_run(self):
        ids, rows = normal_and_outlier_population()
        widened = [list(row) + [7.0] for row in rows]
        names = tuple(FEATURES) + ("measurement_constant",)
        result = RobustDeviationBaseline().evaluate(ids, widened, names, 0.95, 7)
        self.assertFalse(result.refusal.refused)
        self.assertEqual(("measurement_constant",),
                         tuple(e.feature for e in result.population.excluded_features))
        self.assertEqual(tuple(FEATURES), result.population.used_features)

    def test_structural_violations_are_errors_rather_than_honest_refusals(self):
        ids, rows = normal_and_outlier_population()
        with self.assertRaises(NoveltyContractError):
            RobustDeviationBaseline().evaluate(ids[:-1], rows, FEATURES, 0.95, 7)
        with self.assertRaises(NoveltyContractError):
            RobustDeviationBaseline().evaluate(
                ["same"] * len(rows), rows, FEATURES, 0.95, 7
            )
        with self.assertRaises(NoveltyContractError):
            RobustDeviationBaseline().evaluate(ids, rows, (), 0.95, 7)

    def test_eligibility_reports_the_same_verdict_the_models_act_on(self):
        ids, rows = self.small_population()
        outcome = evaluate_eligibility(ids, rows, FEATURES)
        self.assertFalse(outcome.eligible)
        self.assertEqual(NoveltyRefusalCode.TOO_FEW_REFERENCE_UNITS, outcome.refusal.code)


class TheRuntimeProducesManifestCompatibleEvidence(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t174-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def execute(self, ids, rows, feature_names=FEATURES, family=MF03_MODEL_FAMILY, seed=7):
        outputs = os.path.join(tempfile.mkdtemp(dir=self.root), "outputs")
        spec = JobSpec(
            protocol=PROTOCOL_ID,
            job_id="job-t174",
            tenant_id="tenant-fixture",
            site_id="site-fixture",
            model_family=family,
            inputs=(),
            output_directory=outputs,
            seed=seed,
            code_identity="t174-fixture",
            resources=ResourceBudget(max_wall_clock_seconds=600.0),
            parameters=build_job_parameters(ids, rows, feature_names),
        )
        return run(spec, run_mf03), outputs

    def read_record(self, outputs):
        with open(os.path.join(outputs, EVALUATION_ARTIFACT_NAME), encoding="ascii") as h:
            return json.load(h)

    def test_a_supportable_population_succeeds_and_writes_one_evidence_artifact(self):
        ids, rows = normal_and_outlier_population()
        manifest, outputs = self.execute(ids, rows)
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        self.assertEqual("Finding", manifest.analysis_terminal_state)
        self.assertEqual(1, len(manifest.artifacts))
        self.assertEqual("evaluation", manifest.artifacts[0].artifact_kind)

    def test_the_baseline_is_evaluated_before_the_candidate(self):
        ids, rows = normal_and_outlier_population()
        _, outputs = self.execute(ids, rows)
        record = self.read_record(outputs)
        self.assertEqual(
            [BASELINE_MODEL_CODE, CANDIDATE_MODEL_CODE], record["evaluation_order"]
        )
        self.assertEqual(
            "mandatory_simple_baseline", record["results"][0]["model_class"]
        )
        self.assertEqual("candidate", record["results"][1]["model_class"])

    def test_the_record_keeps_the_four_parts_apart_for_every_family(self):
        ids, rows = normal_and_outlier_population()
        _, outputs = self.execute(ids, rows)
        for result in self.read_record(outputs)["results"]:
            for part in ("scored_units", "threshold", "population", "refusal"):
                self.assertIn(part, result)
            self.assertFalse(result["refusal"]["refused"])
            self.assertIsNotNone(result["threshold"])

    def test_the_manifest_metrics_are_strict_json_with_no_non_finite_value(self):
        ids, rows = normal_and_outlier_population()
        manifest, _ = self.execute(ids, rows)

        def reject(name):
            raise AssertionError(f"The manifest carries the non-finite value '{name}'.")

        json.loads(manifest.to_json(), parse_constant=reject)
        for key in ("baseline.threshold", "candidate.threshold", "population.reference_units"):
            self.assertIn(key, manifest.metrics)

    def test_a_too_small_population_refuses_and_writes_no_evidence_at_all(self):
        ids = [f"unit_{i:03d}" for i in range(8)]
        rows = [[float(i), float(i) * 2.0, 1.0 + i] for i in range(8)]
        manifest, outputs = self.execute(ids, rows)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.ELIGIBILITY_NOT_MET.value, manifest.refusal_code)
        self.assertIn("arithmetic rather than evidence", manifest.refusal_reason)
        self.assertEqual((), manifest.artifacts)
        self.assertEqual([MANIFEST_FILENAME], sorted(os.listdir(outputs)))

    def test_a_degenerate_population_refuses_and_writes_no_evidence_at_all(self):
        ids = [f"unit_{i:03d}" for i in range(60)]
        rows = [[4.0, 9.0, 2.0] for _ in range(60)]
        manifest, outputs = self.execute(ids, rows)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.ELIGIBILITY_NOT_MET.value, manifest.refusal_code)
        self.assertEqual((), manifest.artifacts)
        self.assertEqual([MANIFEST_FILENAME], sorted(os.listdir(outputs)))

    def test_a_job_for_another_family_is_refused_before_anything_is_read(self):
        ids, rows = normal_and_outlier_population()
        manifest, _ = self.execute(ids, rows, family="mf04_supervised")
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.UNSUPPORTED_MODEL_FAMILY.value, manifest.refusal_code)

    def test_a_job_without_a_reference_population_is_refused(self):
        outputs = os.path.join(tempfile.mkdtemp(dir=self.root), "outputs")
        spec = JobSpec(
            protocol=PROTOCOL_ID, job_id="job-t174-bare", tenant_id="t", site_id="s",
            model_family=MF03_MODEL_FAMILY, inputs=(), output_directory=outputs,
            seed=7, code_identity="t174-fixture",
            resources=ResourceBudget(max_wall_clock_seconds=600.0), parameters={},
        )
        manifest = run(spec, run_mf03)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertIn("nothing to measure against", manifest.refusal_reason)

    def test_two_runs_on_identical_input_produce_identical_evidence(self):
        ids, rows = normal_and_outlier_population()
        first, _ = self.execute(ids, rows)
        second, _ = self.execute(ids, rows)
        self.assertEqual(
            [a.content_hash for a in first.artifacts],
            [a.content_hash for a in second.artifacts],
        )

    def test_the_record_carries_the_environment_and_the_seed(self):
        ids, rows = normal_and_outlier_population()
        _, outputs = self.execute(ids, rows, seed=99)
        record = self.read_record(outputs)
        self.assertEqual(99, record["environment"]["seed"])
        self.assertIn("python_version", record["environment"])
        self.assertEqual("fixture_declared_typed_contract",
                         record["reference_population_source"])

    def test_an_excluded_feature_is_reported_as_a_warning_not_hidden(self):
        ids, rows = normal_and_outlier_population()
        widened = [list(row) + [7.0] for row in rows]
        manifest, _ = self.execute(
            ids, widened, tuple(FEATURES) + ("measurement_constant",)
        )
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        self.assertEqual(1, len(manifest.warnings))
        self.assertIn("measurement_constant", manifest.warnings[0])


if __name__ == "__main__":
    unittest.main()
