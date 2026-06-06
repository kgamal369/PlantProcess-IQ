# Pack A-1 Remaining Cleanup Audit + Closure Map

Generated: 2026-06-06T11:24:27.157Z

## Executive Summary

Pack A has four remaining below-90 tasks. The fastest safe route is: archive landed one-off scripts, wire CI certification, de-duplicate Jenkins/Telemetry surfaces, then close mapping/drift developer documentation.

## Remaining Pack A Tasks

| Task | Score | Status | Recommended Step | Objective |
|---|---:|---|---|---|
| T-007 | 75% | MOSTLY DONE | Pack A-4 | Remove or neutralize duplicate CI/worker definitions so Jenkinsfile and TelemetryIngestionWorker have one clear canonical implementation each. |
| T-010 | 0% | NOT YET STARTED | Pack A-2 | Move landed one-time apply/repair/continue scripts out of active tooling into an archive index so the active tools folder contains reusable validators and commands only. |
| T-028 | 80% | MOSTLY DONE | Pack A-3 | Ensure CI certification calls the core gate-exit validators: backend build/tests, frontend build, task closure, Pack B/Pack D gates, Phase 5/6 validation, and route-contract validation. |
| T-035 | 25% | NOT YET STARTED | Pack A-5 | Create developer documentation explaining mapping lifecycle, drift detection, validation gates, schema dictionary, safe SQL, and how to debug mapping/drift failures. |

## T-007 Audit — Jenkinsfile + TelemetryIngestionWorker

- Jenkinsfile-like files: **2**
- Telemetry worker class definitions: **171**
- Telemetry hosted-service registrations: **0**
- Duplicate risk: **HIGH**

### Jenkinsfile-like files

| File | Lines |
|---|---:|
| `Jenkinsfile` | 275 |
| `tools/archive/deduplicated-build-files/Jenkinsfile.deploy-ci.archived` | 275 |

### Telemetry worker references

