"""Protocol identity, codec round-trips and version-mismatch refusal."""

import json
import unittest

from ppiq_ml.runtime import (
    PROTOCOL_ID, ArtifactRef, JobOutcome, JobSpec, ProtocolError, RefusalCode,
    ResourceBudget, ResultManifest, validate_refusal_consistency,
)


def spec(**over):
    base = dict(
        protocol=PROTOCOL_ID, job_id="job-1", tenant_id="t-1", site_id="s-1",
        model_family="mf04_supervised", inputs=(), output_directory="/tmp/out",
        seed=20260812, code_identity="commit-abc",
        resources=ResourceBudget(max_wall_clock_seconds=600.0),
    )
    base.update(over)
    return JobSpec(**base)


class ProtocolContract(unittest.TestCase):
    def test_protocol_id_is_name_and_version(self):
        self.assertEqual(PROTOCOL_ID, "ppiq.mljob/1")

    def test_job_spec_round_trips_without_loss(self):
        original = spec(inputs=(ArtifactRef("a1", "/tmp/a.parquet", "hash1", "parquet", 42),),
                        semantic_manifest_id="manifest-1", parameters={"n_estimators": 200})
        restored = JobSpec.from_json(original.to_json())
        self.assertEqual(original, restored)

    def test_a_future_protocol_is_refused_before_the_payload_is_interpreted(self):
        raw = json.loads(spec().to_json())
        raw["protocol"] = "ppiq.mljob/99"
        raw["job_id"] = "should-never-be-read"
        with self.assertRaises(ProtocolError) as ctx:
            JobSpec.from_json(json.dumps(raw))
        self.assertEqual(RefusalCode.PROTOCOL_VERSION_MISMATCH, ctx.exception.code)
        self.assertIn("was not interpreted", ctx.exception.message)

    def test_a_spec_with_no_protocol_is_refused(self):
        raw = json.loads(spec().to_json())
        del raw["protocol"]
        with self.assertRaises(ProtocolError) as ctx:
            JobSpec.from_json(json.dumps(raw))
        self.assertEqual(RefusalCode.MALFORMED_JOB_SPEC, ctx.exception.code)

    def test_malformed_json_is_refused_not_crashed(self):
        with self.assertRaises(ProtocolError) as ctx:
            JobSpec.from_json("{ this is not json")
        self.assertEqual(RefusalCode.MALFORMED_JOB_SPEC, ctx.exception.code)

    def test_a_spec_missing_required_fields_names_them(self):
        raw = json.loads(spec().to_json())
        del raw["seed"]
        del raw["code_identity"]
        with self.assertRaises(ProtocolError) as ctx:
            JobSpec.from_json(json.dumps(raw))
        self.assertIn("code_identity", ctx.exception.message)
        self.assertIn("seed", ctx.exception.message)

    def test_a_spec_without_a_wall_clock_budget_is_refused(self):
        raw = json.loads(spec().to_json())
        raw["resources"] = {}
        with self.assertRaises(ProtocolError):
            JobSpec.from_json(json.dumps(raw))

    def test_result_manifest_round_trips(self):
        manifest = ResultManifest(
            protocol=PROTOCOL_ID, job_id="job-1", outcome=JobOutcome.SUCCEEDED.value,
            started_at_utc="a", completed_at_utc="b", duration_seconds=1.5,
            code_identity="commit-abc", seed=1, runtime_version="ppiq_ml 0.1.0",
            metrics={"auc": 0.81},
        )
        self.assertEqual(manifest, ResultManifest.from_json(manifest.to_json()))

    def test_a_manifest_from_a_foreign_protocol_is_rejected(self):
        raw = {"protocol": "ppiq.mljob/99", "job_id": "j", "outcome": "succeeded", "duration_seconds": 1.0}
        with self.assertRaises(ValueError):
            ResultManifest.from_dict(raw)

    def test_an_unknown_outcome_is_rejected(self):
        raw = {"protocol": PROTOCOL_ID, "job_id": "j", "outcome": "probably_fine", "duration_seconds": 1.0}
        with self.assertRaises(ValueError):
            ResultManifest.from_dict(raw)

    def test_a_refusal_must_carry_a_code_and_a_sentence(self):
        bare = ResultManifest(
            protocol=PROTOCOL_ID, job_id="j", outcome=JobOutcome.REFUSED.value,
            started_at_utc="a", completed_at_utc="b", duration_seconds=0.1,
            code_identity="c", seed=0, runtime_version="v",
        )
        with self.assertRaises(ValueError):
            validate_refusal_consistency(bare)

    def test_a_success_must_not_carry_a_refusal_code(self):
        confused = ResultManifest(
            protocol=PROTOCOL_ID, job_id="j", outcome=JobOutcome.SUCCEEDED.value,
            started_at_utc="a", completed_at_utc="b", duration_seconds=0.1,
            code_identity="c", seed=0, runtime_version="v",
            refusal_code=RefusalCode.ELIGIBILITY_NOT_MET.value,
        )
        with self.assertRaises(ValueError):
            validate_refusal_consistency(confused)

    def test_integrity_hash_detects_a_changed_manifest(self):
        a = ResultManifest(
            protocol=PROTOCOL_ID, job_id="j", outcome=JobOutcome.SUCCEEDED.value,
            started_at_utc="a", completed_at_utc="b", duration_seconds=0.1,
            code_identity="c", seed=0, runtime_version="v", metrics={"auc": 0.80},
        )
        b = ResultManifest(**{**a.__dict__, "metrics": {"auc": 0.81}})
        self.assertNotEqual(a.integrity_hash(), b.integrity_hash())

    def test_job_outcome_and_analysis_state_are_different_axes(self):
        """A job can succeed while the analysis it ran honestly refuses."""
        manifest = ResultManifest(
            protocol=PROTOCOL_ID, job_id="j", outcome=JobOutcome.SUCCEEDED.value,
            started_at_utc="a", completed_at_utc="b", duration_seconds=0.1,
            code_identity="c", seed=0, runtime_version="v",
            analysis_terminal_state="InsufficientData",
        )
        validate_refusal_consistency(manifest)
        self.assertEqual(JobOutcome.SUCCEEDED.value, manifest.outcome)
        self.assertEqual("InsufficientData", manifest.analysis_terminal_state)


if __name__ == "__main__":
    unittest.main()
