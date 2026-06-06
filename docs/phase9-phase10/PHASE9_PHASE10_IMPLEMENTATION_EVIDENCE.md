# PlantProcess IQ Phase 9 + Phase 10 Evidence

Generated: 2026-06-06T08:54:29.678Z

## Repair note

The Phase 9/10 validator now uses the real P10 filenames present in this repository:

- `Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql`
- `Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql`

## Phase 9 — Identity, SSO/SCIM and UI quality matrix

- Enterprise SSO/SCIM schema foundation: GREEN source evidence.
- OIDC runtime RS256/JWKS certification: GREEN source evidence.
- SCIM deactivate / login denial proof: GREEN source evidence.
- Cross-browser UI matrix: GREEN source evidence.

## Phase 10 — Signed licensing and website commercial acceptance

- Signed license anti-tamper baseline: GREEN source evidence.
- Strict Ed25519 verified license source of truth: GREEN source evidence.
- Website commercial acceptance: GREEN source evidence.

## Validation command

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\phase9-phase10\Invoke-Phase9Phase10Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunFrontendBuild -RunBackendBuild -RunWebsiteValidation
```

## Boundary

This is source/build/website validation. SQL application remains explicit because local Windows PostgreSQL and server Docker PostgreSQL are separate deployment states.

## Phase 10 demo lead marker repair

- Added `PPIQ_PHASE10_DEMO_LEAD_CAPTURE` evidence marker.
- Added stable lead storage contract key: `ppiq.website.demoLeads.v1`.
- Component: `Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx`.

## Phase 10 website overclaim repair

- Marker: `PPIQ_PHASE10_WEBSITE_OVERCLAIM_REPAIR`.
- Removed unsafe absolute commercial claims such as `guaranteed root cause`.
- Replaced them with evidence-backed, decision-support language.
- Guard: `tools/phase9-phase10/website-phase10-guard.cjs`.
