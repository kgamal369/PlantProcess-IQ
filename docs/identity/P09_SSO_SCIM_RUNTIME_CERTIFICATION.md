# PlantProcess IQ â€” P09 SSO / SCIM Runtime Certification

## Scope

This pack provides runtime certification proof for:

- Keycloak-compatible OIDC provider configuration.
- RS256 JWT signature validation from JWKS parameters.
- Issuer, audience, expiry, nbf, and signature rejection.
- JIT principal link and group-to-role mapping.
- SCIM 2.0 schema proof.
- SCIM deactivate means login-deny, never hard-delete.

## Local deterministic proof

The local proof uses a generated RS256 token and JWKS registration SQL:

- `Documentation/v5/oidc-runtime-dev/runtime-valid-id-token.jwt`
- `Documentation/v5/oidc-runtime-dev/runtime-expired-id-token.jwt`
- `Documentation/v5/oidc-runtime-dev/runtime-tampered-id-token.jwt`
- `Documentation/v5/oidc-runtime-dev/register-runtime-jwks.sql`

## Optional Keycloak reference

A reference Keycloak container is provided in:

- `deploy/identity/keycloak-reference-compose.yml`

This file is documentation/reference. The local validation pack does not require internet or a running Keycloak container.
## SCIM Deactivation Login-Deny Proof

The runtime certification pack explicitly validates SCIM deactivation/login-deny behavior:
inactive or deactivated SCIM users must not be allowed to login.