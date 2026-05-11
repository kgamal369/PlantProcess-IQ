# PlantProcess IQ — Phase D, E, F and G Implementation Guide

## Phase D — Import Workflow Orchestration Engine

Adds:

- `ImportWorkflowService`
- `ImportBatchQueueProcessorService`
- `POST /workflow/import/run`
- `POST /workflow/import/process-queue`
- worker automation for import queue + data-quality scan

Flow:

```text
ImportBatch
  -> StagingRecords
  -> MappingExecutionService
  -> Canonical model
  -> DataQualityService.RunFullScanAsync
  -> ImportBatch Completed / Failed
```

## Phase E — Application Layer Completion

Adds:

- Application-layer query services for Plant Layout
- Application-layer enriched quality-event query service
- page/pageSize paging for new read endpoints
- equipment hierarchy traversal
- material-by-equipment reverse lookup
- defect catalog navigation property

## Phase F — Synthetic Dataset Expansion

Adds:

- `database/seeds/003_additional_demo_seed.sql`
- `tools/generate_synthetic_plantprocessiq.py`

The Python generator creates CSV files with hidden patterns for correlation validation.

## Phase G — Correlation Engine

Adds:

- `CorrelationResult` domain entity
- correlation result EF config
- `CorrelationService`
- `GET /analytics/correlations/parameter-defect`
- `GET /analytics/correlations/equipment-defect-rate`
- `GET /analytics/correlations/operation-defect-rate`
- `GET /analytics/correlations/materials/{materialUnitId}/context`

Important: correlation output must be positioned as suspected contributors, not guaranteed root cause.

## EF Migration Commands

```powershell
dotnet ef migrations add AddPhaseGCorrelationResults --project PlantProcess.Infrastructure --startup-project PlantProcess.Api
dotnet ef database update --project PlantProcess.Infrastructure --startup-project PlantProcess.Api
```

If you do not want to generate the migration immediately, apply:

```text
database/migrations/manual/20260511_phase_g_correlation_results.sql
```

## Recommended Validation Sequence

```powershell
dotnet restore
dotnet build

dotnet ef database update --project PlantProcess.Infrastructure --startup-project PlantProcess.Api

psql -h localhost -p 5432 -U plantprocess -d plantprocessiq -f database/seeds/003_additional_demo_seed.sql

python tools/generate_synthetic_plantprocessiq.py --rows 100000 --out data/synthetic/phase_f
```

## Key APIs to Test

```http
POST /workflow/import/run
POST /workflow/import/process-queue
POST /data-quality/scan/run
GET  /plant-layout/equipment/{equipmentId}/materials
GET  /quality/events?page=1&pageSize=50
GET  /analytics/correlations/parameter-defect?parameterCode=CastingSpeed&defectType=SurfaceCrack&bins=8
GET  /analytics/correlations/equipment-defect-rate?defectType=SurfaceCrack
GET  /analytics/correlations/operation-defect-rate?defectType=SurfaceCrack
```
