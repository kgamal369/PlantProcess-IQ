#!/bin/sh
# Presentation smoke: prove the deployed stack is demo-ready.
#  1. log in as the seeded admin (auth + DB + signing key all wired)
#  2. activate the Enterprise signed license for the demo tenant (V5 Ed25519)
#  3. confirm the active license
# Runs INSIDE a curl sibling joined to the app network, reaching the API by
# container name (the agent cannot reach the API on 127.0.0.1). Workspace is
# mounted via --volumes-from so .env and the token fixture are present.
set -eu

# .env is sourced by the caller-provided path or defaults next to this layout.
ENV_FILE="${ENV_FILE:-deploy/compose/.env}"
set -a
. "./${ENV_FILE}"
set +a

BASE="${PPIQ_SMOKE_BASE_URL:-http://plantprocess-api:5063}"
TOKEN_FILE="${PPIQ_PRESENTATION_TOKEN:-deploy/fixtures/license/enterprise.token}"
SMOKE_USER="${PPIQ_SMOKE_USERNAME:-${PlantProcess__Auth__Users__0__UserName:-admin}}"
SMOKE_PASS="${PPIQ_SMOKE_PASSWORD:-${PlantProcess__Auth__Users__0__Password:-}}"

[ -n "$SMOKE_PASS" ] || { echo "FATAL: no smoke password (PPIQ_SMOKE_PASSWORD or Auth Users 0 Password)"; exit 1; }

echo "== presentation smoke: admin login at ${BASE} =="
LOGIN_BODY="{\"userName\":\"${SMOKE_USER}\",\"password\":\"${SMOKE_PASS}\"}"
BEARER="$(curl -fsS -X POST "${BASE}/auth/login" -H "Content-Type: application/json" -d "${LOGIN_BODY}" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')"
[ -n "$BEARER" ] || { echo "FATAL: admin login returned no token"; exit 1; }
echo "  admin login OK (bearer acquired)"

echo "== activate Enterprise signed license =="
JWS="$(cat "${TOKEN_FILE}")"
curl -fsS -X POST "${BASE}/api/v5/licensing/ed25519/activate" -H "Authorization: Bearer ${BEARER}" -H "Content-Type: application/json" -d "{\"token\":\"${JWS}\"}" > /dev/null
echo "  Enterprise token activated"

echo "== confirm active license =="
curl -fsS "${BASE}/api/v5/licensing/ed25519/current" -H "Authorization: Bearer ${BEARER}"
echo ""
echo "Presentation ready: admin + Enterprise active at ${BASE}"
