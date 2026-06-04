# PlantProcess IQ - Secret and Config Scan

Values are intentionally not printed. This report lists only file paths, risk categories, and cleanup actions.

| Risk | Path | Pattern | Reason | Action |
|---|---|---|---|---|
| HIGH | Backend\.env.example | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | Backend\PlantProcess.Api\appsettings.Development.json | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | Backend\PlantProcess.Api\appsettings.Development.json | signing-key | Signing key appears in committed/config file. | Use PlantProcess__Auth__SigningKey from env/secret only. |
| HIGH | Backend\PlantProcess.Api\appsettings.json | signing-key | Signing key appears in committed/config file. | Use PlantProcess__Auth__SigningKey from env/secret only. |
| HIGH | Backend\PlantProcess.Api\Properties\launchSettings.json | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | Backend\PlantProcess.Api\Properties\launchSettings.json | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | env\profiles\customer-template.env.example | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | env\profiles\customer-template.env.example | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | env\profiles\local.env | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | env\profiles\local.env | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | env\profiles\local.env.example | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | env\profiles\local.env.example | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | env\profiles\server.env.example | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | env\profiles\server.env.example | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | env\profiles\server-docker.env.example | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | env\profiles\server-docker.env.example | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | env\profiles\test.env.example | connection-string-password | Connection string includes password. | Use env-only ConnectionStrings__PlantProcessDb or generated runtime value. |
| HIGH | env\profiles\test.env.example | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| HIGH | Frontend\PlantProcess.Web\.env.local | known-development-secret | Known development password or placeholder-like secret detected. | Move real value to ignored env/secret store; committed files should use safe placeholders only. |
| LOW | Backend\.env.example | local-url | Local URL found. This is acceptable only in local/example profile. | Ensure server profile uses production URL values. |
| LOW | Backend\PlantProcess.Api\appsettings.Development.json | local-url | Local URL found. This is acceptable only in local/example profile. | Ensure server profile uses production URL values. |
| LOW | Backend\PlantProcess.Api\Properties\launchSettings.json | local-url | Local URL found. This is acceptable only in local/example profile. | Ensure server profile uses production URL values. |
| LOW | env\profiles\local.env | local-url | Local URL found. This is acceptable only in local/example profile. | Ensure server profile uses production URL values. |
| LOW | env\profiles\local.env.example | local-url | Local URL found. This is acceptable only in local/example profile. | Ensure server profile uses production URL values. |
| LOW | Frontend\PlantProcess.Web\.env.local | local-url | Local URL found. This is acceptable only in local/example profile. | Ensure server profile uses production URL values. |
| MEDIUM | Backend\.env.example | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | Backend\PlantProcess.Api\appsettings.Development.json | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | Backend\PlantProcess.Api\Properties\launchSettings.json | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | env\profiles\customer-template.env.example | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | env\profiles\local.env | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | env\profiles\local.env.example | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | env\profiles\server.env.example | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | env\profiles\server-docker.env.example | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | env\profiles\test.env.example | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
| MEDIUM | Frontend\PlantProcess.Web\.env.local | password | Password-like config detected. | Confirm file is ignored or replace committed value with placeholder. |
