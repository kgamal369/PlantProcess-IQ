# local-repair - RETIRED

These developer DB repair scripts were retired in Phase 2 Part A.

They existed only to fix a drifted *local* database (chiefly a refresh-token
foreign key that had been repointed away from `public.app_users`). They mixed
two concerns:

1. **Structural FK repair** - now handled deploy-safely and idempotently by the
   committed migration `Backend/database/scripts/302_p01_p02_authstore_lineage_lock.sql`,
   which self-heals the FK lineage on every apply and is a no-op when healthy.

2. **Deterministic test seed** (default tenant + admin) - now lives in the
   test-only fixture `Backend/tests/_fixtures/seed_test_auth.sql`, applied only by
   `tools/ci/Rebuild-LocalTestDb.ps1`. It is NEVER deployed (deploying a known
   admin credential would be a backdoor and would trip the no-demo-tenant gate).

To get a clean local test database with no repairs, run:

    powershell -ExecutionPolicy Bypass -File tools/ci/Rebuild-LocalTestDb.ps1