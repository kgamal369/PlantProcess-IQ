# Phase 1/2 fallback secret scan report

Generated: 2026-06-06 09:43:29

Fallback scanner scope:

- Scans active config/runtime files, deploy compose files, deploy scripts, and env examples.
- Skips local-only env files and generated documentation.
- Skips source-code implementation files to avoid false positives on property names.
- Uses gitleaks automatically when installed.

Scanned files: 35
Findings: 7

## Findings
- deploy\.env.example:4 -> hardcoded connection-string password [****]
- deploy\server\.env.example:33 -> hardcoded connection-string password [SE****_I]
- deploy\server\.env.example:56 -> hardcoded connection-string password [SE****_I]
- deploy\server\.env.example:71 -> hardcoded connection-string password [SE****_I]
- deploy\server\.env.example:83 -> hardcoded connection-string password [SE****_I]
- deploy\server\.env.example:84 -> hardcoded connection-string password [SE****_I]
- Frontend\PlantProcess.Web\.env.example:14 -> hardcoded connection-string password [****]
