#!/usr/bin/env python3
"""
PPIQ T-014 - deterministic generator for the source-shaped donor schemas.

WHAT THIS IS
    The ten donor tables in ppiq_presentation have no producer in source control.
    Their rows exist only inside a 29.4 MB binary snapshot. This file is that
    producer, written from ONE input: the capture profile at
    docs/m1/evidence/T-014_capture_profile_20260803_185702.txt.

    Every constant below traces to a number in that file. Nothing is copied from
    a document, a chart, or from memory.

WHAT THIS IS NOT
    It is NOT an improvement. T-014 captures; T-015 onward merges, enhances and
    scales. That separation exists so captured work stays distinguishable from
    newly designed work, so this file DELIBERATELY REPRODUCES SIX MEASURED
    FAULTS. Each is marked FAULT-n at its site.

    FAULT-1  MASS IS NOT CONSERVED. weight_kg is drawn independently of
             width/thickness/length, so implied slab density runs 4,079 to
             17,391 kg/m3 against steel at about 7,850. Slabs from a heat weigh
             1.38x the heat on average; a coil can weigh 1.58x its own slab.
    FAULT-2  actual_thickness_mm EQUALS target_thickness_mm on every coil.
             Deviation mean 0.0000, stddev 0.0000. Thickness control is perfect.
    FAULT-3  THE MILL HAS NO PROFILE. All seven stands draw from one
             distribution, so force, gap, speed and temperature are statistically
             identical stand to stand. A real finishing mill collapses the gap
             and raises the speed down the stands.
    FAULT-4  PRODUCTION IS A METRONOME. Consecutive heats are exactly 4,200 s
             apart, every time, and the 210 downtime events disturb nothing.
    FAULT-5  CARDINALITY IS FIXED, NOT DISTRIBUTED. 9 slabs per heat, 9 coils per
             heat, 7 passes per coil, 3 QA rows per coil, 1 LF per heat, without
             a single exception.
    FAULT-6  QA VALUES ARE NOT PHYSICAL. One uniform draw over 1.07 to 1599.88 is
             stamped on WIDTH mm, THK mm and ROUGHNESS um alike, so thickness
             reaches 1,599 mm.

    CHEMISTRY IS NOT CONDITIONED ON GRADE, AND MUST NOT BE.
    Section G of the structure evidence measured carbon, manganese and silicon per
    steel grade: all six grades share one range, with minima 0.0250 to 0.0287 and
    maxima 0.1711 to 0.1793 for carbon, and standard deviations 0.0390 to 0.0467.
    Section H shows the same for weight, temperature, oxygen and power. The
    captured donor contains no grade relationship, so this generator introduces
    none. Comparator spec v2 section 4a forbids it explicitly.

    Also reproduced: superheat_c goes negative (min -3.0), which is
    thermodynamically impossible; seven columns carry a single value; the defect
    Pareto is flat; planned_grade always equals actual_grade.

USAGE
    python generate_fleet_v2_donor.py --out donor_data.sql
    python generate_fleet_v2_donor.py --out donor_data.sql --seed 20260803
    python generate_fleet_v2_donor.py --profile        (print distributions, no file)

    The emitted SQL assumes the schemas already exist, created by
    Backend/database/scripts/110_phase1_demo_source_shapes.sql.
"""

import argparse
import math
import random
import sys
from datetime import datetime, timedelta, timezone

SEED_DEFAULT = 20260803

# ---------------------------------------------------------------- scale, Section A and F
N_HEATS = 630
SLABS_PER_HEAT = 9          # FAULT-5: exact, all 630 heats
COILS_PER_HEAT = 9          # FAULT-5
PASSES_PER_COIL = 7         # FAULT-5
QA_ROWS_PER_COIL = 3        # FAULT-5
LF_PER_HEAT = 1             # FAULT-5
N_DOWNTIME = 210
N_DEFECTS = 1987
N_PIECES = N_HEATS * SLABS_PER_HEAT          # 5670
N_COILS = N_HEATS * COILS_PER_HEAT           # 5670

# defects per coil, Section F: 4670 coils with none, then 326 / 361 / 313
DEFECT_LADDER = [(0, 4670), (1, 326), (2, 361), (3, 313)]

# ---------------------------------------------------------------- catalogues, Section D
GRADES = [("HSLA-420", 114), ("DX51D", 109), ("S235JR", 108),
          ("S355MC", 105), ("IF-LOW-C", 98), ("DP600", 96)]
TUNDISH = [("TD-4", 117), ("TD-5", 116), ("TD-6", 115),
           ("TD-3", 101), ("TD-2", 94), ("TD-1", 87)]
FURNACE = [("EAF-02", 327), ("EAF-01", 303)]
LF_CODE = [("LF-02", 318), ("LF-01", 312)]
LF_SAMPLE = [("OK", 579), ("CHECK", 51)]
CASTER_SEQ = [("CCM-01", 318), ("CCM-02", 312)]
LINE_ID = [("PKL-02", 2857), ("PKL-01", 2813)]
INSPECTION_RESULT = [("OK", 4970), ("CHECK", 700)]
QA_DECISION = [("Accepted", 4970), ("Downgraded", 418), ("Hold", 219), ("Rejected", 63)]
QA_STATUS = [("OK", 16359), ("CHECK", 651)]
SIDE_CODE = [("BOT", 1016), ("TOP", 971)]
SEVERITY = [("medium", 699), ("high", 648), ("low", 640)]

# FAULT: the Pareto is flat - six codes inside a three-point spread
DEFECTS = [("PINHOLE", "Pinhole", "surface", 351),
           ("SCALE", "Scale", "surface", 347),
           ("EDGE_CRACK", "Edge crack", "edge", 341),
           ("ROLLED_IN", "Rolled-in scale", "surface", 335),
           ("SCRATCH", "Scratch", "surface", 319),
           ("WAVINESS", "Waviness", "shape", 294)]

DOWNTIME_REASONS = [("HYDRAULIC_ALARM", "Hydraulic alarm", "unplanned", 50),
                    ("QUALITY_HOLD", "Quality hold", "quality", 47),
                    ("MECH_ROLL_CHANGE", "Mechanical roll change", "planned", 41),
                    ("SENSOR_FAULT", "Sensor fault", "unplanned", 41),
                    ("ENTRY_DELAY", "Entry section delay", "logistics", 31)]

DOWNTIME_EQUIPMENT = [("CCM-02", 32), ("HSM-01", 26), ("PKL-02", 26), ("CCM-01", 22),
                      ("EAF-01", 22), ("EAF-02", 22), ("LF-01", 22), ("PKL-01", 20),
                      ("LF-02", 18)]

# constant columns, Section D at 100 percent
PLANT_CODE = "DEMO_PLANT"
ROUTE_CODE = "EAF-LF-CCM-HSM-PKL"          # single value across all 630 heats
SEQUENCE_STATUS = "Completed"
MILL_LINE = "HSM-01"
INSPECTION_DEVICE = "PARSYTEC-01"
TARGET_TEMP_C = 1650.00                    # constant, no target variation exists
TARGET_FDT_C = 875.00
TARGET_CT_C = 610.00

# ---------------------------------------------------------------- time base, Section C
# EVERY CONSTANT BELOW CITES THE COMMITTED EVIDENCE IT CAME FROM.
#   CAPTURE   = docs/m1/evidence/T-014_capture_profile_20260803_185702.txt
#   STRUCTURE = docs/m1/evidence/T-014_structure_evidence_20260803_230235.txt
# Comparator spec v2 section 4a forbids a number reaching this file from
# terminal output or from memory. If a value below has no citation, it is a
# defect regardless of what the comparison says.

TZ = timezone(timedelta(hours=2))
T0_TAP = datetime(2026, 4, 1, 2, 0, 0, tzinfo=TZ)   # CAPTURE C, heats.tap_start_utc min
HEAT_INTERVAL_S = 4200                     # STRUCTURE H rhythm: min=median=mean=max. FAULT-4

# STRUCTURE A. Only TWO intervals are constant; the rest are real distributions
# quantised to whole minutes, which the distinct_values column proves:
#   tap_end - tap_start      2520..3480, 17 distinct = (3480-2520)/60 + 1
#   lf_start - tap_start     3000..4440, 25 distinct
#   lf_end - lf_start        1680..2880, 21 distinct
#   seq_start - tap_start    5880..8820, 48 distinct of 50 possible
#   rolling_end - start       480..1080, 11 distinct
#   pkl_exit - pkl_entry     1200..3300, 36 distinct
TAP_DUR_S = (2520, 3480)
LF_START_OFF_S = (3000, 4440)
LF_DUR_S = (1680, 2880)
SEQ_START_OFF_S = (5880, 8820)
ROLL_DUR_S = (480, 1080)
PKL_DUR_S = (1200, 3300)

# STRUCTURE A, sd 0 and distinct 1 - these two ARE deterministic
QA_AFTER_EXIT_S = 300                      # STRUCTURE A: qa_sample - pkl_exit
PASS_STEP_S = 60                           # STRUCTURE E: offset is EXACTLY stand_no * 60

# STRUCTURE C. Slab n is cut at seq_start + n * step, and step takes exactly four
# values: slab_no 1 spans 360..540 with 4 distinct, and slab_no 9 spans 3240..4860,
# which is 9 x the same four. STRUCTURE A confirms seq_end - seq_start also has
# exactly 4 distinct values over 3240..4860.
CUT_STEPS_S = (360, 420, 480, 540)

