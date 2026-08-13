"""Every enabled adapter, every logical type, nulls, order and projection."""

import os
import shutil
import tempfile
import unittest
from datetime import date, datetime, timedelta, timezone
from decimal import Decimal

from ppiq_ml.artifacts import (
    ArtifactHashMismatchError, Field, LogicalSchema, LogicalType,
    UnsupportedSchemaError, enabled_adapters, logical_content_hash,
)
from tests.test_artifact_contract import full_rows, full_schema

UTC = timezone.utc


class RoundTripAcrossEveryAdapter(unittest.TestCase):
    """Each test runs against BOTH formats, so neither is privileged."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t169-rt-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def path(self, adapter, name):
        return os.path.join(self.root, name + adapter.file_suffix)

    def test_every_type_survives_a_round_trip(self):
        schema, rows = full_schema(), full_rows()
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "types"), schema, rows, "types")
                result = adapter.read(self.path(adapter, "types"))

                self.assertEqual(schema.names, result.schema.names)
                self.assertEqual(len(rows), len(result.rows))
                self.assertEqual(logical_content_hash(schema, rows),
                                 logical_content_hash(result.schema, result.rows))

    def test_nulls_survive_and_stay_null(self):
        schema, rows = full_schema(), full_rows()
        null_row_index = 2
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "nulls"), schema, rows, "nulls")
                result = adapter.read(self.path(adapter, "nulls"))
                row = result.rows[null_row_index]
                # Every nullable column in that row is null; the not-null one is not.
                self.assertTrue(all(v is None for v in row[:-1]))
                self.assertEqual(3, row[-1])

    def test_row_order_is_preserved_exactly(self):
        schema = LogicalSchema((Field("i", LogicalType.INT64), Field("s", LogicalType.STRING)))
        rows = tuple((i, f"row-{i}") for i in range(500))
        shuffled = tuple(reversed(rows))
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "order"), schema, shuffled, "order")
                result = adapter.read(self.path(adapter, "order"))
                self.assertEqual(shuffled, result.rows)

    def test_a_projection_returns_the_REQUESTED_column_order(self):
        """Not the order the columns appear in the file."""
        schema, rows = full_schema(), full_rows()
        requested = ("f_date", "f_bool", "f_decimal")
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "proj"), schema, rows, "proj")
                result = adapter.read(self.path(adapter, "proj"), projection=requested)

                self.assertEqual(requested, result.schema.names)
                self.assertEqual(len(rows), len(result.rows))
                for original, projected in zip(rows, result.rows):
                    self.assertEqual(original[8], projected[0])
                    self.assertEqual(original[0], projected[1])
                    self.assertEqual(original[5], projected[2])

    def test_a_single_column_projection_works(self):
        schema, rows = full_schema(), full_rows()
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "one"), schema, rows, "one")
                result = adapter.read(self.path(adapter, "one"), projection=("f_string",))
                self.assertEqual(("f_string",), result.schema.names)
                self.assertEqual(("alpha",), result.rows[0])

    def test_a_projection_of_an_absent_column_is_refused(self):
        schema, rows = full_schema(), full_rows()
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "bad"), schema, rows, "bad")
                with self.assertRaises(UnsupportedSchemaError):
                    adapter.read(self.path(adapter, "bad"), projection=("nope",))

    def test_the_descriptor_records_what_was_written(self):
        schema, rows = full_schema(), full_rows()
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                d = adapter.write(self.path(adapter, "desc"), schema, rows, "desc-1")
                self.assertEqual("desc-1", d.artifact_id)
                self.assertEqual(adapter.format_name, d.artifact_format)
                self.assertEqual(len(rows), d.row_count)
                self.assertEqual(schema.names, d.column_names)
                self.assertEqual(schema.to_canonical(), d.schema_canonical)
                self.assertGreater(d.byte_size, 0)
                self.assertEqual(64, len(d.logical_content_hash))
                self.assertEqual(64, len(d.artifact_byte_hash))

    def test_verify_accepts_the_artifact_it_described(self):
        schema, rows = full_schema(), full_rows()
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                p = self.path(adapter, "verify")
                d = adapter.write(p, schema, rows, "verify")
                adapter.verify(p, d)

    def test_verify_rejects_an_artifact_that_is_not_the_described_one(self):
        schema, rows = full_schema(), full_rows()
        other = rows[:2]
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                p = self.path(adapter, "swap")
                d = adapter.write(p, schema, rows, "swap")
                adapter.write(p, schema, other, "swap")
                with self.assertRaises(ArtifactHashMismatchError) as ctx:
                    adapter.verify(p, d)
                self.assertIn("not the ones the descriptor was written for", str(ctx.exception))

    def test_a_null_in_a_not_nullable_column_is_refused_before_any_file_is_written(self):
        schema = LogicalSchema((Field("n", LogicalType.INT64, nullable=False),))
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                p = self.path(adapter, "notnull")
                with self.assertRaises(UnsupportedSchemaError):
                    adapter.write(p, schema, ((1,), (None,)), "notnull")
                self.assertFalse(os.path.exists(p),
                                 "a refused write must not leave a partial file")

    def test_a_row_of_the_wrong_width_is_refused(self):
        schema = LogicalSchema((Field("a", LogicalType.INT64), Field("b", LogicalType.INT64)))
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                with self.assertRaises(UnsupportedSchemaError):
                    adapter.write(self.path(adapter, "width"), schema, ((1, 2), (3,)), "width")

    def test_a_decimal_beyond_its_declared_precision_is_refused(self):
        schema = LogicalSchema((Field("d", LogicalType.DECIMAL, True, 4, 2),))
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                with self.assertRaises(UnsupportedSchemaError):
                    adapter.write(self.path(adapter, "prec"), schema,
                                  ((Decimal("99999.99"),),), "prec")

    def test_an_empty_population_round_trips(self):
        schema = LogicalSchema((Field("i", LogicalType.INT64),))
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                d = adapter.write(self.path(adapter, "empty"), schema, (), "empty")
                self.assertEqual(0, d.row_count)
                result = adapter.read(self.path(adapter, "empty"))
                self.assertEqual((), result.rows)

    def test_unicode_and_empty_strings_survive(self):
        schema = LogicalSchema((Field("s", LogicalType.STRING),))
        rows = (("",), ("plain",), ("a b\tc\nd",), ("\u00e9\u4e2d\u6587",))
        for adapter in enabled_adapters():
            with self.subTest(fmt=adapter.format_name):
                adapter.write(self.path(adapter, "uni"), schema, rows, "uni")
                self.assertEqual(rows, adapter.read(self.path(adapter, "uni")).rows)


if __name__ == "__main__":
    unittest.main()
