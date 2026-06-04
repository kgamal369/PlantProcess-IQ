#!/usr/bin/env bash
set -e

echo "Creating PlantProcess IQ databases..."

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    SELECT 'CREATE DATABASE ${POSTGRES_APP_DB}'
    WHERE NOT EXISTS (
        SELECT FROM pg_database WHERE datname = '${POSTGRES_APP_DB}'
    )\gexec

    SELECT 'CREATE DATABASE ${POSTGRES_WEBSITE_DB}'
    WHERE NOT EXISTS (
        SELECT FROM pg_database WHERE datname = '${POSTGRES_WEBSITE_DB}'
    )\gexec
EOSQL

echo "Databases ready:"
echo " - ${POSTGRES_APP_DB}"
echo " - ${POSTGRES_WEBSITE_DB}"