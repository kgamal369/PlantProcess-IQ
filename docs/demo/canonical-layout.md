# PlantProcess IQ canonical flat-steel demo layout

This is demo configuration, not a steel-only product model.

The generic PlantProcess IQ canonical model remains manufacturing-agnostic. This file documents the flat-steel pilot/demo source topology used to prove cross-source mapping, genealogy, and defect investigation.

## Areas and systems

| Sort | Area | Equipment | Source | Purpose |
|---:|---|---|---|---|
| 10 | EAF/LF | EAF-01 | DEMO_MELTSHOP_PG | Heat chemistry, additives, melt process |
| 20 | EAF/LF | LF-01 | DEMO_MELTSHOP_PG | Final chemistry adjustment |
| 30 | CC | CASTER-02 | DEMO_CASTER_ORACLE | Sequence, tundish, strand, slab |
| 40 | HSM | HSM-F5 | DEMO_HSM_ORACLE | Rolling force, flatness, temperature |
| 50 | PKL | PKL-01 | DEMO_PKL_MSSQL | Pickling process values |
| 60 | YARD | YARD-HOT | DEMO_YARD_EXCEL | Coil storage and movement |
| 70 | INSPECTION | PARSYTEC-01 | DEMO_PARSYTEC_MYSQL | Surface defects |
| 80 | QA | QA-LAB | DEMO_QA_EXCEL | QA samples and final decision |

## Known end-to-end lineage

`ADV_HEAT4002 -> ADV_LADLE4002 -> ADV_TUNDISH4002 -> ADV_SEQ4002 -> ADV_SLAB4002 -> ADV_COIL4002 -> ADV_DEFECT4002_1 / ADV_QA4002`

Validation query:

```sql
SELECT * FROM public.ppiq_demo_resolve_genealogy('ADV_COIL4002');
```
