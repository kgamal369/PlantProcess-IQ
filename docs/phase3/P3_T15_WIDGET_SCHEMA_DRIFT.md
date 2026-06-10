
# P3-T15 — Widget/dashboard schema-drift root-cause fix

Marker: P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED

## Result

Installed a frontend widget contract layer and proof page:

- Route: /dashboard/widgets/schema-drift
- Canonical widget-definition normalizer
- Contract validator for required widget fields
- Frontend query builder from persisted widget definition JSON
- Heatmap widget proof
- Interactive search, min-value filter, sort-by, and sort-direction controls
- Series-signature proof that filter/sort updates the chart without dashboard reload

## Why this fixes the root cause

The frontend no longer consumes raw widget definitions ad hoc. It normalizes backend PascalCase/camelCase widget-definition payloads into one canonical frontend contract before rendering or executing a widget query.

## Validation

Run:

    node tools/phase3/validate-p3-t15-widget-schema-drift.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/Dashboard/p3t15WidgetSchemaDrift.test.ts --config vitest.config.ts

Optional e2e:

    npx playwright test tests/e2e/p3t15-widget-schema-drift.spec.ts
