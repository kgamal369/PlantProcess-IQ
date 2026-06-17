
# PlantProcess IQ — Canonical Compose Environment Profiles

Marker: PPIQ_REALIZATION_T016_CANONICAL_ENV_DEPLOY_V2

This folder is the canonical deployment root for Compose files.

## Supported main-database topologies

### 1. Local laptop development

Main PlantProcess IQ PostgreSQL is installed directly on the laptop/Windows host.

Use:

    docker-compose.yml
    docker-compose.local-native-main-db.yml

The local overlay points app containers to host.docker.internal and does not require the main DB container.

### 2. Server deployment

All databases, including the main PlantProcess IQ PostgreSQL DB, run as Docker containers.

Use:

    docker-compose.yml
    docker-compose.server-docker-main-db.yml

The server overlay keeps PostgreSQL loopback-bound and lets app containers reach it on the private Docker network.

### 3. Customer deployment

Customer topology can vary: native DB, managed DB, VM DB, Kubernetes service, or Docker DB.

Use:

    docker-compose.yml
    docker-compose.customer-template.yml

The customer overlay relies on PPIQ_MAIN_DB_CONNECTION_STRING and does not hardcode the DB topology.

## Non-negotiables

- Never commit real secrets.
- Never hardcode one DB topology into product scripts.
- Caddy is the only public ingress.
- DB host ports are loopback-only when exposed.
- Runtime environment files are server/customer/local private files, not tracked source.
