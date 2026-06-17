#!/usr/bin/env bash
# =====================================================================================
# deploy/scripts/migrate-and-seed.sh — model-first DB migrate + seed (app + demo).
#
#   migrate-and-seed.sh [--app-only | --demo-only]      (default: both)
#
# APP phase (model-first — fixes the EF-then-SQL ordering / "relation ... does not exist"):
#   1. Generate an IDEMPOTENT SQL script FROM the EF model (`dotnet ef migrations script
#      --idempotent`) and apply it via the DB container, so the model-derived schema exists
#      BEFORE the numbered decoration scripts run.
#   2. Apply Backend/database/scripts/*.sql (post-EF decoration), idempotent.
#   3. Apply Backend/database/seed/*.sql if present.
#
# DEMO phase (only when PPIQ_DEMO_SOURCES_MODE != disabled):
#   4. Regenerate the deterministic dataset.
#   5. Recreate the CANONICAL sources stack so each engine's init scripts re-seed.
#
# Everything is env-driven from the git-ignored .env — no hardcoded DB/container target.
#
# NOTE: this fixes the *ordering*. The audit_log_entries chain DRIFT is fixed separately by
# re-baselining the EF migrations from the C# model; once that is done, the idempotent script
# generated in step 1 contains the CreateTable and the whole sequence is green on a fresh DB.
# =====================================================================================
set -euo pipefail

MODE="both"
case "${1:-}" in
  --app-only)  MODE="app"  ;;
  --demo-only) MODE="demo" ;;
  "")          MODE="both" ;;
  *) echo "usage: $0 [--app-only|--demo-only]"; exit 2 ;;
esac

ENV_FILE="${ENV_FILE:-deploy/compose/.env}"
[ -f "${ENV_FILE}" ] && set -a && . "${ENV_FILE}" && set +a

DB_CONTAINER="${DB_CONTAINER:-plantprocess-postgres}"
DB_USER="${POSTGRES_USER:?POSTGRES_USER missing from .env}"
DB_NAME="${POSTGRES_DB:?POSTGRES_DB missing from .env}"

INFRA_PROJ="${INFRA_PROJ:-Backend/PlantProcess.Infrastructure}"
API_PROJ="${API_PROJ:-Backend/PlantProcess.Api}"

psql_in() { docker exec -i "${DB_CONTAINER}" psql -v ON_ERROR_STOP=1 -U "${DB_USER}" -d "${DB_NAME}"; }

run_app() {
  echo "== [app] EF model -> idempotent SQL -> apply (model-first) =="
  if ! dotnet ef --version >/dev/null 2>&1; then
    dotnet tool restore >/dev/null 2>&1 || dotnet tool install --global dotnet-ef >/dev/null 2>&1 || true
    export PATH="${PATH}:${HOME}/.dotnet/tools"
  fi
  TMP="$(mktemp -d)"
  # generate from the EF model; --no-build first (fast if already built), else full build
  dotnet ef migrations script --idempotent --no-build \
      --project "${INFRA_PROJ}" --startup-project "${API_PROJ}" -o "${TMP}/ef-idempotent.sql" \
    || dotnet ef migrations script --idempotent \
      --project "${INFRA_PROJ}" --startup-project "${API_PROJ}" -o "${TMP}/ef-idempotent.sql"
  echo "  -> applying EF-derived schema to ${DB_NAME} via ${DB_CONTAINER}"
  psql_in < "${TMP}/ef-idempotent.sql"
  rm -rf "${TMP}"

  echo "== [app] numbered SQL decoration (idempotent) =="
  shopt -s nullglob
  local files=( Backend/database/scripts/*.sql )
  [ ${#files[@]} -gt 0 ] || { echo "FATAL: no .sql in Backend/database/scripts (wrong path/case?)"; exit 1; }
  for f in "${files[@]}"; do echo "  -> ${f}"; psql_in < "${f}"; done

  local seeds=( Backend/database/seed/*.sql )
  if [ ${#seeds[@]} -gt 0 ]; then
    echo "== [app] seed =="
    for f in "${seeds[@]}"; do echo "  -> ${f}"; psql_in < "${f}"; done
  fi
}

run_demo() {
  if [ "${PPIQ_DEMO_SOURCES_MODE:-docker}" = "disabled" ]; then
    echo "== [demo] PPIQ_DEMO_SOURCES_MODE=disabled — skipping =="
    return 0
  fi
  echo "== [demo] regenerate deterministic dataset =="
  python3 Backend/tools/generate_demo_dataset.py --out deploy/fixtures/demo

  echo "== [demo] recreate canonical sources stack (re-seed) =="
  local SOURCES_FILE="${DEMO_SOURCES_FILE:-deploy/compose/docker-compose.sources.yml}"
  local SOURCES_PROJECT="${DEMO_SOURCES_PROJECT:-ppiq-sources}"
  # canonical service names exactly as in docker-compose.sources.yml
  # (Excel yard/QA are CSV file mounts, NOT services — do not list them here)
  local DEMO_SERVICES="${DEMO_SERVICES:-meltshop-postgres caster-oracle hsm-oracle pkl-mssql downtime-mysql parsytec-mysql}"
  docker compose -p "${SOURCES_PROJECT}" --env-file "${ENV_FILE}" -f "${SOURCES_FILE}" \
    up -d --force-recreate ${DEMO_SERVICES}
}

case "${MODE}" in
  app)  run_app ;;
  demo) run_demo ;;
  both) run_app; run_demo ;;
esac
echo "== migrate-and-seed (${MODE}) done =="