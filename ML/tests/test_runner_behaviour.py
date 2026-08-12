"""Success, honest refusal, crash, cancellation, checkpoint/resume, determinism,
and the rule that stdout is never authority."""

import hashlib
import json
import os
import shutil
import sys
import tempfile
import unittest

from ppiq_ml.runtime import (
    MANIFEST_FILENAME, PROTOCOL_ID, ArtifactRef, Checkpoint, CheckpointStore,
    JobOutcome, JobSpec, ProducedArtifact, RefusalCode, RefusalError,
    ResourceBudget, ResultManifest, run, validate_refusal_consistency,
)
from ppiq_ml.runtime.checkpoint import Checkpoint as Cp


class RunnerBehaviour(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t168-")
        self.out = os.path.join(self.root, "out")
        self.ckpt = os.path.join(self.root, "ckpt")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def artifact(self, name="input.bin", content=b"sealed-snapshot-bytes"):
        path = os.path.join(self.root, name)
        with open(path, "wb") as handle:
            handle.write(content)
        return ArtifactRef(
            artifact_id="snap-1", uri=path,
            content_hash=hashlib.sha256(content).hexdigest(),
            artifact_format="parquet", byte_size=len(content),
        )

    def spec(self, **over):
        base = dict(
            protocol=PROTOCOL_ID, job_id="job-1", tenant_id="t-1", site_id="s-1",
            model_family="mf04_supervised", inputs=(), output_directory=self.out,
            seed=20260812, code_identity="commit-abc",
            resources=ResourceBudget(max_wall_clock_seconds=600.0),
        )
        base.update(over)
        return JobSpec(**base)

    def read_manifest(self):
        path = os.path.join(self.out, MANIFEST_FILENAME)
        self.assertTrue(os.path.exists(path), "the runtime must always write a manifest")
        with open(path, encoding="ascii") as handle:
            return ResultManifest.from_json(handle.read())

    # ------------------------------------------------------------- success

    def test_a_successful_job_writes_a_valid_manifest(self):
        def handler(spec, store):
            return (
                (ProducedArtifact("model-1", "/tmp/model.bin", "abc", "model", 10),),
                {"auc": 0.83, "brier": 0.11},
                None,
                (),
            )

        result = run(self.spec(inputs=(self.artifact(),)), handler)
        on_disk = self.read_manifest()

        self.assertEqual(JobOutcome.SUCCEEDED.value, result.outcome)
        self.assertEqual(result, on_disk)
        self.assertEqual(0.83, on_disk.metrics["auc"])
        self.assertEqual("snap-1", list(on_disk.input_hashes)[0])
        validate_refusal_consistency(on_disk)

    def test_a_successful_job_may_still_carry_an_honest_analysis_refusal(self):
        def handler(spec, store):
            return ((), {}, "InsufficientData", ("population below the declared floor",))

        result = run(self.spec(), handler)
        self.assertEqual(JobOutcome.SUCCEEDED.value, result.outcome)
        self.assertEqual("InsufficientData", result.analysis_terminal_state)
        self.assertIn("population below the declared floor", result.warnings)

    # ------------------------------------------------------------- refusal

    def test_a_handler_refusal_is_recorded_with_its_code_and_sentence(self):
        def handler(spec, store):
            raise RefusalError(
                RefusalCode.ELIGIBILITY_NOT_MET,
                "The declared outcome carries 12 labelled units against a floor of 500.",
            )

        result = run(self.spec(), handler)
        self.assertEqual(JobOutcome.REFUSED.value, result.outcome)
        self.assertEqual(RefusalCode.ELIGIBILITY_NOT_MET.value, result.refusal_code)
        self.assertIn("500", result.refusal_reason)
        validate_refusal_consistency(result)

    def test_a_missing_input_artifact_refuses_rather_than_falling_back(self):
        missing = ArtifactRef("snap-1", os.path.join(self.root, "absent.bin"), "", "parquet", 0)

        result = run(self.spec(inputs=(missing,)), lambda s, c: ((), {}, None, ()))

        self.assertEqual(JobOutcome.REFUSED.value, result.outcome)
        self.assertEqual(RefusalCode.ARTIFACT_MISSING.value, result.refusal_code)
        self.assertIn("never a database", result.refusal_reason)

    def test_a_tampered_input_artifact_refuses_on_hash_mismatch(self):
        artifact = self.artifact()
        with open(artifact.uri, "wb") as handle:
            handle.write(b"different-bytes-entirely")

        result = run(self.spec(inputs=(artifact,)), lambda s, c: ((), {}, None, ()))

        self.assertEqual(JobOutcome.REFUSED.value, result.outcome)
        self.assertEqual(RefusalCode.ARTIFACT_HASH_MISMATCH.value, result.refusal_code)
        self.assertIn("not the one the job was authorised against", result.refusal_reason)

    # ------------------------------------------------------------- crash

    def test_an_unexpected_crash_still_produces_a_manifest(self):
        def handler(spec, store):
            raise ZeroDivisionError("division by zero deep inside a fit")

        result = run(self.spec(), handler)
        on_disk = self.read_manifest()

        self.assertEqual(JobOutcome.FAILED.value, result.outcome)
        self.assertEqual(JobOutcome.FAILED.value, on_disk.outcome)
        self.assertIn("ZeroDivisionError", on_disk.refusal_reason)

    def test_a_crash_is_failed_and_never_refused(self):
        """An error is not a governed refusal. Conflating them would let a bug read
        as an honest decision."""
        result = run(self.spec(), lambda s, c: (_ for _ in ()).throw(RuntimeError("boom")))
        self.assertEqual(JobOutcome.FAILED.value, result.outcome)
        self.assertNotEqual(JobOutcome.REFUSED.value, result.outcome)
        self.assertEqual(RefusalCode.NONE.value, result.refusal_code)

    # ------------------------------------------------------------- cancellation

    def test_cancellation_before_the_job_starts_is_honoured(self):
        cancel = os.path.join(self.root, "cancel.flag")
        open(cancel, "w").close()
        called = []

        def handler(spec, store):
            called.append(True)
            return ((), {}, None, ())

        result = run(self.spec(cancellation_file=cancel), handler)
        self.assertEqual(JobOutcome.CANCELLED.value, result.outcome)
        self.assertEqual([], called, "the handler must not run after cancellation")

    def test_cancellation_during_execution_is_honoured(self):
        cancel = os.path.join(self.root, "cancel.flag")

        def handler(spec, store):
            open(cancel, "w").close()
            return ((), {}, None, ())

        result = run(self.spec(cancellation_file=cancel), handler)
        self.assertEqual(JobOutcome.CANCELLED.value, result.outcome)

    # ------------------------------------------------------------- checkpoint

    def test_a_checkpoint_is_written_and_the_next_run_resumes_from_it(self):
        def stage_one(spec, store):
            store.write(Cp(spec.job_id, "encode", 1, {"rows_done": 5000}))
            store.write(Cp(spec.job_id, "train", 2, {"epoch": 3}))
            return ((), {}, None, ())

        run(self.spec(checkpoint_directory=self.ckpt), stage_one)

        seen = {}

        def stage_two(spec, store):
            latest = store.latest()
            seen["stage"] = latest.stage if latest else None
            seen["state"] = latest.state if latest else None
            return ((), {}, None, ())

        result = run(self.spec(checkpoint_directory=self.ckpt), stage_two)

        self.assertEqual("train", seen["stage"])
        self.assertEqual(3, seen["state"]["epoch"])
        self.assertEqual("train", result.resumed_from_checkpoint)

    def test_a_half_written_checkpoint_is_never_trusted(self):
        os.makedirs(self.ckpt, exist_ok=True)
        with open(os.path.join(self.ckpt, "stage-0009.json.partial"), "w") as handle:
            handle.write('{"job_id": "job-1", "stage": "trun')

        store = CheckpointStore(self.ckpt)
        self.assertIsNone(store.latest(), "a .partial file must not be read as a checkpoint")

    def test_no_checkpoint_directory_means_no_resume_and_no_error(self):
        result = run(self.spec(), lambda s, c: ((), {}, None, ()))
        self.assertEqual(JobOutcome.SUCCEEDED.value, result.outcome)
        self.assertIsNone(result.resumed_from_checkpoint)

    # ------------------------------------------------------------- determinism

    def test_repeated_runs_of_the_same_spec_agree_on_everything_but_timing(self):
        def handler(spec, store):
            return ((), {"auc": 0.83}, None, ())

        first = run(self.spec(inputs=(self.artifact(),)), handler)
        second = run(self.spec(inputs=(self.artifact(),)), handler)

        for field in ("protocol", "job_id", "outcome", "code_identity", "seed",
                      "runtime_version", "refusal_code", "refusal_reason",
                      "analysis_terminal_state"):
            self.assertEqual(getattr(first, field), getattr(second, field), field)
        self.assertEqual(first.metrics, second.metrics)
        self.assertEqual(first.input_hashes, second.input_hashes)

    # ------------------------------------------------------------- stdout is not authority

    def test_stdout_claiming_success_cannot_substitute_for_a_manifest(self):
        """A process that prints SUCCESS and then fails has still failed."""
        def handler(spec, store):
            sys.stdout.write("SUCCESS model trained, auc 0.99\n")
            sys.stdout.flush()
            raise RuntimeError("the fit never converged")

        result = run(self.spec(), handler)
        on_disk = self.read_manifest()

        self.assertEqual(JobOutcome.FAILED.value, on_disk.outcome)
        self.assertNotIn("0.99", json.dumps(dict(on_disk.metrics)))
        self.assertEqual({}, dict(on_disk.metrics))
        self.assertEqual(result.outcome, on_disk.outcome)

    def test_the_manifest_is_written_atomically(self):
        """A caller must never observe a half-written manifest."""
        run(self.spec(), lambda s, c: ((), {"auc": 0.5}, None, ()))
        names = os.listdir(self.out)
        self.assertIn(MANIFEST_FILENAME, names)
        self.assertNotIn(MANIFEST_FILENAME + ".partial", names)


if __name__ == "__main__":
    unittest.main()
