"""T-175 part two: the training runtime, executed for real through the T-168 protocol.

Every test here writes a sealed artifact with a T-169 adapter, builds a job spec and
calls the protocol runner. Nothing is stubbed except the one case that proves an
absent library is reported as an unavailable installation rather than a crash.
"""

import json
import os
import shutil
import tempfile
import unittest

from ppiq_ml.artifacts import enabled_adapters
from ppiq_ml.models.mf04_supervised import (
    BASELINE_ARTIFACT_NAME,
    CANDIDATE_ARTIFACT_NAME,
    EVALUATION_ARTIFACT_NAME,
    MF04_MODEL_FAMILY,
    ModelUnavailableError,
    OutcomeKind,
    build_job_parameters,
    run_mf04,
)
from ppiq_ml.models.mf04_supervised import candidate as candidate_module
from ppiq_ml.runtime import (
    MANIFEST_FILENAME,
    ArtifactRef,
    JobOutcome,
    JobSpec,
    RefusalCode,
    ResourceBudget,
    run,
)
from ppiq_ml.runtime.protocol import PROTOCOL_ID
from tests.mf04_population_fixture import build_outcome, seal_population


def _reject_constant(name):
    raise AssertionError(
        f"The manifest carries the non-finite value '{name}', which is not valid JSON "
        "and which the .NET side cannot parse."
    )


