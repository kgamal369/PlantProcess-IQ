# Phase 15 Value-Realization Tracking

Marker: PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING

## Purpose

Tracks realized value by comparing a baseline KPI window against an actual KPI window after recommendation review.

## Guardrails

- Baseline and actual windows must use the same KPI metric.
- Realized value must link to a source recommendation.
- Attribution caveat must be visible.
- Correlation is not causation.
- Changing actual value changes realized value.

## Backend routes

- `GET /api/p15/value-realization/health`
- `GET /api/p15/value-realization/contract`
- `GET /api/p15/value-realization/demo-request`
- `POST /api/p15/value-realization/calculate`
- `POST /api/p15/value-realization/calculate-demo`
- `GET /api/p15/value-realization/ledger`

## Frontend routes

- `/phase15/value-realization`
- `/advisory/value-realization` alias
