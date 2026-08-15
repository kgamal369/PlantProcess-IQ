"""T-173 part three: the boundaries of the similarity layer, enforced by tests.

The oracle defines what a correct answer is. If it acquired a numerical dependency,
the definition of correct would move whenever that package changed its summation
order, and every recall figure ever recorded would silently mean something else.
That is the property these guards protect.
"""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
SIMILARITY = os.path.join(SRC, "ppiq_ml", "similarity")

STDLIB_OR_OWN = {
    "__future__", "abc", "dataclasses", "enum", "hashlib", "json", "math", "time",
    "tracemalloc", "typing", "ppiq_ml",
}


def similarity_files():
    for base, _dirs, names in os.walk(SIMILARITY):
        for name in names:
            if name.endswith(".py"):
                yield os.path.join(base, name)


def read(path):
    with open(path, encoding="ascii") as handle:
        return handle.read()


def imported_top_level(path):
    tree = ast.parse(read(path), filename=path)
    found = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            found += [alias.name.split(".")[0] for alias in node.names]
        elif isinstance(node, ast.ImportFrom) and node.module and node.level == 0:
            found.append(node.module.split(".")[0])
    return found


def stripped_source(path):
    """Code with comments and docstrings removed, so a guard judges behaviour."""
    text = read(path)
    tree = ast.parse(text, filename=path)
    docstrings = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.Module, ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)):
            doc = ast.get_docstring(node, clean=False)
            if doc:
                docstrings.add(doc)
    body = "\n".join(l for l in text.split("\n") if not l.strip().startswith("#"))
    for doc in docstrings:
        body = body.replace(doc, "")
    return body


class TheSimilarityLayerCarriesNoDependency(unittest.TestCase):
    def test_no_third_party_package_is_imported_anywhere_in_the_layer(self):
        found = set()
        for path in similarity_files():
            found.update(imported_top_level(path))
        external = sorted(found - STDLIB_OR_OWN)
        self.assertEqual([], external, f"The similarity layer gained a dependency: {external}")

    def test_the_oracle_in_particular_names_no_numerical_package(self):
        """What defines a correct answer must not move because a package did."""
        body = stripped_source(os.path.join(SIMILARITY, "exact_flat.py")).lower()
        for package in ("numpy", "faiss", "scipy", "lightgbm", "torch"):
            self.assertNotIn(package, body)

    def test_no_approximate_family_is_named_in_the_contract(self):
        """The contract must not know which candidate families exist."""
        body = stripped_source(os.path.join(SIMILARITY, "contract.py")).lower()
        for family in ("faiss", "hnsw", "ivf", "annoy", "scann"):
            self.assertNotIn(family, body)