| File | Lines |
|---|---:|
| `Backend/database/seed/005_phase0_job_definitions.sql` | 146 |
| `Backend/PlantProcess.Application/Integration/Services/Jobs/JobDefinitionService.cs` | 338 |
| `Backend/PlantProcess.Infrastructure/TimeSeries/TelemetryIngestionWorker.cs` | 256 |
| `Backend/PlantProcess.Workers/TelemetryIngestionWorker.cs` | 123 |
| `docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json` | 1013 |
| `Documentation/GeminiExport_12May2026_1916/PlantProcessIQ_Audit_2_BackendInfra.txt` | 10439 |
| `Documentation/GeminiExport_14May2026_2100/PlantProcessIQ_Audit_2_BackendInfra.txt` | 10441 |
| `Documentation/GeminiExport_16May2026_0921/PlantProcessIQ_Audit_2_BackendInfra.txt` | 10659 |
| `Documentation/GeminiExport_16May2026_0956/PlantProcessIQ_Audit_2_BackendInfra.txt` | 10911 |
| `Documentation/GeminiExport_16May2026_1028/PlantProcessIQ_Audit_2_BackendInfra.txt` | 11556 |
| `Documentation/GeminiExport_16May2026_1158/PlantProcessIQ_Audit_2_BackendInfra.txt` | 12557 |
| `Documentation/GeminiExport_16May2026_1349/PlantProcessIQ_Audit_1_BackendCore.txt` | 17421 |
| `Documentation/GeminiExport_16May2026_1349/PlantProcessIQ_Audit_2_BackendInfra.txt` | 12575 |
| `Documentation/GeminiExport_16May2026_1349/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_16May2026_1556/PlantProcessIQ_Audit_1_BackendCore.txt` | 18377 |
| `Documentation/GeminiExport_16May2026_1556/PlantProcessIQ_Audit_2_BackendInfra.txt` | 13053 |
| `Documentation/GeminiExport_16May2026_1556/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_1221/PlantProcessIQ_Audit_1_BackendCore.txt` | 18658 |
| `Documentation/GeminiExport_18May2026_1221/PlantProcessIQ_Audit_2_BackendInfra.txt` | 13219 |
| `Documentation/GeminiExport_18May2026_1221/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_1307/PlantProcessIQ_Audit_1_BackendCore.txt` | 19070 |
| `Documentation/GeminiExport_18May2026_1307/PlantProcessIQ_Audit_2_BackendInfra.txt` | 13266 |
| `Documentation/GeminiExport_18May2026_1307/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_1334/PlantProcessIQ_Audit_1_BackendCore.txt` | 19161 |
| `Documentation/GeminiExport_18May2026_1334/PlantProcessIQ_Audit_2_BackendInfra.txt` | 13312 |
| `Documentation/GeminiExport_18May2026_1334/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_1349/PlantProcessIQ_Audit_1_BackendCore.txt` | 19161 |
| `Documentation/GeminiExport_18May2026_1349/PlantProcessIQ_Audit_2_BackendInfra.txt` | 13312 |
| `Documentation/GeminiExport_18May2026_1349/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_1419/PlantProcessIQ_Audit_1_BackendCore.txt` | 19344 |
| `Documentation/GeminiExport_18May2026_1419/PlantProcessIQ_Audit_2_BackendInfra.txt` | 13379 |
| `Documentation/GeminiExport_18May2026_1419/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_2227/PlantProcessIQ_Audit_1_BackendCore.txt` | 19508 |
| `Documentation/GeminiExport_18May2026_2227/PlantProcessIQ_Audit_2_BackendInfra.txt` | 14662 |
| `Documentation/GeminiExport_18May2026_2227/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_18May2026_2339/PlantProcessIQ_Audit_1_BackendCore.txt` | 19508 |
| `Documentation/GeminiExport_18May2026_2339/PlantProcessIQ_Audit_2_BackendInfra.txt` | 14713 |
| `Documentation/GeminiExport_18May2026_2339/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_19May2026_1201/PlantProcessIQ_Audit_1_BackendCore.txt` | 19819 |
| `Documentation/GeminiExport_19May2026_1201/PlantProcessIQ_Audit_2_BackendInfra.txt` | 15320 |
| `Documentation/GeminiExport_19May2026_1201/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/GeminiExport_19May2026_1224/PlantProcessIQ_Audit_1_BackendCore.txt` | 20793 |
| `Documentation/GeminiExport_19May2026_1224/PlantProcessIQ_Audit_2_BackendInfra.txt` | 15774 |
| `Documentation/GeminiExport_19May2026_1224/PlantProcessIQ_Audit_4_Database.txt` | 4575 |
| `Documentation/PlantProcessIQ_11May2026_1109.txt` | 28896 |
| `Documentation/PlantProcessIQ_11May2026_1224.txt` | 36675 |
| `Documentation/PlantProcessIQ_11May2026_1452.txt` | 41908 |
| `Documentation/PlantProcessIQ_11May2026_1521.txt` | 42109 |
| `Documentation/PlantProcessIQ_11May2026_1551.txt` | 47742 |
| `Documentation/PlantProcessIQ_FullStack_11May2026_1616.txt` | 49017 |

## T-010 Audit — Run-once tooling archive

- Active archive candidates: **28**
- Existing archive signals: **8**

