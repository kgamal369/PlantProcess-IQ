# PlantProcess IQ - Deployment Dry-Run Report

Generated at: 2026-06-04 13:37:14

## Gate Summary

| Status | Count |
|---|---:|
| MISSING | 1 |
| OK | 19 |
| WARN | 2 |

## Asset Summary

| Kind | Count |
|---|---:|
| CADDY | 1 |
| CHECKLIST | 1 |
| COMPOSE | 1 |
| DRY_RUN_DOC | 1 |
| ENV_TEMPLATE | 1 |
| RUNBOOK | 1 |
| VALIDATOR | 2 |

## Gate Details

| Gate | Status | Evidence | Action |
|---|---|---|---|
| Server env example required keys | MISSING | 3 required key(s) missing. | Add missing keys to deploy/server/.env.example. |
| Caddyfile content check | OK | deploy/caddy/Caddyfile is not empty. | No action. |
| Pack 3A prerequisite | OK | Latest Pack3A gate is green. | No action. |
| Pack 3B prerequisite | OK | Latest Pack3B gate is green. | No action. |
| Required deployment dry-run asset exists | OK | deploy\caddy\Caddyfile | No action. |
| Required deployment dry-run asset exists | OK | deploy\demo-sources\docker-compose.demo-sources.yml | No action. |
| Required deployment dry-run asset exists | OK | deploy\server\.env.example | No action. |
| Required deployment dry-run asset exists | OK | deploy\server\SERVER_DEPLOYMENT_CHECKLIST.md | No action. |
| Required deployment dry-run asset exists | OK | deploy\server\SERVER_DRY_RUN.md | No action. |
| Required deployment dry-run asset exists | OK | deploy\server\SERVER_RUNBOOK.md | No action. |
| Required deployment dry-run asset exists | OK | scripts\deploy\validate-pack3a-deployment-baseline.ps1 | No action. |
| Required deployment dry-run asset exists | OK | scripts\deploy\validate-pack3b-server-deployment-manifest.ps1 | No action. |
| Runtime env files remain untracked | OK | No runtime env files are tracked. | No action. |
| Runtime env ignore rule exists | OK | deploy/server/.env | No action. |
| Runtime env ignore rule exists | OK | deploy/server/.env.local | No action. |
| Runtime env ignore rule exists | OK | deploy/server/.env.production | No action. |
| Runtime env ignore rule exists | OK | env/profiles/local.env | No action. |
| Runtime env ignore rule exists | OK | Frontend/PlantProcess.Web/.env.local | No action. |
| Runtime env ignore rule exists | OK | Website/.env.local | No action. |
| Server env example secret hygiene | OK | No unsafe secret-like values found in deploy/server/.env.example. | No action. |
| Demo source compose dry-run config | WARN | Docker not available; compose config not executed. | Run this check on a machine with Docker. |
| Docker command available | WARN | Docker command not found in PATH. | Install Docker or use scripts/docker/get-docker-command.ps1 on server. |
