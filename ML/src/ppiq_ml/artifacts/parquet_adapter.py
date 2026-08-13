"""Parquet adapter. One of two enabled formats. B-03 has not selected a winner."""

from __future__ import annotations

import os
from typing import Any, Sequence

import pyarrow as pa
import pyarrow.parquet as pq

from ._arrow import arrow_schema, from_table, to_table
from .contract import (
    ArtifactCorruptError, ArtifactDescriptor, ArtifactTruncatedError,
    ColumnarArtifactAdapter, ReadResult,
)
from .hashing import artifact_byte_hash, logical_content_hash
from .schema import LogicalSchema, UnsupportedSchemaError

PARQUET_MAGIC = b"PAR1"


class ParquetArtifactAdapter(ColumnarArtifactAdapter):
    def __init__(self, compression: str = "snappy") -> None:
        self.compression = compression

    @property
    def format_name(self) -> str:
        return "parquet"

    @property
    def file_suffix(self) -> str:
        return ".parquet"

    def write(self, path: str, schema: LogicalSchema, rows: Sequence[Sequence[Any]],
              artifact_id: str) -> ArtifactDescriptor:
        # Validate the schema before touching the filesystem, so an unsupported
        # schema never leaves a partial file behind.
        arrow_schema(schema)
        table = to_table(schema, rows)

        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
        pq.write_table(table, path, compression=self.compression, version="2.6")

        return ArtifactDescriptor(
            artifact_id=artifact_id,
            uri=path,
            artifact_format=self.format_name,
            logical_content_hash=logical_content_hash(schema, rows),
            artifact_byte_hash=artifact_byte_hash(path),
            byte_size=os.path.getsize(path),
            row_count=len(rows),
            column_names=schema.names,
            schema_canonical=schema.to_canonical(),
        )

    def read(self, path: str, projection: tuple[str, ...] | None = None) -> ReadResult:
        _guard_parquet_file(path)
        try:
            parquet_file = pq.ParquetFile(path)
            stored = _logical_from_arrow(parquet_file.schema_arrow)
            wanted = stored.project(projection) if projection else stored
            table = pq.read_table(path, columns=list(wanted.names))
        except UnsupportedSchemaError:
            raise
        except (pa.ArrowInvalid, pa.ArrowNotImplementedError, OSError) as error:
            raise ArtifactCorruptError(
                f"The Parquet artifact at '{path}' could not be read: {error}"
            ) from error
        return ReadResult(schema=wanted, rows=from_table(table, wanted))


def _guard_parquet_file(path: str) -> None:
    """Parquet begins and ends with PAR1. A missing tail means a truncated write."""
    if not os.path.exists(path):
        raise ArtifactCorruptError(f"No artifact at '{path}'.")
    size = os.path.getsize(path)
    if size < 8:
        raise ArtifactTruncatedError(
            f"The artifact at '{path}' is {size} bytes, shorter than a Parquet header "
            "and footer. It is truncated."
        )
    with open(path, "rb") as handle:
        head = handle.read(4)
        handle.seek(-4, os.SEEK_END)
        tail = handle.read(4)
    if head != PARQUET_MAGIC:
        raise ArtifactCorruptError(
            f"The artifact at '{path}' does not begin with the Parquet magic bytes."
        )
    if tail != PARQUET_MAGIC:
        raise ArtifactTruncatedError(
            f"The artifact at '{path}' does not end with the Parquet footer magic. "
            "The write did not complete."
        )


def _logical_from_arrow(schema: pa.Schema) -> LogicalSchema:
    from ._reverse import logical_from_arrow_schema
    return logical_from_arrow_schema(schema)
