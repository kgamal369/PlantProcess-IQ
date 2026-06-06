# Pack F Closure Map

Generated: 2026-06-06T13:39:39.578Z

Marker: PPIQ_PACK_F1_EDGE_AGENT_CLOSURE_MAP

## Recommended Execution Order

1. Pack F-2 / T-066 — OT-safe edge agent one-way push backend.
2. Pack F-3 / T-067 — Edge agent packaging and deployment.
3. Pack F-4 / T-068 — Edge collector management UX.
4. Pack F-5 / T-071 — Edge tests/docs/regression and final closure.

## Acceptance by Task

### T-066 — OT-safe edge agent one-way push backend

- Pack step: Pack F-2
- Priority: 1
- Risk: HIGH
- Reason: The edge architecture must be safe before packaging or UX. It must prove read-only collection and outbound-only push.

Acceptance:

- One canonical edge agent/collector contract exists
- Agent config supports outbound PlantProcess IQ API endpoint
- Agent never opens inbound OT-facing listener
- Agent supports read-only collection profile
- Agent supports local spool/queue model
- Agent supports heartbeat payload
- Agent supports bounded batch push payload
- Server/API accepts heartbeat and batch push
- OT-safety documentation exists
- Backend build remains green

### T-067 — Edge agent packaging and deployment

- Pack step: Pack F-3
- Priority: 2
- Risk: MEDIUM
- Reason: After the safety contract exists, package it for repeatable demo/customer deployment without mixing local/server DB or unsafe inbound assumptions.

Acceptance:

- Sample edge-agent configuration exists
- Packaging/deployment script exists
- Docker or service-wrapper mode is documented
- Install/uninstall or run-local commands are documented
- Secrets are referenced, not hardcoded
- Environment separation is explicit
- Deployment report and validator exist

### T-068 — Edge collector management UX

- Pack step: Pack F-4
- Priority: 3
- Risk: MEDIUM
- Reason: The UI should display edge collector readiness, heartbeat, queue, and deployment status only after backend and packaging contracts exist.

Acceptance:

- UI route exists for edge collectors
- UI shows collector profile
- UI shows heartbeat status
- UI shows queue/spool status
- UI shows one-way push status
- UI shows deployment command guidance
- UI is honest about OT-safe outbound-only behavior
- Frontend build remains green

### T-071 — Edge tests, docs, regression, final closure

- Pack step: Pack F-5
- Priority: 4
- Risk: LOW
- Reason: After backend, packaging, and UI are complete, lock Pack F with validators, documentation, build regression, and scorecard closure.

Acceptance:

- Pack F backend validator exists
- Pack F packaging validator exists
- Pack F UI validator exists
- Pack F OT-safety contract snapshot exists
- Pack F regression wrapper exists
- Pack F final scorecard bridge exists
- Backend and frontend build remain green
- Remaining below-90 tasks drop to zero
