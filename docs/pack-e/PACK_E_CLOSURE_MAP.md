# Pack E Closure Map

Generated: 2026-06-06T12:57:56.055Z

Marker: PPIQ_PACK_E1_HISTORIAN_CLOSURE_MAP

## Recommended Execution Order

1. Pack E-2 / T-060 — GA historian connector backend.
2. Pack E-3 / T-063 — Historian connector UI register/test/map.
3. Pack E-4 / T-064 — Historian tests/docs/regression and scorecard bridge.

## Acceptance by Task

### T-060 — GA historian connector backend

- Pack step: Pack E-2
- Priority: 1
- Risk: MEDIUM
- Reason: Backend connector behavior must exist before UI and tests can be meaningful.

Acceptance:

- One canonical GA historian provider is declared honestly
- Provider appears in connector provider-types endpoint
- Backend supports typed historian connection test
- Backend supports safe tag/point browse or equivalent metadata browse
- Backend supports bounded read/sample flow
- Historian source can connect to schema/mapping workflow
- Errors are explicit and demo-safe, not fake success
- Backend build remains green

### T-063 — Historian connector UI register/test/map

- Pack step: Pack E-3
- Priority: 2
- Risk: MEDIUM
- Reason: UI should only expose what the backend supports and must avoid vendor-overclaim.

Acceptance:

- Connector UI includes historian provider option
- UI can create/register historian source configuration
- UI can run connection test and display honest result
- UI can browse historian tags/points or show supported metadata result
- UI can send selected tags/points into mapping/schema workflow
- UI copy clearly states supported demo/GA connector scope
- Frontend build remains green

### T-064 — Historian tests, docs, regression, scorecard bridge

- Pack step: Pack E-4
- Priority: 3
- Risk: LOW
- Reason: After backend and UI are present, lock behavior through tests/docs/regression.

Acceptance:

- Backend tests cover provider registration and failure-safe connection test
- Frontend/build smoke covers historian UI surface
- Docs explain supported historian connector scope
- Docs explain fake/demo vs real connector boundaries
- Pack E validator exists
- Pack E regression wrapper exists
- Task closure bridge marks T-060/T-063/T-064 green only after validators pass
