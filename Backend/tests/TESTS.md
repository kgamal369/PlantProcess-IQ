# PPIQ Backend Test Suite - Skip Register (PPIQ-T05 / T03 residual)

Policy: `dotnet test Backend` must exit 0 with the FULL suite executed - no --filter
narrowing in CI. Every skip is registered here with its reason; an unregistered skip
is a review finding.

Status 12 Jun 2026: 540 total / 540 green / 2 skipped (both registered below).

| Test class | File | Skip condition | Why it is legitimate |
|---|---|---|---|
| AdvancedResultPersistenceTests | PlantProcess.Infrastructure.IntegrationTests/Analytics/AdvancedResultPersistenceTests.cs | `SkippableFact` - skips when the integration Postgres (demo schema) is not reachable | Persistence round-trip needs a real DB; on DB-less agents the skip is correct. CI agents HAVE the DB, so in CI these execute - the skip only protects ad-hoc dev machines. |
| ReadOnlyEnforcementTests | PlantProcess.Infrastructure.IntegrationTests/Connectors/ReadOnlyEnforcementTests.cs | `SkippableFact` - skips when the demo source containers are not running | Read-only enforcement is proven against live demo sources; without them there is nothing to enforce against. CI brings the sources up, so CI executes them. |

Rules:
1. New `SkippableFact`/`Skip=` usages MUST add a row here in the same PR.
2. A skip that fires in CI (where DB + sources exist) is a failure, not a skip - investigate.
3. Never use a skip to hide a regression; that was the T03 anti-pattern this register closes out.