# PlantProcess IQ Demo Deployment Exposure Policy

## PPIQ-T208 loopback-binding decision

The demo deployment is intentionally single-tenant and customer-pilot oriented. Only public HTTP/S traffic should enter through Caddy.

| Service | Published externally? | Binding decision | Reason |
|---|---:|---|---|
| Caddy | Yes | 0.0.0.0:80, 0.0.0.0:443 | Public reverse proxy for website, app and API. |
| PostgreSQL | No | 127.0.0.1:${POSTGRES_PORT:-5432}:5432 | Database must never be public. |
| Jenkins | Limited | 127.0.0.1:${JENKINS_PORT:-9090}:8080 or Caddy-protected admin route | Build console must not expose app/data services. |
| API | No direct host port | Internal Docker network only; Caddy routes public API traffic. |
| Workers | No | Internal only | Background processor. |
| App web | No | Internal only; Caddy routes traffic | Static SPA behind Caddy. |
| Website | No | Internal only; Caddy routes traffic | Public site through Caddy only. |
| Backup runner | No | Internal only | No inbound access needed. |

## External scan acceptance

For the current server, the expected external scan is:

nmap -Pn 178.105.152.180

Expected open ports:

- 80
- 443
- 9090 only if intentionally exposed and protected

All data, observability, database, source-system and internal service ports must be filtered or closed externally.

## Operational rule

If a service does not need public inbound traffic, it must remain either unbound to the host or bound to 127.0.0.1 only.

This protects PlantProcess IQ from bypassing Caddy, TLS, auth headers, request logging, and rate-limit/security controls.
