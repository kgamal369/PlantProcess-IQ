"""The storage-format-independent artifact contract.

An adapter converts between the logical schema and one physical format. Nothing above
this line knows whether the bytes are Parquet or Arrow IPC.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass, asdict
from typing import Any, Sequence

from .schema import LogicalSchema


class ArtifactCorruptError(Exception):
    """The file is present but its bytes are not a valid artifact of this format."""


class ArtifactTruncatedError(ArtifactCorruptError):
    """The file ends before the format's own structure says it should."""


class ArtifactHashMismatchError(Exception):
    """The artifact is readable but is not the one the caller expected."""


@dataclass(frozen=True)
class ArtifactDescriptor:
    """What a sealed artifact is, recorded so a later reader can verify it."""

    artifact_id: str
    uri: str
    artifact_format: str
    logical_content_hash: str
    artifact_byte_hash: str
    byte_size: int
    row_count: int
    column_names: tuple[str, ...]
    schema_canonical: str

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass(frozen=True)
class ReadResult:
    """What a reader returns. Column order matches the request, not the file."""

    schema: LogicalSchema
    rows: tuple[tuple[Any, ...], ...]


class ColumnarArtifactAdapter(ABC):
    """One physical format behind the contract.

    Two obligations bind every implementation:

    Row order is preserved exactly as written. A training population is an ordered
    thing, and a reordering would change the logical content hash.

    A projection returns columns in the REQUESTED order, which is not necessarily the
    order they appear in the file.
    """

    @property
    @abstractmethod
    def format_name(self) -> str:
        """Stable identifier recorded on the descriptor, for example 'parquet'."""

    @property
    @abstractmethod
    def file_suffix(self) -> str:
        ...

    @abstractmethod
    def write(
        self,
        path: str,
        schema: LogicalSchema,
        rows: Sequence[Sequence[Any]],
        artifact_id: str,
    ) -> ArtifactDescriptor:
        """Seal an artifact and return its descriptor. Raises UnsupportedSchemaError
        BEFORE writing anything if the schema cannot be represented."""

    @abstractmethod
    def read(self, path: str, projection: tuple[str, ...] | None = None) -> ReadResult:
        """Read an artifact. Raises ArtifactCorruptError or ArtifactTruncatedError
        rather than returning partial data."""

    def verify(self, path: str, expected: ArtifactDescriptor) -> None:
        """Confirm the file is the artifact the caller was authorised against."""
        from .hashing import artifact_byte_hash

        actual = artifact_byte_hash(path)
        if actual != expected.artifact_byte_hash:
            raise ArtifactHashMismatchError(
                f"Artifact '{expected.artifact_id}' hashes to {actual[:16]} but its "
                f"descriptor declares {expected.artifact_byte_hash[:16]}. The bytes are "
                "not the ones the descriptor was written for."
            )
