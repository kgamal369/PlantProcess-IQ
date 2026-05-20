#!/usr/bin/env sh
set -eu

API_HEALTH_URL="${PPIQ_INTERNAL_API_HEALTH_URL:-http://plantprocess-api:5063/health}"
API_DB_HEALTH_URL="${PPIQ_INTERNAL_API_DB_HEALTH_URL:-http://plantprocess-api:5063/db-health}"
APP_HEALTH_URL="${PPIQ_INTERNAL_APP_HEALTH_URL:-http://plantprocess-app-web/health}"
WEBSITE_HEALTH_URL="${PPIQ_INTERNAL_WEBSITE_HEALTH_URL:-http://plantprocess-website/health}"

SMOKE_USERNAME="${PPIQ_SMOKE_USERNAME:-admin}"
SMOKE_PASSWORD="${PPIQ_SMOKE_PASSWORD:-ChangeMe123!}"

echo "============================================================"
echo "PlantProcess IQ — Post Deploy Smoke Test"
echo "============================================================"
echo "API health URL     : $API_HEALTH_URL"
echo "API DB health URL  : $API_DB_HEALTH_URL"
echo "App health URL     : $APP_HEALTH_URL"
echo "Website health URL : $WEBSITE_HEALTH_URL"
echo "============================================================"

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

"$SCRIPT_DIR/wait-for-http.sh" "$API_HEALTH_URL" 90
"$SCRIPT_DIR/wait-for-http.sh" "$API_DB_HEALTH_URL" 90
"$SCRIPT_DIR/wait-for-http.sh" "$APP_HEALTH_URL" 60 "plantprocess-web-ok"
"$SCRIPT_DIR/wait-for-http.sh" "$WEBSITE_HEALTH_URL" 60 "plantprocess-website-ok"

echo "[smoke] Testing auth login..."

LOGIN_BODY="$(cat <<EOF
{
  "UserName": "$SMOKE_USERNAME",
  "Password": "$SMOKE_PASSWORD"
}
EOF
)"

LOGIN_RESPONSE="$(curl -k -s \
  -H "Content-Type: application/json" \
  -d "$LOGIN_BODY" \
  "http://plantprocess-api:5063/auth/login" || true)"

echo "$LOGIN_RESPONSE" | grep -E "accessToken|token" >/dev/null 2>&1 || {
  echo "[smoke] ERROR: Login response does not contain access token."
  echo "$LOGIN_RESPONSE"
  exit 1
}

TOKEN="$(echo "$LOGIN_RESPONSE" | sed -n 's/.*"accessToken"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"

if [ -z "$TOKEN" ]; then
  TOKEN="$(echo "$LOGIN_RESPONSE" | sed -n 's/.*"token"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
fi

if [ -z "$TOKEN" ]; then
  echo "[smoke] ERROR: Could not extract token from login response."
  echo "$LOGIN_RESPONSE"
  exit 1
fi

echo "[smoke] Login OK."

check_api() {
  NAME="$1"
  URL="$2"

  echo "[smoke] Checking $NAME → $URL"

  STATUS_CODE="$(curl -k -s -o /tmp/ppiq_api_body.txt -w "%{http_code}" \
    -H "Authorization: Bearer $TOKEN" \
    "$URL" || true)"

  BODY="$(cat /tmp/ppiq_api_body.txt 2>/dev/null || true)"

  if [ "$STATUS_CODE" != "200" ]; then
    echo "[smoke] ERROR: $NAME returned HTTP $STATUS_CODE"
    echo "$BODY"
    exit 1
  fi

  echo "[smoke] OK: $NAME"
}

check_api "Admin jobs monitor" "http://plantprocess-api:5063/admin/jobs-monitor"
check_api "Dashboard metadata" "http://plantprocess-api:5063/dashboarding/metadata"
check_api "Connector catalog" "http://plantprocess-api:5063/admin/connectors/catalog"
check_api "Readiness" "http://plantprocess-api:5063/validation/readiness"

echo "============================================================"
echo "PlantProcess IQ — Post Deploy Smoke Test PASSED"
echo "============================================================"