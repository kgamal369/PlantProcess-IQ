# Phase 15 Recommendation Honesty & Approval Certification

Marker: PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION

## Purpose

Adds adversarial certification for Phase 15 recommendation honesty and approval governance.

## Certification rules

- No causal language.
- No guaranteed saving claim.
- Weak evidence blocks recommendation.
- Out-of-envelope scenario abstains.
- Approval command must be explicit.
- No automatic write-back path.

## Backend routes

- `GET /api/p15/honesty-certification/health`
- `GET /api/p15/honesty-certification/contract`
- `GET /api/p15/honesty-certification/run`

## Frontend routes

- `/phase15/honesty-certification`
- `/advisory/honesty-certification` alias
