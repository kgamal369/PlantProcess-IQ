# PlantProcess IQ Deployment

This is the canonical deployment root for PlantProcess IQ.

## Structure

- deploy/server — server env examples and server helper scripts.
- deploy/caddy — canonical Caddy reverse-proxy config.
- deploy/demo-sources — demo source-system Docker compose.
- deploy/ci — CI/CD support files.
- deploy/airgap, deploy/dr, deploy/export, deploy/identity — specialized deployment/security assets.

## Rule

Do not create new root-level deployment folders.
Do not keep duplicate Caddyfiles.
Do not keep root-level demo compose files unless a CI tool explicitly requires them.

## Local development

Use:

`powershell
.\scripts\run\start-local.ps1 -Profile local -StartDb -StartDemoSources -FreePorts
`",
    ",
    

Use a server profile/env file and the canonical deploy folder only.
