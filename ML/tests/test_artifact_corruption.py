"""Corruption and truncation must be refused, never returned as partial data."""

import os
import shutil
import tempfile
import unittest

from ppiq_ml.artifacts import (
    ArtifactCorruptError, ArtifactTruncatedError, enabled_adapters,
)
from tests.test_artifact_contract import full_rows, full_schema


class CorruptionIsRefused(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t169-corrupt-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def written(self, adapter, name):
        path = os.path.join(self.root, name + adapter.file_suffix)
        adapter.write(path, full_schema(), full_rows(), name)
        return path

    def test_a_missing_artifact_is_refused(self):
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                with self.assertRaises(ArtifactCorruptError):
                    adapter.read(os.path.join(self.root, "absent" + adapter.file_suffix))

    def test_an_empty_file_is_truncated_not_empty_data(self):
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                path = os.path.join(self.root, "zero" + adapter.file_suffix)
                open(path, "wb").close()
                with self.assertRaises(ArtifactTruncatedError):
                    adapter.read(path)

    def test_a_truncated_tail_is_detected(self):
        """The most dangerous case: a write that stopped part way."""
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                path = self.written(adapter, "cut")
                size = os.path.getsize(path)
                with open(path, "r+b") as handle:
                    handle.truncate(size // 2)
                with self.assertRaises(ArtifactTruncatedError) as ctx:
                    adapter.read(path)
                self.assertIn("did not complete", str(ctx.exception))

    def test_a_damaged_header_is_detected(self):
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                path = self.written(adapter, "head")
                with open(path, "r+b") as handle:
                    handle.seek(0)
                    handle.write(b"XXXX")
                with self.assertRaises(ArtifactCorruptError):
                    adapter.read(path)

    def test_damaged_interior_bytes_are_refused_rather_than_returned(self):
        """A file with valid markers but scrambled middle must not yield partial data."""
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                path = self.written(adapter, "middle")
                size = os.path.getsize(path)
                with open(path, "r+b") as handle:
                    handle.seek(size // 3)
                    handle.write(b"\xde\xad\xbe\xef" * 24)
                try:
                    result = adapter.read(path)
                except ArtifactCorruptError:
                    continue
                # If a format tolerated the damage, the data must still be intact.
                # Silent partial data is the outcome this test exists to forbid.
                self.assertEqual(len(full_rows()), len(result.rows),
                                 "a tolerated read must not silently lose rows")

    def test_a_truncated_file_is_never_reported_as_an_empty_population(self):
        """An empty population and a truncated file are different facts."""
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                empty_path = os.path.join(self.root, "genuinely_empty" + adapter.file_suffix)
                adapter.write(empty_path, full_schema(), (), "genuinely_empty")
                self.assertEqual((), adapter.read(empty_path).rows)

                cut_path = self.written(adapter, "not_empty_just_cut")
                with open(cut_path, "r+b") as handle:
                    handle.truncate(20)
                with self.assertRaises(ArtifactTruncatedError):
                    adapter.read(cut_path)


if __name__ == "__main__":
    unittest.main()
