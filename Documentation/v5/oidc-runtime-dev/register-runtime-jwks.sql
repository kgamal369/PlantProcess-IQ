\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

INSERT INTO public.ppiq_oidc_runtime_jwks_keys
(
    tenant_id,
    provider_code,
    key_id,
    issuer,
    audience,
    algorithm,
    jwk_n_b64url,
    jwk_e_b64url,
    status
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'runtime-keycloak',
    'ppiq-runtime-rs256-20260603',
    'http://localhost:18080/realms/plantprocessiq',
    'plantprocessiq-web',
    'RS256',
    'odzvRyMTLb6ORDWop-A0YSw1NGnTOoaB3YvpdrT8xu_hRYooRvSEz_-PrdqP5RG6kDKrowX8N5iqEODvnUuKM4eOPW7BUhl1STbJ9QuEhQbhGZqFwvF052JDeVfq6RnnEz-8CwwFbZ2oKtH2m8uerVfTjsP5tI4ZGHKMNjUaacdQE1_gixMTikprnqAl4C8KxJgGO2RdKez-KyQwYhRUxwARgQluS_XSHLNowq2w0Y0qC5CkpLHF1jb-T75sChjwBhFJhrCmD7KtxiDN2chmCJw0OHiU0qolfl72wbHVAjy-LoW3fpnCX3cFED3mU0X9Xp4d0bgiO2Z-FYIoyMHK-Q',
    'AQAB',
    'active'
)
ON CONFLICT (tenant_id, provider_code, key_id)
DO UPDATE SET
    issuer = EXCLUDED.issuer,
    audience = EXCLUDED.audience,
    jwk_n_b64url = EXCLUDED.jwk_n_b64url,
    jwk_e_b64url = EXCLUDED.jwk_e_b64url,
    algorithm = 'RS256',
    status = 'active',
    retired_at_utc = NULL;