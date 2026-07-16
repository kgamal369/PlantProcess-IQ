# PlantProcess IQ Automated Journey Certification v1

This pack turns `concept.md` and `PPIQ_Journey_Walk_M1-11_v2.md` into executable pre-manual-walk evidence.

## Acceptance model

Each journey capability is scored from four evidence layers:

- Backend unit/integration evidence: 25 points.
- Frontend build/component/static evidence: 25 points.
- Full-stack Playwright evidence: 35 points.
- Runtime UI/UX evidence: 15 points.

A capability passes at 75/100. Certification requires at least 13 of 16 capabilities passing, including steps 1-10, step 14, step 15 and UI-4. Skipped critical tests never count as passed.

## UI/UX quality rules

The runtime suite checks every journey page for:

- exactly one visible H1;
- title/action non-overlap;
- no horizontal page overflow;
- contained tables;
- named buttons and icon-only controls;
- vertically aligned table cells;
- folded long technical output;
- concise page-header copy;
- no visible phase/task/fixture wording;
- desktop and compact screenshots.

The static audit additionally scores design-system adoption, raw controls, inline styling, asynchronous states, progressive disclosure and responsive ownership.

## Run

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\journey-certification\Invoke-PPIQ-JourneyCertification.ps1 `
  -ProjectRoot C:\Workspace\PlantProcess-IQ `
  -ConnectionString "Host=127.0.0.1;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only" `
  -InstallPlaywrightBrowser
```

Evidence is written under:

`Frontend\PlantProcess.Web\test-results\journey-certification`

The pack fails loudly when the current implementation has remaining defects. It does not convert skipped, mocked-only, static-only or visually incomplete behavior into false green evidence.
