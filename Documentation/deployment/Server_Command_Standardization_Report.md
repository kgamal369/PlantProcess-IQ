# PlantProcess IQ - Server Command Standardization Report

Generated at: 2026-06-04 13:43:06

## Command contract

- server-dry-run.ps1 validates compose/env without starting services.
- server-up.ps1 starts services only when explicitly executed and requires ignored runtime env.
- server-down.ps1 stops services only when explicitly executed and requires ignored runtime env.
- server-status.ps1 is read-only and shows compose status.
- server-command-common.ps1 owns Docker/compose/env resolution.

## Gate Summary

| Status | Count |
|---|---:|
| OK | 15 |
| WARN | 1 |

## Command Summary

| Mode | Count |
|---|---:|
| Dry-run | 1 |
| Runtime | 2 |
| Runtime read-only | 1 |
| Shared | 1 |

## Gate Details

| Gate | Status | Evidence | Action |
|---|---|---|---|
| Demo source compose candidate exists | OK | deploy/demo-sources/docker-compose.demo-sources.yml exists. | No action. |
| Docker command resolvable for server commands | OK | C:\Program Files\Docker\Docker\resources\bin\docker.exe | No action. |
| Pack 3C prerequisite | OK | Latest Pack3C gate is green. | No action. |
| Server command script exists | OK | scripts\deploy\server-command-common.ps1 | No action. |
| Server command script exists | OK | scripts\deploy\server-down.ps1 | No action. |
| Server command script exists | OK | scripts\deploy\server-dry-run.ps1 | No action. |
| Server command script exists | OK | scripts\deploy\server-status.ps1 | No action. |
| Server command script exists | OK | scripts\deploy\server-up.ps1 | No action. |
| Server command script parses | OK | scripts\deploy\server-command-common.ps1 | No action. |
| Server command script parses | OK | scripts\deploy\server-down.ps1 | No action. |
| Server command script parses | OK | scripts\deploy\server-dry-run.ps1 | No action. |
| Server command script parses | OK | scripts\deploy\server-status.ps1 | No action. |
| Server command script parses | OK | scripts\deploy\server-up.ps1 | No action. |
| Server command scripts contain no real secrets | OK | No unsafe secret-like patterns found. | No action. |
| Server dry-run command executes for demo compose | OK | Dry-run succeeded. Output: C:\Workspace\PlantProcess-IQ\Documentation\deployment\Pack3D_ServerDryRunOutput_20260604_134254.txt | No action. |
| Server app compose candidate exists | WARN | No deploy/server/docker-compose.server.yml or deploy/docker-compose.yml found. | Pack 3E should standardize the app compose file. |
