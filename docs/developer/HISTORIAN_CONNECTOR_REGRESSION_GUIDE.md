# Historian Connector Regression Guide

Marker: PPIQ_PACK_E4_T064_HISTORIAN_TESTS_DOCS_REGRESSION

## Purpose

This guide locks the Pack E historian connector behavior after the backend and frontend implementation. It protects the product from three regressions: overclaiming live vendor connectivity, breaking the read-only historian gateway contract, and disconnecting the UI from the backend mapping handoff.

## Regression layers

1. Backend validator: verifies provider catalog, aliases, dependency injection, endpoint registration, and route files.
2. Frontend validator: verifies the UI route, API client, connector page, navigation entry, and mapping actions.
3. Contract snapshot: verifies backend route signals, frontend route/API signals, and honesty/safety wording.
4. Build regression: runs backend build and frontend build.
5. Scorecard bridge: marks T-064 green only when Pack E-2, Pack E-3, and Pack E-4 validators are green.

## Commands

```powershell
node .\tools\pack-e\validate-pack-e-t060-ga-historian-backend.cjs
node .\tools\pack-e\validate-pack-e-t063-historian-ui.cjs
node .\tools\pack-e\validate-pack-e-t064-historian-regression.cjs
powershell -ExecutionPolicy Bypass -File .\tools\pack-e\Invoke-PackE-HistorianRegression.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunBuilds
```

## Acceptance

- Provider type remains `OpcUaHistorian`.
- Backend routes remain under `/api/v5/historian-connector`.
- UI route remains `/historian-connector`.
- Alias route remains `/connectors/historian`.
- Connector remains read-only.
- Live vendor handshake remains environment-specific and is not faked.
- Tag browsing, bounded read, and mapping hints stay connected.
