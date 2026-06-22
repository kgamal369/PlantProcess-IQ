# PlantProcess IQ — Phase 1 + Phase 2 Implementation Evidence

Generated: 2026-06-06 09:33:48

## Scope

This implementation pack covers:

### P01 — Critical Fixes & Production-Safety Baseline

- T-001 PKL MSSQL N-literal corruption fixed.
- T-002 Yard CSV whitespace fixed and CSV parser trim hardened.
- T-003 Dev seed endpoint registration locked to Development.
- T-004 PostgreSQL compose binding patched to loopback where direct port binding exists.
- T-005 NotImplemented/null-object DI binding regression test added.
- T-006 Duplicate frontend page cleanup script executed.
- T-007 Jenkinsfile/Telemetry duplicate cleanup script added/executed conservatively.
- T-008 Architecture dependency-direction tests added.
- T-009 BOM guard script and test added.
- T-010 Run-once archive gate added.
- T-011 Combined Phase 1 validation runner added.
- T-012 Secret scan gate added with gitleaks config and fallback scanner.

### P02 — Realism Demo Dataset Through the HMI

- T-013 Realism-scale source data generated across eight demo source systems.
- T-014 C-0044170 / H-3361 / S-0044170 reference thread embedded.
- T-015 Deliberate imperfection fixtures added:
  - blank quality labels,
  - orphan coil,
  - unmapped defect code,
  - EDGE_CRACK fixture.
- T-016 Demo lifecycle contract test added.
- T-017 No-facade frontend metric source scan added.
- T-018 Realism source validation added.
- T-019 Combined validation runner covers Phase 2 fixtures and regression tests.

## Important product guard

PlantProcess IQ remains a generic manufacturing quality-intelligence platform.
Steel names in this pack are demo metadata only, not product hard-coding.

## Validation command

`powershell
powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Invoke-Phase1Phase2Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
C:\Workspace\PlantProcess-IQ\.phase1_phase2_backup\20260606_093149