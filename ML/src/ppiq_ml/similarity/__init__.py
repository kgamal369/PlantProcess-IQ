"""Vector similarity search.

One contract, one permanent correctness oracle, and candidates measured against it.

Exact flat search is not a competitor to the approximate families. It defines what
the right answer is, so recall can be a measurement rather than an assertion. Every
approximate family sits behind the same interface and is replaceable without
anything above it changing.

Generations are immutable and identified by their content. Standard library only, so
the definition of a correct answer cannot move because a package did.
"""

from .contract import (
    GenerationManifest,
    IndexContractError,
    IndexNotBuiltError,
    IndexSealedError,
    Metric,
    SearchHit,
    SearchResult,
    VectorSimilarityIndex,
    compute_generation_id,
    evidence_handle,
    validate_population,
    vector_content_hash,
)
from .metrics import dot, norm, normalise, prepared, prepared_similarity, similarity
from .exact_flat import EXACT_FLAT_KIND, ExactFlatIndex, rank_hits
from .approximate import (
    DEFAULT_CELLS,
    DEFAULT_PROBES,
    PARTITIONED_PROBE_KIND,
    PartitionedProbeIndex,
)
from .recall import (
    LatencyProfile,
    RecallReport,
    ServingEligibility,
    percentile,
    recall_of,
    recall_probe,
)

__all__ = [
    "GenerationManifest", "IndexContractError", "IndexNotBuiltError", "IndexSealedError",
    "Metric", "SearchHit", "SearchResult", "VectorSimilarityIndex",
    "compute_generation_id", "evidence_handle", "validate_population",
    "vector_content_hash",
    "dot", "norm", "normalise", "prepared", "prepared_similarity", "similarity",
    "EXACT_FLAT_KIND", "ExactFlatIndex", "rank_hits",
    "DEFAULT_CELLS", "DEFAULT_PROBES", "PARTITIONED_PROBE_KIND", "PartitionedProbeIndex",
    "LatencyProfile", "RecallReport", "ServingEligibility", "percentile", "recall_of",
    "recall_probe",
]
