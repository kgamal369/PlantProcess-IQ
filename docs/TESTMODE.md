# PPIQ Test-Mode Run Profiles (PPIQ-T022)

One switch surface to test every auth/license combination - locally and on the server -
without code edits or DB surgery. **All switches are refused in Production** unless
`PPIQ_TESTMODE__IExplicitlyAcceptRisk=true`. Active test mode is logged loudly at startup
and echoed by `GET /admin/testmode-status` (authenticated).

## Switch table (guard test keeps this in sync with code)

| Env variable | Values | Effect |
|---|---|---|
| `PPIQ_TESTMODE__SeedUsers` | true/false | Seeds tm-admin / tm-ceo / tm-engineer / tm-operator (passwords below) via the existing PlantProcess:Auth:Users mechanism |
| `PPIQ_TESTMODE__ForceTier` | Lite \| Pro \| ProPlus \| Enterprise | Upserts `demo_runtime_settings['license.defaultTier']` at startup - the same setting the demo seed writes, so the normal tier resolution applies it |
| `PPIQ_TESTMODE__StatusEndpoint` | true/false (default true) | `GET /admin/testmode-status` echo |
| `PPIQ_TESTMODE__IExplicitlyAcceptRisk` | true/false | REQUIRED for any switch in Production |
| `PlantProcess__Auth__RequireAdminMfa` | true/false (default false) | T021 admin-MFA enforcement master switch |

## Seeded users (SeedUsers=true)

| User | Password | Role |
|---|---|---|
| tm-admin | TestMode-Admin-123! | Admin |
| tm-ceo | TestMode-Ceo-123! | Executive |
| tm-engineer | TestMode-Engineer-123! | ProcessEngineer |
| tm-operator | TestMode-Operator-123! | Operator |

## Local (laptop: native Postgres + Docker demo sources)

```
set ASPNETCORE_ENVIRONMENT=Development
set PPIQ_TESTMODE__SeedUsers=true
set PPIQ_TESTMODE__ForceTier=Pro
dotnet run --project Backend/PlantProcess.Api
```

## Server (staging session)

```
docker compose -p plantprocessiq -f Infrastructure/deploy/docker-compose.demo.yml \
  -f deploy/compose/docker-compose.testmode.yml up -d app-api
# verify: GET /admin/testmode-status ; teardown: redeploy WITHOUT the overlay
```

## Stage license toggle (T14)

Set `PPIQ_TESTMODE_FORCETIER=Pro` (or call the admin tier control) and restart `app-api` -
entitlements change with NO reseed; switch back to `Enterprise` to restore.