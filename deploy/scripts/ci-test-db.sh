#!/usr/bin/env bash
# H1: ephemeral Postgres test DB for stage-3 `dotnet test`. `up` prints the connection string
# on stdout (and nothing else); `down` removes the container. Migrations applied idempotently.
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
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    docker run -d --name "${CT}" \
      -e POSTGRES_USER="${DBUSER}" -e POSTGRES_PASSWORD="${DBPASS}" -e POSTGRES_DB="${DBNAME}" \
      -p "127.0.0.1:${PORT}:5432" postgres:16-alpine >/dev/null
    for i in $(seq 1 30); do
      if docker exec "${CT}" pg_isready -U "${DBUSER}" -d "${DBNAME}" >/dev/null 2>&1; then break; fi
      sleep 1
      [ "${i}" = "30" ] && { echo "ci-test-db: postgres never became ready" >&2; exit 1; }
    done
    shopt -s nullglob
    SQL_FILES=( "${SQL_DIR}"/*.sql )
    [ ${#SQL_FILES[@]} -gt 0 ] || { echo "ci-test-db: no .sql in ${SQL_DIR}" >&2; exit 1; }
    for f in "${SQL_FILES[@]}"; do
      docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}" < "${f}" >/dev/null
    done
    # PPIQ-T07: the same DB carries script 096 audit triggers; callers export
    #          PPIQ_AUDIT_TRIGGER_TEST_CONNECTION="${CONN}" to run the immutability suite.
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
