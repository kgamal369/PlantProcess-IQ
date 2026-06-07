# T-026 GenericSchemaMappingEndpoints Split

Marker: PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT

## Result

The previous runtime shim has been retired and the endpoint has been decomposed into cohesive partial files.

## Files

- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Resolver.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Joins.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Kpi.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Execution.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.SqlHelpers.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Contracts.cs

## Validation

Run:

    node tools/phase5/validate-t026-generic-schema-mapping-split.cjs
    dotnet build Backend

## Backup

.phase5_backup/t026_generic_schema_mapping_split_20260607_123240
