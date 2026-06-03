# PlantProcess IQ — P07 Connector Runtime Certification

## What this pack proves

- A deterministic read-only historian-style connector exists for local runtime certification.
- The connector exposes no write methods.
- Time-bounded tag reads return deterministic runtime data.
- Historical backfill has checkpoint/resume evidence.
- Connector truth state reports reachable, stale, lagging, offline, certified-proof, and last-successful-read.
- The proof is honest: this is a mock historian runtime certification connector, not a customer-certified OPC-UA/Aspen/PI adapter.

## Main endpoints

- `GET /api/v5/connectors/runtime-certification/health`
- `POST /api/v5/connectors/runtime-certification/mock-historian/register-source`
- `POST /api/v5/connectors/runtime-certification/mock-historian/read-window`
- `POST /api/v5/connectors/runtime-certification/backfill/run`
- `POST /api/v5/connectors/runtime-certification/backfill/resume-proof/{runId}`
- `POST /api/v5/connectors/runtime-certification/truth-state`
- `GET /api/v5/connectors/runtime-certification/truth-state/{providerCode?}`

## Certification boundary

This pack closes the product runtime proof for P07. Customer-specific certification still requires testing against the actual plant connector technology and network environment.