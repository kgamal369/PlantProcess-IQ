#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:?usage: ensure-runtime-env.sh <env_file> <preserve_dir> [template]}"; PRESERVE_DIR="${2:?}"; TEMPLATE="${3:-env/profiles/server.env.example}"
PRESERVE_ENV="${PRESERVE_DIR}/.env"; CRED_FILE="${PRESERVE_DIR}/FIRST_LOGIN.txt"
mkdir -p "${PRESERVE_DIR}" "$(dirname "${ENV_FILE}")"
# --- DB-coupled secret preservation -------------------------------------------
# POSTGRES_PASSWORD is written into the Postgres data volume on its FIRST init and
# never changes afterwards; the admin password and signing key are likewise bound
# to already-provisioned rows / live sessions. A stale-key regeneration must NOT
# rotate these, or the regenerated .env diverges from durable state and Postgres
# auth fails with 28P01. RULE: never delete ${PRESERVE_DIR}/.env on its own; if it
# is ever removed, wipe the Postgres data volume in the SAME step so they
# re-initialize together.
PRIOR_PG=""; PRIOR_AP=""; PRIOR_SIGN=""
if [ -f "${PRESERVE_ENV}" ] && grep -q "^PPIQ_API_UPSTREAM=" "${PRESERVE_ENV}" && grep -q "^PPIQ_DEMO_SOURCES_MODE=" "${PRESERVE_ENV}"; then cp -f "${PRESERVE_ENV}" "${ENV_FILE}"; echo "ensure-runtime-env: reused persisted secrets (validated)"; exit 0; fi
if [ -f "${PRESERVE_ENV}" ]; then echo "ensure-runtime-env: persisted .env is stale (missing new keys) -> regenerating (preserving DB-coupled secrets)" >&2; PRIOR_PG="$(grep -E '^POSTGRES_PASSWORD=' "${PRESERVE_ENV}" | head -1 | cut -d= -f2- || true)"; PRIOR_AP="$(grep -E '^PlantProcess__Auth__Users__0__Password=' "${PRESERVE_ENV}" | head -1 | cut -d= -f2- || true)"; PRIOR_SIGN="$(grep -E '^PlantProcess__Auth__SigningKey=' "${PRESERVE_ENV}" | head -1 | cut -d= -f2- || true)"; rm -f "${PRESERVE_ENV}"; fi
if [ -f "${ENV_FILE}" ]; then cp -f "${ENV_FILE}" "${PRESERVE_ENV}"; chmod 600 "${PRESERVE_ENV}"; echo "ensure-runtime-env: adopted operator-provided .env"; exit 0; fi
echo "ensure-runtime-env: generating fresh runtime secrets"
gen(){ openssl rand -hex "$1"; }
PG="${PRIOR_PG:-$(gen 24)}"; SIGN="${PRIOR_SIGN:-$(gen 48)}"; AU="sysadmin"; AP="${PRIOR_AP:-$(gen 16)}"
[ -f "${TEMPLATE}" ] && cp -f "${TEMPLATE}" "${ENV_FILE}" || : > "${ENV_FILE}"
sed -i '1s/^\xEF\xBB\xBF//' "${ENV_FILE}"   # strip a UTF-8 BOM if the template carried one
sed -i '/_Password_REMOVED_FROM_TRACKED_TEMPLATE=/d' "${ENV_FILE}"
setkv(){ local k="$1" v="$2"; if grep -qE "^${k}=" "${ENV_FILE}" 2>/dev/null; then sed -i "s|^${k}=.*|${k}=${v}|" "${ENV_FILE}"; else printf "%s=%s\n" "${k}" "${v}" >> "${ENV_FILE}"; fi; }
val(){ grep -E "^$1=" "${ENV_FILE}" 2>/dev/null | head -1 | cut -d= -f2-; }
U="$(val POSTGRES_USER)"; U="${U:-plantprocess}"; D="$(val POSTGRES_DB)"; D="${D:-plantprocessiq}"; PT="$(val POSTGRES_PORT)"; PT="${PT:-5432}"
H="plantprocess-postgres"
setkv POSTGRES_HOST "${H}"
setkv POSTGRES_PASSWORD "${PG}"
setkv ConnectionStrings__PlantProcessDb "Host=${H};Port=${PT};Database=${D};Username=${U};Password=${PG}"
setkv PlantProcess__Auth__SigningKey "${SIGN}"
setkv PlantProcess__Auth__BootstrapAdminPassword "__DISABLED__"; setkv PPIQ_BOOTSTRAP_ADMIN_PASSWORD "__DISABLED__"
setkv PlantProcess__Auth__Users__0__UserName "${AU}"; setkv PlantProcess__Auth__Users__0__Password "${AP}"
setkv PlantProcess__Auth__Users__0__Role "Admin"; setkv PlantProcess__Auth__Users__0__IsBootstrapAdmin "false"
setkv PlantProcess__Auth__Users__0__DisplayName "PPIQ-System-Administrator"
setkv PPIQ_SMOKE_USERNAME "${AU}"; setkv PPIQ_SMOKE_PASSWORD "${AP}"; setkv VITE_SMOKE_USERNAME "${AU}"; setkv VITE_SMOKE_PASSWORD "${AP}"
setkv PPIQ_DEMO_SOURCES_MODE "disabled"
setkv PPIQ_RUN_E2E "off"
setkv PPIQ_PRESENTATION "on"
# Public host: one variable drives every browser-facing URL (override per customer via PPIQ_SITE_HOST).
PUBLIC_HOST="${PPIQ_SITE_HOST:-178.105.152.180.sslip.io}"
setkv SITE_HOST "${PUBLIC_HOST}"; setkv WEBSITE_HOST "website.${PUBLIC_HOST}"
setkv VITE_API_BASE_URL "https://api.${PUBLIC_HOST}"
setkv VITE_WEBSITE_API_BASE_URL "https://api.${PUBLIC_HOST}"
setkv PLANTPROCESS_ALLOWED_ORIGINS "https://app.${PUBLIC_HOST},https://${PUBLIC_HOST},https://website.${PUBLIC_HOST}"
setkv CADDY_AUTO_HTTPS "off"; setkv ACME_EMAIL "admin@example.invalid"
setkv PPIQ_API_UPSTREAM "plantprocess-api:5063"; setkv PPIQ_APP_UPSTREAM "plantprocess-web:80"; setkv PPIQ_WEBSITE_UPSTREAM "plantprocess-web:80"
chmod 600 "${ENV_FILE}"; cp -f "${ENV_FILE}" "${PRESERVE_ENV}"; chmod 600 "${PRESERVE_ENV}"
umask 077
{ echo "PlantProcess IQ first-login owner ($(date -u +%FT%TZ))"; echo "username: ${AU}"; echo "password: ${AP}"; echo "Rotate after first login. Secrets at ${PRESERVE_ENV}."; } > "${CRED_FILE}"
echo "ensure-runtime-env: generated + persisted; first-login creds in ${CRED_FILE}"
