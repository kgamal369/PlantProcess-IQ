# Phase 15 Advisory/Value Contract

Marker: PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT

## Purpose

This contract is the shared spine for Phase 15: what-if simulation, recommendation generation, value-realization ledger, ROI/CFO dashboard, benchmarking and recommendation honesty certification.

## Non-negotiable rules

- Projection only. Not a guaranteed saving.
- No causal language unless a future certified causal engine explicitly proves causality.
- No automatic process write-back.
- Every recommendation must include confidence, evidence and provenance.
- Human approval is required before any downstream operational action.
- Weak evidence must block the recommendation.
- Out-of-envelope scenario requests must abstain.
- Benchmarking must be privacy-preserving and cohort-size protected.

## Backend contract files

- `Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs`
- `Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs`

## Contract families

- Scenario request/response contracts.
- Recommendation and approval contracts.
- Value-realization ledger contracts.
- ROI/CFO summary contracts.
- Benchmark request/response contracts.
- Honesty policy decision contracts.
