#!/usr/bin/env python3
"""
PlantProcess IQ synthetic data generator.

Purpose:
  Generate safe, non-confidential CSV source extracts for import-workflow demos:
    - materials.csv
    - aliases.csv
    - process_steps.csv
    - parameters.csv
    - quality_events.csv
    - genealogy_edges.csv

Design:
  This generator intentionally keeps industry words in data only. The product model remains generic.
  It creates hidden defect patterns so Phase G correlation can discover them:
    - high CastingSpeed increases SurfaceCrack risk
    - high Superheat increases Inclusion risk
    - some equipment has higher baseline defect probability

Usage:
  python tools/generate_synthetic_plantprocessiq.py --rows 100000 --out data/synthetic/phase_f
"""

from __future__ import annotations

import argparse
import csv
import json
import random
from datetime import datetime, timedelta, timezone
from pathlib import Path


def utc(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def write_csv(path: Path, rows: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not rows:
        return
    with path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rows", type=int, default=100_000, help="Number of parameter rows to generate")
    parser.add_argument("--out", type=str, default="data/synthetic/phase_f", help="Output folder")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    random.seed(args.seed)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    # Keep total material count proportional to parameter count.
    material_count = max(1000, args.rows // 20)
    base_time = datetime(2026, 3, 1, 6, 0, tzinfo=timezone.utc)

    materials = []
    aliases = []
    steps = []
    parameters = []
    quality = []
    genealogy = []

    equipment_codes = ["CASTER_A", "CASTER_B", "HSM_01", "INSPECTION_01"]
    grades = ["G-A", "G-B", "G-C"]

    for i in range(1, material_count + 1):
        heat = f"H{i:06d}"
        slab = f"S{i:06d}"
        coil = f"C{i:06d}"
        grade = random.choice(grades)
        start = base_time + timedelta(minutes=i * 3)

        materials.append({"material_id": heat, "material_type": "Heat", "product_family": "FlatSteelDemo", "grade": grade, "site_code": "DEMO_PLANT_001", "start_utc": utc(start), "end_utc": utc(start + timedelta(minutes=40))})
        materials.append({"material_id": slab, "material_type": "Slab", "product_family": "FlatSteelDemo", "grade": grade, "site_code": "DEMO_PLANT_001", "start_utc": utc(start + timedelta(minutes=40)), "end_utc": utc(start + timedelta(minutes=100))})
        materials.append({"material_id": coil, "material_type": "Coil", "product_family": "FlatSteelDemo", "grade": grade, "site_code": "DEMO_PLANT_001", "start_utc": utc(start + timedelta(minutes=100)), "end_utc": utc(start + timedelta(minutes=160))})

        aliases.append({"material_id": coil, "alias_code": f"QMS-{coil}", "alias_type": "QMS_ID", "source_system": "QMS_SYNTH"})
        genealogy.append({"parent_code": heat, "child_code": slab, "relationship_type": "CastInto", "effective_from_utc": utc(start + timedelta(minutes=40))})
        genealogy.append({"parent_code": slab, "child_code": coil, "relationship_type": "RolledInto", "effective_from_utc": utc(start + timedelta(minutes=100))})

        caster = random.choice(["CASTER_A", "CASTER_B"])
        steps.append({"piece_id": slab, "operation": "Continuous_Casting", "operation_code": "Continuous_Casting", "equipment": caster, "start_utc": utc(start + timedelta(minutes=40)), "end_utc": utc(start + timedelta(minutes=100)), "crew_code": random.choice(["A", "B", "C"])})
        steps.append({"piece_id": coil, "operation": "Hot_Rolling", "operation_code": "Hot_Rolling", "equipment": "HSM_01", "start_utc": utc(start + timedelta(minutes=100)), "end_utc": utc(start + timedelta(minutes=160)), "crew_code": random.choice(["A", "B", "C"])})

        # Hidden defect pattern values
        casting_speed = random.gauss(1.45, 0.18)
        superheat = random.gauss(28, 9)
        if caster == "CASTER_B":
            casting_speed += 0.08

        parameters.append({"piece_id": slab, "tag": "CastingSpeed", "value": round(casting_speed, 3), "sample_time": utc(start + timedelta(minutes=65)), "quality": "Good"})
        parameters.append({"piece_id": slab, "tag": "Superheat", "value": round(superheat, 3), "sample_time": utc(start + timedelta(minutes=55)), "quality": "Good"})
        parameters.append({"piece_id": coil, "tag": "RollingForce", "value": round(random.gauss(1450, 120), 3), "sample_time": utc(start + timedelta(minutes=120)), "quality": "Good"})

        p_surface = 0.02 + max(0.0, casting_speed - 1.55) * 0.22 + (0.04 if caster == "CASTER_B" else 0.0)
        p_inclusion = 0.015 + max(0.0, superheat - 34) * 0.01

        if random.random() < p_surface:
            quality.append({"coil_id": coil, "quality_event": "Defect", "severity": "Major", "decision": "Hold", "defect_code": "SurfaceCrack", "event_at_utc": utc(start + timedelta(minutes=170))})
        elif random.random() < p_inclusion:
            quality.append({"coil_id": coil, "quality_event": "Defect", "severity": "Major", "decision": "Review", "defect_code": "Inclusion", "event_at_utc": utc(start + timedelta(minutes=170))})
        else:
            quality.append({"coil_id": coil, "quality_event": "Released", "severity": "Info", "decision": "Released", "defect_code": "", "event_at_utc": utc(start + timedelta(minutes=170))})

    # Downsample parameter rows to requested row count if needed.
    while len(parameters) < args.rows:
        src = random.choice(parameters)
        clone = dict(src)
        clone["sample_time"] = utc(base_time + timedelta(seconds=random.randint(0, material_count * 180)))
        clone["value"] = round(float(clone["value"]) + random.gauss(0, 0.02), 3)
        parameters.append(clone)

    parameters = parameters[: args.rows]

    write_csv(out / "materials.csv", materials)
    write_csv(out / "aliases.csv", aliases)
    write_csv(out / "process_steps.csv", steps)
    write_csv(out / "parameters.csv", parameters)
    write_csv(out / "quality_events.csv", quality)
    write_csv(out / "genealogy_edges.csv", genealogy)

    manifest = {
        "generated_at_utc": utc(datetime.now(timezone.utc)),
        "parameter_rows": len(parameters),
        "material_rows": len(materials),
        "quality_rows": len(quality),
        "hidden_patterns": [
            "CastingSpeed high -> SurfaceCrack risk",
            "Superheat high -> Inclusion risk",
            "CASTER_B has higher SurfaceCrack baseline"
        ]
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
