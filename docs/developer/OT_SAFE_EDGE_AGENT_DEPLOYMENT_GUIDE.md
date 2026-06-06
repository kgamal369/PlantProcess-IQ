# OT-Safe Edge Agent Deployment Guide

Marker: PPIQ_PACK_F3_EDGE_AGENT_PACKAGING

## Deployment modes

1. Local dry run for demo validation.
2. Local push mode against a running PlantProcess IQ API.
3. Docker dry-run package.
4. Enterprise service wrapper deployment.

## Safety checks before deployment

- Confirm `readOnlyCollection=true`.
- Confirm `outboundOnly=true`.
- Confirm `opensInboundListener=false`.
- Confirm no hardcoded secret exists in config.
- Confirm network route is edge -> PlantProcess IQ only.
- Confirm source profiles use read-only accounts or read-only folders/views.

## Files

- `tools/edge-agent/ppiq-edge-agent.cjs`
- `tools/edge-agent/edge-agent.sample.json`
- `scripts/edge-agent/Run-PPIQ-EdgeAgent-Local.ps1`
- `scripts/edge-agent/Install-PPIQ-EdgeAgent-Service.ps1`
- `scripts/edge-agent/Uninstall-PPIQ-EdgeAgent-Service.ps1`
- `deploy/edge-agent/Dockerfile`
- `deploy/edge-agent/docker-compose.edge-agent.yml`
- `deploy/edge-agent/.env.edge-agent.template`
