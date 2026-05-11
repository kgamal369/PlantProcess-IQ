# PlantProcess IQ - Sprint 3 Current Status

## Status Date

2026-05-10

## Purpose

This document freezes the current Sprint 3 implementation state before introducing the Application layer and process-engine foundation.

## Current Technical State

The current implementation contains a strong canonical backend foundation:

- API project with endpoint groups for:
  - Health
  - Plant layout
  - Configuration
  - Integration
  - Materials
  - Material investigation
  - Process
  - Quality
  - Risk scores
  - Data quality
  - Workflow
  - Validation
  - Development/database validation
- Domain project with canonical manufacturing entities.
- Infrastructure project with EF Core configurations, PostgreSQL persistence, migrations, and concurrency configuration.
- Worker project with a placeholder background service.

## Current Strengths

| Area | Status |
|---|---|
| Canonical data model | Good foundation |
| Recursive area hierarchy | Implemented |
| Recursive equipment hierarchy | Implemented |
| Source system registry | Implemented |
| Import batch metadata | Implemented |
| Mapping definition metadata | Implemented |
| Material genealogy | Implemented |
| Process step and parameter observation model | Implemented |
| Quality event and defect catalog model | Implemented |
| Data-quality issue model | Implemented |
| Risk score model | Implemented |
| Validation endpoint | Initial version implemented |
| Advanced demo seed | Available |

## Current Gaps

| Gap | Description |
|---|---|
| Application layer | Missing. Endpoint files currently contain too much orchestration and EF logic. |
| Real ingestion engine | Missing. Current import batch and mapping definitions are metadata only. |
| Raw/staging persistence | Missing. Source rows are not yet stored exactly as received. |
| Mapping executor | Missing. Mapping JSON is stored but not executed. |
| Workflow service layer | Missing. Workflow endpoint is a facade, not yet a process engine. |
| Worker jobs | Missing. Worker currently has placeholder loop only. |
| Correlation engine | Missing. |
| ML/prediction engine | Missing. |
| Reporting/dashboard API | Missing. |
| Production-grade logging | Partial. Basic request logging exists but needs correlation IDs and separated log files. |

## Next Phase

Phase 1 introduces `PlantProcess.Application`.

The goal is not to change business behavior yet. The goal is to create a clean application-service foundation so later phases can move endpoint logic into services and build the real ingestion/mapping/process engine.

## Guardrails

- Do not use employer/customer confidential data.
- Keep steel-specific words as configuration/demo metadata only.
- Do not write to MES, Level 2, SCADA, PLC, ERP, QMS, or historian systems.
- Keep PlantProcess IQ as a read-only intelligence layer in MVP.
- Use suspected contributor / risk indicator wording, not guaranteed root cause.