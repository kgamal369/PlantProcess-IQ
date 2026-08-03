#!/usr/bin/env python3
"""
PPIQ T-014 capture comparator, version 2.

IMPLEMENTS docs/m1/evidence/capture_comparator_spec_v2.md, FROZEN 3 August 2026.

Every rule traces to a numbered section of that document, and the section is
named in the output beside each difference. This file does not decide policy: if
a rule is wrong, the specification is amended first and this file follows.

    python Compare-PpiqCaptureProfiles.py --captured <reference>.txt --regenerated <scratch>.txt
"""

import argparse
import calendar
import math
import re
import sys

SPEC = "capture_comparator_spec_v2.md (v2.1 FINAL)"

# spec 3.6.3 - the declared path from the deterministic tap grid to each
# timestamp column. The horizon tolerance is the sum of the captured RANGES of
# these intervals, floored at 3600 s. Declared here, never inferred.
HORIZON_PATH = {
    "heats": ["A02_tap_duration"],
    "lf_treatment": ["B01_lf_start_offset", "B02_lf_duration"],
    "cast_sequence": ["C01_seq_start_offset", "C02_seq_duration"],
    "cast_pieces": ["C01_seq_start_offset", "C02_seq_duration"],
    "hsm_coils": ["D01_rolling_lag_from_tap", "D02_rolling_duration"],
    "hsm_pass_measurements": ["D01_rolling_lag_from_tap", "D02_rolling_duration"],
    "pickle_orders": ["D01_rolling_lag_from_tap", "D02_rolling_duration",
                      "F01_pkl_entry_lag", "F02_pkl_duration"],
    "qa_lab_results": ["D01_rolling_lag_from_tap", "D02_rolling_duration",
                       "F01_pkl_entry_lag", "F02_pkl_duration", "G01_qa_sample_lag"],
    "parsytec_surface_defects": ["D01_rolling_lag_from_tap", "H01_defect_lag_from_rolling"],
    "downtime_events": ["I01_downtime_span"],
}

K_SIGMA = 4.0            # 3.1  four standard errors
K_QUANTILE = 1.75        # 3.2  conservative quantile efficiency factor
SD_REL = 0.05            # 3.3
EXTREME_K = 10.0         # 3.4
EXTREME_REL = 0.005      # 3.4
U_LOW, U_HIGH = 0.90, 1.10   # 3.4 family boundaries
TS_FLOOR_S = 3600        # 3.6
SPAN_REL = 0.02          # 3.6
DISTINCT_REL = 0.10      # 3.7
RATIO_REL = 0.05         # 3.5


def num(v):
    try:
        return float(str(v).strip())
    except (TypeError, ValueError):
        return None


def parse_tables(text):
    lines = text.replace("\r\n", "\n").split("\n")
    out, section, label, i = [], "?", "", 0
    while i < len(lines):
        ln = lines[i]
        m = re.match(r"SECTION ([A-I]) - ", ln)
        if m:
            section, label = m.group(1), ""
            i += 1
            continue
        m = re.match(r"--- (.+?) ---\s*$", ln)
        if m:
            label = m.group(1)
            i += 1
            continue
        if ln.startswith("+-") and i + 2 < len(lines) and lines[i + 1].startswith("|"):
            header = [c.strip() for c in lines[i + 1].strip().strip("|").split("|")]
            j, rows = i + 3, []
            while j < len(lines) and lines[j].startswith("|"):
                cells = [c.strip() for c in lines[j].strip().strip("|").split("|")]
                if len(cells) == len(header):
                    rows.append(dict(zip(header, cells)))
                j += 1
            out.append((section, label, header, rows))
            i = j
            continue
        i += 1
    return out


def key_of(section, label, header, row):
    if section == "A":
        return (row.get("schema_name"), row.get("table_name"))
    if section in ("B", "C", "I"):
        return (row.get("schema_name"), row.get("table_name"), row.get("column_name"))
    if section == "J":
        return ("interval", row.get("interval_name"))
    if section == "D":
        return (row.get("schema_name"), row.get("table_name"),
                row.get("column_name"), row.get("value"))
    if section == "E":
        return (row.get("column_ref"), row.get("shape"))
    if section == "F":
        return (label, row.get(header[0]))
    if section in ("G", "H"):
        if "check_name" in row:
            return (label, row.get("check_name"))
        if "stand_no" in row:
            return (label, row.get("stand_no"))
        return (label, "single")
    return (section, label, tuple(sorted(row.items())))


