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
  echo "== [app] EF model -> idempotent SQL -> apply (model-first, via SDK sibling) =="
  # The Jenkins agent has no dotnet; generate the idempotent EF SQL inside the SDK image
  # (workspace inherited via --volumes-from; the design-time factory needs a connection
  # string env to instantiate even though script generation is offline).
  SELF="$(cat /etc/hostname)"
  SDK_IMAGE="${PPIQ_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:9.0}"
  EF_OUT="deploy/.migrate-ef-idempotent.sql"
  docker run --rm --volumes-from "${SELF}" -w "${PWD}" \
    -e ConnectionStrings__PlantProcessDb="${ConnectionStrings__PlantProcessDb:-Host=${DB_CONTAINER};Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${POSTGRES_PASSWORD:-}}" \
    "${SDK_IMAGE}" bash -lc "set -e; mkdir -p /tmp/efbin; dotnet tool install dotnet-ef --version 9.* --tool-path /tmp/efbin >/dev/null; /tmp/efbin/dotnet-ef migrations script --idempotent --project \"${INFRA_PROJ}\" --startup-project \"${API_PROJ}\" -o \"${EF_OUT}\""
  echo "  -> applying EF-derived schema to ${DB_NAME} via ${DB_CONTAINER}"
  psql_in < "${EF_OUT}"
  rm -f "${EF_OUT}"

  echo "== [app] numbered SQL decoration (idempotent) =="
  shopt -s nullglob
  local files=( Backend/database/scripts/*.sql )
  [ ${#files[@]} -gt 0 ] || { echo "FATAL: no .sql in Backend/database/scripts (wrong path/case?)"; exit 1; }

  # M1-07. HONOUR THE MANIFEST.
  #
  # This loop used to be a bare glob, so every classification in
  # database.apply-order.manifest.csv was documentation that nothing enforced.
  # A script marked DO_NOT_AUTO_APPLY ran on every deploy anyway. That is how
  # 070 kept reinstalling the broken widget codes, and it applies equally to
  # VALIDATION_ONLY and OPTIONAL_DEMO_ONLY.
  local manifest="Backend/database/database.apply-order.manifest.csv"
  local matched=0
  local skipped=0

  for f in "${files[@]}"; do
    local base; base="$(basename "${f}")"
    local decision=""
    if [ -f "${manifest}" ]; then
      local line; line="$(grep -F "${base}" "${manifest}" || true)"
      if [ -n "${line}" ]; then
        matched=$((matched + 1))
        case "${line}" in
          *DO_NOT_AUTO_APPLY*)   decision="DO_NOT_AUTO_APPLY" ;;
          *VALIDATION_ONLY*)     decision="VALIDATION_ONLY" ;;
          *OPTIONAL_DEMO_ONLY*)  decision="OPTIONAL_DEMO_ONLY" ;;
        esac
      fi
    fi
    if [ -n "${decision}" ]; then
      echo "  -- SKIP ${f}  (${decision} per the manifest)"
      skipped=$((skipped + 1))
      continue
    fi
    echo "  -> ${f}"
    psql_in < "${f}"
  done

  # A gate that never fires is not a gate. If the manifest is present but
  # matched nothing, the guard is inert and the operator is told so rather than
  # being left with a false sense that classifications are being enforced.
  if [ -f "${manifest}" ] && [ "${matched}" -eq 0 ]; then
    echo "  !! WARNING: the manifest was read but matched no script filename."
    echo "  !! Classifications are NOT being enforced. Check the path column format."
  else
    echo "  == manifest: ${matched} script(s) classified, ${skipped} skipped =="
  fi

  local seeds=( Backend/database/seed/*.sql )
  if [ ${#seeds[@]} -gt 0 ]; then
    echo "== [app] seed (excluding -v-only dev key) =="
    for f in "${seeds[@]}"; do
      case "$(basename "${f}")" in
        dev_ed25519_public_key.sql) echo "  (skip plain-pipe: ${f} is psql -v driven; registered below)" ;;
        *) echo "  -> ${f}"; psql_in < "${f}" ;;
      esac
    done
  fi

  # register the DEV Ed25519 license public key the proper way (psql -v from the committed fixture).
  local DEV_TENANT="${PPIQ_DEV_TENANT:-00000000-0000-0000-0000-000000000001}"
  local DEV_KID="${PPIQ_DEV_KID:-ppiq-dev-ed25519}"
  local DEV_PUB_FILE="${PPIQ_DEV_PUB_FILE:-deploy/fixtures/license/dev_public.b64}"
  local KEYSQL="Backend/database/seed/dev_ed25519_public_key.sql"
  if { [ "${ASPNETCORE_ENVIRONMENT:-}" != "Production" ] || [ "${PPIQ_PRESENTATION:-off}" = "on" ]; } && [ -f "${KEYSQL}" ] && [ -f "${DEV_PUB_FILE}" ]; then
    echo "== [app] register dev Ed25519 license key (kid=${DEV_KID}) =="
    local PUB; PUB="$(tr -d '\r\n' < "${DEV_PUB_FILE}")"
    docker exec -i "${DB_CONTAINER}" psql -v ON_ERROR_STOP=1 -U "${DB_USER}" -d "${DB_NAME}" \
      -v "tenant_id=${DEV_TENANT}" -v "key_id=${DEV_KID}" -v "public_key_b64=${PUB}" < "${KEYSQL}"
  else
    echo "  (skip dev key registration: Production with PPIQ_PRESENTATION off, or fixture/seed absent)"
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