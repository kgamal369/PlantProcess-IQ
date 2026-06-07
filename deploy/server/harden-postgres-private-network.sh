#!/usr/bin/env bash
set -euo pipefail

# PPIQ_REALIZATION_T015_CLOSE_EXPOSED_POSTGRES_PORT
# Run on the server. Postgres must be internal-only and not publicly reachable on 5432.

echo "PPIQ-T015: hardening Postgres external exposure"

if command -v ufw >/dev/null 2>&1; then
  sudo ufw deny 5432/tcp || true
  sudo ufw reload || true
fi

if command -v firewall-cmd >/dev/null 2>&1; then
  sudo firewall-cmd --permanent --remove-port=5432/tcp || true
  sudo firewall-cmd --reload || true
fi

echo "Check Docker ports:"
docker ps --format 'table {{.Names}}\t{{.Ports}}' || true

echo "PPIQ-T015 hardening completed."
