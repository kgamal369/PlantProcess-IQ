# Phase 3 Dead Code Removal — 30 May 2026

## Deleted duplicate / orphan files

| File | Reason |
|---|---|
| src/components/hardening/StandardButton.tsx | Duplicate non-canonical StandardButton implementation. |
| src/hardening/StandardButton.tsx | Duplicate non-canonical StandardButton implementation. |
| src/components/hardening/DataFetchBoundary.tsx | Duplicate non-canonical DataFetchBoundary implementation. |
| src/hardening/DataFetchBoundary.tsx | Duplicate non-canonical DataFetchBoundary implementation. |
| src/components/table/StandardTable.tsx | Duplicate table implementation. Canonical table is src/components/standard/StandardTable.tsx. |
| src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx | Orphan duplicate page path. Router uses src/pages/MaterialInvestigationPage.tsx. |
| src/components/ErrorBoundary.tsx | Moved to canonical src/components/standard/ErrorBoundary.tsx. |
| src/components/hardening/AppErrorBoundary.tsx | Legacy hardening wrapper replaced by canonical ErrorBoundary. |
| src/hardening/RouteErrorBoundary.tsx | Legacy route wrapper replaced by canonical ErrorBoundary. |

## Keep / wire note

Known workflow components that belong to later Phase 7 work must be tracked, not blindly deleted:

- Phase1WorkflowTruthPanel
- SaveInspectionJobModal
- OperationProgressPanel
