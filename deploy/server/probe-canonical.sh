#!/usr/bin/env bash
# PPIQ T01 post-deploy probes - exit non-zero on ANY failure.
# Usage: BASE_URL=https://your-domain ADMIN_USER=ppiq-admin ADMIN_PASS=... bash probe-canonical.sh
set -euo pipefail
BASE_URL="${BASE_URL:-http://127.0.0.1:8080}"
ADMIN_USER="${ADMIN_USER:?set ADMIN_USER}"
ADMIN_PASS="${ADMIN_PASS:?set ADMIN_PASS}"

fail(){ echo "  [FAIL] $1"; exit 1; }
ok(){ echo "  [PASS] $1"; }

echo "== probes against ${BASE_URL} =="

code=$(curl -ks -o /dev/null -w '%{http_code}' "${BASE_URL}/health")
[ "$code" = "200" ] || fail "/health -> $code"; ok "/health 200"

login=$(curl -ks -X POST "${BASE_URL}/auth/login" -H 'Content-Type: application/json' \
  -d "{\"UserName\":\"${ADMIN_USER}\",\"Password\":\"${ADMIN_PASS}\"}")
token=$(echo "$login" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
[ -n "$token" ] || fail "/auth/login returned no accessToken: $login"; ok "/auth/login 200 + token"

# T06 absorbed: dashboard definitions must be 200 (ensure-templates route)
code=$(curl -ks -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $token" \
  "${BASE_URL}/analytics/dashboard/definitions?includeInactive=false&includeSystemTemplates=true")
[ "$code" = "200" ] || fail "/analytics/dashboard/definitions -> $code"; ok "dashboard definitions 200 (T06)"

code=$(curl -ks -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $token" "${BASE_URL}/admin/overview")
case "$code" in 200|403) ok "/admin/overview $code (403 acceptable only while RequireAdminMfa=true without step-up)";; *) fail "/admin/overview -> $code";; esac

echo "-- restart counts (must be 0):"
bad=0
for c in $(docker ps --format '{{.Names}}' | grep -E '^ppiq-'); do
  r=$(docker inspect -f '{{.RestartCount}}' "$c")
  echo "   $c restarts=$r"
  [ "$r" = "0" ] || bad=1
done
[ "$bad" = "0" ] || fail "a canonical container is restart-looping"
echo "== ALL PROBES GREEN =="