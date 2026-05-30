
# PlantProcess IQ UI Standards - Full Component Specification

## Product guard

These components are generic manufacturing-quality intelligence primitives. They must not hard-code steel-only concepts.

## PPIQ-T013 - StandardButton

Single application button component.

- Variants: primary, secondary, ghost, danger, success.
- Sizes: sm, md, lg.
- Supports icon-only mode.
- Supports loading and disabled states.
- Supports button and anchor rendering through as="button" and as="a".
- Loading uses aria-busy and disables interaction.

## PPIQ-T014 - StandardTable

Single application table component.

- Sorting, multi-column by shift-click.
- Pagination and page-size selector.
- Client filter.
- Server mode query callback.
- Row selection: none, single, multi.
- Density: compact, comfortable, spacious.
- Column visibility.
- CSV export.
- Sticky header.
- Empty, loading, error, retry states.
- Virtualization-safe public props.

## PPIQ-T015 - StandardTabs

- Horizontal and vertical modes.
- Icons, badges, disabled tabs.
- Lazy mounting.
- Keyboard support: arrows, Home, End, Enter, Space.
- Optional URL search-param sync.

## PPIQ-T016 - Standard fields

- StandardInput, StandardSelect, StandardTextArea.
- Required marker, helper text, error text.
- Search input clear button.
- Select supports searchable and multi-select mode.

## PPIQ-T017 - Standard surfaces

- StandardCard with flat, raised, floating elevation.
- StandardModal with focus trap, Escape close, focus return, dirty-state click-outside protection.
- StandardToastProvider with stack limit, variants, manual dismiss.

## PPIQ-T018 - Storybook

Storybook contains:
- Component stories.
- Design token page.
- Do / do-not page.
- Contributor onboarding page.
