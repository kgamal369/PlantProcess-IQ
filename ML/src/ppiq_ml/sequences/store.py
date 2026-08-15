"""Writing and reading a chunked sequence payload without materialising it.

THE FILE LAYOUT, AND WHY THE INDEX IS AT THE END.

    MAGIC
    chunk 0 bytes, chunk 1 bytes, ...
    footer, a JSON manifest carrying the chunk index
    footer length
    MAGIC

The index records where each chunk sits, so it cannot be written until the chunks
have been written and their offsets are known. Putting it at the front would mean
buffering the whole payload in memory to find out, which is the one thing this
library exists to avoid. Both ends carry the magic bytes, so a file truncated
anywhere after the first chunk is detected before anything is decoded.

BOUNDED IN BOTH DIRECTIONS. The writer consumes an iterable and holds one chunk at a
time. The reader yields one chunk at a time. Neither path allocates in proportion to
the payload, and a test measures that rather than asserting it.

MEMORY MAPPING WHERE SUPPORTED. The reader maps the file and slices chunk bytes out
of the mapping, so the operating system decides what stays resident. Where mapping is
unavailable it falls back to seeking reads, and the manifest is unaffected either way.
"""

from __future__ import annotations

import json
import mmap
import os
import struct
from typing import Any, Iterable, Iterator, Sequence

from .codecs import Codec, codec_for
from .contract import (
    FORMAT_VERSION,
    MAGIC,
    MANIFEST_KIND,
    ChunkCorruptError,
    ChunkIndexEntry,
    ChunkMissingError,
    PayloadTruncatedError,
    SequenceChunk,
    SequenceManifest,
    SequenceSchema,
    channel_digest_seed,
    chunk_content_hash,
    payload_byte_hash,
    payload_content_hash,
)
from .dtypes import SequenceContractError, decode, encode, item_bytes

_FOOTER_LENGTH_BYTES = 8
_TRAILER_BYTES = _FOOTER_LENGTH_BYTES + len(MAGIC)


def _encode_chunk(schema: SequenceSchema, channels: Sequence[Sequence[float]]) -> bytes:
    """Channel-major layout: every value of channel zero, then channel one, and so on.

    Channel-major rather than interleaved because a reader that wants one channel can
    then take a contiguous slice. Interleaving would make every such read touch the
    whole chunk.
    """
    if len(channels) != schema.channel_count:
        raise SequenceContractError(
            f"A chunk carries {len(channels)} channel(s) against {schema.channel_count} "
            "declared."
        )
    steps = len(channels[0])
    for position, values in enumerate(channels):
        if len(values) != steps:
            raise SequenceContractError(
                f"Channel {position} carries {len(values)} step(s) and channel 0 carries "
                f"{steps}. Every channel in a chunk covers the same steps."
            )
    if steps == 0:
        raise SequenceContractError("A chunk must carry at least one step.")
    if steps > schema.chunk_steps:
        raise SequenceContractError(
            f"A chunk carries {steps} step(s) against a declared chunk size of "
            f"{schema.chunk_steps}."
        )
    return b"".join(encode(schema.dtype, values) for values in channels)


def _decode_chunk(
    schema: SequenceSchema, ordinal: int, first_step: int, steps: int, raw: bytes
) -> SequenceChunk:
    width = steps * item_bytes(schema.dtype)
    channels = tuple(
        decode(schema.dtype, raw[position * width : (position + 1) * width])
        for position in range(schema.channel_count)
    )
    return SequenceChunk(ordinal=ordinal, first_step=first_step, steps=steps, channels=channels)


