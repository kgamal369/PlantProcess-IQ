#!/usr/bin/env bash
# T02 stage 7: build images, tag previous as :previous, recreate plantprocessiq,
# health-gate, AUTOMATIC rollback to :previous on failure.
set -euo pipefail
COMPOSE_PROJECT="${COMPOSE_PROJECT:-plantprocessiq}"
COMPOSE_FILE="${COMPOSE_FILE:-Infrastructure/deploy/docker-compose.demo.yml}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:8080/health}"

echo "== tagging current images as :previous (rollback anchors) =="
for img in $(docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" config --images | sort -u); do
  docker image inspect "${img}" >/dev/null 2>&1 && docker tag "${img}" "${img%%:*}:previous" || true
done

echo "== building =="
docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" build

echo "== recreating =="
docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" up -d --remove-orphans

echo "== health gate: ${HEALTH_URL} =="
ok=0
for i in $(seq 1 45); do
  code=$(curl -ks -o /dev/null -w '%{http_code}' "${HEALTH_URL}" || true)
  [ "$code" = "200" ] && ok=1 && break
  sleep 2
done

if [ "${ok}" != "1" ]; then
  echo "!! HEALTH GATE FAILED - rolling back to :previous"
  for img in $(docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" config --images | sort -u); do
    base="${img%%:*}"
    docker image inspect "${base}:previous" >/dev/null 2>&1 && docker tag "${base}:previous" "${img}" || true
  done
  docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" up -d --remove-orphans
  exit 1
fi
echo "== DEPLOY GREEN =="