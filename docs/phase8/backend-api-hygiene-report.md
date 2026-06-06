# PlantProcess IQ Phase 8 — Backend API Hygiene Report

| File | Lines | Routes | Data-access hits | Risk |
|---|---:|---:|---:|---|
| `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 1389 | 8 | 5 | critical-god-file |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 1368 | 12 | 2 | critical-god-file |
| `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 1031 | 18 | 0 | critical-god-file |
| `Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs` | 1247 | 0 | 0 | critical-god-file |
| `Backend/PlantProcess.Api/Endpoints/Admin/Phase2OperationEndpoints.cs` | 888 | 11 | 7 | large-file |
| `Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs` | 620 | 7 | 7 | watch |
| `Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs` | 752 | 9 | 0 | large-file |
| `Backend/PlantProcess.Api/Endpoints/Process/ProcessEndpoints.cs` | 764 | 14 | 0 | large-file |

This is the safe pre-split guard: current large files are tracked, unknown new oversized backend files are blocked, and a source-route contract snapshot is created before destructive refactoring.