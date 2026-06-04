#!/usr/bin/env bash
set -euo pipefail

POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-ppiq-postgres}"
POSTGRES_USER="${POSTGRES_USER:?Set POSTGRES_USER from the server .env}"
POSTGRES_DB="${POSTGRES_APP_DB:?Set POSTGRES_APP_DB from the server .env}"

SCRIPT_ROOT="${SCRIPT_ROOT:-/opt/plantprocess-iq/Backend/database/scripts}"

scripts=(
  "200_phase02_ml_foundation_feature_store_pgvector.sql"
  "201_phase02_ml_feature_store_v6_completion.sql"
  "202_phase02_ml_compute_basic_correlations_hotfix.sql"
  "203_phase02_ml_compute_v6_wrapper_hotfix.sql"
)

for script in "${scripts[@]}"; do
  echo "Applying ${script} to server Docker PostgreSQL..."
  docker exec -i "${POSTGRES_CONTAINER}" psql \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}" \
    -v ON_ERROR_STOP=1 \
    < "${SCRIPT_ROOT}/${script}"
done

echo "Running server ML proof..."
docker exec -i "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v ON_ERROR_STOP=1 <<'SQL'
SELECT * FROM public.ppiq_ml_seed_foundation_catalog();
SELECT * FROM public.ppiq_ml_refresh_feature_store_v6(3650);
SELECT * FROM public.ppiq_ml_compute_correlations_v6('defect.rate_per_m2', 'coil', 3650);
SQL

echo "PASS: server DB scripts and ML proof completed."
