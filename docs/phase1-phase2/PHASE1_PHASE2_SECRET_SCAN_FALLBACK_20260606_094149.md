# Phase 1/2 fallback secret scan report

Generated: 2026-06-06 09:41:49

Fallback scanner scope:

- Scans active config/runtime files only.
- Skips source-code implementation files because words like Password, AccessToken, ClientSecret, and SigningKey are normal property/variable names.
- Uses gitleaks automatically when installed.

Scanned files: 34
Findings: 15

## Findings
- deploy\airgap\docker-compose.airgap.yml:8 -> possible hardcoded ConnectionStrings__PlantProcessDb [Ho****es]
- deploy\dr\ha-reference-compose.yml:23 -> possible hardcoded ConnectionStrings__PlantProcessDb [Ho****ry]
- deploy\dr\ha-reference-compose.yml:28 -> possible hardcoded ConnectionStrings__PlantProcessDb [Ho****ry]
- env\profiles\local.env:15 -> hardcoded connection-string password [****]
- env\profiles\local.env:18 -> hardcoded connection-string password [****]
- env\profiles\local.env:18 -> possible hardcoded ConnectionStrings__PlantProcessDb [Ho****.1]
- env\profiles\local.env:18 -> possible hardcoded Password [pl****23]
- env\profiles\local.env:19 -> hardcoded connection-string password [****]
- env\profiles\local.env:19 -> possible hardcoded ConnectionStrings__DefaultConnection [Ho****.1]
- env\profiles\local.env:19 -> possible hardcoded Password [pl****23]
- env\profiles\local.env:33 -> hardcoded connection-string password [E2****mi]
- env\profiles\local.env:35 -> hardcoded connection-string password [E2****mi]
- env\profiles\local.env:39 -> hardcoded connection-string password [E2****mi]
- env\profiles\local.env:52 -> hardcoded connection-string password [E2****mi]
- Frontend\PlantProcess.Web\.env.local:6 -> hardcoded connection-string password [E2****mi]
