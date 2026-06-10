
# P3-T17 — Cost Assumption Management i18n + RTL Contract

Marker: PPIQ_P3_T17_COST_I18N_CONTRACT

## Result

P3-T17 certifies that the high-traffic Cost Assumption Management screen has a stable i18n contract.

## Scope

- Namespace: v5.p3.cost
- Screen code: v5-p3-cost-assumptions
- Required keys:
  - title
  - description
  - save
- Required locales:
  - en
  - de
  - ar
- RTL requirement:
  - Arabic locale must resolve direction = rtl.

## Installed files

- Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts
- Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts
- Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql
- tools/phase3/validate-p3-t17-cost-i18n.cjs

## Database application

Local laptop main DB is native Windows PostgreSQL, not Docker. Apply manually only when DB proof is required:

    & "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -f Backend\database\scripts\425_p3_t17_cost_assumption_i18n_contract.sql

Then check:

    SELECT * FROM public.ppiq_p3_t17_cost_i18n_status();

## Validation

Run:

    node tools/phase3/validate-p3-t17-cost-i18n.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/i18n/p3T17CostAssumptionI18n.test.ts --config vitest.config.ts
