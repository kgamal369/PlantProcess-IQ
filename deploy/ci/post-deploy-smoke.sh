#!/usr/bin/env sh
# PlantProcess IQ - post-deploy smoke gate.
# Fails (exit 1) if the API health endpoint or the freshness probes do not
# answer 200 (or 401 = registered + auth required). Run by Jenkins stage 5b.
set -u
API_HEALTH="${API_HEALTH:-https://api.178.105.152.180.sslip.io/health}"
BASE="${PPIQ_PUBLIC_BASE:-https://api.178.105.152.180.sslip.io}"

echo "==> Post-deploy smoke against ${BASE}"
ok=0
for i in 1 2 3 4 5 6; do
    CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_HEALTH" --max-time 10 || echo "0")
    echo "    health attempt $i: $API_HEALTH -> $CODE"
    if [ "$CODE" = "200" ] || [ "$CODE" = "401" ]; then ok=1; break; fi
    sleep 5
done
if [ "$ok" -ne 1 ]; then
    echo "SMOKE FAIL: $API_HEALTH never returned 200/401"
    exit 1
fi

fail=0
for p in "/admin/phase1/connector-truth" "/admin/phase2/pilot/deployment-checklist" "/analytics/phase2/ml-lifecycle"; do
    CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE$p" --max-time 8 || echo "0")
    printf "    %-48s -> %s\n" "$p" "$CODE"
    case "$CODE" in
        200|401) ;;
        *) echo "    SMOKE FAIL: $p returned $CODE"; fail=1 ;;
    esac
done
[ "$fail" -eq 0 ] || exit 1
echo "Smoke gate passed."