#!/usr/bin/env python3
"""
PPIQ T-027 coverage ledger - classify every phenomenon in the historical matrix.

WHAT THIS IS AND IS NOT
    T-027 requires measured effects to be tested against targets PREDECLARED in
    T-008 and refined in T-015, both before the data existed. It says plainly:
    "Writing the band from the data and then testing the data against it is a
    self-fulfilling test and proves nothing."

    So this file does NOT invent a single number. It reads the historical
    declarations exactly as written, reads what the generated plant actually
    contains, and gives every one of the 104 rows a deterministic classification.
    A refusal or a gap IS a result for coverage purposes. It is never silently
    converted into a PASS or a FAIL.

RESULT CLASSES, as ruled
    MEASURED_PASS                    executed against its original numeric target
    MEASURED_FAIL                    executed and contradicted
    DECLARATION_NOT_NUMERIC          the historical assertion has no numeric target
    MEASURE_UNSUPPORTED              the declared statistic is not what the frozen
                                     harness computes; the meaning of
                                     expected_effect_band is NOT changed to fit
    SOURCE_VARIABLE_NOT_MATERIALISED a declared variable is absent from the plant
    GENERATOR_SCOPE_NOT_IMPLEMENTED  the matrix itself marks the row ENRICH or NEW

THE ORDER OF THE TESTS MATTERS AND IS DELIBERATE
    Variables are checked BEFORE the statistic. A row whose declared variables do
    not exist cannot be measured by any statistic, so reporting it as
    MEASURE_UNSUPPORTED would name the wrong cause. This ordering is why the
    05-Aug run produced zero MEASURE_UNSUPPORTED rows: the matrix fails earlier,
    on data that was never generated.

WHAT IS NOT DONE HERE, BY RULING
    - no prose assertion is translated into a numeric band after the fact
    - no rate ratio is reinterpreted as a Spearman coefficient
    - the two data-derived T-026 seeds are NOT inherited as predeclarations; they
      are harness demonstrations and are labelled as such wherever referenced
    - no phenomenon whose variables are missing is called a scientific FAIL
"""

import argparse
import csv
import io
import os
import re
import sys

SUPPORTED_MEASURE = "spearman"

RESULT_CLASSES = [
    "MEASURED_PASS", "MEASURED_FAIL", "DECLARATION_NOT_NUMERIC",
    "MEASURE_UNSUPPORTED", "SOURCE_VARIABLE_NOT_MATERIALISED",
    "GENERATOR_SCOPE_NOT_IMPLEMENTED",
]

# Detected from the assertion text as originally written. Order matters: the
# first pattern that fires names the statistic the declaration actually used.
STAT_PATTERNS = [
    ("rate_ratio",           r"rate ratio|exposed/baseline|baseline ratio|ratio is between"),
    ("fdr_significance",     r"\bq\s*=\s*0?\.\d+|benjamini|\bbh\b|significant at"),
    ("variance_comparison",  r"variance[^.]*\b(lower|higher)\b|by at least \d+\s*percent"),
    ("threshold_membership", r"\babove \d|\bbelow \d|exactly the .*\brows\b"),
    ("step_change",          r"\bstep\b|changepoint|shifts? .*distribution"),
    ("correlation",          r"spearman|pearson|correlation coefficient|\brho\b"),
]
HAS_DIGIT = re.compile(r"\d")

# Phrases the matrix uses instead of naming a column. They are honest shorthand in
# a design document and unusable as a query specification.
DEFERRED = ("named in the assert", "the time axis", "the event and",
            "the quantity named", "the exposure variable", "the control defect code")


def declared_statistic(assertion):
    text = (assertion or "").lower()
    for name, pattern in STAT_PATTERNS:
        if re.search(pattern, text):
            return name
    return "unclassified"


def split_variables(text):
    raw = (text or "").lower()
    for phrase in DEFERRED:
        if phrase in raw:
            return (False, [])
    tokens = [t.strip() for t in re.split(r"[,;]", raw) if t.strip()]
    return (True, tokens)


def load_available(path):
    """Every identifier a population_query may legally reference, read from the
    live database by the runner - never typed by hand."""
    available = set()
    if not os.path.exists(path):
        return available
    with io.open(path, encoding="utf-8") as handle:
        for line in handle:
            token = line.strip().lower()
            if token:
                available.add(token)
    return available