# STRUCTURE B. The rolling lag is NOT deterministic: sd is about 29,400 s at every
# coil position with 517 to 535 distinct values. Overall 18360..123540, and the
# per-position mean drifts from 68420 to 73867, which is (73867-68420)/8 = 681 s
# per position.
ROLL_LAG_S = (18360, 123540)
ROLL_LAG_POS_DRIFT_S = 681

# STRUCTURE D. The pickling lag is uniform in WHOLE HOURS: 14400..259200 with
# exactly 69 distinct values, and 259200-14400 = 68 hours = 68 steps.
PKL_LAG_HOURS = (4, 72)

# STRUCTURE F. The defect lag is 600..2280 with 29 distinct values, mean 1440 and
# sd 368. A uniform over that range would have sd 485, so it is centrally
# concentrated rather than flat.
DEFECT_LAG_S = (600, 2280)
DEFECT_LAG_MEAN_S = 1440
DEFECT_LAG_SD_S = 368


# ---------------------------------------------------------------- EXACT INTERVAL POOLS
# Source: docs/m1/evidence/T-014_interval_histograms_20260804_001518.txt, section K.
# These are the COMPLETE measured value lists with their exact counts. The
# generator draws from them without assuming any named distribution, because a
# shape was guessed twice in this task and was wrong twice. An exact pool
# reproduces the mean, standard deviation, every quantile, both extremes and the
# distinct count by construction.
INTERVAL_POOLS = {
    "A02_tap_duration": (
        (2520, 41), (2580, 38), (2640, 22), (2700, 33), (2760, 38), (2820, 34),
        (2880, 38), (2940, 39), (3000, 34), (3060, 31), (3120, 43), (3180, 37),
        (3240, 34), (3300, 39), (3360, 37), (3420, 51), (3480, 41)),
    "B01_lf_start_offset": (
        (3000, 6), (3060, 9), (3120, 12), (3180, 15), (3240, 14), (3300, 25),
        (3360, 34), (3420, 23), (3480, 38), (3540, 32), (3600, 42), (3660, 41),
        (3720, 34), (3780, 36), (3840, 36), (3900, 33), (3960, 39), (4020, 25),
        (4080, 32), (4140, 28), (4200, 25), (4260, 20), (4320, 14), (4380, 14),
        (4440, 3)),
    "B02_lf_duration": (
        (1680, 30), (1740, 32), (1800, 24), (1860, 29), (1920, 19), (1980, 29),
        (2040, 34), (2100, 27), (2160, 31), (2220, 34), (2280, 38), (2340, 36),
        (2400, 26), (2460, 29), (2520, 29), (2580, 29), (2640, 26), (2700, 30),
        (2760, 35), (2820, 28), (2880, 35)),
    "C01_seq_start_offset": (
        (5880, 1), (6000, 1), (6060, 1), (6120, 2), (6180, 2), (6240, 6),
        (6300, 6), (6360, 6), (6420, 7), (6480, 7), (6540, 5), (6600, 11),
        (6660, 12), (6720, 19), (6780, 17), (6840, 15), (6900, 19), (6960, 19),
        (7020, 24), (7080, 17), (7140, 28), (7200, 10), (7260, 27), (7320, 29),
        (7380, 26), (7440, 31), (7500, 22), (7560, 26), (7620, 27), (7680, 26),
        (7740, 20), (7800, 25), (7860, 16), (7920, 19), (7980, 19), (8040, 14),
        (8100, 10), (8160, 8), (8220, 8), (8280, 10), (8340, 7), (8400, 6),
        (8460, 7), (8520, 4), (8580, 3), (8640, 2), (8700, 2), (8820, 1)),
    "C02_seq_duration": ((3240, 158), (3780, 179), (4320, 165), (4860, 128)),
    # NOT a multiple of 9, which proves the step is drawn PER SLAB and not once
    # per sequence. An earlier model had one step per sequence and was wrong.
    "C04_cut_step_per_slab": ((360, 1367), (420, 1411), (480, 1471), (540, 1421)),
    "D02_rolling_duration": (
        (480, 479), (540, 539), (600, 517), (660, 522), (720, 532), (780, 485),
        (840, 550), (900, 520), (960, 524), (1020, 518), (1080, 484)),
    "F02_pkl_duration": (
        (1200, 143), (1260, 156), (1320, 151), (1380, 142), (1440, 159),
        (1500, 164), (1560, 168), (1620, 146), (1680, 149), (1740, 142),
        (1800, 161), (1860, 141), (1920, 158), (1980, 166), (2040, 155),
        (2100, 147), (2160, 162), (2220, 189), (2280, 156), (2340, 145),
        (2400, 143), (2460, 148), (2520, 174), (2580, 162), (2640, 157),
        (2700, 162), (2760, 162), (2820, 166), (2880, 177), (2940, 143),
        (3000, 195), (3060, 168), (3120, 140), (3180, 142), (3240, 164),
        (3300, 167)),
    "H01_defect_lag": (
        (600, 6), (660, 18), (720, 29), (780, 35), (840, 43), (900, 51),
        (960, 77), (1020, 67), (1080, 102), (1140, 94), (1200, 102),
        (1260, 101), (1320, 113), (1380, 107), (1440, 102), (1500, 109),
        (1560, 112), (1620, 98), (1680, 116), (1740, 82), (1800, 89),
        (1860, 80), (1920, 87), (1980, 51), (2040, 45), (2100, 26),
        (2160, 29), (2220, 13), (2280, 3)),
}

# Section K: every source-update lag is single-valued, so each is EXACT.
UPDATE_LAG_S = {
    "heat": 120, "lf": 120, "sequence": 0, "piece": 120, "coil": 0,
    "pass": 0, "pickle": 0, "qa": 60, "defect": 0, "downtime": 0,
}

# Section K3. The downtime events have NO UPSTREAM PROCESS in this donor - they
# are not derived from a heat, a coil or anything else. Their horizon IS captured
# data rather than a consequence of one.
#
# A first attempt widened the window by the measured order-statistic gap of
# 12,345.6 s so the EXPECTED sample extreme would land on the captured value.
# That corrects the bias but not the variance: the standard deviation of the
# minimum of 210 uniform draws is itself about 12,346 s, so a single sample still
# lands hours from the captured horizon. A local run drifted 10,020 s early.
#
# THIS IS A DECISION, NOT A DERIVATION, AND IT IS FLAGGED AS SUCH. The 210 start
# times are drawn uniformly, sorted, and affine-mapped so the earliest lands on
# the captured minimum and the latest on the captured maximum. The captured
# horizon is reproduced exactly and the 208 interior events stay random. The
# alternative - resampling and accepting an hours-wide horizon drift - reproduces
# the captured window less faithfully, not more.
DT_ANCHOR_HORIZON = True

DT_START = datetime(2026, 4, 1, 12, 11, 0, tzinfo=TZ)   # CAPTURE C downtime min
DT_END = datetime(2026, 5, 1, 8, 55, 0, tzinfo=TZ)      # CAPTURE C downtime max

# ---------------------------------------------------------------- helpers


def weighted_pool(pairs, rnd):
    """Exact counts, shuffled. Not a probability draw - the capture recorded
    counts, and a draw would only approach them."""
    pool = []
    for value, count in pairs:
        pool.extend([value] * count)
    rnd.shuffle(pool)
    return pool


def unif(rnd, lo, hi, places):
    return round(rnd.uniform(lo, hi), places)


def norm(rnd, mean, sd, lo, hi, places):
    for _ in range(64):
        v = rnd.gauss(mean, sd)
        if lo <= v <= hi:
            return round(v, places)
    return round(min(max(rnd.gauss(mean, sd), lo), hi), places)


def pick_step(rnd, steps, jitter, lo, hi, places):
    """Discrete setpoint plus jitter. Section B quantiles show plateaus at
    these values rather than a smooth spread."""
    v = rnd.choice(steps) + rnd.uniform(-jitter, jitter)
    return round(min(max(v, lo), hi), places)



# ================================================================= T-016 TARGET
# Source: docs/m1/evidence/presentation_fleet_v2_target.md, sections 3 and 4.
# These apply in --mode fleet-v2 ONLY. Capture mode is frozen so that retirement
# gate condition 1 stays re-provable at T-031.

# Section 3. Fourteen codes with a role each. Shares sum to 100.0.
# OIL_SPOT and SENSOR_ARTEFACT are NEGATIVE CONTROLS: generated independently of
# every process parameter so a correlation surface can be seen REJECTING them.
FLEET_DEFECTS = (
    # code,              name,               class,     share, role,        severity weights low/med/high
    ("SCALE",            "Scale",            "surface", 26.0, "dominant",         (30, 45, 25)),
    ("EDGE_CRACK",       "Edge crack",       "edge",    15.0, "meaningful",       (10, 30, 60)),
    ("ROLLED_IN_SCALE",  "Rolled-in scale",  "surface", 12.0, "meaningful",       (25, 45, 30)),
    ("SLIVER",           "Sliver",           "surface",  9.0, "meaningful",       (20, 45, 35)),
    ("INCLUSION",        "Inclusion",        "surface",  7.0, "moderate",         (15, 40, 45)),
    ("PINHOLE",          "Pinhole",          "surface",  6.0, "moderate",         (35, 45, 20)),
    ("SCRATCH",          "Scratch",          "surface",  5.0, "rare",             (60, 33, 7)),
    ("WAVINESS",         "Waviness",         "shape",    4.0, "rare",             (40, 45, 15)),
    ("CENTRE_BUCKLE",    "Centre buckle",    "shape",    3.5, "rare",             (25, 45, 30)),
    ("EDGE_WAVE",        "Edge wave",        "shape",    3.0, "rare",             (35, 45, 20)),
    ("ROLL_MARK",        "Roll mark",        "surface",  2.5, "rare",             (30, 50, 20)),
    ("LAMINATION",       "Lamination",       "surface",  2.0, "rare",             (5,  25, 70)),
    ("OIL_SPOT",         "Oil spot",         "surface",  3.0, "negative control", (70, 27, 3)),
    ("SENSOR_ARTEFACT",  "Sensor artefact",  "surface",  2.0, "negative control", (80, 18, 2)),
)

