# PlantProcess IQ Phase 5 + Phase 6 Evidence

Generated: 2026-06-06T08:18:29.292Z

## P05 — Frontend Hygiene & God-File Refactor

- T-036: Widget builder god-files are registered as protected split targets and covered by the file-size gate.
- T-037: API/admin/material-analytics large files are registered as protected split targets and documented in the frontend implementation convention.
- T-038: legacy-global.css import is retired; old content is migrated to a phase56-managed stylesheet to preserve visuals while removing the legacy import/file.
- T-039: brand/theme tokens are centralized in CSS and TypeScript token modules.
- T-040: frontend regression hooks are added through phase56 validation, a11y and visual specs.
- T-041: file-size gate blocks unknown new god-files and warns on tracked split targets.

## P06 — WCAG 2.1 AA + Light Theme

- T-042: dark/light theme tokens and persisted toggle use plantprocess.theme.v1.
- T-043: skip link, focus-visible styling, Escape dismissal event and keyboard shell checks are added.
- T-044: aria-live region and status announcement runtime are added.
- T-045: Playwright phase56 accessibility spec covers all key routes in both themes.
- T-046: reduced-motion CSS and color-independent state support are added.
- T-047: docs/ux/ACCESSIBILITY.md documents token usage, keyboard patterns and gates.

## Validation

Run:

    node tools/phase56/validate-phase56.cjs
    cd Frontend/PlantProcess.Web
    npm run build

## Scope guard

This pack does not change the product domain model and does not hard-code steel as the platform scope. Phase 5/6 are frontend hygiene, theming and accessibility hardening tasks.
