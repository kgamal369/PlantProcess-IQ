# PlantProcess IQ Pack D Evidence

## Pack D-1 Backend split audit + route contract snapshot

- Marker: `PPIQ_PACK_D1_BACKEND_SPLIT_AUDIT_ROUTE_SNAPSHOT`.
- Captured backend god-file split audit for T-054 and T-055.
- Captured route contract snapshot before backend endpoint refactor.
- Captured service public-method surface snapshot before service split.
- Added route-contract validator.
- Added backend thinness validator.
- Added backend regression wrapper.

Generated artifacts:

- `docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.md`
- `docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.json`
- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`
- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`
- `docs/pack-d/PACK_D1_SERVICE_SURFACE_SNAPSHOT.json`
- `tools/pack-d/validate-pack-d-route-contract-snapshot.cjs`
- `tools/pack-d/validate-pack-d-backend-thinness.cjs`
- `tools/pack-d/Invoke-PackD-BackendRegression.ps1`

## Pack D-1B Target path correction

- Marker: `PPIQ_PACK_D1B_TARGET_PATH_CORRECTION`.
- Corrected `Phase1WorkflowTruthEndpoints.cs` path from `Endpoints/Workflow` to `Endpoints/Admin`.
- Corrected `ConnectorConfigurationService.cs` path from `PlantProcess.Infrastructure/Configuration` to `PlantProcess.Application/Integration/Services/Connectors`.
- Rewrote Pack D backend thinness validator using the corrected target registry.
- Generated `docs/pack-d/PACK_D_TARGETS.json`.
- Generated `docs/pack-d/PACK_D1_TARGET_PATH_CORRECTION.md`.

## Pack D-2A T-054 route-preserving split

- Marker: `PPIQ_PACK_D2A_T054_ROUTE_PRESERVING_SPLIT`.
- Made `GenericSchemaMappingEndpoints.cs` thin.
- Made `Phase1WorkflowTruthEndpoints.cs` thin.
- Moved implementations to runtime sibling files in the same endpoint folder.
- Route contracts remain protected by `validate-pack-d-route-contract-snapshot.cjs`.
- Generated report: `docs/pack-d/PACK_D2A_T054_ROUTE_PRESERVING_SPLIT_REPORT.md`.

Important: runtime files are compatibility anchors. They preserve behavior now and should be semantically decomposed later for long-term code hygiene.

## Pack D-3A T-055 route/service-preserving split

- Marker: `PPIQ_PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT`.
- Made `WorkflowEndpoints.cs` thin.
- Made `ConnectorConfigurationService.cs` thin.
- Moved implementations to runtime sibling files.
- Route contracts remain protected by `validate-pack-d-route-contract-snapshot.cjs`.
- Generated report: `docs/pack-d/PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT_REPORT.md`.

Important: runtime files are compatibility anchors. They preserve behavior now and should be semantically decomposed later for long-term code hygiene.
