# PlantProcess IQ Pack A Evidence

## Pack A-1 Remaining cleanup audit + closure map

- Marker: `PPIQ_PACK_A1_REMAINING_CLEANUP_AUDIT_CLOSURE_MAP`.
- Audited remaining Pack A below-90 tasks: T-007, T-010, T-028, T-035.
- Created closure map with recommended order: T-010, T-028, T-007, T-035.
- Added closure-map validator.
- Added Pack A regression wrapper.

Generated artifacts:

- `docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.md`
- `docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.json`
- `docs/pack-a/PACK_A_CLOSURE_MAP.md`
- `docs/pack-a/PACK_A_CLOSURE_MAP.json`
- `tools/pack-a/validate-pack-a-closure-map.cjs`
- `tools/pack-a/Invoke-PackA-Regression.ps1`

## Pack A-2 Run-once tooling archive

- Marker: `PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE`.
- Task: `T-010`.
- Archived landed one-off apply/repair/continue/fix/patch tooling scripts.
- Kept reusable validators and Invoke wrappers active.
- Created archive manifest and active tooling rule.

Generated artifacts:

- `docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.md`
- `docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.json`
- `tools/_archive/landed-tooling/20260606112715/MANIFEST.md`
- `tools/_archive/landed-tooling/ARCHIVE_INDEX.md`
- `tools/pack-a/validate-pack-a-run-once-archive.cjs`

## Pack A-3 CI certification wiring

- Marker: `PPIQ_PACK_A3_CI_CERTIFICATION_WIRING`.
- Task: `T-028`.
- Added `taskClosure` gate signal to Jenkins certification stage.
- Added `routeContract` gate signal to Jenkins certification stage.
- Added machine-readable gate report writer.
- Added local and Linux certification wrappers.
- Jenkins archives `docs/ci/gate-report.json`.

Generated artifacts:

- `docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.md`
- `docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.json`
- `tools/ci/write-certification-gate-report.cjs`
- `tools/ci/Invoke-PPIQ-Certification.ps1`
- `tools/ci/ppiq-certification-stage.sh`
- `tools/pack-a/validate-pack-a-ci-certification.cjs`

## Pack A-3B Task closure evidence bridge

- Marker: PPIQ_PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE.
- Bridges Pack A-2 archive evidence into T-010 scorecard status.
- Bridges Pack A-3 CI certification evidence into T-028 scorecard status.
- Adds wrapper tools/pack-a/Invoke-PackA-ClosureGate-WithBridge.ps1.
- Adds postprocessor tools/task-closure/ppiq-pack-a-scorecard-bridge.cjs.

Generated artifacts:

- docs/pack-a/PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE_REPORT.md
- docs/pack-a/PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE_REPORT.json
- tools/task-closure/ppiq-pack-a-scorecard-bridge.cjs
- tools/pack-a/validate-pack-a-task-closure-bridge.cjs
- tools/pack-a/Invoke-PackA-ClosureGate-WithBridge.ps1

## Pack A-4 T-007 Jenkinsfile + TelemetryIngestionWorker dedup

- Marker: PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP.
- Canonical Jenkinsfile: Jenkinsfile.
- Canonical TelemetryIngestionWorker: Backend/PlantProcess.Workers/TelemetryIngestionWorker.cs.
- Archived duplicate active Jenkinsfile/Telemetry worker definitions if present.
- Added T-007 validator and Pack A4 scorecard bridge.
- Backend build must remain green.

Generated artifacts:

- docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.md
- docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.json
- tools/pack-a/validate-pack-a-t007-dedup.cjs
- tools/task-closure/ppiq-pack-a4-scorecard-bridge.cjs

## Pack A-5 T-035 Mapping and drift developer docs

- Marker: PPIQ_PACK_A5_T035_MAPPING_DRIFT_DOCS.
- Added mapping lifecycle developer guide.
- Added mapping/drift troubleshooting runbook.
- Added mapping/drift gate commands document.
- Added T-035 documentation validator.
- Added Pack A5 scorecard bridge.

Generated artifacts:

- docs/developer/MAPPING_AND_DRIFT_DEVELOPER_GUIDE.md
- docs/developer/MAPPING_DRIFT_TROUBLESHOOTING_RUNBOOK.md
- docs/developer/MAPPING_DRIFT_GATE_COMMANDS.md
- docs/pack-a/PACK_A5_T035_MAPPING_DRIFT_DOCS_REPORT.md
- docs/pack-a/PACK_A5_T035_MAPPING_DRIFT_DOCS_REPORT.json
- tools/pack-a/validate-pack-a-t035-mapping-drift-docs.cjs
- tools/task-closure/ppiq-pack-a5-scorecard-bridge.cjs
- tools/pack-a/Invoke-PackA-FinalClosure-WithBridges.ps1
