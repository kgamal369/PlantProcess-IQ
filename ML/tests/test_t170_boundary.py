"""T-170 boundary guards.

Two things this library must not become, enforced rather than remembered.

IT MUST NOT ACQUIRE A DEPENDENCY. A sealed payload is an archive. If reading one
required a package, the archive would only be readable on a machine that still had
that package, which is the opposite of what sealing is for.

IT MUST NOT REACH A DATABASE. Numeric sequence arrays are not stored in PostgreSQL,
and the manifest persistence that will point at these payloads is T-185's subject.
Nothing here writes a row, names a table or selects a storage backend.
"""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
SEQUENCES = os.path.join(SRC, "ppiq_ml", "sequences")

STDLIB_OR_OWN = {
    "__future__", "abc", "bz2", "dataclasses", "enum", "gc", "hashlib", "json",
    "mmap", "os", "struct", "time", "tracemalloc", "typing", "zlib", "ppiq_ml",
}


def sequence_files():
    for base, _dirs, names in os.walk(SEQUENCES):
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


class TheSequenceLibraryCarriesNoDependency(unittest.TestCase):
    def test_no_third_party_package_is_imported_anywhere(self):
        found = set()
        for path in sequence_files():
            found.update(imported_top_level(path))
        external = sorted(found - STDLIB_OR_OWN)
        self.assertEqual([], external, f"The sequence library gained a dependency: {external}")

    def test_every_codec_it_offers_is_in_the_standard_library(self):
        """A payload readable only where a package is installed is not an archive."""
        from ppiq_ml.sequences import enabled_codecs

        for codec in enabled_codecs():
            module = type(codec).__module__
            self.assertTrue(module.startswith("ppiq_ml."), module)


class ItStoresNothingInADatabase(unittest.TestCase):
    def test_no_database_or_network_capability_is_imported(self):
        forbidden = {
            "psycopg", "psycopg2", "sqlalchemy", "asyncpg", "sqlite3", "socket",
            "subprocess", "http", "urllib", "requests",
        }
        offences = []
        for path in sequence_files():
            for name in imported_top_level(path):
                if name in forbidden:
                    offences.append(f"{os.path.basename(path)} imports {name}")
        self.assertEqual([], offences)

    def test_no_table_column_or_array_column_is_named(self):
        forbidden = (
            "ppiq_" + "app",
            "sequence_" + "manifests",
            "insert " + "into",
            "create " + "table",
            "presentation",
        )
        offences = []
        for path in sequence_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                if token in body:
                    offences.append(f"{os.path.basename(path)} names '{token}'")
        self.assertEqual([], offences)

    def test_it_declares_no_persistence_or_registration_entry_point(self):
        """Persisting a manifest is T-185. This library produces one and stops."""
        forbidden_stems = {"persist", "save", "insert", "register", "activate", "publish"}
        offences = []
        for path in sequence_files():
            tree = ast.parse(read(path), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    stem = node.name.lstrip("_").split("_")[0].lower()
                    if stem in forbidden_stems:
                        offences.append(f"{os.path.basename(path)}:{node.name}")
        self.assertEqual([], offences)


class NoSettingIsSelectedHere(unittest.TestCase):
    def test_no_module_level_literal_declares_a_chosen_codec_or_chunk_size(self):
        offences = []
        for path in sequence_files():
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
                    for token in ("selected", "chosen", "recommended", "best", "winner"):
                        if token in lowered:
                            offences.append(f"{os.path.basename(path)}:{name}")
        self.assertEqual([], offences)

    def test_asking_the_library_for_a_default_codec_is_an_error(self):
        from ppiq_ml.sequences import SequenceContractError, default_codec

        with self.assertRaises(SequenceContractError):
            default_codec()

    def test_the_measurement_record_carries_no_verdict_field(self):
        import dataclasses

        from ppiq_ml.sequences import B04Measurement

        names = {f.name for f in dataclasses.fields(B04Measurement)}
        for forbidden in ("selected", "winner", "verdict", "passed", "recommended", "score"):
            self.assertNotIn(forbidden, names)


class TheSourceIsEncodedAsTheRepositoryRequires(unittest.TestCase):
    def test_every_file_is_ascii_with_unix_line_endings(self):
        for path in sequence_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            self.assertNotEqual(b"\xef\xbb\xbf", raw[:3], os.path.basename(path))
            self.assertNotIn(b"\r\n", raw, os.path.basename(path))
            self.assertFalse(
                any(b > 126 for b in raw if b not in (9, 10)), os.path.basename(path)
            )

    def test_no_fixture_identifier_appears_in_the_source(self):
        from tests.test_t170_bounded_and_corruption import LARGE_CHANNELS
        from tests.test_t170_sequence_payload import CHANNELS

        for path in sequence_files():
            body = read(path)
            for name in tuple(CHANNELS) + tuple(LARGE_CHANNELS):
                self.assertNotIn(name, body, os.path.basename(path))


if __name__ == "__main__":
    unittest.main()
