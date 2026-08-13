"""Shared conversion between the logical schema and pyarrow. Not a public surface."""

from __future__ import annotations

from datetime import timezone
from decimal import Decimal
from typing import Any, Sequence

import pyarrow as pa

from .schema import Field, LogicalSchema, LogicalType, UnsupportedSchemaError

_ARROW_BY_LOGICAL = {
    LogicalType.BOOLEAN: lambda f: pa.bool_(),
    LogicalType.INT32: lambda f: pa.int32(),
    LogicalType.INT64: lambda f: pa.int64(),
    LogicalType.FLOAT32: lambda f: pa.float32(),
    LogicalType.FLOAT64: lambda f: pa.float64(),
    LogicalType.DECIMAL: lambda f: pa.decimal128(int(f.precision), int(f.scale)),
    LogicalType.STRING: lambda f: pa.string(),
    LogicalType.TIMESTAMP_UTC: lambda f: pa.timestamp("us", tz="UTC"),
    LogicalType.DATE: lambda f: pa.date32(),
}


def arrow_field(field: Field) -> pa.Field:
    factory = _ARROW_BY_LOGICAL.get(field.logical_type)
    if factory is None:
        raise UnsupportedSchemaError(
            f"Field '{field.name}' has logical type '{field.logical_type}', which no "
            "adapter can represent. This is a limitation of the supported type set, "
            "not a property of the data."
        )
    return pa.field(field.name, factory(field), nullable=field.nullable)


def arrow_schema(schema: LogicalSchema) -> pa.Schema:
    return pa.schema([arrow_field(f) for f in schema.fields])


def to_table(schema: LogicalSchema, rows: Sequence[Sequence[Any]]) -> pa.Table:
    target = arrow_schema(schema)
    columns = []
    for index, field in enumerate(schema.fields):
        values = []
        for row_index, row in enumerate(rows):
            if len(row) != len(schema.fields):
                raise UnsupportedSchemaError(
                    f"Row {row_index} has {len(row)} values but the schema declares "
                    f"{len(schema.fields)} fields."
                )
            value = row[index]
            if value is None and not field.nullable:
                raise UnsupportedSchemaError(
                    f"Field '{field.name}' is declared not nullable but row {row_index} "
                    "carries a null."
                )
            if value is not None and field.logical_type == LogicalType.DECIMAL:
                value = value if isinstance(value, Decimal) else Decimal(str(value))
                value = value.quantize(Decimal(1).scaleb(-int(field.scale or 0)))
            if value is not None and field.logical_type == LogicalType.TIMESTAMP_UTC:
                if value.tzinfo is None:
                    raise UnsupportedSchemaError(
                        f"Field '{field.name}' carries a naive datetime. A timestamp "
                        "without a zone has no defined identity."
                    )
                value = value.astimezone(timezone.utc)
            values.append(value)
        try:
            columns.append(pa.array(values, type=target.field(index).type))
        except (pa.ArrowInvalid, pa.ArrowTypeError, OverflowError) as error:
            raise UnsupportedSchemaError(
                f"Field '{field.name}' cannot be represented as "
                f"{target.field(index).type}: {error}"
            ) from error
    return pa.Table.from_arrays(columns, schema=target)


def from_table(table: pa.Table, schema: LogicalSchema) -> tuple[tuple[Any, ...], ...]:
    """Materialise rows in file order, with columns in the SCHEMA's order."""
    columns = [table.column(name).to_pylist() for name in schema.names]
    return tuple(tuple(col[i] for col in columns) for i in range(table.num_rows))