| Candidate | Lines | Type |
|---|---:|---|
| `tools/ground-truth/apply-ground-truth-validation-hotfix-pack.cjs` | 1064 | cjs |
| `tools/ground-truth/repair-ground-truth-p03p04-and-godfile-policy.cjs` | 762 | cjs |
| `tools/ml/Apply-PPIQ-v5-T209-T213.ps1` | 1497 | ps1 |
| `tools/pack-a/apply-pack-a1-remaining-cleanup-audit-closure-map.cjs` | 941 | cjs |
| `tools/pack-b/apply-pack-b1-safe-frontend-refactor.cjs` | 731 | cjs |
| `tools/pack-b/apply-pack-b2-widget-builder-content-split-v2.cjs` | 651 | cjs |
| `tools/pack-b/apply-pack-b2-widget-builder-content-split.cjs` | 617 | cjs |
| `tools/pack-b/apply-pack-b3-widget-builder-shell-retirement.cjs` | 184 | cjs |
| `tools/pack-b/apply-pack-b4a-t037-compatibility-split.cjs` | 299 | cjs |
| `tools/pack-b/repair-pack-b1-css-safe-split.cjs` | 390 | cjs |
| `tools/pack-b/repair-pack-b2-generated-split-compile.cjs` | 344 | cjs |
| `tools/pack-b/repair-pack-b2-import-syntax.cjs` | 209 | cjs |
| `tools/pack-d/apply-pack-d1-backend-split-audit-route-snapshot.cjs` | 732 | cjs |
| `tools/pack-d/apply-pack-d2a-t054-route-preserving-split.cjs` | 314 | cjs |
| `tools/pack-d/apply-pack-d3a-t055-route-service-preserving-split.cjs` | 345 | cjs |
| `tools/pack-d/repair-pack-d1-target-paths.cjs` | 274 | cjs |
| `tools/phase3-phase4/repair-phase34-validator-frontend-path-policy.cjs` | 219 | cjs |
| `tools/phase3-phase4/repair-phase34-validator-marker-policy.cjs` | 197 | cjs |
| `tools/phase56/apply-phase5-phase6.cjs` | 787 | cjs |
| `tools/phase78/apply-phase7-phase8-v2.cjs` | 588 | cjs |
| `tools/phase9-phase10/apply-phase9-phase10.cjs` | 505 | cjs |
| `tools/phase9-phase10/repair-phase10-demo-lead-marker.cjs` | 146 | cjs |
| `tools/phase9-phase10/repair-phase10-website-overclaims.cjs` | 140 | cjs |
| `tools/phase9-phase10/repair-phase9-phase10-validator-v2.cjs` | 230 | cjs |
| `tools/phase9-phase10/repair-phase9-phase10-validator.cjs` | 277 | cjs |
| `tools/task-closure/apply-task-closure-gate-t001-t071.cjs` | 815 | cjs |
| `tools/tests/fix-api-integration-test-db-connection.cjs` | 308 | cjs |
| `tools/tests/patch-api-test-local-db-resolver.cjs` | 61 | cjs |

## T-028 Audit — CI certification wiring

| Signal | Found | Files |
|---|---|---|
| dotnet build | YES | `tools/ci/run-local-full-validation.ps1` |
| dotnet test | YES | `tools/ci/run-local-full-validation.ps1` |
| npm run build | YES | `tools/ci/run-local-full-validation.ps1` |
| Phase 5/6 validation | YES | `Jenkinsfile`<br>`tools/archive/deduplicated-build-files/Jenkinsfile.deploy-ci.archived` |
| T001-T071 task closure | **NO** |  |
| Pack B validation | YES | `Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql`<br>`Backend/database/scripts/670_pack_b4_connector_runtime_certification.sql`<br>`Backend/database/scripts/680_pack_b5_private_model_gateway_ciso_controls.sql`<br>`Backend/PlantProcess.Api/PlantConnectors/V5ConnectorRuntimeCertificationEndpoints.cs` |
| Pack D validation | YES | `docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md` |
| Route contract validation | **NO** |  |

## T-035 Audit — Mapping and drift developer docs

