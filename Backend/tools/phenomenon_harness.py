#!/usr/bin/env python3
"""
PPIQ T-026 phenomenon test harness - verdict engine.

WHAT THIS IS FOR
    The fleet will change. Re-hand-checking every phenomenon after each change is
    not affordable, so the expectations are written down once in a manifest and a
    runner re-measures them on demand. This file is the verdict half: it does no
    database work at all.

WHY IT DOES NOT TOUCH THE DATABASE
    Invoke-PpiqPhenomenonHarness.ps1 runs every population_query through psql and
    writes one CSV per phenomenon. This module reads those CSVs. That keeps the
    harness free of any Python database driver, which may not be installed, and
    keeps credentials in one place - the PowerShell runner - rather than two.

MANIFEST COLUMNS - FROZEN BY THE BACKLOG, NOT EXTENSIBLE HERE
    phenomenon_id, population_query, expected_direction, minimum_population,
    expected_effect_band, conditioning_variable, expected_after_conditioning,
    negative_control

RESULT-SHAPE CONTRACT
    Every population_query must return a column named x and a column named y.
    When conditioning_variable is non-empty the query must also return a column
    with exactly that name. Rows where x or y is null are dropped before the
    population is counted, so a query that returns 10,000 rows of nulls reports
    INSUFFICIENT rather than passing on volume.

THE EFFECT MEASURE
    Spearman rank correlation, one measure for every row. It is monotone rather
    than linear, it survives a 0/1 encoded driver, and it does not need the
    manifest to declare a method - which it cannot, because the columns are
    frozen. Ties get average ranks. A constant x or y yields an undefined
    statistic and is reported as such, never as an effect of zero.

VERDICTS
    PASS          expectations met
    FAIL          an expectation was contradicted by the data
    INSUFFICIENT  population below minimum_population - a refusal, not a pass
    ERROR         the query failed or the result shape was wrong

    A NEGATIVE CONTROL THAT STARTS CORRELATING IS A FAILURE, NOT A CURIOSITY.
    For negative_control rows the band is the permitted magnitude of |rho|;
    exceeding it is a FAIL and is reported in those words.

    The process exits 1 if any row FAILs or ERRORs. INSUFFICIENT does not fail
    the run - it is a refusal to judge - but it is counted and printed separately
    and can never be mistaken for a pass.
"""

import argparse
import csv
import json
import math
import os
import sys

MANIFEST_COLUMNS = [
    "phenomenon_id",
    "population_query",
    "expected_direction",
    "minimum_population",
    "expected_effect_band",
    "conditioning_variable",
    "expected_after_conditioning",
    "negative_control",
]

MIN_STRATUM_PAIRS = 8


def die(message):
    sys.stderr.write("[HARNESS ERROR] " + message + "\n")
    sys.exit(2)


def parse_band(text):
    """'0.20..0.60' -> (0.20, 0.60). Returns None when unset."""
    if text is None:
        return None
    raw = text.strip()
    if raw == "":
        return None
    if ".." not in raw:
        raise ValueError("band must be written lo..hi, got " + repr(raw))
    lo_text, hi_text = raw.split("..", 1)
    lo = float(lo_text.strip())
    hi = float(hi_text.strip())
    if lo > hi:
        raise ValueError("band lo is above hi in " + repr(raw))
    return (lo, hi)


def parse_bool(text):
    if text is None:
        return False
    return text.strip().lower() in ("true", "yes", "1", "y")


def parse_direction(text):
    if text is None:
        return "none"
    value = text.strip().lower()
    if value == "":
        return "none"
    if value not in ("positive", "negative", "none"):
        raise ValueError("expected_direction must be positive, negative or none")
    return value


def to_float(text):
    if text is None:
        return None
    raw = text.strip()
    if raw == "" or raw.lower() in ("null", "none", "nan"):
        return None
    try:
        return float(raw)
    except ValueError:
        return None


def ranks(values):
    """Average ranks, so ties do not invent an ordering that is not there."""
    order = sorted(range(len(values)), key=lambda i: values[i])
    result = [0.0] * len(values)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and values[order[j + 1]] == values[order[i]]:
            j += 1
        average = (i + j) / 2.0 + 1.0
        for k in range(i, j + 1):
            result[order[k]] = average
        i = j + 1
    return result


def pearson(xs, ys):
    n = len(xs)
    if n < 2:
        return None
    mean_x = sum(xs) / n
    mean_y = sum(ys) / n
    sxx = sum((v - mean_x) ** 2 for v in xs)
    syy = sum((v - mean_y) ** 2 for v in ys)
    if sxx <= 0.0 or syy <= 0.0:
        return None
    sxy = sum((xs[i] - mean_x) * (ys[i] - mean_y) for i in range(n))
    return sxy / math.sqrt(sxx * syy)


