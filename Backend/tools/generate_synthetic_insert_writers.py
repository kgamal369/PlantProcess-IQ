from __future__ import annotations

import argparse
import random
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path


@dataclass(frozen=True)
class GeneratorConfig:
    output_dir: Path
    heats: int
    coils_per_heat: int
    seed: int


def sql(value):
    if value is None:
        return "NULL"

    if isinstance(value, bool):
        return "TRUE" if value else "FALSE"

    if isinstance(value, (int, float)):
        return str(value)

    escaped = str(value).replace("'", "''")
    return f"'{escaped}'"


def iso(dt: datetime):
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%d %H:%M:%S+00")


def ensure_header(table_name: str):
    return f"""-- ============================================================
-- PlantProcess IQ synthetic source-system insert writer
-- Source table: {table_name}
-- Purpose: demo-only raw customer-like source table data.
-- Generated data must stay outside canonical schema until mapped.
-- ============================================================

"""


def write_hsm(config: GeneratorConfig):
    path = config.output_dir / "synthetic_hsm_source_insert.sql"
    start = datetime(2026, 5, 1, 6, 0, tzinfo=timezone.utc)

    lines = [
        ensure_header("hsm_piece_tracking"),
        """
CREATE TABLE IF NOT EXISTS hsm_piece_tracking (
    hsm_piece_id TEXT PRIMARY KEY,
    heat_no TEXT NOT NULL,
    coil_no TEXT NOT NULL,
    mill_stand TEXT NOT NULL,
    entry_temp_c NUMERIC(10,2),
    exit_temp_c NUMERIC(10,2),
    speed_mps NUMERIC(10,3),
    roll_force_kn NUMERIC(10,2),
    started_at_utc TIMESTAMPTZ NOT NULL,
    completed_at_utc TIMESTAMPTZ NOT NULL
);

""",
    ]

    for heat in range(1, config.heats + 1):
        heat_no = f"H{heat:05d}"

        for coil in range(1, config.coils_per_heat + 1):
            coil_no = f"C{heat:05d}-{coil:03d}"
            event_time = start + timedelta(minutes=(heat * 18) + coil * 7)

            for stand in range(1, 8):
                entry_temp = random.uniform(980, 1190) - stand * random.uniform(5, 12)
                exit_temp = entry_temp - random.uniform(8, 22)
                speed = random.uniform(5.5, 15.5)
                force = random.uniform(9000, 26000)

                piece_id = f"{coil_no}-S{stand}"

                lines.append(
                    "INSERT INTO hsm_piece_tracking "
                    "(hsm_piece_id, heat_no, coil_no, mill_stand, entry_temp_c, exit_temp_c, speed_mps, roll_force_kn, started_at_utc, completed_at_utc) "
                    f"VALUES ({sql(piece_id)}, {sql(heat_no)}, {sql(coil_no)}, {sql(f'S{stand}')}, "
                    f"{entry_temp:.2f}, {exit_temp:.2f}, {speed:.3f}, {force:.2f}, "
                    f"{sql(iso(event_time))}, {sql(iso(event_time + timedelta(minutes=2)))}) "
                    "ON CONFLICT (hsm_piece_id) DO NOTHING;\n"
                )

    path.write_text("".join(lines), encoding="utf-8")
    return path