def phenomenon_id(row, index):
    text = (row.get("phenomenon") or "").strip()
    match = re.match(r"^([A-Z]+\d+)\b", text)
    if match:
        return match.group(1)
    cls = (row.get("phenomenon_class") or "UNCLASSED").strip()
    return cls + "-" + str(index).rjust(3, "0")


def classify(row, available):
    statistic = declared_statistic(row.get("assertion"))
    resolved, tokens = split_variables(row.get("variables"))
    status = (row.get("status") or "").strip().upper()
    numeric = bool(HAS_DIGIT.search(row.get("assertion") or ""))

    if not resolved:
        availability = "NOT RESOLVABLE - the declaration defers to the assertion text"
        if status in ("NEW", "ENRICH"):
            return (statistic, availability, "GENERATOR_SCOPE_NOT_IMPLEMENTED",
                    "matrix status " + status + "; the declaration names no concrete "
                    "column, so nothing can be queried")
        return (statistic, availability, "DECLARATION_NOT_NUMERIC",
                "the declaration defers its variables to the assertion prose; no "
                "concrete column is named")

    missing = [t for t in tokens if t not in available]
    if missing:
        availability = "MISSING: " + ", ".join(missing)
        if status in ("NEW", "ENRICH"):
            return (statistic, availability, "GENERATOR_SCOPE_NOT_IMPLEMENTED",
                    "matrix status " + status + "; declared variable(s) absent from "
                    "the generated plant: " + ", ".join(missing))
        return (statistic, availability, "SOURCE_VARIABLE_NOT_MATERIALISED",
                "declared variable(s) absent from the generated plant: "
                + ", ".join(missing))

    availability = "ALL PRESENT"
    if not numeric:
        return (statistic, availability, "DECLARATION_NOT_NUMERIC",
                "the historical assertion carries no numeric target; it is a "
                "declaration but not an effect band")
    if statistic != "correlation":
        return (statistic, availability, "MEASURE_UNSUPPORTED",
                "declared statistic is " + statistic + "; the frozen harness "
                "computes " + SUPPORTED_MEASURE + " only, and expected_effect_band "
                "is not redefined to fit")
    return (statistic, availability, "EXECUTABLE",
            "numeric correlation target with every declared variable present")


