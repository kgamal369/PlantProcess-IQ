# PlantProcess IQ UI Component Standards

## Scope

This standard defines canonical UI primitives for PlantProcess IQ.

It supports:
- Generic manufacturing quality intelligence.
- Investigation-first workflows.
- Plant data quality and risk analysis.
- Future BI, Qlik, Power BI, Python ML, correlation, and explainability integration.
- Demo and commercial packaging without hard-coding steel-only concepts.

Steel terminology must remain page content, configuration, metadata, or demo data. It must not be baked into base UI components.

## PPIQ-T013 - StandardButton

Use StandardButton for all direct user actions.

Required behavior:
- Uses type button by default.
- Supports loading, disabled, aria-busy, and aria-disabled.
- Supports variants: primary, secondary, ghost, danger, success.
- Supports sizes: xs, sm, md, lg.
- Supports left and right icons.
- Must not replace semantic links for route navigation.

## PPIQ-T014 - StandardTable

Use StandardTable for structured analytical data.

Required behavior:
- Centralized empty, loading, and error states.
- Explicit column definitions.
- Explicit getRowKey.
- Optional row tone: neutral, success, warning, danger.
- Optional row click behavior.
- Does not own filtering, paging, or API calls.

## PPIQ-T015 - StandardTabs

Use StandardTabs for tab-like sections and segmented views.

Required behavior:
- Uses tablist, tab, and tabpanel roles.
- Requires ariaLabel.
- Supports disabled tabs.
- Supports badges and icons.
- Does not own route navigation unless wrapped by a page-level route strategy.

## PPIQ-T016 - StandardInput, StandardSelect, StandardTextArea

Use standard fields for every form field.

Required behavior:
- Label support.
- Required marker support.
- Helper text support.
- Error text support.
- aria-invalid and aria-describedby support.
- No hidden validation messages.

## PPIQ-T017 - StandardCard, StandardModal, StandardToast

StandardCard is used for grouped information, metrics, settings, and analytical panels.

StandardModal is used for blocking confirmation or focused configuration.

StandardToast is used for lightweight status notifications.

## PPIQ-T018 - Storybook UI Standards

Storybook must include:
- Brand token examples.
- Button examples.
- Table examples.
- Tabs examples.
- Form field examples.
- Card, modal, and toast examples.
- Do and do-not examples.

Storybook is a standards reference, not a production route.