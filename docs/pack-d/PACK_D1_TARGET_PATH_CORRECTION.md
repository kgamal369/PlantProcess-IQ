# Pack D-1B Target Path Correction

Generated: 2026-06-06T11:15:44.821Z

## Why this correction exists

Pack D-1 captured the route snapshot correctly, but two target paths were stale:

- `Phase1WorkflowTruthEndpoints.cs` is under `Endpoints/Admin`, not `Endpoints/Workflow`.
- `ConnectorConfigurationService.cs` is under `PlantProcess.Application/Integration/Services/Connectors`, not `PlantProcess.Infrastructure/Configuration`.

## Correct Pack D target registry

| Task | Name | File | Lines | Limit | Status |
|---|---|---|---:|---:|---|
| T-054 | GenericSchemaMappingEndpoints | `Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs` | 1389 | 500 | **SPLIT_REQUIRED** |
| T-054 | Phase1WorkflowTruthEndpoints | `Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs` | 1368 | 500 | **SPLIT_REQUIRED** |
| T-055 | WorkflowEndpoints | `Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs` | 1031 | 500 | **SPLIT_REQUIRED** |
| T-055 | ConnectorConfigurationService | `Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs` | 1247 | 500 | **SPLIT_REQUIRED** |

## Next step

Run Pack D-2 against T-054:

1. `GenericSchemaMappingEndpoints.cs`
2. `Phase1WorkflowTruthEndpoints.cs`

After every split, validate route contracts:

```powershell
node .\tools\pack-d\validate-pack-d-route-contract-snapshot.cjs
dotnet build .\Backend
```
