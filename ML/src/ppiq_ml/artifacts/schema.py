"""Format-independent typed schema for a training artifact.

The schema is the contract. Parquet and Arrow IPC are storage formats behind it, and
neither appears in this module. A column's logical type, nullability and ordinal
position are what a model sees; the physical encoding is not.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class LogicalType(str, Enum):
    """The types a training artifact may carry. Deliberately small and explicit."""

    BOOLEAN = "boolean"
    INT32 = "int32"
    INT64 = "int64"
    FLOAT32 = "float32"
    FLOAT64 = "float64"
    DECIMAL = "decimal"
    STRING = "string"
    TIMESTAMP_UTC = "timestamp_utc"
    DATE = "date"


class UnsupportedSchemaError(Exception):
    """The schema cannot be represented. Raised before any file is written."""


@dataclass(frozen=True)
class Field:
    """One column. Order within a schema is significant and is preserved everywhere."""

    name: str
    logical_type: LogicalType
    nullable: bool = True
    precision: int | None = None
    scale: int | None = None

    def __post_init__(self) -> None:
        if not self.name or not self.name.strip():
            raise UnsupportedSchemaError("A field name may not be empty.")
        if self.logical_type == LogicalType.DECIMAL:
            if self.precision is None or self.scale is None:
                raise UnsupportedSchemaError(
                    f"Field '{self.name}' is decimal and must declare precision and scale. "
                    "A decimal without them has no defined identity across formats."
                )
            if not (1 <= self.precision <= 38):
                raise UnsupportedSchemaError(
                    f"Field '{self.name}' declares precision {self.precision}; "
                    "the supported range is 1 to 38."
                )
            if not (0 <= self.scale <= self.precision):
                raise UnsupportedSchemaError(
                    f"Field '{self.name}' declares scale {self.scale}, which must be "
                    f"between 0 and its precision {self.precision}."
                )
        elif self.precision is not None or self.scale is not None:
            raise UnsupportedSchemaError(
                f"Field '{self.name}' is {self.logical_type.value} and may not declare "
                "precision or scale."
            )


@dataclass(frozen=True)
class LogicalSchema:
    """An ordered list of fields. Column order is part of the identity."""

    fields: tuple[Field, ...]

    def __post_init__(self) -> None:
        if not self.fields:
            raise UnsupportedSchemaError("A schema must declare at least one field.")
        seen: set[str] = set()
        for field in self.fields:
            if field.name in seen:
                raise UnsupportedSchemaError(f"Duplicate field name '{field.name}'.")
            seen.add(field.name)

    @property
    def names(self) -> tuple[str, ...]:
        return tuple(f.name for f in self.fields)

    def field(self, name: str) -> Field:
        for f in self.fields:
            if f.name == name:
                return f
        raise UnsupportedSchemaError(f"Field '{name}' is not in the schema.")

    def project(self, names: tuple[str, ...]) -> "LogicalSchema":
        """Return a schema in the REQUESTED order, not the declared order."""
        missing = [n for n in names if n not in self.names]
        if missing:
            raise UnsupportedSchemaError(
                f"Projection requests fields not in the schema: {', '.join(missing)}."
            )
        return LogicalSchema(tuple(self.field(n) for n in names))

    def to_canonical(self) -> str:
        """Stable text form used by the logical content hash."""
        parts = []
        for f in self.fields:
            spec = f"{f.name}:{f.logical_type.value}:{'null' if f.nullable else 'notnull'}"
            if f.logical_type == LogicalType.DECIMAL:
                spec += f":{f.precision}:{f.scale}"
            parts.append(spec)
        return "|".join(parts)
