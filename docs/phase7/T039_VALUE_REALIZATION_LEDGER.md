
# T-039 Value-Realization / Tracked ROI Recording

Marker: PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_CONTRACTS

## Purpose

Track realized value separately from projected value impact.

## Guardrails

- Potential value and realized value are separated.
- Baseline and actual windows must use the same metric and unit.
- Ledger recording requires a source recommendation or value-impact link.
- ROI is based on tracked realized value, not projected value.
- Correlation is not causation.
- Baseline-vs-actual tracked value is not automatic causal attribution.

## Backend routes

- GET /api/value/realization/contract
- POST /api/value/realization/calculate
- POST /api/value/realization/record
- GET /api/value/realization/ledger

## Validation

Run:

    node tools/phase7/validate-t039-value-realization-ledger.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase7_ValueRealizationTrackingTests --no-build
