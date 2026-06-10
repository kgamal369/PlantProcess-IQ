# P2-T010 — Table Standardization

Marker: PPIQ_P2_T010_TABLE_STANDARDIZATION

## Goal

P2-T010 closes the table standardization contract after P2-T08 and P2-T09.

It establishes:

- Canonical `StandardDataTable`.
- Compatibility `SortableDataTable` delegating to `StandardDataTable`.
- No raw table markup outside standard wrappers.
- No inline table alignment styles in SortableDataTable.
- Loading, empty, and error row states.
- Caption, aria-label, aria-busy, aria-sort, rowKey, density, and sort semantics.
- Blocking `npm run validate:tables`.

## Validation

Run:

    node tools/phase2/validate-p2-t010-table-standardization.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:tables
    npm run validate:standard-imports
    npm run validate:action-buttons
    npm run build