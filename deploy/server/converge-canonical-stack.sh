#!/usr/bin/env bash
# =====================================================================================
# PPIQ T01 - converge the server to ONE compose project: plantprocessiq
# Deletes: ppiq-demo project + every plantprocess-* duplicate container/network.
# Keeps:   plantprocessiq project (ppiq-app-api/workers/web, ppiq-website-web,
#          ppiq-caddy, ppiq-postgres, ppiq-network) and ALL volumes (data is sacred).
# Safe: full backup first, explicit confirmation, never touches volumes.
# =====================================================================================
set -euo pipefail

CANONICAL="plantprocessiq"
TS=$(date +%d%b%Y_%H%M%S)
BACKUP="/root/ppiq-converge-backup-${TS}"

echo "== PPIQ T01 convergence =="
echo "-- current compose projects:"
docker compose ls || true
echo
echo "-- backup to ${BACKUP}"
mkdir -p "${BACKUP}"
docker compose ls --format json > "${BACKUP}/compose-projects.json" 2>/dev/null || true
docker ps -a --format '{{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Label "com.docker.compose.project"}}' > "${BACKUP}/containers.tsv"
for f in /opt/*/deploy/compose/.env /opt/*/.env /opt/*/Caddyfile /opt/*/deploy/**/Caddyfile; do
  [ -f "$f" ] && cp -v --parents "$f" "${BACKUP}/" || true
done
echo "-- backup done."

echo
echo "-- containers that will be REMOVED (project != ${CANONICAL}):"
VICTIMS=$(docker ps -a --format '{{.Names}}\t{{.Label "com.docker.compose.project"}}' \
  | awk -v keep="${CANONICAL}" -F'\t' '($2=="ppiq-demo") || ($1 ~ /^plantprocess-/) || ($2 ~ /^plantprocess-/) {print $1}' | sort -u)
if [ -z "${VICTIMS}" ]; then
  echo "   (none - already converged)"
else
  echo "${VICTIMS}" | sed 's/^/   - /'
  read -r -p "Type DELETE to remove these orphan containers (volumes are NOT touched): " CONFIRM
  [ "${CONFIRM}" = "DELETE" ] || { echo "aborted."; exit 1; }
  echo "${VICTIMS}" | xargs -r docker rm -f
fi

echo
echo "-- removing orphan compose projects (down WITHOUT -v: volumes preserved)"
docker compose -p ppiq-demo down --remove-orphans 2>/dev/null || true
for p in $(docker network ls --format '{{.Name}}' | grep -E '^plantprocess-' || true); do
  docker network rm "$p" 2>/dev/null || true
done

echo
echo "-- final state:"
docker compose ls
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Label "com.docker.compose.project"}}'
echo
echo "== converged. Backup: ${BACKUP} =="
echo "Next: cd to the repo, copy deploy/compose/.env.example -> .env, fill values, then redeploy via Jenkins (T02)."