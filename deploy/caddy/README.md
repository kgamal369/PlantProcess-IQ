
# PlantProcess IQ — Canonical Caddy Deployment

Marker: PPIQ_REALIZATION_T016_CANONICAL_ENV_DEPLOY_V2

This folder owns the single canonical reverse-proxy configuration.

## Rule

Do not create environment-specific Caddyfiles.

Use one Caddyfile and switch behavior through environment variables:

- SITE_HOST
- WEBSITE_HOST
- ACME_EMAIL
- CADDY_AUTO_HTTPS
- PPIQ_API_UPSTREAM
- PPIQ_APP_UPSTREAM
- PPIQ_WEBSITE_UPSTREAM

## Default upstream contract

- API: plantprocess-api:5063
- App frontend: plantprocess-app-web:80
- Website: plantprocess-website:80

## Why

This prevents drift between local, server, and customer deployments. The same reverse-proxy file is used everywhere; only the environment profile changes.
