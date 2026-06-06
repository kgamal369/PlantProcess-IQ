# Pack A Closure Map

Generated: 2026-06-06T11:25:27.707Z

## Recommended Order

1. **Pack A-2 / T-010** — Archive landed run-once tooling scripts.
2. **Pack A-3 / T-028** — Wire gate-exit tests into CI certification.
3. **Pack A-4 / T-007** — De-duplicate Jenkinsfile and TelemetryIngestionWorker.
4. **Pack A-5 / T-035** — Mapping and drift developer docs.

## Acceptance by Task

### T-010 — Archive landed run-once tooling scripts

- Pack step: **Pack A-2**
- Priority: **1**
- Risk: **LOW**
- Reason: Is 0% and mostly file organization/documentation if archive is done with manifest and no active validators moved.

Acceptance:

- Create tools/archive/run-once or tools/_archive/run-once folder
- Move or copy landed one-off apply/repair/continue scripts into archive
- Create archive manifest with original path, reason, and date
- Keep reusable validators in active tools folders
- Task closure score for T-010 >= 90

### T-028 — Wire all gate-exit tests into CI certification stage

- Pack step: **Pack A-3**
- Priority: **2**
- Risk: **MEDIUM**
- Reason: CI wiring should be done after Pack B/D validators exist and are green.

Acceptance:

- Certification stage references frontend build
- Certification stage references backend build/test
- Certification stage references Phase 5/6 validation
- Certification stage references Pack B validation
- Certification stage references Pack D validation
- Certification stage references T001-T071 task closure gate
- Task closure score for T-028 >= 90

### T-007 — De-duplicate Jenkinsfile and TelemetryIngestionWorker

- Pack step: **Pack A-4**
- Priority: **3**
- Risk: **MEDIUM**
- Reason: Dedup should happen after CI certification target is clear.

Acceptance:

- Single canonical Jenkinsfile/pipeline definition or clear wrapper/redirect
- Single canonical TelemetryIngestionWorker class definition
- No duplicate hosted-service worker registrations
- Architecture/build tests remain green
- Task closure score for T-007 >= 90

### T-035 — Mapping and drift developer docs

- Pack step: **Pack A-5**
- Priority: **4**
- Risk: **LOW**
- Reason: Documentation should reference final validators and current mapping/drift reality.

Acceptance:

- Add mapping lifecycle developer guide
- Add drift detection developer guide/runbook
- Document business-key dictionary validation
- Document safe SQL resolver behavior
- Document troubleshooting and gate commands
- Task closure score for T-035 >= 90
