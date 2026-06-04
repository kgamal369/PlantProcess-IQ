#!/usr/bin/env bash
set -euo pipefail

HOST="${1:-178.105.152.180}"

echo "PlantProcess IQ V7 Phase 1 external exposure proof"
echo "Target: ${HOST}"
echo

if ! command -v nmap >/dev/null 2>&1; then
  echo "ERROR: nmap is required on the machine running this proof." >&2
  exit 1
fi

SCAN_OUTPUT="$(nmap -Pn -p 22,80,443,9090,5432,6379,3000,3001,5063,5341,16686 "${HOST}")"
echo "${SCAN_OUTPUT}"

fail_if_open() {
  local port="$1"
  local name="$2"

  if echo "${SCAN_OUTPUT}" | grep -E "^${port}/tcp[[:space:]]+open" >/dev/null; then
    echo "FAIL: ${name} port ${port} is publicly open."
    exit 1
  fi
}

fail_if_open 5432 "PostgreSQL"
fail_if_open 6379 "Redis"
fail_if_open 3000 "Internal frontend/dev"
fail_if_open 3001 "Grafana"
fail_if_open 5063 "Direct API"
fail_if_open 5341 "Seq"
fail_if_open 16686 "Jaeger"

echo
echo "PASS: data/internal service ports are not publicly open."
echo "NOTE: 80/443 may be public. 9090 may be public only when protected by Caddy basicauth."
