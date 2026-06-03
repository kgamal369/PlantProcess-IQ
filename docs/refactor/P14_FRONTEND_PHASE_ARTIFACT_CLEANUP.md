# PlantProcess IQ — P14 Frontend Phase Artifact Cleanup

## Purpose

Pack C2 removes phase-named frontend source paths and old phase import identifiers from product code.

## Closed in C2

- Product-named frontend folders replace phase-named page folders.
- `Phase1WorkflowTruthPanel` becomes `WorkflowTruthPanel`.
- `components/phase2` becomes `components/inspection`.
- Root duplicate `components/dashboard/WidgetBuilderWizard.tsx` is retired to a small bridge if it still exists as a large monolith.
- The validator fails on phase-named frontend paths/import identifiers.
- Content/comment/string findings are recorded for C4.

## Not closed in C2

The following are intentionally not split in this pack:

- `AdminDbConfigurationTab.tsx`
- `AdminSchemaConfigurationTab.tsx`
- `WidgetBuilderWizardContent.tsx`
- Backend endpoint god-files

These require characterization and controlled component extraction in C3/C4.