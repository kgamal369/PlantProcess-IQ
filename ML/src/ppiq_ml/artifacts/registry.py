"""Both formats are enabled. Neither is selected.

T-169 deliberately does not choose between Parquet and Arrow IPC. The choice is
benchmark B-03's to make, against representative data on the target hardware, and
T-169 provides the measurement hook rather than the answer.
"""

from __future__ import annotations

from .arrow_ipc_adapter import ArrowIpcArtifactAdapter
from .contract import ColumnarArtifactAdapter
from .parquet_adapter import ParquetArtifactAdapter


class FormatNotSelectedError(Exception):
    """Raised when a caller asks for THE format before a benchmark has chosen one."""


def enabled_adapters() -> tuple[ColumnarArtifactAdapter, ...]:
    """Every adapter T-169 enables, in a stable order for reproducible measurement."""
    return (ParquetArtifactAdapter(), ArrowIpcArtifactAdapter())


def adapter_for(format_name: str) -> ColumnarArtifactAdapter:
    """Resolve an adapter by the format recorded on a descriptor."""
    for adapter in enabled_adapters():
        if adapter.format_name == format_name:
            return adapter
    raise FormatNotSelectedError(
        f"No enabled adapter for format '{format_name}'. Enabled: "
        + ", ".join(a.format_name for a in enabled_adapters())
    )


def default_adapter() -> ColumnarArtifactAdapter:
    """There is no default, and asking for one is an error rather than a guess."""
    raise FormatNotSelectedError(
        "No storage format has been selected. T-169 enables Parquet and Arrow IPC and "
        "deliberately does not choose between them. Benchmark B-03 selects the format "
        "against representative data on the target hardware. Call adapter_for() with an "
        "explicit format, or enabled_adapters() to measure both."
    )