def main():
    parser = argparse.ArgumentParser(description="T-027 coverage ledger")
    parser.add_argument("--matrix", required=True)
    parser.add_argument("--available", required=True,
                        help="identifiers read from the live database by the runner")
    parser.add_argument("--ledger-out", required=True)
    parser.add_argument("--report-out", required=True)
    args = parser.parse_args()

    if not os.path.exists(args.matrix):
        sys.stderr.write("matrix not found: " + args.matrix + "\n")
        return 2

    available = load_available(args.available)
    if not available:
        sys.stderr.write("the available-identifier list is empty. Refusing to "
                         "classify, because every row would report a missing "
                         "variable and the ledger would be a lie.\n")
        return 2

    with io.open(args.matrix, encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))

    out_columns = [
        "phenomenon_id", "phenomenon", "phenomenon_class", "matrix_status",
        "required_variables", "declared_statistic", "variable_availability",
        "execution_classification", "reason", "original_assertion",
        "primary_chart", "secondary_chart", "negative_control",
    ]

    ledger = []
    for index, row in enumerate(rows, start=1):
        statistic, availability, verdict, reason = classify(row, available)
        ledger.append({
            "phenomenon_id": phenomenon_id(row, index),
            "phenomenon": row.get("phenomenon", ""),
            "phenomenon_class": row.get("phenomenon_class", ""),
            "matrix_status": row.get("status", ""),
            "required_variables": row.get("variables", ""),
            "declared_statistic": statistic,
            "variable_availability": availability,
            "execution_classification": verdict,
            "reason": reason,
            "original_assertion": row.get("assertion", ""),
            "primary_chart": row.get("primary_chart", ""),
            "secondary_chart": row.get("secondary_chart", ""),
            "negative_control": row.get("negative_control", ""),
        })

    with io.open(args.ledger_out, "w", encoding="ascii", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=out_columns, lineterminator="\r\n")
        writer.writeheader()
        for entry in ledger:
            writer.writerow({k: "".join(c for c in str(entry[k]) if ord(c) < 127)
                             for k in out_columns})

    counts = {}
    stats = {}
    charts = set()
    proven_charts = set()
    controls_total = 0
    controls_measured = 0
    for entry in ledger:
        counts[entry["execution_classification"]] = counts.get(entry["execution_classification"], 0) + 1
        stats[entry["declared_statistic"]] = stats.get(entry["declared_statistic"], 0) + 1
        for chart in (entry["primary_chart"], entry["secondary_chart"]):
            chart = (chart or "").strip()
            if chart:
                charts.add(chart)
                if entry["execution_classification"] in ("MEASURED_PASS", "MEASURED_FAIL"):
                    proven_charts.add(chart)
        if entry["phenomenon_class"].strip().upper() == "NEGATIVE_CONTROL":
            controls_total += 1
            if entry["execution_classification"] in ("MEASURED_PASS", "MEASURED_FAIL"):
                controls_measured += 1

    executable = [e for e in ledger if e["execution_classification"] == "EXECUTABLE"]

    lines = []
    def say(text=""):
        lines.append(text)
        print(text)

    say("")
    say("=" * 78)
    say("T-027 COVERAGE LEDGER")
    say("=" * 78)
    say("Every phenomenon in the historical matrix receives a deterministic")
    say("classification. A refusal or a gap is a RESULT for coverage purposes and")
    say("is never converted into a PASS or a FAIL.")
    say("")
    say("  rows accounted for : " + str(len(ledger)) + " / " + str(len(rows)))
    say("")
    say("CLASSIFICATION")
    say("-" * 78)
    for name in RESULT_CLASSES + ["EXECUTABLE"]:
        if counts.get(name):
            say("  " + str(counts[name]).rjust(4) + "  " + name)
    say("")
    say("DECLARED STATISTIC, as originally written in T-008 / T-015")
    say("-" * 78)
    for name in sorted(stats, key=lambda k: -stats[k]):
        say("  " + str(stats[name]).rjust(4) + "  " + name)
    say("")
    say("THE TWO EXECUTION CLAUSES OF THE T-027 VALIDATION")
    say("-" * 78)
    say("  charts referenced by the matrix        : " + str(len(charts)))
    say("  charts referencing a PROVEN phenomenon : " + str(len(proven_charts)))
    say("  negative controls declared             : " + str(controls_total))
    say("  negative controls MEASURED as silent   : " + str(controls_measured))
    say("  A control that was never measured is NOT silent. Recording it as silent")
    say("  would manufacture exactly the evidence the task forbids.")
    say("")
    say("EXECUTABLE ROWS : " + str(len(executable)))
    say("-" * 78)
    if executable:
        for entry in executable:
            say("  " + entry["phenomenon_id"] + "  " + entry["phenomenon"][:60])
            say("      " + entry["original_assertion"][:100])
    else:
        say("  none.")
        say("")
        say("  THE MEASURED T-027 CONCLUSION: the current phenomenon matrix does not")
        say("  yet contain an executable numeric predeclaration compatible with the")
        say("  present harness and data contract. That is a product finding about the")
        say("  matrix and the generated plant, not a runner failure, and no")
        say("  measurement has been manufactured to avoid it.")
    say("")
    say("FOLLOW-UP REQUIREMENT, RECORDED")
    say("-" * 78)
    say("  A numeric or statistical predeclaration - the statistic, its expected")
    say("  direction and its acceptable band - must be authored BEFORE the next")
    say("  independent validation population is generated or observed. A band")
    say("  written after the data exists cannot be labelled a T-008 or T-015")
    say("  predeclaration, and must never be presented as one.")
    say("")
    say("  The three T-026 manifest entries are HARNESS DEMONSTRATIONS, not")
    say("  scientific predeclarations. Two of their bands were drawn around")
    say("  measured values and prove only that the harness can return a verdict.")
    say("")

    with io.open(args.report_out, "w", encoding="ascii", newline="") as handle:
        handle.write("\r\n".join("".join(c for c in l if ord(c) < 127) for l in lines))
        handle.write("\r\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
