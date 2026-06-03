# PlantProcess IQ — Doctrine v5 P05/P06 Runbook

## P05 — No-Code Visual Mapper

Installed capabilities:

- Source discovery catalog.
- Discovered table/column/sample values.
- Business-key dictionary with composite keys.
- Join preview definitions with sample rows.
- Canonical target suggestions with explanations.
- Dry-run coverage records with SafeSqlValidator check.
- Immutable mapping versions and rollback-ready active version pointer.
- Built-in mapping templates for generic relational, historian tag table, and CSV.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p05_acceptance();
```

API smoke:

```http
GET /api/v5/visual-mapper/health
GET /api/v5/visual-mapper/templates
POST /api/v5/visual-mapper/sessions
POST /api/v5/visual-mapper/sessions/{sessionId}/discover-demo
POST /api/v5/visual-mapper/dry-run
POST /api/v5/visual-mapper/publish
```

## P06 — Blended Provenance & Canonical Depth

Installed capabilities:

- `genealogy_edges.contribution_weight`.
- `genealogy_edges.is_transition`.
- `genealogy_edges.provenance_confidence`.
- Deferred trigger validating child contribution weights sum to 1.0.
- Blended attribution view/function.
- API endpoint for transition attribution and weight status.
- Frontend proof page for mapper/provenance flow.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p06_acceptance();
SELECT * FROM public.ppiq_v5_child_weight_status;
```

API smoke:

```http
GET /api/v5/blended-provenance/health
GET /api/v5/blended-provenance/weights/status
GET /api/v5/blended-provenance/child/{childMaterialUnitId}/attribution
```

Full validation:

```powershell
.\tools\v5\validate-p05-p06.ps1
```
