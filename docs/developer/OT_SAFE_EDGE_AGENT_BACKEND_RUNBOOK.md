# OT-Safe Edge Agent Backend Runbook

Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND

## Startup check

Call `/api/v5/edge-collector/health`. The response must state:

- `mode = read-only-outbound-one-way-push`
- `noInboundOtAccessRequired = true`
- `opensInboundListener = false`

## Registration

Register collectors only with:

- `readOnlyCollection = true`
- `outboundOnly = true`
- `opensInboundListener = false`

## Batch push

Batches must be bounded, outbound-only, and read-only. The backend rejects missing samples, oversized batches, and any batch that does not declare the safety flags.

## Troubleshooting

- If register fails, check the three safety booleans first.
- If batch push fails, check sample count and required TagPath/TimestampUtc fields.
- If queue status is high, inspect the edge local spool and outbound connectivity.
- Do not open inbound firewall rules from PlantProcess IQ into OT to “fix” connectivity.
