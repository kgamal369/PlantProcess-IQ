"""T-178 boundary guards.

The one that matters most: this kernel cannot reach a plant. PPIQ records what a
human decided and never issues a control command, so a static and dependency check
that no writer, client or socket is reachable from here is not paperwork. It is the
guarantee.
"""

import ast
import os
import unittest

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
REMEDIATION = os.path.join(SRC, "ppiq_ml", "remediation")

STDLIB_OR_OWN = {"__future__", "dataclasses", "enum", "itertools", "typing", "ppiq_ml"}


def remediation_files():
    for base, _dirs, names in os.walk(REMEDIATION):
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


class TheKernelCannotReachAPlantOrAStore(unittest.TestCase):
    def test_no_customer_source_write_or_control_client_is_reachable(self):
        forbidden = {
            "socket", "subprocess", "http", "urllib", "requests", "psycopg", "psycopg2",
            "sqlalchemy", "asyncpg", "sqlite3", "opcua", "pymodbus", "paho",
        }
        offences = []
        for path in remediation_files():
            for name in imported_top_level(path):
                if name in forbidden:
                    offences.append(f"{os.path.basename(path)} imports {name}")
        self.assertEqual([], offences)

    def test_it_carries_no_third_party_dependency_at_all(self):
        found = set()
        for path in remediation_files():
            found.update(imported_top_level(path))
        self.assertEqual([], sorted(found - STDLIB_OR_OWN))

    def test_it_opens_no_file_and_writes_nothing(self):
        forbidden_calls = {"open", "input", "exec", "eval"}
        offences = []
        for path in remediation_files():
            tree = ast.parse(read(path), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Name):
                    if node.func.id in forbidden_calls:
                        offences.append(f"{os.path.basename(path)} calls {node.func.id}")
        self.assertEqual([], offences)

    def test_it_declares_no_persistence_or_decision_recording_entry_point(self):
        """Accept, Reject and Defer persistence is downstream work, not this task."""
        forbidden_stems = {
            "persist", "save", "insert", "write", "accept", "reject", "defer",
            "register", "activate", "publish", "send", "issue", "command",
        }
        offences = []
        for path in remediation_files():
            tree = ast.parse(read(path), filename=path)
            for node in ast.walk(tree):
                if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    stem = node.name.lstrip("_").split("_")[0].lower()
                    if stem in forbidden_stems:
                        offences.append(f"{os.path.basename(path)}:{node.name}")
        self.assertEqual([], offences)

    def test_no_production_schema_table_or_route_is_named(self):
        forbidden = (
            "ppiq_" + "app",
            "presentation",
            "connection" + "_string",
            "prediction_" + "current",
            "insert " + "into",
        )
        offences = []
        for path in remediation_files():
            body = stripped_source(path).lower()
            for token in forbidden:
                if token in body:
                    offences.append(f"{os.path.basename(path)} names '{token}'")
        self.assertEqual([], offences)


class TheFrozenVocabularyIsNotReinterpreted(unittest.TestCase):
    def test_the_nine_codes_and_names_are_exactly_the_frozen_ones(self):
        from ppiq_ml.remediation import CHECKS

        expected = (
            ("RM01", "Controllability"),
            ("RM02", "Remaining actionable stage"),
            ("RM03", "Operating and specification limits"),
            ("RM04", "Forbidden combinations and safety"),
            ("RM05", "Historical support"),
            ("RM06", "Contextual and confounder survival"),
            ("RM07", "Uncertainty"),
            ("RM08", "Causal and uplift evidence where data permits"),
            ("RM09", "Sensitivity"),
        )
        self.assertEqual(expected, tuple((c.code, c.name) for c in CHECKS))

    def test_there_are_exactly_four_outcome_states_and_no_fifth(self):
        from ppiq_ml.remediation import EligibilityState

        self.assertEqual(
            {"actionable", "evidence_only", "exploratory", "suppressed"},
            {s.value for s in EligibilityState},
        )

    def test_there_are_exactly_seven_conditions(self):
        from ppiq_ml.remediation import CONDITIONS

        self.assertEqual(7, len(CONDITIONS))
        self.assertEqual([1, 2, 3, 4, 5, 6, 7], [c.ordinal for c in CONDITIONS])

    def test_the_safety_check_is_named_by_code_rather_than_by_position(self):
        """A reordering must never be able to move which rule suppresses."""
        body = stripped_source(os.path.join(REMEDIATION, "eligibility.py"))
        self.assertIn("SAFETY_CHECK_CODE", body)
        self.assertNotIn("checks[3]", body)

    def test_every_check_is_a_table_row_rather_than_a_branch(self):
        from ppiq_ml.remediation import CHECKS, CheckDefinition

        for check in CHECKS:
            self.assertIsInstance(check, CheckDefinition)
            self.assertTrue(callable(check.predicate))
            self.assertTrue(callable(check.fail_sentence))

    def test_no_two_checks_share_a_code_or_a_name(self):
        from ppiq_ml.remediation import CHECKS

        self.assertEqual(9, len({c.code for c in CHECKS}))
        self.assertEqual(9, len({c.name for c in CHECKS}))

    def test_no_two_conditions_share_a_code(self):
        from ppiq_ml.remediation import CONDITION_CODES

        self.assertEqual(7, len(set(CONDITION_CODES)))


class TheSourceIsEncodedAsTheRepositoryRequires(unittest.TestCase):
    def test_every_file_is_ascii_with_unix_line_endings(self):
        for path in remediation_files():
            with open(path, "rb") as handle:
                raw = handle.read()
            self.assertNotEqual(b"\xef\xbb\xbf", raw[:3], os.path.basename(path))
            self.assertNotIn(b"\r\n", raw, os.path.basename(path))
            self.assertFalse(
                any(b > 126 for b in raw if b not in (9, 10)), os.path.basename(path)
            )


if __name__ == "__main__":
    unittest.main()
