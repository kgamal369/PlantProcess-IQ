#!/usr/bin/env bash
# T02: kill stale processes that hold workspace files (the 12-Jun MSB3027 class)
# and clear half-dead ppiq-ci leftovers. Scoped strictly to this workspace / CI project.
set -uo pipefail
WS="${WORKSPACE:-$(pwd)}"
echo "== stale sweep in ${WS} =="
for pid in $(pgrep -f "PlantProcess" 2>/dev/null || true); do
  if tr '\0' ' ' < "/proc/${pid}/cmdline" 2>/dev/null | grep -q "${WS}"; then
    echo "  killing PID ${pid} (workspace-scoped PlantProcess process)"
    kill -9 "${pid}" 2>/dev/null || true
  fi
done
for pid in $(pgrep -f "dotnet|testhost|node" 2>/dev/null || true); do
  if tr '\0' ' ' < "/proc/${pid}/cmdline" 2>/dev/null | grep -q "${WS}"; then
    echo "  killing PID ${pid} (workspace-scoped runtime)"
    kill -9 "${pid}" 2>/dev/null || true
  fi
done
docker compose -p ppiq-ci down -v --remove-orphans 2>/dev/null || true
echo "== sweep done =="
exit 0