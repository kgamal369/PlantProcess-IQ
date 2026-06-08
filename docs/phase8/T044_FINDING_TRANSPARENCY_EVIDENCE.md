
# T-044 Finding Transparency Evidence

Marker: PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE

## Purpose

Certify that every advanced correlation finding surfaces:

- population / paired sample size
- dropped or excluded records
- stratification evaluated / not evaluated state
- stratification verdict reason
- provenance handle
- honesty caveat

## Implementation

The existing AdvancedFinding contract already carries the statistical evidence fields. T-044 adds an explicit transparency projection:

- AdvancedFindingTransparency
- AdvancedFindingTransparencyProjector

This avoids changing the core finding constructor while making the HMI/API surface explicit and testable.

## Guardrail

A finding is not considered complete unless it has population, exclusions, stratification reason, provenance and honesty caveat.

## Validation

Run:

    node tools/phase8/validate-t044-finding-transparency.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T044FindingTransparencyTests --no-build
