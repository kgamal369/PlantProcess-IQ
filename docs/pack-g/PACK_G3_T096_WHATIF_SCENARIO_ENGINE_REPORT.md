# Pack G-3 T-096 What-if Scenario Engine Report

Generated: 2026-06-06T14:40:49.287Z

Marker: PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE

## Scope

Adds deterministic what-if simulation backend, API route, frontend API client, and scenario simulation page. The engine is projection-only, bounded by observed data envelope, and blocks weak or missing evidence.

## Changed files

| File | Status |
|---|---|
| `Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs` | WRITTEN |
| `Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs` | WRITTEN |
| `Backend/PlantProcess.Api/Program.cs` | PATCHED |
| `Frontend/PlantProcess.Web/src/api/phase15Advisory.ts` | WRITTEN |
| `Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx` | WRITTEN |
| `Frontend/PlantProcess.Web/src/App.implementation.tsx` | PATCHED |
| `Frontend/PlantProcess.Web/src/components/AppLayout.tsx` | PATCHED |
| `tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs` | WRITTEN |
| `tools/task-closure/ppiq-pack-g3-scorecard-bridge.cjs` | WRITTEN |
| `tools/pack-g/Invoke-PackG-Phase15Regression.ps1` | WRITTEN |
