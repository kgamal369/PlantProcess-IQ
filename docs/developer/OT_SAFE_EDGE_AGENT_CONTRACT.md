# OT-Safe Edge Agent Contract

Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND

## Safety promise

The PlantProcess IQ edge collector is a read-only, outbound-only acquisition pattern. It must not require inbound access into the OT network and must not write to PLC, SCADA, MES, historian, database, or file-drop source systems.

## Required invariants

- Read-only collection from configured source profiles.
- No inbound OT listener.
- No command/control path to production assets.
- Outbound-only push to PlantProcess IQ.
- Bounded local queue/spool behavior during outage.
- Batch size limits.
- Heartbeat and queue status telemetry.
- Secrets referenced by configuration, never hardcoded.

## Backend API contract

- GET `/api/v5/edge-collector/health`
- GET `/api/v5/edge-collector/contract`
- GET `/api/v5/edge-collector/profiles`
- POST `/api/v5/edge-collector/register`
- POST `/api/v5/edge-collector/heartbeat`
- POST `/api/v5/edge-collector/push-batch`
- POST `/api/v5/edge-collector/queue-status`
- GET `/api/v5/edge-collector/status`
