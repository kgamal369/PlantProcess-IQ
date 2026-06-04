# PlantProcess IQ - Pack 2C Config Placeholder Cleanup

Generated at: 2026-06-04 13:06:59

Tracked config/example files were sanitized so real local/server secrets belong in ignored env files only.

Backups were created outside the repository: C:\Workspace\PlantProcess-IQ_Archive\Pack2C_ConfigPlaceholders_20260604_130654

## Changed Files

| Path | Action |
|---|---|
| Backend\.env.example | Cleaned env/example placeholders and removed connection-string password pattern. |
| Backend\PlantProcess.Api\appsettings.Development.json | Cleaned tracked JSON config secrets/placeholders. |
| Backend\PlantProcess.Api\appsettings.json | Cleaned tracked JSON config secrets/placeholders. |
| Backend\PlantProcess.Api\Properties\launchSettings.json | Cleaned tracked JSON config secrets/placeholders. |
| env\profiles\customer-template.env.example | Cleaned env/example placeholders and removed connection-string password pattern. |
| env\profiles\local.env.example | Cleaned env/example placeholders and removed connection-string password pattern. |
| env\profiles\server.env.example | Cleaned env/example placeholders and removed connection-string password pattern. |
| env\profiles\server-docker.env.example | Cleaned env/example placeholders and removed connection-string password pattern. |
| env\profiles\test.env.example | Cleaned env/example placeholders and removed connection-string password pattern. |

## Post-Cleanup Scan Summary

| Risk | Count |
|---|---:|
| HIGH | 11 |
| LOW | 5 |
| OK_PLACEHOLDER | 6 |

## Post-Cleanup Findings

| Risk | Path | Pattern | Action |
|---|---|---|---|
| HIGH | Backend\.env.example | connection-string-password | Tracked config still contains a connection string with Password=. |
| HIGH | env\profiles\customer-template.env.example | connection-string-password | Tracked config still contains a connection string with Password=. |
| HIGH | env\profiles\customer-template.env.example | env-connection-string-password | Tracked env example still contains connection string password. |
| HIGH | env\profiles\local.env.example | connection-string-password | Tracked config still contains a connection string with Password=. |
| HIGH | env\profiles\local.env.example | env-connection-string-password | Tracked env example still contains connection string password. |
| HIGH | env\profiles\server.env.example | connection-string-password | Tracked config still contains a connection string with Password=. |
| HIGH | env\profiles\server.env.example | env-connection-string-password | Tracked env example still contains connection string password. |
| HIGH | env\profiles\server-docker.env.example | connection-string-password | Tracked config still contains a connection string with Password=. |
| HIGH | env\profiles\server-docker.env.example | env-connection-string-password | Tracked env example still contains connection string password. |
| HIGH | env\profiles\test.env.example | connection-string-password | Tracked config still contains a connection string with Password=. |
| HIGH | env\profiles\test.env.example | env-connection-string-password | Tracked env example still contains connection string password. |
| LOW | Backend\.env.example | local-url-or-host | Local URL/host remains. Acceptable only in local/example files. |
| LOW | Backend\PlantProcess.Api\appsettings.Development.json | local-url-or-host | Local URL/host remains. Acceptable only in local/example files. |
| LOW | Backend\PlantProcess.Api\Properties\launchSettings.json | local-url-or-host | Local URL/host remains. Acceptable only in local/example files. |
| LOW | env\profiles\local.env.example | local-url-or-host | Local URL/host remains. Acceptable only in local/example files. |
| LOW | env\profiles\test.env.example | local-url-or-host | Local URL/host remains. Acceptable only in local/example files. |
| OK_PLACEHOLDER | Backend\.env.example | password-placeholder | Password key remains only as ignored-env placeholder. |
| OK_PLACEHOLDER | env\profiles\customer-template.env.example | password-placeholder | Password key remains only as ignored-env placeholder. |
| OK_PLACEHOLDER | env\profiles\local.env.example | password-placeholder | Password key remains only as ignored-env placeholder. |
| OK_PLACEHOLDER | env\profiles\server.env.example | password-placeholder | Password key remains only as ignored-env placeholder. |
| OK_PLACEHOLDER | env\profiles\server-docker.env.example | password-placeholder | Password key remains only as ignored-env placeholder. |
| OK_PLACEHOLDER | env\profiles\test.env.example | password-placeholder | Password key remains only as ignored-env placeholder. |
