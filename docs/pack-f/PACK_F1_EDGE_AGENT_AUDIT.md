# Pack F-1 OT-Safe Edge Agent Audit + Closure Map

Generated: 2026-06-06T13:39:35.854Z

Marker: PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP

## Executive Summary

Pack F closes the remaining OT-safe edge-agent scope: one-way push backend/agent, packaging/deployment, management UX, and final regression/docs closure.

The safety principle is non-negotiable: the edge agent must be read-only toward OT sources and outbound-only toward PlantProcess IQ. It must not require inbound access into the OT network.

## Remaining Pack F Tasks

| Task | Score | Status | Recommended Step | Objective |
|---|---:|---|---|---|
| T-066 | 17% | NOT YET STARTED | Pack F-2 | Create an OT-safe, read-only edge collector/agent pattern that only pushes outbound data to PlantProcess IQ and never requires inbound OT network access. |
| T-067 | 0% | NOT YET STARTED | Pack F-3 | Package the edge agent with deployment scripts, sample configuration, service wrapper guidance, Docker-friendly mode, and safety documentation. |
| T-068 | 50% | PARTIALLY DONE | Pack F-4 | Expose edge collector registration, heartbeat, queue status, collector profile, one-way push status, and deployment guidance in the UI. |
| T-071 | 60% | PARTIALLY DONE | Pack F-5 | Lock Pack F with validators, build regression, documentation, final scorecard bridge, and OT-safety proof. |

## Backend / Agent Signals

- Backend edge signals: **150**
- Backend worker signals: **24**
- Backend API signals: **5**
- Backend OT-safety signals: **41**
- Backend test signals: **25**

### Backend candidates

