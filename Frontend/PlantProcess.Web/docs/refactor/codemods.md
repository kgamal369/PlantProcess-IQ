# PlantProcess IQ Codemods

## scripts/codemods/standardize-imports.cjs

Purpose: keeps Phase 3 canonical UI imports stable.

It rewrites legacy imports from:

- src/components/hardening/*
- src/hardening/*
- src/components/table/StandardTable
- src/components/ErrorBoundary

to the canonical locations:

- src/components/standard/StandardButton
- src/components/standard/DataFetchBoundary
- src/components/standard/ErrorBoundary
- src/components/standard/StandardTable

Run from Frontend/PlantProcess.Web:

```powershell
node scripts/codemods/standardize-imports.cjs
```
