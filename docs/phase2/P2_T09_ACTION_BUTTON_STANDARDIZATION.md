# P2-T09 — Standardize Action Buttons + Material Investigation Styling

Marker: PPIQ_P2_T09_ACTION_BUTTON_STANDARDIZATION

## Goal

P2-T09 completes the action-button contract after P2-T08.

It standardizes:

- Canonical StandardButton variants:
  - primary
  - secondary
  - ghost
  - action
  - danger
  - success
- `isDisabled` instead of native `disabled` on StandardButton callers.
- `isLoading`, `loadingLabel`, spinner, `aria-busy`, and `data-loading`.
- Material Investigation action hierarchy and styling.
- Raw `<button>` rejection outside standard wrappers.
- Visual-regression coverage for Material Investigation and representative pages.

## Validation

Run:

    node tools/phase2/validate-p2-t09-action-buttons.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:action-buttons
    npm run validate:standard-imports
    npm run build