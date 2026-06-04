#!/usr/bin/env bash
set -euo pipefail

POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-ppiq-postgres}"
POSTGRES_USER="${POSTGRES_USER:?Set POSTGRES_USER}"
POSTGRES_DB="${POSTGRES_APP_DB:-postgres}"
NEW_POSTGRES_PASSWORD="${NEW_POSTGRES_PASSWORD:?Set NEW_POSTGRES_PASSWORD}"

echo "Rotating PostgreSQL password for user ${POSTGRES_USER} inside container ${POSTGRES_CONTAINER}..."

docker exec -i "${POSTGRES_CONTAINER}" psql \
  -U "${POSTGRES_USER}" \
  -d "${POSTGRES_DB}" \
  -v ON_ERROR_STOP=1 \
  -v postgres_user="${POSTGRES_USER}" \
  -v new_password="${NEW_POSTGRES_PASSWORD}" <<SQL
ALTER USER :"postgres_user" WITH PASSWORD :'new_password';
SQL

echo "PASS: PostgreSQL password rotated. Update app/server env and redeploy immediately."
