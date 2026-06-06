# Pack D-1 Route Contract Snapshot

Generated: 2026-06-06T11:14:12.271Z

- Route count: **328**
- Group count: **58**

## Routes

| Method | Route | Name | Tag | Group | File | Line |
|---|---|---|---|---|---|---:|
| DELETE | `/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 81 |
| DELETE | `/{slug}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs` | 27 |
| DELETE | `/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 77 |
| DELETE | `/definitions/{dashboardDefinitionId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 57 |
| DELETE | `/events/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 27 |
| DELETE | `/steps/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 203 |
| GET | `/configured-summary` |  | Admin - Users | /admin/widgets | `Backend/PlantProcess.Api/Endpoints/Admin/AdminProofEndpoints.cs` | 16 |
| GET | `/readiness` |  | Analytics Advanced | /api/analytics/advanced | `Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs` | 18 |
| GET | `/cards` |  | Suggestions | /api/suggestions | `Backend/PlantProcess.Api/Endpoints/Analytics/SuggestionEndpoints.cs` | 19 |
| GET | `/cost-assumptions` |  | Value / Impact | /api/value | `Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs` | 26 |
| GET | `/status` |  | Configuration | /configuration | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 16 |
| GET | `/issues` |  | Data Quality | /data-quality | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityEndpoints.cs` | 15 |
| GET | `/lifecycle` |  | Demo Lifecycle | /demo-lifecycle | `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 22 |
| GET | `/database-summary` |  | Development / Database Validation | /dev | `Backend/PlantProcess.Api/Endpoints/Development/DevSeedEndpoints.cs` | 13 |
| GET | `/summary` |  | Integration | /integration | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 22 |
| GET | `/status` | GetPhase34MappingHealthStatus | Phase 3/4 Mapping Health | /mapping-health | `Backend/PlantProcess.Api/Endpoints/MappingHealth/Phase34MappingHealthEndpoints.cs` | 13 |
| GET | `/acceptance-summary` |  | Phase 5 Scheduled Learning Proof | /phase5 | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 21 |
| GET | `/cascade/acceptance` |  | Phase 5 Scheduled Learning Proof | /phase5 | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 19 |
| GET | `/compute/acceptance` |  | Phase 5 Scheduled Learning Proof | /phase5 | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 20 |
| GET | `/features/acceptance` |  | Phase 4 ML Foundation Proof | /phase5 | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 17 |
| GET | `/outcomes/acceptance` |  | Phase 5 Scheduled Learning Proof | /phase5 | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 18 |
| GET | `/defects` |  | Quality | /quality | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 18 |
| GET | `/readiness-assessment/pdf` |  | Readiness | /readiness | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 32 |
| GET | `/phase1.pdf` | GeneratePhase1CustomerDemoPdf | Reports - Customer Demo | /reports/customer-demo | `Backend/PlantProcess.Api/Endpoints/Reporting/CustomerDemoReportEndpoints.cs` | 15 |
| GET | `/sync-report` |  | Validation / Model Sync | /validation | `Backend/PlantProcess.Api/Endpoints/Validation/ValidationEndpoints.cs` | 13 |
| GET | `/overview` |  | Workflow | /workflow | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 34 |
| GET | `/{id:guid}/evidence` |  | Analytics Risk Evidence |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskEvidenceEndpoints.cs` | 22 |
| GET | `/{id:guid}/genealogy` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 223 |
| GET | `/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs` | 22 |
| GET | `/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 52 |
| GET | `/{jobId:guid}/history` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 35 |
| GET | `/{kpiCode}/alerts` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/KpiEvaluationEndpoints.cs` | 111 |
| GET | `/{kpiCode}/target` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/KpiEvaluationEndpoints.cs` | 59 |
| GET | `/{materialUnitId:guid}/investigation-full` |  | Material Investigation |  | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialInvestigationEndpoints.cs` | 18 |
| GET | `/{materialUnitId:guid}` |  | Feature Engineering |  | `Backend/PlantProcess.Api/Endpoints/Analytics/FeatureEngineeringEndpoints.cs` | 15 |
| GET | `/{riskType}` |  | Analytics Risk Calibration |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskCalibrationEndpoints.cs` | 17 |
| GET | `/{slug}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs` | 24 |
| GET | `/` |  | Readiness |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 39 |
| GET | `/` |  |  |  | `Backend/PlantProcess.Api/Program.cs` | 433 |
| GET | `/acceptance-summary` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs` | 19 |
| GET | `/acceptance-summary` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 31 |
| GET | `/areas/{id:guid}/children` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 22 |
| GET | `/areas` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 20 |
| GET | `/audit-log` | QueryAuditLog |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 24 |
| GET | `/business-key-validation` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 34 |
| GET | `/catalog` | GetCanonicalSchemaViewCatalog | Admin - Generic Schema Mapping |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 39 |
| GET | `/commercial-readiness` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 38 |
| GET | `/connection-profiles/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 33 |
| GET | `/connection-profiles` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 30 |
| GET | `/connector-certification` | GetPhase1ConnectorCertification |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 33 |
| GET | `/connector-truth` | GetPhase1ConnectorTruth |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 25 |
| GET | `/current` |  | Admin - License |  | `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 26 |
| GET | `/daily-production` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 27 |
| GET | `/data-quality` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 23 |
| GET | `/datasets` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 51 |
| GET | `/datasets` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/SimpleAnalysisEndpoints.cs` | 24 |
| GET | `/db-configuration/summary` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 41 |
| GET | `/db-health` | GetDbHealth |  |  | `Backend/PlantProcess.Api/Endpoints/Health/HealthEndpoints.cs` | 38 |
| GET | `/defect-by-product-family` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 32 |
| GET | `/defects/{id:guid}` |  | Quality |  | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 19 |
| GET | `/definitions/{dashboardDefinitionId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 41 |
| GET | `/definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 37 |
| GET | `/demo-language/rules` | AuditDemoLanguage |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 38 |
| GET | `/deployment-checklist` | GetDeploymentChecklist |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 29 |
| GET | `/downtime-by-area` |  | Analytics Read Models |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 17 |
| GET | `/downtime-events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 539 |
| GET | `/equipment-defect-rate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 27 |
| GET | `/equipment-stoppage` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 22 |
| GET | `/equipment/{id:guid}/children` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 23 |
| GET | `/equipment/{id:guid}/materials` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 24 |
| GET | `/equipment` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 21 |
| GET | `/events/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 24 |
| GET | `/events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 444 |
| GET | `/events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 23 |
| GET | `/feature-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 22 |
| GET | `/features` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 29 |
| GET | `/genealogy-validation` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 58 |
| GET | `/genealogy/{materialCode}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs` | 17 |
| GET | `/golden-dataset/acceptance` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 30 |
| GET | `/health/ready` | GetReadiness |  |  | `Backend/PlantProcess.Api/Endpoints/Health/HealthEndpoints.cs` | 45 |
| GET | `/health` | GetHealth | Health |  | `Backend/PlantProcess.Api/Endpoints/Health/HealthEndpoints.cs` | 28 |
| GET | `/import-batches/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 31 |
| GET | `/import-batches` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 30 |
| GET | `/import-jobs/configuration-board` | GetPhase1ImportJobConfigurationBoard |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 86 |
| GET | `/import-lifecycle/acceptance` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs` | 18 |
| GET | `/industry-templates` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 25 |
| GET | `/inspection-jobs` | SaveInspectionJobFromCorrelation |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 24 |
| GET | `/issues/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityEndpoints.cs` | 60 |
| GET | `/issues/material/{materialUnitId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityEndpoints.cs` | 87 |
| GET | `/jobs-monitor` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 49 |
| GET | `/jobs` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs` | 20 |
| GET | `/jobs` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 21 |
| GET | `/kpi-parameter-bindings` | GetPhase2KpiParameterBindings |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 32 |
| GET | `/kpis` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 63 |
| GET | `/labels/preview` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 17 |
| GET | `/limits` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 32 |
| GET | `/mapping-definitions/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 41 |
| GET | `/mapping-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 40 |
| GET | `/material-investigation/{materialKey}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 68 |
| GET | `/material-sample` |  | Development / Database Validation |  | `Backend/PlantProcess.Api/Endpoints/Development/DevSeedEndpoints.cs` | 14 |
| GET | `/material-unit-types` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 30 |
| GET | `/material/{materialUnitId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs` | 23 |
| GET | `/materials/{materialUnitId:guid}/context` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 29 |
| GET | `/materials/{materialUnitId:guid}/investigation/pdf` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 19 |
| GET | `/materials/{materialUnitId:guid}/investigation` |  | Reports |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 16 |
| GET | `/materials/{materialUnitId:guid}/investigation` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 58 |
| GET | `/materials` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 27 |
| GET | `/metadata` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 29 |
| GET | `/ml-lifecycle` | GetMlLifecycleStates |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 34 |
| GET | `/models` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs` | 29 |
| GET | `/narrative/proof` |  | ML Providers |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlProviderEndpoints.cs` | 15 |
| GET | `/operation-defect-rate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 28 |
| GET | `/operation-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 35 |
| GET | `/operations/progress/{operationId:guid}` | GetPhase2RecentOperationProgress |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 62 |
| GET | `/operations/progress/recent` | GetPhase2RecentOperationProgress |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 58 |
| GET | `/outcomes` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 23 |
| GET | `/overview` |  | Admin |  | `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 33 |
| GET | `/overview` |  | Admin - Two Stage Import |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 17 |
| GET | `/overview` |  | Dashboard |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 20 |
| GET | `/pages/{slug}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/DynamicContent/DynamicContentEndpoints.cs` | 17 |
| GET | `/parameter-by-grade` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 37 |
| GET | `/parameter-defect/genealogy-aware` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 37 |
| GET | `/parameter-defect` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 25 |
| GET | `/parameter-trend` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 45 |
| GET | `/parameters/definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 234 |
| GET | `/parameters/observations` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 316 |
| GET | `/phase1-summary` | GeneratePhase1CustomerDemoPdf |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/CustomerDemoReportEndpoints.cs` | 19 |
| GET | `/primitives` |  | Analytics Simple |  | `Backend/PlantProcess.Api/Endpoints/Analytics/SimpleAnalysisEndpoints.cs` | 23 |
| GET | `/proof` |  | Admin - Widgets |  | `Backend/PlantProcess.Api/Endpoints/Admin/AdminProofEndpoints.cs` | 23 |
| GET | `/provider-types` |  | Admin - Connectors |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 27 |
| GET | `/quality` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 21 |
| GET | `/readiness-assessment` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 26 |
| GET | `/readiness` | ExecuteCanonicalSchemaMapping |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 67 |
| GET | `/readiness` |  | Admin - P03/P04 Mapping & Genealogy |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04MappingGenealogyEndpoints.cs` | 14 |
| GET | `/readiness` |  | ML Foundation |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 20 |
| GET | `/reference-data` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 28 |
| GET | `/report/pdf` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 51 |
| GET | `/report` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 45 |
| GET | `/reset/{jobId:guid}/progress` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 37 |
| GET | `/reset/{jobId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 34 |
| GET | `/results` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs` | 26 |
| GET | `/results` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs` | 26 |
| GET | `/risk` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 22 |
| GET | `/route-steps` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 45 |
| GET | `/routes` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 40 |
| GET | `/runs-guarded` |  | Analytics Advanced (Guarded) |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ProvenanceGuardedAdvancedResultsEndpoints.cs` | 23 |
| GET | `/runs/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 34 |
| GET | `/runs` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 23 |
| GET | `/runs` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs` | 21 |
| GET | `/runs` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 33 |
| GET | `/scan-preview` |  | Data Quality Scan |  | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityScanEndpoints.cs` | 18 |
| GET | `/scheduled-learning/acceptance` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 29 |
| GET | `/scheduled-learning/jobs` |  | Phase 5 Scheduled Learning Proof |  | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 27 |
| GET | `/schema-configuration/summary` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 45 |
| GET | `/schema-mapping/workbench` | GetPhase1SchemaMappingWorkbench |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 74 |
| GET | `/score` |  | ML Readiness |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 13 |
| GET | `/sites/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 27 |
| GET | `/sites` |  | Plant Layout |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 19 |
| GET | `/source-lifecycle/acceptance` |  | Phase 2 Lifecycle Proof |  | `Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs` | 16 |
| GET | `/source-schedule-board` | GetPhase1SourceScheduleBoard |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 42 |
| GET | `/source-systems/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 25 |
| GET | `/source-systems` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 24 |
| GET | `/source-tables` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 20 |
| GET | `/staging-records` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 37 |
| GET | `/staging/records` | GetPhase1StagingSummary |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 66 |
| GET | `/staging/summary` | GetPhase1StagingSummary |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 62 |
| GET | `/staleness` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 53 |
| GET | `/status` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 24 |
| GET | `/status` |  | ML Learning |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs` | 17 |
| GET | `/status` |  | Workflow |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 35 |
| GET | `/steps/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 65 |
| GET | `/steps` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 18 |
| GET | `/suggestions` |  | Dynamic Content |  | `Backend/PlantProcess.Api/Endpoints/DynamicContent/DynamicContentEndpoints.cs` | 14 |
| GET | `/summary` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 23 |
| GET | `/summary` | GetPhase34MappingHealthStatus |  |  | `Backend/PlantProcess.Api/Endpoints/MappingHealth/Phase34MappingHealthEndpoints.cs` | 21 |
| GET | `/tenant-isolation-decision` | GetTenantIsolationDecision |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 19 |
| GET | `/two-stage-import-model` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 37 |
| GET | `/usage` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 35 |
| GET | `/views/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 39 |
| GET | `/views` |  | Admin - Schema Configuration |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 36 |
| GET | `/workspace` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 29 |
| PATCH | `/connection-profiles/{connectionProfileId:guid}/schedule` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 39 |
| PATCH | `/connection-profiles/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 42 |
| PATCH | `/connection-profiles/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 45 |
| PATCH | `/definitions/{dashboardDefinitionId:guid}/layout` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 53 |
| PATCH | `/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}/layout` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 69 |
| PATCH | `/industry-templates/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 27 |
| PATCH | `/industry-templates/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 28 |
| PATCH | `/mapping-definitions/{id:guid}/mapping-json` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 43 |
| PATCH | `/mappings/{mappingDefinitionId:guid}/schedule` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 43 |
| PATCH | `/material-unit-types/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 32 |
| PATCH | `/material-unit-types/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 33 |
| PATCH | `/operation-definitions/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 37 |
| PATCH | `/operation-definitions/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 38 |
| PATCH | `/routes/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 42 |
| PATCH | `/routes/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 43 |
| PATCH | `/source-systems/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 27 |
| PATCH | `/source-systems/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 28 |
| PATCH | `/steps/{id:guid}/abort` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 180 |
| PATCH | `/steps/{id:guid}/complete` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 157 |
| POST | `/ask` |  | Assistant | /api/assistant | `Backend/PlantProcess.Api/Endpoints/Assistant/AssistantEndpoints.cs` | 20 |
| POST | `/readiness-assessment/pdf` |  | Readiness | /readiness | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 29 |
| POST | `/run` |  | Import Workflow | /workflow/import | `Backend/PlantProcess.Api/Endpoints/Integration/ImportWorkflowEndpoints.cs` | 15 |
| POST | `/{id:guid}/aliases` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 160 |
| POST | `/{jobId:guid}/pause` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 27 |
| POST | `/{jobId:guid}/resume` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 31 |
| POST | `/{jobId:guid}/run-now` |  | Admin - Jobs |  | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 23 |
| POST | `/{kpiCode}/evaluate` |  | KPI Evaluation |  | `Backend/PlantProcess.Api/Endpoints/Analytics/KpiEvaluationEndpoints.cs` | 22 |
| POST | `/areas` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 29 |
| POST | `/calculate-all` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs` | 28 |
| POST | `/canonical/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 26 |
| POST | `/catalog/register` | GetCanonicalSchemaViewCatalog |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 43 |
| POST | `/client-error` |  | Diagnostics |  | `Backend/PlantProcess.Api/Endpoints/Diagnostics/DiagnosticsEndpoints.cs` | 31 |
| POST | `/compute/correlation` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 24 |
| POST | `/connection-profiles/{id:guid}/test` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 48 |
| POST | `/connection-profiles` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 36 |
| POST | `/cross-source/preview-join` | PreviewPhase2CrossSourceJoin |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 23 |
| POST | `/cross-source/save-join-view` | PreviewPhase2CrossSourceJoin |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 27 |
| POST | `/data-quality-issues` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 55 |
| POST | `/datasets/{id:guid}/discover-csv-schema` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 57 |
| POST | `/datasets/{id:guid}/import-csv-snapshot` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 63 |
| POST | `/datasets/{id:guid}/preview-csv` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 60 |
| POST | `/datasets` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 54 |
| POST | `/defects` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 20 |
| POST | `/defects` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 52 |
| POST | `/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}/clone` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 73 |
| POST | `/definitions/{dashboardDefinitionId:guid}/widgets` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 61 |
| POST | `/definitions/system-templates/ensure` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 81 |
| POST | `/definitions/system-templates/repair` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 85 |
| POST | `/definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 45 |
| POST | `/demo-language/audit` | AuditDemoLanguage |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 34 |
| POST | `/downtime-events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 583 |
| POST | `/downtime-events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 50 |
| POST | `/equipment` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 30 |
| POST | `/events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 486 |
| POST | `/events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 26 |
| POST | `/execute/{viewCode}` | CreateGenericKpiView |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 63 |
| POST | `/feature-store/refresh` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 21 |
| POST | `/genealogy-edges` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 185 |
| POST | `/genealogy-edges` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 43 |
| POST | `/impact` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs` | 49 |
| POST | `/import-batches/{id:guid}/mark-completed` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 34 |
| POST | `/import-batches/{id:guid}/mark-failed` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 35 |
| POST | `/import-batches/{id:guid}/mark-running` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 33 |
| POST | `/import-batches` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 32 |
| POST | `/import-batches` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 38 |
| POST | `/import-jobs/from-mapping` | GetPhase1ImportJobConfigurationBoard |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 90 |
| POST | `/industry-templates` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 26 |
| POST | `/inspection-jobs/save-from-correlation` | SaveInspectionJobFromCorrelation |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 20 |
| POST | `/issues` |  |  |  | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityEndpoints.cs` | 112 |
| POST | `/jobs/{jobCode}/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs` | 23 |
| POST | `/jobs/{jobDefinitionId:guid}/disable` | EnablePhase2Job |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 53 |
| POST | `/jobs/{jobDefinitionId:guid}/enable` | RetryPhase2Job |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 49 |
| POST | `/jobs/{jobDefinitionId:guid}/retry` | RunPhase2JobNow |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 45 |
| POST | `/jobs/{jobDefinitionId:guid}/run-now` | RunPhase2JobNow |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 41 |
| POST | `/jobs/ensure` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 25 |
| POST | `/joins/materialize` | PreviewGenericCrossSourceJoin |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 55 |
| POST | `/joins/preview` | ResolveCanonicalSchemaView |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 51 |
| POST | `/kb/search` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 26 |
| POST | `/kb/upsert` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 25 |
| POST | `/kpi-parameter-bindings` | GetPhase2KpiParameterBindings |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 36 |
| POST | `/kpi-views` | MaterializeGenericCrossSourceJoin |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 59 |
| POST | `/kpis` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 66 |
| POST | `/mapping-definitions/{id:guid}/execute` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 45 |
| POST | `/mapping-definitions/{id:guid}/preview` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 44 |
| POST | `/mapping-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 42 |
| POST | `/mapping-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 39 |
| POST | `/mapping-lifecycle-proof/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 80 |
| POST | `/material-unit-types` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 31 |
| POST | `/materials/{materialUnitId:guid}/aliases` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 42 |
| POST | `/materials/{materialUnitId:guid}/calculate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs` | 27 |
| POST | `/materials` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 41 |
| POST | `/ml-lifecycle/evaluate` | GetMlLifecycleStates |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 38 |
| POST | `/operation-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 36 |
| POST | `/operations/progress` | GetPhase2OperationProgress |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 66 |
| POST | `/parameter-definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 46 |
| POST | `/parameter-observations` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 47 |
| POST | `/parameters/definitions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 270 |
| POST | `/parameters/observations` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 376 |
| POST | `/process-events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 49 |
| POST | `/process-queue` |  | Import Workflow |  | `Backend/PlantProcess.Api/Endpoints/Integration/ImportWorkflowEndpoints.cs` | 16 |
| POST | `/process-steps` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 45 |
| POST | `/provision-baseline` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 35 |
| POST | `/quality-events` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 53 |
| POST | `/read-models/refresh` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 34 |
| POST | `/readiness-assessment` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 22 |
| POST | `/report/pdf` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 48 |
| POST | `/report` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 42 |
| POST | `/reset` |  | Demo Lifecycle Reset |  | `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 30 |
| POST | `/resolve` | RegisterCanonicalSchemaView |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 47 |
| POST | `/risk-scores` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 56 |
| POST | `/route-steps` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 46 |
| POST | `/routes` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 41 |
| POST | `/rule-correlation/run` | RunRuleBasedCorrelation |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 29 |
| POST | `/run-due-source-imports` | GetPhase1SourceScheduleBoard |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 46 |
| POST | `/run-full-cycle` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 32 |
| POST | `/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/SimpleAnalysisEndpoints.cs` | 26 |
| POST | `/runs` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 32 |
| POST | `/safe-sql/resolve` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 44 |
| POST | `/scan/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityScanEndpoints.cs` | 19 |
| POST | `/scan` |  |  |  | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityScanEndpoints.cs` | 22 |
| POST | `/scheduled-learning/run-now` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 28 |
| POST | `/schema-mapping/preview-view` | GetPhase1SchemaMappingWorkbench |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 78 |
| POST | `/sites` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 28 |
| POST | `/source-datasets/{sourceDatasetDefinitionId:guid}/cursor` | SchedulePhase1SourceDatasetNow |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 54 |
| POST | `/source-datasets/{sourceDatasetDefinitionId:guid}/schedule-now` | RunPhase1DueSourceImports |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 50 |
| POST | `/source-systems` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 26 |
| POST | `/source-systems` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 37 |
| POST | `/stage1/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 26 |
| POST | `/stage2/run` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 29 |
| POST | `/staging-records/bulk` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 38 |
| POST | `/steps` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 98 |
| POST | `/views/{id:guid}/activate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 57 |
| POST | `/views/{id:guid}/approve` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 54 |
| POST | `/views/{id:guid}/deactivate` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 60 |
| POST | `/views/{id:guid}/preview` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 48 |
| POST | `/views/preview` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 51 |
| POST | `/views` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 42 |
| POST | `/widgets/execute` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 91 |
| POST | `/widgets/query` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 30 |
| POST | `/workspace` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 26 |
| PUT | `/{kpiCode}/target` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/KpiEvaluationEndpoints.cs` | 81 |
| PUT | `/{slug}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs` | 26 |
| PUT | `/connection-profiles/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 39 |
| PUT | `/cost-assumptions` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs` | 33 |
| PUT | `/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 65 |
| PUT | `/definitions/{dashboardDefinitionId:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 49 |
| PUT | `/views/{id:guid}` |  |  |  | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 45 |

## Groups

| Group route | File | Line |
|---|---|---:|
| `/admin/connectors` | `Backend/PlantProcess.Api/Endpoints/Admin/ConnectorAdminEndpoints.cs` | 23 |
| `/admin/jobs` | `Backend/PlantProcess.Api/Endpoints/Admin/JobAdminEndpoints.cs` | 19 |
| `/admin/license` | `Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs` | 22 |
| `/admin/p03p04/completion` | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs` | 14 |
| `/admin/p03p04` | `Backend/PlantProcess.Api/Endpoints/Admin/P03P04MappingGenealogyEndpoints.cs` | 10 |
| `/admin/phase1` | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 18 |
| `/admin/phase2/pilot` | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2PilotReadinessEndpoints.cs` | 14 |
| `/admin/phase2` | `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 18 |
| `/admin/schema-configuration` | `Backend/PlantProcess.Api/Endpoints/Admin/SchemaConfigurationEndpoints.cs` | 32 |
| `/admin/schema-mapping` | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 35 |
| `/admin/two-stage-import` | `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 13 |
| `/admin/users` | `Backend/PlantProcess.Api/Endpoints/Admin/AdminProofEndpoints.cs` | 12 |
| `/admin/widgets` | `Backend/PlantProcess.Api/Endpoints/Admin/AdminProofEndpoints.cs` | 19 |
| `/admin` | `Backend/PlantProcess.Api/Endpoints/Admin/AdminEndpoints.cs` | 29 |
| `/analytics/correlations` | `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 20 |
| `/analytics/dashboard` | `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 16 |
| `/analytics/features` | `Backend/PlantProcess.Api/Endpoints/Analytics/FeatureEngineeringEndpoints.cs` | 11 |
| `/analytics/ml-readiness` | `Backend/PlantProcess.Api/Endpoints/Analytics/MlReadinessEndpoints.cs` | 9 |
| `/analytics/phase2` | `Backend/PlantProcess.Api/Endpoints/Analytics/Phase2InvestigationEndpoints.cs` | 15 |
| `/api/analytics/advanced` | `Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs` | 15 |
| `/api/analytics/advanced` | `Backend/PlantProcess.Api/Endpoints/Analytics/ProvenanceGuardedAdvancedResultsEndpoints.cs` | 19 |
| `/api/analytics/read-models` | `Backend/PlantProcess.Api/Endpoints/Analytics/ReadModelEndpoints.cs` | 13 |
| `/api/analytics/risk-calibration` | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskCalibrationEndpoints.cs` | 13 |
| `/api/analytics/risk-scores` | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskEvidenceEndpoints.cs` | 18 |
| `/api/analytics/simple` | `Backend/PlantProcess.Api/Endpoints/Analytics/SimpleAnalysisEndpoints.cs` | 19 |
| `/api/assistant` | `Backend/PlantProcess.Api/Endpoints/Assistant/AssistantEndpoints.cs` | 18 |
| `/api/kpis` | `Backend/PlantProcess.Api/Endpoints/Analytics/KpiEvaluationEndpoints.cs` | 18 |
| `/api/ml/foundation` | `Backend/PlantProcess.Api/Endpoints/Analytics/MlFoundationEndpoints.cs` | 16 |
| `/api/ml/learning` | `Backend/PlantProcess.Api/Endpoints/Analytics/MlLearningEndpoints.cs` | 13 |
| `/api/ml/providers` | `Backend/PlantProcess.Api/Endpoints/Analytics/MlProviderEndpoints.cs` | 11 |
| `/api/suggestions` | `Backend/PlantProcess.Api/Endpoints/Analytics/SuggestionEndpoints.cs` | 17 |
| `/api/value` | `Backend/PlantProcess.Api/Endpoints/Analytics/ValueEndpoints.cs` | 24 |
| `/api` | `Backend/PlantProcess.Api/Endpoints/DynamicContent/DynamicContentEndpoints.cs` | 10 |
| `/configuration` | `Backend/PlantProcess.Api/Endpoints/Configuration/ConfigurationEndpoints.cs` | 13 |
| `/data-quality` | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityEndpoints.cs` | 12 |
| `/data-quality` | `Backend/PlantProcess.Api/Endpoints/DataQuality/DataQualityScanEndpoints.cs` | 14 |
| `/demo-lifecycle` | `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 26 |
| `/demo` | `Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs` | 18 |
| `/dev` | `Backend/PlantProcess.Api/Endpoints/Development/DevSeedEndpoints.cs` | 10 |
| `/diagnostics` | `Backend/PlantProcess.Api/Endpoints/Diagnostics/DiagnosticsEndpoints.cs` | 27 |
| `/integration` | `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 19 |
| `/mapping-health` | `Backend/PlantProcess.Api/Endpoints/MappingHealth/Phase34MappingHealthEndpoints.cs` | 11 |
| `/materials` | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialEndpoints.cs` | 12 |
| `/materials` | `Backend/PlantProcess.Api/Endpoints/Materials/MaterialInvestigationEndpoints.cs` | 14 |
| `/pages` | `Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs` | 19 |
| `/phase2` | `Backend/PlantProcess.Api/Endpoints/Phase2/Phase2LifecycleProofEndpoints.cs` | 12 |
| `/phase4` | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 13 |
| `/phase5` | `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 23 |
| `/plant-layout` | `Backend/PlantProcess.Api/Endpoints/PlantLayout/PlantLayoutEndpoints.cs` | 15 |
| `/process` | `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 12 |
| `/quality` | `Backend/PlantProcess.Api/Endpoints/Quality/QualityEndpoints.cs` | 15 |
| `/readiness` | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 35 |
| `/reports/customer-demo` | `Backend/PlantProcess.Api/Endpoints/Reporting/CustomerDemoReportEndpoints.cs` | 12 |
| `/reports` | `Backend/PlantProcess.Api/Endpoints/Reporting/ReportingEndpoints.cs` | 12 |
| `/risk-scores` | `Backend/PlantProcess.Api/Endpoints/Analytics/RiskScoreEndpoints.cs` | 17 |
| `/validation` | `Backend/PlantProcess.Api/Endpoints/Validation/ValidationEndpoints.cs` | 10 |
| `/workflow/import` | `Backend/PlantProcess.Api/Endpoints/Integration/ImportWorkflowEndpoints.cs` | 12 |
| `/workflow` | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 31 |
