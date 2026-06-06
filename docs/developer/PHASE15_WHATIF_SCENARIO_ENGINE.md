# Phase 15 What-if Scenario Engine

Marker: PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE

## Purpose

This engine provides deterministic what-if projection for supported findings. It is not a causal optimizer and not a process controller.

## Guardrails

- Projection only. Not a guaranteed saving.
- Same request and seed return the same result.
- Out-of-envelope adjustments abstain.
- Weak or missing evidence blocks supported projection.
- No automatic write-back path exists.

## Backend routes

- `GET /api/p15/advisory/scenarios/health`
- `GET /api/p15/advisory/scenarios/contract`
- `GET /api/p15/advisory/scenarios/sample-request`
- `POST /api/p15/advisory/scenarios/simulate`
- `POST /api/p15/advisory/scenarios/simulate-demo`

## Frontend routes

- `/phase15/scenario-simulation`
- `/advisory/scenario-simulation` alias
