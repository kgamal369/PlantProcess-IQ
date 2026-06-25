#!/usr/bin/env bash
# ci-test-db.sh - ephemeral Postgres for stage-3 dotnet test, on a throwaway network so a
# sibling SDK container can reach it by NAME (the agent has no dotnet; tests run in a sibling).
# Builds the test schema IDENTICALLY to the app DB (migrate-and-seed run_app):
#   1) EF model -> idempotent SQL (generated inside the SDK image) -> apply
#   2) Backend/database/scripts/*.sql   3) Backend/database/seed/*.sql
# stdout prints ONLY the connection string (container-name form); diagnostics go to stderr.
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
# connection string the SDK sibling uses (same network -> reach DB by container name)
CONN="Host=${CT};Port=5432;Database=${DBNAME};Username=${DBUSER};Password=${DBPASS}"
psql_in() { docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}"; }
case "${ACTION}" in
  up)
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    docker network create "${NET}" >/dev/null 2>&1 || true
    docker run -d --name "${CT}" --network "${NET}" -e POSTGRES_USER="${DBUSER}" -e POSTGRES_PASSWORD="${DBPASS}" -e POSTGRES_DB="${DBNAME}" postgres:16-alpine >/dev/null
    ready=0
    for i in $(seq 1 30); do
      if docker exec "${CT}" pg_isready -U "${DBUSER}" -d postgres >/dev/null 2>&1; then ready=1; break; fi
      sleep 1
    done
    [ "${ready}" = "1" ] || { echo "ci-test-db: postgres server never became ready" >&2; docker logs --tail 40 "${CT}" >&2 || true; exit 1; }
    if ! docker exec "${CT}" psql -U "${DBUSER}" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='${DBNAME}'" 2>/dev/null | grep -q 1; then
      echo "ci-test-db: ${DBNAME} missing -> creating it" >&2
      docker exec "${CT}" createdb -U "${DBUSER}" "${DBNAME}"
    fi
    echo "ci-test-db: [1/3] EF model -> idempotent schema (via SDK sibling)" >&2
    WS="$(docker exec "${SELF}" sh -lc 'pwd' 2>/dev/null || echo /var/jenkins_home/workspace/plantprocessiq-deploy)"
    # generate the idempotent EF SQL inside the SDK image (agent has no dotnet), into the shared workspace
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
