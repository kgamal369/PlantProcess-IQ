#!/usr/bin/env bash
# ci-test-db.sh - ephemeral Postgres for stage-3 dotnet test, on a throwaway network so a
# sibling SDK container reaches it by NAME (the agent has no dotnet).
# Builds schema IDENTICALLY to the app DB: EF idempotent -> decoration -> seed -> dev license key.
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
DEV_TENANT="${PPIQ_DEV_TENANT:-00000000-0000-0000-0000-000000000001}"
DEV_KID="${PPIQ_DEV_KID:-ppiq-dev-ed25519}"
DEV_PUB_FILE="${PPIQ_DEV_PUB_FILE:-deploy/fixtures/license/dev_public.b64}"
SELF="$(cat /etc/hostname)"
CONN="Host=${CT};Port=5432;Database=${DBNAME};Username=${DBUSER};Password=${DBPASS}"
psql_in() { docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}"; }
wait_ready() {
  local ok=0 streak=0 i
  for i in $(seq 1 60); do
    if docker exec "${CT}" psql -U "${DBUSER}" -d postgres -tAc "SELECT 1" >/dev/null 2>&1; then
      streak=$((streak+1)); [ "${streak}" -ge 2 ] && { ok=1; break; }
    else streak=0; fi
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
    echo "ci-test-db: [1/4] EF model -> idempotent schema (via SDK sibling)" >&2
    docker run --rm --volumes-from "${SELF}" -w "${PWD}" -e ConnectionStrings__PlantProcessDb="${CONN}" "${SDK_IMAGE}" bash -lc "set -e; mkdir -p /tmp/efbin; dotnet tool install dotnet-ef --version 9.* --tool-path /tmp/efbin >/dev/null; /tmp/efbin/dotnet-ef migrations script --idempotent --project \"${INFRA_PROJ}\" --startup-project \"${API_PROJ}\" -o deploy/.ci-ef-idempotent.sql" 1>&2
    psql_in < deploy/.ci-ef-idempotent.sql >/dev/null
    rm -f deploy/.ci-ef-idempotent.sql
    echo "ci-test-db: [2/4] decoration scripts" >&2
    shopt -s nullglob
    SQL_FILES=( "${SQL_DIR}"/*.sql )
    [ ${#SQL_FILES[@]} -gt 0 ] || { echo "ci-test-db: no .sql in ${SQL_DIR}" >&2; exit 1; }
    for f in "${SQL_FILES[@]}"; do psql_in < "${f}" >/dev/null; done
    echo "ci-test-db: [3/4] seed scripts (excluding -v-only dev key)" >&2
    SEED_FILES=( "${SEED_DIR}"/*.sql )
    for f in "${SEED_FILES[@]}"; do
      case "$(basename "${f}")" in
        dev_ed25519_public_key.sql) echo "  (skip plain-pipe: ${f} is psql -v driven; registered in [4/4])" >&2 ;;
        *) psql_in < "${f}" >/dev/null ;;
      esac
    done
    echo "ci-test-db: [4/4] register dev Ed25519 license key (-v from fixture)" >&2
    KEYSQL="${SEED_DIR}/dev_ed25519_public_key.sql"
    if [ -f "${KEYSQL}" ] && [ -f "${DEV_PUB_FILE}" ]; then
      PUB="$(tr -d '\r\n' < "${DEV_PUB_FILE}")"
      docker exec -i "${CT}" psql -v ON_ERROR_STOP=1 -U "${DBUSER}" -d "${DBNAME}" -v "tenant_id=${DEV_TENANT}" -v "key_id=${DEV_KID}" -v "public_key_b64=${PUB}" < "${KEYSQL}" >/dev/null
    else
      echo "  (dev key fixture or seed missing -> skipping dev key registration)" >&2
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
