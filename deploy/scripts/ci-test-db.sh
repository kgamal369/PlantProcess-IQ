#!/usr/bin/env bash
# H1: ephemeral Postgres test DB for stage-3 `dotnet test`. `up` prints the connection string
# on stdout (and nothing else); `down` removes the container. Migrations applied idempotently.
# Hardened: waits on the postgres maintenance DB (pg_isready -d <name> lies if the DB is absent),
# then GUARANTEES the target DB exists before applying SQL, so a reused container/volume that
# skipped POSTGRES_DB init can never produce "database does not exist".
set -euo pipefail
ACTION="${1:-up}"
CT="${PPIQ_CI_TESTDB_CONTAINER:-ppiq-citestdb}"
PORT="${PPIQ_CI_TESTDB_PORT:-55432}"
DBUSER="${PPIQ_CI_TESTDB_USER:-ppiq_test}"
DBPASS="${PPIQ_CI_TESTDB_PASS:-ppiq_test_pw}"
DBNAME="${PPIQ_CI_TESTDB_NAME:-plantprocess_test_db}"
SQL_DIR="${PPIQ_CI_TESTDB_SQL_DIR:-Backend/database/scripts}"
CONN="Host=127.0.0.1;Port=${PORT};Database=${DBNAME};Username=${DBUSER};Password=${DBPASS}"

case "${ACTION}" in
  up)
    # start clean: remove any leftover container so POSTGRES_DB init actually runs
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    docker run -d --name "${CT}" \
      -e POSTGRES_USER="${DBUSER}" -e POSTGRES_PASSWORD="${DBPASS}" -e POSTGRES_DB="${DBNAME}" \
      -p "127.0.0.1:${PORT}:5432" postgres:16-alpine >/dev/null

    # wait for the SERVER (maintenance db 'postgres' always exists) - not the target db
    ready=0
    for i in $(seq 1 30); do
      if docker exec "${CT}" pg_isready -U "${DBUSER}" -d postgres >/dev/null 2>&1; then ready=1; break; fi
      sleep 1
    done
    [ "${ready}" = "1" ] || { echo "ci-test-db: postgres server never became ready" >&2; docker logs --tail 40 "${CT}" >&2 || true; exit 1; }

    # GUARANTEE the target db exists (handles a reused volume where POSTGRES_DB init was skipped)
    if ! docker exec "${CT}" psql -U "${DBUSER}" -d postgres -tAc \
          "SELECT 1 FROM pg_database WHERE datname='${DBNAME}'" 2>/dev/null | grep -q 1; then
      echo "ci-test-db: ${DBNAME} missing -> creating it" >&2
      docker exec "${CT}" createdb -U "${DBUSER}" "${DBNAME}"
    fi

    shopt -s nullglob
    SQL_FILES=( "${SQL_DIR}"/*.sql )
    [ ${#SQL_FILES[@]} -gt 0 ] || { echo "ci-test-db: no .sql in ${SQL_DIR}" >&2; exit 1; }
    for f in "${SQL_FILES[@]}"; do
      docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}" < "${f}" >/dev/null
    done
    echo "AUDIT trigger DB ready" >&2
    echo "${CONN}"
    ;;
  down)
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    ;;
  *)
    echo "usage: ci-test-db.sh [up|down]" >&2; exit 2
    ;;
esac