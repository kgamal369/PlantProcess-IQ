\encoding UTF8
SET client_encoding = 'UTF8';
SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

INSERT INTO public.ppiq_ed25519_license_public_keys
(
  tenant_id,
  key_id,
  public_key_b64,
  algorithm,
  status
)
VALUES
(
  '00000000-0000-0000-0000-000000000001',
  'ppiq-dev-ed25519-20260603',
  'GOYHSCJ7+rPO3pXfJmQqmhBE4WzA62+BuCLom3suIiE=',
  'Ed25519',
  'active'
)
ON CONFLICT (tenant_id, key_id)
DO UPDATE SET
  public_key_b64 = EXCLUDED.public_key_b64,
  algorithm = 'Ed25519',
  status = 'active',
  retired_at_utc = NULL;