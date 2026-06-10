# P2-T010 Table Standardization

Generated: 2026-06-10T16:32:52.2845554+03:00

Installed:

- Frontend/PlantProcess.Web/src/components/standard/StandardDataTable.tsx
- Frontend/PlantProcess.Web/src/components/SortableDataTable.tsx
- tools/ui/audit-tables.cjs
- tools/phase2/validate-p2-t010-table-standardization.cjs
- docs/phase2/P2_T010_TABLE_STANDARDIZATION.md

Acceptance:

- StandardDataTable supports loading, empty, error, caption, ariaLabel, aria-busy, aria-sort, rowKey, density, and sort handling.
- SortableDataTable delegates to StandardDataTable.
- Raw table markup is rejected outside standard wrappers.
- validate:tables is blocking.
- P2-T08 and P2-T09 validations should remain green.

Static validation:

PASSED