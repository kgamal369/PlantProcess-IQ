# P2-T011 — Tabs / Segmented Controls / Navigation Inventory

Marker: PPIQ_P2_T011_TABS_NAVIGATION_STANDARDIZATION

## Goal

P2-T011 creates the canonical StandardTabs readiness contract and produces the required tabs/navigation inventory.

Scope:

- Primary navigation
- In-page tabs
- Segmented controls
- Sub-navigation
- Breadcrumbs

Inventory output:

- `Frontend/PlantProcess.Web/docs/ui-standards/tabs-inventory.csv`

Captured fields:

- file
- page
- current implementation
- item count
- navigation type
- active indicator style
- badge support
- keyboard navigation
- lazy loading
- responsive behavior
- standard/non-standard status

## Canonical component

`StandardTabs` supports:

- horizontal and vertical orientation
- tabs / segmented / pills / breadcrumb variants
- icons and badges
- disabled tabs
- keyboard navigation: arrows, Home, End, Enter, Space
- active-only or all-panel mounting
- optional URL search-param sync

## Validation

Run:

    node tools/phase2/validate-p2-t011-tabs-navigation.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:tabs
    npm run validate:tables
    npm run validate:standard-imports
    npm run validate:action-buttons
    npm run build