# Pack F-4 T-068 Edge Collector Management UX Report

Generated: 2026-06-06T14:03:16.784Z

Marker: PPIQ_PACK_F4_EDGE_COLLECTOR_UX

## Scope

Adds the frontend edge collector management workspace for registration, heartbeat, queue/spool status, outbound push status and deployment guidance.

## Routes

- /edge-collector
- /edge-agent -> redirect alias

## Changed files

| File | Status |
|---|---|
| `Frontend/PlantProcess.Web/src/api/edgeCollector.ts` | WRITTEN |
| `Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx` | WRITTEN |
| `Frontend/PlantProcess.Web/src/App.implementation.tsx` | PATCHED |
| `Frontend/PlantProcess.Web/src/components/AppLayout.tsx` | PATCHED |
| `tools/pack-f/validate-pack-f-t068-edge-collector-ux.cjs` | WRITTEN |
| `tools/task-closure/ppiq-pack-f4-scorecard-bridge.cjs` | WRITTEN |
