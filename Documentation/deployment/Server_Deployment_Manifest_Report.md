# PlantProcess IQ - Server Deployment Manifest Report

Generated at: 2026-06-04 13:31:21

## Gate Summary

| Status | Count |
|---|---:|
| BLOCKER | 1 |
| OK | 18 |

## Manifest Summary

| Kind | Count |
|---|---:|
| CADDY | 1 |
| COMPOSE | 1 |
| DOC | 6 |
| ENV_TEMPLATE | 1 |
| RUNTIME_ENV | 3 |
| SCRIPT | 1 |

## Deployment Asset Manifest

| Path | Exists | Kind | Required | Tracked Rule | Purpose |
|---|---|---|---|---|---|
| deploy\caddy\Caddyfile | True | CADDY | YES | TRACKED | Canonical reverse proxy configuration. |
| deploy\caddy\README.md | True | DOC | YES | TRACKED | Caddy deployment README. |
| deploy\demo-sources\docker-compose.demo-sources.yml | True | COMPOSE | YES | TRACKED | Demo source-system compose file. |
| deploy\demo-sources\README.md | True | DOC | YES | TRACKED | Demo source-system deployment README. |
| deploy\README.md | True | DOC | YES | TRACKED | Deployment root index. |
| deploy\server\.env | False | RUNTIME_ENV | NO | IGNORED_UNTRACKED | Server runtime env file. |
| deploy\server\.env.example | True | ENV_TEMPLATE | YES | TRACKED_TEMPLATE_ONLY | Safe server env template. |
| deploy\server\.env.local | False | RUNTIME_ENV | NO | IGNORED_UNTRACKED | Server local runtime env file. |
| deploy\server\.env.production | True | RUNTIME_ENV | NO | IGNORED_UNTRACKED | Server production runtime env file. |
| deploy\server\README.md | True | DOC | YES | TRACKED | Server deployment folder index. |
| deploy\server\SERVER_DEPLOYMENT_CHECKLIST.md | True | DOC | YES | TRACKED | Server deployment checklist. |
| deploy\server\SERVER_RUNBOOK.md | True | DOC | YES | TRACKED | Server deployment runbook. |
| scripts\deploy\validate-pack3a-deployment-baseline.ps1 | True | SCRIPT | YES | TRACKED | Pack 3A deployment validator. |
