# Pack E-1 Historian Connector Audit + Closure Map

Generated: 2026-06-06T12:57:53.483Z

Marker: PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP

## Executive Summary

Pack E has three remaining tasks: backend GA historian connector, UI register/test/map flow, and tests/docs/regression closure. The safe execution order is backend first, UI second, tests/docs/scorecard closure third.

## Remaining Pack E Tasks

| Task | Score | Status | Recommended Step | Objective |
|---|---:|---|---|---|
| T-060 | 75% | MOSTLY DONE | Pack E-2 | Promote one historian connector to GA-level behavior with provider registration, typed configuration, connection test, browse/read API, safe error handling, and source-to-mapping compatibility. |
| T-063 | 0% | NOT YET STARTED | Pack E-3 | Expose historian registration, connection test, tag/point browsing, and mapping flow in the UI without overclaiming full vendor support. |
| T-064 | 60% | PARTIALLY DONE | Pack E-4 | Add backend/frontend regression coverage, documentation, validation scripts, and task-closure bridge for the historian connector pack. |

## Backend Historian Signals

- Historian-related backend files: **34**
- Connector-specific backend files: **402**
- API/backend endpoint signals: **16**
- Backend test signals: **9**

### Backend connector candidates

| File | Lines |
|---|---:|
| `Backend/database/database.apply-order.manifest.md` | 117 |
| `Backend/database/README.md` | 37 |
| `Backend/database/scripts/060_phase_8_9_dashboard_materialized_views.sql` | 117 |
| `Backend/database/scripts/095_create_runtime_app_role_admin_only.sql` | 64 |
| `Backend/database/scripts/115_phase2_integrity_audit.sql` | 236 |
| `Backend/database/scripts/116_phase2_operation_analytics_pilot_foundation.sql` | 273 |
| `Backend/database/scripts/120_phase02_canonical_schema_mapping_engine.sql` | 332 |
| `Backend/database/scripts/130_phase03_two_stage_delta_import_architecture.sql` | 1889 |
| `Backend/database/scripts/142_phase02_phase03_page_definition_and_demo_source_completion.sql` | 303 |
| `Backend/database/scripts/204_phase04_phase05_ml_learning_core.sql` | 817 |
| `Backend/database/scripts/205_phase04_phase05_completion_governance_jobs_tests.sql` | 1240 |
| `Backend/database/scripts/206_fix_dashboard_widget_definition_schema_drift.sql` | 200 |
| `Backend/database/scripts/300_p01_p02_security_access_control_spine.sql` | 365 |
| `Backend/database/scripts/301_p01_p02_authstore_compatibility_bridge.sql` | 151 |
| `Backend/database/scripts/310_p03_p04_mapping_genealogy_foundation.sql` | 904 |
| `Backend/database/scripts/311_p03_p04_fix_genealogy_walk_and_safe_sql.sql` | 246 |
| `Backend/database/scripts/312_p03_p04_completion_pack_a.sql` | 721 |
| `Backend/database/scripts/313_p03_p04_completion_pack_a_hotfix.sql` | 404 |
| `Backend/database/scripts/360_p05_read_model_refresh_infrastructure.sql` | 102 |
| `Backend/database/scripts/361_p05_dashboard_read_models.sql` | 59 |
| `Backend/database/scripts/362_p05_dashboard_read_models_extra.sql` | 63 |
| `Backend/database/scripts/420_p3_value_evidence_hmi.sql` | 223 |
| `Backend/database/scripts/440_p5_ot_safe_acquisition.sql` | 117 |
| `Backend/database/scripts/520_v5_p03_timeseries_foundation.sql` | 427 |
| `Backend/database/scripts/550_v5_p06_blended_provenance.sql` | 234 |
| `Backend/database/scripts/560_v5_p07_plant_connector_breadth.sql` | 227 |
| `Backend/database/scripts/610_v5_p12_i18n_rtl_mobile.sql` | 166 |
| `Backend/database/scripts/620_v5_p13_deployment_dr_portability.sql` | 279 |
| `Backend/database/scripts/665_pack_b35_mostly_green_task_closure.sql` | 171 |
| `Backend/database/scripts/670_pack_b4_connector_runtime_certification.sql` | 345 |
| `Backend/database/seed/000_plantprocessiq_unified_advanced_realistic_demo_seed.sql` | 1629 |
| `Backend/database/seed/001_basic_genealogy_seed.sql` | 402 |
| `Backend/database/seed/002_full_feature_demo_seed.sql` | 971 |
| `Backend/database/seed/002_full_feature_demo_seed.txt` | 971 |
| `Backend/database/seed/005_phase0_job_definitions.sql` | 146 |
| `Backend/database/seed/090_phase1_system_dashboard_templates.sql` | 412 |
| `Backend/database/views/006_dashboard_dataset_views.sql` | 57 |
| `Backend/docker-compose.yml` | 52 |
| `Backend/PlantProcess.Analytics.Core/Contracts/AdvancedAnalysisResult.cs` | 28 |
| `Backend/PlantProcess.Analytics.Core/Discipline/StatisticalDiscipline.cs` | 91 |
| `Backend/PlantProcess.Analytics.Core/Kpi/ExpressionEvaluator.cs` | 131 |
| `Backend/PlantProcess.Analytics.Core/Kpi/KpiEngine.cs` | 137 |
| `Backend/PlantProcess.Analytics.Core/Methods/CategoricalAssociation.cs` | 31 |
| `Backend/PlantProcess.Analytics.Core/Methods/MutualInformation.cs` | 67 |
| `Backend/PlantProcess.Analytics.Core/Methods/VarianceInflation.cs` | 38 |
| `Backend/PlantProcess.Analytics.Core/Numerics/Stats.cs` | 105 |
| `Backend/PlantProcess.Analytics.Core/Primitives/AnalysisModels.cs` | 39 |
| `Backend/PlantProcess.Analytics.Core/Readiness/ReadinessGate.cs` | 61 |
| `Backend/PlantProcess.Analytics.Engine/ManagedStatisticalComputeEngine.cs` | 122 |
| `Backend/PlantProcess.Analytics.Engine/Ports.cs` | 46 |
| `Backend/PlantProcess.Analytics.Engine/Postgres/PostgresAnalysisFindingSink.cs` | 110 |
| `Backend/PlantProcess.Analytics.Engine/Postgres/PostgresCanonicalFeatureSource.cs` | 152 |
| `Backend/PlantProcess.Analytics.Engine/Postgres/PostgresEngineServiceCollectionExtensions.cs` | 23 |
| `Backend/PlantProcess.Api/Analytics/KpiEvaluationService.cs` | 133 |
| `Backend/PlantProcess.Api/AssistantGateway/V5AssistantGateway.cs` | 654 |
| `Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs` | 786 |
| `Backend/PlantProcess.Api/BlendedProvenance/V5BlendedProvenanceEndpoints.cs` | 142 |
| `Backend/PlantProcess.Api/ComplianceControls/V5ComplianceControlsEndpoints.cs` | 487 |
| `Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs` | 435 |
| `Backend/PlantProcess.Api/DeploymentPortability/V5DeploymentPortabilityEndpoints.cs` | 272 |
| `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 579 |
| `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 229 |
| `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.runtime.cs` | 1389 |
| `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 397 |
| `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 146 |
| `Backend/PlantProcess.Api/Endpoints/Admin/P03P04MappingGenealogyEndpoints.cs` | 46 |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.runtime.cs` | 1368 |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 888 |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 401 |
| `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 426 |
| `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 620 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs` | 125 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 752 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/KpiEvaluationEndpoints.cs` | 142 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 359 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs` | 307 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 87 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 600 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/ProvenanceGuardedAdvancedResultsEndpoints.cs` | 78 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 77 |

## Frontend Historian Signals

- Historian-related frontend files: **8**
- UI page/component signals: **222**
- API client signals: **7**

### Frontend candidates

| File | Lines |
|---|---:|
| `Frontend/PlantProcess.Web/docs/a11y/audit-30May2026.md` | 34 |
| `Frontend/PlantProcess.Web/docs/visual-regression/phase56-baseline.md` | 26 |
| `Frontend/PlantProcess.Web/e2e/a11y/phase56-accessibility.spec.ts` | 35 |
| `Frontend/PlantProcess.Web/e2e/admin-db-focused.spec.ts` | 109 |
| `Frontend/PlantProcess.Web/e2e/admin-schema-focused.spec.ts` | 126 |
| `Frontend/PlantProcess.Web/e2e/api-smoke.spec.ts` | 99 |
| `Frontend/PlantProcess.Web/e2e/api/phase02-data-lifecycle-contract.spec.ts` | 9 |
| `Frontend/PlantProcess.Web/e2e/api/phase03-two-stage-import.spec.ts` | 58 |
| `Frontend/PlantProcess.Web/e2e/critical-shell-regression.spec.ts` | 23 |
| `Frontend/PlantProcess.Web/e2e/dashboard-refresh-filter.spec.ts` | 196 |
| `Frontend/PlantProcess.Web/e2e/dimension2-dimension6-readiness.spec.ts` | 67 |
| `Frontend/PlantProcess.Web/e2e/dimension7-brand-identity.spec.ts` | 24 |
| `Frontend/PlantProcess.Web/e2e/full-stack-smoke.spec.ts` | 27 |
| `Frontend/PlantProcess.Web/e2e/golden-path.spec.ts` | 222 |
| `Frontend/PlantProcess.Web/e2e/hardening-matrix.spec.ts` | 154 |
| `Frontend/PlantProcess.Web/e2e/helpers/auth.ts` | 181 |
| `Frontend/PlantProcess.Web/e2e/helpers/e2eFailureFilters.ts` | 72 |
| `Frontend/PlantProcess.Web/e2e/helpers/hardening.ts` | 176 |
| `Frontend/PlantProcess.Web/e2e/helpers/networkGuard.ts` | 39 |
| `Frontend/PlantProcess.Web/e2e/helpers/phase1Hardening.ts` | 174 |
| `Frontend/PlantProcess.Web/e2e/helpers/phase2Guard.ts` | 217 |
| `Frontend/PlantProcess.Web/e2e/helpers/phase9ActionMatrix.ts` | 167 |
| `Frontend/PlantProcess.Web/e2e/i18n/phase78-i18n-rtl.spec.ts` | 26 |
| `Frontend/PlantProcess.Web/e2e/journeys/p00-e2e-consolidation.contract.spec.ts` | 69 |
| `Frontend/PlantProcess.Web/e2e/license-and-demo-lifecycle.spec.ts` | 90 |
| `Frontend/PlantProcess.Web/e2e/license-gate-ux.spec.ts` | 27 |
| `Frontend/PlantProcess.Web/e2e/nav-graph-refresh-survival.spec.ts` | 63 |
| `Frontend/PlantProcess.Web/e2e/p0-auth-pages-contract.spec.ts` | 43 |
| `Frontend/PlantProcess.Web/e2e/p1-risk-dataquality-contract.spec.ts` | 71 |
| `Frontend/PlantProcess.Web/e2e/p1-safety-net.spec.ts` | 28 |
| `Frontend/PlantProcess.Web/e2e/p4-advanced-analysis.spec.ts` | 20 |
| `Frontend/PlantProcess.Web/e2e/p6-genealogy-widget-workflow.spec.ts` | 42 |
| `Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts` | 43 |
| `Frontend/PlantProcess.Web/e2e/phase1-button-action-matrix.spec.ts` | 67 |
| `Frontend/PlantProcess.Web/e2e/phase1-golden-demo.spec.ts` | 84 |
| `Frontend/PlantProcess.Web/e2e/phase1-security-hardening.spec.ts` | 83 |
| `Frontend/PlantProcess.Web/e2e/phase1-toast-mapping.spec.ts` | 46 |
| `Frontend/PlantProcess.Web/e2e/phase2-backend-outage.spec.ts` | 73 |
| `Frontend/PlantProcess.Web/e2e/phase2-chart-interaction.spec.ts` | 82 |
| `Frontend/PlantProcess.Web/e2e/phase2-lifecycle-proof.spec.ts` | 157 |
| `Frontend/PlantProcess.Web/e2e/phase2-realism-demo-lifecycle.spec.ts` | 22 |
| `Frontend/PlantProcess.Web/e2e/phase2-responsive-multibrowser.spec.ts` | 65 |
| `Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts` | 159 |
| `Frontend/PlantProcess.Web/e2e/phase3-dynamic-page-rendering.spec.ts` | 66 |
| `Frontend/PlantProcess.Web/e2e/phase4-ml-foundation-proof.spec.ts` | 78 |
| `Frontend/PlantProcess.Web/e2e/phase5-scheduled-learning-proof.spec.ts` | 89 |
| `Frontend/PlantProcess.Web/e2e/phase56-primary-flows.spec.ts` | 27 |
| `Frontend/PlantProcess.Web/e2e/phase78-workflow-widget.spec.ts` | 26 |
| `Frontend/PlantProcess.Web/e2e/phase9-action-matrix.spec.ts` | 30 |
| `Frontend/PlantProcess.Web/e2e/phase9-responsive-states.spec.ts` | 54 |
| `Frontend/PlantProcess.Web/e2e/route-smoke.spec.ts` | 28 |
| `Frontend/PlantProcess.Web/e2e/security/auth-matrix-admin.spec.ts` | 147 |
| `Frontend/PlantProcess.Web/e2e/tsconfig.json` | 18 |
| `Frontend/PlantProcess.Web/e2e/visual/phase56-analytics-system.visual.spec.ts` | 17 |
| `Frontend/PlantProcess.Web/e2e/visual/phase9-core.visual.spec.ts` | 36 |
| `Frontend/PlantProcess.Web/e2e/widget-builder-focused.spec.ts` | 114 |
| `Frontend/PlantProcess.Web/eslint.config.js` | 136 |
| `Frontend/PlantProcess.Web/package-lock.json` | 7599 |
| `Frontend/PlantProcess.Web/package.json` | 127 |
| `Frontend/PlantProcess.Web/playwright.config.ts` | 178 |
| `Frontend/PlantProcess.Web/playwright.phase2.config.ts` | 66 |
| `Frontend/PlantProcess.Web/playwright.phase9.config.ts` | 35 |
| `Frontend/PlantProcess.Web/reports/ui-audit/phase2-ui-audit.json` | 8831 |
| `Frontend/PlantProcess.Web/reports/ui-audit/phase2-ui-audit.md` | 1017 |
| `Frontend/PlantProcess.Web/scripts/codemods/standardize-imports.cjs` | 49 |
| `Frontend/PlantProcess.Web/src/api/__tests__/apiClient.retry-backoff.test.ts` | 53 |
| `Frontend/PlantProcess.Web/src/api/advancedAnalysis.ts` | 32 |
| `Frontend/PlantProcess.Web/src/api/demo/demoLifecycle.api.ts` | 116 |
| `Frontend/PlantProcess.Web/src/api/http/apiClient.ts` | 364 |
| `Frontend/PlantProcess.Web/src/api/integration/integration.api.ts` | 31 |
| `Frontend/PlantProcess.Web/src/api/legacy/__tests__/plantProcessApi.contract.test.ts` | 62 |
| `Frontend/PlantProcess.Web/src/api/license/license.api.ts` | 51 |
| `Frontend/PlantProcess.Web/src/api/license/licenseUsage.api.ts` | 85 |
| `Frontend/PlantProcess.Web/src/api/ml/mlReadiness.api.ts` | 120 |
| `Frontend/PlantProcess.Web/src/api/product-core/admin-mapping-types.ts` | 337 |
| `Frontend/PlantProcess.Web/src/api/product-core/dashboard-widget-types.ts` | 271 |
| `Frontend/PlantProcess.Web/src/api/product-core/license-commercial-types.ts` | 12 |
| `Frontend/PlantProcess.Web/src/api/product-core/product-core-types.manifest.json` | 345 |
| `Frontend/PlantProcess.Web/src/api/product-core/shared-types.ts` | 106 |
| `Frontend/PlantProcess.Web/src/api/product-core/types.ts` | 90 |

## Docs/Test Signals

- Historian docs: **59**
- Connector honesty/vendor-scope docs: **19**

## Closure Order

| Priority | Task | Step | Risk | Reason |
|---:|---|---|---|---|
| 1 | T-060 | Pack E-2 | MEDIUM | Backend connector behavior must exist before UI and tests can be meaningful. |
| 2 | T-063 | Pack E-3 | MEDIUM | UI should only expose what the backend supports and must avoid vendor-overclaim. |
| 3 | T-064 | Pack E-4 | LOW | After backend and UI are present, lock behavior through tests/docs/regression. |

## Next Step

Next implementation step: **Pack E-2 / T-060 — GA historian connector backend**.
