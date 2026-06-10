
# P3-T16 — Canonical Caddyfile and Compose Per Environment

Marker: PPIQ_REALIZATION_T016_CANONICAL_ENV_DEPLOY_V2

## Result

P3-T16 establishes a generic deployment contract:

1. One canonical Caddyfile:
   - deploy/caddy/Caddyfile

2. One canonical compose base:
   - deploy/compose/docker-compose.demo.yml

3. Environment-specific overlays:
   - deploy/compose/docker-compose.local-native-main-db.yml
   - deploy/compose/docker-compose.server-docker-main-db.yml
   - deploy/compose/docker-compose.customer-template.yml

4. Safe example env templates:
   - deploy/compose/env/.env.local-native-main-db.example
   - deploy/compose/env/.env.server-docker-main-db.example
   - deploy/compose/env/.env.customer-template.example

## Environment policy

### Local laptop

The main PlantProcess IQ PostgreSQL DB is native Windows PostgreSQL. App containers connect to it through host.docker.internal.

Demo/customer-source DBs remain Docker containers.

### Server

All DBs are Docker containers.

### Customer

Topology is not assumed. Customer deployment uses PPIQ_MAIN_DB_CONNECTION_STRING and PPIQ_DB_TOPOLOGY.

## Validation

Run:

    node tools/phase3/validate-p3-t16-canonical-env-deploy.cjs

The validator checks:

- Caddyfile marker and security headers.
- Caddy upstream uses plantprocess-api:5063, not stale 8080 drift.
- Local, server, and customer compose overlays exist.
- Local profile does not require a Docker main DB.
- Server profile keeps Postgres loopback-bound.
- Customer profile is connection-string driven.
- Docker Compose config parses for all three profiles using safe dummy env values.
