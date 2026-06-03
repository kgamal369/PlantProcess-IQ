# PlantProcess IQ — Doctrine v5 P09/P10 Runbook

## P09 — Enterprise Identity: SSO & SCIM

Installed capabilities:

- Per-tenant SSO provider configuration.
- Mock OIDC IdP token generation for local/e2e validation.
- SSO login with signature and expiry validation.
- JIT principal linking/provisioning.
- Claim/group to app-role and plant-role mapping.
- SCIM 2.0 Users and Groups endpoints.
- SCIM bearer-token hashes.
- Deactivate semantics: SCIM delete disables login and never hard-deletes user data.
- Identity provisioning audit.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p09_acceptance();
```

API smoke:

```http
GET /api/v5/sso/health
POST /api/v5/sso/mock-idp/token
POST /api/v5/sso/login
GET /api/v5/sso/role-mappings?providerCode=mock-idp
POST /api/v5/sso/role-mappings
GET /api/v5/scim/v2/ServiceProviderConfig
POST /api/v5/scim/v2/Users
GET /api/v5/scim/v2/Users
PUT /api/v5/scim/v2/Users/{id}
DELETE /api/v5/scim/v2/Users/{id}
POST /api/v5/scim/v2/Groups
```

SCIM local token:

```text
local-dev-scim-token
```

## P10 — Signed Licensing & Anti-Tamper

Installed capabilities:

- Signed license artifact storage.
- Offline verification endpoint.
- Dev signing tool using built-in ECDSA-P256.
- Entitlement projection from verified signed license only.
- License activation lifecycle.
- Current entitlement resolver.
- Tamper-test endpoint proving payload edits invalidate signature.
- License lifecycle/audit events.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p10_acceptance();
SELECT * FROM public.ppiq_v5_license_source_of_truth;
```

API smoke:

```http
GET /api/v5/licensing/health
POST /api/v5/licensing/dev/create-license
POST /api/v5/licensing/activate
GET /api/v5/licensing/current
POST /api/v5/licensing/verify
POST /api/v5/licensing/tamper-test
```

Full validation:

```powershell
.\tools\v5\validate-p09-p10.ps1
```