# Section 4. Six elements, each because a chart needs it. C, Mn and Si are
# CAPTURED and are not touched by T-016 - the element SET is what changes.
# S and P carry a maximum only, which is why C12 gets two conditional-format rule
# shapes rather than one. Al is added because IF-LOW-C and DP600 constrain it.
# Bands are grade-aware because T-016's validation asks for a plausible per-grade
# distribution; the SPECIFICATION table that judges them arrives in T-017.
FLEET_CHEMISTRY = {
    #                sulphur         phosphorus        aluminium
    "S235JR":   ((0.008, 0.035), (0.010, 0.032), (0.020, 0.055)),
    "S355MC":   ((0.005, 0.022), (0.008, 0.025), (0.025, 0.060)),
    "DX51D":    ((0.006, 0.028), (0.009, 0.028), (0.030, 0.070)),
    "HSLA-420": ((0.004, 0.018), (0.006, 0.020), (0.028, 0.065)),
    "IF-LOW-C": ((0.003, 0.012), (0.004, 0.014), (0.035, 0.080)),
    "DP600":    ((0.004, 0.016), (0.006, 0.018), (0.030, 0.075)),
}

# fleet-v2 mode adds the three columns rather than editing
# 110_phase1_demo_source_shapes.sql, because that file describes the DONOR
# schemas, which are scheduled for retirement and must not be mutated.
FLEET_ALTERS = (
    "ALTER TABLE src_meltshop_pg.heats ADD COLUMN IF NOT EXISTS sulphur_pct    numeric(7,5);",
    "ALTER TABLE src_meltshop_pg.heats ADD COLUMN IF NOT EXISTS phosphorus_pct numeric(7,5);",
    "ALTER TABLE src_meltshop_pg.heats ADD COLUMN IF NOT EXISTS aluminium_pct  numeric(7,5);",
)


def largest_remainder(shares, total):
    """Turn percentage shares into integer counts that sum EXACTLY to total.
    Rounding each share independently does not sum to the total, and a generator
    that quietly loses or invents a defect row would fail its own row-count gate."""
    raw = [total * sh / 100.0 for sh in shares]
    base = [int(x) for x in raw]
    rem = total - sum(base)
    order = sorted(range(len(raw)), key=lambda i: raw[i] - base[i], reverse=True)
    for i in range(rem):
        base[order[i % len(order)]] += 1
    return base



# ================================================================== T-017 TARGET
# Two additions to the EMULATED CUSTOMER WORLD, never to the product.

# --- the plant timezone, DERIVED not assumed -----------------------------------
# CAPTURE section C shows offsets of +02 in early April and +03 from late April.
# That is Egypt: EET +02 with EEST +03 from the last Friday of April, which in
# 2026 is the 24th. The plant is on Africa/Cairo. Shift derivation must honour the
# switch or every night shift after 24 April is an hour wrong.
PLANT_TZ_NAME = "Africa/Cairo"
DST_START_UTC = datetime(2026, 4, 23, 22, 0, 0, tzinfo=timezone.utc)


def plant_offset_hours(dt):
    return 3 if dt.astimezone(timezone.utc) >= DST_START_UTC else 2


def plant_local(dt):
    """The wall clock a plant operator would read."""
    return dt.astimezone(timezone(timedelta(hours=plant_offset_hours(dt))))


# --- shift as BEHAVIOUR, not a label ------------------------------------------
# v2.6 correction 3: do NOT add a shift column to a source that would not
# realistically record one. Generate the behaviour; expose the field in ONE place;
# derive it everywhere else in a saved transformation from local timestamp plus
# this calendar.
SHIFTS = (
    # code, start hour, end hour, parameter spread multiplier, night bias
    ("A", 6, 14, 0.85, 0.0),    # day, conservative, lower variance
    ("B", 14, 22, 1.00, 0.0),   # evening, the reference
    ("C", 22, 6, 1.25, -1.0),   # night, wider variance and a slightly cooler bias
)
CREWS = ("CREW-1", "CREW-2", "CREW-3", "CREW-4")
ROTATION_DAYS = 7


def shift_of(dt):
    h = plant_local(dt).hour
    for code, a, b, spread, bias in SHIFTS:
        if a < b:
            if a <= h < b:
                return code, spread, bias
        else:
            if h >= a or h < b:
                return code, spread, bias
    return SHIFTS[1][0], SHIFTS[1][3], SHIFTS[1][4]


def crew_of(dt, shift_code):
    """Four crews on a weekly rotation. The crew is a property of the calendar,
    not of the row, which is what makes the calendar worth reading."""
    week = (plant_local(dt) - plant_local(T0_TAP)).days // ROTATION_DAYS
    base = {"A": 0, "B": 1, "C": 2}[shift_code]
    return CREWS[(base + week) % len(CREWS)]


# --- grade specification -------------------------------------------------------
# Six grades x six elements. min_value is NULL for sulphur and phosphorus, which
# carry a MAXIMUM ONLY - that is why C12 gets two conditional-format rule shapes
# rather than one, per target specification section 4.
# Values are ordinary steel-standard bands. Generation aims at target_value with a
# spread that puts about five percent of heats outside their own band, so C12 has
# both states to colour.
GRADE_SPEC = {
    "S235JR":   {"C":  (0.10, 0.15, 0.20), "Mn": (0.40, 0.90, 1.40),
                 "Si": (None, 0.20, 0.30), "P":  (None, 0.018, 0.035),
                 "S":  (None, 0.018, 0.035), "Al": (0.015, 0.035, 0.060)},
    "S355MC":   {"C":  (None, 0.090, 0.120), "Mn": (0.80, 1.20, 1.50),
                 "Si": (None, 0.25, 0.50), "P":  (None, 0.012, 0.025),
                 "S":  (None, 0.010, 0.020), "Al": (0.015, 0.040, 0.070)},
    "DX51D":    {"C":  (None, 0.060, 0.180), "Mn": (None, 0.35, 0.60),
                 "Si": (None, 0.10, 0.50), "P":  (None, 0.015, 0.030),
                 "S":  (None, 0.014, 0.028), "Al": (0.020, 0.045, 0.070)},
    "HSLA-420": {"C":  (None, 0.080, 0.120), "Mn": (1.00, 1.30, 1.60),
                 "Si": (None, 0.30, 0.50), "P":  (None, 0.012, 0.025),
                 "S":  (None, 0.009, 0.018), "Al": (0.020, 0.040, 0.065)},
    "IF-LOW-C": {"C":  (None, 0.0040, 0.0100), "Mn": (0.10, 0.20, 0.35),
                 "Si": (None, 0.020, 0.050), "P":  (None, 0.008, 0.014),
                 "S":  (None, 0.006, 0.012), "Al": (0.035, 0.055, 0.080)},
    "DP600":    {"C":  (0.08, 0.11, 0.15), "Mn": (1.30, 1.60, 2.00),
                 "Si": (None, 0.30, 0.60), "P":  (None, 0.009, 0.018),
                 "S":  (None, 0.005, 0.010), "Al": (0.020, 0.045, 0.070)},
}
SPEC_ELEMENTS = ("C", "Mn", "Si", "P", "S", "Al")
SPEC_COLUMN = {"C": "carbon_pct", "Mn": "manganese_pct", "Si": "silicon_pct",
               "P": "phosphorus_pct", "S": "sulphur_pct", "Al": "aluminium_pct"}
# A small share of heats miss their specification. This is DELIBERATE and
# assigned, not left to the draw: relying on a tail to produce a violation makes
# the acceptance depend on the seed, and a first run had DX51D manganese at zero
# out of 109 heats - about an 8 percent event, so it would pass on some seeds and
# fail on others. A validation that depends on luck is not a validation.
OFF_SPEC_RATE = 0.04


def spec_draw(rnd, band):
    """In-band draw. The spread is deliberately tight - reach/3 - so a conforming
    heat conforms, and every violation in the data is one this generator chose."""
    lo, target, hi = band
    reach = (hi - target) if lo is None else min(hi - target, target - lo)
    sd = reach / 3.0 if reach > 0 else abs(target) * 0.02
    v = rnd.gauss(target, sd)
    if lo is not None:
        v = max(v, lo + reach * 0.02)
    v = min(v, hi - reach * 0.02)
    return max(v, 0.0)


