# PlantProcess IQ — Doctrine v5 P11/P12 Runbook

## P11 — Outbound Notifications, Closed Loop & Lead System

Installed capabilities:

- Backend lead capture replacing website localStorage capture.
- GDPR/contact consent enforcement.
- Honeypot spam protection.
- Fit score and spam score.
- Mock SMTP/webhook notification channel registry.
- Notification preferences per event/role.
- Notification delivery queue/log.
- Mock delivery processor for CI/local validation.
- Closed-loop suggestion action outcome measurement.
- Honesty rule: association tracking only, no causation claim.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p11_acceptance();
```

API smoke:

```http
GET /api/v5/outbound/health
POST /api/v5/outbound/leads
GET /api/v5/outbound/leads
POST /api/v5/outbound/notifications/trigger
POST /api/v5/outbound/notifications/process-mock
POST /api/v5/outbound/suggestions/outcomes
GET /api/v5/outbound/suggestions/outcomes/kpi
```

## P12 — Internationalization, RTL & Mobile

Installed capabilities:

- EN/DE/AR locale registry.
- Arabic RTL catalog.
- String key/translation catalog.
- React i18n provider.
- Language switcher proof page.
- RTL direction mirroring proof.
- Mobile/tablet readiness contract.
- 44px+ consume-and-act touch target proof.

SQL acceptance:

```sql
SELECT * FROM public.ppiq_v5_p12_acceptance();
```

Full validation:

```powershell
.\tools\v5\validate-p11-p12.ps1
```
