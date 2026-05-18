# PlantProcess IQ Refactor Phase R2 Acceptance Checklist

## Required gates
- Backend build: dotnet build Backend\PlantProcessIQ.sln
- Backend tests: dotnet test Backend\PlantProcessIQ.sln
- Frontend build: npm run build
- Frontend lint: npm run lint
- Frontend unit/integration: npm run test
- Full-stack E2E: npm run e2e

## Backend architecture acceptance
- Analytics contains risk/correlation/feature engineering only.
- Dashboarding contains dashboard contracts, services, metadata, widgets and query validation.
- Integration contracts contain DTOs/commands/results only.
- Integration services contain service implementations only.
- No service implementation file exists under Integration\Contracts.

## Frontend architecture acceptance
- API has domain folders: admin, dashboarding, integration, analytics, http.
- plantProcessApi.ts remains a compatibility facade.
- Pages have route-level shell files.
- Large page content is isolated for safe component extraction.
- CSS entry imports split base/page/component/legacy styles.
- Legacy CSS remains only during migration.

## Test acceptance
- Backend API smoke tests cover health, swagger, admin overview, jobs, DB config summary, schema config summary.
- Frontend Vitest does not execute Playwright specs.
- Playwright covers root, dashboard, admin, materials, risk, data-quality, correlations and critical shell checks.

## Not allowed
- No endpoint URL changes during refactor.
- No removal of compatibility API facade.
- No deletion of legacy CSS until page/component CSS is complete.
- No 77M dataset generation before import/query performance tests are stable.