class Mf04RuntimeCase(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t175-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def execute(
        self,
        kind=OutcomeKind.BINARY,
        include_after_cutoff_feature=False,
        artifact_format="parquet",
        units=200,
        outcome=None,
        model_family=MF04_MODEL_FAMILY,
        directory=None,
        seed=7,
        rows=None,
    ):
        directory = directory or tempfile.mkdtemp(dir=self.root)
        inputs = os.path.join(directory, "inputs")
        outputs = os.path.join(directory, "outputs")
        os.makedirs(inputs, exist_ok=True)

        descriptor = seal_population(
            inputs, kind, artifact_format=artifact_format, units=units, rows=rows
        )
        declared = outcome or build_outcome(kind, include_after_cutoff_feature)

        spec = JobSpec(
            protocol=PROTOCOL_ID,
            job_id="job-t175",
            tenant_id="tenant-fixture",
            site_id="site-fixture",
            model_family=model_family,
            inputs=(
                ArtifactRef(
                    artifact_id=descriptor.artifact_id,
                    uri=descriptor.uri,
                    content_hash=descriptor.artifact_byte_hash,
                    artifact_format=descriptor.artifact_format,
                    byte_size=descriptor.byte_size,
                ),
            ),
            output_directory=outputs,
            seed=seed,
            code_identity="t175-fixture",
            resources=ResourceBudget(max_wall_clock_seconds=600.0),
            parameters=build_job_parameters(declared),
        )
        manifest = run(spec, run_mf04)
        return manifest, outputs, descriptor

    def read_record(self, outputs):
        with open(os.path.join(outputs, EVALUATION_ARTIFACT_NAME), encoding="ascii") as handle:
            return json.load(handle)


class TheSupervisedRuntimeTrainsBothModels(Mf04RuntimeCase):
    def test_a_binary_outcome_produces_a_baseline_a_candidate_and_an_evidence_record(self):
        manifest, outputs, _ = self.execute()
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        self.assertEqual("Finding", manifest.analysis_terminal_state)
        kinds = sorted(a.artifact_kind for a in manifest.artifacts)
        self.assertEqual(["evaluation", "model", "model"], kinds)
        for name in (BASELINE_ARTIFACT_NAME, CANDIDATE_ARTIFACT_NAME, EVALUATION_ARTIFACT_NAME):
            self.assertTrue(os.path.exists(os.path.join(outputs, name)), name)

    def test_the_baseline_is_trained_before_the_candidate(self):
        _, outputs, _ = self.execute()
        record = self.read_record(outputs)
        self.assertEqual(
            ["mf04.prior_baseline", "mf04.gbdt_tabular"], record["training_order"]
        )
        self.assertEqual("mandatory_simple_baseline", record["models"][0]["model_class"])
        self.assertEqual("candidate", record["models"][1]["model_class"])

    def test_the_floor_scores_exactly_one_half_because_it_predicts_a_constant(self):
        """The known answer that makes every candidate number interpretable."""
        manifest, _, _ = self.execute()
        self.assertAlmostEqual(0.5, manifest.metrics["baseline.auc"], places=12)
        self.assertIn("candidate.auc", manifest.metrics)

    def test_the_snapshot_identity_covers_what_was_read_not_what_was_stored(self):
        """A refused column is not part of the identity of what the model saw.

        The artifact carries a column the leakage gate rejected. Its logical hash
        therefore covers seven columns while the snapshot identity covers the six
        that were projected. Reporting the artifact's own hash here would claim the
        model saw data it was never given.
        """
        _, outputs, descriptor = self.execute()
        identity = self.read_record(outputs)["snapshot"]["snapshot_identity"]
        self.assertEqual(64, len(identity))
        self.assertNotEqual(descriptor.logical_content_hash, identity)
        self.assertEqual(
            descriptor.artifact_byte_hash,
            self.read_record(outputs)["snapshot"]["input_content_hash"],
        )

    def test_both_models_report_the_same_snapshot_and_the_same_holdout(self):
        manifest, outputs, descriptor = self.execute()
        record = self.read_record(outputs)
        comparison = record["comparison"]
        self.assertEqual(record["snapshot"]["snapshot_identity"], comparison["snapshot_identity"])
        self.assertEqual(record["holdout"]["holdout_identity"], comparison["holdout_identity"])
        self.assertEqual(
            record["holdout"]["train_units"] + record["holdout"]["holdout_units"],
            record["snapshot"]["population_units"],
        )
        self.assertEqual(0, len(manifest.warnings))

    def test_a_multiclass_ordinal_outcome_reports_the_rank_distance(self):
        manifest, outputs, _ = self.execute(kind=OutcomeKind.ORDINAL)
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        for prefix in ("baseline", "candidate"):
            self.assertIn(f"{prefix}.log_loss", manifest.metrics)
            self.assertIn(f"{prefix}.mean_absolute_rank_error", manifest.metrics)
            self.assertNotIn(f"{prefix}.auc", manifest.metrics)
        record = self.read_record(outputs)
        self.assertEqual(
            ["band_low", "band_middle", "band_high"],
            record["outcome_definition"]["class_order"],
        )

    def test_a_multiclass_outcome_without_a_declared_rank_omits_the_rank_distance(self):
        manifest, _, _ = self.execute(kind=OutcomeKind.MULTICLASS)
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        self.assertIn("candidate.log_loss", manifest.metrics)
        self.assertNotIn("candidate.mean_absolute_rank_error", manifest.metrics)

    def test_a_continuous_outcome_reports_error_rather_than_discrimination(self):
        manifest, _, _ = self.execute(kind=OutcomeKind.CONTINUOUS)
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        for prefix in ("baseline", "candidate"):
            self.assertIn(f"{prefix}.rmse", manifest.metrics)
            self.assertIn(f"{prefix}.mae", manifest.metrics)
            self.assertNotIn(f"{prefix}.auc", manifest.metrics)
            self.assertNotIn(f"{prefix}.accuracy", manifest.metrics)

    def test_the_manifest_is_strict_json_with_no_non_finite_value(self):
        manifest, _, _ = self.execute()
        json.loads(manifest.to_json(), parse_constant=_reject_constant)


class TheGatesStopTrainingBeforeAnythingIsFitted(Mf04RuntimeCase):
    def test_a_post_cutoff_feature_blocks_training_and_leaves_no_model_behind(self):
        """The central falsification of this task."""
        manifest, outputs, _ = self.execute(include_after_cutoff_feature=True)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.ELIGIBILITY_NOT_MET.value, manifest.refusal_code)
        self.assertIn("measurement_after_cutoff", manifest.refusal_reason)
        self.assertIn("train on future information", manifest.refusal_reason)
        self.assertEqual((), manifest.artifacts)
        self.assertEqual([MANIFEST_FILENAME], sorted(os.listdir(outputs)))

    def test_an_outcome_already_observable_at_the_cutoff_is_refused_as_a_lookup(self):
        declared = build_outcome(OutcomeKind.BINARY)
        lookup = type(declared)(
            outcome_code=declared.outcome_code,
            kind=declared.kind,
            grain_column=declared.grain_column,
            order_column=declared.order_column,
            label_column=declared.label_column,
            detection_position_ordinal=declared.cutoff_ordinal - 1,
            prediction_point=declared.prediction_point,
            features=declared.features,
            positive_class=declared.positive_class,
            class_order=declared.class_order,
        )
        manifest, outputs, _ = self.execute(outcome=lookup)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertIn("lookup, not a prediction", manifest.refusal_reason)
        self.assertEqual([MANIFEST_FILENAME], sorted(os.listdir(outputs)))

    def test_a_population_below_the_eligibility_floor_is_refused_with_its_numbers(self):
        manifest, outputs, _ = self.execute(units=30)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.ELIGIBILITY_NOT_MET.value, manifest.refusal_code)
        self.assertIn("required 40, observed 30", manifest.refusal_reason)
        self.assertEqual([MANIFEST_FILENAME], sorted(os.listdir(outputs)))

    def test_a_job_spec_for_another_family_is_refused_before_anything_is_read(self):
        manifest, _, _ = self.execute(model_family="mf03_novelty")
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.UNSUPPORTED_MODEL_FAMILY.value, manifest.refusal_code)

    def test_a_tampered_artifact_is_refused_by_the_sealed_input_check(self):
        directory = tempfile.mkdtemp(dir=self.root)
        inputs = os.path.join(directory, "inputs")
        os.makedirs(inputs, exist_ok=True)
        descriptor = seal_population(inputs, OutcomeKind.BINARY)
        with open(descriptor.uri, "r+b") as handle:
            handle.seek(descriptor.byte_size // 2)
            handle.write(b"\x00\x00\x00\x00")

        spec = JobSpec(
            protocol=PROTOCOL_ID,
            job_id="job-t175-tampered",
            tenant_id="tenant-fixture",
            site_id="site-fixture",
            model_family=MF04_MODEL_FAMILY,
            inputs=(
                ArtifactRef(
                    artifact_id=descriptor.artifact_id,
                    uri=descriptor.uri,
                    content_hash=descriptor.artifact_byte_hash,
                    artifact_format=descriptor.artifact_format,
                ),
            ),
            output_directory=os.path.join(directory, "outputs"),
            seed=7,
            code_identity="t175-fixture",
            resources=ResourceBudget(max_wall_clock_seconds=600.0),
            parameters=build_job_parameters(build_outcome(OutcomeKind.BINARY)),
        )
        manifest = run(spec, run_mf04)
        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.ARTIFACT_HASH_MISMATCH.value, manifest.refusal_code)

    def test_an_absent_booster_library_is_an_unavailable_installation_not_a_crash(self):
        original = candidate_module._import_booster_library

        def refuse():
            raise ModelUnavailableError(
                "The tabular candidate requires the 'lightgbm' package, which is not "
                "installed in this environment."
            )

        candidate_module._import_booster_library = refuse
        try:
            manifest, outputs, _ = self.execute()
        finally:
            candidate_module._import_booster_library = original

        self.assertEqual(JobOutcome.REFUSED.value, manifest.outcome)
        self.assertEqual(RefusalCode.UNSUPPORTED_MODEL_FAMILY.value, manifest.refusal_code)
        self.assertIn("lightgbm", manifest.refusal_reason)
        self.assertEqual([MANIFEST_FILENAME], sorted(os.listdir(outputs)))


