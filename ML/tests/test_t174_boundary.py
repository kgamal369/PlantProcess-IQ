"""T-174 boundary guards.

The model-layer guards written for T-175 already scan this package for third-party
dependencies, production schema names, network access and encoding, so they are not
repeated here. What is added is what is specific to a novelty family: that every
honest refusal it declares is reachable, and that no answer is written into the source.
"""

import ast
import os
import unittest

from ppiq_ml.models.mf03_novelty import (
    NoveltyRefusalCode,
    RobustDeviationBaseline,
    evaluate_eligibility,
)

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
NOVELTY = os.path.join(SRC, "ppiq_ml", "models", "mf03_novelty")

FEATURES = ("measurement_alpha", "measurement_beta", "measurement_gamma")


def novelty_files():
    for base, _dirs, names in os.walk(NOVELTY):
        for name in names:
            if name.endswith(".py"):
                yield os.path.join(base, name)


def read(path):
    with open(path, encoding="ascii") as handle:
        return handle.read()


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


class EveryDeclaredRefusalIsReachable(unittest.TestCase):
    """A refusal state nothing can produce is documentation, not a gate."""

    def reached(self):
        produced = set()

        small_ids = [f"unit_{i:03d}" for i in range(5)]
        small_rows = [[float(i), float(i) + 1.0, 2.0] for i in range(5)]
        produced.add(evaluate_eligibility(small_ids, small_rows, FEATURES).refusal.code)

        flat_ids = [f"unit_{i:03d}" for i in range(60)]
        flat_rows = [[1.0, 2.0, 3.0] for _ in range(60)]
        produced.add(evaluate_eligibility(flat_ids, flat_rows, FEATURES).refusal.code)

        few_ids = [f"unit_{i:03d}" for i in range(60)]
        few_rows = [[float(i % 3), float(i % 3), float(i % 3) + 1.0] for i in range(60)]
        produced.add(evaluate_eligibility(few_ids, few_rows, FEATURES).refusal.code)

        good_ids = [f"unit_{i:03d}" for i in range(60)]
        good_rows = [[float(i), float(i) * 1.5, float(i) + 3.0] for i in range(60)]
        produced.add(evaluate_eligibility(good_ids, good_rows, FEATURES).refusal.code)
        return produced

    def test_each_population_shape_produces_its_own_named_refusal(self):
        produced = self.reached()
        for expected in (
            NoveltyRefusalCode.NONE,
            NoveltyRefusalCode.TOO_FEW_REFERENCE_UNITS,
            NoveltyRefusalCode.DEGENERATE_POPULATION,
            NoveltyRefusalCode.TOO_FEW_DISTINCT_UNITS,
        ):
            self.assertIn(expected, produced)

    def test_no_two_population_shapes_collapse_into_one_reason(self):
        self.assertEqual(4, len(self.reached()))

    def test_every_refusal_carries_a_required_and_an_observed_number(self):
        small_ids = [f"unit_{i:03d}" for i in range(5)]
        small_rows = [[float(i), float(i) + 1.0, 2.0] for i in range(5)]
        result = RobustDeviationBaseline().evaluate(small_ids, small_rows, FEATURES, 0.95, 7)
        self.assertIsNotNone(result.refusal.required)
        self.assertIsNotNone(result.refusal.observed)
        self.assertTrue(result.refusal.reason.strip())


class NoAnswerIsWrittenIntoTheSource(unittest.TestCase):
    def test_no_fixture_identifier_appears_in_the_novelty_source(self):
        from tests.test_t174_novelty_runtime import OUTLIER_IDS, normal_and_outlier_population

        ids, _ = normal_and_outlier_population()
        forbidden = set(OUTLIER_IDS) | {ids[0], ids[-1]}
        for path in novelty_files():
            body = read(path)
            for identifier in forbidden:
                self.assertNotIn(identifier, body, os.path.basename(path))

    def test_no_module_level_literal_is_named_for_a_score_or_an_expected_unit(self):
        offences = []
        for path in novelty_files():
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
                    for token in ("score", "outlier", "expected", "answer", "flag"):
                        if token in lowered:
                            offences.append(f"{os.path.basename(path)}:{name}")
        self.assertEqual([], offences)

    def test_the_source_never_reaches_into_a_test_module(self):
        for path in novelty_files():
            lowered = stripped_source(path).lower()
            for token in ("tests.", "normal_and_outlier", "unittest"):
                self.assertNotIn(token, lowered, os.path.basename(path))

    def test_the_only_use_of_the_word_fixture_is_the_provenance_label(self):
        """The SAFE-NOW rule requires the source to declare that its reference
        population is a fixture contract rather than production authority. That
        declaration is the point; knowledge of a particular fixture is the defect."""
        label = "fixture_declared_typed_contract"
        for path in novelty_files():
            lowered = stripped_source(path).lower()
            self.assertEqual(
                lowered.count("fixture"),
                lowered.count(label),
                f"{os.path.basename(path)} uses the word outside the provenance label",
            )

    def test_the_novelty_family_needs_no_numerical_package(self):
        """The floor must run on a machine with no ML stack installed."""
        found = []
        for path in novelty_files():
            tree = ast.parse(read(path), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    found += [a.name.split(".")[0] for a in node.names]
                elif isinstance(node, ast.ImportFrom) and node.module and node.level == 0:
                    found.append(node.module.split(".")[0])
        for package in ("numpy", "lightgbm", "scipy", "sklearn"):
            self.assertNotIn(package, found)

    def test_the_density_candidate_reuses_the_sealed_exact_index(self):
        """One definition of nearest in the repository, not two."""
        body = read(os.path.join(NOVELTY, "candidate.py"))
        self.assertIn("ExactFlatIndex", body)
        self.assertNotIn("def _nearest", body)


if __name__ == "__main__":
    unittest.main()
