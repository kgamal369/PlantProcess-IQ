# PlantProcess IQ Ground Truth Hotfix Evidence

Generated: 2026-06-06T09:09:32.892Z

## Applied safe hotfixes

- `HOTFIX-DOC-001` — Normalized literal escaped newline sequences in Phase 9/10 evidence markdown.
- `HOTFIX-WEB-001` — Removed unsafe website/app overclaims and replaced them with evidence-backed decision-support language.
- `HOTFIX-VAL-001` — Hardened Phase 9/10 validator so string paths are handled as single-file arrays and directories are not read as files.

## Guardrails

- This pack does not silently apply database SQL.
- Local Windows PostgreSQL and server Docker PostgreSQL remain separate deployment states.
- Scores are evidence-based; large features without runtime/customer proof are not marked as done.
- Existing tracked god-files remain backlog unless actually refactored.

## Ground Truth P03/P04 + god-file policy repair

- Marker: `PPIQ_GROUND_TRUTH_GODFILE_POLICY_REPAIRED`.
- Created `tools/phase3-phase4/Invoke-Phase3Phase4Validation.ps1`.
- Created `tools/phase3-phase4/validate-phase3-phase4-source.cjs`.
- Added Phase 3/4 validation to the Ground Truth wrapper.
- Excluded backup folders and generated EF migration/model snapshot files from active god-file failures.
- Preserved active oversized files as tracked technical debt instead of pretending they are clean.

## Phase 3/4 validator marker-policy repair

- Marker: `PPIQ_PHASE34_VALIDATOR_MARKER_POLICY_REPAIR`.
- Fixed validator policy: `ppiq_walk_genealogy` is validated in `311_p03_p04_fix_genealogy_walk_and_safe_sql.sql`, not in `313_p03_p04_completion_pack_a_hotfix.sql`.
- `313` is now validated against its real purpose: material investigation ambiguity, rollback proof, and business-key duplicate classification.
- No product code was changed.

## Phase 3/4 validator frontend-path policy repair

- Marker: `PPIQ_PHASE34_VALIDATOR_FRONTEND_PATH_POLICY_REPAIR`.
- Fixed validator path policy to include the active frontend implementation: `Frontend/PlantProcess.Web/src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx`.
- Validator now checks real P03/P04 endpoint usage instead of requiring the exact visible words `P03/P04`, `mapping`, and `genealogy` inside the barrel export file.
- No product code was changed.
