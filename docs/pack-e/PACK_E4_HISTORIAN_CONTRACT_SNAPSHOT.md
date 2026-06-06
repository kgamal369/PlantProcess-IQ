# Pack E-4 Historian Connector Contract Snapshot

Generated: 2026-06-06T13:32:00.302Z

Marker: PPIQ_PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT

## Provider

- Provider type: `OpcUaHistorian`
- UI route: `/historian-connector`
- Alias route: `/connectors/historian`
- Mode: read-only historian gateway

## Backend route contract

| Route | Present |
|---|---:|
| `GET /health` | YES |
| `GET /provider` | YES |
| `POST /test-connection` | YES |
| `POST /browse-tags` | YES |
| `POST /read-window` | YES |
| `POST /mapping-hints` | YES |

## Frontend contract

| Signal | Present |
|---|---:|
| `historianConnectorApi.health` | YES |
| `historianConnectorApi.provider` | YES |
| `historianConnectorApi.testConnection` | YES |
| `historianConnectorApi.browseTags` | YES |
| `historianConnectorApi.readWindow` | YES |
| `historianConnectorApi.mappingHints` | YES |
| `HistorianConnectorPage` | YES |
| `Test connection button` | YES |
| `Browse tags button` | YES |
| `Create mapping hints button` | YES |

## Safety / honesty contract

| Signal | Present |
|---|---:|
| `read-only` | YES |
| `no fake live handshake` | YES |
| `bounded read` | YES |
| `mapping handoff` | YES |