TS_RE = re.compile(r"(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2})([+-]\d{2})")


def ts_seconds(v):
    m = TS_RE.match(v or "")
    if not m:
        return None
    y, mo, d, h, mi, s, off = m.groups()
    return calendar.timegm((int(y), int(mo), int(d), int(h), int(mi),
                            int(s), 0, 0, 0)) - int(off) * 3600


def family_of(sd, rng):
    """Spec 3.4. Assigned mechanically from captured statistics, never by hand."""
    if rng is None or rng <= 0 or sd is None:
        return "DEGENERATE"
    u = sd / (rng / math.sqrt(12.0))
    if u > U_HIGH:
        return "SETPOINT_JITTER"
    if u >= U_LOW:
        return "BOUNDED_UNIFORM"
    return "CENTRAL_BOUNDED"


def tol_mean(sd, n):
    return K_SIGMA * sd / math.sqrt(n)


def tol_quantile(sd, n):
    return K_SIGMA * K_QUANTILE * sd / math.sqrt(n)


def tol_sd(sd, n):
    return max(SD_REL * abs(sd), K_SIGMA * abs(sd) / math.sqrt(2.0 * n))


def tol_extreme(rng, n):
    return max(EXTREME_K * rng / (n + 1.0), EXTREME_REL * rng)


def horizon_tol(table, interval_range):
    """Spec 3.6.3 - the sum of the captured ranges of the stochastic intervals on
    the declared path from the deterministic grid to this table."""
    total = 0.0
    for name in HORIZON_PATH.get(table, []):
        total += interval_range.get(name, 0.0)
    return max(TS_FLOOR_S, total)


def tol_ratio(captured, sd_underlying, n):
    floor = 0.0
    if sd_underlying is not None and n:
        floor = K_SIGMA * abs(sd_underlying) / math.sqrt(n)
    return max(RATIO_REL * abs(captured), floor)


class Report(object):
    def __init__(self):
        self.checked = 0
        self.by_column = {}

    def ok(self):
        self.checked += 1

    def fail(self, where, field, cap, reg, tol, rule):
        self.checked += 1
        self.by_column.setdefault(where, []).append((field, cap, reg, tol, rule))

    @property
    def total(self):
        return sum(len(v) for v in self.by_column.values())


def eq(rep, where, field, cap, reg, rule):
    if str(cap) == str(reg):
        rep.ok()
    else:
        rep.fail(where, field, cap, reg, "exact", rule)


def near(rep, where, field, cap, reg, tol, rule):
    c, r = num(cap), num(reg)
    if c is None or r is None:
        eq(rep, where, field, cap, reg, rule)
        return
    if abs(c - r) <= tol:
        rep.ok()
    else:
        rep.fail(where, field, "%.6g" % c, "%.6g" % r, "%.6g" % tol, rule)


def sd_underlying_for(label, row, n_of_table):
    """Spec 3.5 - the SD of the ROW-LEVEL DERIVED METRIC, never a substituted
    source column. Returns (sd, n) or (None, n) when the aggregate carries its
    own per-field SD and is handled field by field."""
    L = (label or "").lower()
    if "slab weight against its dimensions" in L:
        return num(row.get("sd_density")), n_of_table.get(
            ("src_caster_oracle_shape", "cast_pieces"), 1)
    if "coil weight against its slab" in L:
        return num(row.get("sd_ratio")), n_of_table.get(
            ("src_hsm_oracle_shape", "hsm_coils"), 1)
    if "heat weight against the sum" in L:
        return num(row.get("sd_ratio")), n_of_table.get(
            ("src_meltshop_pg", "heats"), 1)
    if "target against actual deviation" in L:
        return None, n_of_table.get(("src_hsm_oracle_shape", "hsm_coils"), 1)
    if "rolling force by stand" in L:
        return None, int(num(row.get("passes")) or 1)
    return None, 1


DEV_PAIR = {"mean_thk_dev": "sd_thk_dev",
            "mean_wid_dev": "sd_wid_dev",
            "mean_fdt_dev": "sd_fdt_dev"}

