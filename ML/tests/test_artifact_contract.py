"""Typed schema mapping, the two hashes, deterministic order and projection order."""

import os
import shutil
import tempfile
import unittest
from datetime import date, datetime, timedelta, timezone
from decimal import Decimal

from ppiq_ml.artifacts import (
    ArrowIpcArtifactAdapter, Field, FormatNotSelectedError, LogicalSchema, LogicalType,
    ParquetArtifactAdapter, UnsupportedSchemaError, adapter_for, default_adapter,
    enabled_adapters, logical_content_hash,
)

UTC = timezone.utc


def full_schema() -> LogicalSchema:
    return LogicalSchema((
        Field("f_bool", LogicalType.BOOLEAN),
        Field("f_int32", LogicalType.INT32),
        Field("f_int64", LogicalType.INT64),
        Field("f_float32", LogicalType.FLOAT32),
        Field("f_float64", LogicalType.FLOAT64),
        Field("f_decimal", LogicalType.DECIMAL, True, 18, 4),
        Field("f_string", LogicalType.STRING),
        Field("f_ts", LogicalType.TIMESTAMP_UTC),
        Field("f_date", LogicalType.DATE),
        Field("f_notnull", LogicalType.INT64, nullable=False),
    ))


def full_rows():
    base = datetime(2026, 8, 13, 10, 15, 30, 123456, tzinfo=UTC)
    return (
        (True, 1, 10_000_000_000, 1.5, 2.25, Decimal("1234.5678"), "alpha", base, date(2026, 1, 2), 1),
        (False, -2, -10_000_000_000, -0.5, -1.75, Decimal("-0.0001"), "beta", base + timedelta(seconds=1), date(2026, 6, 30), 2),
        (None, None, None, None, None, None, None, None, None, 3),
        (True, 0, 0, 0.0, 0.0, Decimal("0.0000"), "", base + timedelta(days=1), date(2026, 12, 31), 4),
    )


class SchemaMapping(unittest.TestCase):
    def test_a_decimal_without_precision_and_scale_is_refused(self):
        with self.assertRaises(UnsupportedSchemaError) as ctx:
            Field("d", LogicalType.DECIMAL)
        self.assertIn("no defined identity across formats", str(ctx.exception))

    def test_a_non_decimal_may_not_declare_precision(self):
        with self.assertRaises(UnsupportedSchemaError):
            Field("n", LogicalType.INT64, True, 10, 2)

    def test_decimal_precision_and_scale_bounds_are_enforced(self):
        with self.assertRaises(UnsupportedSchemaError):
            Field("d", LogicalType.DECIMAL, True, 39, 2)
        with self.assertRaises(UnsupportedSchemaError):
            Field("d", LogicalType.DECIMAL, True, 10, 11)

    def test_duplicate_field_names_are_refused(self):
        with self.assertRaises(UnsupportedSchemaError):
            LogicalSchema((Field("a", LogicalType.INT64), Field("a", LogicalType.STRING)))

    def test_an_empty_schema_is_refused(self):
        with self.assertRaises(UnsupportedSchemaError):
            LogicalSchema(())

    def test_a_projection_of_an_unknown_field_is_refused(self):
        with self.assertRaises(UnsupportedSchemaError) as ctx:
            full_schema().project(("f_bool", "not_a_field"))
        self.assertIn("not_a_field", str(ctx.exception))