| File | Lines |
|---|---:|
| `Backend/database/database.apply-order.manifest.md` | 117 |
| `Backend/database/scripts/050_dashboard_phase8_9_10_indexes.sql` | 127 |
| `Backend/database/scripts/112_phase1_golden_demo_data.sql` | 77826 |
| `Backend/database/scripts/115_phase2_integrity_audit.sql` | 236 |
| `Backend/database/scripts/116_phase2_operation_analytics_pilot_foundation.sql` | 273 |
| `Backend/database/scripts/130_phase03_two_stage_delta_import_architecture.sql` | 1889 |
| `Backend/database/scripts/200_phase02_ml_foundation_feature_store_pgvector.sql` | 710 |
| `Backend/database/scripts/201_phase02_ml_feature_store_v6_completion.sql` | 187 |
| `Backend/database/scripts/204_phase04_phase05_ml_learning_core.sql` | 817 |
| `Backend/database/scripts/300_p01_p02_security_access_control_spine.sql` | 365 |
| `Backend/database/scripts/301_p01_p02_authstore_compatibility_bridge.sql` | 151 |
| `Backend/database/scripts/310_p03_p04_mapping_genealogy_foundation.sql` | 904 |
| `Backend/database/scripts/311_p03_p04_fix_genealogy_walk_and_safe_sql.sql` | 246 |
| `Backend/database/scripts/312_p03_p04_completion_pack_a.sql` | 721 |
| `Backend/database/scripts/313_p03_p04_completion_pack_a_hotfix.sql` | 404 |
| `Backend/database/scripts/440_p5_ot_safe_acquisition.sql` | 117 |
| `Backend/database/scripts/510_v5_p02_rls_tenant_isolation_and_secret_vault.sql` | 320 |
| `Backend/database/scripts/511_v5_p02_hotpath_explain_review.sql` | 42 |
| `Backend/database/scripts/530_v5_p04_assistant_model_gateway_boundary.sql` | 301 |
| `Backend/database/scripts/550_v5_p06_blended_provenance.sql` | 234 |
| `Backend/database/scripts/560_v5_p07_plant_connector_breadth.sql` | 227 |
| `Backend/database/scripts/570_v5_p08_enterprise_identity_mfa_sessions.sql` | 240 |
| `Backend/database/scripts/600_v5_p11_outbound_notifications_leads.sql` | 271 |
| `Backend/database/scripts/610_v5_p12_i18n_rtl_mobile.sql` | 166 |
| `Backend/database/scripts/630_v5_p14_compliance_refactor_controls.sql` | 214 |
| `Backend/database/scripts/640_remaining_sensitive_data_catalog.sql` | 86 |
| `Backend/database/scripts/665_pack_b35_mostly_green_task_closure.sql` | 171 |
| `Backend/database/seed/000_plantprocessiq_unified_advanced_realistic_demo_seed.sql` | 1629 |
| `Backend/database/seed/001_basic_genealogy_seed.sql` | 402 |
| `Backend/database/seed/002_full_feature_demo_seed.sql` | 971 |
| `Backend/database/seed/002_full_feature_demo_seed.txt` | 971 |
| `Backend/database/seed/003_additional_demo_seed.sql` | 157 |
| `Backend/database/seed/005_phase0_job_definitions.sql` | 146 |
| `Backend/database/seed/source-systems/synthetic_inspection_source_insert.sql` | 6602 |
| `Backend/PlantProcess.Analytics.Core/Methods/MutualInformation.cs` | 67 |
| `Backend/PlantProcess.Analytics.Core/Primitives/SimpleAnalysis.cs` | 140 |
| `Backend/PlantProcess.Api/AssistantGateway/V5AssistantGateway.cs` | 654 |
| `Backend/PlantProcess.Api/BlendedProvenance/V5BlendedProvenanceEndpoints.cs` | 142 |
| `Backend/PlantProcess.Api/ComplianceControls/V5ComplianceControlsEndpoints.cs` | 487 |
| `Backend/PlantProcess.Api/Endpoints/Admin/P03P04MappingGenealogyEndpoints.cs` | 46 |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.runtime.cs` | 1368 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 752 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 359 |
| `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 415 |
| `Backend/PlantProcess.Api/Endpoints/Development/DevSeedEndpoints.cs` | 95 |
| `Backend/PlantProcess.Api/Endpoints/Diagnostics/DiagnosticsEndpoints.cs` | 92 |
| `Backend/PlantProcess.Api/Endpoints/Integration/ImportWorkflowEndpoints.cs` | 97 |
| `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 296 |
| `Backend/PlantProcess.Api/Endpoints/Materials/MaterialInvestigationEndpoints.cs` | 390 |
| `Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs` | 483 |
| `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 603 |
| `Backend/PlantProcess.Api/Endpoints/Validation/ValidationEndpoints.cs` | 275 |
| `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.runtime.cs` | 1031 |
| `Backend/PlantProcess.Api/EnterpriseIdentity/V5EnterpriseIdentityEndpoints.cs` | 958 |
| `Backend/PlantProcess.Api/Middleware/AuditLogMiddleware.cs` | 231 |
| `Backend/PlantProcess.Api/OutboundLeadSystem/V5OutboundLeadSystemEndpoints.cs` | 737 |
| `Backend/PlantProcess.Api/PlantConnectors/V5PlantConnectorEndpoints.cs` | 642 |
| `Backend/PlantProcess.Api/Program.cs` | 571 |
| `Backend/PlantProcess.Api/Security/AuthStore.cs` | 452 |
| `Backend/PlantProcess.Api/Security/P01P02SecuritySchema.cs` | 199 |
| `Backend/PlantProcess.Api/VisualMapper/V5VisualMapperEndpoints.cs` | 717 |
| `Backend/PlantProcess.Application/Acquisition/OtSafeAcquisitionContracts.cs` | 107 |
| `Backend/PlantProcess.Application/Acquisition/OtSafeEdgeCollectorGateway.cs` | 56 |
| `Backend/PlantProcess.Application/Analytics/Contracts/MlReadinessDtos.cs` | 90 |
| `Backend/PlantProcess.Application/Analytics/Services/MlReadinessService.cs` | 482 |
| `Backend/PlantProcess.Application/Analytics/Services/QualityLabelBuilderService.cs` | 245 |
| `Backend/PlantProcess.Application/Analytics/Suggestions/SuggestionWorkflow.cs` | 35 |
| `Backend/PlantProcess.Application/Audit/AuditLogContext.cs` | 26 |
| `Backend/PlantProcess.Application/Common/Persistence/IPlantProcessDbContext.cs` | 66 |
| `Backend/PlantProcess.Application/Contracts/Materials/CreateGenealogyEdgeCommand.cs` | 17 |
| `Backend/PlantProcess.Application/Contracts/Readiness/ApplicationReadinessDto.cs` | 90 |
| `Backend/PlantProcess.Application/Demo/Contracts/DemoLifecycleDtos.cs` | 102 |
| `Backend/PlantProcess.Application/Demo/Services/DemoLifecycleService.cs` | 584 |
| `Backend/PlantProcess.Application/DependencyInjection.cs` | 133 |
| `Backend/PlantProcess.Application/Integration/Contracts/Dtos/ImportWorkflowResult.cs` | 52 |
| `Backend/PlantProcess.Application/Integration/Interfaces/Import/IImportBatchQueueProcessorService.cs` | 19 |
| `Backend/PlantProcess.Application/Integration/Security/CanonicalTableAllowlist.cs` | 26 |
| `Backend/PlantProcess.Application/Integration/Security/SqlAllowlistProvider.cs` | 101 |
| `Backend/PlantProcess.Application/Integration/Services/Import/ImportBatchQueueProcessorService.cs` | 173 |
| `Backend/PlantProcess.Application/Integration/Services/Jobs/JobDefinitionService.cs` | 338 |

## Frontend Signals

- Frontend edge signals: **27**
- Frontend page/component signals: **38**
- Frontend API client signals: **23**

## Infrastructure / Packaging Signals

- Packaging signals: **80**
- Config signals: **31**

## Docs Signals

- Edge docs: **45**
- OT-safety docs: **33**
- Deployment docs: **142**

## Closure Order

| Priority | Task | Step | Risk | Reason |
|---:|---|---|---|---|
| 1 | T-066 | Pack F-2 | HIGH | The edge architecture must be safe before packaging or UX. It must prove read-only collection and outbound-only push. |
| 2 | T-067 | Pack F-3 | MEDIUM | After the safety contract exists, package it for repeatable demo/customer deployment without mixing local/server DB or unsafe inbound assumptions. |
| 3 | T-068 | Pack F-4 | MEDIUM | The UI should display edge collector readiness, heartbeat, queue, and deployment status only after backend and packaging contracts exist. |
| 4 | T-071 | Pack F-5 | LOW | After backend, packaging, and UI are complete, lock Pack F with validators, documentation, build regression, and scorecard closure. |

## Next Step

Next implementation step: **Pack F-2 / T-066 — OT-safe edge agent one-way push backend**.
