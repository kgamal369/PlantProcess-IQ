# Pack E-3 T-063 Historian Connector UI Report

Generated: 2026-06-06T13:15:31.590Z

Marker: PPIQ_PACK_E3_T063_HISTORIAN_UI

## Scope

Adds the frontend historian connector workspace for register/test/browse/read/map flow. The UI is intentionally honest: it exposes read-only gateway onboarding and mapping handoff without claiming a fake live vendor handshake.

## Routes

- /historian-connector
- /connectors/historian -> redirect alias

## Changed files

| File | Status |
|---|---|
| `Frontend/PlantProcess.Web/src/api/historianConnector.ts` | WRITTEN |
| `Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx` | WRITTEN |
| `Frontend/PlantProcess.Web/src/App.implementation.tsx` | PATCHED |
| `Frontend/PlantProcess.Web/src/components/AppLayout.tsx` | PATCHED |
| `Frontend/PlantProcess.Web/src/brand/plantProcessBrand.ts` | PATCHED |
| `tools/pack-e/validate-pack-e-t063-historian-ui.cjs` | WRITTEN |
| `tools/task-closure/ppiq-pack-e3-scorecard-bridge.cjs` | WRITTEN |