class TheContractIsSatisfiedByEveryImplementation(unittest.TestCase):
    def test_both_families_implement_the_same_interface(self):
        from ppiq_ml.similarity import (
            ExactFlatIndex,
            PartitionedProbeIndex,
            VectorSimilarityIndex,
        )

        for family in (ExactFlatIndex, PartitionedProbeIndex):
            self.assertTrue(issubclass(family, VectorSimilarityIndex))
            for member in ("index_kind", "is_exact", "manifest", "build", "search", "extend"):
                self.assertTrue(hasattr(family, member), f"{family.__name__}.{member}")

    def test_exactly_one_implementation_declares_itself_exact(self):
        from ppiq_ml.similarity import ExactFlatIndex, PartitionedProbeIndex

        exact = [f for f in (ExactFlatIndex, PartitionedProbeIndex) if f().is_exact]
        self.assertEqual([ExactFlatIndex], exact)

    def test_a_third_family_can_satisfy_the_contract_without_touching_it(self):
        """Replaceability, proven by writing one rather than by asserting it."""
        from ppiq_ml.similarity import (
            ExactFlatIndex,
            Metric,
            VectorSimilarityIndex,
            recall_probe,
        )
        from ppiq_ml.similarity.contract import (
            GenerationManifest,
            SearchResult,
            compute_generation_id,
            evidence_handle,
            validate_population,
            vector_content_hash,
        )
        from ppiq_ml.similarity.exact_flat import rank_hits
        from ppiq_ml.similarity.metrics import prepared, prepared_similarity

        class FirstHalfOnlyIndex(VectorSimilarityIndex):
            """A candidate that scans only the first half of the population.

            Not useful. Deliberately so: it exists to show that a family the contract
            has never heard of plugs in and is measured by the same probe.
            """

            def __init__(self):
                self._manifest = None
                self._ids = ()
                self._prepared = ()
                self._metric = Metric.COSINE

            @property
            def index_kind(self):
                return "first_half_only"

            @property
            def is_exact(self):
                return False

            @property
            def manifest(self):
                return self._manifest

            def build(self, ids, vectors, metric=Metric.COSINE, parameters=None,
                      parent_generation_id=None):
                dimension = validate_population(ids, vectors)
                self._metric = metric
                self._ids = tuple(str(i) for i in ids)
                raw = tuple(tuple(float(v) for v in row) for row in vectors)
                self._prepared = tuple(prepared(metric, row) for row in raw)
                content = vector_content_hash(self._ids, raw)
                self._manifest = GenerationManifest(
                    generation_id=compute_generation_id(
                        self.index_kind, metric, dimension, len(self._ids), content,
                        dict(parameters or {}), parent_generation_id,
                    ),
                    index_kind=self.index_kind,
                    metric=metric,
                    dimension=dimension,
                    vector_count=len(self._ids),
                    vector_content_hash=content,
                    parameters=dict(parameters or {}),
                    build_seconds=0.0,
                    peak_build_bytes=1,
                    parent_generation_id=parent_generation_id,
                )
                return self._manifest

            def search(self, queries, k):
                half = max(1, len(self._ids) // 2)
                results = []
                for position, query in enumerate(queries):
                    probe = prepared(self._metric, query)
                    scored = [
                        (self._ids[i], prepared_similarity(self._metric, probe, self._prepared[i]))
                        for i in range(half)
                    ]
                    hits = rank_hits(scored, k)
                    results.append(
                        SearchResult(
                            query_position=position,
                            hits=hits,
                            generation_id=self._manifest.generation_id,
                            evidence_handle=evidence_handle(
                                self._manifest.generation_id, query, k, hits
                            ),
                        )
                    )
                return tuple(results)

            def extend(self, ids, vectors):
                raise NotImplementedError

        ids = [f"vector_{i:03d}" for i in range(40)]
        vectors = [[float(i), float(40 - i), 1.0] for i in range(40)]

        oracle = ExactFlatIndex()
        oracle.build(ids, vectors, Metric.COSINE)
        stranger = FirstHalfOnlyIndex()
        stranger.build(ids, vectors, Metric.COSINE)

        report = recall_probe(stranger, oracle, vectors[-10:], k=5, recall_floor=0.90)
        self.assertEqual("first_half_only", report.candidate_kind)
        self.assertLess(report.recall_at_k, 0.90)
        self.assertEqual("not_eligible_to_serve", report.eligibility.value)


class TheSimilarityLayerTouchesNoProductionSurface(unittest.TestCase):
    def test_it_persists_nothing_and_reaches_nowhere(self):
        forbidden = {
            "socket", "subprocess", "http", "urllib", "requests", "psycopg", "sqlite3",
            "pathlib", "shutil", "tempfile", "os",
        }
        offences = []
        for path in similarity_files():
            for name in imported_top_level(path):
                if name in forbidden:
                    offences.append(f"{os.path.basename(path)} imports {name}")
        self.assertEqual([], offences)

    def test_it_declares_no_activation_or_registration_entry_point(self):
        forbidden_stems = {"activate", "register", "deploy", "serve", "publish", "persist"}
        offences = []
        for path in similarity_files():
            tree = ast.parse(read(path), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    stem = node.name.lstrip("_").split("_")[0].lower()
                    if stem in forbidden_stems:
                        offences.append(f"{os.path.basename(path)}:{node.name}")
        self.assertEqual([], offences)

    def test_no_production_schema_table_or_route_is_named(self):
        forbidden = ("ppiq_" + "app", "dump_" + "store", "presentation", "connection" + "_string")
        offences = []
        for path in similarity_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                if token in body:
                    offences.append(f"{os.path.basename(path)} names '{token}'")
        self.assertEqual([], offences)

    def test_every_similarity_source_file_is_ascii_with_unix_line_endings(self):
        for path in similarity_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            self.assertNotEqual(b"\xef\xbb\xbf", raw[:3], os.path.basename(path))
            self.assertNotIn(b"\r\n", raw, os.path.basename(path))
            self.assertFalse(
                any(b > 126 for b in raw if b not in (9, 10)), os.path.basename(path)
            )


class TheCandidateKnowsNothingAboutAnyFixture(unittest.TestCase):
    """Genericity. An index tuned to the population it is measured on measures nothing.

    The forbidden tokens are taken from the fixtures themselves at run time rather
    than typed here, so the guard cannot drift out of step with what the fixtures
    actually contain.
    """

    def source_bodies(self):
        return {os.path.basename(p): read(p) for p in similarity_files()}

    def test_no_fixture_identifier_appears_in_the_similarity_source(self):
        from tests.test_t173_index_contract import synthetic_population
        from tests.test_t173_recall_and_eligibility import clustered_population

        identifiers = set()
        for builder in (synthetic_population, clustered_population):
            ids, _ = builder()
            identifiers.update({ids[0], ids[1], ids[-1]})

        for name, body in self.source_bodies().items():
            for identifier in identifiers:
                self.assertNotIn(identifier, body, f"{name} names the fixture id {identifier}")

    def test_the_source_carries_no_expected_recall_or_neighbour_constant(self):
        """A declared answer in the implementation would make recall self-fulfilling."""
        # Only module-level assignments of a literal are judged. A value computed
        # from the data is a measurement; a literal declared in the source is an
        # answer written down in advance, and only the second is the defect.
        offences = []
        for path in similarity_files():
            tree = ast.parse(read(path), filename=path)
            for node in tree.body:
                targets = []
                if isinstance(node, ast.Assign):
                    targets = [t.id for t in node.targets if isinstance(t, ast.Name)]
                elif isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name):
                    targets = [node.target.id]
                if not targets or not isinstance(getattr(node, "value", None), ast.Constant):
                    continue
                for name in targets:
                    lowered = name.lower()
                    for token in ("recall", "expected", "neighbour", "neighbor", "answer"):
                        if token in lowered:
                            offences.append(f"{os.path.basename(path)}:{name}")
        self.assertEqual([], offences)

    def test_the_source_never_mentions_a_fixture_or_a_test(self):
        for name, body in self.source_bodies().items():
            lowered = body.lower()
            for token in ("fixture", "synthetic_population", "clustered_population", "tests."):
                self.assertNotIn(token, lowered, f"{name} mentions '{token}'")

    def test_the_declared_defaults_do_not_match_any_fixture_shape(self):
        """If the default cell count happened to equal the fixture cluster count,
        a good recall figure would be an accident of the fixture rather than a
        property of the index."""
        from ppiq_ml.similarity import DEFAULT_CELLS, DEFAULT_PROBES
        from tests.test_t173_recall_and_eligibility import clustered_population

        for clusters in (6, 12):
            ids, _ = clustered_population(clusters=clusters, per_cluster=4)
            self.assertNotEqual(clusters, DEFAULT_CELLS)
            self.assertNotEqual(clusters, DEFAULT_PROBES)
            self.assertGreater(len(ids), 0)


class TheSameImplementationHandlesAnUnrelatedPopulation(unittest.TestCase):
    """One genericity falsification, not a benchmark programme.

    A second population that differs from the first in dimension, scale, group
    count, identifier scheme and metric. Nothing in the implementation or its
    parameters is changed for it.
    """

    def lattice_population(self):
        from ppiq_ml.similarity import Metric

        ids, vectors = [], []
        dimension = 24
        for group in range(5):
            for member in range(33):
                base = [0.0] * dimension
                for axis in range(dimension):
                    base[axis] = 6.0 * ((group + 1) if axis % 5 == group else 0.0)
                    base[axis] += member * 0.13 + axis * 0.05 + (member * axis) % 3 * 0.21
                ids.append("u-%d-%02d" % (group, member))
                vectors.append(base)
        return ids, vectors, Metric.EUCLIDEAN

    def test_the_probe_behaves_the_same_way_on_a_population_it_has_never_seen(self):
        from ppiq_ml.similarity import (
            ExactFlatIndex,
            PartitionedProbeIndex,
            ServingEligibility,
            recall_probe,
        )

        ids, vectors, metric = self.lattice_population()
        oracle = ExactFlatIndex()
        oracle.build(ids, vectors, metric)
        queries = vectors[::7]

        observed = []
        for probes in (1, 3, 10):
            candidate = PartitionedProbeIndex()
            candidate.build(ids, vectors, metric, {"cells": 10, "probes": probes})
            observed.append(
                recall_probe(candidate, oracle, queries, k=5, recall_floor=0.90)
            )

        self.assertEqual(165, oracle.manifest.vector_count)
        self.assertEqual(24, oracle.manifest.dimension)
        self.assertEqual(metric, oracle.manifest.metric)

        recalls = [r.recall_at_k for r in observed]
        self.assertEqual(recalls, sorted(recalls))
        self.assertEqual(1.0, recalls[-1])
        self.assertEqual(ServingEligibility.ELIGIBLE, observed[-1].eligibility)
        self.assertLessEqual(recalls[0], recalls[-1])

    def test_a_weakened_build_is_refused_on_the_second_population_too(self):
        from ppiq_ml.similarity import (
            ExactFlatIndex,
            PartitionedProbeIndex,
            ServingEligibility,
            recall_probe,
        )

        ids, vectors, metric = self.lattice_population()
        oracle = ExactFlatIndex()
        oracle.build(ids, vectors, metric)
        weakened = PartitionedProbeIndex()
        weakened.build(ids, vectors, metric, {"cells": 30, "probes": 1})

        report = recall_probe(
            weakened, oracle, vectors[::7], k=10, recall_floor=0.90
        )
        self.assertLess(report.recall_at_k, 0.90)
        self.assertEqual(ServingEligibility.NOT_ELIGIBLE_TO_SERVE, report.eligibility)

    def test_a_stored_vector_is_its_own_nearest_neighbour_here_as_well(self):
        from ppiq_ml.similarity import ExactFlatIndex

        ids, vectors, metric = self.lattice_population()
        oracle = ExactFlatIndex()
        oracle.build(ids, vectors, metric)
        for position in (0, 47, 164):
            hit = oracle.search([vectors[position]], k=1)[0].hits[0]
            self.assertEqual(ids[position], hit.vector_id)


if __name__ == "__main__":
    unittest.main()
