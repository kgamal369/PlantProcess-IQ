"""T-176 part three: the boundaries of a pure kernel, enforced by tests.

A promotion kernel that acquires a dependency, a file handle or a registry call
stops being reproducible. Each guard here is scoped to the governance layer and
assembles the tokens it forbids so it cannot match itself.
"""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
GOVERNANCE = os.path.join(SRC, "ppiq_ml", "governance")

STDLIB_OR_OWN = {
    "__future__", "abc", "ast", "dataclasses", "enum", "hashlib", "json", "math",
    "os", "sys", "typing", "ppiq_ml",
}


def governance_files():
    for root, _dirs, names in os.walk(GOVERNANCE):
        for name in names:
            if name.endswith(".py"):
                yield os.path.join(root, name)


def imported_top_level(path):
    with open(path, encoding="ascii") as handle:
        tree = ast.parse(handle.read(), filename=path)
    found = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            found += [alias.name.split(".")[0] for alias in node.names]
        elif isinstance(node, ast.ImportFrom) and node.module and node.level == 0:
            found.append(node.module.split(".")[0])
    return found


def stripped_source(path):
    with open(path, encoding="ascii") as handle:
        text = handle.read()
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


class ThePromotionKernelIsPure(unittest.TestCase):
    def test_it_carries_no_third_party_dependency_at_all(self):
        """A decision that depended on a package would carry its version into the answer."""
        found = set()
        for path in governance_files():
            found.update(imported_top_level(path))
        external = sorted(found - STDLIB_OR_OWN)
        self.assertEqual([], external, f"The governance layer gained a dependency: {external}")

    def test_it_does_not_import_the_model_family_it_judges(self):
        """The kernel reads recorded numbers; it must not be able to retrain anything."""
        for path in governance_files():
            with open(path, encoding="ascii") as handle:
                text = handle.read()
            self.assertNotIn("models.mf04", text, os.path.basename(path))
            self.assertNotIn("lightgbm", stripped_source(path), os.path.basename(path))

    def test_it_opens_no_file_and_reaches_no_network(self):
        forbidden_calls = {"open", "input"}
        forbidden_imports = {
            "socket", "subprocess", "http", "urllib", "requests", "shutil", "tempfile",
            "pathlib", "psycopg", "sqlite3",
        }
        offences = []
        for path in governance_files():
            for name in imported_top_level(path):
                if name in forbidden_imports:
                    offences.append(f"{os.path.basename(path)} imports {name}")
            with open(path, encoding="ascii") as handle:
                tree = ast.parse(handle.read(), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Name):
                    if node.func.id in forbidden_calls:
                        offences.append(f"{os.path.basename(path)} calls {node.func.id}")
        self.assertEqual([], offences)

    def test_it_declares_no_activation_or_registration_entry_point(self):
        """Deciding is not activating. Wiring a decision to a registry is a later task."""
        forbidden_stems = {"activate", "register", "deploy", "serve", "publish", "install"}
        offences = []
        for path in governance_files():
            with open(path, encoding="ascii") as handle:
                tree = ast.parse(handle.read(), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    stem = node.name.lstrip("_").split("_")[0].lower()
                    if stem in forbidden_stems:
                        offences.append(f"{os.path.basename(path)}:{node.name}")
        self.assertEqual([], offences)

    def test_the_source_never_names_a_serving_winner(self):
        forbidden = "cham" + "pion"
        offences = []
        for path in governance_files():
            if forbidden in stripped_source(path).lower():
                offences.append(os.path.basename(path))
        self.assertEqual([], offences)

    def test_no_production_schema_table_or_route_is_named(self):
        forbidden = (
            "ppiq_" + "app",
            "ml_" + "outcome_definitions",
            "dump_" + "store",
            "presentation",
            "connection" + "_string",
        )
        offences = []
        for path in governance_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                if token in body:
                    offences.append(f"{os.path.basename(path)} names '{token}'")
        self.assertEqual([], offences)

    def test_every_governance_source_file_is_ascii_with_unix_line_endings(self):
        offences = []
        for path in governance_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            if raw[:3] == b"\xef\xbb\xbf":
                offences.append(f"{os.path.basename(path)} has a byte order mark")
            if b"\r\n" in raw:
                offences.append(f"{os.path.basename(path)} has carriage returns")
            if any(b > 126 for b in raw if b not in (9, 10)):
                offences.append(f"{os.path.basename(path)} has non-ascii bytes")
        self.assertEqual([], offences)


class TheDimensionsAreNeverCombined(unittest.TestCase):
    def test_no_weight_or_total_appears_in_the_kernel_source(self):
        """A weighted total is the defect this task exists to prevent, so it is guarded."""
        forbidden = ("weighted_sum", "total_score", "overall_score", "composite_score")
        for path in governance_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                self.assertNotIn(token, body, f"{os.path.basename(path)} names '{token}'")

    def test_the_encoder_inequality_carries_exactly_four_clauses(self):
        from ppiq_ml.governance import (
            CLAUSE_ARTIFACT_SIZE,
            CLAUSE_EXPLANATION_STABILITY,
            CLAUSE_METRIC_LIFT,
            CLAUSE_P95_LATENCY_DELTA,
        )

        self.assertEqual(
            4,
            len(
                {
                    CLAUSE_METRIC_LIFT,
                    CLAUSE_P95_LATENCY_DELTA,
                    CLAUSE_ARTIFACT_SIZE,
                    CLAUSE_EXPLANATION_STABILITY,
                }
            ),
        )


if __name__ == "__main__":
    unittest.main()
