"""The two hashes an artifact carries, and why they are different things.

logical_content_hash
    Identity of the typed schema plus the ordered logical values. Format independent.
    The same data written as Parquet and as Arrow IPC produces the SAME value. This is
    what makes the storage format genuinely replaceable and a benchmark comparison fair.

artifact_byte_hash
    Hash of the actual file bytes. Parquet and Arrow IPC produce DIFFERENT values for
    the same data, and so do two versions of the same writer. This is what detects a
    corrupted or truncated file.

Confusing the two would let a format change look like a data change, or a corrupted
file look like a legitimate re-encode.
"""

from __future__ import annotations

import hashlib
from datetime import date, datetime, timezone
from decimal import Decimal
from typing import Any, Sequence

from .schema import Field, LogicalSchema, LogicalType, UnsupportedSchemaError

NULL_SENTINEL = "\x00NULL"


def _canonical_value(field: Field, value: Any) -> str:
    """One value, one stable string, independent of how a format stored it."""
    if value is None:
        if not field.nullable:
            raise UnsupportedSchemaError(
                f"Field '{field.name}' is declared not nullable but carries a null."
            )
        return NULL_SENTINEL

    t = field.logical_type
    if t == LogicalType.BOOLEAN:
        return "true" if bool(value) else "false"
    if t in (LogicalType.INT32, LogicalType.INT64):
        return str(int(value))
    if t in (LogicalType.FLOAT32, LogicalType.FLOAT64):
        # repr round-trips a double exactly and is stable across platforms.
        return repr(float(value))
    if t == LogicalType.DECIMAL:
        d = value if isinstance(value, Decimal) else Decimal(str(value))
        # Normalise to the declared scale so 1.5 and 1.50 are the same logical value
        # when the field declares scale 2, and different when it declares scale 1.
        quantised = d.quantize(Decimal(1).scaleb(-int(field.scale or 0)))
        sign, digits, exponent = quantised.as_tuple()
        unscaled = int("".join(str(x) for x in digits) or "0") * (-1 if sign else 1)
        return f"{unscaled}E{exponent}"
    if t == LogicalType.STRING:
        return str(value)
    if t == LogicalType.TIMESTAMP_UTC:
        if not isinstance(value, datetime):
            raise UnsupportedSchemaError(
                f"Field '{field.name}' is a UTC timestamp and requires a datetime."
            )
        if value.tzinfo is None:
            raise UnsupportedSchemaError(
                f"Field '{field.name}' carries a naive datetime. A timestamp without a "
                "zone has no defined identity."
            )
        micros = int(value.astimezone(timezone.utc).timestamp() * 1_000_000)
        return f"T{micros}"
    if t == LogicalType.DATE:
        if not isinstance(value, date) or isinstance(value, datetime):
            raise UnsupportedSchemaError(f"Field '{field.name}' is a date and requires a date.")
        return f"D{value.toordinal()}"

    raise UnsupportedSchemaError(f"Field '{field.name}' has an unsupported type '{t}'.")


def logical_content_hash(schema: LogicalSchema, rows: Sequence[Sequence[Any]]) -> str:
    """Format-independent identity of the typed schema plus the ordered logical values.

    Row order is significant. Column order is significant. Both are part of the
    artifact contract, so a reordering is a different artifact and must hash differently.
    """
    digest = hashlib.sha256()
    digest.update(b"ppiq.artifact.logical/1\n")
    digest.update(schema.to_canonical().encode("utf-8"))
    digest.update(b"\n")
    for index, row in enumerate(rows):
        if len(row) != len(schema.fields):
            raise UnsupportedSchemaError(
                f"Row {index} has {len(row)} values but the schema declares "
                f"{len(schema.fields)} fields."
            )
        cells = [_canonical_value(f, v) for f, v in zip(schema.fields, row)]
        digest.update("\x1f".join(cells).encode("utf-8"))
        digest.update(b"\x1e")
    return digest.hexdigest()


def artifact_byte_hash(path: str) -> str:
    """Hash of the actual file bytes, read in bounded chunks."""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()
