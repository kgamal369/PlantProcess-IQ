# OT-Safe Edge Agent Regression Guide

Marker: PPIQ_PACK_F5_EDGE_TESTS_DOCS_REGRESSION

## Purpose

This guide locks Pack F after the OT-safe edge backend, deployment package, and management UX are complete.

## Regression gates

1. Pack F-1 closure-map validator.
2. Pack F-2 backend OT-safe edge validator.
3. Pack F-3 packaging/deployment validator.
4. Pack F-4 edge collector UX validator.
5. Pack F-5 final regression validator.
6. Backend build.
7. Frontend build.
8. Final task-closure bridge.

## Commands

```powershell
node .\tools\pack-f\validate-pack-f-closure-map.cjs
node .\tools\pack-f\validate-pack-f-t066-edge-backend.cjs
node .\tools\pack-f\validate-pack-f-t067-edge-packaging.cjs
node .\tools\pack-f\validate-pack-f-t068-edge-collector-ux.cjs
node .\tools\pack-f\validate-pack-f-t071-edge-regression.cjs
powershell -ExecutionPolicy Bypass -File .\tools\pack-f\Invoke-PackF-FinalRegression.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunBuilds
```

## Non-negotiable safety rule

Do not introduce any inbound OT listener, write path to PLC/SCADA/MES/source systems, or fake claim of direct production control.
