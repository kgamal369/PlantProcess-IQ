-- PPIQ-104: migration watermark. migrate-and-seed records every applied script here so a
-- re-run is a no-op (idempotency by watermark, not only by idempotent SQL).
CREATE TABLE IF NOT EXISTS public.schema_migrations(
    script_name    text PRIMARY KEY,
    checksum       text NOT NULL,
    applied_at_utc timestamptz NOT NULL DEFAULT now()
);