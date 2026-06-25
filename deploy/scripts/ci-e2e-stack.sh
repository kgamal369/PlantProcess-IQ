#!/usr/bin/env bash
# T02 stage 5: bring up an EPHEMERAL ppiq-ci stack, run Playwright, tear down. BLOCKING.
# >>> CONFIG - set once to your compose service names (docker compose config --services):
API_SERVICE="plantprocess-api"
WEB_SERVICE="plantprocess-web"
DB_SERVICE="plantprocess-postgres"
# <
set -euo pipefail
COMPOSE_FILE="${COMPOSE_FILE:-deploy/compose/docker-compose.yml}"
FRONTEND_DIR="${FRONTEND_DIR:-Frontend/PlantProcess.Web}"
API_PORT_CI="${API_PORT_CI:-18080}"
BASE_URL="http://127.0.0.1:${API_PORT_CI}"

cleanup(){ docker compose -p ppiq-ci -f "${COMPOSE_FILE}" down -v --remove-orphans || true; }
trap cleanup EXIT

# Deterministic T22 test profile for CI (Development env: guard allows it)
export ASPNETCORE_ENVIRONMENT=Development
export PPIQ_TESTMODE__SeedUsers=true
export PPIQ_TESTMODE__ForceTier=Enterprise
export PlantProcess__Auth__RequireAdminMfa=false
export PPIQ_SIGNING_KEY="CI-ONLY-SIGNING-KEY-AT-LEAST-64-CHARACTERS-LONG-0123456789ABCDEF"
export PPIQ_ALLOWED_ORIGINS="http://localhost:5173,${BASE_URL}"

docker compose -p ppiq-ci -f "${COMPOSE_FILE}" up -d --build "${DB_SERVICE}" "${API_SERVICE}" "${WEB_SERVICE}"

echo "-- waiting for CI API health on ${BASE_URL}/health"
for i in $(seq 1 60); do
  code=$(curl -ks -o /dev/null -w '%{http_code}' "${BASE_URL}/health" || true)
  [ "$code" = "200" ] && break
  sleep 2
  [ "$i" = "60" ] && { echo "CI stack never became healthy"; docker compose -p ppiq-ci -f "${COMPOSE_FILE}" logs --tail 80; exit 1; }
done

cd "${FRONTEND_DIR}"
export PPIQ_E2E_BASE_URL="${BASE_URL}"
export PPIQ_E2E_USER="admin"
export PPIQ_E2E_PASS="ChangeMe123!"
npx playwright install --with-deps chromium
npm run e2e
