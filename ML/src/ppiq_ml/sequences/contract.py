"""What a sequence payload is, and what a reader is entitled to trust about it.

A sequence is multi-channel and long. The two facts that follow from that shape drive
everything here.

IT DOES NOT FIT IN MEMORY, SO IT IS CHUNKED. The payload is divided along the step
axis. A reader takes one chunk at a time and never needs room for the whole thing.
The chunk size is recorded rather than assumed, because a reader that guessed would
be reading a different artifact from the one that was written.

IT IS AN ARCHIVE, SO IT IS IMMUTABLE AND HASHED. Every chunk carries the hash of its
own uncompressed bytes, and the payload carries a hash over the ordered chunk hashes.
A missing chunk, a truncated file, a reordered index or a single flipped bit is
detected by the reader rather than returned as data.

WHERE THESE BYTES LIVE. In a file, or in object storage behind one. Numeric sequence
arrays are not stored in PostgreSQL. Persisting the manifest that points at a payload
is T-185's subject and no part of this library.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

from .dtypes import SequenceContractError, SequenceDType, item_bytes

#: File format identity. Written at both ends so a truncated file is obvious.
MAGIC = b"PPIQSEQ1"
FORMAT_VERSION = 1

#: The manifest that describes a payload. T-185 persists one of these; this library
#: only produces it.
MANIFEST_KIND = "ppiq.sequence.manifest/1"


@dataclass(frozen=True)
class SequenceSchema:
    """Channels, element type, and the chunking the payload was written with."""

    channel_names: tuple[str, ...]
    dtype: SequenceDType
    chunk_steps: int

    def __post_init__(self) -> None:
        if not self.channel_names:
            raise SequenceContractError("A sequence must declare at least one channel.")
        if len(set(self.channel_names)) != len(self.channel_names):
            raise SequenceContractError(
                "Channel names must be unique, or two channels could not be told apart."
            )
        for name in self.channel_names:
            if not str(name).strip():
                raise SequenceContractError("A channel name may not be empty.")
        if self.chunk_steps < 1:
            raise SequenceContractError(
                f"A chunk must carry at least one step; {self.chunk_steps} was declared."
            )

    @property
    def channel_count(self) -> int:
        return len(self.channel_names)

    def chunk_payload_bytes(self, steps: int) -> int:
        return steps * self.channel_count * item_bytes(self.dtype)

    def to_dict(self) -> dict[str, Any]:
        return {
            "channel_names": list(self.channel_names),
            "dtype": self.dtype.value,
            "chunk_steps": self.chunk_steps,
        }

    @staticmethod
    def from_dict(raw: Mapping[str, Any]) -> "SequenceSchema":
        for field in ("channel_names", "dtype", "chunk_steps"):
            if field not in raw:
                raise SequenceContractError(
                    f"The payload header declares no '{field}'."
                )
        return SequenceSchema(
            channel_names=tuple(str(n) for n in raw["channel_names"]),
            dtype=SequenceDType(str(raw["dtype"])),
            chunk_steps=int(raw["chunk_steps"]),
        )


@dataclass(frozen=True)
class ChunkIndexEntry:
    """Where one chunk sits, how large it is, and what it should hash to."""

    ordinal: int
    first_step: int
    steps: int
    file_offset: int
    stored_bytes: int
    payload_bytes: int
    content_hash: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "ordinal": self.ordinal,
            "first_step": self.first_step,
            "steps": self.steps,
            "file_offset": self.file_offset,
            "stored_bytes": self.stored_bytes,
            "payload_bytes": self.payload_bytes,
            "content_hash": self.content_hash,
        }

    @staticmethod
    def from_dict(raw: Mapping[str, Any]) -> "ChunkIndexEntry":
        return ChunkIndexEntry(
            ordinal=int(raw["ordinal"]),
            first_step=int(raw["first_step"]),
            steps=int(raw["steps"]),
            file_offset=int(raw["file_offset"]),
            stored_bytes=int(raw["stored_bytes"]),
            payload_bytes=int(raw["payload_bytes"]),
            content_hash=str(raw["content_hash"]),
        )


@dataclass(frozen=True)
class SequenceManifest:
    """The sealed description of one payload. Produced here, persisted by T-185."""

    manifest_kind: str
    format_version: int
    sequence_id: str
    schema: SequenceSchema
    codec_name: str
    total_steps: int
    chunks: tuple[ChunkIndexEntry, ...]
    payload_content_hash: str
    payload_byte_hash: str
    stored_bytes: int
    uncompressed_bytes: int
    chunk_stored_bytes: int = 0

    @property
    def chunk_count(self) -> int:
        return len(self.chunks)

    @property
    def compression_ratio(self) -> float:
        """Uncompressed chunk bytes over stored chunk bytes.

        Measured across the chunks alone. stored_bytes is the whole file and
        includes the index, so using it here would report a codec that saved
        nothing as having made the payload larger, which is true of the file and
        false of the codec.
        """
        if self.chunk_stored_bytes == 0:
            return 0.0
        return self.uncompressed_bytes / self.chunk_stored_bytes

    def to_dict(self) -> dict[str, Any]:
        return {
            "manifest_kind": self.manifest_kind,
            "format_version": self.format_version,
            "sequence_id": self.sequence_id,
            "schema": self.schema.to_dict(),
            "codec_name": self.codec_name,
            "total_steps": self.total_steps,
            "chunks": [c.to_dict() for c in self.chunks],
            "payload_content_hash": self.payload_content_hash,
            "payload_byte_hash": self.payload_byte_hash,
            "stored_bytes": self.stored_bytes,
            "uncompressed_bytes": self.uncompressed_bytes,
            "chunk_stored_bytes": self.chunk_stored_bytes,
        }

    @staticmethod
    def from_dict(raw: Mapping[str, Any]) -> "SequenceManifest":
        return SequenceManifest(
            manifest_kind=str(raw["manifest_kind"]),
            format_version=int(raw["format_version"]),
            sequence_id=str(raw["sequence_id"]),
            schema=SequenceSchema.from_dict(raw["schema"]),
            codec_name=str(raw["codec_name"]),
            total_steps=int(raw["total_steps"]),
            chunks=tuple(ChunkIndexEntry.from_dict(c) for c in raw["chunks"]),
            payload_content_hash=str(raw["payload_content_hash"]),
            payload_byte_hash=str(raw["payload_byte_hash"]),
            stored_bytes=int(raw["stored_bytes"]),
            uncompressed_bytes=int(raw["uncompressed_bytes"]),
            chunk_stored_bytes=int(raw.get("chunk_stored_bytes", 0)),
        )


@dataclass(frozen=True)
class SequenceChunk:
    """One decoded chunk. Channel-major: one tuple of values per channel."""

    ordinal: int
    first_step: int
    steps: int
    channels: tuple[tuple[float, ...], ...]

    def channel(self, position: int) -> tuple[float, ...]:
        return self.channels[position]


class ChunkCorruptError(SequenceContractError):
    """A chunk is present but its bytes are not the bytes the index describes."""


class ChunkMissingError(SequenceContractError):
    """The index describes a chunk the file does not contain."""


class PayloadTruncatedError(SequenceContractError):
    """The file ends before its own structure says it should."""


def chunk_content_hash(raw: bytes) -> str:
    """Hash of one chunk's UNCOMPRESSED bytes.

    Uncompressed on purpose. The same data written under two codecs must produce the
    same chunk hash, or a change of compression setting would look like a change of
    data and the B-04 comparison would be comparing two different things.
    """
    digest = hashlib.sha256()
    digest.update(b"ppiq.sequence.chunk/1\n")
    digest.update(raw)
    return digest.hexdigest()


def payload_content_hash(
    schema: SequenceSchema, total_steps: int, channel_digests: Sequence[str]
) -> str:
    """Identity of the data, independent of BOTH the codec and the chunk size.

    Taken from one running digest per channel rather than from the chunk hashes.
    The chunk hashes describe the physical layout and necessarily move when the
    chunk size moves; this must not, because B-04 compares chunk sizes against each
    other and a comparison whose identity changed with the setting would be
    comparing two different payloads and reporting it as a measurement.

    chunk_steps is excluded from the schema fingerprint here for the same reason.
    """
    digest = hashlib.sha256()
    digest.update(b"ppiq.sequence.payload/2\n")
    digest.update(
        json.dumps(
            {"channel_names": list(schema.channel_names), "dtype": schema.dtype.value},
            sort_keys=True,
        ).encode("ascii")
    )
    digest.update(f"|steps={total_steps}|".encode("ascii"))
    for value in channel_digests:
        digest.update(value.encode("ascii"))
        digest.update(b"\x1f")
    return digest.hexdigest()


def channel_digest_seed() -> "hashlib._Hash":
    """A fresh running digest for one channel's ordered values."""
    digest = hashlib.sha256()
    digest.update(b"ppiq.sequence.channel/1\n")
    return digest


#: Read size for whole-file hashing. Small enough that hashing a large payload
#: costs a fixed and modest amount of memory rather than a megabyte per buffer.
HASH_PIECE_BYTES = 128 * 1024


def payload_byte_hash(path: str) -> str:
    """Hash of the file bytes, read in bounded pieces."""
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for piece in iter(lambda: handle.read(HASH_PIECE_BYTES), b""):
            digest.update(piece)
    return digest.hexdigest()
