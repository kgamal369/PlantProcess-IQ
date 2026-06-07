# Phase 03 Clean-Machine-to-Login Runbook

Marker: PPIQ_REALIZATION_T019_CLEAN_MACHINE_TO_LOGIN

Goal: on a fresh VM, a reviewer can reach a working login using the runbook.

Steps:

1. Install Docker and Docker Compose plugin.
2. Clone repository.
3. Create deploy/server/.env.production from deploy/server/.env.example.
4. Set SITE_HOST, ACME_EMAIL, POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD, PPIQ_API_IMAGE, PPIQ_WEB_IMAGE.
5. Run: powershell -ExecutionPolicy Bypass -File .\deploy\server\clean-machine-to-login.ps1
6. Confirm health, login, HTTPS, and external Postgres closure.
