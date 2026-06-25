#!/usr/bin/env bash
# Build images, tag current as :previous, recreate the canonical plantprocessiq stack
# (base + server overlay so Caddy starts), health-gate, and auto-rollback on failure.
set -euo pipefail
COMPOSE_PROJECT="${COMPOSE_PROJECT:-plantprocessiq}"
COMPOSE_BASE="${COMPOSE_BASE:-deploy/compose/docker-compose.yml}"
COMPOSE_SERVER="${COMPOSE_SERVER:-deploy/compose/docker-compose.server.yml}"
ENV_FILE="${ENV_FILE:-deploy/compose/.env}"
HEALTH_NETWORK="${HEALTH_NETWORK:-${COMPOSE_PROJECT}_plantprocess-private}"
HEALTH_TARGET="${HEALTH_TARGET:-http://plantprocess-api:5063/health}"
HEALTH_CURL_IMAGE="${HEALTH_CURL_IMAGE:-curlimages/curl:8.10.1}"
HEALTH_RETRIES="${HEALTH_RETRIES:-45}"

for f in "${COMPOSE_BASE}" "${COMPOSE_SERVER}"; do
  [ -f "${f}" ] || { echo "!! deploy aborted: compose file not found: ${f}" >&2; exit 1; }
done
dc() { docker compose -p "${COMPOSE_PROJECT}" --env-file "${ENV_FILE}" -f "${COMPOSE_BASE}" -f "${COMPOSE_SERVER}" "$@"; }

echo "== tagging current images as :previous (rollback anchors) =="
for img in $(dc config --images | sort -u); do
  docker image inspect "${img}" >/dev/null 2>&1 && docker tag "${img}" "${img%%:*}:previous" || true
done

echo "== building =="
dc build

echo "== recreating =="
dc up -d --remove-orphans

echo "== health gate: ${HEALTH_TARGET} (network ${HEALTH_NETWORK}) =="
ok=0
for _ in $(seq 1 "${HEALTH_RETRIES}"); do
  code=$(docker run --rm --network "${HEALTH_NETWORK}" "${HEALTH_CURL_IMAGE}" \
    -ks -o /dev/null -w '%{http_code}' "${HEALTH_TARGET}" 2>/dev/null || true)
  [ "${code}" = "200" ] && ok=1 && break
  sleep 2
done

if [ "${ok}" != "1" ]; then
  echo "!! HEALTH GATE FAILED - rolling back to :previous" >&2
  for img in $(dc config --images | sort -u); do
    base="${img%%:*}"
    docker image inspect "${base}:previous" >/dev/null 2>&1 && docker tag "${base}:previous" "${img}" || true
  done
  dc up -d --remove-orphans
  exit 1
fi
echo "== DEPLOY GREEN =="