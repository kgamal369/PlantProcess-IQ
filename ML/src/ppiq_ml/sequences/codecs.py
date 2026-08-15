"""The compression seam.

A codec is named on the payload header and resolved by name when reading, so the
choice is a property of the artifact rather than of the code that happens to be
installed. Adding a codec is implementing this interface and registering it; nothing
in the reader or the writer changes.

Both codecs here are standard library. A payload that could only be read on a machine
with a particular package installed would not be an archive, and the whole point of
sealing a sequence is that it can still be read in five years.

No codec is selected. Which one to use is a benchmark's answer against representative
data on the target hardware, and B-04 measures rather than decides.
"""

from __future__ import annotations

import bz2
import zlib
from abc import ABC, abstractmethod
from typing import Mapping

from .dtypes import SequenceContractError


class Codec(ABC):
    @property
    @abstractmethod
    def name(self) -> str:
        """Stable identifier written onto the payload header."""

    @abstractmethod
    def compress(self, raw: bytes) -> bytes:
        ...

    @abstractmethod
    def decompress(self, raw: bytes, expected_bytes: int) -> bytes:
        ...


class StoredCodec(Codec):
    """No compression. The measurement floor every other codec is compared against."""

    @property
    def name(self) -> str:
        return "stored"

    def compress(self, raw: bytes) -> bytes:
        return raw

    def decompress(self, raw: bytes, expected_bytes: int) -> bytes:
        return raw


class DeflateCodec(Codec):
    def __init__(self, level: int = 6) -> None:
        if not 1 <= level <= 9:
            raise SequenceContractError(
                f"A deflate level must lie between 1 and 9; {level} was declared."
            )
        self._level = level

    @property
    def name(self) -> str:
        return "deflate"

    def compress(self, raw: bytes) -> bytes:
        return zlib.compress(raw, self._level)

    def decompress(self, raw: bytes, expected_bytes: int) -> bytes:
        try:
            return zlib.decompress(raw)
        except zlib.error as broken:
            raise SequenceContractError(
                f"A chunk could not be decompressed: {broken}. The bytes are not a "
                "valid payload of the codec the header declares."
            ) from broken


class BlockSortCodec(Codec):
    """Slower and usually smaller. Present so the seam carries more than one shape."""

    @property
    def name(self) -> str:
        return "blocksort"

    def compress(self, raw: bytes) -> bytes:
        return bz2.compress(raw)

    def decompress(self, raw: bytes, expected_bytes: int) -> bytes:
        try:
            return bz2.decompress(raw)
        except (OSError, ValueError) as broken:
            raise SequenceContractError(
                f"A chunk could not be decompressed: {broken}. The bytes are not a "
                "valid payload of the codec the header declares."
            ) from broken


def enabled_codecs() -> tuple[Codec, ...]:
    """Every codec, in a stable order for reproducible measurement."""
    return (StoredCodec(), DeflateCodec(), BlockSortCodec())


def codec_for(name: str) -> Codec:
    """Resolve the codec a payload header names."""
    for codec in enabled_codecs():
        if codec.name == name:
            return codec
    raise SequenceContractError(
        f"No codec named '{name}' is enabled. Enabled: "
        + ", ".join(c.name for c in enabled_codecs())
        + ". A payload written by a codec this build does not carry cannot be read, "
        "and guessing would produce numbers rather than a refusal."
    )


def default_codec() -> Codec:
    """There is no default, and asking for one is an error rather than a guess."""
    raise SequenceContractError(
        "No codec has been selected. T-170 enables several and deliberately does not "
        "choose between them. Benchmark B-04 measures them against representative data "
        "on the target hardware. Call codec_for() with an explicit name."
    )


def codec_names() -> Mapping[str, str]:
    return {c.name: type(c).__name__ for c in enabled_codecs()}
