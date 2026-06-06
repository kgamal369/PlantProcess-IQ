# PlantProcess IQ OT-Safe Edge Agent Deployment

Marker: PPIQ_PACK_F3_EDGE_AGENT_PACKAGING

## Scope

This package provides a deployment-ready reference edge agent wrapper for the OT-safe one-way push pattern.

The package is intentionally conservative:

- It is read-only toward source systems.
- It is outbound-only toward PlantProcess IQ.
- It opens no inbound listener in the OT network.
- It uses local spool/queue files for outage tolerance.
- It references secrets; it does not hardcode them.

## Local dry run

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\edge-agent\Run-PPIQ-EdgeAgent-Local.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ"
```

## Local push mode

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\edge-agent\Run-PPIQ-EdgeAgent-Local.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -Push
```

## Docker dry run

```powershell
docker compose -f .\deploy\edge-agent\docker-compose.edge-agent.yml up --build
```

## Service wrapper

Use an approved service wrapper such as WinSW, NSSM, systemd, Kubernetes, or enterprise deployment tooling. The script `Install-PPIQ-EdgeAgent-Service.ps1` prints the approved command payload but does not silently install a persistent service.
