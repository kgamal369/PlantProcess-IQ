"""T-172 boundary guards.

Two properties this package must keep, enforced rather than remembered.

ITS ONLY INPUT IS A SEALED ARTIFACT. There is no database client, no feature store
client and no connection string anywhere in it, so there is no route by which live
customer data could reach a model through this path.

IT DECLARES NOTHING DEPLOYABLE. The encoder is optional. Training successfully is not
evidence that it should be served, and no name in this package suggests otherwise.
"""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
ENCODERS = os.path.join(SRC, "ppiq_ml", "encoders")

STDLIB_OR_OWN = {
    "__future__", "abc", "dataclasses", "enum", "hashlib", "json", "math", "os",
    "sys", "time", "typing", "ppiq_ml",
}

#: The one framework this task's authoritative description requires.
PERMITTED_FRAMEWORK = "torch"


def encoder_files():
    for base, _dirs, names in os.walk(ENCODERS):
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


def module_level_imports(path):
    tree = ast.parse(read(path), filename=path)
    found = []
    for node in tree.body:
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


class TheEncoderCarriesOnlyItsDeclaredFramework(unittest.TestCase):
    def test_no_package_beyond_the_one_framework_this_task_requires(self):
        found = set()
        for path in encoder_files():
            found.update(imported_top_level(path))
        external = sorted(found - STDLIB_OR_OWN - {PERMITTED_FRAMEWORK})
        self.assertEqual([], external, f"The encoder layer gained a dependency: {external}")

    def test_the_framework_import_is_deferred_in_every_file(self):
        """The contract must import on a machine that has no framework installed."""
        for path in encoder_files():
            self.assertNotIn(
                PERMITTED_FRAMEWORK, module_level_imports(path), os.path.basename(path)
            )

    def test_the_contract_and_the_eligibility_rules_need_no_framework_at_all(self):
        for name in ("contract.py", "eligibility.py", "windows.py", "b05.py"):
            path = os.path.join(ENCODERS, name)
            self.assertNotIn(PERMITTED_FRAMEWORK, imported_top_level(path), name)

    def test_the_dependency_and_its_lock_landed_together(self):
        root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        with open(os.path.join(root, "requirements.lock"), encoding="ascii") as handle:
            lock = handle.read()
        self.assertIn(PERMITTED_FRAMEWORK + "==", lock)

    def test_no_second_framework_is_introduced(self):
        forbidden = ("tensorflow", "keras", "jax", "mxnet")
        for path in encoder_files():
            imported = imported_top_level(path)
            for package in forbidden:
                self.assertNotIn(package, imported, os.path.basename(path))


class ItReadsOnlySealedArtifacts(unittest.TestCase):
    def test_no_database_or_network_capability_is_imported(self):
        forbidden = {
            "psycopg", "psycopg2", "sqlalchemy", "asyncpg", "sqlite3", "socket",
            "subprocess", "http", "urllib", "requests",
        }
        offences = []
        for path in encoder_files():
            for name in imported_top_level(path):
                if name in forbidden:
                    offences.append(f"{os.path.basename(path)} imports {name}")
        self.assertEqual([], offences)

    def test_no_live_store_schema_or_table_is_named(self):
        forbidden = (
            "feature_" + "store",
            "ppiq_" + "app",
            "presentation",
            "connection" + "_string",
            "dump_" + "store",
        )
        offences = []
        for path in encoder_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                if token in body:
                    offences.append(f"{os.path.basename(path)} names '{token}'")
        self.assertEqual([], offences)

    def test_the_only_reader_it_uses_is_the_sealed_sequence_library(self):
        body = read(os.path.join(ENCODERS, "windows.py"))
        self.assertIn("from ..sequences import", body)
        for name in ("open(", "connect(", "cursor("):
            self.assertNotIn(name, stripped_source(os.path.join(ENCODERS, "windows.py")))


class ItDeclaresNothingDeployable(unittest.TestCase):
    def test_the_source_never_names_a_serving_winner(self):
        forbidden = "cham" + "pion"
        for path in encoder_files():
            self.assertNotIn(forbidden, stripped_source(path).lower(), os.path.basename(path))

    def test_it_declares_no_promotion_activation_or_registration_entry_point(self):
        forbidden_stems = {"promote", "activate", "register", "deploy", "serve", "publish"}
        offences = []
        for path in encoder_files():
            tree = ast.parse(read(path), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    stem = node.name.lstrip("_").split("_")[0].lower()
                    if stem in forbidden_stems:
                        offences.append(f"{os.path.basename(path)}:{node.name}")
        self.assertEqual([], offences)

    def test_no_module_level_literal_declares_a_verdict(self):
        offences = []
        for path in encoder_files():
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
                    for token in ("deployable", "selected", "winner", "recommended", "verdict"):
                        if token in lowered:
                            offences.append(f"{os.path.basename(path)}:{name}")
        self.assertEqual([], offences)

    def test_a_second_architecture_could_satisfy_the_same_contract(self):
        """Replaceability, checked on the interface rather than asserted in prose."""
        from ppiq_ml.encoders import ProcessEncoder, TemporalConvolutionEncoder

        self.assertTrue(issubclass(TemporalConvolutionEncoder, ProcessEncoder))
        for member in ("encoder_kind", "manifest", "train", "encode", "save"):
            self.assertTrue(hasattr(ProcessEncoder, member), member)


class TheSourceIsEncodedAsTheRepositoryRequires(unittest.TestCase):
    def test_every_file_is_ascii_with_unix_line_endings(self):
        for path in encoder_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            self.assertNotEqual(b"\xef\xbb\xbf", raw[:3], os.path.basename(path))
            self.assertNotIn(b"\r\n", raw, os.path.basename(path))
            self.assertFalse(
                any(b > 126 for b in raw if b not in (9, 10)), os.path.basename(path)
            )

    def test_no_fixture_identifier_appears_in_the_source(self):
        from tests.test_t172_process_encoder import CHANNEL_NAMES

        for path in encoder_files():
            body = read(path)
            for name in CHANNEL_NAMES:
                self.assertNotIn(name, body, os.path.basename(path))


if __name__ == "__main__":
    unittest.main()