def write_sequence(
    path: str,
    sequence_id: str,
    schema: SequenceSchema,
    chunks: Iterable[Sequence[Sequence[float]]],
    codec: Codec,
) -> SequenceManifest:
    """Seal a payload from an iterable of chunks, holding one chunk at a time."""
    directory = os.path.dirname(os.path.abspath(path))
    if directory:
        os.makedirs(directory, exist_ok=True)

    index: list[ChunkIndexEntry] = []
    channel_digests = [channel_digest_seed() for _ in range(schema.channel_count)]
    total_steps = 0
    uncompressed = 0
    chunk_stored = 0

    temporary = path + ".partial"
    with open(temporary, "wb") as handle:
        handle.write(MAGIC)
        for ordinal, channels in enumerate(chunks):
            raw = _encode_chunk(schema, channels)
            steps = len(channels[0])
            width = steps * item_bytes(schema.dtype)
            for position in range(schema.channel_count):
                channel_digests[position].update(
                    raw[position * width : (position + 1) * width]
                )
            stored = codec.compress(raw)
            offset = handle.tell()
            handle.write(stored)

            digest = chunk_content_hash(raw)
            index.append(
                ChunkIndexEntry(
                    ordinal=ordinal,
                    first_step=total_steps,
                    steps=steps,
                    file_offset=offset,
                    stored_bytes=len(stored),
                    payload_bytes=len(raw),
                    content_hash=digest,
                )
            )
            total_steps += steps
            uncompressed += len(raw)
            chunk_stored += len(stored)

        if not index:
            handle.close()
            os.remove(temporary)
            raise SequenceContractError(
                "A sequence payload must carry at least one chunk. An empty payload "
                "would seal successfully and describe nothing."
            )

        footer = {
            "manifest_kind": MANIFEST_KIND,
            "format_version": FORMAT_VERSION,
            "sequence_id": sequence_id,
            "schema": schema.to_dict(),
            "codec_name": codec.name,
            "total_steps": total_steps,
            "chunks": [c.to_dict() for c in index],
            "payload_content_hash": payload_content_hash(
                schema, total_steps, [d.hexdigest() for d in channel_digests]
            ),
            "uncompressed_bytes": uncompressed,
            "chunk_stored_bytes": chunk_stored,
        }
        encoded = json.dumps(footer, indent=2, sort_keys=True).encode("ascii")
        handle.write(encoded)
        handle.write(struct.pack("<Q", len(encoded)))
        handle.write(MAGIC)

    os.replace(temporary, path)

    return SequenceManifest(
        manifest_kind=MANIFEST_KIND,
        format_version=FORMAT_VERSION,
        sequence_id=sequence_id,
        schema=schema,
        codec_name=codec.name,
        total_steps=total_steps,
        chunks=tuple(index),
        payload_content_hash=footer["payload_content_hash"],
        payload_byte_hash=payload_byte_hash(path),
        stored_bytes=os.path.getsize(path),
        uncompressed_bytes=uncompressed,
        chunk_stored_bytes=chunk_stored,
    )


def read_manifest(path: str, with_byte_hash: bool = False) -> SequenceManifest:
    """Read the footer without touching a single chunk.

    The byte hash is NOT computed by default. It costs a pass over the whole file,
    and opening a payload to look at its index is the most common thing a caller
    does. It is recorded on the manifest returned by write_sequence, where the file
    has just been written anyway, and can be asked for here explicitly.
    """
    size = os.path.getsize(path)
    if size < len(MAGIC) + _TRAILER_BYTES:
        raise PayloadTruncatedError(
            f"The file holds {size} byte(s), which is shorter than an empty payload's "
            "own structure. It was not written by this library or it was cut short."
        )
    with open(path, "rb") as handle:
        if handle.read(len(MAGIC)) != MAGIC:
            raise SequenceContractError(
                "The file does not begin with the sequence payload marker."
            )
        handle.seek(-len(MAGIC), os.SEEK_END)
        if handle.read(len(MAGIC)) != MAGIC:
            raise PayloadTruncatedError(
                "The file does not end with the sequence payload marker, so it was "
                "cut short after the chunks were written."
            )
        handle.seek(-_TRAILER_BYTES, os.SEEK_END)
        footer_length = struct.unpack("<Q", handle.read(_FOOTER_LENGTH_BYTES))[0]
        footer_start = size - _TRAILER_BYTES - footer_length
        if footer_start < len(MAGIC):
            raise PayloadTruncatedError(
                "The footer length points before the start of the payload, so the "
                "index cannot be read."
            )
        handle.seek(footer_start)
        raw = handle.read(footer_length)

    try:
        parsed = json.loads(raw.decode("ascii"))
    except (UnicodeDecodeError, json.JSONDecodeError) as broken:
        raise ChunkCorruptError(
            f"The payload index is not readable: {broken}"
        ) from broken

    parsed["payload_byte_hash"] = payload_byte_hash(path) if with_byte_hash else ""
    parsed["stored_bytes"] = size
    manifest = SequenceManifest.from_dict(parsed)

    if manifest.format_version != FORMAT_VERSION:
        raise SequenceContractError(
            f"The payload declares format version {manifest.format_version}; this "
            f"library reads version {FORMAT_VERSION}."
        )
    return manifest


