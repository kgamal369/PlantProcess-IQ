"""T-176 Pack 2: contract completeness, and the boundaries of the explanation layer.

The completeness test exists because a promotion kernel that silently cannot express
one of its frozen inputs would pass every other test in this repository. It asserts
that each named input has a field to live in, and then that the field is actually
read by the dimension that owns it. A field nothing reads is not coverage.
"""

import ast
import dataclasses
import os
import unittest

from ppiq_ml.governance import (
    CandidateEvidence,
    QualityEvidence,
    ServingEvidence,
    TrainingEvidence,
    decide,
)
from ppiq_ml.governance.dimensions import (
    evaluate_quality,
    evaluate_serving,
    evaluate_training,
)
from tests.t176_promotion_fixture import candidate, document, incumbent, thresholds

SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")
EXPLANATION = os.path.join(SRC, "ppiq_ml", "explanation")
GOVERNANCE = os.path.join(SRC, "ppiq_ml", "governance")


def field_names(cls):
    return {f.name for f in dataclasses.fields(cls)}


def python_files(root):
    for base, _dirs, names in os.walk(root):
        for name in names:
            if name.endswith(".py"):
                yield os.path.join(base, name)


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
    """Source with comments and docstrings removed.

    A guard must judge what the code does, not the prose explaining what it refuses
    to do. Without this, the sentence stating that no method here measures causation
    is itself matched as a claim of causation.
    """
    text = open_text(path)
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


def module_level_imports(path):
    with open(path, encoding="ascii") as handle:
        tree = ast.parse(handle.read(), filename=path)
    found = []
    for node in tree.body:
        if isinstance(node, ast.Import):
            found += [alias.name.split(".")[0] for alias in node.names]
        elif isinstance(node, ast.ImportFrom) and node.module and node.level == 0:
            found.append(node.module.split(".")[0])
    return found


class TheEvidenceModelRepresentsEveryFrozenInput(unittest.TestCase):
    def test_quality_carries_all_six_frozen_inputs(self):
        fields = field_names(QualityEvidence)
        for frozen_input, field in (
            ("discrimination or error", "primary_metric"),
            ("calibration", "calibration_error"),
            ("out of time", "out_of_time_primary_metric"),
            ("subgroup or regime stability", "subgroup_primary_metrics"),
            ("missingness robustness", "missingness_primary_metric"),
            ("explanation stability", "explanation"),
        ):
            self.assertIn(field, fields, f"QUALITY input '{frozen_input}' has no field")

    def test_serving_carries_all_seven_frozen_inputs(self):
        fields = field_names(ServingEvidence)
        for frozen_input, field in (
            ("p50", "p50_latency_ms"),
            ("p95", "p95_latency_ms"),
            ("p99", "p99_latency_ms"),
            ("throughput", "throughput_per_second"),
            ("artifact size", "artifact_size_bytes"),
            ("RAM", "resident_memory_mb"),
            ("VRAM", "accelerator_memory_mb"),
            ("warm-up", "warm_up_seconds"),
        ):
            self.assertIn(field, fields, f"SERVING input '{frozen_input}' has no field")

    def test_training_carries_all_three_frozen_inputs(self):
        fields = field_names(TrainingEvidence)
        for frozen_input, field in (
            ("duration", "training_seconds"),
            ("peak memory", "peak_memory_mb"),
            ("snapshot throughput", "snapshot_rows_per_second"),
        ):
            self.assertIn(field, fields, f"TRAINING input '{frozen_input}' has no field")

    def test_every_frozen_input_is_actually_checked_by_its_dimension(self):
        """A field nothing reads is not coverage, so the check names are asserted too."""
        held = incumbent()
        challenger = candidate()
        declared = thresholds(max_accelerator_memory_mb=2048.0)

        quality_verdict, _ = evaluate_quality(challenger, held, declared)
        quality_checks = {c.name for c in quality_verdict.checks}
        for expected in (
            "primary_metric",
            "calibration_error",
            "proper_score_not_worse_than_incumbent",
            "out_of_time_drop",
            "subgroup_spread",
            "missingness_drop",
            "explanation_rank_agreement",
            "explanation_top_k_overlap",
        ):
            self.assertIn(expected, quality_checks)

        serving_checks = {c.name for c in evaluate_serving(challenger, declared).checks}
        for expected in (
            "p50_latency_ms",
            "p95_latency_ms",
            "p99_latency_ms",
            "throughput_per_second",
            "artifact_size_bytes",
            "resident_memory_mb",
            "accelerator_memory_mb",
            "warm_up_seconds",
        ):
            self.assertIn(expected, serving_checks)

        training_checks = {c.name for c in evaluate_training(challenger, declared).checks}
        for expected in ("training_seconds", "peak_memory_mb", "snapshot_rows_per_second"):
            self.assertIn(expected, training_checks)

    def test_a_candidate_record_carries_all_three_dimensions_and_both_identities(self):
        fields = field_names(CandidateEvidence)
        for field in (
            "quality", "serving", "training", "snapshot_identity", "holdout_identity",
        ):
            self.assertIn(field, fields)

    def test_a_fully_populated_document_still_decides(self):
        decision = decide(
            document(
                challenger=candidate(),
                declared=thresholds(max_accelerator_memory_mb=None),
            )
        )
        self.assertIsNotNone(decision.outcome)


