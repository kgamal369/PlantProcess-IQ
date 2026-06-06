# Phase 1/2 fallback secret scan report

Generated: 2026-06-06 11:54:48

Fallback scanner scope:

- Scans production/default app config, deploy templates, airgap/DR templates, env examples, and runtime scripts.
- Skips Development config, demo-source compose files, reference Keycloak compose, local env files, tests, generated docs, and backups.
- Skips source-code implementation files to avoid false positives on property/variable names.
- Uses gitleaks automatically when installed.

Scanned files: 17
Findings: 0

No production-runtime hardcoded secrets found by fallback scanner.
