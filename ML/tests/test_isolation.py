"""Boundary tests. These fail the build if the runtime grows a forbidden dependency."""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")

FORBIDDEN_IMPORTS = {
    "psycopg", "psycopg2", "sqlalchemy", "asyncpg", "pyodbc", "pymysql",
    "sqlite3", "requests", "httpx", "urllib3", "boto3",
}

FORBIDDEN_TOKENS = (
    "ppiq_plant", "ppiq_presentation", "ppiq_meta", "ppiq_staging",
    "SELECT ", "INSERT INTO", "connection_string", "ConnectionString",
)


def python_files():
    for root, _dirs, names in os.walk(SRC):
        for name in names:
            if name.endswith(".py"):
                yield os.path.join(root, name)


class RuntimeIsolation(unittest.TestCase):
    def test_the_runtime_imports_no_database_or_network_library(self):
        offences = []
        for path in python_files():
            with open(path, encoding="ascii") as handle:
                tree = ast.parse(handle.read(), filename=path)
            for node in ast.walk(tree):
                names = []
                if isinstance(node, ast.Import):
                    names = [a.name.split(".")[0] for a in node.names]
                elif isinstance(node, ast.ImportFrom) and node.module:
                    names = [node.module.split(".")[0]]
                for name in names:
                    if name in FORBIDDEN_IMPORTS:
                        offences.append(f"{os.path.basename(path)} imports {name}")
        self.assertEqual([], offences,
                         "The Python runtime reads sealed artifacts, never a database.")

    def test_the_runtime_contains_no_schema_name_or_sql(self):
        offences = []
        for path in python_files():
            with open(path, encoding="ascii") as handle:
                text = handle.read()
            for token in FORBIDDEN_TOKENS:
                if token in text:
                    offences.append(f"{os.path.basename(path)} contains '{token}'")
        self.assertEqual([], offences)

    def test_every_source_file_is_pure_ascii(self):
        offences = []
        for path in python_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            if raw[:3] == b"\xef\xbb\xbf":
                offences.append(f"{os.path.basename(path)} has a BOM")
            if any(b > 126 for b in raw if b not in (9, 10, 13)):
                offences.append(f"{os.path.basename(path)} has non-ASCII bytes")
        self.assertEqual([], offences)

    def test_the_runtime_declares_no_industry_vocabulary(self):
        forbidden = ("coil", "heat", "slab", "caster", "grade", "steel", "billet")
        offences = []
        for path in python_files():
            with open(path, encoding="ascii") as handle:
                text = handle.read().lower()
            for word in forbidden:
                if word in text:
                    offences.append(f"{os.path.basename(path)} contains '{word}'")
        self.assertEqual([], offences, "The runtime must remain industry-generic.")

    def test_the_runtime_needs_no_third_party_package_to_execute_the_protocol(self):
        """The protocol layer is standard library only, so it runs in any environment."""
        import ppiq_ml.runtime as runtime
        third_party = []
        for path in python_files():
            with open(path, encoding="ascii") as handle:
                tree = ast.parse(handle.read(), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    third_party += [a.name.split(".")[0] for a in node.names]
                elif isinstance(node, ast.ImportFrom) and node.module and node.level == 0:
                    third_party.append(node.module.split(".")[0])
        stdlib_or_own = {
            "__future__", "json", "os", "sys", "hashlib", "enum", "dataclasses",
            "typing", "datetime", "traceback", "ast", "unittest", "tempfile",
            "shutil", "ppiq_ml",
        }
        unexpected = sorted(set(third_party) - stdlib_or_own)
        self.assertEqual([], unexpected,
                         f"The protocol layer gained a third-party dependency: {unexpected}")
        self.assertTrue(hasattr(runtime, "run"))


if __name__ == "__main__":
    unittest.main()
