# PlantProcess IQ - Tracked Secret Separation

Generated at: 2026-06-04 13:04:42

This report separates acceptable local ignored secrets from dangerous tracked committed config.

Secret values are not printed.

## Summary by Final Risk

| Final Risk | Count |
|---|---:|
| ACCEPTABLE_LOCAL_IGNORED | 5 |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | 6 |
| BLOCKER_TRACKED_HIGH | 16 |
| LOW_INFORMATIONAL | 6 |
| WARN_TRACKED_MEDIUM | 2 |

## Details

| Final Risk | Tracked | File Kind | Path | Pattern | Required Action |
|---|---|---|---|---|---|
| ACCEPTABLE_LOCAL_IGNORED | NO | LOCAL_IGNORED_RUNTIME | env\profiles\local.env | connection-string-password | Allowed only if untracked and ignored. |
| ACCEPTABLE_LOCAL_IGNORED | NO | LOCAL_IGNORED_RUNTIME | env\profiles\local.env | known-development-secret | Allowed only if untracked and ignored. |
| ACCEPTABLE_LOCAL_IGNORED | NO | LOCAL_IGNORED_RUNTIME | env\profiles\local.env | password | Allowed only if untracked and ignored. |
| ACCEPTABLE_LOCAL_IGNORED | NO | LOCAL_FRONTEND_ENV | Frontend\PlantProcess.Web\.env.local | known-development-secret | Allowed only if untracked and ignored. |
| ACCEPTABLE_LOCAL_IGNORED | NO | LOCAL_FRONTEND_ENV | Frontend\PlantProcess.Web\.env.local | password | Allowed only if untracked and ignored. |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | YES | EXAMPLE_OR_TEMPLATE | Backend\.env.example | password | Allowed only with placeholders. No real local/server secrets. |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | YES | EXAMPLE_OR_TEMPLATE | env\profiles\customer-template.env.example | password | Allowed only with placeholders. No real local/server secrets. |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | YES | EXAMPLE_OR_TEMPLATE | env\profiles\local.env.example | password | Allowed only with placeholders. No real local/server secrets. |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | YES | EXAMPLE_OR_TEMPLATE | env\profiles\server.env.example | password | Allowed only with placeholders. No real local/server secrets. |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | YES | EXAMPLE_OR_TEMPLATE | env\profiles\server-docker.env.example | password | Allowed only with placeholders. No real local/server secrets. |
| ACCEPTABLE_TEMPLATE_PLACEHOLDER | YES | EXAMPLE_OR_TEMPLATE | env\profiles\test.env.example | password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | Backend\.env.example | connection-string-password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | COMMITTED_APP_CONFIG | Backend\PlantProcess.Api\appsettings.Development.json | connection-string-password | Must not contain real secrets. Replace with placeholders and env overrides. |
| BLOCKER_TRACKED_HIGH | YES | COMMITTED_APP_CONFIG | Backend\PlantProcess.Api\appsettings.Development.json | signing-key | Must not contain real secrets. Replace with placeholders and env overrides. |
| BLOCKER_TRACKED_HIGH | YES | COMMITTED_APP_CONFIG | Backend\PlantProcess.Api\appsettings.json | signing-key | Must not contain real secrets. Replace with placeholders and env overrides. |
| BLOCKER_TRACKED_HIGH | YES | UNKNOWN | Backend\PlantProcess.Api\Properties\launchSettings.json | connection-string-password | Review manually. |
| BLOCKER_TRACKED_HIGH | YES | UNKNOWN | Backend\PlantProcess.Api\Properties\launchSettings.json | known-development-secret | Review manually. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\customer-template.env.example | connection-string-password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\customer-template.env.example | known-development-secret | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\local.env.example | connection-string-password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\local.env.example | known-development-secret | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\server.env.example | connection-string-password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\server.env.example | known-development-secret | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\server-docker.env.example | connection-string-password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\server-docker.env.example | known-development-secret | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\test.env.example | connection-string-password | Allowed only with placeholders. No real local/server secrets. |
| BLOCKER_TRACKED_HIGH | YES | EXAMPLE_OR_TEMPLATE | env\profiles\test.env.example | known-development-secret | Allowed only with placeholders. No real local/server secrets. |
| LOW_INFORMATIONAL | YES | EXAMPLE_OR_TEMPLATE | Backend\.env.example | local-url | Allowed only with placeholders. No real local/server secrets. |
| LOW_INFORMATIONAL | YES | COMMITTED_APP_CONFIG | Backend\PlantProcess.Api\appsettings.Development.json | local-url | Must not contain real secrets. Replace with placeholders and env overrides. |
| LOW_INFORMATIONAL | YES | UNKNOWN | Backend\PlantProcess.Api\Properties\launchSettings.json | local-url | Review manually. |
| LOW_INFORMATIONAL | NO | LOCAL_IGNORED_RUNTIME | env\profiles\local.env | local-url | Allowed only if untracked and ignored. |
| LOW_INFORMATIONAL | YES | EXAMPLE_OR_TEMPLATE | env\profiles\local.env.example | local-url | Allowed only with placeholders. No real local/server secrets. |
| LOW_INFORMATIONAL | NO | LOCAL_FRONTEND_ENV | Frontend\PlantProcess.Web\.env.local | local-url | Allowed only if untracked and ignored. |
| WARN_TRACKED_MEDIUM | YES | COMMITTED_APP_CONFIG | Backend\PlantProcess.Api\appsettings.Development.json | password | Must not contain real secrets. Replace with placeholders and env overrides. |
| WARN_TRACKED_MEDIUM | YES | UNKNOWN | Backend\PlantProcess.Api\Properties\launchSettings.json | password | Review manually. |
