-- DEV-ONLY fixture: registers the development Ed25519 license public key.
-- Never run in production; production registers the customer signing key via the ops/registration path.
-- Idempotent and RLS-scoped. Identity is injected by the caller via psql -v (nothing hardcoded).
BEGIN;
SELECT set_config('app.current_tenant', :'tenant_id', true);
INSERT INTO public.ppiq_ed25519_license_public_keys (tenant_id, key_id, public_key_b64)
VALUES (:'tenant_id', :'key_id', :'public_key_b64')
ON CONFLICT (tenant_id, key_id) DO UPDATE
  SET public_key_b64 = EXCLUDED.public_key_b64, status = 'active', retired_at_utc = NULL;
COMMIT;
SELECT set_config('app.current_tenant', :'tenant_id', false);
SELECT key_id, algorithm, status FROM public.ppiq_ed25519_license_public_keys WHERE tenant_id = :'tenant_id';