# Phase 15 ROI / CFO Value Dashboard

Marker: PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD

## Purpose

Provides a buyer-facing CFO dashboard that separates potential value from realized value, computes payback period and produces an exportable evidence pack.

## Guardrails

- Potential value and realized value are separated.
- Realized value reconciles with the value-realization ledger.
- Payback period is computed from realized value.
- Export evidence pack carries ledger IDs, provenance and caveats.
- Correlation is not causation.

## Backend routes

- `GET /api/p15/roi-cfo-dashboard/health`
- `GET /api/p15/roi-cfo-dashboard/contract`
- `GET /api/p15/roi-cfo-dashboard/summary`
- `GET /api/p15/roi-cfo-dashboard/evidence-pack`

## Frontend routes

- `/phase15/roi-cfo-dashboard`
- `/advisory/roi-cfo-dashboard` alias
