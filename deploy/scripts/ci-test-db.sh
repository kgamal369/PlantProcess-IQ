#!/usr/bin/env bash
# ci-test-db.sh - ephemeral Postgres for stage-3 dotnet test, on a throwaway network so a
# sibling SDK container reaches it by NAME (the agent has no dotnet).
# Builds schema IDENTICALLY to the app DB: EF idempotent -> decoration -> seed.
# stdout prints ONLY the connection string; diagnostics go to stderr.
set -euo pipefail
ACTION="${1:-up}"
CT="${PPIQ_CI_TESTDB_CONTAINER:-ppiq-citestdb}"
NET="${PPIQ_CI_TESTDB_NETWORK:-ppiq-citestnet}"
DBUSER="${PPIQ_CI_TESTDB_USER:-ppiq_test}"
DBPASS="${PPIQ_CI_TESTDB_PASS:-ppiq_test_pw}"
DBNAME="${PPIQ_CI_TESTDB_NAME:-plantprocess_test_db}"
SQL_DIR="${PPIQ_CI_TESTDB_SQL_DIR:-Backend/database/scripts}"
SEED_DIR="${PPIQ_CI_TESTDB_SEED_DIR:-Backend/database/seed}"
INFRA_PROJ="${INFRA_PROJ:-Backend/PlantProcess.Infrastructure}"
API_PROJ="${API_PROJ:-Backend/PlantProcess.Api}"
SDK_IMAGE="${PPIQ_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:9.0}"
SELF="$(cat /etc/hostname)"
CONN="Host=${CT};Port=5432;Database=${DBNAME};Username=${DBUSER};Password=${DBPASS}"
psql_in() { docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}"; }
# robust readiness: require a real SELECT to succeed against the MAINTENANCE db, twice in a
# row, so the init-time throwaway server (which answers pg_isready early then restarts) cannot
# produce a false "ready". Returns only when the REAL server is genuinely up.
wait_ready() {
  local ok=0 streak=0 i
  for i in $(seq 1 60); do
    if docker exec "${CT}" psql -U "${DBUSER}" -d postgres -tAc "SELECT 1" >/dev/null 2>&1; then
      streak=$((streak+1))
      if [ "${streak}" -ge 2 ]; then ok=1; break; fi
    else
      streak=0
    fi
    sleep 1
  done
  [ "${ok}" = "1" ]
}
case "${ACTION}" in
  up)
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    docker network create "${NET}" >/dev/null 2>&1 || true
    docker run -d --name "${CT}" --network "${NET}" -e POSTGRES_USER="${DBUSER}" -e POSTGRES_PASSWORD="${DBPASS}" -e POSTGRES_DB="${DBNAME}" postgres:16-alpine >/dev/null
    if ! wait_ready; then echo "ci-test-db: postgres never became ready" >&2; docker logs --tail 50 "${CT}" >&2 || true; exit 1; fi
    if ! docker exec "${CT}" psql -U "${DBUSER}" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='${DBNAME}'" 2>/dev/null | grep -q 1; then
      echo "ci-test-db: ${DBNAME} missing -> creating it" >&2
      docker exec "${CT}" createdb -U "${DBUSER}" "${DBNAME}"
    fi
    echo "ci-test-db: [1/3] EF model -> idempotent schema (via SDK sibling)" >&2
    docker run --rm --volumes-from "${SELF}" -w "${PWD}" "${SDK_IMAGE}" bash -lc "set -e; export PATH=\"\$PATH:\$HOME/.dotnet/tools\"; dotnet tool restore >/dev/null 2>&1 || dotnet tool install --global dotnet-ef >/dev/null 2>&1 || true; dotnet ef migrations script --idempotent --project \"${INFRA_PROJ}\" --startup-project \"${API_PROJ}\" -o deploy/.ci-ef-idempotent.sql" 1>&2
    psql_in < deploy/.ci-ef-idempotent.sql >/dev/null
    rm -f deploy/.ci-ef-idempotent.sql
    echo "ci-test-db: [2/3] decoration scripts" >&2
    shopt -s nullglob
    SQL_FILES=( "${SQL_DIR}"/*.sql )
    [ ${#SQL_FILES[@]} -gt 0 ] || { echo "ci-test-db: no .sql in ${SQL_DIR}" >&2; exit 1; }
    for f in "${SQL_FILES[@]}"; do psql_in < "${f}" >/dev/null; done
    echo "ci-test-db: [3/3] seed scripts" >&2
    SEED_FILES=( "${SEED_DIR}"/*.sql )
    if [ ${#SEED_FILES[@]} -gt 0 ]; then
      for f in "${SEED_FILES[@]}"; do psql_in < "${f}" >/dev/null; done
    fi
    echo "AUDIT trigger DB ready" >&2
    echo "${CONN}"
    ;;
  down)
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    docker network rm "${NET}" >/dev/null 2>&1 || true
    ;;
  *)
    echo "usage: ci-test-db.sh [up|down]" >&2; exit 2
    ;;
esac
