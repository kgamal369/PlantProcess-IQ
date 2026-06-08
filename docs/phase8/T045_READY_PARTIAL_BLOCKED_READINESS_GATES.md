
# T-045 Ready / Partial / Blocked Readiness Gates

Marker: PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES

## Purpose

Expose advanced-analysis readiness as an explicit API and HMI contract:

- Ready
- Partial
- Blocked

## Backend

Adds:

- AdvancedReadinessGateStates
- AdvancedReadinessGateDto
- AdvancedReadinessGateSummaryDto
- AdvancedReadinessGateProjector
- GET /api/analytics/advanced/readiness/gates

## Frontend / HMI

Adds visible readiness gate cards to the Advanced Analysis page.

Each gate displays:

- state
- reason
- evidence string
- blocking status

## Guardrail

Blocked analysis must abstain. Partial analysis may run but must show that at least one dimension needs attention.

## Validation

Run:

    node tools/phase8/validate-t045-readiness-gates.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T045ReadinessGateSurfaceTests --no-build
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/Analytics/advancedReadinessGateView.test.ts --config vitest.config.ts
