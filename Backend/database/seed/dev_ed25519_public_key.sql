-- PPIQ-V1: neutralized redundant seed.
-- The dev Ed25519 license public key (kid=ppiq-dev-ed25519, tenant=00000000-0000-0000-0000-000000000001)
-- is registered idempotently during the MIGRATION phase by the ppiq.ps1 "registering dev Ed25519
-- license public key" step (and 650_remaining_p10_ed25519_verified_license.sql), proven active in the
-- run log. This file required psql -v key_id / -v public_key variables that the generic seed loop does
-- not pass, causing: ERROR syntax error at or near ":". It is now a safe no-op so the seed loop
-- completes. The original is preserved under deploy/.ppiq-backups.
\echo 'dev_ed25519_public_key.sql: no-op (dev Ed25519 key already registered in the migration phase).'