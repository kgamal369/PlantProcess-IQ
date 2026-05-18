# PlantProcess IQ Refactor Phase Acceptance Checklist

## Goal
Complete the frontend/backend restructuring without losing existing functionality.

## Required Green Gates
- Backend: dotnet build PlantProcessIQ.sln
- Backend: dotnet test PlantProcessIQ.sln
- Frontend: npm run build
- Frontend: npm run lint
- Frontend: npm run test
- Frontend + Backend: npm run e2e

## Refactor Requirements
- Analytics contains risk, correlation, feature engineering only.
- Dashboarding contains dashboard definitions, metadata, widgets, filters, layouts.
- Integration contracts contain DTOs/commands/results only.
- Integration services contain service implementations only.
- Frontend API is split by feature/domain.
- Frontend pages are split into page folders with subcomponents and hooks.
- CSS is split by theme, layout, page, and component.
- Legacy compatibility facades remain until all imports are migrated.

## Do Not Break
- Existing routes.
- Existing endpoint URLs.
- Existing dashboard pages.
- Existing Admin page.
- Existing E2E route smoke tests.
- Existing API smoke tests.
