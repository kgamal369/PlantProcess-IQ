#!/usr/bin/env bash
# T02 stage 6: migrate app DB (idempotent SQL) + regenerate/seed the 8 demo sources.
# App-schema EF migrations also run at API startup (belt-and-braces) - this stage makes
# the DB correct BEFORE the new image starts.
set -euo pipefail
ENV_FILE="${ENV_FILE:-deploy/compose/.env}"
[ -f "${ENV_FILE}" ] && set -a && . "${ENV_FILE}" && set +a

DB_CONTAINER="${DB_CONTAINER:-ppiq-postgres}"
DB_USER="${POSTGRES_USER:?POSTGRES_USER missing from .env}"
DB_NAME="${POSTGRES_DB:?POSTGRES_DB missing from .env}"

echo "== applying numbered SQL scripts (idempotent) to ${DB_NAME} =="
shopt -s nullglob
SQL_DIR="Backend/database/scripts"
SQL_FILES=( "${SQL_DIR}"/*.sql )
[ ${#SQL_FILES[@]} -gt 0 ] || { echo "FATAL: no .sql files found in ${SQL_DIR} (wrong path/case?)"; exit 1; }
for f in "${SQL_FILES[@]}"; do
  echo "  -> ${f}"
  docker exec -i "${DB_CONTAINER}" psql -v ON_ERROR_STOP=1 -U "${DB_USER}" -d "${DB_NAME}" < "${f}"
done

echo "== regenerating deterministic demo dataset (T-039, fixed seed) =="
python3 Backend/tools/generate_demo_dataset.py --out Infrastructure/demo-sources

echo "== recreating demo source containers so init scripts re-seed =="
COMPOSE_FILE="${COMPOSE_FILE:-Infrastructure/deploy/docker-compose.demo.yml}"
# >>> CONFIG: list your demo-source service names exactly as in the compose file:
DEMO_SERVICES="meltshop-postgres caster-oracle hsm-oracle pkl-mssql parsytec-mysql downtime-mysql excel-qa excel-yard"
# <
DEMO_SOURCES_FILE="${DEMO_SOURCES_FILE:-deploy/compose/docker-compose.demo-sources.yml}"
DEMO_SOURCES_PORTS="${DEMO_SOURCES_PORTS:-deploy/compose/docker-compose.demo-sources.ports.yml}"
DEMO_SOURCES_PROJECT="${DEMO_SOURCES_PROJECT:-plantprocessiq-demo-sources}"
docker compose -p "${DEMO_SOURCES_PROJECT}" -f "${DEMO_SOURCES_FILE}" -f "${DEMO_SOURCES_PORTS}" up -d --force-recreate ${DEMO_SERVICES}
echo "== migrate + seed done =="
