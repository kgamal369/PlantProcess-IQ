
# P2-T08 — Standard Component Rollout

Marker: PPIQ_P2_T08_STANDARD_COMPONENT_ROLLOUT_BLOCKING

## Result

P2-T08 makes the Standard-component rollout blocking instead of advisory.

Installed:

- Standard tolerant control adapters:
  - StandardP2Button
  - StandardP2Input
  - StandardP2Select
  - StandardP2TextArea
  - StandardP2Table
- Brand CSS contract:
  - disabled opacity = 0.35
  - 8px radius
  - Inter / system fallback, 14px, semibold
  - visible focus ring
- UI audit:
  - tools/ui/audit-ui-instances.cjs
- Blocking npm gate:
  - npm run validate:standard-imports

## Validation

Run:

    node tools/phase2/validate-p2-t08-standard-rollout.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:standard-imports
    npm run build

Expected:

- 0 native form controls outside src/components/standard
- 0 native table tags outside src/components/standard
- 0 inline style props outside src/components/standard
- validate:standard-imports exits non-zero on future drift
