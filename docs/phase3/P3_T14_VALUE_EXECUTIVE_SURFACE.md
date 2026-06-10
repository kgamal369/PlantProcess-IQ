
# P3-T14 — Value/ROI executive surface

Marker: P3_T14_VALUE_ROI_EXECUTIVE_SURFACE

## Result

The frontend now exposes:

- Route: /value/executive
- Real engine call: PUT /api/value/cost-assumptions then POST /api/value/impact
- Executive Low / Mid / High cards
- Input drill-through per value term
- Provenance handle per value term
- ABSTAIN proof for missing assumptions
- Payback view versus monthly license cost
- Print-friendly monthly value report PDF surface

## Honesty guard

The page does not emit money values when the engine abstains. It does not use forbidden commercial certainty wording.

## Validation

Run:

    node tools/phase3/validate-p3-t14-value-executive.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/ValueExecutive/p3t14ValueExecutive.test.ts --config vitest.config.ts

Optional e2e:

    npx playwright test tests/e2e/p3t14-value-executive.spec.ts