STAND_PAIR = {"mean_force": ("src_hsm_oracle_shape", "hsm_pass_measurements", "rolling_force_kn"),
              "mean_gap": ("src_hsm_oracle_shape", "hsm_pass_measurements", "roll_gap_mm"),
              "mean_speed": ("src_hsm_oracle_shape", "hsm_pass_measurements", "speed_mps"),
              "mean_temp": ("src_hsm_oracle_shape", "hsm_pass_measurements", "temperature_c")}


def compare(cap_t, reg_t, echo):
    rep = Report()

    n_of_table = {}
    for section, label, header, rows in cap_t:
        if section == "A":
            for row in rows:
                n_of_table[(row["schema_name"], row["table_name"])] = int(row["row_count"])

    col_sd, col_n = {}, {}
    for section, label, header, rows in cap_t:
        if section == "B":
            for row in rows:
                k = (row["schema_name"], row["table_name"], row["column_name"])
                col_sd[k] = num(row["stddev_value"])
                col_n[k] = n_of_table.get((row["schema_name"], row["table_name"]), 1)

    interval_range = {}
    for section, label, header, rows in cap_t:
        if section == "J":
            for row in rows:
                interval_range[row["interval_name"]] = \
                    (num(row["max_s"]) or 0.0) - (num(row["min_s"]) or 0.0)

    reg_index = {}
    for section, label, header, rows in reg_t:
        for row in rows:
            reg_index[key_of(section, label, header, row)] = row

    if not interval_range:
        echo("")
        echo("[FAIL] the captured profile carries no SECTION J. Comparator v2.1 reads")
        echo("       the process intervals from it. Re-run the capture after applying")
        echo("       the section J pack.")
        raise SystemExit(3)

    echo("")
    echo("HORIZON TOLERANCES (spec 3.6.3), from the captured interval ranges")
    for t in sorted(HORIZON_PATH):
        echo("  %-28s %10d s   %s" % (t, int(horizon_tol(t, interval_range)),
                                      " + ".join(HORIZON_PATH[t])))

    echo("")
    echo("COMPUTED TOLERANCES PER NUMERIC COLUMN (spec 3.1 to 3.4)")
    echo("%-52s %6s %11s %11s %-16s %11s %13s"
         % ("column", "n", "sd", "range", "family", "tol_mean", "tol_extreme"))
    echo("-" * 126)
    for section, label, header, rows in cap_t:
        if section != "B":
            continue
        for row in rows:
            k = (row["schema_name"], row["table_name"], row["column_name"])
            n = col_n.get(k, 1)
            sd = num(row["stddev_value"]) or 0.0
            rng = (num(row["max_value"]) or 0.0) - (num(row["min_value"]) or 0.0)
            if sd == 0.0:
                fam, tm, te = "ZERO_VARIANCE", "exact", "exact"
            else:
                fam = family_of(sd, rng)
                tm = "%.6g" % tol_mean(sd, n)
                te = "containment" if fam == "CENTRAL_BOUNDED" else "%.6g" % tol_extreme(rng, n)
            echo("%-52s %6d %11.6g %11.6g %-16s %11s %13s"
                 % (k[1] + "." + k[2], n, sd, rng, fam, tm, te))

    for section, label, header, rows in cap_t:
        for row in rows:
            k = key_of(section, label, header, row)
            where = " / ".join(str(x) for x in k if x)
            other = reg_index.get(k)
            if other is None:
                rep.fail(where, "-", "present", "ABSENT", "exact",
                         "spec 2, the regenerated profile has no matching entry")
                continue

            if section == "A":
                eq(rep, where, "row_count", row["row_count"], other["row_count"],
                   "spec 2 exact")
                eq(rep, where, "column_count", row["column_count"], other["column_count"],
                   "spec 2 exact")

            elif section == "B":
                n = col_n.get(k, 1)
                sd = num(row["stddev_value"]) or 0.0
                rng = (num(row["max_value"]) or 0.0) - (num(row["min_value"]) or 0.0)
                eq(rep, where, "max_decimal_scale", row["max_decimal_scale"],
                   other["max_decimal_scale"], "spec 2 exact")
                if sd == 0.0:
                    for f in ("min_value", "p10", "p25", "p50", "p75", "p90",
                              "max_value", "mean_value", "stddev_value"):
                        eq(rep, where, f, row[f], other[f],
                           "spec 2, zero variance compared exactly")
                    continue
                near(rep, where, "mean_value", row["mean_value"], other["mean_value"],
                     tol_mean(sd, n), "spec 3.1, 4 SE of the mean")
                for f in ("p10", "p25", "p50", "p75", "p90"):
                    near(rep, where, f, row[f], other[f], tol_quantile(sd, n),
                         "spec 3.2, 4 SE x 1.75")
                near(rep, where, "stddev_value", row["stddev_value"],
                     other["stddev_value"], tol_sd(sd, n),
                     "spec 3.3, max(5 percent, 4 SE of sd)")

                fam = family_of(sd, rng)
                if fam == "CENTRAL_BOUNDED":
                    scale = int(num(row["max_decimal_scale"]) or 0)
                    epsilon = 10.0 ** (-scale) if scale > 0 else 1.0
                    cmin, cmax = num(row["min_value"]), num(row["max_value"])
                    rmin, rmax = num(other["min_value"]), num(other["max_value"])
                    if rmin is not None and cmin is not None and rmin >= cmin - epsilon:
                        rep.ok()
                    else:
                        rep.fail(where, "min_value", "%.6g" % cmin, "%.6g" % rmin,
                                 "containment",
                                 "spec 3.4 CENTRAL_BOUNDED, drew below the captured support")
                    if rmax is not None and cmax is not None and rmax <= cmax + epsilon:
                        rep.ok()
                    else:
                        rep.fail(where, "max_value", "%.6g" % cmax, "%.6g" % rmax,
                                 "containment",
                                 "spec 3.4 CENTRAL_BOUNDED, drew above the captured support")
                else:
                    te = tol_extreme(rng, n)
                    for f in ("min_value", "max_value"):
                        near(rep, where, f, row[f], other[f], te,
                             "spec 3.4 " + fam + ", two-sided extreme")
                    rrng = (num(other["max_value"]) or 0.0) - (num(other["min_value"]) or 0.0)
                    if rrng - rng <= te:
                        rep.ok()
                    else:
                        rep.fail(where, "range", "%.6g" % rng, "%.6g" % rrng, "%.6g" % te,
                                 "spec 3.4 condition 2, regenerated range exceeds the captured support")

            elif section == "C":
                # spec 3.6.3 - HORIZON CONTAINMENT ONLY. The absolute extreme of a
                # resampled dataset is a lottery among the units near the boundary.
                # What is actually compared is the interval, in section J.
                te = horizon_tol(k[1], interval_range)
                for f in ("min_ts", "max_ts"):
                    a, b = ts_seconds(row[f]), ts_seconds(other[f])
                    if a is None or b is None:
                        eq(rep, where, f, row[f], other[f], "spec 3.6.3")
                    elif abs(a - b) <= te:
                        rep.ok()
                    else:
                        rep.fail(where, f, row[f], other[f], "%d s" % int(te),
                                 "spec 3.6.3 horizon containment, delta %d s" % abs(a - b))
                near(rep, where, "span_days", row["span_days"], other["span_days"],
                     2.0 * te / 86400.0, "spec 3.6.3 horizon span")
                near(rep, where, "distinct_ts", row["distinct_ts"], other["distinct_ts"],
                     DISTINCT_REL * abs(num(row["distinct_ts"]) or 1.0),
                     "spec 3.7 distinct")

            elif section == "D":
                eq(rep, where, "occurrences", row["occurrences"], other["occurrences"],
                   "spec 2 exact, categorical count")

            elif section == "E":
                eq(rep, where, "occurrences", row["occurrences"], other["occurrences"],
                   "spec 2 exact, identifier shape count")
                eq(rep, where, "example", row["example"], other["example"],
                   "spec 2 exact, identifier example")

            elif section == "F":
                for f in header[1:]:
                    eq(rep, where, f, row[f], other[f], "spec 2 exact, cardinality")

            elif section in ("G", "H"):
                if "violations" in row:
                    eq(rep, where, "violations", row["violations"], other["violations"],
                       "spec 2 exact, integrity violation")
                    continue
                vals = [num(row[f]) for f in header if num(row[f]) is not None]
                if len(vals) >= 2 and max(vals) == min(vals):
                    for f in header:
                        eq(rep, where, f, row[f], other[f],
                           "spec 2, zero variance compared exactly")
                    continue
                sd_u, n_u = sd_underlying_for(label, row, n_of_table)
                for f in header:
                    if f in ("check_name", "stand_no"):
                        continue
                    if f == "passes":
                        eq(rep, where, f, row[f], other[f], "spec 2 exact, row count")
                        continue
                    c = num(row[f])
                    if c is None:
                        eq(rep, where, f, row[f], other[f], "spec 2 exact")
                        continue
                    sd_field = sd_u
                    if f in DEV_PAIR:
                        sd_field = num(row.get(DEV_PAIR[f]))
                    elif f in STAND_PAIR:
                        sd_field = col_sd.get(STAND_PAIR[f])
                    near(rep, where, f, row[f], other[f], tol_ratio(c, sd_field, n_u),
                         "spec 3.5, max(5 percent, 4 SE of the row-level metric)")

            elif section == "J":
                n = int(num(row["observations"]) or 1)
                sd = num(row["sd_s"]) or 0.0
                rng = (num(row["max_s"]) or 0.0) - (num(row["min_s"]) or 0.0)
                eq(rep, where, "observations", row["observations"], other["observations"],
                   "spec 2 exact, interval observation count")
                if sd == 0.0:
                    for f in ("min_s", "p10", "p25", "p50", "p75", "p90",
                              "max_s", "mean_s", "sd_s", "distinct_values"):
                        eq(rep, where, f, row[f], other[f],
                           "spec 3.6.1, deterministic interval compared exactly")
                    continue
                near(rep, where, "mean_s", row["mean_s"], other["mean_s"],
                     tol_mean(sd, n), "spec 3.6.2 via 3.1, 4 SE of the mean")
                for f in ("p10", "p25", "p50", "p75", "p90"):
                    near(rep, where, f, row[f], other[f], tol_quantile(sd, n),
                         "spec 3.6.2 via 3.2, 4 SE x 1.75")
                near(rep, where, "sd_s", row["sd_s"], other["sd_s"], tol_sd(sd, n),
                     "spec 3.6.2 via 3.3, max(5 percent, 4 SE of sd)")
                fam = family_of(sd, rng)
                if fam == "CENTRAL_BOUNDED":
                    cmin, cmax = num(row["min_s"]), num(row["max_s"])
                    rmin, rmax = num(other["min_s"]), num(other["max_s"])
                    if rmin is not None and cmin is not None and rmin >= cmin - 1.0:
                        rep.ok()
                    else:
                        rep.fail(where, "min_s", "%.6g" % cmin, "%.6g" % rmin, "containment",
                                 "spec 3.6.2 via 3.4 CENTRAL_BOUNDED, drew below the captured support")
                    if rmax is not None and cmax is not None and rmax <= cmax + 1.0:
                        rep.ok()
                    else:
                        rep.fail(where, "max_s", "%.6g" % cmax, "%.6g" % rmax, "containment",
                                 "spec 3.6.2 via 3.4 CENTRAL_BOUNDED, drew above the captured support")
                else:
                    te = tol_extreme(rng, n)
                    for f in ("min_s", "max_s"):
                        near(rep, where, f, row[f], other[f], te,
                             "spec 3.6.2 via 3.4 " + fam + ", two-sided extreme")
                near(rep, where, "distinct_values", row["distinct_values"],
                     other["distinct_values"],
                     DISTINCT_REL * abs(num(row["distinct_values"]) or 1.0),
                     "spec 3.7 distinct")

            elif section == "I":
                for f in ("min_len", "max_len"):
                    eq(rep, where, f, row[f], other[f], "spec 2 exact, text length")
                near(rep, where, "distinct_values", row["distinct_values"],
                     other["distinct_values"],
                     DISTINCT_REL * abs(num(row["distinct_values"]) or 1.0),
                     "spec 3.7 distinct")

    cap_keys = set()
    for section, label, header, rows in cap_t:
        for row in rows:
            cap_keys.add(key_of(section, label, header, row))
    for k in sorted(set(reg_index) - cap_keys, key=lambda x: [str(i) for i in x]):
        rep.fail(" / ".join(str(x) for x in k if x), "-", "ABSENT", "present",
                 "exact", "spec 2, the regenerated profile invented an entry")
    return rep


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--captured", required=True)
    ap.add_argument("--regenerated", required=True)
    args = ap.parse_args()

    def echo(s=""):
        print(s)

    echo("=" * 78)
    echo("PPIQ T-014 - CAPTURE COMPARISON, COMPARATOR v2")
    echo("=" * 78)
    echo("specification : " + SPEC + " (FROZEN)")
    echo("captured      : " + args.captured)
    echo("regenerated   : " + args.regenerated)
    echo("")
    echo("THE CAPTURED PROFILE IS A FIXED REFERENCE, NOT A SECOND RANDOM SAMPLE.")
    echo("All tolerances are one-sample rules. No two-sample sqrt(2) is applied.")
    echo("")
    echo("RULES, from the frozen specification:")
    echo("  spec 2    EXACT - schema, row and column counts, categorical values and")
    echo("            counts, identifier shapes, cardinality, integrity violations,")
    echo("            decimal scale, text lengths, every zero-variance column")
    echo("  spec 3.1  mean       4 * sd / sqrt(n)")
    echo("  spec 3.2  quantiles  4 * 1.75 * sd / sqrt(n)")
    echo("  spec 3.3  stddev     max(5 percent, 4 * sd / sqrt(2n))")
    echo("  spec 3.4  extremes   family from u = sd / (range / sqrt(12))")
    echo("            BOUNDED_UNIFORM, SETPOINT_JITTER  two-sided,")
    echo("                       max(10 * range / (n+1), 0.5 percent of range)")
    echo("            CENTRAL_BOUNDED                   containment only")
    echo("  spec 3.5  ratios     max(5 percent, 4 SE of the ROW-LEVEL metric)")
    echo("  spec 3.6.1 deterministic intervals   EXACT")
    echo("  spec 3.6.2 stochastic intervals      compared as distributions")
    echo("             using 3.1 to 3.4, in SECTION J")
    echo("  spec 3.6.3 absolute min_ts / max_ts  horizon containment only")
    echo("  spec 3.7  distinct   10 percent relative")

    cap = open(args.captured, encoding="utf-8", errors="replace").read()
    reg = open(args.regenerated, encoding="utf-8", errors="replace").read()
    cap_t, reg_t = parse_tables(cap), parse_tables(reg)
    echo("")
    echo("tables parsed : captured %d, regenerated %d" % (len(cap_t), len(reg_t)))
    if not cap_t or not reg_t:
        echo("[FAIL] one profile parsed to nothing. Refusing to report a comparison.")
        return 3

    rep = compare(cap_t, reg_t, echo)

    if rep.by_column:
        echo("")
        echo("-" * 78)
        echo("DIFFERENCES, GROUPED PER COLUMN")
        echo("-" * 78)
        echo("Spec 3.2: the quantiles of one sample are CORRELATED. Several quantile")
        echo("misses on ONE column are ONE event, not several findings.")
        for where in sorted(rep.by_column):
            items = rep.by_column[where]
            echo("")
            echo("[%s]  %d difference(s)" % (where, len(items)))
            for field, c, r, tol, rule in items:
                echo("    %-20s captured %-22s regenerated %-22s" % (field, c, r))
                echo("    %-20s tolerance %-21s %s" % ("", tol, rule))

    echo("")
    echo("=" * 78)
    echo("COMPARISONS RUN    : %d" % rep.checked)
    echo("COLUMNS AFFECTED   : %d" % len(rep.by_column))
    echo("TOTAL DIFFERENCES  : %d" % rep.total)
    echo("=" * 78)
    if rep.total:
        echo("T-014 IS NOT PROVEN.")
        echo("Report the remaining differences. DO NOT alter the frozen comparator")
        echo("specification because of this result.")
        return 1
    echo("T-014 CAPTURE PROVEN across all nine dimensions.")
    echo("Condition 1 of the Chapter 3 section 4.5.2a retirement gate is met.")
    echo("Conditions 2, 3 and 4 remain: regenerate both presentation layers, pass")
    echo("cross-layer certification, and take AND RESTORE one backup.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
