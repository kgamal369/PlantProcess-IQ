# P3-T18 — Least-Privilege Read-Only DB Role for Query/Preview

Marker: `PPIQ_P3_T18_READONLY_QUERY_PREVIEW_ROLE`

P3-T18 introduces a DB role contract for safe query-preview execution.

The query/preview runtime must not use the app owner account. It must use a restricted read-only role.

## Roles

| Role | Login | Purpose |
|---|---:|---|
| `ppiq_query_preview_readonly` | No | Group role with restricted SELECT-only access |
| `ppiq_query_preview_login` | Optional | Login role for runtime query-preview connection strings |

## Privilege rules

The read-only role is not superuser, cannot create DBs, cannot create roles, cannot replicate, cannot bypass RLS, has no write privileges on public base tables, and has no broad base-table `SELECT`.

## Environment policy

### Local laptop

The main PlantProcess IQ PostgreSQL DB is native Windows PostgreSQL. Apply with local `psql.exe` using an admin/owner DB account.

### Server

The main PlantProcess IQ PostgreSQL DB is a Docker container. Apply through `docker exec` or the deployment migration runbook.

### Customer

Topology may vary. Customer DBA may apply the SQL manually; app config should be supplied through `PPIQ_QUERY_PREVIEW_CONNECTION_STRING`.

## Native local proof

Run:

    powershell -ExecutionPolicy Bypass -File tools\phase3\apply-p3-t18-local-native-db.ps1 -AdminUser postgres

Then check that every row from:

    SELECT * FROM public.ppiq_p3_t18_readonly_preview_role_status();

is green.