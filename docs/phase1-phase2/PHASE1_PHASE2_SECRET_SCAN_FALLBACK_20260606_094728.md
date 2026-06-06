# Phase 1/2 fallback secret scan report

Generated: 2026-06-06 09:47:28

Fallback scanner scope:

- Scans active config/runtime files, deploy compose files, deploy scripts, and env examples.
- Skips local-only env files and generated documentation.
- Skips source-code implementation files to avoid false positives on property names.
- Uses gitleaks automatically when installed.

Scanned files: 35
Findings: 17

## Findings
- Backend\PlantProcess.Api\appsettings.Development.json:19 -> possible hardcoded BootstrapAdminPassword [Ad****3!]
- Backend\PlantProcess.Api\appsettings.Development.json:23 -> possible hardcoded Password [Ad****3!]
- Backend\PlantProcess.Api\appsettings.Development.json:29 -> possible hardcoded Password [En****3!]
- Backend\PlantProcess.Api\appsettings.Development.json:35 -> possible hardcoded Password [Da****3!]
- Backend\PlantProcess.Api\appsettings.Development.json:41 -> possible hardcoded Password [Vi****3!]
- deploy\demo-sources\docker-compose.demo-sources.yml:112 -> possible hardcoded MYSQL_PASSWORD [pa****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:113 -> possible hardcoded MYSQL_ROOT_PASSWORD [pa****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:22 -> possible hardcoded POSTGRES_PASSWORD [me****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:38 -> possible hardcoded ORACLE_PASSWORD [ca****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:40 -> possible hardcoded APP_USER_PASSWORD [ca****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:56 -> possible hardcoded ORACLE_PASSWORD [hs****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:58 -> possible hardcoded APP_USER_PASSWORD [hs****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:75 -> possible hardcoded MSSQL_SA_PASSWORD [Pp****g!]
- deploy\demo-sources\docker-compose.demo-sources.yml:93 -> possible hardcoded MYSQL_PASSWORD [do****wd]
- deploy\demo-sources\docker-compose.demo-sources.yml:94 -> possible hardcoded MYSQL_ROOT_PASSWORD [do****wd]
- deploy\identity\keycloak-reference-compose.yml:7 -> possible hardcoded KEYCLOAK_ADMIN_PASSWORD [ad****in]
- deploy\server\docker-compose.demo.yml:8 -> possible hardcoded POSTGRES_PASSWORD [pl****sk]
