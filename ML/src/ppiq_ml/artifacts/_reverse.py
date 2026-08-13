"""Recover the logical schema from a stored arrow schema. Not a public surface."""

from __future__ import annotations

import pyarrow as pa

from .schema import Field, LogicalSchema, LogicalType, UnsupportedSchemaError


def logical_from_arrow_schema(schema: pa.Schema) -> LogicalSchema:
    fields = []
    for f in schema:
        t = f.type
        if pa.types.is_boolean(t):
            fields.append(Field(f.name, LogicalType.BOOLEAN, f.nullable))
        elif pa.types.is_int32(t):
            fields.append(Field(f.name, LogicalType.INT32, f.nullable))
        elif pa.types.is_int64(t):
            fields.append(Field(f.name, LogicalType.INT64, f.nullable))
        elif pa.types.is_float32(t):
            fields.append(Field(f.name, LogicalType.FLOAT32, f.nullable))
        elif pa.types.is_float64(t):
            fields.append(Field(f.name, LogicalType.FLOAT64, f.nullable))
        elif pa.types.is_decimal(t):
            fields.append(Field(f.name, LogicalType.DECIMAL, f.nullable, t.precision, t.scale))
        elif pa.types.is_string(t) or pa.types.is_large_string(t):
            fields.append(Field(f.name, LogicalType.STRING, f.nullable))
        elif pa.types.is_timestamp(t):
            if t.tz is None:
                raise UnsupportedSchemaError(
                    f"Stored field '{f.name}' is a timestamp with no zone. A timestamp "
                    "without a zone has no defined identity."
                )
            fields.append(Field(f.name, LogicalType.TIMESTAMP_UTC, f.nullable))
        elif pa.types.is_date32(t):
            fields.append(Field(f.name, LogicalType.DATE, f.nullable))
        else:
            raise UnsupportedSchemaError(
                f"Stored field '{f.name}' has physical type '{t}', which the logical "
                "schema does not cover. This is a limitation of the supported type set."
            )
    return LogicalSchema(tuple(fields))
