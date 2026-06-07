
# T-040 Value Scenario Page

Marker: PPIQ_REALIZATION_T040_VALUE_SCENARIO_PAGE

## Result

The frontend now has a real Value Scenario Workbench route:

    /value/scenario

The page wires the Phase 07 value APIs:

- PUT /api/value/cost-assumptions
- POST /api/value/impact
- POST /api/value/realization/calculate
- POST /api/value/realization/record

## Guardrails

- Projected impact and tracked realized value are separated.
- The page reproduces the EUR 28k / 42k / 56k worked case.
- The page shows that projected value is not a guaranteed saving.
- The page shows that baseline-vs-actual value is not automatic causal attribution.

## Validation

Run:

    node tools/phase7/validate-t040-value-scenario-page.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts --config vitest.config.ts
