# PlantProcess IQ - Server Deployment Runbook

This runbook standardizes server deployment without adding new product functionality.

## Standard server deployment principle

Server deployment must be profile-driven. Do not edit committed source code, appsettings, frontend source, or website source to switch between local, server, and customer deployment.

## Canonical server deployment flow

1. Pull the latest repository version.
2. Confirm Pack 2D env/profile standardization is green.
3. Confirm Pack 3A deployment baseline is green.
4. Create server runtime env file from deploy/server/.env.example.
5. Fill real secrets only in ignored runtime env files or server secret manager.
6. Start required Docker services according to the server profile.
7. Validate API health, frontend reachability, website reachability, and reverse proxy routing.
8. Run smoke checks using configured smoke credentials.

## Server env rules

- deploy/server/.env.example is allowed to be tracked.
- deploy/server/.env, deploy/server/.env.local, and deploy/server/.env.production must not be tracked.
- env/profiles/local.env must not be tracked.
- Frontend/PlantProcess.Web/.env.local must not be tracked.
- Website/.env.local must not be tracked.

## Deployment mode

Default standard server mode: server-docker.

## Demo-source rule

Flat-steel demo source systems are demo deployment assets only. They must remain optional for customer deployment and must not become product hardcoding.

## Handover rule

Every deployment must be reproducible from:

- deploy/
- env/profiles/*.example
- Documentation/deployment/
- scripts/deploy/

Runtime secrets are intentionally excluded.
