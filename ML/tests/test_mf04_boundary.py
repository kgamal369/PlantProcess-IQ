"""T-175 part three: the boundaries this task must not cross, enforced by tests.

A convention drifts. A test does not. Each guard here is scoped to the model layer,
runs against the source it judges, and assembles the tokens it forbids from fragments
so that the guard cannot match itself.
"""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
MODELS = os.path.join(SRC, "ppiq_ml", "models")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

STDLIB_OR_OWN = {
    "__future__", "abc", "ast", "dataclasses", "datetime", "decimal", "enum",
    "hashlib", "json", "math", "os", "sys", "time", "typing", "ppiq_ml",
}

#: The only third-party packages this family's extra declares as implementation.
#: Explanation tooling is named in the extra but belongs to the later task, so it
#: must not appear in this source.
PERMITTED_THIRD_PARTY = {"lightgbm", "numpy"}


def model_source_files():
    for root, _dirs, names in os.walk(MODELS):
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
    """Source with comment lines and docstrings removed.

    A guard must judge behaviour, not the prose that explains it. Without this a
    sentence describing what the module refuses to do would fail the check that it
    does not do it.
    """
    with open(path, encoding="ascii") as handle:
        text = handle.read()
    tree = ast.parse(text, filename=path)
    docstrings = set()
    for node in ast.walk(tree):
        if isinstance(node, (ast.Module, ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)):
            doc = ast.get_docstring(node, clean=False)
            if doc:
                docstrings.add(doc)
    lines = [l for l in text.split("\n") if not l.strip().startswith("#")]
    body = "\n".join(lines)
    for doc in docstrings:
        body = body.replace(doc, "")
    return body


class TheModelLayerCarriesOnlyItsDeclaredDependencies(unittest.TestCase):
    def test_no_third_party_package_beyond_the_declared_family_extra(self):
        found = set()
        for path in model_source_files():
            found.update(imported_top_level(path))
        external = sorted(found - STDLIB_OR_OWN - PERMITTED_THIRD_PARTY)
        self.assertEqual(
            [], external, f"The model layer gained an undeclared dependency: {external}"
        )

    def test_the_explanation_package_is_not_used_in_this_task(self):
        """Explanation stability is the later task's subject, not this one's."""
        explanation_package = "sh" + "ap"
        for path in model_source_files():
            self.assertNotIn(
                explanation_package,
                [name for name in imported_top_level(path)],
                f"{os.path.basename(path)} imports the explanation package",
            )

    def test_the_booster_import_is_deferred_so_the_floor_runs_without_it(self):
        """A machine with no ML stack must still import the package and train the floor."""
        module_level = []
        path = os.path.join(MODELS, "mf04_supervised", "candidate.py")
        with open(path, encoding="ascii") as handle:
            tree = ast.parse(handle.read(), filename=path)
        for node in tree.body:
            if isinstance(node, (ast.Import, ast.ImportFrom)):
                module_level += imported_top_level(path) if False else []
                if isinstance(node, ast.Import):
                    module_level += [a.name.split(".")[0] for a in node.names]
                elif node.module and node.level == 0:
                    module_level.append(node.module.split(".")[0])
        self.assertEqual([], sorted(set(module_level) & PERMITTED_THIRD_PARTY))

    def test_the_dependency_and_its_lock_landed_together(self):
        with open(os.path.join(ROOT, "requirements.lock"), encoding="ascii") as handle:
            lock = handle.read()
        for package in sorted(PERMITTED_THIRD_PARTY):
            self.assertIn(
                package + "==", lock, f"{package} is used but carries no locked version"
            )


class TheModelLayerMakesNoDecisionItDoesNotOwn(unittest.TestCase):
    def test_the_source_never_names_a_serving_winner(self):
        """Selection is the later task's decision and has dimensions this one cannot see."""
        forbidden = "cham" + "pion"
        offences = []
        for path in model_source_files():
            if forbidden in stripped_source(path).lower():
                offences.append(os.path.basename(path))
        self.assertEqual([], offences)

    def test_the_source_declares_no_activation_or_registration_entry_point(self):
        forbidden_names = {"promote", "activate", "register", "deploy", "serve"}
        offences = []
        for path in model_source_files():
            with open(path, encoding="ascii") as handle:
                tree = ast.parse(handle.read(), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    stem = node.name.lstrip("_").split("_")[0].lower()
                    if stem in forbidden_names:
                        offences.append(f"{os.path.basename(path)}:{node.name}")
        self.assertEqual([], offences)


class TheModelLayerTouchesNoProductionSurface(unittest.TestCase):
    def test_no_production_schema_table_or_outcome_store_is_named(self):
        forbidden = (
            "ppiq_" + "app",
            "ml_" + "outcome_definitions",
            "dump_" + "store",
            "presentation",
            "connection" + "_string",
        )
        offences = []
        for path in model_source_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                if token in body:
                    offences.append(f"{os.path.basename(path)} names '{token}'")
        self.assertEqual([], offences)

    def test_no_network_or_process_control_capability_is_imported(self):
        forbidden = {"socket", "subprocess", "http", "urllib", "requests", "ftplib"}
        offences = []
        for path in model_source_files():
            for name in imported_top_level(path):
                if name in forbidden:
                    offences.append(f"{os.path.basename(path)} imports {name}")
        self.assertEqual([], offences)

    def test_every_model_source_file_is_ascii_with_unix_line_endings(self):
        offences = []
        for path in model_source_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            if raw[:3] == b"\xef\xbb\xbf":
                offences.append(f"{os.path.basename(path)} has a byte order mark")
            if b"\r\n" in raw:
                offences.append(f"{os.path.basename(path)} has carriage returns")
            if any(b > 126 for b in raw if b not in (9, 10)):
                offences.append(f"{os.path.basename(path)} has non-ascii bytes")
        self.assertEqual([], offences)

    def test_the_fixture_contract_declares_that_it_is_not_production_authority(self):
        path = os.path.join(MODELS, "mf04_supervised", "outcome.py")
        with open(path, encoding="ascii") as handle:
            text = handle.read()
        self.assertIn("not the production owner", text)
        self.assertIn("SM-06", text)


if __name__ == "__main__":
    unittest.main()
