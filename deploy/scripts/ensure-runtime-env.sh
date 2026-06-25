#!/usr/bin/env bash
# deploy/scripts/ensure-runtime-env.sh
# Permanent self-bootstrapping runtime config: reuse persisted -> respect override -> else generate.
set -euo pipefail
ENV_FILE="${1:?usage: ensure-runtime-env.sh <env_file> <preserve_dir> [template]}"
PRESERVE_DIR="${2:?}"; TEMPLATE="${3:-env/profiles/server.env.example}"
PRESERVE_ENV="${PRESERVE_DIR}/.env"; CRED_FILE="${PRESERVE_DIR}/FIRST_LOGIN.txt"
mkdir -p "${PRESERVE_DIR}" "$(dirname "${ENV_FILE}")"
if [ -f "${PRESERVE_ENV}" ]; then cp -f "${PRESERVE_ENV}" "${ENV_FILE}"; echo "ensure-runtime-env: reused persisted secrets"; exit 0; fi
if [ -f "${ENV_FILE}" ]; then cp -f "${ENV_FILE}" "${PRESERVE_ENV}"; chmod 600 "${PRESERVE_ENV}"; echo "ensure-runtime-env: adopted operator-provided .env"; exit 0; fi
echo "ensure-runtime-env: generating fresh runtime secrets"
gen() { openssl rand -hex "$1"; }
PG_PASS="$(gen 24)"; SIGNING_KEY="$(gen 48)"; ADMIN_USER="ppiq-owner"; ADMIN_PASS="$(gen 16)"
[ -f "${TEMPLATE}" ] && cp -f "${TEMPLATE}" "${ENV_FILE}" || : > "${ENV_FILE}"
sed -i '/_Password_REMOVED_FROM_TRACKED_TEMPLATE=/d' "${ENV_FILE}"   # strip dead placeholders
setkv() { local k="$1" v="$2"; if grep -qE "^${k}=" "${ENV_FILE}" 2>/dev/null; then sed -i "s|^${k}=.*|${k}=${v}|" "${ENV_FILE}"; else printf '%s=%s\n' "${k}" "${v}" >> "${ENV_FILE}"; fi; }
val() { grep -E "^$1=" "${ENV_FILE}" 2>/dev/null | head -1 | cut -d= -f2-; }
PG_USER="$(val POSTGRES_USER)"; PG_USER="${PG_USER:-plantprocess}"
PG_DB="$(val POSTGRES_DB)";     PG_DB="${PG_DB:-plantprocessiq}"
PG_HOST="$(val POSTGRES_HOST)"; PG_HOST="${PG_HOST:-plantprocess-postgres}"
PG_PORT="$(val POSTGRES_PORT)"; PG_PORT="${PG_PORT:-5432}"
setkv POSTGRES_PASSWORD "${PG_PASS}"
setkv ConnectionStrings__PlantProcessDb "Host=${PG_HOST};Port=${PG_PORT};Database=${PG_DB};Username=${PG_USER};Password=${PG_PASS}"
setkv PlantProcess__Auth__SigningKey "${SIGNING_KEY}"
setkv PlantProcess__Auth__BootstrapAdminPassword "__DISABLED__"
setkv PPIQ_BOOTSTRAP_ADMIN_PASSWORD "__DISABLED__"
setkv PlantProcess__Auth__Users__0__UserName "${ADMIN_USER}"
setkv PlantProcess__Auth__Users__0__Password "${ADMIN_PASS}"
setkv PlantProcess__Auth__Users__0__Role "Admin"
setkv PlantProcess__Auth__Users__0__IsBootstrapAdmin "false"
setkv PPIQ_SMOKE_USERNAME "${ADMIN_USER}"; setkv PPIQ_SMOKE_PASSWORD "${ADMIN_PASS}"
setkv VITE_SMOKE_USERNAME "${ADMIN_USER}"; setkv VITE_SMOKE_PASSWORD "${ADMIN_PASS}"
chmod 600 "${ENV_FILE}"; cp -f "${ENV_FILE}" "${PRESERVE_ENV}"; chmod 600 "${PRESERVE_ENV}"
umask 077
{ echo "PlantProcess IQ first-login owner ($(date -u +%FT%TZ))"; echo "username: ${ADMIN_USER}"; echo "password: ${ADMIN_PASS}"; echo "Rotate after first login. Secrets at ${PRESERVE_ENV}."; } > "${CRED_FILE}"
echo "ensure-runtime-env: generated + persisted; first-login creds in ${CRED_FILE}"