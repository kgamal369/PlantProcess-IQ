# PlantProcess IQ Deployment

This is the canonical deployment root for PlantProcess IQ.

## Structure

- deploy/server — server env examples and server helper scripts.
- deploy/caddy — canonical Caddy reverse-proxy config.
- deploy/demo-sources — demo source-system Docker compose.
- deploy/ci — CI/CD support files.
- deploy/airgap, deploy/dr, deploy/export, deploy/identity — specialized deployment/security assets.

## Rule

Do not create new root-level deployment folders.
Do not keep duplicate Caddyfiles.
Do not keep root-level demo compose files unless a CI tool explicitly requires them.

## Local development

Use:

`powershell
.\scripts\run\start-local.ps1 -Profile local -StartDb -StartDemoSources -FreePorts
`",
    ",
    

Use a server profile/env file and the canonical deploy folder only.

## Loopback-binding decision

The demo database (`ppiq-postgres`) is bound to the loopback interface only -
`127.0.0.1:${POSTGRES_PORT:-5432}:5432` in `deploy/compose/docker-compose.demo.yml`.
It is never published on the server's public interface. Application containers reach
PostgreSQL over the private `ppiq-network` by service name (`ppiq-postgres:5432`), and
external clients reach the API only through Caddy over TLS - never the database directly.

Exposure is proven after each deploy by `deploy/server/verify-server-exposure.sh`, which
checks that ports 5432 (PostgreSQL), 6379 (Redis) and 5063 (API internal) are not publicly
open on the server's public IP. Redis and the API listen on internal networks only.