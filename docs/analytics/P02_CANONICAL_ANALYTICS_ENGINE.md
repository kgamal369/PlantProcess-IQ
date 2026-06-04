# PlantProcess IQ — P2 Canonical Analytics Engine

## Closed by Pack P2A

P2A establishes the canonical analytics path:

- `ICorrelationEngine` is the single authoritative correlation interface.
- `CanonicalCorrelationEngine` wraps the deterministic `IAdvancedCorrelationService` / Analytics.Core path.
- `CorrelationEngineRegistry` exposes the documented default strategy.
- `/analytics/correlations/canonical/run` is the canonical API endpoint for inferential correlation findings.
- Legacy `CorrelationService` remains only for backward-compatible MVP descriptive endpoints and is marked obsolete for inferential claims.
- Canonical findings use `AdvancedAnalysisRunResult` / `AdvancedFinding`, including method, effect, q-value, stability/bootstrap interval, stratification, provenance, and honesty caveat.

## Scope note

Old MVP endpoints remain to avoid breaking the frontend while the canonical run endpoint becomes the official inference path.