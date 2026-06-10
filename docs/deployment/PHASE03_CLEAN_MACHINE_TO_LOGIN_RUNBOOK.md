# Phase 03 — Clean-Machine-to-Login Runbook

Marker: `PPIQ_REALIZATION_T019_CLEAN_MACHINE_TO_LOGIN_V2`

## Goal

A reviewer can start from a clean machine and reach a working PlantProcess IQ login using only this runbook and runtime secrets supplied outside Git.

This runbook supports three deployment profiles:

| Profile | Main DB topology | Use case |
|---|---|---|
| `local-native-main-db` | PostgreSQL installed directly on the laptop/host | Local laptop proof |
| `server-docker-main-db` | Main PostgreSQL runs in Docker | Standard server deployment |
| `customer-template` | Customer/managed/external DB through connection string | Customer-specific topology |

## Non-negotiables

- Do not commit real runtime env files.
- Do not commit DB passwords, signing keys, smoke credentials, or generated local secrets.
- Caddy is the public ingress.
- PostgreSQL host ports must be loopback/private only.
- The app must reach login through configured health + frontend + auth routes.

## Clean machine prerequisites

Install:

1. Git
2. Docker + Docker Compose plugin
3. .NET SDK matching the repo target
4. Node.js + npm
5. PowerShell 5.1+ or PowerShell 7+

## Runtime env preparation

Create one runtime env file outside committed source or from a tracked example.

Server Docker DB profile:

    copy deploy\compose\env\.env.server-docker-main-db.example deploy\server\.env.production

Then edit `deploy\server\.env.production` on the target machine only.

Required values include:

- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_DB`
- `PPIQ_SIGNING_KEY`
- `SITE_HOST`
- `WEBSITE_HOST`
- `ACME_EMAIL`

Local native DB profile:

    copy deploy\compose\env\.env.local-native-main-db.example deploy\server\.env.production

Customer template profile:

    copy deploy\compose\env\.env.customer-template.example deploy\server\.env.production

## Dry run

Dry run validates assets and Compose syntax without starting production containers:

    powershell -ExecutionPolicy Bypass -File deploy\server\clean-machine-to-login.ps1 -Profile server-docker-main-db -DryRun

## Server deployment

    powershell -ExecutionPolicy Bypass -File deploy\server\clean-machine-to-login.ps1 -Profile server-docker-main-db -EnvFile deploy\server\.env.production

## Local laptop deployment profile

Use this only if app containers need to connect to native Windows PostgreSQL:

    powershell -ExecutionPolicy Bypass -File deploy\server\clean-machine-to-login.ps1 -Profile local-native-main-db -EnvFile deploy\server\.env.production

## Customer topology deployment profile

    powershell -ExecutionPolicy Bypass -File deploy\server\clean-machine-to-login.ps1 -Profile customer-template -EnvFile deploy\server\.env.production

## Login smoke

With smoke credentials supplied at runtime:

    powershell -ExecutionPolicy Bypass -File tools\phase3\smoke-p3-t19-clean-machine-login.ps1 -BaseUrl http://localhost:5173 -ApiBaseUrl http://localhost:5063 -SmokeUsername admin -SmokePassword "<runtime-only-password>"

## Expected success

The deploy script must end with:

    [GREEN] P3-T19 clean-machine-to-login deployment completed.

The smoke script must end with:

    [GREEN] P3-T19 clean-machine smoke passed.

## Failure modes

| Failure | Meaning | Fix |
|---|---|---|
| Missing runtime env file | `deploy/server/.env.production` or `-EnvFile` not found | Create it from the right `.example` file |
| Docker compose config fails | Compose/env mismatch | Fix env variables or selected profile |
| Backend build fails | .NET/build issue | Run `dotnet build Backend` directly |
| Frontend build fails | npm/build issue | Run `npm run build` inside `Frontend/PlantProcess.Web` |
| Health fails | API not up or reverse proxy wrong | Check API container logs and `PPIQ_API_UPSTREAM` |
| Login fails | smoke user missing or auth config wrong | Create/seed smoke user and confirm signing key |