def spec_violate(rnd, band):
    """Push a value just outside its band. Just outside, not absurd: an off-spec
    heat is a process miss, not a different alloy."""
    lo, target, hi = band
    if lo is not None and rnd.random() < 0.4:
        return max(lo * 0.92, 0.0)
    return hi * rnd.uniform(1.03, 1.12)


FLEET_ALTERS_T017 = (
    # The ONE place the field is exposed. A meltshop Level 2 heat record carries
    # the operating crew because the heat sheet is signed off by it; a rolling
    # mill pass measurement does not, so it is derived there instead.
    "ALTER TABLE src_meltshop_pg.heats ADD COLUMN IF NOT EXISTS crew_code text;",
    "CREATE TABLE IF NOT EXISTS src_meltshop_pg.grade_specification ("
    "grade_code text NOT NULL, element_code text NOT NULL, min_value numeric(8,5), "
    "target_value numeric(8,5) NOT NULL, max_value numeric(8,5) NOT NULL, "
    "unit_code text NOT NULL, effective_from date NOT NULL, effective_to date, "
    "PRIMARY KEY (grade_code, element_code));",
    "CREATE TABLE IF NOT EXISTS src_meltshop_pg.shift_calendar ("
    "shift_code text NOT NULL, start_local_time time NOT NULL, "
    "end_local_time time NOT NULL, crew_code text NOT NULL, "
    "effective_from date NOT NULL, effective_to date NOT NULL, "
    "timezone text NOT NULL, PRIMARY KEY (shift_code, effective_from));",
)



# ================================================================== T-018 TARGET
# The second downtime quantity, and the buffer posture behind it.
#
# THE SOURCE CARRIES ONLY duration_seconds. stopped_minutes derives from it.
# production_impact_seconds IS GENERATED INDEPENDENTLY - no code path below
# computes it from the duration, from the timestamps, or from the other quantity.
# Its parameters depend on WHERE in the plant the stoppage happened, which is a
# property of the equipment and not of the event's own length.
#
# THE TWO DERIVED METRICS ARE NOT STORED HERE OR ANYWHERE:
#     buffer_absorbed_minutes      = MAX(stopped - impact, 0)
#     cascade_amplification_minutes = MAX(impact - stopped, 0)
# A plain subtraction was wrong. Stopped 3 with impact 260 would report minus 257
# minutes of buffer absorption, which is not a quantity that exists. The canonical
# model still stores exactly TWO columns.
#
# POSTURE, by where the equipment sits relative to its buffers:
#   ABSORBED   the downstream buffer swallowed it, impact is ZERO
#   CONTAINED  some production was lost, drawn on its own scale
#   CASCADE    a short stoppage forced a sequence rebuild, impact far exceeds it
POSTURE_BY_LINE = {
    # line, (absorbed, contained, cascade) - upstream units have more buffer
    "EAF": (0.55, 0.40, 0.05),
    "LF":  (0.50, 0.42, 0.08),
    "CCM": (0.22, 0.53, 0.25),   # a caster stop aborts the sequence
    "HSM": (0.15, 0.63, 0.22),
    "PKL": (0.38, 0.54, 0.08),   # downstream, coils wait rather than vanish
}
CONTAINED_IMPACT_S = (60, 2400)
CASCADE_IMPACT_S = (3600, 18000)


def draw_impact(rnd, source_line):
    """Independent of duration_seconds by construction: the only inputs are the
    equipment line and the random stream."""
    weights = POSTURE_BY_LINE.get(source_line, (0.30, 0.55, 0.15))
    posture = rnd.choices(("ABSORBED", "CONTAINED", "CASCADE"), weights=weights)[0]
    if posture == "ABSORBED":
        return 0, posture
    if posture == "CONTAINED":
        return rnd.randint(CONTAINED_IMPACT_S[0], CONTAINED_IMPACT_S[1]), posture
    return rnd.randint(CASCADE_IMPACT_S[0], CASCADE_IMPACT_S[1]), posture


FLEET_ALTERS_T018 = (
    "ALTER TABLE src_inspection_mysql_shape.downtime_events "
    "ADD COLUMN IF NOT EXISTS production_impact_seconds integer;",
)



# ================================================================== T-019 TARGET
# Shift and crew operating-practice regimes. ONE phenomenon, eight charts.
#
# THE ANALYSIS UNIT IS THE HEAT'S SHIFT, NOT THE COIL'S. A coil rolls 5 to 34
# hours after its heat, so the rolling shift is all but independent of the tap
# shift and a grade confound built on the rolling shift would evaporate. The
# coherent chain is metallurgical: the crew that MADE the steel sets both the
# grade campaign it was scheduled to run and the meltshop process variance it ran
# with.
#
#   shift ---> grade family ------------------> defect probability   (CONFOUND)
#   shift ---> tap temperature variance ------> defect probability   (RESIDUAL)
#
# Conditioning on grade removes the first path and must NOT remove the second.
# The contract names both failure modes: if the conditioned difference vanishes
# the confounder is too strong and the phenomenon is uninteresting; if it does not
# move the confounder is absent and the story does not exist.

# Harder grades are more defect-prone. This is the grade family the night crew is
# scheduled onto more often, which is the whole confound.
GRADE_HARDNESS = {
    "DP600": 1.00, "HSLA-420": 0.82, "S355MC": 0.52,
    "S235JR": 0.33, "DX51D": 0.28, "IF-LOW-C": 0.12,
}
# Scheduling bias by shift. Positive means this shift draws harder grades. The
# marginal grade counts are UNCHANGED - only which shift runs them moves.
SHIFT_GRADE_BIAS = {"A": -1.15, "B": 0.0, "C": 1.15}
# How strongly grade hardness raises defect risk
HARDNESS_TO_RISK = 1.35
# How strongly the residual process deviation raises defect risk. This is the
# path that must SURVIVE conditioning on grade.
DEVIATION_TO_RISK = 0.95
TAP_TARGET_C = 1650.0
TAP_SD_C = 21.85



def t019_report(data, coils, defect_count):
    """T-019 proof. Naive against grade-conditioned, and the two failure modes the
    contract names explicitly."""
    rows = []
    for idx, coil in enumerate(coils):
        h = coil["heat"]
        rows.append((h.get("shift", "B"), h["grade"], defect_count[idx],
                     h.get("temp_dev", 0.0)))

    def rate(sel):
        return (sum(r[2] for r in sel) / len(sel)) if sel else 0.0

    shifts = ("A", "B", "C")
    naive = dict((sh, rate([r for r in rows if r[0] == sh])) for sh in shifts)
    grades = sorted(GRADE_HARDNESS)
    cond = {}
    for sh in shifts:
        per = [rate([r for r in rows if r[0] == sh and r[1] == g]) for g in grades]
        per = [x for x in per if x > 0]
        cond[sh] = sum(per) / len(per) if per else 0.0

    naive_diff = naive["C"] - naive["A"]
    cond_diff = cond["C"] - cond["A"]
    shrink = (1.0 - cond_diff / naive_diff) if naive_diff else 0.0

    def sd(vals):
        if len(vals) < 2:
            return 0.0
        mu = sum(vals) / len(vals)
        return (sum((v - mu) ** 2 for v in vals) / len(vals)) ** 0.5

    sd_naive = dict((sh, sd([r[3] for r in rows if r[0] == sh])) for sh in shifts)
    sd_cond = {}
    for sh in shifts:
        per = [sd([r[3] for r in rows if r[0] == sh and r[1] == g]) for g in grades]
        per = [x for x in per if x > 0]
        sd_cond[sh] = sum(per) / len(per) if per else 0.0

    hard_share = {}
    for sh in shifts:
        sel = [r for r in rows if r[0] == sh]
        hard = [r for r in sel if GRADE_HARDNESS[r[1]] >= 0.5]
        hard_share[sh] = 100.0 * len(hard) / len(sel) if sel else 0.0

    out = []
    out.append("")
    out.append("T-019 shift and crew operating-practice regimes")
    out.append("  The analysis unit is the HEAT'S shift - the crew that made the")
    out.append("  steel - because a coil rolls 5 to 34 hours later and its rolling")
    out.append("  shift is all but independent of its tap shift.")
    out.append("")
    out.append("  THE CONFOUND, hard-grade share scheduled per shift")
    for sh in shifts:
        out.append("    shift %s  %5.1f percent hard grades" % (sh, hard_share[sh]))
    out.append("")
    out.append("  NAIVE defect rate per coil")
    for sh in shifts:
        out.append("    shift %s  %.4f" % (sh, naive[sh]))
    out.append("    night minus day  %+.4f  (%+.1f percent)"
               % (naive_diff, 100.0 * naive_diff / naive["A"]))
    out.append("")
    out.append("  GRADE-CONDITIONED, stratified with equal grade weights")
    for sh in shifts:
        out.append("    shift %s  %.4f" % (sh, cond[sh]))
    out.append("    night minus day  %+.4f  (%+.1f percent)"
               % (cond_diff, 100.0 * cond_diff / cond["A"]))
    out.append("    SHRINKAGE  %.1f percent of the naive difference was the grade mix"
               % (100.0 * shrink))
    out.append("")
    out.append("  VARIANCE OF TAP DEVIATION - this must SURVIVE conditioning")
    for sh in shifts:
        out.append("    shift %s  naive sd %.3f   within-grade sd %.3f"
                   % (sh, sd_naive[sh], sd_cond[sh]))
    out.append("    night/day ratio  naive %.3f   within-grade %.3f"
               % (sd_naive["C"] / sd_naive["A"], sd_cond["C"] / sd_cond["A"]))

    problems = []
    if naive_diff <= 0 or 100.0 * naive_diff / naive["A"] < 15.0:
        problems.append("the naive shift comparison shows no material difference, "
                        "so there is nothing to condition")
    if shrink < 0.25:
        problems.append("CONFOUNDER ABSENT: conditioning removed only %.1f percent of "
                        "the difference, so the grade-mix story does not exist"
                        % (100.0 * shrink))
    if shrink > 0.80 or cond_diff <= 0 or 100.0 * cond_diff / cond["A"] < 8.0:
        problems.append("CONFOUNDER TOO STRONG: the conditioned difference all but "
                        "vanished, so the phenomenon is uninteresting")
    if sd_cond["C"] / sd_cond["A"] < 1.15:
        problems.append("the variance difference did not survive conditioning, so the "
                        "residual that actually drives defect probability is absent")
    if problems:
        out.append("")
        out.append("T-019 ACCEPTANCE FAILED:")
        for pr in problems:
            out.append("  " + pr)
        return out, False
    out.append("")
    out.append("  Both required outcomes hold: the mean difference shrinks materially")
    out.append("  under conditioning and the variance difference survives it.")
    return out, True


