"""Arrow IPC adapter. One of two enabled formats. B-03 has not selected a winner."""

from __future__ import annotations

import os
from typing import Any, Sequence

import pyarrow as pa

from ._arrow import arrow_schema, from_table, to_table
from ._reverse import logical_from_arrow_schema
from .contract import (
    ArtifactCorruptError, ArtifactDescriptor, ArtifactTruncatedError,
    ColumnarArtifactAdapter, ReadResult,
)
from .hashing import artifact_byte_hash, logical_content_hash
from .schema import LogicalSchema, UnsupportedSchemaError

ARROW_MAGIC = b"ARROW1"


class ArrowIpcArtifactAdapter(ColumnarArtifactAdapter):
    def __init__(self, compression: str | None = None) -> None:
        self.compression = compression

    @property
    def format_name(self) -> str:
        return "arrow_ipc"

    @property
    def file_suffix(self) -> str:
        return ".arrow"

    def write(self, path: str, schema: LogicalSchema, rows: Sequence[Sequence[Any]],
              artifact_id: str) -> ArtifactDescriptor:
        arrow_schema(schema)
        table = to_table(schema, rows)

        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
        options = pa.ipc.IpcWriteOptions(compression=self.compression) if self.compression else None
        with pa.OSFile(path, "wb") as sink:
            with pa.ipc.new_file(sink, table.schema, options=options) as writer:
                writer.write_table(table)

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
        _guard_arrow_file(path)
        try:
            with pa.memory_map(path, "rb") as source:
                with pa.ipc.open_file(source) as reader:
                    table = reader.read_all()
        except (pa.ArrowInvalid, pa.ArrowNotImplementedError, OSError) as error:
            raise ArtifactCorruptError(
                f"The Arrow IPC artifact at '{path}' could not be read: {error}"
            ) from error

        stored = logical_from_arrow_schema(table.schema)
        wanted = stored.project(projection) if projection else stored
        if projection:
            table = table.select(list(wanted.names))
        return ReadResult(schema=wanted, rows=from_table(table, wanted))


def _guard_arrow_file(path: str) -> None:
    """The Arrow IPC file format begins and ends with the ARROW1 marker."""
    if not os.path.exists(path):
        raise ArtifactCorruptError(f"No artifact at '{path}'.")
    size = os.path.getsize(path)
    if size < 16:
        raise ArtifactTruncatedError(
            f"The artifact at '{path}' is {size} bytes, shorter than an Arrow IPC "
            "header and footer. It is truncated."
        )
    with open(path, "rb") as handle:
        head = handle.read(6)
        handle.seek(-6, os.SEEK_END)
        tail = handle.read(6)
    if head != ARROW_MAGIC:
        raise ArtifactCorruptError(
            f"The artifact at '{path}' does not begin with the Arrow IPC marker."
        )
    if tail != ARROW_MAGIC:
        raise ArtifactTruncatedError(
            f"The artifact at '{path}' does not end with the Arrow IPC marker. "
            "The write did not complete."
        )
