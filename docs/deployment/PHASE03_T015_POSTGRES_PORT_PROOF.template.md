# T-015 Postgres External Port Closure Proof

Marker: PPIQ_REALIZATION_T015_CLOSE_EXPOSED_POSTGRES_PORT

Required external proof:

    powershell -ExecutionPolicy Bypass -File .\tools\deploy\Test-ExternalPostgresPortClosed.ps1 -ServerHost <server-ip-or-hostname>

Expected:

    PPIQ-T015 passed: external Postgres port is refused/timed out.

Internal app proof:

    docker ps --format 'table {{.Names}}\t{{.Ports}}'

Postgres must not publish:

    0.0.0.0:5432->5432/tcp

Status: repo-side assets created. Server-side proof must be attached during deployment acceptance.
