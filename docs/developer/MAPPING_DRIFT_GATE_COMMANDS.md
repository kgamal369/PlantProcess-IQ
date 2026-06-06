# Mapping and Drift Gate Commands

Marker: PPIQ_PACK_A5_T035_MAPPING_DRIFT_DOCS

## Pack A documentation validation

```powershell
node .\tools\pack-a\validate-pack-a-t035-mapping-drift-docs.cjs
```

## Full Pack A final closure

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\pack-a\Invoke-PackA-FinalClosure-WithBridges.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
```

## Task closure gate

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\task-closure\Invoke-T001-T071-TaskClosureGate.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
```

## Pack D route-contract validation

```powershell
node .\tools\pack-d\validate-pack-d-route-contract-snapshot.cjs
```

## CI certification wrapper

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\ci\Invoke-PPIQ-Certification.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
```

## Build validation

```powershell
dotnet build .\Backend
cd .\Frontend\PlantProcess.Web
npm run build
```
