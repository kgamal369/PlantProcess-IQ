# Phase 1 - Deploy Chain Restoration: acceptance runbook

## What Phase 1 changed
- Single self-contained compose at `deploy/compose/docker-compose.demo.yml`
  (ppiq-postgres + api + workers + app-web + website), loopback-bound DB.
- Jenkinsfile DEPLOY_DIR -> `deploy/compose`; stage-1 now preserves only the
  server-only `deploy/compose/.env` and `deploy/caddy/Caddyfile`.
- Gate validators repointed to the new paths; `verify-server-exposure.sh`
  recreated under `deploy/server/`.
- The three `999_*_DO_NOT_DEPLOY.sql` scripts moved to
  `Backend/database/local-repair/` (out of the deploy SQL glob).
- New deploy smoke gate (stage 5b) + CRLF guard.

## Before you push (server-side, one time)
1. Create `deploy/compose/.env` on the server from `.env.example` and set
   `POSTGRES_PASSWORD` to match the already-running `ppiq-postgres`.
2. Confirm the API port: the image listens on :5063. If your Caddy proxies to
   plantprocess-api:8080, uncomment `ASPNETCORE_URLS: http://0.0.0.0:8080` in
   the api service (or change the Caddyfile upstream to :5063 and reload Caddy).

## Local preflight
    powershell -ExecutionPolicy Bypass -File tools/ci/Verify-Phase01-Local.ps1

## Acceptance (definition of done)
- Pipeline reaches stage 5b with no ENOENT / path crash.
- `/health` returns 200/401 and the three freshness probes return 200/401.
- A clean VM reaches the login page using only this runbook (no manual SQL).
- No `999_*_DO_NOT_DEPLOY.sql` is applied during deploy (grep the deploy log).
- `ss -ltnp` shows PostgreSQL bound to 127.0.0.1 only.