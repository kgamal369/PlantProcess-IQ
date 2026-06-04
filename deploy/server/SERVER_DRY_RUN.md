# PlantProcess IQ - Server Deployment Dry-Run

This document defines the non-destructive dry-run proof for server deployment.

## Dry-run rules

- Do not start production containers during this proof.
- Do not create real server secrets in tracked files.
- Validate deployment assets before runtime execution.
- Validate Docker compose syntax before server start.
- Validate Caddy ownership and non-empty reverse-proxy config.
- Validate env templates are placeholders only.

## Standard dry-run sequence

1. Confirm Pack 3A deployment baseline is green.
2. Confirm Pack 3B server deployment manifest is green.
3. Confirm deploy/server/.env.example exists and contains only placeholders.
4. Confirm runtime env files are not tracked.
5. Confirm deploy/demo-sources/docker-compose.demo-sources.yml parses using docker compose config.
6. Confirm deploy/caddy/Caddyfile exists and is not empty.
7. Confirm server runbook and checklist exist.

## Runtime execution comes later

Pack 3C is not a production startup pack. Startup automation belongs to the next deployment pack.
