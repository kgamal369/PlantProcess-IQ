"""Chunked sequence payloads.

Immutable, chunked, typed numeric arrays with a compression seam, a chunk index and
content hashes. Written and read one chunk at a time, so neither path allocates in
proportion to the payload.

Numeric sequence arrays live in a payload file or the object storage behind one, and
never in PostgreSQL. Persisting the manifest that points at a payload is T-185's
subject and no part of this library.

No codec and no chunk size is selected. B-04 measures; a later decision chooses.
"""

from .dtypes import SequenceContractError, SequenceDType, decode, encode, item_bytes
from .codecs import (
    BlockSortCodec,
    Codec,
    DeflateCodec,
    StoredCodec,
    codec_for,
    codec_names,
    default_codec,
    enabled_codecs,
)
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
    chunk_content_hash,
    payload_byte_hash,
    payload_content_hash,
)
from .store import (
    iter_chunks,
    read_channel,
    read_manifest,
    verify_payload,
    write_sequence,
)
from .b04 import B04_RESULT_SCHEMA_VERSION, B04Measurement, measure_setting, measure_settings

__all__ = [
    "SequenceContractError", "SequenceDType", "decode", "encode", "item_bytes",
    "BlockSortCodec", "Codec", "DeflateCodec", "StoredCodec", "codec_for",
    "codec_names", "default_codec", "enabled_codecs",
    "FORMAT_VERSION", "MAGIC", "MANIFEST_KIND", "ChunkCorruptError", "ChunkIndexEntry",
    "ChunkMissingError", "PayloadTruncatedError", "SequenceChunk", "SequenceManifest",
    "SequenceSchema", "chunk_content_hash", "payload_byte_hash", "payload_content_hash",
    "iter_chunks", "read_channel", "read_manifest", "verify_payload", "write_sequence",
    "B04_RESULT_SCHEMA_VERSION", "B04Measurement", "measure_setting", "measure_settings",
]
