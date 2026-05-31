# PlantProcess IQ Demo Deployment Exposure Policy

## PPIQ-T208 loopback-binding decision

The demo deployment is intentionally single-tenant and customer-pilot oriented. Only public HTTP/S traffic should enter through Caddy.

| Service | Published externally? | Binding decision | Reason |
|---|---:|---|---|
| Caddy | Yes | 0.0.0.0:80, 0.0.0.0:443 | Public reverse proxy for website, app and API. |
| PostgreSQL | No | 127.0.0.1:${POSTGRES_PORT:-5432}:5432 | Database must never be public. |
| Jenkins | No direct public host port by default | Internal Docker network only; expose only through protected Caddy route or SSH tunnel | Build console must not expose app/data services. |
| API | No direct host port | Internal Docker network only; Caddy routes public API traffic. | Prevents bypass of TLS/auth/logging controls. |
| Workers | No | Internal only | Background processor. |
| App web | No | Internal only; Caddy routes traffic | Static SPA behind Caddy. |
| Website | No | Internal only; Caddy routes traffic | Public site through Caddy only. |
| Backup runner | No | Internal only | No inbound access needed. |

## External scan acceptance

Expected external scan from outside the server/VPN:

    nmap -Pn 178.105.152.180

Acceptance result for the normal public demo posture:

- 80/tcp open
- 443/tcp open
- 5432/tcp closed or filtered
- 5063/tcp closed or filtered
- 5173/tcp closed or filtered
- 8080/tcp closed or filtered
- 9090/tcp closed or filtered unless Jenkins is intentionally exposed behind authentication
- Observability/data/internal ports closed or filtered

## Local static validation

Run:

    node tools/validation/validate-t208-exposure.cjs

This catches accidental Compose host-port exposure before deployment. The external nmap proof must still be captured from a machine outside the host.