def iter_chunks(
    path: str, manifest: SequenceManifest | None = None, verify: bool = True
) -> Iterator[SequenceChunk]:
    """Yield one decoded chunk at a time, in index order.

    verify is on by default. Every chunk is hashed as it is read and compared with
    the index, so corruption surfaces as a refusal at the chunk that carries it
    rather than as a number somewhere downstream.
    """
    manifest = manifest or read_manifest(path)
    codec = codec_for(manifest.codec_name)
    size = os.path.getsize(path)

    with open(path, "rb") as handle:
        mapping = None
        try:
            mapping = mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ)
        except (ValueError, OSError):
            mapping = None

        try:
            for entry in manifest.chunks:
                end = entry.file_offset + entry.stored_bytes
                if end > size:
                    raise ChunkMissingError(
                        f"Chunk {entry.ordinal} is described as {entry.stored_bytes} "
                        f"byte(s) at offset {entry.file_offset}, which runs past the end "
                        f"of a {size} byte file. The chunk is not present."
                    )
                if mapping is not None:
                    stored = mapping[entry.file_offset : end]
                else:
                    handle.seek(entry.file_offset)
                    stored = handle.read(entry.stored_bytes)

                raw = codec.decompress(bytes(stored), entry.payload_bytes)
                if len(raw) != entry.payload_bytes:
                    raise ChunkCorruptError(
                        f"Chunk {entry.ordinal} decompresses to {len(raw)} byte(s) "
                        f"against {entry.payload_bytes} recorded in the index."
                    )
                if verify:
                    actual = chunk_content_hash(raw)
                    if actual != entry.content_hash:
                        raise ChunkCorruptError(
                            f"Chunk {entry.ordinal} hashes to {actual[:16]} but the "
                            f"index records {entry.content_hash[:16]}. The bytes are "
                            "not the bytes that were sealed."
                        )
                yield _decode_chunk(
                    manifest.schema, entry.ordinal, entry.first_step, entry.steps, raw
                )
        finally:
            if mapping is not None:
                mapping.close()


def read_channel(
    path: str, channel: str, manifest: SequenceManifest | None = None
) -> Iterator[tuple[int, tuple[float, ...]]]:
    """Stream one channel, chunk by chunk. The other channels are never decoded."""
    manifest = manifest or read_manifest(path)
    if channel not in manifest.schema.channel_names:
        raise SequenceContractError(
            f"Channel '{channel}' is not declared by this payload. Declared: "
            + ", ".join(manifest.schema.channel_names)
        )
    position = manifest.schema.channel_names.index(channel)
    for chunk in iter_chunks(path, manifest):
        yield chunk.first_step, chunk.channel(position)


def verify_payload(path: str, manifest: SequenceManifest | None = None) -> dict[str, Any]:
    """Walk every chunk and confirm the payload is the one the index describes."""
    manifest = manifest or read_manifest(path)
    seen = 0
    steps = 0
    digests = [channel_digest_seed() for _ in range(manifest.schema.channel_count)]
    for chunk in iter_chunks(path, manifest):
        if chunk.ordinal != seen:
            raise ChunkCorruptError(
                f"Chunk {seen} was expected and chunk {chunk.ordinal} was read. The "
                "index order is not the payload order."
            )
        seen += 1
        steps += chunk.steps
        for position in range(manifest.schema.channel_count):
            digests[position].update(
                encode(manifest.schema.dtype, chunk.channel(position))
            )

    if seen != manifest.chunk_count:
        raise ChunkMissingError(
            f"The index describes {manifest.chunk_count} chunk(s) and {seen} were read."
        )
    if steps != manifest.total_steps:
        raise ChunkCorruptError(
            f"The chunks carry {steps} step(s) against {manifest.total_steps} recorded."
        )
    recomputed = payload_content_hash(
        manifest.schema, steps, [d.hexdigest() for d in digests]
    )
    if recomputed != manifest.payload_content_hash:
        raise ChunkCorruptError(
            "The payload content hash recomputed from the chunks does not match the "
            "one recorded in the index."
        )
    return {
        "chunks_read": seen,
        "steps_read": steps,
        "payload_content_hash": recomputed,
    }
