# PlantProcess IQ - Deployment Contract

Pack 3A defines the standard deployment ownership model. This document does not introduce new features.

## Standard deployment modes

| Mode | Purpose | Database | Demo Sources | API/Web/Website | Rule |
|---|---|---|---|---|---|
| local-hybrid | Developer machine | Native or existing PostgreSQL | Docker | Local processes | Used for Karim local development. |
| server-docker | Demo/server deployment | Docker PostgreSQL or managed equivalent | Docker | Docker/reverse proxy | Used for standard demo server. |
| customer-template | Customer installation | Customer-selected | Optional | Customer-selected | Values change by profile, not source code edits. |

## Canonical deployment folders

| Folder | Owner | Rule |
|---|---|---|
| deploy/local | Deployment | Local-only runtime helpers. |
| deploy/server | Deployment | Server deployment scripts and env instructions. |
| deploy/demo-sources | Deployment | Demo source-system compose files only. |
| deploy/caddy | Deployment | Reverse-proxy configuration. |
| deploy/ci | CI/CD | Deployment pipeline assets. |

## Non-negotiable rules

- No committed real secrets.
- No deployment duplication under deployment/ or Infrastructure/deploy/.
- No root-level duplicate demo-source compose file.
- No manual appsettings edits to switch local/server/customer.
- Flat-steel demo source systems remain demo deployment assets, not hardcoded product assumptions.
