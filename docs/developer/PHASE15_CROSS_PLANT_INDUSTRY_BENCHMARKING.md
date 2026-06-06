# Phase 15 Cross-Plant & Industry Benchmarking

Marker: PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING

## Purpose

Adds privacy-preserving cross-plant and industry benchmarking using anonymized aggregate bands and minimum cohort suppression.

## Guardrails

- No identifiable cross-tenant row exposure.
- Only anonymized aggregate bands are returned.
- Minimum cohort size is enforced.
- Below-minimum cohort benchmark is suppressed.
- Reference bands are configuration/template driven.
- Generic manufacturing model, not steel-only.

## Backend routes

- `GET /api/p15/benchmarking/health`
- `GET /api/p15/benchmarking/contract`
- `GET /api/p15/benchmarking/demo-request`
- `GET /api/p15/benchmarking/summary`
- `GET /api/p15/benchmarking/suppressed-demo`
- `POST /api/p15/benchmarking/benchmark`

## Frontend routes

- `/phase15/benchmarking`
- `/advisory/benchmarking` alias
