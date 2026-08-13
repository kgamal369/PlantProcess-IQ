"""B-03 provides a measurement hook and a result shape. It selects no winner."""

import shutil
import tempfile
import unittest
from dataclasses import fields as dataclass_fields

from ppiq_ml.artifacts import (
    B03_RESULT_SCHEMA_VERSION, B03Measurement, enabled_adapters, measure_all_enabled,
)
from tests.test_artifact_contract import full_rows, full_schema


class B03Hook(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t169-b03-")
        self.schema = full_schema()
        self.rows = full_rows() * 40
        self.projection = ("f_int64", "f_float64")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def measure(self):
        return measure_all_enabled(self.root, "fixture-a", self.schema, self.rows, self.projection)

    def test_the_same_fixture_is_measured_through_every_enabled_adapter(self):
        results = self.measure()
        self.assertEqual(len(enabled_adapters()), len(results))
        self.assertEqual({"parquet", "arrow_ipc"}, {r.artifact_format for r in results})

    def test_the_comparison_is_fair_because_the_logical_hash_matches(self):
        """Two formats can only be compared if they hold the same logical data."""
        results = self.measure()
        self.assertEqual(1, len({r.logical_content_hash for r in results}))
        self.assertEqual(len(results), len({r.artifact_byte_hash for r in results}))

    def test_every_measurement_field_is_populated(self):
        for result in self.measure():
            with self.subTest(fmt=result.artifact_format):
                self.assertEqual("B-03", result.benchmark_id)
                self.assertEqual(B03_RESULT_SCHEMA_VERSION, result.result_schema_version)
                self.assertEqual(len(self.rows), result.row_count)
                self.assertEqual(len(self.schema.fields), result.column_count)
                self.assertEqual(len(self.projection), result.projected_column_count)
                self.assertGreater(result.write_seconds, 0.0)
                self.assertGreater(result.read_seconds, 0.0)
                self.assertGreater(result.projected_read_seconds, 0.0)
                self.assertGreater(result.artifact_bytes, 0)
                self.assertGreater(result.bytes_per_row, 0.0)
                self.assertGreater(result.write_rows_per_second, 0.0)
                self.assertGreater(result.read_rows_per_second, 0.0)
                self.assertGreater(result.projected_read_rows_per_second, 0.0)
                self.assertGreater(result.peak_write_bytes, 0)
                self.assertGreater(result.peak_read_bytes, 0)

    def test_the_result_is_machine_readable(self):
        for result in self.measure():
            payload = result.to_dict()
            self.assertIsInstance(payload, dict)
            self.assertEqual(len(dataclass_fields(B03Measurement)), len(payload))
            import json
            json.loads(json.dumps(payload))

    def test_memory_is_measured_and_bounded(self):
        """Peak allocation is reported, so an unbounded reader is visible as a number."""
        for result in self.measure():
            with self.subTest(fmt=result.artifact_format):
                self.assertGreater(result.peak_read_bytes, 0)
                self.assertLess(result.peak_read_bytes, 512 * 1024 * 1024)

    def test_the_hook_declares_no_winner_and_no_threshold(self):
        """T-169 emits numbers. Selecting a format is B-03's job, on target hardware."""
        results = self.measure()
        for result in results:
            payload = result.to_dict()
            for forbidden in ("winner", "selected", "passed", "verdict", "threshold", "budget"):
                self.assertNotIn(forbidden, payload,
                                 f"the B-03 result shape must not carry '{forbidden}'")

    def test_the_full_benchmark_framework_is_not_implemented_here(self):
        """T-182 owns the common B-01 to B-09 framework. T-169 provides the hook only."""
        import ppiq_ml.artifacts.b03 as module
        exported = {n for n in dir(module) if not n.startswith("_")}
        for framework_concept in ("run_all_benchmarks", "BenchmarkSuite", "ResultStore",
                                  "compare_runs", "schedule"):
            self.assertNotIn(framework_concept, exported)


if __name__ == "__main__":
    unittest.main()
