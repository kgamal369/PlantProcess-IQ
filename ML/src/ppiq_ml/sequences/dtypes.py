"""The numeric element types a sequence payload may carry.

Every layout is little-endian and explicitly sized. The standard library's array
module would be faster to write against, but two of its type codes change width
between platforms, and a payload whose byte length depends on the machine that wrote
it is not an artifact anybody can verify later.

The set is deliberately small. A type nobody has measured is a type nobody can
promise to read back.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass
from enum import Enum


class SequenceContractError(Exception):
    """The payload or the request cannot be interpreted under this contract."""


@dataclass(frozen=True)
class _Layout:
    code: str
    item_bytes: int


class SequenceDType(str, Enum):
    INT16 = "int16"
    INT32 = "int32"
    INT64 = "int64"
    FLOAT32 = "float32"
    FLOAT64 = "float64"


_LAYOUTS = {
    SequenceDType.INT16: _Layout("h", 2),
    SequenceDType.INT32: _Layout("i", 4),
    SequenceDType.INT64: _Layout("q", 8),
    SequenceDType.FLOAT32: _Layout("f", 4),
    SequenceDType.FLOAT64: _Layout("d", 8),
}


def item_bytes(dtype: SequenceDType) -> int:
    return _LAYOUTS[dtype].item_bytes


def encode(dtype: SequenceDType, values) -> bytes:
    """Pack values into little-endian bytes of the declared type."""
    layout = _LAYOUTS[dtype]
    try:
        return struct.pack("<%d%s" % (len(values), layout.code), *values)
    except struct.error as invalid:
        raise SequenceContractError(
            f"A value could not be represented as {dtype.value}: {invalid}"
        ) from invalid


def decode(dtype: SequenceDType, raw: bytes) -> tuple:
    """Unpack bytes of the declared type. Refuses a length that does not divide."""
    layout = _LAYOUTS[dtype]
    if len(raw) % layout.item_bytes != 0:
        raise SequenceContractError(
            f"{len(raw)} byte(s) do not divide into {dtype.value} items of "
            f"{layout.item_bytes} byte(s). The payload is not what its header declares."
        )
    count = len(raw) // layout.item_bytes
    return struct.unpack("<%d%s" % (count, layout.code), raw)


def rounds_to_float32(dtype: SequenceDType) -> bool:
    """True where a stored value is not the value that was handed in.

    Thirty-two bit storage halves the payload and loses precision. Callers that
    compare a read against what they wrote need to know which of the two is
    happening, so it is stated here rather than discovered in a failing assertion.
    """
    return dtype == SequenceDType.FLOAT32
