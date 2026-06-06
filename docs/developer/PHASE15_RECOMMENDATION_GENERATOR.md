# Phase 15 Recommendation Generator

Marker: PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT

## Purpose

The recommendation generator converts supported what-if scenario projections into guarded advisory recommendations with expected e-impact range, confidence, evidence, provenance and explicit approval workflow.

## Guardrails

- No causal language.
- Expected e-impact is projection-only.
- Confidence, evidence and provenance are required.
- Weak evidence blocks recommendation.
- Human approval is required.
- No automatic process write-back.

## Backend routes

- `GET /api/p15/advisory/recommendations/health`
- `GET /api/p15/advisory/recommendations/contract`
- `GET /api/p15/advisory/recommendations/demo-request`
- `POST /api/p15/advisory/recommendations/generate`
- `POST /api/p15/advisory/recommendations/generate-demo`
- `POST /api/p15/advisory/recommendations/approve`
- `GET /api/p15/advisory/recommendations/approvals`

## Frontend routes

- `/phase15/recommendations`
- `/advisory/recommendations` alias
