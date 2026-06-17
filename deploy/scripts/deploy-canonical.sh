#!/usr/bin/env bash
# Stage 7: build images, tag current images as :previous (rollback anchors),
# recreate the canonical plantprocessiq stack, run a health gate, and perform an
# AUTOMATIC rollback to :previous on failure.
#
# Runs under nounset; every input is env-configurable with a sensible default and
# the one fatal precondition (compose file present) is validated up-front, so a
# bad value fails loudly here instead of half-way through a deploy.
set -euo pipefail

# --- inputs (env-configurable; never hardcode targets) ---
COMPOSE_PROJECT="${COMPOSE_PROJECT:-plantprocessiq}"
COMPOSE_FILE="${COMPOSE_FILE:-deploy/compose/docker-compose.yml}"
HEALTH_NETWORK="${HEALTH_NETWORK:-ppiq-network}"
HEALTH_TARGET="${HEALTH_TARGET:-http://ppiq-app-api:5063/health}"
HEALTH_CURL_IMAGE="${HEALTH_CURL_IMAGE:-curlimages/curl:8.10.1}"
HEALTH_RETRIES="${HEALTH_RETRIES:-45}"

# --- fail-loud precondition: the canonical compose file must exist ---
if [ ! -f "${COMPOSE_FILE}" ]; then
  echo "!! deploy aborted: compose file not found: ${COMPOSE_FILE}" >&2
  exit 1
fi

echo "== tagging current images as :previous (rollback anchors) =="
# shellcheck disable=SC2046  # intentional word-splitting over the image list
for img in $(docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" config --images | sort -u); do
  docker image inspect "${img}" >/dev/null 2>&1 && docker tag "${img}" "${img%%:*}:previous" || true
done

echo "== building =="
docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" build

echo "== recreating =="
docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" up -d --remove-orphans

echo "== health gate: ${HEALTH_TARGET} =="
ok=0
for _ in $(seq 1 "${HEALTH_RETRIES}"); do
  code=$(docker run --rm --network "${HEALTH_NETWORK}" "${HEALTH_CURL_IMAGE}" \
    -ks -o /dev/null -w '%{http_code}' "${HEALTH_TARGET}" 2>/dev/null || true)
  [ "${code}" = "200" ] && ok=1 && break
  sleep 2
done

if [ "${ok}" != "1" ]; then
  echo "!! HEALTH GATE FAILED - rolling back to :previous" >&2
  # shellcheck disable=SC2046  # intentional word-splitting over the image list
  for img in $(docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" config --images | sort -u); do
    base="${img%%:*}"
    docker image inspect "${base}:previous" >/dev/null 2>&1 && docker tag "${base}:previous" "${img}" || true
  done
  docker compose -p "${COMPOSE_PROJECT}" -f "${COMPOSE_FILE}" up -d --remove-orphans
  exit 1
fi
echo "== DEPLOY GREEN =="