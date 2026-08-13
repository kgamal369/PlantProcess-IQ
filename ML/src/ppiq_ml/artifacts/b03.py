"""B-03 measurement hook and result shape.

T-169 owns the HOOK and the RESULT SHAPE only. The common B-01 to B-09 benchmark
framework belongs to T-182, and nothing here attempts it: no scheduling, no result
store, no comparison across runs, no pass or fail verdict.

No threshold is defined. A measurement that produced a verdict here would be inventing
a production threshold, which T-169 must not do.
"""

from __future__ import annotations

import gc
import os
import time
import tracemalloc
from dataclasses import asdict, dataclass, field
from typing import Any, Sequence

from .contract import ColumnarArtifactAdapter
from .schema import LogicalSchema

B03_RESULT_SCHEMA_VERSION = 1


@dataclass(frozen=True)
class B03Measurement:
    """One adapter measured once. Machine readable, no verdict attached."""

    benchmark_id: str
    result_schema_version: int
    artifact_format: str
    fixture_id: str
    row_count: int
    column_count: int
    projected_column_count: int
    write_seconds: float
    read_seconds: float
    projected_read_seconds: float
    artifact_bytes: int
    bytes_per_row: float
    write_rows_per_second: float
    read_rows_per_second: float
    projected_read_rows_per_second: float
    peak_write_bytes: int
    peak_read_bytes: int
    logical_content_hash: str
    artifact_byte_hash: str
    notes: tuple[str, ...] = field(default_factory=tuple)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def _peak_bytes(callable_) -> tuple[Any, int]:
    """Run something and report its peak allocation, so memory stays bounded and measured."""
    gc.collect()
    tracemalloc.start()
    try:
        value = callable_()
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    return value, peak


def measure_adapter(
    adapter: ColumnarArtifactAdapter,
    directory: str,
    fixture_id: str,
    schema: LogicalSchema,
    rows: Sequence[Sequence[Any]],
    projection: tuple[str, ...],
) -> B03Measurement:
    """Measure one adapter against one fixture. Emits numbers, never a winner."""
    os.makedirs(directory, exist_ok=True)
    path = os.path.join(directory, f"{fixture_id}_{adapter.format_name}{adapter.file_suffix}")

    start = time.perf_counter()
    descriptor, peak_write = _peak_bytes(lambda: adapter.write(path, schema, rows, fixture_id))
    write_seconds = time.perf_counter() - start

    start = time.perf_counter()
    full, peak_read = _peak_bytes(lambda: adapter.read(path))
    read_seconds = time.perf_counter() - start

    start = time.perf_counter()
    projected = adapter.read(path, projection=projection)
    projected_read_seconds = time.perf_counter() - start

    row_count = len(rows)
    return B03Measurement(
        benchmark_id="B-03",
        result_schema_version=B03_RESULT_SCHEMA_VERSION,
        artifact_format=adapter.format_name,
        fixture_id=fixture_id,
        row_count=row_count,
        column_count=len(schema.fields),
        projected_column_count=len(projected.schema.fields),
        write_seconds=write_seconds,
        read_seconds=read_seconds,
        projected_read_seconds=projected_read_seconds,
        artifact_bytes=descriptor.byte_size,
        bytes_per_row=(descriptor.byte_size / row_count) if row_count else 0.0,
        write_rows_per_second=(row_count / write_seconds) if write_seconds > 0 else 0.0,
        read_rows_per_second=(row_count / read_seconds) if read_seconds > 0 else 0.0,
        projected_read_rows_per_second=(row_count / projected_read_seconds)
        if projected_read_seconds > 0 else 0.0,
        peak_write_bytes=peak_write,
        peak_read_bytes=peak_read,
        logical_content_hash=descriptor.logical_content_hash,
        artifact_byte_hash=descriptor.artifact_byte_hash,
        notes=(f"rows_read={len(full.rows)}",),
    )


def measure_all_enabled(
    directory: str,
    fixture_id: str,
    schema: LogicalSchema,
    rows: Sequence[Sequence[Any]],
    projection: tuple[str, ...],
) -> tuple[B03Measurement, ...]:
    """Measure the SAME fixture through every enabled adapter.

    The comparison is only fair because every adapter reports the same
    logical_content_hash for the same data. The caller may compare; this function
    does not, and returns no winner.
    """
    from .registry import enabled_adapters

    return tuple(
        measure_adapter(a, directory, fixture_id, schema, rows, projection)
        for a in enabled_adapters()
    )