def write_inspection(config: GeneratorConfig):
    path = config.output_dir / "synthetic_inspection_source_insert.sql"
    start = datetime(2026, 5, 1, 8, 0, tzinfo=timezone.utc)

    defect_types = ["SCRATCH", "SCALE", "EDGE_CRACK", "PINHOLE", "ROLL_MARK", "NO_DEFECT"]
    severities = ["Info", "Minor", "Major", "Critical"]

    lines = [
        ensure_header("inspection_surface_events"),
        """
CREATE TABLE IF NOT EXISTS inspection_surface_events (
    inspection_event_id TEXT PRIMARY KEY,
    coil_no TEXT NOT NULL,
    device_code TEXT NOT NULL,
    defect_type TEXT NOT NULL,
    severity TEXT NOT NULL,
    position_m NUMERIC(12,3),
    width_pos_mm NUMERIC(12,3),
    confidence NUMERIC(6,3),
    detected_at_utc TIMESTAMPTZ NOT NULL
);

""",
    ]

    for heat in range(1, config.heats + 1):
        for coil in range(1, config.coils_per_heat + 1):
            coil_no = f"C{heat:05d}-{coil:03d}"
            base_time = start + timedelta(minutes=(heat * 19) + coil * 8)

            event_count = random.randint(8, 25)

            for event in range(1, event_count + 1):
                defect = random.choices(
                    defect_types,
                    weights=[12, 18, 5, 7, 8, 50],
                    k=1,
                )[0]

                severity = "Info" if defect == "NO_DEFECT" else random.choice(severities)
                event_id = f"INS-{coil_no}-{event:04d}"

                lines.append(
                    "INSERT INTO inspection_surface_events "
                    "(inspection_event_id, coil_no, device_code, defect_type, severity, position_m, width_pos_mm, confidence, detected_at_utc) "
                    f"VALUES ({sql(event_id)}, {sql(coil_no)}, {sql('SURF-INSP-01')}, {sql(defect)}, {sql(severity)}, "
                    f"{random.uniform(0, 1200):.3f}, {random.uniform(-900, 900):.3f}, {random.uniform(0.65, 0.99):.3f}, "
                    f"{sql(iso(base_time + timedelta(seconds=event * 15)))}) "
                    "ON CONFLICT (inspection_event_id) DO NOTHING;\n"
                )

    path.write_text("".join(lines), encoding="utf-8")
    return path


def write_qms(config: GeneratorConfig):
    path = config.output_dir / "synthetic_qms_source_insert.sql"
    start = datetime(2026, 5, 1, 10, 0, tzinfo=timezone.utc)

    decisions = ["ACCEPT", "HOLD", "REWORK", "REJECT"]
    grades = ["S235", "S355", "DX51D", "304", "316L"]

    lines = [
        ensure_header("qms_quality_decisions"),
        """
CREATE TABLE IF NOT EXISTS qms_quality_decisions (
    qms_decision_id TEXT PRIMARY KEY,
    coil_no TEXT NOT NULL,
    grade_code TEXT NOT NULL,
    decision TEXT NOT NULL,
    quality_score NUMERIC(8,3),
    reason_code TEXT,
    decided_by TEXT NOT NULL,
    decided_at_utc TIMESTAMPTZ NOT NULL
);

""",
    ]

    for heat in range(1, config.heats + 1):
        for coil in range(1, config.coils_per_heat + 1):
            coil_no = f"C{heat:05d}-{coil:03d}"
            quality_score = random.uniform(45, 99)
            decision = random.choices(decisions, weights=[76, 14, 7, 3], k=1)[0]

            reason_code = None
            if decision != "ACCEPT":
                reason_code = random.choice(["SURFACE_RISK", "DIMENSION_RISK", "CHEMISTRY_REVIEW", "PROCESS_DEVIATION"])

            decision_id = f"QMS-{coil_no}"

            lines.append(
                "INSERT INTO qms_quality_decisions "
                "(qms_decision_id, coil_no, grade_code, decision, quality_score, reason_code, decided_by, decided_at_utc) "
                f"VALUES ({sql(decision_id)}, {sql(coil_no)}, {sql(random.choice(grades))}, {sql(decision)}, "
                f"{quality_score:.3f}, {sql(reason_code)}, {sql('synthetic.qms')}, "
                f"{sql(iso(start + timedelta(minutes=(heat * 21) + coil * 9)))}) "
                "ON CONFLICT (qms_decision_id) DO NOTHING;\n"
            )

    path.write_text("".join(lines), encoding="utf-8")
    return path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", default="Backend/database/seed/source-systems")
    parser.add_argument("--heats", type=int, default=30)
    parser.add_argument("--coils-per-heat", type=int, default=6)
    parser.add_argument("--seed", type=int, default=20260524)

    args = parser.parse_args()

    config = GeneratorConfig(
        output_dir=Path(args.output_dir),
        heats=args.heats,
        coils_per_heat=args.coils_per_heat,
        seed=args.seed,
    )

    random.seed(config.seed)
    config.output_dir.mkdir(parents=True, exist_ok=True)

    paths = [
        write_hsm(config),
        write_inspection(config),
        write_qms(config),
    ]

    print("Generated synthetic source INSERT writers:")
    for path in paths:
        print(f" - {path}")


if __name__ == "__main__":
    main()