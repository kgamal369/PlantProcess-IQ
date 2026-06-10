# P3-T16 Canonical Caddyfile + Compose Per Environment

Generated: 2026-06-10T13:12:32.0291404+03:00

Installed:

- deploy/caddy/Caddyfile
- deploy/caddy/README.md
- deploy/compose/README.md
- deploy/compose/docker-compose.local-native-main-db.yml
- deploy/compose/docker-compose.server-docker-main-db.yml
- deploy/compose/docker-compose.customer-template.yml
- deploy/compose/env/.env.local-native-main-db.example
- deploy/compose/env/.env.server-docker-main-db.example
- deploy/compose/env/.env.customer-template.example
- tools/phase3/validate-p3-t16-canonical-env-deploy.cjs
- docs/phase3/P3_T16_CANONICAL_ENV_DEPLOY.md

Environment contract:

- Local laptop: native Windows PostgreSQL main DB; demo source DBs remain Docker.
- Server: all DBs are Docker containers.
- Customer: topology is generic and connection-string driven.

Validation:

- Docker Compose config parsed for local-native-main-db profile.
- Docker Compose config parsed for server-docker-main-db profile.
- Docker Compose config parsed for customer-template profile.
- Caddy upstream drift to plantprocess-api:8080 blocked.
- Caddy defaults to plantprocess-api:5063.
- DB host binding remains loopback-only where host port exists.

Static validation:

PASSED