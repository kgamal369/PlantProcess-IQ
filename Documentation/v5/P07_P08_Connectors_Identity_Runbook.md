# PlantProcess IQ — Doctrine v5 P07/P08 Runbook

## P07 — Plant Connector Breadth

Installed capabilities:

- Read-only mock historian / OPC-style connector foundation.
- Connector registry with read-only database constraint.
- Tag catalog mapped to canonical telemetry target.
- Connector truth snapshots with live/certified state.
- Historical backfill job table.
- Incremental sync checkpoints.
- Idempotent telemetry sample store.
- Schema drift / availability event table.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p07_acceptance();
```

API smoke:

```http
GET /api/v5/plant-connectors/health
POST /api/v5/plant-connectors/mock-historian/register
POST /api/v5/plant-connectors/mock-historian/read
POST /api/v5/plant-connectors/backfill
GET /api/v5/plant-connectors/truth
POST /api/v5/plant-connectors/mock-historian/write
```

## P08 — Enterprise Identity: MFA & Sessions

Installed capabilities:

- Tenant identity policy.
- TOTP MFA enrollment with otpauth URI.
- TOTP verification.
- Recovery codes stored as hashes only.
- Session registry with idle and absolute expiry.
- Manual session revoke.
- Refresh-token reuse detection table.
- Account protection lockout/throttling.
- Password policy check endpoint.
- Auth audit events.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p08_acceptance();
```

API smoke:

```http
GET /api/v5/enterprise-identity/health
GET /api/v5/enterprise-identity/policy
POST /api/v5/enterprise-identity/mfa/enroll
POST /api/v5/enterprise-identity/mfa/verify
POST /api/v5/enterprise-identity/sessions
GET /api/v5/enterprise-identity/sessions
POST /api/v5/enterprise-identity/sessions/{sessionId}/revoke
POST /api/v5/enterprise-identity/account-protection/login-attempt
POST /api/v5/enterprise-identity/password-policy/check
```

Full validation:

```powershell
.\tools\v5\validate-p07-p08.ps1
```
