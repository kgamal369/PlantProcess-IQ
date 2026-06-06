# PlantProcess IQ Pack F Evidence

## Pack F-1 OT-safe edge agent audit and closure map

- Marker: PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP.
- Audited remaining Pack F tasks: T-066, T-067, T-068, T-071.
- Created closure map with recommended order: edge one-way backend, packaging/deployment, management UX, tests/docs/regression.
- Added Pack F closure-map validator.
- Added Pack F regression wrapper.

Generated artifacts:

- docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.md
- docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json
- docs/pack-f/PACK_F_CLOSURE_MAP.md
- docs/pack-f/PACK_F_CLOSURE_MAP.json
- tools/pack-f/validate-pack-f-closure-map.cjs
- tools/pack-f/Invoke-PackF-Regression.ps1

## Pack F-2 T-066 OT-safe edge agent one-way push backend

- Marker: PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND.
- Added OT-safe edge collector backend endpoints.
- Added worker-side edge agent contract model.
- Added register, heartbeat, push-batch, queue-status, status, contract and profile routes.
- Added safety documentation for read-only / outbound-only behavior.
- Added validator and T-066 scorecard bridge.
- Backend build must remain green.

Generated artifacts:

- Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs
- Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs
- docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md
- docs/developer/OT_SAFE_EDGE_AGENT_BACKEND_RUNBOOK.md
- docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.md
- docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.json
- tools/pack-f/validate-pack-f-t066-edge-backend.cjs
- tools/task-closure/ppiq-pack-f2-scorecard-bridge.cjs

## Pack F-3 T-067 Edge agent packaging and deployment

- Marker: PPIQ_PACK_F3_EDGE_AGENT_PACKAGING.
- Added reference edge-agent script with dry-run and push mode.
- Added sample config with read-only / outbound-only / no-inbound-listener safety flags.
- Added local run script.
- Added install/uninstall service-wrapper guidance scripts.
- Added Dockerfile and docker-compose template.
- Added environment template and deployment documentation.
- Added validator and T-067 scorecard bridge.

Generated artifacts:

- tools/edge-agent/ppiq-edge-agent.cjs
- tools/edge-agent/edge-agent.sample.json
- tools/edge-agent/package-manifest.json
- scripts/edge-agent/Run-PPIQ-EdgeAgent-Local.ps1
- scripts/edge-agent/Install-PPIQ-EdgeAgent-Service.ps1
- scripts/edge-agent/Uninstall-PPIQ-EdgeAgent-Service.ps1
- deploy/edge-agent/Dockerfile
- deploy/edge-agent/docker-compose.edge-agent.yml
- deploy/edge-agent/.env.edge-agent.template
- deploy/edge-agent/README_EDGE_AGENT_DEPLOYMENT.md
- docs/developer/OT_SAFE_EDGE_AGENT_DEPLOYMENT_GUIDE.md
- tools/pack-f/validate-pack-f-t067-edge-packaging.cjs
- tools/task-closure/ppiq-pack-f3-scorecard-bridge.cjs

## Pack F-4 T-068 Edge collector management UX

- Marker: PPIQ_PACK_F4_EDGE_COLLECTOR_UX.
- Added edge collector frontend API client.
- Added edge collector management page.
- Added /edge-collector route and /edge-agent alias.
- Added navigation entry in AppLayout.
- UI covers registration, heartbeat, queue status, outbound sample push and deployment guidance.
- Added validator and T-068 scorecard bridge.
- Frontend build must remain green.

Generated artifacts:

- Frontend/PlantProcess.Web/src/api/edgeCollector.ts
- Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx
- docs/pack-f/PACK_F4_T068_EDGE_COLLECTOR_UX_REPORT.md
- docs/pack-f/PACK_F4_T068_EDGE_COLLECTOR_UX_REPORT.json
- tools/pack-f/validate-pack-f-t068-edge-collector-ux.cjs
- tools/task-closure/ppiq-pack-f4-scorecard-bridge.cjs

## Pack F-5 T-071 Edge tests docs regression and final closure

- Marker: PPIQ_PACK_F5_EDGE_TESTS_DOCS_REGRESSION.
- Added Pack F final contract snapshot.
- Added OT-safe edge agent regression guide.
- Added final runbook.
- Added final acceptance report.
- Added final regression wrapper.
- Added final closure wrapper.
- Added T-071 scorecard bridge.
- Backend and frontend builds must remain green.

Generated artifacts:

- docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.md
- docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.json
- docs/developer/OT_SAFE_EDGE_AGENT_REGRESSION_GUIDE.md
- docs/developer/OT_SAFE_EDGE_AGENT_FINAL_RUNBOOK.md
- docs/pack-f/PACK_F_FINAL_ACCEPTANCE.md
- tools/pack-f/validate-pack-f-t071-edge-regression.cjs
- tools/pack-f/Invoke-PackF-FinalRegression.ps1
- tools/pack-f/Invoke-PackF-FinalClosure.ps1
- tools/task-closure/ppiq-pack-f5-scorecard-bridge.cjs
