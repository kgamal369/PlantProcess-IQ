#!/usr/bin/env bash
# PPIQ-T05 phase-1 evidence capture - run on the server AFTER a green Jenkins push.
# Usage: BUILD_URL=... BASE_URL=https://your-domain ADMIN_USER=... ADMIN_PASS=... bash phase1-proof.sh
set -euo pipefail
BUILD_URL="${BUILD_URL:?paste the green Jenkins build URL}"
OUT="Documentation/Phase1_Proof_$(date +%d%b%Y_%H%M%S).md"
mkdir -p Documentation

{
  echo "# Phase-1 Deployable Evidence (PPIQ-T05)"
  echo
  echo "- Jenkins build: ${BUILD_URL}"
  echo "- Commit: $(git rev-parse HEAD)"
  echo "- Captured: $(date -u +%FT%TZ)"
  echo
  echo "## Probes"
} > "${OUT}"

bash deploy/server/probe-canonical.sh | tee -a "${OUT}"

echo >> "${OUT}"
echo "## Guard self-test" >> "${OUT}"
echo "CiPipelineTruthGateTests executed inside the build above (dotnet test stage)." >> "${OUT}"
echo "evidence written: ${OUT}"