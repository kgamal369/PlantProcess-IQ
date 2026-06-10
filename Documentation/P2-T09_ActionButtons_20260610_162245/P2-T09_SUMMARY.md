# P2-T09 Action Button Standardization

Generated: 2026-06-10T16:23:04.9569995+03:00

Installed:

- Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx
- tools/phase2/apply-p2-t09-action-buttons.cjs
- tools/ui/audit-action-buttons.cjs
- tools/phase2/validate-p2-t09-action-buttons.cjs
- Frontend/PlantProcess.Web/e2e/p2-t09-action-button-visual.spec.ts
- docs/phase2/P2_T09_ACTION_BUTTON_STANDARDIZATION.md

Acceptance:

- StandardButton supports isDisabled, isLoading, loadingLabel, aria-busy, spinner, and data-loading.
- StandardButton keeps canonical variants: primary, secondary, ghost, action, danger, success.
- disabled= is rejected on StandardButton callers.
- Raw <button> is rejected outside standard wrappers.
- Material Investigation uses canonical StandardButton and primary/secondary hierarchy.
- Visual-regression spec exists for Material Investigation plus representative pages.

Static validation:

PASSED