| Topic | Found | Files |
|---|---|---|
| mapping lifecycle | YES | `docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json`<br>`docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md`<br>`docs/modules/PRODUCT_MODULE_BOUNDARIES.md`<br>`docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json` |
| drift detection | YES | `docs/acquisition/P05_OT_SAFE_ACQUISITION.md`<br>`docs/closure/PACK_B35_MOSTLY_GREEN_TASK_CLOSURE.md`<br>`docs/phase3-phase4/PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md`<br>`docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json`<br>`Documentation/v5/P07_P08_Connectors_Identity_Runbook.md` |
| business-key dictionary | YES | `.ground_truth_backup/repair_phase34_frontend_path_policy_20260606092150/docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md`<br>`docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md`<br>`docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json`<br>`docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md`<br>`docs/modules/PRODUCT_MODULE_BOUNDARIES.md`<br>`docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`<br>`docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`<br>`docs/phase8/openapi-source-contract-snapshot.json`<br>`Documentation/v5/P05_P06_VisualMapper_BlendedProvenance_Runbook.md` |
| safe SQL | YES | `docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json`<br>`docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md`<br>`docs/modules/PRODUCT_MODULE_BOUNDARIES.md`<br>`docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`<br>`docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`<br>`docs/phase8/openapi-source-contract-snapshot.json`<br>`docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json` |
| validation gates | YES | `.ground_truth_backup/20260606090928/docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md`<br>`.ground_truth_backup/repair_p03p04_policy_20260606091632/docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md`<br>`.ground_truth_backup/repair_p03p04_policy_20260606091632/docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md`<br>`.ground_truth_backup/repair_phase34_frontend_path_policy_20260606092150/docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md`<br>`.ground_truth_backup/repair_phase34_marker_policy_20260606092001/docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md`<br>`.phase9_phase10_backup/demo_lead_marker_20260606085649/docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md`<br>`.phase9_phase10_backup/validator_repair_20260606085429/docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md`<br>`.phase9_phase10_backup/website_overclaim_repair_20260606085743/docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md`<br>`Backend/database/database.apply-order.manifest.md`<br>`docs/acquisition/P05_OT_SAFE_ACQUISITION.md`<br>`docs/ai/P04_PRIVATE_MODEL_GATEWAY_CISO_CONTROLS.md`<br>`docs/assistant/P04_PRODUCTION_ASSISTANT.md`<br>`docs/closure/P13_CUSTOMER_ACCEPTANCE_REPORT_TEMPLATE.md`<br>`docs/closure/PACK_B35_MOSTLY_GREEN_TASK_CLOSURE.md`<br>`docs/connectors/P07_CONNECTOR_RUNTIME_CERTIFICATION.md`<br>`docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md`<br>`docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json`<br>`docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md`<br>`docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md`<br>`docs/pack-b/PACK_B4A_T037_COMPATIBILITY_SPLIT_REPORT.json`<br>`docs/pack-b/PACK_B_IMPLEMENTATION_EVIDENCE.md`<br>`docs/pack-b/PACK_B_SPLIT_PLAN.json`<br>`docs/pack-b/PACK_B_SPLIT_PLAN.md`<br>`docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.json`<br>`docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.md`<br>`docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`<br>`docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`<br>`docs/pack-d/PACK_D1_SERVICE_SURFACE_SNAPSHOT.json`<br>`docs/pack-d/PACK_D1_TARGET_PATH_CORRECTION.json`<br>`docs/pack-d/PACK_D_IMPLEMENTATION_EVIDENCE.md`<br>`docs/page-builder/widget-script-grammar.md`<br>`docs/phase1-phase2/PHASE1_PHASE2_IMPLEMENTATION_EVIDENCE.md`<br>`docs/phase3-phase4/PHASE3_PHASE4_IMPLEMENTATION_EVIDENCE.md`<br>`docs/phase5-phase6/PHASE5_PHASE6_IMPLEMENTATION_EVIDENCE.md`<br>`docs/phase5b/phase5b-refactor-report.md`<br>`docs/phase7-phase8/PHASE7_PHASE8_IMPLEMENTATION_EVIDENCE.md`<br>`docs/phase8/backend-api-hygiene-report.json`<br>`docs/phase8/openapi-source-contract-snapshot.json`<br>`docs/phase9-phase10/phase9-phase10-evidence-ledger.json`<br>`docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md`<br>`docs/README.md`<br>`docs/refactor/P14_FRONTEND_PHASE_ARTIFACT_CLEANUP.md`<br>`docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json`<br>`docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.md`<br>`docs/testing/P00A_77_Implementation_Backlog.md`<br>`docs/testing/P00D_E2E_Consolidation_Map.json`<br>`docs/testing/PlantProcessIQ_Test_Register_v1.md`<br>`docs/ux/ACCESSIBILITY.md`<br>`Documentation/config/Final_Env_Profile_Standardization.md`<br>`Documentation/deployment/Deployment_DryRun_Report.md`<br>`Documentation/deployment/Deployment_Standardization_Final_Report.md`<br>`Documentation/deployment/Deployment_Standardization_Report.md`<br>`Documentation/deployment/Server_App_Compose_Report.md`<br>`Documentation/deployment/Server_Command_Standardization_Report.md`<br>`Documentation/deployment/Server_Deployment_Manifest_Report.md`<br>`Documentation/docs/PlantProcessIQ_Analysis_and_Validation_30May2026.md`<br>`Documentation/hygiene/Pack1A_FinalCleanRepoGate_20260604_125229.md`<br>`Documentation/hygiene/Repo_Cleanup_Final_Report.md`<br>`Documentation/hygiene/S2A_Repo_Cleanup_DryRun_20260604_105338.md`<br>`Documentation/hygiene/S2B_Batch4C_ValidationToTestBacklog_20260604_121109.md`<br>`Documentation/hygiene/S2B_Batch5A_DatabaseStructureReport_20260604_121948.md`<br>`Documentation/refactor/Analytics_Kpi_SqlBurnDown_Report.md`<br>`Documentation/refactor/Api_RawSql_Quarantine_Contract.md`<br>`Documentation/refactor/Api_RawSql_Quarantine_Report.md`<br>`Documentation/refactor/Canonical_Integration_SqlBurnDown_Report.md`<br>`Documentation/refactor/Genericness_Refactor_Baseline_Report.md`<br>`Documentation/refactor/RefactorPhaseAcceptanceChecklist.md`<br>`Documentation/refactor/RefactorPhaseR2_AcceptanceChecklist.md`<br>`Documentation/testing/E2E_Stabilization_Plan.md`<br>`Documentation/testing/S3B_E2EStabilizationPlan_20260604_123306.md`<br>`Documentation/testing/Validation_To_Test_Backlog.md`<br>`Documentation/v5/P03_P04_TimeSeries_AssistantGateway_Runbook.md`<br>`Documentation/v5/P05_P06_VisualMapper_BlendedProvenance_Runbook.md`<br>`Frontend/PlantProcess.Web/docs/visual-regression/phase56-baseline.md`<br>`Frontend/PlantProcess.Web/README.frontend-implementation.md` |
| developer workflow/runbook | YES | `Backend/tools/phase0/README.md`<br>`deploy/dr/RPO_RTO_RUNBOOK.md`<br>`deploy/server/SERVER_DRY_RUN.md`<br>`deploy/server/SERVER_RUNBOOK.md`<br>`docs/escrow/SOURCE_CODE_ESCROW_PROCESS.md`<br>`docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json`<br>`Documentation/deployment/Deployment_DryRun_Report.md`<br>`Documentation/deployment/Deployment_Standardization_Final_Report.md`<br>`Documentation/deployment/Server_Deployment_Manifest_Report.md`<br>`Documentation/docs/PlantProcessIQ_Analysis_and_Validation_30May2026.md`<br>`Documentation/v5/P03_P04_TimeSeries_AssistantGateway_Runbook.md`<br>`Documentation/v5/P05_P06_VisualMapper_BlendedProvenance_Runbook.md`<br>`Documentation/v5/P07_P08_Connectors_Identity_Runbook.md`<br>`Documentation/v5/P09_P10_SSO_SCIM_SignedLicensing_Runbook.md`<br>`Documentation/v5/P11_P12_Outbound_I18n_Mobile_Runbook.md` |

## Closure Order

| Priority | Task | Pack Step | Risk | Reason |
|---:|---|---|---|---|
| 1 | T-010 | Pack A-2 | LOW | Is 0% and mostly file organization/documentation if archive is done with manifest and no active validators moved. |
| 2 | T-028 | Pack A-3 | MEDIUM | CI wiring should be done after Pack B/D validators exist and are green. |
| 3 | T-007 | Pack A-4 | MEDIUM | Dedup should happen after CI certification target is clear. |
| 4 | T-035 | Pack A-5 | LOW | Documentation should reference final validators and current mapping/drift reality. |

## Next Commands

```powershell
node .\tools\pack-a\validate-pack-a-closure-map.cjs
powershell -ExecutionPolicy Bypass -File .\tools\pack-a\Invoke-PackA-Regression.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
```
