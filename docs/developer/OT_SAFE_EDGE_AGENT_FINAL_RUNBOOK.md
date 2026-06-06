# OT-Safe Edge Agent Final Runbook

Marker: PPIQ_PACK_F5_EDGE_TESTS_DOCS_REGRESSION

## Final demo flow

1. Open `/edge-collector`.
2. Confirm health says read-only outbound one-way push.
3. Register collector.
4. Send heartbeat.
5. Update queue/spool status.
6. Push sample batch.
7. Confirm status table shows collector, heartbeat, queue, push and safety flags.

## Deployment flow

1. Run edge-agent dry run.
2. Review generated spool file.
3. Configure outbound PlantProcess IQ URL.
4. Use approved service wrapper or Docker package.
5. Never open inbound OT firewall access as a workaround.

## Final closure result expected

After the Pack F-5 bridge, `Tasks below 90%` should be `0`.