def build_pools(rnd):
    """One exact pool per interval, shuffled once. Popping from it reproduces the
    measured distribution exactly rather than approximately."""
    pools = {}
    for name, pairs in INTERVAL_POOLS.items():
        vals = []
        for value, count in pairs:
            vals.extend([value] * count)
        rnd.shuffle(vals)
        pools[name] = vals
    return pools


def take(pools, name, i):
    """Draw the i-th value from an exact pool. Wraps if a caller needs more than
    the captured observation count, which would itself be a defect worth seeing."""
    p = pools[name]
    return p[i % len(p)]


def minutes(rnd, bounds):
    """A whole-minute uniform draw. STRUCTURE A shows every interval's distinct
    count equals its range divided by 60 plus one, so the underlying draw is over
    minutes and not over seconds."""
    lo, hi = bounds
    return 60 * rnd.randint(lo // 60, hi // 60)


def q(s):
    if s is None:
        return "NULL"
    return "'" + str(s).replace("'", "''") + "'"


def ts(dt):
    return "'" + dt.astimezone(TZ).strftime("%Y-%m-%d %H:%M:%S%z") + "'"


def ts_plant(dt):
    """fleet-v2 emits the plant's own offset, so it reads +02 before the DST
    switch and +03 after, exactly as the captured donor does. The instant is
    unchanged; only the displayed offset moves."""
    return "'" + plant_local(dt).strftime("%Y-%m-%d %H:%M:%S%z") + "'"


class Writer(object):
    def __init__(self, handle, batch=500):
        self.h = handle
        self.batch = batch

    def table(self, name, columns, rows):
        if not rows:
            return
        cols = "(" + ", ".join(columns) + ")"
        for i in range(0, len(rows), self.batch):
            chunk = rows[i:i + self.batch]
            self.h.write("INSERT INTO " + name + " " + cols + " VALUES\n")
            self.h.write(",\n".join("  (" + ", ".join(r) + ")" for r in chunk))
            self.h.write(";\n")
        self.h.write("\n")


# ---------------------------------------------------------------- generation


def generate(seed, mode="capture"):
    rnd = random.Random(seed)
    pools = build_pools(rnd)
    data = {}

    grade_seen = {}
    off_spec_seq = [0]
    grade_pool = weighted_pool(GRADES, rnd)
    if mode == "fleet-v2":
        # T-019: reassign the SAME grade pool so that harder grades land more
        # often on the night crew. Counts per grade are untouched; only which
        # shift runs them moves, so nothing downstream of the grade marginal
        # changes and the confound is purely a scheduling one.
        remaining = {}
        for gname, gcount in GRADES:
            remaining[gname] = gcount
        assigned = []
        for i in range(N_HEATS):
            t = T0_TAP + timedelta(seconds=i * HEAT_INTERVAL_S)
            sc = shift_of(t)[0]
            bias = SHIFT_GRADE_BIAS[sc]
            choices, weights = [], []
            for gname in remaining:
                if remaining[gname] <= 0:
                    continue
                choices.append(gname)
                weights.append(remaining[gname]
                               * math.exp(bias * GRADE_HARDNESS[gname]))
            pick = rnd.choices(choices, weights=weights)[0]
            remaining[pick] -= 1
            assigned.append(pick)
        grade_pool = assigned
    tundish_pool = weighted_pool(TUNDISH, rnd)
    furnace_pool = weighted_pool(FURNACE, rnd)
    lfcode_pool = weighted_pool(LF_CODE, rnd)
    lfsample_pool = weighted_pool(LF_SAMPLE, rnd)
    caster_pool = weighted_pool(CASTER_SEQ, rnd)

    # ---------------------------------------------------------- heats
    heats = []
    heat_rows = []
    for i in range(N_HEATS):
        heat_no = "H2026%05d" % (i + 1)
        tap_start = T0_TAP + timedelta(seconds=i * HEAT_INTERVAL_S)   # FAULT-4
        tap_end = tap_start + timedelta(seconds=take(pools, "A02_tap_duration", i))
        grade = grade_pool[i]
        rec = {
            "heat_no": heat_no,
            "tap_start": tap_start,
            "tap_end": tap_end,
            "grade": grade,
            "furnace": furnace_pool[i],
            # FAULT-1: weight is independent of every downstream dimension
            "weight_ton": unif(rnd, 145.016, 174.937, 3),
        }
        heats.append(rec)
        heat_rows.append([
            q(heat_no), q(PLANT_CODE), q(rec["furnace"]),
            ts(tap_start), ts(tap_end), q(grade), q(ROUTE_CODE),
            "%.3f" % rec["weight_ton"],
            "%.2f" % TARGET_TEMP_C,
            "%.2f" % norm(rnd, 1647.7157, 21.8501, 1577.38, 1711.43, 2),
            "%.3f" % unif(rnd, 3200.815, 5095.138, 3),
            "%.3f" % unif(rnd, 69034.392, 84984.125, 3),
            "%.5f" % unif(rnd, 0.02503, 0.17928, 5),
            "%.5f" % unif(rnd, 0.25147, 1.59943, 5),
            "%.5f" % unif(rnd, 0.01069, 0.31996, 5),
            ts(tap_end + timedelta(seconds=UPDATE_LAG_S["heat"])),
        ])
        if mode == "fleet-v2":
            # T-017: chemistry is produced TO GRADE. Leaving it ungrouped would
            # put most heats outside most bands, and a plant that produces mostly
            # non-conforming steel is not a plant. This is the distribution the
            # SPECIFICATION implies, not a hidden relationship invented beyond it.
            spec = GRADE_SPEC[grade]
            vals = dict((e, spec_draw(rnd, spec[e])) for e in SPEC_ELEMENTS)
            # every OFF_SPEC_RATE-th heat of a grade misses on ONE element, and
            # the element cycles so every one of the six is violated somewhere in
            # the plant rather than only the widest-banded ones
            seen = grade_seen.get(grade, 0)
            grade_seen[grade] = seen + 1
            step = max(int(round(1.0 / OFF_SPEC_RATE)), 1)
            if seen % step == step - 1:
                # the cycle is GLOBAL, not per grade. A per-grade cycle only ever
                # reached the first four elements, because four off-spec heats per
                # grade cannot walk a six-element list - the acceptance check
                # caught sulphur and aluminium never being violated at all.
                el = SPEC_ELEMENTS[off_spec_seq[0] % len(SPEC_ELEMENTS)]
                off_spec_seq[0] += 1
                vals[el] = spec_violate(rnd, spec[el])
            shift_code, spread, bias = shift_of(tap_start)
            heat_rows[-1][9] = "%.2f" % norm(rnd, 1647.7157 + bias, 21.8501 * spread,
                                             1577.38, 1711.43, 2)
            heat_rows[-1][12] = "%.5f" % vals["C"]
            heat_rows[-1][13] = "%.5f" % vals["Mn"]
            heat_rows[-1][14] = "%.5f" % vals["Si"]
            heat_rows[-1].extend([
                "%.5f" % vals["S"],
                "%.5f" % vals["P"],
                "%.5f" % vals["Al"],
                q(crew_of(tap_start, shift_code)),
            ])
            rec["shift"] = shift_code
            rec["spread"] = spread
            rec["bias"] = bias
            rec["hardness"] = GRADE_HARDNESS[grade]
            rec["temp_dev"] = abs(float(heat_rows[-1][9]) - TAP_TARGET_C)
    data["src_meltshop_pg.heats"] = (
        ["heat_no", "plant_code", "furnace_code", "tap_start_utc", "tap_end_utc",
         "steel_grade", "route_code", "heat_weight_ton", "target_temp_c",
         "actual_temp_c", "oxygen_nm3", "power_kwh", "carbon_pct",
         "manganese_pct", "silicon_pct", "source_updated_at_utc"]
        + (["sulphur_pct", "phosphorus_pct", "aluminium_pct", "crew_code"]
           if mode == "fleet-v2" else []),
        heat_rows)

    # ---------------------------------------------------------- lf_treatment
    lf_rows = []
    for i, h in enumerate(heats):
        st = h["tap_start"] + timedelta(seconds=take(pools, "B01_lf_start_offset", i))
        en = st + timedelta(seconds=take(pools, "B02_lf_duration", i))
        lf_rows.append([
            str(i + 1), q(h["heat_no"]), q(lfcode_pool[i]), ts(st), ts(en),
            "%.3f" % unif(rnd, 120.003, 279.898, 3),
            "%.3f" % unif(rnd, 20.198, 109.868, 3),
            "%.2f" % norm(rnd, 1609.8018, 16.4367, 1562.20, 1665.47, 2),
            q(lfsample_pool[i]),
            ts(en + timedelta(seconds=UPDATE_LAG_S["lf"])),
        ])
    data["src_meltshop_pg.lf_treatment"] = (
        ["treatment_id", "heat_no", "lf_code", "treatment_start_utc",
         "treatment_end_utc", "argon_flow_nm3", "calcium_wire_m", "final_temp_c",
         "sample_result_code", "source_updated_at_utc"], lf_rows)

    # ---------------------------------------------------------- cast_sequence
    seq_rows = []
    sequences = []
    for i, h in enumerate(heats):
        seq_no = "SEQ%05d" % (i + 1)
        st = h["tap_start"] + timedelta(seconds=take(pools, "C01_seq_start_offset", i))
        en = st + timedelta(seconds=take(pools, "C02_seq_duration", i))
        caster = caster_pool[i]
        sequences.append({"seq_no": seq_no, "start": st, "caster": caster})
        seq_rows.append([
            q(seq_no), q(caster), ts(st), ts(en),
            q(tundish_pool[i]),
            # planned always equals actual - no deviation story exists
            q(h["grade"]), q(h["grade"]),
            q(SEQUENCE_STATUS),
            ts(en),
        ])
    # NOTE: cast_sequence carries NO heat_no. The heat-to-sequence link exists
    # only through cast_pieces, which carries both. Section I of the capture is
    # the authority for this; an earlier draft invented the column and the load
    # failed on it.
    data["src_caster_oracle_shape.cast_sequence"] = (
        ["sequence_no", "caster_id", "start_time", "end_time",
         "tundish_no", "planned_grade", "actual_grade", "sequence_status",
         "last_update_ts"], seq_rows)

    # ---------------------------------------------------------- cast_pieces
    piece_rows = []
    pieces = []
    for i, h in enumerate(heats):
        seq = sequences[i]
        for s in range(SLABS_PER_HEAT):
            piece_id = "SLB%05d%02d" % (i + 1, s + 1)
            # the step is drawn PER SLAB - section K counts are not multiples of 9
            step = take(pools, "C04_cut_step_per_slab", i * SLABS_PER_HEAT + s)
            cut = seq["start"] + timedelta(seconds=(s + 1) * step)
            width = pick_step(rnd, [950.0, 1050.0, 1250.0, 1450.0, 1550.0], 8.0, 943.01, 1557.00, 2)
            thick = pick_step(rnd, [220.0, 230.0, 250.0], 2.0, 218.01, 252.00, 2)
            length = unif(rnd, 8501.03, 11799.68, 2)
            # FAULT-1: weight drawn independently of width, thickness and length
            weight = unif(rnd, 18000.274, 30999.427, 3)
            pieces.append({"piece_id": piece_id, "heat": h, "width": width,
                           "thick": thick, "weight": weight, "cut": cut})
            piece_rows.append([
                q(piece_id), q(seq["seq_no"]), q(h["heat_no"]), q(seq["caster"]),
                str(rnd.randint(1, 2)), str(s + 1),
                "%.2f" % width, "%.2f" % thick, "%.2f" % length, "%.3f" % weight,
                ts(cut),
                "%.4f" % norm(rnd, -0.0841, 2.5241, -8.7648, 9.0856, 4),
                "%.4f" % norm(rnd, 1.3497 + (0.0 if mode != "fleet-v2" else h.get("bias", 0.0) * 0.02),
                              0.1221 * (1.0 if mode != "fleet-v2" else h.get("spread", 1.0)),
                              0.9114, 1.8262, 4),
                # superheat goes NEGATIVE at the low tail - physically impossible
                "%.4f" % norm(rnd, 23.9157, 7.8133 * (1.0 if mode != "fleet-v2" else h.get("spread", 1.0)),
                              -3.0036, 51.4916, 4),
                ts(cut + timedelta(seconds=UPDATE_LAG_S["piece"])),
            ])
    data["src_caster_oracle_shape.cast_pieces"] = (
        ["piece_id", "sequence_no", "heat_no", "caster_id", "strand_no", "slab_no",
         "width_mm", "thickness_mm", "length_mm", "weight_kg", "cut_time",
         "mould_level_avg", "casting_speed_avg", "superheat_c", "last_update_ts"],
        piece_rows)

    # ---------------------------------------------------------- hsm_coils
    coil_rows = []
    coils = []
    for i, h in enumerate(heats):
        for c in range(COILS_PER_HEAT):
            idx = i * COILS_PER_HEAT + c
            coil_id = "C%07d" % (idx + 1)
            piece = pieces[idx]
            lo = ROLL_LAG_S[0]
            hi = ROLL_LAG_S[1] - (COILS_PER_HEAT - 1) * ROLL_LAG_POS_DRIFT_S
            lag = 60 * rnd.randint(lo // 60, hi // 60) + c * ROLL_LAG_POS_DRIFT_S
            rs = (h["tap_start"] + timedelta(seconds=lag)).replace(second=0, microsecond=0)
            re_ = rs + timedelta(seconds=take(pools, "D02_rolling_duration", idx))
            tgt_thk = pick_step(rnd, [1.5, 2.0, 2.5, 3.0, 4.0], 0.08, 1.42, 4.0799, 4)
            tgt_wid = piece["width"]
            act_wid = round(min(max(tgt_wid + rnd.gauss(0.012, 1.7343), 940.25), 1559.33), 2)
            # FAULT-1 again: coil weight independent of its slab weight
            coil_weight = unif(rnd, 12508.135, 28498.388, 3)
            coils.append({"coil_id": coil_id, "heat": h, "piece": piece,
                          "rs": rs, "re": re_})
            coil_rows.append([
                q(coil_id), q(MILL_LINE), q(piece["piece_id"]), q(h["heat_no"]),
                ts(rs), ts(re_),
                "%.2f" % TARGET_FDT_C,
                "%.2f" % norm(rnd, 874.9533 + (0.0 if mode != "fleet-v2" else h.get("bias", 0.0) * 3.0),
                              21.8351 * (1.0 if mode != "fleet-v2" else h.get("spread", 1.0)),
                              798.34, 958.13, 2),
                "%.2f" % TARGET_CT_C,
                "%.2f" % norm(rnd, 609.3825, 27.6698, 491.56, 729.02, 2),
                "%.4f" % tgt_thk,
                # FAULT-2: actual thickness IS the target, to the last decimal
                "%.4f" % tgt_thk,
                "%.2f" % tgt_wid, "%.2f" % act_wid,
                "%.3f" % coil_weight,
                ts(re_),
            ])
    data["src_hsm_oracle_shape.hsm_coils"] = (
        ["coil_id", "mill_line", "input_piece_id", "heat_no", "rolling_start_time",
         "rolling_end_time", "target_fdt_c", "actual_fdt_c", "target_ct_c",
         "actual_ct_c", "target_thickness_mm", "actual_thickness_mm",
         "target_width_mm", "actual_width_mm", "coil_weight_kg", "last_update_ts"],
        coil_rows)

    # ---------------------------------------------------------- pass measurements
    pass_rows = []
    mid = 0
    for coil in coils:
        for stand in range(1, PASSES_PER_COIL + 1):
            mid += 1
            # STRUCTURE E: sd 0, one distinct value per stand. Deterministic.
            st = coil["rs"] + timedelta(seconds=stand * PASS_STEP_S)
            pass_rows.append([
                str(mid), q(coil["coil_id"]), str(stand), ts(st),
                # FAULT-3: every stand draws from the SAME distribution, so the
                # mill has no profile down its length
                "%.3f" % unif(rnd, 8200.096, 24498.523, 3),
                "%.5f" % unif(rnd, 0.60016, 5.99983, 5),
                "%.5f" % unif(rnd, 4.00005, 18.49934, 5),
                "%.2f" % norm(rnd, 875.0072, 29.6361, 774.96, 993.03, 2),
                ts(st),
            ])
    data["src_hsm_oracle_shape.hsm_pass_measurements"] = (
        ["measurement_id", "coil_id", "stand_no", "sample_time", "rolling_force_kn",
         "roll_gap_mm", "speed_mps", "temperature_c", "last_update_ts"], pass_rows)

    # ---------------------------------------------------------- pickle orders
    line_pool = weighted_pool(LINE_ID, rnd)
    insp_pool = weighted_pool(INSPECTION_RESULT, rnd)
    dec_pool = []
    # Accepted count equals the OK count exactly, so the two columns are linked
    for value, count in QA_DECISION:
        dec_pool.extend([value] * count)
    not_ok = [d for d in dec_pool if d != "Accepted"]
    rnd.shuffle(not_ok)
    pkl_rows = []
    orders = []
    ni = 0
    for i, coil in enumerate(coils):
        entry = coil["re"] + timedelta(hours=rnd.randint(PKL_LAG_HOURS[0], PKL_LAG_HOURS[1]))
        entry = entry.replace(second=0, microsecond=0)
        exit_ = entry + timedelta(seconds=take(pools, "F02_pkl_duration", i))
        insp = insp_pool[i]
        if insp == "OK":
            decision = "Accepted"
        else:
            decision = not_ok[ni]
            ni += 1
        orders.append({"coil": coil, "exit": exit_})
        pkl_rows.append([
            q("PKL-" + coil["coil_id"]), q(coil["coil_id"]), q(line_pool[i]),
            ts(entry), ts(exit_),
            "%.4f" % unif(rnd, 5.5009, 11.5000, 4),
            "%.2f" % unif(rnd, 72.01, 91.99, 2),
            "%.3f" % unif(rnd, 80.021, 239.998, 3),
            q(insp), q(decision),
            q("CUST-%03d" % rnd.randint(100, 998)),
            ts(exit_),
        ])
    data["src_pkl_mssql_shape.pickle_orders"] = (
        ["order_id", "coil_id", "line_id", "entry_time_utc", "exit_time_utc",
         "acid_concentration_pct", "bath_temperature_c", "line_speed_mpm",
         "inspection_result", "qa_decision", "customer_code", "modified_at_utc"],
        pkl_rows)

    # ---------------------------------------------------------- qa lab results
    status_pool = weighted_pool(QA_STATUS, rnd)
    qa_rows = []
    lid = 0
    tests = [("WIDTH", "mm"), ("THK", "mm"), ("ROUGHNESS", "um")]
    for order in orders:
        st = order["exit"] + timedelta(seconds=QA_AFTER_EXIT_S)
        for code, unit in tests:
            lid += 1
            qa_rows.append([
                str(lid), q(order["coil"]["coil_id"]), q(code),
                # FAULT-6: one distribution for all three tests, so THK reaches
                # 1,599 mm and ROUGHNESS reaches 1,599 um
                "%.6f" % unif(rnd, 1.067509, 1599.881510, 6),
                q(unit), q(status_pool[lid - 1]), ts(st),
                ts(st + timedelta(seconds=60)),
            ])
    data["src_pkl_mssql_shape.qa_lab_results"] = (
        ["lab_result_id", "coil_id", "test_code", "measured_value", "unit_code",
         "result_status", "sample_time_utc", "modified_at_utc"], qa_rows)

    # ---------------------------------------------------------- surface defects
    ladder = []
    for count, coil_n in DEFECT_LADDER:
        ladder.extend([count] * coil_n)
    if mode != "fleet-v2":
        rnd.shuffle(ladder)
    else:
        # T-019: the ladder VALUES are unchanged - the same 4,670 clean coils and
        # the same 326 / 361 / 313 - but WHICH coil gets which is now a race
        # weighted by the two causal paths. An exponential race preserves the
        # ladder exactly while making defect count monotone in risk.
        keys = []
        for idx, coil in enumerate(coils):
            h = coil["heat"]
            risk = math.exp(HARDNESS_TO_RISK * h.get("hardness", 0.4)
                            + DEVIATION_TO_RISK * h.get("temp_dev", 0.0) / TAP_SD_C)
            keys.append((rnd.expovariate(1.0) / risk, idx))
        keys.sort()
        ladder_sorted = sorted(ladder, reverse=True)
        ladder = [0] * len(coils)
        for rank, (_k, idx) in enumerate(keys):
            ladder[idx] = ladder_sorted[rank]
        data["_t019"] = (coils, list(ladder))
    if mode == "fleet-v2":
        counts = largest_remainder([d[3] for d in FLEET_DEFECTS], N_DEFECTS)
        code_pool = weighted_pool(
            [(FLEET_DEFECTS[i][0], counts[i]) for i in range(len(FLEET_DEFECTS))], rnd)
        meta = dict((d[0], (d[1], d[2])) for d in FLEET_DEFECTS)
        # severity is CONDITIONED ON CODE. Captured severity is uniform across all
        # eighteen code-and-severity combinations, which is target-spec section 3's
        # second finding and not only a Pareto problem.
        sev_weights = dict((d[0], d[5]) for d in FLEET_DEFECTS)
        sev_pool = None
    else:
        code_pool = weighted_pool([(d[0], d[3]) for d in DEFECTS], rnd)
        sev_pool = weighted_pool(SEVERITY, rnd)
        meta = dict((d[0], (d[1], d[2])) for d in DEFECTS)
        sev_weights = None
    side_pool = weighted_pool(SIDE_CODE, rnd)
    def_rows = []
    di = 0
    for i, coil in enumerate(coils):
        for _ in range(ladder[i]):
            if di >= N_DEFECTS:
                break
            code = code_pool[di]
            name, klass = meta[code]
            if sev_weights is not None:
                lo, md, hi = sev_weights[code]
                severity = rnd.choices(("low", "medium", "high"),
                                       weights=(lo, md, hi))[0]
            else:
                severity = sev_pool[di]
            et = coil["rs"] + timedelta(seconds=take(pools, "H01_defect_lag", di))
            et = et.replace(second=0, microsecond=0)
            start_m = unif(rnd, 0.220, 799.798, 3)
            def_rows.append([
                str(di + 1), q(coil["coil_id"]), q(INSPECTION_DEVICE), ts(et),
                q(code), q(name), q(klass), q(severity),
                "%.3f" % start_m,
                "%.3f" % round(min(start_m + rnd.uniform(0.5, 25.0), 817.308), 3),
                "%.3f" % unif(rnd, 51.581, 1449.613, 3),
                q(side_pool[di]),
                "%.4f" % unif(rnd, 70.0045, 98.9827, 4),
                ts(et),
            ])
            di += 1
    data["src_inspection_mysql_shape.parsytec_surface_defects"] = (
        ["defect_row_id", "coil_id", "inspection_device", "event_time_utc",
         "defect_code", "defect_name", "defect_class", "defect_severity",
         "position_start_m", "position_end_m", "width_position_mm", "side_code",
         "confidence_pct", "updated_at_utc"], def_rows)

    # ---------------------------------------------------------- downtime
    reason_pool = weighted_pool([(r[0], r[3]) for r in DOWNTIME_REASONS], rnd)
    equip_pool = weighted_pool(DOWNTIME_EQUIPMENT, rnd)
    rmeta = dict((r[0], (r[1], r[2])) for r in DOWNTIME_REASONS)
    dt_posture = []
    window = int((DT_END - DT_START).total_seconds())
    if DT_ANCHOR_HORIZON:
        raw = sorted(rnd.random() for _ in range(N_DOWNTIME))
        lo, hi = raw[0], raw[-1]
        span = (hi - lo) if hi > lo else 1.0
        dt_starts = [DT_START + timedelta(seconds=60 * int(round(
            ((x - lo) / span) * window / 60.0))) for x in raw]
        rnd.shuffle(dt_starts)
    else:
        dt_starts = None
    dt_rows = []
    for i in range(N_DOWNTIME):
        code = reason_pool[i]
        text, category = rmeta[code]
        equip = equip_pool[i]
        # FAULT-4: the window is independent of the heat schedule, so a downtime
        # event never delays a heat
        if dt_starts is not None:
            st = dt_starts[i]
        else:
            st = DT_START + timedelta(seconds=rnd.randint(0, window))
        st = st.replace(second=0, microsecond=0)
        dur = rnd.randint(196, 5374)
        en = st + timedelta(seconds=dur)
        row = [
            str(i + 1), q(equip), q(equip.split("-")[0]), ts(st), ts(en),
            str(dur), q(code), q(text), q(category), ts(en),
        ]
        if mode == "fleet-v2":
            impact, posture = draw_impact(rnd, equip.split("-")[0])
            row.append(str(impact))
            dt_posture.append((dur, impact, posture))
        dt_rows.append(row)
    if mode == "fleet-v2":
        # T-017 acceptance, asserted here rather than hoped for downstream
        cols_h, rows_h = data["src_meltshop_pg.heats"]
        gi = cols_h.index("steel_grade")
        viol_by_grade = {}
        viol_by_element = {}
        for r in rows_h:
            gr = r[gi].strip("'")
            viol_by_grade.setdefault(gr, [0, 0])
            out = False
            for el in SPEC_ELEMENTS:
                lo, tgt, hi = GRADE_SPEC[gr][el]
                v = float(r[cols_h.index(SPEC_COLUMN[el])])
                if v > hi or (lo is not None and v < lo):
                    out = True
                    viol_by_element[el] = viol_by_element.get(el, 0) + 1
            viol_by_grade[gr][0 if out else 1] += 1
        problems = []
        for gr, (out_n, in_n) in sorted(viol_by_grade.items()):
            if out_n == 0:
                problems.append("%s has no heat outside its band" % gr)
            if in_n == 0:
                problems.append("%s has no heat inside its band" % gr)
        for el in SPEC_ELEMENTS:
            if viol_by_element.get(el, 0) == 0:
                problems.append("element %s is never violated anywhere" % el)
        if problems:
            raise SystemExit("T-017 ACCEPTANCE FAILED:\n  " + "\n  ".join(problems))

        spec_rows = []
        for grade in sorted(GRADE_SPEC):
            for el in SPEC_ELEMENTS:
                lo, tgt, hi = GRADE_SPEC[grade][el]
                spec_rows.append([
                    q(grade), q(el),
                    "NULL" if lo is None else "%.5f" % lo,
                    "%.5f" % tgt, "%.5f" % hi, q("pct"),
                    q("2026-01-01"), "NULL",
                ])
        data["src_meltshop_pg.grade_specification"] = (
            ["grade_code", "element_code", "min_value", "target_value",
             "max_value", "unit_code", "effective_from", "effective_to"], spec_rows)

        cal_rows = []
        first = plant_local(T0_TAP).date()
        last = plant_local(heats[-1]["tap_start"]).date()
        weeks = ((last - first).days // ROTATION_DAYS) + 1
        for w in range(weeks):
            eff_from = first + timedelta(days=w * ROTATION_DAYS)
            eff_to = eff_from + timedelta(days=ROTATION_DAYS - 1)
            for idx, (code, a, b, _sp, _bi) in enumerate(SHIFTS):
                cal_rows.append([
                    q(code), q("%02d:00:00" % a), q("%02d:00:00" % b),
                    q(CREWS[(idx + w) % len(CREWS)]),
                    q(eff_from.isoformat()), q(eff_to.isoformat()),
                    q(PLANT_TZ_NAME),
                ])
        data["src_meltshop_pg.shift_calendar"] = (
            ["shift_code", "start_local_time", "end_local_time", "crew_code",
             "effective_from", "effective_to", "timezone"], cal_rows)

    data["src_inspection_mysql_shape.downtime_events"] = (
        ["downtime_id", "equipment_code", "source_line", "start_time_utc",
         "end_time_utc", "duration_seconds", "reason_code", "reason_text",
         "downtime_category", "updated_at_utc"]
        + (["production_impact_seconds"] if mode == "fleet-v2" else []), dt_rows)

    if mode == "fleet-v2":
        # T-018 acceptance, asserted rather than hoped for. The two shapes must be
        # present IN MEANINGFUL NUMBERS, not as single planted rows.
        absorbed = [(d, im) for d, im, p in dt_posture if im == 0 and d > 0]
        cascade = [(d, im) for d, im, p in dt_posture if im > 2 * d]
        troubles = []
        if len(absorbed) < 10:
            troubles.append("only %d absorbed events (stopped above zero, impact zero); "
                            "a single planted row is not a distribution" % len(absorbed))
        if len(cascade) < 10:
            troubles.append("only %d cascade events with impact above twice stopped"
                            % len(cascade))
        for d, im, p in dt_posture:
            if im < 0:
                troubles.append("negative production impact emitted")
                break
        if troubles:
            raise SystemExit("T-018 ACCEPTANCE FAILED:\n  " + "\n  ".join(troubles))
        data["_t018_posture"] = dt_posture

    return data


ORDER_T017 = ["src_meltshop_pg.grade_specification", "src_meltshop_pg.shift_calendar"]

ORDER = [
    "src_meltshop_pg.heats",
    "src_meltshop_pg.lf_treatment",
    "src_caster_oracle_shape.cast_sequence",
    "src_caster_oracle_shape.cast_pieces",
    "src_hsm_oracle_shape.hsm_coils",
    "src_hsm_oracle_shape.hsm_pass_measurements",
    "src_pkl_mssql_shape.pickle_orders",
    "src_pkl_mssql_shape.qa_lab_results",
    "src_inspection_mysql_shape.parsytec_surface_defects",
    "src_inspection_mysql_shape.downtime_events",
]

EXPECTED = {
    "src_meltshop_pg.heats": 630,
    "src_meltshop_pg.lf_treatment": 630,
    "src_caster_oracle_shape.cast_sequence": 630,
    "src_caster_oracle_shape.cast_pieces": 5670,
    "src_hsm_oracle_shape.hsm_coils": 5670,
    "src_hsm_oracle_shape.hsm_pass_measurements": 39690,
    "src_pkl_mssql_shape.pickle_orders": 5670,
    "src_pkl_mssql_shape.qa_lab_results": 17010,
    "src_inspection_mysql_shape.parsytec_surface_defects": 1987,
    "src_inspection_mysql_shape.downtime_events": 210,
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="")
    ap.add_argument("--seed", type=int, default=SEED_DEFAULT)
    ap.add_argument("--profile", action="store_true")
    ap.add_argument("--mode", choices=("capture", "fleet-v2"), default="capture",
                    help="capture reproduces the T-014 baseline and is FROZEN so "
                         "retirement gate condition 1 stays re-provable at T-031; "
                         "fleet-v2 carries the target specification changes")
    ap.add_argument("--columns", action="store_true",
                    help="print the column manifest and exit, for the runner to check "
                         "against information_schema BEFORE loading anything")
    args = ap.parse_args()

    data = generate(args.seed, args.mode)

    if args.columns:
        for name in ORDER:
            print(name + "|" + ",".join(data[name][0]))
        return 0

    failures = []
    for name in ORDER:
        cols, rows = data[name]
        for n, r in enumerate(rows):
            if len(r) != len(cols):
                failures.append("%s row %d has %d values for %d columns"
                                % (name, n + 1, len(r), len(cols)))
                break
    for name in ORDER:
        actual = len(data[name][1])
        if actual != EXPECTED[name]:
            failures.append("%s produced %d rows, captured %d"
                            % (name, actual, EXPECTED[name]))
    if failures:
        sys.stderr.write("REFUSING TO EMIT - row counts do not match the capture:\n")
        for f in failures:
            sys.stderr.write("  [FAIL] " + f + "\n")
        return 2

    print("mode: " + args.mode)
    posture = data.pop("_t018_posture", None)
    t019 = data.pop("_t019", None)
    print("row counts, generated against captured")
    for name in ORDER:
        print("  [OK] %-52s %6d" % (name, len(data[name][1])))

    if posture is not None:
        import collections as _c
        counts = _c.Counter(p for _d, _i, p in posture)
        print("")
        print("T-018 downtime, the two quantities. production_impact_seconds is")
        print("drawn from the equipment line and the random stream ONLY - never")
        print("from duration_seconds, the timestamps, or the other quantity.")
        print("  posture           " + "  ".join(
            "%s %d" % (k, counts.get(k, 0)) for k in ("ABSORBED", "CONTAINED", "CASCADE")))
        absorbed = [(d, i) for d, i, p in posture if i == 0 and d > 0]
        cascade = sorted(((d, i) for d, i, p in posture if i > 2 * d),
                         key=lambda x: x[1] - x[0], reverse=True)
        print("  buffer absorbed   %d events, stopped above zero with impact zero"
              % len(absorbed))
        print("  cascade           %d events with impact above twice stopped"
              % len(cascade))
        if absorbed:
            d, i = max(absorbed)
            print("    example absorbed  stopped %5.1f min  impact %5.1f min  "
                  "-> absorbed %.1f, cascade %.1f"
                  % (d / 60.0, i / 60.0, max(d - i, 0) / 60.0, max(i - d, 0) / 60.0))
        if cascade:
            d, i = cascade[0]
            print("    example cascade   stopped %5.1f min  impact %5.1f min  "
                  "-> absorbed %.1f, cascade %.1f"
                  % (d / 60.0, i / 60.0, max(d - i, 0) / 60.0, max(i - d, 0) / 60.0))
        print("  NEITHER derived metric is stored. Canonical keeps exactly two columns.")

    if t019 is not None:
        lines, ok = t019_report(data, t019[0], t019[1])
        for ln in lines:
            print(ln)
        if not ok:
            return 4

    if args.profile or not args.out:
        print("\nno --out given, nothing written")
        return 0

    with open(args.out, "w", encoding="utf-8", newline="\n") as fh:
        # CAPTURE MODE EMITS ITS ORIGINAL HEADER VERBATIM. The capture output is the
        # permanent regression test for retirement gate condition 1, so its SHA256
        # must stay a stable fingerprint - 11EDF4B275A106C86D75EA3147D47B56F7763AD9
        # EE2D258487953B7155939AD7 for seed 20260803. Changing this comment line
        # once already moved the hash while leaving every data row identical.
        if args.mode == "capture":
            fh.write("-- PPIQ Fleet v2 donor capture, seed %d\n" % args.seed)
        else:
            fh.write("-- PPIQ donor data, mode %s, seed %d\n" % (args.mode, args.seed))
        fh.write("-- Generated by Backend/tools/generate_fleet_v2_donor.py\n")
        fh.write("-- Reproduces the captured donor state INCLUDING its six measured\n")
        fh.write("-- faults. T-014 captures; T-015 onward corrects.\n")
        fh.write("BEGIN;\n\n")
        if args.mode == "fleet-v2":
            fh.write("-- T-016 adds the chemistry columns the target specification\n")
            fh.write("-- names. 110_phase1_demo_source_shapes.sql is NOT edited: it\n")
            fh.write("-- describes the donor schemas, which are scheduled for retirement.\n")
            for a in FLEET_ALTERS + FLEET_ALTERS_T017 + FLEET_ALTERS_T018:
                fh.write(a + "\n")
            fh.write("\n")
        for name in ORDER + ([n for n in ORDER_T017 if n in data]):
            fh.write("DELETE FROM " + name + ";\n")
        fh.write("\n")
        w = Writer(fh)
        for name in ORDER + ([n for n in ORDER_T017 if n in data]):
            cols, rows = data[name]
            fh.write("-- " + name + " : " + str(len(rows)) + " rows\n")
            w.table(name, cols, rows)
        fh.write("COMMIT;\n")

    print("\nwritten: %s" % args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