def spearman(xs, ys):
    """None means undefined - a constant input, not an effect of zero."""
    if len(xs) < 2:
        return None
    return pearson(ranks(xs), ranks(ys))


def direction_of(rho):
    if rho is None:
        return "undefined"
    if rho > 0:
        return "positive"
    if rho < 0:
        return "negative"
    return "none"


def load_rows(path, conditioning):
    """Returns (pairs, strata, shape_error). pairs is a list of (x, y)."""
    if not os.path.exists(path):
        return ([], {}, "no result file was produced for this phenomenon")
    with open(path, "r", newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        field_names = reader.fieldnames or []
        if "x" not in field_names or "y" not in field_names:
            return ([], {}, "population_query must return columns x and y; got "
                    + ", ".join(field_names) if field_names else "an empty result")
        if conditioning and conditioning not in field_names:
            return ([], {}, "conditioning_variable '" + conditioning
                    + "' is not a column of the result; got " + ", ".join(field_names))
        pairs = []
        strata = {}
        for row in reader:
            x = to_float(row.get("x"))
            y = to_float(row.get("y"))
            if x is None or y is None:
                continue
            pairs.append((x, y))
            if conditioning:
                key = (row.get(conditioning) or "").strip()
                strata.setdefault(key, []).append((x, y))
        return (pairs, strata, None)


def conditioned_effect(strata):
    """Population-weighted mean of the within-stratum effect.

    Strata thinner than MIN_STRATUM_PAIRS are skipped rather than allowed to
    dominate the average with noise, and a stratum whose effect is undefined is
    skipped rather than counted as zero. Returns (value, used, skipped).
    """
    total_weight = 0
    total = 0.0
    used = 0
    skipped = 0
    for key in sorted(strata.keys()):
        pairs = strata[key]
        if len(pairs) < MIN_STRATUM_PAIRS:
            skipped += 1
            continue
        rho = spearman([p[0] for p in pairs], [p[1] for p in pairs])
        if rho is None:
            skipped += 1
            continue
        total += rho * len(pairs)
        total_weight += len(pairs)
        used += 1
    if total_weight == 0:
        return (None, used, skipped)
    return (total / total_weight, used, skipped)


def judge(row, pairs, strata, shape_error):
    """Returns a result dict. Every reason is stated, never implied."""
    result = {
        "phenomenon_id": row["phenomenon_id"],
        "population": len(pairs),
        "minimum_population": None,
        "effect": None,
        "direction": None,
        "conditioned_effect": None,
        "strata_used": 0,
        "strata_skipped": 0,
        "negative_control": parse_bool(row.get("negative_control")),
        "verdict": "ERROR",
        "reasons": [],
    }

    if shape_error:
        result["reasons"].append(shape_error)
        return result

    try:
        minimum = int(str(row.get("minimum_population") or "0").strip() or "0")
        band = parse_band(row.get("expected_effect_band"))
        after_band = parse_band(row.get("expected_after_conditioning"))
        expected = parse_direction(row.get("expected_direction"))
    except ValueError as error:
        result["reasons"].append("manifest row is malformed: " + str(error))
        return result

    result["minimum_population"] = minimum
    conditioning = (row.get("conditioning_variable") or "").strip()

    if len(pairs) < minimum:
        result["verdict"] = "INSUFFICIENT"
        result["reasons"].append(
            "population " + str(len(pairs)) + " is below the declared minimum "
            + str(minimum) + "; refusing to judge rather than passing quietly")
        return result

    rho = spearman([p[0] for p in pairs], [p[1] for p in pairs])
    result["effect"] = rho
    result["direction"] = direction_of(rho)

    if rho is None:
        result["verdict"] = "FAIL"
        result["reasons"].append(
            "the effect is undefined because x or y is constant; this is not an "
            "effect of zero and is not reported as one")
        return result

    failures = []
    magnitude = abs(rho)

    if result["negative_control"]:
        # The whole point of a negative control is that it stays silent.
        if band is not None and not (band[0] <= magnitude <= band[1]):
            failures.append(
                "NEGATIVE CONTROL IS CORRELATING: |rho| " + format(magnitude, ".4f")
                + " is outside its permitted band " + format(band[0], ".4f") + ".."
                + format(band[1], ".4f") + ". A negative control that starts "
                "correlating is a failure, not a curiosity")
    else:
        if expected != "none" and result["direction"] != expected:
            failures.append(
                "direction is " + result["direction"] + ", expected " + expected)
        if band is not None and not (band[0] <= rho <= band[1]):
            failures.append(
                "effect " + format(rho, ".4f") + " is outside the expected band "
                + format(band[0], ".4f") + ".." + format(band[1], ".4f"))

    if conditioning:
        value, used, skipped = conditioned_effect(strata)
        result["conditioned_effect"] = value
        result["strata_used"] = used
        result["strata_skipped"] = skipped
        if value is None:
            failures.append(
                "no stratum of '" + conditioning + "' had at least "
                + str(MIN_STRATUM_PAIRS) + " usable pairs, so the conditioned "
                "effect could not be measured")
        elif after_band is not None and not (after_band[0] <= value <= after_band[1]):
            failures.append(
                "conditioned effect " + format(value, ".4f") + " is outside "
                + format(after_band[0], ".4f") + ".." + format(after_band[1], ".4f")
                + " after conditioning on " + conditioning)

    if failures:
        result["verdict"] = "FAIL"
        result["reasons"] = failures
    else:
        result["verdict"] = "PASS"
        result["reasons"].append("population, direction, effect band"
                                 + (" and conditioned band" if conditioning else "")
                                 + " all met")
    return result



def build_selftest(folder):
    """Fixtures that force every verdict the runner can produce.

    This exists so the harness can be trusted BEFORE it is pointed at real
    phenomena. If the engine cannot produce a FAIL on demand, a manifest full of
    PASSes proves nothing.
    """
    import random
    random.seed(7)
    os.makedirs(folder, exist_ok=True)
    data = os.path.join(folder, "data")
    os.makedirs(data, exist_ok=True)

    def write(name, header, rows):
        with open(os.path.join(data, name + ".csv"), "w", newline="",
                  encoding="utf-8") as handle:
            writer = csv.writer(handle)
            writer.writerow(header)
            for row in rows:
                writer.writerow(row)

    strong = []
    for i in range(300):
        x = random.uniform(0, 100)
        strong.append([round(x, 3), round(0.9 * x + random.gauss(0, 8), 3),
                       "G" + str(i % 3)])
    write("SELFTEST_PASS", ["x", "y", "grade"], strong)

    inverted = []
    for i in range(300):
        x = random.uniform(0, 100)
        inverted.append([round(x, 3), round(-0.8 * x + random.gauss(0, 10), 3)])
    write("SELFTEST_FAIL", ["x", "y"], inverted)

    write("SELFTEST_INSUFFICIENT", ["x", "y"], [[i, i * 2] for i in range(12)])

    noisy = []
    for i in range(300):
        x = random.uniform(0, 100)
        noisy.append([round(x, 3), round(0.7 * x + random.gauss(0, 15), 3)])
    write("SELFTEST_NEGCTL", ["x", "y"], noisy)

    write("SELFTEST_CONSTANT", ["x", "y"], [[i, 5] for i in range(300)])

    rows = [
        ("SELFTEST_PASS", "positive", "100", "0.70..1.00", "grade", "0.60..1.00", "false"),
        ("SELFTEST_FAIL", "positive", "100", "0.30..0.90", "", "", "false"),
        ("SELFTEST_INSUFFICIENT", "positive", "100", "0.30..0.90", "", "", "false"),
        ("SELFTEST_NEGCTL", "none", "100", "0.00..0.10", "", "", "true"),
        ("SELFTEST_CONSTANT", "positive", "100", "0.10..0.90", "", "", "false"),
    ]
    manifest = os.path.join(folder, "manifest.csv")
    with open(manifest, "w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=MANIFEST_COLUMNS)
        writer.writeheader()
        for r in rows:
            writer.writerow({
                "phenomenon_id": r[0],
                "population_query": "-- self-test fixture, no database",
                "expected_direction": r[1],
                "minimum_population": r[2],
                "expected_effect_band": r[3],
                "conditioning_variable": r[4],
                "expected_after_conditioning": r[5],
                "negative_control": r[6],
            })
    return (manifest, data)


EXPECTED_SELFTEST = {
    "SELFTEST_PASS": "PASS",
    "SELFTEST_FAIL": "FAIL",
    "SELFTEST_INSUFFICIENT": "INSUFFICIENT",
    "SELFTEST_NEGCTL": "FAIL",
    "SELFTEST_CONSTANT": "FAIL",
}


def fmt(value, width=10):
    if value is None:
        return "-".rjust(width)
    return format(value, ".4f").rjust(width)


def main():
    parser = argparse.ArgumentParser(description="PPIQ phenomenon harness verdicts")
    parser.add_argument("--manifest", default=None)
    parser.add_argument("--datadir", default=None,
                        help="folder holding <phenomenon_id>.csv written by psql")
    parser.add_argument("--json-out", default=None)
    parser.add_argument("--selftest", action="store_true",
                        help="prove the verdict engine on fixtures, no database")
    args = parser.parse_args()

    selftest = args.selftest
    if selftest:
        import tempfile
        folder = os.path.join(tempfile.gettempdir(), "ppiq_t026_selftest")
        args.manifest, args.datadir = build_selftest(folder)
        args.json_out = None
        print("")
        print("SELF-TEST - fixtures only, no database was contacted.")
        print("Each row exists to force one verdict. If any expected verdict is")
        print("not produced, the engine cannot be trusted with real phenomena.")
    if not args.manifest or not args.datadir:
        die("--manifest and --datadir are required unless --selftest is used")

    if not os.path.exists(args.manifest):
        die("manifest not found: " + args.manifest)

    with open(args.manifest, "r", newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        columns = reader.fieldnames or []
        missing = [c for c in MANIFEST_COLUMNS if c not in columns]
        extra = [c for c in columns if c not in MANIFEST_COLUMNS]
        if missing:
            die("manifest is missing frozen columns: " + ", ".join(missing))
        if extra:
            # The eight columns are frozen by the backlog. A ninth means someone
            # widened the contract without a ruling.
            die("manifest has columns outside the frozen eight: " + ", ".join(extra))
        rows = [r for r in reader if (r.get("phenomenon_id") or "").strip() != ""]

    if not rows:
        print("")
        print("The manifest has no phenomenon rows. Nothing to judge.")
        print("T-026 requires at least three hand-checked phenomena demonstrating")
        print("a pass, a fail and a refusal.")
        return 2

    results = []
    seen = set()
    for row in rows:
        pid = row["phenomenon_id"].strip()
        if pid in seen:
            die("duplicate phenomenon_id in the manifest: " + pid)
        seen.add(pid)
        conditioning = (row.get("conditioning_variable") or "").strip()
        pairs, strata, shape_error = load_rows(
            os.path.join(args.datadir, pid + ".csv"), conditioning)
        results.append(judge(row, pairs, strata, shape_error))

    width = max(len(r["phenomenon_id"]) for r in results)
    width = max(width, 20)
    print("")
    print("=" * 78)
    print("PHENOMENON HARNESS RESULTS")
    print("=" * 78)
    print("  " + "phenomenon_id".ljust(width) + " " + "verdict".ljust(13)
          + "population".rjust(11) + "effect".rjust(11) + "conditioned".rjust(12))
    for r in results:
        print("  " + r["phenomenon_id"].ljust(width) + " "
              + r["verdict"].ljust(13)
              + str(r["population"]).rjust(11)
              + fmt(r["effect"], 11)
              + fmt(r["conditioned_effect"], 12))

    print("")
    print("=" * 78)
    print("WHY, ROW BY ROW")
    print("=" * 78)
    for r in results:
        print("")
        print("  " + r["phenomenon_id"] + "  ->  " + r["verdict"])
        if r["negative_control"]:
            print("    declared a NEGATIVE CONTROL")
        if r["strata_skipped"]:
            print("    strata used " + str(r["strata_used"])
                  + ", skipped for thinness or undefined effect "
                  + str(r["strata_skipped"]))
        for reason in r["reasons"]:
            print("    - " + reason)

    counts = {"PASS": 0, "FAIL": 0, "INSUFFICIENT": 0, "ERROR": 0}
    for r in results:
        counts[r["verdict"]] = counts.get(r["verdict"], 0) + 1

    print("")
    print("=" * 78)
    print("SUMMARY")
    print("=" * 78)
    print("  PASS         " + str(counts["PASS"]))
    print("  FAIL         " + str(counts["FAIL"]))
    print("  INSUFFICIENT " + str(counts["INSUFFICIENT"])
          + "   refusal to judge, never counted as a pass")
    print("  ERROR        " + str(counts["ERROR"]))

    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as handle:
            json.dump(results, handle, indent=2)
        print("")
        print("  machine-readable results: " + args.json_out)

    if selftest:
        print("")
        print("=" * 78)
        print("SELF-TEST VERDICT")
        print("=" * 78)
        wrong = 0
        for r in results:
            want = EXPECTED_SELFTEST.get(r["phenomenon_id"], "?")
            ok = "ok" if r["verdict"] == want else "WRONG"
            if r["verdict"] != want:
                wrong += 1
            print("  " + r["phenomenon_id"].ljust(24) + "expected " + want.ljust(14)
                  + "got " + r["verdict"].ljust(14) + ok)
        print("")
        if wrong:
            print("  SELF-TEST FAILED - " + str(wrong) + " verdict(s) wrong.")
            return 1
        print("  SELF-TEST PASSED - the engine produces a pass, a fail, a refusal,")
        print("  a correlating-negative-control failure, and an undefined statistic")
        print("  reported as undefined rather than as an effect of zero.")
        return 0

    if counts["FAIL"] > 0 or counts["ERROR"] > 0:
        print("")
        print("  EXIT 1 - at least one phenomenon failed or errored.")
        return 1
    print("")
    print("  EXIT 0 - no phenomenon failed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
