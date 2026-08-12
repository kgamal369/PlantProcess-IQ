"""Stage checkpointing. A failure at hour nineteen must not restart from zero."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, asdict
from typing import Any, Mapping


@dataclass(frozen=True)
class Checkpoint:
    job_id: str
    stage: str
    sequence: int
    state: Mapping[str, Any]

    def to_json(self) -> str:
        return json.dumps(asdict(self), indent=2, sort_keys=True)


class CheckpointStore:
    """One directory per job. The newest checkpoint by sequence wins."""

    def __init__(self, directory: str | None) -> None:
        self.directory = directory

    @property
    def enabled(self) -> bool:
        return bool(self.directory)

    def write(self, checkpoint: Checkpoint) -> str | None:
        if not self.directory:
            return None
        os.makedirs(self.directory, exist_ok=True)
        path = os.path.join(self.directory, f"stage-{checkpoint.sequence:04d}.json")
        # Write to a temporary name and move, so a crash mid-write cannot leave a
        # half-written checkpoint that a later run would trust.
        tmp = path + ".partial"
        with open(tmp, "w", encoding="ascii", newline="\n") as handle:
            handle.write(checkpoint.to_json())
        os.replace(tmp, path)
        return path

    def latest(self) -> Checkpoint | None:
        if not self.directory or not os.path.isdir(self.directory):
            return None
        names = sorted(n for n in os.listdir(self.directory) if n.endswith(".json"))
        if not names:
            return None
        with open(os.path.join(self.directory, names[-1]), encoding="ascii") as handle:
            raw = json.load(handle)
        return Checkpoint(
            job_id=str(raw["job_id"]),
            stage=str(raw["stage"]),
            sequence=int(raw["sequence"]),
            state=dict(raw.get("state", {})),
        )
