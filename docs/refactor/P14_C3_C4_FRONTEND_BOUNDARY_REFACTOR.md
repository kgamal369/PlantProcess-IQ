# PlantProcess IQ — P14 C3/C4 Frontend Boundary Refactor

## Purpose

C3/C4 creates stable frontend architecture boundaries before deep component extraction.

## Closed by this pack

- Large public files are converted into small facade files.
- Original implementation bodies move to same-folder `.implementation.ts` / `.implementation.tsx` files.
- Existing imports remain stable.
- Implementation files are not imported directly by unrelated modules.
- Legacy `plantProcessApi` naming remains blocked.
- Phase-named frontend paths/imports remain blocked.
- A C4 deep extraction backlog is generated.

## Not closed by this pack

This pack does not rewrite internal business logic. It prepares safe boundaries first.

Deep extraction from implementation files should happen later in focused packs after PPIQ-T001 to PPIQ-T009 hardening is complete.

## Evidence

- `Documentation/v5/pack-c3-c4-frontend-boundary-refactor-report.json`
- `Documentation/v5/pack-c4-deep-extraction-backlog.json`
- `Documentation/v5/pack-c3-c4-large-file-inventory.json`
- `Documentation/v5/pack-c3-c4-frontend-boundary-validation-report.json`