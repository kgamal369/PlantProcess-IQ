& {
# ============================================================================
#  PlantProcess IQ - T-039 : write + run the deterministic demo dataset generator
#
#  This applier writes Backend\tools\generate_demo_dataset.py and runs it,
#  emitting 002_data.sql into each Infrastructure\demo-sources\*\init plus the
#  two CSVs. Then recreate the demo-source containers to load the data.
# ============================================================================
    $ErrorActionPreference = 'Stop'

    $Root = 'C:\Workspace\PlantProcess-IQ'
    if (-not (Test-Path (Join-Path $Root 'Jenkinsfile'))) {
        $cwd = (Get-Location).Path
        while ($cwd -and -not (Test-Path (Join-Path $cwd 'Jenkinsfile'))) { $cwd = Split-Path $cwd -Parent }
        if ($cwd) { $Root = $cwd }
    }
    if (-not (Test-Path (Join-Path $Root 'Jenkinsfile'))) { throw "Repo root not found (run from inside the repo)." }

    $enc = New-Object System.Text.UTF8Encoding($false)

    $py = @'
#!/usr/bin/env python3
# ============================================================================
#  PlantProcess IQ - T-039  Deterministic demo dataset generator
#
#  Emits the Vision-scale flat-steel dataset across all EIGHT demo source
#  schemas, reproducibly from a fixed seed. The genealogy spine is consistent
#  across sources:
#
#     meltshop_heats (H-####)
#         -> caster_sequences (SEQ-######)  [1 per heat]
#             -> caster_slabs (SLAB-######)  [~9 per heat]
#                 -> hsm_coils (C-#######)   [1 per slab]
#                     -> pkl_coils           [1 per coil]
#                     -> parsytec_surface_defects [0..n per coil]
#                     -> qa_samples (every 3rd coil)
#                     -> yard_inventory (every coil)
#     downtime_events (per equipment)
#
#  The headline coil C-0044170 is generated with a clean chain back to its
#  heat and exactly five EDGE_CRACK surface defects (T-042 golden thread).
#
#  Output (one file per source; existing 001_schema_seed.sql is left intact):
#     <out>/meltshop-postgres/init/002_data.sql      (PostgreSQL)
#     <out>/caster-oracle/init/002_data.sql          (Oracle)
#     <out>/hsm-oracle/init/002_data.sql             (Oracle)
#     <out>/pkl-mssql/init/002_data.sql              (SQL Server)
#     <out>/downtime-mysql/init/002_data.sql         (MySQL)
#     <out>/parsytec-mysql/init/002_data.sql         (MySQL)
#     <out>/excel-qa/qa_samples.csv                  (CSV)
#     <out>/excel-yard/yard_inventory.csv            (CSV)
#
#  Deterministic: a fixed seed yields byte-identical output. No external deps.
# ============================================================================
from __future__ import annotations

import argparse
import csv
import io
import random
from datetime import datetime, timedelta, timezone
from pathlib import Path

# --- Vision targets (defaults; overridable via CLI) -------------------------
DEFAULT_SEED = 20260601
DEFAULT_HEATS = 630          # ~630 heats / month
COIL_NUMBER_BASE = 40000     # coil C-00NNNNN numbering base; 44170 falls in range
SLABS_PER_HEAT = (7, 11)     # -> ~5,600 coils from 630 heats
TONNES_PER_HEAT = 160.0
LADLE_COUNT = 11             # 11 ladles
LADLE_FURNACES = 4           # 4 LF
STRANDS = 2
GOLDEN_COIL_NUMBER = 44170   # C-0044170
GOLDEN_EDGE_CRACKS = 5

STEEL_GRADES = ["S235JR", "S355MC", "DX51D", "HX340LAD", "DD11", "API5L-X52"]
PRODUCT_CODES = ["HRC-2.0x1250", "HRC-3.0x1500", "HRC-4.0x1250", "HRC-2.5x1000"]
DEFECT_CODES = ["EDGE_CRACK", "SCALE", "INCLUSION", "ROLLED_IN_SCALE", "SLIVER"]
DEFECT_SEVERITY = ["LOW", "MEDIUM", "HIGH"]
QA_LABELS = ["PASS", "PASS", "PASS", "HOLD", "FAIL"]
DISPOSITIONS = ["RELEASE", "RELEASE", "REWORK", "SCRAP", "DOWNGRADE"]
EQUIPMENT = ["CASTER-1", "CASTER-2", "HSM-FM", "HSM-RM", "PKL-1", "LF-1", "LF-2"]
DOWNTIME_REASONS = [
    ("ROLL_CHANGE", "Scheduled roll change"),
    ("BREAKDOWN", "Mechanical breakdown"),
    ("PLANNED_MAINT", "Planned maintenance window"),
    ("PROCESS_HOLD", "Process hold for quality check"),
    ("NOZZLE_CLOG", "Submerged entry nozzle clogging"),
]

BASE_TIME = datetime(2026, 5, 1, 0, 0, 0, tzinfo=timezone.utc)


# --- value formatting helpers ----------------------------------------------
def q(s) -> str:
    """SQL string literal with single-quote escaping."""
    return "'" + str(s).replace("'", "''") + "'"


def num(x, scale: int) -> str:
    return f"{round(float(x), scale):.{scale}f}"


def pg_ts(dt: datetime) -> str:
    return "'" + dt.strftime("%Y-%m-%d %H:%M:%S") + "+00'"


def plain_ts(dt: datetime) -> str:
    return "'" + dt.strftime("%Y-%m-%d %H:%M:%S") + "'"


def ora_ts(dt: datetime) -> str:
    return "TIMESTAMP '" + dt.strftime("%Y-%m-%d %H:%M:%S") + "'"


def iso_z(dt: datetime) -> str:
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")


def coil_id(n: int) -> str:
    return "C-" + str(n).zfill(7)


# --- SQL emission -----------------------------------------------------------
def emit_inserts(buf: io.StringIO, table: str, columns: list[str],
                 rows: list[list[str]], dialect: str) -> None:
    """Write INSERTs. Oracle: one row per statement (no multi-row VALUES).
    Others: batched multi-row VALUES."""
    if not rows:
        return
    collist = "(" + ", ".join(columns) + ")"
    if dialect == "oracle":
        for r in rows:
            buf.write(f"INSERT INTO {table} {collist} VALUES (" + ", ".join(r) + ");\n")
    else:
        chunk = 200
        for i in range(0, len(rows), chunk):
            part = rows[i:i + chunk]
            buf.write(f"INSERT INTO {table} {collist} VALUES\n")
            buf.write(",\n".join("(" + ", ".join(r) + ")" for r in part))
            buf.write(";\n")


def header(dialect: str, n_rows: int) -> str:
    return (f"-- PlantProcess IQ T-039 generated demo data ({dialect}). "
            f"{n_rows} rows. DO NOT EDIT BY HAND; regenerate via generate_demo_dataset.py.\n\n")


# --- generation -------------------------------------------------------------
def generate(seed: int, n_heats: int):
    rnd = random.Random(seed)

    heats, sequences, slabs, coils = [], [], [], []
    pkl, defects, qa, yard, downtime, measurements = [], [], [], [], [], []
    golden_roll_end = None

    coil_counter = COIL_NUMBER_BASE
    seq_counter = 100000
    slab_counter = 500000
    t = BASE_TIME

    for h in range(1, n_heats + 1):
        heat_id = "H-" + str(3000 + h).zfill(4)
        furnace = 1 + (h % 2)
        tap_start = t + timedelta(minutes=h * 41)
        tap_end = tap_start + timedelta(minutes=rnd.randint(38, 52))
        target_temp = rnd.uniform(1610, 1640)
        tap_temp = target_temp + rnd.uniform(-8, 6)
        carbon = rnd.uniform(0.04, 0.22)
        oxygen = rnd.uniform(2.0, 9.0)
        heats.append([
            q(heat_id), str(furnace), pg_ts(tap_start), pg_ts(tap_end), q(rnd.choice(STEEL_GRADES)),
            num(target_temp, 3), num(tap_temp, 3), num(carbon, 5), num(oxygen, 3),
        ])

        # one caster sequence per heat
        seq_counter += 1
        sequence_id = "SEQ-" + str(seq_counter)
        ladle_id = "LADLE-" + str(1 + (h % LADLE_COUNT)).zfill(2)
        tundish_id = "TUN-" + str(1 + (h % 3)).zfill(2)
        strand_no = 1 + (h % STRANDS)
        cast_start = tap_end + timedelta(minutes=rnd.randint(8, 20))
        cast_end = cast_start + timedelta(minutes=rnd.randint(45, 75))
        superheat = rnd.uniform(15, 35)
        sequences.append([
            q(sequence_id), q(heat_id), q(ladle_id), q(tundish_id), str(strand_no),
            ora_ts(cast_start), ora_ts(cast_end), num(superheat, 3),
        ])

        n_slabs = rnd.randint(*SLABS_PER_HEAT)
        slab_weight = TONNES_PER_HEAT / n_slabs
        for s in range(n_slabs):
            slab_counter += 1
            slab_id = "SLAB-" + str(slab_counter)
            mould_id = "MOULD-" + str(1 + (s % 4)).zfill(2)
            sl_strand = 1 + (s % STRANDS)
            slabs.append([
                q(slab_id), q(sequence_id), q(heat_id), q(mould_id), str(sl_strand),
                num(slab_weight + rnd.uniform(-1.5, 1.5), 3),
                num(rnd.choice([1000, 1250, 1500]), 2),
                num(rnd.choice([200, 220, 250]), 2),
            ])

            # one HSM coil per slab
            coil_counter += 1
            if coil_counter > COIL_NUMBER_BASE + 5600:
                # cap to keep ~5,600 coils even if heat/slab mix overshoots
                break
            cid = coil_id(coil_counter)
            roll_start = cast_end + timedelta(hours=rnd.randint(2, 30))
            roll_end = roll_start + timedelta(minutes=rnd.randint(3, 9))
            target_fdt = rnd.uniform(850, 900)
            target_ct = rnd.uniform(560, 640)
            coils.append([
                q(cid), q(slab_id), q("HSM-CMP-" + str(1 + (h // 40))), q(rnd.choice(PRODUCT_CODES)),
                ora_ts(roll_start), ora_ts(roll_end),
                num(target_fdt, 3), num(target_fdt + rnd.uniform(-12, 12), 3),
                num(target_ct, 3), num(target_ct + rnd.uniform(-15, 15), 3),
            ])

            for stand in range(1, rnd.randint(4, 8)):
                measurements.append([
                    q(cid + "-M" + str(stand)), q(cid),
                    ora_ts(roll_start + timedelta(seconds=stand * 12)), str(stand),
                    num(rnd.uniform(8000, 26000), 3), num(rnd.uniform(0.5, 18.0), 3),
                ])

            # PKL pickling pass
            pkl_entry = roll_end + timedelta(hours=rnd.randint(4, 48))
            pkl_exit = pkl_entry + timedelta(minutes=rnd.randint(6, 14))
            pkl.append([
                q(cid), plain_ts(pkl_entry), plain_ts(pkl_exit),
                num(rnd.uniform(78, 88), 3), num(rnd.uniform(120, 180), 3),
            ])

            # surface defects (bulk realism)
            is_golden = (coil_counter == GOLDEN_COIL_NUMBER)
            if is_golden:
                golden_roll_end = roll_end
            n_def = GOLDEN_EDGE_CRACKS if is_golden else rnd.choices([0, 1, 2], weights=[70, 22, 8])[0]
            for d in range(n_def):
                did = "D-" + str(4400000 + len(defects) + 1).zfill(7)
                code = "EDGE_CRACK" if is_golden else rnd.choice(DEFECT_CODES)
                sev = "HIGH" if is_golden else rnd.choice(DEFECT_SEVERITY)
                pstart = rnd.uniform(0, 900)
                defects.append([
                    q(did), q(cid), plain_ts(roll_end + timedelta(seconds=d * 3 + 1)),
                    q("CAM-" + str(1 + (d % 4))), q(code), q(sev),
                    num(pstart, 3), num(pstart + rnd.uniform(0.2, 4.0), 3), num(rnd.uniform(1, 12), 3),
                ])

            # QA sample every 3rd coil
            if coil_counter % 3 == 0 or is_golden:
                qlabel = "HOLD" if is_golden else rnd.choice(QA_LABELS)
                qa.append({
                    "sample_id": "QA-" + str(900000 + len(qa) + 1),
                    "coil_id": cid, "slab_id": slab_id, "heat_id": heat_id,
                    "sample_time_utc": iso_z(pkl_exit + timedelta(minutes=rnd.randint(5, 40))),
                    "quality_label": qlabel,
                    "defect_code": "EDGE_CRACK" if is_golden else rnd.choice(DEFECT_CODES),
                    "disposition": "REWORK" if is_golden else rnd.choice(DISPOSITIONS),
                })

            # yard record for every coil
            yard.append({
                "yard_record_id": "Y-" + str(700000 + len(yard) + 1),
                "coil_id": cid, "slab_id": slab_id,
                "storage_area": "AREA-" + str(1 + (coil_counter % 13)).zfill(2),
                "bay": "BAY-03" if is_golden else "B" + str(1 + (coil_counter % 9)),
                "position": "POS-17" if is_golden else str(1 + (coil_counter % 40)),
                "received_at_utc": iso_z(pkl_exit + timedelta(hours=1)),
                "shipped_at_utc": iso_z(pkl_exit + timedelta(days=rnd.randint(2, 20))),
            })

    # downtime events (~ one per 6 heats), some referencing the golden coil window
    for i in range(max(1, n_heats // 6)):
        start = BASE_TIME + timedelta(hours=i * 13 + rnd.randint(0, 6))
        end = start + timedelta(minutes=rnd.randint(8, 90))
        code, text = rnd.choice(DOWNTIME_REASONS)
        downtime.append([
            q("DT-" + str(800000 + i + 1).zfill(7)), q(rnd.choice(EQUIPMENT)),
            plain_ts(start), plain_ts(end), q(code), q(text),
        ])

    # golden-coil narrative: a short roll-change delay just before C-0044170
    if golden_roll_end is not None:
        downtime.append([
            q("DT-HSM-GOLDEN-0044170"), q("HSM-FM"),
            plain_ts(golden_roll_end - timedelta(minutes=9)), plain_ts(golden_roll_end),
            q("ROLL_CHANGE"), q("Short roll-change delay before C-0044170"),
        ])

    return dict(heats=heats, sequences=sequences, slabs=slabs, coils=coils,
                measurements=measurements, pkl=pkl, defects=defects, qa=qa,
                yard=yard, downtime=downtime)


def write_csv_rows(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    out = io.StringIO()
    w = csv.DictWriter(out, fieldnames=fieldnames, lineterminator="\n")
    w.writeheader()
    for r in rows:
        w.writerow(r)
    path.write_text(out.getvalue(), encoding="utf-8")


def write_sql(path: Path, dialect: str, *tables) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    buf = io.StringIO()
    total = sum(len(rows) for _, _, rows in tables)
    buf.write(header(dialect, total))
    # Make this file authoritative + idempotent: clear any prior rows
    # (including the placeholder seed in 001_schema_seed.sql) before loading.
    for table, _, _ in reversed(tables):
        buf.write(f"DELETE FROM {table};\n")
    buf.write("\n")
    for table, columns, rows in tables:
        emit_inserts(buf, table, columns, rows, dialect)
        buf.write("\n")
    path.write_text(buf.getvalue(), encoding="utf-8")
    return total


def main() -> None:
    ap = argparse.ArgumentParser(description="PPIQ T-039 deterministic demo dataset generator")
    ap.add_argument("--seed", type=int, default=DEFAULT_SEED)
    ap.add_argument("--heats", type=int, default=DEFAULT_HEATS)
    ap.add_argument("--out", type=str, default="Infrastructure/demo-sources")
    args = ap.parse_args()

    out = Path(args.out)
    d = generate(args.seed, args.heats)

    counts = {}
    counts["meltshop_heats"] = write_sql(
        out / "meltshop-postgres/init/002_data.sql", "postgres",
        ("meltshop_heats",
         ["heat_id", "furnace_no", "tap_start_utc", "tap_end_utc", "steel_grade",
          "target_temp_c", "tap_temp_c", "carbon_pct", "oxygen_ppm"], d["heats"]))

    counts["caster"] = write_sql(
        out / "caster-oracle/init/002_data.sql", "oracle",
        ("caster_sequences",
         ["sequence_id", "heat_id", "ladle_id", "tundish_id", "strand_no",
          "cast_start_utc", "cast_end_utc", "superheat_c"], d["sequences"]),
        ("caster_slabs",
         ["slab_id", "sequence_id", "heat_id", "mould_id", "strand_no",
          "slab_weight_t", "slab_width_mm", "slab_thickness_mm"], d["slabs"]))

    counts["hsm"] = write_sql(
        out / "hsm-oracle/init/002_data.sql", "oracle",
        ("hsm_coils",
         ["coil_id", "slab_id", "hsm_campaign", "product_code", "rolling_start_utc",
          "rolling_end_utc", "target_fdt_c", "actual_fdt_c", "target_ct_c", "actual_ct_c"], d["coils"]),
        ("hsm_measurements",
         ["measurement_id", "coil_id", "measured_at_utc", "stand_no",
          "rolling_force_kn", "flatness_iunit"], d["measurements"]))

    counts["pkl_coils"] = write_sql(
        out / "pkl-mssql/init/002_data.sql", "mssql",
        ("dbo.pkl_coils",
         ["coil_id", "entry_time_utc", "exit_time_utc", "acid_temp_c", "line_speed_mpm"], d["pkl"]))

    counts["downtime_events"] = write_sql(
        out / "downtime-mysql/init/002_data.sql", "mysql",
        ("downtime_events",
         ["event_id", "equipment_code", "started_at_utc", "ended_at_utc",
          "reason_code", "reason_text"], d["downtime"]))

    counts["parsytec_surface_defects"] = write_sql(
        out / "parsytec-mysql/init/002_data.sql", "mysql",
        ("parsytec_surface_defects",
         ["defect_id", "coil_id", "detected_at_utc", "camera_id", "defect_code",
          "severity", "position_start_m", "position_end_m", "width_mm"], d["defects"]))

    write_csv_rows(out / "excel-qa/qa_samples.csv",
                   ["sample_id", "coil_id", "slab_id", "heat_id", "sample_time_utc",
                    "quality_label", "defect_code", "disposition"], d["qa"])
    counts["qa_samples"] = len(d["qa"])

    write_csv_rows(out / "excel-yard/yard_inventory.csv",
                   ["yard_record_id", "coil_id", "slab_id", "storage_area", "bay",
                    "position", "received_at_utc", "shipped_at_utc"], d["yard"])
    counts["yard_inventory"] = len(d["yard"])

    print("PlantProcess IQ T-039 - generated demo dataset")
    print(f"  seed={args.seed} heats={args.heats} out={out}")
    for k, v in counts.items():
        print(f"    {k:28} {v}")


if __name__ == "__main__":
    main()

'@

    $pyPath = Join-Path $Root 'Backend/tools/generate_demo_dataset.py'
    New-Item -ItemType Directory -Force -Path (Split-Path $pyPath) | Out-Null
    [System.IO.File]::WriteAllText($pyPath, $py, $enc)
    Write-Host ("[write] " + $pyPath) -ForegroundColor Green

    $python = $null
    foreach ($c in @('python','py','python3')) {
        if (Get-Command $c -ErrorAction SilentlyContinue) { $python = $c; break }
    }
    if (-not $python) {
        Write-Host "Python 3 not found on PATH. Install it, then run:" -ForegroundColor Yellow
        Write-Host ("  python `"" + $pyPath + "`" --out `"" + (Join-Path $Root 'Infrastructure/demo-sources') + "`"") -ForegroundColor Yellow
        return
    }

    $outDir = Join-Path $Root 'Infrastructure/demo-sources'
    Write-Host ("[run] " + $python + " generate_demo_dataset.py --out " + $outDir) -ForegroundColor Cyan
    & $python $pyPath --out $outDir --seed 20260601

    Write-Host "`nNext - recreate the demo-source containers so init reloads with the new data:" -ForegroundColor Gray
    Write-Host "  docker compose -f deploy/compose/docker-compose.demo-sources.yml down -v" -ForegroundColor Gray
    Write-Host "  docker compose -f deploy/compose/docker-compose.demo-sources.yml up -d" -ForegroundColor Gray
    Write-Host ""
}
