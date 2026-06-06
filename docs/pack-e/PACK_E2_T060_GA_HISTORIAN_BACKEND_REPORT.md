# Pack E-2 T-060 GA Historian Backend Report

Generated: 2026-06-06T13:02:09.481Z

Marker: PPIQ_PACK_E2_GA_HISTORIAN_BACKEND

## Scope

This step promotes the backend historian connector to a GA-ready gateway-mode backend surface. It is intentionally honest: it supports configuration validation, tag browse metadata, bounded sample reads and mapping hints, while live vendor handshake remains customer-environment specific.

## Backend routes

- GET /api/v5/historian-connector/health
- GET /api/v5/historian-connector/provider
- POST /api/v5/historian-connector/test-connection
- POST /api/v5/historian-connector/browse-tags
- POST /api/v5/historian-connector/read-window
- POST /api/v5/historian-connector/mapping-hints

## Changed files

| File | Status |
|---|---|
| `Backend/PlantProcess.Infrastructure/Connectors/Historian/OpcUaHistorianConnector.cs` | WRITTEN |
| `Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs` | WRITTEN |
| `Backend/PlantProcess.Application/Integration/Connectors/ConnectorProviderCatalog.cs` | PATCHED |
| `Backend/PlantProcess.Infrastructure/Connectors/Common/DataSourceConnectorFactory.cs` | PATCHED |
| `Backend/PlantProcess.Infrastructure/DependencyInjection.cs` | PATCHED |
| `Backend/PlantProcess.Api/Program.cs` | PATCHED |
| `tools/pack-e/validate-pack-e-t060-ga-historian-backend.cjs` | WRITTEN |
| `tools/task-closure/ppiq-pack-e2-scorecard-bridge.cjs` | WRITTEN |