class TheEvidenceIsReproducibleAndDecidesNothing(Mf04RuntimeCase):
    def test_two_runs_on_identical_input_produce_identical_artifact_bytes(self):
        first, _, _ = self.execute()
        second, _, _ = self.execute()
        self.assertEqual(
            [a.content_hash for a in first.artifacts],
            [a.content_hash for a in second.artifacts],
        )

    def test_the_snapshot_identity_is_the_same_through_either_storage_format(self):
        identities = []
        for adapter in enabled_adapters():
            _, outputs, _ = self.execute(artifact_format=adapter.format_name)
            identities.append(self.read_record(outputs)["snapshot"]["snapshot_identity"])
        self.assertEqual(1, len(set(identities)), "The identity must not depend on the format")

    def test_the_record_reports_a_difference_and_makes_no_selection(self):
        _, outputs, _ = self.execute()
        record = self.read_record(outputs)
        comparison = record["comparison"]
        self.assertFalse(comparison["selection_made_here"])
        self.assertEqual("T-176 selection kernel", comparison["selection_owner"])
        self.assertIn("auc", comparison["differences"])
        for values in comparison["differences"].values():
            self.assertIn("baseline", values)
            self.assertIn("candidate", values)
            self.assertAlmostEqual(
                values["candidate"] - values["baseline"],
                values["candidate_minus_baseline"],
                places=12,
            )

    def test_the_record_carries_the_environment_and_the_hyperparameters(self):
        _, outputs, _ = self.execute()
        record = self.read_record(outputs)
        self.assertIn("python_version", record["environment"])
        self.assertEqual(7, record["environment"]["seed"])
        candidate = record["models"][1]["description"]
        self.assertEqual("lightgbm", candidate["library"])
        self.assertTrue(candidate["library_version"])
        self.assertIn("num_leaves", candidate["hyperparameters"])
        self.assertEqual("fixture_declared_typed_contract", record["outcome_definition_source"])

    def test_the_leakage_and_eligibility_evidence_is_written_beside_the_metrics(self):
        _, outputs, _ = self.execute()
        record = self.read_record(outputs)
        self.assertTrue(record["leakage"]["passed"])
        self.assertEqual(3, len(record["leakage"]["detail"]))
        self.assertTrue(record["eligibility"]["eligible"])
        self.assertTrue(all(c["satisfied"] for c in record["eligibility"]["clauses"]))

    def test_the_holdout_is_the_tail_of_the_declared_order_not_a_random_sample(self):
        _, outputs, _ = self.execute(units=200)
        record = self.read_record(outputs)
        self.assertEqual(50, record["holdout"]["holdout_units"])
        self.assertEqual(150, record["holdout"]["train_units"])
        self.assertEqual("out_of_time_tail", record["holdout"]["strategy"])


if __name__ == "__main__":
    unittest.main()
