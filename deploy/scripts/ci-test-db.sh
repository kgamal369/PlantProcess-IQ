#!/usr/bin/env bash
# ci-test-db.sh - ephemeral Postgres for stage-3 dotnet test.
# Builds the test schema IDENTICALLY to the real app DB (migrate-and-seed.sh run_app):
#   1) EF model -> idempotent SQL -> apply   (creates material_units, audit_log_entries, ...)
#   2) Backend/database/scripts/*.sql        (post-EF decoration: views/functions/matviews)
#   3) Backend/database/seed/*.sql           (tenant + canonical_material_units + genealogy spine)
# Only the connection string is printed on stdout; ALL diagnostics go to stderr.
set -euo pipefail
ACTION="${1:-up}"
CT="${PPIQ_CI_TESTDB_CONTAINER:-ppiq-citestdb}"
PORT="${PPIQ_CI_TESTDB_PORT:-55432}"
DBUSER="${PPIQ_CI_TESTDB_USER:-ppiq_test}"
DBPASS="${PPIQ_CI_TESTDB_PASS:-ppiq_test_pw}"
DBNAME="${PPIQ_CI_TESTDB_NAME:-plantprocess_test_db}"
SQL_DIR="${PPIQ_CI_TESTDB_SQL_DIR:-Backend/database/scripts}"
SEED_DIR="${PPIQ_CI_TESTDB_SEED_DIR:-Backend/database/seed}"
INFRA_PROJ="${INFRA_PROJ:-Backend/PlantProcess.Infrastructure}"
API_PROJ="${API_PROJ:-Backend/PlantProcess.Api}"
CONN="Host=127.0.0.1;Port=${PORT};Database=${DBNAME};Username=${DBUSER};Password=${DBPASS}"
psql_in() { docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}"; }
case "${ACTION}" in
  up)
    docker rm -f "${CT}" >/dev/null 2>&1 || true
    docker run -d --name "${CT}" -e POSTGRES_USER="${DBUSER}" -e POSTGRES_PASSWORD="${DBPASS}" -e POSTGRES_DB="${DBNAME}" -p "127.0.0.1:${PORT}:5432" postgres:16-alpine >/dev/null
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
    echo "ci-test-db: [1/3] EF model -> idempotent schema" >&2
    if ! dotnet ef --version >/dev/null 2>&1; then
      dotnet tool restore >/dev/null 2>&1 || dotnet tool install --global dotnet-ef >/dev/null 2>&1 || true
      export PATH="${PATH}:${HOME}/.dotnet/tools"
    fi
    TMP="$(mktemp -d)"
    dotnet ef migrations script --idempotent --no-build --project "${INFRA_PROJ}" --startup-project "${API_PROJ}" -o "${TMP}/ef-idempotent.sql" 1>&2 || dotnet ef migrations script --idempotent --project "${INFRA_PROJ}" --startup-project "${API_PROJ}" -o "${TMP}/ef-idempotent.sql" 1>&2
    psql_in < "${TMP}/ef-idempotent.sql" >/dev/null
    rm -rf "${TMP}"
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
    ;;
  *)
    echo "usage: ci-test-db.sh [up|down]" >&2; exit 2
    ;;
esac