class TheTwoHashes(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t169-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def test_the_logical_hash_is_the_same_across_both_formats(self):
        """This is what makes the storage format genuinely replaceable."""
        schema, rows = full_schema(), full_rows()
        descriptors = []
        for adapter in enabled_adapters():
            path = os.path.join(self.root, "same" + adapter.file_suffix)
            descriptors.append(adapter.write(path, schema, rows, "same"))

        hashes = {d.logical_content_hash for d in descriptors}
        self.assertEqual(1, len(hashes),
                         "the same data in two formats must have one logical identity")

    def test_the_byte_hashes_differ_across_formats(self):
        """And this is what detects a corrupted or truncated file."""
        schema, rows = full_schema(), full_rows()
        descriptors = []
        for adapter in enabled_adapters():
            path = os.path.join(self.root, "bytes" + adapter.file_suffix)
            descriptors.append(adapter.write(path, schema, rows, "bytes"))

        byte_hashes = {d.artifact_byte_hash for d in descriptors}
        self.assertEqual(len(descriptors), len(byte_hashes),
                         "different formats must produce different bytes")

    def test_the_two_hashes_are_never_the_same_value(self):
        adapter = ParquetArtifactAdapter()
        path = os.path.join(self.root, "distinct.parquet")
        d = adapter.write(path, full_schema(), full_rows(), "distinct")
        self.assertNotEqual(d.logical_content_hash, d.artifact_byte_hash)

    def test_row_order_changes_the_logical_hash(self):
        schema, rows = full_schema(), full_rows()
        reordered = (rows[1], rows[0]) + rows[2:]
        self.assertNotEqual(logical_content_hash(schema, rows),
                            logical_content_hash(schema, reordered))

    def test_column_order_changes_the_logical_hash(self):
        schema = LogicalSchema((Field("a", LogicalType.INT64), Field("b", LogicalType.STRING)))
        swapped = LogicalSchema((Field("b", LogicalType.STRING), Field("a", LogicalType.INT64)))
        self.assertNotEqual(logical_content_hash(schema, ((1, "x"),)),
                            logical_content_hash(swapped, (("x", 1),)))

    def test_declared_decimal_scale_is_part_of_the_identity(self):
        two = LogicalSchema((Field("d", LogicalType.DECIMAL, True, 10, 2),))
        one = LogicalSchema((Field("d", LogicalType.DECIMAL, True, 10, 1),))
        self.assertNotEqual(logical_content_hash(two, ((Decimal("1.50"),),)),
                            logical_content_hash(one, ((Decimal("1.5"),),)))

    def test_a_naive_timestamp_has_no_identity_and_is_refused(self):
        schema = LogicalSchema((Field("t", LogicalType.TIMESTAMP_UTC),))
        with self.assertRaises(UnsupportedSchemaError) as ctx:
            logical_content_hash(schema, ((datetime(2026, 1, 1, 12, 0, 0),),))
        self.assertIn("no defined identity", str(ctx.exception))

    def test_the_same_instant_in_a_different_zone_hashes_the_same(self):
        schema = LogicalSchema((Field("t", LogicalType.TIMESTAMP_UTC),))
        utc = datetime(2026, 8, 13, 12, 0, 0, tzinfo=UTC)
        plus_two = utc.astimezone(timezone(timedelta(hours=2)))
        self.assertEqual(logical_content_hash(schema, ((utc,),)),
                         logical_content_hash(schema, ((plus_two,),)))

    def test_a_null_in_a_not_nullable_field_is_refused(self):
        schema = LogicalSchema((Field("n", LogicalType.INT64, nullable=False),))
        with self.assertRaises(UnsupportedSchemaError):
            logical_content_hash(schema, ((None,),))


class NoWinnerIsSelected(unittest.TestCase):
    def test_both_formats_are_enabled(self):
        names = [a.format_name for a in enabled_adapters()]
        self.assertIn("parquet", names)
        self.assertIn("arrow_ipc", names)
        self.assertEqual(2, len(names))

    def test_asking_for_the_default_format_is_an_error_not_a_guess(self):
        with self.assertRaises(FormatNotSelectedError) as ctx:
            default_adapter()
        message = str(ctx.exception)
        self.assertIn("B-03", message)
        self.assertIn("deliberately does not choose", message)

    def test_an_adapter_is_resolved_only_by_an_explicit_format_name(self):
        self.assertEqual("parquet", adapter_for("parquet").format_name)
        self.assertEqual("arrow_ipc", adapter_for("arrow_ipc").format_name)
        with self.assertRaises(FormatNotSelectedError):
            adapter_for("orc")


if __name__ == "__main__":
    unittest.main()
