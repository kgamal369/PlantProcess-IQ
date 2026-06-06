# PlantProcess IQ Pack E Evidence

## Pack E-1 Historian audit and closure map

- Marker: PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP.
- Audited remaining Pack E tasks: T-060, T-063, T-064.
- Created closure map with recommended order: backend connector, UI flow, tests/docs/regression.
- Added Pack E closure-map validator.
- Added Pack E regression wrapper.

Generated artifacts:

- docs/pack-e/PACK_E1_HISTORIAN_AUDIT.md
- docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json
- docs/pack-e/PACK_E_CLOSURE_MAP.md
- docs/pack-e/PACK_E_CLOSURE_MAP.json
- tools/pack-e/validate-pack-e-closure-map.cjs
- tools/pack-e/Invoke-PackE-Regression.ps1

## Pack E-2 T-060 GA historian connector backend

- Marker: PPIQ_PACK_E2_GA_HISTORIAN_BACKEND.
- Promoted OpcUaHistorian provider catalog entry to available read-only gateway mode.
- Added OpcUaHistorianConnector infrastructure connector.
- Added DI registration for the historian connector.
- Added provider alias normalization for historian/opcua/opc-ua/piwebapi.
- Added backend routes for health, provider metadata, test-connection, browse-tags, read-window, and mapping-hints.
- Added validator and T-060 scorecard bridge.
- Backend build must remain green.

Generated artifacts:

- Backend/PlantProcess.Infrastructure/Connectors/Historian/OpcUaHistorianConnector.cs
- Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs
- docs/pack-e/PACK_E2_T060_GA_HISTORIAN_BACKEND_REPORT.md
- docs/pack-e/PACK_E2_T060_GA_HISTORIAN_BACKEND_REPORT.json
- tools/pack-e/validate-pack-e-t060-ga-historian-backend.cjs
- tools/task-closure/ppiq-pack-e2-scorecard-bridge.cjs

## Pack E-3 T-063 Historian connector UI register/test/map

- Marker: PPIQ_PACK_E3_T063_HISTORIAN_UI.
- Added historian connector frontend API client.
- Added historian connector page for configuration, test-connection, tag browsing, bounded sample read, and mapping hints.
- Added /historian-connector route and /connectors/historian alias.
- Added navigation entry in AppLayout.
- Added validator and T-063 scorecard bridge.
- Frontend build must remain green.

Generated artifacts:

- Frontend/PlantProcess.Web/src/api/historianConnector.ts
- Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx
- docs/pack-e/PACK_E3_T063_HISTORIAN_UI_REPORT.md
- docs/pack-e/PACK_E3_T063_HISTORIAN_UI_REPORT.json
- tools/pack-e/validate-pack-e-t063-historian-ui.cjs
- tools/task-closure/ppiq-pack-e3-scorecard-bridge.cjs

## Pack E-4 T-064 Historian tests docs regression

- Marker: PPIQ_PACK_E4_T064_HISTORIAN_TESTS_DOCS_REGRESSION.
- Added historian connector contract snapshot.
- Added historian connector regression guide.
- Added historian connector runbook.
- Added Pack E historian regression wrapper.
- Added T-064 validator and scorecard bridge.
- Added Pack E final closure wrapper.

Generated artifacts:

- docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.md
- docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.json
- docs/developer/HISTORIAN_CONNECTOR_REGRESSION_GUIDE.md
- docs/developer/HISTORIAN_CONNECTOR_RUNBOOK.md
- tools/pack-e/validate-pack-e-t064-historian-regression.cjs
- tools/pack-e/Invoke-PackE-HistorianRegression.ps1
- tools/pack-e/Invoke-PackE-FinalClosure.ps1
- tools/task-closure/ppiq-pack-e4-scorecard-bridge.cjs
