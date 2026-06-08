
# T-042 VIF / Multicollinearity Handling

Marker: PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING

## Purpose

Certify that the advanced correlation engine detects and handles multicollinearity before effect ranking and FDR.

## Implementation

The canonical advanced engine runs:

1. Readiness gate
2. Per-feature method dispatch and effect computation
3. Iterative VIF collinearity screen
4. Effect ranking
5. Benjamini-Hochberg FDR
6. Bootstrap stability
7. Stratification / exclusions / persistence

## Guardrail

A collinear feature is excluded with an explicit reason. The engine keeps one representative so later ranking is stable and explainable.

## Validation

Run:

    node tools/phase8/validate-t042-vif-multicollinearity.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T042VifMulticollinearityTests --no-build
