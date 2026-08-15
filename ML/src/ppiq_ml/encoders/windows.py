"""Turning a sealed sequence payload into fixed-length windows.

WHERE THE DATA MAY COME FROM. A sealed typed sequence artifact, and nothing else.
This module is the only path into the encoder, and it opens a payload file. It has no
database client, no feature store client and no connection string, so there is no
route by which live data could reach a model through this package.

WHY THE INPUT IDENTITY IS COMPUTED HERE. Two training runs are the same run only if
they saw the same windows. The identity therefore covers the payload's own content
hash, the channels selected, their order, and the windowing that produced the
windows. Change any of those and the encoder was fitted on different input, and says so.
"""

from __future__ import annotations

import hashlib
import json
from typing import Iterator, Sequence

from ..sequences import SequenceManifest, iter_chunks, read_manifest
from .contract import ChannelSet, EncoderContractError


def training_input_identity(
    payload_content_hash: str,
    channel_set: ChannelSet,
    window_steps: int,
    stride: int,
    max_windows: int | None,
) -> str:
    """Identity of the exact windows an encoder was fitted on."""
    return hashlib.sha256(
        json.dumps(
            {
                "payload_content_hash": payload_content_hash,
                "channel_set": channel_set.to_dict(),
                "window_steps": window_steps,
                "stride": stride,
                "max_windows": max_windows,
            },
            sort_keys=True,
        ).encode("ascii")
    ).hexdigest()


def _channel_positions(manifest: SequenceManifest, channel_set: ChannelSet) -> list[int]:
    declared = manifest.schema.channel_names
    missing = [name for name in channel_set.names if name not in declared]
    if missing:
        raise EncoderContractError(
            "The sealed payload does not carry every channel the channel set declares: "
            + ", ".join(missing)
            + ". The encoder would be fitted on a different channel set from the one "
            "it records."
        )
    return [declared.index(name) for name in channel_set.names]


def iter_windows(
    path: str,
    channel_set: ChannelSet,
    window_steps: int,
    stride: int | None = None,
    max_windows: int | None = None,
    manifest: SequenceManifest | None = None,
) -> Iterator[tuple[tuple[float, ...], ...]]:
    """Yield one window at a time, channel-major, in the declared channel order.

    Windows are cut across chunk boundaries by carrying a tail forward, so the
    windowing does not depend on how the payload happened to be chunked. Only the
    tail and the current chunk are held, so a long payload still reads bounded.
    """
    if window_steps < 1:
        raise EncoderContractError(
            f"A window must carry at least one step; {window_steps} was declared."
        )
    step = stride if stride is not None else window_steps
    if step < 1:
        raise EncoderContractError(f"A stride must be at least 1; {step} was declared.")

    manifest = manifest or read_manifest(path)
    positions = _channel_positions(manifest, channel_set)

    carried: list[list[float]] = [[] for _ in positions]
    produced = 0

    for chunk in iter_chunks(path, manifest):
        for offset, position in enumerate(positions):
            carried[offset].extend(chunk.channel(position))

        while len(carried[0]) >= window_steps:
            if max_windows is not None and produced >= max_windows:
                return
            yield tuple(tuple(values[:window_steps]) for values in carried)
            produced += 1
            carried = [values[step:] for values in carried]

    if max_windows is not None and produced >= max_windows:
        return


def collect_windows(
    path: str,
    channel_set: ChannelSet,
    window_steps: int,
    stride: int | None = None,
    max_windows: int | None = None,
) -> tuple[tuple[tuple[float, ...], ...], ...]:
    """Materialise a bounded number of windows for training.

    Training holds its population, so this one is explicit about it: max_windows is
    the caller's declared ceiling rather than a surprise.
    """
    return tuple(
        iter_windows(path, channel_set, window_steps, stride, max_windows)
    )


def validate_windows(
    windows: Sequence[Sequence[Sequence[float]]], channel_set: ChannelSet
) -> int:
    """Structural checks shared by training and encoding. Returns the window length."""
    if not windows:
        raise EncoderContractError("There are no windows to work with.")
    channels = len(windows[0])
    if channels != channel_set.channel_count:
        raise EncoderContractError(
            f"A window carries {channels} channel(s) against {channel_set.channel_count} "
            "declared by the channel set."
        )
    steps = len(windows[0][0])
    if steps < 1:
        raise EncoderContractError("A window must carry at least one step.")
    for index, window in enumerate(windows):
        if len(window) != channels:
            raise EncoderContractError(
                f"Window {index} carries {len(window)} channel(s) and window 0 carries "
                f"{channels}."
            )
        for position, values in enumerate(window):
            if len(values) != steps:
                raise EncoderContractError(
                    f"Window {index} channel {position} carries {len(values)} step(s) "
                    f"against {steps}. A ragged window has no fixed shape to encode."
                )
    return steps
