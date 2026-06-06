# Historian Connector Runbook

Marker: PPIQ_PACK_E4_T064_HISTORIAN_TESTS_DOCS_REGRESSION

## Supported scope

PlantProcess IQ currently exposes a read-only OPC-UA / historian gateway onboarding flow. The current scope is backend and UI readiness for connector configuration, connection validation, tag/point metadata browsing, bounded sample reads, and mapping hints.

It does not claim universal live connectivity to every OPC-UA server, PI Web API installation, or vendor historian without customer environment configuration.

## Demo-safe workflow

1. Open `/historian-connector`.
2. Keep `readOnly=true` behavior.
3. Use a gateway endpoint URL.
4. Run `Test connection`.
5. Run `Browse tags`.
6. Select tags.
7. Run `Read sample window`.
8. Run `Create mapping hints`.
9. Move mapping candidates into the generic mapping lifecycle.

## Troubleshooting

### Backend not reachable

- Confirm API is running.
- Confirm `/api/v5/historian-connector/health` exists.
- Confirm `app.MapV5GaHistorianConnectorEndpoints()` is in `Program.cs`.

### Provider missing

- Confirm provider catalog includes `OpcUaHistorian`.
- Confirm provider is marked available-now for read-only gateway mode.

### UI page missing

- Confirm `/historian-connector` route exists.
- Confirm `/connectors/historian` redirects to `/historian-connector`.
- Confirm AppLayout has the Historian Connector nav entry.

### Mapping hints empty

- Select tags first.
- Verify backend `/mapping-hints` route exists.
- Verify selected tags are sent from the UI API client.

## Non-negotiable honesty rule

Never describe this connector as a proven live connection to a customer historian unless a real customer gateway is configured and tested. In demos, call it a read-only historian gateway onboarding flow.
