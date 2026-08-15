"""Reading a training population out of a sealed artifact.

MF-04 never reads a database, a table or a physical schema. Its entire view of the
customer's world is one sealed artifact produced upstream and named in the job spec,
which is what makes this family immune to a change in physical storage.

Only the columns the outcome contract declares are projected, and only the features
the leakage gate admitted. A column that was refused is never read, so it cannot
reach a model through an oversight later in the chain.
"""

from __future__ import annotations

from typing import Sequence

from ...artifacts.hashing import logical_content_hash
from ...artifacts.registry import adapter_for
from ...artifacts.schema import UnsupportedSchemaError
from .contract import Population
from .outcome import OutcomeDefinition


class PopulationContractError(Exception):
    """The artifact does not carry what the outcome contract declared."""


def load_population(
    uri: str,
    artifact_format: str,
    outcome: OutcomeDefinition,
    legal_features: Sequence[str],
) -> Population:
    """Read one sealed artifact into a typed population.

    The returned snapshot_identity is the format-independent logical hash of exactly
    the projected columns and rows, so a Parquet artifact and an Arrow IPC artifact
    carrying the same population produce the same identity.
    """
    adapter = adapter_for(artifact_format)
    projection = (outcome.grain_column, outcome.order_column, outcome.label_column) + tuple(
        legal_features
    )

    try:
        result = adapter.read(uri, projection=projection)
    except UnsupportedSchemaError as missing:
        raise PopulationContractError(
            f"The sealed artifact does not carry every column the outcome contract "
            f"declares. {missing}"
        ) from missing

    rows = result.rows
    grains = tuple(row[0] for row in rows)
    order_values = tuple(row[1] for row in rows)
    labels = tuple(row[2] for row in rows)
    feature_rows = tuple(tuple(row[3:]) for row in rows)

    return Population(
        outcome=outcome,
        feature_columns=tuple(legal_features),
        grains=grains,
        order_values=order_values,
        labels=labels,
        feature_rows=feature_rows,
        snapshot_identity=logical_content_hash(result.schema, rows),
    )