class TheExplanationLayerStaysOnItsOwnSideOfTheBoundary(unittest.TestCase):
    def test_the_governance_layer_never_imports_the_explanation_layer(self):
        """The kernel judges numbers. It must not be able to produce them."""
        for path in python_files(GOVERNANCE):
            self.assertNotIn(
                "explanation.treeshap",
                open_text(path),
                os.path.basename(path),
            )
            for name in imported_top_level(path):
                self.assertNotEqual("lightgbm", name)
                self.assertNotEqual("numpy", name)

    def test_the_explanation_contract_needs_no_third_party_package_at_all(self):
        """A machine with no ML stack can still read and validate contribution evidence."""
        for name in ("contract.py", "bridge.py", "__init__.py"):
            path = os.path.join(EXPLANATION, name)
            for imported in imported_top_level(path):
                self.assertIn(
                    imported,
                    {"__future__", "abc", "dataclasses", "enum", "hashlib", "json", "typing", "ppiq_ml"},
                    f"{name} imports {imported}",
                )

    def test_the_numeric_import_in_the_producer_is_deferred(self):
        path = os.path.join(EXPLANATION, "treeshap.py")
        self.assertEqual([], sorted(set(module_level_imports(path)) & {"numpy", "lightgbm"}))

    def test_the_producer_names_no_package_this_task_did_not_already_pin(self):
        forbidden = "sh" + "ap"
        for path in python_files(EXPLANATION):
            for imported in imported_top_level(path):
                self.assertNotEqual(forbidden, imported, os.path.basename(path))

    def test_no_source_in_the_explanation_layer_claims_a_cause(self):
        """A contribution is not a cause, and no record here may say otherwise."""
        forbidden = ("causal", "causation", "causes ", "caused by", "counterfactual")
        for path in python_files(EXPLANATION):
            body = stripped_source(path).lower()
            for token in forbidden:
                self.assertNotIn(token, body, f"{os.path.basename(path)} claims '{token}'")

    def test_the_explanation_layer_persists_nothing_and_reaches_nowhere(self):
        forbidden_imports = {
            "socket", "subprocess", "http", "urllib", "requests", "psycopg", "sqlite3",
            "pathlib", "shutil", "tempfile",
        }
        for path in python_files(EXPLANATION):
            for imported in imported_top_level(path):
                self.assertNotIn(imported, forbidden_imports, os.path.basename(path))

    def test_every_explanation_source_file_is_ascii_with_unix_line_endings(self):
        for path in python_files(EXPLANATION):
            with open(path, "rb") as handle:
                raw = handle.read()
            self.assertNotEqual(b"\xef\xbb\xbf", raw[:3], os.path.basename(path))
            self.assertNotIn(b"\r\n", raw, os.path.basename(path))
            self.assertFalse(
                any(b > 126 for b in raw if b not in (9, 10)), os.path.basename(path)
            )


def open_text(path):
    with open(path, encoding="ascii") as handle:
        return handle.read()


if __name__ == "__main__":
    unittest.main()
