"""Typed columnar training artifacts.

Two hashes, two formats, no winner.

logical_content_hash is format independent: the same data written as Parquet and as
Arrow IPC produces the same value. artifact_byte_hash is the file bytes and differs
between formats by design.
"""

from .schema import Field, LogicalSchema, LogicalType, UnsupportedSchemaError
from .hashing import artifact_byte_hash, logical_content_hash
from .contract import (
    ArtifactCorruptError, ArtifactDescriptor, ArtifactHashMismatchError,
    ArtifactTruncatedError, ColumnarArtifactAdapter, ReadResult,
)
from .parquet_adapter import ParquetArtifactAdapter
from .arrow_ipc_adapter import ArrowIpcArtifactAdapter
from .registry import FormatNotSelectedError, adapter_for, default_adapter, enabled_adapters
from .b03 import B03_RESULT_SCHEMA_VERSION, B03Measurement, measure_adapter, measure_all_enabled

__all__ = [
    "Field", "LogicalSchema", "LogicalType", "UnsupportedSchemaError",
    "artifact_byte_hash", "logical_content_hash",
    "ArtifactCorruptError", "ArtifactDescriptor", "ArtifactHashMismatchError",
    "ArtifactTruncatedError", "ColumnarArtifactAdapter", "ReadResult",
    "ParquetArtifactAdapter", "ArrowIpcArtifactAdapter",
    "FormatNotSelectedError", "adapter_for", "default_adapter", "enabled_adapters",
    "B03_RESULT_SCHEMA_VERSION", "B03Measurement", "measure_adapter", "measure_all_enabled",
]
