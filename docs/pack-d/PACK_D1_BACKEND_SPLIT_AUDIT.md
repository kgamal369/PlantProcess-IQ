# Pack D-1 Backend Split Audit

Generated: 2026-06-06T11:14:12.362Z

## Purpose

Pack D-1 is audit-only. It prepares Pack D-2/D-3 by recording current backend god-files, route contracts, and service public method surfaces before any source refactor.

## Target blocker files

| Task | File | Lines | Limit | Status | Routes | Public/internal methods |
|---|---|---:|---:|---|---:|---:|
| T-054 | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 1389 | 500 | **SPLIT REQUIRED** | 8 | 9 |
| T-054 | `Backend/PlantProcess.Api/Endpoints/Workflow/Phase1WorkflowTruthEndpoints.cs` | 0 | 500 | **SPLIT REQUIRED** | 0 | 0 |
| T-055 | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 1031 | 500 | **SPLIT REQUIRED** | 18 | 16 |
| T-055 | `Backend/PlantProcess.Infrastructure/Configuration/ConnectorConfigurationService.cs` | 0 | 500 | **SPLIT REQUIRED** | 0 | 0 |

## Recommended split order

1. `GenericSchemaMappingEndpoints.cs` — split by mapping metadata, dictionary, version validation, preview/proof, and route registration.
2. `Phase1WorkflowTruthEndpoints.cs` — split by lifecycle proof, evidence reporting, and route registration.
3. `WorkflowEndpoints.cs` — split by command/query/proof routes.
4. `ConnectorConfigurationService.cs` — split by provider registry, connection testing, schema discovery, persistence, and DTO mapping.

## Large backend files found

| File | Lines |
|---|---:|
| `Backend/PlantProcess.Infrastructure/Migrations/20260603203438_SyncModel_20260603.Designer.cs` | 4645 |
| `Backend/PlantProcess.Infrastructure/Migrations/PlantProcessDbContextModelSnapshot.cs` | 4642 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260530130708_Phase03_ModelSnapshotSync.Designer.cs` | 4624 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260524110553_HardenAuditLogEntrySchemaAndImmutability.Designer.cs` | 4584 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260525082545_Phase1WorkflowTruth.Designer.cs` | 4584 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260524061000_AddAuditLogAndPerSourceScheduling.Designer.cs` | 4575 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260519075125_TodayAddRiskScoreExplanationAndCriticalIndexes.Designer.cs` | 4440 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260519083512_AddCriticalQueryIndexes.Designer.cs` | 4440 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260521182857_Fix_WidgetCode_UniqueIndex_PerDashboard.Designer.cs` | 4440 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260521221629_Fix_PendingModelChanges.Designer.cs` | 4440 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260519064446_AddRiskScoreExplanationAndCriticalIndexes.Designer.cs` | 4427 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260516094248_AddJobDefinition.Designer.cs` | 4261 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260516080826_AddPhase3ConnectorFoundation.Designer.cs` | 3820 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260512085506_AddDashboardDefinitions.Designer.cs` | 3357 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260511124245_AddModelRegistry.Designer.cs` | 3094 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260511115023_AddPhaseGCorrelationResults.Designer.cs` | 2974 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260511092313_AddStagingRecordEntity.Designer.cs` | 2868 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260511081726_AddOperationDefinitionIdToProcessStep.Designer.cs` | 2741 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260510132030_Phase3_ModelConsistencyFixes.Designer.cs` | 2734 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260510091155_Add2ndDayDomain.Designer.cs` | 2713 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260507092434_AddPlantLayoutAndIntegrationMasterData.Designer.cs` | 1929 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260507074459_InitialCanonicalModel.Designer.cs` | 1497 |
| `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 1389 |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 1368 |
| `Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs` | 1247 |
| `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 1031 |
| `Backend/PlantProcess.Api/EnterpriseIdentity/V5EnterpriseIdentityEndpoints.cs` | 958 |
| `Backend/PlantProcess.Application/Services/Readiness/ApplicationReadinessService.cs` | 930 |
| `Backend/PlantProcess.Api/PlantConnectors/V5ConnectorRuntimeCertificationEndpoints.cs` | 906 |
| `Backend/PlantProcess.Application/Dashboarding/Services/Dashboards/DashboardDefinitionService.cs` | 903 |
| `Backend/PlantProcess.Application/Integration/Services/Mapping/MappingExecutionService.cs` | 900 |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 888 |
| `Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs` | 880 |
| `Backend/PlantProcess.Api/EnterpriseSsoScim/V5EnterpriseSsoScimEndpoints.cs` | 858 |
| `Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs` | 851 |
| `Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardQueryService.cs` | 800 |
| `Backend/PlantProcess.Infrastructure/Migrations/20260507074459_InitialCanonicalModel.cs` | 792 |
| `Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs` | 786 |
| `Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs` | 773 |
| `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 764 |
| `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 752 |
| `Backend/PlantProcess.Api/OutboundLeadSystem/V5OutboundLeadSystemEndpoints.cs` | 737 |
| `Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs` | 720 |
| `Backend/PlantProcess.Api/VisualMapper/V5VisualMapperEndpoints.cs` | 717 |
| `Backend/PlantProcess.Api/Endpoints/Integration/IntegrationEndpoints.cs` | 695 |
| `Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs` | 694 |
| `Backend/PlantProcess.Api/AssistantGateway/V5AssistantGateway.cs` | 654 |
| `Backend/PlantProcess.Api/PlantConnectors/V5PlantConnectorEndpoints.cs` | 642 |
| `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 620 |
| `Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs` | 603 |

## Route-contract guard

Route snapshot written to:

- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`
- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`

Before and after each Pack D split, run:

```powershell
node .\tools\pack-d\validate-pack-d-route-contract-snapshot.cjs
```
