# T-007 Clean Machine Deploy Acceptance

Marker: PPIQ_REALIZATION_T007_CLEAN_MACHINE_DEPLOY_ACCEPTANCE

This gate proves that a clean machine/server deployment uses only the canonical deploy layout:

- deploy/compose
- deploy/caddy/Caddyfile
- no DO_NOT_DEPLOY, local repair, or test-only scripts under deploy paths
- compose files pass docker compose config
- app/API respond with controlled statuses

## Local structural proof

Run this local structural proof:

cd C:\Workspace\PlantProcess-IQ
.\scripts\deploy\Invoke-CleanMachineDeployAcceptance.ps1 -SkipHttp

## Server proof

Only run server proof after replacing BaseUrl and ApiUrl with real deployed domains.

Example:

.\scripts\deploy\Invoke-CleanMachineDeployAcceptance.ps1 -BaseUrl https://app.real-domain.example -ApiUrl https://api.real-domain.example