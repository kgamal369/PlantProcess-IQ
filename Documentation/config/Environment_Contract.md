# PlantProcess IQ - Environment Contract

This contract defines the standard environment variable schema for local, server, and customer deployments.

Rule: the key names stay the same everywhere; only values change by profile or secret store.

| Key | Owner | Required For | Secret Level | Purpose | Rule |
|---|---|---|---|---|---|
| ASPNETCORE_ENVIRONMENT | API | local/server/customer | PUBLIC_CONFIG | ASP.NET runtime environment. | Must come from env/profile. |
| ASPNETCORE_URLS | API | local/server/customer | PUBLIC_CONFIG | API binding URL. | Port must be profile-driven. |
| ConnectionStrings__PlantProcessDb | API Database | local/server/customer | SECRET | Final .NET connection string override. | Must be env-owned or derived consistently. |
| PlantProcess__Auth__AccessTokenMinutes | API Auth | local/server/customer | PUBLIC_CONFIG | Access token lifetime. | Profile-driven. |
| PlantProcess__Auth__SigningKey | API Auth | local/server/customer | SECRET | JWT signing key. | Must never be committed as real value. |
| PLANTPROCESS_ALLOWED_ORIGINS | API Security | local/server/customer | PUBLIC_CONFIG | CORS allowed origins. | Must be env-driven, never hardcoded per machine. |
| POSTGRES_APP_DB | Database | local/server/customer | PUBLIC_CONFIG | Application DB name. | Profile-driven. |
| POSTGRES_HOST | Database | local/server/customer | PUBLIC_CONFIG | PostgreSQL host. | Never edit appsettings to switch host. |
| POSTGRES_PASSWORD | Database | local/server/customer | SECRET | Application DB password. | Must be ignored/env/secret only. |
| POSTGRES_PORT | Database | local/server/customer | PUBLIC_CONFIG | PostgreSQL port. | Never edit appsettings to switch port. |
| POSTGRES_USER | Database | local/server/customer | SENSITIVE | Application DB user. | Do not hardcode in appsettings. |
| PPIQ_BOOTSTRAP_ADMIN_PASSWORD | Bootstrap Auth | local/server/customer | SECRET | Initial admin password. | Must never be committed as real value. |
| PPIQ_BOOTSTRAP_ADMIN_USER | Bootstrap Auth | local/server/customer | SENSITIVE | Initial admin username. | Profile/secret-driven. |
| PPIQ_DEMO_SOURCES_MODE | Runtime | local/server/customer | PUBLIC_CONFIG | Defines whether demo source systems run through Docker. | Demo sources must stay optional and profile-driven. |
| PPIQ_MAIN_DB_MODE | Runtime | local/server/customer | PUBLIC_CONFIG | Defines whether main app DB is native, docker, or external. | Do not hardcode DB mode in scripts. |
| PPIQ_RUNTIME_TOPOLOGY | Runtime | local/server/customer | PUBLIC_CONFIG | Selects runtime mode such as local-hybrid or server-docker. | Same key everywhere; value changes by profile only. |
| PPIQ_SMOKE_PASSWORD | Testing | local/server/customer | SECRET | Smoke test login password. | Ignored/env/secret only. |
| PPIQ_SMOKE_USERNAME | Testing | local/server/customer | SENSITIVE | Smoke test login username. | Profile-driven. |
| PPIQ_START_DEMO_SOURCES | Runtime | local/server/customer | PUBLIC_CONFIG | Controls whether demo source containers start. | Profile controls demo source startup. |
| PPIQ_START_MAIN_DB | Runtime | local/server/customer | PUBLIC_CONFIG | Controls whether start-local starts main DB. | Profile controls startup behavior. |
| VITE_API_BASE_URL | Frontend | local/server/customer | PUBLIC_CONFIG | Frontend API base URL. | Frontend and API profile values must match. |
| VITE_PORT | Frontend | local | PUBLIC_CONFIG | Local Vite port. | Local only unless explicitly needed. |
