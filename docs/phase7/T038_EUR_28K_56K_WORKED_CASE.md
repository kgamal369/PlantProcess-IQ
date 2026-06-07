
# T-038 EUR 28k-56k Worked Case

Marker: PPIQ_REALIZATION_T038_EUR_28K_56K_WORKED_CASE_FIXTURE

## Formula

Affected tons:

    0.02 defect-rate delta × 10,000 monthly tons = 200 affected tons/month

Downgrade delta assumption band:

    EUR 140 / 210 / 280 per ton

Bounded result:

    Low      = 200 × 140 = EUR 28,000/month
    Expected = 200 × 210 = EUR 42,000/month
    High     = 200 × 280 = EUR 56,000/month

## Honesty

This is a deterministic worked-case fixture for demo/proof. It is not a guaranteed saving and not a production claim.

## Validation

Run:

    node tools/phase7/validate-t038-eur-28k-56k-worked-case.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase7_ValueImpactWorkedCaseTests --no-build
