
# T-037 Value Engine Depth

Marker: PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_RANGE

## Result

The value engine now emits bounded Low / Expected / High ranges, abstains when required assumptions are missing or invalid, preserves provenance handles, and persists richer evidence JSON.

## Honesty rule

This engine must never emit guaranteed savings. It emits a projected bounded range only.

## Validation

Run:

    node tools/phase7/validate-t037-value-engine-depth.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase7_ValueImpactEngineDepthTests
