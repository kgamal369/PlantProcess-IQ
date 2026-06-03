-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P01 · Security & Hygiene Foundation
-- Auth storage upgrade: Argon2id columns + legacy PBKDF2 compatibility.
-- Safe to run multiple times.
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

ALTER TABLE IF EXISTS public.app_users
    ADD COLUMN IF NOT EXISTS password_algorithm text NOT NULL DEFAULT 'pbkdf2-sha256';

ALTER TABLE IF EXISTS public.app_users
    ADD COLUMN IF NOT EXISTS password_hash_parameters jsonb NOT NULL DEFAULT '{}'::jsonb;

UPDATE public.app_users
SET password_algorithm = 'pbkdf2-sha256',
    password_hash_parameters = jsonb_build_object(
        'algorithm', 'pbkdf2-sha256',
        'iterations', COALESCE(password_iterations, 210000),
        'hashBytes', 32
    )
WHERE password_algorithm IS NULL
   OR password_algorithm = ''
   OR password_hash_parameters = '{}'::jsonb;

CREATE INDEX IF NOT EXISTS ix_app_users_auth_algorithm
ON public.app_users(password_algorithm)
WHERE is_enabled = true;

COMMENT ON COLUMN public.app_users.password_algorithm IS
'Doctrine v5 P01: argon2id is the default for new/rehash users; pbkdf2-sha256 is legacy verification only.';

COMMENT ON COLUMN public.app_users.password_hash_parameters IS
'Doctrine v5 P01: serialized password hash parameters for audit and transparent future migration.';

COMMIT;

SELECT 'P01 auth Argon2id compatibility columns installed' AS status;