"""B-04 measurement hook and result shape.

T-170 owns the HOOK and the RESULT SHAPE only. The common B-01 to B-09 framework is
T-182's, and nothing here attempts it: no scheduling, no result store, no comparison
across runs and no verdict.

No threshold is defined and no setting is selected. A chunk size and a codec are a
trade between payload size, write cost and how much memory a bounded read needs, and
the right point on that trade is a property of the target hardware and the data. This
module produces the numbers a later decision is made from, and makes none.
"""

from __future__ import annotations

import gc
import os
import time
import tracemalloc
from dataclasses import asdict, dataclass, field
from typing import Any, Callable, Iterable, Sequence

from .codecs import Codec, enabled_codecs
from .contract import SequenceDType, SequenceSchema
from .store import iter_chunks, read_manifest, write_sequence

B04_RESULT_SCHEMA_VERSION = 1


@dataclass(frozen=True)
class B04Measurement:
    """One chunk size and codec measured once. Machine readable, no verdict."""

    benchmark_id: str
    result_schema_version: int
    fixture_id: str
    codec_name: str
    chunk_steps: int
    channel_count: int
    dtype: str
    total_steps: int
    chunk_count: int
    write_seconds: float
    full_read_seconds: float
    single_channel_read_seconds: float
    stored_bytes: int
    uncompressed_bytes: int
    compression_ratio: float
    bytes_per_step: float
    write_steps_per_second: float
    read_steps_per_second: float
    peak_write_bytes: int
    peak_read_bytes: int
    peak_read_over_payload: float
    payload_content_hash: str
    notes: tuple[str, ...] = field(default_factory=tuple)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def _peak_bytes(work: Callable[[], Any]) -> tuple[Any, int]:
    gc.collect()
    tracemalloc.start()
    try:
        value = work()
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    return value, peak


def measure_setting(
    directory: str,
    fixture_id: str,
    channel_names: Sequence[str],
    dtype: SequenceDType,
    chunk_steps: int,
    codec: Codec,
    chunk_source: Callable[[int], Iterable[Sequence[Sequence[float]]]],
) -> B04Measurement:
    """Measure one chunk size and codec against one fixture.

    chunk_source is a callable rather than a materialised list on purpose. Handing
    this function the whole payload in memory would defeat what it is measuring.
    """
    os.makedirs(directory, exist_ok=True)
    schema = SequenceSchema(tuple(channel_names), dtype, chunk_steps)
    path = os.path.join(directory, f"{fixture_id}_{codec.name}_{chunk_steps}.ppiqseq")

    start = time.perf_counter()
    manifest, peak_write = _peak_bytes(
        lambda: write_sequence(path, fixture_id, schema, chunk_source(chunk_steps), codec)
    )
    write_seconds = time.perf_counter() - start

    def walk_all() -> int:
        steps = 0
        for chunk in iter_chunks(path, manifest):
            steps += chunk.steps
        return steps

    start = time.perf_counter()
    steps_read, peak_read = _peak_bytes(walk_all)
    full_read_seconds = time.perf_counter() - start

    start = time.perf_counter()
    position = 0
    total = 0
    for chunk in iter_chunks(path, manifest, verify=False):
        total += len(chunk.channel(position))
    single_channel_read_seconds = time.perf_counter() - start

    payload = manifest.uncompressed_bytes
    return B04Measurement(
        benchmark_id="B-04",
        result_schema_version=B04_RESULT_SCHEMA_VERSION,
        fixture_id=fixture_id,
        codec_name=codec.name,
        chunk_steps=chunk_steps,
        channel_count=schema.channel_count,
        dtype=dtype.value,
        total_steps=manifest.total_steps,
        chunk_count=manifest.chunk_count,
        write_seconds=write_seconds,
        full_read_seconds=full_read_seconds,
        single_channel_read_seconds=single_channel_read_seconds,
        stored_bytes=manifest.stored_bytes,
        uncompressed_bytes=payload,
        compression_ratio=manifest.compression_ratio,
        bytes_per_step=(manifest.stored_bytes / manifest.total_steps)
        if manifest.total_steps else 0.0,
        write_steps_per_second=(manifest.total_steps / write_seconds)
        if write_seconds > 0 else 0.0,
        read_steps_per_second=(steps_read / full_read_seconds)
        if full_read_seconds > 0 else 0.0,
        peak_write_bytes=peak_write,
        peak_read_bytes=peak_read,
        peak_read_over_payload=(peak_read / payload) if payload else 0.0,
        payload_content_hash=manifest.payload_content_hash,
        notes=(f"single_channel_values={total}",),
    )


def measure_settings(
    directory: str,
    fixture_id: str,
    channel_names: Sequence[str],
    dtype: SequenceDType,
    chunk_sizes: Sequence[int],
    chunk_source: Callable[[int], Iterable[Sequence[Sequence[float]]]],
    codecs: Sequence[Codec] | None = None,
) -> tuple[B04Measurement, ...]:
    """Measure the SAME data across several settings.

    The comparison is fair because every setting reports the same payload content
    hash for the same data. The caller may compare them; this function does not, and
    returns no winner.
    """
    chosen = tuple(codecs) if codecs is not None else enabled_codecs()
    return tuple(
        measure_setting(
            directory, fixture_id, channel_names, dtype, chunk_steps, codec, chunk_source
        )
        for chunk_steps in chunk_sizes
        for codec in chosen
    )
